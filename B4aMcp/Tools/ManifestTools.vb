Imports ModelContextProtocol.Server
Imports System.ComponentModel
Imports System.IO
Imports B4aMcp.Utils

Namespace Tools
    <McpServerToolType>
    Public Class ManifestTools

        <McpServerTool, Description("Extracts the Manifest Editor block from a B4A project file")>
        Public Shared Function B4aReadManifest(
            <Description("Full path to the .b4a project file")> projectPath As String
        ) As String
            If Not File.Exists(projectPath) Then Return $"Error: File not found: {projectPath}"
            If Not projectPath.EndsWith(".b4a", StringComparison.OrdinalIgnoreCase) Then
                Return "Error: File must have .b4a extension"
            End If
            Try
                Dim block = B4aParser.GetManifestBlock(projectPath)
                If String.IsNullOrWhiteSpace(block) Then
                    Return "No Manifest Editor block found in this project."
                End If
                Return block
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Replaces the Manifest Editor block in a B4A project file. Creates .bak backup first.")>
        Public Shared Function B4aWriteManifest(
            <Description("Full path to the .b4a project file")> projectPath As String,
            <Description("New content for the Manifest Editor block (lines between #Region Manifest Editor and #End Region)")> manifestContent As String
        ) As String
            If Not File.Exists(projectPath) Then Return $"Error: File not found: {projectPath}"
            If Not projectPath.EndsWith(".b4a", StringComparison.OrdinalIgnoreCase) Then
                Return "Error: File must have .b4a extension"
            End If
            Try
                B4aParser.WriteManifestBlock(projectPath, manifestContent)
                Return $"OK: backup saved as {projectPath}.bak"
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

    End Class
End Namespace
