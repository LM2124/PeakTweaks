using System.Collections.Generic;

using BepInEx.Configuration;

using HarmonyLib;

using UnityEngine;

namespace PeakTweaks.Patches;

public class CampfireHungerReduction : ModPatch {
    public static float CampfireHungerMultiplier;
    public static float CampfireHungerReductionRange;

    public override bool ShouldLoad(ConfigFile config) {
        bool enabled = config.Bind(
            section: "Everyone",
            key: "Campfire Hunger Reduction",
            defaultValue: false,
            description: "Reduce hunger gain while close to a campfire." +
            "\nUseful to avoid starvation while waiting for that *one guy* to catch up. It's always him." +
            "\n*Might* work when not everyone has this, but might also cause desyncs. I dunno."
            ).Value;

        CampfireHungerMultiplier = config.Bind(
            section: "Everyone",
            key: "Campfire Hunger Reduction Multiplier",
            defaultValue: 0.5f,
            description: "Multiply hunger gain near campfires by this amount." +
            "\nCan be 0 to pause hunger entirely, or a negative value like -1 to regenerate hunger near the campfire."
            ).Value;
        CampfireHungerReductionRange = config.Bind(
            section: "Everyone",
            key: "Campfire Hunger Reduction Range",
            defaultValue: 15f,
            description: "Range of the hunger reduction effect, in in-game meters."
            ).Value / CharacterStats.unitsToMeters;

        return enabled && CampfireHungerMultiplier != 1 && CampfireHungerReductionRange > 0;
    }

    [HarmonyPatch(typeof(Campfire), nameof(Campfire.Awake))]
    [HarmonyPrefix]
    private static void Campfire_Awake(Campfire __instance) {
        CampfireProximityTracker.CreateAndAttachToCampfire(__instance, CampfireHungerReductionRange);
    }

    [HarmonyPatch(typeof(CharacterAfflictions), nameof(CharacterAfflictions.AddStatus))]
    [HarmonyPrefix]
    public static bool CharacterAfflictions_AddStatus(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, ref float amount, bool fromRPC) {
        if (statusType == CharacterAfflictions.STATUSTYPE.Hunger
            && amount > 0
            && !fromRPC
            && CampfireProximityTracker.charactersNearCampfires.Contains(__instance.character)
            // Currently this is the formula they use for hunger rate
            && Mathf.Approximately(amount, Time.deltaTime * __instance.hungerPerSecond * Ascents.hungerRateMultiplier)
        ) {
            float adjustedAmount = amount * CampfireHungerMultiplier;

            switch (adjustedAmount) {
                case < 0:
                    // In case hungerMultiplier is negative,
                    // redirect to SubtractStatus to restore hunger instead
                    __instance.SubtractStatus(statusType, -adjustedAmount, fromRPC);
                    return false;
                case 0:
                    // When adjustedAmount == 0, just skip the method entirely
                    return false;
                default:
                    // Adjust the amount, and let the call keep going
                    amount = adjustedAmount;
                    return true;
            }
        }
        return true;
    }

    // For hot-reloading
    public override void Init() {
        // Won't find anything at first Init, but might after a hot-reload.
        Object.FindObjectsByType<Campfire>(FindObjectsSortMode.None)
            .Do(c => CampfireProximityTracker.CreateAndAttachToCampfire(c, CampfireHungerReductionRange));
    }
    public override void DeInit() {
        Object.FindObjectsByType<CampfireProximityTracker>(FindObjectsSortMode.None)
            .Do(Object.Destroy);
    }
}

public class CampfireProximityTracker : MonoBehaviour {
    public static HashSet<Character> charactersNearCampfires = [];

    public static void CreateAndAttachToCampfire(Campfire campfire, float range) {
        Plugin.Log.LogDebug($"Attaching Proximity Tracker to {campfire.name}");

        CampfireProximityTracker tracker = campfire.gameObject.AddComponent<CampfireProximityTracker>();
        tracker.Initialize(campfire, range);
    }

    public void Initialize(Campfire campfire, float range) {
        transform.SetParent(campfire.transform);
        transform.localPosition = Vector3.zero;

        SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = range;
    }

    private void OnTriggerEnter(Collider other) {
        Character? character = other.GetComponentInParent<Character>();
        if (character != null && charactersNearCampfires.Add(character)) {
            Plugin.Log.LogDebug($"Character entered campfire radius: {character.name}");
        }
    }

    private void OnTriggerExit(Collider other) {
        Character? character = other.GetComponentInParent<Character>();
        if (character != null && charactersNearCampfires.Remove(character)) {
            Plugin.Log.LogDebug($"Character exited campfire radius: {character.name}");
        }
    }
}
