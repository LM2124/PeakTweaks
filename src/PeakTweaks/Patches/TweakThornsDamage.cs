using System.Collections.Generic;
using System.Linq;

using BepInEx.Configuration;

using HarmonyLib;

using UnityEngine;
using UnityEngine.SceneManagement;
namespace PeakTweaks.Patches;

public class TweakThornsDamage : ModPatch {
    public static float ThornsDamageMultiplier;
    public static float ThornsKnockbackMultiplier;

    public override bool ShouldLoad(ConfigFile config) {
        ThornsDamageMultiplier = config.Bind(
            section: "Everyone",
            key: "Thorns Damage Multiplier",
            defaultValue: 1f,
            description: "Multiply Poison damage taken from contact with jungle thorns."
            ).Value;
        ThornsKnockbackMultiplier = config.Bind(
            section: "Everyone",
            key: "Thorns Knockback Multiplier",
            defaultValue: 1f,
            description: "Multiply Knockback dealt by contact with jungle thorns."
            ).Value;
        return !Mathf.Approximately(ThornsDamageMultiplier, 1) || !Mathf.Approximately(ThornsKnockbackMultiplier, 1);
    }

    public override void Init() {
        SceneManager.sceneLoaded += OnSceneLoaded;
        //Plugin.Log.LogError($"Init-{SceneManager.GetActiveScene().name is not "Title" and not "Airport"}");
        //Plugin.Log.LogError(SceneManager.GetActiveScene().name);
        // For Hot Reloading
        if (SceneManager.GetActiveScene().name is not "Pretitle" and not "Title" and not "Airport") {
            Execute();
        }
    }
    public override void DeInit() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GetThornCollisionModifiers().Do(CM => {
            CM.damage /= ThornsDamageMultiplier;
            CM.knockback /= ThornsKnockbackMultiplier;
        });
    }
    public void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode) {
        //Plugin.Log.LogError($"SceneLoaded-{SceneManager.GetActiveScene().name is not "Title" and not "Airport"}");
        if (scene.name is not "Title" and not "Airport") {
            Execute();
        }
    }

    // The Thorns don't have any MonoBeahavior class we could hook into,
    // they're just regular game objects with CollisionModifier components;
    // Luckily they at least bothered to give them a Tag, so I won't need
    // to loop through every single GameObject and check if it's a Thorn
    public static IEnumerable<CollisionModifier> GetThornCollisionModifiers() {
        return GameObject.FindGameObjectsWithTag("Thorn") // They better not change this >:(
            .Select(GO => GO.GetComponent<CollisionModifier>())
            .Where(CM => CM != null);
    }

    public static void Execute() {
        int count = 0;
        GetThornCollisionModifiers()
            .Do(CM => {
                CM.damage *= ThornsDamageMultiplier;
                CM.knockback *= ThornsKnockbackMultiplier;
                count++;
            });
        Plugin.Log.LogInfo($"Modified {count} Thorns objects");
    }
}
