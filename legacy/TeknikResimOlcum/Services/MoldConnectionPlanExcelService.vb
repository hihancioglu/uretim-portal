Imports System.Globalization
Imports System.IO
Imports System.IO.Compression
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Xml.Linq

Public NotInheritable Class MoldConnectionPlanExcelService
    Private Sub New()
    End Sub

    Private Const HeaderRow As Integer = 5
    Private Const FirstDataRow As Integer = 6
    Private Const LastDataRow As Integer = 42

    Private NotInheritable Class WorksheetReadResult
        Public Property Cells As Dictionary(Of String, String) = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Public Property SheetName As String = ""
    End Class

    Private NotInheritable Class WorksheetEntryResult
        Public Property Entry As ZipArchiveEntry
        Public Property SheetName As String = ""
    End Class

    Public Shared Function ImportFromXlsx(filePath As String) As List(Of Dictionary(Of String, String))
        If String.IsNullOrWhiteSpace(filePath) Then Throw New ArgumentException("Excel dosyası seçilmelidir.")
        If Not File.Exists(filePath) Then Throw New FileNotFoundException("Excel dosyası bulunamadı.", filePath)
        If Not String.Equals(Path.GetExtension(filePath), ".xlsx", StringComparison.OrdinalIgnoreCase) Then
            Throw New InvalidOperationException("Sadece .xlsx dosyası içe aktarılabilir.")
        End If

        Dim worksheet = ReadFirstWorksheet(filePath)
        Dim cells = worksheet.Cells
        ValidateHeaders(cells)

        Dim rows As New List(Of Dictionary(Of String, String))()
        Dim importStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        Dim sourceFile = Path.GetFileName(filePath)
        Dim sourceSheet = worksheet.SheetName

        For rowIndex As Integer = FirstDataRow To LastDataRow
            Dim machineName = Cell(cells, "A", rowIndex)
            Dim machineNo = Cell(cells, "B", rowIndex)
            Dim runningMolds = Cell(cells, "C", rowIndex)
            Dim currentMoldNo = Cell(cells, "E", rowIndex)
            Dim currentRackNo = Cell(cells, "F", rowIndex)
            Dim currentPlasticCode = Cell(cells, "G", rowIndex)
            Dim currentTrCode = Cell(cells, "H", rowIndex)
            Dim firstMoldNo = Cell(cells, "K", rowIndex)
            Dim firstRackNo = Cell(cells, "L", rowIndex)
            Dim firstPlasticCode = Cell(cells, "M", rowIndex)
            Dim firstTrCode = Cell(cells, "N", rowIndex)
            Dim secondMoldNo = Cell(cells, "Q", rowIndex)
            Dim secondRackNo = Cell(cells, "R", rowIndex)
            Dim secondPlasticCode = Cell(cells, "S", rowIndex)
            Dim secondTrCode = Cell(cells, "T", rowIndex)

            If AllBlank({machineName, machineNo, runningMolds, currentMoldNo, currentRackNo, currentPlasticCode, currentTrCode,
                         firstMoldNo, firstRackNo, firstPlasticCode, firstTrCode, secondMoldNo, secondRackNo, secondPlasticCode, secondTrCode}) Then
                Continue For
            End If

            rows.Add(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                {"PlanId", "MCP-" & DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) & "-" & rowIndex.ToString("00", CultureInfo.InvariantCulture)},
                {"ImportedAt", importStamp},
                {"ImportedBy", AppState.CurrentUserName},
                {"SourceFile", sourceFile},
                {"SourceSheet", sourceSheet},
                {"SourceRow", rowIndex.ToString(CultureInfo.InvariantCulture)},
                {"MachineName", machineName},
                {"MachineNo", machineNo},
                {"RunningMolds", runningMolds},
                {"CurrentMoldNo", currentMoldNo},
                {"CurrentMoldRackNo", currentRackNo},
                {"CurrentPlasticCode", currentPlasticCode},
                {"CurrentTrCode", currentTrCode},
                {"FirstMoldNo", firstMoldNo},
                {"FirstMoldRackNo", firstRackNo},
                {"FirstPlasticCode", firstPlasticCode},
                {"FirstTrCode", firstTrCode},
                {"SecondMoldNo", secondMoldNo},
                {"SecondMoldRackNo", secondRackNo},
                {"SecondPlasticCode", secondPlasticCode},
                {"SecondTrCode", secondTrCode}
            })
        Next

        Return rows
    End Function

    Private Shared Sub ValidateHeaders(cells As Dictionary(Of String, String))
        Dim expected As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {"A", "MAKINE ADI"},
            {"B", "MAKINE NO"},
            {"C", "CALISAN KALIPLAR"},
            {"E", "KALIP NO"},
            {"F", "KALIP RAF NO"},
            {"G", "P KODU"},
            {"H", "TR"},
            {"I", "1 BAGLANACAK KALIPLAR"},
            {"K", "KALIP NO"},
            {"L", "KALIP RAF NO"},
            {"M", "P KODU"},
            {"N", "TR"},
            {"O", "2 BAGLANACAK KALIPLAR"},
            {"Q", "KALIP NO"},
            {"R", "KALIP RAF NO"},
            {"S", "P KODU"},
            {"T", "TR"}
        }

        Dim missing As New List(Of String)()
        For Each item In expected
            Dim actual = NormalizeHeader(Cell(cells, item.Key, HeaderRow))
            Dim wanted = NormalizeHeader(item.Value)
            If actual = "" OrElse Not actual.Contains(wanted) Then
                missing.Add(item.Key & HeaderRow.ToString(CultureInfo.InvariantCulture) & "=" & item.Value)
            End If
        Next

        If missing.Count > 0 Then
            Throw New InvalidOperationException("Excel başlıkları beklenen formatta değil. Kontrol edin: " & String.Join(", ", missing))
        End If
    End Sub

    Private Shared Function ReadFirstWorksheet(filePath As String) As WorksheetReadResult
        Dim result As New WorksheetReadResult()
        Dim cells = result.Cells

        Using archive = ZipFile.OpenRead(filePath)
            Dim sharedStrings = ReadSharedStrings(archive)
            Dim worksheet = GetFirstWorksheetEntry(archive)
            Dim sheetEntry As ZipArchiveEntry = If(worksheet Is Nothing, Nothing, worksheet.Entry)
            If worksheet IsNot Nothing Then result.SheetName = If(worksheet.SheetName, "").Trim()
            If sheetEntry Is Nothing Then Throw New InvalidOperationException("Excel çalışma sayfası bulunamadı.")

            Using stream = sheetEntry.Open()
                Dim doc = XDocument.Load(stream)
                Dim ns = doc.Root.Name.Namespace

                For Each cellElement In doc.Descendants(ns + "c")
                    Dim cellRefAttr = cellElement.Attribute("r")
                    If cellRefAttr Is Nothing Then Continue For

                    Dim cellRef = cellRefAttr.Value
                    Dim rowNumber = RowNumberFromCellRef(cellRef)
                    If rowNumber < HeaderRow OrElse rowNumber > LastDataRow Then Continue For

                    Dim value = ReadCellValue(cellElement, sharedStrings, ns)
                    cells(cellRef.ToUpperInvariant()) = value
                Next
            End Using
        End Using

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
                Dim textParts = item.Descendants(ns + "t").Select(Function(t) t.Value)
                result.Add(String.Join("", textParts))
            Next
        End Using

        Return result
    End Function

    Private Shared Function GetFirstWorksheetEntry(archive As ZipArchive) As WorksheetEntryResult
        Dim workbookEntry = archive.GetEntry("xl/workbook.xml")
        Dim relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels")
        If workbookEntry Is Nothing OrElse relsEntry Is Nothing Then Return New WorksheetEntryResult With {.Entry = archive.GetEntry("xl/worksheets/sheet1.xml"), .SheetName = "sheet1"}

        Dim relationshipId As String = ""
        Dim sheetName As String = ""
        Using stream = workbookEntry.Open()
            Dim doc = XDocument.Load(stream)
            Dim ns = doc.Root.Name.Namespace
            Dim relNs = XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships")
            Dim sheet = doc.Descendants(ns + "sheet").FirstOrDefault()
            If sheet IsNot Nothing Then
                Dim nameAttr = sheet.Attribute("name")
                If nameAttr IsNot Nothing Then sheetName = nameAttr.Value
                Dim idAttr = sheet.Attribute(relNs + "id")
                If idAttr IsNot Nothing Then relationshipId = idAttr.Value
            End If
        End Using

        If sheetName.Trim() = "" Then sheetName = "sheet1"
        If relationshipId = "" Then Return New WorksheetEntryResult With {.Entry = archive.GetEntry("xl/worksheets/sheet1.xml"), .SheetName = sheetName}

        Using stream = relsEntry.Open()
            Dim doc = XDocument.Load(stream)
            Dim ns = doc.Root.Name.Namespace
            Dim rel = doc.Descendants(ns + "Relationship").
                FirstOrDefault(Function(r) String.Equals(Convert.ToString(r.Attribute("Id")?.Value), relationshipId, StringComparison.OrdinalIgnoreCase))

            If rel IsNot Nothing Then
                Dim target = Convert.ToString(rel.Attribute("Target")?.Value).Replace("\"c, "/"c)
                If target.StartsWith("/", StringComparison.Ordinal) Then target = target.TrimStart("/"c)
                If Not target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) Then target = "xl/" & target
                Return New WorksheetEntryResult With {.Entry = archive.GetEntry(target), .SheetName = sheetName}
            End If
        End Using

        Return New WorksheetEntryResult With {.Entry = archive.GetEntry("xl/worksheets/sheet1.xml"), .SheetName = sheetName}
    End Function

    Private Shared Function ReadCellValue(cellElement As XElement, sharedStrings As List(Of String), ns As XNamespace) As String
        Dim dataType = Convert.ToString(cellElement.Attribute("t")?.Value)

        If String.Equals(dataType, "inlineStr", StringComparison.OrdinalIgnoreCase) Then
            Return NormalizeCellText(String.Join("", cellElement.Descendants(ns + "t").Select(Function(t) t.Value)))
        End If

        Dim raw = Convert.ToString(cellElement.Element(ns + "v")?.Value)
        If String.IsNullOrWhiteSpace(raw) Then Return ""

        If String.Equals(dataType, "s", StringComparison.OrdinalIgnoreCase) Then
            Dim index As Integer
            If Integer.TryParse(raw, index) AndAlso index >= 0 AndAlso index < sharedStrings.Count Then
                Return NormalizeCellText(sharedStrings(index))
            End If
        End If

        If String.Equals(dataType, "b", StringComparison.OrdinalIgnoreCase) Then
            Return If(raw = "1", "TRUE", "FALSE")
        End If

        Return NormalizeCellText(raw)
    End Function

    Private Shared Function Cell(cells As Dictionary(Of String, String), columnName As String, rowNumber As Integer) As String
        Dim key = columnName.ToUpperInvariant() & rowNumber.ToString(CultureInfo.InvariantCulture)
        If cells.ContainsKey(key) Then Return cells(key)
        Return ""
    End Function

    Private Shared Function RowNumberFromCellRef(cellRef As String) As Integer
        Dim m = Regex.Match(cellRef, "\d+")
        If Not m.Success Then Return 0
        Dim rowNumber As Integer
        If Integer.TryParse(m.Value, rowNumber) Then Return rowNumber
        Return 0
    End Function

    Private Shared Function NormalizeCellText(value As String) As String
        Return If(value, "").Replace(vbCrLf, " ").Replace(vbCr, " ").Replace(vbLf, " ").Trim()
    End Function

    Private Shared Function NormalizeHeader(value As String) As String
        Dim text = If(value, "").Trim().ToUpperInvariant()
        text = text.Replace("İ", "I").Replace("İ", "I").Replace("ı", "I").
            Replace("Ğ", "G").Replace("Ü", "U").Replace("Ş", "S").
            Replace("Ö", "O").Replace("Ç", "C")

        Dim sb As New StringBuilder()
        For Each ch In text
            If Char.IsLetterOrDigit(ch) Then sb.Append(ch)
        Next

        Return sb.ToString()
    End Function

    Private Shared Function AllBlank(values As IEnumerable(Of String)) As Boolean
        Return values.All(Function(v) String.IsNullOrWhiteSpace(v))
    End Function
End Class
