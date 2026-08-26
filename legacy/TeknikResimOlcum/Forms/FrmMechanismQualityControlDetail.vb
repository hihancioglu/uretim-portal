Imports System.Drawing
Imports System.Globalization
Imports System.Linq
Imports System.Text.RegularExpressions
Imports System.Windows.Forms

Public Class FrmMechanismQualityControlDetail
    Inherits Form

    Private ReadOnly record As Dictionary(Of String, String)
    Private ReadOnly isNewRecord As Boolean

    Private ReadOnly numIncomingEyeCount As New NumericUpDown()
    Private ReadOnly txtProductSelectionFilter As New TextBox()
    Private ReadOnly chkIncludeAllProducts As New CheckBox()
    Private ReadOnly cboProductNameCode As New ComboBox()
    Private ReadOnly txtIncomingEyeCountView As New TextBox()
    Private ReadOnly txtProductNameCodeView As New TextBox()
    Private ReadOnly txtDeliveryExplanation As New TextBox()
    Private ReadOnly txtMountedMechanismCounter As New TextBox()
    Private ReadOnly txtControlExplanation As New TextBox()
    Private ReadOnly lblDeliveredBy As New Label()
    Private ReadOnly lblDeliveredAt As New Label()
    Private ReadOnly lblControlledBy As New Label()
    Private ReadOnly lblStatus As New Label()
    Private ReadOnly btnSubmit As New Button()
    Private ReadOnly btnSaveDetails As New Button()
    Private ReadOnly btnSuitable As New Button()
    Private ReadOnly btnNotSuitable As New Button()
    Private ReadOnly btnClose As New Button()

    Private productOptions As New List(Of String)()
    Private productCavityCountByTr As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
    Private selectedProducts As New List(Of String)()
    Private isClearingForm As Boolean = False
    Private isUpdatingProductOptions As Boolean = False
    Private savedChanges As Boolean = False

    Private originalMountedMechanismCounter As String = ""
    Private originalControlExplanation As String = ""
    Private originalDeliveryExplanation As String = ""
    Private originalProductText As String = ""
    Private originalEyeCount As Decimal = 1D

    Public Property AffectedControlId As String = ""

    Public Sub New(initialRecord As Dictionary(Of String, String))
        record = If(initialRecord Is Nothing,
                    Nothing,
                    New Dictionary(Of String, String)(initialRecord, StringComparer.OrdinalIgnoreCase))
        isNewRecord = record Is Nothing

        AppIconService.Apply(Me)

        Text = If(isNewRecord, "Yeni Mekanizma Kalite Kontrol Teslimi", "Mekanizma Kalite Kontrol Detayı")
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        If isNewRecord Then
            Size = New Size(780, 540)
            MinimumSize = New Size(640, 480)
        Else
            Size = New Size(1040, 590)
            MinimumSize = New Size(820, 520)
        End If
        AutoScaleMode = AutoScaleMode.Dpi
        Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
        BackColor = Color.FromArgb(243, 247, 252)

        BuildScreen()
        LoadProducts()
        LoadRecord()
        ApplyMode()

        AddHandler Shown, AddressOf Detail_Shown
        AddHandler FormClosing, AddressOf Detail_FormClosing
    End Sub

    Private Sub BuildScreen()
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .BackColor = BackColor
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 44.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 72.0F))
        Controls.Add(root)

        Dim header As New Panel() With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(31, 71, 136),
            .Padding = New Padding(18, 0, 18, 0)
        }
        Dim title As New Label() With {
            .Dock = DockStyle.Fill,
            .Text = If(isNewRecord, "Yeni Teslim Oluştur", "Mekanizma Kalite Kontrol Detayı"),
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 13.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(0)
        }
        header.Controls.Add(title)
        root.Controls.Add(header, 0, 0)

        Dim body As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = If(isNewRecord, 1, 2),
            .RowCount = 1,
            .Padding = If(isNewRecord, New Padding(16, 14, 16, 12), New Padding(14, 14, 14, 12)),
            .BackColor = BackColor
        }
        If isNewRecord Then
            body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        Else
            body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 49.0F))
            body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 51.0F))
        End If
        body.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        body.Controls.Add(If(isNewRecord, BuildDeliveryPanel(), BuildDeliveryDetailPanel()), 0, 0)
        If Not isNewRecord Then body.Controls.Add(BuildControlDetailPanel(), 1, 0)
        root.Controls.Add(body, 0, 1)

        Dim footerShell As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .BackColor = Color.FromArgb(225, 231, 240),
            .Padding = New Padding(0)
        }
        footerShell.RowStyles.Add(New RowStyle(SizeType.Absolute, 1.0F))
        footerShell.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim footer As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = If(isNewRecord, 3, 5),
            .RowCount = 1,
            .BackColor = Color.White,
            .Padding = New Padding(14, 10, 14, 10)
        }
        footer.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        If isNewRecord Then
            footer.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 150.0F))
            footer.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 104.0F))
        Else
            footer.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 130.0F))
            footer.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 130.0F))
            footer.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 142.0F))
            footer.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 104.0F))
        End If
        footer.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim footerHint As New Label() With {
            .Dock = DockStyle.Fill,
            .Text = If(isNewRecord,
                       "Yeni teslim kaydı bekleyen listeye eklenecek.",
                       "Bekleyen kayıtta sonucu seçin; yönetici/admin gerekli detay düzeltmesini kaydedebilir."),
            .ForeColor = Color.FromArgb(89, 101, 118),
            .Font = New Font("Segoe UI", 8.8F, FontStyle.Regular),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(4, 0, 12, 0)
        }

        ConfigureActionButton(btnSubmit, "Teslim Et", Color.FromArgb(226, 238, 255), Color.FromArgb(31, 71, 136), 150)
        btnSubmit.Dock = DockStyle.Fill
        btnSubmit.Margin = New Padding(8, 0, 8, 0)
        AddHandler btnSubmit.Click, AddressOf Submit_Click

        ConfigureActionButton(btnSaveDetails, "Kaydet", Color.FromArgb(235, 243, 255), Color.FromArgb(31, 71, 136), 120)
        btnSaveDetails.Dock = DockStyle.Fill
        btnSaveDetails.Margin = New Padding(8, 0, 8, 0)
        AddHandler btnSaveDetails.Click, AddressOf SaveDetails_Click

        ConfigureActionButton(btnSuitable, "UYGUN", Color.Honeydew, Color.DarkGreen, 128)
        btnSuitable.Dock = DockStyle.Fill
        btnSuitable.Margin = New Padding(8, 0, 8, 0)
        AddHandler btnSuitable.Click, Sub() CompleteSelected(True)

        ConfigureActionButton(btnNotSuitable, "UYGUN DEĞİL", Color.MistyRose, Color.DarkRed, 140)
        btnNotSuitable.Dock = DockStyle.Fill
        btnNotSuitable.Margin = New Padding(8, 0, 8, 0)
        AddHandler btnNotSuitable.Click, Sub() CompleteSelected(False)

        btnClose.Text = "Kapat"
        btnClose.Dock = DockStyle.Fill
        btnClose.Margin = New Padding(8, 0, 0, 0)
        btnClose.MinimumSize = New Size(96, 36)
        btnClose.Cursor = Cursors.Hand
        btnClose.FlatStyle = FlatStyle.Flat
        btnClose.BackColor = Color.White
        btnClose.FlatAppearance.BorderColor = Color.FromArgb(130, 130, 130)
        btnClose.AutoEllipsis = False
        AddHandler btnClose.Click, Sub() Close()

        footer.Controls.Add(footerHint, 0, 0)
        If isNewRecord Then
            footer.Controls.Add(btnSubmit, 1, 0)
            footer.Controls.Add(btnClose, 2, 0)
        Else
            footer.Controls.Add(btnSaveDetails, 1, 0)
            footer.Controls.Add(btnSuitable, 2, 0)
            footer.Controls.Add(btnNotSuitable, 3, 0)
            footer.Controls.Add(btnClose, 4, 0)
        End If
        footerShell.Controls.Add(New Panel() With {.Dock = DockStyle.Fill, .BackColor = Color.FromArgb(225, 231, 240)}, 0, 0)
        footerShell.Controls.Add(footer, 0, 1)
        root.Controls.Add(footerShell, 0, 2)

        CancelButton = btnClose
    End Sub

    Private Function BuildDeliveryPanel() As Control
        Dim group As New GroupBox() With {
            .Text = If(isNewRecord, "Teslim Bilgileri", "1. Teslim Bilgileri"),
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold),
            .BackColor = Color.White,
            .Padding = New Padding(12),
            .Margin = If(isNewRecord, New Padding(0), New Padding(0, 0, 6, 0)),
            .ForeColor = Color.FromArgb(31, 71, 136)
        }

        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 7,
            .Padding = New Padding(8),
            .BackColor = Color.White,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, If(isNewRecord, 155.0F, 170.0F)))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
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
        numIncomingEyeCount.Minimum = 1D
        numIncomingEyeCount.Maximum = 999999D
        numIncomingEyeCount.Value = 1D
        ConfigureNumericInput(numIncomingEyeCount)
        eyeCountHost.Controls.Add(numIncomingEyeCount)
        layout.Controls.Add(eyeCountHost, 1, 1)

        layout.Controls.Add(CreateFieldLabel("Ürün Filtresi"), 0, 2)
        txtProductSelectionFilter.Dock = DockStyle.Fill
        txtProductSelectionFilter.Margin = New Padding(6)
        txtProductSelectionFilter.PlaceholderText = "TR kodu / ürün adı / makine / kalıp"
        ConfigureTextInput(txtProductSelectionFilter)
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
        txtDeliveryExplanation.PlaceholderText = "Teslim eden kullanıcının açıklaması"
        ConfigureTextInput(txtDeliveryExplanation)
        layout.Controls.Add(txtDeliveryExplanation, 1, 5)

        lblStatus.Dock = DockStyle.Fill
        lblStatus.Margin = New Padding(6, 4, 6, 2)
        lblStatus.Padding = New Padding(8, 0, 8, 0)
        lblStatus.BackColor = Color.FromArgb(247, 250, 253)
        lblStatus.ForeColor = Color.FromArgb(89, 101, 118)
        lblStatus.Font = New Font("Segoe UI", 8.75F, FontStyle.Bold)
        lblStatus.TextAlign = ContentAlignment.MiddleLeft
        layout.SetColumnSpan(lblStatus, 2)
        layout.Controls.Add(lblStatus, 0, 6)

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
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 210.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        group.Controls.Add(layout)

        layout.Controls.Add(CreateFieldLabel("Kontrol Eden"), 0, 0)
        ConfigureInfoLabel(lblControlledBy)
        layout.Controls.Add(lblControlledBy, 1, 0)

        layout.Controls.Add(CreateFieldLabel("Montajı Yapılan Mekanizma/Sayaç"), 0, 1)
        txtMountedMechanismCounter.Dock = DockStyle.Fill
        txtMountedMechanismCounter.Margin = New Padding(6)
        txtMountedMechanismCounter.PlaceholderText = "Mekanizma veya sayaç bilgisini girin"
        ConfigureTextInput(txtMountedMechanismCounter)
        layout.Controls.Add(txtMountedMechanismCounter, 1, 1)

        layout.Controls.Add(CreateFieldLabel("Kontrol Açıklaması"), 0, 2)
        txtControlExplanation.Dock = DockStyle.Fill
        txtControlExplanation.Margin = New Padding(6)
        txtControlExplanation.Multiline = True
        txtControlExplanation.ScrollBars = ScrollBars.Vertical
        txtControlExplanation.PlaceholderText = "Kontrol eden kullanıcının açıklaması"
        ConfigureTextInput(txtControlExplanation)
        layout.Controls.Add(txtControlExplanation, 1, 2)

        Dim hint As New Label() With {
            .Dock = DockStyle.Fill,
            .Margin = New Padding(6, 2, 6, 2),
            .Text = "Uygun değil sonucunda kontrol açıklaması zorunludur.",
            .ForeColor = Color.FromArgb(89, 101, 118),
            .Font = New Font("Segoe UI", 8.75F, FontStyle.Regular),
            .TextAlign = ContentAlignment.MiddleLeft
        }
        layout.Controls.Add(hint, 1, 3)

        Return group
    End Function

    Private Function BuildDeliveryDetailPanel() As Control
        Dim card = CreateSectionCard("Teslim Özeti", Color.FromArgb(235, 243, 255), Color.FromArgb(31, 71, 136), New Padding(0, 0, 8, 0))

        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 6,
            .Padding = New Padding(18, 16, 18, 14),
            .BackColor = Color.White,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 140.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 74.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))

        ConfigureInfoLabel(lblDeliveredAt)
        AddDetailRow(layout, "Teslim Tarihi / Saati", lblDeliveredAt, 0)

        ConfigureInfoLabel(lblDeliveredBy)
        AddDetailRow(layout, "Teslim Eden", lblDeliveredBy, 1)

        ConfigureReadOnlyTextBox(txtIncomingEyeCountView)
        AddDetailRow(layout, "Gelen Göz Sayısı", txtIncomingEyeCountView, 2)

        ConfigureReadOnlyTextBox(txtProductNameCodeView)
        txtProductNameCodeView.Multiline = True
        txtProductNameCodeView.ScrollBars = ScrollBars.Vertical
        txtProductNameCodeView.WordWrap = True
        AddDetailRow(layout, "Ürün Adı ve Kodu", txtProductNameCodeView, 3)

        txtDeliveryExplanation.Dock = DockStyle.Fill
        txtDeliveryExplanation.Margin = New Padding(6)
        txtDeliveryExplanation.Multiline = True
        txtDeliveryExplanation.ScrollBars = ScrollBars.Vertical
        txtDeliveryExplanation.PlaceholderText = "Teslim açıklaması girilmemiş."
        ConfigureTextInput(txtDeliveryExplanation)
        AddDetailRow(layout, "Teslim Açıklaması", txtDeliveryExplanation, 4)

        lblStatus.Dock = DockStyle.Fill
        lblStatus.Margin = New Padding(6, 5, 6, 3)
        lblStatus.Padding = New Padding(10, 0, 10, 0)
        lblStatus.BackColor = Color.FromArgb(247, 250, 253)
        lblStatus.ForeColor = Color.FromArgb(65, 76, 92)
        lblStatus.Font = New Font("Segoe UI", 8.8F, FontStyle.Bold)
        lblStatus.TextAlign = ContentAlignment.MiddleLeft
        layout.SetColumnSpan(lblStatus, 2)
        layout.Controls.Add(lblStatus, 0, 5)

        card.Controls.Add(layout, 0, 1)
        Return card
    End Function

    Private Function BuildControlDetailPanel() As Control
        Dim card = CreateSectionCard("Kontrol İşlemi", Color.FromArgb(238, 249, 241), Color.FromArgb(26, 102, 65), New Padding(8, 0, 0, 0))

        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 4,
            .Padding = New Padding(18, 16, 18, 14),
            .BackColor = Color.White,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 150.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))

        ConfigureInfoLabel(lblControlledBy)
        AddDetailRow(layout, "Kontrol Eden", lblControlledBy, 0)

        txtMountedMechanismCounter.Dock = DockStyle.Fill
        txtMountedMechanismCounter.Margin = New Padding(6)
        txtMountedMechanismCounter.PlaceholderText = "Mekanizma veya sayaç bilgisini girin"
        ConfigureTextInput(txtMountedMechanismCounter)
        AddDetailRow(layout, "Mekanizma / Sayaç", txtMountedMechanismCounter, 1)

        txtControlExplanation.Dock = DockStyle.Fill
        txtControlExplanation.Margin = New Padding(6)
        txtControlExplanation.Multiline = True
        txtControlExplanation.ScrollBars = ScrollBars.Vertical
        txtControlExplanation.PlaceholderText = "Kontrol açıklamasını girin."
        ConfigureTextInput(txtControlExplanation)
        AddDetailRow(layout, "Kontrol Açıklaması", txtControlExplanation, 2)

        Dim hint As New Label() With {
            .Dock = DockStyle.Fill,
            .Margin = New Padding(6, 5, 6, 3),
            .Padding = New Padding(10, 0, 10, 0),
            .Text = "Uygun değil sonucunda kontrol açıklaması zorunludur.",
            .BackColor = Color.FromArgb(247, 250, 253),
            .ForeColor = Color.FromArgb(89, 101, 118),
            .Font = New Font("Segoe UI", 8.75F, FontStyle.Regular),
            .TextAlign = ContentAlignment.MiddleLeft
        }
        layout.SetColumnSpan(hint, 2)
        layout.Controls.Add(hint, 0, 3)

        card.Controls.Add(layout, 0, 1)
        Return card
    End Function

    Private Function CreateSectionCard(titleText As String, headerBackColor As Color, headerForeColor As Color, margin As Padding) As TableLayoutPanel
        Dim card As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .Margin = margin,
            .Padding = New Padding(0),
            .BackColor = Color.White,
            .CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
        }
        card.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        card.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        card.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))

        Dim title As New Label() With {
            .Dock = DockStyle.Fill,
            .Text = titleText,
            .BackColor = headerBackColor,
            .ForeColor = headerForeColor,
            .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(16, 0, 16, 0),
            .Margin = New Padding(0)
        }
        card.Controls.Add(title, 0, 0)

        Return card
    End Function

    Private Sub AddDetailRow(layout As TableLayoutPanel, labelText As String, valueControl As Control, rowIndex As Integer)
        Dim label = CreateDetailLabel(labelText)
        If TypeOf valueControl Is TextBox AndAlso DirectCast(valueControl, TextBox).Multiline Then
            label.TextAlign = ContentAlignment.TopLeft
            label.Padding = New Padding(4, 10, 4, 0)
        End If

        layout.Controls.Add(label, 0, rowIndex)
        layout.Controls.Add(valueControl, 1, rowIndex)
    End Sub

    Private Shared Function CreateDetailLabel(text As String) As Label
        Return New Label() With {
            .Text = text,
            .Dock = DockStyle.Fill,
            .Margin = New Padding(4, 4, 4, 4),
            .Padding = New Padding(4, 0, 4, 0),
            .TextAlign = ContentAlignment.MiddleLeft,
            .ForeColor = Color.FromArgb(65, 76, 92),
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        }
    End Function

    Private Sub LoadRecord()
        isClearingForm = True
        Try
            If isNewRecord Then
                lblDeliveredBy.Text = AppState.CurrentUserName
                lblControlledBy.Text = ""
                lblStatus.Text = "Yeni teslim kaydı"
                originalEyeCount = 1D
                Return
            End If

            AffectedControlId = ValueOf("ControlId")
            lblDeliveredAt.Text = FormatDateTimeValue(ValueOf("CreatedAt"))
            lblDeliveredBy.Text = ValueOf("DeliveredBy")
            lblControlledBy.Text = ValueOf("ControlledBy")

            Dim eyeCount As Decimal = 1D
            Decimal.TryParse(ValueOf("IncomingEyeCount"), eyeCount)
            eyeCount = Math.Max(numIncomingEyeCount.Minimum, Math.Min(numIncomingEyeCount.Maximum, eyeCount))
            numIncomingEyeCount.Value = eyeCount
            txtIncomingEyeCountView.Text = CInt(eyeCount).ToString()

            selectedProducts = DeserializeSelectedProducts(ValueOf("ProductNameCode"))
            cboProductNameCode.Text = If(selectedProducts.FirstOrDefault(), "")
            txtProductNameCodeView.Text = String.Join(Environment.NewLine, selectedProducts)
            txtDeliveryExplanation.Text = If(ValueOf("DeliveryExplanation") <> "", ValueOf("DeliveryExplanation"), ValueOf("Explanation"))
            txtMountedMechanismCounter.Text = ValueOf("MountedMechanismCounter")
            txtControlExplanation.Text = ValueOf("ControlExplanation")

            originalEyeCount = numIncomingEyeCount.Value
            originalProductText = txtProductNameCodeView.Text.Trim()
            originalDeliveryExplanation = txtDeliveryExplanation.Text.Trim()
            originalMountedMechanismCounter = txtMountedMechanismCounter.Text.Trim()
            originalControlExplanation = txtControlExplanation.Text.Trim()

            Dim statusText = If(String.Equals(ValueOf("Status"), "PENDING", StringComparison.OrdinalIgnoreCase), "Bekleyen", "Tamamlanan")
            lblStatus.Text = $"Kayıt No: {AffectedControlId} | Durum: {statusText}"
        Finally
            isClearingForm = False
        End Try
    End Sub

    Private Shared Function FormatDateTimeValue(value As String) As String
        Dim parsed As DateTime
        If DateTime.TryParse(value, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.AllowWhiteSpaces, parsed) OrElse
           DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, parsed) Then
            Return parsed.ToString("dd.MM.yyyy HH:mm")
        End If
        Return If(String.IsNullOrWhiteSpace(value), "-", value.Trim())
    End Function

    Private Sub ApplyMode()
        Dim canCreate = isNewRecord AndAlso AppState.CanCreateMechanismQualityDelivery
        Dim isPending = Not isNewRecord AndAlso String.Equals(ValueOf("Status"), "PENDING", StringComparison.OrdinalIgnoreCase)
        Dim canReviewPending = isPending AndAlso AppState.CanReviewMechanismQualityDelivery
        Dim canEditExistingDetails = Not isNewRecord AndAlso AppState.CanEditMechanismQualityDetails

        If canReviewPending Then lblControlledBy.Text = AppState.CurrentUserName

        numIncomingEyeCount.Enabled = canCreate
        txtProductSelectionFilter.Enabled = canCreate
        chkIncludeAllProducts.Enabled = canCreate
        cboProductNameCode.Enabled = canCreate
        txtDeliveryExplanation.ReadOnly = Not (canCreate OrElse canEditExistingDetails)

        txtMountedMechanismCounter.Enabled = True
        txtMountedMechanismCounter.ReadOnly = Not (canReviewPending OrElse canEditExistingDetails)
        txtControlExplanation.ReadOnly = Not (canReviewPending OrElse canEditExistingDetails)
        txtIncomingEyeCountView.ReadOnly = Not canEditExistingDetails
        txtProductNameCodeView.ReadOnly = Not canEditExistingDetails
        ApplyTextBoxReadOnlyState(txtDeliveryExplanation, Not (canCreate OrElse canEditExistingDetails))
        ApplyTextBoxReadOnlyState(txtMountedMechanismCounter, Not (canReviewPending OrElse canEditExistingDetails))
        ApplyTextBoxReadOnlyState(txtControlExplanation, Not (canReviewPending OrElse canEditExistingDetails))
        ApplyTextBoxReadOnlyState(txtIncomingEyeCountView, Not canEditExistingDetails)
        ApplyTextBoxReadOnlyState(txtProductNameCodeView, Not canEditExistingDetails)

        btnSubmit.Visible = isNewRecord
        btnSubmit.Enabled = canCreate
        btnSaveDetails.Visible = canEditExistingDetails
        btnSaveDetails.Enabled = canEditExistingDetails
        btnSuitable.Visible = canReviewPending
        btnNotSuitable.Visible = canReviewPending
        btnClose.Text = If(isNewRecord OrElse canReviewPending, "Vazgeç", "Kapat")
    End Sub

    Private Sub Submit_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.CanCreateMechanismQualityDelivery, "Mekanizma Kontrol Teslimi")

            Dim productNameCode As String = ""
            If Not TryGetSelectedProduct(productNameCode, True) Then Return

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
                {"MountedMechanismCounter", ""},
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

            AffectedControlId = controlId
            savedChanges = True
            MessageBox.Show("Teslim kaydı Mekanizma Kalite Kontrol'e gönderildi." & Environment.NewLine &
                            "Kayıt No: " & controlId,
                            "Teslim oluşturuldu", MessageBoxButtons.OK, MessageBoxIcon.Information)
            DialogResult = DialogResult.OK
            Close()
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Teslim kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub SaveDetails_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.CanEditMechanismQualityDetails, "Mekanizma Kalite Kontrol Detay Düzeltme")

            If isNewRecord OrElse AffectedControlId = "" Then
                MessageBox.Show("Düzeltilecek mekanizma kalite kontrol kaydı bulunamadı.",
                                "Kayıt seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim productNameCode As String = ""
            If Not TryGetProductNameCodeForSave(productNameCode, True) Then Return

            Dim eyeCountText = txtIncomingEyeCountView.Text.Trim()
            Dim eyeCountValue As Integer
            If Not Integer.TryParse(eyeCountText, eyeCountValue) OrElse eyeCountValue < 1 OrElse eyeCountValue > 999999 Then
                MessageBox.Show("Gelen göz sayısı 1 ile 999999 arasında tam sayı olmalıdır.",
                                "Geçersiz göz sayısı", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                txtIncomingEyeCountView.Focus()
                Return
            End If

            DataService.UpdateMechanismQualityControlDetails(
                AffectedControlId,
                eyeCountValue.ToString(),
                productNameCode,
                txtDeliveryExplanation.Text,
                txtMountedMechanismCounter.Text,
                txtControlExplanation.Text)

            If record IsNot Nothing Then
                record("IncomingEyeCount") = eyeCountValue.ToString()
                record("ProductNameCode") = productNameCode
                record("Explanation") = txtDeliveryExplanation.Text.Trim()
                record("DeliveryExplanation") = txtDeliveryExplanation.Text.Trim()
                record("MountedMechanismCounter") = txtMountedMechanismCounter.Text.Trim()
                record("ControlExplanation") = txtControlExplanation.Text.Trim()
            End If

            txtIncomingEyeCountView.Text = eyeCountValue.ToString()
            selectedProducts = DeserializeSelectedProducts(productNameCode)
            txtProductNameCodeView.Text = String.Join(Environment.NewLine, selectedProducts)
            originalEyeCount = eyeCountValue
            originalProductText = txtProductNameCodeView.Text.Trim()
            originalDeliveryExplanation = txtDeliveryExplanation.Text.Trim()
            originalMountedMechanismCounter = txtMountedMechanismCounter.Text.Trim()
            originalControlExplanation = txtControlExplanation.Text.Trim()

            AuditService.Log("MECHANISM_QUALITY_DETAIL_UPDATE", "", "",
                             $"ControlId={AffectedControlId}; Product={productNameCode}; EyeCount={eyeCountValue}")

            savedChanges = True
            MessageBox.Show("Mekanizma kalite kontrol detayları güncellendi.",
                            "Detay kaydedildi", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Detay kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub CompleteSelected(isSuitable As Boolean)
        Try
            AuthorizationService.Require(AppState.CanReviewMechanismQualityDelivery, "Mekanizma Kalite Kontrol Sonuçlandırma")

            If isNewRecord OrElse AffectedControlId = "" Then
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
            If Not TryGetProductNameCodeForSave(productNameCode, True) Then Return

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
                AffectedControlId,
                isSuitable,
                AppState.CurrentUserName,
                txtControlExplanation.Text,
                txtMountedMechanismCounter.Text,
                productNameCode)

            Dim mailWarning As String = ""
            If Not isSuitable Then
                MechanismQualityEmailNotificationService.TryNotifyNotSuitable(AffectedControlId, mailWarning)
            End If

            AuditService.Log("MECHANISM_QUALITY_CONTROL_COMPLETE", "", "",
                             $"ControlId={AffectedControlId}; Result={resultText}; Product={productNameCode}")

            savedChanges = True
            Dim completionMessage = "Mekanizma kalite kontrolü " & resultText & " olarak tamamlandı."
            Dim completionIcon = MessageBoxIcon.Information
            If mailWarning <> "" Then
                completionMessage &= Environment.NewLine & Environment.NewLine &
                                     "Kayıt kaydedildi ancak otomatik mail gönderilemedi:" & Environment.NewLine &
                                     mailWarning
                completionIcon = MessageBoxIcon.Warning
            End If
            MessageBox.Show(completionMessage,
                            "Kontrol tamamlandı", MessageBoxButtons.OK, completionIcon)
            DialogResult = DialogResult.OK
            Close()
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Kontrol tamamlanamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ProductSelectionFilterChanged(sender As Object, e As EventArgs)
        ApplyProductSelectionFilter()
    End Sub

    Private Sub ProductNameCodeTextUpdated(sender As Object, e As EventArgs)
        If isUpdatingProductOptions OrElse
           isClearingForm OrElse
           Not isNewRecord OrElse
           Not cboProductNameCode.Enabled Then
            Return
        End If

        ApplyProductSelectionFilter(cboProductNameCode.Text, True)
    End Sub

    Private Sub ProductSelectionChanged(sender As Object, e As EventArgs)
        If isClearingForm OrElse Not isNewRecord OrElse Not cboProductNameCode.Enabled Then Return
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

        Dim isCurrentRecordProduct = Not isNewRecord AndAlso
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

    Private Function TryGetProductNameCodeForSave(ByRef productText As String, showMessage As Boolean) As Boolean
        If isNewRecord Then Return TryGetSelectedProduct(productText, showMessage)

        productText = txtProductNameCodeView.Text.Trim()
        If productText = "" Then
            If showMessage Then
                MessageBox.Show("Ürün adı ve kodu boş olamaz.", "Eksik ürün bilgisi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                txtProductNameCodeView.Focus()
            End If
            Return False
        End If

        Return True
    End Function

    Private Sub ApplyProductSelectionFilter(Optional productEntryText As String = Nothing,
                                            Optional openDropDown As Boolean = False)
        Dim selectedText = cboProductNameCode.Text.Trim()
        Dim filterText = txtProductSelectionFilter.Text.Trim()
        Dim filtered = productOptions.AsEnumerable()

        If filterText <> "" Then filtered = filtered.Where(Function(item) ProductMatchesFilter(item, filterText))
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
            ElseIf isNewRecord Then
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
        productName = SafeText(productName)
        Dim parts As New List(Of String) From {SafeText(trCode), productName}
        If SafeText(sourceText) <> "" Then parts.Add(SafeText(sourceText))
        If SafeText(machineText) <> "" Then parts.Add("Makine: " & SafeText(machineText))
        If SafeText(moldText) <> "" Then parts.Add("Kalıp: " & SafeText(moldText))
        Return String.Join(" | ", parts)
    End Function

    Private Sub Detail_Shown(sender As Object, e As EventArgs)
        FitToWorkingArea()
    End Sub

    Private Sub FitToWorkingArea()
        Dim area = Screen.FromControl(Me).WorkingArea
        Dim targetWidth = Math.Min(Width, Math.Max(MinimumSize.Width, CInt(area.Width * 0.94)))
        Dim targetHeight = Math.Min(Height, Math.Max(MinimumSize.Height, CInt(area.Height * 0.88)))
        Size = New Size(targetWidth, targetHeight)
        Location = New Point(area.Left + (area.Width - Width) \ 2, area.Top + (area.Height - Height) \ 2)
    End Sub

    Private Sub Detail_FormClosing(sender As Object, e As FormClosingEventArgs)
        If savedChanges OrElse DialogResult = DialogResult.OK Then Return
        If Not HasUnsavedDraft() Then Return

        If MessageBox.Show(
            "Pencerede kaydedilmemiş bilgiler var." & Environment.NewLine &
            "Bu değişikliklerden vazgeçilsin mi?",
            "Kaydedilmemiş bilgiler",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then
            e.Cancel = True
        End If
    End Sub

    Private Function HasUnsavedDraft() As Boolean
        If isNewRecord Then
            Return cboProductNameCode.Text.Trim() <> "" OrElse
                   txtDeliveryExplanation.Text.Trim() <> "" OrElse
                   numIncomingEyeCount.Value <> 1D
        End If

        Dim isPending = String.Equals(ValueOf("Status"), "PENDING", StringComparison.OrdinalIgnoreCase)
        Dim canEditDetails = AppState.CanEditMechanismQualityDetails
        If Not canEditDetails AndAlso (Not isPending OrElse Not AppState.CanReviewMechanismQualityDelivery) Then Return False

        Return Not String.Equals(txtIncomingEyeCountView.Text.Trim(), CInt(originalEyeCount).ToString(), StringComparison.Ordinal) OrElse
               Not String.Equals(txtProductNameCodeView.Text.Trim(), originalProductText, StringComparison.Ordinal) OrElse
               Not String.Equals(txtDeliveryExplanation.Text.Trim(), originalDeliveryExplanation, StringComparison.Ordinal) OrElse
               Not String.Equals(txtMountedMechanismCounter.Text.Trim(), originalMountedMechanismCounter, StringComparison.Ordinal) OrElse
               Not String.Equals(txtControlExplanation.Text.Trim(), originalControlExplanation, StringComparison.Ordinal)
    End Function

    Private Function ValueOf(columnName As String) As String
        If record Is Nothing OrElse Not record.ContainsKey(columnName) Then Return ""
        Return If(record(columnName), "").Trim()
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

    Private Shared Function CreateFieldLabel(text As String) As Label
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

    Private Shared Sub ConfigureTextInput(textBox As TextBox)
        textBox.BorderStyle = BorderStyle.FixedSingle
        textBox.Font = New Font("Segoe UI", 9.25F, FontStyle.Regular)
        textBox.BackColor = Color.White
        textBox.ForeColor = Color.FromArgb(31, 41, 55)
    End Sub

    Private Shared Sub ConfigureReadOnlyTextBox(textBox As TextBox)
        ConfigureTextInput(textBox)
        textBox.Dock = DockStyle.Fill
        textBox.Margin = New Padding(6)
        textBox.ReadOnly = True
        textBox.BackColor = Color.FromArgb(247, 250, 253)
        textBox.ForeColor = Color.FromArgb(31, 41, 55)
    End Sub

    Private Shared Sub ApplyTextBoxReadOnlyState(textBox As TextBox, isReadOnly As Boolean)
        If textBox Is Nothing Then Return

        textBox.ReadOnly = isReadOnly
        If isReadOnly Then
            textBox.BackColor = Color.FromArgb(247, 250, 253)
            textBox.ForeColor = Color.FromArgb(31, 41, 55)
        Else
            textBox.BackColor = Color.White
            textBox.ForeColor = Color.FromArgb(31, 41, 55)
        End If
    End Sub

    Private Shared Sub ConfigureSelectionInput(comboBox As ComboBox)
        comboBox.FlatStyle = FlatStyle.Standard
        comboBox.Font = New Font("Segoe UI", 9.25F, FontStyle.Regular)
        comboBox.BackColor = Color.White
        comboBox.ForeColor = Color.FromArgb(31, 41, 55)
        comboBox.IntegralHeight = False
        comboBox.DropDownHeight = 260
    End Sub

    Private Shared Sub ConfigureNumericInput(input As NumericUpDown)
        input.BorderStyle = BorderStyle.FixedSingle
        input.Font = New Font("Segoe UI", 9.25F, FontStyle.Regular)
        input.BackColor = Color.White
        input.ForeColor = Color.FromArgb(31, 41, 55)
        input.TextAlign = HorizontalAlignment.Left
    End Sub

    Private Shared Sub ConfigureActionButton(button As Button, text As String, backColor As Color, foreColor As Color, width As Integer)
        button.Text = text
        button.Width = width
        button.Height = 36
        button.Margin = New Padding(8, 0, 0, 0)
        button.BackColor = backColor
        button.ForeColor = foreColor
        button.FlatStyle = FlatStyle.Flat
        button.Cursor = Cursors.Hand
        button.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        button.UseVisualStyleBackColor = False
        button.FlatAppearance.BorderSize = 2
        button.FlatAppearance.BorderColor = foreColor
        button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(backColor, 0.08F)
    End Sub
End Class
