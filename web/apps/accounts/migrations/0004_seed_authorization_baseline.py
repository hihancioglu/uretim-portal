from django.db import migrations

def seed(apps, schema_editor):
    Role=apps.get_model("accounts","Role"); Permission=apps.get_model("accounts","ActionPermission"); Link=apps.get_model("accounts","RolePermission")
    from apps.accounts.baseline import GRANTS, PERMISSIONS, ROLES
    roles={code:Role.objects.get_or_create(code=code,defaults={"name":name})[0] for code,name in ROLES.items()}
    permissions={code:Permission.objects.get_or_create(code=code,defaults={"name":name})[0] for code,name in PERMISSIONS.items()}
    for pc,codes in GRANTS.items():
        for rc in codes: Link.objects.get_or_create(role=roles[rc],permission=permissions[pc])
class Migration(migrations.Migration):
    dependencies=[("accounts","0003_wp002_authorization_foundation")]
    operations=[migrations.RunPython(seed,migrations.RunPython.noop)]
