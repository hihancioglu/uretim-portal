Imports System.Linq
Imports System.IO
Imports System.Net
Imports System.Text

Public NotInheritable Class TestRequestEmailNotificationService
    Public Const AllDepartments As String = "ALL"
    Public Const EventRequestCreated As String = "REQUEST_CREATED"
    Public Const EventRequestAccepted As String = "REQUEST_ACCEPTED"
    Public Const EventCompletedSuitable As String = "REQUEST_COMPLETED_SUITABLE"
    Public Const EventCompletedUnsuitable As String = "REQUEST_COMPLETED_UNSUITABLE"
    Public Const EventRequestCancelled As String = "REQUEST_CANCELLED"

    Private Sub New()
    End Sub

    Public Shared ReadOnly Property SupportedEventTypes As String()
        Get
            Return {EventRequestCreated, EventRequestAccepted, EventCompletedSuitable, EventCompletedUnsuitable, EventRequestCancelled}
        End Get
    End Property

    Public Shared ReadOnly Property SupportedDepartments As String()
        Get
            Return {AllDepartments, "GKK", "MEKANİZMA", "PLASTİKHANE", "KARTLI SAYAÇ", "ELEKTRİK SAYACI", "DİĞER"}
        End Get
    End Property

    Public Shared Function NormalizeDepartment(value As String) As String
        Dim normalized = If(value, "").Trim().ToUpperInvariant()
        If normalized = "" OrElse normalized = "ALL" OrElse normalized = "TÜM BÖLÜMLER" Then Return AllDepartments
        Return normalized
    End Function

    Public Shared Function SplitDepartments(value As String) As List(Of String)
        Return If(value, "").
            Split(New Char() {"|"c, ";"c, ","c}, StringSplitOptions.RemoveEmptyEntries).
            Select(Function(part) NormalizeDepartment(part)).
            Where(Function(part) part <> AllDepartments).
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToList()
    End Function

    Public Shared Function SerializeDepartments(departments As IEnumerable(Of String)) As String
        Dim normalized = If(departments, Enumerable.Empty(Of String)()).
            Select(Function(item) NormalizeDepartment(item)).
            Where(Function(item) item <> AllDepartments).
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToList()
        Return String.Join(" | ", normalized)
    End Function

    Public Shared Function FormatDepartmentList(value As String) As String
        Return FormatDepartmentList(SplitDepartments(value))
    End Function

    Public Shared Function FormatDepartmentList(departments As IEnumerable(Of String)) As String
        Return String.Join(" + ", If(departments, Enumerable.Empty(Of String)()).
            Select(Function(item) DepartmentDisplayName(item)).
            Where(Function(item) item <> "").
            Distinct(StringComparer.OrdinalIgnoreCase))
    End Function

    Public Shared Function ContainsDepartment(value As String, department As String) As Boolean
        Dim target = NormalizeDepartment(department)
        If target = AllDepartments Then Return True
        Return SplitDepartments(value).Any(Function(item) String.Equals(item, target, StringComparison.OrdinalIgnoreCase))
    End Function

    Public Shared Function DepartmentDisplayName(department As String) As String
        Dim normalized = NormalizeDepartment(department)
        If normalized = AllDepartments Then Return "Tüm Bölümler"
        Return normalized
    End Function

    Public Shared Function NormalizeEventType(value As String) As String
        Dim normalized = If(value, "").Trim().ToUpperInvariant()
        Select Case normalized
            Case EventRequestAccepted, EventCompletedSuitable, EventCompletedUnsuitable, EventRequestCancelled
                Return normalized
            Case Else
                ' Eski alıcı kayıtları olay bilgisi taşımıyordu; güvenli varsayılan yeni taleptir.
                Return EventRequestCreated
        End Select
    End Function

    Public Shared Function EventDisplayName(eventType As String) As String
        Select Case NormalizeEventType(eventType)
            Case EventRequestAccepted : Return "Talep işleme alındı"
            Case EventCompletedSuitable : Return "Test tamamlandı - Uygun"
            Case EventCompletedUnsuitable : Return "Test tamamlandı - Uygun Değil"
            Case EventRequestCancelled : Return "Talep iptal edildi"
            Case Else : Return "Test talebi oluşturuldu"
        End Select
    End Function

    Public Shared Function CompletionEventType(resultText As String) As String
        Dim normalized = If(resultText, "").Trim().ToUpperInvariant().Replace("İ"c, "I"c)
        If normalized.Contains("UYGUN DEGIL") OrElse normalized.Contains("UYGUN DEĞIL") OrElse normalized.Contains("NOK") Then
            Return EventCompletedUnsuitable
        End If
        Return EventCompletedSuitable
    End Function

    Public Shared Function TryNotifyEvent(row As Dictionary(Of String, String),
                                          eventType As String,
                                          ByRef errorMessage As String) As Boolean
        errorMessage = ""

        Try
            If row Is Nothing Then Return True
            Dim normalizedEvent = NormalizeEventType(eventType)
            Dim requestId = DataService.GetValue(row, "RequestId").Trim()
            Dim requestingDepartments = SplitDepartments(DataService.GetValue(row, "RequestingDepartment"))
            Dim requestingDepartmentText = FormatDepartmentList(requestingDepartments)
            If requestId = "" Then Return True

            ' Aynı talep ve olay daha önce başarıyla gönderildiyse ikinci bir mail oluşturulmaz.
            If WasEventSent(requestId, normalizedEvent) Then Return True

            Dim recipients = DataService.GetTestRequestEmailRecipients(True).
                Where(Function(item) Not String.IsNullOrWhiteSpace(item.Email) AndAlso
                                     String.Equals(NormalizeEventType(item.EventType), normalizedEvent, StringComparison.OrdinalIgnoreCase) AndAlso
                                     (String.Equals(NormalizeDepartment(item.RequestingDepartment), AllDepartments, StringComparison.OrdinalIgnoreCase) OrElse
                                      requestingDepartments.Any(Function(department) String.Equals(NormalizeDepartment(item.RequestingDepartment), department, StringComparison.OrdinalIgnoreCase)))).
                GroupBy(Function(item) item.Email.Trim(), StringComparer.OrdinalIgnoreCase).
                Select(Function(group) If(group.FirstOrDefault(Function(item) Not IsCcRecipient(item)), group.First())).
                ToList()

            If recipients.Count = 0 Then Return True

            Dim toText = String.Join(";", recipients.
                Where(Function(item) Not IsCcRecipient(item)).
                Select(Function(item) item.Email.Trim()).
                Distinct(StringComparer.OrdinalIgnoreCase))
            Dim ccText = String.Join(";", recipients.
                Where(Function(item) IsCcRecipient(item)).
                Select(Function(item) item.Email.Trim()).
                Distinct(StringComparer.OrdinalIgnoreCase))
            Dim recipientAuditText = "Kime=" & toText & "; CC=" & ccText
            Dim actionText = EventDisplayName(normalizedEvent)
            Dim productText = DataService.GetValue(row, "ProductNameTrCode").Trim()
            Dim subject = "Test / Talep Bildirimi - " & actionText
            If requestId <> "" Then subject &= " - " & requestId
            If productText <> "" Then subject &= " - " & productText

            Dim attachmentPaths As New List(Of String)()
            If normalizedEvent = EventCompletedSuitable OrElse normalizedEvent = EventCompletedUnsuitable Then
                For Each attachment In TestRequestAttachmentService.GetAttachments(requestId)
                    Dim attachmentPath = TestRequestAttachmentService.ResolveAttachmentPath(attachment)
                    If attachmentPath <> "" AndAlso File.Exists(attachmentPath) Then attachmentPaths.Add(attachmentPath)
                Next
            End If
            Dim htmlBody = BuildHtml(row, actionText, attachmentPaths.Count)
            If OutlookEmailDraftService.TrySendMail(subject, htmlBody, toText, ccText, attachmentPaths) Then
                RecordSentEvent(requestId, normalizedEvent, recipientAuditText)
                AuditService.Log(
                    "TEST_REQUEST_AUTO_EMAIL_SENT",
                    "",
                    "",
                    "Talep No=" & requestId & "; Olay=" & normalizedEvent & "; Bölüm=" & requestingDepartmentText & "; " & recipientAuditText)
                Return True
            End If

            errorMessage = "Outlook otomatik e-posta gönderemedi. Outlook'un açık/kurulu ve bu bilgisayarda kullanılabilir olduğunu kontrol edin."
            AuditService.Log(
                "TEST_REQUEST_AUTO_EMAIL_FAILED",
                "",
                "",
                "Talep No=" & requestId & "; Olay=" & normalizedEvent & "; Bölüm=" & requestingDepartmentText & "; " & recipientAuditText)
            Return False
        Catch ex As Exception
            ErrorLogService.Log("TestRequestEmailNotificationService.NotifyEvent", ex)
            errorMessage = "Otomatik e-posta gönderimi sırasında hata oluştu: " & ex.Message
            Return False
        End Try
    End Function

    Private Shared Function WasEventSent(requestId As String, eventType As String) As Boolean
        Dim eventKey = BuildEventKey(requestId, eventType)
        Return CsvUtil.ReadRows(AppPaths.TestRequestEmailEventsCsv).
            Any(Function(row) String.Equals(DataService.GetValue(row, "EventKey"), eventKey, StringComparison.OrdinalIgnoreCase))
    End Function

    Private Shared Function IsCcRecipient(item As TestRequestEmailRecipient) As Boolean
        Return item IsNot Nothing AndAlso
               String.Equals(If(item.RecipientType, "").Trim(), "CC", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Sub RecordSentEvent(requestId As String, eventType As String, recipients As String)
        Dim eventKey = BuildEventKey(requestId, eventType)
        CsvUtil.UpdateRowsLocked(
            AppPaths.TestRequestEmailEventsCsv,
            DataService.TestRequestEmailEventHeaders,
            Sub(rows)
                If rows.Any(Function(row) String.Equals(DataService.GetValue(row, "EventKey"), eventKey, StringComparison.OrdinalIgnoreCase)) Then Return
                rows.Add(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                    {"EventKey", eventKey},
                    {"RequestId", requestId},
                    {"EventType", eventType},
                    {"SentAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")},
                    {"SentBy", If(AppState.CurrentUserName, "").Trim()},
                    {"ComputerName", Environment.MachineName},
                    {"Recipients", recipients}
                })
            End Sub)
    End Sub

    Private Shared Function BuildEventKey(requestId As String, eventType As String) As String
        Return requestId.Trim().ToUpperInvariant() & "|" & NormalizeEventType(eventType)
    End Function

    Private Shared Function BuildHtml(row As Dictionary(Of String, String), actionText As String, attachmentCount As Integer) As String
        Dim html As New StringBuilder()
        html.AppendLine("<!DOCTYPE html><html><head><meta charset=""utf-8""></head>")
        html.AppendLine("<body style=""font-family:Segoe UI,Arial,sans-serif;font-size:13px;color:#1f2937;background:#ffffff;"">")
        html.AppendLine("<h2 style=""margin:0 0 8px;color:#1f477e;"">Test / Talep Yönetimi Bildirimi</h2>")
        html.AppendLine("<p style=""margin:0 0 14px;color:#4b5563;"">" & EncodeHtml(actionText) & "</p>")
        html.AppendLine("<table style=""border-collapse:collapse;width:100%;max-width:980px;"">")
        AppendRow(html, "İşlem", actionText)
        AppendRow(html, "Talep No", DataService.GetValue(row, "RequestId"))
        AppendRow(html, "Durum", StatusDisplay(DataService.GetValue(row, "Status")))
        AppendRow(html, "Talep Tarihi", FormatDateTime(DataService.GetValue(row, "CreatedAt")))
        AppendRow(html, "Talep Eden", DataService.GetValue(row, "CreatedBy"))
        AppendRow(html, "Talep Eden Bölüm", FormatDepartmentList(DataService.GetValue(row, "RequestingDepartment")))
        AppendRow(html, "Talep Edilen Bölüm", DataService.GetValue(row, "RequestedDepartment"))
        AppendRow(html, "Ürün Adı / TR No", DataService.GetValue(row, "ProductNameTrCode"))
        AppendRow(html, "Talep Nedeni", DataService.GetValue(row, "RequestReason"), True)
        AppendRow(html, "Talep Edilen Test", DataService.GetValue(row, "RequestedTests"), True)
        AppendRow(html, "Numune / Miktar", DataService.GetValue(row, "SampleQuantity"))
        AppendRow(html, "Öncelik", DataService.GetValue(row, "Priority"))
        AppendRow(html, "Termin", FormatDate(DataService.GetValue(row, "DueDate")))
        AppendRow(html, "Rapor / Referans No", DataService.GetValue(row, "RequesterReportNo"))
        AppendRow(html, "Talep Eden Açıklama", DataService.GetValue(row, "RequesterExplanation"), True)
        AppendRow(html, "İşleme Alan", DataService.GetValue(row, "AcceptedBy"))
        AppendRow(html, "İşleme Alma", FormatDateTime(DataService.GetValue(row, "AcceptedAt")))
        AppendRow(html, "Sonuç", DataService.GetValue(row, "Result"))
        AppendRow(html, "Laboratuvar Rapor No", DataService.GetValue(row, "LabReportNo"))
        AppendRow(html, "Kontrol Eden Açıklama", DataService.GetValue(row, "LabExplanation"), True)
        If attachmentCount > 0 Then AppendRow(html, "Ekli Sonuç Dosyası", attachmentCount.ToString() & " adet")
        AppendRow(html, "Sonuçlandıran", DataService.GetValue(row, "CompletedBy"))
        AppendRow(html, "Sonuçlandırma", FormatDateTime(DataService.GetValue(row, "CompletedAt")))
        AppendRow(html, "İptal Eden", DataService.GetValue(row, "CancelledBy"))
        AppendRow(html, "İptal Nedeni", DataService.GetValue(row, "CancelReason"), True)
        html.AppendLine("</table><p style=""margin-top:16px;"">Bilginize.</p></body></html>")
        Return html.ToString()
    End Function

    Private Shared Sub AppendRow(html As StringBuilder, caption As String, value As String, Optional preserveLines As Boolean = False)
        Dim encodedValue = EncodeHtml(value)
        If preserveLines Then encodedValue = encodedValue.Replace(vbCrLf, "<br>").Replace(vbCr, "<br>").Replace(vbLf, "<br>")
        html.AppendLine("<tr><th style=""border:1px solid #cbd5e1;background:#e7eef8;color:#1f477e;padding:8px;text-align:left;width:190px;"">" & EncodeHtml(caption) & "</th>")
        html.AppendLine("<td style=""border:1px solid #cbd5e1;padding:8px;vertical-align:top;"">" & encodedValue & "</td></tr>")
    End Sub

    Private Shared Function StatusDisplay(status As String) As String
        Select Case If(status, "").Trim().ToUpperInvariant()
            Case "OPEN" : Return "YENİ"
            Case "ACCEPTED" : Return "İŞLEMDE"
            Case "COMPLETED" : Return "TAMAMLANDI"
            Case "CANCELLED" : Return "İPTAL"
            Case Else : Return If(status, "")
        End Select
    End Function

    Private Shared Function FormatDateTime(value As String) As String
        Dim parsed As DateTime
        If DateTime.TryParse(value, parsed) Then Return parsed.ToString("dd.MM.yyyy HH:mm")
        Return If(value, "")
    End Function

    Private Shared Function FormatDate(value As String) As String
        Dim parsed As DateTime
        If DateTime.TryParse(value, parsed) Then Return parsed.ToString("dd.MM.yyyy")
        Return If(value, "")
    End Function

    Private Shared Function EncodeHtml(value As String) As String
        Return WebUtility.HtmlEncode(If(value, ""))
    End Function
End Class
