Imports System.Linq
Imports System.Text.RegularExpressions

Public NotInheritable Class ProductNameResolver
    Private Sub New()
    End Sub

    Public Shared Function Resolve(products As IEnumerable(Of ProductInfo),
                                   trCode As String,
                                   Optional plasticCode As String = "",
                                   Optional moldCode As String = "") As String
        If products Is Nothing Then Return ""

        Dim namedProducts = products.
            Where(Function(product) product IsNot Nothing AndAlso SafeText(product.ProductName) <> "").
            ToList()
        If namedProducts.Count = 0 Then Return ""

        Dim trKey = NormalizeTrKey(trCode)
        If trKey <> "" Then
            Dim exactTrProduct = namedProducts.
                Where(Function(product) String.Equals(NormalizeTrKey(product.TrCode), trKey, StringComparison.OrdinalIgnoreCase)).
                OrderByDescending(Function(product) String.Equals(SafeText(product.IsActive), "YES", StringComparison.OrdinalIgnoreCase)).
                ThenByDescending(Function(product) SafeText(product.DrawingRev), StringComparer.OrdinalIgnoreCase).
                FirstOrDefault()
            If exactTrProduct IsNot Nothing Then Return SafeText(exactTrProduct.ProductName)
        End If

        Dim plasticName = ResolveUniqueName(
            namedProducts,
            plasticCode,
            Function(product) product.PlasticCode)
        If plasticName <> "" Then Return plasticName

        Return ResolveUniqueName(
            namedProducts,
            moldCode,
            Function(product) product.MoldCode)
    End Function

    Public Shared Function EnrichDisplayText(products As IEnumerable(Of ProductInfo), displayText As String) As String
        Dim entries = SafeText(displayText).
            Replace(vbCrLf, vbLf).
            Replace(vbCr, vbLf).
            Split({vbLf}, StringSplitOptions.RemoveEmptyEntries)

        Dim enrichedEntries As New List(Of String)()
        For Each entry In entries
            Dim parts = entry.Split("|"c).
                Select(Function(part) part.Trim()).
                ToList()

            If parts.Count >= 2 AndAlso
               (parts(1) = "" OrElse String.Equals(parts(1), "Ürün adı tanımsız", StringComparison.OrdinalIgnoreCase)) Then
                Dim productName = Resolve(products, parts(0))
                parts(1) = productName
            End If

            enrichedEntries.Add(String.Join(" | ", parts))
        Next

        Return String.Join(Environment.NewLine, enrichedEntries)
    End Function

    Private Shared Function ResolveUniqueName(products As IEnumerable(Of ProductInfo),
                                              lookupValue As String,
                                              selector As Func(Of ProductInfo, String)) As String
        Dim lookupKey = NormalizeGeneralKey(lookupValue)
        If lookupKey = "" Then Return ""

        Dim names = products.
            Where(Function(product) String.Equals(NormalizeGeneralKey(selector(product)), lookupKey, StringComparison.OrdinalIgnoreCase)).
            Select(Function(product) SafeText(product.ProductName)).
            Where(Function(name) name <> "").
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToList()

        If names.Count = 1 Then Return names(0)
        Return ""
    End Function

    Private Shared Function NormalizeTrKey(value As String) As String
        Dim text = SafeText(value).ToUpperInvariant()
        If text = "" Then Return ""

        text = Regex.Replace(text, "^TR\s*[-_/]?\s*", "", RegexOptions.IgnoreCase)

        Dim numericOnly = Regex.Match(text, "^0*(\d+)(?:[\.,]0+)?$")
        If numericOnly.Success Then
            Dim numericValue As Long
            If Long.TryParse(numericOnly.Groups(1).Value, numericValue) Then
                Return numericValue.ToString(Globalization.CultureInfo.InvariantCulture)
            End If
        End If

        Dim compact = Regex.Replace(text, "[^A-Z0-9]", "")
        Dim numericPrefix = Regex.Match(compact, "^0*(\d+)(.*)$")
        If numericPrefix.Success Then
            Dim numericValue As Long
            If Long.TryParse(numericPrefix.Groups(1).Value, numericValue) Then
                Return numericValue.ToString(Globalization.CultureInfo.InvariantCulture) & numericPrefix.Groups(2).Value
            End If
        End If

        Return compact
    End Function

    Private Shared Function NormalizeGeneralKey(value As String) As String
        Return Regex.Replace(SafeText(value).ToUpperInvariant(), "[^A-Z0-9]", "")
    End Function

    Private Shared Function SafeText(value As String) As String
        Return If(value, "").Trim()
    End Function
End Class
