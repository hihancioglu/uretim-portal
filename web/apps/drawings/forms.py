from django import forms

from .models import Drawing


class ProductForm(forms.Form):
    tr_code = forms.CharField(label="TR Kodu", max_length=120)
    product_name = forms.CharField(label="Ürün Adı", max_length=255)
    plastic_code = forms.CharField(label="Plastik Kodu", max_length=120, required=False)
    material = forms.CharField(label="Malzeme", max_length=255, required=False)
    color_name = forms.CharField(label="Renk", max_length=160, required=False)


class DrawingForm(forms.Form):
    scope = forms.ChoiceField(label="Kapsam", choices=Drawing.Scope.choices)
    title = forms.CharField(label="Başlık", max_length=255, required=False)


class DrawingEditForm(forms.Form):
    title = forms.CharField(label="Başlık", max_length=255, required=False)


class RevisionForm(forms.Form):
    revision_code = forms.CharField(label="Revizyon", max_length=120)
    drawing_file = forms.FileField(label="Teknik resim dosyası")
    change_reason = forms.CharField(
        label="Değişiklik nedeni", required=False, widget=forms.Textarea
    )


class RevisionEditForm(forms.Form):
    revision_code = forms.CharField(label="Revizyon", max_length=120)
    change_reason = forms.CharField(
        label="Değişiklik nedeni", required=False, widget=forms.Textarea
    )


class ReplaceFileForm(forms.Form):
    drawing_file = forms.FileField(label="Yeni dosya")
