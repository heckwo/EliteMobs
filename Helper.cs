using Unity.Collections;
using Unity.Entities;

namespace EliteMobs;

internal static class Helper
{
    public static NativeArray<Entity> GetEntitiesByComponentTypes<T1>(bool includeDisabled = false)
        where T1 : struct
    {
        EntityQueryOptions options = includeDisabled ? EntityQueryOptions.IncludeDisabled : EntityQueryOptions.Default;
        var query = Core.EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[] { new(Il2CppInterop.Runtime.Il2CppType.Of<T1>()) },
            Options = options
        });
        return query.ToEntityArray(Allocator.Temp);
    }

    public static NativeArray<Entity> GetEntitiesByComponentTypes<T1, T2>(bool includeDisabled = false)
        where T1 : struct where T2 : struct
    {
        EntityQueryOptions options = includeDisabled ? EntityQueryOptions.IncludeDisabled : EntityQueryOptions.Default;
        var query = Core.EntityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                new(Il2CppInterop.Runtime.Il2CppType.Of<T1>()),
                new(Il2CppInterop.Runtime.Il2CppType.Of<T2>())
            },
            Options = options
        });
        return query.ToEntityArray(Allocator.Temp);
    }
}
