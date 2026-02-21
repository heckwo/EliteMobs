using EliteMobs.Data;
using EliteMobs.Services;
using ProjectM;
using Stunlock.Core;
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using VampireCommandFramework;

namespace EliteMobs.Commands;

/// <summary>
/// Admin commands for the Elite Modifier system.
/// 
/// .em champion|warlord|apex         — Promote nearest mob to elite tier
/// .em debug [tier] [affix,affix]    — Debug: promote with specific tier + affixes
/// .em info                          — Show info on nearest elite
/// .em count                         — Show active elite count
/// .em clear                         — Cleanup stale tracking
/// .em affixes                       — List available affixes
/// .em demote                        — Demote nearest elite back to normal
/// .em purge                         — Revert ALL elites server-wide
/// .em toggles                       — Show current feature toggle states
/// </summary>
public static class EliteCommands
{
    [Command("em", description: "Elite mobs. Usage: .em champion|warlord|apex | .em info | .em debug [tier] [affix]", adminOnly: true)]
    public static void EliteMainCommand(ChatCommandContext ctx, string arg1, string arg2 = null, string arg3 = null)
    {
        switch (arg1.ToLowerInvariant())
        {
            case "info":
                HandleInfo(ctx);
                break;
            case "count":
                HandleCount(ctx);
                break;
            case "clear":
                HandleClear(ctx);
                break;
            case "purge":
                HandlePurge(ctx);
                break;
            case "demote":
                HandleDemote(ctx);
                break;
            case "nearby":
                HandleNearby(ctx);
                break;
            case "affixes":
                HandleAffixList(ctx);
                break;
            case "toggles":
                HandleToggles(ctx);
                break;
            case "debug":
                HandleDebug(ctx, arg2, arg3);
                break;
            case "champion":
            case "warlord":
            case "apex":
                HandlePromote(ctx, arg1);
                break;
            default:
                ctx.Reply("Usage: .em champion|warlord|apex | .em info | .em debug [tier] [affix] | .em toggles");
                break;
        }
    }

    static void HandlePromote(ChatCommandContext ctx, string tierName)
    {
        if (!TryParseTier(tierName, out EliteTier tier))
        {
            ctx.Reply($"Unknown tier: {tierName}. Use: champion, warlord, apex");
            return;
        }

        Entity nearest = FindNearestMob(ctx.Event.SenderCharacterEntity);
        if (nearest.Equals(Entity.Null))
        {
            ctx.Reply("No valid mob found nearby.");
            return;
        }

        string mobName = GetMobName(nearest);

        if (EliteService.IsElite(nearest))
        {
            ctx.Reply($"{mobName} is already an elite.");
            return;
        }

        bool success = EliteService.ForceElite(nearest, tier);
        if (success)
        {
            var data = EliteService.GetEliteData(nearest);
            string affixStr = data?.AffixSummary ?? "";
            string affixMsg = string.IsNullOrEmpty(affixStr) ? "" : $" [{affixStr}]";
            ctx.Reply($"Promoted {mobName} to {data?.TierName}{affixMsg}");
        }
        else
        {
            ctx.Reply($"Failed to promote {mobName}.");
        }
    }

    static void HandleDebug(ChatCommandContext ctx, string tierName, string affixName)
    {
        if (string.IsNullOrEmpty(tierName))
        {
            ctx.Reply("Usage: .em debug champion|warlord|apex [affix,affix]");
            return;
        }

        if (!TryParseTier(tierName, out EliteTier tier))
        {
            ctx.Reply($"Unknown tier: {tierName}. Use: champion, warlord, apex");
            return;
        }

        Entity nearest = FindNearestMob(ctx.Event.SenderCharacterEntity);
        if (nearest.Equals(Entity.Null))
        {
            ctx.Reply("No valid mob found nearby.");
            return;
        }

        List<EliteAffix> affixes = new();
        if (!string.IsNullOrEmpty(affixName) && !string.Equals(affixName, "none", StringComparison.OrdinalIgnoreCase))
        {
            foreach (string part in affixName.Split(','))
            {
                if (TryParseAffix(part.Trim(), out EliteAffix affix))
                {
                    affixes.Add(affix);
                }
                else
                {
                    ctx.Reply($"Unknown affix: {part.Trim()}. Use .em affixes to see options.");
                    return;
                }
            }
        }

        string mobName = GetMobName(nearest);
        bool success = EliteService.ForceElite(nearest, tier, affixes.Count > 0 ? affixes : null);

        if (success)
        {
            var data = EliteService.GetEliteData(nearest);
            string affixStr = data?.AffixSummary ?? "none";
            ctx.Reply($"[DEBUG] Promoted {mobName} to {data?.TierName} [{affixStr}]");
        }
        else
        {
            ctx.Reply($"Failed to promote {mobName}.");
        }
    }

