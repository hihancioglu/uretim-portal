Imports System.Data
Imports System.Drawing
Imports System.Linq
Imports System.Net
Imports System.Text
Imports System.Windows.Forms

Public Class FrmPlasticShiftTracking
    Inherits Form

    Private ReadOnly mechanismMode As Boolean
    Private ReadOnly grid As New DataGridView()
    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly dtpDate As New DateTimePicker()
    Private ReadOnly lblCount As New Label()
    Private ReadOnly btnNew As New Button()
    Private ReadOnly btnDetail As New Button()
    Private ReadOnly btnEdit As New Button()
    Private ReadOnly btnDelete As New Button()
    Private ReadOnly btnRefresh As New Button()
    Private ReadOnly btnEmailReport As New Button()
    Private ReadOnly btnResendEmail As New Button()
    Private ReadOnly btnEmailRecipients As New Button()
    Private ReadOnly btnErrorReport As New Button()
    Private currentRows As New List(Of Dictionary(Of String, String))()

    Public Sub New(Optional useMechanismMode As Boolean = False)
        mechanismMode = useMechanismMode
        AuthorizationService.Require(CanOpenFeature, FeatureTitle)
        AppIconService.Apply(Me)
        Text = FeatureTitle
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1500, 820)
        MinimumSize = New Size(760, 520)
        Font = New Font("Segoe UI", 9.0F)
        BackColor = Color.FromArgb(244, 247, 251)

        BuildScreen()
        ResponsiveFormService.Apply(Me)
        LoadGrid()
    End Sub

    Private ReadOnly Property FeatureTitle As String
        Get
            Return If(mechanismMode, "Mekanizma Vardiya Takip Listesi", "Plastikhane Vardiya Takip Listesi")
        End Get
    End Property

    Private ReadOnly Property CanOpenFeature As Boolean
        Get
            Return If(mechanismMode, AppState.CanOpenMechanismShiftTracking, AppState.CanOpenPlasticShiftTracking)
        End Get
    End Property

    Private ReadOnly Property CanModifyFeature As Boolean
        Get
            Return If(mechanismMode, AppState.CanModifyMechanismShiftTracking, AppState.CanModifyPlasticShiftTracking)
        End Get
    End Property

    Private ReadOnly Property CanDeleteFeature As Boolean
        Get
            Return If(mechanismMode, AppState.CanDeleteMechanismShiftTracking, AppState.CanDeletePlasticShiftTracking)
        End Get
    End Property

    Private Sub BuildScreen()
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .BackColor = BackColor,
            .Padding = New Padding(8)
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(root)

        Dim toolbar As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = True,
            .AutoScroll = False,
            .Padding = New Padding(8, 9, 8, 7),
            .BackColor = Color.White,
            .Margin = New Padding(0, 0, 0, 6)
        }
        root.Controls.Add(toolbar, 0, 0)

        ConfigureButton(btnNew, "Yeni Kayıt", 112, Color.FromArgb(22, 128, 70), Color.White)
        ConfigureButton(btnDetail, "Detay", 90, Color.White, Color.FromArgb(35, 62, 92))
        ConfigureButton(btnEdit, "Düzenle", 95, Color.FromArgb(37, 86, 142), Color.White)
        ConfigureButton(btnDelete, "Seçili Kaydı Sil", 130, Color.FromArgb(190, 40, 35), Color.White)
        ConfigureButton(btnRefresh, "Yenile", 90, Color.White, Color.FromArgb(35, 62, 92))
        ConfigureButton(btnEmailReport, "E-posta Raporu", 135, Color.FromArgb(15, 123, 63), Color.White)
        ConfigureButton(btnResendEmail, "Seçili Maili Hazırla", 175, Color.FromArgb(255, 243, 224), Color.FromArgb(145, 74, 0))
        ConfigureButton(btnEmailRecipients, "Mail Alıcıları", 125, Color.FromArgb(232, 242, 255), Color.FromArgb(31, 71, 126))
        ConfigureButton(btnErrorReport, "Hata Raporu", 120, Color.FromArgb(255, 231, 231), Color.FromArgb(156, 26, 26))

        AddHandler btnNew.Click, AddressOf NewRecord_Click
        AddHandler btnDetail.Click, AddressOf Detail_Click
        AddHandler btnEdit.Click, AddressOf Edit_Click
        AddHandler btnDelete.Click, AddressOf Delete_Click
        AddHandler btnRefresh.Click, Sub() LoadGrid(SelectedRecordId())
        AddHandler btnEmailReport.Click, AddressOf EmailReport_Click
        AddHandler btnResendEmail.Click, AddressOf PrepareEmail_Click
        AddHandler btnEmailRecipients.Click, AddressOf EmailRecipients_Click
        AddHandler btnErrorReport.Click, AddressOf ErrorReport_Click

        btnNew.Visible = CanModifyFeature
        btnEdit.Visible = CanModifyFeature
        btnDelete.Visible = CanDeleteFeature
        btnResendEmail.Visible = mechanismMode AndAlso AppState.IsAdmin
        btnEmailRecipients.Visible = AppState.CanManagePlasticShiftEmailRecipients
        btnErrorReport.Visible = Not mechanismMode

        toolbar.Controls.Add(btnNew)
        toolbar.Controls.Add(btnDetail)
        toolbar.Controls.Add(btnEdit)
        toolbar.Controls.Add(btnErrorReport)
        toolbar.Controls.Add(btnDelete)
        toolbar.Controls.Add(btnRefresh)
        toolbar.Controls.Add(btnResendEmail)
        toolbar.Controls.Add(btnEmailReport)
        toolbar.Controls.Add(btnEmailRecipients)

        toolbar.Controls.Add(New Label() With {
            .Text = "Arama",
            .Width = 50,
            .Height = 32,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(18, 2, 3, 2)
        })
        txtSearch.Width = 270
        txtSearch.Height = 26
        txtSearch.PlaceholderText = "ürün / sorumlu / sorun / aksiyon"
        txtSearch.Margin = New Padding(3, 5, 8, 5)
        AddHandler txtSearch.TextChanged, Sub() LoadGrid(SelectedRecordId())
        toolbar.Controls.Add(txtSearch)

        toolbar.Controls.Add(New Label() With {
            .Text = "Tarih",
            .Width = 40,
            .Height = 32,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(4, 2, 3, 2)
        })
        dtpDate.Width = 135
        dtpDate.Height = 26
        dtpDate.Format = DateTimePickerFormat.Custom
        dtpDate.CustomFormat = "dd.MM.yyyy"
        dtpDate.ShowCheckBox = True
        dtpDate.Checked = False
        dtpDate.Margin = New Padding(3, 4, 8, 4)
        AddHandler dtpDate.ValueChanged, Sub() LoadGrid(SelectedRecordId())
        toolbar.Controls.Add(dtpDate)

        If Not CanModifyFeature Then
            toolbar.Controls.Add(New Label() With {
                .Text = If(CanDeleteFeature, "SİLME YETKİSİ", "SALT OKUNUR"),
                .Width = 120,
                .Height = 32,
                .TextAlign = ContentAlignment.MiddleCenter,
                .Font = New Font("Segoe UI", 8.5F, FontStyle.Bold),
                .ForeColor = Color.FromArgb(130, 82, 0),
                .BackColor = Color.FromArgb(255, 245, 204),
                .Margin = New Padding(10, 2, 4, 2)
            })
        End If

        Dim adjustToolbarHeight As Action =
            Sub()
                If root.IsDisposed OrElse toolbar.IsDisposed Then Return
                Dim availableWidth = Math.Max(320, root.ClientSize.Width - root.Padding.Horizontal)
                Dim preferredHeight = toolbar.GetPreferredSize(New Size(availableWidth, 0)).Height
                root.RowStyles(0).Height = CSng(Math.Max(58, Math.Min(150, preferredHeight + 2)))
                toolbar.AutoScroll = preferredHeight > 148
            End Sub
        AddHandler root.ClientSizeChanged, Sub(sender, e) adjustToolbarHeight()
        AddHandler Shown, Sub(sender, e) adjustToolbarHeight()

        Dim summary As New Panel() With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(231, 238, 248),
            .Padding = New Padding(12, 0, 12, 0),
            .Margin = New Padding(0, 0, 0, 6)
        }
        lblCount.Dock = DockStyle.Fill
        lblCount.TextAlign = ContentAlignment.MiddleLeft
        lblCount.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblCount.ForeColor = Color.FromArgb(31, 71, 126)
        summary.Controls.Add(lblCount)
        root.Controls.Add(summary, 0, 1)

        ConfigureGrid()
        root.Controls.Add(grid, 0, 2)
    End Sub

    Private Shared Sub ConfigureButton(button As Button, caption As String, width As Integer, backColor As Color, foreColor As Color)
        button.Text = caption
        button.Width = width
        button.Height = 34
        button.Margin = New Padding(4, 1, 4, 1)
        button.FlatStyle = FlatStyle.Flat
        button.FlatAppearance.BorderColor = Color.FromArgb(190, 201, 215)
        button.BackColor = backColor
        button.ForeColor = foreColor
        button.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        button.Cursor = Cursors.Hand
        button.AutoEllipsis = False
        button.Tag = "RESPONSIVE_NO_AUTO_SCALE"
    End Sub

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.Margin = New Padding(0)
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.AllowUserToResizeRows = False
        grid.MultiSelect = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.AutoGenerateColumns = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
        grid.RowHeadersVisible = False
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.GridColor = Color.FromArgb(214, 221, 230)
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersHeight = 48
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(226, 234, 244)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(24, 50, 82)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
        grid.DefaultCellStyle.Font = New Font("Segoe UI", 9.0F)
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(213, 229, 249)
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 40, 65)
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 253)
        grid.RowTemplate.Height = 56
        grid.ScrollBars = ScrollBars.Both

        Dim recordIdColumn = MakeTextColumn("RecordId", "Kayıt No", 120, DataGridViewAutoSizeColumnMode.None)
        recordIdColumn.Visible = False
        grid.Columns.Add(recordIdColumn)
        grid.Columns.Add(MakeTextColumn("OccurredAt", "TARİH / SAAT", 130, DataGridViewAutoSizeColumnMode.None))
        grid.Columns.Add(MakeTextColumn("DefectiveQuantity", "HATALI ADET / MİKTAR", 135, DataGridViewAutoSizeColumnMode.None))
        grid.Columns.Add(MakeTextColumn("Responsible", "SORUMLU", 120, DataGridViewAutoSizeColumnMode.Fill, 11.0F))
        grid.Columns.Add(MakeTextColumn("ProductNameCode", "ÜRÜN ADI VE KODU", 180, DataGridViewAutoSizeColumnMode.Fill, 20.0F))
        grid.Columns.Add(MakeTextColumn("Problem", "SORUN", 210, DataGridViewAutoSizeColumnMode.Fill, 27.0F))
        grid.Columns.Add(MakeTextColumn("ActionTaken", "ALINAN AKSİYON", 210, DataGridViewAutoSizeColumnMode.Fill, 27.0F))
        grid.Columns.Add(MakeCheckColumn("YellowCard", "SARI" & Environment.NewLine & "KART"))
        grid.Columns.Add(MakeCheckColumn("MoldModification", "KALIP" & Environment.NewLine & "TADİLAT"))
        grid.Columns.Add(MakeCheckColumn("ErrorReport", "HATA" & Environment.NewLine & "RAPORU"))
        grid.Columns.Add(MakeCheckColumn("TestPerformed", "TEST"))

        AddHandler grid.SelectionChanged, AddressOf Grid_SelectionChanged
        AddHandler grid.CellDoubleClick, AddressOf Grid_CellDoubleClick
        AddHandler grid.KeyDown, AddressOf Grid_KeyDown
        AddHandler grid.CellFormatting, AddressOf Grid_CellFormatting
    End Sub

    Private Shared Function MakeTextColumn(name As String,
                                           header As String,
                                           width As Integer,
                                           autoSizeMode As DataGridViewAutoSizeColumnMode,
                                           Optional fillWeight As Single = 10.0F) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .DataPropertyName = name,
            .HeaderText = header,
            .Width = width,
            .MinimumWidth = Math.Min(width, 90),
            .AutoSizeMode = autoSizeMode,
            .FillWeight = fillWeight,
            .SortMode = DataGridViewColumnSortMode.Automatic
        }
    End Function

    Private Shared Function MakeCheckColumn(name As String, header As String) As DataGridViewCheckBoxColumn
        Return New DataGridViewCheckBoxColumn() With {
            .Name = name,
            .DataPropertyName = name,
            .HeaderText = header,
            .Width = 78,
            .MinimumWidth = 68,
            .AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            .ThreeState = False,
            .SortMode = DataGridViewColumnSortMode.Automatic,
            .DefaultCellStyle = New DataGridViewCellStyle() With {.Alignment = DataGridViewContentAlignment.MiddleCenter}
        }
    End Function

    Private Sub LoadGrid(Optional selectedId As String = "")
        Try
            Dim allRows = If(mechanismMode,
                             DataService.GetMechanismShiftTrackingRecords(),
                             DataService.GetPlasticShiftTrackingRecords()).
                OrderByDescending(Function(row) DataService.GetValue(row, "OccurredAt")).
                ToList()
            Dim rows = allRows.AsEnumerable()

            Dim filterText = txtSearch.Text.Trim()
            If filterText <> "" Then
                Dim tokens = filterText.Split(New Char() {" "c, ";"c, ","c, "|"c}, StringSplitOptions.RemoveEmptyEntries)
                rows = rows.Where(
                    Function(row)
                        Dim haystack = String.Join(" ", {
                            DataService.GetValue(row, "RecordId"),
                            DataService.GetValue(row, "DefectiveQuantity"),
                            DataService.GetValue(row, "Responsible"),
                            DataService.GetValue(row, "ProductNameCode"),
                            DataService.GetValue(row, "Problem"),
                            DataService.GetValue(row, "ActionTaken")})
                        Return tokens.All(Function(token) haystack.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    End Function)
            End If

            If dtpDate.Checked Then
                rows = rows.Where(
                    Function(row)
                        Dim value As DateTime
                        Return DateTime.TryParse(DataService.GetValue(row, "OccurredAt"), value) AndAlso value.Date = dtpDate.Value.Date
                    End Function)
            End If

            currentRows = rows.ToList()
            Dim table As New DataTable()
            table.Columns.Add("RecordId")
            table.Columns.Add("OccurredAt", GetType(DateTime))
            table.Columns.Add("DefectiveQuantity")
            table.Columns.Add("Responsible")
            table.Columns.Add("ProductNameCode")
            table.Columns.Add("Problem")
            table.Columns.Add("ActionTaken")
            table.Columns.Add("YellowCard", GetType(Boolean))
            table.Columns.Add("MoldModification", GetType(Boolean))
            table.Columns.Add("ErrorReport", GetType(Boolean))
            table.Columns.Add("TestPerformed", GetType(Boolean))

            For Each sourceRow In currentRows
                Dim row = table.NewRow()
                row("RecordId") = DataService.GetValue(sourceRow, "RecordId")
                Dim occurredAt As DateTime
                If DateTime.TryParse(DataService.GetValue(sourceRow, "OccurredAt"), occurredAt) Then row("OccurredAt") = occurredAt
                row("DefectiveQuantity") = DataService.GetValue(sourceRow, "DefectiveQuantity")
                row("Responsible") = DataService.GetValue(sourceRow, "Responsible")
                row("ProductNameCode") = DataService.GetValue(sourceRow, "ProductNameCode")
                row("Problem") = DataService.GetValue(sourceRow, "Problem")
                row("ActionTaken") = DataService.GetValue(sourceRow, "ActionTaken")
                row("YellowCard") = IsFlagSet(sourceRow, "YellowCard")
                row("MoldModification") = IsFlagSet(sourceRow, "MoldModification")
                row("ErrorReport") = IsFlagSet(sourceRow, "ErrorReport")
                row("TestPerformed") = IsFlagSet(sourceRow, "TestPerformed")
                table.Rows.Add(row)
            Next

            grid.DataSource = table
            If grid.Columns.Contains("OccurredAt") Then grid.Columns("OccurredAt").DefaultCellStyle.Format = "dd.MM.yyyy HH:mm"
            If grid.Columns.Contains("DefectiveQuantity") Then grid.Columns("DefectiveQuantity").DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter

            Dim yellowCount = currentRows.Where(Function(row) IsFlagSet(row, "YellowCard")).Count()
            Dim moldCount = currentRows.Where(Function(row) IsFlagSet(row, "MoldModification")).Count()
            Dim reportCount = currentRows.Where(Function(row) IsFlagSet(row, "ErrorReport")).Count()
            Dim testCount = currentRows.Where(Function(row) IsFlagSet(row, "TestPerformed")).Count()
            lblCount.Text = "Gösterilen: " & currentRows.Count.ToString() & " / " & allRows.Count.ToString() &
                            "   |   Sarı Kart: " & yellowCount.ToString() &
                            "   |   Kalıp Tadilat: " & moldCount.ToString() &
                            "   |   Hata Raporu: " & reportCount.ToString() &
                            "   |   Test: " & testCount.ToString()

            RestoreSelection(selectedId)
            UpdateButtonState()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Vardiya takip listesi okunamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Shared Function IsFlagSet(row As Dictionary(Of String, String), columnName As String) As Boolean
        Dim value = DataService.GetValue(row, columnName).Trim().ToUpperInvariant()
        Return value = "YES" OrElse value = "EVET" OrElse value = "TRUE" OrElse value = "1" OrElse value = "X"
    End Function

    Private Sub RestoreSelection(recordId As String)
        If recordId = "" OrElse Not grid.Columns.Contains("RecordId") Then Return
        For Each row As DataGridViewRow In grid.Rows
            If String.Equals(Convert.ToString(row.Cells("RecordId").Value), recordId, StringComparison.OrdinalIgnoreCase) Then
                row.Selected = True
                grid.CurrentCell = row.Cells("OccurredAt")
                Exit For
            End If
        Next
    End Sub

    Private Function SelectedRecordId() As String
        If grid.CurrentRow Is Nothing OrElse Not grid.Columns.Contains("RecordId") Then Return ""
        Return Convert.ToString(grid.CurrentRow.Cells("RecordId").Value).Trim()
    End Function

    Private Function SelectedRecord() As Dictionary(Of String, String)
        Dim recordId = SelectedRecordId()
        If recordId = "" Then Return Nothing
        Return currentRows.FirstOrDefault(
            Function(row) String.Equals(DataService.GetValue(row, "RecordId"), recordId, StringComparison.OrdinalIgnoreCase))
    End Function

    Private Sub UpdateButtonState()
        Dim hasSelection = SelectedRecord() IsNot Nothing
        btnDetail.Enabled = hasSelection
        btnEdit.Enabled = hasSelection AndAlso CanModifyFeature
        btnDelete.Enabled = hasSelection AndAlso CanDeleteFeature
        btnResendEmail.Enabled = btnResendEmail.Visible AndAlso hasSelection
        If mechanismMode Then
            btnErrorReport.Enabled = False
            Return
        End If
        If hasSelection Then
            Dim selected = SelectedRecord()
            Dim existing = DataService.GetPlasticShiftErrorReport(DataService.GetValue(selected, "RecordId"))
            btnErrorReport.Enabled = existing IsNot Nothing OrElse AppState.CanCreatePlasticShiftErrorReport
            btnErrorReport.BackColor = If(
                existing IsNot Nothing,
                Color.FromArgb(255, 211, 211),
                Color.FromArgb(255, 241, 224))
            btnErrorReport.Text = If(existing IsNot Nothing, "Hata Raporunu Aç", "Hata Raporu Oluştur")
            btnErrorReport.Width = If(existing IsNot Nothing, 145, 160)
        Else
            btnErrorReport.Enabled = False
            btnErrorReport.Text = "Hata Raporu"
            btnErrorReport.Width = 120
        End If
    End Sub

    Private Sub Grid_SelectionChanged(sender As Object, e As EventArgs)
        UpdateButtonState()
    End Sub

    Private Sub NewRecord_Click(sender As Object, e As EventArgs)
        AuthorizationService.Require(CanModifyFeature, "Yeni " & FeatureTitle & " Kaydı")
        Using detail As New FrmPlasticShiftTrackingDetail(Nothing, False, mechanismMode)
            detail.ShowDialog(Me)
            If detail.SavedChanges Then LoadGrid(detail.AffectedRecordId)
        End Using
    End Sub

    Private Sub Detail_Click(sender As Object, e As EventArgs)
        OpenSelected(True)
    End Sub

    Private Sub Edit_Click(sender As Object, e As EventArgs)
        AuthorizationService.Require(CanModifyFeature, FeatureTitle & " Kaydı Düzenleme")
        OpenSelected(False)
    End Sub

    Private Sub ErrorReport_Click(sender As Object, e As EventArgs)
        Try
            Dim selected = SelectedRecord()
            If selected Is Nothing Then Return
            Dim shiftId = DataService.GetValue(selected, "RecordId")
            Dim existing = DataService.GetPlasticShiftErrorReport(shiftId)
            If existing Is Nothing AndAlso Not AppState.CanCreatePlasticShiftErrorReport Then
                MessageBox.Show(
                    "Bu vardiya kaydı için henüz hata raporu oluşturulmamış.",
                    "Hata raporu",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information)
                Return
            End If

            Using reportForm As New FrmPlasticShiftErrorReport(selected)
                reportForm.ShowDialog(Me)
                If reportForm.SavedChanges Then LoadGrid(shiftId)
            End Using
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Hata raporu açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub OpenSelected(readOnlyMode As Boolean)
        Dim selected = SelectedRecord()
        If selected Is Nothing Then Return
        Using detail As New FrmPlasticShiftTrackingDetail(selected, readOnlyMode OrElse Not CanModifyFeature, mechanismMode)
            detail.ShowDialog(Me)
            If detail.SavedChanges Then LoadGrid(detail.AffectedRecordId)
        End Using
    End Sub

    Private Sub EmailRecipients_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.CanManagePlasticShiftEmailRecipients, "Vardiya Takip Mail Alıcıları")
            Using recipientsForm As New FrmPlasticShiftEmailRecipients(mechanismMode)
                recipientsForm.ShowDialog(Me)
            End Using
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Mail alıcıları açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub PrepareEmail_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(
                mechanismMode AndAlso AppState.IsAdmin,
                "Mekanizma Vardiya Takip Mail Taslağı Hazırlama")

            Dim selected = SelectedRecord()
            If selected Is Nothing Then
                MessageBox.Show(
                    "Mail taslağı hazırlanacak kaydı seçin.",
                    "Kayıt seçilmedi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information)
                Return
            End If

            Dim recordId = DataService.GetValue(selected, "RecordId")
            Dim product = DataService.GetValue(selected, "ProductNameCode")
            Dim answer = MessageBox.Show(
                "Seçili kaydın ilk kayıt bildirimi Outlook'ta düzenlenebilir taslak olarak açılacak." & Environment.NewLine &
                "Kime ve CC alanlarına kişi ekleyebilir, içeriği kontrol ettikten sonra kendiniz gönderebilirsiniz." & Environment.NewLine & Environment.NewLine &
                "Kayıt No: " & recordId & Environment.NewLine &
                "Ürün: " & product & Environment.NewLine & Environment.NewLine &
                "Kayıt verisi değiştirilmeyecek. Taslak hazırlansın mı?",
                "Mail taslağı hazırla",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1)
            If answer <> DialogResult.Yes Then Return

            btnResendEmail.Enabled = False
            UseWaitCursor = True
            Dim emailError As String = ""
            If Not PlasticShiftEmailNotificationService.TryOpenNewRecordDraft(selected, emailError, True) Then
                MessageBox.Show(
                    "Seçili kaydın e-posta taslağı hazırlanamadı." & Environment.NewLine & Environment.NewLine & emailError,
                    "E-posta taslağı açılamadı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning)
                Return
            End If
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "E-posta taslağı açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            UseWaitCursor = False
            UpdateButtonState()
        End Try
    End Sub

    Private Sub EmailReport_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(CanOpenFeature, FeatureTitle & " E-posta Raporu")
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

            Dim reportDate = If(dtpDate.Checked,
                                dtpDate.Value.ToString("dd.MM.yyyy"),
                                DateTime.Now.ToString("dd.MM.yyyy"))
            Dim subject = FeatureTitle & " Raporu - " & reportDate
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
                If(mechanismMode, "MECHANISM_SHIFT_EMAIL_REPORT", "PLASTIC_SHIFT_EMAIL_REPORT"),
                "",
                "",
                "Kayıt sayısı=" & currentRows.Count.ToString() &
                "; Arama=" & txtSearch.Text.Trim() &
                "; Tarih=" & If(dtpDate.Checked, dtpDate.Value.ToString("yyyy-MM-dd"), "TÜMÜ"))
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "E-posta raporu hazırlanamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function BuildEmailReportHtml(rows As List(Of Dictionary(Of String, String))) As String
        Dim defectiveQuantitySummary = BuildDefectiveQuantitySummary(rows)

        Dim yellowCount = rows.Where(Function(row) IsFlagSet(row, "YellowCard")).Count()
        Dim moldCount = rows.Where(Function(row) IsFlagSet(row, "MoldModification")).Count()
        Dim errorCount = rows.Where(Function(row) IsFlagSet(row, "ErrorReport")).Count()
        Dim testCount = rows.Where(Function(row) IsFlagSet(row, "TestPerformed")).Count()
        Dim filterDescription = If(txtSearch.Text.Trim() = "", "Yok", txtSearch.Text.Trim())
        Dim dateDescription = If(dtpDate.Checked, dtpDate.Value.ToString("dd.MM.yyyy"), "Tüm tarihler")

        Dim html As New StringBuilder()
        html.AppendLine("<!DOCTYPE html><html><head><meta charset=""utf-8""></head>")
        html.AppendLine("<body style=""font-family:Segoe UI,Arial,sans-serif;font-size:13px;color:#1f2937;background:#ffffff;"">")
        html.AppendLine("<h2 style=""margin:0 0 6px;color:#1f477e;"">" & EncodeHtml(FeatureTitle) & " Raporu</h2>")
        html.AppendLine("<div style=""margin-bottom:14px;color:#4b5563;"">Hazırlanma: " & EncodeHtml(DateTime.Now.ToString("dd.MM.yyyy HH:mm")) &
                        " &nbsp; | &nbsp; Hazırlayan: " & EncodeHtml(AppState.CurrentUserName & " / " & AppState.NormalizeRole(AppState.CurrentRole)) &
                        " &nbsp; | &nbsp; Tarih filtresi: " & EncodeHtml(dateDescription) &
                        " &nbsp; | &nbsp; Arama: " & EncodeHtml(filterDescription) & "</div>")

        html.AppendLine("<table style=""border-collapse:collapse;margin-bottom:16px;""><tr>")
        AppendSummaryCell(html, "Kayıt", rows.Count.ToString(), "#e7eef8", "#1f477e")
        AppendSummaryCell(html, "Hatalı Miktar", defectiveQuantitySummary, "#fee2e2", "#991b1b")
        AppendSummaryCell(html, "Sarı Kart", yellowCount.ToString(), "#fff4b3", "#785700")
        AppendSummaryCell(html, "Kalıp Tadilat", moldCount.ToString(), "#ffe2bf", "#8a4700")
        AppendSummaryCell(html, "Hata Raporu", errorCount.ToString(), "#ffd3d3", "#991b1b")
        AppendSummaryCell(html, "Test", testCount.ToString(), "#d3f0db", "#166534")
        html.AppendLine("</tr></table>")

        html.AppendLine("<table style=""border-collapse:collapse;width:100%;font-size:12px;"">")
        html.AppendLine("<thead><tr style=""background:#dfe9f5;color:#183252;"">")
        For Each header In {"TARİH / SAAT", "HATALI ADET / MİKTAR", "SORUMLU", "ÜRÜN ADI VE KODU", "SORUN", "ALINAN AKSİYON", "SARI KART", "KALIP TADİLAT", "HATA RAPORU", "TEST"}
            html.AppendLine("<th style=""border:1px solid #aab8ca;padding:7px;text-align:left;"">" & EncodeHtml(header) & "</th>")
        Next
        html.AppendLine("</tr></thead><tbody>")

        Dim rowIndex As Integer = 0
        For Each row In rows
            Dim backColor = If(rowIndex Mod 2 = 0, "#ffffff", "#f8fafc")
            If IsFlagSet(row, "ErrorReport") Then
                backColor = "#fff1f1"
            ElseIf IsFlagSet(row, "MoldModification") Then
                backColor = "#fff7ed"
            ElseIf IsFlagSet(row, "YellowCard") Then
                backColor = "#fffbea"
            End If

            Dim occurredAtText = DataService.GetValue(row, "OccurredAt")
            Dim occurredAt As DateTime
            If DateTime.TryParse(occurredAtText, occurredAt) Then occurredAtText = occurredAt.ToString("dd.MM.yyyy HH:mm")

            html.AppendLine("<tr style=""background:" & backColor & ";"">")
            AppendReportCell(html, occurredAtText, False)
            AppendReportCell(html, DataService.GetValue(row, "DefectiveQuantity"), False)
            AppendReportCell(html, DataService.GetValue(row, "Responsible"), False)
            AppendReportCell(html, DataService.GetValue(row, "ProductNameCode"), False)
            AppendReportCell(html, DataService.GetValue(row, "Problem"), True)
            AppendReportCell(html, DataService.GetValue(row, "ActionTaken"), True)
            AppendReportCell(html, FlagReportText(row, "YellowCard"), False)
            AppendReportCell(html, FlagReportText(row, "MoldModification"), False)
            AppendReportCell(html, FlagReportText(row, "ErrorReport"), False)
            AppendReportCell(html, FlagReportText(row, "TestPerformed"), False)
            html.AppendLine("</tr>")
            rowIndex += 1
        Next

        html.AppendLine("</tbody></table>")
        html.AppendLine("<p style=""margin-top:16px;"">Bilginize.</p>")
        html.AppendLine("</body></html>")
        Return html.ToString()
    End Function

    Private Shared Sub AppendSummaryCell(html As StringBuilder, caption As String, value As String, backColor As String, foreColor As String)
        html.AppendLine("<td style=""min-width:90px;border:1px solid #cbd5e1;padding:8px 12px;background:" & backColor & ";color:" & foreColor & ";"">" &
                        "<div style=""font-size:11px;font-weight:600;"">" & EncodeHtml(caption) & "</div>" &
                        "<div style=""font-size:18px;font-weight:700;"">" & EncodeHtml(value) & "</div></td>")
    End Sub

    Private Shared Function BuildDefectiveQuantitySummary(rows As List(Of Dictionary(Of String, String))) As String
        Dim allValues = rows.
            Select(Function(row) DataService.GetValue(row, "DefectiveQuantity").Trim()).
            Where(Function(value) value <> "").
            ToList()

        If allValues.Count = 0 Then Return "-"

        Dim numericTotal As Long = 0
        Dim allNumeric = True
        For Each value In allValues
            Dim quantity As Long
            If Long.TryParse(value, quantity) Then
                numericTotal += quantity
            Else
                allNumeric = False
                Exit For
            End If
        Next
        If allNumeric Then Return numericTotal.ToString()

        Dim values = allValues.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        Dim visibleValues = values.Take(3).ToList()
        Dim summary = String.Join(", ", visibleValues)
        If values.Count > visibleValues.Count Then summary &= " …"
        Return summary
    End Function

    Private Shared Sub AppendReportCell(html As StringBuilder, value As String, preserveLines As Boolean)
        Dim encoded = EncodeHtml(value)
        If preserveLines Then encoded = encoded.Replace(vbCrLf, "<br>").Replace(vbCr, "<br>").Replace(vbLf, "<br>")
        html.AppendLine("<td style=""border:1px solid #cbd5e1;padding:7px;vertical-align:top;"">" & encoded & "</td>")
    End Sub

    Private Shared Function FlagReportText(row As Dictionary(Of String, String), columnName As String) As String
        Return If(IsFlagSet(row, columnName), "EVET", "-")
    End Function

    Private Shared Function EncodeHtml(value As String) As String
        Return WebUtility.HtmlEncode(If(value, ""))
    End Function

    Private Sub Delete_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(CanDeleteFeature, FeatureTitle & " Kaydı Silme")
            Dim selected = SelectedRecord()
            If selected Is Nothing Then Return

            Dim recordId = DataService.GetValue(selected, "RecordId")
            Dim product = DataService.GetValue(selected, "ProductNameCode")
            Dim answer = MessageBox.Show(
                "Seçili vardiya takip kaydı kalıcı olarak silinecek." & Environment.NewLine & Environment.NewLine &
                "Kayıt No: " & recordId & Environment.NewLine &
                "Ürün: " & product & Environment.NewLine & Environment.NewLine &
                "Devam edilsin mi?",
                "Kaydı sil",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2)
            If answer <> DialogResult.Yes Then Return

            If mechanismMode Then
                DataService.DeleteMechanismShiftTrackingRecord(recordId)
            Else
                DataService.DeletePlasticShiftTrackingRecord(recordId)
            End If
            LoadGrid()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Kayıt silinemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Grid_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return
        OpenSelected(Not CanModifyFeature)
    End Sub

    Private Sub Grid_KeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode <> Keys.Enter OrElse grid.CurrentRow Is Nothing Then Return
        OpenSelected(Not CanModifyFeature)
        e.Handled = True
        e.SuppressKeyPress = True
    End Sub

    Private Sub Grid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 Then Return
        Select Case grid.Columns(e.ColumnIndex).Name
            Case "YellowCard"
                If Convert.ToBoolean(e.Value) Then e.CellStyle.BackColor = Color.FromArgb(255, 244, 179)
            Case "MoldModification"
                If Convert.ToBoolean(e.Value) Then e.CellStyle.BackColor = Color.FromArgb(255, 226, 191)
            Case "ErrorReport"
                If Convert.ToBoolean(e.Value) Then e.CellStyle.BackColor = Color.FromArgb(255, 211, 211)
            Case "TestPerformed"
                If Convert.ToBoolean(e.Value) Then e.CellStyle.BackColor = Color.FromArgb(211, 240, 219)
        End Select
    End Sub
End Class
