Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Windows.Forms

Public Class DashboardForm
    Inherits Form

    Private ReadOnly periodStats As List(Of DashboardPeriodStat)

    Public Sub New(total As Integer, approved As Integer, pending As Integer, rejected As Integer, checkCount As Integer, stats As List(Of DashboardPeriodStat))
        Me.Text = "Dashboard"
        Me.StartPosition = FormStartPosition.CenterParent
        Me.Size = New Size(980, 540)
        Me.MinimumSize = New Size(860, 470)
        Me.Font = New Font("Segoe UI", 9.0F)
        Me.BackColor = Color.FromArgb(243, 246, 250)

        AppIconHelper.ApplyIcon(Me)
        periodStats = If(stats, New List(Of DashboardPeriodStat)())

        BuildUi(total, approved, pending, rejected, checkCount)
    End Sub

    Private Sub BuildUi(total As Integer, approved As Integer, pending As Integer, rejected As Integer, checkCount As Integer)
        Dim root As New TableLayoutPanel()
        root.Dock = DockStyle.Fill
        root.ColumnCount = 1
        root.RowCount = 4
        root.Padding = New Padding(14)
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 42))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 150))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 34))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        Me.Controls.Add(root)

        Dim lblTitle As New Label()
        lblTitle.Text = "İNO-1 / İNO-2 Dashboard"
        lblTitle.Dock = DockStyle.Fill
        lblTitle.TextAlign = ContentAlignment.MiddleLeft
        lblTitle.Font = New Font("Segoe UI", 14.0F, FontStyle.Bold)
        lblTitle.ForeColor = Color.FromArgb(31, 78, 121)
        root.Controls.Add(lblTitle, 0, 0)

        Dim cards As New TableLayoutPanel()
        cards.Dock = DockStyle.Fill
        cards.ColumnCount = 5
        cards.RowCount = 1

        For i As Integer = 0 To 4
            cards.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 20))
        Next

        root.Controls.Add(cards, 0, 1)

        AddCard(cards, 0, "TOPLAM KAYIT", total, Color.FromArgb(31, 78, 121))
        AddCard(cards, 1, "TAM ONAYLI", approved, Color.FromArgb(15, 123, 63))
        AddCard(cards, 2, "BEKLEYEN", pending, Color.FromArgb(183, 121, 31))
        AddCard(cards, 3, "RED / UYGUN DEĞİL", rejected, Color.FromArgb(180, 35, 24))
        AddCard(cards, 4, "KONTROL GEREKİR", checkCount, Color.FromArgb(105, 65, 198))

        Dim lblPeriod As New Label()
        lblPeriod.Text = "Günlük / Haftalık / Aylık Özet"
        lblPeriod.Dock = DockStyle.Fill
        lblPeriod.TextAlign = ContentAlignment.MiddleLeft
        lblPeriod.Font = New Font("Segoe UI", 10.5F, FontStyle.Bold)
        lblPeriod.ForeColor = Color.FromArgb(52, 64, 84)
        lblPeriod.Padding = New Padding(2, 8, 0, 0)
        root.Controls.Add(lblPeriod, 0, 2)

        Dim grid As New DataGridView()
        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.AllowUserToResizeRows = False
        grid.RowHeadersVisible = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.MultiSelect = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(233, 238, 245)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(23, 32, 51)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 8.6F, FontStyle.Bold)
        grid.DefaultCellStyle.Font = New Font("Segoe UI", 8.8F)
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(223, 232, 246)
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(23, 32, 51)

        grid.Columns.Add("PeriodName", "Dönem")
        grid.Columns.Add("Total", "Yeni Kayıt")
        grid.Columns.Add("Approved", "Tam Onaylı")
        grid.Columns.Add("Pending", "Bekleyen")
        grid.Columns.Add("Rejected", "Red / Uygun Değil")
        grid.Columns.Add("CheckRequired", "Kontrol Gerekir")
        grid.Columns.Add("Ino1Pending", "İNO-1 Bekleyen")
        grid.Columns.Add("Ino2Pending", "İNO-2 Bekleyen")

        For Each stat In periodStats
            grid.Rows.Add(stat.PeriodName,
                          stat.Total.ToString("N0"),
                          stat.Approved.ToString("N0"),
                          stat.Pending.ToString("N0"),
                          stat.Rejected.ToString("N0"),
                          stat.CheckRequired.ToString("N0"),
                          stat.Ino1Pending.ToString("N0"),
                          stat.Ino2Pending.ToString("N0"))
        Next

        root.Controls.Add(grid, 0, 3)
    End Sub

    Private Sub AddCard(parent As TableLayoutPanel, index As Integer, title As String, value As Integer, color As Color)
        Dim card As New KpiCard(title, color)
        card.Dock = DockStyle.Fill
        card.Margin = New Padding(8)
        card.ValueLabel.Text = value.ToString("N0")
        parent.Controls.Add(card, index, 0)
    End Sub
End Class
