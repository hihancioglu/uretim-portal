Imports System.Data
Imports System.Drawing
Imports System.Globalization
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmMoldBindingDashboard
    Inherits Form

    Private ReadOnly cboPeriod As New ComboBox()
    Private ReadOnly dtPeriod As New DateTimePicker()
    Private ReadOnly txtFilter As New TextBox()
    Private ReadOnly lblRange As New Label()
    Private ReadOnly lblKpiTotal As New Label()
    Private ReadOnly lblKpiActive As New Label()
    Private ReadOnly lblKpiCompleted As New Label()
    Private ReadOnly lblKpiAvgDuration As New Label()
    Private ReadOnly lblKpiMachineChange As New Label()
    Private ReadOnly gridUser As New DataGridView()
    Private ReadOnly gridMold As New DataGridView()
    Private ReadOnly gridDetail As New DataGridView()

    Public Sub New()
        AuthorizationService.Require(AppState.CanOpenMoldBindingDashboard, "Kalip Baglama Dashboardu")
        AppIconService.Apply(Me)
        Text = "Kalıp Bağlama Dashboardu"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        MinimumSize = New Size(760, 560)
        BackColor = Color.White

        Dim main As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .BackColor = Color.White
        }
        main.RowStyles.Add(New RowStyle(SizeType.Absolute, 76.0F))
        main.RowStyles.Add(New RowStyle(SizeType.Absolute, 70.0F))
        main.RowStyles.Add(New RowStyle(SizeType.Percent, 42.0F))
        main.RowStyles.Add(New RowStyle(SizeType.Percent, 58.0F))
        Controls.Add(main)

        Dim top As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(12, 12, 12, 6),
            .BackColor = SystemColors.Control,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .AutoScroll = True
        }
        main.Controls.Add(top, 0, 0)

        top.Controls.Add(New Label() With {.Text = "Dönem", .Width = 55, .Height = 28, .TextAlign = ContentAlignment.MiddleLeft})
        cboPeriod.Width = 115
        cboPeriod.Height = 28
        cboPeriod.DropDownStyle = ComboBoxStyle.DropDownList
        cboPeriod.Items.AddRange({"GÜNLÜK", "HAFTALIK", "AYLIK", "TÜMÜ"})
        cboPeriod.SelectedIndex = 0
        AddHandler cboPeriod.SelectedIndexChanged, Sub() LoadDashboard()
        top.Controls.Add(cboPeriod)

        top.Controls.Add(New Label() With {.Text = "Tarih", .Width = 45, .Height = 28, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(14, 0, 0, 0)})
        dtPeriod.Width = 135
        dtPeriod.Height = 28
        dtPeriod.Format = DateTimePickerFormat.Custom
        dtPeriod.CustomFormat = "dd.MM.yyyy"
        dtPeriod.Value = DateTime.Today
        AddHandler dtPeriod.ValueChanged, Sub() LoadDashboard()
        top.Controls.Add(dtPeriod)

        top.Controls.Add(New Label() With {.Text = "Arama", .Width = 55, .Height = 28, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(14, 0, 0, 0)})
        txtFilter.Width = 300
        txtFilter.Height = 28
        txtFilter.PlaceholderText = "kullanıcı / kalıp / makine / TR / iş emri"
        If Not AppState.CanViewAllMoldBindingDashboard Then
            txtFilter.PlaceholderText = "kalıp / makine / TR / iş emri"
        End If
        AddHandler txtFilter.TextChanged, Sub() LoadDashboard()
        top.Controls.Add(txtFilter)

        Dim btnRefresh As New Button() With {.Text = "Yenile", .Width = 95, .Height = 32, .Margin = New Padding(16, 0, 0, 0)}
        AddHandler btnRefresh.Click, Sub() LoadDashboard()
        top.Controls.Add(btnRefresh)

        lblRange.Width = 650
        lblRange.Height = 32
        lblRange.TextAlign = ContentAlignment.MiddleLeft
        lblRange.ForeColor = Color.DimGray
        lblRange.Margin = New Padding(16, 0, 0, 0)
        top.Controls.Add(lblRange)

        Dim kpiPanel As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(12, 8, 12, 8),
            .BackColor = Color.WhiteSmoke,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .AutoScroll = True
        }
        main.Controls.Add(kpiPanel, 0, 1)

        ConfigureKpi(lblKpiTotal, "Toplam Bağlama Kaydı", "0")
        ConfigureKpi(lblKpiActive, "Devam Eden", "0")
        ConfigureKpi(lblKpiCompleted, "Tamamlanan", "0")
        ConfigureKpi(lblKpiAvgDuration, "Ort. Süre (dk)", "-")
        ConfigureKpi(lblKpiMachineChange, "Makine Değişimi", "0")
        kpiPanel.Controls.AddRange({lblKpiTotal, lblKpiActive, lblKpiCompleted, lblKpiAvgDuration, lblKpiMachineChange})

        Dim upperSplit As New SplitContainer() With {
            .Dock = DockStyle.Fill,
            .Orientation = Orientation.Vertical,
            .SplitterWidth = 6,
            .BackColor = Color.White
        }
        main.Controls.Add(upperSplit, 0, 2)
        AddHandler Shown, Sub() ResponsiveFormService.FitSplitContainer(upperSplit, 0.5R, 260, 260)

        Dim userSummaryTitle = If(AppState.CanViewAllMoldBindingDashboard, "Kim ne kadar kalıp bağladı?", "Benim kalıp bağlama özetim")
        Dim moldSummaryTitle = If(AppState.CanViewAllMoldBindingDashboard, "Hangi kalıbı kim bağladı?", "Benim kalıplarım")
        upperSplit.Panel1.Controls.Add(BuildGridGroup(userSummaryTitle, gridUser))
        upperSplit.Panel2.Controls.Add(BuildGridGroup(moldSummaryTitle, gridMold))

        main.Controls.Add(BuildGridGroup("Detay Liste - Başlangıç / Bitiş Takibi", gridDetail), 0, 3)

        ConfigureUserGrid()
        ConfigureMoldGrid()
        ConfigureDetailGrid()

        LoadDashboard()
    End Sub

    Private Sub ConfigureKpi(lbl As Label, title As String, valueText As String)
        lbl.Width = 230
        lbl.Height = 52
        lbl.Margin = New Padding(4, 0, 10, 0)
        lbl.Padding = New Padding(10, 4, 10, 4)
        lbl.BackColor = Color.White
        lbl.BorderStyle = BorderStyle.FixedSingle
        lbl.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lbl.TextAlign = ContentAlignment.MiddleLeft
        lbl.Text = title & Environment.NewLine & valueText
    End Sub

    Private Sub SetKpi(lbl As Label, title As String, valueText As String)
        lbl.Text = title & Environment.NewLine & valueText
    End Sub

    Private Function BuildGridGroup(title As String, grid As DataGridView) As Control
        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .BackColor = Color.White
        }
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 30.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim lbl As New Label() With {
            .Text = title,
            .Dock = DockStyle.Fill,
            .Padding = New Padding(10, 0, 0, 0),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 9.5F, FontStyle.Bold),
            .BackColor = Color.WhiteSmoke
        }
        layout.Controls.Add(lbl, 0, 0)
        layout.Controls.Add(grid, 0, 1)
        Return layout
    End Function

    Private Sub ConfigureCommonGrid(grid As DataGridView)
        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.MultiSelect = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.AutoGenerateColumns = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        grid.RowHeadersVisible = False
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.GridColor = Color.Gainsboro
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 235, 235)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.DefaultCellStyle.BackColor = Color.White
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248)
        grid.RowTemplate.Height = 26
    End Sub

    Private Function MakeColumn(name As String, header As String, width As Integer, fillWeight As Single) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .DataPropertyName = name,
            .HeaderText = header,
            .Width = width,
            .MinimumWidth = 55,
            .FillWeight = fillWeight,
            .SortMode = DataGridViewColumnSortMode.Automatic
        }
    End Function

    Private Sub ConfigureUserGrid()
        ConfigureCommonGrid(gridUser)
        gridUser.Columns.Clear()
        gridUser.Columns.Add(MakeColumn("UserName", "Kullanıcı", 150, 18))
        gridUser.Columns.Add(MakeColumn("StartedCount", "Başlatılan", 95, 11))
        gridUser.Columns.Add(MakeColumn("CompletedCount", "Bitirilen", 90, 10))
        gridUser.Columns.Add(MakeColumn("ActiveCount", "Devam Eden", 90, 10))
        gridUser.Columns.Add(MakeColumn("MachineChangeCount", "Mak. Değ.", 85, 9))
        gridUser.Columns.Add(MakeColumn("AvgDurationMin", "Ort. Süre dk", 95, 10))
        gridUser.Columns.Add(MakeColumn("LastStarted", "Son Başlangıç", 135, 16))
        gridUser.Columns.Add(MakeColumn("LastCompleted", "Son Bitiş", 135, 16))
        gridUser.Columns.Add(MakeColumn("Molds", "Kalıplar", 190, 20))
    End Sub

    Private Sub ConfigureMoldGrid()
        ConfigureCommonGrid(gridMold)
        gridMold.Columns.Clear()
        gridMold.Columns.Add(MakeColumn("MoldCode", "Kalıp Kodu", 120, 16))
        gridMold.Columns.Add(MakeColumn("StartedCount", "Başlatılan", 95, 11))
        gridMold.Columns.Add(MakeColumn("CompletedCount", "Bitirilen", 90, 10))
        gridMold.Columns.Add(MakeColumn("ActiveCount", "Devam Eden", 90, 10))
        gridMold.Columns.Add(MakeColumn("MachineChangeCount", "Mak. Değ.", 85, 9))
        gridMold.Columns.Add(MakeColumn("Users", "Bağlayanlar", 210, 27))
        gridMold.Columns.Add(MakeColumn("TrCodes", "TR Kodları", 160, 20))
        gridMold.Columns.Add(MakeColumn("LastStarted", "Son Başlangıç", 135, 16))
        gridMold.Columns.Add(MakeColumn("LastCompleted", "Son Bitiş", 135, 16))
    End Sub

    Private Sub ConfigureDetailGrid()
        ConfigureCommonGrid(gridDetail)
        gridDetail.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
        gridDetail.ScrollBars = ScrollBars.Both
        gridDetail.Columns.Clear()
        gridDetail.Columns.Add(MakeColumn("StatusText", "Durum", 95, 7))
        gridDetail.Columns.Add(MakeColumn("StartedAt", "Bağlama Başlangıç", 140, 10))
        gridDetail.Columns.Add(MakeColumn("CompletedAt", "Bağlama Bitiş", 140, 10))
        gridDetail.Columns.Add(MakeColumn("DurationMin", "Süre dk", 80, 6))
        gridDetail.Columns.Add(MakeColumn("StartedBy", "Başlatan", 120, 9))
        gridDetail.Columns.Add(MakeColumn("CompletedBy", "Bitiren", 120, 9))
        gridDetail.Columns.Add(MakeColumn("MachineNo", "Makine", 120, 9))
        gridDetail.Columns.Add(MakeColumn("PreviousMachineNo", "Önceki Makine", 120, 9))
        gridDetail.Columns.Add(MakeColumn("MoldCode", "Kalıp Kodu", 110, 8))
        gridDetail.Columns.Add(MakeColumn("BindingReason", "Bağlama Nedeni", 145, 10))
        gridDetail.Columns.Add(MakeColumn("MachineChangeReason", "Makine Değişim Nedeni", 210, 15))
        gridDetail.Columns.Add(MakeColumn("TrCode", "TR Kodu", 100, 8))
        gridDetail.Columns.Add(MakeColumn("DrawingRev", "Rev.", 60, 5))
        gridDetail.Columns.Add(MakeColumn("ProductName", "Ürün Adı", 180, 14))
        gridDetail.Columns.Add(MakeColumn("RawMaterial", "Hammadde", 160, 12))
        gridDetail.Columns.Add(MakeColumn("WorkOrderNo", "İş Emri No", 115, 9))
        gridDetail.Columns.Add(MakeColumn("ProductionTicketId", "Kalite Ticket", 170, 13))
        gridDetail.Columns.Add(MakeColumn("BindingId", "Bağlama No", 170, 13))
        gridDetail.Columns.Add(MakeColumn("StartNote", "Başlangıç Notu", 210, 15))
        gridDetail.Columns.Add(MakeColumn("FinishNote", "Bitiş Notu", 210, 15))
        gridDetail.Columns.Add(MakeColumn("Note", "Genel Not", 260, 20))
        AddHandler gridDetail.CellFormatting, AddressOf DetailGrid_CellFormatting
    End Sub

    Private Sub LoadDashboard()
        Try
            Dim range = GetSelectedRange()
            Dim startDate As DateTime = range.Item1
            Dim endDate As DateTime = range.Item2
            Dim includeAllDates As Boolean = cboPeriod.Text = "TÜMÜ"

            Dim scopeText = If(AppState.CanViewAllMoldBindingDashboard,
                               "Kapsam: Tüm kullanıcılar",
                               "Kapsam: Kendi kalıp bağlama kayıtlarım")
            If includeAllDates Then
                lblRange.Text = "Dönem: Tüm kayıtlar   |   " & scopeText
            Else
                lblRange.Text = "Dönem: " & startDate.ToString("dd.MM.yyyy HH:mm") & " - " & endDate.AddSeconds(-1).ToString("dd.MM.yyyy HH:mm") & "   |   " & scopeText
            End If

            Dim rows = DataService.GetMoldBindingRecords().
                Where(Function(r)
                          Dim d As DateTime
                          If Not TryParseDate(DataService.GetValue(r, "StartedAt"), d) Then Return False
                          If Not includeAllDates AndAlso (d < startDate OrElse d >= endDate) Then Return False
                          Return True
                      End Function).
                ToList()

            rows = ApplyUserScope(rows)

            Dim filterText = txtFilter.Text.Trim()
            If filterText <> "" Then
                Dim tokens = filterText.Split(New Char() {" "c, ";"c, ","c, "|"c}, StringSplitOptions.RemoveEmptyEntries)
                rows = rows.Where(Function(r)
                                      Dim haystack = (DataService.GetValue(r, "StartedBy") & " " &
                                                      DataService.GetValue(r, "CompletedBy") & " " &
                                                      DataService.GetValue(r, "MachineNo") & " " &
                                                      DataService.GetValue(r, "MoldCode") & " " &
                                                      DataService.GetValue(r, "TrCode") & " " &
                                                      DataService.GetValue(r, "DrawingRev") & " " &
                                                      DataService.GetValue(r, "ProductName") & " " &
                                                      DataService.GetValue(r, "RawMaterial") & " " &
                                                      DataService.GetValue(r, "WorkOrderNo") & " " &
                                                      DataService.GetValue(r, "BindingReason") & " " &
                                                      DataService.GetValue(r, "MachineChangeReason") & " " &
                                                      DataService.GetValue(r, "PreviousMachineNo") & " " &
                                                      DataService.GetValue(r, "Note")).ToUpperInvariant()
                                      For Each token In tokens
                                          If Not haystack.Contains(token.ToUpperInvariant()) Then Return False
                                      Next
                                      Return True
                                  End Function).
                    ToList()
            End If

            LoadKpis(rows)
            LoadUserSummary(rows)
            LoadMoldSummary(rows)
            LoadDetail(rows)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Dashboard yüklenemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function ApplyUserScope(rows As List(Of Dictionary(Of String, String))) As List(Of Dictionary(Of String, String))
        If AppState.CanViewAllMoldBindingDashboard Then Return rows

        Dim currentUser = If(AppState.CurrentUserName, "").Trim()
        If currentUser = "" Then Return New List(Of Dictionary(Of String, String))()

        Return rows.
            Where(Function(r) IsCurrentUsersMoldBindingRow(r, currentUser)).
            ToList()
    End Function

    Private Function IsCurrentUsersMoldBindingRow(row As Dictionary(Of String, String), currentUser As String) As Boolean
        Return String.Equals(DataService.GetValue(row, "StartedBy"), currentUser, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(DataService.GetValue(row, "CompletedBy"), currentUser, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Sub LoadKpis(rows As List(Of Dictionary(Of String, String)))
        Dim completedRows = rows.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "COMPLETED", StringComparison.OrdinalIgnoreCase)).ToList()
        Dim activeRows = rows.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "STARTED", StringComparison.OrdinalIgnoreCase)).ToList()
        Dim durations = completedRows.Select(Function(r) DurationMinutes(r)).Where(Function(x) x >= 0).ToList()
        Dim avgText = If(durations.Count = 0, "-", Math.Round(durations.Average(), 1).ToString("0.0"))
        Dim machineChangeCount = rows.Where(Function(r) IsMachineChangeRow(r)).Count()

        SetKpi(lblKpiTotal, "Toplam Bağlama Kaydı", rows.Count.ToString())
        SetKpi(lblKpiActive, "Devam Eden", activeRows.Count.ToString())
        SetKpi(lblKpiCompleted, "Tamamlanan", completedRows.Count.ToString())
        SetKpi(lblKpiAvgDuration, "Ort. Süre (dk)", avgText)
        SetKpi(lblKpiMachineChange, "Makine Değişimi", machineChangeCount.ToString())
    End Sub

    Private Sub LoadUserSummary(rows As List(Of Dictionary(Of String, String)))
        Dim dt As New DataTable()
        For Each col In {"UserName", "StartedCount", "CompletedCount", "ActiveCount", "MachineChangeCount", "AvgDurationMin", "LastStarted", "LastCompleted", "Molds"}
            dt.Columns.Add(col)
        Next

        Dim grouped = rows.
            GroupBy(Function(r)
                        Dim u = DataService.GetValue(r, "StartedBy")
                        If u.Trim() = "" Then u = "(boş)"
                        Return u
                    End Function).
            OrderByDescending(Function(g) g.Count()).
            ThenBy(Function(g) g.Key)

        For Each g In grouped
            Dim completedRows = g.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "COMPLETED", StringComparison.OrdinalIgnoreCase)).ToList()
            Dim activeRows = g.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "STARTED", StringComparison.OrdinalIgnoreCase)).ToList()
            Dim durations = completedRows.Select(Function(r) DurationMinutes(r)).Where(Function(x) x >= 0).ToList()

            Dim dr = dt.NewRow()
            dr("UserName") = g.Key
            dr("StartedCount") = g.Count().ToString()
            dr("CompletedCount") = completedRows.Count.ToString()
            dr("ActiveCount") = activeRows.Count.ToString()
            dr("MachineChangeCount") = g.Where(Function(r) IsMachineChangeRow(r)).Count().ToString()
            dr("AvgDurationMin") = If(durations.Count = 0, "-", Math.Round(durations.Average(), 1).ToString("0.0"))
            dr("LastStarted") = MaxDateText(g.Select(Function(r) DataService.GetValue(r, "StartedAt")))
            dr("LastCompleted") = MaxDateText(completedRows.Select(Function(r) DataService.GetValue(r, "CompletedAt")))
            dr("Molds") = JoinDistinct(g.Select(Function(r) DataService.GetValue(r, "MoldCode")))
            dt.Rows.Add(dr)
        Next

        gridUser.DataSource = dt
    End Sub

    Private Sub LoadMoldSummary(rows As List(Of Dictionary(Of String, String)))
        Dim dt As New DataTable()
        For Each col In {"MoldCode", "StartedCount", "CompletedCount", "ActiveCount", "MachineChangeCount", "Users", "TrCodes", "LastStarted", "LastCompleted"}
            dt.Columns.Add(col)
        Next

        Dim grouped = rows.
            GroupBy(Function(r)
                        Dim m = DataService.GetValue(r, "MoldCode")
                        If m.Trim() = "" Then m = "(kalıp boş)"
                        Return m
                    End Function).
            OrderByDescending(Function(g) g.Count()).
            ThenBy(Function(g) g.Key)

        For Each g In grouped
            Dim completedRows = g.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "COMPLETED", StringComparison.OrdinalIgnoreCase)).ToList()
            Dim activeRows = g.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "STARTED", StringComparison.OrdinalIgnoreCase)).ToList()

            Dim dr = dt.NewRow()
            dr("MoldCode") = g.Key
            dr("StartedCount") = g.Count().ToString()
            dr("CompletedCount") = completedRows.Count.ToString()
            dr("ActiveCount") = activeRows.Count.ToString()
            dr("MachineChangeCount") = g.Where(Function(r) IsMachineChangeRow(r)).Count().ToString()
            dr("Users") = JoinDistinct(g.Select(Function(r) DataService.GetValue(r, "StartedBy")))
            dr("TrCodes") = JoinDistinct(g.Select(Function(r) DataService.GetValue(r, "TrCode")))
            dr("LastStarted") = MaxDateText(g.Select(Function(r) DataService.GetValue(r, "StartedAt")))
            dr("LastCompleted") = MaxDateText(completedRows.Select(Function(r) DataService.GetValue(r, "CompletedAt")))
            dt.Rows.Add(dr)
        Next

        gridMold.DataSource = dt
    End Sub

    Private Sub LoadDetail(rows As List(Of Dictionary(Of String, String)))
        Dim dt As New DataTable()
        For Each col In {"StatusText", "StartedAt", "CompletedAt", "DurationMin", "StartedBy", "CompletedBy", "MachineNo", "PreviousMachineNo", "MoldCode", "BindingReason", "MachineChangeReason", "TrCode", "DrawingRev", "ProductName", "RawMaterial", "WorkOrderNo", "ProductionTicketId", "BindingId", "StartNote", "FinishNote", "Note"}
            dt.Columns.Add(col)
        Next

        For Each r In rows.OrderByDescending(Function(x) GetDateSortValue(DataService.GetValue(x, "StartedAt")))
            Dim dr = dt.NewRow()
            dr("StatusText") = If(String.Equals(DataService.GetValue(r, "Status"), "COMPLETED", StringComparison.OrdinalIgnoreCase), "BİTTİ", "DEVAM EDİYOR")
            dr("StartedAt") = DataService.GetValue(r, "StartedAt")
            dr("CompletedAt") = DataService.GetValue(r, "CompletedAt")
            dr("DurationMin") = DurationText(r)
            dr("StartedBy") = DataService.GetValue(r, "StartedBy")
            dr("CompletedBy") = DataService.GetValue(r, "CompletedBy")
            dr("MachineNo") = DataService.GetValue(r, "MachineNo")
            dr("PreviousMachineNo") = DataService.GetValue(r, "PreviousMachineNo")
            dr("MoldCode") = DataService.GetValue(r, "MoldCode")
            dr("BindingReason") = DataService.GetValue(r, "BindingReason")
            dr("MachineChangeReason") = DataService.GetValue(r, "MachineChangeReason")
            dr("TrCode") = DataService.GetValue(r, "TrCode")
            dr("DrawingRev") = DataService.GetValue(r, "DrawingRev")
            dr("ProductName") = DataService.GetValue(r, "ProductName")
            dr("RawMaterial") = DataService.GetValue(r, "RawMaterial")
            dr("WorkOrderNo") = DataService.GetValue(r, "WorkOrderNo")
            dr("ProductionTicketId") = DataService.GetValue(r, "ProductionTicketId")
            dr("BindingId") = DataService.GetValue(r, "BindingId")
            dr("StartNote") = DataService.GetValue(r, "StartNote")
            dr("FinishNote") = DataService.GetValue(r, "FinishNote")
            dr("Note") = DataService.GetValue(r, "Note")
            dt.Rows.Add(dr)
        Next

        gridDetail.DataSource = dt
    End Sub

    Private Sub DetailGrid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse Not gridDetail.Columns.Contains("StatusText") Then Return
        Dim st = Convert.ToString(gridDetail.Rows(e.RowIndex).Cells("StatusText").Value)
        If st = "DEVAM EDİYOR" Then
            gridDetail.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.LemonChiffon
            gridDetail.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.FromArgb(90, 70, 0)
        Else
            gridDetail.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.Honeydew
            gridDetail.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.DarkGreen
        End If
    End Sub

    Private Function IsMachineChangeRow(row As Dictionary(Of String, String)) As Boolean
        Dim previousMachine = DataService.GetValue(row, "PreviousMachineNo")
        Dim currentMachine = DataService.GetValue(row, "MachineNo")

        Return previousMachine.Trim() <> "" AndAlso
               currentMachine.Trim() <> "" AndAlso
               Not String.Equals(previousMachine, currentMachine, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function GetSelectedRange() As Tuple(Of DateTime, DateTime)
        Dim d = dtPeriod.Value.Date
        Dim period = If(cboPeriod.SelectedItem Is Nothing, "GÜNLÜK", cboPeriod.SelectedItem.ToString())

        If period = "HAFTALIK" Then
            Dim diff = (7 + (CInt(d.DayOfWeek) - CInt(DayOfWeek.Monday))) Mod 7
            Dim startWeek = d.AddDays(-diff)
            Return Tuple.Create(startWeek, startWeek.AddDays(7))
        End If

        If period = "AYLIK" Then
            Dim startMonth As New DateTime(d.Year, d.Month, 1)
            Return Tuple.Create(startMonth, startMonth.AddMonths(1))
        End If

        If period = "TÜMÜ" Then
            Return Tuple.Create(DateTime.MinValue, DateTime.MaxValue)
        End If

        Return Tuple.Create(d, d.AddDays(1))
    End Function

    Private Function TryParseDate(text As String, ByRef value As DateTime) As Boolean
        Return DateTime.TryParseExact(text,
                                      "yyyy-MM-dd HH:mm:ss",
                                      CultureInfo.InvariantCulture,
                                      DateTimeStyles.None,
                                      value) OrElse DateTime.TryParse(text, value)
    End Function

    Private Function GetDateSortValue(text As String) As DateTime
        Dim d As DateTime
        If TryParseDate(text, d) Then Return d
        Return DateTime.MinValue
    End Function

    Private Function DurationMinutes(row As Dictionary(Of String, String)) As Double
        Dim s As DateTime
        Dim e As DateTime
        If Not TryParseDate(DataService.GetValue(row, "StartedAt"), s) Then Return -1
        If Not TryParseDate(DataService.GetValue(row, "CompletedAt"), e) Then Return -1
        Return Math.Max(0, (e - s).TotalMinutes)
    End Function

    Private Function DurationText(row As Dictionary(Of String, String)) As String
        Dim d = DurationMinutes(row)
        If d < 0 Then
            If String.Equals(DataService.GetValue(row, "Status"), "STARTED", StringComparison.OrdinalIgnoreCase) Then Return "Devam ediyor"
            Return "-"
        End If
        Return Math.Round(d, 0).ToString("0")
    End Function

    Private Function MaxDateText(values As IEnumerable(Of String)) As String
        Dim dates = values.
            Select(Function(x) GetDateSortValue(x)).
            Where(Function(d) d > DateTime.MinValue).
            OrderByDescending(Function(d) d).
            ToList()

        If dates.Count = 0 Then Return "-"
        Return dates(0).ToString("yyyy-MM-dd HH:mm:ss")
    End Function

    Private Function JoinDistinct(values As IEnumerable(Of String)) As String
        Dim list = values.
            Where(Function(x) If(x, "").Trim() <> "").
            Select(Function(x) x.Trim()).
            Distinct(StringComparer.OrdinalIgnoreCase).
            OrderBy(Function(x) x).
            ToList()

        If list.Count = 0 Then Return "-"
        Return String.Join(", ", list.Take(8)) & If(list.Count > 8, " +" & (list.Count - 8).ToString(), "")
    End Function
End Class
