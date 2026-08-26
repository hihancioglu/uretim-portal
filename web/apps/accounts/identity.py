import hashlib
from django.conf import settings
from django.contrib.auth import get_user_model
from django.db import transaction
from django.utils import timezone
from apps.audit.services import create_audit_event
from .models import ExternalIdentity

class IdentityDenied(Exception): pass

def _safe_subject(subject):
    return hashlib.sha256(subject.encode()).hexdigest()[:16]

def _audit(event_type, issuer, subject, **metadata):
    create_audit_event(event_type=event_type, metadata={"issuer": issuer, "subject_fingerprint": _safe_subject(subject), **metadata})

@transaction.atomic
def resolve_oidc_identity(*, issuer, subject, claims, auto_provision=None):
    if not issuer or not subject:
        raise IdentityDenied("Missing validated issuer or subject")
    now = timezone.now()
    try:
        identity = ExternalIdentity.objects.select_related("user").get(issuer=issuer, subject=subject)
    except ExternalIdentity.DoesNotExist:
        enabled = settings.OIDC_AUTO_PROVISION if auto_provision is None else auto_provision
        if not enabled:
            _audit("auth.oidc_login_denied", issuer, subject, reason="unknown_identity")
            raise IdentityDenied("Unknown external identity")
        return _provision(issuer, subject, claims, now)
    if not identity.user.is_active:
        _audit("auth.oidc_login_denied", issuer, subject, reason="inactive_user")
        raise IdentityDenied("Inactive user")
    identity.email_snapshot = claims.get("email", "")[:254]
    identity.preferred_username_snapshot = claims.get("preferred_username", "")[:150]
    identity.display_name_snapshot = claims.get("name", "")[:255]
    identity.last_seen_at = now
    identity.save(update_fields=["email_snapshot", "preferred_username_snapshot", "display_name_snapshot", "last_seen_at", "updated_at"])
    _audit("auth.oidc_login_succeeded", issuer, subject)
    return identity.user

def _provision(issuer, subject, claims, now):
    User = get_user_model()
    preferred = (claims.get("preferred_username") or "oidc-user").strip()[:120]
    email = (claims.get("email") or "").strip()
    if User.objects.filter(username__iexact=preferred).exists() or (email and User.objects.filter(email__iexact=email).exists()):
        _audit("auth.oidc_login_denied", issuer, subject, reason="attribute_collision")
        raise IdentityDenied("Ambiguous local attributes")
    suffix = hashlib.sha256(f"{issuer}|{subject}".encode()).hexdigest()[:12]
    username = f"{preferred}-{suffix}"[:150]
    user = User(username=username, email=email, first_name=(claims.get("given_name") or "")[:150], last_name=(claims.get("family_name") or "")[:150])
    user.set_unusable_password()
    user.save()
    ExternalIdentity.objects.create(user=user, issuer=issuer, subject=subject, preferred_username_snapshot=preferred, email_snapshot=email, display_name_snapshot=(claims.get("name") or "")[:255], first_seen_at=now, last_seen_at=now)
    _audit("auth.external_identity_provisioned", issuer, subject)
    _audit("auth.oidc_login_succeeded", issuer, subject)
    return user

@transaction.atomic
def link_identity(*, user, issuer, subject, actor=None):
    now = timezone.now()
    identity = ExternalIdentity.objects.create(user=user, issuer=issuer, subject=subject, first_seen_at=now, last_seen_at=now)
    _audit("auth.external_identity_linked", issuer, subject, linked_user_id=str(user.pk), actor_id=str(actor.pk) if actor else "")
    return identity
