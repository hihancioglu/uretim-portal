Imports System.Drawing
Imports System.Windows.Forms

Public Class FrmInactivityWarning
    Inherits Form

    Private ReadOnly countdownTimer As New Timer()
    Private ReadOnly lblCountdown As New Label()
    Private remainingSeconds As Integer = 60

    Public Sub New()
        AppIconService.Apply(Me)
        Text = "Otomatik Kapanma Uyarısı"
        StartPosition = FormStartPosition.CenterScreen
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = True
        TopMost = True
        ClientSize = New Size(540, 275)
        MinimumSize = Size
        MaximumSize = Size
        AutoScroll = False
        BackColor = Color.White

        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .Padding = New Padding(24, 18, 24, 18),
            .BackColor = Color.White
        }
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 52.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 56.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(layout)

        Dim title As New Label() With {
            .Text = "Programı kullanmaya devam etmek istiyor musunuz?",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleCenter,
            .Font = New Font("Segoe UI", 13.0F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(40, 55, 80)
        }
        layout.Controls.Add(title, 0, 0)

        Dim explanation As New Label() With {
            .Text = "Program 10 dakikadır kullanılmıyor." & Environment.NewLine &
                    "Cevap verilmezse program otomatik olarak kapanacaktır.",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleCenter,
            .Font = New Font("Segoe UI", 10.0F),
            .ForeColor = Color.DimGray
        }
        layout.Controls.Add(explanation, 0, 1)

        lblCountdown.Dock = DockStyle.Fill
        lblCountdown.TextAlign = ContentAlignment.MiddleCenter
        lblCountdown.Font = New Font("Segoe UI", 12.0F, FontStyle.Bold)
        lblCountdown.ForeColor = Color.DarkRed
        layout.Controls.Add(lblCountdown, 0, 2)

        Dim buttons As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .Padding = New Padding(62, 8, 62, 0),
            .Margin = New Padding(0),
            .BackColor = Color.White
        }
        buttons.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        buttons.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        buttons.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim btnContinue As New Button() With {
            .Text = "Devam Et",
            .Width = 170,
            .Height = 42,
            .Font = New Font("Segoe UI", 11.0F, FontStyle.Bold),
            .BackColor = Color.FromArgb(225, 242, 228),
            .ForeColor = Color.DarkGreen,
            .DialogResult = DialogResult.OK,
            .Cursor = Cursors.Hand,
            .Anchor = AnchorStyles.None,
            .Margin = New Padding(6),
            .AutoEllipsis = False,
            .Tag = "RESPONSIVE_NO_AUTO_SCALE"
        }
        Dim btnClose As New Button() With {
            .Text = "Şimdi Kapat",
            .Width = 150,
            .Height = 42,
            .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold),
            .ForeColor = Color.DarkRed,
            .DialogResult = DialogResult.Cancel,
            .Cursor = Cursors.Hand,
            .Anchor = AnchorStyles.None,
            .Margin = New Padding(6),
            .AutoEllipsis = False,
            .Tag = "RESPONSIVE_NO_AUTO_SCALE"
        }
        buttons.Controls.Add(btnClose, 0, 0)
        buttons.Controls.Add(btnContinue, 1, 0)
        layout.Controls.Add(buttons, 0, 3)

        AcceptButton = btnContinue
        CancelButton = btnClose

        countdownTimer.Interval = 1000
        AddHandler countdownTimer.Tick, AddressOf CountdownTimer_Tick
        AddHandler Shown,
            Sub()
                UpdateCountdownText()
                countdownTimer.Start()
                BringToFront()
                Activate()
            End Sub
        AddHandler FormClosed, Sub() countdownTimer.Stop()
    End Sub

    Private Sub CountdownTimer_Tick(sender As Object, e As EventArgs)
        remainingSeconds -= 1
        If remainingSeconds <= 0 Then
            countdownTimer.Stop()
            DialogResult = DialogResult.Cancel
            Close()
            Return
        End If

        UpdateCountdownText()
    End Sub

    Private Sub UpdateCountdownText()
        lblCountdown.Text = remainingSeconds.ToString() & " saniye içinde otomatik kapanacak"
    End Sub
End Class
