Imports System.Data
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Windows.Forms

Public Class FrmPermissionMatrix
    Inherits Form

    Private NotInheritable Class ScreenDefinition
        Public ReadOnly Property Category As String
        Public ReadOnly Property ScreenName As String
        Public ReadOnly Property BaseColumn As String
        Public ReadOnly Property AccessRule As String

        Public Sub New(category As String,
                       screenName As String,
                       baseColumn As String,
                       Optional accessRule As String = "INHERIT")
            Me.Category = category
            Me.ScreenName = screenName
            Me.BaseColumn = baseColumn
            Me.AccessRule = accessRule
        End Sub

        Public ReadOnly Property ColumnTitle As String
            Get
                Return Category & " • " & ScreenName
            End Get
        End Property
    End Class

    Private ReadOnly grid As New DataGridView()
    Private ReadOnly cboRole As New ComboBox()
    Private ReadOnly cboPermission As New ComboBox()
    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly lblSummary As New Label()
    Private ReadOnly refreshTimer As New Timer() With {.Interval = 3000}
    Private sourceTable As New DataTable()
    Private matrixFileStamp As String = ""
    Private lastSuccessfulRefresh As DateTime?
    Private isRefreshing As Boolean

    Public Sub New()
        AuthorizationService.Require(AppState.CanViewPermissionMatrix, "Yetki Matrisi")
        AppIconService.Apply(Me)
        Text = "Yetki Matrisi"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1500, 820)
        MinimumSize = New Size(760, 520)
        Font = New Font("Segoe UI", 9.0F)
        BackColor = Color.FromArgb(244, 247, 251)

        BuildScreen()
        AddHandler refreshTimer.Tick, AddressOf RefreshTimer_Tick
        AddHandler Shown, AddressOf PermissionMatrix_Shown
        AddHandler Activated, AddressOf PermissionMatrix_Activated
        AddHandler FormClosed, AddressOf PermissionMatrix_FormClosed
        LoadMatrix(False, True)
    End Sub

    Private Sub BuildScreen()
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .Padding = New Padding(10),
            .BackColor = BackColor
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 62.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
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

        toolbar.Controls.Add(CreateToolbarLabel("Rol"))
        cboRole.Width = 220
        cboRole.Height = 28
        cboRole.DropDownStyle = ComboBoxStyle.DropDownList
        cboRole.Margin = New Padding(3, 4, 12, 4)
        AddHandler cboRole.SelectedIndexChanged, Sub() ApplyFilters()
        toolbar.Controls.Add(cboRole)

        toolbar.Controls.Add(CreateToolbarLabel("Yetki"))
        cboPermission.Width = 130
        cboPermission.Height = 28
        cboPermission.DropDownStyle = ComboBoxStyle.DropDownList
        cboPermission.Items.AddRange({"TÜMÜ", "TAM", "SINIRLI", "SALT OKUNUR", "ERİŞİM YOK"})
        cboPermission.SelectedIndex = 0
        cboPermission.Margin = New Padding(3, 4, 12, 4)
        AddHandler cboPermission.SelectedIndexChanged, Sub() ApplyFilters()
        toolbar.Controls.Add(cboPermission)

        toolbar.Controls.Add(CreateToolbarLabel("Arama"))
        txtSearch.Width = 230
        txtSearch.Height = 27
        txtSearch.PlaceholderText = "rol / ekran / yetki"
        txtSearch.Margin = New Padding(3, 5, 12, 5)
        AddHandler txtSearch.TextChanged, Sub() ApplyFilters()
        toolbar.Controls.Add(txtSearch)

        Dim btnRefresh = CreateToolbarButton("Yenile", 90)
        AddHandler btnRefresh.Click, Sub() LoadMatrix(True, True)
        toolbar.Controls.Add(btnRefresh)

        Dim btnOpenCsv = CreateToolbarButton("Tüm Ekranlar CSV", 135)
        AddHandler btnOpenCsv.Click, Sub() OpenExpandedMatrixCsv()
        toolbar.Controls.Add(btnOpenCsv)

        Dim btnOpenDocument = CreateToolbarButton("Detaylı Belgeyi Aç", 145)
        AddHandler btnOpenDocument.Click, Sub() OpenDocument(AppPaths.PermissionMatrixMarkdown)
        toolbar.Controls.Add(btnOpenDocument)

        Dim adjustToolbarHeight As Action =
            Sub()
                If root.IsDisposed OrElse toolbar.IsDisposed Then Return
                Dim availableWidth = Math.Max(320, root.ClientSize.Width - root.Padding.Horizontal)
                Dim preferredHeight = toolbar.GetPreferredSize(New Size(availableWidth, 0)).Height
                root.RowStyles(0).Height = CSng(Math.Max(62, Math.Min(150, preferredHeight + 2)))
                toolbar.AutoScroll = preferredHeight > 148
            End Sub
        AddHandler root.ClientSizeChanged, Sub(sender, e) adjustToolbarHeight()
        AddHandler Shown, Sub(sender, e) adjustToolbarHeight()

        Dim legend As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 5,
            .RowCount = 1,
            .BackColor = Color.White,
            .Margin = New Padding(0, 0, 0, 6),
            .Padding = New Padding(8, 4, 8, 4)
        }
        legend.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 28.0F))
        legend.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 18.0F))
        legend.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 18.0F))
        legend.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 18.0F))
        legend.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 18.0F))
        legend.Controls.Add(New Label() With {
            .Text = "Bu pencere salt okunurdur. Hücreye çift tıklayarak tam açıklamayı görebilirsiniz.",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .ForeColor = Color.FromArgb(55, 70, 90),
            .AutoEllipsis = True
        }, 0, 0)
        legend.Controls.Add(CreateLegendLabel("TAM", Color.FromArgb(217, 240, 222), Color.FromArgb(22, 101, 52)), 1, 0)
        legend.Controls.Add(CreateLegendLabel("SINIRLI", Color.FromArgb(255, 235, 196), Color.FromArgb(137, 81, 0)), 2, 0)
        legend.Controls.Add(CreateLegendLabel("SALT OKUNUR", Color.FromArgb(220, 233, 247), Color.FromArgb(31, 71, 126)), 3, 0)
        legend.Controls.Add(CreateLegendLabel("ERİŞİM YOK", Color.FromArgb(238, 240, 243), Color.FromArgb(100, 108, 118)), 4, 0)
        root.Controls.Add(legend, 0, 1)

        ConfigureGrid()
        root.Controls.Add(grid, 0, 2)

        Dim footer As New Panel() With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(231, 238, 248),
            .Padding = New Padding(12, 0, 12, 0),
            .Margin = New Padding(0, 6, 0, 0)
        }
        lblSummary.Dock = DockStyle.Fill
        lblSummary.TextAlign = ContentAlignment.MiddleLeft
        lblSummary.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblSummary.ForeColor = Color.FromArgb(31, 71, 126)
        footer.Controls.Add(lblSummary)
        root.Controls.Add(footer, 0, 3)
    End Sub

    Private Shared Function CreateToolbarLabel(caption As String) As Label
        Return New Label() With {
            .Text = caption,
            .Width = If(caption = "Arama", 50, 42),
            .Height = 34,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .Margin = New Padding(4, 1, 3, 1)
        }
    End Function

    Private Shared Function CreateToolbarButton(caption As String, width As Integer) As Button
        Dim button As New Button() With {
            .Text = caption,
            .Width = width,
            .Height = 34,
            .Margin = New Padding(4, 1, 4, 1),
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.White,
            .ForeColor = Color.FromArgb(35, 55, 80),
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .Cursor = Cursors.Hand,
            .AutoEllipsis = False,
            .Tag = "RESPONSIVE_NO_AUTO_SCALE"
        }
        button.FlatAppearance.BorderColor = Color.FromArgb(190, 201, 215)
        Return button
    End Function

    Private Shared Function CreateLegendLabel(caption As String, backColor As Color, foreColor As Color) As Label
        Return New Label() With {
            .Text = caption,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleCenter,
            .BackColor = backColor,
            .ForeColor = foreColor,
            .Font = New Font("Segoe UI", 8.5F, FontStyle.Bold),
            .Margin = New Padding(4, 0, 4, 0)
        }
    End Function

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.Margin = New Padding(0)
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.AllowUserToResizeRows = False
        grid.MultiSelect = False
        grid.SelectionMode = DataGridViewSelectionMode.CellSelect
        grid.AutoGenerateColumns = True
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
        grid.RowHeadersVisible = False
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.GridColor = Color.FromArgb(205, 213, 223)
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersHeight = 54
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(226, 234, 244)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(24, 50, 82)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
        grid.DefaultCellStyle.Font = New Font("Segoe UI", 9.0F)
        grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(196, 220, 248)
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 40, 65)
        grid.RowTemplate.Height = 52
        grid.ScrollBars = ScrollBars.Both
        AddHandler grid.CellFormatting, AddressOf Grid_CellFormatting
        AddHandler grid.CellDoubleClick, AddressOf Grid_CellDoubleClick
        AddHandler grid.CellToolTipTextNeeded, AddressOf Grid_CellToolTipTextNeeded
    End Sub

    Private Sub PermissionMatrix_Shown(sender As Object, e As EventArgs)
        RefreshMatrixIfChanged(False, False)
        refreshTimer.Start()
    End Sub

    Private Sub PermissionMatrix_Activated(sender As Object, e As EventArgs)
        RefreshMatrixIfChanged(False, False)
    End Sub

    Private Sub PermissionMatrix_FormClosed(sender As Object, e As FormClosedEventArgs)
        refreshTimer.Stop()
        refreshTimer.Dispose()
    End Sub

    Private Sub RefreshTimer_Tick(sender As Object, e As EventArgs)
        If Not Visible OrElse WindowState = FormWindowState.Minimized Then Return
        RefreshMatrixIfChanged(False, False)
    End Sub

    Private Sub RefreshMatrixIfChanged(force As Boolean, showError As Boolean)
        If isRefreshing OrElse IsDisposed Then Return

        Dim currentStamp = GetMatrixFileStamp()
        If force OrElse Not String.Equals(currentStamp, matrixFileStamp, StringComparison.Ordinal) Then
            LoadMatrix(True, showError)
        End If
    End Sub

    Private Shared Function GetMatrixFileStamp() As String
        Try
            If Not File.Exists(AppPaths.PermissionMatrixCsv) Then Return "MISSING"
            Dim info As New FileInfo(AppPaths.PermissionMatrixCsv)
            Return info.LastWriteTimeUtc.Ticks.ToString() & ":" & info.Length.ToString()
        Catch
            Return "UNAVAILABLE"
        End Try
    End Function

    Private Sub LoadMatrix(Optional preserveView As Boolean = True, Optional showError As Boolean = True)
        If isRefreshing Then Return

        Dim selectedRole = ""
        Dim selectedColumn = ""
        Dim firstDisplayedRow = -1
        Dim firstDisplayedColumn = -1

        If preserveView AndAlso grid.CurrentCell IsNot Nothing Then
            selectedRole = Convert.ToString(grid.Rows(grid.CurrentCell.RowIndex).Cells(0).Value)
            selectedColumn = grid.Columns(grid.CurrentCell.ColumnIndex).HeaderText
            Try
                firstDisplayedRow = grid.FirstDisplayedScrollingRowIndex
                firstDisplayedColumn = grid.FirstDisplayedScrollingColumnIndex
            Catch
                ' Görünüm bilgisi alınamazsa yenileme yine de devam eder.
            End Try
        End If

        isRefreshing = True
        grid.SuspendLayout()
        Try
            If Not File.Exists(AppPaths.PermissionMatrixCsv) Then
                Throw New FileNotFoundException("Yetki matrisi CSV dosyası bulunamadı. Güncelleme paketini yeniden uygulayın.")
            End If

            Dim lines = File.ReadAllLines(AppPaths.PermissionMatrixCsv, Encoding.UTF8).
                Where(Function(line) Not String.IsNullOrWhiteSpace(line)).
                ToArray()
            If lines.Length < 2 Then Throw New InvalidDataException("Yetki matrisi CSV dosyası boş veya geçersiz.")

            Dim headers = lines(0).Split(";"c).Select(Function(value) value.Trim().TrimStart(ChrW(&HFEFF))).ToArray()
            If headers.Length < 2 OrElse Not String.Equals(headers(0), "Rol", StringComparison.OrdinalIgnoreCase) Then
                Throw New InvalidDataException("Yetki matrisi başlıkları geçersiz.")
            End If

            Dim table As New DataTable()
            For Each header In headers
                table.Columns.Add(header)
            Next

            For lineIndex As Integer = 1 To lines.Length - 1
                Dim values = lines(lineIndex).Split(";"c)
                Dim row = table.NewRow()
                For columnIndex As Integer = 0 To headers.Length - 1
                    row(columnIndex) = If(columnIndex < values.Length, values(columnIndex).Trim(), "")
                Next
                table.Rows.Add(row)
            Next

            sourceTable = ExpandToAllScreens(table)
            PopulateRoleFilter()
            lastSuccessfulRefresh = DateTime.Now
            matrixFileStamp = GetMatrixFileStamp()
            ApplyFilters()
            RestoreGridView(selectedRole, selectedColumn, firstDisplayedRow, firstDisplayedColumn)
        Catch ex As Exception
            If showError Then
                sourceTable = New DataTable()
                grid.DataSource = Nothing
                lblSummary.Text = "Yetki matrisi yüklenemedi"
                MessageBox.Show(ex.Message, "Yetki matrisi açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End If
        Finally
            grid.ResumeLayout()
            isRefreshing = False
        End Try
    End Sub

    Private Shared Function ExpandToAllScreens(baseTable As DataTable) As DataTable
        Dim expanded As New DataTable()
        expanded.Columns.Add("Rol")

        Dim definitions = GetScreenDefinitions()
        For Each definition In definitions
            expanded.Columns.Add(definition.ColumnTitle)
        Next

        For Each baseRow As DataRow In baseTable.Rows
            Dim roleName = Convert.ToString(baseRow("Rol"))
            Dim row = expanded.NewRow()
            row("Rol") = roleName

            For Each definition In definitions
                row(definition.ColumnTitle) = ResolveScreenPermission(baseRow, roleName, definition)
            Next
            expanded.Rows.Add(row)
        Next

        Return expanded
    End Function

    Private Shared Function ResolveScreenPermission(baseRow As DataRow,
                                                    roleName As String,
                                                    definition As ScreenDefinition) As String
        Dim normalizedRole = AppState.NormalizeRole(roleName)
        Dim inheritedPermission = "Erişim yok"
        If definition.BaseColumn <> "" AndAlso baseRow.Table.Columns.Contains(definition.BaseColumn) Then
            inheritedPermission = Convert.ToString(baseRow(definition.BaseColumn))
        End If

        Select Case definition.AccessRule
            Case "ALL_USERS"
                Return "Tam"
            Case "ADMIN_ONLY"
                Return If(String.Equals(normalizedRole, AppState.RoleAdmin, StringComparison.OrdinalIgnoreCase),
                          "Tam",
                          "Erişim yok")
            Case "MANAGER_ADMIN_READ"
                If String.Equals(normalizedRole, AppState.RoleAdmin, StringComparison.OrdinalIgnoreCase) Then
                    Return "Tam"
                End If
                If String.Equals(normalizedRole, AppState.RoleManager, StringComparison.OrdinalIgnoreCase) Then
                    Return "Salt okunur"
                End If
                Return "Erişim yok"
            Case "SCRAP_DASHBOARD"
                If String.Equals(normalizedRole, AppState.RoleAdmin, StringComparison.OrdinalIgnoreCase) Then
                    Return "Tam"
                End If
                If String.Equals(normalizedRole, AppState.RoleManager, StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(normalizedRole, AppState.RoleQualityManager, StringComparison.OrdinalIgnoreCase) Then
                    Return "Salt okunur"
                End If
                Return "Erişim yok"
            Case "REWORK_DASHBOARD"
                If String.Equals(normalizedRole, AppState.RoleAdmin, StringComparison.OrdinalIgnoreCase) Then
                    Return "Tam"
                End If
                If String.Equals(normalizedRole, AppState.RoleManager, StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(normalizedRole, AppState.RoleQualityManager, StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(normalizedRole, AppState.RoleProductionManager, StringComparison.OrdinalIgnoreCase) Then
                    Return "Salt okunur"
                End If
                Return "Erişim yok"
            Case "QUALITY_MANAGER_ADMIN"
                If String.Equals(normalizedRole, AppState.RoleAdmin, StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(normalizedRole, AppState.RoleQualityManager, StringComparison.OrdinalIgnoreCase) Then
                    Return "Tam"
                End If
                Return "Erişim yok"
            Case "PRODUCTION_MANAGER_ADMIN"
                If String.Equals(normalizedRole, AppState.RoleAdmin, StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(normalizedRole, AppState.RoleProductionManager, StringComparison.OrdinalIgnoreCase) Then
                    Return "Tam"
                End If
                Return "Erişim yok"
            Case "NEW_MOLD_COMMISSIONING"
                If String.Equals(normalizedRole, AppState.RoleAdmin, StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(normalizedRole, AppState.RoleProductionManager, StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(normalizedRole, AppState.RoleQualityManager, StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(normalizedRole, AppState.RoleTechnicalDrawing, StringComparison.OrdinalIgnoreCase) Then
                    Return "Tam"
                End If
                If String.Equals(normalizedRole, AppState.RoleManager, StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(normalizedRole, AppState.RolePlanning, StringComparison.OrdinalIgnoreCase) Then
                    Return "Salt okunur"
                End If
                Return "Erişim yok"
            Case "MECHANISM_SHIFT"
                If String.Equals(normalizedRole, AppState.RoleAdmin, StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(normalizedRole, AppState.RoleMechanismManager, StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(normalizedRole, AppState.RoleMechanismQuality, StringComparison.OrdinalIgnoreCase) Then
                    Return "Tam"
                End If
                If String.Equals(normalizedRole, AppState.RoleProductionManager, StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(normalizedRole, AppState.RoleQualityManager, StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(normalizedRole, AppState.RoleManager, StringComparison.OrdinalIgnoreCase) Then
                    Return "Salt okunur"
                End If
                Return "Erişim yok"
            Case "TECHNICAL_ADMIN_MANAGER_READ"
                If String.Equals(normalizedRole, AppState.RoleAdmin, StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(normalizedRole, AppState.RoleTechnicalDrawing, StringComparison.OrdinalIgnoreCase) Then
                    Return "Tam"
                End If
                If String.Equals(normalizedRole, AppState.RoleManager, StringComparison.OrdinalIgnoreCase) Then
                    Return "Salt okunur"
                End If
                Return "Erişim yok"
            Case "MEASUREMENT_ENTRY"
                If String.Equals(normalizedRole, AppState.RoleManager, StringComparison.OrdinalIgnoreCase) Then
                    Return "Erişim yok"
                End If
                Return inheritedPermission
            Case "TEST_ASSIGNMENT"
                If String.Equals(normalizedRole, AppState.RoleAdmin, StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(normalizedRole, AppState.RoleQualityManager, StringComparison.OrdinalIgnoreCase) Then
                    Return "Tam"
                End If
                Return "Erişim yok"
            Case "TEST_EXECUTION"
                If String.Equals(normalizedRole, AppState.RoleAdmin, StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(normalizedRole, AppState.RoleQualityManager, StringComparison.OrdinalIgnoreCase) Then
                    Return "Tam"
                End If
                If String.Equals(normalizedRole, AppState.RoleLaboratory, StringComparison.OrdinalIgnoreCase) Then
                    Return "Sınırlı - atanmış test adımı"
                End If
                Return "Erişim yok"
            Case Else
                Return inheritedPermission
        End Select
    End Function

    Private Shared Function GetScreenDefinitions() As List(Of ScreenDefinition)
        Return New List(Of ScreenDefinition) From {
            New ScreenDefinition("Oturum", "Giriş Ekranı", "", "ALL_USERS"),
            New ScreenDefinition("Oturum", "Ana Ekran", "", "ALL_USERS"),
            New ScreenDefinition("Oturum", "Şifre Değiştir", "", "ALL_USERS"),
            New ScreenDefinition("Oturum", "Kullanıcı Değiştir", "", "ALL_USERS"),
            New ScreenDefinition("Oturum", "Otomatik Kapanma Uyarısı", "", "ALL_USERS"),
            New ScreenDefinition("Oturum", "İlk Admin Kurulumu", "", "ADMIN_ONLY"),
            New ScreenDefinition("Plastikhane Kalite", "Ölçüm Girişi", "Ölçüm Girişi / Geçmişi", "MEASUREMENT_ENTRY"),
            New ScreenDefinition("Plastikhane Kalite", "Görsel Kontrol", "Ölçüm Girişi / Geçmişi", "MEASUREMENT_ENTRY"),
            New ScreenDefinition("Plastikhane Kalite", "Ölçüm Geçmişi", "Ölçüm Girişi / Geçmişi"),
            New ScreenDefinition("Plastikhane Kalite", "Ölçüm Kaydı İnceleme", "Ölçüm Girişi / Geçmişi"),
            New ScreenDefinition("Plastikhane Kalite", "Geçmiş Ölçüm Değeri Düzeltme", "", "ADMIN_ONLY"),
            New ScreenDefinition("Plastikhane Kalite", "Kalite Ticketları", "Kalite Ticketları"),
            New ScreenDefinition("Plastikhane Kalite", "Ticket Ölçümleri", "Kalite Ticketları"),
            New ScreenDefinition("Plastikhane Kalite", "Plastikhane Vardiya Takip Listesi", "Plastikhane Vardiya"),
            New ScreenDefinition("Plastikhane Kalite", "Vardiya Takip Kaydı Detayı", "Plastikhane Vardiya"),
            New ScreenDefinition("Plastikhane Kalite", "Vardiya Takip Mail Alıcıları", "", "ADMIN_ONLY"),
            New ScreenDefinition("Plastikhane Kalite", "Hata Raporları", "Plastikhane Vardiya"),
            New ScreenDefinition("Plastikhane Kalite", "Hata Raporu Detayı", "Plastikhane Vardiya"),
            New ScreenDefinition("Plastikhane Kalite", "Hata Raporu Değerlendirme Atamaları", "", "ADMIN_ONLY"),
            New ScreenDefinition("SPC", "SPC Dashboard", "SPC Dashboard"),
            New ScreenDefinition("SPC", "SPC Analizi", "SPC Dashboard"),
            New ScreenDefinition("SPC", "SPC Ölçüm Detayları", "SPC Dashboard"),
            New ScreenDefinition("SPC", "Kontrol Ölçüsü SPC Geçmişi", "Kontrol Ölçüleri"),
            New ScreenDefinition("Hurda", "Hurda Dashboard", "", "SCRAP_DASHBOARD"),
            New ScreenDefinition("REWORK", "REWORK Dashboard", "", "REWORK_DASHBOARD"),
            New ScreenDefinition("Hurda", "Üretim Öncesi Hurda Pareto", "Kalıp Bağlama"),
            New ScreenDefinition("Mekanizma Kalite", "Mekanizma Kontrol Formu", "Mekanizma Kontrol"),
            New ScreenDefinition("Mekanizma Kalite", "Mekanizma Kontrol Detayı", "Mekanizma Kontrol"),
            New ScreenDefinition("Mekanizma Kalite", "Uygun Değil Mail Alıcıları", "", "ADMIN_ONLY"),
            New ScreenDefinition("Mekanizma Kalite", "İNO-1 / İNO-2 Takip", "İNO-1 / İNO-2"),
            New ScreenDefinition("Mekanizma Kalite", "Mekanizma Vardiya Takip Listesi", "", "MECHANISM_SHIFT"),
            New ScreenDefinition("Mekanizma Kalite", "Mekanizma Vardiya Takip Kaydı Detayı", "", "MECHANISM_SHIFT"),
            New ScreenDefinition("Mekanizma Kalite", "Mekanizma Vardiya Mail Taslağı Hazırla", "", "ADMIN_ONLY"),
            New ScreenDefinition("Üretim ve Kalıp", "Kalıp Bağlama Bildirimi", "Kalıp Bağlama"),
            New ScreenDefinition("Üretim ve Kalıp", "Bağlanacak Kalıp Listesi", "Bağlanacak Kalıp"),
            New ScreenDefinition("Üretim ve Kalıp", "Bağlanacak Kalıp Mail Alıcıları", "", "ADMIN_ONLY"),
            New ScreenDefinition("Üretim ve Kalıp", "Teknik Resim Arama", "Teknik Resim Arama"),
            New ScreenDefinition("Üretim ve Kalıp", "Teknik Resim Görüntüleyici", "Teknik Resim Arama"),
            New ScreenDefinition("Üretim ve Kalıp", "Kalıp Bağlama Dashboardu", "Bağlama Dashboard"),
            New ScreenDefinition("Üretim ve Kalıp", "Üretim Ticketları", "Üretim Ticketları"),
            New ScreenDefinition("Üretim ve Kalıp", "Kalıp Ticketları", "Kalıp Ticketları"),
            New ScreenDefinition("Üretim ve Kalıp", "Kalıp Ticket Detayı", "Kalıp Ticketları"),
            New ScreenDefinition("Üretim ve Kalıp", "Vardiya Kaydından Kalıp Ticketı", "Plastikhane Vardiya"),
            New ScreenDefinition("Kalıphane", "Yeni Kalıp Devreye Alma", "", "NEW_MOLD_COMMISSIONING"),
            New ScreenDefinition("Kalıphane", "Yeni Kalıp Devreye Alma Detayı", "", "NEW_MOLD_COMMISSIONING"),
            New ScreenDefinition("Teknik Resim", "Ürün / Teknik Resim Yönetimi", "Ürün / Teknik Resim"),
            New ScreenDefinition("Teknik Resim", "Bugün Bağlanacak Kalıplar - Resim Kontrolü", "Ürün / Teknik Resim"),
            New ScreenDefinition("Teknik Resim", "Kontrol Ölçüleri", "Kontrol Ölçüleri"),
            New ScreenDefinition("Teknik Resim", "Eksik Ürün Bilgileri", "Kontrol Ölçüleri"),
            New ScreenDefinition("Teknik Resim", "CAD Ölçü Önizleme", "", "TECHNICAL_ADMIN_MANAGER_READ"),
            New ScreenDefinition("MSA", "MSA Dashboard - Ölçüm Cihazları", "MSA Dashboard"),
            New ScreenDefinition("Laboratuvar", "Test / Talep Yönetimi", "Test Talepleri"),
            New ScreenDefinition("Laboratuvar", "Test Talebi / Kontrol Sonucu", "Test Talepleri"),
            New ScreenDefinition("Laboratuvar", "Talep Eden Bölüm Seçimi", "Test Talepleri"),
            New ScreenDefinition("Laboratuvar", "Test Seçimi ve Sıralama", "", "TEST_ASSIGNMENT"),
            New ScreenDefinition("Laboratuvar", "Test Uygulama Adımı", "", "TEST_EXECUTION"),
            New ScreenDefinition("Laboratuvar", "Test Listesi Yönetimi", "", "ADMIN_ONLY"),
            New ScreenDefinition("Laboratuvar", "Test Grubu Yönetimi", "", "ADMIN_ONLY"),
            New ScreenDefinition("Laboratuvar", "Test Talep Mail Alıcıları", "", "ADMIN_ONLY"),
            New ScreenDefinition("Laboratuvar", "Paket Sayaç Kontrolleri", "Paket Sayaç Kontrolleri"),
            New ScreenDefinition("Laboratuvar", "Paket Sayaç Kontrol Detayı", "Paket Sayaç Kontrolleri"),
            New ScreenDefinition("Laboratuvar", "Paket Sayaç Uygun Değil Mail Alıcıları", "", "ADMIN_ONLY"),
            New ScreenDefinition("Yönetim ve Sistem", "Kullanıcı Yönetimi", "Kullanıcı / Log / Güncelleme"),
            New ScreenDefinition("Yönetim ve Sistem", "Açık Oturumlar", "Kullanıcı / Log / Güncelleme"),
            New ScreenDefinition("Yönetim ve Sistem", "Çalışan Programlar", "Kullanıcı / Log / Güncelleme"),
            New ScreenDefinition("Yönetim ve Sistem", "Log Kayıtları", "Kullanıcı / Log / Güncelleme"),
            New ScreenDefinition("Yönetim ve Sistem", "Program Güncelleme Sihirbazı", "Kullanıcı / Log / Güncelleme"),
            New ScreenDefinition("Yönetim ve Sistem", "Veri Sağlığı", "Kullanıcı / Log / Güncelleme"),
            New ScreenDefinition("Yönetim ve Sistem", "Kritik Veri Günlüğü", "Kullanıcı / Log / Güncelleme"),
            New ScreenDefinition("Yönetim ve Sistem", "Yetki Matrisi", "", "MANAGER_ADMIN_READ")
        }
    End Function

    Private Sub OpenExpandedMatrixCsv()
        Try
            If sourceTable Is Nothing OrElse sourceTable.Columns.Count = 0 Then
                Throw New InvalidOperationException("Dışa aktarılacak yetki matrisi bulunamadı.")
            End If

            Dim exportPath = Path.Combine(AppPaths.BaseDir, "Docs", "YETKI_MATRISI_TUM_EKRANLAR.csv")
            Directory.CreateDirectory(Path.GetDirectoryName(exportPath))
            Dim lines As New List(Of String) From {
                String.Join(";", sourceTable.Columns.Cast(Of DataColumn)().
                            Select(Function(column) EscapeCsvValue(column.ColumnName)))
            }
            For Each row As DataRow In sourceTable.Rows
                lines.Add(String.Join(";", sourceTable.Columns.Cast(Of DataColumn)().
                                      Select(Function(column) EscapeCsvValue(Convert.ToString(row(column))))))
            Next
            File.WriteAllLines(exportPath, lines, New UTF8Encoding(True))
            OpenDocument(exportPath)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Tüm ekranlar CSV dosyası açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Shared Function EscapeCsvValue(value As String) As String
        Dim safeValue = If(value, "")
        If safeValue.Contains(";"c) OrElse safeValue.Contains(""""c) OrElse
           safeValue.Contains(ControlChars.Cr) OrElse safeValue.Contains(ControlChars.Lf) Then
            Return """" & safeValue.Replace("""", """""") & """"
        End If
        Return safeValue
    End Function

    Private Sub RestoreGridView(selectedRole As String,
                                selectedColumn As String,
                                firstDisplayedRow As Integer,
                                firstDisplayedColumn As Integer)
        If grid.Rows.Count = 0 OrElse grid.Columns.Count = 0 Then Return

        Dim targetRow = -1
        If selectedRole <> "" Then
            For rowIndex As Integer = 0 To grid.Rows.Count - 1
                If String.Equals(Convert.ToString(grid.Rows(rowIndex).Cells(0).Value),
                                 selectedRole,
                                 StringComparison.OrdinalIgnoreCase) Then
                    targetRow = rowIndex
                    Exit For
                End If
            Next
        End If

        Dim targetColumn = 0
        If selectedColumn <> "" Then
            For columnIndex As Integer = 0 To grid.Columns.Count - 1
                If String.Equals(grid.Columns(columnIndex).HeaderText,
                                 selectedColumn,
                                 StringComparison.OrdinalIgnoreCase) Then
                    targetColumn = columnIndex
                    Exit For
                End If
            Next
        End If

        If targetRow >= 0 Then
            grid.ClearSelection()
            grid.CurrentCell = grid.Rows(targetRow).Cells(targetColumn)
            grid.CurrentCell.Selected = True
        End If

        Try
            If firstDisplayedRow >= 0 AndAlso firstDisplayedRow < grid.Rows.Count Then
                grid.FirstDisplayedScrollingRowIndex = firstDisplayedRow
            End If
            If firstDisplayedColumn > 0 AndAlso firstDisplayedColumn < grid.Columns.Count Then
                grid.FirstDisplayedScrollingColumnIndex = firstDisplayedColumn
            End If
        Catch
            ' Sütunlar değişmişse eski kaydırma konumu uygulanamayabilir.
        End Try
    End Sub

    Private Sub PopulateRoleFilter()
        Dim selectedRole = If(cboRole.SelectedItem Is Nothing, "TÜM ROLLER", cboRole.SelectedItem.ToString())
        cboRole.BeginUpdate()
        Try
            cboRole.Items.Clear()
            cboRole.Items.Add("TÜM ROLLER")
            If sourceTable.Columns.Contains("Rol") Then
                For Each roleName In sourceTable.AsEnumerable().
                    Select(Function(row) Convert.ToString(row("Rol"))).
                    Where(Function(value) value <> "").
                    OrderBy(Function(value) value)
                    cboRole.Items.Add(roleName)
                Next
            End If

            Dim index = cboRole.FindStringExact(selectedRole)
            cboRole.SelectedIndex = If(index >= 0, index, 0)
        Finally
            cboRole.EndUpdate()
        End Try
    End Sub

    Private Sub ApplyFilters()
        If sourceTable Is Nothing OrElse sourceTable.Columns.Count = 0 Then Return

        Dim selectedRole = If(cboRole.SelectedItem Is Nothing, "TÜM ROLLER", cboRole.SelectedItem.ToString())
        Dim permissionFilter = If(cboPermission.SelectedItem Is Nothing, "TÜMÜ", cboPermission.SelectedItem.ToString())
        Dim searchText = txtSearch.Text.Trim()
        Dim filtered = sourceTable.Clone()

        For Each sourceRow As DataRow In sourceTable.Rows
            Dim roleName = Convert.ToString(sourceRow("Rol"))
            If selectedRole <> "TÜM ROLLER" AndAlso Not String.Equals(roleName, selectedRole, StringComparison.OrdinalIgnoreCase) Then Continue For

            If permissionFilter <> "TÜMÜ" AndAlso Not RowContainsPermission(sourceRow, permissionFilter) Then Continue For

            If searchText <> "" Then
                Dim matches = roleName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
                If Not matches Then
                    For columnIndex As Integer = 1 To sourceTable.Columns.Count - 1
                        Dim columnName = sourceTable.Columns(columnIndex).ColumnName
                        Dim value = Convert.ToString(sourceRow(columnIndex))
                        If columnName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                           value.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 Then
                            matches = True
                            Exit For
                        End If
                    Next
                End If
                If Not matches Then Continue For
            End If

            filtered.ImportRow(sourceRow)
        Next

        grid.DataSource = filtered
        ConfigureGeneratedColumns()
        Dim refreshText = If(lastSuccessfulRefresh.HasValue,
                             "   |   Son güncelleme: " & lastSuccessfulRefresh.Value.ToString("HH:mm:ss"),
                             "")
        lblSummary.Text = "Gösterilen rol: " & filtered.Rows.Count.ToString() & " / " & sourceTable.Rows.Count.ToString() &
                          "   |   Ekran / işlev: " & Math.Max(0, sourceTable.Columns.Count - 1).ToString() &
                          refreshText
    End Sub

    Private Shared Function RowContainsPermission(row As DataRow, permissionFilter As String) As Boolean
        For columnIndex As Integer = 1 To row.Table.Columns.Count - 1
            Dim kind = PermissionKind(Convert.ToString(row(columnIndex)))
            If String.Equals(kind, permissionFilter, StringComparison.OrdinalIgnoreCase) Then Return True
        Next
        Return False
    End Function

    Private Sub ConfigureGeneratedColumns()
        If grid.Columns.Count = 0 Then Return

        For columnIndex As Integer = 0 To grid.Columns.Count - 1
            Dim column = grid.Columns(columnIndex)
            column.SortMode = DataGridViewColumnSortMode.Automatic
            column.MinimumWidth = If(columnIndex = 0, 180, 135)
            column.Width = If(columnIndex = 0, 220, 165)
            column.Frozen = columnIndex = 0
            column.DefaultCellStyle.Alignment = If(columnIndex = 0,
                                                    DataGridViewContentAlignment.MiddleLeft,
                                                    DataGridViewContentAlignment.MiddleCenter)
        Next
    End Sub

    Private Sub Grid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse e.ColumnIndex < 0 Then Return

        If e.ColumnIndex = 0 Then
            e.CellStyle.BackColor = Color.FromArgb(43, 78, 119)
            e.CellStyle.ForeColor = Color.White
            e.CellStyle.Font = New Font(grid.Font, FontStyle.Bold)
            e.CellStyle.SelectionBackColor = Color.FromArgb(28, 58, 94)
            e.CellStyle.SelectionForeColor = Color.White
            e.CellStyle.Padding = New Padding(8, 0, 4, 0)
            Return
        End If

        Select Case PermissionKind(Convert.ToString(e.Value))
            Case "TAM"
                e.CellStyle.BackColor = Color.FromArgb(217, 240, 222)
                e.CellStyle.ForeColor = Color.FromArgb(22, 101, 52)
            Case "SINIRLI"
                e.CellStyle.BackColor = Color.FromArgb(255, 235, 196)
                e.CellStyle.ForeColor = Color.FromArgb(137, 81, 0)
            Case "SALT OKUNUR"
                e.CellStyle.BackColor = Color.FromArgb(220, 233, 247)
                e.CellStyle.ForeColor = Color.FromArgb(31, 71, 126)
            Case Else
                e.CellStyle.BackColor = Color.FromArgb(238, 240, 243)
                e.CellStyle.ForeColor = Color.FromArgb(100, 108, 118)
        End Select
    End Sub

    Private Shared Function PermissionKind(value As String) As String
        Dim normalized = If(value, "").Trim().ToUpperInvariant()
        If normalized.StartsWith("TAM") Then Return "TAM"
        If normalized.StartsWith("SINIRLI") Then Return "SINIRLI"
        If normalized.StartsWith("SALT OKUNUR") Then Return "SALT OKUNUR"
        Return "ERİŞİM YOK"
    End Function

    Private Sub Grid_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 OrElse e.ColumnIndex <= 0 Then Return
        Dim roleName = Convert.ToString(grid.Rows(e.RowIndex).Cells(0).Value)
        Dim screenName = grid.Columns(e.ColumnIndex).HeaderText
        Dim permissionText = Convert.ToString(grid.Rows(e.RowIndex).Cells(e.ColumnIndex).Value)
        MessageBox.Show(
            "Rol: " & roleName & Environment.NewLine &
            "Ekran / İşlev: " & screenName & Environment.NewLine &
            "Yetki: " & permissionText,
            "Yetki Detayı",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information)
    End Sub

    Private Sub Grid_CellToolTipTextNeeded(sender As Object, e As DataGridViewCellToolTipTextNeededEventArgs)
        If e.RowIndex < 0 OrElse e.ColumnIndex <= 0 Then Return
        Dim roleName = Convert.ToString(grid.Rows(e.RowIndex).Cells(0).Value)
        Dim permissionText = Convert.ToString(grid.Rows(e.RowIndex).Cells(e.ColumnIndex).Value)
        e.ToolTipText = roleName & " — " & grid.Columns(e.ColumnIndex).HeaderText & ": " & permissionText
    End Sub

    Private Sub OpenDocument(pathValue As String)
        Try
            If Not File.Exists(pathValue) Then Throw New FileNotFoundException("Belge bulunamadı.", pathValue)
            Process.Start(New ProcessStartInfo(pathValue) With {.UseShellExecute = True})
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Belge açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub
End Class
