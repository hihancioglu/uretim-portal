import uuid
from django.conf import settings
from django.db import migrations, models
import django.db.models.deletion

class Migration(migrations.Migration):
    dependencies = [("accounts", "0002_alter_user_options_alter_user_groups_and_more")]
    operations = [
        migrations.CreateModel(name="ActionPermission", fields=[
            ("id", models.UUIDField(default=uuid.uuid4, editable=False, primary_key=True, serialize=False)), ("created_at", models.DateTimeField(auto_now_add=True)), ("updated_at", models.DateTimeField(auto_now=True)),
            ("code", models.CharField(max_length=120, unique=True)), ("name", models.CharField(max_length=160)), ("description", models.TextField(blank=True)), ("is_active", models.BooleanField(default=True))]),
        migrations.CreateModel(name="Role", fields=[
            ("id", models.UUIDField(default=uuid.uuid4, editable=False, primary_key=True, serialize=False)), ("created_at", models.DateTimeField(auto_now_add=True)), ("updated_at", models.DateTimeField(auto_now=True)),
            ("code", models.CharField(max_length=80, unique=True)), ("name", models.CharField(max_length=150)), ("description", models.TextField(blank=True)), ("is_active", models.BooleanField(default=True))]),
        migrations.CreateModel(name="ExternalIdentity", fields=[
            ("id", models.UUIDField(default=uuid.uuid4, editable=False, primary_key=True, serialize=False)), ("created_at", models.DateTimeField(auto_now_add=True)), ("updated_at", models.DateTimeField(auto_now=True)),
            ("issuer", models.CharField(max_length=500)), ("subject", models.CharField(max_length=255)), ("provider_code", models.CharField(default="AUTHENTIK", max_length=40)),
            ("preferred_username_snapshot", models.CharField(blank=True, max_length=150)), ("email_snapshot", models.EmailField(blank=True, max_length=254)), ("display_name_snapshot", models.CharField(blank=True, max_length=255)),
            ("first_seen_at", models.DateTimeField()), ("last_seen_at", models.DateTimeField()),
            ("user", models.ForeignKey(on_delete=django.db.models.deletion.PROTECT, related_name="external_identities", to=settings.AUTH_USER_MODEL))],
            options={"constraints":[models.UniqueConstraint(fields=("issuer","subject"), name="accounts_identity_issuer_subject_uniq")]}),
        migrations.CreateModel(name="RolePermission", fields=[
            ("id", models.UUIDField(default=uuid.uuid4, editable=False, primary_key=True, serialize=False)), ("created_at", models.DateTimeField(auto_now_add=True)), ("updated_at", models.DateTimeField(auto_now=True)),
            ("permission", models.ForeignKey(on_delete=django.db.models.deletion.PROTECT, related_name="role_links", to="accounts.actionpermission")),
            ("role", models.ForeignKey(on_delete=django.db.models.deletion.PROTECT, related_name="permission_links", to="accounts.role"))],
            options={"constraints":[models.UniqueConstraint(fields=("role","permission"), name="accounts_role_permission_uniq")]}),
        migrations.CreateModel(name="UserRole", fields=[
            ("id", models.UUIDField(default=uuid.uuid4, editable=False, primary_key=True, serialize=False)), ("created_at", models.DateTimeField(auto_now_add=True)), ("updated_at", models.DateTimeField(auto_now=True)),
            ("source", models.CharField(choices=[("MANUAL","Manual")], default="MANUAL", max_length=30)), ("is_active", models.BooleanField(default=True)), ("assigned_at", models.DateTimeField(auto_now_add=True)), ("assigned_by_snapshot", models.CharField(blank=True, max_length=255)),
            ("assigned_by", models.ForeignKey(blank=True, null=True, on_delete=django.db.models.deletion.PROTECT, related_name="role_assignments_made", to=settings.AUTH_USER_MODEL)),
            ("role", models.ForeignKey(on_delete=django.db.models.deletion.PROTECT, related_name="user_assignments", to="accounts.role")),
            ("user", models.ForeignKey(on_delete=django.db.models.deletion.PROTECT, related_name="role_assignments", to=settings.AUTH_USER_MODEL))],
            options={"constraints":[models.UniqueConstraint(fields=("user","role"), name="accounts_user_role_uniq")]}),
        migrations.CreateModel(name="ScopeGrant", fields=[
            ("id", models.UUIDField(default=uuid.uuid4, editable=False, primary_key=True, serialize=False)), ("created_at", models.DateTimeField(auto_now_add=True)), ("updated_at", models.DateTimeField(auto_now=True)),
            ("scope_type", models.CharField(choices=[("DEPARTMENT","Department"),("DRAWING","Drawing"),("OWNERSHIP","Ownership"),("ROW","Row"),("FIELD","Field")], max_length=20)), ("scope_key", models.CharField(max_length=120)), ("is_active", models.BooleanField(default=True)),
            ("user_role", models.ForeignKey(on_delete=django.db.models.deletion.PROTECT, related_name="scope_grants", to="accounts.userrole"))],
            options={"constraints":[models.UniqueConstraint(fields=("user_role","scope_type","scope_key"), name="accounts_scope_grant_uniq")]}),
    ]
