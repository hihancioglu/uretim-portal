Imports System.Drawing
Imports System.Globalization
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmPackageMeterControlDetail
    Inherits Form

    Private ReadOnly sourceRow As Dictionary(Of String, String)
    Private ReadOnly isNew As Boolean
    Private ReadOnly txtMeterModel As New TextBox()
    Private ReadOnly txtPulseCount As New TextBox()
    Private ReadOnly txtCustomer As New TextBox()
    Private ReadOnly txtControlDate As New TextBox()
    Private ReadOnly txtOperatorInfo As New TextBox()
    Private ReadOnly txtControllerName As New TextBox()
    Private ReadOnly txtProductionPanelNo As New TextBox()
    Private ReadOnly txtControlPanelNo As New TextBox()
    Private ReadOnly txtReferenceFlowQ4 As New TextBox()
    Private ReadOnly txtReferenceFlowQ3 As New TextBox()
    Private ReadOnly txtReferenceFlowQ2 As New TextBox()
    Private ReadOnly txtReferenceFlowQ1 As New TextBox()
    Private ReadOnly chkSmartMeter As New CheckBox()
    Private ReadOnly txtExplanation As New TextBox()
    Private ReadOnly grid As New DataGridView()
    Private ReadOnly lblStatus As New Label()
    Private ReadOnly lblRangeValue As New TextBox()
    Private ReadOnly referenceToolTip As New ToolTip()
    Private ReadOnly btnAddRow As New Button()
    Private ReadOnly btnRemoveRow As New Button()
    Private ReadOnly btnSaveDraft As New Button()
    Private ReadOnly btnComplete As New Button()
    Private ReadOnly rootLayout As New TableLayoutPanel()
    Private ReadOnly lineLayout As New TableLayoutPanel()
    Private isLoading As Boolean
    Private isUpdatingRange As Boolean
    Private recordChanged As Boolean
    Private compactLayoutApplied As Boolean?

    Public ReadOnly Property HasChanges As Boolean
        Get
            Return recordChanged
        End Get
    End Property

    Public ReadOnly Property AffectedControlId As String

    Public Sub New(Optional row As Dictionary(Of String, String) = Nothing)
        AuthorizationService.Require(AppState.CanOpenPackageMeterControls, "Paket Sayaç Kontrol Detayı")
        AppIconService.Apply(Me)
        sourceRow = If(row Is Nothing, Nothing, New Dictionary(Of String, String)(row, StringComparer.OrdinalIgnoreCase))
        isNew = sourceRow Is Nothing

        Text = If(isNew, "Yeni Paket Sayaç Kontrolü", "Paket Sayaç Kontrol Detayı")
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1540, 900)
        MinimumSize = New Size(920, 640)
        BackColor = Color.FromArgb(244, 247, 251)
        Font = New Font("Segoe UI", 9.0F)

        BuildScreen()
        LoadRecord()
        ApplyPermissions()
        AddHandler ClientSizeChanged, AddressOf Detail_ClientSizeChanged
        AddHandler Shown, Sub() ApplyResponsiveLayout(True)
    End Sub

    Private Sub BuildScreen()
        With rootLayout
            .Dock = DockStyle.Fill
            .ColumnCount = 1
            .RowCount = 5
            .Padding = New Padding(10)
            .BackColor = BackColor
        End With
        rootLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 54.0F))
        rootLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 190.0F))
        rootLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        rootLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 150.0F))
        rootLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        Controls.Add(rootLayout)

        Dim header As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .BackColor = Color.FromArgb(31, 71, 126),
            .Padding = New Padding(16, 4, 16, 4),
            .Margin = New Padding(0, 0, 0, 8)
        }
        header.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 75.0F))
        header.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25.0F))
        header.Controls.Add(New Label() With {
            .Text = "Paketten Alınan Sayaçların Kontrolü",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 12.5F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0)
        lblRangeValue.Dock = DockStyle.None
        lblRangeValue.Text = "R = —"
        lblRangeValue.BackColor = Color.FromArgb(232, 243, 255)
        lblRangeValue.ForeColor = Color.FromArgb(21, 75, 137)
        lblRangeValue.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblRangeValue.TextAlign = HorizontalAlignment.Center
        lblRangeValue.BorderStyle = BorderStyle.FixedSingle
        lblRangeValue.Size = New Size(130, 25)
        lblRangeValue.Anchor = AnchorStyles.None
        lblRangeValue.Margin = New Padding(0)
        lblRangeValue.TabStop = False
        lblRangeValue.AutoSize = False
        lblRangeValue.ReadOnly = True
        lblRangeValue.ShortcutsEnabled = False
        lblRangeValue.Cursor = Cursors.Default
        lblStatus.Dock = DockStyle.Fill
        lblStatus.ForeColor = Color.White
        lblStatus.Font = New Font("Segoe UI", 9.5F, FontStyle.Bold)
        lblStatus.TextAlign = ContentAlignment.MiddleRight
        header.Controls.Add(lblStatus, 1, 0)
        rootLayout.Controls.Add(header, 0, 0)

        rootLayout.Controls.Add(BuildHeaderFields(), 0, 1)
        rootLayout.Controls.Add(BuildLinePanel(), 0, 2)

        Dim explanationGroup As New GroupBox() With {
            .Text = "Açıklama",
            .Dock = DockStyle.Fill,
            .BackColor = Color.White,
            .Padding = New Padding(10),
            .Margin = New Padding(0, 8, 0, 0)
        }
        txtExplanation.Dock = DockStyle.Fill
        txtExplanation.Multiline = True
        txtExplanation.ScrollBars = ScrollBars.Vertical
        txtExplanation.MaxLength = 4000
        txtExplanation.PlaceholderText = "Kontrolün geneliyle ilgili açıklama, sapma veya takip notu..."
        explanationGroup.Controls.Add(txtExplanation)
        rootLayout.Controls.Add(explanationGroup, 0, 3)

        rootLayout.Controls.Add(BuildFooter(), 0, 4)
    End Sub

    Private Sub Detail_ClientSizeChanged(sender As Object, e As EventArgs)
        ApplyResponsiveLayout()
    End Sub

    Private Sub ApplyResponsiveLayout(Optional force As Boolean = False)
        If rootLayout.RowStyles.Count < 5 Then Return

        ' 720p ve benzeri ekranlarda sabit alanlar tabloyu iki satıra kadar
        ' daraltıyordu. Kullanılabilir yüksekliğe göre kompakt düzene geçilir.
        Dim compact = ClientSize.Height > 0 AndAlso ClientSize.Height < 790
        If Not force AndAlso compactLayoutApplied.HasValue AndAlso compactLayoutApplied.Value = compact Then Return

        compactLayoutApplied = compact
        SuspendLayout()
        rootLayout.SuspendLayout()
        grid.SuspendLayout()
        Try
            If compact Then
                rootLayout.RowStyles(0).Height = 46.0F
                rootLayout.RowStyles(1).Height = 136.0F
                rootLayout.RowStyles(3).Height = 72.0F
                ' Footer içindeki 8 px üst margin + 16 px dikey padding + 34 px
                ' buton yüksekliği için en az 58 px gerekir; aksi halde 720p
                ' ekranda butonların alt kısmı kesilir.
                rootLayout.RowStyles(4).Height = 58.0F
                lineLayout.RowStyles(0).Height = 42.0F
                grid.ColumnHeadersHeight = 40
                grid.RowTemplate.Height = 26
            Else
                rootLayout.RowStyles(0).Height = 54.0F
                rootLayout.RowStyles(1).Height = 160.0F
                rootLayout.RowStyles(3).Height = 150.0F
                rootLayout.RowStyles(4).Height = 58.0F
                lineLayout.RowStyles(0).Height = 46.0F
                grid.ColumnHeadersHeight = 48
                grid.RowTemplate.Height = 32
            End If

            For Each row As DataGridViewRow In grid.Rows
                row.Height = grid.RowTemplate.Height
            Next
        Finally
            grid.ResumeLayout()
            rootLayout.ResumeLayout(True)
            ResumeLayout(True)
        End Try
    End Sub

    Private Function BuildHeaderFields() As Control
        Dim group As New Panel() With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.White,
            .Padding = New Padding(12),
            .Margin = New Padding(0)
        }
        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 6,
            .RowCount = 4,
            .BackColor = Color.White
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 175.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 175.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25.0F))
        For index As Integer = 0 To 3
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 25.0F))
        Next
        group.Controls.Add(layout)

        ConfigureTextBox(txtMeterModel, "Sayaç modelini girin")
        ConfigureTextBox(txtPulseCount, "Sayaç pals sayısı")
        ConfigureTextBox(txtCustomer, "Müşteri adı")
        ConfigureTextBox(txtOperatorInfo, "Operatör no / adı-soyadı")
        ConfigureTextBox(txtControllerName, "Kontrol eden kişi")
        ConfigureTextBox(txtProductionPanelNo, "Üretim pano no")
        ConfigureTextBox(txtControlPanelNo, "Kontrol pano no")

        ConfigureTextBox(txtControlDate, "İlk kaydetmede otomatik atanır")
        txtControlDate.ReadOnly = True
        txtControlDate.TabStop = False
        txtControlDate.BackColor = Color.FromArgb(239, 243, 248)
        txtControlDate.ForeColor = Color.FromArgb(31, 71, 126)
        txtControlDate.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)

        txtControllerName.ReadOnly = True
        txtControllerName.TabStop = False
        txtControllerName.BackColor = Color.FromArgb(239, 243, 248)
        txtControllerName.ForeColor = Color.FromArgb(31, 71, 126)
        txtControllerName.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)

        AddField(layout, "Sayaç Modeli", txtMeterModel, 0, 0)
        AddField(layout, "Sayaç Pals Sayısı", txtPulseCount, 3, 0)
        AddField(layout, "Müşteri", txtCustomer, 0, 1)
        AddField(layout, "Kontrol Tarihi", txtControlDate, 3, 1)
        AddField(layout, "Operatör No / Ad Soyad", txtOperatorInfo, 0, 2)
        AddField(layout, "Kontrol Eden Kişi", txtControllerName, 3, 2)
        AddField(layout, "Üretim Pano No", txtProductionPanelNo, 0, 3)
        AddField(layout, "Kontrol Pano No", txtControlPanelNo, 3, 3)
        Return group
    End Function

    Private Function BuildLinePanel() As Control
        Dim group As New Panel() With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.White,
            .Padding = New Padding(8),
            .Margin = New Padding(0, 8, 0, 0)
        }
        lineLayout.Dock = DockStyle.Fill
        lineLayout.ColumnCount = 1
        lineLayout.RowCount = 2
        lineLayout.BackColor = Color.White
        lineLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 46.0F))
        lineLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        group.Controls.Add(lineLayout)

        lineLayout.Controls.Add(BuildReferenceFlowPanel(), 0, 0)
        ConfigureGrid()
        lineLayout.Controls.Add(grid, 0, 1)
        Return group
    End Function

    Private Function BuildReferenceFlowPanel() As Control
        Dim panel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .BackColor = Color.FromArgb(235, 244, 255),
            .Padding = New Padding(10, 5, 8, 5),
            .Margin = New Padding(0, 0, 0, 5)
        }
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 140.0F))

        Dim inputs As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .Margin = New Padding(0),
            .Padding = New Padding(0)
        }
        inputs.Controls.Add(New Label() With {
            .Text = "REFERANS DEBİLERİ (L/saat)",
            .AutoSize = False,
            .Width = 190,
            .Height = 26,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 8.8F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(31, 71, 126),
            .Margin = New Padding(0)
        })
        AddReferenceFlowInput(inputs, "Q4", txtReferenceFlowQ4)
        AddReferenceFlowInput(inputs, "Q3", txtReferenceFlowQ3)
        AddReferenceFlowInput(inputs, "Q2", txtReferenceFlowQ2)
        AddReferenceFlowInput(inputs, "Q1", txtReferenceFlowQ1)

        chkSmartMeter.Text = "Akıllı sayaç — kredi ve vana testleri uygulanacak"
        chkSmartMeter.AutoSize = True
        chkSmartMeter.Dock = DockStyle.None
        chkSmartMeter.CheckAlign = ContentAlignment.MiddleLeft
        chkSmartMeter.Font = New Font("Segoe UI", 8.7F, FontStyle.Bold)
        chkSmartMeter.ForeColor = Color.FromArgb(31, 71, 126)
        chkSmartMeter.Margin = New Padding(20, 3, 0, 0)
        AddHandler chkSmartMeter.CheckedChanged, AddressOf SmartMeter_CheckedChanged
        inputs.Controls.Add(chkSmartMeter)

        Dim rangeHost As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False,
            .AutoScroll = False,
            .BackColor = Color.Transparent,
            .Margin = New Padding(0),
            .Padding = New Padding(0)
        }
        rangeHost.Controls.Add(lblRangeValue)

        panel.Controls.Add(inputs, 0, 0)
        panel.Controls.Add(rangeHost, 1, 0)
        Return panel
    End Function

    Private Sub AddReferenceFlowInput(parent As FlowLayoutPanel, labelText As String, textBox As TextBox)
        parent.Controls.Add(New Label() With {
            .Text = labelText & ":",
            .AutoSize = False,
            .Width = 30,
            .Height = 26,
            .TextAlign = ContentAlignment.MiddleRight,
            .Font = New Font("Segoe UI", 8.6F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(31, 50, 75),
            .Margin = New Padding(7, 0, 3, 0)
        })
        textBox.Width = 85
        textBox.Height = 25
        textBox.TextAlign = HorizontalAlignment.Center
        textBox.Margin = New Padding(0)
        textBox.PlaceholderText = "Debi"
        AddHandler textBox.TextChanged, AddressOf ReferenceFlow_TextChanged
        parent.Controls.Add(textBox)
    End Sub

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.AllowUserToResizeRows = False
        grid.MultiSelect = False
        grid.SelectionMode = DataGridViewSelectionMode.CellSelect
        grid.RowHeadersVisible = False
        grid.AutoGenerateColumns = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.GridColor = Color.FromArgb(218, 225, 234)
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(220, 232, 247)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(31, 50, 75)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 8.5F, FontStyle.Bold)
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
        grid.ColumnHeadersHeight = 48
        grid.RowTemplate.Height = 32
        grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter

        grid.Columns.Add(TextColumn("SortNo", "SIRA", 50, 5, True))
        grid.Columns.Add(TextColumn("SerialNumber", "SERİ NUMARASI", 150, 15))
        grid.Columns.Add(TextColumn("LabelErrorQ3", "ETİKET HATA Q3 (%)", 90, 9))
        grid.Columns.Add(TextColumn("LabelErrorQ2", "ETİKET HATA Q2 (%)", 90, 9))
        grid.Columns.Add(TextColumn("LabelErrorQ1", "ETİKET HATA Q1 (%)", 90, 9))
        grid.Columns.Add(TextColumn("TestFlowQ4Manual", "TEST HATA Q4 MANUEL (%)", 100, 10))
        grid.Columns.Add(TextColumn("TestFlowQ3", "TEST HATA Q3 (%)", 85, 8))
        grid.Columns.Add(TextColumn("TestFlowQ2", "TEST HATA Q2 (%)", 85, 8))
        grid.Columns.Add(TextColumn("TestFlowQ1", "TEST HATA Q1 (%)", 85, 8))
        grid.Columns.Add(ResultColumn("CreditResult", "KREDİ TESTİ"))
        grid.Columns.Add(ResultColumn("ValveResult", "VANA TESTİ"))
        grid.Columns.Add(TextColumn("OverallResult", "SATIR DURUMU", 115, 11, True))

        AddHandler grid.CellValueChanged, AddressOf Grid_CellValueChanged
        AddHandler grid.CurrentCellDirtyStateChanged,
            Sub()
                If grid.IsCurrentCellDirty Then grid.CommitEdit(DataGridViewDataErrorContexts.Commit)
            End Sub
        AddHandler grid.DataError, Sub(sender, e) e.ThrowException = False
    End Sub

    Private Function BuildFooter() As Control
        Dim footer As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .BackColor = Color.White,
            .Padding = New Padding(8, 10, 8, 6),
            .Margin = New Padding(0, 8, 0, 0)
        }
        footer.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        footer.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))

        Dim leftActions As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False, .Margin = New Padding(0)}
        ConfigureButton(btnAddRow, "Sayaç Satırı Ekle", 145, Color.FromArgb(232, 242, 255), Color.FromArgb(31, 71, 126))
        ConfigureButton(btnRemoveRow, "Seçili Satırı Sil", 145, Color.MistyRose, Color.DarkRed)
        AddHandler btnAddRow.Click, Sub() AddBlankLine()
        AddHandler btnRemoveRow.Click, AddressOf RemoveLine_Click
        leftActions.Controls.AddRange({btnAddRow, btnRemoveRow})
        footer.Controls.Add(leftActions, 0, 0)

        Dim rightActions As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.RightToLeft, .WrapContents = False, .Margin = New Padding(0)}
        Dim btnClose As New Button()
        ConfigureButton(btnClose, "Kapat", 105, Color.White, Color.FromArgb(35, 50, 70))
        btnClose.DialogResult = DialogResult.Cancel
        ConfigureButton(btnSaveDraft, "Taslağı Kaydet ve Kapat", 190, Color.FromArgb(232, 242, 255), Color.FromArgb(31, 71, 126))
        ConfigureButton(btnComplete, "Kontrolü Tamamla", 160, Color.FromArgb(220, 245, 226), Color.DarkGreen)
        AddHandler btnSaveDraft.Click, Sub() SaveRecord(False)
        AddHandler btnComplete.Click, Sub() SaveRecord(True)
        rightActions.Controls.AddRange({btnClose, btnComplete, btnSaveDraft})
        footer.Controls.Add(rightActions, 1, 0)
        CancelButton = btnClose
        Return footer
    End Function

    Private Sub LoadRecord()
        isLoading = True
        Try
            If isNew Then
                lblStatus.Text = ""
                txtControlDate.Text = "İlk kaydetmede otomatik atanacak"
                txtControllerName.Text = AppState.CurrentUserName
                chkSmartMeter.Checked = False
                For index As Integer = 1 To 7
                    AddBlankLine()
                Next
                Return
            End If

            _AffectedControlId = ValueOf("ControlId")
            lblStatus.Text = StatusDisplay(ValueOf("Status")) & "  |  " & AffectedControlId
            txtMeterModel.Text = ValueOf("MeterModel")
            txtPulseCount.Text = ValueOf("PulseCount")
            txtCustomer.Text = ValueOf("Customer")
            Dim controlDate As DateTime
            If DateTime.TryParse(ValueOf("ControlDate"), controlDate) Then
                txtControlDate.Text = controlDate.ToString("dd.MM.yyyy HH:mm")
            Else
                txtControlDate.Text = "-"
            End If
            txtOperatorInfo.Text = ValueOf("OperatorInfo")
            txtControllerName.Text = ValueOf("ControllerName")
            txtProductionPanelNo.Text = ValueOf("ProductionPanelNo")
            txtControlPanelNo.Text = ValueOf("ControlPanelNo")
            chkSmartMeter.Checked = String.Equals(ValueOf("IsSmartMeter"), "YES", StringComparison.OrdinalIgnoreCase)
            txtReferenceFlowQ4.Text = ValueOf("ReferenceFlowQ4")
            txtReferenceFlowQ3.Text = ValueOf("ReferenceFlowQ3")
            txtReferenceFlowQ2.Text = ValueOf("ReferenceFlowQ2")
            txtReferenceFlowQ1.Text = ValueOf("ReferenceFlowQ1")
            txtExplanation.Text = ValueOf("Explanation")

            For Each line In DataService.GetPackageMeterControlLines(AffectedControlId)
                AddLineToGrid(line)
            Next
            If DataLineCount() = 0 AndAlso Not IsCompleted() Then
                For index As Integer = 1 To 7
                    AddBlankLine()
                Next
            End If
        Finally
            isLoading = False
            ApplySmartMeterColumns()
            UpdateRangeDisplay()
            RefreshGridStatus()
        End Try
    End Sub

    Private Sub ApplyPermissions()
        Dim canEdit = CanEditCurrentRecord()
        For Each control In {DirectCast(txtMeterModel, Control), txtPulseCount, txtCustomer, txtOperatorInfo,
                             txtControllerName, txtProductionPanelNo, txtControlPanelNo, chkSmartMeter, txtExplanation}
            control.Enabled = canEdit
        Next
        grid.ReadOnly = Not canEdit
        btnAddRow.Visible = canEdit
        btnRemoveRow.Visible = canEdit
        btnSaveDraft.Visible = canEdit AndAlso Not IsCompleted()
        btnComplete.Visible = canEdit
        btnComplete.Text = If(IsCompleted(), "Değişiklikleri Kaydet", "Kontrolü Tamamla")
        ApplySmartMeterColumns()
        UpdateRangeDisplay()
    End Sub

    Private Sub SmartMeter_CheckedChanged(sender As Object, e As EventArgs)
        If Not isLoading AndAlso Not chkSmartMeter.Checked Then
            For Each row As DataGridViewRow In grid.Rows
                row.Cells("CreditResult").Value = ""
                row.Cells("ValveResult").Value = ""
            Next
        End If
        ApplySmartMeterColumns()
        RefreshGridStatus()
    End Sub

    Private Sub ApplySmartMeterColumns()
        Dim editable = CanEditCurrentRecord()
        For Each row As DataGridViewRow In grid.Rows
            For Each columnName In {"CreditResult", "ValveResult"}
                Dim cell = row.Cells(columnName)
                cell.ReadOnly = Not editable OrElse Not chkSmartMeter.Checked
                cell.Style.BackColor = If(chkSmartMeter.Checked, Color.Empty, Color.FromArgb(238, 240, 243))
                cell.Style.ForeColor = If(chkSmartMeter.Checked, Color.Empty, Color.Gray)
            Next
        Next
        ApplyReferenceFlowPermissions(editable)
    End Sub

    Private Sub ApplyReferenceFlowPermissions(editable As Boolean)
        For Each textBox In {txtReferenceFlowQ4, txtReferenceFlowQ3, txtReferenceFlowQ2, txtReferenceFlowQ1}
            textBox.ReadOnly = Not editable
            textBox.TabStop = editable
        Next
    End Sub

    Private Sub Grid_CellValueChanged(sender As Object, e As DataGridViewCellEventArgs)
        If isLoading OrElse e.RowIndex < 0 Then Return
        RefreshGridStatus()
    End Sub

    Private Sub ReferenceFlow_TextChanged(sender As Object, e As EventArgs)
        If isLoading Then Return
        UpdateRangeDisplay()
        RefreshGridStatus()
    End Sub

    Private Sub RefreshGridStatus()
        For Each row As DataGridViewRow In grid.Rows
            Dim result = CalculateGridRowResult(row)
            row.Cells("OverallResult").Value = result
            row.Cells("OverallResult").ToolTipText = BuildFlowDeviationTooltip(row)
            Dim hasData = RowHasData(row)
            ApplyGridRowColor(row, result, hasData)
        Next
    End Sub

    Private Sub UpdateRangeDisplay()
        If isUpdatingRange Then Return
        isUpdatingRange = True
        Try
            ResetReferenceFlowStyles()
            Dim rangeValue As Decimal
            Dim expectedQ4 As Decimal
            Dim expectedQ2 As Decimal
            Dim rangeMatches As Boolean
            Dim q4Matches As Boolean
            Dim q2Matches As Boolean
            Dim rangeText = "—"
            Dim q3Value As Decimal
            Dim q1Value As Decimal
            Dim hasNumericRange = TryParsePackageMeterPercent(txtReferenceFlowQ3.Text.Trim(), q3Value) AndAlso
                                  TryParsePackageMeterPercent(txtReferenceFlowQ1.Text.Trim(), q1Value) AndAlso
                                  q3Value > 0D AndAlso q1Value > 0D
            If DataService.TryResolvePackageMeterRange(txtReferenceFlowQ3.Text.Trim(),
                                                       txtReferenceFlowQ1.Text.Trim(),
                                                       rangeValue) Then
                rangeMatches = True
                rangeText = rangeValue.ToString("0", CultureInfo.GetCultureInfo("tr-TR"))
            ElseIf hasNumericRange Then
                rangeText = "GEÇERSİZ"
            End If

            Dim referencesValid = DataService.EvaluatePackageMeterReferenceFlows(txtReferenceFlowQ4.Text.Trim(),
                                                                                   txtReferenceFlowQ3.Text.Trim(),
                                                                                   txtReferenceFlowQ2.Text.Trim(),
                                                                                   txtReferenceFlowQ1.Text.Trim(),
                                                                                   rangeValue,
                                                                                   expectedQ4,
                                                                                   expectedQ2,
                                                                                   rangeMatches,
                                                                                   q4Matches,
                                                                                   q2Matches)
            If referencesValid Then
                ApplyReferenceFlowResult(txtReferenceFlowQ4, q4Matches, "Q4", expectedQ4)
                ApplyReferenceFlowResult(txtReferenceFlowQ2, q2Matches, "Q2", expectedQ2)
                txtReferenceFlowQ3.BackColor = Color.FromArgb(220, 245, 226)
                If rangeMatches Then
                    txtReferenceFlowQ1.BackColor = Color.FromArgb(220, 245, 226)
                    txtReferenceFlowQ1.ForeColor = Color.DarkGreen
                Else
                    txtReferenceFlowQ1.BackColor = Color.MistyRose
                    txtReferenceFlowQ1.ForeColor = Color.DarkRed
                    referenceToolTip.SetToolTip(txtReferenceFlowQ1,
                                                "Q3 / Q1 oranı standart R değerlerinden birini vermelidir: " &
                                                DataService.PackageMeterAllowedRangeDisplay)
                End If
                If rangeMatches AndAlso q4Matches AndAlso q2Matches Then
                    lblRangeValue.BackColor = Color.FromArgb(220, 245, 226)
                    lblRangeValue.ForeColor = Color.DarkGreen
                    referenceToolTip.SetToolTip(lblRangeValue, "Referans debileri Q3 / R ilişkisiyle uyumlu.")
                Else
                    lblRangeValue.BackColor = Color.MistyRose
                    lblRangeValue.ForeColor = Color.DarkRed
                    referenceToolTip.SetToolTip(lblRangeValue,
                                                "R şu değerlerden biri olmalıdır: " &
                                                DataService.PackageMeterAllowedRangeDisplay &
                                                ". Q4 ve Q2 de seçilen R ile uyumlu olmalıdır.")
                End If
            ElseIf rangeMatches Then
                lblRangeValue.BackColor = Color.FromArgb(232, 243, 255)
                lblRangeValue.ForeColor = Color.FromArgb(21, 75, 137)
                referenceToolTip.SetToolTip(lblRangeValue, "R = Q3 / Q1. Q4 ve Q2 girildiğinde ilişki kontrol edilir.")
            ElseIf hasNumericRange Then
                lblRangeValue.BackColor = Color.MistyRose
                lblRangeValue.ForeColor = Color.DarkRed
                txtReferenceFlowQ1.BackColor = Color.MistyRose
                txtReferenceFlowQ1.ForeColor = Color.DarkRed
                referenceToolTip.SetToolTip(lblRangeValue,
                                            "R şu standart değerlerden biri olmalıdır: " &
                                            DataService.PackageMeterAllowedRangeDisplay)
            Else
                lblRangeValue.BackColor = Color.FromArgb(232, 243, 255)
                lblRangeValue.ForeColor = Color.FromArgb(21, 75, 137)
                referenceToolTip.SetToolTip(lblRangeValue, "R değeri Q3 / Q1 olarak hesaplanır.")
            End If
            lblRangeValue.Text = "R = " & rangeText
            lblRangeValue.Refresh()
            lblRangeValue.Invalidate()
        Finally
            isUpdatingRange = False
        End Try
    End Sub

    Private Sub ResetReferenceFlowStyles()
        For Each textBox In {txtReferenceFlowQ4, txtReferenceFlowQ3, txtReferenceFlowQ2, txtReferenceFlowQ1}
            textBox.BackColor = Color.FromArgb(255, 249, 219)
            textBox.ForeColor = Color.FromArgb(55, 65, 78)
            referenceToolTip.SetToolTip(textBox, "Pozitif referans debisini girin.")
        Next
    End Sub

    Private Sub ApplyReferenceFlowResult(textBox As TextBox,
                                         matches As Boolean,
                                         flowName As String,
                                         expectedValue As Decimal)
        If matches Then
            textBox.BackColor = Color.FromArgb(220, 245, 226)
            textBox.ForeColor = Color.DarkGreen
            referenceToolTip.SetToolTip(textBox, flowName & " referans debisi hesapla uyumlu.")
        Else
            textBox.BackColor = Color.MistyRose
            textBox.ForeColor = Color.DarkRed
            referenceToolTip.SetToolTip(textBox,
                                        flowName & " yaklaşık " &
                                        expectedValue.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR")) &
                                        " L/saat olmalıdır.")
        End If
    End Sub

    Private Function CalculateGridRowResult(row As DataGridViewRow) As String
        If Not RowHasData(row) Then Return ""

        Dim requiredColumns = {"SerialNumber", "LabelErrorQ3", "LabelErrorQ2", "LabelErrorQ1",
                               "TestFlowQ4Manual", "TestFlowQ3", "TestFlowQ2", "TestFlowQ1"}
        If requiredColumns.Any(Function(name) CellText(row, name) = "") Then Return "EKSİK"

        Dim q3Error As Decimal
        Dim q2Error As Decimal
        Dim q1Error As Decimal
        If Not TryParsePackageMeterPercent(CellText(row, "LabelErrorQ3"), q3Error) OrElse
           Not TryParsePackageMeterPercent(CellText(row, "LabelErrorQ2"), q2Error) OrElse
           Not TryParsePackageMeterPercent(CellText(row, "LabelErrorQ1"), q1Error) Then
            Return "GEÇERSİZ DEĞER"
        End If

        Dim testQ4Value As Decimal
        Dim testQ3Value As Decimal
        Dim testQ2Value As Decimal
        Dim testQ1Value As Decimal
        If Not TryParsePackageMeterPercent(CellText(row, "TestFlowQ4Manual"), testQ4Value) OrElse
           Not TryParsePackageMeterPercent(CellText(row, "TestFlowQ3"), testQ3Value) OrElse
           Not TryParsePackageMeterPercent(CellText(row, "TestFlowQ2"), testQ2Value) OrElse
           Not TryParsePackageMeterPercent(CellText(row, "TestFlowQ1"), testQ1Value) Then
            Return "GEÇERSİZ DEĞER"
        End If

        Dim referenceTexts = {txtReferenceFlowQ4.Text.Trim(),
                              txtReferenceFlowQ3.Text.Trim(),
                              txtReferenceFlowQ2.Text.Trim(),
                              txtReferenceFlowQ1.Text.Trim()}
        If referenceTexts.Any(Function(value) value = "") Then Return "EKSİK"
        Dim rangeValue As Decimal
        Dim expectedQ4 As Decimal
        Dim expectedQ2 As Decimal
        Dim rangeMatches As Boolean
        Dim q4Matches As Boolean
        Dim q2Matches As Boolean
        If Not DataService.EvaluatePackageMeterReferenceFlows(referenceTexts(0),
                                                               referenceTexts(1),
                                                               referenceTexts(2),
                                                               referenceTexts(3),
                                                               rangeValue,
                                                               expectedQ4,
                                                               expectedQ2,
                                                               rangeMatches,
                                                               q4Matches,
                                                               q2Matches) Then Return "GEÇERSİZ DEĞER"
        If Not rangeMatches OrElse Not q4Matches OrElse Not q2Matches Then Return "GEÇERSİZ REFERANS"

        If Decimal.Abs(q3Error) > 2D OrElse Decimal.Abs(q2Error) > 2D OrElse Decimal.Abs(q1Error) > 5D OrElse
           Decimal.Abs(testQ4Value) > 2D OrElse
           Decimal.Abs(testQ3Value) > 2D OrElse Decimal.Abs(testQ2Value) > 2D OrElse Decimal.Abs(testQ1Value) > 5D Then
            Return "UYGUN DEĞİL"
        End If

        If chkSmartMeter.Checked Then
            Dim credit = CellText(row, "CreditResult").ToUpperInvariant()
            Dim valve = CellText(row, "ValveResult").ToUpperInvariant()
            If credit = "UYGUN DEĞİL" OrElse valve = "UYGUN DEĞİL" Then Return "UYGUN DEĞİL"
            If credit = "UYGUN" AndAlso valve = "UYGUN" Then Return "UYGUN"
            Return "EKSİK"
        End If
        Return "UYGUN"
    End Function

    Private Function BuildFlowDeviationTooltip(row As DataGridViewRow) As String
        If Not RowHasData(row) Then Return ""
        Dim testQ4 As Decimal
        Dim testQ3 As Decimal
        Dim testQ2 As Decimal
        Dim testQ1 As Decimal
        If Not TryParsePackageMeterPercent(CellText(row, "TestFlowQ4Manual"), testQ4) OrElse
           Not TryParsePackageMeterPercent(CellText(row, "TestFlowQ3"), testQ3) OrElse
           Not TryParsePackageMeterPercent(CellText(row, "TestFlowQ2"), testQ2) OrElse
           Not TryParsePackageMeterPercent(CellText(row, "TestFlowQ1"), testQ1) Then Return ""

        Return "Test hata değerleri: Q4 %" & testQ4.ToString("0.##") &
               " | Q3 %" & testQ3.ToString("0.##") &
               " | Q2 %" & testQ2.ToString("0.##") &
               " | Q1 %" & testQ1.ToString("0.##")
    End Function

    Private Shared Function TryParsePackageMeterPercent(value As String, ByRef parsedValue As Decimal) As Boolean
        Dim text = If(value, "").Trim().Replace("%", "").Replace(" ", "")
        If text = "" Then Return False
        If Decimal.TryParse(text, NumberStyles.Float, CultureInfo.GetCultureInfo("tr-TR"), parsedValue) Then Return True
        Return Decimal.TryParse(text.Replace(","c, "."c), NumberStyles.Float, CultureInfo.InvariantCulture, parsedValue)
    End Function

    Private Sub ApplyGridRowColor(row As DataGridViewRow, result As String, hasData As Boolean)
        row.DefaultCellStyle.BackColor = Color.White
        row.DefaultCellStyle.ForeColor = Color.FromArgb(55, 65, 78)
        For Each cell As DataGridViewCell In row.Cells
            cell.Style.BackColor = Color.White
            cell.Style.ForeColor = Color.FromArgb(55, 65, 78)
            cell.Style.Font = grid.Font
        Next

        If Not chkSmartMeter.Checked Then
            For Each columnName In {"CreditResult", "ValveResult"}
                row.Cells(columnName).Style.BackColor = Color.FromArgb(238, 240, 243)
                row.Cells(columnName).Style.ForeColor = Color.Gray
            Next
        End If
        If Not hasData Then Return

        Dim statusCell = row.Cells("OverallResult")
        statusCell.Style.Font = grid.ColumnHeadersDefaultCellStyle.Font
        If result = "UYGUN DEĞİL" Then
            statusCell.Style.BackColor = Color.MistyRose
            statusCell.Style.ForeColor = Color.DarkRed
        ElseIf result = "UYGUN" OrElse result = "KONTROL EDİLDİ" Then
            statusCell.Style.ForeColor = Color.DarkGreen
        ElseIf result = "GEÇERSİZ DEĞER" OrElse result = "GEÇERSİZ REFERANS" Then
            statusCell.Style.BackColor = Color.FromArgb(255, 228, 181)
            statusCell.Style.ForeColor = Color.DarkRed
        Else
            statusCell.Style.BackColor = Color.LemonChiffon
            statusCell.Style.ForeColor = Color.FromArgb(112, 71, 0)
        End If

        HighlightInvalidCells(row)
    End Sub

    Private Sub HighlightInvalidCells(row As DataGridViewRow)
        Dim requiredColumns = {"SerialNumber", "LabelErrorQ3", "LabelErrorQ2", "LabelErrorQ1",
                               "TestFlowQ4Manual", "TestFlowQ3", "TestFlowQ2", "TestFlowQ1"}
        For Each columnName In requiredColumns
            If CellText(row, columnName) = "" Then MarkMissingCell(row.Cells(columnName))
        Next

        HighlightLimitCell(row, "LabelErrorQ3", 2D)
        HighlightLimitCell(row, "LabelErrorQ2", 2D)
        HighlightLimitCell(row, "LabelErrorQ1", 5D)

        HighlightLimitCell(row, "TestFlowQ4Manual", 2D)
        HighlightLimitCell(row, "TestFlowQ3", 2D)
        HighlightLimitCell(row, "TestFlowQ2", 2D)
        HighlightLimitCell(row, "TestFlowQ1", 5D)

        If chkSmartMeter.Checked Then
            For Each columnName In {"CreditResult", "ValveResult"}
                If String.Equals(CellText(row, columnName), "UYGUN DEĞİL", StringComparison.OrdinalIgnoreCase) Then
                    MarkUnsuitableCell(row.Cells(columnName))
                ElseIf CellText(row, columnName) = "" Then
                    MarkMissingCell(row.Cells(columnName))
                End If
            Next
        End If
    End Sub

    Private Sub HighlightLimitCell(row As DataGridViewRow, columnName As String, limitValue As Decimal)
        Dim text = CellText(row, columnName)
        If text = "" Then Return
        Dim value As Decimal
        If Not TryParsePackageMeterPercent(text, value) Then
            MarkInvalidValueCell(row.Cells(columnName))
        ElseIf Decimal.Abs(value) > limitValue Then
            MarkUnsuitableCell(row.Cells(columnName))
        End If
    End Sub

    Private Shared Sub MarkUnsuitableCell(cell As DataGridViewCell)
        cell.Style.BackColor = Color.MistyRose
        cell.Style.ForeColor = Color.DarkRed
    End Sub

    Private Shared Sub MarkInvalidValueCell(cell As DataGridViewCell)
        cell.Style.BackColor = Color.FromArgb(255, 228, 181)
        cell.Style.ForeColor = Color.DarkRed
    End Sub

    Private Shared Sub MarkMissingCell(cell As DataGridViewCell)
        cell.Style.BackColor = Color.LemonChiffon
        cell.Style.ForeColor = Color.FromArgb(112, 71, 0)
    End Sub

    Private Sub AddBlankLine()
        Dim line As New PackageMeterControlLine With {.SortNo = DataLineCount() + 1}
        AddLineToGrid(line)
        If Not isLoading Then
            ApplySmartMeterColumns()
            RefreshGridStatus()
        End If
    End Sub

    Private Sub AddLineToGrid(line As PackageMeterControlLine)
        Dim index = grid.Rows.Add(line.SortNo, line.SerialNumber, line.LabelErrorQ3, line.LabelErrorQ2, line.LabelErrorQ1,
                                  line.TestFlowQ4Manual, line.TestFlowQ3, line.TestFlowQ2, line.TestFlowQ1,
                                  line.CreditResult, line.ValveResult, line.OverallResult)
        grid.Rows(index).Tag = line
    End Sub

    Private Sub RemoveLine_Click(sender As Object, e As EventArgs)
        If grid.CurrentRow Is Nothing Then Return
        grid.Rows.Remove(grid.CurrentRow)
        RenumberRows()
        RefreshGridStatus()
    End Sub

    Private Sub RenumberRows()
        Dim sortNo As Integer = 0
        For Each row As DataGridViewRow In grid.Rows
            sortNo += 1
            row.Cells("SortNo").Value = sortNo
        Next
    End Sub

    Private Sub SaveRecord(completeRecord As Boolean)
        Try
            grid.EndEdit()
            Dim previousUnsuitableKeys As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            If AffectedControlId <> "" Then
                For Each line In DataService.GetPackageMeterControlLines(AffectedControlId)
                    If String.Equals(If(line.OverallResult, "").Trim(), "UYGUN DEĞİL", StringComparison.OrdinalIgnoreCase) Then
                        previousUnsuitableKeys.Add(PackageMeterLineIdentity(line))
                    End If
                Next
            End If
            Dim saveAsCompleted = completeRecord OrElse (IsCompleted() AndAlso AppState.IsAdmin)
            Dim header As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                {"ControlId", AffectedControlId},
                {"MeterModel", txtMeterModel.Text.Trim()},
                {"PulseCount", txtPulseCount.Text.Trim()},
                {"Customer", txtCustomer.Text.Trim()},
                {"ControlDate", If(isNew, "", ValueOf("ControlDate"))},
                {"OperatorInfo", txtOperatorInfo.Text.Trim()},
                {"ControllerName", txtControllerName.Text.Trim()},
                {"ProductionPanelNo", txtProductionPanelNo.Text.Trim()},
                {"ControlPanelNo", txtControlPanelNo.Text.Trim()},
                {"IsSmartMeter", If(chkSmartMeter.Checked, "YES", "NO")},
                {"ReferenceFlowQ4", txtReferenceFlowQ4.Text.Trim()},
                {"ReferenceFlowQ3", txtReferenceFlowQ3.Text.Trim()},
                {"ReferenceFlowQ2", txtReferenceFlowQ2.Text.Trim()},
                {"ReferenceFlowQ1", txtReferenceFlowQ1.Text.Trim()},
                {"Explanation", txtExplanation.Text.Trim()}
            }
            Dim savedId = DataService.SavePackageMeterControl(header, ReadGridLines(), saveAsCompleted)
            _AffectedControlId = savedId
            recordChanged = True

            Dim newlyUnsuitableLines = DataService.GetPackageMeterControlLines(savedId).
                Where(Function(line)
                          Return String.Equals(If(line.OverallResult, "").Trim(), "UYGUN DEĞİL", StringComparison.OrdinalIgnoreCase) AndAlso
                                 Not previousUnsuitableKeys.Contains(PackageMeterLineIdentity(line))
                      End Function).
                ToList()
            If newlyUnsuitableLines.Count > 0 Then
                Dim mailError As String = ""
                If Not PackageMeterEmailNotificationService.TryNotifyNewUnsuitableMeters(savedId, newlyUnsuitableLines, mailError) AndAlso
                   mailError <> "" Then
                    AppNotificationService.ShowWarning("Uygun değil sayaç maili gönderilemedi", mailError)
                End If
            End If

            DialogResult = DialogResult.OK
            Close()
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, If(completeRecord OrElse IsCompleted(), "Kontrol kaydedilemedi", "Taslak kaydedilemedi"), MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Shared Function PackageMeterLineIdentity(line As PackageMeterControlLine) As String
        If line Is Nothing Then Return ""
        Dim lineId = If(line.LineId, "").Trim()
        If lineId <> "" Then Return "ID:" & lineId
        Dim serialNumber = If(line.SerialNumber, "").Trim()
        If serialNumber <> "" Then Return "SERIAL:" & serialNumber
        Return "SORT:" & line.SortNo.ToString(CultureInfo.InvariantCulture)
    End Function

    Private Function ReadGridLines() As List(Of PackageMeterControlLine)
        Dim lines As New List(Of PackageMeterControlLine)()
        For Each row As DataGridViewRow In grid.Rows
            Dim existingLine = TryCast(row.Tag, PackageMeterControlLine)
            lines.Add(New PackageMeterControlLine With {
                .LineId = If(existingLine Is Nothing, "", existingLine.LineId),
                .SortNo = lines.Count + 1,
                .SerialNumber = CellText(row, "SerialNumber"),
                .LabelErrorQ3 = CellText(row, "LabelErrorQ3"),
                .LabelErrorQ2 = CellText(row, "LabelErrorQ2"),
                .LabelErrorQ1 = CellText(row, "LabelErrorQ1"),
                .TestFlowQ4Manual = CellText(row, "TestFlowQ4Manual"),
                .TestFlowQ3 = CellText(row, "TestFlowQ3"),
                .TestFlowQ2 = CellText(row, "TestFlowQ2"),
                .TestFlowQ1 = CellText(row, "TestFlowQ1"),
                .CreditResult = CellText(row, "CreditResult"),
                .ValveResult = CellText(row, "ValveResult")
            })
        Next
        Return lines
    End Function

    Private Function RowHasData(row As DataGridViewRow) As Boolean
        Return {"SerialNumber", "LabelErrorQ3", "LabelErrorQ2", "LabelErrorQ1", "TestFlowQ4Manual", "TestFlowQ3", "TestFlowQ2", "TestFlowQ1", "CreditResult", "ValveResult"}.
            Any(Function(name) CellText(row, name) <> "")
    End Function

    Private Function DataLineCount() As Integer
        Return grid.Rows.Count
    End Function

    Private Shared Function CellText(row As DataGridViewRow, columnName As String) As String
        Dim value = row.Cells(columnName).Value
        Return If(value Is Nothing, "", value.ToString().Trim())
    End Function

    Private Function IsCompleted() As Boolean
        Return Not isNew AndAlso String.Equals(ValueOf("Status"), "COMPLETED", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function CanEditCurrentRecord() As Boolean
        If Not AppState.CanModifyPackageMeterControls Then Return False
        Return Not IsCompleted() OrElse AppState.IsAdmin
    End Function

    Private Function ValueOf(key As String) As String
        If sourceRow Is Nothing Then Return ""
        Return DataService.GetValue(sourceRow, key)
    End Function

    Private Shared Function StatusDisplay(value As String) As String
        If String.Equals(If(value, "").Trim(), "COMPLETED", StringComparison.OrdinalIgnoreCase) Then Return "TAMAMLANDI"
        Return "TASLAK"
    End Function

    Private Shared Sub ConfigureTextBox(textBox As TextBox, placeholder As String)
        textBox.Dock = DockStyle.Fill
        textBox.PlaceholderText = placeholder
        textBox.Margin = New Padding(4, 4, 10, 4)
    End Sub

    Private Shared Sub AddField(layout As TableLayoutPanel, labelText As String, control As Control, column As Integer, row As Integer)
        layout.Controls.Add(New Label() With {
            .Text = labelText,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 8.6F, FontStyle.Bold),
            .Padding = New Padding(4, 0, 0, 0)
        }, column, row)
        layout.Controls.Add(control, column + 1, row)
    End Sub

    Private Shared Function TextColumn(name As String, header As String, minimumWidth As Integer, fillWeight As Single, Optional readOnlyValue As Boolean = False) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .HeaderText = header,
            .MinimumWidth = minimumWidth,
            .FillWeight = fillWeight,
            .ReadOnly = readOnlyValue,
            .SortMode = DataGridViewColumnSortMode.NotSortable
        }
    End Function

    Private Shared Function ResultColumn(name As String, header As String) As DataGridViewComboBoxColumn
        Dim column As New DataGridViewComboBoxColumn() With {
            .Name = name,
            .HeaderText = header,
            .MinimumWidth = 105,
            .FillWeight = 10,
            .FlatStyle = FlatStyle.Flat,
            .DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
            .SortMode = DataGridViewColumnSortMode.NotSortable
        }
        column.Items.AddRange({"", "UYGUN", "UYGUN DEĞİL"})
        Return column
    End Function

    Private Shared Sub ConfigureButton(button As Button, text As String, width As Integer, backColor As Color, foreColor As Color)
        button.Text = text
        button.Width = width
        button.Height = 34
        button.BackColor = backColor
        button.ForeColor = foreColor
        button.FlatStyle = FlatStyle.Flat
        button.Font = New Font("Segoe UI", 8.8F, FontStyle.Bold)
        button.Margin = New Padding(6, 0, 0, 0)
        button.Cursor = Cursors.Hand
        button.UseVisualStyleBackColor = False
    End Sub
End Class
