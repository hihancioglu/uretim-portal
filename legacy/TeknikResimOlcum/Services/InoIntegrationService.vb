Imports System.IO
Imports System.Windows.Forms

Public NotInheritable Class InoIntegrationService
    Private Sub New()
    End Sub

    Public Shared Function CreateForm() As Form
        AuthorizationService.Require(AppState.CanOpenInoTracking, "İNO-1 / İNO-2 Takip")

        Dim dataDirectory = EnsureDataDirectory()
        Dim forcedReadOnly = AppState.IsPlanningUser OrElse AppState.IsManager
        Dim inoRole = If(forcedReadOnly, AppState.NormalizeRole(AppState.CurrentRole), MapCurrentRole())

        AuditService.Log(
            "INO_MODULE_OPEN",
            "",
            "",
            $"İNO takip penceresi açıldı; INO Rolü={inoRole}; SaltOkunur={forcedReadOnly}; Veri={dataDirectory}")

        Return New InoTakipCsvApp.MainForm(
            dataDirectory,
            AppState.CurrentUserName,
            inoRole,
            AddressOf WriteCentralAudit,
            forcedReadOnly)
    End Function

    Private Shared Sub WriteCentralAudit(actionName As String, sira As String, sayacAdi As String, details As String)
        Dim centralDetail = $"SIRA={If(sira, "")}; SAYAÇ ADI={If(sayacAdi, "")}; {If(details, "")}"
        AuditService.Log("INO_" & If(actionName, ""), "", "", centralDetail)
    End Sub

    Private Shared Function EnsureDataDirectory() As String
        Dim dataDirectory = Path.Combine(AppPaths.DataDir, "INO")
        Directory.CreateDirectory(dataDirectory)

        Dim databasePath = Path.Combine(dataDirectory, "INO_Database.csv")
        If File.Exists(databasePath) Then Return dataDirectory

        Dim seedPath = Path.Combine(
            AppPaths.BaseDir,
            "Resources",
            "INO",
            "INO_Database.seed.csv")

        If Not File.Exists(seedPath) Then
            Throw New FileNotFoundException(
                "İNO başlangıç veritabanı bulunamadı. Güncelleme paketini yeniden uygulayın.",
                seedPath)
        End If

        Try
            File.Copy(seedPath, databasePath, False)
        Catch ex As IOException
            ' Ortak klasörde iki bilgisayar ilk kurulumu aynı anda yaparsa,
            ' diğer bilgisayarın oluşturduğu geçerli dosya kullanılabilir.
            If Not File.Exists(databasePath) Then Throw
        End Try

        Return dataDirectory
    End Function

    Private Shared Function MapCurrentRole() As String
        If AppState.IsAdmin Then Return InoTakipCsvApp.UserStore.RoleAdmin
        If AppState.IsMechanismQualityControlUser OrElse AppState.IsMechanismManager Then
            Return InoTakipCsvApp.UserStore.RoleMechanism
        End If
        If AppState.IsLaboratoryUser OrElse AppState.IsQualityControlManager Then
            Return InoTakipCsvApp.UserStore.RoleApproval
        End If

        Return InoTakipCsvApp.UserStore.RoleApproval
    End Function
End Class
