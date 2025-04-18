﻿using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;
using ModUtils;
using ModUtils.Extensions;

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
                new(OpCodes.Call, typeof(SEnvironment).Method("GetEnvironmentCurrent")),
                new(OpCodes.Ldfld, typeof(CEnvironment).Field("m_mist")),
                new(OpCodes.Brfalse))
            .ThrowIfInvalid("(1)")
            .SetAndAdvance(OpCodes.Pop, null)
            .MatchForward(useEnd: false,
                new(OpCodes.Call, typeof(SEnvironment).Method("GetEnvironmentCurrent")),
                new(OpCodes.Ldc_R4, 5f),
                new(OpCodes.Callvirt, typeof(CEnvironment).Method("GetBeginEndSmoothingValue")))
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
                new(OpCodes.Ldfld, typeof(CPlayer).Field("m_inventory")),
                new(OpCodes.Ldsfld, typeof(GItems).StaticField("gunRifle")))
            .ThrowIfInvalid("(1)")
            .Insert(
                new CodeInstruction(OpCodes.Ldarg_1),
                Transpilers.EmitDelegate((CPlayer player) => {
                    player.m_inventory.AddToInventory(GItems.lightSun);
                }));
        codeMatcher.Start()
            .MatchForward(useEnd: false,
                new(OpCodes.Ldsfld, typeof(GItems).StaticField("gunRifle")),
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
                new(OpCodes.Call, typeof(SEnvironment).Method("GetEnvironmentCurrent")),
                new(OpCodes.Ldfld, typeof(CEnvironment).Field("m_acidWater")),
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
                new(OpCodes.Stfld, typeof(CUnit.CDesc).Field("m_regenSpeed")))
            .ThrowIfInvalid("(1)")
            .Advance(1)
            .Set(OpCodes.Ldc_R4, 0f);

        return codeMatcher.Instructions();
    }
}
public static class NoQuickSavesPatch {
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SGameStartEnd), nameof(SGameStartEnd.SaveGame))]
    private static bool SGameStartEnd_SaveGame(SDataSave.SaveType saveType) {
        if (saveType == SDataSave.SaveType.QuickSave) {
            return false;
        }
        return true;
    }
}
public static class ContinuousEventsPatch {
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(SEnvironment), nameof(SEnvironment.OnUpdateSimu))]
    private static IEnumerable<CodeInstruction> SEnvironment_OnUpdateSimu(IEnumerable<CodeInstruction> instructions) {
        static void RemoveImpossibleEvents(List<CEnvironment> events) {
            bool hasPermanentMist = UltraHardcorePlugin.configPermanentMist.Value;
            bool hasPermanentAcidWater = UltraHardcorePlugin.configPermanentAcidWater.Value;
            events.RemoveAll(evnt => {
                if (hasPermanentMist && evnt.m_id == "mist") { return true; }
                if (hasPermanentAcidWater && evnt.m_id == "acidWater") { return true; }

                if (evnt.m_isDayEnv && SGame.IsNight()) {
                    return true;
                }
                if (evnt.m_isNightEnv && !SGame.IsNight()) {
                    return true;
                }
                return false;
            });
        }

        var codeMatcher = new CodeMatcher(instructions);
        codeMatcher.Start()
            // list[global::UnityEngine.Random.Range(0, list.Count)];
            .MatchForward(useEnd: false,
                new(OpCodes.Ldloc_S),
                new(OpCodes.Ldloc_S),
                new(OpCodes.Ldc_I4_0),
                new(OpCodes.Ldloc_S),
                new(OpCodes.Callvirt, typeof(List<CEnvironment>).Method("get_Count")),
                new(OpCodes.Call),
                new(OpCodes.Callvirt),
                new(OpCodes.Stfld))
            .GetOperandAtOffset(1, out LocalBuilder listVar)
            .InjectAndAdvance(OpCodes.Ldloc, listVar)
            .Insert(Transpilers.EmitDelegate(RemoveImpossibleEvents));

        codeMatcher
            .MatchForward(useEnd: false,
                new(OpCodes.Ldloc_S),
                new(OpCodes.Ldfld),
                new(OpCodes.Ldfld, typeof(CEnvironment).Field("m_isNightEnv")),
                new(OpCodes.Brfalse))
            .RemoveInstructions(51)
            .Insert(
                new(OpCodes.Call, typeof(GVars).Method("get_SimuTime")),
                new(OpCodes.Stsfld, typeof(GVars).StaticField("m_eventStartTime")));

        return codeMatcher.Instructions();
    }
}
public static class InstantDrowning {
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(CUnit), nameof(CUnit.Update))]
    private static IEnumerable<CodeInstruction> CUnit_Update(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
        // Patch the amount of damage when the player is drowning (m_air <= 0) to maximum possible damage (int.MaxValue)

        var codeMatcher = new CodeMatcher(instructions, generator);
        codeMatcher.Start()
            .MatchForward(useEnd: false,
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, typeof(GVars).Method("get_SimuTime")),
                new(OpCodes.Stfld, typeof(CUnit).Field("m_lastAirHit")),

                new(OpCodes.Ldarg_0),
                new(OpCodes.Isinst, typeof(CUnitPlayer)),
                new(OpCodes.Brfalse),
                new(OpCodes.Ldc_I4_5), // overwrite this to "ldc.i4 int.MaxValue"
                new(OpCodes.Br))
            .ThrowIfInvalid("(1)")
            .Advance(6)
            .Set(OpCodes.Ldc_I4, int.MaxValue);
        return codeMatcher.Instructions();
    }
}
// public static class EnemyNoDetourPathing {
//     [HarmonyTranspiler]
//     [HarmonyPatch(typeof(SWorld_PF), nameof(SWorld_PF.AddMove))]
//     private static IEnumerable<CodeInstruction> SWorld_PF_AddMove(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
//         // Patch the enemy pathfinding algorithm, and make them ignore more optimal pathes and go directly through every cell
// 
//         var codeMatcher = new CodeMatcher(instructions, generator);
//         codeMatcher.Start()
//             .MatchForward(useEnd: false,
//                 new(OpCodes.Ldarg_0),
//                 new(OpCodes.Ldfld, typeof(SWorld_PF).GetField("m_grid")),
//                 new(OpCodes.Ldloc_0),
//                 new(OpCodes.Ldloc_1),
//                 new(OpCodes.Call, typeof(CCell[,]).GetMethod("Address")),
//                 new(OpCodes.Call, typeof(CCell).GetMethod("IsPassable")),
//                 new(OpCodes.Brfalse))
//             .ThrowIfInvalid("(1)")
//             .RemoveInstructions(7); // remove these instruction to make every cell behave like it is passble
//         return codeMatcher.Instructions();
//     }
// }

