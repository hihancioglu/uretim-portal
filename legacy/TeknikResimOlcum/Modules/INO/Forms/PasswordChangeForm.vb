Imports System
Imports System.Drawing
Imports System.Windows.Forms

Public Class PasswordChangeForm
    Inherits Form

    Private ReadOnly store As UserStore
    Private ReadOnly activeUser As String
    Private ReadOnly adminMode As Boolean

    Private cmbTargetUser As ComboBox
    Private txtCurrentPassword As TextBox
    Private txtNewPassword As TextBox
    Private txtRepeatPassword As TextBox

    Public Sub New(userStore As UserStore, currentUser As String, isAdmin As Boolean)
        store = userStore
        activeUser = currentUser
        adminMode = isAdmin

        Me.Text = If(adminMode, "Şifre Yönetimi", "Şifre Değiştir")
        Me.StartPosition = FormStartPosition.CenterParent
        Me.ClientSize = New Size(500, 380)
        Me.MinimumSize = New Size(516, 419)
        Me.MaximumSize = New Size(516, 419)
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.AutoScaleMode = AutoScaleMode.Dpi
        Me.Font = New Font("Segoe UI", 9.0F)
        Me.BackColor = Color.FromArgb(243, 246, 250)

        AppIconHelper.ApplyIcon(Me)
        BuildUi()
    End Sub

    Private Sub BuildUi()
        Dim lblHeader As New Label()
        lblHeader.Text = If(adminMode, "Kullanıcı şifrelerini yönetin", "Şifrenizi değiştirin")
        lblHeader.SetBounds(22, 18, 450, 24)
        lblHeader.Font = New Font("Segoe UI", 10.0F, FontStyle.Bold)
        lblHeader.ForeColor = Color.FromArgb(31, 78, 121)
        Me.Controls.Add(lblHeader)

        Dim lblUser As New Label()
        lblUser.Text = "Şifresi Değiştirilecek Kullanıcı"
        lblUser.SetBounds(22, 56, 450, 20)
        lblUser.Font = New Font("Segoe UI", 8.8F, FontStyle.Bold)
        lblUser.ForeColor = Color.FromArgb(52, 64, 84)
        Me.Controls.Add(lblUser)

        cmbTargetUser = New ComboBox()
        cmbTargetUser.SetBounds(22, 78, 456, 28)
        cmbTargetUser.Font = New Font("Segoe UI", 9.0F)
        cmbTargetUser.DropDownStyle = ComboBoxStyle.DropDownList

        If adminMode Then
            cmbTargetUser.Items.AddRange(store.GetUsers().ToArray())
        Else
            cmbTargetUser.Items.Add(activeUser)
            cmbTargetUser.Enabled = False
        End If

        If cmbTargetUser.Items.Count > 0 Then cmbTargetUser.SelectedIndex = 0
        AddHandler cmbTargetUser.SelectedIndexChanged, AddressOf CmbTargetUser_SelectedIndexChanged
        Me.Controls.Add(cmbTargetUser)

        Dim lblCurrent As New Label()
        lblCurrent.Text = If(adminMode, "Mevcut Şifre", "Mevcut Şifre")
        lblCurrent.SetBounds(22, 118, 450, 20)
        lblCurrent.Font = New Font("Segoe UI", 8.8F, FontStyle.Bold)
        lblCurrent.ForeColor = Color.FromArgb(52, 64, 84)
        Me.Controls.Add(lblCurrent)

        txtCurrentPassword = New TextBox()
        txtCurrentPassword.SetBounds(22, 140, 456, 28)
        txtCurrentPassword.Font = New Font("Segoe UI", 9.0F)
        txtCurrentPassword.ReadOnly = True
        txtCurrentPassword.BackColor = Color.FromArgb(245, 247, 250)
        txtCurrentPassword.ForeColor = Color.FromArgb(52, 64, 84)
        Me.Controls.Add(txtCurrentPassword)

        Dim lblNew As New Label()
        lblNew.Text = "Yeni Şifre"
        lblNew.SetBounds(22, 180, 450, 20)
        lblNew.Font = New Font("Segoe UI", 8.8F, FontStyle.Bold)
        lblNew.ForeColor = Color.FromArgb(52, 64, 84)
        Me.Controls.Add(lblNew)

        txtNewPassword = New TextBox()
        txtNewPassword.SetBounds(22, 202, 456, 28)
        txtNewPassword.Font = New Font("Segoe UI", 9.0F)
        txtNewPassword.UseSystemPasswordChar = True
        Me.Controls.Add(txtNewPassword)

        Dim lblRepeat As New Label()
        lblRepeat.Text = "Yeni Şifre Tekrar"
        lblRepeat.SetBounds(22, 242, 450, 20)
        lblRepeat.Font = New Font("Segoe UI", 8.8F, FontStyle.Bold)
        lblRepeat.ForeColor = Color.FromArgb(52, 64, 84)
        Me.Controls.Add(lblRepeat)

        txtRepeatPassword = New TextBox()
        txtRepeatPassword.SetBounds(22, 264, 456, 28)
        txtRepeatPassword.Font = New Font("Segoe UI", 9.0F)
        txtRepeatPassword.UseSystemPasswordChar = True
        Me.Controls.Add(txtRepeatPassword)

        Dim btnSave As New Button()
        btnSave.Text = "Kaydet"
        btnSave.SetBounds(378, 324, 100, 34)
        btnSave.BackColor = Color.FromArgb(15, 123, 63)
        btnSave.ForeColor = Color.White
        btnSave.FlatStyle = FlatStyle.Flat
        btnSave.FlatAppearance.BorderSize = 0
        btnSave.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        AddHandler btnSave.Click, AddressOf BtnSave_Click
        Me.Controls.Add(btnSave)

        Dim btnCancel As New Button()
        btnCancel.Text = "Vazgeç"
        btnCancel.SetBounds(266, 324, 100, 34)
        btnCancel.BackColor = Color.FromArgb(238, 242, 247)
        btnCancel.ForeColor = Color.FromArgb(52, 64, 84)
        btnCancel.FlatStyle = FlatStyle.Flat
        btnCancel.FlatAppearance.BorderSize = 0
        btnCancel.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        btnCancel.DialogResult = DialogResult.Cancel
        Me.Controls.Add(btnCancel)

        UpdateCurrentPasswordDisplay()

        Me.AcceptButton = btnSave
        Me.CancelButton = btnCancel

        AddHandler Me.Shown, Sub()
                                 txtNewPassword.Focus()
                             End Sub
    End Sub

    Private Sub CmbTargetUser_SelectedIndexChanged(sender As Object, e As EventArgs)
        UpdateCurrentPasswordDisplay()
    End Sub

    Private Sub UpdateCurrentPasswordDisplay()
        Dim targetUser = Convert.ToString(cmbTargetUser.SelectedItem)

        If String.IsNullOrWhiteSpace(targetUser) Then
            txtCurrentPassword.Text = ""
            Return
        End If

        If adminMode Then
            txtCurrentPassword.Text = store.GetVisiblePassword(targetUser)
        Else
            txtCurrentPassword.Text = "Yetkiniz yok"
        End If
    End Sub

    Private Sub BtnSave_Click(sender As Object, e As EventArgs)
        Dim targetUser = Convert.ToString(cmbTargetUser.SelectedItem)

        If String.IsNullOrWhiteSpace(txtNewPassword.Text) Then
            MessageBox.Show("Yeni şifre boş olamaz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If txtNewPassword.Text <> txtRepeatPassword.Text Then
            MessageBox.Show("Yeni şifreler aynı değil.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        store.SetPassword(targetUser, txtNewPassword.Text)

        UpdateCurrentPasswordDisplay()

        MessageBox.Show("Şifre değiştirildi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub
End Class
