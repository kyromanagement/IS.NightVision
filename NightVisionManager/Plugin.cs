using System;
using System.Collections;
using System.IO;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.Scp079Events;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.Handlers;
using UnityEngine;
using YamlException = YamlDotNet.Core.YamlException;
using ExiledPlayerEvents = Exiled.Events.Handlers.Player;
using ExiledServerEvents = Exiled.Events.Handlers.Server;

namespace InitStudio.NightVision;

public sealed class NightVisionPlugin : Plugin<ExiledConfig>
{
    private const string SettingsFileName = "night_vision_manager.yml";
    private readonly VisionController controller = new();
    private Keybind? keybind;
    private VisionTicker? ticker;

    public override string Name => "NightVisionManager";
    public override string Author => "Init Studio";
    public override string Prefix => "night_vision_manager";
    public override Version Version { get; } = new(1, 0, 0);
    public override Version RequiredExiledVersion { get; } = new(9, 15, 3);

    internal static NightVisionPlugin? Instance { get; private set; }
    internal VisionController Controller => controller;

    private static string SettingsPath => Path.Combine(Paths.Configs, Server.Port.ToString(), SettingsFileName);

    public override void OnEnabled()
    {
        Instance = this;
        keybind = new Keybind(controller.OnKeyChanged);
        keybind.Subscribe();

        PlayerEvents.ChangedRole += OnRoleChanged;
        ExiledPlayerEvents.Verified += OnVerified;
        ExiledPlayerEvents.Died += OnDied;
        ExiledPlayerEvents.Left += OnLeft;
        ExiledPlayerEvents.RoomChanged += OnRoomChanged;
        ExiledServerEvents.RoundEnded += OnRoundEnded;
        ExiledServerEvents.RestartingRound += controller.Reset;
        ExiledServerEvents.WaitingForPlayers += controller.Reset;
        Scp079Events.BlackedOutRoom += OnRoomBlackout;
        Scp079Events.BlackedOutZone += OnZoneBlackout;
        ServerEvents.RoomLightChanged += OnLightingChanged;

        if (!ReloadSettings(out string message, allowKeybindConflict: true))
            Log.Error(message);

        var host = new GameObject("NightVisionManager ticker");
        UnityEngine.Object.DontDestroyOnLoad(host);
        ticker = host.AddComponent<VisionTicker>();
        ticker.StartTicks(controller);
        base.OnEnabled();
    }

    public override void OnDisabled()
    {
        PlayerEvents.ChangedRole -= OnRoleChanged;
        ExiledPlayerEvents.Verified -= OnVerified;
        ExiledPlayerEvents.Died -= OnDied;
        ExiledPlayerEvents.Left -= OnLeft;
        ExiledPlayerEvents.RoomChanged -= OnRoomChanged;
        ExiledServerEvents.RoundEnded -= OnRoundEnded;
        ExiledServerEvents.RestartingRound -= controller.Reset;
        ExiledServerEvents.WaitingForPlayers -= controller.Reset;
        Scp079Events.BlackedOutRoom -= OnRoomBlackout;
        Scp079Events.BlackedOutZone -= OnZoneBlackout;
        ServerEvents.RoomLightChanged -= OnLightingChanged;

        if (ticker != null)
        {
            ticker.StopAllCoroutines();
            UnityEngine.Object.Destroy(ticker.gameObject);
            ticker = null;
        }

        keybind?.Unsubscribe();
        keybind = null;
        controller.Reset();
        Instance = null;
        base.OnDisabled();
    }

    internal bool ReloadSettings(out string message, bool allowKeybindConflict = false)
    {
        Settings settings;
        try
        {
            settings = Settings.Load(SettingsPath);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or YamlException)
        {
            message = $"Unable to load {SettingsPath}. {error.Message}";
            return false;
        }

        if (keybind is null)
        {
            message = "The keybind service is unavailable.";
            return false;
        }

        if (!keybind.Configure(settings.IsEnabled ? settings.Activation : null, out string? keyError))
        {
            if (!allowKeybindConflict)
            {
                message = keyError!;
                return false;
            }

            Log.Warn($"{keyError} The keybind is unavailable until the ID is changed. Console activation follows the YAML.");
        }

        controller.ApplySettings(settings);
        message = $"Loaded {SettingsPath} (enabled: {settings.IsEnabled}).";
        if (settings.Debug)
            Log.Info(message);
        return true;
    }

    private void OnRoleChanged(PlayerChangedRoleEventArgs ev)
    {
        if (Player.TryGet(ev.Player, out Player player))
            controller.OnRoleChanged(player);
    }

    private void OnVerified(VerifiedEventArgs ev) => controller.QueueAuto(ev.Player);
    private void OnDied(DiedEventArgs ev) => controller.Forget(ev.Player);
    private void OnLeft(LeftEventArgs ev) => controller.Forget(ev.Player);
    private void OnRoomChanged(RoomChangedEventArgs ev) => controller.Refresh(ev.Player);
    private void OnRoundEnded(Exiled.Events.EventArgs.Server.RoundEndedEventArgs _) => controller.Reset();

    private void OnRoomBlackout(Scp079BlackedOutRoomEventArgs ev)
    {
        controller.Lighting.OnRoomBlackout(ev);
        controller.RefreshActive();
    }

    private void OnZoneBlackout(Scp079BlackedOutZoneEventArgs ev)
    {
        controller.Lighting.OnZoneBlackout(ev);
        controller.RefreshActive();
    }

    private void OnLightingChanged(RoomLightChangedEventArgs _)
    {
        controller.Lighting.Prune();
        controller.RefreshActive();
    }
}

internal sealed class VisionTicker : MonoBehaviour
{
    public void StartTicks(VisionController controller) => StartCoroutine(Run(controller));

    private static IEnumerator Run(VisionController controller)
    {
        var wait = new WaitForSecondsRealtime(0.1f);
        while (true)
        {
            yield return wait;
            controller.Tick();
        }
    }
}
