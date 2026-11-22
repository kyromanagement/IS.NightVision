using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles.FirstPersonControl;
using GameRound = Exiled.API.Features.Round;

namespace InitStudio.NightVision;

internal sealed class VisionController
{
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Dictionary<Player, Session> sessions = new();
    private readonly HashSet<Player> active = new();
    private readonly HashSet<Player> pendingAuto = new();

    public Settings Settings { get; private set; } = new();
    public Lighting Lighting { get; } = new();

    public void ApplySettings(Settings settings)
    {
        Settings = settings;
        if (!settings.IsEnabled)
        {
            ResetSessions();
            return;
        }

        foreach (Session session in sessions.Values)
        {
            if (session.Profile is not null && !settings.Profiles.ContainsKey(session.Profile))
                session.Profile = null;
        }

        RefreshActive();
    }

    public void Tick()
    {
        Lighting.Prune();
        RefreshActive();

        if (pendingAuto.Count == 0)
            return;

        Player[] candidates = pendingAuto.ToArray();
        pendingAuto.Clear();
        foreach (Player player in candidates)
        {
            if (CanUseRole(player) && !active.Contains(player) && Settings.Resolve(player).AutoActivate)
                SetActive(player, true, null, out _);
        }
    }

    public void OnRoleChanged(Player player)
    {
        Forget(player);
        QueueAuto(player);
    }

    public void QueueAuto(Player player)
    {
        if (Settings.IsEnabled)
            pendingAuto.Add(player);
    }

    public void Forget(Player player)
    {
        pendingAuto.Remove(player);
        active.Remove(player);
        if (!sessions.TryGetValue(player, out Session session))
            return;

        sessions.Remove(player);
        session.NightVision?.Stop();
        session.StopShutdownEffects();
    }

    public void Reset()
    {
        ResetSessions();
        Lighting.Clear();
    }

    private void ResetSessions()
    {
        foreach (Player player in sessions.Keys.ToArray())
            Forget(player);
        pendingAuto.Clear();
    }

    public void OnKeyChanged(Player player, bool pressed)
    {
        if (!Settings.IsEnabled || !Settings.Activation.KeybindEnabled || player.IsHost || player.IsNPC)
            return;

        Session session = GetSession(player);
        bool risingEdge = pressed && !session.KeyDown;
        session.KeyDown = pressed;
        if (risingEdge && !Toggle(player, out string response) && Settings.Debug)
            Log.Info($"NVG input for player {player.Id}: {response}");
    }

    public bool Toggle(Player player, out string response)
    {
        if (!Settings.IsEnabled || !CanUseRole(player))
        {
            response = "Night vision is unavailable for your current role or round.";
            return false;
        }

        Session session = GetSession(player);
        double now = clock.Elapsed.TotalSeconds;
        if (now < session.NextToggleAt)
        {
            response = "Please release the key and wait before toggling again.";
            return false;
        }

        session.NextToggleAt = now + Settings.Activation.ToggleCooldownSeconds;
        return SetActive(player, session.NightVision?.IsCurrent != true, null, out response);
    }

    public bool SetAssignment(Player player, bool? allowed, string? profile, out string response)
    {
        if (!CanUseRole(player))
        {
            response = "The player must have a living first-person role.";
            return false;
        }

        if (profile is not null && !Settings.Profiles.ContainsKey(profile))
        {
            response = $"Unknown profile '{profile}'.";
            return false;
        }

        Session session = GetSession(player);
        session.Assigned = allowed;
        session.Profile = profile;
        Refresh(player);
        if (allowed == false)
            session.StopShutdownEffects();

        response = allowed switch
        {
            true => $"Night vision granted to player {player.Id} for this life.",
            false => $"Night vision revoked for player {player.Id} for this life.",
            null => $"Player {player.Id} now follows the configured rules.",
        };
        return true;
    }

    public bool SetProfile(Player player, string profile, out string response)
    {
        if (!CanUseRole(player) || !Settings.Profiles.ContainsKey(profile))
        {
            response = "Select an existing profile and a player with a living first-person role.";
            return false;
        }

        GetSession(player).Profile = profile;
        Refresh(player);
        response = $"Profile '{profile}' selected for player {player.Id} for this life.";
        return true;
    }

    public bool SetActive(Player player, bool enabled, double? seconds, out string response)
    {
        if (!enabled)
        {
            bool stopped = Stop(player, useShutdownEffects: true);
            response = stopped ? "Night vision switched off." : "The manager has no active visor for this player.";
            return stopped;
        }

        Session session = GetSession(player);
        if (!CanActivate(player, session, out VisionProfile profile, out LightSample light, out response))
            return false;

        double now = clock.Elapsed.TotalSeconds;
        if (session.NightVision?.IsCurrent == true)
        {
            session.ActiveUntil = seconds.HasValue ? now + seconds.Value : null;
            response = "Night vision is active.";
            return true;
        }

        Stop(player);
        byte intensity = TargetIntensity(profile, light);
        EffectLease? effect = EffectLease.TryStart(player, EffectType.NightVision, intensity);
        if (effect is null)
        {
            response = "The native NightVision effect is busy or could not be applied.";
            return false;
        }

        session.StopShutdownEffects();
        session.NightVision = effect;
        session.Gain = new GainTransition(intensity);
        session.ActiveUntil = seconds.HasValue ? now + seconds.Value : null;
        active.Add(player);
        response = "Night vision switched on.";
        return true;
    }

