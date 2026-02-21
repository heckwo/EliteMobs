using BepInEx.Configuration;

namespace EliteMobs.Config;

/// <summary>
/// BepInEx .cfg configuration for elite mobs.
/// All settings appear in BepInEx/config/EliteMobs.cfg
/// </summary>
public static class EliteConfig
{
    // ═══════════════════════════════════════════════════════════
    //  PUBLIC ACCESSORS
    // ═══════════════════════════════════════════════════════════

    public static bool Enabled => _enabled?.Value ?? true;

    // Spawn chances (probability per mob spawn)
    public static float ChampionChance => _championChance?.Value ?? 0.07f;
    public static float WarlordChance => _warlordChance?.Value ?? 0.02f;
    public static float ApexChance => _apexChance?.Value ?? 0.003f;

    // Stat scaling
    public static float ChampionHP => _championHP?.Value ?? 0.50f;
    public static float ChampionDMG => _championDMG?.Value ?? 0.25f;
    public static float WarlordHP => _warlordHP?.Value ?? 1.50f;
    public static float WarlordDMG => _warlordDMG?.Value ?? 0.50f;
    public static float ApexHP => _apexHP?.Value ?? 3.0f;
    public static float ApexDMG => _apexDMG?.Value ?? 1.0f;

    // Affix settings
    public static float ShieldInterval => _shieldInterval?.Value ?? 15f;
    public static float MaxLifetimeSeconds => _maxLifetime?.Value ?? 300f;

    // ═══════════════════════════════════════════════════════════
    //  FEATURE TOGGLES — for isolating corruption source
    // ═══════════════════════════════════════════════════════════

    /// <summary>If true, adds DontSaveEntity component to promoted mobs.</summary>
    public static bool AddDontSaveToMob => _addDontSave?.Value ?? true;

    /// <summary>If true, applies visual tier aura buff (creates buff entity, strips components).</summary>
    public static bool ApplyTierAura => _applyTierAura?.Value ?? true;

    /// <summary>If true, applies Bolstering buff (Sentinel Level Aura).</summary>
    public static bool ApplyBolstering => _applyBolstering?.Value ?? true;

    /// <summary>If true, enables Shielded affix (Gargoyle WingShield buff).</summary>
    public static bool EnableShielded => _enableShielded?.Value ?? true;

    /// <summary>If true, enables Phasing affix (Stealth buff).</summary>
    public static bool EnablePhasing => _enablePhasing?.Value ?? true;

    /// <summary>If true, adds DontSaveEntity to buff entities created by auras.</summary>
    public static bool AddDontSaveToBuffs => _addDontSaveToBuffs?.Value ?? true;

    /// <summary>If true, strips removal trigger components from buff entities.</summary>
    public static bool StripBuffTriggers => _stripBuffTriggers?.Value ?? true;

    // ═══════════════════════════════════════════════════════════
    //  BACKING FIELDS
    // ═══════════════════════════════════════════════════════════

    static ConfigEntry<bool> _enabled;
    static ConfigEntry<float> _championChance, _warlordChance, _apexChance;
    static ConfigEntry<float> _championHP, _championDMG;
    static ConfigEntry<float> _warlordHP, _warlordDMG;
    static ConfigEntry<float> _apexHP, _apexDMG;
    static ConfigEntry<float> _shieldInterval, _maxLifetime;

    // Feature toggles
    static ConfigEntry<bool> _addDontSave;
    static ConfigEntry<bool> _applyTierAura;
    static ConfigEntry<bool> _applyBolstering;
    static ConfigEntry<bool> _enableShielded;
    static ConfigEntry<bool> _enablePhasing;
    static ConfigEntry<bool> _addDontSaveToBuffs;
    static ConfigEntry<bool> _stripBuffTriggers;

