using System.Collections.Generic;

namespace EliteMobs.Data;

public enum EliteTier
{
    None = 0,
    Champion = 1,   // Common elite: +50% HP, +25% DMG, 2x XP, no affixes
    Warlord = 2,    // Rare elite: +150% HP, +50% DMG, 2x XP, 1 affix
    Apex = 3        // Ultra rare: +300% HP, +100% DMG, 4x XP, 2 affixes
}

public enum EliteAffix
{
    None = 0,
    Vampiric,       // Heals 2% of max HP per hit dealt
    Thorns,         // Reflects 3% of elite's max HP back to attacker
    Frenzied,       // 2x attack speed
    Ironhide,       // 55% damage reduction
    Phasing,        // Every 8s, goes invisible for 2s
    Summoner,       // Spawns 2 adds when combat starts
    Berserker,      // +1% damage per 1% HP lost (BANNED from Apex)
    Chilling,       // 35% chance to freeze on hit
    Shielded,       // Periodic damage immunity (gargoyle shield)
    Illusionist,    // Spawns floating weapon decoys every 20s
    Bolstering      // Sentinel Level Aura — buffs nearby mobs
}

public class ActiveEliteData
{
    public EliteTier Tier { get; set; }
    public List<EliteAffix> Affixes { get; set; } = new();
    public int OriginalPrefabHash { get; set; }
    public float OriginalMaxHealth { get; set; }
    public float OriginalPhysPower { get; set; }
    public float OriginalSpellPower { get; set; }

    public float LastHpPercent { get; set; } = 1f;
    public bool SummonerTriggered { get; set; } = false;
    public double NextShieldTime { get; set; } = 0;
    public double NextPhaseTime { get; set; } = 0;
    public double PhaseEndTime { get; set; } = 0;
    public double NextDecoyTime { get; set; } = 0;
    public double SpawnTime { get; set; } = 0;

    public float XPMultiplier => Tier switch
    {
        EliteTier.Champion => 2f,
        EliteTier.Warlord => 2f,
        EliteTier.Apex => 4f,
        _ => 1f
    };

    public string TierName => Tier switch
    {
        EliteTier.Champion => "<color=#FFD700>[Champion]</color>",
        EliteTier.Warlord => "<color=#9B30FF>[Warlord]</color>",
        EliteTier.Apex => "<color=#FF0000>[Apex]</color>",
        _ => ""
    };

    public string AffixSummary
    {
        get
        {
            if (Affixes.Count == 0) return "";
            return string.Join(", ", Affixes);
        }
    }
}

public class EliteTierConfig
{
    public float SpawnChance { get; set; }
    public float HPMultiplier { get; set; }
    public float DMGMultiplier { get; set; }
    public float XPMultiplier { get; set; }
    public int AffixCount { get; set; }
}
