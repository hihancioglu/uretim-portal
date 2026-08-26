Imports System.Data
Imports System.Drawing
Imports System.Globalization
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmTicketMeasurementResults
    Inherits Form

    Private ReadOnly ticketId As String
    Private ReadOnly trCode As String
    Private ReadOnly drawingRev As String
    Private ReadOnly createdAtText As String
    Private ReadOnly closedAtText As String
    Private ReadOnly grid As New DataGridView()
    Private ReadOnly lblInfo As New Label()

    Public Sub New(ticketId As String, trCode As String, drawingRev As String, createdAtText As String, closedAtText As String)
        AuthorizationService.Require(AppState.CanOpenQualityTickets, "Ticket Olcumleri")
        AppIconService.Apply(Me)
        Me.ticketId = If(ticketId, "")
        Me.trCode = If(trCode, "")
        Me.drawingRev = If(drawingRev, "")
        Me.createdAtText = If(createdAtText, "")
        Me.closedAtText = If(closedAtText, "")

        Text = "Ticket Ölçümleri - " & Me.ticketId
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        MinimumSize = New Size(700, 480)
        BackColor = Color.White

        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .BackColor = Color.White
        }
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 64.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(layout)

        Dim top As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(12), .BackColor = SystemColors.Control}
        lblInfo.Dock = DockStyle.Fill
        lblInfo.TextAlign = ContentAlignment.MiddleLeft
        lblInfo.Font = New Font("Segoe UI", 9.5F, FontStyle.Bold)
        lblInfo.AutoEllipsis = True
        top.Controls.Add(lblInfo)
        layout.Controls.Add(top, 0, 0)

        ConfigureGrid()
        layout.Controls.Add(grid, 0, 1)

        AddHandler Shown, Sub() LoadGrid()
    End Sub

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
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
        grid.RowTemplate.Height = 28

        grid.Columns.Clear()
        grid.Columns.Add(MakeColumn("RecordId", "Ölçüm Kayıt No", 190))
        grid.Columns.Add(MakeColumn("RecordType", "Tip", 90))
        grid.Columns.Add(MakeColumn("RecordDate", "Tarih", 135))
        grid.Columns.Add(MakeColumn("TrCode", "TR Kodu", 100))
        grid.Columns.Add(MakeColumn("DrawingRev", "Rev.", 70))
        grid.Columns.Add(MakeColumn("LotNo", "İş Emri No", 110))
        grid.Columns.Add(MakeColumn("SerialNo", "Seri No", 100))
        grid.Columns.Add(MakeColumn("EyeNo", "Göz No", 70))
        grid.Columns.Add(MakeColumn("EyeCount", "Göz Adedi", 80))
        grid.Columns.Add(MakeColumn("RowCount", "Ölçü Satırı", 90))
        grid.Columns.Add(MakeColumn("NokCount", "NOK", 60))
        grid.Columns.Add(MakeColumn("VisualStatus", "Görsel Kontrol", 140))

        AddHandler grid.CellDoubleClick, AddressOf Grid_DoubleClick
        AddHandler grid.CellFormatting, AddressOf Grid_CellFormatting
    End Sub

    Private Function MakeColumn(name As String, header As String, width As Integer) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .DataPropertyName = name,
            .HeaderText = header,
            .Width = width,
            .MinimumWidth = 55,
            .SortMode = DataGridViewColumnSortMode.Automatic
        }
    End Function

    Private Sub LoadGrid()
        Dim measurementRows = DataService.GetMeasurementRows().
            Where(Function(r) String.Equals(DataService.GetValue(r, "ProductionTicketId"), ticketId, StringComparison.OrdinalIgnoreCase)).
            ToList()

        Dim closedRows = DataService.GetClosedEyeRows().
            Where(Function(r) String.Equals(DataService.GetValue(r, "ProductionTicketId"), ticketId, StringComparison.OrdinalIgnoreCase)).
            ToList()

        Dim usedFallback As Boolean = False

        If measurementRows.Count = 0 AndAlso closedRows.Count = 0 Then
            usedFallback = True
            measurementRows = GetFallbackMeasurementRows()
            closedRows = GetFallbackClosedEyeRows()
        End If

        Dim dt As New DataTable()
        For Each col In {"RecordId", "RecordType", "RecordDate", "TrCode", "DrawingRev", "LotNo", "SerialNo", "EyeNo", "EyeCount", "RowCount", "NokCount", "VisualStatus"}
            dt.Columns.Add(col)
        Next

        For Each grp In measurementRows.GroupBy(Function(r) DataService.GetValue(r, "RecordId")).OrderByDescending(Function(g) DataService.GetValue(g.First(), "MeasurementDate"))
            Dim firstRow = grp.First()
            Dim recordId = DataService.GetValue(firstRow, "RecordId")
            Dim dr = dt.NewRow()
            dr("RecordId") = recordId
            dr("RecordType") = "ÖLÇÜM"
            dr("RecordDate") = DataService.GetValue(firstRow, "MeasurementDate")
            dr("TrCode") = DataService.GetValue(firstRow, "TrCode")
            dr("DrawingRev") = DataService.GetValue(firstRow, "DrawingRev")
            dr("LotNo") = DataService.GetValue(firstRow, "LotNo")
            dr("SerialNo") = DataService.GetValue(firstRow, "SerialNo")
            dr("EyeNo") = DataService.GetValue(firstRow, "EyeNo")
            dr("EyeCount") = DataService.GetValue(firstRow, "EyeCount")
            dr("RowCount") = grp.Count().ToString()
            dr("NokCount") = grp.Where(Function(r) String.Equals(DataService.GetValue(r, "Result"), "NOK", StringComparison.OrdinalIgnoreCase)).Count().ToString()
            dr("VisualStatus") = GetVisualStatus(recordId)
            dt.Rows.Add(dr)
        Next

        For Each r In closedRows.OrderByDescending(Function(x) DataService.GetValue(x, "ClosedDate"))
            Dim recordId = DataService.GetValue(r, "RecordId")
            Dim dr = dt.NewRow()
            dr("RecordId") = recordId
            dr("RecordType") = "KAPALI GÖZ"
            dr("RecordDate") = DataService.GetValue(r, "ClosedDate")
            dr("TrCode") = DataService.GetValue(r, "TrCode")
            dr("DrawingRev") = DataService.GetValue(r, "DrawingRev")
            dr("LotNo") = DataService.GetValue(r, "LotNo")
            dr("SerialNo") = DataService.GetValue(r, "SerialNo")
            dr("EyeNo") = DataService.GetValue(r, "EyeNo")
            dr("EyeCount") = DataService.GetValue(r, "EyeCount")
            dr("RowCount") = "0"
            dr("NokCount") = "0"
            dr("VisualStatus") = "Göz Kapalı"
            dt.Rows.Add(dr)
        Next

        grid.DataSource = dt

        lblInfo.Text = $"Ticket No: {ticketId}    TR / Revizyon: {trCode} / {drawingRev}    Ölçüm kayıt sayısı: {dt.Rows.Count}"
        If usedFallback Then
            lblInfo.Text &= "    |    Not: Eski kayıtta ticket bağlantısı bulunamadı, TR ve tarih aralığına göre listelendi."
        End If

        If dt.Rows.Count = 0 Then
            MessageBox.Show("Bu ticket ile bağlantılı ölçüm kaydı bulunamadı.", "Ticket ölçümleri", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If
    End Sub

    Private Function GetFallbackMeasurementRows() As List(Of Dictionary(Of String, String))
        Dim startDate As DateTime
        Dim endDate As DateTime
        Dim hasStart = TryParseDate(createdAtText, startDate)
        Dim hasEnd = TryParseDate(closedAtText, endDate)

        Return DataService.GetMeasurementRows().
            Where(Function(r)
                      If trCode <> "" AndAlso Not String.Equals(DataService.GetValue(r, "TrCode"), trCode, StringComparison.OrdinalIgnoreCase) Then Return False
                      If drawingRev <> "" AndAlso Not String.Equals(DataService.GetValue(r, "DrawingRev"), drawingRev, StringComparison.OrdinalIgnoreCase) Then Return False
                      Dim d As DateTime
                      If TryParseDate(DataService.GetValue(r, "MeasurementDate"), d) Then
                          If hasStart AndAlso d < startDate.AddMinutes(-1) Then Return False
                          If hasEnd AndAlso d > endDate.AddMinutes(1) Then Return False
                      End If
                      Return True
                  End Function).
            ToList()
    End Function

    Private Function GetFallbackClosedEyeRows() As List(Of Dictionary(Of String, String))
        Dim startDate As DateTime
        Dim endDate As DateTime
        Dim hasStart = TryParseDate(createdAtText, startDate)
        Dim hasEnd = TryParseDate(closedAtText, endDate)

        Return DataService.GetClosedEyeRows().
            Where(Function(r)
                      If trCode <> "" AndAlso Not String.Equals(DataService.GetValue(r, "TrCode"), trCode, StringComparison.OrdinalIgnoreCase) Then Return False
                      If drawingRev <> "" AndAlso Not String.Equals(DataService.GetValue(r, "DrawingRev"), drawingRev, StringComparison.OrdinalIgnoreCase) Then Return False
                      Dim d As DateTime
                      If TryParseDate(DataService.GetValue(r, "ClosedDate"), d) Then
                          If hasStart AndAlso d < startDate.AddMinutes(-1) Then Return False
                          If hasEnd AndAlso d > endDate.AddMinutes(1) Then Return False
                      End If
                      Return True
                  End Function).
            ToList()
    End Function

    Private Function TryParseDate(text As String, ByRef value As DateTime) As Boolean
        Return DateTime.TryParseExact(text,
                                      "yyyy-MM-dd HH:mm:ss",
                                      CultureInfo.InvariantCulture,
                                      DateTimeStyles.None,
                                      value) OrElse DateTime.TryParse(text, value)
    End Function

    Private Function GetVisualStatus(recordId As String) As String
        Dim rows = DataService.GetVisualControlRows().
            Where(Function(r) String.Equals(DataService.GetValue(r, "RecordId"), recordId, StringComparison.OrdinalIgnoreCase)).
            ToList()

        If rows.Count = 0 Then Return "Yok"

        Dim nok = rows.Where(Function(r)
                                 Dim resultText = DataService.GetValue(r, "Result")
                                 Return resultText.IndexOf("DEĞİL", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                                        resultText.IndexOf("UYGUNSUZ", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                                        resultText.IndexOf("NOK", StringComparison.OrdinalIgnoreCase) >= 0
                             End Function).Count()

        If nok > 0 Then Return "Uygun Değil: " & nok.ToString()
        Return "Uygun"
    End Function

    Private Function SelectedRecordId() As String
        If grid.CurrentRow Is Nothing OrElse Not grid.Columns.Contains("RecordId") Then Return ""
        Return Convert.ToString(grid.CurrentRow.Cells("RecordId").Value)
    End Function

    Private Sub Grid_DoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return

        Dim recordId = SelectedRecordId()
        If recordId = "" Then Return

        Using f As New FrmMeasurementReview(recordId, "")
            f.ShowDialog(Me)
        End Using
    End Sub

    Private Sub Grid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse Not grid.Columns.Contains("NokCount") Then Return

        Dim nokText = Convert.ToString(grid.Rows(e.RowIndex).Cells("NokCount").Value)
        Dim nok As Integer = 0
        Integer.TryParse(nokText, nok)

        If nok > 0 Then
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.MistyRose
            grid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.DarkRed
        Else
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.Honeydew
            grid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.DarkGreen
        End If
    End Sub
End Class
