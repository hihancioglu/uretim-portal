Imports System
Imports System.Drawing
Imports System.Windows.Forms

Public Class LoginForm
    Inherits Form

    Private ReadOnly store As UserStore
    Private cmbUser As ComboBox
    Private txtPassword As TextBox

    Public Property LoggedInUser As String = ""

    Public Sub New(userStore As UserStore)
        store = userStore

        Me.Text = "Kullanıcı Girişi"
        Me.StartPosition = FormStartPosition.CenterParent
        Me.ClientSize = New Size(440, 250)
        Me.MinimumSize = New Size(456, 289)
        Me.MaximumSize = New Size(456, 289)
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
        lblHeader.Text = "Kullanıcı bilgilerini girin"
        lblHeader.SetBounds(22, 18, 390, 24)
        lblHeader.Font = New Font("Segoe UI", 10.0F, FontStyle.Bold)
        lblHeader.ForeColor = Color.FromArgb(31, 78, 121)
        Me.Controls.Add(lblHeader)

        Dim lblUser As New Label()
        lblUser.Text = "Kullanıcı"
        lblUser.SetBounds(22, 56, 390, 20)
        lblUser.Font = New Font("Segoe UI", 8.8F, FontStyle.Bold)
        lblUser.ForeColor = Color.FromArgb(52, 64, 84)
        Me.Controls.Add(lblUser)

        cmbUser = New ComboBox()
        cmbUser.SetBounds(22, 78, 396, 28)
        cmbUser.DropDownStyle = ComboBoxStyle.DropDownList
        cmbUser.Font = New Font("Segoe UI", 9.0F)
        cmbUser.Items.AddRange(store.GetUsers().ToArray())
        If cmbUser.Items.Count > 0 Then cmbUser.SelectedIndex = 0
        Me.Controls.Add(cmbUser)

        Dim lblPassword As New Label()
        lblPassword.Text = "Şifre"
        lblPassword.SetBounds(22, 118, 390, 20)
        lblPassword.Font = New Font("Segoe UI", 8.8F, FontStyle.Bold)
        lblPassword.ForeColor = Color.FromArgb(52, 64, 84)
        Me.Controls.Add(lblPassword)

        txtPassword = New TextBox()
        txtPassword.SetBounds(22, 140, 396, 28)
        txtPassword.Font = New Font("Segoe UI", 9.0F)
        txtPassword.UseSystemPasswordChar = True
        Me.Controls.Add(txtPassword)

        Dim btnLogin As New Button()
        btnLogin.Text = "Giriş"
        btnLogin.SetBounds(318, 194, 100, 34)
        btnLogin.BackColor = Color.FromArgb(31, 78, 121)
        btnLogin.ForeColor = Color.White
        btnLogin.FlatStyle = FlatStyle.Flat
        btnLogin.FlatAppearance.BorderSize = 0
        btnLogin.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        AddHandler btnLogin.Click, AddressOf BtnLogin_Click
        Me.Controls.Add(btnLogin)

        Dim btnCancel As New Button()
        btnCancel.Text = "Vazgeç"
        btnCancel.SetBounds(206, 194, 100, 34)
        btnCancel.BackColor = Color.FromArgb(238, 242, 247)
        btnCancel.ForeColor = Color.FromArgb(52, 64, 84)
        btnCancel.FlatStyle = FlatStyle.Flat
        btnCancel.FlatAppearance.BorderSize = 0
        btnCancel.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        btnCancel.DialogResult = DialogResult.Cancel
        Me.Controls.Add(btnCancel)

        Me.AcceptButton = btnLogin
        Me.CancelButton = btnCancel

        AddHandler Me.Shown, Sub()
                                 txtPassword.Focus()
                             End Sub
    End Sub

    Private Sub BtnLogin_Click(sender As Object, e As EventArgs)
        Dim userName = Convert.ToString(cmbUser.SelectedItem)

        If store.ValidatePassword(userName, txtPassword.Text) Then
            LoggedInUser = userName
            Me.DialogResult = DialogResult.OK
            Me.Close()
        Else
            MessageBox.Show("Kullanıcı adı veya şifre hatalı.", "Giriş Başarısız", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtPassword.SelectAll()
            txtPassword.Focus()
        End If
    End Sub
End Class
