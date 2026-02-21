using System;
using System.Collections.Generic;
using HarmonyLib;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace EliteMobs.Services;

/// <summary>
/// Spawns units with post-spawn callbacks using the LifeTime key trick.
/// </summary>
public class SpawnService
{
    static readonly Entity _empty = new();
    static readonly System.Random _rng = new();

    internal static Dictionary<long, (float actualDuration, Action<Entity> actions)> PostActions = new();

    public static void SpawnWithCallback(PrefabGUID unit, float3 position, float duration, Action<Entity> postActions)
    {
        var usus = Core.TheWorld.GetExistingSystemManaged<UnitSpawnerUpdateSystem>();

        SpawnReactPatch.Enabled = true;

        long key = NextKey();
        usus.SpawnUnit(_empty, unit, position, 1, 1f, 3f, key);
        PostActions.Add(key, (duration, postActions));
    }

    static long NextKey()
    {
        long key;
        int breaker = 10;
        do
        {
            key = _rng.NextInt64(10000) * 3;
            breaker--;
            if (breaker < 0)
                throw new Exception("Failed to generate unique spawn key");
        } while (PostActions.ContainsKey(key));
        return key;
    }

    [HarmonyPatch(typeof(UnitSpawnerReactSystem), nameof(UnitSpawnerReactSystem.OnUpdate))]
    public static class SpawnReactPatch
    {
        public static bool Enabled { get; set; } = false;

        public static void Prefix(UnitSpawnerReactSystem __instance)
        {
            if (!Enabled) return;

            var entities = __instance._Query.ToEntityArray(Allocator.Temp);

            try
            {
                foreach (var entity in entities)
                {
                    if (!Core.EntityManager.HasComponent<LifeTime>(entity)) continue;

                    var lifetime = Core.EntityManager.GetComponentData<LifeTime>(entity);
                    long durationKey = (long)Mathf.Round(lifetime.Duration);

                    if (PostActions.TryGetValue(durationKey, out var data))
                    {
                        PostActions.Remove(durationKey);

                        var endAction = data.actualDuration < 0 ? LifeTimeEndAction.None : LifeTimeEndAction.Destroy;
                        Core.EntityManager.SetComponentData(entity, new LifeTime
                        {
                            Duration = data.actualDuration,
                            EndAction = endAction
                        });

                        data.actions(entity);
                    }
                }

                if (PostActions.Count == 0)
                    Enabled = false;
            }
            finally
            {
                entities.Dispose();
            }
        }
    }
}
