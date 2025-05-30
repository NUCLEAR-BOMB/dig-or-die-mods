﻿using BepInEx;
using HarmonyLib;
using ModUtils;
using ModUtils.Extensions;
using System.Collections.Generic;
using System.Configuration;
using System.Reflection.Emit;
using UnityEngine;

public static class PlayersDamagePlayersPatch {
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(CBullet), nameof(CBullet.CheckColWithUnits))]
    private static IEnumerable<CodeInstruction> CBullet_CheckColWithUnits(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
        var codeMatcher = new CodeMatcher(instructions, generator);
        
        codeMatcher.Start()
            .MatchForward(useEnd: false,
                new(OpCodes.Call, typeof(Vector2).Method("get_zero")),
                new(OpCodes.Stloc_S))
            .ThrowIfInvalid("(1)")
            .CreateLabel(out var successLabel);
        
        codeMatcher.Start()
            .MatchForward(useEnd: true,
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, typeof(CBullet).Field("m_unitsHit")),
                new(OpCodes.Ldloc_2),
                new(OpCodes.Callvirt, typeof(List<CUnit>).Method("Contains")),
                new(OpCodes.Brtrue))
            .ThrowIfInvalid("(2)")
            .Advance(1)
            .InjectAndAdvance(OpCodes.Ldarg_0)
            .CreateLabel(out var failLabel)
            .Insert(
                new(OpCodes.Ldfld, typeof(CBullet).Field("m_attacker")),
                new(OpCodes.Isinst, typeof(CUnitPlayer)),
                new(OpCodes.Brfalse, failLabel), // `m_attacker` is not CUnitPlayer
                new(OpCodes.Ldloc_2),
                new(OpCodes.Isinst, typeof(CUnitPlayer)),
                new(OpCodes.Brfalse, failLabel), // `cunit2` is CUnitPlayer
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, typeof(CBullet).Field("m_attacker")),
                new(OpCodes.Ldloc_2),
                new(OpCodes.Bne_Un, successLabel)); // `m_attacker` != `cunit2`

        return codeMatcher.Instructions();
    }
}

public static class DoDamageAOEToPlayers {
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(SUnits), nameof(SUnits.DoDamageAOE))]
    private static IEnumerable<CodeInstruction> SUnits_DoDamageAOE(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
        var codeMatcher = new CodeMatcher(instructions, generator);

        codeMatcher.Start()
            .MatchForward(useEnd: false,
                new(OpCodes.Ldarg_S, (byte)8),
                new(OpCodes.Ldc_R4, -3.4028235E+38f),
                new(OpCodes.Beq))
            .ThrowIfInvalid("(1)")
            .CreateLabel(out var successLabel);

        codeMatcher.Start()
            .MatchForward(useEnd: true,
                new(OpCodes.Ldarg_S, (byte)6),
                new(OpCodes.Brfalse))
            .ThrowIfInvalid("(2)")
            .Advance(1)
            .InjectAndAdvance(OpCodes.Ldloc_2)
            .Insert(
                new(OpCodes.Isinst, typeof(CUnitPlayer)),
                new(OpCodes.Brtrue, successLabel));

        return codeMatcher.Instructions();
    }
}

public static class HidePlayerNamesPatch {
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(SScreenHudWorld), nameof(SScreenHudWorld.OnUpdate))]
    private static IEnumerable<CodeInstruction> SScreenHudWorld_OnUpdate(IEnumerable<CodeInstruction> instructions) {
        var codeMatcher = new CodeMatcher(instructions);

        codeMatcher.Start()
            .MatchForward(useEnd: false,
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldc_I4_0),
                new(OpCodes.Call, typeof(CMesh<CMeshText>).Method("Get", [typeof(SScreen), typeof(bool)])))
            .ThrowIfInvalid("(1)")
            .SetAndAdvance(OpCodes.Nop, null)
            .RemoveInstructions(28);
        codeMatcher
            .MatchForward(useEnd: false,
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldc_I4_0),
                new(OpCodes.Call, typeof(CMesh<CMeshText>).Method("Get", [typeof(SScreen), typeof(bool)])),
                new(OpCodes.Ldloc_S),
                new(OpCodes.Ldfld, AccessTools.Field(typeof(CPlayer), nameof(CPlayer.m_lastChat))),
                new(OpCodes.Ldloca_S))
            .ThrowIfInvalid("(2)")
            .SetAndAdvance(OpCodes.Nop, null)
            .RemoveInstructions(22);

        return codeMatcher.Instructions();
    }
}

