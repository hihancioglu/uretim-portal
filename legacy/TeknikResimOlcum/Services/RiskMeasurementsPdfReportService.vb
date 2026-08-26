Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Text
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Microsoft.Web.WebView2.Core
Imports Microsoft.Web.WebView2.WinForms

Public NotInheritable Class RiskMeasurementPdfRow
    Public Property RiskLevel As String = ""
    Public Property TrCode As String = ""
    Public Property DrawingRev As String = ""
    Public Property MeasureName As String = ""
    Public Property Cp As Double?
    Public Property Cpk As Double?
    Public Property RecordCount As Integer
    Public Property NokCount As Integer
    Public Property InvalidCount As Integer
    Public Property LastValue As Decimal?
    Public Property LastResult As String = ""
    Public Property Reason As String = ""
    Public Property LowerLimit As Decimal?
    Public Property UpperLimit As Decimal?
    Public Property Nominal As Decimal?
    Public Property ChartPoints As New List(Of RiskMeasurementPdfPoint)()
End Class

Public NotInheritable Class RiskMeasurementPdfPoint
    Public Property DateValue As DateTime
    Public Property EyeNo As Integer
    Public Property Value As Decimal
    Public Property Result As String = ""
End Class

Public NotInheritable Class RiskMeasurementPdfReportOptions
    Public Property FilterSummary As String = "Tümü"
    Public Property SortSummary As String = "Varsayılan risk sırası"
    Public Property GeneratedBy As String = ""
    Public Property ComputerName As String = ""
    Public Property Rows As New List(Of RiskMeasurementPdfRow)()
End Class

