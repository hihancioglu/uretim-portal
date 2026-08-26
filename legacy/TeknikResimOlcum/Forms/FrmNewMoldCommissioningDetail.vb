Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmNewMoldCommissioningDetail
    Inherits Form

    Private Shared ReadOnly PreAcceptanceItems As String() = {
        "Kalıp kodu mevcut", "Ürün kodu mevcut", "Kalıp ağırlığı belirtilmiş", "Kaldırma noktaları uygun",
        "Taşıma kilidi mevcut", "Merkezleme flanşı uygun", "Meme burcu uygun", "Kalıp açma-kapama hareketi uygun",
        "İtici sistemi uygun", "Maça hareketleri uygun", "Limit anahtarları çalışıyor", "Soğutma kanalları açık",
        "Soğutma sisteminde kaçak yok", "Su giriş-çıkışları işaretli", "Sıcak yolluk dirençleri uygun",
        "Termokupllar çalışıyor", "Elektrik topraklaması uygun", "Göz numaraları tanımlı",
        "Hava tahliyeleri mevcut", "Yedek parça listesi mevcut", "Kalıp teknik dosyası tamamlandı"
    }

    Private commissioningId As String
    Private ReadOnly canEdit As Boolean
    Private currentRow As Dictionary(Of String, String)

    Private ReadOnly lblRecord As New Label()
    Private ReadOnly lblStage As New Label()
    Private ReadOnly tabs As New TabControl()
    Private ReadOnly requestControls As New Dictionary(Of String, Control)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly checklistGrid As New DataGridView()
    Private ReadOnly trialsGrid As New DataGridView()
    Private ReadOnly actionsGrid As New DataGridView()
    Private ReadOnly cboMechanical As New ComboBox()
    Private ReadOnly cboProduct As New ComboBox()
    Private ReadOnly cboProcess As New ComboBox()
    Private ReadOnly cboFinalDecision As New ComboBox()
    Private ReadOnly txtFinalNote As New TextBox()
    Private ReadOnly dtpConditionalUntil As New DateTimePicker()
    Private ReadOnly txtConditionalQuantity As New TextBox()
    Private ReadOnly dtpNextTrial As New DateTimePicker()
    Private ReadOnly lblMechanicalMeta As New Label()
    Private ReadOnly lblProductMeta As New Label()
    Private ReadOnly lblProcessMeta As New Label()
    Private ReadOnly cboMeasurementProduct As New ComboBox()
    Private ReadOnly lblDrawingStatus As New Label()
    Private ReadOnly lblControlPointStatus As New Label()
    Private ReadOnly lblMeasurementStatus As New Label()
    Private ReadOnly linkedMeasurementsGrid As New DataGridView()
    Private measurementProducts As New List(Of ProductInfo)()
    Private loadedMechanical As String = ""
    Private loadedProduct As String = ""
    Private loadedProcess As String = ""

    Public Sub New(id As String)
        AuthorizationService.Require(AppState.CanOpenNewMoldCommissioning, "Yeni Kalıp Devreye Alma Detayı")
        commissioningId = If(id, "").Trim()
        canEdit = AppState.CanModifyNewMoldCommissioning
        AppIconService.Apply(Me)
        Text = If(commissioningId.Length = 0, "Yeni Kalıp Devreye Alma Kaydı", "Kalıp Devreye Alma Detayı")
        StartPosition = FormStartPosition.CenterScreen
        WindowState = FormWindowState.Maximized
        MinimumSize = New Size(1150, 700)
        BackColor = Color.FromArgb(244, 247, 251)
        Font = New Font("Segoe UI", 9.0F)
        BuildScreen()
        LoadRecord()
        ApplyAccess()
    End Sub

    Private Sub BuildScreen()
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .Padding = New Padding(10),
            .BackColor = BackColor
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 56))
        Controls.Add(root)

        Dim header As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 3,
            .RowCount = 1,
            .BackColor = Color.FromArgb(37, 82, 134),
            .Padding = New Padding(18, 0, 18, 0),
            .Margin = New Padding(0, 0, 0, 6)
        }
        header.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        header.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 340))
        header.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 220))
        header.Controls.Add(New Label() With {
            .Text = "Yeni Kalıp Devreye Alma",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 14.0F, FontStyle.Bold)
        }, 0, 0)
        lblRecord.Dock = DockStyle.Fill
        lblRecord.ForeColor = Color.White
        lblRecord.TextAlign = ContentAlignment.MiddleCenter
        lblRecord.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblStage.Dock = DockStyle.Fill
        lblStage.ForeColor = Color.White
        lblStage.TextAlign = ContentAlignment.MiddleRight
        lblStage.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        header.Controls.Add(lblRecord, 1, 0)
        header.Controls.Add(lblStage, 2, 0)
        root.Controls.Add(header, 0, 0)

        tabs.Dock = DockStyle.Fill
        tabs.Font = New Font("Segoe UI", 9.0F)
        tabs.TabPages.Add(BuildRequestTab())
        tabs.TabPages.Add(BuildDrawingMeasurementTab())
        tabs.TabPages.Add(BuildChecklistTab())
        tabs.TabPages.Add(BuildTrialsTab())
        tabs.TabPages.Add(BuildActionsTab())
        tabs.TabPages.Add(BuildApprovalTab())
        root.Controls.Add(tabs, 0, 1)

        Dim footer As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False,
            .BackColor = Color.White,
            .Padding = New Padding(8),
            .Margin = New Padding(0, 6, 0, 0)
        }
        Dim btnClose = MakeButton("Kapat", Color.White, Color.Black, 105)
        footer.Controls.Add(btnClose)
        AddHandler btnClose.Click, Sub() Close()
        If canEdit Then
            Dim btnSave = MakeButton("Kaydet / Güncelle", Color.FromArgb(37, 82, 134), Color.White, 165)
            footer.Controls.Add(btnSave)
            AddHandler btnSave.Click, AddressOf SaveRecord
            If AppState.CanDeleteNewMoldCommissioning AndAlso commissioningId.Length > 0 Then
                Dim btnDelete = MakeButton("Kaydı Sil", Color.FromArgb(255, 232, 232), Color.DarkRed, 115)
                footer.Controls.Add(btnDelete)
                AddHandler btnDelete.Click, AddressOf DeleteRecord
            End If
        End If
        root.Controls.Add(footer, 0, 2)
    End Sub

    Private Function BuildRequestTab() As TabPage
        Dim page As New TabPage("1. Talep ve Dokümanlar") With {.Padding = New Padding(12), .BackColor = Color.White}
        Dim table As New TableLayoutPanel() With {
            .Dock = DockStyle.Top,
            .AutoSize = True,
            .ColumnCount = 4,
            .RowCount = 14,
            .Padding = New Padding(8)
        }
        table.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 170))
        table.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        table.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 170))
        table.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))

        AddRequestField(table, 0, 0, "Ürün Adı", "ProductName")
        AddRequestField(table, 0, 2, "Ürün / TR Kodu", "ProductCode")
        AddRequestField(table, 1, 0, "Teknik Resim No", "DrawingNo")
        AddRequestField(table, 1, 2, "Revizyon", "DrawingRev")
        AddRequestField(table, 2, 0, "Kalıp Kodu", "MoldCode")
        AddRequestField(table, 2, 2, "Kalıp Üreticisi", "MoldManufacturer")
        AddRequestField(table, 3, 0, "Göz Sayısı", "CavityCount")
        AddRequestField(table, 3, 2, "Planlanan Makine", "PlannedMachine")
        AddRequestField(table, 4, 0, "Hammadde", "RawMaterial")
        AddRequestField(table, 4, 2, "Renk", "Color")
        AddRequestField(table, 5, 0, "Masterbatch", "Masterbatch")
        AddRequestField(table, 5, 2, "Hedef Çevrim (sn)", "TargetCycleSeconds")
        AddRequestField(table, 6, 0, "Planlanan Miktar", "PlannedQuantity")
        AddRequestDateField(table, 6, 2, "İstenen İlk Üretim", "RequestedProductionDate")
        AddRequestField(table, 7, 0, "Katılımcı Bölümler", "ParticipatingDepartments")
        AddRequestMultiline(table, 8, "Kritik Ölçüler", "CriticalDimensions")
        AddRequestMultiline(table, 9, "Özel Karakteristikler", "SpecialCharacteristics")
        AddRequestMultiline(table, 10, "Fonksiyon Testleri", "FunctionTests")
        AddRequestMultiline(table, 11, "Eşleşen Parçalar", "MatingParts")
        AddRequestMultiline(table, 12, "Müşteri Özel Şartları", "CustomerRequirements")
        AddRequestMultiline(table, 13, "Dokümanlar / Not", "DocumentsNote")
        page.Controls.Add(New Panel() With {.Dock = DockStyle.Fill, .AutoScroll = True})
        page.Controls(0).Controls.Add(table)
        Return page
    End Function

    Private Function BuildDrawingMeasurementTab() As TabPage
        Dim page As New TabPage("2. Teknik Resim ve Parça Ölçümü") With {
            .Padding = New Padding(12),
            .BackColor = Color.White
        }
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .Padding = New Padding(6)
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 118))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

        Dim top As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 4,
            .RowCount = 2,
            .Padding = New Padding(10),
            .BackColor = Color.FromArgb(241, 247, 253)
        }
        top.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 180))
        top.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        top.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 165))
        top.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 165))
        top.RowStyles.Add(New RowStyle(SizeType.Absolute, 40))
        top.RowStyles.Add(New RowStyle(SizeType.Absolute, 48))

        top.Controls.Add(MakeFieldLabel("Ürün / Teknik Resim"), 0, 0)
        cboMeasurementProduct.Dock = DockStyle.Fill
        cboMeasurementProduct.DropDownStyle = ComboBoxStyle.DropDownList
        ' DropDownList ile ListItems otomatik tamamlama birlikte kullanılamaz.
        ' Bu ayarlar form oluşturulurken InvalidOperationException üretiyordu.
        cboMeasurementProduct.AutoCompleteMode = AutoCompleteMode.None
        cboMeasurementProduct.AutoCompleteSource = AutoCompleteSource.None
        top.Controls.Add(cboMeasurementProduct, 1, 0)

        Dim btnApplyProduct = MakeButton("Ürünü Kayda Aktar", Color.White, Color.FromArgb(24, 66, 116), 155)
        btnApplyProduct.Dock = DockStyle.Fill
        top.Controls.Add(btnApplyProduct, 2, 0)
        AddHandler btnApplyProduct.Click, Sub() ApplySelectedMeasurementProduct()

        Dim btnRefresh = MakeButton("Listeyi Yenile", Color.White, Color.FromArgb(24, 66, 116), 155)
        btnRefresh.Dock = DockStyle.Fill
        top.Controls.Add(btnRefresh, 3, 0)
        AddHandler btnRefresh.Click,
            Sub()
                LoadMeasurementProducts()
                RefreshLinkedMeasurements()
            End Sub

        Dim statusPanel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 3,
            .RowCount = 1,
            .Margin = New Padding(0, 4, 8, 0)
        }
        statusPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 34))
        statusPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33))
        statusPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33))
        ConfigureStatusLabel(lblDrawingStatus)
        ConfigureStatusLabel(lblControlPointStatus)
        ConfigureStatusLabel(lblMeasurementStatus)
        statusPanel.Controls.Add(lblDrawingStatus, 0, 0)
        statusPanel.Controls.Add(lblControlPointStatus, 1, 0)
        statusPanel.Controls.Add(lblMeasurementStatus, 2, 0)
        top.Controls.Add(statusPanel, 0, 1)
        top.SetColumnSpan(statusPanel, 2)

        Dim btnDrawing = MakeButton("Teknik Resmi Yönet", Color.White, Color.FromArgb(24, 66, 116), 155)
        btnDrawing.Dock = DockStyle.Fill
        btnDrawing.Enabled = AppState.CanOpenTechnicalDrawingAdmin
        top.Controls.Add(btnDrawing, 2, 1)
        AddHandler btnDrawing.Click,
            Sub()
                If Not AppState.CanOpenTechnicalDrawingAdmin Then Return
                Using form As New FrmProductAdmin()
                    form.ShowDialog(Me)
                End Using
                LoadMeasurementProducts()
            End Sub

        Dim btnMeasure = MakeButton("Parça Ölçümü Başlat", Color.FromArgb(40, 126, 75), Color.White, 155)
        btnMeasure.Dock = DockStyle.Fill
        btnMeasure.Enabled = canEdit
        top.Controls.Add(btnMeasure, 3, 1)
        AddHandler btnMeasure.Click, AddressOf StartPartMeasurement
        root.Controls.Add(top, 0, 0)

        ConfigureGrid(linkedMeasurementsGrid)
        linkedMeasurementsGrid.AllowUserToAddRows = False
        linkedMeasurementsGrid.AllowUserToDeleteRows = False
        linkedMeasurementsGrid.ReadOnly = True
        linkedMeasurementsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        linkedMeasurementsGrid.MultiSelect = False
        linkedMeasurementsGrid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "MeasurementDate", .HeaderText = "ÖLÇÜM TARİHİ", .Width = 150})
        linkedMeasurementsGrid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "RecordId", .HeaderText = "KAYIT NO", .Width = 205})
        linkedMeasurementsGrid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Drawing", .HeaderText = "TR / REVİZYON", .Width = 190})
        linkedMeasurementsGrid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Eye", .HeaderText = "GÖZ", .Width = 85})
        linkedMeasurementsGrid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "MeasureCount", .HeaderText = "ÖLÇÜ", .Width = 80})
        linkedMeasurementsGrid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "OkCount", .HeaderText = "OK", .Width = 75})
        linkedMeasurementsGrid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "NokCount", .HeaderText = "NOK", .Width = 75})
        linkedMeasurementsGrid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "ErrorCount", .HeaderText = "HATALI", .Width = 85})
        linkedMeasurementsGrid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "OperatorName", .HeaderText = "ÖLÇEN", .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill})
        AddHandler linkedMeasurementsGrid.CellDoubleClick, AddressOf OpenLinkedMeasurement
        root.Controls.Add(linkedMeasurementsGrid, 0, 1)

        AddHandler cboMeasurementProduct.SelectedIndexChanged, Sub() RefreshDrawingMeasurementStatus()
        page.Controls.Add(root)
        Return page
    End Function

    Private Shared Sub ConfigureStatusLabel(label As Label)
        label.Dock = DockStyle.Fill
        label.TextAlign = ContentAlignment.MiddleLeft
        label.Font = New Font("Segoe UI", 8.5F, FontStyle.Bold)
        label.Padding = New Padding(8, 0, 4, 0)
        label.BackColor = Color.White
        label.ForeColor = Color.FromArgb(75, 85, 98)
        label.Margin = New Padding(0, 0, 6, 0)
    End Sub

    Private Function BuildChecklistTab() As TabPage
        Dim page As New TabPage("3. Kalıphane Ön Kabul") With {.Padding = New Padding(12), .BackColor = Color.White}
        ConfigureGrid(checklistGrid)
        checklistGrid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "ItemNo", .HeaderText = "NO", .Width = 55, .ReadOnly = True})
        checklistGrid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "ItemText", .HeaderText = "KONTROL MADDESİ", .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .ReadOnly = True})
        checklistGrid.Columns.Add(New DataGridViewComboBoxColumn() With {.Name = "Result", .HeaderText = "SONUÇ", .Width = 150, .DataSource = New String() {"", "UYGUN", "UYGUN DEĞİL", "UYGULANMAZ"}})
        checklistGrid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Explanation", .HeaderText = "AÇIKLAMA", .Width = 420})
        checklistGrid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "CheckedBy", .HeaderText = "KONTROL EDEN", .Width = 150, .ReadOnly = True})
        checklistGrid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "CheckedAt", .HeaderText = "KONTROL TARİHİ", .Width = 145, .ReadOnly = True})
        For i = 0 To PreAcceptanceItems.Length - 1
            checklistGrid.Rows.Add((i + 1).ToString(), PreAcceptanceItems(i), "", "", "", "")
        Next
        AddHandler checklistGrid.CellValueChanged, AddressOf ChecklistCellChanged
        page.Controls.Add(checklistGrid)
        Return page
    End Function

    Private Function BuildTrialsTab() As TabPage
        Dim page As New TabPage("4. T0 / T1 / T2 Denemeleri") With {.Padding = New Padding(12), .BackColor = Color.White}
        Dim root As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2}
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 42))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        root.Controls.Add(New Label() With {
            .Text = "T0: ilk teknik deneme • T1/T2: düzeltme sonrası doğrulama • Önerilen: T0'da göz başına en az 5, nihai onayda en az 30 ardışık parça.",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .BackColor = Color.FromArgb(235, 243, 252),
            .ForeColor = Color.FromArgb(24, 66, 116),
            .Padding = New Padding(12, 0, 0, 0)
        }, 0, 0)
        ConfigureGrid(trialsGrid)
        AddGridText(trialsGrid, "TrialNo", "DENEME", 75)
        AddGridText(trialsGrid, "TrialDate", "TARİH / SAAT", 135)
        AddGridText(trialsGrid, "MachineNo", "MAKİNE", 100)
        AddGridText(trialsGrid, "MaterialLot", "MALZEME LOT", 120)
        AddGridText(trialsGrid, "SamplePerCavity", "GÖZ BAŞI NUMUNE", 105)
        AddGridText(trialsGrid, "CycleTime", "ÇEVRİM", 85)
        AddGridText(trialsGrid, "PartWeight", "PARÇA AĞIRLIĞI (g)", 110)
        AddGridText(trialsGrid, "RunnerWeight", "YOLLUK AĞIRLIĞI (g)", 120)
        AddGridCombo(trialsGrid, "ProcessStatus", "PROSES", 120, {"", "HAZIRLIK", "KARARSIZ", "STABİL"})
        AddGridCombo(trialsGrid, "VisualResult", "GÖRSEL", 105, {"", "UYGUN", "UYGUN DEĞİL"})
        AddGridCombo(trialsGrid, "FunctionResult", "FONKSİYON", 105, {"", "UYGUN", "UYGUN DEĞİL"})
        AddGridCombo(trialsGrid, "MeasurementResult", "ÖLÇÜM", 105, {"", "UYGUN", "UYGUN DEĞİL"})
        AddGridCombo(trialsGrid, "QualityValidationResult", "KALİTE DOĞRULAMA", 130, {"", "BEKLİYOR", "UYGUN", "UYGUN DEĞİL"})
        AddGridText(trialsGrid, "Nonconformity", "UYGUNSUZLUK / NOT", 260, DataGridViewAutoSizeColumnMode.Fill)
        root.Controls.Add(trialsGrid, 0, 1)
        page.Controls.Add(root)
        Return page
    End Function

    Private Function BuildActionsTab() As TabPage
        Dim page As New TabPage("5. Düzeltmeler ve Aksiyonlar") With {.Padding = New Padding(12), .BackColor = Color.White}
        ConfigureGrid(actionsGrid)
        AddGridText(actionsGrid, "TrialNo", "DENEME", 75)
        AddGridCombo(actionsGrid, "Severity", "ÖNEM", 100, {"", "DÜŞÜK", "ORTA", "YÜKSEK", "KRİTİK"})
        AddGridText(actionsGrid, "CavityNo", "GÖZ", 70)
        AddGridText(actionsGrid, "Description", "UYGUNSUZLUK", 250)
        AddGridText(actionsGrid, "ProbableCause", "OLASI NEDEN", 220)
        AddGridText(actionsGrid, "ResponsibleDepartment", "SORUMLU BÖLÜM", 150)
        AddGridText(actionsGrid, "Action", "YAPILACAK AKSİYON", 270, DataGridViewAutoSizeColumnMode.Fill)
        AddGridText(actionsGrid, "DueDate", "TERMİN", 105)
        AddGridCombo(actionsGrid, "Status", "DURUM", 110, {"", "AÇIK", "İŞLEMDE", "TAMAMLANDI"})
        AddGridText(actionsGrid, "VerificationNote", "DOĞRULAMA NOTU", 220)
        page.Controls.Add(actionsGrid)
        Return page
    End Function

    Private Function BuildApprovalTab() As TabPage
        Dim page As New TabPage("6. Onay ve Devir") With {.Padding = New Padding(18), .BackColor = Color.White}
        Dim table As New TableLayoutPanel() With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 3, .RowCount = 9, .Padding = New Padding(16)}
        table.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 235))
        table.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 320))
        table.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        SetupApprovalCombo(cboMechanical)
        SetupApprovalCombo(cboProduct)
        SetupApprovalCombo(cboProcess)
        SetupApprovalCombo(cboFinalDecision, New String() {"", "BEKLİYOR", "ŞARTLI ONAY", "ONAYLANDI", "RED"})
        AddApprovalRow(table, 0, "Kalıp Mekanik Onayı", cboMechanical, lblMechanicalMeta, "Kalıphane")
        AddApprovalRow(table, 1, "Ürün Uygunluk Onayı", cboProduct, lblProductMeta, "Kalite")
        AddApprovalRow(table, 2, "Proses / Seri Üretim Onayı", cboProcess, lblProcessMeta, "Üretim")
        AddApprovalRow(table, 3, "Nihai Karar", cboFinalDecision, New Label(), "Kalıphane + Üretim + Kalite")
        txtFinalNote.Multiline = True
        txtFinalNote.Height = 95
        txtFinalNote.Dock = DockStyle.Fill
        table.Controls.Add(MakeFieldLabel("Nihai Karar Notu"), 0, 4)
        table.Controls.Add(txtFinalNote, 1, 4)
        table.SetColumnSpan(txtFinalNote, 2)
        ConfigureNullableDate(dtpConditionalUntil)
        table.Controls.Add(MakeFieldLabel("Şartlı Onay Bitişi"), 0, 5)
        table.Controls.Add(dtpConditionalUntil, 1, 5)
        txtConditionalQuantity.Dock = DockStyle.Fill
        table.Controls.Add(MakeFieldLabel("Şartlı Üretim Miktarı"), 0, 6)
        table.Controls.Add(txtConditionalQuantity, 1, 6)
        ConfigureNullableDate(dtpNextTrial)
        table.Controls.Add(MakeFieldLabel("Sonraki Deneme Tarihi"), 0, 7)
        table.Controls.Add(dtpNextTrial, 1, 7)
        table.Controls.Add(New Label() With {
            .Text = "Nihai onay; mekanik, ürün ve proses onaylarının üçü de ONAYLANDI olduğunda kaydı tamamlar. Şartlı onayda miktar/süre ve sonraki deneme açıkça kaydedilmelidir.",
            .Dock = DockStyle.Fill,
            .AutoSize = True,
            .BackColor = Color.FromArgb(255, 247, 218),
            .ForeColor = Color.FromArgb(125, 78, 0),
            .Padding = New Padding(12)
        }, 0, 8)
        table.SetColumnSpan(table.GetControlFromPosition(0, 8), 3)
        page.Controls.Add(table)
        Return page
    End Function

    Private Sub LoadMeasurementProducts()
        Dim selectedKey = ""
        Dim selected = TryCast(cboMeasurementProduct.SelectedItem, ProductInfo)
        If selected IsNot Nothing Then selectedKey = ProductKey(selected)

        measurementProducts = DataService.GetProducts(True).
            Where(Function(product) String.Equals(ProductInfo.NormalizeDrawingScope(product.DrawingScope), ProductInfo.DrawingScopeTr, StringComparison.OrdinalIgnoreCase)).
            OrderBy(Function(product) product.TrCode, StringComparer.CurrentCultureIgnoreCase).
            ThenBy(Function(product) product.DrawingRev, StringComparer.CurrentCultureIgnoreCase).
            ToList()
        cboMeasurementProduct.DataSource = Nothing
        cboMeasurementProduct.DisplayMember = NameOf(ProductInfo.DisplayName)
        cboMeasurementProduct.DataSource = measurementProducts

        Dim productCode = GetRequestValue("ProductCode")
        Dim revision = GetRequestValue("DrawingRev")
        Dim match = measurementProducts.FirstOrDefault(
            Function(product) (selectedKey.Length > 0 AndAlso String.Equals(ProductKey(product), selectedKey, StringComparison.OrdinalIgnoreCase)) OrElse
                              (String.Equals(product.TrCode, productCode, StringComparison.OrdinalIgnoreCase) AndAlso
                               (revision.Length = 0 OrElse String.Equals(product.DrawingRev, revision, StringComparison.OrdinalIgnoreCase))))
        If match IsNot Nothing Then cboMeasurementProduct.SelectedItem = match
        RefreshDrawingMeasurementStatus()
    End Sub

    Private Shared Function ProductKey(product As ProductInfo) As String
        If product Is Nothing Then Return ""
        Return product.TrCode.Trim() & "|" & product.DrawingRev.Trim() & "|" & ProductInfo.NormalizeDrawingScope(product.DrawingScope)
    End Function

    Private Sub ApplySelectedMeasurementProduct()
        Dim product = TryCast(cboMeasurementProduct.SelectedItem, ProductInfo)
        If product Is Nothing Then
            MessageBox.Show("Önce ürün / TR Resmi seçin.", "TR Resmi", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        SetControlValue(requestControls("ProductName"), product.ProductName)
        SetControlValue(requestControls("ProductCode"), product.TrCode)
        SetControlValue(requestControls("DrawingNo"), product.TrCode)
        SetControlValue(requestControls("DrawingRev"), product.DrawingRev)
        CopyProductValueIfPresent("MoldCode", product.MoldCode)
        CopyProductValueIfPresent("CavityCount", product.MoldCavityCount)
        CopyProductValueIfPresent("RawMaterial", product.Material)
        CopyProductValueIfPresent("Color", product.ColorName)
        RefreshDrawingMeasurementStatus()
    End Sub

    Private Sub CopyProductValueIfPresent(key As String, value As String)
        If String.IsNullOrWhiteSpace(value) OrElse Not requestControls.ContainsKey(key) Then Return
        SetControlValue(requestControls(key), value.Trim())
    End Sub

    Private Sub RefreshDrawingMeasurementStatus()
        Dim product = TryCast(cboMeasurementProduct.SelectedItem, ProductInfo)
        If product Is Nothing Then
            SetStatus(lblDrawingStatus, "TR Resmi: ürün seçilmedi", False)
            SetStatus(lblControlPointStatus, "Kontrol ölçüsü: ürün seçilmedi", False)
            SetStatus(lblMeasurementStatus, "Bağlı ölçüm: 0", True)
            Return
        End If

        Dim hasDrawing = Not String.IsNullOrWhiteSpace(product.DrawingFile) AndAlso File.Exists(product.DrawingFile)
        Dim pointCount = DataService.GetControlPoints(product.TrCode, product.DrawingRev, True, ProductInfo.NormalizeDrawingScope(product.DrawingScope)).Count
        SetStatus(lblDrawingStatus, If(hasDrawing, "TR Resmi: hazır", "TR Resmi: eksik"), hasDrawing)
        SetStatus(lblControlPointStatus, "Kontrol ölçüsü: " & pointCount.ToString(CultureInfo.CurrentCulture), pointCount > 0)

        Dim linkedCount = 0
        If commissioningId.Length > 0 Then
            linkedCount = CsvUtil.ReadRows(AppPaths.MeasurementsCsv).
                Where(Function(row) String.Equals(DataService.GetValue(row, "CommissioningId"), commissioningId, StringComparison.OrdinalIgnoreCase)).
                Count()
        End If
        SetStatus(lblMeasurementStatus, "Bağlı ölçü: " & linkedCount.ToString(CultureInfo.CurrentCulture), True)
    End Sub

    Private Shared Sub SetStatus(label As Label, text As String, positive As Boolean)
        label.Text = text
        label.ForeColor = If(positive, Color.FromArgb(24, 112, 66), Color.FromArgb(177, 47, 47))
        label.BackColor = If(positive, Color.FromArgb(235, 248, 240), Color.FromArgb(255, 239, 239))
    End Sub

    Private Sub StartPartMeasurement(sender As Object, e As EventArgs)
        If Not canEdit Then Return
        Dim product = TryCast(cboMeasurementProduct.SelectedItem, ProductInfo)
        If product Is Nothing Then
            MessageBox.Show("Önce ürün / TR Resmi seçin.", "Parça Ölçümü", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        If String.IsNullOrWhiteSpace(product.DrawingFile) OrElse Not File.Exists(product.DrawingFile) Then
            MessageBox.Show("Seçilen ürünün TR Resmi tanımlı değil veya dosyaya ulaşılamıyor.", "Parça Ölçümü", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        Dim pointCount = DataService.GetControlPoints(product.TrCode, product.DrawingRev, True, ProductInfo.NormalizeDrawingScope(product.DrawingScope)).Count
        If pointCount = 0 Then
            MessageBox.Show("Seçilen TR Resmi için aktif kontrol ölçüsü tanımlı değil.", "Parça Ölçümü", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        ApplySelectedMeasurementProduct()
        If commissioningId.Length = 0 AndAlso Not PersistRecord(False) Then Return

        Using form As New FrmMeasurementEntry(product.TrCode, product.DrawingRev, "", commissioningId, ProductInfo.DrawingScopeTr)
            form.ShowDialog(Me)
        End Using
        RefreshLinkedMeasurements()
    End Sub

    Private Sub RefreshLinkedMeasurements()
        linkedMeasurementsGrid.Rows.Clear()
        If commissioningId.Length = 0 Then
            RefreshDrawingMeasurementStatus()
            Return
        End If

        Dim rows = CsvUtil.ReadRows(AppPaths.MeasurementsCsv).
            Where(Function(row) String.Equals(DataService.GetValue(row, "CommissioningId"), commissioningId, StringComparison.OrdinalIgnoreCase)).
            GroupBy(Function(row) DataService.GetValue(row, "RecordId"), StringComparer.OrdinalIgnoreCase).
            Select(Function(group) New With {
                .RecordId = group.Key,
                .Rows = group.ToList(),
                .First = group.OrderByDescending(Function(row) DataService.GetValue(row, "MeasurementDate")).First()
            }).
            OrderByDescending(Function(item) DataService.GetValue(item.First, "MeasurementDate")).
            ToList()

        For Each item In rows
            Dim first = item.First
            Dim index = linkedMeasurementsGrid.Rows.Add(
                DataService.GetValue(first, "MeasurementDate"),
                item.RecordId,
                DataService.GetValue(first, "TrCode") & " / " & DataService.GetValue(first, "DrawingRev"),
                DataService.GetValue(first, "EyeNo") & " / " & DataService.GetValue(first, "EyeCount"),
                item.Rows.Count.ToString(CultureInfo.CurrentCulture),
                item.Rows.Where(Function(row) String.Equals(DataService.GetValue(row, "Result"), "OK", StringComparison.OrdinalIgnoreCase)).Count().ToString(CultureInfo.CurrentCulture),
                item.Rows.Where(Function(row) String.Equals(DataService.GetValue(row, "Result"), "NOK", StringComparison.OrdinalIgnoreCase)).Count().ToString(CultureInfo.CurrentCulture),
                item.Rows.Where(Function(row) String.Equals(DataService.GetValue(row, "Result"), "HATALI", StringComparison.OrdinalIgnoreCase)).Count().ToString(CultureInfo.CurrentCulture),
                DataService.GetValue(first, "OperatorName"))
            linkedMeasurementsGrid.Rows(index).Tag = Tuple.Create(item.RecordId, DataService.GetValue(first, "MeasureId"))
        Next
        RefreshDrawingMeasurementStatus()
    End Sub

    Private Sub OpenLinkedMeasurement(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 OrElse Not AppState.CanViewMeasurementHistory Then Return
        Dim link = TryCast(linkedMeasurementsGrid.Rows(e.RowIndex).Tag, Tuple(Of String, String))
        If link Is Nothing OrElse link.Item1.Length = 0 Then Return
        Using form As New FrmMeasurementReview(link.Item1, link.Item2)
            form.ShowDialog(Me)
        End Using
        RefreshLinkedMeasurements()
    End Sub

    Private Sub LoadRecord()
        EnsureCommissioningFiles()
        If commissioningId.Length > 0 Then
            currentRow = CsvUtil.ReadRows(AppPaths.NewMoldCommissioningsCsv).
                FirstOrDefault(Function(row) String.Equals(DataService.GetValue(row, "CommissioningId"), commissioningId, StringComparison.OrdinalIgnoreCase))
        End If
        If currentRow Is Nothing Then
            currentRow = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        End If

        For Each pair In requestControls
            SetControlValue(pair.Value, DataService.GetValue(currentRow, pair.Key))
        Next
        loadedMechanical = DataService.GetValue(currentRow, "MechanicalApproval")
        loadedProduct = DataService.GetValue(currentRow, "ProductApproval")
        loadedProcess = DataService.GetValue(currentRow, "ProcessApproval")
        cboMechanical.Text = loadedMechanical
        cboProduct.Text = loadedProduct
        cboProcess.Text = loadedProcess
        cboFinalDecision.Text = DataService.GetValue(currentRow, "FinalDecision")
        txtFinalNote.Text = DataService.GetValue(currentRow, "FinalDecisionNote")
        SetNullableDate(dtpConditionalUntil, DataService.GetValue(currentRow, "ConditionalUntil"))
        txtConditionalQuantity.Text = DataService.GetValue(currentRow, "ConditionalQuantity")
        SetNullableDate(dtpNextTrial, DataService.GetValue(currentRow, "NextTrialDate"))
        lblMechanicalMeta.Text = FormatApprovalMeta("Mechanical")
        lblProductMeta.Text = FormatApprovalMeta("Product")
        lblProcessMeta.Text = FormatApprovalMeta("Process")
        LoadMeasurementProducts()
        LoadChecklist()
        LoadTrials()
        LoadActions()
        RefreshLinkedMeasurements()
        RefreshHeader()
    End Sub

    Private Shared Sub EnsureCommissioningFiles()
        CsvUtil.EnsureFile(AppPaths.NewMoldCommissioningsCsv, DataService.NewMoldCommissioningHeaders)
        CsvUtil.EnsureFile(AppPaths.NewMoldCommissioningChecklistCsv, DataService.NewMoldCommissioningChecklistHeaders)
        CsvUtil.EnsureFile(AppPaths.NewMoldCommissioningTrialsCsv, DataService.NewMoldCommissioningTrialHeaders)
        CsvUtil.EnsureFile(AppPaths.NewMoldCommissioningActionsCsv, DataService.NewMoldCommissioningActionHeaders)
    End Sub

    Private Sub LoadChecklist()
        If commissioningId.Length = 0 Then Return

        Dim rows = CsvUtil.ReadRows(AppPaths.NewMoldCommissioningChecklistCsv).
            Where(Function(row) String.Equals(DataService.GetValue(row, "CommissioningId"), commissioningId, StringComparison.OrdinalIgnoreCase)).
            GroupBy(Function(row) DataService.GetValue(row, "ItemNo"), StringComparer.OrdinalIgnoreCase).
            Where(Function(group) group.Key.Length > 0).
            ToDictionary(Function(group) group.Key, Function(group) group.Last(), StringComparer.OrdinalIgnoreCase)
        For Each gridRow As DataGridViewRow In checklistGrid.Rows
            Dim key = Convert.ToString(gridRow.Cells("ItemNo").Value, CultureInfo.InvariantCulture)
            If rows.ContainsKey(key) Then
                Dim item = rows(key)
                gridRow.Cells("Result").Value = DataService.GetValue(item, "Result")
                gridRow.Cells("Explanation").Value = DataService.GetValue(item, "Explanation")
                gridRow.Cells("CheckedBy").Value = DataService.GetValue(item, "CheckedBy")
                gridRow.Cells("CheckedAt").Value = DataService.GetValue(item, "CheckedAt")
            End If
        Next
    End Sub

    Private Sub LoadTrials()
        trialsGrid.Rows.Clear()
        If commissioningId.Length = 0 Then Return

        For Each item In CsvUtil.ReadRows(AppPaths.NewMoldCommissioningTrialsCsv).
            Where(Function(row) String.Equals(DataService.GetValue(row, "CommissioningId"), commissioningId, StringComparison.OrdinalIgnoreCase)).
            OrderBy(Function(row) DataService.GetValue(row, "TrialNo"))
            Dim index = trialsGrid.Rows.Add()
            For Each column As DataGridViewColumn In trialsGrid.Columns
                trialsGrid.Rows(index).Cells(column.Name).Value = DataService.GetValue(item, column.Name)
            Next
            trialsGrid.Rows(index).Tag = DataService.GetValue(item, "TrialId")
        Next
    End Sub

    Private Sub LoadActions()
        actionsGrid.Rows.Clear()
        If commissioningId.Length = 0 Then Return

        For Each item In CsvUtil.ReadRows(AppPaths.NewMoldCommissioningActionsCsv).
            Where(Function(row) String.Equals(DataService.GetValue(row, "CommissioningId"), commissioningId, StringComparison.OrdinalIgnoreCase)).
            OrderBy(Function(row) DataService.GetValue(row, "DueDate"))
            Dim index = actionsGrid.Rows.Add()
            For Each column As DataGridViewColumn In actionsGrid.Columns
                actionsGrid.Rows(index).Cells(column.Name).Value = DataService.GetValue(item, column.Name)
            Next
            actionsGrid.Rows(index).Tag = DataService.GetValue(item, "ActionId")
        Next
    End Sub

    Private Sub SaveRecord(sender As Object, e As EventArgs)
        If Not PersistRecord(True) Then Return
        DialogResult = DialogResult.OK
        Close()
    End Sub

    Private Function PersistRecord(showConfirmation As Boolean) As Boolean
        If Not canEdit Then Return False
        Dim productCode = GetRequestValue("ProductCode")
        Dim moldCode = GetRequestValue("MoldCode")
        If productCode.Length = 0 OrElse moldCode.Length = 0 Then
            MessageBox.Show("Ürün / TR Kodu ve Kalıp Kodu zorunludur.", "Eksik Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            tabs.SelectedIndex = 0
            Return False
        End If

        Try
            Dim id = commissioningId
            If id.Length = 0 Then id = "KDA-" & DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) & "-" & Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant()
            Dim nowText = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.CurrentCulture)
            Dim row = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            For Each header In DataService.NewMoldCommissioningHeaders
                row(header) = DataService.GetValue(currentRow, header)
            Next
            row("CommissioningId") = id
            row("CreatedAt") = If(DataService.GetValue(currentRow, "CreatedAt").Length = 0, nowText, DataService.GetValue(currentRow, "CreatedAt"))
            row("CreatedBy") = If(DataService.GetValue(currentRow, "CreatedBy").Length = 0, AppState.CurrentUserName, DataService.GetValue(currentRow, "CreatedBy"))
            row("UpdatedAt") = nowText
            row("UpdatedBy") = AppState.CurrentUserName
            For Each pair In requestControls
                row(pair.Key) = GetControlValue(pair.Value)
            Next
            ApplyApproval(row, "Mechanical", cboMechanical.Text, loadedMechanical, nowText)
            ApplyApproval(row, "Product", cboProduct.Text, loadedProduct, nowText)
            ApplyApproval(row, "Process", cboProcess.Text, loadedProcess, nowText)
            row("FinalDecision") = cboFinalDecision.Text.Trim()
            row("FinalDecisionNote") = txtFinalNote.Text.Trim()
            row("ConditionalUntil") = GetNullableDate(dtpConditionalUntil)
            row("ConditionalQuantity") = txtConditionalQuantity.Text.Trim()
            row("NextTrialDate") = GetNullableDate(dtpNextTrial)

            Dim stage = DetermineStage(row)
            row("CurrentStage") = stage
            row("Status") = If(AllApprovalsComplete(row), "TAMAMLANDI", "AKTİF")
            CsvUtil.UpdateRowsLocked(AppPaths.NewMoldCommissioningsCsv, DataService.NewMoldCommissioningHeaders,
                Sub(rows)
                    Dim index = rows.FindIndex(Function(existing) String.Equals(DataService.GetValue(existing, "CommissioningId"), id, StringComparison.OrdinalIgnoreCase))
                    If index >= 0 Then
                        rows(index) = row
                    Else
                        rows.Add(row)
                    End If
                End Sub)
            PersistChecklist(id, nowText)
            PersistTrials(id, nowText)
            PersistActions(id, nowText)
            AuditService.Log("KALIP DEVREYE ALMA KAYDI", productCode, GetRequestValue("DrawingRev"), id & " | " & stage & " | " & row("Status"))
            commissioningId = id
            currentRow = row
            loadedMechanical = DataService.GetValue(row, "MechanicalApproval")
            loadedProduct = DataService.GetValue(row, "ProductApproval")
            loadedProcess = DataService.GetValue(row, "ProcessApproval")
            lblMechanicalMeta.Text = FormatApprovalMeta("Mechanical")
            lblProductMeta.Text = FormatApprovalMeta("Product")
            lblProcessMeta.Text = FormatApprovalMeta("Process")
            RefreshHeader()
            RefreshLinkedMeasurements()
            If showConfirmation Then
                MessageBox.Show("Kalıp devreye alma kaydı güncellendi.", "Kayıt", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
            Return True
        Catch ex As Exception
            ErrorLogService.Log("FrmNewMoldCommissioningDetail.PersistRecord", ex)
            MessageBox.Show("Kayıt yapılamadı: " & ex.Message, "Kayıt Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return False
        End Try
    End Function

    Private Sub PersistChecklist(id As String, nowText As String)
        Dim newRows As New List(Of Dictionary(Of String, String))()
        For Each gridRow As DataGridViewRow In checklistGrid.Rows
            If gridRow.IsNewRow Then Continue For
            Dim result = CellText(gridRow, "Result")
            Dim explanation = CellText(gridRow, "Explanation")
            If result.Length = 0 AndAlso explanation.Length = 0 Then Continue For
            newRows.Add(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                {"ChecklistId", id & "-" & CellText(gridRow, "ItemNo").PadLeft(2, "0"c)},
                {"CommissioningId", id}, {"ItemNo", CellText(gridRow, "ItemNo")}, {"Category", "Kalıphane Ön Kabul"},
                {"ItemText", CellText(gridRow, "ItemText")}, {"Result", result}, {"Explanation", explanation},
                {"CheckedBy", If(CellText(gridRow, "CheckedBy").Length = 0, AppState.CurrentUserName, CellText(gridRow, "CheckedBy"))},
                {"CheckedAt", If(CellText(gridRow, "CheckedAt").Length = 0, nowText, CellText(gridRow, "CheckedAt"))}
            })
        Next
        ReplaceChildren(AppPaths.NewMoldCommissioningChecklistCsv, DataService.NewMoldCommissioningChecklistHeaders, id, newRows)
    End Sub

    Private Sub PersistTrials(id As String, nowText As String)
        Dim newRows As New List(Of Dictionary(Of String, String))()
        For Each gridRow As DataGridViewRow In trialsGrid.Rows
            If gridRow.IsNewRow OrElse Not RowHasData(gridRow, {"TrialNo", "MachineNo", "Nonconformity"}) Then Continue For
            Dim row = GridRowToDictionary(gridRow, DataService.NewMoldCommissioningTrialHeaders)
            row("TrialId") = If(gridRow.Tag Is Nothing OrElse gridRow.Tag.ToString().Length = 0, Guid.NewGuid().ToString("N"), gridRow.Tag.ToString())
            row("CommissioningId") = id
            row("CreatedBy") = If(row("CreatedBy").Length = 0, AppState.CurrentUserName, row("CreatedBy"))
            row("CreatedAt") = If(row("CreatedAt").Length = 0, nowText, row("CreatedAt"))
            row("UpdatedBy") = AppState.CurrentUserName
            row("UpdatedAt") = nowText
            newRows.Add(row)
        Next
        ReplaceChildren(AppPaths.NewMoldCommissioningTrialsCsv, DataService.NewMoldCommissioningTrialHeaders, id, newRows)
    End Sub

    Private Sub PersistActions(id As String, nowText As String)
        Dim newRows As New List(Of Dictionary(Of String, String))()
        For Each gridRow As DataGridViewRow In actionsGrid.Rows
            If gridRow.IsNewRow OrElse Not RowHasData(gridRow, {"Description", "Action", "ResponsibleDepartment"}) Then Continue For
            Dim row = GridRowToDictionary(gridRow, DataService.NewMoldCommissioningActionHeaders)
            row("ActionId") = If(gridRow.Tag Is Nothing OrElse gridRow.Tag.ToString().Length = 0, Guid.NewGuid().ToString("N"), gridRow.Tag.ToString())
            row("CommissioningId") = id
            row("CreatedBy") = If(row("CreatedBy").Length = 0, AppState.CurrentUserName, row("CreatedBy"))
            row("CreatedAt") = If(row("CreatedAt").Length = 0, nowText, row("CreatedAt"))
            row("UpdatedBy") = AppState.CurrentUserName
            row("UpdatedAt") = nowText
            newRows.Add(row)
        Next
        ReplaceChildren(AppPaths.NewMoldCommissioningActionsCsv, DataService.NewMoldCommissioningActionHeaders, id, newRows)
    End Sub

    Private Shared Sub ReplaceChildren(path As String, headers As String(), id As String, replacement As List(Of Dictionary(Of String, String)))
        CsvUtil.UpdateRowsLocked(path, headers,
            Sub(rows)
                rows.RemoveAll(Function(item) String.Equals(DataService.GetValue(item, "CommissioningId"), id, StringComparison.OrdinalIgnoreCase))
                rows.AddRange(replacement)
            End Sub)
    End Sub

    Private Sub DeleteRecord(sender As Object, e As EventArgs)
        If commissioningId.Length = 0 OrElse Not AppState.CanDeleteNewMoldCommissioning Then Return
        If MessageBox.Show("Bu devreye alma kaydı; ön kabul, deneme ve aksiyon geçmişiyle birlikte silinecek. Devam edilsin mi?", "Kaydı Sil", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) <> DialogResult.Yes Then Return
        Try
            CsvUtil.DeleteRowsLocked(AppPaths.NewMoldCommissioningsCsv, DataService.NewMoldCommissioningHeaders, Function(row) String.Equals(DataService.GetValue(row, "CommissioningId"), commissioningId, StringComparison.OrdinalIgnoreCase))
            CsvUtil.DeleteRowsLocked(AppPaths.NewMoldCommissioningChecklistCsv, DataService.NewMoldCommissioningChecklistHeaders, Function(row) String.Equals(DataService.GetValue(row, "CommissioningId"), commissioningId, StringComparison.OrdinalIgnoreCase))
            CsvUtil.DeleteRowsLocked(AppPaths.NewMoldCommissioningTrialsCsv, DataService.NewMoldCommissioningTrialHeaders, Function(row) String.Equals(DataService.GetValue(row, "CommissioningId"), commissioningId, StringComparison.OrdinalIgnoreCase))
            CsvUtil.DeleteRowsLocked(AppPaths.NewMoldCommissioningActionsCsv, DataService.NewMoldCommissioningActionHeaders, Function(row) String.Equals(DataService.GetValue(row, "CommissioningId"), commissioningId, StringComparison.OrdinalIgnoreCase))
            AuditService.Log("KALIP DEVREYE ALMA SİLİNDİ", GetRequestValue("ProductCode"), GetRequestValue("DrawingRev"), commissioningId)
            DialogResult = DialogResult.OK
            Close()
        Catch ex As Exception
            ErrorLogService.Log("FrmNewMoldCommissioningDetail.DeleteRecord", ex)
            MessageBox.Show("Kayıt silinemedi: " & ex.Message, "Silme Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ApplyAccess()
        If canEdit Then Return
        SetReadOnlyRecursive(tabs)
        checklistGrid.ReadOnly = True
        trialsGrid.ReadOnly = True
        actionsGrid.ReadOnly = True
    End Sub

    Private Shared Sub SetReadOnlyRecursive(parent As Control)
        For Each child As Control In parent.Controls
            If TypeOf child Is TextBoxBase Then DirectCast(child, TextBoxBase).ReadOnly = True
            If TypeOf child Is ComboBox OrElse TypeOf child Is DateTimePicker OrElse TypeOf child Is CheckBox Then child.Enabled = False
            If child.HasChildren Then SetReadOnlyRecursive(child)
        Next
    End Sub

    Private Sub ChecklistCellChanged(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 OrElse e.ColumnIndex < 0 OrElse Not canEdit Then Return
        If checklistGrid.Columns(e.ColumnIndex).Name <> "Result" AndAlso checklistGrid.Columns(e.ColumnIndex).Name <> "Explanation" Then Return
        checklistGrid.Rows(e.RowIndex).Cells("CheckedBy").Value = AppState.CurrentUserName
        checklistGrid.Rows(e.RowIndex).Cells("CheckedAt").Value = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")
    End Sub

    Private Function DetermineStage(row As Dictionary(Of String, String)) As String
        If AllApprovalsComplete(row) Then Return "Nihai Onay"
        If cboMechanical.Text = "ONAYLANDI" OrElse cboProduct.Text.Length > 0 OrElse cboProcess.Text.Length > 0 Then Return "Ölçüm / Doğrulama"
        If actionsGrid.Rows.Cast(Of DataGridViewRow)().Any(Function(r) Not r.IsNewRow AndAlso RowHasData(r, {"Description", "Action"})) Then Return "Düzeltmeler"
        If trialsGrid.Rows.Cast(Of DataGridViewRow)().Any(Function(r) Not r.IsNewRow AndAlso RowHasData(r, {"TrialNo", "MachineNo"})) Then Return "Denemeler"
        If checklistGrid.Rows.Cast(Of DataGridViewRow)().Any(Function(r) CellText(r, "Result").Length > 0) Then Return "Kalıphane Ön Kabul"
        Return "Talep"
    End Function

    Private Shared Function AllApprovalsComplete(row As Dictionary(Of String, String)) As Boolean
        Return String.Equals(DataService.GetValue(row, "MechanicalApproval"), "ONAYLANDI", StringComparison.OrdinalIgnoreCase) AndAlso
               String.Equals(DataService.GetValue(row, "ProductApproval"), "ONAYLANDI", StringComparison.OrdinalIgnoreCase) AndAlso
               String.Equals(DataService.GetValue(row, "ProcessApproval"), "ONAYLANDI", StringComparison.OrdinalIgnoreCase) AndAlso
               String.Equals(DataService.GetValue(row, "FinalDecision"), "ONAYLANDI", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Sub ApplyApproval(row As Dictionary(Of String, String), prefix As String, value As String, oldValue As String, nowText As String)
        row(prefix & "Approval") = value.Trim()
        If Not String.Equals(value.Trim(), oldValue, StringComparison.OrdinalIgnoreCase) Then
            row(prefix & "ApprovedBy") = AppState.CurrentUserName
            row(prefix & "ApprovedAt") = nowText
        End If
    End Sub

    Private Sub RefreshHeader()
        lblRecord.Text = If(commissioningId.Length = 0, "YENİ KAYIT", commissioningId)
        lblStage.Text = If(commissioningId.Length = 0, If(canEdit, "DÜZENLEME", "SALT OKUNUR"), DataService.GetValue(currentRow, "CurrentStage") & "  |  " & DataService.GetValue(currentRow, "Status"))
    End Sub

    Private Function FormatApprovalMeta(prefix As String) As String
        Dim who = DataService.GetValue(currentRow, prefix & "ApprovedBy")
        Dim whenText = DataService.GetValue(currentRow, prefix & "ApprovedAt")
        If who.Length = 0 Then Return "Henüz işlem yapılmadı."
        Return who & "  |  " & whenText
    End Function

    Private Sub AddRequestField(table As TableLayoutPanel, row As Integer, labelColumn As Integer, label As String, key As String)
        Dim input As New TextBox() With {.Dock = DockStyle.Fill}
        requestControls(key) = input
        table.Controls.Add(MakeFieldLabel(label), labelColumn, row)
        table.Controls.Add(input, labelColumn + 1, row)
    End Sub

    Private Sub AddRequestDateField(table As TableLayoutPanel, row As Integer, labelColumn As Integer, label As String, key As String)
        Dim input As New DateTimePicker()
        ConfigureNullableDate(input)
        requestControls(key) = input
        table.Controls.Add(MakeFieldLabel(label), labelColumn, row)
        table.Controls.Add(input, labelColumn + 1, row)
    End Sub

    Private Sub AddRequestMultiline(table As TableLayoutPanel, row As Integer, label As String, key As String)
        Dim input As New TextBox() With {.Dock = DockStyle.Fill, .Multiline = True, .Height = 58, .ScrollBars = ScrollBars.Vertical}
        requestControls(key) = input
        table.Controls.Add(MakeFieldLabel(label), 0, row)
        table.Controls.Add(input, 1, row)
        table.SetColumnSpan(input, 3)
    End Sub

    Private Shared Function MakeFieldLabel(text As String) As Label
        Return New Label() With {.Text = text, .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Font = New Font("Segoe UI", 8.5F, FontStyle.Bold), .Padding = New Padding(0, 3, 0, 0)}
    End Function

    Private Shared Sub ConfigureGrid(target As DataGridView)
        target.Dock = DockStyle.Fill
        target.AllowUserToAddRows = True
        target.AllowUserToDeleteRows = True
        target.AllowUserToResizeRows = False
        target.MultiSelect = False
        target.SelectionMode = DataGridViewSelectionMode.CellSelect
        target.AutoGenerateColumns = False
        target.BackgroundColor = Color.White
        target.BorderStyle = BorderStyle.FixedSingle
        target.RowHeadersVisible = False
        target.EnableHeadersVisualStyles = False
        target.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(218, 230, 244)
        target.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(15, 50, 92)
        target.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 8.2F, FontStyle.Bold)
        target.ColumnHeadersHeight = 44
        target.RowTemplate.Height = 31
    End Sub

    Private Shared Sub AddGridText(target As DataGridView, name As String, header As String, width As Integer, Optional mode As DataGridViewAutoSizeColumnMode = DataGridViewAutoSizeColumnMode.None)
        target.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = name, .HeaderText = header, .Width = width, .AutoSizeMode = mode})
    End Sub

    Private Shared Sub AddGridCombo(target As DataGridView, name As String, header As String, width As Integer, values As String())
        target.Columns.Add(New DataGridViewComboBoxColumn() With {.Name = name, .HeaderText = header, .Width = width, .DataSource = values})
    End Sub

    Private Shared Sub SetupApprovalCombo(combo As ComboBox, Optional values As String() = Nothing)
        combo.Dock = DockStyle.Fill
        combo.DropDownStyle = ComboBoxStyle.DropDownList
        combo.DataSource = If(values, New String() {"", "BEKLİYOR", "ONAYLANDI", "UYGUN DEĞİL"})
    End Sub

    Private Shared Sub AddApprovalRow(table As TableLayoutPanel, row As Integer, label As String, combo As ComboBox, meta As Label, responsible As String)
        table.Controls.Add(MakeFieldLabel(label), 0, row)
        table.Controls.Add(combo, 1, row)
        meta.Dock = DockStyle.Fill
        meta.TextAlign = ContentAlignment.MiddleLeft
        meta.ForeColor = Color.FromArgb(75, 85, 98)
        meta.Text = responsible
        table.Controls.Add(meta, 2, row)
    End Sub

    Private Shared Sub ConfigureNullableDate(picker As DateTimePicker)
        picker.Dock = DockStyle.Fill
        picker.Format = DateTimePickerFormat.Custom
        picker.CustomFormat = "dd.MM.yyyy"
        picker.ShowCheckBox = True
        picker.Checked = False
    End Sub

    Private Shared Sub SetNullableDate(picker As DateTimePicker, value As String)
        Dim parsed As DateTime
        picker.Checked = DateTime.TryParse(value, parsed)
        If picker.Checked Then picker.Value = parsed
    End Sub

    Private Shared Function GetNullableDate(picker As DateTimePicker) As String
        Return If(picker.Checked, picker.Value.ToString("dd.MM.yyyy"), "")
    End Function

    Private Shared Function MakeButton(text As String, backColor As Color, foreColor As Color, width As Integer) As Button
        Return New Button() With {.Text = text, .Width = width, .Height = 34, .BackColor = backColor, .ForeColor = foreColor, .FlatStyle = FlatStyle.Flat, .Font = New Font("Segoe UI", 8.5F, FontStyle.Bold), .Margin = New Padding(6, 0, 0, 0)}
    End Function

    Private Function GetRequestValue(key As String) As String
        Dim control As Control = Nothing
        If Not requestControls.TryGetValue(key, control) Then Return ""
        Return GetControlValue(control)
    End Function

    Private Shared Function GetControlValue(control As Control) As String
        If TypeOf control Is DateTimePicker Then Return GetNullableDate(DirectCast(control, DateTimePicker))
        Return control.Text.Trim()
    End Function

    Private Shared Sub SetControlValue(control As Control, value As String)
        If TypeOf control Is DateTimePicker Then
            SetNullableDate(DirectCast(control, DateTimePicker), value)
        Else
            control.Text = value
        End If
    End Sub

    Private Shared Function CellText(row As DataGridViewRow, columnName As String) As String
        If row Is Nothing OrElse Not row.DataGridView.Columns.Contains(columnName) Then Return ""
        Return Convert.ToString(row.Cells(columnName).Value, CultureInfo.CurrentCulture).Trim()
    End Function

    Private Shared Function RowHasData(row As DataGridViewRow, keys As String()) As Boolean
        Return keys.Any(Function(key) CellText(row, key).Length > 0)
    End Function

    Private Shared Function GridRowToDictionary(gridRow As DataGridViewRow, headers As String()) As Dictionary(Of String, String)
        Dim result = headers.ToDictionary(Function(header) header, Function(header) "", StringComparer.OrdinalIgnoreCase)
        For Each column As DataGridViewColumn In gridRow.DataGridView.Columns
            If result.ContainsKey(column.Name) Then result(column.Name) = CellText(gridRow, column.Name)
        Next
        Return result
    End Function
End Class
