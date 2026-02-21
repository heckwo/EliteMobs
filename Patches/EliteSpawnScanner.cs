using System;
using System.Collections.Generic;
using EliteMobs.Config;
using EliteMobs.Services;
using ProjectM;
using Unity.Collections;
using Unity.Entities;

namespace EliteMobs.Patches;

/// <summary>
/// Periodically scans for newly spawned mobs and rolls elite modifiers.
/// Runs every 5 seconds via the tick loop.
/// </summary>
public static class EliteSpawnScanner
{
    static readonly HashSet<long> _processedEntities = new();

    static DateTime _lastScan = DateTime.MinValue;
    static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(5);

    static int _scanCount;
    const int PurgeInterval = 60; // Every 5 minutes

    static long EntityKey(Entity e) => ((long)e.Index << 32) | (uint)e.Version;

    public static void Tick()
    {
        if (!EliteConfig.Enabled) return;

        DateTime now = DateTime.UtcNow;
        if (now - _lastScan < ScanInterval) return;
        _lastScan = now;

        try
        {
            int newCount = 0;
            int promoted = 0;

            var entities = Helper.GetEntitiesByComponentTypes<UnitLevel, Movement>();
            try
            {
                foreach (Entity entity in entities)
                {
                    if (!Core.EntityManager.Exists(entity)) continue;

                    long key = EntityKey(entity);
                    if (!_processedEntities.Add(key)) continue;

                    newCount++;

                    // Skip player-owned entities (familiars, summons)
                    if (entity.Has<EntityOwner>())
                    {
                        var owner = entity.Read<EntityOwner>().Owner;
                        if (Core.EntityManager.Exists(owner) && owner.Has<PlayerCharacter>())
                            continue;

                        if (Core.EntityManager.Exists(owner) && owner.Has<EntityOwner>())
                        {
                            var grandOwner = owner.Read<EntityOwner>().Owner;
                            if (Core.EntityManager.Exists(grandOwner) && grandOwner.Has<PlayerCharacter>())
                                continue;
                        }
                    }

                    if (EliteService.TryMakeElite(entity))
                        promoted++;
                }
            }
            finally
            {
                entities.Dispose();
            }

            if (newCount > 0)
                Core.Log.LogInfo($"[EliteMobs:Spawn] Scanned {newCount} new entities, promoted {promoted} to elite.");

            _scanCount++;
            if (_scanCount >= PurgeInterval)
            {
                _scanCount = 0;
                if (_processedEntities.Count > 5000)
                    _processedEntities.Clear();
                EliteService.CleanupStale();
            }
        }
        catch (Exception e)
        {
            Core.Log.LogWarning($"[EliteMobs:Spawn] Scan error: {e.Message}");
        }
    }
}
