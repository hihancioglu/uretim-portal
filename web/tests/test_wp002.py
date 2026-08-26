import pytest
from django.contrib.auth import get_user_model
from django.db import IntegrityError, transaction
from apps.accounts.authz import can_process_lab_request, has_action, has_scoped_action
from apps.accounts.baseline import ROLE_ALIASES
from apps.accounts.identity import IdentityDenied, resolve_oidc_identity
from apps.accounts.models import ExternalIdentity, Role, ScopeGrant, UserRole
from apps.accounts.seeding import seed_assignment_scopes, seed_authorization_baseline

pytestmark = pytest.mark.django_db

def user(name="user", active=True): return get_user_model().objects.create_user(username=name, is_active=active)
def assign(account, code):
    assignment=UserRole.objects.create(user=account, role=Role.objects.get(code=code)); seed_assignment_scopes(assignment); return assignment

def test_external_identity_unique():
    account=user(); claims={"preferred_username":"external"}
    resolve_oidc_identity(issuer="https://issuer",subject="s",claims=claims,auto_provision=True)
    with pytest.raises(IntegrityError), transaction.atomic():
        ExternalIdentity.objects.create(user=account,issuer="https://issuer",subject="s",first_seen_at="2026-01-01T00:00Z",last_seen_at="2026-01-01T00:00Z")

def test_known_identity_is_stable_when_email_changes():
    account=resolve_oidc_identity(issuer="https://issuer",subject="s",claims={"preferred_username":"external","email":"one@example.com"},auto_provision=True)
    same=resolve_oidc_identity(issuer="https://issuer",subject="s",claims={"email":"two@example.com"})
    assert same == account and same.email == "one@example.com"
    assert same.external_identities.get().email_snapshot == "two@example.com"

def test_inactive_and_unknown_are_denied():
    with pytest.raises(IdentityDenied): resolve_oidc_identity(issuer="https://issuer",subject="unknown",claims={},auto_provision=False)
    account=user(active=False); ExternalIdentity.objects.create(user=account,issuer="https://issuer",subject="inactive",first_seen_at="2026-01-01T00:00Z",last_seen_at="2026-01-01T00:00Z")
    with pytest.raises(IdentityDenied): resolve_oidc_identity(issuer="https://issuer",subject="inactive",claims={})

def test_provision_has_unusable_password_no_roles_and_collision_denied():
    account=resolve_oidc_identity(issuer="https://issuer",subject="new",claims={"preferred_username":"external","email":"new@example.com"},auto_provision=True)
    assert not account.has_usable_password() and not account.role_assignments.exists()
    user("collision").email="collision@example.com"; get_user_model().objects.filter(username="collision").update(email="collision@example.com")
    with pytest.raises(IdentityDenied): resolve_oidc_identity(issuer="https://issuer",subject="other",claims={"preferred_username":"other","email":"collision@example.com"},auto_provision=True)

@pytest.mark.parametrize(("role_code","allowed"), [("admin",True),("technical_drawing",True),("manager",False),("planning",False)])
def test_drawing_manage_matrix(role_code, allowed):
    seed_authorization_baseline(); account=user(role_code); assign(account,role_code)
    assert has_action(account,"drawings.manage") is allowed

@pytest.mark.parametrize(("role_code","scope","allowed"), [("incoming_quality","INCOMING_QUALITY",True),("incoming_quality","PLASTIC",False),("plastic_quality","PLASTIC",True),("plastic_quality","TR",False),("production_label","PLASTIC",True),("planning","PLASTIC",False)])
def test_drawing_scopes(role_code,scope,allowed):
    seed_authorization_baseline(); account=user(role_code); assign(account,role_code)
    assert has_scoped_action(account,"drawings.view",scope_type=ScopeGrant.ScopeType.DRAWING,scope_key=scope) is allowed

def test_inactive_user_role_and_permission_denied():
    seed_authorization_baseline(); account=user(); assignment=assign(account,"admin")
    assert has_action(account,"spc.adjust_limits")
    assignment.is_active=False; assignment.save(); assert not has_action(account,"spc.adjust_limits")
    assignment.is_active=True; assignment.save(); account.is_active=False; account.save(); assert not has_action(account,"spc.adjust_limits")

def test_restricted_admin_permissions_and_molding():
    seed_authorization_baseline()
    for code in ("manager","production_user","admin"):
        account=user(code); assign(account,code)
        assert has_action(account,"spc.adjust_limits") is (code == "admin")
        assert has_action(account,"msa.manage") is (code == "admin")
        assert has_action(account,"molding.bind") is (code in {"production_user","admin"})

def test_mechanism_context_rule_and_alias():
    seed_authorization_baseline(); account=user(); assign(account,"mechanism_quality")
    assert can_process_lab_request(account,requested_department="MEKANİZMA")
    assert not can_process_lab_request(account,requested_department="PLASTİKHANE")
    assert ROLE_ALIASES["Kalite Kontrol Kullanıcısı"] == "plastic_quality"

def test_seed_idempotent():
    seed_authorization_baseline(); first=(Role.objects.count(),)
    seed_authorization_baseline(); assert (Role.objects.count(),) == first
