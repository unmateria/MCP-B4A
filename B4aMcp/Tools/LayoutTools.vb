Imports ModelContextProtocol.Server
Imports System.ComponentModel
Imports System.IO
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports B4aMcp.Utils

Namespace Tools
    <McpServerToolType>
    Public Class LayoutTools

        <McpServerTool, Description("Reads a B4A layout file (.bal or .bil) and returns its content as JSON")>
        Public Shared Function B4aReadLayout(
            <Description("Full path to the .bal or .bil layout file")> layoutPath As String
        ) As String
            If Not File.Exists(layoutPath) Then Return $"Error: File not found: {layoutPath}"
            Dim ext = Path.GetExtension(layoutPath).ToLowerInvariant()
            If ext <> ".bal" AndAlso ext <> ".bil" Then
                Return "Error: File must have .bal or .bil extension"
            End If
            Try
                Dim cached As String = Nothing
                If CacheManager.TryGetByMtime(Of String)(layoutPath, cached) Then Return cached

                Dim converter = New BalConverter(ext = ".bil")
                Dim dir = Path.GetDirectoryName(layoutPath)
                If String.IsNullOrEmpty(dir) Then dir = "."
                Dim json = converter.ConvertBalToJson(dir, Path.GetFileName(layoutPath))
                CacheManager.SetByMtime(layoutPath, json)
                Return json
            Catch ex As Exception
                Return $"Error reading layout: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Writes JSON data to a B4A layout file (.bal or .bil). Creates a .bak backup first.")>
        Public Shared Function B4aWriteLayout(
            <Description("Full path to the .bal or .bil layout file to write")> layoutPath As String,
            <Description("JSON layout data (as returned by b4a_read_layout)")> jsonData As String
        ) As String
            Dim ext = Path.GetExtension(layoutPath).ToLowerInvariant()
            If ext <> ".bal" AndAlso ext <> ".bil" Then
                Return "Error: File must have .bal or .bil extension"
            End If
            Try
                Dim json As JObject
                Try
                    json = JObject.Parse(jsonData)
                Catch ex As JsonException
                    Return $"Error: Invalid JSON — {ex.Message}"
                End Try

                ' Validate required structure
                If json("LayoutHeader") Is Nothing Then Return "Error: Missing 'LayoutHeader' in JSON"
                If json("Variants") Is Nothing Then Return "Error: Missing 'Variants' in JSON"
                If json("Data") Is Nothing Then Return "Error: Missing 'Data' in JSON"

                ' Backup
                If File.Exists(layoutPath) Then
                    File.Copy(layoutPath, layoutPath & ".bak", overwrite:=True)
                End If

                Dim converter = New BalConverter(ext = ".bil")
                Using stream = File.Create(layoutPath)
                    converter.ConvertJsonToBalInMemory(json, stream)
                End Using
                CacheManager.Invalidate(layoutPath)
                Return $"OK: backup saved as {layoutPath}.bak"
            Catch ex As Exception
                Return $"Error writing layout: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Lists all .bal and .bil layout files in a B4A project directory")>
        Public Shared Function B4aListLayouts(
            <Description("Path to the B4A project directory (or .b4a file path)")> projectDir As String
        ) As String
            If projectDir.EndsWith(".b4a", StringComparison.OrdinalIgnoreCase) Then
                projectDir = If(Path.GetDirectoryName(projectDir), ".")
            End If
            If Not Directory.Exists(projectDir) Then Return $"Error: Directory not found: {projectDir}"
            Try
                Dim layouts = Directory.GetFiles(projectDir, "*.bal", SearchOption.AllDirectories) _
                    .Concat(Directory.GetFiles(projectDir, "*.bil", SearchOption.AllDirectories)) _
                    .OrderBy(Function(f) f) _
                    .Select(Function(f) New With {
                        .name = Path.GetFileName(f),
                        .path = f,
                        .sizeKb = Math.Round(New FileInfo(f).Length / 1024.0, 1)
                    }).ToList()
                Return JsonConvert.SerializeObject(New With {
                    .count = layouts.Count,
                    .layouts = layouts
                }, Formatting.Indented)
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

    End Class
End Namespace
