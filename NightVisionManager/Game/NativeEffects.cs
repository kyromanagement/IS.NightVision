using System;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;

namespace InitStudio.NightVision;

internal sealed class EffectLease
{
    private readonly StatusEffectBase effect;
    private byte intensity;
    private readonly float duration;

    private EffectLease(StatusEffectBase effect)
    {
        this.effect = effect;
        intensity = effect.Intensity;
        duration = effect.Duration;
    }

    public bool IsCurrent => effect != null && effect.IsEnabled
        && effect.Intensity == intensity && effect.Duration == duration;

    public static bool IsActive(Player player, EffectType type) =>
        player.TryGetEffect(type, out StatusEffectBase effect) && effect.IsEnabled;

    public static EffectLease? TryStart(Player player, EffectType type, byte intensity, float duration = 0)
    {
        if (!player.TryGetEffect(type, out StatusEffectBase effect) || effect.IsEnabled)
            return null;

        if (!player.EnableEffect(effect, intensity, duration))
            return null;

        // A server plugin can alter the request. Treat a different result as an external effect.
        return effect.Intensity == intensity && effect.Duration == duration ? new EffectLease(effect) : null;
    }

    public bool SetIntensity(byte requested)
    {
        if (!IsCurrent)
            return false;

        if (requested == intensity)
            return true;

        effect.Intensity = requested;
        if (effect.Intensity != requested || effect.Duration != duration)
            return false;

        intensity = requested;
        return true;
    }

    public void Stop()
    {
        if (IsCurrent)
            effect.ServerDisable();
    }
}

internal sealed class GainTransition
{
    private double startedAt;
    private float duration;
    private byte from;
    private byte target;

    public GainTransition(byte intensity) => from = target = intensity;

    public byte Advance(byte requested, double now, float seconds)
    {
        if (seconds <= 0)
        {
            from = target = requested;
            return requested;
        }

        byte current = Sample(now);
        if (requested != target)
        {
            from = current;
            target = requested;
            startedAt = now;
            duration = seconds;
        }

        return current;
    }

    private byte Sample(double now)
    {
        if (from == target)
            return target;

        double progress = Math.Max(0, Math.Min(1, (now - startedAt) / duration));
        return (byte)Math.Round(from + (target - from) * progress);
    }
}
