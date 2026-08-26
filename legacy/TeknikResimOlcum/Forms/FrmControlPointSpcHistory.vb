Imports System.Data
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmControlPointSpcHistory
    Inherits Form

    Private ReadOnly sourcePoint As ControlPoint
    Private ReadOnly lblTitle As New Label()
    Private ReadOnly lblSummary As New Label()
    Private ReadOnly grid As New DataGridView()

    Public Sub New(controlPoint As ControlPoint)
        If controlPoint Is Nothing Then Throw New ArgumentNullException(NameOf(controlPoint))
        sourcePoint = controlPoint

        AppIconService.Apply(Me)
        Text = "SPC Ölçüm Geçmişi"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(1180, 680)
        MinimumSize = New Size(820, 520)

        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .BackColor = Color.White,
            .Padding = New Padding(10)
        }
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 46.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(layout)

        lblTitle.Dock = DockStyle.Fill
        lblTitle.TextAlign = ContentAlignment.MiddleLeft
        lblTitle.Font = New Font("Segoe UI", 12.0F, FontStyle.Bold)
        lblTitle.ForeColor = Color.FromArgb(31, 71, 126)
        lblTitle.Text = BuildTitle()
        layout.Controls.Add(lblTitle, 0, 0)

        lblSummary.Dock = DockStyle.Fill
        lblSummary.TextAlign = ContentAlignment.MiddleLeft
        lblSummary.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblSummary.ForeColor = Color.FromArgb(42, 70, 105)
        lblSummary.BackColor = Color.FromArgb(239, 246, 255)
        lblSummary.Padding = New Padding(12, 0, 12, 0)
        layout.Controls.Add(lblSummary, 0, 1)

        ConfigureGrid()
        layout.Controls.Add(grid, 0, 2)

        LoadRows()
    End Sub

    Private Function BuildTitle() As String
        Dim spcKey = If(String.IsNullOrWhiteSpace(sourcePoint.SpcKey), sourcePoint.MeasureId, sourcePoint.SpcKey)
        Return sourcePoint.TrCode & " / " & sourcePoint.DrawingRev &
               "   |   SPC: " & spcKey &
               "   |   Seçili Ölçü: " & sourcePoint.MeasureId &
               "   |   V" & Math.Max(1, sourcePoint.MeasureVersion).ToString() &
               If(String.IsNullOrWhiteSpace(sourcePoint.MeasureName), "", "   |   " & sourcePoint.MeasureName)
    End Function

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.RowHeadersVisible = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.MultiSelect = False
        grid.AutoGenerateColumns = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(228, 236, 247)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.BackgroundColor = Color.White
        grid.GridColor = Color.Gainsboro
        grid.RowTemplate.Height = 27

        grid.Columns.Add(MakeColumn("MeasurementDate", "Tarih / Saat", 135, 115))
        grid.Columns.Add(MakeColumn("EyeNo", "Göz", 45, 35))
        grid.Columns.Add(MakeColumn("MeasureId", "Ölçü No", 125, 95))
        grid.Columns.Add(MakeColumn("MeasureVersion", "V.", 45, 35))
        grid.Columns.Add(MakeColumn("MeasuredValue", "Değer", 80, 60))
        grid.Columns.Add(MakeColumn("Result", "Sonuç", 70, 55))
        grid.Columns.Add(MakeColumn("Limits", "Limit", 120, 85))
        grid.Columns.Add(MakeColumn("OperatorName", "Ölçen", 100, 80))
        grid.Columns.Add(MakeColumn("LotNo", "İş Emri", 95, 70))
        grid.Columns.Add(MakeColumn("SerialNo", "Seri", 95, 70))
        grid.Columns.Add(MakeColumn("RecordId", "Kayıt No", 155, 105))
        grid.Columns.Add(MakeColumn("Note", "Not", 150, 120))

        AddHandler grid.CellFormatting, AddressOf Grid_CellFormatting
    End Sub

    Private Function MakeColumn(name As String, header As String, width As Integer, fillWeight As Single) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .DataPropertyName = name,
            .HeaderText = header,
            .Width = width,
            .MinimumWidth = 35,
            .FillWeight = fillWeight,
            .SortMode = DataGridViewColumnSortMode.Automatic
        }
    End Function

    Private Sub LoadRows()
        Dim targetSpcKey = If(String.IsNullOrWhiteSpace(sourcePoint.SpcKey), sourcePoint.MeasureId, sourcePoint.SpcKey).Trim()
        Dim normalizedScope = ProductInfo.NormalizeDrawingScope(sourcePoint.DrawingScope)

        Dim rows = DataService.GetMeasurementRows().
            Where(Function(row)
                      If Not String.Equals(DataService.GetValue(row, "TrCode"), sourcePoint.TrCode, StringComparison.OrdinalIgnoreCase) Then Return False
                      If Not String.Equals(DataService.GetValue(row, "DrawingRev"), sourcePoint.DrawingRev, StringComparison.OrdinalIgnoreCase) Then Return False
                      If Not String.Equals(ProductInfo.NormalizeDrawingScope(DataService.GetValue(row, "DrawingScope")), normalizedScope, StringComparison.OrdinalIgnoreCase) Then Return False

                      Dim rowSpc = DataService.GetValue(row, "SpcKey").Trim()
                      If rowSpc = "" Then rowSpc = DataService.GetValue(row, "MeasureId").Trim()
                      Return String.Equals(rowSpc, targetSpcKey, StringComparison.OrdinalIgnoreCase) OrElse
                             String.Equals(DataService.GetValue(row, "MeasureId"), sourcePoint.MeasureId, StringComparison.OrdinalIgnoreCase)
                  End Function).
            OrderBy(Function(row) ParseDateSafe(DataService.GetValue(row, "MeasurementDate"))).
            ThenBy(Function(row) ParseIntSafe(DataService.GetValue(row, "EyeNo"))).
            ThenBy(Function(row) Math.Max(1, ParseIntSafe(DataService.GetValue(row, "MeasureVersion")))).
            ToList()

        Dim table As New DataTable()
        For Each col In {"MeasurementDate", "EyeNo", "MeasureId", "MeasureVersion", "MeasuredValue", "Result", "Limits", "OperatorName", "LotNo", "SerialNo", "RecordId", "Note"}
            table.Columns.Add(col)
        Next

        Dim numericValues As New List(Of Decimal)()
        For Each row In rows
            Dim valueText = DataService.GetValue(row, "MeasuredValue")
            Dim measured = NumberUtil.CsvToDec(valueText)
            If valueText.Trim() <> "" Then numericValues.Add(measured)

            table.Rows.Add(
                DataService.GetValue(row, "MeasurementDate"),
                DataService.GetValue(row, "EyeNo"),
                DataService.GetValue(row, "MeasureId"),
                If(String.IsNullOrWhiteSpace(DataService.GetValue(row, "MeasureVersion")), "1", DataService.GetValue(row, "MeasureVersion")),
                valueText,
                DataService.GetValue(row, "Result"),
                DataService.GetValue(row, "LowerLimit") & " - " & DataService.GetValue(row, "UpperLimit"),
                DataService.GetValue(row, "OperatorName"),
                DataService.GetValue(row, "LotNo"),
                DataService.GetValue(row, "SerialNo"),
                DataService.GetValue(row, "RecordId"),
                DataService.GetValue(row, "Note"))
        Next

        grid.DataSource = table

        Dim okCount = rows.Where(Function(row) String.Equals(DataService.GetValue(row, "Result"), "OK", StringComparison.OrdinalIgnoreCase)).Count()
        Dim nokCount = rows.Where(Function(row) String.Equals(DataService.GetValue(row, "Result"), "NOK", StringComparison.OrdinalIgnoreCase)).Count()
        Dim invalidCount = rows.Where(Function(row) String.Equals(DataService.GetValue(row, "Result"), "HATALI", StringComparison.OrdinalIgnoreCase)).Count()
        Dim versionText = String.Join(", ", rows.
                                      Select(Function(row) If(String.IsNullOrWhiteSpace(DataService.GetValue(row, "MeasureVersion")), "1", DataService.GetValue(row, "MeasureVersion"))).
                                      Distinct(StringComparer.OrdinalIgnoreCase).
                                      OrderBy(Function(value) ParseIntSafe(value)).
                                      Select(Function(value) "V" & value))

        Dim stats = ""
        If numericValues.Count > 0 Then
            stats = "   |   Min: " & NumberUtil.DecToCsv(numericValues.Min()) &
                    "   |   Ort: " & NumberUtil.DecToCsv(numericValues.Average()) &
                    "   |   Max: " & NumberUtil.DecToCsv(numericValues.Max())
        End If

        lblSummary.Text = "Kayıt: " & rows.Count.ToString() &
                          "   |   OK: " & okCount.ToString() &
                          "   |   NOK: " & nokCount.ToString() &
                          "   |   Hatalı: " & invalidCount.ToString() &
                          If(versionText = "", "", "   |   Versiyonlar: " & versionText) &
                          stats
    End Sub

    Private Sub Grid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse e.RowIndex >= grid.Rows.Count Then Return
        Dim resultText = Convert.ToString(grid.Rows(e.RowIndex).Cells("Result").Value)
        If String.Equals(resultText, "NOK", StringComparison.OrdinalIgnoreCase) Then
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.MistyRose
            grid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.DarkRed
        ElseIf String.Equals(resultText, "OK", StringComparison.OrdinalIgnoreCase) Then
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.Honeydew
            grid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.DarkGreen
        ElseIf String.Equals(resultText, "HATALI", StringComparison.OrdinalIgnoreCase) Then
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.LemonChiffon
            grid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.DarkGoldenrod
        End If
    End Sub

    Private Shared Function ParseDateSafe(value As String) As DateTime
        Dim parsed As DateTime
        If DateTime.TryParse(If(value, "").Trim(), parsed) Then Return parsed
        Return DateTime.MinValue
    End Function

    Private Shared Function ParseIntSafe(value As String) As Integer
        Dim parsed As Integer
        If Integer.TryParse(If(value, "").Trim(), parsed) Then Return parsed
        Return 0
    End Function
End Class
