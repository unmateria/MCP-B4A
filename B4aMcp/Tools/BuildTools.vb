Imports ModelContextProtocol.Server
Imports System.ComponentModel
Imports System.Diagnostics
Imports System.IO
Imports Newtonsoft.Json
Imports B4aMcp.Utils

Namespace Tools
    <McpServerToolType>
    Public Class BuildTools
        Private Const LastBuildLogKey As String = "lastBuildLog"

        <McpServerTool, Description("Compiles a B4A project using B4ABuilder.exe. Returns the full build log and the output APK path on success.")>
        Public Shared Async Function B4aBuild(
            <Description("Full path to the .b4a project file")> projectPath As String,
            <Description("Build mode: 'release' (default, signed APK), 'debug' (unsigned APK), 'bundle' (signed AAB)")> Optional mode As String = "release"
        ) As Task(Of String)
            If Not File.Exists(projectPath) Then Return $"Error: File not found: {projectPath}"
            If Not projectPath.EndsWith(".b4a", StringComparison.OrdinalIgnoreCase) Then
                Return "Error: File must have .b4a extension"
            End If

            Dim cfg = AppConfig.Load()
            If String.IsNullOrEmpty(cfg.B4aPath) Then
                Return "Error: b4aPath is not configured. Use b4a_set_config(key='b4aPath', value='C:\\B4A')"
            End If

            Dim builderPath = Path.Combine(cfg.B4aPath, "B4ABuilder.exe")
            If Not File.Exists(builderPath) Then
                Return $"Error: B4ABuilder.exe not found at {builderPath}"
            End If

            Dim baseFolder = Path.GetDirectoryName(projectPath)
            Dim projectFile = Path.GetFileName(projectPath)
            Dim normalizedMode = If(mode, "release").ToLowerInvariant().Trim()

            ' Choose build task
            Dim buildTask As String = If(normalizedMode = "bundle", "BuildBundle", "Build")

            ' NoSign only for debug mode
            Dim noSignArg As String = If(normalizedMode = "debug", " -NoSign=True", "")

            ' Always pass the INI file so B4ABuilder picks up the correct keystore
            Dim iniArg As String = ""
            Dim iniPath = AppConfig.GetB4aIniPath()
            If File.Exists(iniPath) Then iniArg = $" -INI=""{iniPath}"""

            Dim args = $"-Task={buildTask} -BaseFolder=""{baseFolder}"" -Project=""{projectFile}"" -Obfuscate=False -ShowWarnings=True{noSignArg}{iniArg}"

            Try
                Dim psi As New ProcessStartInfo() With {
                    .FileName = builderPath,
                    .Arguments = args,
                    .RedirectStandardOutput = True,
                    .RedirectStandardError = True,
                    .UseShellExecute = False,
                    .CreateNoWindow = True
                }

                Dim output As New System.Text.StringBuilder()
                Dim exitCode As Integer = -1
                Using proc As New Process() With {.StartInfo = psi}
                    AddHandler proc.OutputDataReceived, Sub(s, e) If e.Data IsNot Nothing Then output.AppendLine(e.Data)
                    AddHandler proc.ErrorDataReceived, Sub(s, e) If e.Data IsNot Nothing Then output.AppendLine(e.Data)
                    proc.Start()
                    proc.BeginOutputReadLine()
                    proc.BeginErrorReadLine()
                    Await Task.Run(Sub() proc.WaitForExit(300_000))
                    exitCode = proc.ExitCode
                End Using

                Dim log = output.ToString()
                CacheManager.Store(LastBuildLogKey, log)

                Dim result As New System.Text.StringBuilder()
                result.AppendLine($"Build completed (exit code {exitCode}):")
                result.AppendLine(log)

                ' Append output APK/AAB path if build succeeded
                If exitCode = 0 Then
                    Dim projectName = Path.GetFileNameWithoutExtension(projectPath)
                    Dim ext = If(normalizedMode = "bundle", ".aab", ".apk")
                    Dim outputPath = Path.Combine(baseFolder, "Objects", projectName & ext)
                    If File.Exists(outputPath) Then
                        result.AppendLine($"Output: {outputPath}")
                    End If
                End If

                Return result.ToString().TrimEnd()
            Catch ex As Exception
                Return $"Error starting build: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Returns the log from the last b4a_build call")>
        Public Shared Function B4aGetBuildLog() As String
            Dim log As String = Nothing
            If CacheManager.TryGet(Of String)(LastBuildLogKey, log) Then Return log
            Return "No build log available. Run b4a_build first."
        End Function

        <McpServerTool, Description("Returns the signing configuration from B4A IDE settings: keystore path, alias, and whether signing is fully configured. Does NOT expose the password.")>
        Public Shared Function B4aGetSigningInfo() As String
            Try
                Dim ini = AppConfig.GetB4aIniValues()
                Dim keyFile As String = Nothing
                Dim keyAlias As String = Nothing
                Dim keyPassword As String = Nothing
                ini.TryGetValue("SignKeyFile", keyFile)
                ini.TryGetValue("SignKeyAlias", keyAlias)
                ini.TryGetValue("SignKeyPassword", keyPassword)

                Dim keyFileExists = Not String.IsNullOrEmpty(keyFile) AndAlso File.Exists(keyFile)

                Return JsonConvert.SerializeObject(New With {
                    .keyFile = If(keyFile, ""),
                    .keyFileExists = keyFileExists,
                    .keyAlias = If(keyAlias, ""),
                    .hasPassword = Not String.IsNullOrEmpty(keyPassword),
                    .signingConfigured = keyFileExists AndAlso Not String.IsNullOrEmpty(keyAlias) AndAlso Not String.IsNullOrEmpty(keyPassword)
                }, Formatting.Indented)
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

    End Class
End Namespace
