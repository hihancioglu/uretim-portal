Imports System.Data
Imports System.Drawing
Imports System.Globalization
Imports System.Windows.Forms

Public Class FrmDataJournal
    Inherits Form

    Private ReadOnly grid As New DataGridView()
    Private ReadOnly lblSummary As New Label()
    Private ReadOnly btnRefresh As New Button()
    Private ReadOnly btnClose As New Button()

    Public Sub New()
        AuthorizationService.Require(AppState.IsAdmin, "Veri Hareketleri")

        AppIconService.Apply(Me)
        Text = "Kritik Veri Hareketleri"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        MinimumSize = New Size(980, 620)
        Size = New Size(1280, 760)

        BuildScreen()
        LoadJournal()
    End Sub

    Private Sub BuildScreen()
        BackColor = Color.FromArgb(246, 248, 251)

        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .Padding = New Padding(8),
            .Margin = New Padding(0)
        }
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 56.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(root)

        Dim header As New Panel() With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(35, 78, 130),
            .Margin = New Padding(0, 0, 0, 8),
            .Padding = New Padding(22, 8, 22, 8)
        }
        Dim lblTitle As New Label() With {
            .Dock = DockStyle.Fill,
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 14.0F, FontStyle.Bold),
            .Text = "Kritik Veri Hareketleri" & Environment.NewLine &
                    "CSV dosyalarında yapılan ekleme ve güncellemeler satır, boyut ve hash özetiyle izlenir.",
            .TextAlign = ContentAlignment.MiddleLeft
        }
        header.Controls.Add(lblTitle)
        root.Controls.Add(header, 0, 0)

        lblSummary.Dock = DockStyle.Fill
        lblSummary.Margin = New Padding(0, 0, 0, 8)
        lblSummary.Padding = New Padding(14, 0, 14, 0)
        lblSummary.BackColor = Color.FromArgb(232, 240, 252)
        lblSummary.ForeColor = Color.FromArgb(0, 47, 94)
        lblSummary.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblSummary.TextAlign = ContentAlignment.MiddleLeft
        root.Controls.Add(lblSummary, 0, 1)

        Dim actions As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .AutoScroll = False,
            .Padding = New Padding(8, 6, 8, 6),
            .Margin = New Padding(0, 0, 0, 8),
            .BackColor = Color.White
        }

        btnRefresh.Text = "Yenile"
        btnRefresh.Width = 120
        btnRefresh.Height = 30
        btnRefresh.Margin = New Padding(0, 0, 8, 0)
        AddHandler btnRefresh.Click, Sub() LoadJournal()
        actions.Controls.Add(btnRefresh)

        btnClose.Text = "Kapat"
        btnClose.Width = 120
        btnClose.Height = 30
        btnClose.Margin = New Padding(0, 0, 8, 0)
        AddHandler btnClose.Click, Sub() Close()
        actions.Controls.Add(btnClose)

        root.Controls.Add(actions, 0, 2)

        grid.Dock = DockStyle.Fill
        grid.Margin = New Padding(0)
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.ReadOnly = True
        grid.MultiSelect = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        grid.RowHeadersVisible = False
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(219, 232, 247)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(0, 32, 64)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.DefaultCellStyle.Font = New Font("Segoe UI", 9.0F)
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 120, 215)
        grid.DefaultCellStyle.SelectionForeColor = Color.White
        AddHandler grid.CellFormatting, AddressOf Grid_CellFormatting
        root.Controls.Add(grid, 0, 3)
    End Sub

    Private Sub LoadJournal()
        CriticalDataJournalService.EnsureJournalFile()

        Dim table As New DataTable()
        For Each header In CriticalDataJournalService.Headers
            table.Columns.Add(header)
        Next

        Dim rows = CriticalDataJournalService.GetRecentEntries(1000)
        For Each row In rows
            Dim dr = table.NewRow()
            For Each header In CriticalDataJournalService.Headers
                dr(header) = If(row.ContainsKey(header), row(header), "")
            Next
            table.Rows.Add(dr)
        Next

        grid.DataSource = table
        ConfigureColumns()
        lblSummary.Text = "Son " & table.Rows.Count.ToString(CultureInfo.CurrentCulture) &
            " veri hareketi gösteriliyor. Satır azalması veya dosya boyutu düşüşü olan kayıtlar dikkat rengiyle işaretlenir."
    End Sub

    Private Sub ConfigureColumns()
        If grid.Columns.Count = 0 Then Return

        For Each col As DataGridViewColumn In grid.Columns
            col.SortMode = DataGridViewColumnSortMode.Automatic
        Next

        SetColumnWidth("EntryId", 170, DataGridViewAutoSizeColumnMode.None)
        SetColumnWidth("EventAt", 135, DataGridViewAutoSizeColumnMode.None)
        SetColumnWidth("FileName", 180, DataGridViewAutoSizeColumnMode.None)
        SetColumnWidth("Operation", 110, DataGridViewAutoSizeColumnMode.None)
        SetColumnWidth("BeforeRows", 85, DataGridViewAutoSizeColumnMode.None)
        SetColumnWidth("AfterRows", 85, DataGridViewAutoSizeColumnMode.None)
        SetColumnWidth("RowDelta", 85, DataGridViewAutoSizeColumnMode.None)
        SetColumnWidth("BeforeBytes", 95, DataGridViewAutoSizeColumnMode.None)
        SetColumnWidth("AfterBytes", 95, DataGridViewAutoSizeColumnMode.None)
        SetColumnWidth("ByteDelta", 90, DataGridViewAutoSizeColumnMode.None)
        SetColumnWidth("UserName", 110, DataGridViewAutoSizeColumnMode.None)
        SetColumnWidth("Role", 120, DataGridViewAutoSizeColumnMode.None)
        SetColumnWidth("ComputerName", 110, DataGridViewAutoSizeColumnMode.None)
        SetColumnWidth("Note", 180, DataGridViewAutoSizeColumnMode.Fill)

        HideColumn("FilePath")
        HideColumn("BeforeHash")
        HideColumn("AfterHash")
        HideColumn("WindowsUser")
        HideColumn("ProcessId")
        HideColumn("Version")
    End Sub

    Private Sub SetColumnWidth(name As String, width As Integer, mode As DataGridViewAutoSizeColumnMode)
        If Not grid.Columns.Contains(name) Then Return
        grid.Columns(name).AutoSizeMode = mode
        If mode = DataGridViewAutoSizeColumnMode.None Then grid.Columns(name).Width = width
    End Sub

    Private Sub HideColumn(name As String)
        If grid.Columns.Contains(name) Then grid.Columns(name).Visible = False
    End Sub

    Private Sub Grid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse grid.Rows(e.RowIndex).DataBoundItem Is Nothing Then Return

        Dim row = grid.Rows(e.RowIndex)
        Dim rowDelta = ParseIntCell(row, "RowDelta")
        Dim byteDelta = ParseLongCell(row, "ByteDelta")

        If rowDelta < 0 OrElse byteDelta < 0 Then
            e.CellStyle.BackColor = Color.FromArgb(255, 242, 204)
            e.CellStyle.ForeColor = Color.FromArgb(120, 70, 0)
        ElseIf rowDelta > 0 Then
            e.CellStyle.BackColor = Color.FromArgb(226, 246, 232)
            e.CellStyle.ForeColor = Color.FromArgb(0, 95, 45)
        End If
    End Sub

    Private Function ParseIntCell(row As DataGridViewRow, columnName As String) As Integer
        Dim value As Integer
        If grid.Columns.Contains(columnName) AndAlso row.Cells(columnName).Value IsNot Nothing Then
            Integer.TryParse(Convert.ToString(row.Cells(columnName).Value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, value)
        End If
        Return value
    End Function

    Private Function ParseLongCell(row As DataGridViewRow, columnName As String) As Long
        Dim value As Long
        If grid.Columns.Contains(columnName) AndAlso row.Cells(columnName).Value IsNot Nothing Then
            Long.TryParse(Convert.ToString(row.Cells(columnName).Value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, value)
        End If
        Return value
    End Function
End Class
