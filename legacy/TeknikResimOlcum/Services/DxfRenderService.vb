Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Security
Imports System.Text
Imports System.Text.RegularExpressions

Public Class DxfRenderResult
    Public Property SvgPath As String = ""
    Public Property AspectRatioText As String = "1 / 1"
End Class

Public NotInheritable Class DxfRenderService
    Private Sub New()
    End Sub

    Private Class DxfPair
        Public Property Code As Integer
        Public Property Value As String = ""
    End Class

    Private Class DxfBounds
        Public Property HasValue As Boolean
        Public Property MinX As Double
        Public Property MinY As Double
        Public Property MaxX As Double
        Public Property MaxY As Double

        Public Sub Include(x As Double, y As Double)
            If Not HasValue Then
                HasValue = True
                MinX = x
                MaxX = x
                MinY = y
                MaxY = y
                Return
            End If

            MinX = Math.Min(MinX, x)
            MaxX = Math.Max(MaxX, x)
            MinY = Math.Min(MinY, y)
            MaxY = Math.Max(MaxY, y)
        End Sub

        Public Sub IncludeCircle(cx As Double, cy As Double, radius As Double)
            Include(cx - radius, cy - radius)
            Include(cx + radius, cy + radius)
        End Sub
    End Class

    Private Class RenderShape
        Public Property BoundsAction As Action(Of DxfBounds)
        Public Property SvgFactory As Func(Of String)
        Public Property IsText As Boolean
    End Class

    Public Shared Function RenderToSvg(dxfPath As String) As DxfRenderResult
        dxfPath = If(dxfPath, "").Trim()
        If dxfPath = "" OrElse Not File.Exists(dxfPath) Then
            Throw New FileNotFoundException("DXF dosyası bulunamadı.", dxfPath)
        End If

        Dim pairs = ReadPairs(dxfPath)
        If pairs.Count = 0 Then Throw New InvalidDataException("DXF dosyası okunamadı. ASCII DXF olarak dışa aktarılmış olmalıdır.")

        Dim headerBounds = ReadHeaderBounds(pairs)
        Dim drawingPairs = EntitySectionPairs(pairs)
        If drawingPairs.Count = 0 Then drawingPairs = pairs

        Dim blockDefinitions = ReadBlockDefinitions(pairs)
        Dim shapes = ParseShapes(drawingPairs, blockDefinitions, True, New HashSet(Of String)(StringComparer.OrdinalIgnoreCase))
        Dim drawingBounds As New DxfBounds()
        For Each shape In shapes
            If shape.BoundsAction IsNot Nothing Then shape.BoundsAction.Invoke(drawingBounds)
        Next

        Dim bounds = If(headerBounds.HasValue AndAlso headerBounds.MaxX > headerBounds.MinX AndAlso headerBounds.MaxY > headerBounds.MinY,
                        headerBounds,
                        drawingBounds)

        If Not bounds.HasValue OrElse bounds.MaxX <= bounds.MinX OrElse bounds.MaxY <= bounds.MinY Then
            bounds = New DxfBounds()
            bounds.Include(0, 0)
            bounds.Include(100, 100)
        End If

        Dim width = Math.Max(0.000001R, bounds.MaxX - bounds.MinX)
        Dim height = Math.Max(0.000001R, bounds.MaxY - bounds.MinY)
        Dim svgWidth As Integer = 2000
        Dim svgHeight As Integer = Math.Max(200, CInt(Math.Round(svgWidth * height / width)))
        Dim strokeWidth = Math.Max(width, height) / 900.0R
        If strokeWidth <= 0 Then strokeWidth = 0.15R

        Dim sb As New StringBuilder()
        sb.AppendLine("<?xml version=""1.0"" encoding=""UTF-8""?>")
        sb.AppendLine("<svg xmlns=""http://www.w3.org/2000/svg"" width=""" & svgWidth.ToString(CultureInfo.InvariantCulture) & """ height=""" & svgHeight.ToString(CultureInfo.InvariantCulture) & """ viewBox=""" &
                      Num(bounds.MinX) & " " & Num(-bounds.MaxY) & " " & Num(width) & " " & Num(height) & """>")
        sb.AppendLine("<rect x=""" & Num(bounds.MinX) & """ y=""" & Num(-bounds.MaxY) & """ width=""" & Num(width) & """ height=""" & Num(height) & """ fill=""white""/>")
        sb.AppendLine("<g transform=""scale(1,-1)"" stroke=""#111827"" stroke-width=""" & Num(strokeWidth) & """ fill=""none"" stroke-linecap=""round"" stroke-linejoin=""round"" vector-effect=""non-scaling-stroke"">")
        For Each shape In shapes.Where(Function(item) Not item.IsText)
            sb.AppendLine(shape.SvgFactory.Invoke())
        Next
        sb.AppendLine("</g>")
        sb.AppendLine("<g fill=""#111827"" stroke=""none"" font-family=""Segoe UI, Arial, sans-serif"">")
        For Each shape In shapes.Where(Function(item) item.IsText)
            sb.AppendLine(shape.SvgFactory.Invoke())
        Next
        sb.AppendLine("</g>")
        sb.AppendLine("</svg>")

        Dim svgPath = Path.Combine(AppPaths.TempDir, "dxf_view_" & Guid.NewGuid().ToString("N") & ".svg")
        File.WriteAllText(svgPath, sb.ToString(), New UTF8Encoding(False))

        Return New DxfRenderResult With {
            .SvgPath = svgPath,
            .AspectRatioText = Num(width) & " / " & Num(height)
        }
    End Function

    Private Shared Function ReadPairs(dxfPath As String) As List(Of DxfPair)
        Dim bytes = File.ReadAllBytes(dxfPath)
        If bytes.Length >= 22 Then
            Dim header = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 22))
            If header.StartsWith("AutoCAD Binary DXF", StringComparison.OrdinalIgnoreCase) Then
                Throw New InvalidDataException("Binary DXF görüntülenemez. ASCII DXF olarak dışa aktarın.")
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

    Private Shared Function ReadHeaderBounds(pairs As List(Of DxfPair)) As DxfBounds
        Dim bounds As New DxfBounds()
        Dim minX As Double = Double.NaN
        Dim minY As Double = Double.NaN
        Dim maxX As Double = Double.NaN
        Dim maxY As Double = Double.NaN

        For i As Integer = 0 To pairs.Count - 2
            If pairs(i).Code <> 9 Then Continue For
            Dim variableName = pairs(i).Value.Trim().ToUpperInvariant()
            If variableName = "$EXTMIN" Then
                Dim xPair = FindNextPair(pairs, i + 1, 10)
                Dim yPair = FindNextPair(pairs, i + 1, 20)
                If xPair IsNot Nothing Then TryParseDouble(xPair.Value, minX)
                If yPair IsNot Nothing Then TryParseDouble(yPair.Value, minY)
            ElseIf variableName = "$EXTMAX" Then
                Dim xPair = FindNextPair(pairs, i + 1, 10)
                Dim yPair = FindNextPair(pairs, i + 1, 20)
                If xPair IsNot Nothing Then TryParseDouble(xPair.Value, maxX)
                If yPair IsNot Nothing Then TryParseDouble(yPair.Value, maxY)
            End If
        Next

        If Not Double.IsNaN(minX) AndAlso Not Double.IsNaN(minY) AndAlso Not Double.IsNaN(maxX) AndAlso Not Double.IsNaN(maxY) AndAlso maxX > minX AndAlso maxY > minY Then
            bounds.Include(minX, minY)
            bounds.Include(maxX, maxY)
        End If
        Return bounds
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

    Private Shared Function FindNextPair(pairs As List(Of DxfPair), startIndex As Integer, code As Integer) As DxfPair
        For i As Integer = startIndex To Math.Min(pairs.Count - 1, startIndex + 12)
            If pairs(i).Code = code Then Return pairs(i)
            If pairs(i).Code = 9 OrElse pairs(i).Code = 0 Then Return Nothing
        Next
        Return Nothing
    End Function

    Private Shared Function ParseShapes(pairs As List(Of DxfPair),
                                        blockDefinitions As Dictionary(Of String, List(Of DxfPair)),
                                        includeText As Boolean,
                                        activeBlocks As HashSet(Of String)) As List(Of RenderShape)
        Dim shapes As New List(Of RenderShape)()
        If activeBlocks Is Nothing Then activeBlocks = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim i As Integer = 0
        While i < pairs.Count
            If pairs(i).Code <> 0 Then
                i += 1
                Continue While
            End If

            Dim entityName = pairs(i).Value.Trim().ToUpperInvariant()
            If entityName = "POLYLINE" Then
                Dim polylineEntity = ReadEntityPairs(pairs, i + 1, i)
                Dim vertices As New List(Of Tuple(Of Double, Double))()
                Dim j = i + 1 + polylineEntity.Count
                While j < pairs.Count
                    If pairs(j).Code = 0 AndAlso String.Equals(pairs(j).Value.Trim(), "VERTEX", StringComparison.OrdinalIgnoreCase) Then
                        Dim vertexEntity = ReadEntityPairs(pairs, j + 1, j)
                        Dim x As Double
                        Dim y As Double
                        If TryReadPoint(vertexEntity, 10, 20, x, y) Then vertices.Add(Tuple.Create(x, y))
                        j += vertexEntity.Count + 1
                    ElseIf pairs(j).Code = 0 AndAlso String.Equals(pairs(j).Value.Trim(), "SEQEND", StringComparison.OrdinalIgnoreCase) Then
                        j += 1
                        Exit While
                    Else
                        Exit While
                    End If
                End While
                AddPolylineShape(shapes, vertices, IsClosed(polylineEntity))
                i = Math.Max(i + 1, j)
                Continue While
            End If

            Dim entityPairs = ReadEntityPairs(pairs, i + 1, i)
            Select Case entityName
                Case "LINE"
                    AddLineShape(shapes, entityPairs)
                Case "LWPOLYLINE"
                    AddPolylineShape(shapes, ReadLightweightPolylinePoints(entityPairs), IsClosed(entityPairs))
                Case "CIRCLE"
                    AddCircleShape(shapes, entityPairs)
                Case "ARC"
                    AddArcShape(shapes, entityPairs)
                Case "TEXT", "MTEXT"
                    If includeText Then AddTextShape(shapes, entityPairs, entityName)
                Case "SOLID", "TRACE"
                    AddSolidShape(shapes, entityPairs)
                Case "DIMENSION"
                    Dim blockRendered = AddReferencedDimensionBlockShapes(shapes, entityPairs, blockDefinitions, activeBlocks)
                    If includeText AndAlso Not blockRendered Then AddDimensionTextShape(shapes, entityPairs)
            End Select
            i += entityPairs.Count + 1
        End While
        Return shapes
    End Function

    Private Shared Function ReadEntityPairs(pairs As List(Of DxfPair), startIndex As Integer, ByRef zeroIndex As Integer) As List(Of DxfPair)
        Dim result As New List(Of DxfPair)()
        Dim i = startIndex
        While i < pairs.Count AndAlso pairs(i).Code <> 0
            result.Add(pairs(i))
            i += 1
        End While
        zeroIndex = startIndex - 1
        Return result
    End Function

    Private Shared Function AddReferencedDimensionBlockShapes(shapes As List(Of RenderShape),
                                                             entityPairs As List(Of DxfPair),
                                                             blockDefinitions As Dictionary(Of String, List(Of DxfPair)),
                                                             activeBlocks As HashSet(Of String)) As Boolean
        If blockDefinitions Is Nothing OrElse blockDefinitions.Count = 0 Then Return False

        Dim blockName = FirstValue(entityPairs, 2).Trim()
        If blockName = "" OrElse Not blockDefinitions.ContainsKey(blockName) Then Return False
        If activeBlocks.Contains(blockName) Then Return False

        activeBlocks.Add(blockName)
        Try
            Dim blockShapes = ParseShapes(blockDefinitions(blockName), blockDefinitions, True, activeBlocks)
            shapes.AddRange(blockShapes)
            Return blockShapes.Count > 0
        Finally
            activeBlocks.Remove(blockName)
        End Try
    End Function

    Private Shared Sub AddLineShape(shapes As List(Of RenderShape), entityPairs As List(Of DxfPair))
        Dim x1 As Double
        Dim y1 As Double
        Dim x2 As Double
        Dim y2 As Double
        If Not TryReadPoint(entityPairs, 10, 20, x1, y1) OrElse Not TryReadPoint(entityPairs, 11, 21, x2, y2) Then Return
        shapes.Add(New RenderShape With {
            .BoundsAction = Sub(bounds)
                                bounds.Include(x1, y1)
                                bounds.Include(x2, y2)
                            End Sub,
            .SvgFactory = Function() "<line x1=""" & Num(x1) & """ y1=""" & Num(y1) & """ x2=""" & Num(x2) & """ y2=""" & Num(y2) & """/>"
        })
    End Sub

    Private Shared Sub AddCircleShape(shapes As List(Of RenderShape), entityPairs As List(Of DxfPair))
        Dim cx As Double
        Dim cy As Double
        Dim radius As Double
        If Not TryReadPoint(entityPairs, 10, 20, cx, cy) OrElse Not TryParseDouble(FirstValue(entityPairs, 40), radius) OrElse radius <= 0 Then Return
        shapes.Add(New RenderShape With {
            .BoundsAction = Sub(bounds) bounds.IncludeCircle(cx, cy, radius),
            .SvgFactory = Function() "<circle cx=""" & Num(cx) & """ cy=""" & Num(cy) & """ r=""" & Num(radius) & """/>"
        })
    End Sub

    Private Shared Sub AddArcShape(shapes As List(Of RenderShape), entityPairs As List(Of DxfPair))
        Dim cx As Double
        Dim cy As Double
        Dim radius As Double
        Dim startAngle As Double
        Dim endAngle As Double
        If Not TryReadPoint(entityPairs, 10, 20, cx, cy) OrElse
           Not TryParseDouble(FirstValue(entityPairs, 40), radius) OrElse radius <= 0 OrElse
           Not TryParseDouble(FirstValue(entityPairs, 50), startAngle) OrElse
           Not TryParseDouble(FirstValue(entityPairs, 51), endAngle) Then Return

        Dim points = ArcPoints(cx, cy, radius, startAngle, endAngle)
        AddPolylineShape(shapes, points, False)
    End Sub

    Private Shared Sub AddSolidShape(shapes As List(Of RenderShape), entityPairs As List(Of DxfPair))
        Dim points As New List(Of Tuple(Of Double, Double))()
        AddPointIfPresent(entityPairs, points, 10, 20)
        AddPointIfPresent(entityPairs, points, 11, 21)
        AddPointIfPresent(entityPairs, points, 12, 22)
        AddPointIfPresent(entityPairs, points, 13, 23)

        points = points.
            Where(Function(point) point IsNot Nothing).
            Distinct().
            ToList()
        If points.Count < 3 Then Return

        Dim pointText = String.Join(" ", points.Select(Function(point) Num(point.Item1) & "," & Num(point.Item2)))
        shapes.Add(New RenderShape With {
            .BoundsAction = Sub(bounds)
                                For Each point In points
                                    bounds.Include(point.Item1, point.Item2)
                                Next
                            End Sub,
            .SvgFactory = Function() "<polygon points=""" & pointText & """ fill=""#111827"" stroke=""#111827""/>"
        })
    End Sub

    Private Shared Sub AddPointIfPresent(entityPairs As List(Of DxfPair),
                                         points As List(Of Tuple(Of Double, Double)),
                                         xCode As Integer,
                                         yCode As Integer)
        Dim x As Double
        Dim y As Double
        If TryReadPoint(entityPairs, xCode, yCode, x, y) Then points.Add(Tuple.Create(x, y))
    End Sub

    Private Shared Sub AddPolylineShape(shapes As List(Of RenderShape), points As List(Of Tuple(Of Double, Double)), closed As Boolean)
        If points Is Nothing OrElse points.Count < 2 Then Return
        Dim pointText = String.Join(" ", points.Select(Function(point) Num(point.Item1) & "," & Num(point.Item2)))
        shapes.Add(New RenderShape With {
            .BoundsAction = Sub(bounds)
                                For Each point In points
                                    bounds.Include(point.Item1, point.Item2)
                                Next
                            End Sub,
            .SvgFactory = Function()
                              If closed Then Return "<polygon points=""" & pointText & """/>"
                              Return "<polyline points=""" & pointText & """/>"
                          End Function
        })
    End Sub

    Private Shared Function ReadLightweightPolylinePoints(entityPairs As List(Of DxfPair)) As List(Of Tuple(Of Double, Double))
        Dim result As New List(Of Tuple(Of Double, Double))()
        For i As Integer = 0 To entityPairs.Count - 1
            If entityPairs(i).Code <> 10 Then Continue For
            Dim x As Double
            Dim y As Double
            If Not TryParseDouble(entityPairs(i).Value, x) Then Continue For
            For j As Integer = i + 1 To Math.Min(entityPairs.Count - 1, i + 4)
                If entityPairs(j).Code = 20 AndAlso TryParseDouble(entityPairs(j).Value, y) Then
                    result.Add(Tuple.Create(x, y))
                    Exit For
                End If
                If entityPairs(j).Code = 10 Then Exit For
            Next
        Next
        Return result
    End Function

    Private Shared Sub AddTextShape(shapes As List(Of RenderShape), entityPairs As List(Of DxfPair), entityName As String)
        Dim x As Double
        Dim y As Double
        If Not TryReadTextAnchorPoint(entityPairs, entityName, x, y) Then Return
        Dim text = If(entityName = "MTEXT", MTextValue(entityPairs), FirstValue(entityPairs, 1))
        text = CleanText(text)
        If text = "" Then Return
        Dim height As Double
        If Not TryParseDouble(FirstValue(entityPairs, 40), height) OrElse height <= 0 Then height = 2.5R
        Dim rotation As Double
        If Not TryParseDouble(FirstValue(entityPairs, 50), rotation) AndAlso
            String.Equals(entityName, "MTEXT", StringComparison.OrdinalIgnoreCase) Then
            TryReadMTextDirectionRotation(entityPairs, rotation)
        End If
        AddReadableTextShape(shapes, x, y, text, height, rotation, SvgTextAnchor(entityPairs, entityName))
    End Sub

    Private Shared Sub AddDimensionTextShape(shapes As List(Of RenderShape), entityPairs As List(Of DxfPair))
        Dim x As Double
        Dim y As Double
        If Not TryReadPoint(entityPairs, 11, 21, x, y) Then
            If Not TryReadPoint(entityPairs, 10, 20, x, y) Then Return
        End If

        Dim text = CleanText(FirstValue(entityPairs, 1))
        If text = "" OrElse text = "<>" Then
            text = FirstValue(entityPairs, 42)
        End If
        If text = "" Then Return
        AddReadableTextShape(shapes, x, y, text, 2.5R, 0)
    End Sub

    Private Shared Sub AddReadableTextShape(shapes As List(Of RenderShape),
                                            x As Double,
                                            y As Double,
                                            text As String,
                                            height As Double,
                                            rotation As Double,
                                            Optional textAnchor As String = "start")
        shapes.Add(New RenderShape With {
            .IsText = True,
            .BoundsAction = Sub(bounds) bounds.Include(x, y),
            .SvgFactory = Function()
                              Dim transform = "translate(" & Num(x) & "," & Num(-y) & ")"
                              If Math.Abs(rotation) > 0.0001R Then transform &= " rotate(" & Num(-rotation) & ")"
                              Return "<text transform=""" & transform & """ font-size=""" & Num(Math.Max(0.5R, height)) & """ text-anchor=""" & textAnchor & """ dominant-baseline=""middle"">" & EscapeXml(text) & "</text>"
                          End Function
        })
    End Sub

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

    Private Shared Function SvgTextAnchor(pairs As List(Of DxfPair), entityType As String) As String
        If Not String.Equals(entityType, "TEXT", StringComparison.OrdinalIgnoreCase) Then Return "start"

        Dim horizontalAlign As Integer
        Integer.TryParse(FirstValue(pairs, 72), horizontalAlign)
        Select Case horizontalAlign
            Case 1, 4
                Return "middle"
            Case 2
                Return "end"
            Case Else
                Return "start"
        End Select
    End Function

    Private Shared Function ArcPoints(cx As Double, cy As Double, radius As Double, startAngle As Double, endAngle As Double) As List(Of Tuple(Of Double, Double))
        Dim sweep = endAngle - startAngle
        If sweep < 0 Then sweep += 360.0R
        If sweep <= 0 Then sweep = 360.0R
        Dim steps = Math.Max(8, CInt(Math.Ceiling(sweep / 8.0R)))
        Dim result As New List(Of Tuple(Of Double, Double))()
        For i As Integer = 0 To steps
            Dim angle = (startAngle + sweep * i / steps) * Math.PI / 180.0R
            result.Add(Tuple.Create(cx + Math.Cos(angle) * radius, cy + Math.Sin(angle) * radius))
        Next
        Return result
    End Function

    Private Shared Function IsClosed(entityPairs As List(Of DxfPair)) As Boolean
        Dim flag As Integer
        Return Integer.TryParse(FirstValue(entityPairs, 70), flag) AndAlso (flag And 1) = 1
    End Function

    Private Shared Function TryReadPoint(entityPairs As List(Of DxfPair), xCode As Integer, yCode As Integer, ByRef x As Double, ByRef y As Double) As Boolean
        Dim xPair = entityPairs.FirstOrDefault(Function(pair) pair.Code = xCode)
        Dim yPair = entityPairs.FirstOrDefault(Function(pair) pair.Code = yCode)
        If xPair Is Nothing OrElse yPair Is Nothing Then Return False
        Return TryParseDouble(xPair.Value, x) AndAlso TryParseDouble(yPair.Value, y)
    End Function

    Private Shared Function FirstValue(entityPairs As List(Of DxfPair), code As Integer) As String
        Dim found = entityPairs.FirstOrDefault(Function(pair) pair.Code = code)
        Return If(found Is Nothing, "", found.Value)
    End Function

    Private Shared Function MTextValue(entityPairs As List(Of DxfPair)) As String
        Dim parts = entityPairs.
            Where(Function(pair) pair.Code = 1 OrElse pair.Code = 3).
            Select(Function(pair) pair.Value).
            ToList()
        Return String.Join("", parts)
    End Function

    Private Shared Function CleanText(text As String) As String
        Return NormalizeDxfText(text)
    End Function

    Private Shared Function CleanTextLegacyUnused(text As String) As String
        Return If(text, "").
            Replace("<>", "").
            Replace("%%c", "Ø").
            Replace("%%C", "Ø").
            Replace("\P", " ").
            Replace("\X", " ").
            Replace("{", "").
            Replace("}", "").
            Trim()
    End Function

    Private Shared Function EscapeXml(text As String) As String
        Return If(SecurityElement.Escape(If(text, "")), "")
    End Function

    Private Shared Function NormalizeDxfText(text As String) As String
        Dim result = If(text, "")
        result = Regex.Replace(result, "\\S([^;]*);", Function(match As Match) FormatStackedText(match.Groups(1).Value))
        result = Regex.Replace(result, "\\[ACcFfHhQqTtWw][^;]*;", " ")
        result = Regex.Replace(result, "\\[LlOoKk]", "")
        result = result.
            Replace("<>", "").
            Replace("%%c", "Ã˜").
            Replace("%%C", "Ã˜").
            Replace("%%d", "°").
            Replace("%%D", "°").
            Replace("%%p", ChrW(&HB1)).
            Replace("%%P", ChrW(&HB1)).
            Replace("\P", " ").
            Replace("\X", " ").
            Replace("\~", " ").
            Replace("{", "").
            Replace("}", "")
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
        If Not TryParseDouble(CleanStackPart(leftText).TrimStart("+"c, "-"c), leftValue) Then Return False
        If Not TryParseDouble(CleanStackPart(rightText).TrimStart("+"c, "-"c), rightValue) Then Return False
        Return Math.Abs(Math.Abs(leftValue) - Math.Abs(rightValue)) < 0.0000001R
    End Function

    Private Shared Function TryReadMTextDirectionRotation(entityPairs As List(Of DxfPair), ByRef rotation As Double) As Boolean
        Dim vectorX As Double
        Dim vectorY As Double
        If Not TryParseDouble(FirstValue(entityPairs, 11), vectorX) OrElse
           Not TryParseDouble(FirstValue(entityPairs, 21), vectorY) Then
            Return False
        End If
        If Math.Abs(vectorX) < 0.0000001R AndAlso Math.Abs(vectorY) < 0.0000001R Then Return False
        rotation = Math.Atan2(vectorY, vectorX) * 180.0R / Math.PI
        Return True
    End Function

    Private Shared Function TryParseDouble(text As String, ByRef value As Double) As Boolean
        Return Double.TryParse(If(text, ""), NumberStyles.Any, CultureInfo.InvariantCulture, value) OrElse
               Double.TryParse(If(text, "").Replace(","c, "."c), NumberStyles.Any, CultureInfo.InvariantCulture, value)
    End Function

    Private Shared Function Num(value As Double) As String
        Return value.ToString("0.########", CultureInfo.InvariantCulture)
    End Function
End Class
