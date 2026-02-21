using System;
using EliteMobs.Services;
using HarmonyLib;
using ProjectM;
using Unity.Entities;

namespace EliteMobs.Patches;

/// <summary>
/// Hooks ServerBootstrapSystem.OnUpdate for:
///   1. One-time initialization (when world becomes ready)
///   2. Per-tick elite service updates (spawn scanning, affix behaviors)
/// </summary>
[HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUpdate))]
internal static class TickPatch
{
    static bool _initialized = false;

    [HarmonyPostfix]
    static void Postfix()
    {
        if (!_initialized)
        {
            try
            {
                Core.InitializeAfterLoaded();
                _initialized = true;
            }
            catch
            {
                return; // World not ready yet
            }
        }

        if (!Core.IsReady) return;

        try
        {
            EliteService.Tick();
            EliteSpawnScanner.Tick();
        }
        catch (Exception e)
        {
            Core.Log.LogWarning($"[EliteMobs:Tick] Error: {e.Message}");
        }
    }
}
