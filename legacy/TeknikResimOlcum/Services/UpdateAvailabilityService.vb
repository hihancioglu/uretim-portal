Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Windows.Forms

Public NotInheritable Class UpdateAvailabilityService
    Private Sub New()
    End Sub

    Public NotInheritable Class UpdateAvailabilityInfo
        Public Property IsAvailable As Boolean
        Public Property PackagePath As String = ""
        Public Property CurrentBuildStamp As String = ""
        Public Property PublishedBuildStamp As String = ""
        Public Property AvailableBuildStamp As String = ""
    End Class

    Public Shared Function CheckForUpdate() As UpdateAvailabilityInfo
        Dim result As New UpdateAvailabilityInfo With {
            .CurrentBuildStamp = InstalledBuildStamp(),
            .PublishedBuildStamp = PublishedBuildStamp()
        }

        Try
            Directory.CreateDirectory(AppPaths.UpdatesDir)

            Dim currentValue = BuildStampValue(result.CurrentBuildStamp)
            Dim publishedValue = BuildStampValue(result.PublishedBuildStamp)
            Dim bestValue As Long = Math.Max(currentValue, publishedValue)

            For Each packagePath In Directory.GetFiles(AppPaths.UpdatesDir, "*.zip", SearchOption.TopDirectoryOnly).
                OrderByDescending(Function(path) File.GetLastWriteTimeUtc(path))

                Dim packageStamp As String = ""
                Dim validationMessage As String = ""
                If Not UpdatePackageSecurity.TryGetValidatedBuildStamp(packagePath, packageStamp, validationMessage) Then Continue For

                Dim packageValue = BuildStampValue(packageStamp)
                If packageValue > bestValue Then
                    bestValue = packageValue
                    result.IsAvailable = True
                    result.PackagePath = packagePath
                    result.AvailableBuildStamp = packageStamp
                End If
            Next
        Catch ex As Exception
            ErrorLogService.Log("UpdateAvailabilityService.CheckForUpdate", ex)
            result.IsAvailable = False
            result.PackagePath = ""
            result.AvailableBuildStamp = ""
        End Try

        Return result
    End Function

    Private Shared Function InstalledBuildStamp() As String
        Try
            Dim manifestPath = Path.Combine(Application.StartupPath, UpdatePackageSecurity.ManifestFileName)
            If Not File.Exists(manifestPath) Then Return ""

            Dim manifestText = File.ReadAllText(manifestPath, Encoding.UTF8)
            Return UpdatePackageSecurity.ManifestValue(manifestText, "BuildStamp")
        Catch ex As Exception
            ErrorLogService.Log("UpdateAvailabilityService.InstalledBuildStamp", ex)
            Return ""
        End Try
    End Function

    Private Shared Function PublishedBuildStamp() As String
        Try
            If Not File.Exists(AppPaths.CurrentVersionFile) Then Return ""
            Return File.ReadAllText(AppPaths.CurrentVersionFile, Encoding.UTF8).Trim()
        Catch ex As Exception
            ErrorLogService.Log("UpdateAvailabilityService.PublishedBuildStamp", ex)
            Return ""
        End Try
    End Function

    Private Shared Function BuildStampValue(value As String) As Long
        Dim digits = New String(If(value, "").Where(Function(ch) Char.IsDigit(ch)).ToArray())
        Dim parsed As Long
        If Long.TryParse(digits, parsed) Then Return parsed
        Return 0
    End Function
End Class
