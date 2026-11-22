using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Interfaces;
using Exiled.CustomRoles.API.Features;
using PlayerRoles;
using UnityEngine;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace InitStudio.NightVision;

public sealed class ExiledConfig : IConfig
{
    public bool IsEnabled { get; set; } = true;
    public bool Debug { get; set; }
}

public enum UsageCondition
{
    Always,
    BlackoutOnly,
    DarkRoomsOnly,
    SelectedZones,
    Disabled,
}

public sealed class AccessRule
{
    public bool? Enabled { get; set; }
    public string? Profile { get; set; }
    public UsageCondition? Condition { get; set; }
    public HashSet<ZoneType>? Zones { get; set; }
    public bool? AutoActivate { get; set; }
}

public sealed class VisionProfile
{
    public byte Intensity { get; set; } = 65;
    public byte? LitIntensity { get; set; }
}

public sealed class ActivationSettings
{
    public bool CommandEnabled { get; set; } = true;
    public bool KeybindEnabled { get; set; } = true;
    public KeyCode SuggestedKey { get; set; } = KeyCode.N;
    public int SettingId { get; set; } = 45380129;
    public float ToggleCooldownSeconds { get; set; } = 0.35f;
}

public sealed class AdaptationSettings
{
    public bool Enabled { get; set; } = true;
    public float Seconds { get; set; } = 0.75f;
}

public sealed class ShutdownSettings
{
    public bool Blur { get; set; } = true;
    public bool Disorientation { get; set; }
    public float Duration { get; set; } = 1.5f;
}

public sealed class AdminPermissions
{
    public string Grant { get; set; } = "nightvision.grant";
    public string Remove { get; set; } = "nightvision.remove";
    public string Toggle { get; set; } = "nightvision.toggle";
    public string Status { get; set; } = "nightvision.status";
    public string Profile { get; set; } = "nightvision.profile";
    public string Reload { get; set; } = "nightvision.reload";

    internal void Validate()
    {
        var all = new[] { Grant, Remove, Toggle, Status, Profile, Reload };
        if (all.Any(value => string.IsNullOrWhiteSpace(value)
            || value.Split('.').Any(segment => segment.Length == 0
                || segment.Any(character => !(char.IsLetterOrDigit(character) || character is '_' or '-')))))
        {
            throw new InvalidDataException("permissions: every node must be a dot-separated permission without wildcards.");
        }
    }
}

public sealed class Settings
{
    private const string ResourceName = "NightVisionManager.DefaultSettings.yml";
    private const string Ntf = "Ntf";
    private const string Guards = "FacilityGuards";
    private const string Tutorial = "Tutorial";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    private KeyValuePair<uint, AccessRule>[] orderedCustomRoles = Array.Empty<KeyValuePair<uint, AccessRule>>();

