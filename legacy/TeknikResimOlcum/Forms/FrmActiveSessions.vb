Imports System.Data
Imports System.Drawing
Imports System.Windows.Forms

Public Class FrmActiveSessions
    Inherits Form

    Private ReadOnly grid As New DataGridView()
    Private ReadOnly lblCount As New Label()
    Private ReadOnly btnEndSelected As New Button()
    Private ReadOnly refreshTimer As New Timer()

    Public Sub New()
        AuthorizationService.Require(AppState.IsAdmin, "Açık Oturumlar")
        AppIconService.Apply(Me)
        Text = "Açık Oturumlar"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(1000, 560)
        MinimumSize = New Size(760, 420)
        BackColor = Color.White

        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .BackColor = Color.White
        }
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 52.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        Controls.Add(layout)

        Dim header As New Panel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(12, 10, 12, 6),
            .BackColor = Color.WhiteSmoke
        }
        lblCount.Dock = DockStyle.Fill
        lblCount.Font = New Font("Segoe UI", 10.0F, FontStyle.Bold)
        lblCount.TextAlign = ContentAlignment.MiddleLeft
        header.Controls.Add(lblCount)
        layout.Controls.Add(header, 0, 0)

        ConfigureGrid()
        layout.Controls.Add(grid, 0, 1)

        Dim buttons As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False,
            .Padding = New Padding(10),
            .BackColor = Color.WhiteSmoke
        }
        Dim btnClose As New Button() With {.Text = "Kapat", .Width = 100, .Height = 32}
        AddHandler btnClose.Click, Sub() Close()

        Dim btnRefresh As New Button() With {.Text = "Yenile", .Width = 100, .Height = 32}
        AddHandler btnRefresh.Click, Sub() LoadSessions()

        Dim btnRunningPrograms As New Button() With {
            .Text = "Program Açık Bilgisayarlar",
            .Width = 205,
            .Height = 32,
            .ForeColor = Color.FromArgb(25, 58, 100)
        }
        AddHandler btnRunningPrograms.Click,
            Sub()
                Using form As New FrmRunningInstances()
                    form.ShowDialog(Me)
                End Using
            End Sub

        Dim btnEndAll As New Button() With {
            .Text = "Tüm Diğer Oturumları Kapat",
            .Width = 215,
            .Height = 32,
            .ForeColor = Color.DarkRed
        }
        AddHandler btnEndAll.Click, AddressOf EndAllOtherSessions_Click

        btnEndSelected.Text = "Seçili Oturumu Kapat"
        btnEndSelected.Width = 175
        btnEndSelected.Height = 32
        btnEndSelected.ForeColor = Color.DarkRed
        AddHandler btnEndSelected.Click, AddressOf EndSelectedSession_Click

        buttons.Controls.AddRange({btnClose, btnRefresh, btnRunningPrograms, btnEndAll, btnEndSelected})
        layout.Controls.Add(buttons, 0, 2)

        refreshTimer.Interval = 10000
        AddHandler refreshTimer.Tick, Sub() LoadSessions()
        AddHandler Shown,
            Sub()
                LoadSessions()
                refreshTimer.Start()
            End Sub
        AddHandler FormClosed, Sub() refreshTimer.Stop()
    End Sub

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.MultiSelect = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.RowHeadersVisible = False
        grid.AutoGenerateColumns = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.GridColor = Color.Gainsboro
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 235, 235)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
        grid.RowTemplate.Height = 28

        grid.Columns.Add(MakeColumn("CurrentText", "Durum", 180, 16))
        grid.Columns.Add(MakeColumn("Username", "Kullanıcı", 130, 14))
        grid.Columns.Add(MakeColumn("Role", "Rol", 170, 18))
        grid.Columns.Add(MakeColumn("ComputerName", "Bilgisayar", 150, 16))
        grid.Columns.Add(MakeColumn("LoginAt", "Giriş Zamanı", 145, 15))
        grid.Columns.Add(MakeColumn("LastSeen", "Son Görülme", 145, 15))
        Dim sessionColumn = MakeColumn("SessionId", "Oturum No", 180, 13)
        sessionColumn.Visible = False
        grid.Columns.Add(sessionColumn)

        AddHandler grid.SelectionChanged, AddressOf UpdateButtonState
    End Sub

    Private Shared Function MakeColumn(name As String, header As String, width As Integer, fillWeight As Single) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .DataPropertyName = name,
            .HeaderText = header,
            .Width = width,
            .MinimumWidth = 70,
            .FillWeight = fillWeight,
            .SortMode = DataGridViewColumnSortMode.Automatic
        }
    End Function

    Private Sub LoadSessions()
        Try
            Dim sessions = UserService.GetActiveSessionsByAdmin()
            Dim retainedSessionId As String = SelectedSessionId()

            Dim table As New DataTable()
            For Each columnName In {"CurrentText", "Username", "Role", "ComputerName", "LoginAt", "LastSeen", "SessionId", "IsCurrent", "IsStale"}
                table.Columns.Add(columnName)
            Next

            For Each session In sessions
                Dim row = table.NewRow()
                Dim isCurrent = String.Equals(DataService.GetValue(session, "IsCurrent"), "YES", StringComparison.OrdinalIgnoreCase)
                Dim isStale = String.Equals(DataService.GetValue(session, "IsStale"), "YES", StringComparison.OrdinalIgnoreCase)
                Dim isAdmin = String.Equals(
                    AppState.NormalizeRole(DataService.GetValue(session, "Role")),
                    AppState.RoleAdmin,
                    StringComparison.OrdinalIgnoreCase)
                row("CurrentText") =
                    If(isCurrent,
                       "BU OTURUM",
                       If(isAdmin,
                          "KORUMALI",
                          If(isStale, "10+ DK GÜNCELLENMEDİ", "AÇIK")))
                row("Username") = DataService.GetValue(session, "Username")
                row("Role") = DataService.GetValue(session, "Role")
                row("ComputerName") = DataService.GetValue(session, "ComputerName")
                row("LoginAt") = DataService.GetValue(session, "LoginAt")
                row("LastSeen") = DataService.GetValue(session, "LastSeen")
                row("SessionId") = DataService.GetValue(session, "SessionId")
                row("IsCurrent") = If(isCurrent, "YES", "NO")
                row("IsStale") = If(isStale, "YES", "NO")
                table.Rows.Add(row)
            Next

            grid.DataSource = table
            lblCount.Text = $"Oturum kayıtları: {table.Rows.Count} adet | Liste 5 saniyede bir yenilenir."

            For Each row As DataGridViewRow In grid.Rows
                Dim isCurrent = String.Equals(Convert.ToString(DirectCast(row.DataBoundItem, DataRowView)("IsCurrent")), "YES", StringComparison.OrdinalIgnoreCase)
                If isCurrent Then
                    row.DefaultCellStyle.BackColor = Color.Honeydew
                    row.DefaultCellStyle.ForeColor = Color.DarkGreen
                ElseIf String.Equals(
                    AppState.NormalizeRole(Convert.ToString(row.Cells("Role").Value)),
                    AppState.RoleAdmin,
                    StringComparison.OrdinalIgnoreCase) Then
                    row.DefaultCellStyle.BackColor = Color.AliceBlue
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(25, 58, 100)
                ElseIf String.Equals(
                    Convert.ToString(DirectCast(row.DataBoundItem, DataRowView)("IsStale")),
                    "YES",
                    StringComparison.OrdinalIgnoreCase) Then
                    row.DefaultCellStyle.BackColor = Color.LemonChiffon
                    row.DefaultCellStyle.ForeColor = Color.DarkGoldenrod
                End If

                If retainedSessionId <> "" AndAlso
                   String.Equals(Convert.ToString(row.Cells("SessionId").Value), retainedSessionId, StringComparison.OrdinalIgnoreCase) Then
                    row.Selected = True
                    grid.CurrentCell = row.Cells("Username")
                End If
            Next
            UpdateButtonState(Me, EventArgs.Empty)
        Catch ex As Exception
            ErrorLogService.Log("FrmActiveSessions.LoadSessions", ex)
            lblCount.Text = "Açık oturumlar yüklenemedi."
        End Try
    End Sub

    Private Function SelectedSessionId() As String
        If grid.CurrentRow Is Nothing OrElse Not grid.Columns.Contains("SessionId") Then Return ""
        Return Convert.ToString(grid.CurrentRow.Cells("SessionId").Value).Trim()
    End Function

    Private Function SelectedIsCurrent() As Boolean
        If grid.CurrentRow Is Nothing Then Return False
        Dim view = TryCast(grid.CurrentRow.DataBoundItem, DataRowView)
        Return view IsNot Nothing AndAlso String.Equals(Convert.ToString(view("IsCurrent")), "YES", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function SelectedIsAdmin() As Boolean
        If grid.CurrentRow Is Nothing OrElse Not grid.Columns.Contains("Role") Then Return False
        Return String.Equals(
            AppState.NormalizeRole(Convert.ToString(grid.CurrentRow.Cells("Role").Value)),
            AppState.RoleAdmin,
            StringComparison.OrdinalIgnoreCase)
    End Function

    Private Sub UpdateButtonState(sender As Object, e As EventArgs)
        btnEndSelected.Enabled =
            SelectedSessionId() <> "" AndAlso
            Not SelectedIsCurrent() AndAlso
            Not SelectedIsAdmin()
    End Sub

    Private Sub EndSelectedSession_Click(sender As Object, e As EventArgs)
        Try
            Dim sessionId = SelectedSessionId()
            If sessionId = "" OrElse grid.CurrentRow Is Nothing Then Return
            If SelectedIsAdmin() Then
                MessageBox.Show(
                    "Admin oturumları başka bir Admin tarafından kapatılamaz.",
                    "Admin oturumu korumalı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information)
                Return
            End If

            Dim username = Convert.ToString(grid.CurrentRow.Cells("Username").Value)
            Dim computer = Convert.ToString(grid.CurrentRow.Cells("ComputerName").Value)
            If MessageBox.Show(
                username & " kullanıcısının " & computer & " bilgisayarındaki oturumu kapatılacak." & Environment.NewLine &
                "O bilgisayardaki program da otomatik kapanacaktır." & Environment.NewLine & Environment.NewLine &
                "Devam edilsin mi?",
                "Oturumu Kapat",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then Return

            UserService.EndSessionByAdmin(sessionId)
            LoadSessions()
            MessageBox.Show(
                "Oturum kapatıldı. İlgili bilgisayardaki program yaklaşık 5 saniye içinde kapanacaktır.",
                "Oturum Kapatıldı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information)
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Oturum kapatılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub EndAllOtherSessions_Click(sender As Object, e As EventArgs)
        Try
            If MessageBox.Show(
                "Tüm Admin oturumları açık kalacak; yalnızca Admin olmayan diğer oturumlar kapatılacak." & Environment.NewLine &
                "Bu oturumların açık olduğu bilgisayarlardaki programlar da otomatik kapanacaktır." & Environment.NewLine & Environment.NewLine &
                "Devam edilsin mi?",
                "Tüm Diğer Oturumları Kapat",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then Return

            Dim closedCount = UserService.EndAllOtherSessionsByAdmin()
            LoadSessions()
            MessageBox.Show(
                closedCount.ToString() & " oturum kapatıldı. İlgili programlar yaklaşık 5 saniye içinde kapanacaktır.",
                "Oturumlar Kapatıldı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information)
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Oturumlar kapatılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub
End Class
