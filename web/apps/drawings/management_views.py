from django.contrib import messages
from django.core.exceptions import PermissionDenied, ValidationError
from django.core.paginator import Paginator
from django.http import Http404
from django.shortcuts import get_object_or_404, redirect, render
from django.urls import reverse
from django.views.decorators.http import require_http_methods

from apps.accounts.authz import require_action
from apps.products.models import Product
from apps.products.services import create_product, deactivate_product, update_product

from .forms import (
    DrawingEditForm,
    DrawingForm,
    ProductForm,
    ReplaceFileForm,
    RevisionEditForm,
    RevisionForm,
)
from .models import Drawing, DrawingRevision
from .selectors import (
    get_drawing_for_management,
    list_drawings_for_product,
    list_products_for_management,
    list_revisions_for_drawing,
)
from .services import (
    activate_revision,
    create_drawing,
    create_drawing_revision_with_file,
    deactivate_drawing,
    encryption_scheme_for_upload,
    replace_draft_revision_file,
    update_draft_revision,
    update_drawing,
    withdraw_revision,
)


def _authorize(request):
    require_action(request.user, "drawings.manage")


def _product(request, product_id):
    _authorize(request)
    return get_object_or_404(Product, pk=product_id)


def _drawing(request, drawing_id):
    _authorize(request)
    try:
        return get_drawing_for_management(actor=request.user, drawing_id=drawing_id)
    except Drawing.DoesNotExist as exc:
        raise Http404 from exc


def _revision(request, revision_id):
    _authorize(request)
    return get_object_or_404(
        DrawingRevision.objects.select_related(
            "drawing", "drawing__product", "primary_file"
        ),
        pk=revision_id,
    )


def _apply_validation(form, exc):
    if hasattr(exc, "message_dict"):
        for field, errors in exc.message_dict.items():
            target = field if field in form.fields else None
            for error in errors:
                form.add_error(target, error)
    else:
        form.add_error(None, " ".join(exc.messages))


def _form_page(request, *, form, title, back_url, warning=""):
    return render(
        request,
        "drawings/manage/form.html",
        {"form": form, "title": title, "back_url": back_url, "warning": warning},
    )


def management_home(request):
    _authorize(request)
    query = request.GET.get("q", "").strip()
    page = Paginator(
        list_products_for_management(actor=request.user, query=query), 25
    ).get_page(request.GET.get("page"))
    return render(request, "drawings/manage/home.html", {"page": page, "query": query})


@require_http_methods(["GET", "POST"])
def product_create(request):
    _authorize(request)
    form = ProductForm(request.POST or None)
    if request.method == "POST" and form.is_valid():
        try:
            product = create_product(actor=request.user, **form.cleaned_data)
        except ValidationError as exc:
            _apply_validation(form, exc)
        else:
            messages.success(request, "Ürün oluşturuldu.")
            return redirect("drawings:manage-product-detail", product_id=product.id)
    return _form_page(
        request,
        form=form,
        title="Yeni Ürün",
        back_url=reverse("drawings:manage-home"),
    )


def product_detail(request, product_id):
    product = _product(request, product_id)
    drawings = list_drawings_for_product(actor=request.user, product=product)
    return render(
        request,
        "drawings/manage/product_detail.html",
        {"product": product, "drawings": drawings},
    )


@require_http_methods(["GET", "POST"])
def product_edit(request, product_id):
    product = _product(request, product_id)
    form = ProductForm(
        request.POST or None,
        initial={name: getattr(product, name) for name in ProductForm.base_fields},
    )
    if request.method == "POST" and form.is_valid():
        try:
            update_product(actor=request.user, product=product, **form.cleaned_data)
        except ValidationError as exc:
            _apply_validation(form, exc)
        else:
            messages.success(request, "Ürün güncellendi.")
            return redirect("drawings:manage-product-detail", product_id=product.id)
    return _form_page(
        request,
        form=form,
        title="Ürünü Düzenle",
        back_url=reverse("drawings:manage-product-detail", args=[product.id]),
    )


