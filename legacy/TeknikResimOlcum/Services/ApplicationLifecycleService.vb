Imports System.Collections.Generic
Imports System.Threading.Tasks

Public NotInheritable Class ApplicationLifecycleService
    Private Shared ReadOnly endedSessionIds As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Private Shared ReadOnly sessionLock As New Object()

    Private Sub New()
    End Sub

    Public Shared Sub RunStartupMaintenanceInBackground()
        Task.Run(Sub()
                     Try
                         DataService.EnsureAllFiles()
                     Catch ex As Exception
                         ErrorLogService.Log("ApplicationLifecycleService.EnsureAllFiles", ex)
                     End Try

                     Try
                         DataService.RecoverPendingTransactions()
                     Catch ex As Exception
                         ErrorLogService.Log("ApplicationLifecycleService.RecoverPendingTransactions", ex)
                     End Try

                     Try
                         SqlDatabaseService.TryEnsureSchemaFromConfig()
                     Catch ex As Exception
                         ErrorLogService.Log("ApplicationLifecycleService.EnsureSqlSchema", ex)
                     End Try

                     Try
                         TempFileService.CleanTempFiles()
                     Catch ex As Exception
                         ErrorLogService.Log("ApplicationLifecycleService.StartupTempCleanup", ex)
                     End Try

                     Try
                         Dim migratedCount = CryptoService.MigrateLegacyDrawings()
                         If migratedCount > 0 Then
                             AuditService.Log(
                                 "DRAWING_ENCRYPTION_MIGRATE",
                                 "",
                                 "",
                                 "TROP1 formatından TROP2/AES-GCM formatına dönüştürülen teknik resim: " & migratedCount.ToString())
                         End If
                     Catch ex As Exception
                         ErrorLogService.Log("ApplicationLifecycleService.MigrateLegacyDrawings", ex)
                     End Try
                 End Sub)
    End Sub

    Public Shared Sub EndSessionInBackground(sessionId As String)
        Dim capturedSessionId = If(sessionId, "").Trim()
        If Not MarkSessionForEnd(capturedSessionId) Then Return

        Task.Run(Sub()
                     Try
                         DataService.EndUserSession(capturedSessionId)
                     Catch ex As Exception
                         ErrorLogService.Log("ApplicationLifecycleService.EndSessionInBackground", ex)
                     End Try
                 End Sub)
    End Sub

    Public Shared Sub RunExitCleanupInBackground(sessionId As String)
        Dim ignoredTask As Task = RunExitCleanupAsync(sessionId)
    End Sub

    Public Shared Function RunExitCleanupAsync(sessionId As String) As Task
        Dim capturedSessionId = If(sessionId, "").Trim()

        Return Task.Run(Sub()
                            If MarkSessionForEnd(capturedSessionId) Then
                                Try
                                    DataService.EndUserSession(capturedSessionId)
                                Catch ex As Exception
                                    ErrorLogService.Log("ApplicationLifecycleService.ExitEndSession", ex)
                                End Try
                            End If

                            Try
                                TempFileService.CleanTempFiles()
                            Catch ex As Exception
                                ErrorLogService.Log("ApplicationLifecycleService.ExitTempCleanup", ex)
                            End Try
                        End Sub)
    End Function

    Private Shared Function MarkSessionForEnd(sessionId As String) As Boolean
        If String.IsNullOrWhiteSpace(sessionId) Then Return False

        SyncLock sessionLock
            If endedSessionIds.Contains(sessionId) Then Return False
            endedSessionIds.Add(sessionId)
            Return True
        End SyncLock
    End Function
End Class
