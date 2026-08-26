Imports System.Drawing
Imports System.Windows.Forms

Public Class FrmMeasurementCorrection
    Inherits Form

    Private ReadOnly txtNewValue As New TextBox()
    Private ReadOnly txtReason As New TextBox()

    Public ReadOnly Property NewValueText As String
        Get
            Return txtNewValue.Text.Trim()
        End Get
    End Property

    Public ReadOnly Property CorrectionReason As String
        Get
            Return txtReason.Text.Trim()
        End Get
    End Property

    Public Sub New(recordId As String,
                   trCode As String,
                   eyeNo As String,
                   measureId As String,
                   measureName As String,
                   oldValue As String,
                   lowerLimit As String,
                   upperLimit As String,
                   measurementDate As String,
                   operatorName As String)
        AuthorizationService.Require(AppState.IsAdmin, "Geçmiş Ölçüm Düzeltme")
        AppIconService.Apply(Me)

        Text = "Geçmiş Ölçüm Değerini Düzelt"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(760, 510)
        MinimumSize = New Size(680, 470)
        MaximizeBox = False
        MinimizeBox = False
        BackColor = Color.FromArgb(244, 247, 251)
        Font = New Font("Segoe UI", 9.0F)

        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .Padding = New Padding(12),
            .BackColor = BackColor
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 52.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 180.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 54.0F))
        Controls.Add(root)

        root.Controls.Add(New Label() With {
            .Text = "ADMIN — GEÇMİŞ ÖLÇÜM DÜZELTME",
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(31, 71, 126),
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 12.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(16, 0, 0, 0),
            .Margin = New Padding(0, 0, 0, 8)
        }, 0, 0)

        Dim summary As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 4,
            .RowCount = 5,
            .BackColor = Color.White,
            .Padding = New Padding(12),
            .Margin = New Padding(0, 0, 0, 8)
        }
        summary.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 105.0F))
        summary.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        summary.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 105.0F))
        summary.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        For index As Integer = 0 To 4
            summary.RowStyles.Add(New RowStyle(SizeType.Percent, 20.0F))
        Next
        AddSummary(summary, "Kayıt No", recordId, 0, 0)
        AddSummary(summary, "TR Kodu", trCode, 2, 0)
        AddSummary(summary, "Göz No", eyeNo, 0, 1)
        AddSummary(summary, "Ölçü No", measureId, 2, 1)
        AddSummary(summary, "Ölçü Adı", measureName, 0, 2, 3)
        AddSummary(summary, "Eski Değer", oldValue, 0, 3)
        AddSummary(summary, "Limit", lowerLimit & " - " & upperLimit, 2, 3)
        AddSummary(summary, "Ölçüm Tarihi", measurementDate, 0, 4)
        AddSummary(summary, "Ölçen", operatorName, 2, 4)
        root.Controls.Add(summary, 0, 1)

        Dim editor As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 3,
            .BackColor = Color.White,
            .Padding = New Padding(12),
            .Margin = New Padding(0, 0, 0, 8)
        }
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 120.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 30.0F))
        root.Controls.Add(editor, 0, 2)

        editor.Controls.Add(MakeCaption("Yeni Değer"), 0, 0)
        txtNewValue.Dock = DockStyle.Left
        txtNewValue.Width = 180
        txtNewValue.Margin = New Padding(5, 6, 5, 6)
        txtNewValue.Text = oldValue
        editor.Controls.Add(txtNewValue, 1, 0)

        editor.Controls.Add(MakeCaption("Düzeltme Nedeni"), 0, 1)
        txtReason.Dock = DockStyle.Fill
        txtReason.Multiline = True
        txtReason.ScrollBars = ScrollBars.Vertical
        txtReason.MaxLength = 500
        txtReason.PlaceholderText = "Yanlış girişin nedenini ve doğru değerin kaynağını yazın"
        txtReason.Margin = New Padding(5)
        editor.Controls.Add(txtReason, 1, 1)
        editor.Controls.Add(New Label() With {
            .Text = "Eski değer silinmez; düzeltme geçmişinde kullanıcı, bilgisayar ve tarih bilgisiyle saklanır.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.FromArgb(125, 70, 0),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(5, 2, 5, 2)
        }, 1, 2)

        Dim actions As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False,
            .BackColor = Color.White,
            .Padding = New Padding(8, 9, 8, 7)
        }
        root.Controls.Add(actions, 0, 3)

        Dim btnCancel As New Button() With {.Text = "Vazgeç", .Width = 105, .Height = 34, .DialogResult = DialogResult.Cancel}
        Dim btnSave As New Button() With {
            .Text = "Düzeltmeyi Kaydet",
            .Width = 160,
            .Height = 34,
            .BackColor = Color.FromArgb(31, 71, 126),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        Dim btnOpenHistory As New Button() With {
            .Text = "Ölçüm Geçmişinde Aç",
            .Width = 175,
            .Height = 34,
            .BackColor = Color.FromArgb(232, 242, 255),
            .ForeColor = Color.FromArgb(31, 71, 126),
            .FlatStyle = FlatStyle.Flat
        }
        AddHandler btnSave.Click, AddressOf SaveClick
        AddHandler btnOpenHistory.Click,
            Sub()
                Try
                    Using historyForm As New FrmMeasurementHistory(recordId, eyeNo, measureId, measurementDate)
                        historyForm.ShowDialog(Me)
                    End Using
                Catch ex As UnauthorizedAccessException
                    AuthorizationService.ShowDenied(ex, Me)
                Catch ex As Exception
                    MessageBox.Show(ex.Message, "Ölçüm geçmişi açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End Sub
        actions.Controls.Add(btnCancel)
        actions.Controls.Add(btnSave)
        actions.Controls.Add(btnOpenHistory)
        AcceptButton = btnSave
        CancelButton = btnCancel
        AddHandler Shown, Sub()
                              txtNewValue.SelectAll()
                              txtNewValue.Focus()
                          End Sub
    End Sub

    Private Sub SaveClick(sender As Object, e As EventArgs)
        Dim parsedValue As Decimal = 0D
        If Not NumberUtil.TryParseDecimal(txtNewValue.Text, parsedValue) Then
            MessageBox.Show("Geçerli bir ölçüm değeri giriniz.", "Değer geçersiz", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtNewValue.Focus()
            Return
        End If
        If txtReason.Text.Trim() = "" Then
            MessageBox.Show("Düzeltme nedeni zorunludur.", "Düzeltme nedeni", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtReason.Focus()
            Return
        End If
        DialogResult = DialogResult.OK
        Close()
    End Sub

    Private Shared Function MakeCaption(textValue As String) As Label
        Return New Label() With {.Text = textValue, .Dock = DockStyle.Fill, .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold), .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(5)}
    End Function

    Private Shared Sub AddSummary(layout As TableLayoutPanel, caption As String, value As String, column As Integer, row As Integer, Optional valueSpan As Integer = 1)
        layout.Controls.Add(MakeCaption(caption), column, row)
        Dim valueLabel As New Label() With {
            .Text = If(String.IsNullOrWhiteSpace(value), "-", value),
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(247, 249, 252),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(8, 0, 8, 0),
            .AutoEllipsis = True,
            .Margin = New Padding(3)
        }
        layout.Controls.Add(valueLabel, column + 1, row)
        If valueSpan > 1 Then layout.SetColumnSpan(valueLabel, valueSpan)
    End Sub
End Class
