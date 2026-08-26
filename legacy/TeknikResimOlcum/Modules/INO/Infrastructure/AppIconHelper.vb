Imports System
Imports System.Drawing
Imports System.IO
Imports System.Runtime.CompilerServices
Imports System.Windows.Forms

Public Module AppIconHelper
    Private cachedIcon As Icon = Nothing
    Private ReadOnly buttonBaselines As New ConditionalWeakTable(Of Button, ButtonLayoutBaseline)()

    Public Function GetAppIcon() As Icon
        If cachedIcon IsNot Nothing Then
            Return cachedIcon
        End If

        Try
            Dim iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon_INO.ico")
            If File.Exists(iconPath) Then
                Using fs As New FileStream(iconPath, FileMode.Open, FileAccess.Read)
                    cachedIcon = New Icon(fs)
                End Using
                Return cachedIcon
            End If
        Catch
        End Try

        Try
            cachedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
        Catch
            cachedIcon = SystemIcons.Application
        End Try

        Return cachedIcon
    End Function

    Public Sub ApplyIcon(targetForm As Form)
        If targetForm Is Nothing Then Return

        ConfigureResponsiveForm(targetForm)

        Try
            targetForm.Icon = GetAppIcon()
        Catch
        End Try
    End Sub

    Private Sub ConfigureResponsiveForm(targetForm As Form)
        Try
            targetForm.AutoScaleMode = AutoScaleMode.Dpi
            targetForm.AutoScroll = True
            AddHandler targetForm.Load, Sub() FitFormToScreen(targetForm)
            AddHandler targetForm.Shown, Sub() FitFormToScreen(targetForm)
            AddHandler targetForm.Resize, Sub() ConfigureResponsiveChildren(targetForm)
            AddHandler targetForm.DpiChanged, Sub() FitFormToScreen(targetForm)
        Catch
        End Try
    End Sub

    Private Sub FitFormToScreen(targetForm As Form)
        If targetForm Is Nothing OrElse targetForm.IsDisposed Then Return

        Try
            Dim area = Screen.FromControl(targetForm).WorkingArea
            If area.Width <= 0 OrElse area.Height <= 0 Then Return

            Dim currentMin = targetForm.MinimumSize
            Dim maxMinWidth = Math.Max(360, area.Width - 32)
            Dim maxMinHeight = Math.Max(260, area.Height - 56)
            targetForm.MinimumSize = New Size(
                If(currentMin.Width <= 0, 0, Math.Min(currentMin.Width, maxMinWidth)),
                If(currentMin.Height <= 0, 0, Math.Min(currentMin.Height, maxMinHeight)))

            If targetForm.WindowState = FormWindowState.Normal Then
                Dim targetWidth = Math.Min(targetForm.Width, Math.Max(360, area.Width - 24))
                Dim targetHeight = Math.Min(targetForm.Height, Math.Max(260, area.Height - 48))
                targetForm.Size = New Size(targetWidth, targetHeight)

                Dim x = Math.Max(area.Left, Math.Min(targetForm.Left, area.Right - targetForm.Width))
                Dim y = Math.Max(area.Top, Math.Min(targetForm.Top, area.Bottom - targetForm.Height))
                targetForm.Location = New Point(x, y)
            End If

            ConfigureResponsiveChildren(targetForm)
        Catch
        End Try
    End Sub

    Private Sub ConfigureResponsiveChildren(parent As Control)
        If parent Is Nothing OrElse parent.IsDisposed Then Return

        Dim form = TryCast(parent, Form)
        If form Is Nothing Then form = parent.FindForm()
        If form Is Nothing Then Return

        Dim dpi = Math.Max(96, form.DeviceDpi)
        Dim logicalWidth = form.ClientSize.Width * 96.0R / dpi
        Dim logicalHeight = form.ClientSize.Height * 96.0R / dpi
        Dim workingArea = Screen.FromControl(form).WorkingArea
        Dim logicalWorkingHeight = workingArea.Height * 96.0R / dpi
        Dim scale = If(logicalWidth < 900, 0.82R, If(logicalWidth < 1450, 0.92R, 1.0R))
        If logicalWorkingHeight > 0 AndAlso logicalWorkingHeight < 900 Then scale *= 0.96R
        If logicalWorkingHeight > 0 AndAlso logicalWorkingHeight < 820 Then scale *= 0.94R
        If logicalWorkingHeight > 0 AndAlso logicalWorkingHeight < 720 Then scale *= 0.92R
        If logicalHeight > 0 AndAlso logicalHeight < 650 Then scale *= 0.94R
        scale = Math.Max(0.74R, Math.Min(1.0R, scale))

        ConfigureResponsiveChildrenCore(parent, scale, dpi)
    End Sub

    Private Sub ConfigureResponsiveChildrenCore(parent As Control, scale As Double, dpi As Integer)
        If parent Is Nothing OrElse parent.IsDisposed Then Return

        For Each child As Control In parent.Controls
            Dim flow = TryCast(child, FlowLayoutPanel)
            If flow IsNot Nothing AndAlso Not flow.WrapContents Then flow.AutoScroll = True

            Dim button = TryCast(child, Button)
            If button IsNot Nothing Then ApplyResponsiveButtonSize(button, scale, dpi)

            ConfigureResponsiveChildrenCore(child, scale, dpi)
        Next
    End Sub

    Private Sub ApplyResponsiveButtonSize(button As Button, scale As Double, dpi As Integer)
        If button Is Nothing OrElse button.IsDisposed Then Return

        button.AutoEllipsis = False

        Dim baseline = buttonBaselines.GetValue(button, Function(b) New ButtonLayoutBaseline(b, dpi))
        Dim dpiScale = dpi / 96.0R
        Dim textMinimum = MeasureButtonTextMinimum(button, dpi)

        If baseline.AutoSize Then
            button.Padding = New Padding(
                Math.Max(0, CInt(Math.Round(baseline.LogicalPadding.Left * scale * dpiScale))),
                Math.Max(0, CInt(Math.Round(baseline.LogicalPadding.Top * scale * dpiScale))),
                Math.Max(0, CInt(Math.Round(baseline.LogicalPadding.Right * scale * dpiScale))),
                Math.Max(0, CInt(Math.Round(baseline.LogicalPadding.Bottom * scale * dpiScale))))
            EnsureButtonTextMinimum(button, dpi)
            Return
        End If

        Dim shortCaption = If(button.Text, "").Trim().Length <= 2
        Dim minLogicalWidth = If(shortCaption, 30.0R, 68.0R)
        Dim targetWidth = CInt(Math.Round(Math.Max(minLogicalWidth, baseline.LogicalWidth * scale) * dpiScale))
        Dim targetHeight = CInt(Math.Round(Math.Max(32.0R, baseline.LogicalHeight * scale) * dpiScale))
        Dim targetMinWidth = Math.Max(0, CInt(Math.Round(baseline.LogicalMinimumWidth * scale * dpiScale)))
        Dim targetMinHeight = Math.Max(0, CInt(Math.Round(baseline.LogicalMinimumHeight * scale * dpiScale)))

        If textMinimum.Width > 0 Then
            targetWidth = Math.Max(targetWidth, textMinimum.Width)
            targetMinWidth = Math.Max(targetMinWidth, textMinimum.Width)
        End If

        If textMinimum.Height > 0 Then
            targetHeight = Math.Max(targetHeight, textMinimum.Height)
            targetMinHeight = Math.Max(targetMinHeight, textMinimum.Height)
        End If

        button.MinimumSize = New Size(targetMinWidth, targetMinHeight)

        Select Case button.Dock
            Case DockStyle.Fill
                Return
            Case DockStyle.Left, DockStyle.Right
                button.Width = targetWidth
            Case DockStyle.Top, DockStyle.Bottom
                button.Height = targetHeight
            Case Else
                button.Size = New Size(targetWidth, targetHeight)
        End Select
    End Sub

    Private Sub EnsureButtonTextMinimum(button As Button, dpi As Integer)
        Dim textMinimum = MeasureButtonTextMinimum(button, dpi)
        If textMinimum.Width <= 0 AndAlso textMinimum.Height <= 0 Then Return

        button.MinimumSize = New Size(
            Math.Max(button.MinimumSize.Width, textMinimum.Width),
            Math.Max(button.MinimumSize.Height, textMinimum.Height))
    End Sub

    Private Function MeasureButtonTextMinimum(button As Button, dpi As Integer) As Size
        Dim text = If(button.Text, "").Trim()
        If text = "" Then Return Size.Empty

        Dim dpiScale = Math.Max(96, dpi) / 96.0R
        Dim measured = TextRenderer.MeasureText(
            text,
            button.Font,
            New Size(2400, 600),
            TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine)

        Dim horizontalPadding = button.Padding.Left + button.Padding.Right + CInt(Math.Round(24 * dpiScale))
        Dim verticalPadding = button.Padding.Top + button.Padding.Bottom + CInt(Math.Round(12 * dpiScale))

        Return New Size(measured.Width + horizontalPadding, measured.Height + verticalPadding)
    End Function

    Private NotInheritable Class ButtonLayoutBaseline
        Public ReadOnly LogicalWidth As Double
        Public ReadOnly LogicalHeight As Double
        Public ReadOnly LogicalMinimumWidth As Double
        Public ReadOnly LogicalMinimumHeight As Double
        Public ReadOnly LogicalPadding As Padding
        Public ReadOnly AutoSize As Boolean

        Public Sub New(button As Button, dpi As Integer)
            Dim normalize = 96.0R / Math.Max(96, dpi)
            LogicalWidth = button.Width * normalize
            LogicalHeight = button.Height * normalize
            LogicalMinimumWidth = button.MinimumSize.Width * normalize
            LogicalMinimumHeight = button.MinimumSize.Height * normalize
            LogicalPadding = New Padding(
                CInt(Math.Round(button.Padding.Left * normalize)),
                CInt(Math.Round(button.Padding.Top * normalize)),
                CInt(Math.Round(button.Padding.Right * normalize)),
                CInt(Math.Round(button.Padding.Bottom * normalize)))
            AutoSize = button.AutoSize
        End Sub
    End Class
End Module