@require_http_methods(["GET", "POST"])
def product_deactivate(request, product_id):
    product = _product(request, product_id)
    if request.method == "POST":
        deactivate_product(actor=request.user, product=product)
        messages.success(request, "Ürün pasife alındı.")
        return redirect("drawings:manage-product-detail", product_id=product.id)
    return render(
        request,
        "drawings/manage/confirm.html",
        {
            "title": "Ürünü Pasife Al",
            "message": "Ürün pasife alınacak. Teknik resimler ve geçmiş kayıtlar silinmeyecek.",
            "back_url": reverse("drawings:manage-product-detail", args=[product.id]),
        },
    )


@require_http_methods(["GET", "POST"])
def drawing_create(request, product_id):
    product = _product(request, product_id)
    form = DrawingForm(request.POST or None)
    if request.method == "POST" and form.is_valid():
        try:
            drawing = create_drawing(
                actor=request.user, product=product, **form.cleaned_data
            )
        except ValidationError as exc:
            _apply_validation(form, exc)
        else:
            messages.success(request, "Teknik resim oluşturuldu.")
            return redirect("drawings:manage-drawing-detail", drawing_id=drawing.id)
    return _form_page(
        request,
        form=form,
        title="Yeni Teknik Resim",
        back_url=reverse("drawings:manage-product-detail", args=[product.id]),
    )


def drawing_detail(request, drawing_id):
    drawing = _drawing(request, drawing_id)
    revisions = list_revisions_for_drawing(actor=request.user, drawing=drawing)
    return render(
        request,
        "drawings/manage/drawing_detail.html",
        {"drawing": drawing, "revisions": revisions},
    )


@require_http_methods(["GET", "POST"])
def drawing_edit(request, drawing_id):
    drawing = _drawing(request, drawing_id)
    form = DrawingEditForm(request.POST or None, initial={"title": drawing.title})
    if request.method == "POST" and form.is_valid():
        update_drawing(actor=request.user, drawing=drawing, **form.cleaned_data)
        messages.success(request, "Teknik resim güncellendi.")
        return redirect("drawings:manage-drawing-detail", drawing_id=drawing.id)
    return _form_page(
        request,
        form=form,
        title="Teknik Resmi Düzenle",
        back_url=reverse("drawings:manage-drawing-detail", args=[drawing.id]),
    )


@require_http_methods(["GET", "POST"])
def drawing_deactivate_view(request, drawing_id):
    drawing = _drawing(request, drawing_id)
    if request.method == "POST":
        deactivate_drawing(actor=request.user, drawing=drawing)
        messages.success(request, "Teknik resim pasife alındı.")
        return redirect("drawings:manage-drawing-detail", drawing_id=drawing.id)
    return render(
        request,
        "drawings/manage/confirm.html",
        {
            "title": "Teknik Resmi Pasife Al",
            "message": "Teknik resim pasife alınacak. Revizyonlar, dosyalar ve kontrol noktaları silinmeyecek.",
            "back_url": reverse("drawings:manage-drawing-detail", args=[drawing.id]),
        },
    )


@require_http_methods(["GET", "POST"])
def revision_create(request, drawing_id):
    drawing = _drawing(request, drawing_id)
    form = RevisionForm(request.POST or None, request.FILES or None)
    if request.method == "POST" and form.is_valid():
        upload = form.cleaned_data.pop("drawing_file")
        try:
            create_drawing_revision_with_file(
                actor=request.user,
                drawing=drawing,
                stream=upload,
                original_name=upload.name,
                mime_type=upload.content_type or "",
                encryption_scheme=encryption_scheme_for_upload(upload.name),
                **form.cleaned_data,
            )
        except (ValidationError, ValueError, OSError) as exc:
            if isinstance(exc, ValidationError):
                _apply_validation(form, exc)
            else:
                form.add_error(
                    "drawing_file", "Dosya kaydedilemedi veya desteklenmiyor."
                )
        else:
            messages.success(request, "Taslak revizyon oluşturuldu.")
            return redirect("drawings:manage-drawing-detail", drawing_id=drawing.id)
    return _form_page(
        request,
        form=form,
        title="Yeni Revizyon",
        back_url=reverse("drawings:manage-drawing-detail", args=[drawing.id]),
        warning="PDF tarayıcıda görüntülenebilir. PDF.ENC, DWG ve DXF saklanır ve indirilebilir; tarayıcıda görüntülenmez.",
    )


