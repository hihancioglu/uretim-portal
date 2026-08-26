Imports System.Drawing
Imports System.Runtime.InteropServices
Imports System.Windows.Forms

Public NotInheritable Class AppIconService
    Private Sub New()
    End Sub

    Private Const WM_SETICON As Integer = &H80
    Private Const ICON_SMALL As Integer = 0
    Private Const ICON_BIG As Integer = 1

    Private Shared cachedIcon As Icon = Nothing
    Private Shared cachedSmallIcon As Icon = Nothing
    Private Shared cachedLargeIcon As Icon = Nothing

    Public Shared Sub Apply(form As Form)
        If form Is Nothing Then Return

        ResponsiveFormService.Apply(form)

        Try
            EnsureIcons()

            If cachedIcon IsNot Nothing Then
                form.Icon = cachedIcon
                ApplyWindowIconHandles(form)
                AddHandler form.HandleCreated, AddressOf Form_HandleCreated
            End If
        Catch ex As Exception
            ErrorLogService.Log("AppIconService.Apply", ex)
        End Try
    End Sub

    Private Shared Sub EnsureIcons()
        If cachedIcon IsNot Nothing Then Return

        Dim iconPath = IO.Path.Combine(Application.StartupPath, "Resources", "app_icon.ico")

        If IO.File.Exists(iconPath) Then
            cachedIcon = New Icon(iconPath)
            cachedSmallIcon = New Icon(iconPath, SystemInformation.SmallIconSize)
            cachedLargeIcon = New Icon(iconPath, SystemInformation.IconSize)
        Else
            Dim extracted = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
            If extracted IsNot Nothing Then
                cachedIcon = extracted
                cachedSmallIcon = New Icon(extracted, SystemInformation.SmallIconSize)
                cachedLargeIcon = New Icon(extracted, SystemInformation.IconSize)
            End If
        End If
    End Sub

    Private Shared Sub Form_HandleCreated(sender As Object, e As EventArgs)
        Try
            Dim form = TryCast(sender, Form)
            ApplyWindowIconHandles(form)
        Catch ex As Exception
            ErrorLogService.Log("AppIconService.Form_HandleCreated", ex)
        End Try
    End Sub

    Private Shared Sub ApplyWindowIconHandles(form As Form)
        If form Is Nothing OrElse Not form.IsHandleCreated Then Return

        If cachedSmallIcon IsNot Nothing Then
            SendMessage(form.Handle, WM_SETICON, New IntPtr(ICON_SMALL), cachedSmallIcon.Handle)
        End If

        If cachedLargeIcon IsNot Nothing Then
            SendMessage(form.Handle, WM_SETICON, New IntPtr(ICON_BIG), cachedLargeIcon.Handle)
        End If
    End Sub

    <DllImport("user32.dll", SetLastError:=False)>
    Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function
End Class
