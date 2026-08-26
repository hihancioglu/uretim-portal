Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmPlasticShiftErrorReportEvaluatorAssignments
    Inherits Form

    Private ReadOnly rowsByPosition As New Dictionary(Of String, AssignmentEditorRow)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly lblInfo As New Label()

    Public Sub New()
        AuthorizationService.Require(AppState.IsAdmin, "Hata Raporu Değerlendirme Atamaları")
        AppIconService.Apply(Me)
        Text = "Hata Raporu Değerlendirme Atamaları"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(1120, 520)
        MinimumSize = New Size(900, 470)
        Font = New Font("Segoe UI", 9.0F)
        BackColor = Color.FromArgb(243, 247, 252)
        BuildScreen()
        LoadAssignments()
        ResponsiveFormService.Apply(Me)
    End Sub

    Private Sub BuildScreen()
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .Padding = New Padding(12),
            .BackColor = BackColor
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 54.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        Controls.Add(root)

        root.Controls.Add(New Label() With {
            .Dock = DockStyle.Fill,
            .Text = "Hata Raporu Değerlendirme Atamaları",
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(18, 0, 0, 0),
            .BackColor = Color.FromArgb(35, 82, 136),
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 14.0F, FontStyle.Bold)
        }, 0, 0)

        lblInfo.Dock = DockStyle.Fill
        lblInfo.Text = "Her yeni hata raporunda bu üç kişi değerlendirmeye atanır ve kendilerine e-posta gönderilir."
        lblInfo.TextAlign = ContentAlignment.MiddleLeft
        lblInfo.Padding = New Padding(14, 0, 14, 0)
        lblInfo.BackColor = Color.FromArgb(235, 243, 252)
        lblInfo.ForeColor = Color.FromArgb(25, 71, 124)
        lblInfo.Margin = New Padding(0, 6, 0, 6)
        root.Controls.Add(lblInfo, 0, 1)

        Dim table As New TableLayoutPanel() With {
            .Dock = DockStyle.Top,
            .AutoSize = True,
            .ColumnCount = 4,
            .RowCount = 4,
            .Padding = New Padding(14, 12, 14, 12),
            .BackColor = Color.White
        }
        table.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 32.0F))
        table.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 22.0F))
        table.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 21.0F))
        table.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25.0F))
        For index = 0 To 3
            table.RowStyles.Add(New RowStyle(SizeType.Absolute, If(index = 0, 38.0F, 58.0F)))
        Next
        AddHeader(table, "DEĞERLENDİRME POZİSYONU", 0)
        AddHeader(table, "GEREKLİ ROL", 1)
        AddHeader(table, "ATANACAK KULLANICI", 2)
        AddHeader(table, "E-POSTA", 3)

        Dim rowIndex = 1
        For Each positionKey In PlasticShiftErrorReportEvaluationPositions.AllKeys()
            Dim editor = New AssignmentEditorRow(positionKey)
            rowsByPosition(positionKey) = editor
            table.Controls.Add(MakeValueLabel(PlasticShiftErrorReportEvaluationPositions.PositionName(positionKey), True), 0, rowIndex)
            table.Controls.Add(MakeValueLabel(PlasticShiftErrorReportEvaluationPositions.RequiredRole(positionKey), False), 1, rowIndex)
            editor.UserCombo.Dock = DockStyle.Fill
            editor.UserCombo.DropDownStyle = ComboBoxStyle.DropDownList
            editor.UserCombo.Margin = New Padding(6, 12, 12, 10)
            table.Controls.Add(editor.UserCombo, 2, rowIndex)
            editor.EmailText.Dock = DockStyle.Fill
            editor.EmailText.Margin = New Padding(6, 12, 6, 10)
            editor.EmailText.BorderStyle = BorderStyle.FixedSingle
            table.Controls.Add(editor.EmailText, 3, rowIndex)
            rowIndex += 1
        Next
        root.Controls.Add(table, 0, 2)

        Dim footer As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False,
            .Padding = New Padding(0, 10, 4, 6),
            .BackColor = Color.White,
            .Margin = New Padding(0, 6, 0, 0)
        }
        Dim closeButton As New Button() With {.Text = "Kapat", .Width = 110, .Height = 36}
        Dim saveButton As New Button() With {
            .Text = "Atamaları Kaydet",
            .Width = 170,
            .Height = 36,
            .BackColor = Color.FromArgb(31, 92, 153),
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        }
        AddHandler closeButton.Click, Sub() Close()
        AddHandler saveButton.Click, AddressOf Save_Click
        footer.Controls.Add(closeButton)
        footer.Controls.Add(saveButton)
        root.Controls.Add(footer, 0, 3)
    End Sub

    Private Sub LoadAssignments()
        Dim users = UserService.GetUsers().
            Where(Function(row) String.Equals(DataService.GetValue(row, "IsActive"), "YES", StringComparison.OrdinalIgnoreCase)).
            ToList()
        Dim assignments = DataService.GetPlasticShiftErrorReportEvaluatorAssignments()
        For Each positionKey In PlasticShiftErrorReportEvaluationPositions.AllKeys()
            Dim editor = rowsByPosition(positionKey)
            Dim requiredRole = AppState.NormalizeRole(PlasticShiftErrorReportEvaluationPositions.RequiredRole(positionKey))
            Dim eligibleUsers = users.
                Where(Function(row) String.Equals(
                    AppState.NormalizeRole(DataService.GetValue(row, "Role")),
                    requiredRole,
                    StringComparison.OrdinalIgnoreCase)).
                Select(Function(row) DataService.GetValue(row, "Username")).
                Where(Function(value) value.Trim() <> "").
                OrderBy(Function(value) value, StringComparer.CurrentCultureIgnoreCase).
                ToArray()
            editor.UserCombo.Items.Clear()
            editor.UserCombo.Items.AddRange(eligibleUsers)
            Dim current = assignments.FirstOrDefault(
                Function(item) String.Equals(item.PositionKey, positionKey, StringComparison.OrdinalIgnoreCase))
            If current IsNot Nothing Then
                editor.UserCombo.SelectedItem = current.UserName
                editor.EmailText.Text = current.Email
            ElseIf editor.UserCombo.Items.Count > 0 Then
                editor.UserCombo.SelectedIndex = 0
            End If
        Next
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        Try
            Dim assignments As New List(Of PlasticShiftErrorReportEvaluatorAssignment)()
            For Each positionKey In PlasticShiftErrorReportEvaluationPositions.AllKeys()
                Dim editor = rowsByPosition(positionKey)
                assignments.Add(New PlasticShiftErrorReportEvaluatorAssignment With {
                    .PositionKey = positionKey,
                    .UserName = editor.UserCombo.Text.Trim(),
                    .Email = editor.EmailText.Text.Trim()
                })
            Next

            Dim unitUser = assignments.First(Function(item) item.PositionKey = PlasticShiftErrorReportEvaluationPositions.UnitManager).UserName
            Dim technicalUser = assignments.First(Function(item) item.PositionKey = PlasticShiftErrorReportEvaluationPositions.TechnicalProductionManager).UserName
            If unitUser <> "" AndAlso String.Equals(unitUser, technicalUser, StringComparison.OrdinalIgnoreCase) Then
                Dim answer = MessageBox.Show(
                    "İlgili Birim Amiri ile Teknik/Üretim Müdürü aynı kullanıcı seçildi. Bağımsız değerlendirme için farklı kişiler önerilir." &
                    Environment.NewLine & Environment.NewLine & "Yine de kaydetmek istiyor musunuz?",
                    "Aynı kullanıcı seçildi",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning)
                If answer <> DialogResult.Yes Then Return
            End If

            DataService.SavePlasticShiftErrorReportEvaluatorAssignments(assignments)
            lblInfo.Text = "Atamalar kaydedildi. Bundan sonra oluşturulan hata raporlarında bu kişiler kullanılacak."
            lblInfo.BackColor = Color.FromArgb(228, 245, 232)
            lblInfo.ForeColor = Color.FromArgb(18, 105, 48)
            MessageBox.Show("Üç değerlendirme ataması kaydedildi.", "Atamalar", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Atamalar kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Shared Sub AddHeader(table As TableLayoutPanel, text As String, column As Integer)
        table.Controls.Add(New Label() With {
            .Text = text,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(8, 0, 0, 0),
            .BackColor = Color.FromArgb(222, 233, 246),
            .ForeColor = Color.FromArgb(25, 51, 82),
            .Font = New Font("Segoe UI", 8.5F, FontStyle.Bold),
            .Margin = New Padding(1)
        }, column, 0)
    End Sub

    Private Shared Function MakeValueLabel(text As String, bold As Boolean) As Label
        Return New Label() With {
            .Text = text,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(8, 0, 8, 0),
            .Font = New Font("Segoe UI", 9.0F, If(bold, FontStyle.Bold, FontStyle.Regular)),
            .ForeColor = Color.FromArgb(35, 55, 80)
        }
    End Function

    Private NotInheritable Class AssignmentEditorRow
        Public Sub New(key As String)
            PositionKey = key
        End Sub

        Public ReadOnly Property PositionKey As String
        Public ReadOnly Property UserCombo As New ComboBox()
        Public ReadOnly Property EmailText As New TextBox()
    End Class
End Class
