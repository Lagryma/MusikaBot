Imports System
Imports System.Diagnostics
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks

Namespace DiscordMusicBot
    Friend Class DownloadHelper
        Private Shared ReadOnly DownloadPath As String = Path.Combine(Directory.GetCurrentDirectory(), "Temp")

        Public Shared Async Function Download(ByVal url As String) As Task(Of String)
            If url.ToLower().Contains("youtube.com") Then
                Return Await DownloadFromYouTube(url)
            Else
                Throw New Exception("Video URL not supported!")
            End If
        End Function

        Public Shared Async Function DownloadPlaylist(ByVal url As String) As Task(Of String)
            If url.ToLower().Contains("youtube.com") Then
                Return Await DownloadPlaylistFromYouTube(url)
            Else
                Throw New Exception("Video URL not supported!")
            End If
        End Function

        Public Shared Async Function GetInfo(ByVal url As String) As Task(Of Tuple(Of String, String))
            If url.ToLower().Contains("youtube.com") Then
                Return Await GetInfoFromYouTube(url)
            Else
                Throw New Exception("Video URL not supported!")
            End If
        End Function

        Private Shared Async Function GetInfoFromYouTube(ByVal url As String) As Task(Of Tuple(Of String, String))
            Dim tcs As TaskCompletionSource(Of Tuple(Of String, String)) = New TaskCompletionSource(Of Tuple(Of String, String))()
            Dim thread As New Thread(
                Sub()
                    Dim title As String
                    Dim duration As String
                    Dim youtubedl As Process
                    Dim youtubedlGetTitle As ProcessStartInfo = New ProcessStartInfo() With {
                        .FileName = "youtube-dl",
                        .Arguments = $"-s -e --get-duration {url}",
                        .UseShellExecute = False,
                        .CreateNoWindow = True,
                        .RedirectStandardOutput = True
                    }
                    youtubedl = Process.Start(youtubedlGetTitle)
                    youtubedl.WaitForExit()
                    Dim lines As String() = youtubedl.StandardOutput.ReadToEnd().Split(vbLf)

                    If lines.Length >= 2 Then
                        title = lines(0)
                        duration = lines(1)
                    Else
                        title = "No Title found"
                        duration = "0"
                    End If

                    tcs.SetResult(New Tuple(Of String, String)(title, duration))
                End Sub
            )
            thread.Start()
            Dim result As Tuple(Of String, String) = Await tcs.Task
            If result Is Nothing Then Throw New Exception("youtube-dl.exe failed to receive title!")
            Return result
        End Function

        Private Shared Async Function DownloadFromYouTube(ByVal url As String) As Task(Of String)
            Dim tcs As TaskCompletionSource(Of String) = New TaskCompletionSource(Of String)()
            Dim thread As New Thread(
                Sub()
                    Dim filePath As String
                    Dim count As Integer = 0

                    Do
                        filePath = Path.Combine(DownloadPath, "botsong" & System.Threading.Interlocked.Increment(count) & ".mp3")
                    Loop While File.Exists(filePath)

                    Dim youtubedl As Process
                    Dim youtubedlDownload As ProcessStartInfo = New ProcessStartInfo() With {
                        .FileName = "youtube-dl",
                        .Arguments = $"-x --audio-format mp3 -o """ + filePath.Replace(".mp3", ".%(ext)s") + """ " + url,
                        .UseShellExecute = False,
                        .CreateNoWindow = True,
                        .RedirectStandardOutput = True
                    }
                    youtubedl = Process.Start(youtubedlDownload)
                    youtubedl.WaitForExit()
                    Thread.Sleep(1000)

                    If File.Exists(filePath) Then
                        tcs.SetResult(filePath)
                    Else
                        tcs.SetResult(Nothing)
                        MusicBot.Print($"Could not download Song, youtube-dl responded with:{vbCrLf}{vbNewLine}{youtubedl.StandardOutput.ReadToEnd()}", ConsoleColor.Red)
                    End If
                End Sub
            )
            thread.Start()
            Dim result As String = Await tcs.Task
            If result Is Nothing Then Throw New Exception("youtube-dl.exe failed to download!")
            result = result.Replace(vbLf, "").Replace(Environment.NewLine, "")
            Return result
        End Function

        Private Shared Async Function DownloadPlaylistFromYouTube(ByVal url As String) As Task(Of String)
            Dim tcs As TaskCompletionSource(Of String) = New TaskCompletionSource(Of String)()
            Dim thread As New Thread(
                Sub()
                    Dim filePath As String
                    Dim count As Integer = 0

                    Do
                        filePath = Path.Combine(DownloadPath, "tempvideo" & System.Threading.Interlocked.Increment(count) & ".mp3")
                    Loop While File.Exists(filePath)

                    Dim youtubedl As Process
                    Dim youtubedlDownload As ProcessStartInfo = New ProcessStartInfo() With {
                        .FileName = "youtube-dl",
                        .Arguments = $"--extract-audio --audio-format mp3 -o ""{filePath.Replace(".mp3", ".%(ext)s")}"" {url}",
                        .UseShellExecute = False,
                        .CreateNoWindow = True,
                        .RedirectStandardOutput = True
                    }
                    youtubedl = Process.Start(youtubedlDownload)
                    youtubedl.WaitForExit()

                    If File.Exists(filePath) Then
                        tcs.SetResult(filePath)
                    Else
                        tcs.SetResult(Nothing)
                        MusicBot.Print($"Could not download Song, youtube-dl responded with:\n\r{youtubedl.StandardOutput.ReadToEnd()}", ConsoleColor.Red)
                    End If
                End Sub
            )
            thread.Start()
            Dim result As String = Await tcs.Task
            If result Is Nothing Then Throw New Exception("youtube-dl.exe failed to download!")
            result = result.Replace(vbLf, "").Replace(Environment.NewLine, "")
            Return result
        End Function
    End Class
End Namespace
