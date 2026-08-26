Imports System.Drawing
Imports System.Windows.Forms

Public Class FrmInitialAdminSetup
    Inherits Form

    Private ReadOnly txtPassword As New TextBox()
    Private ReadOnly txtConfirm As New TextBox()

    Public Sub New()
        AppIconService.Apply(Me)
        Text = "Admin Parolası Belirle"
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
            .RowCount = 5,
            .Dock = DockStyle.Fill
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 145.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 270.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 70.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 45.0F))
        Controls.Add(layout)

        Dim info As New Label() With {
            .Text = "Admin hesabı için kullanmak istediğiniz kalıcı parolayı belirleyin." &
                    Environment.NewLine & "Bu ekran yalnızca ilk kurulumda gösterilir.",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .ForeColor = Color.DimGray
        }
        layout.SetColumnSpan(info, 2)
        layout.Controls.Add(info, 0, 0)

        AddPasswordRow(layout, "Admin Parolası", txtPassword, 1)
        AddPasswordRow(layout, "Parola Tekrar", txtConfirm, 2)

        Dim showPassword As New CheckBox() With {
            .Text = "Parolayı göster",
            .Dock = DockStyle.Fill,
            .AutoSize = True
        }
        AddHandler showPassword.CheckedChanged,
            Sub()
                txtPassword.UseSystemPasswordChar = Not showPassword.Checked
                txtConfirm.UseSystemPasswordChar = Not showPassword.Checked
            End Sub
        layout.Controls.Add(showPassword, 1, 3)

        Dim buttons As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False
        }
        Dim btnSave As New Button() With {.Text = "Parolayı Kaydet", .Width = 140, .Height = 32}
        Dim btnCancel As New Button() With {
            .Text = "İptal",
            .Width = 90,
            .Height = 32,
            .DialogResult = DialogResult.Cancel
        }
        AddHandler btnSave.Click, AddressOf Save_Click
        buttons.Controls.AddRange({btnSave, btnCancel})
        layout.SetColumnSpan(buttons, 2)
        layout.Controls.Add(buttons, 0, 4)

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
            If txtPassword.Text = "" Then
                MessageBox.Show("Admin parolası boş olamaz.", "Parola gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                txtPassword.Focus()
                Return
            End If

            If txtPassword.Text <> txtConfirm.Text Then
                MessageBox.Show("Parola ve tekrar alanı aynı değil.", "Parolalar eşleşmiyor", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                txtConfirm.SelectAll()
                txtConfirm.Focus()
                Return
            End If

            UserService.SetInitialAdminPassword(txtPassword.Text)
            MessageBox.Show(
                "Admin parolası kaydedildi. Bundan sonraki açılışlarda bu uyarı gösterilmeyecek.",
                "Admin parolası hazır",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information)
            DialogResult = DialogResult.OK
            Close()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Admin parolası kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub
End Class
