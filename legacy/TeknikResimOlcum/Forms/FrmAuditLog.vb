Imports System.Data
Imports System.Drawing
Imports System.Globalization
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmAuditLog
    Inherits Form

    Private ReadOnly grid As New DataGridView()

    Public Sub New()
        AuthorizationService.Require(AppState.CanOpenUserAdmin, "Log Kayitlari")
        AppIconService.Apply(Me)
        Text = "Log Kayıtları"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1200, 700)
        MinimumSize = New Size(700, 480)

        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .Padding = New Padding(0),
            .Margin = New Padding(0)
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 52.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(layout)

        Dim top As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(12, 9, 12, 5),
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .AutoScroll = True
        }
        Dim btnRefresh As New Button() With {.Text = "Yenile", .Width = 100, .Height = 30, .Margin = New Padding(0)}
        AddHandler btnRefresh.Click, Sub() LoadGrid()
        top.Controls.Add(btnRefresh)
        layout.Controls.Add(top, 0, 0)

        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells
        layout.Controls.Add(grid, 0, 1)

        LoadGrid()
    End Sub

    Private Sub LoadGrid()
        Dim rows = CsvUtil.ReadRows(AppPaths.AuditLogCsv)
        Dim dt As New DataTable()
        For Each h In DataService.AuditHeaders
            dt.Columns.Add(h)
        Next
        For Each r In rows.
            OrderByDescending(Function(x) AuditDateValue(DataService.GetValue(x, "DateTime"))).
            ThenByDescending(Function(x) AuditIdValue(DataService.GetValue(x, "LogId")))
            Dim dr = dt.NewRow()
            For Each h In DataService.AuditHeaders
                dr(h) = DataService.GetValue(r, h)
            Next
            dt.Rows.Add(dr)
        Next
        grid.DataSource = dt
    End Sub

    Private Shared Function AuditDateValue(value As String) As DateTime
        Dim parsed As DateTime
        If DateTime.TryParseExact(
            If(value, "").Trim(),
            "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            parsed) OrElse DateTime.TryParse(value, parsed) Then
            Return parsed
        End If

        Return DateTime.MinValue
    End Function

    Private Shared Function AuditIdValue(value As String) As Long
        Dim parsed As Long
        If Long.TryParse(If(value, "").Trim(), parsed) Then Return parsed
        Return 0
    End Function
End Class
