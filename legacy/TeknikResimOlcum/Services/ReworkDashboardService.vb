Imports System.Globalization
Imports System.IO
Imports System.IO.Compression
Imports System.Text
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports System.Xml.Linq

Public NotInheritable Class ReworkDashboardService
    Private Sub New()
    End Sub

    Public Shared Function ImportAndPersist(sourcePath As String) As ReworkImportResult
        Dim result = ReadWorkbook(sourcePath)
        If Not result.IsSuccess Then Return result

        Directory.CreateDirectory(AppPaths.ReworkDashboardDataDir)
        Dim extension = Path.GetExtension(sourcePath).ToLowerInvariant()
        Dim storedFileName = "LastReworkData" & extension
        Dim storedPath = Path.Combine(AppPaths.ReworkDashboardDataDir, storedFileName)
        Dim temporaryPath = storedPath & ".tmp"

        File.Copy(sourcePath, temporaryPath, True)
        File.Move(temporaryPath, storedPath, True)

        Dim state As New ReworkDashboardState With {
            .OriginalFileName = Path.GetFileName(sourcePath),
            .StoredFileName = storedFileName,
            .ImportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            .ImportedBy = If(AppState.CurrentUserName, "").Trim(),
            .ComputerName = Environment.MachineName,
            .RecordCount = result.Records.Count,
            .RejectedRowCount = result.RejectedRowCount
        }
        SaveState(state)
        result.State = state
        Return result
    End Function

    Public Shared Function LoadCurrent() As ReworkImportResult
        Dim state = LoadState()
        If state Is Nothing OrElse String.IsNullOrWhiteSpace(state.StoredFileName) Then
            Return ReworkImportResult.Failure("REWORK Dashboard'a henüz Excel dosyası yüklenmemiş.")
        End If

        Dim storedPath = Path.Combine(AppPaths.ReworkDashboardDataDir, Path.GetFileName(state.StoredFileName))
        If Not File.Exists(storedPath) Then
            Return ReworkImportResult.Failure("Son REWORK Excel dosyası bulunamadı: " & state.OriginalFileName)
        End If

        Dim result = ReadWorkbook(storedPath)
        result.State = state
        Return result
    End Function

    Public Shared Function LoadState() As ReworkDashboardState
        Try
            If Not File.Exists(AppPaths.ReworkDashboardStateJson) Then Return Nothing
            Dim json = File.ReadAllText(AppPaths.ReworkDashboardStateJson, Encoding.UTF8)
            Return JsonSerializer.Deserialize(Of ReworkDashboardState)(json)
        Catch ex As Exception
            ErrorLogService.Log("ReworkDashboardService.LoadState", ex)
            Return Nothing
        End Try
    End Function

    Private Shared Sub SaveState(state As ReworkDashboardState)
        Dim options As New JsonSerializerOptions With {.WriteIndented = True}
        File.WriteAllText(
            AppPaths.ReworkDashboardStateJson,
            JsonSerializer.Serialize(state, options),
            Encoding.UTF8)
    End Sub

    Private Shared Function ReadWorkbook(filePath As String) As ReworkImportResult
        If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then
            Return ReworkImportResult.Failure("REWORK Excel dosyası bulunamadı.")
        End If

        Dim extension = Path.GetExtension(filePath)
        If Not String.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase) AndAlso
           Not String.Equals(extension, ".xlsm", StringComparison.OrdinalIgnoreCase) Then

            Return ReworkImportResult.Failure("REWORK dosyası .xlsx veya .xlsm formatında olmalıdır.")
        End If

        Try
            Dim result As New ReworkImportResult()
            Dim worksheets = ReadWorksheets(filePath)
            Dim mappedSheetCount As Integer = 0

            For Each sheet In worksheets
                Dim headerMap = DetectHeaderMap(sheet.Rows)
                If Not headerMap.IsComplete Then Continue For
                mappedSheetCount += 1

                For Each rowEntry In sheet.Rows.
                    Where(Function(item) item.Key > headerMap.HeaderRowIndex).
                    OrderBy(Function(item) item.Key)

                    Dim cells = rowEntry.Value
                    If cells.Values.All(Function(value) String.IsNullOrWhiteSpace(value)) Then Continue For

                    Dim operationDate As DateTime
                    Dim quantity As Decimal
                    Dim dateText = Cell(cells, headerMap.OperationDateColumn)
                    Dim quantityText = Cell(cells, headerMap.CompletedQuantityColumn)

                    If Not TryParseDate(dateText, operationDate) Then
                        result.InvalidDateRowCount += 1
                        result.RejectedRowCount += 1
                        Continue For
                    End If

                    If Not TryParseQuantity(quantityText, quantity) Then
                        result.InvalidQuantityRowCount += 1
                        result.RejectedRowCount += 1
                        Continue For
                    End If

                    If quantity <= 0D Then
                        result.ZeroQuantityRowCount += 1
                        result.RejectedRowCount += 1
                        Continue For
                    End If

                    Dim workCenter = CleanText(Cell(cells, headerMap.WorkCenterColumn))
                    Dim workCenterDescription = CleanText(Cell(cells, headerMap.WorkCenterDescriptionColumn))
                    Dim materialDescription = CleanText(Cell(cells, headerMap.MaterialDescriptionColumn))
                    Dim operationDescription = CleanText(Cell(cells, headerMap.OperationDescriptionColumn))

                    If String.IsNullOrWhiteSpace(workCenter) Then
                        result.MissingWorkCenterRowCount += 1
                        result.RejectedRowCount += 1
                        Continue For
                    End If

                    If String.IsNullOrWhiteSpace(materialDescription) Then
                        result.MissingMaterialDescriptionRowCount += 1
                        result.RejectedRowCount += 1
                        Continue For
                    End If

                    If String.IsNullOrWhiteSpace(operationDescription) Then
                        result.MissingOperationDescriptionRowCount += 1
                        result.RejectedRowCount += 1
                        Continue For
                    End If

                    result.Records.Add(New ReworkRecord With {
                        .OperationDate = operationDate,
                        .WorkCenter = workCenter,
                        .WorkCenterDescription = workCenterDescription,
                        .TourTemplate = CleanText(Cell(cells, headerMap.TourTemplateColumn)),
                        .MaterialDescription = materialDescription,
                        .CompletedQuantity = quantity,
                        .OperationDescription = operationDescription,
                        .SourceSheet = sheet.SheetName,
                        .SourceRowNumber = rowEntry.Key
                    })
                Next
            Next

            If mappedSheetCount = 0 Then
                Return ReworkImportResult.Failure(
                    "Excel içinde gerekli REWORK başlıklarının tamamı bulunamadı: " &
                    String.Join(", ", RequiredDisplayHeaders))
            End If

            If result.Records.Count = 0 Then
                Return ReworkImportResult.Failure("Excel içinde geçerli ve miktarı sıfırdan büyük REWORK kaydı bulunamadı.")
            End If

            result.IsSuccess = True
            result.StatusMessage = result.Records.Count.ToString("N0", CultureInfo.GetCultureInfo("tr-TR")) &
                " REWORK kaydı yüklendi."
            If result.RejectedRowCount > 0 Then
                Dim trCulture = CultureInfo.GetCultureInfo("tr-TR")
                result.StatusMessage &= Environment.NewLine & Environment.NewLine &
                    "Alınmayan satırlar (toplam " & result.RejectedRowCount.ToString("N0", trCulture) & "):" & Environment.NewLine &
                    "• İşlem tarihi boş/okunamadı: " & result.InvalidDateRowCount.ToString("N0", trCulture) & Environment.NewLine &
                    "• Tamamlanan miktar boş/okunamadı: " & result.InvalidQuantityRowCount.ToString("N0", trCulture) & Environment.NewLine &
                    "• Tamamlanan miktar 0: " & result.ZeroQuantityRowCount.ToString("N0", trCulture) & Environment.NewLine &
                    "• İş Merkezi boş: " & result.MissingWorkCenterRowCount.ToString("N0", trCulture) & Environment.NewLine &
                    "• Malzeme Açıklaması boş: " & result.MissingMaterialDescriptionRowCount.ToString("N0", trCulture) & Environment.NewLine &
                    "• Operasyon Açıklama boş: " & result.MissingOperationDescriptionRowCount.ToString("N0", trCulture)
            End If
            Return result
        Catch ex As Exception
            ErrorLogService.Log("ReworkDashboardService.ReadWorkbook", ex, filePath)
            Return ReworkImportResult.Failure("REWORK Excel dosyası okunamadı: " & ex.Message)
        End Try
    End Function

    Private Shared ReadOnly RequiredDisplayHeaders As String() = {
        "İşlem Tarihi",
        "İş Merkezi",
        "İş Merkezi Tanımı",
        "Tur Sablonu",
        "Malzeme Açıklaması",
        "Tamamlanan Mik. (Topl)",
        "Operasyon Açıklama"
    }

    Private Shared Function DetectHeaderMap(rows As Dictionary(Of Integer, Dictionary(Of Integer, String))) As ReworkHeaderMap
        Dim bestMap As ReworkHeaderMap = Nothing
        Dim bestScore As Integer = 0

        For Each rowEntry In rows.OrderBy(Function(item) item.Key).Take(60)
            Dim candidate As New ReworkHeaderMap With {.HeaderRowIndex = rowEntry.Key}
            For Each headerCell In rowEntry.Value
                Select Case HeaderKey(headerCell.Value)
                    Case "OPERATION_DATE"
                        candidate.OperationDateColumn = headerCell.Key
                    Case "WORK_CENTER"
                        candidate.WorkCenterColumn = headerCell.Key
                    Case "WORK_CENTER_DESCRIPTION"
                        candidate.WorkCenterDescriptionColumn = headerCell.Key
                    Case "TOUR_TEMPLATE"
                        candidate.TourTemplateColumn = headerCell.Key
                    Case "MATERIAL_DESCRIPTION"
                        candidate.MaterialDescriptionColumn = headerCell.Key
                    Case "COMPLETED_QUANTITY"
                        candidate.CompletedQuantityColumn = headerCell.Key
                    Case "OPERATION_DESCRIPTION"
                        candidate.OperationDescriptionColumn = headerCell.Key
                End Select
            Next

            Dim score = candidate.FoundColumnCount
            If score > bestScore Then
                bestScore = score
                bestMap = candidate
            End If
        Next

        Return If(bestMap, New ReworkHeaderMap())
    End Function

    Private Shared Function HeaderKey(value As String) As String
        Dim header = NormalizeHeader(value)
        Select Case header
            Case "ISLEM TARIHI"
                Return "OPERATION_DATE"
            Case "IS MERKEZI"
                Return "WORK_CENTER"
            Case "IS MERKEZI TANIMI", "IS MERKEZI TANIM"
                Return "WORK_CENTER_DESCRIPTION"
            Case "TUR SABLONU", "TUR SABLON", "TUR ŞABLONU"
                Return "TOUR_TEMPLATE"
            Case "MALZEME ACIKLAMASI", "MALZEME ACIKLAMA"
                Return "MATERIAL_DESCRIPTION"
            Case "TAMAMLANAN MIK TOPL", "TAMAMLANAN MIKTAR TOPLAM", "TAMAMLANAN MIKTARI TOPLAM"
                Return "COMPLETED_QUANTITY"
            Case "OPERASYON ACIKLAMA", "OPERASYON ACIKLAMASI"
                Return "OPERATION_DESCRIPTION"
            Case Else
                Return ""
        End Select
    End Function

    Private Shared Function NormalizeHeader(value As String) As String
        Dim text = If(value, "").Trim().
            Replace("İ", "I").
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
            Replace("ç", "C").
            ToUpperInvariant()
        text = Regex.Replace(text, "[^A-Z0-9]+", " ")
        Return Regex.Replace(text, "\s+", " ").Trim()
    End Function

    Private Shared Function TryParseDate(value As String, ByRef parsed As DateTime) As Boolean
        parsed = DateTime.MinValue
        Dim text = If(value, "").Trim()
        If text = "" Then Return False

        Dim serial As Double
        If Double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, serial) AndAlso
           serial >= 1 AndAlso serial <= 2958465 Then

            Try
                parsed = DateTime.FromOADate(serial)
                Return True
            Catch
            End Try
        End If

        Return DateTime.TryParse(text, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.AllowWhiteSpaces, parsed) OrElse
               DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, parsed)
    End Function

    Private Shared Function TryParseQuantity(value As String, ByRef parsed As Decimal) As Boolean
        parsed = 0D
        Dim text = If(value, "").Replace(ChrW(&HA0), " "c).Trim()
        If text = "" Then Return False

        If Decimal.TryParse(text, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), parsed) Then Return parsed >= 0D
        If Decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, parsed) Then Return parsed >= 0D

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

        Return Decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, parsed) AndAlso parsed >= 0D
    End Function

    Private Shared Function CleanText(value As String) As String
        Return Regex.Replace(If(value, "").Replace(ChrW(&HA0), " "c), "\s+", " ").Trim()
    End Function

    Private Shared Function Cell(cells As Dictionary(Of Integer, String), column As Integer) As String
        If column <= 0 OrElse Not cells.ContainsKey(column) Then Return ""
        Return If(cells(column), "")
    End Function

    Private Shared Function ReadWorksheets(filePath As String) As List(Of ReworkWorksheetRows)
        Dim worksheets As New List(Of ReworkWorksheetRows)()
        Using archive = ZipFile.OpenRead(filePath)
            Dim sharedStrings = ReadSharedStrings(archive)
            For Each entryInfo In GetWorksheetEntries(archive)
                If entryInfo.Entry Is Nothing Then Continue For
                Dim worksheet As New ReworkWorksheetRows With {.SheetName = entryInfo.SheetName}
                Using stream = entryInfo.Entry.Open()
                    Dim document = XDocument.Load(stream)
                    Dim ns = document.Root.Name.Namespace
                    For Each cellElement In document.Descendants(ns + "c")
                        Dim reference = Convert.ToString(cellElement.Attribute("r")?.Value)
                        Dim rowNumber = RowNumberFromCellRef(reference)
                        Dim columnNumber = ColumnNumberFromCellRef(reference)
                        If rowNumber <= 0 OrElse columnNumber <= 0 Then Continue For

                        Dim cellValue = ReadCellValue(cellElement, sharedStrings, ns)
                        If Not worksheet.Rows.ContainsKey(rowNumber) Then
                            worksheet.Rows(rowNumber) = New Dictionary(Of Integer, String)()
                        End If
                        worksheet.Rows(rowNumber)(columnNumber) = cellValue.Trim()
                    Next
                End Using
                worksheets.Add(worksheet)
            Next
        End Using
        Return worksheets
    End Function

    Private Shared Function GetWorksheetEntries(archive As ZipArchive) As List(Of ReworkWorksheetEntry)
        Dim result As New List(Of ReworkWorksheetEntry)()
        Dim workbookEntry = archive.GetEntry("xl/workbook.xml")
        Dim relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels")
        If workbookEntry Is Nothing OrElse relationshipsEntry Is Nothing Then
            result.Add(New ReworkWorksheetEntry With {.Entry = archive.GetEntry("xl/worksheets/sheet1.xml"), .SheetName = "sheet1"})
            Return result
        End If

        Dim relationshipTargets As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Using stream = relationshipsEntry.Open()
            Dim document = XDocument.Load(stream)
            Dim ns = document.Root.Name.Namespace
            For Each relationship In document.Descendants(ns + "Relationship")
                Dim id = Convert.ToString(relationship.Attribute("Id")?.Value)
                Dim target = Convert.ToString(relationship.Attribute("Target")?.Value)
                If id = "" OrElse target = "" Then Continue For
                target = target.Replace("\"c, "/"c).TrimStart("/"c)
                If Not target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) Then target = "xl/" & target
                relationshipTargets(id) = target
            Next
        End Using

        Using stream = workbookEntry.Open()
            Dim document = XDocument.Load(stream)
            Dim ns = document.Root.Name.Namespace
            Dim relationshipNamespace = XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships")
            For Each sheet In document.Descendants(ns + "sheet")
                Dim sheetName = Convert.ToString(sheet.Attribute("name")?.Value)
                Dim id = Convert.ToString(sheet.Attribute(relationshipNamespace + "id")?.Value)
                If id = "" OrElse Not relationshipTargets.ContainsKey(id) Then Continue For
                result.Add(New ReworkWorksheetEntry With {
                    .Entry = archive.GetEntry(relationshipTargets(id)),
                    .SheetName = If(sheetName, "sheet")
                })
            Next
        End Using

        Return result
    End Function

    Private Shared Function ReadSharedStrings(archive As ZipArchive) As List(Of String)
        Dim result As New List(Of String)()
        Dim entry = archive.GetEntry("xl/sharedStrings.xml")
        If entry Is Nothing Then Return result
        Using stream = entry.Open()
            Dim document = XDocument.Load(stream)
            Dim ns = document.Root.Name.Namespace
            For Each item In document.Descendants(ns + "si")
                result.Add(String.Join("", item.Descendants(ns + "t").Select(Function(text) text.Value)))
            Next
        End Using
        Return result
    End Function

    Private Shared Function ReadCellValue(cellElement As XElement,
                                          sharedStrings As List(Of String),
                                          ns As XNamespace) As String
        Dim dataType = Convert.ToString(cellElement.Attribute("t")?.Value)
        If String.Equals(dataType, "inlineStr", StringComparison.OrdinalIgnoreCase) Then
            Dim inlineText = cellElement.Element(ns + "is")
            If inlineText Is Nothing Then Return ""
            Return String.Join("", inlineText.Descendants(ns + "t").Select(Function(text) text.Value))
        End If

        Dim valueElement = cellElement.Element(ns + "v")
        If valueElement Is Nothing Then Return ""
        Dim rawValue = valueElement.Value
        If String.Equals(dataType, "s", StringComparison.OrdinalIgnoreCase) Then
            Dim index As Integer
            If Integer.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, index) AndAlso
               index >= 0 AndAlso index < sharedStrings.Count Then Return sharedStrings(index)
        End If
        Return rawValue
    End Function

    Private Shared Function RowNumberFromCellRef(cellReference As String) As Integer
        Dim match = Regex.Match(If(cellReference, ""), "\d+")
        Dim value As Integer
        If match.Success AndAlso Integer.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, value) Then Return value
        Return 0
    End Function

    Private Shared Function ColumnNumberFromCellRef(cellReference As String) As Integer
        Dim match = Regex.Match(If(cellReference, ""), "^[A-Za-z]+")
        If Not match.Success Then Return 0
        Dim result As Integer = 0
        For Each character In match.Value.ToUpperInvariant()
            result = result * 26 + (AscW(character) - AscW("A"c) + 1)
        Next
        Return result
    End Function

    Private NotInheritable Class ReworkHeaderMap
        Public Property HeaderRowIndex As Integer
        Public Property OperationDateColumn As Integer
        Public Property WorkCenterColumn As Integer
        Public Property WorkCenterDescriptionColumn As Integer
        Public Property TourTemplateColumn As Integer
        Public Property MaterialDescriptionColumn As Integer
        Public Property CompletedQuantityColumn As Integer
        Public Property OperationDescriptionColumn As Integer

        Public ReadOnly Property FoundColumnCount As Integer
            Get
                Return {OperationDateColumn, WorkCenterColumn, WorkCenterDescriptionColumn, TourTemplateColumn,
                    MaterialDescriptionColumn, CompletedQuantityColumn, OperationDescriptionColumn}.
                    Count(Function(column) column > 0)
            End Get
        End Property

        Public ReadOnly Property IsComplete As Boolean
            Get
                Return FoundColumnCount = 7
            End Get
        End Property
    End Class

    Private NotInheritable Class ReworkWorksheetRows
        Public Property SheetName As String = ""
        Public Property Rows As New Dictionary(Of Integer, Dictionary(Of Integer, String))()
    End Class

    Private NotInheritable Class ReworkWorksheetEntry
        Public Property Entry As ZipArchiveEntry
        Public Property SheetName As String = ""
    End Class
