Imports System.Linq
Imports System.Net
Imports System.Text

Public NotInheritable Class MoldConnectionPlanOpenTicketNotificationService
    Private Sub New()
    End Sub

    Private NotInheritable Class OpenTicketMatch
        Public Property PlanRowNo As String = ""
        Public Property Machine As String = ""
        Public Property MatchType As String = ""
        Public Property TrCode As String = ""
        Public Property MoldCode As String = ""
        Public Property TicketId As String = ""
        Public Property TicketStatus As String = ""
        Public Property TicketProduct As String = ""
        Public Property ProblemType As String = ""
        Public Property ProblemDescription As String = ""
        Public Property CreatedAt As String = ""
        Public Property CreatedBy As String = ""
    End Class

    Public Shared Function TryNotifyOpenTickets(planRows As IEnumerable(Of Dictionary(Of String, String)),
                                                sourceFileName As String,
                                                ByRef matchCount As Integer,
                                                ByRef recipientCount As Integer,
                                                ByRef mailSent As Boolean,
                                                ByRef errorMessage As String) As Boolean
        matchCount = 0
        recipientCount = 0
        mailSent = False
        errorMessage = ""

        Try
            Dim rows = If(planRows, Enumerable.Empty(Of Dictionary(Of String, String))()).ToList()
            If rows.Count = 0 Then Return True

            Dim matches = FindOpenTicketMatches(rows)
            matchCount = matches.Count
            If matches.Count = 0 Then Return True

            Dim recipients = DataService.GetMoldConnectionPlanEmailRecipients(True).
                Where(Function(item) Not String.IsNullOrWhiteSpace(item.Email)).
                GroupBy(Function(item) item.Email.Trim(), StringComparer.OrdinalIgnoreCase).
                Select(Function(group) group.First()).
                ToList()

            recipientCount = recipients.Count
            If recipients.Count = 0 Then Return True

            Dim toText = String.Join(";", recipients.Select(Function(item) item.Email.Trim()))
            Dim subject = "Bağlanacak Kalıp Listesi - Açık Kalıp Ticket Uyarısı"
            Dim htmlBody = BuildHtml(matches, sourceFileName)

            If OutlookEmailDraftService.TrySendMail(subject, htmlBody, toText) Then
                mailSent = True
                AuditService.Log(
                    "MOLD_CONNECTION_PLAN_OPEN_TICKET_EMAIL_SENT",
                    "",
                    "",
                    "Eşleşme=" & matches.Count.ToString() & "; Alıcı=" & toText & "; Dosya=" & If(sourceFileName, ""))
                Return True
            End If

            errorMessage = "Outlook otomatik e-posta gönderemedi. Outlook'un bu bilgisayarda açık/kurulu ve kullanılabilir olduğunu kontrol edin."
            AuditService.Log(
                "MOLD_CONNECTION_PLAN_OPEN_TICKET_EMAIL_FAILED",
                "",
                "",
                "Eşleşme=" & matches.Count.ToString() & "; Alıcı=" & toText & "; Dosya=" & If(sourceFileName, ""))
            Return False
        Catch ex As Exception
            ErrorLogService.Log("MoldConnectionPlanOpenTicketNotificationService.TryNotifyOpenTickets", ex)
            errorMessage = "Açık kalıp ticket bildirimi sırasında hata oluştu: " & ex.Message
            Return False
        End Try
    End Function

    Private Shared Function FindOpenTicketMatches(planRows As List(Of Dictionary(Of String, String))) As List(Of OpenTicketMatch)
        Dim openTickets = DataService.GetMoldTickets().
            Where(Function(ticket) IsOpenTicketStatus(DataService.GetValue(ticket, "Status"))).
            ToList()

        Dim results As New List(Of OpenTicketMatch)()
        Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        For Each planRow In planRows
            Dim planTrCodes = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            AddNormalized(planTrCodes, DataService.GetValue(planRow, "CurrentTrCode"))
            AddNormalized(planTrCodes, DataService.GetValue(planRow, "FirstTrCode"))
            AddNormalized(planTrCodes, DataService.GetValue(planRow, "SecondTrCode"))

            Dim planMoldCodes = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            AddMoldTokens(planMoldCodes, DataService.GetValue(planRow, "RunningMolds"))
            AddMoldTokens(planMoldCodes, DataService.GetValue(planRow, "CurrentMoldNo"))
            AddMoldTokens(planMoldCodes, DataService.GetValue(planRow, "FirstMoldNo"))
            AddMoldTokens(planMoldCodes, DataService.GetValue(planRow, "SecondMoldNo"))

            If planTrCodes.Count = 0 AndAlso planMoldCodes.Count = 0 Then Continue For

            For Each ticket In openTickets
                Dim ticketTr = NormalizeKey(DataService.GetValue(ticket, "TrCode"))
                Dim ticketMoldCodes = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
                AddMoldTokens(ticketMoldCodes, DataService.GetValue(ticket, "MoldCode"))
                Dim trMatched = ticketTr <> "" AndAlso planTrCodes.Contains(ticketTr)
                Dim moldMatched = ticketMoldCodes.Any(Function(ticketMold) planMoldCodes.Contains(ticketMold))
                If Not trMatched AndAlso Not moldMatched Then Continue For

                Dim ticketId = DataService.GetValue(ticket, "MoldTicketId")
                Dim planId = DataService.GetValue(planRow, "PlanId")
                Dim uniqueKey = ticketId & "|" & planId
                If Not seen.Add(uniqueKey) Then Continue For

                results.Add(New OpenTicketMatch With {
                    .PlanRowNo = DataService.GetValue(planRow, "SourceRow"),
                    .Machine = FirstNonEmpty(DataService.GetValue(planRow, "MachineName"), DataService.GetValue(planRow, "MachineNo")),
                    .MatchType = If(trMatched AndAlso moldMatched, "TR + Kalıp", If(trMatched, "TR", "Kalıp")),
                    .TrCode = FirstNonEmpty(DataService.GetValue(ticket, "TrCode"), FirstNonEmpty(DataService.GetValue(planRow, "FirstTrCode"), DataService.GetValue(planRow, "SecondTrCode"), DataService.GetValue(planRow, "CurrentTrCode"))),
                    .MoldCode = FirstNonEmpty(DataService.GetValue(ticket, "MoldCode"), FirstNonEmpty(DataService.GetValue(planRow, "FirstMoldNo"), DataService.GetValue(planRow, "SecondMoldNo"), DataService.GetValue(planRow, "CurrentMoldNo"))),
                    .TicketId = ticketId,
                    .TicketStatus = DataService.GetValue(ticket, "Status"),
                    .TicketProduct = DataService.GetValue(ticket, "ProductName"),
                    .ProblemType = DataService.GetValue(ticket, "ProblemType"),
                    .ProblemDescription = DataService.GetValue(ticket, "ProblemDescription"),
                    .CreatedAt = FormatDateTime(DataService.GetValue(ticket, "CreatedAt")),
                    .CreatedBy = DataService.GetValue(ticket, "CreatedBy")
                })
            Next
        Next

        Return results.
            OrderBy(Function(item) SafeInt(item.PlanRowNo)).
            ThenBy(Function(item) item.Machine).
            ThenBy(Function(item) item.MoldCode).
            ThenBy(Function(item) item.TrCode).
            ToList()
    End Function

    Private Shared Function IsOpenTicketStatus(status As String) As Boolean
        Dim normalized = NormalizeKey(status)
        If normalized = "" Then Return True
        Return normalized <> "CLOSED" AndAlso
               normalized <> "CLOSE" AndAlso
               normalized <> "KAPALI" AndAlso
               normalized <> "TAMAMLANDI" AndAlso
               normalized <> "PASIF"
    End Function

    Private Shared Sub AddNormalized(target As HashSet(Of String), value As String)
        Dim normalized = NormalizeKey(value)
        If normalized <> "" Then target.Add(normalized)
    End Sub

    Private Shared Sub AddMoldTokens(target As HashSet(Of String), value As String)
        For Each token In SplitTokens(value)
            AddNormalized(target, token)
        Next
    End Sub

    Private Shared Function SplitTokens(value As String) As IEnumerable(Of String)
        Dim raw = If(value, "")
        Dim separators As Char() = {","c, ";"c, "/"c, "\"c, "|"c, vbCr(0), vbLf(0), vbTab(0)}
        Return raw.Split(separators, StringSplitOptions.RemoveEmptyEntries).
            Select(Function(item) item.Trim()).
            Where(Function(item) item <> "")
    End Function

    Private Shared Function NormalizeKey(value As String) As String
        Return If(value, "").Trim().ToUpperInvariant()
    End Function

    Private Shared Function FirstNonEmpty(ParamArray values As String()) As String
        For Each value In values
            If Not String.IsNullOrWhiteSpace(value) Then Return value.Trim()
        Next
        Return ""
    End Function

    Private Shared Function SafeInt(value As String) As Integer
        Dim parsed As Integer
        If Integer.TryParse(If(value, "").Trim(), parsed) Then Return parsed
        Return Integer.MaxValue
    End Function

    Private Shared Function FormatDateTime(value As String) As String
        Dim parsed As DateTime
        If DateTime.TryParse(value, parsed) Then Return parsed.ToString("dd.MM.yyyy HH:mm")
        Return If(value, "")
    End Function

    Private Shared Function BuildHtml(matches As List(Of OpenTicketMatch), sourceFileName As String) As String
        Dim html As New StringBuilder()
        html.AppendLine("<!DOCTYPE html><html><head><meta charset=""utf-8""></head>")
        html.AppendLine("<body style=""font-family:Segoe UI,Arial,sans-serif;font-size:13px;color:#1f2937;background:#ffffff;"">")
        html.AppendLine("<h2 style=""margin:0 0 8px;color:#92400e;"">Bağlanacak Kalıp Listesi - Açık Kalıp Ticket Uyarısı</h2>")
        html.AppendLine("<p style=""margin:0 0 12px;color:#4b5563;"">Excel'den yüklenen bağlanacak kalıp listesinde açık kalıp ticket bulunan TR veya kalıp eşleşmeleri görüldü.</p>")
        html.AppendLine("<p style=""margin:0 0 14px;color:#4b5563;""><b>Dosya:</b> " & EncodeHtml(sourceFileName) & " &nbsp; | &nbsp; <b>Eşleşme:</b> " & matches.Count.ToString() & "</p>")

        html.AppendLine("<table style=""border-collapse:collapse;width:100%;max-width:1200px;"">")
        html.AppendLine("<tr>")
        AppendHeader(html, "Liste Satırı")
        AppendHeader(html, "Makine")
        AppendHeader(html, "Eşleşme")
        AppendHeader(html, "TR")
        AppendHeader(html, "Kalıp")
        AppendHeader(html, "Ticket No")
        AppendHeader(html, "Ürün")
        AppendHeader(html, "Problem")
        AppendHeader(html, "Oluşturan")
        AppendHeader(html, "Tarih")
        html.AppendLine("</tr>")

        For Each item In matches
            html.AppendLine("<tr>")
            AppendCell(html, item.PlanRowNo)
            AppendCell(html, item.Machine)
            AppendCell(html, item.MatchType)
            AppendCell(html, item.TrCode)
            AppendCell(html, item.MoldCode)
            AppendCell(html, item.TicketId)
            AppendCell(html, item.TicketProduct)
            AppendCell(html, FirstNonEmpty(item.ProblemType, item.ProblemDescription), True)
            AppendCell(html, item.CreatedBy)
            AppendCell(html, item.CreatedAt)
            html.AppendLine("</tr>")
        Next

        html.AppendLine("</table>")
        html.AppendLine("<p style=""margin-top:16px;color:#4b5563;"">Bilginize.</p>")
        html.AppendLine("</body></html>")
        Return html.ToString()
    End Function

    Private Shared Sub AppendHeader(html As StringBuilder, caption As String)
        html.AppendLine("<th style=""border:1px solid #cbd5e1;background:#fde68a;color:#78350f;padding:8px;text-align:left;"">" & EncodeHtml(caption) & "</th>")
    End Sub

    Private Shared Sub AppendCell(html As StringBuilder, value As String, Optional preserveLines As Boolean = False)
        Dim encoded = EncodeHtml(value)
        If preserveLines Then encoded = encoded.Replace(vbCrLf, "<br>").Replace(vbCr, "<br>").Replace(vbLf, "<br>")
        html.AppendLine("<td style=""border:1px solid #cbd5e1;padding:8px;vertical-align:top;"">" & encoded & "</td>")
    End Sub

    Private Shared Function EncodeHtml(value As String) As String
        Return WebUtility.HtmlEncode(If(value, ""))
    End Function
End Class
