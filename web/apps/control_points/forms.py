from django import forms
from apps.core.decimal import parse_factory_decimal


class ControlPointForm(forms.Form):
    measure_code = forms.CharField(max_length=120)
    measure_name = forms.CharField(max_length=255)
    nominal = forms.CharField()
    lower_tolerance = forms.CharField()
    upper_tolerance = forms.CharField()
    unit = forms.CharField(max_length=32, required=False, initial="mm")
    page_no = forms.IntegerField(min_value=1)
    x_ratio = forms.CharField()
    y_ratio = forms.CharField()
    is_mandatory = forms.BooleanField(required=False, initial=True)
    measurement_group = forms.CharField(max_length=120, required=False, initial="Genel")
    sample_frequency = forms.CharField(
        max_length=120, required=False, initial="Her Kontrol"
    )
    is_critical = forms.BooleanField(required=False)
    sort_no = forms.IntegerField(required=False, initial=0)
    change_reason = forms.CharField(required=False)

    def clean(self):
        data = super().clean()
        for field, label in (
            ("nominal", "Nominal"),
            ("lower_tolerance", "Alt tolerans"),
            ("upper_tolerance", "Üst tolerans"),
        ):
            if field in data:
                data[field] = parse_factory_decimal(data[field], label=label)
        for field in ("x_ratio", "y_ratio"):
            if field in data:
                value = parse_factory_decimal(
                    data[field],
                    label="Kontrol noktası koordinatı",
                    decimal_places=6,
                )
                if value < 0 or value > 1 or value.as_tuple().exponent < -6:
                    self.add_error(field, "Kontrol noktası koordinatı geçersiz.")
                data[field] = value
        return data


class CopyControlPointsForm(forms.Form):
    source_revision_id = forms.UUIDField(
        required=True,
        error_messages={
            "required": "Kaynak revizyon seçilmelidir.",
            "invalid": "Kaynak revizyon kimliği geçersiz.",
        },
    )
