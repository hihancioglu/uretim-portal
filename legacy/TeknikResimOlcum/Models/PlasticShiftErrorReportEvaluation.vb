Public Class PlasticShiftErrorReportEvaluatorAssignment
    Public Property PositionKey As String = ""
    Public Property PositionName As String = ""
    Public Property RequiredRole As String = ""
    Public Property UserName As String = ""
    Public Property Email As String = ""
    Public Property IsActive As String = "YES"
    Public Property UpdatedBy As String = ""
    Public Property UpdatedAt As String = ""
End Class

Public Class PlasticShiftErrorReportEvaluation
    Public Property EvaluationId As String = ""
    Public Property ReportId As String = ""
    Public Property PositionKey As String = ""
    Public Property PositionName As String = ""
    Public Property RequiredRole As String = ""
    Public Property AssignedUserName As String = ""
    Public Property AssignedEmail As String = ""
    Public Property Decision As String = "PENDING"
    Public Property Explanation As String = ""
    Public Property EvaluatedBy As String = ""
    Public Property EvaluatedAt As String = ""
    Public Property UpdatedAt As String = ""
    Public Property ComputerName As String = ""
End Class

Public NotInheritable Class PlasticShiftErrorReportEvaluationPositions
    Public Const UnitManager As String = "UNIT_MANAGER"
    Public Const QualityManager As String = "QUALITY_MANAGER"
    Public Const TechnicalProductionManager As String = "TECHNICAL_PRODUCTION_MANAGER"

    Private Sub New()
    End Sub

    Public Shared Function AllKeys() As String()
        Return {UnitManager, QualityManager, TechnicalProductionManager}
    End Function

    Public Shared Function PositionName(positionKey As String) As String
        Select Case If(positionKey, "").Trim().ToUpperInvariant()
            Case UnitManager
                Return "İLGİLİ BİRİM AMİRİ"
            Case QualityManager
                Return "KALİTE KONTROL SORUMLUSU"
            Case TechnicalProductionManager
                Return "TEKNİK/ÜRETİM MÜDÜRÜ"
            Case Else
                Return If(positionKey, "").Trim()
        End Select
    End Function

    Public Shared Function RequiredRole(positionKey As String) As String
        If String.Equals(positionKey, QualityManager, StringComparison.OrdinalIgnoreCase) Then
            Return AppState.RoleQualityManager
        End If
        Return AppState.RoleProductionManager
    End Function
End Class
