Imports System.Data
Imports System.Drawing
Imports System.Globalization
Imports System.Linq
Imports System.Text.RegularExpressions
Imports System.Windows.Forms

Public Class FrmMoldConnectionPlan
    Inherits Form

    Private Const GridRowBaseHeight As Integer = 54

    Private ReadOnly grid As New DataGridView()
    Private ReadOnly txtFilter As New TextBox()
    Private ReadOnly lblCount As New Label()
    Private ReadOnly lblSource As New Label()
    Private ReadOnly lblImportDate As New Label()
    Private ReadOnly lblCurrentInfo As New Label()
    Private ReadOnly lblFirstInfo As New Label()
    Private ReadOnly lblSecondInfo As New Label()
    Private ReadOnly planInfoToolTip As New ToolTip()
    Private rootLayout As TableLayoutPanel = Nothing
    Private topLayout As TableLayoutPanel = Nothing
    Private ReadOnly highlightFont As New Font("Segoe UI", 9.0F, FontStyle.Bold)
    Private todayConnectedMoldCodes As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Private activeBindingMoldCodes As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Private productsByTrCode As New Dictionary(Of String, ProductInfo)(StringComparer.OrdinalIgnoreCase)
    Private lastGridDoubleClickHandledAt As DateTime = DateTime.MinValue

    Public Sub New()
        AuthorizationService.Require(AppState.CanOpenMoldConnectionPlan, "Baglanacak Kalip Listesi")
        AppIconService.Apply(Me)
        Text = "Bağlanacak Kalıp Listesi"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        MinimumSize = New Size(760, 520)
        BackColor = Color.White

        rootLayout = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .BackColor = Color.White
        }
        rootLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 110.0F))
        rootLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 118.0F))
        rootLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 76.0F))
        rootLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(rootLayout)

        rootLayout.Controls.Add(BuildTopPanel(), 0, 0)
        rootLayout.Controls.Add(BuildSelectedPlanInfoPanel(), 0, 1)
        rootLayout.Controls.Add(BuildFilterPanel(), 0, 2)

        ConfigureGrid()
        rootLayout.Controls.Add(grid, 0, 3)

        AddHandler Shown, Sub() ApplyPlanLayoutMetrics()
        AddHandler DpiChanged, Sub() ApplyPlanLayoutMetrics()
        AddHandler Resize, Sub() ApplyPlanLayoutMetrics()

        LoadGrid()
    End Sub

    Private Sub ApplyPlanLayoutMetrics()
        If rootLayout Is Nothing OrElse rootLayout.IsDisposed OrElse rootLayout.RowStyles.Count < 4 Then Return

        Try
            Dim dpiScale = Math.Max(96, DeviceDpi) / 96.0R
            Dim logicalHeight = ResponsiveFormService.GetLogicalClientHeight(Me)
            Dim tightHeight = logicalHeight > 0 AndAlso logicalHeight < 700

            Dim topLogicalHeight = If(tightHeight, 104.0R, 110.0R)
            Dim infoLogicalHeight = If(tightHeight, 110.0R, 118.0R)
            Dim filterLogicalHeight = If(tightHeight, 70.0R, 76.0R)

            rootLayout.RowStyles(0).Height = CSng(Math.Round(topLogicalHeight * dpiScale))
            rootLayout.RowStyles(1).Height = CSng(Math.Round(infoLogicalHeight * dpiScale))
            rootLayout.RowStyles(2).Height = CSng(Math.Round(filterLogicalHeight * dpiScale))

            ApplyGridRowHeight(dpiScale)
        Catch ex As Exception
            ErrorLogService.Log("FrmMoldConnectionPlan.ApplyPlanLayoutMetrics", ex)
        End Try
    End Sub

    Private Sub ApplyGridRowHeight(Optional dpiScale As Double = 1.0R)
        If grid Is Nothing OrElse grid.IsDisposed Then Return

        Dim rowHeight = CInt(Math.Round(GridRowBaseHeight * Math.Max(1.0R, dpiScale)))
        If rowHeight < GridRowBaseHeight Then rowHeight = GridRowBaseHeight

        grid.RowTemplate.Height = rowHeight
        For Each row As DataGridViewRow In grid.Rows
            row.Height = rowHeight
        Next
    End Sub

    Private Function BuildTopPanel() As Control
        topLayout = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(10, 6, 10, 6),
            .BackColor = SystemColors.Control,
            .ColumnCount = 1,
            .RowCount = 3
        }
        topLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        topLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 31.0F))
        topLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 44.0F))
        topLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 25.0F))

        Dim headerRow As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0)
        }
        headerRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        headerRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 330.0F))
        headerRow.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim title As New Label() With {
            .Text = "Bağlanacak Kalıp Listesi",
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 14.0F, FontStyle.Bold),
            .BackColor = Color.Transparent,
            .TextAlign = ContentAlignment.MiddleLeft,
            .AutoEllipsis = True,
            .Margin = New Padding(4, 0, 8, 0)
        }
        headerRow.Controls.Add(title, 0, 0)

        lblImportDate.Dock = DockStyle.Fill
        lblImportDate.Font = New Font("Segoe UI", 10.5F, FontStyle.Bold)
        lblImportDate.ForeColor = Color.FromArgb(112, 71, 0)
        lblImportDate.BackColor = Color.FromArgb(255, 244, 214)
        lblImportDate.TextAlign = ContentAlignment.MiddleCenter
        lblImportDate.AutoEllipsis = True
        lblImportDate.Margin = New Padding(8, 0, 4, 0)
        lblImportDate.BorderStyle = BorderStyle.FixedSingle
        lblImportDate.Text = "Yükleme Tarihi: -"
        headerRow.Controls.Add(lblImportDate, 1, 0)

        topLayout.Controls.Add(headerRow, 0, 0)

        lblSource.Dock = DockStyle.Fill
        lblSource.Font = New Font("Segoe UI", 8.75F, FontStyle.Bold)
        lblSource.ForeColor = Color.FromArgb(25, 58, 100)
        lblSource.BackColor = Color.Transparent
        lblSource.AutoEllipsis = True
        lblSource.TextAlign = ContentAlignment.MiddleLeft
        lblSource.Margin = New Padding(4, 0, 4, 0)
        topLayout.Controls.Add(lblSource, 0, 2)

        Dim buttonHost As New Panel() With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0)
        }
        topLayout.Controls.Add(buttonHost, 0, 1)

        Dim bindFlow As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .BackColor = Color.Transparent,
            .AutoScroll = True,
            .Padding = New Padding(0, 3, 0, 3),
            .Margin = New Padding(0),
            .Visible = True
        }
        buttonHost.Controls.Add(bindFlow)

        Dim btnBindCurrent As New Button() With {.Text = "Çalışan TR'yi Bağla", .Width = 145, .Height = 30}
        btnBindCurrent.Margin = New Padding(0, 0, 6, 0)
        btnBindCurrent.Visible = AppState.CanOpenProductionBinding
        AddHandler btnBindCurrent.Click, Sub() OpenBindingFromSelectedPlan("Current")
        bindFlow.Controls.Add(btnBindCurrent)

        Dim btnBindFirst As New Button() With {.Text = "1. TR'yi Bağla", .Width = 115, .Height = 30}
        btnBindFirst.Margin = New Padding(0, 0, 6, 0)
        btnBindFirst.Visible = AppState.CanOpenProductionBinding
        AddHandler btnBindFirst.Click, Sub() OpenBindingFromSelectedPlan("First")
        bindFlow.Controls.Add(btnBindFirst)

        Dim btnBindSecond As New Button() With {.Text = "2. TR'yi Bağla", .Width = 115, .Height = 30}
        btnBindSecond.Margin = New Padding(0, 0, 12, 0)
        btnBindSecond.Visible = AppState.CanOpenProductionBinding
        AddHandler btnBindSecond.Click, Sub() OpenBindingFromSelectedPlan("Second")
        bindFlow.Controls.Add(btnBindSecond)

        Dim btnImport As New Button() With {.Text = "Excel'den Al", .Width = 130, .Height = 32}
        btnImport.Margin = New Padding(0, 0, 8, 0)
        btnImport.Visible = AppState.CanModifyMoldConnectionPlan
        AddHandler btnImport.Click, AddressOf Import_Click
        bindFlow.Controls.Add(btnImport)

        Dim btnEmailRecipients As New Button() With {.Text = "Mail Alıcıları", .Width = 125, .Height = 32}
        btnEmailRecipients.Margin = New Padding(0, 0, 8, 0)
        btnEmailRecipients.Visible = AppState.CanManageMoldConnectionPlanEmailRecipients
        AddHandler btnEmailRecipients.Click, AddressOf EmailRecipients_Click
        bindFlow.Controls.Add(btnEmailRecipients)

        Dim btnRefresh As New Button() With {.Text = "Yenile", .Width = 100, .Height = 32}
        btnRefresh.Margin = New Padding(0, 0, 8, 0)
        AddHandler btnRefresh.Click, Sub() LoadGrid()
        bindFlow.Controls.Add(btnRefresh)

        Dim btnClose As New Button() With {.Text = "Kapat", .Width = 100, .Height = 32}
        btnClose.Margin = New Padding(0, 0, 0, 0)
        AddHandler btnClose.Click, Sub() Close()
        bindFlow.Controls.Add(btnClose)

        Return topLayout
    End Function

    Private Function BuildSelectedPlanInfoPanel() As Control
        Dim panel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 3,
            .RowCount = 1,
            .Padding = New Padding(10, 5, 10, 5),
            .Margin = New Padding(0),
            .BackColor = Color.White
        }
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.3333F))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.3333F))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.3334F))
        panel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        panel.Controls.Add(CreatePlanInfoCard("ÇALIŞAN", Color.FromArgb(198, 224, 248), lblCurrentInfo, New Padding(0, 0, 7, 0)), 0, 0)
        panel.Controls.Add(CreatePlanInfoCard("1. BAĞLANACAK TR", Color.FromArgb(248, 218, 173), lblFirstInfo, New Padding(0, 0, 7, 0)), 1, 0)
        panel.Controls.Add(CreatePlanInfoCard("2. BAĞLANACAK TR", Color.FromArgb(218, 205, 244), lblSecondInfo, New Padding(0)), 2, 0)

        UpdateSelectedPlanInfo()
        Return panel
    End Function

    Private Function CreatePlanInfoCard(title As String, headerColor As Color, detailLabel As Label, margin As Padding) As Control
        Dim card As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .Margin = margin,
            .Padding = New Padding(0),
            .BackColor = Color.White,
            .CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
        }
        card.RowStyles.Add(New RowStyle(SizeType.Percent, 30.0F))
        card.RowStyles.Add(New RowStyle(SizeType.Percent, 70.0F))

        card.Controls.Add(New Label() With {
            .Text = title,
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(35, 45, 60),
            .BackColor = headerColor,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(10, 0, 6, 0),
            .Margin = New Padding(0)
        }, 0, 0)

        detailLabel.Text = "Bilgileri görmek için listeden bir satır seçin."
        detailLabel.Dock = DockStyle.Fill
        detailLabel.Font = New Font("Segoe UI", 8.75F, FontStyle.Regular)
        detailLabel.ForeColor = Color.FromArgb(42, 52, 66)
        detailLabel.BackColor = Color.White
        detailLabel.TextAlign = ContentAlignment.MiddleLeft
        detailLabel.Padding = New Padding(10, 3, 8, 3)
        detailLabel.Margin = New Padding(0)
        detailLabel.AutoEllipsis = True
        card.Controls.Add(detailLabel, 0, 1)

        Return card
    End Function

    Private Function BuildFilterPanel() As Control
        Dim panel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(10, 5, 10, 5),
            .BackColor = Color.WhiteSmoke,
            .ColumnCount = 3,
            .RowCount = 2
        }
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 60.0F))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 140.0F))
        panel.RowStyles.Add(New RowStyle(SizeType.Percent, 42.0F))
        panel.RowStyles.Add(New RowStyle(SizeType.Percent, 58.0F))

        lblCount.Text = "Liste"
        lblCount.Dock = DockStyle.Fill
        lblCount.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblCount.BackColor = Color.Transparent
        lblCount.TextAlign = ContentAlignment.MiddleLeft
        lblCount.AutoEllipsis = False
        lblCount.Margin = New Padding(2, 0, 2, 0)
        panel.SetColumnSpan(lblCount, 3)
        panel.Controls.Add(lblCount, 0, 0)

        panel.Controls.Add(New Label() With {.Text = "Arama", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .BackColor = Color.Transparent}, 0, 1)
        txtFilter.Dock = DockStyle.Fill
        txtFilter.Margin = New Padding(3, 3, 8, 3)
        txtFilter.PlaceholderText = "makine / kalıp / raf / P kodu / TR"
        AddHandler txtFilter.TextChanged, Sub() LoadGrid()
        panel.Controls.Add(txtFilter, 1, 1)

        Dim btnClearFilter As New Button() With {.Text = "Filtreyi Temizle", .Dock = DockStyle.Fill, .Margin = New Padding(0, 1, 0, 1)}
        AddHandler btnClearFilter.Click, Sub()
                                             txtFilter.Clear()
                                             LoadGrid()
                                         End Sub
        panel.Controls.Add(btnClearFilter, 2, 1)

        Return panel
    End Function

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.MultiSelect = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.AutoGenerateColumns = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None
        grid.ScrollBars = ScrollBars.Vertical
        grid.RowHeadersVisible = False
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.GridColor = Color.Gainsboro
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 235, 235)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
        grid.DefaultCellStyle.BackColor = Color.White
        grid.DefaultCellStyle.Padding = New Padding(4, 6, 4, 6)
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248)
        ApplyGridRowHeight()

        grid.Columns.Clear()
        grid.Columns.Add(MakeColumn("MachineName", "Makine Adı", 150))
        grid.Columns.Add(MakeColumn("MachineNo", "Makine No", 100))
        grid.Columns.Add(MakeSeparatorColumn("SepCurrent"))
        grid.Columns.Add(MakeColumn("RunningMolds", "Çalışan Kalıplar", 150))
        grid.Columns.Add(MakeColumn("CurrentMoldNo", "Çalışan Kalıp No", 130))
        grid.Columns.Add(MakeColumn("CurrentMoldRackNo", "Çalışan Raf", 105))
        grid.Columns.Add(MakeColumn("CurrentPlasticCode", "Çalışan P Kodu", 115))
        grid.Columns.Add(MakeColumn("CurrentTrCode", "Çalışan TR", 100))
        grid.Columns.Add(MakeSeparatorColumn("SepFirst"))
        grid.Columns.Add(MakeColumn("FirstMoldNo", "1. Bağlanacak Kalıp No", 155))
        grid.Columns.Add(MakeColumn("FirstMoldRackNo", "1. Raf", 95))
        grid.Columns.Add(MakeColumn("FirstPlasticCode", "1. P Kodu", 95))
        grid.Columns.Add(MakeColumn("FirstTrCode", "1. TR", 95))
        grid.Columns.Add(MakeSeparatorColumn("SepSecond"))
        grid.Columns.Add(MakeColumn("SecondMoldNo", "2. Bağlanacak Kalıp No", 155))
        grid.Columns.Add(MakeColumn("SecondMoldRackNo", "2. Raf", 95))
        grid.Columns.Add(MakeColumn("SecondPlasticCode", "2. P Kodu", 95))
        grid.Columns.Add(MakeColumn("SecondTrCode", "2. TR", 95))
        grid.Columns("MachineName").AutoSizeMode = DataGridViewAutoSizeColumnMode.None
        grid.Columns("MachineNo").AutoSizeMode = DataGridViewAutoSizeColumnMode.None
        grid.Columns("RunningMolds").AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
        grid.Columns("RunningMolds").MinimumWidth = 110
        grid.Columns("RunningMolds").DefaultCellStyle.WrapMode = DataGridViewTriState.False
        ApplyColumnGroupStyles()
        AddHandler grid.SelectionChanged, Sub() UpdateSelectedPlanInfo()
        AddHandler grid.CellFormatting, AddressOf Grid_CellFormatting
        AddHandler grid.CellMouseDoubleClick, AddressOf Grid_CellMouseDoubleClick
        AddHandler grid.MouseDoubleClick, AddressOf Grid_MouseDoubleClick
    End Sub

    Private Function MakeColumn(name As String, header As String, width As Integer) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .DataPropertyName = name,
            .HeaderText = header,
            .Width = width,
            .MinimumWidth = 40,
            .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            .FillWeight = CSng(width),
            .SortMode = DataGridViewColumnSortMode.Automatic
        }
    End Function

    Private Function MakeSeparatorColumn(name As String) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .DataPropertyName = "",
            .HeaderText = "",
            .Width = 8,
            .MinimumWidth = 8,
            .AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            .ReadOnly = True,
            .Resizable = DataGridViewTriState.False,
            .SortMode = DataGridViewColumnSortMode.NotSortable
        }
    End Function

    Private Sub ApplyColumnGroupStyles()
        For Each column As DataGridViewColumn In grid.Columns
            If IsSeparatorColumn(column.Name) Then
                column.DefaultCellStyle.BackColor = Color.FromArgb(214, 220, 228)
                column.HeaderCell.Style.BackColor = Color.FromArgb(174, 184, 198)
                Continue For
            End If

            Dim backColor = GroupBaseBackColor(column.Name, False)
            Dim headerColor = GroupHeaderBackColor(column.Name)
            column.DefaultCellStyle.BackColor = backColor
            column.HeaderCell.Style.BackColor = headerColor
            column.HeaderCell.Style.ForeColor = Color.FromArgb(35, 35, 35)
            column.HeaderCell.Style.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        Next
    End Sub

    Private Sub LoadGrid()
        Try
            todayConnectedMoldCodes = LoadTodayConnectedMoldCodes()
            activeBindingMoldCodes = LoadActiveBindingMoldCodes()
            RefreshProductLookup()
            Dim allRows = DataService.GetMoldConnectionPlanRows()
            Dim rows = allRows.AsEnumerable()
            Dim filterText = txtFilter.Text.Trim()

            If filterText <> "" Then
                Dim tokens = filterText.Split(New Char() {" "c, ";"c, ","c, "|"c}, StringSplitOptions.RemoveEmptyEntries)
                rows = rows.Where(Function(r)
                                      Dim haystack = String.Join(" ", DataService.MoldConnectionPlanHeaders.Select(Function(h) DataService.GetValue(r, h))).ToUpperInvariant()
                                      For Each token In tokens
                                          If Not haystack.Contains(token.ToUpperInvariant()) Then Return False
                                      Next
                                      Return True
                                  End Function)
            End If

            Dim list = rows.
                OrderByDescending(Function(r) HasMoldWaitingToConnect(r)).
                ThenBy(Function(r) SourceRowSortValue(DataService.GetValue(r, "SourceRow"))).
                ThenBy(Function(r) DataService.GetValue(r, "PlanId")).
                ToList()

            Dim dt As New DataTable()
            For Each h In DataService.MoldConnectionPlanHeaders
                dt.Columns.Add(h)
            Next

            For Each r In list
                Dim dr = dt.NewRow()
                For Each h In DataService.MoldConnectionPlanHeaders
                    dr(h) = DataService.GetValue(r, h)
                Next
                dt.Rows.Add(dr)
            Next

            grid.DataSource = dt
            Dim todayPlannedCount = CountTodayPlannedConnections(allRows)
            lblCount.Text = $"Liste: {dt.Rows.Count} / {allRows.Count} satır | Bağlama başladı: {activeBindingMoldCodes.Count} | Bugün tamamlanan: {todayConnectedMoldCodes.Count} / {todayPlannedCount}"
            UpdateSourceLabel(allRows)
            UpdateSelectedPlanInfo()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Liste yüklenemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub RefreshProductLookup()
        Try
            productsByTrCode = DataService.GetProducts(True).
                Where(Function(product) NormalizeTrCodeForExactMatch(product.TrCode) <> "").
                GroupBy(Function(product) NormalizeTrCodeForExactMatch(product.TrCode), StringComparer.OrdinalIgnoreCase).
                ToDictionary(
                    Function(group) group.Key,
                    Function(group) group.OrderBy(Function(product) product.DrawingRev, StringComparer.OrdinalIgnoreCase).Last(),
                    StringComparer.OrdinalIgnoreCase)
        Catch ex As Exception
            productsByTrCode = New Dictionary(Of String, ProductInfo)(StringComparer.OrdinalIgnoreCase)
            ErrorLogService.Log("FrmMoldConnectionPlan.RefreshProductLookup", ex)
        End Try
    End Sub

    Private Sub UpdateSelectedPlanInfo()
        If lblCurrentInfo Is Nothing OrElse lblCurrentInfo.IsDisposed Then Return

        If grid.CurrentRow Is Nothing Then
            SetPlanInfoText(lblCurrentInfo, "Bilgileri görmek için listeden bir satır seçin.")
            SetPlanInfoText(lblFirstInfo, "Bilgileri görmek için listeden bir satır seçin.")
            SetPlanInfoText(lblSecondInfo, "Bilgileri görmek için listeden bir satır seçin.")
            Return
        End If

        SetPlanInfoText(lblCurrentInfo, BuildPlanInfoText("Current", "CurrentTrCode", "CurrentMoldNo", "CurrentMoldRackNo", "CurrentPlasticCode"))
        SetPlanInfoText(lblFirstInfo, BuildPlanInfoText("First", "FirstTrCode", "FirstMoldNo", "FirstMoldRackNo", "FirstPlasticCode"))
        SetPlanInfoText(lblSecondInfo, BuildPlanInfoText("Second", "SecondTrCode", "SecondMoldNo", "SecondMoldRackNo", "SecondPlasticCode"))
    End Sub

    Private Function BuildPlanInfoText(planPart As String,
                                       trColumn As String,
                                       moldColumn As String,
                                       rackColumn As String,
                                       plasticColumn As String) As String
        Dim trCode = SelectedPlanValue(trColumn)
        Dim moldCode = SelectedPlanValue(moldColumn)
        If String.Equals(planPart, "Current", StringComparison.OrdinalIgnoreCase) AndAlso moldCode = "" Then
            moldCode = SelectedPlanValue("RunningMolds")
        End If

        Dim rackNo = SelectedPlanValue(rackColumn)
        Dim plasticCode = SelectedPlanValue(plasticColumn)
        Dim product As ProductInfo = Nothing
        Dim productKey = NormalizeTrCodeForExactMatch(trCode)
        If productKey <> "" Then productsByTrCode.TryGetValue(productKey, product)

        If plasticCode = "" AndAlso product IsNot Nothing Then plasticCode = product.PlasticCode
        If moldCode = "" AndAlso product IsNot Nothing Then moldCode = product.MoldCode

        Dim productName = If(product Is Nothing, If(trCode = "", "-", "Ürün kaydı bulunamadı"), DisplayValue(product.ProductName))
        Dim material = If(product Is Nothing, "-", DisplayValue(product.Material))
        Dim colorName = If(product Is Nothing, "-", DisplayValue(product.ColorName))
        Dim drawingRev = If(product Is Nothing, "-", DisplayValue(product.DrawingRev))

        Return "TR: " & DisplayValue(trCode) & "  |  Ürün: " & productName & Environment.NewLine &
               "Kalıp: " & DisplayValue(moldCode) & "  |  Raf: " & DisplayValue(rackNo) & "  |  P Kodu: " & DisplayValue(plasticCode) & Environment.NewLine &
               "Malzeme: " & material & "  |  Renk: " & colorName & "  |  Rev: " & drawingRev
    End Function

    Private Sub SetPlanInfoText(label As Label, value As String)
        If label Is Nothing OrElse label.IsDisposed Then Return
        label.Text = value
        planInfoToolTip.SetToolTip(label, value.Replace(Environment.NewLine, " | "))
    End Sub

    Private Shared Function DisplayValue(value As String) As String
        Dim text = If(value, "").Trim()
        Return If(text = "", "-", text)
    End Function

    Private Sub Grid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse e.ColumnIndex < 0 Then Return

        Dim columnName = grid.Columns(e.ColumnIndex).Name
        If IsSeparatorColumn(columnName) Then
            e.CellStyle.BackColor = Color.FromArgb(214, 220, 228)
            e.CellStyle.SelectionBackColor = Color.FromArgb(174, 184, 198)
            e.CellStyle.SelectionForeColor = Color.FromArgb(174, 184, 198)
            Return
        End If

        If IsGroupedColumn(columnName) Then
            e.CellStyle.BackColor = GroupBaseBackColor(columnName, e.RowIndex Mod 2 <> 0)
            e.CellStyle.SelectionBackColor = Color.FromArgb(180, 205, 235)
            e.CellStyle.SelectionForeColor = Color.Black
        End If

        If Not IsMoldColumn(columnName) OrElse e.Value Is Nothing Then Return

        If ContainsMoldCode(Convert.ToString(e.Value), activeBindingMoldCodes) Then
            e.CellStyle.BackColor = Color.FromArgb(189, 215, 238)
            e.CellStyle.ForeColor = Color.FromArgb(0, 51, 102)
            e.CellStyle.SelectionBackColor = Color.FromArgb(91, 155, 213)
            e.CellStyle.SelectionForeColor = Color.White
            e.CellStyle.Font = highlightFont
            Return
        End If

        If ContainsTodayConnectedMold(Convert.ToString(e.Value)) Then
            e.CellStyle.BackColor = Color.FromArgb(198, 239, 206)
            e.CellStyle.ForeColor = Color.FromArgb(0, 97, 0)
            e.CellStyle.SelectionBackColor = Color.FromArgb(146, 208, 80)
            e.CellStyle.SelectionForeColor = Color.Black
            e.CellStyle.Font = highlightFont
        End If
    End Sub

    Private Sub Grid_CellMouseDoubleClick(sender As Object, e As DataGridViewCellMouseEventArgs)
        HandleGridDoubleClick(e.RowIndex, e.ColumnIndex)
    End Sub

    Private Sub Grid_MouseDoubleClick(sender As Object, e As MouseEventArgs)
        Dim hit = grid.HitTest(e.X, e.Y)
        HandleGridDoubleClick(hit.RowIndex, hit.ColumnIndex)
    End Sub

    Private Sub HandleGridDoubleClick(rowIndex As Integer, columnIndex As Integer)
        If rowIndex < 0 Then Return
        If DateTime.Now.Subtract(lastGridDoubleClickHandledAt).TotalMilliseconds < 350 Then Return
        lastGridDoubleClickHandledAt = DateTime.Now

        If Not AppState.CanOpenProductionBinding Then Return

        If columnIndex >= 0 AndAlso columnIndex < grid.Columns.Count Then
            grid.CurrentCell = grid.Rows(rowIndex).Cells(columnIndex)
        ElseIf grid.Columns.Contains("MachineNo") Then
            grid.CurrentCell = grid.Rows(rowIndex).Cells("MachineNo")
        End If

        Dim columnName = If(columnIndex >= 0 AndAlso columnIndex < grid.Columns.Count, grid.Columns(columnIndex).Name, "")
        Dim planPart = PlanPartFromColumn(columnName)
        If planPart = "" Then planPart = FirstAvailablePlanPartForSelectedRow()

        If planPart = "" Then
            MessageBox.Show("Bu satırda bağlama başlatılabilecek TR bilgisi bulunamadı.", "Kalıp bağlama", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        OpenBindingFromSelectedPlan(planPart)
    End Sub

    Private Function PlanPartFromColumn(columnName As String) As String
        If CurrentGroupColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase) Then Return "Current"
        If FirstGroupColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase) Then Return "First"
        If SecondGroupColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase) Then Return "Second"
        Return ""
    End Function

    Private Function FirstAvailablePlanPartForSelectedRow() As String
        If SelectedPlanValue("FirstTrCode") <> "" Then Return "First"
        If SelectedPlanValue("SecondTrCode") <> "" Then Return "Second"
        If SelectedPlanValue("CurrentTrCode") <> "" Then Return "Current"
        Return ""
    End Function

    Private Sub OpenBindingFromSelectedPlan(planPart As String)
        Try
            AuthorizationService.Require(AppState.CanOpenProductionBinding, "Kalip Baglama Takibi")

            If grid.CurrentRow Is Nothing Then
                MessageBox.Show("Önce listeden bir satır seçiniz.", "Kalıp bağlama", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim trColumn As String = ""
            Dim moldColumn As String = ""
            Dim displayName As String = ""

            Select Case If(planPart, "").Trim().ToUpperInvariant()
                Case "CURRENT"
                    trColumn = "CurrentTrCode"
                    moldColumn = "CurrentMoldNo"
                    displayName = "Çalışan TR"
                Case "FIRST"
                    trColumn = "FirstTrCode"
                    moldColumn = "FirstMoldNo"
                    displayName = "1. bağlanacak TR"
                Case "SECOND"
                    trColumn = "SecondTrCode"
                    moldColumn = "SecondMoldNo"
                    displayName = "2. bağlanacak TR"
                Case Else
                    Return
            End Select

            Dim trCode = NormalizeTrCodeForBinding(SelectedPlanValue(trColumn))
            Dim moldCode = SelectedPlanValue(moldColumn)
            Dim machineNo = SelectedPlanValue("MachineNo")

            If trCode.Trim() = "" Then
                MessageBox.Show(displayName & " bilgisi boş. Bu satırdan kalıp bağlama başlatılamaz.", "Kalıp bağlama", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            ShowExactTechnicalDrawingBeforeBinding(trCode)

            Using f As New FrmProductionTicketEntry(trCode, moldCode, machineNo, "")
                If f.ShowDialog(Me) = DialogResult.OK Then
                    DialogResult = DialogResult.OK
                    Close()
                End If
            End Using
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Kalıp bağlama açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Shared Function NormalizeTrCodeForBinding(value As String) As String
        Dim text = Regex.Replace(If(value, "").Trim(), "\s+", " ")
        If text = "" Then Return ""

        Dim match = Regex.Match(text, "^TR\s*[-_/]?\s*(.+)$", RegexOptions.IgnoreCase)
        If Not match.Success Then Return text

        Dim rest = Regex.Replace(match.Groups(1).Value.Trim(), "\s+", " ").Trim()
        If rest = "" Then Return "TR"
        Return "TR " & rest
    End Function

    Private Sub ShowExactTechnicalDrawingBeforeBinding(trCode As String)
        Dim wantedTr = NormalizeTrCodeForExactMatch(trCode)
        Dim product = DataService.GetProducts(True).
            FirstOrDefault(Function(p) NormalizeTrCodeForExactMatch(p.TrCode) = wantedTr)

        If product Is Nothing Then
            MessageBox.Show(
                trCode & " koduyla birebir eşleşen aktif teknik resim kaydı bulunamadı." & Environment.NewLine &
                "Kalıp bağlama ekranı boş ürün bilgileriyle açılacak.",
                "Teknik resim bulunamadı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information)
            Return
        End If

        Dim drawingPath = IO.Path.Combine(AppPaths.DrawingsDir, If(product.DrawingFile, "").Trim())
        If String.IsNullOrWhiteSpace(product.DrawingFile) OrElse Not IO.File.Exists(drawingPath) Then
            MessageBox.Show(
                product.TrCode & " koduyla birebir eşleşen kayıt için teknik resim dosyası bulunamadı." & Environment.NewLine &
                "Kalıp bağlama ekranı boş kalıp kodu ve hammadde bilgileriyle açılacak.",
                "Teknik resim bulunamadı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information)
            Return
        End If

        Using viewer As New FrmPdfViewer(
            product.DrawingFile,
            "Teknik Resim - " & product.TrCode & " / " & product.DrawingRev,
            False)
            viewer.ShowDialog(Me)
        End Using

        AuditService.Log(
            "MOLD_CONNECTION_PLAN_DRAWING_VIEW",
            product.TrCode,
            product.DrawingRev,
            "Bağlanacak Kalıp Listesi üzerinden kalıp bağlama öncesinde teknik resim açıldı.")
    End Sub

    Private Shared Function NormalizeTrCodeForExactMatch(value As String) As String
        Return Regex.Replace(If(value, "").Trim(), "[\s\-_/]+", "").ToUpperInvariant()
    End Function

    Private Function SelectedPlanValue(columnName As String) As String
        If grid.CurrentRow Is Nothing Then Return ""

        Dim view = TryCast(grid.CurrentRow.DataBoundItem, DataRowView)
        If view IsNot Nothing AndAlso view.Row.Table.Columns.Contains(columnName) Then
            Return Convert.ToString(view.Row(columnName)).Trim()
        End If

        If grid.Columns.Contains(columnName) Then
            Return Convert.ToString(grid.CurrentRow.Cells(columnName).Value).Trim()
        End If

        Return ""
    End Function

    Private Shared Function IsMoldColumn(columnName As String) As Boolean
        Return String.Equals(columnName, "RunningMolds", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(columnName, "CurrentMoldNo", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(columnName, "FirstMoldNo", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(columnName, "SecondMoldNo", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function IsSeparatorColumn(columnName As String) As Boolean
        Return String.Equals(columnName, "SepCurrent", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(columnName, "SepFirst", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(columnName, "SepSecond", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function IsGroupedColumn(columnName As String) As Boolean
        Return CurrentGroupColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase) OrElse
               FirstGroupColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase) OrElse
               SecondGroupColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase)
    End Function

    Private Shared Function GroupBaseBackColor(columnName As String, alternate As Boolean) As Color
        If CurrentGroupColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase) Then
            Return If(alternate, Color.FromArgb(232, 242, 252), Color.FromArgb(241, 247, 253))
        End If

        If FirstGroupColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase) Then
            Return If(alternate, Color.FromArgb(255, 243, 223), Color.FromArgb(255, 248, 235))
        End If

        If SecondGroupColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase) Then
            Return If(alternate, Color.FromArgb(241, 235, 253), Color.FromArgb(248, 244, 255))
        End If

        Return If(alternate, Color.FromArgb(248, 248, 248), Color.White)
    End Function

    Private Shared Function GroupHeaderBackColor(columnName As String) As Color
        If CurrentGroupColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase) Then Return Color.FromArgb(198, 224, 248)
        If FirstGroupColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase) Then Return Color.FromArgb(248, 218, 173)
        If SecondGroupColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase) Then Return Color.FromArgb(218, 205, 244)
        Return Color.FromArgb(235, 235, 235)
    End Function

    Private Shared ReadOnly CurrentGroupColumns As String() = {"RunningMolds", "CurrentMoldNo", "CurrentMoldRackNo", "CurrentPlasticCode", "CurrentTrCode"}
    Private Shared ReadOnly FirstGroupColumns As String() = {"FirstMoldNo", "FirstMoldRackNo", "FirstPlasticCode", "FirstTrCode"}
    Private Shared ReadOnly SecondGroupColumns As String() = {"SecondMoldNo", "SecondMoldRackNo", "SecondPlasticCode", "SecondTrCode"}

    Private Shared Function SourceRowSortValue(sourceRow As String) As Integer
        Dim rowNumber As Integer
        If Integer.TryParse(If(sourceRow, "").Trim(), rowNumber) Then Return rowNumber
        Return Integer.MaxValue
    End Function

    Private Shared Function HasMoldWaitingToConnect(row As Dictionary(Of String, String)) As Boolean
        If row Is Nothing Then Return False

        Return DataService.GetValue(row, "FirstMoldNo").Trim() <> "" OrElse
               DataService.GetValue(row, "FirstTrCode").Trim() <> "" OrElse
               DataService.GetValue(row, "SecondMoldNo").Trim() <> "" OrElse
               DataService.GetValue(row, "SecondTrCode").Trim() <> ""
    End Function

    Private Shared Function CountTodayPlannedConnections(rows As List(Of Dictionary(Of String, String))) As Integer
        If rows Is Nothing OrElse rows.Count = 0 Then Return 0

        Dim candidateRows = rows.
            Where(Function(row) IsToday(DataService.GetValue(row, "ImportedAt"))).
            ToList()

        If candidateRows.Count = 0 Then
            candidateRows = rows
        End If

        Dim uniqueTargets As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        For Each row In candidateRows
            AddPlannedConnectionTarget(uniqueTargets, row, "First")
            AddPlannedConnectionTarget(uniqueTargets, row, "Second")
        Next

        If uniqueTargets.Count > 0 Then Return uniqueTargets.Count

        Return candidateRows.Where(Function(row) HasMoldWaitingToConnect(row)).Count()
    End Function

    Private Shared Sub AddPlannedConnectionTarget(targets As HashSet(Of String), row As Dictionary(Of String, String), prefix As String)
        If targets Is Nothing OrElse row Is Nothing Then Return

        Dim moldCode = NormalizeMoldCode(DataService.GetValue(row, prefix & "MoldNo"))
        Dim trCode = DataService.GetValue(row, prefix & "TrCode").Trim().ToUpperInvariant()
        Dim plasticCode = DataService.GetValue(row, prefix & "PlasticCode").Trim().ToUpperInvariant()
        Dim rackNo = DataService.GetValue(row, prefix & "MoldRackNo").Trim().ToUpperInvariant()

        If moldCode = "" AndAlso trCode = "" AndAlso plasticCode = "" Then Return

        Dim targetKey = If(moldCode <> "",
                           "MOLD:" & moldCode,
                           "TR:" & trCode & "|P:" & plasticCode & "|R:" & rackNo)
        targets.Add(targetKey)
    End Sub

    Private Function ContainsTodayConnectedMold(value As String) As Boolean
        Return ContainsMoldCode(value, todayConnectedMoldCodes)
    End Function

    Private Shared Function ContainsMoldCode(value As String, moldCodes As HashSet(Of String)) As Boolean
        If moldCodes Is Nothing OrElse moldCodes.Count = 0 Then Return False

        Dim normalizedValue = NormalizeMoldCode(value)
        If normalizedValue = "" Then Return False
        If moldCodes.Contains(normalizedValue) Then Return True

        For Each part In SplitMoldCodes(value)
            If moldCodes.Contains(part) Then Return True
        Next

        Return False
    End Function

    Private Function LoadTodayConnectedMoldCodes() As HashSet(Of String)
        Dim result As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        Try
            For Each r In DataService.GetMoldBindingRecords()
                If Not String.Equals(DataService.GetValue(r, "Status"), "COMPLETED", StringComparison.OrdinalIgnoreCase) Then Continue For
                If Not IsToday(DataService.GetValue(r, "CompletedAt")) Then Continue For

                For Each moldCode In SplitMoldCodes(DataService.GetValue(r, "MoldCode"))
                    If moldCode <> "" Then result.Add(moldCode)
                Next
            Next
        Catch ex As Exception
            ErrorLogService.Log("FrmMoldConnectionPlan.ApplyRowColors", ex)
        End Try

        Return result
    End Function

    Private Function LoadActiveBindingMoldCodes() As HashSet(Of String)
        Dim result As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        Try
            For Each r In DataService.GetMoldBindingRecords()
                If Not String.Equals(DataService.GetValue(r, "Status"), "STARTED", StringComparison.OrdinalIgnoreCase) Then Continue For

                For Each moldCode In SplitMoldCodes(DataService.GetValue(r, "MoldCode"))
                    If moldCode <> "" Then result.Add(moldCode)
                Next
            Next
        Catch ex As Exception
            ErrorLogService.Log("FrmMoldConnectionPlan.ApplyGridLayout", ex)
        End Try

        Return result
    End Function

    Private Shared Function SplitMoldCodes(value As String) As List(Of String)
        Dim raw = If(value, "")
        Dim parts = raw.Split(New Char() {","c, ";"c, "/"c, "\"c, "|"c}, StringSplitOptions.RemoveEmptyEntries)
        Dim result As New List(Of String)()

        For Each part In parts
            Dim normalized = NormalizeMoldCode(part)
            If normalized <> "" Then result.Add(normalized)
        Next

        If result.Count = 0 Then
            Dim normalized = NormalizeMoldCode(raw)
            If normalized <> "" Then result.Add(normalized)
        End If

        Return result
    End Function

    Private Shared Function NormalizeMoldCode(value As String) As String
        Return If(value, "").Trim().ToUpperInvariant()
    End Function

    Private Shared Function IsToday(text As String) As Boolean
        Dim value As DateTime
        If Not TryParseDate(text, value) Then Return False
        Return value.Date = Date.Today
    End Function

    Private Shared Function TryParseDate(text As String, ByRef value As DateTime) As Boolean
        Return DateTime.TryParseExact(If(text, "").Trim(),
                                      "yyyy-MM-dd HH:mm:ss",
                                      CultureInfo.InvariantCulture,
                                      DateTimeStyles.None,
                                      value) OrElse DateTime.TryParse(text, value)
    End Function

    Private Sub UpdateSourceLabel(rows As List(Of Dictionary(Of String, String)))
        If rows.Count = 0 Then
            lblImportDate.Text = "Yükleme Tarihi: -"
            lblSource.Text = "Excel Sayfası: - | Dosya: - | Aktaran: -"
            Return
        End If

        Dim newest = rows.OrderByDescending(Function(r) DataService.GetValue(r, "ImportedAt")).First()
        Dim sourceSheet = DataService.GetValue(newest, "SourceSheet")
        If sourceSheet.Trim() = "" Then sourceSheet = "-"
        Dim importedAt = DataService.GetValue(newest, "ImportedAt")
        lblImportDate.Text = "Yükleme Tarihi: " & FormatImportedDate(importedAt)
        lblSource.Text = "Excel Sayfası: " & sourceSheet &
                         " | Dosya: " & DisplaySourceValue(DataService.GetValue(newest, "SourceFile")) &
                         " | Aktaran: " & DisplaySourceValue(DataService.GetValue(newest, "ImportedBy")) &
                         " | Saat: " & FormatImportedTime(importedAt)
    End Sub

    Private Shared Function FormatImportedDate(value As String) As String
        Dim parsed As DateTime
        If TryParseDate(value, parsed) Then Return parsed.ToString("dd.MM.yyyy")
        If String.IsNullOrWhiteSpace(value) Then Return "-"
        Return value.Trim()
    End Function

    Private Shared Function FormatImportedTime(value As String) As String
        Dim parsed As DateTime
        If TryParseDate(value, parsed) Then Return parsed.ToString("HH:mm")
        Return "-"
    End Function

    Private Shared Function DisplaySourceValue(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then Return "-"
        Return value.Trim()
    End Function

    Private Sub EmailRecipients_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.CanManageMoldConnectionPlanEmailRecipients, "Bağlanacak Kalıp Listesi Mail Alıcıları")
            Using form As New FrmMoldConnectionPlanEmailRecipients()
                form.ShowDialog(Me)
            End Using
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Mail alıcıları açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub Import_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.CanModifyMoldConnectionPlan, "Baglanacak Kalip Listesi Aktarimi")

            Using ofd As New OpenFileDialog()
                ofd.Title = "Bağlanacak Kalıp Listesi Excel dosyası seç"
                ofd.Filter = "Excel Dosyası (*.xlsx)|*.xlsx"
                ofd.Multiselect = False

                If ofd.ShowDialog(Me) <> DialogResult.OK Then Return

                Dim rows = MoldConnectionPlanExcelService.ImportFromXlsx(ofd.FileName)
                If rows.Count = 0 Then
                    MessageBox.Show("Excel içinde 6-42 arası dolu satır bulunamadı.", "Aktarım", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Return
                End If

                If MessageBox.Show("Mevcut Bağlanacak Kalıp Listesi silinip seçilen Excel dosyasındaki " & rows.Count.ToString() & " satır aktarılacak." & Environment.NewLine &
                                   "Devam edilsin mi?",
                                   "Excel'den Al", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then
                    Return
                End If

                DataService.ReplaceMoldConnectionPlanRows(rows)
                AuditService.Log("MOLD_CONNECTION_PLAN_IMPORT", "", "", "Rows=" & rows.Count.ToString() & "; File=" & IO.Path.GetFileName(ofd.FileName))

                Dim mailMatchCount As Integer = 0
                Dim mailRecipientCount As Integer = 0
                Dim mailSent As Boolean = False
                Dim mailError As String = ""
                Dim mailOk = MoldConnectionPlanOpenTicketNotificationService.TryNotifyOpenTickets(
                    rows,
                    IO.Path.GetFileName(ofd.FileName),
                    mailMatchCount,
                    mailRecipientCount,
                    mailSent,
                    mailError)
                LoadGrid()

                Dim resultMessage As New System.Text.StringBuilder()
                resultMessage.AppendLine("Bağlanacak Kalıp Listesi aktarıldı.")
                resultMessage.AppendLine("Aktarılan satır: " & rows.Count.ToString())
                If mailMatchCount > 0 Then
                    resultMessage.AppendLine("Açık kalıp ticket eşleşmesi: " & mailMatchCount.ToString())
                    If mailSent Then
                        resultMessage.AppendLine("Mail gönderildi: " & mailRecipientCount.ToString() & " alıcı")
                    ElseIf mailRecipientCount = 0 Then
                        resultMessage.AppendLine("Mail alıcısı tanımlı olmadığı için bildirim gönderilmedi.")
                    ElseIf Not mailOk Then
                        resultMessage.AppendLine("Mail gönderilemedi: " & mailError)
                    End If
                End If

                MessageBox.Show(resultMessage.ToString().TrimEnd(),
                                If(mailOk, "Aktarım Tamamlandı", "Aktarım Tamamlandı - Mail Uyarısı"),
                                MessageBoxButtons.OK,
                                If(mailOk, MessageBoxIcon.Information, MessageBoxIcon.Warning))
                Return
                MessageBox.Show("Bağlanacak Kalıp Listesi aktarıldı." & Environment.NewLine &
                                "Aktarılan satır: " & rows.Count.ToString(),
                                "Aktarım Tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End Using
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Excel aktarımı başarısız", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub
End Class
