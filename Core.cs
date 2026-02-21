using BepInEx.Logging;
using ProjectM;
using ProjectM.Network;
using ProjectM.Scripting;
using Unity.Entities;

namespace EliteMobs;

/// <summary>
/// Minimal core — exposes server systems needed by the elite mod.
/// Initializes on first ServerBootstrapSystem tick when world is ready.
/// </summary>
public static class Core
{
    public static World TheWorld
    {
        get
        {
            if (_theWorld != null && _theWorld.IsCreated) return _theWorld;
            _theWorld = GetWorld("Server") ?? throw new System.Exception("Server world not found.");
            return _theWorld;
        }
    }
    static World _theWorld;

    public static bool IsReady => TheWorld != null && _hasInitialized;

    public static EntityManager EntityManager => TheWorld.EntityManager;
    public static ServerScriptMapper ServerScriptMapper { get; internal set; }
    public static double ServerTime => ServerGameManager.ServerTime;
    public static ServerGameManager ServerGameManager => ServerScriptMapper.GetServerGameManager();
    public static DebugEventsSystem DebugEventsSystem { get; internal set; }

    public static ManualLogSource Log => Plugin.LogInstance;

    internal static void InitializeAfterLoaded()
    {
        if (_hasInitialized) return;

        ServerScriptMapper = TheWorld.GetExistingSystemManaged<ServerScriptMapper>();
        DebugEventsSystem = TheWorld.GetExistingSystemManaged<DebugEventsSystem>();

        Services.EliteService.Initialize();

        _hasInitialized = true;
        Log.LogInfo($"[EliteMobs] Core initialized. Elite system active.");
    }

    static bool _hasInitialized = false;

    public static void SendMessage(Entity userEntity, string message)
    {
        if (!EntityManager.Exists(userEntity)) return;
        if (!userEntity.Has<User>()) return;

        var user = userEntity.Read<User>();
        var msg = new FixedString512Bytes(message);
        ServerChatUtils.SendSystemMessageToClient(EntityManager, user, ref msg);
    }

    static World GetWorld(string name)
    {
        foreach (var world in World.s_AllWorlds)
        {
            if (world.Name == name) return world;
        }
        return null;
    }
}
