Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Text.RegularExpressions
Imports Docnet.Core
Imports Docnet.Core.Models

Public NotInheritable Class PdfRenderService
    Private Sub New()
    End Sub

    Public Shared Function RenderFirstPageToPng(pdfPath As String) As String
        If String.IsNullOrWhiteSpace(pdfPath) OrElse Not File.Exists(pdfPath) Then
            Throw New FileNotFoundException("PDF dosyası bulunamadı.", pdfPath)
        End If

        Dim detectedAspect As Decimal = GetPdfPageAspectRatioDecimal(pdfPath)
        Dim candidateAspects As New List(Of Decimal)()

        AddCandidateAspect(candidateAspects, detectedAspect)
        If detectedAspect > 0D Then AddCandidateAspect(candidateAspects, 1D / detectedAspect)

        ' Teknik resimlerde en sık karşılaşılan A4 yönleri.
        AddCandidateAspect(candidateAspects, 1.41421356D)  ' yatay
        AddCandidateAspect(candidateAspects, 0.70710678D)  ' dikey

        Dim candidates As New List(Of RenderCandidate)()
        For Each aspect In candidateAspects
            Try
                Dim candidatePath As String = RenderWithAspect(pdfPath, aspect)
                candidates.Add(New RenderCandidate With {
                    .FilePath = candidatePath,
                    .Score = CalculateEdgeInkScore(candidatePath),
                    .Aspect = aspect
                })
            Catch ex As Exception
                ErrorLogService.Log("PdfRenderService.RenderCandidate", ex, "PdfPath=" & pdfPath & "; Aspect=" & aspect.ToString())
            End Try
        Next

        If candidates.Count = 0 Then
            Throw New InvalidOperationException("PDF sayfası görüntüye çevrilemedi.")
        End If

        Dim best = candidates.OrderBy(Function(c) c.Score).ThenByDescending(Function(c) c.Aspect).First()

        For Each c In candidates
            If Not String.Equals(c.FilePath, best.FilePath, StringComparison.OrdinalIgnoreCase) Then
                Try
                    If File.Exists(c.FilePath) Then File.Delete(c.FilePath)
                Catch ex As Exception
                    ErrorLogService.Log("PdfRenderService.DeleteUnusedCandidate", ex, "Path=" & c.FilePath)
                End Try
            End If
        Next

        Return best.FilePath
    End Function

    Public Shared Function GetImageAspectRatioText(imagePath As String) As String
        Try
            Using img As Image = Image.FromFile(imagePath)
                If img.Width > 0 AndAlso img.Height > 0 Then
                    Dim aspect As Decimal = CDec(img.Width) / CDec(img.Height)
                    Return aspect.ToString("0.########", Globalization.CultureInfo.InvariantCulture)
                End If
            End Using
        Catch ex As Exception
            ErrorLogService.Log("PdfRenderService.GetImageAspectRatioText", ex, "Path=" & If(imagePath, ""))
        End Try

        Return "1.41421356"
    End Function

    Private Shared Sub AddCandidateAspect(list As List(Of Decimal), aspect As Decimal)
        If aspect <= 0.1D OrElse aspect >= 10D Then Return

        For Each existing In list
            If Math.Abs(existing - aspect) < 0.03D Then Return
        Next

        list.Add(aspect)
    End Sub

    Private Shared Function RenderWithAspect(pdfPath As String, aspect As Decimal) As String
        ' Yüksek çözünürlük: zoom sonrası yazılar daha net görünür.
        Dim maxSide As Integer = 6000
        Dim renderWidth As Integer
        Dim renderHeight As Integer

        If aspect >= 1D Then
            renderWidth = maxSide
            renderHeight = Math.Max(300, CInt(Math.Round(maxSide / aspect)))
        Else
            renderHeight = maxSide
            renderWidth = Math.Max(300, CInt(Math.Round(maxSide * aspect)))
        End If

        Dim outPath As String = Path.Combine(AppPaths.TempDir, "pdf_page_" & Guid.NewGuid().ToString("N") & ".png")
        Dim pdfBytes As Byte() = File.ReadAllBytes(pdfPath)

        Using docReader = DocLib.Instance.GetDocReader(pdfBytes, New PageDimensions(renderWidth, renderHeight))
            Using pageReader = docReader.GetPageReader(0)
                Dim rawBytes As Byte() = pageReader.GetImage()
                Dim width As Integer = pageReader.GetPageWidth()
                Dim height As Integer = pageReader.GetPageHeight()

                Using bmp As New Bitmap(width, height, PixelFormat.Format32bppArgb)
                    Dim rect As New Rectangle(0, 0, width, height)
                    Dim bmpData As BitmapData = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb)
                    Try
                        Marshal.Copy(rawBytes, 0, bmpData.Scan0, Math.Min(rawBytes.Length, Math.Abs(bmpData.Stride) * height))
                    Finally
                        bmp.UnlockBits(bmpData)
                    End Try

                    bmp.Save(outPath, ImageFormat.Png)
                End Using
            End Using
        End Using

        Return outPath
    End Function

    Private Shared Function CalculateEdgeInkScore(imagePath As String) As Decimal
        Try
            Using bmp As New Bitmap(imagePath)
                Dim stepPx As Integer = Math.Max(1, Math.Min(bmp.Width, bmp.Height) \ 220)
                Dim strip As Integer = Math.Max(6, Math.Min(bmp.Width, bmp.Height) \ 80)

                Dim edgeInk As Integer = 0
                Dim edgeSamples As Integer = 0

                ' Üst / alt kenar
                For x As Integer = 0 To bmp.Width - 1 Step stepPx
                    For y As Integer = 0 To strip - 1 Step stepPx
                        edgeSamples += 1
                        If IsInk(bmp.GetPixel(x, y)) Then edgeInk += 1

                        edgeSamples += 1
                        If IsInk(bmp.GetPixel(x, bmp.Height - 1 - y)) Then edgeInk += 1
                    Next
                Next

                ' Sol / sağ kenar
                For y As Integer = 0 To bmp.Height - 1 Step stepPx
                    For x As Integer = 0 To strip - 1 Step stepPx
                        edgeSamples += 1
                        If IsInk(bmp.GetPixel(x, y)) Then edgeInk += 1

                        edgeSamples += 1
                        If IsInk(bmp.GetPixel(bmp.Width - 1 - x, y)) Then edgeInk += 1
                    Next
                Next

                If edgeSamples = 0 Then Return 999999D

                Dim edgeRatio As Decimal = CDec(edgeInk) / CDec(edgeSamples)

                ' Çok dar / çok uzun anormal renderlara küçük ceza.
                Dim aspect As Decimal = CDec(bmp.Width) / CDec(bmp.Height)
                Dim aspectPenalty As Decimal = 0D
                If aspect < 0.3D OrElse aspect > 3.5D Then aspectPenalty = 1D

                Return edgeRatio + aspectPenalty
            End Using
        Catch ex As Exception
            ErrorLogService.Log("PdfRenderService.CalculateEdgeInkScore", ex, "Path=" & If(imagePath, ""))
            Return 999999D
        End Try
    End Function

    Private Shared Function IsInk(c As Color) As Boolean
        ' Beyaza çok yakın olmayan piksel "içerik" kabul edilir.
        Return c.R < 245 OrElse c.G < 245 OrElse c.B < 245
    End Function

    Private Shared Function GetPdfPageAspectRatioDecimal(pdfPath As String) As Decimal
        Try
            Dim raw As String = Encoding.ASCII.GetString(File.ReadAllBytes(pdfPath))

            Dim boxMatch As Match = Regex.Match(raw, "/CropBox\s*\[\s*([-0-9\.]+)\s+([-0-9\.]+)\s+([-0-9\.]+)\s+([-0-9\.]+)\s*\]", RegexOptions.IgnoreCase)
            If Not boxMatch.Success Then
                boxMatch = Regex.Match(raw, "/MediaBox\s*\[\s*([-0-9\.]+)\s+([-0-9\.]+)\s+([-0-9\.]+)\s+([-0-9\.]+)\s*\]", RegexOptions.IgnoreCase)
            End If

            If boxMatch.Success Then
                Dim x0 As Decimal = Decimal.Parse(boxMatch.Groups(1).Value, Globalization.CultureInfo.InvariantCulture)
                Dim y0 As Decimal = Decimal.Parse(boxMatch.Groups(2).Value, Globalization.CultureInfo.InvariantCulture)
                Dim x1 As Decimal = Decimal.Parse(boxMatch.Groups(3).Value, Globalization.CultureInfo.InvariantCulture)
                Dim y1 As Decimal = Decimal.Parse(boxMatch.Groups(4).Value, Globalization.CultureInfo.InvariantCulture)

                Dim w As Decimal = Math.Abs(x1 - x0)
                Dim h As Decimal = Math.Abs(y1 - y0)
                If w > 0D AndAlso h > 0D Then
                    Dim rotated As Boolean = Regex.IsMatch(raw, "/Rotate\s+(90|270)", RegexOptions.IgnoreCase)
                    Dim aspect As Decimal = If(rotated, h / w, w / h)
                    If aspect > 0.1D AndAlso aspect < 10D Then Return aspect
                End If
            End If
        Catch ex As Exception
            ErrorLogService.Log("PdfRenderService.GetPdfPageAspectRatioDecimal", ex, "Path=" & If(pdfPath, ""))
        End Try

        Return 1.41421356D
    End Function

    Private Class RenderCandidate
        Public Property FilePath As String = ""
        Public Property Score As Decimal
        Public Property Aspect As Decimal
    End Class
End Class