[BepInPlugin("ultra-hardcore", "Ultra Hardcore", "0.0.0")]
public class UltraHardcorePlugin : BaseUnityPlugin
{
    public static ConfigEntry<float> configPlayerHpMax;
    public static ConfigEntry<bool> configPermanentMist;
    public static ConfigEntry<bool> configPermanentAcidWater;

    private void Start() {
        Utils.UniqualizeVersionBuild(ref G.m_versionBuild, this);

        configPlayerHpMax = Config.Bind<float>(
            section: "UltraHardcore", key: "HpMax", defaultValue: 0f,
            configDescription: new ConfigDescription(
                "Maximum health for the player. '0' for default maximum health",
                new AcceptableValueRange<float>(0, float.MaxValue)
            )
        );
        configPermanentMist = Config.Bind<bool>(
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
        configPermanentAcidWater = Config.Bind<bool>(
            section: "UltraHardcore", key: "PermanentAcidWater", defaultValue: false,
            description: "Makes event 'ACIDIC WATERS' always active"
        );
        var configNoRegeneration = Config.Bind<bool>(
            section: "UltraHardcore", key: "NoRegeneration", defaultValue: false,
            description: "Removes regeneration from the player, so you can only gain health with potions"
        );
        var configNoQuickSaves = Config.Bind<bool>(
            section: "UltraHardcore", key: "NoQuickSaves", defaultValue: false,
            description: "Removes ability to quick save"
        );
        var configContinuousEvents = Config.Bind<bool>(
            section: "UltraHardcore", key: "ContinuousEvents", defaultValue: false,
            description: "Makes events always active. Note that day-only and night-only events will appear only at corresponding time of day"
        );
        var configInstantDrowning = Config.Bind<bool>(
            section: "UltraHardcore", key: "InstantDrowning", defaultValue: false,
            description: "Causes the player to instantly die by drowning (without slow loss of health)"
        );
        // var configEnemyNoDetourPathing = Config.Bind<bool>(
        //     section: "UltraHardcore", key: "EnemyNoDetourPathing", defaultValue: false,
        //     description: "Makes enemies to go ahead through cells ignoring it's durability"
        // );

        var harmony = new Harmony("ultra-hardcore");

        if (configPlayerHpMax.Value != 0f) {
            harmony.PatchAll(typeof(HpMaxPatch));
        }
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
        if (configNoQuickSaves.Value) {
            harmony.PatchAll(typeof(NoQuickSavesPatch));
        }
        if (configContinuousEvents.Value) {
            harmony.PatchAll(typeof(ContinuousEventsPatch));
        }
        if (configInstantDrowning.Value) {
            harmony.PatchAll(typeof(InstantDrowning));
        }
        // if (configEnemyNoDetourPathing.Value) {
        //     harmony.PatchAll(typeof(EnemyNoDetourPathing));
        // }
    }
}
