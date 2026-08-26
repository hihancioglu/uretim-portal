Imports System.Data
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmQualityToProductionTickets
    Inherits Form

    Private ReadOnly grid As New DataGridView()
    Private ReadOnly txtFilter As New TextBox()
    Private ReadOnly cboStatus As New ComboBox()
    Private ReadOnly lblCount As New Label()
    Private ReadOnly groupRowFont As New Font("Segoe UI", 9.25F, FontStyle.Bold)
    Private ReadOnly expandedTrGroupKeys As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Private visibleTickets As New List(Of Dictionary(Of String, String))()
    Private totalTicketCount As Integer
    Private openTicketCount As Integer
    Private seenTicketCount As Integer
    Private closedTicketCount As Integer

    Public Sub New()
        AuthorizationService.Require(AppState.CanOpenQualityToProductionTickets, "Uretim Ticketlari")
        AppIconService.Apply(Me)
        Text = "Üretim Ticketları - Kalite Uygunsuzlukları"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1450, 780)
        MinimumSize = New Size(760, 520)
        BackColor = Color.White

        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .BackColor = Color.White
        }
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 76.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(layout)

        Dim top As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(10, 10, 10, 4),
            .BackColor = SystemColors.Control,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .AutoScroll = True
        }
        layout.Controls.Add(top, 0, 0)

        top.Controls.Add(New Label() With {.Text = "Arama", .Width = 50, .Height = 26, .TextAlign = ContentAlignment.MiddleLeft})
        txtFilter.Width = 280
        txtFilter.Height = 26
        txtFilter.PlaceholderText = "TR / ürün / iş emri / seri / kayıt / uygunsuzluk"
        AddHandler txtFilter.TextChanged, Sub() LoadGrid()
        top.Controls.Add(txtFilter)

        top.Controls.Add(New Label() With {.Text = "Durum", .Width = 55, .Height = 26, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(14, 0, 0, 0)})
        cboStatus.Width = 110
        cboStatus.Height = 26
        cboStatus.DropDownStyle = ComboBoxStyle.DropDownList
        cboStatus.Items.AddRange({"AKTİF", "AÇIK", "GÖRÜLDÜ", "KAPALI", "TÜMÜ"})
        cboStatus.SelectedIndex = 0
        AddHandler cboStatus.SelectedIndexChanged, Sub() LoadGrid()
        top.Controls.Add(cboStatus)

        Dim btnRefresh As New Button() With {.Text = "Yenile", .Width = 90, .Height = 30, .Margin = New Padding(16, 0, 0, 0)}
        AddHandler btnRefresh.Click, Sub() LoadGrid()

        Dim btnSeen As New Button() With {.Text = "Görüldü Yap", .Width = 110, .Height = 30}
        AddHandler btnSeen.Click, AddressOf Seen_Click

        Dim btnOpenRecord As New Button() With {.Text = "Ölçüm Kaydını Aç", .Width = 135, .Height = 30}
        AddHandler btnOpenRecord.Click, AddressOf OpenRecord_Click

        Dim btnClose As New Button() With {.Text = "Ticketı Kapat", .Width = 115, .Height = 30}
        AddHandler btnClose.Click, AddressOf Close_Click

        top.Controls.Add(btnRefresh)

        If AppState.CanModifyQualityToProductionTickets Then
            top.Controls.Add(btnSeen)
        End If

        top.Controls.Add(btnOpenRecord)

        If AppState.CanModifyQualityToProductionTickets Then
            top.Controls.Add(btnClose)
        End If

        If Not AppState.CanModifyQualityToProductionTickets Then
            top.Controls.Add(New Label() With {
                .Text = "Sadece görüntüleme",
                .Width = 135,
                .Height = 30,
                .TextAlign = ContentAlignment.MiddleLeft,
                .ForeColor = Color.DimGray
            })
        End If

        Dim summary As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(10, 5, 10, 4), .BackColor = Color.WhiteSmoke}
        layout.Controls.Add(summary, 0, 1)

        lblCount.Text = "Ticket: 0"
        lblCount.Dock = DockStyle.Fill
        lblCount.Margin = New Padding(2)
        lblCount.TextAlign = ContentAlignment.MiddleLeft
        lblCount.AutoEllipsis = True
        lblCount.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        summary.Controls.Add(lblCount)

        ConfigureGrid()
        layout.Controls.Add(grid, 0, 2)

        LoadGrid()
    End Sub

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.MultiSelect = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.AutoGenerateColumns = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
        grid.RowHeadersVisible = False
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.GridColor = Color.Gainsboro
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 235, 235)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.DefaultCellStyle.BackColor = Color.White
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248)
        grid.RowTemplate.Height = 28
        grid.ScrollBars = ScrollBars.Both

        grid.Columns.Clear()
        grid.Columns.Add(MakeColumn("_Toggle", "", 58))
        grid.Columns.Add(MakeColumn("_RowType", "", 60))
        grid.Columns.Add(MakeColumn("_GroupKey", "", 60))
        grid.Columns.Add(MakeColumn("TicketId", "Ticket No", 170))
        grid.Columns.Add(MakeColumn("Status", "Durum", 80))
        grid.Columns.Add(MakeColumn("CreatedAt", "Tarih", 135))
        grid.Columns.Add(MakeColumn("CreatedBy", "Açan Kalite", 120))
        grid.Columns.Add(MakeColumn("TrCode", "TR Kodu", 100))
        grid.Columns.Add(MakeColumn("DrawingRev", "Rev.", 65))
        grid.Columns.Add(MakeColumn("ProductName", "Ürün Adı", 150))
        grid.Columns.Add(MakeColumn("LotNo", "İş Emri No", 110))
        grid.Columns.Add(MakeColumn("SerialNo", "Seri No", 90))
        grid.Columns.Add(MakeColumn("EyeNo", "Göz", 55))
        grid.Columns.Add(MakeColumn("RecordId", "Ölçüm Kayıt No", 170))
        grid.Columns.Add(MakeColumn("MeasurementNokCount", "Ölçüm NOK", 90))
        grid.Columns.Add(MakeColumn("VisualNokCount", "Görsel NOK", 90))
        grid.Columns.Add(MakeColumn("IssueSummary", "Uygunsuzluk Özeti", 420))
        grid.Columns.Add(MakeColumn("SeenByProduction", "Gören Üretim", 120))
        grid.Columns.Add(MakeColumn("SeenAt", "Görülme Tarihi", 135))
        grid.Columns.Add(MakeColumn("ClosedBy", "Kapatan", 110))
        grid.Columns.Add(MakeColumn("ClosedAt", "Kapanış Tarihi", 135))
        grid.Columns.Add(MakeColumn("CloseNote", "Kapanış Notu", 220))

        grid.Columns("_Toggle").Frozen = True
        grid.Columns("_Toggle").MinimumWidth = 58
        grid.Columns("_Toggle").DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
        grid.Columns("_RowType").Visible = False
        grid.Columns("_GroupKey").Visible = False

        AddHandler grid.CellFormatting, AddressOf Grid_CellFormatting
        AddHandler grid.CellClick, AddressOf Grid_CellClick
        AddHandler grid.CellDoubleClick, AddressOf Grid_DoubleClick
        AddHandler grid.KeyDown, AddressOf Grid_KeyDown
    End Sub

    Private Function MakeColumn(name As String, header As String, width As Integer) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .DataPropertyName = name,
            .HeaderText = header,
            .Width = width,
            .MinimumWidth = 55,
            .SortMode = DataGridViewColumnSortMode.NotSortable
        }
    End Function

    Private Sub LoadGrid()
        Dim allRows = DataService.GetQualityToProductionTickets()
        Dim rows = allRows.AsEnumerable()

        Dim statusText = If(cboStatus.SelectedItem Is Nothing, "AKTİF", cboStatus.SelectedItem.ToString())
        If statusText = "AKTİF" Then
            rows = rows.Where(Function(r)
                                  Dim st = DataService.GetValue(r, "Status")
                                  Return String.Equals(st, "OPEN", StringComparison.OrdinalIgnoreCase) OrElse
                                         String.Equals(st, "SEEN", StringComparison.OrdinalIgnoreCase)
                              End Function)
        ElseIf statusText = "AÇIK" Then
            rows = rows.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "OPEN", StringComparison.OrdinalIgnoreCase))
        ElseIf statusText = "GÖRÜLDÜ" Then
            rows = rows.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "SEEN", StringComparison.OrdinalIgnoreCase))
        ElseIf statusText = "KAPALI" Then
            rows = rows.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "CLOSED", StringComparison.OrdinalIgnoreCase))
        End If

        Dim filterText = txtFilter.Text.Trim()
        If filterText <> "" Then
            Dim tokens = filterText.Split(New Char() {" "c, ";"c, ","c, "|"c}, StringSplitOptions.RemoveEmptyEntries)
            rows = rows.Where(Function(r)
                                  Dim haystack = (DataService.GetValue(r, "TicketId") & " " &
                                                  DataService.GetValue(r, "TrCode") & " " &
                                                  DataService.GetValue(r, "DrawingRev") & " " &
                                                  DataService.GetValue(r, "ProductName") & " " &
                                                  DataService.GetValue(r, "LotNo") & " " &
                                                  DataService.GetValue(r, "SerialNo") & " " &
                                                  DataService.GetValue(r, "RecordId") & " " &
                                                  DataService.GetValue(r, "IssueSummary")).ToUpperInvariant()
                                  For Each token In tokens
                                      If Not haystack.Contains(token.ToUpperInvariant()) Then Return False
                                  Next
                                  Return True
                              End Function)
        End If

        visibleTickets = rows.
            OrderByDescending(Function(r) DataService.GetValue(r, "CreatedAt")).
            ToList()
        Dim visibleGroupKeys = New HashSet(Of String)(
            visibleTickets.Select(Function(row) TicketTrGroupKey(row)),
            StringComparer.OrdinalIgnoreCase)
        expandedTrGroupKeys.RemoveWhere(Function(key) Not visibleGroupKeys.Contains(key))

        BindGroupedGrid()

        Dim openCount = allRows.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "OPEN", StringComparison.OrdinalIgnoreCase)).Count()
        Dim seenCount = allRows.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "SEEN", StringComparison.OrdinalIgnoreCase)).Count()
        Dim activeCount = openCount + seenCount
        Dim closedCount = allRows.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "CLOSED", StringComparison.OrdinalIgnoreCase)).Count()
        totalTicketCount = allRows.Count
        openTicketCount = openCount
        seenTicketCount = seenCount
        closedTicketCount = closedCount
        lblCount.Text = $"TR grubu: {visibleGroupKeys.Count}   |   Ticket: {visibleTickets.Count} / {allRows.Count}   |   Aktif: {activeCount}   Açık: {openCount}   Görüldü: {seenCount}   Kapalı: {closedCount}   |   Açık grup: {expandedTrGroupKeys.Count}"
    End Sub

    Private Sub BindGroupedGrid(Optional selectedGroupKey As String = "", Optional ticketIdToSelect As String = "")
        Dim firstDisplayedIndex = If(grid.FirstDisplayedScrollingRowIndex >= 0, grid.FirstDisplayedScrollingRowIndex, 0)
        If ticketIdToSelect = "" Then ticketIdToSelect = SelectedTicketId()

        Dim dt = CreateTicketDisplayTable()
        Dim trGroups = visibleTickets.
            GroupBy(Function(row) TicketTrGroupKey(row), StringComparer.OrdinalIgnoreCase).
            OrderByDescending(Function(group) group.Max(Function(row) DataService.GetValue(row, "CreatedAt"))).
            ToList()

        For Each trGroup In trGroups
            Dim groupKey = trGroup.Key
            Dim groupTickets = trGroup.
                OrderByDescending(Function(row) DataService.GetValue(row, "CreatedAt")).
                ToList()
            AddTrGroupRow(dt, groupKey, groupTickets)

            If expandedTrGroupKeys.Contains(groupKey) Then
                For Each ticket In groupTickets
                    AddTicketDetailRow(dt, groupKey, ticket)
                Next
            End If
        Next

        grid.DataSource = dt

        If ticketIdToSelect <> "" Then
            For Each row As DataGridViewRow In grid.Rows
                If String.Equals(Convert.ToString(row.Cells("TicketId").Value), ticketIdToSelect, StringComparison.OrdinalIgnoreCase) AndAlso
                   String.Equals(GridRowType(row), "DETAIL", StringComparison.OrdinalIgnoreCase) Then
                    grid.CurrentCell = row.Cells("TicketId")
                    row.Selected = True
                    Exit For
                End If
            Next
        ElseIf selectedGroupKey <> "" Then
            For Each row As DataGridViewRow In grid.Rows
                If String.Equals(Convert.ToString(row.Cells("_GroupKey").Value), selectedGroupKey, StringComparison.OrdinalIgnoreCase) AndAlso
                   String.Equals(GridRowType(row), "TR_GROUP", StringComparison.OrdinalIgnoreCase) Then
                    grid.CurrentCell = row.Cells("_Toggle")
                    row.Selected = True
                    Exit For
                End If
            Next
        End If

        If firstDisplayedIndex >= 0 AndAlso firstDisplayedIndex < grid.Rows.Count Then
            Try
                grid.FirstDisplayedScrollingRowIndex = firstDisplayedIndex
            Catch
            End Try
        End If
    End Sub

    Private Shared Function CreateTicketDisplayTable() As DataTable
        Dim dt As New DataTable()
        dt.Columns.Add("_Toggle")
        dt.Columns.Add("_RowType")
        dt.Columns.Add("_GroupKey")
        For Each header In DataService.QualityToProductionTicketHeaders
            dt.Columns.Add(header)
        Next
        Return dt
    End Function

    Private Sub AddTrGroupRow(dt As DataTable, groupKey As String, tickets As List(Of Dictionary(Of String, String)))
        If tickets Is Nothing OrElse tickets.Count = 0 Then Return

        Dim firstTicket = tickets.First()
        Dim dr = dt.NewRow()
        FillTicketDataRow(dr, firstTicket)
        dr("_Toggle") = If(expandedTrGroupKeys.Contains(groupKey), "▼", "▶")
        dr("_RowType") = "TR_GROUP"
        dr("_GroupKey") = groupKey
        dr("TicketId") = tickets.Count.ToString() & " ticket"
        dr("Status") = OverallTicketStatus(tickets)
        dr("CreatedAt") = tickets.Max(Function(row) DataService.GetValue(row, "CreatedAt"))
        dr("CreatedBy") = CommonTicketValue(tickets, "CreatedBy")
        dr("TrCode") = DisplayTicketTrCode(firstTicket)
        dr("DrawingRev") = CommonTicketValue(tickets, "DrawingRev")
        dr("ProductName") = CommonTicketValue(tickets, "ProductName")
        dr("LotNo") = CommonTicketValue(tickets, "LotNo")
        dr("SerialNo") = CommonTicketValue(tickets, "SerialNo")
        dr("EyeCount") = CommonTicketValue(tickets, "EyeCount")
        dr("EyeNo") = BuildTicketEyeSummary(tickets)
        dr("RecordId") = tickets.Select(Function(row) DataService.GetValue(row, "RecordId")).Where(Function(value) value.Trim() <> "").Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString() & " ölçüm kaydı"
        dr("MeasurementNokCount") = SumTicketNumber(tickets, "MeasurementNokCount").ToString()
        dr("VisualNokCount") = SumTicketNumber(tickets, "VisualNokCount").ToString()
        dr("IssueSummary") = BuildTicketStatusSummary(tickets)
        dr("SourceQualityTicketId") = ""
        dr("SourceType") = ""
        dr("SeenByProduction") = CommonTicketValue(tickets, "SeenByProduction")
        dr("SeenAt") = ""
        dr("ClosedBy") = CommonTicketValue(tickets, "ClosedBy")
        dr("ClosedAt") = ""
        dr("CloseNote") = ""
        dt.Rows.Add(dr)
    End Sub

    Private Shared Sub AddTicketDetailRow(dt As DataTable, groupKey As String, ticket As Dictionary(Of String, String))
        Dim dr = dt.NewRow()
        FillTicketDataRow(dr, ticket)
        dr("_Toggle") = "  •"
        dr("_RowType") = "DETAIL"
        dr("_GroupKey") = groupKey
        dt.Rows.Add(dr)
    End Sub

    Private Shared Sub FillTicketDataRow(dr As DataRow, ticket As Dictionary(Of String, String))
        For Each header In DataService.QualityToProductionTicketHeaders
            dr(header) = DataService.GetValue(ticket, header)
        Next
    End Sub

    Private Shared Function TicketTrGroupKey(ticket As Dictionary(Of String, String)) As String
        Dim trCode = DataService.GetValue(ticket, "TrCode").Trim()
        Return If(trCode = "", "(TR KODU YOK)", trCode.ToUpperInvariant())
    End Function

    Private Shared Function DisplayTicketTrCode(ticket As Dictionary(Of String, String)) As String
        Dim value = DataService.GetValue(ticket, "TrCode").Trim()
        Return If(value = "", "TR KODU YOK", value)
    End Function

    Private Shared Function CommonTicketValue(tickets As IEnumerable(Of Dictionary(Of String, String)), columnName As String) As String
        Dim values = tickets.
            Select(Function(row) DataService.GetValue(row, columnName).Trim()).
            Where(Function(value) value <> "").
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToList()
        If values.Count = 1 Then Return values(0)
        If values.Count > 1 Then Return "ÇEŞİTLİ"
        Return ""
    End Function

    Private Shared Function OverallTicketStatus(tickets As IEnumerable(Of Dictionary(Of String, String))) As String
        Dim statuses = tickets.Select(Function(row) DataService.GetValue(row, "Status").Trim().ToUpperInvariant()).Distinct().ToList()
        If statuses.Count = 1 Then Return statuses(0)
        If statuses.Any(Function(status) status = "OPEN" OrElse status = "SEEN") Then Return "AKTİF"
        Return "KARMA"
    End Function

    Private Shared Function BuildTicketStatusSummary(tickets As IEnumerable(Of Dictionary(Of String, String))) As String
        Dim rows = tickets.ToList()
        Dim openCount = rows.Where(Function(row) String.Equals(DataService.GetValue(row, "Status"), "OPEN", StringComparison.OrdinalIgnoreCase)).Count()
        Dim seenCount = rows.Where(Function(row) String.Equals(DataService.GetValue(row, "Status"), "SEEN", StringComparison.OrdinalIgnoreCase)).Count()
        Dim closedCount = rows.Where(Function(row) String.Equals(DataService.GetValue(row, "Status"), "CLOSED", StringComparison.OrdinalIgnoreCase)).Count()
        Return "Açık: " & openCount.ToString() & " | Görüldü: " & seenCount.ToString() & " | Kapalı: " & closedCount.ToString()
    End Function

    Private Shared Function BuildTicketEyeSummary(tickets As IEnumerable(Of Dictionary(Of String, String))) As String
        Return String.Join(", ", tickets.
            Select(Function(row) DataService.GetValue(row, "EyeNo").Trim()).
            Where(Function(value) value <> "").
            Distinct(StringComparer.OrdinalIgnoreCase).
            OrderBy(Function(value) value, StringComparer.OrdinalIgnoreCase))
    End Function

    Private Shared Function SumTicketNumber(tickets As IEnumerable(Of Dictionary(Of String, String)), columnName As String) As Integer
        Dim total As Integer = 0
        For Each ticket In tickets
            Dim value As Integer
            If Integer.TryParse(DataService.GetValue(ticket, columnName), value) Then total += value
        Next
        Return total
    End Function

    Private Function SelectedTicketId() As String
        If grid.CurrentRow Is Nothing OrElse Not grid.Columns.Contains("TicketId") Then Return ""
        If Not String.Equals(GridRowType(grid.CurrentRow), "DETAIL", StringComparison.OrdinalIgnoreCase) Then Return ""
        Return Convert.ToString(grid.CurrentRow.Cells("TicketId").Value)
    End Function

    Private Function SelectedCellText(columnName As String) As String
        If grid.CurrentRow Is Nothing OrElse Not grid.Columns.Contains(columnName) Then Return ""
        If Not String.Equals(GridRowType(grid.CurrentRow), "DETAIL", StringComparison.OrdinalIgnoreCase) Then Return ""
        Return Convert.ToString(grid.CurrentRow.Cells(columnName).Value)
    End Function

    Private Function GridRowType(row As DataGridViewRow) As String
        If row Is Nothing OrElse Not grid.Columns.Contains("_RowType") Then Return ""
        Return Convert.ToString(row.Cells("_RowType").Value).Trim()
    End Function

    Private Function IsTrGroupRow(row As DataGridViewRow) As Boolean
        Return String.Equals(GridRowType(row), "TR_GROUP", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Sub Grid_CellClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 OrElse e.ColumnIndex < 0 Then Return
        If e.ColumnIndex = grid.Columns("_Toggle").Index AndAlso IsTrGroupRow(grid.Rows(e.RowIndex)) Then
            ToggleTrGroupAtRow(e.RowIndex)
        End If
    End Sub

    Private Sub Grid_KeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode <> Keys.Enter AndAlso e.KeyCode <> Keys.Space Then Return
        If grid.CurrentRow Is Nothing OrElse Not IsTrGroupRow(grid.CurrentRow) Then Return
        ToggleTrGroupAtRow(grid.CurrentRow.Index)
        e.Handled = True
        e.SuppressKeyPress = True
    End Sub

    Private Sub ToggleTrGroupAtRow(rowIndex As Integer)
        If rowIndex < 0 OrElse rowIndex >= grid.Rows.Count Then Return
        Dim row = grid.Rows(rowIndex)
        If Not IsTrGroupRow(row) Then Return

        Dim groupKey = Convert.ToString(row.Cells("_GroupKey").Value).Trim()
        If groupKey = "" Then Return
        If expandedTrGroupKeys.Contains(groupKey) Then
            expandedTrGroupKeys.Remove(groupKey)
        Else
            expandedTrGroupKeys.Add(groupKey)
        End If
        BindGroupedGrid(groupKey)
        LoadCountSummaryOnly()
    End Sub

    Private Sub LoadCountSummaryOnly()
        Dim groupCount = visibleTickets.Select(Function(row) TicketTrGroupKey(row)).Distinct(StringComparer.OrdinalIgnoreCase).Count()
        lblCount.Text = $"TR grubu: {groupCount}   |   Ticket: {visibleTickets.Count} / {totalTicketCount}   |   Aktif: {openTicketCount + seenTicketCount}   Açık: {openTicketCount}   Görüldü: {seenTicketCount}   Kapalı: {closedTicketCount}   |   Açık grup: {expandedTrGroupKeys.Count}"
    End Sub

    Private Sub OpenRecord_Click(sender As Object, e As EventArgs)
        Try
            Dim ticketId = SelectedTicketId()
            If ticketId = "" Then
                MessageBox.Show("Önce açılmış bir grubun içinden ticket satırı seçiniz.", "Ticket seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim statusText = SelectedCellText("Status")
            If AppState.CanModifyQualityToProductionTickets AndAlso String.Equals(statusText, "OPEN", StringComparison.OrdinalIgnoreCase) Then
                DataService.MarkQualityToProductionTicketSeen(ticketId, AppState.CurrentUserName)
                AuditService.Log("QUALITY_TO_PRODUCTION_TICKET_SEEN", SelectedCellText("TrCode"), SelectedCellText("DrawingRev"), "TicketId=" & ticketId)
            End If

            Dim recordId = SelectedCellText("RecordId")
            If recordId.Trim() = "" Then
                MessageBox.Show("Seçili ticket içinde ölçüm kayıt no bulunamadı.", "Kayıt bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                LoadGrid()
                Return
            End If

            Using f As New FrmMeasurementReview(recordId, "")
                f.ShowDialog(Me)
            End Using

            LoadGrid()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Ölçüm kaydı açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Seen_Click(sender As Object, e As EventArgs)
        Try
            If Not AppState.CanModifyQualityToProductionTickets Then
                MessageBox.Show("Bu rol için üretim ticketına müdahale yetkisi yoktur. Sadece görüntüleme yapılabilir.",
                                "Yetki yok", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim ticketId = SelectedTicketId()
            If ticketId = "" Then
                MessageBox.Show("Önce açılmış bir grubun içinden ticket satırı seçiniz.", "Ticket seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If
            DataService.MarkQualityToProductionTicketSeen(ticketId, AppState.CurrentUserName)
            AuditService.Log("QUALITY_TO_PRODUCTION_TICKET_SEEN", SelectedCellText("TrCode"), SelectedCellText("DrawingRev"), "TicketId=" & ticketId)
            LoadGrid()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Ticket güncellenemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Close_Click(sender As Object, e As EventArgs)
        Try
            If Not AppState.CanModifyQualityToProductionTickets Then
                MessageBox.Show("Bu rol için üretim ticketına müdahale yetkisi yoktur. Sadece görüntüleme yapılabilir.",
                                "Yetki yok", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim ticketId = SelectedTicketId()
            If ticketId = "" Then
                MessageBox.Show("Önce açılmış bir grubun içinden ticket satırı seçiniz.", "Ticket seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim note = InputBox("Kapanış notu giriniz:", "Ticketı Kapat", "Üretim tarafından aksiyon alındı.")
            If note Is Nothing Then note = ""

            DataService.CloseQualityToProductionTicket(ticketId, AppState.CurrentUserName, note)
            AuditService.Log("QUALITY_TO_PRODUCTION_TICKET_CLOSE", SelectedCellText("TrCode"), SelectedCellText("DrawingRev"), "TicketId=" & ticketId & "; Note=" & note)
            LoadGrid()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Ticket kapatılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Grid_DoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return
        If IsTrGroupRow(grid.Rows(e.RowIndex)) Then
            ToggleTrGroupAtRow(e.RowIndex)
            Return
        End If
        OpenRecord_Click(sender, EventArgs.Empty)
    End Sub

    Private Sub Grid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse Not grid.Columns.Contains("Status") Then Return

        Dim dataRow = grid.Rows(e.RowIndex)
        dataRow.DefaultCellStyle.Font = grid.DefaultCellStyle.Font
        dataRow.DefaultCellStyle.BackColor = If(e.RowIndex Mod 2 = 0, Color.White, Color.FromArgb(248, 248, 248))
        dataRow.DefaultCellStyle.ForeColor = Color.Black

        If IsTrGroupRow(dataRow) Then
            dataRow.DefaultCellStyle.Font = groupRowFont
            dataRow.DefaultCellStyle.BackColor = Color.FromArgb(226, 236, 249)
            dataRow.DefaultCellStyle.ForeColor = Color.FromArgb(25, 58, 100)
            dataRow.Height = 34
            Return
        End If

        dataRow.Height = 28
        Dim st = Convert.ToString(dataRow.Cells("Status").Value)
        If String.Equals(st, "OPEN", StringComparison.OrdinalIgnoreCase) Then
            dataRow.DefaultCellStyle.BackColor = Color.MistyRose
            dataRow.DefaultCellStyle.ForeColor = Color.DarkRed
        ElseIf String.Equals(st, "SEEN", StringComparison.OrdinalIgnoreCase) Then
            dataRow.DefaultCellStyle.BackColor = Color.LemonChiffon
            dataRow.DefaultCellStyle.ForeColor = Color.FromArgb(90, 70, 0)
        ElseIf String.Equals(st, "CLOSED", StringComparison.OrdinalIgnoreCase) Then
            dataRow.DefaultCellStyle.BackColor = Color.Honeydew
            dataRow.DefaultCellStyle.ForeColor = Color.DarkGreen
        End If
    End Sub
End Class
