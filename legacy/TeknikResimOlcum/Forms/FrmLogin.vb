Imports System.Drawing
Imports System.Linq
Imports System.Threading.Tasks
Imports System.Windows.Forms

Public Class FrmLogin
    Inherits Form

    Private NotInheritable Class LoginUserOption
        Public Property Username As String = ""
        Public Property Role As String = ""
        Public Property ShowOnLogin As String = "YES"
        Public Property IsPermissionTestAccount As Boolean = False

        Public ReadOnly Property DisplayText As String
            Get
                Return Username & "    —    " & Role
            End Get
        End Property
    End Class

    Private ReadOnly lstUsers As New ListBox()
    Private ReadOnly txtUserFilter As New TextBox()
    Private ReadOnly txtAdminUser As New TextBox()
    Private ReadOnly txtPass As New TextBox()
    Private ReadOnly btnLogin As New Button()
    Private ReadOnly lblInfo As New Label()
    Private ReadOnly lblSelectedUser As New Label()
    Private ReadOnly lblPasswordHint As New Label()
    Private ReadOnly chkShowPassword As New CheckBox()
    Private ReadOnly pnlPasswordBox As New Panel()
    Private loginOuter As TableLayoutPanel
    Private loginHeader As TableLayoutPanel
    Private loginCard As TableLayoutPanel
    Private loginUserSection As TableLayoutPanel
    Private loginPasswordSection As TableLayoutPanel
    Private allLoginUsers As New List(Of LoginUserOption)()
    Private manualLoginUsers As New List(Of LoginUserOption)()
    Private ReadOnly previousUserName As String
    Private ReadOnly previousRole As String
    Private ReadOnly previousSessionId As String
    Private ReadOnly previousMustChangePassword As Boolean
    Private ReadOnly previousIsPermissionTestAccount As Boolean
    Private startupReady As Boolean = False
    Private startupTaskStarted As Boolean = False
    Private loginControlsEnabled As Boolean = False
    Private isApplyingUserFilter As Boolean = False
    Private isUpdatingIdentitySelection As Boolean = False
    Private isApplyingResponsiveLayout As Boolean = False

    Public Sub New()
        AppIconService.Apply(Me)
        previousUserName = AppState.CurrentUserName
        previousRole = AppState.CurrentRole
        previousSessionId = AppState.CurrentSessionId
        previousMustChangePassword = AppState.CurrentUserMustChangePassword
        previousIsPermissionTestAccount = AppState.CurrentUserIsPermissionTestAccount
        Text = "Teknik Resim Ölçüm Kontrol - Giriş"
        StartPosition = FormStartPosition.CenterScreen
        Size = New Size(920, 720)
        MinimumSize = New Size(860, 660)
        Padding = New Padding(18)
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        AutoScroll = False
        BackColor = Color.FromArgb(238, 243, 249)

        loginOuter = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .Margin = New Padding(0),
            .Padding = New Padding(0),
            .BackColor = BackColor
        }
        loginOuter.RowStyles.Add(New RowStyle(SizeType.Absolute, 116.0F))
        loginOuter.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        loginOuter.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
        Controls.Add(loginOuter)

        loginHeader = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .Margin = New Padding(0),
            .Padding = New Padding(24, 18, 24, 14),
            .BackColor = Color.FromArgb(28, 86, 155)
        }
        loginHeader.RowStyles.Add(New RowStyle(SizeType.Percent, 60.0F))
        loginHeader.RowStyles.Add(New RowStyle(SizeType.Percent, 40.0F))
        loginOuter.Controls.Add(loginHeader, 0, 0)

        loginHeader.Controls.Add(New Label() With {
            .Text = "Teknik Resim Ölçüm Kontrol",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.BottomLeft,
            .Font = New Font("Segoe UI", 23.0F, FontStyle.Bold),
            .ForeColor = Color.White,
            .Margin = New Padding(0)
        }, 0, 0)

        loginHeader.Controls.Add(New Label() With {
            .Text = "Kullanıcınızı seçin, parolanızı girin ve devam edin.",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 11.0F, FontStyle.Regular),
            .ForeColor = Color.FromArgb(218, 232, 248),
            .Margin = New Padding(2, 0, 0, 0)
        }, 0, 1)

        loginCard = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .Margin = New Padding(0, 14, 0, 10),
            .Padding = New Padding(20),
            .BackColor = Color.White
        }
        loginCard.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 56.0F))
        loginCard.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 44.0F))
        loginOuter.Controls.Add(loginCard, 0, 1)

        loginUserSection = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .Margin = New Padding(0, 0, 14, 0),
            .BackColor = Color.White
        }
        loginUserSection.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
        loginUserSection.RowStyles.Add(New RowStyle(SizeType.Absolute, 50.0F))
        loginUserSection.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        loginUserSection.RowStyles.Add(New RowStyle(SizeType.Absolute, 82.0F))
        loginCard.Controls.Add(loginUserSection, 0, 0)

        loginUserSection.Controls.Add(New Label() With {
            .Text = "1   KULLANICINIZI SEÇİN",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 12.0F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(28, 86, 155)
        }, 0, 0)

        txtUserFilter.Dock = DockStyle.Fill
        txtUserFilter.Margin = New Padding(0, 4, 0, 8)
        txtUserFilter.Font = New Font("Segoe UI", 12.0F)
        txtUserFilter.PlaceholderText = "Kullanıcı ara..."
        AddHandler txtUserFilter.TextChanged, Sub() ApplyUserFilter()
        loginUserSection.Controls.Add(txtUserFilter, 0, 1)

        lstUsers.Dock = DockStyle.Fill
        lstUsers.Margin = New Padding(0)
        lstUsers.Font = New Font("Segoe UI", 13.0F, FontStyle.Bold)
        lstUsers.DrawMode = DrawMode.OwnerDrawFixed
        lstUsers.ItemHeight = 68
        lstUsers.IntegralHeight = False
        lstUsers.DisplayMember = "DisplayText"
        lstUsers.SelectionMode = SelectionMode.One
        lstUsers.BackColor = Color.FromArgb(249, 251, 254)
        lstUsers.Cursor = Cursors.Hand
        AddHandler lstUsers.SelectedIndexChanged, AddressOf SelectedUserChanged
        AddHandler lstUsers.DrawItem, AddressOf DrawUserItem
        AddHandler DpiChanged, Sub() UpdateUserListMetrics()
        AddHandler Shown, Sub() UpdateUserListMetrics()
        loginUserSection.Controls.Add(lstUsers, 0, 2)

        Dim adminEntry As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .Margin = New Padding(0, 10, 0, 0),
            .Padding = New Padding(0),
            .BackColor = Color.White
        }
        adminEntry.RowStyles.Add(New RowStyle(SizeType.Absolute, 28.0F))
        adminEntry.RowStyles.Add(New RowStyle(SizeType.Absolute, 44.0F))
        adminEntry.Controls.Add(New Label() With {
            .Text = "YÖNETİCİ / GİZLİ TEST HESABI  •  Listede gösterilmez",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 9.5F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(91, 101, 115)
        }, 0, 0)
        txtAdminUser.Dock = DockStyle.Fill
        txtAdminUser.Margin = New Padding(0, 2, 0, 2)
        txtAdminUser.Font = New Font("Segoe UI", 12.0F)
        txtAdminUser.PlaceholderText = "Gizli kullanıcı adını manuel yazın"
        AddHandler txtAdminUser.TextChanged, AddressOf ManualHiddenUserChanged
        adminEntry.Controls.Add(txtAdminUser, 0, 1)
        loginUserSection.Controls.Add(adminEntry, 0, 3)

        loginPasswordSection = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 7,
            .Margin = New Padding(14, 0, 0, 0),
            .Padding = New Padding(18, 0, 0, 0),
            .BackColor = Color.White
        }
        loginPasswordSection.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
        loginPasswordSection.RowStyles.Add(New RowStyle(SizeType.Absolute, 82.0F))
        loginPasswordSection.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        loginPasswordSection.RowStyles.Add(New RowStyle(SizeType.Absolute, 62.0F))
        loginPasswordSection.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        loginPasswordSection.RowStyles.Add(New RowStyle(SizeType.Absolute, 64.0F))
        loginPasswordSection.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        loginCard.Controls.Add(loginPasswordSection, 1, 0)

        loginPasswordSection.Controls.Add(New Label() With {
            .Text = "2   PAROLANIZI GİRİN",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 12.0F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(28, 86, 155)
        }, 0, 0)

        lblSelectedUser.Text = "Henüz kullanıcı seçilmedi"
        lblSelectedUser.Dock = DockStyle.Fill
        lblSelectedUser.TextAlign = ContentAlignment.MiddleLeft
        lblSelectedUser.Font = New Font("Segoe UI", 12.0F, FontStyle.Bold)
        lblSelectedUser.ForeColor = Color.FromArgb(30, 68, 112)
        lblSelectedUser.BackColor = Color.FromArgb(235, 244, 255)
        lblSelectedUser.Padding = New Padding(14, 0, 10, 0)
        lblSelectedUser.BorderStyle = BorderStyle.FixedSingle
        lblSelectedUser.Margin = New Padding(0, 2, 0, 12)
        loginPasswordSection.Controls.Add(lblSelectedUser, 0, 1)

        lblPasswordHint.Text = "Önce kullanıcı seçin."
        lblPasswordHint.Dock = DockStyle.Fill
        lblPasswordHint.TextAlign = ContentAlignment.MiddleLeft
        lblPasswordHint.Font = New Font("Segoe UI", 11.0F, FontStyle.Bold)
        lblPasswordHint.ForeColor = Color.FromArgb(92, 102, 116)
        loginPasswordSection.Controls.Add(lblPasswordHint, 0, 2)

        pnlPasswordBox.Dock = DockStyle.Fill
        pnlPasswordBox.Margin = New Padding(0, 2, 0, 8)
        pnlPasswordBox.Padding = New Padding(12, 6, 12, 6)
        pnlPasswordBox.BorderStyle = BorderStyle.FixedSingle
        pnlPasswordBox.BackColor = Color.FromArgb(246, 248, 251)
        AddHandler pnlPasswordBox.Resize, Sub() LayoutPasswordTextBox()
        loginPasswordSection.Controls.Add(pnlPasswordBox, 0, 3)

        txtPass.Dock = DockStyle.None
        txtPass.Anchor = AnchorStyles.Left Or AnchorStyles.Right
        txtPass.Margin = New Padding(0)
        txtPass.BorderStyle = BorderStyle.None
        txtPass.AutoSize = False
        txtPass.Font = New Font("Segoe UI", 13.5F)
        txtPass.PlaceholderText = "Parolanızı buraya girin"
        txtPass.UseSystemPasswordChar = False
        txtPass.BackColor = pnlPasswordBox.BackColor
        AddHandler txtPass.TextChanged,
            Sub()
                ApplyPasswordMaskState()
                UpdateLoginActionState()
            End Sub
        pnlPasswordBox.Controls.Add(txtPass)
        LayoutPasswordTextBox()

        chkShowPassword.Text = "Parolayı göster"
        chkShowPassword.Dock = DockStyle.Fill
        chkShowPassword.AutoSize = True
        chkShowPassword.Font = New Font("Segoe UI", 10.0F)
        chkShowPassword.ForeColor = Color.FromArgb(65, 75, 88)
        chkShowPassword.Margin = New Padding(0, 4, 0, 4)
        AddHandler chkShowPassword.CheckedChanged,
            Sub()
                ApplyPasswordMaskState()
                txtPass.Focus()
            End Sub
        loginPasswordSection.Controls.Add(chkShowPassword, 0, 4)

        btnLogin.Text = "ÖNCE KULLANICI SEÇİN"
        btnLogin.Dock = DockStyle.Fill
        btnLogin.Margin = New Padding(0, 4, 0, 4)
        btnLogin.Font = New Font("Segoe UI", 13.0F, FontStyle.Bold)
        btnLogin.BackColor = Color.FromArgb(206, 214, 224)
        btnLogin.ForeColor = Color.FromArgb(105, 115, 128)
        btnLogin.FlatStyle = FlatStyle.Flat
        btnLogin.FlatAppearance.BorderSize = 0
        btnLogin.Cursor = Cursors.Default
        AddHandler btnLogin.Click, AddressOf Login_Click
        AddHandler Shown, AddressOf FrmLogin_Shown
        loginPasswordSection.Controls.Add(btnLogin, 0, 5)

        loginPasswordSection.Controls.Add(New Label() With {
            .Text = "Parola yazılmadan giriş butonu etkinleşmez.",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.TopLeft,
            .Font = New Font("Segoe UI", 9.5F),
            .ForeColor = Color.FromArgb(112, 122, 135),
            .Padding = New Padding(0, 12, 0, 0)
        }, 0, 6)

        lblInfo.Text = "Program hazırlanıyor..."
        lblInfo.Dock = DockStyle.Fill
        lblInfo.ForeColor = Color.FromArgb(76, 88, 104)
        lblInfo.TextAlign = ContentAlignment.MiddleCenter
        lblInfo.Font = New Font("Segoe UI", 10.0F)
        lblInfo.AutoEllipsis = True
        lblInfo.Margin = New Padding(0)
        loginOuter.Controls.Add(lblInfo, 0, 2)

        AcceptButton = btnLogin
        SetLoginEnabled(False)
        AddHandler Load, Sub() ApplyResponsiveLoginLayout(True)
        AddHandler Resize, Sub() ApplyResponsiveLoginLayout(False)
        AddHandler DpiChanged,
            Sub()
                If IsHandleCreated AndAlso Not IsDisposed Then
                    BeginInvoke(New MethodInvoker(Sub() ApplyResponsiveLoginLayout(True)))
                End If
            End Sub
        AddHandler Shown,
            Sub()
                If IsHandleCreated AndAlso Not IsDisposed Then
                    BeginInvoke(New MethodInvoker(Sub() ApplyResponsiveLoginLayout(True)))
                End If
            End Sub
    End Sub

    Private Sub ApplyResponsiveLoginLayout(recenterWindow As Boolean)
        If isApplyingResponsiveLayout OrElse loginOuter Is Nothing OrElse loginCard Is Nothing OrElse
           loginUserSection Is Nothing OrElse loginPasswordSection Is Nothing Then Return

        isApplyingResponsiveLayout = True
        Try
            Dim workingArea = Screen.FromControl(Me).WorkingArea
            If workingArea.Width <= 0 OrElse workingArea.Height <= 0 Then Return

            Dim dpiScale = Math.Max(1.0R, DeviceDpi / 96.0R)
            Dim edge = Math.Max(8, CInt(Math.Round(12 * dpiScale)))
            Dim maxWidth = Math.Max(320, workingArea.Width - (edge * 2))
            Dim maxHeight = Math.Max(360, workingArea.Height - (edge * 2))
            Dim targetWidth = Math.Min(CInt(Math.Round(920 * dpiScale)), maxWidth)
            Dim targetHeight = Math.Min(CInt(Math.Round(720 * dpiScale)), maxHeight)

            MinimumSize = New Size(
                Math.Min(CInt(Math.Round(760 * dpiScale)), maxWidth),
                Math.Min(CInt(Math.Round(600 * dpiScale)), maxHeight))

            Dim targetSize = New Size(targetWidth, targetHeight)
            If WindowState = FormWindowState.Normal AndAlso Size <> targetSize Then Size = targetSize

            If WindowState = FormWindowState.Normal Then
                Dim targetX As Integer
                Dim targetY As Integer

                If recenterWindow Then
                    targetX = workingArea.Left + Math.Max(0, (workingArea.Width - Width) \ 2)
                    targetY = workingArea.Top + Math.Max(0, (workingArea.Height - Height) \ 2)
                Else
                    targetX = Math.Max(workingArea.Left, Math.Min(Left, workingArea.Right - Width))
                    targetY = Math.Max(workingArea.Top, Math.Min(Top, workingArea.Bottom - Height))
                End If

                If Left <> targetX OrElse Top <> targetY Then Location = New Point(targetX, targetY)
            End If

            Dim logicalWidth = ClientSize.Width / dpiScale
            Dim logicalHeight = ClientSize.Height / dpiScale
            Dim compact = logicalWidth < 900 OrElse logicalHeight < 690
            Dim shortLayout = logicalHeight < 560

            Padding = ScaleLoginPadding(
                If(shortLayout, New Padding(6), If(compact, New Padding(10), New Padding(18))),
                dpiScale)
            loginOuter.RowStyles(0).Height = CSng(Math.Round(If(shortLayout, 78, If(compact, 96, 116)) * dpiScale))
            loginOuter.RowStyles(2).Height = CSng(Math.Round(If(shortLayout, 32, If(compact, 40, 48)) * dpiScale))

            loginHeader.Padding = ScaleLoginPadding(
                If(shortLayout,
                   New Padding(14, 8, 14, 7),
                   If(compact, New Padding(18, 12, 18, 10), New Padding(24, 18, 24, 14))),
                dpiScale)

            loginCard.Margin = ScaleLoginPadding(
                If(shortLayout,
                   New Padding(0, 4, 0, 4),
                   If(compact, New Padding(0, 8, 0, 6), New Padding(0, 14, 0, 10))),
                dpiScale)
            loginCard.Padding = ScaleLoginPadding(
                If(shortLayout, New Padding(8), If(compact, New Padding(12), New Padding(20))),
                dpiScale)
            loginCard.ColumnStyles(0).Width = If(compact, 54.0F, 56.0F)
            loginCard.ColumnStyles(1).Width = If(compact, 46.0F, 44.0F)

            loginUserSection.Margin = ScaleLoginPadding(
                If(shortLayout,
                   New Padding(0, 0, 4, 0),
                   If(compact, New Padding(0, 0, 6, 0), New Padding(0, 0, 14, 0))),
                dpiScale)
            loginPasswordSection.Margin = ScaleLoginPadding(
                If(shortLayout,
                   New Padding(4, 0, 0, 0),
                   If(compact, New Padding(6, 0, 0, 0), New Padding(14, 0, 0, 0))),
                dpiScale)
            loginPasswordSection.Padding = ScaleLoginPadding(
                If(shortLayout,
                   New Padding(5, 0, 0, 0),
                   If(compact, New Padding(8, 0, 0, 0), New Padding(18, 0, 0, 0))),
                dpiScale)

            loginUserSection.RowStyles(0).Height = CSng(Math.Round(If(shortLayout, 34, If(compact, 40, 48)) * dpiScale))
            loginUserSection.RowStyles(1).Height = CSng(Math.Round(If(shortLayout, 38, If(compact, 44, 50)) * dpiScale))
            loginUserSection.RowStyles(3).Height = CSng(Math.Round(If(shortLayout, 60, If(compact, 72, 82)) * dpiScale))

            Dim shortPasswordRows = New Integer() {34, 58, 38, 44, 32, 46}
            Dim compactPasswordRows = New Integer() {40, 72, 48, 54, 38, 56}
            Dim standardPasswordRows = New Integer() {48, 82, 58, 60, 42, 64}
            Dim passwordRows = If(shortLayout, shortPasswordRows, If(compact, compactPasswordRows, standardPasswordRows))
            For rowIndex As Integer = 0 To passwordRows.Length - 1
                loginPasswordSection.RowStyles(rowIndex).Height = CSng(Math.Round(passwordRows(rowIndex) * dpiScale))
            Next

            loginOuter.PerformLayout()
            UpdateUserListMetrics()
        Catch ex As Exception
            ErrorLogService.Log("FrmLogin.ApplyResponsiveLoginLayout", ex)
        Finally
            isApplyingResponsiveLayout = False
        End Try
    End Sub

    Private Shared Function ScaleLoginPadding(value As Padding, scale As Double) As Padding
        Return New Padding(
            Math.Max(0, CInt(Math.Round(value.Left * scale))),
            Math.Max(0, CInt(Math.Round(value.Top * scale))),
            Math.Max(0, CInt(Math.Round(value.Right * scale))),
            Math.Max(0, CInt(Math.Round(value.Bottom * scale))))
    End Function

    Private Async Sub FrmLogin_Shown(sender As Object, e As EventArgs)
        If startupTaskStarted Then Return
        startupTaskStarted = True
        lblInfo.Text = "Program hazırlanıyor..."
        Dim initialAdminSetupRequired As Boolean = False

        Try
                           Await Task.Run(Sub()
                               AppPaths.EnsureFolders()
                               UserStoreRecoveryService.PrepareBeforeDataInitialization()
                               CryptoService.EnsureKeyStore()
                               DataService.EnsureLoginFiles()
                               UserService.EnsureDefaultAdmin()
                               initialAdminSetupRequired = UserService.NeedsInitialAdminPasswordSetup()
                           End Sub)

            If IsDisposed OrElse Not IsHandleCreated Then Return

            If initialAdminSetupRequired Then
                Using setupForm As New FrmInitialAdminSetup()
                    If setupForm.ShowDialog(Me) <> DialogResult.OK Then
                        MessageBox.Show(
                            "Admin parolası belirlenmeden program kullanılamaz.",
                            "Kurulum tamamlanmadı",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information)
                        Close()
                        Return
                    End If
                End Using
            End If

            startupReady = True
            LoadSelectableUsers()
            SetLoginEnabled(True)
            lblInfo.Text = "Önce kullanıcınızı seçin."
            lstUsers.Focus()
        Catch ex As Exception
            If IsDisposed OrElse Not IsHandleCreated Then Return

            lblInfo.Text = "Program başlatılamadı."
            MessageBox.Show(ex.Message, "Başlatma hatası", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Async Sub Login_Click(sender As Object, e As EventArgs)
        If Not startupReady Then
            lblInfo.Text = "Program hazırlanıyor, lütfen bekleyin..."
            Return
        End If

        Dim userName = SelectedUserName()
        If userName = "" Then
            Dim message =
                If(txtAdminUser.Text.Trim() <> "",
                   "Gizli kullanıcı adını doğru ve tam olarak yazın.",
                   "Önce listeden kullanıcı seçin veya gizli kullanıcı adını manuel yazın.")
            MessageBox.Show(
                message,
                "Kullanıcı seçilmedi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning)
            If txtAdminUser.Text.Trim() <> "" Then txtAdminUser.Focus() Else lstUsers.Focus()
            Return
        End If

        Dim password = txtPass.Text
        If password = "" Then
            MessageBox.Show(
                "Devam etmek için parolanızı girin.",
                "Parola girilmedi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning)
            txtPass.Focus()
            Return
        End If

        SetLoginEnabled(False)
        lblInfo.Text = "Giriş yapılıyor..."

        Try
            Dim authenticated = Await Task.Run(Function()
                                                   Dim ok = UserService.AuthenticateFast(userName, password)
                                                   If Not ok Then
                                                       AuditService.Log("LOGIN_FAIL", "", "", "Hatalı giriş denemesi: " & userName)
                                                   End If
                                                   Return ok
                                               End Function)

            If authenticated Then
                If AppState.CurrentUserMustChangePassword OrElse UserService.MustCurrentUserChangePassword() Then
                    Using changePasswordForm As New FrmChangePassword(True)
                        If changePasswordForm.ShowDialog(Me) <> DialogResult.OK Then
                            Await CancelAuthenticatedSessionAsync()
                            MessageBox.Show(
                                "Zorunlu parola değişikliği tamamlanmadığı için giriş iptal edildi.",
                                "Giriş iptal edildi",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information)
                            txtPass.Clear()
                            Return
                        End If
                    End Using
                End If

                DialogResult = DialogResult.OK
                Close()
            Else
                MessageBox.Show("Kullanıcı adı veya şifre hatalı.", "Giriş başarısız", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                txtPass.SelectAll()
            End If
        Catch ex As InvalidOperationException
            LogLoginBlockedInBackground(userName, ex.Message)
            MessageBox.Show(ex.Message, "Giriş engellendi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Giriş hatası", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            If Not IsDisposed AndAlso DialogResult <> DialogResult.OK Then
                SetLoginEnabled(startupReady)
                If startupReady Then lblInfo.Text = "Başka bir kullanıcı seçebilir veya parolanızı yeniden girebilirsiniz."
            End If
        End Try
    End Sub

    Private Sub SetLoginEnabled(enabled As Boolean)
        loginControlsEnabled = enabled
        lstUsers.Enabled = enabled
        txtUserFilter.Enabled = enabled
        txtAdminUser.Enabled = enabled
        chkShowPassword.Enabled = enabled
        Cursor = If(enabled, Cursors.Default, Cursors.WaitCursor)
        UpdateLoginActionState()
    End Sub

    Private Sub LayoutPasswordTextBox()
        If pnlPasswordBox Is Nothing OrElse txtPass Is Nothing Then Return

        Dim area = pnlPasswordBox.DisplayRectangle
        Dim measuredHeight = TextRenderer.MeasureText("Ay", txtPass.Font, New Size(400, 100), TextFormatFlags.NoPadding).Height
        Dim targetHeight = Math.Max(30, measuredHeight + 8)
        Dim top = area.Top + Math.Max(0, (area.Height - targetHeight) \ 2)

        txtPass.SetBounds(area.Left, top, Math.Max(20, area.Width), targetHeight)
    End Sub

    Private Sub ApplyPasswordMaskState()
        ' Windows TextBox, UseSystemPasswordChar=True iken bazı DPI/ölçeklerde placeholder metnini kırpabiliyor.
        ' Alan boşken maskeyi kapatıp placeholder'ı normal metin gibi çizdiriyoruz; kullanıcı yazınca tekrar maske uygulanıyor.
        txtPass.UseSystemPasswordChar = txtPass.Text.Length > 0 AndAlso Not chkShowPassword.Checked
    End Sub

    Private Sub UpdateLoginActionState()
        Dim hasUser = SelectedUserName() <> ""
        Dim hasPassword = txtPass.Text.Length > 0
        Dim canInteract = loginControlsEnabled AndAlso startupReady

        txtPass.Enabled = canInteract AndAlso hasUser
        chkShowPassword.Enabled = canInteract AndAlso hasUser
        btnLogin.Enabled = canInteract AndAlso hasUser AndAlso hasPassword

        If Not canInteract Then
            btnLogin.Text = If(startupReady, "GİRİŞ YAPILIYOR...", "PROGRAM HAZIRLANIYOR...")
            btnLogin.BackColor = Color.FromArgb(206, 214, 224)
            btnLogin.ForeColor = Color.FromArgb(105, 115, 128)
            btnLogin.Cursor = Cursors.Default
            pnlPasswordBox.BackColor = Color.FromArgb(246, 248, 251)
            txtPass.BackColor = pnlPasswordBox.BackColor
            Return
        End If

        If Not hasUser Then
            btnLogin.Text = "ÖNCE KULLANICI SEÇİN"
            btnLogin.BackColor = Color.FromArgb(206, 214, 224)
            btnLogin.ForeColor = Color.FromArgb(105, 115, 128)
            btnLogin.Cursor = Cursors.Default
            lblPasswordHint.Text =
                If(txtAdminUser.Text.Trim() <> "",
                   "Gizli kullanıcı adını doğru ve tam yazın.",
                   "Listeden kullanıcı seçin veya gizli kullanıcı adını manuel yazın.")
            lblPasswordHint.ForeColor = Color.FromArgb(92, 102, 116)
            pnlPasswordBox.BackColor = Color.FromArgb(246, 248, 251)
        ElseIf Not hasPassword Then
            btnLogin.Text = "PAROLANIZI GİRİN"
            btnLogin.BackColor = Color.FromArgb(220, 226, 234)
            btnLogin.ForeColor = Color.FromArgb(92, 102, 116)
            btnLogin.Cursor = Cursors.Default
            lblPasswordHint.Text = "Parolanızı girmeyi unutmayın."
            lblPasswordHint.ForeColor = Color.FromArgb(181, 103, 0)
            pnlPasswordBox.BackColor = Color.FromArgb(255, 248, 225)
        Else
            btnLogin.Text = "GİRİŞ YAP  →"
            btnLogin.BackColor = Color.FromArgb(28, 104, 190)
            btnLogin.ForeColor = Color.White
            btnLogin.Cursor = Cursors.Hand
            lblPasswordHint.Text = "Parola hazır. Giriş yapabilirsiniz."
            lblPasswordHint.ForeColor = Color.FromArgb(35, 125, 74)
            pnlPasswordBox.BackColor = Color.FromArgb(235, 249, 241)
        End If

        txtPass.BackColor = pnlPasswordBox.BackColor
    End Sub

    Private Sub LoadSelectableUsers()
        Dim activeUsers = UserService.GetUsers().
            Where(Function(r)
                      Return String.Equals(DataService.GetValue(r, "IsActive"), "YES", StringComparison.OrdinalIgnoreCase)
                   End Function).
            Select(Function(r) New LoginUserOption With {
                .Username = DataService.GetValue(r, "Username").Trim(),
                .Role = AppState.NormalizeRole(DataService.GetValue(r, "Role")),
                .ShowOnLogin = If(String.Equals(DataService.GetValue(r, "ShowOnLogin"), "NO", StringComparison.OrdinalIgnoreCase), "NO", "YES"),
                .IsPermissionTestAccount = String.Equals(DataService.GetValue(r, "IsPermissionTestAccount"), "YES", StringComparison.OrdinalIgnoreCase)
            }).
            Where(Function(item) item.Username <> "").
            GroupBy(Function(item) item.Username, StringComparer.OrdinalIgnoreCase).
            Select(Function(group) group.First()).
            OrderBy(Function(item) item.Username, StringComparer.CurrentCultureIgnoreCase).
            ToList()

        manualLoginUsers = activeUsers.
            Where(
                Function(item) item.ShowOnLogin = "NO" OrElse
                               String.Equals(
                                   AppState.NormalizeRole(item.Role),
                                   AppState.RoleAdmin,
                                   StringComparison.OrdinalIgnoreCase)).
            ToList()
        allLoginUsers = activeUsers.
            Where(
                Function(item) item.ShowOnLogin <> "NO" AndAlso
                               Not String.Equals(
                                   AppState.NormalizeRole(item.Role),
                                   AppState.RoleAdmin,
                                   StringComparison.OrdinalIgnoreCase)).
            ToList()

        ApplyUserFilter()
    End Sub

    Private Sub ApplyUserFilter()
        Dim selectedName = SelectedListUserName()
        Dim filterText = txtUserFilter.Text.Trim()
        Dim visibleUsers = allLoginUsers.AsEnumerable()

        If filterText <> "" Then
            visibleUsers = visibleUsers.Where(
                Function(item)
                    Return (item.Username & " " & item.Role).
                        IndexOf(filterText, StringComparison.CurrentCultureIgnoreCase) >= 0
                End Function)
        End If

        Dim list = visibleUsers.ToList()
        isApplyingUserFilter = True
        lstUsers.BeginUpdate()
        Try
            lstUsers.DataSource = Nothing
            lstUsers.DisplayMember = "DisplayText"
            lstUsers.DataSource = list
            lstUsers.SelectedIndex = -1

            If selectedName <> "" Then
                For i As Integer = 0 To list.Count - 1
                    If String.Equals(list(i).Username, selectedName, StringComparison.OrdinalIgnoreCase) Then
                        lstUsers.SelectedIndex = i
                        Exit For
                    End If
                Next
            End If
        Finally
            lstUsers.EndUpdate()
            isApplyingUserFilter = False
        End Try

        If list.Count = 0 Then
            lblSelectedUser.Text = "Filtreye uygun kullanıcı bulunamadı."
        ElseIf lstUsers.SelectedIndex < 0 Then
            lblSelectedUser.Text = "Henüz kullanıcı seçilmedi"
        End If
        RefreshSelectedIdentity(False, False)
    End Sub

    Private Sub SelectedUserChanged(sender As Object, e As EventArgs)
        If isApplyingUserFilter OrElse isUpdatingIdentitySelection Then Return

        If lstUsers.SelectedItem IsNot Nothing AndAlso txtAdminUser.Text <> "" Then
            isUpdatingIdentitySelection = True
            Try
                txtAdminUser.Clear()
            Finally
                isUpdatingIdentitySelection = False
            End Try
        End If

        RefreshSelectedIdentity(True, True)
    End Sub

    Private Sub ManualHiddenUserChanged(sender As Object, e As EventArgs)
        If isUpdatingIdentitySelection Then Return

        isUpdatingIdentitySelection = True
        Try
            If lstUsers.SelectedIndex >= 0 Then lstUsers.SelectedIndex = -1
        Finally
            isUpdatingIdentitySelection = False
        End Try

        RefreshSelectedIdentity(True, ManualHiddenOption() IsNot Nothing)
    End Sub

    Private Sub RefreshSelectedIdentity(clearPassword As Boolean, focusPassword As Boolean)
        Dim optionValue = ManualHiddenOption()
        If optionValue Is Nothing Then optionValue = TryCast(lstUsers.SelectedItem, LoginUserOption)

        If optionValue Is Nothing Then
            If txtAdminUser.Text.Trim() <> "" Then
                lblSelectedUser.Text = "Gizli kullanıcı adını tam olarak yazın"
            ElseIf lstUsers.Items.Count > 0 Then
                lblSelectedUser.Text = "Henüz kullanıcı seçilmedi"
            End If
            If clearPassword Then txtPass.Clear()
            UpdateLoginActionState()
            Return
        End If

        lblSelectedUser.Text = optionValue.Username & Environment.NewLine & optionValue.Role &
                               If(optionValue.IsPermissionTestAccount, "  •  YETKİ TEST HESABI", "")
        If clearPassword Then txtPass.Clear()
        UpdateLoginActionState()

        If focusPassword AndAlso startupReady AndAlso IsHandleCreated Then
            BeginInvoke(
                CType(
                    Sub()
                        If Not IsDisposed Then
                            txtPass.Focus()
                            txtPass.SelectAll()
                        End If
                    End Sub,
                    MethodInvoker))
        End If
    End Sub

    Private Function SelectedUserName() As String
        Dim manualOption = ManualHiddenOption()
        If manualOption IsNot Nothing Then Return manualOption.Username

        Dim optionValue = TryCast(lstUsers.SelectedItem, LoginUserOption)
        Return If(optionValue Is Nothing, "", optionValue.Username)
    End Function

    Private Function SelectedListUserName() As String
        Dim optionValue = TryCast(lstUsers.SelectedItem, LoginUserOption)
        Return If(optionValue Is Nothing, "", optionValue.Username)
    End Function

    Private Function ManualHiddenOption() As LoginUserOption
        Dim typedUserName = txtAdminUser.Text.Trim()
        If typedUserName = "" Then Return Nothing

        Return manualLoginUsers.FirstOrDefault(
            Function(item) String.Equals(
                item.Username,
                typedUserName,
                StringComparison.OrdinalIgnoreCase))
    End Function

    Private Sub DrawUserItem(sender As Object, e As DrawItemEventArgs)
        If e.Index < 0 OrElse e.Index >= lstUsers.Items.Count Then Return

        Dim optionValue = TryCast(lstUsers.Items(e.Index), LoginUserOption)
        If optionValue Is Nothing Then Return

        Dim selected = (e.State And DrawItemState.Selected) = DrawItemState.Selected
        Dim backgroundColor =
            If(selected,
               Color.FromArgb(28, 104, 190),
               If(e.Index Mod 2 = 0, Color.White, Color.FromArgb(246, 249, 252)))
        Dim foreColor = If(selected, Color.White, Color.FromArgb(31, 42, 55))
        Dim roleColor = If(selected, Color.FromArgb(225, 237, 250), Color.FromArgb(101, 112, 126))
        Dim dpiScale = Math.Max(1.0F, e.Graphics.DpiY / 96.0F)
        Dim textLeft = e.Bounds.Left + CInt(Math.Round(14 * dpiScale))
        Dim textRightPadding = CInt(Math.Round(12 * dpiScale))

        Using backgroundBrush As New SolidBrush(backgroundColor)
            e.Graphics.FillRectangle(backgroundBrush, e.Bounds)
        End Using

        If selected Then
            Using accentBrush As New SolidBrush(Color.FromArgb(255, 190, 58))
                e.Graphics.FillRectangle(accentBrush, e.Bounds.Left, e.Bounds.Top, 5, e.Bounds.Height)
            End Using
        End If

        Using userFont As New Font("Segoe UI", 13.0F, FontStyle.Bold),
              roleFont As New Font("Segoe UI", 9.5F, FontStyle.Regular)
            Dim userHeight = TextRenderer.MeasureText(e.Graphics, "ÇğıÖŞÜyjp", userFont, New Size(2000, 200), TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine).Height + 2
            Dim roleHeight = TextRenderer.MeasureText(e.Graphics, "ÇğıÖŞÜyjp", roleFont, New Size(2000, 200), TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine).Height + 2
            Dim lineGap = Math.Max(1, CInt(Math.Round(1 * dpiScale)))
            Dim contentHeight = userHeight + lineGap + roleHeight
            Dim contentTop = e.Bounds.Top + Math.Max(3, (e.Bounds.Height - contentHeight) \ 2)
            Dim textWidth = Math.Max(20, e.Bounds.Right - textLeft - textRightPadding)

            TextRenderer.DrawText(
                e.Graphics,
                optionValue.Username,
                userFont,
                New Rectangle(textLeft, contentTop, textWidth, userHeight),
                foreColor,
                TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix)
            TextRenderer.DrawText(
                e.Graphics,
                optionValue.Role,
                roleFont,
                New Rectangle(textLeft, contentTop + userHeight + lineGap, textWidth, roleHeight),
                roleColor,
                TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix)
        End Using

        Using separatorPen As New Pen(Color.FromArgb(226, 232, 240))
            e.Graphics.DrawLine(
                separatorPen,
                e.Bounds.Left + 8,
                e.Bounds.Bottom - 1,
                e.Bounds.Right - 8,
                e.Bounds.Bottom - 1)
        End Using

        e.DrawFocusRectangle()
    End Sub

    Private Sub UpdateUserListMetrics()
        If lstUsers Is Nothing OrElse lstUsers.IsDisposed Then Return

        Try
            Using userFont As New Font("Segoe UI", 13.0F, FontStyle.Bold),
                  roleFont As New Font("Segoe UI", 9.5F, FontStyle.Regular),
                  graphics = lstUsers.CreateGraphics()
                Dim userHeight = TextRenderer.MeasureText(graphics, "ÇğıÖŞÜyjp", userFont, New Size(2000, 200), TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine).Height + 2
                Dim roleHeight = TextRenderer.MeasureText(graphics, "ÇğıÖŞÜyjp", roleFont, New Size(2000, 200), TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine).Height + 2
                Dim dpiScale = Math.Max(1.0R, graphics.DpiY / 96.0R)
                Dim verticalPadding = Math.Max(10, CInt(Math.Round(12 * dpiScale)))
                lstUsers.ItemHeight = Math.Max(CInt(Math.Round(64 * dpiScale)), userHeight + roleHeight + verticalPadding)
            End Using

            lstUsers.Invalidate()
        Catch ex As Exception
            ErrorLogService.Log("FrmLogin.UpdateUserListMetrics", ex)
        End Try
    End Sub

    Private Sub LogLoginBlockedInBackground(userName As String, message As String)
        Dim ignoredTask As Task = Task.Run(Sub()
                                               AuditService.Log("LOGIN_BLOCKED_ACTIVE_SESSION", "", "", "Kullanıcı: " & userName & "; " & message)
                                           End Sub)
    End Sub

    Private Async Function CancelAuthenticatedSessionAsync() As Task
        Dim sessionId = AppState.CurrentSessionId
        If sessionId <> "" Then
            Try
                Await Task.Run(Sub() DataService.EndUserSession(sessionId))
            Catch cleanupEx As Exception
                ErrorLogService.Log("FrmLogin.LoginCleanup", cleanupEx, "SessionId=" & sessionId)
            End Try
        End If

        AppState.CurrentUserName = previousUserName
        AppState.CurrentRole = previousRole
        AppState.CurrentSessionId = previousSessionId
        AppState.CurrentUserMustChangePassword = previousMustChangePassword
        AppState.CurrentUserIsPermissionTestAccount = previousIsPermissionTestAccount
    End Function

End Class
