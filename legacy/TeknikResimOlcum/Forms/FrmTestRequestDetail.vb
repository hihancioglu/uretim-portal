Imports System.Drawing
Imports System.Diagnostics
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmTestRequestDetail
    Inherits Form

    Private Shared ReadOnly RequestingDepartments As String() = {"GKK", "MEKANİZMA", "PLASTİKHANE", "KARTLI SAYAÇ", "ELEKTRİK SAYACI", "DİĞER"}
    Private Shared ReadOnly RequestedDepartments As String() = {"KALİTE LAB.", "SAYAÇ MONTAJ", "MEKANİZMA", "PLASTİKHANE", "KARTLI SAYAÇ", "ELEKTRİK SAYACI", "TALAŞLI İMALAT", "DİĞER"}
    Private Shared ReadOnly ReasonOptions As String() = {"İLK (MUADİL) ÜRÜN", "ARA ÜRÜN", "REVİZYON", "ŞÜPHELİ ÜRÜN", "KALIP TADİLAT", "YERİNDE DENEME", "YURTDIŞI YERİNDE DENEME", "YURTİÇİ YERİNDE DENEME", "DİĞER"}

    Private ReadOnly sourceRow As Dictionary(Of String, String)
    Private ReadOnly isNew As Boolean
    Private ReadOnly pnlRequestingDepartments As New TableLayoutPanel()
    Private ReadOnly txtRequestingDepartments As New TextBox()
    Private ReadOnly btnSelectRequestingDepartments As New Button()
    Private ReadOnly selectedRequestingDepartments As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly cboRequestedDepartment As New ComboBox()
    Private ReadOnly pnlReasons As New TableLayoutPanel()
    Private ReadOnly reasonChecks As New List(Of CheckBox)()
    Private ReadOnly txtProduct As New TextBox()
    Private ReadOnly pnlRequestedTests As New TableLayoutPanel()
    Private ReadOnly clbRequestedTests As New CheckedListBox()
    Private ReadOnly txtTestFilter As New TextBox()
    Private ReadOnly lblSelectedTests As New Label()
    Private ReadOnly btnClearSelectedTests As New Button()
    Private ReadOnly allRequestedTestNames As New List(Of String)()
    Private ReadOnly selectedRequestedTestNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly txtRequestedTests As New TextBox()
    Private ReadOnly btnSelectRequestedTests As New Button()
    Private ReadOnly txtSampleQuantity As New TextBox()
    Private ReadOnly cboPriority As New ComboBox()
    Private ReadOnly dtpDueDate As New DateTimePicker()
    Private ReadOnly txtRequesterReportNo As New TextBox()
    Private ReadOnly txtRequesterExplanation As New TextBox()
    Private ReadOnly txtLabReportNo As New TextBox()
    Private ReadOnly cboResult As New ComboBox()
    Private ReadOnly txtLabExplanation As New TextBox()
    Private ReadOnly attachmentList As New ListBox()
    Private ReadOnly lblAttachmentCount As New Label()
    Private ReadOnly btnAddAttachment As New Button()
    Private ReadOnly btnOpenAttachment As New Button()
    Private ReadOnly btnDeleteAttachment As New Button()
    Private ReadOnly lblStatus As New Label()
    Private ReadOnly lblRequestAudit As New Label()
    Private ReadOnly lblLabAudit As New Label()
    Private ReadOnly lblResultInfo As New Label()
    Private ReadOnly btnCreate As New Button()
    Private ReadOnly btnAdminSave As New Button()
    Private ReadOnly btnAccept As New Button()
    Private ReadOnly btnComplete As New Button()
    Private ReadOnly btnCancelRequest As New Button()
    Private ReadOnly testStepGrid As New DataGridView()
    Private ReadOnly lblTestStepSummary As New Label()
    Private ReadOnly btnCompleteStep As New Button()
    Private ReadOnly btnSkipStep As New Button()
    Private ReadOnly btnReopenStep As New Button()
    Private ReadOnly testDetailTabs As New TabControl()
    Private currentTestSteps As New List(Of TestRequestStep)()
    Private recordChanged As Boolean

    Private NotInheritable Class AttachmentListItem
        Public ReadOnly Property Row As Dictionary(Of String, String)

        Public Sub New(value As Dictionary(Of String, String))
            Row = value
        End Sub

        Public Overrides Function ToString() As String
            Dim fileName = DataService.GetValue(Row, "OriginalFileName")
            Dim addedAt = DataService.GetValue(Row, "AddedAt")
            Dim addedBy = DataService.GetValue(Row, "AddedBy")
            Return fileName & If(addedAt = "", "", "  ·  " & addedAt) & If(addedBy = "", "", "  ·  " & addedBy)
        End Function
    End Class

    Public ReadOnly Property AffectedRequestId As String

    Public ReadOnly Property HasChanges As Boolean
        Get
            Return recordChanged
        End Get
    End Property

    Public Sub New(Optional row As Dictionary(Of String, String) = Nothing)
        AuthorizationService.Require(AppState.CanOpenTestRequests, "Test Talep Detayı")
        AppIconService.Apply(Me)

        sourceRow = If(row Is Nothing,
                       Nothing,
                       New Dictionary(Of String, String)(row, StringComparer.OrdinalIgnoreCase))
        isNew = sourceRow Is Nothing

        Text = If(isNew, "Yeni Test Talebi", "Test Talep Detayı")
        StartPosition = FormStartPosition.CenterScreen
        WindowState = FormWindowState.Maximized
        Size = If(isNew, New Size(980, 760), New Size(1180, 760))
        MinimumSize = New Size(880, 620)
        BackColor = Color.FromArgb(244, 247, 251)
        Font = New Font("Segoe UI", 9.0F)

        BuildScreen()
        LoadRecord()
        ApplyPermissions()
    End Sub

    Private Sub BuildScreen()
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .Padding = New Padding(10),
            .BackColor = BackColor
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        Controls.Add(root)

        Dim header As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = If(isNew, 1, 2),
            .RowCount = 1,
            .BackColor = Color.FromArgb(31, 71, 126),
            .Padding = New Padding(16, 5, 16, 5),
            .Margin = New Padding(0, 0, 0, 8)
        }
        If isNew Then
            header.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        Else
            header.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 65.0F))
            header.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 35.0F))
        End If
        header.Controls.Add(New Label() With {
            .Text = If(isNew, "Yeni Test Talebi Oluştur", "Test Talebi / Kontrol Sonucu"),
            .Dock = DockStyle.Fill,
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 12.5F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0)
        If Not isNew Then
            lblStatus.Dock = DockStyle.Fill
            lblStatus.ForeColor = Color.White
            lblStatus.Font = New Font("Segoe UI", 10.0F, FontStyle.Bold)
            lblStatus.TextAlign = ContentAlignment.MiddleCenter
            header.Controls.Add(lblStatus, 1, 0)
        End If
        root.Controls.Add(header, 0, 0)

        If isNew Then
            root.Controls.Add(BuildNewRequestContainer(), 0, 1)
        Else
            testDetailTabs.Dock = DockStyle.Fill
            testDetailTabs.Margin = New Padding(0)
            testDetailTabs.Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)

            Dim summaryPage As New TabPage("Talep ve Sonuç") With {.BackColor = BackColor, .Padding = New Padding(6)}
            Dim summaryContent As New TableLayoutPanel() With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 2,
                .RowCount = 1,
                .Margin = New Padding(0),
                .BackColor = BackColor
            }
            summaryContent.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 55.0F))
            summaryContent.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 45.0F))
            summaryContent.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            summaryContent.Controls.Add(BuildRequestPanel(), 0, 0)
            summaryContent.Controls.Add(BuildLaboratoryPanel(), 1, 0)
            summaryPage.Controls.Add(summaryContent)

            Dim executionPage As New TabPage("Test Uygulama Sırası") With {.BackColor = BackColor, .Padding = New Padding(6)}
            Dim executionPanel = BuildTestExecutionPanel()
            executionPanel.Margin = New Padding(0)
            executionPage.Controls.Add(executionPanel)

            testDetailTabs.TabPages.Add(summaryPage)
            testDetailTabs.TabPages.Add(executionPage)
            root.Controls.Add(testDetailTabs, 0, 1)
        End If

        Dim footerHost As New Panel() With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.White,
            .Margin = New Padding(0, 8, 0, 0)
        }
        Dim footer As New FlowLayoutPanel() With {
            .Dock = DockStyle.None,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False,
            .Padding = New Padding(8, 10, 0, 4),
            .BackColor = Color.White,
            .Margin = New Padding(0)
        }
        footerHost.Controls.Add(footer)
        Dim btnClose As New Button() With {.Text = "Kapat", .Width = 105, .Height = 34, .DialogResult = DialogResult.Cancel, .Margin = New Padding(8, 0, 0, 0)}
        ConfigureActionButton(btnCreate, "Talebi Oluştur", 145, Color.FromArgb(31, 71, 126), Color.White)
        ConfigureActionButton(btnAdminSave, "Değişiklikleri Kaydet", 190, Color.FromArgb(31, 71, 126), Color.White)
        ConfigureActionButton(btnAccept, "İşleme Al", 125, Color.FromArgb(255, 244, 214), Color.FromArgb(128, 88, 0))
        ConfigureActionButton(btnComplete, "Sonuçlandır", 135, Color.FromArgb(220, 245, 226), Color.DarkGreen)
        ConfigureActionButton(btnCancelRequest, "Talebi İptal Et", 135, Color.MistyRose, Color.DarkRed)
        AddHandler btnCreate.Click, AddressOf Create_Click
        AddHandler btnAdminSave.Click, AddressOf AdminSave_Click
        AddHandler btnAccept.Click, AddressOf Accept_Click
        AddHandler btnComplete.Click, AddressOf Complete_Click
        AddHandler btnCancelRequest.Click, AddressOf Cancel_Click
        footer.Controls.AddRange({btnClose, btnComplete, btnAccept, btnAdminSave, btnCreate, btnCancelRequest})

        Dim alignFooter As Action =
            Sub()
                If footerHost.IsDisposed OrElse footer.IsDisposed Then Return
                Dim targetWidth = If(isNew,
                                     Math.Min(1280, Math.Max(860, footerHost.ClientSize.Width - 40)),
                                     footerHost.ClientSize.Width)
                footer.Width = Math.Max(1, targetWidth)
                footer.Height = Math.Max(1, footerHost.ClientSize.Height)
                footer.Left = Math.Max(0, (footerHost.ClientSize.Width - footer.Width) \ 2)
                footer.Top = 0
            End Sub
        AddHandler footerHost.Resize, Sub() alignFooter()
        AddHandler footerHost.HandleCreated,
            Sub()
                If footerHost.IsDisposed Then Return
                alignFooter()
            End Sub
        root.Controls.Add(footerHost, 0, 2)
        CancelButton = btnClose
    End Sub

    Private Function BuildNewRequestContainer() As Control
        Dim container As New Panel() With {
            .Dock = DockStyle.Fill,
            .BackColor = BackColor,
            .AutoScroll = False,
            .Margin = New Padding(0)
        }

        Dim requestPanel = BuildRequestPanel()
        requestPanel.Dock = DockStyle.None
        container.Controls.Add(requestPanel)

        Dim applyLayout As Action =
            Sub()
                If container.IsDisposed OrElse requestPanel.IsDisposed Then Return

                Dim targetWidth = Math.Min(1280, Math.Max(860, container.ClientSize.Width - 40))
                Dim targetHeight = Math.Max(520, container.ClientSize.Height - 4)
                requestPanel.Width = targetWidth
                requestPanel.Height = targetHeight
                requestPanel.Left = Math.Max(0, (container.ClientSize.Width - requestPanel.Width) \ 2)
                requestPanel.Top = 0
            End Sub

        AddHandler container.Resize, Sub() applyLayout()
        AddHandler container.HandleCreated,
            Sub()
                If container.IsDisposed Then Return
                applyLayout()
            End Sub
        Return container
    End Function

    Private Function BuildRequestPanel() As Control
        Dim group As New GroupBox() With {
            .Text = "Talep Bilgileri",
            .Dock = DockStyle.Fill,
            .BackColor = Color.White,
            .Padding = New Padding(12),
            .Margin = If(isNew, New Padding(0), New Padding(0, 0, 8, 0))
        }
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 10, .Padding = New Padding(5), .BackColor = Color.White}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 160.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 128.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 52.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        group.Controls.Add(layout)

        ConfigureRequestingDepartmentsPanel()
        ConfigureCombo(cboRequestedDepartment, RequestedDepartments)
        ConfigureReasonPanel()
        ConfigureTextBox(txtProduct, "Ürün adı ve TR numarasını girin")
        ConfigureRequestedTestsSummaryPanel()
        ConfigureTextBox(txtSampleQuantity, "Örn. 5 adet / 1 numune")
        ConfigureCombo(cboPriority, {"DÜŞÜK", "NORMAL", "YÜKSEK"})
        cboPriority.SelectedItem = "NORMAL"
        dtpDueDate.Dock = DockStyle.Left
        dtpDueDate.Width = 180
        dtpDueDate.Format = DateTimePickerFormat.Custom
        dtpDueDate.CustomFormat = "dd.MM.yyyy"
        dtpDueDate.ShowCheckBox = True
        dtpDueDate.Checked = False
        ConfigureTextBox(txtRequesterReportNo, "Varsa GKK / referans rapor no")
        ConfigureMultiline(txtRequesterExplanation, "Talep eden açıklaması")

        AddField(layout, "Talep Eden Bölüm", pnlRequestingDepartments, 0)
        AddField(layout, "Talep Edilen Bölüm", cboRequestedDepartment, 1)
        AddField(layout, "Talep Nedeni", pnlReasons, 2)
        AddField(layout, "Ürün Adı / TR No", txtProduct, 3)
        AddField(layout, "Talep Edilen Test", pnlRequestedTests, 4)
        AddField(layout, "Numune / Miktar", txtSampleQuantity, 5)
        AddField(layout, "Öncelik", cboPriority, 6)
        AddField(layout, "Termin", dtpDueDate, 7)
        AddField(layout, "Rapor / Referans No", txtRequesterReportNo, 8)
        AddField(layout, "Talep Eden Açıklama", txtRequesterExplanation, 9)
        Return group
    End Function

    Private Function BuildLaboratoryPanel() As Control
        Dim group As New GroupBox() With {.Text = "Laboratuvar / Kontrol Sonucu", .Dock = DockStyle.Fill, .BackColor = Color.White, .Padding = New Padding(10), .Margin = New Padding(8, 0, 0, 0)}
        Dim layout As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 8, .Padding = New Padding(5), .BackColor = Color.White}
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 135.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 70.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 70.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 70.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 96.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 70.0F))
        group.Controls.Add(layout)

        ConfigureInfoLabel(lblRequestAudit)
        ConfigureInfoLabel(lblLabAudit)
        ConfigureInfoLabel(lblResultInfo)
        ConfigureTextBox(txtLabReportNo, "Laboratuvar rapor no")
        ConfigureCombo(cboResult, {"UYGUN", "UYGUN DEĞİL", "BİLGİ / DEĞERLENDİRME"})
        ConfigureMultiline(txtLabExplanation, "Test sonucu, ölçülen değerler ve laboratuvar açıklaması")
        Dim rule As New Label() With {
            .Text = "İşlemi yapan kullanıcı ile tarih ve saat bilgisi otomatik olarak denetim kaydına alınır.",
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(247, 250, 253),
            .ForeColor = Color.FromArgb(55, 75, 100),
            .Padding = New Padding(10),
            .TextAlign = ContentAlignment.MiddleLeft,
            .AutoEllipsis = True
        }

        AddField(layout, "Talep / Durum", lblRequestAudit, 0)
        AddField(layout, "İşleme Alan", lblLabAudit, 1)
        AddField(layout, "Sonuç Bilgisi", lblResultInfo, 2)
        AddField(layout, "Laboratuvar Rapor No", txtLabReportNo, 3)
        AddField(layout, "Sonuç", cboResult, 4)
        AddField(layout, "Sonuç Dosyası", BuildAttachmentPanel(), 5)
        AddField(layout, "Kontrol Eden Açıklama", txtLabExplanation, 6)
        layout.SetColumnSpan(rule, 2)
        layout.Controls.Add(rule, 0, 7)
        Return group
    End Function

    Private Function BuildAttachmentPanel() As Control
        Dim panel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .Margin = New Padding(0),
            .Padding = New Padding(0),
            .BackColor = Color.White
        }
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        panel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 24.0F))
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 32.0F))

        attachmentList.Dock = DockStyle.Fill
        attachmentList.Margin = New Padding(0, 0, 0, 3)
        attachmentList.IntegralHeight = False
        attachmentList.HorizontalScrollbar = False
        attachmentList.Font = New Font("Segoe UI", 8.5F)
        AddHandler attachmentList.SelectedIndexChanged, Sub() UpdateAttachmentButtons()
        AddHandler attachmentList.DoubleClick, AddressOf OpenAttachment_Click
        panel.Controls.Add(attachmentList, 0, 0)

        lblAttachmentCount.Dock = DockStyle.Fill
        lblAttachmentCount.Margin = New Padding(2, 0, 0, 0)
        lblAttachmentCount.TextAlign = ContentAlignment.MiddleLeft
        lblAttachmentCount.ForeColor = Color.FromArgb(75, 88, 105)
        lblAttachmentCount.Font = New Font("Segoe UI", 8.0F, FontStyle.Bold)
        panel.Controls.Add(lblAttachmentCount, 0, 1)

        Dim actions As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 3,
            .RowCount = 1,
            .Margin = New Padding(0),
            .Padding = New Padding(0),
            .AutoScroll = False,
            .BackColor = Color.White
        }
        actions.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 34.0F))
        actions.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.0F))
        actions.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.0F))
        actions.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        ConfigureAttachmentButton(btnAddAttachment, "Dosya Ekle")
        ConfigureAttachmentButton(btnOpenAttachment, "Aç")
        ConfigureAttachmentButton(btnDeleteAttachment, "Sil")
        AddHandler btnAddAttachment.Click, AddressOf AddAttachment_Click
        AddHandler btnOpenAttachment.Click, AddressOf OpenAttachment_Click
        AddHandler btnDeleteAttachment.Click, AddressOf DeleteAttachment_Click
        actions.Controls.Add(btnAddAttachment, 0, 0)
        actions.Controls.Add(btnOpenAttachment, 1, 0)
        actions.Controls.Add(btnDeleteAttachment, 2, 0)
        panel.Controls.Add(actions, 0, 2)
        Return panel
    End Function

    Private Shared Sub ConfigureAttachmentButton(button As Button, text As String)
        button.Text = text
        button.Dock = DockStyle.Fill
        button.AutoSize = False
        button.Margin = New Padding(0, 2, 6, 0)
        button.FlatStyle = FlatStyle.Flat
        button.FlatAppearance.BorderColor = Color.FromArgb(180, 198, 216)
        button.BackColor = Color.White
        button.ForeColor = Color.FromArgb(31, 71, 126)
        button.Font = New Font("Segoe UI", 8.0F, FontStyle.Bold)
    End Sub

    Private Function BuildTestExecutionPanel() As Control
        Dim group As New GroupBox() With {
            .Text = "Sıralı Test Uygulama ve Tamamlanma Takibi",
            .Dock = DockStyle.Fill,
            .BackColor = Color.White,
            .Padding = New Padding(10),
            .Margin = New Padding(0, 8, 0, 0)
        }
        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .Padding = New Padding(4),
            .BackColor = Color.White
        }
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
        group.Controls.Add(layout)

        lblTestStepSummary.Dock = DockStyle.Fill
        lblTestStepSummary.BackColor = Color.FromArgb(237, 244, 253)
        lblTestStepSummary.ForeColor = Color.FromArgb(31, 71, 126)
        lblTestStepSummary.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblTestStepSummary.TextAlign = ContentAlignment.MiddleLeft
        lblTestStepSummary.Padding = New Padding(12, 0, 8, 0)
        lblTestStepSummary.Margin = New Padding(0, 0, 0, 6)
        layout.Controls.Add(lblTestStepSummary, 0, 0)

        ConfigureTestStepGrid()
        layout.Controls.Add(testStepGrid, 0, 1)

        Dim actions As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .BackColor = Color.White,
            .Padding = New Padding(0, 8, 0, 0),
            .Margin = New Padding(0)
        }
        ConfigureActionButton(btnCompleteStep, "✓ Sıradaki Testi Tamamla", 200, Color.FromArgb(220, 245, 226), Color.DarkGreen)
        ConfigureActionButton(btnSkipStep, "Gerekçe ile Atla", 145, Color.FromArgb(255, 244, 214), Color.FromArgb(128, 88, 0))
        ConfigureActionButton(btnReopenStep, "Seçili Testi Geri Aç", 175, Color.MistyRose, Color.DarkRed)
        btnCompleteStep.Margin = New Padding(0, 0, 8, 0)
        btnSkipStep.Margin = New Padding(0, 0, 8, 0)
        btnReopenStep.Margin = New Padding(0)
        AddHandler btnCompleteStep.Click, AddressOf CompleteStep_Click
        AddHandler btnSkipStep.Click, AddressOf SkipStep_Click
        AddHandler btnReopenStep.Click, AddressOf ReopenStep_Click
        actions.Controls.AddRange({btnCompleteStep, btnSkipStep, btnReopenStep})
        layout.Controls.Add(actions, 0, 2)
        Return group
    End Function

    Private Sub ConfigureTestStepGrid()
        testStepGrid.Dock = DockStyle.Fill
        testStepGrid.Margin = New Padding(0)
        testStepGrid.AllowUserToAddRows = False
        testStepGrid.AllowUserToDeleteRows = False
        testStepGrid.AllowUserToResizeRows = False
        testStepGrid.AutoGenerateColumns = False
        testStepGrid.BackgroundColor = Color.White
        testStepGrid.BorderStyle = BorderStyle.FixedSingle
        testStepGrid.ColumnHeadersHeight = 34
        testStepGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
        testStepGrid.RowTemplate.Height = 34
        testStepGrid.MultiSelect = False
        testStepGrid.ReadOnly = True
        testStepGrid.RowHeadersVisible = False
        testStepGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        testStepGrid.Columns.Clear()
        testStepGrid.Columns.Add(New DataGridViewCheckBoxColumn() With {
            .Name = "Done",
            .HeaderText = "✓",
            .Width = 45,
            .ReadOnly = True
        })
        testStepGrid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "SortNo", .HeaderText = "SIRA", .Width = 58})
        testStepGrid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "TestName", .HeaderText = "TEST", .Width = 185})
        testStepGrid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Description", .HeaderText = "TEST AÇIKLAMASI / KRİTER", .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, .MinimumWidth = 190})
        testStepGrid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Status", .HeaderText = "DURUM", .Width = 105})
        testStepGrid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Result", .HeaderText = "SONUÇ", .Width = 125})
        testStepGrid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "PerformedBy", .HeaderText = "YAPAN", .Width = 125})
        testStepGrid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "PerformedAt", .HeaderText = "TARİH / SAAT", .Width = 135})
        testStepGrid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Explanation", .HeaderText = "AÇIKLAMA / GEREKÇE", .Width = 190})
        AddHandler testStepGrid.SelectionChanged, Sub() UpdateTestStepActionButtons()
        AddHandler testStepGrid.CellDoubleClick,
            Sub(sender As Object, e As DataGridViewCellEventArgs)
                If e.RowIndex < 0 Then Return
                Dim stepItem = TryCast(testStepGrid.Rows(e.RowIndex).Tag, TestRequestStep)
                Dim nextPending = currentTestSteps.FirstOrDefault(Function(item) String.Equals(item.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
                If stepItem IsNot Nothing AndAlso nextPending Is stepItem AndAlso btnCompleteStep.Enabled Then CompleteStep_Click(btnCompleteStep, EventArgs.Empty)
            End Sub
    End Sub

    Private Sub LoadRecord()
        If isNew Then
            lblStatus.Text = "YENİ TALEP"
            lblRequestAudit.Text = "Talep eden: " & AppState.CurrentUserName & Environment.NewLine & "Tarih/saat kayıt sırasında atanır."
            lblLabAudit.Text = "Henüz işleme alınmadı."
            lblResultInfo.Text = "Talep oluşturulduktan sonra laboratuvar/kontrol süreci bu alanda izlenir."
            SetRequestingDepartments({DefaultRequestingDepartment()})
            cboRequestedDepartment.SelectedItem = "KALİTE LAB."
            Return
        End If

        _AffectedRequestId = ValueOf("RequestId")
        lblStatus.Text = StatusDisplay(ValueOf("Status")) & "  |  " & AffectedRequestId
        SetRequestingDepartments(TestRequestEmailNotificationService.SplitDepartments(ValueOf("RequestingDepartment")))
        cboRequestedDepartment.Text = ValueOf("RequestedDepartment")
        CheckReasons(ValueOf("RequestReason"))
        txtProduct.Text = ValueOf("ProductNameTrCode")
        LoadRequestedTests(ValueOf("RequestedTests"))
        txtSampleQuantity.Text = ValueOf("SampleQuantity")
        cboPriority.Text = If(ValueOf("Priority") = "", "NORMAL", ValueOf("Priority"))
        Dim dueDate As DateTime
        If DateTime.TryParse(ValueOf("DueDate"), dueDate) Then
            dtpDueDate.Value = dueDate
            dtpDueDate.Checked = True
        End If
        txtRequesterReportNo.Text = ValueOf("RequesterReportNo")
        txtRequesterExplanation.Text = ValueOf("RequesterExplanation")
        txtLabReportNo.Text = ValueOf("LabReportNo")
        cboResult.Text = ValueOf("Result")
        txtLabExplanation.Text = ValueOf("LabExplanation")

        lblRequestAudit.Text = "Talep eden: " & DisplayValue(ValueOf("CreatedBy")) & Environment.NewLine &
                               "Talep tarihi: " & FormatDateTime(ValueOf("CreatedAt"))
        lblLabAudit.Text = "İşleme alan: " & DisplayValue(ValueOf("AcceptedBy")) & Environment.NewLine &
                           "İşleme alma: " & FormatDateTime(ValueOf("AcceptedAt")) & Environment.NewLine &
                           "Sonuçlandıran: " & DisplayValue(ValueOf("CompletedBy")) & " / " & FormatDateTime(ValueOf("CompletedAt"))
        lblResultInfo.Text = BuildResultInfoText()
        RefreshTestSteps()
        RefreshAttachments()
        If ValueOf("Status").Trim().ToUpperInvariant() = "ACCEPTED" AndAlso testDetailTabs.TabPages.Count > 1 Then
            testDetailTabs.SelectedIndex = 1
        End If
    End Sub

    Private Sub RefreshAttachments()
        If isNew OrElse AffectedRequestId = "" Then Return
        attachmentList.Items.Clear()
        For Each row In TestRequestAttachmentService.GetAttachments(AffectedRequestId)
            attachmentList.Items.Add(New AttachmentListItem(row))
        Next
        lblAttachmentCount.Text = If(attachmentList.Items.Count = 0,
                                     "Dosya eklenmemiş (isteğe bağlı).",
                                     attachmentList.Items.Count.ToString() & " dosya")
        If attachmentList.Items.Count > 0 Then attachmentList.SelectedIndex = 0
        UpdateAttachmentButtons()
    End Sub

    Private Function SelectedAttachment() As AttachmentListItem
        Return TryCast(attachmentList.SelectedItem, AttachmentListItem)
    End Function

    Private Function CanEditResultAttachments() As Boolean
        If isNew Then Return False
        If AppState.IsAdmin Then Return True
        Dim status = ValueOf("Status").Trim().ToUpperInvariant()
        Dim allTestsResolved = currentTestSteps.Count > 0 AndAlso currentTestSteps.All(Function(stepItem) stepItem.IsResolved)
        Return AppState.CanProcessTestRequestForDepartment(ValueOf("RequestedDepartment")) AndAlso
               status = "ACCEPTED" AndAlso
               allTestsResolved
    End Function

    Private Sub UpdateAttachmentButtons()
        Dim canEdit = CanEditResultAttachments()
        Dim hasSelection = SelectedAttachment() IsNot Nothing
        btnAddAttachment.Enabled = canEdit
        btnDeleteAttachment.Enabled = canEdit AndAlso hasSelection
        btnOpenAttachment.Enabled = hasSelection
    End Sub

    Private Sub AddAttachment_Click(sender As Object, e As EventArgs)
        If Not CanEditResultAttachments() Then Return
        Using dialog As New OpenFileDialog() With {
            .Title = "Test sonuç dosyası seç",
            .Filter = "Desteklenen dosyalar|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.csv;*.txt;*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.zip;*.7z|Tüm dosyalar|*.*",
            .Multiselect = True,
            .CheckFileExists = True
        }
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                For Each filePath In dialog.FileNames
                    TestRequestAttachmentService.AddAttachment(AffectedRequestId, filePath)
                Next
                recordChanged = True
                RefreshAttachments()
            Catch ex As UnauthorizedAccessException
                AuthorizationService.ShowDenied(ex, Me)
            Catch ex As Exception
                MessageBox.Show(ex.Message, "Dosya eklenemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End Try
        End Using
    End Sub

    Private Sub OpenAttachment_Click(sender As Object, e As EventArgs)
        Dim selected = SelectedAttachment()
        If selected Is Nothing Then Return
        Try
            Dim fullPath = TestRequestAttachmentService.ResolveAttachmentPath(selected.Row)
            If fullPath = "" OrElse Not File.Exists(fullPath) Then Throw New FileNotFoundException("Sonuç dosyası ortak veri klasöründe bulunamadı.", fullPath)
            Process.Start(New ProcessStartInfo(fullPath) With {.UseShellExecute = True})
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Dosya açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub DeleteAttachment_Click(sender As Object, e As EventArgs)
        If Not CanEditResultAttachments() Then Return
        Dim selected = SelectedAttachment()
        If selected Is Nothing Then Return
        If MessageBox.Show("Seçili sonuç dosyası silinsin mi?" & Environment.NewLine & selected.ToString(),
                           "Dosya sil", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) <> DialogResult.Yes Then Return
        Try
            TestRequestAttachmentService.DeleteAttachment(
                DataService.GetValue(selected.Row, "AttachmentId"),
                AffectedRequestId)
            recordChanged = True
            RefreshAttachments()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Dosya silinemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub RefreshTestSteps()
        If isNew Then Return
        currentTestSteps = DataService.GetTestRequestSteps(AffectedRequestId)
        testStepGrid.Rows.Clear()

        Dim nextPending = currentTestSteps.FirstOrDefault(Function(stepItem) String.Equals(stepItem.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
        For Each stepItem In currentTestSteps.OrderBy(Function(item) item.SortNo)
            Dim isCompleted = String.Equals(stepItem.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase)
            Dim performedBy = If(isCompleted, stepItem.CompletedBy, If(String.Equals(stepItem.Status, "SKIPPED", StringComparison.OrdinalIgnoreCase), stepItem.SkippedBy, ""))
            Dim performedAt = If(isCompleted, stepItem.CompletedAt, If(String.Equals(stepItem.Status, "SKIPPED", StringComparison.OrdinalIgnoreCase), stepItem.SkippedAt, ""))
            Dim explanation = If(String.Equals(stepItem.Status, "SKIPPED", StringComparison.OrdinalIgnoreCase), stepItem.SkipReason, stepItem.Explanation)
            Dim rowIndex = testStepGrid.Rows.Add(
                isCompleted,
                stepItem.SortNo,
                stepItem.TestName,
                stepItem.TestDescription,
                TestStepStatusDisplay(stepItem.Status, stepItem Is nextPending),
                stepItem.Result,
                performedBy,
                FormatDateTime(performedAt),
                explanation)
            Dim gridRow = testStepGrid.Rows(rowIndex)
            gridRow.Tag = stepItem
            ApplyTestStepRowStyle(gridRow, stepItem, stepItem Is nextPending)
        Next

        Dim completedCount = currentTestSteps.Where(Function(stepItem) String.Equals(stepItem.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase)).Count()
        Dim skippedCount = currentTestSteps.Where(Function(stepItem) String.Equals(stepItem.Status, "SKIPPED", StringComparison.OrdinalIgnoreCase)).Count()
        If testDetailTabs.TabPages.Count > 1 Then
            testDetailTabs.TabPages(1).Text = "Test Uygulama Sırası (" & (completedCount + skippedCount).ToString() & "/" & currentTestSteps.Count.ToString() & ")"
        End If
        If currentTestSteps.Count = 0 Then
            lblTestStepSummary.Text = "Test ataması bekleniyor. Testler Kalite Kontrol Yöneticisi veya Admin tarafından sıraya alınmalıdır."
        ElseIf nextPending Is Nothing Then
            lblTestStepSummary.Text = "Tüm test adımları çözüldü.  ✓ Tamamlanan: " & completedCount.ToString() &
                                          "  |  Atlanan: " & skippedCount.ToString() &
                                          "  |  Toplam: " & currentTestSteps.Count.ToString()
        Else
            lblTestStepSummary.Text = "İlerleme: " & (completedCount + skippedCount).ToString() & "/" & currentTestSteps.Count.ToString() &
                                          "  |  Sıradaki: " & nextPending.SortNo.ToString() & ". " & nextPending.TestName
        End If

        If nextPending IsNot Nothing Then
            For Each gridRow As DataGridViewRow In testStepGrid.Rows
                If gridRow.Tag Is nextPending Then
                    gridRow.Selected = True
                    testStepGrid.CurrentCell = gridRow.Cells("TestName")
                    Exit For
                End If
            Next
        ElseIf testStepGrid.Rows.Count > 0 Then
            testStepGrid.Rows(testStepGrid.Rows.Count - 1).Selected = True
        End If
        UpdateTestStepActionButtons()
    End Sub

    Private Shared Sub ApplyTestStepRowStyle(gridRow As DataGridViewRow, stepItem As TestRequestStep, isNextPending As Boolean)
        Dim status = If(stepItem.Status, "").Trim().ToUpperInvariant()
        If status = "COMPLETED" Then
            gridRow.DefaultCellStyle.BackColor = Color.Honeydew
            gridRow.DefaultCellStyle.ForeColor = Color.DarkGreen
        ElseIf status = "SKIPPED" Then
            gridRow.DefaultCellStyle.BackColor = Color.FromArgb(242, 242, 242)
            gridRow.DefaultCellStyle.ForeColor = Color.DimGray
        ElseIf status = "CANCELLED" Then
            gridRow.DefaultCellStyle.BackColor = Color.MistyRose
            gridRow.DefaultCellStyle.ForeColor = Color.DarkRed
        ElseIf isNextPending Then
            gridRow.DefaultCellStyle.BackColor = Color.LemonChiffon
            gridRow.DefaultCellStyle.ForeColor = Color.FromArgb(112, 71, 0)
            gridRow.DefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        Else
            gridRow.DefaultCellStyle.BackColor = Color.White
            gridRow.DefaultCellStyle.ForeColor = Color.FromArgb(70, 78, 90)
        End If
    End Sub

    Private Shared Function TestStepStatusDisplay(statusValue As String, isNextPending As Boolean) As String
        Select Case If(statusValue, "").Trim().ToUpperInvariant()
            Case "COMPLETED" : Return "✓ TAMAMLANDI"
            Case "SKIPPED" : Return "ATLANDI"
            Case "CANCELLED" : Return "İPTAL"
            Case Else : Return If(isNextPending, "SIRADAKİ", "BEKLİYOR")
        End Select
    End Function

    Private Function SelectedTestStep() As TestRequestStep
        If testStepGrid.CurrentRow Is Nothing Then Return Nothing
        Return TryCast(testStepGrid.CurrentRow.Tag, TestRequestStep)
    End Function

    Private Sub UpdateTestStepActionButtons()
        If isNew Then Return
        Dim requestStatus = ValueOf("Status").Trim().ToUpperInvariant()
        Dim nextPending = currentTestSteps.FirstOrDefault(Function(stepItem) String.Equals(stepItem.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
        Dim selected = SelectedTestStep()
        Dim canPerformTest = AppState.CanProcessTestRequestForDepartment(ValueOf("RequestedDepartment"))
        btnCompleteStep.Visible = canPerformTest
        btnCompleteStep.Enabled = canPerformTest AndAlso requestStatus = "ACCEPTED" AndAlso nextPending IsNot Nothing
        btnSkipStep.Visible = AppState.CanOverrideTestRequestSteps
        btnSkipStep.Enabled = AppState.CanOverrideTestRequestSteps AndAlso requestStatus = "ACCEPTED" AndAlso nextPending IsNot Nothing
        btnReopenStep.Visible = AppState.CanOverrideTestRequestSteps
        btnReopenStep.Enabled = AppState.CanOverrideTestRequestSteps AndAlso
                                    requestStatus = "ACCEPTED" AndAlso
                                    selected IsNot Nothing AndAlso selected.IsResolved
    End Sub

    Private Sub CompleteStep_Click(sender As Object, e As EventArgs)
        Dim nextPending = currentTestSteps.FirstOrDefault(Function(stepItem) String.Equals(stepItem.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
        If nextPending Is Nothing Then Return
        Using dialog As New FrmTestStepAction(nextPending, FrmTestStepAction.ModeComplete)
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                DataService.CompleteTestRequestStep(AffectedRequestId, nextPending.StepId, dialog.ResultText, dialog.Explanation)
                recordChanged = True
                RefreshTestSteps()
                ApplyPermissions()
            Catch ex As UnauthorizedAccessException
                AuthorizationService.ShowDenied(ex, Me)
            Catch ex As Exception
                MessageBox.Show(ex.Message, "Test tamamlanamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Private Sub SkipStep_Click(sender As Object, e As EventArgs)
        Dim nextPending = currentTestSteps.FirstOrDefault(Function(stepItem) String.Equals(stepItem.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
        If nextPending Is Nothing Then Return
        Using dialog As New FrmTestStepAction(nextPending, FrmTestStepAction.ModeSkip)
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                DataService.SkipTestRequestStep(AffectedRequestId, nextPending.StepId, dialog.Explanation)
                recordChanged = True
                RefreshTestSteps()
                ApplyPermissions()
            Catch ex As UnauthorizedAccessException
                AuthorizationService.ShowDenied(ex, Me)
            Catch ex As Exception
                MessageBox.Show(ex.Message, "Test atlanamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Private Sub ReopenStep_Click(sender As Object, e As EventArgs)
        Dim selected = SelectedTestStep()
        If selected Is Nothing OrElse Not selected.IsResolved Then Return
        Using dialog As New FrmTestStepAction(selected, FrmTestStepAction.ModeReopen)
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                DataService.ReopenTestRequestStep(AffectedRequestId, selected.StepId, dialog.Explanation)
                recordChanged = True
                RefreshTestSteps()
                ApplyPermissions()
            Catch ex As UnauthorizedAccessException
                AuthorizationService.ShowDenied(ex, Me)
            Catch ex As Exception
                MessageBox.Show(ex.Message, "Test geri açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Private Sub ApplyPermissions()
        Dim status = ValueOf("Status").Trim().ToUpperInvariant()
        Dim canPerformTest = Not isNew AndAlso AppState.CanProcessTestRequestForDepartment(ValueOf("RequestedDepartment"))
        Dim canEditRequest = (isNew AndAlso AppState.CanCreateTestRequest) OrElse (Not isNew AndAlso AppState.IsAdmin)
        For Each control In {DirectCast(cboRequestedDepartment, Control), pnlReasons, txtProduct,
                             txtSampleQuantity, cboPriority, dtpDueDate, txtRequesterReportNo, txtRequesterExplanation}
            control.Enabled = canEditRequest
        Next
        btnSelectRequestingDepartments.Visible = canEditRequest
        btnSelectRequestingDepartments.Enabled = canEditRequest
        If pnlRequestingDepartments.ColumnStyles.Count > 1 Then
            pnlRequestingDepartments.ColumnStyles(1).Width = If(canEditRequest, 120.0F, 0.0F)
        End If

        Dim canAssignTests = Not isNew AndAlso
                             AppState.CanAssignTestRequestTests AndAlso
                             (AppState.IsAdmin OrElse
                              (status <> "COMPLETED" AndAlso
                               status <> "CANCELLED" AndAlso
                               Not currentTestSteps.Any(Function(stepItem) stepItem.IsResolved)))
        pnlRequestedTests.Enabled = True
        txtRequestedTests.ReadOnly = True
        btnSelectRequestedTests.Visible = canAssignTests
        btnSelectRequestedTests.Enabled = canAssignTests
        btnSelectRequestedTests.Text = If(String.IsNullOrWhiteSpace(txtRequestedTests.Text), "Test Ata", "Test Değiştir")
        If isNew Then
            txtRequestedTests.PlaceholderText = "Test seçimi kayıt oluşturulduktan sonra Kalite Kontrol Yöneticisi/Admin tarafından yapılır"
        ElseIf String.IsNullOrWhiteSpace(txtRequestedTests.Text) Then
            txtRequestedTests.PlaceholderText = If(canAssignTests, "Bu talebe test atamak için Test Ata butonuna basın", "Test seçimi bekleniyor")
        End If

        Dim allTestsResolved = currentTestSteps.Count > 0 AndAlso currentTestSteps.All(Function(stepItem) stepItem.IsResolved)
        Dim canEnterFinalResult = Not isNew AndAlso
                                  canPerformTest AndAlso
                                  status = "ACCEPTED" AndAlso
                                  allTestsResolved
        Dim canEditFinalResult = canEnterFinalResult OrElse (Not isNew AndAlso AppState.IsAdmin)
        txtLabReportNo.ReadOnly = Not canEditFinalResult
        cboResult.Enabled = canEditFinalResult
        txtLabExplanation.ReadOnly = Not canEditFinalResult
        btnCreate.Visible = isNew AndAlso AppState.CanCreateTestRequest
        btnAdminSave.Visible = Not isNew AndAlso AppState.IsAdmin
        btnAccept.Visible = canPerformTest AndAlso status = "OPEN"
        btnComplete.Visible = canPerformTest AndAlso status = "ACCEPTED"
        btnComplete.Enabled = canEnterFinalResult

        Dim isCreator = Not isNew AndAlso String.Equals(ValueOf("CreatedBy"), AppState.CurrentUserName, StringComparison.OrdinalIgnoreCase)
        btnCancelRequest.Visible = Not isNew AndAlso (status = "OPEN" OrElse status = "ACCEPTED") AndAlso (isCreator OrElse AppState.CanProcessTestRequest)
        UpdateTestStepActionButtons()
        UpdateAttachmentButtons()
    End Sub

    Private Sub Create_Click(sender As Object, e As EventArgs)
        Try
            Dim row As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                {"RequestingDepartment", BuildRequestingDepartmentText()},
                {"RequestedDepartment", cboRequestedDepartment.Text.Trim()},
                {"RequestReason", String.Join(", ", reasonChecks.Where(Function(check) check.Checked).Select(Function(check) check.Text))},
                {"ProductNameTrCode", txtProduct.Text.Trim()},
                {"RequestedTests", BuildRequestedTestsText()},
                {"SampleQuantity", txtSampleQuantity.Text.Trim()},
                {"Priority", cboPriority.Text.Trim()},
                {"DueDate", If(dtpDueDate.Checked, dtpDueDate.Value.ToString("yyyy-MM-dd"), "")},
                {"RequesterReportNo", txtRequesterReportNo.Text.Trim()},
                {"RequesterExplanation", txtRequesterExplanation.Text.Trim()}
            }
            _AffectedRequestId = DataService.CreateTestRequest(row)
            NotifyTestRequestEvent(TestRequestEmailNotificationService.EventRequestCreated)
            DialogResult = DialogResult.OK
            Close()
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Test talebi oluşturulamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub AdminSave_Click(sender As Object, e As EventArgs)
        If isNew OrElse Not AppState.IsAdmin Then Return
        Try
            Dim row As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                {"RequestingDepartment", BuildRequestingDepartmentText()},
                {"RequestedDepartment", cboRequestedDepartment.Text.Trim()},
                {"RequestReason", String.Join(", ", reasonChecks.Where(Function(check) check.Checked).Select(Function(check) check.Text))},
                {"ProductNameTrCode", txtProduct.Text.Trim()},
                {"SampleQuantity", txtSampleQuantity.Text.Trim()},
                {"Priority", cboPriority.Text.Trim()},
                {"DueDate", If(dtpDueDate.Checked, dtpDueDate.Value.ToString("yyyy-MM-dd"), "")},
                {"RequesterReportNo", txtRequesterReportNo.Text.Trim()},
                {"RequesterExplanation", txtRequesterExplanation.Text.Trim()},
                {"LabReportNo", txtLabReportNo.Text.Trim()},
                {"Result", cboResult.Text.Trim()},
                {"LabExplanation", txtLabExplanation.Text.Trim()}
            }
            DataService.UpdateTestRequestByAdmin(AffectedRequestId, row)
            recordChanged = True
            DialogResult = DialogResult.OK
            Close()
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Test talebi güncellenemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Accept_Click(sender As Object, e As EventArgs)
        Try
            DataService.AcceptTestRequest(AffectedRequestId)
            NotifyTestRequestEvent(TestRequestEmailNotificationService.EventRequestAccepted)
            DialogResult = DialogResult.OK
            Close()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Talep işleme alınamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Complete_Click(sender As Object, e As EventArgs)
        Try
            DataService.CompleteTestRequest(AffectedRequestId, cboResult.Text, txtLabReportNo.Text, txtLabExplanation.Text)
            NotifyTestRequestEvent(TestRequestEmailNotificationService.CompletionEventType(cboResult.Text))
            DialogResult = DialogResult.OK
            Close()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Test talebi sonuçlandırılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Cancel_Click(sender As Object, e As EventArgs)
        Dim reason = Microsoft.VisualBasic.Interaction.InputBox("İptal nedenini yazın:", "Test talebini iptal et", "")
        If reason.Trim() = "" Then Return
        Try
            DataService.CancelTestRequest(AffectedRequestId, reason)
            NotifyTestRequestEvent(TestRequestEmailNotificationService.EventRequestCancelled)
            DialogResult = DialogResult.OK
            Close()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Test talebi iptal edilemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub NotifyTestRequestEvent(eventType As String)
        Try
            Dim requestId = If(AffectedRequestId, "").Trim()
            If requestId = "" Then Return

            Dim row = DataService.GetTestRequests().
                FirstOrDefault(Function(item) String.Equals(DataService.GetValue(item, "RequestId"), requestId, StringComparison.OrdinalIgnoreCase))
            If row Is Nothing Then Return

            Dim emailError As String = ""
            If Not TestRequestEmailNotificationService.TryNotifyEvent(row, eventType, emailError) Then
                MessageBox.Show(
                    "İşlem kaydedildi ancak otomatik e-posta gönderilemedi." & Environment.NewLine & Environment.NewLine & emailError,
                    "Mail gönderilemedi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning)
            End If
        Catch ex As Exception
            ErrorLogService.Log("FrmTestRequestDetail.NotifyTestRequestEvent", ex)
            MessageBox.Show(
                "İşlem kaydedildi ancak otomatik e-posta bildirimi hazırlanamadı." & Environment.NewLine & Environment.NewLine & ex.Message,
                "Mail bildirimi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub CheckReasons(serialized As String)
        Dim selected = If(serialized, "").Split({","c, ";"c}, StringSplitOptions.RemoveEmptyEntries).Select(Function(item) item.Trim()).ToList()
        For Each check In reasonChecks
            check.Checked = selected.Any(Function(item) String.Equals(item, check.Text, StringComparison.OrdinalIgnoreCase))
        Next
    End Sub

    Private Function ValueOf(key As String) As String
        If sourceRow Is Nothing Then Return ""
        Return DataService.GetValue(sourceRow, key)
    End Function

    Private Function BuildResultInfoText() As String
        Dim status = ValueOf("Status").Trim().ToUpperInvariant()
        Select Case status
            Case "COMPLETED"
                Return "Sonuç: " & DisplayValue(ValueOf("Result")) & Environment.NewLine &
                       "Tamamlanma: " & FormatDateTime(ValueOf("CompletedAt"))
            Case "ACCEPTED"
                Return "Talep işleme alındı; laboratuvar sonucu bekleniyor."
            Case "CANCELLED"
                Return "Talep iptal edildi." & Environment.NewLine &
                       "Neden: " & DisplayValue(ValueOf("CancelReason"))
            Case Else
                Return "Laboratuvar işlemi bekleniyor."
        End Select
    End Function

    Private Function DefaultRequestingDepartment() As String
        If AppState.IsIncomingQualityControlUser Then Return "GKK"
        If AppState.IsMechanismQualityControlUser Then Return "MEKANİZMA"
        If AppState.IsPlasticQualityControlUser Then Return "PLASTİKHANE"
        Return "GKK"
    End Function

    Private Shared Function StatusDisplay(status As String) As String
        Select Case If(status, "").Trim().ToUpperInvariant()
            Case "OPEN" : Return "YENİ"
            Case "ACCEPTED" : Return "İŞLEMDE"
            Case "COMPLETED" : Return "TAMAMLANDI"
            Case "CANCELLED" : Return "İPTAL"
            Case Else : Return "-"
        End Select
    End Function

    Private Shared Function FormatDateTime(value As String) As String
        Dim parsed As DateTime
        If DateTime.TryParse(value, parsed) Then Return parsed.ToString("dd.MM.yyyy HH:mm")
        Return "-"
    End Function

    Private Shared Function DisplayValue(value As String) As String
        Return If(String.IsNullOrWhiteSpace(value), "-", value.Trim())
    End Function

    Private Shared Sub ConfigureActionButton(button As Button, text As String, width As Integer, backColor As Color, foreColor As Color)
        button.Text = text
        button.Width = width
        button.Height = 34
        button.BackColor = backColor
        button.ForeColor = foreColor
        button.FlatStyle = FlatStyle.Flat
        button.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        button.Margin = New Padding(8, 0, 0, 0)
        button.Cursor = Cursors.Hand
        button.UseVisualStyleBackColor = False
    End Sub

    Private Shared Sub ConfigureMiniButton(button As Button, text As String)
        button.Text = text
        button.Dock = DockStyle.Fill
        button.Height = 30
        button.BackColor = Color.White
        button.ForeColor = Color.FromArgb(31, 71, 126)
        button.FlatStyle = FlatStyle.Flat
        button.Font = New Font("Segoe UI", 8.5F, FontStyle.Bold)
        button.Margin = New Padding(0, 0, 0, 4)
        button.Cursor = Cursors.Hand
        button.UseVisualStyleBackColor = False
    End Sub

    Private Shared Sub ConfigureCombo(combo As ComboBox, items As IEnumerable(Of String))
        Dim itemArray = items.ToArray()
        Dim calculatedWidth = CalculateComboWidth(combo, itemArray)
        combo.Dock = DockStyle.None
        combo.Anchor = AnchorStyles.Left
        combo.Width = calculatedWidth
        combo.DropDownWidth = calculatedWidth
        combo.DropDownStyle = ComboBoxStyle.DropDownList
        combo.Margin = New Padding(5, 6, 5, 6)
        combo.Items.AddRange(itemArray.Cast(Of Object)().ToArray())
    End Sub

    Private Shared Function CalculateComboWidth(combo As ComboBox, items As IEnumerable(Of String)) As Integer
        Dim maxTextWidth = items.
            Select(Function(item) TextRenderer.MeasureText(If(item, ""), combo.Font, New Size(2000, 200), TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine).Width).
            DefaultIfEmpty(90).
            Max()

        Dim width = maxTextWidth + SystemInformation.VerticalScrollBarWidth + 42
        Return Math.Max(150, Math.Min(420, width))
    End Function

    Private Sub ConfigureRequestingDepartmentsPanel()
        pnlRequestingDepartments.Dock = DockStyle.Fill
        pnlRequestingDepartments.Margin = New Padding(5, 4, 5, 4)
        pnlRequestingDepartments.Padding = New Padding(0)
        pnlRequestingDepartments.BackColor = Color.White
        pnlRequestingDepartments.ColumnCount = 2
        pnlRequestingDepartments.RowCount = 1
        pnlRequestingDepartments.ColumnStyles.Clear()
        pnlRequestingDepartments.RowStyles.Clear()
        pnlRequestingDepartments.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        pnlRequestingDepartments.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 120.0F))
        pnlRequestingDepartments.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        pnlRequestingDepartments.Controls.Clear()

        ConfigureTextBox(txtRequestingDepartments, "Bir veya daha fazla bölüm seçin")
        txtRequestingDepartments.ReadOnly = True
        txtRequestingDepartments.BackColor = Color.White
        txtRequestingDepartments.Margin = New Padding(0, 2, 8, 2)

        ConfigureActionButton(btnSelectRequestingDepartments, "Bölüm Seç", 110, Color.FromArgb(31, 71, 126), Color.White)
        btnSelectRequestingDepartments.Dock = DockStyle.Fill
        btnSelectRequestingDepartments.Margin = New Padding(0, 2, 0, 2)
        AddHandler btnSelectRequestingDepartments.Click, AddressOf SelectRequestingDepartments_Click

        pnlRequestingDepartments.Controls.Add(txtRequestingDepartments, 0, 0)
        pnlRequestingDepartments.Controls.Add(btnSelectRequestingDepartments, 1, 0)
    End Sub

    Private Sub SelectRequestingDepartments_Click(sender As Object, e As EventArgs)
        Using dialog As New FrmDepartmentSelectionDialog(RequestingDepartments, selectedRequestingDepartments)
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            SetRequestingDepartments(dialog.SelectedDepartments)
        End Using
    End Sub

    Private Sub SetRequestingDepartments(departments As IEnumerable(Of String))
        selectedRequestingDepartments.Clear()
        For Each department In If(departments, Enumerable.Empty(Of String)())
            Dim normalized = If(department, "").Trim()
            If normalized <> "" Then selectedRequestingDepartments.Add(normalized)
        Next
        txtRequestingDepartments.Text = TestRequestEmailNotificationService.FormatDepartmentList(selectedRequestingDepartments)
    End Sub

    Private Function BuildRequestingDepartmentText() As String
        Return TestRequestEmailNotificationService.SerializeDepartments(selectedRequestingDepartments)
    End Function

    Private Sub ConfigureReasonPanel()
        pnlReasons.Dock = DockStyle.Fill
        pnlReasons.Margin = New Padding(5, 4, 5, 4)
        pnlReasons.Padding = New Padding(0, 2, 0, 2)
        pnlReasons.BackColor = Color.White
        pnlReasons.CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        pnlReasons.ColumnCount = 3
        pnlReasons.RowCount = 3
        pnlReasons.ColumnStyles.Clear()
        pnlReasons.RowStyles.Clear()
        For columnIndex = 0 To 2
            pnlReasons.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.3333F))
        Next
        For rowIndex = 0 To 2
            pnlReasons.RowStyles.Add(New RowStyle(SizeType.Percent, 33.3333F))
        Next

        pnlReasons.Controls.Clear()
        reasonChecks.Clear()
        For index = 0 To ReasonOptions.Length - 1
            Dim reason = ReasonOptions(index)
            Dim check As New CheckBox() With {
                .Text = reason,
                .AutoSize = False,
                .Dock = DockStyle.Fill,
                .Margin = New Padding(0, 1, 12, 1),
                .TextAlign = ContentAlignment.MiddleLeft,
                .AutoEllipsis = False
            }
            reasonChecks.Add(check)
            pnlReasons.Controls.Add(check, index Mod 3, index \ 3)
        Next
    End Sub

    Private Sub ConfigureRequestedTestsSummaryPanel()
        pnlRequestedTests.Dock = DockStyle.Fill
        pnlRequestedTests.Margin = New Padding(5, 6, 5, 6)
        pnlRequestedTests.Padding = New Padding(0)
        pnlRequestedTests.BackColor = Color.White
        pnlRequestedTests.CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        pnlRequestedTests.ColumnCount = 2
        pnlRequestedTests.RowCount = 1
        pnlRequestedTests.ColumnStyles.Clear()
        pnlRequestedTests.RowStyles.Clear()
        pnlRequestedTests.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        pnlRequestedTests.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 130.0F))
        pnlRequestedTests.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        pnlRequestedTests.Controls.Clear()

        ConfigureTextBox(txtRequestedTests, "Test seçmek için Test Seç butonuna basın")
        txtRequestedTests.ReadOnly = True
        txtRequestedTests.BackColor = Color.White
        txtRequestedTests.Margin = New Padding(0, 2, 8, 2)

        ConfigureActionButton(btnSelectRequestedTests, "Test Seç", 120, Color.FromArgb(31, 71, 126), Color.White)
        btnSelectRequestedTests.Dock = DockStyle.Fill
        btnSelectRequestedTests.Margin = New Padding(0, 2, 0, 2)
        AddHandler btnSelectRequestedTests.Click, AddressOf SelectRequestedTests_Click

        pnlRequestedTests.Controls.Add(txtRequestedTests, 0, 0)
        pnlRequestedTests.Controls.Add(btnSelectRequestedTests, 1, 0)
    End Sub

    Private Sub SelectRequestedTests_Click(sender As Object, e As EventArgs)
        If isNew Then Return
        Try
            AuthorizationService.Require(AppState.CanAssignTestRequestTests, "Test Talebi Test Atama")
            Using dialog As New FrmTestSelectionDialog(txtRequestedTests.Text)
                If dialog.ShowDialog(Me) = DialogResult.OK Then
                    Dim status = ValueOf("Status").Trim().ToUpperInvariant()
                    Dim requiresAdminReset = AppState.IsAdmin AndAlso
                                             (status = "COMPLETED" OrElse
                                              status = "CANCELLED" OrElse
                                              currentTestSteps.Any(Function(stepItem) stepItem.IsResolved))
                    If requiresAdminReset Then
                        Dim answer = MessageBox.Show(
                            "Test ataması değiştirildiğinde mevcut test adımı sonuçları ve talep sonucu sıfırlanacak; kayıt yeniden işlem durumuna alınacaktır." &
                            Environment.NewLine & Environment.NewLine &
                            "Devam etmek istiyor musunuz?",
                            "Test akışını yeniden başlat",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning,
                            MessageBoxDefaultButton.Button2)
                        If answer <> DialogResult.Yes Then Return
                        DataService.ResetTestRequestTestsByAdmin(AffectedRequestId, dialog.SelectedTestsText)
                    Else
                        DataService.UpdateTestRequestTests(AffectedRequestId, dialog.SelectedTestsText)
                    End If
                    txtRequestedTests.Text = dialog.SelectedTestsText
                    btnSelectRequestedTests.Text = If(String.IsNullOrWhiteSpace(txtRequestedTests.Text), "Test Ata", "Test Değiştir")
                    recordChanged = True
                    If requiresAdminReset Then
                        DialogResult = DialogResult.OK
                        Close()
                        Return
                    End If
                    RefreshTestSteps()
                    ApplyPermissions()
                    ' Test ataması yalnızca kayıt güncellemesidir; otomatik e-posta gönderilmez.
                End If
            End Using
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Test ataması kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ConfigureRequestedTestsPanel()
        pnlRequestedTests.Dock = DockStyle.Fill
        pnlRequestedTests.Margin = New Padding(5, 6, 5, 6)
        pnlRequestedTests.Padding = New Padding(0)
        pnlRequestedTests.BackColor = Color.White
        pnlRequestedTests.ColumnCount = 1
        pnlRequestedTests.RowCount = 2
        pnlRequestedTests.ColumnStyles.Clear()
        pnlRequestedTests.RowStyles.Clear()
        pnlRequestedTests.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        pnlRequestedTests.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        pnlRequestedTests.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        pnlRequestedTests.Controls.Clear()

        clbRequestedTests.Dock = DockStyle.Fill
        clbRequestedTests.CheckOnClick = True
        clbRequestedTests.IntegralHeight = False
        clbRequestedTests.BorderStyle = BorderStyle.FixedSingle
        clbRequestedTests.Margin = New Padding(0, 0, 0, 4)
        clbRequestedTests.Items.Clear()

        For Each item In DataService.GetTestCatalog(True)
            If Not String.IsNullOrWhiteSpace(item.TestName) Then
                clbRequestedTests.Items.Add(item.TestName.Trim(), False)
            End If
        Next

        ConfigureTextBox(txtRequestedTests, "Listede yoksa ek test / kriter açıklaması yazın")
        txtRequestedTests.Multiline = False
        txtRequestedTests.Margin = New Padding(0)

        pnlRequestedTests.Controls.Add(clbRequestedTests, 0, 0)
        pnlRequestedTests.Controls.Add(txtRequestedTests, 0, 1)
    End Sub

    Private Sub ConfigureRequestedTestsPanelV2()
        pnlRequestedTests.Dock = DockStyle.Fill
        pnlRequestedTests.Margin = New Padding(5, 6, 5, 6)
        pnlRequestedTests.Padding = New Padding(8)
        pnlRequestedTests.BackColor = Color.FromArgb(247, 250, 253)
        pnlRequestedTests.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
        pnlRequestedTests.ColumnCount = 1
        pnlRequestedTests.RowCount = 3
        pnlRequestedTests.ColumnStyles.Clear()
        pnlRequestedTests.RowStyles.Clear()
        pnlRequestedTests.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        pnlRequestedTests.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
        pnlRequestedTests.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        pnlRequestedTests.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        pnlRequestedTests.Controls.Clear()

        Dim topBar As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 3,
            .RowCount = 1,
            .BackColor = Color.FromArgb(247, 250, 253),
            .Margin = New Padding(0)
        }
        topBar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        topBar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 145.0F))
        topBar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 100.0F))
        topBar.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        ConfigureTextBox(txtTestFilter, "Test listesinde ara...")
        txtTestFilter.Margin = New Padding(0, 0, 8, 4)
        AddHandler txtTestFilter.TextChanged, Sub() RefreshRequestedTestList()

        lblSelectedTests.Dock = DockStyle.Fill
        lblSelectedTests.TextAlign = ContentAlignment.MiddleLeft
        lblSelectedTests.ForeColor = Color.FromArgb(31, 71, 126)
        lblSelectedTests.Font = New Font("Segoe UI", 8.5F, FontStyle.Bold)
        lblSelectedTests.Margin = New Padding(0, 0, 8, 4)

        ConfigureMiniButton(btnClearSelectedTests, "Temizle")
        AddHandler btnClearSelectedTests.Click,
            Sub()
                selectedRequestedTestNames.Clear()
                For i As Integer = 0 To clbRequestedTests.Items.Count - 1
                    clbRequestedTests.SetItemChecked(i, False)
                Next
                txtRequestedTests.Clear()
                UpdateSelectedTestsLabel()
            End Sub

        topBar.Controls.Add(txtTestFilter, 0, 0)
        topBar.Controls.Add(lblSelectedTests, 1, 0)
        topBar.Controls.Add(btnClearSelectedTests, 2, 0)

        clbRequestedTests.Dock = DockStyle.Fill
        clbRequestedTests.CheckOnClick = True
        clbRequestedTests.IntegralHeight = False
        clbRequestedTests.BorderStyle = BorderStyle.None
        clbRequestedTests.BackColor = Color.White
        clbRequestedTests.Margin = New Padding(0, 2, 0, 4)
        clbRequestedTests.HorizontalScrollbar = True
        clbRequestedTests.Items.Clear()
        AddHandler clbRequestedTests.ItemCheck,
            Sub(sender, e)
                If e.Index >= 0 AndAlso e.Index < clbRequestedTests.Items.Count Then
                    Dim testName = clbRequestedTests.Items(e.Index).ToString()
                    If e.NewValue = CheckState.Checked Then
                        selectedRequestedTestNames.Add(testName)
                    Else
                        selectedRequestedTestNames.Remove(testName)
                    End If
                End If

                If IsHandleCreated AndAlso Not IsDisposed Then
                    BeginInvoke(New MethodInvoker(Sub()
                                                      SyncVisibleRequestedTestChecks()
                                                      UpdateSelectedTestsLabel()
                                                  End Sub))
                End If
            End Sub

        allRequestedTestNames.Clear()
        For Each item In DataService.GetTestCatalog(True)
            If Not String.IsNullOrWhiteSpace(item.TestName) Then
                allRequestedTestNames.Add(item.TestName.Trim())
            End If
        Next
        RefreshRequestedTestList()

        ConfigureTextBox(txtRequestedTests, "Listede yoksa ek test / kriter açıklaması yazın")
        txtRequestedTests.Multiline = False
        txtRequestedTests.Margin = New Padding(0, 4, 0, 0)
        AddHandler txtRequestedTests.TextChanged, Sub() UpdateSelectedTestsLabel()

        pnlRequestedTests.Controls.Add(topBar, 0, 0)
        pnlRequestedTests.Controls.Add(clbRequestedTests, 0, 1)
        pnlRequestedTests.Controls.Add(txtRequestedTests, 0, 2)
        UpdateSelectedTestsLabel()
    End Sub

    Private Sub LoadRequestedTests(serialized As String)
        txtRequestedTests.Text = If(serialized, "").Trim()
    End Sub

    Private Function BuildRequestedTestsText() As String
        Return txtRequestedTests.Text.Trim()
    End Function

    Private Function FindRequestedTestIndex(testName As String) As Integer
        For i As Integer = 0 To clbRequestedTests.Items.Count - 1
            If String.Equals(clbRequestedTests.Items(i).ToString(), testName, StringComparison.OrdinalIgnoreCase) Then
                Return i
            End If
        Next
        Return -1
    End Function

    Private Sub RefreshRequestedTestList()
        Dim filter = txtTestFilter.Text.Trim().ToUpperInvariant()
        clbRequestedTests.Items.Clear()

        For Each testName In allRequestedTestNames
            If filter = "" OrElse testName.ToUpperInvariant().Contains(filter) Then
                clbRequestedTests.Items.Add(testName, selectedRequestedTestNames.Contains(testName))
            End If
        Next

        UpdateSelectedTestsLabel()
    End Sub

    Private Sub SyncVisibleRequestedTestChecks()
        For i As Integer = 0 To clbRequestedTests.Items.Count - 1
            Dim shouldBeChecked = selectedRequestedTestNames.Contains(clbRequestedTests.Items(i).ToString())
            If clbRequestedTests.GetItemChecked(i) <> shouldBeChecked Then
                clbRequestedTests.SetItemChecked(i, shouldBeChecked)
            End If
        Next
    End Sub

    Private Sub UpdateSelectedTestsLabel()
        Dim selectedCount = selectedRequestedTestNames.Count
        Dim extraCount = SplitRequestedTests(txtRequestedTests.Text).Count
        lblSelectedTests.Text = "Seçili: " & (selectedCount + extraCount).ToString()
    End Sub

    Private Shared Function SplitRequestedTests(value As String) As List(Of String)
        Return If(value, "").
            Replace(vbCrLf, ";").
            Replace(vbCr, ";").
            Replace(vbLf, ";").
            Split({";"c}, StringSplitOptions.RemoveEmptyEntries).
            Select(Function(part) part.Trim()).
            Where(Function(part) part <> "").
            ToList()
    End Function

    Private Shared Sub ConfigureTextBox(box As TextBox, placeholder As String)
        box.Dock = DockStyle.Fill
        box.Margin = New Padding(5, 6, 5, 6)
        box.PlaceholderText = placeholder
    End Sub

    Private Shared Sub ConfigureMultiline(box As TextBox, placeholder As String)
        ConfigureTextBox(box, placeholder)
        box.Multiline = True
        box.ScrollBars = ScrollBars.Vertical
    End Sub

    Private Shared Sub ConfigureInfoLabel(label As Label)
        label.Dock = DockStyle.Fill
        label.BackColor = Color.FromArgb(247, 250, 253)
        label.ForeColor = Color.FromArgb(45, 65, 92)
        label.Padding = New Padding(10)
        label.TextAlign = ContentAlignment.MiddleLeft
        label.AutoEllipsis = True
    End Sub

    Private Shared Sub AddField(layout As TableLayoutPanel, caption As String, control As Control, row As Integer)
        Dim label As New Label() With {
            .Text = caption,
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(5)
        }
        layout.Controls.Add(label, 0, row)
        layout.Controls.Add(control, 1, row)
    End Sub
End Class
