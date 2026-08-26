from django.db import transaction
from .baseline import DRAWING_SCOPES, GRANTS, PERMISSIONS, ROLES
from .models import ActionPermission, Role, RolePermission, ScopeGrant, UserRole

@transaction.atomic
def seed_authorization_baseline():
    roles = {code: Role.objects.get_or_create(code=code, defaults={"name": name})[0] for code, name in ROLES.items()}
    permissions = {code: ActionPermission.objects.get_or_create(code=code, defaults={"name": name})[0] for code, name in PERMISSIONS.items()}
    for permission_code, role_codes in GRANTS.items():
        for role_code in role_codes:
            RolePermission.objects.get_or_create(role=roles[role_code], permission=permissions[permission_code])
    return roles

def seed_assignment_scopes(user_role):
    for key in DRAWING_SCOPES.get(user_role.role.code, set()):
        ScopeGrant.objects.get_or_create(user_role=user_role, scope_type=ScopeGrant.ScopeType.DRAWING, scope_key=key)
