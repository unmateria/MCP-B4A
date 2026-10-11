Imports System.IO
Imports System.Text

Namespace Utils
    ''' <summary>
    ''' Text writes that keep the encoding the file already has on disk.
    ''' The B4A IDE saves .bas and .b4a as UTF-8 with a BOM, while
    ''' File.WriteAllText always writes UTF-8 without one.
    ''' </summary>
    Public Class TextFileIO

        Public Shared Sub WritePreservingBom(path As String, text As String)
            File.WriteAllText(path, text, New UTF8Encoding(HasUtf8Bom(path)))
        End Sub

        Public Shared Function HasUtf8Bom(path As String) As Boolean
            Try
                Using stream = File.OpenRead(path)
                    Dim head(2) As Byte
                    If stream.Read(head, 0, head.Length) < head.Length Then Return False
                    Return head(0) = &HEF AndAlso head(1) = &HBB AndAlso head(2) = &HBF
                End Using
            Catch
                Return False
            End Try
        End Function

    End Class
End Namespace
