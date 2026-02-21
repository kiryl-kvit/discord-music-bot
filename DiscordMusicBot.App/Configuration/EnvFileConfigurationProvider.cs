using DotNetEnv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace DiscordMusicBot.App.Configuration;

public sealed class EnvFileConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly EnvFileConfigurationSource _source;
    private PhysicalFileProvider? _fileProvider;
    private IDisposable? _changeTokenRegistration;
    private Timer? _debounceTimer;
    private readonly object _reloadLock = new();

    public EnvFileConfigurationProvider(EnvFileConfigurationSource source)
    {
        _source = source;
        WatchForChanges();
    }

    public override void Load()
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var envFileValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (File.Exists(_source.FilePath))
        {
            try
            {
                var parsedValues = Env.Load(_source.FilePath, Env.NoEnvVars());

                foreach (var kv in parsedValues)
                {
                    envFileValues[kv.Key] = kv.Value;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"Failed to parse .env file '{_source.FilePath}': {ex.Message}. " +
                    "Retaining previous configuration.");
                return;
            }
        }

        foreach (var (envKey, configPath) in _source.KeyMapping)
        {
            if (envFileValues.TryGetValue(envKey, out var envFileValue))
            {
                data[configPath] = envFileValue;
            }
            else
            {
                var systemValue = Environment.GetEnvironmentVariable(envKey);
                if (systemValue is not null)
                {
                    data[configPath] = systemValue;
                }
            }
        }

        Data = data;
    }

    private void WatchForChanges()
    {
        var directory = Path.GetDirectoryName(_source.FilePath);
        var fileName = Path.GetFileName(_source.FilePath);

        if (directory is null || !Directory.Exists(directory))
        {
            return;
        }

        _fileProvider = new PhysicalFileProvider(directory)
        {
            UsePollingFileWatcher = true,
            UseActivePolling = true,
        };

        _changeTokenRegistration = ChangeToken.OnChange(
            () => _fileProvider.Watch(fileName),
            OnFileChanged);
    }

    private void OnFileChanged()
    {
        // Debounce: file system events often fire multiple times for a single save
        // (truncate, write, close). Reset the timer on each event so we only reload
        // once the file has stabilized.
        lock (_reloadLock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(
                _ =>
                {
                    lock (_reloadLock)
                    {
                        Load();
                        OnReload();
                    }
                },
                state: null,
                dueTime: TimeSpan.FromSeconds(1),
                period: Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        lock (_reloadLock)
        {
            _debounceTimer?.Dispose();
        }

        _changeTokenRegistration?.Dispose();
        _fileProvider?.Dispose();
    }
}
