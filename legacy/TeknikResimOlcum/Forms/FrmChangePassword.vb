Imports System.Drawing
Imports System.Windows.Forms

Public Class FrmChangePassword
    Inherits Form

    Private ReadOnly txtCurrentPassword As New TextBox()
    Private ReadOnly txtNewPassword As New TextBox()
    Private ReadOnly txtConfirmPassword As New TextBox()
    Private ReadOnly forcedChange As Boolean

    Public Sub New(Optional forcedChange As Boolean = False)
        AppIconService.Apply(Me)
        Me.forcedChange = forcedChange

        Text = If(forcedChange, "Parola Değişikliği Zorunlu", "Şifremi Değiştir")
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        AutoSize = True
        AutoSizeMode = AutoSizeMode.GrowAndShrink
        Padding = New Padding(18)

        Dim layout As New TableLayoutPanel() With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 2,
            .RowCount = 6,
            .Dock = DockStyle.Fill
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 145.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 260.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 55.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        Controls.Add(layout)

        Dim info As New Label() With {
            .Text = If(
                forcedChange,
                "İlk veya geçici parolanızı değiştirmeden programa devam edemezsiniz.",
                "Yeni parolanızı giriniz."),
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .ForeColor = If(forcedChange, Color.DarkRed, Color.DimGray)
        }
        layout.SetColumnSpan(info, 2)
        layout.Controls.Add(info, 0, 0)

        AddPasswordRow(layout, "Mevcut Parola", txtCurrentPassword, 1)
        AddPasswordRow(layout, "Yeni Parola", txtNewPassword, 2)
        AddPasswordRow(layout, "Yeni Parola Tekrar", txtConfirmPassword, 3)

        Dim showPasswords As New CheckBox() With {
            .Text = "Parolaları göster",
            .Dock = DockStyle.Fill,
            .AutoSize = True
        }
        AddHandler showPasswords.CheckedChanged,
            Sub()
                Dim hidePasswords = Not showPasswords.Checked
                txtCurrentPassword.UseSystemPasswordChar = hidePasswords
                txtNewPassword.UseSystemPasswordChar = hidePasswords
                txtConfirmPassword.UseSystemPasswordChar = hidePasswords
            End Sub
        layout.Controls.Add(showPasswords, 1, 4)

        Dim buttons As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False
        }
        Dim btnSave As New Button() With {.Text = "Parolayı Değiştir", .Width = 140, .Height = 32}
        Dim btnCancel As New Button() With {
            .Text = If(forcedChange, "Girişi İptal Et", "İptal"),
            .Width = 110,
            .Height = 32,
            .DialogResult = DialogResult.Cancel
        }
        AddHandler btnSave.Click, AddressOf Save_Click
        buttons.Controls.AddRange({btnSave, btnCancel})
        layout.SetColumnSpan(buttons, 2)
        layout.Controls.Add(buttons, 0, 5)

        AcceptButton = btnSave
        CancelButton = btnCancel
    End Sub

    Private Shared Sub AddPasswordRow(layout As TableLayoutPanel, labelText As String, textBox As TextBox, rowIndex As Integer)
        layout.Controls.Add(New Label() With {
            .Text = labelText,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft
        }, 0, rowIndex)

        textBox.Dock = DockStyle.Fill
        textBox.Margin = New Padding(0, 6, 0, 6)
        textBox.UseSystemPasswordChar = True
        layout.Controls.Add(textBox, 1, rowIndex)
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        Try
            If txtNewPassword.Text <> txtConfirmPassword.Text Then
                MessageBox.Show(
                    "Yeni parola ile tekrar alanı aynı değil.",
                    "Parolalar eşleşmiyor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning)
                txtConfirmPassword.SelectAll()
                txtConfirmPassword.Focus()
                Return
            End If

            UserService.ChangeOwnPassword(txtCurrentPassword.Text, txtNewPassword.Text)
            MessageBox.Show(
                "Parolanız başarıyla değiştirildi.",
                "Parola değiştirildi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information)
            DialogResult = DialogResult.OK
            Close()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Parola değiştirilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub
End Class
