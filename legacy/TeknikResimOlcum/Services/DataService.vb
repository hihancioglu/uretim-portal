Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json
Imports System.Text.RegularExpressions

Public NotInheritable Class DataService
    Public Const PackageMeterAllowedRangeDisplay As String = "40, 50, 63, 80, 100, 125, 160, 200, 250, 315, 400, 500, 630, 800, 1000"
    Private Shared ReadOnly PackageMeterAllowedRangeValues As Decimal() = {
        40D, 50D, 63D, 80D, 100D, 125D, 160D, 200D, 250D, 315D, 400D, 500D, 630D, 800D, 1000D
    }
    Private Shared ReadOnly mechanismQualityRepairLock As New Object()
    Private Shared mechanismQualityRepairAttempted As Boolean

    Private Sub New()
    End Sub

    Private NotInheritable Class MoldBindingTicketTransaction
        Public Property Version As Integer = 1
        Public Property BindingId As String = ""
        Public Property TicketId As String = ""
        Public Property CompletedAt As String = ""
        Public Property CompletedBy As String = ""
        Public Property CompletedComputerName As String = ""
        Public Property FinishNote As String = ""
        Public Property BindingDurationMin As String = ""
        Public Property CreatedAtUtc As String = ""
        Public Property TicketRow As Dictionary(Of String, String) =
            New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
    End Class

    Public Shared ReadOnly UserHeaders As String() = {"Username", "PasswordHash", "PasswordSalt", "Role", "IsActive", "ShowOnLogin", "IsPermissionTestAccount", "MustChangePassword", "PasswordChangedAt", "CreatedAt", "LastLoginAt"}
    Public Shared ReadOnly ActiveSessionHeaders As String() = {"SessionId", "Username", "ComputerName", "LoginAt", "LastSeen"}
    Public Shared ReadOnly SessionEndRequestHeaders As String() = {"RequestId", "SessionId", "Username", "ComputerName", "RequestedBy", "RequestedAt", "Reason"}
    Public Shared ReadOnly RunningInstanceHeaders As String() = {"InstanceId", "ComputerName", "WindowsUser", "AppUser", "Role", "ProcessId", "Version", "StartedAt", "LastSeen", "ExecutablePath"}
    Public Shared ReadOnly ProductHeaders As String() = {"TrCode", "ProductName", "PlasticCode", "Material", "ColorName", "MoldCavityCount", "MoldCode", "DrawingRev", "DrawingFile", "DrawingScope", "IsActive", "CreatedBy", "CreatedAt"}
    Public Shared ReadOnly ControlPointHeaders As String() = {"TrCode", "DrawingRev", "DrawingScope", "MeasureId", "MeasureName", "Nominal", "LowerTol", "UpperTol", "LowerLimit", "UpperLimit", "PageNo", "XPercent", "YPercent", "Unit", "IsMandatory", "MeasurementGroup", "SampleFrequency", "IsCritical", "SortNo", "IsActive", "SpcKey", "MeasureVersion", "ValidFrom", "ValidTo", "ChangeReason"}
    Public Shared ReadOnly MeasurementGroupAreaHeaders As String() = {"TrCode", "DrawingRev", "DrawingScope", "GroupName", "PageNo", "LeftPercent", "TopPercent", "RightPercent", "BottomPercent", "UpdatedBy", "UpdatedAt"}
    Public Shared ReadOnly MeasurementHeaders As String() = {"RecordId", "TrCode", "DrawingRev", "DrawingScope", "LotNo", "SerialNo", "EyeCount", "EyeNo", "OperatorName", "ComputerName", "MeasurementDate", "MeasureId", "MeasureName", "MeasurementGroup", "SampleFrequency", "IsCritical", "SortNo", "Nominal", "LowerLimit", "UpperLimit", "PageNo", "XPercent", "YPercent", "MeasuredValue", "Result", "Note", "ProductionTicketId", "SpcKey", "MeasureVersion", "CommissioningId"}
    Public Shared ReadOnly MeasurementCorrectionHeaders As String() = {"CorrectionId", "RecordId", "TrCode", "DrawingRev", "EyeNo", "MeasureId", "MeasureName", "MeasurementDate", "OldValue", "NewValue", "OldResult", "NewResult", "Reason", "ChangedBy", "ChangedAt", "ComputerName"}
    Public Shared ReadOnly SpcLimitCorrectionHeaders As String() = {"CorrectionId", "TrCode", "DrawingRev", "DrawingScope", "SpcKey", "DateFrom", "DateTo", "OldLimits", "NewNominal", "NewLowerLimit", "NewUpperLimit", "AffectedRows", "ResultChangedRows", "Reason", "ChangedBy", "ChangedAt", "ComputerName"}
    Public Shared ReadOnly VisualControlHeaders As String() = {"RecordId", "TrCode", "DrawingRev", "DrawingScope", "LotNo", "SerialNo", "EyeCount", "EyeNo", "OperatorName", "ComputerName", "ControlDate", "ControlName", "IsSelected", "Result", "Note"}
    Public Shared ReadOnly ClosedEyeHeaders As String() = {"RecordId", "TrCode", "DrawingRev", "DrawingScope", "LotNo", "SerialNo", "EyeCount", "EyeNo", "OperatorName", "ComputerName", "ClosedDate", "Reason", "ProductionTicketId", "CommissioningId"}
    Public Shared ReadOnly AuditHeaders As String() = {"LogId", "DateTime", "UserName", "Role", "ComputerName", "Action", "TrCode", "DrawingRev", "Detail"}
    Public Shared ReadOnly ProductionTicketHeaders As String() = {"TicketId", "Status", "CreatedAt", "CreatedBy", "ComputerName", "MachineNo", "PreviousMachineNo", "MoldCode", "TrCode", "DrawingRev", "ProductName", "Material", "ColorName", "PlasticCode", "RawMaterial", "WorkOrderNo", "Note", "SeenByQuality", "SeenAt", "ClosedBy", "ClosedAt", "CloseNote", "BindingId", "BindingStartAt", "BindingEndAt", "BindingDurationMin", "BindingReason", "MachineChangeReason"}
    Public Shared ReadOnly MoldBindingHeaders As String() = {"BindingId", "Status", "StartedAt", "StartedBy", "StartComputerName", "CompletedAt", "CompletedBy", "CompletedComputerName", "MachineNo", "PreviousMachineNo", "MoldCode", "TrCode", "DrawingRev", "ProductName", "Material", "ColorName", "PlasticCode", "RawMaterial", "WorkOrderNo", "BindingReason", "MachineChangeReason", "StartNote", "FinishNote", "Note", "BindingDurationMin", "ProductionTicketId"}
    Public Shared ReadOnly MoldTicketHeaders As String() = {"MoldTicketId", "Status", "CreatedAt", "CreatedBy", "ComputerName", "MoldCode", "TrCode", "DrawingRev", "ProductName", "Severity", "ProblemType", "ProblemDescription", "ActionPlan", "SourcePlasticShiftRecordId", "ClosedBy", "ClosedAt", "CloseNote"}
    Public Shared ReadOnly NewMoldCommissioningHeaders As String() = {
        "CommissioningId", "Status", "CurrentStage", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy",
        "ProductName", "ProductCode", "DrawingNo", "DrawingRev", "MoldCode", "MoldManufacturer", "CavityCount",
        "RawMaterial", "Color", "Masterbatch", "PlannedMachine", "TargetCycleSeconds", "PlannedQuantity",
        "CriticalDimensions", "SpecialCharacteristics", "FunctionTests", "MatingParts", "CustomerRequirements",
        "RequestedProductionDate", "ParticipatingDepartments", "DocumentsNote",
        "MechanicalApproval", "MechanicalApprovedBy", "MechanicalApprovedAt",
        "ProductApproval", "ProductApprovedBy", "ProductApprovedAt",
        "ProcessApproval", "ProcessApprovedBy", "ProcessApprovedAt",
        "FinalDecision", "FinalDecisionNote", "ConditionalUntil", "ConditionalQuantity", "NextTrialDate"
    }
    Public Shared ReadOnly NewMoldCommissioningChecklistHeaders As String() = {
        "ChecklistId", "CommissioningId", "ItemNo", "Category", "ItemText", "Result", "Explanation", "CheckedBy", "CheckedAt"
    }
    Public Shared ReadOnly NewMoldCommissioningTrialHeaders As String() = {
        "TrialId", "CommissioningId", "TrialNo", "TrialDate", "MachineNo", "MaterialLot", "ProcessStatus",
        "SamplePerCavity", "CycleTime", "PartWeight", "RunnerWeight", "VisualResult", "FunctionResult", "MeasurementResult",
        "QualityValidationResult", "Nonconformity", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"
    }
    Public Shared ReadOnly NewMoldCommissioningActionHeaders As String() = {
        "ActionId", "CommissioningId", "TrialNo", "Severity", "CavityNo", "Description", "ProbableCause",
        "ResponsibleDepartment", "Action", "DueDate", "Status", "VerificationNote",
        "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"
    }
    Public Shared ReadOnly QualityToProductionTicketHeaders As String() = {"TicketId", "Status", "CreatedAt", "CreatedBy", "ComputerName", "TrCode", "DrawingRev", "ProductName", "LotNo", "SerialNo", "EyeCount", "EyeNo", "RecordId", "SourceQualityTicketId", "SourceType", "IssueSummary", "MeasurementNokCount", "VisualNokCount", "SeenByProduction", "SeenAt", "ClosedBy", "ClosedAt", "CloseNote"}
    Public Shared ReadOnly MoldConnectionPlanHeaders As String() = {"PlanId", "ImportedAt", "ImportedBy", "SourceFile", "SourceSheet", "SourceRow", "MachineName", "MachineNo", "RunningMolds", "CurrentMoldNo", "CurrentMoldRackNo", "CurrentPlasticCode", "CurrentTrCode", "FirstMoldNo", "FirstMoldRackNo", "FirstPlasticCode", "FirstTrCode", "SecondMoldNo", "SecondMoldRackNo", "SecondPlasticCode", "SecondTrCode"}
    Public Shared ReadOnly MechanismQualityControlHeaders As String() = {"ControlId", "Status", "CreatedAt", "ControlDateTime", "IncomingEyeCount", "DeliveredBy", "ProductNameCode", "MountedMechanismCounter", "Explanation", "DeliveryExplanation", "ControlExplanation", "IsSuitable", "IsNotSuitable", "ControlledBy", "ControlledAt", "CreatedComputerName", "ControlledComputerName"}
    Public Shared ReadOnly PlasticShiftTrackingHeaders As String() = {"RecordId", "OccurredAt", "DefectiveQuantity", "Responsible", "ProductNameCode", "Problem", "ActionTaken", "YellowCard", "MoldModification", "ErrorReport", "TestPerformed", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt", "ComputerName"}
    Public Shared ReadOnly PlasticShiftErrorReportHeaders As String() = {
        "ReportId", "ShiftRecordId", "ReportNo", "Status", "RevisionDate",
        "SourceDepartment", "QualityControlPoint", "PartNameNo", "TrNo", "PartType", "Quantity",
        "MachineNo", "OperatorName", "DefectArea", "DefectCode", "DefectType",
        "NonconformityDescription", "QualityInspector", "DetectedBy", "UnitManagerApproval",
        "Disposition", "KaizenResponsible", "KaizenNo", "RootCause",
        "Action1", "ActionResponsible1", "ActionDueDate1", "ActionClosedDate1",
        "Action2", "ActionResponsible2", "ActionDueDate2", "ActionClosedDate2",
        "Action3", "ActionResponsible3", "ActionDueDate3", "ActionClosedDate3",
        "Action4", "ActionResponsible4", "ActionDueDate4", "ActionClosedDate4",
        "Action5", "ActionResponsible5", "ActionDueDate5", "ActionClosedDate5",
        "StockReviewResult", "StockReviewDetail",
        "AffectedProcessResult", "AffectedProcessDetail",
        "AffectedProductResult", "AffectedProductDetail",
        "DocumentNeedResult", "DocumentNeedDetail",
        "DrawingRevisionResult", "DrawingRevisionDetail",
        "MoldRevisionResult", "MoldRevisionDetail",
        "SemiFinishedReviewResult", "SemiFinishedReviewDetail",
        "VerificationDueDate", "VerificationActivities", "VerificationResponsible",
        "VerificationDate", "VerificationSuitable", "CloseApproved", "CloseNote",
        "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt", "ComputerName"
    }
    Public Shared ReadOnly PlasticShiftErrorReportEvaluatorAssignmentHeaders As String() = {
        "PositionKey", "PositionName", "RequiredRole", "UserName", "Email", "IsActive", "UpdatedBy", "UpdatedAt"
    }
    Public Shared ReadOnly PlasticShiftErrorReportEvaluationHeaders As String() = {
        "EvaluationId", "ReportId", "PositionKey", "PositionName", "RequiredRole",
        "AssignedUserName", "AssignedEmail", "Decision", "Explanation",
        "EvaluatedBy", "EvaluatedAt", "UpdatedAt", "ComputerName"
    }
    Public Shared ReadOnly PlasticShiftErrorReportEmailEventHeaders As String() = {
        "EventKey", "ReportId", "EventType", "SentAt", "SentBy", "ComputerName", "Recipients"
    }
    Public Shared ReadOnly PlasticShiftEmailRecipientHeaders As String() = {"Email", "DisplayName", "RecipientType", "IsActive", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"}
    Public Shared ReadOnly MoldConnectionPlanEmailRecipientHeaders As String() = {"Email", "DisplayName", "IsActive", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"}
    Public Shared ReadOnly MechanismQualityEmailRecipientHeaders As String() = {"Email", "DisplayName", "RecipientType", "IsActive", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"}
    Public Shared ReadOnly TestRequestEmailRecipientHeaders As String() = {"EventType", "RequestingDepartment", "Email", "DisplayName", "RecipientType", "IsActive", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"}
    Public Shared ReadOnly TestRequestEmailEventHeaders As String() = {"EventKey", "RequestId", "EventType", "SentAt", "SentBy", "ComputerName", "Recipients"}
    Public Shared ReadOnly TestRequestHeaders As String() = {"RequestId", "Status", "CreatedAt", "CreatedBy", "CreatedComputerName", "RequestingDepartment", "RequestedDepartment", "RequestReason", "ProductNameTrCode", "RequestedTests", "SampleQuantity", "Priority", "DueDate", "RequesterReportNo", "RequesterExplanation", "AcceptedAt", "AcceptedBy", "CompletedAt", "CompletedBy", "LabReportNo", "Result", "LabExplanation", "CancelledAt", "CancelledBy", "CancelReason", "UpdatedAt", "UpdatedBy"}
    Public Shared ReadOnly TestRequestStepHeaders As String() = {"RequestId", "StepId", "SortNo", "TestName", "TestDescription", "Status", "Result", "Explanation", "CompletedAt", "CompletedBy", "CompletedComputerName", "SkippedAt", "SkippedBy", "SkipReason", "ReopenedAt", "ReopenedBy", "ReopenReason", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy"}
    Public Shared ReadOnly TestCatalogHeaders As String() = {"TestName", "Description", "IsActive", "SortNo", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"}
    Public Shared ReadOnly TestGroupHeaders As String() = {"GroupName", "TestsText", "IsActive", "SortNo", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"}
    Public Shared ReadOnly MeasurementDeviceHeaders As String() = {
        "DeviceId", "FixedAssetNo",
        "StdIso9001", "StdIso45001", "StdIso50001", "StdIso46001", "StdIso17020", "StdIso17025",
        "DeviceName", "SerialNo", "Brand", "Model", "DeviceType", "MeasurementRange", "Resolution", "Unit",
        "Location", "ReferenceDevice", "UsageStatus", "RegistrationDate", "Note", "Status",
        "CalibrationPeriodMonths", "CalibrationDate", "CalibrationDueDate", "Organization",
        "Responsible", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"
    }
    Public Shared ReadOnly PackageMeterControlHeaders As String() = {"ControlId", "Status", "MeterModel", "PulseCount", "Customer", "ControlDate", "OperatorInfo", "ControllerName", "ProductionPanelNo", "ControlPanelNo", "IsSmartMeter", "ReferenceFlowQ4", "ReferenceFlowQ3", "ReferenceFlowQ2", "ReferenceFlowQ1", "RangeValue", "Explanation", "MeterCount", "SuitableCount", "UnsuitableCount", "IncompleteCount", "CreatedAt", "CreatedBy", "CreatedComputerName", "CompletedAt", "CompletedBy", "UpdatedAt", "UpdatedBy"}
    Public Shared ReadOnly PackageMeterControlLineHeaders As String() = {"ControlId", "LineId", "SortNo", "SerialNumber", "LabelErrorQ3", "LabelErrorQ2", "LabelErrorQ1", "TestFlowQ4Manual", "TestFlowQ3", "TestFlowQ2", "TestFlowQ1", "CreditResult", "ValveResult", "OverallResult", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy"}
    Public Shared ReadOnly PackageMeterEmailRecipientHeaders As String() = {"Email", "DisplayName", "RecipientType", "IsActive", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"}

    Public Shared Sub EnsureLoginFiles()
        CsvUtil.EnsureFile(AppPaths.UsersCsv, UserHeaders)
        CsvUtil.EnsureFile(AppPaths.ActiveSessionsCsv, ActiveSessionHeaders)
        CsvUtil.EnsureFile(AppPaths.SessionEndRequestsCsv, SessionEndRequestHeaders)
        CsvUtil.EnsureFile(AppPaths.AuditLogCsv, AuditHeaders)
    End Sub

    Public Shared Sub EnsureAllFiles()
        CsvUtil.EnsureFile(AppPaths.UsersCsv, UserHeaders)
        CsvUtil.EnsureFile(AppPaths.ActiveSessionsCsv, ActiveSessionHeaders)
        CsvUtil.EnsureFile(AppPaths.SessionEndRequestsCsv, SessionEndRequestHeaders)
        ' Çalışan program listesi yalnızca izleme amaçlıdır. Dosya başka bir süreç
        ' tarafından kilitliyse uygulamanın açılışını engellememelidir;
        ' ApplicationInstanceService dosyayı kendi hata yönetimi içinde hazırlar.
        CsvUtil.EnsureFile(AppPaths.ProductsCsv, ProductHeaders)
        CsvUtil.EnsureFile(AppPaths.ControlPointsCsv, ControlPointHeaders)
        CsvUtil.EnsureFile(AppPaths.MeasurementGroupAreasCsv, MeasurementGroupAreaHeaders)
        CsvUtil.EnsureFile(AppPaths.MeasurementsCsv, MeasurementHeaders)
        CsvUtil.EnsureFile(AppPaths.MeasurementCorrectionsCsv, MeasurementCorrectionHeaders)
        CsvUtil.EnsureFile(AppPaths.SpcLimitCorrectionsCsv, SpcLimitCorrectionHeaders)
        CsvUtil.EnsureFile(AppPaths.VisualControlsCsv, VisualControlHeaders)
        CsvUtil.EnsureFile(AppPaths.ClosedEyesCsv, ClosedEyeHeaders)
        CsvUtil.EnsureFile(AppPaths.AuditLogCsv, AuditHeaders)
        CsvUtil.EnsureFile(AppPaths.ProductionTicketsCsv, ProductionTicketHeaders)
        CsvUtil.EnsureFile(AppPaths.MoldBindingRecordsCsv, MoldBindingHeaders)
        CsvUtil.EnsureFile(AppPaths.MoldTicketsCsv, MoldTicketHeaders)
        CsvUtil.EnsureFile(AppPaths.NewMoldCommissioningsCsv, NewMoldCommissioningHeaders)
        CsvUtil.EnsureFile(AppPaths.NewMoldCommissioningChecklistCsv, NewMoldCommissioningChecklistHeaders)
        CsvUtil.EnsureFile(AppPaths.NewMoldCommissioningTrialsCsv, NewMoldCommissioningTrialHeaders)
        CsvUtil.EnsureFile(AppPaths.NewMoldCommissioningActionsCsv, NewMoldCommissioningActionHeaders)
        CsvUtil.EnsureFile(AppPaths.QualityToProductionTicketsCsv, QualityToProductionTicketHeaders)
        EnsureOptionalFile(AppPaths.MoldConnectionPlanCsv, MoldConnectionPlanHeaders, "MoldConnectionPlan")
        CsvUtil.EnsureFile(AppPaths.MechanismQualityControlRecordsCsv, MechanismQualityControlHeaders)
        CsvUtil.EnsureFile(AppPaths.PlasticShiftTrackingRecordsCsv, PlasticShiftTrackingHeaders)
        CsvUtil.EnsureFile(AppPaths.MechanismShiftTrackingRecordsCsv, PlasticShiftTrackingHeaders)
        CsvUtil.EnsureFile(AppPaths.PlasticShiftErrorReportsCsv, PlasticShiftErrorReportHeaders)
        CsvUtil.EnsureFile(AppPaths.PlasticShiftErrorReportEvaluatorAssignmentsCsv, PlasticShiftErrorReportEvaluatorAssignmentHeaders)
        CsvUtil.EnsureFile(AppPaths.PlasticShiftErrorReportEvaluationsCsv, PlasticShiftErrorReportEvaluationHeaders)
        CsvUtil.EnsureFile(AppPaths.PlasticShiftErrorReportEmailEventsCsv, PlasticShiftErrorReportEmailEventHeaders)
        CsvUtil.EnsureFile(AppPaths.PlasticShiftEmailRecipientsCsv, PlasticShiftEmailRecipientHeaders)
        CsvUtil.EnsureFile(AppPaths.MechanismShiftEmailRecipientsCsv, PlasticShiftEmailRecipientHeaders)
        CsvUtil.EnsureFile(AppPaths.MoldConnectionPlanEmailRecipientsCsv, MoldConnectionPlanEmailRecipientHeaders)
        CsvUtil.EnsureFile(AppPaths.MechanismQualityEmailRecipientsCsv, MechanismQualityEmailRecipientHeaders)
        CsvUtil.EnsureFile(AppPaths.TestRequestEmailRecipientsCsv, TestRequestEmailRecipientHeaders)
        CsvUtil.EnsureFile(AppPaths.TestRequestEmailEventsCsv, TestRequestEmailEventHeaders)
        CsvUtil.EnsureFile(AppPaths.TestRequestRecordsCsv, TestRequestHeaders)
        CsvUtil.EnsureFile(AppPaths.TestRequestStepsCsv, TestRequestStepHeaders)
        CsvUtil.EnsureFile(AppPaths.TestCatalogCsv, TestCatalogHeaders)
        CsvUtil.EnsureFile(AppPaths.TestGroupsCsv, TestGroupHeaders)
        CsvUtil.EnsureFile(AppPaths.MeasurementDevicesCsv, MeasurementDeviceHeaders)
        CsvUtil.EnsureFile(AppPaths.PackageMeterControlsCsv, PackageMeterControlHeaders)
        CsvUtil.EnsureFile(AppPaths.PackageMeterControlLinesCsv, PackageMeterControlLineHeaders)
        CsvUtil.EnsureFile(AppPaths.PackageMeterEmailRecipientsCsv, PackageMeterEmailRecipientHeaders)
        MigrateControlPointSpcMetadata()
        MigrateMechanismQualityControlExplanations()
    End Sub

    Private Shared Sub EnsureOptionalFile(filePath As String, headers As String(), context As String)
        Try
            CsvUtil.EnsureFile(filePath, headers)
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            ErrorLogService.Log("DataService.EnsureOptionalFile", ex, context & "; Path=" & filePath)
        End Try
    End Sub

    Private Shared Sub MigrateControlPointSpcMetadata()
        Try
            CsvUtil.UpdateRowsLockedIfChanged(
                AppPaths.ControlPointsCsv,
                ControlPointHeaders,
                Function(rows)
                    Dim changed As Boolean = False
                    For Each row In rows
                        Dim measureId = GetValue(row, "MeasureId").Trim()
                        If measureId = "" Then Continue For

                        If GetValue(row, "SpcKey").Trim() = "" Then
                            row("SpcKey") = measureId
                            changed = True
                        End If

                        Dim version As Integer = 0
                        If Not Integer.TryParse(GetValue(row, "MeasureVersion").Trim(), version) OrElse version <= 0 Then
                            row("MeasureVersion") = "1"
                            changed = True
                        End If
                    Next
                    Return changed
                End Function)

            CsvUtil.UpdateRowsLockedIfChanged(
                AppPaths.MeasurementsCsv,
                MeasurementHeaders,
                Function(rows)
                    Dim changed As Boolean = False
                    For Each row In rows
                        Dim measureId = GetValue(row, "MeasureId").Trim()
                        If measureId = "" Then Continue For

                        If GetValue(row, "SpcKey").Trim() = "" Then
                            row("SpcKey") = measureId
                            changed = True
                        End If

                        Dim version As Integer = 0
                        If Not Integer.TryParse(GetValue(row, "MeasureVersion").Trim(), version) OrElse version <= 0 Then
                            row("MeasureVersion") = "1"
                            changed = True
                        End If
                    Next
                    Return changed
                End Function)
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            ErrorLogService.Log("DataService.MigrateControlPointSpcMetadata", ex)
        End Try
    End Sub

    Private Shared Sub MigrateMechanismQualityControlExplanations()
        Try
            CsvUtil.UpdateRowsLockedIfChanged(
                AppPaths.MechanismQualityControlRecordsCsv,
                MechanismQualityControlHeaders,
                Function(rows)
                    Dim changed As Boolean = False

                    For Each row In rows
                        Dim legacyExplanation = GetValue(row, "Explanation").Trim()
                        If legacyExplanation = "" OrElse
                           GetValue(row, "DeliveryExplanation").Trim() <> "" OrElse
                           GetValue(row, "ControlExplanation").Trim() <> "" Then
                            Continue For
                        End If

                        If String.Equals(GetValue(row, "Status"), "COMPLETED", StringComparison.OrdinalIgnoreCase) Then
                            row("ControlExplanation") = legacyExplanation
                        Else
                            row("DeliveryExplanation") = legacyExplanation
                        End If
                        changed = True
                    Next

                    Return changed
                End Function)
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            ErrorLogService.Log("DataService.MigrateMechanismQualityControlExplanations", ex)
        End Try
    End Sub


    Private Shared Function TryParseCsvDate(text As String, ByRef value As DateTime) As Boolean
        Return DateTime.TryParseExact(text,
                                      "yyyy-MM-dd HH:mm:ss",
                                      System.Globalization.CultureInfo.InvariantCulture,
                                      System.Globalization.DateTimeStyles.None,
                                      value) OrElse DateTime.TryParse(text, value)
    End Function

    Private Shared Function IsSessionExpired(row As Dictionary(Of String, String)) As Boolean
        Dim lastSeen As DateTime
        If Not TryParseCsvDate(GetValue(row, "LastSeen"), lastSeen) Then Return True
        Return lastSeen < DateTime.Now.AddMinutes(-10)
    End Function

    Public Shared Function TouchUserSession(sessionId As String) As Boolean
        sessionId = If(sessionId, "").Trim()
        If sessionId = "" Then Return False

        Return CsvUtil.UpdateRowsLocked(
            AppPaths.ActiveSessionsCsv,
            ActiveSessionHeaders,
            Function(rows)
                Dim currentSession = rows.FirstOrDefault(
                    Function(r) String.Equals(GetValue(r, "SessionId"), sessionId, StringComparison.OrdinalIgnoreCase))
                If currentSession Is Nothing Then Return False

                currentSession("LastSeen") = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                Return True
            End Function)
    End Function

    Public Shared Sub RequestUserSessionEnd(sessionId As String,
                                            username As String,
                                            computerName As String,
                                            requestedBy As String,
                                            reason As String)
        sessionId = If(sessionId, "").Trim()
        If sessionId = "" Then Return

        CsvUtil.AppendRowLocked(
            AppPaths.SessionEndRequestsCsv,
            SessionEndRequestHeaders,
            New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                {"RequestId", Guid.NewGuid().ToString("N")},
                {"SessionId", sessionId},
                {"Username", If(username, "").Trim()},
                {"ComputerName", If(computerName, "").Trim()},
                {"RequestedBy", If(requestedBy, "").Trim()},
                {"RequestedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")},
                {"Reason", If(reason, "").Trim()}
            })
    End Sub

    Public Shared Function IsUserSessionEndRequested(sessionId As String) As Boolean
        sessionId = If(sessionId, "").Trim()
        If sessionId = "" Then Return False

        Return CsvUtil.ReadRowsLocked(AppPaths.SessionEndRequestsCsv).
            Any(Function(r) String.Equals(GetValue(r, "SessionId"), sessionId, StringComparison.OrdinalIgnoreCase))
    End Function

    Public Shared Function GetCurrentUserSessionState(sessionId As String,
                                                      username As String,
                                                      computerName As String) As String
        sessionId = If(sessionId, "").Trim()
        username = If(username, "").Trim()
        computerName = If(computerName, "").Trim()
        If sessionId = "" Then Return "SESSION_MISSING"

        Dim rows = CsvUtil.ReadRowsLocked(AppPaths.ActiveSessionsCsv)
        If rows.Any(Function(r) String.Equals(GetValue(r, "SessionId"), sessionId, StringComparison.OrdinalIgnoreCase)) Then
            Return "OK"
        End If

        If username <> "" AndAlso computerName <> "" AndAlso
           rows.Any(
               Function(r) String.Equals(GetValue(r, "Username"), username, StringComparison.OrdinalIgnoreCase) AndAlso
                          String.Equals(GetValue(r, "ComputerName"), computerName, StringComparison.OrdinalIgnoreCase) AndAlso
                          Not String.Equals(GetValue(r, "SessionId"), sessionId, StringComparison.OrdinalIgnoreCase)) Then
            Return "SESSION_REPLACED"
        End If

        Return "SESSION_MISSING"
    End Function

    Public Shared Function EnsureCurrentUserSession(sessionId As String,
                                                    username As String,
                                                    computerName As String) As String
        sessionId = If(sessionId, "").Trim()
        username = If(username, "").Trim()
        computerName = If(computerName, "").Trim()
        If sessionId = "" Then Return "SESSION_MISSING"

        Return CsvUtil.UpdateRowsLocked(
            AppPaths.ActiveSessionsCsv,
            ActiveSessionHeaders,
            Function(rows)
                Dim currentSession = rows.FirstOrDefault(
                    Function(r) String.Equals(GetValue(r, "SessionId"), sessionId, StringComparison.OrdinalIgnoreCase))
                If currentSession IsNot Nothing Then
                    currentSession("LastSeen") = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    Return "OK"
                End If

                If username <> "" AndAlso computerName <> "" AndAlso
                   rows.Any(
                       Function(r) String.Equals(GetValue(r, "Username"), username, StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(GetValue(r, "ComputerName"), computerName, StringComparison.OrdinalIgnoreCase) AndAlso
                                  Not String.Equals(GetValue(r, "SessionId"), sessionId, StringComparison.OrdinalIgnoreCase)) Then
                    Return "SESSION_REPLACED"
                End If

                If IsUserSessionEndRequested(sessionId) Then
                    Return "SESSION_TERMINATED"
                End If

                Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                rows.Add(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                    {"SessionId", sessionId},
                    {"Username", username},
                    {"ComputerName", computerName},
                    {"LoginAt", nowText},
                    {"LastSeen", nowText}
                })
                Return "SESSION_RESTORED"
            End Function)
    End Function

    Public Shared Function IsUserSessionActive(sessionId As String) As Boolean
        sessionId = If(sessionId, "").Trim()
        If sessionId = "" Then Return False

        Return CsvUtil.ReadRowsLocked(AppPaths.ActiveSessionsCsv).
            Any(Function(r) String.Equals(GetValue(r, "SessionId"), sessionId, StringComparison.OrdinalIgnoreCase))
    End Function

    Public Shared Function GetActiveUserSessions() As List(Of Dictionary(Of String, String))
        Return CsvUtil.ReadRows(AppPaths.ActiveSessionsCsv).
            OrderBy(Function(r) GetValue(r, "Username"), StringComparer.CurrentCultureIgnoreCase).
            ThenBy(Function(r) GetValue(r, "ComputerName"), StringComparer.CurrentCultureIgnoreCase).
            Select(
                Function(r)
                    Dim copy = New Dictionary(Of String, String)(r, StringComparer.OrdinalIgnoreCase)
                    copy("IsStale") = If(IsSessionExpired(r), "YES", "NO")
                    Return copy
                End Function).
            ToList()
    End Function

    Public Shared Sub EndUserSession(sessionId As String)
        sessionId = If(sessionId, "").Trim()
        If sessionId = "" Then Return

        CsvUtil.UpdateRowsLocked(
            AppPaths.ActiveSessionsCsv,
            ActiveSessionHeaders,
            Sub(rows)
                rows.RemoveAll(
                    Function(r) String.Equals(GetValue(r, "SessionId"), sessionId, StringComparison.OrdinalIgnoreCase))
            End Sub)
    End Sub

    Public Shared Function GetProducts(Optional activeOnly As Boolean = False) As List(Of ProductInfo)
        Return CsvUtil.ReadRows(AppPaths.ProductsCsv).
            Select(Function(r) New ProductInfo With {
                .TrCode = GetValue(r, "TrCode"),
                .ProductName = GetValue(r, "ProductName"),
                .PlasticCode = GetValue(r, "PlasticCode"),
                .Material = GetValue(r, "Material"),
                .ColorName = GetValue(r, "ColorName"),
                .MoldCavityCount = GetValue(r, "MoldCavityCount"),
                .MoldCode = GetValue(r, "MoldCode"),
                .DrawingRev = GetValue(r, "DrawingRev"),
                .DrawingFile = GetValue(r, "DrawingFile"),
                .DrawingScope = ProductInfo.NormalizeDrawingScope(GetValue(r, "DrawingScope")),
                .IsActive = GetValue(r, "IsActive"),
                .CreatedBy = GetValue(r, "CreatedBy"),
                .CreatedAt = GetValue(r, "CreatedAt")
            }).
            Where(Function(p) Not activeOnly OrElse String.Equals(p.IsActive, "YES", StringComparison.OrdinalIgnoreCase)).
            OrderBy(Function(p) TrCodeNumericSortValue(p.TrCode)).
            ThenBy(Function(p) NormalizeTrCodeForSort(p.TrCode), StringComparer.OrdinalIgnoreCase).
            ThenBy(Function(p) p.DrawingRev, StringComparer.OrdinalIgnoreCase).
            ToList()
    End Function

    Public Shared Function TrCodeNumericSortValue(trCode As String) As Long
        Dim match = Regex.Match(If(trCode, ""), "\d+")
        If Not match.Success Then Return Long.MaxValue

        Dim value As Long
        If Long.TryParse(match.Value, value) Then Return value
        Return Long.MaxValue
    End Function

    Private Shared Function NormalizeTrCodeForSort(trCode As String) As String
        Return Regex.Replace(If(trCode, "").Trim(), "\s+", " ")
    End Function

    Public Shared Sub SaveProduct(newProduct As ProductInfo, setSameTrPassive As Boolean)
        AuthorizationService.Require(AppState.CanOpenTechnicalDrawingAdmin, "Ürün / Teknik Resim Kaydetme")
        If newProduct Is Nothing Then Throw New ArgumentNullException(NameOf(newProduct))

        newProduct.TrCode = If(newProduct.TrCode, "").Trim()
        newProduct.DrawingRev = If(newProduct.DrawingRev, "").Trim()
        newProduct.DrawingFile = If(newProduct.DrawingFile, "").Trim()
        newProduct.DrawingScope = ProductInfo.NormalizeDrawingScope(newProduct.DrawingScope)
        If newProduct.TrCode = "" OrElse newProduct.DrawingRev = "" OrElse newProduct.DrawingFile = "" Then
            Throw New ArgumentException("TR kodu, revizyon ve teknik resim dosyası zorunludur.")
        End If

        CsvUtil.UpdateRowsLocked(
            AppPaths.ProductsCsv,
            ProductHeaders,
            Sub(rows)
                If setSameTrPassive Then
                    For Each r In rows
                        If String.Equals(GetValue(r, "TrCode"), newProduct.TrCode, StringComparison.OrdinalIgnoreCase) AndAlso
                           String.Equals(ProductInfo.NormalizeDrawingScope(GetValue(r, "DrawingScope")), newProduct.DrawingScope, StringComparison.OrdinalIgnoreCase) Then
                            r("IsActive") = "NO"
                        End If
                    Next
                End If

                Dim existing = rows.FirstOrDefault(
                    Function(r) String.Equals(GetValue(r, "TrCode"), newProduct.TrCode, StringComparison.OrdinalIgnoreCase) AndAlso
                                String.Equals(GetValue(r, "DrawingRev"), newProduct.DrawingRev, StringComparison.OrdinalIgnoreCase) AndAlso
                                String.Equals(GetValue(r, "DrawingFile"), newProduct.DrawingFile, StringComparison.OrdinalIgnoreCase) AndAlso
                                String.Equals(ProductInfo.NormalizeDrawingScope(GetValue(r, "DrawingScope")), newProduct.DrawingScope, StringComparison.OrdinalIgnoreCase))
                If existing Is Nothing Then
                    rows.Add(ProductToRow(newProduct))
                Else
                    existing("ProductName") = newProduct.ProductName
                    If newProduct.PlasticCode <> "" Then existing("PlasticCode") = newProduct.PlasticCode
                    If newProduct.Material <> "" Then existing("Material") = newProduct.Material
                    If newProduct.ColorName <> "" Then existing("ColorName") = newProduct.ColorName
                    If newProduct.MoldCavityCount <> "" Then existing("MoldCavityCount") = newProduct.MoldCavityCount
                    If newProduct.MoldCode <> "" Then existing("MoldCode") = newProduct.MoldCode
                    existing("DrawingFile") = newProduct.DrawingFile
                    existing("DrawingScope") = newProduct.DrawingScope
                    existing("IsActive") = newProduct.IsActive
                    existing("CreatedBy") = newProduct.CreatedBy
                    existing("CreatedAt") = newProduct.CreatedAt
                End If
            End Sub)
    End Sub

    Public Shared Sub SaveProductMetadata(product As ProductInfo)
        AuthorizationService.Require(AppState.CanOpenTechnicalDrawingAdmin, "Ürün Bilgilerini Kaydetme")
        If product Is Nothing Then Throw New ArgumentNullException(NameOf(product))

        CsvUtil.UpdateRowsLocked(
            AppPaths.ProductsCsv,
            ProductHeaders,
            Sub(rows)
                Dim existing = rows.FirstOrDefault(
                    Function(r) String.Equals(GetValue(r, "TrCode"), product.TrCode, StringComparison.OrdinalIgnoreCase) AndAlso
                                String.Equals(GetValue(r, "DrawingRev"), product.DrawingRev, StringComparison.OrdinalIgnoreCase) AndAlso
                                (String.IsNullOrWhiteSpace(product.DrawingFile) OrElse String.Equals(GetValue(r, "DrawingFile"), product.DrawingFile, StringComparison.OrdinalIgnoreCase)))

                If existing Is Nothing Then
                    Throw New InvalidOperationException("Ürün/teknik resim kaydı bulunamadı. Önce Ürün / Teknik Resim Yönetimi ekranından teknik resmi kaydediniz.")
                End If

                existing("ProductName") = product.ProductName
                existing("PlasticCode") = product.PlasticCode
                existing("Material") = product.Material
                existing("ColorName") = product.ColorName
                existing("MoldCavityCount") = product.MoldCavityCount
                existing("MoldCode") = product.MoldCode
            End Sub)
    End Sub

    Public Shared Function DeleteProduct(trCode As String, drawingRev As String, deleteEncryptedFile As Boolean, Optional drawingFile As String = "") As String
        AuthorizationService.Require(AppState.IsAdmin, "Teknik Resim Kaydı Silme")

        trCode = If(trCode, "").Trim()
        drawingRev = If(drawingRev, "").Trim()
        drawingFile = If(drawingFile, "").Trim()

        If trCode = "" OrElse drawingRev = "" Then
            Throw New ArgumentException("Silmek için TR Kodu ve Revizyon gereklidir.")
        End If

        Dim deletedFiles As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim deletedScopes As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim removedProductCount As Integer = 0
        Dim scopesStillHavingSameRevision As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        CsvUtil.UpdateRowsLocked(
            AppPaths.ProductsCsv,
            ProductHeaders,
            Sub(productRows)
                For Each r In productRows.ToList()
                    If String.Equals(GetValue(r, "TrCode"), trCode, StringComparison.OrdinalIgnoreCase) AndAlso
                       String.Equals(GetValue(r, "DrawingRev"), drawingRev, StringComparison.OrdinalIgnoreCase) AndAlso
                       (drawingFile = "" OrElse String.Equals(GetValue(r, "DrawingFile"), drawingFile, StringComparison.OrdinalIgnoreCase)) Then
                        Dim deletedFile = GetValue(r, "DrawingFile")
                        If deletedFile <> "" Then deletedFiles.Add(deletedFile)
                        deletedScopes.Add(ProductInfo.NormalizeDrawingScope(GetValue(r, "DrawingScope")))
                        productRows.Remove(r)
                        removedProductCount += 1
                    End If
                Next

                If removedProductCount = 0 Then
                    Throw New InvalidOperationException("Silinecek teknik resim kaydı bulunamadı.")
                End If

                For Each remaining In productRows
                    If String.Equals(GetValue(remaining, "TrCode"), trCode, StringComparison.OrdinalIgnoreCase) AndAlso
                       String.Equals(GetValue(remaining, "DrawingRev"), drawingRev, StringComparison.OrdinalIgnoreCase) Then
                        scopesStillHavingSameRevision.Add(ProductInfo.NormalizeDrawingScope(GetValue(remaining, "DrawingScope")))
                    End If
                Next
            End Sub)

        Dim removedControlCount As Integer = 0
        Dim scopesToRemoveControlPoints = deletedScopes.
            Where(Function(scope) Not scopesStillHavingSameRevision.Contains(scope)).
            ToList()

        If scopesToRemoveControlPoints.Count > 0 Then
            CsvUtil.UpdateRowsLocked(
                AppPaths.ControlPointsCsv,
                ControlPointHeaders,
                Sub(controlRows)
                    For Each r In controlRows.ToList()
                        If String.Equals(GetValue(r, "TrCode"), trCode, StringComparison.OrdinalIgnoreCase) AndAlso
                           String.Equals(GetValue(r, "DrawingRev"), drawingRev, StringComparison.OrdinalIgnoreCase) AndAlso
                           scopesToRemoveControlPoints.Contains(ProductInfo.NormalizeDrawingScope(GetValue(r, "DrawingScope"))) Then
                            controlRows.Remove(r)
                            removedControlCount += 1
                        End If
                    Next
                End Sub)
        End If

        Dim fileResult As String = "Şifreli teknik resim dosyası silinmedi."

        If deleteEncryptedFile AndAlso deletedFiles.Count > 0 Then
            Dim productRowsAfterDelete = CsvUtil.ReadRows(AppPaths.ProductsCsv)
            Dim deletedFileCount As Integer = 0
            Dim skippedFileCount As Integer = 0

            For Each fileName In deletedFiles
                Dim isUsedByOtherRecord =
                    productRowsAfterDelete.Any(Function(r) String.Equals(GetValue(r, "DrawingFile"), fileName, StringComparison.OrdinalIgnoreCase))
                If isUsedByOtherRecord Then
                    skippedFileCount += 1
                    Continue For
                End If

                Dim deletedFilePath = AppPaths.ResolveDrawingFilePath(fileName)
                If File.Exists(deletedFilePath) Then
                    File.Delete(deletedFilePath)
                    deletedFileCount += 1
                Else
                    skippedFileCount += 1
                End If
            Next

            fileResult = $"Şifreli teknik resim dosyası silinen: {deletedFileCount}; silinmeyen/bulunamayan: {skippedFileCount}."
        End If

        Dim controlResult = If(scopesToRemoveControlPoints.Count < deletedScopes.Count,
                               "Aynı TR/revizyonda başka teknik resim kaydı kaldığı için kontrol ölçüleri silinmedi.",
                               "Silinen kontrol ölçüsü: " & removedControlCount)
        Return $"Silinen teknik resim kaydı: {removedProductCount}; {controlResult}; {fileResult}"
    End Function

    Public Shared Function GetControlPoints(trCode As String, drawingRev As String, Optional activeOnly As Boolean = True, Optional drawingScope As String = "") As List(Of ControlPoint)
        Dim normalizedScope = ProductInfo.NormalizeDrawingScope(drawingScope)
        Return CsvUtil.ReadRows(AppPaths.ControlPointsCsv).
            Where(Function(r) String.Equals(GetValue(r, "TrCode"), trCode, StringComparison.OrdinalIgnoreCase) AndAlso
                              String.Equals(GetValue(r, "DrawingRev"), drawingRev, StringComparison.OrdinalIgnoreCase) AndAlso
                              String.Equals(ProductInfo.NormalizeDrawingScope(GetValue(r, "DrawingScope")), normalizedScope, StringComparison.OrdinalIgnoreCase)).
            Select(Function(r) New ControlPoint With {
                .TrCode = GetValue(r, "TrCode"),
                .DrawingRev = GetValue(r, "DrawingRev"),
                .DrawingScope = ProductInfo.NormalizeDrawingScope(GetValue(r, "DrawingScope")),
                .MeasureId = GetValue(r, "MeasureId"),
                .MeasureName = GetValue(r, "MeasureName"),
                .Nominal = NumberUtil.CsvToDec(GetValue(r, "Nominal")),
                .LowerTol = -Math.Abs(NumberUtil.CsvToDec(GetValue(r, "LowerTol"))),
                .UpperTol = Math.Abs(NumberUtil.CsvToDec(GetValue(r, "UpperTol"))),
                .LowerLimit = NumberUtil.CsvToDec(GetValue(r, "Nominal")) - Math.Abs(NumberUtil.CsvToDec(GetValue(r, "LowerTol"))),
                .UpperLimit = NumberUtil.CsvToDec(GetValue(r, "Nominal")) + Math.Abs(NumberUtil.CsvToDec(GetValue(r, "UpperTol"))),
                .PageNo = ToIntDefault(GetValue(r, "PageNo"), 1),
                .XPercent = NumberUtil.CsvToDec(GetValue(r, "XPercent")),
                .YPercent = NumberUtil.CsvToDec(GetValue(r, "YPercent")),
                .Unit = GetValue(r, "Unit"),
                .IsMandatory = GetValue(r, "IsMandatory"),
                .MeasurementGroup = If(String.IsNullOrWhiteSpace(GetValue(r, "MeasurementGroup")), "Genel", GetValue(r, "MeasurementGroup").Trim()),
                .SampleFrequency = If(String.IsNullOrWhiteSpace(GetValue(r, "SampleFrequency")), "Her Kontrol", GetValue(r, "SampleFrequency").Trim()),
                .IsCritical = If(String.Equals(GetValue(r, "IsCritical"), "YES", StringComparison.OrdinalIgnoreCase), "YES", "NO"),
                .SortNo = ToInt(GetValue(r, "SortNo")),
                .IsActive = GetValue(r, "IsActive"),
                .SpcKey = If(String.IsNullOrWhiteSpace(GetValue(r, "SpcKey")), GetValue(r, "MeasureId"), GetValue(r, "SpcKey").Trim()),
                .MeasureVersion = Math.Max(1, ToIntDefault(GetValue(r, "MeasureVersion"), 1)),
                .ValidFrom = GetValue(r, "ValidFrom"),
                .ValidTo = GetValue(r, "ValidTo"),
                .ChangeReason = GetValue(r, "ChangeReason")
            }).
            Where(Function(c) Not activeOnly OrElse String.Equals(c.IsActive, "YES", StringComparison.OrdinalIgnoreCase)).
            OrderBy(Function(c) If(c.SortNo > 0, c.SortNo, Integer.MaxValue)).
            ThenBy(Function(c) c.MeasureId).
            ToList()
    End Function

    Public Shared Function GetActiveControlPointProductKeys() As HashSet(Of String)
        Dim keys = CsvUtil.ReadRows(AppPaths.ControlPointsCsv).
            Where(Function(r) String.Equals(GetValue(r, "IsActive"), "YES", StringComparison.OrdinalIgnoreCase) AndAlso
                              Not String.IsNullOrWhiteSpace(GetValue(r, "TrCode")) AndAlso
                              Not String.IsNullOrWhiteSpace(GetValue(r, "MeasureId"))).
            Select(Function(r) GetControlPointProductKey(GetValue(r, "TrCode"), GetValue(r, "DrawingRev"), GetValue(r, "DrawingScope")))

        Return New HashSet(Of String)(keys, StringComparer.OrdinalIgnoreCase)
    End Function

    Public Shared Function GetControlPointProductKey(trCode As String, drawingRev As String, Optional drawingScope As String = "") As String
        Return If(trCode, "").Trim() & "|" & If(drawingRev, "").Trim() & "|" & ProductInfo.NormalizeDrawingScope(drawingScope)
    End Function

    Public Shared Sub SaveControlPoint(cp As ControlPoint)
        cp.DrawingScope = ProductInfo.NormalizeDrawingScope(cp.DrawingScope)
        NormalizeControlPointSpcMetadata(cp)
        CsvUtil.UpdateRowsLocked(
            AppPaths.ControlPointsCsv,
            ControlPointHeaders,
            Sub(rows)
                Dim existing = rows.FirstOrDefault(
                    Function(r) String.Equals(GetValue(r, "TrCode"), cp.TrCode, StringComparison.OrdinalIgnoreCase) AndAlso
                                String.Equals(GetValue(r, "DrawingRev"), cp.DrawingRev, StringComparison.OrdinalIgnoreCase) AndAlso
                                String.Equals(ProductInfo.NormalizeDrawingScope(GetValue(r, "DrawingScope")), cp.DrawingScope, StringComparison.OrdinalIgnoreCase) AndAlso
                                String.Equals(GetValue(r, "MeasureId"), cp.MeasureId, StringComparison.OrdinalIgnoreCase))
                If existing Is Nothing Then
                    rows.Add(ControlPointToRow(cp))
                Else
                    Dim existingSpcKey = GetValue(existing, "SpcKey").Trim()
                    If existingSpcKey <> "" Then cp.SpcKey = existingSpcKey

                    Dim existingVersion = ToIntDefault(GetValue(existing, "MeasureVersion"), 1)
                    If existingVersion > 0 Then cp.MeasureVersion = existingVersion

                    If String.IsNullOrWhiteSpace(cp.ValidFrom) Then cp.ValidFrom = GetValue(existing, "ValidFrom")
                    If String.IsNullOrWhiteSpace(cp.ValidTo) Then cp.ValidTo = GetValue(existing, "ValidTo")
                    If String.IsNullOrWhiteSpace(cp.ChangeReason) Then cp.ChangeReason = GetValue(existing, "ChangeReason")

                    Dim updated = ControlPointToRow(cp)
                    For Each h In ControlPointHeaders
                        existing(h) = updated(h)
                    Next
                End If
            End Sub)
    End Sub

    Public Shared Sub UpdateControlPointSortNos(controlPoints As IEnumerable(Of ControlPoint))
        AuthorizationService.Require(AppState.CanOpenTechnicalDrawingAdmin, "Kontrol Ölçüsü Sıralama")
        If controlPoints Is Nothing Then Throw New ArgumentNullException(NameOf(controlPoints))

        Dim updates = controlPoints.
            Where(Function(point) point IsNot Nothing AndAlso
                                  Not String.IsNullOrWhiteSpace(point.TrCode) AndAlso
                                  Not String.IsNullOrWhiteSpace(point.MeasureId)).
            ToDictionary(
                Function(point) (If(point.TrCode, "").Trim() & "|" & If(point.DrawingRev, "").Trim() & "|" & ProductInfo.NormalizeDrawingScope(point.DrawingScope) & "|" & If(point.MeasureId, "").Trim()).ToUpperInvariant(),
                Function(point) point.SortNo)

        If updates.Count = 0 Then Return

        CsvUtil.UpdateRowsLocked(
            AppPaths.ControlPointsCsv,
            ControlPointHeaders,
            Sub(rows)
                For Each row In rows
                    Dim key = (GetValue(row, "TrCode").Trim() & "|" &
                               GetValue(row, "DrawingRev").Trim() & "|" &
                               ProductInfo.NormalizeDrawingScope(GetValue(row, "DrawingScope")) & "|" &
                               GetValue(row, "MeasureId").Trim()).ToUpperInvariant()
                    If updates.ContainsKey(key) Then
                        row("SortNo") = updates(key).ToString()
                    End If
                Next
            End Sub)
    End Sub

    Public Shared Sub SaveControlPointsBulk(controlPoints As IEnumerable(Of ControlPoint))
        AuthorizationService.Require(AppState.CanOpenTechnicalDrawingAdmin, "DWG/DXF Kontrol Ölçüsü Aktarımı")
        If controlPoints Is Nothing Then Throw New ArgumentNullException(NameOf(controlPoints))

        Dim points = controlPoints.ToList()
        If points.Count = 0 Then Throw New ArgumentException("Kaydedilecek kontrol ölçüsü bulunamadı.", NameOf(controlPoints))

        Dim duplicateKeys = points.
            GroupBy(
                Function(point) (If(point.TrCode, "").Trim() & "|" &
                                 If(point.DrawingRev, "").Trim() & "|" &
                                 ProductInfo.NormalizeDrawingScope(point.DrawingScope) & "|" &
                                 If(point.MeasureId, "").Trim()).ToUpperInvariant()).
            Where(Function(group) group.Key = "||" OrElse group.Count() > 1).
            Select(Function(group) group.Key).
            ToList()
        If duplicateKeys.Count > 0 Then
            Throw New InvalidOperationException("Toplu aktarımda boş veya mükerrer ölçü numarası bulunuyor.")
        End If

        CsvUtil.UpdateRowsLocked(
            AppPaths.ControlPointsCsv,
            ControlPointHeaders,
            Sub(rows)
                For Each point In points
                    NormalizeControlPointSpcMetadata(point)
                    If String.IsNullOrWhiteSpace(point.TrCode) OrElse
                       String.IsNullOrWhiteSpace(point.MeasureId) Then
                        Throw New InvalidOperationException("TR kodu ve ölçü numarası zorunludur.")
                    End If

                    Dim existing = rows.FirstOrDefault(
                        Function(row) String.Equals(GetValue(row, "TrCode"), point.TrCode, StringComparison.OrdinalIgnoreCase) AndAlso
                                      String.Equals(GetValue(row, "DrawingRev"), point.DrawingRev, StringComparison.OrdinalIgnoreCase) AndAlso
                                      String.Equals(ProductInfo.NormalizeDrawingScope(GetValue(row, "DrawingScope")), ProductInfo.NormalizeDrawingScope(point.DrawingScope), StringComparison.OrdinalIgnoreCase) AndAlso
                                      String.Equals(GetValue(row, "MeasureId"), point.MeasureId, StringComparison.OrdinalIgnoreCase))
                    If existing IsNot Nothing Then
                        Throw New InvalidOperationException("Ölçü numarası zaten mevcut: " & point.MeasureId)
                    End If

                    rows.Add(ControlPointToRow(point))
                Next
            End Sub)
    End Sub

    Public Shared Sub SetControlPointPassive(trCode As String, drawingRev As String, measureId As String, Optional drawingScope As String = "")
        Dim normalizedScope = ProductInfo.NormalizeDrawingScope(drawingScope)
        CsvUtil.UpdateRowsLocked(
            AppPaths.ControlPointsCsv,
            ControlPointHeaders,
            Sub(rows)
                For Each r In rows
                    If String.Equals(GetValue(r, "TrCode"), trCode, StringComparison.OrdinalIgnoreCase) AndAlso
                       String.Equals(GetValue(r, "DrawingRev"), drawingRev, StringComparison.OrdinalIgnoreCase) AndAlso
                       String.Equals(ProductInfo.NormalizeDrawingScope(GetValue(r, "DrawingScope")), normalizedScope, StringComparison.OrdinalIgnoreCase) AndAlso
                       String.Equals(GetValue(r, "MeasureId"), measureId, StringComparison.OrdinalIgnoreCase) Then
                        r("IsActive") = "NO"
                        If GetValue(r, "ValidTo").Trim() = "" Then r("ValidTo") = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    End If
                Next
            End Sub)
    End Sub

    Public Shared Sub DeleteUnusedControlPoint(trCode As String, drawingRev As String, measureId As String, Optional drawingScope As String = "")
        AuthorizationService.Require(AppState.CanOpenTechnicalDrawingAdmin, "Kontrol Ölçüsü Silme")

        trCode = If(trCode, "").Trim()
        drawingRev = If(drawingRev, "").Trim()
        measureId = If(measureId, "").Trim()
        drawingScope = ProductInfo.NormalizeDrawingScope(drawingScope)
        If trCode = "" OrElse measureId = "" Then
            Throw New ArgumentException("TR kodu ve ölçü numarası zorunludur.")
        End If

        CsvUtil.UpdateTwoFilesLocked(
            AppPaths.ControlPointsCsv,
            ControlPointHeaders,
            AppPaths.MeasurementsCsv,
            MeasurementHeaders,
            Sub(controlPointRows, measurementRows)
                Dim wasUsed = measurementRows.Any(
                    Function(row) String.Equals(GetValue(row, "TrCode"), trCode, StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(GetValue(row, "DrawingRev"), drawingRev, StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(ProductInfo.NormalizeDrawingScope(GetValue(row, "DrawingScope")), drawingScope, StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(GetValue(row, "MeasureId"), measureId, StringComparison.OrdinalIgnoreCase))
                If wasUsed Then
                    Throw New InvalidOperationException(
                        "Bu ölçü daha önce bir ölçüm kaydında kullanılmıştır ve silinemez." & Environment.NewLine &
                        "Geçmiş kayıtların bütünlüğünü korumak için ölçüyü pasif yapabilirsiniz.")
                End If

                Dim removedCount = controlPointRows.RemoveAll(
                    Function(row) String.Equals(GetValue(row, "TrCode"), trCode, StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(GetValue(row, "DrawingRev"), drawingRev, StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(ProductInfo.NormalizeDrawingScope(GetValue(row, "DrawingScope")), drawingScope, StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(GetValue(row, "MeasureId"), measureId, StringComparison.OrdinalIgnoreCase))
                If removedCount = 0 Then
                    Throw New InvalidOperationException("Silinecek kontrol ölçüsü bulunamadı.")
                End If
            End Sub)
    End Sub

    Public Shared Function ReviseControlPoint(trCode As String,
                                              drawingRev As String,
                                              measureId As String,
                                              drawingScope As String,
                                              revisedPoint As ControlPoint,
                                              changeReason As String) As ControlPoint
        AuthorizationService.Require(AppState.CanOpenTechnicalDrawingAdmin, "Kontrol Ölçüsü Revizyonu")

        trCode = If(trCode, "").Trim()
        drawingRev = If(drawingRev, "").Trim()
        measureId = If(measureId, "").Trim()
        drawingScope = ProductInfo.NormalizeDrawingScope(drawingScope)
        If trCode = "" OrElse measureId = "" Then Throw New ArgumentException("Revize edilecek ölçü seçilmelidir.")
        If revisedPoint Is Nothing Then Throw New ArgumentNullException(NameOf(revisedPoint))

        Dim created As ControlPoint = Nothing
        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")

        CsvUtil.UpdateRowsLocked(
            AppPaths.ControlPointsCsv,
            ControlPointHeaders,
            Sub(rows)
                Dim existing = rows.FirstOrDefault(
                    Function(row) String.Equals(GetValue(row, "TrCode"), trCode, StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(GetValue(row, "DrawingRev"), drawingRev, StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(ProductInfo.NormalizeDrawingScope(GetValue(row, "DrawingScope")), drawingScope, StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(GetValue(row, "MeasureId"), measureId, StringComparison.OrdinalIgnoreCase))
                If existing Is Nothing Then Throw New InvalidOperationException("Revize edilecek kontrol ölçüsü bulunamadı.")

                Dim spcKey = GetValue(existing, "SpcKey").Trim()
                If spcKey = "" Then spcKey = GetValue(existing, "MeasureId").Trim()

                Dim nextVersion = rows.
                    Where(Function(row) String.Equals(GetValue(row, "TrCode"), trCode, StringComparison.OrdinalIgnoreCase) AndAlso
                                        String.Equals(GetValue(row, "DrawingRev"), drawingRev, StringComparison.OrdinalIgnoreCase) AndAlso
                                        String.Equals(ProductInfo.NormalizeDrawingScope(GetValue(row, "DrawingScope")), drawingScope, StringComparison.OrdinalIgnoreCase) AndAlso
                                        String.Equals(If(String.IsNullOrWhiteSpace(GetValue(row, "SpcKey")), GetValue(row, "MeasureId"), GetValue(row, "SpcKey")).Trim(), spcKey, StringComparison.OrdinalIgnoreCase)).
                    Select(Function(row) Math.Max(1, ToIntDefault(GetValue(row, "MeasureVersion"), 1))).
                    DefaultIfEmpty(1).
                    Max() + 1

                existing("IsActive") = "NO"
                If GetValue(existing, "ValidTo").Trim() = "" Then existing("ValidTo") = nowText
                If changeReason.Trim() <> "" Then existing("ChangeReason") = changeReason.Trim()

                revisedPoint.TrCode = trCode
                revisedPoint.DrawingRev = drawingRev
                revisedPoint.DrawingScope = drawingScope
                revisedPoint.SpcKey = spcKey
                revisedPoint.MeasureVersion = nextVersion
                revisedPoint.ValidFrom = nowText
                revisedPoint.ValidTo = ""
                revisedPoint.ChangeReason = changeReason.Trim()
                revisedPoint.IsActive = "YES"
                revisedPoint.MeasureId = BuildRevisionMeasureId(rows, trCode, drawingRev, drawingScope, measureId, nextVersion)
                NormalizeControlPointSpcMetadata(revisedPoint)

                rows.Add(ControlPointToRow(revisedPoint))
                created = revisedPoint
            End Sub)

        Return created
    End Function

    Private Shared Function BuildRevisionMeasureId(rows As List(Of Dictionary(Of String, String)),
                                                   trCode As String,
                                                   drawingRev As String,
                                                   drawingScope As String,
                                                   baseMeasureId As String,
                                                   version As Integer) As String
        Dim baseId = Regex.Replace(If(baseMeasureId, "").Trim(), "-R\d+(-\d+)?$", "", RegexOptions.IgnoreCase)
        If baseId = "" Then baseId = "OLCU"

        Dim suffix As Integer = 0
        Do
            Dim candidate = baseId & "-R" & version.ToString()
            If suffix > 0 Then candidate &= "-" & suffix.ToString()

            Dim exists = rows.Any(
                Function(row) String.Equals(GetValue(row, "TrCode"), trCode, StringComparison.OrdinalIgnoreCase) AndAlso
                              String.Equals(GetValue(row, "DrawingRev"), drawingRev, StringComparison.OrdinalIgnoreCase) AndAlso
                              String.Equals(ProductInfo.NormalizeDrawingScope(GetValue(row, "DrawingScope")), drawingScope, StringComparison.OrdinalIgnoreCase) AndAlso
                              String.Equals(GetValue(row, "MeasureId"), candidate, StringComparison.OrdinalIgnoreCase))
            If Not exists Then Return candidate
            suffix += 1
        Loop
    End Function

    Public Shared Function GetMeasurementGroupAreas(trCode As String, drawingRev As String, Optional drawingScope As String = "") As List(Of MeasurementGroupArea)
        Dim normalizedScope = ProductInfo.NormalizeDrawingScope(drawingScope)
        Return CsvUtil.ReadRows(AppPaths.MeasurementGroupAreasCsv).
            Where(Function(row) String.Equals(GetValue(row, "TrCode"), trCode, StringComparison.OrdinalIgnoreCase) AndAlso
                                String.Equals(GetValue(row, "DrawingRev"), drawingRev, StringComparison.OrdinalIgnoreCase) AndAlso
                                String.Equals(ProductInfo.NormalizeDrawingScope(GetValue(row, "DrawingScope")), normalizedScope, StringComparison.OrdinalIgnoreCase)).
            Select(Function(row) New MeasurementGroupArea With {
                .TrCode = GetValue(row, "TrCode"),
                .DrawingRev = GetValue(row, "DrawingRev"),
                .DrawingScope = ProductInfo.NormalizeDrawingScope(GetValue(row, "DrawingScope")),
                .GroupName = GetValue(row, "GroupName"),
                .PageNo = ToIntDefault(GetValue(row, "PageNo"), 1),
                .LeftPercent = NumberUtil.CsvToDec(GetValue(row, "LeftPercent")),
                .TopPercent = NumberUtil.CsvToDec(GetValue(row, "TopPercent")),
                .RightPercent = NumberUtil.CsvToDec(GetValue(row, "RightPercent")),
                .BottomPercent = NumberUtil.CsvToDec(GetValue(row, "BottomPercent")),
                .UpdatedBy = GetValue(row, "UpdatedBy"),
                .UpdatedAt = GetValue(row, "UpdatedAt")
            }).
            OrderBy(Function(area) area.GroupName, StringComparer.CurrentCultureIgnoreCase).
            ToList()
    End Function

    Public Shared Sub SaveMeasurementGroupArea(area As MeasurementGroupArea)
        AuthorizationService.Require(AppState.CanOpenTechnicalDrawingAdmin, "Ölçüm Grubu Alanı Tanımlama")
        If area Is Nothing Then Throw New ArgumentNullException(NameOf(area))

        area.TrCode = If(area.TrCode, "").Trim()
        area.DrawingRev = If(area.DrawingRev, "").Trim()
        area.DrawingScope = ProductInfo.NormalizeDrawingScope(area.DrawingScope)
        area.GroupName = If(area.GroupName, "").Trim()
        If area.TrCode = "" OrElse area.GroupName = "" Then
            Throw New ArgumentException("TR kodu ve ölçüm grubu zorunludur.")
        End If

        area.PageNo = Math.Max(1, area.PageNo)
        area.LeftPercent = Math.Max(0D, Math.Min(100D, area.LeftPercent))
        area.TopPercent = Math.Max(0D, Math.Min(100D, area.TopPercent))
        area.RightPercent = Math.Max(0D, Math.Min(100D, area.RightPercent))
        area.BottomPercent = Math.Max(0D, Math.Min(100D, area.BottomPercent))
        If area.RightPercent <= area.LeftPercent OrElse area.BottomPercent <= area.TopPercent Then
            Throw New ArgumentException("Ölçüm grubu alanı geçerli bir dikdörtgen olmalıdır.")
        End If

        area.UpdatedBy = AppState.CurrentUserName
        area.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")

        CsvUtil.UpdateRowsLocked(
            AppPaths.MeasurementGroupAreasCsv,
            MeasurementGroupAreaHeaders,
            Sub(rows)
                Dim existing = rows.FirstOrDefault(
                    Function(row) String.Equals(GetValue(row, "TrCode"), area.TrCode, StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(GetValue(row, "DrawingRev"), area.DrawingRev, StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(ProductInfo.NormalizeDrawingScope(GetValue(row, "DrawingScope")), area.DrawingScope, StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(GetValue(row, "GroupName"), area.GroupName, StringComparison.OrdinalIgnoreCase))
                Dim updated = MeasurementGroupAreaToRow(area)
                If existing Is Nothing Then
                    rows.Add(updated)
                Else
                    For Each header In MeasurementGroupAreaHeaders
                        existing(header) = updated(header)
                    Next
                End If
            End Sub)
    End Sub

    Public Shared Sub DeleteMeasurementGroupArea(trCode As String, drawingRev As String, groupName As String, Optional drawingScope As String = "")
        AuthorizationService.Require(AppState.CanOpenTechnicalDrawingAdmin, "Ölçüm Grubu Alanı Silme")
        Dim normalizedScope = ProductInfo.NormalizeDrawingScope(drawingScope)
        CsvUtil.UpdateRowsLocked(
            AppPaths.MeasurementGroupAreasCsv,
            MeasurementGroupAreaHeaders,
            Sub(rows)
                rows.RemoveAll(
                    Function(row) String.Equals(GetValue(row, "TrCode"), trCode, StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(GetValue(row, "DrawingRev"), drawingRev, StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(ProductInfo.NormalizeDrawingScope(GetValue(row, "DrawingScope")), normalizedScope, StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(GetValue(row, "GroupName"), groupName, StringComparison.OrdinalIgnoreCase))
            End Sub)
    End Sub

    Public Shared Sub AppendMeasurement(row As Dictionary(Of String, String))
        AuthorizationService.Require(AppState.CanOpenMeasurement, "Ölçüm Kaydı Oluşturma")
        If row Is Nothing Then Throw New ArgumentNullException(NameOf(row))
        If GetValue(row, "SpcKey").Trim() = "" Then row("SpcKey") = GetValue(row, "MeasureId").Trim()
        If GetValue(row, "MeasureVersion").Trim() = "" Then row("MeasureVersion") = "1"
        CsvUtil.AppendRowLocked(AppPaths.MeasurementsCsv, MeasurementHeaders, row)
    End Sub

    Public Shared Sub AppendVisualControl(row As Dictionary(Of String, String))
        AuthorizationService.Require(AppState.CanOpenMeasurement, "Görsel Kontrol Kaydı Oluşturma")
        CsvUtil.AppendRowLocked(AppPaths.VisualControlsCsv, VisualControlHeaders, row)
    End Sub

    Public Shared Sub AppendClosedEye(row As Dictionary(Of String, String))
        AuthorizationService.Require(AppState.CanOpenMeasurement, "Kapalı Göz Kaydı Oluşturma")
        CsvUtil.AppendRowLocked(AppPaths.ClosedEyesCsv, ClosedEyeHeaders, row)
    End Sub

    Public Shared Function GetClosedEyeRows() As List(Of Dictionary(Of String, String))
        Return CsvUtil.ReadRows(AppPaths.ClosedEyesCsv)
    End Function

    Public Shared Function GetVisualControlRows() As List(Of Dictionary(Of String, String))
        Return CsvUtil.ReadRows(AppPaths.VisualControlsCsv)
    End Function

    Public Shared Function GetMeasurementRows() As List(Of Dictionary(Of String, String))
        Return CsvUtil.ReadRows(AppPaths.MeasurementsCsv)
    End Function

    Public Shared Function CorrectSpcHistoricalLimits(trCode As String,
                                                       drawingRev As String,
                                                       drawingScope As String,
                                                       spcKey As String,
                                                       dateFrom As DateTime?,
                                                       dateTo As DateTime?,
                                                       newNominal As Decimal,
                                                       newLowerLimit As Decimal,
                                                       newUpperLimit As Decimal,
                                                       correctionReason As String) As SpcLimitCorrectionResult
        AuthorizationService.Require(AppState.IsAdmin, "SPC Geçmiş Limit Düzeltme")

        trCode = If(trCode, "").Trim()
        drawingRev = If(drawingRev, "").Trim()
        drawingScope = ProductInfo.NormalizeDrawingScope(drawingScope)
        spcKey = If(spcKey, "").Trim()
        correctionReason = If(correctionReason, "").Trim()

        If trCode = "" OrElse spcKey = "" Then Throw New ArgumentException("TR kodu ve SPC ölçü anahtarı zorunludur.")
        If newUpperLimit <= newLowerLimit Then Throw New ArgumentException("Üst limit, alt limitten büyük olmalıdır.")
        If newNominal < newLowerLimit OrElse newNominal > newUpperLimit Then
            Throw New ArgumentException("Nominal değer alt ve üst limit arasında olmalıdır.")
        End If
        If correctionReason = "" Then Throw New ArgumentException("Geçmiş limit düzeltme nedeni zorunludur.")
        If correctionReason.Length > 500 Then correctionReason = correctionReason.Substring(0, 500)
        If dateFrom.HasValue AndAlso dateTo.HasValue AndAlso dateFrom.Value.Date > dateTo.Value.Date Then
            Throw New ArgumentException("Başlangıç tarihi bitiş tarihinden sonra olamaz.")
        End If

        Dim result As New SpcLimitCorrectionResult With {
            .CorrectionId = "SLC-" & DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") & "-" & Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant()
        }
        Dim oldLimits As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim changedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")

        CsvUtil.UpdateTwoFilesLocked(
            AppPaths.MeasurementsCsv,
            MeasurementHeaders,
            AppPaths.SpcLimitCorrectionsCsv,
            SpcLimitCorrectionHeaders,
            Sub(measurementRows, correctionRows)
                For Each row In measurementRows
                    If Not String.Equals(GetValue(row, "TrCode").Trim(), trCode, StringComparison.OrdinalIgnoreCase) OrElse
                       Not String.Equals(GetValue(row, "DrawingRev").Trim(), drawingRev, StringComparison.OrdinalIgnoreCase) OrElse
                       Not String.Equals(ProductInfo.NormalizeDrawingScope(GetValue(row, "DrawingScope")), drawingScope, StringComparison.OrdinalIgnoreCase) Then
                        Continue For
                    End If

                    Dim rowSpcKey = GetValue(row, "SpcKey").Trim()
                    If rowSpcKey = "" Then rowSpcKey = GetValue(row, "MeasureId").Trim()
                    If Not String.Equals(rowSpcKey, spcKey, StringComparison.OrdinalIgnoreCase) Then Continue For

                    Dim measurementDate As DateTime
                    If dateFrom.HasValue OrElse dateTo.HasValue Then
                        If Not DateTime.TryParse(GetValue(row, "MeasurementDate"), measurementDate) Then Continue For
                        If dateFrom.HasValue AndAlso measurementDate.Date < dateFrom.Value.Date Then Continue For
                        If dateTo.HasValue AndAlso measurementDate.Date > dateTo.Value.Date Then Continue For
                    End If

                    Dim oldNominal = GetValue(row, "Nominal")
                    Dim oldLower = GetValue(row, "LowerLimit")
                    Dim oldUpper = GetValue(row, "UpperLimit")
                    Dim oldResult = GetValue(row, "Result")
                    oldLimits.Add(oldNominal & " / " & oldLower & " - " & oldUpper)

                    Dim measuredValue As Decimal
                    Dim newResult = oldResult
                    If NumberUtil.TryParseDecimal(GetValue(row, "MeasuredValue"), measuredValue) Then
                        newResult = If(measuredValue >= newLowerLimit AndAlso measuredValue <= newUpperLimit, "OK", "NOK")
                    End If

                    Dim limitsChanged = Not String.Equals(oldNominal, NumberUtil.DecToCsv(newNominal), StringComparison.OrdinalIgnoreCase) OrElse
                                        Not String.Equals(oldLower, NumberUtil.DecToCsv(newLowerLimit), StringComparison.OrdinalIgnoreCase) OrElse
                                        Not String.Equals(oldUpper, NumberUtil.DecToCsv(newUpperLimit), StringComparison.OrdinalIgnoreCase)
                    Dim resultChanged = Not String.Equals(oldResult, newResult, StringComparison.OrdinalIgnoreCase)
                    If Not limitsChanged AndAlso Not resultChanged Then Continue For

                    row("Nominal") = NumberUtil.DecToCsv(newNominal)
                    row("LowerLimit") = NumberUtil.DecToCsv(newLowerLimit)
                    row("UpperLimit") = NumberUtil.DecToCsv(newUpperLimit)
                    row("Result") = newResult
                    result.AffectedRows += 1
                    If resultChanged Then result.ResultChangedRows += 1
                Next

                If result.AffectedRows = 0 Then
                    Throw New InvalidOperationException("Seçili seri ve tarih aralığında düzeltilecek ölçüm satırı bulunamadı veya limitler zaten aynı.")
                End If

                correctionRows.Add(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                    {"CorrectionId", result.CorrectionId},
                    {"TrCode", trCode},
                    {"DrawingRev", drawingRev},
                    {"DrawingScope", drawingScope},
                    {"SpcKey", spcKey},
                    {"DateFrom", If(dateFrom.HasValue, dateFrom.Value.ToString("yyyy-MM-dd"), "")},
                    {"DateTo", If(dateTo.HasValue, dateTo.Value.ToString("yyyy-MM-dd"), "")},
                    {"OldLimits", String.Join(" | ", oldLimits.Take(10))},
                    {"NewNominal", NumberUtil.DecToCsv(newNominal)},
                    {"NewLowerLimit", NumberUtil.DecToCsv(newLowerLimit)},
                    {"NewUpperLimit", NumberUtil.DecToCsv(newUpperLimit)},
                    {"AffectedRows", result.AffectedRows.ToString()},
                    {"ResultChangedRows", result.ResultChangedRows.ToString()},
                    {"Reason", correctionReason},
                    {"ChangedBy", If(AppState.CurrentUserName, "").Trim()},
                    {"ChangedAt", changedAt},
                    {"ComputerName", Environment.MachineName}
                })
            End Sub)

        AuditService.Log(
            "SPC_HISTORICAL_LIMIT_CORRECT",
            trCode,
            drawingRev,
            "CorrectionId=" & result.CorrectionId &
            "; Scope=" & drawingScope &
            "; SpcKey=" & spcKey &
            "; Nominal=" & NumberUtil.DecToCsv(newNominal) &
            "; Limits=" & NumberUtil.DecToCsv(newLowerLimit) & "-" & NumberUtil.DecToCsv(newUpperLimit) &
            "; Rows=" & result.AffectedRows.ToString() &
            "; ResultChanges=" & result.ResultChangedRows.ToString() &
            "; Reason=" & correctionReason)

        Return result
    End Function

    Public Shared Function CorrectMeasurementValue(recordId As String,
                                                    eyeNo As String,
                                                    measureId As String,
                                                    measurementDate As String,
                                                    newValueText As String,
                                                    correctionReason As String) As String
        AuthorizationService.Require(AppState.IsAdmin, "Geçmiş Ölçüm Düzeltme")

        recordId = If(recordId, "").Trim()
        eyeNo = If(eyeNo, "").Trim()
        measureId = If(measureId, "").Trim()
        measurementDate = If(measurementDate, "").Trim()
        correctionReason = If(correctionReason, "").Trim()
        If recordId = "" Then Throw New ArgumentException("Kayıt numarası boş olamaz.")
        If measureId = "" Then Throw New ArgumentException("Ölçü numarası boş olamaz.")
        If correctionReason = "" Then Throw New ArgumentException("Düzeltme nedeni zorunludur.")
        If correctionReason.Length > 500 Then correctionReason = correctionReason.Substring(0, 500)

        Dim newValue As Decimal = 0D
        If Not NumberUtil.TryParseDecimal(newValueText, newValue) Then
            Throw New ArgumentException("Yeni ölçüm değeri geçerli bir sayı olmalıdır.")
        End If

        Dim oldValue As String = ""
        Dim oldResult As String = ""
        Dim newResult As String = ""
        Dim trCode As String = ""
        Dim drawingRev As String = ""
        Dim measureName As String = ""
        Dim storedMeasurementDate As String = ""
        Dim changed As Boolean = False

        CsvUtil.UpdateRowsLocked(
            AppPaths.MeasurementsCsv,
            MeasurementHeaders,
            Sub(rows)
                Dim candidates = rows.Where(
                    Function(row) String.Equals(GetValue(row, "RecordId").Trim(), recordId, StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(GetValue(row, "EyeNo").Trim(), eyeNo, StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(GetValue(row, "MeasureId").Trim(), measureId, StringComparison.OrdinalIgnoreCase)).ToList()

                If candidates.Count > 1 AndAlso measurementDate <> "" Then
                    candidates = candidates.Where(
                        Function(row) String.Equals(GetValue(row, "MeasurementDate").Trim(), measurementDate, StringComparison.OrdinalIgnoreCase)).ToList()
                End If
                If candidates.Count = 0 Then Throw New InvalidOperationException("Düzeltilecek ölçüm satırı bulunamadı.")
                If candidates.Count > 1 Then Throw New InvalidOperationException("Birden fazla ölçüm satırı eşleşti. Kayıt güvenli biçimde düzeltilemedi.")

                Dim target = candidates(0)
                oldValue = GetValue(target, "MeasuredValue")
                oldResult = GetValue(target, "Result")
                Dim oldDecimal As Decimal = 0D
                If NumberUtil.TryParseDecimal(oldValue, oldDecimal) AndAlso oldDecimal = newValue Then
                    Throw New InvalidOperationException("Yeni değer mevcut değer ile aynıdır.")
                End If

                Dim lowerLimit As Decimal = 0D
                Dim upperLimit As Decimal = 0D
                If Not NumberUtil.TryParseDecimal(GetValue(target, "LowerLimit"), lowerLimit) OrElse
                   Not NumberUtil.TryParseDecimal(GetValue(target, "UpperLimit"), upperLimit) Then
                    Throw New InvalidDataException("Ölçüm limitleri okunamadığı için sonuç yeniden hesaplanamadı.")
                End If

                newResult = If(newValue >= lowerLimit AndAlso newValue <= upperLimit, "OK", "NOK")
                target("MeasuredValue") = NumberUtil.DecToCsv(newValue)
                target("Result") = newResult
                trCode = GetValue(target, "TrCode")
                drawingRev = GetValue(target, "DrawingRev")
                measureName = GetValue(target, "MeasureName")
                storedMeasurementDate = GetValue(target, "MeasurementDate")
                changed = True
            End Sub)

        If Not changed Then Throw New InvalidOperationException("Ölçüm değeri değiştirilemedi.")

        Dim correctionId = "MC-" & DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") & "-" & Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant()
        CsvUtil.AppendRowLocked(
            AppPaths.MeasurementCorrectionsCsv,
            MeasurementCorrectionHeaders,
            New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                {"CorrectionId", correctionId},
                {"RecordId", recordId},
                {"TrCode", trCode},
                {"DrawingRev", drawingRev},
                {"EyeNo", eyeNo},
                {"MeasureId", measureId},
                {"MeasureName", measureName},
                {"MeasurementDate", storedMeasurementDate},
                {"OldValue", oldValue},
                {"NewValue", NumberUtil.DecToCsv(newValue)},
                {"OldResult", oldResult},
                {"NewResult", newResult},
                {"Reason", correctionReason},
                {"ChangedBy", If(AppState.CurrentUserName, "").Trim()},
                {"ChangedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")},
                {"ComputerName", Environment.MachineName}
            })

        AuditService.Log(
            "MEASUREMENT_VALUE_CORRECT",
            trCode,
            drawingRev,
            "CorrectionId=" & correctionId &
            "; RecordId=" & recordId &
            "; EyeNo=" & eyeNo &
            "; MeasureId=" & measureId &
            "; OldValue=" & oldValue &
            "; NewValue=" & NumberUtil.DecToCsv(newValue) &
            "; OldResult=" & oldResult &
            "; NewResult=" & newResult &
            "; Reason=" & correctionReason)

        Return newResult
    End Function

    Public Shared Sub DeleteMeasurementRecord(recordId As String,
                                              ByRef deletedMeasurementRows As Integer,
                                              ByRef deletedVisualRows As Integer,
                                              ByRef deletedClosedEyeRows As Integer)
        recordId = If(recordId, "").Trim()
        If recordId = "" Then Throw New ArgumentException("Ölçüm kayıt numarası boş olamaz.", NameOf(recordId))

        Dim measurementDeleteCount As Integer = 0
        Dim visualDeleteCount As Integer = 0
        Dim closedEyeDeleteCount As Integer = 0

        CsvUtil.UpdateRowsLocked(
            AppPaths.MeasurementsCsv,
            MeasurementHeaders,
            Sub(rows)
                measurementDeleteCount = rows.RemoveAll(
                    Function(r) String.Equals(GetValue(r, "RecordId"), recordId, StringComparison.OrdinalIgnoreCase))
            End Sub)

        CsvUtil.UpdateRowsLocked(
            AppPaths.VisualControlsCsv,
            VisualControlHeaders,
            Sub(rows)
                visualDeleteCount = rows.RemoveAll(
                    Function(r) String.Equals(GetValue(r, "RecordId"), recordId, StringComparison.OrdinalIgnoreCase))
            End Sub)

        CsvUtil.UpdateRowsLocked(
            AppPaths.ClosedEyesCsv,
            ClosedEyeHeaders,
            Sub(rows)
                closedEyeDeleteCount = rows.RemoveAll(
                    Function(r) String.Equals(GetValue(r, "RecordId"), recordId, StringComparison.OrdinalIgnoreCase))
            End Sub)

        deletedMeasurementRows = measurementDeleteCount
        deletedVisualRows = visualDeleteCount
        deletedClosedEyeRows = closedEyeDeleteCount

        If measurementDeleteCount + visualDeleteCount + closedEyeDeleteCount = 0 Then
            Throw New InvalidOperationException("Silinecek ölçüm kaydı bulunamadı.")
        End If
    End Sub


    Public Shared Sub AppendMoldBindingRecord(row As Dictionary(Of String, String))
        AuthorizationService.Require(AppState.CanOpenProductionBinding, "Kalıp Bağlama Kaydı Oluşturma")
        CsvUtil.AppendRowLocked(AppPaths.MoldBindingRecordsCsv, MoldBindingHeaders, row)
    End Sub

    Public Shared Function GetMoldBindingRecords() As List(Of Dictionary(Of String, String))
        Return CsvUtil.ReadRows(AppPaths.MoldBindingRecordsCsv)
    End Function

    Public Shared Function GetMoldConnectionPlanRows() As List(Of Dictionary(Of String, String))
        Return CsvUtil.ReadRows(AppPaths.MoldConnectionPlanCsv)
    End Function

    Public Shared Sub ReplaceMoldConnectionPlanRows(rows As List(Of Dictionary(Of String, String)))
        AuthorizationService.Require(AppState.CanModifyMoldConnectionPlan, "Bağlanacak Kalıp Listesi Aktarımı")
        Dim replacementRows = rows.Select(
            Function(r) New Dictionary(Of String, String)(r, StringComparer.OrdinalIgnoreCase)).ToList()

        CsvUtil.UpdateRowsLocked(
            AppPaths.MoldConnectionPlanCsv,
            MoldConnectionPlanHeaders,
            Sub(currentRows)
                currentRows.Clear()
                currentRows.AddRange(replacementRows)
            End Sub)
    End Sub

    Public Shared Sub AppendMechanismQualityControl(row As Dictionary(Of String, String))
        AuthorizationService.Require(AppState.CanCreateMechanismQualityDelivery, "Mekanizma Kontrol Teslimi")
        If row Is Nothing Then Throw New ArgumentNullException(NameOf(row))

        Dim currentUser = If(AppState.CurrentUserName, "").Trim()
        If currentUser = "" Then Throw New UnauthorizedAccessException("Teslim oluşturmak için aktif kullanıcı oturumu gereklidir.")

        Dim controlId = GetValue(row, "ControlId").Trim()
        Dim productNameCode = NormalizeMechanismProductList(GetValue(row, "ProductNameCode"))
        Dim incomingEyeCountText = GetValue(row, "IncomingEyeCount").Trim()
        Dim deliveredBy = GetValue(row, "DeliveredBy").Trim()
        Dim incomingEyeCount As Integer

        If controlId = "" Then Throw New ArgumentException("Kontrol kayıt numarası boş olamaz.", NameOf(row))
        If productNameCode = "" Then Throw New ArgumentException("Ürün adı ve kodu zorunludur.", NameOf(row))
        If productNameCode.IndexOfAny(New Char() {ChrW(13), ChrW(10)}) >= 0 Then
            Throw New ArgumentException("Her teslim kaydında yalnızca bir ürün seçilebilir.", NameOf(row))
        End If
        If Not Integer.TryParse(incomingEyeCountText, incomingEyeCount) OrElse incomingEyeCount < 1 Then
            Throw New ArgumentException("Gelen göz sayısı en az 1 olmalıdır.", NameOf(row))
        End If
        If deliveredBy <> "" AndAlso Not String.Equals(deliveredBy, currentUser, StringComparison.OrdinalIgnoreCase) Then
            Throw New UnauthorizedAccessException("Teslim eden kullanıcı aktif oturum kullanıcısıyla eşleşmiyor.")
        End If

        Dim validatedRow = New Dictionary(Of String, String)(row, StringComparer.OrdinalIgnoreCase)
        validatedRow("Status") = "PENDING"
        validatedRow("DeliveredBy") = currentUser
        validatedRow("IncomingEyeCount") = incomingEyeCount.ToString()
        validatedRow("ProductNameCode") = productNameCode
        validatedRow("IsSuitable") = ""
        validatedRow("IsNotSuitable") = ""
        validatedRow("ControlledBy") = ""
        validatedRow("ControlledAt") = ""
        validatedRow("ControlDateTime") = ""
        validatedRow("ControlExplanation") = ""
        validatedRow("CreatedComputerName") = Environment.MachineName
        validatedRow("ControlledComputerName") = ""
        If GetValue(validatedRow, "CreatedAt").Trim() = "" Then
            validatedRow("CreatedAt") = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        End If

        CsvUtil.AppendRowLocked(
            AppPaths.MechanismQualityControlRecordsCsv,
            MechanismQualityControlHeaders,
            Function(rows)
                If rows.Any(
                    Function(existingRow) String.Equals(
                        GetValue(existingRow, "ControlId"),
                        controlId,
                        StringComparison.OrdinalIgnoreCase)) Then
                    Throw New InvalidOperationException("Bu kontrol kayıt numarası daha önce kullanılmış: " & controlId)
                End If

                Return validatedRow
            End Function)
    End Sub

    Private Shared Function NormalizeMechanismProductList(productListText As String) As String
        Dim products = If(productListText, "").
            Replace(vbCrLf, vbLf).
            Replace(vbCr, vbLf).
            Split({vbLf}, StringSplitOptions.RemoveEmptyEntries).
            Select(Function(product) product.Trim()).
            Where(Function(product) product <> "").
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToList()

        Return String.Join(Environment.NewLine, products)
    End Function

    Public Shared Function GetMechanismQualityControls() As List(Of Dictionary(Of String, String))
        AuthorizationService.Require(AppState.CanOpenMechanismQualityControl, "Mekanizma Kalite Kontrol Kayıtları")
        RepairMechanismQualityControlRecordsIfNeeded()
        Return CsvUtil.ReadRows(AppPaths.MechanismQualityControlRecordsCsv).
            Where(AddressOf IsValidMechanismQualityControlRow).
            ToList()
    End Function

    Private Shared Sub RepairMechanismQualityControlRecordsIfNeeded()
        SyncLock mechanismQualityRepairLock
            If mechanismQualityRepairAttempted Then Return
            mechanismQualityRepairAttempted = True
        End SyncLock

        Dim filePath = AppPaths.MechanismQualityControlRecordsCsv

        Try
            Dim initialRows = CsvUtil.ReadRows(filePath)
            If initialRows.All(AddressOf IsValidMechanismQualityControlRow) Then Return

            Dim supplementalRows = ReadMechanismQualityControlRecoveryRows(filePath)
            Dim invalidCount As Integer = 0
            Dim embeddedRecoveredCount As Integer = 0
            Dim supplementalRecoveredCount As Integer = 0
            Dim finalRowCount As Integer = 0

            Dim changed = CsvUtil.UpdateRowsLockedIfChanged(
                filePath,
                MechanismQualityControlHeaders,
                Function(rows)
                    invalidCount = rows.Where(Function(row) Not IsValidMechanismQualityControlRow(row)).Count()
                    If invalidCount = 0 Then Return False

                    Dim repairedRows As New List(Of Dictionary(Of String, String))()
                    Dim seenIds As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

                    ' Her zaman canlı dosyadaki sağlam sürüm önceliklidir.
                    For Each row In rows
                        If Not IsValidMechanismQualityControlRow(row) Then Continue For
                        AddMechanismQualityControlRowIfMissing(repairedRows, seenIds, row)
                    Next

                    ' Eski hatalı yazımda bütün CSV kaydı ControlId hücresine gömülmüş olabilir.
                    For Each row In rows
                        If IsValidMechanismQualityControlRow(row) Then Continue For

                        Dim recoveredRow As Dictionary(Of String, String) = Nothing
                        If TryParseEmbeddedMechanismQualityControlRow(row, recoveredRow) AndAlso
                           AddMechanismQualityControlRowIfMissing(repairedRows, seenIds, recoveredRow) Then
                            embeddedRecoveredCount += 1
                        End If
                    Next

                    ' Atomik yazımdan kalmış sağlam tmp kopyaları yalnızca eksik kimlikleri tamamlar.
                    For Each row In supplementalRows
                        If AddMechanismQualityControlRowIfMissing(repairedRows, seenIds, row) Then
                            supplementalRecoveredCount += 1
                        End If
                    Next

                    finalRowCount = repairedRows.Count
                    rows.Clear()
                    rows.AddRange(repairedRows)
                    Return True
                End Function,
                allowIntentionalRowReduction:=True)

            If changed Then
                ErrorLogService.Log(
                    "DataService.RepairMechanismQualityControlRecords",
                    New InvalidDataException("Mekanizma kalite kontrol CSV dosyasındaki yapısal olarak bozuk satırlar güvenli biçimde onarıldı."),
                    "InvalidFragments=" & invalidCount.ToString() &
                    "; EmbeddedRecovered=" & embeddedRecoveredCount.ToString() &
                    "; SupplementalRecovered=" & supplementalRecoveredCount.ToString() &
                    "; FinalRows=" & finalRowCount.ToString() &
                    "; Path=" & filePath)
            End If
        Catch ex As Exception
            ' Form yine açılır; çağıran taraf aşağıda yalnızca yapısal olarak sağlam satırları gösterir.
            ErrorLogService.Log("DataService.RepairMechanismQualityControlRecordsIfNeeded", ex, "Path=" & filePath)
        End Try
    End Sub

    Private Shared Function ReadMechanismQualityControlRecoveryRows(filePath As String) As List(Of Dictionary(Of String, String))
        Dim result As New List(Of Dictionary(Of String, String))()

        Try
            Dim directoryPath = Path.GetDirectoryName(filePath)
            If String.IsNullOrWhiteSpace(directoryPath) OrElse Not Directory.Exists(directoryPath) Then Return result

            Dim fileName = Path.GetFileName(filePath)
            For Each candidatePath In Directory.EnumerateFiles(directoryPath, fileName & ".*.tmp", SearchOption.TopDirectoryOnly)
                Try
                    Dim candidateInfo As New FileInfo(candidatePath)
                    If candidateInfo.LastWriteTimeUtc > DateTime.UtcNow.AddSeconds(-15) Then Continue For

                    For Each row In CsvUtil.ReadRows(candidatePath)
                        If IsValidMechanismQualityControlRow(row) Then
                            result.Add(New Dictionary(Of String, String)(row, StringComparer.OrdinalIgnoreCase))
                        End If
                    Next
                Catch ex As Exception
                    ErrorLogService.Log(
                        "DataService.ReadMechanismQualityControlRecoveryRows.Candidate",
                        ex,
                        "Path=" & candidatePath)
                End Try
            Next
        Catch ex As Exception
            ErrorLogService.Log("DataService.ReadMechanismQualityControlRecoveryRows", ex, "Path=" & filePath)
        End Try

        Return result
    End Function

    Private Shared Function TryParseEmbeddedMechanismQualityControlRow(
        sourceRow As Dictionary(Of String, String),
        ByRef recoveredRow As Dictionary(Of String, String)) As Boolean

        recoveredRow = Nothing
        Dim embeddedCsv = GetValue(sourceRow, "ControlId")
        If String.IsNullOrWhiteSpace(embeddedCsv) OrElse
           Not embeddedCsv.TrimStart().StartsWith("MKC-", StringComparison.OrdinalIgnoreCase) Then
            Return False
        End If

        Dim values = CsvUtil.ParseLine(embeddedCsv)
        If values.Count <> MechanismQualityControlHeaders.Length Then Return False

        Dim parsed As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        For index As Integer = 0 To MechanismQualityControlHeaders.Length - 1
            parsed(MechanismQualityControlHeaders(index)) = values(index)
        Next

        If Not IsValidMechanismQualityControlRow(parsed) Then Return False
        recoveredRow = parsed
        Return True
    End Function

    Private Shared Function AddMechanismQualityControlRowIfMissing(
        rows As List(Of Dictionary(Of String, String)),
        seenIds As HashSet(Of String),
        row As Dictionary(Of String, String)) As Boolean

        If Not IsValidMechanismQualityControlRow(row) Then Return False

        Dim controlId = GetValue(row, "ControlId").Trim()
        If Not seenIds.Add(controlId) Then Return False

        rows.Add(New Dictionary(Of String, String)(row, StringComparer.OrdinalIgnoreCase))
        Return True
    End Function

    Private Shared Function IsValidMechanismQualityControlRow(row As Dictionary(Of String, String)) As Boolean
        If row Is Nothing Then Return False

        Dim controlId = GetValue(row, "ControlId").Trim()
        If Not controlId.StartsWith("MKC-", StringComparison.OrdinalIgnoreCase) Then Return False

        Dim status = GetValue(row, "Status").Trim()
        Return String.Equals(status, "PENDING", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase)
    End Function

    Public Shared Sub DeleteMechanismQualityControlRecord(controlId As String)
        AuthorizationService.Require(AppState.IsAdmin, "Mekanizma Kalite Kontrol Kaydı Silme")

        controlId = If(controlId, "").Trim()
        If controlId = "" Then Throw New ArgumentException("Silinecek kontrol kayıt numarası boş olamaz.", NameOf(controlId))

        Dim deletedProduct As String = ""
        Dim deletedStatus As String = ""
        Dim deleted As Boolean = CsvUtil.UpdateRowsLocked(
            AppPaths.MechanismQualityControlRecordsCsv,
            MechanismQualityControlHeaders,
            Function(rows)
                Dim row = rows.FirstOrDefault(
                    Function(item) String.Equals(GetValue(item, "ControlId"), controlId, StringComparison.OrdinalIgnoreCase))
                If row Is Nothing Then Return False

                deletedProduct = GetValue(row, "ProductNameCode")
                deletedStatus = GetValue(row, "Status")
                rows.Remove(row)
                Return True
            End Function)

        If Not deleted Then Throw New InvalidOperationException("Silinecek mekanizma kalite kontrol kaydı bulunamadı.")

        AuditService.Log(
            "MECHANISM_QUALITY_CONTROL_DELETE",
            "",
            "",
            "ControlId=" & controlId & "; Product=" & deletedProduct & "; Status=" & deletedStatus)
    End Sub

    Public Shared Sub UpdateMechanismQualityControlDetails(controlId As String,
                                                           incomingEyeCount As String,
                                                           productNameCode As String,
                                                           deliveryExplanation As String,
                                                           mountedMechanismCounter As String,
                                                           controlExplanation As String)
        AuthorizationService.Require(AppState.CanEditMechanismQualityDetails, "Mekanizma Kalite Kontrol Detay Düzeltme")

        controlId = If(controlId, "").Trim()
        incomingEyeCount = If(incomingEyeCount, "").Trim()
        productNameCode = NormalizeMechanismProductList(productNameCode)
        deliveryExplanation = If(deliveryExplanation, "").Trim()
        mountedMechanismCounter = If(mountedMechanismCounter, "").Trim()
        controlExplanation = If(controlExplanation, "").Trim()

        If controlId = "" Then Throw New ArgumentException("Kontrol kayıt numarası boş olamaz.")
        If productNameCode = "" Then Throw New ArgumentException("Ürün adı ve kodu boş olamaz.")

        Dim eyeCountValue As Integer
        If Not Integer.TryParse(incomingEyeCount, eyeCountValue) OrElse eyeCountValue < 1 OrElse eyeCountValue > 999999 Then
            Throw New ArgumentException("Gelen göz sayısı 1 ile 999999 arasında tam sayı olmalıdır.")
        End If

        CsvUtil.UpdateRowsLocked(
            AppPaths.MechanismQualityControlRecordsCsv,
            MechanismQualityControlHeaders,
            Sub(rows)
                Dim row = rows.FirstOrDefault(
                    Function(r) String.Equals(GetValue(r, "ControlId"), controlId, StringComparison.OrdinalIgnoreCase))

                If row Is Nothing Then Throw New InvalidOperationException("Mekanizma kalite kontrol kaydı bulunamadı.")

                row("IncomingEyeCount") = eyeCountValue.ToString()
                row("ProductNameCode") = productNameCode
                row("Explanation") = deliveryExplanation
                row("DeliveryExplanation") = deliveryExplanation
                row("MountedMechanismCounter") = mountedMechanismCounter
                row("ControlExplanation") = controlExplanation
            End Sub)
    End Sub

    Public Shared Sub CompleteMechanismQualityControl(controlId As String,
                                                      isSuitable As Boolean,
                                                      controlledBy As String,
                                                      controlExplanation As String,
                                                      mountedMechanismCounter As String,
                                                      productNameCode As String)
        AuthorizationService.Require(AppState.CanReviewMechanismQualityDelivery, "Mekanizma Kalite Kontrol Sonuçlandırma")

        controlId = If(controlId, "").Trim()
        controlledBy = If(controlledBy, "").Trim()
        controlExplanation = If(controlExplanation, "").Trim()
        mountedMechanismCounter = If(mountedMechanismCounter, "").Trim()
        productNameCode = NormalizeMechanismProductList(productNameCode)

        If controlId = "" Then Throw New ArgumentException("Kontrol kayıt numarası boş olamaz.")
        If productNameCode = "" Then Throw New ArgumentException("Kontrolü tamamlamak için en az bir ürün seçilmelidir.")
        If mountedMechanismCounter = "" Then
            Throw New ArgumentException("Montajı yapılan mekanizma/sayaç bilgisi kontrol aşamasında zorunludur.")
        End If
        If Not isSuitable AndAlso controlExplanation = "" Then
            Throw New ArgumentException("UYGUN DEĞİL sonucu için kontrol açıklaması zorunludur.")
        End If

        Dim currentUser = If(AppState.CurrentUserName, "").Trim()
        If currentUser = "" Then Throw New UnauthorizedAccessException("Kontrolü tamamlamak için aktif kullanıcı oturumu gereklidir.")
        If controlledBy <> "" AndAlso Not String.Equals(controlledBy, currentUser, StringComparison.OrdinalIgnoreCase) Then
            Throw New UnauthorizedAccessException("Kontrol eden kullanıcı aktif oturum kullanıcısıyla eşleşmiyor.")
        End If

        CsvUtil.UpdateRowsLocked(
            AppPaths.MechanismQualityControlRecordsCsv,
            MechanismQualityControlHeaders,
            Sub(rows)
                Dim row = rows.FirstOrDefault(
                    Function(r) String.Equals(GetValue(r, "ControlId"), controlId, StringComparison.OrdinalIgnoreCase))

                If row Is Nothing Then Throw New InvalidOperationException("Mekanizma kalite kontrol kaydı bulunamadı.")
                Dim currentStatus = GetValue(row, "Status").Trim()
                If String.Equals(currentStatus, "COMPLETED", StringComparison.OrdinalIgnoreCase) Then
                    Throw New InvalidOperationException("Bu kayıt daha önce kontrol edilmiş.")
                End If
                If Not String.Equals(currentStatus, "PENDING", StringComparison.OrdinalIgnoreCase) Then
                    Throw New InvalidOperationException("Yalnızca bekleyen mekanizma kalite kontrol kayıtları tamamlanabilir.")
                End If

                Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                row("Status") = "COMPLETED"
                row("ControlDateTime") = nowText
                row("ProductNameCode") = productNameCode
                row("MountedMechanismCounter") = mountedMechanismCounter
                row("ControlExplanation") = controlExplanation
                row("IsSuitable") = If(isSuitable, "X", "")
                row("IsNotSuitable") = If(isSuitable, "", "X")
                row("ControlledBy") = currentUser
                row("ControlledAt") = nowText
                row("ControlledComputerName") = Environment.MachineName
            End Sub)
    End Sub

    Public Shared Function CompleteMoldBindingAndCreateProductionTicket(
        bindingId As String,
        ticketRow As Dictionary(Of String, String),
        completedBy As String,
        Optional finishNote As String = "",
        Optional bindingDurationMin As String = "") As String

        AuthorizationService.Require(AppState.CanOpenProductionBinding, "Kalıp Bağlama Tamamlama")

        bindingId = If(bindingId, "").Trim()
        completedBy = If(completedBy, "").Trim()
        If bindingId = "" Then Throw New ArgumentException("Bağlama kayıt no boş olamaz.", NameOf(bindingId))
        If ticketRow Is Nothing Then Throw New ArgumentNullException(NameOf(ticketRow))

        Dim requestedTicketId = GetValue(ticketRow, "TicketId").Trim()
        If requestedTicketId = "" Then Throw New ArgumentException("Kalite ticket numarası boş olamaz.", NameOf(ticketRow))

        Directory.CreateDirectory(AppPaths.PendingTransactionsDir)
        Dim journalPath = MoldBindingTicketJournalPath(bindingId)
        Dim effectiveTicketId As String = ""

        CsvUtil.ExecuteWithExclusiveLock(
            journalPath,
            Sub()
                Dim transaction As MoldBindingTicketTransaction
                If File.Exists(journalPath) Then
                    transaction = ReadMoldBindingTicketTransaction(journalPath)
                Else
                    transaction = New MoldBindingTicketTransaction With {
                        .BindingId = bindingId,
                        .TicketId = requestedTicketId,
                        .CompletedAt = FirstNonEmpty(GetValue(ticketRow, "BindingEndAt"),
                                                     GetValue(ticketRow, "CreatedAt"),
                                                     DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                        .CompletedBy = completedBy,
                        .CompletedComputerName = Environment.MachineName,
                        .FinishNote = If(finishNote, "").Trim(),
                        .BindingDurationMin = If(bindingDurationMin, "").Trim(),
                        .CreatedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                        .TicketRow = NormalizeRow(ticketRow, ProductionTicketHeaders)
                    }
                    WriteMoldBindingTicketTransaction(journalPath, transaction)
                End If

                effectiveTicketId = ApplyMoldBindingTicketTransaction(transaction)
                File.Delete(journalPath)
            End Sub)

        Return effectiveTicketId
    End Function

    Public Shared Function RecoverPendingTransactions() As Integer
        Directory.CreateDirectory(AppPaths.PendingTransactionsDir)
        Dim recoveredCount As Integer = 0

        For Each journalPath In Directory.EnumerateFiles(
            AppPaths.PendingTransactionsDir,
            "MoldBindingTicket_*.json",
            SearchOption.TopDirectoryOnly)

            Try
                CsvUtil.ExecuteWithExclusiveLock(
                    journalPath,
                    Sub()
                        If Not File.Exists(journalPath) Then Return
                        Dim transaction = ReadMoldBindingTicketTransaction(journalPath)
                        ApplyMoldBindingTicketTransaction(transaction)
                        File.Delete(journalPath)
                        recoveredCount += 1
                    End Sub)
            Catch ex As Exception
                AppendTransactionRecoveryLog(
                    "Bekleyen kalıp bağlama/ticket işlemi kurtarılamadı: " &
                    journalPath & " | " & ex.Message)
            End Try
        Next

        Return recoveredCount
    End Function

    Private Shared Function ApplyMoldBindingTicketTransaction(transaction As MoldBindingTicketTransaction) As String
        If transaction Is Nothing Then Throw New InvalidDataException("Bekleyen işlem kaydı okunamadı.")

        Dim bindingId = If(transaction.BindingId, "").Trim()
        Dim requestedTicketId = If(transaction.TicketId, "").Trim()
        If bindingId = "" OrElse requestedTicketId = "" Then
            Throw New InvalidDataException("Bekleyen işlemde bağlama veya ticket numarası eksik.")
        End If

        Dim effectiveTicketId As String = ""

        CsvUtil.UpdateTwoFilesLocked(
            AppPaths.ProductionTicketsCsv,
            ProductionTicketHeaders,
            AppPaths.MoldBindingRecordsCsv,
            MoldBindingHeaders,
            Sub(ticketRows, bindingRows)
                Dim bindingRow = bindingRows.FirstOrDefault(
                    Function(row) String.Equals(GetValue(row, "BindingId"), bindingId, StringComparison.OrdinalIgnoreCase))
                If bindingRow Is Nothing Then
                    Throw New InvalidOperationException("Bağlama kaydı bulunamadı: " & bindingId)
                End If

                Dim bindingTicketId = GetValue(bindingRow, "ProductionTicketId").Trim()
                Dim existingTicket = ticketRows.FirstOrDefault(
                    Function(row) String.Equals(GetValue(row, "BindingId"), bindingId, StringComparison.OrdinalIgnoreCase))

                If existingTicket Is Nothing AndAlso bindingTicketId <> "" Then
                    existingTicket = ticketRows.FirstOrDefault(
                        Function(row) String.Equals(GetValue(row, "TicketId"), bindingTicketId, StringComparison.OrdinalIgnoreCase))
                End If

                If existingTicket Is Nothing Then
                    existingTicket = ticketRows.FirstOrDefault(
                        Function(row) String.Equals(GetValue(row, "TicketId"), requestedTicketId, StringComparison.OrdinalIgnoreCase))
                    If existingTicket IsNot Nothing AndAlso
                       Not String.Equals(GetValue(existingTicket, "BindingId"), bindingId, StringComparison.OrdinalIgnoreCase) Then
                        Throw New InvalidOperationException(
                            "Ticket numarası başka bir bağlama kaydında kullanılıyor: " & requestedTicketId)
                    End If
                End If

                If existingTicket Is Nothing Then
                    effectiveTicketId = If(bindingTicketId <> "", bindingTicketId, requestedTicketId)
                    Dim collision = ticketRows.FirstOrDefault(
                        Function(row) String.Equals(GetValue(row, "TicketId"), effectiveTicketId, StringComparison.OrdinalIgnoreCase))
                    If collision IsNot Nothing Then
                        Throw New InvalidOperationException(
                            "Ticket numarası başka bir kayıtta kullanılıyor: " & effectiveTicketId)
                    End If

                    Dim newTicketRow = NormalizeRow(transaction.TicketRow, ProductionTicketHeaders)
                    newTicketRow("TicketId") = effectiveTicketId
                    newTicketRow("BindingId") = bindingId
                    ticketRows.Add(newTicketRow)
                Else
                    effectiveTicketId = GetValue(existingTicket, "TicketId").Trim()
                    If effectiveTicketId = "" Then
                        Throw New InvalidDataException("Mevcut kalite ticketının numarası boş.")
                    End If
                End If

                Dim bindingStatus = GetValue(bindingRow, "Status").Trim()
                If String.Equals(bindingStatus, "STARTED", StringComparison.OrdinalIgnoreCase) Then
                    bindingRow("Status") = "COMPLETED"
                    bindingRow("CompletedAt") = transaction.CompletedAt
                    bindingRow("CompletedBy") = transaction.CompletedBy
                    bindingRow("CompletedComputerName") = transaction.CompletedComputerName
                    bindingRow("ProductionTicketId") = effectiveTicketId
                    bindingRow("FinishNote") = transaction.FinishNote
                    bindingRow("BindingDurationMin") = transaction.BindingDurationMin
                ElseIf String.Equals(bindingStatus, "COMPLETED", StringComparison.OrdinalIgnoreCase) Then
                    If bindingTicketId = "" Then
                        bindingRow("ProductionTicketId") = effectiveTicketId
                    ElseIf Not String.Equals(bindingTicketId, effectiveTicketId, StringComparison.OrdinalIgnoreCase) Then
                        Throw New InvalidOperationException(
                            "Tamamlanmış bağlama farklı bir ticket ile eşleşiyor. Bağlama: " &
                            bindingId & "; Ticket: " & bindingTicketId)
                    End If
                Else
                    Throw New InvalidOperationException(
                        "Bağlama kaydı işlem için uygun durumda değil: " & bindingStatus)
                End If
            End Sub)

        Return effectiveTicketId
    End Function

    Private Shared Function MoldBindingTicketJournalPath(bindingId As String) As String
        Dim hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(If(bindingId, "").Trim())))
        Return Path.Combine(
            AppPaths.PendingTransactionsDir,
            "MoldBindingTicket_" & hash.Substring(0, 32) & ".json")
    End Function

    Private Shared Sub WriteMoldBindingTicketTransaction(
        journalPath As String,
        transaction As MoldBindingTicketTransaction)

        Dim tempPath = journalPath & "." & Guid.NewGuid().ToString("N") & ".tmp"
        Try
            Dim json = JsonSerializer.Serialize(
                transaction,
                New JsonSerializerOptions With {.WriteIndented = True})
            File.WriteAllText(tempPath, json, New UTF8Encoding(False))
            File.Move(tempPath, journalPath)
        Finally
            Try
                If File.Exists(tempPath) Then File.Delete(tempPath)
            Catch cleanupEx As Exception
                ErrorLogService.Log("DataService.WritePendingTransaction.Cleanup", cleanupEx, "Path=" & tempPath)
            End Try
        End Try
    End Sub

    Private Shared Function ReadMoldBindingTicketTransaction(
        journalPath As String) As MoldBindingTicketTransaction

        Dim json = File.ReadAllText(journalPath, Encoding.UTF8)
        Dim transaction = JsonSerializer.Deserialize(Of MoldBindingTicketTransaction)(json)
        If transaction Is Nothing Then Throw New InvalidDataException("Bekleyen işlem dosyası boş.")

        transaction.TicketRow = New Dictionary(Of String, String)(
            If(transaction.TicketRow, New Dictionary(Of String, String)()),
            StringComparer.OrdinalIgnoreCase)
        Return transaction
    End Function

    Private Shared Function NormalizeRow(
        source As Dictionary(Of String, String),
        headers As String()) As Dictionary(Of String, String)

        Dim normalized As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        For Each header In headers
            normalized(header) = If(source IsNot Nothing AndAlso source.ContainsKey(header), source(header), "")
        Next
        Return normalized
    End Function

    Private Shared Function FirstNonEmpty(ParamArray values As String()) As String
        For Each value In values
            If Not String.IsNullOrWhiteSpace(value) Then Return value.Trim()
        Next
        Return ""
    End Function

    Private Shared Sub AppendTransactionRecoveryLog(message As String)
        Try
            File.AppendAllText(
                Path.Combine(AppPaths.DataDir, "TransactionRecovery.log"),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") & " | " & message & Environment.NewLine,
                Encoding.UTF8)
        Catch ex As Exception
            ErrorLogService.Log("DataService.AppendTransactionRecoveryLog", ex)
        End Try
    End Sub

    Public Shared Function GetProductionTickets() As List(Of Dictionary(Of String, String))
        Return CsvUtil.ReadRows(AppPaths.ProductionTicketsCsv)
    End Function

    Public Shared Sub MarkProductionTicketSeen(ticketId As String, userName As String)
        AuthorizationService.Require(AppState.CanModifyQualityTickets, "Kalite Ticketını Görüldü Yapma")
        CsvUtil.UpdateRowsLocked(
            AppPaths.ProductionTicketsCsv,
            ProductionTicketHeaders,
            Sub(rows)
                For Each r In rows
                    If String.Equals(GetValue(r, "TicketId"), ticketId, StringComparison.OrdinalIgnoreCase) Then
                        r("SeenByQuality") = userName
                        r("SeenAt") = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        If String.Equals(GetValue(r, "Status"), "OPEN", StringComparison.OrdinalIgnoreCase) Then
                            r("Status") = "SEEN"
                        End If
                        Exit For
                    End If
                Next
            End Sub)
    End Sub

    Public Shared Sub CloseProductionTicket(ticketId As String, userName As String, closeNote As String)
        AuthorizationService.Require(AppState.CanModifyQualityTickets, "Kalite Ticketını Kapatma")
        CsvUtil.UpdateRowsLocked(
            AppPaths.ProductionTicketsCsv,
            ProductionTicketHeaders,
            Sub(rows)
                For Each r In rows
                    If String.Equals(GetValue(r, "TicketId"), ticketId, StringComparison.OrdinalIgnoreCase) Then
                        r("Status") = "CLOSED"
                        r("ClosedBy") = userName
                        r("ClosedAt") = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        r("CloseNote") = closeNote
                        Exit For
                    End If
                Next
            End Sub)
    End Sub


    Public Shared Sub AppendQualityToProductionTicket(row As Dictionary(Of String, String))
        AuthorizationService.Require(AppState.CanOpenMeasurement, "Üretim Uygunsuzluk Ticketı Oluşturma")
        CsvUtil.AppendRowLocked(AppPaths.QualityToProductionTicketsCsv, QualityToProductionTicketHeaders, row)
    End Sub

    Public Shared Function GetQualityToProductionTickets() As List(Of Dictionary(Of String, String))
        Return CsvUtil.ReadRows(AppPaths.QualityToProductionTicketsCsv)
    End Function

    Public Shared Function QualityToProductionTicketExistsForRecord(recordId As String) As Boolean
        recordId = If(recordId, "").Trim()
        If recordId = "" Then Return False

        Return CsvUtil.ReadRows(AppPaths.QualityToProductionTicketsCsv).
            Any(Function(r) String.Equals(GetValue(r, "RecordId"), recordId, StringComparison.OrdinalIgnoreCase))
    End Function

    Public Shared Sub MarkQualityToProductionTicketSeen(ticketId As String, userName As String)
        AuthorizationService.Require(AppState.CanModifyQualityToProductionTickets, "Üretim Ticketını Görüldü Yapma")
        CsvUtil.UpdateRowsLocked(
            AppPaths.QualityToProductionTicketsCsv,
            QualityToProductionTicketHeaders,
            Sub(rows)
                For Each r In rows
                    If String.Equals(GetValue(r, "TicketId"), ticketId, StringComparison.OrdinalIgnoreCase) Then
                        r("SeenByProduction") = userName
                        r("SeenAt") = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        If String.Equals(GetValue(r, "Status"), "OPEN", StringComparison.OrdinalIgnoreCase) Then
                            r("Status") = "SEEN"
                        End If
                        Exit For
                    End If
                Next
            End Sub)
    End Sub

    Public Shared Sub CloseQualityToProductionTicket(ticketId As String, userName As String, closeNote As String)
        AuthorizationService.Require(AppState.CanModifyQualityToProductionTickets, "Üretim Ticketını Kapatma")
        CsvUtil.UpdateRowsLocked(
            AppPaths.QualityToProductionTicketsCsv,
            QualityToProductionTicketHeaders,
            Sub(rows)
                For Each r In rows
                    If String.Equals(GetValue(r, "TicketId"), ticketId, StringComparison.OrdinalIgnoreCase) Then
                        r("Status") = "CLOSED"
                        r("ClosedBy") = userName
                        r("ClosedAt") = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        r("CloseNote") = closeNote
                        Exit For
                    End If
                Next
            End Sub)
    End Sub

    Public Shared Sub AppendMoldTicket(row As Dictionary(Of String, String))
        AuthorizationService.Require(AppState.CanModifyMoldTickets, "Kalıp Ticketı Oluşturma")
        CsvUtil.AppendRowLocked(AppPaths.MoldTicketsCsv, MoldTicketHeaders, row)
    End Sub

    Public Shared Function GetMoldTicketById(moldTicketId As String) As Dictionary(Of String, String)
        moldTicketId = If(moldTicketId, "").Trim()
        If moldTicketId = "" Then Return Nothing

        Return CsvUtil.ReadRows(AppPaths.MoldTicketsCsv).
            FirstOrDefault(Function(row) String.Equals(GetValue(row, "MoldTicketId"), moldTicketId, StringComparison.OrdinalIgnoreCase))
    End Function

    Public Shared Sub UpdateMoldTicket(moldTicketId As String, updates As Dictionary(Of String, String))
        AuthorizationService.Require(AppState.IsAdmin, "Kalıp Ticketı Düzenleme")

        moldTicketId = If(moldTicketId, "").Trim()
        If moldTicketId = "" Then Throw New ArgumentException("Düzenlenecek kalıp ticket numarası boş olamaz.")
        If updates Is Nothing Then Throw New ArgumentNullException(NameOf(updates))

        Dim updatedMoldCode As String = ""
        Dim updatedTrCode As String = ""
        Dim updatedStatus As String = ""
        Dim updated As Boolean = CsvUtil.UpdateRowsLocked(
            AppPaths.MoldTicketsCsv,
            MoldTicketHeaders,
            Function(rows)
                Dim row = rows.FirstOrDefault(
                    Function(item) String.Equals(GetValue(item, "MoldTicketId"), moldTicketId, StringComparison.OrdinalIgnoreCase))
                If row Is Nothing Then Return False

                Dim editableHeaders = {
                    "MoldCode",
                    "TrCode",
                    "DrawingRev",
                    "ProductName",
                    "Severity",
                    "ProblemType",
                    "ProblemDescription",
                    "ActionPlan",
                    "CloseNote"
                }

                For Each header In editableHeaders
                    If updates.ContainsKey(header) Then
                        row(header) = If(updates(header), "").Trim()
                    End If
                Next

                updatedMoldCode = GetValue(row, "MoldCode")
                updatedTrCode = GetValue(row, "TrCode")
                updatedStatus = GetValue(row, "Status")
                Return True
            End Function)

        If Not updated Then Throw New InvalidOperationException("Düzenlenecek kalıp ticketı bulunamadı.")

        AuditService.Log(
            "MOLD_TICKET_UPDATE",
            updatedTrCode,
            "",
            "MoldTicketId=" & moldTicketId & "; Mold=" & updatedMoldCode & "; Status=" & updatedStatus)
    End Sub

    Public Shared Function GetMoldTicketIdForPlasticShift(recordId As String) As String
        AuthorizationService.Require(AppState.CanCreateMoldTicketFromPlasticShift, "Vardiya Kaydından Kalıp Ticketı")
        recordId = If(recordId, "").Trim()
        If recordId = "" Then Return ""

        Dim existing = CsvUtil.ReadRows(AppPaths.MoldTicketsCsv).
            FirstOrDefault(Function(row) String.Equals(GetValue(row, "SourcePlasticShiftRecordId"), recordId, StringComparison.OrdinalIgnoreCase))
        Return If(existing Is Nothing, "", GetValue(existing, "MoldTicketId"))
    End Function

    Public Shared Function CreateMoldTicketFromPlasticShift(
        recordId As String,
        ticketDraft As Dictionary(Of String, String)) As String

        AuthorizationService.Require(AppState.CanCreateMoldTicketFromPlasticShift, "Vardiya Kaydından Kalıp Ticketı Oluşturma")
        recordId = If(recordId, "").Trim()
        If recordId = "" Then Throw New ArgumentException("Kalıp ticketı için vardiya kayıt numarası zorunludur.")
        If ticketDraft Is Nothing Then Throw New ArgumentNullException(NameOf(ticketDraft))

        Dim sourceRecord = CsvUtil.ReadRows(AppPaths.PlasticShiftTrackingRecordsCsv).
            FirstOrDefault(Function(row) String.Equals(GetValue(row, "RecordId"), recordId, StringComparison.OrdinalIgnoreCase))
        If sourceRecord Is Nothing Then Throw New InvalidOperationException("Kalıp ticketına bağlanacak vardiya kaydı bulunamadı.")
        If Not String.Equals(NormalizeYesNoFlag(GetValue(sourceRecord, "MoldModification")), "YES", StringComparison.OrdinalIgnoreCase) Then
            Throw New InvalidOperationException("Kalıp Tadilat seçeneği açık olmayan vardiya kaydı için kalıp ticketı oluşturulamaz.")
        End If

        Dim moldCode = GetValue(ticketDraft, "MoldCode").Trim()
        Dim problemDescription = GetValue(ticketDraft, "ProblemDescription").Trim()
        If moldCode = "" Then Throw New ArgumentException("Kalıp Kodu zorunludur.")
        If problemDescription = "" Then Throw New ArgumentException("Sorun açıklaması zorunludur.")

        Dim createdNew As Boolean = False
        Dim ticketId = CsvUtil.UpdateRowsLocked(
            AppPaths.MoldTicketsCsv,
            MoldTicketHeaders,
            Function(rows)
                Dim existing = rows.FirstOrDefault(
                    Function(row) String.Equals(GetValue(row, "SourcePlasticShiftRecordId"), recordId, StringComparison.OrdinalIgnoreCase))
                If existing IsNot Nothing Then Return GetValue(existing, "MoldTicketId")

                Dim newTicketId = "KLP-" & DateTime.Now.ToString("yyyyMMdd-HHmmss") & "-" &
                                  Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant()
                Dim safeRow As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                For Each header In MoldTicketHeaders
                    safeRow(header) = ""
                Next

                safeRow("MoldTicketId") = newTicketId
                safeRow("Status") = "OPEN"
                safeRow("CreatedAt") = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                safeRow("CreatedBy") = AppState.CurrentUserName
                safeRow("ComputerName") = Environment.MachineName
                safeRow("MoldCode") = moldCode
                safeRow("TrCode") = GetValue(ticketDraft, "TrCode").Trim()
                safeRow("DrawingRev") = GetValue(ticketDraft, "DrawingRev").Trim()
                safeRow("ProductName") = GetValue(ticketDraft, "ProductName").Trim()
                safeRow("Severity") = GetValue(ticketDraft, "Severity").Trim()
                safeRow("ProblemType") = GetValue(ticketDraft, "ProblemType").Trim()
                safeRow("ProblemDescription") = problemDescription
                safeRow("ActionPlan") = GetValue(ticketDraft, "ActionPlan").Trim()
                safeRow("SourcePlasticShiftRecordId") = recordId
                rows.Add(safeRow)
                createdNew = True
                Return newTicketId
            End Function)

        If createdNew Then
            AuditService.Log(
                "MOLD_TICKET_CREATE_FROM_PLASTIC_SHIFT",
                GetValue(ticketDraft, "TrCode"),
                GetValue(ticketDraft, "DrawingRev"),
                "MoldTicketId=" & ticketId & "; VardiyaKaydi=" & recordId & "; Mold=" & moldCode)
        End If
        Return ticketId
    End Function

    Public Shared Function GetMoldTickets() As List(Of Dictionary(Of String, String))
        Return CsvUtil.ReadRows(AppPaths.MoldTicketsCsv)
    End Function

    Public Shared Function GetOpenMoldTickets(moldCode As String) As List(Of Dictionary(Of String, String))
        moldCode = If(moldCode, "").Trim()
        If moldCode = "" Then Return New List(Of Dictionary(Of String, String))()

        Return CsvUtil.ReadRows(AppPaths.MoldTicketsCsv).
            Where(Function(r) MoldCodeMatches(GetValue(r, "MoldCode"), moldCode) AndAlso
                              String.Equals(GetValue(r, "Status"), "OPEN", StringComparison.OrdinalIgnoreCase)).
            OrderByDescending(Function(r) GetValue(r, "CreatedAt")).
            ToList()
    End Function

    Public Shared Function MoldCodeMatches(recordMoldCode As String, selectedMoldCode As String) As Boolean
        Dim selectedCodes = SplitMoldCodeTokens(selectedMoldCode)
        If selectedCodes.Count = 0 Then Return False

        Dim recordCodes = SplitMoldCodeTokens(recordMoldCode)
        If recordCodes.Count = 0 Then Return False

        Return recordCodes.Any(Function(code) selectedCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
    End Function

    Public Shared Function SplitMoldCodeTokens(value As String) As List(Of String)
        Dim result As New List(Of String)()
        Dim raw = If(value, "")
        Dim separators As Char() = {";"c, ","c, "/"c, "\"c, "|"c, vbCr(0), vbLf(0), vbTab(0)}

        For Each part In raw.Split(separators, StringSplitOptions.RemoveEmptyEntries)
            Dim token = part.Trim()
            If token = "" Then Continue For
            If Not result.Any(Function(existing) String.Equals(existing, token, StringComparison.OrdinalIgnoreCase)) Then
                result.Add(token)
            End If
        Next

        If result.Count = 0 AndAlso raw.Trim() <> "" Then result.Add(raw.Trim())
        Return result
    End Function

    Public Shared Sub CloseMoldTicket(moldTicketId As String, userName As String, closeNote As String)
        AuthorizationService.Require(AppState.CanModifyMoldTickets, "Kalıp Ticketını Kapatma")
        CsvUtil.UpdateRowsLocked(
            AppPaths.MoldTicketsCsv,
            MoldTicketHeaders,
            Sub(rows)
                For Each r In rows
                    If String.Equals(GetValue(r, "MoldTicketId"), moldTicketId, StringComparison.OrdinalIgnoreCase) Then
                        r("Status") = "CLOSED"
                        r("ClosedBy") = userName
                        r("ClosedAt") = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        r("CloseNote") = closeNote
                        Exit For
                    End If
                Next
            End Sub)
    End Sub

    Public Shared Sub DeleteMoldTicket(moldTicketId As String)
        AuthorizationService.Require(AppState.CanDeleteMoldTickets, "Kalıp Ticketı Silme")

        moldTicketId = If(moldTicketId, "").Trim()
        If moldTicketId = "" Then Throw New ArgumentException("Silinecek kalıp ticket numarası boş olamaz.")

        Dim deletedMoldCode As String = ""
        Dim deletedTrCode As String = ""
        Dim deletedStatus As String = ""
        Dim deleted As Boolean = CsvUtil.UpdateRowsLocked(
            AppPaths.MoldTicketsCsv,
            MoldTicketHeaders,
            Function(rows)
                Dim row = rows.FirstOrDefault(
                    Function(item) String.Equals(GetValue(item, "MoldTicketId"), moldTicketId, StringComparison.OrdinalIgnoreCase))
                If row Is Nothing Then Return False

                deletedMoldCode = GetValue(row, "MoldCode")
                deletedTrCode = GetValue(row, "TrCode")
                deletedStatus = GetValue(row, "Status")
                rows.Remove(row)
                Return True
            End Function)

        If Not deleted Then Throw New InvalidOperationException("Silinecek kalıp ticketı bulunamadı.")

        AuditService.Log(
            "MOLD_TICKET_DELETE",
            deletedTrCode,
            "",
            "MoldTicketId=" & moldTicketId & "; Mold=" & deletedMoldCode & "; Status=" & deletedStatus)
    End Sub

    Public Shared Function GetPlasticShiftTrackingRecords() As List(Of Dictionary(Of String, String))
        AuthorizationService.Require(AppState.CanOpenPlasticShiftTracking, "Plastikhane Vardiya Takip Listesi")
        Return CsvUtil.ReadRows(AppPaths.PlasticShiftTrackingRecordsCsv)
    End Function

    Public Shared Function SavePlasticShiftTrackingRecord(inputRow As Dictionary(Of String, String)) As String
        Return SaveShiftTrackingRecord(inputRow, False)
    End Function

    Public Shared Function GetMechanismShiftTrackingRecords() As List(Of Dictionary(Of String, String))
        AuthorizationService.Require(AppState.CanOpenMechanismShiftTracking, "Mekanizma Vardiya Takip Listesi")
        Return CsvUtil.ReadRows(AppPaths.MechanismShiftTrackingRecordsCsv)
    End Function

    Public Shared Function SaveMechanismShiftTrackingRecord(inputRow As Dictionary(Of String, String)) As String
        Return SaveShiftTrackingRecord(inputRow, True)
    End Function

    Private Shared Function SaveShiftTrackingRecord(inputRow As Dictionary(Of String, String),
                                                    mechanismMode As Boolean) As String
        Dim canModify = If(mechanismMode, AppState.CanModifyMechanismShiftTracking, AppState.CanModifyPlasticShiftTracking)
        Dim featureName = If(mechanismMode, "Mekanizma Vardiya Takip Kaydı", "Plastikhane Vardiya Takip Kaydı")
        Dim targetPath = If(mechanismMode, AppPaths.MechanismShiftTrackingRecordsCsv, AppPaths.PlasticShiftTrackingRecordsCsv)
        AuthorizationService.Require(canModify, featureName)
        If inputRow Is Nothing Then Throw New ArgumentNullException(NameOf(inputRow))

        Dim currentUser = If(AppState.CurrentUserName, "").Trim()
        If currentUser = "" Then Throw New UnauthorizedAccessException("Kayıt işlemi için aktif kullanıcı oturumu gereklidir.")

        Dim recordId = GetValue(inputRow, "RecordId").Trim()
        Dim isNew = recordId = ""
        Dim occurredAt As DateTime
        If isNew Then
            occurredAt = DateTime.Now
        ElseIf Not TryParseCsvDate(GetValue(inputRow, "OccurredAt"), occurredAt) Then
            Throw New ArgumentException("Geçerli bir tarih ve saat girilmelidir.")
        End If

        Dim defectiveQuantity = GetValue(inputRow, "DefectiveQuantity").Trim()
        defectiveQuantity = defectiveQuantity.Replace(vbCr, " ").Replace(vbLf, " ")
        While defectiveQuantity.Contains("  ")
            defectiveQuantity = defectiveQuantity.Replace("  ", " ")
        End While
        If defectiveQuantity = "" Then
            Throw New ArgumentException("Hatalı adet / miktar alanı zorunludur.")
        End If
        If defectiveQuantity.Length > 100 Then
            Throw New ArgumentException("Hatalı adet / miktar en fazla 100 karakter olabilir.")
        End If

        Dim responsible = GetValue(inputRow, "Responsible").Trim()
        Dim productNameCode = GetValue(inputRow, "ProductNameCode").Trim()
        Dim problem = GetValue(inputRow, "Problem").Trim()
        If responsible = "" Then Throw New ArgumentException("Sorumlu alanı zorunludur.")
        If productNameCode = "" Then Throw New ArgumentException("Ürün adı ve kodu zorunludur.")
        If problem = "" Then Throw New ArgumentException("Sorun açıklaması zorunludur.")

        If isNew Then
            recordId = If(mechanismMode, "MVT-", "PVT-") &
                       DateTime.Now.ToString("yyyyMMdd-HHmmss") & "-" &
                       Guid.NewGuid().ToString("N").Substring(0, 5).ToUpperInvariant()
        End If

        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        Dim safeRow As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        For Each header In PlasticShiftTrackingHeaders
            safeRow(header) = ""
        Next

        safeRow("RecordId") = recordId
        safeRow("OccurredAt") = occurredAt.ToString("yyyy-MM-dd HH:mm:ss")
        safeRow("DefectiveQuantity") = defectiveQuantity
        safeRow("Responsible") = responsible
        safeRow("ProductNameCode") = productNameCode
        safeRow("Problem") = problem
        safeRow("ActionTaken") = GetValue(inputRow, "ActionTaken").Trim()
        safeRow("YellowCard") = NormalizeYesNoFlag(GetValue(inputRow, "YellowCard"))
        safeRow("MoldModification") = NormalizeYesNoFlag(GetValue(inputRow, "MoldModification"))
        safeRow("ErrorReport") = NormalizeYesNoFlag(GetValue(inputRow, "ErrorReport"))
        safeRow("TestPerformed") = NormalizeYesNoFlag(GetValue(inputRow, "TestPerformed"))
        safeRow("UpdatedBy") = currentUser
        safeRow("UpdatedAt") = nowText
        safeRow("ComputerName") = Environment.MachineName

        CsvUtil.UpdateRowsLocked(
            targetPath,
            PlasticShiftTrackingHeaders,
            Sub(rows)
                Dim existing = rows.FirstOrDefault(
                    Function(row) String.Equals(GetValue(row, "RecordId"), recordId, StringComparison.OrdinalIgnoreCase))

                If isNew Then
                    If existing IsNot Nothing Then Throw New InvalidOperationException("Kayıt numarası çakıştı. Lütfen yeniden deneyin.")
                    safeRow("CreatedBy") = currentUser
                    safeRow("CreatedAt") = nowText
                    rows.Add(safeRow)
                    Return
                End If

                If existing Is Nothing Then Throw New InvalidOperationException("Düzenlenecek vardiya takip kaydı bulunamadı.")
                Dim originalOccurredAt = GetValue(existing, "OccurredAt").Trim()
                If originalOccurredAt <> "" Then safeRow("OccurredAt") = originalOccurredAt
                safeRow("CreatedBy") = GetValue(existing, "CreatedBy")
                safeRow("CreatedAt") = GetValue(existing, "CreatedAt")
                For Each header In PlasticShiftTrackingHeaders
                    existing(header) = safeRow(header)
                Next
            End Sub)

        AuditService.Log(
            If(mechanismMode,
               If(isNew, "MECHANISM_SHIFT_CREATE", "MECHANISM_SHIFT_UPDATE"),
               If(isNew, "PLASTIC_SHIFT_CREATE", "PLASTIC_SHIFT_UPDATE")),
            "",
            "",
            "Kayıt No=" & recordId & "; Ürün=" & productNameCode & "; Hatalı Adet/Miktar=" & defectiveQuantity)
        Return recordId
    End Function

    Public Shared Function GetPlasticShiftErrorReport(shiftRecordId As String) As Dictionary(Of String, String)
        AuthorizationService.Require(AppState.CanOpenPlasticShiftErrorReport, "Vardiya Hata Raporu")
        shiftRecordId = If(shiftRecordId, "").Trim()
        If shiftRecordId = "" Then Return Nothing

        Dim row = CsvUtil.ReadRows(AppPaths.PlasticShiftErrorReportsCsv).
            FirstOrDefault(
                Function(item) String.Equals(
                    GetValue(item, "ShiftRecordId"),
                    shiftRecordId,
                    StringComparison.OrdinalIgnoreCase))
        If row Is Nothing Then Return Nothing
        EnsurePlasticShiftErrorReportEvaluations(GetValue(row, "ReportId"))
        Return New Dictionary(Of String, String)(row, StringComparer.OrdinalIgnoreCase)
    End Function

    Public Shared Function GetPlasticShiftErrorReports() As List(Of Dictionary(Of String, String))
        AuthorizationService.Require(AppState.CanOpenPlasticShiftErrorReport, "Hata Raporları")
        Return CsvUtil.ReadRows(AppPaths.PlasticShiftErrorReportsCsv).
            Select(Function(row) New Dictionary(Of String, String)(row, StringComparer.OrdinalIgnoreCase)).
            ToList()
    End Function

    Public Shared Sub DeletePlasticShiftErrorReport(reportId As String)
        AuthorizationService.Require(AppState.CanDeletePlasticShiftErrorReport, "Hata Raporu Silme")

        reportId = If(reportId, "").Trim()
        If reportId = "" Then Throw New ArgumentException("Silinecek hata raporu seçilmedi.")

        Dim reports = CsvUtil.ReadRowsLocked(AppPaths.PlasticShiftErrorReportsCsv)
        Dim selectedReport = reports.FirstOrDefault(
            Function(row) String.Equals(
                GetValue(row, "ReportId"),
                reportId,
                StringComparison.OrdinalIgnoreCase))
        If selectedReport Is Nothing Then Throw New InvalidOperationException("Silinecek hata raporu bulunamadı.")

        Dim reportNo = GetValue(selectedReport, "ReportNo")
        Dim shiftRecordId = GetValue(selectedReport, "ShiftRecordId")
        Dim trNo = GetValue(selectedReport, "TrNo")
        Dim status = GetValue(selectedReport, "Status")
        Dim removedReportIds = reports.
            Where(
                Function(row)
                    Return String.Equals(GetValue(row, "ReportId"), reportId, StringComparison.OrdinalIgnoreCase) OrElse
                           (shiftRecordId <> "" AndAlso
                            String.Equals(GetValue(row, "ShiftRecordId"), shiftRecordId, StringComparison.OrdinalIgnoreCase)) OrElse
                           (reportNo <> "" AndAlso
                            String.Equals(GetValue(row, "ReportNo"), reportNo, StringComparison.OrdinalIgnoreCase))
                End Function).
            Select(Function(row) GetValue(row, "ReportId").Trim()).
            Where(Function(value) value <> "").
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToHashSet(StringComparer.OrdinalIgnoreCase)
        removedReportIds.Add(reportId)

        Dim deletedCount = CsvUtil.DeleteRowsLocked(
            AppPaths.PlasticShiftErrorReportsCsv,
            PlasticShiftErrorReportHeaders,
            Function(row)
                Return removedReportIds.Contains(GetValue(row, "ReportId").Trim()) OrElse
                       (shiftRecordId <> "" AndAlso
                        String.Equals(GetValue(row, "ShiftRecordId"), shiftRecordId, StringComparison.OrdinalIgnoreCase)) OrElse
                       (reportNo <> "" AndAlso
                        String.Equals(GetValue(row, "ReportNo"), reportNo, StringComparison.OrdinalIgnoreCase))
            End Function)

        If deletedCount = 0 Then Throw New InvalidOperationException("Silinecek hata raporu bulunamadı.")

        CsvUtil.DeleteRowsLocked(
            AppPaths.PlasticShiftErrorReportEvaluationsCsv,
            PlasticShiftErrorReportEvaluationHeaders,
            Function(row) removedReportIds.Contains(GetValue(row, "ReportId").Trim()))

        CsvUtil.DeleteRowsLocked(
            AppPaths.PlasticShiftErrorReportEmailEventsCsv,
            PlasticShiftErrorReportEmailEventHeaders,
            Function(row) removedReportIds.Contains(GetValue(row, "ReportId").Trim()))

        If shiftRecordId <> "" Then
            Dim anotherReportExists = CsvUtil.ReadRows(AppPaths.PlasticShiftErrorReportsCsv).
                Any(Function(row) String.Equals(
                    GetValue(row, "ShiftRecordId"),
                    shiftRecordId,
                    StringComparison.OrdinalIgnoreCase))

            If Not anotherReportExists Then
                Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                CsvUtil.UpdateRowsLocked(
                    AppPaths.PlasticShiftTrackingRecordsCsv,
                    PlasticShiftTrackingHeaders,
                    Sub(rows)
                        Dim source = rows.FirstOrDefault(
                            Function(row) String.Equals(
                                GetValue(row, "RecordId"),
                                shiftRecordId,
                                StringComparison.OrdinalIgnoreCase))
                        If source Is Nothing Then Return
                        source("ErrorReport") = "NO"
                        source("UpdatedBy") = AppState.CurrentUserName
                        source("UpdatedAt") = nowText
                    End Sub)
            End If
        End If

        AuditService.Log(
            "PLASTIC_SHIFT_ERROR_REPORT_DELETE",
            trNo,
            "",
            "Rapor No=" & reportNo &
            "; Rapor Id=" & reportId &
            "; Vardiya Kayıt No=" & shiftRecordId &
            "; Önceki Durum=" & status &
            "; Silinen Satır=" & deletedCount.ToString())
    End Sub

    Public Shared Function SavePlasticShiftErrorReport(inputRow As Dictionary(Of String, String)) As String
        If Not AppState.CanCreatePlasticShiftErrorReport AndAlso Not AppState.CanManagePlasticShiftErrorReport Then
            Throw New UnauthorizedAccessException("Vardiya hata raporu kaydetme yetkiniz bulunmuyor.")
        End If
        If inputRow Is Nothing Then Throw New ArgumentNullException(NameOf(inputRow))

        Dim currentUser = If(AppState.CurrentUserName, "").Trim()
        If currentUser = "" Then Throw New UnauthorizedAccessException("Hata raporu için aktif kullanıcı oturumu gereklidir.")

        Dim shiftRecordId = GetValue(inputRow, "ShiftRecordId").Trim()
        If shiftRecordId = "" Then Throw New ArgumentException("Bağlı vardiya takip kaydı bulunamadı.")

        Dim sourceRecord = CsvUtil.ReadRows(AppPaths.PlasticShiftTrackingRecordsCsv).
            FirstOrDefault(
                Function(item) String.Equals(
                    GetValue(item, "RecordId"),
                    shiftRecordId,
                    StringComparison.OrdinalIgnoreCase))
        If sourceRecord Is Nothing Then Throw New InvalidOperationException("Bağlı vardiya takip kaydı artık bulunamıyor.")

        Dim reportId = GetValue(inputRow, "ReportId").Trim()
        Dim reportNo As String = ""
        Dim isNew As Boolean = False
        Dim now = DateTime.Now
        Dim nowText = now.ToString("yyyy-MM-dd HH:mm:ss")

        Dim initialFields As New HashSet(Of String)(
            {
                "RevisionDate", "SourceDepartment", "QualityControlPoint", "PartNameNo", "TrNo", "PartType",
                "Quantity", "MachineNo", "OperatorName", "DefectArea", "DefectCode", "DefectType",
                "NonconformityDescription", "QualityInspector", "DetectedBy", "UnitManagerApproval"
            },
            StringComparer.OrdinalIgnoreCase)

        Dim managementFields As New HashSet(Of String)(
            PlasticShiftErrorReportHeaders.
                Where(Function(header)
                          Return Not initialFields.Contains(header) AndAlso
                                 header <> "ReportId" AndAlso header <> "ShiftRecordId" AndAlso
                                 header <> "ReportNo" AndAlso header <> "CreatedBy" AndAlso
                                 header <> "CreatedAt" AndAlso header <> "UpdatedBy" AndAlso
                                 header <> "UpdatedAt" AndAlso header <> "ComputerName"
                      End Function),
            StringComparer.OrdinalIgnoreCase)

        CsvUtil.UpdateRowsLocked(
            AppPaths.PlasticShiftErrorReportsCsv,
            PlasticShiftErrorReportHeaders,
            Sub(rows)
                Dim existing = rows.FirstOrDefault(
                    Function(item)
                        Return (reportId <> "" AndAlso
                                String.Equals(GetValue(item, "ReportId"), reportId, StringComparison.OrdinalIgnoreCase)) OrElse
                               String.Equals(GetValue(item, "ShiftRecordId"), shiftRecordId, StringComparison.OrdinalIgnoreCase)
                    End Function)

                isNew = existing Is Nothing
                If isNew AndAlso Not AppState.CanCreatePlasticShiftErrorReport AndAlso Not AppState.CanManagePlasticShiftErrorReport Then
                    Throw New UnauthorizedAccessException("Yeni hata raporu oluşturma yetkiniz bulunmuyor.")
                End If
                If existing IsNot Nothing AndAlso
                   String.Equals(GetValue(existing, "Status"), "CLOSED", StringComparison.OrdinalIgnoreCase) AndAlso
                   Not AppState.CanManagePlasticShiftErrorReport Then
                    Throw New UnauthorizedAccessException("Kapatılmış hata raporunu yalnızca Kalite Kontrol Yöneticisi veya Admin değiştirebilir.")
                End If

                Dim safeRow As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                For Each header In PlasticShiftErrorReportHeaders
                    safeRow(header) = If(existing Is Nothing, "", GetValue(existing, header))
                Next

                If isNew Then
                    reportId = "HER-" & now.ToString("yyyyMMdd-HHmmss") & "-" &
                               Guid.NewGuid().ToString("N").Substring(0, 5).ToUpperInvariant()
                    Dim yearToken = now.ToString("yy")
                    Dim maxSequence As Integer = 0
                    Dim pattern = "^HR-A-" & Regex.Escape(yearToken) & "-(\d+)$"
                    For Each item In rows
                        Dim match = Regex.Match(GetValue(item, "ReportNo").Trim(), pattern, RegexOptions.IgnoreCase)
                        If Not match.Success Then Continue For
                        Dim sequence As Integer
                        If Integer.TryParse(match.Groups(1).Value, sequence) Then
                            maxSequence = Math.Max(maxSequence, sequence)
                        End If
                    Next
                    reportNo = "HR-A-" & yearToken & "-" & (maxSequence + 1).ToString("00")
                    safeRow("CreatedBy") = currentUser
                    safeRow("CreatedAt") = nowText
                Else
                    reportId = GetValue(existing, "ReportId")
                    reportNo = GetValue(existing, "ReportNo")
                End If

                safeRow("ReportId") = reportId
                safeRow("ShiftRecordId") = shiftRecordId
                safeRow("ReportNo") = reportNo

                For Each header In initialFields
                    safeRow(header) = GetValue(inputRow, header).Trim()
                Next
                If AppState.CanManagePlasticShiftErrorReport Then
                    For Each header In managementFields
                        safeRow(header) = GetValue(inputRow, header).Trim()
                    Next

                    If existing IsNot Nothing Then
                        Dim managementChanged = managementFields.
                            Where(Function(header) Not String.Equals(header, "Status", StringComparison.OrdinalIgnoreCase)).
                            Any(Function(header) Not String.Equals(GetValue(existing, header).Trim(),
                                                                   safeRow(header).Trim(),
                                                                   StringComparison.Ordinal))
                        If managementChanged AndAlso Not ArePlasticShiftErrorReportEvaluationsApproved(reportId) Then
                            Throw New InvalidOperationException(
                                "Değerlendirme ve aksiyon alanları, üç değerlendiricinin de onayı tamamlandıktan sonra değiştirilebilir.")
                        End If
                    End If
                End If

                If safeRow("RevisionDate") = "" Then safeRow("RevisionDate") = now.ToString("yyyy-MM-dd")
                If safeRow("PartNameNo") = "" Then safeRow("PartNameNo") = GetValue(sourceRecord, "ProductNameCode")
                If safeRow("Quantity") = "" Then safeRow("Quantity") = GetValue(sourceRecord, "DefectiveQuantity")
                If safeRow("NonconformityDescription") = "" Then safeRow("NonconformityDescription") = GetValue(sourceRecord, "Problem")
                If safeRow("DetectedBy") = "" Then safeRow("DetectedBy") = currentUser
                If safeRow("QualityInspector") = "" Then safeRow("QualityInspector") = currentUser

                Dim hasManagementWork =
                    safeRow("Disposition") <> "" OrElse safeRow("RootCause") <> "" OrElse
                    Enumerable.Range(1, 5).Any(Function(index) safeRow("Action" & index.ToString()) <> "")
                Dim evaluationRows = GetPlasticShiftErrorReportEvaluationsInternal(reportId)
                Dim hasRevisionRequest = evaluationRows.Any(
                    Function(item) String.Equals(item.Decision, "REVISION_REQUIRED", StringComparison.OrdinalIgnoreCase))
                Dim allEvaluationsApproved =
                    PlasticShiftErrorReportEvaluationPositions.AllKeys().
                        All(Function(positionKey)
                                Return evaluationRows.Any(
                                    Function(item)
                                        Return String.Equals(item.PositionKey, positionKey, StringComparison.OrdinalIgnoreCase) AndAlso
                                               String.Equals(item.Decision, "APPROVED", StringComparison.OrdinalIgnoreCase)
                                    End Function)
                            End Function)

                If isNew Then
                    safeRow("Status") = "PENDING_EVALUATION"
                ElseIf hasRevisionRequest Then
                    safeRow("Status") = "REVISION_REQUIRED"
                ElseIf evaluationRows.Count > 0 AndAlso Not allEvaluationsApproved Then
                    safeRow("Status") = "PENDING_EVALUATION"
                ElseIf NormalizeYesNoFlag(safeRow("CloseApproved")) = "YES" Then
                    safeRow("Status") = "CLOSED"
                ElseIf hasManagementWork Then
                    safeRow("Status") = "IN_PROGRESS"
                ElseIf allEvaluationsApproved Then
                    safeRow("Status") = "APPROVED"
                Else
                    safeRow("Status") = "OPEN"
                End If

                safeRow("UpdatedBy") = currentUser
                safeRow("UpdatedAt") = nowText
                safeRow("ComputerName") = Environment.MachineName

                If existing Is Nothing Then
                    rows.Add(safeRow)
                Else
                    For Each header In PlasticShiftErrorReportHeaders
                        existing(header) = safeRow(header)
                    Next
                End If
            End Sub)

        If isNew Then
            EnsurePlasticShiftErrorReportEvaluations(reportId)
        End If

        CsvUtil.UpdateRowsLocked(
            AppPaths.PlasticShiftTrackingRecordsCsv,
            PlasticShiftTrackingHeaders,
            Sub(rows)
                Dim source = rows.FirstOrDefault(
                    Function(item) String.Equals(
                        GetValue(item, "RecordId"),
                        shiftRecordId,
                        StringComparison.OrdinalIgnoreCase))
                If source Is Nothing Then Return
                source("ErrorReport") = "YES"
                source("UpdatedBy") = currentUser
                source("UpdatedAt") = nowText
            End Sub)

        AuditService.Log(
            If(isNew, "PLASTIC_SHIFT_ERROR_REPORT_CREATE", "PLASTIC_SHIFT_ERROR_REPORT_UPDATE"),
            GetValue(inputRow, "TrNo"),
            "",
            "Rapor No=" & reportNo & "; Vardiya Kayıt No=" & shiftRecordId)
        Return reportId
    End Function

    Public Shared Function GetPlasticShiftErrorReportEvaluatorAssignments() As List(Of PlasticShiftErrorReportEvaluatorAssignment)
        AuthorizationService.Require(AppState.CanOpenPlasticShiftErrorReport OrElse AppState.IsAdmin,
                                     "Hata Raporu Değerlendirme Atamaları")
        Return CsvUtil.ReadRows(AppPaths.PlasticShiftErrorReportEvaluatorAssignmentsCsv).
            Select(Function(row) New PlasticShiftErrorReportEvaluatorAssignment With {
                .PositionKey = GetValue(row, "PositionKey"),
                .PositionName = GetValue(row, "PositionName"),
                .RequiredRole = GetValue(row, "RequiredRole"),
                .UserName = GetValue(row, "UserName"),
                .Email = GetValue(row, "Email"),
                .IsActive = If(GetValue(row, "IsActive").Trim() = "", "YES", GetValue(row, "IsActive")),
                .UpdatedBy = GetValue(row, "UpdatedBy"),
                .UpdatedAt = GetValue(row, "UpdatedAt")
            }).
            OrderBy(Function(item) Array.IndexOf(PlasticShiftErrorReportEvaluationPositions.AllKeys(), item.PositionKey)).
            ToList()
    End Function

    Public Shared Sub SavePlasticShiftErrorReportEvaluatorAssignments(
        assignments As IEnumerable(Of PlasticShiftErrorReportEvaluatorAssignment))

        AuthorizationService.Require(AppState.IsAdmin, "Hata Raporu Değerlendirme Atamaları")
        If assignments Is Nothing Then Throw New ArgumentNullException(NameOf(assignments))

        Dim supplied = assignments.
            Where(Function(item) item IsNot Nothing).
            GroupBy(Function(item) If(item.PositionKey, "").Trim(), StringComparer.OrdinalIgnoreCase).
            ToDictionary(Function(group) group.Key, Function(group) group.First(), StringComparer.OrdinalIgnoreCase)
        Dim users = UserService.GetUsers()
        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        Dim safeRows As New List(Of Dictionary(Of String, String))()

        For Each positionKey In PlasticShiftErrorReportEvaluationPositions.AllKeys()
            Dim assignment As PlasticShiftErrorReportEvaluatorAssignment = Nothing
            If Not supplied.TryGetValue(positionKey, assignment) Then
                Throw New ArgumentException(PlasticShiftErrorReportEvaluationPositions.PositionName(positionKey) &
                                            " için değerlendirici seçilmelidir.")
            End If

            Dim userName = If(assignment.UserName, "").Trim()
            Dim email = NormalizeEmailAddress(assignment.Email)
            If userName = "" Then
                Throw New ArgumentException(PlasticShiftErrorReportEvaluationPositions.PositionName(positionKey) &
                                            " için kullanıcı seçilmelidir.")
            End If
            If email = "" Then Throw New ArgumentException(userName & " için e-posta adresi girilmelidir.")
            ValidateEmailAddress(email)

            Dim user = users.FirstOrDefault(
                Function(row)
                    Return String.Equals(GetValue(row, "Username"), userName, StringComparison.OrdinalIgnoreCase) AndAlso
                           NormalizeYesNoFlag(GetValue(row, "IsActive")) = "YES"
                End Function)
            If user Is Nothing Then Throw New InvalidOperationException(userName & " aktif kullanıcılar arasında bulunamadı.")

            Dim requiredRole = PlasticShiftErrorReportEvaluationPositions.RequiredRole(positionKey)
            If Not String.Equals(AppState.NormalizeRole(GetValue(user, "Role")),
                                 AppState.NormalizeRole(requiredRole),
                                 StringComparison.OrdinalIgnoreCase) Then
                Throw New InvalidOperationException(
                    PlasticShiftErrorReportEvaluationPositions.PositionName(positionKey) &
                    " için yalnızca " & requiredRole & " rolündeki bir kullanıcı seçilebilir.")
            End If

            safeRows.Add(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                {"PositionKey", positionKey},
                {"PositionName", PlasticShiftErrorReportEvaluationPositions.PositionName(positionKey)},
                {"RequiredRole", requiredRole},
                {"UserName", userName},
                {"Email", email},
                {"IsActive", "YES"},
                {"UpdatedBy", AppState.CurrentUserName},
                {"UpdatedAt", nowText}
            })
        Next

        CsvUtil.UpdateRowsLocked(
            AppPaths.PlasticShiftErrorReportEvaluatorAssignmentsCsv,
            PlasticShiftErrorReportEvaluatorAssignmentHeaders,
            Sub(rows)
                rows.Clear()
                rows.AddRange(safeRows)
            End Sub)

        AuditService.Log("PLASTIC_SHIFT_ERROR_REPORT_ASSIGNMENTS_SAVE", "", "",
                         String.Join("; ", safeRows.Select(Function(row) GetValue(row, "PositionName") & "=" & GetValue(row, "UserName"))))
    End Sub

    Public Shared Sub EnsurePlasticShiftErrorReportEvaluations(reportId As String)
        reportId = If(reportId, "").Trim()
        If reportId = "" Then Return

        Dim assignments = CsvUtil.ReadRows(AppPaths.PlasticShiftErrorReportEvaluatorAssignmentsCsv).
            Where(Function(row) NormalizeYesNoFlag(GetValue(row, "IsActive")) = "YES").
            ToList()
        If assignments.Count = 0 Then Return

        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        CsvUtil.UpdateRowsLocked(
            AppPaths.PlasticShiftErrorReportEvaluationsCsv,
            PlasticShiftErrorReportEvaluationHeaders,
            Sub(rows)
                For Each positionKey In PlasticShiftErrorReportEvaluationPositions.AllKeys()
                    If rows.Any(Function(row)
                                    Return String.Equals(GetValue(row, "ReportId"), reportId, StringComparison.OrdinalIgnoreCase) AndAlso
                                           String.Equals(GetValue(row, "PositionKey"), positionKey, StringComparison.OrdinalIgnoreCase)
                                End Function) Then Continue For

                    Dim assignment = assignments.FirstOrDefault(
                        Function(row) String.Equals(GetValue(row, "PositionKey"), positionKey, StringComparison.OrdinalIgnoreCase))
                    If assignment Is Nothing Then Continue For

                    rows.Add(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                        {"EvaluationId", "HDE-" & Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant()},
                        {"ReportId", reportId},
                        {"PositionKey", positionKey},
                        {"PositionName", PlasticShiftErrorReportEvaluationPositions.PositionName(positionKey)},
                        {"RequiredRole", PlasticShiftErrorReportEvaluationPositions.RequiredRole(positionKey)},
                        {"AssignedUserName", GetValue(assignment, "UserName")},
                        {"AssignedEmail", GetValue(assignment, "Email")},
                        {"Decision", "PENDING"},
                        {"Explanation", ""},
                        {"EvaluatedBy", ""},
                        {"EvaluatedAt", ""},
                        {"UpdatedAt", nowText},
                        {"ComputerName", Environment.MachineName}
                    })
                Next
            End Sub)
    End Sub

    Public Shared Function GetPlasticShiftErrorReportEvaluations(reportId As String) As List(Of PlasticShiftErrorReportEvaluation)
        AuthorizationService.Require(AppState.CanOpenPlasticShiftErrorReport, "Hata Raporu Değerlendirmeleri")
        reportId = If(reportId, "").Trim()
        If reportId = "" Then Return New List(Of PlasticShiftErrorReportEvaluation)()
        EnsurePlasticShiftErrorReportEvaluations(reportId)
        Return GetPlasticShiftErrorReportEvaluationsInternal(reportId)
    End Function

    Public Shared Function GetAllPlasticShiftErrorReportEvaluations() As List(Of PlasticShiftErrorReportEvaluation)
        AuthorizationService.Require(AppState.CanOpenPlasticShiftErrorReport, "Hata Raporu Değerlendirmeleri")
        Return MapPlasticShiftErrorReportEvaluations(
            CsvUtil.ReadRows(AppPaths.PlasticShiftErrorReportEvaluationsCsv))
    End Function

    Private Shared Function GetPlasticShiftErrorReportEvaluationsInternal(reportId As String) As List(Of PlasticShiftErrorReportEvaluation)
        reportId = If(reportId, "").Trim()
        If reportId = "" Then Return New List(Of PlasticShiftErrorReportEvaluation)()

        Return MapPlasticShiftErrorReportEvaluations(
            CsvUtil.ReadRows(AppPaths.PlasticShiftErrorReportEvaluationsCsv).
                Where(Function(row) String.Equals(GetValue(row, "ReportId"), reportId, StringComparison.OrdinalIgnoreCase)))
    End Function

    Private Shared Function MapPlasticShiftErrorReportEvaluations(
        rows As IEnumerable(Of Dictionary(Of String, String))) As List(Of PlasticShiftErrorReportEvaluation)

        Return rows.
            Select(Function(row) New PlasticShiftErrorReportEvaluation With {
                .EvaluationId = GetValue(row, "EvaluationId"),
                .ReportId = GetValue(row, "ReportId"),
                .PositionKey = GetValue(row, "PositionKey"),
                .PositionName = GetValue(row, "PositionName"),
                .RequiredRole = GetValue(row, "RequiredRole"),
                .AssignedUserName = GetValue(row, "AssignedUserName"),
                .AssignedEmail = GetValue(row, "AssignedEmail"),
                .Decision = If(GetValue(row, "Decision").Trim() = "", "PENDING", GetValue(row, "Decision")),
                .Explanation = GetValue(row, "Explanation"),
                .EvaluatedBy = GetValue(row, "EvaluatedBy"),
                .EvaluatedAt = GetValue(row, "EvaluatedAt"),
                .UpdatedAt = GetValue(row, "UpdatedAt"),
                .ComputerName = GetValue(row, "ComputerName")
            }).
            OrderBy(Function(item) Array.IndexOf(PlasticShiftErrorReportEvaluationPositions.AllKeys(), item.PositionKey)).
            ToList()
    End Function

    Public Shared Sub SavePlasticShiftErrorReportEvaluation(reportId As String,
                                                             positionKey As String,
                                                             decision As String,
                                                             explanation As String)
        AuthorizationService.Require(AppState.CanOpenPlasticShiftErrorReport, "Hata Raporu Değerlendirmesi")
        reportId = If(reportId, "").Trim()
        positionKey = If(positionKey, "").Trim().ToUpperInvariant()
        decision = If(decision, "").Trim().ToUpperInvariant()
        explanation = If(explanation, "").Trim()
        If reportId = "" OrElse Not PlasticShiftErrorReportEvaluationPositions.AllKeys().Contains(positionKey) Then
            Throw New ArgumentException("Değerlendirme kaydı bulunamadı.")
        End If
        If decision <> "APPROVED" AndAlso decision <> "REVISION_REQUIRED" Then
            Throw New ArgumentException("Karar olarak Onay veya Revizyon Gerekli seçilmelidir.")
        End If
        If decision = "REVISION_REQUIRED" AndAlso explanation = "" Then
            Throw New ArgumentException("Revizyon gerekli kararında açıklama zorunludur.")
        End If

        EnsurePlasticShiftErrorReportEvaluations(reportId)
        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        CsvUtil.UpdateRowsLocked(
            AppPaths.PlasticShiftErrorReportEvaluationsCsv,
            PlasticShiftErrorReportEvaluationHeaders,
            Sub(rows)
                Dim row = rows.FirstOrDefault(
                    Function(item)
                        Return String.Equals(GetValue(item, "ReportId"), reportId, StringComparison.OrdinalIgnoreCase) AndAlso
                               String.Equals(GetValue(item, "PositionKey"), positionKey, StringComparison.OrdinalIgnoreCase)
                    End Function)
                If row Is Nothing Then Throw New InvalidOperationException("Bu pozisyon için değerlendirme ataması bulunamadı.")
                Dim assignedUser = GetValue(row, "AssignedUserName").Trim()
                If Not AppState.IsAdmin AndAlso
                   Not String.Equals(assignedUser, AppState.CurrentUserName, StringComparison.OrdinalIgnoreCase) Then
                    Throw New UnauthorizedAccessException("Yalnızca bu pozisyona atanmış kullanıcı kendi değerlendirmesini değiştirebilir.")
                End If

                row("Decision") = decision
                row("Explanation") = explanation
                row("EvaluatedBy") = AppState.CurrentUserName
                row("EvaluatedAt") = nowText
                row("UpdatedAt") = nowText
                row("ComputerName") = Environment.MachineName
            End Sub)

        RefreshPlasticShiftErrorReportEvaluationStatus(reportId)
        AuditService.Log("PLASTIC_SHIFT_ERROR_REPORT_EVALUATION_SAVE", "", "",
                         "Rapor=" & reportId & "; Pozisyon=" & positionKey & "; Karar=" & decision)
    End Sub

    Public Shared Function ArePlasticShiftErrorReportEvaluationsApproved(reportId As String) As Boolean
        Dim rows = GetPlasticShiftErrorReportEvaluationsInternal(reportId)
        Return PlasticShiftErrorReportEvaluationPositions.AllKeys().
            All(Function(positionKey)
                    Return rows.Any(
                        Function(item)
                            Return String.Equals(item.PositionKey, positionKey, StringComparison.OrdinalIgnoreCase) AndAlso
                                   String.Equals(item.Decision, "APPROVED", StringComparison.OrdinalIgnoreCase)
                        End Function)
                End Function)
    End Function

    Private Shared Sub RefreshPlasticShiftErrorReportEvaluationStatus(reportId As String)
        Dim evaluations = GetPlasticShiftErrorReportEvaluationsInternal(reportId)
        Dim hasRevision = evaluations.Any(
            Function(item) String.Equals(item.Decision, "REVISION_REQUIRED", StringComparison.OrdinalIgnoreCase))
        Dim allApproved = ArePlasticShiftErrorReportEvaluationsApproved(reportId)
        CsvUtil.UpdateRowsLocked(
            AppPaths.PlasticShiftErrorReportsCsv,
            PlasticShiftErrorReportHeaders,
            Sub(rows)
                Dim row = rows.FirstOrDefault(
                    Function(item) String.Equals(GetValue(item, "ReportId"), reportId, StringComparison.OrdinalIgnoreCase))
                If row Is Nothing Then Return
                If String.Equals(GetValue(row, "Status"), "CLOSED", StringComparison.OrdinalIgnoreCase) Then Return
                row("Status") = If(hasRevision, "REVISION_REQUIRED", If(allApproved, "APPROVED", "PENDING_EVALUATION"))
                row("UpdatedBy") = AppState.CurrentUserName
                row("UpdatedAt") = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            End Sub)
    End Sub

    Public Shared Function HasPlasticShiftErrorReportEmailEvent(eventKey As String) As Boolean
        eventKey = If(eventKey, "").Trim()
        If eventKey = "" Then Return False
        Return CsvUtil.ReadRows(AppPaths.PlasticShiftErrorReportEmailEventsCsv).
            Any(Function(row) String.Equals(GetValue(row, "EventKey"), eventKey, StringComparison.OrdinalIgnoreCase))
    End Function

    Public Shared Sub RecordPlasticShiftErrorReportEmailEvent(eventKey As String,
                                                               reportId As String,
                                                               eventType As String,
                                                               recipients As String)
        Dim row As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {"EventKey", If(eventKey, "").Trim()},
            {"ReportId", If(reportId, "").Trim()},
            {"EventType", If(eventType, "").Trim()},
            {"SentAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")},
            {"SentBy", AppState.CurrentUserName},
            {"ComputerName", Environment.MachineName},
            {"Recipients", If(recipients, "").Trim()}
        }
        CsvUtil.UpdateRowsLocked(
            AppPaths.PlasticShiftErrorReportEmailEventsCsv,
            PlasticShiftErrorReportEmailEventHeaders,
            Sub(rows)
                If rows.Any(Function(item) String.Equals(GetValue(item, "EventKey"), eventKey, StringComparison.OrdinalIgnoreCase)) Then Return
                rows.Add(row)
            End Sub)
    End Sub

    Public Shared Sub DeletePlasticShiftTrackingRecord(recordId As String)
        DeleteShiftTrackingRecord(recordId, False)
    End Sub

    Public Shared Sub DeleteMechanismShiftTrackingRecord(recordId As String)
        DeleteShiftTrackingRecord(recordId, True)
    End Sub

    Private Shared Sub DeleteShiftTrackingRecord(recordId As String, mechanismMode As Boolean)
        Dim canDelete = If(mechanismMode, AppState.CanDeleteMechanismShiftTracking, AppState.CanDeletePlasticShiftTracking)
        Dim featureName = If(mechanismMode, "Mekanizma Vardiya Takip Kaydı Silme", "Plastikhane Vardiya Takip Kaydı Silme")
        Dim targetPath = If(mechanismMode, AppPaths.MechanismShiftTrackingRecordsCsv, AppPaths.PlasticShiftTrackingRecordsCsv)
        AuthorizationService.Require(canDelete, featureName)
        recordId = If(recordId, "").Trim()
        If recordId = "" Then Throw New ArgumentException("Silinecek kayıt numarası boş olamaz.")

        Dim deleted As Boolean = CsvUtil.UpdateRowsLocked(
            targetPath,
            PlasticShiftTrackingHeaders,
            Function(rows)
                Return rows.RemoveAll(
                    Function(row) String.Equals(GetValue(row, "RecordId"), recordId, StringComparison.OrdinalIgnoreCase)) > 0
            End Function)

        If Not deleted Then Throw New InvalidOperationException("Silinecek vardiya takip kaydı bulunamadı.")
        AuditService.Log(If(mechanismMode, "MECHANISM_SHIFT_DELETE", "PLASTIC_SHIFT_DELETE"), "", "", "Kayıt No=" & recordId)
    End Sub

    Public Shared Function GetPlasticShiftEmailRecipients(Optional activeOnly As Boolean = False) As List(Of PlasticShiftEmailRecipient)
        AuthorizationService.Require(AppState.CanOpenPlasticShiftTracking, "Plastikhane Vardiya Takip Mail Alıcıları")

        Return CsvUtil.ReadRows(AppPaths.PlasticShiftEmailRecipientsCsv).
            Select(Function(row) New PlasticShiftEmailRecipient With {
                .Email = GetValue(row, "Email"),
                .DisplayName = GetValue(row, "DisplayName"),
                .RecipientType = NormalizePlasticShiftEmailRecipientType(GetValue(row, "RecipientType")),
                .IsActive = If(GetValue(row, "IsActive").Trim() = "", "YES", GetValue(row, "IsActive")),
                .CreatedBy = GetValue(row, "CreatedBy"),
                .CreatedAt = GetValue(row, "CreatedAt"),
                .UpdatedBy = GetValue(row, "UpdatedBy"),
                .UpdatedAt = GetValue(row, "UpdatedAt")
            }).
            Where(Function(item) Not activeOnly OrElse String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase)).
            OrderBy(Function(item) If(String.Equals(item.RecipientType, "CC", StringComparison.OrdinalIgnoreCase), 1, 0)).
            ThenBy(Function(item) item.DisplayName).
            ThenBy(Function(item) item.Email).
            ToList()
    End Function

    Public Shared Sub SavePlasticShiftEmailRecipient(originalEmail As String, recipient As PlasticShiftEmailRecipient)
        AuthorizationService.Require(AppState.CanManagePlasticShiftEmailRecipients, "Plastikhane Vardiya Takip Mail Alıcıları")
        If recipient Is Nothing Then Throw New ArgumentNullException(NameOf(recipient))

        Dim oldEmail = NormalizeEmailAddress(originalEmail)
        Dim email = NormalizeEmailAddress(recipient.Email)
        If email = "" Then Throw New ArgumentException("E-posta adresi boş olamaz.")
        ValidateEmailAddress(email)

        Dim displayName = CleanTestRequestSingleLine(recipient.DisplayName, 150)
        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        Dim currentUser = If(AppState.CurrentUserName, "").Trim()
        Dim recipientType = NormalizePlasticShiftEmailRecipientType(recipient.RecipientType)
        Dim activeFlag = NormalizeYesNoFlag(recipient.IsActive)

        CsvUtil.UpdateRowsLocked(
            AppPaths.PlasticShiftEmailRecipientsCsv,
            PlasticShiftEmailRecipientHeaders,
            Sub(rows)
                Dim existing = rows.FirstOrDefault(
                    Function(row)
                        Dim rowEmail = NormalizeEmailAddress(GetValue(row, "Email"))
                        Return (oldEmail <> "" AndAlso String.Equals(rowEmail, oldEmail, StringComparison.OrdinalIgnoreCase)) OrElse
                               String.Equals(rowEmail, email, StringComparison.OrdinalIgnoreCase)
                    End Function)

                If existing Is Nothing Then
                    Dim newRow As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                    For Each header In PlasticShiftEmailRecipientHeaders
                        newRow(header) = ""
                    Next
                    newRow("Email") = email
                    newRow("DisplayName") = displayName
                    newRow("RecipientType") = recipientType
                    newRow("IsActive") = activeFlag
                    newRow("CreatedBy") = currentUser
                    newRow("CreatedAt") = nowText
                    newRow("UpdatedBy") = currentUser
                    newRow("UpdatedAt") = nowText
                    rows.Add(newRow)
                Else
                    existing("Email") = email
                    existing("DisplayName") = displayName
                    existing("RecipientType") = recipientType
                    existing("IsActive") = activeFlag
                    If GetValue(existing, "CreatedBy").Trim() = "" Then existing("CreatedBy") = currentUser
                    If GetValue(existing, "CreatedAt").Trim() = "" Then existing("CreatedAt") = nowText
                    existing("UpdatedBy") = currentUser
                    existing("UpdatedAt") = nowText
                End If
            End Sub)

        AuditService.Log("PLASTIC_SHIFT_MAIL_RECIPIENT_SAVE", "", "", "E-posta=" & email & "; Tür=" & recipientType)
    End Sub

    Public Shared Sub DeletePlasticShiftEmailRecipient(email As String)
        AuthorizationService.Require(AppState.CanManagePlasticShiftEmailRecipients, "Plastikhane Vardiya Takip Mail Alıcı Silme")
        Dim target = NormalizeEmailAddress(email)
        If target = "" Then Throw New ArgumentException("Silinecek e-posta adresi boş olamaz.")

        Dim deleted As Boolean = CsvUtil.UpdateRowsLocked(
            AppPaths.PlasticShiftEmailRecipientsCsv,
            PlasticShiftEmailRecipientHeaders,
            Function(rows)
                Return rows.RemoveAll(
                    Function(row) String.Equals(NormalizeEmailAddress(GetValue(row, "Email")), target, StringComparison.OrdinalIgnoreCase)) > 0
            End Function)

        If Not deleted Then Throw New InvalidOperationException("Silinecek mail alıcısı bulunamadı.")
        AuditService.Log("PLASTIC_SHIFT_MAIL_RECIPIENT_DELETE", "", "", "E-posta=" & target)
    End Sub

    Public Shared Function GetMechanismShiftEmailRecipients(Optional activeOnly As Boolean = False) As List(Of PlasticShiftEmailRecipient)
        AuthorizationService.Require(AppState.CanOpenMechanismShiftTracking, "Mekanizma Vardiya Takip Mail Alıcıları")

        Return ReadShiftEmailRecipients(AppPaths.MechanismShiftEmailRecipientsCsv, activeOnly)
    End Function

    Public Shared Sub SaveMechanismShiftEmailRecipient(originalEmail As String, recipient As PlasticShiftEmailRecipient)
        AuthorizationService.Require(AppState.CanManagePlasticShiftEmailRecipients, "Mekanizma Vardiya Takip Mail Alıcıları")
        SaveShiftEmailRecipient(AppPaths.MechanismShiftEmailRecipientsCsv, originalEmail, recipient, "MECHANISM_SHIFT_MAIL_RECIPIENT_SAVE")
    End Sub

    Public Shared Sub DeleteMechanismShiftEmailRecipient(email As String)
        AuthorizationService.Require(AppState.CanManagePlasticShiftEmailRecipients, "Mekanizma Vardiya Takip Mail Alıcısı Silme")
        DeleteShiftEmailRecipient(AppPaths.MechanismShiftEmailRecipientsCsv, email, "MECHANISM_SHIFT_MAIL_RECIPIENT_DELETE")
    End Sub

    Private Shared Function ReadShiftEmailRecipients(path As String, activeOnly As Boolean) As List(Of PlasticShiftEmailRecipient)
        Return CsvUtil.ReadRows(path).
            Select(Function(row) New PlasticShiftEmailRecipient With {
                .Email = GetValue(row, "Email"),
                .DisplayName = GetValue(row, "DisplayName"),
                .RecipientType = NormalizePlasticShiftEmailRecipientType(GetValue(row, "RecipientType")),
                .IsActive = If(GetValue(row, "IsActive").Trim() = "", "YES", GetValue(row, "IsActive")),
                .CreatedBy = GetValue(row, "CreatedBy"),
                .CreatedAt = GetValue(row, "CreatedAt"),
                .UpdatedBy = GetValue(row, "UpdatedBy"),
                .UpdatedAt = GetValue(row, "UpdatedAt")
            }).
            Where(Function(item) Not activeOnly OrElse String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase)).
            OrderBy(Function(item) If(String.Equals(item.RecipientType, "CC", StringComparison.OrdinalIgnoreCase), 1, 0)).
            ThenBy(Function(item) item.DisplayName).
            ThenBy(Function(item) item.Email).
            ToList()
    End Function

    Private Shared Sub SaveShiftEmailRecipient(path As String, originalEmail As String, recipient As PlasticShiftEmailRecipient, auditAction As String)
        If recipient Is Nothing Then Throw New ArgumentNullException(NameOf(recipient))

        Dim oldEmail = NormalizeEmailAddress(originalEmail)
        Dim email = NormalizeEmailAddress(recipient.Email)
        If email = "" Then Throw New ArgumentException("E-posta adresi boş olamaz.")
        ValidateEmailAddress(email)

        Dim displayName = CleanTestRequestSingleLine(recipient.DisplayName, 150)
        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        Dim currentUser = If(AppState.CurrentUserName, "").Trim()
        Dim recipientType = NormalizePlasticShiftEmailRecipientType(recipient.RecipientType)
        Dim activeFlag = NormalizeYesNoFlag(recipient.IsActive)

        CsvUtil.UpdateRowsLocked(
            path,
            PlasticShiftEmailRecipientHeaders,
            Sub(rows)
                Dim existing = rows.FirstOrDefault(
                    Function(row)
                        Dim rowEmail = NormalizeEmailAddress(GetValue(row, "Email"))
                        Return (oldEmail <> "" AndAlso String.Equals(rowEmail, oldEmail, StringComparison.OrdinalIgnoreCase)) OrElse
                               String.Equals(rowEmail, email, StringComparison.OrdinalIgnoreCase)
                    End Function)

                If existing Is Nothing Then
                    Dim newRow As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                    For Each header In PlasticShiftEmailRecipientHeaders
                        newRow(header) = ""
                    Next
                    newRow("Email") = email
                    newRow("DisplayName") = displayName
                    newRow("RecipientType") = recipientType
                    newRow("IsActive") = activeFlag
                    newRow("CreatedBy") = currentUser
                    newRow("CreatedAt") = nowText
                    newRow("UpdatedBy") = currentUser
                    newRow("UpdatedAt") = nowText
                    rows.Add(newRow)
                Else
                    existing("Email") = email
                    existing("DisplayName") = displayName
                    existing("RecipientType") = recipientType
                    existing("IsActive") = activeFlag
                    If GetValue(existing, "CreatedBy").Trim() = "" Then existing("CreatedBy") = currentUser
                    If GetValue(existing, "CreatedAt").Trim() = "" Then existing("CreatedAt") = nowText
                    existing("UpdatedBy") = currentUser
                    existing("UpdatedAt") = nowText
                End If
            End Sub)

        AuditService.Log(auditAction, "", "", "E-posta=" & email & "; Tür=" & recipientType)
    End Sub

    Private Shared Sub DeleteShiftEmailRecipient(path As String, email As String, auditAction As String)
        Dim target = NormalizeEmailAddress(email)
        If target = "" Then Throw New ArgumentException("Silinecek e-posta adresi boş olamaz.")

        Dim deleted As Boolean = CsvUtil.UpdateRowsLocked(
            path,
            PlasticShiftEmailRecipientHeaders,
            Function(rows)
                Return rows.RemoveAll(
                    Function(row) String.Equals(NormalizeEmailAddress(GetValue(row, "Email")), target, StringComparison.OrdinalIgnoreCase)) > 0
            End Function)

        If Not deleted Then Throw New InvalidOperationException("Silinecek mail alıcısı bulunamadı.")
        AuditService.Log(auditAction, "", "", "E-posta=" & target)
    End Sub

    Public Shared Function GetMoldConnectionPlanEmailRecipients(Optional activeOnly As Boolean = False) As List(Of PlasticShiftEmailRecipient)
        AuthorizationService.Require(AppState.CanOpenMoldConnectionPlan, "Bağlanacak Kalıp Listesi Mail Alıcıları")

        Return CsvUtil.ReadRows(AppPaths.MoldConnectionPlanEmailRecipientsCsv).
            Select(Function(row) New PlasticShiftEmailRecipient With {
                .Email = GetValue(row, "Email"),
                .DisplayName = GetValue(row, "DisplayName"),
                .RecipientType = NormalizePlasticShiftEmailRecipientType(GetValue(row, "RecipientType")),
                .IsActive = If(GetValue(row, "IsActive").Trim() = "", "YES", GetValue(row, "IsActive")),
                .CreatedBy = GetValue(row, "CreatedBy"),
                .CreatedAt = GetValue(row, "CreatedAt"),
                .UpdatedBy = GetValue(row, "UpdatedBy"),
                .UpdatedAt = GetValue(row, "UpdatedAt")
            }).
            Where(Function(item) Not activeOnly OrElse String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase)).
            OrderBy(Function(item) item.DisplayName).
            ThenBy(Function(item) item.Email).
            ToList()
    End Function

    Public Shared Sub SaveMoldConnectionPlanEmailRecipient(originalEmail As String, recipient As PlasticShiftEmailRecipient)
        AuthorizationService.Require(AppState.CanManageMoldConnectionPlanEmailRecipients, "Bağlanacak Kalıp Listesi Mail Alıcıları")
        If recipient Is Nothing Then Throw New ArgumentNullException(NameOf(recipient))

        Dim oldEmail = NormalizeEmailAddress(originalEmail)
        Dim email = NormalizeEmailAddress(recipient.Email)
        If email = "" Then Throw New ArgumentException("E-posta adresi boş olamaz.")
        ValidateEmailAddress(email)

        Dim displayName = CleanTestRequestSingleLine(recipient.DisplayName, 150)
        Dim recipientType = NormalizePlasticShiftEmailRecipientType(recipient.RecipientType)
        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        Dim currentUser = If(AppState.CurrentUserName, "").Trim()
        Dim activeFlag = NormalizeYesNoFlag(recipient.IsActive)

        CsvUtil.UpdateRowsLocked(
            AppPaths.MoldConnectionPlanEmailRecipientsCsv,
            MoldConnectionPlanEmailRecipientHeaders,
            Sub(rows)
                Dim existing = rows.FirstOrDefault(
                    Function(row)
                        Dim rowEmail = NormalizeEmailAddress(GetValue(row, "Email"))
                        Return (oldEmail <> "" AndAlso String.Equals(rowEmail, oldEmail, StringComparison.OrdinalIgnoreCase)) OrElse
                               String.Equals(rowEmail, email, StringComparison.OrdinalIgnoreCase)
                    End Function)

                If existing Is Nothing Then
                    Dim newRow As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                    For Each header In MoldConnectionPlanEmailRecipientHeaders
                        newRow(header) = ""
                    Next
                    newRow("Email") = email
                    newRow("DisplayName") = displayName
                    newRow("RecipientType") = recipientType
                    newRow("IsActive") = activeFlag
                    newRow("CreatedBy") = currentUser
                    newRow("CreatedAt") = nowText
                    newRow("UpdatedBy") = currentUser
                    newRow("UpdatedAt") = nowText
                    rows.Add(newRow)
                Else
                    existing("Email") = email
                    existing("DisplayName") = displayName
                    existing("RecipientType") = recipientType
                    existing("IsActive") = activeFlag
                    If GetValue(existing, "CreatedBy").Trim() = "" Then existing("CreatedBy") = currentUser
                    If GetValue(existing, "CreatedAt").Trim() = "" Then existing("CreatedAt") = nowText
                    existing("UpdatedBy") = currentUser
                    existing("UpdatedAt") = nowText
                End If
            End Sub)

        AuditService.Log("MOLD_CONNECTION_PLAN_MAIL_RECIPIENT_SAVE", "", "", "E-posta=" & email)
    End Sub

    Public Shared Sub DeleteMoldConnectionPlanEmailRecipient(email As String)
        AuthorizationService.Require(AppState.CanManageMoldConnectionPlanEmailRecipients, "Bağlanacak Kalıp Listesi Mail Alıcı Silme")
        Dim target = NormalizeEmailAddress(email)
        If target = "" Then Throw New ArgumentException("Silinecek e-posta adresi boş olamaz.")

        Dim deleted As Boolean = CsvUtil.UpdateRowsLocked(
            AppPaths.MoldConnectionPlanEmailRecipientsCsv,
            MoldConnectionPlanEmailRecipientHeaders,
            Function(rows)
                Return rows.RemoveAll(
                    Function(row) String.Equals(NormalizeEmailAddress(GetValue(row, "Email")), target, StringComparison.OrdinalIgnoreCase)) > 0
            End Function)

        If Not deleted Then Throw New InvalidOperationException("Silinecek mail alıcısı bulunamadı.")
        AuditService.Log("MOLD_CONNECTION_PLAN_MAIL_RECIPIENT_DELETE", "", "", "E-posta=" & target)
    End Sub

    Public Shared Function GetMechanismQualityEmailRecipients(Optional activeOnly As Boolean = False) As List(Of PlasticShiftEmailRecipient)
        AuthorizationService.Require(AppState.CanOpenMechanismQualityControl, "Mekanizma Kalite Kontrol Mail Alıcıları")
        Return CsvUtil.ReadRows(AppPaths.MechanismQualityEmailRecipientsCsv).
            Select(Function(row) New PlasticShiftEmailRecipient With {
                .Email = GetValue(row, "Email"), .DisplayName = GetValue(row, "DisplayName"),
                .RecipientType = NormalizePlasticShiftEmailRecipientType(GetValue(row, "RecipientType")),
                .IsActive = If(GetValue(row, "IsActive").Trim() = "", "YES", GetValue(row, "IsActive")),
                .CreatedBy = GetValue(row, "CreatedBy"), .CreatedAt = GetValue(row, "CreatedAt"),
                .UpdatedBy = GetValue(row, "UpdatedBy"), .UpdatedAt = GetValue(row, "UpdatedAt")}).
            Where(Function(item) Not activeOnly OrElse String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase)).
            OrderBy(Function(item) If(String.Equals(item.RecipientType, "CC", StringComparison.OrdinalIgnoreCase), 1, 0)).
            ThenBy(Function(item) item.DisplayName).ThenBy(Function(item) item.Email).ToList()
    End Function

    Public Shared Sub SaveMechanismQualityEmailRecipient(originalEmail As String, recipient As PlasticShiftEmailRecipient)
        AuthorizationService.Require(AppState.CanManageMechanismQualityEmailRecipients, "Mekanizma Kalite Kontrol Mail Alıcıları")
        If recipient Is Nothing Then Throw New ArgumentNullException(NameOf(recipient))
        Dim oldEmail = NormalizeEmailAddress(originalEmail)
        Dim email = NormalizeEmailAddress(recipient.Email)
        If email = "" Then Throw New ArgumentException("E-posta adresi boş olamaz.")
        ValidateEmailAddress(email)
        Dim displayName = CleanTestRequestSingleLine(recipient.DisplayName, 150)
        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        Dim currentUser = If(AppState.CurrentUserName, "").Trim()
        Dim recipientType = NormalizePlasticShiftEmailRecipientType(recipient.RecipientType)
        Dim activeFlag = NormalizeYesNoFlag(recipient.IsActive)
        CsvUtil.UpdateRowsLocked(AppPaths.MechanismQualityEmailRecipientsCsv, MechanismQualityEmailRecipientHeaders,
            Sub(rows)
                Dim existing = rows.FirstOrDefault(Function(row)
                    Dim rowEmail = NormalizeEmailAddress(GetValue(row, "Email"))
                    Return (oldEmail <> "" AndAlso String.Equals(rowEmail, oldEmail, StringComparison.OrdinalIgnoreCase)) OrElse String.Equals(rowEmail, email, StringComparison.OrdinalIgnoreCase)
                End Function)
                If existing Is Nothing Then
                    Dim newRow As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                    For Each header In MechanismQualityEmailRecipientHeaders : newRow(header) = "" : Next
                    newRow("Email") = email : newRow("DisplayName") = displayName : newRow("RecipientType") = recipientType : newRow("IsActive") = activeFlag
                    newRow("CreatedBy") = currentUser : newRow("CreatedAt") = nowText : newRow("UpdatedBy") = currentUser : newRow("UpdatedAt") = nowText
                    rows.Add(newRow)
                Else
                    existing("Email") = email : existing("DisplayName") = displayName : existing("RecipientType") = recipientType : existing("IsActive") = activeFlag
                    If GetValue(existing, "CreatedBy").Trim() = "" Then existing("CreatedBy") = currentUser
                    If GetValue(existing, "CreatedAt").Trim() = "" Then existing("CreatedAt") = nowText
                    existing("UpdatedBy") = currentUser : existing("UpdatedAt") = nowText
                End If
            End Sub)
        AuditService.Log("MECHANISM_QUALITY_MAIL_RECIPIENT_SAVE", "", "", "E-posta=" & email & "; Tür=" & recipientType)
    End Sub

    Public Shared Sub DeleteMechanismQualityEmailRecipient(email As String)
        AuthorizationService.Require(AppState.CanManageMechanismQualityEmailRecipients, "Mekanizma Kalite Kontrol Mail Alıcı Silme")
        Dim target = NormalizeEmailAddress(email)
        If target = "" Then Throw New ArgumentException("Silinecek e-posta adresi boş olamaz.")
        Dim deleted = CsvUtil.UpdateRowsLocked(AppPaths.MechanismQualityEmailRecipientsCsv, MechanismQualityEmailRecipientHeaders,
            Function(rows) rows.RemoveAll(Function(row) String.Equals(NormalizeEmailAddress(GetValue(row, "Email")), target, StringComparison.OrdinalIgnoreCase)) > 0)
        If Not deleted Then Throw New InvalidOperationException("Silinecek mail alıcısı bulunamadı.")
        AuditService.Log("MECHANISM_QUALITY_MAIL_RECIPIENT_DELETE", "", "", "E-posta=" & target)
    End Sub

    Public Shared Function GetPackageMeterEmailRecipients(Optional activeOnly As Boolean = False) As List(Of PlasticShiftEmailRecipient)
        AuthorizationService.Require(AppState.CanOpenPackageMeterControls, "Paket Sayaç Kontrol Mail Alıcıları")
        Return CsvUtil.ReadRows(AppPaths.PackageMeterEmailRecipientsCsv).
            Select(Function(row) New PlasticShiftEmailRecipient With {
                .Email = GetValue(row, "Email"), .DisplayName = GetValue(row, "DisplayName"),
                .RecipientType = NormalizePlasticShiftEmailRecipientType(GetValue(row, "RecipientType")),
                .IsActive = If(GetValue(row, "IsActive").Trim() = "", "YES", GetValue(row, "IsActive")),
                .CreatedBy = GetValue(row, "CreatedBy"), .CreatedAt = GetValue(row, "CreatedAt"),
                .UpdatedBy = GetValue(row, "UpdatedBy"), .UpdatedAt = GetValue(row, "UpdatedAt")} ).
            Where(Function(item) Not activeOnly OrElse String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase)).
            OrderBy(Function(item) If(String.Equals(item.RecipientType, "CC", StringComparison.OrdinalIgnoreCase), 1, 0)).
            ThenBy(Function(item) item.DisplayName).ThenBy(Function(item) item.Email).ToList()
    End Function

    Public Shared Sub SavePackageMeterEmailRecipient(originalEmail As String, recipient As PlasticShiftEmailRecipient)
        AuthorizationService.Require(AppState.CanManagePackageMeterEmailRecipients, "Paket Sayaç Kontrol Mail Alıcıları")
        If recipient Is Nothing Then Throw New ArgumentNullException(NameOf(recipient))
        Dim oldEmail = NormalizeEmailAddress(originalEmail)
        Dim email = NormalizeEmailAddress(recipient.Email)
        If email = "" Then Throw New ArgumentException("E-posta adresi boş olamaz.")
        ValidateEmailAddress(email)
        Dim displayName = CleanTestRequestSingleLine(recipient.DisplayName, 150)
        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        Dim currentUser = If(AppState.CurrentUserName, "").Trim()
        Dim recipientType = NormalizePlasticShiftEmailRecipientType(recipient.RecipientType)
        Dim activeFlag = NormalizeYesNoFlag(recipient.IsActive)
        CsvUtil.UpdateRowsLocked(AppPaths.PackageMeterEmailRecipientsCsv, PackageMeterEmailRecipientHeaders,
            Sub(rows)
                Dim existing = rows.FirstOrDefault(Function(row)
                    Dim rowEmail = NormalizeEmailAddress(GetValue(row, "Email"))
                    Return (oldEmail <> "" AndAlso String.Equals(rowEmail, oldEmail, StringComparison.OrdinalIgnoreCase)) OrElse String.Equals(rowEmail, email, StringComparison.OrdinalIgnoreCase)
                End Function)
                If existing Is Nothing Then
                    Dim newRow As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                    For Each header In PackageMeterEmailRecipientHeaders : newRow(header) = "" : Next
                    newRow("Email") = email : newRow("DisplayName") = displayName : newRow("RecipientType") = recipientType : newRow("IsActive") = activeFlag
                    newRow("CreatedBy") = currentUser : newRow("CreatedAt") = nowText : newRow("UpdatedBy") = currentUser : newRow("UpdatedAt") = nowText
                    rows.Add(newRow)
                Else
                    existing("Email") = email : existing("DisplayName") = displayName : existing("RecipientType") = recipientType : existing("IsActive") = activeFlag
                    If GetValue(existing, "CreatedBy").Trim() = "" Then existing("CreatedBy") = currentUser
                    If GetValue(existing, "CreatedAt").Trim() = "" Then existing("CreatedAt") = nowText
                    existing("UpdatedBy") = currentUser : existing("UpdatedAt") = nowText
                End If
            End Sub)
        AuditService.Log("PACKAGE_METER_MAIL_RECIPIENT_SAVE", "", "", "E-posta=" & email & "; Tür=" & recipientType)
    End Sub

    Public Shared Sub DeletePackageMeterEmailRecipient(email As String)
        AuthorizationService.Require(AppState.CanManagePackageMeterEmailRecipients, "Paket Sayaç Kontrol Mail Alıcısı Silme")
        Dim target = NormalizeEmailAddress(email)
        If target = "" Then Throw New ArgumentException("Silinecek e-posta adresi boş olamaz.")
        Dim deleted = CsvUtil.UpdateRowsLocked(AppPaths.PackageMeterEmailRecipientsCsv, PackageMeterEmailRecipientHeaders,
            Function(rows) rows.RemoveAll(Function(row) String.Equals(NormalizeEmailAddress(GetValue(row, "Email")), target, StringComparison.OrdinalIgnoreCase)) > 0)
        If Not deleted Then Throw New InvalidOperationException("Silinecek mail alıcısı bulunamadı.")
        AuditService.Log("PACKAGE_METER_MAIL_RECIPIENT_DELETE", "", "", "E-posta=" & target)
    End Sub

    Public Shared Function GetTestRequestEmailRecipients(Optional activeOnly As Boolean = False) As List(Of TestRequestEmailRecipient)
        AuthorizationService.Require(AppState.CanOpenTestRequests, "Test Talep Mail Alıcıları")

        Return CsvUtil.ReadRows(AppPaths.TestRequestEmailRecipientsCsv).
            Select(Function(row) New TestRequestEmailRecipient With {
                .EventType = TestRequestEmailNotificationService.NormalizeEventType(GetValue(row, "EventType")),
                .RequestingDepartment = TestRequestEmailNotificationService.NormalizeDepartment(GetValue(row, "RequestingDepartment")),
                .Email = GetValue(row, "Email"),
                .DisplayName = GetValue(row, "DisplayName"),
                .RecipientType = NormalizePlasticShiftEmailRecipientType(GetValue(row, "RecipientType")),
                .IsActive = If(GetValue(row, "IsActive").Trim() = "", "YES", GetValue(row, "IsActive")),
                .CreatedBy = GetValue(row, "CreatedBy"),
                .CreatedAt = GetValue(row, "CreatedAt"),
                .UpdatedBy = GetValue(row, "UpdatedBy"),
                .UpdatedAt = GetValue(row, "UpdatedAt")
            }).
            Where(Function(item) Not activeOnly OrElse String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase)).
            OrderBy(Function(item) item.EventType).
            ThenBy(Function(item) item.RequestingDepartment).
            ThenBy(Function(item) item.DisplayName).
            ThenBy(Function(item) item.Email).
            ToList()
    End Function

    Public Shared Sub SaveTestRequestEmailRecipient(originalEmail As String,
                                                     recipient As TestRequestEmailRecipient,
                                                     Optional originalEventType As String = "",
                                                     Optional originalDepartment As String = "")
        AuthorizationService.Require(AppState.CanManageTestRequestEmailRecipients, "Test Talep Mail Alıcıları")
        If recipient Is Nothing Then Throw New ArgumentNullException(NameOf(recipient))

        Dim oldEmail = NormalizeEmailAddress(originalEmail)
        Dim oldEventType = If(String.IsNullOrWhiteSpace(originalEventType), "", TestRequestEmailNotificationService.NormalizeEventType(originalEventType))
        Dim oldDepartment = If(String.IsNullOrWhiteSpace(originalDepartment), "", TestRequestEmailNotificationService.NormalizeDepartment(originalDepartment))
        Dim email = NormalizeEmailAddress(recipient.Email)
        Dim eventType = TestRequestEmailNotificationService.NormalizeEventType(recipient.EventType)
        Dim department = TestRequestEmailNotificationService.NormalizeDepartment(recipient.RequestingDepartment)
        If email = "" Then Throw New ArgumentException("E-posta adresi boş olamaz.")
        ValidateEmailAddress(email)

        Dim displayName = CleanTestRequestSingleLine(recipient.DisplayName, 150)
        Dim recipientType = NormalizePlasticShiftEmailRecipientType(recipient.RecipientType)
        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        Dim currentUser = If(AppState.CurrentUserName, "").Trim()
        Dim activeFlag = NormalizeYesNoFlag(recipient.IsActive)

        CsvUtil.UpdateRowsLocked(
            AppPaths.TestRequestEmailRecipientsCsv,
            TestRequestEmailRecipientHeaders,
            Sub(rows)
                Dim existing = rows.FirstOrDefault(
                    Function(row)
                        Dim rowEmail = NormalizeEmailAddress(GetValue(row, "Email"))
                        Dim rowEventType = TestRequestEmailNotificationService.NormalizeEventType(GetValue(row, "EventType"))
                        Dim rowDepartment = TestRequestEmailNotificationService.NormalizeDepartment(GetValue(row, "RequestingDepartment"))
                        Dim matchesOriginal = oldEmail <> "" AndAlso
                                              String.Equals(rowEmail, oldEmail, StringComparison.OrdinalIgnoreCase) AndAlso
                                              (oldEventType = "" OrElse String.Equals(rowEventType, oldEventType, StringComparison.OrdinalIgnoreCase)) AndAlso
                                              (oldDepartment = "" OrElse String.Equals(rowDepartment, oldDepartment, StringComparison.OrdinalIgnoreCase))
                        Dim matchesNew = String.Equals(rowEmail, email, StringComparison.OrdinalIgnoreCase) AndAlso
                                         String.Equals(rowEventType, eventType, StringComparison.OrdinalIgnoreCase) AndAlso
                                         String.Equals(rowDepartment, department, StringComparison.OrdinalIgnoreCase)
                        Return matchesOriginal OrElse matchesNew
                    End Function)

                If existing Is Nothing Then
                    Dim newRow As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                    For Each header In TestRequestEmailRecipientHeaders
                        newRow(header) = ""
                    Next
                    newRow("EventType") = eventType
                    newRow("RequestingDepartment") = department
                    newRow("Email") = email
                    newRow("DisplayName") = displayName
                    newRow("RecipientType") = recipientType
                    newRow("IsActive") = activeFlag
                    newRow("CreatedBy") = currentUser
                    newRow("CreatedAt") = nowText
                    newRow("UpdatedBy") = currentUser
                    newRow("UpdatedAt") = nowText
                    rows.Add(newRow)
                Else
                    existing("EventType") = eventType
                    existing("RequestingDepartment") = department
                    existing("Email") = email
                    existing("DisplayName") = displayName
                    existing("RecipientType") = recipientType
                    existing("IsActive") = activeFlag
                    If GetValue(existing, "CreatedBy").Trim() = "" Then existing("CreatedBy") = currentUser
                    If GetValue(existing, "CreatedAt").Trim() = "" Then existing("CreatedAt") = nowText
                    existing("UpdatedBy") = currentUser
                    existing("UpdatedAt") = nowText
                End If
            End Sub)

        AuditService.Log("TEST_REQUEST_MAIL_RECIPIENT_SAVE", "", "", "Olay=" & eventType & "; Bölüm=" & department & "; Tür=" & recipientType & "; E-posta=" & email)
    End Sub

    Public Shared Sub DeleteTestRequestEmailRecipient(email As String,
                                                       Optional eventType As String = "",
                                                       Optional requestingDepartment As String = "")
        AuthorizationService.Require(AppState.CanManageTestRequestEmailRecipients, "Test Talep Mail Alıcı Silme")
        Dim target = NormalizeEmailAddress(email)
        Dim targetEventType = If(String.IsNullOrWhiteSpace(eventType), "", TestRequestEmailNotificationService.NormalizeEventType(eventType))
        Dim targetDepartment = If(String.IsNullOrWhiteSpace(requestingDepartment), "", TestRequestEmailNotificationService.NormalizeDepartment(requestingDepartment))
        If target = "" Then Throw New ArgumentException("Silinecek e-posta adresi boş olamaz.")

        Dim deleted As Boolean = CsvUtil.UpdateRowsLocked(
            AppPaths.TestRequestEmailRecipientsCsv,
            TestRequestEmailRecipientHeaders,
            Function(rows)
                Return rows.RemoveAll(
                    Function(row)
                        If Not String.Equals(NormalizeEmailAddress(GetValue(row, "Email")), target, StringComparison.OrdinalIgnoreCase) Then Return False
                        If targetEventType = "" Then Return True
                        If Not String.Equals(TestRequestEmailNotificationService.NormalizeEventType(GetValue(row, "EventType")), targetEventType, StringComparison.OrdinalIgnoreCase) Then Return False
                        If targetDepartment = "" Then Return True
                        Return String.Equals(TestRequestEmailNotificationService.NormalizeDepartment(GetValue(row, "RequestingDepartment")), targetDepartment, StringComparison.OrdinalIgnoreCase)
                    End Function) > 0
            End Function)

        If Not deleted Then Throw New InvalidOperationException("Silinecek mail alıcısı bulunamadı.")
        AuditService.Log("TEST_REQUEST_MAIL_RECIPIENT_DELETE", "", "", "Olay=" & targetEventType & "; Bölüm=" & targetDepartment & "; E-posta=" & target)
    End Sub

    Private Shared Function NormalizeEmailAddress(value As String) As String
        Return If(value, "").Trim().Replace(" "c, "")
    End Function

    Private Shared Function NormalizePlasticShiftEmailRecipientType(value As String) As String
        Dim normalized = If(value, "").Trim().ToUpperInvariant()
        If normalized = "CC" OrElse normalized = "BCC" Then Return "CC"
        Return "Kime"
    End Function

    Private Shared Sub ValidateEmailAddress(email As String)
        Try
            Dim address As New System.Net.Mail.MailAddress(email)
            If Not String.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase) Then
                Throw New FormatException()
            End If
        Catch
            Throw New ArgumentException("Geçerli bir e-posta adresi girilmelidir.")
        End Try
    End Sub

    Public Shared Function GetPackageMeterControls() As List(Of Dictionary(Of String, String))
        AuthorizationService.Require(AppState.CanOpenPackageMeterControls, "Paket Sayaç Kontrolleri")
        Return CsvUtil.ReadRows(AppPaths.PackageMeterControlsCsv)
    End Function

    Public Shared Function GetPackageMeterControlLines(controlId As String) As List(Of PackageMeterControlLine)
        AuthorizationService.Require(AppState.CanOpenPackageMeterControls, "Paket Sayaç Kontrol Detayı")
        Dim targetId = CleanPackageMeterSingleLine(controlId, 100)
        If targetId = "" Then Return New List(Of PackageMeterControlLine)()

        Return CsvUtil.ReadRows(AppPaths.PackageMeterControlLinesCsv).
            Where(Function(row) String.Equals(GetValue(row, "ControlId"), targetId, StringComparison.OrdinalIgnoreCase)).
            Select(Function(row) PackageMeterControlLineFromRow(row)).
            OrderBy(Function(line) line.SortNo).
            ThenBy(Function(line) line.SerialNumber).
            ToList()
    End Function

    Public Shared Function GetAllPackageMeterControlLines() As List(Of PackageMeterControlLine)
        AuthorizationService.Require(AppState.CanOpenPackageMeterControls, "Paket Sayaç Kontrol Satırları")
        Return CsvUtil.ReadRows(AppPaths.PackageMeterControlLinesCsv).
            Select(Function(row) PackageMeterControlLineFromRow(row)).
            OrderBy(Function(line) line.ControlId).
            ThenBy(Function(line) line.SortNo).
            ToList()
    End Function

    Public Shared Function SavePackageMeterControl(headerRow As Dictionary(Of String, String),
                                                    sourceLines As IEnumerable(Of PackageMeterControlLine),
                                                    completeRecord As Boolean) As String
        AuthorizationService.Require(AppState.CanModifyPackageMeterControls, "Paket Sayaç Kontrol Kaydı")
        If headerRow Is Nothing Then Throw New ArgumentNullException(NameOf(headerRow))

        Dim requestedId = CleanPackageMeterSingleLine(GetValue(headerRow, "ControlId"), 100)
        Dim controlId = requestedId
        If controlId = "" Then
            controlId = "PSK-" & DateTime.Now.ToString("yyyyMMdd-HHmmss") & "-" & Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant()
        End If

        Dim meterModel = CleanPackageMeterSingleLine(GetValue(headerRow, "MeterModel"), 200)
        Dim pulseCount = CleanPackageMeterSingleLine(GetValue(headerRow, "PulseCount"), 100)
        Dim customer = CleanPackageMeterSingleLine(GetValue(headerRow, "Customer"), 250)
        Dim operatorInfo = CleanPackageMeterSingleLine(GetValue(headerRow, "OperatorInfo"), 200)
        Dim controllerName = CleanPackageMeterSingleLine(GetValue(headerRow, "ControllerName"), 200)
        Dim productionPanelNo = CleanPackageMeterSingleLine(GetValue(headerRow, "ProductionPanelNo"), 100)
        Dim controlPanelNo = CleanPackageMeterSingleLine(GetValue(headerRow, "ControlPanelNo"), 100)
        Dim isSmartMeter = NormalizeYesNoFlag(GetValue(headerRow, "IsSmartMeter"))
        Dim referenceFlowQ4 = CleanPackageMeterSingleLine(GetValue(headerRow, "ReferenceFlowQ4"), 100)
        Dim referenceFlowQ3 = CleanPackageMeterSingleLine(GetValue(headerRow, "ReferenceFlowQ3"), 100)
        Dim referenceFlowQ2 = CleanPackageMeterSingleLine(GetValue(headerRow, "ReferenceFlowQ2"), 100)
        Dim referenceFlowQ1 = CleanPackageMeterSingleLine(GetValue(headerRow, "ReferenceFlowQ1"), 100)
        Dim rangeNumber As Decimal
        Dim expectedQ4 As Decimal
        Dim expectedQ2 As Decimal
        Dim rangeMatches As Boolean
        Dim q4Matches As Boolean
        Dim q2Matches As Boolean
        Dim referenceFlowsValid = EvaluatePackageMeterReferenceFlows(referenceFlowQ4,
                                                                      referenceFlowQ3,
                                                                      referenceFlowQ2,
                                                                      referenceFlowQ1,
                                                                      rangeNumber,
                                                                      expectedQ4,
                                                                      expectedQ2,
                                                                      rangeMatches,
                                                                      q4Matches,
                                                                      q2Matches)
        Dim referenceFlowsConsistent = referenceFlowsValid AndAlso rangeMatches AndAlso q4Matches AndAlso q2Matches
        Dim rangeValue = If(referenceFlowsValid, rangeNumber.ToString("0.##", CultureInfo.InvariantCulture), "")
        Dim explanation = If(GetValue(headerRow, "Explanation"), "").Trim()
        If explanation.Length > 4000 Then Throw New ArgumentException("Açıklama en fazla 4000 karakter olabilir.")

        Dim normalizedLines = NormalizePackageMeterLines(controlId,
                                                         sourceLines,
                                                         isSmartMeter = "YES",
                                                         referenceFlowQ4,
                                                         referenceFlowQ3,
                                                         referenceFlowQ2,
                                                         referenceFlowQ1)
        If normalizedLines.Count > 500 Then Throw New ArgumentException("Bir kontrol kaydında en fazla 500 sayaç satırı olabilir.")

        If completeRecord Then
            If meterModel = "" Then Throw New ArgumentException("Kontrol tamamlanmadan önce sayaç modeli girilmelidir.")
            If pulseCount = "" Then Throw New ArgumentException("Kontrol tamamlanmadan önce sayaç pals sayısı girilmelidir.")
            If customer = "" Then Throw New ArgumentException("Kontrol tamamlanmadan önce müşteri bilgisi girilmelidir.")
            If operatorInfo = "" Then Throw New ArgumentException("Kontrol tamamlanmadan önce operatör bilgisi girilmelidir.")
            If controllerName = "" Then Throw New ArgumentException("Kontrol tamamlanmadan önce kontrol eden kişi girilmelidir.")
            If productionPanelNo = "" Then Throw New ArgumentException("Kontrol tamamlanmadan önce üretim pano numarası girilmelidir.")
            If controlPanelNo = "" Then Throw New ArgumentException("Kontrol tamamlanmadan önce kontrol pano numarası girilmelidir.")
            If Not referenceFlowsValid Then
                Throw New ArgumentException("Kontrol tamamlanmadan önce Q4, Q3, Q2 ve Q1 referans debileri pozitif sayısal değerler olarak girilmelidir.")
            End If
            If Not rangeMatches Then
                Throw New ArgumentException("R değeri şu standart değerlerden biri olmalıdır: " & PackageMeterAllowedRangeDisplay & ".")
            End If
            If Not referenceFlowsConsistent Then
                Throw New ArgumentException("Referans debileri birbiriyle uyumlu değil. Q4 yaklaşık " &
                                            expectedQ4.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR")) &
                                            " ve Q2 yaklaşık " &
                                            expectedQ2.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR")) &
                                            " olmalıdır.")
            End If
            If normalizedLines.Count = 0 Then Throw New ArgumentException("Kontrol tamamlanmadan önce en az bir sayaç satırı eklenmelidir.")
            If normalizedLines.Any(Function(line) String.IsNullOrWhiteSpace(line.SerialNumber)) Then
                Throw New ArgumentException("Kontrol tamamlanmadan önce her satırın seri numarası girilmelidir.")
            End If
            Dim duplicateSerial = normalizedLines.
                Where(Function(line) Not String.IsNullOrWhiteSpace(line.SerialNumber)).
                GroupBy(Function(line) line.SerialNumber.Trim(), StringComparer.OrdinalIgnoreCase).
                FirstOrDefault(Function(group) group.Count() > 1)
            If duplicateSerial IsNot Nothing Then
                Throw New ArgumentException("Aynı seri numarası birden fazla satırda kullanılamaz: " & duplicateSerial.Key)
            End If
            If isSmartMeter = "YES" AndAlso
               normalizedLines.Any(Function(line) line.CreditResult = "" OrElse line.ValveResult = "") Then
                Throw New ArgumentException("Akıllı sayaçlarda her satır için kredi ve vana testi sonucu seçilmelidir.")
            End If
            Dim unresolvedLine = normalizedLines.FirstOrDefault(
                Function(line) line.OverallResult <> "UYGUN" AndAlso line.OverallResult <> "UYGUN DEĞİL")
            If unresolvedLine IsNot Nothing Then
                Dim serialInfo = If(String.IsNullOrWhiteSpace(unresolvedLine.SerialNumber),
                                    unresolvedLine.SortNo.ToString() & ". satır",
                                    "Seri " & unresolvedLine.SerialNumber)
                Throw New ArgumentException(serialInfo & " için eksik veya geçersiz kontrol bilgisi var. Tüm bilgiler dolmadan kontrol tamamlanamaz.")
            End If
        End If

        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        Dim currentUser = If(AppState.CurrentUserName, "").Trim()
        Dim successfulCount = normalizedLines.Where(Function(line) line.OverallResult = "UYGUN" OrElse line.OverallResult = "KONTROL EDİLDİ").Count()
        Dim unsuitableCount = normalizedLines.Where(Function(line) line.OverallResult = "UYGUN DEĞİL").Count()
        Dim incompleteCount = normalizedLines.Where(
            Function(line) line.OverallResult <> "UYGUN" AndAlso line.OverallResult <> "UYGUN DEĞİL").Count()

        CsvUtil.UpdateRowsLocked(
            AppPaths.PackageMeterControlsCsv,
            PackageMeterControlHeaders,
            Sub(rows)
                Dim existing = rows.FirstOrDefault(Function(row) String.Equals(GetValue(row, "ControlId"), controlId, StringComparison.OrdinalIgnoreCase))
                Dim existingWasCompleted = existing IsNot Nothing AndAlso
                    String.Equals(GetValue(existing, "Status"), "COMPLETED", StringComparison.OrdinalIgnoreCase)
                If existingWasCompleted Then
                    If Not AppState.IsAdmin Then
                        Throw New InvalidOperationException("Tamamlanmış paket sayaç kontrol kaydı yalnızca Admin tarafından değiştirilebilir.")
                    End If
                    completeRecord = True
                End If

                If existing Is Nothing Then
                    existing = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                    For Each header In PackageMeterControlHeaders
                        existing(header) = ""
                    Next
                    existing("ControlId") = controlId
                    existing("CreatedAt") = nowText
                    existing("CreatedBy") = currentUser
                    existing("CreatedComputerName") = Environment.MachineName
                    rows.Add(existing)
                End If

                Dim previousCompletedAt = GetValue(existing, "CompletedAt")
                Dim previousCompletedBy = GetValue(existing, "CompletedBy")

                existing("Status") = If(completeRecord, "COMPLETED", "DRAFT")
                existing("MeterModel") = meterModel
                existing("PulseCount") = pulseCount
                existing("Customer") = customer
                If String.IsNullOrWhiteSpace(GetValue(existing, "ControlDate")) Then existing("ControlDate") = nowText
                existing("OperatorInfo") = operatorInfo
                existing("ControllerName") = controllerName
                existing("ProductionPanelNo") = productionPanelNo
                existing("ControlPanelNo") = controlPanelNo
                existing("IsSmartMeter") = isSmartMeter
                existing("ReferenceFlowQ4") = referenceFlowQ4
                existing("ReferenceFlowQ3") = referenceFlowQ3
                existing("ReferenceFlowQ2") = referenceFlowQ2
                existing("ReferenceFlowQ1") = referenceFlowQ1
                existing("RangeValue") = rangeValue
                existing("Explanation") = explanation
                existing("MeterCount") = normalizedLines.Count.ToString()
                existing("SuitableCount") = successfulCount.ToString()
                existing("UnsuitableCount") = unsuitableCount.ToString()
                existing("IncompleteCount") = incompleteCount.ToString()
                existing("CompletedAt") = If(completeRecord, If(previousCompletedAt <> "", previousCompletedAt, nowText), "")
                existing("CompletedBy") = If(completeRecord, If(previousCompletedBy <> "", previousCompletedBy, currentUser), "")
                existing("UpdatedAt") = nowText
                existing("UpdatedBy") = currentUser
            End Sub)

        CsvUtil.UpdateRowsLocked(
            AppPaths.PackageMeterControlLinesCsv,
            PackageMeterControlLineHeaders,
            Sub(rows)
                Dim previous = rows.
                    Where(Function(row) String.Equals(GetValue(row, "ControlId"), controlId, StringComparison.OrdinalIgnoreCase)).
                    ToDictionary(Function(row) GetValue(row, "LineId"), StringComparer.OrdinalIgnoreCase)
                For Each row In rows.
                    Where(Function(item) String.Equals(GetValue(item, "ControlId"), controlId, StringComparison.OrdinalIgnoreCase)).
                    ToList()
                    rows.Remove(row)
                Next

                For Each line In normalizedLines
                    Dim oldRow As Dictionary(Of String, String) = Nothing
                    If previous.TryGetValue(line.LineId, oldRow) Then
                        line.CreatedAt = GetValue(oldRow, "CreatedAt")
                        line.CreatedBy = GetValue(oldRow, "CreatedBy")
                    End If
                    If line.CreatedAt = "" Then line.CreatedAt = nowText
                    If line.CreatedBy = "" Then line.CreatedBy = currentUser
                    line.UpdatedAt = nowText
                    line.UpdatedBy = currentUser
                    rows.Add(PackageMeterControlLineToRow(line))
                Next
            End Sub)

        AuditService.Log(If(completeRecord, "PACKAGE_METER_CONTROL_COMPLETE", "PACKAGE_METER_CONTROL_SAVE"),
                         "", "", "Kontrol No=" & controlId & "; Sayaç=" & normalizedLines.Count.ToString() & "; Model=" & meterModel)
        Return controlId
    End Function

    Public Shared Sub DeletePackageMeterControl(controlId As String)
        AuthorizationService.Require(AppState.CanDeletePackageMeterControls, "Paket Sayaç Kontrol Silme")
        Dim targetId = CleanPackageMeterSingleLine(controlId, 100)
        If targetId = "" Then Throw New ArgumentException("Silinecek kontrol kaydı seçilmelidir.")

        Dim deleted As Boolean = False
        CsvUtil.UpdateRowsLocked(
            AppPaths.PackageMeterControlsCsv,
            PackageMeterControlHeaders,
            Sub(rows)
                deleted = rows.RemoveAll(Function(row) String.Equals(GetValue(row, "ControlId"), targetId, StringComparison.OrdinalIgnoreCase)) > 0
            End Sub)
        If Not deleted Then Throw New InvalidOperationException("Silinecek paket sayaç kontrol kaydı bulunamadı.")

        CsvUtil.UpdateRowsLocked(
            AppPaths.PackageMeterControlLinesCsv,
            PackageMeterControlLineHeaders,
            Sub(rows)
                rows.RemoveAll(Function(row) String.Equals(GetValue(row, "ControlId"), targetId, StringComparison.OrdinalIgnoreCase))
            End Sub)
        AuditService.Log("PACKAGE_METER_CONTROL_DELETE", "", "", "Kontrol No=" & targetId)
    End Sub

    Private Shared Function NormalizePackageMeterLines(controlId As String,
                                                       sourceLines As IEnumerable(Of PackageMeterControlLine),
                                                       isSmartMeter As Boolean,
                                                       referenceFlowQ4 As String,
                                                       referenceFlowQ3 As String,
                                                       referenceFlowQ2 As String,
                                                       referenceFlowQ1 As String) As List(Of PackageMeterControlLine)
        Dim result As New List(Of PackageMeterControlLine)()
        Dim usedLineIds As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        If sourceLines Is Nothing Then Return result

        For Each source In sourceLines
            If source Is Nothing Then Continue For
            Dim hasContent = {source.SerialNumber, source.LabelErrorQ3, source.LabelErrorQ2, source.LabelErrorQ1,
                              source.TestFlowQ4Manual, source.TestFlowQ3, source.TestFlowQ2, source.TestFlowQ1,
                              source.CreditResult, source.ValveResult}.
                Any(Function(value) Not String.IsNullOrWhiteSpace(value))
            If Not hasContent Then Continue For

            Dim sortNo = result.Count + 1
            Dim lineId = CleanPackageMeterSingleLine(source.LineId, 150)
            If lineId = "" OrElse usedLineIds.Contains(lineId) Then
                lineId = controlId & "-L" & Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant()
            End If
            usedLineIds.Add(lineId)
            Dim line As New PackageMeterControlLine With {
                .ControlId = controlId,
                .LineId = lineId,
                .SortNo = sortNo,
                .SerialNumber = CleanPackageMeterSingleLine(source.SerialNumber, 150),
                .LabelErrorQ3 = CleanPackageMeterSingleLine(source.LabelErrorQ3, 100),
                .LabelErrorQ2 = CleanPackageMeterSingleLine(source.LabelErrorQ2, 100),
                .LabelErrorQ1 = CleanPackageMeterSingleLine(source.LabelErrorQ1, 100),
                .TestFlowQ4Manual = CleanPackageMeterSingleLine(source.TestFlowQ4Manual, 100),
                .TestFlowQ3 = CleanPackageMeterSingleLine(source.TestFlowQ3, 100),
                .TestFlowQ2 = CleanPackageMeterSingleLine(source.TestFlowQ2, 100),
                .TestFlowQ1 = CleanPackageMeterSingleLine(source.TestFlowQ1, 100),
                .CreditResult = If(isSmartMeter, NormalizePackageMeterResult(source.CreditResult), ""),
                .ValveResult = If(isSmartMeter, NormalizePackageMeterResult(source.ValveResult), "")
            }
            line.OverallResult = CalculatePackageMeterLineResult(line,
                                                                 isSmartMeter,
                                                                 referenceFlowQ4,
                                                                 referenceFlowQ3,
                                                                 referenceFlowQ2,
                                                                 referenceFlowQ1)
            result.Add(line)
        Next
        Return result
    End Function

    Private Shared Function PackageMeterControlLineFromRow(row As Dictionary(Of String, String)) As PackageMeterControlLine
        Return New PackageMeterControlLine With {
            .ControlId = GetValue(row, "ControlId"),
            .LineId = GetValue(row, "LineId"),
            .SortNo = ParseIntSafe(GetValue(row, "SortNo")),
            .SerialNumber = GetValue(row, "SerialNumber"),
            .LabelErrorQ3 = GetValue(row, "LabelErrorQ3"),
            .LabelErrorQ2 = GetValue(row, "LabelErrorQ2"),
            .LabelErrorQ1 = GetValue(row, "LabelErrorQ1"),
            .TestFlowQ4Manual = GetValue(row, "TestFlowQ4Manual"),
            .TestFlowQ3 = GetValue(row, "TestFlowQ3"),
            .TestFlowQ2 = GetValue(row, "TestFlowQ2"),
            .TestFlowQ1 = GetValue(row, "TestFlowQ1"),
            .CreditResult = GetValue(row, "CreditResult"),
            .ValveResult = GetValue(row, "ValveResult"),
            .OverallResult = GetValue(row, "OverallResult"),
            .CreatedAt = GetValue(row, "CreatedAt"),
            .CreatedBy = GetValue(row, "CreatedBy"),
            .UpdatedAt = GetValue(row, "UpdatedAt"),
            .UpdatedBy = GetValue(row, "UpdatedBy")
        }
    End Function

    Private Shared Function PackageMeterControlLineToRow(line As PackageMeterControlLine) As Dictionary(Of String, String)
        Dim row As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        For Each header In PackageMeterControlLineHeaders
            row(header) = ""
        Next
        row("ControlId") = line.ControlId
        row("LineId") = line.LineId
        row("SortNo") = line.SortNo.ToString()
        row("SerialNumber") = line.SerialNumber
        row("LabelErrorQ3") = line.LabelErrorQ3
        row("LabelErrorQ2") = line.LabelErrorQ2
        row("LabelErrorQ1") = line.LabelErrorQ1
        row("TestFlowQ4Manual") = line.TestFlowQ4Manual
        row("TestFlowQ3") = line.TestFlowQ3
        row("TestFlowQ2") = line.TestFlowQ2
        row("TestFlowQ1") = line.TestFlowQ1
        row("CreditResult") = line.CreditResult
        row("ValveResult") = line.ValveResult
        row("OverallResult") = line.OverallResult
        row("CreatedAt") = line.CreatedAt
        row("CreatedBy") = line.CreatedBy
        row("UpdatedAt") = line.UpdatedAt
        row("UpdatedBy") = line.UpdatedBy
        Return row
    End Function

    Private Shared Function CalculatePackageMeterLineResult(line As PackageMeterControlLine,
                                                            isSmartMeter As Boolean,
                                                            referenceFlowQ4 As String,
                                                            referenceFlowQ3 As String,
                                                            referenceFlowQ2 As String,
                                                            referenceFlowQ1 As String) As String
        Dim requiredValues = {line.SerialNumber, line.LabelErrorQ3, line.LabelErrorQ2, line.LabelErrorQ1,
                              line.TestFlowQ4Manual, line.TestFlowQ3, line.TestFlowQ2, line.TestFlowQ1}
        If requiredValues.Any(Function(value) String.IsNullOrWhiteSpace(value)) Then Return "EKSİK"

        Dim q3Error As Decimal
        Dim q2Error As Decimal
        Dim q1Error As Decimal
        If Not TryParsePackageMeterPercent(line.LabelErrorQ3, q3Error) OrElse
           Not TryParsePackageMeterPercent(line.LabelErrorQ2, q2Error) OrElse
           Not TryParsePackageMeterPercent(line.LabelErrorQ1, q1Error) Then
            Return "GEÇERSİZ DEĞER"
        End If

        Dim testQ4Value As Decimal
        Dim testQ3Value As Decimal
        Dim testQ2Value As Decimal
        Dim testQ1Value As Decimal
        If Not TryParsePackageMeterPercent(line.TestFlowQ4Manual, testQ4Value) OrElse
           Not TryParsePackageMeterPercent(line.TestFlowQ3, testQ3Value) OrElse
           Not TryParsePackageMeterPercent(line.TestFlowQ2, testQ2Value) OrElse
           Not TryParsePackageMeterPercent(line.TestFlowQ1, testQ1Value) Then
            Return "GEÇERSİZ DEĞER"
        End If

        Dim referenceValues = {referenceFlowQ4, referenceFlowQ3, referenceFlowQ2, referenceFlowQ1}
        If referenceValues.Any(Function(value) String.IsNullOrWhiteSpace(value)) Then Return "EKSİK"
        Dim rangeValue As Decimal
        Dim expectedQ4 As Decimal
        Dim expectedQ2 As Decimal
        Dim rangeMatches As Boolean
        Dim q4Matches As Boolean
        Dim q2Matches As Boolean
        If Not EvaluatePackageMeterReferenceFlows(referenceFlowQ4,
                                                   referenceFlowQ3,
                                                   referenceFlowQ2,
                                                   referenceFlowQ1,
                                                   rangeValue,
                                                   expectedQ4,
                                                   expectedQ2,
                                                   rangeMatches,
                                                   q4Matches,
                                                   q2Matches) Then Return "GEÇERSİZ DEĞER"
        If Not rangeMatches OrElse Not q4Matches OrElse Not q2Matches Then Return "GEÇERSİZ REFERANS"

        If Decimal.Abs(q3Error) > 2D OrElse Decimal.Abs(q2Error) > 2D OrElse Decimal.Abs(q1Error) > 5D OrElse
           Decimal.Abs(testQ4Value) > 2D OrElse Decimal.Abs(testQ3Value) > 2D OrElse
           Decimal.Abs(testQ2Value) > 2D OrElse Decimal.Abs(testQ1Value) > 5D Then
            Return "UYGUN DEĞİL"
        End If

        If isSmartMeter Then
            If line.CreditResult = "UYGUN DEĞİL" OrElse line.ValveResult = "UYGUN DEĞİL" Then Return "UYGUN DEĞİL"
            If line.CreditResult = "UYGUN" AndAlso line.ValveResult = "UYGUN" Then Return "UYGUN"
            Return "EKSİK"
        End If
        Return "UYGUN"
    End Function

    Public Shared Function EvaluatePackageMeterReferenceFlows(referenceFlowQ4 As String,
                                                               referenceFlowQ3 As String,
                                                               referenceFlowQ2 As String,
                                                               referenceFlowQ1 As String,
                                                               ByRef rangeValue As Decimal,
                                                               ByRef expectedQ4 As Decimal,
                                                               ByRef expectedQ2 As Decimal,
                                                               ByRef rangeMatches As Boolean,
                                                               ByRef q4Matches As Boolean,
                                                               ByRef q2Matches As Boolean) As Boolean
        rangeValue = 0D
        expectedQ4 = 0D
        expectedQ2 = 0D
        rangeMatches = False
        q4Matches = False
        q2Matches = False

        Dim q4 As Decimal
        Dim q3 As Decimal
        Dim q2 As Decimal
        Dim q1 As Decimal
        If Not TryParsePackageMeterPercent(referenceFlowQ4, q4) OrElse
           Not TryParsePackageMeterPercent(referenceFlowQ3, q3) OrElse
           Not TryParsePackageMeterPercent(referenceFlowQ2, q2) OrElse
           Not TryParsePackageMeterPercent(referenceFlowQ1, q1) OrElse
           q4 <= 0D OrElse q3 <= 0D OrElse q2 <= 0D OrElse q1 <= 0D Then Return False

        Dim rawRange = q3 / q1
        For Each allowedRange In PackageMeterAllowedRangeValues.OrderBy(Function(value) Decimal.Abs(value - rawRange))
            Dim expectedQ1 = q3 / allowedRange
            If PackageMeterReferenceValueMatches(referenceFlowQ1, q1, expectedQ1) Then
                rangeValue = allowedRange
                rangeMatches = True
                Exit For
            End If
        Next
        If Not rangeMatches Then rangeValue = rawRange

        expectedQ4 = q3 * 1.25D
        expectedQ2 = If(rangeMatches, q3 / rangeValue * 1.6D, q1 * 1.6D)
        q4Matches = PackageMeterReferenceValueMatches(referenceFlowQ4, q4, expectedQ4)
        q2Matches = PackageMeterReferenceValueMatches(referenceFlowQ2, q2, expectedQ2)
        Return True
    End Function

    Public Shared Function TryResolvePackageMeterRange(referenceFlowQ3 As String,
                                                       referenceFlowQ1 As String,
                                                       ByRef rangeValue As Decimal) As Boolean
        rangeValue = 0D
        Dim q3 As Decimal
        Dim q1 As Decimal
        If Not TryParsePackageMeterPercent(referenceFlowQ3, q3) OrElse
           Not TryParsePackageMeterPercent(referenceFlowQ1, q1) OrElse
           q3 <= 0D OrElse q1 <= 0D Then Return False

        Dim rawRange = q3 / q1
        For Each allowedRange In PackageMeterAllowedRangeValues.OrderBy(Function(value) Decimal.Abs(value - rawRange))
            If PackageMeterReferenceValueMatches(referenceFlowQ1, q1, q3 / allowedRange) Then
                rangeValue = allowedRange
                Return True
            End If
        Next
        rangeValue = rawRange
        Return False
    End Function

    Private Shared Function PackageMeterReferenceValueMatches(originalText As String,
                                                               actualValue As Decimal,
                                                               expectedValue As Decimal) As Boolean
        Dim normalized = If(originalText, "").Trim().Replace("%", "").Replace(" ", "").Replace(","c, "."c)
        Dim decimalIndex = normalized.IndexOf("."c)
        Dim decimalPlaces = If(decimalIndex < 0, 0, Math.Max(0, normalized.Length - decimalIndex - 1))
        Dim roundingTolerance = 0.5D
        For index As Integer = 1 To decimalPlaces
            roundingTolerance /= 10D
        Next
        Return Decimal.Abs(actualValue - expectedValue) <= roundingTolerance
    End Function

    Private Shared Function TryParsePackageMeterPercent(value As String, ByRef parsedValue As Decimal) As Boolean
        Dim text = If(value, "").Trim().Replace("%", "").Replace(" ", "")
        If text = "" Then Return False
        If Decimal.TryParse(text, NumberStyles.Float, CultureInfo.GetCultureInfo("tr-TR"), parsedValue) Then Return True
        Return Decimal.TryParse(text.Replace(","c, "."c), NumberStyles.Float, CultureInfo.InvariantCulture, parsedValue)
    End Function

    Private Shared Function NormalizePackageMeterResult(value As String) As String
        Dim normalized = If(value, "").Trim().ToUpperInvariant()
        If normalized = "UYGUN" Then Return "UYGUN"
        If normalized = "UYGUN DEĞİL" OrElse normalized = "UYGUN DEGIL" Then Return "UYGUN DEĞİL"
        Return ""
    End Function

    Private Shared Function CleanPackageMeterSingleLine(value As String, maxLength As Integer) As String
        Dim text = If(value, "").Replace(vbCr, " ").Replace(vbLf, " ").Trim()
        While text.Contains("  ")
            text = text.Replace("  ", " ")
        End While
        If text.Length > maxLength Then Throw New ArgumentException("Girilen alan en fazla " & maxLength.ToString() & " karakter olabilir.")
        Return text
    End Function

    Public Shared Function GetTestCatalog(Optional activeOnly As Boolean = False) As List(Of TestCatalogItem)
        Return CsvUtil.ReadRows(AppPaths.TestCatalogCsv).
            Select(Function(row) New TestCatalogItem With {
                .TestName = GetValue(row, "TestName"),
                .Description = GetValue(row, "Description"),
                .IsActive = If(GetValue(row, "IsActive").Trim() = "", "YES", GetValue(row, "IsActive")),
                .SortNo = If(GetValue(row, "SortNo").Trim() = "", "0", GetValue(row, "SortNo")),
                .CreatedBy = GetValue(row, "CreatedBy"),
                .CreatedAt = GetValue(row, "CreatedAt"),
                .UpdatedBy = GetValue(row, "UpdatedBy"),
                .UpdatedAt = GetValue(row, "UpdatedAt")
            }).
            Where(Function(item) Not activeOnly OrElse String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase)).
            OrderBy(Function(item) ParseIntSafe(item.SortNo)).
            ThenBy(Function(item) item.TestName).
            ToList()
    End Function

    Public Shared Sub SaveTestCatalogItem(originalTestName As String, item As TestCatalogItem)
        AuthorizationService.Require(AppState.IsAdmin, "Test Listesi Yönetimi")

        If item Is Nothing Then Throw New ArgumentNullException(NameOf(item))

        Dim oldName = CleanTestRequestSingleLine(originalTestName, 200)
        Dim testName = CleanTestRequestSingleLine(item.TestName, 200)
        If testName = "" Then Throw New ArgumentException("Test adı boş olamaz.")

        Dim description = If(item.Description, "").Trim()
        If description.Length > 1000 Then Throw New ArgumentException("Test açıklaması en fazla 1000 karakter olabilir.")

        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        Dim currentUser = If(AppState.CurrentUserName, "").Trim()
        Dim activeFlag = NormalizeYesNoFlag(item.IsActive)
        Dim sortNo = Math.Max(0, ParseIntSafe(item.SortNo)).ToString()

        CsvUtil.UpdateRowsLocked(
            AppPaths.TestCatalogCsv,
            TestCatalogHeaders,
            Sub(rows)
                Dim existing = rows.FirstOrDefault(
                    Function(row) String.Equals(GetValue(row, "TestName"), If(oldName = "", testName, oldName), StringComparison.OrdinalIgnoreCase))

                Dim duplicate = rows.FirstOrDefault(
                    Function(row) Not Object.ReferenceEquals(row, existing) AndAlso
                                  String.Equals(GetValue(row, "TestName"), testName, StringComparison.OrdinalIgnoreCase))
                If duplicate IsNot Nothing Then
                    Throw New InvalidOperationException("Bu test adı zaten listede var: " & testName)
                End If

                If existing Is Nothing Then
                    existing = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                    For Each header In TestCatalogHeaders
                        existing(header) = ""
                    Next
                    existing("CreatedBy") = currentUser
                    existing("CreatedAt") = nowText
                    rows.Add(existing)
                End If

                existing("TestName") = testName
                existing("Description") = description
                existing("IsActive") = activeFlag
                existing("SortNo") = sortNo
                existing("UpdatedBy") = currentUser
                existing("UpdatedAt") = nowText
            End Sub)
    End Sub

    Public Shared Sub DeleteTestCatalogItem(testName As String)
        AuthorizationService.Require(AppState.IsAdmin, "Test Listesi Yönetimi")

        Dim target = CleanTestRequestSingleLine(testName, 200)
        If target = "" Then Throw New ArgumentException("Silinecek test adı boş olamaz.")

        Dim removed As Boolean = False
        CsvUtil.UpdateRowsLocked(
            AppPaths.TestCatalogCsv,
            TestCatalogHeaders,
            Sub(rows)
                For Each row In rows.ToList()
                    If String.Equals(GetValue(row, "TestName"), target, StringComparison.OrdinalIgnoreCase) Then
                        rows.Remove(row)
                        removed = True
                    End If
                Next
            End Sub)

        If Not removed Then Throw New InvalidOperationException("Silinecek test bulunamadı: " & target)
    End Sub

    Public Shared Sub MoveTestCatalogItem(testName As String, direction As Integer)
        AuthorizationService.Require(AppState.IsAdmin, "Test Listesi Yönetimi")

        Dim target = CleanTestRequestSingleLine(testName, 200)
        If target = "" Then Throw New ArgumentException("Taşınacak test adı boş olamaz.")
        If direction = 0 Then Return

        Dim moved As Boolean = False
        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        Dim currentUser = If(AppState.CurrentUserName, "").Trim()

        CsvUtil.UpdateRowsLocked(
            AppPaths.TestCatalogCsv,
            TestCatalogHeaders,
            Sub(rows)
                Dim orderedRows = rows.
                    OrderBy(Function(row) ParseIntSafe(GetValue(row, "SortNo"))).
                    ThenBy(Function(row) GetValue(row, "TestName")).
                    ToList()

                Dim index = orderedRows.FindIndex(Function(row) String.Equals(GetValue(row, "TestName"), target, StringComparison.OrdinalIgnoreCase))
                If index < 0 Then Throw New InvalidOperationException("Taşınacak test bulunamadı: " & target)

                Dim newIndex = index + If(direction < 0, -1, 1)
                If newIndex < 0 Then Throw New InvalidOperationException("Seçili test zaten en üst sırada.")
                If newIndex >= orderedRows.Count Then Throw New InvalidOperationException("Seçili test zaten en alt sırada.")

                Dim rowToMove = orderedRows(index)
                orderedRows.RemoveAt(index)
                orderedRows.Insert(newIndex, rowToMove)

                For i As Integer = 0 To orderedRows.Count - 1
                    orderedRows(i)("SortNo") = (i + 1).ToString()
                    orderedRows(i)("UpdatedBy") = currentUser
                    orderedRows(i)("UpdatedAt") = nowText
                Next

                moved = True
            End Sub)

        If Not moved Then Throw New InvalidOperationException("Test sırası değiştirilemedi.")
    End Sub

    Public Shared Function GetTestGroups(Optional activeOnly As Boolean = False) As List(Of TestGroupItem)
        Return CsvUtil.ReadRows(AppPaths.TestGroupsCsv).
            Select(Function(row) New TestGroupItem With {
                .GroupName = GetValue(row, "GroupName"),
                .TestsText = GetValue(row, "TestsText"),
                .IsActive = If(GetValue(row, "IsActive").Trim() = "", "YES", GetValue(row, "IsActive")),
                .SortNo = If(GetValue(row, "SortNo").Trim() = "", "0", GetValue(row, "SortNo")),
                .CreatedBy = GetValue(row, "CreatedBy"),
                .CreatedAt = GetValue(row, "CreatedAt"),
                .UpdatedBy = GetValue(row, "UpdatedBy"),
                .UpdatedAt = GetValue(row, "UpdatedAt")
            }).
            Where(Function(item) Not activeOnly OrElse String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase)).
            OrderBy(Function(item) ParseIntSafe(item.SortNo)).
            ThenBy(Function(item) item.GroupName).
            ToList()
    End Function

    Public Shared Sub SaveTestGroup(originalGroupName As String, item As TestGroupItem)
        AuthorizationService.Require(AppState.IsAdmin, "Test Grubu Yönetimi")
        If item Is Nothing Then Throw New ArgumentNullException(NameOf(item))

        Dim oldName = CleanTestRequestSingleLine(originalGroupName, 200)
        Dim groupName = CleanTestRequestSingleLine(item.GroupName, 200)
        If groupName = "" Then Throw New ArgumentException("Grup adı boş olamaz.")

        Dim testsText = If(item.TestsText, "").Trim()
        If testsText = "" Then Throw New ArgumentException("Gruba en az bir test eklenmelidir.")
        If testsText.Length > 4000 Then Throw New ArgumentException("Test grubu içeriği en fazla 4000 karakter olabilir.")

        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        Dim currentUser = If(AppState.CurrentUserName, "").Trim()
        Dim activeFlag = NormalizeYesNoFlag(item.IsActive)
        Dim sortNo = Math.Max(0, ParseIntSafe(item.SortNo)).ToString()

        CsvUtil.UpdateRowsLocked(
            AppPaths.TestGroupsCsv,
            TestGroupHeaders,
            Sub(rows)
                Dim existing = rows.FirstOrDefault(
                    Function(row) String.Equals(GetValue(row, "GroupName"), If(oldName = "", groupName, oldName), StringComparison.OrdinalIgnoreCase))

                Dim duplicate = rows.FirstOrDefault(
                    Function(row) Not Object.ReferenceEquals(row, existing) AndAlso
                                  String.Equals(GetValue(row, "GroupName"), groupName, StringComparison.OrdinalIgnoreCase))
                If duplicate IsNot Nothing Then
                    Throw New InvalidOperationException("Bu test grubu zaten var: " & groupName)
                End If

                If existing Is Nothing Then
                    existing = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                    For Each header In TestGroupHeaders
                        existing(header) = ""
                    Next
                    existing("CreatedBy") = currentUser
                    existing("CreatedAt") = nowText
                    rows.Add(existing)
                End If

                existing("GroupName") = groupName
                existing("TestsText") = testsText
                existing("IsActive") = activeFlag
                existing("SortNo") = sortNo
                existing("UpdatedBy") = currentUser
                existing("UpdatedAt") = nowText
            End Sub)
    End Sub

    Public Shared Sub DeleteTestGroup(groupName As String)
        AuthorizationService.Require(AppState.IsAdmin, "Test Grubu Yönetimi")

        Dim target = CleanTestRequestSingleLine(groupName, 200)
        If target = "" Then Throw New ArgumentException("Silinecek grup adı boş olamaz.")

        Dim removed As Boolean = False
        CsvUtil.UpdateRowsLocked(
            AppPaths.TestGroupsCsv,
            TestGroupHeaders,
            Sub(rows)
                For Each row In rows.ToList()
                    If String.Equals(GetValue(row, "GroupName"), target, StringComparison.OrdinalIgnoreCase) Then
                        rows.Remove(row)
                        removed = True
                    End If
                Next
            End Sub)

        If Not removed Then Throw New InvalidOperationException("Silinecek test grubu bulunamadı: " & target)
    End Sub

    Public Shared Function GetMeasurementDevices() As List(Of Dictionary(Of String, String))
        AuthorizationService.Require(AppState.CanOpenMsaDashboard, "MSA Dashboard")

        Return CsvUtil.ReadRows(AppPaths.MeasurementDevicesCsv).
            Select(Function(row)
                       Dim copy As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                       For Each header In MeasurementDeviceHeaders
                           copy(header) = GetValue(row, header)
                       Next
                       If copy("DeviceName").Trim() = "" Then copy("DeviceName") = GetValue(row, "MeasurementDeviceDescription")
                       If copy("DeviceType").Trim() = "" Then copy("DeviceType") = GetValue(row, "Type")
                       If copy("Location").Trim() = "" Then copy("Location") = GetValue(row, "UsedDepartment")
                       If copy("CalibrationDate").Trim() = "" Then copy("CalibrationDate") = GetValue(row, "LastCalibrationDate")
                       If copy("CalibrationDueDate").Trim() = "" Then copy("CalibrationDueDate") = GetValue(row, "NextCalibrationDate")
                       If copy("RegistrationDate").Trim() = "" Then copy("RegistrationDate") = GetValue(row, "CreatedAt")
                       If copy("ReferenceDevice").Trim() = "" Then copy("ReferenceDevice") = "HAYIR"
                       If copy("UsageStatus").Trim() = "" Then copy("UsageStatus") = "KULLANIMDA"
                       If copy("Status").Trim() = "" Then copy("Status") = "AKTİF"
                       Return copy
                   End Function).
            OrderBy(Function(row) GetValue(row, "DeviceId")).
            ThenBy(Function(row) GetValue(row, "DeviceName")).
            ToList()
    End Function

    Public Shared Function SuggestMeasurementDeviceId(location As String, deviceType As String) As String
        AuthorizationService.Require(AppState.CanOpenMsaDashboard, "Ölçüm Cihazı Tanımı")
        Dim cleanLocation = CleanTestRequestSingleLine(location, 150)
        Dim cleanDeviceType = CleanTestRequestSingleLine(deviceType, 100)
        If cleanLocation = "" OrElse cleanDeviceType = "" Then Return ""
        Return BuildNextMeasurementDeviceId(GetMeasurementDevices(), cleanLocation, cleanDeviceType)
    End Function

    Public Shared Function SaveMeasurementDevice(originalDeviceId As String,
                                                inputRow As Dictionary(Of String, String),
                                                Optional useRequestedDeviceId As Boolean = False) As String
        AuthorizationService.Require(AppState.CanModifyMsaDashboard, "Ölçüm Cihazı Tanımı")
        If inputRow Is Nothing Then Throw New ArgumentNullException(NameOf(inputRow))

        Dim oldDeviceId = CleanTestRequestSingleLine(originalDeviceId, 100)
        Dim deviceId = CleanTestRequestSingleLine(GetValue(inputRow, "DeviceId"), 100)
        Dim deviceName = CleanTestRequestSingleLine(GetValue(inputRow, "DeviceName"), 200)
        If oldDeviceId <> "" AndAlso deviceId = "" Then Throw New ArgumentException("Cihaz no boş olamaz.")
        If deviceName = "" Then Throw New ArgumentException("Ölçüm cihazı tanımı boş olamaz.")

        Dim fixedAssetNo = CleanTestRequestSingleLine(GetValue(inputRow, "FixedAssetNo"), 100)
        Dim deviceType = CleanTestRequestSingleLine(GetValue(inputRow, "DeviceType"), 100)
        Dim serialNo = CleanTestRequestSingleLine(GetValue(inputRow, "SerialNo"), 100)
        Dim brand = CleanTestRequestSingleLine(GetValue(inputRow, "Brand"), 100)
        Dim model = CleanTestRequestSingleLine(GetValue(inputRow, "Model"), 100)
        Dim measurementRange = CleanTestRequestSingleLine(GetValue(inputRow, "MeasurementRange"), 100)
        Dim resolution = CleanTestRequestSingleLine(GetValue(inputRow, "Resolution"), 50)
        Dim unitText = CleanTestRequestSingleLine(GetValue(inputRow, "Unit"), 50)
        Dim referenceDevice = NormalizeYesNoFlag(GetValue(inputRow, "ReferenceDevice"))
        Dim usageStatus = NormalizeMeasurementDeviceUsageStatus(GetValue(inputRow, "UsageStatus"))
        Dim registrationDate = CleanTestRequestSingleLine(GetValue(inputRow, "RegistrationDate"), 30)
        Dim calibrationPeriodMonths = CleanTestRequestSingleLine(GetValue(inputRow, "CalibrationPeriodMonths"), 20)
        Dim calibrationDate = CleanTestRequestSingleLine(GetValue(inputRow, "CalibrationDate"), 20)
        Dim calibrationDueDate = CleanTestRequestSingleLine(GetValue(inputRow, "CalibrationDueDate"), 20)
        Dim status = NormalizeMeasurementDeviceStatus(GetValue(inputRow, "Status"))
        Dim location = CleanTestRequestSingleLine(GetValue(inputRow, "Location"), 150)
        If oldDeviceId = "" AndAlso location = "" Then
            Throw New ArgumentException("Otomatik cihaz numarası için 'Kullanıldığı Bölüm' alanı boş olamaz.")
        End If
        If oldDeviceId = "" AndAlso deviceType = "" Then
            Throw New ArgumentException("Otomatik cihaz numarası için cihaz tipi boş olamaz.")
        End If
        Dim organization = CleanTestRequestSingleLine(GetValue(inputRow, "Organization"), 150)
        Dim responsible = CleanTestRequestSingleLine(GetValue(inputRow, "Responsible"), 150)
        Dim note = If(GetValue(inputRow, "Note"), "").Trim()
        If note.Length > 1000 Then Throw New ArgumentException("Not alanı en fazla 1000 karakter olabilir.")
        Dim stdIso9001 = NormalizeYesNoFlag(GetValue(inputRow, "StdIso9001"))
        Dim stdIso45001 = NormalizeYesNoFlag(GetValue(inputRow, "StdIso45001"))
        Dim stdIso50001 = NormalizeYesNoFlag(GetValue(inputRow, "StdIso50001"))
        Dim stdIso46001 = NormalizeYesNoFlag(GetValue(inputRow, "StdIso46001"))
        Dim stdIso17020 = NormalizeYesNoFlag(GetValue(inputRow, "StdIso17020"))
        Dim stdIso17025 = NormalizeYesNoFlag(GetValue(inputRow, "StdIso17025"))

        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        Dim currentUser = If(AppState.CurrentUserName, "").Trim()

        CsvUtil.UpdateRowsLocked(
            AppPaths.MeasurementDevicesCsv,
            MeasurementDeviceHeaders,
            Sub(rows)
                Dim existing As Dictionary(Of String, String) = Nothing
                Dim duplicate As Dictionary(Of String, String) = Nothing

                If oldDeviceId = "" Then
                    If useRequestedDeviceId AndAlso AppState.IsAdmin Then
                        If deviceId = "" Then Throw New ArgumentException("Cihaz no boş olamaz.")
                        duplicate = rows.FirstOrDefault(
                            Function(row) String.Equals(GetValue(row, "DeviceId"), deviceId, StringComparison.OrdinalIgnoreCase))
                        If duplicate IsNot Nothing Then
                            Throw New InvalidOperationException("Bu cihaz no zaten kayıtlı: " & deviceId)
                        End If
                    Else
                        deviceId = BuildNextMeasurementDeviceId(rows, location, deviceType)
                    End If
                Else
                    existing = rows.FirstOrDefault(
                        Function(row) String.Equals(GetValue(row, "DeviceId"), oldDeviceId, StringComparison.OrdinalIgnoreCase))
                    If existing Is Nothing Then
                        Throw New InvalidOperationException("Güncellenecek cihaz bulunamadı: " & oldDeviceId)
                    End If

                    duplicate = rows.FirstOrDefault(
                        Function(row) Not Object.ReferenceEquals(row, existing) AndAlso
                                      String.Equals(GetValue(row, "DeviceId"), deviceId, StringComparison.OrdinalIgnoreCase))
                    If duplicate IsNot Nothing Then
                        Throw New InvalidOperationException("Bu cihaz no zaten kayıtlı: " & deviceId)
                    End If
                End If

                If existing Is Nothing Then
                    existing = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                    For Each header In MeasurementDeviceHeaders
                        existing(header) = ""
                    Next
                    existing("CreatedBy") = currentUser
                    existing("CreatedAt") = nowText
                    existing("RegistrationDate") = If(registrationDate = "", nowText, registrationDate)
                    rows.Add(existing)
                End If

                existing("DeviceId") = deviceId
                existing("FixedAssetNo") = fixedAssetNo
                existing("StdIso9001") = stdIso9001
                existing("StdIso45001") = stdIso45001
                existing("StdIso50001") = stdIso50001
                existing("StdIso46001") = stdIso46001
                existing("StdIso17020") = stdIso17020
                existing("StdIso17025") = stdIso17025
                existing("DeviceName") = deviceName
                existing("SerialNo") = serialNo
                existing("Brand") = brand
                existing("Model") = model
                existing("DeviceType") = deviceType
                existing("MeasurementRange") = measurementRange
                existing("Resolution") = resolution
                existing("Unit") = unitText
                existing("ReferenceDevice") = referenceDevice
                existing("UsageStatus") = usageStatus
                If registrationDate <> "" OrElse existing("RegistrationDate").Trim() = "" Then
                    existing("RegistrationDate") = If(registrationDate = "", GetValue(existing, "CreatedAt"), registrationDate)
                End If
                existing("CalibrationPeriodMonths") = calibrationPeriodMonths
                existing("CalibrationDate") = calibrationDate
                existing("CalibrationDueDate") = calibrationDueDate
                existing("Status") = status
                existing("Location") = location
                existing("Organization") = organization
                existing("Responsible") = responsible
                existing("Note") = note
                existing("UpdatedBy") = currentUser
                existing("UpdatedAt") = nowText
            End Sub)
        Return deviceId
    End Function

    Private Shared Function BuildNextMeasurementDeviceId(rows As IEnumerable(Of Dictionary(Of String, String)),
                                                         location As String,
                                                         deviceType As String) As String
        Dim prefix = BuildMeasurementDeviceCodePart(location, True) & "-" &
                     BuildMeasurementDeviceCodePart(deviceType, False)
        Dim pattern = "^" & Regex.Escape(prefix) & "-(\d+)$"
        Dim maximum As Integer = 0

        For Each row In If(rows, Enumerable.Empty(Of Dictionary(Of String, String))())
            Dim match = Regex.Match(GetValue(row, "DeviceId").Trim(), pattern, RegexOptions.IgnoreCase)
            If Not match.Success Then Continue For

            Dim sequence As Integer
            If Integer.TryParse(match.Groups(1).Value, sequence) AndAlso sequence > maximum Then
                maximum = sequence
            End If
        Next

        Return prefix & "-" & (maximum + 1).ToString("000", CultureInfo.InvariantCulture)
    End Function

    Private Shared Function BuildMeasurementDeviceCodePart(value As String, isLocation As Boolean) As String
        Dim normalized = If(value, "").Trim().ToUpperInvariant().
            Replace("Ç", "C").
            Replace("Ğ", "G").
            Replace("İ", "I").
            Replace("Ö", "O").
            Replace("Ş", "S").
            Replace("Ü", "U")
        normalized = Regex.Replace(normalized, "[^A-Z0-9]+", " ").Trim()

        If isLocation Then
            If normalized.Contains("GIRIS KALITE") Then Return "GKK"
            If normalized.Contains("KALITE LAB") Then Return "KLB"
            If normalized.Contains("PLASTIKHANE") Then Return "PLK"
            If normalized.Contains("MEKANIZMA") Then Return "MKN"
            If normalized.Contains("URETIM") Then Return "URT"
            If normalized.Contains("KALITE") Then Return "KLT"
            If normalized.Contains("A BLOK") Then Return "ABL"
        Else
            If normalized.Contains("KUMPAS") Then Return "KMP"
            If normalized.Contains("MIKROMETRE") Then Return "MKR"
            If normalized.Contains("MIHENGIR") Then Return "MHG"
            If normalized.Contains("KOMPARATOR") Then Return "KOM"
            If normalized.Contains("FIKSTUR") Then Return "FKS"
            If normalized.Contains("TEST") Then Return "TST"
        End If

        Dim words = normalized.Split({" "c}, StringSplitOptions.RemoveEmptyEntries)
        If words.Length = 0 Then Return If(isLocation, "GEN", "DGR")
        If words.Length = 1 Then Return words(0).Substring(0, Math.Min(3, words(0).Length)).PadRight(3, "X"c)

        Dim initials As New StringBuilder()
        For Each word In words
            If word.Length > 0 Then initials.Append(word(0))
            If initials.Length = 3 Then Exit For
        Next
        Return initials.ToString().PadRight(3, "X"c)
    End Function

    Public Shared Sub DeleteMeasurementDevice(deviceId As String)
        AuthorizationService.Require(AppState.CanModifyMsaDashboard, "Ölçüm Cihazı Silme")

        Dim target = CleanTestRequestSingleLine(deviceId, 100)
        If target = "" Then Throw New ArgumentException("Silinecek cihaz no boş olamaz.")

        Dim removed As Boolean = False
        CsvUtil.UpdateRowsLocked(
            AppPaths.MeasurementDevicesCsv,
            MeasurementDeviceHeaders,
            Sub(rows)
                For Each row In rows.ToList()
                    If String.Equals(GetValue(row, "DeviceId"), target, StringComparison.OrdinalIgnoreCase) Then
                        rows.Remove(row)
                        removed = True
                    End If
                Next
            End Sub)

        If Not removed Then Throw New InvalidOperationException("Silinecek cihaz bulunamadı: " & target)
    End Sub

    Private Shared Function NormalizeMeasurementDeviceStatus(value As String) As String
        Dim text = If(value, "").Trim().ToUpperInvariant()
        If text = "PASİF" OrElse text = "PASIF" OrElse text = "KULLANIM DIŞI" OrElse text = "KULLANIM DISI" Then Return "KULLANIM DIŞI"
        If text = "KALİBRASYON BEKLİYOR" OrElse text = "KALIBRASYON BEKLIYOR" OrElse text = "KALİBRASYON" OrElse text = "KALIBRASYON" Then Return "KALİBRASYON BEKLİYOR"
        Return "AKTİF"
    End Function

    Private Shared Function NormalizeMeasurementDeviceUsageStatus(value As String) As String
        Dim text = If(value, "").Trim().ToUpperInvariant()
        If text = "KULLANIM DIŞI" OrElse text = "KULLANIM DISI" OrElse text = "PASİF" OrElse text = "PASIF" Then Return "KULLANIM DIŞI"
        If text = "YEDEK" Then Return "YEDEK"
        If text = "ARIZALI" Then Return "ARIZALI"
        Return "KULLANIMDA"
    End Function

    Public Shared Function GetTestRequests() As List(Of Dictionary(Of String, String))
        AuthorizationService.Require(AppState.CanOpenTestRequests, "Test Talep Listesi")
        Return CsvUtil.ReadRows(AppPaths.TestRequestRecordsCsv)
    End Function

    Public Shared Sub DeleteTestRequest(requestId As String)
        AuthorizationService.Require(AppState.CanDeleteTestRequests, "Test Talebi Silme")

        requestId = CleanTestRequestSingleLine(requestId, 100)
        If requestId = "" Then Throw New ArgumentException("Silinecek test talep numarası boş olamaz.")

        Dim deletedProduct As String = ""
        Dim deletedStatus As String = ""
        Dim deleted As Boolean = CsvUtil.UpdateRowsLocked(
            AppPaths.TestRequestRecordsCsv,
            TestRequestHeaders,
            Function(rows)
                Dim row = rows.FirstOrDefault(
                    Function(item) String.Equals(GetValue(item, "RequestId"), requestId, StringComparison.OrdinalIgnoreCase))
                If row Is Nothing Then Return False

                deletedProduct = GetValue(row, "ProductNameTrCode")
                deletedStatus = GetValue(row, "Status")
                rows.Remove(row)
                Return True
            End Function)

        If Not deleted Then Throw New InvalidOperationException("Silinecek test talebi bulunamadı.")

        Dim deletedSteps As Integer = CsvUtil.UpdateRowsLocked(
            AppPaths.TestRequestStepsCsv,
            TestRequestStepHeaders,
            Function(rows)
                Return rows.RemoveAll(
                    Function(row) String.Equals(GetValue(row, "RequestId"), requestId, StringComparison.OrdinalIgnoreCase))
            End Function)

        AuditService.Log(
            "TEST_REQUEST_DELETE",
            "",
            "",
            "Talep No=" & requestId & "; Ürün/TR=" & deletedProduct & "; Durum=" & deletedStatus & "; Silinen test adımı=" & deletedSteps.ToString())
    End Sub

    Public Shared Function CreateTestRequest(inputRow As Dictionary(Of String, String)) As String
        AuthorizationService.Require(AppState.CanCreateTestRequest, "Test Talebi Oluşturma")
        If inputRow Is Nothing Then Throw New ArgumentNullException(NameOf(inputRow))

        Dim currentUser = If(AppState.CurrentUserName, "").Trim()
        If currentUser = "" Then Throw New UnauthorizedAccessException("Test talebi oluşturmak için aktif kullanıcı oturumu gereklidir.")

        Dim requestingDepartment = TestRequestEmailNotificationService.SerializeDepartments(
            TestRequestEmailNotificationService.SplitDepartments(CleanTestRequestSingleLine(GetValue(inputRow, "RequestingDepartment"), 100)))
        Dim requestedDepartment = CleanTestRequestSingleLine(GetValue(inputRow, "RequestedDepartment"), 100)
        Dim requestReason = CleanTestRequestSingleLine(GetValue(inputRow, "RequestReason"), 250)
        Dim productNameTrCode = CleanTestRequestSingleLine(GetValue(inputRow, "ProductNameTrCode"), 250)
        Dim requestedTests = GetValue(inputRow, "RequestedTests").Trim()

        If requestingDepartment = "" Then Throw New ArgumentException("Talep eden bölüm seçilmelidir.")
        If requestedDepartment = "" Then Throw New ArgumentException("Talep edilen bölüm seçilmelidir.")
        If requestReason = "" Then Throw New ArgumentException("Talep nedeni seçilmelidir.")
        If productNameTrCode = "" Then Throw New ArgumentException("Ürün adı / TR No zorunludur.")
        If requestedTests.Length > 4000 Then Throw New ArgumentException("Talep edilen test açıklaması en fazla 4000 karakter olabilir.")

        Dim dueDateText = CleanTestRequestSingleLine(GetValue(inputRow, "DueDate"), 20)
        If dueDateText <> "" Then
            Dim dueDate As DateTime
            If Not DateTime.TryParse(dueDateText, dueDate) Then Throw New ArgumentException("Geçerli bir termin tarihi girilmelidir.")
            dueDateText = dueDate.ToString("yyyy-MM-dd")
        End If

        Dim requestId = "TST-" & DateTime.Now.ToString("yyyyMMdd-HHmmss") & "-" & Guid.NewGuid().ToString("N").Substring(0, 5).ToUpperInvariant()
        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        Dim safeRow As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        For Each header In TestRequestHeaders
            safeRow(header) = ""
        Next

        safeRow("RequestId") = requestId
        safeRow("Status") = "OPEN"
        safeRow("CreatedAt") = nowText
        safeRow("CreatedBy") = currentUser
        safeRow("CreatedComputerName") = Environment.MachineName
        safeRow("RequestingDepartment") = requestingDepartment
        safeRow("RequestedDepartment") = requestedDepartment
        safeRow("RequestReason") = requestReason
        safeRow("ProductNameTrCode") = productNameTrCode
        safeRow("RequestedTests") = requestedTests
        safeRow("SampleQuantity") = CleanTestRequestSingleLine(GetValue(inputRow, "SampleQuantity"), 100)
        safeRow("Priority") = NormalizeTestRequestPriority(GetValue(inputRow, "Priority"))
        safeRow("DueDate") = dueDateText
        safeRow("RequesterReportNo") = CleanTestRequestSingleLine(GetValue(inputRow, "RequesterReportNo"), 100)
        safeRow("RequesterExplanation") = GetValue(inputRow, "RequesterExplanation").Trim()
        safeRow("UpdatedAt") = nowText
        safeRow("UpdatedBy") = currentUser

        CsvUtil.AppendRowLocked(AppPaths.TestRequestRecordsCsv, TestRequestHeaders, safeRow)
        If requestedTests <> "" Then ReplaceTestRequestStepSnapshot(requestId, requestedTests)
        AuditService.Log("TEST_REQUEST_CREATE", "", "", "Talep No=" & requestId & "; Ürün/TR=" & productNameTrCode & "; Talep edilen=" & requestedDepartment)
        Return requestId
    End Function

    Public Shared Sub UpdateTestRequestByAdmin(requestId As String, inputRow As Dictionary(Of String, String))
        AuthorizationService.Require(AppState.IsAdmin, "Test Talebi Admin Güncelleme")
        If inputRow Is Nothing Then Throw New ArgumentNullException(NameOf(inputRow))

        requestId = CleanTestRequestSingleLine(requestId, 100)
        If requestId = "" Then Throw New ArgumentException("Test talep numarası boş olamaz.")

        Dim requestingDepartment = TestRequestEmailNotificationService.SerializeDepartments(
            TestRequestEmailNotificationService.SplitDepartments(CleanTestRequestSingleLine(GetValue(inputRow, "RequestingDepartment"), 100)))
        Dim requestedDepartment = CleanTestRequestSingleLine(GetValue(inputRow, "RequestedDepartment"), 100)
        Dim requestReason = CleanTestRequestSingleLine(GetValue(inputRow, "RequestReason"), 250)
        Dim productNameTrCode = CleanTestRequestSingleLine(GetValue(inputRow, "ProductNameTrCode"), 250)
        If requestingDepartment = "" Then Throw New ArgumentException("Talep eden bölüm seçilmelidir.")
        If requestedDepartment = "" Then Throw New ArgumentException("Talep edilen bölüm seçilmelidir.")
        If requestReason = "" Then Throw New ArgumentException("Talep nedeni seçilmelidir.")
        If productNameTrCode = "" Then Throw New ArgumentException("Ürün adı / TR No zorunludur.")

        Dim dueDateText = CleanTestRequestSingleLine(GetValue(inputRow, "DueDate"), 20)
        If dueDateText <> "" Then
            Dim dueDate As DateTime
            If Not DateTime.TryParse(dueDateText, dueDate) Then Throw New ArgumentException("Geçerli bir termin tarihi girilmelidir.")
            dueDateText = dueDate.ToString("yyyy-MM-dd")
        End If

        Dim requesterExplanation = GetValue(inputRow, "RequesterExplanation").Trim()
        Dim labExplanation = GetValue(inputRow, "LabExplanation").Trim()
        If requesterExplanation.Length > 4000 Then Throw New ArgumentException("Talep eden açıklaması en fazla 4000 karakter olabilir.")
        If labExplanation.Length > 4000 Then Throw New ArgumentException("Laboratuvar açıklaması en fazla 4000 karakter olabilir.")

        Dim resultText = CleanTestRequestSingleLine(GetValue(inputRow, "Result"), 100)
        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        CsvUtil.UpdateRowsLocked(
            AppPaths.TestRequestRecordsCsv,
            TestRequestHeaders,
            Sub(rows)
                Dim row = FindTestRequest(rows, requestId)
                Dim status = GetValue(row, "Status").Trim().ToUpperInvariant()
                If status = "COMPLETED" AndAlso resultText = "" Then
                    Throw New ArgumentException("Tamamlanmış talebin sonuç bilgisi boş bırakılamaz.")
                End If
                If status = "COMPLETED" AndAlso labExplanation = "" Then
                    Throw New ArgumentException("Tamamlanmış talebin laboratuvar açıklaması boş bırakılamaz.")
                End If

                row("RequestingDepartment") = requestingDepartment
                row("RequestedDepartment") = requestedDepartment
                row("RequestReason") = requestReason
                row("ProductNameTrCode") = productNameTrCode
                row("SampleQuantity") = CleanTestRequestSingleLine(GetValue(inputRow, "SampleQuantity"), 100)
                row("Priority") = NormalizeTestRequestPriority(GetValue(inputRow, "Priority"))
                row("DueDate") = dueDateText
                row("RequesterReportNo") = CleanTestRequestSingleLine(GetValue(inputRow, "RequesterReportNo"), 100)
                row("RequesterExplanation") = requesterExplanation
                row("LabReportNo") = CleanTestRequestSingleLine(GetValue(inputRow, "LabReportNo"), 100)
                row("Result") = resultText
                row("LabExplanation") = labExplanation
                row("UpdatedAt") = nowText
                row("UpdatedBy") = AppState.CurrentUserName
            End Sub)

        AuditService.Log("TEST_REQUEST_ADMIN_UPDATE", "", "", "Talep No=" & requestId & "; Admin tarafından iş alanları güncellendi.")
    End Sub

    Public Shared Sub ResetTestRequestTestsByAdmin(requestId As String, requestedTests As String)
        AuthorizationService.Require(AppState.IsAdmin, "Test Talebi Admin Test Akışı Sıfırlama")
        requestId = CleanTestRequestSingleLine(requestId, 100)
        requestedTests = If(requestedTests, "").Trim()
        If requestId = "" Then Throw New ArgumentException("Test talep numarası boş olamaz.")
        If requestedTests.Length > 4000 Then Throw New ArgumentException("Talep edilen test açıklaması en fazla 4000 karakter olabilir.")

        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        CsvUtil.UpdateRowsLocked(
            AppPaths.TestRequestRecordsCsv,
            TestRequestHeaders,
            Sub(rows)
                Dim row = FindTestRequest(rows, requestId)
                row("RequestedTests") = requestedTests
                row("Status") = If(requestedTests = "", "OPEN", "ACCEPTED")
                If requestedTests = "" Then
                    row("AcceptedAt") = ""
                    row("AcceptedBy") = ""
                Else
                    row("AcceptedAt") = nowText
                    row("AcceptedBy") = AppState.CurrentUserName
                End If
                row("CompletedAt") = ""
                row("CompletedBy") = ""
                row("LabReportNo") = ""
                row("Result") = ""
                row("LabExplanation") = ""
                row("CancelledAt") = ""
                row("CancelledBy") = ""
                row("CancelReason") = ""
                row("UpdatedAt") = nowText
                row("UpdatedBy") = AppState.CurrentUserName
            End Sub)

        ReplaceTestRequestStepSnapshot(requestId, requestedTests)
        AuditService.Log("TEST_REQUEST_ADMIN_TESTS_RESET", "", "", "Talep No=" & requestId & "; Testler=" & requestedTests)
    End Sub

    Public Shared Sub UpdateTestRequestTests(requestId As String, requestedTests As String)
        AuthorizationService.Require(AppState.CanAssignTestRequestTests, "Test Talebi Test Atama")
        requestId = If(requestId, "").Trim()
        requestedTests = If(requestedTests, "").Trim()
        If requestId = "" Then Throw New ArgumentException("Test talep numarası boş olamaz.")
        If requestedTests.Length > 4000 Then Throw New ArgumentException("Talep edilen test açıklaması en fazla 4000 karakter olabilir.")

        Dim persistedSteps = ReadPersistedTestRequestSteps(requestId)
        If persistedSteps.Any(Function(stepItem) Not String.Equals(stepItem.Status, "PENDING", StringComparison.OrdinalIgnoreCase)) Then
            Throw New InvalidOperationException("Test yürütme işlemi başladığı için test ataması ve sırası artık değiştirilemez.")
        End If

        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        CsvUtil.UpdateRowsLocked(
            AppPaths.TestRequestRecordsCsv,
            TestRequestHeaders,
            Sub(rows)
                Dim row = FindTestRequest(rows, requestId)
                Dim status = GetValue(row, "Status").Trim().ToUpperInvariant()
                If status = "COMPLETED" OrElse status = "CANCELLED" Then
                    Throw New InvalidOperationException("Tamamlanmış veya iptal edilmiş test talebinde test ataması değiştirilemez.")
                End If

                row("RequestedTests") = requestedTests
                row("UpdatedAt") = nowText
                row("UpdatedBy") = AppState.CurrentUserName
            End Sub)

        ReplaceTestRequestStepSnapshot(requestId, requestedTests)

        AuditService.Log("TEST_REQUEST_TESTS_UPDATE", "", "", "Talep No=" & requestId & "; Testler=" & requestedTests)
    End Sub

    Public Shared Function GetTestRequestSteps(requestId As String) As List(Of TestRequestStep)
        AuthorizationService.Require(AppState.CanOpenTestRequests, "Test Talebi Test Akışı")
        requestId = If(requestId, "").Trim()
        If requestId = "" Then Return New List(Of TestRequestStep)()

        Dim persisted = ReadPersistedTestRequestSteps(requestId)
        If persisted.Count > 0 Then Return persisted

        Dim requestRow = CsvUtil.ReadRows(AppPaths.TestRequestRecordsCsv).
            FirstOrDefault(Function(row) String.Equals(GetValue(row, "RequestId"), requestId, StringComparison.OrdinalIgnoreCase))
        If requestRow Is Nothing Then Return New List(Of TestRequestStep)()
        Return BuildLegacyTestRequestSteps(requestRow)
    End Function

    Public Shared Sub CompleteTestRequestStep(requestId As String,
                                              stepId As String,
                                              resultText As String,
                                              explanation As String)
        requestId = CleanTestRequestSingleLine(requestId, 100)
        stepId = CleanTestRequestSingleLine(stepId, 150)
        resultText = CleanTestRequestSingleLine(resultText, 100)
        explanation = If(explanation, "").Trim()
        If requestId = "" OrElse stepId = "" Then Throw New ArgumentException("Test talebi ve test adımı seçilmelidir.")
        RequireTestRequestProcessingPermission(requestId, "Test Adımı Tamamlama")
        If resultText = "" Then resultText = "TAMAMLANDI"
        If explanation.Length > 2000 Then Throw New ArgumentException("Test açıklaması en fazla 2000 karakter olabilir.")

        RequireAcceptedTestRequest(requestId)
        EnsurePersistedTestRequestSteps(requestId)

        Dim completedTestName = ""
        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        CsvUtil.UpdateRowsLocked(
            AppPaths.TestRequestStepsCsv,
            TestRequestStepHeaders,
            Sub(rows)
                Dim requestRows = rows.
                    Where(Function(row) String.Equals(GetValue(row, "RequestId"), requestId, StringComparison.OrdinalIgnoreCase)).
                    OrderBy(Function(row) ParseIntSafe(GetValue(row, "SortNo"))).
                    ToList()
                Dim target = requestRows.FirstOrDefault(Function(row) String.Equals(GetValue(row, "StepId"), stepId, StringComparison.OrdinalIgnoreCase))
                If target Is Nothing Then Throw New InvalidOperationException("Seçili test adımı bulunamadı.")
                If Not String.Equals(NormalizeTestStepStatus(GetValue(target, "Status")), "PENDING", StringComparison.OrdinalIgnoreCase) Then
                    Throw New InvalidOperationException("Yalnızca bekleyen test adımı tamamlanabilir.")
                End If

                Dim nextPending = requestRows.FirstOrDefault(Function(row) String.Equals(NormalizeTestStepStatus(GetValue(row, "Status")), "PENDING", StringComparison.OrdinalIgnoreCase))
                If nextPending Is Nothing OrElse Not Object.ReferenceEquals(nextPending, target) Then
                    Throw New InvalidOperationException("Testler sıra ile tamamlanmalıdır. Önce sıradaki bekleyen testi tamamlayın.")
                End If

                completedTestName = GetValue(target, "TestName")
                target("Status") = "COMPLETED"
                target("Result") = resultText
                target("Explanation") = explanation
                target("CompletedAt") = nowText
                target("CompletedBy") = AppState.CurrentUserName
                target("CompletedComputerName") = Environment.MachineName
                target("SkippedAt") = ""
                target("SkippedBy") = ""
                target("SkipReason") = ""
                target("UpdatedAt") = nowText
                target("UpdatedBy") = AppState.CurrentUserName
            End Sub)

        AuditService.Log("TEST_REQUEST_STEP_COMPLETE", "", "", "Talep No=" & requestId & "; Test=" & completedTestName & "; Sonuç=" & resultText)
    End Sub

    Public Shared Sub SkipTestRequestStep(requestId As String, stepId As String, reason As String)
        AuthorizationService.Require(AppState.CanOverrideTestRequestSteps, "Test Adımı Atlama")
        requestId = CleanTestRequestSingleLine(requestId, 100)
        stepId = CleanTestRequestSingleLine(stepId, 150)
        reason = If(reason, "").Trim()
        If requestId = "" OrElse stepId = "" Then Throw New ArgumentException("Test talebi ve test adımı seçilmelidir.")
        If reason = "" Then Throw New ArgumentException("Testi atlama gerekçesi zorunludur.")
        If reason.Length > 2000 Then Throw New ArgumentException("Atlama gerekçesi en fazla 2000 karakter olabilir.")

        RequireAcceptedTestRequest(requestId)
        EnsurePersistedTestRequestSteps(requestId)

        Dim skippedTestName = ""
        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        CsvUtil.UpdateRowsLocked(
            AppPaths.TestRequestStepsCsv,
            TestRequestStepHeaders,
            Sub(rows)
                Dim requestRows = rows.
                    Where(Function(row) String.Equals(GetValue(row, "RequestId"), requestId, StringComparison.OrdinalIgnoreCase)).
                    OrderBy(Function(row) ParseIntSafe(GetValue(row, "SortNo"))).
                    ToList()
                Dim target = requestRows.FirstOrDefault(Function(row) String.Equals(GetValue(row, "StepId"), stepId, StringComparison.OrdinalIgnoreCase))
                If target Is Nothing Then Throw New InvalidOperationException("Seçili test adımı bulunamadı.")
                Dim nextPending = requestRows.FirstOrDefault(Function(row) String.Equals(NormalizeTestStepStatus(GetValue(row, "Status")), "PENDING", StringComparison.OrdinalIgnoreCase))
                If nextPending Is Nothing OrElse Not Object.ReferenceEquals(nextPending, target) Then
                    Throw New InvalidOperationException("Yalnızca sıradaki bekleyen test gerekçe ile atlanabilir.")
                End If

                skippedTestName = GetValue(target, "TestName")
                target("Status") = "SKIPPED"
                target("Result") = "ATLANDI"
                target("Explanation") = reason
                target("SkippedAt") = nowText
                target("SkippedBy") = AppState.CurrentUserName
                target("SkipReason") = reason
                target("UpdatedAt") = nowText
                target("UpdatedBy") = AppState.CurrentUserName
            End Sub)

        AuditService.Log("TEST_REQUEST_STEP_SKIP", "", "", "Talep No=" & requestId & "; Test=" & skippedTestName & "; Gerekçe=" & reason)
    End Sub

    Public Shared Sub ReopenTestRequestStep(requestId As String, stepId As String, reason As String)
        AuthorizationService.Require(AppState.CanOverrideTestRequestSteps, "Test Adımı Geri Açma")
        requestId = CleanTestRequestSingleLine(requestId, 100)
        stepId = CleanTestRequestSingleLine(stepId, 150)
        reason = If(reason, "").Trim()
        If requestId = "" OrElse stepId = "" Then Throw New ArgumentException("Test talebi ve test adımı seçilmelidir.")
        If reason = "" Then Throw New ArgumentException("Testi geri açma gerekçesi zorunludur.")
        If reason.Length > 2000 Then Throw New ArgumentException("Geri açma gerekçesi en fazla 2000 karakter olabilir.")

        RequireAcceptedTestRequest(requestId)
        EnsurePersistedTestRequestSteps(requestId)

        Dim reopenedTestName = ""
        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        CsvUtil.UpdateRowsLocked(
            AppPaths.TestRequestStepsCsv,
            TestRequestStepHeaders,
            Sub(rows)
                Dim requestRows = rows.
                    Where(Function(row) String.Equals(GetValue(row, "RequestId"), requestId, StringComparison.OrdinalIgnoreCase)).
                    OrderBy(Function(row) ParseIntSafe(GetValue(row, "SortNo"))).
                    ToList()
                Dim target = requestRows.FirstOrDefault(Function(row) String.Equals(GetValue(row, "StepId"), stepId, StringComparison.OrdinalIgnoreCase))
                If target Is Nothing Then Throw New InvalidOperationException("Seçili test adımı bulunamadı.")
                Dim status = NormalizeTestStepStatus(GetValue(target, "Status"))
                If status <> "COMPLETED" AndAlso status <> "SKIPPED" Then
                    Throw New InvalidOperationException("Yalnızca tamamlanmış veya atlanmış test geri açılabilir.")
                End If

                Dim targetSortNo = ParseIntSafe(GetValue(target, "SortNo"))
                If requestRows.Any(Function(row) ParseIntSafe(GetValue(row, "SortNo")) > targetSortNo AndAlso
                                                 (NormalizeTestStepStatus(GetValue(row, "Status")) = "COMPLETED" OrElse
                                                  NormalizeTestStepStatus(GetValue(row, "Status")) = "SKIPPED")) Then
                    Throw New InvalidOperationException("Sıra bütünlüğü için testler yalnızca son tamamlanan adımdan geriye doğru açılabilir.")
                End If

                reopenedTestName = GetValue(target, "TestName")
                target("Status") = "PENDING"
                target("Result") = ""
                target("Explanation") = ""
                target("CompletedAt") = ""
                target("CompletedBy") = ""
                target("CompletedComputerName") = ""
                target("SkippedAt") = ""
                target("SkippedBy") = ""
                target("SkipReason") = ""
                target("ReopenedAt") = nowText
                target("ReopenedBy") = AppState.CurrentUserName
                target("ReopenReason") = reason
                target("UpdatedAt") = nowText
                target("UpdatedBy") = AppState.CurrentUserName
            End Sub)

        AuditService.Log("TEST_REQUEST_STEP_REOPEN", "", "", "Talep No=" & requestId & "; Test=" & reopenedTestName & "; Gerekçe=" & reason)
    End Sub

    Public Shared Sub AcceptTestRequest(requestId As String)
        requestId = If(requestId, "").Trim()
        If requestId = "" Then Throw New ArgumentException("Test talep numarası boş olamaz.")
        RequireTestRequestProcessingPermission(requestId, "Test Talebi Kabul Etme")

        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        CsvUtil.UpdateRowsLocked(
            AppPaths.TestRequestRecordsCsv,
            TestRequestHeaders,
            Sub(rows)
                Dim row = FindTestRequest(rows, requestId)
                Dim status = GetValue(row, "Status").Trim().ToUpperInvariant()
                If status <> "OPEN" Then Throw New InvalidOperationException("Yalnızca yeni test talepleri işleme alınabilir.")
                row("Status") = "ACCEPTED"
                row("AcceptedAt") = nowText
                row("AcceptedBy") = AppState.CurrentUserName
                row("UpdatedAt") = nowText
                row("UpdatedBy") = AppState.CurrentUserName
            End Sub)
        AuditService.Log("TEST_REQUEST_ACCEPT", "", "", "Talep No=" & requestId)
    End Sub

    Public Shared Sub CompleteTestRequest(requestId As String,
                                          resultText As String,
                                          labReportNo As String,
                                          labExplanation As String)
        requestId = If(requestId, "").Trim()
        resultText = CleanTestRequestSingleLine(resultText, 100)
        labReportNo = CleanTestRequestSingleLine(labReportNo, 100)
        labExplanation = If(labExplanation, "").Trim()
        If requestId = "" Then Throw New ArgumentException("Test talep numarası boş olamaz.")
        RequireTestRequestProcessingPermission(requestId, "Test Talebi Sonuçlandırma")
        If resultText = "" Then Throw New ArgumentException("Test sonucu seçilmelidir.")
        If labExplanation = "" Then Throw New ArgumentException("Laboratuvar sonuç açıklaması zorunludur.")
        If labExplanation.Length > 4000 Then Throw New ArgumentException("Laboratuvar açıklaması en fazla 4000 karakter olabilir.")

        Dim testSteps = GetTestRequestSteps(requestId)
        If testSteps.Count = 0 Then
            Throw New InvalidOperationException("Talep sonuçlandırılmadan önce testler atanmalı ve sıra ile tamamlanmalıdır.")
        End If
        Dim pendingSteps = testSteps.Where(Function(stepItem) Not stepItem.IsResolved).ToList()
        If pendingSteps.Count > 0 Then
            Dim nextStep = pendingSteps.OrderBy(Function(stepItem) stepItem.SortNo).First()
            Throw New InvalidOperationException(
                "Talep sonuçlandırılamaz. Bekleyen test: " & nextStep.SortNo.ToString() & ". " & nextStep.TestName)
        End If

        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        CsvUtil.UpdateRowsLocked(
            AppPaths.TestRequestRecordsCsv,
            TestRequestHeaders,
            Sub(rows)
                Dim row = FindTestRequest(rows, requestId)
                Dim status = GetValue(row, "Status").Trim().ToUpperInvariant()
                If status <> "OPEN" AndAlso status <> "ACCEPTED" Then
                    Throw New InvalidOperationException("Yalnızca yeni veya işlemdeki test talepleri sonuçlandırılabilir.")
                End If
                If GetValue(row, "AcceptedAt").Trim() = "" Then
                    row("AcceptedAt") = nowText
                    row("AcceptedBy") = AppState.CurrentUserName
                End If
                row("Status") = "COMPLETED"
                row("CompletedAt") = nowText
                row("CompletedBy") = AppState.CurrentUserName
                row("LabReportNo") = labReportNo
                row("Result") = resultText
                row("LabExplanation") = labExplanation
                row("UpdatedAt") = nowText
                row("UpdatedBy") = AppState.CurrentUserName
            End Sub)
        AuditService.Log("TEST_REQUEST_COMPLETE", "", "", "Talep No=" & requestId & "; Sonuç=" & resultText & "; Rapor=" & labReportNo)
    End Sub

    Public Shared Sub CancelTestRequest(requestId As String, cancelReason As String)
        AuthorizationService.Require(AppState.CanOpenTestRequests, "Test Talebi İptali")
        requestId = If(requestId, "").Trim()
        cancelReason = If(cancelReason, "").Trim()
        If requestId = "" Then Throw New ArgumentException("Test talep numarası boş olamaz.")
        If cancelReason = "" Then Throw New ArgumentException("İptal nedeni zorunludur.")

        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        CsvUtil.UpdateRowsLocked(
            AppPaths.TestRequestRecordsCsv,
            TestRequestHeaders,
            Sub(rows)
                Dim row = FindTestRequest(rows, requestId)
                Dim currentUser = If(AppState.CurrentUserName, "").Trim()
                Dim isCreator = String.Equals(GetValue(row, "CreatedBy"), currentUser, StringComparison.OrdinalIgnoreCase)
                If Not isCreator AndAlso Not AppState.CanProcessTestRequest Then
                    Throw New UnauthorizedAccessException("Yalnızca talebi açan kullanıcı veya laboratuvar yetkilisi talebi iptal edebilir.")
                End If
                Dim status = GetValue(row, "Status").Trim().ToUpperInvariant()
                If status = "COMPLETED" OrElse status = "CANCELLED" Then
                    Throw New InvalidOperationException("Tamamlanmış veya iptal edilmiş talep yeniden iptal edilemez.")
                End If
                row("Status") = "CANCELLED"
                row("CancelledAt") = nowText
                row("CancelledBy") = currentUser
                row("CancelReason") = cancelReason
                row("UpdatedAt") = nowText
                row("UpdatedBy") = currentUser
            End Sub)
        AuditService.Log("TEST_REQUEST_CANCEL", "", "", "Talep No=" & requestId & "; Neden=" & cancelReason)
    End Sub

    Private Shared Function ReadPersistedTestRequestSteps(requestId As String) As List(Of TestRequestStep)
        Return CsvUtil.ReadRows(AppPaths.TestRequestStepsCsv).
            Where(Function(row) String.Equals(GetValue(row, "RequestId"), requestId, StringComparison.OrdinalIgnoreCase)).
            Select(AddressOf TestRequestStepFromRow).
            OrderBy(Function(stepItem) stepItem.SortNo).
            ThenBy(Function(stepItem) stepItem.TestName).
            ToList()
    End Function

    Private Shared Function TestRequestStepFromRow(row As Dictionary(Of String, String)) As TestRequestStep
        Return New TestRequestStep With {
            .RequestId = GetValue(row, "RequestId"),
            .StepId = GetValue(row, "StepId"),
            .SortNo = ParseIntSafe(GetValue(row, "SortNo")),
            .TestName = GetValue(row, "TestName"),
            .TestDescription = GetValue(row, "TestDescription"),
            .Status = NormalizeTestStepStatus(GetValue(row, "Status")),
            .Result = GetValue(row, "Result"),
            .Explanation = GetValue(row, "Explanation"),
            .CompletedAt = GetValue(row, "CompletedAt"),
            .CompletedBy = GetValue(row, "CompletedBy"),
            .CompletedComputerName = GetValue(row, "CompletedComputerName"),
            .SkippedAt = GetValue(row, "SkippedAt"),
            .SkippedBy = GetValue(row, "SkippedBy"),
            .SkipReason = GetValue(row, "SkipReason"),
            .ReopenedAt = GetValue(row, "ReopenedAt"),
            .ReopenedBy = GetValue(row, "ReopenedBy"),
            .ReopenReason = GetValue(row, "ReopenReason"),
            .CreatedAt = GetValue(row, "CreatedAt"),
            .CreatedBy = GetValue(row, "CreatedBy"),
            .UpdatedAt = GetValue(row, "UpdatedAt"),
            .UpdatedBy = GetValue(row, "UpdatedBy")
        }
    End Function

    Private Shared Function BuildLegacyTestRequestSteps(requestRow As Dictionary(Of String, String)) As List(Of TestRequestStep)
        Dim requestId = GetValue(requestRow, "RequestId")
        Dim requestStatus = GetValue(requestRow, "Status").Trim().ToUpperInvariant()
        Dim testNames = SplitTestRequestTests(GetValue(requestRow, "RequestedTests"))
        Dim catalog = GetTestCatalog(False)
        Dim result As New List(Of TestRequestStep)()
        For index As Integer = 0 To testNames.Count - 1
            Dim testName = testNames(index)
            Dim catalogItem = catalog.FirstOrDefault(Function(item) String.Equals(item.TestName, testName, StringComparison.OrdinalIgnoreCase))
            Dim stepStatus = "PENDING"
            If requestStatus = "COMPLETED" Then
                stepStatus = "COMPLETED"
            ElseIf requestStatus = "CANCELLED" Then
                stepStatus = "CANCELLED"
            End If

            result.Add(New TestRequestStep With {
                .RequestId = requestId,
                .StepId = BuildTestRequestStepId(requestId, index + 1),
                .SortNo = index + 1,
                .TestName = testName,
                .TestDescription = If(catalogItem Is Nothing, "", catalogItem.Description),
                .Status = stepStatus,
                .Result = If(requestStatus = "COMPLETED", GetValue(requestRow, "Result"), ""),
                .Explanation = If(requestStatus = "COMPLETED", "Eski talep kaydından aktarılan tamamlanma bilgisi.", ""),
                .CompletedAt = If(requestStatus = "COMPLETED", GetValue(requestRow, "CompletedAt"), ""),
                .CompletedBy = If(requestStatus = "COMPLETED", GetValue(requestRow, "CompletedBy"), ""),
                .CreatedAt = GetValue(requestRow, "CreatedAt"),
                .CreatedBy = GetValue(requestRow, "CreatedBy"),
                .UpdatedAt = GetValue(requestRow, "UpdatedAt"),
                .UpdatedBy = GetValue(requestRow, "UpdatedBy")
            })
        Next
        Return result
    End Function

    Private Shared Sub EnsurePersistedTestRequestSteps(requestId As String)
        If ReadPersistedTestRequestSteps(requestId).Count > 0 Then Return

        Dim requestRow = CsvUtil.ReadRows(AppPaths.TestRequestRecordsCsv).
            FirstOrDefault(Function(row) String.Equals(GetValue(row, "RequestId"), requestId, StringComparison.OrdinalIgnoreCase))
        If requestRow Is Nothing Then Throw New InvalidOperationException("Test talebi bulunamadı: " & requestId)

        Dim snapshot = BuildTestRequestStepSnapshot(requestId, GetValue(requestRow, "RequestedTests"))
        If snapshot.Count = 0 Then Throw New InvalidOperationException("Bu talebe henüz test atanmamış.")

        CsvUtil.UpdateRowsLocked(
            AppPaths.TestRequestStepsCsv,
            TestRequestStepHeaders,
            Sub(rows)
                If rows.Any(Function(row) String.Equals(GetValue(row, "RequestId"), requestId, StringComparison.OrdinalIgnoreCase)) Then Return
                For Each stepItem In snapshot
                    rows.Add(TestRequestStepToRow(stepItem))
                Next
            End Sub)
    End Sub

    Private Shared Sub ReplaceTestRequestStepSnapshot(requestId As String, requestedTests As String)
        Dim snapshot = BuildTestRequestStepSnapshot(requestId, requestedTests)
        CsvUtil.UpdateRowsLocked(
            AppPaths.TestRequestStepsCsv,
            TestRequestStepHeaders,
            Sub(rows)
                For Each row In rows.
                    Where(Function(item) String.Equals(GetValue(item, "RequestId"), requestId, StringComparison.OrdinalIgnoreCase)).
                    ToList()
                    rows.Remove(row)
                Next
                For Each stepItem In snapshot
                    rows.Add(TestRequestStepToRow(stepItem))
                Next
            End Sub)
    End Sub

    Private Shared Function BuildTestRequestStepSnapshot(requestId As String, requestedTests As String) As List(Of TestRequestStep)
        Dim testNames = SplitTestRequestTests(requestedTests)
        Dim catalog = GetTestCatalog(False)
        Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        Dim result As New List(Of TestRequestStep)()
        For index As Integer = 0 To testNames.Count - 1
            Dim testName = testNames(index)
            Dim catalogItem = catalog.FirstOrDefault(Function(item) String.Equals(item.TestName, testName, StringComparison.OrdinalIgnoreCase))
            result.Add(New TestRequestStep With {
                .RequestId = requestId,
                .StepId = BuildTestRequestStepId(requestId, index + 1),
                .SortNo = index + 1,
                .TestName = testName,
                .TestDescription = If(catalogItem Is Nothing, "", catalogItem.Description),
                .Status = "PENDING",
                .CreatedAt = nowText,
                .CreatedBy = AppState.CurrentUserName,
                .UpdatedAt = nowText,
                .UpdatedBy = AppState.CurrentUserName
            })
        Next
        Return result
    End Function

    Private Shared Function TestRequestStepToRow(stepItem As TestRequestStep) As Dictionary(Of String, String)
        Dim row As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        For Each header In TestRequestStepHeaders
            row(header) = ""
        Next
        row("RequestId") = stepItem.RequestId
        row("StepId") = stepItem.StepId
        row("SortNo") = stepItem.SortNo.ToString()
        row("TestName") = stepItem.TestName
        row("TestDescription") = stepItem.TestDescription
        row("Status") = NormalizeTestStepStatus(stepItem.Status)
        row("Result") = stepItem.Result
        row("Explanation") = stepItem.Explanation
        row("CompletedAt") = stepItem.CompletedAt
        row("CompletedBy") = stepItem.CompletedBy
        row("CompletedComputerName") = stepItem.CompletedComputerName
        row("SkippedAt") = stepItem.SkippedAt
        row("SkippedBy") = stepItem.SkippedBy
        row("SkipReason") = stepItem.SkipReason
        row("ReopenedAt") = stepItem.ReopenedAt
        row("ReopenedBy") = stepItem.ReopenedBy
        row("ReopenReason") = stepItem.ReopenReason
        row("CreatedAt") = stepItem.CreatedAt
        row("CreatedBy") = stepItem.CreatedBy
        row("UpdatedAt") = stepItem.UpdatedAt
        row("UpdatedBy") = stepItem.UpdatedBy
        Return row
    End Function

    Private Shared Function SplitTestRequestTests(value As String) As List(Of String)
        Return If(value, "").
            Replace(vbCrLf, ";").
            Replace(vbCr, ";").
            Replace(vbLf, ";").
            Split({";"c}, StringSplitOptions.RemoveEmptyEntries).
            Select(Function(part) part.Trim()).
            Where(Function(part) part <> "").
            ToList()
    End Function

    Private Shared Function BuildTestRequestStepId(requestId As String, sortNo As Integer) As String
        Return requestId & "-S" & sortNo.ToString("000")
    End Function

    Private Shared Function NormalizeTestStepStatus(value As String) As String
        Dim status = If(value, "").Trim().ToUpperInvariant()
        Select Case status
            Case "COMPLETED", "SKIPPED", "CANCELLED" : Return status
            Case Else : Return "PENDING"
        End Select
    End Function

    Private Shared Sub RequireAcceptedTestRequest(requestId As String)
        Dim requestRow = CsvUtil.ReadRows(AppPaths.TestRequestRecordsCsv).
            FirstOrDefault(Function(row) String.Equals(GetValue(row, "RequestId"), requestId, StringComparison.OrdinalIgnoreCase))
        If requestRow Is Nothing Then Throw New InvalidOperationException("Test talebi bulunamadı: " & requestId)
        Dim status = GetValue(requestRow, "Status").Trim().ToUpperInvariant()
        If status = "OPEN" Then Throw New InvalidOperationException("Testlere başlamadan önce talep işleme alınmalıdır.")
        If status <> "ACCEPTED" Then Throw New InvalidOperationException("Yalnızca işlemdeki test taleplerinde test adımı değiştirilebilir.")
    End Sub

    Private Shared Sub RequireTestRequestProcessingPermission(requestId As String, actionName As String)
        Dim requestRow = CsvUtil.ReadRows(AppPaths.TestRequestRecordsCsv).
            FirstOrDefault(Function(row) String.Equals(GetValue(row, "RequestId"), requestId, StringComparison.OrdinalIgnoreCase))
        If requestRow Is Nothing Then Throw New InvalidOperationException("Test talebi bulunamadı: " & requestId)

        Dim requestedDepartment = GetValue(requestRow, "RequestedDepartment").Trim()
        If String.Equals(requestedDepartment, "MEKANİZMA", StringComparison.OrdinalIgnoreCase) Then
            If Not AppState.IsMechanismQualityControlUser AndAlso Not AppState.IsAdmin Then
                Throw New UnauthorizedAccessException(
                    "Talep edilen bölüm MEKANİZMA olduğunda testi yalnızca Mekanizma Kalite Kontrol veya Admin rolündeki kullanıcı yapabilir.")
            End If
            Return
        End If

        AuthorizationService.Require(AppState.CanProcessTestRequest, actionName)
    End Sub

    Private Shared Function FindTestRequest(rows As List(Of Dictionary(Of String, String)), requestId As String) As Dictionary(Of String, String)
        Dim row = rows.FirstOrDefault(Function(item) String.Equals(GetValue(item, "RequestId"), requestId, StringComparison.OrdinalIgnoreCase))
        If row Is Nothing Then Throw New InvalidOperationException("Test talebi bulunamadı: " & requestId)
        Return row
    End Function

    Private Shared Function CleanTestRequestSingleLine(value As String, maxLength As Integer) As String
        Dim text = If(value, "").Replace(vbCr, " ").Replace(vbLf, " ").Trim()
        While text.Contains("  ")
            text = text.Replace("  ", " ")
        End While
        If text.Length > maxLength Then Throw New ArgumentException("Girilen alan en fazla " & maxLength.ToString() & " karakter olabilir.")
        Return text
    End Function

    Private Shared Function NormalizeTestRequestPriority(value As String) As String
        Dim text = If(value, "").Trim().ToUpperInvariant()
        If text = "ACİL" OrElse text = "ACIL" OrElse text = "YÜKSEK" OrElse text = "YUKSEK" Then Return "YÜKSEK"
        If text = "DÜŞÜK" OrElse text = "DUSUK" Then Return "DÜŞÜK"
        Return "NORMAL"
    End Function

    Private Shared Function ParseIntSafe(value As String) As Integer
        Dim parsed As Integer
        If Integer.TryParse(If(value, "").Trim(), parsed) Then Return parsed
        Return 0
    End Function

    Private Shared Function NormalizeYesNoFlag(value As String) As String
        Dim normalized = If(value, "").Trim().ToUpperInvariant()
        If normalized = "YES" OrElse normalized = "EVET" OrElse normalized = "TRUE" OrElse
           normalized = "1" OrElse normalized = "X" Then Return "YES"
        Return "NO"
    End Function

    Public Shared Function GetValue(row As Dictionary(Of String, String), key As String) As String
        If row.ContainsKey(key) Then Return row(key)
        Return ""
    End Function

    Private Shared Function ProductToRow(p As ProductInfo) As Dictionary(Of String, String)
        Return New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {"TrCode", p.TrCode},
            {"ProductName", p.ProductName},
            {"PlasticCode", p.PlasticCode},
            {"Material", p.Material},
            {"ColorName", p.ColorName},
            {"MoldCavityCount", p.MoldCavityCount},
            {"MoldCode", p.MoldCode},
            {"DrawingRev", p.DrawingRev},
            {"DrawingFile", p.DrawingFile},
            {"DrawingScope", ProductInfo.NormalizeDrawingScope(p.DrawingScope)},
            {"IsActive", p.IsActive},
            {"CreatedBy", p.CreatedBy},
            {"CreatedAt", p.CreatedAt}
        }
    End Function

    Private Shared Sub NormalizeControlPointSpcMetadata(cp As ControlPoint)
        If cp Is Nothing Then Return
        If String.IsNullOrWhiteSpace(cp.SpcKey) Then cp.SpcKey = If(cp.MeasureId, "").Trim()
        If cp.MeasureVersion <= 0 Then cp.MeasureVersion = 1
    End Sub

    Private Shared Function ControlPointToRow(cp As ControlPoint) As Dictionary(Of String, String)
        NormalizeControlPointSpcMetadata(cp)
        Return New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {"TrCode", cp.TrCode},
            {"DrawingRev", cp.DrawingRev},
            {"DrawingScope", ProductInfo.NormalizeDrawingScope(cp.DrawingScope)},
            {"MeasureId", cp.MeasureId},
            {"MeasureName", cp.MeasureName},
            {"Nominal", NumberUtil.DecToCsv(cp.Nominal)},
            {"LowerTol", NumberUtil.DecToCsv(cp.LowerTol)},
            {"UpperTol", NumberUtil.DecToCsv(cp.UpperTol)},
            {"LowerLimit", NumberUtil.DecToCsv(cp.LowerLimit)},
            {"UpperLimit", NumberUtil.DecToCsv(cp.UpperLimit)},
            {"PageNo", cp.PageNo.ToString()},
            {"XPercent", NumberUtil.DecToCsv(cp.XPercent)},
            {"YPercent", NumberUtil.DecToCsv(cp.YPercent)},
            {"Unit", cp.Unit},
            {"IsMandatory", cp.IsMandatory},
            {"MeasurementGroup", If(String.IsNullOrWhiteSpace(cp.MeasurementGroup), "Genel", cp.MeasurementGroup.Trim())},
            {"SampleFrequency", If(String.IsNullOrWhiteSpace(cp.SampleFrequency), "Her Kontrol", cp.SampleFrequency.Trim())},
            {"IsCritical", If(String.Equals(cp.IsCritical, "YES", StringComparison.OrdinalIgnoreCase), "YES", "NO")},
            {"SortNo", cp.SortNo.ToString()},
            {"IsActive", cp.IsActive},
            {"SpcKey", cp.SpcKey},
            {"MeasureVersion", cp.MeasureVersion.ToString()},
            {"ValidFrom", cp.ValidFrom},
            {"ValidTo", cp.ValidTo},
            {"ChangeReason", cp.ChangeReason}
        }
    End Function

    Private Shared Function MeasurementGroupAreaToRow(area As MeasurementGroupArea) As Dictionary(Of String, String)
        Return New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {"TrCode", area.TrCode},
            {"DrawingRev", area.DrawingRev},
            {"DrawingScope", ProductInfo.NormalizeDrawingScope(area.DrawingScope)},
            {"GroupName", area.GroupName},
            {"PageNo", area.PageNo.ToString()},
            {"LeftPercent", NumberUtil.DecToCsv(area.LeftPercent)},
            {"TopPercent", NumberUtil.DecToCsv(area.TopPercent)},
            {"RightPercent", NumberUtil.DecToCsv(area.RightPercent)},
            {"BottomPercent", NumberUtil.DecToCsv(area.BottomPercent)},
            {"UpdatedBy", area.UpdatedBy},
            {"UpdatedAt", area.UpdatedAt}
        }
    End Function

    Private Shared Function ToInt(text As String) As Integer
        Dim i As Integer = 0
        Integer.TryParse(text, i)
        Return i
    End Function

    Private Shared Function ToIntDefault(text As String, defaultValue As Integer) As Integer
        Dim i As Integer = defaultValue
        If Integer.TryParse(text, i) Then Return i
        Return defaultValue
    End Function
End Class
