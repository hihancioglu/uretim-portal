Public NotInheritable Class AppState
    Private Sub New()
    End Sub

    Public Const RoleProduction As String = "Üretim Kullanıcısı"
    Public Const RoleProductionLabel As String = "Üretim Etiket"
    Public Const RoleProductionManager As String = "Üretim Yöneticisi"
    Public Const LegacyRoleQuality As String = "Kalite Kontrol Kullanıcısı"
    Public Const RoleQuality As String = "Plastikhane Kalite Kontrol"
    Public Const RoleQualityManager As String = "Kalite Kontrol Yöneticisi"
    Public Const RoleIncomingQuality As String = "Giriş Kalite Kontrol"
    Public Const RoleMechanismQuality As String = "Mekanizma Kalite Kontrol"
    Public Const RoleMechanismManager As String = "Mekanizma Yöneticisi"
    Public Const RolePlasticQuality As String = "Plastikhane Kalite Kontrol"
    Public Const RoleLaboratory As String = "Kalite Laboratuvar"
    Public Const RoleTechnicalDrawing As String = "Teknik Resim"
    Public Const RolePlanning As String = "Planlama"
    Public Const RoleManager As String = "Yönetici"
    Public Const RoleAdmin As String = "Admin"

    Public Shared Property CurrentUserName As String = ""
    Public Shared Property CurrentRole As String = ""
    Public Shared Property CurrentSessionId As String = ""
    Public Shared Property CurrentUserMustChangePassword As Boolean = False
    Public Shared Property CurrentUserIsPermissionTestAccount As Boolean = False

    Public Shared Function NormalizeRole(roleText As String) As String
        Dim raw = If(roleText, "").Trim()
        If raw = "" Then Return RoleProduction

        If String.Equals(raw, RoleProduction, StringComparison.OrdinalIgnoreCase) Then Return RoleProduction
        If String.Equals(raw, RoleProductionLabel, StringComparison.OrdinalIgnoreCase) Then Return RoleProductionLabel
        If String.Equals(raw, RoleProductionManager, StringComparison.OrdinalIgnoreCase) Then Return RoleProductionManager
        If String.Equals(raw, LegacyRoleQuality, StringComparison.OrdinalIgnoreCase) Then Return RolePlasticQuality
        If String.Equals(raw, RoleQuality, StringComparison.OrdinalIgnoreCase) Then Return RolePlasticQuality
        If String.Equals(raw, RoleQualityManager, StringComparison.OrdinalIgnoreCase) Then Return RoleQualityManager
        If String.Equals(raw, RoleIncomingQuality, StringComparison.OrdinalIgnoreCase) Then Return RoleIncomingQuality
        If String.Equals(raw, RoleMechanismQuality, StringComparison.OrdinalIgnoreCase) Then Return RoleMechanismQuality
        If String.Equals(raw, RoleMechanismManager, StringComparison.OrdinalIgnoreCase) Then Return RoleMechanismManager
        If String.Equals(raw, RolePlasticQuality, StringComparison.OrdinalIgnoreCase) Then Return RolePlasticQuality
        If String.Equals(raw, RoleLaboratory, StringComparison.OrdinalIgnoreCase) Then Return RoleLaboratory
        If String.Equals(raw, RoleTechnicalDrawing, StringComparison.OrdinalIgnoreCase) Then Return RoleTechnicalDrawing
        If String.Equals(raw, RolePlanning, StringComparison.OrdinalIgnoreCase) Then Return RolePlanning
        If String.Equals(raw, RoleManager, StringComparison.OrdinalIgnoreCase) Then Return RoleManager
        If String.Equals(raw, RoleAdmin, StringComparison.OrdinalIgnoreCase) Then Return RoleAdmin

        Dim t = raw.ToUpperInvariant().
            Replace("İ", "I").
            Replace("ı", "I").
            Replace("Ş", "S").
            Replace("Ğ", "G").
            Replace("Ü", "U").
            Replace("Ö", "O").
            Replace("Ç", "C")

        If t = "ADMIN" OrElse t = "ADMINISTRATOR" OrElse t = "ADMİN" Then Return RoleAdmin

        If t = "MANAGER" OrElse t = "YONETICI" OrElse t = "YONETICI" Then Return RoleManager

        If t = "USER" OrElse t = "URETIM" OrElse t = "URETIM KULLANICISI" OrElse t = "PRODUCTION" OrElse t = "PRODUCTION USER" Then
            Return RoleProduction
        End If

        If t = "URETIM ETIKET" OrElse t = "URETIM ETIKETI" OrElse t = "PRODUCTION LABEL" OrElse t = "LABEL" Then
            Return RoleProductionLabel
        End If

        If t = "URETIM YONETICISI" OrElse t = "PRODUCTION MANAGER" OrElse t = "URETIM MANAGER" Then
            Return RoleProductionManager
        End If

        If t = "QUALITY" OrElse t = "KALITE" OrElse t = "KALITE KONTROL" OrElse t = "KALITE KONTROL KULLANICISI" OrElse t = "QUALITY USER" Then
            Return RolePlasticQuality
        End If

        If t = "KALITE KONTROL YONETICISI" OrElse t = "QUALITY MANAGER" OrElse t = "KALITE MANAGER" Then
            Return RoleQualityManager
        End If

        If t = "GIRIS KALITE KONTROL" OrElse
           t = "GIRIS KALITE" OrElse
           t = "GKK" OrElse
           t = "INCOMING QUALITY" OrElse
           t = "INCOMING QUALITY CONTROL" Then
            Return RoleIncomingQuality
        End If

        If t = "MEKANIZMA KALITE KONTROL KULLANICISI" OrElse
           t = "MEKANIZMA KALITE KONTROL" OrElse
           t = "MEKANIZMA MONTAJ KALITE KONTROL" OrElse
           t = "MEKANIZMA MONTAJ KALITE KONTROL KULLANICISI" OrElse
           t = "MEKANIZMA KALITE" OrElse
           t = "MECHANISM QUALITY" OrElse
           t = "MECHANISM QUALITY USER" Then
            Return RoleMechanismQuality
        End If

        If t = "MEKANIZMA YONETICISI" OrElse
           t = "MEKANIZMA YONETICI" OrElse
           t = "MEKANIZMA MANAGER" OrElse
           t = "MECHANISM MANAGER" Then
            Return RoleMechanismManager
        End If

        If t = "PLASTIKHANE KALITE KONTROL" OrElse
           t = "PLASTIKHANE KALITE" OrElse
           t = "PLASTIC QUALITY" OrElse
           t = "PLASTICS QUALITY" Then
            Return RolePlasticQuality
        End If

        If t = "KALITE LABORATUVAR" OrElse
           t = "KALITE LABORATURVAR" OrElse
           t = "LABORATURVAR" OrElse
           t = "LABORATUVAR" OrElse
           t = "LABORATORY" Then
            Return RoleLaboratory
        End If

        If t = "TEKNIK RESIM" OrElse
           t = "TEKNIK RESIM KULLANICISI" OrElse
           t = "TEKNIK CIZIM" OrElse
           t = "TECHNICAL DRAWING" OrElse
           t = "DRAWING" Then
            Return RoleTechnicalDrawing
        End If

        If t = "PLANLAMA" OrElse
           t = "PLANLAMA KULLANICISI" OrElse
           t = "PLANNING" OrElse
           t = "PLANNING USER" Then
            Return RolePlanning
        End If

        Return raw
    End Function

    Public Shared Function IsValidRole(roleText As String) As Boolean
        Dim r = NormalizeRole(roleText)
        Return String.Equals(r, RoleProduction, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(r, RoleProductionLabel, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(r, RoleProductionManager, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(r, RoleQuality, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(r, RoleQualityManager, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(r, RoleIncomingQuality, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(r, RoleMechanismQuality, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(r, RoleMechanismManager, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(r, RolePlasticQuality, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(r, RoleLaboratory, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(r, RoleTechnicalDrawing, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(r, RolePlanning, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(r, RoleManager, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(r, RoleAdmin, StringComparison.OrdinalIgnoreCase)
    End Function

    Public Shared ReadOnly Property IsProductionUser As Boolean
        Get
            Return String.Equals(NormalizeRole(CurrentRole), RoleProduction, StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

    Public Shared ReadOnly Property IsProductionLabel As Boolean
        Get
            Return String.Equals(NormalizeRole(CurrentRole), RoleProductionLabel, StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

    Public Shared ReadOnly Property IsProductionManager As Boolean
        Get
            Return String.Equals(NormalizeRole(CurrentRole), RoleProductionManager, StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

    Public Shared ReadOnly Property IsQualityControlUser As Boolean
        Get
            Dim role = NormalizeRole(CurrentRole)
            Return String.Equals(role, RoleQuality, StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(role, RolePlasticQuality, StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

    Public Shared ReadOnly Property IsQualityControlManager As Boolean
        Get
            Return String.Equals(NormalizeRole(CurrentRole), RoleQualityManager, StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

    Public Shared ReadOnly Property IsIncomingQualityControlUser As Boolean
        Get
            Return String.Equals(NormalizeRole(CurrentRole), RoleIncomingQuality, StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

    Public Shared ReadOnly Property IsMechanismQualityControlUser As Boolean
        Get
            Return String.Equals(NormalizeRole(CurrentRole), RoleMechanismQuality, StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

    Public Shared ReadOnly Property IsMechanismManager As Boolean
        Get
            Return String.Equals(NormalizeRole(CurrentRole), RoleMechanismManager, StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

    Public Shared ReadOnly Property IsPlasticQualityControlUser As Boolean
        Get
            Dim role = NormalizeRole(CurrentRole)
            Return String.Equals(role, RolePlasticQuality, StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(role, RoleQuality, StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

    Public Shared ReadOnly Property IsLaboratoryUser As Boolean
        Get
            Return String.Equals(NormalizeRole(CurrentRole), RoleLaboratory, StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

    Public Shared ReadOnly Property IsTechnicalDrawingUser As Boolean
        Get
            Return String.Equals(NormalizeRole(CurrentRole), RoleTechnicalDrawing, StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

    Public Shared ReadOnly Property IsPlanningUser As Boolean
        Get
            Return String.Equals(NormalizeRole(CurrentRole), RolePlanning, StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

    Public Shared ReadOnly Property IsManager As Boolean
        Get
            Return String.Equals(NormalizeRole(CurrentRole), RoleManager, StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

    Public Shared ReadOnly Property IsAdmin As Boolean
        Get
            Return String.Equals(NormalizeRole(CurrentRole), RoleAdmin, StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenMeasurement As Boolean
        Get
            Return IsQualityControlUser OrElse IsIncomingQualityControlUser OrElse IsQualityControlManager OrElse IsAdmin
        End Get
    End Property

    Public Shared Function CanAccessDrawingScope(scope As String) As Boolean
        Dim normalizedScope = ProductInfo.NormalizeDrawingScope(scope)

        If IsAdmin OrElse IsTechnicalDrawingUser OrElse IsQualityControlManager OrElse IsManager Then
            Return True
        End If

        If IsIncomingQualityControlUser Then
            Return String.Equals(normalizedScope, ProductInfo.DrawingScopeIncomingQuality, StringComparison.OrdinalIgnoreCase)
        End If

        If IsQualityControlUser OrElse IsPlasticQualityControlUser Then
            Return String.Equals(normalizedScope, ProductInfo.DrawingScopePlastic, StringComparison.OrdinalIgnoreCase)
        End If

        If IsProductionLabel Then
            Return String.Equals(normalizedScope, ProductInfo.DrawingScopePlastic, StringComparison.OrdinalIgnoreCase)
        End If

        Return False
    End Function

    Public Shared ReadOnly Property CanViewMeasurementHistory As Boolean
        Get
            Return CanOpenMeasurement OrElse IsManager
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenSpcDashboard As Boolean
        Get
            Return IsAdmin OrElse IsQualityControlManager OrElse IsManager
        End Get
    End Property

    Public Shared ReadOnly Property CanEditSpcDashboard As Boolean
        Get
            Return IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenScrapDashboard As Boolean
        Get
            Return IsAdmin OrElse IsManager OrElse IsQualityControlManager
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenReworkDashboard As Boolean
        Get
            Return IsAdmin OrElse IsManager OrElse IsQualityControlManager OrElse IsProductionManager
        End Get
    End Property

    Public Shared ReadOnly Property CanEditReworkDashboard As Boolean
        Get
            Return IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenMsaDashboard As Boolean
        Get
            Return IsAdmin OrElse IsQualityControlManager OrElse IsManager
        End Get
    End Property

    Public Shared ReadOnly Property CanModifyMsaDashboard As Boolean
        Get
            Return IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenProductionBinding As Boolean
        Get
            Return IsProductionUser OrElse IsProductionManager OrElse IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenMoldConnectionPlan As Boolean
        Get
            Return IsProductionLabel OrElse
                   IsQualityControlUser OrElse IsQualityControlManager OrElse
                   IsMechanismQualityControlUser OrElse IsMechanismManager OrElse
                   IsManager OrElse CanOpenProductionBinding OrElse CanOpenMoldBindingDashboard
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenProductionDrawingSearch As Boolean
        Get
            Return IsProductionLabel OrElse
                   IsProductionManager OrElse
                   IsPlasticQualityControlUser OrElse
                   IsQualityControlManager OrElse
                   IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanModifyMoldConnectionPlan As Boolean
        Get
            Return IsAdmin OrElse IsProductionManager
        End Get
    End Property

    Public Shared ReadOnly Property CanManageMoldConnectionPlanEmailRecipients As Boolean
        Get
            Return IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenMoldBindingDashboard As Boolean
        Get
            Return IsProductionUser OrElse IsProductionManager OrElse IsManager OrElse IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanViewAllMoldBindingDashboard As Boolean
        Get
            Return IsProductionManager OrElse IsManager OrElse IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenQualityTickets As Boolean
        Get
            Return IsProductionManager OrElse IsQualityControlUser OrElse IsQualityControlManager OrElse IsManager OrElse IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanModifyQualityTickets As Boolean
        Get
            Return IsQualityControlUser OrElse IsQualityControlManager OrElse IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenMoldTickets As Boolean
        Get
            Return IsProductionUser OrElse IsProductionManager OrElse
                   IsQualityControlUser OrElse IsQualityControlManager OrElse
                   IsManager OrElse IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenQualityToProductionTickets As Boolean
        Get
            Return IsProductionUser OrElse IsProductionManager OrElse
                   IsQualityControlManager OrElse IsManager OrElse IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanModifyQualityToProductionTickets As Boolean
        Get
            Return Not IsProductionUser AndAlso Not IsManager AndAlso CanOpenQualityToProductionTickets
        End Get
    End Property

    Public Shared ReadOnly Property CanModifyMoldTickets As Boolean
        Get
            Return Not IsProductionUser AndAlso Not IsManager AndAlso CanOpenMoldTickets
        End Get
    End Property

    Public Shared ReadOnly Property CanDeleteMoldTickets As Boolean
        Get
            Return IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenNewMoldCommissioning As Boolean
        Get
            Return IsAdmin OrElse IsManager OrElse IsProductionManager OrElse
                   IsQualityControlManager OrElse IsTechnicalDrawingUser OrElse IsPlanningUser
        End Get
    End Property

    Public Shared ReadOnly Property CanModifyNewMoldCommissioning As Boolean
        Get
            Return IsAdmin OrElse IsProductionManager OrElse
                   IsQualityControlManager OrElse IsTechnicalDrawingUser
        End Get
    End Property

    Public Shared ReadOnly Property CanDeleteNewMoldCommissioning As Boolean
        Get
            Return IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenOperationalAdmin As Boolean
        Get
            Return IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenTechnicalDrawingAdmin As Boolean
        Get
            Return IsTechnicalDrawingUser OrElse IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanViewTechnicalDrawingAdmin As Boolean
        Get
            Return CanOpenTechnicalDrawingAdmin OrElse IsManager
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenMechanismQualityControl As Boolean
        Get
            Return IsQualityControlUser OrElse IsQualityControlManager OrElse
                   IsMechanismQualityControlUser OrElse IsMechanismManager OrElse
                   IsManager OrElse IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenInoTracking As Boolean
        Get
            Return IsMechanismQualityControlUser OrElse
                   IsMechanismManager OrElse
                   IsLaboratoryUser OrElse
                   IsQualityControlManager OrElse
                   IsPlanningUser OrElse IsManager OrElse
                   IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanCreateMechanismQualityDelivery As Boolean
        Get
            Return IsQualityControlUser OrElse IsQualityControlManager OrElse IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanReviewMechanismQualityDelivery As Boolean
        Get
            Return IsQualityControlUser OrElse IsQualityControlManager OrElse
                   IsMechanismQualityControlUser OrElse IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanEditMechanismQualityDetails As Boolean
        Get
            Return IsQualityControlManager OrElse IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenUserAdmin As Boolean
        Get
            Return IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenPlasticShiftTracking As Boolean
        Get
            Return IsPlasticQualityControlUser OrElse
                   IsQualityControlUser OrElse
                   IsQualityControlManager OrElse
                   IsProductionUser OrElse IsProductionManager OrElse
                   IsMechanismQualityControlUser OrElse IsMechanismManager OrElse
                   IsManager OrElse IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanModifyPlasticShiftTracking As Boolean
        Get
            Return IsPlasticQualityControlUser OrElse IsQualityControlUser OrElse IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanCreateMoldTicketFromPlasticShift As Boolean
        Get
            Return CanModifyPlasticShiftTracking
        End Get
    End Property

    Public Shared ReadOnly Property CanDeletePlasticShiftTracking As Boolean
        Get
            Return IsQualityControlManager OrElse IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenMechanismShiftTracking As Boolean
        Get
            Return IsMechanismQualityControlUser OrElse IsMechanismManager OrElse
                   IsProductionManager OrElse IsQualityControlManager OrElse
                   IsManager OrElse IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanModifyMechanismShiftTracking As Boolean
        Get
            Return IsMechanismQualityControlUser OrElse IsMechanismManager OrElse IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanDeleteMechanismShiftTracking As Boolean
        Get
            Return IsMechanismManager OrElse IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenPlasticShiftErrorReport As Boolean
        Get
            Return CanOpenPlasticShiftTracking
        End Get
    End Property

    Public Shared ReadOnly Property CanCreatePlasticShiftErrorReport As Boolean
        Get
            Return CanModifyPlasticShiftTracking OrElse IsQualityControlManager
        End Get
    End Property

    Public Shared ReadOnly Property CanManagePlasticShiftErrorReport As Boolean
        Get
            Return IsQualityControlManager OrElse IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanDeletePlasticShiftErrorReport As Boolean
        Get
            Return IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanManagePlasticShiftEmailRecipients As Boolean
        Get
            Return IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanManageMechanismQualityEmailRecipients As Boolean
        Get
            Return IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanManageTestRequestEmailRecipients As Boolean
        Get
            Return IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenTestRequests As Boolean
        Get
            Return CanCreateTestRequest OrElse CanProcessTestRequest
        End Get
    End Property

    Public Shared ReadOnly Property CanCreateTestRequest As Boolean
        Get
            Return IsMechanismQualityControlUser OrElse
                   IsIncomingQualityControlUser OrElse
                   IsPlasticQualityControlUser OrElse
                   IsQualityControlManager OrElse
                   IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanProcessTestRequest As Boolean
        Get
            Return IsLaboratoryUser OrElse IsQualityControlManager OrElse IsAdmin
        End Get
    End Property

    Public Shared Function CanProcessTestRequestForDepartment(requestedDepartment As String) As Boolean
        If String.Equals(If(requestedDepartment, "").Trim(), "MEKANİZMA", StringComparison.OrdinalIgnoreCase) Then
            Return IsMechanismQualityControlUser OrElse IsAdmin
        End If

        Return CanProcessTestRequest
    End Function

    Public Shared ReadOnly Property CanAssignTestRequestTests As Boolean
        Get
            Return IsQualityControlManager OrElse IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanOverrideTestRequestSteps As Boolean
        Get
            Return IsQualityControlManager OrElse IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanDeleteTestRequests As Boolean
        Get
            Return IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanOpenPackageMeterControls As Boolean
        Get
            Return IsLaboratoryUser OrElse
                   IsQualityControlManager OrElse
                   IsManager OrElse
                   IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanModifyPackageMeterControls As Boolean
        Get
            Return IsLaboratoryUser OrElse IsQualityControlManager OrElse IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanDeletePackageMeterControls As Boolean
        Get
            Return IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanManagePackageMeterEmailRecipients As Boolean
        Get
            Return IsAdmin
        End Get
    End Property

    Public Shared ReadOnly Property CanViewPermissionMatrix As Boolean
        Get
            Return IsManager OrElse IsAdmin
        End Get
    End Property
End Class