    public string Status(Player player)
    {
        sessions.TryGetValue(player, out Session session);
        AccessPolicy policy = Settings.Resolve(player);
        string profile = session?.Profile ?? policy.Profile;
        string authority = session?.Assigned switch { true => "granted", false => "revoked", _ => "YAML rules" };
        bool managed = session?.NightVision?.IsCurrent == true;
        string effect = managed ? "managed" : EffectLease.IsActive(player, EffectType.NightVision) ? "external" : "off";

        string eligibility = CanActivate(player, session, out _, out LightSample light, out string reason)
            ? "eligible"
            : reason;
        string lighting = !light.HasLocalLights ? "unknown" : light.Dark ? "dark" : "lit";
        string zone = light.Zone.ToString();
        string currentIntensity = managed && player.TryGetEffect(EffectType.NightVision, out var native)
            ? $", intensity {native.Intensity}"
            : string.Empty;
        return $"Player {player.Id} | {effect}{currentIntensity} | {profile} | {policy.Condition} | {authority}"
            + $" | {zone}, {lighting}, SCP-079 blackout: {light.Blackout} | {eligibility}";
    }

    public void Refresh(Player player)
    {
        if (!active.Contains(player) || !sessions.TryGetValue(player, out Session session))
            return;

        double now = clock.Elapsed.TotalSeconds;
        if (session.NightVision?.IsCurrent != true || (session.ActiveUntil is double end && now >= end)
            || !CanActivate(player, session, out VisionProfile profile, out LightSample light, out _))
        {
            Stop(player);
            return;
        }

        byte target = TargetIntensity(profile, light);
        float seconds = Settings.Adaptation.Enabled ? Settings.Adaptation.Seconds : 0f;
        byte intensity = session.Gain!.Advance(target, now, seconds);
        if (!session.NightVision.SetIntensity(intensity))
            Stop(player);
    }

    public void RefreshActive()
    {
        if (active.Count == 0)
            return;

        foreach (Player player in active.ToArray())
            Refresh(player);
    }

    private bool Stop(Player player, bool useShutdownEffects = false)
    {
        active.Remove(player);
        if (!sessions.TryGetValue(player, out Session session) || session.NightVision is not { } effect)
            return false;

        session.NightVision = null;
        session.Gain = null;
        session.ActiveUntil = null;

        bool wasCurrent = effect.IsCurrent;
        effect.Stop();
        if (wasCurrent && useShutdownEffects && CanUseRole(player) && !EffectLease.IsActive(player, EffectType.NightVision))
            ApplyShutdownEffects(player, session);

        return wasCurrent;
    }

    private void ApplyShutdownEffects(Player player, Session session)
    {
        ShutdownSettings config = Settings.ShutdownEffects;
        if ((!config.Blur && !config.Disorientation) || EffectLease.IsActive(player, EffectType.Flashed)
            || EffectLease.IsActive(player, EffectType.Blinded))
            return;

        if (config.Blur)
            session.Blur = EffectLease.TryStart(player, EffectType.Blurred, 1, config.Duration);
        if (config.Disorientation)
            session.Disorientation = EffectLease.TryStart(player, EffectType.Concussed, 1, config.Duration);
    }

    private byte TargetIntensity(VisionProfile profile, LightSample light) =>
        Settings.Adaptation.Enabled && light.HasLocalLights && !light.Dark ? profile.LitIntensity ?? profile.Intensity : profile.Intensity;

    private bool CanActivate(Player player, Session? session, out VisionProfile profile, out LightSample light, out string reason)
    {
        profile = null!;
        light = default;
        if (!Settings.IsEnabled || !CanUseRole(player))
        {
            reason = "Night vision is unavailable for this role or round.";
            return false;
        }

        AccessPolicy policy = Settings.Resolve(player);
        if (!(session?.Assigned ?? policy.Enabled) || policy.Condition == UsageCondition.Disabled)
        {
            reason = "The current rule does not allow this player to use night vision.";
            return false;
        }

        light = Lighting.Read(player);
        bool allowed = policy.Condition switch
        {
            UsageCondition.Always => true,
            UsageCondition.SelectedZones => policy.Zones?.Contains(light.Zone) == true,
            UsageCondition.DarkRoomsOnly => light.Dark,
            UsageCondition.BlackoutOnly => light.Blackout && light.Dark,
            _ => false,
        };
        if (!allowed)
        {
            reason = $"The current location does not meet {policy.Condition}.";
            return false;
        }

        profile = Settings.Profiles[session?.Profile ?? policy.Profile];
        reason = string.Empty;
        return true;
    }

    private static bool CanUseRole(Player player) =>
        player.IsVerified && !player.IsHost && !player.IsNPC && player.IsAlive
        && player.Role.Base is IFpcRole && !GameRound.IsEnded;

    private Session GetSession(Player player)
    {
        if (!sessions.TryGetValue(player, out Session session))
        {
            session = new Session();
            sessions.Add(player, session);
        }

        return session;
    }

    private sealed class Session
    {
        public bool? Assigned { get; set; }
        public string? Profile { get; set; }
        public bool KeyDown { get; set; }
        public double NextToggleAt { get; set; }
        public double? ActiveUntil { get; set; }
        public EffectLease? NightVision { get; set; }
        public GainTransition? Gain { get; set; }
        public EffectLease? Blur { get; set; }
        public EffectLease? Disorientation { get; set; }

        public void StopShutdownEffects()
        {
            Blur?.Stop();
            Disorientation?.Stop();
            Blur = null;
            Disorientation = null;
        }
    }
}
