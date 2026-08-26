Imports System.Data
Imports System.Drawing
Imports System.Windows.Forms

Public Class FrmRunningInstances
    Inherits Form

    Private ReadOnly grid As New DataGridView()
    Private ReadOnly lblInfo As New Label()
    Private ReadOnly refreshTimer As New Timer()
    Private ReadOnly activeRowFont As New Font("Segoe UI", 9.0F, FontStyle.Bold)

    Public Sub New()
        AuthorizationService.Require(AppState.IsAdmin, "Program Açık Bilgisayarlar")
        AppIconService.Apply(Me)
        Text = "Program Açık Bilgisayarlar - Olası Dosya Kilitleri"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(1250, 620)
        MinimumSize = New Size(820, 450)
        BackColor = Color.White

        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .BackColor = Color.White
        }
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 76.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        Controls.Add(layout)

        Dim header As New Panel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(12, 8, 12, 6),
            .BackColor = Color.WhiteSmoke
        }
        lblInfo.Dock = DockStyle.Fill
        lblInfo.Font = New Font("Segoe UI", 9.5F, FontStyle.Bold)
        lblInfo.TextAlign = ContentAlignment.MiddleLeft
        lblInfo.AutoEllipsis = True
        header.Controls.Add(lblInfo)
        layout.Controls.Add(header, 0, 0)

        ConfigureGrid()
        layout.Controls.Add(grid, 0, 1)

        Dim buttons As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False,
            .Padding = New Padding(10),
            .BackColor = Color.WhiteSmoke
        }
        Dim btnClose As New Button() With {.Text = "Kapat", .Width = 100, .Height = 32}
        AddHandler btnClose.Click, Sub() Close()
        Dim btnRefresh As New Button() With {.Text = "Yenile", .Width = 100, .Height = 32}
        AddHandler btnRefresh.Click, Sub() LoadInstances()
        Dim btnCopy As New Button() With {.Text = "Seçili Bilgiyi Kopyala", .Width = 175, .Height = 32}
        AddHandler btnCopy.Click, AddressOf CopySelected_Click
        buttons.Controls.AddRange({btnClose, btnRefresh, btnCopy})
        layout.Controls.Add(buttons, 0, 2)

        refreshTimer.Interval = 10000
        AddHandler refreshTimer.Tick, Sub() LoadInstances()
        AddHandler Shown,
            Sub()
                LoadInstances()
                refreshTimer.Start()
            End Sub
        AddHandler FormClosed,
            Sub()
                refreshTimer.Stop()
                refreshTimer.Dispose()
                activeRowFont.Dispose()
            End Sub
    End Sub

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.MultiSelect = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.RowHeadersVisible = False
        grid.AutoGenerateColumns = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
        grid.ScrollBars = ScrollBars.Both
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.GridColor = Color.Gainsboro
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 235, 235)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
        grid.RowTemplate.Height = 28

        grid.Columns.Add(MakeColumn("StatusText", "Durum", 140))
        grid.Columns.Add(MakeColumn("ComputerName", "Bilgisayar", 145))
        grid.Columns.Add(MakeColumn("WindowsUser", "Windows Kullanıcısı", 145))
        grid.Columns.Add(MakeColumn("AppUser", "Program Kullanıcısı", 140))
        grid.Columns.Add(MakeColumn("Role", "Rol", 185))
        grid.Columns.Add(MakeColumn("ProcessId", "PID", 70))
        grid.Columns.Add(MakeColumn("Version", "Sürüm", 135))
        grid.Columns.Add(MakeColumn("StartedAt", "Program Açılışı", 145))
        grid.Columns.Add(MakeColumn("LastSeen", "Son Görülme", 145))
        grid.Columns.Add(MakeColumn("AgeSeconds", "Geçen sn", 80))
        grid.Columns.Add(MakeColumn("ExecutablePath", "Çalıştırılan EXE Yolu", 430))
    End Sub

    Private Shared Function MakeColumn(name As String, header As String, width As Integer) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .DataPropertyName = name,
            .HeaderText = header,
            .Width = width,
            .MinimumWidth = 55,
            .SortMode = DataGridViewColumnSortMode.Automatic
        }
    End Function

    Private Sub LoadInstances()
        Try
            Dim instances = ApplicationInstanceService.GetInstancesForAdmin()
            Dim table As New DataTable()
            For Each columnName In {"StatusText", "ComputerName", "WindowsUser", "AppUser", "Role", "ProcessId", "Version", "StartedAt", "LastSeen", "AgeSeconds", "ExecutablePath", "InstanceId"}
                table.Columns.Add(columnName)
            Next

            For Each instance In instances
                Dim row = table.NewRow()
                For Each column As DataColumn In table.Columns
                    row(column.ColumnName) = DataService.GetValue(instance, column.ColumnName)
                Next
                table.Rows.Add(row)
            Next

            grid.DataSource = table
            Dim activeCount = instances.Where(
                Function(row) String.Equals(
                    DataService.GetValue(row, "StatusText"),
                    "PROGRAM AÇIK",
                    StringComparison.OrdinalIgnoreCase)).Count()
            lblInfo.Text =
                $"Program kaydı: {table.Rows.Count} | Aktif görünen: {activeCount}" & Environment.NewLine &
                "PROGRAM AÇIK satırları ortak EXE/DLL dosyalarını kilitleme ihtimali en yüksek bilgisayarlardır. Liste 5 saniyede bir yenilenir."

            For Each row As DataGridViewRow In grid.Rows
                Dim status = Convert.ToString(row.Cells("StatusText").Value)
                If status = "PROGRAM AÇIK" Then
                    row.DefaultCellStyle.BackColor = Color.MistyRose
                    row.DefaultCellStyle.ForeColor = Color.DarkRed
                    row.DefaultCellStyle.Font = activeRowFont
                ElseIf status = "YANIT GECİKMİŞ" Then
                    row.DefaultCellStyle.BackColor = Color.LemonChiffon
                    row.DefaultCellStyle.ForeColor = Color.DarkGoldenrod
                Else
                    row.DefaultCellStyle.BackColor = Color.WhiteSmoke
                    row.DefaultCellStyle.ForeColor = Color.DimGray
                End If
            Next
        Catch ex As Exception
            ErrorLogService.Log("FrmRunningInstances.LoadInstances", ex)
            lblInfo.Text = "Çalışan program kayıtları yüklenemedi."
        End Try
    End Sub

    Private Sub CopySelected_Click(sender As Object, e As EventArgs)
        If grid.CurrentRow Is Nothing Then Return
        Dim text =
            "Bilgisayar: " & Convert.ToString(grid.CurrentRow.Cells("ComputerName").Value) & Environment.NewLine &
            "Windows Kullanıcısı: " & Convert.ToString(grid.CurrentRow.Cells("WindowsUser").Value) & Environment.NewLine &
            "Program Kullanıcısı: " & Convert.ToString(grid.CurrentRow.Cells("AppUser").Value) & Environment.NewLine &
            "PID: " & Convert.ToString(grid.CurrentRow.Cells("ProcessId").Value) & Environment.NewLine &
            "Durum: " & Convert.ToString(grid.CurrentRow.Cells("StatusText").Value) & Environment.NewLine &
            "EXE: " & Convert.ToString(grid.CurrentRow.Cells("ExecutablePath").Value)
        Clipboard.SetText(text)
    End Sub
End Class
