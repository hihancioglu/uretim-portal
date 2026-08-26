Imports System.Globalization
Imports System.Text.RegularExpressions

Public NotInheritable Class NumberUtil
    Private Sub New()
    End Sub

    Public Shared Function NormalizeMeasurementInput(text As String) As String
        If text Is Nothing Then Return ""

        Dim s = text.Trim()
        If s = "" Then Return ""

        ' Bluetooth HID kumpaslar bazen ondalık ayırıcıyı klavye dizilimi nedeniyle
        ' "Ç" olarak gönderebiliyor: +012Ç59 -> 12.59
        s = s.Replace("Ç", ".").Replace("ç", ".").Replace(",", ".")
        s = Regex.Replace(s, "\s+", "")
        s = Regex.Replace(s, "[^0-9\+\-\.]", "")

        Dim sign = ""
        If s.StartsWith("-", StringComparison.Ordinal) Then sign = "-"
        s = s.Replace("+", "").Replace("-", "")
        If s = "" Then Return sign

        Dim lastDecimal = s.LastIndexOf("."c)
        Dim integerPart As String
        Dim fractionPart As String = ""
        Dim hasDecimal = lastDecimal >= 0

        If hasDecimal Then
            integerPart = s.Substring(0, lastDecimal).Replace(".", "")
            fractionPart = s.Substring(lastDecimal + 1).Replace(".", "")
        Else
            integerPart = s.Replace(".", "")
        End If

        integerPart = integerPart.TrimStart("0"c)
        If integerPart = "" Then integerPart = "0"

        If hasDecimal Then
            Return sign & integerPart & "." & fractionPart
        End If

        Return sign & integerPart
    End Function

    Public Shared Function TryParseDecimal(text As String, ByRef value As Decimal) As Boolean
        value = 0D
        If text Is Nothing Then Return False

        Dim s = NormalizeMeasurementInput(text)
        If s = "" Then Return False

        ' Eğer sadece nokta içeriyor ve virgül içermiyorsa,
        ' bunu ondalık ayırıcı olarak kabul edip InvariantCulture ile parse et.
        If s.Contains(".") AndAlso Not s.Contains(",") Then
            If Decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, value) Then Return True
        End If

        ' Eğer sadece virgül içeriyor ve nokta içermiyorsa,
        ' bunu tr-TR biçimi gibi ele al.
        If s.Contains(",") AndAlso Not s.Contains(".") Then
            If Decimal.TryParse(s, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), value) Then Return True
        End If

        ' Hem nokta hem virgül varsa olası iki biçimi de dene.
        If s.Contains(".") AndAlso s.Contains(",") Then
            ' 1.234,56 -> tr-TR
            If Decimal.TryParse(s, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), value) Then Return True
            ' 1,234.56 -> invariant için binlik virgülü kaldır
            Dim inv As String = s.Replace(",", "")
            If Decimal.TryParse(inv, NumberStyles.Any, CultureInfo.InvariantCulture, value) Then Return True
        End If

        ' Son çareler
        If Decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, value) Then Return True
        If Decimal.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, value) Then Return True
        If Decimal.TryParse(s, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), value) Then Return True

        Return False
    End Function

    Public Shared Function DecToCsv(value As Decimal) As String
        Return value.ToString("0.#####", CultureInfo.InvariantCulture)
    End Function

    Public Shared Function CsvToDec(text As String) As Decimal
        Dim d As Decimal = 0D
        TryParseDecimal(text, d)
        Return d
    End Function
End Class
