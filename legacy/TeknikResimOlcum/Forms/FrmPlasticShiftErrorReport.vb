Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmPlasticShiftErrorReport
    Inherits Form

    Private Shared ReadOnly ReviewItems As String() = {
        "Stok kontrol ihtiyacı var mı?",
        "Etkilenen operasyon (süreç) var mı?",
        "Etkilenen ürün tipi / tipleri var mı?",
        "Doküman ihtiyacı var mı?",
        "Teknik resim revizyon ihtiyacı var mı?",
        "Kalıp revizyonu gerekli mi?",
        "Yarı mamul gözden geçirme ihtiyacı var mı?"
    }

    Private Shared ReadOnly ReviewResultFields As String() = {
        "StockReviewResult", "AffectedProcessResult", "AffectedProductResult",
        "DocumentNeedResult", "DrawingRevisionResult", "MoldRevisionResult",
        "SemiFinishedReviewResult"
    }

    Private Shared ReadOnly ReviewDetailFields As String() = {
        "StockReviewDetail", "AffectedProcessDetail", "AffectedProductDetail",
        "DocumentNeedDetail", "DrawingRevisionDetail", "MoldRevisionDetail",
        "SemiFinishedReviewDetail"
    }

    Private ReadOnly shiftRecord As Dictionary(Of String, String)
    Private reportRow As Dictionary(Of String, String)
    Private ReadOnly canEditInitial As Boolean
    Private ReadOnly canManage As Boolean

    Private ReadOnly lblReportNo As New Label()
    Private ReadOnly lblShiftNo As New Label()
    Private ReadOnly lblStatus As New Label()
    Private ReadOnly tabs As New TabControl()
    Private ReadOnly btnSave As New Button()
    Private ReadOnly btnClose As New Button()

    Private ReadOnly txtRevisionDate As New TextBox()
    Private ReadOnly txtSourceDepartment As New TextBox()
    Private ReadOnly txtQualityPoint As New TextBox()
    Private ReadOnly txtPartNameNo As New TextBox()
    Private ReadOnly txtTrNo As New TextBox()
    Private ReadOnly txtPartType As New TextBox()
    Private ReadOnly txtQuantity As New TextBox()
    Private ReadOnly txtMachineNo As New TextBox()
    Private ReadOnly txtOperator As New TextBox()
    Private ReadOnly txtDefectArea As New TextBox()
    Private ReadOnly txtDefectCode As New TextBox()
    Private ReadOnly txtDefectType As New TextBox()
    Private ReadOnly txtQualityInspector As New TextBox()
    Private ReadOnly txtDetectedBy As New TextBox()
    Private ReadOnly txtUnitManagerApproval As New TextBox()
    Private ReadOnly txtNonconformity As New TextBox()

    Private ReadOnly cboDisposition As New ComboBox()
    Private ReadOnly txtKaizenResponsible As New TextBox()
    Private ReadOnly txtKaizenNo As New TextBox()
    Private ReadOnly txtRootCause As New TextBox()
    Private ReadOnly gridActions As New DataGridView()
    Private ReadOnly gridReviews As New DataGridView()
    Private ReadOnly gridEvaluations As New DataGridView()
    Private ReadOnly lblEvaluationInfo As New Label()
    Private ReadOnly lblSelectedEvaluation As New Label()
    Private ReadOnly cboEvaluationDecision As New ComboBox()
    Private ReadOnly txtEvaluationExplanation As New TextBox()
    Private ReadOnly btnSaveEvaluation As New Button()

    Private ReadOnly txtVerificationActivities As New TextBox()
    Private ReadOnly txtVerificationResponsible As New TextBox()
    Private ReadOnly dtpVerificationDate As New DateTimePicker()
    Private ReadOnly dtpVerificationDueDate As New DateTimePicker()
    Private ReadOnly cboVerificationSuitable As New ComboBox()
    Private ReadOnly cboCloseApproved As New ComboBox()
    Private ReadOnly txtCloseNote As New TextBox()

    Public Property SavedChanges As Boolean

    Public Sub New(sourceShiftRecord As Dictionary(Of String, String))
        AuthorizationService.Require(AppState.CanOpenPlasticShiftErrorReport, "Vardiya Hata Raporu")
        If sourceShiftRecord Is Nothing Then Throw New ArgumentNullException(NameOf(sourceShiftRecord))

        shiftRecord = New Dictionary(Of String, String)(sourceShiftRecord, StringComparer.OrdinalIgnoreCase)
        Dim shiftId = DataService.GetValue(shiftRecord, "RecordId").Trim()
        If shiftId = "" Then Throw New ArgumentException("Hata raporu için vardiya kayıt numarası bulunamadı.")

        reportRow = DataService.GetPlasticShiftErrorReport(shiftId)
        canManage = AppState.CanManagePlasticShiftErrorReport
        canEditInitial = AppState.CanCreatePlasticShiftErrorReport OrElse canManage

        AppIconService.Apply(Me)
        Text = "Vardiya Hata Raporu"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1450, 850)
        MinimumSize = New Size(900, 650)
        Font = New Font("Segoe UI", 9.0F)
        BackColor = Color.FromArgb(243, 247, 252)

        BuildScreen()
        LoadReport()
        ApplyPermissions()
        ResponsiveFormService.Apply(Me)
    End Sub

    Private Sub BuildScreen()
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .Padding = New Padding(10),
            .BackColor = BackColor
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 56.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        Controls.Add(root)

        Dim header As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .BackColor = Color.FromArgb(35, 82, 136),
            .Padding = New Padding(20, 0, 14, 0)
        }
        header.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        header.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 210.0F))
        header.Controls.Add(New Label() With {
            .Dock = DockStyle.Fill,
            .Text = "HATA RAPORU",
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 14.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0)
        lblStatus.Dock = DockStyle.Fill
        lblStatus.TextAlign = ContentAlignment.MiddleCenter
        lblStatus.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblStatus.ForeColor = Color.White
        lblStatus.BackColor = Color.FromArgb(24, 60, 104)
        lblStatus.Margin = New Padding(8, 9, 0, 9)
        header.Controls.Add(lblStatus, 1, 0)
        root.Controls.Add(header, 0, 0)

        Dim identity As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 4,
            .BackColor = Color.White,
            .Padding = New Padding(16, 7, 16, 7),
            .Margin = New Padding(0, 6, 0, 6)
        }
        identity.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 90.0F))
        identity.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        identity.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 105.0F))
        identity.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        identity.Controls.Add(MakeCaption("Rapor No"), 0, 0)
        identity.Controls.Add(lblReportNo, 1, 0)
        identity.Controls.Add(MakeCaption("Vardiya Kaydı"), 2, 0)
        identity.Controls.Add(lblShiftNo, 3, 0)
        For Each label In {lblReportNo, lblShiftNo}
            label.Dock = DockStyle.Fill
            label.TextAlign = ContentAlignment.MiddleLeft
            label.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
            label.ForeColor = Color.FromArgb(31, 71, 126)
        Next
        root.Controls.Add(identity, 0, 1)

        tabs.Dock = DockStyle.Fill
        tabs.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        tabs.TabPages.Add(BuildDetectionTab())
        tabs.TabPages.Add(BuildEvaluationTab())
        tabs.TabPages.Add(BuildManagementTab())
        tabs.TabPages.Add(BuildVerificationTab())
        tabs.TabPages(2).Text = "3. Değerlendirme ve Aksiyon"
        tabs.TabPages(3).Text = "4. Doğrulama ve Kapanış"
        root.Controls.Add(tabs, 0, 2)

        Dim footer As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False,
            .Padding = New Padding(0, 9, 6, 7),
            .BackColor = Color.White,
            .Margin = New Padding(0, 6, 0, 0)
        }
        ConfigureButton(btnClose, "Kapat", 110, Color.White, Color.FromArgb(35, 55, 80))
        ConfigureButton(btnSave, "Kaydet / Güncelle", 155, Color.FromArgb(26, 113, 70), Color.White)
        AddHandler btnClose.Click, Sub() Close()
        AddHandler btnSave.Click, AddressOf Save_Click
        footer.Controls.Add(btnClose)
        footer.Controls.Add(btnSave)
        root.Controls.Add(footer, 0, 3)
    End Sub

    Private Function BuildDetectionTab() As TabPage
        Dim page As New TabPage("1. Uygunsuzluk Tespiti") With {.BackColor = Color.White}
        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 4,
            .RowCount = 9,
            .Padding = New Padding(18, 16, 18, 16),
            .BackColor = Color.White
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 150.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 150.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        For index = 0 To 6
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        Next
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 32.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        AddPair(layout, 0, "Düzenleme Tarihi", txtRevisionDate, "Kaynak Bölüm", txtSourceDepartment)
        AddPair(layout, 1, "Kalite Kontrol Noktası", txtQualityPoint, "Parça Adı / No", txtPartNameNo)
        AddPair(layout, 2, "TR No", txtTrNo, "Tip", txtPartType)
        AddPair(layout, 3, "Miktarı", txtQuantity, "Tezgâh No", txtMachineNo)
        AddPair(layout, 4, "Operatör", txtOperator, "Hata Bölgesi", txtDefectArea)
        AddPair(layout, 5, "Hata Kodu", txtDefectCode, "Hata Çeşidi", txtDefectType)
        AddPair(layout, 6, "Kalite Kontrol Elemanı", txtQualityInspector, "Tespit Eden", txtDetectedBy)
        layout.Controls.Add(MakeCaption("Birim Amiri Onayı"), 0, 7)
        PrepareInput(txtUnitManagerApproval)
        layout.Controls.Add(txtUnitManagerApproval, 1, 7)
        layout.SetColumnSpan(txtUnitManagerApproval, 3)
        layout.Controls.Add(MakeCaption("Uygunsuzluk Tanımı"), 0, 8)
        PrepareMultiline(txtNonconformity)
        layout.Controls.Add(txtNonconformity, 1, 8)
        layout.SetColumnSpan(txtNonconformity, 3)
        page.Controls.Add(layout)
        Return page
    End Function

    Private Function BuildEvaluationTab() As TabPage
        Dim page As New TabPage("2. Üçlü Değerlendirme") With {.BackColor = Color.White}
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .Padding = New Padding(16),
            .BackColor = Color.White
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 46.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 56.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 44.0F))

        lblEvaluationInfo.Dock = DockStyle.Fill
        lblEvaluationInfo.TextAlign = ContentAlignment.MiddleLeft
        lblEvaluationInfo.Padding = New Padding(12, 0, 12, 0)
        lblEvaluationInfo.BackColor = Color.FromArgb(235, 243, 252)
        lblEvaluationInfo.ForeColor = Color.FromArgb(25, 71, 124)
        lblEvaluationInfo.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        root.Controls.Add(lblEvaluationInfo, 0, 0)

        ConfigureEvaluationGrid()
        root.Controls.Add(gridEvaluations, 0, 1)

        Dim editor As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 4,
            .RowCount = 3,
            .Padding = New Padding(12, 12, 12, 8),
            .BackColor = Color.FromArgb(248, 250, 253),
            .Margin = New Padding(0, 8, 0, 0)
        }
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 160.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 170.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 190.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 28.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        editor.Controls.Add(MakeCaption("Seçili Pozisyon"), 0, 0)
        lblSelectedEvaluation.Dock = DockStyle.Fill
        lblSelectedEvaluation.TextAlign = ContentAlignment.MiddleLeft
        lblSelectedEvaluation.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        editor.Controls.Add(lblSelectedEvaluation, 1, 0)
        cboEvaluationDecision.DropDownStyle = ComboBoxStyle.DropDownList
        cboEvaluationDecision.Items.AddRange({"ONAY", "REVİZYON GEREKLİ"})
        PrepareControl(cboEvaluationDecision)
        editor.Controls.Add(cboEvaluationDecision, 2, 0)
        ConfigureButton(btnSaveEvaluation, "Değerlendirmemi Kaydet", 180, Color.FromArgb(31, 92, 153), Color.White)
        btnSaveEvaluation.Dock = DockStyle.Fill
        btnSaveEvaluation.Margin = New Padding(8, 2, 0, 2)
        AddHandler btnSaveEvaluation.Click, AddressOf SaveEvaluation_Click
        editor.Controls.Add(btnSaveEvaluation, 3, 0)
        editor.Controls.Add(MakeCaption("Karar Açıklaması"), 0, 1)
        PrepareMultiline(txtEvaluationExplanation)
        editor.Controls.Add(txtEvaluationExplanation, 0, 2)
        editor.SetColumnSpan(txtEvaluationExplanation, 4)
        root.Controls.Add(editor, 0, 2)

        page.Controls.Add(root)
        Return page
    End Function

    Private Sub ConfigureEvaluationGrid()
        ConfigureGridBase(gridEvaluations)
        gridEvaluations.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        gridEvaluations.ReadOnly = True
        gridEvaluations.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "PositionKey", .Visible = False})
        gridEvaluations.Columns.Add(New DataGridViewTextBoxColumn() With {
            .Name = "PositionName", .HeaderText = "DEĞERLENDİRME POZİSYONU",
            .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 26
        })
        gridEvaluations.Columns.Add(New DataGridViewTextBoxColumn() With {
            .Name = "AssignedUser", .HeaderText = "ATANAN KULLANICI",
            .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 18
        })
        gridEvaluations.Columns.Add(New DataGridViewTextBoxColumn() With {
            .Name = "Decision", .HeaderText = "KARAR", .Width = 155
        })
        gridEvaluations.Columns.Add(New DataGridViewTextBoxColumn() With {
            .Name = "Explanation", .HeaderText = "AÇIKLAMA",
            .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 38
        })
        gridEvaluations.Columns.Add(New DataGridViewTextBoxColumn() With {
            .Name = "EvaluatedBy", .HeaderText = "DEĞERLENDİREN", .Width = 145
        })
        gridEvaluations.Columns.Add(New DataGridViewTextBoxColumn() With {
            .Name = "EvaluatedAt", .HeaderText = "TARİH / SAAT", .Width = 145
        })
        AddHandler gridEvaluations.SelectionChanged, AddressOf EvaluationSelectionChanged
    End Sub

    Private Function BuildManagementTab() As TabPage
        Dim page As New TabPage("2. Değerlendirme ve Aksiyon") With {.BackColor = Color.White}
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .Padding = New Padding(16),
            .BackColor = Color.White
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 52.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 110.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 48.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 52.0F))

        Dim decision As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 6, .BackColor = Color.White}
        For Each width As Single In {120.0F, 230.0F, 145.0F, 230.0F, 90.0F, 230.0F}
            decision.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, width))
        Next
        cboDisposition.DropDownStyle = ComboBoxStyle.DropDownList
        cboDisposition.Items.AddRange({"", "MALZEME RET", "MALZEME KABUL", "MALZEME ŞARTLI KABUL", "KAİZEN"})
        AddInline(decision, 0, "Değerlendirme", cboDisposition)
        AddInline(decision, 2, "Kaizen Sorumlusu", txtKaizenResponsible)
        AddInline(decision, 4, "Kaizen No", txtKaizenNo)
        root.Controls.Add(decision, 0, 0)

        Dim rootCausePanel As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2}
        rootCausePanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 28.0F))
        rootCausePanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        rootCausePanel.Controls.Add(MakeCaption("Hatanın Kök Nedeni"), 0, 0)
        PrepareMultiline(txtRootCause)
        rootCausePanel.Controls.Add(txtRootCause, 0, 1)
        root.Controls.Add(rootCausePanel, 0, 1)

        ConfigureActionGrid()
        root.Controls.Add(WrapSection("MGGK Yorumu / Yapılacak İşler", gridActions), 0, 2)
        ConfigureReviewGrid()
        root.Controls.Add(WrapSection("Etkilenen Alan ve Revizyon Kontrolleri", gridReviews), 0, 3)
        page.Controls.Add(root)
        Return page
    End Function

    Private Function BuildVerificationTab() As TabPage
        Dim page As New TabPage("3. Doğrulama ve Kapanış") With {.BackColor = Color.White}
        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 4,
            .RowCount = 7,
            .Padding = New Padding(18, 18, 18, 16),
            .BackColor = Color.White
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 160.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 160.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        For index = 0 To 2
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 44.0F))
        Next
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 28.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 48.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 28.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 52.0F))

        PrepareDatePicker(dtpVerificationDueDate)
        PrepareDatePicker(dtpVerificationDate)
        PrepareYesNoCombo(cboVerificationSuitable)
        PrepareYesNoCombo(cboCloseApproved)
        AddPair(layout, 0, "Doğrulama Termin Tarihi", dtpVerificationDueDate, "Doğrulama Tarihi", dtpVerificationDate)
        AddPair(layout, 1, "Doğrulama Sorumlusu", txtVerificationResponsible, "Doğrulama Uygun mu?", cboVerificationSuitable)
        layout.Controls.Add(MakeCaption("Kapatılsın mı?"), 0, 2)
        layout.Controls.Add(cboCloseApproved, 1, 2)
        layout.Controls.Add(MakeCaption("Doğrulama Faaliyetleri"), 0, 3)
        PrepareMultiline(txtVerificationActivities)
        layout.Controls.Add(txtVerificationActivities, 0, 4)
        layout.SetColumnSpan(txtVerificationActivities, 4)
        layout.Controls.Add(MakeCaption("Kapanış Notu"), 0, 5)
        PrepareMultiline(txtCloseNote)
        layout.Controls.Add(txtCloseNote, 0, 6)
        layout.SetColumnSpan(txtCloseNote, 4)
        page.Controls.Add(layout)
        Return page
    End Function

    Private Sub ConfigureActionGrid()
        ConfigureGridBase(gridActions)
        gridActions.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "No", .HeaderText = "No", .Width = 46, .ReadOnly = True})
        gridActions.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Action", .HeaderText = "Yapılacak İş", .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 55})
        gridActions.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Responsible", .HeaderText = "Sorumlu", .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 20})
        gridActions.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "DueDate", .HeaderText = "Termin Tarihi", .Width = 125})
        gridActions.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "ClosedDate", .HeaderText = "Kapatılma Tarihi", .Width = 135})
        For index = 1 To 5
            gridActions.Rows.Add(index.ToString(), "", "", "", "")
        Next
    End Sub

    Private Sub ConfigureReviewGrid()
        ConfigureGridBase(gridReviews)
        gridReviews.Columns.Add(New DataGridViewTextBoxColumn() With {
            .Name = "Question", .HeaderText = "Kontrol", .ReadOnly = True,
            .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 48
        })
        Dim resultColumn As New DataGridViewComboBoxColumn() With {
            .Name = "Result", .HeaderText = "Sonuç", .Width = 115,
            .FlatStyle = FlatStyle.Flat
        }
        resultColumn.Items.AddRange("", "EVET", "HAYIR")
        gridReviews.Columns.Add(resultColumn)
        gridReviews.Columns.Add(New DataGridViewTextBoxColumn() With {
            .Name = "Detail", .HeaderText = "Evetse Açıklama / Referans",
            .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .FillWeight = 52
        })
        For Each item In ReviewItems
            gridReviews.Rows.Add(item, "", "")
        Next
    End Sub

    Private Shared Sub ConfigureGridBase(grid As DataGridView)
        grid.Dock = DockStyle.Fill
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.AllowUserToResizeRows = False
        grid.RowHeadersVisible = False
        grid.SelectionMode = DataGridViewSelectionMode.CellSelect
        grid.MultiSelect = False
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None
        grid.RowTemplate.Height = 30
        grid.ColumnHeadersHeight = 34
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(222, 233, 246)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(25, 51, 82)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 8.5F, FontStyle.Bold)
        grid.EnableHeadersVisualStyles = False
    End Sub

    Private Shared Function WrapSection(caption As String, content As Control) As Control
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .Margin = New Padding(0, 6, 0, 0)}
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 28.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.Controls.Add(MakeCaption(caption), 0, 0)
        layout.Controls.Add(content, 0, 1)
        Return layout
    End Function

    Private Sub LoadReport()
        If reportRow Is Nothing Then
            reportRow = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            txtRevisionDate.Text = DateTime.Now.ToString("dd.MM.yyyy")
            txtSourceDepartment.Text = "PLASTİKHANE"
            txtQualityPoint.Text = "VARDİYA TAKİP"
            txtPartNameNo.Text = DataService.GetValue(shiftRecord, "ProductNameCode")
            txtTrNo.Text = ExtractTrCode(txtPartNameNo.Text)
            txtQuantity.Text = DataService.GetValue(shiftRecord, "DefectiveQuantity")
            txtOperator.Text = DataService.GetValue(shiftRecord, "Responsible")
            txtNonconformity.Text = DataService.GetValue(shiftRecord, "Problem")
            txtQualityInspector.Text = AppState.CurrentUserName
            txtDetectedBy.Text = AppState.CurrentUserName
            lblReportNo.Text = "Kaydedildiğinde otomatik atanacak"
            lblStatus.Text = "YENİ RAPOR"
        Else
            txtRevisionDate.Text = DisplayDate(DataService.GetValue(reportRow, "RevisionDate"))
            txtSourceDepartment.Text = DataService.GetValue(reportRow, "SourceDepartment")
            txtQualityPoint.Text = DataService.GetValue(reportRow, "QualityControlPoint")
            txtPartNameNo.Text = DataService.GetValue(reportRow, "PartNameNo")
            txtTrNo.Text = DataService.GetValue(reportRow, "TrNo")
            txtPartType.Text = DataService.GetValue(reportRow, "PartType")
            txtQuantity.Text = DataService.GetValue(reportRow, "Quantity")
            txtMachineNo.Text = DataService.GetValue(reportRow, "MachineNo")
            txtOperator.Text = DataService.GetValue(reportRow, "OperatorName")
            txtDefectArea.Text = DataService.GetValue(reportRow, "DefectArea")
            txtDefectCode.Text = DataService.GetValue(reportRow, "DefectCode")
            txtDefectType.Text = DataService.GetValue(reportRow, "DefectType")
            txtNonconformity.Text = DataService.GetValue(reportRow, "NonconformityDescription")
            txtQualityInspector.Text = DataService.GetValue(reportRow, "QualityInspector")
            txtDetectedBy.Text = DataService.GetValue(reportRow, "DetectedBy")
            txtUnitManagerApproval.Text = DataService.GetValue(reportRow, "UnitManagerApproval")
            cboDisposition.Text = DataService.GetValue(reportRow, "Disposition")
            txtKaizenResponsible.Text = DataService.GetValue(reportRow, "KaizenResponsible")
            txtKaizenNo.Text = DataService.GetValue(reportRow, "KaizenNo")
            txtRootCause.Text = DataService.GetValue(reportRow, "RootCause")
            For index = 1 To 5
                Dim row = gridActions.Rows(index - 1)
                row.Cells("Action").Value = DataService.GetValue(reportRow, "Action" & index.ToString())
                row.Cells("Responsible").Value = DataService.GetValue(reportRow, "ActionResponsible" & index.ToString())
                row.Cells("DueDate").Value = DisplayDate(DataService.GetValue(reportRow, "ActionDueDate" & index.ToString()))
                row.Cells("ClosedDate").Value = DisplayDate(DataService.GetValue(reportRow, "ActionClosedDate" & index.ToString()))
            Next
            For index = 0 To ReviewItems.Length - 1
                gridReviews.Rows(index).Cells("Result").Value = LocalYesNo(DataService.GetValue(reportRow, ReviewResultFields(index)))
                gridReviews.Rows(index).Cells("Detail").Value = DataService.GetValue(reportRow, ReviewDetailFields(index))
            Next
            SetOptionalDate(dtpVerificationDueDate, DataService.GetValue(reportRow, "VerificationDueDate"))
            SetOptionalDate(dtpVerificationDate, DataService.GetValue(reportRow, "VerificationDate"))
            txtVerificationActivities.Text = DataService.GetValue(reportRow, "VerificationActivities")
            txtVerificationResponsible.Text = DataService.GetValue(reportRow, "VerificationResponsible")
            cboVerificationSuitable.Text = LocalYesNo(DataService.GetValue(reportRow, "VerificationSuitable"))
            cboCloseApproved.Text = LocalYesNo(DataService.GetValue(reportRow, "CloseApproved"))
            txtCloseNote.Text = DataService.GetValue(reportRow, "CloseNote")
            lblReportNo.Text = DataService.GetValue(reportRow, "ReportNo")
            lblStatus.Text = LocalStatus(DataService.GetValue(reportRow, "Status"))
        End If
        lblShiftNo.Text = DataService.GetValue(shiftRecord, "RecordId")
        LoadEvaluations()
    End Sub

    Private Sub LoadEvaluations()
        gridEvaluations.Rows.Clear()
        Dim reportId = DataService.GetValue(reportRow, "ReportId").Trim()
        If reportId = "" Then
            lblEvaluationInfo.Text = "Üçlü değerlendirme, hata raporu ilk kez kaydedildikten sonra açılır."
            lblSelectedEvaluation.Text = "Önce raporu kaydedin"
            cboEvaluationDecision.SelectedIndex = -1
            txtEvaluationExplanation.Clear()
            btnSaveEvaluation.Enabled = False
            Return
        End If

        Dim evaluations = DataService.GetPlasticShiftErrorReportEvaluations(reportId)
        For Each item In evaluations
            Dim rowIndex = gridEvaluations.Rows.Add(
                item.PositionKey,
                item.PositionName,
                item.AssignedUserName,
                LocalDecision(item.Decision),
                item.Explanation,
                item.EvaluatedBy,
                DisplayDateTime(item.EvaluatedAt))
            Dim row = gridEvaluations.Rows(rowIndex)
            If String.Equals(item.Decision, "APPROVED", StringComparison.OrdinalIgnoreCase) Then
                row.DefaultCellStyle.BackColor = Color.FromArgb(228, 245, 232)
                row.DefaultCellStyle.ForeColor = Color.FromArgb(18, 105, 48)
            ElseIf String.Equals(item.Decision, "REVISION_REQUIRED", StringComparison.OrdinalIgnoreCase) Then
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 230, 230)
                row.DefaultCellStyle.ForeColor = Color.DarkRed
            End If
        Next

        Dim approvedCount = evaluations.Where(
            Function(item) String.Equals(item.Decision, "APPROVED", StringComparison.OrdinalIgnoreCase)).Count()
        If evaluations.Count < PlasticShiftErrorReportEvaluationPositions.AllKeys().Length Then
            lblEvaluationInfo.Text = "Değerlendirme atamaları eksik. Admin, Yönetim ve Sistem bölümünden üç pozisyonu tamamlamalıdır."
            lblEvaluationInfo.BackColor = Color.FromArgb(255, 242, 204)
            lblEvaluationInfo.ForeColor = Color.FromArgb(125, 78, 0)
        ElseIf evaluations.Any(Function(item) String.Equals(item.Decision, "REVISION_REQUIRED", StringComparison.OrdinalIgnoreCase)) Then
            lblEvaluationInfo.Text = "Revizyon gerekli. Uygunsuzluk bilgileri düzeltilip ilgili değerlendirici yeniden karar vermelidir."
            lblEvaluationInfo.BackColor = Color.FromArgb(255, 230, 230)
            lblEvaluationInfo.ForeColor = Color.DarkRed
        ElseIf approvedCount = evaluations.Count Then
            lblEvaluationInfo.Text = "Üç değerlendirme de onaylandı. Değerlendirme, aksiyon ve kapanış alanları kullanılabilir."
            lblEvaluationInfo.BackColor = Color.FromArgb(228, 245, 232)
            lblEvaluationInfo.ForeColor = Color.FromArgb(18, 105, 48)
        Else
            lblEvaluationInfo.Text = approvedCount.ToString() & "/3 onay tamamlandı. Tüm kararlar tamamlanmadan aksiyon süreci açılamaz."
            lblEvaluationInfo.BackColor = Color.FromArgb(235, 243, 252)
            lblEvaluationInfo.ForeColor = Color.FromArgb(25, 71, 124)
        End If

        If gridEvaluations.Rows.Count > 0 Then
            gridEvaluations.Rows(0).Selected = True
            EvaluationSelectionChanged(Nothing, EventArgs.Empty)
        Else
            lblSelectedEvaluation.Text = "Atama bulunamadı"
            btnSaveEvaluation.Enabled = False
        End If
    End Sub

    Private Sub EvaluationSelectionChanged(sender As Object, e As EventArgs)
        If gridEvaluations.SelectedRows.Count = 0 Then Return
        Dim row = gridEvaluations.SelectedRows(0)
        lblSelectedEvaluation.Text = Convert.ToString(row.Cells("PositionName").Value) &
                                     " — " & Convert.ToString(row.Cells("AssignedUser").Value)
        Dim decisionText = Convert.ToString(row.Cells("Decision").Value)
        If decisionText = "ONAY" Then
            cboEvaluationDecision.SelectedItem = "ONAY"
        ElseIf decisionText = "REVİZYON GEREKLİ" Then
            cboEvaluationDecision.SelectedItem = "REVİZYON GEREKLİ"
        Else
            cboEvaluationDecision.SelectedIndex = -1
        End If
        txtEvaluationExplanation.Text = Convert.ToString(row.Cells("Explanation").Value)
        Dim assignedUser = Convert.ToString(row.Cells("AssignedUser").Value).Trim()
        Dim canEvaluate = AppState.IsAdmin OrElse
                          String.Equals(assignedUser, AppState.CurrentUserName, StringComparison.OrdinalIgnoreCase)
        cboEvaluationDecision.Enabled = canEvaluate
        txtEvaluationExplanation.ReadOnly = Not canEvaluate
        btnSaveEvaluation.Enabled = canEvaluate
    End Sub

    Private Sub SaveEvaluation_Click(sender As Object, e As EventArgs)
        Try
            If gridEvaluations.SelectedRows.Count = 0 Then
                Throw New ArgumentException("Değerlendirilecek pozisyonu seçin.")
            End If
            Dim positionKey = Convert.ToString(gridEvaluations.SelectedRows(0).Cells("PositionKey").Value)
            Dim decision = If(cboEvaluationDecision.Text = "ONAY", "APPROVED",
                              If(cboEvaluationDecision.Text = "REVİZYON GEREKLİ", "REVISION_REQUIRED", ""))
            DataService.SavePlasticShiftErrorReportEvaluation(
                DataService.GetValue(reportRow, "ReportId"),
                positionKey,
                decision,
                txtEvaluationExplanation.Text)
            reportRow = DataService.GetPlasticShiftErrorReport(DataService.GetValue(shiftRecord, "RecordId"))
            lblStatus.Text = LocalStatus(DataService.GetValue(reportRow, "Status"))
            LoadEvaluations()
            ApplyPermissions()
            SavedChanges = True
            MessageBox.Show("Değerlendirmeniz kaydedildi.", "Hata Raporu", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Değerlendirme kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub ApplyPermissions()
        Dim isClosed = reportRow IsNot Nothing AndAlso
                       String.Equals(DataService.GetValue(reportRow, "Status"), "CLOSED", StringComparison.OrdinalIgnoreCase)
        Dim initialEditable = canEditInitial AndAlso (Not isClosed OrElse canManage)
        For Each textBox In {
            txtRevisionDate, txtSourceDepartment, txtQualityPoint, txtPartNameNo, txtTrNo, txtPartType,
            txtQuantity, txtMachineNo, txtOperator, txtDefectArea, txtDefectCode, txtDefectType,
            txtQualityInspector, txtDetectedBy, txtUnitManagerApproval, txtNonconformity
        }
            SetTextBoxEditable(textBox, initialEditable)
        Next

        Dim reportId = DataService.GetValue(reportRow, "ReportId").Trim()
        Dim evaluationsApproved = reportId <> "" AndAlso
                                  DataService.ArePlasticShiftErrorReportEvaluationsApproved(reportId)
        Dim managementEditable = canManage AndAlso evaluationsApproved
        For Each control In {DirectCast(cboDisposition, Control), txtKaizenResponsible, txtKaizenNo, txtRootCause,
                             gridActions, gridReviews, txtVerificationActivities, txtVerificationResponsible,
                             dtpVerificationDate, dtpVerificationDueDate, cboVerificationSuitable,
                             cboCloseApproved, txtCloseNote}
            control.Enabled = managementEditable
        Next
        btnSave.Visible = initialEditable OrElse canManage
        If Not canManage Then
            If Not tabs.TabPages(2).Text.Contains("Salt Okunur") Then tabs.TabPages(2).Text &= " (Salt Okunur)"
            If Not tabs.TabPages(3).Text.Contains("Salt Okunur") Then tabs.TabPages(3).Text &= " (Salt Okunur)"
        ElseIf Not evaluationsApproved Then
            tabs.TabPages(2).Text = "3. Değerlendirme ve Aksiyon (3 onay bekleniyor)"
            tabs.TabPages(3).Text = "4. Doğrulama ve Kapanış (3 onay bekleniyor)"
        Else
            tabs.TabPages(2).Text = "3. Değerlendirme ve Aksiyon"
            tabs.TabPages(3).Text = "4. Doğrulama ve Kapanış"
        End If
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        Try
            If txtPartNameNo.Text.Trim() = "" Then Throw New ArgumentException("Parça adı / no zorunludur.")
            If txtNonconformity.Text.Trim() = "" Then Throw New ArgumentException("Uygunsuzluk tanımı zorunludur.")

            Dim row = New Dictionary(Of String, String)(reportRow, StringComparer.OrdinalIgnoreCase)
            row("ShiftRecordId") = DataService.GetValue(shiftRecord, "RecordId")
            row("RevisionDate") = NormalizeDateText(txtRevisionDate.Text)
            row("SourceDepartment") = txtSourceDepartment.Text.Trim()
            row("QualityControlPoint") = txtQualityPoint.Text.Trim()
            row("PartNameNo") = txtPartNameNo.Text.Trim()
            row("TrNo") = txtTrNo.Text.Trim()
            row("PartType") = txtPartType.Text.Trim()
            row("Quantity") = txtQuantity.Text.Trim()
            row("MachineNo") = txtMachineNo.Text.Trim()
            row("OperatorName") = txtOperator.Text.Trim()
            row("DefectArea") = txtDefectArea.Text.Trim()
            row("DefectCode") = txtDefectCode.Text.Trim()
            row("DefectType") = txtDefectType.Text.Trim()
            row("NonconformityDescription") = txtNonconformity.Text.Trim()
            row("QualityInspector") = txtQualityInspector.Text.Trim()
            row("DetectedBy") = txtDetectedBy.Text.Trim()
            row("UnitManagerApproval") = txtUnitManagerApproval.Text.Trim()
            row("Disposition") = cboDisposition.Text.Trim()
            row("KaizenResponsible") = txtKaizenResponsible.Text.Trim()
            row("KaizenNo") = txtKaizenNo.Text.Trim()
            row("RootCause") = txtRootCause.Text.Trim()
            For index = 1 To 5
                Dim actionRow = gridActions.Rows(index - 1)
                row("Action" & index.ToString()) = CellText(actionRow, "Action")
                row("ActionResponsible" & index.ToString()) = CellText(actionRow, "Responsible")
                row("ActionDueDate" & index.ToString()) = NormalizeDateText(CellText(actionRow, "DueDate"))
                row("ActionClosedDate" & index.ToString()) = NormalizeDateText(CellText(actionRow, "ClosedDate"))
            Next
            For index = 0 To ReviewItems.Length - 1
                row(ReviewResultFields(index)) = YesNoValue(CellText(gridReviews.Rows(index), "Result"))
                row(ReviewDetailFields(index)) = CellText(gridReviews.Rows(index), "Detail")
            Next
            row("VerificationDueDate") = OptionalDateValue(dtpVerificationDueDate)
            row("VerificationDate") = OptionalDateValue(dtpVerificationDate)
            row("VerificationActivities") = txtVerificationActivities.Text.Trim()
            row("VerificationResponsible") = txtVerificationResponsible.Text.Trim()
            row("VerificationSuitable") = YesNoValue(cboVerificationSuitable.Text)
            row("CloseApproved") = YesNoValue(cboCloseApproved.Text)
            row("CloseNote") = txtCloseNote.Text.Trim()

            ValidateWorkflow(row)
            Dim wasNew = DataService.GetValue(reportRow, "ReportId").Trim() = ""
            Dim reportId = DataService.SavePlasticShiftErrorReport(row)
            reportRow = DataService.GetPlasticShiftErrorReport(DataService.GetValue(shiftRecord, "RecordId"))
            If reportRow Is Nothing Then Throw New InvalidOperationException("Kaydedilen hata raporu tekrar okunamadı.")
            reportRow("ReportId") = reportId
            SavedChanges = True
            lblReportNo.Text = DataService.GetValue(reportRow, "ReportNo")
            lblStatus.Text = LocalStatus(DataService.GetValue(reportRow, "Status"))
            LoadEvaluations()
            ApplyPermissions()
            Dim information = "Hata raporu kaydedildi."
            If wasNew Then
                Dim mailWarning As String = ""
                If Not PlasticShiftErrorReportEmailNotificationService.TrySendCreatedNotification(
                    reportId, reportRow, mailWarning) AndAlso mailWarning <> "" Then
                    information &= Environment.NewLine & Environment.NewLine & mailWarning
                End If
            End If
            MessageBox.Show(information, "Hata Raporu", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Hata raporu kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Shared Sub ValidateWorkflow(row As Dictionary(Of String, String))
        Dim disposition = DataService.GetValue(row, "Disposition").Trim()
        Dim rootCause = DataService.GetValue(row, "RootCause").Trim()
        Dim closeApproved = YesNoValue(DataService.GetValue(row, "CloseApproved"))
        Dim verificationSuitable = YesNoValue(DataService.GetValue(row, "VerificationSuitable"))

        If String.Equals(disposition, "KAİZEN", StringComparison.OrdinalIgnoreCase) Then
            If DataService.GetValue(row, "KaizenResponsible").Trim() = "" OrElse
               DataService.GetValue(row, "KaizenNo").Trim() = "" Then
                Throw New ArgumentException("KAİZEN seçildiğinde Kaizen sorumlusu ve Kaizen numarası zorunludur.")
            End If
        End If

        Dim hasAction As Boolean = False
        For index = 1 To 5
            Dim actionText = DataService.GetValue(row, "Action" & index.ToString()).Trim()
            If actionText = "" Then Continue For
            hasAction = True
            If DataService.GetValue(row, "ActionResponsible" & index.ToString()).Trim() = "" Then
                Throw New ArgumentException(index.ToString() & ". aksiyon için sorumlu kişi zorunludur.")
            End If
            Dim dueText = DataService.GetValue(row, "ActionDueDate" & index.ToString()).Trim()
            Dim dueDate As DateTime
            If dueText = "" OrElse Not DateTime.TryParse(dueText, dueDate) Then
                Throw New ArgumentException(index.ToString() & ". aksiyon için geçerli bir termin tarihi zorunludur.")
            End If
            Dim closedText = DataService.GetValue(row, "ActionClosedDate" & index.ToString()).Trim()
            Dim closedDate As DateTime
            If closedText <> "" AndAlso Not DateTime.TryParse(closedText, closedDate) Then
                Throw New ArgumentException(index.ToString() & ". aksiyonun kapatılma tarihi geçerli değil.")
            End If
        Next

        If disposition <> "" OrElse rootCause <> "" OrElse hasAction Then
            If disposition = "" Then Throw New ArgumentException("Değerlendirme aşamasında karar alanı zorunludur.")
            If rootCause = "" Then Throw New ArgumentException("Değerlendirme aşamasında hatanın kök nedeni zorunludur.")
            If Not hasAction Then Throw New ArgumentException("Değerlendirme aşamasında en az bir aksiyon tanımlanmalıdır.")
        End If

        If closeApproved = "YES" Then
            If verificationSuitable <> "YES" Then
                Throw New ArgumentException("Rapor yalnızca doğrulama sonucu uygun olduğunda kapatılabilir.")
            End If
            If DataService.GetValue(row, "VerificationDate").Trim() = "" OrElse
               DataService.GetValue(row, "VerificationResponsible").Trim() = "" OrElse
               DataService.GetValue(row, "VerificationActivities").Trim() = "" Then
                Throw New ArgumentException("Kapanış için doğrulama tarihi, sorumlusu ve faaliyet açıklaması zorunludur.")
            End If
        End If
    End Sub

    Private Shared Sub AddPair(layout As TableLayoutPanel, rowIndex As Integer,
                               leftCaption As String, leftControl As Control,
                               rightCaption As String, rightControl As Control)
        layout.Controls.Add(MakeCaption(leftCaption), 0, rowIndex)
        PrepareControl(leftControl)
        layout.Controls.Add(leftControl, 1, rowIndex)
        layout.Controls.Add(MakeCaption(rightCaption), 2, rowIndex)
        PrepareControl(rightControl)
        layout.Controls.Add(rightControl, 3, rowIndex)
    End Sub

    Private Shared Sub AddInline(layout As TableLayoutPanel, captionColumn As Integer, caption As String, input As Control)
        layout.Controls.Add(MakeCaption(caption), captionColumn, 0)
        PrepareControl(input)
        layout.Controls.Add(input, captionColumn + 1, 0)
    End Sub

    Private Shared Function MakeCaption(text As String) As Label
        Return New Label() With {
            .Text = text,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 8.7F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(35, 55, 80),
            .Margin = New Padding(0, 0, 8, 0)
        }
    End Function

    Private Shared Sub PrepareControl(control As Control)
        control.Dock = DockStyle.Fill
        control.Margin = New Padding(3, 5, 12, 6)
        If TypeOf control Is TextBox Then PrepareInput(DirectCast(control, TextBox))
    End Sub

    Private Shared Sub PrepareInput(textBox As TextBox)
        textBox.BorderStyle = BorderStyle.FixedSingle
        textBox.BackColor = Color.White
        textBox.Font = New Font("Segoe UI", 9.0F)
    End Sub

    Private Shared Sub PrepareMultiline(textBox As TextBox)
        PrepareInput(textBox)
        textBox.Multiline = True
        textBox.ScrollBars = ScrollBars.Vertical
        textBox.Dock = DockStyle.Fill
        textBox.Margin = New Padding(3, 3, 12, 8)
    End Sub

    Private Shared Sub PrepareDatePicker(picker As DateTimePicker)
        picker.Format = DateTimePickerFormat.Custom
        picker.CustomFormat = "dd.MM.yyyy"
        picker.ShowCheckBox = True
        picker.Checked = False
    End Sub

    Private Shared Sub PrepareYesNoCombo(combo As ComboBox)
        combo.DropDownStyle = ComboBoxStyle.DropDownList
        combo.Items.AddRange({"", "EVET", "HAYIR"})
    End Sub

    Private Shared Sub ConfigureButton(button As Button, caption As String, width As Integer, backColor As Color, foreColor As Color)
        button.Text = caption
        button.Width = width
        button.Height = 36
        button.Margin = New Padding(8, 0, 0, 0)
        button.FlatStyle = FlatStyle.Flat
        button.FlatAppearance.BorderColor = Color.FromArgb(175, 189, 207)
        button.BackColor = backColor
        button.ForeColor = foreColor
        button.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        button.AutoEllipsis = False
        button.Tag = "RESPONSIVE_NO_AUTO_SCALE"
    End Sub

    Private Shared Sub SetTextBoxEditable(textBox As TextBox, editable As Boolean)
        textBox.ReadOnly = Not editable
        textBox.BackColor = If(editable, Color.White, Color.FromArgb(242, 245, 248))
    End Sub

    Private Shared Function ExtractTrCode(value As String) As String
        Dim first = If(value, "").Split("|"c).FirstOrDefault()
        Return If(first, "").Trim()
    End Function

    Private Shared Function DisplayDate(value As String) As String
        Dim parsed As DateTime
        If DateTime.TryParse(value, parsed) Then Return parsed.ToString("dd.MM.yyyy")
        Return If(value, "").Trim()
    End Function

    Private Shared Function NormalizeDateText(value As String) As String
        Dim text = If(value, "").Trim()
        If text = "" Then Return ""
        Dim parsed As DateTime
        If DateTime.TryParse(text, parsed) Then Return parsed.ToString("yyyy-MM-dd")
        Return text
    End Function

    Private Shared Function DisplayDateTime(value As String) As String
        Dim parsed As DateTime
        If DateTime.TryParse(value, parsed) Then Return parsed.ToString("dd.MM.yyyy HH:mm")
        Return If(value, "").Trim()
    End Function

    Private Shared Sub SetOptionalDate(picker As DateTimePicker, value As String)
        Dim parsed As DateTime
        If DateTime.TryParse(value, parsed) Then
            picker.Value = parsed
            picker.Checked = True
        Else
            picker.Checked = False
        End If
    End Sub

    Private Shared Function OptionalDateValue(picker As DateTimePicker) As String
        Return If(picker.Checked, picker.Value.ToString("yyyy-MM-dd"), "")
    End Function

    Private Shared Function CellText(row As DataGridViewRow, columnName As String) As String
        Return Convert.ToString(row.Cells(columnName).Value).Trim()
    End Function

    Private Shared Function YesNoValue(value As String) As String
        Dim normalized = If(value, "").Trim().ToUpperInvariant()
        If normalized = "EVET" OrElse normalized = "YES" Then Return "YES"
        If normalized = "HAYIR" OrElse normalized = "NO" Then Return "NO"
        Return ""
    End Function

    Private Shared Function LocalYesNo(value As String) As String
        Dim normalized = If(value, "").Trim().ToUpperInvariant()
        If normalized = "YES" OrElse normalized = "EVET" Then Return "EVET"
        If normalized = "NO" OrElse normalized = "HAYIR" Then Return "HAYIR"
        Return ""
    End Function

    Private Shared Function LocalDecision(value As String) As String
        Select Case If(value, "").Trim().ToUpperInvariant()
            Case "APPROVED" : Return "ONAY"
            Case "REVISION_REQUIRED" : Return "REVİZYON GEREKLİ"
            Case Else : Return "BEKLİYOR"
        End Select
    End Function

    Private Shared Function LocalStatus(value As String) As String
        Select Case If(value, "").Trim().ToUpperInvariant()
            Case "PENDING_EVALUATION" : Return "DEĞERLENDİRME BEKLİYOR"
            Case "REVISION_REQUIRED" : Return "REVİZYON GEREKLİ"
            Case "APPROVED" : Return "DEĞERLENDİRME ONAYLANDI"
            Case "CLOSED" : Return "KAPALI"
            Case "IN_PROGRESS" : Return "AKSİYON AŞAMASINDA"
            Case Else : Return "AÇIK"
        End Select
    End Function
End Class
