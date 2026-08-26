Imports System.Linq
Imports System.Net
Imports System.Text

Public NotInheritable Class PackageMeterEmailNotificationService
    Private Sub New()
    End Sub

    Public Shared Function TryNotifyNewUnsuitableMeters(controlId As String,
                                                        unsuitableLines As IEnumerable(Of PackageMeterControlLine),
                                                        ByRef errorMessage As String) As Boolean
        errorMessage = ""
        controlId = If(controlId, "").Trim()
        If controlId = "" Then
            errorMessage = "Mail gönderimi için paket sayaç kontrol numarası bulunamadı."
            Return False
        End If

        Try
            Dim lines = If(unsuitableLines, Enumerable.Empty(Of PackageMeterControlLine)()).
                Where(Function(line) line IsNot Nothing AndAlso
                      String.Equals(If(line.OverallResult, "").Trim(), "UYGUN DEĞİL", StringComparison.OrdinalIgnoreCase)).
                OrderBy(Function(line) line.SortNo).
                ToList()
            If lines.Count = 0 Then Return True

            Dim header = DataService.GetPackageMeterControls().
                FirstOrDefault(Function(row)
                                   Return String.Equals(DataService.GetValue(row, "ControlId").Trim(),
                                                        controlId,
                                                        StringComparison.OrdinalIgnoreCase)
                               End Function)
            If header Is Nothing Then
                errorMessage = "Uygun olmayan sayaç kaydedildi ancak mail için kontrol bilgileri yeniden okunamadı."
                AuditService.Log("PACKAGE_METER_NOT_SUITABLE_EMAIL_FAILED", "", "",
                                 "Kontrol No=" & controlId & "; Neden=Kontrol kaydı yeniden okunamadı")
                Return False
            End If

            Dim recipients = DataService.GetPackageMeterEmailRecipients(True).
                Where(Function(item) Not String.IsNullOrWhiteSpace(item.Email)).
                ToList()
            If recipients.Count = 0 Then
                errorMessage = "Aktif paket sayaç Uygun Değil mail alıcısı tanımlı değil. 'Uygun Değil Mail Alıcıları' penceresinden en az bir aktif alıcı ekleyin."
                AuditService.Log("PACKAGE_METER_NOT_SUITABLE_EMAIL_FAILED", "", "",
                                 "Kontrol No=" & controlId & "; Neden=Aktif alıcı yok")
                Return False
            End If

            Dim toEmails = recipients.
                Where(Function(item) Not IsCcRecipient(item)).
                Select(Function(item) item.Email.Trim()).
                Distinct(StringComparer.OrdinalIgnoreCase).
                ToList()
            Dim ccEmails = recipients.
                Where(Function(item) IsCcRecipient(item)).
                Select(Function(item) item.Email.Trim()).
                Distinct(StringComparer.OrdinalIgnoreCase).
                Where(Function(email) Not toEmails.Contains(email, StringComparer.OrdinalIgnoreCase)).
                ToList()
            If toEmails.Count = 0 Then
                errorMessage = "Mail gönderimi için en az bir aktif Kime alıcısı tanımlanmalıdır."
                AuditService.Log("PACKAGE_METER_NOT_SUITABLE_EMAIL_FAILED", "", "",
                                 "Kontrol No=" & controlId & "; Neden=Aktif Kime alıcısı yok")
                Return False
            End If

            Dim toText = String.Join(";", toEmails)
            Dim ccText = String.Join(";", ccEmails)
            Dim meterModel = DataService.GetValue(header, "MeterModel").Trim()
            Dim subject = "Paket Sayaç Kontrolü - Uygun Değil"
            If meterModel <> "" Then subject &= " - " & meterModel
            subject &= " - " & lines.Count.ToString() & " sayaç"

            Dim html As New StringBuilder()
            html.AppendLine("<!DOCTYPE html><html><head><meta charset=""utf-8""></head><body style=""font-family:Segoe UI,Arial,sans-serif;font-size:13px;color:#1f2937"">")
            html.AppendLine("<h2 style=""color:#991b1b"">Paket Sayaç Kontrolü - Uygun Değil</h2>")
            html.AppendLine("<p>Bir veya daha fazla sayacın test sonucu <strong>UYGUN DEĞİL</strong> olarak kaydedildi.</p>")
            html.AppendLine("<table style=""border-collapse:collapse;margin-bottom:14px"">")
            AppendInfoRow(html, "Kontrol No", controlId)
            AppendInfoRow(html, "Sayaç Modeli", meterModel)
            AppendInfoRow(html, "Müşteri", DataService.GetValue(header, "Customer"))
            AppendInfoRow(html, "Üretim Pano", DataService.GetValue(header, "ProductionPanelNo"))
            AppendInfoRow(html, "Kontrol Pano", DataService.GetValue(header, "ControlPanelNo"))
            AppendInfoRow(html, "Operatör", DataService.GetValue(header, "OperatorInfo"))
            AppendInfoRow(html, "Kontrol Eden", DataService.GetValue(header, "ControllerName"))
            AppendInfoRow(html, "Kontrol Tarihi", DataService.GetValue(header, "ControlDate"))
            AppendInfoRow(html, "Açıklama", DataService.GetValue(header, "Explanation"), True)
            html.AppendLine("</table>")

            html.AppendLine("<table style=""border-collapse:collapse;width:100%;font-size:12px"">")
            html.AppendLine("<thead><tr style=""background:#fee2e2;color:#7f1d1d"">")
            For Each caption In {"Sıra", "Seri No", "Etiket Q3", "Etiket Q2", "Etiket Q1", "Test Q4", "Test Q3", "Test Q2", "Test Q1", "Kredi", "Vana", "Sonuç"}
                html.Append("<th style=""border:1px solid #cbd5e1;padding:6px;text-align:left"">" & WebUtility.HtmlEncode(caption) & "</th>")
            Next
            html.AppendLine("</tr></thead><tbody>")
            For Each line In lines
                html.AppendLine("<tr>")
                AppendCell(html, line.SortNo.ToString())
                AppendCell(html, line.SerialNumber)
                AppendCell(html, line.LabelErrorQ3)
                AppendCell(html, line.LabelErrorQ2)
                AppendCell(html, line.LabelErrorQ1)
                AppendCell(html, line.TestFlowQ4Manual)
                AppendCell(html, line.TestFlowQ3)
                AppendCell(html, line.TestFlowQ2)
                AppendCell(html, line.TestFlowQ1)
                AppendCell(html, line.CreditResult)
                AppendCell(html, line.ValveResult)
                AppendCell(html, line.OverallResult, True)
                html.AppendLine("</tr>")
            Next
            html.AppendLine("</tbody></table></body></html>")

            If OutlookEmailDraftService.TrySendMail(subject, html.ToString(), toText, ccText) Then
                AuditService.Log("PACKAGE_METER_NOT_SUITABLE_EMAIL_SENT", "", "",
                                 "Kontrol No=" & controlId & "; Sayaç=" & lines.Count.ToString() &
                                 "; Seriler=" & String.Join(",", lines.Select(Function(line) line.SerialNumber)) &
                                 "; Kime=" & toText & "; CC=" & ccText)
                Return True
            End If

            errorMessage = "Outlook otomatik e-postayı gönderemedi. Outlook'un açık ve hesaba bağlı olduğunu kontrol edin."
            AuditService.Log("PACKAGE_METER_NOT_SUITABLE_EMAIL_FAILED", "", "",
                             "Kontrol No=" & controlId & "; Kime=" & toText & "; CC=" & ccText)
            Return False
        Catch ex As Exception
            ErrorLogService.Log("PackageMeterEmailNotificationService.TryNotifyNewUnsuitableMeters", ex)
            errorMessage = ex.Message
            Return False
        End Try
    End Function

    Private Shared Sub AppendInfoRow(html As StringBuilder,
                                     caption As String,
                                     value As String,
                                     Optional preserveLines As Boolean = False)
        Dim encoded = WebUtility.HtmlEncode(If(value, ""))
        If preserveLines Then encoded = encoded.Replace(vbCrLf, "<br>").Replace(vbCr, "<br>").Replace(vbLf, "<br>")
        html.AppendLine("<tr><th style=""border:1px solid #cbd5e1;background:#f8fafc;padding:7px;text-align:left"">" &
                        WebUtility.HtmlEncode(caption) &
                        "</th><td style=""border:1px solid #cbd5e1;padding:7px"">" & encoded & "</td></tr>")
    End Sub

    Private Shared Sub AppendCell(html As StringBuilder, value As String, Optional emphasize As Boolean = False)
        Dim style = "border:1px solid #cbd5e1;padding:6px;text-align:left;vertical-align:top"
        If emphasize Then style &= ";background:#fee2e2;color:#991b1b;font-weight:700"
        html.Append("<td style=""" & style & """>" & WebUtility.HtmlEncode(If(value, "")) & "</td>")
    End Sub

    Private Shared Function IsCcRecipient(item As PlasticShiftEmailRecipient) As Boolean
        If item Is Nothing Then Return False
        Return String.Equals(If(item.RecipientType, "").Trim(), "CC", StringComparison.OrdinalIgnoreCase)
    End Function
End Class
