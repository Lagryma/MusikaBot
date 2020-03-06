Imports System.Threading
Imports Discord
Imports Discord.WebSocket
Imports Discord.Commands
Imports Microsoft.Extensions.Configuration
Imports Microsoft.Extensions.Configuration.Json
Imports System.IO

Module Module1

    Private _client As DiscordSocketClient
    Private _commands As CommandService
    Private prefix As String = "&"

    Sub Main()
        MainAsync().GetAwaiter().GetResult()
    End Sub

    Async Function MainAsync() As Task
        Dim config = BuildConfig()

        _client = New DiscordSocketClient()
        _commands = New CommandService()

        Console.WriteLine(config("token"))

        Await _client.LoginAsync(TokenType.Bot, config("token"))
        Await _client.StartAsync()

        AddEventHandlers()

        Await Task.Delay(Timeout.Infinite)
    End Function

    Private Sub AddEventHandlers()
        AddHandler _client.Log, AddressOf Logger
        AddHandler _commands.Log, AddressOf Logger
        AddHandler _client.MessageReceived, AddressOf CommandHandler
    End Sub

    Private Async Function CommandHandler(ByVal message As SocketMessage) As Task
        Dim userMessage As SocketUserMessage = TryCast(message, SocketUserMessage)

        If userMessage Is Nothing OrElse userMessage.Author.IsBot Then Return

        Dim msg As String = userMessage.Content

        If msg.StartsWith(prefix) Then
            If msg.Contains(" ") Then
                Dim cmd As String = msg.Split(prefix)(1).Split(" ")(0)
                Dim arg As String = msg.Split(" ")(1)

                Select Case cmd.ToLower
                    Case "myname"
                        Await userMessage.Channel.SendMessageAsync("Your name is " + arg + ".")
                    Case Else
                        Await userMessage.Channel.SendMessageAsync("There is no such command... :(")
                End Select
            Else
                Dim cmd As String = msg.Split(prefix)(1)

                Select Case cmd.ToLower
                    Case "ping"
                        Await userMessage.Channel.SendMessageAsync("Pong!")
                    Case Else
                        Await userMessage.Channel.SendMessageAsync("There is no such command... :(")
                End Select
            End If
        Else
            Return
        End If
    End Function

    Private Function Logger(ByVal message As LogMessage, Optional task As Task = Nothing) As Task
        'Very basic logging
        Console.WriteLine($"{DateTime.Now,-19} [{message.Severity,8}] {message.Source}: {message.Message} {message.Exception}")
        Return Task.CompletedTask
    End Function

    Private Function BuildConfig() As IConfiguration
        Return New ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory).AddJsonFile("config.json").Build
    End Function

End Module


