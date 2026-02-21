using System;
using System.Collections.Generic;
using System.Linq;
using EliteMobs.Config;
using EliteMobs.Data;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace EliteMobs.Services;

/// <summary>
/// Elite Modifier system — randomly upgrades regular mobs into Champions, Warlords, or Apex.
/// 
/// ALL features are enabled by default. Each suspected corruption source has a config toggle
/// under [Feature Toggles] in the .cfg file so they can be selectively disabled for testing.
/// 
/// SUSPECTED CORRUPTION SOURCES (all involve archetype changes on entities):
///   1. AddDontSaveToMob — adds DontSaveEntity component to the mob entity
///   2. ApplyTierAura — creates buff entity, then strips 5 components from it
///   3. StripBuffTriggers — removes RemoveBuffOnGameplayEvent etc. from buff entities
///   4. AddDontSaveToBuffs — adds DontSaveEntity to buff entities
///   5. ApplyBolstering — creates Sentinel Level Aura buff entity
///   6. EnableShielded — applies Gargoyle WingShield buff periodically
///   7. EnablePhasing — applies Stealth buff periodically
/// 
/// SAFE operations (value-only writes, no archetype changes):
///   - ScaleStats (Health.MaxHealth._Value, UnitStats.PhysicalPower._Value)
///   - ApplyFrenzied (AbilityBar_Shared._Value)
///   - ApplyIronhide (UnitStats.DamageReduction._Value)
///   - UpdateBerserker (UnitStats._Value)
/// </summary>
public static class EliteService
{
    static readonly System.Random _rng = new();

    // ═══════════════════════════════════════════════════════════
    //  RUNTIME TRACKING
    // ═══════════════════════════════════════════════════════════

    static readonly Dictionary<Entity, ActiveEliteData> _activeElites = new();

    // ═══════════════════════════════════════════════════════════
    //  BUFF PrefabGUIDs
    // ═══════════════════════════════════════════════════════════

    // Tier auras (shiny-style buffs)
    static readonly PrefabGUID ChampionAura = new(-1576512627);  // Storm/gold
    static readonly PrefabGUID WarlordAura = new(348724578);     // Chaos/purple
    static readonly PrefabGUID ApexAura = new(-1246704569);      // Blood/red

    // Affix auras
    static readonly Dictionary<EliteAffix, PrefabGUID> AffixAuras = new()
    {
        { EliteAffix.Vampiric,    new(-1246704569) },
        { EliteAffix.Thorns,      new(1723455773) },
        { EliteAffix.Frenzied,    new(-1576512627) },
        { EliteAffix.Ironhide,    new(1723455773) },
        { EliteAffix.Phasing,     new(348724578) },
        { EliteAffix.Summoner,    new(27300215) },
        { EliteAffix.Berserker,   new(-1246704569) },
        { EliteAffix.Chilling,    new(27300215) },
        { EliteAffix.Shielded,    new(-528753799) },
        { EliteAffix.Illusionist, new(348724578) },
        { EliteAffix.Bolstering,  new(-1576512627) },
    };

    static readonly PrefabGUID GargoyleShieldBuff = new(-528753799);
    static readonly PrefabGUID InCombatBuff = new(581443919);
    static readonly PrefabGUID StealthBuff = new(-361911593);
    static readonly PrefabGUID LevelAuraSelf = new(-2104035188);

    static readonly EliteAffix[] AllAffixes = (EliteAffix[])Enum.GetValues(typeof(EliteAffix));

    static bool _initialized;

    // ═══════════════════════════════════════════════════════════
    //  INITIALIZATION
    // ═══════════════════════════════════════════════════════════

    public static void Initialize()
    {
        _initialized = true;
        Core.Log.LogInfo("[EliteMobs] Elite Modifier system initialized.");
    }

    // ═══════════════════════════════════════════════════════════
    //  SPAWN PROCESSING
    // ═══════════════════════════════════════════════════════════

