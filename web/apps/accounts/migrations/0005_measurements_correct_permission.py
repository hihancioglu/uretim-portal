from django.db import migrations


def seed(apps, schema_editor):
    Permission = apps.get_model("accounts", "ActionPermission")
    Role = apps.get_model("accounts", "Role")
    Link = apps.get_model("accounts", "RolePermission")
    permission, _ = Permission.objects.get_or_create(code="measurements.correct", defaults={"name": "Tamamlanmış ölçüm düzeltme"})
    Link.objects.get_or_create(role=Role.objects.get(code="admin"), permission=permission)


class Migration(migrations.Migration):
    dependencies = [("accounts", "0004_seed_authorization_baseline")]
    operations = [migrations.RunPython(seed, migrations.RunPython.noop)]
