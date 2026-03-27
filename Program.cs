//Create builder
var builder = WebApplication.CreateBuilder(args);

//Add service
builder.Services.AddSingleton<DiscordBotService>();
builder.Services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<DiscordBotService>());

//Build app
var app = builder.Build();

//Health endpoint
app.MapGet("/", () => Results.Ok("NanaBot web app is running."));
app.MapGet("/health", () => Results.Ok("OK"));

//Set endpoints
app.MapPost("/debug", async (HttpRequest request, DiscordBotService nanaBot) =>
{
    return await nanaBot.Debug_SendMessageInAdminChannel();
});

//Run
app.Run();