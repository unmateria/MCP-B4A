Imports ModelContextProtocol.Server
Imports System.ComponentModel
Imports System.Diagnostics
Imports System.IO
Imports Newtonsoft.Json
Imports B4aMcp.Utils

Namespace Tools
    <McpServerToolType>
    Public Class AdbTools

        <McpServerTool, Description("Returns logcat output filtered by the 'B4A' tag. Read-only.")>
        Public Shared Async Function B4aGetLogcat(
            <Description("ADB device serial (optional, uses first device if not specified)")> Optional deviceSerial As String = "",
            <Description("Number of lines to return (default 100, max 500)")> Optional lines As Integer = 100,
            <Description("Additional logcat filter tag (in addition to B4A tag)")> Optional filter As String = ""
        ) As Task(Of String)
            lines = Math.Min(Math.Max(lines, 1), 500)
            Try
                Dim adbPath = FindAdb()
                If adbPath Is Nothing Then
                    Return "Error: adb not found. Set adbPath in config or install Android SDK Platform Tools."
                End If

                Dim deviceArg = If(Not String.IsNullOrEmpty(deviceSerial), $"-s {deviceSerial} ", "")
                Dim tagFilter = "B4A:*"
                If Not String.IsNullOrEmpty(filter) Then tagFilter &= $" {filter}:*"
                tagFilter &= " *:S"

                Dim psi As New ProcessStartInfo() With {
                    .FileName = adbPath,
                    .Arguments = $"{deviceArg}logcat -d {tagFilter}",
                    .RedirectStandardOutput = True,
                    .RedirectStandardError = True,
                    .UseShellExecute = False,
                    .CreateNoWindow = True
                }

                Dim output As New System.Text.StringBuilder()
                Using proc As New Process() With {.StartInfo = psi}
                    AddHandler proc.OutputDataReceived, Sub(s, e) If e.Data IsNot Nothing Then output.AppendLine(e.Data)
                    AddHandler proc.ErrorDataReceived, Sub(s, e) If e.Data IsNot Nothing Then output.AppendLine("[stderr] " & e.Data)
                    proc.Start()
                    proc.BeginOutputReadLine()
                    proc.BeginErrorReadLine()
                    Await Task.Run(Sub() proc.WaitForExit(15_000))
                End Using

                Dim allLines = output.ToString().Split(New String() {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries)
                Dim lastLines = allLines.TakeLast(lines).ToArray()
                Return $"[{lastLines.Length} lines]{Environment.NewLine}{String.Join(Environment.NewLine, lastLines)}"
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Lists ADB-connected Android devices")>
        Public Shared Async Function B4aListDevices() As Task(Of String)
            Try
                Dim cached As String = Nothing
                If CacheManager.TryGetByTtl(Of String)("adb:devices", cached) Then Return cached

                Dim adbPath = FindAdb()
                If adbPath Is Nothing Then
                    Return "Error: adb not found. Set adbPath in config or install Android SDK Platform Tools."
                End If

                Dim psi As New ProcessStartInfo() With {
                    .FileName = adbPath,
                    .Arguments = "devices -l",
                    .RedirectStandardOutput = True,
                    .RedirectStandardError = True,
                    .UseShellExecute = False,
                    .CreateNoWindow = True
                }

                Dim output As New System.Text.StringBuilder()
                Using proc As New Process() With {.StartInfo = psi}
                    AddHandler proc.OutputDataReceived, Sub(s, e) If e.Data IsNot Nothing Then output.AppendLine(e.Data)
                    proc.Start()
                    proc.BeginOutputReadLine()
                    Await Task.Run(Sub() proc.WaitForExit(10_000))
                End Using

                Dim raw = output.ToString()
                Dim deviceLines = raw.Split(New String() {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries) _
                    .Skip(1) _
                    .Where(Function(l) Not String.IsNullOrWhiteSpace(l)) _
                    .ToList()

                Dim devices = deviceLines.Select(Function(l)
                    Dim parts = l.Split(New Char() {" "c, Chr(9)}, StringSplitOptions.RemoveEmptyEntries)
                    Return New With {
                        .serial = If(parts.Length > 0, parts(0), ""),
                        .state = If(parts.Length > 1, parts(1), ""),
                        .info = If(parts.Length > 2, String.Join(" ", parts.Skip(2)), "")
                    }
                End Function).ToList()

                Dim result = JsonConvert.SerializeObject(New With {
                    .count = devices.Count,
                    .devices = devices
                }, Formatting.Indented)
                CacheManager.SetByTtl("adb:devices", result, 5)
                Return result
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Installs an APK on a connected Android device via ADB (-r flag allows reinstall).")>
        Public Shared Async Function B4aInstallApk(
            <Description("Full path to the APK file to install")> apkPath As String,
            <Description("ADB device serial (optional, uses first device if not specified)")> Optional deviceSerial As String = ""
        ) As Task(Of String)
            If Not File.Exists(apkPath) Then Return $"Error: APK not found: {apkPath}"
            Try
                Dim adbPath = FindAdb()
                If adbPath Is Nothing Then
                    Return "Error: adb not found. Set adbPath in config or install Android SDK Platform Tools."
                End If

                Dim deviceArg = If(Not String.IsNullOrEmpty(deviceSerial), $"-s {deviceSerial} ", "")
                Dim psi As New ProcessStartInfo() With {
                    .FileName = adbPath,
                    .Arguments = $"{deviceArg}install -r ""{apkPath}""",
                    .RedirectStandardOutput = True,
                    .RedirectStandardError = True,
                    .UseShellExecute = False,
                    .CreateNoWindow = True
                }

                Dim output As New System.Text.StringBuilder()
                Using proc As New Process() With {.StartInfo = psi}
                    AddHandler proc.OutputDataReceived, Sub(s, e) If e.Data IsNot Nothing Then output.AppendLine(e.Data)
                    AddHandler proc.ErrorDataReceived, Sub(s, e) If e.Data IsNot Nothing Then output.AppendLine("[err] " & e.Data)
                    proc.Start()
                    proc.BeginOutputReadLine()
                    proc.BeginErrorReadLine()
                    Await Task.Run(Sub() proc.WaitForExit(60_000))
                End Using
                Return output.ToString().Trim()
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

        ' ── Helper ───────────────────────────────────────────────────────────────

        Private Shared Function FindAdb() As String
            Dim cfg = AppConfig.Load()

            ' 1. Configured path
            If Not String.IsNullOrEmpty(cfg.AdbPath) Then
                If File.Exists(cfg.AdbPath) Then Return cfg.AdbPath
                Dim adbExe = Path.Combine(cfg.AdbPath, "adb.exe")
                If File.Exists(adbExe) Then Return adbExe
            End If

            ' 2. PATH environment variable
            Dim pathEnv = If(Environment.GetEnvironmentVariable("PATH"), "")
            For Each pathDir In pathEnv.Split(";"c)
                Dim candidate = Path.Combine(pathDir.Trim(), "adb.exe")
                If File.Exists(candidate) Then Return candidate
            Next

            ' 3. Well-known location
            Dim localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            Dim sdkPath = Path.Combine(localAppData, "Android", "Sdk", "platform-tools", "adb.exe")
            If File.Exists(sdkPath) Then Return sdkPath

            Return Nothing
        End Function

    End Class
End Namespace
