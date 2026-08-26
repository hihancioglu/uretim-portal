Imports System.Net
Imports System.Text

Public NotInheritable Class PlasticShiftErrorReportEmailNotificationService
    Private Sub New()
    End Sub

    Public Shared Function TrySendCreatedNotification(reportId As String,
                                                      reportRow As Dictionary(Of String, String),
                                                      ByRef warningMessage As String) As Boolean
        warningMessage = ""
        reportId = If(reportId, "").Trim()
        If reportId = "" OrElse reportRow Is Nothing Then Return True

        Dim eventKey = "REPORT_CREATED|" & reportId
        If DataService.HasPlasticShiftErrorReportEmailEvent(eventKey) Then Return True

        Dim evaluations = DataService.GetPlasticShiftErrorReportEvaluations(reportId)
        Dim recipients = evaluations.
            Select(Function(item) If(item.AssignedEmail, "").Trim()).
            Where(Function(value) value <> "").
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToList()
        If recipients.Count = 0 Then
            warningMessage = "Hata raporu kaydedildi; ancak üçlü değerlendirme atamalarında e-posta adresi bulunmadığı için bildirim gönderilemedi."
            Return False
        End If

        Dim subject = "Değerlendirme Bekleyen Hata Raporu - " &
                      DataService.GetValue(reportRow, "ReportNo")
        Dim html As New StringBuilder()
        html.Append("<html><body style='font-family:Segoe UI,Arial;font-size:10pt;color:#24364b'>")
        html.Append("<h2 style='color:#235288'>Yeni Hata Raporu Değerlendirme Bekliyor</h2>")
        html.Append("<table cellpadding='7' cellspacing='0' style='border-collapse:collapse;border:1px solid #cbd7e6'>")
        AddRow(html, "Rapor No", DataService.GetValue(reportRow, "ReportNo"))
        AddRow(html, "Parça / Ürün", DataService.GetValue(reportRow, "PartNameNo"))
        AddRow(html, "TR No", DataService.GetValue(reportRow, "TrNo"))
        AddRow(html, "Miktar", DataService.GetValue(reportRow, "Quantity"))
        AddRow(html, "Uygunsuzluk", DataService.GetValue(reportRow, "NonconformityDescription"))
        AddRow(html, "Oluşturan", DataService.GetValue(reportRow, "CreatedBy"))
        html.Append("</table>")
        html.Append("<h3 style='color:#235288'>Atanan değerlendiriciler</h3>")
        html.Append("<table cellpadding='7' cellspacing='0' style='border-collapse:collapse;border:1px solid #cbd7e6'>")
        html.Append("<tr style='background:#e4eef9'><th>Pozisyon</th><th>Kullanıcı</th></tr>")
        For Each item In evaluations
            html.Append("<tr><td style='border:1px solid #cbd7e6'>").
                Append(WebUtility.HtmlEncode(item.PositionName)).
                Append("</td><td style='border:1px solid #cbd7e6'>").
                Append(WebUtility.HtmlEncode(item.AssignedUserName)).
                Append("</td></tr>")
        Next
        html.Append("</table><p>Programdaki hata raporu detayından kendi değerlendirmenizi kaydedebilirsiniz.</p></body></html>")

        Dim toText = String.Join(";", recipients)
        If Not OutlookEmailDraftService.TrySendMail(subject, html.ToString(), toText, "") Then
            warningMessage = "Hata raporu kaydedildi; ancak Outlook üzerinden değerlendirme e-postası gönderilemedi."
            Return False
        End If

        DataService.RecordPlasticShiftErrorReportEmailEvent(eventKey, reportId, "REPORT_CREATED", toText)
        Return True
    End Function

    Private Shared Sub AddRow(builder As StringBuilder, caption As String, value As String)
        builder.Append("<tr><td style='font-weight:bold;background:#f4f7fb;border:1px solid #cbd7e6'>").
            Append(WebUtility.HtmlEncode(caption)).
            Append("</td><td style='border:1px solid #cbd7e6'>").
            Append(WebUtility.HtmlEncode(If(value, ""))).
            Append("</td></tr>")
    End Sub
End Class
