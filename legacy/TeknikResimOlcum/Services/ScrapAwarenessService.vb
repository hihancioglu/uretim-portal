Imports System.Globalization
Imports System.IO
Imports System.IO.Compression
Imports System.Text
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports System.Xml.Linq

Public NotInheritable Class ScrapAwarenessReason
    Public Property Reason As String = ""
    Public Property Description As String = ""
    Public Property Count As Integer = 0
    Public Property Quantity As Decimal = 0D
End Class

Public NotInheritable Class ScrapAwarenessSummary
    Public Property TrCode As String = ""
    Public Property ProductName As String = ""
    Public Property SourceFileName As String = ""
    Public Property SourceSavedAt As String = ""
    Public Property TotalScrapRows As Integer = 0
    Public Property MatchedRows As Integer = 0
    Public Property MatchedQuantity As Decimal = 0D
    Public Property StatusMessage As String = ""
    Public Property Reasons As List(Of ScrapAwarenessReason) = New List(Of ScrapAwarenessReason)()

    Public ReadOnly Property HasData As Boolean
        Get
            Return Reasons IsNot Nothing AndAlso Reasons.Count > 0
        End Get
    End Property

    Public ReadOnly Property ProductKey As String
        Get
            Return (If(TrCode, "").Trim() & "|" & If(ProductName, "").Trim()).Trim("|"c)
        End Get
    End Property

    Public Function BuildShortText(Optional maxReasons As Integer = 5) As String
        If Not String.IsNullOrWhiteSpace(StatusMessage) Then
            Return "Hurda bilinçlendirme: " & StatusMessage
        End If

        If Not HasData Then
            Return "Hurda bilinçlendirme: Bu ürün için son yüklenen hurda verisinde kayıt bulunamadı."
        End If

        Dim parts = Reasons.
            Take(Math.Max(1, maxReasons)).
            Select(Function(reason)
                       Dim qtyText = If(reason.Quantity > 0D, " / " & FormatQuantity(reason.Quantity), "")
                       Return reason.Reason & " (" & reason.Count.ToString(CultureInfo.InvariantCulture) & " kayıt" & qtyText & ")"
                   End Function)

        Dim sourceText = If(String.IsNullOrWhiteSpace(SourceFileName), "", " | Kaynak: " & SourceFileName)
        Return "Geçmiş hurda sebepleri: " &
               String.Join("; ", parts) &
               " | Eşleşen: " & MatchedRows.ToString(CultureInfo.InvariantCulture) & " kayıt" &
               If(MatchedQuantity > 0D, " / " & FormatQuantity(MatchedQuantity), "") &
               sourceText
    End Function

    Public Function BuildDialogText(Optional maxReasons As Integer = 6) As String
        If Not HasData Then Return BuildShortText(maxReasons)

        Dim sb As New StringBuilder()
        sb.AppendLine("Bu ürün daha önce hurda kayıtlarında görülmüş.")
        sb.AppendLine()
        sb.AppendLine("TR: " & If(String.IsNullOrWhiteSpace(TrCode), "-", TrCode))
        sb.AppendLine("Ürün: " & If(String.IsNullOrWhiteSpace(ProductName), "-", ProductName))
        sb.AppendLine()
        sb.AppendLine("En sık hurda sebepleri:")

        Dim index As Integer = 1
        For Each reason In Reasons.Take(Math.Max(1, maxReasons))
            Dim qtyText = If(reason.Quantity > 0D, " / " & FormatQuantity(reason.Quantity), "")
            sb.AppendLine(index.ToString(CultureInfo.InvariantCulture) & ". " & reason.Reason &
                          " — " & reason.Count.ToString(CultureInfo.InvariantCulture) & " kayıt" & qtyText)
            index += 1
        Next

        If Not String.IsNullOrWhiteSpace(SourceFileName) Then
            sb.AppendLine()
            sb.AppendLine("Kaynak: " & SourceFileName)
        End If

        Return sb.ToString().Trim()
    End Function

    Private Shared Function FormatQuantity(value As Decimal) As String
        If Decimal.Truncate(value) = value Then
            Return value.ToString("N0", CultureInfo.GetCultureInfo("tr-TR")) & " adet"
        End If

        Return value.ToString("N2", CultureInfo.GetCultureInfo("tr-TR")) & " adet"
    End Function
End Class

