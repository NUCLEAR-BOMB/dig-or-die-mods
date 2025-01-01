﻿using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;
using ModUtils;

public static class HpMaxPatch {
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(CUnitPlayer.CDesc), nameof(CUnitPlayer.CDesc.GetHpMax))]
    private static IEnumerable<CodeInstruction> CUnitPlayer_CDesc_GetHpMax(IEnumerable<CodeInstruction> instructions) {
        return [
            new(OpCodes.Ldc_R4, UltraHardcorePlugin.configPlayerHpMax.Value), 
            new(OpCodes.Ret)
        ];
    }
}
public static class PermanentMistPatch {
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(CBackground), nameof(CBackground.DrawBackgrounds))]
    [HarmonyPatch(typeof(SDrawWorld), nameof(SDrawWorld.OnUpdate))]
    [HarmonyPatch(typeof(SWorld), nameof(SWorld.ProcessLighting_DynamicUnits))]
    private static IEnumerable<CodeInstruction> AlwaysMistTranspiler(IEnumerable<CodeInstruction> instructions) {
        var codeMatcher = new CodeMatcher(instructions);

        codeMatcher.Start()
            .MatchForward(useEnd: true,
                new(OpCodes.Call, AccessTools.Method(typeof(SEnvironment), nameof(SEnvironment.GetEnvironmentCurrent))),
                new(OpCodes.Ldfld, AccessTools.Field(typeof(CEnvironment), nameof(CEnvironment.m_mist))),
                new(OpCodes.Brfalse))
            .ThrowIfInvalid("(1)")
            .SetAndAdvance(OpCodes.Pop, null)
            .MatchForward(useEnd: false,
                new(OpCodes.Call, AccessTools.Method(typeof(SEnvironment), nameof(SEnvironment.GetEnvironmentCurrent))),
                new(OpCodes.Ldc_R4, 5f),
                new(OpCodes.Callvirt, AccessTools.Method(typeof(CEnvironment), nameof(CEnvironment.GetBeginEndSmoothingValue))))
            .ThrowIfInvalid("(2)")
            .SetAndAdvance(OpCodes.Ldc_R4, 1f)
            .RemoveInstructions(2);

        return codeMatcher.Instructions();
    }
}
public static class PermanentDarknessPatch {
    // [HarmonyTranspiler]
    // [HarmonyPatch(typeof(CBackground), nameof(CBackground.DrawBackgroundParralax))]
    // [HarmonyPatch(typeof(CBackground), nameof(CBackground.DrawBackgrounds))]
    // [HarmonyPatch(typeof(CParticleGroup), nameof(CParticleGroup.EmitNb))]
    // [HarmonyPatch(typeof(SWorldDll), "ProcessLightingSquare")]
    // private static IEnumerable<CodeInstruction> PermanentDarknessTranspiler(IEnumerable<CodeInstruction> instructions) {
    //     var codeMatcher = new CodeMatcher(instructions);
    // 
    //     codeMatcher.Start()
    //         .MatchForward(useEnd: false,
    //             new CodeMatch(OpCodes.Ldsfld, AccessTools.Field(typeof(G), nameof(G.m_sunLight))))
    //         .ThrowIfInvalid("(1)")
    //         .Set(OpCodes.Ldc_R4, 0f);
    // 
    //     return codeMatcher.Instructions();
    // }
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SWorld), nameof(SWorld.UpdateLightSunValue))]
    private static void SWorld_UpdateLightSunValue() {
        G.m_sunLight = 0f;
    }
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(CModeSolo), nameof(CModeSolo.CreateInitialPlayerItems))]
    private static IEnumerable<CodeInstruction> CModeSolo_CreateInitialPlayerItems(IEnumerable<CodeInstruction> instructions) {
        var codeMatcher = new CodeMatcher(instructions);

        codeMatcher.Start()
            .MatchForward(useEnd: false,
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldfld, AccessTools.Field(typeof(CPlayer), nameof(CPlayer.m_inventory))),
                new(OpCodes.Ldsfld, AccessTools.Field(typeof(GItems), nameof(GItems.gunRifle))))
            .ThrowIfInvalid("(1)")
            .Insert(
                new CodeInstruction(OpCodes.Ldarg_1),
                Transpilers.EmitDelegate((CPlayer player) => {
                    player.m_inventory.AddToInventory(GItems.lightSun);
                }));
        codeMatcher.Start()
            .MatchForward(useEnd: false,
                new(OpCodes.Ldsfld, AccessTools.Field(typeof(GItems), nameof(GItems.gunRifle))),
                new(OpCodes.Ldc_R4, 1f))
            .ThrowIfInvalid("(2)")
            .Insert(
                Transpilers.EmitDelegate(() => {
                    SPickups.CreatePickup(GItems.lightSun, nb: 1f, pos: G.m_player.PosCenter + 6.5f * Vector2.right, withSpeed: false);
                }));

        return codeMatcher.Instructions();
    }
}
public static class NoRainPatch {
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(SWorld), nameof(SWorld.OnUpdateSimu))]
    private static IEnumerable<CodeInstruction> SWorld_OnUpdateSimu(IEnumerable<CodeInstruction> instructions) {
        var codeMatcher = new CodeMatcher(instructions);
        codeMatcher.Start()
            .MatchForward(useEnd: false,
                new CodeMatch(OpCodes.Ldc_I4_1))
            .ThrowIfInvalid("Match failed")
            .Set(OpCodes.Ldc_I4_0, null);
        return codeMatcher.Instructions();
    }
}
public static class InverseNightPatch {
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SGame), nameof(SGame.IsNight))]
    private static void SGame_IsNight(ref bool __result) {
        __result = !__result;
    }
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SGame), nameof(SGame.GetNightDurationLeft))]
    private static bool SGame_GetNightDurationLeft(ref float __result) {
        __result = 10f;
        return false;
    }
}
public static class PermanentAcidWaterPatch {
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(CUnit), nameof(CUnit.Update))]
    private static IEnumerable<CodeInstruction> CUnit_Update(IEnumerable<CodeInstruction> instructions) {
        var codeMatcher = new CodeMatcher(instructions);
        codeMatcher.Start()
            .MatchForward(useEnd: false,
                new(OpCodes.Call, AccessTools.Method(typeof(SEnvironment), nameof(SEnvironment.GetEnvironmentCurrent))),
                new(OpCodes.Ldfld, AccessTools.Field(typeof(CEnvironment), nameof(CEnvironment.m_acidWater))),
                new(OpCodes.Brfalse))
            .ThrowIfInvalid("Match failed")
            .CollapseInstructions(3);
        return codeMatcher.Instructions();
    }
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(CEnvironment), nameof(CEnvironment.GetWaterAcidRatio))]
    private static IEnumerable<CodeInstruction> CEnvironment_GetWaterAcidRatio(IEnumerable<CodeInstruction> instructions) {
        return [
            new(OpCodes.Ldc_R4, 1f),
            new(OpCodes.Ret)
        ];
    }
}
public static class NoRegenerationPatch {
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(CUnitPlayer.CDesc), MethodType.Constructor, typeof(float), typeof(Vector2), typeof(int), typeof(CTile), typeof(CTile), typeof(CTile), typeof(CTile))]
    private static IEnumerable<CodeInstruction> CUnitPlayer_CDesc_Constructor(IEnumerable<CodeInstruction> instructions) {
        var codeMatcher = new CodeMatcher(instructions);

        codeMatcher.Start()
            .MatchForward(useEnd: false,
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldc_R4, 0.005f),
                new(OpCodes.Stfld, AccessTools.Field(typeof(CUnit.CDesc), nameof(CUnit.CDesc.m_regenSpeed))))
            .ThrowIfInvalid("(1)")
            .Advance(1)
            .Set(OpCodes.Ldc_R4, 0f);

        return codeMatcher.Instructions();
    }
}