End Class

Public NotInheritable Class ReworkRecord
    Public Property OperationDate As DateTime
    Public Property WorkCenter As String = ""
    Public Property WorkCenterDescription As String = ""
    Public Property TourTemplate As String = ""
    Public Property MaterialDescription As String = ""
    Public Property CompletedQuantity As Decimal
    Public Property OperationDescription As String = ""
    Public Property SourceSheet As String = ""
    Public Property SourceRowNumber As Integer
End Class

Public NotInheritable Class ReworkDashboardState
    Public Property OriginalFileName As String = ""
    Public Property StoredFileName As String = ""
    Public Property ImportedAt As String = ""
    Public Property ImportedBy As String = ""
    Public Property ComputerName As String = ""
    Public Property RecordCount As Integer
    Public Property RejectedRowCount As Integer
End Class

Public NotInheritable Class ReworkImportResult
    Public Property IsSuccess As Boolean
    Public Property StatusMessage As String = ""
    Public Property Records As New List(Of ReworkRecord)()
    Public Property RejectedRowCount As Integer
    Public Property InvalidDateRowCount As Integer
    Public Property InvalidQuantityRowCount As Integer
    Public Property ZeroQuantityRowCount As Integer
    Public Property MissingWorkCenterRowCount As Integer
    Public Property MissingMaterialDescriptionRowCount As Integer
    Public Property MissingOperationDescriptionRowCount As Integer
    Public Property State As ReworkDashboardState

    Public Shared Function Failure(message As String) As ReworkImportResult
        Return New ReworkImportResult With {.StatusMessage = If(message, "")}
    End Function
End Class
