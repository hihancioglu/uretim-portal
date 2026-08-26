Public Class ProductInfo
    Public Const DrawingScopePlastic As String = "Plastik Resmi"
    Public Const DrawingScopeIncomingQuality As String = "Giriş Kalite Kontrol Resmi"
    Public Const DrawingScopeTr As String = "TR Resmi"

    Public Property TrCode As String = ""
    Public Property ProductName As String = ""
    Public Property PlasticCode As String = ""
    Public Property Material As String = ""
    Public Property ColorName As String = ""
    Public Property MoldCavityCount As String = ""
    Public Property MoldCode As String = ""
    Public Property DrawingRev As String = ""
    Public Property DrawingFile As String = ""
    Public Property DrawingScope As String = DrawingScopePlastic
    Public Property IsActive As String = "YES"
    Public Property CreatedBy As String = ""
    Public Property CreatedAt As String = ""

    Public ReadOnly Property DisplayName As String
        Get
            Return $"{TrCode} | {DrawingRev} | {ProductName} | {NormalizeDrawingScope(DrawingScope)}"
        End Get
    End Property

    Public Shared ReadOnly Property DrawingScopeLabels As String()
        Get
            Return New String() {DrawingScopePlastic, DrawingScopeIncomingQuality, DrawingScopeTr}
        End Get
    End Property

    Public Shared Function NormalizeDrawingScope(value As String) As String
        Dim raw = If(value, "").Trim()
        If raw = "" Then Return DrawingScopePlastic

        If String.Equals(raw, DrawingScopePlastic, StringComparison.OrdinalIgnoreCase) Then Return DrawingScopePlastic
        If String.Equals(raw, DrawingScopeIncomingQuality, StringComparison.OrdinalIgnoreCase) Then Return DrawingScopeIncomingQuality
        If String.Equals(raw, DrawingScopeTr, StringComparison.OrdinalIgnoreCase) Then Return DrawingScopeTr

        Dim t = raw.ToUpperInvariant().
            Replace("İ", "I").
            Replace("ı", "I").
            Replace("Ş", "S").
            Replace("Ğ", "G").
            Replace("Ü", "U").
            Replace("Ö", "O").
            Replace("Ç", "C")

        If t.Contains("GIRIS") OrElse t.Contains("GKK") OrElse t.Contains("INCOMING") Then
            Return DrawingScopeIncomingQuality
        End If
        If t = "TR RESMI" OrElse t = "TR DRAWING" Then
            Return DrawingScopeTr
        End If

        Return DrawingScopePlastic
    End Function

    Public Shared Function DrawingScopeFolder(value As String) As String
        Dim normalized = NormalizeDrawingScope(value)
        If String.Equals(normalized, DrawingScopeIncomingQuality, StringComparison.OrdinalIgnoreCase) Then
            Return "GirisKaliteKontrolResmi"
        End If
        If String.Equals(normalized, DrawingScopeTr, StringComparison.OrdinalIgnoreCase) Then
            Return "TRResmi"
        End If

        Return "PlastikResmi"
    End Function

    Public Function GetMissingMetadataFields() As List(Of String)
        Dim missing As New List(Of String)()
        If String.IsNullOrWhiteSpace(ProductName) Then missing.Add("Ürün Adı")
        If String.IsNullOrWhiteSpace(PlasticCode) Then missing.Add("Plastik Kodu")
        If String.IsNullOrWhiteSpace(Material) Then missing.Add("Malzeme")
        If String.IsNullOrWhiteSpace(ColorName) Then missing.Add("Renk")
        If String.IsNullOrWhiteSpace(MoldCavityCount) Then missing.Add("Kalıp Göz Adedi")
        If String.IsNullOrWhiteSpace(MoldCode) Then missing.Add("Kalıp Kodu")
        Return missing
    End Function

    Public ReadOnly Property HasIncompleteMetadata As Boolean
        Get
            Return GetMissingMetadataFields().Count > 0
        End Get
    End Property
End Class
