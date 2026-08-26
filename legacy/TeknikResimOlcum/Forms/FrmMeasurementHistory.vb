Imports System.Collections.Generic
Imports System.Data
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmMeasurementHistory
    Inherits Form

    Private NotInheritable Class HistoryRecordGroup
        Public Property Key As String = ""
        Public Property RecordId As String = ""
        Public Property Rows As New List(Of Dictionary(Of String, String))()
    End Class

    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly txtTr As New TextBox()
    Private ReadOnly txtLot As New TextBox()
    Private ReadOnly txtSerial As New TextBox()
    Private ReadOnly txtMeasure As New TextBox()
    Private ReadOnly txtOperator As New TextBox()
    Private ReadOnly cboResult As New ComboBox()
    Private ReadOnly cboVisualResult As New ComboBox()
    Private ReadOnly grid As New DataGridView()
    Private ReadOnly lblCount As New Label()
    Private historyLayout As TableLayoutPanel
    Private historyToolbar As FlowLayoutPanel
    Private ReadOnly groupRowFont As New Font("Segoe UI", 9.0F, FontStyle.Bold)
    Private ReadOnly trGroupRowFont As New Font("Segoe UI", 9.25F, FontStyle.Bold)
    Private ReadOnly expandedGroupKeys As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly expandedTrGroupKeys As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Private visibleGroups As New List(Of HistoryRecordGroup)()
    Private currentVisualByRecord As New Dictionary(Of String, List(Of Dictionary(Of String, String)))(StringComparer.OrdinalIgnoreCase)
    Private currentProductByKey As New Dictionary(Of String, ProductInfo)(StringComparer.OrdinalIgnoreCase)
    Private filteredDetailRowCount As Integer = 0
    Private totalDetailRowCount As Integer = 0
    Private totalGroupCount As Integer = 0
    Private todayMeasurementCount As Integer = 0
    Private isApplyingResponsiveLayout As Boolean = False
    Private requestedRecordId As String = ""
    Private requestedEyeNo As String = ""
    Private requestedMeasureId As String = ""
    Private requestedMeasurementDate As String = ""
    Private ReadOnly initialDrawingScope As String

    Public Sub New(Optional initialDrawingScope As String = "")
        AuthorizationService.Require(AppState.CanViewMeasurementHistory, "Olcum Gecmisi")
        AppIconService.Apply(Me)
        Me.initialDrawingScope = If(
            String.IsNullOrWhiteSpace(initialDrawingScope),
            "",
            ProductInfo.NormalizeDrawingScope(initialDrawingScope))
        Text = "Ölçüm Geçmişi"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1400, 780)
        MinimumSize = New Size(760, 520)
        AutoScroll = False

        historyLayout = New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .BackColor = Color.White
        }
        historyLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 64.0F))
        historyLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        historyLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(historyLayout)

        historyToolbar = New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(10, 8, 10, 4),
            .BackColor = SystemColors.Control,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = True,
            .AutoScroll = False
        }
        historyLayout.Controls.Add(historyToolbar, 0, 0)

        AddFilterBlock(historyToolbar, "Genel Arama", txtSearch, 180, "TR / ölçü / lot / seri / operatör")
        AddFilterBlock(historyToolbar, "TR Kodu", txtTr, 115, "TR")
        AddFilterBlock(historyToolbar, "İş Emri No", txtLot, 120, "İş Emri")
        AddFilterBlock(historyToolbar, "Seri No", txtSerial, 110, "Seri")
        AddFilterBlock(historyToolbar, "Ölçü No / Adı", txtMeasure, 150, "M001 / çap")
        AddFilterBlock(historyToolbar, "Ölçümü Yapan", txtOperator, 135, "Kullanıcı")

        Dim resultPanel As New Panel() With {.Width = 140, .Height = 42, .Margin = New Padding(0, 0, 10, 6)}
        resultPanel.Controls.Add(New Label() With {.Text = "Ölçüm Sonucu", .Left = 0, .Top = 0, .Width = 135, .Height = 16, .BackColor = Color.Transparent})
        cboResult.SetBounds(0, 17, 120, 24)
        cboResult.DropDownStyle = ComboBoxStyle.DropDownList
        cboResult.Items.AddRange({"TÜMÜ", "OK", "NOK", "HATALI", "GÖZ KAPALI"})
        cboResult.SelectedIndex = 0
        AddHandler cboResult.SelectedIndexChanged, Sub() LoadGrid()
        resultPanel.Controls.Add(cboResult)
        historyToolbar.Controls.Add(resultPanel)

        Dim visualPanel As New Panel() With {.Width = 150, .Height = 42, .Margin = New Padding(0, 0, 10, 6)}
        visualPanel.Controls.Add(New Label() With {.Text = "Görsel Sonuç", .Left = 0, .Top = 0, .Width = 145, .Height = 16, .BackColor = Color.Transparent})
        cboVisualResult.SetBounds(0, 17, 130, 24)
        cboVisualResult.DropDownStyle = ComboBoxStyle.DropDownList
        cboVisualResult.Items.AddRange({"TÜMÜ", "UYGUN", "UYGUN DEĞİL", "YOK", "GÖZ KAPALI"})
        cboVisualResult.SelectedIndex = 0
        AddHandler cboVisualResult.SelectedIndexChanged, Sub() LoadGrid()
        visualPanel.Controls.Add(cboVisualResult)
        historyToolbar.Controls.Add(visualPanel)

        Dim btnFilter As New Button() With {.Text = "Filtrele", .Width = 100, .Height = 30, .Margin = New Padding(0, 17, 8, 6)}
        AddHandler btnFilter.Click, Sub() LoadGrid()

        Dim btnClear As New Button() With {.Text = "Temizle", .Width = 100, .Height = 30, .Margin = New Padding(0, 17, 8, 6)}
        AddHandler btnClear.Click, AddressOf ClearFilters_Click

        historyToolbar.Controls.AddRange({btnFilter, btnClear})
        If AppState.IsAdmin Then
            Dim btnCorrect As New Button() With {
                .Text = "Seçili Ölçüyü Düzelt",
                .Width = 150,
                .Height = 30,
                .Margin = New Padding(0, 17, 8, 6),
                .BackColor = Color.FromArgb(232, 242, 255),
                .ForeColor = Color.FromArgb(31, 71, 126),
                .FlatStyle = FlatStyle.Flat
            }
            AddHandler btnCorrect.Click, AddressOf CorrectSelectedMeasurement_Click
            historyToolbar.Controls.Add(btnCorrect)

            Dim btnDelete As New Button() With {
                .Text = "Seçili Kaydı Sil",
                .Width = 130,
                .Height = 30,
                .Margin = New Padding(0, 17, 8, 6),
                .ForeColor = Color.DarkRed
            }
            AddHandler btnDelete.Click, AddressOf DeleteSelectedRecord_Click
            historyToolbar.Controls.Add(btnDelete)
        End If

        Dim summary As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(10, 4, 10, 4),
            .BackColor = Color.WhiteSmoke,
            .ColumnCount = 1,
            .RowCount = 1
        }
        summary.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        historyLayout.Controls.Add(summary, 0, 1)

        lblCount.Text = "Kayıt: 0 satır"
        lblCount.Dock = DockStyle.Fill
        lblCount.Font = New Font(Font.FontFamily, 9.0F, FontStyle.Bold)
        lblCount.TextAlign = ContentAlignment.MiddleLeft
        lblCount.AutoEllipsis = False
        summary.Controls.Add(lblCount, 0, 0)

        ConfigureGrid()
        historyLayout.Controls.Add(grid, 0, 2)

        AddHandler txtSearch.TextChanged, Sub() LoadGrid()
        AddHandler txtTr.TextChanged, Sub() LoadGrid()
        AddHandler txtLot.TextChanged, Sub() LoadGrid()
        AddHandler txtSerial.TextChanged, Sub() LoadGrid()
        AddHandler txtMeasure.TextChanged, Sub() LoadGrid()
        AddHandler txtOperator.TextChanged, Sub() LoadGrid()

        AddHandler Resize, Sub() ApplyResponsiveHistoryLayout()
        AddHandler DpiChanged,
            Sub()
                If IsHandleCreated AndAlso Not IsDisposed Then
                    BeginInvoke(New MethodInvoker(AddressOf ApplyResponsiveHistoryLayout))
                End If
            End Sub
        AddHandler Shown,
            Sub()
                ApplyResponsiveHistoryLayout()
                If requestedRecordId <> "" Then
                    txtSearch.Text = requestedRecordId
                    If txtSearch.Text = "" Then LoadGrid()
                    NavigateToRequestedMeasurement()
                Else
                    LoadGrid()
                End If
            End Sub
    End Sub

    Public Sub New(recordId As String, eyeNo As String, measureId As String, measurementDate As String)
        Me.New()
        requestedRecordId = If(recordId, "").Trim()
        requestedEyeNo = If(eyeNo, "").Trim()
        requestedMeasureId = If(measureId, "").Trim()
        requestedMeasurementDate = If(measurementDate, "").Trim()
    End Sub

    Private Sub NavigateToRequestedMeasurement()
        If requestedRecordId = "" Then Return

        Dim targetGroup = visibleGroups.FirstOrDefault(
            Function(group) group.Rows.Any(Function(row) IsRequestedMeasurementRow(row)))
        If targetGroup Is Nothing Then
            MessageBox.Show("İlgili ölçüm kaydı Ölçüm Geçmişi'nde bulunamadı.", "Ölçüm geçmişi", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        expandedTrGroupKeys.Add(MeasurementRecordGroupKey(targetGroup))
        expandedGroupKeys.Add(targetGroup.Key)
        BindGroupedGrid()

        For Each historyGridRow As DataGridViewRow In grid.Rows
            If Not String.Equals(GridRowType(historyGridRow), "DETAIL", StringComparison.OrdinalIgnoreCase) Then Continue For
            If Not IsRequestedMeasurementGridRow(historyGridRow) Then Continue For

            grid.ClearSelection()
            Dim focusColumn = If(grid.Columns.Contains("MeasuredValue"), "MeasuredValue", "RecordId")
            grid.CurrentCell = historyGridRow.Cells(focusColumn)
            historyGridRow.Selected = True
            Try
                grid.FirstDisplayedScrollingRowIndex = Math.Max(0, historyGridRow.Index - 2)
            Catch
            End Try
            Return
        Next

        MessageBox.Show("Ölçüm kaydı bulundu ancak detay satırı seçilemedi.", "Ölçüm geçmişi", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Function IsRequestedMeasurementRow(row As Dictionary(Of String, String)) As Boolean
        If row Is Nothing Then Return False
        If Not String.Equals(DataService.GetValue(row, "RecordId").Trim(), requestedRecordId, StringComparison.OrdinalIgnoreCase) Then Return False
        If requestedEyeNo <> "" AndAlso Not String.Equals(DataService.GetValue(row, "EyeNo").Trim(), requestedEyeNo, StringComparison.OrdinalIgnoreCase) Then Return False
        If requestedMeasureId <> "" AndAlso Not String.Equals(DataService.GetValue(row, "MeasureId").Trim(), requestedMeasureId, StringComparison.OrdinalIgnoreCase) Then Return False
        If requestedMeasurementDate <> "" AndAlso Not MeasurementDatesMatch(DataService.GetValue(row, "MeasurementDate"), requestedMeasurementDate) Then Return False
        Return True
    End Function

    Private Function IsRequestedMeasurementGridRow(row As DataGridViewRow) As Boolean
        If row Is Nothing Then Return False
        If Not String.Equals(Convert.ToString(row.Cells("RecordId").Value).Trim(), requestedRecordId, StringComparison.OrdinalIgnoreCase) Then Return False
        If requestedEyeNo <> "" AndAlso Not String.Equals(Convert.ToString(row.Cells("EyeNo").Value).Trim(), requestedEyeNo, StringComparison.OrdinalIgnoreCase) Then Return False
        If requestedMeasureId <> "" AndAlso Not String.Equals(Convert.ToString(row.Cells("MeasureId").Value).Trim(), requestedMeasureId, StringComparison.OrdinalIgnoreCase) Then Return False
        If requestedMeasurementDate <> "" AndAlso Not MeasurementDatesMatch(Convert.ToString(row.Cells("MeasurementDate").Value), requestedMeasurementDate) Then Return False
        Return True
    End Function

    Private Shared Function MeasurementDatesMatch(leftText As String, rightText As String) As Boolean
        If String.Equals(If(leftText, "").Trim(), If(rightText, "").Trim(), StringComparison.OrdinalIgnoreCase) Then Return True

        Dim leftDate As DateTime
        Dim rightDate As DateTime
        Return TryParseHistoryDate(leftText, leftDate) AndAlso
               TryParseHistoryDate(rightText, rightDate) AndAlso
               leftDate = rightDate
    End Function

    Private Sub AddFilterBlock(parent As FlowLayoutPanel, caption As String, box As TextBox, width As Integer, placeholder As String)
        Dim p As New Panel() With {.Width = width + 10, .Height = 42, .Margin = New Padding(0, 0, 10, 6)}
        p.Controls.Add(New Label() With {.Text = caption, .Left = 0, .Top = 0, .Width = width + 5, .Height = 16, .BackColor = Color.Transparent})
        box.SetBounds(0, 17, width, 24)
        box.PlaceholderText = placeholder
        p.Controls.Add(box)
        parent.Controls.Add(p)
    End Sub

    Private Sub ApplyResponsiveHistoryLayout()
        If isApplyingResponsiveLayout OrElse historyLayout Is Nothing OrElse historyLayout.IsDisposed OrElse
           historyToolbar Is Nothing OrElse historyToolbar.IsDisposed Then Return

        isApplyingResponsiveLayout = True
        Try
            Dim availableWidth = Math.Max(320, historyLayout.ClientSize.Width)
            Dim availableHeight = Math.Max(300, historyLayout.ClientSize.Height)

            historyToolbar.AutoScroll = False
            Dim preferredToolbarHeight = historyToolbar.GetPreferredSize(New Size(availableWidth, 0)).Height
            preferredToolbarHeight = Math.Max(58, preferredToolbarHeight)

            Dim summaryTextWidth = Math.Max(220, availableWidth - 36)
            Dim summaryText = If(lblCount.Text, "")
            Dim measuredSummary = TextRenderer.MeasureText(
                summaryText,
                lblCount.Font,
                New Size(summaryTextWidth, 240),
                TextFormatFlags.Left Or TextFormatFlags.WordBreak Or TextFormatFlags.NoPrefix)
            Dim summaryHeight = Math.Max(34, Math.Min(96, measuredSummary.Height + 10))

            Dim dpiScale = Math.Max(1.0R, DeviceDpi / 96.0R)
            Dim minimumGridHeight = Math.Max(130, CInt(Math.Round(170 * dpiScale)))
            Dim maximumToolbarHeight = Math.Max(58, availableHeight - summaryHeight - minimumGridHeight)
            Dim toolbarHeight = Math.Min(preferredToolbarHeight, maximumToolbarHeight)

            historyLayout.RowStyles(0).Height = toolbarHeight
            historyLayout.RowStyles(1).Height = summaryHeight
            historyToolbar.AutoScroll = preferredToolbarHeight > toolbarHeight
            historyLayout.PerformLayout()
        Catch ex As Exception
            ErrorLogService.Log("FrmMeasurementHistory.ApplyResponsiveHistoryLayout", ex)
        Finally
            isApplyingResponsiveLayout = False
        End Try
    End Sub

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.MultiSelect = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
        grid.RowHeadersVisible = False
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.GridColor = Color.Gainsboro
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 235, 235)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
        grid.DefaultCellStyle.BackColor = Color.White
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248)
        grid.RowTemplate.Height = 26
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None
        grid.ScrollBars = ScrollBars.Both
        AddHandler grid.CellClick, AddressOf Grid_CellClick
        AddHandler grid.CellFormatting, AddressOf Grid_CellFormatting
        AddHandler grid.CellDoubleClick, AddressOf Grid_CellDoubleClick
        AddHandler grid.KeyDown, AddressOf Grid_KeyDown
        AddHandler grid.Resize, Sub() FitMeasureNameColumn()
    End Sub

    Private Sub CorrectSelectedMeasurement_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.IsAdmin, "Geçmiş Ölçüm Düzeltme")
            If grid.CurrentRow Is Nothing OrElse Not String.Equals(GridRowType(grid.CurrentRow), "DETAIL", StringComparison.OrdinalIgnoreCase) Then
                MessageBox.Show(
                    "Önce TR ve göz gruplarını açın, ardından düzeltilecek ölçünün detay satırını seçin.",
                    "Ölçü detay satırını seçin",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information)
                Return
            End If

            Dim selectedRow = grid.CurrentRow
            Dim recordId = Convert.ToString(selectedRow.Cells("RecordId").Value).Trim()
            Dim eyeNo = Convert.ToString(selectedRow.Cells("EyeNo").Value).Trim()
            Dim measureId = Convert.ToString(selectedRow.Cells("MeasureId").Value).Trim()
            Dim measureName = Convert.ToString(selectedRow.Cells("MeasureName").Value).Trim()
            Dim oldValue = Convert.ToString(selectedRow.Cells("MeasuredValue").Value).Trim()
            Dim lowerLimit = Convert.ToString(selectedRow.Cells("LowerLimit").Value).Trim()
            Dim upperLimit = Convert.ToString(selectedRow.Cells("UpperLimit").Value).Trim()
            Dim measurementDate = Convert.ToString(selectedRow.Cells("MeasurementDate").Value).Trim()
            Dim operatorName = Convert.ToString(selectedRow.Cells("OperatorName").Value).Trim()
            Dim trCode = Convert.ToString(selectedRow.Cells("TrCode").Value).Trim()

            If recordId = "" OrElse measureId = "" OrElse oldValue = "" Then
                MessageBox.Show("Seçili satır düzenlenebilir bir ölçüm kaydı değildir.", "Ölçüm düzeltme", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Using correctionForm As New FrmMeasurementCorrection(
                recordId,
                trCode,
                eyeNo,
                measureId,
                measureName,
                oldValue,
                lowerLimit,
                upperLimit,
                measurementDate,
                operatorName)

                If correctionForm.ShowDialog(Me) <> DialogResult.OK Then Return
                Dim confirmation = MessageBox.Show(
                    "Ölçüm değeri değiştirilecek:" & Environment.NewLine &
                    oldValue & "  →  " & correctionForm.NewValueText & Environment.NewLine & Environment.NewLine &
                    "Bu değişiklik SPC analizlerini etkileyecektir. Devam edilsin mi?",
                    "Geçmiş ölçümü düzelt",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2)
                If confirmation <> DialogResult.Yes Then Return

                Dim newResult = DataService.CorrectMeasurementValue(
                    recordId,
                    eyeNo,
                    measureId,
                    measurementDate,
                    correctionForm.NewValueText,
                    correctionForm.CorrectionReason)

                LoadGrid()
                MessageBox.Show(
                    "Ölçüm değeri düzeltildi." & Environment.NewLine &
                    "Yeni sonuç: " & newResult & Environment.NewLine &
                    "Eski ve yeni değer düzeltme geçmişine kaydedildi.",
                    "Düzeltme tamamlandı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information)
            End Using
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Ölçüm değeri düzeltilemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub DeleteSelectedRecord_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.IsAdmin, "Ölçüm Kaydı Silme")

            If grid.CurrentRow Is Nothing OrElse Not grid.Columns.Contains("RecordId") Then
                MessageBox.Show("Önce silinecek ölçüm kaydını seçiniz.", "Ölçüm kaydı silme", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            If Not IsRecordGroupGridRow(grid.CurrentRow) Then
                MessageBox.Show(
                    "TR özet satırı veya alt ölçü satırı silinmez. Silmek istediğiniz göz kaydının grup satırını seçiniz.",
                    "Göz kaydı satırını seçin",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information)
                Return
            End If

            Dim recordId = Convert.ToString(grid.CurrentRow.Cells("RecordId").Value).Trim()
            If recordId = "" Then
                MessageBox.Show("Seçili satırın kayıt numarası bulunamadı.", "Ölçüm kaydı silme", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim trCode = If(grid.Columns.Contains("TrCode"), Convert.ToString(grid.CurrentRow.Cells("TrCode").Value).Trim(), "")
            Dim drawingRev = If(grid.Columns.Contains("DrawingRev"), Convert.ToString(grid.CurrentRow.Cells("DrawingRev").Value).Trim(), "")
            Dim eyeNo = If(grid.Columns.Contains("EyeNo"), Convert.ToString(grid.CurrentRow.Cells("EyeNo").Value).Trim(), "")
            Dim confirmation = Microsoft.VisualBasic.Interaction.InputBox(
                "Bu işlem geri alınamaz." & Environment.NewLine &
                "Kayıt No: " & recordId & Environment.NewLine &
                "TR: " & If(trCode = "", "-", trCode) & "   Göz No: " & If(eyeNo = "", "-", eyeNo) & Environment.NewLine & Environment.NewLine &
                "Kaydı ve bağlı görsel kontrol sonuçlarını silmek için ONAY yazınız.",
                "ADMIN ölçüm kaydı silme doğrulaması",
                "")

            If Not String.Equals(confirmation.Trim(), "ONAY", StringComparison.Ordinal) Then
                MessageBox.Show("ONAY yazılmadığı için silme işlemi iptal edildi.", "İşlem iptal edildi", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim deletedMeasurements As Integer = 0
            Dim deletedVisuals As Integer = 0
            Dim deletedClosedEyes As Integer = 0
            DataService.DeleteMeasurementRecord(recordId, deletedMeasurements, deletedVisuals, deletedClosedEyes)

            AuditService.Log(
                "MEASUREMENT_RECORD_DELETE",
                trCode,
                drawingRev,
                "RecordId=" & recordId &
                "; MeasurementRows=" & deletedMeasurements.ToString() &
                "; VisualRows=" & deletedVisuals.ToString() &
                "; ClosedEyeRows=" & deletedClosedEyes.ToString() &
                "; DeletedBy=" & AppState.CurrentUserName)

            LoadGrid()
            MessageBox.Show(
                "Ölçüm kaydı silindi." & Environment.NewLine &
                "Ölçüm satırı: " & deletedMeasurements.ToString() & Environment.NewLine &
                "Görsel kontrol satırı: " & deletedVisuals.ToString() & Environment.NewLine &
                "Kapalı göz satırı: " & deletedClosedEyes.ToString(),
                "Silme tamamlandı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Ölçüm kaydı silinemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub LoadGrid()
        Try
            Dim measurementRows = DataService.GetMeasurementRows()
            Dim closedRows = DataService.GetClosedEyeRows()
            Dim visualRows = DataService.GetVisualControlRows()
            Dim visualByRecord = BuildVisualIndex(visualRows)
            Dim productByKey = BuildProductIndex()

            Dim allRows As New List(Of Dictionary(Of String, String))()
            allRows.AddRange(measurementRows)

            For Each c In closedRows
                allRows.Add(ClosedEyeToHistoryRow(c))
            Next

            allRows = allRows.
                Where(Function(r) AppState.CanAccessDrawingScope(GetHistoryRowDrawingScope(r, productByKey))).
                Where(Function(r) String.IsNullOrWhiteSpace(initialDrawingScope) OrElse
                                  String.Equals(
                                      GetHistoryRowDrawingScope(r, productByKey),
                                      initialDrawingScope,
                                      StringComparison.OrdinalIgnoreCase)).
                ToList()

            todayMeasurementCount = CountTodayMeasurementGroups(allRows)

            Dim rows = allRows

            Dim general = txtSearch.Text.Trim()
            Dim tr = txtTr.Text.Trim()
            Dim lot = txtLot.Text.Trim()
            Dim serial = txtSerial.Text.Trim()
            Dim measure = txtMeasure.Text.Trim()
            Dim operatorName = txtOperator.Text.Trim()
            Dim res = cboResult.Text.Trim()
            Dim visualRes = cboVisualResult.Text.Trim()
            If visualRes = "UYGUNSUZ" Then visualRes = "UYGUN DEĞİL"

            If general <> "" Then
                Dim tokens = general.Split(New Char() {" "c, ";"c, ","c, "|"c}, StringSplitOptions.RemoveEmptyEntries)
                rows = rows.Where(Function(r)
                                      Dim recordId = DataService.GetValue(r, "RecordId")
                                      Dim isClosed = IsClosedEyeRow(r)
                                      Dim visualStatus = If(isClosed, "GÖZ KAPALI", GetVisualStatus(recordId, visualByRecord))
                                      Dim visualFailed = If(isClosed, "", GetVisualFailedList(recordId, visualByRecord))
                                      Dim visualNotes = If(isClosed, "", GetVisualNotes(recordId, visualByRecord))
                                      Dim productText = GetProductSearchText(r, productByKey)

                                      Dim haystack As String =
                                          (productText & " " &
                                           recordId & " " &
                                           DataService.GetValue(r, "TrCode") & " " &
                                           DataService.GetValue(r, "DrawingRev") & " " &
                                           DataService.GetValue(r, "LotNo") & " " &
                                           DataService.GetValue(r, "SerialNo") & " " &
                                           DataService.GetValue(r, "EyeCount") & " " &
                                           DataService.GetValue(r, "EyeNo") & " " &
                                           DataService.GetValue(r, "OperatorName") & " " &
                                           DataService.GetValue(r, "ComputerName") & " " &
                                           DataService.GetValue(r, "MeasureId") & " " &
                                           DataService.GetValue(r, "MeasureName") & " " &
                                           DataService.GetValue(r, "Result") & " " &
                                           DataService.GetValue(r, "Note") & " " &
                                           visualStatus & " " &
                                           visualFailed & " " &
                                           visualNotes).ToUpperInvariant()
                                      For Each token In tokens
                                          If Not haystack.Contains(token.ToUpperInvariant()) Then Return False
                                      Next
                                      Return True
                                  End Function).ToList()
            End If

            If tr <> "" Then
                rows = rows.Where(Function(r) DataService.GetValue(r, "TrCode").IndexOf(tr, StringComparison.OrdinalIgnoreCase) >= 0).ToList()
            End If

            If lot <> "" Then
                rows = rows.Where(Function(r) DataService.GetValue(r, "LotNo").IndexOf(lot, StringComparison.OrdinalIgnoreCase) >= 0).ToList()
            End If

            If serial <> "" Then
                rows = rows.Where(Function(r) DataService.GetValue(r, "SerialNo").IndexOf(serial, StringComparison.OrdinalIgnoreCase) >= 0).ToList()
            End If

            If measure <> "" Then
                rows = rows.Where(Function(r)
                                      Return DataService.GetValue(r, "MeasureId").IndexOf(measure, StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                                             DataService.GetValue(r, "MeasureName").IndexOf(measure, StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                                             DataService.GetValue(r, "Result").IndexOf(measure, StringComparison.OrdinalIgnoreCase) >= 0
                                  End Function).ToList()
            End If

            If operatorName <> "" Then
                rows = rows.Where(
                    Function(r) DataService.GetValue(r, "OperatorName").
                        IndexOf(operatorName, StringComparison.OrdinalIgnoreCase) >= 0).
                    ToList()
            End If

            If res <> "" AndAlso res <> "TÜMÜ" Then
                rows = rows.Where(Function(r) String.Equals(DataService.GetValue(r, "Result"), res, StringComparison.OrdinalIgnoreCase)).ToList()
            End If

            If visualRes <> "" AndAlso visualRes <> "TÜMÜ" Then
                rows = rows.Where(Function(r)
                                      If IsClosedEyeRow(r) Then
                                          Return String.Equals(visualRes, "GÖZ KAPALI", StringComparison.OrdinalIgnoreCase)
                                      End If
                                      Return String.Equals(GetVisualStatus(DataService.GetValue(r, "RecordId"), visualByRecord), visualRes, StringComparison.OrdinalIgnoreCase)
                                  End Function).ToList()
            End If

            Dim matchedGroupKeys = New HashSet(Of String)(
                rows.Select(Function(row) HistoryGroupKey(row)),
                StringComparer.OrdinalIgnoreCase)

            currentVisualByRecord = visualByRecord
            currentProductByKey = productByKey
            totalDetailRowCount = allRows.Count
            filteredDetailRowCount = rows.Count

            Dim allGroups = allRows.
                GroupBy(Function(row) HistoryGroupKey(row), StringComparer.OrdinalIgnoreCase).
                Select(Function(group)
                           Dim firstRow = group.First()
                           Return New HistoryRecordGroup With {
                               .Key = group.Key,
                               .RecordId = DataService.GetValue(firstRow, "RecordId"),
                               .Rows = group.OrderBy(Function(row) DetailSortValue(row)).ToList()
                           }
                       End Function).
                OrderByDescending(Function(group) DataService.GetValue(group.Rows.First(), "MeasurementDate")).
                ToList()

            totalGroupCount = allGroups.Count
            visibleGroups = allGroups.Where(Function(group) matchedGroupKeys.Contains(group.Key)).ToList()
            expandedGroupKeys.RemoveWhere(Function(key) Not visibleGroups.Any(Function(group) String.Equals(group.Key, key, StringComparison.OrdinalIgnoreCase)))
            Dim visibleMeasurementRecordKeys = New HashSet(Of String)(visibleGroups.Select(Function(group) MeasurementRecordGroupKey(group)), StringComparer.OrdinalIgnoreCase)
            expandedTrGroupKeys.RemoveWhere(Function(key) Not visibleMeasurementRecordKeys.Contains(key))

            BindGroupedGrid()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Ölçüm geçmişi okunamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub BindGroupedGrid(Optional selectedGroupKey As String = "")
        Dim firstDisplayedIndex As Integer = -1
        Try
            firstDisplayedIndex = grid.FirstDisplayedScrollingRowIndex
        Catch
        End Try

        Dim dt = CreateHistoryDisplayTable()

        Dim measurementRecordGroups = visibleGroups.
            GroupBy(Function(group) MeasurementRecordGroupKey(group), StringComparer.OrdinalIgnoreCase).
            OrderByDescending(Function(recordGroup) recordGroup.Max(Function(group) GroupMeasurementDateTicks(group))).
            ThenBy(Function(recordGroup) RecordGroupTrSortText(recordGroup.First())).
            ToList()

        For Each measurementRecordGroup In measurementRecordGroups
            Dim trKey = measurementRecordGroup.Key
            Dim eyeGroups = measurementRecordGroup.
                OrderBy(Function(group) GroupEyeNumberSortValue(group)).
                ThenBy(Function(group) GroupEyeNumberSortText(group)).
                ThenByDescending(Function(group) GroupMeasurementDateTicks(group)).
                ThenBy(Function(group) group.RecordId).
                ToList()
            AddTrGroupRow(dt, trKey, eyeGroups)

            If expandedTrGroupKeys.Contains(trKey) Then
                For Each group In eyeGroups
                    AddGroupRow(dt, group)

                    If expandedGroupKeys.Contains(group.Key) Then
                        For Each detailRow In group.Rows
                            AddDetailRow(dt, group, detailRow)
                        Next
                    End If
                Next
            End If
        Next

        grid.DataSource = dt
        ApplyColumnHeaders()

        lblCount.Text = "Bugün yapılan ölçüm: " & todayMeasurementCount.ToString() &
                        "   |   Ölçüm kaydı: " & measurementRecordGroups.Count.ToString() &
                        "   |   Göz kaydı: " & visibleGroups.Count.ToString() & " / " & totalGroupCount.ToString() &
                        "   |   Eşleşen ölçü satırı: " & filteredDetailRowCount.ToString() & " / " & totalDetailRowCount.ToString() &
                        "   |   Açık grup: " & (expandedTrGroupKeys.Count + expandedGroupKeys.Count).ToString()
        ApplyResponsiveHistoryLayout()

        If selectedGroupKey <> "" Then
            For Each row As DataGridViewRow In grid.Rows
                If String.Equals(Convert.ToString(row.Cells("_GroupKey").Value), selectedGroupKey, StringComparison.OrdinalIgnoreCase) Then
                    grid.CurrentCell = row.Cells("_Toggle")
                    row.Selected = True
                    Exit For
                End If
            Next
        End If

        If firstDisplayedIndex >= 0 AndAlso firstDisplayedIndex < grid.Rows.Count Then
            Try
                grid.FirstDisplayedScrollingRowIndex = firstDisplayedIndex
            Catch
            End Try
        End If
    End Sub

    Private Function CreateHistoryDisplayTable() As DataTable
        Dim dt As New DataTable()
        dt.Columns.Add("_Toggle")
        dt.Columns.Add("_RowType")
        dt.Columns.Add("_GroupKey")

        For Each header In DataService.MeasurementHeaders
            dt.Columns.Add(header)
        Next

        For Each extraColumn In {"ProductName", "PlasticCode", "Material", "ColorName", "MoldCavityCount", "MoldCode", "VisualStatus", "VisualFailed", "VisualNotes"}
            dt.Columns.Add(extraColumn)
        Next

        Return dt
    End Function

    Private Sub AddTrGroupRow(dt As DataTable, trKey As String, eyeGroups As List(Of HistoryRecordGroup))
        If eyeGroups Is Nothing OrElse eyeGroups.Count = 0 Then Return

        Dim allRows = eyeGroups.SelectMany(Function(group) group.Rows).ToList()
        If allRows.Count = 0 Then Return

        Dim firstRow = allRows.First()
        Dim dr = dt.NewRow()
        FillHistoryDataRow(dr, firstRow)

        dr("_Toggle") = If(expandedTrGroupKeys.Contains(trKey), "▼", "▶")
        dr("_RowType") = "TR_GROUP"
        dr("_GroupKey") = trKey
        dr("TrCode") = DisplayTrCode(firstRow)
        dr("RecordId") = eyeGroups.Count.ToString() & " göz kaydı"
        Dim measurementRows = allRows.Where(Function(row) Not IsClosedEyeRow(row)).ToList()
        Dim closedEyeCount As Integer = eyeGroups.
            Where(Function(group) group.Rows.All(Function(row) IsClosedEyeRow(row))).
            Count()
        If measurementRows.Count = 0 Then
            dr("MeasureId") = closedEyeCount.ToString() & " kapalı göz"
            dr("MeasureName") = "Bu TR için ölçüm alınmadı"
            dr("Result") = "GÖZ KAPALI"
        Else
            dr("MeasureId") = measurementRows.Count.ToString() & " ölçü satırı"
            If closedEyeCount > 0 Then
                dr("MeasureId") = Convert.ToString(dr("MeasureId")) & " + " & closedEyeCount.ToString() & " kapalı göz"
            End If
            dr("MeasureName") = BuildGroupResultSummary(measurementRows)
            dr("Result") = OverallGroupResult(measurementRows)
        End If
        dr("DrawingRev") = CommonGroupValue(allRows, "DrawingRev")
        dr("LotNo") = CommonGroupValue(allRows, "LotNo")
        dr("SerialNo") = CommonGroupValue(allRows, "SerialNo")
        dr("OperatorName") = CommonGroupValue(allRows, "OperatorName")
        dr("ComputerName") = CommonGroupValue(allRows, "ComputerName")
        dr("MeasurementDate") = BuildMeasurementDateSummary(allRows)
        dr("EyeNo") = BuildEyeNumberSummary(eyeGroups)
        dr("VisualStatus") = OverallVisualStatus(eyeGroups)
        dr("VisualFailed") = ""
        dr("VisualNotes") = ""

        For Each columnName In {"MeasurementGroup", "SampleFrequency", "IsCritical", "SortNo", "Nominal", "LowerLimit", "UpperLimit", "PageNo", "XPercent", "YPercent", "MeasuredValue", "Note"}
            If dt.Columns.Contains(columnName) Then dr(columnName) = ""
        Next

        dt.Rows.Add(dr)
    End Sub

    Private Sub AddGroupRow(dt As DataTable, group As HistoryRecordGroup)
        If group Is Nothing OrElse group.Rows.Count = 0 Then Return

        Dim firstRow = group.Rows.First()
        Dim dr = dt.NewRow()
        FillHistoryDataRow(dr, firstRow)

        dr("_Toggle") = If(expandedGroupKeys.Contains(group.Key), "  ▼", "  ▶")
        dr("_RowType") = "EYE_GROUP"
        dr("_GroupKey") = group.Key
        dr("RecordId") = group.RecordId

        Dim closedGroup = group.Rows.All(Function(row) IsClosedEyeRow(row))
        If closedGroup Then
            dr("MeasureId") = "GÖZ KAPALI"
            dr("MeasureName") = "Bu göz için ölçüm alınmadı"
            dr("Result") = "GÖZ KAPALI"
        Else
            dr("MeasureId") = group.Rows.Count.ToString() & " ölçü"
            dr("MeasureName") = BuildGroupResultSummary(group.Rows)
            dr("Result") = OverallGroupResult(group.Rows)
        End If

        For Each columnName In {"MeasurementGroup", "SampleFrequency", "IsCritical", "SortNo", "Nominal", "LowerLimit", "UpperLimit", "PageNo", "XPercent", "YPercent", "MeasuredValue", "Note"}
            If dt.Columns.Contains(columnName) Then dr(columnName) = ""
        Next

        dt.Rows.Add(dr)
    End Sub

    Private Sub AddDetailRow(dt As DataTable, group As HistoryRecordGroup, historyRow As Dictionary(Of String, String))
        Dim dr = dt.NewRow()
        FillHistoryDataRow(dr, historyRow)
        dr("_Toggle") = "    •"
        dr("_RowType") = "DETAIL"
        dr("_GroupKey") = group.Key
        dt.Rows.Add(dr)
    End Sub

    Private Sub FillHistoryDataRow(dr As DataRow, historyRow As Dictionary(Of String, String))
        For Each header In DataService.MeasurementHeaders
            dr(header) = DataService.GetValue(historyRow, header)
        Next

        Dim productInfo = GetProductForRow(historyRow, currentProductByKey)
        If productInfo IsNot Nothing Then
            dr("ProductName") = productInfo.ProductName
            dr("PlasticCode") = productInfo.PlasticCode
            dr("Material") = productInfo.Material
            dr("ColorName") = productInfo.ColorName
            dr("MoldCavityCount") = productInfo.MoldCavityCount
            dr("MoldCode") = productInfo.MoldCode
        End If

        Dim recordId = DataService.GetValue(historyRow, "RecordId")
        If IsClosedEyeRow(historyRow) Then
            dr("VisualStatus") = "GÖZ KAPALI"
            dr("VisualFailed") = ""
            dr("VisualNotes") = ""
        Else
            dr("VisualStatus") = GetVisualStatus(recordId, currentVisualByRecord)
            dr("VisualFailed") = GetVisualFailedList(recordId, currentVisualByRecord)
            dr("VisualNotes") = GetVisualNotes(recordId, currentVisualByRecord)
        End If
    End Sub

    Private Shared Function HistoryGroupKey(row As Dictionary(Of String, String)) As String
        Dim recordId = DataService.GetValue(row, "RecordId").Trim()
        If recordId <> "" Then Return recordId

        Return "NOID|" & DataService.GetValue(row, "TrCode") & "|" &
               DataService.GetValue(row, "DrawingRev") & "|" &
               DataService.GetValue(row, "MeasurementDate") & "|" &
               DataService.GetValue(row, "EyeNo")
    End Function

    Private Shared Function CountTodayMeasurementGroups(rows As IEnumerable(Of Dictionary(Of String, String))) As Integer
        Dim todayRows = rows.
            Where(Function(row)
                      Dim measurementDate As DateTime
                      Return TryParseHistoryDate(DataService.GetValue(row, "MeasurementDate"), measurementDate) AndAlso
                             measurementDate.Date = Date.Today
                  End Function).
            ToList()

        Dim measurementRecordKeys = todayRows.
            GroupBy(Function(row) HistoryGroupKey(row), StringComparer.OrdinalIgnoreCase).
            Select(Function(group)
                       Dim firstRow = group.First()
                       Return New HistoryRecordGroup With {
                           .Key = group.Key,
                           .RecordId = DataService.GetValue(firstRow, "RecordId"),
                           .Rows = group.ToList()
                       }
                   End Function).
            Select(Function(group) MeasurementRecordGroupKey(group)).
            Distinct(StringComparer.OrdinalIgnoreCase).
            Count()

        Return measurementRecordKeys
    End Function

    Private Shared Function TryParseHistoryDate(text As String, ByRef value As DateTime) As Boolean
        Return DateTime.TryParseExact(text,
                                      "yyyy-MM-dd HH:mm:ss",
                                      System.Globalization.CultureInfo.InvariantCulture,
                                      System.Globalization.DateTimeStyles.None,
                                      value) OrElse
               DateTime.TryParse(text, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, value) OrElse
               DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, value)
    End Function

    Private Shared Function MeasurementRecordGroupKey(group As HistoryRecordGroup) As String
        If group Is Nothing OrElse group.Rows.Count = 0 Then Return "MEAS|[EMPTY]"

        Dim row = group.Rows.First()
        Return "MEAS|" &
               NormalizeGroupKeyValue(DataService.GetValue(row, "TrCode")) & "|" &
               NormalizeGroupKeyValue(DataService.GetValue(row, "DrawingRev")) & "|" &
               NormalizeGroupKeyValue(DataService.GetValue(row, "DrawingScope")) & "|" &
               NormalizeGroupKeyValue(DataService.GetValue(row, "LotNo")) & "|" &
               NormalizeGroupKeyValue(DataService.GetValue(row, "SerialNo")) & "|" &
               NormalizeGroupKeyValue(DataService.GetValue(row, "OperatorName")) & "|" &
               NormalizeGroupKeyValue(DataService.GetValue(row, "ComputerName")) & "|" &
               MeasurementDateMinuteKey(DataService.GetValue(row, "MeasurementDate"))
    End Function

    Private Shared Function MeasurementDateMinuteKey(text As String) As String
        Dim parsed As DateTime
        If TryParseHistoryDate(text, parsed) Then
            Return parsed.ToString("yyyyMMdd-HHmm", System.Globalization.CultureInfo.InvariantCulture)
        End If

        Return NormalizeGroupKeyValue(text)
    End Function

    Private Shared Function NormalizeGroupKeyValue(text As String) As String
        Dim value = If(text, "").Trim()
        If value = "" Then Return "-"
        Return value.ToUpperInvariant()
    End Function

    Private Shared Function RecordGroupTrSortText(group As HistoryRecordGroup) As String
        If group Is Nothing OrElse group.Rows.Count = 0 Then Return ""
        Return DataService.GetValue(group.Rows.First(), "TrCode").Trim()
    End Function

    Private Shared Function BuildMeasurementDateSummary(rows As List(Of Dictionary(Of String, String))) As String
        If rows Is Nothing OrElse rows.Count = 0 Then Return ""

        Dim parsedDates As New List(Of DateTime)()
        For Each row In rows
            Dim parsed As DateTime
            If TryParseHistoryDate(DataService.GetValue(row, "MeasurementDate"), parsed) Then
                parsedDates.Add(parsed)
            End If
        Next

        If parsedDates.Count = 0 Then Return CommonGroupValue(rows, "MeasurementDate")

        Dim firstDate = parsedDates.Min()
        Dim lastDate = parsedDates.Max()
        If firstDate = lastDate Then Return firstDate.ToString("yyyy-MM-dd HH:mm:ss")
        If firstDate.Date = lastDate.Date Then
            Return firstDate.ToString("yyyy-MM-dd HH:mm:ss") & " - " & lastDate.ToString("HH:mm:ss")
        End If

        Return firstDate.ToString("yyyy-MM-dd HH:mm:ss") & " - " & lastDate.ToString("yyyy-MM-dd HH:mm:ss")
    End Function

    Private Shared Function DisplayTrCode(row As Dictionary(Of String, String)) As String
        Dim trCode = DataService.GetValue(row, "TrCode").Trim()
        Return If(trCode = "", "(TR kodu yok)", trCode)
    End Function

    Private Shared Function CommonGroupValue(rows As List(Of Dictionary(Of String, String)), columnName As String) As String
        Dim values = rows.
            Select(Function(row) DataService.GetValue(row, columnName).Trim()).
            Where(Function(value) value <> "").
            Distinct(StringComparer.OrdinalIgnoreCase).
            Take(2).
            ToList()

        If values.Count = 0 Then Return ""
        If values.Count = 1 Then Return values(0)
        Return "Çeşitli"
    End Function

    Private Shared Function BuildEyeNumberSummary(groups As List(Of HistoryRecordGroup)) As String
        Dim eyeNumbers = groups.
            Where(Function(group) group.Rows.Count > 0).
            Select(Function(group) DataService.GetValue(group.Rows.First(), "EyeNo").Trim()).
            Where(Function(value) value <> "").
            Distinct(StringComparer.OrdinalIgnoreCase).
            OrderBy(Function(value) EyeNumberSortValue(value)).
            ThenBy(Function(value) value).
            ToList()

        If eyeNumbers.Count = 0 Then Return ""
        If eyeNumbers.Count <= 6 Then Return String.Join(", ", eyeNumbers)
        Return eyeNumbers.Count.ToString() & " farklı göz"
    End Function

    Private Shared Function GroupEyeNumberSortValue(group As HistoryRecordGroup) As Integer
        If group Is Nothing OrElse group.Rows.Count = 0 Then Return Integer.MaxValue
        Return EyeNumberSortValue(DataService.GetValue(group.Rows.First(), "EyeNo"))
    End Function

    Private Shared Function GroupEyeNumberSortText(group As HistoryRecordGroup) As String
        If group Is Nothing OrElse group.Rows.Count = 0 Then Return ""
        Return DataService.GetValue(group.Rows.First(), "EyeNo").Trim()
    End Function

    Private Shared Function GroupMeasurementDateTicks(group As HistoryRecordGroup) As Long
        If group Is Nothing OrElse group.Rows.Count = 0 Then Return 0L

        Dim parsed As DateTime
        If TryParseHistoryDate(DataService.GetValue(group.Rows.First(), "MeasurementDate"), parsed) Then
            Return parsed.Ticks
        End If

        Return 0L
    End Function

    Private Shared Function EyeNumberSortValue(value As String) As Integer
        Dim parsed As Integer
        If Integer.TryParse(If(value, "").Trim(), parsed) Then Return parsed
        Return Integer.MaxValue
    End Function

    Private Function OverallVisualStatus(groups As List(Of HistoryRecordGroup)) As String
        Dim statuses = groups.
            Select(Function(group)
                       If group.Rows.All(Function(row) IsClosedEyeRow(row)) Then Return "GÖZ KAPALI"
                       Return GetVisualStatus(group.RecordId, currentVisualByRecord)
                   End Function).
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToList()

        If statuses.Count = 0 Then Return "YOK"
        If statuses.Count = 1 Then Return statuses(0)
        If statuses.Any(Function(value) String.Equals(value, "UYGUN DEĞİL", StringComparison.OrdinalIgnoreCase) OrElse
                                         String.Equals(value, "UYGUNSUZ", StringComparison.OrdinalIgnoreCase)) Then Return "UYGUN DEĞİL"
        Return "ÇEŞİTLİ"
    End Function

    Private Shared Function DetailSortValue(row As Dictionary(Of String, String)) As String
        Dim sortNo As Integer
        If Not Integer.TryParse(DataService.GetValue(row, "SortNo"), sortNo) Then sortNo = Integer.MaxValue
        Return sortNo.ToString("0000000000") & "|" & DataService.GetValue(row, "MeasureId")
    End Function

    Private Shared Function BuildGroupResultSummary(rows As List(Of Dictionary(Of String, String))) As String
        Dim okCount = rows.Where(Function(row) String.Equals(DataService.GetValue(row, "Result"), "OK", StringComparison.OrdinalIgnoreCase)).Count()
        Dim nokCount = rows.Where(Function(row) String.Equals(DataService.GetValue(row, "Result"), "NOK", StringComparison.OrdinalIgnoreCase)).Count()
        Dim errorCount = rows.Where(Function(row) String.Equals(DataService.GetValue(row, "Result"), "HATALI", StringComparison.OrdinalIgnoreCase)).Count()
        Return "OK: " & okCount.ToString() & " | NOK: " & nokCount.ToString() & " | Hatalı: " & errorCount.ToString()
    End Function

    Private Shared Function OverallGroupResult(rows As List(Of Dictionary(Of String, String))) As String
        If rows.Any(Function(row) String.Equals(DataService.GetValue(row, "Result"), "NOK", StringComparison.OrdinalIgnoreCase)) Then Return "NOK"
        If rows.Any(Function(row) String.Equals(DataService.GetValue(row, "Result"), "HATALI", StringComparison.OrdinalIgnoreCase)) Then Return "HATALI"
        If rows.Any(Function(row) String.Equals(DataService.GetValue(row, "Result"), "OK", StringComparison.OrdinalIgnoreCase)) Then Return "OK"
        Return "-"
    End Function

    Private Function ClosedEyeToHistoryRow(c As Dictionary(Of String, String)) As Dictionary(Of String, String)
        Dim row As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

        For Each h In DataService.MeasurementHeaders
            row(h) = ""
        Next

        row("RecordId") = DataService.GetValue(c, "RecordId")
        row("TrCode") = DataService.GetValue(c, "TrCode")
        row("DrawingRev") = DataService.GetValue(c, "DrawingRev")
        row("DrawingScope") = DataService.GetValue(c, "DrawingScope")
        row("LotNo") = DataService.GetValue(c, "LotNo")
        row("SerialNo") = DataService.GetValue(c, "SerialNo")
        row("EyeCount") = DataService.GetValue(c, "EyeCount")
        row("EyeNo") = DataService.GetValue(c, "EyeNo")
        row("OperatorName") = DataService.GetValue(c, "OperatorName")
        row("ComputerName") = DataService.GetValue(c, "ComputerName")
        row("MeasurementDate") = DataService.GetValue(c, "ClosedDate")
        row("MeasureId") = ""
        row("MeasureName") = "GÖZ KAPALI"
        row("Result") = "GÖZ KAPALI"
        row("Note") = DataService.GetValue(c, "Reason")
        Return row
    End Function

    Private Function IsClosedEyeRow(r As Dictionary(Of String, String)) As Boolean
        Return String.Equals(DataService.GetValue(r, "Result"), "GÖZ KAPALI", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(DataService.GetValue(r, "MeasureName"), "GÖZ KAPALI", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function BuildProductIndex() As Dictionary(Of String, ProductInfo)
        Dim result As New Dictionary(Of String, ProductInfo)(StringComparer.OrdinalIgnoreCase)

        For Each p In DataService.GetProducts(False)
            Dim key = p.TrCode & "|" & p.DrawingRev
            result(key) = p
        Next

        Return result
    End Function

    Private Function GetProductForRow(r As Dictionary(Of String, String), productByKey As Dictionary(Of String, ProductInfo)) As ProductInfo
        Dim key = DataService.GetValue(r, "TrCode") & "|" & DataService.GetValue(r, "DrawingRev")
        If productByKey.ContainsKey(key) Then Return productByKey(key)
        Return Nothing
    End Function

    Private Function GetHistoryRowDrawingScope(r As Dictionary(Of String, String), productByKey As Dictionary(Of String, ProductInfo)) As String
        Dim scope = DataService.GetValue(r, "DrawingScope")
        If Not String.IsNullOrWhiteSpace(scope) Then Return ProductInfo.NormalizeDrawingScope(scope)

        Dim p = GetProductForRow(r, productByKey)
        If p IsNot Nothing Then Return ProductInfo.NormalizeDrawingScope(p.DrawingScope)

        Return ProductInfo.DrawingScopePlastic
    End Function

    Private Function GetProductSearchText(r As Dictionary(Of String, String), productByKey As Dictionary(Of String, ProductInfo)) As String
        Dim p = GetProductForRow(r, productByKey)
        If p Is Nothing Then Return ""
        Return ProductInfo.NormalizeDrawingScope(p.DrawingScope) & " " & p.ProductName & " " & p.PlasticCode & " " & p.Material & " " & p.ColorName & " " & p.MoldCavityCount & " " & p.MoldCode
    End Function

    Private Function BuildVisualIndex(visualRows As List(Of Dictionary(Of String, String))) As Dictionary(Of String, List(Of Dictionary(Of String, String)))
        Dim result As New Dictionary(Of String, List(Of Dictionary(Of String, String)))(StringComparer.OrdinalIgnoreCase)

        For Each r In visualRows
            Dim recordId = DataService.GetValue(r, "RecordId")
            If recordId = "" Then Continue For

            If Not result.ContainsKey(recordId) Then
                result(recordId) = New List(Of Dictionary(Of String, String))()
            End If
            result(recordId).Add(r)
        Next

        Return result
    End Function

    Private Function GetVisualStatus(recordId As String, visualByRecord As Dictionary(Of String, List(Of Dictionary(Of String, String)))) As String
        If recordId = "" OrElse Not visualByRecord.ContainsKey(recordId) OrElse visualByRecord(recordId).Count = 0 Then Return "YOK"

        Dim hasSuitable As Boolean = False

        For Each r In visualByRecord(recordId)
            Dim resultText = DataService.GetValue(r, "Result").Trim().ToUpperInvariant()
            If resultText = "UYGUN DEĞİL" OrElse resultText = "UYGUNSUZ" OrElse resultText = "NOK" Then Return "UYGUN DEĞİL"
            If resultText = "UYGUN" OrElse resultText = "OK" Then hasSuitable = True
        Next

        If hasSuitable Then Return "UYGUN"
        Return "YOK"
    End Function

    Private Function GetVisualFailedList(recordId As String, visualByRecord As Dictionary(Of String, List(Of Dictionary(Of String, String)))) As String
        If recordId = "" OrElse Not visualByRecord.ContainsKey(recordId) Then Return ""

        Dim failed As New List(Of String)()

        For Each r In visualByRecord(recordId)
            Dim resultText = DataService.GetValue(r, "Result").Trim().ToUpperInvariant()
            If resultText = "UYGUN DEĞİL" OrElse resultText = "UYGUNSUZ" OrElse resultText = "NOK" Then
                Dim controlName = DataService.GetValue(r, "ControlName")
                If controlName <> "" Then failed.Add(controlName)
            End If
        Next

        Return String.Join(", ", failed.Distinct())
    End Function

    Private Function GetVisualNotes(recordId As String, visualByRecord As Dictionary(Of String, List(Of Dictionary(Of String, String)))) As String
        If recordId = "" OrElse Not visualByRecord.ContainsKey(recordId) Then Return ""

        Dim notes As New List(Of String)()

        For Each r In visualByRecord(recordId)
            Dim noteText = DataService.GetValue(r, "Note").Trim()
            If noteText = "" Then Continue For

            Dim controlName = DataService.GetValue(r, "ControlName").Trim()
            If controlName <> "" Then
                notes.Add(controlName & ": " & noteText)
            Else
                notes.Add(noteText)
            End If
        Next

        Return String.Join(" | ", notes.Distinct())
    End Function

    Private Sub ClearFilters_Click(sender As Object, e As EventArgs)
        txtSearch.Clear()
        txtTr.Clear()
        txtLot.Clear()
        txtSerial.Clear()
        txtMeasure.Clear()
        txtOperator.Clear()
        cboResult.SelectedIndex = 0
        cboVisualResult.SelectedIndex = 0
        LoadGrid()
    End Sub

    Private Sub ApplyColumnHeaders()
        If grid.Columns.Count = 0 Then Return

        If grid.Columns.Contains("_RowType") Then grid.Columns("_RowType").Visible = False
        If grid.Columns.Contains("_GroupKey") Then grid.Columns("_GroupKey").Visible = False
        If grid.Columns.Contains("DrawingScope") Then grid.Columns("DrawingScope").Visible = False
        If grid.Columns.Contains("_Toggle") Then
            Dim toggleColumn = grid.Columns("_Toggle")
            toggleColumn.HeaderText = ""
            toggleColumn.Width = 58
            toggleColumn.MinimumWidth = 58
            toggleColumn.DisplayIndex = 0
            toggleColumn.Frozen = True
            toggleColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
            toggleColumn.SortMode = DataGridViewColumnSortMode.NotSortable
        End If

        SetHeader("TrCode", "TR Kodu", 110, 0)
        SetHeader("RecordId", "Kayıt No", 140, 1)
        SetHeader("MeasurementDate", "Ölçüm Tarihi", 145, 2)
        SetHeader("OperatorName", "Ölçümü Yapan", 120, 3)
        SetHeader("DrawingRev", "Revizyon", 75, 4)
        SetHeader("ProductName", "Ürün Adı", 140, 5)
        SetHeader("PlasticCode", "Plastik Kodu", 110, 6)
        SetHeader("Material", "Malzeme", 120, 7)
        SetHeader("ColorName", "Renk", 90, 8)
        SetHeader("MoldCavityCount", "Kalıp Göz Adedi", 120, 9)
        SetHeader("MoldCode", "Kalıp Kodu", 110, 10)
        SetHeader("LotNo", "İş Emri No", 110, 11)
        SetHeader("SerialNo", "Seri No", 90, 12)
        SetHeader("EyeCount", "Göz Adedi", 80, 13)
        SetHeader("EyeNo", "Göz No", 70, 14)
        SetHeader("MeasureId", "Ölçü No", 140, 15)
        SetHeader("MeasureName", "Ölçü Adı", 140, 16)
        SetHeader("Nominal", "Nominal", 75, 17)
        SetHeader("LowerLimit", "Alt Limit", 75, 18)
        SetHeader("UpperLimit", "Üst Limit", 75, 19)
        SetHeader("MeasuredValue", "Girilen Değer", 95, 20)
        SetHeader("Result", "Ölçüm Sonucu", 95, 21)
        SetHeader("VisualStatus", "Görsel Sonuç", 110, 22)
        SetHeader("VisualFailed", "Geçmeyen Görsel Kontroller", 240, 23)
        SetHeader("VisualNotes", "Görsel Not", 260, 24)
        SetHeader("ComputerName", "Bilgisayar", 90, 25)
        SetHeader("Note", "Ölçüm Notu", 160, 26)
        SetHeader("PageNo", "Sayfa", 55, 27)
        SetHeader("XPercent", "X %", 60, 28)
        SetHeader("YPercent", "Y %", 60, 29)

        For Each col As DataGridViewColumn In grid.Columns
            If col.Name <> "_Toggle" Then col.MinimumWidth = 55
            col.SortMode = DataGridViewColumnSortMode.NotSortable
        Next

        If grid.Columns.Contains("MeasureName") Then
            Dim measureNameColumn = grid.Columns("MeasureName")
            measureNameColumn.DisplayIndex = 1
        End If

        FitMeasureNameColumn()
    End Sub

    Private Sub FitMeasureNameColumn()
        If grid Is Nothing OrElse grid.IsDisposed OrElse Not grid.Columns.Contains("MeasureName") Then Return

        Try
            Dim column = grid.Columns("MeasureName")
            Dim dpiScale = Math.Max(1.0R, DeviceDpi / 96.0R)
            Dim minimumWidth = CInt(Math.Round(180 * dpiScale))
            Dim maximumWidth = Math.Max(
                minimumWidth,
                Math.Min(CInt(Math.Round(420 * dpiScale)), CInt(Math.Round(Math.Max(480, grid.ClientSize.Width) * 0.42R))))

            Dim desiredWidth = TextRenderer.MeasureText(
                column.HeaderText,
                grid.ColumnHeadersDefaultCellStyle.Font,
                New Size(2400, 200),
                TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine).Width + CInt(Math.Round(28 * dpiScale))

            Dim sampledRows As Integer = 0
            For Each row As DataGridViewRow In grid.Rows
                If row.IsNewRow Then Continue For

                Dim valueText = Convert.ToString(row.Cells(column.Index).Value)
                If valueText.Length > 90 Then valueText = valueText.Substring(0, 90)

                Dim measuredWidth = TextRenderer.MeasureText(
                    valueText,
                    grid.DefaultCellStyle.Font,
                    New Size(3200, 200),
                    TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine).Width + CInt(Math.Round(28 * dpiScale))
                desiredWidth = Math.Max(desiredWidth, measuredWidth)

                sampledRows += 1
                If sampledRows >= 200 Then Exit For
            Next

            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            column.MinimumWidth = minimumWidth
            column.Width = Math.Max(minimumWidth, Math.Min(desiredWidth, maximumWidth))
        Catch ex As Exception
            ErrorLogService.Log("FrmMeasurementHistory.FitMeasureNameColumn", ex)
        End Try
    End Sub

    Private Sub SetHeader(columnName As String, headerText As String, width As Integer, displayIndex As Integer)
        If grid.Columns.Contains(columnName) Then
            Dim c = grid.Columns(columnName)
            c.HeaderText = headerText
            c.Width = width
            c.DisplayIndex = Math.Min(displayIndex + 1, grid.Columns.Count - 1)
        End If
    End Sub

    Private Sub Grid_CellClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 OrElse e.ColumnIndex < 0 Then Return
        If Not grid.Columns.Contains("_Toggle") OrElse e.ColumnIndex <> grid.Columns("_Toggle").Index Then Return
        ToggleGroupAtRow(e.RowIndex)
    End Sub

    Private Sub Grid_KeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode <> Keys.Enter AndAlso e.KeyCode <> Keys.Space Then Return
        If grid.CurrentRow Is Nothing OrElse Not IsExpandableGridRow(grid.CurrentRow) Then Return

        ToggleGroupAtRow(grid.CurrentRow.Index)
        e.Handled = True
        e.SuppressKeyPress = True
    End Sub

    Private Sub ToggleGroupAtRow(rowIndex As Integer)
        If rowIndex < 0 OrElse rowIndex >= grid.Rows.Count Then Return

        Dim row = grid.Rows(rowIndex)
        If Not IsExpandableGridRow(row) Then Return

        Dim groupKey = Convert.ToString(row.Cells("_GroupKey").Value).Trim()
        If groupKey = "" Then Return

        If String.Equals(GridRowType(row), "TR_GROUP", StringComparison.OrdinalIgnoreCase) Then
            If expandedTrGroupKeys.Contains(groupKey) Then
                expandedTrGroupKeys.Remove(groupKey)
            Else
                expandedTrGroupKeys.Add(groupKey)
            End If
        Else
            If expandedGroupKeys.Contains(groupKey) Then
                expandedGroupKeys.Remove(groupKey)
            Else
                expandedGroupKeys.Add(groupKey)
            End If
        End If

        BindGroupedGrid(groupKey)
    End Sub

    Private Function GridRowType(row As DataGridViewRow) As String
        If row Is Nothing OrElse Not grid.Columns.Contains("_RowType") Then Return ""
        Return Convert.ToString(row.Cells("_RowType").Value).Trim()
    End Function

    Private Function IsExpandableGridRow(row As DataGridViewRow) As Boolean
        Dim rowType = GridRowType(row)
        Return String.Equals(rowType, "TR_GROUP", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(rowType, "EYE_GROUP", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function IsRecordGroupGridRow(row As DataGridViewRow) As Boolean
        If row Is Nothing OrElse Not grid.Columns.Contains("_RowType") Then Return False
        Return String.Equals(GridRowType(row), "EYE_GROUP", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Sub Grid_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return

        If IsExpandableGridRow(grid.Rows(e.RowIndex)) Then
            ToggleGroupAtRow(e.RowIndex)
            Return
        End If

        Try
            Dim recordId = Convert.ToString(grid.Rows(e.RowIndex).Cells("RecordId").Value)
            Dim measureId As String = ""
            If grid.Columns.Contains("MeasureId") Then
                measureId = Convert.ToString(grid.Rows(e.RowIndex).Cells("MeasureId").Value)
            End If

            If String.IsNullOrWhiteSpace(recordId) Then Return

            Using frm As New FrmMeasurementReview(recordId, measureId)
                frm.ShowDialog(Me)
            End Using
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Teknik resim görüntülenemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Grid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 Then Return

        Dim dataRow = grid.Rows(e.RowIndex)
        Dim rowType = GridRowType(dataRow)
        Dim isTrGroup = String.Equals(rowType, "TR_GROUP", StringComparison.OrdinalIgnoreCase)
        Dim isEyeGroup = String.Equals(rowType, "EYE_GROUP", StringComparison.OrdinalIgnoreCase)

        If isTrGroup Then
            dataRow.DefaultCellStyle.BackColor = Color.FromArgb(36, 78, 125)
            dataRow.DefaultCellStyle.ForeColor = Color.White
            dataRow.DefaultCellStyle.SelectionBackColor = Color.FromArgb(27, 60, 98)
            dataRow.DefaultCellStyle.SelectionForeColor = Color.White
            dataRow.DefaultCellStyle.Font = trGroupRowFont
            dataRow.Height = 36
            If grid.Columns(e.ColumnIndex).Name = "_Toggle" Then
                e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
                e.CellStyle.ForeColor = Color.White
            End If
            Return
        End If

        Dim defaultBack = If(isEyeGroup,
                             Color.FromArgb(226, 236, 248),
                             If(e.RowIndex Mod 2 = 0, Color.White, Color.FromArgb(248, 248, 248)))
        Dim rowBack = defaultBack

        If grid.Columns.Contains("VisualStatus") Then
            Dim visualText As String = Convert.ToString(grid.Rows(e.RowIndex).Cells("VisualStatus").Value)
            If String.Equals(visualText, "GÖZ KAPALI", StringComparison.OrdinalIgnoreCase) Then
                rowBack = If(isEyeGroup, Color.FromArgb(192, 211, 232), Color.LightSteelBlue)
            ElseIf String.Equals(visualText, "UYGUN DEĞİL", StringComparison.OrdinalIgnoreCase) OrElse String.Equals(visualText, "UYGUNSUZ", StringComparison.OrdinalIgnoreCase) Then
                rowBack = If(isEyeGroup, Color.FromArgb(248, 205, 202), Color.MistyRose)
            ElseIf String.Equals(visualText, "UYGUN", StringComparison.OrdinalIgnoreCase) Then
                rowBack = If(isEyeGroup, Color.FromArgb(210, 235, 216), Color.Honeydew)
            ElseIf String.Equals(visualText, "YOK", StringComparison.OrdinalIgnoreCase) Then
                rowBack = If(isEyeGroup, Color.FromArgb(226, 236, 248), Color.WhiteSmoke)
            End If
        End If

        If grid.Columns.Contains("Result") Then
            Dim resultText As String = Convert.ToString(grid.Rows(e.RowIndex).Cells("Result").Value)
            If String.Equals(resultText, "GÖZ KAPALI", StringComparison.OrdinalIgnoreCase) Then
                rowBack = If(isEyeGroup, Color.FromArgb(192, 211, 232), Color.LightSteelBlue)
            ElseIf String.Equals(resultText, "NOK", StringComparison.OrdinalIgnoreCase) Then
                rowBack = If(isEyeGroup, Color.FromArgb(248, 205, 202), Color.MistyRose)
            ElseIf String.Equals(resultText, "HATALI", StringComparison.OrdinalIgnoreCase) Then
                rowBack = If(isEyeGroup, Color.FromArgb(250, 235, 170), Color.LightYellow)
            End If
        End If

        dataRow.DefaultCellStyle.BackColor = rowBack
        dataRow.DefaultCellStyle.ForeColor = Color.FromArgb(35, 45, 58)
        dataRow.DefaultCellStyle.Font = If(isEyeGroup, groupRowFont, grid.Font)
        dataRow.Height = If(isEyeGroup, 32, 26)

        If grid.Columns(e.ColumnIndex).Name = "_Toggle" Then
            e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
            e.CellStyle.ForeColor = If(isEyeGroup, Color.FromArgb(25, 75, 130), Color.FromArgb(130, 140, 150))
        End If
    End Sub
End Class
