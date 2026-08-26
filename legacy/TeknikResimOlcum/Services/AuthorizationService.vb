Imports System.Windows.Forms

Public NotInheritable Class AuthorizationService
    Private Sub New()
    End Sub

    Public Shared Sub Require(canOpen As Boolean, screenName As String)
        ' Admin sistem genelinde tam yetkilidir. Yeni ekran veya işlem izinlerine
        ' yanlışlıkla ayrıca eklenmese bile merkezi kontrol Admin'i engellemez.
        If AppState.IsAdmin OrElse canOpen Then Return

        Dim userName = If(String.IsNullOrWhiteSpace(AppState.CurrentUserName), "(oturum yok)", AppState.CurrentUserName)
        Dim roleName = If(String.IsNullOrWhiteSpace(AppState.CurrentRole), "(rol yok)", AppState.NormalizeRole(AppState.CurrentRole))

        Throw New UnauthorizedAccessException("Bu ekrani acma yetkiniz yok: " & screenName & Environment.NewLine &
                                              "Kullanici: " & userName & Environment.NewLine &
                                              "Rol: " & roleName)
    End Sub

    Public Shared Sub ShowDenied(ex As UnauthorizedAccessException, owner As IWin32Window)
        Try
            AuditService.Log("AUTH_DENIED", "", "", ex.Message)
        Catch auditEx As Exception
            ErrorLogService.Log("AuthorizationService.ShowDenied.Audit", auditEx)
        End Try

        MessageBox.Show(owner,
                        ex.Message,
                        "Yetkisiz islem",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning)
    End Sub
End Class
