using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using EliteMobs.Config;
using EliteMobs.Services;
using HarmonyLib;
using VampireCommandFramework;

namespace EliteMobs;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("gg.deca.VampireCommandFramework")]
public class Plugin : BasePlugin
{
    public static Harmony Harmony => _harmony;
    static Harmony _harmony;
    public static ManualLogSource LogInstance { get; private set; }

    static bool _cleanupDone = false;

    public override void Load()
    {
        LogInstance = Log;
        Log.LogInfo($"[EliteMobs] v{MyPluginInfo.PLUGIN_VERSION} loading...");

        EliteConfig.Initialize(Config);

        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());

        CommandRegistry.RegisterAll(System.Reflection.Assembly.GetExecutingAssembly());

        // Shutdown hook — revert all elites before the world saves
        try
        {
            System.AppDomain.CurrentDomain.ProcessExit += (sender, args) => PerformCleanup("ProcessExit");
            Log.LogInfo("[EliteMobs] Shutdown hook registered.");
        }
        catch (System.Exception e)
        {
            Log.LogWarning($"[EliteMobs] Failed to register shutdown hook: {e.Message}");
        }

        Log.LogInfo($"[EliteMobs] Loaded. Elite mob system ready.");
    }

    static void PerformCleanup(string source)
    {
        if (_cleanupDone) return;
        _cleanupDone = true;

        try
        {
            LogInstance?.LogInfo($"[EliteMobs] Shutdown cleanup triggered by {source}");

            int purged = EliteService.PurgeAll();
            LogInstance?.LogInfo($"[EliteMobs] Elites purged: {purged}");

            LogInstance?.LogInfo("[EliteMobs] Shutdown cleanup complete.");
        }
        catch (System.Exception e)
        {
            LogInstance?.LogError($"[EliteMobs] Shutdown cleanup failed: {e.Message}");
        }
    }

    public override bool Unload()
    {
        PerformCleanup("Unload");
        CommandRegistry.UnregisterAssembly();
        _harmony?.UnpatchSelf();
        return true;
    }
}
