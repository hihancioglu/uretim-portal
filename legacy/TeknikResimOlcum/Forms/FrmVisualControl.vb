Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmVisualControl
    Inherits Form

    Private ReadOnly grid As New DataGridView()
    Private ReadOnly lblInfo As New Label()
    Private ReadOnly lblExpectedInfo As New Label()

    Private ReadOnly recordId As String
    Private ReadOnly trCode As String
    Private ReadOnly drawingRev As String
    Private ReadOnly drawingScope As String
    Private ReadOnly lotNo As String
    Private ReadOnly serialNo As String
    Private ReadOnly eyeNo As String
    Private ReadOnly eyeCount As String

    Private Shared ReadOnly VisualControlNames As String() = {
        "Çapak",
        "Çöküntü",
        "Çatlak",
        "Eksik Baskı",
        "Sıyırma",
        "Delik",
        "Oring yuva kontrolü",
        "Dişli Yatakları Uygunluğu",
        "Yüzeyde şekil bozukluğu",
        "Renk bozukluğu",
        "İtici izi",
        "Mıknatıs yuva kontrolü",
        "Kullanılan malzeme kontrolü",
        "Dış çapı kontrolü",
        "Taş yatağı kontrolü",
        "Balans kontrolü",
        "Çanak derinlik kontrolü",
        "Diş sayısı kontrolü",
        "Sıcak su malzeme kontrolü",
        "Diğer"
    }

    Public Sub New(recordId As String, trCode As String, drawingRev As String, lotNo As String, serialNo As String, eyeCount As String, eyeNo As String, Optional drawingScope As String = "")
        AuthorizationService.Require(AppState.CanOpenMeasurement, "Gorsel Kontrol")
        AppIconService.Apply(Me)
        Me.recordId = recordId
        Me.trCode = trCode
        Me.drawingRev = drawingRev
        Me.drawingScope = ProductInfo.NormalizeDrawingScope(drawingScope)
        Me.lotNo = lotNo
        Me.serialNo = serialNo
        Me.eyeNo = eyeNo
        Me.eyeCount = eyeCount

        Text = "Görsel Kontrol"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(1040, 820)
        MinimumSize = New Size(700, 520)
        BackColor = Color.White
        AddHandler FormClosing, AddressOf FrmVisualControl_FormClosing

        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .BackColor = Color.White
        }
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 128.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 70.0F))
        Controls.Add(layout)

        Dim top As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(10, 5, 10, 5),
            .BackColor = SystemColors.Control,
            .ColumnCount = 1,
            .RowCount = 4
        }
        For i As Integer = 0 To 3
            top.RowStyles.Add(New RowStyle(SizeType.Percent, 25.0F))
        Next

        lblInfo.Text = $"GÖRSEL KONTROL   |   Göz: {eyeNo}/{eyeCount}   |   TR: {trCode} / {drawingRev}   |   İş Emri No: {lotNo}   |   Seri: {serialNo}"
        lblInfo.Font = New Font("Segoe UI", 12.0F, FontStyle.Bold)
        lblInfo.Dock = DockStyle.Fill
        lblInfo.ForeColor = Color.FromArgb(30, 30, 30)
        lblInfo.BackColor = Color.Transparent
        lblInfo.AutoEllipsis = True
        lblInfo.TextAlign = ContentAlignment.MiddleLeft

        lblExpectedInfo.Text = GetExpectedInfoText()
        lblExpectedInfo.Dock = DockStyle.Fill
        lblExpectedInfo.Font = New Font("Segoe UI", 10.0F, FontStyle.Bold)
        lblExpectedInfo.ForeColor = Color.FromArgb(35, 65, 110)
        lblExpectedInfo.BackColor = Color.Transparent
        lblExpectedInfo.AutoEllipsis = True
        lblExpectedInfo.TextAlign = ContentAlignment.MiddleLeft

        Dim lblHint As New Label() With {
            .Text = "Zorunlu: Malzeme ve Renk satırlarında UYGUN veya UYGUN DEĞİL seçilmelidir. Sonuç hücresine tıklayınca hızlı seçim yapılır.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.DimGray,
            .BackColor = Color.Transparent,
            .AutoEllipsis = True,
            .TextAlign = ContentAlignment.MiddleLeft
        }

        Dim lblHint2 As New Label() With {
            .Text = "Kısayol: Space/Enter = UYGUN, N = UYGUN DEĞİL. Alt butonlardan seçili satır veya tüm satırlar hızlı işaretlenebilir.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.DimGray,
            .BackColor = Color.Transparent,
            .AutoEllipsis = True,
            .TextAlign = ContentAlignment.MiddleLeft
        }

        top.Controls.Add(lblInfo, 0, 0)
        top.Controls.Add(lblExpectedInfo, 0, 1)
        top.Controls.Add(lblHint, 0, 2)
        top.Controls.Add(lblHint2, 0, 3)
        layout.Controls.Add(top, 0, 0)

        ConfigureGrid()
        layout.Controls.Add(grid, 0, 1)

        Dim bottom As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(10, 10, 10, 6),
            .BackColor = SystemColors.Control,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .AutoScroll = True
        }

        Dim btnSave As New Button() With {.Text = "Kaydet ve Sonraki Göze Geç", .Width = 210, .Height = 38}
        btnSave.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        AddHandler btnSave.Click, AddressOf Save_Click

        Dim btnMandatoryOk As New Button() With {.Text = "Malzeme/Renk Uygun", .Width = 160, .Height = 38}
        AddHandler btnMandatoryOk.Click, AddressOf MandatoryRowsOk_Click

        Dim btnSelectedOk As New Button() With {.Text = "Seçili Uygun", .Width = 120, .Height = 38}
        AddHandler btnSelectedOk.Click, Sub() SetSelectedResult("UYGUN")

        Dim btnSelectedNotOk As New Button() With {.Text = "Seçili Uygun Değil", .Width = 150, .Height = 38}
        AddHandler btnSelectedNotOk.Click, Sub() SetSelectedResult("UYGUN DEĞİL")

        Dim btnAllOk As New Button() With {.Text = "Tümünü Uygun Yap", .Width = 145, .Height = 38}
        AddHandler btnAllOk.Click, AddressOf AllRowsOk_Click

        Dim btnClear As New Button() With {.Text = "Temizle", .Width = 100, .Height = 38}
        AddHandler btnClear.Click, AddressOf ClearResults_Click

        bottom.Controls.AddRange({btnSave, btnMandatoryOk, btnSelectedOk, btnSelectedNotOk, btnAllOk, btnClear})
        layout.Controls.Add(bottom, 0, 2)

        LoadRows()
    End Sub

    Private Sub FrmVisualControl_FormClosing(sender As Object, e As FormClosingEventArgs)
        If DialogResult = DialogResult.None Then
            DialogResult = DialogResult.Cancel
        End If
    End Sub

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
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
        grid.RowTemplate.Height = 32
        grid.ScrollBars = ScrollBars.Vertical

        Dim colName As New DataGridViewTextBoxColumn() With {
            .Name = "ControlName",
            .HeaderText = "Görsel Kontrol",
            .ReadOnly = True,
            .FillWeight = 48
        }

        Dim colResult As New DataGridViewComboBoxColumn() With {
            .Name = "Result",
            .HeaderText = "Sonuç",
            .FillWeight = 20,
            .FlatStyle = FlatStyle.Flat
        }
        colResult.Items.AddRange(New Object() {"", "UYGUN", "UYGUN DEĞİL"})

        Dim colNote As New DataGridViewTextBoxColumn() With {
            .Name = "Note",
            .HeaderText = "Not",
            .FillWeight = 32
        }

        grid.Columns.AddRange({colName, colResult, colNote})
        AddHandler grid.CurrentCellDirtyStateChanged, AddressOf Grid_CurrentCellDirtyStateChanged
        AddHandler grid.CellFormatting, AddressOf Grid_CellFormatting
        AddHandler grid.CellClick, AddressOf Grid_CellClick
        AddHandler grid.KeyDown, AddressOf Grid_KeyDown
    End Sub

    Private Sub LoadRows()
        grid.Rows.Clear()

        AddMetadataControlRow("Malzeme bilgisi kontrolü", GetExpectedNote("Malzeme", GetExpectedMaterial()))
        AddMetadataControlRow("Renk bilgisi kontrolü", GetExpectedNote("Renk", GetExpectedColor()))

        For Each controlName As String In VisualControlNames
            Dim idx = grid.Rows.Add(controlName, "", "")
            grid.Rows(idx).Cells("ControlName").Value = controlName
        Next
    End Sub

    Private Sub AddMetadataControlRow(controlName As String, noteText As String)
        Dim idx = grid.Rows.Add(controlName, "", noteText)
        grid.Rows(idx).Cells("ControlName").Value = controlName
        grid.Rows(idx).DefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.Rows(idx).DefaultCellStyle.BackColor = Color.FromArgb(245, 248, 255)
    End Sub

    Private Function CurrentProduct() As ProductInfo
        Return DataService.GetProducts(False).
            FirstOrDefault(Function(p) String.Equals(p.TrCode, trCode, StringComparison.OrdinalIgnoreCase) AndAlso
                                        String.Equals(p.DrawingRev, drawingRev, StringComparison.OrdinalIgnoreCase) AndAlso
                                        (drawingScope = "" OrElse String.Equals(ProductInfo.NormalizeDrawingScope(p.DrawingScope), drawingScope, StringComparison.OrdinalIgnoreCase)))
    End Function

    Private Function GetExpectedMaterial() As String
        Dim p = CurrentProduct()
        If p Is Nothing Then Return ""
        Return p.Material
    End Function

    Private Function GetExpectedColor() As String
        Dim p = CurrentProduct()
        If p Is Nothing Then Return ""
        Return p.ColorName
    End Function

    Private Function GetExpectedNote(labelText As String, valueText As String) As String
        If String.IsNullOrWhiteSpace(valueText) Then
            Return "Beklenen " & labelText & " bilgisi tanımlı değil."
        End If

        Return "Beklenen " & labelText & ": " & valueText
    End Function

    Private Function GetExpectedInfoText() As String
        Dim p = CurrentProduct()
        If p Is Nothing Then Return "Beklenen Malzeme: -    Beklenen Renk: -"

        Return "Beklenen Malzeme: " & If(p.Material = "", "-", p.Material) &
               "    |    Beklenen Renk: " & If(p.ColorName = "", "-", p.ColorName)
    End Function

    Private Sub Grid_CurrentCellDirtyStateChanged(sender As Object, e As EventArgs)
        If grid.IsCurrentCellDirty Then
            grid.CommitEdit(DataGridViewDataErrorContexts.Commit)
        End If
    End Sub

    Private Sub Grid_CellClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return

        If e.ColumnIndex >= 0 AndAlso grid.Columns(e.ColumnIndex).Name = "Result" Then
            ToggleRowResult(e.RowIndex)
        End If
    End Sub

    Private Sub Grid_KeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode = Keys.Space OrElse e.KeyCode = Keys.Enter Then
            SetSelectedResult("UYGUN")
            e.Handled = True
        ElseIf e.KeyCode = Keys.N Then
            SetSelectedResult("UYGUN DEĞİL")
            e.Handled = True
        End If
    End Sub

    Private Sub ToggleRowResult(rowIndex As Integer)
        If rowIndex < 0 OrElse rowIndex >= grid.Rows.Count Then Return

        Dim current = Convert.ToString(grid.Rows(rowIndex).Cells("Result").Value).Trim().ToUpperInvariant()

        If current = "" OrElse current = "UYGUN DEĞİL" OrElse current = "UYGUNSUZ" OrElse current = "NOK" Then
            grid.Rows(rowIndex).Cells("Result").Value = "UYGUN"
        Else
            grid.Rows(rowIndex).Cells("Result").Value = "UYGUN DEĞİL"
        End If

        grid.Rows(rowIndex).Selected = True
        grid.CurrentCell = grid.Rows(rowIndex).Cells("Result")
        grid.Refresh()
    End Sub

    Private Sub SetSelectedResult(resultText As String)
        If grid.CurrentRow Is Nothing Then Return

        grid.CurrentRow.Cells("Result").Value = resultText
        Dim nextIndex = Math.Min(grid.CurrentRow.Index + 1, grid.Rows.Count - 1)
        grid.Rows(nextIndex).Selected = True
        grid.CurrentCell = grid.Rows(nextIndex).Cells("Result")
        grid.Refresh()
    End Sub

    Private Sub Grid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse Not grid.Columns.Contains("Result") Then Return

        Dim resultText = Convert.ToString(grid.Rows(e.RowIndex).Cells("Result").Value).Trim().ToUpperInvariant()
        Dim controlName = Convert.ToString(grid.Rows(e.RowIndex).Cells("ControlName").Value).Trim()

        If resultText = "" AndAlso (controlName = "Malzeme bilgisi kontrolü" OrElse controlName = "Renk bilgisi kontrolü") Then
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.FromArgb(245, 248, 255)
            grid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.FromArgb(30, 50, 90)
        ElseIf resultText = "UYGUN" Then
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.Honeydew
            grid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.DarkGreen
        ElseIf resultText = "UYGUN DEĞİL" OrElse resultText = "UYGUNSUZ" OrElse resultText = "NOK" Then
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.MistyRose
            grid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.DarkRed
        Else
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = If(e.RowIndex Mod 2 = 0, Color.White, Color.FromArgb(248, 248, 248))
            grid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.Black
        End If
    End Sub

    Private Sub MandatoryRowsOk_Click(sender As Object, e As EventArgs)
        For Each row As DataGridViewRow In grid.Rows
            Dim controlName = Convert.ToString(row.Cells("ControlName").Value).Trim()
            If controlName = "Malzeme bilgisi kontrolü" OrElse controlName = "Renk bilgisi kontrolü" Then
                row.Cells("Result").Value = "UYGUN"
            End If
        Next
        grid.Refresh()
    End Sub

    Private Sub AllRowsOk_Click(sender As Object, e As EventArgs)
        For Each row As DataGridViewRow In grid.Rows
            row.Cells("Result").Value = "UYGUN"
        Next
    End Sub

    Private Sub ClearResults_Click(sender As Object, e As EventArgs)
        For Each row As DataGridViewRow In grid.Rows
            row.Cells("Result").Value = ""
            row.Cells("Note").Value = ""
        Next
    End Sub

    Private Function ValidateMaterialAndColorResults() As Boolean
        For Each row As DataGridViewRow In grid.Rows
            Dim controlName = Convert.ToString(row.Cells("ControlName").Value).Trim()
            If controlName <> "Malzeme bilgisi kontrolü" AndAlso controlName <> "Renk bilgisi kontrolü" Then Continue For

            Dim resultText = Convert.ToString(row.Cells("Result").Value).Trim().ToUpperInvariant()
            If resultText = "UYGUNSUZ" Then resultText = "UYGUN DEĞİL"

            If resultText <> "UYGUN" AndAlso resultText <> "UYGUN DEĞİL" Then
                row.Selected = True
                grid.CurrentCell = row.Cells("Result")
                MessageBox.Show(controlName & " için UYGUN veya UYGUN DEĞİL seçilmeden kayıt tamamlanamaz.",
                                "Malzeme / Renk kontrolü gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return False
            End If
        Next

        Return True
    End Function

    Private Sub Save_Click(sender As Object, e As EventArgs)
        Try
            grid.EndEdit()

            If Not ValidateMaterialAndColorResults() Then Return

            Dim savedCount As Integer = 0
            Dim dt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")

            For Each row As DataGridViewRow In grid.Rows
                Dim result = Convert.ToString(row.Cells("Result").Value).Trim().ToUpperInvariant()
                If result = "UYGUNSUZ" Then result = "UYGUN DEĞİL"

                If result = "" Then Continue For

                If result <> "UYGUN" AndAlso result <> "UYGUN DEĞİL" AndAlso result <> "UYGUNSUZ" Then
                    MessageBox.Show("Sonuç yalnızca UYGUN veya UYGUN DEĞİL olabilir.", "Hatalı sonuç", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return
                End If

                Dim csvRow As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                    {"RecordId", recordId},
                    {"TrCode", trCode},
                    {"DrawingRev", drawingRev},
                    {"DrawingScope", drawingScope},
                    {"LotNo", lotNo},
                    {"SerialNo", serialNo},
                    {"EyeCount", eyeCount},
                    {"EyeNo", eyeNo},
                    {"OperatorName", AppState.CurrentUserName},
                    {"ComputerName", Environment.MachineName},
                    {"ControlDate", dt},
                    {"ControlName", Convert.ToString(row.Cells("ControlName").Value)},
                    {"IsSelected", "YES"},
                    {"Result", result},
                    {"Note", Convert.ToString(row.Cells("Note").Value)}
                }

                DataService.AppendVisualControl(csvRow)
                savedCount += 1
            Next

            AuditService.Log("VISUAL_CONTROL_SAVE", trCode, drawingRev, $"RecordId={recordId}; Saved={savedCount}")
            DialogResult = DialogResult.OK
            Close()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Görsel kontrol kayıt hatası", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub
End Class
