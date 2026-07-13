Imports ModelContextProtocol.Server
Imports System.ComponentModel
Imports Newtonsoft.Json
Imports B4aMcp.Utils

Namespace Tools
    <McpServerToolType>
    Public Class ConfigTools

        <McpServerTool, Description("Returns the current MCP-B4A configuration (paths to B4A, ADB, libraries, etc.)")>
        Public Shared Function B4aGetConfig() As String
            Dim cfg = AppConfig.Load()
            Dim sources = AppConfig.GetSources()
            Dim result = New With {
                .b4aPath = cfg.B4aPath,
                .additionalLibrariesPath = cfg.AdditionalLibrariesPath,
                .adbPath = cfg.AdbPath,
                .projectsRoot = cfg.ProjectsRoot,
                .sharedModulesFolder = cfg.SharedModulesFolder,
                .javaBin = cfg.JavaBin,
                .configFile = AppConfig.GetConfigPath(),
                .b4aIniFile = AppConfig.GetB4aIniPath(),
                .sources = sources
            }
            Dim needsSetup = String.IsNullOrEmpty(cfg.B4aPath)
            If needsSetup Then
                Return JsonConvert.SerializeObject(New With {
                    .config = result,
                    .warning = "b4aPath is not set. Use b4a_set_config to configure. Example: b4a_set_config(key='b4aPath', value='C:\\B4A')"
                }, Formatting.Indented)
            End If
            Return JsonConvert.SerializeObject(result, Formatting.Indented)
        End Function

        <McpServerTool, Description("Updates a configuration value. Valid keys: b4aPath, additionalLibrariesPath, adbPath, projectsRoot, sharedModulesFolder, javaBin")>
        Public Shared Function B4aSetConfig(
            <Description("Configuration key to set (b4aPath, additionalLibrariesPath, adbPath, projectsRoot, sharedModulesFolder, javaBin)")> key As String,
            <Description("New value for the configuration key")> value As String
        ) As String
            Return AppConfig.SetValue(key, value)
        End Function

    End Class
End Namespace