@require_http_methods(["GET", "POST"])
def revision_edit(request, revision_id):
    revision = _revision(request, revision_id)
    if revision.status != DrawingRevision.Status.DRAFT:
        raise PermissionDenied
    form = RevisionEditForm(
        request.POST or None,
        initial={
            "revision_code": revision.revision_code,
            "change_reason": revision.change_reason,
        },
    )
    if request.method == "POST" and form.is_valid():
        try:
            update_draft_revision(
                actor=request.user, revision=revision, **form.cleaned_data
            )
        except ValidationError as exc:
            _apply_validation(form, exc)
        else:
            messages.success(request, "Taslak revizyon güncellendi.")
            return redirect(
                "drawings:manage-drawing-detail", drawing_id=revision.drawing_id
            )
    return _form_page(
        request,
        form=form,
        title="Taslak Revizyonu Düzenle",
        back_url=reverse("drawings:manage-drawing-detail", args=[revision.drawing_id]),
    )


@require_http_methods(["GET", "POST"])
def revision_replace_file(request, revision_id):
    revision = _revision(request, revision_id)
    if revision.status != DrawingRevision.Status.DRAFT:
        raise PermissionDenied
    form = ReplaceFileForm(request.POST or None, request.FILES or None)
    if request.method == "POST" and form.is_valid():
        upload = form.cleaned_data["drawing_file"]
        try:
            replace_draft_revision_file(
                actor=request.user,
                revision=revision,
                stream=upload,
                original_name=upload.name,
                mime_type=upload.content_type or "",
                encryption_scheme=encryption_scheme_for_upload(upload.name),
            )
        except (ValidationError, ValueError, OSError):
            form.add_error("drawing_file", "Dosya değiştirilemedi veya desteklenmiyor.")
        else:
            messages.success(
                request,
                "Taslak dosyası değiştirildi; önceki dosya tarihsel olarak korundu.",
            )
            return redirect(
                "drawings:manage-drawing-detail", drawing_id=revision.drawing_id
            )
    return _form_page(
        request,
        form=form,
        title="Taslak Dosyasını Değiştir",
        back_url=reverse("drawings:manage-drawing-detail", args=[revision.drawing_id]),
        warning="Önceki FileObject tarihsel ve değişmez kayıt olarak korunacaktır.",
    )


def _transition(request, revision_id, *, service, title, message, success):
    revision = _revision(request, revision_id)
    if request.method == "POST":
        try:
            service(actor=request.user, revision=revision)
        except ValidationError as exc:
            messages.error(request, " ".join(exc.messages))
        else:
            messages.success(request, success)
        return redirect(
            "drawings:manage-drawing-detail", drawing_id=revision.drawing_id
        )
    return render(
        request,
        "drawings/manage/confirm.html",
        {
            "title": title,
            "message": message,
            "back_url": reverse(
                "drawings:manage-drawing-detail", args=[revision.drawing_id]
            ),
        },
    )


@require_http_methods(["GET", "POST"])
def revision_activate(request, revision_id):
    return _transition(
        request,
        revision_id,
        service=activate_revision,
        title="Revizyonu Aktif Et",
        message="Bu revizyon aktif hale getirilecek. Mevcut aktif revizyon varsa otomatik olarak SUPERSEDED olacaktır.",
        success="Revizyon aktif edildi.",
    )


@require_http_methods(["GET", "POST"])
def revision_withdraw(request, revision_id):
    return _transition(
        request,
        revision_id,
        service=withdraw_revision,
        title="Revizyonu Geri Çek",
        message="Revizyon geri çekilecek; dosya ve tarihçe silinmeyecek.",
        success="Revizyon geri çekildi.",
    )
