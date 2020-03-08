Imports Discord
Imports Discord.Audio
Imports Discord.Rest
Imports Discord.WebSocket
Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.Linq
Imports System.Threading
Imports System.Threading.Tasks

Namespace DiscordMusicBot
    Friend Class MusicBot
        Implements IDisposable

        Private _client As DiscordSocketClient
        Private _voiceChannel As IVoiceChannel
        Private _textChannel As ITextChannel
        Private _permittedUsers As List(Of String)
        Private _tcs As TaskCompletionSource(Of Boolean)
        Private _disposeToken As CancellationTokenSource
        Private _audio As IAudioClient
        Private Const ImABot As String = " *I'm a Bot, beep boop blop*"
        Private ReadOnly _commands As String() = {"!help", "!queue", "!add", "!addPlaylist", "!pause", "!play", "!clear", "!come", "!update", "!skip"}
        Private _queue As Queue(Of Tuple(Of String, String, String, String))

        Private Property Pause As Boolean
            Get
                Return _internalPause
            End Get
            Set(ByVal value As Boolean)
                Dim thread As New Thread(
                    Sub()
                        _tcs.TrySetResult(value)
                    End Sub
                )
                thread.Start()
                _internalPause = value
            End Set
        End Property

        Private _internalPause As Boolean

        Private Property Skip As Boolean
            Get
                Dim ret As Boolean = _internalSkip
                _internalSkip = False
                Return ret
            End Get
            Set(ByVal value As Boolean)
                _internalSkip = value
            End Set
        End Property

        Private _internalSkip As Boolean
        Public IsDisposed As Boolean

        Public Sub New()
            Initialize()
        End Sub

        Public Async Sub Initialize()
            ReadConfig()
            _queue = New Queue(Of Tuple(Of String, String, String, String))()
            _tcs = New TaskCompletionSource(Of Boolean)()
            _disposeToken = New CancellationTokenSource()
            _client = New DiscordSocketClient(New DiscordSocketConfig With {
                .LogLevel = LogSeverity.Info
            })
            AddHandler _client.Log, AddressOf Log
            AddHandler _client.Disconnected, AddressOf Disconnected
            AddHandler _client.Connected, AddressOf Connected
            AddHandler _client.Ready, AddressOf OnReady
            AddHandler _client.MessageReceived, AddressOf OnMessageReceived
            Console.Title = "Music Bot (Connecting...)"
            Await _client.StartAsync()
            Await _client.LoginAsync(TokenType.Bot, Information.Token)
            InitThread()
            Status()
        End Sub

        Private Shared Function Disconnected(ByVal arg As Exception) As Task
            Print($"Connection lost! ({arg.Message})", ConsoleColor.Red)
            Return Task.CompletedTask
        End Function

        Private Shared Function Connected() As Task
            Console.Title = "Music Bot (Connected)"
            Print("Connected!", ConsoleColor.Green)
            Return Task.CompletedTask
        End Function

        Private Function OnReady() As Task
            Ready()
            Return Task.CompletedTask
        End Function

        Private Async Sub Ready()
            Print("Ready!", ConsoleColor.Green)
            Await _client.SetGameAsync("Nothing :/")

            Try
                PrintServers()
                Dim guild As SocketGuild = _client.Guilds.FirstOrDefault(Function(g) g.Name = Information.ServerName)
                _textChannel = guild.TextChannels.FirstOrDefault(Function(t) t.Name = Information.TextChannelName)
                Print($"Using Text Channel: ""#{_textChannel.Name}""", ConsoleColor.Cyan)
                _voiceChannel = guild.VoiceChannels.FirstOrDefault(Function(t) t.Name = Information.VoiceChannelName)
                Print($"Using Voice Channel: ""{_voiceChannel.Name}""", ConsoleColor.Cyan)
                _audio = Await _voiceChannel.ConnectAsync()
            Catch e As Exception
                Print("Could not join Voice/Text Channel (" & e.Message & ")", ConsoleColor.Red)
            End Try
        End Sub

        Private Function OnMessageReceived(ByVal socketMsg As SocketMessage) As Task
            MessageReceived(socketMsg)
            Return Task.CompletedTask
        End Function

        Private Async Sub MessageReceived(ByVal socketMsg As SocketMessage)
            Dim uriResult As Uri = Nothing

            Try

                If socketMsg.Author.Id = _client.CurrentUser.Id Then
                    Return
                End If

                Print($"User ""{socketMsg.Author}"" wrote: ""{socketMsg.Content}""", ConsoleColor.Magenta)
                Dim msg As String = socketMsg.Content
                Dim isCmd As Boolean = _commands.Any(Function(c) msg.StartsWith(c))

                If isCmd Then

                    If socketMsg.Channel.Name = "general" Then
                        Await socketMsg.DeleteAsync()
                        Return
                    End If
                Else

                    If socketMsg.Channel.Name = Information.TextChannelName Then
                        Await socketMsg.DeleteAsync()
                    End If

                    Return
                End If

                'Dim dm As RestDMChannel = Await socketMsg.Author.GetOrCreateDMChannelAsync()

                Try
                    Await socketMsg.DeleteAsync()
                Catch
                End Try

                If msg.StartsWith("!help") Then
                    Print("User requested: Help", ConsoleColor.Magenta)
                    Await _textChannel.SendMessageAsync($"Use these *Commands* by sending me a **private Message**, or writing in **#{Information.TextChannelName}**!", embed:=GetHelp(socketMsg.Author.ToString()))
                    'Await dm.SendMessageAsync($"Use these *Commands* by sending me a **private Message**, or writing in **#{Information.TextChannelName}**!" & ImABot, embed:=GetHelp(socketMsg.Author.ToString()))
                    Return
                ElseIf msg.StartsWith("!queue") Then
                    Print("User requested: Queue", ConsoleColor.Magenta)
                    Await SendQueue(_textChannel)
                    Return
                End If

                If Not _permittedUsers.Contains(socketMsg.Author.ToString()) Then
                    Console.WriteLine(socketMsg.Author.ToString)
                    Console.WriteLine(_permittedUsers)
                    Await _textChannel.SendMessageAsync("Sorry, but you're not yet permitted to do that!")
                    'Await dm.SendMessageAsync("Sorry, but you're not allowed to do that!" & ImABot)
                    Return
                End If

                Dim split As String() = msg.Split(" "c)
                Dim command As String = split(0).ToLower()
                Dim parameter As String = Nothing
                If split.Length > 1 Then parameter = split(1)

                Select Case command
                    Case "!add"

                        If parameter IsNot Nothing Then

                            Using _textChannel.EnterTypingState()
                                Dim result As Boolean = Uri.TryCreate(parameter, UriKind.Absolute, uriResult) AndAlso (uriResult.Scheme = "http" OrElse uriResult.Scheme = "https")

                                If result Then

                                    Try
                                        Print("Downloading Video...", ConsoleColor.Magenta)
                                        Dim info As Tuple(Of String, String) = Await DownloadHelper.GetInfo(parameter)
                                        Await SendMessage($"<@{socketMsg.Author.Id}> requested ""{info.Item1}"" ({info.Item2})! Downloading now..." & ImABot)
                                        Dim file As String = Await DownloadHelper.Download(parameter)
                                        Dim vidInfo = New Tuple(Of String, String, String, String)(file, info.Item1, info.Item2, socketMsg.Author.ToString())
                                        _queue.Enqueue(vidInfo)
                                        Pause = False
                                        Print($"Song added to playlist! ({vidInfo.Item2} ({vidInfo.Item3}))!", ConsoleColor.Magenta)
                                    Catch ex As Exception
                                        Print($"Could not download Song! {ex.Message}", ConsoleColor.Red)
                                        'Await SendMessage($"Sorry <@{socketMsg.Author.Id}>, unfortunately I can't play that Song!" & ImABot)
                                    End Try
                                Else
                                    Await _textChannel.SendMessageAsync($"Sorry <@{socketMsg.Author.Id}>, but that was not a valid URL!" & ImABot)
                                End If
                            End Using
                        End If

                    Case "!addPlaylist"

                        If parameter IsNot Nothing Then

                            Using _textChannel.EnterTypingState()
                                Dim result As Boolean = Uri.TryCreate(parameter, UriKind.Absolute, uriResult) AndAlso (uriResult.Scheme = "http" OrElse uriResult.Scheme = "https")

                                If result Then

                                    Try
                                        Print("Downloading Playlist...", ConsoleColor.Magenta)
                                        Dim info As Tuple(Of String, String) = Await DownloadHelper.GetInfo(parameter)
                                        Await SendMessage($"<@{socketMsg.Author.Id}> requested Playlist ""{info.Item1}"" ({info.Item2})! Downloading now..." & ImABot)
                                        Dim file As String = Await DownloadHelper.DownloadPlaylist(parameter)
                                        Dim vidInfo = New Tuple(Of String, String, String, String)(file, info.Item1, info.Item2, socketMsg.Author.ToString())
                                        _queue.Enqueue(vidInfo)
                                        Pause = False
                                        Print($"Playlist added to playlist! (""{vidInfo.Item2}"" ({vidInfo.Item2}))!", ConsoleColor.Magenta)
                                    Catch ex As Exception
                                        Print($"Could not download Playlist! {ex.Message}", ConsoleColor.Red)
                                        'Await SendMessage($"Sorry <@{socketMsg.Author.Id}>, unfortunately I can't play that Playlist!" & ImABot)
                                    End Try
                                Else
                                    Await _textChannel.SendMessageAsync($"Sorry <@{socketMsg.Author.Id}>, but that was not a valid URL!" & ImABot)
                                End If
                            End Using
                        End If

                    Case "!pause"
                        Pause = True
                        Print("Playback paused!", ConsoleColor.Magenta)
                        Await _textChannel.SendMessageAsync($"<@{socketMsg.Author}> paused playback!" & ImABot)
                    Case "!play"
                        Pause = False
                        Print("Playback continued!", ConsoleColor.Magenta)
                        Await _textChannel.SendMessageAsync($"<@{socketMsg.Author}> resumed playback!" & ImABot)
                    Case "!clear"
                        Pause = True
                        _queue.Clear()
                        Print("Playlist cleared!", ConsoleColor.Magenta)
                        Await SendMessage($"<@{socketMsg.Author.Id}> cleared the Playlist!" & ImABot)
                    Case "!come"
                        _audio?.Dispose()
                        _voiceChannel = (TryCast(socketMsg.Author, IGuildUser))?.VoiceChannel

                        If _voiceChannel Is Nothing Then
                            Print("Error joining Voice Channel!", ConsoleColor.Red)
                            Await socketMsg.Channel.SendMessageAsync($"I can't connect to your Voice Channel <@{socketMsg.Author}>!" & ImABot)
                        Else
                            Print($"Joined Voice Channel ""{_voiceChannel.Name}""", ConsoleColor.Magenta)
                            _audio = Await _voiceChannel.ConnectAsync()
                        End If

                    Case "!update"
                        ReadConfig()
                        Print("User Config Updated!", ConsoleColor.Magenta)
                        Await _textChannel.SendMessageAsync("Updated user permitted list!")
                        'Await dm.SendMessageAsync("Updated Permitted Users List!")
                    Case "!skip"
                        Print("Song Skipped!", ConsoleColor.Magenta)
                        Await _textChannel.SendMessageAsync($"<@{socketMsg.Author}> skipped **{_queue.Peek().Item2}**!")
                        Skip = True
                        Pause = False
                    Case Else
                End Select

            Catch ex As Exception
                Print(ex.Message, ConsoleColor.Red)
            End Try
        End Sub

        Private Async Function Connect() As Task
            Await _client.LoginAsync(TokenType.Bot, Information.Token)
            Await _client.StartAsync()
        End Function

        Private Shared Function Log(ByVal arg As LogMessage) As Task
            Select Case arg.Severity
                Case LogSeverity.Critical, LogSeverity.[Error]
                    Console.ForegroundColor = ConsoleColor.Red
                Case LogSeverity.Debug, LogSeverity.Verbose
                    Console.ForegroundColor = ConsoleColor.Gray
                Case LogSeverity.Warning
                    Console.ForegroundColor = ConsoleColor.Yellow
                Case LogSeverity.Info
                    Console.ForegroundColor = ConsoleColor.Green
                Case Else
            End Select

            Console.WriteLine($"[{arg.Severity}] [{arg.Source}] [{arg.Message}]")
            Console.ResetColor()
            Return Task.CompletedTask
        End Function

        Public Async Function SendMessage(ByVal message As String) As Task
            If _textChannel IsNot Nothing Then Await _textChannel.SendMessageAsync(message)
        End Function

        Private Async Function SendQueue(ByVal channel As IMessageChannel) As Task
            Dim builder As EmbedBuilder = New EmbedBuilder() With {
                .Author = New EmbedAuthorBuilder With {
                    .Name = "Music Bot Song Queue"
                },
                .Footer = New EmbedFooterBuilder() With {
                    .Text = "(I don't actually sing)"
                },
                .Color = If(Pause, New Color(244, 67, 54), New Color(0, 99, 33))
            }

            If _queue.Count = 0 Then
                Await channel.SendMessageAsync("Sorry, Song Queue is empty! Add some songs with the `!add [url]` command!" & ImABot)
            Else

                For Each song As Tuple(Of String, String, String, String) In _queue
                    builder.AddField($"{song.Item2} ({song.Item3})", $"by {song.Item4}")
                Next

                Await channel.SendMessageAsync("", embed:=builder.Build())
            End If
        End Function

        Public Function GetHelp(ByVal user As String) As Embed
            Dim builder As EmbedBuilder = New EmbedBuilder() With {
                .Title = "Music Bot Help",
                .Description = If(_permittedUsers.Contains(user), "You are allowed to use **every** command.", "You are only allowed to use `!help` and `!queue`"),
                .Color = New Color(102, 153, 255)
            }
            builder.AddField("`!help`", "Prints available Commands and usage")
            builder.AddField("`!queue`", "Prints all queued Songs & their User")
            builder.AddField("`!add [url]`", "Adds a single Song to Music-queue")
            builder.AddField("`!addPlaylist [url]`", "Adds whole playlist to Music-queue")
            builder.AddField("`!pause`", "Pause the queue and current Song")
            builder.AddField("`!play`", "Resume the queue and current Song")
            builder.AddField("`!clear`", "Clear queue and current Song")
            builder.AddField("`!come`", "Let Bot join your Channel")
            builder.AddField("`!update`", "Updates Permitted Clients from File")
            Return builder.Build()
        End Function

        Private Async Function DisposeAsync() As Task
            Try
                Await _client.StopAsync()
                Await _client.LogoutAsync()
            Catch
            End Try

            _client?.Dispose()
        End Function

        Private Async Sub Status()
            Try

                While Not _disposeToken.IsCancellationRequested
                    Dim state As ConnectionState = _client.ConnectionState
                    Console.Title = $"Music Bot ({state})"

                    If state = ConnectionState.Disconnected Then
                        Await Task.Delay(5000, _disposeToken.Token)

                        If state = ConnectionState.Disconnected Then
                            Await Connect()
                        End If
                    End If

                    Await Task.Delay(5000, _disposeToken.Token)
                End While

            Catch __unusedTaskCanceledException1__ As TaskCanceledException
            End Try
        End Sub

        Public Shared Sub Print(ByVal message As String, ByVal color As ConsoleColor)
            Console.ForegroundColor = color
            Console.WriteLine(message)
            Console.ResetColor()
        End Sub

        Private Sub PrintServers()
            Print(vbLf & vbCr & "Added Servers:", ConsoleColor.Cyan)

            For Each server As SocketGuild In _client.Guilds
                Print(If(server.Name = Information.ServerName, $" [x] {server.Name}", $" [ ] {server.Name}"), ConsoleColor.Cyan)
            Next

            Print("", ConsoleColor.Cyan)
        End Sub

        Public Sub ReadConfig()
            If Not File.Exists("users.txt") Then File.Create("users.txt").Dispose()
            _permittedUsers = New List(Of String)(File.ReadAllLines("users.txt"))
            Dim msg As String = _permittedUsers.Aggregate("Permitted Users:" & vbLf & vbCr & "    ", Function(current, user) current & (user & ", "))
            Print(msg, ConsoleColor.Cyan)
        End Sub

        Public Sub InitThread()
            Dim thread As New Thread(AddressOf MusicPlay)
            thread.Start()
        End Sub

        Private Shared Function GetFfmpeg(ByVal path As String) As Process
            Dim ffmpeg As ProcessStartInfo = New ProcessStartInfo With {
                .FileName = "ffmpeg",
                .Arguments = $"-xerror -i ""{path}"" -ac 2 -f s16le -ar 48000 pipe:1",
                .RedirectStandardOutput = True
            }
            Return Process.Start(ffmpeg)
        End Function

        Private Shared Function GetFfplay(ByVal path As String) As Process
            Dim ffplay As ProcessStartInfo = New ProcessStartInfo With {
                .FileName = "ffplay",
                .Arguments = $"-i ""{path}"" -ac 2 -f s16le -ar 48000 pipe:1 -autoexit",
                .RedirectStandardOutput = True
            }
            Return New Process With {
                .StartInfo = ffplay
            }
        End Function

        Private Async Function SendAudio(ByVal path As String) As Task
            Dim ffmpeg As Process = GetFfmpeg(path)

            Using output As Stream = ffmpeg.StandardOutput.BaseStream

                Using discord As AudioOutStream = _audio.CreatePCMStream(AudioApplication.Mixed, 1920)
                    Dim bufferSize As Integer = 1024
                    Dim bytesSent As Integer = 0
                    Dim fail As Boolean = False
                    Dim [exit] As Boolean = False
                    Dim buffer As Byte() = New Byte(bufferSize - 1) {}

                    While Not Skip AndAlso Not fail AndAlso Not _disposeToken.IsCancellationRequested AndAlso Not [exit]

                        Try
                            Dim read As Integer = Await output.ReadAsync(buffer, 0, bufferSize, _disposeToken.Token)

                            If read = 0 Then
                                [exit] = True
                                Exit While
                            End If

                            Await discord.WriteAsync(buffer, 0, read, _disposeToken.Token)

                            If Pause Then
                                Dim pauseAgain As Boolean

                                Do
                                    pauseAgain = Await _tcs.Task
                                    _tcs = New TaskCompletionSource(Of Boolean)()
                                Loop While pauseAgain
                            End If

                            bytesSent += read
                        Catch __unusedTaskCanceledException1__ As TaskCanceledException
                            [exit] = True
                        Catch
                            fail = True
                        End Try
                    End While

                    Await discord.FlushAsync()
                End Using
            End Using
        End Function

        Private Async Sub MusicPlay()
            Dim [next] As Boolean = False

            While True
                Dim pause As Boolean = False

                If Not [next] Then
                    pause = Await _tcs.Task
                    _tcs = New TaskCompletionSource(Of Boolean)()
                Else
                    [next] = False
                End If

                Try

                    If _queue.Count = 0 Then
                        Await _client.SetGameAsync("Nothing :/")
                        Print("Playlist ended.", ConsoleColor.Magenta)
                    Else

                        If Not pause Then
                            Dim song = _queue.Peek()
                            Await _client.SetGameAsync(song.Item2, song.Item1)
                            Print($"Now playing: {song.Item2} ({song.Item3})", ConsoleColor.Magenta)
                            Await SendMessage($"Now playing: **{song.Item2}** ({song.Item3})")
                            Await SendAudio(song.Item1)

                            Try
                                File.Delete(song.Item1)
                            Catch
                            Finally
                                _queue.Dequeue()
                            End Try

                            [next] = True
                        End If
                    End If

                Catch
                End Try
            End While
        End Sub

        Public Sub Dispose()
            IsDisposed = True
            _disposeToken.Cancel()
            Print("Shutting down...", ConsoleColor.Red)
            Dim thread As New Thread(
                Sub()

                    For Each song In _queue

                        Try
                            File.Delete(song.Item1)
                        Catch
                        End Try
                    Next
                End Sub
            )
            thread.Start()
            DisposeAsync().GetAwaiter().GetResult()
        End Sub

        Private Sub IDisposable_Dispose() Implements IDisposable.Dispose
            Throw New NotImplementedException()
        End Sub
    End Class
End Namespace
