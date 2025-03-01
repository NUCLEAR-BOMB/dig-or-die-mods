﻿using BepInEx;
using HarmonyLib;
using ModUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;

[Serializable]
public class InvalidCommandArgument : Exception {
    public int? argIndex = null;

    public InvalidCommandArgument(string message) : base(message) {}

    public InvalidCommandArgument(string message, int argIndex) : base(message) {
        this.argIndex = argIndex;
    }
}

public static class CustomCommandsPatch {
    public delegate void ExecCommandFn(string[] args, CPlayer playerSender);
    public delegate List<string> TabCommandFn(int argIndex);

    public static readonly Dictionary<string, ExecCommandFn> customCommands = new();

    public static readonly Dictionary<string, TabCommandFn> customTabCommands = new();

    public static readonly Dictionary<string, string> customCommandHelpString = new();

    private static string[] ParseArgs(string text) {
        return text.Split([' ', '\t', '\r', '\n', '\v', '\f'], StringSplitOptions.RemoveEmptyEntries);
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(SNetworkCommands), nameof(SNetworkCommands.ProcessCommand))]
    private static IEnumerable<CodeInstruction> SNetworkCommands_ProcessCommand(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
        static bool ExecCustomCommand(string text, CPlayer playerSender) {
            string[] commandAndArgs = ParseArgs(text);
            string command = commandAndArgs[0];
            if (!customCommands.TryGetValue(command, out ExecCommandFn fn)) {
                return false;
            }
            string[] args = commandAndArgs.Skip(1).ToArray();

            try {
                fn(args, playerSender);
            } catch (InvalidCommandArgument exception) {
                if (!playerSender.IsMe()) { return true; }

                string errorMessage = exception.argIndex switch {
                    null => $"{command}: {exception.Message}",
                    int idx => $"{command}: {exception.Message} (argument {idx})"
                }; 
                SSingletonScreen<SScreenHudChat>.Inst.AddChatMessage_Local(null, errorMessage, false);
            }
            return true;
        }

        var codeMatcher = new CodeMatcher(instructions, generator);

        codeMatcher.Start()
            .MatchForward(useEnd: false,
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldsfld, Utils.StaticField(typeof(System.String), "Empty")),
                new(OpCodes.Ldarg_2),
                new(OpCodes.Ldc_I4_1),
                new(OpCodes.Call, AccessTools.Method(typeof(SNetworkCommands), "DrawHelp_IfSenderIsMe")),
                new(OpCodes.Ret))
            .CreateLabelAtOffset(5, out Label exitLabel)
            .InjectAndAdvance(OpCodes.Ldarg_1)
            .Insert(
                new(OpCodes.Ldarg_2),
                Transpilers.EmitDelegate(ExecCustomCommand),
                new(OpCodes.Brtrue, exitLabel));

        return codeMatcher.Instructions();
    }
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SNetworkCommands), nameof(SNetworkCommands.TabCommand))]
    private static void SNetworkCommands_TabCommand(SNetworkCommands __instance, string input, ref string __result) {
        string[] commandAndArgs = ParseArgs(input);
        string command = commandAndArgs[0];

        if (!customTabCommands.TryGetValue(command, out TabCommandFn tabCommand)) {
            return;
        }
        string arg = commandAndArgs.Length <= 1 ? "" : commandAndArgs[1];
        List<string> argList = tabCommand(commandAndArgs.Length);
        __result = __instance.TabOnList(__result, command, arg, argList);
    }
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SNetworkCommands), nameof(SNetworkCommands.DrawHelp_IfSenderIsMe))]
    private static void SNetworkCommands_DrawHelp_IfSenderIsMe(SNetworkCommands __instance, string command, CPlayer playerSender, bool all) {
        if (!playerSender.IsMe()) { return; }

        if (all) {
            foreach (var commandHelpString in customCommandHelpString.Values) {
                Utils.AddChatMessageLocalNL(commandHelpString);
            }
            return;
        }
        if (!customCommandHelpString.TryGetValue(command, out string helpString)) {
            return;
        }
        Utils.AddChatMessageLocalNL(helpString);
    }
}

[BepInPlugin("extra-commands", "Extra Commands", "1.0.0")]
public class ExtraCommands : BaseUnityPlugin {
    private void Start() {
        var harmony = new Harmony("extra-commands");
        harmony.PatchAll(typeof(CustomCommandsPatch));

        CustomCommands.AddCustomCommands();
    }
}