    public bool IsEnabled { get; set; }
    public bool Debug { get; set; }
    public string DefaultProfile { get; set; } = "Standard";
    public ActivationSettings Activation { get; set; } = new();
    public AdaptationSettings Adaptation { get; set; } = new();
    public ShutdownSettings ShutdownEffects { get; set; } = new();
    public AdminPermissions Permissions { get; set; } = new();
    public Dictionary<string, VisionProfile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Standard"] = new(),
    };
    public Dictionary<string, AccessRule> Teams { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<RoleTypeId, AccessRule> Roles { get; set; } = new();
    public Dictionary<uint, AccessRule> CustomRoles { get; set; } = new();

    internal static Settings Load(string path)
    {
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using Stream template = typeof(Settings).Assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException("Embedded settings template is missing.");
            using var reader = new StreamReader(template);
            // Write the complete template first so a failed write cannot leave a partial config.
            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, reader.ReadToEnd());
                File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        using StreamReader input = File.OpenText(path);
        return Parse(input);
    }

    internal static Settings Parse(TextReader input)
    {
        string text = input.ReadToEnd();
        var documents = new YamlStream();
        using var validationReader = new StringReader(text);
        documents.Load(validationReader); // YamlDotNet 11 rejects duplicate mapping keys here.
        if (documents.Documents.Count != 1 || documents.Documents[0].RootNode is not YamlMappingNode)
            throw new InvalidDataException("Expected one YAML document with a settings mapping.");

        Settings settings = Deserializer.Deserialize<Settings>(text)
            ?? throw new InvalidDataException("The YAML document is empty.");
        settings.Validate();
        return settings;
    }

    internal AccessPolicy Resolve(Player player)
    {
        RoleTypeId role = player.Role.Type;
        var policy = new AccessPolicy(false, DefaultProfile, UsageCondition.Always, null, false);
        if (Teams.TryGetValue(player.Role.Team.ToString(), out AccessRule teamRule))
            policy = policy.Apply(teamRule);

        string? squad = role switch
        {
            RoleTypeId.NtfPrivate or RoleTypeId.NtfSergeant or RoleTypeId.NtfCaptain or RoleTypeId.NtfSpecialist => Ntf,
            RoleTypeId.FacilityGuard => Guards,
            RoleTypeId.Tutorial => Tutorial,
            _ => null,
        };
        if (squad is not null && Teams.TryGetValue(squad, out AccessRule squadRule))
            policy = policy.Apply(squadRule);

        if (Roles.TryGetValue(role, out AccessRule roleRule))
            policy = policy.Apply(roleRule);

        foreach (KeyValuePair<uint, AccessRule> configured in orderedCustomRoles)
        {
            if (CustomRole.Registered.FirstOrDefault(role => role.Id == configured.Key)?.Check(player) == true)
                policy = policy.Apply(configured.Value);
        }

        return policy;
    }

    private void Validate()
    {
        if (Activation is null || Adaptation is null || ShutdownEffects is null || Permissions is null
            || Profiles is null || Teams is null || Roles is null || CustomRoles is null)
            throw new InvalidDataException("Configuration sections must be YAML mappings.");

        Profiles = CaseInsensitive(Profiles, "profiles");
        Teams = CaseInsensitive(Teams, "teams");

        if (Profiles.Count == 0 || Profiles.Any(entry => string.IsNullOrWhiteSpace(entry.Key)
            || entry.Key.Any(char.IsWhiteSpace) || entry.Value is null
            || entry.Value.Intensity == 0 || entry.Value.LitIntensity == 0))
            throw new InvalidDataException("profiles: intensity and lit_intensity must be between 1 and 255. Names cannot contain whitespace.");

        if (string.IsNullOrWhiteSpace(DefaultProfile) || !Profiles.ContainsKey(DefaultProfile))
            throw new InvalidDataException("default_profile must name an existing profile.");

        if (Activation.SettingId <= 0 || Activation.SuggestedKey == KeyCode.None
            || !Enum.IsDefined(typeof(KeyCode), Activation.SuggestedKey))
            throw new InvalidDataException("activation: use a positive setting_id and a defined suggested_key.");

        CheckRange(Activation.ToggleCooldownSeconds, 0, float.MaxValue, "activation.toggle_cooldown_seconds");
        CheckRange(Adaptation.Seconds, 0.1f, float.MaxValue, "adaptation.seconds");
        CheckRange(ShutdownEffects.Duration, 1, 2, "shutdown_effects.duration");
        Permissions.Validate();

        var teamNames = new HashSet<string>(Enum.GetNames(typeof(Team)), StringComparer.OrdinalIgnoreCase)
        {
            Ntf, Guards, Tutorial,
        };
        foreach (var entry in Teams)
        {
            if (!teamNames.Contains(entry.Key))
                throw new InvalidDataException($"Unknown team '{entry.Key}'.");
            ValidateRule(entry.Value, $"teams.{entry.Key}");
        }

        foreach (var entry in Roles)
        {
            if (!Enum.IsDefined(typeof(RoleTypeId), entry.Key))
                throw new InvalidDataException($"Unknown role '{entry.Key}'.");
            ValidateRule(entry.Value, $"roles.{entry.Key}");
        }

        foreach (var entry in CustomRoles)
            ValidateRule(entry.Value, $"custom_roles.{entry.Key}");

        orderedCustomRoles = CustomRoles.OrderByDescending(entry => entry.Key).ToArray();
    }

    private void ValidateRule(AccessRule? rule, string path)
    {
        if (rule is null || (rule.Profile is not null && !Profiles.ContainsKey(rule.Profile))
            || (rule.Condition.HasValue && !Enum.IsDefined(typeof(UsageCondition), rule.Condition.Value))
            || (rule.Zones?.Any(zone => zone == ZoneType.Unspecified || !Enum.IsDefined(typeof(ZoneType), zone)) ?? false))
            throw new InvalidDataException($"{path}: invalid rule, profile, condition or zone.");
    }

    private static Dictionary<string, T> CaseInsensitive<T>(Dictionary<string, T> values, string path)
    {
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in values)
        {
            if (result.ContainsKey(entry.Key))
                throw new InvalidDataException($"{path}: duplicate entry '{entry.Key}'.");
            result.Add(entry.Key, entry.Value);
        }

        return result;
    }

    private static void CheckRange(float value, float minimum, float maximum, string key)
    {
        if (float.IsNaN(value) || value < minimum || value > maximum)
            throw new InvalidDataException($"{key}: value is outside the supported range (minimum {minimum}).");
    }
}

internal readonly struct AccessPolicy
{
    public AccessPolicy(bool enabled, string profile, UsageCondition condition, HashSet<ZoneType>? zones, bool autoActivate)
    {
        Enabled = enabled;
        Profile = profile;
        Condition = condition;
        Zones = zones;
        AutoActivate = autoActivate;
    }

    public bool Enabled { get; }
    public string Profile { get; }
    public UsageCondition Condition { get; }
    public HashSet<ZoneType>? Zones { get; }
    public bool AutoActivate { get; }

    public AccessPolicy Apply(AccessRule rule) => new(
        rule.Enabled ?? Enabled, rule.Profile ?? Profile, rule.Condition ?? Condition,
        rule.Zones ?? Zones, rule.AutoActivate ?? AutoActivate);
}
