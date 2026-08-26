Imports System
Imports System.Drawing
Imports System.Windows.Forms

Public Class UserManagementForm
    Inherits Form

    Private ReadOnly store As UserStore
    Private ReadOnly activeUser As String

    Private lstUsers As ListBox
    Private txtUserName As TextBox
    Private cmbRole As ComboBox
    Private txtCurrentPassword As TextBox
    Private txtNewPassword As TextBox
    Private txtRepeatPassword As TextBox

    Public Sub New(userStore As UserStore, currentUser As String)
        store = userStore
        activeUser = currentUser

        Me.Text = "Kullanıcı Yönetimi"
        Me.StartPosition = FormStartPosition.CenterParent
        Me.Size = New Size(840, 560)
        Me.MinimumSize = New Size(800, 540)
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Font = New Font("Segoe UI", 9.0F)
        Me.BackColor = Color.FromArgb(243, 246, 250)

        AppIconHelper.ApplyIcon(Me)
        BuildUi()
        LoadUsers()
    End Sub

    Private Sub BuildUi()
        Dim root As New TableLayoutPanel()
        root.Dock = DockStyle.Fill
        root.ColumnCount = 2
        root.RowCount = 1
        root.Padding = New Padding(14)
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 260))
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        Me.Controls.Add(root)

        Dim leftLayout As New TableLayoutPanel()
        leftLayout.Dock = DockStyle.Fill
        leftLayout.ColumnCount = 1
        leftLayout.RowCount = 2
        leftLayout.Margin = New Padding(0, 0, 12, 0)
        leftLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34))
        leftLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        root.Controls.Add(leftLayout, 0, 0)

        Dim lblUsers As New Label()
        lblUsers.Text = "Kullanıcılar"
        lblUsers.Dock = DockStyle.Fill
        lblUsers.TextAlign = ContentAlignment.MiddleLeft
        lblUsers.Font = New Font("Segoe UI", 10.0F, FontStyle.Bold)
        lblUsers.ForeColor = Color.FromArgb(31, 78, 121)
        leftLayout.Controls.Add(lblUsers, 0, 0)

        lstUsers = New ListBox()
        lstUsers.Dock = DockStyle.Fill
        lstUsers.Font = New Font("Segoe UI", 9.0F)
        lstUsers.IntegralHeight = False
        AddHandler lstUsers.SelectedIndexChanged, AddressOf LstUsers_SelectedIndexChanged
        leftLayout.Controls.Add(lstUsers, 0, 1)

        Dim right As New Panel()
        right.Dock = DockStyle.Fill
        right.BackColor = Color.White
        right.Padding = New Padding(18)
        root.Controls.Add(right, 1, 0)

        Dim formLayout As New TableLayoutPanel()
        formLayout.Dock = DockStyle.Fill
        formLayout.ColumnCount = 1
        formLayout.RowCount = 7
        formLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 62))
        formLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 62))
        formLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 62))
        formLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 62))
        formLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 62))
        formLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        formLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 46))
        right.Controls.Add(formLayout)

        txtUserName = AddLabeledTextBox(formLayout, 0, "Kullanıcı Adı", False)
        cmbRole = AddLabeledComboBox(formLayout, 1, "Rol")
        txtCurrentPassword = AddLabeledTextBox(formLayout, 2, "Mevcut Şifre", True)
        txtNewPassword = AddLabeledTextBox(formLayout, 3, "Yeni Şifre", False)
        txtNewPassword.UseSystemPasswordChar = True
        txtRepeatPassword = AddLabeledTextBox(formLayout, 4, "Yeni Şifre Tekrar", False)
        txtRepeatPassword.UseSystemPasswordChar = True

        Dim infoLabel As New Label()
        infoLabel.Text = "Rol seçenekleri: ADMİN, MEKANİZMA, ONAY" & Environment.NewLine &
                         "Yeni kullanıcı için şifre zorunludur. Mevcut kullanıcıda şifre boş bırakılırsa sadece rol güncellenir."
        infoLabel.Dock = DockStyle.Fill
        infoLabel.ForeColor = Color.FromArgb(102, 112, 133)
        infoLabel.TextAlign = ContentAlignment.TopLeft
        formLayout.Controls.Add(infoLabel, 0, 5)

        Dim buttons As New FlowLayoutPanel()
        buttons.Dock = DockStyle.Fill
        buttons.FlowDirection = FlowDirection.RightToLeft
        buttons.WrapContents = False
        formLayout.Controls.Add(buttons, 0, 6)

        AddActionButton(buttons, "Kapat", Color.FromArgb(71, 84, 103), AddressOf BtnClose_Click)
        AddActionButton(buttons, "Sil", Color.FromArgb(180, 35, 24), AddressOf BtnDelete_Click)
        AddActionButton(buttons, "Kaydet / Güncelle", Color.FromArgb(15, 123, 63), AddressOf BtnSave_Click, 142)
        AddActionButton(buttons, "Yeni Temizle", Color.FromArgb(238, 242, 247), AddressOf BtnNew_Click, 110, True)
    End Sub

    Private Function AddLabeledTextBox(parent As TableLayoutPanel, rowIndex As Integer, labelText As String, readOnlyText As Boolean) As TextBox
        Dim panel As New TableLayoutPanel()
        panel.Dock = DockStyle.Fill
        panel.ColumnCount = 1
        panel.RowCount = 2
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 22))
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 30))
        parent.Controls.Add(panel, 0, rowIndex)

        Dim lbl As New Label()
        lbl.Text = labelText
        lbl.Dock = DockStyle.Fill
        lbl.Font = New Font("Segoe UI", 8.8F, FontStyle.Bold)
        lbl.ForeColor = Color.FromArgb(52, 64, 84)
        lbl.TextAlign = ContentAlignment.MiddleLeft
        panel.Controls.Add(lbl, 0, 0)

        Dim txt As New TextBox()
        txt.Dock = DockStyle.Fill
        txt.Font = New Font("Segoe UI", 9.0F)
        txt.ReadOnly = readOnlyText
        If readOnlyText Then txt.BackColor = Color.FromArgb(245, 247, 250)
        panel.Controls.Add(txt, 0, 1)

        Return txt
    End Function

    Private Function AddLabeledComboBox(parent As TableLayoutPanel, rowIndex As Integer, labelText As String) As ComboBox
        Dim panel As New TableLayoutPanel()
        panel.Dock = DockStyle.Fill
        panel.ColumnCount = 1
        panel.RowCount = 2
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 22))
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 30))
        parent.Controls.Add(panel, 0, rowIndex)

        Dim lbl As New Label()
        lbl.Text = labelText
        lbl.Dock = DockStyle.Fill
        lbl.Font = New Font("Segoe UI", 8.8F, FontStyle.Bold)
        lbl.ForeColor = Color.FromArgb(52, 64, 84)
        lbl.TextAlign = ContentAlignment.MiddleLeft
        panel.Controls.Add(lbl, 0, 0)

        Dim cmb As New ComboBox()
        cmb.Dock = DockStyle.Fill
        cmb.DropDownStyle = ComboBoxStyle.DropDownList
        cmb.Items.AddRange(store.GetRoleOptions())
        panel.Controls.Add(cmb, 0, 1)

        Return cmb
    End Function

    Private Sub AddActionButton(parent As FlowLayoutPanel, text As String, backColor As Color, clickHandler As EventHandler, Optional width As Integer = 100, Optional darkText As Boolean = False)
        Dim btn As New Button()
        btn.Text = text
        btn.Size = New Size(width, 34)
        btn.Margin = New Padding(6, 6, 0, 0)
        btn.BackColor = backColor
        btn.ForeColor = If(darkText, Color.FromArgb(52, 64, 84), Color.White)
        btn.FlatStyle = FlatStyle.Flat
        btn.FlatAppearance.BorderSize = 0
        AddHandler btn.Click, clickHandler
        parent.Controls.Add(btn)
    End Sub

    Private Sub LoadUsers()
        Dim selected = If(lstUsers IsNot Nothing AndAlso lstUsers.SelectedItem IsNot Nothing, Convert.ToString(lstUsers.SelectedItem), "")

        lstUsers.Items.Clear()

        For Each u In store.GetUsers()
            lstUsers.Items.Add(u)
        Next

        If selected.Length > 0 AndAlso lstUsers.Items.Contains(selected) Then
            lstUsers.SelectedItem = selected
        ElseIf lstUsers.Items.Count > 0 Then
            lstUsers.SelectedIndex = 0
        End If
    End Sub

    Private Sub LstUsers_SelectedIndexChanged(sender As Object, e As EventArgs)
        If lstUsers.SelectedItem Is Nothing Then Return

        Dim userName = Convert.ToString(lstUsers.SelectedItem)

        txtUserName.Text = userName
        cmbRole.SelectedItem = store.GetRole(userName)
        txtCurrentPassword.Text = store.GetVisiblePassword(userName)
        txtNewPassword.Text = ""
        txtRepeatPassword.Text = ""
    End Sub

    Private Sub BtnNew_Click(sender As Object, e As EventArgs)
        lstUsers.ClearSelected()
        txtUserName.Text = ""
        If cmbRole.Items.Count > 0 Then cmbRole.SelectedItem = UserStore.RoleApproval
        txtCurrentPassword.Text = ""
        txtNewPassword.Text = ""
        txtRepeatPassword.Text = ""
        txtUserName.Focus()
    End Sub

    Private Sub BtnSave_Click(sender As Object, e As EventArgs)
        Try
            Dim userName = txtUserName.Text.Trim()
            Dim roleName = Convert.ToString(cmbRole.SelectedItem)
            Dim password = txtNewPassword.Text

            If String.IsNullOrWhiteSpace(userName) Then
                MessageBox.Show("Kullanıcı adı boş olamaz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            If String.IsNullOrWhiteSpace(roleName) Then
                MessageBox.Show("Rol seçmelisiniz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            If password.Length > 0 OrElse Not store.HasUser(userName) Then
                If password.Length = 0 Then
                    MessageBox.Show("Yeni kullanıcı için şifre zorunludur.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return
                End If

                If password <> txtRepeatPassword.Text Then
                    MessageBox.Show("Yeni şifreler aynı değil.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return
                End If
            End If

            store.CreateOrUpdateUser(userName, password, roleName)
            LoadUsers()
            lstUsers.SelectedItem = userName

            MessageBox.Show("Kullanıcı kaydedildi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub BtnDelete_Click(sender As Object, e As EventArgs)
        If lstUsers.SelectedItem Is Nothing Then Return

        Dim userName = Convert.ToString(lstUsers.SelectedItem)

        If String.Equals(userName, activeUser, StringComparison.OrdinalIgnoreCase) Then
            MessageBox.Show("Aktif oturumdaki kullanıcı silinemez.", "Silme Engellendi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If String.Equals(store.GetRole(userName), UserStore.RoleAdmin, StringComparison.OrdinalIgnoreCase) AndAlso store.CountUsersWithRole(UserStore.RoleAdmin) <= 1 Then
            MessageBox.Show("Sistemde en az bir ADMİN kullanıcısı kalmalıdır.", "Silme Engellendi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If MessageBox.Show(userName & " kullanıcısı silinsin mi?", "Kullanıcı Sil", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) <> DialogResult.Yes Then
            Return
        End If

        store.DeleteUser(userName)
        LoadUsers()
        BtnNew_Click(Nothing, EventArgs.Empty)
    End Sub

    Private Sub BtnClose_Click(sender As Object, e As EventArgs)
        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub
End Class
