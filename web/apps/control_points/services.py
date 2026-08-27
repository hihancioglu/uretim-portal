from decimal import Decimal
from django.db import IntegrityError, transaction
from django.db.models import Max
from django.utils import timezone
from apps.accounts.authz import require_scoped_action
from apps.audit.services import create_audit_event
from apps.drawings.models import DrawingRevision
from .models import ControlPoint, ControlPointVersion


class ControlPointError(Exception):
    pass


def _authorize(actor, revision):
    require_scoped_action(
        actor, "drawings.manage", scope_type="DRAWING", scope_key=revision.drawing.scope
    )


def _validate_revision(revision):
    if revision.status not in (
        DrawingRevision.Status.DRAFT,
        DrawingRevision.Status.ACTIVE,
    ):
        raise ControlPointError("Bu revizyon artık değiştirilemez.")


def _definition(data):
    nominal = data["nominal"]
    lower = -abs(data["lower_tolerance"])
    upper = abs(data["upper_tolerance"])
    values = {
        "measure_code": data["measure_code"].strip(),
        "measure_name": data["measure_name"].strip(),
        "nominal": nominal,
        "lower_tolerance": lower,
        "upper_tolerance": upper,
        "lower_limit": nominal - abs(lower),
        "upper_limit": nominal + abs(upper),
        "unit": (data.get("unit") or "mm").strip(),
        "page_no": data["page_no"],
        "x_ratio": data["x_ratio"],
        "y_ratio": data["y_ratio"],
        "is_mandatory": data.get("is_mandatory", False),
        "measurement_group": (data.get("measurement_group") or "Genel").strip(),
        "sample_frequency": (data.get("sample_frequency") or "Her Kontrol").strip(),
        "is_critical": data.get("is_critical", False),
        "sort_no": data.get("sort_no") or 0,
        "change_reason": data.get("change_reason", "").strip(),
    }
    if not values["measure_code"] or not values["measure_name"]:
        raise ControlPointError("Kontrol noktası tanımı eksik.")
    for key in (
        "nominal",
        "lower_tolerance",
        "upper_tolerance",
        "lower_limit",
        "upper_limit",
    ):
        value = values[key]
        if (
            not isinstance(value, Decimal)
            or value.as_tuple().exponent < -5
            or value.adjusted() > 8
        ):
            raise ControlPointError("Tolerans değeri geçersiz.")
    return values


def _audit(actor, event_type, obj, metadata):
    create_audit_event(
        actor=actor,
        actor_snapshot=actor.get_username(),
        event_type=event_type,
        entity_type="control_point",
        entity_id=str(obj.pk),
        metadata=metadata,
    )


def _reject_duplicate(revision, measure_code, *, exclude=None):
    query = ControlPointVersion.objects.filter(
        drawing_revision=revision, is_active=True, measure_code=measure_code
    )
    if exclude:
        query = query.exclude(control_point=exclude)
    if query.exists():
        raise ControlPointError(
            "Bu teknik resimde aynı kodla aktif bir kontrol noktası zaten var."
        )


def create_control_point(*, actor, drawing_revision, data):
    values = _definition(data)
    try:
        with transaction.atomic():
            revision = (
                DrawingRevision.objects.select_for_update()
                .select_related("drawing")
                .get(pk=drawing_revision.pk)
            )
            _authorize(actor, revision)
            _validate_revision(revision)
            _reject_duplicate(revision, values["measure_code"])
            point = ControlPoint.objects.create(
                drawing=revision.drawing,
                spc_key=values["measure_code"],
                logical_code=values["measure_code"],
                created_by=actor,
                updated_by=actor,
            )
            version = ControlPointVersion.objects.create(
                control_point=point,
                drawing_revision=revision,
                version_no=1,
                valid_from=timezone.now(),
                is_active=True,
                created_by=actor,
                updated_by=actor,
                **values,
            )
            _audit(
                actor,
                "control_point.created",
                point,
                {"drawing_id": str(revision.drawing_id), "spc_key": point.spc_key},
            )
            _audit(
                actor,
                "control_point.version_created",
                point,
                {
                    "version_id": str(version.id),
                    "version_no": 1,
                    "revision_id": str(revision.id),
                },
            )
            return version
    except IntegrityError as exc:
        raise ControlPointError(
            "Kontrol noktası kaydedilemedi; lütfen yeniden deneyin."
        ) from exc


def revise_control_point(*, actor, control_point, drawing_revision, data):
    values = _definition(data)
    try:
        with transaction.atomic():
            revision = (
                DrawingRevision.objects.select_for_update()
                .select_related("drawing")
                .get(pk=drawing_revision.pk)
            )
            _authorize(actor, revision)
            _validate_revision(revision)
            point = ControlPoint.objects.select_for_update().get(pk=control_point.pk)
            if point.drawing_id != revision.drawing_id:
                raise ControlPointError("Kontrol noktası bu teknik resme ait değil.")
            current = ControlPointVersion.objects.select_for_update().get(
                control_point=point, drawing_revision=revision, is_active=True
            )
            _reject_duplicate(revision, values["measure_code"], exclude=point)
            now = timezone.now()
            current.is_active = False
            current.valid_to = current.valid_to or now
            current.updated_by = actor
            current.save(
                update_fields=("is_active", "valid_to", "updated_by", "updated_at")
            )
            next_no = (
                ControlPointVersion.objects.filter(control_point=point).aggregate(
                    value=Max("version_no")
                )["value"]
                or 0
            ) + 1
            version = ControlPointVersion.objects.create(
                control_point=point,
                drawing_revision=revision,
                version_no=next_no,
                valid_from=now,
                is_active=True,
                created_by=actor,
                updated_by=actor,
                **values,
            )
            point.updated_by = actor
            point.save(update_fields=("updated_by", "updated_at"))
            _audit(
                actor,
                "control_point.revised",
                point,
                {
                    "from_version_id": str(current.id),
                    "to_version_id": str(version.id),
                    "version_no": next_no,
                },
            )
            _audit(
                actor,
                "control_point.version_created",
                point,
                {
                    "version_id": str(version.id),
                    "version_no": next_no,
                    "revision_id": str(revision.id),
                },
            )
            return version
    except ControlPointVersion.DoesNotExist as exc:
        raise ControlPointError("Aktif kontrol noktası bulunamadı.") from exc
    except IntegrityError as exc:
        raise ControlPointError(
            "Kontrol noktası kaydedilemedi; lütfen yeniden deneyin."
        ) from exc


