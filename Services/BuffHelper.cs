using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Entities;

namespace EliteMobs.Services;

public static class BuffHelper
{
    static EntityManager EntityManager => Core.EntityManager;

    public static bool HasBuff(Entity entity, PrefabGUID buffPrefab)
    {
        return BuffUtility.HasBuff(EntityManager, entity, buffPrefab);
    }

    public static bool TryGetBuff(Entity entity, PrefabGUID buffPrefab, out Entity buffEntity)
    {
        return BuffUtility.TryGetBuff(EntityManager, entity, buffPrefab, out buffEntity);
    }

    public static bool TryApplyBuff(Entity entity, PrefabGUID buffPrefab)
    {
        if (HasBuff(entity, buffPrefab)) return false;

        var des = Core.DebugEventsSystem;
        var buffEvent = new ApplyBuffDebugEvent()
        {
            BuffPrefabGUID = buffPrefab
        };

        var fromCharacter = new FromCharacter()
        {
            Character = entity,
            User = entity.Has<PlayerCharacter>() ? entity.GetUserEntity() : entity
        };

        des.ApplyBuff(fromCharacter, buffEvent);
        return true;
    }

    public static bool TryApplyAndGetBuff(Entity entity, PrefabGUID buffPrefab, out Entity buffEntity)
    {
        buffEntity = Entity.Null;

        TryApplyBuff(entity, buffPrefab);

        return TryGetBuff(entity, buffPrefab, out buffEntity);
    }

    public static void TryRemoveBuff(Entity entity, PrefabGUID buffPrefab)
    {
        if (TryGetBuff(entity, buffPrefab, out Entity buffEntity))
        {
            DestroyUtility.Destroy(EntityManager, buffEntity);
        }
    }
}
