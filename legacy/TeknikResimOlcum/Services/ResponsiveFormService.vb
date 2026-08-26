Imports System.Drawing
Imports System.Runtime.CompilerServices
Imports System.Windows.Forms

Public Enum ResponsiveLayoutProfile
    Compact
    Standard
    Wide
End Enum

Public NotInheritable Class ResponsiveFormService
    Private Sub New()
    End Sub

    Private Shared ReadOnly configuredForms As New HashSet(Of Form)()
    Private Shared ReadOnly buttonBaselines As New ConditionalWeakTable(Of Button, ButtonLayoutBaseline)()

    Public Shared Sub Apply(form As Form)
        If form Is Nothing Then Return

        SyncLock configuredForms
            If configuredForms.Contains(form) Then Return
            configuredForms.Add(form)
        End SyncLock

        Try
            form.AutoScaleMode = AutoScaleMode.Dpi
            form.AutoScroll = True

            AddHandler form.Load, AddressOf Form_Load
            AddHandler form.Shown, AddressOf Form_Shown
            AddHandler form.Resize, AddressOf Form_Resize
            AddHandler form.DpiChanged, AddressOf Form_DpiChanged
            AddHandler form.Disposed, AddressOf Form_Disposed
        Catch ex As Exception
            ErrorLogService.Log("ResponsiveFormService.Apply", ex)
        End Try
    End Sub

    Public Shared Sub FitSplitContainer(split As SplitContainer,
                                        ratio As Double,
                                        preferredPanel1Min As Integer,
                                        preferredPanel2Min As Integer)
        If split Is Nothing OrElse split.IsDisposed Then Return

        Try
            Dim width = split.ClientSize.Width
            Dim available = width - split.SplitterWidth
            If available < 54 Then Return

            Dim panel1Min = Math.Max(25, Math.Min(preferredPanel1Min, CInt(Math.Floor(available * 0.4R))))
            Dim panel2Min = Math.Max(25, Math.Min(preferredPanel2Min, CInt(Math.Floor(available * 0.4R))))

            If panel1Min + panel2Min >= available Then
                panel1Min = Math.Max(25, CInt(Math.Floor((available - 2) * 0.45R)))
                panel2Min = Math.Max(25, available - panel1Min - 2)
            End If

            Dim lower = panel1Min
            Dim upper = available - panel2Min
            If upper < lower Then Return

            Dim target = CInt(Math.Round(width * ratio))
            target = Math.Max(lower, Math.Min(target, upper))

            split.Panel1MinSize = 25
            split.Panel2MinSize = 25
            split.SplitterDistance = target
            split.Panel1MinSize = panel1Min
            split.Panel2MinSize = panel2Min
        Catch ex As Exception
            ErrorLogService.Log("ResponsiveFormService.FitSplitContainer", ex)
        End Try
    End Sub

    Public Shared Function GetLogicalClientWidth(control As Control) As Integer
        If control Is Nothing Then Return 0

        Dim dpi = Math.Max(96, control.DeviceDpi)
        Return CInt(Math.Round(control.ClientSize.Width * 96.0R / dpi))
    End Function

    Public Shared Function GetLogicalClientHeight(control As Control) As Integer
        If control Is Nothing Then Return 0

        Dim dpi = Math.Max(96, control.DeviceDpi)
        Return CInt(Math.Round(control.ClientSize.Height * 96.0R / dpi))
    End Function

    Public Shared Function GetLogicalWorkingAreaWidth(control As Control) As Integer
        If control Is Nothing Then Return 0

        Dim area = Screen.FromControl(control).WorkingArea
        Dim dpi = Math.Max(96, control.DeviceDpi)
        Return CInt(Math.Round(area.Width * 96.0R / dpi))
    End Function

    Public Shared Function GetLogicalWorkingAreaHeight(control As Control) As Integer
        If control Is Nothing Then Return 0

        Dim area = Screen.FromControl(control).WorkingArea
        Dim dpi = Math.Max(96, control.DeviceDpi)
        Return CInt(Math.Round(area.Height * 96.0R / dpi))
    End Function

    Public Shared Function GetLayoutProfile(control As Control) As ResponsiveLayoutProfile
        Dim logicalWidth = GetLogicalClientWidth(control)
        If logicalWidth <= 0 Then logicalWidth = GetLogicalWorkingAreaWidth(control)

        If logicalWidth < 900 Then Return ResponsiveLayoutProfile.Compact
        If logicalWidth < 1450 Then Return ResponsiveLayoutProfile.Standard
        Return ResponsiveLayoutProfile.Wide
    End Function

    Private Shared Sub Form_Load(sender As Object, e As EventArgs)
        ApplyScreenFit(TryCast(sender, Form))
    End Sub

    Private Shared Sub Form_Shown(sender As Object, e As EventArgs)
        ApplyScreenFit(TryCast(sender, Form))
    End Sub

    Private Shared Sub Form_Resize(sender As Object, e As EventArgs)
        ConfigureResponsiveChildren(TryCast(sender, Form))
    End Sub

    Private Shared Sub Form_DpiChanged(sender As Object, e As DpiChangedEventArgs)
        Dim form = TryCast(sender, Form)
        ApplyScreenFit(form)

        If form Is Nothing OrElse form.IsDisposed OrElse Not form.IsHandleCreated Then Return

        Try
            form.BeginInvoke(New MethodInvoker(Sub() ApplyScreenFit(form)))
        Catch ex As Exception
            ErrorLogService.Log("ResponsiveFormService.Form_DpiChanged.Deferred", ex)
        End Try
    End Sub

    Private Shared Sub Form_Disposed(sender As Object, e As EventArgs)
        Dim form = TryCast(sender, Form)
        If form Is Nothing Then Return

        SyncLock configuredForms
            configuredForms.Remove(form)
        End SyncLock
    End Sub

    Private Shared Sub ApplyScreenFit(form As Form)
        If form Is Nothing OrElse form.IsDisposed Then Return

        Try
            Dim area = Screen.FromControl(form).WorkingArea
            If area.Width <= 0 OrElse area.Height <= 0 Then Return

            FitMinimumSize(form, area)

            If form.WindowState = FormWindowState.Normal Then
                FitNormalWindowSize(form, area)
                KeepWindowInsideScreen(form, area)
            End If

            ConfigureResponsiveChildren(form)
        Catch ex As Exception
            ErrorLogService.Log("ResponsiveFormService.ApplyScreenFit", ex)
        End Try
    End Sub

    Private Shared Sub FitMinimumSize(form As Form, area As Rectangle)
        Dim maxMinWidth = Math.Max(320, area.Width - 40)
        Dim maxMinHeight = Math.Max(240, area.Height - 70)

        Dim currentMin = form.MinimumSize
        If currentMin.Width <= 0 AndAlso currentMin.Height <= 0 Then Return

        Dim newMinWidth = If(currentMin.Width <= 0, 0, Math.Min(currentMin.Width, maxMinWidth))
        Dim newMinHeight = If(currentMin.Height <= 0, 0, Math.Min(currentMin.Height, maxMinHeight))

        If newMinWidth <> currentMin.Width OrElse newMinHeight <> currentMin.Height Then
            form.MinimumSize = New Size(newMinWidth, newMinHeight)
        End If
    End Sub

    Private Shared Sub FitNormalWindowSize(form As Form, area As Rectangle)
        Dim maxWidth = Math.Max(360, area.Width - 24)
        Dim maxHeight = Math.Max(260, area.Height - 48)

        Dim newWidth = Math.Min(form.Width, maxWidth)
        Dim newHeight = Math.Min(form.Height, maxHeight)

        If newWidth <> form.Width OrElse newHeight <> form.Height Then
            form.Size = New Size(newWidth, newHeight)
        End If
    End Sub

    Private Shared Sub KeepWindowInsideScreen(form As Form, area As Rectangle)
        Dim x = form.Left
        Dim y = form.Top

        If form.Right > area.Right Then x = area.Right - form.Width
        If form.Bottom > area.Bottom Then y = area.Bottom - form.Height
        If x < area.Left Then x = area.Left
        If y < area.Top Then y = area.Top

        If x <> form.Left OrElse y <> form.Top Then form.Location = New Point(x, y)
    End Sub

    Private Shared Sub ConfigureResponsiveChildren(parent As Control)
        If parent Is Nothing OrElse parent.IsDisposed Then Return

        Dim ownerForm = TryCast(parent, Form)
        If ownerForm Is Nothing Then ownerForm = parent.FindForm()
        If ownerForm Is Nothing Then Return

        Dim dpi = Math.Max(96, ownerForm.DeviceDpi)
        Dim logicalWidth = GetLogicalClientWidth(ownerForm)
        If logicalWidth <= 0 Then logicalWidth = GetLogicalWorkingAreaWidth(ownerForm)

        Dim logicalHeight = GetLogicalClientHeight(ownerForm)
        If logicalHeight <= 0 Then logicalHeight = GetLogicalWorkingAreaHeight(ownerForm)

        Dim logicalWorkingHeight = GetLogicalWorkingAreaHeight(ownerForm)
        Dim scale As Double
        If logicalWidth < 900 Then
            scale = 0.82R
        ElseIf logicalWidth < 1450 Then
            scale = 0.92R
        Else
            scale = 1.0R
        End If

        If logicalWorkingHeight > 0 AndAlso logicalWorkingHeight < 900 Then scale *= 0.96R
        If logicalWorkingHeight > 0 AndAlso logicalWorkingHeight < 820 Then scale *= 0.94R
        If logicalWorkingHeight > 0 AndAlso logicalWorkingHeight < 720 Then scale *= 0.92R
        If logicalHeight > 0 AndAlso logicalHeight < 650 Then scale *= 0.94R
        scale = Math.Max(0.74R, Math.Min(1.0R, scale))

        ConfigureResponsiveChildrenCore(parent, scale, dpi)
    End Sub

    Private Shared Sub ConfigureResponsiveChildrenCore(parent As Control, scale As Double, dpi As Integer)
        If parent Is Nothing OrElse parent.IsDisposed Then Return

        For Each child As Control In parent.Controls
            Dim flow = TryCast(child, FlowLayoutPanel)
            If flow IsNot Nothing AndAlso Not flow.WrapContents Then
                flow.AutoScroll = True
            End If

            Dim button = TryCast(child, Button)
            If button IsNot Nothing Then ApplyResponsiveButtonSize(button, scale, dpi)

            ConfigureResponsiveChildrenCore(child, scale, dpi)
        Next
    End Sub

    Private Shared Sub ApplyResponsiveButtonSize(button As Button, scale As Double, dpi As Integer)
        If button Is Nothing OrElse button.IsDisposed Then Return

        button.AutoEllipsis = False

        If String.Equals(TryCast(button.Tag, String), "RESPONSIVE_NO_AUTO_SCALE", StringComparison.Ordinal) Then
            EnsureButtonTextMinimum(button, dpi)
            Return
        End If

        Dim baseline = buttonBaselines.GetValue(button, Function(b) New ButtonLayoutBaseline(b, dpi))
        Dim dpiScale = dpi / 96.0R
        Dim textMinimum = MeasureButtonTextMinimum(button, dpi)

        If baseline.AutoSize Then
            button.Padding = ScalePadding(baseline.LogicalPadding, scale * dpiScale)
            EnsureButtonTextMinimum(button, dpi)
            Return
        End If

        Dim shortCaption = If(button.Text, "").Trim().Length <= 2
        Dim minLogicalWidth = If(shortCaption, 30.0R, 68.0R)
        Dim minLogicalHeight = 32.0R
        Dim targetWidth = CInt(Math.Round(Math.Max(minLogicalWidth, baseline.LogicalWidth * scale) * dpiScale))
        Dim targetHeight = CInt(Math.Round(Math.Max(minLogicalHeight, baseline.LogicalHeight * scale) * dpiScale))
        Dim targetMinWidth = CInt(Math.Round(baseline.LogicalMinimumWidth * scale * dpiScale))
        Dim targetMinHeight = CInt(Math.Round(baseline.LogicalMinimumHeight * scale * dpiScale))

        If textMinimum.Width > 0 Then
            targetWidth = Math.Max(targetWidth, textMinimum.Width)
            targetMinWidth = Math.Max(targetMinWidth, textMinimum.Width)
        End If

        If textMinimum.Height > 0 Then
            targetHeight = Math.Max(targetHeight, textMinimum.Height)
            targetMinHeight = Math.Max(targetMinHeight, textMinimum.Height)
        End If

        button.MinimumSize = New Size(Math.Max(0, targetMinWidth), Math.Max(0, targetMinHeight))

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

    Private Shared Sub EnsureButtonTextMinimum(button As Button, dpi As Integer)
        Dim textMinimum = MeasureButtonTextMinimum(button, dpi)
        If textMinimum.Width <= 0 AndAlso textMinimum.Height <= 0 Then Return

        button.MinimumSize = New Size(
            Math.Max(button.MinimumSize.Width, textMinimum.Width),
            Math.Max(button.MinimumSize.Height, textMinimum.Height))
    End Sub

    Private Shared Function MeasureButtonTextMinimum(button As Button, dpi As Integer) As Size
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

    Private Shared Function ScalePadding(value As Padding, factor As Double) As Padding
        Return New Padding(
            Math.Max(0, CInt(Math.Round(value.Left * factor))),
            Math.Max(0, CInt(Math.Round(value.Top * factor))),
            Math.Max(0, CInt(Math.Round(value.Right * factor))),
            Math.Max(0, CInt(Math.Round(value.Bottom * factor))))
    End Function

    Private NotInheritable Class ButtonLayoutBaseline
        Public ReadOnly LogicalWidth As Double
        Public ReadOnly LogicalHeight As Double
        Public ReadOnly LogicalMinimumWidth As Double
        Public ReadOnly LogicalMinimumHeight As Double
        Public ReadOnly LogicalPadding As Padding
        Public ReadOnly AutoSize As Boolean

        Public Sub New(button As Button, dpi As Integer)
            Dim safeDpi = Math.Max(96, dpi)
            Dim normalize = 96.0R / safeDpi
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
End Class
