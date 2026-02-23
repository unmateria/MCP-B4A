Imports System.IO

Namespace Utils
    Public Class CacheManager
        Private Shared ReadOnly _cache As New Dictionary(Of String, CacheEntry)
        Private Shared ReadOnly _syncRoot As New Object()

        Private Class CacheEntry
            Public Property Value As Object
            Public Property Mtime As DateTime
            Public Property Expiry As DateTime
        End Class

        ''' <summary>Gets a cached value if it still matches the file's mtime.</summary>
        Public Shared Function TryGetByMtime(Of T)(path As String, ByRef result As T) As Boolean
            SyncLock _syncRoot
                If Not _cache.ContainsKey(path) Then Return False
                Dim entry = _cache(path)
                Dim currentMtime = File.GetLastWriteTimeUtc(path)
                If currentMtime <> entry.Mtime Then
                    _cache.Remove(path)
                    Return False
                End If
                result = DirectCast(entry.Value, T)
                Return True
            End SyncLock
        End Function

        Public Shared Sub SetByMtime(path As String, value As Object)
            SyncLock _syncRoot
                _cache(path) = New CacheEntry With {
                    .Value = value,
                    .Mtime = File.GetLastWriteTimeUtc(path),
                    .Expiry = DateTime.MaxValue
                }
            End SyncLock
        End Sub

        ''' <summary>TTL-based cache (no file backing).</summary>
        Public Shared Function TryGetByTtl(Of T)(key As String, ByRef result As T) As Boolean
            SyncLock _syncRoot
                If Not _cache.ContainsKey(key) Then Return False
                Dim entry = _cache(key)
                If DateTime.UtcNow > entry.Expiry Then
                    _cache.Remove(key)
                    Return False
                End If
                result = DirectCast(entry.Value, T)
                Return True
            End SyncLock
        End Function

        Public Shared Sub SetByTtl(key As String, value As Object, ttlSeconds As Integer)
            SyncLock _syncRoot
                _cache(key) = New CacheEntry With {
                    .Value = value,
                    .Mtime = DateTime.MinValue,
                    .Expiry = DateTime.UtcNow.AddSeconds(ttlSeconds)
                }
            End SyncLock
        End Sub

        Public Shared Sub Store(key As String, value As Object)
            SyncLock _syncRoot
                _cache(key) = New CacheEntry With {
                    .Value = value,
                    .Mtime = DateTime.MinValue,
                    .Expiry = DateTime.MaxValue
                }
            End SyncLock
        End Sub

        Public Shared Function TryGet(Of T)(key As String, ByRef result As T) As Boolean
            SyncLock _syncRoot
                If Not _cache.ContainsKey(key) Then Return False
                result = DirectCast(_cache(key).Value, T)
                Return True
            End SyncLock
        End Function

        Public Shared Sub InvalidateLibraries()
            SyncLock _syncRoot
                Dim keysToRemove = _cache.Keys.Where(Function(k) k.StartsWith("libs:")).ToList()
                For Each k In keysToRemove
                    _cache.Remove(k)
                Next
            End SyncLock
        End Sub

        Public Shared Sub Invalidate(key As String)
            SyncLock _syncRoot
                _cache.Remove(key)
            End SyncLock
        End Sub
    End Class
End Namespace
