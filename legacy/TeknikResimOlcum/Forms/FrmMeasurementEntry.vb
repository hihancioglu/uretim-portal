Imports System.ComponentModel
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Microsoft.Web.WebView2.Core
Imports Microsoft.Web.WebView2.WinForms

Public Class FrmMeasurementEntry
    Inherits Form

    Private ReadOnly cboProduct As New ComboBox()
    Private ReadOnly txtProductFilter As New TextBox()
    Private allProducts As New List(Of ProductInfo)()
    Private ReadOnly txtLot As New TextBox()
    Private ReadOnly txtSerial As New TextBox()
    Private ReadOnly txtEyeNo As New TextBox()
    Private ReadOnly txtEyeCount As New TextBox()
    Private ReadOnly chkEyeClosed As New CheckBox()
    Private ReadOnly grid As New DataGridView()
    Private ReadOnly pdfViewer As New WebView2()
    Private ReadOnly lblPdfInfo As New Label()
    Private ReadOnly lblProductMeta As New Label()
    Private ReadOnly cboMeasurementGroup As New ComboBox()
    Private ReadOnly chkAutoNextMeasure As New CheckBox()
    Private ReadOnly lblMeasurementProgress As New Label()
    Private ReadOnly lblDraftInfo As New Label()
    Private rows As New BindingList(Of MeasurementRow)()
    Private measurementGroupAreas As New List(Of MeasurementGroupArea)()
    Private criticalGridFont As Font
    Private currentTempPdf As String = ""
    Private currentTempPng As String = ""
    Private currentTempHtml As String = ""
    Private webMessageHooked As Boolean = False
    Private navigationHooked As Boolean = False
    Private currentPdfZoom As Integer = 100
    Private ReadOnly lblZoomText As New Label()
    Private ReadOnly eyeBuffers As New Dictionary(Of Integer, Dictionary(Of String, EyeMeasureState))()
    Private ReadOnly eyeClosedStates As New Dictionary(Of Integer, Boolean)()
    Private ReadOnly initialTrCode As String
    Private ReadOnly initialDrawingRev As String
    Private ReadOnly linkedProductionTicketId As String
    Private ReadOnly linkedCommissioningId As String
    Private ReadOnly initialDrawingScope As String
    Private ReadOnly draftSaveTimer As New System.Windows.Forms.Timer()
    Private measurementSplit As SplitContainer
    Private measurementLayout As TableLayoutPanel
    Private measurementPdfToolbar As TableLayoutPanel
    Private measurementPdfTools As FlowLayoutPanel
    Private isApplyingResponsiveLayout As Boolean = False
    Private lastDraftPromptKey As String = ""
    Private suppressDraftSave As Boolean = False
    Private measurementDataCommitted As Boolean = False

    Public Sub New(Optional initialTrCode As String = "",
                   Optional initialDrawingRev As String = "",
                   Optional linkedProductionTicketId As String = "",
                   Optional linkedCommissioningId As String = "",
                   Optional initialDrawingScope As String = "")
        Dim isCommissioningMeasurement = Not String.IsNullOrWhiteSpace(linkedCommissioningId)
        AuthorizationService.Require(
            If(isCommissioningMeasurement, AppState.CanModifyNewMoldCommissioning, AppState.CanOpenMeasurement),
            If(isCommissioningMeasurement, "Yeni Kalıp Devreye Alma Parça Ölçümü", "Ölçüm Girişi"))
        AppIconService.Apply(Me)
        Me.initialTrCode = initialTrCode
        Me.initialDrawingRev = initialDrawingRev
        Me.linkedProductionTicketId = linkedProductionTicketId
        Me.linkedCommissioningId = linkedCommissioningId
        Me.initialDrawingScope = If(
            String.IsNullOrWhiteSpace(initialDrawingScope),
            "",
            ProductInfo.NormalizeDrawingScope(initialDrawingScope))
        Text = "Ölçüm Girişi - Teknik Resim ile Ölçüm Kaydı"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1500, 820)
        MinimumSize = New Size(760, 560)

        measurementSplit = New SplitContainer() With {
            .Dock = DockStyle.Fill,
            .Orientation = Orientation.Vertical,
            .SplitterWidth = 6
        }
        Controls.Add(measurementSplit)

        BuildPdfPanel(measurementSplit.Panel1)
        BuildMeasurementPanel(measurementSplit.Panel2)

        AddHandler Shown,
            Sub()
                ApplyResponsiveMeasurementLayout()
                BeginInvoke(New MethodInvoker(AddressOf ApplyResponsiveMeasurementLayout))
            End Sub
        AddHandler Resize, Sub() ApplyResponsiveMeasurementLayout()
        AddHandler DpiChanged, Sub() BeginInvoke(New MethodInvoker(AddressOf ApplyResponsiveMeasurementLayout))

        draftSaveTimer.Interval = 700
        AddHandler draftSaveTimer.Tick, AddressOf DraftSaveTimer_Tick
        AddHandler FormClosing, AddressOf FrmMeasurementEntry_FormClosing
        AddHandler FormClosed, AddressOf FrmMeasurementEntry_FormClosed
        LoadProducts()
        SelectInitialProductIfRequested()
    End Sub

    Private Sub BuildPdfPanel(parent As Control)
        parent.BackColor = Color.White
        pdfViewer.Dock = DockStyle.Fill
        parent.Controls.Add(pdfViewer)
    End Sub

    Private Sub BuildMeasurementPanel(parent As Control)
        parent.BackColor = Color.White

        measurementLayout = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .BackColor = Color.White
        }
        measurementLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 68.0F))
        measurementLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 310.0F))
        measurementLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        parent.Controls.Add(measurementLayout)

        measurementPdfToolbar = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .BackColor = SystemColors.Control,
            .ColumnCount = 1,
            .RowCount = 2,
            .Padding = New Padding(6, 4, 6, 2)
        }
        measurementPdfToolbar.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
        measurementPdfToolbar.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        measurementLayout.Controls.Add(measurementPdfToolbar, 0, 0)

        measurementPdfTools = New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = True,
            .AutoScroll = False,
            .Margin = New Padding(0)
        }
        measurementPdfToolbar.Controls.Add(measurementPdfTools, 0, 0)

        Dim btnLoadPdf As New Button() With {.Text = "Resim Göster", .Width = 105, .Height = 30, .Margin = New Padding(0, 0, 6, 5)}
        AddHandler btnLoadPdf.Click, AddressOf LoadPdf_Click

        Dim btnFit As New Button() With {.Text = "Fit", .Width = 58, .Height = 30, .Margin = New Padding(0, 0, 6, 5)}
        AddHandler btnFit.Click, Sub()
                                     currentPdfZoom = 100
                                     UpdateZoomLabel()
                                     ExecutePdfScript("if(window.externalFit){window.externalFit();}")
                                 End Sub

        Dim btnZoomOut As New Button() With {.Text = "-", .Width = 34, .Height = 30, .Margin = New Padding(0, 0, 4, 5)}
        AddHandler btnZoomOut.Click, Sub()
                                        currentPdfZoom = Math.Max(50, currentPdfZoom - 10)
                                        UpdateZoomLabel()
                                        ExecutePdfScript("if(window.externalSetZoom){window.externalSetZoom(" & (currentPdfZoom / 100.0R).ToString(System.Globalization.CultureInfo.InvariantCulture) & ");}")
                                    End Sub

        lblZoomText.Text = "100%"
        lblZoomText.TextAlign = ContentAlignment.MiddleCenter
        lblZoomText.Width = 52
        lblZoomText.Height = 30
        lblZoomText.Margin = New Padding(0, 0, 4, 5)

        Dim btnZoomIn As New Button() With {.Text = "+", .Width = 34, .Height = 30, .Margin = New Padding(0, 0, 10, 5)}
        AddHandler btnZoomIn.Click, Sub()
                                       currentPdfZoom = Math.Min(400, currentPdfZoom + 10)
                                       UpdateZoomLabel()
                                       ExecutePdfScript("if(window.externalSetZoom){window.externalSetZoom(" & (currentPdfZoom / 100.0R).ToString(System.Globalization.CultureInfo.InvariantCulture) & ");}")
                                   End Sub

        Dim btnUp As New Button() With {.Text = "↑", .Width = 34, .Height = 30, .Margin = New Padding(0, 0, 4, 5)}
        AddHandler btnUp.Click, Sub() ExecutePdfScript("if(window.externalScroll){window.externalScroll(0,-160);}")
        Dim btnDown As New Button() With {.Text = "↓", .Width = 34, .Height = 30, .Margin = New Padding(0, 0, 4, 5)}
        AddHandler btnDown.Click, Sub() ExecutePdfScript("if(window.externalScroll){window.externalScroll(0,160);}")
        Dim btnLeft As New Button() With {.Text = "←", .Width = 34, .Height = 30, .Margin = New Padding(0, 0, 4, 5)}
        AddHandler btnLeft.Click, Sub() ExecutePdfScript("if(window.externalScroll){window.externalScroll(-160,0);}")
        Dim btnRight As New Button() With {.Text = "→", .Width = 34, .Height = 30, .Margin = New Padding(0, 0, 8, 5)}
        AddHandler btnRight.Click, Sub() ExecutePdfScript("if(window.externalScroll){window.externalScroll(160,0);}")

        Dim btnRotateLeft As New Button() With {.Text = "Sol 90", .Width = 58, .Height = 30, .Margin = New Padding(0, 0, 4, 5)}
        AddHandler btnRotateLeft.Click, Sub()
                                            currentPdfZoom = 100
                                            UpdateZoomLabel()
                                            ExecutePdfScript("if(window.externalRotate){window.externalRotate(-90);}")
                                        End Sub

        Dim btnRotateRight As New Button() With {.Text = "Sağ 90", .Width = 58, .Height = 30, .Margin = New Padding(0, 0, 12, 5)}
        AddHandler btnRotateRight.Click, Sub()
                                             currentPdfZoom = 100
                                             UpdateZoomLabel()
                                             ExecutePdfScript("if(window.externalRotate){window.externalRotate(90);}")
                                         End Sub

        lblPdfInfo.Text = "Ölçü balonuna tıklayın, açılan kutudan değer girin."
        lblPdfInfo.Dock = DockStyle.Fill
        lblPdfInfo.AutoEllipsis = True
        lblPdfInfo.TextAlign = ContentAlignment.MiddleLeft
        lblPdfInfo.Margin = New Padding(2, 0, 2, 0)

        measurementPdfTools.Controls.AddRange({btnLoadPdf, btnFit, btnZoomOut, lblZoomText, btnZoomIn, btnUp, btnDown, btnLeft, btnRight, btnRotateLeft, btnRotateRight})
        measurementPdfToolbar.Controls.Add(lblPdfInfo, 0, 1)

        Dim top As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 7,
            .Padding = New Padding(8, 6, 8, 6),
            .BackColor = SystemColors.Control
        }
        top.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        top.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        top.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        top.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        top.RowStyles.Add(New RowStyle(SizeType.Absolute, 50.0F))
        top.RowStyles.Add(New RowStyle(SizeType.Absolute, 66.0F))
        top.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        measurementLayout.Controls.Add(top, 0, 1)

        Dim productRow = CreateResponsiveRow({95.0F, 100.0F, 75.0F, 55.0F}, {SizeType.Absolute, SizeType.Percent, SizeType.Absolute, SizeType.Absolute})
        top.Controls.Add(productRow, 0, 0)
        productRow.Controls.Add(CreateFieldLabel("TR / Revizyon"), 0, 0)
        cboProduct.Dock = DockStyle.Fill
        cboProduct.Margin = New Padding(3, 4, 8, 4)
        cboProduct.DropDownStyle = ComboBoxStyle.DropDownList
        AddHandler cboProduct.SelectedIndexChanged, Sub()
                                                       UpdateProductInfoLabel()
                                                       ApplyProductMoldCavityToEyeCount()
                                                       ClearEyeBuffers()
                                                       ResetEyeSequence()
                                                       LoadMeasurements()
                                                       LoadPdfToViewer(False)
                                                   End Sub
        productRow.Controls.Add(cboProduct, 1, 0)

        productRow.Controls.Add(CreateFieldLabel("Göz Adedi"), 2, 0)
        txtEyeCount.Dock = DockStyle.Fill
        txtEyeCount.Margin = New Padding(3, 4, 0, 4)
        txtEyeCount.Text = "1"
        txtEyeCount.TextAlign = HorizontalAlignment.Center
        AddHandler txtEyeCount.Leave, Sub()
                                          CommitGridAndStoreEyeState(GetCurrentEyeNo())
                                          NormalizeEyeInputs()
                                          ResetEyeSequence()
                                          RestoreEyeState(GetCurrentEyeNo())
                                          ScheduleDraftSave()
                                      End Sub
        productRow.Controls.Add(txtEyeCount, 3, 0)

        Dim filterRow = CreateResponsiveRow({95.0F, 100.0F}, {SizeType.Absolute, SizeType.Percent})
        top.Controls.Add(filterRow, 0, 1)
        filterRow.Controls.Add(CreateFieldLabel("TR Filtre"), 0, 0)
        txtProductFilter.Dock = DockStyle.Fill
        txtProductFilter.Margin = New Padding(3, 4, 0, 4)
        txtProductFilter.PlaceholderText = "TR / revizyon / ürün"
        AddHandler txtProductFilter.TextChanged, Sub() ApplyProductFilter()
        filterRow.Controls.Add(txtProductFilter, 1, 0)

        Dim eyeRow = CreateResponsiveRow({290.0F, 100.0F}, {SizeType.Absolute, SizeType.Percent})
        top.Controls.Add(eyeRow, 0, 2)

        Dim eyeFlow As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .AutoScroll = False,
            .Margin = New Padding(0)
        }
        eyeRow.Controls.Add(eyeFlow, 0, 0)

        chkEyeClosed.Text = "Göz Kapalı"
        chkEyeClosed.Width = 92
        chkEyeClosed.Height = 27
        chkEyeClosed.Margin = New Padding(0, 3, 6, 0)
        AddHandler chkEyeClosed.CheckedChanged, AddressOf EyeClosed_CheckedChanged

        Dim btnPrevEye As New Button() With {.Text = "◀", .Width = 34, .Height = 27, .Margin = New Padding(0, 2, 4, 0)}
        AddHandler btnPrevEye.Click, AddressOf PrevEye_Click

        Dim lblEyeNo = CreateFieldLabel("Göz No")
        lblEyeNo.Width = 52
        lblEyeNo.Margin = New Padding(0)

        txtEyeNo.Width = 45
        txtEyeNo.Height = 25
        txtEyeNo.Margin = New Padding(0, 3, 4, 0)
        txtEyeNo.Text = "1"
        txtEyeNo.TextAlign = HorizontalAlignment.Center
        txtEyeNo.ReadOnly = True
        txtEyeNo.BackColor = Color.WhiteSmoke

        Dim btnNextEye As New Button() With {.Text = "▶", .Width = 34, .Height = 27, .Margin = New Padding(0, 2, 0, 0)}
        AddHandler btnNextEye.Click, AddressOf NextEye_Click
        eyeFlow.Controls.AddRange({chkEyeClosed, btnPrevEye, lblEyeNo, txtEyeNo, btnNextEye})

        Dim lblUser As New Label() With {
            .Text = $"Operatör: {AppState.CurrentUserName}    Bilgisayar: {Environment.MachineName}",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .BackColor = Color.Transparent,
            .AutoEllipsis = True,
            .Margin = New Padding(6, 0, 0, 0)
        }
        eyeRow.Controls.Add(lblUser, 1, 0)

        Dim orderRow = CreateResponsiveRow({95.0F, 50.0F, 60.0F, 50.0F}, {SizeType.Absolute, SizeType.Percent, SizeType.Absolute, SizeType.Percent})
        top.Controls.Add(orderRow, 0, 3)
        orderRow.Controls.Add(CreateFieldLabel("İş Emri No"), 0, 0)
        txtLot.Dock = DockStyle.Fill
        txtLot.Margin = New Padding(3, 4, 8, 4)
        txtLot.PlaceholderText = "Opsiyonel"
        AddHandler txtLot.TextChanged, Sub() ScheduleDraftSave()
        orderRow.Controls.Add(txtLot, 1, 0)
        orderRow.Controls.Add(CreateFieldLabel("Seri No"), 2, 0)
        txtSerial.Dock = DockStyle.Fill
        txtSerial.Margin = New Padding(3, 4, 0, 4)
        AddHandler txtSerial.TextChanged, Sub() ScheduleDraftSave()
        orderRow.Controls.Add(txtSerial, 3, 0)

        lblProductMeta.Dock = DockStyle.Fill
        lblProductMeta.Margin = New Padding(2, 2, 2, 2)
        lblProductMeta.BackColor = Color.Transparent
        lblProductMeta.ForeColor = Color.FromArgb(45, 45, 45)
        lblProductMeta.Font = New Font("Segoe UI", 8.75F, FontStyle.Regular)
        lblProductMeta.AutoEllipsis = True
        lblProductMeta.TextAlign = ContentAlignment.MiddleLeft
        UpdateProductInfoLabel()
        top.Controls.Add(lblProductMeta, 0, 4)

        Dim statusFlow As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = True,
            .AutoScroll = False,
            .Margin = New Padding(0)
        }
        top.Controls.Add(statusFlow, 0, 5)

        Dim lblGroup As New Label() With {
            .Text = "Ölçüm Grubu",
            .AutoSize = True,
            .Height = 30,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(4, 8, 6, 0)
        }
        cboMeasurementGroup.Width = 145
        cboMeasurementGroup.Height = 30
        cboMeasurementGroup.DropDownStyle = ComboBoxStyle.DropDownList
        cboMeasurementGroup.Margin = New Padding(0, 4, 12, 0)
        AddHandler cboMeasurementGroup.SelectedIndexChanged, AddressOf MeasurementGroup_SelectedIndexChanged

        chkAutoNextMeasure.Text = "Kumpas modu: sıradaki ölçüye geç"
        chkAutoNextMeasure.Checked = True
        chkAutoNextMeasure.AutoSize = True
        chkAutoNextMeasure.Height = 30
        chkAutoNextMeasure.TextAlign = ContentAlignment.MiddleLeft
        chkAutoNextMeasure.Margin = New Padding(0, 8, 12, 0)

        lblMeasurementProgress.AutoSize = True
        lblMeasurementProgress.MinimumSize = New Size(190, 30)
        lblMeasurementProgress.Height = 30
        lblMeasurementProgress.TextAlign = ContentAlignment.MiddleLeft
        lblMeasurementProgress.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblMeasurementProgress.Margin = New Padding(0, 4, 12, 0)

        lblDraftInfo.AutoSize = True
        lblDraftInfo.MinimumSize = New Size(150, 30)
        lblDraftInfo.Height = 30
        lblDraftInfo.TextAlign = ContentAlignment.MiddleLeft
        lblDraftInfo.ForeColor = Color.DimGray
        lblDraftInfo.Margin = New Padding(0, 4, 0, 0)

        statusFlow.Controls.AddRange({lblGroup, cboMeasurementGroup, chkAutoNextMeasure, lblMeasurementProgress, lblDraftInfo})

        Dim buttonFlow As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .AutoScroll = False,
            .Margin = New Padding(0)
        }
        top.Controls.Add(buttonFlow, 0, 6)

        Dim btnSave As New Button() With {.Text = "Tüm Gözleri Kaydet", .Width = 155, .Height = 32, .Margin = New Padding(4, 3, 10, 0)}
        AddHandler btnSave.Click, AddressOf Save_Click
        Dim btnClear As New Button() With {.Text = "Temizle / Göz 1", .Width = 125, .Height = 32, .Margin = New Padding(0, 3, 0, 0)}
        AddHandler btnClear.Click, AddressOf Clear_Click
        buttonFlow.Controls.AddRange({btnSave, btnClear})

        ConfigureGrid()
        measurementLayout.Controls.Add(grid, 0, 2)
    End Sub

    Private Sub ApplyResponsiveMeasurementLayout()
        If isApplyingResponsiveLayout OrElse measurementSplit Is Nothing OrElse measurementSplit.IsDisposed OrElse
           measurementLayout Is Nothing OrElse measurementPdfToolbar Is Nothing OrElse measurementPdfTools Is Nothing Then Return

        isApplyingResponsiveLayout = True
        Try
            Dim logicalWidth = ResponsiveFormService.GetLogicalClientWidth(Me)
            If logicalWidth <= 0 Then logicalWidth = ResponsiveFormService.GetLogicalWorkingAreaWidth(Me)

            Dim splitRatio As Double
            If logicalWidth < 900 Then
                splitRatio = 0.34R
            ElseIf logicalWidth < 1200 Then
                splitRatio = 0.39R
            ElseIf logicalWidth < 1550 Then
                splitRatio = 0.46R
            Else
                splitRatio = 0.52R
            End If

            Dim dpiScale = Math.Max(96, DeviceDpi) / 96.0R
            ResponsiveFormService.FitSplitContainer(
                measurementSplit,
                splitRatio,
                CInt(Math.Round(220 * dpiScale)),
                CInt(Math.Round(480 * dpiScale)))

            Dim panelLogicalWidth = ResponsiveFormService.GetLogicalClientWidth(measurementSplit.Panel2)
            Dim compactToolbar = panelLogicalWidth > 0 AndAlso panelLogicalWidth < 720
            Dim toolsLogicalHeight = If(compactToolbar, 70, 36)
            Dim infoLogicalHeight = 26
            Dim toolsHeight = CSng(Math.Round(toolsLogicalHeight * dpiScale))
            Dim infoHeight = CSng(Math.Round(infoLogicalHeight * dpiScale))

            measurementPdfTools.WrapContents = True
            measurementPdfTools.AutoScroll = False
            measurementPdfToolbar.RowStyles(0).SizeType = SizeType.Absolute
            measurementPdfToolbar.RowStyles(0).Height = toolsHeight
            measurementPdfToolbar.RowStyles(1).SizeType = SizeType.Absolute
            measurementPdfToolbar.RowStyles(1).Height = infoHeight
            measurementLayout.RowStyles(0).SizeType = SizeType.Absolute
            measurementLayout.RowStyles(0).Height = toolsHeight + infoHeight + CSng(Math.Round(6 * dpiScale))
            measurementPdfTools.PerformLayout()
        Catch ex As Exception
            ErrorLogService.Log("FrmMeasurementEntry.ApplyResponsiveMeasurementLayout", ex)
        Finally
            isApplyingResponsiveLayout = False
        End Try
    End Sub

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.AutoGenerateColumns = False
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.MultiSelect = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        grid.RowHeadersVisible = False
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.GridColor = Color.Gainsboro
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 235, 235)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False
        grid.DefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(25, 118, 210)
        grid.DefaultCellStyle.SelectionForeColor = Color.White
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252)
        grid.ColumnHeadersHeight = 32
        grid.RowTemplate.Height = 30
        grid.ShowCellToolTips = True
        criticalGridFont = New Font(grid.Font, FontStyle.Bold)
        grid.ReadOnly = True
        grid.Columns.Clear()
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.DataPropertyName = "SortNo", .HeaderText = "Balon", .ReadOnly = True, .FillWeight = 45})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.DataPropertyName = "MeasureId", .HeaderText = "Ölçü No", .ReadOnly = True, .FillWeight = 125})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.DataPropertyName = "MeasureName", .HeaderText = "Ölçü Adı", .ReadOnly = True, .FillWeight = 145})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.DataPropertyName = "MeasurementGroup", .HeaderText = "Grup", .ReadOnly = True, .FillWeight = 80})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.DataPropertyName = "SampleFrequency", .HeaderText = "Numune Sıklığı", .ReadOnly = True, .FillWeight = 85})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.DataPropertyName = "IsCritical", .HeaderText = "Kritik", .ReadOnly = True, .FillWeight = 55})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.DataPropertyName = "Nominal", .HeaderText = "Nominal", .ReadOnly = True, .FillWeight = 65})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.DataPropertyName = "LowerTol", .HeaderText = "Alt Tol.", .ReadOnly = True, .FillWeight = 65})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.DataPropertyName = "UpperTol", .HeaderText = "Üst Tol.", .ReadOnly = True, .FillWeight = 65})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.DataPropertyName = "PageNo", .HeaderText = "Sayfa", .ReadOnly = True, .FillWeight = 45})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.DataPropertyName = "XPercent", .HeaderText = "X %", .ReadOnly = True, .FillWeight = 45})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.DataPropertyName = "YPercent", .HeaderText = "Y %", .ReadOnly = True, .FillWeight = 45})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.DataPropertyName = "Unit", .HeaderText = "Birim", .ReadOnly = True, .FillWeight = 45})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.DataPropertyName = "MeasuredValueText", .HeaderText = "Girilen", .ReadOnly = True, .FillWeight = 80})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.DataPropertyName = "Result", .HeaderText = "Sonuç", .ReadOnly = True, .FillWeight = 55})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.DataPropertyName = "Note", .HeaderText = "Not", .ReadOnly = True, .FillWeight = 110})
        ApplyMeasurementGridLayout()
        AddHandler grid.CellFormatting, AddressOf Grid_CellFormatting
        AddHandler grid.CellToolTipTextNeeded, AddressOf Grid_CellToolTipTextNeeded
        AddHandler grid.SelectionChanged, AddressOf Grid_SelectionChanged
        AddHandler grid.CellDoubleClick, AddressOf Grid_CellDoubleClick
        AddHandler grid.KeyDown, AddressOf Grid_KeyDown
    End Sub

    Private Sub ApplyMeasurementGridLayout()
        ConfigureMeasurementGridColumn("SortNo", "Balon", 52, 48, True, DataGridViewContentAlignment.MiddleCenter)
        ConfigureMeasurementGridColumn("MeasureId", "Ölçü No", 120, 110, False)
        ConfigureMeasurementGridColumn("MeasureName", "Ölçü", 210, 170, True)
        ConfigureMeasurementGridColumn("MeasurementGroup", "Grup", 80, 70, False)
        ConfigureMeasurementGridColumn("SampleFrequency", "Numune", 95, 80, False)
        ConfigureMeasurementGridColumn("IsCritical", "Kritik", 62, 58, True, DataGridViewContentAlignment.MiddleCenter)
        ConfigureMeasurementGridColumn("Nominal", "Nominal", 82, 70, True, DataGridViewContentAlignment.MiddleCenter)
        ConfigureMeasurementGridColumn("LowerTol", "Alt", 62, 55, True, DataGridViewContentAlignment.MiddleCenter)
        ConfigureMeasurementGridColumn("UpperTol", "Üst", 62, 55, True, DataGridViewContentAlignment.MiddleCenter)
        ConfigureMeasurementGridColumn("PageNo", "Sayfa", 45, 45, False, DataGridViewContentAlignment.MiddleCenter)
        ConfigureMeasurementGridColumn("XPercent", "X %", 45, 45, False, DataGridViewContentAlignment.MiddleCenter)
        ConfigureMeasurementGridColumn("YPercent", "Y %", 45, 45, False, DataGridViewContentAlignment.MiddleCenter)
        ConfigureMeasurementGridColumn("Unit", "Birim", 45, 45, False, DataGridViewContentAlignment.MiddleCenter)
        ConfigureMeasurementGridColumn("MeasuredValueText", "Girilen", 96, 82, True, DataGridViewContentAlignment.MiddleCenter)
        ConfigureMeasurementGridColumn("Result", "Sonuç", 72, 65, True, DataGridViewContentAlignment.MiddleCenter)
        ConfigureMeasurementGridColumn("Note", "Not", 150, 110, True)
    End Sub

    Private Sub ConfigureMeasurementGridColumn(dataPropertyName As String,
                                               headerText As String,
                                               fillWeight As Single,
                                               minimumWidth As Integer,
                                               visible As Boolean,
                                               Optional alignment As DataGridViewContentAlignment = DataGridViewContentAlignment.MiddleLeft)
        Dim column As DataGridViewColumn = Nothing
        If grid.Columns.Contains(dataPropertyName) Then
            column = grid.Columns(dataPropertyName)
        Else
            column = grid.Columns.
                Cast(Of DataGridViewColumn)().
                FirstOrDefault(Function(item) String.Equals(item.DataPropertyName, dataPropertyName, StringComparison.OrdinalIgnoreCase))
        End If
        If column Is Nothing Then Return

        column.Name = dataPropertyName
        column.HeaderText = headerText
        column.FillWeight = fillWeight
        column.MinimumWidth = minimumWidth
        column.Visible = visible
        column.SortMode = DataGridViewColumnSortMode.NotSortable
        column.DefaultCellStyle.Alignment = alignment
    End Sub

    Private Function CreateFieldLabel(text As String) As Label
        Return New Label() With {
            .Text = text,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .BackColor = Color.Transparent,
            .AutoEllipsis = True,
            .Margin = New Padding(0, 2, 6, 2)
        }
    End Function

    Private Function CreateResponsiveRow(widths As Single(), types As SizeType()) As TableLayoutPanel
        Dim row As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .RowCount = 1,
            .ColumnCount = widths.Length,
            .Margin = New Padding(0),
            .BackColor = Color.Transparent
        }
        row.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        For i As Integer = 0 To widths.Length - 1
            row.ColumnStyles.Add(New ColumnStyle(types(i), widths(i)))
        Next

        Return row
    End Function

    Private Sub ApplyProductMoldCavityToEyeCount()
        Dim p = SelectedProduct()
        If p Is Nothing Then Return

        Dim cavityCount As Integer = 0
        If Integer.TryParse(p.MoldCavityCount.Trim(), cavityCount) AndAlso cavityCount > 0 Then
            txtEyeCount.Text = cavityCount.ToString()
        ElseIf txtEyeCount.Text.Trim() = "" Then
            txtEyeCount.Text = "1"
        End If

        NormalizeEyeInputs()
    End Sub

    Private Sub UpdateProductInfoLabel()
        Dim p = SelectedProduct()
        If p Is Nothing Then
            lblProductMeta.Text = "Resim Tipi: -    TR Kodu: -    Plastik Kodu: -    Ürün Adı: -    Malzeme: -    Renk: -    Kalıp Göz Adedi: -    Kalıp Kodu: -"
            Return
        End If

        lblProductMeta.Text =
            "Resim Tipi: " & ProductInfo.NormalizeDrawingScope(p.DrawingScope) & "    TR Kodu: " & p.TrCode & "    Plastik Kodu: " & If(p.PlasticCode = "", "-", p.PlasticCode) & "    Ürün Adı: " & If(p.ProductName = "", "-", p.ProductName) & Environment.NewLine &
            "Malzeme: " & If(p.Material = "", "-", p.Material) & "    Renk: " & If(p.ColorName = "", "-", p.ColorName) & "    Kalıp Göz Adedi: " & If(p.MoldCavityCount = "", "-", p.MoldCavityCount) & "    Kalıp Kodu: " & If(p.MoldCode = "", "-", p.MoldCode)
    End Sub

    Private Sub SelectInitialProductIfRequested()
        If String.IsNullOrWhiteSpace(initialTrCode) Then Return

        For i As Integer = 0 To cboProduct.Items.Count - 1
            Dim p = TryCast(cboProduct.Items(i), ProductInfo)
            If p Is Nothing Then Continue For

            Dim trMatches = String.Equals(p.TrCode, initialTrCode, StringComparison.OrdinalIgnoreCase)
            Dim revMatches = String.IsNullOrWhiteSpace(initialDrawingRev) OrElse String.Equals(p.DrawingRev, initialDrawingRev, StringComparison.OrdinalIgnoreCase)
            Dim scopeMatches = String.IsNullOrWhiteSpace(initialDrawingScope) OrElse
                               String.Equals(ProductInfo.NormalizeDrawingScope(p.DrawingScope), initialDrawingScope, StringComparison.OrdinalIgnoreCase)

            If trMatches AndAlso revMatches AndAlso scopeMatches Then
                cboProduct.SelectedIndex = i
                UpdateProductInfoLabel()
                ApplyProductMoldCavityToEyeCount()
                LoadMeasurements()
                LoadPdfToViewer(False)
                Exit For
            End If
        Next
    End Sub

    Private Sub LoadProducts()
        Dim products = DataService.GetProducts(True)
        If Not String.IsNullOrWhiteSpace(linkedCommissioningId) Then
            allProducts = products.
                Where(Function(p) String.Equals(ProductInfo.NormalizeDrawingScope(p.DrawingScope), ProductInfo.DrawingScopeTr, StringComparison.OrdinalIgnoreCase)).
                ToList()
        ElseIf Not String.IsNullOrWhiteSpace(initialDrawingScope) Then
            allProducts = products.
                Where(Function(p) AppState.CanAccessDrawingScope(p.DrawingScope) AndAlso
                                  String.Equals(
                                      ProductInfo.NormalizeDrawingScope(p.DrawingScope),
                                      initialDrawingScope,
                                      StringComparison.OrdinalIgnoreCase)).
                ToList()
        Else
            allProducts = products.
                Where(Function(p) AppState.CanAccessDrawingScope(p.DrawingScope)).
                ToList()
        End If
        ApplyProductFilter()
    End Sub

    Private Sub ApplyProductFilter()
        Dim filterText As String = txtProductFilter.Text.Trim()
        Dim selectedKey As String = ""
        Dim current = SelectedProduct()
        If current IsNot Nothing Then selectedKey = current.TrCode & "|" & current.DrawingRev & "|" & current.DrawingFile

        Dim filtered As List(Of ProductInfo)
        If filterText = "" Then
            filtered = allProducts.ToList()
        Else
            Dim tokens = filterText.Split(New Char() {" "c, ";"c, ","c, "|"c}, StringSplitOptions.RemoveEmptyEntries)
            filtered = allProducts.Where(Function(p)
                                             Dim haystack As String = (p.TrCode & " " & p.DrawingRev & " " & p.ProductName & " " & ProductInfo.NormalizeDrawingScope(p.DrawingScope) & " " & p.DisplayName).ToUpperInvariant()
                                             For Each token In tokens
                                                 If Not haystack.Contains(token.ToUpperInvariant()) Then Return False
                                             Next
                                             Return True
                                         End Function).ToList()
        End If

        cboProduct.DataSource = Nothing
        cboProduct.DisplayMember = "DisplayName"
        cboProduct.DataSource = filtered
        cboProduct.DisplayMember = "DisplayName"

        If filtered.Count > 0 Then
            Dim restoreIndex As Integer = filtered.FindIndex(Function(p) (p.TrCode & "|" & p.DrawingRev & "|" & p.DrawingFile) = selectedKey)
            cboProduct.SelectedIndex = If(restoreIndex >= 0, restoreIndex, 0)
        Else
            rows = New BindingList(Of MeasurementRow)()
            grid.DataSource = rows
            UpdateProductInfoLabel()
            txtEyeCount.Text = "1"
            ResetEyeSequence()
        End If
    End Sub

    Private Function SelectedProduct() As ProductInfo
        Return TryCast(cboProduct.SelectedItem, ProductInfo)
    End Function

    Private Sub LoadMeasurements()
        Dim p = SelectedProduct()
        rows = New BindingList(Of MeasurementRow)()
        measurementGroupAreas = New List(Of MeasurementGroupArea)()
        If p IsNot Nothing Then
            measurementGroupAreas = DataService.GetMeasurementGroupAreas(p.TrCode, p.DrawingRev, p.DrawingScope)
            For Each cp In DataService.GetControlPoints(p.TrCode, p.DrawingRev, True, p.DrawingScope)
                rows.Add(New MeasurementRow With {
                    .MeasureId = cp.MeasureId,
                    .MeasureName = cp.MeasureName,
                    .Nominal = cp.Nominal,
                    .LowerTol = cp.LowerTol,
                    .UpperTol = cp.UpperTol,
                    .LowerLimit = cp.LowerLimit,
                    .UpperLimit = cp.UpperLimit,
                    .PageNo = cp.PageNo,
                    .XPercent = cp.XPercent,
                    .YPercent = cp.YPercent,
                    .Unit = cp.Unit,
                    .IsMandatory = cp.IsMandatory,
                    .MeasurementGroup = If(String.IsNullOrWhiteSpace(cp.MeasurementGroup), "Genel", cp.MeasurementGroup),
                    .SampleFrequency = If(String.IsNullOrWhiteSpace(cp.SampleFrequency), "Her Kontrol", cp.SampleFrequency),
                    .IsCritical = cp.IsCritical,
                    .SortNo = cp.SortNo,
                    .SpcKey = If(String.IsNullOrWhiteSpace(cp.SpcKey), cp.MeasureId, cp.SpcKey),
                    .MeasureVersion = Math.Max(1, cp.MeasureVersion),
                    .MeasuredValueText = "",
                    .Result = "",
                    .Note = ""
                })
            Next
        End If
        grid.DataSource = rows
        ClearEyeBuffers()
        PopulateMeasurementGroups()
        If Not TryRestoreMeasurementDraft() Then
            RestoreEyeState(GetCurrentEyeNo())
        End If
        ApplyMeasurementGroupFilter()
    End Sub

    Private Sub RecalculateRow(r As MeasurementRow)
        Dim measured As Decimal
        If r.MeasuredValueText Is Nothing OrElse r.MeasuredValueText.Trim() = "" Then
            r.Result = ""
            Return
        End If

        If Not NumberUtil.TryParseDecimal(r.MeasuredValueText, measured) Then
            r.Result = "HATALI"
            Return
        End If

        If measured >= r.LowerLimit AndAlso measured <= r.UpperLimit Then
            r.Result = "OK"
        Else
            r.Result = "NOK"
        End If
    End Sub

    Private Sub PopulateMeasurementGroups()
        Dim selectedText = If(TryCast(cboMeasurementGroup.SelectedItem, String), "Tüm Ölçüler")

        RemoveHandler cboMeasurementGroup.SelectedIndexChanged, AddressOf MeasurementGroup_SelectedIndexChanged
        cboMeasurementGroup.Items.Clear()
        cboMeasurementGroup.Items.Add("Tüm Ölçüler")
        cboMeasurementGroup.Items.Add("Kritik Ölçüler")

        For Each groupName In rows.
            Select(Function(r) If(String.IsNullOrWhiteSpace(r.MeasurementGroup), "Genel", r.MeasurementGroup.Trim())).
            Distinct(StringComparer.OrdinalIgnoreCase).
            OrderBy(Function(name) name, StringComparer.CurrentCultureIgnoreCase)
            cboMeasurementGroup.Items.Add(groupName)
        Next

        Dim restoreIndex = cboMeasurementGroup.FindStringExact(selectedText)
        cboMeasurementGroup.SelectedIndex = If(restoreIndex >= 0, restoreIndex, 0)
        AddHandler cboMeasurementGroup.SelectedIndexChanged, AddressOf MeasurementGroup_SelectedIndexChanged
    End Sub

    Private Sub MeasurementGroup_SelectedIndexChanged(sender As Object, e As EventArgs)
        ApplyMeasurementGroupFilter()
    End Sub

    Private Function IsRowInSelectedGroup(r As MeasurementRow) As Boolean
        If r Is Nothing OrElse cboMeasurementGroup.SelectedIndex <= 0 Then Return True
        If cboMeasurementGroup.SelectedIndex = 1 Then
            Return String.Equals(r.IsCritical, "YES", StringComparison.OrdinalIgnoreCase)
        End If

        Dim selectedGroup = If(TryCast(cboMeasurementGroup.SelectedItem, String), "")
        Dim rowGroup = If(String.IsNullOrWhiteSpace(r.MeasurementGroup), "Genel", r.MeasurementGroup.Trim())
        Return String.Equals(rowGroup, selectedGroup, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Sub ApplyMeasurementGroupFilter()
        If grid.DataSource Is Nothing Then Return

        Try
            grid.CurrentCell = Nothing
            grid.ClearSelection()

            Dim firstVisibleIndex As Integer = -1
            For i As Integer = 0 To grid.Rows.Count - 1
                Dim visible = i < rows.Count AndAlso IsRowInSelectedGroup(rows(i))
                grid.Rows(i).Visible = visible
                If visible AndAlso firstVisibleIndex < 0 Then firstVisibleIndex = i
            Next

            If firstVisibleIndex >= 0 Then
                grid.Rows(firstVisibleIndex).Selected = True
                grid.CurrentCell = grid.Rows(firstVisibleIndex).Cells(0)
            Else
                RefreshPdfSelection()
            End If
        Catch ex As Exception
            ErrorLogService.Log("FrmMeasurementEntry.ApplyMeasurementGroupFilter", ex)
        End Try

        UpdateMeasurementProgress()
        RefreshPdfMarkerFilter()
    End Sub

    Private Sub UpdateMeasurementProgress()
        Dim visibleRows = rows.Where(Function(r) IsRowInSelectedGroup(r)).ToList()
        Dim visibleCompleted = visibleRows.Where(Function(r) Not String.IsNullOrWhiteSpace(r.MeasuredValueText)).Count()
        Dim totalCompleted = rows.Where(Function(r) Not String.IsNullOrWhiteSpace(r.MeasuredValueText)).Count()
        lblMeasurementProgress.Text = $"Grup: {visibleCompleted}/{visibleRows.Count}   Toplam: {totalCompleted}/{rows.Count}"
    End Sub

    Private Sub Grid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse e.RowIndex >= rows.Count Then Return
        Dim r = rows(e.RowIndex)
        If r.Result = "NOK" Then
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.MistyRose
        ElseIf r.Result = "OK" Then
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.Honeydew
        ElseIf r.Result = "HATALI" Then
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.LightYellow
        Else
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.White
        End If

        If String.Equals(r.IsCritical, "YES", StringComparison.OrdinalIgnoreCase) Then
            grid.Rows(e.RowIndex).DefaultCellStyle.Font = criticalGridFont
            grid.Rows(e.RowIndex).DefaultCellStyle.ForeColor =
                If(String.IsNullOrWhiteSpace(r.Result), Color.DarkRed, SystemColors.ControlText)
        Else
            grid.Rows(e.RowIndex).DefaultCellStyle.Font = grid.Font
            grid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = SystemColors.ControlText
        End If

        If e.ColumnIndex >= 0 AndAlso grid.Columns(e.ColumnIndex).DataPropertyName = "IsCritical" Then
            e.Value = If(String.Equals(r.IsCritical, "YES", StringComparison.OrdinalIgnoreCase), "KRİTİK", "")
            e.FormattingApplied = True
        End If

        If e.ColumnIndex >= 0 Then
            Dim dataPropertyName = grid.Columns(e.ColumnIndex).DataPropertyName
            If String.Equals(dataPropertyName, "MeasuredValueText", StringComparison.OrdinalIgnoreCase) AndAlso
               Not String.IsNullOrWhiteSpace(r.MeasuredValueText) Then
                e.CellStyle.Font = criticalGridFont
            ElseIf String.Equals(dataPropertyName, "Result", StringComparison.OrdinalIgnoreCase) Then
                e.CellStyle.Font = criticalGridFont
                If String.Equals(r.Result, "OK", StringComparison.OrdinalIgnoreCase) Then
                    e.CellStyle.ForeColor = Color.DarkGreen
                ElseIf String.Equals(r.Result, "NOK", StringComparison.OrdinalIgnoreCase) Then
                    e.CellStyle.ForeColor = Color.DarkRed
                ElseIf String.Equals(r.Result, "HATALI", StringComparison.OrdinalIgnoreCase) Then
                    e.CellStyle.ForeColor = Color.DarkGoldenrod
                End If
            End If
        End If
    End Sub

    Private Sub Grid_CellToolTipTextNeeded(sender As Object, e As DataGridViewCellToolTipTextNeededEventArgs)
        If e.RowIndex < 0 OrElse e.RowIndex >= rows.Count Then Return

        Dim r = rows(e.RowIndex)
        Dim groupText = If(String.IsNullOrWhiteSpace(r.MeasurementGroup), "Genel", r.MeasurementGroup)
        Dim sampleText = If(String.IsNullOrWhiteSpace(r.SampleFrequency), "Her Kontrol", r.SampleFrequency)
        Dim unitText = If(String.IsNullOrWhiteSpace(r.Unit), "mm", r.Unit)

        e.ToolTipText =
            "Ölçü No: " & r.MeasureId & Environment.NewLine &
            "Grup: " & groupText & Environment.NewLine &
            "Numune: " & sampleText & Environment.NewLine &
            "Sayfa: " & r.PageNo.ToString() &
            " | X: " & NumberUtil.DecToCsv(r.XPercent) &
            " | Y: " & NumberUtil.DecToCsv(r.YPercent) & Environment.NewLine &
            "Birim: " & unitText
    End Sub

    Private Sub Grid_SelectionChanged(sender As Object, e As EventArgs)
        RefreshPdfSelection()
    End Sub

    Private Sub Grid_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 OrElse e.RowIndex >= rows.Count Then Return
        OpenEditorForMeasure(rows(e.RowIndex).MeasureId)
    End Sub

    Private Sub Grid_KeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode <> Keys.Enter Then Return
        Dim measureId = GetSelectedMeasureId()
        If String.IsNullOrWhiteSpace(measureId) Then Return
        e.Handled = True
        e.SuppressKeyPress = True
        OpenEditorForMeasure(measureId)
    End Sub

    Private Sub UpdateZoomLabel()
        lblZoomText.Text = currentPdfZoom.ToString() & "%"
    End Sub

    Private Sub ExecutePdfScript(script As String)
        Try
            If pdfViewer.CoreWebView2 IsNot Nothing Then
                pdfViewer.CoreWebView2.ExecuteScriptAsync(script)
            End If
        Catch ex As Exception
            ErrorLogService.Log("FrmMeasurementEntry.ExecutePdfScript", ex)
        End Try
    End Sub

    Private Sub LoadPdf_Click(sender As Object, e As EventArgs)
        LoadPdfToViewer(True)
    End Sub

    Private Async Sub LoadPdfToViewer(showErrors As Boolean)
        Try
            Dim p = SelectedProduct()
            If p Is Nothing Then Return

            CleanupCurrentPdfFiles()

            Dim imageUri As String = ""
            Dim aspectText As String = "1 / 1"
            Dim sourceKind As String = "PDF"

            If TempFileService.IsEncryptedPdf(p.DrawingFile) Then
                currentTempPdf = TempFileService.DecryptEncryptedPdfToTemp(p.DrawingFile)
                currentTempPng = PdfRenderService.RenderFirstPageToPng(currentTempPdf)
                imageUri = New Uri(currentTempPng).AbsoluteUri
                aspectText = PdfRenderService.GetImageAspectRatioText(currentTempPng)
            ElseIf TempFileService.IsEncryptedDxf(p.DrawingFile) Then
                currentTempPdf = TempFileService.DecryptEncryptedDrawingToTemp(p.DrawingFile)
                Dim render = DxfRenderService.RenderToSvg(currentTempPdf)
                currentTempPng = render.SvgPath
                imageUri = New Uri(currentTempPng).AbsoluteUri
                aspectText = render.AspectRatioText
                sourceKind = "DXF"
            Else
                Throw New InvalidDataException("Desteklenmeyen teknik resim türü: " & p.DrawingFile)
            End If

            currentTempHtml = Path.Combine(AppPaths.TempDir, "pdf_measure_" & Guid.NewGuid().ToString("N") & ".html")

            File.WriteAllText(currentTempHtml, BuildPdfWrapperHtml(imageUri, aspectText), New UTF8Encoding(False))

            Await pdfViewer.EnsureCoreWebView2Async()
            If Not webMessageHooked Then
                AddHandler pdfViewer.CoreWebView2.WebMessageReceived, AddressOf PdfMessageReceived
                webMessageHooked = True
            End If
            If Not navigationHooked Then
                AddHandler pdfViewer.CoreWebView2.NavigationCompleted, AddressOf PdfNavigationCompleted
                navigationHooked = True
            End If

            currentPdfZoom = 100
            UpdateZoomLabel()
            pdfViewer.Source = New Uri(currentTempHtml)
            lblPdfInfo.Text = sourceKind & " açık: " & p.TrCode & " / " & p.DrawingRev & "   |   Yanıp sönen balon sıradaki ölçüyü gösterir. Değeri sabit ölçüm çubuğundan girin."
            AuditService.Log(sourceKind & "_VIEW_MEASUREMENT", p.TrCode, p.DrawingRev, "Ölçüm girişi ekranında açıldı.")
        Catch ex As Exception
            lblPdfInfo.Text = "Teknik resim açılamadı."
            If showErrors Then MessageBox.Show(ex.Message, "Teknik resim açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function GetPdfPageAspectRatio(pdfPath As String) As String
        Try
            Dim raw As String = Encoding.ASCII.GetString(File.ReadAllBytes(pdfPath))

            Dim boxMatch As Match = Regex.Match(raw, "/CropBox\s*\[\s*([-0-9\.]+)\s+([-0-9\.]+)\s+([-0-9\.]+)\s+([-0-9\.]+)\s*\]", RegexOptions.IgnoreCase)
            If Not boxMatch.Success Then
                boxMatch = Regex.Match(raw, "/MediaBox\s*\[\s*([-0-9\.]+)\s+([-0-9\.]+)\s+([-0-9\.]+)\s+([-0-9\.]+)\s*\]", RegexOptions.IgnoreCase)
            End If

            If boxMatch.Success Then
                Dim x0 As Decimal = Decimal.Parse(boxMatch.Groups(1).Value, Globalization.CultureInfo.InvariantCulture)
                Dim y0 As Decimal = Decimal.Parse(boxMatch.Groups(2).Value, Globalization.CultureInfo.InvariantCulture)
                Dim x1 As Decimal = Decimal.Parse(boxMatch.Groups(3).Value, Globalization.CultureInfo.InvariantCulture)
                Dim y1 As Decimal = Decimal.Parse(boxMatch.Groups(4).Value, Globalization.CultureInfo.InvariantCulture)

                Dim w As Decimal = Math.Abs(x1 - x0)
                Dim h As Decimal = Math.Abs(y1 - y0)
                If w > 0D AndAlso h > 0D Then
                    Dim rotated As Boolean = Regex.IsMatch(raw, "/Rotate\s+(90|270)", RegexOptions.IgnoreCase)
                    Dim aspect As Decimal
                    If rotated Then
                        aspect = h / w
                    Else
                        aspect = w / h
                    End If

                    If aspect > 0.1D AndAlso aspect < 10D Then
                        Return aspect.ToString("0.########", Globalization.CultureInfo.InvariantCulture)
                    End If
                End If
            End If
        Catch ex As Exception
            ErrorLogService.Log("FrmMeasurementEntry.ReadPdfAspect", ex)
        End Try

        ' Teknik resimlerde varsayılan genellikle yatay A4'tür.
        Return "1.41421356"
    End Function

    Private Function NormalizePercent(value As Decimal) As Decimal
        Dim v As Decimal = value
        While v > 100D
            v = v / 100D
        End While
        If v < 0D Then v = 0D
        If v > 100D Then v = 100D
        Return v
    End Function

    Private Function BuildPdfWrapperHtml(pdfUri As String, pageAspect As String) As String
        Dim encodedPdfUri As String = WebUtility.HtmlEncode(pdfUri)
        Dim sb As New StringBuilder()
        sb.AppendLine("<!DOCTYPE html>")
        sb.AppendLine("<html><head><meta charset='utf-8'>")
        sb.AppendLine("<style>")
        sb.AppendLine("html, body { margin:0; width:100%; height:100%; overflow:hidden; background:#fff; font-family:Segoe UI, Arial; }")
        sb.AppendLine("#viewport { position:relative; width:100%; height:100%; overflow:auto; background:#fff; }")
        sb.AppendLine("#stage { position:absolute; left:0; top:0; background:#fff; }")
        sb.AppendLine("#pdf { position:absolute; border:0; z-index:1; background:#fff; pointer-events:none; display:block; image-rendering:auto; transform-origin:center center; }")
        sb.AppendLine("#overlay { position:absolute; left:0; top:0; width:100%; height:100%; z-index:20; pointer-events:none; }")
        sb.AppendLine(".marker { position:absolute; transform:translate(-50%,-50%); width:34px; min-width:34px; height:34px; padding:0; border-radius:50%; border:2px solid #8b0000; background:rgba(255,140,0,0.94); color:#fff; font-size:12px; font-weight:800; line-height:30px; text-align:center; box-shadow:0 1px 4px rgba(0,0,0,.35); pointer-events:auto; cursor:pointer; white-space:nowrap; }")
        sb.AppendLine(".marker:hover { filter:brightness(.95); }")
        sb.AppendLine("@keyframes activeMarkerBlink { 0%,100% { transform:translate(-50%,-50%) scale(1); box-shadow:0 0 0 2px rgba(255,204,0,.45), 0 1px 4px rgba(0,0,0,.4); } 50% { transform:translate(-50%,-50%) scale(1.22); box-shadow:0 0 0 9px rgba(255,204,0,.28), 0 0 18px rgba(255,204,0,.95), 0 1px 5px rgba(0,0,0,.45); } }")
        sb.AppendLine(".marker.sel { border-color:#ffcc00; animation:activeMarkerBlink .9s ease-in-out infinite; z-index:35; }")
        sb.AppendLine(".marker.critical { outline:3px solid rgba(139,0,0,.72); outline-offset:2px; }")
        sb.AppendLine(".marker.ok { background:rgba(40,167,69,.94); border-color:#1d7f35; }")
        sb.AppendLine(".marker.nok { background:rgba(220,53,69,.94); border-color:#8b0000; }")
        sb.AppendLine(".marker.empty { background:rgba(255,140,0,.94); border-color:#b36b00; }")
        sb.AppendLine(".marker.bad { background:rgba(255,193,7,.96); border-color:#8a6d00; color:#222; }")
        sb.AppendLine(".group-focus { position:absolute; display:none; border:4px solid rgba(25,86,155,.95); background:rgba(25,86,155,.10); box-sizing:border-box; pointer-events:none; z-index:25; }")
        sb.AppendLine(".group-focus span { position:absolute; left:4px; top:4px; padding:3px 7px; border-radius:3px; background:rgba(25,86,155,.95); color:#fff; font-size:12px; font-weight:700; }")
        sb.AppendLine("#editor { position:fixed; left:14px; right:14px; bottom:14px; top:auto; display:none; z-index:60; align-items:center; gap:10px; flex-wrap:wrap; padding:8px 10px; border:1px solid #666; border-radius:10px; background:rgba(255,255,255,.94); box-shadow:0 6px 20px rgba(0,0,0,.26); pointer-events:auto; backdrop-filter:blur(2px); }")
        sb.AppendLine("#editor.top { top:14px; bottom:auto; }")
        sb.AppendLine("#editor .ttl { font-size:13px; font-weight:800; margin:0; color:#111; white-space:nowrap; }")
        sb.AppendLine("#editor .meta { font-size:12px; color:#333; margin:0; max-width:240px; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }")
        sb.AppendLine("#editor .spec { display:grid; grid-template-columns:auto auto auto auto auto auto; gap:2px 6px; font-size:12px; align-items:center; }")
        sb.AppendLine("#editor .spec b { font-weight:700; color:#333; }")
        sb.AppendLine("#editor .row { display:flex; align-items:center; gap:6px; margin:0; }")
        sb.AppendLine("#editor .row label { font-weight:700; white-space:nowrap; }")
        sb.AppendLine("#editor input { width:190px; box-sizing:border-box; padding:7px 9px; font-size:15px; font-weight:700; }")
        sb.AppendLine("#editor .btns { display:flex; align-items:center; gap:6px; margin-left:auto; }")
        sb.AppendLine("#editor button { margin:0; padding:7px 12px; cursor:pointer; }")
        sb.AppendLine("</style></head><body>")
        sb.AppendLine("<div id='viewport'><div id='stage'>")
        sb.AppendLine("<img id='pdf' src='" & encodedPdfUri & "' alt='PDF Sayfası' />")
        sb.AppendLine("<div id='overlay'></div>")
        sb.AppendLine("<div id='groupFocus' class='group-focus'><span id='groupFocusLabel'></span></div>")
        sb.AppendLine("<div id='editor'>")
        sb.AppendLine("<div class='ttl' id='edTitle'>Ölçü Girişi</div>")
        sb.AppendLine("<div class='meta' id='edName'></div>")
        sb.AppendLine("<div class='spec'>")
        sb.AppendLine("<b>Nominal</b><span id='edNominal'></span>")
        sb.AppendLine("<b>Alt Tolerans</b><span id='edLowerTol'></span>")
        sb.AppendLine("<b>Üst Tolerans</b><span id='edUpperTol'></span>")
        sb.AppendLine("</div>")
        sb.AppendLine("<div class='row'><label>Girilen Değer</label><input id='edValue' type='text' /></div>")
        sb.AppendLine("<div class='btns'><button type='button' onclick='closeEditor()'>Kapat</button><button type='button' onclick='saveEditor()'>Kaydet</button></div>")
        sb.AppendLine("</div>")
        sb.AppendLine("</div></div>")
        sb.AppendLine("<script>")
        sb.AppendLine("const viewport=document.getElementById('viewport'); const stage=document.getElementById('stage'); const overlay=document.getElementById('overlay'); const img=document.getElementById('pdf');")
        sb.AppendLine("const markers={}; let selectedId=''; let activeId=''; let zoom=1; let baseW=0; let baseH=0; let rotation=0; let groupAreaData=null;")
        sb.AppendLine("const ed=document.getElementById('editor'); const edTitle=document.getElementById('edTitle'); const edName=document.getElementById('edName'); const edNominal=document.getElementById('edNominal'); const edLowerTol=document.getElementById('edLowerTol'); const edUpperTol=document.getElementById('edUpperTol'); const edValue=document.getElementById('edValue');")
        sb.AppendLine("const groupFocus=document.getElementById('groupFocus'); const groupFocusLabel=document.getElementById('groupFocusLabel');")
        sb.AppendLine("function imageAspect(){ const w=img.naturalWidth||1; const h=img.naturalHeight||1; return w/h; }")
        sb.AppendLine("function visibleAspect(){ const a=imageAspect(); return (rotation%180===0)?a:(1/a); }")
        sb.AppendLine("function origToStage(x,y){ x=parseFloat(x)||0; y=parseFloat(y)||0; if(rotation===90){ return {x:100-y,y:x}; } if(rotation===180){ return {x:100-x,y:100-y}; } if(rotation===270){ return {x:y,y:100-x}; } return {x:x,y:y}; }")
        sb.AppendLine("function layoutImage(){ const sw=stage.clientWidth, sh=stage.clientHeight; if(rotation%180===0){ img.style.left='0px'; img.style.top='0px'; img.style.width=sw+'px'; img.style.height=sh+'px'; } else { img.style.width=sh+'px'; img.style.height=sw+'px'; img.style.left=((sw-sh)/2)+'px'; img.style.top=((sh-sw)/2)+'px'; } img.style.transform='rotate('+rotation+'deg)'; }")
        sb.AppendLine("function updateMarkerPosition(btn){ const pt=origToStage(btn.dataset.x,btn.dataset.y); btn.style.left=pt.x+'%'; btn.style.top=pt.y+'%'; }")
        sb.AppendLine("function updateGroupFocus(){ if(!groupAreaData){groupFocus.style.display='none';return;} const a=groupAreaData; const pts=[origToStage(a.l,a.t),origToStage(a.r,a.t),origToStage(a.l,a.b),origToStage(a.r,a.b)]; const xs=pts.map(p=>p.x),ys=pts.map(p=>p.y);const l=Math.min(...xs),r=Math.max(...xs),t=Math.min(...ys),b=Math.max(...ys);groupFocus.style.left=l+'%';groupFocus.style.top=t+'%';groupFocus.style.width=(r-l)+'%';groupFocus.style.height=(b-t)+'%';groupFocus.style.display='block';groupFocusLabel.textContent=a.name||''; }")
        sb.AppendLine("function updateAllMarkers(){ Object.keys(markers).forEach(function(k){ updateMarkerPosition(markers[k]); }); updateGroupFocus(); }")
        sb.AppendLine("function placeStage(){ const sw=baseW*zoom, sh=baseH*zoom; stage.style.width=sw+'px'; stage.style.height=sh+'px'; const left=Math.max(0,(viewport.clientWidth-sw)/2); const top=Math.max(0,(viewport.clientHeight-sh)/2); stage.style.left=left+'px'; stage.style.top=top+'px'; layoutImage(); updateAllMarkers(); }")
        sb.AppendLine("function fitStage(){ const fitMargin=0.98; const vw=Math.max(10,viewport.clientWidth*fitMargin); const vh=Math.max(10,viewport.clientHeight*fitMargin); const a=visibleAspect(); let w=vw; let h=w/a; if(h>vh){ h=vh; w=h*a; } baseW=w; baseH=h; zoom=1; placeStage(); viewport.scrollLeft=0; viewport.scrollTop=0; notifyZoom(); }")
        sb.AppendLine("function notifyZoom(){ if(window.chrome && chrome.webview){ chrome.webview.postMessage('zoom|' + Math.round(zoom*100)); } }")
        sb.AppendLine("function applyZoom(newZoom, clientX, clientY){ if(!baseW||!baseH) fitStage(); const old=zoom; const sx=viewport.scrollLeft, sy=viewport.scrollTop; const hasPoint=(typeof clientX==='number' && typeof clientY==='number'); zoom=Math.max(0.5,Math.min(4,newZoom)); if(hasPoint){ const rect=viewport.getBoundingClientRect(); const px=(clientX-rect.left+viewport.scrollLeft-(parseFloat(stage.style.left)||0))/old; const py=(clientY-rect.top+viewport.scrollTop-(parseFloat(stage.style.top)||0))/old; placeStage(); viewport.scrollLeft=px*zoom+(parseFloat(stage.style.left)||0)-(clientX-rect.left); viewport.scrollTop=py*zoom+(parseFloat(stage.style.top)||0)-(clientY-rect.top); } else { placeStage(); if(zoom<=1){ viewport.scrollLeft=0; viewport.scrollTop=0; } else { viewport.scrollLeft=sx; viewport.scrollTop=sy; } } notifyZoom(); }")
        sb.AppendLine("viewport.addEventListener('wheel', function(ev){ if(ev.ctrlKey){ ev.preventDefault(); applyZoom(zoom*(ev.deltaY<0?1.12:0.89), ev.clientX, ev.clientY); } }, {passive:false});")
        sb.AppendLine("window.externalSetZoom=function(z){ applyZoom(z); };")
        sb.AppendLine("window.externalFit=function(){ fitStage(); };")
        sb.AppendLine("window.externalScroll=function(dx,dy){ viewport.scrollLeft += dx; viewport.scrollTop += dy; };")
        sb.AppendLine("window.externalRotate=function(delta){ rotation=(rotation+delta+360)%360; closeEditor(); fitStage(); };")
        sb.AppendLine("window.addEventListener('resize', fitStage);")
        sb.AppendLine("if(img.complete){ setTimeout(fitStage,50); } else { img.onload=function(){ fitStage(); }; }")
        Dim markerIndex As Integer = 0
        For Each r In rows
            markerIndex += 1
            If r.XPercent > 0D AndAlso r.YPercent > 0D Then
                Dim xText = NumberUtil.DecToCsv(NormalizePercent(r.XPercent))
                Dim yText = NumberUtil.DecToCsv(NormalizePercent(r.YPercent))
                Dim measureId = JsString(r.MeasureId)
                Dim measureName = JsString(r.MeasureName)
                Dim nominal = JsString(NumberUtil.DecToCsv(r.Nominal))
                Dim lowerTol = JsString(NumberUtil.DecToCsv(r.LowerTol))
                Dim upperTol = JsString(NumberUtil.DecToCsv(r.UpperTol))
                Dim unit = JsString(r.Unit)
                Dim valueText = JsString(r.MeasuredValueText)
                Dim groupText = JsString(If(String.IsNullOrWhiteSpace(r.MeasurementGroup), "Genel", r.MeasurementGroup.Trim()))
                Dim balloonNo As Integer = If(r.SortNo > 0, r.SortNo, markerIndex)
                Dim shortCaption As String = balloonNo.ToString()
                Dim statusClass = GetMarkerStatusClass(r)
                Dim criticalClass = If(String.Equals(r.IsCritical, "YES", StringComparison.OrdinalIgnoreCase), " critical", "")
                sb.AppendLine("(function(){")
                sb.AppendLine("var btn=document.createElement('button');")
                sb.AppendLine("btn.type='button';")
                sb.AppendLine("btn.className='marker " & statusClass & criticalClass & "';")
                sb.AppendLine("btn.dataset.x='" & xText & "';")
                sb.AppendLine("btn.dataset.y='" & yText & "';")
                sb.AppendLine("btn.dataset.id='" & measureId & "';")
                sb.AppendLine("btn.dataset.name='" & measureName & "';")
                sb.AppendLine("btn.dataset.nominal='" & nominal & "';")
                sb.AppendLine("btn.dataset.lowertol='" & lowerTol & "';")
                sb.AppendLine("btn.dataset.uppertol='" & upperTol & "';")
                sb.AppendLine("btn.dataset.unit='" & unit & "';")
                sb.AppendLine("btn.dataset.value='" & valueText & "';")
                sb.AppendLine("btn.dataset.group='" & groupText & "';")
                sb.AppendLine("btn.dataset.critical='" & If(String.Equals(r.IsCritical, "YES", StringComparison.OrdinalIgnoreCase), "true", "false") & "';")
                sb.AppendLine("btn.title='Balon " & shortCaption & " | Ölçü No: " & measureId & " | " & measureName & "';")
                sb.AppendLine("btn.textContent='" & shortCaption & "';")
                sb.AppendLine("btn.addEventListener('click', function(ev){ ev.preventDefault(); ev.stopPropagation(); openEditorWithFocus(this); if(window.chrome && chrome.webview){ chrome.webview.postMessage('select|' + this.dataset.id); } });")
                sb.AppendLine("overlay.appendChild(btn); markers['" & measureId & "']=btn; updateMarkerPosition(btn);})();")
            End If
        Next
        sb.AppendLine("function openEditor(btn){ activeId=btn.dataset.id; edTitle.textContent='Ölçü No: '+btn.dataset.id; edName.textContent='Ölçü: '+(btn.dataset.name||''); edNominal.textContent=(btn.dataset.nominal||'')+' '+(btn.dataset.unit||''); edLowerTol.textContent=(btn.dataset.lowertol||'')+' '+(btn.dataset.unit||''); edUpperTol.textContent=(btn.dataset.uppertol||'')+' '+(btn.dataset.unit||''); edValue.value=btn.dataset.value||''; const r=btn.getBoundingClientRect(); if((r.top+r.height/2)>window.innerHeight/2){ ed.classList.add('top'); } else { ed.classList.remove('top'); } ed.style.display='flex'; setTimeout(function(){ edValue.focus(); edValue.select(); },0); }")
        sb.AppendLine("function closeEditor(){ ed.style.display='none'; activeId=''; }")
        sb.AppendLine("function saveEditor(){ if(!activeId) return; if(window.chrome && chrome.webview){ chrome.webview.postMessage('save|' + activeId + '|' + edValue.value); } closeEditor(); }")
        sb.AppendLine("window.saveEditorIfOpen=function(){ if(ed.style.display!=='none' && activeId){ saveEditor(); } };")
        sb.AppendLine("function normalizeMeasurementInput(v){ let s=(v||'').trim(); if(!s) return ''; s=s.replace(/[Çç,]/g,'.').replace(/\s+/g,'').replace(/[^0-9+\-.]/g,''); let sign=''; if(s.startsWith('-')) sign='-'; s=s.replace(/[+\-]/g,''); if(!s) return sign; const last=s.lastIndexOf('.'); let intPart=s, fracPart='', hasDecimal=last>=0; if(hasDecimal){ intPart=s.substring(0,last).replace(/\./g,''); fracPart=s.substring(last+1).replace(/\./g,''); } else { intPart=s.replace(/\./g,''); } intPart=intPart.replace(/^0+(?=\d)/,''); if(!intPart) intPart='0'; return sign + intPart + (hasDecimal ? '.' + fracPart : ''); }")
        sb.AppendLine("function isLikelyCaliperPayload(v){ const s=(v||'').trim(); return /[Çç]/.test(s) || /^[+\-]\d{2,}[\.,]\d+$/.test(s); }")
        sb.AppendLine("let caliperAutoSaveTimer=null;")
        sb.AppendLine("edValue.addEventListener('input', function(){ const raw=edValue.value; if(!isLikelyCaliperPayload(raw)) return; const normalized=normalizeMeasurementInput(raw); if(normalized && normalized!==raw){ edValue.value=normalized; } if(caliperAutoSaveTimer){ clearTimeout(caliperAutoSaveTimer); } caliperAutoSaveTimer=setTimeout(function(){ if(activeId && ed.style.display!=='none'){ saveEditor(); } }, 120); });")
        sb.AppendLine("edValue.addEventListener('keydown', function(ev){ if(ev.key==='Enter'){ saveEditor(); } if(ev.key==='Escape'){ closeEditor(); } });")
        sb.AppendLine("function focusMarker(btn){ if(!btn) return; if(zoom<1.8){ applyZoom(1.8); } setTimeout(function(){ const stageLeft=parseFloat(stage.style.left)||0; const stageTop=parseFloat(stage.style.top)||0; viewport.scrollLeft=Math.max(0,stageLeft+btn.offsetLeft-(viewport.clientWidth/2)); viewport.scrollTop=Math.max(0,stageTop+btn.offsetTop-(viewport.clientHeight/2)); },20); }")
        sb.AppendLine("function openEditorWithFocus(btn){ if(!btn) return; focusMarker(btn); setTimeout(function(){ openEditor(btn); },70); }")
        sb.AppendLine("window.highlightMeasure=function(id,focus){ selectedId=id||''; Object.keys(markers).forEach(function(k){ markers[k].classList.toggle('sel', k===selectedId); }); if(focus && markers[selectedId]){ focusMarker(markers[selectedId]); } };")
        sb.AppendLine("window.filterMarkers=function(mode,value){ const wanted=(value||'').toLocaleLowerCase('tr-TR'); Object.keys(markers).forEach(function(k){ const m=markers[k]; const visible=(mode==='all')||(mode==='critical'&&m.dataset.critical==='true')||(mode==='group'&&(m.dataset.group||'').toLocaleLowerCase('tr-TR')===wanted); m.style.display=visible?'':'none'; }); };")
        sb.AppendLine("window.clearGroupArea=function(){groupAreaData=null;updateGroupFocus();};")
        sb.AppendLine("window.focusGroupArea=function(l,t,r,b,name){l=parseFloat(l)||0;t=parseFloat(t)||0;r=parseFloat(r)||0;b=parseFloat(b)||0;groupAreaData={l:l,t:t,r:r,b:b,name:name||''};const w=Math.max(2,r-l),h=Math.max(2,b-t);const target=Math.max(1.2,Math.min(4,Math.min(78/w,68/h)));applyZoom(target);setTimeout(function(){updateGroupFocus();const c=origToStage((l+r)/2,(t+b)/2);const sx=stage.clientWidth*c.x/100+(parseFloat(stage.style.left)||0);const sy=stage.clientHeight*c.y/100+(parseFloat(stage.style.top)||0);viewport.scrollLeft=Math.max(0,sx-viewport.clientWidth/2);viewport.scrollTop=Math.max(0,sy-viewport.clientHeight/2);},30);};")
        sb.AppendLine("window.setMarkerStatus=function(id,status,value){ if(!markers[id]) return; markers[id].classList.remove('ok','nok','empty','bad'); markers[id].classList.add(status||'empty'); markers[id].dataset.value=value||''; };")
        sb.AppendLine("window.openMeasureEditor=function(id){ if(markers[id]){ openEditorWithFocus(markers[id]); } };")
        sb.AppendLine("// Sabit ölçüm çubuğu çizime tıklanınca kapanmaz; Bluetooth kumpas odağı korunur.")
        sb.AppendLine("</script></body></html>")
        Return sb.ToString()
    End Function

    Private Function JsString(value As String) As String
        If value Is Nothing Then Return ""
        Return value.Replace("\", "\\").Replace("'", "\'").Replace(vbCr, "").Replace(vbLf, "\n")
    End Function

    Private Function GetMarkerStatusClass(r As MeasurementRow) As String
        If String.Equals(r.Result, "OK", StringComparison.OrdinalIgnoreCase) Then Return "ok"
        If String.Equals(r.Result, "NOK", StringComparison.OrdinalIgnoreCase) Then Return "nok"
        If String.Equals(r.Result, "HATALI", StringComparison.OrdinalIgnoreCase) Then Return "bad"
        Return "empty"
    End Function

    Private Sub PdfNavigationCompleted(sender As Object, e As CoreWebView2NavigationCompletedEventArgs)
        RefreshMarkerStatuses()
        RefreshPdfMarkerFilter()
        RefreshPdfSelection()
    End Sub

    Private Sub PdfMessageReceived(sender As Object, e As CoreWebView2WebMessageReceivedEventArgs)
        Try
            Dim msg As String = e.TryGetWebMessageAsString()
            If String.IsNullOrWhiteSpace(msg) Then Return

            If msg.StartsWith("zoom|", StringComparison.OrdinalIgnoreCase) Then
                Dim z As Integer = 100
                If Integer.TryParse(msg.Substring(5), z) Then
                    currentPdfZoom = z
                    UpdateZoomLabel()
                End If
                Return
            End If

            If msg.StartsWith("select|", StringComparison.OrdinalIgnoreCase) Then
                Dim measureId As String = msg.Substring(7)
                SelectMeasureInGrid(measureId)
                Return
            End If

            If msg.StartsWith("save|", StringComparison.OrdinalIgnoreCase) Then
                Dim payload As String = msg.Substring(5)
                Dim idx As Integer = payload.IndexOf("|"c)
                If idx > -1 Then
                    Dim measureId As String = payload.Substring(0, idx)
                    Dim valueText As String = payload.Substring(idx + 1)
                    ApplyMeasuredValue(measureId, valueText)
                End If
                Return
            End If
        Catch ex As Exception
            ErrorLogService.Log("FrmMeasurementEntry.HandleWebMessage", ex)
        End Try
    End Sub

    Private Sub ApplyMeasuredValue(measureId As String, valueText As String)
        For i As Integer = 0 To rows.Count - 1
            If String.Equals(rows(i).MeasureId, measureId, StringComparison.OrdinalIgnoreCase) Then
                Dim normalizedValue = NumberUtil.NormalizeMeasurementInput(valueText)
                rows(i).MeasuredValueText = If(normalizedValue <> "", normalizedValue, If(valueText, "").Trim())
                RecalculateRow(rows(i))
                grid.Refresh()
                SelectMeasureInGrid(measureId)
                RefreshMarkerStatuses()
                StoreEyeState(GetCurrentEyeNo())
                UpdateMeasurementProgress()
                ScheduleDraftSave()
                If chkAutoNextMeasure.Checked AndAlso
                   Not String.IsNullOrWhiteSpace(rows(i).MeasuredValueText) AndAlso
                   Not String.Equals(rows(i).Result, "HATALI", StringComparison.OrdinalIgnoreCase) Then
                    SelectNextPendingMeasure(i)
                End If
                Exit For
            End If
        Next
    End Sub

    Private Sub SelectNextPendingMeasure(currentIndex As Integer)
        If rows.Count = 0 Then Return

        For offset As Integer = 1 To rows.Count
            Dim index = (currentIndex + offset) Mod rows.Count
            If Not IsRowInSelectedGroup(rows(index)) Then Continue For
            If Not String.IsNullOrWhiteSpace(rows(index).MeasuredValueText) Then Continue For
            Dim nextMeasureId = rows(index).MeasureId
            SelectMeasureInGrid(nextMeasureId)
            lblPdfInfo.Text = "Sıradaki ölçüye geçildi: " & nextMeasureId & "   |   Bluetooth kumpas değeri için giriş kutusu hazır."
            BeginInvoke(CType(Sub() OpenEditorForMeasure(nextMeasureId), MethodInvoker))
            Return
        Next

        lblPdfInfo.Text = "Seçili gruptaki ölçüler tamamlandı."
    End Sub

    Private Sub SelectMeasureInGrid(measureId As String)
        If String.IsNullOrWhiteSpace(measureId) Then Return
        For i As Integer = 0 To rows.Count - 1
            If String.Equals(rows(i).MeasureId, measureId, StringComparison.OrdinalIgnoreCase) Then
                If i >= 0 AndAlso i < grid.Rows.Count Then
                    If Not grid.Rows(i).Visible AndAlso cboMeasurementGroup.Items.Count > 0 Then
                        cboMeasurementGroup.SelectedIndex = 0
                    End If
                    grid.ClearSelection()
                    grid.Rows(i).Selected = True
                    grid.CurrentCell = grid.Rows(i).Cells(0)
                    If i >= 0 Then grid.FirstDisplayedScrollingRowIndex = Math.Max(0, i)
                End If
                Exit For
            End If
        Next
        RefreshPdfSelection()
    End Sub

    Private Sub OpenEditorForMeasure(measureId As String)
        Try
            If pdfViewer.CoreWebView2 Is Nothing Then Return
            Dim script As String = "if(window.openMeasureEditor){window.openMeasureEditor('" & JsString(measureId) & "');}"
            pdfViewer.CoreWebView2.ExecuteScriptAsync(script)
        Catch ex As Exception
            ErrorLogService.Log("FrmMeasurementEntry.OpenEditorForMeasure", ex, "MeasureId=" & If(measureId, ""))
        End Try
    End Sub

    Private Sub RefreshPdfSelection(Optional focusSelectedMarker As Boolean = False)
        Try
            If pdfViewer.CoreWebView2 Is Nothing Then Return
            Dim measureId As String = GetSelectedMeasureId()
            Dim focusText As String = If(focusSelectedMarker, "true", "false")
            Dim script As String = "if(window.highlightMeasure){window.highlightMeasure('" & JsString(measureId) & "'," & focusText & ");}"
            pdfViewer.CoreWebView2.ExecuteScriptAsync(script)
        Catch ex As Exception
            ErrorLogService.Log("FrmMeasurementEntry.HighlightSelectedMeasure", ex)
        End Try
    End Sub

    Private Function GetSelectedMeasureId() As String
        If grid.SelectedRows Is Nothing OrElse grid.SelectedRows.Count = 0 Then Return ""
        Dim rowIndex As Integer = grid.SelectedRows(0).Index
        If rowIndex < 0 OrElse rowIndex >= rows.Count Then Return ""
        Return rows(rowIndex).MeasureId
    End Function

    Private Sub RefreshMarkerStatuses()
        Try
            If pdfViewer.CoreWebView2 Is Nothing Then Return
            For Each r In rows
                If r.XPercent > 0D AndAlso r.YPercent > 0D Then
                    Dim statusClass = GetMarkerStatusClass(r)
                    Dim valueText = JsString(r.MeasuredValueText)
                    Dim script As String = "if(window.setMarkerStatus){window.setMarkerStatus('" & JsString(r.MeasureId) & "','" & statusClass & "','" & valueText & "');}"
                    pdfViewer.CoreWebView2.ExecuteScriptAsync(script)
                End If
            Next
        Catch ex As Exception
            ErrorLogService.Log("FrmMeasurementEntry.RefreshMarkerStatuses", ex)
        End Try
    End Sub

    Private Sub RefreshPdfMarkerFilter()
        Try
            If pdfViewer.CoreWebView2 Is Nothing Then Return

            Dim mode = "all"
            Dim value = ""
            If cboMeasurementGroup.SelectedIndex = 1 Then
                mode = "critical"
            ElseIf cboMeasurementGroup.SelectedIndex > 1 Then
                mode = "group"
                value = If(TryCast(cboMeasurementGroup.SelectedItem, String), "")
            End If

            Dim script = "if(window.filterMarkers){window.filterMarkers('" & mode & "','" & JsString(value) & "');}"
            If mode = "group" Then
                Dim area = measurementGroupAreas.FirstOrDefault(
                    Function(item) String.Equals(item.GroupName, value, StringComparison.OrdinalIgnoreCase))
                If area IsNot Nothing Then
                    script &= "if(window.focusGroupArea){window.focusGroupArea(" &
                              NumberUtil.DecToCsv(area.LeftPercent).Replace(",", ".") & "," &
                              NumberUtil.DecToCsv(area.TopPercent).Replace(",", ".") & "," &
                              NumberUtil.DecToCsv(area.RightPercent).Replace(",", ".") & "," &
                              NumberUtil.DecToCsv(area.BottomPercent).Replace(",", ".") & ",'" &
                              JsString(area.GroupName) & "');}"
                Else
                    script &= "if(window.clearGroupArea){window.clearGroupArea();}"
                End If
            Else
                script &= "if(window.clearGroupArea){window.clearGroupArea();}"
            End If
            pdfViewer.CoreWebView2.ExecuteScriptAsync(script)
        Catch ex As Exception
            ErrorLogService.Log("FrmMeasurementEntry.FilterPdfMarkers", ex)
        End Try
    End Sub

    Private Sub ScheduleDraftSave()
        If suppressDraftSave OrElse measurementDataCommitted OrElse IsDisposed Then Return
        draftSaveTimer.Stop()
        SaveMeasurementDraftNow()
    End Sub

    Private Sub DraftSaveTimer_Tick(sender As Object, e As EventArgs)
        draftSaveTimer.Stop()
        SaveMeasurementDraftNow()
    End Sub

    Private Function TryRestoreMeasurementDraft() As Boolean
        Dim p = SelectedProduct()
        If p Is Nothing OrElse rows.Count = 0 Then Return False

        Dim draft = MeasurementDraftService.Load(AppState.CurrentUserName, p.TrCode, p.DrawingRev, p.DrawingScope)
        If draft Is Nothing Then Return False

        Dim draftKey = (p.TrCode & "|" & p.DrawingRev & "|" & ProductInfo.NormalizeDrawingScope(p.DrawingScope)).ToUpperInvariant()
        If String.Equals(lastDraftPromptKey, draftKey, StringComparison.OrdinalIgnoreCase) Then
            RestoreMeasurementDraft(draft)
            Return True
        End If

        lastDraftPromptKey = draftKey
        Dim answer = MessageBox.Show(
            "Bu ürün için tamamlanmamış bir ölçüm taslağı bulundu." & Environment.NewLine &
            "Kaydedilme zamanı: " & draft.SavedAt.ToString("dd.MM.yyyy HH:mm:ss") & Environment.NewLine &
            "İş Emri No: " & If(String.IsNullOrWhiteSpace(draft.LotNo), "-", draft.LotNo) & Environment.NewLine &
            "Seri No: " & If(String.IsNullOrWhiteSpace(draft.SerialNo), "-", draft.SerialNo) & Environment.NewLine & Environment.NewLine &
            "Kaldığınız yerden devam etmek ister misiniz?" & Environment.NewLine &
            "Hayır seçilirse bu taslak silinir.",
            "Ölçüm taslağı bulundu",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question)

        If answer = DialogResult.Yes Then
            RestoreMeasurementDraft(draft)
            Return True
        End If

        MeasurementDraftService.Delete(AppState.CurrentUserName, p.TrCode, p.DrawingRev, p.DrawingScope)
        lblDraftInfo.Text = ""
        Return False
    End Function

    Private Sub RestoreMeasurementDraft(draft As MeasurementDraft)
        If draft Is Nothing Then Return

        suppressDraftSave = True
        Try
            txtLot.Text = draft.LotNo
            txtSerial.Text = draft.SerialNo
            txtEyeCount.Text = Math.Max(1, draft.EyeCount).ToString()
            txtEyeNo.Text = Math.Max(1, Math.Min(draft.EyeNo, Math.Max(1, draft.EyeCount))).ToString()

            ClearEyeBuffers()
            For Each draftEye In draft.Eyes
                If draftEye Is Nothing OrElse draftEye.EyeNo <= 0 Then Continue For

                Dim state As New Dictionary(Of String, EyeMeasureState)(StringComparer.OrdinalIgnoreCase)
                For Each draftValue In draftEye.Values
                    If draftValue Is Nothing OrElse String.IsNullOrWhiteSpace(draftValue.MeasureId) Then Continue For
                    state(draftValue.MeasureId) = New EyeMeasureState With {
                        .MeasuredValueText = draftValue.MeasuredValueText,
                        .Result = draftValue.Result,
                        .Note = draftValue.Note
                    }
                Next

                eyeBuffers(draftEye.EyeNo) = state
                eyeClosedStates(draftEye.EyeNo) = draftEye.IsClosed
            Next

            RestoreEyeState(GetCurrentEyeNo())
            If Not String.IsNullOrWhiteSpace(draft.SelectedMeasureId) Then
                SelectMeasureInGrid(draft.SelectedMeasureId)
            End If
            lblDraftInfo.Text = "Taslak geri yüklendi"
        Finally
            suppressDraftSave = False
        End Try
    End Sub

    Private Sub SaveMeasurementDraftNow()
        If suppressDraftSave OrElse measurementDataCommitted OrElse rows.Count = 0 Then Return

        Dim p = SelectedProduct()
        If p Is Nothing Then Return

        Try
            StoreEyeState(GetCurrentEyeNo())

            Dim hasDraftData =
                Not String.IsNullOrWhiteSpace(txtLot.Text) OrElse
                Not String.IsNullOrWhiteSpace(txtSerial.Text) OrElse
                eyeClosedStates.Values.Any(Function(value) value) OrElse
                eyeBuffers.Values.Any(
                    Function(state) state.Values.Any(
                        Function(value) value IsNot Nothing AndAlso
                                         (Not String.IsNullOrWhiteSpace(value.MeasuredValueText) OrElse
                                          Not String.IsNullOrWhiteSpace(value.Note))))

            If Not hasDraftData Then
                MeasurementDraftService.Delete(AppState.CurrentUserName, p.TrCode, p.DrawingRev, p.DrawingScope)
                lblDraftInfo.Text = ""
                Return
            End If

            Dim draft As New MeasurementDraft With {
                .UserName = AppState.CurrentUserName,
                .ComputerName = Environment.MachineName,
                .TrCode = p.TrCode,
                .DrawingRev = p.DrawingRev,
                .DrawingScope = ProductInfo.NormalizeDrawingScope(p.DrawingScope),
                .LotNo = txtLot.Text.Trim(),
                .SerialNo = txtSerial.Text.Trim(),
                .EyeCount = GetEyeCount(),
                .EyeNo = GetCurrentEyeNo(),
                .SelectedMeasureId = GetSelectedMeasureId(),
                .SavedAt = DateTime.Now
            }

            For eyeNo As Integer = 1 To draft.EyeCount
                Dim state = GetEyeState(eyeNo)
                Dim draftEye As New MeasurementDraftEye With {
                    .EyeNo = eyeNo,
                    .IsClosed = IsEyeClosed(eyeNo)
                }

                For Each item In state
                    If item.Value Is Nothing Then Continue For
                    If String.IsNullOrWhiteSpace(item.Value.MeasuredValueText) AndAlso
                       String.IsNullOrWhiteSpace(item.Value.Note) Then Continue For

                    draftEye.Values.Add(New MeasurementDraftValue With {
                        .MeasureId = item.Key,
                        .MeasuredValueText = item.Value.MeasuredValueText,
                        .Result = item.Value.Result,
                        .Note = item.Value.Note
                    })
                Next
                draft.Eyes.Add(draftEye)
            Next

            MeasurementDraftService.Save(draft)
            lblDraftInfo.Text = "Taslak: " & draft.SavedAt.ToString("HH:mm:ss")
        Catch ex As Exception
            ErrorLogService.Log("FrmMeasurementEntry.SaveDraft", ex)
            lblDraftInfo.Text = "Taslak kaydedilemedi"
        End Try
    End Sub

    Private Sub DeleteCurrentMeasurementDraft()
        draftSaveTimer.Stop()
        Dim p = SelectedProduct()
        If p Is Nothing Then Return
        MeasurementDraftService.Delete(AppState.CurrentUserName, p.TrCode, p.DrawingRev, p.DrawingScope)
        lblDraftInfo.Text = ""
    End Sub

    Private Function BuildRecordId(prefix As String) As String
        Dim pfx = If(String.IsNullOrWhiteSpace(prefix), "", prefix.Trim().ToUpperInvariant() & "-")
        Return DateTime.Now.ToString("yyyyMMdd-HHmmss") & "-" & pfx & Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant()
    End Function

    Private Function IsEyeClosed(eyeNo As Integer) As Boolean
        Return eyeClosedStates.ContainsKey(eyeNo) AndAlso eyeClosedStates(eyeNo)
    End Function

    Private Function GetEyeState(eyeNo As Integer) As Dictionary(Of String, EyeMeasureState)
        Dim dict As Dictionary(Of String, EyeMeasureState) = Nothing
        If eyeBuffers.TryGetValue(eyeNo, dict) Then Return dict
        Return New Dictionary(Of String, EyeMeasureState)(StringComparer.OrdinalIgnoreCase)
    End Function

    Private Function GetStateValue(state As Dictionary(Of String, EyeMeasureState), measureId As String) As String
        If state IsNot Nothing AndAlso state.ContainsKey(measureId) AndAlso state(measureId) IsNot Nothing Then
            Return If(state(measureId).MeasuredValueText, "")
        End If
        Return ""
    End Function

    Private Function GetStateNote(state As Dictionary(Of String, EyeMeasureState), measureId As String) As String
        If state IsNot Nothing AndAlso state.ContainsKey(measureId) AndAlso state(measureId) IsNot Nothing Then
            Return If(state(measureId).Note, "")
        End If
        Return ""
    End Function

    Private Function CalculateResultForValue(r As MeasurementRow, measuredValueText As String) As String
        If measuredValueText Is Nothing OrElse measuredValueText.Trim() = "" Then Return ""

        Dim measured As Decimal
        If Not NumberUtil.TryParseDecimal(measuredValueText, measured) Then Return "HATALI"

        If measured >= r.LowerLimit AndAlso measured <= r.UpperLimit Then
            Return "OK"
        End If

        Return "NOK"
    End Function

    Private Function ValidateAllEyesBeforeSave(eyeCount As Integer) As Boolean
        For eyeNo As Integer = 1 To eyeCount
            If IsEyeClosed(eyeNo) Then Continue For

            Dim state = GetEyeState(eyeNo)
            Dim missing As New List(Of String)()
            Dim invalid As New List(Of String)()

            For Each r In rows
                Dim valueText = GetStateValue(state, r.MeasureId).Trim()
                Dim resultText = CalculateResultForValue(r, valueText)

                If String.Equals(r.IsMandatory, "YES", StringComparison.OrdinalIgnoreCase) AndAlso valueText = "" Then
                    missing.Add(r.MeasureId)
                End If

                If valueText <> "" AndAlso String.Equals(resultText, "HATALI", StringComparison.OrdinalIgnoreCase) Then
                    invalid.Add(r.MeasureId)
                End If
            Next

            If missing.Count > 0 Then
                txtEyeNo.Text = eyeNo.ToString()
                RestoreEyeState(eyeNo)
                MessageBox.Show($"Göz {eyeNo}/{eyeCount} için zorunlu ölçüler boş bırakılamaz: " & String.Join(", ", missing),
                                "Eksik ölçüm", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return False
            End If

            If invalid.Count > 0 Then
                txtEyeNo.Text = eyeNo.ToString()
                RestoreEyeState(eyeNo)
                MessageBox.Show($"Göz {eyeNo}/{eyeCount} için sayısal olmayan ölçüm değeri var: " & String.Join(", ", invalid),
                                "Hatalı değer", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return False
            End If
        Next

        Return True
    End Function

    Private Sub CloseLinkedProductionTicketAfterMeasurement(p As ProductInfo)
        If String.IsNullOrWhiteSpace(linkedProductionTicketId) Then Return

        Try
            Dim closeNote = "Ticket üzerinden ölçüm girişi tamamlandı. Otomatik kapatıldı."
            DataService.CloseProductionTicket(linkedProductionTicketId, AppState.CurrentUserName, closeNote)
            AuditService.Log("PRODUCTION_TICKET_AUTO_CLOSE_AFTER_MEASUREMENT",
                             If(p Is Nothing, "", p.TrCode),
                             If(p Is Nothing, "", p.DrawingRev),
                             "TicketId=" & linkedProductionTicketId)
        Catch ex As Exception
            MessageBox.Show("Ölçüm kaydı tamamlandı ancak ticket otomatik kapatılamadı." & Environment.NewLine & ex.Message,
                            "Ticket kapatma uyarısı", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Function BuildQualityToProductionTicketId() As String
        Return "URT-" & DateTime.Now.ToString("yyyyMMdd-HHmmss") & "-" & Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant()
    End Function

    Private Function IsVisualNotOkResult(resultText As String) As Boolean
        Dim t = If(resultText, "").Trim().ToUpperInvariant()
        Return t = "UYGUN DEĞİL" OrElse t = "UYGUNSUZ" OrElse t = "NOK" OrElse t.Contains("DEĞİL")
    End Function

    Private Function CreateQualityToProductionTicketIfNeeded(p As ProductInfo,
                                                             recordId As String,
                                                             lotText As String,
                                                             serialText As String,
                                                             eyeCountText As String,
                                                             eyeNoText As String) As Boolean
        If p Is Nothing OrElse String.IsNullOrWhiteSpace(recordId) Then Return False

        If DataService.QualityToProductionTicketExistsForRecord(recordId) Then
            Return False
        End If

        Dim measurementRows = DataService.GetMeasurementRows().
            Where(Function(r) String.Equals(DataService.GetValue(r, "RecordId"), recordId, StringComparison.OrdinalIgnoreCase)).
            ToList()

        Dim visualRows = DataService.GetVisualControlRows().
            Where(Function(r) String.Equals(DataService.GetValue(r, "RecordId"), recordId, StringComparison.OrdinalIgnoreCase)).
            ToList()

        Dim nokMeasurementRows = measurementRows.
            Where(Function(r) String.Equals(DataService.GetValue(r, "Result"), "NOK", StringComparison.OrdinalIgnoreCase)).
            ToList()

        Dim nokVisualRows = visualRows.
            Where(Function(r) IsVisualNotOkResult(DataService.GetValue(r, "Result"))).
            ToList()

        If nokMeasurementRows.Count = 0 AndAlso nokVisualRows.Count = 0 Then Return False

        Dim details As New List(Of String)()

        For Each r In nokMeasurementRows.Take(5)
            Dim measureName = DataService.GetValue(r, "MeasureName")
            If measureName.Trim() = "" Then measureName = DataService.GetValue(r, "MeasureId")
            details.Add("Ölçüm NOK: " & measureName &
                        " = " & DataService.GetValue(r, "MeasuredValue") &
                        " / Limit: " & DataService.GetValue(r, "LowerLimit") &
                        " - " & DataService.GetValue(r, "UpperLimit"))
        Next

        For Each r In nokVisualRows.Take(5)
            Dim noteText = DataService.GetValue(r, "Note")
            Dim part = "Görsel uygunsuz: " & DataService.GetValue(r, "ControlName") &
                       " = " & DataService.GetValue(r, "Result")
            If noteText.Trim() <> "" Then part &= " / Not: " & noteText
            details.Add(part)
        Next

        If nokMeasurementRows.Count + nokVisualRows.Count > details.Count Then
            details.Add("Ek uygunsuzluk sayısı: " & ((nokMeasurementRows.Count + nokVisualRows.Count) - details.Count).ToString())
        End If

        Dim summary = "Ölçüm NOK: " & nokMeasurementRows.Count.ToString() &
                      "; Görsel uygunsuz: " & nokVisualRows.Count.ToString() &
                      If(details.Count > 0, " | " & String.Join(" | ", details), "")

        Dim row As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {"TicketId", BuildQualityToProductionTicketId()},
            {"Status", "OPEN"},
            {"CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")},
            {"CreatedBy", AppState.CurrentUserName},
            {"ComputerName", Environment.MachineName},
            {"TrCode", p.TrCode},
            {"DrawingRev", p.DrawingRev},
            {"ProductName", p.ProductName},
            {"LotNo", lotText},
            {"SerialNo", serialText},
            {"EyeCount", eyeCountText},
            {"EyeNo", eyeNoText},
            {"RecordId", recordId},
            {"SourceQualityTicketId", linkedProductionTicketId},
            {"SourceType", "KALITE_KONTROL_UYGUNSUZLUK"},
            {"IssueSummary", summary},
            {"MeasurementNokCount", nokMeasurementRows.Count.ToString()},
            {"VisualNokCount", nokVisualRows.Count.ToString()},
            {"SeenByProduction", ""},
            {"SeenAt", ""},
            {"ClosedBy", ""},
            {"ClosedAt", ""},
            {"CloseNote", ""}
        }

        DataService.AppendQualityToProductionTicket(row)
        AuditService.Log("QUALITY_TO_PRODUCTION_TICKET_CREATE", p.TrCode, p.DrawingRev, "RecordId=" & recordId & "; " & summary)
        Return True
    End Function

    Private Async Sub Save_Click(sender As Object, e As EventArgs)
        Try
            Dim p = SelectedProduct()
            If p Is Nothing Then Return
            If Not String.IsNullOrWhiteSpace(initialDrawingScope) AndAlso
               Not String.Equals(
                   ProductInfo.NormalizeDrawingScope(p.DrawingScope),
                   initialDrawingScope,
                   StringComparison.OrdinalIgnoreCase) Then
                MessageBox.Show(
                    "Bu pencerede yalnızca " & initialDrawingScope & " kapsamındaki teknik resimlerle ölçüm kaydı oluşturulabilir.",
                    "Resim kapsamı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning)
                Return
            End If
            If rows.Count = 0 Then
                MessageBox.Show("Bu TR için kontrol ölçüsü tanımlanmamış.", "Ölçü yok", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            If Not ValidateEyeInputs() Then Return

            Await CommitOpenPdfEditorAsync()

            Dim currentEyeNo As Integer = GetCurrentEyeNo()
            Dim currentEyeCount As Integer = GetEyeCount()

            ' Aktif gözde ekranda görünen son değerleri önce hafızaya al.
            CommitGridAndStoreEyeState(currentEyeNo)

            ' Tek tuşla tüm gözler kaydedileceği için tüm gözlerin ölçüleri önceden kontrol edilir.
            If Not ValidateAllEyesBeforeSave(currentEyeCount) Then Return

            Dim lotText = txtLot.Text.Trim()
            Dim serialText = txtSerial.Text.Trim()
            Dim savedMeasurementEyeCount As Integer = 0
            Dim savedClosedEyeCount As Integer = 0
            Dim savedMeasurementRowCount As Integer = 0
            Dim visualRecords As New List(Of Tuple(Of Integer, String))()

            For eyeNo As Integer = 1 To currentEyeCount
                If IsEyeClosed(eyeNo) Then
                    Dim closedRecordId = BuildRecordId("KAPALI")
                    Dim closedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")

                    Dim closedRow As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                        {"RecordId", closedRecordId},
                        {"TrCode", p.TrCode},
                        {"DrawingRev", p.DrawingRev},
                        {"DrawingScope", ProductInfo.NormalizeDrawingScope(p.DrawingScope)},
                        {"LotNo", lotText},
                        {"SerialNo", serialText},
                        {"EyeCount", currentEyeCount.ToString()},
                        {"EyeNo", eyeNo.ToString()},
                        {"OperatorName", AppState.CurrentUserName},
                        {"ComputerName", Environment.MachineName},
                        {"ClosedDate", closedDate},
                        {"Reason", "Göz Kapalı"},
                        {"ProductionTicketId", linkedProductionTicketId},
                        {"CommissioningId", linkedCommissioningId}
                    }

                    DataService.AppendClosedEye(closedRow)
                    AuditService.Log("EYE_CLOSED_SKIP", p.TrCode, p.DrawingRev, $"RecordId={closedRecordId}; EyeNo={eyeNo}/{currentEyeCount}; İşEmriNo={lotText}; Serial={serialText}")
                    savedClosedEyeCount += 1
                    Continue For
                End If

                Dim state = GetEyeState(eyeNo)
                Dim recordId = BuildRecordId("")
                Dim dt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                Dim nokCount As Integer = 0
                Dim eyeRowCount As Integer = 0

                For Each r In rows
                    Dim valueText = GetStateValue(state, r.MeasureId).Trim()
                    If valueText = "" Then Continue For

                    Dim resultText = CalculateResultForValue(r, valueText)
                    Dim measured As Decimal = 0D
                    NumberUtil.TryParseDecimal(valueText, measured)

                    Dim csvRow As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                        {"RecordId", recordId},
                        {"TrCode", p.TrCode},
                        {"DrawingRev", p.DrawingRev},
                        {"DrawingScope", ProductInfo.NormalizeDrawingScope(p.DrawingScope)},
                        {"LotNo", lotText},
                        {"SerialNo", serialText},
                        {"EyeCount", currentEyeCount.ToString()},
                        {"EyeNo", eyeNo.ToString()},
                        {"OperatorName", AppState.CurrentUserName},
                        {"ComputerName", Environment.MachineName},
                        {"MeasurementDate", dt},
                        {"MeasureId", r.MeasureId},
                        {"MeasureName", r.MeasureName},
                        {"MeasurementGroup", r.MeasurementGroup},
                        {"SampleFrequency", r.SampleFrequency},
                        {"IsCritical", r.IsCritical},
                        {"SortNo", r.SortNo.ToString()},
                        {"Nominal", NumberUtil.DecToCsv(r.Nominal)},
                        {"LowerLimit", NumberUtil.DecToCsv(r.LowerLimit)},
                        {"UpperLimit", NumberUtil.DecToCsv(r.UpperLimit)},
                        {"PageNo", r.PageNo.ToString()},
                        {"XPercent", NumberUtil.DecToCsv(r.XPercent)},
                        {"YPercent", NumberUtil.DecToCsv(r.YPercent)},
                        {"MeasuredValue", NumberUtil.DecToCsv(measured)},
                        {"Result", resultText},
                        {"Note", GetStateNote(state, r.MeasureId)},
                        {"ProductionTicketId", linkedProductionTicketId},
                        {"SpcKey", If(String.IsNullOrWhiteSpace(r.SpcKey), r.MeasureId, r.SpcKey)},
                        {"MeasureVersion", Math.Max(1, r.MeasureVersion).ToString()},
                        {"CommissioningId", linkedCommissioningId}
                    }

                    DataService.AppendMeasurement(csvRow)
                    eyeRowCount += 1
                    savedMeasurementRowCount += 1
                    If String.Equals(resultText, "NOK", StringComparison.OrdinalIgnoreCase) Then nokCount += 1
                Next

                savedMeasurementEyeCount += 1
                visualRecords.Add(Tuple.Create(eyeNo, recordId))
                AuditService.Log("MEASUREMENT_SAVE_ALL_EYES", p.TrCode, p.DrawingRev, $"RecordId={recordId}; EyeNo={eyeNo}/{currentEyeCount}; Rows={eyeRowCount}; NOK={nokCount}")
            Next

            measurementDataCommitted = True
            DeleteCurrentMeasurementDraft()

            Dim completedVisualCount As Integer = 0
            For Each item In visualRecords
                Using visualForm As New FrmVisualControl(item.Item2, p.TrCode, p.DrawingRev, lotText, serialText, currentEyeCount.ToString(), item.Item1.ToString(), p.DrawingScope)
                    Dim dialogResult = visualForm.ShowDialog(Me)
                    If dialogResult = DialogResult.OK Then
                        completedVisualCount += 1
                    Else
                        AuditService.Log(
                            "MEASUREMENT_VISUAL_CONTROL_INCOMPLETE",
                            p.TrCode,
                            p.DrawingRev,
                            $"TicketId={linkedProductionTicketId}; RecordId={item.Item2}; EyeNo={item.Item1}/{currentEyeCount}; CompletedVisual={completedVisualCount}/{visualRecords.Count}")

                        MessageBox.Show($"Göz {item.Item1}/{currentEyeCount} için görsel kontrol tamamlanmadı." & Environment.NewLine &
                                        "Ölçüm kayıtları Ölçüm Geçmişi'ne yazıldı; kalan görsel kontroller tamamlanmadı." & Environment.NewLine &
                                        "Kalite ticketı açık bırakıldı ve otomatik kapatılmadı." & Environment.NewLine &
                                        "Kontrolü tamamlamak için ticket üzerinden yeniden kontrol girişi açınız.",
                                        "Görsel kontrol tamamlanmadı", MessageBoxButtons.OK, MessageBoxIcon.Warning)

                        ' Ölçümler yazılmış olsa da tüm görsel kontroller bitmeden kayıt tamamlanmış
                        ' sayılamaz. Aşağıdaki uygunsuzluk ticketı üretme ve kalite ticketını otomatik
                        ' kapatma adımlarına kesinlikle geçme.
                        DialogResult = DialogResult.Cancel
                        Close()
                        Return
                    End If
                End Using
            Next

            Dim createdQualityToProductionTicketCount As Integer = 0
            For Each item In visualRecords
                If CreateQualityToProductionTicketIfNeeded(p, item.Item2, lotText, serialText, currentEyeCount.ToString(), item.Item1.ToString()) Then
                    createdQualityToProductionTicketCount += 1
                End If
            Next

            ClearEyeBuffers()
            RemoveHandler chkEyeClosed.CheckedChanged, AddressOf EyeClosed_CheckedChanged
            chkEyeClosed.Checked = False
            AddHandler chkEyeClosed.CheckedChanged, AddressOf EyeClosed_CheckedChanged
            ResetEyeSequence()
            RestoreEyeState(GetCurrentEyeNo())

            CloseLinkedProductionTicketAfterMeasurement(p)
            If Not String.IsNullOrWhiteSpace(linkedProductionTicketId) Then DialogResult = DialogResult.OK

            MessageBox.Show("Tüm gözler için ölçüm kaydı tamamlandı." & Environment.NewLine &
                            $"Göz Adedi: {currentEyeCount}" & Environment.NewLine &
                            $"Ölçüm kaydedilen göz: {savedMeasurementEyeCount}" & Environment.NewLine &
                            $"Kapalı göz: {savedClosedEyeCount}" & Environment.NewLine &
                            $"Kaydedilen ölçüm satırı: {savedMeasurementRowCount}" & Environment.NewLine &
                            $"Tamamlanan görsel kontrol: {completedVisualCount}/{visualRecords.Count}" & Environment.NewLine &
                            $"Üretime açılan uygunsuzluk ticketı: {createdQualityToProductionTicketCount}",
                            "Kayıt tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information)
            If String.IsNullOrWhiteSpace(linkedProductionTicketId) Then
                measurementDataCommitted = False
            End If
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Kayıt hatası", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub PrevEye_Click(sender As Object, e As EventArgs)
        MoveToEye(GetCurrentEyeNo() - 1)
    End Sub

    Private Sub NextEye_Click(sender As Object, e As EventArgs)
        MoveToEye(GetCurrentEyeNo() + 1)
    End Sub

    Private Async Function CommitOpenPdfEditorAsync() As Task
        Try
            If pdfViewer Is Nothing OrElse pdfViewer.IsDisposed Then Return
            If pdfViewer.CoreWebView2 Is Nothing Then Return

            ' Açık ölçü giriş penceresi varsa, göz değiştirmeden veya kaydetmeden önce otomatik kaydedilir.
            Await pdfViewer.ExecuteScriptAsync("if(window.saveEditorIfOpen){window.saveEditorIfOpen();}")
            Await Task.Delay(80)
        Catch ex As Exception
            ErrorLogService.Log("FrmMeasurementEntry.SaveOpenEditor", ex)
        End Try
    End Function

    Private Sub CommitGridAndStoreEyeState(eyeNo As Integer)
        Try
            grid.EndEdit()
            If grid.DataSource IsNot Nothing Then
                BindingContext(grid.DataSource).EndCurrentEdit()
            End If
        Catch ex As Exception
            ErrorLogService.Log("FrmMeasurementEntry.CommitGridEdits", ex)
        End Try

        For Each r In rows
            RecalculateRow(r)
        Next
        grid.Refresh()
        RefreshMarkerStatuses()
        StoreEyeState(eyeNo)
    End Sub

    Private Async Sub MoveToEye(targetEyeNo As Integer)
        Dim eyeCount As Integer = GetEyeCount()
        Dim currentEyeNo As Integer = GetCurrentEyeNo()

        If targetEyeNo < 1 Then
            MessageBox.Show("İlk gözdesiniz.", "Göz geçişi", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        If targetEyeNo > eyeCount Then
            MessageBox.Show("Son gözdesiniz." & Environment.NewLine &
                            $"Göz Adedi: {eyeCount}",
                            "Göz geçişi", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Await CommitOpenPdfEditorAsync()
        CommitGridAndStoreEyeState(currentEyeNo)

        txtEyeNo.Text = targetEyeNo.ToString()
        RestoreEyeState(targetEyeNo)
        lblPdfInfo.Text = $"Aktif Göz No: {targetEyeNo}/{eyeCount}. Ölçü balonuna tıklayıp değer girebilirsiniz."
        ScheduleDraftSave()
    End Sub

    Private Sub ClearEyeBuffers()
        eyeBuffers.Clear()
        eyeClosedStates.Clear()
    End Sub

    Private Sub StoreEyeState(eyeNo As Integer)
        If eyeNo <= 0 Then Return

        Dim dict As New Dictionary(Of String, EyeMeasureState)(StringComparer.OrdinalIgnoreCase)

        For Each r In rows
            dict(r.MeasureId) = New EyeMeasureState With {
                .MeasuredValueText = If(r.MeasuredValueText Is Nothing, "", r.MeasuredValueText),
                .Result = If(r.Result Is Nothing, "", r.Result),
                .Note = If(r.Note Is Nothing, "", r.Note)
            }
        Next

        eyeBuffers(eyeNo) = dict
        eyeClosedStates(eyeNo) = chkEyeClosed.Checked
    End Sub

    Private Sub RestoreEyeState(eyeNo As Integer)
        Dim wasChecked = chkEyeClosed.Checked

        RemoveHandler chkEyeClosed.CheckedChanged, AddressOf EyeClosed_CheckedChanged
        chkEyeClosed.Checked = eyeClosedStates.ContainsKey(eyeNo) AndAlso eyeClosedStates(eyeNo)
        AddHandler chkEyeClosed.CheckedChanged, AddressOf EyeClosed_CheckedChanged

        Dim dict As Dictionary(Of String, EyeMeasureState) = Nothing

        If eyeBuffers.TryGetValue(eyeNo, dict) Then
            For Each r In rows
                If dict.ContainsKey(r.MeasureId) Then
                    r.MeasuredValueText = dict(r.MeasureId).MeasuredValueText
                    r.Result = dict(r.MeasureId).Result
                    r.Note = dict(r.MeasureId).Note
                Else
                    r.MeasuredValueText = ""
                    r.Result = ""
                    r.Note = ""
                End If
            Next
        Else
            For Each r In rows
                r.MeasuredValueText = ""
                r.Result = ""
                r.Note = ""
            Next
        End If

        ApplyEyeClosedUiState()
        grid.Refresh()
        RefreshMarkerStatuses()
        UpdateMeasurementProgress()
        If rows.Count > 0 Then SelectMeasureInGrid(rows(0).MeasureId)
    End Sub

    Private Sub RemoveEyeState(eyeNo As Integer)
        If eyeBuffers.ContainsKey(eyeNo) Then eyeBuffers.Remove(eyeNo)
        If eyeClosedStates.ContainsKey(eyeNo) Then eyeClosedStates.Remove(eyeNo)
    End Sub

    Private Sub ApplyEyeClosedUiState()
        If chkEyeClosed.Checked Then
            grid.Enabled = False
            lblPdfInfo.Text = $"Göz {txtEyeNo.Text}/{txtEyeCount.Text} kapalı seçildi. Ölçüm ve görsel kontrol alınmadan sıradaki göze geçilir."
        Else
            grid.Enabled = True
            lblPdfInfo.Text = $"Aktif Göz No: {txtEyeNo.Text}/{txtEyeCount.Text}. Ölçü balonuna tıklayın, açılan kutudan değer girin."
        End If
    End Sub

    Private Sub EyeClosed_CheckedChanged(sender As Object, e As EventArgs)
        If chkEyeClosed.Checked Then
            ClearMeasurementValuesOnly()
        End If

        ApplyEyeClosedUiState()
        StoreEyeState(GetCurrentEyeNo())
        UpdateMeasurementProgress()
        ScheduleDraftSave()
    End Sub

    Private Sub SaveClosedEyeOnly(p As ProductInfo, currentEyeNo As Integer, currentEyeCount As Integer)
        Dim recordId = DateTime.Now.ToString("yyyyMMdd-HHmmss") & "-KAPALI-" & Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant()
        Dim dt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")

        Dim closedRow As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {"RecordId", recordId},
            {"TrCode", p.TrCode},
            {"DrawingRev", p.DrawingRev},
            {"DrawingScope", ProductInfo.NormalizeDrawingScope(p.DrawingScope)},
            {"LotNo", txtLot.Text.Trim()},
            {"SerialNo", txtSerial.Text.Trim()},
            {"EyeCount", currentEyeCount.ToString()},
            {"EyeNo", currentEyeNo.ToString()},
            {"OperatorName", AppState.CurrentUserName},
            {"ComputerName", Environment.MachineName},
            {"ClosedDate", dt},
            {"Reason", "Göz Kapalı"},
            {"ProductionTicketId", linkedProductionTicketId},
            {"CommissioningId", linkedCommissioningId}
        }

        DataService.AppendClosedEye(closedRow)
        AuditService.Log("EYE_CLOSED_SKIP", p.TrCode, p.DrawingRev, $"RecordId={recordId}; EyeNo={currentEyeNo}/{currentEyeCount}; İşEmriNo={txtLot.Text.Trim()}; Serial={txtSerial.Text.Trim()}")

        RemoveEyeState(currentEyeNo)
        RemoveHandler chkEyeClosed.CheckedChanged, AddressOf EyeClosed_CheckedChanged
        chkEyeClosed.Checked = False
        AddHandler chkEyeClosed.CheckedChanged, AddressOf EyeClosed_CheckedChanged
        AdvanceEyeNoAfterSave(currentEyeNo, currentEyeCount)
        RestoreEyeState(GetCurrentEyeNo())

        MessageBox.Show("Göz kapalı olarak kaydedildi." & Environment.NewLine &
                        $"Göz No: {currentEyeNo}/{currentEyeCount}" & Environment.NewLine &
                        "Bu göz için ölçüm ve görsel kontrol kaydı alınmadı." & Environment.NewLine &
                        $"Sıradaki Göz No: {txtEyeNo.Text}/{currentEyeCount}",
                        "Göz kapalı", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Sub Clear_Click(sender As Object, e As EventArgs)
        suppressDraftSave = True
        Try
            txtSerial.Clear()
            ClearEyeBuffers()
            RemoveHandler chkEyeClosed.CheckedChanged, AddressOf EyeClosed_CheckedChanged
            chkEyeClosed.Checked = False
            AddHandler chkEyeClosed.CheckedChanged, AddressOf EyeClosed_CheckedChanged
            ResetEyeSequence()
            RestoreEyeState(GetCurrentEyeNo())
        Finally
            suppressDraftSave = False
        End Try
        DeleteCurrentMeasurementDraft()
    End Sub

    Private Sub ClearMeasurementValuesOnly()
        For Each r In rows
            r.MeasuredValueText = ""
            r.Result = ""
            r.Note = ""
        Next
        grid.Refresh()
        RefreshMarkerStatuses()
        UpdateMeasurementProgress()
        If rows.Count > 0 Then
            SelectMeasureInGrid(rows(0).MeasureId)
        End If
    End Sub

    Private Sub NormalizeEyeInputs()
        Dim eyeCount As Integer = 1
        If Not Integer.TryParse(txtEyeCount.Text.Trim(), eyeCount) OrElse eyeCount <= 0 Then
            eyeCount = 1
        End If

        txtEyeCount.Text = eyeCount.ToString()

        Dim eyeNo As Integer = 1
        If Not Integer.TryParse(txtEyeNo.Text.Trim(), eyeNo) OrElse eyeNo <= 0 Then
            eyeNo = 1
        End If

        If eyeNo > eyeCount Then eyeNo = eyeCount
        txtEyeNo.Text = eyeNo.ToString()
    End Sub

    Private Sub ResetEyeSequence()
        NormalizeEyeInputs()
        txtEyeNo.Text = "1"
    End Sub

    Private Function GetEyeCount() As Integer
        NormalizeEyeInputs()
        Dim eyeCount As Integer = 1
        Integer.TryParse(txtEyeCount.Text.Trim(), eyeCount)
        If eyeCount <= 0 Then eyeCount = 1
        Return eyeCount
    End Function

    Private Function GetCurrentEyeNo() As Integer
        NormalizeEyeInputs()
        Dim eyeNo As Integer = 1
        Integer.TryParse(txtEyeNo.Text.Trim(), eyeNo)
        If eyeNo <= 0 Then eyeNo = 1

        Dim eyeCount As Integer = 1
        Integer.TryParse(txtEyeCount.Text.Trim(), eyeCount)
        If eyeCount <= 0 Then eyeCount = 1

        If eyeNo > eyeCount Then
            eyeNo = eyeCount
            txtEyeNo.Text = eyeNo.ToString()
        End If

        Return eyeNo
    End Function

    Private Function ValidateEyeInputs() As Boolean
        Dim eyeCount As Integer = GetEyeCount()
        Dim eyeNo As Integer = GetCurrentEyeNo()

        If eyeNo > eyeCount Then
            MessageBox.Show("Göz numarası, göz adedinden büyük olamaz." & Environment.NewLine &
                            "Tüm gözler kaydedildiyse Temizle / Göz 1 butonu ile yeni sıraya başlayabilirsiniz." & Environment.NewLine &
                            "Göz adedi yanlışsa Göz Adedi alanını düzeltiniz.",
                            "Göz sırası tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return False
        End If

        Return True
    End Function

    Private Sub AdvanceEyeNoAfterSave(savedEyeNo As Integer, eyeCount As Integer)
        If savedEyeNo < eyeCount Then
            txtEyeNo.Text = (savedEyeNo + 1).ToString()
        Else
            txtEyeNo.Text = eyeCount.ToString()
            MessageBox.Show("Bu ürün için tanımlanan göz adedi tamamlandı." & Environment.NewLine &
                            $"Kaydedilen son göz: {savedEyeNo}/{eyeCount}" & Environment.NewLine &
                            "Göz No, Göz Adedini geçemez." & Environment.NewLine &
                            "Yeni çevrim/ürün için Temizle / Göz 1 butonuna basabilirsiniz.",
                            "Göz sırası tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If
    End Sub

    Private Sub FrmMeasurementEntry_FormClosed(sender As Object, e As FormClosedEventArgs)
        draftSaveTimer.Stop()
        draftSaveTimer.Dispose()
        If criticalGridFont IsNot Nothing Then criticalGridFont.Dispose()
        Try
            pdfViewer.Dispose()
        Catch ex As Exception
            ErrorLogService.Log("FrmMeasurementEntry.FormClosed.DisposeViewer", ex)
        End Try
        CleanupCurrentPdfFiles()
    End Sub

    Private Sub FrmMeasurementEntry_FormClosing(sender As Object, e As FormClosingEventArgs)
        If measurementDataCommitted Then Return
        draftSaveTimer.Stop()
        SaveMeasurementDraftNow()
    End Sub

    Private Sub CleanupCurrentPdfFiles()
        TempFileService.TryDeleteTempPdf(currentTempPdf)
        TempFileService.TryDeleteTempPdf(currentTempPng)
        TempFileService.TryDeleteTempPdf(currentTempHtml)
        currentTempPdf = ""
        currentTempPng = ""
        currentTempHtml = ""
    End Sub
    Private Class EyeMeasureState
        Public Property MeasuredValueText As String = ""
        Public Property Result As String = ""
        Public Property Note As String = ""
    End Class

End Class
