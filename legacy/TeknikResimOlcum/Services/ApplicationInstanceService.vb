Imports System.Diagnostics
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Threading
Imports System.Windows.Forms

Public NotInheritable Class ApplicationInstanceService
    Private Shared ReadOnly instanceId As String = Guid.NewGuid().ToString("N")
    Private Shared ReadOnly syncRoot As New Object()
    Private Shared heartbeatTimer As System.Threading.Timer
    Private Shared startedAt As DateTime = DateTime.Now
    Private Shared isStarted As Boolean = False
    Private Shared isUpdating As Boolean = False

    Private Sub New()
    End Sub

    Public Shared Sub StartTracking()
        SyncLock syncRoot
            If isStarted Then Return
            isStarted = True
            startedAt = DateTime.Now
        End SyncLock

        UpdateNow()
        heartbeatTimer = New System.Threading.Timer(
            Sub(state) UpdateNow(),
            Nothing,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30))
    End Sub

    Public Shared Sub UpdateNow()
        SyncLock syncRoot
            If Not isStarted OrElse isUpdating Then Return
            isUpdating = True
        End SyncLock

        Try
            Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            CsvUtil.UpdateRowsLocked(
                AppPaths.RunningInstancesCsv,
                DataService.RunningInstanceHeaders,
                Sub(rows)
                    rows.RemoveAll(Function(row) IsOlderThan(DataService.GetValue(row, "LastSeen"), TimeSpan.FromDays(1)))

                    Dim existing = rows.FirstOrDefault(
                        Function(row) String.Equals(
                            DataService.GetValue(row, "InstanceId"),
                            instanceId,
                            StringComparison.OrdinalIgnoreCase))

                    If existing Is Nothing Then
                        existing = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                        rows.Add(existing)
                    End If

                    existing("InstanceId") = instanceId
                    existing("ComputerName") = Environment.MachineName
                    existing("WindowsUser") = Environment.UserName
                    existing("AppUser") = If(AppState.CurrentUserName, "")
                    existing("Role") = If(AppState.CurrentRole, "")
                    existing("ProcessId") = Environment.ProcessId.ToString()
                    existing("Version") = CurrentBuildVersion()
                    existing("StartedAt") = startedAt.ToString("yyyy-MM-dd HH:mm:ss")
                    existing("LastSeen") = nowText
                    existing("ExecutablePath") = Environment.ProcessPath
                End Sub)
        Catch ex As Exception
            ErrorLogService.Log("ApplicationInstanceService.UpdateNow", ex)
        Finally
            SyncLock syncRoot
                isUpdating = False
            End SyncLock
        End Try
    End Sub

    Public Shared Sub StopTracking()
        SyncLock syncRoot
            If Not isStarted Then Return
            isStarted = False
            If heartbeatTimer IsNot Nothing Then
                heartbeatTimer.Dispose()
                heartbeatTimer = Nothing
            End If
        End SyncLock

        For attempt As Integer = 1 To 40
            Dim updateInProgress As Boolean
            SyncLock syncRoot
                updateInProgress = isUpdating
            End SyncLock
            If Not updateInProgress Then Exit For
            Thread.Sleep(50)
        Next

        Try
            CsvUtil.UpdateRowsLocked(
                AppPaths.RunningInstancesCsv,
                DataService.RunningInstanceHeaders,
                Sub(rows)
                    rows.RemoveAll(
                        Function(row) String.Equals(
                            DataService.GetValue(row, "InstanceId"),
                            instanceId,
                            StringComparison.OrdinalIgnoreCase))
                End Sub)
        Catch ex As Exception
            ErrorLogService.Log("ApplicationInstanceService.StopTracking", ex)
        End Try
    End Sub

    Public Shared Function GetInstancesForAdmin() As List(Of Dictionary(Of String, String))
        If Not AppState.IsAdmin Then
            Throw New UnauthorizedAccessException("Çalışan program örneklerini yalnızca Admin görebilir.")
        End If

        Dim now = DateTime.Now
        Return CsvUtil.ReadRows(AppPaths.RunningInstancesCsv).
            Select(
                Function(row)
                    Dim copy = New Dictionary(Of String, String)(row, StringComparer.OrdinalIgnoreCase)
                    Dim lastSeen As DateTime
                    Dim ageSeconds As Integer = Integer.MaxValue
                    If TryParseDate(DataService.GetValue(row, "LastSeen"), lastSeen) Then
                        ageSeconds = Math.Max(0, CInt((now - lastSeen).TotalSeconds))
                    End If
                    copy("AgeSeconds") = If(ageSeconds = Integer.MaxValue, "", ageSeconds.ToString())
                    copy("StatusText") =
                        If(ageSeconds <= 75,
                           "PROGRAM AÇIK",
                           If(ageSeconds <= 180, "YANIT GECİKMİŞ", "ESKİ KAYIT"))
                    Return copy
                End Function).
            OrderBy(Function(row) If(DataService.GetValue(row, "StatusText") = "PROGRAM AÇIK", 0, 1)).
            ThenBy(Function(row) DataService.GetValue(row, "ComputerName"), StringComparer.CurrentCultureIgnoreCase).
            ToList()
    End Function

    Public Shared Function CurrentBuildVersion() As String
        Try
            Dim manifestPath = Path.Combine(AppPaths.BaseDir, UpdatePackageSecurity.ManifestFileName)
            If File.Exists(manifestPath) Then
                Dim manifest = File.ReadAllText(manifestPath)
                Dim stamp = UpdatePackageSecurity.ManifestValue(manifest, "BuildStamp")
                If stamp <> "" Then Return stamp
            End If
        Catch ex As Exception
            ErrorLogService.Log("ApplicationInstanceService.CurrentBuildVersion", ex)
        End Try

        Return Application.ProductVersion
    End Function

    Private Shared Function IsOlderThan(text As String, age As TimeSpan) As Boolean
        Dim value As DateTime
        Return TryParseDate(text, value) AndAlso value < DateTime.Now.Subtract(age)
    End Function

    Private Shared Function TryParseDate(text As String, ByRef value As DateTime) As Boolean
        Return DateTime.TryParseExact(
                   If(text, "").Trim(),
                   "yyyy-MM-dd HH:mm:ss",
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   value) OrElse DateTime.TryParse(text, value)
    End Function
End Class
