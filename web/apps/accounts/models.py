import uuid

from django.conf import settings
from django.contrib.auth.models import AbstractUser
from django.db import models

class User(AbstractUser):
    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    email = models.EmailField(blank=True)
    created_at = models.DateTimeField(auto_now_add=True)
    updated_at = models.DateTimeField(auto_now=True)


class TimestampedUUIDModel(models.Model):
    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    created_at = models.DateTimeField(auto_now_add=True)
    updated_at = models.DateTimeField(auto_now=True)

    class Meta:
        abstract = True


class ExternalIdentity(TimestampedUUIDModel):
    user = models.ForeignKey(settings.AUTH_USER_MODEL, on_delete=models.PROTECT, related_name="external_identities")
    issuer = models.CharField(max_length=500)
    subject = models.CharField(max_length=255)
    provider_code = models.CharField(max_length=40, default="AUTHENTIK")
    preferred_username_snapshot = models.CharField(max_length=150, blank=True)
    email_snapshot = models.EmailField(blank=True)
    display_name_snapshot = models.CharField(max_length=255, blank=True)
    first_seen_at = models.DateTimeField()
    last_seen_at = models.DateTimeField()

    class Meta:
        constraints = [models.UniqueConstraint(fields=("issuer", "subject"), name="accounts_identity_issuer_subject_uniq")]


class Role(TimestampedUUIDModel):
    code = models.CharField(max_length=80, unique=True)
    name = models.CharField(max_length=150)
    description = models.TextField(blank=True)
    is_active = models.BooleanField(default=True)


class ActionPermission(TimestampedUUIDModel):
    code = models.CharField(max_length=120, unique=True)
    name = models.CharField(max_length=160)
    description = models.TextField(blank=True)
    is_active = models.BooleanField(default=True)


class RolePermission(TimestampedUUIDModel):
    role = models.ForeignKey(Role, on_delete=models.PROTECT, related_name="permission_links")
    permission = models.ForeignKey(ActionPermission, on_delete=models.PROTECT, related_name="role_links")

    class Meta:
        constraints = [models.UniqueConstraint(fields=("role", "permission"), name="accounts_role_permission_uniq")]


class UserRole(TimestampedUUIDModel):
    class Source(models.TextChoices):
        MANUAL = "MANUAL", "Manual"

    user = models.ForeignKey(settings.AUTH_USER_MODEL, on_delete=models.PROTECT, related_name="role_assignments")
    role = models.ForeignKey(Role, on_delete=models.PROTECT, related_name="user_assignments")
    source = models.CharField(max_length=30, choices=Source.choices, default=Source.MANUAL)
    is_active = models.BooleanField(default=True)
    assigned_at = models.DateTimeField(auto_now_add=True)
    assigned_by = models.ForeignKey(settings.AUTH_USER_MODEL, null=True, blank=True, on_delete=models.PROTECT, related_name="role_assignments_made")
    assigned_by_snapshot = models.CharField(max_length=255, blank=True)

    class Meta:
        constraints = [models.UniqueConstraint(fields=("user", "role"), name="accounts_user_role_uniq")]


class ScopeGrant(TimestampedUUIDModel):
    class ScopeType(models.TextChoices):
        DEPARTMENT = "DEPARTMENT", "Department"
        DRAWING = "DRAWING", "Drawing"
        OWNERSHIP = "OWNERSHIP", "Ownership"
        ROW = "ROW", "Row"
        FIELD = "FIELD", "Field"

    user_role = models.ForeignKey(UserRole, on_delete=models.PROTECT, related_name="scope_grants")
    scope_type = models.CharField(max_length=20, choices=ScopeType.choices)
    scope_key = models.CharField(max_length=120)
    is_active = models.BooleanField(default=True)

    class Meta:
        constraints = [models.UniqueConstraint(fields=("user_role", "scope_type", "scope_key"), name="accounts_scope_grant_uniq")]
