using Discord;
using Microsoft.Extensions.Logging;

namespace DiscordMusicBot.App;

public static class Logging
{
    public static Func<LogMessage, Task> CreateLogHandler(ILogger logger)
    {
        return logMessage =>
        {
            const string message = "[{Source}] {Message}";
            switch (logMessage.Severity)
            {
                case LogSeverity.Critical:
                    logger.LogCritical(logMessage.Exception, message, logMessage.Source, logMessage.Message);
                    break;
                case LogSeverity.Error:
                    logger.LogError(logMessage.Exception, message, logMessage.Source, logMessage.Message);
                    break;
                case LogSeverity.Warning:
                    logger.LogWarning(logMessage.Exception, message, logMessage.Source, logMessage.Message);
                    break;
                case LogSeverity.Verbose:
                    logger.LogTrace(logMessage.Exception, message, logMessage.Source, logMessage.Message);
                    break;
                case LogSeverity.Debug:
                    logger.LogDebug(logMessage.Exception, message, logMessage.Source, logMessage.Message);
                    break;
                default:
                case LogSeverity.Info:
                    logger.LogInformation(logMessage.Exception, message, logMessage.Source, logMessage.Message);
                    break;
            }

            return Task.CompletedTask;
        };
    }
}