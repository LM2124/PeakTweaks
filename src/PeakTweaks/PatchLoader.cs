using System;
using System.Collections.Generic;
using System.Linq;

using BepInEx.Configuration;

using HarmonyLib;

namespace PeakTweaks;

/*
 * PatchLoader will search for implentations of ModPatch via reflection.
 * It'll run `shouldLoad() -> bool` to decide if 
 * they should be loaded into Harmony or not.
 */
public abstract class ModPatch {
    public virtual bool ShouldLoad(ConfigFile config) { return true; }
    public virtual void Init() { }
    public virtual void DeInit() { }
}

public static class PatchLoader {
    public static List<ModPatch> ActivePatches = [];

    public static int ApplyPatches(Harmony harmony, ConfigFile config) {
        // TODO: Maybe switch to using Annotations; this is kinda icky
        // https://stackoverflow.com/a/17680332
        var modPatchTypes = AccessTools.AllTypes()
            .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(ModPatch)))
            .ToList();

        Plugin.Log.LogDebug($"Found {modPatchTypes.Count} Patches");

        foreach (var patchClass in modPatchTypes) {
            if (Activator.CreateInstance(patchClass) is not ModPatch patch) {
                continue;
            }

            if (!patch.ShouldLoad(config)) {
                continue;
            }

            Plugin.Log.LogDebug($"Applying patch {patchClass.Name}");
            try {
                // Run Init() before patching, so if it errros out we won't
                // have a patched but partially initialized class in a weird state
                patch.Init();
                harmony.PatchAll(patchClass);
            } catch (Exception e) {
                Plugin.Log.LogError($"""
                    Error applying patch {patchClass.Name}: {e.GetType().FullName}
                    {e.Message}
                    {e.StackTrace}
                    """);
                //AccessTools.RethrowException(e);
                continue;
            }
            ActivePatches.Add(patch);
            Plugin.Log.LogInfo($"Initialized patch {patchClass.Name}");
        }
        return ActivePatches.Count;
    }
    public static void ClearPatches(Harmony harmony) {
        foreach (ModPatch patchClass in ActivePatches) {
            patchClass.DeInit();
        }
        Harmony.UnpatchID(harmony.Id);
    }
}
