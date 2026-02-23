Imports System.IO
Imports System.Text.RegularExpressions
Imports B4aMcp.Models

Namespace Utils
    Public Class B4aParser
        Private Const Separator As String = "@EndOfDesignText@"

        Public Shared Function Parse(projectFilePath As String) As B4aProject
            Dim proj As B4aProject = Nothing
            If CacheManager.TryGetByMtime(Of B4aProject)(projectFilePath, proj) Then Return proj

            proj = New B4aProject()
            proj.ProjectFile = projectFilePath
            Dim lines = File.ReadAllLines(projectFilePath)
            Dim sepIndex = Array.IndexOf(lines, Separator)
            If sepIndex < 0 Then sepIndex = lines.Length

            ' Parse header key=value pairs
            Dim header As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            For i = 0 To sepIndex - 1
                Dim line = lines(i).Trim()
                Dim eq = line.IndexOf("="c)
                If eq > 0 Then
                    header(line.Substring(0, eq).Trim()) = line.Substring(eq + 1).Trim()
                End If
            Next

            ' Extract libraries
            Dim libCount As Integer
            If header.ContainsKey("NumberOfLibraries") Then Integer.TryParse(header("NumberOfLibraries"), libCount)
            For i = 1 To libCount
                If header.ContainsKey($"Library{i}") Then proj.Libraries.Add(header($"Library{i}"))
            Next

            ' Extract modules (source files)
            Dim modCount As Integer
            If header.ContainsKey("NumberOfFiles") Then Integer.TryParse(header("NumberOfFiles"), modCount)
            For i = 1 To modCount
                If header.ContainsKey($"Module{i}") Then proj.Modules.Add(header($"Module{i}"))
            Next

            ' Remaining header entries as build configs
            Dim knownKeys As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
                "NumberOfLibraries", "NumberOfFiles"
            }
            For i = 1 To Math.Max(libCount, modCount) + 1
                knownKeys.Add($"Library{i}")
                knownKeys.Add($"Module{i}")
            Next
            For Each kv In header
                If Not knownKeys.Contains(kv.Key) Then proj.BuildConfigs(kv.Key) = kv.Value
            Next

            ' Parse code section
            If sepIndex < lines.Length - 1 Then
                ParseCodeSection(proj, lines.Skip(sepIndex + 1).ToArray())
            End If

            ' Find layouts next to project or in Files subfolder
            Dim projectDir = Path.GetDirectoryName(projectFilePath)
            If String.IsNullOrEmpty(projectDir) Then projectDir = "."
            Dim filesDir = Path.Combine(projectDir, "Files")
            For Each searchDir In {projectDir, filesDir}
                If Directory.Exists(searchDir) Then
                    For Each f In Directory.GetFiles(searchDir, "*.bal").Concat(Directory.GetFiles(searchDir, "*.bil"))
                        proj.Layouts.Add(f)
                    Next
                End If
            Next

            CacheManager.SetByMtime(projectFilePath, proj)
            Return proj
        End Function

        Private Shared Sub ParseCodeSection(proj As B4aProject, lines As String())
            Dim inAttributes = False
            Dim inManifest = False
            Dim manifestLines As New List(Of String)

            For Each line In lines
                Dim trimmed = line.TrimStart()

                If trimmed.StartsWith("#Region") AndAlso line.Contains("Project Attributes") Then
                    inAttributes = True
                    Continue For
                End If
                If inAttributes AndAlso trimmed.StartsWith("#End Region") Then
                    inAttributes = False
                    Continue For
                End If

                If inAttributes Then
                    Dim m = Regex.Match(line, "#ApplicationLabel:\s*(.+)", RegexOptions.IgnoreCase)
                    If m.Success Then proj.AppLabel = m.Groups(1).Value.Trim() : Continue For
                    m = Regex.Match(line, "#Package:\s*(.+)", RegexOptions.IgnoreCase)
                    If m.Success Then proj.PackageName = m.Groups(1).Value.Trim() : Continue For
                    m = Regex.Match(line, "#VersionCode:\s*(.+)", RegexOptions.IgnoreCase)
                    If m.Success Then proj.VersionCode = m.Groups(1).Value.Trim() : Continue For
                    m = Regex.Match(line, "#VersionName:\s*(.+)", RegexOptions.IgnoreCase)
                    If m.Success Then proj.VersionName = m.Groups(1).Value.Trim() : Continue For
                End If

                If trimmed.StartsWith("#Region") AndAlso line.Contains("Manifest Editor") Then
                    inManifest = True
                    Continue For
                End If
                If inManifest AndAlso trimmed.StartsWith("#End Region") Then
                    inManifest = False
                    proj.ManifestBlock = String.Join(Environment.NewLine, manifestLines)
                    Continue For
                End If
                If inManifest Then manifestLines.Add(line)
            Next
        End Sub

        Public Shared Function GetManifestBlock(projectFilePath As String) As String
            Return Parse(projectFilePath).ManifestBlock
        End Function

        Public Shared Sub WriteManifestBlock(projectFilePath As String, newManifest As String)
            Dim content = File.ReadAllText(projectFilePath)
            Dim pattern = "(#Region\s+Manifest Editor.*?)(#End Region)"
            Dim replacement = $"#Region  Manifest Editor{Environment.NewLine}{newManifest}{Environment.NewLine}#End Region"
            Dim newContent = Regex.Replace(content, pattern, replacement,
                RegexOptions.Singleline Or RegexOptions.IgnoreCase)
            If newContent = content Then
                ' No manifest region found — append at end of file
                newContent = content & Environment.NewLine &
                    $"#Region  Manifest Editor{Environment.NewLine}{newManifest}{Environment.NewLine}#End Region"
            End If
            File.Copy(projectFilePath, projectFilePath & ".bak", overwrite:=True)
            File.WriteAllText(projectFilePath, newContent)
            CacheManager.Invalidate(projectFilePath)
        End Sub
    End Class
End Namespace
