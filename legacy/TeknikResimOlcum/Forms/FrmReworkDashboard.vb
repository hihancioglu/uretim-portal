Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Net
Imports System.Text.Json
Imports System.Windows.Forms
Imports Microsoft.Web.WebView2.Core
Imports Microsoft.Web.WebView2.WinForms

Public Class FrmReworkDashboard
    Inherits Form

    Private ReadOnly browser As New WebView2()
    Private ReadOnly uploadButton As New Button()
    Private ReadOnly refreshButton As New Button()
    Private ReadOnly reportButton As New Button()
    Private ReadOnly sourceLabel As New Label()
    Private browserReady As Boolean

    Public Sub New()
        AuthorizationService.Require(AppState.CanOpenReworkDashboard, "REWORK Dashboard")
        AppIconService.Apply(Me)

        Text = "REWORK DASHBOARD"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        MinimumSize = New Size(1100, 700)
        BackColor = Color.FromArgb(241, 246, 251)

        BuildLayout()
        AddHandler Shown, AddressOf FrmReworkDashboard_Shown
    End Sub

    Private Sub BuildLayout()
        Dim toolbar As New TableLayoutPanel With {
            .Dock = DockStyle.Top,
            .Height = 54,
            .ColumnCount = 5,
            .BackColor = Color.White,
            .Padding = New Padding(12, 9, 12, 8)
        }
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 150.0F))
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 110.0F))
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 110.0F))
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 110.0F))

        uploadButton.Text = "Excel Yükle"
        uploadButton.Dock = DockStyle.Fill
        uploadButton.BackColor = Color.FromArgb(22, 112, 72)
        uploadButton.ForeColor = Color.White
        uploadButton.FlatStyle = FlatStyle.Flat
        uploadButton.FlatAppearance.BorderSize = 0
        uploadButton.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        uploadButton.Visible = AppState.CanEditReworkDashboard
        AddHandler uploadButton.Click, AddressOf UploadButton_Click

        refreshButton.Text = "Yenile"
        refreshButton.Dock = DockStyle.Fill
        refreshButton.BackColor = Color.White
        refreshButton.ForeColor = Color.FromArgb(19, 64, 105)
        refreshButton.FlatStyle = FlatStyle.Flat
        refreshButton.FlatAppearance.BorderColor = Color.FromArgb(151, 181, 207)
        refreshButton.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        AddHandler refreshButton.Click, AddressOf RefreshButton_Click

        reportButton.Text = "RAPOR"
        reportButton.Dock = DockStyle.Fill
        reportButton.BackColor = Color.FromArgb(31, 78, 121)
        reportButton.ForeColor = Color.White
        reportButton.FlatStyle = FlatStyle.Flat
        reportButton.FlatAppearance.BorderSize = 0
        reportButton.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        AddHandler reportButton.Click, AddressOf ReportButton_Click

        sourceLabel.Dock = DockStyle.Fill
        sourceLabel.TextAlign = ContentAlignment.MiddleLeft
        sourceLabel.Padding = New Padding(14, 0, 8, 0)
        sourceLabel.ForeColor = Color.FromArgb(65, 90, 115)
        sourceLabel.Font = New Font("Segoe UI", 8.5F, FontStyle.Regular)
        sourceLabel.AutoEllipsis = True
        sourceLabel.Text = "Henüz REWORK Excel dosyası yüklenmedi."

        Dim titleLabel As New Label With {
            .Text = "REWORK DASHBOARD",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleRight,
            .ForeColor = Color.FromArgb(19, 64, 105),
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        }

        toolbar.Controls.Add(uploadButton, 0, 0)
        toolbar.Controls.Add(refreshButton, 1, 0)
        toolbar.Controls.Add(reportButton, 2, 0)
        toolbar.Controls.Add(sourceLabel, 3, 0)
        toolbar.Controls.Add(titleLabel, 4, 0)

        browser.Dock = DockStyle.Fill
        Controls.Add(browser)
        Controls.Add(toolbar)
    End Sub

    Private Async Sub FrmReworkDashboard_Shown(sender As Object, e As EventArgs)
        Await InitializeBrowserAsync()
    End Sub

    Private Async Function InitializeBrowserAsync() As Threading.Tasks.Task
        Try
            Dim userDataFolder = Path.Combine(AppPaths.LocalAppDataRoot, "WebView2", "ReworkDashboard")
            Directory.CreateDirectory(userDataFolder)
            Dim environment = Await CoreWebView2Environment.CreateAsync(Nothing, userDataFolder)
            Await browser.EnsureCoreWebView2Async(environment)
            browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = True
            browser.CoreWebView2.Settings.AreDevToolsEnabled = AppState.IsAdmin
            AddHandler browser.CoreWebView2.NavigationCompleted, AddressOf Browser_NavigationCompleted

            Dim htmlPath = AppPaths.ReworkDashboardHtmlPath
            If Not File.Exists(htmlPath) Then
                browser.NavigateToString(BuildMessageHtml("REWORK dashboard dosyası bulunamadı: " & htmlPath))
                Return
            End If

            browser.CoreWebView2.Navigate(New Uri(htmlPath).AbsoluteUri)
        Catch ex As Exception
            ErrorLogService.Log("FrmReworkDashboard.InitializeBrowserAsync", ex)
            browser.NavigateToString(BuildMessageHtml("REWORK Dashboard açılamadı: " & ex.Message))
        End Try
    End Function

    Private Sub Browser_NavigationCompleted(sender As Object, e As CoreWebView2NavigationCompletedEventArgs)
        If Not e.IsSuccess Then Return
        browserReady = True
        RefreshDashboard()
    End Sub

    Private Sub UploadButton_Click(sender As Object, e As EventArgs)
        Using dialog As New OpenFileDialog With {
            .Title = "REWORK Excel Dosyası Seçin",
            .Filter = "Excel dosyası (*.xlsx;*.xlsm)|*.xlsx;*.xlsm",
            .Multiselect = False,
            .CheckFileExists = True
        }
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return

            uploadButton.Enabled = False
            Try
                Dim result = ReworkDashboardService.ImportAndPersist(dialog.FileName)
                If Not result.IsSuccess Then
                    MessageBox.Show(result.StatusMessage, "REWORK Excel Yükleme", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return
                End If

                MessageBox.Show(result.StatusMessage, "REWORK Excel Yükleme", MessageBoxButtons.OK, MessageBoxIcon.Information)
                RefreshDashboard(result)
            Catch ex As Exception
                ErrorLogService.Log("FrmReworkDashboard.UploadButton_Click", ex)
                MessageBox.Show("REWORK Excel dosyası yüklenemedi: " & ex.Message,
                                "REWORK Excel Yükleme",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error)
            Finally
                uploadButton.Enabled = True
            End Try
        End Using
    End Sub

    Private Sub RefreshButton_Click(sender As Object, e As EventArgs)
        RefreshDashboard()
    End Sub

    Private Sub ReportButton_Click(sender As Object, e As EventArgs)
        Dim result = ReworkDashboardService.LoadCurrent()
        If Not result.IsSuccess OrElse result.Records Is Nothing OrElse result.Records.Count = 0 Then
            MessageBox.Show(
                If(result.StatusMessage, "Raporlanacak REWORK kaydı bulunamadı."),
                "REWORK Dashboard Raporu",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information)
            Return
        End If

        Using dialog As New SaveFileDialog With {
            .Title = "REWORK Dashboard HTML Raporunu Kaydet",
            .Filter = "HTML dosyası (*.html)|*.html",
            .DefaultExt = "html",
            .AddExtension = True,
            .FileName = "REWORK_DASHBOARD_Raporu_" & DateTime.Now.ToString("yyyyMMdd_HHmm") & ".html"
        }
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return

            Try
                reportButton.Enabled = False
                Cursor = Cursors.WaitCursor
                ReworkDashboardReportService.CreateReport(dialog.FileName, result)

                Dim openResult = MessageBox.Show(
                    "REWORK Dashboard HTML raporu oluşturuldu." & Environment.NewLine &
                    dialog.FileName & Environment.NewLine & Environment.NewLine &
                    "Rapor şimdi açılsın mı?",
                    "REWORK Dashboard Raporu",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information)
                If openResult = DialogResult.Yes Then
                    Process.Start(New ProcessStartInfo(dialog.FileName) With {.UseShellExecute = True})
                End If
            Catch ex As Exception
                ErrorLogService.Log("FrmReworkDashboard.ReportButton_Click", ex)
                MessageBox.Show(
                    "REWORK Dashboard HTML raporu oluşturulamadı: " & ex.Message,
                    "REWORK Dashboard Raporu",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error)
            Finally
                reportButton.Enabled = True
                Cursor = Cursors.Default
            End Try
        End Using
    End Sub

    Private Sub RefreshDashboard(Optional result As ReworkImportResult = Nothing)
        If Not browserReady OrElse browser.CoreWebView2 Is Nothing Then Return
        If result Is Nothing Then result = ReworkDashboardService.LoadCurrent()

        Dim state = result.State
        If state IsNot Nothing Then
            sourceLabel.Text = state.OriginalFileName & "  |  " & state.RecordCount.ToString("N0") &
                " kayıt  |  " & state.ImportedAt & "  |  " & state.ImportedBy
        Else
            sourceLabel.Text = result.StatusMessage
        End If

        Dim recordPayloads As New List(Of Dictionary(Of String, Object))()
        For Each record In result.Records
            recordPayloads.Add(New Dictionary(Of String, Object) From {
                {"operationDate", record.OperationDate.ToString("yyyy-MM-dd")},
                {"workCenter", record.WorkCenter},
                {"workCenterDescription", record.WorkCenterDescription},
                {"tourTemplate", record.TourTemplate},
                {"materialDescription", record.MaterialDescription},
                {"completedQuantity", record.CompletedQuantity},
                {"operationDescription", record.OperationDescription},
                {"sourceSheet", record.SourceSheet},
                {"sourceRowNumber", record.SourceRowNumber}
            })
        Next

        Dim payload As New Dictionary(Of String, Object) From {
            {"type", "rework-dashboard-data"},
            {"isSuccess", result.IsSuccess},
            {"statusMessage", result.StatusMessage},
            {"state", state},
            {"records", recordPayloads}
        }

        Dim options As New JsonSerializerOptions With {.PropertyNamingPolicy = JsonNamingPolicy.CamelCase}
        browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload, options))
    End Sub

    Private Shared Function BuildMessageHtml(message As String) As String
        Return "<!doctype html><html><head><meta charset='utf-8'><style>" &
            "body{font-family:Segoe UI,sans-serif;background:#f1f6fb;color:#123f67;padding:32px}" &
            ".box{background:#fff;border:1px solid #c9dbea;border-radius:10px;padding:24px}" &
            "</style></head><body><div class='box'><b>REWORK Dashboard</b><p>" &
            WebUtility.HtmlEncode(If(message, "")) & "</p></div></body></html>"
    End Function
End Class