public static class HideMinimapPlayers_Patch {
    private static void HideMinimapPlayerIcon(CodeMatcher codeMatcher) {
        codeMatcher.Start()
            .MatchForward(useEnd: false,
                new(OpCodes.Ldloc_S),
                new(OpCodes.Callvirt, typeof(CPlayer).Method("HasUnitPlayer")),
                new(OpCodes.Brtrue))
            .ThrowIfInvalid("(1)");

        codeMatcher.Clone()
            .MatchForward(useEnd: false, new CodeMatch(OpCodes.Br))
            .ThrowIfInvalid("(2)")
            .GetOperand(out Label failLabel);

        codeMatcher
            .GetOperand(out LocalBuilder playerVar)
            .Insert(
                new(OpCodes.Ldloc_S, playerVar),
                new(OpCodes.Call, typeof(CPlayer).Method("IsMe")),
                new(OpCodes.Brfalse, failLabel));
    }
    private static void HideLiveViewPixels(CodeMatcher codeMatcher) {
        codeMatcher.Start()
            .MatchForward(useEnd: false,
                new CodeMatch(OpCodes.Callvirt, typeof(Texture2D).Method("SetPixels32", [typeof(int), typeof(int), typeof(int), typeof(int), typeof(Color32[])])))
            .ThrowIfInvalid("(3)")
            .Advance(1)
            .CreateLabel(out Label skipLabel)
            .Advance(-13)
            .Insert(
                new(OpCodes.Ldloc_1),
                new(OpCodes.Call, typeof(CPlayer).Method("IsMe")),
                new(OpCodes.Brfalse, skipLabel));
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(SMinimap), nameof(SMinimap.OnUpdate))]
    private static IEnumerable<CodeInstruction> SMinimap_OnUpdate(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
        var codeMatcher = new CodeMatcher(instructions, generator);

        HideMinimapPlayerIcon(codeMatcher);
        HideLiveViewPixels(codeMatcher);

        return codeMatcher.Instructions();
    }
}

public static class PlayerDamageToGroundPatch {
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(CBullet), nameof(CBullet.CheckColWithGround))]
    private static IEnumerable<CodeInstruction> CBullet_CheckColWithGround(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
        var codeMatcher = new CodeMatcher(instructions, generator);

        codeMatcher.Start()
            .MatchForward(useEnd: false,
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, typeof(CBullet).Field("m_attacker")),
                new(OpCodes.Isinst, typeof(CUnitMonster)),
                new(OpCodes.Brfalse))
            .CreateLabelAtOffset(4, out Label successLabel)
            .InjectAndAdvance(OpCodes.Ldarg_0)
            .Insert(
                new(OpCodes.Ldfld, typeof(CBullet).Field("m_attacker")),
                new(OpCodes.Isinst, typeof(CUnitPlayer)),
                new(OpCodes.Brtrue, successLabel));

        return codeMatcher.Instructions();
    }
}

