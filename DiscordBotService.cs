using Discord;
using Discord.Webhook;
using Discord.WebSocket;
using System.Text.RegularExpressions;

public class DiscordBotService : BackgroundService
{
    //Enums
    public enum Type
    {
        Tournament_HighScoreSubmit, Tournament_TimeAttackSubmit, Tournament_NewTournament,
        ElderAltar_DonateObject, ElderAltar_OpenPresent,
        CampMaps_Rate, CampMaps_Upload,
        RunInfo_RescuedSirBearington, RunInfo_DefeatedSirBearington,
        RunInfo_SealedTimeline, RunInfo_RewindRunStarted, RunInfo_ChaosRunStarted
    }

    //Variables
    private readonly IConfiguration config;
    private readonly ILogger<DiscordBotService> logger;

    private DiscordSocketClient? client;

    private readonly string discordAPIKey;
    private readonly ulong nanaChannelID;
    private readonly ulong gamelogChannelID;
    private readonly ulong adminChannelID;

    private readonly Random random = new();

    private readonly string nanaChannel_transformMessage_regexPattern = @"(<a?:nana\w*:\d+>)|(:nana\w*?:)|([\.,!\?])|(\S+)";

    //Strings
    private readonly string[] nanaQuestionReplies = { "Yes.", "No." };
    private readonly string[] nanaGeneralReplies = { "Nana.", "Nana!!", "Nana?", "Nana, Nana Nana.", "Nana Nana!" };

    //Constructor
    public DiscordBotService(IConfiguration _config, ILogger<DiscordBotService> _logger)
    {
        //Set Azure variables
        config = _config;
        logger = _logger;

        //Set Discord variables
        discordAPIKey = config["discordAPIKey"] ?? throw new Exception("Missing setting: discordAPIKey");

        if (!ulong.TryParse(config["nanaChannelID"], out nanaChannelID))
            throw new InvalidOperationException("Invalid or missing environment variable: nanaChannelID");

        if (!ulong.TryParse(config["gamelogChannelID"], out gamelogChannelID))
            throw new InvalidOperationException("Invalid or missing environment variable: gamelogChannelID");

        if (!ulong.TryParse(config["adminChannelID"], out adminChannelID))
            throw new InvalidOperationException("Invalid or missing environment variable: adminChannelID");

        //Log if startup was successful
        logger.LogInformation("DiscordBotService configured. NanaChannelId={NanaChannelId}, GamelogChannelId={GamelogChannelId}", nanaChannelID, gamelogChannelID);
    }

