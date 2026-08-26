Imports System.Diagnostics
Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Windows.Forms

Module Program
    Private Const AppUserModelId As String = "TeknikResimOlcum.KontrolProgram"
    Private Const SingleInstanceMutexName As String = "Local\TeknikResimOlcum.KontrolProgram.SingleInstance"
    Private Const SwRestore As Integer = 9

    <STAThread>
    Sub Main()
        Using singleInstanceMutex As New Mutex(False, SingleInstanceMutexName)
            Dim ownsSingleInstance As Boolean = False
            Try
                Try
                    ownsSingleInstance = singleInstanceMutex.WaitOne(0, False)
                Catch ex As AbandonedMutexException
                    ownsSingleInstance = True
                    ErrorLogService.Log("Program.SingleInstance.AbandonedMutex", ex)
                End Try

                If Not ownsSingleInstance Then
                    If Not TryActivateExistingInstance() Then
                        MessageBox.Show(
                            "Program zaten açık. Mevcut program penceresinden devam edin.",
                            "Program zaten açık",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information)
                    End If
                    Return
                End If

                ' Önceki sürümler tek-örnek mutex'i kullanmadığı için güncelleme geçişinde
                ' hâlâ açık kalmış eski bir pencere bulunabilir. Yeni giriş oluşturmadan
                ' önce onu da süreç listesinden yakala ve öne getir.
                If TryActivateExistingInstance() Then Return

                TrySetAppUserModelId()

                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException)
                AddHandler Application.ThreadException, AddressOf HandleThreadException
                AddHandler AppDomain.CurrentDomain.UnhandledException, AddressOf HandleUnhandledException

                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)
                Application.EnableVisualStyles()
                Application.SetCompatibleTextRenderingDefault(False)

                AddHandler Application.ApplicationExit, Sub(sender, e)
                                                            ApplicationInstanceService.StopTracking()
                                                            ApplicationLifecycleService.RunExitCleanupInBackground(AppState.CurrentSessionId)
                                                        End Sub

                Using login As New FrmLogin()
                    If login.ShowDialog() = DialogResult.OK Then
                        Threading.Tasks.Task.Run(Sub() ApplicationInstanceService.StartTracking())
                        ApplicationLifecycleService.RunStartupMaintenanceInBackground()
                        Application.Run(New FrmMain())
                    End If
                End Using
            Finally
                If ownsSingleInstance Then
                    Try
                        singleInstanceMutex.ReleaseMutex()
                    Catch ex As ApplicationException
                        ErrorLogService.Log("Program.SingleInstance.ReleaseMutex", ex)
                    End Try
                End If
            End Try
        End Using
    End Sub

    Private Function TryActivateExistingInstance() As Boolean
        Try
            Dim currentProcess = Process.GetCurrentProcess()
            For Each candidate In Process.GetProcessesByName(currentProcess.ProcessName)
                Try
                    If candidate.Id = currentProcess.Id Then Continue For
                    Dim windowHandle = candidate.MainWindowHandle
                    If windowHandle = IntPtr.Zero Then Continue For

                    NativeMethods.ShowWindowAsync(windowHandle, SwRestore)
                    NativeMethods.SetForegroundWindow(windowHandle)
                    Return True
                Finally
                    candidate.Dispose()
                End Try
            Next
        Catch ex As Exception
            ErrorLogService.Log("Program.SingleInstance.ActivateExisting", ex)
        End Try

        Return False
    End Function

    Private Sub TrySetAppUserModelId()
        Try
            NativeMethods.SetCurrentProcessExplicitAppUserModelID(AppUserModelId)
        Catch ex As Exception
            ErrorLogService.Log("Program.TrySetAppUserModelId", ex)
        End Try
    End Sub

    Private Sub HandleThreadException(sender As Object, e As Threading.ThreadExceptionEventArgs)
        ErrorLogService.Log("Program.ApplicationThreadException", e.Exception)
        MessageBox.Show(
            "Beklenmeyen bir uygulama hatası oluştu. Ayrıntılar hata günlüğüne kaydedildi.",
            "Uygulama hatası",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error)
    End Sub

    Private Sub HandleUnhandledException(sender As Object, e As UnhandledExceptionEventArgs)
        Dim ex = TryCast(e.ExceptionObject, Exception)
        If ex Is Nothing Then ex = New Exception("İşlenmeyen ve Exception türünde olmayan hata.")
        ErrorLogService.Log("Program.AppDomainUnhandledException", ex, "IsTerminating=" & e.IsTerminating.ToString())
    End Sub

    Private NotInheritable Class NativeMethods
        Private Sub New()
        End Sub

        <DllImport("shell32.dll", CharSet:=CharSet.Unicode, SetLastError:=True)>
        Friend Shared Function SetCurrentProcessExplicitAppUserModelID(appId As String) As Integer
        End Function

        <DllImport("user32.dll")>
        Friend Shared Function ShowWindowAsync(windowHandle As IntPtr, command As Integer) As Boolean
        End Function

        <DllImport("user32.dll")>
        Friend Shared Function SetForegroundWindow(windowHandle As IntPtr) As Boolean
        End Function
    End Class
End Module
