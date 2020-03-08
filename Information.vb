Imports Newtonsoft.Json
Imports System.IO

Namespace DiscordMusicBot
    Friend Class Information
        Friend Shared ReadOnly Property Config As Config
            Get
                Return JsonConvert.DeserializeObject(Of Config)(File.ReadAllText("config.json"))
            End Get
        End Property

        Friend Shared ReadOnly Property ClientId As String
            Get
                Return Config.ClientId
            End Get
        End Property

        Friend Shared ReadOnly Property ClientSecret As String
            Get
                Return Config.ClientSecret
            End Get
        End Property

        Friend Shared ReadOnly Property BotName As String
            Get
                Return Config.BotName
            End Get
        End Property

        Friend Shared ReadOnly Property Token As String
            Get
                Return Config.Token
            End Get
        End Property

        Friend Shared ReadOnly Property ServerName As String
            Get
                Return Config.ServerName
            End Get
        End Property

        Friend Shared ReadOnly Property TextChannelName As String
            Get
                Return Config.TextChannelName
            End Get
        End Property

        Friend Shared ReadOnly Property VoiceChannelName As String
            Get
                Return Config.VoiceChannelName
            End Get
        End Property
    End Class

    Public Class Config
        Public ClientId As String = "YourClientID"
        Public ClientSecret As String = "YourClientSecret"
        Public BotName As String = "YourBotName"
        Public Token As String = "YourBotToken"
        Public ServerName As String = "TheServerYouWantToConnectTo"
        Public TextChannelName As String = "TheTextChannelYouWantToJoin"
        Public VoiceChannelName As String = "TheVoiceChannelYouWantToJoin"

        Public Shared Operator =(ByVal cfg1 As Config, ByVal cfg2 As Config) As Boolean
            Return If(ReferenceEquals(cfg1, Nothing), ReferenceEquals(cfg2, Nothing), cfg1.Equals(cfg2))
        End Operator

        Public Shared Operator <>(ByVal cfg1 As Config, ByVal cfg2 As Config) As Boolean
            Return If(Not ReferenceEquals(cfg1, Nothing), Not ReferenceEquals(cfg2, Nothing), Not cfg1.Equals(cfg2))
        End Operator

        Public Overrides Function Equals(ByVal obj As Object) As Boolean
            Return Equals(TryCast(obj, Config))
        End Function

        Public Overloads Function Equals(ByVal compare As Config) As Boolean
            If compare Is Nothing Then Return False
            Return ClientId = compare.ClientId AndAlso ClientSecret = compare.ClientSecret AndAlso BotName = compare.BotName AndAlso Token = compare.Token AndAlso ServerName = compare.ServerName AndAlso TextChannelName = compare.TextChannelName AndAlso VoiceChannelName = compare.VoiceChannelName
        End Function

        Public Overrides Function GetHashCode() As Integer
            Return (ClientId & ClientSecret & BotName & Token & ServerName & TextChannelName & VoiceChannelName).GetHashCode()
        End Function
    End Class
End Namespace