    static void HandleInfo(ChatCommandContext ctx)
    {
        Entity nearest = FindNearestMob(ctx.Event.SenderCharacterEntity, includeEliteOnly: true);
        if (nearest.Equals(Entity.Null))
        {
            ctx.Reply("No elite mob found nearby.");
            return;
        }

        var data = EliteService.GetEliteData(nearest);
        if (data == null)
        {
            ctx.Reply("Nearest mob is not an elite.");
            return;
        }

        string mobName = GetMobName(nearest);
        float currentHp = 0f, maxHp = 0f;
        if (nearest.Has<Health>())
        {
            var h = nearest.Read<Health>();
            currentHp = h.Value;
            maxHp = h.MaxHealth._Value;
        }

        ctx.Reply($"{data.TierName} {mobName}");
        ctx.Reply($"  HP: {currentHp:F0}/{maxHp:F0} (orig: {data.OriginalMaxHealth:F0})");
        ctx.Reply($"  XP Multi: {data.XPMultiplier}x");
        ctx.Reply($"  Affixes: {(data.Affixes.Count > 0 ? data.AffixSummary : "none")}");
    }

    static void HandleCount(ChatCommandContext ctx)
    {
        ctx.Reply($"Active elites: {EliteService.ActiveCount}");
    }

    static void HandleClear(ChatCommandContext ctx)
    {
        EliteService.CleanupStale();
        ctx.Reply($"Stale elites cleaned. Active: {EliteService.ActiveCount}");
    }

    static void HandlePurge(ChatCommandContext ctx)
    {
        int count = EliteService.PurgeAll();
        ctx.Reply($"Purged {count} elites server-wide. All reverted to normal.");
    }

    static void HandleDemote(ChatCommandContext ctx)
    {
        Entity nearest = FindNearestMob(ctx.Event.SenderCharacterEntity, includeEliteOnly: true);
        if (nearest == Entity.Null)
        {
            ctx.Reply("No elite found nearby.");
            return;
        }

        string name = nearest.Has<PrefabGUID>() ? nearest.Read<PrefabGUID>().LookupName() : "Unknown";
        bool success = EliteService.DemoteElite(nearest);

        if (success)
            ctx.Reply($"Demoted {name} back to normal.");
        else
            ctx.Reply($"{name} is not a tracked elite.");
    }

    static void HandleNearby(ChatCommandContext ctx)
    {
        if (!ctx.Event.SenderCharacterEntity.Has<LocalToWorld>())
        {
            ctx.Reply("Can't determine your position.");
            return;
        }

        float3 playerPos = ctx.Event.SenderCharacterEntity.Read<LocalToWorld>().Position;
        int found = 0;

        var entities = Helper.GetEntitiesByComponentTypes<UnitLevel, Movement>();
        try
        {
            foreach (var entity in entities)
            {
                if (!Core.EntityManager.Exists(entity)) continue;
                if (!EliteService.IsElite(entity)) continue;
                if (!entity.Has<LocalToWorld>()) continue;

                float3 pos = entity.Read<LocalToWorld>().Position;
                float dist = math.distance(playerPos, pos);

                if (dist <= 50f)
                {
                    var data = EliteService.GetEliteData(entity);
                    string mobName = GetMobName(entity);
                    string affixStr = data.Affixes.Count > 0 ? $" [{data.AffixSummary}]" : "";
                    ctx.Reply($"  {data.TierName} {mobName} — {dist:F0}m away{affixStr}");
                    found++;
                }
            }
        }
        finally
        {
            entities.Dispose();
        }

        if (found == 0)
            ctx.Reply($"No elites within 50 units. Total tracked: {EliteService.ActiveCount}");
        else
            ctx.Reply($"Found {found} elite(s) nearby. Total tracked: {EliteService.ActiveCount}");
    }

