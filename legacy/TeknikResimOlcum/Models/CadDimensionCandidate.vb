Public Class CadDimensionCandidate
    Public Property IsSelected As Boolean = True
    Public Property EntityHandle As String = ""
    Public Property LayerName As String = ""
    Public Property LayoutName As String = ""
    Public Property DimensionType As String = ""
    Public Property DisplayText As String = ""
    Public Property MeasureName As String = ""
    Public Property SuggestedMeasureId As String = ""
    Public Property Nominal As Decimal
    Public Property LowerTolerance As Decimal
    Public Property UpperTolerance As Decimal
    Public Property Unit As String = "mm"
    Public Property PageNo As Integer = 1
    Public Property XPercent As Decimal
    Public Property YPercent As Decimal
    Public Property SortNo As Integer
    Public Property WarningText As String = ""
    Public Property RawX As Double
    Public Property RawY As Double
    Public Property RawTextHeight As Double
    Public Property RawTextRotationDegrees As Double
    Public Property HasRawTextRotation As Boolean
End Class

Public Class CadDimensionExtractionResult
    Public Property Candidates As New List(Of CadDimensionCandidate)()
    Public Property AutoCadToolPath As String = ""
    Public Property SourceDrawingPath As String = ""
    Public Property Unit As String = "mm"
End Class
