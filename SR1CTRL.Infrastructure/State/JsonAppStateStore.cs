using Microsoft.Extensions.Logging;
using SR1CTRL.Application.Abstractions;
using SR1CTRL.Application.Models;
using System.Text.Json;

namespace SR1CTRL.Infrastructure.State;

public sealed class JsonAppStateStore(ILogger<JsonAppStateStore> logger) : IAppStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly ILogger<JsonAppStateStore> _logger = logger;
    private readonly string _stateFilePath = Path.Combine(AppContext.BaseDirectory, "app-state.json");

    public AppStateSnapshot Load()
    {
        try
        {
            if (!File.Exists(_stateFilePath))
            {
                return new AppStateSnapshot();
            }

            var json = File.ReadAllText(_stateFilePath);
            return JsonSerializer.Deserialize<AppStateSnapshot>(json, SerializerOptions) ?? new AppStateSnapshot();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load application state from {StateFilePath}.", _stateFilePath);
            return new AppStateSnapshot();
        }
    }

    public void Save(AppStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        try
        {
            var directory = Path.GetDirectoryName(_stateFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(snapshot, SerializerOptions);
            File.WriteAllText(_stateFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save application state to {StateFilePath}.", _stateFilePath);
        }
    }
}