    static void HandleAffixList(ChatCommandContext ctx)
    {
        ctx.Reply("Available affixes:");
        ctx.Reply("  vampiric, thorns, frenzied, ironhide");
        ctx.Reply("  summoner, berserker, chilling, shielded");
        ctx.Reply("  phasing, illusionist, bolstering");
    }

    static void HandleToggles(ChatCommandContext ctx)
    {
        ctx.Reply("Feature Toggles (edit EliteMobs.cfg to change):");
        ctx.Reply($"  AddDontSaveToMob: {Config.EliteConfig.AddDontSaveToMob}");
        ctx.Reply($"  ApplyTierAura: {Config.EliteConfig.ApplyTierAura}");
        ctx.Reply($"  ApplyBolstering: {Config.EliteConfig.ApplyBolstering}");
        ctx.Reply($"  EnableShielded: {Config.EliteConfig.EnableShielded}");
        ctx.Reply($"  EnablePhasing: {Config.EliteConfig.EnablePhasing}");
        ctx.Reply($"  AddDontSaveToBuffs: {Config.EliteConfig.AddDontSaveToBuffs}");
        ctx.Reply($"  StripBuffTriggers: {Config.EliteConfig.StripBuffTriggers}");
    }

    // ═══════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════

    static Entity FindNearestMob(Entity playerCharacter, bool includeEliteOnly = false)
    {
        if (!playerCharacter.Has<LocalToWorld>()) return Entity.Null;

        float3 playerPos = playerCharacter.Read<LocalToWorld>().Position;
        float closestDist = float.MaxValue;
        Entity closest = Entity.Null;

        var entities = Helper.GetEntitiesByComponentTypes<UnitLevel, Movement>();
        try
        {
            foreach (var entity in entities)
            {
                if (!Core.EntityManager.Exists(entity)) continue;
                if (entity.Has<PlayerCharacter>()) continue;
                if (entity.Has<VBloodConsumeSource>()) continue;
                if (entity.Has<Trader>()) continue;
                if (entity.Has<Minion>()) continue;
                if (!entity.Has<LocalToWorld>()) continue;

                if (includeEliteOnly && !EliteService.IsElite(entity)) continue;

                float3 pos = entity.Read<LocalToWorld>().Position;
                float dist = math.distance(playerPos, pos);

                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = entity;
                }
            }
        }
        finally
        {
            entities.Dispose();
        }

        return closest;
    }

    static string GetMobName(Entity mob)
    {
        if (mob.Has<PrefabGUID>())
        {
            return mob.Read<PrefabGUID>().LookupName();
        }
        return "Unknown";
    }

    static bool TryParseTier(string input, out EliteTier tier)
    {
        tier = EliteTier.None;
        if (string.IsNullOrEmpty(input)) return false;

        return input.ToLowerInvariant() switch
        {
            "champion" => Assign(out tier, EliteTier.Champion),
            "warlord" => Assign(out tier, EliteTier.Warlord),
            "apex" => Assign(out tier, EliteTier.Apex),
            _ => false
        };
    }

    static bool TryParseAffix(string input, out EliteAffix affix)
    {
        affix = EliteAffix.None;
        if (string.IsNullOrEmpty(input)) return false;

        return input.ToLowerInvariant() switch
        {
            "vampiric" => Assign(out affix, EliteAffix.Vampiric),
            "thorns" => Assign(out affix, EliteAffix.Thorns),
            "frenzied" => Assign(out affix, EliteAffix.Frenzied),
            "ironhide" => Assign(out affix, EliteAffix.Ironhide),
            "phasing" => Assign(out affix, EliteAffix.Phasing),
            "summoner" => Assign(out affix, EliteAffix.Summoner),
            "berserker" => Assign(out affix, EliteAffix.Berserker),
            "chilling" => Assign(out affix, EliteAffix.Chilling),
            "shielded" => Assign(out affix, EliteAffix.Shielded),
            "illusionist" => Assign(out affix, EliteAffix.Illusionist),
            "bolstering" => Assign(out affix, EliteAffix.Bolstering),
            _ => false
        };
    }

    static bool Assign<T>(out T target, T value) { target = value; return true; }
}
