using BepInEx.Configuration;

using HarmonyLib;

namespace PeakTweaks.Patches;

public class BananaPeelFix : ModPatch {
    public override bool ShouldLoad(ConfigFile config) {
        ConfigEntry<bool> enabled = config.Bind(
            section: "Client",
            key: "Banana Peel Join Fix",
            defaultValue: true,
            description: "Fix a bug that made it impossible to join a lobby if a banana peel existed anywhere.");
        return enabled.Value;
    }

    [HarmonyPatch(typeof(BananaPeel), nameof(BananaPeel.Update))]
    [HarmonyPrefix]
    public static bool BananaPeel_Update() {
        if (Character.localCharacter == null) {
            Plugin.Log.LogInfo("Preventing Banana Peel join bug ;)");
            return false;
        }
        return true;
    }
}
