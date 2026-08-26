Imports System.Data
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Globalization
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmSpcDashboard
    Inherits Form

    Private Const MinimumSpcInterpretationCount As Integer = 25

    Private NotInheritable Class SpcSeries
        Public Property Key As String = ""
        Public Property TrCode As String = ""
        Public Property DrawingRev As String = ""
        Public Property DrawingScope As String = ""
        Public Property SpcKey As String = ""
        Public Property MeasureName As String = ""
        Public Property LatestMeasureId As String = ""
        Public Property Rows As New List(Of Dictionary(Of String, String))()

        Public ReadOnly Property DisplayText As String
            Get
                Dim namePart = If(String.IsNullOrWhiteSpace(MeasureName), "", " | " & MeasureName)
                Return TrCode & " / " & DrawingRev &
                       " | " & ProductInfo.NormalizeDrawingScope(DrawingScope) &
                       " | SPC: " & SpcKey &
                       namePart &
                       " (" & Rows.Count.ToString() & " kayıt)"
            End Get
        End Property

        Public ReadOnly Property SearchText As String
            Get
                Return (DisplayText & " " & LatestMeasureId).ToUpperInvariant()
            End Get
        End Property
    End Class

    Private NotInheritable Class SpcPoint
        Public Property SourceRow As Dictionary(Of String, String)
        Public Property DateValue As DateTime
        Public Property EyeNo As Integer
        Public Property Value As Decimal
        Public Property Result As String = ""
        Public Property LowerLimit As Decimal?
        Public Property UpperLimit As Decimal?
        Public Property Nominal As Decimal?
    End Class

    Private NotInheritable Class RiskSummary
        Public Property SeriesKey As String = ""
        Public Property RiskLevel As String = ""
        Public Property RiskScore As Integer
        Public Property TrCode As String = ""
        Public Property DrawingRev As String = ""
        Public Property MeasureName As String = ""
        Public Property SpcKey As String = ""
        Public Property RecordCount As Integer
        Public Property NokCount As Integer
        Public Property InvalidCount As Integer
        Public Property Cp As Double?
        Public Property Cpk As Double?
        Public Property LastValue As Decimal
        Public Property LastResult As String = ""
        Public Property Reason As String = ""
    End Class

    Private NotInheritable Class TrendChartPanel
        Inherits Panel

        Private points As New List(Of SpcPoint)()
        Private lowerLimit As Decimal?
        Private upperLimit As Decimal?
        Private nominalValue As Decimal?

        Public Sub New()
            DoubleBuffered = True
            BackColor = Color.White
            BorderStyle = BorderStyle.FixedSingle
        End Sub

        Public Sub SetData(newPoints As List(Of SpcPoint), lower As Decimal?, upper As Decimal?, nominal As Decimal?)
            points = If(newPoints, New List(Of SpcPoint)())
            lowerLimit = lower
            upperLimit = upper
            nominalValue = nominal
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
            e.Graphics.Clear(Color.White)

            Dim plot = New Rectangle(58, 18, Math.Max(40, ClientSize.Width - 82), Math.Max(40, ClientSize.Height - 62))
            Using axisPen As New Pen(Color.FromArgb(180, 190, 205)),
                  gridPen As New Pen(Color.FromArgb(232, 236, 242)),
                  textBrush As New SolidBrush(Color.FromArgb(65, 75, 90))

                e.Graphics.DrawRectangle(axisPen, plot)

                If points.Count = 0 Then
                    TextRenderer.DrawText(e.Graphics,
                                          "Gösterilecek ölçüm verisi yok.",
                                          New Font("Segoe UI", 10.0F, FontStyle.Bold),
                                          ClientRectangle,
                                          Color.FromArgb(90, 100, 115),
                                          TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
                    Return
                End If

                Dim yValues As New List(Of Double)(points.Select(Function(p) CDbl(p.Value)))
                If lowerLimit.HasValue Then yValues.Add(CDbl(lowerLimit.Value))
                If upperLimit.HasValue Then yValues.Add(CDbl(upperLimit.Value))
                If nominalValue.HasValue Then yValues.Add(CDbl(nominalValue.Value))

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

                For i As Integer = 0 To 4
                    Dim y = CSng(plot.Bottom - (plot.Height * i / 4.0F))
                    e.Graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y)
                    Dim labelValue = minY + ((maxY - minY) * i / 4.0R)
                    TextRenderer.DrawText(e.Graphics,
                                          labelValue.ToString("0.###", CultureInfo.GetCultureInfo("tr-TR")),
                                          New Font("Segoe UI", 7.5F),
                                          New Rectangle(0, CInt(y - 9), 54, 18),
                                          Color.FromArgb(90, 100, 115),
                                          TextFormatFlags.Right Or TextFormatFlags.VerticalCenter)
                Next

                DrawLimitLine(e.Graphics, plot, minY, maxY, upperLimit, "Üst", Color.Firebrick)
                DrawLimitLine(e.Graphics, plot, minY, maxY, lowerLimit, "Alt", Color.Firebrick)
                DrawLimitLine(e.Graphics, plot, minY, maxY, nominalValue, "Nom", Color.FromArgb(31, 91, 158))

                Dim screenPoints As New List(Of PointF)()
                For i As Integer = 0 To points.Count - 1
                    Dim x As Single
                    If points.Count = 1 Then
                        x = plot.Left + plot.Width / 2.0F
                    Else
                        x = CSng(plot.Left + (plot.Width * i / CDbl(points.Count - 1)))
                    End If
                    Dim y = ValueToY(CDbl(points(i).Value), plot, minY, maxY)
                    screenPoints.Add(New PointF(x, y))
                Next

                If screenPoints.Count > 1 Then
                    Using trendPen As New Pen(Color.FromArgb(44, 93, 160), 2.0F)
                        e.Graphics.DrawLines(trendPen, screenPoints.ToArray())
                    End Using
                End If

                For i As Integer = 0 To screenPoints.Count - 1
                    Dim color = ResultColor(points(i).Result)
                    Using brush As New SolidBrush(color),
                          borderPen As New Pen(Color.White, 1.5F)
                        Dim r = New RectangleF(screenPoints(i).X - 4.5F, screenPoints(i).Y - 4.5F, 9.0F, 9.0F)
                        e.Graphics.FillEllipse(brush, r)
                        e.Graphics.DrawEllipse(borderPen, r)
                    End Using
                Next

                Dim firstDate = points.First().DateValue.ToString("dd.MM.yyyy HH:mm")
                Dim lastDate = points.Last().DateValue.ToString("dd.MM.yyyy HH:mm")
                TextRenderer.DrawText(e.Graphics, firstDate, New Font("Segoe UI", 7.5F), New Point(plot.Left, plot.Bottom + 8), Color.FromArgb(90, 100, 115))
                Dim lastSize = TextRenderer.MeasureText(lastDate, New Font("Segoe UI", 7.5F))
                TextRenderer.DrawText(e.Graphics, lastDate, New Font("Segoe UI", 7.5F), New Point(plot.Right - lastSize.Width, plot.Bottom + 8), Color.FromArgb(90, 100, 115))
            End Using
        End Sub

        Private Shared Sub DrawLimitLine(g As Graphics,
                                         plot As Rectangle,
                                         minY As Double,
                                         maxY As Double,
                                         value As Decimal?,
                                         label As String,
                                         color As Color)
            If Not value.HasValue Then Return

            Dim y = ValueToY(CDbl(value.Value), plot, minY, maxY)
            Using pen As New Pen(color, 1.4F)
                pen.DashStyle = DashStyle.Dash
                g.DrawLine(pen, plot.Left, y, plot.Right, y)
            End Using

            TextRenderer.DrawText(g,
                                  label & ": " & value.Value.ToString("0.###", CultureInfo.GetCultureInfo("tr-TR")),
                                  New Font("Segoe UI", 7.5F, FontStyle.Bold),
                                  New Point(plot.Left + 4, CInt(y - 18)),
                                  color)
        End Sub

        Private Shared Function ValueToY(value As Double, plot As Rectangle, minY As Double, maxY As Double) As Single
            Dim ratio = (value - minY) / (maxY - minY)
            Return CSng(plot.Bottom - (plot.Height * ratio))
        End Function

        Private Shared Function ResultColor(resultText As String) As Color
            If String.Equals(resultText, "NOK", StringComparison.OrdinalIgnoreCase) Then Return Color.Firebrick
            If String.Equals(resultText, "HATALI", StringComparison.OrdinalIgnoreCase) Then Return Color.DarkGoldenrod
            Return Color.SeaGreen
        End Function
    End Class

    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly cboSeries As New ComboBox()
    Private ReadOnly cboEye As New ComboBox()
    Private ReadOnly dtFrom As New DateTimePicker()
    Private ReadOnly dtTo As New DateTimePicker()
    Private ReadOnly lblInfo As New Label()
    Private ReadOnly lblTotalValue As New Label()
    Private ReadOnly lblOkValue As New Label()
    Private ReadOnly lblNokValue As New Label()
    Private ReadOnly lblAverageValue As New Label()
    Private ReadOnly lblMinMaxValue As New Label()
    Private ReadOnly lblStdDevValue As New Label()
    Private ReadOnly lblCapabilityValue As New Label()
    Private ReadOnly kpiToolTip As New ToolTip() With {
        .AutoPopDelay = 20000,
        .InitialDelay = 350,
        .ReshowDelay = 100,
        .ShowAlways = True
    }
    Private ReadOnly chart As New TrendChartPanel()
    Private ReadOnly riskGrid As New DataGridView()
    Private ReadOnly btnDetails As New Button()
    Private ReadOnly btnAnalysis As New Button()
    Private ReadOnly btnCorrectLimits As New Button()
    Private ReadOnly grid As New DataGridView()

    Private allRows As New List(Of Dictionary(Of String, String))()
    Private allSeries As New List(Of SpcSeries)()
    Private isLoading As Boolean = False

    Public Sub New()
        AuthorizationService.Require(AppState.CanOpenSpcDashboard, "SPC Dashboard")
        AppIconService.Apply(Me)
        Text = "SPC Dashboard"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1400, 820)
        MinimumSize = New Size(920, 620)
        BackColor = Color.White

        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 5,
            .BackColor = Color.White,
            .Padding = New Padding(10)
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 74.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 86.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))
        Controls.Add(root)

        root.Controls.Add(BuildFilterPanel(), 0, 0)
        root.Controls.Add(BuildInfoPanel(), 0, 1)
        root.Controls.Add(BuildStatsPanel(), 0, 2)
        root.Controls.Add(BuildRiskPanel(), 0, 3)
        chart.Dock = DockStyle.Fill
        chart.Margin = New Padding(3, 4, 3, 6)
        root.Controls.Add(chart, 0, 4)

        AddHandler Shown, Sub() LoadDashboardData()
    End Sub

    Private Function BuildHeader() As Control
        Dim panel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .BackColor = Color.FromArgb(31, 78, 121),
            .Padding = New Padding(18, 6, 18, 6)
        }
        panel.RowStyles.Add(New RowStyle(SizeType.Percent, 58.0F))
        panel.RowStyles.Add(New RowStyle(SizeType.Percent, 42.0F))

        Dim title As New Label() With {
            .Text = "SPC Dashboard",
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 15.0F, FontStyle.Bold),
            .ForeColor = Color.White,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        panel.Controls.Add(title, 0, 0)

        Dim subTitle As New Label() With {
            .Text = "Kontrol ölçülerinin zaman içindeki gidişini, tolerans dışı kayıtlarını ve proses özetini izleyin.",
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Regular),
            .ForeColor = Color.FromArgb(235, 242, 252),
            .TextAlign = ContentAlignment.MiddleLeft,
            .AutoEllipsis = True
        }
        panel.Controls.Add(subTitle, 0, 1)

        Return panel
    End Function

    Private Function BuildInfoPanel() As Control
        Dim panel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .BackColor = Color.FromArgb(239, 246, 255),
            .Margin = New Padding(3, 4, 3, 0)
        }
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 510.0F))
        panel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        lblInfo.Text = "SPC ölçü serisi seçiniz."
        lblInfo.Dock = DockStyle.Fill
        lblInfo.TextAlign = ContentAlignment.MiddleLeft
        lblInfo.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblInfo.ForeColor = Color.FromArgb(42, 70, 105)
        lblInfo.BackColor = Color.FromArgb(239, 246, 255)
        lblInfo.Padding = New Padding(12, 0, 12, 0)
        lblInfo.AutoEllipsis = True
        lblInfo.Margin = New Padding(0)
        panel.Controls.Add(lblInfo, 0, 0)

        btnDetails.Text = "Ölçüm Detayları"
        btnDetails.Dock = DockStyle.Fill
        btnDetails.Margin = New Padding(3, 2, 3, 2)
        btnDetails.BackColor = Color.FromArgb(31, 78, 121)
        btnDetails.ForeColor = Color.White
        btnDetails.FlatStyle = FlatStyle.Flat
        btnDetails.Cursor = Cursors.Hand
        btnDetails.Enabled = False
        AddHandler btnDetails.Click, Sub() OpenSelectedDetails()

        btnAnalysis.Text = "SPC Analizi"
        btnAnalysis.Dock = DockStyle.Fill
        btnAnalysis.Margin = New Padding(3, 2, 3, 2)
        btnAnalysis.BackColor = Color.FromArgb(32, 126, 75)
        btnAnalysis.ForeColor = Color.White
        btnAnalysis.FlatStyle = FlatStyle.Flat
        btnAnalysis.Cursor = Cursors.Hand
        btnAnalysis.Enabled = False
        AddHandler btnAnalysis.Click, Sub() OpenSelectedAnalysis()

        btnCorrectLimits.Text = "Geçmiş Limitleri Düzelt"
        btnCorrectLimits.Dock = DockStyle.Fill
        btnCorrectLimits.Margin = New Padding(3, 2, 3, 2)
        btnCorrectLimits.BackColor = Color.FromArgb(180, 103, 15)
        btnCorrectLimits.ForeColor = Color.White
        btnCorrectLimits.FlatStyle = FlatStyle.Flat
        btnCorrectLimits.Cursor = Cursors.Hand
        btnCorrectLimits.Enabled = False
        btnCorrectLimits.Visible = AppState.CanEditSpcDashboard
        AddHandler btnCorrectLimits.Click, AddressOf CorrectSelectedHistoricalLimits

        Dim buttonPanel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 3,
            .RowCount = 1,
            .Margin = New Padding(3, 0, 3, 0),
            .BackColor = Color.Transparent
        }
        buttonPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 34.0F))
        buttonPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.0F))
        buttonPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 33.0F))
        If AppState.CanEditSpcDashboard Then
            buttonPanel.Controls.Add(btnCorrectLimits, 0, 0)
            buttonPanel.Controls.Add(btnAnalysis, 1, 0)
            buttonPanel.Controls.Add(btnDetails, 2, 0)
        Else
            buttonPanel.Controls.Add(btnAnalysis, 1, 0)
            buttonPanel.Controls.Add(btnDetails, 2, 0)
        End If
        panel.Controls.Add(buttonPanel, 1, 0)

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

        txtSearch.PlaceholderText = "TR / ölçü adı / SPC ara"
        AddHandler txtSearch.TextChanged, Sub() RefreshSeriesList()
        panel.Controls.Add(BuildField("Arama", txtSearch, 210))

        cboSeries.DropDownStyle = ComboBoxStyle.DropDownList
        cboSeries.DisplayMember = "DisplayText"
        AddHandler cboSeries.SelectedIndexChanged,
            Sub()
                If isLoading Then Return
                RefreshEyeFilter()
                RefreshDashboard()
            End Sub
        panel.Controls.Add(BuildField("Ölçü Serisi", cboSeries, 520))

        cboEye.DropDownStyle = ComboBoxStyle.DropDownList
        AddHandler cboEye.SelectedIndexChanged, Sub()
                                                    If Not isLoading Then RefreshDashboard()
                                                End Sub
        panel.Controls.Add(BuildField("Göz", cboEye, 95))

        ConfigureDatePicker(dtFrom)
        ConfigureDatePicker(dtTo)
        AddHandler dtFrom.ValueChanged, Sub()
                                            If Not isLoading Then
                                                RefreshRiskList()
                                                RefreshDashboard()
                                            End If
                                        End Sub
        AddHandler dtTo.ValueChanged, Sub()
                                          If Not isLoading Then
                                              RefreshRiskList()
                                              RefreshDashboard()
                                          End If
                                      End Sub
        panel.Controls.Add(BuildField("Başlangıç", dtFrom, 125))
        panel.Controls.Add(BuildField("Bitiş", dtTo, 125))

        Dim btnRefresh As New Button() With {
            .Text = "Yenile",
            .Width = 100,
            .Height = 32,
            .Margin = New Padding(8, 21, 4, 4),
            .BackColor = Color.FromArgb(31, 78, 121),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand
        }
        AddHandler btnRefresh.Click, Sub() LoadDashboardData()
        panel.Controls.Add(btnRefresh)

        Dim btnClear As New Button() With {
            .Text = "Temizle",
            .Width = 100,
            .Height = 32,
            .Margin = New Padding(4, 21, 4, 4),
            .Cursor = Cursors.Hand
        }
        AddHandler btnClear.Click,
            Sub()
                txtSearch.Clear()
                dtFrom.Checked = False
                dtTo.Checked = False
                If cboEye.Items.Count > 0 Then cboEye.SelectedIndex = 0
                RefreshSeriesList()
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
            .Font = New Font("Segoe UI", 8.3F, FontStyle.Bold),
            .BackColor = Color.Transparent
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

    Private Function BuildStatsPanel() As Control
        Dim panel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 7,
            .RowCount = 1,
            .BackColor = Color.White,
            .Padding = New Padding(0, 8, 0, 8)
        }
        For i As Integer = 0 To 6
            panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F / 7.0F))
        Next
        panel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        panel.Controls.Add(BuildKpiCard("Kayıt", lblTotalValue, Color.FromArgb(31, 78, 121),
                                       "Aktif tarih, göz ve ölçü serisi filtrelerine uyan geçerli ölçüm satırı sayısıdır."), 0, 0)
        panel.Controls.Add(BuildKpiCard("OK", lblOkValue, Color.SeaGreen,
                                       "Alt ve üst tolerans limitleri içinde kalan ölçümlerin sayısıdır."), 1, 0)
        panel.Controls.Add(BuildKpiCard("NOK / Hatalı", lblNokValue, Color.Firebrick,
                                       "İlk değer tolerans dışı (NOK), ikinci değer sayısal olarak değerlendirilemeyen veya geçersiz ölçüm sayısıdır."), 2, 0)
        panel.Controls.Add(BuildKpiCard("Ortalama", lblAverageValue, Color.FromArgb(44, 93, 160),
                                       "Seçili ölçümlerin aritmetik ortalamasıdır. Nominale yakınlık prosesin merkezlenmesini gösterir."), 3, 0)
        panel.Controls.Add(BuildKpiCard("Min / Max", lblMinMaxValue, Color.FromArgb(90, 95, 110),
                                       "Seçili ölçümlerde görülen en düşük ve en yüksek değerdir. Tolerans limitleriyle birlikte değerlendirilmelidir."), 4, 0)
        panel.Controls.Add(BuildKpiCard("Std. Sapma", lblStdDevValue, Color.FromArgb(112, 71, 0),
                                       "Ölçümlerin ortalama etrafındaki yayılımını gösterir. Aynı ölçü ve koşullarda daha düşük değer daha kararlı proses demektir."), 5, 0)
        panel.Controls.Add(BuildKpiCard("Cp / Cpk", lblCapabilityValue, Color.FromArgb(94, 68, 165),
                                       "Cp prosesin potansiyel yeterliliğini, Cpk ise merkezlenmeyi de dikkate alan gerçek yeterliliğini gösterir."), 6, 0)

        Return panel
    End Function

    Private Function BuildRiskPanel() As Control
        Dim panel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .BackColor = Color.White,
            .Margin = New Padding(3, 0, 3, 4)
        }
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        panel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim titleBar As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .BackColor = Color.FromArgb(255, 248, 225),
            .Margin = New Padding(0)
        }
        titleBar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        titleBar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 175.0F))
        Dim title As New Label() With {
            .Text = "Riskli Ölçüler Listesi",
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(80, 45, 0),
            .BackColor = Color.FromArgb(255, 248, 225),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(10, 0, 10, 0)
        }
        titleBar.Controls.Add(title, 0, 0)

        Dim btnPdfReport As New Button() With {
            .Text = "PDF Raporu Hazırla",
            .Dock = DockStyle.Fill,
            .Margin = New Padding(5, 3, 5, 3),
            .BackColor = Color.FromArgb(31, 78, 121),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand
        }
        AddHandler btnPdfReport.Click, AddressOf PrepareRiskPdfReport
        titleBar.Controls.Add(btnPdfReport, 1, 0)
        panel.Controls.Add(titleBar, 0, 0)

        ConfigureRiskGrid()
        panel.Controls.Add(riskGrid, 0, 1)

        Return panel
    End Function

    Private Async Sub PrepareRiskPdfReport(sender As Object, e As EventArgs)
        If riskGrid.Rows.Count = 0 Then
            MessageBox.Show("Aktif filtrelerde raporlanacak riskli ölçü bulunamadı.", "Riskli ölçüler PDF raporu", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Using dialog As New SaveFileDialog() With {
            .Title = "Riskli Ölçüler PDF Raporunu Kaydet",
            .Filter = "PDF Dosyası (*.pdf)|*.pdf",
            .DefaultExt = "pdf",
            .AddExtension = True,
            .FileName = "SPC_Riskli_Olculer_" & DateTime.Now.ToString("yyyyMMdd_HHmm") & ".pdf"
        }
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return

            Dim reportButton = TryCast(sender, Button)
            Try
                Cursor = Cursors.WaitCursor
                If reportButton IsNot Nothing Then reportButton.Enabled = False
                Dim options As New RiskMeasurementPdfReportOptions With {
                    .FilterSummary = BuildRiskReportFilterSummary(),
                    .SortSummary = BuildRiskReportSortSummary(),
                    .GeneratedBy = AppState.CurrentUserName & " / " & AppState.CurrentRole,
                    .ComputerName = Environment.MachineName,
                    .Rows = BuildRiskReportRows()
                }
                Await RiskMeasurementsPdfReportService.CreateAsync(dialog.FileName, options, Me)

                Dim openResult = MessageBox.Show(
                    "PDF raporu oluşturuldu." & Environment.NewLine & dialog.FileName & Environment.NewLine & Environment.NewLine & "Rapor şimdi açılsın mı?",
                    "Riskli ölçüler PDF raporu",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information)
                If openResult = DialogResult.Yes Then
                    Process.Start(New ProcessStartInfo(dialog.FileName) With {.UseShellExecute = True})
                End If
            Catch ex As Exception
                MessageBox.Show(ex.Message, "PDF raporu oluşturulamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                If reportButton IsNot Nothing AndAlso Not reportButton.IsDisposed Then reportButton.Enabled = True
                Cursor = Cursors.Default
            End Try
        End Using
    End Sub

    Private Function BuildRiskReportRows() As List(Of RiskMeasurementPdfRow)
        Dim result As New List(Of RiskMeasurementPdfRow)()
        For Each gridRow As DataGridViewRow In riskGrid.Rows
            If gridRow.IsNewRow Then Continue For
            Dim seriesKey = GridCellText(gridRow, "SeriesKey")
            Dim series = allSeries.FirstOrDefault(
                Function(candidate) String.Equals(candidate.Key, seriesKey, StringComparison.OrdinalIgnoreCase))
            Dim reportPoints As New List(Of SpcPoint)()
            If series IsNot Nothing Then
                reportPoints = BuildPointsForSeries(series, "TÜMÜ").
                    OrderBy(Function(point) point.DateValue).
                    ThenBy(Function(point) point.EyeNo).
                    ThenBy(Function(point) DataService.GetValue(point.SourceRow, "RecordId")).
                    ToList()
            End If

            Dim chartPoints = reportPoints.
                TakeLast(50).
                Select(Function(point) New RiskMeasurementPdfPoint With {
                    .DateValue = point.DateValue,
                    .EyeNo = point.EyeNo,
                    .Value = point.Value,
                    .Result = point.Result
                }).
                ToList()

            result.Add(New RiskMeasurementPdfRow With {
                .RiskLevel = GridCellText(gridRow, "RiskLevel"),
                .TrCode = GridCellText(gridRow, "TrCode"),
                .DrawingRev = GridCellText(gridRow, "DrawingRev"),
                .MeasureName = GridCellText(gridRow, "MeasureName"),
                .Cp = GridCellNullableDouble(gridRow, "Cp"),
                .Cpk = GridCellNullableDouble(gridRow, "Cpk"),
                .RecordCount = GridCellInteger(gridRow, "RecordCount"),
                .NokCount = GridCellInteger(gridRow, "NokCount"),
                .InvalidCount = GridCellInteger(gridRow, "InvalidCount"),
                .LastValue = GridCellNullableDecimal(gridRow, "LastValue"),
                .LastResult = GridCellText(gridRow, "LastResult"),
                .Reason = GridCellText(gridRow, "Reason"),
                .LowerLimit = LastNullable(reportPoints.Select(Function(point) point.LowerLimit)),
                .UpperLimit = LastNullable(reportPoints.Select(Function(point) point.UpperLimit)),
                .Nominal = LastNullable(reportPoints.Select(Function(point) point.Nominal)),
                .ChartPoints = chartPoints
            })
        Next
        Return result
    End Function

    Private Function BuildRiskReportFilterSummary() As String
        Dim parts As New List(Of String)()
        parts.Add("Arama: " & If(txtSearch.Text.Trim() = "", "Tümü", txtSearch.Text.Trim()))
        parts.Add("Başlangıç: " & If(dtFrom.Checked, dtFrom.Value.ToString("dd.MM.yyyy"), "Tümü"))
        parts.Add("Bitiş: " & If(dtTo.Checked, dtTo.Value.ToString("dd.MM.yyyy"), "Tümü"))
        parts.Add("Gösterilen riskli ölçü: " & riskGrid.Rows.Count.ToString())
        Return String.Join(" | ", parts)
    End Function

    Private Function BuildRiskReportSortSummary() As String
        If riskGrid.SortedColumn Is Nothing OrElse riskGrid.SortOrder = SortOrder.None Then Return "Cpk - artan"
        Dim direction = If(riskGrid.SortOrder = SortOrder.Descending, "azalan", "artan")
        Return riskGrid.SortedColumn.HeaderText & " - " & direction
    End Function

    Private Shared Function GridCellText(row As DataGridViewRow, columnName As String) As String
        If row Is Nothing OrElse row.DataGridView Is Nothing OrElse Not row.DataGridView.Columns.Contains(columnName) Then Return ""
        Return Convert.ToString(row.Cells(columnName).Value).Trim()
    End Function

    Private Shared Function GridCellNullableDouble(row As DataGridViewRow, columnName As String) As Double?
        If row Is Nothing OrElse row.DataGridView Is Nothing OrElse Not row.DataGridView.Columns.Contains(columnName) Then Return Nothing
        Dim raw = row.Cells(columnName).Value
        If raw Is Nothing OrElse raw Is DBNull.Value Then Return Nothing
        If TypeOf raw Is Double Then Return DirectCast(raw, Double)
        Dim parsed As Decimal
        If NumberUtil.TryParseDecimal(Convert.ToString(raw), parsed) Then Return CDbl(parsed)
        Return Nothing
    End Function

    Private Shared Function GridCellNullableDecimal(row As DataGridViewRow, columnName As String) As Decimal?
        If row Is Nothing OrElse row.DataGridView Is Nothing OrElse Not row.DataGridView.Columns.Contains(columnName) Then Return Nothing
        Dim raw = row.Cells(columnName).Value
        If raw Is Nothing OrElse raw Is DBNull.Value Then Return Nothing
        If TypeOf raw Is Decimal Then Return DirectCast(raw, Decimal)
        Dim parsed As Decimal
        If NumberUtil.TryParseDecimal(Convert.ToString(raw), parsed) Then Return parsed
        Return Nothing
    End Function

    Private Shared Function GridCellInteger(row As DataGridViewRow, columnName As String) As Integer
        If row Is Nothing OrElse row.DataGridView Is Nothing OrElse Not row.DataGridView.Columns.Contains(columnName) Then Return 0
        Dim raw = row.Cells(columnName).Value
        If raw Is Nothing OrElse raw Is DBNull.Value Then Return 0
        If TypeOf raw Is Integer Then Return DirectCast(raw, Integer)
        Dim parsed As Integer
        If Integer.TryParse(Convert.ToString(raw), parsed) Then Return parsed
        Return 0
    End Function

    Private Function BuildKpiCard(title As String, valueLabel As Label, accent As Color, helpText As String) As Control
        Dim card As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 2,
            .BackColor = Color.FromArgb(248, 250, 253),
            .Margin = New Padding(3),
            .Padding = New Padding(0)
        }
        card.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 5.0F))
        card.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        card.RowStyles.Add(New RowStyle(SizeType.Percent, 46.0F))
        card.RowStyles.Add(New RowStyle(SizeType.Percent, 54.0F))

        Dim stripe As New Panel() With {.Dock = DockStyle.Fill, .BackColor = accent, .Margin = New Padding(0)}
        card.Controls.Add(stripe, 0, 0)
        card.SetRowSpan(stripe, 2)

        card.Controls.Add(New Label() With {
            .Text = title,
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 8.2F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(70, 80, 95),
            .TextAlign = ContentAlignment.BottomLeft,
            .Padding = New Padding(10, 0, 6, 0),
            .AutoEllipsis = True
        }, 1, 0)

        valueLabel.Text = "-"
        valueLabel.Dock = DockStyle.Fill
        valueLabel.Font = New Font("Segoe UI", 13.0F, FontStyle.Bold)
        valueLabel.ForeColor = accent
        valueLabel.TextAlign = ContentAlignment.TopLeft
        valueLabel.Padding = New Padding(10, 0, 6, 0)
        valueLabel.AutoEllipsis = True
        card.Controls.Add(valueLabel, 1, 1)

        SetToolTipRecursive(card, helpText)

        Return card
    End Function

    Private Sub SetToolTipRecursive(target As Control, textValue As String)
        If target Is Nothing Then Return
        kpiToolTip.SetToolTip(target, If(textValue, ""))
        For Each child As Control In target.Controls
            SetToolTipRecursive(child, textValue)
        Next
    End Sub

    Private Sub ConfigureRiskGrid()
        riskGrid.Dock = DockStyle.Fill
        riskGrid.ReadOnly = True
        riskGrid.AllowUserToAddRows = False
        riskGrid.AllowUserToDeleteRows = False
        riskGrid.RowHeadersVisible = False
        riskGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        riskGrid.MultiSelect = False
        riskGrid.AutoGenerateColumns = False
        riskGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        riskGrid.EnableHeadersVisualStyles = False
        riskGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(255, 239, 213)
        riskGrid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 8.5F, FontStyle.Bold)
        riskGrid.BackgroundColor = Color.White
        riskGrid.GridColor = Color.Gainsboro
        riskGrid.RowTemplate.Height = 25

        riskGrid.Columns.Clear()
        riskGrid.Columns.Add(MakeColumn("RiskLevel", "Risk", 85, 55))
        riskGrid.Columns.Add(MakeColumn("TrCode", "TR", 90, 65))
        riskGrid.Columns.Add(MakeColumn("DrawingRev", "Rev.", 55, 35))
        riskGrid.Columns.Add(MakeColumn("MeasureName", "Ölçü", 170, 140))
        riskGrid.Columns.Add(MakeColumn("SpcKey", "SPC", 90, 60))
        riskGrid.Columns.Add(MakeColumn("RecordCount", "Kayıt", 55, 40))
        riskGrid.Columns.Add(MakeColumn("NokCount", "NOK", 55, 40))
        riskGrid.Columns.Add(MakeColumn("InvalidCount", "Hatalı", 55, 40))
        riskGrid.Columns.Add(MakeColumn("Cp", "Cp", 58, 42))
        riskGrid.Columns.Add(MakeColumn("Cpk", "Cpk", 58, 42))
        riskGrid.Columns.Add(MakeColumn("LastValue", "Son Değer", 85, 60))
        riskGrid.Columns.Add(MakeColumn("LastResult", "Sonuç", 70, 50))
        riskGrid.Columns.Add(MakeColumn("Reason", "Risk Nedeni", 280, 200))

        ConfigureRiskNumericColumn("Cp", "0.##")
        ConfigureRiskNumericColumn("Cpk", "0.##")
        ConfigureRiskNumericColumn("LastValue", "0.###")

        Dim keyColumn = MakeColumn("SeriesKey", "SeriesKey", 1, 1)
        keyColumn.Visible = False
        riskGrid.Columns.Add(keyColumn)

        AddHandler riskGrid.CellFormatting, AddressOf RiskGrid_CellFormatting
        AddHandler riskGrid.CellClick, AddressOf RiskGrid_CellClick
        AddHandler riskGrid.CellDoubleClick, AddressOf RiskGrid_CellDoubleClick
        riskGrid.Cursor = Cursors.Hand
    End Sub

    Private Sub ConfigureRiskNumericColumn(columnName As String, formatText As String)
        If Not riskGrid.Columns.Contains(columnName) Then Return
        Dim column = riskGrid.Columns(columnName)
        column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight
        column.DefaultCellStyle.Format = formatText
        column.DefaultCellStyle.FormatProvider = CultureInfo.GetCultureInfo("tr-TR")
        column.DefaultCellStyle.NullValue = "-"
    End Sub

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

        grid.Columns.Add(MakeColumn("MeasurementDate", "Tarih / Saat", 135, 90))
        grid.Columns.Add(MakeColumn("EyeNo", "Göz", 45, 35))
        grid.Columns.Add(MakeColumn("MeasuredValue", "Değer", 80, 55))
        grid.Columns.Add(MakeColumn("Result", "Sonuç", 75, 55))
        grid.Columns.Add(MakeColumn("Limits", "Limit", 120, 80))
        grid.Columns.Add(MakeColumn("MeasureId", "Ölçü No", 95, 65))
        grid.Columns.Add(MakeColumn("MeasureVersion", "V.", 45, 30))
        grid.Columns.Add(MakeColumn("OperatorName", "Ölçen", 105, 70))
        grid.Columns.Add(MakeColumn("LotNo", "İş Emri", 95, 65))
        grid.Columns.Add(MakeColumn("SerialNo", "Seri", 95, 65))
        grid.Columns.Add(MakeColumn("RecordId", "Kayıt No", 155, 95))
        grid.Columns.Add(MakeColumn("Note", "Not", 160, 120))

        AddHandler grid.CellFormatting, AddressOf Grid_CellFormatting
    End Sub

    Private Shared Function MakeColumn(name As String, header As String, width As Integer, fillWeight As Single) As DataGridViewTextBoxColumn
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

    Private Sub LoadDashboardData()
        isLoading = True
        Try
            allRows = DataService.GetMeasurementRows().
                Where(Function(row) Not String.IsNullOrWhiteSpace(DataService.GetValue(row, "TrCode")) AndAlso
                                    Not String.IsNullOrWhiteSpace(DataService.GetValue(row, "MeasureId"))).
                ToList()

            BuildSeries()
        Finally
            isLoading = False
        End Try

        RefreshSeriesList()
    End Sub

    Private Sub BuildSeries()
        allSeries = allRows.
            GroupBy(Function(row) BuildSeriesKey(row)).
            Select(Function(group)
                       Dim orderedRows = group.
                           OrderByDescending(Function(row) ParseDateSafe(DataService.GetValue(row, "MeasurementDate"))).
                           ToList()
                       Dim latest = orderedRows.First()
                       Dim spcKey = DataService.GetValue(latest, "SpcKey").Trim()
                       If spcKey = "" Then spcKey = DataService.GetValue(latest, "MeasureId").Trim()

                       Return New SpcSeries With {
                           .Key = group.Key,
                           .TrCode = DataService.GetValue(latest, "TrCode"),
                           .DrawingRev = DataService.GetValue(latest, "DrawingRev"),
                           .DrawingScope = ProductInfo.NormalizeDrawingScope(DataService.GetValue(latest, "DrawingScope")),
                           .SpcKey = spcKey,
                           .MeasureName = FirstNonEmpty(orderedRows, "MeasureName"),
                           .LatestMeasureId = DataService.GetValue(latest, "MeasureId"),
                           .Rows = orderedRows
                       }
                   End Function).
            OrderBy(Function(series) series.TrCode).
            ThenBy(Function(series) series.DrawingRev).
            ThenBy(Function(series) series.MeasureName).
            ThenBy(Function(series) series.SpcKey).
            ToList()
    End Sub

    Private Shared Function BuildSeriesKey(row As Dictionary(Of String, String)) As String
        Dim spcKey = DataService.GetValue(row, "SpcKey").Trim()
        If spcKey = "" Then spcKey = DataService.GetValue(row, "MeasureId").Trim()
        Return DataService.GetValue(row, "TrCode").Trim().ToUpperInvariant() & "|" &
               DataService.GetValue(row, "DrawingRev").Trim().ToUpperInvariant() & "|" &
               ProductInfo.NormalizeDrawingScope(DataService.GetValue(row, "DrawingScope")).ToUpperInvariant() & "|" &
               spcKey.ToUpperInvariant()
    End Function

    Private Shared Function FirstNonEmpty(rows As List(Of Dictionary(Of String, String)), key As String) As String
        For Each row In rows
            Dim value = DataService.GetValue(row, key).Trim()
            If value <> "" Then Return value
        Next
        Return ""
    End Function

    Private Sub RefreshSeriesList()
        Dim previousKey = ""
        Dim selected = TryCast(cboSeries.SelectedItem, SpcSeries)
        If selected IsNot Nothing Then previousKey = selected.Key

        Dim filtered = GetSearchFilteredSeries()

        isLoading = True
        Try
            cboSeries.Items.Clear()
            For Each series In filtered
                cboSeries.Items.Add(series)
            Next

            If cboSeries.Items.Count > 0 Then
                Dim indexToSelect = 0
                If previousKey <> "" Then
                    For i As Integer = 0 To cboSeries.Items.Count - 1
                        Dim candidate = TryCast(cboSeries.Items(i), SpcSeries)
                        If candidate IsNot Nothing AndAlso String.Equals(candidate.Key, previousKey, StringComparison.OrdinalIgnoreCase) Then
                            indexToSelect = i
                            Exit For
                        End If
                    Next
                End If
                cboSeries.SelectedIndex = indexToSelect
            End If
        Finally
            isLoading = False
        End Try

        RefreshEyeFilter()
        RefreshRiskList()
        RefreshDashboard()
    End Sub

    Private Function GetSearchFilteredSeries() As List(Of SpcSeries)
        Dim filterText = txtSearch.Text.Trim().ToUpperInvariant()
        If filterText = "" Then Return allSeries.ToList()
        Return allSeries.Where(Function(series) series.SearchText.Contains(filterText)).ToList()
    End Function

    Private Sub RefreshEyeFilter()
        Dim previous = If(TryCast(cboEye.SelectedItem, String), "TÜMÜ")
        Dim selected = TryCast(cboSeries.SelectedItem, SpcSeries)

        isLoading = True
        Try
            cboEye.Items.Clear()
            cboEye.Items.Add("TÜMÜ")
            If selected IsNot Nothing Then
                Dim eyes = selected.Rows.
                    Select(Function(row) ParseIntSafe(DataService.GetValue(row, "EyeNo"))).
                    Where(Function(value) value > 0).
                    Distinct().
                    OrderBy(Function(value) value).
                    ToList()
                For Each eye In eyes
                    cboEye.Items.Add(eye.ToString())
                Next
            End If

            Dim selectedIndex = 0
            For i As Integer = 0 To cboEye.Items.Count - 1
                If String.Equals(Convert.ToString(cboEye.Items(i)), previous, StringComparison.OrdinalIgnoreCase) Then
                    selectedIndex = i
                    Exit For
                End If
            Next
            cboEye.SelectedIndex = selectedIndex
        Finally
            isLoading = False
        End Try
    End Sub

    Private Sub RefreshRiskList()
        Dim table As New DataTable()
        table.Locale = CultureInfo.GetCultureInfo("tr-TR")
        table.CaseSensitive = False
        For Each col In {"RiskLevel", "TrCode", "DrawingRev", "MeasureName", "SpcKey"}
            table.Columns.Add(col, GetType(String))
        Next
        table.Columns.Add("RecordCount", GetType(Integer))
        table.Columns.Add("NokCount", GetType(Integer))
        table.Columns.Add("InvalidCount", GetType(Integer))
        table.Columns.Add("Cp", GetType(Double))
        table.Columns.Add("Cpk", GetType(Double))
        table.Columns.Add("LastValue", GetType(Decimal))
        For Each col In {"LastResult", "Reason", "SeriesKey"}
            table.Columns.Add(col, GetType(String))
        Next
        table.Columns.Add("RiskScore", GetType(Integer))
        table.Columns.Add("CpkSort", GetType(Double))

        Dim summaries = GetSearchFilteredSeries().
            Select(Function(series) BuildRiskSummary(series)).
            Where(Function(summary) summary IsNot Nothing AndAlso summary.RiskScore > 0).
            OrderBy(Function(summary) If(summary.Cpk.HasValue, summary.Cpk.Value, Double.MaxValue)).
            ThenByDescending(Function(summary) summary.RiskScore).
            ThenByDescending(Function(summary) summary.NokCount).
            ThenBy(Function(summary) summary.TrCode).
            Take(50).
            ToList()

        For Each summary In summaries
            table.Rows.Add(
                summary.RiskLevel,
                summary.TrCode,
                summary.DrawingRev,
                summary.MeasureName,
                summary.SpcKey,
                summary.RecordCount,
                summary.NokCount,
                summary.InvalidCount,
                If(summary.Cp.HasValue, CType(summary.Cp.Value, Object), DBNull.Value),
                If(summary.Cpk.HasValue, CType(summary.Cpk.Value, Object), DBNull.Value),
                summary.LastValue,
                summary.LastResult,
                summary.Reason,
                summary.SeriesKey,
                summary.RiskScore,
                If(summary.Cpk.HasValue, summary.Cpk.Value, Double.MaxValue))
        Next

        Dim view As New DataView(table) With {
            .Sort = "CpkSort ASC, RiskScore DESC, NokCount DESC, TrCode ASC, DrawingRev ASC, MeasureName ASC"
        }
        riskGrid.DataSource = view
        If riskGrid.Columns.Contains("Cpk") Then
            riskGrid.Columns("Cpk").HeaderCell.SortGlyphDirection = SortOrder.Ascending
        End If
    End Sub

    Private Function BuildRiskSummary(series As SpcSeries) As RiskSummary
        Dim points = BuildPointsForSeries(series, "TÜMÜ").
            OrderBy(Function(point) point.DateValue).
            ThenBy(Function(point) point.EyeNo).
            ToList()

        If points.Count = 0 Then Return Nothing

        Dim values = points.Select(Function(point) point.Value).ToList()
        Dim lower = LastNullable(points.Select(Function(point) point.LowerLimit))
        Dim upper = LastNullable(points.Select(Function(point) point.UpperLimit))
        Dim lastPoint = points.Last()
        Dim nokCount = points.Where(Function(point) String.Equals(point.Result, "NOK", StringComparison.OrdinalIgnoreCase)).Count()
        Dim invalidCount = points.Where(Function(point) String.Equals(point.Result, "HATALI", StringComparison.OrdinalIgnoreCase)).Count()
        Dim stdDev = StandardDeviation(values)
        Dim cp = CalculateCp(lower, upper, stdDev)
        Dim cpk = CalculateCpk(values, lower, upper, stdDev)
        Dim reasons As New List(Of String)()
        Dim score As Integer = 0

        If nokCount > 0 Then
            score += Math.Min(240, nokCount * 80)
            reasons.Add("NOK: " & nokCount.ToString())
        End If

        If invalidCount > 0 Then
            score += Math.Min(120, invalidCount * 50)
            reasons.Add("Hatalı veri: " & invalidCount.ToString())
        End If

        If String.Equals(lastPoint.Result, "NOK", StringComparison.OrdinalIgnoreCase) Then
            score += 80
            reasons.Add("son ölçüm NOK")
        ElseIf String.Equals(lastPoint.Result, "HATALI", StringComparison.OrdinalIgnoreCase) Then
            score += 45
            reasons.Add("son ölçüm hatalı")
        End If

        If cpk.HasValue Then
            If cpk.Value < 0.67R Then
                score += 110
                reasons.Add("Cpk çok düşük")
            ElseIf cpk.Value < 1.0R Then
                score += 75
                reasons.Add("Cpk düşük")
            ElseIf cpk.Value < 1.33R Then
                score += 35
                reasons.Add("Cpk sınırda")
            End If
        End If

        If lower.HasValue AndAlso upper.HasValue AndAlso upper.Value > lower.Value Then
            Dim toleranceRange = CDbl(upper.Value - lower.Value)
            Dim lastValue = CDbl(lastPoint.Value)
            Dim lowerValue = CDbl(lower.Value)
            Dim upperValue = CDbl(upper.Value)

            If lastValue < lowerValue OrElse lastValue > upperValue Then
                score += 100
                reasons.Add("son değer tolerans dışı")
            Else
                Dim nearestDistance = Math.Min(Math.Abs(lastValue - lowerValue), Math.Abs(upperValue - lastValue))
                Dim distanceRatio = nearestDistance / toleranceRange
                If distanceRatio <= 0.05R Then
                    score += 80
                    reasons.Add("son değer limite çok yakın")
                ElseIf distanceRatio <= 0.1R Then
                    score += 45
                    reasons.Add("son değer limite yakın")
                End If
            End If

            If stdDev > 0.0R Then
                Dim spreadRatio = stdDev / toleranceRange
                If spreadRatio >= 0.3R Then
                    score += 70
                    reasons.Add("yayılım çok yüksek")
                ElseIf spreadRatio >= 0.2R Then
                    score += 35
                    reasons.Add("yayılım yüksek")
                End If
            End If
        End If

        If HasRecentMonotonicTrend(points) Then
            score += 25
            reasons.Add("son ölçümlerde trend var")
        End If

        If points.Count < MinimumSpcInterpretationCount Then
            reasons.Add("veri yetersiz (" & points.Count.ToString() & "/" & MinimumSpcInterpretationCount.ToString() & ")")
        End If

        If score <= 0 Then Return Nothing

        Dim level = If(score >= 160, "YÜKSEK", If(score >= 80, "ORTA", "DÜŞÜK"))
        Dim reasonText = String.Join(", ", reasons.Distinct(StringComparer.OrdinalIgnoreCase))
        If reasonText = "" Then reasonText = "İzlenmeli"

        Return New RiskSummary With {
            .SeriesKey = series.Key,
            .RiskLevel = level,
            .RiskScore = score,
            .TrCode = series.TrCode,
            .DrawingRev = series.DrawingRev,
            .MeasureName = If(String.IsNullOrWhiteSpace(series.MeasureName), series.LatestMeasureId, series.MeasureName),
            .SpcKey = series.SpcKey,
            .RecordCount = points.Count,
            .NokCount = nokCount,
            .InvalidCount = invalidCount,
            .Cp = cp,
            .Cpk = cpk,
            .LastValue = lastPoint.Value,
            .LastResult = lastPoint.Result,
            .Reason = reasonText
        }
    End Function

    Private Sub RefreshDashboard()
        Dim selected = TryCast(cboSeries.SelectedItem, SpcSeries)
        If selected Is Nothing Then
            btnDetails.Enabled = False
            btnAnalysis.Enabled = False
            btnCorrectLimits.Enabled = False
            SetInfoWarningStyle(False)
            lblInfo.Text = "SPC ölçüm serisi bulunamadı."
            SetEmptyStats()
            chart.SetData(New List(Of SpcPoint)(), Nothing, Nothing, Nothing)
            grid.DataSource = New DataTable()
            Return
        End If

        Dim points = BuildFilteredPoints(selected)
        points = points.OrderBy(Function(point) point.DateValue).
                        ThenBy(Function(point) point.EyeNo).
                        ThenBy(Function(point) DataService.GetValue(point.SourceRow, "RecordId")).
                        ToList()

        Dim lower = LastNullable(points.Select(Function(point) point.LowerLimit))
        Dim upper = LastNullable(points.Select(Function(point) point.UpperLimit))
        Dim nominal = LastNullable(points.Select(Function(point) point.Nominal))

        UpdateStats(points, lower, upper)
        chart.SetData(points, lower, upper, nominal)
        btnDetails.Enabled = points.Count > 0
        btnAnalysis.Enabled = points.Count > 1
        btnCorrectLimits.Enabled = AppState.IsAdmin AndAlso points.Count > 0

        Dim dateText = ""
        If points.Count > 0 Then
            dateText = "   |   İlk: " & points.First().DateValue.ToString("dd.MM.yyyy HH:mm") &
                       "   |   Son: " & points.Last().DateValue.ToString("dd.MM.yyyy HH:mm")
        End If
        Dim dataWarning = BuildDataSufficiencyWarning(points.Count)
        If dataWarning <> "" Then
            dateText &= "   |   UYARI: " & dataWarning
        End If

        lblInfo.Text = selected.DisplayText & dateText
        SetInfoWarningStyle(dataWarning <> "")
    End Sub

    Private Sub SetInfoWarningStyle(hasWarning As Boolean)
        If hasWarning Then
            lblInfo.ForeColor = Color.FromArgb(130, 82, 0)
            lblInfo.BackColor = Color.FromArgb(255, 248, 225)
        Else
            lblInfo.ForeColor = Color.FromArgb(42, 70, 105)
            lblInfo.BackColor = Color.FromArgb(239, 246, 255)
        End If
    End Sub

    Private Function BuildFilteredPoints(series As SpcSeries) As List(Of SpcPoint)
        Dim eyeFilter = If(TryCast(cboEye.SelectedItem, String), "TÜMÜ")
        Return BuildPointsForSeries(series, eyeFilter)
    End Function

    Private Function BuildPointsForSeries(series As SpcSeries, eyeFilter As String) As List(Of SpcPoint)
        Dim result As New List(Of SpcPoint)()

        For Each row In series.Rows
            Dim parsedDate = ParseDateSafe(DataService.GetValue(row, "MeasurementDate"))
            If parsedDate = DateTime.MinValue Then Continue For

            If dtFrom.Checked AndAlso parsedDate.Date < dtFrom.Value.Date Then Continue For
            If dtTo.Checked AndAlso parsedDate.Date > dtTo.Value.Date Then Continue For

            Dim eye = ParseIntSafe(DataService.GetValue(row, "EyeNo"))
            If eyeFilter <> "TÜMÜ" AndAlso Not String.Equals(eye.ToString(), eyeFilter, StringComparison.OrdinalIgnoreCase) Then Continue For

            Dim measured As Decimal = 0D
            If Not NumberUtil.TryParseDecimal(DataService.GetValue(row, "MeasuredValue"), measured) Then Continue For

            Dim point As New SpcPoint With {
                .SourceRow = row,
                .DateValue = parsedDate,
                .EyeNo = eye,
                .Value = measured,
                .Result = DataService.GetValue(row, "Result"),
                .LowerLimit = ParseNullableDecimal(DataService.GetValue(row, "LowerLimit")),
                .UpperLimit = ParseNullableDecimal(DataService.GetValue(row, "UpperLimit")),
                .Nominal = ParseNullableDecimal(DataService.GetValue(row, "Nominal"))
            }
            result.Add(point)
        Next

        Return result
    End Function

    Private Shared Function ParseNullableDecimal(text As String) As Decimal?
        If String.IsNullOrWhiteSpace(text) Then Return Nothing
        Dim value As Decimal = 0D
        If NumberUtil.TryParseDecimal(text, value) Then Return value
        Return Nothing
    End Function

    Private Shared Function LastNullable(values As IEnumerable(Of Decimal?)) As Decimal?
        For Each value In values.Reverse()
            If value.HasValue Then Return value.Value
        Next
        Return Nothing
    End Function

    Private Sub UpdateStats(points As List(Of SpcPoint), lower As Decimal?, upper As Decimal?)
        Dim total = points.Count
        Dim okCount = points.Where(Function(point) String.Equals(point.Result, "OK", StringComparison.OrdinalIgnoreCase)).Count()
        Dim nokCount = points.Where(Function(point) String.Equals(point.Result, "NOK", StringComparison.OrdinalIgnoreCase)).Count()
        Dim invalidCount = points.Where(Function(point) String.Equals(point.Result, "HATALI", StringComparison.OrdinalIgnoreCase)).Count()

        lblTotalValue.Text = total.ToString()
        lblOkValue.Text = okCount.ToString()
        lblNokValue.Text = nokCount.ToString() & " / " & invalidCount.ToString()

        If total = 0 Then
            lblAverageValue.Text = "-"
            lblMinMaxValue.Text = "-"
            lblStdDevValue.Text = "-"
            lblCapabilityValue.Text = "-"
            SetCapabilityToolTip("Cp/Cpk yorumu için seçili filtrelerde geçerli ölçüm bulunamadı.")
            Return
        End If

        Dim values = points.Select(Function(point) point.Value).ToList()
        Dim average = values.Average()
        Dim minValue = values.Min()
        Dim maxValue = values.Max()
        Dim stdDev = StandardDeviation(values)

        lblAverageValue.Text = FormatDecimal(average)
        lblMinMaxValue.Text = FormatDecimal(minValue) & " / " & FormatDecimal(maxValue)
        lblStdDevValue.Text = If(stdDev <= 0.0R, "-", stdDev.ToString("0.###", CultureInfo.GetCultureInfo("tr-TR")))

        Dim dataWarning = BuildDataSufficiencyWarning(total)

        If lower.HasValue AndAlso upper.HasValue AndAlso stdDev > 0.0R Then
            Dim mean = CDbl(average)
            Dim lsl = CDbl(lower.Value)
            Dim usl = CDbl(upper.Value)
            Dim cp = (usl - lsl) / (6.0R * stdDev)
            Dim cpk = Math.Min((usl - mean) / (3.0R * stdDev), (mean - lsl) / (3.0R * stdDev))
            lblCapabilityValue.Text = If(dataWarning <> "", "⚠ ", "") &
                                      cp.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR")) &
                                      " / " &
                                      cpk.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR"))
            SetCapabilityToolTip(BuildCapabilityInterpretation(cp, cpk, total))
        Else
            lblCapabilityValue.Text = "-"
            Dim reason = If(Not lower.HasValue OrElse Not upper.HasValue,
                            "Cp/Cpk hesaplanamadı: alt veya üst tolerans limiti bulunmuyor.",
                            "Cp/Cpk hesaplanamadı: standart sapma için yeterli değişkenlik/veri bulunmuyor.")
            If dataWarning <> "" Then reason &= Environment.NewLine & dataWarning
            SetCapabilityToolTip(reason)
        End If
    End Sub

    Private Sub SetEmptyStats()
        For Each label In {lblTotalValue, lblOkValue, lblNokValue, lblAverageValue, lblMinMaxValue, lblStdDevValue, lblCapabilityValue}
            label.Text = "-"
        Next
        SetCapabilityToolTip("Cp/Cpk yorumu için önce bir ölçü serisi seçin.")
    End Sub

    Private Sub SetCapabilityToolTip(textValue As String)
        Dim card = TryCast(lblCapabilityValue.Parent, Control)
        If card Is Nothing Then
            kpiToolTip.SetToolTip(lblCapabilityValue, textValue)
        Else
            SetToolTipRecursive(card, textValue)
        End If
    End Sub

    Private Shared Function BuildCapabilityInterpretation(cp As Double, cpk As Double, sampleCount As Integer) As String
        Dim cpComment = CapabilityLevel(cp)
        Dim cpkComment = CapabilityLevel(cpk)
        Dim processComment As String

        If cpk < 0.0R Then
            processComment = "Negatif Cpk, proses ortalamasının tolerans sınırlarından en az birini aştığını gösterir."
        ElseIf cp >= 1.33R AndAlso cpk < 1.0R Then
            processComment = "Yayılım potansiyel olarak yeterli olsa da proses tolerans merkezinde değildir."
        ElseIf cp - cpk >= 0.25R Then
            processComment = "Cp ile Cpk arasındaki fark, prosesin tolerans merkezinden kaydığını gösterir."
        Else
            processComment = "Cp ve Cpk birbirine yakınsa proses tolerans aralığında görece iyi merkezlenmiştir."
        End If

        Dim sampleComment = If(sampleCount < 25,
                               Environment.NewLine & "Not: " & sampleCount.ToString() & " kayıt ön değerlendirme için az olabilir; sonuç daha fazla veriyle doğrulanmalıdır.",
                               "")

        Return "Cp " & cp.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR")) & ": " & cpComment & Environment.NewLine &
               "Cpk " & cpk.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR")) & ": " & cpkComment & Environment.NewLine &
               processComment & Environment.NewLine &
               "Genel eşik: ≥ 1,33 yeterli; 1,00–1,33 sınırda; < 1,00 yetersiz." & sampleComment
    End Function

    Private Shared Function CapabilityLevel(value As Double) As String
        If value >= 1.67R Then Return "çok yeterli"
        If value >= 1.33R Then Return "yeterli"
        If value >= 1.0R Then Return "sınırda"
        Return "yetersiz"
    End Function

    Private Shared Function BuildDataSufficiencyWarning(sampleCount As Integer) As String
        If sampleCount <= 0 OrElse sampleCount >= MinimumSpcInterpretationCount Then Return ""
        Return "Veri yetersiz: " & sampleCount.ToString() & "/" & MinimumSpcInterpretationCount.ToString() & " kayıt. Cp/Cpk ve SPC yorumu ön değerlendirme niteliğindedir."
    End Function

    Private Sub BindGrid(points As List(Of SpcPoint))
        Dim table As New DataTable()
        For Each col In {"MeasurementDate", "EyeNo", "MeasuredValue", "Result", "Limits", "MeasureId", "MeasureVersion", "OperatorName", "LotNo", "SerialNo", "RecordId", "Note"}
            table.Columns.Add(col)
        Next

        For Each point In points.OrderByDescending(Function(p) p.DateValue).ThenBy(Function(p) p.EyeNo)
            Dim row = point.SourceRow
            table.Rows.Add(
                point.DateValue.ToString("dd.MM.yyyy HH:mm"),
                DataService.GetValue(row, "EyeNo"),
                DataService.GetValue(row, "MeasuredValue"),
                DataService.GetValue(row, "Result"),
                DataService.GetValue(row, "LowerLimit") & " - " & DataService.GetValue(row, "UpperLimit"),
                DataService.GetValue(row, "MeasureId"),
                If(String.IsNullOrWhiteSpace(DataService.GetValue(row, "MeasureVersion")), "1", DataService.GetValue(row, "MeasureVersion")),
                DataService.GetValue(row, "OperatorName"),
                DataService.GetValue(row, "LotNo"),
                DataService.GetValue(row, "SerialNo"),
                DataService.GetValue(row, "RecordId"),
                DataService.GetValue(row, "Note"))
        Next

        grid.DataSource = table
    End Sub

    Private Sub Grid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse e.RowIndex >= grid.Rows.Count OrElse Not grid.Columns.Contains("Result") Then Return

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

    Private Sub RiskGrid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse e.RowIndex >= riskGrid.Rows.Count OrElse Not riskGrid.Columns.Contains("RiskLevel") Then Return

        Dim riskText = Convert.ToString(riskGrid.Rows(e.RowIndex).Cells("RiskLevel").Value)
        If String.Equals(riskText, "YÜKSEK", StringComparison.OrdinalIgnoreCase) Then
            riskGrid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.MistyRose
            riskGrid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.DarkRed
        ElseIf String.Equals(riskText, "ORTA", StringComparison.OrdinalIgnoreCase) Then
            riskGrid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.LemonChiffon
            riskGrid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.FromArgb(112, 71, 0)
        ElseIf String.Equals(riskText, "DÜŞÜK", StringComparison.OrdinalIgnoreCase) Then
            riskGrid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.FromArgb(245, 250, 255)
            riskGrid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.FromArgb(31, 71, 126)
        End If
    End Sub

    Private Sub RiskGrid_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 OrElse e.RowIndex >= riskGrid.Rows.Count OrElse Not riskGrid.Columns.Contains("SeriesKey") Then Return

        Dim seriesKey = Convert.ToString(riskGrid.Rows(e.RowIndex).Cells("SeriesKey").Value)
        If String.IsNullOrWhiteSpace(seriesKey) Then Return

        Dim selected = SelectSeriesByKey(seriesKey)
        If Not selected Then
            txtSearch.Clear()
            RefreshSeriesList()
            selected = SelectSeriesByKey(seriesKey)
        End If

        If selected Then OpenSelectedDetails()
    End Sub

    Private Sub RiskGrid_CellClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 OrElse e.RowIndex >= riskGrid.Rows.Count OrElse Not riskGrid.Columns.Contains("SeriesKey") Then Return

        Dim seriesKey = Convert.ToString(riskGrid.Rows(e.RowIndex).Cells("SeriesKey").Value).Trim()
        If seriesKey = "" Then Return

        If Not SelectSeriesByKey(seriesKey) Then
            ' Liste ve seri filtresi normalde aynıdır; dışarıdan yenilenen bir satır varsa
            ' arama filtresini kaldırıp ilgili seriyi yeniden seçmeyi deneriz.
            txtSearch.Clear()
            RefreshSeriesList()
            SelectSeriesByKey(seriesKey)
        End If
    End Sub

    Private Sub OpenSelectedAnalysis()
        Dim selected = TryCast(cboSeries.SelectedItem, SpcSeries)
        If selected Is Nothing Then Return

        ' X̄-R analizi aynı kayıt içindeki bütün gözlere ihtiyaç duyar; tarih filtresi korunur.
        Dim points = BuildPointsForSeries(selected, "TÜMÜ").
            OrderBy(Function(point) point.DateValue).
            ThenBy(Function(point) point.EyeNo).
            ToList()
        If points.Count < 2 Then
            MessageBox.Show("SPC analizi için en az iki geçerli ölçüm kaydı gereklidir.", "SPC Analizi", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim analysisRows = points.Select(Function(point) point.SourceRow).ToList()
        Using frm As New FrmSpcAnalysis(selected.DisplayText, analysisRows)
            frm.ShowDialog(Me)
        End Using
    End Sub

    Private Sub CorrectSelectedHistoricalLimits(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.CanEditSpcDashboard, "SPC Geçmiş Limit Düzeltme")

            Dim selected = TryCast(cboSeries.SelectedItem, SpcSeries)
            If selected Is Nothing Then
                MessageBox.Show("Önce düzeltilecek SPC ölçü serisini seçin.",
                                "SPC serisi seçilmedi",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information)
                Return
            End If

            Dim activePoint = DataService.GetControlPoints(
                selected.TrCode,
                selected.DrawingRev,
                True,
                selected.DrawingScope).
                FirstOrDefault(
                    Function(point)
                        Dim pointSpcKey = If(String.IsNullOrWhiteSpace(point.SpcKey), point.MeasureId, point.SpcKey).Trim()
                        Return String.Equals(pointSpcKey, selected.SpcKey, StringComparison.OrdinalIgnoreCase)
                    End Function)

            If activePoint Is Nothing Then
                MessageBox.Show(
                    "Bu SPC serisi için aktif kontrol ölçüsü bulunamadı." & Environment.NewLine & Environment.NewLine &
                    "Önce Kontrol Ölçüleri penceresinde ilgili ölçüyü seçip 'Ölçüyü Revize Et' ile doğru nominal ve toleransları kaydedin.",
                    "Aktif limit bulunamadı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning)
                Return
            End If

            Dim points = BuildFilteredPoints(selected)
            If points.Count = 0 Then
                MessageBox.Show("Seçili tarih filtrelerinde düzeltilecek ölçüm bulunamadı.",
                                "Kayıt bulunamadı",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information)
                Return
            End If

            Dim dateFrom As DateTime? = Nothing
            Dim dateTo As DateTime? = Nothing
            If dtFrom.Checked Then dateFrom = dtFrom.Value.Date
            If dtTo.Checked Then dateTo = dtTo.Value.Date

            Dim reason = Microsoft.VisualBasic.Interaction.InputBox(
                "Geçmiş ölçüm limitleri neden düzeltiliyor?" & Environment.NewLine &
                "Örn: Teknik resimde tolerans yanlış tanımlanmıştı.",
                "SPC geçmiş limit düzeltme nedeni",
                "")
            reason = If(reason, "").Trim()
            If reason = "" Then
                MessageBox.Show("Düzeltme nedeni yazılmadığı için işlem yapılmadı.",
                                "İşlem iptal",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information)
                Return
            End If

            Dim dateScope = If(dateFrom.HasValue, dateFrom.Value.ToString("dd.MM.yyyy"), "İlk kayıt") &
                            " - " &
                            If(dateTo.HasValue, dateTo.Value.ToString("dd.MM.yyyy"), "Son kayıt")
            Dim answer = MessageBox.Show(
                "Ölçüm değerleri değiştirilmeyecek; yalnızca geçmiş limit kopyaları ve OK/NOK sonuçları yeniden hesaplanacak." & Environment.NewLine & Environment.NewLine &
                "TR: " & selected.TrCode & " / " & selected.DrawingRev & Environment.NewLine &
                "SPC: " & selected.SpcKey & Environment.NewLine &
                "Tarih aralığı: " & dateScope & Environment.NewLine &
                "Filtrede görülen ölçüm: " & points.Count.ToString() & Environment.NewLine & Environment.NewLine &
                "Yeni nominal: " & NumberUtil.DecToCsv(activePoint.Nominal) & Environment.NewLine &
                "Yeni alt limit: " & NumberUtil.DecToCsv(activePoint.LowerLimit) & Environment.NewLine &
                "Yeni üst limit: " & NumberUtil.DecToCsv(activePoint.UpperLimit) & Environment.NewLine & Environment.NewLine &
                "Devam edilsin mi?",
                "Geçmiş SPC limitlerini düzelt",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2)
            If answer <> DialogResult.Yes Then Return

            Dim correction = DataService.CorrectSpcHistoricalLimits(
                selected.TrCode,
                selected.DrawingRev,
                selected.DrawingScope,
                selected.SpcKey,
                dateFrom,
                dateTo,
                activePoint.Nominal,
                activePoint.LowerLimit,
                activePoint.UpperLimit,
                reason)

            Dim selectedKey = selected.Key
            LoadDashboardData()
            SelectSeriesByKey(selectedKey)

            MessageBox.Show(
                "SPC geçmiş limitleri güncellendi." & Environment.NewLine &
                "Güncellenen ölçüm satırı: " & correction.AffectedRows.ToString() & Environment.NewLine &
                "OK/NOK sonucu değişen: " & correction.ResultChangedRows.ToString() & Environment.NewLine &
                "Düzeltme No: " & correction.CorrectionId,
                "Limit düzeltmesi tamamlandı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information)
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "SPC limitleri düzeltilemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub OpenSelectedDetails()
        Dim selected = TryCast(cboSeries.SelectedItem, SpcSeries)
        If selected Is Nothing Then Return

        Dim points = BuildFilteredPoints(selected).
            OrderByDescending(Function(point) point.DateValue).
            ThenBy(Function(point) point.EyeNo).
            ToList()
        If points.Count = 0 Then
            MessageBox.Show("Seçili filtrelere uygun ölçüm kaydı bulunamadı.", "SPC Ölçüm Detayları", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim detailRows = points.Select(Function(point) point.SourceRow).ToList()
        Using frm As New FrmSpcMeasurementDetails(selected.DisplayText, detailRows)
            frm.ShowDialog(Me)
        End Using
    End Sub

    Private Function SelectSeriesByKey(seriesKey As String) As Boolean
        For i As Integer = 0 To cboSeries.Items.Count - 1
            Dim series = TryCast(cboSeries.Items(i), SpcSeries)
            If series IsNot Nothing AndAlso String.Equals(series.Key, seriesKey, StringComparison.OrdinalIgnoreCase) Then
                cboSeries.SelectedIndex = i
                RefreshEyeFilter()
                RefreshDashboard()
                Return True
            End If
        Next

        Return False
    End Function

    Private Shared Function CalculateCpk(values As List(Of Decimal), lower As Decimal?, upper As Decimal?, stdDev As Double) As Double?
        If values Is Nothing OrElse values.Count = 0 OrElse Not lower.HasValue OrElse Not upper.HasValue OrElse stdDev <= 0.0R Then
            Return Nothing
        End If

        Dim mean = CDbl(values.Average())
        Dim lsl = CDbl(lower.Value)
        Dim usl = CDbl(upper.Value)
        If usl <= lsl Then Return Nothing

        Return Math.Min((usl - mean) / (3.0R * stdDev), (mean - lsl) / (3.0R * stdDev))
    End Function

    Private Shared Function CalculateCp(lower As Decimal?, upper As Decimal?, stdDev As Double) As Double?
        If Not lower.HasValue OrElse Not upper.HasValue OrElse stdDev <= 0.0R Then Return Nothing

        Dim lsl = CDbl(lower.Value)
        Dim usl = CDbl(upper.Value)
        If usl <= lsl Then Return Nothing
        Return (usl - lsl) / (6.0R * stdDev)
    End Function

    Private Shared Function HasRecentMonotonicTrend(points As List(Of SpcPoint)) As Boolean
        If points Is Nothing OrElse points.Count < 5 Then Return False

        Dim recent = points.
            OrderBy(Function(point) point.DateValue).
            ThenBy(Function(point) point.EyeNo).
            TakeLast(5).
            Select(Function(point) point.Value).
            ToList()

        If recent.Count < 5 Then Return False

        Dim rising = True
        Dim falling = True
        For i As Integer = 1 To recent.Count - 1
            If recent(i) <= recent(i - 1) Then rising = False
            If recent(i) >= recent(i - 1) Then falling = False
        Next

        Return rising OrElse falling
    End Function

    Private Shared Function StandardDeviation(values As List(Of Decimal)) As Double
        If values Is Nothing OrElse values.Count <= 1 Then Return 0.0R

        Dim doubles = values.Select(Function(value) CDbl(value)).ToList()
        Dim mean = doubles.Average()
        Dim variance = doubles.Sum(Function(value) Math.Pow(value - mean, 2.0R)) / (doubles.Count - 1)
        Return Math.Sqrt(variance)
    End Function

    Private Shared Function FormatDecimal(value As Decimal) As String
        Return value.ToString("0.###", CultureInfo.GetCultureInfo("tr-TR"))
    End Function

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
