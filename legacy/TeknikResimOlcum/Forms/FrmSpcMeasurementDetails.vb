Imports System.Data
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmSpcMeasurementDetails
    Inherits Form

    Private ReadOnly sourceRows As List(Of Dictionary(Of String, String))
    Private ReadOnly seriesTitle As String
    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly cboEye As New ComboBox()
    Private ReadOnly cboResult As New ComboBox()
    Private ReadOnly cboOperator As New ComboBox()
    Private ReadOnly dtFrom As New DateTimePicker()
    Private ReadOnly dtTo As New DateTimePicker()
    Private ReadOnly lblSummary As New Label()
    Private ReadOnly grid As New DataGridView()

    Public Sub New(titleText As String, rows As List(Of Dictionary(Of String, String)))
        AppIconService.Apply(Me)
        seriesTitle = If(titleText, "").Trim()
        sourceRows = If(rows, New List(Of Dictionary(Of String, String))()).ToList()

        Text = "SPC Ölçüm Detayları"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1480, 820)
        MinimumSize = New Size(900, 560)
        BackColor = Color.White

        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .Padding = New Padding(10),
            .BackColor = Color.White
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 46.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 74.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(root)

        root.Controls.Add(BuildTitlePanel(), 0, 0)
        root.Controls.Add(BuildFilterPanel(), 0, 1)
        root.Controls.Add(BuildSummaryPanel(), 0, 2)
        ConfigureGrid()
        root.Controls.Add(grid, 0, 3)

        AddHandler Shown, Sub()
                              FillFilters()
                              RefreshGrid()
                          End Sub
    End Sub

    Private Function BuildTitlePanel() As Control
        Dim panel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .BackColor = Color.FromArgb(31, 78, 121)
        }
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 110.0F))

        panel.Controls.Add(New Label() With {
            .Text = seriesTitle,
            .Dock = DockStyle.Fill,
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 10.5F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(14, 0, 8, 0),
            .AutoEllipsis = True
        }, 0, 0)

        Dim btnClose As New Button() With {
            .Text = "Kapat",
            .Dock = DockStyle.Fill,
            .Margin = New Padding(8, 7, 8, 7),
            .BackColor = Color.White,
            .ForeColor = Color.FromArgb(31, 78, 121),
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand
        }
        AddHandler btnClose.Click, Sub() Close()
        panel.Controls.Add(btnClose, 1, 0)
        Return panel
    End Function

    Private Function BuildFilterPanel() As Control
        Dim panel As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = True,
            .AutoScroll = False,
            .BackColor = Color.WhiteSmoke,
            .Padding = New Padding(10, 8, 10, 4)
        }

        txtSearch.PlaceholderText = "Kayıt no / iş emri / seri / not ara"
        AddHandler txtSearch.TextChanged, Sub() RefreshGrid()
        panel.Controls.Add(BuildField("Arama", txtSearch, 275))

        cboEye.DropDownStyle = ComboBoxStyle.DropDownList
        cboResult.DropDownStyle = ComboBoxStyle.DropDownList
        cboOperator.DropDownStyle = ComboBoxStyle.DropDownList
        AddHandler cboEye.SelectedIndexChanged, Sub() RefreshGrid()
        AddHandler cboResult.SelectedIndexChanged, Sub() RefreshGrid()
        AddHandler cboOperator.SelectedIndexChanged, Sub() RefreshGrid()
        panel.Controls.Add(BuildField("Göz", cboEye, 90))
        panel.Controls.Add(BuildField("Sonuç", cboResult, 105))
        panel.Controls.Add(BuildField("Ölçen", cboOperator, 170))

        ConfigureDatePicker(dtFrom)
        ConfigureDatePicker(dtTo)
        AddHandler dtFrom.ValueChanged, Sub() RefreshGrid()
        AddHandler dtTo.ValueChanged, Sub() RefreshGrid()
        panel.Controls.Add(BuildField("Başlangıç", dtFrom, 125))
        panel.Controls.Add(BuildField("Bitiş", dtTo, 125))

        Dim btnClear As New Button() With {
            .Text = "Filtreleri Temizle",
            .Width = 135,
            .Height = 32,
            .Margin = New Padding(8, 21, 4, 4),
            .Cursor = Cursors.Hand
        }
        AddHandler btnClear.Click,
            Sub()
                txtSearch.Clear()
                If cboEye.Items.Count > 0 Then cboEye.SelectedIndex = 0
                If cboResult.Items.Count > 0 Then cboResult.SelectedIndex = 0
                If cboOperator.Items.Count > 0 Then cboOperator.SelectedIndex = 0
                dtFrom.Checked = False
                dtTo.Checked = False
                RefreshGrid()
            End Sub
        panel.Controls.Add(btnClear)
        Return panel
    End Function

    Private Shared Function BuildField(caption As String, control As Control, width As Integer) As Control
        Dim panel As New Panel() With {.Width = width, .Height = 48, .Margin = New Padding(0, 0, 10, 6)}
        panel.Controls.Add(New Label() With {
            .Text = caption,
            .Left = 0,
            .Top = 0,
            .Width = width,
            .Height = 17,
            .Font = New Font("Segoe UI", 8.3F, FontStyle.Bold)
        })
        control.SetBounds(0, 20, width, 25)
        panel.Controls.Add(control)
        Return panel
    End Function

    Private Shared Sub ConfigureDatePicker(picker As DateTimePicker)
        picker.Format = DateTimePickerFormat.Custom
        picker.CustomFormat = "dd.MM.yyyy"
        picker.ShowCheckBox = True
        picker.Checked = False
    End Sub

    Private Function BuildSummaryPanel() As Control
        lblSummary.Dock = DockStyle.Fill
        lblSummary.TextAlign = ContentAlignment.MiddleLeft
        lblSummary.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblSummary.ForeColor = Color.FromArgb(42, 70, 105)
        lblSummary.BackColor = Color.FromArgb(239, 246, 255)
        lblSummary.Padding = New Padding(12, 0, 12, 0)
        Return lblSummary
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
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(228, 236, 247)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.BackgroundColor = Color.White
        grid.GridColor = Color.Gainsboro
        grid.RowTemplate.Height = 28

        AddColumn("MeasurementDate", "Tarih / Saat", 145)
        AddColumn("EyeNo", "Göz", 55)
        AddColumn("MeasuredValue", "Değer", 90)
        AddColumn("Result", "Sonuç", 85)
        AddColumn("Limits", "Limit", 135)
        AddColumn("MeasureId", "Ölçü No", 115)
        AddColumn("MeasureVersion", "V.", 45)
        AddColumn("OperatorName", "Ölçen", 130)
        AddColumn("LotNo", "İş Emri", 115)
        AddColumn("SerialNo", "Seri", 110)
        AddColumn("RecordId", "Kayıt No", 180)
        AddColumn("Note", "Not", 250)

        AddHandler grid.CellFormatting, AddressOf Grid_CellFormatting
        AddHandler grid.CellDoubleClick, AddressOf Grid_CellDoubleClick
        If AppState.IsAdmin Then grid.Cursor = Cursors.Hand
    End Sub

    Private Sub AddColumn(name As String, header As String, width As Integer)
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {
            .Name = name,
            .DataPropertyName = name,
            .HeaderText = header,
            .Width = width,
            .MinimumWidth = 40,
            .SortMode = DataGridViewColumnSortMode.Automatic
        })
    End Sub

    Private Sub FillFilters()
        cboEye.Items.Clear()
        cboEye.Items.Add("TÜMÜ")
        For Each itemText In sourceRows.Select(Function(sourceRow) DataService.GetValue(sourceRow, "EyeNo").Trim()).Where(Function(textValue) textValue <> "").Distinct().OrderBy(Function(textValue) ParseIntSafe(textValue))
            cboEye.Items.Add(itemText)
        Next
        cboEye.SelectedIndex = 0

        cboResult.Items.Clear()
        cboResult.Items.Add("TÜMÜ")
        For Each itemText In sourceRows.Select(Function(sourceRow) DataService.GetValue(sourceRow, "Result").Trim()).Where(Function(textValue) textValue <> "").Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(Function(textValue) textValue)
            cboResult.Items.Add(itemText)
        Next
        cboResult.SelectedIndex = 0

        cboOperator.Items.Clear()
        cboOperator.Items.Add("TÜMÜ")
        For Each itemText In sourceRows.Select(Function(sourceRow) DataService.GetValue(sourceRow, "OperatorName").Trim()).Where(Function(textValue) textValue <> "").Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(Function(textValue) textValue)
            cboOperator.Items.Add(itemText)
        Next
        cboOperator.SelectedIndex = 0
    End Sub

    Private Sub RefreshGrid()
        If cboEye.Items.Count = 0 OrElse cboResult.Items.Count = 0 OrElse cboOperator.Items.Count = 0 Then Return

        Dim searchText = txtSearch.Text.Trim().ToUpperInvariant()
        Dim eyeFilter = Convert.ToString(cboEye.SelectedItem)
        Dim resultFilter = Convert.ToString(cboResult.SelectedItem)
        Dim operatorFilter = Convert.ToString(cboOperator.SelectedItem)

        Dim filtered = sourceRows.Where(
            Function(row)
                Dim measuredDate = ParseDateSafe(DataService.GetValue(row, "MeasurementDate"))
                If dtFrom.Checked AndAlso (measuredDate = DateTime.MinValue OrElse measuredDate.Date < dtFrom.Value.Date) Then Return False
                If dtTo.Checked AndAlso (measuredDate = DateTime.MinValue OrElse measuredDate.Date > dtTo.Value.Date) Then Return False
                If eyeFilter <> "TÜMÜ" AndAlso Not String.Equals(DataService.GetValue(row, "EyeNo"), eyeFilter, StringComparison.OrdinalIgnoreCase) Then Return False
                If resultFilter <> "TÜMÜ" AndAlso Not String.Equals(DataService.GetValue(row, "Result"), resultFilter, StringComparison.OrdinalIgnoreCase) Then Return False
                If operatorFilter <> "TÜMÜ" AndAlso Not String.Equals(DataService.GetValue(row, "OperatorName"), operatorFilter, StringComparison.OrdinalIgnoreCase) Then Return False
                If searchText <> "" Then
                    Dim haystack = String.Join(" ", {
                        DataService.GetValue(row, "RecordId"),
                        DataService.GetValue(row, "LotNo"),
                        DataService.GetValue(row, "SerialNo"),
                        DataService.GetValue(row, "MeasureId"),
                        DataService.GetValue(row, "Note"),
                        DataService.GetValue(row, "OperatorName")}).ToUpperInvariant()
                    If Not haystack.Contains(searchText) Then Return False
                End If
                Return True
            End Function).
            OrderByDescending(Function(row) ParseDateSafe(DataService.GetValue(row, "MeasurementDate"))).
            ThenBy(Function(row) ParseIntSafe(DataService.GetValue(row, "EyeNo"))).
            ToList()

        Dim table As New DataTable()
        For Each columnName As String In {"MeasurementDate", "EyeNo", "MeasuredValue", "Result", "Limits", "MeasureId", "MeasureVersion", "OperatorName", "LotNo", "SerialNo", "RecordId", "Note"}
            table.Columns.Add(columnName)
        Next

        For Each itemRow In filtered
            Dim measuredDate = ParseDateSafe(DataService.GetValue(itemRow, "MeasurementDate"))
            table.Rows.Add(
                If(measuredDate = DateTime.MinValue, DataService.GetValue(itemRow, "MeasurementDate"), measuredDate.ToString("dd.MM.yyyy HH:mm")),
                DataService.GetValue(itemRow, "EyeNo"),
                DataService.GetValue(itemRow, "MeasuredValue"),
                DataService.GetValue(itemRow, "Result"),
                DataService.GetValue(itemRow, "LowerLimit") & " - " & DataService.GetValue(itemRow, "UpperLimit"),
                DataService.GetValue(itemRow, "MeasureId"),
                If(String.IsNullOrWhiteSpace(DataService.GetValue(itemRow, "MeasureVersion")), "1", DataService.GetValue(itemRow, "MeasureVersion")),
                DataService.GetValue(itemRow, "OperatorName"),
                DataService.GetValue(itemRow, "LotNo"),
                DataService.GetValue(itemRow, "SerialNo"),
                DataService.GetValue(itemRow, "RecordId"),
                DataService.GetValue(itemRow, "Note"))
        Next

        grid.DataSource = table
        Dim okCount = filtered.Where(Function(item) String.Equals(DataService.GetValue(item, "Result"), "OK", StringComparison.OrdinalIgnoreCase)).Count()
        Dim nokCount = filtered.Where(Function(item) String.Equals(DataService.GetValue(item, "Result"), "NOK", StringComparison.OrdinalIgnoreCase)).Count()
        Dim invalidCount = filtered.Where(Function(item) String.Equals(DataService.GetValue(item, "Result"), "HATALI", StringComparison.OrdinalIgnoreCase)).Count()
        lblSummary.Text = "Gösterilen: " & filtered.Count.ToString() & " / " & sourceRows.Count.ToString() &
                          "   |   OK: " & okCount.ToString() &
                          "   |   NOK: " & nokCount.ToString() &
                          "   |   Hatalı: " & invalidCount.ToString() &
                          If(AppState.IsAdmin, "   |   Değeri düzeltmek için satıra çift tıklayın.", "")
    End Sub

    Private Sub Grid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse e.RowIndex >= grid.Rows.Count Then Return
        Dim resultText = Convert.ToString(grid.Rows(e.RowIndex).Cells("Result").Value)
        If String.Equals(resultText, "NOK", StringComparison.OrdinalIgnoreCase) Then
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.MistyRose
            grid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.DarkRed
        ElseIf String.Equals(resultText, "HATALI", StringComparison.OrdinalIgnoreCase) Then
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.LemonChiffon
            grid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.DarkGoldenrod
        ElseIf String.Equals(resultText, "OK", StringComparison.OrdinalIgnoreCase) Then
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.Honeydew
            grid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.DarkGreen
        End If
    End Sub

    Private Sub Grid_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 OrElse e.RowIndex >= grid.Rows.Count OrElse Not AppState.IsAdmin Then Return

        Try
            Dim selectedGridRow = grid.Rows(e.RowIndex)
            Dim recordId = Convert.ToString(selectedGridRow.Cells("RecordId").Value).Trim()
            Dim eyeNo = Convert.ToString(selectedGridRow.Cells("EyeNo").Value).Trim()
            Dim measureId = Convert.ToString(selectedGridRow.Cells("MeasureId").Value).Trim()
            Dim displayedDate = Convert.ToString(selectedGridRow.Cells("MeasurementDate").Value).Trim()

            Dim candidates = sourceRows.Where(
                Function(row)
                    If Not String.Equals(DataService.GetValue(row, "RecordId").Trim(), recordId, StringComparison.OrdinalIgnoreCase) Then Return False
                    If Not String.Equals(DataService.GetValue(row, "EyeNo").Trim(), eyeNo, StringComparison.OrdinalIgnoreCase) Then Return False
                    If Not String.Equals(DataService.GetValue(row, "MeasureId").Trim(), measureId, StringComparison.OrdinalIgnoreCase) Then Return False

                    Dim sourceDate = ParseDateSafe(DataService.GetValue(row, "MeasurementDate"))
                    If sourceDate <> DateTime.MinValue AndAlso sourceDate.ToString("dd.MM.yyyy HH:mm") <> displayedDate Then Return False
                    Return True
                End Function).
                ToList()

            If candidates.Count <> 1 Then
                MessageBox.Show("Düzeltilecek ölçüm satırı güvenli biçimde belirlenemedi.", "Ölçüm düzeltme", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            If MeasurementCorrectionUiService.EditMeasurement(Me, candidates(0)) Then RefreshGrid()
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Ölçüm değeri düzeltilemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Shared Function ParseDateSafe(value As String) As DateTime
        Dim parsed As DateTime
        If DateTime.TryParse(If(value, "").Trim(), parsed) Then Return parsed
        Return DateTime.MinValue
    End Function

    Private Shared Function ParseIntSafe(value As String) As Integer
        Dim parsed As Integer
        If Integer.TryParse(If(value, "").Trim(), parsed) Then Return parsed
        Return Integer.MaxValue
    End Function
End Class
