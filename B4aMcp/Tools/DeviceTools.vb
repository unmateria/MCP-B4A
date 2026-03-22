Imports ModelContextProtocol.Server
Imports System.ComponentModel
Imports System.Diagnostics
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports Newtonsoft.Json
Imports B4aMcp.Utils

Namespace Tools

    ''' <summary>
    ''' Device interaction tools: screenshot, tap, swipe, launch app, pixel scan.
    ''' Designed to replace manual Bash+PIL workflows for UI verification.
    ''' </summary>
    <McpServerToolType>
    Public Class DeviceTools

        ' ── Screenshot ────────────────────────────────────────────────────────────

        <McpServerTool, Description(
            "Captures a screenshot from the Android device and saves it to disk. " &
            "Optionally crops to a sub-region. Returns the output path and image dimensions. " &
            "After calling this, use the Read tool on the returned path to view the image.")>
        Public Shared Async Function B4aScreenshot(
            <Description("ADB device serial (optional, uses first device if not specified)")>
            Optional deviceSerial As String = "",
            <Description("Output file path (optional, default: C:\temp\b4a_ss.png)")>
            Optional outputPath As String = "",
            <Description("Crop: left X in device pixels (only applied when cropW > 0)")>
            Optional cropX As Integer = 0,
            <Description("Crop: top Y in device pixels (only applied when cropW > 0)")>
            Optional cropY As Integer = 0,
            <Description("Crop: width in pixels (0 = full screen, no crop)")>
            Optional cropW As Integer = 0,
            <Description("Crop: height in pixels (0 = full screen, no crop)")>
            Optional cropH As Integer = 0,
            <Description("Milliseconds to wait before capturing (useful after tap/swipe/launch to let the UI settle). Default 0.")>
            Optional delayMs As Integer = 0
        ) As Task(Of String)
            Try
                If delayMs > 0 Then Await Task.Delay(delayMs)

                Dim adbPath = FindAdb()
                If adbPath Is Nothing Then Return "Error: adb not found. Check adbPath in config."

                If String.IsNullOrEmpty(outputPath) Then
                    outputPath = Path.Combine("C:\temp", "b4a_ss.png")
                End If
                Dim outDir = Path.GetDirectoryName(outputPath)
                If Not String.IsNullOrEmpty(outDir) Then Directory.CreateDirectory(outDir)

                Dim deviceArg = If(Not String.IsNullOrEmpty(deviceSerial), $"-s {deviceSerial} ", "")

                Dim psi As New ProcessStartInfo() With {
                    .FileName = adbPath,
                    .Arguments = $"{deviceArg}exec-out screencap -p",
                    .RedirectStandardOutput = True,
                    .RedirectStandardError = True,
                    .UseShellExecute = False,
                    .CreateNoWindow = True
                }

                ' Read raw PNG bytes from binary stdout
                Dim pngBytes As Byte()
                Using proc As New Process() With {.StartInfo = psi}
                    proc.Start()
                    Dim ms As New MemoryStream()
                    Dim buf(65535) As Byte
                    Dim bytesRead As Integer
                    Do
                        bytesRead = Await proc.StandardOutput.BaseStream.ReadAsync(buf, 0, buf.Length)
                        If bytesRead > 0 Then ms.Write(buf, 0, bytesRead)
                    Loop While bytesRead > 0
                    Await Task.Run(Sub() proc.WaitForExit(15_000))
                    pngBytes = ms.ToArray()
                End Using

                If pngBytes.Length < 100 Then
                    Return $"Error: Screenshot returned only {pngBytes.Length} bytes — device may not be connected or screen may be off."
                End If

                ' Load to get dimensions (and optionally crop)
                Using bmp As Bitmap = DirectCast(Image.FromStream(New MemoryStream(pngBytes)), Bitmap)
                    Dim screenW = bmp.Width
                    Dim screenH = bmp.Height

                    If cropW > 0 AndAlso cropH > 0 Then
                        ' Clamp crop rect to image bounds
                        Dim cw = Math.Min(cropW, screenW - cropX)
                        Dim ch = Math.Min(cropH, screenH - cropY)
                        Dim cropRect As New Rectangle(cropX, cropY, cw, ch)

                        Using cropped As New Bitmap(cw, ch)
                            Using g = Graphics.FromImage(cropped)
                                g.DrawImage(bmp, New Rectangle(0, 0, cw, ch), cropRect, GraphicsUnit.Pixel)
                            End Using
                            cropped.Save(outputPath, ImageFormat.Png)
                        End Using

                        Return JsonConvert.SerializeObject(New With {
                            .path = outputPath,
                            .screenSize = $"{screenW}x{screenH}",
                            .crop = $"({cropX},{cropY}) {cw}x{ch}",
                            .savedBytes = New FileInfo(outputPath).Length
                        }, Formatting.Indented)
                    Else
                        File.WriteAllBytes(outputPath, pngBytes)
                        Return JsonConvert.SerializeObject(New With {
                            .path = outputPath,
                            .size = $"{screenW}x{screenH}",
                            .savedBytes = pngBytes.Length
                        }, Formatting.Indented)
                    End If
                End Using

            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

        ' ── Tap ──────────────────────────────────────────────────────────────────

        <McpServerTool, Description(
            "Sends a tap (touch) event to the Android device at the given screen coordinates.")>
        Public Shared Async Function B4aTap(
            <Description("X coordinate in device pixels")> x As Integer,
            <Description("Y coordinate in device pixels")> y As Integer,
            <Description("ADB device serial (optional)")> Optional deviceSerial As String = ""
        ) As Task(Of String)
            Return Await RunAdbShell($"input tap {x} {y}", deviceSerial, $"Tapped ({x},{y})")
        End Function

        ' ── Swipe ────────────────────────────────────────────────────────────────

        <McpServerTool, Description(
            "Sends a swipe gesture to the Android device from (x1,y1) to (x2,y2).")>
        Public Shared Async Function B4aSwipe(
            <Description("Start X coordinate")> x1 As Integer,
            <Description("Start Y coordinate")> y1 As Integer,
            <Description("End X coordinate")> x2 As Integer,
            <Description("End Y coordinate")> y2 As Integer,
            <Description("Duration in milliseconds (default 300)")> Optional durationMs As Integer = 300,
            <Description("ADB device serial (optional)")> Optional deviceSerial As String = ""
        ) As Task(Of String)
            Return Await RunAdbShell($"input swipe {x1} {y1} {x2} {y2} {durationMs}", deviceSerial, $"Swiped ({x1},{y1})→({x2},{y2}) {durationMs}ms")
        End Function

        ' ── Launch App ───────────────────────────────────────────────────────────

        <McpServerTool, Description(
            "Launches an installed app on the Android device using 'adb shell am start'.")>
        Public Shared Async Function B4aLaunchApp(
            <Description("App package name (e.g. b4a.purrfense)")> packageName As String,
            <Description("Activity class name (default: .main). Include the dot prefix for relative names.")>
            Optional activity As String = ".main",
            <Description("ADB device serial (optional)")> Optional deviceSerial As String = ""
        ) As Task(Of String)
            If Not activity.StartsWith(".") AndAlso Not activity.Contains(".") Then
                activity = "." & activity
            End If
            Dim component = $"{packageName}/{activity}"
            Return Await RunAdbShell($"am start -n {component}", deviceSerial, $"Launched {component}")
        End Function

        ' ── Pixel Scan ───────────────────────────────────────────────────────────

        <McpServerTool, Description(
            "Reads pixel RGB values from a PNG screenshot file. Replaces PIL/Python pixel measurement. " &
            "Two modes:" & vbLf &
            "  points: space-separated 'x,y' pairs — e.g. '540,960 100,200 80,730'" & vbLf &
            "  region: 'x y width height stepPx' — samples a grid within the region, e.g. '0 600 1080 400 30'." & vbLf &
            "Both can be used together in the same call.")>
        Public Shared Function B4aPixelScan(
            <Description("Path to the PNG screenshot file (use the path returned by b4a_screenshot)")>
            imagePath As String,
            <Description("Space-separated 'x,y' coordinate pairs to sample. E.g. '540,960 100,200'")>
            Optional points As String = "",
            <Description("Region scan: 'x y width height stepPx'. E.g. '0 600 1080 400 30' samples every 30px.")>
            Optional region As String = ""
        ) As String
            If Not File.Exists(imagePath) Then Return $"Error: File not found: {imagePath}"

            Try
                ' Load without locking the file
                Using bmp As Bitmap = DirectCast(Image.FromStream(New MemoryStream(File.ReadAllBytes(imagePath))), Bitmap)
                    Dim results As New List(Of Object)()

                    ' Individual coordinate points
                    If Not String.IsNullOrWhiteSpace(points) Then
                        For Each pt In points.Trim().Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)
                            Dim parts = pt.Split(","c)
                            If parts.Length < 2 Then Continue For
                            Dim px = Integer.Parse(parts(0).Trim())
                            Dim py = Integer.Parse(parts(1).Trim())
                            If px < 0 OrElse px >= bmp.Width OrElse py < 0 OrElse py >= bmp.Height Then
                                results.Add(New With {.x = px, .y = py, .error = "out of bounds"})
                                Continue For
                            End If
                            Dim c = bmp.GetPixel(px, py)
                            results.Add(New With {
                                .x = px, .y = py,
                                .r = CInt(c.R), .g = CInt(c.G), .b = CInt(c.B),
                                .hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}"
                            })
                        Next
                    End If

                    ' Region grid scan
                    If Not String.IsNullOrWhiteSpace(region) Then
                        Dim parts = region.Trim().Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)
                        If parts.Length >= 4 Then
                            Dim rx = Integer.Parse(parts(0))
                            Dim ry = Integer.Parse(parts(1))
                            Dim rw = Integer.Parse(parts(2))
                            Dim rh = Integer.Parse(parts(3))
                            Dim stepPx = If(parts.Length >= 5, Integer.Parse(parts(4)), 20)
                            Dim maxX = Math.Min(rx + rw, bmp.Width)
                            Dim maxY = Math.Min(ry + rh, bmp.Height)
                            Dim y = ry
                            Do While y < maxY
                                Dim x = rx
                                Do While x < maxX
                                    Dim c = bmp.GetPixel(x, y)
                                    results.Add(New With {
                                        .x = x, .y = y,
                                        .r = CInt(c.R), .g = CInt(c.G), .b = CInt(c.B),
                                        .hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}"
                                    })
                                    x += stepPx
                                Loop
                                y += stepPx
                            Loop
                        End If
                    End If

                    Return JsonConvert.SerializeObject(New With {
                        .imageSize = $"{bmp.Width}x{bmp.Height}",
                        .samples = results.Count,
                        .pixels = results
                    }, Formatting.Indented)
                End Using

            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

        ' ── Key Event ────────────────────────────────────────────────────────────

        <McpServerTool, Description(
            "Sends a key event to the Android device via 'adb shell input keyevent'. " &
            "Common codes: 4=BACK, 3=HOME, 82=MENU, 66=ENTER, 67=DEL, 24=VOL_UP, 25=VOL_DOWN, 26=POWER, 187=RECENTS.")>
        Public Shared Async Function B4aKeyEvent(
            <Description("Android KeyEvent code (integer). E.g. 4 for BACK, 3 for HOME.")>
            keyCode As Integer,
            <Description("ADB device serial (optional)")> Optional deviceSerial As String = ""
        ) As Task(Of String)
            Return Await RunAdbShell($"input keyevent {keyCode}", deviceSerial, $"KeyEvent {keyCode} sent")
        End Function

        ' ── Input Text ───────────────────────────────────────────────────────────

        <McpServerTool, Description(
            "Types text into the focused input field on the Android device via 'adb shell input text'. " &
            "Spaces must be passed as %s (ADB limitation). Special chars may need escaping.")>
        Public Shared Async Function B4aInputText(
            <Description("Text to type. Use %s for spaces.")>
            text As String,
            <Description("ADB device serial (optional)")> Optional deviceSerial As String = ""
        ) As Task(Of String)
            If String.IsNullOrEmpty(text) Then Return "Error: text is empty"
            Return Await RunAdbShell($"input text ""{text}""", deviceSerial, $"Text sent: {text}")
        End Function

        ' ── Helpers ──────────────────────────────────────────────────────────────

        Private Shared Async Function RunAdbShell(shellCmd As String, deviceSerial As String, successMsg As String) As Task(Of String)
            Try
                Dim adbPath = FindAdb()
                If adbPath Is Nothing Then Return "Error: adb not found."

                Dim deviceArg = If(Not String.IsNullOrEmpty(deviceSerial), $"-s {deviceSerial} ", "")
                Dim psi As New ProcessStartInfo() With {
                    .FileName = adbPath,
                    .Arguments = $"{deviceArg}shell {shellCmd}",
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
                    Await Task.Run(Sub() proc.WaitForExit(10_000))
                End Using

                Dim out = output.ToString().Trim()
                Return If(String.IsNullOrEmpty(out), successMsg, out)
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

        Private Shared Function FindAdb() As String
            Dim cfg = AppConfig.Load()
            If Not String.IsNullOrEmpty(cfg.AdbPath) Then
                If File.Exists(cfg.AdbPath) Then Return cfg.AdbPath
                Dim adbExe = Path.Combine(cfg.AdbPath, "adb.exe")
                If File.Exists(adbExe) Then Return adbExe
            End If
            Dim pathEnv = If(Environment.GetEnvironmentVariable("PATH"), "")
            For Each pathDir In pathEnv.Split(";"c)
                Dim candidate = Path.Combine(pathDir.Trim(), "adb.exe")
                If File.Exists(candidate) Then Return candidate
            Next
            Dim localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            Dim sdkPath = Path.Combine(localAppData, "Android", "Sdk", "platform-tools", "adb.exe")
            If File.Exists(sdkPath) Then Return sdkPath
            Return Nothing
        End Function

    End Class
End Namespace
