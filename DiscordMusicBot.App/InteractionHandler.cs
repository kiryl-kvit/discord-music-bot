using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App;

public class InteractionHandler(
    DiscordSocketClient client,
    InteractionService interactionService,
    IServiceProvider services,
    ILogger<InteractionHandler> logger)
{
    private bool _registered;

    public async Task InitializeAsync()
    {
        client.Ready += OnReadyAsync;
        client.InteractionCreated += OnInteractionCreatedAsync;
        interactionService.InteractionExecuted += OnInteractionExecutedAsync;

        await interactionService.AddModulesAsync(typeof(InteractionHandler).Assembly, services);
    }

    private async Task OnReadyAsync()
    {
        if (_registered)
        {
            return;
        }

        logger.LogInformation("Bot is ready. Registering slash commands...");

        try
        {
            await interactionService.RegisterCommandsGloballyAsync();
            _registered = true;
            logger.LogInformation("Slash commands registered globally.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register slash commands globally");
            throw;
        }
    }

    private async Task OnInteractionCreatedAsync(SocketInteraction interaction)
    {
        try
        {
            var context = new SocketInteractionContext(client, interaction);
            await interactionService.ExecuteCommandAsync(context, services);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing command");

            if (interaction.Type == InteractionType.ApplicationCommand)
            {
                await interaction.RespondAsync("An error occurred while executing the command.", ephemeral: true);
            }
        }
    }

    private Task OnInteractionExecutedAsync(ICommandInfo command, IInteractionContext context, IResult result)
    {
        if (!result.IsSuccess)
        {
            logger.LogError("Command execution failed: {Error}", result.ErrorReason);
        }

        return Task.CompletedTask;
    }
}