    public static bool TryMakeElite(Entity mob)
    {
        if (!_initialized || !EliteConfig.Enabled) return false;
        if (_activeElites.ContainsKey(mob)) return false;
        if (mob.Has<VBloodConsumeSource>()) return false;
        if (!mob.Has<UnitLevel>()) return false;
        if (!mob.Has<Movement>()) return false;
        if (mob.Has<Trader>()) return false;
        if (mob.Has<Minion>()) return false;

        EliteTier tier = RollTier();
        if (tier == EliteTier.None) return false;

        return PromoteToElite(mob, tier);
    }

    public static bool ForceElite(Entity mob, EliteTier tier, List<EliteAffix> forcedAffixes = null)
    {
        if (!_initialized) return false;
        _activeElites.Remove(mob);
        return PromoteToElite(mob, tier, forcedAffixes);
    }

    static bool PromoteToElite(Entity mob, EliteTier tier, List<EliteAffix> forcedAffixes = null)
    {
        try
        {
            var eliteData = new ActiveEliteData
            {
                Tier = tier,
                OriginalPrefabHash = mob.Has<PrefabGUID>() ? mob.Read<PrefabGUID>().GuidHash : 0,
                SpawnTime = Core.ServerTime,
            };

            // ── Capture original stats ──
            if (mob.Has<Health>())
            {
                var health = mob.Read<Health>();
                eliteData.OriginalMaxHealth = health.MaxHealth._Value;
            }
            if (mob.Has<UnitStats>())
            {
                var stats = mob.Read<UnitStats>();
                eliteData.OriginalPhysPower = stats.PhysicalPower._Value;
                eliteData.OriginalSpellPower = stats.SpellPower._Value;
            }

            // ── Scale stats (SAFE — value-only writes) ──
            var tierConfig = GetTierConfig(tier);
            ScaleStats(mob, tierConfig);

            // ── Assign affixes ──
            if (forcedAffixes != null)
            {
                eliteData.Affixes = new List<EliteAffix>(forcedAffixes);
            }
            else
            {
                eliteData.Affixes = RollAffixes(tier, tierConfig.AffixCount);
            }

            // ── Apply visual aura (SUSPECTED — creates buff entity) ──
            if (EliteConfig.ApplyTierAura)
            {
                ApplyTierAura(mob, tier);
                Core.Log.LogInfo($"[EliteMobs] Applied tier aura for {tier}");
            }

            // ── Apply affix effects ──
            foreach (var affix in eliteData.Affixes)
            {
                switch (affix)
                {
                    case EliteAffix.Frenzied:
                        ApplyFrenzied(mob);      // SAFE — value-only
                        break;
                    case EliteAffix.Ironhide:
                        ApplyIronhide(mob);      // SAFE — value-only
                        break;
                    case EliteAffix.Illusionist:
                        eliteData.NextDecoyTime = Core.ServerTime + 20.0;
                        break;
                    case EliteAffix.Bolstering:
                        if (EliteConfig.ApplyBolstering)
                        {
                            ApplyBolstering(mob); // SUSPECTED — creates buff entity
                            Core.Log.LogInfo($"[EliteMobs] Applied Bolstering aura");
                        }
                        break;
                }
            }

            // ── DontSaveEntity on mob (SUSPECTED — archetype change) ──
            if (EliteConfig.AddDontSaveToMob)
            {
                if (!mob.Has<PersistenceV2.DontSaveEntity>())
                {
                    mob.Add<PersistenceV2.DontSaveEntity>();
                    Core.Log.LogInfo($"[EliteMobs] Added DontSaveEntity to mob");
                }
            }

            // ── Track ──
            _activeElites[mob] = eliteData;

            string affixStr = eliteData.Affixes.Count > 0 ? $" [{eliteData.AffixSummary}]" : "";
            Core.Log.LogInfo($"[EliteMobs] Promoted mob to {tier}{affixStr} (prefab: {eliteData.OriginalPrefabHash})");

            return true;
        }
        catch (Exception e)
        {
            Core.Log.LogWarning($"[EliteMobs] PromoteToElite error: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  TIER ROLLING
    // ═══════════════════════════════════════════════════════════

    static EliteTier RollTier()
    {
        double roll = _rng.NextDouble();

        if (roll < EliteConfig.ApexChance) return EliteTier.Apex;
        if (roll < EliteConfig.ApexChance + EliteConfig.WarlordChance) return EliteTier.Warlord;
        if (roll < EliteConfig.ApexChance + EliteConfig.WarlordChance + EliteConfig.ChampionChance) return EliteTier.Champion;

        return EliteTier.None;
    }

    static List<EliteAffix> RollAffixes(EliteTier tier, int count)
    {
        var affixes = new List<EliteAffix>();
        if (count <= 0) return affixes;

        // Build rollable pool based on config toggles
        var pool = AllAffixes
            .Where(a => a != EliteAffix.None)
            .Where(a => a != EliteAffix.Shielded || EliteConfig.EnableShielded)
            .Where(a => a != EliteAffix.Phasing || EliteConfig.EnablePhasing)
            .Where(a => a != EliteAffix.Bolstering || EliteConfig.ApplyBolstering)
            .ToList();

        if (tier == EliteTier.Apex)
            pool.Remove(EliteAffix.Berserker);

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int idx = _rng.Next(pool.Count);
            affixes.Add(pool[idx]);
        }

        return affixes;
    }

    // ═══════════════════════════════════════════════════════════
    //  STAT SCALING (SAFE — value-only writes)
    // ═══════════════════════════════════════════════════════════

    static void ScaleStats(Entity mob, EliteTierConfig config)
    {
        if (mob.Has<Health>())
        {
            mob.With((ref Health health) =>
            {
                health.MaxHealth._Value *= (1f + config.HPMultiplier);
                health.Value = health.MaxHealth._Value;
                health.MaxRecoveryHealth = health.MaxHealth._Value;
            });
        }

        if (mob.Has<UnitStats>())
        {
            mob.With((ref UnitStats stats) =>
            {
                stats.PhysicalPower._Value *= (1f + config.DMGMultiplier);
                stats.SpellPower._Value *= (1f + config.DMGMultiplier);
            });
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  AFFIX APPLICATION (at spawn)
    // ═══════════════════════════════════════════════════════════

    static void ApplyFrenzied(Entity mob)
    {
        if (mob.Has<AbilityBar_Shared>())
        {
            mob.With((ref AbilityBar_Shared abilityBar) =>
            {
                abilityBar.AbilityAttackSpeed._Value *= 2f;
                abilityBar.PrimaryAttackSpeed._Value *= 2f;
            });
        }
    }

    static void ApplyIronhide(Entity mob)
    {
        if (mob.Has<UnitStats>())
        {
            mob.With((ref UnitStats stats) =>
            {
                stats.DamageReduction._Value += 0.55f;
            });
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  VISUAL AURAS (SUSPECTED — buff entity + component stripping)
    // ═══════════════════════════════════════════════════════════

    static void ApplyTierAura(Entity mob, EliteTier tier)
    {
        PrefabGUID aura = tier switch
        {
            EliteTier.Champion => ChampionAura,
            EliteTier.Warlord => WarlordAura,
            EliteTier.Apex => ApexAura,
            _ => default
        };

        if (aura.GuidHash != 0)
        {
            if (BuffHelper.TryApplyAndGetBuff(mob, aura, out Entity buffEntity))
            {
                // Make permanent
                if (buffEntity.Has<LifeTime>())
                {
                    buffEntity.With((ref LifeTime lt) =>
                    {
                        lt.Duration = 0f;
                        lt.EndAction = LifeTimeEndAction.None;
                    });
                }

                // SUSPECTED: Strip removal triggers (archetype changes on buff entity)
                if (EliteConfig.StripBuffTriggers)
                {
                    if (buffEntity.Has<RemoveBuffOnGameplayEvent>())
                        buffEntity.Remove<RemoveBuffOnGameplayEvent>();
                    if (buffEntity.Has<RemoveBuffOnGameplayEventEntry>())
                        buffEntity.Remove<RemoveBuffOnGameplayEventEntry>();
                    if (buffEntity.Has<DestroyOnGameplayEvent>())
                        buffEntity.Remove<DestroyOnGameplayEvent>();
                    if (buffEntity.Has<CreateGameplayEventsOnSpawn>())
                        buffEntity.Remove<CreateGameplayEventsOnSpawn>();
                    if (buffEntity.Has<GameplayEventListeners>())
                        buffEntity.Remove<GameplayEventListeners>();
                    Core.Log.LogInfo($"[EliteMobs] Stripped buff triggers from tier aura entity");
                }

                // SUSPECTED: DontSaveEntity on buff entity (archetype change)
                if (EliteConfig.AddDontSaveToBuffs)
                {
                    if (!buffEntity.Has<PersistenceV2.DontSaveEntity>())
                        buffEntity.Add<PersistenceV2.DontSaveEntity>();
                    Core.Log.LogInfo($"[EliteMobs] Added DontSaveEntity to tier aura buff entity");
                }
            }
        }
    }

    /// <summary>
    /// Bolstering: Apply Sentinel Level Aura (SUSPECTED — creates buff entity).
    /// </summary>
    static void ApplyBolstering(Entity mob)
    {
        if (BuffHelper.TryApplyAndGetBuff(mob, LevelAuraSelf, out Entity buffEntity))
        {
            if (buffEntity.Has<LifeTime>())
            {
                buffEntity.With((ref LifeTime lt) =>
                {
                    lt.Duration = 0f;
                    lt.EndAction = LifeTimeEndAction.None;
                });
            }

            if (EliteConfig.AddDontSaveToBuffs)
            {
                if (!buffEntity.Has<PersistenceV2.DontSaveEntity>())
                    buffEntity.Add<PersistenceV2.DontSaveEntity>();
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  PUBLIC QUERIES
    // ═══════════════════════════════════════════════════════════

    public static ActiveEliteData GetEliteData(Entity entity)
    {
        return _activeElites.TryGetValue(entity, out var data) ? data : null;
    }

    public static bool IsElite(Entity entity) => _activeElites.ContainsKey(entity);

    public static int ActiveCount => _activeElites.Count;

    // ═══════════════════════════════════════════════════════════
    //  CLEANUP
    // ═══════════════════════════════════════════════════════════

    public static ActiveEliteData OnEliteDeath(Entity entity)
    {
        if (_activeElites.TryGetValue(entity, out var data))
        {
            _activeElites.Remove(entity);
            return data;
        }
        return null;
    }

    public static bool DemoteElite(Entity entity)
    {
        if (!_activeElites.TryGetValue(entity, out var data)) return false;

        try
        {
            if (entity.Has<Health>() && data.OriginalMaxHealth > 0)
            {
                entity.With((ref Health h) =>
                {
                    h.MaxHealth._Value = data.OriginalMaxHealth;
                    h.Value = System.Math.Min(h.Value, data.OriginalMaxHealth);
                });
            }

            if (entity.Has<UnitStats>())
            {
                entity.With((ref UnitStats stats) =>
                {
                    if (data.OriginalPhysPower > 0)
                        stats.PhysicalPower._Value = data.OriginalPhysPower;
                    if (data.OriginalSpellPower > 0)
                        stats.SpellPower._Value = data.OriginalSpellPower;
                    stats.DamageReduction._Value = 0f;
                });
            }

            if (data.Affixes.Contains(EliteAffix.Frenzied) && entity.Has<AbilityBar_Shared>())
            {
                entity.With((ref AbilityBar_Shared abs) =>
                {
                    abs.AbilityAttackSpeed._Value /= 2f;
                    abs.PrimaryAttackSpeed._Value /= 2f;
                });
            }

            BuffHelper.TryRemoveBuff(entity, ChampionAura);
            BuffHelper.TryRemoveBuff(entity, WarlordAura);
            BuffHelper.TryRemoveBuff(entity, ApexAura);
            BuffHelper.TryRemoveBuff(entity, GargoyleShieldBuff);
            BuffHelper.TryRemoveBuff(entity, StealthBuff);
            BuffHelper.TryRemoveBuff(entity, LevelAuraSelf);
        }
        catch (System.Exception e)
        {
            Core.Log.LogWarning($"[EliteMobs] Demote error: {e.Message}");
        }

        _activeElites.Remove(entity);
        return true;
    }

    public static int PurgeAll()
    {
        var elites = new List<Entity>(_activeElites.Keys);
        int count = 0;

        foreach (var entity in elites)
        {
            if (!Core.EntityManager.Exists(entity))
            {
                _activeElites.Remove(entity);
                continue;
            }

            if (DemoteElite(entity))
                count++;
        }

        return count;
    }

    public static void CleanupStale()
    {
        if (_activeElites.Count == 0) return;

        var stale = new List<Entity>();
        foreach (var kvp in _activeElites)
        {
            if (!Core.EntityManager.Exists(kvp.Key))
                stale.Add(kvp.Key);
        }

        foreach (var e in stale)
            _activeElites.Remove(e);
    }

    // ═══════════════════════════════════════════════════════════
    //  TICK — per-frame affix behaviors
    // ═══════════════════════════════════════════════════════════

    public static void Tick()
    {
        if (!_initialized || _activeElites.Count == 0) return;

        double now = Core.ServerTime;
        float maxLifetime = EliteConfig.MaxLifetimeSeconds;
        List<Entity> timedOut = null;

        foreach (var kvp in _activeElites)
        {
            Entity mob = kvp.Key;
            ActiveEliteData data = kvp.Value;

            if (!Core.EntityManager.Exists(mob)) continue;

            if (maxLifetime > 0 && now - data.SpawnTime > maxLifetime)
            {
                timedOut ??= new List<Entity>();
                timedOut.Add(mob);
                continue;
            }

            bool inCombat = BuffHelper.HasBuff(mob, InCombatBuff);

            foreach (var affix in data.Affixes)
            {
                switch (affix)
                {
                    case EliteAffix.Shielded:
                        if (EliteConfig.EnableShielded && inCombat && now >= data.NextShieldTime)
                        {
                            BuffHelper.TryApplyBuff(mob, GargoyleShieldBuff);
                            data.NextShieldTime = now + EliteConfig.ShieldInterval;
                            Core.Log.LogInfo($"[EliteMobs] Shielded: Applied gargoyle shield");
                        }
                        break;

                    case EliteAffix.Berserker:
                        UpdateBerserker(mob, data);
                        break;

                    case EliteAffix.Phasing:
                        if (EliteConfig.EnablePhasing)
                            TickPhasing(mob, data, now);
                        break;

                    case EliteAffix.Illusionist:
                        TickIllusionist(mob, data, now);
                        break;
                }
            }
        }

        if (timedOut != null)
        {
            foreach (var mob in timedOut)
            {
                _activeElites.Remove(mob);
                try
                {
                    if (Core.EntityManager.Exists(mob))
                    {
                        Core.EntityManager.DestroyEntity(mob);
                        Core.Log.LogInfo($"[EliteMobs] Timed out elite destroyed.");
                    }
                }
                catch { }
            }
        }
    }

    static void UpdateBerserker(Entity mob, ActiveEliteData data)
    {
        if (!mob.Has<Health>()) return;

        var health = mob.Read<Health>();
        float maxHp = health.MaxHealth._Value;
        if (maxHp <= 0) return;

        float hpPercent = health.Value / maxHp;
        float hpLostPercent = 1f - hpPercent;
        float dmgMult = 1f + hpLostPercent;

        var tierConfig = GetTierConfig(data.Tier);
        float tierDmgMult = 1f + tierConfig.DMGMultiplier;

        if (mob.Has<UnitStats>())
        {
            mob.With((ref UnitStats stats) =>
            {
                stats.PhysicalPower._Value = data.OriginalPhysPower > 0
                    ? data.OriginalPhysPower * tierDmgMult * dmgMult
                    : stats.PhysicalPower._Value;
                stats.SpellPower._Value = data.OriginalSpellPower > 0
                    ? data.OriginalSpellPower * tierDmgMult * dmgMult
                    : stats.SpellPower._Value;
            });
        }

        data.LastHpPercent = hpPercent;
    }

    static void TickPhasing(Entity mob, ActiveEliteData data, double now)
    {
        if (data.PhaseEndTime > 0 && now >= data.PhaseEndTime)
        {
            BuffHelper.TryRemoveBuff(mob, StealthBuff);
            if (EliteConfig.ApplyTierAura)
                ApplyTierAura(mob, data.Tier);
            data.PhaseEndTime = 0;
        }

        if (now >= data.NextPhaseTime)
        {
            BuffHelper.TryApplyBuff(mob, StealthBuff);
            data.PhaseEndTime = now + 2.0;
            data.NextPhaseTime = now + 8.0;
        }
    }

    // Illusionist floating weapon pool
    static readonly PrefabGUID[] IllusionistSpawns = new[]
    {
        new PrefabGUID(1971653132),  // FloatingWeapon_Axe
        new PrefabGUID(-1099451233), // FloatingWeapon_Base
        new PrefabGUID(-55245645),   // FloatingWeapon_Mace
        new PrefabGUID(769910415),   // FloatingWeapon_Slashers
        new PrefabGUID(233127264),   // FloatingWeapon_Spear
        new PrefabGUID(-2020619708), // FloatingWeapon_Sword
    };

    static void TickIllusionist(Entity mob, ActiveEliteData data, double now)
    {
        if (now < data.NextDecoyTime) return;
        data.NextDecoyTime = now + 20.0;

        try
        {
            if (!mob.Has<Translation>() || !mob.Has<UnitLevel>()) return;

            var pos = mob.Read<Translation>().Value;
            int eliteLevel = (int)mob.Read<UnitLevel>().Level._Value;
            int spawnLevel = System.Math.Max(1, eliteLevel - 10);

            int spawned = 0;
            for (int i = 0; i < 2; i++)
            {
                if (_rng.NextDouble() > 0.50) continue;

                PrefabGUID pick = IllusionistSpawns[_rng.Next(IllusionistSpawns.Length)];
                int level = spawnLevel;

                SpawnService.SpawnWithCallback(pick, pos, 10f, (entity) =>
                {
                    if (entity.Has<UnitLevel>())
                    {
                        entity.With((ref UnitLevel ul) => ul.Level._Value = level);
                    }
                });

                spawned++;
            }

            if (spawned > 0)
                Core.Log.LogInfo($"[EliteMobs] Illusionist spawned {spawned} weapon(s) at level {spawnLevel}");
        }
        catch (System.Exception e)
        {
            Core.Log.LogWarning($"[EliteMobs] Illusionist error: {e.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  TIER CONFIG
    // ═══════════════════════════════════════════════════════════

    public static EliteTierConfig GetTierConfig(EliteTier tier)
    {
        return tier switch
        {
            EliteTier.Champion => new EliteTierConfig
            {
                SpawnChance = EliteConfig.ChampionChance,
                HPMultiplier = EliteConfig.ChampionHP,
                DMGMultiplier = EliteConfig.ChampionDMG,
                XPMultiplier = 2f,
                AffixCount = 0
            },
            EliteTier.Warlord => new EliteTierConfig
            {
                SpawnChance = EliteConfig.WarlordChance,
                HPMultiplier = EliteConfig.WarlordHP,
                DMGMultiplier = EliteConfig.WarlordDMG,
                XPMultiplier = 2f,
                AffixCount = 1
            },
            EliteTier.Apex => new EliteTierConfig
            {
                SpawnChance = EliteConfig.ApexChance,
                HPMultiplier = EliteConfig.ApexHP,
                DMGMultiplier = EliteConfig.ApexDMG,
                XPMultiplier = 4f,
                AffixCount = 2
            },
            _ => new EliteTierConfig()
        };
    }
}
