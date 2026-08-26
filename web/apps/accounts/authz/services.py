from django.core.exceptions import PermissionDenied
from apps.accounts.models import ScopeGrant, UserRole


def _assignments(user):
    if not getattr(user, "is_authenticated", False) or not user.is_active:
        return UserRole.objects.none()
    return UserRole.objects.filter(user=user, is_active=True, role__is_active=True)


def has_action(user, permission_code):
    return _assignments(user).filter(role__permission_links__permission__code=permission_code, role__permission_links__permission__is_active=True).exists()


def has_scoped_action(user, permission_code, *, scope_type, scope_key):
    return _assignments(user).filter(
        role__permission_links__permission__code=permission_code,
        role__permission_links__permission__is_active=True,
        scope_grants__scope_type=scope_type,
        scope_grants__scope_key=scope_key,
        scope_grants__is_active=True,
    ).exists()


def require_action(user, permission_code):
    if not has_action(user, permission_code):
        raise PermissionDenied


def require_scoped_action(user, permission_code, *, scope_type, scope_key):
    if not has_scoped_action(user, permission_code, scope_type=scope_type, scope_key=scope_key):
        raise PermissionDenied


def can_process_lab_request(user, *, requested_department):
    if has_action(user, "lab_requests.process"):
        return True
    return requested_department == "MEKANİZMA" and _assignments(user).filter(role__code="mechanism_quality").exists()
