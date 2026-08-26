Imports System.IO
Imports System.Text.RegularExpressions

Public NotInheritable Class FileNameUtil
    Private Sub New()
    End Sub

    Public Shared Function SafeFileName(text As String) As String
        Dim invalid = Regex.Escape(New String(Path.GetInvalidFileNameChars()))
        Dim pattern = "[" & invalid & "]+"
        Dim safe = Regex.Replace(text.Trim(), pattern, "_")
        safe = safe.Replace(" ", "_")
        Return safe
    End Function
End Class
