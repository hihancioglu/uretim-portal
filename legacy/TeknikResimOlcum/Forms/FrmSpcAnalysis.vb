Imports System.Data
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Globalization
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmSpcAnalysis
    Inherits Form

    Private Const MinimumSpcInterpretationCount As Integer = 25

    Private NotInheritable Class MeasurementPoint
        Public Property SourceRow As Dictionary(Of String, String)
        Public Property DateValue As DateTime
        Public Property EyeNo As Integer
        Public Property Value As Double
        Public Property Result As String = ""
        Public Property LowerLimit As Double?
        Public Property UpperLimit As Double?
        Public Property Nominal As Double?
        Public Property RecordId As String = ""
    End Class

    Private NotInheritable Class Subgroup
        Public Property Key As String = ""
        Public Property DateValue As DateTime
        Public Property Points As New List(Of MeasurementPoint)()
        Public ReadOnly Property Mean As Double
            Get
                Return If(Points.Count = 0, 0.0R, Points.Average(Function(item) item.Value))
            End Get
        End Property
        Public ReadOnly Property Range As Double
            Get
                Return If(Points.Count = 0, 0.0R, Points.Max(Function(item) item.Value) - Points.Min(Function(item) item.Value))
            End Get
        End Property
    End Class

    Private NotInheritable Class ChartDatum
        Public Property DateValue As DateTime
        Public Property Caption As String = ""
        Public Property Value As Double
        Public Property IsViolation As Boolean
    End Class

    Private NotInheritable Class ViolationRecord
        Public Property ChartType As String = ""
        Public Property RuleName As String = ""
        Public Property DateValue As DateTime
        Public Property EyeText As String = ""
        Public Property Value As Double
        Public Property Detail As String = ""
        Public Property RecordId As String = ""
        Public Property Result As String = ""
    End Class

    Private NotInheritable Class ControlChartPanel
        Inherits Panel

        Private chartTitle As String = ""
        Private data As New List(Of ChartDatum)()
        Private center As Double?
        Private upperControl As Double?
        Private lowerControl As Double?
        Private upperSpec As Double?
        Private lowerSpec As Double?

        Public Sub New()
            DoubleBuffered = True
            BackColor = Color.White
            BorderStyle = BorderStyle.FixedSingle
        End Sub

        Public Sub SetData(titleText As String,
                           items As List(Of ChartDatum),
                           centerValue As Double?,
                           ucl As Double?,
                           lcl As Double?,
                           usl As Double?,
                           lsl As Double?)
            chartTitle = If(titleText, "")
            data = If(items, New List(Of ChartDatum)())
            center = centerValue
            upperControl = ucl
            lowerControl = lcl
            upperSpec = usl
            lowerSpec = lsl
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
            e.Graphics.Clear(Color.White)

            TextRenderer.DrawText(e.Graphics,
                                  chartTitle,
                                  New Font("Segoe UI", 9.5F, FontStyle.Bold),
                                  New Rectangle(12, 5, Math.Max(10, ClientSize.Width - 24), 24),
                                  Color.FromArgb(31, 78, 121),
                                  TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)

            Dim plot = New Rectangle(68, 35, Math.Max(40, ClientSize.Width - 94), Math.Max(40, ClientSize.Height - 78))
            If data.Count = 0 Then
                TextRenderer.DrawText(e.Graphics,
                                      "Bu grafik için yeterli veri bulunamadı.",
                                      New Font("Segoe UI", 10.0F, FontStyle.Bold),
                                      New Rectangle(0, 28, ClientSize.Width, Math.Max(20, ClientSize.Height - 28)),
                                      Color.FromArgb(100, 110, 125),
                                      TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
                Return
            End If

            Dim yValues As New List(Of Double)(data.Select(Function(item) item.Value))
            For Each optionalValue In {center, upperControl, lowerControl, upperSpec, lowerSpec}
                If optionalValue.HasValue Then yValues.Add(optionalValue.Value)
            Next
            Dim minY = yValues.Min()
            Dim maxY = yValues.Max()
            If Math.Abs(maxY - minY) < 0.000001R Then
                minY -= 1.0R
                maxY += 1.0R
            Else
                Dim pad = (maxY - minY) * 0.12R
                minY -= pad
                maxY += pad
            End If

            Using axisPen As New Pen(Color.FromArgb(175, 185, 200)),
                  gridPen As New Pen(Color.FromArgb(235, 238, 244))
                e.Graphics.DrawRectangle(axisPen, plot)
                For index As Integer = 0 To 4
                    Dim y = CSng(plot.Bottom - plot.Height * index / 4.0F)
                    e.Graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y)
                    Dim labelValue = minY + (maxY - minY) * index / 4.0R
                    TextRenderer.DrawText(e.Graphics,
                                          labelValue.ToString("0.###", CultureInfo.GetCultureInfo("tr-TR")),
                                          New Font("Segoe UI", 7.5F),
                                          New Rectangle(2, CInt(y - 9), 62, 18),
                                          Color.FromArgb(90, 100, 115),
                                          TextFormatFlags.Right Or TextFormatFlags.VerticalCenter)
                Next

                DrawLine(e.Graphics, plot, minY, maxY, upperSpec, "USL", Color.Firebrick, DashStyle.Dot)
                DrawLine(e.Graphics, plot, minY, maxY, lowerSpec, "LSL", Color.Firebrick, DashStyle.Dot)
                DrawLine(e.Graphics, plot, minY, maxY, upperControl, "UCL", Color.DarkOrange, DashStyle.Dash)
                DrawLine(e.Graphics, plot, minY, maxY, lowerControl, "LCL", Color.DarkOrange, DashStyle.Dash)
                DrawLine(e.Graphics, plot, minY, maxY, center, "CL", Color.FromArgb(31, 78, 121), DashStyle.Dash)

                Dim screenPoints As New List(Of PointF)()
                For index As Integer = 0 To data.Count - 1
                    Dim x = If(data.Count = 1,
                               plot.Left + plot.Width / 2.0F,
                               CSng(plot.Left + plot.Width * index / CDbl(data.Count - 1)))
                    Dim ratio = (data(index).Value - minY) / (maxY - minY)
                    Dim y = CSng(plot.Bottom - plot.Height * ratio)
                    screenPoints.Add(New PointF(x, y))
                Next

                If screenPoints.Count > 1 Then
                    Using trendPen As New Pen(Color.FromArgb(52, 101, 170), 1.8F)
                        e.Graphics.DrawLines(trendPen, screenPoints.ToArray())
                    End Using
                End If

                For index As Integer = 0 To screenPoints.Count - 1
                    Dim fillColor = If(data(index).IsViolation, Color.Firebrick, Color.SeaGreen)
                    Dim radius = If(data(index).IsViolation, 5.5F, 4.0F)
                    Using brush As New SolidBrush(fillColor), borderPen As New Pen(Color.White, 1.2F)
                        Dim bounds = New RectangleF(screenPoints(index).X - radius, screenPoints(index).Y - radius, radius * 2.0F, radius * 2.0F)
                        e.Graphics.FillEllipse(brush, bounds)
                        e.Graphics.DrawEllipse(borderPen, bounds)
                    End Using
                Next

                Dim firstText = data.First().DateValue.ToString("dd.MM.yyyy HH:mm")
                Dim lastText = data.Last().DateValue.ToString("dd.MM.yyyy HH:mm")
                TextRenderer.DrawText(e.Graphics, firstText, New Font("Segoe UI", 7.3F), New Point(plot.Left, plot.Bottom + 8), Color.DimGray)
                Dim lastSize = TextRenderer.MeasureText(lastText, New Font("Segoe UI", 7.3F))
                TextRenderer.DrawText(e.Graphics, lastText, New Font("Segoe UI", 7.3F), New Point(plot.Right - lastSize.Width, plot.Bottom + 8), Color.DimGray)
            End Using
        End Sub

        Private Shared Sub DrawLine(g As Graphics,
                                    plot As Rectangle,
                                    minY As Double,
                                    maxY As Double,
                                    value As Double?,
                                    caption As String,
                                    color As Color,
                                    style As DashStyle)
            If Not value.HasValue Then Return
            Dim ratio = (value.Value - minY) / (maxY - minY)
            Dim y = CSng(plot.Bottom - plot.Height * ratio)
            Using pen As New Pen(color, 1.3F)
                pen.DashStyle = style
                g.DrawLine(pen, plot.Left, y, plot.Right, y)
            End Using
            Dim numericText = value.Value.ToString("0.###", CultureInfo.GetCultureInfo("tr-TR"))
            Dim lineLabel = caption & ": " & numericText
            TextRenderer.DrawText(g, lineLabel, New Font("Segoe UI", 7.0F, FontStyle.Bold), New Point(plot.Left + 4, CInt(y - 16)), color)
        End Sub
    End Class

    Private ReadOnly sourceRows As List(Of Dictionary(Of String, String))
    Private ReadOnly seriesTitle As String
    Private ReadOnly points As New List(Of MeasurementPoint)()
    Private ReadOnly cboChartType As New ComboBox()
    Private ReadOnly lblChartInfo As New Label()
    Private ReadOnly chartTop As New ControlChartPanel()
    Private ReadOnly chartBottom As New ControlChartPanel()
    Private ReadOnly violationGrid As New DataGridView()
    Private ReadOnly heatGrid As New DataGridView()
    Private allViolations As New List(Of ViolationRecord)()

    Public Sub New(titleText As String, rows As List(Of Dictionary(Of String, String)))
        AppIconService.Apply(Me)
        seriesTitle = If(titleText, "").Trim()
        sourceRows = If(rows, New List(Of Dictionary(Of String, String))()).ToList()

        Text = "SPC Analizi"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1500, 860)
        MinimumSize = New Size(980, 640)
        BackColor = Color.White

        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .Padding = New Padding(10),
            .BackColor = Color.White
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 46.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(root)

        root.Controls.Add(BuildTitlePanel(), 0, 0)
        root.Controls.Add(BuildTabs(), 0, 1)
        AddHandler Shown, AddressOf FormShown
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
            .FlatStyle = FlatStyle.Flat
        }
        AddHandler btnClose.Click, Sub() Close()
        panel.Controls.Add(btnClose, 1, 0)
        Return panel
    End Function

    Private Function BuildTabs() As Control
        Dim tabs As New TabControl() With {.Dock = DockStyle.Fill, .Font = New Font("Segoe UI", 9.0F)}
        Dim chartTab As New TabPage("Kontrol Grafikleri") With {.BackColor = Color.White}
        Dim violationTab As New TabPage("Kural İhlalleri") With {.BackColor = Color.White}
        Dim heatTab As New TabPage("Göz Isı Haritası") With {.BackColor = Color.White}
        chartTab.Controls.Add(BuildChartTab())
        violationTab.Controls.Add(BuildViolationTab())
        heatTab.Controls.Add(BuildHeatTab())
        tabs.TabPages.Add(chartTab)
        tabs.TabPages.Add(violationTab)
        tabs.TabPages.Add(heatTab)
        Return tabs
    End Function

    Private Function BuildChartTab() As Control
        Dim root As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3, .Padding = New Padding(6)}
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 50.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))

        Dim toolbar As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .BackColor = Color.WhiteSmoke, .Padding = New Padding(8, 7, 8, 7)}
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 270.0F))
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        cboChartType.Dock = DockStyle.Fill
        cboChartType.DropDownStyle = ComboBoxStyle.DropDownList
        cboChartType.Items.Add("I-MR (Tekil Ölçümler)")
        cboChartType.Items.Add("X̄-R (Kayıt / göz alt grupları)")
        AddHandler cboChartType.SelectedIndexChanged, Sub() RefreshCharts()
        toolbar.Controls.Add(cboChartType, 0, 0)
        lblChartInfo.Dock = DockStyle.Fill
        lblChartInfo.TextAlign = ContentAlignment.MiddleLeft
        lblChartInfo.Padding = New Padding(12, 0, 0, 0)
        lblChartInfo.Font = New Font("Segoe UI", 8.8F, FontStyle.Bold)
        lblChartInfo.ForeColor = Color.FromArgb(60, 75, 95)
        toolbar.Controls.Add(lblChartInfo, 1, 0)
        root.Controls.Add(toolbar, 0, 0)
        chartTop.Dock = DockStyle.Fill
        chartTop.Margin = New Padding(0, 6, 0, 3)
        chartBottom.Dock = DockStyle.Fill
        chartBottom.Margin = New Padding(0, 3, 0, 0)
        root.Controls.Add(chartTop, 0, 1)
        root.Controls.Add(chartBottom, 0, 2)
        Return root
    End Function

    Private Function BuildViolationTab() As Control
        ConfigureViolationGrid()
        Dim root As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .Padding = New Padding(6)}
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.Controls.Add(New Label() With {
            .Text = If(AppState.IsAdmin,
                       "İhlalin kaynak ölçümüne gitmek ve gerekirse değeri düzeltmek için satıra çift tıklayın.",
                       "İhlalin kaynak ölçümünü görüntülemek için satıra çift tıklayın."),
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(255, 244, 230),
            .ForeColor = Color.FromArgb(120, 60, 0),
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(12, 0, 12, 0)
        }, 0, 0)
        root.Controls.Add(violationGrid, 0, 1)
        Return root
    End Function

    Private Function BuildHeatTab() As Control
        ConfigureHeatGrid()
        Dim root As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .Padding = New Padding(6)}
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.Controls.Add(New Label() With {
            .Text = "Gözler; NOK, proses yeterliliği ve son ölçüm durumuna göre yeşil, sarı veya kırmızı renklendirilir.",
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(239, 246, 255),
            .ForeColor = Color.FromArgb(42, 70, 105),
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(12, 0, 12, 0)
        }, 0, 0)
        root.Controls.Add(heatGrid, 0, 1)
        Return root
    End Function

    Private Sub ConfigureViolationGrid()
        ConfigureBaseGrid(violationGrid)
        violationGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        AddColumn(violationGrid, "ChartType", "Grafik", 80, 60)
        AddColumn(violationGrid, "RuleName", "Kural", 170, 120)
        AddColumn(violationGrid, "DateText", "Tarih / Saat", 140, 90)
        AddColumn(violationGrid, "EyeText", "Göz", 55, 40)
        AddColumn(violationGrid, "ValueText", "Değer", 80, 55)
        AddColumn(violationGrid, "Result", "Sonuç", 75, 50)
        AddColumn(violationGrid, "Detail", "Açıklama", 360, 250)
        AddColumn(violationGrid, "RecordId", "Kayıt No", 170, 110)
        AddHandler violationGrid.CellFormatting, AddressOf ViolationGridCellFormatting
        AddHandler violationGrid.CellDoubleClick, AddressOf ViolationGridCellDoubleClick
        violationGrid.Cursor = Cursors.Hand
    End Sub

    Private Sub ConfigureHeatGrid()
        ConfigureBaseGrid(heatGrid)
        heatGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        heatGrid.RowTemplate.Height = 36
        AddColumn(heatGrid, "RiskLevel", "Durum", 85, 60)
        AddColumn(heatGrid, "EyeNo", "Göz", 55, 40)
        AddColumn(heatGrid, "RecordCount", "Kayıt", 65, 45)
        AddColumn(heatGrid, "OkCount", "OK", 55, 40)
        AddColumn(heatGrid, "NokCount", "NOK", 55, 40)
        AddColumn(heatGrid, "InvalidCount", "Hatalı", 60, 45)
        AddColumn(heatGrid, "Average", "Ortalama", 90, 65)
        AddColumn(heatGrid, "StdDev", "Std. Sapma", 90, 65)
        AddColumn(heatGrid, "MinMax", "Min / Max", 120, 85)
        AddColumn(heatGrid, "Cpk", "Cpk", 65, 45)
        AddColumn(heatGrid, "LastValue", "Son Değer", 85, 60)
        AddColumn(heatGrid, "LastResult", "Sonuç", 75, 55)
        AddHandler heatGrid.CellFormatting, AddressOf HeatGridCellFormatting
    End Sub

    Private Shared Sub ConfigureBaseGrid(target As DataGridView)
        target.Dock = DockStyle.Fill
        target.ReadOnly = True
        target.AllowUserToAddRows = False
        target.AllowUserToDeleteRows = False
        target.RowHeadersVisible = False
        target.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        target.MultiSelect = False
        target.AutoGenerateColumns = False
        target.EnableHeadersVisualStyles = False
        target.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(228, 236, 247)
        target.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        target.BackgroundColor = Color.White
        target.GridColor = Color.Gainsboro
        target.RowTemplate.Height = 28
    End Sub

    Private Shared Sub AddColumn(target As DataGridView, name As String, header As String, width As Integer, fillWeight As Single)
        target.Columns.Add(New DataGridViewTextBoxColumn() With {
            .Name = name,
            .DataPropertyName = name,
            .HeaderText = header,
            .Width = width,
            .MinimumWidth = 40,
            .FillWeight = fillWeight,
            .SortMode = DataGridViewColumnSortMode.Automatic
        })
    End Sub

    Private Sub FormShown(sender As Object, e As EventArgs)
        LoadPoints()
        BuildAllViolations()
        BindViolations()
        BindHeatMap()
        cboChartType.SelectedIndex = 0
    End Sub

    Private Sub LoadPoints()
        points.Clear()
        For Each itemRow In sourceRows
            Dim dateValue = ParseDateSafe(DataService.GetValue(itemRow, "MeasurementDate"))
            Dim measuredValue As Decimal = 0D
            If dateValue = DateTime.MinValue OrElse Not NumberUtil.TryParseDecimal(DataService.GetValue(itemRow, "MeasuredValue"), measuredValue) Then Continue For
            points.Add(New MeasurementPoint With {
                .SourceRow = itemRow,
                .DateValue = dateValue,
                .EyeNo = ParseIntSafe(DataService.GetValue(itemRow, "EyeNo")),
                .Value = CDbl(measuredValue),
                .Result = DataService.GetValue(itemRow, "Result").Trim(),
                .LowerLimit = ParseNullableDouble(DataService.GetValue(itemRow, "LowerLimit")),
                .UpperLimit = ParseNullableDouble(DataService.GetValue(itemRow, "UpperLimit")),
                .Nominal = ParseNullableDouble(DataService.GetValue(itemRow, "Nominal")),
                .RecordId = DataService.GetValue(itemRow, "RecordId").Trim()
            })
        Next
        points.Sort(Function(leftItem, rightItem)
                        Dim dateCompare = leftItem.DateValue.CompareTo(rightItem.DateValue)
                        If dateCompare <> 0 Then Return dateCompare
                        Return leftItem.EyeNo.CompareTo(rightItem.EyeNo)
                    End Function)
    End Sub

    Private Sub RefreshCharts()
        If cboChartType.SelectedIndex = 1 Then
            ShowXbarRCharts()
        Else
            ShowImrCharts()
        End If
        ApplyCurrentChartDataWarning()
    End Sub

    Private Sub ApplyCurrentChartDataWarning()
        Dim sampleCount As Integer
        Dim unitText As String

        If cboChartType.SelectedIndex = 1 Then
            sampleCount = BuildSubgroups().Count
            unitText = "alt grup"
        Else
            sampleCount = points.Count
            unitText = "ölçüm"
        End If

        Dim warningText = BuildDataSufficiencyWarning(sampleCount, unitText)
        If warningText = "" Then
            lblChartInfo.ForeColor = Color.FromArgb(60, 75, 95)
            lblChartInfo.BackColor = Color.White
            Return
        End If

        If lblChartInfo.Text.IndexOf("UYARI:", StringComparison.OrdinalIgnoreCase) < 0 Then
            lblChartInfo.Text &= "   |   UYARI: " & warningText
        End If
        lblChartInfo.ForeColor = Color.FromArgb(130, 82, 0)
        lblChartInfo.BackColor = Color.FromArgb(255, 248, 225)
    End Sub

    Private Shared Function BuildDataSufficiencyWarning(sampleCount As Integer, unitText As String) As String
        If sampleCount <= 0 OrElse sampleCount >= MinimumSpcInterpretationCount Then Return ""
        Return "Veri yetersiz: " & sampleCount.ToString() & "/" & MinimumSpcInterpretationCount.ToString() & " " & unitText & ". SPC yorumu ön değerlendirme niteliğindedir."
    End Function

    Private Sub ShowImrCharts()
        If points.Count < 2 Then
            chartTop.SetData("I Grafiği", New List(Of ChartDatum)(), Nothing, Nothing, Nothing, Nothing, Nothing)
            chartBottom.SetData("MR Grafiği", New List(Of ChartDatum)(), Nothing, Nothing, Nothing, Nothing, Nothing)
            lblChartInfo.Text = "I-MR için en az iki ölçüm gereklidir."
            Return
        End If

        Dim values = points.Select(Function(item) item.Value).ToList()
        Dim mean = values.Average()
        Dim movingRanges As New List(Of Double)()
        For index As Integer = 1 To values.Count - 1
            movingRanges.Add(Math.Abs(values(index) - values(index - 1)))
        Next
        Dim mrBar = movingRanges.Average()
        Dim sigma = mrBar / 1.128R
        Dim iUcl = mean + 3.0R * sigma
        Dim iLcl = mean - 3.0R * sigma
        Dim mrUcl = 3.267R * mrBar
        Dim mrLcl = 0.0R

        Dim violations = DetectRules(points.Select(Function(item) item.DateValue).ToList(), values, points.Select(Function(item) item.EyeNo.ToString()).ToList(), points.Select(Function(item) item.RecordId).ToList(), points.Select(Function(item) item.Result).ToList(), "I-MR / I", mean, iUcl, iLcl)
        Dim violationKeys = violations.Select(Function(item) item.RecordId & "|" & item.DateValue.Ticks.ToString() & "|" & item.EyeText).ToHashSet(StringComparer.OrdinalIgnoreCase)
        Dim iData = points.Select(Function(item) New ChartDatum With {
            .DateValue = item.DateValue,
            .Caption = item.EyeNo.ToString(),
            .Value = item.Value,
            .IsViolation = violationKeys.Contains(item.RecordId & "|" & item.DateValue.Ticks.ToString() & "|" & item.EyeNo.ToString())
        }).ToList()

        Dim mrData As New List(Of ChartDatum)()
        For index As Integer = 1 To points.Count - 1
            mrData.Add(New ChartDatum With {
                .DateValue = points(index).DateValue,
                .Caption = points(index).EyeNo.ToString(),
                .Value = movingRanges(index - 1),
                .IsViolation = movingRanges(index - 1) > mrUcl
            })
        Next

        Dim usl = LastNullable(points.Select(Function(item) item.UpperLimit))
        Dim lsl = LastNullable(points.Select(Function(item) item.LowerLimit))
        chartTop.SetData("I Grafiği — Tekil ölçümler | Kontrol sınırları turuncu, tolerans sınırları kırmızı", iData, mean, iUcl, iLcl, usl, lsl)
        chartBottom.SetData("MR Grafiği — Ardışık ölçümler arasındaki hareketli aralık", mrData, mrBar, mrUcl, mrLcl, Nothing, Nothing)
        lblChartInfo.Text = "Kayıt: " & points.Count.ToString() & "   |   Ortalama: " & FormatNumber(mean) & "   |   MR̄: " & FormatNumber(mrBar) & "   |   Tahmini σ: " & FormatNumber(sigma)
    End Sub

    Private Sub ShowXbarRCharts()
        Dim groups = BuildSubgroups()
        If groups.Count < 2 Then
            chartTop.SetData("X̄ Grafiği", New List(Of ChartDatum)(), Nothing, Nothing, Nothing, Nothing, Nothing)
            chartBottom.SetData("R Grafiği", New List(Of ChartDatum)(), Nothing, Nothing, Nothing, Nothing, Nothing)
            lblChartInfo.Text = "X̄-R için en az iki kayıt ve her kayıtta en az iki göz ölçümü gereklidir."
            Return
        End If

        Dim means = groups.Select(Function(item) item.Mean).ToList()
        Dim ranges = groups.Select(Function(item) item.Range).ToList()
        Dim xBarBar = means.Average()
        Dim rBar = ranges.Average()
        Dim averageN = Math.Max(2, Math.Min(10, CInt(Math.Round(groups.Average(Function(item) CDbl(item.Points.Count))))))
        Dim a2 As Double = 0.0R
        Dim d3 As Double = 0.0R
        Dim d4 As Double = 0.0R
        GetControlConstants(averageN, a2, d3, d4)
        Dim xUcl = xBarBar + a2 * rBar
        Dim xLcl = xBarBar - a2 * rBar
        Dim rUcl = d4 * rBar
        Dim rLcl = d3 * rBar

        Dim violations = DetectRules(groups.Select(Function(item) item.DateValue).ToList(), means, groups.Select(Function(item) "Tümü").ToList(), groups.Select(Function(item) item.Key).ToList(), groups.Select(Function(item) "").ToList(), "X̄-R / X̄", xBarBar, xUcl, xLcl)
        Dim violationKeys = violations.Select(Function(item) item.RecordId).ToHashSet(StringComparer.OrdinalIgnoreCase)
        Dim meanData = groups.Select(Function(item) New ChartDatum With {.DateValue = item.DateValue, .Caption = item.Key, .Value = item.Mean, .IsViolation = violationKeys.Contains(item.Key)}).ToList()
        Dim rangeData = groups.Select(Function(item) New ChartDatum With {.DateValue = item.DateValue, .Caption = item.Key, .Value = item.Range, .IsViolation = item.Range > rUcl OrElse item.Range < rLcl}).ToList()

        Dim usl = LastNullable(points.Select(Function(item) item.UpperLimit))
        Dim lsl = LastNullable(points.Select(Function(item) item.LowerLimit))
        chartTop.SetData("X̄ Grafiği — Kayıt içindeki gözlerin ortalaması", meanData, xBarBar, xUcl, xLcl, usl, lsl)
        chartBottom.SetData("R Grafiği — Aynı kayıttaki gözlerin yayılımı", rangeData, rBar, rUcl, rLcl, Nothing, Nothing)
        lblChartInfo.Text = "Alt grup: " & groups.Count.ToString() & "   |   Ortalama göz sayısı: " & averageN.ToString() & "   |   X̄̄: " & FormatNumber(xBarBar) & "   |   R̄: " & FormatNumber(rBar)
    End Sub

    Private Function BuildSubgroups() As List(Of Subgroup)
        Return points.
            GroupBy(Function(item)
                        If item.RecordId <> "" Then Return item.RecordId
                        Return item.DateValue.ToString("yyyyMMddHHmmss")
                    End Function).
            Select(Function(group) New Subgroup With {
                .Key = group.Key,
                .DateValue = group.Min(Function(item) item.DateValue),
                .Points = group.OrderBy(Function(item) item.EyeNo).ToList()
            }).
            Where(Function(group) group.Points.Count >= 2).
            OrderBy(Function(group) group.DateValue).
            ToList()
    End Function

    Private Sub BuildAllViolations()
        allViolations.Clear()
        If points.Count >= 2 Then
            Dim values = points.Select(Function(item) item.Value).ToList()
            Dim movingRanges As New List(Of Double)()
            For index As Integer = 1 To values.Count - 1
                movingRanges.Add(Math.Abs(values(index) - values(index - 1)))
            Next
            Dim mean = values.Average()
            Dim mrBar = movingRanges.Average()
            Dim sigma = mrBar / 1.128R
            allViolations.AddRange(DetectRules(points.Select(Function(item) item.DateValue).ToList(), values, points.Select(Function(item) item.EyeNo.ToString()).ToList(), points.Select(Function(item) item.RecordId).ToList(), points.Select(Function(item) item.Result).ToList(), "I-MR / I", mean, mean + 3.0R * sigma, mean - 3.0R * sigma))
            For index As Integer = 0 To movingRanges.Count - 1
                If movingRanges(index) > 3.267R * mrBar Then
                    allViolations.Add(New ViolationRecord With {.ChartType = "I-MR / MR", .RuleName = "Kontrol sınırı dışı", .DateValue = points(index + 1).DateValue, .EyeText = points(index + 1).EyeNo.ToString(), .Value = movingRanges(index), .Detail = "Hareketli aralık UCL sınırını aştı.", .RecordId = points(index + 1).RecordId, .Result = points(index + 1).Result})
                End If
            Next
        End If

        Dim groups = BuildSubgroups()
        If groups.Count >= 2 Then
            Dim means = groups.Select(Function(item) item.Mean).ToList()
            Dim ranges = groups.Select(Function(item) item.Range).ToList()
            Dim xBarBar = means.Average()
            Dim rBar = ranges.Average()
            Dim averageN = Math.Max(2, Math.Min(10, CInt(Math.Round(groups.Average(Function(item) CDbl(item.Points.Count))))))
            Dim a2 As Double = 0.0R
            Dim d3 As Double = 0.0R
            Dim d4 As Double = 0.0R
            GetControlConstants(averageN, a2, d3, d4)
            allViolations.AddRange(DetectRules(groups.Select(Function(item) item.DateValue).ToList(), means, groups.Select(Function(item) "Tümü").ToList(), groups.Select(Function(item) item.Key).ToList(), groups.Select(Function(item) "").ToList(), "X̄-R / X̄", xBarBar, xBarBar + a2 * rBar, xBarBar - a2 * rBar))
            For index As Integer = 0 To groups.Count - 1
                If ranges(index) > d4 * rBar OrElse ranges(index) < d3 * rBar Then
                    allViolations.Add(New ViolationRecord With {.ChartType = "X̄-R / R", .RuleName = "Kontrol sınırı dışı", .DateValue = groups(index).DateValue, .EyeText = "Tümü", .Value = ranges(index), .Detail = "Alt grup aralığı kontrol sınırları dışında.", .RecordId = groups(index).Key})
                End If
            Next
        End If

        allViolations = allViolations.
            GroupBy(Function(item) item.ChartType & "|" & item.RuleName & "|" & item.RecordId & "|" & item.DateValue.Ticks.ToString() & "|" & item.EyeText).
            Select(Function(group) group.First()).
            OrderByDescending(Function(item) item.DateValue).
            ToList()
    End Sub

    Private Shared Function DetectRules(dates As List(Of DateTime),
                                        values As List(Of Double),
                                        eyeTexts As List(Of String),
                                        recordIds As List(Of String),
                                        results As List(Of String),
                                        chartType As String,
                                        center As Double,
                                        ucl As Double,
                                        lcl As Double) As List(Of ViolationRecord)
        Dim found As New List(Of ViolationRecord)()
        If values.Count = 0 Then Return found
        Dim sigma = Math.Abs(ucl - lcl) / 6.0R

        For index As Integer = 0 To values.Count - 1
            If values(index) > ucl OrElse values(index) < lcl Then
                AddViolation(found, chartType, "Kural 1 — Kontrol sınırı dışı", index, dates, values, eyeTexts, recordIds, results, "Değer UCL/LCL kontrol sınırlarının dışında.")
            End If

            If index >= 7 Then
                Dim window = values.Skip(index - 7).Take(8).ToList()
                If window.All(Function(value) value > center) OrElse window.All(Function(value) value < center) Then
                    AddViolation(found, chartType, "Kural 2 — Aynı tarafta 8 nokta", index, dates, values, eyeTexts, recordIds, results, "Proses merkezi kalıcı olarak kaymış olabilir.")
                End If
            End If

            If index >= 5 Then
                Dim window = values.Skip(index - 5).Take(6).ToList()
                Dim rising = True
                Dim falling = True
                For position As Integer = 1 To window.Count - 1
                    If window(position) <= window(position - 1) Then rising = False
                    If window(position) >= window(position - 1) Then falling = False
                Next
                If rising OrElse falling Then
                    AddViolation(found, chartType, "Kural 3 — 6 noktalı trend", index, dates, values, eyeTexts, recordIds, results, "Arka arkaya altı değer sürekli yükseliyor veya düşüyor.")
                End If
            End If

            If sigma > 0.0R AndAlso index >= 2 Then
                Dim window = values.Skip(index - 2).Take(3).ToList()
                If window.Where(Function(value) value > center + 2.0R * sigma).Count() >= 2 OrElse window.Where(Function(value) value < center - 2.0R * sigma).Count() >= 2 Then
                    AddViolation(found, chartType, "Kural 4 — 3 noktadan 2'si 2σ dışında", index, dates, values, eyeTexts, recordIds, results, "Proses kontrol sınırına doğru belirgin biçimde kayıyor.")
                End If
            End If

            If sigma > 0.0R AndAlso index >= 4 Then
                Dim window = values.Skip(index - 4).Take(5).ToList()
                If window.Where(Function(value) value > center + sigma).Count() >= 4 OrElse window.Where(Function(value) value < center - sigma).Count() >= 4 Then
                    AddViolation(found, chartType, "Kural 5 — 5 noktadan 4'ü 1σ dışında", index, dates, values, eyeTexts, recordIds, results, "Proses merkezden uzaklaşıyor olabilir.")
                End If
            End If
        Next
        Return found
    End Function

    Private Shared Sub AddViolation(target As List(Of ViolationRecord),
                                    chartType As String,
                                    ruleName As String,
                                    index As Integer,
                                    dates As List(Of DateTime),
                                    values As List(Of Double),
                                    eyeTexts As List(Of String),
                                    recordIds As List(Of String),
                                    results As List(Of String),
                                    detail As String)
        target.Add(New ViolationRecord With {
            .ChartType = chartType,
            .RuleName = ruleName,
            .DateValue = dates(index),
            .EyeText = If(index < eyeTexts.Count, eyeTexts(index), ""),
            .Value = values(index),
            .Detail = detail,
            .RecordId = If(index < recordIds.Count, recordIds(index), ""),
            .Result = If(index < results.Count, results(index), "")
        })
    End Sub

    Private Sub BindViolations()
        Dim table As New DataTable()
        For Each columnName As String In {"ChartType", "RuleName", "DateText", "EyeText", "ValueText", "Result", "Detail", "RecordId"}
            table.Columns.Add(columnName)
        Next
        For Each item In allViolations
            table.Rows.Add(item.ChartType, item.RuleName, item.DateValue.ToString("dd.MM.yyyy HH:mm"), item.EyeText, FormatNumber(item.Value), item.Result, item.Detail, item.RecordId)
        Next
        violationGrid.DataSource = table
    End Sub

    Private Sub BindHeatMap()
        Dim table As New DataTable()
        For Each columnName As String In {"RiskLevel", "EyeNo", "RecordCount", "OkCount", "NokCount", "InvalidCount", "Average", "StdDev", "MinMax", "Cpk", "LastValue", "LastResult"}
            table.Columns.Add(columnName)
        Next

        For Each eyeGroup In points.Where(Function(item) item.EyeNo > 0).GroupBy(Function(item) item.EyeNo).OrderBy(Function(group) group.Key)
            Dim eyePoints = eyeGroup.OrderBy(Function(item) item.DateValue).ToList()
            Dim values = eyePoints.Select(Function(item) item.Value).ToList()
            Dim stdDev = StandardDeviation(values)
            Dim lower = LastNullable(eyePoints.Select(Function(item) item.LowerLimit))
            Dim upper = LastNullable(eyePoints.Select(Function(item) item.UpperLimit))
            Dim cpk = CalculateCpk(values, lower, upper, stdDev)
            Dim okCount = eyePoints.Where(Function(item) String.Equals(item.Result, "OK", StringComparison.OrdinalIgnoreCase)).Count()
            Dim nokCount = eyePoints.Where(Function(item) String.Equals(item.Result, "NOK", StringComparison.OrdinalIgnoreCase)).Count()
            Dim invalidCount = eyePoints.Where(Function(item) String.Equals(item.Result, "HATALI", StringComparison.OrdinalIgnoreCase)).Count()
            Dim lastPoint = eyePoints.Last()
            Dim riskLevel = "İYİ"
            If nokCount > 0 OrElse String.Equals(lastPoint.Result, "NOK", StringComparison.OrdinalIgnoreCase) OrElse (cpk.HasValue AndAlso cpk.Value < 1.0R) Then
                riskLevel = "YÜKSEK"
            ElseIf invalidCount > 0 OrElse (cpk.HasValue AndAlso cpk.Value < 1.33R) Then
                riskLevel = "İZLE"
            End If
            table.Rows.Add(riskLevel,
                           eyeGroup.Key.ToString(),
                           eyePoints.Count.ToString(),
                           okCount.ToString(),
                           nokCount.ToString(),
                           invalidCount.ToString(),
                           FormatNumber(values.Average()),
                           If(stdDev > 0.0R, FormatNumber(stdDev), "-"),
                           FormatNumber(values.Min()) & " / " & FormatNumber(values.Max()),
                           If(cpk.HasValue, cpk.Value.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR")), "-"),
                           FormatNumber(lastPoint.Value),
                           lastPoint.Result)
        Next
        heatGrid.DataSource = table
    End Sub

    Private Sub ViolationGridCellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse e.RowIndex >= violationGrid.Rows.Count Then Return
        violationGrid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.MistyRose
        violationGrid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.DarkRed
    End Sub

    Private Sub ViolationGridCellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 OrElse e.RowIndex >= violationGrid.Rows.Count Then Return

        Try
            Dim selectedGridRow = violationGrid.Rows(e.RowIndex)
            Dim recordId = Convert.ToString(selectedGridRow.Cells("RecordId").Value).Trim()
            Dim eyeText = Convert.ToString(selectedGridRow.Cells("EyeText").Value).Trim()
            If recordId = "" Then
                MessageBox.Show("Bu ihlal için kaynak ölçüm kaydı bulunamadı.", "Kural ihlali", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim matchingRows = sourceRows.
                Where(Function(row) String.Equals(DataService.GetValue(row, "RecordId").Trim(), recordId, StringComparison.OrdinalIgnoreCase)).
                ToList()

            Dim eyeNumber As Integer
            Dim hasExactEye = Integer.TryParse(eyeText, eyeNumber)
            If hasExactEye Then
                matchingRows = matchingRows.
                    Where(Function(row) String.Equals(DataService.GetValue(row, "EyeNo").Trim(), eyeNumber.ToString(), StringComparison.OrdinalIgnoreCase)).
                    ToList()
            End If

            If matchingRows.Count = 0 Then
                MessageBox.Show("İhlale bağlı ölçüm satırı güncel kayıtlarda bulunamadı.", "Kaynak ölçüm bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            If hasExactEye AndAlso matchingRows.Count = 1 AndAlso AppState.IsAdmin Then
                If MeasurementCorrectionUiService.EditMeasurement(Me, matchingRows(0)) Then RefreshAfterMeasurementCorrection()
                Return
            End If

            Dim detailTitle = seriesTitle & " | Kayıt: " & recordId
            Using detailForm As New FrmSpcMeasurementDetails(detailTitle, matchingRows)
                detailForm.ShowDialog(Me)
            End Using
            RefreshAfterMeasurementCorrection()
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Ölçüm açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub RefreshAfterMeasurementCorrection()
        LoadPoints()
        BuildAllViolations()
        BindViolations()
        BindHeatMap()
        RefreshCharts()
    End Sub

    Private Sub HeatGridCellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse e.RowIndex >= heatGrid.Rows.Count Then Return
        Dim riskText = Convert.ToString(heatGrid.Rows(e.RowIndex).Cells("RiskLevel").Value)
        If String.Equals(riskText, "YÜKSEK", StringComparison.OrdinalIgnoreCase) Then
            heatGrid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.MistyRose
            heatGrid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.DarkRed
        ElseIf String.Equals(riskText, "İZLE", StringComparison.OrdinalIgnoreCase) Then
            heatGrid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.LemonChiffon
            heatGrid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.FromArgb(112, 71, 0)
        Else
            heatGrid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.Honeydew
            heatGrid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.DarkGreen
        End If
    End Sub

    Private Shared Sub GetControlConstants(sampleSize As Integer, ByRef a2 As Double, ByRef d3 As Double, ByRef d4 As Double)
        Select Case Math.Max(2, Math.Min(10, sampleSize))
            Case 2 : a2 = 1.88R : d3 = 0.0R : d4 = 3.267R
            Case 3 : a2 = 1.023R : d3 = 0.0R : d4 = 2.574R
            Case 4 : a2 = 0.729R : d3 = 0.0R : d4 = 2.282R
            Case 5 : a2 = 0.577R : d3 = 0.0R : d4 = 2.114R
            Case 6 : a2 = 0.483R : d3 = 0.0R : d4 = 2.004R
            Case 7 : a2 = 0.419R : d3 = 0.076R : d4 = 1.924R
            Case 8 : a2 = 0.373R : d3 = 0.136R : d4 = 1.864R
            Case 9 : a2 = 0.337R : d3 = 0.184R : d4 = 1.816R
            Case Else : a2 = 0.308R : d3 = 0.223R : d4 = 1.777R
        End Select
    End Sub

    Private Shared Function CalculateCpk(values As List(Of Double), lower As Double?, upper As Double?, stdDev As Double) As Double?
        If values.Count < 2 OrElse Not lower.HasValue OrElse Not upper.HasValue OrElse stdDev <= 0.0R OrElse upper.Value <= lower.Value Then Return Nothing
        Dim mean = values.Average()
        Return Math.Min((upper.Value - mean) / (3.0R * stdDev), (mean - lower.Value) / (3.0R * stdDev))
    End Function

    Private Shared Function StandardDeviation(values As List(Of Double)) As Double
        If values.Count <= 1 Then Return 0.0R
        Dim mean = values.Average()
        Return Math.Sqrt(values.Sum(Function(value) Math.Pow(value - mean, 2.0R)) / (values.Count - 1))
    End Function

    Private Shared Function LastNullable(values As IEnumerable(Of Double?)) As Double?
        For Each optionalValue In values.Reverse()
            If optionalValue.HasValue Then Return optionalValue.Value
        Next
        Return Nothing
    End Function

    Private Shared Function ParseNullableDouble(textValue As String) As Double?
        Dim decimalValue As Decimal = 0D
        If NumberUtil.TryParseDecimal(textValue, decimalValue) Then Return CDbl(decimalValue)
        Return Nothing
    End Function

    Private Shared Function ParseDateSafe(textValue As String) As DateTime
        Dim parsed As DateTime
        If DateTime.TryParse(If(textValue, "").Trim(), parsed) Then Return parsed
        Return DateTime.MinValue
    End Function

    Private Shared Function ParseIntSafe(textValue As String) As Integer
        Dim parsed As Integer
        If Integer.TryParse(If(textValue, "").Trim(), parsed) Then Return parsed
        Return 0
    End Function

    Private Shared Function FormatNumber(value As Double) As String
        Return value.ToString("0.###", CultureInfo.GetCultureInfo("tr-TR"))
    End Function
End Class
