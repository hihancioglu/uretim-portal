from django import forms

from apps.core.decimal import parse_factory_decimal


class InspectionLaunchForm(forms.Form):
    drawing_revision = forms.UUIDField()
    lot_no = forms.CharField(max_length=120, required=False)
    serial_no = forms.CharField(max_length=120, required=False)
    declared_eye_count = forms.IntegerField(min_value=1, initial=1)


class MeasurementForm(forms.Form):
    measured_value = forms.CharField()
    note = forms.CharField(required=False)

    def clean_measured_value(self):
        return parse_factory_decimal(self.cleaned_data["measured_value"], label="Ölçüm değeri")


class VisualControlForm(forms.Form):
    control_name = forms.CharField(max_length=255)
    result = forms.ChoiceField(choices=(("OK", "OK"), ("NOK", "NOK")))
    note = forms.CharField(required=False)


class CorrectionForm(forms.Form):
    new_value = forms.CharField(label="Yeni Değer")
    reason = forms.CharField(max_length=500, widget=forms.Textarea, label="Düzeltme Nedeni")

    def clean_new_value(self):
        return parse_factory_decimal(self.cleaned_data["new_value"], label="Yeni değer")
