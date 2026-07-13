Imports System.IO
Imports System.IO.Compression
Imports System.Text
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Namespace Utils
    ''' <summary>
    ''' Ports BalConverter.bas (B4J) to VB.NET.
    ''' Converts B4A binary layout files (.bal/.bil) to/from JSON.
    ''' All binary I/O is little-endian (matching B4J ByteConverter.LittleEndian=True on x86/x64).
    ''' </summary>
    Public Class BalConverter
        ' Type codes — same as in BalConverter.bas (renamed to avoid VB.NET built-in conflicts)
        Private Const TYPE_INT As Byte = 1
        Private Const TYPE_STR As Byte = 2
        Private Const TYPE_MAP As Byte = 3
        Private Const TYPE_EOM As Byte = 4    ' ENDOFMAP
        Private Const TYPE_BOOL As Byte = 5
        Private Const TYPE_COLOR As Byte = 6
        Private Const TYPE_FLOAT As Byte = 7
        Private Const TYPE_CACHED As Byte = 9  ' CACHED_STRING
        Private Const TYPE_RECT As Byte = 11   ' RECT32
        Private Const TYPE_NULL As Byte = 12   ' CNULL

        Private ReadOnly _toBIL As Boolean

        Public Sub New(toBIL As Boolean)
            _toBIL = toBIL
        End Sub

        ' ── Public API ──────────────────────────────────────────────────────────

        Public Function ConvertBalToJson(dir As String, fileName As String) As String
            Using stream = File.OpenRead(Path.Combine(dir, fileName))
                Dim jobj = ConvertBalToJsonInMemory(stream)
                Return jobj.ToString(Formatting.Indented)
            End Using
        End Function

        Public Function ConvertBalToJsonInMemory(stream As Stream) As JObject
            Using reader = New BinaryReader(stream, Encoding.UTF8, leaveOpen:=True)
                Dim lh = ReadLayoutHeader(reader)
                Dim version = lh("Version").Value(Of Integer)()
                If version < 3 Then
                    Throw New InvalidDataException($"Unsupported .bal version: {version}")
                End If

                Dim cache = LoadStringsCache(reader)
                Dim numberOfVariants = reader.ReadInt32()
                Dim variants As New JArray()
                For i = 0 To numberOfVariants - 1
                    variants.Add(ReadVariantFromStream(reader))
                Next

                Dim data = ReadMap(reader, cache)
                reader.ReadInt32() ' trailing 0

                Dim fontAwesome = reader.ReadSByte() = 1
                Dim materialIcons = reader.ReadSByte() = 1

                Dim result As New JObject()
                result.Add("LayoutHeader", lh)
                result.Add("Variants", variants)
                result.Add("Data", data)
                result.Add("FontAwesome", New JValue(fontAwesome))
                result.Add("MaterialIcons", New JValue(materialIcons))
                Return result
            End Using
        End Function

        Public Sub ConvertJsonToBal(dir As String, jsonFileName As String)
            Dim bfile = jsonFileName.Substring(0, jsonFileName.Length - 5) ' remove .json
            Dim fullBfile = Path.Combine(dir, bfile)
            If File.Exists(fullBfile) Then
                File.Copy(fullBfile, fullBfile & ".bak", overwrite:=True)
            End If
            Dim json = JObject.Parse(File.ReadAllText(Path.Combine(dir, jsonFileName)))
            Using stream = File.Create(fullBfile)
                ConvertJsonToBalInMemory(json, stream)
            End Using
        End Sub

        Public Sub ConvertJsonToBalInMemory(json As JObject, stream As Stream)
            Using writer = New BinaryWriter(stream, Encoding.UTF8, leaveOpen:=True)
                Dim variants = DirectCast(json("Variants"), JArray)
                WriteLayoutHeader(DirectCast(json("LayoutHeader"), JObject), writer, variants)
                WriteAllLayout(writer, variants, DirectCast(json("Data"), JObject))

                For Each fnt In {"FontAwesome", "MaterialIcons"}
                    Dim tok = json(fnt)
                    Dim b As Byte = If(tok IsNot Nothing AndAlso tok.Value(Of Boolean)(), CByte(1), CByte(0))
                    writer.Write(b)
                Next
            End Using
        End Sub

        ' ── Read Helpers ─────────────────────────────────────────────────────────

        Private Function ReadLayoutHeader(reader As BinaryReader) As JObject
            Dim data As New JObject()
            Dim version = reader.ReadInt32()
            data.Add("Version", New JValue(version))
            If version < 3 Then Return data

            reader.ReadInt32() ' skip size stub

            Dim gridSize = If(version >= 4, reader.ReadInt32(), 10)
            data.Add("GridSize", New JValue(gridSize))

            Dim cache = LoadStringsCache(reader)
            Dim numberOfControls = reader.ReadInt32()
            Dim controls As New JArray()
            For i = 0 To numberOfControls - 1
                Dim ctrl As New JObject()
                ctrl.Add("Name", New JValue(ReadCachedString(reader, cache)))
                ctrl.Add("JavaType", New JValue(ReadCachedString(reader, cache)))
                ctrl.Add("DesignerType", New JValue(ReadCachedString(reader, cache)))
                controls.Add(ctrl)
            Next
            data.Add("ControlsHeaders", controls)

            Dim numberOfFiles = reader.ReadInt32()
            Dim files As New JArray()
            For i = 0 To numberOfFiles - 1
                files.Add(New JValue(ReadString(reader)))
            Next
            data.Add("Files", files)

            data.Add("DesignerScript", ReadScripts(reader))
            Return data
        End Function

        Private Function ReadScripts(reader As BinaryReader) As JArray
            Dim rawLen = reader.ReadInt32()
            Dim rawData = reader.ReadBytes(rawLen)
            Dim decompressed = Decompress(rawData)

            Using ms = New MemoryStream(decompressed)
                Using scriptReader = New BinaryReader(ms, Encoding.UTF8)
                    Dim res As New JArray()
                    res.Add(New JValue(ReadBinaryString(scriptReader))) ' general script

                    Dim numberOfVariants = scriptReader.ReadInt32()
                    For i = 0 To numberOfVariants - 1
                        ReadVariantFromStream(scriptReader) ' consumed but not added here
                        res.Add(New JValue(ReadBinaryString(scriptReader)))
                    Next
                    Return res
                End Using
            End Using
        End Function

        Private Shared Function ReadBinaryString(reader As BinaryReader) As String
            ' Variable-length encoding: 7 bits per byte, high bit = more bytes follow
            Dim length = 0
            Dim shift = 0
            Do
                Dim b = reader.ReadSByte()
                Dim value = b And &H7F
                length = length Or (value << shift)
                If b = value Then Exit Do ' high bit not set = last byte
                shift += 7
            Loop
            Dim bytes = reader.ReadBytes(length)
            Return Encoding.UTF8.GetString(bytes)
        End Function

        Private Shared Function ReadVariantFromStream(reader As BinaryReader) As JObject
            Dim v As New JObject()
            v.Add("Scale", New JValue(reader.ReadSingle()))
            v.Add("Width", New JValue(reader.ReadInt32()))
            v.Add("Height", New JValue(reader.ReadInt32()))
            Return v
        End Function

        Private Shared Function LoadStringsCache(reader As BinaryReader) As String()
            Dim count = reader.ReadInt32()
            Dim cache(count - 1) As String
            For i = 0 To count - 1
                cache(i) = ReadString(reader)
            Next
            Return cache
        End Function

        Private Shared Function ReadCachedString(reader As BinaryReader, cache As String()) As String
            If cache.Length = 0 Then Return ReadString(reader)
            Return cache(reader.ReadInt32())
        End Function

        Private Shared Function ReadString(reader As BinaryReader) As String
            Dim len = reader.ReadInt32()
            Dim bytes = reader.ReadBytes(len)
            Return Encoding.UTF8.GetString(bytes)
        End Function

        Private Function ReadMap(reader As BinaryReader, cache As String()) As JObject
            Dim props As New JObject()
            Do
                Dim key = ReadCachedString(reader, cache)
                Dim typeCode = reader.ReadSByte()

                Select Case typeCode
                    Case TYPE_EOM
                        Exit Do
                    Case TYPE_INT
                        props.Add(key, New JValue(reader.ReadInt32()))
                    Case TYPE_CACHED
                        props.Add(key, New JValue(ReadCachedString(reader, cache)))
                    Case TYPE_STR
                        Dim entry As New JObject()
                        entry.Add("ValueType", New JValue(TYPE_STR))
                        entry.Add("Value", New JValue(ReadString(reader)))
                        props.Add(key, entry)
                    Case TYPE_FLOAT
                        Dim entry As New JObject()
                        entry.Add("ValueType", New JValue(TYPE_FLOAT))
                        entry.Add("Value", New JValue(reader.ReadSingle()))
                        props.Add(key, entry)
                    Case TYPE_MAP
                        props.Add(key, ReadMap(reader, cache))
                    Case TYPE_BOOL
                        props.Add(key, New JValue(reader.ReadSByte() = 1))
                    Case TYPE_COLOR
                        Dim bytes = reader.ReadBytes(4)
                        Dim entry As New JObject()
                        entry.Add("ValueType", New JValue(TYPE_COLOR))
                        entry.Add("Value", New JValue("0x" & Convert.ToHexString(bytes)))
                        props.Add(key, entry)
                    Case TYPE_NULL
                        Dim entry As New JObject()
                        entry.Add("ValueType", New JValue(TYPE_NULL))
                        props.Add(key, entry)
                    Case TYPE_RECT
                        Dim bytes = reader.ReadBytes(8)
                        Dim shorts As New JArray()
                        For i = 0 To 3
                            shorts.Add(New JValue(BitConverter.ToInt16(bytes, i * 2)))
                        Next
                        Dim entry As New JObject()
                        entry.Add("ValueType", New JValue(TYPE_RECT))
                        entry.Add("Value", shorts)
                        props.Add(key, entry)
                    Case Else
                        Throw New InvalidDataException($"Unknown type code {typeCode} for key '{key}'")
                End Select
            Loop
            Return props
        End Function

        ' ── Write Helpers ────────────────────────────────────────────────────────

        Private Sub WriteLayoutHeader(header As JObject, writer As BinaryWriter, variants As JArray)
            Dim version = header("Version").Value(Of Integer)()
            writer.Write(version)
            Dim stubPos = writer.BaseStream.Position
            writer.Write(0) ' stub — filled in after

            If version >= 4 Then
                writer.Write(header("GridSize").Value(Of Integer)())
            End If

            ' Write control headers via temp buffer to build the string cache
            Dim cache As New Dictionary(Of String, Integer)()
            Using temp = New MemoryStream()
                Using tempW = New BinaryWriter(temp, Encoding.UTF8, leaveOpen:=True)
                    Dim controls = DirectCast(header("ControlsHeaders"), JArray)
                    tempW.Write(controls.Count)
                    For Each c As JObject In controls
                        WriteCachedString(tempW, cache, c("Name").ToString())
                        WriteCachedString(tempW, cache, c("JavaType").ToString())
                        WriteCachedString(tempW, cache, c("DesignerType").ToString())
                    Next

                    WriteStringsCache(writer, cache)
                    writer.Write(temp.ToArray())
                End Using
            End Using

            Dim files = DirectCast(header("Files"), JArray)
            writer.Write(files.Count)
            For Each f As JToken In files
                WriteString(writer, f.ToString())
            Next

            Dim scripts = DirectCast(header("DesignerScript"), JArray)
            Dim scriptBytes = WriteScripts(scripts, variants)
            writer.Write(scriptBytes.Length)
            writer.Write(scriptBytes)

            ' Fill in the stub with (current position - stubPos - 4)
            Dim endPos = writer.BaseStream.Position
            writer.BaseStream.Seek(stubPos, SeekOrigin.Begin)
            writer.Write(CInt(endPos - stubPos - 4))
            writer.BaseStream.Seek(endPos, SeekOrigin.Begin)
        End Sub

        Private Shared Function WriteScripts(scripts As JArray, variants As JArray) As Byte()
            Using ms = New MemoryStream()
                Using w = New BinaryWriter(ms, Encoding.UTF8, leaveOpen:=True)
                    Dim scriptList = scripts.Select(Function(t) t.ToString()).ToList()
                    WriteBinaryString(w, scriptList(0))
                    w.Write(variants.Count)
                    For i = 0 To variants.Count - 1
                        Dim v = DirectCast(variants(i), JObject)
                        w.Write(v("Scale").Value(Of Single)())
                        w.Write(v("Width").Value(Of Integer)())
                        w.Write(v("Height").Value(Of Integer)())
                        Dim scriptText = If(i + 1 < scriptList.Count, scriptList(i + 1), "")
                        WriteBinaryString(w, scriptText)
                    Next
                    Return Compress(ms.ToArray())
                End Using
            End Using
        End Function

        Private Shared Sub WriteBinaryString(writer As BinaryWriter, s As String)
            Dim length = Encoding.UTF8.GetByteCount(s)
            Dim remaining = length
            Do
                Dim b = CByte(remaining And &H7F)
                remaining = remaining >> 7
                If remaining > 0 Then b = b Or &H80
                writer.Write(b)
            Loop While remaining > 0
            writer.Write(Encoding.UTF8.GetBytes(s))
        End Sub

        Private Sub WriteAllLayout(writer As BinaryWriter, variants As JArray, data As JObject)
            Dim cache As New Dictionary(Of String, Integer)()
            Using temp = New MemoryStream()
                Using tempW = New BinaryWriter(temp, Encoding.UTF8, leaveOpen:=True)
                    tempW.Write(variants.Count)
                    For Each v As JObject In variants
                        tempW.Write(v("Scale").Value(Of Single)())
                        tempW.Write(v("Width").Value(Of Integer)())
                        tempW.Write(v("Height").Value(Of Integer)())
                    Next
                    WriteMap(tempW, data, cache)
                    WriteString(tempW, "")
                    tempW.Write(TYPE_EOM)

                    WriteStringsCache(writer, cache)
                    writer.Write(temp.ToArray())
                End Using
            End Using
            writer.Write(0) ' trailing int 0
        End Sub

        Private Sub WriteMap(writer As BinaryWriter, m As JObject, cache As Dictionary(Of String, Integer))
            For Each prop As KeyValuePair(Of String, JToken) In m
                Dim key = prop.Key
                Dim val = prop.Value

                ' When writing .bil, skip TYPE_NULL and TYPE_RECT entries
                If _toBIL AndAlso val IsNot Nothing AndAlso val.Type = JTokenType.Object Then
                    Dim mval = DirectCast(val, JObject)
                    If mval.ContainsKey("ValueType") Then
                        Dim bt = mval("ValueType").Value(Of Byte)()
                        If bt = TYPE_NULL OrElse bt = TYPE_RECT Then Continue For
                    End If
                End If

                WriteCachedString(writer, cache, key)

                If val Is Nothing OrElse val.Type = JTokenType.Null Then
                    writer.Write(TYPE_NULL)
                    Continue For
                End If

                Select Case val.Type
                    Case JTokenType.Integer
                        writer.Write(TYPE_INT)
                        writer.Write(val.Value(Of Integer)())

                    Case JTokenType.String
                        writer.Write(TYPE_CACHED)
                        WriteCachedString(writer, cache, val.ToString())

                    Case JTokenType.Boolean
                        writer.Write(TYPE_BOOL)
                        writer.Write(If(val.Value(Of Boolean)(), CByte(1), CByte(0)))

                    Case JTokenType.Object
                        Dim mval = DirectCast(val, JObject)
                        If mval.ContainsKey("ValueType") Then
                            Dim bt = mval("ValueType").Value(Of Byte)()
                            writer.Write(bt)
                            Select Case bt
                                Case TYPE_STR
                                    WriteString(writer, mval("Value").ToString())
                                Case TYPE_FLOAT
                                    writer.Write(mval("Value").Value(Of Single)())
                                Case TYPE_COLOR
                                    Dim hex = mval("Value").ToString()
                                    If hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) Then hex = hex.Substring(2)
                                    writer.Write(Convert.FromHexString(hex))
                                Case TYPE_RECT
                                    Dim shorts = DirectCast(mval("Value"), JArray)
                                    For i = 0 To 3
                                        writer.Write(CShort(shorts(i).Value(Of Integer)()))
                                    Next
                                Case TYPE_NULL
                                    ' nothing to write
                            End Select
                        Else
                            ' Nested map
                            writer.Write(TYPE_MAP)
                            WriteMap(writer, mval, cache)
                            WriteString(writer, "")
                            writer.Write(TYPE_EOM)
                        End If

                    Case Else
                        Throw New InvalidDataException($"Unexpected JSON token type {val.Type} for key '{key}'")
                End Select
            Next
        End Sub

        Private Shared Sub WriteStringsCache(writer As BinaryWriter, cache As Dictionary(Of String, Integer))
            writer.Write(cache.Count)
            For Each s In cache.Keys
                WriteString(writer, s)
            Next
        End Sub

        Private Shared Sub WriteCachedString(writer As BinaryWriter, cache As Dictionary(Of String, Integer), s As String)
            If Not cache.ContainsKey(s) Then
                cache(s) = cache.Count
            End If
            writer.Write(cache(s))
        End Sub

        Private Shared Sub WriteString(writer As BinaryWriter, s As String)
            Dim bytes = Encoding.UTF8.GetBytes(s)
            writer.Write(bytes.Length)
            writer.Write(bytes)
        End Sub

        ' ── GZip Helpers ─────────────────────────────────────────────────────────

        Private Shared Function Decompress(data As Byte()) As Byte()
            Using input = New MemoryStream(data)
                Using gz = New GZipStream(input, CompressionMode.Decompress)
                    Using output = New MemoryStream()
                        gz.CopyTo(output)
                        Return output.ToArray()
                    End Using
                End Using
            End Using
        End Function

        Private Shared Function Compress(data As Byte()) As Byte()
            Using output = New MemoryStream()
                Using gz = New GZipStream(output, CompressionLevel.Optimal, leaveOpen:=True)
                    gz.Write(data, 0, data.Length)
                End Using
                Return output.ToArray()
            End Using
        End Function
    End Class
End Namespace
