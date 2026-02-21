using System;
using EliteMobs.Services;
using HarmonyLib;
using ProjectM;
using Unity.Collections;
using Unity.Entities;

namespace EliteMobs.Patches;

/// <summary>
/// Hooks DeathEventListenerSystem to clean up elite tracking when mobs die.
/// Logs the death for debugging save corruption.
/// </summary>
[HarmonyPatch(typeof(DeathEventListenerSystem), nameof(DeathEventListenerSystem.OnUpdate))]
public static class EliteDeathPatch
{
    public static void Postfix(DeathEventListenerSystem __instance)
    {
        if (!Core.IsReady) return;

        try
        {
            var entities = __instance._DeathEventQuery.ToEntityArray(Allocator.Temp);

            try
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    if (!entity.Has<DeathEvent>()) continue;

                    var deathEvent = entity.Read<DeathEvent>();
                    Entity died = deathEvent.Died;

                    if (!Core.EntityManager.Exists(died)) continue;

                    // Check if dead mob was an elite — remove from tracking
                    var eliteData = EliteService.OnEliteDeath(died);
                    if (eliteData != null)
                    {
                        Core.Log.LogInfo($"[EliteMobs] Elite {eliteData.Tier} [{eliteData.AffixSummary}] died. " +
                            $"Remaining: {EliteService.ActiveCount}");
                    }
                }
            }
            finally
            {
                entities.Dispose();
            }
        }
        catch (Exception e)
        {
            Core.Log.LogWarning($"[EliteMobs:Death] Error: {e.Message}");
        }
    }
}
