Namespace Models
    Public Class B4aProject
        Public Property ProjectFile As String = ""
        Public Property AppLabel As String = ""
        Public Property PackageName As String = ""
        Public Property VersionCode As String = "1"
        Public Property VersionName As String = "1.0"
        Public Property Libraries As New List(Of String)
        Public Property Modules As New List(Of String)
        Public Property Layouts As New List(Of String)
        Public Property Assets As New List(Of String)
        Public Property BuildConfigs As New Dictionary(Of String, String)
        Public Property ManifestBlock As String = ""
    End Class
End Namespace
