Imports System.Linq
Imports System.Net
Imports System.Text

Public NotInheritable Class MechanismQualityEmailNotificationService
    Private Sub New()
    End Sub

    Public Shared Function TryNotifyNotSuitable(controlId As String, ByRef errorMessage As String) As Boolean
        errorMessage = ""
        controlId = If(controlId, "").Trim()
        If controlId = "" Then
            errorMessage = "Mail gönderimi için mekanizma kalite kontrol kayıt numarası bulunamadı."
            Return False
        End If

        Try
            Dim row = DataService.GetMechanismQualityControls().
                FirstOrDefault(Function(item)
                                   Return String.Equals(
                                       DataService.GetValue(item, "ControlId").Trim(),
                                       controlId,
                                       StringComparison.OrdinalIgnoreCase)
                               End Function)

            If row Is Nothing Then
                errorMessage = "Uygun değil sonucu kaydedildi ancak mail için kayıt bilgileri yeniden okunamadı."
                AuditService.Log("MECHANISM_QUALITY_NOT_SUITABLE_EMAIL_FAILED", "", "",
                                 "Kayıt No=" & controlId & "; Neden=Kayıt yeniden okunamadı")
                Return False
            End If

            If Not String.Equals(DataService.GetValue(row, "IsNotSuitable").Trim(), "X", StringComparison.OrdinalIgnoreCase) Then
                errorMessage = "Kayıt Uygun Değil durumunda olmadığı için otomatik mail gönderilmedi."
                AuditService.Log("MECHANISM_QUALITY_NOT_SUITABLE_EMAIL_FAILED", "", "",
                                 "Kayıt No=" & controlId & "; Neden=Uygun Değil sonucu doğrulanamadı")
                Return False
            End If

            Return TryNotifyNotSuitable(row, errorMessage)
        Catch ex As Exception
            ErrorLogService.Log("MechanismQualityEmailNotificationService.TryNotifyNotSuitableById", ex)
            errorMessage = ex.Message
            Return False
        End Try
    End Function

    Public Shared Function TryNotifyNotSuitable(row As Dictionary(Of String, String), ByRef errorMessage As String) As Boolean
        errorMessage = ""
        Try
            If row Is Nothing Then
                errorMessage = "Mail gönderimi için mekanizma kalite kontrol kayıt bilgileri bulunamadı."
                Return False
            End If

            Dim recipients = DataService.GetMechanismQualityEmailRecipients(True).
                Where(Function(item) Not String.IsNullOrWhiteSpace(item.Email)).
                ToList()
            If recipients.Count = 0 Then
                errorMessage = "Aktif Uygun Değil mail alıcısı tanımlı değil. 'Uygun Değil Mail Alıcıları' penceresinden en az bir aktif alıcı ekleyin."
                AuditService.Log("MECHANISM_QUALITY_NOT_SUITABLE_EMAIL_FAILED", "", "",
                                 "Kayıt No=" & DataService.GetValue(row, "ControlId") & "; Neden=Aktif alıcı yok")
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
            If toEmails.Count = 0 AndAlso ccEmails.Count = 0 Then
                errorMessage = "Aktif mail alıcılarının e-posta adresleri boş. Alıcı tanımlarını kontrol edin."
                AuditService.Log("MECHANISM_QUALITY_NOT_SUITABLE_EMAIL_FAILED", "", "",
                                 "Kayıt No=" & DataService.GetValue(row, "ControlId") & "; Neden=Geçerli alıcı adresi yok")
                Return False
            End If

            Dim toText = String.Join(";", toEmails)
            Dim ccText = String.Join(";", ccEmails)
            Dim product = DataService.GetValue(row, "ProductNameCode").Trim()
            Dim subject = "Mekanizma Kalite Kontrol - Uygun Değil"
            If product <> "" Then subject &= " - " & product
            Dim html As New StringBuilder()
            html.AppendLine("<!DOCTYPE html><html><head><meta charset=""utf-8""></head><body style=""font-family:Segoe UI,Arial,sans-serif;font-size:13px;color:#1f2937"">")
            html.AppendLine("<h2 style=""color:#991b1b"">Mekanizma Kalite Kontrol - Uygun Değil</h2>")
            html.AppendLine("<p>Bir mekanizma kalite kontrol kaydı Uygun Değil olarak sonuçlandırıldı.</p><table style=""border-collapse:collapse"">")
            AppendRow(html, "Kayıt No", DataService.GetValue(row, "ControlId"))
            AppendRow(html, "Ürün / TR", product)
            AppendRow(html, "Teslim Eden", DataService.GetValue(row, "DeliveredBy"))
            AppendRow(html, "Kontrol Eden", DataService.GetValue(row, "ControlledBy"))
            AppendRow(html, "Montaj Mekanizma / Sayaç", DataService.GetValue(row, "MountedMechanismCounter"))
            AppendRow(html, "Kontrol Açıklaması", DataService.GetValue(row, "ControlExplanation"), True)
            AppendRow(html, "Kontrol Tarihi", DataService.GetValue(row, "ControlledAt"))
            html.AppendLine("</table></body></html>")
            If OutlookEmailDraftService.TrySendMail(subject, html.ToString(), toText, ccText) Then
                AuditService.Log("MECHANISM_QUALITY_NOT_SUITABLE_EMAIL_SENT", "", "", "Kayit No=" & DataService.GetValue(row, "ControlId") & "; Kime=" & toText & "; CC=" & ccText)
                Return True
            End If
            errorMessage = "Outlook otomatik e-postayı gönderemedi. Outlook'un açık ve hesaba bağlı olduğunu kontrol edin."
            AuditService.Log("MECHANISM_QUALITY_NOT_SUITABLE_EMAIL_FAILED", "", "", "Kayit No=" & DataService.GetValue(row, "ControlId") & "; Kime=" & toText & "; CC=" & ccText)
            Return False
        Catch ex As Exception
            ErrorLogService.Log("MechanismQualityEmailNotificationService.TryNotifyNotSuitable", ex)
            errorMessage = ex.Message
            Return False
        End Try
    End Function

    Private Shared Sub AppendRow(html As StringBuilder, caption As String, value As String, Optional preserveLines As Boolean = False)
        Dim encoded = WebUtility.HtmlEncode(If(value, ""))
        If preserveLines Then encoded = encoded.Replace(vbCrLf, "<br>").Replace(vbCr, "<br>").Replace(vbLf, "<br>")
        html.AppendLine("<tr><th style=""border:1px solid #cbd5e1;background:#fee2e2;padding:7px;text-align:left"">" & WebUtility.HtmlEncode(caption) & "</th><td style=""border:1px solid #cbd5e1;padding:7px"">" & encoded & "</td></tr>")
    End Sub

    Private Shared Function IsCcRecipient(item As PlasticShiftEmailRecipient) As Boolean
        If item Is Nothing Then Return False
        Return String.Equals(If(item.RecipientType, "").Trim(), "CC", StringComparison.OrdinalIgnoreCase)
    End Function
End Class
