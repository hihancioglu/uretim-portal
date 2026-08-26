Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Text

Public NotInheritable Class PlasticShiftEmailNotificationService
    Private Sub New()
    End Sub

    Public Shared Function TryNotifyNewRecord(row As Dictionary(Of String, String),
                                              ByRef errorMessage As String,
                                              Optional mechanismMode As Boolean = False) As Boolean
        errorMessage = ""

        Try
            If row Is Nothing Then Return True

            Dim recipients = If(mechanismMode,
                                DataService.GetMechanismShiftEmailRecipients(True),
                                DataService.GetPlasticShiftEmailRecipients(True)).
                Where(Function(item) Not String.IsNullOrWhiteSpace(item.Email)).
                ToList()

            If recipients.Count = 0 Then Return True

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

            If toEmails.Count = 0 AndAlso ccEmails.Count = 0 Then Return True

            Dim toText = String.Join(";", toEmails)
            Dim ccText = String.Join(";", ccEmails)
            Dim occurredAtText = FormatDateTime(DataService.GetValue(row, "OccurredAt"))
            Dim productText = DataService.GetValue(row, "ProductNameCode").Trim()
            Dim featureName = If(mechanismMode, "Mekanizma", "Plastikhane")
            Dim subject = "Yeni " & featureName & " Vardiya Takip Kaydı"
            If productText <> "" Then subject &= " - " & productText

            Dim attachmentPaths = ResolvePhotoAttachments(DataService.GetValue(row, "RecordId"), mechanismMode)
            Dim htmlBody = BuildHtml(row, occurredAtText, featureName, attachmentPaths.Count)
            If OutlookEmailDraftService.TrySendMail(subject, htmlBody, toText, ccText, attachmentPaths) Then
                AuditService.Log(
                    If(mechanismMode, "MECHANISM_SHIFT_AUTO_EMAIL_SENT", "PLASTIC_SHIFT_AUTO_EMAIL_SENT"),
                    "",
                    "",
                    "Kayıt No=" & DataService.GetValue(row, "RecordId") & "; Kime=" & toText & "; CC=" & ccText & "; Fotoğraf=" & attachmentPaths.Count.ToString())
                Return True
            End If

            errorMessage = "Outlook otomatik e-posta gönderemedi. Outlook'un açık/kurulu ve bu bilgisayarda kullanılabilir olduğunu kontrol edin."
            AuditService.Log(
                If(mechanismMode, "MECHANISM_SHIFT_AUTO_EMAIL_FAILED", "PLASTIC_SHIFT_AUTO_EMAIL_FAILED"),
                "",
                "",
                "Kayıt No=" & DataService.GetValue(row, "RecordId") & "; Kime=" & toText & "; CC=" & ccText & "; Fotoğraf=" & attachmentPaths.Count.ToString())
            Return False
        Catch ex As Exception
            ErrorLogService.Log(If(mechanismMode,
                                   "MechanismShiftEmailNotificationService.NotifyNewRecord",
                                   "PlasticShiftEmailNotificationService.NotifyNewRecord"), ex)
            errorMessage = "Otomatik e-posta gönderimi sırasında hata oluştu: " & ex.Message
            Return False
        End Try
    End Function

    Public Shared Function TryOpenNewRecordDraft(row As Dictionary(Of String, String),
                                                 ByRef errorMessage As String,
                                                 Optional mechanismMode As Boolean = False) As Boolean
        errorMessage = ""

        Try
            If row Is Nothing Then
                errorMessage = "Taslağı hazırlanacak kayıt bulunamadı."
                Return False
            End If

            Dim recipients = If(mechanismMode,
                                DataService.GetMechanismShiftEmailRecipients(True),
                                DataService.GetPlasticShiftEmailRecipients(True)).
                Where(Function(item) Not String.IsNullOrWhiteSpace(item.Email)).
                ToList()
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

            Dim toText = String.Join(";", toEmails)
            Dim ccText = String.Join(";", ccEmails)
            Dim occurredAtText = FormatDateTime(DataService.GetValue(row, "OccurredAt"))
            Dim productText = DataService.GetValue(row, "ProductNameCode").Trim()
            Dim featureName = If(mechanismMode, "Mekanizma", "Plastikhane")
            Dim subject = "Yeni " & featureName & " Vardiya Takip Kaydı"
            If productText <> "" Then subject &= " - " & productText

            Dim attachmentPaths = ResolvePhotoAttachments(DataService.GetValue(row, "RecordId"), mechanismMode)
            Dim htmlBody = BuildHtml(row, occurredAtText, featureName, attachmentPaths.Count)
            If Not OutlookEmailDraftService.TryOpenEditableDraft(subject, htmlBody, toText, ccText, attachmentPaths) Then
                errorMessage = "Outlook düzenlenebilir e-posta taslağı açılamadı. Outlook'un açık/kurulu ve bu bilgisayarda kullanılabilir olduğunu kontrol edin."
                Return False
            End If

            AuditService.Log(
                If(mechanismMode, "MECHANISM_SHIFT_EMAIL_DRAFT_OPENED", "PLASTIC_SHIFT_EMAIL_DRAFT_OPENED"),
                "",
                "",
                "Kayıt No=" & DataService.GetValue(row, "RecordId") & "; Kime=" & toText & "; CC=" & ccText & "; Fotoğraf=" & attachmentPaths.Count.ToString())
            Return True
        Catch ex As Exception
            ErrorLogService.Log(If(mechanismMode,
                                   "MechanismShiftEmailNotificationService.OpenRecordDraft",
                                   "PlasticShiftEmailNotificationService.OpenRecordDraft"), ex)
            errorMessage = "E-posta taslağı hazırlanırken hata oluştu: " & ex.Message
            Return False
        End Try
    End Function

    Private Shared Function BuildHtml(row As Dictionary(Of String, String),
                                      occurredAtText As String,
                                      featureName As String,
                                      attachmentCount As Integer) As String
        Dim html As New StringBuilder()
        html.AppendLine("<!DOCTYPE html><html><head><meta charset=""utf-8""></head>")
        html.AppendLine("<body style=""font-family:Segoe UI,Arial,sans-serif;font-size:13px;color:#1f2937;background:#ffffff;"">")
        html.AppendLine("<h2 style=""margin:0 0 8px;color:#1f477e;"">Yeni " & EncodeHtml(featureName) & " Vardiya Takip Kaydı</h2>")
        html.AppendLine("<p style=""margin:0 0 14px;color:#4b5563;"">Program üzerinden yeni bir vardiya takip kaydı oluşturuldu.</p>")

        html.AppendLine("<table style=""border-collapse:collapse;width:100%;max-width:980px;"">")
        AppendRow(html, "Kayıt No", DataService.GetValue(row, "RecordId"))
        AppendRow(html, "Tarih / Saat", occurredAtText)
        AppendRow(html, "Hatalı Adet / Miktar", DataService.GetValue(row, "DefectiveQuantity"))
        AppendRow(html, "Sorumlu", DataService.GetValue(row, "Responsible"))
        AppendRow(html, "Ürün Adı ve Kodu", DataService.GetValue(row, "ProductNameCode"))
        AppendRow(html, "Sorun", DataService.GetValue(row, "Problem"), True)
        AppendRow(html, "Alınan Aksiyon", DataService.GetValue(row, "ActionTaken"), True)
        AppendRow(html, "Sarı Kart", FlagText(row, "YellowCard"))
        AppendRow(html, "Kalıp Tadilat", FlagText(row, "MoldModification"))
        AppendRow(html, "Hata Raporu", FlagText(row, "ErrorReport"))
        AppendRow(html, "Test", FlagText(row, "TestPerformed"))
        If attachmentCount > 0 Then AppendRow(html, "Ekli Fotoğraf", attachmentCount.ToString() & " adet")
        AppendRow(html, "Kaydı Oluşturan", DataService.GetValue(row, "CreatedBy"))
        AppendRow(html, "Bilgisayar", DataService.GetValue(row, "ComputerName"))
        html.AppendLine("</table>")

        html.AppendLine("<p style=""margin-top:16px;"">Bilginize.</p>")
        html.AppendLine("</body></html>")
        Return html.ToString()
    End Function

    Private Shared Function ResolvePhotoAttachments(recordId As String, mechanismMode As Boolean) As List(Of String)
        Dim result As New List(Of String)()
        Dim uniquePaths As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        For Each photo In ShiftTrackingPhotoService.GetPhotos(recordId, mechanismMode)
            Try
                Dim photoPath = ShiftTrackingPhotoService.ResolvePhotoPath(photo)
                If photoPath <> "" AndAlso File.Exists(photoPath) AndAlso uniquePaths.Add(photoPath) Then
                    result.Add(photoPath)
                End If
            Catch ex As Exception
                ErrorLogService.Log("PlasticShiftEmailNotificationService.ResolvePhotoAttachment", ex, DataService.GetValue(photo, "RelativePath"))
            End Try
        Next

        Return result
    End Function

    Private Shared Sub AppendRow(html As StringBuilder, caption As String, value As String, Optional preserveLines As Boolean = False)
        Dim encodedValue = EncodeHtml(value)
        If preserveLines Then encodedValue = encodedValue.Replace(vbCrLf, "<br>").Replace(vbCr, "<br>").Replace(vbLf, "<br>")

        html.AppendLine("<tr>")
        html.AppendLine("<th style=""border:1px solid #cbd5e1;background:#e7eef8;color:#1f477e;padding:8px;text-align:left;width:190px;"">" & EncodeHtml(caption) & "</th>")
        html.AppendLine("<td style=""border:1px solid #cbd5e1;padding:8px;vertical-align:top;"">" & encodedValue & "</td>")
        html.AppendLine("</tr>")
    End Sub

    Private Shared Function FlagText(row As Dictionary(Of String, String), key As String) As String
        Dim value = DataService.GetValue(row, key).Trim().ToUpperInvariant()
        If value = "YES" OrElse value = "EVET" OrElse value = "TRUE" OrElse value = "1" OrElse value = "X" Then Return "EVET"
        Return "-"
    End Function

    Private Shared Function IsCcRecipient(item As PlasticShiftEmailRecipient) As Boolean
        If item Is Nothing Then Return False
        Return String.Equals(If(item.RecipientType, "").Trim(), "CC", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function FormatDateTime(value As String) As String
        Dim parsed As DateTime
        If DateTime.TryParse(value, parsed) Then Return parsed.ToString("dd.MM.yyyy HH:mm")
        Return If(value, "")
    End Function

    Private Shared Function EncodeHtml(value As String) As String
        Return WebUtility.HtmlEncode(If(value, ""))
    End Function
End Class
