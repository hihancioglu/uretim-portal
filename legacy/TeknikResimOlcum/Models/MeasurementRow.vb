Public Class MeasurementRow
    Public Property MeasureId As String = ""
    Public Property MeasureName As String = ""
    Public Property Nominal As Decimal
    Public Property LowerTol As Decimal
    Public Property UpperTol As Decimal
    Public Property LowerLimit As Decimal
    Public Property UpperLimit As Decimal
    Public Property PageNo As Integer = 1
    Public Property XPercent As Decimal
    Public Property YPercent As Decimal
    Public Property Unit As String = "mm"
    Public Property IsMandatory As String = "YES"
    Public Property MeasurementGroup As String = "Genel"
    Public Property SampleFrequency As String = "Her Kontrol"
    Public Property IsCritical As String = "NO"
    Public Property SortNo As Integer
    Public Property SpcKey As String = ""
    Public Property MeasureVersion As Integer = 1
    Public Property MeasuredValueText As String = ""
    Public Property Result As String = ""
    Public Property Note As String = ""
End Class
