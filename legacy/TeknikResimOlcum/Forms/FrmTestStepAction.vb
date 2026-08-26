Imports System.Drawing
Imports System.Windows.Forms

Public Class FrmTestStepAction
    Inherits Form

    Public Const ModeComplete As String = "COMPLETE"
    Public Const ModeSkip As String = "SKIP"
    Public Const ModeReopen As String = "REOPEN"

    Private ReadOnly actionMode As String
    Private ReadOnly cboResult As New ComboBox()
    Private ReadOnly txtExplanation As New TextBox()

    Public ReadOnly Property ResultText As String
        Get
            If actionMode = ModeComplete Then Return cboResult.Text.Trim()
            Return ""
        End Get
    End Property

    Public ReadOnly Property Explanation As String
        Get
            Return txtExplanation.Text.Trim()
        End Get
    End Property

    Public Sub New(stepItem As TestRequestStep, mode As String)
        If stepItem Is Nothing Then Throw New ArgumentNullException(NameOf(stepItem))
        actionMode = If(mode, "").Trim().ToUpperInvariant()
        If actionMode <> ModeComplete AndAlso actionMode <> ModeSkip AndAlso actionMode <> ModeReopen Then
            Throw New ArgumentException("Geçersiz test adımı işlemi.")
        End If

        AppIconService.Apply(Me)
        Text = ActionTitle()
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(700, 430)
        MinimumSize = New Size(620, 380)
        BackColor = Color.FromArgb(244, 247, 251)
        Font = New Font("Segoe UI", 9.0F)
        ShowInTaskbar = False

        BuildScreen(stepItem)
    End Sub

    Private Sub BuildScreen(stepItem As TestRequestStep)
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .Padding = New Padding(12),
            .BackColor = BackColor
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 54.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 88.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 56.0F))
        Controls.Add(root)

        root.Controls.Add(New Label() With {
            .Text = ActionTitle(),
            .Dock = DockStyle.Fill,
            .BackColor = HeaderColor(),
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 12.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(16, 0, 0, 0),
            .Margin = New Padding(0, 0, 0, 8)
        }, 0, 0)

        Dim info As New Label() With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.White,
            .ForeColor = Color.FromArgb(35, 53, 76),
            .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold),
            .Padding = New Padding(14, 10, 14, 10),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Text = stepItem.SortNo.ToString() & ". " & stepItem.TestName &
                    If(String.IsNullOrWhiteSpace(stepItem.TestDescription), "", Environment.NewLine & stepItem.TestDescription),
            .Margin = New Padding(0, 0, 0, 8),
            .AutoEllipsis = True
        }
        root.Controls.Add(info, 0, 1)

        Dim fields As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 2,
            .Padding = New Padding(12),
            .BackColor = Color.White,
            .Margin = New Padding(0, 0, 0, 8)
        }
        fields.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 145.0F))
        fields.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        fields.RowStyles.Add(New RowStyle(SizeType.Absolute, If(actionMode = ModeComplete, 48.0F, 0.0F)))
        fields.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.Controls.Add(fields, 0, 2)

        cboResult.Dock = DockStyle.Left
        cboResult.Width = 260
        cboResult.DropDownStyle = ComboBoxStyle.DropDownList
        cboResult.Items.AddRange({"TAMAMLANDI", "UYGUN", "UYGUN DEĞİL", "BİLGİ / DEĞERLENDİRME"})
        cboResult.SelectedIndex = 0
        cboResult.Visible = actionMode = ModeComplete

        Dim resultLabel As New Label() With {
            .Text = "Test Sonucu",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .Visible = actionMode = ModeComplete
        }
        fields.Controls.Add(resultLabel, 0, 0)
        fields.Controls.Add(cboResult, 1, 0)

        Dim explanationLabel = New Label() With {
            .Text = If(actionMode = ModeComplete, "Açıklama", "Gerekçe"),
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.TopLeft,
            .Padding = New Padding(0, 8, 0, 0),
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        }
        txtExplanation.Dock = DockStyle.Fill
        txtExplanation.Multiline = True
        txtExplanation.ScrollBars = ScrollBars.Vertical
        txtExplanation.MaxLength = 2000
        txtExplanation.PlaceholderText = If(actionMode = ModeComplete,
                                            "Ölçülen değer, yöntem veya gerekli test açıklaması...",
                                            "Bu işlem için zorunlu gerekçeyi yazın...")
        fields.Controls.Add(explanationLabel, 0, 1)
        fields.Controls.Add(txtExplanation, 1, 1)

        Dim footer As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False,
            .BackColor = Color.White,
            .Padding = New Padding(8, 10, 8, 8),
            .Margin = New Padding(0)
        }
        root.Controls.Add(footer, 0, 3)

        Dim btnCancel As New Button() With {
            .Text = "Vazgeç",
            .Width = 105,
            .Height = 34,
            .DialogResult = DialogResult.Cancel,
            .Margin = New Padding(8, 0, 0, 0)
        }
        Dim btnApply As New Button() With {
            .Text = ActionButtonText(),
            .Width = 175,
            .Height = 34,
            .BackColor = HeaderColor(),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .Margin = New Padding(8, 0, 0, 0),
            .Cursor = Cursors.Hand,
            .UseVisualStyleBackColor = False
        }
        AddHandler btnApply.Click, AddressOf Apply_Click
        footer.Controls.AddRange({btnCancel, btnApply})
        AcceptButton = btnApply
        CancelButton = btnCancel
    End Sub

    Private Sub Apply_Click(sender As Object, e As EventArgs)
        If actionMode <> ModeComplete AndAlso Explanation = "" Then
            MessageBox.Show("Bu işlem için gerekçe zorunludur.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtExplanation.Focus()
            Return
        End If
        If actionMode = ModeComplete AndAlso String.Equals(ResultText, "UYGUN DEĞİL", StringComparison.OrdinalIgnoreCase) AndAlso Explanation = "" Then
            MessageBox.Show("Uygun değil sonucu için açıklama zorunludur.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtExplanation.Focus()
            Return
        End If
        DialogResult = DialogResult.OK
        Close()
    End Sub

    Private Function ActionTitle() As String
        If actionMode = ModeSkip Then Return "Test Adımını Gerekçe ile Atla"
        If actionMode = ModeReopen Then Return "Test Adımını Geri Aç"
        Return "Sıradaki Testi Tamamla"
    End Function

    Private Function ActionButtonText() As String
        If actionMode = ModeSkip Then Return "Testi Atla"
        If actionMode = ModeReopen Then Return "Testi Geri Aç"
        Return "✓ Testi Tamamla"
    End Function

    Private Function HeaderColor() As Color
        If actionMode = ModeSkip Then Return Color.FromArgb(176, 111, 0)
        If actionMode = ModeReopen Then Return Color.FromArgb(167, 45, 45)
        Return Color.FromArgb(24, 126, 67)
    End Function
End Class
