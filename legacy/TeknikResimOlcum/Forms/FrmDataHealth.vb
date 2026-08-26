Imports System.Data
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Windows.Forms

Public Class FrmDataHealth
    Inherits Form

    Private Class CsvDefinition
        Public Property DisplayName As String = ""
        Public Property FilePathFactory As Func(Of String)
        Public Property Headers As String() = Array.Empty(Of String)()
        Public Property IgnoreTempRisk As Boolean
        Public Property AutoCleanStaleLock As Boolean
        Public ReadOnly Property FilePath As String
            Get
                If FilePathFactory Is Nothing Then Return ""
                Return FilePathFactory.Invoke()
            End Get
        End Property
    End Class

    Private Class CsvCandidate
        Public Property Kind As String = ""
        Public Property FilePath As String = ""
        Public Property IsValid As Boolean
        Public Property DataRowCount As Integer = -1
        Public Property Length As Long
        Public Property LastWriteUtc As DateTime = DateTime.MinValue
        Public ReadOnly Property LastWriteText As String
            Get
                If LastWriteUtc = DateTime.MinValue Then Return ""
                Return LastWriteUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.CurrentCulture)
            End Get
        End Property
    End Class

    Private ReadOnly grid As New DataGridView()
    Private ReadOnly lblSummary As New Label()
    Private ReadOnly lblDetail As New Label()
    Private ReadOnly btnRefresh As New Button()
    Private ReadOnly btnRecover As New Button()
    Private ReadOnly btnCleanTmp As New Button()
    Private ReadOnly btnJournal As New Button()
    Private currentTable As DataTable

    Public Sub New()
        AuthorizationService.Require(AppState.IsAdmin, "Veri Sağlığı")

        AppIconService.Apply(Me)
        Text = "Veri Sağlığı / CSV Kurtarma"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        MinimumSize = New Size(980, 620)
        Size = New Size(1280, 760)

        BuildScreen()
        LoadGrid()
    End Sub

    Private Sub BuildScreen()
        BackColor = Color.FromArgb(246, 248, 251)

        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 5,
            .Padding = New Padding(8),
            .Margin = New Padding(0)
        }
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 56.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 44.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 90.0F))
        Controls.Add(root)

        Dim header As New Panel() With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(35, 78, 130),
            .Margin = New Padding(0, 0, 0, 8),
            .Padding = New Padding(22, 8, 22, 8)
        }
        Dim lblTitle As New Label() With {
            .Dock = DockStyle.Fill,
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 14.0F, FontStyle.Bold),
            .Text = "Veri Sağlığı / CSV Kurtarma" & Environment.NewLine &
                    "CSV kilitleri, tmp dosyaları, yedekler ve veri satırları admin tarafından izlenir.",
            .TextAlign = ContentAlignment.MiddleLeft
        }
        header.Controls.Add(lblTitle)
        root.Controls.Add(header, 0, 0)

        lblSummary.Dock = DockStyle.Fill
        lblSummary.Margin = New Padding(0, 0, 0, 8)
        lblSummary.Padding = New Padding(14, 0, 14, 0)
        lblSummary.BackColor = Color.FromArgb(232, 240, 252)
        lblSummary.ForeColor = Color.FromArgb(0, 47, 94)
        lblSummary.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblSummary.TextAlign = ContentAlignment.MiddleLeft
        root.Controls.Add(lblSummary, 0, 1)

        Dim actions As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .AutoScroll = True,
            .Padding = New Padding(8, 6, 8, 6),
            .Margin = New Padding(0, 0, 0, 8),
            .BackColor = Color.White
        }

        btnRefresh.Text = "Yenile"
        btnRefresh.Width = 130
        btnRefresh.Height = 30
        btnRefresh.Margin = New Padding(0, 0, 8, 0)
        AddHandler btnRefresh.Click, Sub() LoadGrid()
        actions.Controls.Add(btnRefresh)

        btnRecover.Text = "Seçili Dosyayı Yedekten Kurtar"
        btnRecover.Width = 230
        btnRecover.Height = 30
        btnRecover.BackColor = Color.FromArgb(255, 248, 220)
        btnRecover.FlatStyle = FlatStyle.Flat
        btnRecover.Margin = New Padding(0, 0, 8, 0)
        AddHandler btnRecover.Click, Sub() RecoverSelected()
        actions.Controls.Add(btnRecover)

        btnCleanTmp.Text = "Eski Tmp'leri Temizle"
        btnCleanTmp.Width = 170
        btnCleanTmp.Height = 30
        btnCleanTmp.BackColor = Color.FromArgb(232, 240, 252)
        btnCleanTmp.FlatStyle = FlatStyle.Flat
        btnCleanTmp.Margin = New Padding(0, 0, 8, 0)
        AddHandler btnCleanTmp.Click, Sub() CleanSelectedOldTemps()
        actions.Controls.Add(btnCleanTmp)

        btnJournal.Text = "Veri Hareketleri"
        btnJournal.Width = 150
        btnJournal.Height = 30
        btnJournal.BackColor = Color.FromArgb(220, 237, 255)
        btnJournal.FlatStyle = FlatStyle.Flat
        btnJournal.Margin = New Padding(0, 0, 8, 0)
        AddHandler btnJournal.Click,
            Sub()
                Using frm As New FrmDataJournal()
                    frm.ShowDialog(Me)
                End Using
            End Sub
        actions.Controls.Add(btnJournal)

        Dim info As New Label() With {
            .AutoSize = True,
            .Height = 30,
            .Padding = New Padding(12, 7, 0, 0),
            .Text = "Eski tmp dosyaları tek başına uyarı sayılmaz. Sarı uyarı yalnızca riskli tmp veya eski lock varsa verilir.",
            .ForeColor = Color.FromArgb(70, 82, 100)
        }
        actions.Controls.Add(info)
        root.Controls.Add(actions, 0, 2)

        grid.Dock = DockStyle.Fill
        grid.Margin = New Padding(0)
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.ReadOnly = True
        grid.MultiSelect = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        grid.RowHeadersVisible = False
        grid.BackgroundColor = Color.White
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(219, 231, 245)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(0, 32, 64)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        AddHandler grid.SelectionChanged, Sub() UpdateDetail()
        AddHandler grid.CellFormatting, AddressOf Grid_CellFormatting
        root.Controls.Add(grid, 0, 3)

        lblDetail.Dock = DockStyle.Fill
        lblDetail.Margin = New Padding(0, 8, 0, 0)
        lblDetail.Padding = New Padding(12)
        lblDetail.BackColor = Color.White
        lblDetail.ForeColor = Color.FromArgb(28, 43, 63)
        lblDetail.Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
        lblDetail.TextAlign = ContentAlignment.TopLeft
        root.Controls.Add(lblDetail, 0, 4)
    End Sub

    Private Sub LoadGrid()
        Dim table As New DataTable()
        table.Columns.Add("Durum")
        table.Columns.Add("Dosya")
        table.Columns.Add("Satır", GetType(Integer))
        table.Columns.Add("Boyut")
        table.Columns.Add("Son Güncelleme")
        table.Columns.Add("Lock")
        table.Columns.Add("Tmp")
        table.Columns.Add("Riskli Tmp")
        table.Columns.Add("Bak")
        table.Columns.Add("Recovery")
        table.Columns.Add("En İyi Yedek")
        table.Columns.Add("Yol")
        table.Columns.Add("_Path")
        table.Columns.Add("_BestBackup")
        table.Columns.Add("_SafeTmpPaths")

        Dim total As Integer = 0
        Dim healthy As Integer = 0
        Dim warning As Integer = 0
        Dim danger As Integer = 0

        For Each definition In GetCsvDefinitions()
            total += 1
            Dim health = InspectFile(definition)
            Dim dr = table.NewRow()
            dr("Durum") = health.Status
            dr("Dosya") = definition.DisplayName
            dr("Satır") = Math.Max(0, health.DataRowCount)
            dr("Boyut") = FormatBytes(health.Length)
            dr("Son Güncelleme") = health.LastWriteText
            dr("Lock") = If(health.HasLock, If(health.HasStaleLock, "ESKİ", "VAR"), "")
            dr("Tmp") = If(health.TempCount > 0, health.TempCount.ToString(), "")
            dr("Riskli Tmp") = If(health.RiskyTempCount > 0, health.RiskyTempCount.ToString(), "")
            dr("Bak") = If(health.HasBackup, "VAR", "")
            dr("Recovery") = If(health.RecoveryCount > 0, health.RecoveryCount.ToString(), "")
            dr("En İyi Yedek") = If(health.BestCandidate Is Nothing, "", health.BestCandidate.DataRowCount & " satır / " & health.BestCandidate.LastWriteText)
            dr("Yol") = health.FilePath
            dr("_Path") = health.FilePath
            dr("_BestBackup") = If(health.BestCandidate Is Nothing, "", health.BestCandidate.FilePath)
            dr("_SafeTmpPaths") = String.Join(ControlChars.Tab, health.SafeTempPaths)
            table.Rows.Add(dr)

            Select Case health.Severity
                Case 0
                    healthy += 1
                Case 1
                    warning += 1
                Case Else
                    danger += 1
            End Select
        Next

        currentTable = table
        grid.DataSource = table
        If grid.Columns.Contains("_Path") Then grid.Columns("_Path").Visible = False
        If grid.Columns.Contains("_BestBackup") Then grid.Columns("_BestBackup").Visible = False
        If grid.Columns.Contains("_SafeTmpPaths") Then grid.Columns("_SafeTmpPaths").Visible = False

        If grid.Columns.Contains("Durum") Then grid.Columns("Durum").FillWeight = 80
        If grid.Columns.Contains("Dosya") Then grid.Columns("Dosya").FillWeight = 135
        If grid.Columns.Contains("Satır") Then grid.Columns("Satır").FillWeight = 55
        If grid.Columns.Contains("Boyut") Then grid.Columns("Boyut").FillWeight = 70
        If grid.Columns.Contains("Son Güncelleme") Then grid.Columns("Son Güncelleme").FillWeight = 110
        If grid.Columns.Contains("Riskli Tmp") Then grid.Columns("Riskli Tmp").FillWeight = 70
        If grid.Columns.Contains("Yol") Then grid.Columns("Yol").FillWeight = 220

        lblSummary.Text = "CSV dosyaları: " & total &
                          "   |   Sağlıklı: " & healthy &
                          "   |   Uyarı: " & warning &
                          "   |   Kritik: " & danger &
                          "   |   Veri klasörü: " & AppPaths.DataDir
        UpdateDetail()
    End Sub

    Private Class CsvHealth
        Public Property Status As String = ""
        Public Property Severity As Integer
        Public Property FilePath As String = ""
        Public Property Length As Long
        Public Property DataRowCount As Integer = -1
        Public Property LastWriteUtc As DateTime = DateTime.MinValue
        Public Property HasLock As Boolean
        Public Property HasStaleLock As Boolean
        Public Property TempCount As Integer
        Public Property RiskyTempCount As Integer
        Public Property HasBackup As Boolean
        Public Property RecoveryCount As Integer
        Public Property BestCandidate As CsvCandidate
        Public Property SafeTempPaths As New List(Of String)()
        Public ReadOnly Property LastWriteText As String
            Get
                If LastWriteUtc = DateTime.MinValue Then Return ""
                Return LastWriteUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.CurrentCulture)
            End Get
        End Property
    End Class

    Private Function InspectFile(definition As CsvDefinition) As CsvHealth
        Dim health As New CsvHealth With {.FilePath = definition.FilePath}

        Try
            Dim lockPath = definition.FilePath & ".lock"
            health.HasLock = File.Exists(lockPath)
            health.HasStaleLock = IsStaleLock(lockPath)

            If health.HasStaleLock AndAlso definition.AutoCleanStaleLock AndAlso TryDeleteStaleLock(lockPath) Then
                health.HasLock = File.Exists(lockPath)
                health.HasStaleLock = False
            End If

            Dim candidates = GetRecoveryCandidates(definition)
            Dim tempCandidates = candidates.Where(Function(c) String.Equals(c.Kind, "TMP", StringComparison.OrdinalIgnoreCase)).ToList()
            health.TempCount = tempCandidates.Count()
            health.HasBackup = candidates.Any(Function(c) String.Equals(c.Kind, "BAK", StringComparison.OrdinalIgnoreCase))
            health.RecoveryCount = candidates.Where(Function(c) String.Equals(c.Kind, "RECOVERY", StringComparison.OrdinalIgnoreCase)).Count()
            health.BestCandidate = candidates.
                Where(Function(c) c.IsValid).
                OrderByDescending(Function(c) c.DataRowCount).
                ThenByDescending(Function(c) c.LastWriteUtc).
                FirstOrDefault()

            Dim current = InspectCandidate("ANA", definition.FilePath, definition.Headers)
            health.Length = current.Length
            health.DataRowCount = current.DataRowCount
            health.LastWriteUtc = current.LastWriteUtc

            If definition.IgnoreTempRisk Then
                health.RiskyTempCount = 0
            Else
                health.RiskyTempCount = tempCandidates.Where(Function(c) IsRiskyTempCandidate(c, current)).Count()
            End If
            health.SafeTempPaths = tempCandidates.
                Where(Function(c) definition.IgnoreTempRisk OrElse Not IsRiskyTempCandidate(c, current)).
                Select(Function(c) c.FilePath).
                Where(Function(path) Not String.IsNullOrWhiteSpace(path)).
                ToList()

            If Not File.Exists(definition.FilePath) Then
                health.Status = If(health.BestCandidate Is Nothing, "EKSİK", "KURTARMA VAR")
                health.Severity = If(health.BestCandidate Is Nothing, 2, 1)
            ElseIf current.Length <= 0 Then
                health.Status = If(health.BestCandidate Is Nothing, "BOŞ", "KURTARMA VAR")
                health.Severity = 2
            ElseIf Not current.IsValid Then
                health.Status = If(health.BestCandidate Is Nothing, "HEADER SORUNU", "KURTARMA VAR")
                health.Severity = 2
            ElseIf health.RiskyTempCount > 0 Then
                health.Status = "TMP RİSKİ"
                health.Severity = 1
            ElseIf health.HasStaleLock Then
                health.Status = "ESKİ LOCK"
                health.Severity = 1
            Else
                health.Status = "SAĞLIKLI"
                health.Severity = 0
            End If
        Catch ex As Exception
            ErrorLogService.Log("FrmDataHealth.InspectFile", ex, "Path=" & definition.FilePath)
            health.Status = "OKUNAMADI"
            health.Severity = 2
        End Try

        Return health
    End Function

    Private Shared Function IsStaleLock(lockPath As String) As Boolean
        Try
            If String.IsNullOrWhiteSpace(lockPath) OrElse Not File.Exists(lockPath) Then Return False
            Dim lastWriteUtc = File.GetLastWriteTimeUtc(lockPath)
            If lastWriteUtc = DateTime.MinValue Then Return True
            Return (DateTime.UtcNow - lastWriteUtc).TotalMinutes >= 5.0R
        Catch ex As Exception
            ErrorLogService.Log("FrmDataHealth.IsStaleLock", ex, "Path=" & lockPath)
            Return True
        End Try
    End Function

    Private Shared Function TryDeleteStaleLock(lockPath As String) As Boolean
        Try
            If String.IsNullOrWhiteSpace(lockPath) OrElse Not File.Exists(lockPath) Then Return True
            File.Delete(lockPath)
            Return Not File.Exists(lockPath)
        Catch ex As Exception
            ErrorLogService.Log("FrmDataHealth.TryDeleteStaleLock", ex, "Path=" & lockPath)
            Return False
        End Try
    End Function

    Private Shared Function IsRiskyTempCandidate(candidate As CsvCandidate, current As CsvCandidate) As Boolean
        If candidate Is Nothing OrElse Not candidate.IsValid Then Return False

        If current Is Nothing OrElse Not current.IsValid OrElse current.Length <= 0 Then
            Return True
        End If

        If candidate.DataRowCount > Math.Max(0, current.DataRowCount) Then
            Return True
        End If

        If candidate.LastWriteUtc <> DateTime.MinValue AndAlso
           current.LastWriteUtc <> DateTime.MinValue AndAlso
           candidate.LastWriteUtc > current.LastWriteUtc.AddSeconds(5) Then
            Return True
        End If

        Return False
    End Function

    Private Function GetRecoveryCandidates(definition As CsvDefinition) As List(Of CsvCandidate)
        Dim list As New List(Of CsvCandidate)()
        Dim filePath = definition.FilePath

        Try
            Dim backupPath = filePath & ".bak"
            If File.Exists(backupPath) Then list.Add(InspectCandidate("BAK", backupPath, definition.Headers))

            Dim dirName = Path.GetDirectoryName(filePath)
            If Not String.IsNullOrWhiteSpace(dirName) AndAlso Directory.Exists(dirName) Then
                Dim fileName = Path.GetFileName(filePath)
                For Each tmp In Directory.EnumerateFiles(dirName, fileName & ".*.tmp", SearchOption.TopDirectoryOnly)
                    list.Add(InspectCandidate("TMP", tmp, definition.Headers))
                Next

                Dim recoveryDir = Path.Combine(AppPaths.BackupsDir, "CsvRecovery", SafeBackupName(fileName))
                If Directory.Exists(recoveryDir) Then
                    For Each backup In Directory.EnumerateFiles(recoveryDir, "*", SearchOption.TopDirectoryOnly)
                        list.Add(InspectCandidate("RECOVERY", backup, definition.Headers))
                    Next
                End If
            End If
        Catch ex As Exception
            ErrorLogService.Log("FrmDataHealth.GetRecoveryCandidates", ex, "Path=" & filePath)
        End Try

        Return list
    End Function

    Private Function InspectCandidate(kind As String, filePath As String, headers As String()) As CsvCandidate
        Dim result As New CsvCandidate With {.Kind = kind, .FilePath = If(filePath, "")}

        Try
            If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then Return result
            Dim info As New FileInfo(filePath)
            result.Length = info.Length
            result.LastWriteUtc = info.LastWriteTimeUtc
            If info.Length <= 0 Then Return result

            Using fs As New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite Or FileShare.Delete)
                Using sr As New StreamReader(fs, Encoding.UTF8, True)
                    Dim firstLine = If(sr.ReadLine(), "")
                    If String.IsNullOrWhiteSpace(firstLine) Then Return result
                    Dim currentHeaders = CsvUtil.ParseLine(firstLine)
                    Dim hasAllHeaders = headers.All(Function(h) currentHeaders.Any(Function(x) String.Equals(x, h, StringComparison.OrdinalIgnoreCase)))
                    If Not hasAllHeaders Then Return result

                    Dim rowCount As Integer = 0
                    While Not sr.EndOfStream
                        Dim line = sr.ReadLine()
                        If Not String.IsNullOrWhiteSpace(line) Then rowCount += 1
                    End While
                    result.DataRowCount = rowCount
                    result.IsValid = True
                End Using
            End Using
        Catch ex As Exception
            ErrorLogService.Log("FrmDataHealth.InspectCandidate", ex, "Path=" & filePath)
        End Try

        Return result
    End Function

    Private Sub RecoverSelected()
        If grid.CurrentRow Is Nothing Then Return

        Dim targetPath = Convert.ToString(grid.CurrentRow.Cells("_Path").Value)
        Dim sourcePath = Convert.ToString(grid.CurrentRow.Cells("_BestBackup").Value)
        If String.IsNullOrWhiteSpace(targetPath) OrElse String.IsNullOrWhiteSpace(sourcePath) Then
            MessageBox.Show(Me, "Bu dosya için geçerli bir kurtarma adayı bulunamadı.", "Veri Sağlığı", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim fileName = Path.GetFileName(targetPath)
        Dim answer = MessageBox.Show(Me,
                                     fileName & " dosyası şu yedekten geri alınacak:" & Environment.NewLine &
                                     sourcePath & Environment.NewLine & Environment.NewLine &
                                     "Mevcut dosya önce .manual_damaged_* olarak saklanacak. Devam edilsin mi?",
                                     "CSV Kurtarma",
                                     MessageBoxButtons.YesNo,
                                     MessageBoxIcon.Warning,
                                     MessageBoxDefaultButton.Button2)
        If answer <> DialogResult.Yes Then Return

        Try
            CsvUtil.ExecuteWithExclusiveLock(
                targetPath,
                Sub()
                    Dim dirName = Path.GetDirectoryName(targetPath)
                    If Not String.IsNullOrWhiteSpace(dirName) Then Directory.CreateDirectory(dirName)
                    If File.Exists(targetPath) Then
                        Dim damagedPath = targetPath & ".manual_damaged_" & DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")
                        File.Copy(targetPath, damagedPath, True)
                    End If
                    File.Copy(sourcePath, targetPath, True)
                End Sub)

            AuditService.Log("CSV_RECOVERY", fileName, sourcePath, "Admin CSV kurtarma islemi yapti.")
            MessageBox.Show(Me, "Kurtarma tamamlandı.", "Veri Sağlığı", MessageBoxButtons.OK, MessageBoxIcon.Information)
            LoadGrid()
        Catch ex As Exception
            ErrorLogService.Log("FrmDataHealth.RecoverSelected", ex, "Target=" & targetPath & "; Source=" & sourcePath)
            MessageBox.Show(Me, "Kurtarma yapılamadı:" & Environment.NewLine & ex.Message, "Veri Sağlığı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub UpdateDetail()
        If grid.CurrentRow Is Nothing Then
            lblDetail.Text = ""
            btnRecover.Enabled = False
            btnCleanTmp.Enabled = False
            Return
        End If

        Dim targetPath = Convert.ToString(grid.CurrentRow.Cells("_Path").Value)
        Dim sourcePath = Convert.ToString(grid.CurrentRow.Cells("_BestBackup").Value)
        Dim safeTmpPaths = ParseHiddenPaths(Convert.ToString(grid.CurrentRow.Cells("_SafeTmpPaths").Value))
        btnRecover.Enabled = Not String.IsNullOrWhiteSpace(sourcePath)
        btnCleanTmp.Enabled = safeTmpPaths.Count > 0

        lblDetail.Text = "Seçili dosya: " & targetPath & Environment.NewLine &
                         "En iyi kurtarma adayı: " & If(String.IsNullOrWhiteSpace(sourcePath), "(yok)", sourcePath) & Environment.NewLine &
                         "Temizlenebilir eski tmp: " & safeTmpPaths.Count & Environment.NewLine &
                         "Not: Eski tmp dosyaları veri kaybı değildir; temizlenirse güvenli arşive taşınır. Riskli tmp veya eski lock varsa satır sarı görünür."
    End Sub

    Private Sub CleanSelectedOldTemps()
        If grid.CurrentRow Is Nothing Then Return

        Dim targetPath = Convert.ToString(grid.CurrentRow.Cells("_Path").Value)
        Dim paths = ParseHiddenPaths(Convert.ToString(grid.CurrentRow.Cells("_SafeTmpPaths").Value)).
            Where(Function(path) File.Exists(path)).
            ToList()

        If paths.Count = 0 Then
            MessageBox.Show(Me, "Bu dosya için temizlenebilir eski tmp bulunamadı.", "Veri Sağlığı", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim answer = MessageBox.Show(Me,
                                     paths.Count & " adet eski tmp dosyası güvenli arşive taşınacak." & Environment.NewLine &
                                     "Ana CSV dosyasına dokunulmayacak. Devam edilsin mi?",
                                     "Eski Tmp Temizleme",
                                     MessageBoxButtons.YesNo,
                                     MessageBoxIcon.Question,
                                     MessageBoxDefaultButton.Button2)
        If answer <> DialogResult.Yes Then Return

        Try
            CsvUtil.ExecuteWithExclusiveLock(
                targetPath,
                Sub()
                    Dim archiveDir = Path.Combine(AppPaths.BackupsDir,
                                                  "CsvRecovery",
                                                  "TmpTrash",
                                                  DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture))
                    Directory.CreateDirectory(archiveDir)

                    For Each tmpPath In paths
                        If Not File.Exists(tmpPath) Then Continue For
                        Dim targetArchivePath = BuildUniquePath(Path.Combine(archiveDir, Path.GetFileName(tmpPath)))
                        File.Move(tmpPath, targetArchivePath)
                    Next
                End Sub)

            AuditService.Log("CSV_TMP_CLEANUP", Path.GetFileName(targetPath), paths.Count.ToString(CultureInfo.InvariantCulture), "Admin eski tmp dosyalarini guvenli arsive tasidi.")
            MessageBox.Show(Me, "Eski tmp dosyaları güvenli arşive taşındı.", "Veri Sağlığı", MessageBoxButtons.OK, MessageBoxIcon.Information)
            LoadGrid()
        Catch ex As Exception
            ErrorLogService.Log("FrmDataHealth.CleanSelectedOldTemps", ex, "Target=" & targetPath)
            MessageBox.Show(Me, "Eski tmp temizliği yapılamadı:" & Environment.NewLine & ex.Message, "Veri Sağlığı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Shared Function ParseHiddenPaths(value As String) As List(Of String)
        If String.IsNullOrWhiteSpace(value) Then Return New List(Of String)()
        Return value.Split(New Char() {ControlChars.Tab}, StringSplitOptions.RemoveEmptyEntries).
            Select(Function(path) path.Trim()).
            Where(Function(path) path <> "").
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToList()
    End Function

    Private Shared Function BuildUniquePath(targetPath As String) As String
        If Not File.Exists(targetPath) Then Return targetPath

        Dim dirName = Path.GetDirectoryName(targetPath)
        Dim baseName = Path.GetFileNameWithoutExtension(targetPath)
        Dim ext = Path.GetExtension(targetPath)
        Dim index As Integer = 1

        Do
            Dim candidate = Path.Combine(If(dirName, ""), baseName & "_" & index.ToString(CultureInfo.InvariantCulture) & ext)
            If Not File.Exists(candidate) Then Return candidate
            index += 1
        Loop
    End Function

    Private Sub Grid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse Not grid.Columns.Contains("Durum") Then Return
        Dim status = Convert.ToString(grid.Rows(e.RowIndex).Cells("Durum").Value)
        Dim row = grid.Rows(e.RowIndex)

        Select Case status
            Case "SAĞLIKLI"
                row.DefaultCellStyle.BackColor = Color.FromArgb(232, 248, 235)
                row.DefaultCellStyle.ForeColor = Color.FromArgb(0, 90, 42)
            Case "UYARI", "KURTARMA VAR", "TMP RİSKİ", "ESKİ LOCK"
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 247, 214)
                row.DefaultCellStyle.ForeColor = Color.FromArgb(111, 73, 0)
            Case Else
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 226, 226)
                row.DefaultCellStyle.ForeColor = Color.FromArgb(140, 0, 0)
        End Select
    End Sub

    Private Shared Function FormatBytes(length As Long) As String
        If length < 1024 Then Return length.ToString("N0", CultureInfo.CurrentCulture) & " B"
        If length < 1024 * 1024 Then Return (length / 1024.0R).ToString("N1", CultureInfo.CurrentCulture) & " KB"
        Return (length / 1024.0R / 1024.0R).ToString("N1", CultureInfo.CurrentCulture) & " MB"
    End Function

    Private Shared Function SafeBackupName(value As String) As String
        Dim safe = If(value, "").Trim()
        If safe = "" Then safe = "csv"
        For Each invalidChar In Path.GetInvalidFileNameChars()
            safe = safe.Replace(invalidChar, "_"c)
        Next
        Return safe
    End Function

    Private Shared Function GetCsvDefinitions() As List(Of CsvDefinition)
        Return New List(Of CsvDefinition) From {
            CsvDef("Kullanıcılar", Function() AppPaths.UsersCsv, DataService.UserHeaders),
            CsvDef("Aktif Oturumlar", Function() AppPaths.ActiveSessionsCsv, DataService.ActiveSessionHeaders, True),
            CsvDef("Oturum Kapatma İstekleri", Function() AppPaths.SessionEndRequestsCsv, DataService.SessionEndRequestHeaders),
            CsvDef("Çalışan Programlar", Function() AppPaths.RunningInstancesCsv, DataService.RunningInstanceHeaders, True),
            CsvDef("Ürün / Teknik Resim", Function() AppPaths.ProductsCsv, DataService.ProductHeaders),
            CsvDef("Kontrol Ölçüleri", Function() AppPaths.ControlPointsCsv, DataService.ControlPointHeaders),
            CsvDef("Ölçüm Grup Alanları", Function() AppPaths.MeasurementGroupAreasCsv, DataService.MeasurementGroupAreaHeaders),
            CsvDef("Ölçüm Kayıtları", Function() AppPaths.MeasurementsCsv, DataService.MeasurementHeaders),
            CsvDef("Ölçüm Düzeltmeleri", Function() AppPaths.MeasurementCorrectionsCsv, DataService.MeasurementCorrectionHeaders),
            CsvDef("SPC Limit Düzeltmeleri", Function() AppPaths.SpcLimitCorrectionsCsv, DataService.SpcLimitCorrectionHeaders),
            CsvDef("Görsel Kontroller", Function() AppPaths.VisualControlsCsv, DataService.VisualControlHeaders),
            CsvDef("Kapalı Gözler", Function() AppPaths.ClosedEyesCsv, DataService.ClosedEyeHeaders),
            CsvDef("Log Kayıtları", Function() AppPaths.AuditLogCsv, DataService.AuditHeaders),
            CsvDef("Kritik Veri Günlüğü", Function() AppPaths.CriticalDataJournalCsv, CriticalDataJournalService.Headers, True, True),
            CsvDef("Üretim Ticketları", Function() AppPaths.ProductionTicketsCsv, DataService.ProductionTicketHeaders),
            CsvDef("Kalıp Bağlama Kayıtları", Function() AppPaths.MoldBindingRecordsCsv, DataService.MoldBindingHeaders),
            CsvDef("Bağlanacak Kalıp Listesi", Function() AppPaths.MoldConnectionPlanCsv, DataService.MoldConnectionPlanHeaders),
            CsvDef("Kalıp Ticketları", Function() AppPaths.MoldTicketsCsv, DataService.MoldTicketHeaders),
            CsvDef("Kalite → Üretim Ticketları", Function() AppPaths.QualityToProductionTicketsCsv, DataService.QualityToProductionTicketHeaders),
            CsvDef("Mekanizma Kontrol", Function() AppPaths.MechanismQualityControlRecordsCsv, DataService.MechanismQualityControlHeaders),
            CsvDef("Plastikhane Vardiya Takip", Function() AppPaths.PlasticShiftTrackingRecordsCsv, DataService.PlasticShiftTrackingHeaders),
            CsvDef("Mekanizma Vardiya Takip", Function() AppPaths.MechanismShiftTrackingRecordsCsv, DataService.PlasticShiftTrackingHeaders),
            CsvDef("Vardiya Hata Raporları", Function() AppPaths.PlasticShiftErrorReportsCsv, DataService.PlasticShiftErrorReportHeaders),
            CsvDef("Hata Raporu Değerlendirici Atamaları", Function() AppPaths.PlasticShiftErrorReportEvaluatorAssignmentsCsv, DataService.PlasticShiftErrorReportEvaluatorAssignmentHeaders),
            CsvDef("Hata Raporu Değerlendirmeleri", Function() AppPaths.PlasticShiftErrorReportEvaluationsCsv, DataService.PlasticShiftErrorReportEvaluationHeaders),
            CsvDef("Hata Raporu Mail Olayları", Function() AppPaths.PlasticShiftErrorReportEmailEventsCsv, DataService.PlasticShiftErrorReportEmailEventHeaders),
            CsvDef("Plastikhane Vardiya Mail Alıcıları", Function() AppPaths.PlasticShiftEmailRecipientsCsv, DataService.PlasticShiftEmailRecipientHeaders),
            CsvDef("Mekanizma Vardiya Mail Alıcıları", Function() AppPaths.MechanismShiftEmailRecipientsCsv, DataService.PlasticShiftEmailRecipientHeaders),
            CsvDef("Bağlama Mail Alıcıları", Function() AppPaths.MoldConnectionPlanEmailRecipientsCsv, DataService.MoldConnectionPlanEmailRecipientHeaders),
            CsvDef("Mekanizma Mail Alıcıları", Function() AppPaths.MechanismQualityEmailRecipientsCsv, DataService.MechanismQualityEmailRecipientHeaders),
            CsvDef("Test Mail Alıcıları", Function() AppPaths.TestRequestEmailRecipientsCsv, DataService.TestRequestEmailRecipientHeaders),
            CsvDef("Test Mail Olayları", Function() AppPaths.TestRequestEmailEventsCsv, DataService.TestRequestEmailEventHeaders),
            CsvDef("Test Talepleri", Function() AppPaths.TestRequestRecordsCsv, DataService.TestRequestHeaders),
            CsvDef("Test Adımları", Function() AppPaths.TestRequestStepsCsv, DataService.TestRequestStepHeaders),
            CsvDef("Test Listesi", Function() AppPaths.TestCatalogCsv, DataService.TestCatalogHeaders),
            CsvDef("Test Grupları", Function() AppPaths.TestGroupsCsv, DataService.TestGroupHeaders),
            CsvDef("MSA Ölçüm Cihazları", Function() AppPaths.MeasurementDevicesCsv, DataService.MeasurementDeviceHeaders),
            CsvDef("Paket Sayaç Kontrolleri", Function() AppPaths.PackageMeterControlsCsv, DataService.PackageMeterControlHeaders),
            CsvDef("Paket Sayaç Satırları", Function() AppPaths.PackageMeterControlLinesCsv, DataService.PackageMeterControlLineHeaders),
            CsvDef("Paket Sayaç Mail Alıcıları", Function() AppPaths.PackageMeterEmailRecipientsCsv, DataService.PackageMeterEmailRecipientHeaders)
        }
    End Function

    Private Shared Function CsvDef(displayName As String,
                                   pathFactory As Func(Of String),
                                   headers As String(),
                                   Optional ignoreTempRisk As Boolean = False,
                                   Optional autoCleanStaleLock As Boolean = False) As CsvDefinition
        Return New CsvDefinition With {
            .DisplayName = displayName,
            .FilePathFactory = pathFactory,
            .Headers = If(headers, Array.Empty(Of String)()),
            .IgnoreTempRisk = ignoreTempRisk,
            .AutoCleanStaleLock = autoCleanStaleLock
        }
    End Function
End Class
