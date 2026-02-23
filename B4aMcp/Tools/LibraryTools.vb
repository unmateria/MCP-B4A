Imports ModelContextProtocol.Server
Imports System.ComponentModel
Imports System.IO
Imports System.Xml.Linq
Imports Newtonsoft.Json
Imports B4aMcp.Utils

Namespace Tools
    <McpServerToolType>
    Public Class LibraryTools

        <McpServerTool, Description("Lists all available B4A libraries (.jar + .xml pairs) from the B4A installation and additional libraries path")>
        Public Shared Function B4aListLibraries(
            <Description("Include built-in libraries from b4aPath. Default true.")> Optional includeBuiltIn As Boolean = True
        ) As String
            Try
                Dim cfg = AppConfig.Load()
                Dim cacheKey = $"libs:list:{includeBuiltIn}:{cfg.B4aPath}:{cfg.AdditionalLibrariesPath}"
                Dim cached As String = Nothing
                If CacheManager.TryGetByTtl(Of String)(cacheKey, cached) Then Return cached

                Dim dirs As New List(Of String)()
                If includeBuiltIn AndAlso Not String.IsNullOrEmpty(cfg.B4aPath) Then
                    Dim libDir = Path.Combine(cfg.B4aPath, "Libraries")
                    If Directory.Exists(libDir) Then dirs.Add(libDir)
                End If
                If Not String.IsNullOrEmpty(cfg.AdditionalLibrariesPath) AndAlso
                   Directory.Exists(cfg.AdditionalLibrariesPath) Then
                    dirs.Add(cfg.AdditionalLibrariesPath)
                End If

                If dirs.Count = 0 Then
                    Return "Error: No library directories configured. Set b4aPath and/or additionalLibrariesPath."
                End If

                Dim libs As New List(Of Object)()
                For Each searchDir In dirs
                    For Each xmlFile In Directory.GetFiles(searchDir, "*.xml")
                        Dim jarFile = Path.ChangeExtension(xmlFile, ".jar")
                        If Not File.Exists(jarFile) Then Continue For
                        Try
                            Dim doc = XDocument.Load(xmlFile)
                            Dim nameEl = doc.Root.Element("name")
                            Dim versionEl = doc.Root.Element("version")
                            Dim libName = If(nameEl IsNot Nothing, nameEl.Value, Path.GetFileNameWithoutExtension(xmlFile))
                            Dim libVersion = If(versionEl IsNot Nothing, versionEl.Value, "?")
                            libs.Add(New With {.name = libName, .version = libVersion, .source = searchDir})
                        Catch
                            libs.Add(New With {
                                .name = Path.GetFileNameWithoutExtension(xmlFile),
                                .version = "?",
                                .source = searchDir
                            })
                        End Try
                    Next
                Next

                Dim result = JsonConvert.SerializeObject(New With {
                    .count = libs.Count,
                    .libraries = libs.OrderBy(Function(l) DirectCast(l, Object).GetType().GetProperty("name").GetValue(l))
                }, Formatting.Indented)
                CacheManager.SetByTtl(cacheKey, result, 60)
                Return result
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Returns the documented methods, properties, and events of a B4A library in compact format")>
        Public Shared Function B4aGetLibraryDocs(
            <Description("Library name (e.g. 'net', 'StringUtils', 'json')")> libraryName As String
        ) As String
            Try
                Dim xmlPath = FindLibraryXml(libraryName)
                If xmlPath Is Nothing Then Return $"Error: Library XML not found for '{libraryName}'"

                Dim cached As String = Nothing
                If CacheManager.TryGetByMtime(Of String)(xmlPath, cached) Then Return cached

                Dim doc = XDocument.Load(xmlPath)
                Dim sb As New System.Text.StringBuilder()

                Dim rootNameEl = doc.Root.Element("name")
                Dim rootVersionEl = doc.Root.Element("version")
                Dim rootName = If(rootNameEl IsNot Nothing, rootNameEl.Value, libraryName)
                Dim rootVersion = If(rootVersionEl IsNot Nothing, rootVersionEl.Value, "?")
                sb.AppendLine($"Library: {rootName} v{rootVersion}")
                sb.AppendLine()

                For Each cls In doc.Root.Elements("class")
                    Dim typeNameAttr = cls.Attribute("typeName")
                    Dim typeName = If(typeNameAttr IsNot Nothing, typeNameAttr.Value, "?")
                    sb.AppendLine($"[{typeName}]")

                    For Each m In cls.Elements("method")
                        Dim mNameAttr = m.Attribute("name")
                        Dim mName = If(mNameAttr IsNot Nothing, mNameAttr.Value, "?")
                        Dim params = String.Join(", ", m.Elements("parameter").Select(
                            Function(p)
                                Dim pName = If(p.Attribute("name") IsNot Nothing, p.Attribute("name").Value, "")
                                Dim pType = ShortType(If(p.Attribute("type") IsNot Nothing, p.Attribute("type").Value, ""))
                                Return $"{pName}: {pType}"
                            End Function))
                        Dim retTypeAttr = m.Attribute("returnType")
                        Dim retType = ShortType(If(retTypeAttr IsNot Nothing, retTypeAttr.Value, ""))
                        Dim commentEl = m.Element("comment")
                        Dim comment = If(commentEl IsNot Nothing, commentEl.Value.Trim(), "")
                        Dim line = $"  .{mName}({params})"
                        If Not String.IsNullOrEmpty(retType) AndAlso retType <> "void" Then line &= $" → {retType}"
                        If Not String.IsNullOrEmpty(comment) Then line &= $" — {TruncateComment(comment)}"
                        sb.AppendLine(line)
                    Next

                    For Each p In cls.Elements("property")
                        Dim pNameAttr = p.Attribute("name")
                        Dim pName = If(pNameAttr IsNot Nothing, pNameAttr.Value, "?")
                        Dim pTypeAttr = p.Attribute("type")
                        Dim pType = ShortType(If(pTypeAttr IsNot Nothing, pTypeAttr.Value, ""))
                        Dim commentEl = p.Element("comment")
                        Dim comment = If(commentEl IsNot Nothing, commentEl.Value.Trim(), "")
                        Dim line = $"  .{pName}: {pType}"
                        If Not String.IsNullOrEmpty(comment) Then line &= $" — {TruncateComment(comment)}"
                        sb.AppendLine(line)
                    Next

                    For Each ev In cls.Elements("event")
                        Dim evNameAttr = ev.Attribute("name")
                        Dim evName = If(evNameAttr IsNot Nothing, evNameAttr.Value, "?")
                        Dim params = String.Join(", ", ev.Elements("parameter").Select(
                            Function(p)
                                Dim pName = If(p.Attribute("name") IsNot Nothing, p.Attribute("name").Value, "")
                                Dim pType = ShortType(If(p.Attribute("type") IsNot Nothing, p.Attribute("type").Value, ""))
                                Return $"{pName}: {pType}"
                            End Function))
                        Dim commentEl = ev.Element("comment")
                        Dim comment = If(commentEl IsNot Nothing, commentEl.Value.Trim(), "")
                        Dim line = $"  [event] {evName}({params})"
                        If Not String.IsNullOrEmpty(comment) Then line &= $" — {TruncateComment(comment)}"
                        sb.AppendLine(line)
                    Next
                    sb.AppendLine()
                Next

                Dim result = sb.ToString()
                CacheManager.SetByMtime(xmlPath, result)
                Return result
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

        <McpServerTool, Description("Searches library documentation for methods, properties, or events matching a query")>
        Public Shared Function B4aSearchLibrary(
            <Description("Search query (method name, keyword, or description text)")> query As String,
            <Description("Optional: limit search to a specific library name")> Optional libraryName As String = ""
        ) As String
            Try
                Dim cfg = AppConfig.Load()
                Dim dirs As New List(Of String)()
                If Not String.IsNullOrEmpty(cfg.B4aPath) Then
                    Dim libDir = Path.Combine(cfg.B4aPath, "Libraries")
                    If Directory.Exists(libDir) Then dirs.Add(libDir)
                End If
                If Not String.IsNullOrEmpty(cfg.AdditionalLibrariesPath) AndAlso
                   Directory.Exists(cfg.AdditionalLibrariesPath) Then
                    dirs.Add(cfg.AdditionalLibrariesPath)
                End If

                If dirs.Count = 0 Then Return "Error: No library directories configured."

                Dim matches As New List(Of Object)()
                Dim queryLower = query.ToLowerInvariant()

                For Each searchDir In dirs
                    For Each xmlFile In Directory.GetFiles(searchDir, "*.xml")
                        Dim libBaseName = Path.GetFileNameWithoutExtension(xmlFile)
                        If Not String.IsNullOrEmpty(libraryName) AndAlso
                           Not libBaseName.Equals(libraryName, StringComparison.OrdinalIgnoreCase) Then
                            Continue For
                        End If
                        If Not File.Exists(Path.ChangeExtension(xmlFile, ".jar")) Then Continue For
                        Try
                            Dim doc = XDocument.Load(xmlFile)
                            Dim nameEl = doc.Root.Element("name")
                            Dim libNameVal = If(nameEl IsNot Nothing, nameEl.Value, libBaseName)
                            For Each cls In doc.Root.Elements("class")
                                Dim typeNameAttr = cls.Attribute("typeName")
                                Dim typeName = If(typeNameAttr IsNot Nothing, typeNameAttr.Value, "")
                                Dim allElems = cls.Elements("method").Concat(cls.Elements("property")).Concat(cls.Elements("event"))
                                For Each elem In allElems
                                    Dim mNameAttr = elem.Attribute("name")
                                    Dim mName = If(mNameAttr IsNot Nothing, mNameAttr.Value, "")
                                    Dim commentEl = elem.Element("comment")
                                    Dim comment = If(commentEl IsNot Nothing, commentEl.Value, "")
                                    If mName.ToLowerInvariant().Contains(queryLower) OrElse
                                       comment.ToLowerInvariant().Contains(queryLower) OrElse
                                       typeName.ToLowerInvariant().Contains(queryLower) Then
                                        matches.Add(New With {
                                            .library = libNameVal,
                                            .typeName = typeName,
                                            .kind = elem.Name.LocalName,
                                            .name = mName,
                                            .description = TruncateComment(comment.Trim())
                                        })
                                    End If
                                Next
                            Next
                        Catch
                            ' Skip malformed XMLs
                        End Try
                    Next
                Next

                Return JsonConvert.SerializeObject(New With {
                    .query = query,
                    .count = matches.Count,
                    .results = matches.Take(50)
                }, Formatting.Indented)
            Catch ex As Exception
                Return $"Error: {ex.Message}"
            End Try
        End Function

        ' ── Helpers ──────────────────────────────────────────────────────────────

        Private Shared Function FindLibraryXml(name As String) As String
            Dim cfg = AppConfig.Load()
            Dim dirs As New List(Of String)()
            If Not String.IsNullOrEmpty(cfg.AdditionalLibrariesPath) AndAlso
               Directory.Exists(cfg.AdditionalLibrariesPath) Then
                dirs.Add(cfg.AdditionalLibrariesPath)
            End If
            If Not String.IsNullOrEmpty(cfg.B4aPath) Then
                Dim libDir = Path.Combine(cfg.B4aPath, "Libraries")
                If Directory.Exists(libDir) Then dirs.Add(libDir)
            End If

            For Each searchDir In dirs
                Dim candidate = Path.Combine(searchDir, name & ".xml")
                If File.Exists(candidate) Then Return candidate
                Dim found = Directory.GetFiles(searchDir, "*.xml").FirstOrDefault(
                    Function(f) Path.GetFileNameWithoutExtension(f).Equals(name, StringComparison.OrdinalIgnoreCase))
                If found IsNot Nothing Then Return found
            Next
            Return Nothing
        End Function

        Private Shared Function ShortType(fullType As String) As String
            If String.IsNullOrEmpty(fullType) Then Return ""
            Dim dot = fullType.LastIndexOf("."c)
            Return If(dot >= 0, fullType.Substring(dot + 1), fullType)
        End Function

        Private Shared Function TruncateComment(comment As String) As String
            If String.IsNullOrEmpty(comment) Then Return ""
            Dim firstLine = comment.Split(New Char() {Chr(10), Chr(13)}).FirstOrDefault()
            If firstLine Is Nothing Then Return ""
            firstLine = firstLine.Trim()
            Return If(firstLine.Length > 100, firstLine.Substring(0, 100) & "…", firstLine)
        End Function

    End Class
End Namespace
