Public NotInheritable Class AuditService
    Private Sub New()
    End Sub

    Public Shared Sub Log(action As String, trCode As String, drawingRev As String, detail As String)
        Try
            CsvUtil.AppendRowFastLocked(
                AppPaths.AuditLogCsv,
                DataService.AuditHeaders,
                New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                    {"LogId", DateTime.Now.ToString("yyyyMMddHHmmssfff")},
                    {"DateTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")},
                    {"UserName", AppState.CurrentUserName},
                    {"Role", AppState.CurrentRole},
                    {"ComputerName", Environment.MachineName},
                    {"Action", action},
                    {"TrCode", trCode},
                    {"DrawingRev", drawingRev},
                    {"Detail", detail}
                })
        Catch ex As Exception
            ErrorLogService.Log(
                "AuditService.Log",
                ex,
                "Action=" & If(action, "") & "; TrCode=" & If(trCode, "") & "; DrawingRev=" & If(drawingRev, ""))
            ' Log yazılamazsa ana işlemi bozma.
        End Try
    End Sub
End Class
