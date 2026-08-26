Imports System.Diagnostics
Imports System.Globalization
Imports System.IO
Imports System.Text

Public NotInheritable Class AutoCadDimensionImportService
    Private Const ProcessTimeoutMs As Integer = 180000

    Private Sub New()
    End Sub

    Public Shared Function ExtractDimensions(drawingPath As String) As CadDimensionExtractionResult
        drawingPath = If(drawingPath, "").Trim()
        If drawingPath = "" OrElse Not File.Exists(drawingPath) Then
            Throw New FileNotFoundException("DWG/DXF dosyası bulunamadı.", drawingPath)
        End If

        Dim extension = Path.GetExtension(drawingPath)
        If Not String.Equals(extension, ".dwg", StringComparison.OrdinalIgnoreCase) AndAlso
           Not String.Equals(extension, ".dxf", StringComparison.OrdinalIgnoreCase) Then
            Throw New InvalidDataException("Yalnızca DWG veya DXF dosyaları taranabilir.")
        End If

        Dim coreConsolePath = FindAutoCadCoreConsole()
        If coreConsolePath = "" Then
            Throw New FileNotFoundException(
                "AutoCAD Core Console (accoreconsole.exe) bulunamadı." & Environment.NewLine &
                "Bu özellik için tam AutoCAD kurulumu gereklidir. AutoCAD LT kurulumu yeterli olmayabilir.")
        End If

        Dim workDir = Path.Combine(AppPaths.TempDir, "CadDimensionImport_" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(workDir)

        Dim lispPath = Path.Combine(workDir, "extract_dimensions.lsp")
        Dim scriptPath = Path.Combine(workDir, "run_extract.scr")
        Dim outputPath = Path.Combine(workDir, "dimensions.tsv")
        Dim logPath = Path.Combine(workDir, "accoreconsole.log")

        Try
            File.WriteAllText(lispPath, BuildAutoLisp(), New UTF8Encoding(False))
            File.WriteAllText(
                scriptPath,
                "(load """ & ToLispPath(lispPath) & """)" & Environment.NewLine &
                "_.REGENALL" & Environment.NewLine &
                "(TRMEXPORT """ & ToLispPath(outputPath) & """)" & Environment.NewLine &
                "_.QUIT" & Environment.NewLine &
                "_N" & Environment.NewLine,
                New UTF8Encoding(False))

            RunCoreConsole(coreConsolePath, drawingPath, scriptPath, logPath)

            If Not File.Exists(outputPath) Then
                Dim logText = If(File.Exists(logPath), File.ReadAllText(logPath), "")
                Throw New InvalidOperationException(
                    "AutoCAD ölçü tarama çıktısı oluşturamadı." &
                    If(logText.Trim() = "", "", Environment.NewLine & LastLogPart(logText)))
            End If

            Dim result = ParseExportFile(outputPath)
            result.AutoCadToolPath = coreConsolePath
            result.SourceDrawingPath = drawingPath
            Return result
        Finally
            Try
                If Directory.Exists(workDir) Then Directory.Delete(workDir, True)
            Catch ex As Exception
                ErrorLogService.Log("AutoCadDimensionImportService.Cleanup", ex, "Path=" & workDir)
            End Try
        End Try
    End Function

    Private Shared Function FindAutoCadCoreConsole() As String
        Dim configuredPath = Environment.GetEnvironmentVariable("ACCORECONSOLE_PATH")
        If Not String.IsNullOrWhiteSpace(configuredPath) AndAlso File.Exists(configuredPath) Then
            Return Path.GetFullPath(configuredPath)
        End If

        Dim roots As New List(Of String)()
        AddExistingRoot(roots, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Autodesk"))
        AddExistingRoot(roots, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Autodesk"))

        Dim candidates As New List(Of String)()
        For Each root In roots
            Try
                candidates.AddRange(Directory.GetFiles(root, "accoreconsole.exe", SearchOption.AllDirectories))
            Catch ex As Exception
                ErrorLogService.Log("AutoCadDimensionImportService.FindCoreConsole", ex, "Root=" & root)
            End Try
        Next

        Return candidates.
            OrderByDescending(Function(path) path, StringComparer.OrdinalIgnoreCase).
            FirstOrDefault()
    End Function

    Private Shared Sub AddExistingRoot(roots As List(Of String), root As String)
        If Not String.IsNullOrWhiteSpace(root) AndAlso Directory.Exists(root) AndAlso
           Not roots.Contains(root, StringComparer.OrdinalIgnoreCase) Then
            roots.Add(root)
        End If
    End Sub

    Private Shared Sub RunCoreConsole(coreConsolePath As String,
                                      drawingPath As String,
                                      scriptPath As String,
                                      logPath As String)
        Dim psi As New ProcessStartInfo() With {
            .FileName = coreConsolePath,
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .WindowStyle = ProcessWindowStyle.Hidden,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True
        }
        psi.ArgumentList.Add("/i")
        psi.ArgumentList.Add(drawingPath)
        psi.ArgumentList.Add("/s")
        psi.ArgumentList.Add(scriptPath)
        psi.ArgumentList.Add("/l")
        psi.ArgumentList.Add("en-US")

        Using runningProcess As Process = Process.Start(psi)
            If runningProcess Is Nothing Then Throw New InvalidOperationException("AutoCAD Core Console başlatılamadı.")

            Dim standardOutputTask = runningProcess.StandardOutput.ReadToEndAsync()
            Dim standardErrorTask = runningProcess.StandardError.ReadToEndAsync()
            If Not runningProcess.WaitForExit(ProcessTimeoutMs) Then
                Try
                    runningProcess.Kill(True)
                Catch ex As Exception
                    ErrorLogService.Log("AutoCadDimensionImportService.KillTimedOutProcess", ex)
                End Try
                Throw New TimeoutException("AutoCAD ölçü taraması 3 dakika içinde tamamlanamadı.")
            End If

            Dim logText = standardOutputTask.GetAwaiter().GetResult() &
                          Environment.NewLine &
                          standardErrorTask.GetAwaiter().GetResult()
            File.WriteAllText(logPath, logText, New UTF8Encoding(False))

            If runningProcess.ExitCode <> 0 Then
                Throw New InvalidOperationException(
                    "AutoCAD ölçü taraması başarısız oldu. Çıkış kodu: " & runningProcess.ExitCode.ToString() &
                    Environment.NewLine & LastLogPart(logText))
            End If
        End Using
    End Sub

    Private Shared Function ParseExportFile(outputPath As String) As CadDimensionExtractionResult
        Dim result As New CadDimensionExtractionResult()
        Dim rawCandidates As New List(Of CadDimensionCandidate)()
        Dim extMinX As Double = Double.NaN
        Dim extMinY As Double = Double.NaN
        Dim extMaxX As Double = Double.NaN
        Dim extMaxY As Double = Double.NaN

        For Each rawLine In File.ReadAllLines(outputPath, Encoding.UTF8)
            If String.IsNullOrWhiteSpace(rawLine) Then Continue For
            Dim parts = rawLine.Split(ControlChars.Tab)
            If parts.Length = 0 Then Continue For

            If String.Equals(parts(0), "META", StringComparison.OrdinalIgnoreCase) Then
                If parts.Length >= 6 Then
                    result.Unit = UnitFromInsUnits(parts(1))
                    TryParseDoubleInvariant(parts(2), extMinX)
                    TryParseDoubleInvariant(parts(3), extMinY)
                    TryParseDoubleInvariant(parts(4), extMaxX)
                    TryParseDoubleInvariant(parts(5), extMaxY)
                End If
                Continue For
            End If

            If Not String.Equals(parts(0), "DIM", StringComparison.OrdinalIgnoreCase) OrElse parts.Length < 11 Then Continue For

            Dim nominal As Decimal
            Dim lowerTolerance As Decimal
            Dim upperTolerance As Decimal
            Dim rawX As Double
            Dim rawY As Double
            If Not Decimal.TryParse(parts(6), NumberStyles.Any, CultureInfo.InvariantCulture, nominal) Then Continue For
            Decimal.TryParse(parts(7), NumberStyles.Any, CultureInfo.InvariantCulture, lowerTolerance)
            Decimal.TryParse(parts(8), NumberStyles.Any, CultureInfo.InvariantCulture, upperTolerance)
            Double.TryParse(parts(9), NumberStyles.Any, CultureInfo.InvariantCulture, rawX)
            Double.TryParse(parts(10), NumberStyles.Any, CultureInfo.InvariantCulture, rawY)

            Dim dimensionType = parts(4).Trim()
            Dim displayText = parts(5).Trim()
            rawCandidates.Add(New CadDimensionCandidate With {
                .EntityHandle = parts(1).Trim(),
                .LayerName = parts(2).Trim(),
                .LayoutName = parts(3).Trim(),
                .DimensionType = dimensionType,
                .DisplayText = displayText,
                .MeasureName = BuildMeasureName(displayText, dimensionType),
                .Nominal = nominal,
                .LowerTolerance = -Math.Abs(lowerTolerance),
                .UpperTolerance = Math.Abs(upperTolerance),
                .Unit = result.Unit,
                .RawX = rawX,
                .RawY = rawY
            })
        Next

        If rawCandidates.Count = 0 Then
            result.Candidates = rawCandidates
            Return result
        End If

        If Double.IsNaN(extMinX) OrElse Double.IsNaN(extMinY) OrElse
           Double.IsNaN(extMaxX) OrElse Double.IsNaN(extMaxY) OrElse
           extMaxX <= extMinX OrElse extMaxY <= extMinY Then
            extMinX = rawCandidates.Min(Function(candidate) candidate.RawX)
            extMaxX = rawCandidates.Max(Function(candidate) candidate.RawX)
            extMinY = rawCandidates.Min(Function(candidate) candidate.RawY)
            extMaxY = rawCandidates.Max(Function(candidate) candidate.RawY)
        End If

        Dim width = Math.Max(0.000001R, extMaxX - extMinX)
        Dim height = Math.Max(0.000001R, extMaxY - extMinY)
        For Each candidate In rawCandidates
            candidate.XPercent = ClampPercent(CDec((candidate.RawX - extMinX) * 100.0R / width))
            candidate.YPercent = ClampPercent(CDec(100.0R - ((candidate.RawY - extMinY) * 100.0R / height)))
            If candidate.XPercent <= 0D OrElse candidate.XPercent >= 100D OrElse
               candidate.YPercent <= 0D OrElse candidate.YPercent >= 100D Then
                candidate.WarningText = "Konum çizim sınırına yakın; PDF üzerinde kontrol edin."
            End If
        Next

        result.Candidates = rawCandidates
        Return result
    End Function

    Private Shared Function ClampPercent(value As Decimal) As Decimal
        Return Math.Round(Math.Max(0.01D, Math.Min(99.99D, value)), 2)
    End Function

    Private Shared Function BuildMeasureName(displayText As String, dimensionType As String) As String
        Dim text = If(displayText, "").Trim().
            Replace("<>", "").
            Replace("%%c", "Ø").
            Replace("\X", " ").
            Replace("\P", " ")
        If text <> "" Then Return text

        Dim typeText = If(dimensionType, "").ToUpperInvariant()
        If typeText.Contains("DIAMETRIC") Then Return "Çap ölçüsü"
        If typeText.Contains("RADIAL") Then Return "Radyüs ölçüsü"
        If typeText.Contains("ANGULAR") Then Return "Açı ölçüsü"
        If typeText.Contains("ORDINATE") Then Return "Koordinat ölçüsü"
        If typeText.Contains("ALIGNED") Then Return "Hizalı ölçü"
        Return "Doğrusal ölçü"
    End Function

    Private Shared Function UnitFromInsUnits(value As String) As String
        Dim unitCode As Integer
        If Not Integer.TryParse(If(value, "").Trim(), unitCode) Then Return "mm"
        Select Case unitCode
            Case 1
                Return "in"
            Case 2
                Return "ft"
            Case 4
                Return "mm"
            Case 5
                Return "cm"
            Case 6
                Return "m"
            Case Else
                Return "mm"
        End Select
    End Function

    Private Shared Function TryParseDoubleInvariant(text As String, ByRef value As Double) As Boolean
        Return Double.TryParse(If(text, ""), NumberStyles.Any, CultureInfo.InvariantCulture, value)
    End Function

    Private Shared Function ToLispPath(filePath As String) As String
        Return IO.Path.GetFullPath(filePath).Replace("\", "/")
    End Function

    Private Shared Function LastLogPart(logText As String) As String
        Dim lines = If(logText, "").
            Replace(vbCrLf, vbLf).
            Replace(vbCr, vbLf).
            Split({vbLf}, StringSplitOptions.RemoveEmptyEntries)
        Return String.Join(Environment.NewLine, lines.Skip(Math.Max(0, lines.Length - 12)))
    End Function

    Private Shared Function BuildAutoLisp() As String
        Dim sb As New StringBuilder()
        sb.AppendLine("(vl-load-com)")
        sb.AppendLine("(defun trm-clean (s)")
        sb.AppendLine("  (if (null s) (setq s """"))")
        sb.AppendLine("  (if (/= (type s) 'STR) (setq s (vl-princ-to-string s)))")
        sb.AppendLine("  (setq s (vl-string-translate (strcat (chr 9) (chr 10) (chr 13)) ""   "" s))")
        sb.AppendLine("  s)")
        sb.AppendLine("(defun trm-num (v) (if (numberp v) (rtos v 2 12) ""0""))")
        sb.AppendLine("(defun trm-prop (obj prop default / value)")
        sb.AppendLine("  (if (vlax-property-available-p obj prop)")
        sb.AppendLine("    (progn")
        sb.AppendLine("      (setq value (vl-catch-all-apply 'vlax-get-property (list obj prop)))")
        sb.AppendLine("      (if (vl-catch-all-error-p value) default value))")
        sb.AppendLine("    default))")
        sb.AppendLine("(defun trm-point (obj / value)")
        sb.AppendLine("  (setq value (trm-prop obj 'TextPosition nil))")
        sb.AppendLine("  (if (null value) (setq value (trm-prop obj 'TextPosition2 nil)))")
        sb.AppendLine("  (if value (vlax-safearray->list (vlax-variant-value value)) '(0.0 0.0 0.0)))")
        sb.AppendLine("(defun TRMEXPORT (outputPath / file extmin extmax insunits ss i en obj data pt layout lower upper)")
        sb.AppendLine("  (setq file (open outputPath ""w""))")
        sb.AppendLine("  (if file")
        sb.AppendLine("    (progn")
        sb.AppendLine("      (setq extmin (getvar ""EXTMIN"") extmax (getvar ""EXTMAX"") insunits (getvar ""INSUNITS""))")
        sb.AppendLine("      (write-line (strcat ""META\t"" (itoa insunits) ""\t"" (trm-num (car extmin)) ""\t"" (trm-num (cadr extmin)) ""\t"" (trm-num (car extmax)) ""\t"" (trm-num (cadr extmax))) file)")
        sb.AppendLine("      (setq ss (ssget ""_X"" '((0 . ""DIMENSION""))))")
        sb.AppendLine("      (if ss")
        sb.AppendLine("        (progn")
        sb.AppendLine("          (setq i 0)")
        sb.AppendLine("          (repeat (sslength ss)")
        sb.AppendLine("            (setq en (ssname ss i) obj (vlax-ename->vla-object en) data (entget en) pt (trm-point obj))")
        sb.AppendLine("            (setq layout (cdr (assoc 410 data)))")
        sb.AppendLine("            (if (null layout) (setq layout ""Model""))")
        sb.AppendLine("            (setq lower (trm-prop obj 'ToleranceLowerLimit 0.0) upper (trm-prop obj 'ToleranceUpperLimit 0.0))")
        sb.AppendLine("            (write-line")
        sb.AppendLine("              (strcat ""DIM\t""")
        sb.AppendLine("                (trm-clean (trm-prop obj 'Handle """")) ""\t""")
        sb.AppendLine("                (trm-clean (trm-prop obj 'Layer """")) ""\t""")
        sb.AppendLine("                (trm-clean layout) ""\t""")
        sb.AppendLine("                (trm-clean (trm-prop obj 'ObjectName """")) ""\t""")
        sb.AppendLine("                (trm-clean (trm-prop obj 'TextOverride """")) ""\t""")
        sb.AppendLine("                (trm-num (trm-prop obj 'Measurement 0.0)) ""\t""")
        sb.AppendLine("                (trm-num lower) ""\t"" (trm-num upper) ""\t""")
        sb.AppendLine("                (trm-num (car pt)) ""\t"" (trm-num (cadr pt))) file)")
        sb.AppendLine("            (setq i (1+ i)))))")
        sb.AppendLine("      (close file)))")
        sb.AppendLine("  (princ))")
        Return sb.ToString()
    End Function
End Class
