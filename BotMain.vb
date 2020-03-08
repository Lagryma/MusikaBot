Imports Newtonsoft.Json
Imports System
Imports System.Diagnostics
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks

Namespace DiscordMusicBot
    Module Program
        Public Bot As MusicBot
        Private _cts As CancellationTokenSource

        Sub Main()
            Console.Title = "Music Bot (Loading...)"
            Console.WriteLine("(Press Ctrl + C or close this Window to exit Bot)")

            Try
                Dim json As String = File.ReadAllText("config.json")
                Dim cfg As Config = JsonConvert.DeserializeObject(Of Config)(json)
                If cfg = New Config() Then Throw New Exception("Please insert values into Config.json!")
            Catch e As Exception
                MusicBot.Print("Your config.json has incorrect formatting, or is not readable!", ConsoleColor.Red)
                MusicBot.Print(e.Message, ConsoleColor.Red)

                Try
                    Process.Start("config.json")
                Catch
                End Try

                Console.ReadKey()
                Return
            End Try

            [Do]().GetAwaiter().GetResult()
        End Sub

        Private Async Function [Do]() As Task
            Try
                _cts = New CancellationTokenSource()
                Bot = New MusicBot()
                Await Task.Delay(-1, _cts.Token)
            Catch __unusedTaskCanceledException1__ As TaskCanceledException
            End Try
        End Function

    End Module
End Namespace
