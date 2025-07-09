using BepInEx.Configuration;

using HarmonyLib;

using UnityEngine;
using UnityEngine.EventSystems;

namespace PeakTweaks.Patches;

public class TweakVoiceVolume : ModPatch {
    public static float NewMaxValue;

    public override bool ShouldLoad(ConfigFile config) {
        NewMaxValue = config.Bind(
            section: "Client",
            key: "Maximum Voice Volume",
            defaultValue: 400f,
            description: "Increase the maximum volume you can set other players' voice volume to." +
            "\nSet to the game's default value of 200 to disable."
            ).Value / 200; // Game maps out 0%-200% to 0-1 on the volume

        return NewMaxValue != 1; // 200%
    }

    [HarmonyPatch(typeof(AudioLevelSlider), nameof(AudioLevelSlider.Awake))]
    [HarmonyPostfix]
    public static void AudioLevelSlider_Awake(AudioLevelSlider __instance) {
        __instance.slider.maxValue = NewMaxValue;
    }

    [HarmonyPatch(typeof(AudioLevelSlider), nameof(AudioLevelSlider.OnSliderChanged))]
    [HarmonyPrefix]
    public static bool AudioLevelSlider_OnSliderChanged(AudioLevelSlider __instance, float newValue) {
        // Copy-pasting some lines from decompiled original method
        // (I don't want to bother with a Transpile)
        if (__instance.player != null) {
            AudioLevels.SetPlayerLevel(__instance.player.ActorNumber, newValue);


            // Custom code - Select a sprite based on the new maximum volume value
            // (It was effectively hardcoded before)
            float filledPercentage = newValue / NewMaxValue;

            __instance.icon.sprite = (newValue == 0f)
                ? __instance.mutedAudioSprite
                : __instance.audioSprites[Mathf.Clamp(
                    Mathf.FloorToInt(filledPercentage * __instance.audioSprites.Length),
                    0, __instance.audioSprites.Length - 1)];

            // Gradient receives values from 0-1
            __instance.bar.color = __instance.barGradient.Evaluate(filledPercentage);


            // Back to copy-paste
            EventSystem.current.SetSelectedGameObject(null);
            __instance.percent.text = Mathf.RoundToInt(newValue * 200f).ToString() + "%";
        }
        return false;
    }

    // For hot-reloading
    public override void Init() {
        Object.FindObjectsByType<AudioLevelSlider>(FindObjectsSortMode.None)
            .Do(instance => instance.slider.maxValue = NewMaxValue);
    }
    public override void DeInit() {
        Object.FindObjectsByType<AudioLevelSlider>(FindObjectsSortMode.None)
            .Do(instance => {
                instance.slider.maxValue = 1;
                instance.slider.value = Mathf.Min(1, instance.slider.value);
            });
    }
}