def deactivate_control_point(*, actor, control_point, drawing_revision):
    with transaction.atomic():
        revision = (
            DrawingRevision.objects.select_for_update()
            .select_related("drawing")
            .get(pk=drawing_revision.pk)
        )
        _authorize(actor, revision)
        _validate_revision(revision)
        point = ControlPoint.objects.select_for_update().get(pk=control_point.pk)
        if point.drawing_id != revision.drawing_id:
            raise ControlPointError("Kontrol noktası bu teknik resme ait değil.")
        try:
            version = ControlPointVersion.objects.select_for_update().get(
                control_point=point, drawing_revision=revision, is_active=True
            )
        except ControlPointVersion.DoesNotExist as exc:
            raise ControlPointError("Aktif kontrol noktası bulunamadı.") from exc
        version.is_active = False
        version.valid_to = version.valid_to or timezone.now()
        version.updated_by = actor
        version.save(
            update_fields=("is_active", "valid_to", "updated_by", "updated_at")
        )
        _audit(
            actor,
            "control_point.version_deactivated",
            point,
            {"version_id": str(version.id), "revision_id": str(revision.id)},
        )
        return version


def copy_control_points_to_revision(actor, source_revision, target_revision):
    try:
        with transaction.atomic():
            revisions = {
                item.id: item
                for item in DrawingRevision.objects.select_for_update()
                .select_related("drawing")
                .filter(pk__in=(source_revision.pk, target_revision.pk))
            }
            source, target = (
                revisions[source_revision.pk],
                revisions[target_revision.pk],
            )
            _authorize(actor, target)
            if source.drawing_id != target.drawing_id:
                raise ControlPointError("Revizyonlar aynı teknik resme ait olmalıdır.")
            if target.status != DrawingRevision.Status.DRAFT:
                raise ControlPointError("Hedef revizyon taslak olmalıdır.")
            if ControlPointVersion.objects.filter(
                drawing_revision=target, is_active=True
            ).exists():
                raise ControlPointError(
                    "Hedef revizyonda aktif kontrol noktaları zaten var."
                )
            source_versions = list(
                ControlPointVersion.objects.select_for_update()
                .filter(drawing_revision=source, is_active=True)
                .select_related("control_point")
            )
            points = {
                p.id: p
                for p in ControlPoint.objects.select_for_update().filter(
                    pk__in=[v.control_point_id for v in source_versions]
                )
            }
            created = []
            for old in source_versions:
                point = points[old.control_point_id]
                next_no = (
                    ControlPointVersion.objects.filter(control_point=point).aggregate(
                        value=Max("version_no")
                    )["value"]
                    or 0
                ) + 1
                copied = ControlPointVersion.objects.create(
                    control_point=point,
                    drawing_revision=target,
                    version_no=next_no,
                    measure_code=old.measure_code,
                    measure_name=old.measure_name,
                    nominal=old.nominal,
                    lower_tolerance=old.lower_tolerance,
                    upper_tolerance=old.upper_tolerance,
                    lower_limit=old.lower_limit,
                    upper_limit=old.upper_limit,
                    unit=old.unit,
                    page_no=old.page_no,
                    x_ratio=old.x_ratio,
                    y_ratio=old.y_ratio,
                    is_mandatory=old.is_mandatory,
                    measurement_group=old.measurement_group,
                    sample_frequency=old.sample_frequency,
                    is_critical=old.is_critical,
                    sort_no=old.sort_no,
                    valid_from=timezone.now(),
                    change_reason=f"{source.revision_code} revizyonundan kopyalandı",
                    is_active=True,
                    created_by=actor,
                    updated_by=actor,
                )
                created.append(copied)
                _audit(
                    actor,
                    "control_point.version_created",
                    point,
                    {
                        "version_id": str(copied.id),
                        "version_no": next_no,
                        "revision_id": str(target.id),
                        "copied_from": str(old.id),
                    },
                )
            create_audit_event(
                actor=actor,
                actor_snapshot=actor.get_username(),
                event_type="control_points.copied_to_revision",
                entity_type="drawing_revision",
                entity_id=str(target.id),
                metadata={
                    "source_revision_id": str(source.id),
                    "target_revision_id": str(target.id),
                    "count": len(created),
                },
            )
            return created
    except KeyError as exc:
        raise ControlPointError("Revizyon bulunamadı.") from exc
    except IntegrityError as exc:
        raise ControlPointError(
            "Kontrol noktaları kopyalanamadı; lütfen yeniden deneyin."
        ) from exc
