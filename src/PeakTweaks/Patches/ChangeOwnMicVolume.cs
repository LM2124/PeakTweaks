using BepInEx.Configuration;

using HarmonyLib;

using Photon.Voice.Unity.UtilityScripts;

using UnityEngine;

namespace PeakTweaks.Patches;

public class ChangeOwnMicVolume : ModPatch {
    public static float NewVolume;

    public override bool ShouldLoad(ConfigFile config) {
        NewVolume = config.Bind(
            section: "Client",
            key: "Microphone Volume",
            defaultValue: 100.0f,
            description: "Change your microphone's output volume to your friends."
            //"\nNote: There is perceived loudness compensation; so a value of 200% should truly sound 2x louder"
            ).Value / 100; // 100% -> 1.0, 200% -> 2.0, etc
        return !Mathf.Approximately(NewVolume, 1f);
    }

    // Volume perception is logarithmic and stuff; multiplying a sample by
    // 10x only increases the human-perceived loudness by ~2x
    private static float VolumeMultiplierFromPerceivedVolumeIncrease(float perceivedMultiplier) {
        return Mathf.Pow(10, Mathf.Log(perceivedMultiplier, 2));
    }

    [HarmonyPatch(typeof(CharacterVoiceHandler), nameof(CharacterVoiceHandler.Start))]
    [HarmonyPrefix] // Has to happen before Recorder is initialized; PhotonVoice will look for a MicAmplifier component or something
    public static void CharacterVoiceHandler_Start(CharacterVoiceHandler __instance) {
        // m_character is not initialized yet so we have to grab it ourselves
        Character? character = __instance.GetComponentInParent<Character>();
        if (character == null || character.IsLocal == false) {
            return;
        }

        Plugin.Log.LogInfo($"Boosting your microphone's volume by {NewVolume:F2}x...");
        float multiplier = VolumeMultiplierFromPerceivedVolumeIncrease(NewVolume);

        Plugin.Log.LogDebug($"Adding MicAmplifier component for {character?.name}; AmplificationFactor = {multiplier}");
        MicAmplifier micAmplifier = __instance.gameObject.AddComponent<MicAmplifier>();
        micAmplifier.AmplificationFactor = multiplier;
    }


    // For hot-reloading
    public override void Init() {
        MicAmplifier? micAmplifier = Character.localCharacter
            ?.GetComponentInChildren<CharacterVoiceHandler>()
            ?.gameObject?.GetComponent<MicAmplifier>();

        if (micAmplifier != null) {
            float multiplier = VolumeMultiplierFromPerceivedVolumeIncrease(NewVolume);
            Plugin.Log.LogDebug($"Found MicAmplifier component; setting new AmplificationFactor: {multiplier}");
            micAmplifier.AmplificationFactor = multiplier;
        }
    }
    public override void DeInit() {
        MicAmplifier? micAmplifier = Character.localCharacter
            ?.GetComponentInChildren<CharacterVoiceHandler>()
            ?.gameObject?.GetComponent<MicAmplifier>();

        if (micAmplifier != null) {
            // Can't really delete it because
            // the Recorder has already been created
            micAmplifier.AmplificationFactor = 1f;
        }
    }
}
