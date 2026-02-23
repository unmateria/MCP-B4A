Imports System.IO
Imports Newtonsoft.Json
Imports B4aMcp.Models

Namespace Utils
    Public Class AppConfig
        Private Shared ReadOnly _configPath As String = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "mcp-b4a", "config.json")

        Private Shared ReadOnly _b4aIniPath As String = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Anywhere Software", "Basic4android", "b4xV5.ini")

        Private Shared _stored As McpConfig   ' Explicit overrides from JSON
        Private Shared _instance As McpConfig ' Effective config (stored + auto-detected)

        Public Shared Function Load() As McpConfig
            If _instance IsNot Nothing Then Return _instance
            LoadStored()
            _instance = MergeWithB4aDefaults(_stored)
            Return _instance
        End Function

        Private Shared Sub LoadStored()
            If Not File.Exists(_configPath) Then
                _stored = New McpConfig()
                SaveStored()
            Else
                Dim json = File.ReadAllText(_configPath)
                Dim parsed = JsonConvert.DeserializeObject(Of McpConfig)(json)
                _stored = If(parsed IsNot Nothing, parsed, New McpConfig())
            End If
        End Sub

        Private Shared Function MergeWithB4aDefaults(base As McpConfig) As McpConfig
            Dim merged As New McpConfig With {
                .B4aPath = base.B4aPath,
                .AdditionalLibrariesPath = base.AdditionalLibrariesPath,
                .AdbPath = base.AdbPath,
                .ProjectsRoot = base.ProjectsRoot,
                .SharedModulesFolder = base.SharedModulesFolder,
                .JavaBin = base.JavaBin
            }

            If Not File.Exists(_b4aIniPath) Then Return merged

            Dim ini = ParseIni(_b4aIniPath)

            ' Auto-detect AdditionalLibrariesPath from B4A's AdditionalLibrariesFolder
            If String.IsNullOrEmpty(merged.AdditionalLibrariesPath) Then
                Dim v As String = Nothing
                If ini.TryGetValue("AdditionalLibrariesFolder", v) AndAlso Not String.IsNullOrEmpty(v) Then
                    merged.AdditionalLibrariesPath = v
                End If
            End If

            ' Auto-detect AdbPath: look in ../platform-tools/adb.exe relative to ToolsFolder
            If String.IsNullOrEmpty(merged.AdbPath) Then
                Dim toolsFolder As String = Nothing
                If ini.TryGetValue("ToolsFolder", toolsFolder) AndAlso Not String.IsNullOrEmpty(toolsFolder) Then
                    Dim sdkRoot = Path.GetDirectoryName(toolsFolder.TrimEnd("\"c, "/"c))
                    If sdkRoot IsNot Nothing Then
                        Dim adbCandidate = Path.Combine(sdkRoot, "platform-tools", "adb.exe")
                        If File.Exists(adbCandidate) Then
                            merged.AdbPath = adbCandidate
                        End If
                    End If
                End If
            End If

            ' Auto-detect B4aPath from standard installation folder
            If String.IsNullOrEmpty(merged.B4aPath) Then
                Dim candidate = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Anywhere Software", "B4A")
                If Directory.Exists(candidate) Then
                    merged.B4aPath = candidate
                End If
            End If

            ' Auto-detect SharedModulesFolder
            If String.IsNullOrEmpty(merged.SharedModulesFolder) Then
                Dim v As String = Nothing
                If ini.TryGetValue("SharedModulesFolder", v) AndAlso Not String.IsNullOrEmpty(v) Then
                    merged.SharedModulesFolder = v
                End If
            End If

            ' Auto-detect JavaBin
            If String.IsNullOrEmpty(merged.JavaBin) Then
                Dim v As String = Nothing
                If ini.TryGetValue("JavaBin", v) AndAlso Not String.IsNullOrEmpty(v) Then
                    merged.JavaBin = v
                End If
            End If

            Return merged
        End Function

        Private Shared Function ParseIni(path As String) As Dictionary(Of String, String)
            Dim result As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            For Each line In File.ReadAllLines(path)
                Dim trimmed = line.Trim()
                If trimmed.StartsWith(";") OrElse trimmed.StartsWith("#") OrElse Not trimmed.Contains("=") Then Continue For
                Dim idx = trimmed.IndexOf("="c)
                Dim key = trimmed.Substring(0, idx).Trim()
                Dim value = trimmed.Substring(idx + 1).Trim()
                result(key) = value
            Next
            Return result
        End Function

        Private Shared Sub SaveStored()
            Dim dir = Path.GetDirectoryName(_configPath)
            If Not String.IsNullOrEmpty(dir) AndAlso Not Directory.Exists(dir) Then
                Directory.CreateDirectory(dir)
            End If
            File.WriteAllText(_configPath, JsonConvert.SerializeObject(_stored, Formatting.Indented))
        End Sub

        Public Shared Sub Save(config As McpConfig)
            _stored = config
            SaveStored()
            _instance = Nothing  ' Invalidate cache so it's rebuilt with auto-detected values
        End Sub

        Public Shared Function SetValue(key As String, value As String) As String
            If _stored Is Nothing Then LoadStored()
            Dim prop = GetType(McpConfig).GetProperty(key,
                Reflection.BindingFlags.Public Or Reflection.BindingFlags.Instance Or
                Reflection.BindingFlags.IgnoreCase)
            If prop Is Nothing Then
                Return $"Unknown key: {key}. Valid keys: b4aPath, additionalLibrariesPath, adbPath, projectsRoot, sharedModulesFolder, javaBin"
            End If
            prop.SetValue(_stored, value)
            SaveStored()
            _instance = Nothing  ' Invalidate cache
            CacheManager.InvalidateLibraries()
            Return $"OK: {key} = {value}"
        End Function

        Public Shared Function GetConfigPath() As String
            Return _configPath
        End Function

        Public Shared Function GetB4aIniPath() As String
            Return _b4aIniPath
        End Function

        ''' <summary>Exposes the parsed b4xV5.ini as a dictionary for use by other tools.</summary>
        Public Shared Function GetB4aIniValues() As Dictionary(Of String, String)
            If Not File.Exists(_b4aIniPath) Then Return New Dictionary(Of String, String)()
            Return ParseIni(_b4aIniPath)
        End Function

        ''' <summary>
        ''' Returns which keys have explicit overrides vs auto-detected from B4A ini.
        ''' </summary>
        Public Shared Function GetSources() As Dictionary(Of String, String)
            If _stored Is Nothing Then LoadStored()
            Dim sources As New Dictionary(Of String, String)
            For Each key In {"B4aPath", "AdditionalLibrariesPath", "AdbPath", "ProjectsRoot", "SharedModulesFolder", "JavaBin"}
                Dim prop = GetType(McpConfig).GetProperty(key)
                Dim storedVal = prop.GetValue(_stored)?.ToString()
                sources(key) = If(String.IsNullOrEmpty(storedVal), "auto (b4xV5.ini)", "explicit (mcp config)")
            Next
            Return sources
        End Function
    End Class
End Namespace
