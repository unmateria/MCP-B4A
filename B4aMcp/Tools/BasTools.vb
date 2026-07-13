Imports ModelContextProtocol.Server
Imports System.ComponentModel
Imports System.IO

Namespace Tools
    <McpServerToolType>
    Public Class BasTools

        Private Shared Function IsB4aSource(path As String) As Boolean
            Return path.EndsWith(".bas", StringComparison.OrdinalIgnoreCase) OrElse
                   path.EndsWith(".b4a", StringComparison.OrdinalIgnoreCase)
        End Function

        <McpServerTool, Description("Reads a B4A source file (.bas or .b4a) and returns its full content with line numbers")>
        Public Shared Function B4aReadBas(
            <Description("Full path to the .bas or .b4a file")> basPath As String
        ) As String
            If Not File.Exists(basPath) Then Return $"Error: File not found: {basPath}"
            If Not IsB4aSource(basPath) Then
                Return "Error: File must have .bas or .b4a extension"
            End If
            Try
                Dim lines = File.ReadAllLines(basPath)
                Dim sb As New Text.StringBuilder()
                For i = 0 To lines.Length - 1
                    sb.AppendLine($"{i + 1,5}| {lines(i)}")
                Next
                Return sb.ToString()
            Catch ex As Exception
                Return $"Error reading file: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Performs a search-and-replace edit on a B4A source file (.bas or .b4a). Creates .bak backup first. The old_text must match exactly (including indentation).")>
        Public Shared Function B4aEditBas(
            <Description("Full path to the .bas or .b4a file")> basPath As String,
            <Description("Exact text to find (must match including whitespace/indentation)")> old_text As String,
            <Description("Replacement text")> new_text As String,
            <Description("If true, replace ALL occurrences; if false (default), the old_text must be unique")> Optional replace_all As Boolean = False
        ) As String
            If Not File.Exists(basPath) Then Return $"Error: File not found: {basPath}"
            If Not IsB4aSource(basPath) Then
                Return "Error: File must have .bas or .b4a extension"
            End If
            If String.IsNullOrEmpty(old_text) Then Return "Error: old_text cannot be empty"
            If old_text = new_text Then Return "Error: old_text and new_text are identical"

            Try
                Dim content = File.ReadAllText(basPath)

                ' Normalise line endings for matching
                Dim normContent = content.Replace(vbCrLf, vbLf)
                Dim normOld = old_text.Replace(vbCrLf, vbLf)
                Dim normNew = new_text.Replace(vbCrLf, vbLf)

                Dim count = CountOccurrences(normContent, normOld)

                If count = 0 Then
                    Return "Error: old_text not found in file. Make sure whitespace and indentation match exactly."
                End If

                If Not replace_all AndAlso count > 1 Then
                    Return $"Error: old_text found {count} times. Provide more context to make it unique, or set replace_all=true."
                End If

                ' Create backup
                File.Copy(basPath, basPath & ".bak", overwrite:=True)

                ' Apply replacement (preserve original line endings)
                Dim result As String
                If replace_all Then
                    result = normContent.Replace(normOld, normNew)
                Else
                    Dim idx = normContent.IndexOf(normOld, StringComparison.Ordinal)
                    result = normContent.Substring(0, idx) & normNew & normContent.Substring(idx + normOld.Length)
                End If

                ' Restore CRLF if original used it
                If content.Contains(vbCrLf) Then
                    result = result.Replace(vbLf, vbCrLf)
                End If

                File.WriteAllText(basPath, result)

                Dim replacements = If(replace_all, count, 1)
                Return $"OK: {replacements} replacement(s) made. Backup saved as {basPath}.bak"
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

        Private Shared Function CountOccurrences(source As String, search As String) As Integer
            Dim count = 0
            Dim idx = 0
            Do
                idx = source.IndexOf(search, idx, StringComparison.Ordinal)
                If idx < 0 Then Exit Do
                count += 1
                idx += search.Length
            Loop
            Return count
        End Function

    End Class
End Namespace
