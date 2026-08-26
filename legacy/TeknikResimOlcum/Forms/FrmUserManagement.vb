Imports System.Data
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmUserManagement
    Inherits Form

    Private ReadOnly grid As New DataGridView()
    Private ReadOnly txtFilter As New TextBox()
    Private ReadOnly txtUser As New TextBox()
    Private ReadOnly txtPass As New TextBox()
    Private ReadOnly cboRole As New ComboBox()
    Private ReadOnly cboStatus As New ComboBox()
    Private ReadOnly chkActive As New CheckBox()
    Private ReadOnly chkShowOnLogin As New CheckBox()
    Private ReadOnly chkPermissionTestAccount As New CheckBox()
    Private ReadOnly btnResetPassword As New Button()
    Private ReadOnly lblCount As New Label()

    Public Sub New()
        AuthorizationService.Require(AppState.CanOpenUserAdmin, "Kullanici Yonetimi")
        AppIconService.Apply(Me)
        Text = "Kullanıcı Yönetimi"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1150, 680)
        MinimumSize = New Size(700, 480)
        BackColor = Color.White

        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .BackColor = Color.White
        }
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 150.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(layout)

        Dim top As New Panel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(12),
            .BackColor = SystemColors.Control,
            .AutoScroll = True,
            .AutoScrollMinSize = New Size(1140, 140)
        }
        layout.Controls.Add(top, 0, 0)

        AddLabel(top, "Kullanıcı", 15, 18)
        txtUser.SetBounds(110, 15, 180, 25)

        AddLabel(top, "Yeni Parola", 320, 18)
        txtPass.SetBounds(420, 15, 180, 25)
        txtPass.UseSystemPasswordChar = True

        AddLabel(top, "Rol", 15, 58)
        cboRole.SetBounds(110, 55, 230, 25)
        cboRole.DropDownStyle = ComboBoxStyle.DropDownList
        cboRole.Items.AddRange({AppState.RoleProduction,
                                AppState.RoleProductionLabel,
                                AppState.RoleProductionManager,
                                AppState.RoleQualityManager,
                                AppState.RoleIncomingQuality,
                                AppState.RoleMechanismQuality,
                                AppState.RoleMechanismManager,
                                AppState.RolePlasticQuality,
                                AppState.RoleLaboratory,
                                AppState.RoleTechnicalDrawing,
                                AppState.RolePlanning,
                                AppState.RoleManager,
                                AppState.RoleAdmin})
        cboRole.SelectedIndex = 0
        AddHandler cboRole.SelectedIndexChanged, AddressOf AccountOptionChanged

        chkActive.Text = "Aktif"
        chkActive.SetBounds(370, 55, 75, 25)
        chkActive.Checked = True

        chkShowOnLogin.Text = "Giriş ekranında göster"
        chkShowOnLogin.SetBounds(455, 55, 175, 25)
        chkShowOnLogin.Checked = True

        chkPermissionTestAccount.Text = "Yetki test hesabı"
        chkPermissionTestAccount.SetBounds(645, 55, 165, 25)
        AddHandler chkPermissionTestAccount.CheckedChanged, AddressOf AccountOptionChanged

        Dim btnSave As New Button() With {.Text = "Kaydet", .Left = 110, .Top = 95, .Width = 120, .Height = 32}
        AddHandler btnSave.Click, AddressOf Save_Click

        Dim btnClear As New Button() With {.Text = "Yeni", .Left = 250, .Top = 95, .Width = 100, .Height = 32}
        AddHandler btnClear.Click, Sub() ClearInputs()

        btnResetPassword.Text = "Parolayı Belirle"
        btnResetPassword.SetBounds(370, 95, 130, 32)
        AddHandler btnResetPassword.Click, AddressOf ResetPassword_Click

        Dim btnDelete As New Button() With {
            .Text = "Kullanıcıyı Sil",
            .Left = 520,
            .Top = 95,
            .Width = 135,
            .Height = 32,
            .ForeColor = Color.DarkRed
        }
        AddHandler btnDelete.Click, AddressOf DeleteUser_Click

        Dim btnToggleActive As New Button() With {
            .Text = "Aktif / Pasif Yap",
            .Left = 675,
            .Top = 95,
            .Width = 140,
            .Height = 32
        }
        AddHandler btnToggleActive.Click, AddressOf ToggleUserActive_Click

        Dim info As New Label() With {
            .Text = "Gizli ve test hesapları girişte listelenmez; kullanıcı adı manuel yazılır.",
            .Left = 835,
            .Top = 100,
            .Width = 295,
            .Height = 36,
            .ForeColor = Color.DimGray,
            .BackColor = Color.Transparent
        }

        top.Controls.AddRange({txtUser, txtPass, cboRole, chkActive, chkShowOnLogin, chkPermissionTestAccount, btnSave, btnClear, btnResetPassword, btnDelete, btnToggleActive, info})

        Dim filterPanel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 8,
            .RowCount = 1,
            .Padding = New Padding(12, 5, 12, 5),
            .BackColor = Color.WhiteSmoke,
            .Margin = New Padding(0)
        }
        filterPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 245.0F))
        filterPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 84.0F))
        filterPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        filterPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 54.0F))
        filterPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 108.0F))
        filterPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 14.0F))
        filterPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 136.0F))
        filterPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 182.0F))
        filterPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.Controls.Add(filterPanel, 0, 1)

        lblCount.Text = "Kullanıcılar"
        lblCount.Dock = DockStyle.Fill
        lblCount.Margin = New Padding(2, 0, 10, 0)
        lblCount.TextAlign = ContentAlignment.MiddleLeft
        lblCount.Font = New Font(Font.FontFamily, 9.0F, FontStyle.Bold)

        Dim lblFilter As New Label() With {
            .Text = "Liste Filtresi",
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0),
            .BackColor = Color.Transparent,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        txtFilter.Dock = DockStyle.Fill
        txtFilter.Margin = New Padding(0, 2, 10, 2)
        txtFilter.PlaceholderText = "kullanıcı / rol"
        AddHandler txtFilter.TextChanged, Sub() LoadGrid()

        Dim lblStatus As New Label() With {
            .Text = "Durum",
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0),
            .BackColor = Color.Transparent,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        cboStatus.Dock = DockStyle.Fill
        cboStatus.Margin = New Padding(0, 2, 10, 2)
        cboStatus.DropDownStyle = ComboBoxStyle.DropDownList
        cboStatus.Items.AddRange({"TÜMÜ", "AKTİF", "PASİF"})
        cboStatus.SelectedIndex = 0
        AddHandler cboStatus.SelectedIndexChanged, Sub() LoadGrid()

        Dim spacer As New Panel() With {.Dock = DockStyle.Fill, .BackColor = Color.Transparent, .Margin = New Padding(0)}

        Dim btnClearFilter As New Button() With {
            .Text = "Filtreyi Temizle",
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0, 1, 8, 1),
            .MinimumSize = New Size(128, 29),
            .AutoEllipsis = False
        }
        AddHandler btnClearFilter.Click, Sub()
                                             txtFilter.Clear()
                                             cboStatus.SelectedIndex = 0
                                             LoadGrid()
                                         End Sub

        Dim btnEndAllSessions As New Button() With {
            .Text = "Açık Oturumlar",
            .Dock = DockStyle.Fill,
            .Margin = New Padding(8, 1, 0, 1),
            .MinimumSize = New Size(168, 29),
            .AutoEllipsis = False,
            .ForeColor = Color.FromArgb(25, 58, 100)
        }
        AddHandler btnEndAllSessions.Click, AddressOf OpenActiveSessions_Click

        filterPanel.Controls.Add(lblCount, 0, 0)
        filterPanel.Controls.Add(lblFilter, 1, 0)
        filterPanel.Controls.Add(txtFilter, 2, 0)
        filterPanel.Controls.Add(lblStatus, 3, 0)
        filterPanel.Controls.Add(cboStatus, 4, 0)
        filterPanel.Controls.Add(spacer, 5, 0)
        filterPanel.Controls.Add(btnClearFilter, 6, 0)
        filterPanel.Controls.Add(btnEndAllSessions, 7, 0)

        ConfigureGrid()
        layout.Controls.Add(grid, 0, 2)

        LoadGrid()
    End Sub

    Private Sub AddLabel(parent As Control, text As String, x As Integer, y As Integer)
        parent.Controls.Add(New Label() With {.Text = text, .Left = x, .Top = y + 3, .Width = 90, .BackColor = Color.Transparent})
    End Sub

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.MultiSelect = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.AutoGenerateColumns = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        grid.RowHeadersVisible = False
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.GridColor = Color.Gainsboro
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 235, 235)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.DefaultCellStyle.BackColor = Color.White
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248)
        grid.RowTemplate.Height = 26

        grid.Columns.Clear()
        grid.Columns.Add(MakeTextColumn("Username", "Kullanıcı", 180, 18))
        grid.Columns.Add(MakeTextColumn("Role", "Rol", 190, 12))
        grid.Columns.Add(MakeTextColumn("IsActive", "Aktif", 80, 8))
        grid.Columns.Add(MakeTextColumn("ShowOnLogin", "Girişte Göster", 105, 9))
        grid.Columns.Add(MakeTextColumn("IsPermissionTestAccount", "Test Hesabı", 95, 9))
        grid.Columns.Add(MakeTextColumn("LastLoginAt", "Son Giriş Tarihi / Saati", 170, 16))
        grid.Columns.Add(MakeTextColumn("PasswordStatus", "Parola Durumu", 130, 13))
        grid.Columns.Add(MakeTextColumn("PasswordChangedAt", "Parola Değişikliği", 160, 16))
        grid.Columns.Add(MakeTextColumn("CreatedAt", "Kayıt Tarihi", 160, 16))

        AddHandler grid.CellDoubleClick, AddressOf Grid_DoubleClick
    End Sub

    Private Function MakeTextColumn(name As String, header As String, width As Integer, fillWeight As Single) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .DataPropertyName = name,
            .HeaderText = header,
            .Width = width,
            .MinimumWidth = 70,
            .FillWeight = fillWeight,
            .SortMode = DataGridViewColumnSortMode.Automatic
        }
    End Function

    Private Sub LoadGrid()
        Dim allRows = UserService.GetUsersWithLastLogin()
        Dim rows = allRows.AsEnumerable()

        Dim filterText = txtFilter.Text.Trim()
        If filterText <> "" Then
            Dim tokens = filterText.Split(New Char() {" "c, ";"c, ","c, "|"c}, StringSplitOptions.RemoveEmptyEntries)
            rows = rows.Where(Function(r)
                                  Dim haystack = (DataService.GetValue(r, "Username") & " " &
                                                  DataService.GetValue(r, "Role") & " " &
                                                  DataService.GetValue(r, "IsActive") & " " &
                                                  DataService.GetValue(r, "ShowOnLogin") & " " &
                                                  DataService.GetValue(r, "IsPermissionTestAccount") & " " &
                                                  DataService.GetValue(r, "LastLoginAt")).ToUpperInvariant()
                                  For Each token In tokens
                                      If Not haystack.Contains(token.ToUpperInvariant()) Then Return False
                                  Next
                                  Return True
                              End Function)
        End If

        Dim statusText = If(cboStatus.SelectedItem Is Nothing, "TÜMÜ", cboStatus.SelectedItem.ToString())
        If statusText = "AKTİF" Then
            rows = rows.Where(Function(r) String.Equals(DataService.GetValue(r, "IsActive"), "YES", StringComparison.OrdinalIgnoreCase))
        ElseIf statusText = "PASİF" Then
            rows = rows.Where(Function(r) Not String.Equals(DataService.GetValue(r, "IsActive"), "YES", StringComparison.OrdinalIgnoreCase))
        End If

        Dim list = rows.OrderBy(Function(x) DataService.GetValue(x, "Username")).ToList()

        Dim dt As New DataTable()
        dt.Columns.Add("Username")
        dt.Columns.Add("Role")
        dt.Columns.Add("IsActive")
        dt.Columns.Add("ShowOnLogin")
        dt.Columns.Add("IsPermissionTestAccount")
        dt.Columns.Add("LastLoginAt")
        dt.Columns.Add("PasswordStatus")
        dt.Columns.Add("PasswordChangedAt")
        dt.Columns.Add("CreatedAt")

        For Each r In list
            Dim dr = dt.NewRow()
            dr("Username") = DataService.GetValue(r, "Username")
            dr("Role") = AppState.NormalizeRole(DataService.GetValue(r, "Role"))
            dr("IsActive") = DataService.GetValue(r, "IsActive")
            dr("ShowOnLogin") = If(String.Equals(DataService.GetValue(r, "ShowOnLogin"), "NO", StringComparison.OrdinalIgnoreCase), "HAYIR", "EVET")
            dr("IsPermissionTestAccount") = If(String.Equals(DataService.GetValue(r, "IsPermissionTestAccount"), "YES", StringComparison.OrdinalIgnoreCase), "EVET", "HAYIR")
            dr("LastLoginAt") = FormatLastLogin(DataService.GetValue(r, "LastLoginAt"))
            dr("PasswordStatus") = If(
                String.Equals(DataService.GetValue(r, "MustChangePassword"), "YES", StringComparison.OrdinalIgnoreCase),
                "DEĞİŞİKLİK ZORUNLU",
                "NORMAL")
            dr("PasswordChangedAt") = DataService.GetValue(r, "PasswordChangedAt")
            dr("CreatedAt") = DataService.GetValue(r, "CreatedAt")
            dt.Rows.Add(dr)
        Next

        grid.DataSource = dt
        lblCount.Text = $"Kullanıcılar: {dt.Rows.Count} / {allRows.Count} adet"
    End Sub

    Private Shared Function FormatLastLogin(value As String) As String
        value = If(value, "").Trim()
        If value = "" OrElse String.Equals(value, "NEVER", StringComparison.OrdinalIgnoreCase) Then Return "—"
        Dim parsed As DateTime
        If DateTime.TryParse(value, parsed) Then Return parsed.ToString("dd.MM.yyyy HH:mm:ss")
        Return value
    End Function

    Private Sub Save_Click(sender As Object, e As EventArgs)
        Try
            UserService.SaveUser(
                txtUser.Text,
                txtPass.Text,
                cboRole.Text,
                If(chkActive.Checked, "YES", "NO"),
                If(chkShowOnLogin.Checked, "YES", "NO"),
                If(chkPermissionTestAccount.Checked, "YES", "NO"))
            LoadGrid()
            MessageBox.Show(
                "Kullanıcı kaydedildi." & Environment.NewLine &
                "Şifre alanı boşsa mevcut parola korunur." & Environment.NewLine &
                "Mevcut kullanıcıya yeni parola yazıldıysa doğrudan geçerli olur." & Environment.NewLine &
                "Rolü değiştirilen veya pasif yapılan kullanıcının açık oturumu otomatik kapatılır.",
                "Bilgi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information)
            ClearInputs()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ResetPassword_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.IsAdmin, "Kullanici Parolasi Belirleme")

            Dim username = SelectedOrTypedUsername()
            If username = "" Then
                MessageBox.Show("Önce listeden bir kullanıcı seçin veya kullanıcı adını yazın.", "Kullanıcı seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim newPassword = txtPass.Text
            If newPassword = "" Then
                MessageBox.Show(
                    "Yeni Parola alanına atanacak parolayı yazın.",
                    "Parola girilmedi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning)
                txtPass.Focus()
                Return
            End If

            If MessageBox.Show(username & " kullanıcısının parolası Yeni Parola alanındaki değerle değiştirilecek." & Environment.NewLine &
                               "Devam edilsin mi?",
                               "Parolayı Belirle", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then
                Return
            End If

            UserService.SetPasswordByAdmin(username, newPassword)
            txtPass.Clear()
            LoadGrid()

            MessageBox.Show("Parola Admin tarafından belirlendi." & Environment.NewLine &
                            "Kullanıcı: " & username & Environment.NewLine &
                            "Yeni parola doğrudan kullanılabilir.",
                            "Parola Belirlendi", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub DeleteUser_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.IsAdmin, "Kullanıcı Silme")

            Dim username = SelectedUsernameForDelete()
            If username = "" Then
                MessageBox.Show("Önce listeden silinecek kullanıcıyı seçiniz.", "Kullanıcı seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            If String.Equals(username, AppState.CurrentUserName, StringComparison.OrdinalIgnoreCase) Then
                MessageBox.Show("Açık olan ADMIN hesabı kendi kendisini silemez.", "Kullanıcı silinemez", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim confirmation = Microsoft.VisualBasic.Interaction.InputBox(
                "Bu işlem geri alınamaz." & Environment.NewLine &
                "Silinecek kullanıcı: " & username & Environment.NewLine & Environment.NewLine &
                "Kullanıcıyı silmek için ONAY yazınız.",
                "ADMIN kullanıcı silme doğrulaması",
                "")

            If Not String.Equals(confirmation.Trim(), "ONAY", StringComparison.Ordinal) Then
                MessageBox.Show("ONAY yazılmadığı için silme işlemi iptal edildi.", "İşlem iptal edildi", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            UserService.DeleteUser(username)
            ClearInputs()
            LoadGrid()
            MessageBox.Show(username & " kullanıcısı silindi.", "Kullanıcı silindi", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Kullanıcı silinemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ToggleUserActive_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.IsAdmin, "Kullanıcı Aktif/Pasif İşlemi")

            Dim username = SelectedUsernameForDelete()
            If username = "" Then
                MessageBox.Show("Önce listeden durumu değiştirilecek kullanıcıyı seçiniz.", "Kullanıcı seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim newIsActive = UserService.ToggleUserActive(username)
            chkActive.Checked = newIsActive
            LoadGrid()

            MessageBox.Show(
                username & " kullanıcısı " & If(newIsActive, "aktif", "pasif") & " yapıldı.",
                "Kullanıcı durumu değiştirildi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information)
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Kullanıcı durumu değiştirilemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub OpenActiveSessions_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.IsAdmin, "Açık Oturumlar")
            Using form As New FrmActiveSessions()
                form.ShowDialog(Me)
            End Using
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Açık oturumlar görüntülenemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Grid_DoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return
        txtUser.Text = Convert.ToString(grid.Rows(e.RowIndex).Cells("Username").Value)
        cboRole.Text = AppState.NormalizeRole(Convert.ToString(grid.Rows(e.RowIndex).Cells("Role").Value))
        chkActive.Checked = String.Equals(Convert.ToString(grid.Rows(e.RowIndex).Cells("IsActive").Value), "YES", StringComparison.OrdinalIgnoreCase)
        chkPermissionTestAccount.Checked = String.Equals(Convert.ToString(grid.Rows(e.RowIndex).Cells("IsPermissionTestAccount").Value), "EVET", StringComparison.OrdinalIgnoreCase)
        chkShowOnLogin.Checked = String.Equals(Convert.ToString(grid.Rows(e.RowIndex).Cells("ShowOnLogin").Value), "EVET", StringComparison.OrdinalIgnoreCase)
        AccountOptionChanged(Nothing, EventArgs.Empty)
        txtPass.Clear()
    End Sub

    Private Sub ClearInputs()
        txtUser.Clear()
        txtPass.Clear()
        cboRole.SelectedIndex = 0
        chkActive.Checked = True
        chkPermissionTestAccount.Checked = False
        chkShowOnLogin.Checked = True
        AccountOptionChanged(Nothing, EventArgs.Empty)
    End Sub

    Private Sub AccountOptionChanged(sender As Object, e As EventArgs)
        Dim mustBeHidden = chkPermissionTestAccount.Checked OrElse
                           String.Equals(cboRole.Text, AppState.RoleAdmin, StringComparison.OrdinalIgnoreCase)
        If mustBeHidden Then chkShowOnLogin.Checked = False
        chkShowOnLogin.Enabled = Not mustBeHidden
    End Sub

    Private Function SelectedOrTypedUsername() As String
        Dim username = txtUser.Text.Trim()
        If username <> "" Then Return username

        If grid.CurrentRow IsNot Nothing Then
            Return Convert.ToString(grid.CurrentRow.Cells("Username").Value).Trim()
        End If

        Return ""
    End Function

    Private Function SelectedUsernameForDelete() As String
        If grid.CurrentRow IsNot Nothing AndAlso grid.Columns.Contains("Username") Then
            Dim selected = Convert.ToString(grid.CurrentRow.Cells("Username").Value).Trim()
            If selected <> "" Then Return selected
        End If

        Return txtUser.Text.Trim()
    End Function
End Class
