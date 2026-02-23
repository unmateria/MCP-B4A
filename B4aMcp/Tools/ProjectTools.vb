Imports ModelContextProtocol.Server
Imports System.ComponentModel
Imports System.IO
Imports Newtonsoft.Json
Imports B4aMcp.Utils

Namespace Tools
    <McpServerToolType>
    Public Class ProjectTools

        <McpServerTool, Description("Reads a B4A project file (.b4a) and returns its metadata: libraries, modules, build config")>
        Public Shared Function B4aReadProject(
            <Description("Full path to the .b4a project file")> projectPath As String
        ) As String
            If Not File.Exists(projectPath) Then Return $"Error: File not found: {projectPath}"
            If Not projectPath.EndsWith(".b4a", StringComparison.OrdinalIgnoreCase) Then
                Return "Error: File must have .b4a extension"
            End If
            Try
                Dim proj = B4aParser.Parse(projectPath)
                Return JsonConvert.SerializeObject(New With {
                    .appLabel = proj.AppLabel,
                    .packageName = proj.PackageName,
                    .versionCode = proj.VersionCode,
                    .versionName = proj.VersionName,
                    .libraries = proj.Libraries,
                    .modules = proj.Modules.Select(Function(m) Path.GetFileName(m)).ToList(),
                    .layouts = proj.Layouts.Select(Function(l) Path.GetFileName(l)).ToList(),
                    .buildConfigs = proj.BuildConfigs
                }, Formatting.Indented)
            Catch ex As Exception
                Return $"Error parsing project: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Lists all source files, layouts, and assets in a B4A project")>
        Public Shared Function B4aListProjectFiles(
            <Description("Full path to the .b4a project file")> projectPath As String
        ) As String
            If Not File.Exists(projectPath) Then Return $"Error: File not found: {projectPath}"
            Try
                Dim proj = B4aParser.Parse(projectPath)
                Dim projectDir = Path.GetDirectoryName(projectPath)
                If String.IsNullOrEmpty(projectDir) Then projectDir = "."

                Dim assets As New List(Of String)
                Dim filesDir = Path.Combine(projectDir, "Files")
                If Directory.Exists(filesDir) Then
                    For Each f In Directory.GetFiles(filesDir, "*", SearchOption.AllDirectories)
                        assets.Add(f.Substring(filesDir.Length).TrimStart(Path.DirectorySeparatorChar))
                    Next
                End If

                Return JsonConvert.SerializeObject(New With {
                    .projectFile = projectPath,
                    .sourceModules = proj.Modules,
                    .layouts = proj.Layouts.Select(Function(l) Path.GetFileName(l)).ToList(),
                    .assets = assets
                }, Formatting.Indented)
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Returns full project context in one call: app info, libraries, modules, layouts, and last build error if any")>
        Public Shared Function B4aProjectContext(
            <Description("Full path to the .b4a project file")> projectPath As String
        ) As String
            If Not File.Exists(projectPath) Then Return $"Error: File not found: {projectPath}"
            Try
                Dim proj = B4aParser.Parse(projectPath)

                Dim lastError As String = ""
                Dim cachedLog As String = Nothing
                If CacheManager.TryGet(Of String)("lastBuildLog", cachedLog) Then
                    Dim errorLine = cachedLog _
                        .Split(New String() {Environment.NewLine, vbLf}, StringSplitOptions.RemoveEmptyEntries) _
                        .FirstOrDefault(Function(l) l.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0)
                    If errorLine IsNot Nothing Then lastError = errorLine.Trim()
                End If

                Return JsonConvert.SerializeObject(New With {
                    .appLabel = proj.AppLabel,
                    .packageName = proj.PackageName,
                    .versionCode = proj.VersionCode,
                    .versionName = proj.VersionName,
                    .libraries = proj.Libraries,
                    .modules = proj.Modules.Select(Function(m) Path.GetFileName(m)).ToList(),
                    .layouts = proj.Layouts.Select(Function(l) Path.GetFileName(l)).ToList(),
                    .lastBuildError = If(String.IsNullOrEmpty(lastError), Nothing, CObj(lastError))
                }, Formatting.Indented)
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Returns the list of recently opened B4A projects from the B4A IDE history (b4xV5.ini RecentFile entries).")>
        Public Shared Function B4aListRecentProjects() As String
            Try
                Dim ini = AppConfig.GetB4aIniValues()
                Dim recentFiles As New List(Of Object)
                Dim i = 1
                Do
                    Dim value As String = Nothing
                    If ini.TryGetValue($"RecentFile{i}", value) AndAlso Not String.IsNullOrEmpty(value) Then
                        recentFiles.Add(New With {
                            .index = i,
                            .path = value,
                            .exists = File.Exists(value),
                            .name = Path.GetFileNameWithoutExtension(value)
                        })
                        i += 1
                    Else
                        Exit Do
                    End If
                Loop
                Return JsonConvert.SerializeObject(New With {
                    .count = recentFiles.Count,
                    .recentProjects = recentFiles
                }, Formatting.Indented)
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

    End Class
End Namespace
