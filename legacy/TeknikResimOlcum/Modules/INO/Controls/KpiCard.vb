Imports System
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

Public Class KpiCard
    Inherits Panel

    Private ReadOnly headerPanel As Panel
    Private ReadOnly accentBar As Panel
    Private ReadOnly titleLabel As Label
    Private ReadOnly valueText As Label
    Private ReadOnly subLabel As Label
    Private ReadOnly accentColor As Color

    Public ReadOnly Property ValueLabel As Label
        Get
            Return valueText
        End Get
    End Property

    Public Sub New(title As String, accent As Color)
        Me.accentColor = accent

        Me.DoubleBuffered = True
        Me.BackColor = Color.White
        Me.Padding = New Padding(0)
        Me.Margin = New Padding(10)
        Me.BorderStyle = BorderStyle.None

        accentBar = New Panel()
        accentBar.Dock = DockStyle.Top
        accentBar.Height = 6
        accentBar.BackColor = accent
        Me.Controls.Add(accentBar)

        headerPanel = New Panel()
        headerPanel.Dock = DockStyle.Top
        headerPanel.Height = 32
        headerPanel.BackColor = Color.White
        headerPanel.Padding = New Padding(12, 6, 12, 0)
        Me.Controls.Add(headerPanel)

        titleLabel = New Label()
        titleLabel.Text = title
        titleLabel.Dock = DockStyle.Fill
        titleLabel.TextAlign = ContentAlignment.MiddleLeft
        titleLabel.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        titleLabel.ForeColor = accent
        headerPanel.Controls.Add(titleLabel)

        valueText = New Label()
        valueText.Text = "0"
        valueText.Dock = DockStyle.Fill
        valueText.TextAlign = ContentAlignment.BottomCenter
        valueText.Font = New Font("Segoe UI", 22.0F, FontStyle.Bold)
        valueText.ForeColor = Color.FromArgb(18, 38, 70)
        valueText.Padding = New Padding(0, 0, 0, 6)
        Me.Controls.Add(valueText)

        subLabel = New Label()
        subLabel.Text = "adet"
        subLabel.Dock = DockStyle.Bottom
        subLabel.Height = 20
        subLabel.TextAlign = ContentAlignment.TopCenter
        subLabel.Font = New Font("Segoe UI", 8.0F, FontStyle.Regular)
        subLabel.ForeColor = Color.FromArgb(102, 112, 133)
        subLabel.Padding = New Padding(0, 0, 0, 8)
        Me.Controls.Add(subLabel)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias

        Dim shadowRect As New Rectangle(3, 4, Me.Width - 7, Me.Height - 7)
        Using shadowPath = CreateRoundRectPath(shadowRect, 16),
              shadowBrush As New SolidBrush(Color.FromArgb(18, 16, 24, 40))
            e.Graphics.FillPath(shadowBrush, shadowPath)
        End Using

        Dim rect As New Rectangle(0, 0, Me.Width - 7, Me.Height - 7)
        Using path = CreateRoundRectPath(rect, 16),
              bgBrush As New SolidBrush(Color.White),
              borderPen As New Pen(Color.FromArgb(220, 226, 234))
            e.Graphics.FillPath(bgBrush, path)
            e.Graphics.DrawPath(borderPen, path)
        End Using
    End Sub

    Protected Overrides Sub OnResize(eventargs As EventArgs)
        MyBase.OnResize(eventargs)
        Me.Invalidate()
    End Sub

    Private Function CreateRoundRectPath(r As Rectangle, radius As Integer) As GraphicsPath
        Dim path As New GraphicsPath()
        Dim d As Integer = radius * 2

        path.AddArc(r.X, r.Y, d, d, 180, 90)
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90)
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
        path.CloseFigure()

        Return path
    End Function
End Class
