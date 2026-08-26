Imports System.Drawing
Imports System.Diagnostics
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmPlasticShiftTrackingDetail
    Inherits Form

    Private ReadOnly sourceRecord As Dictionary(Of String, String)
    Private ReadOnly isNewRecord As Boolean
    Private ReadOnly isReadOnlyMode As Boolean
    Private ReadOnly mechanismMode As Boolean

    Private ReadOnly dtpOccurredAt As New DateTimePicker()
    Private ReadOnly txtDefectiveQuantity As New TextBox()
    Private ReadOnly txtResponsible As New TextBox()
    Private ReadOnly cboProduct As New ComboBox()
    Private ReadOnly txtProblem As New TextBox()
    Private ReadOnly txtActionTaken As New TextBox()
    Private ReadOnly chkYellowCard As New CheckBox()
    Private ReadOnly chkMoldModification As New CheckBox()
    Private ReadOnly chkErrorReport As New CheckBox()
    Private ReadOnly chkTestPerformed As New CheckBox()
    Private ReadOnly lblMode As New Label()
    Private ReadOnly lblAudit As New Label()
    Private ReadOnly btnSave As New Button()
    Private ReadOnly btnClose As New Button()
    Private ReadOnly photoList As New ListView()
    Private ReadOnly photoImages As New ImageList()
    Private ReadOnly lblPhotoCount As New Label()
    Private ReadOnly btnAddPhoto As New Button()
    Private ReadOnly btnOpenPhoto As New Button()
    Private ReadOnly btnDeletePhoto As New Button()
    Private ReadOnly pendingPhotoPaths As New List(Of String)()
    Private isLoadingRecord As Boolean
    Private pendingMoldTicketDraft As Dictionary(Of String, String)

    Public Property SavedChanges As Boolean = False
    Public Property AffectedRecordId As String = ""

    Public Sub New(record As Dictionary(Of String, String), readOnlyMode As Boolean, Optional useMechanismMode As Boolean = False)
        mechanismMode = useMechanismMode
        sourceRecord = If(record Is Nothing,
                          Nothing,
                          New Dictionary(Of String, String)(record, StringComparer.OrdinalIgnoreCase))
        isNewRecord = sourceRecord Is Nothing
        isReadOnlyMode = readOnlyMode OrElse Not CanModifyFeature

        If isNewRecord Then
            AuthorizationService.Require(CanModifyFeature, "Yeni " & FeatureTitle & " Kaydı")
        Else
            AuthorizationService.Require(CanOpenFeature, FeatureTitle & " Detayı")
        End If

        AppIconService.Apply(Me)
        Text = If(isNewRecord, "Yeni " & FeatureTitle & " Kaydı", FeatureTitle & " Detayı")
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1100, 700)
        MinimumSize = New Size(720, 520)
        Font = New Font("Segoe UI", 9.0F)
        BackColor = Color.FromArgb(242, 246, 251)

        BuildScreen()
        LoadOptions()
        LoadRecord()
        ApplyMode()
        ResponsiveFormService.Apply(Me)
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

    Private Sub BuildScreen()
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .BackColor = BackColor
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 52.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 70.0F))
        Controls.Add(root)

        Dim header As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .BackColor = Color.FromArgb(31, 71, 126),
            .Padding = New Padding(18, 0, 14, 0)
        }
        header.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        header.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 145.0F))
        Dim title As New Label() With {
            .Dock = DockStyle.Fill,
            .Text = FeatureTitle.ToUpperInvariant() & " KAYDI",
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 13.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft
        }
        lblMode.Dock = DockStyle.Fill
        lblMode.TextAlign = ContentAlignment.MiddleCenter
        lblMode.Font = New Font("Segoe UI", 8.5F, FontStyle.Bold)
        lblMode.ForeColor = Color.White
        lblMode.BackColor = Color.FromArgb(23, 54, 98)
        lblMode.Margin = New Padding(6, 9, 0, 9)
        header.Controls.Add(title, 0, 0)
        header.Controls.Add(lblMode, 1, 0)
        root.Controls.Add(header, 0, 0)

        Dim body As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .Padding = New Padding(14, 14, 14, 12),
            .BackColor = BackColor
        }
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 38.0F))
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 62.0F))
        body.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        body.Controls.Add(BuildRecordInformationCard(), 0, 0)
        body.Controls.Add(BuildRightColumn(), 1, 0)
        root.Controls.Add(body, 0, 1)

        Dim footer As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .BackColor = Color.White,
            .Padding = New Padding(16, 10, 14, 10)
        }
        footer.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        footer.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 270.0F))
        lblAudit.Dock = DockStyle.Fill
        lblAudit.TextAlign = ContentAlignment.MiddleLeft
        lblAudit.ForeColor = Color.FromArgb(75, 88, 105)
        lblAudit.AutoEllipsis = True
        footer.Controls.Add(lblAudit, 0, 0)

        Dim buttons As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False,
            .Padding = New Padding(0)
        }
        ConfigureFooterButton(btnClose, "Kapat", 105, Color.White, Color.FromArgb(35, 55, 80))
        ConfigureFooterButton(btnSave, "Kaydet", 135, Color.FromArgb(22, 128, 70), Color.White)
        AddHandler btnClose.Click, Sub() Close()
        AddHandler btnSave.Click, AddressOf Save_Click
        buttons.Controls.Add(btnClose)
        buttons.Controls.Add(btnSave)
        footer.Controls.Add(buttons, 1, 0)
        root.Controls.Add(footer, 0, 2)
    End Sub

    Private Function BuildRecordInformationCard() As Control
        Dim content As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 8,
            .Padding = New Padding(18, 14, 18, 16),
            .BackColor = Color.White
        }
        For i As Integer = 0 To 3
            content.RowStyles.Add(New RowStyle(SizeType.Absolute, 27.0F))
            content.RowStyles.Add(New RowStyle(SizeType.Absolute, If(i = 3, 52.0F, 43.0F)))
        Next

        dtpOccurredAt.Dock = DockStyle.Top
        dtpOccurredAt.Height = 30
        dtpOccurredAt.Format = DateTimePickerFormat.Custom
        dtpOccurredAt.CustomFormat = "dd.MM.yyyy HH:mm"

        txtDefectiveQuantity.Dock = DockStyle.Top
        txtDefectiveQuantity.Height = 30
        txtDefectiveQuantity.MaxLength = 100
        txtDefectiveQuantity.PlaceholderText = "Örn: 1 Adet, 1 Koli, 1 Kutu, 1 Palet"
        txtDefectiveQuantity.BorderStyle = BorderStyle.FixedSingle

        txtResponsible.Dock = DockStyle.Top
        txtResponsible.Height = 30
        txtResponsible.MaxLength = 120

        cboProduct.Dock = DockStyle.Top
        cboProduct.Height = 32
        cboProduct.DropDownStyle = ComboBoxStyle.DropDown
        cboProduct.MaxLength = 300
        cboProduct.AutoCompleteMode = AutoCompleteMode.SuggestAppend
        cboProduct.AutoCompleteSource = AutoCompleteSource.CustomSource

        AddLabeledControl(content, 0, "Tarih / Saat (otomatik)", dtpOccurredAt)
        AddLabeledControl(content, 2, "Hatalı Adet / Miktar", txtDefectiveQuantity)
        AddLabeledControl(content, 4, "Sorumlu", txtResponsible)
        AddLabeledControl(content, 6, "Ürün Adı ve Kodu", cboProduct)
        Return WrapCard("Kayıt Bilgileri", content, New Padding(0, 0, 8, 0))
    End Function

    Private Function BuildProblemActionCard() As Control
        Dim content As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 5,
            .Padding = New Padding(18, 14, 18, 16),
            .BackColor = Color.White
        }
        content.RowStyles.Add(New RowStyle(SizeType.Absolute, 27.0F))
        content.RowStyles.Add(New RowStyle(SizeType.Percent, 43.0F))
        content.RowStyles.Add(New RowStyle(SizeType.Absolute, 27.0F))
        content.RowStyles.Add(New RowStyle(SizeType.Percent, 43.0F))
        content.RowStyles.Add(New RowStyle(SizeType.Absolute, 82.0F))

        txtProblem.Dock = DockStyle.Fill
        txtProblem.Multiline = True
        txtProblem.ScrollBars = ScrollBars.Vertical
        txtProblem.MaxLength = 4000
        txtProblem.BackColor = Color.White
        txtProblem.Margin = New Padding(0, 0, 0, 8)

        txtActionTaken.Dock = DockStyle.Fill
        txtActionTaken.Multiline = True
        txtActionTaken.ScrollBars = ScrollBars.Vertical
        txtActionTaken.MaxLength = 4000
        txtActionTaken.BackColor = Color.White
        txtActionTaken.Margin = New Padding(0, 0, 0, 10)

        content.Controls.Add(CreateFieldLabel("Sorun"), 0, 0)
        content.Controls.Add(txtProblem, 0, 1)
        content.Controls.Add(CreateFieldLabel("Alınan Aksiyon"), 0, 2)
        content.Controls.Add(txtActionTaken, 0, 3)
        content.Controls.Add(BuildFlagsPanel(), 0, 4)
        Return WrapCard("Sorun ve Aksiyon", content, New Padding(0))
    End Function

    Private Function BuildRightColumn() As Control
        Dim column As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .Margin = New Padding(8, 0, 0, 0),
            .BackColor = BackColor
        }
        column.RowStyles.Add(New RowStyle(SizeType.Percent, 65.0F))
        column.RowStyles.Add(New RowStyle(SizeType.Percent, 35.0F))

        Dim problemCard = BuildProblemActionCard()
        problemCard.Margin = New Padding(0, 0, 0, 8)
        column.Controls.Add(problemCard, 0, 0)
        column.Controls.Add(BuildPhotoCard(), 0, 1)
        Return column
    End Function

    Private Function BuildPhotoCard() As Control
        Dim content As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .Padding = New Padding(12, 8, 12, 10),
            .BackColor = Color.White
        }
        content.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        content.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim toolbar As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 4,
            .RowCount = 1,
            .Margin = New Padding(0)
        }
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 115.0F))
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 100.0F))
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 95.0F))

        lblPhotoCount.Dock = DockStyle.Fill
        lblPhotoCount.TextAlign = ContentAlignment.MiddleLeft
        lblPhotoCount.ForeColor = Color.FromArgb(75, 88, 105)
        lblPhotoCount.Font = New Font("Segoe UI", 8.5F, FontStyle.Bold)

        ConfigurePhotoButton(btnAddPhoto, "Fotoğraf Ekle", Color.FromArgb(31, 71, 126), Color.White)
        ConfigurePhotoButton(btnOpenPhoto, "Görüntüle", Color.White, Color.FromArgb(31, 71, 126))
        ConfigurePhotoButton(btnDeletePhoto, "Sil", Color.FromArgb(255, 238, 238), Color.FromArgb(170, 25, 25))
        AddHandler btnAddPhoto.Click, AddressOf AddPhoto_Click
        AddHandler btnOpenPhoto.Click, AddressOf OpenPhoto_Click
        AddHandler btnDeletePhoto.Click, AddressOf DeletePhoto_Click

        toolbar.Controls.Add(lblPhotoCount, 0, 0)
        toolbar.Controls.Add(btnAddPhoto, 1, 0)
        toolbar.Controls.Add(btnOpenPhoto, 2, 0)
        toolbar.Controls.Add(btnDeletePhoto, 3, 0)
        content.Controls.Add(toolbar, 0, 0)

        photoImages.ColorDepth = ColorDepth.Depth32Bit
        photoImages.ImageSize = New Size(104, 76)
        photoImages.TransparentColor = Color.Transparent

        photoList.Dock = DockStyle.Fill
        photoList.View = View.LargeIcon
        photoList.LargeImageList = photoImages
        photoList.MultiSelect = False
        photoList.HideSelection = False
        photoList.BorderStyle = BorderStyle.FixedSingle
        photoList.BackColor = Color.FromArgb(249, 251, 254)
        photoList.Margin = New Padding(0, 4, 0, 0)
        AddHandler photoList.DoubleClick, AddressOf OpenPhoto_Click
        content.Controls.Add(photoList, 0, 1)

        Return WrapCard("Fotoğraflar", content, New Padding(0))
    End Function

    Private Function BuildFlagsPanel() As Control
        Dim flags As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 4,
            .RowCount = 1,
            .Padding = New Padding(0, 6, 0, 0),
            .BackColor = Color.White
        }
        For i As Integer = 0 To 3
            flags.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25.0F))
        Next

        ConfigureFlag(chkYellowCard, "SARI KART", Color.FromArgb(255, 244, 179))
        ConfigureFlag(chkMoldModification, "KALIP TADİLAT", Color.FromArgb(255, 226, 191))
        AddHandler chkMoldModification.CheckedChanged, AddressOf MoldModification_CheckedChanged
        ConfigureFlag(chkErrorReport, "HATA RAPORU", Color.FromArgb(255, 211, 211))
        AddHandler chkErrorReport.Click, AddressOf ErrorReport_Click
        ConfigureFlag(chkTestPerformed, "TEST", Color.FromArgb(211, 240, 219))
        flags.Controls.Add(chkYellowCard, 0, 0)
        flags.Controls.Add(chkMoldModification, 1, 0)
        flags.Controls.Add(chkErrorReport, 2, 0)
        flags.Controls.Add(chkTestPerformed, 3, 0)
        If mechanismMode Then
            chkMoldModification.Visible = False
            chkErrorReport.Visible = False
        End If
        Return flags
    End Function

    Private Shared Sub AddLabeledControl(layout As TableLayoutPanel, labelRow As Integer, caption As String, control As Control)
        layout.Controls.Add(CreateFieldLabel(caption), 0, labelRow)
        control.Margin = New Padding(0, 0, 0, 8)
        layout.Controls.Add(control, 0, labelRow + 1)
    End Sub

    Private Shared Function CreateFieldLabel(caption As String) As Label
        Return New Label() With {
            .Text = caption,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(40, 58, 82),
            .Margin = New Padding(0)
        }
    End Function

    Private Shared Function WrapCard(titleText As String, content As Control, margin As Padding) As Control
        Dim card As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .BackColor = Color.White,
            .Margin = margin,
            .CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
        }
        card.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        card.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Dim header As New Label() With {
            .Dock = DockStyle.Fill,
            .Text = titleText,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(14, 0, 0, 0),
            .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(31, 71, 126),
            .BackColor = Color.FromArgb(231, 238, 248),
            .Margin = New Padding(0)
        }
        card.Controls.Add(header, 0, 0)
        card.Controls.Add(content, 0, 1)
        Return card
    End Function

    Private Shared Sub ConfigureFlag(checkBox As CheckBox, caption As String, checkedColor As Color)
        checkBox.Dock = DockStyle.Fill
        checkBox.Text = caption
        checkBox.Appearance = Appearance.Button
        checkBox.TextAlign = ContentAlignment.MiddleCenter
        checkBox.Font = New Font("Segoe UI", 8.5F, FontStyle.Bold)
        checkBox.FlatStyle = FlatStyle.Flat
        checkBox.FlatAppearance.BorderColor = Color.FromArgb(178, 190, 205)
        checkBox.FlatAppearance.CheckedBackColor = checkedColor
        checkBox.BackColor = Color.White
        checkBox.Margin = New Padding(3)
        checkBox.AutoEllipsis = False
    End Sub

    Private Shared Sub ConfigureFooterButton(button As Button, caption As String, width As Integer, backColor As Color, foreColor As Color)
        button.Text = caption
        button.Width = width
        button.Height = 38
        button.Margin = New Padding(8, 0, 0, 0)
        button.FlatStyle = FlatStyle.Flat
        button.FlatAppearance.BorderColor = Color.FromArgb(178, 190, 205)
        button.BackColor = backColor
        button.ForeColor = foreColor
        button.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        button.AutoEllipsis = False
        button.Tag = "RESPONSIVE_NO_AUTO_SCALE"
    End Sub

    Private Shared Sub ConfigurePhotoButton(button As Button, caption As String, backColor As Color, foreColor As Color)
        button.Dock = DockStyle.Fill
        button.Text = caption
        button.Height = 31
        button.Margin = New Padding(6, 2, 0, 3)
        button.FlatStyle = FlatStyle.Flat
        button.FlatAppearance.BorderColor = Color.FromArgb(178, 190, 205)
        button.BackColor = backColor
        button.ForeColor = foreColor
        button.Font = New Font("Segoe UI", 8.5F, FontStyle.Bold)
        button.AutoEllipsis = False
        button.Tag = "RESPONSIVE_NO_AUTO_SCALE"
    End Sub

    Private Sub LoadOptions()
        If isReadOnlyMode Then Return

        Try
            Dim productOptions = DataService.GetProducts(False).
                Select(Function(product)
                           Dim parts = New List(Of String) From {product.TrCode, product.ProductName}
                           If product.PlasticCode.Trim() <> "" Then parts.Add(product.PlasticCode)
                           Return String.Join(" | ", parts.Where(Function(part) Not String.IsNullOrWhiteSpace(part)))
                       End Function).
                Where(Function(value) value <> "").
                Distinct(StringComparer.OrdinalIgnoreCase).
                OrderBy(Function(value) value).
                ToArray()
            cboProduct.Items.AddRange(productOptions)
            Dim productSource As New AutoCompleteStringCollection()
            productSource.AddRange(productOptions)
            cboProduct.AutoCompleteCustomSource = productSource
        Catch ex As Exception
            ErrorLogService.Log("FrmPlasticShiftTrackingDetail.LoadProducts", ex)
        End Try

        Try
            Dim users = UserService.GetUsers().
                Where(Function(row) String.Equals(DataService.GetValue(row, "IsActive"), "YES", StringComparison.OrdinalIgnoreCase)).
                Select(Function(row) DataService.GetValue(row, "Username").Trim()).
                Where(Function(value) value <> "").
                Distinct(StringComparer.OrdinalIgnoreCase).
                OrderBy(Function(value) value).
                ToArray()
            Dim userSource As New AutoCompleteStringCollection()
            userSource.AddRange(users)
            txtResponsible.AutoCompleteMode = AutoCompleteMode.SuggestAppend
            txtResponsible.AutoCompleteSource = AutoCompleteSource.CustomSource
            txtResponsible.AutoCompleteCustomSource = userSource
        Catch ex As Exception
            ErrorLogService.Log("FrmPlasticShiftTrackingDetail.LoadUsers", ex)
        End Try
    End Sub

    Private Sub LoadRecord()
        isLoadingRecord = True
        Try
            If isNewRecord Then
                dtpOccurredAt.Value = DateTime.Now
                txtDefectiveQuantity.Text = "1 Adet"
                txtResponsible.Text = AppState.CurrentUserName
                lblAudit.Text = "Yeni kayıt"
                Return
            End If

            Dim occurredAt As DateTime
            If DateTime.TryParse(DataService.GetValue(sourceRecord, "OccurredAt"), occurredAt) Then
                dtpOccurredAt.Value = occurredAt
            Else
                dtpOccurredAt.Value = DateTime.Now
            End If

            txtDefectiveQuantity.Text = DataService.GetValue(sourceRecord, "DefectiveQuantity")

            txtResponsible.Text = DataService.GetValue(sourceRecord, "Responsible")
            cboProduct.Text = DataService.GetValue(sourceRecord, "ProductNameCode")
            txtProblem.Text = DataService.GetValue(sourceRecord, "Problem")
            txtActionTaken.Text = DataService.GetValue(sourceRecord, "ActionTaken")
            chkYellowCard.Checked = IsFlagSet("YellowCard")
            chkMoldModification.Checked = Not mechanismMode AndAlso IsFlagSet("MoldModification")
            chkErrorReport.Checked = Not mechanismMode AndAlso IsFlagSet("ErrorReport")
            chkTestPerformed.Checked = IsFlagSet("TestPerformed")
            AffectedRecordId = DataService.GetValue(sourceRecord, "RecordId")

            lblAudit.Text = "Kayıt No: " & AffectedRecordId &
                            "   |   Oluşturan: " & EmptyAsDash(DataService.GetValue(sourceRecord, "CreatedBy")) &
                            " / " & EmptyAsDash(DataService.GetValue(sourceRecord, "CreatedAt")) &
                            "   |   Son güncelleyen: " & EmptyAsDash(DataService.GetValue(sourceRecord, "UpdatedBy")) &
                            " / " & EmptyAsDash(DataService.GetValue(sourceRecord, "UpdatedAt"))
        Finally
            isLoadingRecord = False
            RefreshPhotoList()
        End Try
    End Sub

    Private Sub RefreshPhotoList()
        Try
            photoList.BeginUpdate()
            photoList.Items.Clear()
            photoImages.Images.Clear()

            If AffectedRecordId.Trim() <> "" Then
                For Each row In ShiftTrackingPhotoService.GetPhotos(AffectedRecordId, mechanismMode)
                    Dim fullPath = ShiftTrackingPhotoService.ResolvePhotoPath(row)
                    AddPhotoListItem(
                        DataService.GetValue(row, "OriginalFileName"),
                        DataService.GetValue(row, "AddedBy") & " · " & DataService.GetValue(row, "AddedAt"),
                        fullPath,
                        row)
                Next
            End If

            For Each pendingPath In pendingPhotoPaths.ToArray()
                If Not File.Exists(pendingPath) Then
                    pendingPhotoPaths.Remove(pendingPath)
                    Continue For
                End If
                Dim pendingRow As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                    {"Pending", "YES"},
                    {"FullPath", pendingPath},
                    {"OriginalFileName", Path.GetFileName(pendingPath)}
                }
                AddPhotoListItem(Path.GetFileName(pendingPath), "Kayıtla birlikte eklenecek", pendingPath, pendingRow)
            Next

            lblPhotoCount.Text = photoList.Items.Count.ToString() & " fotoğraf" &
                                 If(pendingPhotoPaths.Count > 0, " · " & pendingPhotoPaths.Count.ToString() & " bekliyor", "")
            btnOpenPhoto.Enabled = photoList.Items.Count > 0
            btnDeletePhoto.Enabled = Not isReadOnlyMode AndAlso photoList.Items.Count > 0
        Catch ex As Exception
            ErrorLogService.Log("FrmPlasticShiftTrackingDetail.RefreshPhotos", ex)
            lblPhotoCount.Text = "Fotoğraflar yüklenemedi"
        Finally
            photoList.EndUpdate()
        End Try
    End Sub

    Private Sub AddPhotoListItem(fileName As String,
                                 detail As String,
                                 fullPath As String,
                                 row As Dictionary(Of String, String))
        Dim imageKey = Guid.NewGuid().ToString("N")
        photoImages.Images.Add(imageKey, CreatePhotoThumbnail(fullPath))
        Dim displayName = If(String.IsNullOrWhiteSpace(fileName), "Fotoğraf", fileName.Trim())
        Dim item As New ListViewItem(displayName & Environment.NewLine & detail, imageKey) With {
            .Tag = row,
            .ToolTipText = fullPath
        }
        photoList.Items.Add(item)
    End Sub

    Private Shared Function CreatePhotoThumbnail(fullPath As String) As Image
        Try
            If fullPath <> "" AndAlso File.Exists(fullPath) Then
                Using source = Image.FromFile(fullPath)
                    Using copy As New Bitmap(source)
                        Return copy.GetThumbnailImage(104, 76, Nothing, IntPtr.Zero)
                    End Using
                End Using
            End If
        Catch
        End Try

        Dim placeholder As New Bitmap(104, 76)
        Using canvas = Graphics.FromImage(placeholder)
            canvas.Clear(Color.FromArgb(231, 238, 248))
            Using pen As New Pen(Color.FromArgb(140, 158, 181), 2.0F)
                canvas.DrawRectangle(pen, 12, 10, 79, 55)
                canvas.DrawLine(pen, 16, 57, 42, 34)
                canvas.DrawLine(pen, 42, 34, 58, 49)
                canvas.DrawLine(pen, 58, 49, 74, 29)
                canvas.DrawEllipse(pen, 70, 17, 10, 10)
            End Using
        End Using
        Return placeholder
    End Function

    Private Sub AddPhoto_Click(sender As Object, e As EventArgs)
        If isReadOnlyMode Then Return
        Using dialog As New OpenFileDialog() With {
            .Title = "Vardiya takip fotoğrafı seçin",
            .Filter = "Fotoğraf dosyaları|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Tüm dosyalar|*.*",
            .Multiselect = True,
            .CheckFileExists = True
        }
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return

            Try
                If AffectedRecordId.Trim() = "" Then
                    For Each filePath In dialog.FileNames
                        If Not pendingPhotoPaths.Contains(filePath, StringComparer.OrdinalIgnoreCase) Then
                            pendingPhotoPaths.Add(filePath)
                        End If
                    Next
                Else
                    For Each filePath In dialog.FileNames
                        ShiftTrackingPhotoService.AddPhoto(AffectedRecordId, mechanismMode, filePath)
                    Next
                End If
                RefreshPhotoList()
            Catch ex As Exception
                MessageBox.Show(ex.Message, "Fotoğraf eklenemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End Try
        End Using
    End Sub

    Private Sub OpenPhoto_Click(sender As Object, e As EventArgs)
        If photoList.SelectedItems.Count = 0 Then
            If photoList.Items.Count = 1 Then photoList.Items(0).Selected = True Else Return
        End If

        Try
            Dim row = TryCast(photoList.SelectedItems(0).Tag, Dictionary(Of String, String))
            If row Is Nothing Then Return
            Dim fullPath = If(String.Equals(DataService.GetValue(row, "Pending"), "YES", StringComparison.OrdinalIgnoreCase),
                              DataService.GetValue(row, "FullPath"),
                              ShiftTrackingPhotoService.ResolvePhotoPath(row))
            If fullPath = "" OrElse Not File.Exists(fullPath) Then
                MessageBox.Show("Fotoğraf dosyası bulunamadı.", "Fotoğraf", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If
            Process.Start(New ProcessStartInfo(fullPath) With {.UseShellExecute = True})
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Fotoğraf açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub DeletePhoto_Click(sender As Object, e As EventArgs)
        If isReadOnlyMode OrElse photoList.SelectedItems.Count = 0 Then Return
        Dim row = TryCast(photoList.SelectedItems(0).Tag, Dictionary(Of String, String))
        If row Is Nothing Then Return

        If MessageBox.Show(
            "Seçili fotoğraf silinsin mi?",
            "Fotoğrafı sil",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then Return

        Try
            If String.Equals(DataService.GetValue(row, "Pending"), "YES", StringComparison.OrdinalIgnoreCase) Then
                pendingPhotoPaths.RemoveAll(Function(path) String.Equals(path, DataService.GetValue(row, "FullPath"), StringComparison.OrdinalIgnoreCase))
            Else
                ShiftTrackingPhotoService.DeletePhoto(
                    DataService.GetValue(row, "PhotoId"),
                    AffectedRecordId,
                    mechanismMode)
            End If
            RefreshPhotoList()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Fotoğraf silinemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Function SavePendingPhotos() As String
        If pendingPhotoPaths.Count = 0 Then Return ""

        Dim failures As New List(Of String)()
        For Each filePath In pendingPhotoPaths.ToArray()
            Try
                ShiftTrackingPhotoService.AddPhoto(AffectedRecordId, mechanismMode, filePath)
                pendingPhotoPaths.Remove(filePath)
            Catch ex As Exception
                failures.Add(Path.GetFileName(filePath) & ": " & ex.Message)
            End Try
        Next
        Return String.Join(Environment.NewLine, failures)
    End Function

    Private Sub MoldModification_CheckedChanged(sender As Object, e As EventArgs)
        If mechanismMode Then Return
        If isLoadingRecord OrElse isReadOnlyMode Then Return

        Try
            If Not chkMoldModification.Checked Then
                pendingMoldTicketDraft = Nothing
                Return
            End If

            If EnsureMoldTicketDraft() Then Return

            isLoadingRecord = True
            chkMoldModification.Checked = False
        Catch ex As Exception
            ErrorLogService.Log("FrmPlasticShiftTrackingDetail.MoldModification", ex)
            isLoadingRecord = True
            chkMoldModification.Checked = False
            MessageBox.Show(ex.Message, "Kalıp Ticketı açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Finally
            isLoadingRecord = False
        End Try
    End Sub

    Private Function EnsureMoldTicketDraft() As Boolean
        If Not chkMoldModification.Checked Then Return True
        If pendingMoldTicketDraft IsNot Nothing Then Return True

        If AffectedRecordId.Trim() <> "" AndAlso
           DataService.GetMoldTicketIdForPlasticShift(AffectedRecordId).Trim() <> "" Then
            Return True
        End If

        Using ticketForm As New FrmMoldTicketCreateFromPlasticShift(
            AffectedRecordId,
            cboProduct.Text.Trim(),
            txtProblem.Text.Trim(),
            txtActionTaken.Text.Trim())

            If ticketForm.ShowDialog(Me) <> DialogResult.OK OrElse ticketForm.TicketDraft Is Nothing Then
                Return False
            End If
            pendingMoldTicketDraft = ticketForm.TicketDraft
        End Using
        Return True
    End Function

    Private Function IsFlagSet(columnName As String) As Boolean
        Dim value = DataService.GetValue(sourceRecord, columnName).Trim().ToUpperInvariant()
        Return value = "YES" OrElse value = "EVET" OrElse value = "TRUE" OrElse value = "1" OrElse value = "X"
    End Function

    Private Shared Function EmptyAsDash(value As String) As String
        Return If(String.IsNullOrWhiteSpace(value), "-", value.Trim())
    End Function

    Private Sub ApplyMode()
        Dim editable = Not isReadOnlyMode
        dtpOccurredAt.Enabled = False
        txtDefectiveQuantity.ReadOnly = Not editable
        txtDefectiveQuantity.BackColor = If(editable, Color.White, Color.FromArgb(245, 247, 250))
        txtResponsible.ReadOnly = Not editable
        cboProduct.Enabled = editable
        txtProblem.ReadOnly = Not editable
        txtActionTaken.ReadOnly = Not editable
        For Each flag In {chkYellowCard, chkMoldModification, chkErrorReport, chkTestPerformed}
            flag.Enabled = True
            flag.AutoCheck = editable
            flag.TabStop = editable
        Next
        ' Hata raporu sıradan bir işaret değil, bağlı bir iş akışıdır.
        ' İşareti kaldırmak yerine tıklandığında rapor ekranını açar.
        chkErrorReport.AutoCheck = False
        chkErrorReport.TabStop = True
        btnAddPhoto.Visible = editable
        btnDeletePhoto.Visible = editable
        btnAddPhoto.Enabled = editable
        btnDeletePhoto.Enabled = editable AndAlso photoList.SelectedItems.Count > 0
        btnOpenPhoto.Visible = True
        btnOpenPhoto.Enabled = photoList.Items.Count > 0
        btnSave.Visible = editable
        btnClose.Text = If(editable, "Vazgeç", "Kapat")

        If isNewRecord Then
            lblMode.Text = "YENİ KAYIT"
        ElseIf editable Then
            lblMode.Text = "DÜZENLEME"
        Else
            lblMode.Text = "SALT OKUNUR"
        End If
    End Sub

    Private Sub ErrorReport_Click(sender As Object, e As EventArgs)
        If mechanismMode Then Return
        Try
            If AffectedRecordId.Trim() = "" AndAlso sourceRecord IsNot Nothing Then
                AffectedRecordId = DataService.GetValue(sourceRecord, "RecordId").Trim()
            End If

            If AffectedRecordId.Trim() = "" Then
                If Not isReadOnlyMode Then
                    chkErrorReport.Checked = True
                    MessageBox.Show(
                        "Vardiya kaydını kaydettiğinizde hata raporu ekranı otomatik açılacaktır.",
                        "Hata raporu",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information)
                End If
                Return
            End If

            Dim savedRow = DataService.GetPlasticShiftTrackingRecords().
                FirstOrDefault(Function(item) String.Equals(
                    DataService.GetValue(item, "RecordId"),
                    AffectedRecordId,
                    StringComparison.OrdinalIgnoreCase))
            If savedRow Is Nothing Then Return

            Dim existing = DataService.GetPlasticShiftErrorReport(AffectedRecordId)
            If existing Is Nothing AndAlso Not AppState.CanCreatePlasticShiftErrorReport Then
                MessageBox.Show(
                    "Bu vardiya kaydı için henüz hata raporu oluşturulmamış.",
                    "Hata raporu",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information)
                Return
            End If

            Using reportForm As New FrmPlasticShiftErrorReport(savedRow)
                reportForm.ShowDialog(Me)
                If reportForm.SavedChanges Then chkErrorReport.Checked = True
            End Using
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Hata raporu açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(CanModifyFeature, FeatureTitle & " Kaydı")
            If Not mechanismMode AndAlso chkMoldModification.Checked AndAlso Not EnsureMoldTicketDraft() Then Return
            Dim shouldSendNewRecordEmail = isNewRecord
            Dim row = If(sourceRecord Is Nothing,
                         New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase),
                         New Dictionary(Of String, String)(sourceRecord, StringComparer.OrdinalIgnoreCase))
            row("RecordId") = If(AffectedRecordId.Trim() <> "",
                                 AffectedRecordId,
                                 If(sourceRecord Is Nothing, "", DataService.GetValue(sourceRecord, "RecordId")))
            row("OccurredAt") = If(sourceRecord Is Nothing,
                                   DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                   DataService.GetValue(sourceRecord, "OccurredAt"))
            row("DefectiveQuantity") = txtDefectiveQuantity.Text.Trim()
            row("Responsible") = txtResponsible.Text.Trim()
            row("ProductNameCode") = cboProduct.Text.Trim()
            row("Problem") = txtProblem.Text.Trim()
            row("ActionTaken") = txtActionTaken.Text.Trim()
            row("YellowCard") = If(chkYellowCard.Checked, "YES", "NO")
            row("MoldModification") = If(Not mechanismMode AndAlso chkMoldModification.Checked, "YES", "NO")
            row("ErrorReport") = If(Not mechanismMode AndAlso chkErrorReport.Checked, "YES", "NO")
            row("TestPerformed") = If(chkTestPerformed.Checked, "YES", "NO")

            If mechanismMode Then
                AffectedRecordId = DataService.SaveMechanismShiftTrackingRecord(row)
            Else
                AffectedRecordId = DataService.SavePlasticShiftTrackingRecord(row)
            End If
            Dim photoSaveErrors = SavePendingPhotos()
            If Not mechanismMode AndAlso chkMoldModification.Checked AndAlso pendingMoldTicketDraft IsNot Nothing Then
                DataService.CreateMoldTicketFromPlasticShift(AffectedRecordId, pendingMoldTicketDraft)
            End If
            If Not mechanismMode AndAlso chkErrorReport.Checked AndAlso DataService.GetPlasticShiftErrorReport(AffectedRecordId) Is Nothing Then
                Dim savedShift = DataService.GetPlasticShiftTrackingRecords().
                    FirstOrDefault(Function(item) String.Equals(
                        DataService.GetValue(item, "RecordId"),
                        AffectedRecordId,
                        StringComparison.OrdinalIgnoreCase))
                If savedShift IsNot Nothing Then
                    Using reportForm As New FrmPlasticShiftErrorReport(savedShift)
                        reportForm.ShowDialog(Me)
                    End Using
                End If
            End If
            If shouldSendNewRecordEmail Then
                Dim savedRows = If(mechanismMode,
                                   DataService.GetMechanismShiftTrackingRecords(),
                                   DataService.GetPlasticShiftTrackingRecords())
                Dim savedRow = savedRows.
                    FirstOrDefault(Function(item) String.Equals(DataService.GetValue(item, "RecordId"), AffectedRecordId, StringComparison.OrdinalIgnoreCase))
                If savedRow IsNot Nothing Then
                    Dim emailError As String = ""
                    If Not PlasticShiftEmailNotificationService.TryNotifyNewRecord(savedRow, emailError, mechanismMode) Then
                        MessageBox.Show(
                            "Kayıt kaydedildi ancak otomatik e-posta gönderilemedi." & Environment.NewLine & Environment.NewLine & emailError,
                            "Otomatik e-posta",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning)
                    End If
                End If
            End If
            If photoSaveErrors <> "" Then
                MessageBox.Show(
                    "Kayıt kaydedildi ancak bazı fotoğraflar eklenemedi:" &
                    Environment.NewLine & Environment.NewLine & photoSaveErrors,
                    "Fotoğraf ekleme",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning)
            End If
            SavedChanges = True
            DialogResult = DialogResult.OK
            Close()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Kayıt kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub
End Class
