import re
from decimal import Decimal, InvalidOperation

from django.core.exceptions import ValidationError

_FACTORY_DECIMAL = re.compile(r"^[+-]?\d+(?:[.,]\d+)?$")


def parse_factory_decimal(value, *, label="Değer", decimal_places=5):
    text = str(value).strip()
    if not _FACTORY_DECIMAL.fullmatch(text) or ("," in text and "." in text):
        raise ValidationError(f"{label} geçersiz.")
    fraction = text.lstrip("+-").replace(",", ".").partition(".")[2]
    if len(fraction) > decimal_places:
        raise ValidationError(f"{label} en fazla {decimal_places} ondalık basamak içerebilir.")
    try:
        result = Decimal(text.replace(",", "."))
    except InvalidOperation as exc:
        raise ValidationError(f"{label} geçersiz.") from exc
    if not result.is_finite() or result.adjusted() > 8:
        raise ValidationError(f"{label} numeric(14,5) aralığında olmalıdır.")
    return result
