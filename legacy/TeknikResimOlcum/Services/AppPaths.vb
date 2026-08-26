Imports System.IO
Imports System.Windows.Forms

Public NotInheritable Class AppPaths
    Private Sub New()
    End Sub

    Public Const SharedRootEnvironmentVariable As String = "TEKNIKRESIMOLCUM_SHARED_ROOT"
    Public Const SharedRootConfigFileName As String = "_shared_root.txt"
    Public Const CurrentVersionFileName As String = "CurrentVersion.txt"
    Public Const LauncherFileName As String = "TeknikResimOlcum.exe"
    Public Const LauncherPayloadDirectoryName As String = "_launcher"

    Public Shared ReadOnly Property BaseDir As String
        Get
            Return Application.StartupPath
        End Get
    End Property

    Public Shared ReadOnly Property SharedRootDir As String
        Get
            Dim resolved As String = ""

            If TryResolveSharedRoot(Environment.GetEnvironmentVariable(SharedRootEnvironmentVariable), resolved) Then
                Return resolved
            End If

            Try
                Dim configPath = Path.Combine(BaseDir, SharedRootConfigFileName)
                If File.Exists(configPath) Then
                    If TryResolveSharedRoot(File.ReadAllText(configPath).Trim(), resolved) Then
                        Return resolved
                    End If
                End If
            Catch
            End Try

            Return BaseDir
        End Get
    End Property

    Public Shared ReadOnly Property UsesExternalSharedRoot As Boolean
        Get
            Return Not String.Equals(NormalizeDirectoryForCompare(BaseDir),
                                     NormalizeDirectoryForCompare(SharedRootDir),
                                     StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

    Public Shared ReadOnly Property LocalAppDataRoot As String
        Get
            Return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TeknikResimOlcum")
        End Get
    End Property

    Public Shared ReadOnly Property VersionsDir As String
        Get
            Return Path.Combine(SharedRootDir, "Versions")
        End Get
    End Property

    Public Shared ReadOnly Property CurrentVersionFile As String
        Get
            Return Path.Combine(SharedRootDir, CurrentVersionFileName)
        End Get
    End Property

    Public Shared ReadOnly Property LauncherPath As String
        Get
            Return Path.Combine(SharedRootDir, LauncherFileName)
        End Get
    End Property

    Public Shared ReadOnly Property LauncherPayloadPath As String
        Get
            Return Path.Combine(LauncherPayloadDirectoryName, LauncherFileName)
        End Get
    End Property

    Public Shared ReadOnly Property DataDir As String
        Get
            Return Path.Combine(SharedRootDir, "Data")
        End Get
    End Property

    Public Shared ReadOnly Property DrawingsDir As String
        Get
            Return Path.Combine(SharedRootDir, "Drawings")
        End Get
    End Property

    Public Shared Function GetDrawingScopeDirectory(scope As String) As String
        Return Path.Combine(DrawingsDir, ProductInfo.DrawingScopeFolder(scope))
    End Function

    Public Shared ReadOnly Property TempDir As String
        Get
            If UsesExternalSharedRoot Then
                Return Path.Combine(LocalAppDataRoot, "Temp")
            End If

            Return Path.Combine(BaseDir, "Temp")
        End Get
    End Property

    Public Shared ReadOnly Property LocalDraftsDir As String
        Get
            Return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TeknikResimOlcum",
                "MeasurementDrafts")
        End Get
    End Property

    Public Shared ReadOnly Property UpdatesDir As String
        Get
            Return Path.Combine(SharedRootDir, "Updates")
        End Get
    End Property

    Public Shared ReadOnly Property ResourcesDir As String
        Get
            Return Path.Combine(BaseDir, "Resources")
        End Get
    End Property

    Public Shared ReadOnly Property ScrapDashboardHtmlPath As String
        Get
            Return Path.Combine(ResourcesDir, "hurda_dashboard_v26_12_yonetici_yorumu.html")
        End Get
    End Property

    Public Shared ReadOnly Property ScrapDashboardDataDir As String
        Get
            Return Path.Combine(DataDir, "ScrapDashboard")
        End Get
    End Property

    Public Shared ReadOnly Property ScrapDashboardStateJson As String
        Get
            Return Path.Combine(ScrapDashboardDataDir, "ScrapDashboardState.json")
        End Get
    End Property

    Public Shared ReadOnly Property ReworkDashboardHtmlPath As String
        Get
            Return Path.Combine(ResourcesDir, "rework_dashboard.html")
        End Get
    End Property

    Public Shared ReadOnly Property MainDashboardHtmlPath As String
        Get
            Return Path.Combine(ResourcesDir, "main_dashboard.html")
        End Get
    End Property

    Public Shared ReadOnly Property ReworkDashboardDataDir As String
        Get
            Return Path.Combine(DataDir, "ReworkDashboard")
        End Get
    End Property

    Public Shared ReadOnly Property ReworkDashboardStateJson As String
        Get
            Return Path.Combine(ReworkDashboardDataDir, "ReworkDashboardState.json")
        End Get
    End Property

    Public Shared ReadOnly Property PermissionMatrixCsv As String
        Get
            Return Path.Combine(BaseDir, "Docs", "YETKI_MATRISI.csv")
        End Get
    End Property

    Public Shared ReadOnly Property PermissionMatrixMarkdown As String
        Get
            Return Path.Combine(BaseDir, "Docs", "YETKI_MATRISI.md")
        End Get
    End Property

    Public Shared ReadOnly Property BackupsDir As String
        Get
            Return Path.Combine(SharedRootDir, "Backups")
        End Get
    End Property

    Public Shared ReadOnly Property DrawingEncryptionKeyFile As String
        Get
            Return Path.Combine(DataDir, "DrawingEncryption.key")
        End Get
    End Property

    Public Shared ReadOnly Property LegacyDrawingMigrationKeyFile As String
        Get
            Return Path.Combine(BaseDir, "_legacy_drawing_migration.key")
        End Get
    End Property

    Public Shared ReadOnly Property PendingTransactionsDir As String
        Get
            Return Path.Combine(DataDir, "PendingTransactions")
        End Get
    End Property

    Public Shared ReadOnly Property UsersCsv As String
        Get
            Return Path.Combine(DataDir, "Users.csv")
        End Get
    End Property

    Public Shared ReadOnly Property UserStoreBackupsDir As String
        Get
            Return Path.Combine(DataDir, "UserStoreBackups")
        End Get
    End Property

    Public Shared ReadOnly Property UserStoreMarkerFile As String
        Get
            Return Path.Combine(DataDir, "UserStoreInitialized.flag")
        End Get
    End Property

    Public Shared ReadOnly Property ProductsCsv As String
        Get
            Return Path.Combine(DataDir, "Products.csv")
        End Get
    End Property

    Public Shared ReadOnly Property ControlPointsCsv As String
        Get
            Return Path.Combine(DataDir, "ControlPoints.csv")
        End Get
    End Property

    Public Shared ReadOnly Property MeasurementGroupAreasCsv As String
        Get
            Return Path.Combine(DataDir, "MeasurementGroupAreas.csv")
        End Get
    End Property

    Public Shared ReadOnly Property MeasurementsCsv As String
        Get
            Return Path.Combine(DataDir, "MeasurementRecords.csv")
        End Get
    End Property

    Public Shared ReadOnly Property MeasurementCorrectionsCsv As String
        Get
            Return Path.Combine(DataDir, "MeasurementCorrections.csv")
        End Get
    End Property

    Public Shared ReadOnly Property SpcLimitCorrectionsCsv As String
        Get
            Return Path.Combine(DataDir, "SpcLimitCorrections.csv")
        End Get
    End Property

    Public Shared ReadOnly Property VisualControlsCsv As String
        Get
            Return Path.Combine(DataDir, "VisualControlRecords.csv")
        End Get
    End Property

    Public Shared ReadOnly Property ClosedEyesCsv As String
        Get
            Return Path.Combine(DataDir, "ClosedEyeRecords.csv")
        End Get
    End Property

    Public Shared ReadOnly Property AuditLogCsv As String
        Get
            Return Path.Combine(DataDir, "AuditLog.csv")
        End Get
    End Property

    Public Shared ReadOnly Property CriticalDataJournalCsv As String
        Get
            Return Path.Combine(DataDir, "CriticalDataJournal.csv")
        End Get
    End Property

    Public Shared ReadOnly Property ApplicationErrorsLog As String
        Get
            Return Path.Combine(DataDir, "ApplicationErrors.log")
        End Get
    End Property

    Public Shared ReadOnly Property ProductionTicketsCsv As String
        Get
            Return Path.Combine(DataDir, "ProductionTickets.csv")
        End Get
    End Property

    Public Shared ReadOnly Property MoldTicketsCsv As String
        Get
            Return Path.Combine(DataDir, "MoldTickets.csv")
        End Get
    End Property

    Public Shared ReadOnly Property NewMoldCommissioningsCsv As String
        Get
            Return Path.Combine(DataDir, "NewMoldCommissionings.csv")
        End Get
    End Property

    Public Shared ReadOnly Property NewMoldCommissioningChecklistCsv As String
        Get
            Return Path.Combine(DataDir, "NewMoldCommissioningChecklist.csv")
        End Get
    End Property

    Public Shared ReadOnly Property NewMoldCommissioningTrialsCsv As String
        Get
            Return Path.Combine(DataDir, "NewMoldCommissioningTrials.csv")
        End Get
    End Property

    Public Shared ReadOnly Property NewMoldCommissioningActionsCsv As String
        Get
            Return Path.Combine(DataDir, "NewMoldCommissioningActions.csv")
        End Get
    End Property

    Public Shared ReadOnly Property ActiveSessionsCsv As String
        Get
            Return Path.Combine(DataDir, "ActiveSessions.csv")
        End Get
    End Property

    Public Shared ReadOnly Property SessionEndRequestsCsv As String
        Get
            Return Path.Combine(DataDir, "SessionEndRequests.csv")
        End Get
    End Property

    Public Shared ReadOnly Property RunningInstancesCsv As String
        Get
            Return Path.Combine(DataDir, "RunningInstances.csv")
        End Get
    End Property

    Public Shared ReadOnly Property QualityToProductionTicketsCsv As String
        Get
            Return Path.Combine(DataDir, "QualityToProductionTickets.csv")
        End Get
    End Property

    Public Shared ReadOnly Property MoldBindingRecordsCsv As String
        Get
            Return Path.Combine(DataDir, "MoldBindingRecords.csv")
        End Get
    End Property

    Public Shared ReadOnly Property MoldConnectionPlanCsv As String
        Get
            Return Path.Combine(DataDir, "MoldConnectionPlan.csv")
        End Get
    End Property

    Public Shared ReadOnly Property MoldConnectionPlanEmailRecipientsCsv As String
        Get
            Return Path.Combine(DataDir, "MoldConnectionPlanEmailRecipients.csv")
        End Get
    End Property

    Public Shared ReadOnly Property MechanismQualityControlRecordsCsv As String
        Get
            Return Path.Combine(DataDir, "MechanismQualityControlRecords.csv")
        End Get
    End Property

    Public Shared ReadOnly Property PlasticShiftTrackingRecordsCsv As String
        Get
            Return Path.Combine(DataDir, "PlasticShiftTrackingRecords.csv")
        End Get
    End Property

    Public Shared ReadOnly Property MechanismShiftTrackingRecordsCsv As String
        Get
            Return Path.Combine(DataDir, "MechanismShiftTrackingRecords.csv")
        End Get
    End Property

    Public Shared ReadOnly Property PlasticShiftErrorReportsCsv As String
        Get
            Return Path.Combine(DataDir, "PlasticShiftErrorReports.csv")
        End Get
    End Property

    Public Shared ReadOnly Property PlasticShiftErrorReportEvaluatorAssignmentsCsv As String
        Get
            Return Path.Combine(DataDir, "PlasticShiftErrorReportEvaluatorAssignments.csv")
        End Get
    End Property

    Public Shared ReadOnly Property PlasticShiftErrorReportEvaluationsCsv As String
        Get
            Return Path.Combine(DataDir, "PlasticShiftErrorReportEvaluations.csv")
        End Get
    End Property

    Public Shared ReadOnly Property PlasticShiftErrorReportEmailEventsCsv As String
        Get
            Return Path.Combine(DataDir, "PlasticShiftErrorReportEmailEvents.csv")
        End Get
    End Property

    Public Shared ReadOnly Property PlasticShiftEmailRecipientsCsv As String
        Get
            Return Path.Combine(DataDir, "PlasticShiftEmailRecipients.csv")
        End Get
    End Property

    Public Shared ReadOnly Property MechanismShiftEmailRecipientsCsv As String
        Get
            Return Path.Combine(DataDir, "MechanismShiftEmailRecipients.csv")
        End Get
    End Property

    Public Shared ReadOnly Property MechanismQualityEmailRecipientsCsv As String
        Get
            Return Path.Combine(DataDir, "MechanismQualityEmailRecipients.csv")
        End Get
    End Property

    Public Shared ReadOnly Property TestRequestEmailRecipientsCsv As String
        Get
            Return Path.Combine(DataDir, "TestRequestEmailRecipients.csv")
        End Get
    End Property

    Public Shared ReadOnly Property TestRequestEmailEventsCsv As String
        Get
            Return Path.Combine(DataDir, "TestRequestEmailEvents.csv")
        End Get
    End Property

    Public Shared ReadOnly Property TestRequestRecordsCsv As String
        Get
            Return Path.Combine(DataDir, "TestRequestRecords.csv")
        End Get
    End Property

    Public Shared ReadOnly Property TestRequestStepsCsv As String
        Get
            Return Path.Combine(DataDir, "TestRequestSteps.csv")
        End Get
    End Property

    Public Shared ReadOnly Property TestCatalogCsv As String
        Get
            Return Path.Combine(DataDir, "TestCatalog.csv")
        End Get
    End Property

    Public Shared ReadOnly Property TestGroupsCsv As String
        Get
            Return Path.Combine(DataDir, "TestGroups.csv")
        End Get
    End Property

    Public Shared ReadOnly Property MeasurementDevicesCsv As String
        Get
            Return Path.Combine(DataDir, "MeasurementDevices.csv")
        End Get
    End Property

    Public Shared ReadOnly Property PackageMeterControlsCsv As String
        Get
            Return Path.Combine(DataDir, "PackageMeterControls.csv")
        End Get
    End Property

    Public Shared ReadOnly Property PackageMeterControlLinesCsv As String
        Get
            Return Path.Combine(DataDir, "PackageMeterControlLines.csv")
        End Get
    End Property

    Public Shared ReadOnly Property PackageMeterEmailRecipientsCsv As String
        Get
            Return Path.Combine(DataDir, "PackageMeterEmailRecipients.csv")
        End Get
    End Property

    Public Shared Function ResolveDrawingFilePath(drawingFileName As String) As String
        If String.IsNullOrWhiteSpace(drawingFileName) Then
            Throw New InvalidDataException("Teknik resim dosya adı boş olamaz.")
        End If

        Dim fileName = drawingFileName.Trim()
        If Not String.Equals(fileName, drawingFileName, StringComparison.Ordinal) OrElse
           Path.IsPathRooted(fileName) Then
            Throw New InvalidDataException("Geçersiz teknik resim dosya adı. Yalnızca Drawings klasörü altındaki güvenli dosya adları kullanılabilir.")
        End If

        Dim normalizedRelative = fileName.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
        If normalizedRelative.Contains(New String(Path.DirectorySeparatorChar, 2)) Then
            Throw New InvalidDataException("Geçersiz teknik resim dosya adı. Boş klasör adı kullanılamaz.")
        End If

        Dim parts = normalizedRelative.Split(Path.DirectorySeparatorChar)
        For Each part In parts
            If part = "" OrElse
               part = "." OrElse
               part = ".." OrElse
               part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 OrElse
               Not String.Equals(part.TrimEnd(" "c, "."c), part, StringComparison.Ordinal) Then
                Throw New InvalidDataException("Geçersiz teknik resim dosya adı. Yalnızca Drawings klasörü altındaki güvenli dosya adları kullanılabilir.")
            End If
        Next

        If parts.Length > 2 Then
            Throw New InvalidDataException("Teknik resim dosyası en fazla bir depo klasörü altında saklanabilir.")
        End If

        Dim drawingsRoot = Path.GetFullPath(DrawingsDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        Dim resolvedPath = Path.GetFullPath(Path.Combine(drawingsRoot, normalizedRelative))
        Dim drawingsPrefix = drawingsRoot & Path.DirectorySeparatorChar

        If Not resolvedPath.StartsWith(drawingsPrefix, StringComparison.OrdinalIgnoreCase) Then
            Throw New InvalidDataException("Teknik resim dosyası Drawings klasörü dışında olamaz.")
        End If

        Return resolvedPath
    End Function

    Public Shared Sub EnsureFolders()
        Directory.CreateDirectory(SharedRootDir)
        Directory.CreateDirectory(LocalAppDataRoot)
        Directory.CreateDirectory(DataDir)
        Directory.CreateDirectory(ScrapDashboardDataDir)
        Directory.CreateDirectory(ReworkDashboardDataDir)
        Directory.CreateDirectory(DrawingsDir)
        Directory.CreateDirectory(GetDrawingScopeDirectory(ProductInfo.DrawingScopePlastic))
        Directory.CreateDirectory(GetDrawingScopeDirectory(ProductInfo.DrawingScopeIncomingQuality))
        Directory.CreateDirectory(TempDir)
        Directory.CreateDirectory(UpdatesDir)
        Directory.CreateDirectory(BackupsDir)
        Directory.CreateDirectory(VersionsDir)
        Directory.CreateDirectory(PendingTransactionsDir)
        Directory.CreateDirectory(UserStoreBackupsDir)
        Directory.CreateDirectory(LocalDraftsDir)
    End Sub

    Private Shared Function TryResolveSharedRoot(rawValue As String, ByRef resolvedPath As String) As Boolean
        resolvedPath = ""

        Try
            Dim value = If(rawValue, "").Trim()
            If value = "" Then Return False

            value = value.Trim(""""c)
            value = Environment.ExpandEnvironmentVariables(value)
            If value = "" Then Return False

            If Not Path.IsPathRooted(value) Then
                value = Path.Combine(BaseDir, value)
            End If

            resolvedPath = Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            Return resolvedPath <> ""
        Catch
            resolvedPath = ""
            Return False
        End Try
    End Function

    Private Shared Function NormalizeDirectoryForCompare(pathValue As String) As String
        Try
            Return Path.GetFullPath(If(pathValue, "")).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        Catch
            Return If(pathValue, "").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        End Try
    End Function
End Class
