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

        <McpServerTool, Description("Returns a list of critical B4A language gotchas and pitfalls that frequently cause hard-to-debug bugs. Call this when starting work on a B4A project or when encountering unexpected behavior.")>
        Public Shared Function B4aLanguageGotchas() As String
            Dim gotchas As New List(Of Object) From {
                New With {
                    .title = "B4A is completely case-insensitive",
                    .severity = "CRITICAL",
                    .description = "Variable names differing only in capitalization are THE SAME variable. A local Dim with the same name as a module global (even different case) overwrites the global.",
                    .example = "In DataModule, 'Dim towerList As List' collides with module global 'TowerList'. Calling towerList.Initialize destroys TowerList content.",
                    .fix = "Always use clearly distinct names for local variables vs module globals. E.g. use 'midTowers' instead of 'towerList' when 'TowerList' exists as a global."
                },
                New With {
                    .title = "Application_Error returning True suppresses all exceptions",
                    .severity = "CRITICAL",
                    .description = "If Application_Error (in Starter.bas) returns True, ALL runtime exceptions are silently swallowed. Bugs become invisible.",
                    .example = "NullPointerException in album render loop never shows — Application_Error eats it.",
                    .fix = "During debugging, temporarily set Application_Error to return False (or log the error before returning True)."
                },
                New With {
                    .title = "File.Exists does not work with DirAssets",
                    .severity = "HIGH",
                    .description = "File.Exists(File.DirAssets, filename) always returns False in B4A. Assets are bundled in the APK and cannot be stat'd.",
                    .example = "Checking if a sprite PNG exists in DirAssets will always fail.",
                    .fix = "Use Try-Catch when loading assets, or maintain a hardcoded list of known asset names."
                },
                New With {
                    .title = "SrcRect/DestRect swap does not flip sprites",
                    .severity = "HIGH",
                    .description = "Inverting SrcRect coordinates (x2 < x1) to mirror a bitmap makes the sprite invisible in B4A — it does not flip it.",
                    .example = "Drawing a sprite with SrcRect(width, 0, 0, height) renders nothing.",
                    .fix = "Pre-flip sprites pixel-by-pixel at load time and store as separate bitmaps (e.g. RatSpritesFlipped map)."
                },
                New With {
                    .title = "MediaPlayer causes NullPointerException at compile time",
                    .severity = "HIGH",
                    .description = "Using MediaPlayer in B4A causes a NullPointer during compilation in some project configurations.",
                    .example = "Any AudioModule that instantiates MediaPlayer breaks the build.",
                    .fix = "Use stub methods (empty Subs) for audio, or use SoundPool instead."
                },
                New With {
                    .title = "Reserved keywords cannot be used as variable/sub names",
                    .severity = "MEDIUM",
                    .description = "B4A has keywords that look like valid identifiers but are reserved: 'Is', 'ATan2', 'Rnd'.",
                    .example = "Sub IsReady() or Dim IsActive As Boolean causes compile errors. 'Rnd' as variable name conflicts with the built-in random function.",
                    .fix = "Avoid: Is*, ATan2, Rnd as sub or variable names. Use alternatives like IsOk→ Ready, RndVal, etc."
                },
                New With {
                    .title = "Parameter name must not match a module Global",
                    .severity = "MEDIUM",
                    .description = "If a Sub parameter has the same name as a module-level global (e.g. in Main), it causes unexpected shadowing or compile errors.",
                    .example = "Sub Foo(gv As GameView) in Main.bas shadows the global 'gv' GameView variable.",
                    .fix = "Use distinct parameter names that don't match any Process_Globals declarations in the same module."
                },
                New With {
                    .title = "Colors.R/G/B/A component extraction does not exist",
                    .severity = "MEDIUM",
                    .description = "B4A does not have Colors.R(), Colors.G(), Colors.B(), Colors.A() functions to extract color components.",
                    .example = "Dim r As Int = Colors.R(someColor) — compile error.",
                    .fix = "Use bit operations: R = Bit.And(Bit.ShiftRight(color, 16), 0xFF), etc."
                },
                New With {
                    .title = "BitmapsData property name (with S)",
                    .severity = "LOW",
                    .description = "The GameView property is 'BitmapsData' (plural, with S), not 'BitmapData'.",
                    .example = "gv.BitmapData.Add(...) → runtime error. Correct: gv.BitmapsData.Add(...)",
                    .fix = "Always use gv.BitmapsData (with S)."
                },
                New With {
                    .title = "KeyValueStore2 method names",
                    .severity = "LOW",
                    .description = "KeyValueStore2 uses Get(key) and Put(key, val) — not GetString, PutString, etc.",
                    .example = "kvs.GetString('key') → error. Correct: kvs.Get('key')",
                    .fix = "Use kvs.Get(key) and kvs.Put(key, value)."
                },
                New With {
                    .title = "Build config first token must be 'Default'",
                    .severity = "LOW",
                    .description = "In the B4A project file, Build1 (and other build configs) must start with 'Default' as the first token.",
                    .example = "Build1=release,b4a.purrfense → build fails. Correct: Build1=Default,b4a.purrfense",
                    .fix = "Ensure Build1=Default,<packagename> in the .b4a project file."
                }
            }
            Return JsonConvert.SerializeObject(New With {
                .count = gotchas.Count,
                .gotchas = gotchas
            }, Formatting.Indented)
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
