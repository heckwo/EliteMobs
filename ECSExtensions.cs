using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using System;
using System.Runtime.InteropServices;
using Unity.Entities;

namespace EliteMobs;

public static class ECSExtensions
{
    public unsafe static void Write<T>(this Entity entity, T componentData) where T : struct
    {
        var ct = new ComponentType(Il2CppType.Of<T>());
        byte[] byteArray = StructureToByteArray(componentData);
        int size = Marshal.SizeOf<T>();
        fixed (byte* p = byteArray)
        {
            Core.TheWorld.EntityManager.SetComponentDataRaw(entity, ct.TypeIndex, p, size);
        }
    }

    public static byte[] StructureToByteArray<T>(T structure) where T : struct
    {
        int size = Marshal.SizeOf(structure);
        byte[] byteArray = new byte[size];
        IntPtr ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(structure, ptr, true);
        Marshal.Copy(ptr, byteArray, 0, size);
        Marshal.FreeHGlobal(ptr);
        return byteArray;
    }

    public unsafe static T Read<T>(this Entity entity) where T : struct
    {
        var ct = new ComponentType(Il2CppType.Of<T>());
        if (ct.IsZeroSized)
            return new T();
        void* rawPointer = Core.TheWorld.EntityManager.GetComponentDataRawRO(entity, ct.TypeIndex);
        T componentData = Marshal.PtrToStructure<T>(new IntPtr(rawPointer));
        return componentData;
    }

    public static bool Has<T>(this Entity entity)
    {
        var ct = new ComponentType(Il2CppType.Of<T>());
        return Core.TheWorld.EntityManager.HasComponent(entity, ct);
    }

    public static void Add<T>(this Entity entity)
    {
        var ct = new ComponentType(Il2CppType.Of<T>());
        Core.TheWorld.EntityManager.AddComponent(entity, ct);
    }

    public static void Remove<T>(this Entity entity)
    {
        var ct = new ComponentType(Il2CppType.Of<T>());
        Core.TheWorld.EntityManager.RemoveComponent(entity, ct);
    }

    public static string LookupName(this PrefabGUID prefabGuid)
    {
        var prefabCollectionSystem = Core.TheWorld.GetExistingSystemManaged<PrefabCollectionSystem>();
        return (prefabCollectionSystem._PrefabLookupMap.GuidToEntityMap.ContainsKey(prefabGuid)
            ? prefabCollectionSystem._PrefabLookupMap.GetName(prefabGuid) + " PrefabGuid(" + prefabGuid.GuidHash + ")"
            : "GUID Not Found");
    }

    public static ulong GetSteamId(this Entity charEntity)
    {
        try
        {
            if (!charEntity.Has<PlayerCharacter>()) return 0;
            var pc = charEntity.Read<PlayerCharacter>();
            var userEntity = pc.UserEntity;
            if (!Core.EntityManager.Exists(userEntity) || !userEntity.Has<User>()) return 0;
            return userEntity.Read<User>().PlatformId;
        }
        catch { return 0; }
    }

    public static Entity GetUserEntity(this Entity charEntity)
    {
        if (!charEntity.Has<PlayerCharacter>()) return Entity.Null;
        var pc = charEntity.Read<PlayerCharacter>();
        return pc.UserEntity;
    }

    public static bool Exists(this Entity entity)
    {
        return Core.EntityManager.Exists(entity);
    }

    public delegate void WithRefHandler<T>(ref T component) where T : struct;
    public static void With<T>(this Entity entity, WithRefHandler<T> action) where T : struct
    {
        var component = entity.Read<T>();
        action(ref component);
        entity.Write(component);
    }
}