Public NotInheritable Class ScrapAwarenessService
    Private Sub New()
    End Sub

    Private Const ScrapSlotName As String = "scrap"

    Private Shared ReadOnly CacheLock As New Object()
    Private Shared CachedPath As String = ""
    Private Shared CachedWriteUtc As DateTime = DateTime.MinValue
    Private Shared CachedRows As List(Of ScrapAwarenessRow) = New List(Of ScrapAwarenessRow)()
    Private Shared CachedSourceFileName As String = ""
    Private Shared CachedSavedAt As String = ""
    Private Shared CachedStatusMessage As String = ""

    Public Shared Function GetSummaryForProduct(product As ProductInfo, Optional maxReasons As Integer = 5) As ScrapAwarenessSummary
        Dim summary As New ScrapAwarenessSummary()
        If product Is Nothing Then
            summary.StatusMessage = "Ürün seçilmedi."
            Return summary
        End If

        summary.TrCode = If(product.TrCode, "").Trim()
        summary.ProductName = If(product.ProductName, "").Trim()

        Dim data = LoadScrapRows()
        summary.SourceFileName = data.SourceFileName
        summary.SourceSavedAt = data.SavedAt
        summary.TotalScrapRows = data.Rows.Count

        If Not String.IsNullOrWhiteSpace(data.StatusMessage) Then
            summary.StatusMessage = data.StatusMessage
            Return summary
        End If

        Dim productKeys = ExtractSelectedProductKeys(product)
        Dim productNameKey = NormalizeForCompare(summary.ProductName)

        Dim matchingRows = data.Rows.
            Where(Function(row) RowMatchesProduct(row, productKeys, productNameKey)).
            ToList()

        summary.MatchedRows = matchingRows.Count
        summary.MatchedQuantity = matchingRows.Sum(Function(row) row.Quantity)

        summary.Reasons = matchingRows.
            GroupBy(Function(row) If(String.IsNullOrWhiteSpace(row.Reason), "(Sebep yazılmamış)", row.Reason.Trim()), StringComparer.OrdinalIgnoreCase).
            Select(Function(group) New ScrapAwarenessReason With {
                .Reason = group.Key,
                .Description = group.Select(Function(row) row.ReasonDescription).FirstOrDefault(Function(value) Not String.IsNullOrWhiteSpace(value)),
                .Count = group.Count(),
                .Quantity = group.Sum(Function(row) row.Quantity)
            }).
            OrderByDescending(Function(reason) reason.Quantity).
            ThenByDescending(Function(reason) reason.Count).
            ThenBy(Function(reason) reason.Reason, StringComparer.OrdinalIgnoreCase).
            Take(Math.Max(1, maxReasons)).
            ToList()

        Return summary
    End Function

    Private Shared Function RowMatchesProduct(row As ScrapAwarenessRow, productKeys As HashSet(Of String), productNameKey As String) As Boolean
        If row Is Nothing Then Return False

        If productKeys IsNot Nothing AndAlso productKeys.Count > 0 Then
            Return row.ProductKeys IsNot Nothing AndAlso row.ProductKeys.Overlaps(productKeys)
        End If

        If productNameKey.Length >= 6 Then
            Dim rowText = NormalizeForCompare(row.ProductText)
            If rowText.Length >= 6 AndAlso
               (String.Equals(rowText, productNameKey, StringComparison.OrdinalIgnoreCase) OrElse
                rowText.Contains(productNameKey) OrElse productNameKey.Contains(rowText)) Then Return True
        End If

        Return False
    End Function

    Private Shared Function LoadScrapRows() As LoadedScrapData
        Dim persisted = GetPersistedScrapFile()
        If String.IsNullOrWhiteSpace(persisted.StoredPath) Then
            Return New LoadedScrapData With {.StatusMessage = "Hurda Dashboard'a henüz hurda Excel dosyası yüklenmemiş."}
        End If

        If Not File.Exists(persisted.StoredPath) Then
            Return New LoadedScrapData With {.StatusMessage = "Son hurda dosyası bulunamadı: " & Path.GetFileName(persisted.StoredPath)}
        End If

        Dim extension = Path.GetExtension(persisted.StoredPath)
        If Not String.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase) AndAlso
           Not String.Equals(extension, ".xlsm", StringComparison.OrdinalIgnoreCase) Then
            Return New LoadedScrapData With {
                .SourceFileName = persisted.FileName,
                .SavedAt = persisted.SavedAt,
                .StatusMessage = "Üretim öncesi bilinçlendirme için son hurda dosyası .xlsx/.xlsm formatında olmalı. Mevcut dosya: " & If(persisted.FileName, Path.GetFileName(persisted.StoredPath))
            }
        End If

        Dim writeUtc = File.GetLastWriteTimeUtc(persisted.StoredPath)
        SyncLock CacheLock
            If String.Equals(CachedPath, persisted.StoredPath, StringComparison.OrdinalIgnoreCase) AndAlso
               CachedWriteUtc = writeUtc Then
                Return New LoadedScrapData With {
                    .Rows = CachedRows,
                    .SourceFileName = CachedSourceFileName,
                    .SavedAt = CachedSavedAt,
                    .StatusMessage = CachedStatusMessage
                }
            End If
        End SyncLock

        Dim loaded As LoadedScrapData
        Try
            loaded = ReadScrapWorkbook(persisted.StoredPath)
            loaded.SourceFileName = If(String.IsNullOrWhiteSpace(persisted.FileName), Path.GetFileName(persisted.StoredPath), persisted.FileName)
            loaded.SavedAt = persisted.SavedAt
        Catch ex As Exception
            ErrorLogService.Log("ScrapAwarenessService.LoadScrapRows", ex, persisted.StoredPath)
            loaded = New LoadedScrapData With {
                .SourceFileName = persisted.FileName,
                .SavedAt = persisted.SavedAt,
                .StatusMessage = "Hurda dosyası okunamadı: " & ex.Message
            }
        End Try

        SyncLock CacheLock
            CachedPath = persisted.StoredPath
            CachedWriteUtc = writeUtc
            CachedRows = loaded.Rows
            CachedSourceFileName = loaded.SourceFileName
            CachedSavedAt = loaded.SavedAt
            CachedStatusMessage = loaded.StatusMessage
        End SyncLock

        Return loaded
    End Function

    Private Shared Function ReadScrapWorkbook(filePath As String) As LoadedScrapData
        Dim result As New LoadedScrapData()
        Dim worksheets = ReadWorksheets(filePath)

        For Each sheet In worksheets
            Dim map = DetectHeaderMap(sheet.Rows)
            If map.HeaderRowIndex <= 0 OrElse map.ProductColumns.Count = 0 OrElse
               (map.ReasonColumn <= 0 AndAlso map.ReasonDescriptionColumn <= 0) Then Continue For

            For Each rowEntry In sheet.Rows.Where(Function(item) item.Key > map.HeaderRowIndex).OrderBy(Function(item) item.Key)
                Dim cells = rowEntry.Value
                If map.DepartmentColumn > 0 AndAlso IsExcludedDepartmentValue(Cell(cells, map.DepartmentColumn)) Then Continue For

                Dim productText = JoinColumns(cells, map.ProductColumns)
                Dim reasonText = FirstNonBlank(Cell(cells, map.ReasonColumn), Cell(cells, map.ReasonDescriptionColumn))
                Dim reasonDescriptionText = Cell(cells, map.ReasonDescriptionColumn)
                Dim quantityText = Cell(cells, map.QuantityColumn)

                If String.IsNullOrWhiteSpace(productText) AndAlso String.IsNullOrWhiteSpace(reasonText) Then Continue For

                result.Rows.Add(New ScrapAwarenessRow With {
                    .ProductText = productText,
                    .ProductKeys = ExtractProductKeys(productText),
                    .Reason = CleanReason(reasonText),
                    .ReasonDescription = CleanOptionalText(reasonDescriptionText),
                    .Quantity = ParseDecimal(quantityText)
                })
            Next
        Next

        If result.Rows.Count = 0 Then
            result.StatusMessage = "Hurda Excel içinde ürün/hurda sebebi kolonları algılanamadı."
        End If

        Return result
    End Function

    Private Shared Function DetectHeaderMap(rows As Dictionary(Of Integer, Dictionary(Of Integer, String))) As HeaderMap
        Dim bestRowIndex As Integer = 0
        Dim bestScore As Integer = 0

        For Each rowEntry In rows.OrderBy(Function(item) item.Key).Take(60)
            Dim score As Integer = 0
            For Each cellText In rowEntry.Value.Values
                Dim header = NormalizeHeader(cellText)
                If IsProductHeader(header) Then score += 2
                If IsReasonHeader(header) Then score += 4
                If IsReasonDescriptionHeader(header) Then score += 3
                If IsQuantityHeader(header) Then score += 2
                If IsDepartmentHeader(header) Then score += 1
            Next

            If score > bestScore Then
                bestScore = score
                bestRowIndex = rowEntry.Key
            End If
        Next

        Dim map As New HeaderMap With {.HeaderRowIndex = bestRowIndex}
        If bestRowIndex <= 0 OrElse Not rows.ContainsKey(bestRowIndex) Then Return map

        For Each headerCell In rows(bestRowIndex)
            Dim header = NormalizeHeader(headerCell.Value)
            Dim reasonPriority = ReasonHeaderPriority(header)
            Dim quantityPriority = QuantityHeaderPriority(header)
            If IsReasonDescriptionHeader(header) Then
                map.ReasonDescriptionColumn = headerCell.Key
            ElseIf reasonPriority > map.ReasonColumnPriority Then
                map.ReasonColumn = headerCell.Key
                map.ReasonColumnPriority = reasonPriority
            ElseIf quantityPriority > map.QuantityColumnPriority Then
                map.QuantityColumn = headerCell.Key
                map.QuantityColumnPriority = quantityPriority
            ElseIf IsDepartmentHeader(header) Then
                map.DepartmentColumn = headerCell.Key
            ElseIf IsProductHeader(header) Then
                map.ProductColumns.Add(headerCell.Key)
            End If
        Next

        Return map
    End Function

    Private Shared Function ReadWorksheets(filePath As String) As List(Of WorksheetRows)
        Dim worksheets As New List(Of WorksheetRows)()

        Using archive = ZipFile.OpenRead(filePath)
            Dim sharedStrings = ReadSharedStrings(archive)
            For Each entryInfo In GetWorksheetEntries(archive)
                If entryInfo.Entry Is Nothing Then Continue For

                Dim worksheet As New WorksheetRows With {.SheetName = entryInfo.SheetName}
                Using stream = entryInfo.Entry.Open()
                    Dim doc = XDocument.Load(stream)
                    Dim ns = doc.Root.Name.Namespace

                    For Each cellElement In doc.Descendants(ns + "c")
                        Dim refAttr = cellElement.Attribute("r")
                        If refAttr Is Nothing Then Continue For

                        Dim cellRef = refAttr.Value
                        Dim rowNumber = RowNumberFromCellRef(cellRef)
                        Dim columnNumber = ColumnNumberFromCellRef(cellRef)
                        If rowNumber <= 0 OrElse columnNumber <= 0 Then Continue For

                        Dim value = ReadCellValue(cellElement, sharedStrings, ns)
                        If String.IsNullOrWhiteSpace(value) Then Continue For

                        If Not worksheet.Rows.ContainsKey(rowNumber) Then
                            worksheet.Rows(rowNumber) = New Dictionary(Of Integer, String)()
                        End If

                        worksheet.Rows(rowNumber)(columnNumber) = value.Trim()
                    Next
                End Using

                worksheets.Add(worksheet)
            Next
        End Using

        Return worksheets
    End Function

    Private Shared Function GetWorksheetEntries(archive As ZipArchive) As List(Of WorksheetEntryInfo)
        Dim result As New List(Of WorksheetEntryInfo)()
        Dim workbookEntry = archive.GetEntry("xl/workbook.xml")
        Dim relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels")

        If workbookEntry Is Nothing OrElse relsEntry Is Nothing Then
            result.Add(New WorksheetEntryInfo With {.Entry = archive.GetEntry("xl/worksheets/sheet1.xml"), .SheetName = "sheet1"})
            Return result
        End If

        Dim relTargets As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Using relStream = relsEntry.Open()
            Dim relDoc = XDocument.Load(relStream)
            Dim relNs = relDoc.Root.Name.Namespace
            For Each rel In relDoc.Descendants(relNs + "Relationship")
                Dim id = Convert.ToString(rel.Attribute("Id")?.Value)
                Dim target = Convert.ToString(rel.Attribute("Target")?.Value)
                If String.IsNullOrWhiteSpace(id) OrElse String.IsNullOrWhiteSpace(target) Then Continue For
                target = target.Replace("\"c, "/"c)
                If target.StartsWith("/", StringComparison.Ordinal) Then target = target.TrimStart("/"c)
                If Not target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) Then target = "xl/" & target
                relTargets(id) = target
            Next
        End Using

        Using workbookStream = workbookEntry.Open()
            Dim workbookDoc = XDocument.Load(workbookStream)
            Dim ns = workbookDoc.Root.Name.Namespace
            Dim relNs = XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships")

            For Each sheet In workbookDoc.Descendants(ns + "sheet")
                Dim sheetName = Convert.ToString(sheet.Attribute("name")?.Value)
                Dim id = Convert.ToString(sheet.Attribute(relNs + "id")?.Value)
                If String.IsNullOrWhiteSpace(sheetName) Then sheetName = "sheet"
                If String.IsNullOrWhiteSpace(id) OrElse Not relTargets.ContainsKey(id) Then Continue For

                result.Add(New WorksheetEntryInfo With {
                    .Entry = archive.GetEntry(relTargets(id)),
                    .SheetName = sheetName
                })
            Next
        End Using

        If result.Count = 0 Then
            result.Add(New WorksheetEntryInfo With {.Entry = archive.GetEntry("xl/worksheets/sheet1.xml"), .SheetName = "sheet1"})
        End If

        Return result
    End Function

    Private Shared Function ReadSharedStrings(archive As ZipArchive) As List(Of String)
        Dim result As New List(Of String)()
        Dim entry = archive.GetEntry("xl/sharedStrings.xml")
        If entry Is Nothing Then Return result

        Using stream = entry.Open()
            Dim doc = XDocument.Load(stream)
            Dim ns = doc.Root.Name.Namespace

            For Each item In doc.Descendants(ns + "si")
                result.Add(String.Join("", item.Descendants(ns + "t").Select(Function(t) t.Value)))
            Next
        End Using

        Return result
    End Function

    Private Shared Function ReadCellValue(cellElement As XElement, sharedStrings As List(Of String), ns As XNamespace) As String
        Dim dataType = Convert.ToString(cellElement.Attribute("t")?.Value)

        If String.Equals(dataType, "inlineStr", StringComparison.OrdinalIgnoreCase) Then
            Dim inlineText = cellElement.Element(ns + "is")
            If inlineText Is Nothing Then Return ""
            Return String.Join("", inlineText.Descendants(ns + "t").Select(Function(t) t.Value))
        End If

        Dim valueElement = cellElement.Element(ns + "v")
        If valueElement Is Nothing Then Return ""

        Dim rawValue = valueElement.Value
        If String.Equals(dataType, "s", StringComparison.OrdinalIgnoreCase) Then
            Dim index As Integer
            If Integer.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, index) AndAlso
               index >= 0 AndAlso index < sharedStrings.Count Then
                Return sharedStrings(index)
            End If
        End If

        Return rawValue
    End Function

    Private Shared Function RowNumberFromCellRef(cellRef As String) As Integer
        Dim match = Regex.Match(If(cellRef, ""), "\d+")
        If Not match.Success Then Return 0

        Dim value As Integer
        If Integer.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, value) Then Return value
        Return 0
    End Function

    Private Shared Function ColumnNumberFromCellRef(cellRef As String) As Integer
        Dim match = Regex.Match(If(cellRef, ""), "^[A-Za-z]+")
        If Not match.Success Then Return 0

        Dim result As Integer = 0
        For Each ch In match.Value.ToUpperInvariant()
            result = result * 26 + (AscW(ch) - AscW("A"c) + 1)
        Next

        Return result
    End Function

    Private Shared Function GetPersistedScrapFile() As PersistedScrapFile
        Dim info As New PersistedScrapFile()

        Try
            If File.Exists(AppPaths.ScrapDashboardStateJson) Then
                Using document = JsonDocument.Parse(File.ReadAllText(AppPaths.ScrapDashboardStateJson, Encoding.UTF8))
                    Dim root = document.RootElement
                    Dim scrap As JsonElement
                    If root.TryGetProperty("Scrap", scrap) Then
                        info.FileName = GetJsonString(scrap, "FileName")
                        info.StoredFileName = GetJsonString(scrap, "StoredFileName")
                        info.SavedAt = GetJsonString(scrap, "SavedAt")
                    End If
                End Using
            End If
        Catch ex As Exception
            ErrorLogService.Log("ScrapAwarenessService.GetPersistedScrapFile", ex)
        End Try

        If Not String.IsNullOrWhiteSpace(info.StoredFileName) Then
            info.StoredPath = Path.Combine(AppPaths.ScrapDashboardDataDir, info.StoredFileName)
        End If

        If String.IsNullOrWhiteSpace(info.StoredPath) OrElse Not File.Exists(info.StoredPath) Then
            Try
                If Directory.Exists(AppPaths.ScrapDashboardDataDir) Then
                    Dim fallback = Directory.GetFiles(AppPaths.ScrapDashboardDataDir, "LastScrapData.*").
                        OrderByDescending(Function(path) File.GetLastWriteTimeUtc(path)).
                        FirstOrDefault()
                    If Not String.IsNullOrWhiteSpace(fallback) Then
                        info.StoredPath = fallback
                        If String.IsNullOrWhiteSpace(info.FileName) Then info.FileName = Path.GetFileName(fallback)
                    End If
                End If
            Catch ex As Exception
                ErrorLogService.Log("ScrapAwarenessService.GetPersistedScrapFile.Fallback", ex)
            End Try
        End If

        Return info
    End Function

    Private Shared Function GetJsonString(element As JsonElement, propertyName As String) As String
        Try
            Dim value As JsonElement
            If element.ValueKind = JsonValueKind.Object AndAlso element.TryGetProperty(propertyName, value) AndAlso value.ValueKind = JsonValueKind.String Then
                Return value.GetString()
            End If
        Catch
        End Try

        Return ""
    End Function

    Private Shared Function IsProductHeader(header As String) As Boolean
        If header = "" Then Return False
        If HeaderEqualsAny(header, {"TR KODU", "TR_KODU", "TRKODU", "TR KOD", "TR NO", "TR NUMARASI", "TR"}) Then Return True
        If HeaderEqualsAny(header, {"MALZEME ACIKLAMASI", "IS EMRI MALZEME TANIMI", "IS EMRI MALZEME NO", "SIPARIS REF1"}) Then Return True
        If HeaderEqualsAny(header, {"URUN", "URUN KODU", "STOK KODU", "MALZEME KODU"}) Then Return True
        Return False
    End Function

    Private Shared Function IsReasonHeader(header As String) As Boolean
        Return ReasonHeaderPriority(header) > 0
    End Function

    Private Shared Function ReasonHeaderPriority(header As String) As Integer
        If header = "" Then Return 0
        If IsReasonDescriptionHeader(header) Then Return 0
        If IsDepartmentHeader(header) Then Return 0
        If HeaderEqualsAny(header, {"HURDA MIKTARI", "HURDA ADET", "ENVANTER MIK.", "ENVANTER MIKTAR", "ENVANTER MIK", "MIKTAR", "ADET"}) Then Return 0

        'Hurda Dashboard ile aynı varsayılan seçim mantığı:
        'önce "hurda sebebi", sonra "hurda nedeni"; diğer hurda/hurdalama başlıkları yalnızca yedek.
        If HeaderEqualsAny(header, {"HURDA SEBEBI"}) Then Return 100
        If HeaderEqualsAny(header, {"HURDA NEDENI"}) Then Return 95
        If HeaderEqualsAny(header, {"HURDALAMA SEBEBI"}) Then Return 80
        If HeaderEqualsAny(header, {"HURDALAMA NEDENI"}) Then Return 75
        If header.Contains("HURDA") OrElse header.Contains("HURDALAMA") Then Return 25
        If header.Contains("SEBEBIYET") Then Return 10
        Return 0
    End Function

    Private Shared Function IsReasonDescriptionHeader(header As String) As Boolean
        If header = "" Then Return False
        Return HeaderEqualsAny(header, {"HURDA SEBEBI TANIMI", "HURDA SEBEP TANIMI", "HURDALAMA SEBEBI TANIMI", "HURDALAMA SEBEP TANIMI"})
    End Function

    Private Shared Function IsDepartmentHeader(header As String) As Boolean
        If header = "" Then Return False
        Return HeaderEqualsAny(header, {"HURDAYA SEBEBIYET VEREN BOLUM", "SEBEBIYET VEREN BOLUM", "BOLUM"}) OrElse
               header.Contains("HURDAYA SEBEBIYET VEREN") OrElse
               header.Contains("SEBEBIYET VEREN BOLUM")
    End Function

    Private Shared Function IsQuantityHeader(header As String) As Boolean
        Return QuantityHeaderPriority(header) > 0
    End Function

    Private Shared Function QuantityHeaderPriority(header As String) As Integer
        If header = "" Then Return 0
        If HeaderEqualsAny(header, {"ENVANTER MIK."}) Then Return 100
        If HeaderEqualsAny(header, {"ENVANTER MIKTAR"}) Then Return 95
        If HeaderEqualsAny(header, {"ENVANTER MIK"}) Then Return 90
        If HeaderEqualsAny(header, {"MIKTAR"}) Then Return 70
        If HeaderEqualsAny(header, {"ADET"}) Then Return 65
        If HeaderEqualsAny(header, {"HURDA MIKTARI"}) Then Return 60
        If HeaderEqualsAny(header, {"HURDA ADET"}) Then Return 55
        Return 0
    End Function

    Private Shared Function HeaderEqualsAny(header As String, values As IEnumerable(Of String)) As Boolean
        For Each value In values
            Dim normalized = NormalizeHeader(value)
            If String.Equals(header, normalized, StringComparison.OrdinalIgnoreCase) Then Return True
        Next

        Return False
    End Function

    Private Shared Function NormalizeHeader(value As String) As String
        Dim text = NormalizeForCompare(value)
        text = Regex.Replace(text, "\s+", " ").Trim()
        Return text
    End Function

    Private Shared Function NormalizeForCompare(value As String) As String
        Dim text = If(value, "").Trim().ToUpperInvariant()
        text = text.Replace("İ", "I").
                    Replace("ı", "I").
                    Replace("Ş", "S").
                    Replace("ş", "S").
                    Replace("Ğ", "G").
                    Replace("ğ", "G").
                    Replace("Ü", "U").
                    Replace("ü", "U").
                    Replace("Ö", "O").
                    Replace("ö", "O").
                    Replace("Ç", "C").
                    Replace("ç", "C")
        Return text
    End Function

    Private Shared Function ExtractSelectedProductKeys(product As ProductInfo) As HashSet(Of String)
        Dim keys As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        If product Is Nothing Then Return keys

        AddCanonicalProductToken(keys, product.TrCode, True)
        AddCanonicalProductToken(keys, product.PlasticCode, True)

        'Urun adi icinde acikca TR/P/TB.MKZ. yaziyorsa al; sade sayilari urun adi icinden anahtar yapma.
        AddCanonicalProductToken(keys, product.ProductName, False)

        Return keys
    End Function

    Private Shared Function ExtractProductKeys(value As String) As HashSet(Of String)
        Dim keys As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        AddCanonicalProductToken(keys, value, True)
        Return keys
    End Function

    Private Shared Sub AddCanonicalProductToken(keys As HashSet(Of String), value As String, allowBare As Boolean)
        If keys Is Nothing Then Return

        Dim token = CanonicalProductToken(value, allowBare)
        If Not String.IsNullOrWhiteSpace(token) Then keys.Add(token)
    End Sub

    Private Shared Function CanonicalProductToken(value As String, allowBare As Boolean) As String
        Dim code = CanonicalProductCode(value, allowBare)
        If code Is Nothing Then Return ""
        Return code.Token
    End Function

    Private Shared Function CanonicalProductCode(value As String, allowBare As Boolean) As ProductCodeInfo
        If String.IsNullOrWhiteSpace(value) Then Return Nothing

        Dim raw = If(value, "").Trim()
        If Regex.IsMatch(raw, "^\d{1,2}[.\-/]\d{1,2}[.\-/]\d{2,4}(?:\s|$)") OrElse
           Regex.IsMatch(raw, "^\d{4}[.\-/]\d{1,2}[.\-/]\d{1,2}") Then Return Nothing

        Dim text = NormalizeCodeText(raw)
        If text = "" Then Return Nothing

        Dim trPair = Regex.Match(text, "\bT\s*R\s*(?:NO|NUMARA|NUMARASI|KODU|KOD|CODE)?\s*[\s.\-_:;/\\]*([0-9]{2,})(?:\s*[\-_ .]\s*([0-9A-Z]{1,4}))?\b", RegexOptions.IgnoreCase)
        Dim pPair = Regex.Match(text, "(?:^|[^A-Z0-9])P\s*[\s.\-_:;/\\]*([0-9]{2,})(?:\s*[\-/ _\.]\s*([0-9A-Z]{1,4}))?(?=$|[^A-Z0-9])", RegexOptions.IgnoreCase)

        If trPair.Success AndAlso pPair.Success Then
            Dim trCode = MakeProductCode(trPair.Groups(1).Value, trPair.Groups(2).Value)
            Dim pCode = MakeProductCode(pPair.Groups(1).Value, pPair.Groups(2).Value)
            If trCode <> "" AndAlso pCode <> "" Then
                Return New ProductCodeInfo With {.Token = "TR " & trCode & " / P " & pCode}
            End If
        End If

        Dim match = Regex.Match(text, "\bT\s*B\s*[\.\-_\s]*M\s*K\s*Z\s*[\s.\-_:;/\\]*([0-9]{2,})((?:\s*[\-/ _\.]\s*[0-9A-Z]{1,4})*)\b", RegexOptions.IgnoreCase)
        If match.Success Then
            Dim code = MakeProductCode(match.Groups(1).Value, match.Groups(2).Value)
            If code <> "" Then Return New ProductCodeInfo With {.Token = "TB.MKZ." & code}
        End If

        match = Regex.Match(text, "(?:^|[^A-Z0-9])P\s*[\s.\-_:;/\\]*([0-9]{2,})(?:\s*[\-/ _\.]\s*([0-9A-Z]{1,4}))?(?=$|[^A-Z0-9])", RegexOptions.IgnoreCase)
        If match.Success Then
            Dim code = MakeProductCode(match.Groups(1).Value, match.Groups(2).Value)
            If code <> "" Then Return New ProductCodeInfo With {.Token = "P " & code}
        End If

        match = Regex.Match(text, "\bT\s*R\s*(?:NO|NUMARA|NUMARASI|KODU|KOD|CODE)?\s*[\s.\-_:;/\\]*([0-9]{2,})(?:\s*[\-/ _\.]\s*([0-9A-Z]{1,4}))?\b", RegexOptions.IgnoreCase)
        If match.Success Then
            Dim code = MakeProductCode(match.Groups(1).Value, match.Groups(2).Value)
            If code <> "" Then Return New ProductCodeInfo With {.Token = "TR " & code}
        End If

        match = Regex.Match(text, "\b(?:TUR\s*SABLONU|TUR|TR\s*NO|TR\s*KODU|TR\s*KOD|TR\s*CODE)\s*[\s.\-_:;/\\]*([0-9]{2,})(?:\s*[\-/ _\.]\s*([0-9A-Z]{1,4}))?\b", RegexOptions.IgnoreCase)
        If match.Success Then
            Dim code = MakeProductCode(match.Groups(1).Value, match.Groups(2).Value)
            If code <> "" Then Return New ProductCodeInfo With {.Token = "TR " & code}
        End If

        If Not allowBare Then Return Nothing

        match = Regex.Match(text, "^\s*([0-9]{2,})(?:\s*[\-/ _\.]\s*([0-9A-Z]{1,4}))?\s*$", RegexOptions.IgnoreCase)
        If match.Success Then
            Dim code = MakeProductCode(match.Groups(1).Value, match.Groups(2).Value)
            If code <> "" Then Return New ProductCodeInfo With {.Token = "TR " & code}
        End If

        match = Regex.Match(text, "(?:^|[^0-9A-Z])([0-9]{2,})(?:\s*[\-/ _\.]\s*([0-9A-Z]{1,4}))?(?=$|[^0-9A-Z])", RegexOptions.IgnoreCase)
        If match.Success Then
            Dim baseCode = match.Groups(1).Value
            If Not Regex.IsMatch(baseCode, "^(19|20)\d{2}$") OrElse match.Groups(2).Success Then
                Dim code = MakeProductCode(baseCode, match.Groups(2).Value)
                If code <> "" Then Return New ProductCodeInfo With {.Token = "TR " & code}
            End If
        End If

        Return Nothing
    End Function

    Private Shared Function NormalizeCodeText(value As String) As String
        Dim text = NormalizeForCompare(value)
        text = text.Replace(ChrW(&HA0), " "c).
                    Replace(ChrW(&H2010), "-"c).
                    Replace(ChrW(&H2011), "-"c).
                    Replace(ChrW(&H2012), "-"c).
                    Replace(ChrW(&H2013), "-"c).
                    Replace(ChrW(&H2014), "-"c).
                    Replace(ChrW(&H2212), "-"c).
                    Replace(ChrW(&HFF1A), ":"c)
        Return text.Trim()
    End Function

    Private Shared Function MakeProductCode(basePart As String, suffix As String) As String
        Dim parts As New List(Of String)()
        AddCodePart(parts, basePart)

        If Not String.IsNullOrWhiteSpace(suffix) Then
            For Each match As Match In Regex.Matches(NormalizeCodeText(suffix), "[0-9A-Z]{1,4}", RegexOptions.IgnoreCase)
                AddCodePart(parts, match.Value)
            Next
        End If

        Return String.Join("-", parts)
    End Function

    Private Shared Sub AddCodePart(parts As List(Of String), value As String)
        If parts Is Nothing Then Return

        Dim text = Regex.Replace(NormalizeCodeText(value), "[^0-9A-Z]+", "")
        text = text.TrimStart("0"c)
        If text <> "" Then parts.Add(text)
    End Sub

    Private Shared Function JoinColumns(cells As Dictionary(Of Integer, String), columns As IEnumerable(Of Integer)) As String
        Dim parts As New List(Of String)()
        For Each column In columns
            Dim value = Cell(cells, column)
            If Not String.IsNullOrWhiteSpace(value) Then parts.Add(value.Trim())
        Next
        Return String.Join(" | ", parts)
    End Function

    Private Shared Function Cell(cells As Dictionary(Of Integer, String), column As Integer) As String
        If cells Is Nothing OrElse column <= 0 OrElse Not cells.ContainsKey(column) Then Return ""
        Return If(cells(column), "").Trim()
    End Function

    Private Shared Function FirstNonBlank(ParamArray values As String()) As String
        For Each value In values
            If Not String.IsNullOrWhiteSpace(value) Then Return value.Trim()
        Next
        Return ""
    End Function

    Private Shared Function CleanReason(value As String) As String
        Dim text = If(value, "").Trim()
        If text = "" Then Return "(Sebep yazılmamış)"
        text = Regex.Replace(text, "\s+", " ")
        Return text
    End Function

    Private Shared Function CleanOptionalText(value As String) As String
        Dim text = If(value, "").Trim()
        If text = "" Then Return ""
        text = Regex.Replace(text, "\s+", " ")
        Return text
    End Function

    Private Shared Function IsExcludedDepartmentValue(value As String) As Boolean
        Dim text = If(value, "").Trim()
        If text = "" Then Return True
        Dim normalized = NormalizeForCompare(text)
        Return normalized = "-" OrElse normalized = "?" OrElse normalized = "IPTAL"
    End Function

    Private Shared Function ParseDecimal(value As String) As Decimal
        Dim text = If(value, "").Trim()
        If text = "" Then Return 0D

        text = text.Replace(ChrW(&HA0), " "c).Trim()
        Dim cultureTr = CultureInfo.GetCultureInfo("tr-TR")
        Dim parsed As Decimal

        If Decimal.TryParse(text, NumberStyles.Any, cultureTr, parsed) Then Return Math.Max(0D, parsed)
        If Decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, parsed) Then Return Math.Max(0D, parsed)

        Dim normalized = text
        If normalized.Contains(","c) AndAlso normalized.Contains("."c) Then
            If normalized.LastIndexOf(","c) > normalized.LastIndexOf("."c) Then
                normalized = normalized.Replace(".", "").Replace(","c, "."c)
            Else
                normalized = normalized.Replace(",", "")
            End If
        ElseIf normalized.Contains(","c) Then
            normalized = normalized.Replace(","c, "."c)
        End If

        If Decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, parsed) Then Return Math.Max(0D, parsed)
        Return 0D
    End Function

    Private NotInheritable Class LoadedScrapData
        Public Property Rows As List(Of ScrapAwarenessRow) = New List(Of ScrapAwarenessRow)()
        Public Property SourceFileName As String = ""
        Public Property SavedAt As String = ""
        Public Property StatusMessage As String = ""
    End Class

    Private NotInheritable Class PersistedScrapFile
        Public Property FileName As String = ""
        Public Property StoredFileName As String = ""
        Public Property StoredPath As String = ""
        Public Property SavedAt As String = ""
    End Class

    Private NotInheritable Class ScrapAwarenessRow
        Public Property ProductText As String = ""
        Public Property ProductKeys As HashSet(Of String) = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Public Property Reason As String = ""
        Public Property ReasonDescription As String = ""
        Public Property Quantity As Decimal = 0D
    End Class

    Private NotInheritable Class ProductCodeInfo
        Public Property Token As String = ""
    End Class

    Private NotInheritable Class HeaderMap
        Public Property HeaderRowIndex As Integer = 0
        Public Property ProductColumns As List(Of Integer) = New List(Of Integer)()
        Public Property ReasonColumn As Integer = 0
        Public Property ReasonColumnPriority As Integer = 0
        Public Property ReasonDescriptionColumn As Integer = 0
        Public Property DepartmentColumn As Integer = 0
        Public Property QuantityColumn As Integer = 0
        Public Property QuantityColumnPriority As Integer = 0
    End Class

    Private NotInheritable Class WorksheetRows
        Public Property SheetName As String = ""
        Public Property Rows As Dictionary(Of Integer, Dictionary(Of Integer, String)) = New Dictionary(Of Integer, Dictionary(Of Integer, String))()
    End Class

    Private NotInheritable Class WorksheetEntryInfo
        Public Property Entry As ZipArchiveEntry
        Public Property SheetName As String = ""
    End Class
End Class