    //Methods: Overrides
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        //Create new Discord Socket Client
        client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds
                            | GatewayIntents.GuildMessages
                            | GatewayIntents.MessageContent
        });

        //Hook to message received methods
        client.MessageReceived += General_RespondIfPinged;
        client.MessageReceived += NanaChannel_TransformMessages;

        client.Log += msg =>
        {
            var level = msg.Severity switch
            {
                LogSeverity.Critical => LogLevel.Critical,
                LogSeverity.Error => LogLevel.Error,
                LogSeverity.Warning => LogLevel.Warning,
                LogSeverity.Info => LogLevel.Information,
                LogSeverity.Verbose => LogLevel.Debug,
                LogSeverity.Debug => LogLevel.Debug,
                _ => LogLevel.Information
            };

            logger.Log(level, msg.Exception, "Discord {Source}: {Message}", msg.Source, msg.Message);
            return Task.CompletedTask;
        };

        await client.LoginAsync(TokenType.Bot, discordAPIKey);
        await client.StartAsync();

        logger.LogInformation("Discord bot started.");

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            logger.LogInformation("Discord bot cancellation requested.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (client != null)
        {
            logger.LogInformation("Stopping Discord bot...");

            await client.StopAsync();
            await client.LogoutAsync();
            client.Dispose();

            logger.LogInformation("Discord bot stopped.");
        }

        await base.StopAsync(cancellationToken);
    }

    //Methods: General
    private async Task General_RespondIfPinged(SocketMessage _message)
    {
        //Check if text channel message is valid
        SocketTextChannel textChannel;

        if (!TryGetValidTextChannelMessage(_message, out textChannel))
            return;

        //Don't process messages from nana channel
        if (textChannel.Id == nanaChannelID)
            return;

        //Bot pinged -> Respond with Nana-ish
        ulong currentUserId = client!.CurrentUser!.Id;

        if (_message.MentionedUsers.Any(u => u.Id == currentUserId))
        {
            //Check if the message is a question
            bool isQuestion = _message.Content.Trim().EndsWith('?');

            //Random reply
            string[] pingReplies = isQuestion ? nanaQuestionReplies : nanaGeneralReplies;

            //Send message
            await _message.Channel.SendMessageAsync(pingReplies[random.Next(pingReplies.Length)]);
        }
    }

    private async Task NanaChannel_TransformMessages(SocketMessage _message)
    {
        //Check if text channel message is valid
        SocketTextChannel textChannel;

        if (!TryGetValidTextChannelMessage(_message, out textChannel))
            return;

        //Don't process messages not in the right channel ID
        if (textChannel.Id != nanaChannelID)
            return;

        // Don't process single-word "What?" messages
        if (_message.Content.Trim().Equals("What?", StringComparison.OrdinalIgnoreCase))
            return;

        //Regex: Preserve Nana emojis and specific punctuation (. , ! ?), replace everything else with "Nana"
        string modifiedMessage = Regex.Replace(_message.Content, nanaChannel_transformMessage_regexPattern, match =>
        {
            if (match.Groups[1].Success)
                return match.Value;
            if (match.Groups[2].Success)
                return match.Value;
            if (match.Groups[3].Success)
                return match.Value;

            return "Nana";
        });

        //Delete original message
        try
        {
            await _message.DeleteAsync();

            logger.LogInformation("Deleted original message: {Content}", _message.Content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Delete failed");

            return;
        }

        //Set webhook
        var webhooks = await textChannel.GetWebhooksAsync();
        var webhookInfo = webhooks.FirstOrDefault(w => w.Name == "NanaWebhook") ?? await textChannel.CreateWebhookAsync("NanaWebhook");

        //Send modified message via webhook
        var webhookClient = new DiscordWebhookClient(webhookInfo.Id, webhookInfo.Token!);

        await webhookClient.SendMessageAsync(
            modifiedMessage,
            username: _message.Author.Username,
            avatarUrl: _message.Author.GetAvatarUrl() ?? _message.Author.GetDefaultAvatarUrl()
        );
    }

    public async Task GamelogChannel_SendMessage(Type _type, params string[] _params)
    {
        //Check client is properly initialized
        if (client == null ||
            client.CurrentUser == null)
        {
            logger.LogWarning("GamelogChannel_SendMessage: Failed because Discord client is not initialized.");
            return;
        }
            
        //Get channel
        var channel = await client.GetChannelAsync(gamelogChannelID) as IMessageChannel;

        if (channel == null)
        {
            logger.LogWarning($"GamelogChannel_SendMessage: Failed because gamelog channel {gamelogChannelID} was not found.");
            return;
        }

        //Set safe params
        string[] safeParams = { "", "", "", "" };
        Array.Copy(_params, safeParams, Math.Min(_params.Length, safeParams.Length));

        //Set message
        string message = _type switch
        {
            Type.Tournament_HighScoreSubmit => $"🚀 **{safeParams[0]}** submitted a new Tournament High Score run\n" +
                                               $"They are currently on place {safeParams[1]} / {safeParams[2]} with a score of {safeParams[3]}",
            Type.Tournament_TimeAttackSubmit => $"⏰ **{safeParams[0]}** submitted a new Tournament Time Attack run\n" +
                                                $"They are currently on place {safeParams[1]} / {safeParams[2]} with a time of {safeParams[3]}",
            Type.Tournament_NewTournament => $"# ⚡The **{safeParams[0]}** tournament has started! Good luck!",

            Type.ElderAltar_DonateObject => $"✨ **{safeParams[0]}** donated to the Elder Altar!",
            Type.ElderAltar_OpenPresent => $"⭐ **{safeParams[0]}** opened a Present Chest from **{safeParams[1]}**!",

            Type.CampMaps_Rate => $"💕 **{safeParams[0]}** liked the Camp Map of **{safeParams[1]}**!",
            Type.CampMaps_Upload => $"📁 **{safeParams[0]}** uploaded a new Camp Map!",

            Type.RunInfo_RescuedSirBearington => $"🐻 **{safeParams[0]}** rescued Sir Bearington!",
            Type.RunInfo_DefeatedSirBearington => $"🧸 **{safeParams[0]}** defeated Sir Bearington!",
            Type.RunInfo_SealedTimeline => $"🕰️ **{safeParams[0]}** sealed a timeline!",
            Type.RunInfo_RewindRunStarted => $"⏪ **{safeParams[0]}** started a Rewind Run!",
            Type.RunInfo_ChaosRunStarted => $"⏪ **{safeParams[0]}** started a Chaos Run!",

            _ => throw new InvalidOperationException($"Unsupported gamelog message type: {_type}")
        };

        //Send message
        await channel.SendMessageAsync(message);
    }

    public async Task<IResult> Debug_SendMessageInAdminChannel()
    {
        //Check client is properly initialized
        if (client == null ||
            client.CurrentUser == null)
            return Results.Problem("Discord client is not initialized.");

        //Get channel
        var channel = await client.GetChannelAsync(adminChannelID) as IMessageChannel;

        if (channel == null)
            return Results.Problem("Admin channel not found.");

        //Send message
        await channel.SendMessageAsync("NanaBot is up and running!");
        return Results.Ok("Message sent.");
    }

    //Methods: Helper
    private bool TryGetValidTextChannelMessage(SocketMessage message, out SocketTextChannel textChannel)
    {
        textChannel = null!;

        if (client == null ||
            client.CurrentUser == null)       
        {
            logger.LogWarning("TryGetValidTextChannelMessage: Failed because Discord client is not initialized.");
            return false;
        }

        if (message.Author.Id == client.CurrentUser.Id)
            return false;

        if (message.Source == MessageSource.Webhook)
            return false;

        if (message.Channel is not SocketTextChannel channel)
            return false;

        textChannel = channel;
        return true;
    }
}