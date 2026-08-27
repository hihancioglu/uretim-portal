ROLES = {
    "admin": "Admin", "technical_drawing": "Teknik Resim", "quality_manager": "Kalite Kontrol Yöneticisi",
    "plastic_quality": "Plastikhane Kalite Kontrol", "mechanism_quality": "Mekanizma Kalite Kontrol",
    "mechanism_manager": "Mekanizma Yöneticisi", "incoming_quality": "Giriş Kalite Kontrol",
    "laboratory": "Kalite Laboratuvar", "production_user": "Üretim Kullanıcısı",
    "production_manager": "Üretim Yöneticisi", "production_label": "Üretim Etiket",
    "planning": "Planlama", "manager": "Yönetici",
}
PERMISSIONS = {
    code: name for code, name in (
        ("drawings.view", "Teknik resim görüntüleme"), ("drawings.manage", "Teknik resim/ürün yönetimi"),
        ("measurements.create", "Ölçüm girişi"), ("measurements.view_history", "Ölçüm geçmişi"),
        ("measurements.correct", "Tamamlanmış ölçüm düzeltme"),
        ("spc.view", "SPC görüntüleme"), ("spc.adjust_limits", "SPC limit düzeltme"),
        ("msa.view", "MSA görüntüleme"), ("msa.manage", "MSA yönetimi"),
        ("molding.bind", "Kalıp bağlama"), ("molding.plan_manage", "Kalıp plan yönetimi"),
        ("mold_tickets.delete", "Kalıp ticket silme"), ("commissioning.manage", "Devreye alma yönetimi"),
        ("commissioning.delete", "Devreye alma silme"), ("lab_requests.create", "Test talebi oluşturma"),
        ("lab_requests.process", "Test talebi işleme"), ("lab_requests.skip_or_reopen_step", "Test adımı override"),
        ("package_meter.manage", "Paket sayaç yönetimi"), ("authorization_matrix.view", "Yetki matrisi görüntüleme"),
    )
}
GRANTS = {
    "drawings.view": {"admin", "technical_drawing", "quality_manager", "manager", "incoming_quality", "plastic_quality", "production_label"},
    "drawings.manage": {"admin", "technical_drawing"},
    "measurements.create": {"admin", "quality_manager", "incoming_quality", "plastic_quality"},
    "measurements.view_history": {"admin", "quality_manager", "incoming_quality", "plastic_quality", "manager"},
    "measurements.correct": {"admin"},
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
DRAWING_SCOPES = {
    "admin": {"PLASTIC", "INCOMING_QUALITY", "TR"}, "technical_drawing": {"PLASTIC", "INCOMING_QUALITY", "TR"},
    "quality_manager": {"PLASTIC", "INCOMING_QUALITY", "TR"}, "manager": {"PLASTIC", "INCOMING_QUALITY", "TR"},
    "incoming_quality": {"INCOMING_QUALITY"}, "plastic_quality": {"PLASTIC"}, "production_label": {"PLASTIC"},
}
ROLE_ALIASES = {"Kalite Kontrol Kullanıcısı": "plastic_quality"}
