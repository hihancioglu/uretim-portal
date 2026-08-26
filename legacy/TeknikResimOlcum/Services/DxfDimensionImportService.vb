Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Text.RegularExpressions

Public NotInheritable Class DxfDimensionImportService
    Private Sub New()
    End Sub

    Private Class DxfPair
        Public Property Code As Integer
        Public Property Value As String = ""
    End Class

    Public Shared Function ExtractDimensions(dxfPath As String) As CadDimensionExtractionResult
        dxfPath = If(dxfPath, "").Trim()
        If dxfPath = "" OrElse Not File.Exists(dxfPath) Then
            Throw New FileNotFoundException("DXF dosyası bulunamadı.", dxfPath)
        End If

        If Not String.Equals(Path.GetExtension(dxfPath), ".dxf", StringComparison.OrdinalIgnoreCase) Then
            Throw New InvalidDataException("AutoCAD olmadan yalnızca DXF dosyası taranabilir.")
        End If

        Dim pairs = ReadPairs(dxfPath)
        If pairs.Count = 0 Then
            Throw New InvalidDataException("DXF dosyası okunamadı. Dosya ASCII DXF olarak dışa aktarılmış olmalıdır.")
        End If

        Dim unitCode As String = ""
        Dim extMinX As Double = Double.NaN
        Dim extMinY As Double = Double.NaN
        Dim extMaxX As Double = Double.NaN
        Dim extMaxY As Double = Double.NaN
        ReadHeaderMetadata(pairs, unitCode, extMinX, extMinY, extMaxX, extMaxY)

        Dim result As New CadDimensionExtractionResult() With {
            .SourceDrawingPath = dxfPath,
            .AutoCadToolPath = "Yerleşik DXF okuyucu",
            .Unit = UnitFromInsUnits(unitCode)
        }

        Dim scanPairs = EntitySectionPairs(pairs)
        If scanPairs.Count = 0 Then scanPairs = pairs
        Dim blockDefinitions = ReadBlockDefinitions(pairs)

        Dim candidates As New List(Of CadDimensionCandidate)()
        For i As Integer = 0 To scanPairs.Count - 1
            If scanPairs(i).Code <> 0 OrElse Not String.Equals(scanPairs(i).Value.Trim(), "DIMENSION", StringComparison.OrdinalIgnoreCase) Then Continue For

            Dim entityPairs As New List(Of DxfPair)()
            i += 1
            While i < scanPairs.Count AndAlso scanPairs(i).Code <> 0
                entityPairs.Add(scanPairs(i))
                i += 1
            End While
            i -= 1

            Dim candidate = ParseDimensionEntity(entityPairs, result.Unit, blockDefinitions)
            If candidate IsNot Nothing Then
                candidates.Add(candidate)
            End If
        Next

        ApplyPercentCoordinates(candidates, extMinX, extMinY, extMaxX, extMaxY)
        candidates = RemoveLikelyTitleBlockCandidates(candidates)
        result.Candidates = DeduplicateCandidates(candidates)
        Return result
    End Function

    Private Shared Function ReadPairs(dxfPath As String) As List(Of DxfPair)
        Dim bytes = File.ReadAllBytes(dxfPath)
        If bytes.Length >= 22 Then
            Dim header = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 22))
            If header.StartsWith("AutoCAD Binary DXF", StringComparison.OrdinalIgnoreCase) Then
                Throw New InvalidDataException("Binary DXF okunamaz. Siemens programından ASCII DXF olarak dışa aktarın.")
            End If
        End If

        Dim text = ReadDxfText(bytes).
            Replace(vbCrLf, vbLf).
            Replace(vbCr, vbLf)
        Dim lines = text.Split({vbLf}, StringSplitOptions.None)
        Dim result As New List(Of DxfPair)()

        Dim i As Integer = 0
        While i + 1 < lines.Length
            Dim code As Integer
            If Integer.TryParse(lines(i).Trim(), code) Then
                result.Add(New DxfPair With {.Code = code, .Value = lines(i + 1).Trim()})
                i += 2
            Else
                i += 1
            End If
        End While

        Return result
    End Function

    Private Shared Function ReadDxfText(bytes As Byte()) As String
        If bytes Is Nothing OrElse bytes.Length = 0 Then Return ""

        Try
            Return New UTF8Encoding(False, True).GetString(bytes).TrimStart(ChrW(&HFEFF))
        Catch ex As DecoderFallbackException
            Return Encoding.Default.GetString(bytes).TrimStart(ChrW(&HFEFF))
        End Try
    End Function

    Private Shared Sub ReadHeaderMetadata(pairs As List(Of DxfPair),
                                          ByRef unitCode As String,
                                          ByRef extMinX As Double,
                                          ByRef extMinY As Double,
                                          ByRef extMaxX As Double,
                                          ByRef extMaxY As Double)
        For i As Integer = 0 To pairs.Count - 2
            If pairs(i).Code <> 9 Then Continue For

            Dim variableName = pairs(i).Value.Trim().ToUpperInvariant()
            If variableName = "$INSUNITS" Then
                Dim valuePair = FindNextPair(pairs, i + 1, 70)
                If valuePair IsNot Nothing Then unitCode = valuePair.Value
            ElseIf variableName = "$EXTMIN" Then
                Dim xPair = FindNextPair(pairs, i + 1, 10)
                Dim yPair = FindNextPair(pairs, i + 1, 20)
                If xPair IsNot Nothing Then TryParseDoubleInvariant(xPair.Value, extMinX)
                If yPair IsNot Nothing Then TryParseDoubleInvariant(yPair.Value, extMinY)
            ElseIf variableName = "$EXTMAX" Then
                Dim xPair = FindNextPair(pairs, i + 1, 10)
                Dim yPair = FindNextPair(pairs, i + 1, 20)
                If xPair IsNot Nothing Then TryParseDoubleInvariant(xPair.Value, extMaxX)
                If yPair IsNot Nothing Then TryParseDoubleInvariant(yPair.Value, extMaxY)
            End If
        Next
    End Sub

    Private Shared Function FindNextPair(pairs As List(Of DxfPair), startIndex As Integer, code As Integer) As DxfPair
        For i As Integer = startIndex To Math.Min(pairs.Count - 1, startIndex + 12)
            If pairs(i).Code = code Then Return pairs(i)
            If pairs(i).Code = 9 OrElse pairs(i).Code = 0 Then Return Nothing
        Next
        Return Nothing
    End Function

    Private Shared Function EntitySectionPairs(pairs As List(Of DxfPair)) As List(Of DxfPair)
        Dim result As New List(Of DxfPair)()
        Dim inEntities As Boolean = False

        For i As Integer = 0 To pairs.Count - 1
            If pairs(i).Code = 0 AndAlso String.Equals(pairs(i).Value.Trim(), "SECTION", StringComparison.OrdinalIgnoreCase) Then
                Dim sectionName = If(i + 1 < pairs.Count AndAlso pairs(i + 1).Code = 2, pairs(i + 1).Value.Trim(), "")
                inEntities = String.Equals(sectionName, "ENTITIES", StringComparison.OrdinalIgnoreCase)
                If inEntities Then i += 1
                Continue For
            End If

            If pairs(i).Code = 0 AndAlso String.Equals(pairs(i).Value.Trim(), "ENDSEC", StringComparison.OrdinalIgnoreCase) Then
                If inEntities Then Exit For
                Continue For
            End If

            If inEntities Then result.Add(pairs(i))
        Next

        Return result
    End Function

    Private Shared Function ReadBlockDefinitions(pairs As List(Of DxfPair)) As Dictionary(Of String, List(Of DxfPair))
        Dim blocks As New Dictionary(Of String, List(Of DxfPair))(StringComparer.OrdinalIgnoreCase)
        Dim inBlocksSection As Boolean = False
        Dim i As Integer = 0

        While i < pairs.Count
            If pairs(i).Code = 0 AndAlso String.Equals(pairs(i).Value.Trim(), "SECTION", StringComparison.OrdinalIgnoreCase) Then
                Dim sectionName = If(i + 1 < pairs.Count AndAlso pairs(i + 1).Code = 2, pairs(i + 1).Value.Trim(), "")
                inBlocksSection = String.Equals(sectionName, "BLOCKS", StringComparison.OrdinalIgnoreCase)
                i += If(inBlocksSection, 2, 1)
                Continue While
            End If

            If pairs(i).Code = 0 AndAlso String.Equals(pairs(i).Value.Trim(), "ENDSEC", StringComparison.OrdinalIgnoreCase) Then
                If inBlocksSection Then Exit While
                i += 1
                Continue While
            End If

            If inBlocksSection AndAlso pairs(i).Code = 0 AndAlso String.Equals(pairs(i).Value.Trim(), "BLOCK", StringComparison.OrdinalIgnoreCase) Then
                Dim headerPairs As New List(Of DxfPair)()
                i += 1
                While i < pairs.Count AndAlso pairs(i).Code <> 0
                    headerPairs.Add(pairs(i))
                    i += 1
                End While

                Dim blockName = FirstValue(headerPairs, 2).Trim()
                Dim blockPairs As New List(Of DxfPair)()
                While i < pairs.Count
                    If pairs(i).Code = 0 AndAlso String.Equals(pairs(i).Value.Trim(), "ENDBLK", StringComparison.OrdinalIgnoreCase) Then
                        i += 1
                        Exit While
                    End If
                    blockPairs.Add(pairs(i))
                    i += 1
                End While

                If blockName <> "" Then blocks(blockName) = blockPairs
                Continue While
            End If

            i += 1
        End While

        Return blocks
    End Function

    Private Shared Function ParseDimensionEntity(entityPairs As List(Of DxfPair),
                                                 unitText As String,
                                                 blockDefinitions As Dictionary(Of String, List(Of DxfPair))) As CadDimensionCandidate
        Dim displayText = FirstValue(entityPairs, 1)

        Dim nominal As Decimal
        Dim nominalRead As Boolean = TryParseDecimalInvariant(FirstValue(entityPairs, 42), nominal)
        If Not nominalRead Then nominalRead = TryParseNominalFromText(displayText, nominal)

        If Not nominalRead AndAlso String.IsNullOrWhiteSpace(displayText) Then Return Nothing

        Dim lowerTolerance As Decimal = 0D
        Dim upperTolerance As Decimal = 0D
        TryParseToleranceFromText(displayText, lowerTolerance, upperTolerance)

        Dim rawX As Double = 0R
        Dim rawY As Double = 0R
        Dim hasPoint = TryReadPoint(entityPairs, 11, 21, rawX, rawY)
        If Not hasPoint Then hasPoint = TryReadPoint(entityPairs, 10, 20, rawX, rawY)
        If Not hasPoint Then
            Dim x1 As Double
            Dim y1 As Double
            Dim x2 As Double
            Dim y2 As Double
            If TryReadPoint(entityPairs, 13, 23, x1, y1) AndAlso TryReadPoint(entityPairs, 14, 24, x2, y2) Then
                rawX = (x1 + x2) / 2.0R
                rawY = (y1 + y2) / 2.0R
                hasPoint = True
            End If
        End If

        Dim warning = ""
        If Not nominalRead Then warning = AppendWarning(warning, "Nominal değer DXF'ten kesin okunamadı; önizlemede kontrol edin.")
        If Not hasPoint Then warning = AppendWarning(warning, "Konum okunamadı; X/Y değerini PDF üzerinde düzeltin.")

        Dim typeCode As Integer
        Integer.TryParse(FirstValue(entityPairs, 70), typeCode)
        Dim dimensionType = DimensionTypeText(typeCode)
        Dim measureName = BuildDimensionMeasureName(displayText, dimensionType, nominal, nominalRead)
        If Not nominalRead AndAlso IsStandaloneToleranceText(measureName) Then Return Nothing

        Dim textHeight As Double = 0R
        TryParseDoubleInvariant(FirstValue(entityPairs, 40), textHeight)
        Dim textRotation As Double = 0R
        Dim hasTextRotation = TryReadDimensionTextRotation(entityPairs, textRotation)
        Dim blockTextCandidate = FindDimensionBlockTextCandidate(
            blockDefinitions,
            FirstValue(entityPairs, 2),
            nominal,
            nominalRead,
            unitText)
        If blockTextCandidate IsNot Nothing Then
            rawX = blockTextCandidate.RawX
            rawY = blockTextCandidate.RawY
            nominal = blockTextCandidate.Nominal
            nominalRead = True
            If Math.Abs(blockTextCandidate.LowerTolerance) > 0D OrElse Math.Abs(blockTextCandidate.UpperTolerance) > 0D Then
                lowerTolerance = blockTextCandidate.LowerTolerance
                upperTolerance = blockTextCandidate.UpperTolerance
            End If

            If blockTextCandidate.RawTextHeight > 0R Then textHeight = blockTextCandidate.RawTextHeight
            If blockTextCandidate.HasRawTextRotation Then
                textRotation = blockTextCandidate.RawTextRotationDegrees
                hasTextRotation = True
            End If

            Dim blockMeasureName = If(blockTextCandidate.MeasureName, "").Trim()
            If blockMeasureName <> "" Then
                measureName = blockMeasureName
            End If
        End If

        Return New CadDimensionCandidate With {
            .EntityHandle = FirstValue(entityPairs, 5),
            .LayerName = FirstValue(entityPairs, 8),
            .LayoutName = If(FirstValue(entityPairs, 410).Trim() = "", If(FirstValue(entityPairs, 67).Trim() = "1", "Paper", "Model"), FirstValue(entityPairs, 410)),
            .DimensionType = dimensionType,
            .DisplayText = displayText,
            .MeasureName = measureName,
            .Nominal = nominal,
            .LowerTolerance = -Math.Abs(lowerTolerance),
            .UpperTolerance = Math.Abs(upperTolerance),
            .Unit = unitText,
            .RawX = rawX,
            .RawY = rawY,
            .RawTextHeight = textHeight,
            .RawTextRotationDegrees = textRotation,
            .HasRawTextRotation = hasTextRotation,
            .WarningText = warning
        }
    End Function

    Private Shared Function ParseTextDimensionCandidates(pairs As List(Of DxfPair), unitText As String) As List(Of CadDimensionCandidate)
        Dim result As New List(Of CadDimensionCandidate)()

        For i As Integer = 0 To pairs.Count - 1
            If pairs(i).Code <> 0 Then Continue For

            Dim entityType = pairs(i).Value.Trim().ToUpperInvariant()
            If entityType <> "TEXT" AndAlso entityType <> "MTEXT" Then Continue For

            Dim entityPairs As New List(Of DxfPair)()
            i += 1
            While i < pairs.Count AndAlso pairs(i).Code <> 0
                entityPairs.Add(pairs(i))
                i += 1
            End While
            i -= 1

            Dim candidate = ParseTextDimensionEntity(entityPairs, entityType, unitText)
            If candidate IsNot Nothing Then result.Add(candidate)
        Next

        Return result
    End Function

    Private Shared Function ParseDimensionBlockTextCandidates(blockDefinitions As Dictionary(Of String, List(Of DxfPair)),
                                                             dimensionBlockNames As HashSet(Of String),
                                                             unitText As String) As List(Of CadDimensionCandidate)
        Dim result As New List(Of CadDimensionCandidate)()
        If blockDefinitions Is Nothing OrElse blockDefinitions.Count = 0 Then Return result
        If dimensionBlockNames Is Nothing OrElse dimensionBlockNames.Count = 0 Then Return result

        For Each blockName In dimensionBlockNames
            If Not blockDefinitions.ContainsKey(blockName) Then Continue For
            For Each candidate In ParseTextDimensionCandidates(blockDefinitions(blockName), unitText)
                candidate.DimensionType = candidate.DimensionType & "_BLOCK"
                candidate.WarningText = AppendWarning(candidate.WarningText, "DIMENSION bloğu içindeki ölçü yazısından okundu.")
                result.Add(candidate)
            Next
        Next

        Return result
    End Function

    Private Shared Function FindDimensionBlockTextCandidate(blockDefinitions As Dictionary(Of String, List(Of DxfPair)),
                                                           blockName As String,
                                                           nominal As Decimal,
                                                           nominalRead As Boolean,
                                                           unitText As String) As CadDimensionCandidate
        blockName = If(blockName, "").Trim()
        If blockDefinitions Is Nothing OrElse blockDefinitions.Count = 0 OrElse blockName = "" Then Return Nothing
        If Not blockDefinitions.ContainsKey(blockName) Then Return Nothing

        Dim textCandidates = ParseTextDimensionCandidates(blockDefinitions(blockName), unitText)
        If textCandidates.Count = 0 Then Return Nothing

        Return textCandidates.
            OrderByDescending(Function(candidate) BlockDimensionTextScore(candidate, nominal, nominalRead)).
            ThenByDescending(Function(candidate) CleanDimensionText(candidate.MeasureName).Length).
            FirstOrDefault()
    End Function

    Private Shared Function BlockDimensionTextScore(candidate As CadDimensionCandidate,
                                                   referenceNominal As Decimal,
                                                   referenceNominalRead As Boolean) As Double
        If candidate Is Nothing Then Return Double.MinValue

        Dim text = CleanDimensionText(candidate.MeasureName).Trim()
        If text = "" Then Return -1000.0R

        Dim compact = Regex.Replace(text, "\s+", "")
        Dim score As Double = 0R
        Dim plusMinus = ChrW(&HB1).ToString()

        If IsStandaloneToleranceText(text) Then score -= 500R
        If compact.Contains(plusMinus) OrElse Regex.IsMatch(compact, "\d[+-]\d") Then score += 40R
        If compact.Contains(ChrW(&HD8).ToString()) OrElse Regex.IsMatch(compact, "^R\d", RegexOptions.IgnoreCase) Then score += 16R
        If Regex.IsMatch(compact, "^\d+x", RegexOptions.IgnoreCase) Then score += 12R
        If candidate.HasRawTextRotation Then score += 5R

        Dim firstNumeric = Regex.Match(compact, "\d+(?:[.,]\d+)?")
        If firstNumeric.Success Then
            Dim firstValue As Decimal
            If NumberUtil.TryParseDecimal(firstNumeric.Value, firstValue) Then
                If Math.Abs(firstValue) > 1D Then score += 15R
                If Math.Abs(firstValue) >= 10D Then score += 10R
                If referenceNominalRead AndAlso
                   Math.Abs(firstValue - referenceNominal) <= Math.Max(0.0001D, Math.Abs(referenceNominal) * 0.0001D) Then
                    score += 8R
                End If
            End If
            score += Math.Min(10, firstNumeric.Value.Length)
        End If

        score += Math.Min(25, compact.Length)
        Return score
    End Function

    Private Shared Function ShouldPreferBlockMeasureText(currentText As String, blockText As String) As Boolean
        Dim currentClean = CleanDimensionText(currentText).Trim()
        Dim blockClean = CleanDimensionText(blockText).Trim()
        If blockClean = "" Then Return False
        If currentClean = "" Then Return True
        If currentClean = blockClean Then Return False

        Dim plusMinus = ChrW(&HB1).ToString()
        If blockClean.Contains(plusMinus) AndAlso Not currentClean.Contains(plusMinus) Then Return True
        If blockClean.Contains(ChrW(&HD8).ToString()) AndAlso Not currentClean.Contains(ChrW(&HD8).ToString()) Then Return True
        If Regex.IsMatch(blockClean, "[+-]\s*\d") AndAlso Not Regex.IsMatch(currentClean, "[+-]\s*\d") Then Return True
        Return blockClean.Length > currentClean.Length + 2
    End Function

    Private Shared Function ParseTextDimensionEntity(entityPairs As List(Of DxfPair), entityType As String, unitText As String) As CadDimensionCandidate
        Dim displayText = If(entityType = "MTEXT", MTextValue(entityPairs), FirstValue(entityPairs, 1))
        If IsStandaloneStackedToleranceText(displayText) OrElse IsStandaloneToleranceText(displayText) Then Return Nothing
        If Not LooksLikeMeasurementText(displayText) Then Return Nothing

        Dim nominal As Decimal
        If Not TryParseNominalFromText(displayText, nominal) Then Return Nothing

        Dim rawX As Double = 0R
        Dim rawY As Double = 0R
        Dim hasPoint = TryReadTextAnchorPoint(entityPairs, entityType, rawX, rawY)

        Dim textRotation As Double = 0R
        Dim hasTextRotation = TryReadTextRotation(entityPairs, entityType, textRotation)
        Dim textHeight As Double = 0R
        TryParseDoubleInvariant(FirstValue(entityPairs, 40), textHeight)

        Dim lowerTolerance As Decimal = 0D
        Dim upperTolerance As Decimal = 0D
        TryParseToleranceFromText(displayText, lowerTolerance, upperTolerance)

        Dim warning = "Gerçek DIMENSION nesnesi bulunamadı; sayısal TEXT/MTEXT ölçü adayı olarak okundu. Önizlemede kontrol edin."
        If Not hasPoint Then warning = AppendWarning(warning, "Konum okunamadı; X/Y değerini teknik resim üzerinde düzeltin.")

        Dim cleanedText = CleanDimensionText(displayText).Trim()
        If cleanedText = "" Then cleanedText = NumberUtil.DecToCsv(nominal)

        Return New CadDimensionCandidate With {
            .EntityHandle = FirstValue(entityPairs, 5),
            .LayerName = FirstValue(entityPairs, 8),
            .LayoutName = If(FirstValue(entityPairs, 410).Trim() = "", If(FirstValue(entityPairs, 67).Trim() = "1", "Paper", "Model"), FirstValue(entityPairs, 410)),
            .DimensionType = entityType & "_NUMERIC",
            .DisplayText = displayText,
            .MeasureName = cleanedText,
            .Nominal = nominal,
            .LowerTolerance = -Math.Abs(lowerTolerance),
            .UpperTolerance = Math.Abs(upperTolerance),
            .Unit = unitText,
            .RawX = rawX,
            .RawY = rawY,
            .RawTextHeight = textHeight,
            .RawTextRotationDegrees = textRotation,
            .HasRawTextRotation = hasTextRotation,
            .WarningText = warning
        }
    End Function

    Private Shared Function MTextValue(entityPairs As List(Of DxfPair)) As String
        Dim parts = entityPairs.
            Where(Function(pair) pair.Code = 1 OrElse pair.Code = 3).
            Select(Function(pair) pair.Value).
            ToList()
        Return String.Join("", parts)
    End Function

    Private Shared Function FirstValue(pairs As List(Of DxfPair), code As Integer) As String
        Dim found = pairs.FirstOrDefault(Function(pair) pair.Code = code)
        Return If(found Is Nothing, "", found.Value)
    End Function

    Private Shared Function TryReadPoint(pairs As List(Of DxfPair), xCode As Integer, yCode As Integer, ByRef x As Double, ByRef y As Double) As Boolean
        Dim xPair = pairs.FirstOrDefault(Function(pair) pair.Code = xCode)
        Dim yPair = pairs.FirstOrDefault(Function(pair) pair.Code = yCode)
        If xPair Is Nothing OrElse yPair Is Nothing Then Return False
        Return TryParseDoubleInvariant(xPair.Value, x) AndAlso TryParseDoubleInvariant(yPair.Value, y)
    End Function

    Private Shared Function TryReadTextAnchorPoint(pairs As List(Of DxfPair),
                                                   entityType As String,
                                                   ByRef x As Double,
                                                   ByRef y As Double) As Boolean
        If String.Equals(entityType, "TEXT", StringComparison.OrdinalIgnoreCase) Then
            Dim horizontalAlign As Integer
            Dim verticalAlign As Integer
            Integer.TryParse(FirstValue(pairs, 72), horizontalAlign)
            Integer.TryParse(FirstValue(pairs, 73), verticalAlign)

            If (horizontalAlign <> 0 OrElse verticalAlign <> 0) AndAlso TryReadPoint(pairs, 11, 21, x, y) Then
                Return True
            End If

            If TryReadPoint(pairs, 10, 20, x, y) Then Return True
            Return TryReadPoint(pairs, 11, 21, x, y)
        End If

        Return TryReadPoint(pairs, 10, 20, x, y)
    End Function

    Private Shared Function TryReadTextRotation(entityPairs As List(Of DxfPair),
                                                entityType As String,
                                                ByRef rotation As Double) As Boolean
        If TryParseDoubleInvariant(FirstValue(entityPairs, 50), rotation) Then Return True

        If String.Equals(entityType, "MTEXT", StringComparison.OrdinalIgnoreCase) Then
            Dim vectorX As Double
            Dim vectorY As Double
            If TryParseDoubleInvariant(FirstValue(entityPairs, 11), vectorX) AndAlso
               TryParseDoubleInvariant(FirstValue(entityPairs, 21), vectorY) AndAlso
               (Math.Abs(vectorX) > 0.0000001R OrElse Math.Abs(vectorY) > 0.0000001R) Then
                rotation = Math.Atan2(vectorY, vectorX) * 180.0R / Math.PI
                Return True
            End If
        End If

        rotation = 0R
        Return False
    End Function

    Private Shared Function TryReadDimensionTextRotation(entityPairs As List(Of DxfPair),
                                                        ByRef rotation As Double) As Boolean
        If TryParseDoubleInvariant(FirstValue(entityPairs, 53), rotation) Then Return True
        If TryParseDoubleInvariant(FirstValue(entityPairs, 50), rotation) Then Return True

        Dim x1 As Double
        Dim y1 As Double
        Dim x2 As Double
        Dim y2 As Double
        If TryReadPoint(entityPairs, 13, 23, x1, y1) AndAlso
           TryReadPoint(entityPairs, 14, 24, x2, y2) Then
            Dim dx = x2 - x1
            Dim dy = y2 - y1
            If Math.Abs(dx) > 0.000001R OrElse Math.Abs(dy) > 0.000001R Then
                rotation = Math.Atan2(dy, dx) * 180.0R / Math.PI
                Return True
            End If
        End If

        Dim definitionX As Double
        Dim definitionY As Double
        Dim textX As Double
        Dim textY As Double
        If TryReadPoint(entityPairs, 10, 20, definitionX, definitionY) AndAlso
           TryReadPoint(entityPairs, 11, 21, textX, textY) Then
            Dim dx = textX - definitionX
            Dim dy = textY - definitionY
            If Math.Abs(dx) > 0.000001R OrElse Math.Abs(dy) > 0.000001R Then
                rotation = Math.Atan2(dy, dx) * 180.0R / Math.PI
                Return True
            End If
        End If

        rotation = 0R
        Return False
    End Function

    Private Shared Sub ApplyPercentCoordinates(candidates As List(Of CadDimensionCandidate),
                                               extMinX As Double,
                                               extMinY As Double,
                                               extMaxX As Double,
                                               extMaxY As Double)
        If candidates.Count = 0 Then Return

        Dim usedHeaderExtents = Not Double.IsNaN(extMinX) AndAlso
                                Not Double.IsNaN(extMinY) AndAlso
                                Not Double.IsNaN(extMaxX) AndAlso
                                Not Double.IsNaN(extMaxY) AndAlso
                                extMaxX > extMinX AndAlso
                                extMaxY > extMinY

        If Not usedHeaderExtents Then
            extMinX = candidates.Min(Function(candidate) candidate.RawX)
            extMaxX = candidates.Max(Function(candidate) candidate.RawX)
            extMinY = candidates.Min(Function(candidate) candidate.RawY)
            extMaxY = candidates.Max(Function(candidate) candidate.RawY)
        End If

        Dim width = Math.Max(0.000001R, extMaxX - extMinX)
        Dim height = Math.Max(0.000001R, extMaxY - extMinY)

        For index As Integer = 0 To candidates.Count - 1
            Dim candidate = candidates(index)
            candidate.XPercent = ClampPercent(CDec((candidate.RawX - extMinX) * 100.0R / width))
            candidate.YPercent = ClampPercent(CDec(100.0R - ((candidate.RawY - extMinY) * 100.0R / height)))
            ShiftCandidateMarkerLeftOfText(candidate, width, height)
            If Not usedHeaderExtents Then
                candidate.WarningText = AppendWarning(candidate.WarningText, "DXF çizim sınırı bulunamadı; X/Y konumunu PDF üzerinde kontrol edin.")
            ElseIf candidate.XPercent <= 0D OrElse candidate.XPercent >= 100D OrElse
                   candidate.YPercent <= 0D OrElse candidate.YPercent >= 100D Then
                candidate.WarningText = AppendWarning(candidate.WarningText, "Konum çizim sınırına yakın; PDF üzerinde kontrol edin.")
            End If
        Next
    End Sub

    Private Shared Sub ShiftCandidateMarkerLeftOfText(candidate As CadDimensionCandidate,
                                                      drawingWidth As Double,
                                                      drawingHeight As Double)
        If candidate Is Nothing Then Return

        Dim angleDegrees = If(candidate.HasRawTextRotation, candidate.RawTextRotationDegrees, 0.0R)
        Dim angle = angleDegrees * Math.PI / 180.0R
        Dim maxDim = Math.Max(0.000001R, Math.Max(drawingWidth, drawingHeight))
        Dim textHeight = If(candidate.RawTextHeight > 0R, candidate.RawTextHeight, maxDim * 0.006R)
        Dim textLength = Math.Max(1, CleanDimensionText(If(candidate.MeasureName, candidate.DisplayText)).Count(Function(ch) Not Char.IsWhiteSpace(ch)))
        Dim estimatedHalfTextWidth = textHeight * Math.Min(3.6R, Math.Max(1.0R, textLength * 0.22R))
        Dim rawDistance = Math.Max(estimatedHalfTextWidth + (textHeight * 0.85R), maxDim * 0.006R)
        rawDistance = Math.Min(rawDistance, maxDim * 0.028R)

        Dim xShiftPercent = -(Math.Cos(angle) * rawDistance * 100.0R / Math.Max(0.000001R, drawingWidth))
        Dim yShiftPercent = Math.Sin(angle) * rawDistance * 100.0R / Math.Max(0.000001R, drawingHeight)

        candidate.XPercent = ClampPercent(candidate.XPercent + CDec(xShiftPercent))
        candidate.YPercent = ClampPercent(candidate.YPercent + CDec(yShiftPercent))
    End Sub

    Private Shared Function IsTextBasedCandidate(candidate As CadDimensionCandidate) As Boolean
        Dim dimensionType = If(candidate.DimensionType, "")
        Return dimensionType.IndexOf("TEXT", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               dimensionType.IndexOf("MTEXT", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               dimensionType.IndexOf("NUMERIC", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               candidate.HasRawTextRotation
    End Function

    Private Shared Function RemoveLikelyTitleBlockCandidates(candidates As List(Of CadDimensionCandidate)) As List(Of CadDimensionCandidate)
        If candidates Is Nothing Then Return New List(Of CadDimensionCandidate)()
        Return candidates.
            Where(Function(candidate) Not IsLikelyTitleBlockCandidate(candidate)).
            ToList()
    End Function

    Private Shared Function IsLikelyTitleBlockCandidate(candidate As CadDimensionCandidate) As Boolean
        If candidate Is Nothing Then Return False

        Dim name = CleanDimensionText(candidate.MeasureName).Trim()
        If IsZeroOnlyText(name) Then Return True
        If IsStandaloneToleranceText(name) Then Return True

        Return False
    End Function

    Private Shared Function DeduplicateCandidates(candidates As List(Of CadDimensionCandidate)) As List(Of CadDimensionCandidate)
        Dim result As New List(Of CadDimensionCandidate)()
        If candidates Is Nothing Then Return result

        For Each candidate In candidates
            Dim duplicate = result.FirstOrDefault(Function(existing) AreDuplicateCandidates(existing, candidate))
            If duplicate Is Nothing Then
                result.Add(candidate)
            Else
                MergeCandidateDetails(duplicate, candidate)
            End If
        Next

        Return result
    End Function

    Private Shared Function AreDuplicateCandidates(left As CadDimensionCandidate, right As CadDimensionCandidate) As Boolean
        If left Is Nothing OrElse right Is Nothing Then Return False
        Dim leftHandle = If(left.EntityHandle, "").Trim()
        Dim rightHandle = If(right.EntityHandle, "").Trim()
        If leftHandle <> "" AndAlso rightHandle <> "" Then
            Return String.Equals(leftHandle, rightHandle, StringComparison.OrdinalIgnoreCase)
        End If

        Return False
    End Function

    Private Shared Function NormalizeCandidateKey(value As String) As String
        Dim text = CleanDimensionText(value)
        text = Regex.Replace(text, "\s+", "")
        Return text.Trim()
    End Function

    Private Shared Sub MergeCandidateDetails(target As CadDimensionCandidate, source As CadDimensionCandidate)
        If target Is Nothing OrElse source Is Nothing Then Return

        If Math.Abs(target.LowerTolerance) = 0D AndAlso Math.Abs(source.LowerTolerance) > 0D Then
            target.LowerTolerance = source.LowerTolerance
        End If
        If Math.Abs(target.UpperTolerance) = 0D AndAlso Math.Abs(source.UpperTolerance) > 0D Then
            target.UpperTolerance = source.UpperTolerance
        End If
        If String.IsNullOrWhiteSpace(target.MeasureName) AndAlso Not String.IsNullOrWhiteSpace(source.MeasureName) Then
            target.MeasureName = source.MeasureName
        End If
        target.WarningText = AppendWarning(target.WarningText, source.WarningText)
    End Sub

    Private Shared Function TryParseNominalFromText(text As String, ByRef nominal As Decimal) As Boolean
        Dim cleaned = CleanDimensionText(text)
        Dim normalizedCountPrefix = Regex.Match(cleaned, "^\s*\d+\s*x\s*" & DiameterPrefixPattern() & "\s*(\d+(?:[.,]\d+)?)", RegexOptions.IgnoreCase)
        If normalizedCountPrefix.Success Then Return NumberUtil.TryParseDecimal(normalizedCountPrefix.Groups(1).Value, nominal)

        Dim countPrefix = Regex.Match(cleaned, "^\s*\d+\s*x\s*[ØR⌀]?\s*(\d+(?:[.,]\d+)?)", RegexOptions.IgnoreCase)
        If countPrefix.Success Then Return NumberUtil.TryParseDecimal(countPrefix.Groups(1).Value, nominal)

        Dim match = Regex.Match(cleaned, "[-+]?\d+(?:[.,]\d+)?")
        If Not match.Success Then Return False
        Return NumberUtil.TryParseDecimal(match.Value, nominal)
    End Function

    Private Shared Function LooksLikeMeasurementText(text As String) As Boolean
        Dim cleaned = CleanDimensionText(text).Trim()
        If cleaned = "" Then Return False
        If IsZeroOnlyText(cleaned) Then Return False
        If IsStandaloneToleranceText(cleaned) Then Return False

        If cleaned.IndexOf("DXF", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
           cleaned.IndexOf("CAD", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
           cleaned.IndexOf("FORMAT", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
           cleaned.IndexOf("SAMPLE", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
           cleaned.IndexOf("FIRMA", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
           cleaned.IndexOf("COMPANY", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
           cleaned.IndexOf("TARIH", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
           cleaned.IndexOf("TARİH", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
           cleaned.IndexOf("RESIM", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
           cleaned.IndexOf("RESİM", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
           cleaned.IndexOf("REV", StringComparison.OrdinalIgnoreCase) >= 0 Then
            Return False
        End If

        If Regex.IsMatch(cleaned, "\d{4}[-/.]\d{1,2}[-/.]\d{1,2}") Then Return False
        If Regex.IsMatch(cleaned, "^\d{1,2}:\d{1,2}") Then Return False
        If Regex.IsMatch(cleaned, "^[A-Z]{1,4}[-_/][A-Z0-9]+", RegexOptions.IgnoreCase) Then Return False

        Dim diameterPrefix = DiameterPrefixPattern()
        Dim plusMinus = Regex.Escape(ChrW(&HB1))
        If Regex.IsMatch(cleaned, "^\s*\d+\s*x\s*" & diameterPrefix & "\s*\d+(?:[.,]\d+)?\s*(?:" & plusMinus & "\s*\d+(?:[.,]\d+)?)?\s*$", RegexOptions.IgnoreCase) Then
            Return True
        End If

        If Regex.IsMatch(cleaned, "^\s*" & diameterPrefix & "\s*\d+(?:[.,]\d+)?\s*(?:" & plusMinus & "\s*\d+(?:[.,]\d+)?|[+-]\s*\d+(?:[.,]\d+)?)?\s*$", RegexOptions.IgnoreCase) Then
            Return True
        End If

        If Regex.IsMatch(cleaned, "^\s*(?:R\s*)?\d+(?:[.,]\d+)?\s*(?:" & plusMinus & "\s*\d+(?:[.,]\d+)?|[+-]\s*\d+(?:[.,]\d+)?)?\s*$", RegexOptions.IgnoreCase) Then
            Return True
        End If

        Dim numericMatches = Regex.Matches(cleaned, "[-+]?\d+(?:[.,]\d+)?")
        If numericMatches.Count <> 1 Then Return False
        If cleaned.Length > 18 Then Return False

        If Regex.IsMatch(cleaned, "^\s*(?:[ØR⌀]?\s*)?\d+(?:[.,]\d+)?\s*(?:±\s*\d+(?:[.,]\d+)?|[+-]\s*\d+(?:[.,]\d+)?)?\s*$", RegexOptions.IgnoreCase) Then
            Return True
        End If

        If Regex.IsMatch(cleaned, "^\s*\d+\s*x\s*[ØR⌀]?\s*\d+(?:[.,]\d+)?\s*$", RegexOptions.IgnoreCase) Then
            Return True
        End If

        Return False
    End Function

    Private Shared Function DiameterPrefixPattern() As String
        Return "(?:" & Regex.Escape(ChrW(&HD8)) & "|" & Regex.Escape(ChrW(&HF8)) & "|" & Regex.Escape(ChrW(&H2300)) & "|R)?"
    End Function

    Private Shared Function DiameterRequiredPattern() As String
        Return "(?:" & Regex.Escape(ChrW(&HD8)) & "|" & Regex.Escape(ChrW(&HF8)) & "|" & Regex.Escape(ChrW(&H2300)) & "|R)"
    End Function

    Private Shared Function IsZeroOnlyText(text As String) As Boolean
        Dim cleaned = Regex.Replace(If(text, "").Trim(), "\s+", "")
        Return Regex.IsMatch(cleaned, "^0+(?:[.,]0+)?$")
    End Function

    Private Shared Sub TryParseToleranceFromText(text As String, ByRef lowerTolerance As Decimal, ByRef upperTolerance As Decimal)
        Dim cleaned = CleanDimensionText(text)
        Dim plusMinus = Regex.Match(cleaned, "[±\u00B1]\s*(\d+(?:[.,]\d+)?)")
        If plusMinus.Success Then
            Dim value As Decimal
            If NumberUtil.TryParseDecimal(plusMinus.Groups(1).Value, value) Then
                lowerTolerance = -Math.Abs(value)
                upperTolerance = Math.Abs(value)
                Return
            End If
        End If

        Dim signed = Regex.Matches(cleaned, "([+-])\s*(\d+(?:[.,]\d+)?)")
        For Each item As Match In signed
            Dim value As Decimal
            If Not NumberUtil.TryParseDecimal(item.Groups(2).Value, value) Then Continue For
            If item.Groups(1).Value = "-" Then
                lowerTolerance = -Math.Abs(value)
            Else
                upperTolerance = Math.Abs(value)
            End If
        Next
    End Sub

    Private Shared Function CleanDimensionText(text As String) As String
        Return NormalizeDxfDimensionText(text)
    End Function

    Private Shared Function CleanDimensionTextLegacyUnused(text As String) As String
        Return If(text, "").
            Replace("<>", " ").
            Replace("%%c", "Ø").
            Replace("%%C", "Ø").
            Replace("\X", " ").
            Replace("\P", " ").
            Replace("{", " ").
            Replace("}", " ")
    End Function

    Private Shared Function NormalizeDxfDimensionText(text As String) As String
        Dim result = If(text, "")
        result = Regex.Replace(result, "\\S([^;]*);", Function(match As Match) FormatStackedText(match.Groups(1).Value))
        result = Regex.Replace(result, "\\[ACcFfHhQqTtWw][^;]*;", " ")
        result = Regex.Replace(result, "\\[LlOoKk]", "")
        result = result.
            Replace("<>", " ").
            Replace("%%c", "Ã˜").
            Replace("%%C", "Ã˜").
            Replace("%%d", "°").
            Replace("%%D", "°").
            Replace("%%p", ChrW(&HB1)).
            Replace("%%P", ChrW(&HB1)).
            Replace("\X", " ").
            Replace("\P", " ").
            Replace("\~", " ").
            Replace("{", " ").
            Replace("}", " ")
        result = NormalizeDxfSymbolMojibake(result)
        result = Regex.Replace(result, "\s+", " ")
        Return result.Trim()
    End Function

    Private Shared Function NormalizeDxfSymbolMojibake(text As String) As String
        Dim result = If(text, "")
        Dim diameter = ChrW(&HD8)
        Dim plusMinus = ChrW(&HB1)
        Dim degree = ChrW(&HB0)

        result = result.
            Replace(ChrW(&HC3) & ChrW(&H2DC), diameter).
            Replace(ChrW(&H41) & ChrW(&H2DC), diameter).
            Replace(ChrW(&HC3) & ChrW(&H192) & ChrW(&HCB) & ChrW(&H153), diameter).
            Replace(ChrW(&HC3) & ChrW(&H192) & ChrW(&HC2) & ChrW(&HB8), diameter).
            Replace(ChrW(&HC3) & ChrW(&HB8), diameter).
            Replace(ChrW(&HE2) & ChrW(&H152) & ChrW(&H20AC), diameter).
            Replace(ChrW(&HC2) & ChrW(&HB1), plusMinus).
            Replace(ChrW(&HC2) & ChrW(&HB0), degree)

        Return result
    End Function

    Private Shared Function FormatStackedText(payload As String) As String
        Dim value = If(payload, "").Trim()
        If value = "" Then Return " "

        Dim caretParts = value.Split("^"c)
        If caretParts.Length = 2 Then
            Dim upper = CleanStackPart(caretParts(0))
            Dim lower = CleanStackPart(caretParts(1))
            If upper <> "" AndAlso lower <> "" Then
                If AreSameNumericText(upper, lower) Then Return " " & ChrW(&HB1) & upper
                Return " " & EnsureSignedText(upper, True) & "/" & EnsureSignedText(lower, False)
            End If
        End If

        Dim fractionParts = value.Split("/"c)
        If fractionParts.Length = 2 Then
            Return " " & CleanStackPart(fractionParts(0)) & "/" & CleanStackPart(fractionParts(1))
        End If

        Return " " & CleanStackPart(value)
    End Function

    Private Shared Function CleanStackPart(value As String) As String
        Return If(value, "").
            Replace("{", "").
            Replace("}", "").
            Replace(";", "").
            Trim()
    End Function

    Private Shared Function EnsureSignedText(value As String, positiveDefault As Boolean) As String
        value = CleanStackPart(value)
        If value.StartsWith("+", StringComparison.Ordinal) OrElse value.StartsWith("-", StringComparison.Ordinal) Then Return value
        Return If(positiveDefault, "+", "-") & value
    End Function

    Private Shared Function AreSameNumericText(leftText As String, rightText As String) As Boolean
        Dim leftValue As Double
        Dim rightValue As Double
        If Not TryParseDoubleInvariant(CleanStackPart(leftText).TrimStart("+"c, "-"c), leftValue) Then Return False
        If Not TryParseDoubleInvariant(CleanStackPart(rightText).TrimStart("+"c, "-"c), rightValue) Then Return False
        Return Math.Abs(Math.Abs(leftValue) - Math.Abs(rightValue)) < 0.0000001R
    End Function

    Private Shared Function IsStandaloneStackedToleranceText(text As String) As Boolean
        Dim raw = If(text, "").Trim()
        Return Regex.IsMatch(raw, "^\\S\s*[+-]?\d+(?:[.,]\d+)?\s*\^\s*[+-]?\d+(?:[.,]\d+)?\s*;$")
    End Function

    Private Shared Function IsStandaloneToleranceText(text As String) As Boolean
        Dim cleaned = CleanDimensionText(text).Trim()
        If cleaned = "" Then Return False

        Dim plusMinus = Regex.Escape(ChrW(&HB1))
        Dim numberPattern = "\d+(?:[.,]\d+)?"
        Dim diameter = DiameterRequiredPattern()

        If Regex.IsMatch(cleaned, "^\s*" & plusMinus & "\s*" & numberPattern & "\s*$") Then Return True
        If Regex.IsMatch(cleaned, "^\s*[+-]\s*" & numberPattern & "\s*$") Then Return True
        If Regex.IsMatch(cleaned, "^\s*" & diameter & "\s*" & plusMinus & "\s*" & numberPattern & "\s*$", RegexOptions.IgnoreCase) Then Return True
        If Regex.IsMatch(cleaned, "^\s*" & diameter & "\s*[+-]\s*" & numberPattern & "\s*$", RegexOptions.IgnoreCase) Then Return True

        Return False
    End Function

    Private Shared Function DimensionTypeText(typeCode As Integer) As String
        Select Case (typeCode And 7)
            Case 1
                Return "ALIGNED"
            Case 2
                Return "ANGULAR"
            Case 3
                Return "DIAMETRIC"
            Case 4
                Return "RADIAL"
            Case 5
                Return "ANGULAR_3_POINT"
            Case 6
                Return "ORDINATE"
            Case Else
                Return "LINEAR"
        End Select
    End Function

    Private Shared Function BuildMeasureName(displayText As String, dimensionType As String) As String
        Dim text = CleanDimensionText(displayText).Trim()
        If text <> "" Then Return text

        Dim typeText = If(dimensionType, "").ToUpperInvariant()
        If typeText.Contains("DIAMETRIC") Then Return "Çap ölçüsü"
        If typeText.Contains("RADIAL") Then Return "Radyüs ölçüsü"
        If typeText.Contains("ANGULAR") Then Return "Açı ölçüsü"
        If typeText.Contains("ORDINATE") Then Return "Koordinat ölçüsü"
        If typeText.Contains("ALIGNED") Then Return "Hizalı ölçü"
        Return "Doğrusal ölçü"
    End Function

    Private Shared Function BuildDimensionMeasureName(displayText As String,
                                                      dimensionType As String,
                                                      nominal As Decimal,
                                                      nominalRead As Boolean) As String
        Dim text = CleanDimensionText(displayText).Trim()
        If text <> "" AndAlso Not IsStandaloneToleranceText(text) Then Return text

        If nominalRead Then
            Dim nominalText = NumberUtil.DecToCsv(nominal)
            Dim prefix = DimensionPrefixText(dimensionType)
            If IsStandaloneToleranceText(text) Then
                Return prefix & nominalText & ExtractToleranceSuffix(text)
            End If

            Return prefix & nominalText
        End If

        Return BuildMeasureName(displayText, dimensionType)
    End Function

    Private Shared Function DimensionPrefixText(dimensionType As String) As String
        Dim typeText = If(dimensionType, "").ToUpperInvariant()
        If typeText.Contains("DIAMETRIC") Then Return ChrW(&HD8)
        If typeText.Contains("RADIAL") Then Return "R"
        Return ""
    End Function

    Private Shared Function ExtractToleranceSuffix(text As String) As String
        Dim cleaned = CleanDimensionText(text).Trim()
        Dim plusMinus = ChrW(&HB1).ToString()
        Dim plusMinusIndex = cleaned.IndexOf(plusMinus, StringComparison.Ordinal)
        If plusMinusIndex >= 0 Then Return cleaned.Substring(plusMinusIndex).Replace(" ", "")

        Dim signed = Regex.Match(cleaned, "[+-]\s*\d+(?:[.,]\d+)?")
        If signed.Success Then Return signed.Value.Replace(" ", "")

        Return ""
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

    Private Shared Function ClampPercent(value As Decimal) As Decimal
        Return Math.Round(Math.Max(0.01D, Math.Min(99.99D, value)), 2)
    End Function

    Private Shared Function TryParseDoubleInvariant(text As String, ByRef value As Double) As Boolean
        Return Double.TryParse(If(text, ""), NumberStyles.Any, CultureInfo.InvariantCulture, value) OrElse
               Double.TryParse(If(text, "").Replace(","c, "."c), NumberStyles.Any, CultureInfo.InvariantCulture, value)
    End Function

    Private Shared Function TryParseDecimalInvariant(text As String, ByRef value As Decimal) As Boolean
        Return Decimal.TryParse(If(text, ""), NumberStyles.Any, CultureInfo.InvariantCulture, value) OrElse
               NumberUtil.TryParseDecimal(text, value)
    End Function

    Private Shared Function AppendWarning(currentWarning As String, warning As String) As String
        If String.IsNullOrWhiteSpace(currentWarning) Then Return warning
        Return currentWarning.Trim() & " " & warning
    End Function
End Class
