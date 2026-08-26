Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms

Public NotInheritable Class AppNotificationService
    Private Sub New()
    End Sub

    Private Shared notifyIcon As NotifyIcon = Nothing
    Private Shared ownerForm As Form = Nothing
    Private Shared initialized As Boolean = False

    Public Shared Sub Initialize(owner As Form)
        If initialized Then
            ownerForm = owner
            Return
        End If

        Try
            ownerForm = owner

            notifyIcon = New NotifyIcon() With {
                .Icon = ResolveIcon(owner),
                .Text = "Teknik Resim Ölçüm",
                .Visible = True
            }

            AddHandler notifyIcon.Click, AddressOf NotifyIcon_Click
            AddHandler notifyIcon.BalloonTipClicked, AddressOf NotifyIcon_Click

            initialized = True
        Catch ex As Exception
            initialized = False
            ErrorLogService.Log("AppNotificationService.Initialize", ex)
        End Try
    End Sub

    Public Shared Sub ShowInfo(title As String, message As String)
        Show(title, message, ToolTipIcon.Info)
    End Sub

    Public Shared Sub ShowWarning(title As String, message As String)
        Show(title, message, ToolTipIcon.Warning)
    End Sub

    Public Shared Sub Shutdown()
        Try
            If notifyIcon Is Nothing Then Return

            RemoveHandler notifyIcon.Click, AddressOf NotifyIcon_Click
            RemoveHandler notifyIcon.BalloonTipClicked, AddressOf NotifyIcon_Click
            notifyIcon.Visible = False
            notifyIcon.Dispose()
        Catch ex As Exception
            ErrorLogService.Log("AppNotificationService.Shutdown", ex)
        Finally
            notifyIcon = Nothing
            ownerForm = Nothing
            initialized = False
        End Try
    End Sub

    Private Shared Sub Show(title As String, message As String, icon As ToolTipIcon)
        Try
            If notifyIcon Is Nothing OrElse Not initialized Then Return

            notifyIcon.BalloonTipTitle = LimitText(title, 63)
            notifyIcon.BalloonTipText = LimitText(message, 255)
            notifyIcon.BalloonTipIcon = icon
            notifyIcon.Visible = True
            notifyIcon.ShowBalloonTip(6000)
        Catch ex As Exception
            ErrorLogService.Log("AppNotificationService.Show", ex)
        End Try
    End Sub

    Private Shared Function ResolveIcon(owner As Form) As Icon
        Try
            If owner IsNot Nothing AndAlso owner.Icon IsNot Nothing Then Return owner.Icon

            Dim iconPath = Path.Combine(Application.StartupPath, "Resources", "app_icon.ico")
            If File.Exists(iconPath) Then Return New Icon(iconPath)

            Dim extracted = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
            If extracted IsNot Nothing Then Return extracted
        Catch ex As Exception
            ErrorLogService.Log("AppNotificationService.ResolveIcon", ex)
        End Try

        Return SystemIcons.Application
    End Function

    Private Shared Sub NotifyIcon_Click(sender As Object, e As EventArgs)
        Try
            If ownerForm Is Nothing OrElse ownerForm.IsDisposed Then Return

            If ownerForm.InvokeRequired Then
                ownerForm.BeginInvoke(CType(Sub() ActivateOwnerForm(), MethodInvoker))
            Else
                ActivateOwnerForm()
            End If
        Catch ex As Exception
            ErrorLogService.Log("AppNotificationService.NotifyIcon_Click", ex)
        End Try
    End Sub

    Private Shared Sub ActivateOwnerForm()
        If ownerForm Is Nothing OrElse ownerForm.IsDisposed Then Return

        If Not ownerForm.Visible Then ownerForm.Show()
        If ownerForm.WindowState = FormWindowState.Minimized Then ownerForm.WindowState = FormWindowState.Normal
        ownerForm.Activate()
    End Sub

    Private Shared Function LimitText(value As String, maxLength As Integer) As String
        Dim text = If(value, "").Trim()
        If text.Length <= maxLength Then Return text
        Return text.Substring(0, Math.Max(0, maxLength - 1)).TrimEnd() & "…"
    End Function
End Class
