Imports System.Data
Imports System.Drawing
Imports System.Globalization
Imports System.Linq
Imports System.Net
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Windows.Forms

Public Class FrmMechanismQualityControl
    Inherits Form

    Private ReadOnly numIncomingEyeCount As New NumericUpDown()
    Private ReadOnly cboProductNameCode As New ComboBox()
    Private ReadOnly txtProductSelectionFilter As New TextBox()
    Private ReadOnly chkIncludeAllProducts As New CheckBox()
    Private ReadOnly txtMountedMechanismCounter As New TextBox()
    Private ReadOnly txtDeliveryExplanation As New TextBox()
    Private ReadOnly txtControlExplanation As New TextBox()
    Private ReadOnly lblDeliveredBy As New Label()
    Private ReadOnly lblControlledBy As New Label()
    Private ReadOnly lblMode As New Label()
    Private ReadOnly txtFilter As New TextBox()
    Private ReadOnly cboStatus As New ComboBox()
    Private ReadOnly dtpDeliveryDate As New DateTimePicker()
    Private ReadOnly lblCount As New Label()
    Private ReadOnly lblVisibleCount As New Label()
    Private ReadOnly lblPendingCount As New Label()
    Private ReadOnly lblCompletedCount As New Label()
    Private ReadOnly grid As New DataGridView()
    Private ReadOnly btnSubmit As New Button()
    Private ReadOnly btnSuitable As New Button()
    Private ReadOnly btnNotSuitable As New Button()
    Private ReadOnly btnToggleListFocus As New Button()
    Private ReadOnly btnEmailReport As New Button()
    Private ReadOnly btnEmailRecipients As New Button()
    Private ReadOnly btnDeleteRecord As New Button()
    Private currentRows As New List(Of Dictionary(Of String, String))()

    Private selectedControlId As String = ""
    Private productOptions As New List(Of String)()
    Private productCavityCountByTr As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
    Private selectedProducts As New List(Of String)()
    Private isClearingForm As Boolean = False
    Private isUpdatingProductOptions As Boolean = False
    Private isRestoringGridSelection As Boolean = False
    Private suppressDraftPrompt As Boolean = False
    Private selectedRecordIsPending As Boolean = False
    Private loadedMountedMechanismCounter As String = ""
    Private loadedControlExplanation As String = ""
    Private loadedProductsSerialized As String = ""
    Private isListFocusMode As Boolean = False
    Private mainLayoutHost As TableLayoutPanel = Nothing
    Private headerPanelHost As Control = Nothing
    Private entryPanelHost As Control = Nothing
    Private actionPanelHost As Control = Nothing
    Private filterPanelHost As Control = Nothing
    Private gridPanelHost As Control = Nothing

    Public Sub New()
        AuthorizationService.Require(AppState.CanOpenMechanismQualityControl, "Mekanizma Kalite Kontrol")
        AppIconService.Apply(Me)

        Text = "Mekanizma Kalite Kontrol"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        MinimumSize = New Size(900, 560)
        AutoScaleMode = AutoScaleMode.Dpi
        Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
        BackColor = Color.FromArgb(243, 247, 252)
        DoubleBuffered = True

        BuildScreen()
        LoadProducts()
        LoadGrid()
        ApplyRoleMode()
        AddHandler FormClosing, AddressOf MechanismQualityControl_FormClosing
        AddHandler Resize, AddressOf FrmMechanismQualityControl_Resize
        AddHandler Shown, AddressOf FrmMechanismQualityControl_Shown
        AddHandler DpiChanged,
            Sub()
                If IsHandleCreated AndAlso Not IsDisposed Then
                    BeginInvoke(New MethodInvoker(AddressOf ApplyResponsiveLayout))
                End If
            End Sub
    End Sub

    Private Sub BuildScreen()
        Dim main As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .Padding = New Padding(16),
            .BackColor = BackColor
        }
        main.RowStyles.Add(New RowStyle(SizeType.Absolute, 54.0F))
        main.RowStyles.Add(New RowStyle(SizeType.Absolute, 54.0F))
        main.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(main)

        mainLayoutHost = main
        actionPanelHost = BuildActionPanel()
        filterPanelHost = BuildFilterPanel()

        ConfigureGrid()
        gridPanelHost = BuildGridPanel()

        main.Controls.Add(actionPanelHost, 0, 0)
        main.Controls.Add(filterPanelHost, 0, 1)
        main.Controls.Add(gridPanelHost, 0, 2)
    End Sub

    Private Sub FrmMechanismQualityControl_Shown(sender As Object, e As EventArgs)
        ApplyResponsiveLayout()
    End Sub

    Private Sub FrmMechanismQualityControl_Resize(sender As Object, e As EventArgs)
        ApplyResponsiveLayout()
    End Sub

    Private Function BuildHeaderPanel() As Control
        Dim shell As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .BackColor = Color.FromArgb(31, 71, 136),
            .Padding = New Padding(18, 16, 18, 16),
            .Margin = New Padding(0, 0, 0, 10)
        }
        shell.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        shell.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 420.0F))
        shell.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim textHost As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0)
        }
        textHost.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
        textHost.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim lblTitle As New Label() With {
            .Text = "Mekanizma Kalite Kontrol",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 16.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(0)
        }
        Dim lblSubtitle As New Label() With {
            .Text = "Teslim ve kontrol adımlarını aynı ekranda yönetin. Canlı filtre, seçim ve kayıt listesi birlikte çalışır.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.FromArgb(221, 231, 243),
            .Font = New Font("Segoe UI", 9.5F, FontStyle.Regular),
            .TextAlign = ContentAlignment.TopLeft,
            .Margin = New Padding(0, 2, 0, 0)
        }
        textHost.Controls.Add(lblTitle, 0, 0)
        textHost.Controls.Add(lblSubtitle, 0, 1)
        shell.Controls.Add(textHost, 0, 0)

        Dim statsHost As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .BackColor = Color.Transparent,
            .Padding = New Padding(0, 12, 0, 0),
            .Margin = New Padding(0)
        }
        statsHost.Controls.Add(CreateSummaryCard("GÖSTERİLEN", lblVisibleCount, Color.FromArgb(76, 154, 255)))
        statsHost.Controls.Add(CreateSummaryCard("BEKLEYEN", lblPendingCount, Color.FromArgb(232, 180, 46)))
        statsHost.Controls.Add(CreateSummaryCard("TAMAMLANAN", lblCompletedCount, Color.FromArgb(63, 160, 102)))
        shell.Controls.Add(statsHost, 1, 0)

        Return shell
    End Function

    Private Function CreateSummaryCard(title As String, valueLabel As Label, accentColor As Color) As Control
        Dim card As New Panel() With {
            .Width = 128,
            .Height = 64,
            .BackColor = Color.White,
            .Margin = New Padding(10, 0, 0, 0),
            .Padding = New Padding(12, 10, 12, 10)
        }

        Dim accent As New Panel() With {
            .Dock = DockStyle.Left,
            .Width = 5,
            .BackColor = accentColor
        }
        card.Controls.Add(accent)

        Dim lblTitle As New Label() With {
            .Text = title,
            .Dock = DockStyle.Top,
            .Height = 18,
            .ForeColor = Color.FromArgb(89, 101, 118),
            .Font = New Font("Segoe UI", 8.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(0)
        }

        valueLabel.Dock = DockStyle.Fill
        valueLabel.ForeColor = Color.FromArgb(31, 41, 55)
        valueLabel.Font = New Font("Segoe UI", 15.0F, FontStyle.Bold)
        valueLabel.TextAlign = ContentAlignment.MiddleLeft
        valueLabel.Padding = New Padding(0, 4, 0, 0)
        valueLabel.Text = "0"
        valueLabel.Margin = New Padding(0)

        Dim content As New Panel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(12, 0, 0, 0),
            .BackColor = Color.Transparent
        }
        content.Controls.Add(valueLabel)
        content.Controls.Add(lblTitle)
        card.Controls.Add(content)

        Return card
    End Function

    Private Function BuildEntryPanel() As Control
        Dim split As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .Padding = New Padding(0),
            .BackColor = BackColor,
            .Margin = New Padding(0)
        }
        split.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        split.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        split.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        split.Controls.Add(BuildDeliveryPanel(), 0, 0)
        split.Controls.Add(BuildControlPanel(), 1, 0)
        Return split
    End Function

    Private Function BuildDeliveryPanel() As Control
        Dim group As New GroupBox() With {
            .Text = "1. Teslim Bilgileri",
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold),
            .BackColor = Color.White,
            .Padding = New Padding(10),
            .Margin = New Padding(0, 0, 6, 0),
            .ForeColor = Color.FromArgb(31, 71, 136)
        }
        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 7,
            .Padding = New Padding(6),
            .BackColor = Color.White,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 175.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
        group.Controls.Add(layout)

        layout.Controls.Add(CreateFieldLabel("Teslim Eden"), 0, 0)
        ConfigureInfoLabel(lblDeliveredBy)
        layout.Controls.Add(lblDeliveredBy, 1, 0)

        layout.Controls.Add(CreateFieldLabel("Gelen Göz Sayısı"), 0, 1)
        Dim eyeCountHost As New Panel() With {
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0),
            .Padding = New Padding(6),
            .BackColor = Color.Transparent
        }
        numIncomingEyeCount.Dock = DockStyle.Left
        numIncomingEyeCount.Width = 120
        numIncomingEyeCount.Margin = New Padding(0)
        numIncomingEyeCount.Minimum = 1D
        numIncomingEyeCount.Maximum = 999999D
        numIncomingEyeCount.Value = 1D
        ConfigureNumericInput(numIncomingEyeCount)
        eyeCountHost.Controls.Add(numIncomingEyeCount)
        layout.Controls.Add(eyeCountHost, 1, 1)

        layout.Controls.Add(CreateFieldLabel("Ürün Filtresi"), 0, 2)
        txtProductSelectionFilter.Dock = DockStyle.Fill
        txtProductSelectionFilter.Margin = New Padding(6)
        ConfigureTextInput(txtProductSelectionFilter)
        txtProductSelectionFilter.PlaceholderText = "TR kodu / ürün adı / makine / kalıp"
        AddHandler txtProductSelectionFilter.TextChanged, AddressOf ProductSelectionFilterChanged
        layout.Controls.Add(txtProductSelectionFilter, 1, 2)

        chkIncludeAllProducts.Text = "Tüm ürünleri dahil et"
        chkIncludeAllProducts.Dock = DockStyle.Fill
        chkIncludeAllProducts.Margin = New Padding(6, 4, 6, 4)
        chkIncludeAllProducts.TextAlign = ContentAlignment.MiddleLeft
        AddHandler chkIncludeAllProducts.CheckedChanged, Sub() LoadProducts()
        layout.Controls.Add(chkIncludeAllProducts, 1, 3)

        layout.Controls.Add(CreateFieldLabel("Ürün Adı ve Kodu"), 0, 4)
        cboProductNameCode.Dock = DockStyle.Fill
        cboProductNameCode.Margin = New Padding(6)
        cboProductNameCode.DropDownStyle = ComboBoxStyle.DropDown
        cboProductNameCode.AutoCompleteMode = AutoCompleteMode.None
        cboProductNameCode.AutoCompleteSource = AutoCompleteSource.None
        ConfigureSelectionInput(cboProductNameCode)
        AddHandler cboProductNameCode.TextUpdate, AddressOf ProductNameCodeTextUpdated
        AddHandler cboProductNameCode.SelectedIndexChanged, AddressOf ProductSelectionChanged
        AddHandler cboProductNameCode.Validated, AddressOf ProductSelectionChanged
        layout.Controls.Add(cboProductNameCode, 1, 4)

        layout.Controls.Add(CreateFieldLabel("Teslim Açıklaması"), 0, 5)
        txtDeliveryExplanation.Dock = DockStyle.Fill
        txtDeliveryExplanation.Margin = New Padding(6)
        txtDeliveryExplanation.Multiline = True
        txtDeliveryExplanation.ScrollBars = ScrollBars.Vertical
        ConfigureTextInput(txtDeliveryExplanation)
        txtDeliveryExplanation.PlaceholderText = "Teslim eden kullanıcının açıklaması"
        layout.Controls.Add(txtDeliveryExplanation, 1, 5)

        ConfigureActionButton(btnSubmit, "Teslim Et", Color.FromArgb(226, 238, 255), Color.FromArgb(31, 71, 136), 160)
        btnSubmit.Margin = New Padding(0, 0, 8, 0)
        AddHandler btnSubmit.Click, AddressOf Submit_Click

        Dim btnNew As New Button() With {
            .Text = "Yeni / Temizle",
            .Width = 130,
            .Height = 34,
            .Margin = New Padding(8, 0, 0, 0),
            .Cursor = Cursors.Hand,
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.White
        }
        btnNew.FlatAppearance.BorderColor = Color.FromArgb(120, 120, 120)
        AddButtonHoverEffect(btnNew, Color.FromArgb(238, 238, 238))
        AddHandler btnNew.Click, AddressOf New_Click

        Dim submitFlow As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0),
            .Padding = New Padding(6, 5, 0, 0),
            .WrapContents = False
        }
        submitFlow.Controls.AddRange({btnSubmit, btnNew})
        layout.Controls.Add(submitFlow, 1, 6)
        Return group
    End Function

    Private Function BuildControlPanel() As Control
        Dim group As New GroupBox() With {
            .Text = "2. Kontrol Bilgileri",
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold),
            .BackColor = Color.White,
            .Padding = New Padding(10),
            .Margin = New Padding(6, 0, 0, 0),
            .ForeColor = Color.FromArgb(31, 71, 136)
        }
        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 4,
            .Padding = New Padding(6),
            .BackColor = Color.White,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 215.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
        group.Controls.Add(layout)

        layout.Controls.Add(CreateFieldLabel("Kontrol Eden"), 0, 0)
        ConfigureInfoLabel(lblControlledBy)
        layout.Controls.Add(lblControlledBy, 1, 0)

        layout.Controls.Add(CreateFieldLabel("Montajı Yapılan Mekanizma/Sayaç"), 0, 1)
        txtMountedMechanismCounter.Dock = DockStyle.Fill
        txtMountedMechanismCounter.Margin = New Padding(6)
        ConfigureTextInput(txtMountedMechanismCounter)
        txtMountedMechanismCounter.PlaceholderText = "Mekanizma veya sayaç bilgisini girin"
        layout.Controls.Add(txtMountedMechanismCounter, 1, 1)

        layout.Controls.Add(CreateFieldLabel("Kontrol Açıklaması"), 0, 2)
        txtControlExplanation.Dock = DockStyle.Fill
        txtControlExplanation.Margin = New Padding(6)
        txtControlExplanation.Multiline = True
        txtControlExplanation.ScrollBars = ScrollBars.Vertical
        ConfigureTextInput(txtControlExplanation)
        txtControlExplanation.PlaceholderText = "Kontrol eden kullanıcının açıklaması"
        layout.Controls.Add(txtControlExplanation, 1, 2)

        ConfigureActionButton(btnSuitable, "UYGUN", Color.Honeydew, Color.DarkGreen, 130)
        btnSuitable.Margin = New Padding(0, 0, 8, 0)
        AddHandler btnSuitable.Click, Sub() CompleteSelected(True)
        ConfigureActionButton(btnNotSuitable, "UYGUN DEĞİL", Color.MistyRose, Color.DarkRed, 145)
        btnNotSuitable.Margin = New Padding(8, 0, 0, 0)
        AddHandler btnNotSuitable.Click, Sub() CompleteSelected(False)
        Dim resultFlow As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0),
            .Padding = New Padding(6, 5, 0, 0),
            .WrapContents = False
        }
        resultFlow.Controls.AddRange({btnSuitable, btnNotSuitable})
        layout.Controls.Add(resultFlow, 1, 3)

        Return group
    End Function

    Private Shared Sub ConfigureInfoLabel(label As Label)
        label.Dock = DockStyle.Fill
        label.Margin = New Padding(6)
        label.BorderStyle = BorderStyle.FixedSingle
        label.BackColor = Color.FromArgb(247, 250, 253)
        label.ForeColor = Color.FromArgb(31, 41, 55)
        label.Font = New Font("Segoe UI", 9.25F, FontStyle.Bold)
        label.TextAlign = ContentAlignment.MiddleLeft
        label.Padding = New Padding(8, 0, 0, 0)
    End Sub

    Private Function BuildActionPanel() As Control
        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 6,
            .RowCount = 1,
            .Padding = New Padding(12, 2, 12, 2),
            .BackColor = Color.White,
            .Margin = New Padding(0, 2, 0, 2)
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        lblMode.Dock = DockStyle.Fill
        lblMode.Margin = New Padding(0)
        lblMode.Padding = New Padding(14, 0, 14, 0)
        lblMode.ForeColor = Color.FromArgb(31, 71, 136)
        lblMode.BackColor = Color.FromArgb(237, 244, 255)
        lblMode.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblMode.TextAlign = ContentAlignment.MiddleLeft
        layout.Controls.Add(lblMode, 0, 0)

        Dim btnRefresh As New Button() With {
            .Text = "Yenile",
            .Width = 108,
            .Height = 38,
            .Margin = New Padding(8, 0, 8, 0),
            .Anchor = AnchorStyles.None,
            .Cursor = Cursors.Hand,
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.FromArgb(248, 250, 253),
            .ForeColor = Color.FromArgb(31, 41, 55),
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .UseVisualStyleBackColor = False,
            .AutoEllipsis = False
        }
        btnRefresh.FlatAppearance.BorderSize = 2
        btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(107, 114, 128)
        btnRefresh.FlatAppearance.MouseDownBackColor = Color.FromArgb(229, 231, 235)
        AddButtonHoverEffect(btnRefresh, Color.FromArgb(238, 242, 247))
        AddHandler btnRefresh.Click, AddressOf Refresh_Click

        btnToggleListFocus.Text = "Yeni Teslim"
        btnToggleListFocus.Width = 150
        btnToggleListFocus.Height = 38
        btnToggleListFocus.Margin = New Padding(0, 0, 8, 0)
        btnToggleListFocus.Anchor = AnchorStyles.None
        btnToggleListFocus.Cursor = Cursors.Hand
        btnToggleListFocus.FlatStyle = FlatStyle.Flat
        btnToggleListFocus.BackColor = Color.FromArgb(31, 71, 136)
        btnToggleListFocus.ForeColor = Color.White
        btnToggleListFocus.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        btnToggleListFocus.UseVisualStyleBackColor = False
        btnToggleListFocus.AutoEllipsis = False
        btnToggleListFocus.FlatAppearance.BorderSize = 2
        btnToggleListFocus.FlatAppearance.BorderColor = Color.FromArgb(31, 71, 136)
        btnToggleListFocus.FlatAppearance.MouseDownBackColor = Color.FromArgb(23, 54, 105)
        AddButtonHoverEffect(btnToggleListFocus, Color.FromArgb(42, 91, 166))
        AddHandler btnToggleListFocus.Click, AddressOf NewDelivery_Click

        btnEmailReport.Text = "E-posta Raporu"
        btnEmailReport.Width = 142
        btnEmailReport.Height = 38
        btnEmailReport.Margin = New Padding(0, 0, 8, 0)
        btnEmailReport.Anchor = AnchorStyles.None
        btnEmailReport.Cursor = Cursors.Hand
        btnEmailReport.FlatStyle = FlatStyle.Flat
        btnEmailReport.BackColor = Color.FromArgb(15, 123, 63)
        btnEmailReport.ForeColor = Color.White
        btnEmailReport.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        btnEmailReport.UseVisualStyleBackColor = False
        btnEmailReport.AutoEllipsis = False
        btnEmailReport.FlatAppearance.BorderSize = 2
        btnEmailReport.FlatAppearance.BorderColor = Color.FromArgb(15, 123, 63)
        btnEmailReport.FlatAppearance.MouseDownBackColor = Color.FromArgb(10, 92, 47)
        AddButtonHoverEffect(btnEmailReport, Color.FromArgb(20, 145, 75))
        AddHandler btnEmailReport.Click, AddressOf EmailReport_Click

        btnEmailRecipients.Text = "Uygun Değil Mail Alıcıları"
        btnEmailRecipients.Width = 190
        btnEmailRecipients.Height = 38
        btnEmailRecipients.Margin = New Padding(0, 0, 8, 0)
        btnEmailRecipients.Anchor = AnchorStyles.None
        btnEmailRecipients.Cursor = Cursors.Hand
        btnEmailRecipients.FlatStyle = FlatStyle.Flat
        btnEmailRecipients.BackColor = Color.FromArgb(255, 247, 230)
        btnEmailRecipients.ForeColor = Color.FromArgb(120, 70, 0)
        btnEmailRecipients.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        btnEmailRecipients.UseVisualStyleBackColor = False
        btnEmailRecipients.FlatAppearance.BorderSize = 2
        btnEmailRecipients.FlatAppearance.BorderColor = Color.FromArgb(180, 120, 30)
        AddHandler btnEmailRecipients.Click, AddressOf EmailRecipients_Click

        btnDeleteRecord.Text = "Seçili Kaydı Sil"
        btnDeleteRecord.Width = 142
        btnDeleteRecord.Height = 38
        btnDeleteRecord.Margin = New Padding(0, 0, 8, 0)
        btnDeleteRecord.Anchor = AnchorStyles.None
        btnDeleteRecord.Cursor = Cursors.Hand
        btnDeleteRecord.FlatStyle = FlatStyle.Flat
        btnDeleteRecord.BackColor = Color.FromArgb(185, 28, 28)
        btnDeleteRecord.ForeColor = Color.White
        btnDeleteRecord.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        btnDeleteRecord.UseVisualStyleBackColor = False
        btnDeleteRecord.AutoEllipsis = False
        btnDeleteRecord.FlatAppearance.BorderSize = 2
        btnDeleteRecord.FlatAppearance.BorderColor = Color.FromArgb(153, 27, 27)
        btnDeleteRecord.FlatAppearance.MouseDownBackColor = Color.FromArgb(127, 29, 29)
        AddButtonHoverEffect(btnDeleteRecord, Color.FromArgb(220, 38, 38))
        AddHandler btnDeleteRecord.Click, AddressOf DeleteSelectedRecord_Click

        layout.Controls.Add(btnEmailReport, 1, 0)
        layout.Controls.Add(btnEmailRecipients, 2, 0)
        layout.Controls.Add(btnDeleteRecord, 3, 0)
        layout.Controls.Add(btnToggleListFocus, 4, 0)
        layout.Controls.Add(btnRefresh, 5, 0)
        Return layout
    End Function

    Private Sub EmailRecipients_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.CanManageMechanismQualityEmailRecipients, "Mekanizma Kalite Kontrol Mail Alıcıları")
            Using frm As New FrmMechanismQualityEmailRecipients()
                frm.ShowDialog(Me)
            End Using
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Mail alıcıları açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function BuildFilterPanel() As Control
        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 7,
            .RowCount = 1,
            .Padding = New Padding(12, 8, 12, 6),
            .BackColor = Color.White,
            .Margin = New Padding(0, 0, 0, 10)
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 55.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 62.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 125.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 88.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 150.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 285.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        layout.Controls.Add(New Label() With {
            .Text = "Arama",
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0)

        txtFilter.Dock = DockStyle.Fill
        txtFilter.Margin = New Padding(3, 2, 8, 2)
        ConfigureTextInput(txtFilter)
        txtFilter.PlaceholderText = "ürün / mekanizma / teslim eden / kontrol eden"
        AddHandler txtFilter.TextChanged, Sub() LoadGrid()
        layout.Controls.Add(txtFilter, 1, 0)

        layout.Controls.Add(New Label() With {
            .Text = "Durum",
            .Dock = DockStyle.Fill,
            .Margin = New Padding(10, 0, 0, 0),
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft
        }, 2, 0)

        cboStatus.Dock = DockStyle.Fill
        cboStatus.Margin = New Padding(3, 2, 8, 2)
        cboStatus.DropDownStyle = ComboBoxStyle.DropDownList
        ConfigureSelectionInput(cboStatus)
        cboStatus.Items.AddRange({"BEKLEYEN", "TAMAMLANAN", "TÜMÜ"})
        cboStatus.SelectedIndex = 0
        AddHandler cboStatus.SelectedIndexChanged, Sub() LoadGrid()
        layout.Controls.Add(cboStatus, 3, 0)

        layout.Controls.Add(New Label() With {
            .Text = "Teslim Günü",
            .Dock = DockStyle.Fill,
            .Margin = New Padding(8, 0, 0, 0),
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft
        }, 4, 0)

        dtpDeliveryDate.Dock = DockStyle.Fill
        dtpDeliveryDate.Margin = New Padding(3, 2, 8, 2)
        dtpDeliveryDate.Format = DateTimePickerFormat.Custom
        dtpDeliveryDate.CustomFormat = "dd.MM.yyyy"
        dtpDeliveryDate.ShowCheckBox = True
        dtpDeliveryDate.Checked = False
        dtpDeliveryDate.Value = Date.Today
        dtpDeliveryDate.Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
        AddHandler dtpDeliveryDate.ValueChanged, Sub() LoadGrid()
        layout.Controls.Add(dtpDeliveryDate, 5, 0)

        lblCount.Dock = DockStyle.Fill
        lblCount.Margin = New Padding(12, 0, 0, 0)
        lblCount.TextAlign = ContentAlignment.MiddleRight
        lblCount.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblCount.ForeColor = Color.FromArgb(89, 101, 118)
        lblCount.AutoEllipsis = True
        layout.Controls.Add(lblCount, 6, 0)

        Return layout
    End Function

    Private Function BuildGridPanel() As Control
        Dim host As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .BackColor = Color.White,
            .Margin = New Padding(0)
        }
        host.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        host.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim top As New Panel() With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(248, 250, 253),
            .Padding = New Padding(14, 0, 14, 0)
        }
        top.Controls.Add(New Label() With {
            .Text = "Kayıt Listesi",
            .Dock = DockStyle.Left,
            .Width = 160,
            .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(31, 41, 55),
            .TextAlign = ContentAlignment.MiddleLeft
        })
        top.Controls.Add(New Label() With {
            .Text = "Bekleyen kayıtlar üstte tutulur; sarı satırlar işlem bekleyen teslimlerdir.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.FromArgb(89, 101, 118),
            .Font = New Font("Segoe UI", 8.75F, FontStyle.Regular),
            .TextAlign = ContentAlignment.MiddleRight
        })

        host.Controls.Add(top, 0, 0)
        host.Controls.Add(grid, 0, 1)
        Return host
    End Function

    Private Sub ToggleListFocusMode(sender As Object, e As EventArgs)
        isListFocusMode = Not isListFocusMode
        ApplyResponsiveLayout()
    End Sub

    Private Sub NewDelivery_Click(sender As Object, e As EventArgs)
        OpenNewDeliveryDialog()
    End Sub

    Private Sub EmailReport_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.CanOpenMechanismQualityControl, "Mekanizma Kalite Kontrol E-posta Raporu")
            If currentRows.Count = 0 Then
                MessageBox.Show(
                    "E-posta raporuna eklenecek kayıt bulunamadı.",
                    "E-posta hazırlanmadı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information)
                Return
            End If

            Dim answer = MessageBox.Show(
                "Ekranda filtrelenmiş " & currentRows.Count.ToString() & " kayıt Outlook e-posta taslağına aktarılacak." &
                Environment.NewLine & Environment.NewLine &
                "E-posta otomatik gönderilmez; açılan taslağı düzenleyip siz gönderebilirsiniz." &
                Environment.NewLine & Environment.NewLine &
                "Devam edilsin mi?",
                "E-posta raporu hazırla",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1)
            If answer <> DialogResult.Yes Then Return

            Dim subjectDate = If(dtpDeliveryDate.Checked,
                                 dtpDeliveryDate.Value.ToString("dd.MM.yyyy"),
                                 DateTime.Now.ToString("dd.MM.yyyy"))
            Dim subject = "Mekanizma Kalite Kontrol Raporu - " & subjectDate
            Dim htmlBody = BuildEmailReportHtml(currentRows)
            If Not OutlookEmailDraftService.TryOpenEditableDraft(subject, htmlBody) Then
                MessageBox.Show(
                    "Outlook düzenlenebilir e-posta penceresi açılamadı." & Environment.NewLine &
                    "Lütfen Outlook'un bu bilgisayarda kurulu ve kullanılabilir olduğunu kontrol edin.",
                    "Outlook açılamadı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning)
                Return
            End If

            AuditService.Log(
                "MECHANISM_QUALITY_EMAIL_REPORT",
                "",
                "",
                "Kayıt sayısı=" & currentRows.Count.ToString() &
                "; Arama=" & txtFilter.Text.Trim() &
                "; Durum=" & CurrentStatusFilterText() &
                "; Teslim günü=" & CurrentDeliveryDateFilterText())
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "E-posta raporu hazırlanamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function BuildEmailReportHtml(rows As List(Of Dictionary(Of String, String))) As String
        Dim totalEyeCount As Long = 0
        For Each row In rows
            Dim eyeCount As Long
            If Long.TryParse(DataService.GetValue(row, "IncomingEyeCount"), eyeCount) Then totalEyeCount += eyeCount
        Next

        Dim pendingCount = rows.Where(Function(row) IsPendingReportRow(row)).Count()
        Dim suitableCount = rows.Where(Function(row) ReportResultText(row) = "UYGUN").Count()
        Dim notSuitableCount = rows.Where(Function(row) ReportResultText(row) = "UYGUN DEĞİL").Count()
        Dim today = Date.Today
        Dim todayDeliveredCount = rows.Where(Function(row) IsSameLocalDate(DataService.GetValue(row, "CreatedAt"), today)).Count()
        Dim todayControlledCount = rows.Where(
            Function(row)
                Return IsSameLocalDate(DataService.GetValue(row, "ControlledAt"), today) OrElse
                       IsSameLocalDate(DataService.GetValue(row, "ControlDateTime"), today)
            End Function).Count()
        Dim filterDescription = If(txtFilter.Text.Trim() = "", "Yok", txtFilter.Text.Trim())

        Dim html As New StringBuilder()
        html.AppendLine("<!DOCTYPE html><html><head><meta charset=""utf-8""></head>")
        html.AppendLine("<body style=""font-family:Segoe UI,Arial,sans-serif;font-size:13px;color:#1f2937;background:#ffffff;"">")
        html.AppendLine("<h2 style=""margin:0 0 6px;color:#1f477e;"">Mekanizma Kalite Kontrol Raporu</h2>")
        html.AppendLine("<div style=""margin-bottom:14px;color:#4b5563;"">Hazırlanma: " & EncodeHtml(DateTime.Now.ToString("dd.MM.yyyy HH:mm")) &
                        " &nbsp; | &nbsp; Hazırlayan: " & EncodeHtml(AppState.CurrentUserName & " / " & AppState.NormalizeRole(AppState.CurrentRole)) &
                        " &nbsp; | &nbsp; Durum: " & EncodeHtml(CurrentStatusFilterText()) &
                        " &nbsp; | &nbsp; Teslim günü: " & EncodeHtml(CurrentDeliveryDateFilterText()) &
                        " &nbsp; | &nbsp; Arama: " & EncodeHtml(filterDescription) & "</div>")

        html.AppendLine("<table style=""border-collapse:collapse;margin-bottom:16px;""><tr>")
        AppendSummaryCell(html, "Kayıt", rows.Count.ToString(), "#e7eef8", "#1f477e")
        AppendSummaryCell(html, "Gelen Göz", totalEyeCount.ToString(), "#eef2ff", "#3730a3")
        AppendSummaryCell(html, "Bekleyen", pendingCount.ToString(), "#fff4b3", "#785700")
        AppendSummaryCell(html, "Uygun", suitableCount.ToString(), "#d3f0db", "#166534")
        AppendSummaryCell(html, "Uygun Değil", notSuitableCount.ToString(), "#ffd3d3", "#991b1b")
        AppendSummaryCell(html, "Bugün Teslim", todayDeliveredCount.ToString(), "#e0f2fe", "#075985")
        AppendSummaryCell(html, "Bugün Kontrol", todayControlledCount.ToString(), "#f3e8ff", "#6b21a8")
        html.AppendLine("</tr></table>")

        html.AppendLine("<table style=""border-collapse:collapse;width:100%;font-size:12px;"">")
        html.AppendLine("<thead><tr style=""background:#dfe9f5;color:#183252;"">")
        For Each header In {"TESLİM TARİHİ", "DURUM", "GELEN GÖZ", "TESLİM EDEN", "ÜRÜN ADI VE KODU", "MONTAJ YAPILAN MEKANİZMA/SAYAÇ", "TESLİM AÇIKLAMASI", "KONTROL AÇIKLAMASI", "SONUÇ", "KONTROL EDEN", "KONTROL TARİHİ"}
            html.AppendLine("<th style=""border:1px solid #aab8ca;padding:7px;text-align:left;"">" & EncodeHtml(header) & "</th>")
        Next
        html.AppendLine("</tr></thead><tbody>")

        Dim rowIndex As Integer = 0
        For Each row In rows
            Dim resultText = ReportResultText(row)
            Dim backColor = If(rowIndex Mod 2 = 0, "#ffffff", "#f8fafc")
            If IsPendingReportRow(row) Then
                backColor = "#fffbea"
            ElseIf resultText = "UYGUN DEĞİL" Then
                backColor = "#fff1f1"
            ElseIf resultText = "UYGUN" Then
                backColor = "#eefbf1"
            End If

            html.AppendLine("<tr style=""background:" & backColor & ";"">")
            AppendReportCell(html, FormatReportDateTime(DataService.GetValue(row, "CreatedAt")), False)
            AppendReportCell(html, ReportStatusText(row), False)
            AppendReportCell(html, DataService.GetValue(row, "IncomingEyeCount"), False)
            AppendReportCell(html, DataService.GetValue(row, "DeliveredBy"), False)
            AppendReportCell(html, DataService.GetValue(row, "ProductNameCode"), True)
            AppendReportCell(html, DataService.GetValue(row, "MountedMechanismCounter"), True)
            AppendReportCell(html, DataService.GetValue(row, "DeliveryExplanation"), True)
            AppendReportCell(html, DataService.GetValue(row, "ControlExplanation"), True)
            AppendReportCell(html, resultText, False)
            AppendReportCell(html, DataService.GetValue(row, "ControlledBy"), False)
            Dim controlledAt = DataService.GetValue(row, "ControlledAt")
            If controlledAt.Trim() = "" Then controlledAt = DataService.GetValue(row, "ControlDateTime")
            AppendReportCell(html, FormatReportDateTime(controlledAt), False)
            html.AppendLine("</tr>")
            rowIndex += 1
        Next

        html.AppendLine("</tbody></table>")
        html.AppendLine("<p style=""margin-top:16px;"">Bilginize.</p>")
        html.AppendLine("</body></html>")
        Return html.ToString()
    End Function

    Private Function CurrentStatusFilterText() As String
        Return If(cboStatus.SelectedItem Is Nothing, "BEKLEYEN", cboStatus.SelectedItem.ToString())
    End Function

    Private Function CurrentDeliveryDateFilterText() As String
        Return If(dtpDeliveryDate.Checked, dtpDeliveryDate.Value.ToString("dd.MM.yyyy"), "TÜM GÜNLER")
    End Function

    Private Shared Function IsPendingReportRow(row As Dictionary(Of String, String)) As Boolean
        Return String.Equals(DataService.GetValue(row, "Status"), "PENDING", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function ReportStatusText(row As Dictionary(Of String, String)) As String
        Return If(IsPendingReportRow(row), "BEKLEYEN", "TAMAMLANAN")
    End Function

    Private Shared Function ReportResultText(row As Dictionary(Of String, String)) As String
        If IsPendingReportRow(row) Then Return "BEKLEYEN"
        If IsAffirmativeReportValue(DataService.GetValue(row, "IsNotSuitable")) Then Return "UYGUN DEĞİL"
        If IsAffirmativeReportValue(DataService.GetValue(row, "IsSuitable")) Then Return "UYGUN"
        Return "TAMAMLANAN"
    End Function

    Private Shared Function IsAffirmativeReportValue(value As String) As Boolean
        Select Case If(value, "").Trim().ToUpperInvariant()
            Case "1", "TRUE", "YES", "Y", "EVET", "X", "UYGUN"
                Return True
            Case Else
                Return False
        End Select
    End Function

    Private Shared Function FormatReportDateTime(value As String) As String
        Dim parsed As DateTime
        If DateTime.TryParse(value, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.AllowWhiteSpaces, parsed) OrElse
           DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, parsed) Then
            Return parsed.ToString("dd.MM.yyyy HH:mm")
        End If
        Return If(value, "").Trim()
    End Function

    Private Shared Sub AppendSummaryCell(html As StringBuilder, caption As String, value As String, backColor As String, foreColor As String)
        html.AppendLine("<td style=""min-width:90px;border:1px solid #cbd5e1;padding:8px 12px;background:" & backColor & ";color:" & foreColor & ";"">" &
                        "<div style=""font-size:11px;font-weight:600;"">" & EncodeHtml(caption) & "</div>" &
                        "<div style=""font-size:18px;font-weight:700;"">" & EncodeHtml(value) & "</div></td>")
    End Sub

    Private Shared Sub AppendReportCell(html As StringBuilder, value As String, preserveLines As Boolean)
        Dim encoded = EncodeHtml(value)
        If preserveLines Then encoded = encoded.Replace(vbCrLf, "<br>").Replace(vbCr, "<br>").Replace(vbLf, "<br>")
        html.AppendLine("<td style=""border:1px solid #cbd5e1;padding:7px;vertical-align:top;"">" & encoded & "</td>")
    End Sub

    Private Shared Function EncodeHtml(value As String) As String
        Return WebUtility.HtmlEncode(If(value, ""))
    End Function

    Private Sub Grid_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return

        If grid.CurrentCell Is Nothing OrElse grid.CurrentCell.RowIndex <> e.RowIndex Then
            grid.CurrentCell = grid.Rows(e.RowIndex).Cells.Cast(Of DataGridViewCell)().
                FirstOrDefault(Function(cell) cell.Visible)
        End If
        grid.Rows(e.RowIndex).Selected = True

        OpenSelectedRecordDetails()
    End Sub

    Private Sub OpenNewDeliveryDialog()
        If Not AppState.CanCreateMechanismQualityDelivery Then
            MessageBox.Show("Yeni teslim oluşturma yetkiniz yok.", "Yetki yok", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Using detail As New FrmMechanismQualityControlDetail(Nothing)
            If detail.ShowDialog(Me) = DialogResult.OK Then
                ReloadAfterDetail(detail.AffectedControlId)
            End If
        End Using
    End Sub

    Private Sub OpenSelectedRecordDetails()
        Dim row = CurrentGridRowToDictionary()
        If row Is Nothing Then
            MessageBox.Show("Lütfen açılacak satırı seçin.", "Satır seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Using detail As New FrmMechanismQualityControlDetail(row)
            If detail.ShowDialog(Me) = DialogResult.OK Then
                ReloadAfterDetail(detail.AffectedControlId)
            End If
        End Using
    End Sub

    Private Sub ReloadAfterDetail(controlId As String)
        suppressDraftPrompt = True
        Try
            LoadProducts()
            LoadGrid()
            ClearForm()
            If Not String.IsNullOrWhiteSpace(controlId) Then TrySelectGridRow(controlId)
        Finally
            suppressDraftPrompt = False
        End Try
    End Sub

    Private Function CurrentGridRowToDictionary() As Dictionary(Of String, String)
        If grid.CurrentRow Is Nothing Then Return Nothing

        Dim result As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        For Each header In DataService.MechanismQualityControlHeaders
            If grid.Columns.Contains(header) Then
                result(header) = Convert.ToString(grid.CurrentRow.Cells(header).Value)
            Else
                result(header) = ""
            End If
        Next

        Return result
    End Function

    Private Sub ApplyResponsiveLayout()
        If mainLayoutHost Is Nothing OrElse mainLayoutHost.IsDisposed OrElse mainLayoutHost.RowStyles.Count < 3 Then Return

        Dim logicalHeight = ResponsiveFormService.GetLogicalClientHeight(Me)
        If logicalHeight <= 0 Then logicalHeight = ResponsiveFormService.GetLogicalWorkingAreaHeight(Me)

        Dim tightHeight = logicalHeight > 0 AndAlso logicalHeight < 820
        Dim veryTightHeight = logicalHeight > 0 AndAlso logicalHeight < 720

        mainLayoutHost.SuspendLayout()
        Try
            mainLayoutHost.Padding = If(tightHeight,
                                        New Padding(8, 8, 8, 8),
                                        New Padding(12))

            Dim availableWidth = Math.Max(520, mainLayoutHost.ClientSize.Width - mainLayoutHost.Padding.Horizontal)
            Dim dpiScale = Math.Max(1.0R, DeviceDpi / 96.0R)
            Dim actionRequiredHeight = If(
                actionPanelHost Is Nothing,
                0,
                actionPanelHost.GetPreferredSize(New Size(availableWidth, 0)).Height + actionPanelHost.Margin.Vertical)
            Dim filterRequiredHeight = If(
                filterPanelHost Is Nothing,
                0,
                filterPanelHost.GetPreferredSize(New Size(availableWidth, 0)).Height + filterPanelHost.Margin.Vertical)

            actionRequiredHeight = Math.Max(actionRequiredHeight, CInt(Math.Ceiling(54 * dpiScale)))
            filterRequiredHeight = Math.Max(filterRequiredHeight, CInt(Math.Ceiling(54 * dpiScale)))

            Dim actionProfileHeight = If(veryTightHeight, 48, If(tightHeight, 52, 56))
            Dim filterProfileHeight = If(veryTightHeight, 40, If(tightHeight, 44, 50))

            mainLayoutHost.RowStyles(0).Height = Math.Max(actionProfileHeight, actionRequiredHeight)
            mainLayoutHost.RowStyles(1).Height = Math.Max(filterProfileHeight, filterRequiredHeight)

            grid.ColumnHeadersHeight = If(veryTightHeight, 36, If(tightHeight, 38, 42))
            grid.RowTemplate.Height = If(veryTightHeight, 28, If(tightHeight, 30, 32))

            Dim gridHost = TryCast(gridPanelHost, TableLayoutPanel)
            If gridHost IsNot Nothing AndAlso gridHost.RowStyles.Count > 0 Then
                gridHost.RowStyles(0).Height = If(veryTightHeight, 30.0F, If(tightHeight, 32.0F, 36.0F))
            End If
        Finally
            mainLayoutHost.ResumeLayout(True)
        End Try
    End Sub

    Private Sub ConfigureGrid()
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
        grid.BorderStyle = BorderStyle.None
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
        grid.GridColor = Color.FromArgb(228, 233, 240)
        grid.EnableHeadersVisualStyles = False
        grid.Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(236, 242, 249)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(31, 41, 55)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True
        grid.ColumnHeadersHeight = 48
        grid.DefaultCellStyle.BackColor = Color.White
        grid.DefaultCellStyle.ForeColor = Color.FromArgb(31, 41, 55)
        grid.DefaultCellStyle.Padding = New Padding(5, 4, 5, 4)
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(214, 229, 250)
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(28, 56, 95)
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 251, 253)
        grid.RowTemplate.Height = 34
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders
        grid.ScrollBars = ScrollBars.Both

        grid.Columns.Clear()
        grid.Columns.Add(MakeColumn("CreatedAt", "TESLİM TARİHİ/SAATİ", 12.0F, 115))
        grid.Columns.Add(MakeColumn("ControlDateTime", "KONTROL TARİHİ/SAATİ", 12.0F, 115))
        grid.Columns.Add(MakeColumn("IncomingEyeCount", "GELEN GÖZ SAYISI", 10.0F, 80))
        grid.Columns.Add(MakeColumn("DeliveredBy", "TESLİM EDEN", 11.0F, 85))
        grid.Columns.Add(MakeColumn("ProductNameCode", "ÜRÜNLER / ADI VE KODU", 20.0F, 130))
        grid.Columns.Add(MakeColumn("MountedMechanismCounter", "MONTAJI YAPILAN MEKANİZMA/SAYAÇ", 23.0F, 150))
        grid.Columns.Add(MakeColumn("DeliveryExplanation", "TESLİM AÇIKLAMASI", 13.0F, 110))
        grid.Columns.Add(MakeColumn("ControlExplanation", "KONTROL AÇIKLAMASI", 13.0F, 110))
        grid.Columns.Add(MakeColumn("IsSuitable", "UYGUN", 7.0F, 60))
        grid.Columns.Add(MakeColumn("IsNotSuitable", "UYGUN DEĞİL", 8.0F, 65))
        grid.Columns.Add(MakeColumn("ControlledBy", "KONTROL EDEN", 12.0F, 90))
        grid.Columns.Add(MakeHiddenColumn("ControlId"))
        grid.Columns.Add(MakeHiddenColumn("Status"))

        For Each column As DataGridViewColumn In grid.Columns
            If Not column.Visible Then Continue For
            column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter
            column.SortMode = DataGridViewColumnSortMode.NotSortable
        Next

        grid.Columns("IsSuitable").DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
        grid.Columns("IsNotSuitable").DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
        For Each columnName In {"ProductNameCode", "DeliveryExplanation", "ControlExplanation"}
            grid.Columns(columnName).DefaultCellStyle.WrapMode = DataGridViewTriState.True
        Next
        For Each columnName In {"CreatedAt", "ControlDateTime", "IncomingEyeCount", "DeliveredBy", "MountedMechanismCounter", "ControlledBy"}
            grid.Columns(columnName).DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
        Next

        AddHandler grid.SelectionChanged, AddressOf Grid_SelectionChanged
        AddHandler grid.CellFormatting, AddressOf Grid_CellFormatting
        AddHandler grid.CellToolTipTextNeeded, AddressOf Grid_CellToolTipTextNeeded
        AddHandler grid.CellDoubleClick, AddressOf Grid_CellDoubleClick
        AddHandler grid.MouseDown, AddressOf Grid_MouseDown
    End Sub

    Private Function MakeColumn(name As String, header As String, fillWeight As Single, minimumWidth As Integer) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .DataPropertyName = name,
            .HeaderText = header,
            .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            .FillWeight = fillWeight,
            .MinimumWidth = minimumWidth,
            .SortMode = DataGridViewColumnSortMode.Automatic
        }
    End Function

    Private Function MakeHiddenColumn(name As String) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .DataPropertyName = name,
            .Visible = False
        }
    End Function

    Private Function CreateFieldLabel(text As String) As Label
        Return New Label() With {
            .Text = text,
            .Dock = DockStyle.Fill,
            .Margin = New Padding(4, 4, 2, 4),
            .Padding = New Padding(4, 0, 0, 0),
            .TextAlign = ContentAlignment.MiddleLeft,
            .ForeColor = Color.FromArgb(65, 76, 92),
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        }
    End Function

    Private Function CreateRightFieldLabel(text As String) As Label
        Return New Label() With {
            .Text = text,
            .Dock = DockStyle.Fill,
            .Margin = New Padding(4, 4, 2, 4),
            .Padding = New Padding(0, 0, 4, 0),
            .TextAlign = ContentAlignment.MiddleRight,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
        }
    End Function

    Private Shared Sub ConfigureTextInput(textBox As TextBox)
        textBox.BorderStyle = BorderStyle.FixedSingle
        textBox.Font = New Font("Segoe UI", 9.25F, FontStyle.Regular)
        textBox.BackColor = Color.White
        textBox.ForeColor = Color.FromArgb(31, 41, 55)
    End Sub

    Private Shared Sub ConfigureSelectionInput(comboBox As ComboBox)
        comboBox.FlatStyle = FlatStyle.Flat
        comboBox.Font = New Font("Segoe UI", 9.25F, FontStyle.Regular)
        comboBox.BackColor = Color.White
        comboBox.ForeColor = Color.FromArgb(31, 41, 55)
        comboBox.IntegralHeight = False
    End Sub

    Private Shared Sub ConfigureNumericInput(input As NumericUpDown)
        input.BorderStyle = BorderStyle.FixedSingle
        input.Font = New Font("Segoe UI", 9.25F, FontStyle.Regular)
        input.BackColor = Color.White
        input.ForeColor = Color.FromArgb(31, 41, 55)
        input.TextAlign = HorizontalAlignment.Left
    End Sub

    Private Sub ConfigureActionButton(button As Button, text As String, backColor As Color, foreColor As Color, width As Integer)
        button.Text = text
        button.Width = width
        button.Height = 34
        button.Margin = New Padding(8, 0, 8, 0)
        button.BackColor = backColor
        button.ForeColor = foreColor
        button.FlatStyle = FlatStyle.Flat
        button.Cursor = Cursors.Hand
        button.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        button.UseVisualStyleBackColor = False
        button.FlatAppearance.BorderSize = 2
        button.FlatAppearance.BorderColor = foreColor
        button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(backColor, 0.08F)
        AddButtonHoverEffect(button, ControlPaint.Light(backColor, 0.2F))
        AddHandler button.EnabledChanged,
            Sub()
                ApplyActionButtonEnabledStyle(button, backColor, foreColor)
            End Sub
        ApplyActionButtonEnabledStyle(button, backColor, foreColor)
    End Sub

    Private Sub ApplyActionButtonEnabledStyle(button As Button, activeBackColor As Color, activeForeColor As Color)
        If button.Enabled Then
            button.BackColor = activeBackColor
            button.ForeColor = activeForeColor
            button.FlatAppearance.BorderColor = activeForeColor
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(activeBackColor, 0.08F)
            button.Cursor = Cursors.Hand
        Else
            Dim disabledBackColor = Color.FromArgb(232, 232, 232)
            button.BackColor = disabledBackColor
            button.ForeColor = Color.FromArgb(145, 145, 145)
            button.FlatAppearance.BorderColor = Color.FromArgb(175, 175, 175)
            button.FlatAppearance.MouseDownBackColor = disabledBackColor
            button.Cursor = Cursors.Default
        End If
    End Sub

    Private Sub AddButtonHoverEffect(button As Button, hoverColor As Color)
        Dim normalColor = button.BackColor
        AddHandler button.MouseEnter,
            Sub()
                If button.Enabled Then button.BackColor = hoverColor
            End Sub
        AddHandler button.MouseLeave,
            Sub()
                If button.Enabled Then button.BackColor = normalColor
            End Sub
    End Sub

    Private Sub LoadProducts()
        Dim products = DataService.GetProducts(False)
        Dim productsByTr = products.
            Where(Function(p) If(p.TrCode, "").Trim() <> "").
            GroupBy(Function(p) NormalizeTrKey(p.TrCode), StringComparer.OrdinalIgnoreCase).
            Where(Function(g) g.Key <> "").
            ToDictionary(
                Function(g) g.Key,
                Function(g)
                    Return g.
                        OrderByDescending(Function(p) String.Equals(p.IsActive, "YES", StringComparison.OrdinalIgnoreCase)).
                        ThenByDescending(Function(p) If(p.DrawingRev, "")).
                        FirstOrDefault()
                End Function,
                StringComparer.OrdinalIgnoreCase)

        productCavityCountByTr = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        For Each pair In productsByTr
            If pair.Value Is Nothing Then Continue For

            Dim cavityCount As Integer
            If Integer.TryParse(SafeText(pair.Value.MoldCavityCount), cavityCount) AndAlso cavityCount > 0 Then
                productCavityCountByTr(pair.Key) = cavityCount
            End If
        Next

        Dim options As New List(Of String)()
        Dim planTrCodes As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        For Each row In DataService.GetMoldConnectionPlanRows()
            AddPlanProductOption(options, planTrCodes, row, "CurrentTrCode", "CurrentMoldNo", "CurrentPlasticCode", "ÇALIŞAN", products)
            AddPlanProductOption(options, planTrCodes, row, "FirstTrCode", "FirstMoldNo", "FirstPlasticCode", "1. BAĞLANACAK", products)
            AddPlanProductOption(options, planTrCodes, row, "SecondTrCode", "SecondMoldNo", "SecondPlasticCode", "2. BAĞLANACAK", products)
        Next

        If chkIncludeAllProducts.Checked Then
            For Each product In products.
                Where(Function(p) If(p.TrCode, "").Trim() <> "").
                OrderBy(Function(p) TrNumericSortValue(p.TrCode)).
                ThenBy(Function(p) FormatTrCode(p.TrCode), StringComparer.OrdinalIgnoreCase).
                ThenBy(Function(p) p.ProductName)

                Dim trKey = NormalizeTrKey(product.TrCode)
                If trKey = "" OrElse planTrCodes.Contains(trKey) Then Continue For

                Dim productName = ProductNameResolver.Resolve(products, product.TrCode, product.PlasticCode, product.MoldCode)
                Dim displayText = BuildProductDisplayText(FormatTrCode(product.TrCode), productName, "TÜM ÜRÜNLER", "", "")
                If Not options.Contains(displayText, StringComparer.OrdinalIgnoreCase) Then options.Add(displayText)
            Next
        End If

        productOptions = options.
            Distinct(StringComparer.OrdinalIgnoreCase).
            OrderBy(Function(x) TrNumericSortValue(x)).
            ThenBy(Function(x) x, StringComparer.OrdinalIgnoreCase).
            ToList()

        ApplyProductSelectionFilter()
    End Sub

    Private Sub AddPlanProductOption(options As List(Of String),
                                     planTrCodes As HashSet(Of String),
                                     row As Dictionary(Of String, String),
                                     trColumn As String,
                                     moldColumn As String,
                                     plasticColumn As String,
                                     sourceText As String,
                                     products As IEnumerable(Of ProductInfo))
        Dim rawTrCode = SafeText(DataService.GetValue(row, trColumn))
        Dim trKey = NormalizeTrKey(rawTrCode)
        If trKey = "" Then Return

        planTrCodes.Add(trKey)

        Dim machineText = SafeText(DataService.GetValue(row, "MachineNo"))
        If machineText = "" Then machineText = SafeText(DataService.GetValue(row, "MachineName"))

        Dim moldText = SafeText(DataService.GetValue(row, moldColumn))
        If moldText = "" AndAlso sourceText = "ÇALIŞAN" Then
            moldText = SafeText(DataService.GetValue(row, "RunningMolds"))
        End If

        Dim plasticCode = SafeText(DataService.GetValue(row, plasticColumn))
        Dim productName = ProductNameResolver.Resolve(products, rawTrCode, plasticCode, moldText)

        options.Add(BuildProductDisplayText(FormatTrCode(rawTrCode), productName, sourceText, machineText, moldText))
    End Sub

    Private Function BuildProductDisplayText(trCode As String,
                                             productName As String,
                                             sourceText As String,
                                             machineText As String,
                                             moldText As String) As String
        trCode = SafeText(trCode)
        productName = SafeText(productName)
        sourceText = SafeText(sourceText)
        machineText = SafeText(machineText)
        moldText = SafeText(moldText)

        Dim parts As New List(Of String) From {trCode, productName}
        If sourceText <> "" Then parts.Add(sourceText)
        If machineText <> "" Then parts.Add("Makine: " & machineText)
        If moldText <> "" Then parts.Add("Kalıp: " & moldText)
        Return String.Join(" | ", parts)
    End Function

    Private Shared Function SafeText(value As String) As String
        Return If(value, "").Trim()
    End Function

    Private Shared Function FormatTrCode(value As String) As String
        Dim raw = SafeText(value).ToUpperInvariant()
        If raw = "" Then Return ""

        Dim numericMatch = Regex.Match(raw, "^(\d+)(.*)$")
        If numericMatch.Success Then
            Dim numericPart = numericMatch.Groups(1).Value
            Dim numericSuffix = numericMatch.Groups(2).Value.Trim()
            Return "TR " & numericPart & If(numericSuffix = "", "", " " & numericSuffix)
        End If

        Dim match = Regex.Match(raw, "^TR[\s\-]*(\d+)(.*)$", RegexOptions.IgnoreCase)
        If Not match.Success Then Return raw

        Dim numberPart = match.Groups(1).Value
        Dim suffix = match.Groups(2).Value.Trim()
        Return "TR " & numberPart & If(suffix = "", "", " " & suffix)
    End Function

    Private Shared Function NormalizeTrKey(value As String) As String
        Return Regex.Replace(FormatTrCode(value), "\s+", "").ToUpperInvariant()
    End Function

    Private Shared Function TrNumericSortValue(value As String) As Long
        Dim formatted = FormatTrCode(value)
        Dim match = Regex.Match(formatted, "^TR\s+(\d+)", RegexOptions.IgnoreCase)
        If Not match.Success Then Return Long.MaxValue

        Dim numberValue As Long
        If Long.TryParse(match.Groups(1).Value, numberValue) Then Return numberValue
        Return Long.MaxValue
    End Function

    Private Sub ProductSelectionFilterChanged(sender As Object, e As EventArgs)
        ApplyProductSelectionFilter()
    End Sub

    Private Sub ProductNameCodeTextUpdated(sender As Object, e As EventArgs)
        If isUpdatingProductOptions OrElse
           isClearingForm OrElse
           selectedControlId <> "" OrElse
           Not cboProductNameCode.Enabled Then
            Return
        End If

        ApplyProductSelectionFilter(cboProductNameCode.Text, True)
    End Sub

    Private Sub ProductSelectionChanged(sender As Object, e As EventArgs)
        If isClearingForm OrElse selectedControlId <> "" OrElse Not cboProductNameCode.Enabled Then Return
        ApplySelectedProductEyeCount()
    End Sub

    Private Sub ApplySelectedProductEyeCount()
        Dim productText = cboProductNameCode.Text.Trim()
        If productText = "" Then Return

        Dim separatorIndex = productText.IndexOf("|"c)
        Dim trText = If(separatorIndex >= 0, productText.Substring(0, separatorIndex), productText)
        Dim trKey = NormalizeTrKey(trText)

        Dim cavityCount As Integer = 1
        productCavityCountByTr.TryGetValue(trKey, cavityCount)
        cavityCount = Math.Max(CInt(numIncomingEyeCount.Minimum), Math.Min(CInt(numIncomingEyeCount.Maximum), cavityCount))
        numIncomingEyeCount.Value = cavityCount
    End Sub

    Private Function TryGetSelectedProduct(ByRef productText As String, showMessage As Boolean) As Boolean
        productText = cboProductNameCode.Text.Trim()
        If productText = "" Then
            If showMessage Then
                MessageBox.Show("Önce listeden bir ürün seçiniz.", "Ürün seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Information)
                cboProductNameCode.Focus()
            End If
            Return False
        End If

        Dim isCurrentRecordProduct = selectedControlId <> "" AndAlso
                                     selectedProducts.Contains(productText, StringComparer.OrdinalIgnoreCase)
        If Not productOptions.Contains(productText, StringComparer.OrdinalIgnoreCase) AndAlso
           Not isCurrentRecordProduct Then
            If showMessage Then
                MessageBox.Show("Ürün, listeden seçilmelidir.", "Geçersiz ürün seçimi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                cboProductNameCode.Focus()
            End If
            Return False
        End If

        Return True
    End Function

    Private Sub LoadSelectedProducts(serializedProducts As String)
        selectedProducts = DeserializeSelectedProducts(serializedProducts)
        cboProductNameCode.SelectedIndex = -1
        cboProductNameCode.Text = If(selectedProducts.FirstOrDefault(), "")
    End Sub

    Private Shared Function SerializeSelectedProducts(products As IEnumerable(Of String)) As String
        Return String.Join(
            Environment.NewLine,
            products.
                Select(Function(product) If(product, "").Trim()).
                Where(Function(product) product <> "").
                Distinct(StringComparer.OrdinalIgnoreCase))
    End Function

    Private Shared Function DeserializeSelectedProducts(serializedProducts As String) As List(Of String)
        Return If(serializedProducts, "").
            Replace(vbCrLf, vbLf).
            Replace(vbCr, vbLf).
            Split({vbLf}, StringSplitOptions.RemoveEmptyEntries).
            Select(Function(product) product.Trim()).
            Where(Function(product) product <> "").
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToList()
    End Function

    Private Sub Refresh_Click(sender As Object, e As EventArgs)
        If Not ConfirmDiscardControlDraft() Then Return

        suppressDraftPrompt = True
        Try
            LoadProducts()
            LoadGrid()
            ClearForm()
        Finally
            suppressDraftPrompt = False
        End Try
    End Sub

    Private Sub New_Click(sender As Object, e As EventArgs)
        If Not ConfirmDiscardControlDraft() Then Return
        ClearForm()
    End Sub

    Private Sub MechanismQualityControl_FormClosing(sender As Object, e As FormClosingEventArgs)
        If e.CloseReason = CloseReason.WindowsShutDown OrElse
           e.CloseReason = CloseReason.TaskManagerClosing OrElse
           e.CloseReason = CloseReason.ApplicationExitCall Then Return

        If Not ConfirmDiscardControlDraft() Then e.Cancel = True
    End Sub

    Private Function ConfirmDiscardControlDraft() As Boolean
        If suppressDraftPrompt OrElse Not HasUnsavedControlDraft() Then Return True

        Return MessageBox.Show(
            "Formda kaydedilmemiş ürün veya kontrol bilgileri var." & Environment.NewLine &
            "Bu değişikliklerden vazgeçilsin mi?",
            "Kaydedilmemiş bilgiler",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) = DialogResult.Yes
    End Function

    Private Function HasUnsavedControlDraft() As Boolean
        If selectedControlId = "" Then
            Return cboProductNameCode.Text.Trim() <> "" OrElse
                   txtMountedMechanismCounter.Text.Trim() <> "" OrElse
                   txtDeliveryExplanation.Text.Trim() <> "" OrElse
                   numIncomingEyeCount.Value <> 1D
        End If
        If Not AppState.CanReviewMechanismQualityDelivery Then Return False

        Return Not String.Equals(
                   txtMountedMechanismCounter.Text.Trim(),
                   loadedMountedMechanismCounter,
                   StringComparison.Ordinal) OrElse
               Not String.Equals(
                   cboProductNameCode.Text.Trim(),
                   loadedProductsSerialized,
                   StringComparison.Ordinal) OrElse
               Not String.Equals(
                   txtControlExplanation.Text.Trim(),
                   loadedControlExplanation,
                   StringComparison.Ordinal)
    End Function

    Private Sub ApplyProductSelectionFilter(Optional productEntryText As String = Nothing,
                                            Optional openDropDown As Boolean = False)
        Dim selectedText = cboProductNameCode.Text.Trim()
        Dim filterText = txtProductSelectionFilter.Text.Trim()
        Dim filtered = productOptions.AsEnumerable()

        If filterText <> "" Then
            filtered = filtered.Where(Function(item) ProductMatchesFilter(item, filterText))
        End If

        If productEntryText IsNot Nothing AndAlso productEntryText.Trim() <> "" Then
            filtered = filtered.Where(Function(item) ProductMatchesFilter(item, productEntryText))
        End If

        Dim visibleOptions = filtered.ToArray()
        Dim caretPosition = cboProductNameCode.SelectionStart
        isUpdatingProductOptions = True
        cboProductNameCode.BeginUpdate()
        Try
            cboProductNameCode.Items.Clear()
            cboProductNameCode.Items.AddRange(visibleOptions)

            If productEntryText IsNot Nothing Then
                cboProductNameCode.SelectedIndex = -1
                cboProductNameCode.Text = productEntryText
                cboProductNameCode.SelectionStart = Math.Min(caretPosition, cboProductNameCode.Text.Length)
                cboProductNameCode.SelectionLength = 0
            ElseIf selectedText <> "" AndAlso visibleOptions.Contains(selectedText, StringComparer.OrdinalIgnoreCase) Then
                cboProductNameCode.Text = selectedText
            Else
                cboProductNameCode.SelectedIndex = -1
                cboProductNameCode.Text = ""
            End If
        Finally
            cboProductNameCode.EndUpdate()
            isUpdatingProductOptions = False
        End Try

        If openDropDown AndAlso cboProductNameCode.Focused Then
            cboProductNameCode.DroppedDown = visibleOptions.Length > 0
        End If
    End Sub

    Private Shared Function ProductMatchesFilter(item As String, filterText As String) As Boolean
        Dim itemText = If(item, "")
        Dim compactItem = Regex.Replace(itemText, "[\s\-]+", "")
        Dim tokens = If(filterText, "").Split(
            New Char() {" "c, ";"c, ","c, "|"c},
            StringSplitOptions.RemoveEmptyEntries)

        Return tokens.All(
            Function(token)
                Dim compactToken = Regex.Replace(token, "[\s\-]+", "")
                Return itemText.IndexOf(token, StringComparison.CurrentCultureIgnoreCase) >= 0 OrElse
                       (compactToken <> "" AndAlso
                        compactItem.IndexOf(compactToken, StringComparison.CurrentCultureIgnoreCase) >= 0)
            End Function)
    End Function

    Private Sub ApplyRoleMode()
        btnSubmit.Visible = AppState.CanCreateMechanismQualityDelivery
        btnSuitable.Visible = AppState.CanReviewMechanismQualityDelivery
        btnNotSuitable.Visible = AppState.CanReviewMechanismQualityDelivery
        btnToggleListFocus.Visible = AppState.CanCreateMechanismQualityDelivery
        btnToggleListFocus.Enabled = AppState.CanCreateMechanismQualityDelivery
        btnDeleteRecord.Visible = AppState.IsAdmin
        btnDeleteRecord.Enabled = False
        btnEmailRecipients.Visible = AppState.CanManageMechanismQualityEmailRecipients
        btnEmailRecipients.Enabled = AppState.CanManageMechanismQualityEmailRecipients
        ClearForm()
    End Sub

    Private Sub UpdateSummaryCards(visibleCount As Integer,
                                   pendingCount As Integer,
                                   completedCount As Integer,
                                   todayDeliveredCount As Integer,
                                   todayControlledCount As Integer)
        lblVisibleCount.Text = visibleCount.ToString()
        lblPendingCount.Text = pendingCount.ToString()
        lblCompletedCount.Text = completedCount.ToString()
        lblCount.Text = $"Bugün teslim: {todayDeliveredCount} | Bugün kontrol: {todayControlledCount}"
        lblCount.AccessibleDescription = $"Bugün teslim edilen kayıt: {todayDeliveredCount}; bugün kontrol edilen kayıt: {todayControlledCount}"
    End Sub

    Private Shared Function IsSameLocalDate(value As String, targetDate As Date) As Boolean
        value = If(value, "").Trim()
        If value = "" Then Return False

        Dim parsed As DateTime
        Dim formats = New String() {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd H:mm:ss",
            "yyyy-MM-dd",
            "dd.MM.yyyy HH:mm:ss",
            "d.M.yyyy HH:mm:ss",
            "dd.MM.yyyy",
            "d.M.yyyy",
            "dd/MM/yyyy HH:mm:ss",
            "d/M/yyyy HH:mm:ss",
            "dd/MM/yyyy",
            "d/M/yyyy"
        }

        If DateTime.TryParseExact(
            value,
            formats,
            CultureInfo.GetCultureInfo("tr-TR"),
            DateTimeStyles.AllowWhiteSpaces,
            parsed) Then
            Return parsed.Date = targetDate.Date
        End If

        If DateTime.TryParse(value, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.AllowWhiteSpaces, parsed) OrElse
           DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, parsed) Then
            Return parsed.Date = targetDate.Date
        End If

        Return False
    End Function

    Private Sub SetModeMessage(message As String, backColor As Color, foreColor As Color)
        lblMode.Text = message
        lblMode.BackColor = backColor
        lblMode.ForeColor = foreColor
    End Sub

    Private Sub ClearForm()
        isClearingForm = True
        Try
            selectedControlId = ""
            selectedRecordIsPending = False
            grid.ClearSelection()
            grid.CurrentCell = Nothing

            numIncomingEyeCount.Value = 1D
            txtProductSelectionFilter.Clear()
            cboProductNameCode.SelectedIndex = -1
            cboProductNameCode.Text = ""
            selectedProducts = New List(Of String)()
            txtMountedMechanismCounter.Clear()
            txtDeliveryExplanation.Clear()
            txtControlExplanation.Clear()
            loadedMountedMechanismCounter = ""
            loadedControlExplanation = ""
            loadedProductsSerialized = ""
            lblDeliveredBy.Text = AppState.CurrentUserName
            lblControlledBy.Text = ""

            Dim canCreate = AppState.CanCreateMechanismQualityDelivery
            numIncomingEyeCount.Enabled = canCreate
            txtProductSelectionFilter.Enabled = canCreate
            chkIncludeAllProducts.Enabled = canCreate
            cboProductNameCode.Enabled = canCreate
            txtMountedMechanismCounter.Enabled = False
            txtDeliveryExplanation.ReadOnly = Not canCreate
            txtControlExplanation.ReadOnly = True
            btnSubmit.Enabled = canCreate
            btnSuitable.Enabled = False
            btnNotSuitable.Enabled = False
            btnDeleteRecord.Enabled = False

            Dim roleText = AppState.NormalizeRole(AppState.CurrentRole)
            If AppState.CanCreateMechanismQualityDelivery AndAlso AppState.CanReviewMechanismQualityDelivery Then
                SetModeMessage($"Rol: {roleText} | Yeni teslim için 'Yeni Teslim'; detay veya kontrol için listedeki satıra çift tıklayın.",
                               Color.FromArgb(237, 244, 255),
                               Color.FromArgb(31, 71, 136))
            ElseIf AppState.IsMechanismQualityControlUser Then
                SetModeMessage($"Rol: {roleText} | Bekleyen kaydı çift tıklayın; açılan pencerede mekanizma/sayaç bilgisini girip sonucu belirleyin.",
                               Color.FromArgb(255, 247, 222),
                               Color.FromArgb(128, 88, 0))
            ElseIf canCreate Then
                SetModeMessage($"Rol: {roleText} | Yeni teslim için 'Yeni Teslim' düğmesini kullanın; kayıt detayları için satıra çift tıklayın.",
                               Color.FromArgb(238, 249, 241),
                               Color.FromArgb(26, 102, 65))
            Else
                SetModeMessage($"Rol: {roleText} | Salt okunur: kayıtları ve satır ayrıntılarını görüntüleyebilirsiniz.",
                               Color.FromArgb(243, 244, 246),
                               Color.FromArgb(75, 85, 99))
            End If
        Finally
            isClearingForm = False
        End Try
    End Sub

    Private Sub Submit_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.CanCreateMechanismQualityDelivery, "Mekanizma Kontrol Teslimi")

            Dim productNameCode As String = ""
            If Not TryGetSelectedProduct(productNameCode, True) Then Return
            selectedProducts = New List(Of String) From {productNameCode}
            Dim mechanismCounter = ""
            Dim controlId = "MKC-" & DateTime.Now.ToString("yyyyMMdd-HHmmss") & "-" &
                            Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant()
            Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")

            Dim row As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                {"ControlId", controlId},
                {"Status", "PENDING"},
                {"CreatedAt", nowText},
                {"ControlDateTime", ""},
                {"IncomingEyeCount", CInt(numIncomingEyeCount.Value).ToString()},
                {"DeliveredBy", AppState.CurrentUserName},
                {"ProductNameCode", productNameCode},
                {"MountedMechanismCounter", mechanismCounter},
                {"Explanation", txtDeliveryExplanation.Text.Trim()},
                {"DeliveryExplanation", txtDeliveryExplanation.Text.Trim()},
                {"ControlExplanation", ""},
                {"IsSuitable", ""},
                {"IsNotSuitable", ""},
                {"ControlledBy", ""},
                {"ControlledAt", ""},
                {"CreatedComputerName", Environment.MachineName},
                {"ControlledComputerName", ""}
            }

            DataService.AppendMechanismQualityControl(row)
            AuditService.Log("MECHANISM_QUALITY_DELIVERY_CREATE", "", "",
                             $"ControlId={controlId}; Product={productNameCode}; EyeCount={CInt(numIncomingEyeCount.Value)}")

            LoadGrid()
            ClearForm()
            MessageBox.Show("Teslim kaydı Mekanizma Kalite Kontrol'e gönderildi." & Environment.NewLine &
                            "Kayıt No: " & controlId,
                            "Teslim oluşturuldu", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Teslim kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub CompleteSelected(isSuitable As Boolean)
        Try
            AuthorizationService.Require(AppState.CanReviewMechanismQualityDelivery, "Mekanizma Kalite Kontrol Sonuçlandırma")

            If selectedControlId = "" Then
                MessageBox.Show("Önce bekleyen bir teslim kaydı seçiniz.", "Kayıt seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            If txtMountedMechanismCounter.Text.Trim() = "" Then
                MessageBox.Show("Montajı yapılan mekanizma/sayaç bilgisi kontrol aşamasında zorunludur.",
                                "Eksik bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                txtMountedMechanismCounter.Focus()
                Return
            End If

            Dim productNameCode As String = ""
            If Not TryGetSelectedProduct(productNameCode, True) Then Return

            If Not isSuitable AndAlso txtControlExplanation.Text.Trim() = "" Then
                MessageBox.Show("UYGUN DEĞİL sonucu için kontrol açıklaması zorunludur.", "Açıklama gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                txtControlExplanation.Focus()
                Return
            End If

            Dim resultText = If(isSuitable, "UYGUN", "UYGUN DEĞİL")
            If MessageBox.Show("Seçili kayıt " & resultText & " olarak tamamlanacak." & Environment.NewLine &
                               "Devam edilsin mi?",
                               "Kontrolü tamamla", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                               MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then
                Return
            End If

            DataService.CompleteMechanismQualityControl(
                selectedControlId,
                isSuitable,
                AppState.CurrentUserName,
                txtControlExplanation.Text,
                txtMountedMechanismCounter.Text,
                productNameCode)

            If Not isSuitable Then
                Dim mailError As String = ""
                If Not MechanismQualityEmailNotificationService.TryNotifyNotSuitable(selectedControlId, mailError) AndAlso mailError <> "" Then
                    AppNotificationService.ShowWarning("Uygun değil maili gönderilemedi", mailError)
                End If
            End If

            AuditService.Log("MECHANISM_QUALITY_CONTROL_COMPLETE", "", "",
                             $"ControlId={selectedControlId}; Result={resultText}; Product={productNameCode}")

            suppressDraftPrompt = True
            Try
                LoadGrid()
                ClearForm()
            Finally
                suppressDraftPrompt = False
            End Try
            MessageBox.Show("Mekanizma kalite kontrolü " & resultText & " olarak tamamlandı.",
                            "Kontrol tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Kontrol tamamlanamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub DeleteSelectedRecord_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.IsAdmin, "Mekanizma Kalite Kontrol Kaydı Silme")

            Dim selected = CurrentGridRowToDictionary()
            If selected Is Nothing Then
                MessageBox.Show("Lütfen silinecek kaydı seçin.",
                                "Kayıt seçilmedi",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information)
                Return
            End If

            Dim controlId = DataService.GetValue(selected, "ControlId").Trim()
            Dim product = DataService.GetValue(selected, "ProductNameCode").Trim()
            Dim status = DataService.GetValue(selected, "Status").Trim()
            If controlId = "" Then
                MessageBox.Show("Seçili satırın kayıt numarası okunamadı.",
                                "Kayıt silinemedi",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error)
                Return
            End If

            Dim firstAnswer = MessageBox.Show(
                "Seçili mekanizma kalite kontrol kaydı kalıcı olarak silinecek." & Environment.NewLine & Environment.NewLine &
                "Kayıt No: " & controlId & Environment.NewLine &
                "Ürün: " & If(product = "", "-", product) & Environment.NewLine &
                "Durum: " & If(String.Equals(status, "PENDING", StringComparison.OrdinalIgnoreCase), "BEKLEYEN", "TAMAMLANAN") & Environment.NewLine & Environment.NewLine &
                "Devam edilsin mi?",
                "Kaydı sil",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2)
            If firstAnswer <> DialogResult.Yes Then Return

            Dim finalAnswer = MessageBox.Show(
                "Bu işlem geri alınamaz." & Environment.NewLine &
                "Kayıt No " & controlId & " kesin olarak silinsin mi?",
                "Son silme onayı",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Stop,
                MessageBoxDefaultButton.Button2)
            If finalAnswer <> DialogResult.Yes Then Return

            DataService.DeleteMechanismQualityControlRecord(controlId)

            suppressDraftPrompt = True
            Try
                selectedControlId = ""
                LoadGrid()
                ClearForm()
            Finally
                suppressDraftPrompt = False
            End Try

            MessageBox.Show("Mekanizma kalite kontrol kaydı silindi." & Environment.NewLine &
                            "Kayıt No: " & controlId,
                            "Kayıt silindi",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information)
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Kayıt silinemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub LoadGrid()
        Try
            Dim allRows = DataService.GetMechanismQualityControls()
            Dim rows = allRows.AsEnumerable()

            Dim statusText = If(cboStatus.SelectedItem Is Nothing, "BEKLEYEN", cboStatus.SelectedItem.ToString())
            If statusText = "BEKLEYEN" Then
                rows = rows.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "PENDING", StringComparison.OrdinalIgnoreCase))
            ElseIf statusText = "TAMAMLANAN" Then
                rows = rows.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "COMPLETED", StringComparison.OrdinalIgnoreCase))
            End If

            If dtpDeliveryDate.Checked Then
                Dim selectedDate = dtpDeliveryDate.Value.Date
                rows = rows.Where(Function(r) IsSameLocalDate(DataService.GetValue(r, "CreatedAt"), selectedDate))
            End If

            Dim filterText = txtFilter.Text.Trim()
            If filterText <> "" Then
                Dim tokens = filterText.Split(New Char() {" "c, ";"c, ","c, "|"c}, StringSplitOptions.RemoveEmptyEntries)
                rows = rows.Where(
                    Function(r)
                        Dim haystack = (DataService.GetValue(r, "ProductNameCode") & " " &
                                        DataService.GetValue(r, "MountedMechanismCounter") & " " &
                                        DataService.GetValue(r, "DeliveryExplanation") & " " &
                                        DataService.GetValue(r, "ControlExplanation") & " " &
                                        DataService.GetValue(r, "DeliveredBy") & " " &
                                        DataService.GetValue(r, "ControlledBy")).ToUpperInvariant()
                        Return tokens.All(Function(token) haystack.Contains(token.ToUpperInvariant()))
                    End Function)
            End If

            Dim list = rows.
                OrderBy(Function(r) If(String.Equals(DataService.GetValue(r, "Status"), "PENDING", StringComparison.OrdinalIgnoreCase), 0, 1)).
                ThenByDescending(Function(r) DataService.GetValue(r, "CreatedAt")).
                ToList()
            Dim products = DataService.GetProducts(False)
            Dim displayRows = list.
                Select(
                    Function(sourceRow)
                        Dim displayRow = New Dictionary(Of String, String)(sourceRow, StringComparer.OrdinalIgnoreCase)
                        displayRow("ProductNameCode") = ProductNameResolver.EnrichDisplayText(
                            products,
                            DataService.GetValue(sourceRow, "ProductNameCode"))
                        Return displayRow
                    End Function).
                ToList()
            currentRows = displayRows

            Dim dt As New DataTable()
            For Each header In DataService.MechanismQualityControlHeaders
                dt.Columns.Add(header)
            Next

            For Each row In displayRows
                Dim dataRow = dt.NewRow()
                For Each header In DataService.MechanismQualityControlHeaders
                    dataRow(header) = DataService.GetValue(row, header)
                Next
                dt.Rows.Add(dataRow)
            Next

            Dim selectionToRestore = selectedControlId
            isRestoringGridSelection = True
            Try
                grid.DataSource = dt
                grid.ClearSelection()
                grid.CurrentCell = Nothing
                If selectionToRestore <> "" Then TrySelectGridRow(selectionToRestore)
            Finally
                isRestoringGridSelection = False
            End Try

            Dim pendingCount = allRows.Where(
                Function(r) String.Equals(DataService.GetValue(r, "Status"), "PENDING", StringComparison.OrdinalIgnoreCase)).Count()
            Dim completedCount = allRows.Where(
                Function(r) String.Equals(DataService.GetValue(r, "Status"), "COMPLETED", StringComparison.OrdinalIgnoreCase)).Count()
            Dim today = Date.Today
            Dim todayDeliveredCount = allRows.Where(
                Function(r) IsSameLocalDate(DataService.GetValue(r, "CreatedAt"), today)).Count()
            Dim todayControlledCount = allRows.Where(
                Function(r)
                    Return IsSameLocalDate(DataService.GetValue(r, "ControlledAt"), today) OrElse
                           IsSameLocalDate(DataService.GetValue(r, "ControlDateTime"), today)
                End Function).Count()
            UpdateSummaryCards(dt.Rows.Count, pendingCount, completedCount, todayDeliveredCount, todayControlledCount)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Kayıtlar yüklenemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Grid_SelectionChanged(sender As Object, e As EventArgs)
        If isClearingForm OrElse isRestoringGridSelection Then Return
        If grid.CurrentRow Is Nothing OrElse Not grid.CurrentRow.Selected Then Return

        Dim newControlId = CellText("ControlId")
        If newControlId = "" Then Return

        If selectedControlId <> "" AndAlso
           Not String.Equals(selectedControlId, newControlId, StringComparison.OrdinalIgnoreCase) AndAlso
           Not ConfirmDiscardControlDraft() Then
            RestoreGridSelection(selectedControlId)
            Return
        End If

        selectedControlId = newControlId
        selectedRecordIsPending = String.Equals(CellText("Status"), "PENDING", StringComparison.OrdinalIgnoreCase)
        btnDeleteRecord.Enabled = AppState.IsAdmin

        Dim eyeCount As Decimal = 1D
        Decimal.TryParse(CellText("IncomingEyeCount"), eyeCount)
        numIncomingEyeCount.Value = Math.Max(numIncomingEyeCount.Minimum, Math.Min(numIncomingEyeCount.Maximum, eyeCount))
        cboProductNameCode.Text = ""
        LoadSelectedProducts(CellText("ProductNameCode"))
        loadedProductsSerialized = cboProductNameCode.Text.Trim()
        txtMountedMechanismCounter.Text = CellText("MountedMechanismCounter")
        txtDeliveryExplanation.Text = CellText("DeliveryExplanation")
        txtControlExplanation.Text = CellText("ControlExplanation")
        loadedMountedMechanismCounter = txtMountedMechanismCounter.Text.Trim()
        loadedControlExplanation = txtControlExplanation.Text.Trim()
        lblDeliveredBy.Text = CellText("DeliveredBy")

        numIncomingEyeCount.Enabled = False
        Dim isPending = selectedRecordIsPending
        Dim canReviewPending = isPending AndAlso AppState.CanReviewMechanismQualityDelivery
        txtProductSelectionFilter.Enabled = False
        chkIncludeAllProducts.Enabled = False
        cboProductNameCode.Enabled = False
        txtMountedMechanismCounter.Enabled = canReviewPending
        txtDeliveryExplanation.ReadOnly = True
        txtControlExplanation.ReadOnly = Not canReviewPending
        btnSuitable.Enabled = canReviewPending
        btnNotSuitable.Enabled = canReviewPending
        btnSubmit.Enabled = False
        lblControlledBy.Text = If(
            isPending AndAlso canReviewPending,
            AppState.CurrentUserName,
            CellText("ControlledBy"))
        If canReviewPending Then
            SetModeMessage("Seçili teslim kaydı kontrol bekliyor. İşlem yapmak için satıra çift tıklayın.",
                           Color.FromArgb(255, 247, 222),
                           Color.FromArgb(128, 88, 0))
        ElseIf isPending Then
            SetModeMessage("Seçili kayıt kontrol bekliyor; detayları görmek için satıra çift tıklayın.",
                           Color.FromArgb(243, 244, 246),
                           Color.FromArgb(75, 85, 99))
        Else
            SetModeMessage("Seçili kayıt tamamlanmış; detayları görmek için satıra çift tıklayın.",
                           Color.FromArgb(238, 249, 241),
                           Color.FromArgb(26, 102, 65))
        End If
    End Sub

    Private Sub Grid_MouseDown(sender As Object, e As MouseEventArgs)
        If e.Button <> MouseButtons.Left Then Return

        Dim hit = grid.HitTest(e.X, e.Y)
        If hit.Type <> DataGridViewHitTestType.None Then Return

        New_Click(grid, EventArgs.Empty)
    End Sub

    Private Sub RestoreGridSelection(controlId As String)
        isRestoringGridSelection = True
        Try
            TrySelectGridRow(controlId)
        Finally
            isRestoringGridSelection = False
        End Try
    End Sub

    Private Function TrySelectGridRow(controlId As String) As Boolean
        For Each row As DataGridViewRow In grid.Rows
            If String.Equals(
                Convert.ToString(row.Cells("ControlId").Value),
                controlId,
                StringComparison.OrdinalIgnoreCase) Then

                grid.ClearSelection()
                row.Selected = True
                Dim firstVisibleCell = row.Cells.Cast(Of DataGridViewCell)().
                    FirstOrDefault(Function(cell) cell.Visible)
                If firstVisibleCell IsNot Nothing Then grid.CurrentCell = firstVisibleCell
                Return True
            End If
        Next
        Return False
    End Function

    Private Function CellText(columnName As String) As String
        If grid.CurrentRow Is Nothing OrElse Not grid.Columns.Contains(columnName) Then Return ""
        Return Convert.ToString(grid.CurrentRow.Cells(columnName).Value)
    End Function

    Private Sub Grid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse Not grid.Columns.Contains("Status") Then Return

        Dim status = Convert.ToString(grid.Rows(e.RowIndex).Cells("Status").Value)
        If String.Equals(status, "PENDING", StringComparison.OrdinalIgnoreCase) Then
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.FromArgb(255, 249, 225)
            grid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.FromArgb(110, 79, 12)
        Else
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor =
                If(e.RowIndex Mod 2 = 0, Color.White, Color.FromArgb(247, 250, 253))
            grid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.FromArgb(31, 41, 55)
        End If

        Dim columnName = grid.Columns(e.ColumnIndex).Name
        If (String.Equals(columnName, "CreatedAt", StringComparison.OrdinalIgnoreCase) OrElse
            String.Equals(columnName, "ControlDateTime", StringComparison.OrdinalIgnoreCase)) AndAlso
           Not String.IsNullOrWhiteSpace(Convert.ToString(e.Value)) Then
            e.Value = FormatReportDateTime(Convert.ToString(e.Value))
            e.FormattingApplied = True
        End If

        If String.Equals(columnName, "IsSuitable", StringComparison.OrdinalIgnoreCase) AndAlso
           Convert.ToString(e.Value) = "X" Then
            e.Value = "Uygun"
            e.FormattingApplied = True
            e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
            e.CellStyle.BackColor = Color.FromArgb(198, 239, 206)
            e.CellStyle.ForeColor = Color.DarkGreen
            e.CellStyle.SelectionBackColor = Color.FromArgb(169, 221, 181)
            e.CellStyle.SelectionForeColor = Color.DarkGreen
            e.CellStyle.Font = New Font(grid.Font, FontStyle.Bold)
        ElseIf String.Equals(columnName, "IsNotSuitable", StringComparison.OrdinalIgnoreCase) AndAlso
               Convert.ToString(e.Value) = "X" Then
            e.Value = "Uygun Değil"
            e.FormattingApplied = True
            e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
            e.CellStyle.BackColor = Color.FromArgb(255, 199, 206)
            e.CellStyle.ForeColor = Color.DarkRed
            e.CellStyle.SelectionBackColor = Color.FromArgb(245, 166, 176)
            e.CellStyle.SelectionForeColor = Color.DarkRed
            e.CellStyle.Font = New Font(grid.Font, FontStyle.Bold)
        End If
    End Sub

    Private Sub Grid_CellToolTipTextNeeded(sender As Object, e As DataGridViewCellToolTipTextNeededEventArgs)
        If e.RowIndex < 0 OrElse e.ColumnIndex < 0 Then Return

        Dim value = Convert.ToString(grid.Rows(e.RowIndex).Cells(e.ColumnIndex).Value).Trim()
        If value <> "" Then e.ToolTipText = value
    End Sub
End Class