''' <summary>
''' SPC risk raporunu Chromium'un yazdırma motoruyla gerçek metin tabanlı PDF olarak üretir.
''' Metinler resme dönüştürülmediği için yakınlaştırıldığında net ve seçilebilir kalır.
''' </summary>
Public NotInheritable Class RiskMeasurementsPdfReportService
    Private Shared ReadOnly TurkishCulture As CultureInfo = CultureInfo.GetCultureInfo("tr-TR")

    Private Sub New()
    End Sub

    Public Shared Async Function CreateAsync(outputPath As String,
                                             options As RiskMeasurementPdfReportOptions,
                                             owner As IWin32Window) As Task
        If String.IsNullOrWhiteSpace(outputPath) Then Throw New ArgumentException("PDF dosya yolu boş olamaz.")
        If options Is Nothing Then Throw New ArgumentNullException(NameOf(options))
        If options.Rows Is Nothing OrElse options.Rows.Count = 0 Then
            Throw New InvalidOperationException("PDF raporuna eklenecek riskli ölçü bulunamadı.")
        End If

        Dim absolutePath = Path.GetFullPath(outputPath)
        Dim directoryPath = Path.GetDirectoryName(absolutePath)
        If Not String.IsNullOrWhiteSpace(directoryPath) Then Directory.CreateDirectory(directoryPath)

        Dim html = BuildHtml(options)
        Using host As New Form() With {
            .Text = "SPC PDF hazırlanıyor",
            .ShowInTaskbar = False,
            .FormBorderStyle = FormBorderStyle.FixedToolWindow,
            .StartPosition = FormStartPosition.Manual,
            .Location = New Drawing.Point(-32000, -32000),
            .Size = New Drawing.Size(1280, 900)
        },
              browser As New WebView2() With {.Dock = DockStyle.Fill}

            host.Controls.Add(browser)
            If owner Is Nothing Then
                host.Show()
            Else
                host.Show(owner)
            End If

            Dim userDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TeknikResimOlcum",
                "WebView2Pdf")
            Directory.CreateDirectory(userDataPath)

            Dim webEnvironment = Await CoreWebView2Environment.CreateAsync(Nothing, userDataPath)
            Await browser.EnsureCoreWebView2Async(webEnvironment)

            Dim navigationSource As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
            Dim navigationHandler As EventHandler(Of CoreWebView2NavigationCompletedEventArgs) =
                Sub(sender As Object, args As CoreWebView2NavigationCompletedEventArgs)
                    If args.IsSuccess Then
                        navigationSource.TrySetResult(True)
                    Else
                        navigationSource.TrySetException(
                            New InvalidOperationException("PDF rapor görünümü hazırlanamadı: " & args.WebErrorStatus.ToString()))
                    End If
                End Sub

            AddHandler browser.NavigationCompleted, navigationHandler
            Try
                browser.NavigateToString(html)
                Dim completedTask = Await Task.WhenAny(navigationSource.Task, Task.Delay(TimeSpan.FromSeconds(30)))
                If completedTask IsNot navigationSource.Task Then
                    Throw New TimeoutException("PDF rapor görünümü hazırlanırken zaman aşımı oluştu.")
                End If
                Await navigationSource.Task

                Dim printSettings = browser.CoreWebView2.Environment.CreatePrintSettings()
                printSettings.Orientation = CoreWebView2PrintOrientation.Landscape
                printSettings.PageWidth = 8.27R
                printSettings.PageHeight = 11.69R
                printSettings.MarginTop = 0
                printSettings.MarginBottom = 0
                printSettings.MarginLeft = 0
                printSettings.MarginRight = 0
                printSettings.ScaleFactor = 1.0R
                printSettings.ShouldPrintBackgrounds = True
                printSettings.ShouldPrintHeaderAndFooter = False

                Dim succeeded = Await browser.CoreWebView2.PrintToPdfAsync(absolutePath, printSettings)
                If Not succeeded OrElse Not File.Exists(absolutePath) Then
                    Throw New InvalidOperationException("PDF dosyası oluşturulamadı.")
                End If
            Finally
                RemoveHandler browser.NavigationCompleted, navigationHandler
                host.Close()
            End Try
        End Using
    End Function

    Private Shared Function BuildHtml(options As RiskMeasurementPdfReportOptions) As String
        Dim rows = options.Rows
        Dim pageCount = rows.Count + 1
        Dim highCount = rows.Where(Function(item) IsRisk(item.RiskLevel, "YÜKSEK")).Count()
        Dim mediumCount = rows.Where(Function(item) IsRisk(item.RiskLevel, "ORTA")).Count()
        Dim lowCount = rows.Where(Function(item) IsRisk(item.RiskLevel, "DÜŞÜK")).Count()
        Dim generatedAt = DateTime.Now

        Dim html As New StringBuilder(32768)
        html.AppendLine("<!doctype html>")
        html.AppendLine("<html lang='tr'><head><meta charset='utf-8'>")
        html.AppendLine("<title>SPC Riskli Ölçüler Raporu</title>")
        html.AppendLine("<style>")
        html.AppendLine("@page{size:A4 landscape;margin:8mm}")
        html.AppendLine("*{box-sizing:border-box}")
        html.AppendLine("html,body{margin:0;padding:0;background:#fff;color:#243247;font-family:'Segoe UI',Arial,sans-serif;font-size:8.5pt;-webkit-print-color-adjust:exact;print-color-adjust:exact}")
        html.AppendLine(".page{width:100%;min-height:190mm;position:relative;page-break-after:always;padding-bottom:8mm}")
        html.AppendLine(".page:last-child{page-break-after:auto}")
        html.AppendLine(".report-head{min-height:19mm;background:#1f4e79;color:#fff;padding:3.2mm 5mm;border-radius:1.2mm}")
        html.AppendLine(".report-title{font-size:17pt;font-weight:700;letter-spacing:.15pt;line-height:1.15;margin:0 0 2.1mm}")
        html.AppendLine(".meta{font-size:8.4pt;color:#edf4fc;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}")
        html.AppendLine(".filters{margin-top:2.5mm;padding:2.2mm 3.2mm;background:#eff6ff;border:1px solid #bed0e6;border-radius:1mm;color:#2a4669;line-height:1.45}")
        html.AppendLine(".filters strong{font-weight:650}")
        html.AppendLine(".cards{display:grid;grid-template-columns:repeat(4,1fr);gap:3mm;margin:2.5mm 0}")
        html.AppendLine(".card{height:13mm;display:flex;align-items:center;padding:0 3.5mm;border:1px solid #dce2eb;border-left:1.5mm solid var(--accent);background:#f8fafd;border-radius:.8mm}")
        html.AppendLine(".card-label{font-weight:650;color:#526074;margin-right:2.2mm}.card-value{font-size:12pt;font-weight:750;color:var(--accent)}")
        html.AppendLine(".cover-note{margin-top:5mm;padding:7mm;background:#f8fafd;border:1px solid #dce2eb;border-left:1.8mm solid #1f4e79;border-radius:1mm}")
        html.AppendLine(".cover-note h2{margin:0 0 2.5mm;color:#1f4e79;font-size:14pt}.cover-note p{margin:0;font-size:10pt;line-height:1.6;color:#45566e}")
        html.AppendLine(".legend{display:grid;grid-template-columns:repeat(3,1fr);gap:3mm;margin-top:5mm}")
        html.AppendLine(".legend-item{padding:4mm;border:1px solid #dce2eb;border-left:1.7mm solid var(--accent);background:var(--soft);border-radius:1mm}")
        html.AppendLine(".legend-title{font-size:11pt;font-weight:750;color:var(--accent);margin-bottom:1mm}.legend-text{color:#526074;line-height:1.4}")
        html.AppendLine(".risk-banner{margin-top:2.4mm;padding:2.7mm 4mm;border:1px solid var(--accent);border-left:2.2mm solid var(--accent);background:var(--soft);border-radius:1mm;display:flex;align-items:center;justify-content:space-between;gap:5mm}")
        html.AppendLine(".risk-banner-title{font-size:12.5pt;font-weight:750;color:#20334d;overflow-wrap:anywhere}.risk-badge{flex:0 0 auto;padding:1.7mm 4mm;border-radius:20mm;background:var(--accent);color:#fff;font-size:9pt;font-weight:750;letter-spacing:.25pt}")
        html.AppendLine(".section{margin-top:2.8mm;border:1px solid #d6dde7;border-radius:1mm;overflow:hidden;break-inside:avoid}")
        html.AppendLine(".section-title{padding:1.8mm 3mm;background:#eaf1fa;color:#1f4e79;font-size:9pt;font-weight:700;border-bottom:1px solid #d6dde7}")
        html.AppendLine(".identity-grid{display:grid;grid-template-columns:1.1fr .65fr 2.75fr}")
        html.AppendLine(".value-block{min-height:15mm;padding:2.3mm 3.2mm;border-right:1px solid #e1e6ed}.value-block:last-child{border-right:0}.value-label{font-size:7pt;font-weight:650;color:#6a7789;text-transform:uppercase;letter-spacing:.25pt;margin-bottom:1.2mm}.value-text{font-size:11pt;font-weight:700;color:#23364f;overflow-wrap:anywhere}")
        html.AppendLine(".metric-grid{display:grid;grid-template-columns:repeat(7,1fr);gap:2mm;margin-top:2.8mm}")
        html.AppendLine(".metric{min-height:18mm;padding:2.2mm;background:#f8fafd;border:1px solid #dce2eb;border-top:1.3mm solid var(--metric);border-radius:1mm}.metric-label{font-size:7pt;font-weight:650;color:#6a7789;text-transform:uppercase;letter-spacing:.2pt}.metric-value{margin-top:2mm;font-size:12pt;line-height:1;font-weight:750;color:var(--metric);font-variant-numeric:tabular-nums;overflow-wrap:anywhere}")
        html.AppendLine(".chart-box{height:48mm;padding:1.5mm 2mm 1mm;background:#fff}.chart-box svg{display:block;width:100%;height:100%;font-family:'Segoe UI',Arial,sans-serif}")
        html.AppendLine(".chart-empty{height:100%;display:flex;align-items:center;justify-content:center;color:#697588;font-size:10pt;background:#f8fafd}")
        html.AppendLine(".reason-box{min-height:19mm;max-height:25mm;padding:2.5mm 3mm;background:var(--soft);border-left:1.6mm solid var(--accent);font-size:9pt;line-height:1.4;color:#293c55;overflow:hidden;overflow-wrap:anywhere}")
        html.AppendLine(".footer{position:absolute;left:0;right:0;bottom:0;border-top:1px solid #d6dde7;padding-top:1.5mm;color:#697588;font-size:7.5pt;display:flex;justify-content:space-between}")
        html.AppendLine("</style></head><body>")

        AppendCoverPage(html, options, generatedAt, pageCount, highCount, mediumCount, lowCount)
        For index As Integer = 0 To rows.Count - 1
            AppendRiskPage(html, rows(index), index, rows.Count, options, generatedAt, pageCount)
        Next

        html.AppendLine("</body></html>")
        Return html.ToString()
    End Function

    Private Shared Sub AppendCoverPage(html As StringBuilder,
                                       options As RiskMeasurementPdfReportOptions,
                                       generatedAt As DateTime,
                                       pageCount As Integer,
                                       highCount As Integer,
                                       mediumCount As Integer,
                                       lowCount As Integer)
        html.AppendLine("<section class='page'>")
        AppendReportHeader(html, "SPC RİSKLİ ÖLÇÜLER RAPORU", options, generatedAt, 1, pageCount)
        html.AppendLine("<div class='filters'>")
        html.Append("<div><strong>Filtreler:</strong> ").Append(EncodeWithBreaks(EmptyAsDash(options.FilterSummary))).AppendLine("</div>")
        html.Append("<div><strong>Sıralama:</strong> ").Append(EncodeWithBreaks(EmptyAsDash(options.SortSummary))).AppendLine("</div>")
        html.AppendLine("</div>")
        html.AppendLine("<div class='cards'>")
        AppendSummaryCard(html, "TOPLAM", options.Rows.Count, "#1f4e79")
        AppendSummaryCard(html, "YÜKSEK", highCount, "#b22222")
        AppendSummaryCard(html, "ORTA", mediumCount, "#b97400")
        AppendSummaryCard(html, "DÜŞÜK", lowCount, "#1f649b")
        html.AppendLine("</div>")
        html.AppendLine("<div class='cover-note'><h2>Rapor Kapsamı</h2>")
        html.Append("<p>Bu PDF, aktif filtrelerde bulunan <strong>").Append(options.Rows.Count)
        html.AppendLine(" riskli ölçüyü içerir. Her riskli ölçü, takip eden sayfalarda bağımsız bir değerlendirme olarak sunulmuştur.</p></div>")
        html.AppendLine("<div class='legend'>")
        AppendLegend(html, "YÜKSEK RİSK", "Öncelikli inceleme ve aksiyon gerektirir.", "#b22222", "#ffe7e5")
        AppendLegend(html, "ORTA RİSK", "Yakın takip ve proses değerlendirmesi gerektirir.", "#b97400", "#fff8da")
        AppendLegend(html, "DÜŞÜK RİSK", "İzlenmeli ve eğilim değişimleri kontrol edilmelidir.", "#1f649b", "#eff6ff")
        html.AppendLine("</div>")
        AppendFooter(html, generatedAt, 1, pageCount)
        html.AppendLine("</section>")
    End Sub

    Private Shared Sub AppendRiskPage(html As StringBuilder,
                                      row As RiskMeasurementPdfRow,
                                      riskIndex As Integer,
                                      riskCount As Integer,
                                      options As RiskMeasurementPdfReportOptions,
                                      generatedAt As DateTime,
                                      pageCount As Integer)
        Dim accent = RiskAccent(row.RiskLevel)
        Dim softColor = RiskSoftColor(row.RiskLevel)
        Dim pageNumber = riskIndex + 2
        html.Append("<section class='page' style='--accent:").Append(accent).Append(";--soft:").Append(softColor).AppendLine("'>")
        AppendReportHeader(html, "SPC RİSKLİ ÖLÇÜ RAPORU", options, generatedAt, pageNumber, pageCount, (riskIndex + 1).ToString() & "/" & riskCount.ToString())

        html.AppendLine("<div class='risk-banner'>")
        html.Append("<div class='risk-banner-title'>").Append(Encode(EmptyAsDash(row.TrCode))).Append(" &nbsp;•&nbsp; ")
        html.Append(Encode(EmptyAsDash(row.MeasureName))).AppendLine("</div>")
        html.Append("<div class='risk-badge'>").Append(Encode(EmptyAsDash(row.RiskLevel))).AppendLine(" RİSK</div></div>")

        html.AppendLine("<div class='section'><div class='section-title'>Teknik Resim ve Ölçü Bilgisi</div><div class='identity-grid'>")
        AppendValueBlock(html, "TR Kodu", row.TrCode)
        AppendValueBlock(html, "Revizyon", row.DrawingRev)
        AppendValueBlock(html, "Ölçü", row.MeasureName)
        html.AppendLine("</div></div>")

        html.AppendLine("<div class='metric-grid'>")
        AppendMetric(html, "Cp", FormatNullable(row.Cp, "0.##"), accent)
        AppendMetric(html, "Cpk", FormatNullable(row.Cpk, "0.##"), accent)
        AppendMetric(html, "Kayıt", row.RecordCount.ToString(TurkishCulture), "#1f4e79")
        AppendMetric(html, "NOK", row.NokCount.ToString(TurkishCulture), If(row.NokCount > 0, "#b22222", "#18834b"))
        AppendMetric(html, "Hatalı", row.InvalidCount.ToString(TurkishCulture), If(row.InvalidCount > 0, "#b22222", "#18834b"))
        AppendMetric(html, "Son Değer", If(row.LastValue.HasValue, row.LastValue.Value.ToString("0.###", TurkishCulture), "-"), "#315d8f")
        AppendMetric(html, "Sonuç", row.LastResult, ResultAccent(row.LastResult))
        html.AppendLine("</div>")

        html.AppendLine("<div class='section'><div class='section-title'>Ölçüm Trendi • Aktif tarih filtresi • En fazla son 50 ölçüm</div>")
        AppendMeasurementChart(html, row)
        html.AppendLine("</div>")

        html.AppendLine("<div class='section'><div class='section-title'>Risk Değerlendirmesi</div>")
        html.Append("<div class='reason-box'>").Append(EncodeWithBreaks(EmptyAsDash(row.Reason))).AppendLine("</div></div>")
        AppendFooter(html, generatedAt, pageNumber, pageCount)
        html.AppendLine("</section>")
    End Sub

    Private Shared Sub AppendReportHeader(html As StringBuilder,
                                          title As String,
                                          options As RiskMeasurementPdfReportOptions,
                                          generatedAt As DateTime,
                                          pageNumber As Integer,
                                          pageCount As Integer,
                                          Optional riskCounter As String = "")
        html.AppendLine("<header class='report-head'>")
        html.Append("<div class='report-title'>").Append(Encode(title)).AppendLine("</div>")
        html.Append("<div class='meta'>Rapor: ").Append(Encode(generatedAt.ToString("dd.MM.yyyy HH:mm")))
        html.Append(" &nbsp; | &nbsp; Hazırlayan: ").Append(Encode(EmptyAsDash(options.GeneratedBy)))
        html.Append(" &nbsp; | &nbsp; Bilgisayar: ").Append(Encode(EmptyAsDash(options.ComputerName)))
        html.Append(" &nbsp; | &nbsp; Sayfa: ").Append(pageNumber).Append("/").Append(pageCount)
        If riskCounter <> "" Then html.Append(" &nbsp; | &nbsp; Risk kaydı: ").Append(Encode(riskCounter))
        html.AppendLine("</div>")
        html.AppendLine("</header>")
    End Sub

    Private Shared Sub AppendSummaryCard(html As StringBuilder, title As String, value As Integer, accent As String)
        html.Append("<div class='card' style='--accent:").Append(accent).Append("'><span class='card-label'>")
        html.Append(Encode(title)).Append("</span><span class='card-value'>").Append(value).AppendLine("</span></div>")
    End Sub

    Private Shared Sub AppendLegend(html As StringBuilder, title As String, description As String, accent As String, softColor As String)
        html.Append("<div class='legend-item' style='--accent:").Append(accent).Append(";--soft:").Append(softColor).Append("'>")
        html.Append("<div class='legend-title'>").Append(Encode(title)).Append("</div><div class='legend-text'>")
        html.Append(Encode(description)).AppendLine("</div></div>")
    End Sub

    Private Shared Sub AppendValueBlock(html As StringBuilder, label As String, value As String)
        html.Append("<div class='value-block'><div class='value-label'>").Append(Encode(label)).Append("</div><div class='value-text'>")
        html.Append(Encode(EmptyAsDash(value))).AppendLine("</div></div>")
    End Sub

    Private Shared Sub AppendMetric(html As StringBuilder, label As String, value As String, accent As String)
        html.Append("<div class='metric' style='--metric:").Append(accent).Append("'><div class='metric-label'>")
        html.Append(Encode(label)).Append("</div><div class='metric-value'>").Append(Encode(EmptyAsDash(value))).AppendLine("</div></div>")
    End Sub

    Private Shared Sub AppendMeasurementChart(html As StringBuilder, row As RiskMeasurementPdfRow)
        Dim points = If(row.ChartPoints, New List(Of RiskMeasurementPdfPoint)()).
            OrderBy(Function(point) point.DateValue).
            ThenBy(Function(point) point.EyeNo).
            TakeLast(50).
            ToList()

        html.AppendLine("<div class='chart-box'>")
        If points.Count = 0 Then
            html.AppendLine("<div class='chart-empty'>Grafik için uygun ölçüm kaydı bulunamadı.</div></div>")
            Return
        End If

        Const svgWidth As Double = 1200.0R
        Const svgHeight As Double = 250.0R
        Const plotLeft As Double = 72.0R
        Const plotTop As Double = 22.0R
        Const plotWidth As Double = 1098.0R
        Const plotHeight As Double = 164.0R
        Dim plotBottom = plotTop + plotHeight
        Dim plotRight = plotLeft + plotWidth

        Dim yValues As New List(Of Double)(points.Select(Function(point) CDbl(point.Value)))
        If row.LowerLimit.HasValue Then yValues.Add(CDbl(row.LowerLimit.Value))
        If row.UpperLimit.HasValue Then yValues.Add(CDbl(row.UpperLimit.Value))
        If row.Nominal.HasValue Then yValues.Add(CDbl(row.Nominal.Value))

        Dim minY = yValues.Min()
        Dim maxY = yValues.Max()
        If Math.Abs(maxY - minY) < 0.0000001R Then
            Dim pad = Math.Max(0.001R, Math.Abs(maxY) * 0.02R)
            minY -= pad
            maxY += pad
        Else
            Dim pad = (maxY - minY) * 0.12R
            minY -= pad
            maxY += pad
        End If

        html.Append("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 ").Append(SvgNumber(svgWidth)).Append(" ").Append(SvgNumber(svgHeight)).AppendLine("' role='img' aria-label='Ölçüm trend grafiği'>")
        html.Append("<rect x='").Append(SvgNumber(plotLeft)).Append("' y='").Append(SvgNumber(plotTop)).Append("' width='").Append(SvgNumber(plotWidth)).Append("' height='").Append(SvgNumber(plotHeight)).AppendLine("' fill='#ffffff' stroke='#aeb9c8' stroke-width='1'/>")

        For gridIndex As Integer = 0 To 4
            Dim y = plotBottom - (plotHeight * gridIndex / 4.0R)
            Dim labelValue = minY + ((maxY - minY) * gridIndex / 4.0R)
            html.Append("<line x1='").Append(SvgNumber(plotLeft)).Append("' y1='").Append(SvgNumber(y)).Append("' x2='").Append(SvgNumber(plotRight)).Append("' y2='").Append(SvgNumber(y)).AppendLine("' stroke='#e7ebf1' stroke-width='1'/>")
            html.Append("<text x='").Append(SvgNumber(plotLeft - 8)).Append("' y='").Append(SvgNumber(y + 4)).Append("' text-anchor='end' font-size='12' fill='#687588'>")
            html.Append(Encode(labelValue.ToString("0.###", TurkishCulture))).AppendLine("</text>")
        Next

        AppendSvgLimitLine(html, row.UpperLimit, "Üst", "#b22222", plotLeft, plotRight, plotTop, plotHeight, minY, maxY)
        AppendSvgLimitLine(html, row.LowerLimit, "Alt", "#b22222", plotLeft, plotRight, plotTop, plotHeight, minY, maxY)
        AppendSvgLimitLine(html, row.Nominal, "Nominal", "#1f5b9e", plotLeft, plotRight, plotTop, plotHeight, minY, maxY)

        Dim coordinates As New List(Of String)()
        For index As Integer = 0 To points.Count - 1
            Dim x = If(points.Count = 1,
                       plotLeft + plotWidth / 2.0R,
                       plotLeft + plotWidth * index / CDbl(points.Count - 1))
            Dim y = ChartValueToY(CDbl(points(index).Value), plotTop, plotHeight, minY, maxY)
            coordinates.Add(SvgNumber(x) & "," & SvgNumber(y))
        Next
        If coordinates.Count > 1 Then
            html.Append("<polyline points='").Append(String.Join(" ", coordinates)).AppendLine("' fill='none' stroke='#2c5da0' stroke-width='2.2' stroke-linejoin='round' stroke-linecap='round'/>")
        End If

        For index As Integer = 0 To points.Count - 1
            Dim point = points(index)
            Dim x = If(points.Count = 1,
                       plotLeft + plotWidth / 2.0R,
                       plotLeft + plotWidth * index / CDbl(points.Count - 1))
            Dim y = ChartValueToY(CDbl(point.Value), plotTop, plotHeight, minY, maxY)
            html.Append("<circle cx='").Append(SvgNumber(x)).Append("' cy='").Append(SvgNumber(y)).Append("' r='4.8' fill='")
            html.Append(ChartPointColor(point.Result)).AppendLine("' stroke='#ffffff' stroke-width='1.4'>")
            html.Append("<title>").Append(Encode(point.DateValue.ToString("dd.MM.yyyy HH:mm") & " • Göz " & point.EyeNo.ToString() & " • " & point.Value.ToString("0.###", TurkishCulture) & " • " & EmptyAsDash(point.Result))).AppendLine("</title></circle>")
        Next

        AppendChartDateLabel(html, points.First(), plotLeft, "start")
        If points.Count > 2 Then
            AppendChartDateLabel(html, points(points.Count \ 2), plotLeft + plotWidth / 2.0R, "middle")
        End If
        AppendChartDateLabel(html, points.Last(), plotRight, "end")

        html.Append("<circle cx='80' cy='232' r='4.5' fill='#18834b'/><text x='90' y='236' font-size='12' fill='#526074'>OK</text>")
        html.Append("<circle cx='135' cy='232' r='4.5' fill='#b22222'/><text x='145' y='236' font-size='12' fill='#526074'>NOK</text>")
        html.Append("<circle cx='205' cy='232' r='4.5' fill='#b97400'/><text x='215' y='236' font-size='12' fill='#526074'>Hatalı</text>")
        html.Append("<text x='").Append(SvgNumber(plotRight)).Append("' y='236' text-anchor='end' font-size='12' fill='#526074'>Grafikte: ")
        html.Append(points.Count).Append(" / ").Append(row.RecordCount).AppendLine(" kayıt</text>")
        html.AppendLine("</svg></div>")
    End Sub

    Private Shared Sub AppendSvgLimitLine(html As StringBuilder,
                                          value As Decimal?,
                                          label As String,
                                          color As String,
                                          plotLeft As Double,
                                          plotRight As Double,
                                          plotTop As Double,
                                          plotHeight As Double,
                                          minY As Double,
                                          maxY As Double)
        If Not value.HasValue Then Return
        Dim y = ChartValueToY(CDbl(value.Value), plotTop, plotHeight, minY, maxY)
        html.Append("<line x1='").Append(SvgNumber(plotLeft)).Append("' y1='").Append(SvgNumber(y)).Append("' x2='").Append(SvgNumber(plotRight)).Append("' y2='").Append(SvgNumber(y))
        html.Append("' stroke='").Append(color).AppendLine("' stroke-width='1.6' stroke-dasharray='7 5'/>")
        html.Append("<text x='").Append(SvgNumber(plotRight - 5)).Append("' y='").Append(SvgNumber(y - 5)).Append("' text-anchor='end' font-size='12' font-weight='700' fill='").Append(color).Append("'>")
        html.Append(Encode(label & ": " & value.Value.ToString("0.###", TurkishCulture))).AppendLine("</text>")
    End Sub

    Private Shared Sub AppendChartDateLabel(html As StringBuilder, point As RiskMeasurementPdfPoint, x As Double, anchor As String)
        html.Append("<text x='").Append(SvgNumber(x)).Append("' y='207' text-anchor='").Append(anchor).AppendLine("' font-size='11.5' fill='#687588'>")
        html.Append(Encode(point.DateValue.ToString("dd.MM.yyyy HH:mm") & " • Göz " & point.EyeNo.ToString())).AppendLine("</text>")
    End Sub

    Private Shared Function ChartValueToY(value As Double,
                                          plotTop As Double,
                                          plotHeight As Double,
                                          minY As Double,
                                          maxY As Double) As Double
        Dim ratio = (value - minY) / (maxY - minY)
        Return plotTop + plotHeight - plotHeight * ratio
    End Function

    Private Shared Function ChartPointColor(resultValue As String) As String
        Dim normalized = If(resultValue, "").Trim().ToUpper(TurkishCulture)
        If normalized = "NOK" OrElse normalized.Contains("UYGUN DEĞİL") Then Return "#b22222"
        If normalized = "HATALI" Then Return "#b97400"
        If normalized = "OK" OrElse normalized = "UYGUN" Then Return "#18834b"
        Return "#315d8f"
    End Function

    Private Shared Function SvgNumber(value As Double) As String
        Return value.ToString("0.###", CultureInfo.InvariantCulture)
    End Function

    Private Shared Sub AppendFooter(html As StringBuilder, generatedAt As DateTime, pageNumber As Integer, pageCount As Integer)
        html.Append("<footer class='footer'><span>Teknik Resim Ölçüm Kontrol • SPC Riskli Ölçüler</span><span>")
        html.Append(Encode(generatedAt.ToString("dd.MM.yyyy HH:mm"))).Append(" &nbsp;•&nbsp; Sayfa ")
        html.Append(pageNumber).Append("/").Append(pageCount).AppendLine("</span></footer>")
    End Sub

    Private Shared Function RiskAccent(riskLevel As String) As String
        If IsRisk(riskLevel, "YÜKSEK") Then Return "#b22222"
        If IsRisk(riskLevel, "ORTA") Then Return "#b97400"
        If IsRisk(riskLevel, "DÜŞÜK") Then Return "#1f649b"
        Return "#526074"
    End Function

    Private Shared Function RiskSoftColor(riskLevel As String) As String
        If IsRisk(riskLevel, "YÜKSEK") Then Return "#ffe7e5"
        If IsRisk(riskLevel, "ORTA") Then Return "#fff8da"
        If IsRisk(riskLevel, "DÜŞÜK") Then Return "#eff6ff"
        Return "#f3f5f8"
    End Function

    Private Shared Function ResultAccent(resultValue As String) As String
        Dim normalized = If(resultValue, "").Trim().ToUpper(TurkishCulture)
        If normalized = "OK" OrElse normalized = "UYGUN" Then Return "#18834b"
        If normalized.Contains("NOK") OrElse normalized.Contains("UYGUN DEĞİL") OrElse normalized.Contains("HATALI") Then Return "#b22222"
        Return "#315d8f"
    End Function

    Private Shared Function IsRisk(value As String, expected As String) As Boolean
        Return String.Equals(If(value, "").Trim(), expected, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function FormatNullable(value As Double?, formatText As String) As String
        If Not value.HasValue Then Return "-"
        Return value.Value.ToString(formatText, TurkishCulture)
    End Function

    Private Shared Function EmptyAsDash(value As String) As String
        Return If(String.IsNullOrWhiteSpace(value), "-", value.Trim())
    End Function

    Private Shared Function Encode(value As String) As String
        Return WebUtility.HtmlEncode(If(value, ""))
    End Function

    Private Shared Function EncodeWithBreaks(value As String) As String
        Return Encode(value).Replace(vbCrLf, "<br>").Replace(vbLf, "<br>")
    End Function
End Class