[BepInPlugin("ultra-hardcore", "Ultra Hardcore", "0.0.0")]
public class UltraHardcorePlugin : BaseUnityPlugin
{
    public static ConfigEntry<float> configPlayerHpMax;

    private void Start() {
        configPlayerHpMax = Config.Bind<float>(
            section: "UltraHardcore", key: "HpMax", defaultValue: 1f,
            configDescription: new ConfigDescription(
                "Maximum health for the player",
                new AcceptableValueRange<float>(0, float.MaxValue)
            )
        );
        var configPermanentMist = Config.Bind<bool>(
            section: "UltraHardcore", key: "PermanentMist", defaultValue: false,
            description: "Makes the mist permanent, regardless of the current events"
        );
        var configPermanentDarkness = Config.Bind<bool>(
            section: "UltraHardcore", key: "PermanentDarkness", defaultValue: false,
            description: "Makes the night permanent, but without monsters. Adds Sun Light to the initial item list"
        );
        var configNoRain = Config.Bind<bool>(
            section: "UltraHardcore", key: "NoRain", defaultValue: false,
            description: "Removes all rains from the game"
        );
        var configInverseNight = Config.Bind<bool>(
            section: "UltraHardcore", key: "InverseNight", defaultValue: false,
            description: "Monsters attack during the day, but stop at night"
        );
        var configPermanentAcidWater = Config.Bind<bool>(
            section: "UltraHardcore", key: "PermanentAcidWater", defaultValue: false,
            description: "Makes event 'ACIDIC WATERS' always active"
        );
        var configNoRegeneration = Config.Bind<bool>(
            section: "UltraHardcore", key: "NoRegeneration", defaultValue: false,
            description: "Removes regeneration from the player, so you can only gain health with potions"
        );

        var harmony = new Harmony("ultra-hardcore");

        harmony.PatchAll(typeof(HpMaxPatch));
        if (configPermanentMist.Value) {
            harmony.PatchAll(typeof(PermanentMistPatch));
        }
        if (configPermanentDarkness.Value) {
            harmony.PatchAll(typeof(PermanentDarknessPatch));
        }
        if (configNoRain.Value) {
            harmony.PatchAll(typeof(NoRainPatch));
        }
        if (configInverseNight.Value) {
            harmony.PatchAll(typeof(InverseNightPatch));
        }
        if (configPermanentAcidWater.Value) {
            harmony.PatchAll(typeof(PermanentAcidWaterPatch));
        }
        if (configNoRegeneration.Value) {
            harmony.PatchAll(typeof(NoRegenerationPatch));
        }
    }
}
