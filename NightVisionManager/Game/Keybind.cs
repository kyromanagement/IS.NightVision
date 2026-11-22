using System;
using System.Linq;
using Exiled.API.Features;
using UserSettings.ServerSpecific;

namespace InitStudio.NightVision;

internal sealed class Keybind
{
    private readonly Action<Player, bool> onChange;
    private SSKeybindSetting? definition;

    public Keybind(Action<Player, bool> onChange) => this.onChange = onChange;

    public void Subscribe() => ServerSpecificSettingsSync.ServerOnSettingValueReceived += OnSettingReceived;

    public void Unsubscribe()
    {
        ServerSpecificSettingsSync.ServerOnSettingValueReceived -= OnSettingReceived;
        Configure(null, out _);
    }

    public bool Configure(ActivationSettings? config, out string? error)
    {
        error = null;
        var all = ServerSpecificSettingsSync.DefinedSettings ?? Array.Empty<ServerSpecificSettingBase>();
        if (config is { KeybindEnabled: true })
        {
            if (all.Any(setting => !ReferenceEquals(setting, definition) && setting.SettingId == config.SettingId))
            {
                error = $"Server-Specific Settings ID {config.SettingId} is already in use.";
                return false;
            }

            if (definition is not null && all.Contains(definition) && definition.SettingId == config.SettingId
                && definition.SuggestedKey == config.SuggestedKey)
                return true;

            var replacement = new SSKeybindSetting(config.SettingId, "NightVision · Toggle", config.SuggestedKey,
                true, false, "Toggle your assigned night vision. You can also use .nvg.");
            ServerSpecificSettingsSync.DefinedSettings = all.Where(setting => !ReferenceEquals(setting, definition))
                .Append(replacement).ToArray();
            definition = replacement;
        }
        else
        {
            if (definition is null)
                return true;

            ServerSpecificSettingsSync.DefinedSettings = all.Where(setting => !ReferenceEquals(setting, definition)).ToArray();
            definition = null;
        }

        ServerSpecificSettingsSync.SendToAll();
        return true;
    }

    private void OnSettingReceived(ReferenceHub hub, ServerSpecificSettingBase setting)
    {
        if (setting is SSKeybindSetting key && definition is not null
            && ReferenceEquals(key.OriginalDefinition, definition)
            && Player.TryGet(hub, out Player player))
            onChange(player, key.SyncIsPressed);
    }
}
