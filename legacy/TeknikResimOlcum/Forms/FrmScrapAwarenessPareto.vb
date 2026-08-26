Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Globalization
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmScrapAwarenessPareto
    Inherits Form

    Private ReadOnly summary As ScrapAwarenessSummary
    Private ReadOnly paretoItems As List(Of ParetoItem)
    Private ReadOnly grid As New DataGridView()
    Private ReadOnly chartPanel As New ParetoChartPanel()

    Public Sub New(summary As ScrapAwarenessSummary)
        If summary Is Nothing Then Throw New ArgumentNullException(NameOf(summary))
        Me.summary = summary
        paretoItems = BuildParetoItems(summary)

        AppIconService.Apply(Me)
        Text = "Üretim Öncesi Hurda Bilinçlendirme"
        StartPosition = FormStartPosition.CenterScreen
        Size = New Size(1180, 760)
        MinimumSize = New Size(900, 600)
        WindowState = FormWindowState.Maximized
        BackColor = Color.WhiteSmoke

        BuildScreen()
    End Sub

    Private Sub BuildScreen()
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .Padding = New Padding(10),
            .BackColor = Color.WhiteSmoke
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 48.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 52.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 52.0F))
        Controls.Add(root)

        root.Controls.Add(BuildHeader(), 0, 0)
        root.Controls.Add(BuildChartPanel(), 0, 1)
        root.Controls.Add(BuildGridPanel(), 0, 2)
        root.Controls.Add(BuildBottomPanel(), 0, 3)
    End Sub

    Private Function BuildHeader() As Control
        Dim panel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 4,
            .RowCount = 1,
            .BackColor = Color.FromArgb(31, 78, 132),
            .Padding = New Padding(18, 8, 18, 8)
        }
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 300.0F))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 270.0F))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 320.0F))
        panel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim title As New Label() With {
            .Text = "Ürün Bazında Hurda Pareto",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 14.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .AutoEllipsis = True,
            .Margin = New Padding(0, 0, 16, 0)
        }
        panel.Controls.Add(title, 0, 0)

        Dim productText As New Label() With {
            .Text = "Ürün: " & BuildProductText(),
            .Dock = DockStyle.Fill,
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 9.5F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .AutoEllipsis = True,
            .Margin = New Padding(0, 0, 16, 0)
        }
        panel.Controls.Add(productText, 1, 0)

        Dim metaText As New Label() With {
            .Text = "Eşleşen hurda: " & summary.MatchedRows.ToString(CultureInfo.InvariantCulture) & " kayıt / " & FormatQuantity(summary.MatchedQuantity) & " adet" &
                    "   |   Kaynak: " & If(String.IsNullOrWhiteSpace(summary.SourceFileName), "-", summary.SourceFileName),
            .Dock = DockStyle.Fill,
            .ForeColor = Color.FromArgb(230, 238, 250),
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Regular),
            .TextAlign = ContentAlignment.MiddleLeft,
            .AutoEllipsis = True,
            .Margin = New Padding(0)
        }
        panel.Controls.Add(metaText, 2, 0)
        panel.SetColumnSpan(metaText, 2)

        Return panel
    End Function

    Private Function BuildInfoPanel() As Control
        Dim panel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 3,
            .RowCount = 1,
            .BackColor = Color.White,
            .Padding = New Padding(10, 6, 10, 6)
        }
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 38.0F))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 34.0F))
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 28.0F))

        panel.Controls.Add(MakeInfoLabel("TR / Ürün", BuildProductText()), 0, 0)
        panel.Controls.Add(MakeInfoLabel("Eşleşen Hurda", summary.MatchedRows.ToString(CultureInfo.InvariantCulture) & " kayıt / " & FormatQuantity(summary.MatchedQuantity)), 1, 0)
        panel.Controls.Add(MakeInfoLabel("Kaynak", If(String.IsNullOrWhiteSpace(summary.SourceFileName), "-", summary.SourceFileName)), 2, 0)

        Return panel
    End Function

    Private Function BuildChartPanel() As Control
        Dim panel As New Panel() With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.White,
            .Padding = New Padding(8),
            .Margin = New Padding(0, 8, 0, 6)
        }
        panel.BorderStyle = BorderStyle.FixedSingle
        chartPanel.Dock = DockStyle.Fill
        chartPanel.Items = paretoItems
        panel.Controls.Add(chartPanel)
        Return panel
    End Function

    Private Function BuildGridPanel() As Control
        Dim panel As New Panel() With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.White,
            .Padding = New Padding(0),
            .Margin = New Padding(0, 0, 0, 6)
        }
        panel.BorderStyle = BorderStyle.FixedSingle

        ConfigureGrid()
        panel.Controls.Add(grid)

        Return panel
    End Function

    Private Function BuildBottomPanel() As Control
        Dim panel As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False,
            .Padding = New Padding(0, 8, 0, 0),
            .BackColor = Color.WhiteSmoke
        }

        Dim closeButton As New Button() With {
            .Text = "Tamam",
            .Width = 120,
            .Height = 34,
            .DialogResult = DialogResult.OK
        }
        closeButton.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        panel.Controls.Add(closeButton)

        AcceptButton = closeButton
        CancelButton = closeButton

        Return panel
    End Function

    Private Function MakeInfoLabel(caption As String, value As String) As Control
        Dim panel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .Padding = New Padding(8, 0, 8, 0),
            .BackColor = Color.White
        }
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 16.0F))
        panel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        panel.Controls.Add(New Label() With {
            .Text = caption,
            .Dock = DockStyle.Fill,
            .ForeColor = Color.FromArgb(90, 99, 110),
            .Font = New Font("Segoe UI", 8.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.BottomLeft
        }, 0, 0)

        panel.Controls.Add(New Label() With {
            .Text = If(String.IsNullOrWhiteSpace(value), "-", value),
            .Dock = DockStyle.Fill,
            .ForeColor = Color.FromArgb(15, 35, 70),
            .Font = New Font("Segoe UI", 9.5F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .AutoEllipsis = True
        }, 0, 1)

        Return panel
    End Function

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.ReadOnly = True
        grid.RowHeadersVisible = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.MultiSelect = False
        grid.AutoGenerateColumns = False
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.None
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(219, 230, 244)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(10, 35, 70)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.DefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(31, 120, 210)
        grid.DefaultCellStyle.SelectionForeColor = Color.White
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 251, 255)
        grid.RowTemplate.Height = 27

        grid.Columns.Clear()
        grid.Columns.Add(MakeColumn("Rank", "Sıra", 55, DataGridViewAutoSizeColumnMode.None))
        grid.Columns.Add(MakeColumn("Reason", "Hurda Sebebi", 160, DataGridViewAutoSizeColumnMode.None))
        grid.Columns.Add(MakeColumn("Description", "Hurda Sebebi Tanımı", 300, DataGridViewAutoSizeColumnMode.Fill))
        grid.Columns.Add(MakeColumn("CountText", "Kayıt", 80, DataGridViewAutoSizeColumnMode.None))
        grid.Columns.Add(MakeColumn("QuantityText", "Adet", 110, DataGridViewAutoSizeColumnMode.None))
        grid.Columns.Add(MakeColumn("PercentText", "Pay", 85, DataGridViewAutoSizeColumnMode.None))
        grid.Columns.Add(MakeColumn("CumulativeText", "Kümülatif", 95, DataGridViewAutoSizeColumnMode.None))

        grid.DataSource = paretoItems.Select(Function(item) New With {
            .Rank = item.Rank.ToString(CultureInfo.InvariantCulture),
            .Reason = item.Reason,
            .Description = If(String.IsNullOrWhiteSpace(item.Description), "-", item.Description),
            .CountText = item.Count.ToString(CultureInfo.InvariantCulture),
            .QuantityText = FormatQuantity(item.Quantity),
            .PercentText = item.Percent.ToString("N1", CultureInfo.GetCultureInfo("tr-TR")) & " %",
            .CumulativeText = item.CumulativePercent.ToString("N1", CultureInfo.GetCultureInfo("tr-TR")) & " %"
        }).ToList()
    End Sub

    Private Function MakeColumn(name As String, header As String, width As Integer, mode As DataGridViewAutoSizeColumnMode) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .DataPropertyName = name,
            .HeaderText = header,
            .Width = width,
            .MinimumWidth = Math.Min(width, 70),
            .AutoSizeMode = mode,
            .SortMode = DataGridViewColumnSortMode.NotSortable
        }
    End Function

    Private Function BuildProductText() As String
        Dim parts As New List(Of String)()
        If Not String.IsNullOrWhiteSpace(summary.TrCode) Then parts.Add(summary.TrCode.Trim())
        If Not String.IsNullOrWhiteSpace(summary.ProductName) Then parts.Add(summary.ProductName.Trim())
        If parts.Count = 0 Then Return "-"
        Return String.Join(" | ", parts)
    End Function

    Private Shared Function BuildParetoItems(summary As ScrapAwarenessSummary) As List(Of ParetoItem)
        Dim reasons = If(summary.Reasons, New List(Of ScrapAwarenessReason)()).
            OrderByDescending(Function(reason) reason.Quantity).
            ThenByDescending(Function(reason) reason.Count).
            ThenBy(Function(reason) reason.Reason, StringComparer.OrdinalIgnoreCase).
            ToList()

        Dim totalQuantity = reasons.Sum(Function(reason) reason.Quantity)
        If totalQuantity <= 0D Then totalQuantity = 1D
        Dim cumulative As Double = 0
        Dim rank As Integer = 1
        Dim result As New List(Of ParetoItem)()

        For Each reason In reasons
            Dim percent = CDbl(reason.Quantity) * 100.0R / CDbl(totalQuantity)
            cumulative += percent
            result.Add(New ParetoItem With {
                .Rank = rank,
                .Reason = If(String.IsNullOrWhiteSpace(reason.Reason), "(Sebep yazılmamış)", reason.Reason.Trim()),
                .Description = If(String.IsNullOrWhiteSpace(reason.Description), "", reason.Description.Trim()),
                .Count = reason.Count,
                .Quantity = reason.Quantity,
                .Percent = percent,
                .CumulativePercent = Math.Min(100.0R, cumulative)
            })
            rank += 1
        Next

        Return result
    End Function

    Private Shared Function FormatQuantity(value As Decimal) As String
        If value <= 0D Then Return "-"
        If Decimal.Truncate(value) = value Then
            Return value.ToString("N0", CultureInfo.GetCultureInfo("tr-TR"))
        End If

        Return value.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"))
    End Function

    Private NotInheritable Class ParetoItem
        Public Property Rank As Integer
        Public Property Reason As String = ""
        Public Property Description As String = ""
        Public Property Count As Integer
        Public Property Quantity As Decimal
        Public Property Percent As Double
        Public Property CumulativePercent As Double

        Public ReadOnly Property DisplayText As String
            Get
                If String.IsNullOrWhiteSpace(Description) OrElse
                   String.Equals(Reason, Description, StringComparison.OrdinalIgnoreCase) Then
                    Return Reason
                End If

                Return Reason & " - " & Description
            End Get
        End Property
    End Class

    Private NotInheritable Class ParetoChartPanel
        Inherits Panel

        Public Property Items As List(Of ParetoItem) = New List(Of ParetoItem)()

        Public Sub New()
            DoubleBuffered = True
            ResizeRedraw = True
            BackColor = Color.White
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
            e.Graphics.Clear(Color.White)

            Dim allItems = If(Items, New List(Of ParetoItem)())
            If allItems.Count = 0 Then
                DrawCenteredText(e.Graphics, "Bu ürün için Pareto oluşturulacak hurda kaydı bulunamadı.")
                Return
            End If

            Dim bounds = ClientRectangle
            If bounds.Width < 420 OrElse bounds.Height < 120 Then
                DrawCenteredText(e.Graphics, "Grafik alanı dar. Pencereyi büyütün veya tam ekran kullanın.")
                Return
            End If

            Dim top As Integer = 58
            Dim bottom As Integer = 16
            Dim availableRowsHeight As Integer = Math.Max(1, bounds.Height - top - bottom)
            Dim maxRowsByHeight As Integer = Math.Max(3, Math.Min(10, CInt(Math.Floor(availableRowsHeight / 26.0R))))
            Dim data = allItems.Take(maxRowsByHeight).ToList()

            Using titleFont As New Font("Segoe UI", 10.0F, FontStyle.Bold),
                  labelFont As New Font("Segoe UI", 8.5F, FontStyle.Regular),
                  valueFont As New Font("Segoe UI", 8.5F, FontStyle.Bold),
                  axisPen As New Pen(Color.FromArgb(205, 214, 226), 1.0F),
                  rowPen As New Pen(Color.FromArgb(235, 240, 247), 1.0F),
                  pillPen As New Pen(Color.FromArgb(247, 203, 145), 1.0F),
                  barBrush As New SolidBrush(Color.FromArgb(47, 116, 181)),
                  barBrushTop As New SolidBrush(Color.FromArgb(31, 78, 132)),
                  cumulativeBrush As New SolidBrush(Color.FromArgb(229, 126, 31)),
                  cumulativeBackBrush As New SolidBrush(Color.FromArgb(255, 243, 226)),
                  textBrush As New SolidBrush(Color.FromArgb(22, 45, 80)),
                  mutedBrush As New SolidBrush(Color.FromArgb(95, 105, 120)),
                  labelFormat As New StringFormat(),
                  valueFormat As New StringFormat()

                labelFormat.FormatFlags = StringFormatFlags.NoWrap
                labelFormat.Trimming = StringTrimming.EllipsisCharacter
                labelFormat.LineAlignment = StringAlignment.Center
                labelFormat.Alignment = StringAlignment.Near

                valueFormat.FormatFlags = StringFormatFlags.NoWrap
                valueFormat.Trimming = StringTrimming.EllipsisCharacter
                valueFormat.LineAlignment = StringAlignment.Center
                valueFormat.Alignment = StringAlignment.Center

                e.Graphics.DrawString("Ürün Bazında Pareto Grafiği", titleFont, textBrush, New PointF(12.0F, 10.0F))
                e.Graphics.DrawString("Bar: hurda adet miktarı  |  Turuncu rozet: kümülatif adet yüzdesi  |  Detaylar tabloda", labelFont, mutedBrush, New PointF(12.0F, 32.0F))

                Dim leftLabelWidth As Integer = Math.Min(380, Math.Max(250, CInt(bounds.Width * 0.28R)))
                Dim valueWidth As Integer = 118
                Dim rightInfoWidth As Integer = 132
                Dim labelLeft As Integer = 16
                Dim barLeft As Integer = labelLeft + leftLabelWidth + 18
                Dim barRight As Integer = bounds.Width - rightInfoWidth - 24
                Dim barWidthMax As Integer = Math.Max(80, barRight - barLeft - valueWidth - 10)
                Dim valueLeft As Integer = barLeft + barWidthMax + 8
                Dim rowHeight As Single = CSng((bounds.Height - top - bottom) / Math.Max(1, data.Count))
                Dim barHeight As Single = Math.Min(22.0F, Math.Max(12.0F, rowHeight * 0.48F))
                Dim maxQuantity As Decimal = data.Max(Function(item) item.Quantity)
                If maxQuantity <= 0D Then maxQuantity = 1D

                For i As Integer = 0 To data.Count - 1
                    Dim item = data(i)
                    Dim rowTop = top + rowHeight * i
                    Dim centerY = rowTop + rowHeight / 2.0F
                    Dim reasonLabel = item.Rank.ToString(CultureInfo.InvariantCulture) & ". " & item.DisplayText
                    Dim rowRect As New RectangleF(8.0F, rowTop + 1.0F, bounds.Width - 16.0F, Math.Max(10.0F, rowHeight - 2.0F))
                    If i Mod 2 = 1 Then
                        Using altBrush As New SolidBrush(Color.FromArgb(249, 251, 254))
                            e.Graphics.FillRectangle(altBrush, rowRect)
                        End Using
                    End If
                    e.Graphics.DrawLine(rowPen, 8.0F, rowTop + rowHeight, bounds.Width - 8.0F, rowTop + rowHeight)

                    e.Graphics.DrawString(reasonLabel, labelFont, textBrush, New RectangleF(labelLeft, rowTop, leftLabelWidth, rowHeight), labelFormat)

                    Dim barWidth = CSng(CDec(barWidthMax) * item.Quantity / maxQuantity)
                    Dim barRect As New RectangleF(barLeft, centerY - barHeight / 2.0F, Math.Max(3.0F, barWidth), barHeight)
                    e.Graphics.FillRectangle(If(i = 0, barBrushTop, barBrush), barRect)
                    e.Graphics.DrawRectangle(axisPen, Rectangle.Round(barRect))

                    Dim quantityText = FormatQuantity(item.Quantity) & " adet"
                    e.Graphics.DrawString(quantityText, valueFont, textBrush, New RectangleF(valueLeft, rowTop, valueWidth, rowHeight), labelFormat)

                    Dim cumulativeText = item.CumulativePercent.ToString("N1", CultureInfo.GetCultureInfo("tr-TR")) & " %"
                    Dim pillRect As New RectangleF(bounds.Width - rightInfoWidth + 16.0F, centerY - 12.0F, rightInfoWidth - 28.0F, 24.0F)
                    e.Graphics.FillRectangle(cumulativeBackBrush, pillRect)
                    e.Graphics.DrawRectangle(pillPen, Rectangle.Round(pillRect))
                    e.Graphics.DrawString(cumulativeText, valueFont, cumulativeBrush, pillRect, valueFormat)
                Next
            End Using
        End Sub

        Private Sub DrawCenteredText(graphics As Graphics, text As String)
            Using font As New Font("Segoe UI", 10.0F, FontStyle.Bold),
                  brush As New SolidBrush(Color.FromArgb(80, 90, 105))
                Dim size = graphics.MeasureString(text, font)
                graphics.DrawString(text, font, brush, CSng((Width - size.Width) / 2), CSng((Height - size.Height) / 2))
            End Using
        End Sub

        Private Shared Function TruncateText(value As String, maxLength As Integer) As String
            If String.IsNullOrWhiteSpace(value) OrElse value.Length <= maxLength Then Return If(value, "")
            Return value.Substring(0, Math.Max(0, maxLength - 1)) & "…"
        End Function
    End Class
End Class
