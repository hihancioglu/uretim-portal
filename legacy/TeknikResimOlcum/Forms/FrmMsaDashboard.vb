Imports System.Drawing
Imports System.Globalization
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmMsaDashboard
    Inherits Form

    Private ReadOnly grid As New DataGridView()
    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly cmbStatusFilter As New ComboBox()
    Private ReadOnly lblCount As New Label()
    Private ReadOnly lblMode As New Label()

    Private ReadOnly txtDeviceId As New TextBox()
    Private ReadOnly txtFixedAssetNo As New TextBox()
    Private ReadOnly txtDeviceName As New TextBox()
    Private ReadOnly txtSerialNo As New TextBox()
    Private ReadOnly txtBrand As New TextBox()
    Private ReadOnly txtModel As New TextBox()
    Private ReadOnly cmbDeviceType As New ComboBox()
    Private ReadOnly txtMeasurementRange As New TextBox()
    Private ReadOnly txtResolution As New TextBox()
    Private ReadOnly cmbUnit As New ComboBox()
    Private ReadOnly cmbReferenceDevice As New ComboBox()
    Private ReadOnly cmbUsageStatus As New ComboBox()
    Private ReadOnly txtRegistrationDate As New TextBox()
    Private ReadOnly txtCalibrationPeriodMonths As New TextBox()
    Private ReadOnly dtpCalibrationDate As New DateTimePicker()
    Private ReadOnly dtpCalibrationDueDate As New DateTimePicker()
    Private ReadOnly cmbStatus As New ComboBox()
    Private ReadOnly txtLocation As New TextBox()
    Private ReadOnly txtOrganization As New TextBox()
    Private ReadOnly txtResponsible As New TextBox()
    Private ReadOnly txtNote As New TextBox()
    Private ReadOnly chkIso9001 As New CheckBox()
    Private ReadOnly chkIso45001 As New CheckBox()
    Private ReadOnly chkIso50001 As New CheckBox()
    Private ReadOnly chkIso46001 As New CheckBox()
    Private ReadOnly chkIso17020 As New CheckBox()
    Private ReadOnly chkIso17025 As New CheckBox()

    Private ReadOnly btnSave As New Button()
    Private ReadOnly btnNew As New Button()
    Private ReadOnly btnDelete As New Button()
    Private ReadOnly btnRefresh As New Button()

    Private selectedOriginalDeviceId As String = ""
    Private isNewRecord As Boolean = True
    Private lastAutomaticDeviceId As String = ""
    Private suppressCalibrationDueAutoUpdate As Boolean = False

    Public Sub New()
        AuthorizationService.Require(AppState.CanOpenMsaDashboard, "MSA Dashboard")
        AppIconService.Apply(Me)

        Text = "MSA Dashboard - Ölçüm Cihazları"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        MinimumSize = New Size(1100, 700)
        BackColor = Color.FromArgb(243, 247, 252)
        Font = New Font("Segoe UI", 9.0F)

        BuildScreen()
        LoadGrid()
        New_Click(Me, EventArgs.Empty)
    End Sub

    Private Sub BuildScreen()
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 5,
            .Padding = New Padding(10),
            .BackColor = BackColor
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 56.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 354.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 50.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(root)

        Dim header As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .BackColor = Color.FromArgb(31, 71, 126),
            .Margin = New Padding(0, 0, 0, 8),
            .Padding = New Padding(16, 0, 16, 0)
        }
        header.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        header.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 210.0F))

        Dim title As New Label() With {
            .Dock = DockStyle.Fill,
            .ForeColor = Color.White,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 12.0F, FontStyle.Bold),
            .Text = "MSA Dashboard  |  Ölçüm cihazları tanımlanır ve kalibrasyon durumu izlenir."
        }
        lblMode.Dock = DockStyle.Fill
        lblMode.ForeColor = Color.White
        lblMode.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblMode.TextAlign = ContentAlignment.MiddleRight
        lblMode.Text = If(AppState.CanModifyMsaDashboard, "DÜZENLEME", "SALT OKUNUR")
        header.Controls.Add(title, 0, 0)
        header.Controls.Add(lblMode, 1, 0)
        root.Controls.Add(header, 0, 0)

        Dim editor As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 9,
            .RowCount = 9,
            .BackColor = Color.White,
            .Padding = New Padding(12),
            .Margin = New Padding(0, 0, 0, 8)
        }
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 112.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 24.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 9.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 112.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 24.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 9.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 112.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 24.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 10.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 31.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 31.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 31.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 31.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 31.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 31.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 31.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 31.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.Controls.Add(editor, 0, 1)

        ConfigureTextBox(txtDeviceId, "Bölüm ve cihaz tipine göre otomatik atanır")
        txtDeviceId.ReadOnly = Not AppState.IsAdmin
        txtDeviceId.BackColor = If(AppState.IsAdmin, Color.White, Color.FromArgb(235, 242, 250))
        ConfigureTextBox(txtFixedAssetNo, "Demirbaş no")
        ConfigureTextBox(txtDeviceName, "Ölçüm cihazı tanımı")
        ConfigureTextBox(txtSerialNo, "Seri no")
        ConfigureTextBox(txtBrand, "Marka")
        ConfigureTextBox(txtModel, "Model")
        ConfigureTextBox(txtMeasurementRange, "Örn. 0-150")
        ConfigureTextBox(txtResolution, "Örn. 0,01")
        ConfigureTextBox(txtLocation, "Kullanıldığı bölüm")
        ConfigureTextBox(txtRegistrationDate, "İlk kayıt tarihi")
        ConfigureTextBox(txtCalibrationPeriodMonths, "Ay")
        ConfigureTextBox(txtOrganization, "Kalibrasyon/doğrulama kuruluşu")
        ConfigureTextBox(txtResponsible, "Sorumlu kişi")
        ConfigureMultiline(txtNote, "Açıklama")
        ConfigureCombo(cmbDeviceType, {"Kumpas", "Mikrometre", "Mihengir", "Komparatör", "Fikstür", "Test Cihazı", "Diğer"})
        ConfigureCombo(cmbUnit, {"mm", "cm", "inch", "g", "kg", "N", "Nm", "L/saat", "Diğer"})
        ConfigureCombo(cmbReferenceDevice, {"HAYIR", "EVET"})
        ConfigureCombo(cmbUsageStatus, {"KULLANIMDA", "KULLANIM DIŞI", "YEDEK", "ARIZALI"})
        ConfigureCombo(cmbStatus, {"AKTİF", "KALİBRASYON BEKLİYOR", "KULLANIM DIŞI"})
        AddHandler cmbDeviceType.SelectedIndexChanged, Sub() RefreshAutomaticDeviceId()
        AddHandler txtLocation.Leave, Sub() RefreshAutomaticDeviceId()
        ConfigureDate(dtpCalibrationDate)
        ConfigureDate(dtpCalibrationDueDate)
        AddHandler dtpCalibrationDate.ValueChanged, Sub() UpdateCalibrationDueDateFromPeriod()
        AddHandler txtCalibrationPeriodMonths.TextChanged, Sub() UpdateCalibrationDueDateFromPeriod()
        ConfigureCheckBox(chkIso9001, "ISO 9001")
        ConfigureCheckBox(chkIso45001, "ISO 45001")
        ConfigureCheckBox(chkIso50001, "ISO 50001")
        ConfigureCheckBox(chkIso46001, "ISO 46001")
        ConfigureCheckBox(chkIso17020, "ISO 17020")
        ConfigureCheckBox(chkIso17025, "ISO 17025")

        Dim standardsPanel As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .AutoScroll = True,
            .Margin = New Padding(5, 2, 5, 2),
            .BackColor = Color.White
        }
        standardsPanel.Controls.AddRange(New Control() {chkIso9001, chkIso45001, chkIso50001, chkIso46001, chkIso17020, chkIso17025})

        AddField(editor, "Cihaz No (Otomatik)", txtDeviceId, 0, 0)
        AddField(editor, "Demirbaş No", txtFixedAssetNo, 3, 0)
        AddField(editor, "Ölçüm Cihazı Tanımı", txtDeviceName, 6, 0)
        AddField(editor, "Seri No", txtSerialNo, 0, 1)
        AddField(editor, "Marka", txtBrand, 3, 1)
        AddField(editor, "Model", txtModel, 6, 1)
        AddField(editor, "Tip", cmbDeviceType, 0, 2)
        AddField(editor, "Ölçüm Aralığı", txtMeasurementRange, 3, 2)
        AddField(editor, "Çözünürlük", txtResolution, 6, 2)
        AddField(editor, "Birim", cmbUnit, 0, 3)
        AddField(editor, "Kullanıldığı Bölüm", txtLocation, 3, 3)
        AddField(editor, "Referans Cihaz mı?", cmbReferenceDevice, 6, 3)
        AddField(editor, "Kullanım Durumu", cmbUsageStatus, 0, 4)
        AddField(editor, "Durum", cmbStatus, 3, 4)
        AddField(editor, "Periyot (Ay)", txtCalibrationPeriodMonths, 6, 4)
        AddField(editor, "Son Kalibrasyon", dtpCalibrationDate, 0, 5)
        AddField(editor, "Gelecek Kalibrasyon", dtpCalibrationDueDate, 3, 5)
        AddField(editor, "Kuruluş", txtOrganization, 6, 5)
        AddField(editor, "İlk Kayıt Tarihi", txtRegistrationDate, 0, 6)
        AddField(editor, "Sorumlu", txtResponsible, 3, 6)
        AddField(editor, "İlgili Standart Kapsamı", standardsPanel, 0, 7)
        editor.SetColumnSpan(standardsPanel, 8)
        AddField(editor, "Açıklama", txtNote, 0, 8)
        editor.SetColumnSpan(txtNote, 8)

        Dim actions As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .BackColor = Color.White,
            .Margin = New Padding(0, 0, 0, 6),
            .Padding = New Padding(8, 8, 8, 8)
        }
        ConfigureButton(btnSave, "Yeni Cihazı Kaydet", 190, Color.FromArgb(31, 71, 126), Color.White)
        ConfigureButton(btnNew, "Yeni", 90, Color.White, Color.FromArgb(35, 50, 70))
        ConfigureButton(btnDelete, "Seçili Cihazı Sil", 150, Color.MistyRose, Color.DarkRed)
        ConfigureButton(btnRefresh, "Yenile", 90, Color.White, Color.FromArgb(35, 50, 70))
        AddHandler btnSave.Click, AddressOf Save_Click
        AddHandler btnNew.Click, AddressOf New_Click
        AddHandler btnDelete.Click, AddressOf Delete_Click
        AddHandler btnRefresh.Click, Sub() LoadGrid()
        actions.Controls.AddRange({btnSave, btnNew, btnDelete, btnRefresh})
        root.Controls.Add(actions, 0, 2)

        Dim filters As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 5,
            .RowCount = 1,
            .BackColor = Color.White,
            .Padding = New Padding(10, 4, 10, 4),
            .Margin = New Padding(0, 0, 0, 6)
        }
        filters.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 64.0F))
        filters.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        filters.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 64.0F))
        filters.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 180.0F))
        filters.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 330.0F))
        Dim lblSearch As New Label() With {.Text = "Arama", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)}
        Dim lblStatus As New Label() With {.Text = "Durum", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)}
        ConfigureTextBox(txtSearch, "cihaz no / demirbaş / tanım / seri / marka / model / bölüm")
        ConfigureCombo(cmbStatusFilter, {"TÜMÜ", "AKTİF", "KALİBRASYON BEKLİYOR", "KULLANIM DIŞI"})
        cmbStatusFilter.SelectedIndex = 0
        AddHandler txtSearch.TextChanged, Sub() LoadGrid()
        AddHandler cmbStatusFilter.SelectedIndexChanged, Sub() LoadGrid()
        filters.Controls.Add(lblSearch, 0, 0)
        filters.Controls.Add(txtSearch, 1, 0)
        filters.Controls.Add(lblStatus, 2, 0)
        filters.Controls.Add(cmbStatusFilter, 3, 0)
        lblCount.Dock = DockStyle.Fill
        lblCount.TextAlign = ContentAlignment.MiddleRight
        lblCount.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblCount.ForeColor = Color.FromArgb(31, 71, 126)
        filters.Controls.Add(lblCount, 4, 0)
        root.Controls.Add(filters, 0, 3)

        ConfigureGrid()
        root.Controls.Add(grid, 0, 4)

        SetEditorEnabled(AppState.CanModifyMsaDashboard)
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
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(220, 232, 247)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(31, 50, 75)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.ColumnHeadersHeight = 38
        grid.RowTemplate.Height = 30

        grid.Columns.Add(MakeColumn("DeviceId", "CİHAZ NO", 110, 12))
        grid.Columns.Add(MakeColumn("FixedAssetNo", "DEMİRBAŞ NO", 120, 10))
        grid.Columns.Add(MakeColumn("StdIso9001", "ISO 9001", 80, 6))
        grid.Columns.Add(MakeColumn("StdIso45001", "ISO 45001", 80, 6))
        grid.Columns.Add(MakeColumn("StdIso50001", "ISO 50001", 80, 6))
        grid.Columns.Add(MakeColumn("StdIso46001", "ISO 46001", 80, 6))
        grid.Columns.Add(MakeColumn("StdIso17020", "ISO 17020", 80, 6))
        grid.Columns.Add(MakeColumn("StdIso17025", "ISO 17025", 80, 6))
        grid.Columns.Add(MakeColumn("DeviceName", "ÖLÇÜM CİHAZI TANIMI", 230, 22))
        grid.Columns.Add(MakeColumn("SerialNo", "SERİ NO", 120, 10))
        grid.Columns.Add(MakeColumn("Brand", "MARKA", 110, 10))
        grid.Columns.Add(MakeColumn("Model", "MODEL", 110, 10))
        grid.Columns.Add(MakeColumn("DeviceType", "TİP", 120, 10))
        grid.Columns.Add(MakeColumn("MeasurementRange", "ÖLÇÜM ARALIĞI", 130, 10))
        grid.Columns.Add(MakeColumn("Resolution", "ÇÖZÜNÜRLÜK", 110, 9))
        grid.Columns.Add(MakeColumn("Location", "KULLANILDIĞI BÖLÜM", 150, 12))
        grid.Columns.Add(MakeColumn("ReferenceDevice", "REFERANS CİHAZ MI?", 140, 10))
        grid.Columns.Add(MakeColumn("UsageStatus", "KULLANIM DURUMU", 145, 11))
        grid.Columns.Add(MakeColumn("RegistrationDate", "İLK KAYIT TARİHİ", 130, 10))
        grid.Columns.Add(MakeColumn("Note", "AÇIKLAMA", 180, 14))
        grid.Columns.Add(MakeColumn("Status", "DURUM", 150, 13))
        grid.Columns.Add(MakeColumn("CalibrationPeriodMonths", "PERİYOT (AY)", 110, 9))
        grid.Columns.Add(MakeColumn("CalibrationDate", "SON KALİBRASYON / DOĞRULAMA", 190, 14))
        grid.Columns.Add(MakeColumn("CalibrationDueDate", "GELECEK KALİBRASYON-DOĞRULAMA", 210, 14))
        grid.Columns.Add(MakeColumn("Organization", "KURULUŞ", 150, 12))
        grid.Columns.Add(MakeColumn("Responsible", "SORUMLU", 130, 11))
        grid.Columns.Add(MakeColumn("UpdatedAt", "GÜNCELLEME", 135, 12))

        AddHandler grid.CellClick,
            Sub(sender, e)
                If e.RowIndex >= 0 Then LoadSelectedRow()
            End Sub
        AddHandler grid.CellDoubleClick,
            Sub(sender, e)
                If e.RowIndex >= 0 Then LoadSelectedRow()
            End Sub
    End Sub

    Private Sub LoadGrid()
        Try
            Dim rows = DataService.GetMeasurementDevices()
            Dim searchText = txtSearch.Text.Trim().ToUpperInvariant()
            Dim statusFilter = If(cmbStatusFilter.SelectedItem, "TÜMÜ").ToString()

            If statusFilter <> "TÜMÜ" Then
                rows = rows.Where(Function(row) String.Equals(DataService.GetValue(row, "Status"), statusFilter, StringComparison.OrdinalIgnoreCase)).ToList()
            End If
            If searchText <> "" Then
                rows = rows.Where(
                    Function(row)
                        Dim haystack = String.Join(" ", {
                            DataService.GetValue(row, "DeviceId"),
                            DataService.GetValue(row, "FixedAssetNo"),
                            DataService.GetValue(row, "DeviceName"),
                            DataService.GetValue(row, "SerialNo"),
                            DataService.GetValue(row, "Brand"),
                            DataService.GetValue(row, "Model"),
                            DataService.GetValue(row, "DeviceType"),
                            DataService.GetValue(row, "MeasurementRange"),
                            DataService.GetValue(row, "Location"),
                            DataService.GetValue(row, "Organization"),
                            DataService.GetValue(row, "Responsible"),
                            DataService.GetValue(row, "Note")
                        }).ToUpperInvariant()
                        Return haystack.Contains(searchText)
                    End Function).ToList()
            End If

            grid.Rows.Clear()
            For Each item In rows
                Dim index = grid.Rows.Add(
                    DataService.GetValue(item, "DeviceId"),
                    DataService.GetValue(item, "FixedAssetNo"),
                    FormatYesNoDisplay(DataService.GetValue(item, "StdIso9001")),
                    FormatYesNoDisplay(DataService.GetValue(item, "StdIso45001")),
                    FormatYesNoDisplay(DataService.GetValue(item, "StdIso50001")),
                    FormatYesNoDisplay(DataService.GetValue(item, "StdIso46001")),
                    FormatYesNoDisplay(DataService.GetValue(item, "StdIso17020")),
                    FormatYesNoDisplay(DataService.GetValue(item, "StdIso17025")),
                    DataService.GetValue(item, "DeviceName"),
                    DataService.GetValue(item, "SerialNo"),
                    DataService.GetValue(item, "Brand"),
                    DataService.GetValue(item, "Model"),
                    DataService.GetValue(item, "DeviceType"),
                    DataService.GetValue(item, "MeasurementRange"),
                    DataService.GetValue(item, "Resolution"),
                    DataService.GetValue(item, "Location"),
                    FormatYesNoDisplay(DataService.GetValue(item, "ReferenceDevice")),
                    DataService.GetValue(item, "UsageStatus"),
                    FormatDateDisplay(DataService.GetValue(item, "RegistrationDate")),
                    DataService.GetValue(item, "Note"),
                    DataService.GetValue(item, "Status"),
                    DataService.GetValue(item, "CalibrationPeriodMonths"),
                    FormatDateDisplay(DataService.GetValue(item, "CalibrationDate")),
                    FormatDateDisplay(DataService.GetValue(item, "CalibrationDueDate")),
                    DataService.GetValue(item, "Organization"),
                    DataService.GetValue(item, "Responsible"),
                    DataService.GetValue(item, "UpdatedAt"))
                Dim gridRow = grid.Rows(index)
                gridRow.Tag = item
                ApplyRowStyle(gridRow, item)
            Next

            Dim dueSoon = rows.Where(Function(row) IsDueSoon(DataService.GetValue(row, "CalibrationDueDate"))).Count()
            Dim expired = rows.Where(Function(row) IsExpired(DataService.GetValue(row, "CalibrationDueDate"))).Count()
            lblCount.Text = "Cihaz: " & rows.Count.ToString() &
                            "   |   Yaklaşan kalibrasyon: " & dueSoon.ToString() &
                            "   |   Süresi geçen: " & expired.ToString()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "MSA cihaz listesi yüklenemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub LoadSelectedRow()
        If grid.CurrentRow Is Nothing Then Return
        Dim row = TryCast(grid.CurrentRow.Tag, Dictionary(Of String, String))
        If row Is Nothing Then Return

        isNewRecord = False
        lastAutomaticDeviceId = ""
        suppressCalibrationDueAutoUpdate = True
        Try
            selectedOriginalDeviceId = DataService.GetValue(row, "DeviceId")
            txtDeviceId.Text = selectedOriginalDeviceId
            txtFixedAssetNo.Text = DataService.GetValue(row, "FixedAssetNo")
            txtDeviceName.Text = DataService.GetValue(row, "DeviceName")
            txtSerialNo.Text = DataService.GetValue(row, "SerialNo")
            txtBrand.Text = DataService.GetValue(row, "Brand")
            txtModel.Text = DataService.GetValue(row, "Model")
            SetComboText(cmbDeviceType, DataService.GetValue(row, "DeviceType"))
            txtMeasurementRange.Text = DataService.GetValue(row, "MeasurementRange")
            txtResolution.Text = DataService.GetValue(row, "Resolution")
            SetComboText(cmbUnit, DataService.GetValue(row, "Unit"))
            SetComboText(cmbReferenceDevice, If(IsYesFlag(DataService.GetValue(row, "ReferenceDevice")), "EVET", "HAYIR"))
            SetComboText(cmbUsageStatus, DataService.GetValue(row, "UsageStatus"))
            txtRegistrationDate.Text = FormatDateDisplay(DataService.GetValue(row, "RegistrationDate"))
            txtCalibrationPeriodMonths.Text = DataService.GetValue(row, "CalibrationPeriodMonths")
            SetDateValue(dtpCalibrationDate, DataService.GetValue(row, "CalibrationDate"))
            SetDateValue(dtpCalibrationDueDate, DataService.GetValue(row, "CalibrationDueDate"))
            SetComboText(cmbStatus, DataService.GetValue(row, "Status"))
            txtLocation.Text = DataService.GetValue(row, "Location")
            txtOrganization.Text = DataService.GetValue(row, "Organization")
            txtResponsible.Text = DataService.GetValue(row, "Responsible")
            txtNote.Text = DataService.GetValue(row, "Note")
            chkIso9001.Checked = IsYesFlag(DataService.GetValue(row, "StdIso9001"))
            chkIso45001.Checked = IsYesFlag(DataService.GetValue(row, "StdIso45001"))
            chkIso50001.Checked = IsYesFlag(DataService.GetValue(row, "StdIso50001"))
            chkIso46001.Checked = IsYesFlag(DataService.GetValue(row, "StdIso46001"))
            chkIso17020.Checked = IsYesFlag(DataService.GetValue(row, "StdIso17020"))
            chkIso17025.Checked = IsYesFlag(DataService.GetValue(row, "StdIso17025"))
        Finally
            suppressCalibrationDueAutoUpdate = False
        End Try
        If Not dtpCalibrationDueDate.Checked Then UpdateCalibrationDueDateFromPeriod()
        UpdateEditorModeUi()
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        Try
            UpdateCalibrationDueDateFromPeriod()
            Dim savingNewRecord = isNewRecord
            If savingNewRecord Then RefreshAutomaticDeviceId()
            Dim manualDeviceId = savingNewRecord AndAlso
                                 AppState.IsAdmin AndAlso
                                 txtDeviceId.Text.Trim() <> "" AndAlso
                                 Not String.Equals(txtDeviceId.Text.Trim(), lastAutomaticDeviceId, StringComparison.OrdinalIgnoreCase)
            Dim row As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                {"DeviceId", txtDeviceId.Text.Trim()},
                {"FixedAssetNo", txtFixedAssetNo.Text.Trim()},
                {"StdIso9001", If(chkIso9001.Checked, "YES", "NO")},
                {"StdIso45001", If(chkIso45001.Checked, "YES", "NO")},
                {"StdIso50001", If(chkIso50001.Checked, "YES", "NO")},
                {"StdIso46001", If(chkIso46001.Checked, "YES", "NO")},
                {"StdIso17020", If(chkIso17020.Checked, "YES", "NO")},
                {"StdIso17025", If(chkIso17025.Checked, "YES", "NO")},
                {"DeviceName", txtDeviceName.Text.Trim()},
                {"SerialNo", txtSerialNo.Text.Trim()},
                {"Brand", txtBrand.Text.Trim()},
                {"Model", txtModel.Text.Trim()},
                {"DeviceType", cmbDeviceType.Text.Trim()},
                {"MeasurementRange", txtMeasurementRange.Text.Trim()},
                {"Resolution", txtResolution.Text.Trim()},
                {"Unit", cmbUnit.Text.Trim()},
                {"ReferenceDevice", cmbReferenceDevice.Text.Trim()},
                {"UsageStatus", cmbUsageStatus.Text.Trim()},
                {"RegistrationDate", txtRegistrationDate.Text.Trim()},
                {"CalibrationPeriodMonths", txtCalibrationPeriodMonths.Text.Trim()},
                {"CalibrationDate", GetDateText(dtpCalibrationDate)},
                {"CalibrationDueDate", GetDateText(dtpCalibrationDueDate)},
                {"Status", cmbStatus.Text.Trim()},
                {"Location", txtLocation.Text.Trim()},
                {"Organization", txtOrganization.Text.Trim()},
                {"Responsible", txtResponsible.Text.Trim()},
                {"Note", txtNote.Text.Trim()}
            }
            Dim savedDeviceId = DataService.SaveMeasurementDevice(
                If(savingNewRecord, "", selectedOriginalDeviceId),
                row,
                manualDeviceId)
            row("DeviceId") = savedDeviceId
            AuditService.Log(If(savingNewRecord, "MSA_DEVICE_CREATE", "MSA_DEVICE_UPDATE"), "", "", "Cihaz=" & row("DeviceId"))
            LoadGrid()
            SelectDevice(row("DeviceId"))
            MessageBox.Show(If(savingNewRecord, "Yeni ölçüm cihazı kaydedildi.", "Ölçüm cihazı güncellendi."),
                            "Bilgi",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Cihaz kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Delete_Click(sender As Object, e As EventArgs)
        Try
            Dim target = selectedOriginalDeviceId.Trim()
            If isNewRecord OrElse target = "" Then
                MessageBox.Show("Silinecek cihazı listeden seçiniz.", "Seçim gerekli", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If
            If MessageBox.Show("'" & target & "' ölçüm cihazı silinsin mi?",
                               "Silme Onayı",
                               MessageBoxButtons.YesNo,
                               MessageBoxIcon.Warning,
                               MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then
                Return
            End If

            DataService.DeleteMeasurementDevice(target)
            AuditService.Log("MSA_DEVICE_DELETE", "", "", "Cihaz=" & target)
            LoadGrid()
            New_Click(sender, e)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Cihaz silinemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub New_Click(sender As Object, e As EventArgs)
        suppressCalibrationDueAutoUpdate = True
        Try
            isNewRecord = True
            lastAutomaticDeviceId = ""
            selectedOriginalDeviceId = ""
            txtDeviceId.Clear()
            txtFixedAssetNo.Clear()
            txtDeviceName.Clear()
            txtSerialNo.Clear()
            txtBrand.Clear()
            txtModel.Clear()
            cmbDeviceType.SelectedIndex = 0
            txtMeasurementRange.Clear()
            txtResolution.Clear()
            cmbUnit.SelectedIndex = 0
            cmbReferenceDevice.SelectedIndex = 0
            cmbUsageStatus.SelectedIndex = 0
            txtRegistrationDate.Clear()
            txtCalibrationPeriodMonths.Clear()
            dtpCalibrationDate.Checked = False
            dtpCalibrationDueDate.Checked = False
            cmbStatus.SelectedIndex = 0
            txtLocation.Clear()
            txtOrganization.Clear()
            txtResponsible.Clear()
            txtNote.Clear()
            chkIso9001.Checked = False
            chkIso45001.Checked = False
            chkIso50001.Checked = False
            chkIso46001.Checked = False
            chkIso17020.Checked = False
            chkIso17025.Checked = False
        Finally
            suppressCalibrationDueAutoUpdate = False
        End Try
        grid.CurrentCell = Nothing
        grid.ClearSelection()
        RefreshAutomaticDeviceId()
        UpdateEditorModeUi()
        txtLocation.Focus()
    End Sub

    Private Sub SelectDevice(deviceId As String)
        For Each row As DataGridViewRow In grid.Rows
            Dim item = TryCast(row.Tag, Dictionary(Of String, String))
            If item IsNot Nothing AndAlso String.Equals(DataService.GetValue(item, "DeviceId"), deviceId, StringComparison.OrdinalIgnoreCase) Then
                row.Selected = True
                grid.CurrentCell = row.Cells(0)
                LoadSelectedRow()
                Exit For
            End If
        Next
    End Sub

    Private Sub SetEditorEnabled(enabled As Boolean)
        For Each control As TextBox In New TextBox() {txtFixedAssetNo, txtDeviceName, txtSerialNo, txtBrand, txtModel, txtMeasurementRange, txtResolution, txtLocation, txtRegistrationDate, txtCalibrationPeriodMonths, txtOrganization, txtResponsible, txtNote}
            control.ReadOnly = Not enabled
        Next
        txtDeviceId.ReadOnly = Not (enabled AndAlso AppState.IsAdmin)
        txtDeviceId.BackColor = If(enabled AndAlso AppState.IsAdmin, Color.White, Color.FromArgb(235, 242, 250))
        For Each control As Control In New Control() {cmbDeviceType, cmbUnit, cmbReferenceDevice, cmbUsageStatus, cmbStatus, dtpCalibrationDate, dtpCalibrationDueDate, chkIso9001, chkIso45001, chkIso50001, chkIso46001, chkIso17020, chkIso17025}
            control.Enabled = enabled
        Next
        btnSave.Enabled = enabled
        btnNew.Enabled = enabled
        btnDelete.Enabled = enabled AndAlso Not isNewRecord AndAlso selectedOriginalDeviceId.Trim() <> ""
        UpdateEditorModeUi()
    End Sub

    Private Sub UpdateEditorModeUi()
        If Not AppState.CanModifyMsaDashboard Then
            lblMode.Text = "SALT OKUNUR"
            btnSave.Text = "Kaydet"
            btnDelete.Enabled = False
            Return
        End If

        If isNewRecord Then
            lblMode.Text = "YENİ CİHAZ"
            btnSave.Text = "Yeni Cihazı Kaydet"
            btnDelete.Enabled = False
        Else
            lblMode.Text = "KAYIT DÜZENLEME"
            btnSave.Text = "Değişiklikleri Kaydet"
            btnDelete.Enabled = selectedOriginalDeviceId.Trim() <> ""
        End If
    End Sub

    Private Sub RefreshAutomaticDeviceId()
        If Not isNewRecord Then Return
        If AppState.IsAdmin AndAlso
           txtDeviceId.Text.Trim() <> "" AndAlso
           Not String.Equals(txtDeviceId.Text.Trim(), lastAutomaticDeviceId, StringComparison.OrdinalIgnoreCase) Then
            Return
        End If

        Dim location = txtLocation.Text.Trim()
        Dim deviceType = cmbDeviceType.Text.Trim()
        If location = "" OrElse deviceType = "" Then
            txtDeviceId.Clear()
            lastAutomaticDeviceId = ""
            txtDeviceId.PlaceholderText = "Önce kullanıldığı bölüm ve cihaz tipini girin"
            Return
        End If

        Try
            lastAutomaticDeviceId = DataService.SuggestMeasurementDeviceId(location, deviceType)
            txtDeviceId.Text = lastAutomaticDeviceId
        Catch
            txtDeviceId.Clear()
            lastAutomaticDeviceId = ""
        End Try
    End Sub

    Private Shared Function MakeColumn(name As String, header As String, minimumWidth As Integer, fillWeight As Single) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .HeaderText = header,
            .Width = minimumWidth,
            .MinimumWidth = minimumWidth,
            .FillWeight = fillWeight,
            .SortMode = DataGridViewColumnSortMode.Automatic
        }
    End Function

    Private Shared Sub ConfigureTextBox(box As TextBox, placeholder As String)
        box.Dock = DockStyle.Fill
        box.Margin = New Padding(5, 4, 5, 4)
        box.PlaceholderText = placeholder
    End Sub

    Private Shared Sub ConfigureMultiline(box As TextBox, placeholder As String)
        ConfigureTextBox(box, placeholder)
        box.Multiline = True
        box.ScrollBars = ScrollBars.Vertical
    End Sub

    Private Shared Sub ConfigureCombo(combo As ComboBox, items As IEnumerable(Of String))
        combo.Dock = DockStyle.Fill
        combo.Margin = New Padding(5, 4, 5, 4)
        combo.DropDownStyle = ComboBoxStyle.DropDown
        combo.Items.Clear()
        For Each item In items
            combo.Items.Add(item)
        Next
        If combo.Items.Count > 0 Then combo.SelectedIndex = 0
    End Sub

    Private Shared Sub ConfigureDate(picker As DateTimePicker)
        picker.Dock = DockStyle.Fill
        picker.Margin = New Padding(5, 4, 5, 4)
        picker.Format = DateTimePickerFormat.Custom
        picker.CustomFormat = "dd.MM.yyyy"
        picker.ShowCheckBox = True
        picker.Checked = False
    End Sub

    Private Shared Sub ConfigureCheckBox(box As CheckBox, text As String)
        box.Text = text
        box.AutoSize = True
        box.Margin = New Padding(4, 5, 14, 2)
        box.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        box.ForeColor = Color.FromArgb(31, 71, 126)
    End Sub

    Private Shared Sub ConfigureButton(button As Button, text As String, width As Integer, backColor As Color, foreColor As Color)
        button.Text = text
        button.Width = width
        button.Height = 36
        button.BackColor = backColor
        button.ForeColor = foreColor
        button.FlatStyle = FlatStyle.Flat
        button.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        button.Margin = New Padding(4, 0, 4, 0)
        button.Cursor = Cursors.Hand
        button.UseVisualStyleBackColor = False
    End Sub

    Private Shared Sub AddField(layout As TableLayoutPanel, caption As String, control As Control, column As Integer, row As Integer)
        Dim label As New Label() With {
            .Text = caption,
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(5)
        }
        layout.Controls.Add(label, column, row)
        layout.Controls.Add(control, column + 1, row)
    End Sub

    Private Sub UpdateCalibrationDueDateFromPeriod()
        If suppressCalibrationDueAutoUpdate Then Return
        If Not dtpCalibrationDate.Checked Then Return

        Dim months As Integer
        If Not Integer.TryParse(txtCalibrationPeriodMonths.Text.Trim(), months) OrElse months <= 0 Then Return

        dtpCalibrationDueDate.Value = dtpCalibrationDate.Value.Date.AddMonths(months)
        dtpCalibrationDueDate.Checked = True
    End Sub

    Private Shared Function GetDateText(picker As DateTimePicker) As String
        If Not picker.Checked Then Return ""
        Return picker.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
    End Function

    Private Shared Sub SetDateValue(picker As DateTimePicker, value As String)
        Dim parsed As DateTime
        If DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, parsed) OrElse
           DateTime.TryParse(value, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, parsed) Then
            picker.Value = parsed
            picker.Checked = True
        Else
            picker.Checked = False
        End If
    End Sub

    Private Shared Function FormatDateDisplay(value As String) As String
        Dim parsed As DateTime
        If DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, parsed) OrElse
           DateTime.TryParse(value, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, parsed) Then
            Return parsed.ToString("dd.MM.yyyy")
        End If
        Return ""
    End Function

    Private Shared Function IsYesFlag(value As String) As Boolean
        Dim text = If(value, "").Trim().ToUpperInvariant()
        Return text = "YES" OrElse text = "EVET" OrElse text = "TRUE" OrElse text = "1"
    End Function

    Private Shared Function FormatYesNoDisplay(value As String) As String
        Return If(IsYesFlag(value), "EVET", "HAYIR")
    End Function

    Private Shared Sub SetComboText(combo As ComboBox, value As String)
        Dim text = If(value, "").Trim()
        If text = "" Then
            If combo.Items.Count > 0 Then combo.SelectedIndex = 0
            Return
        End If
        For i As Integer = 0 To combo.Items.Count - 1
            If String.Equals(combo.Items(i).ToString(), text, StringComparison.OrdinalIgnoreCase) Then
                combo.SelectedIndex = i
                Return
            End If
        Next
        combo.Text = text
    End Sub

    Private Shared Sub ApplyRowStyle(gridRow As DataGridViewRow, item As Dictionary(Of String, String))
        Dim status = DataService.GetValue(item, "Status")
        If String.Equals(status, "KULLANIM DIŞI", StringComparison.OrdinalIgnoreCase) Then
            gridRow.DefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245)
            gridRow.DefaultCellStyle.ForeColor = Color.Gray
            Return
        End If

        If IsExpired(DataService.GetValue(item, "CalibrationDueDate")) Then
            gridRow.DefaultCellStyle.BackColor = Color.FromArgb(255, 224, 224)
            gridRow.DefaultCellStyle.ForeColor = Color.DarkRed
        ElseIf IsDueSoon(DataService.GetValue(item, "CalibrationDueDate")) OrElse
               String.Equals(status, "KALİBRASYON BEKLİYOR", StringComparison.OrdinalIgnoreCase) Then
            gridRow.DefaultCellStyle.BackColor = Color.FromArgb(255, 248, 220)
            gridRow.DefaultCellStyle.ForeColor = Color.FromArgb(120, 80, 0)
        End If
    End Sub

    Private Shared Function IsExpired(value As String) As Boolean
        Dim parsed As DateTime
        If Not DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, parsed) AndAlso
           Not DateTime.TryParse(value, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, parsed) Then Return False
        Return parsed.Date < DateTime.Today
    End Function

    Private Shared Function IsDueSoon(value As String) As Boolean
        Dim parsed As DateTime
        If Not DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, parsed) AndAlso
           Not DateTime.TryParse(value, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, parsed) Then Return False
        Return parsed.Date >= DateTime.Today AndAlso parsed.Date <= DateTime.Today.AddDays(30)
    End Function
End Class
