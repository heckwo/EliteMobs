using System;
using EliteMobs.Data;
using EliteMobs.Services;
using HarmonyLib;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;

namespace EliteMobs.Patches;

/// <summary>
/// Hooks StatChangeSystem to handle elite affix combat behaviors:
///   - Vampiric: Heal 2% max HP on hit dealt
///   - Chilling: 35% chance to freeze target on hit
///   - Thorns: Reflect 3% max HP to attacker on hit taken
///   - Summoner: Spawn 2 adds on first combat
/// </summary>
[HarmonyPatch]
internal static class EliteCombatPatch
{
    static readonly System.Random _rng = new();
    static bool _loggedOnce = false;

    static readonly PrefabGUID ChillDebuff = new(-948292568); // Buff_General_Freeze

    [HarmonyPatch(typeof(StatChangeSystem), nameof(StatChangeSystem.OnUpdate))]
    [HarmonyPrefix]
    static void OnUpdatePrefix(StatChangeSystem __instance)
    {
        if (!Core.IsReady) return;

        if (!_loggedOnce)
        {
            Core.Log.LogInfo("[EliteMobs:Combat] StatChangeSystem patch is ACTIVE");
            _loggedOnce = true;
        }

        NativeArray<Entity> entities = default;
        NativeArray<DamageTakenEvent> damageTakenEvents = default;

        try
        {
            entities = __instance._DamageTakenEventQuery.ToEntityArray(Allocator.Temp);
            damageTakenEvents = __instance._DamageTakenEventQuery.ToComponentDataArray<DamageTakenEvent>(Allocator.Temp);
        }
        catch
        {
            return;
        }

        try
        {
            var sgm = Core.ServerGameManager;

            for (int i = 0; i < entities.Length; i++)
            {
                DamageTakenEvent damageTakenEvent = damageTakenEvents[i];

                Entity sourceOwner;
                try
                {
                    sourceOwner = sgm.GetOwner(damageTakenEvent.Source);
                }
                catch
                {
                    continue;
                }

                Entity target = damageTakenEvent.Entity;

                // ── Elite deals damage → Vampiric heal + Chilling proc ──
                Entity attackerElite = Entity.Null;
                if (EliteService.IsElite(sourceOwner))
                {
                    attackerElite = sourceOwner;
                }
                else if (damageTakenEvent.Source.Has<EntityOwner>())
                {
                    Entity directOwner = damageTakenEvent.Source.Read<EntityOwner>().Owner;
                    if (Core.EntityManager.Exists(directOwner) && EliteService.IsElite(directOwner))
                        attackerElite = directOwner;
                }

                if (attackerElite != Entity.Null)
                {
                    var eliteData = EliteService.GetEliteData(attackerElite);
                    if (eliteData != null)
                    {
                        foreach (var affix in eliteData.Affixes)
                        {
                            switch (affix)
                            {
                                case EliteAffix.Vampiric:
                                    TryVampiricHeal(attackerElite, eliteData);
                                    break;
                                case EliteAffix.Chilling:
                                    TryChillingProc(target);
                                    break;
                            }
                        }
                    }
                }

                // ── Elite takes damage → Thorns reflect + Summoner trigger ──
                if (EliteService.IsElite(target))
                {
                    var eliteData = EliteService.GetEliteData(target);
                    if (eliteData != null)
                    {
                        foreach (var affix in eliteData.Affixes)
                        {
                            switch (affix)
                            {
                                case EliteAffix.Thorns:
                                    TryThornsReflect(sourceOwner, target, eliteData);
                                    break;
                                case EliteAffix.Summoner:
                                    TrySummonerSpawn(target, eliteData);
                                    break;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Core.Log.LogWarning($"[EliteMobs:Combat] Error: {e.Message}");
        }
        finally
        {
            if (entities.IsCreated) entities.Dispose();
            if (damageTakenEvents.IsCreated) damageTakenEvents.Dispose();
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  AFFIX COMBAT HELPERS
    // ═══════════════════════════════════════════════════════════

    static void TryVampiricHeal(Entity elite, ActiveEliteData data)
    {
        if (!elite.Has<Health>()) return;

        elite.With((ref Health h) =>
        {
            float healAmount = h.MaxHealth._Value * 0.02f;
            h.Value = Math.Min(h.Value + healAmount, h.MaxHealth._Value);
        });
    }

    static void TryChillingProc(Entity target)
    {
        if (_rng.NextDouble() > 0.35) return;
        if (!Core.EntityManager.Exists(target)) return;

        BuffHelper.TryApplyBuff(target, ChillDebuff);
    }

    static void TryThornsReflect(Entity attacker, Entity elite, ActiveEliteData data)
    {
        if (!attacker.Has<PlayerCharacter>()) return;
        if (!attacker.Has<Health>()) return;
        if (!elite.Has<Health>()) return;

        float reflectDamage = elite.Read<Health>().MaxHealth._Value * 0.03f;

        attacker.With((ref Health h) =>
        {
            h.Value = Math.Max(1f, h.Value - reflectDamage);
        });
    }

    static void TrySummonerSpawn(Entity elite, ActiveEliteData data)
    {
        if (data.SummonerTriggered) return;
        data.SummonerTriggered = true;

        try
        {
            if (!elite.Has<PrefabGUID>()) return;
            if (!elite.Has<Unity.Transforms.Translation>()) return;

            PrefabGUID prefab = elite.Read<PrefabGUID>();
            var pos = elite.Read<Unity.Transforms.Translation>().Value;

            var usus = Core.TheWorld.GetExistingSystemManaged<UnitSpawnerUpdateSystem>();
            usus.SpawnUnit(Entity.Null, prefab, pos, 2, 1f, 2f, -1f);

            Core.Log.LogInfo($"[EliteMobs] Summoner spawned 2 adds ({prefab.LookupName()})");
        }
        catch (Exception e)
        {
            Core.Log.LogWarning($"[EliteMobs] Summoner spawn error: {e.Message}");
        }
    }
}
