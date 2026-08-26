Imports System.IO

Public NotInheritable Class DatabaseConfig
    Private Sub New()
    End Sub

    Public Shared ReadOnly Property ConfigPath As String
        Get
            Return Path.Combine(AppPaths.DataDir, "Database.config")
        End Get
    End Property

    Public Shared Function IsSqlEnabled() As Boolean
        Return String.Equals(ReadSetting("Mode", "CSV"), "SQL", StringComparison.OrdinalIgnoreCase)
    End Function

    Public Shared Function ConnectionString() As String
        Return ReadSetting("ConnectionString", "")
    End Function

    Private Shared Function ReadSetting(key As String, defaultValue As String) As String
        Try
            If Not File.Exists(ConfigPath) Then Return defaultValue

            For Each rawLine In File.ReadAllLines(ConfigPath)
                Dim line = If(rawLine, "").Trim()
                If line = "" OrElse line.StartsWith("#") OrElse line.StartsWith(";") Then Continue For

                Dim idx = line.IndexOf("="c)
                If idx <= 0 Then Continue For

                Dim k = line.Substring(0, idx).Trim()
                Dim v = line.Substring(idx + 1).Trim()
                If String.Equals(k, key, StringComparison.OrdinalIgnoreCase) Then Return v
            Next
        Catch ex As Exception
            ErrorLogService.Log("DatabaseConfig.ReadSetting", ex, "Key=" & If(key, ""))
        End Try

        Return defaultValue
    End Function
End Class