public static class DefenseDamagePlayersPatch {
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(CBullet), nameof(CBullet.CheckColWithUnits))]
    private static IEnumerable<CodeInstruction> CBullet_CheckColWithUnits(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
        var codeMatcher = new CodeMatcher(instructions, generator);

        // (Start of collision check, end of unit type check)
        // call Vector2::get_zero()
        // stloc.s V_7
        codeMatcher.Start()
            .MatchForward(useEnd: false,
                new(OpCodes.Call, typeof(Vector2).Method("get_zero")),
                new(OpCodes.Stloc_S))
            .ThrowIfInvalid("(1)")
            .CreateLabel(out Label successLabel);

        codeMatcher.Start()
            .MatchForward(useEnd: false,
                new(OpCodes.Ldloc_S),
                new(OpCodes.Brtrue))
            .ThrowIfInvalid("(2)")
            // if (m_attacker is CUnitDefense && 
            //     cunit2 is CUnitPlayer && 
            //     m_attacker != cunit2) 
            //     -> jump to success (bypass original checks)
            .InjectAndAdvance(OpCodes.Ldarg_0)
            .CreateLabel(out Label failLabel)
            .Insert(
                new(OpCodes.Ldfld, typeof(CBullet).Field("m_attacker")),
                new(OpCodes.Isinst, typeof(CUnitDefense)),
                new(OpCodes.Brfalse, failLabel),
                new(OpCodes.Ldloc_2),
                new(OpCodes.Isinst, typeof(CUnitPlayer)),
                new(OpCodes.Brfalse, failLabel),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld, typeof(CBullet).Field("m_attacker")),
                new(OpCodes.Ldloc_2),
                new(OpCodes.Bne_Un, successLabel));

        return codeMatcher.Instructions();

        //          [ if (... && (m_unitsHit == null || ...) && ...) ]
        // ldarg.0
        // ldfld CBullet::m_unitsHit
        // brfalse            --------------------|
        //          [ if (... && (... || !m_unitsHit.Contains(cunit2))) && ...) ]
        // ldarg.0                                |
        // ldfld CBullet::m_unitsHit              |
        // ldloc.2                                |
        // callvirt List<CUnit>::Contains(CUnit)  |
        // brtrue             --------------------|--------|
        //                                        |        |
        // |> ldfld CBullet::m_attacker  <---------X----|  |
        // |> isinst CUnitDefense                       |  |
        // |> brfalse         --------------------------|  |
        // |> ldloc.2                                   |  |
        // |> isinst CUnitPlayer                        |  |
        // |> brfalse         --------------------------|  |
        // |> ldarg.0                                   |  |
        // |> ldfld CBullet::m_attacker                 |  |
        // |> ldloc.2                                   |  |
        // |> bne.un          -----------------------|  |  |
        //                                           |  |  |
        // ldloc.s   V_6             <---------------|---  |
        // brtrue                                    |     |
        // ...                                       |     |
        //         [ Collision checking start ]      |     |
        // call Vector2::get_zero()  <----------------     |
        // stloc.s V_7                                     |
        // ...                                             |
        //         [ Loop end ]                            |
        // ldloc.1                   <----------------------
        // ldc.i4.1
        // add
        // stloc.1
        // ...
    }
}

[BepInPlugin("friendly-fire", "Friendly Fire", "1.0.0")]
public class FriendlyFire : BaseUnityPlugin {
    private void Start() {
        Utils.UniqualizeVersionBuild(ref G.m_versionBuild, this);

        var configEnabled = Config.Bind<bool>(
            section: "General", key: "Enabled", defaultValue: true,
            description: "Enables the plugin"
        );
        var configDamageAOE = Config.Bind<bool>(
            section: "FriendlyFire", key: "DamageAOE", defaultValue: true,
            description: "Enables damage for players from explosions/lightning"
        );
        var configHideNames = Config.Bind<bool>(
            section: "FriendlyFire", key: "HideNames", defaultValue: false,
            description: "Hides other player names and chat messages above their heads"
        );
        var configHideMinimapPlayers = Config.Bind<bool>(
            section: "FriendlyFire", key: "HideMinimapPlayers", defaultValue: false,
            description: "Hides player icons from minimap"
        );
        var configPlayerDamageToGround = Config.Bind<bool>(
            section: "FriendlyFire", key: "PlayerDamageToGround", defaultValue: false,
            description: "Allows players to do damage to tiles"
        );
        var configDefenseDamagePlayers = Config.Bind<bool>(
            section: "FriendlyFire", key: "DefenseDamagePlayers", defaultValue: false,
            description: "Allows defense units (turrrets) to do damage to players"
        );
        if (!configEnabled.Value) { return; }

        var harmony = new Harmony("friendly-fire");

        harmony.PatchAll(typeof(PlayersDamagePlayersPatch));

        if (configDamageAOE.Value) {
            harmony.PatchAll(typeof(DoDamageAOEToPlayers));
        }
        if (configHideNames.Value) {
            harmony.PatchAll(typeof(HidePlayerNamesPatch));
        }
        if (configHideMinimapPlayers.Value) {
            harmony.PatchAll(typeof(HideMinimapPlayers_Patch));
        }
        if (configPlayerDamageToGround.Value) {
            harmony.PatchAll(typeof(PlayerDamageToGroundPatch));
        }
        if (configDefenseDamagePlayers.Value) {
            harmony.PatchAll(typeof(DefenseDamagePlayersPatch));
        }
    }

