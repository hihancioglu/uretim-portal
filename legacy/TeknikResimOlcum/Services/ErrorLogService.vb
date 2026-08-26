Imports System.Diagnostics
Imports System.IO
Imports System.Text
Imports System.Threading

Public NotInheritable Class ErrorLogService
    Private Shared ReadOnly writeLock As New Object()
    Private Const RetryCount As Integer = 5
    Private Const RetryDelayMs As Integer = 80

    Private Sub New()
    End Sub

    Public Shared Sub Log(context As String, ex As Exception, Optional detail As String = "")
        If ex Is Nothing Then ex = New Exception("Bilinmeyen hata.")

        Dim entry = BuildEntry(context, ex, detail)
        If TryAppend(AppPaths.ApplicationErrorsLog, entry) Then Return

        Dim localLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TeknikResimOlcum",
            "Logs",
            "ApplicationErrors.log")

        If Not TryAppend(localLogPath, entry) Then
            Debug.WriteLine("Hata günlüğü yazılamadı: " & context & " | " & ex.ToString())
        End If
    End Sub

    Private Shared Function BuildEntry(context As String, ex As Exception, detail As String) As String
        Dim userName = If(String.IsNullOrWhiteSpace(AppState.CurrentUserName), "(oturum yok)", AppState.CurrentUserName)
        Dim roleName = If(String.IsNullOrWhiteSpace(AppState.CurrentRole), "(rol yok)", AppState.CurrentRole)
        Dim builder As New StringBuilder()

        builder.AppendLine(New String("-"c, 100))
        builder.AppendLine("TimeLocal=" & DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"))
        builder.AppendLine("TimeUtc=" & DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"))
        builder.AppendLine("Machine=" & Environment.MachineName)
        builder.AppendLine("User=" & userName)
        builder.AppendLine("Role=" & roleName)
        builder.AppendLine("Context=" & If(context, ""))
        If Not String.IsNullOrWhiteSpace(detail) Then builder.AppendLine("Detail=" & detail)
        builder.AppendLine("Exception=" & ex.ToString())
        Return builder.ToString()
    End Function

    Private Shared Function TryAppend(logPath As String, entry As String) As Boolean
        Try
            SyncLock writeLock
                Dim directoryPath = Path.GetDirectoryName(logPath)
                If Not String.IsNullOrWhiteSpace(directoryPath) Then Directory.CreateDirectory(directoryPath)

                Dim bytes = New UTF8Encoding(False).GetBytes(entry)
                For attempt As Integer = 1 To RetryCount
                    Try
                        Using stream As New FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.Read)
                            stream.Write(bytes, 0, bytes.Length)
                            stream.Flush(True)
                        End Using
                        Return True
                    Catch ex As IOException
                        If attempt = RetryCount Then Exit For
                        Thread.Sleep(RetryDelayMs)
                    Catch ex As UnauthorizedAccessException
                        Exit For
                    End Try
                Next
            End SyncLock
        Catch loggingEx As Exception
            Debug.WriteLine("Hata günlüğü yazma işlemi başarısız: " & loggingEx.Message)
        End Try

        Return False
    End Function
End Class
