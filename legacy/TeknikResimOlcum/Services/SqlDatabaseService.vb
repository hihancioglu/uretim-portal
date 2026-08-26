Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Text.RegularExpressions
Imports Microsoft.Data.SqlClient

Public NotInheritable Class SqlDatabaseService
    Private Sub New()
    End Sub

    Public Shared Sub TryEnsureSchemaFromConfig()
        Try
            If Not DatabaseConfig.IsSqlEnabled() Then Return

            Dim cs = DatabaseConfig.ConnectionString()
            If String.IsNullOrWhiteSpace(cs) Then Return

            EnsureSchema(cs)
        Catch ex As Exception
            ErrorLogService.Log("SqlDatabaseService.TryEnsureSchemaFromConfig", ex)
        End Try
    End Sub

    Public Shared Sub EnsureSchema(connectionString As String)
        If String.IsNullOrWhiteSpace(connectionString) Then Throw New ArgumentException("SQL bağlantı cümlesi boş olamaz.")

        Dim schemaPath = Path.Combine(AppPaths.BaseDir, "Sql", "Schema.sql")
        If Not File.Exists(schemaPath) Then
            schemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sql", "Schema.sql")
        End If

        If Not File.Exists(schemaPath) Then Throw New FileNotFoundException("SQL şema dosyası bulunamadı.", schemaPath)

        ExecuteSqlBatches(connectionString, File.ReadAllText(schemaPath, Encoding.UTF8))
    End Sub

    Public Shared Sub ImportAllCsvToSqlFromConfig()
        If Not DatabaseConfig.IsSqlEnabled() Then Throw New InvalidOperationException("SQL modu aktif değil. Data\\Database.config içinde Mode=SQL olmalıdır.")

        Dim cs = DatabaseConfig.ConnectionString()
        If String.IsNullOrWhiteSpace(cs) Then Throw New InvalidOperationException("SQL bağlantı cümlesi boş.")

        EnsureSchema(cs)

        BulkReplaceCsvTable(cs, "Users", AppPaths.UsersCsv, DataService.UserHeaders)
        BulkReplaceCsvTable(cs, "ActiveSessions", AppPaths.ActiveSessionsCsv, DataService.ActiveSessionHeaders)
        BulkReplaceCsvTable(cs, "Products", AppPaths.ProductsCsv, DataService.ProductHeaders)
        BulkReplaceCsvTable(cs, "ControlPoints", AppPaths.ControlPointsCsv, DataService.ControlPointHeaders)
        BulkReplaceCsvTable(cs, "MeasurementGroupAreas", AppPaths.MeasurementGroupAreasCsv, DataService.MeasurementGroupAreaHeaders)
        BulkReplaceCsvTable(cs, "MeasurementRecords", AppPaths.MeasurementsCsv, DataService.MeasurementHeaders)
        BulkReplaceCsvTable(cs, "MeasurementCorrections", AppPaths.MeasurementCorrectionsCsv, DataService.MeasurementCorrectionHeaders)
        BulkReplaceCsvTable(cs, "VisualControlRecords", AppPaths.VisualControlsCsv, DataService.VisualControlHeaders)
        BulkReplaceCsvTable(cs, "ClosedEyeRecords", AppPaths.ClosedEyesCsv, DataService.ClosedEyeHeaders)
        BulkReplaceCsvTable(cs, "AuditLog", AppPaths.AuditLogCsv, DataService.AuditHeaders)
        BulkReplaceCsvTable(cs, "ProductionTickets", AppPaths.ProductionTicketsCsv, DataService.ProductionTicketHeaders)
        BulkReplaceCsvTable(cs, "MoldBindingRecords", AppPaths.MoldBindingRecordsCsv, DataService.MoldBindingHeaders)
        BulkReplaceCsvTable(cs, "MoldTickets", AppPaths.MoldTicketsCsv, DataService.MoldTicketHeaders)
        BulkReplaceCsvTable(cs, "QualityToProductionTickets", AppPaths.QualityToProductionTicketsCsv, DataService.QualityToProductionTicketHeaders)
        BulkReplaceCsvTable(cs, "MoldConnectionPlan", AppPaths.MoldConnectionPlanCsv, DataService.MoldConnectionPlanHeaders)
        BulkReplaceCsvTable(cs, "MechanismQualityControlRecords", AppPaths.MechanismQualityControlRecordsCsv, DataService.MechanismQualityControlHeaders)
        BulkReplaceCsvTable(cs, "PlasticShiftTrackingRecords", AppPaths.PlasticShiftTrackingRecordsCsv, DataService.PlasticShiftTrackingHeaders)
        BulkReplaceCsvTable(cs, "MechanismShiftTrackingRecords", AppPaths.MechanismShiftTrackingRecordsCsv, DataService.PlasticShiftTrackingHeaders)
        BulkReplaceCsvTable(cs, "PlasticShiftEmailRecipients", AppPaths.PlasticShiftEmailRecipientsCsv, DataService.PlasticShiftEmailRecipientHeaders)
        BulkReplaceCsvTable(cs, "MechanismShiftEmailRecipients", AppPaths.MechanismShiftEmailRecipientsCsv, DataService.PlasticShiftEmailRecipientHeaders)
        BulkReplaceCsvTable(cs, "MechanismQualityEmailRecipients", AppPaths.MechanismQualityEmailRecipientsCsv, DataService.MechanismQualityEmailRecipientHeaders)
        BulkReplaceCsvTable(cs, "TestRequestEmailRecipients", AppPaths.TestRequestEmailRecipientsCsv, DataService.TestRequestEmailRecipientHeaders)
        BulkReplaceCsvTable(cs, "TestRequestEmailEvents", AppPaths.TestRequestEmailEventsCsv, DataService.TestRequestEmailEventHeaders)
        BulkReplaceCsvTable(cs, "TestRequestRecords", AppPaths.TestRequestRecordsCsv, DataService.TestRequestHeaders)
        BulkReplaceCsvTable(cs, "TestRequestSteps", AppPaths.TestRequestStepsCsv, DataService.TestRequestStepHeaders)
        BulkReplaceCsvTable(cs, "TestCatalog", AppPaths.TestCatalogCsv, DataService.TestCatalogHeaders)
        BulkReplaceCsvTable(cs, "TestGroups", AppPaths.TestGroupsCsv, DataService.TestGroupHeaders)
        BulkReplaceCsvTable(cs, "PackageMeterControls", AppPaths.PackageMeterControlsCsv, DataService.PackageMeterControlHeaders)
        BulkReplaceCsvTable(cs, "PackageMeterControlLines", AppPaths.PackageMeterControlLinesCsv, DataService.PackageMeterControlLineHeaders)
        BulkReplaceCsvTable(cs, "PackageMeterEmailRecipients", AppPaths.PackageMeterEmailRecipientsCsv, DataService.PackageMeterEmailRecipientHeaders)
    End Sub

    Private Shared Sub ExecuteSqlBatches(connectionString As String, sqlText As String)
        Dim batches = Regex.Split(sqlText, "^\s*GO\s*$", RegexOptions.Multiline Or RegexOptions.IgnoreCase)

        Using cn As New SqlConnection(connectionString)
            cn.Open()
            For Each batch In batches
                Dim sql = If(batch, "").Trim()
                If sql = "" Then Continue For

                Using cmd As New SqlCommand(sql, cn)
                    cmd.CommandTimeout = 120
                    cmd.ExecuteNonQuery()
                End Using
            Next
        End Using
    End Sub

    Private Shared Sub BulkReplaceCsvTable(connectionString As String, tableName As String, csvPath As String, headers As String())
        Dim rows = CsvUtil.ReadRows(csvPath)

        Using cn As New SqlConnection(connectionString)
            cn.Open()
            Using tr = cn.BeginTransaction()
                Try
                    Using deleteCmd As New SqlCommand("DELETE FROM " & Bracket(tableName), cn, tr)
                        deleteCmd.CommandTimeout = 120
                        deleteCmd.ExecuteNonQuery()
                    End Using

                    Dim colList = String.Join(",", headers.Select(Function(h) Bracket(h)))
                    Dim prmList = String.Join(",", Enumerable.Range(0, headers.Length).Select(Function(i) "@p" & i.ToString()))
                    Dim insertSql = "INSERT INTO " & Bracket(tableName) & " (" & colList & ") VALUES (" & prmList & ")"

                    For Each row In rows
                        Using cmd As New SqlCommand(insertSql, cn, tr)
                            cmd.CommandTimeout = 120
                            For i As Integer = 0 To headers.Length - 1
                                Dim v = If(row.ContainsKey(headers(i)), row(headers(i)), "")
                                cmd.Parameters.AddWithValue("@p" & i.ToString(), If(v Is Nothing, DBNull.Value, CType(v, Object)))
                            Next
                            cmd.ExecuteNonQuery()
                        End Using
                    Next

                    tr.Commit()
                Catch
                    tr.Rollback()
                    Throw
                End Try
            End Using
        End Using
    End Sub

    Private Shared Function Bracket(name As String) As String
        Return "[" & If(name, "").Replace("]", "]]") & "]"
    End Function
End Class
