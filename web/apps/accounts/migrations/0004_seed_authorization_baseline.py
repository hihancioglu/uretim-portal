from django.db import migrations

# Historical V1 snapshot. Runtime baseline changes require a new data migration.
ROLES = {
    "admin": "Admin", "technical_drawing": "Teknik Resim", "quality_manager": "Kalite Kontrol Yöneticisi",
    "plastic_quality": "Plastikhane Kalite Kontrol", "mechanism_quality": "Mekanizma Kalite Kontrol",
    "mechanism_manager": "Mekanizma Yöneticisi", "incoming_quality": "Giriş Kalite Kontrol",
    "laboratory": "Kalite Laboratuvar", "production_user": "Üretim Kullanıcısı",
    "production_manager": "Üretim Yöneticisi", "production_label": "Üretim Etiket",
    "planning": "Planlama", "manager": "Yönetici",
}
PERMISSIONS = dict((
    ("drawings.view", "Teknik resim görüntüleme"), ("drawings.manage", "Teknik resim/ürün yönetimi"),
    ("measurements.create", "Ölçüm girişi"), ("measurements.view_history", "Ölçüm geçmişi"),
    ("spc.view", "SPC görüntüleme"), ("spc.adjust_limits", "SPC limit düzeltme"),
    ("msa.view", "MSA görüntüleme"), ("msa.manage", "MSA yönetimi"),
    ("molding.bind", "Kalıp bağlama"), ("molding.plan_manage", "Kalıp plan yönetimi"),
    ("mold_tickets.delete", "Kalıp ticket silme"), ("commissioning.manage", "Devreye alma yönetimi"),
    ("commissioning.delete", "Devreye alma silme"), ("lab_requests.create", "Test talebi oluşturma"),
    ("lab_requests.process", "Test talebi işleme"), ("lab_requests.skip_or_reopen_step", "Test adımı override"),
    ("package_meter.manage", "Paket sayaç yönetimi"), ("authorization_matrix.view", "Yetki matrisi görüntüleme"),
))
GRANTS = {
    "drawings.view": {"admin", "technical_drawing", "quality_manager", "manager", "incoming_quality", "plastic_quality", "production_label"},
    "drawings.manage": {"admin", "technical_drawing"},
    "measurements.create": {"admin", "quality_manager", "incoming_quality", "plastic_quality"},
    "measurements.view_history": {"admin", "quality_manager", "incoming_quality", "plastic_quality", "manager"},
    "spc.view": {"admin", "quality_manager", "manager"}, "spc.adjust_limits": {"admin"},
    "msa.view": {"admin", "quality_manager", "manager"}, "msa.manage": {"admin"},
    "molding.bind": {"admin", "production_user", "production_manager"},
    "molding.plan_manage": {"admin", "production_manager"}, "mold_tickets.delete": {"admin"},
    "commissioning.manage": {"admin", "production_manager", "quality_manager", "technical_drawing"},
    "commissioning.delete": {"admin"},
    "lab_requests.create": {"admin", "quality_manager", "mechanism_quality", "incoming_quality", "plastic_quality"},
    "lab_requests.process": {"admin", "quality_manager", "laboratory"},
    "lab_requests.skip_or_reopen_step": {"admin", "quality_manager"},
    "package_meter.manage": {"admin", "quality_manager", "laboratory"},
    "authorization_matrix.view": {"admin", "manager"},
}

def seed(apps, schema_editor):
    Role=apps.get_model("accounts","Role"); Permission=apps.get_model("accounts","ActionPermission"); Link=apps.get_model("accounts","RolePermission")
    roles={code:Role.objects.get_or_create(code=code,defaults={"name":name})[0] for code,name in ROLES.items()}
    permissions={code:Permission.objects.get_or_create(code=code,defaults={"name":name})[0] for code,name in PERMISSIONS.items()}
    for pc,codes in GRANTS.items():
        for rc in codes: Link.objects.get_or_create(role=roles[rc],permission=permissions[pc])
class Migration(migrations.Migration):
    dependencies=[("accounts","0003_wp002_authorization_foundation")]
    operations=[migrations.RunPython(seed,migrations.RunPython.noop)]