    public static void Initialize(ConfigFile cfg)
    {
        // ── Core Settings ──
        _enabled = cfg.Bind("Elite Modifiers", "Enabled", true,
            "Enable elite mob modifier system");

        // ── Spawn Chances ──
        _championChance = cfg.Bind("Spawn Chances", "ChampionSpawnChance", 0.07f,
            "Chance for a mob to become Champion (7%)");
        _warlordChance = cfg.Bind("Spawn Chances", "WarlordSpawnChance", 0.02f,
            "Chance for a mob to become Warlord (2%)");
        _apexChance = cfg.Bind("Spawn Chances", "ApexSpawnChance", 0.003f,
            "Chance for a mob to become Apex (0.3%)");

        // ── Stat Scaling ──
        _championHP = cfg.Bind("Stat Scaling", "ChampionHPBonus", 0.50f,
            "Champion HP multiplier bonus (0.50 = +50%)");
        _championDMG = cfg.Bind("Stat Scaling", "ChampionDMGBonus", 0.25f,
            "Champion DMG multiplier bonus (0.25 = +25%)");
        _warlordHP = cfg.Bind("Stat Scaling", "WarlordHPBonus", 1.50f,
            "Warlord HP multiplier bonus (1.50 = +150%)");
        _warlordDMG = cfg.Bind("Stat Scaling", "WarlordDMGBonus", 0.50f,
            "Warlord DMG multiplier bonus (0.50 = +50%)");
        _apexHP = cfg.Bind("Stat Scaling", "ApexHPBonus", 3.0f,
            "Apex HP multiplier bonus (3.0 = +300%)");
        _apexDMG = cfg.Bind("Stat Scaling", "ApexDMGBonus", 1.0f,
            "Apex DMG multiplier bonus (1.0 = +100%)");

        // ── Affix Settings ──
        _shieldInterval = cfg.Bind("Affix Settings", "ShieldedReapplySeconds", 15f,
            "Seconds between Shielded buff re-applications");
        _maxLifetime = cfg.Bind("Affix Settings", "MaxLifetimeSeconds", 300f,
            "Max seconds an elite can live before auto-removal (0 = infinite)");

        // ── Feature Toggles (for testing) ──
        _addDontSave = cfg.Bind("Feature Toggles", "AddDontSaveToMob", true,
            "SUSPECTED CORRUPTION SOURCE: Adds DontSaveEntity component to elite mobs (archetype change)");
        _applyTierAura = cfg.Bind("Feature Toggles", "ApplyTierAura", true,
            "SUSPECTED CORRUPTION SOURCE: Applies visual tier aura buff (creates buff entity, strips components)");
        _applyBolstering = cfg.Bind("Feature Toggles", "ApplyBolstering", true,
            "SUSPECTED CORRUPTION SOURCE: Applies Bolstering buff (Sentinel Level Aura)");
        _enableShielded = cfg.Bind("Feature Toggles", "EnableShielded", true,
            "SUSPECTED CORRUPTION SOURCE: Enables Shielded affix (Gargoyle WingShield buff)");
        _enablePhasing = cfg.Bind("Feature Toggles", "EnablePhasing", true,
            "SUSPECTED CORRUPTION SOURCE: Enables Phasing affix (Stealth buff)");
        _addDontSaveToBuffs = cfg.Bind("Feature Toggles", "AddDontSaveToBuffs", true,
            "SUSPECTED CORRUPTION SOURCE: Adds DontSaveEntity to buff entities created by auras");
        _stripBuffTriggers = cfg.Bind("Feature Toggles", "StripBuffTriggers", true,
            "SUSPECTED CORRUPTION SOURCE: Strips RemoveBuffOnGameplayEvent etc. from buff entities");

        Core.Log.LogInfo($"[EliteMobs] Config loaded. Enabled={Enabled}, " +
            $"Chances: C={ChampionChance:P1} W={WarlordChance:P1} A={ApexChance:P1}");
        Core.Log.LogInfo($"[EliteMobs] Feature toggles: DontSaveMob={AddDontSaveToMob}, " +
            $"TierAura={ApplyTierAura}, Bolstering={ApplyBolstering}, " +
            $"Shielded={EnableShielded}, Phasing={EnablePhasing}, " +
            $"DontSaveBuffs={AddDontSaveToBuffs}, StripTriggers={StripBuffTriggers}");
    }
}
