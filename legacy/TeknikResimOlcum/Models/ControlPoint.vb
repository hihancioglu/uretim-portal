Public Class ControlPoint
    Public Property TrCode As String = ""
    Public Property DrawingRev As String = ""
    Public Property DrawingScope As String = ProductInfo.DrawingScopePlastic
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
    Public Property IsActive As String = "YES"
    Public Property SpcKey As String = ""
    Public Property MeasureVersion As Integer = 1
    Public Property ValidFrom As String = ""
    Public Property ValidTo As String = ""
    Public Property ChangeReason As String = ""
End Class