    // [HarmonyTranspiler]
    // [HarmonyPatch(typeof(CUnitPlayerLocal), "OnDeath")]
    // private static IEnumerable<CodeInstruction> CUnitPlayerLocal_OnDeath(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
    //     void OnDeathByPlayer(CUnitPlayerLocal self, CUnitPlayer attacker) {
    //         var SScreenHudChat_inst = Utils.SSingleton_Inst<SScreenHudChat>();
    //         var AddChatMessage_Local = AccessTools.Method(typeof(SScreenHudChat), "AddChatMessage_Local");
    // 
    //         string deathMessage = SLoc.GetText("CHAT_DEATH_KILLED", false, self.GetPlayer().m_name, attacker.GetPlayer().m_name);
    //         AddChatMessage_Local.Invoke(SScreenHudChat_inst, [null, deathMessage, false]);
    //     }
    // 
    //     var codeMatcher = new CodeMatcher(instructions, generator);
    // 
    //     codeMatcher.End()
    //         .MatchBack(useEnd: false,
    //             new CodeMatch(OpCodes.Ldarg_0),
    //             new CodeMatch(OpCodes.Ldarg_1),
    //             new CodeMatch(OpCodes.Ldsfld, AccessTools.Field(typeof(string), nameof(string.Empty))),
    //             new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(CUnitPlayer), "OnDeath")))
    //         .ThrowIfInvalid("friendly-fire transpiler: Failed to find `ldarg.0`, `ldarg.1`, `ldsfld string.Empty`, `call CUnitPlayer.OnDeath`")
    //         .CreateLabel(out var successLabel);
    // 
    //     codeMatcher.Start()
    //         // if (attacker != null && attacker != this && attacker is CUnitMonster)
    //         .MatchForward(useEnd: false,
    //             new CodeMatch(OpCodes.Ldarg_1),
    //             new CodeMatch(OpCodes.Brfalse),
    // 
    //             new CodeMatch(OpCodes.Ldarg_1),
    //             new CodeMatch(OpCodes.Ldarg_0),
    //             new CodeMatch(OpCodes.Beq),
    // 
    //             new CodeMatch(OpCodes.Ldarg_1),
    //             new CodeMatch(OpCodes.Isinst, typeof(CUnitMonster)),
    //             new CodeMatch(OpCodes.Brfalse))
    //         .Inject(OpCodes.Ldarg_1)
    //         .CreateLabel(out var failLabel)
    //         .Insert(
    //             // attacker != null
    //             // OpCodes.Ldarg_1
    //             new CodeInstruction(OpCodes.Brfalse, failLabel),
    //             // attacker != this
    //             new CodeInstruction(OpCodes.Ldarg_1),
    //             new CodeInstruction(OpCodes.Ldarg_0),
    //             new CodeInstruction(OpCodes.Beq, failLabel),
    //             // attacker is CUnitPlayer
    //             new CodeInstruction(OpCodes.Ldarg_1),
    //             new CodeInstruction(OpCodes.Isinst, typeof(CUnitPlayer)),
    //             new CodeInstruction(OpCodes.Brfalse, failLabel),
    // 
    //             new CodeInstruction(OpCodes.Ldarg_0), // arg 0: this
    //             new CodeInstruction(OpCodes.Ldarg_1), // arg 1: attacker
    //             Transpilers.EmitDelegate(OnDeathByPlayer),
    //             new CodeInstruction(OpCodes.Br, successLabel));
    // 
    //     return codeMatcher.Instructions();
    // }
}

