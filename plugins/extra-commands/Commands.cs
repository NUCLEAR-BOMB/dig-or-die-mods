using HarmonyLib;
using ModUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using UnityEngine;

public class AirCItemCell : CItemCell {
    public AirCItemCell() : base(tile: null, tileIcon: null, 0, 0) {
        m_id = 0;
        m_name = "Air";
        m_codeName = "air";
    }

    public static readonly CItemCell Inst = new AirCItemCell();
}

public static class CustomCommands {
    private static void AddCommand(
        string name,
        CustomCommandsPatch.ExecCommandFn fn,
        CustomCommandsPatch.TabCommandFn tabCommandFn = null
    ) {
        if (!name.StartsWith("/")) {
            throw new ArgumentException("Command name must start with '/'", nameof(name));
        }
        CustomCommandsPatch.customCommands.Add(name, fn);
        if (tabCommandFn is not null) {
            CustomCommandsPatch.customTabCommands.Add(name, tabCommandFn);
        }
    }
    private static CPlayer GetPlayerByName(string name) {
        return SNetwork.Players.FirstOrDefault(player => player.m_name == name);
    }
    private static List<string> GetListOfPlayersNames() {
        List<string> result = SNetwork.Players.Select(player => player.m_name).ToList();
        result.Sort();
        return result;
    }
    private static CItem ParseItem(string codeName) {
        if (codeName.StartsWith("#")) {
            if (!uint.TryParse(codeName.Substring(1), out uint itemId)) {
                throw new FormatException("Invalid item id");
            }
            if (itemId >= GItems.Items.Count) {
                throw new FormatException("Item id is out of range");
            }
            return GItems.Items[(int)itemId];
        }
        if (codeName == AirCItemCell.Inst.m_codeName) {
            return AirCItemCell.Inst;
        }
        var item = GItems.Items.Skip(1).FirstOrDefault(x => x.m_codeName == codeName);
        if (item is null) {
            throw new FormatException("Unknown item code name");
        }
        return item;
    }
    private class SetCellArgs {
        public uint flags = 0;
        public bool replaceBackground = false;
        public ushort hp = ushort.MaxValue;
        public short forceX = 0;
        public short forceY = 0;
        public float water = 0f;
        public Color24 light = default;
        public byte elecProd = 0;
        public byte elecCons = 0;
        public Color24 temp = default;

        public static readonly SetCellArgs Default = new();
    }
    private static void SetCell(int i, int j, CItemCell cell, SetCellArgs args = null) {
        args ??= SetCellArgs.Default;

        ref CCell selectedCell = ref SWorld.Grid[i, j];
        CItemCell prevContent = selectedCell.GetContent();
        selectedCell.m_contentId = cell.m_id;
        selectedCell.m_contentHP = args.hp == ushort.MaxValue ? cell.m_hpMax : args.hp;

        if (!args.replaceBackground) {
            selectedCell.m_flags &= (CCell.Flag_BackWall_0 | CCell.Flag_BgSurface_0 | CCell.Flag_BgSurface_1 | CCell.Flag_BgSurface_2);
            selectedCell.m_flags |= args.flags;
        } else {
            selectedCell.m_flags = args.flags;
        }
        selectedCell.m_forceX = args.forceX;
        selectedCell.m_forceY = args.forceY;
        selectedCell.m_water = args.water;
        selectedCell.m_light = args.light;
        selectedCell.m_elecProd = args.elecProd;
        selectedCell.m_elecCons = args.elecCons;
        selectedCell.m_temp = args.temp;

        SWorldNetwork.OnSetContent(i, j, true, prevContent);
    }
    private static void SetCell(int2 pos, CItemCell cell, SetCellArgs args = null) {
        SetCell(pos.x, pos.y, cell, args);
    }

    private class ParseCellResult {
        public CItemCell item;
        public SetCellArgs parameters = new();
    }

    private static ParseCellResult ParseCellParameters(string str) {
        int codeNameEnd = str.IndexOf('{');
        string codeName = str.Substring(0, codeNameEnd == -1 ? str.Length : codeNameEnd);

        CItem item = ParseItem(codeName);
        if (item is null) {
            throw new FormatException("Unknown item code name");
        }
        if (item is not CItemCell itemCell) {
            throw new FormatException("Expected item cell, not regular item");
        }
        var result = new ParseCellResult() { item = itemCell };
        result.parameters.hp = itemCell.m_hpMax;

        if (codeNameEnd == -1) {
            return result;
        }
        if (str[str.Length - 1] != '}') {
            throw new FormatException("Unmatched '}'");
        }
        var cellParamsStr = str.Remove(str.Length - 1).Substring(codeNameEnd + 1).Split(',').Select(x => x.Trim());
        var parameters = result.parameters;

        void SetFlag(uint flag, string val) {
            Utils.SetFlag(ref parameters.flags, flag, Utils.ParseBool(val));
        }
        foreach (var cellParamStr in cellParamsStr) {
            string[] paramNameAndValue = cellParamStr.Split('=');
            if (paramNameAndValue.Length != 2) {
                throw new FormatException("There must be only one '='");
            }
            string paramName = paramNameAndValue[0];
            string paramValue = paramNameAndValue[1];

            switch (paramName.ToLower()) {
            case "hp": parameters.hp = ushort.Parse(paramValue); break;
            case "forcex": parameters.forceX = short.Parse(paramValue); break;
            case "forcey": parameters.forceY = short.Parse(paramValue); break;
            case "water": parameters.water = float.Parse(paramValue); break;
            case "elecprod": parameters.elecProd = byte.Parse(paramValue); break;
            case "eleccons": parameters.elecCons = byte.Parse(paramValue); break;
            case "data0": SetFlag(CCell.Flag_CustomData0, paramValue); break;
            case "data1": SetFlag(CCell.Flag_CustomData1, paramValue); break;
            case "data2": SetFlag(CCell.Flag_CustomData2, paramValue); break;
            case "burning": SetFlag(CCell.Flag_IsBurning, paramValue); break;
            case "mapped": SetFlag(CCell.Flag_IsMapped, paramValue); break;
            case "backwall": SetFlag(CCell.Flag_BackWall_0, paramValue); break;
            case "bg0": SetFlag(CCell.Flag_BgSurface_0, paramValue); parameters.replaceBackground = true; break;
            case "bg1": SetFlag(CCell.Flag_BgSurface_1, paramValue); parameters.replaceBackground = true; break;
            case "bg2": SetFlag(CCell.Flag_BgSurface_2, paramValue); parameters.replaceBackground = true; break;
            case "waterfall": SetFlag(CCell.Flag_WaterFall, paramValue); break;
            case "streamlfast": SetFlag(CCell.Flag_StreamLFast, paramValue); break;
            case "streamrfast": SetFlag(CCell.Flag_StreamRFast, paramValue); break;
            case "lava": SetFlag(CCell.Flag_IsLava, paramValue); break;
            case "haswireright": SetFlag(CCell.Flag_HasWireRight, paramValue); break;
            case "haswiretop": SetFlag(CCell.Flag_HasWireTop, paramValue); break;
            case "electricalgostate": SetFlag(CCell.Flag_ElectricAlgoState, paramValue); break;
            case "powered": SetFlag(CCell.Flag_IsPowered, paramValue); break;
            case "light": parameters.light = Utils.ParseColor24(paramValue); break;
            case "temp": parameters.temp = Utils.ParseColor24(paramValue); break;
            default: throw new FormatException($"Unknown cell parameter '{paramName}'");
            }
        }
        return result;
    }

    private static List<string> GetListOfCCellItemNames() {
        return GItems.Items.Skip(1).Where(x => x is CItemCell).Select(x => x.m_codeName).ToList();
    }
    private static CUnit.CDesc ParseUnitCDesc(string codeName) {
        if (codeName.StartsWith("#")) {
            if (!uint.TryParse(codeName.Substring(1), out uint unitId)) {
                throw new FormatException("Invalid unit id");
            }
            if (unitId >= GUnits.UDescs.Count) {
                throw new FormatException("Unit id is out of range");
            }
            return GUnits.UDescs[(int)unitId];
        }

        var unit = GUnits.UDescs.Skip(1).FirstOrDefault(x => x.m_codeName == codeName);
        if (unit is null) {
            throw new FormatException("Unknown unit code name");
        }
        return unit;
    }
    private static bool ParseCoordinate(string input, int playerPos, int playerCursorPos, out int result) {
        int num = 0;
        result = 0;
        if (input.StartsWith("~")) {
            if (input.Length == 1 || int.TryParse(input.Substring(1), out num)) {
                result = num + playerPos;
                return true;
            } else {
                return false;
            }
        }
        if (input.StartsWith("^")) {
            if (input.Length == 1 || int.TryParse(input.Substring(1), out num)) {
                result = num + playerCursorPos;
                return true;
            } else {
                return false;
            }
        }
        if (int.TryParse(input, out num)) {
            result = num;
            return true;
        } else {
            return false;
        }
    }
    private static bool ParseCoordinate(string input, float playerPos, float playerCursorPos, out float result) {
        float num = 0;
        result = 0;
        if (input.StartsWith("~")) {
            if (input.Length == 1 || float.TryParse(input.Substring(1), out num)) {
                result = num + playerPos;
                return true;
            } else {
                return false;
            }
        }
        if (input.StartsWith("^")) {
            if (input.Length == 1 || float.TryParse(input.Substring(1), out num)) {
                result = num + playerCursorPos;
                return true;
            } else {
                return false;
            }
        }
        if (float.TryParse(input, out num)) {
            result = num;
            return true;
        } else {
            return false;
        }
    }
    private static Vector2 ArgParseXYCoordinate(string[] args, int argXIndex, int argYIndex, CPlayer player) {
        if (args.Length <= argXIndex) {
            throw new InvalidCommandArgument($"Expected X coordinate (number)", argXIndex + 1);
        }
        if (!ParseCoordinate(args[argXIndex], player.m_unitPlayer.Pos.x, SGame.MouseWorldPos.x, out float coordX)) {
            throw new InvalidCommandArgument($"Invalid X coordinate (number)", argXIndex + 1);
        }
        if (args.Length <= argYIndex) {
            throw new InvalidCommandArgument($"Expected Y coordinate (number)", argYIndex + 1);
        }
        if (!ParseCoordinate(args[argYIndex], player.m_unitPlayer.Pos.y, SGame.MouseWorldPos.y, out float coordY)) {
            throw new InvalidCommandArgument($"Invalid Y coordinate (number)", argYIndex + 1);
        }
        return new Vector2(coordX, coordY);
    }
    private static int2 ArgParseXYCoordinateInt(string[] args, int argXIndex, int argYIndex, CPlayer player) {
        if (args.Length <= argXIndex) {
            throw new InvalidCommandArgument($"Expected X coordinate (number)", argXIndex + 1);
        }
        if (!ParseCoordinate(args[argXIndex], player.m_unitPlayer.PosCell.x, SGame.MouseWorldPosInt.x, out int coordX)) {
            throw new InvalidCommandArgument($"Invalid X coordinate (number)", argXIndex + 1);
        }
        if (args.Length <= argYIndex) {
            throw new InvalidCommandArgument($"Expected Y coordinate (integer)", argYIndex + 1);
        }
        if (!ParseCoordinate(args[argYIndex], player.m_unitPlayer.PosCell.y, SGame.MouseWorldPosInt.y, out int coordY)) {
            throw new InvalidCommandArgument($"Invalid Y coordinate (integer)", argYIndex + 1);
        }
        return new int2(coordX, coordY);
    }

    static public void AddCustomCommands() {
        AddCommand("/tp", (string[] args, CPlayer player) => {
            if (args.Length == 0) {
                throw new InvalidCommandArgument("Expected number or player name", 1);
            }

            if (args.Length == 1) {
                CPlayer targetPlayer = GetPlayerByName(args[0]);
                if (targetPlayer == null) {
                    throw new InvalidCommandArgument("Unknown player name", 1);
                }
                player.m_unitPlayer.Pos = targetPlayer.m_unitPlayer.Pos;
            } else {
                Vector2 pos = ArgParseXYCoordinate(args, argXIndex: 0, argYIndex: 1, player);
                if (!SWorld.GridRectM2.Contains(pos)) {
                    throw new InvalidCommandArgument("The position is out of the world");
                }
                player.m_unitPlayer.Pos = pos;
                Utils.AddChatMessageLocal($"Teleported to {pos}");
            }
        }, tabCommandFn: (int argIndex) => {
            return GetListOfPlayersNames();
        });
        AddCommand("/give", (string[] args, CPlayer player) => {
            if (args.Length == 0) {
                throw new InvalidCommandArgument("Expected item name", 1);
            }
            CItem selectedItem;
            try {
                selectedItem = ParseItem(args[0]);
            } catch (FormatException formatException) {
                throw new InvalidCommandArgument(formatException.Message, 1);
            }
            if (selectedItem is null) {
                throw new InvalidCommandArgument("Cannot give null item", 1);
            }

            int itemCount = 1;
            if (args.Length >= 2) {
                if (!int.TryParse(args[1], out itemCount)) {
                    throw new InvalidCommandArgument("Expected number of items", 2);
                }
            }
            Utils.AddChatMessageLocal($"Given {itemCount} {selectedItem.Name}");
            player.m_inventory.AddToInventory(selectedItem, itemCount);
        }, tabCommandFn: (int argIndex) => {
            return GItems.Items.Skip(1).Select(x => x.m_codeName).ToList();
        });
        AddCommand("/place", (string[] args, CPlayer player) => {
            if (args.Length == 0) {
                throw new InvalidCommandArgument("Expected item cell code name", 1);
            }
            ParseCellResult selectedCell;
            try {
                selectedCell = ParseCellParameters(args[0]);
            } catch (Exception ex) when (ex is FormatException || ex is OverflowException) {
                throw new InvalidCommandArgument(ex.Message, 1);
            }
            int2 pos = ArgParseXYCoordinateInt(args, argXIndex: 1, argYIndex: 2, player);

            if (!Utils.IsInWorld(pos)) {
                throw new InvalidCommandArgument("The cell position is out of the world");
            }
            Utils.AddChatMessageLocal($"Replaced cell at {pos} with {selectedCell.item.Name}");
            SetCell(pos, selectedCell.item, selectedCell.parameters);
        }, tabCommandFn: (int argIndex) => {
            return GetListOfCCellItemNames();
        });
        AddCommand("/fill", (string[] args, CPlayer player) => {
            if (args.Length == 0) {
                throw new InvalidCommandArgument("Expected item cell code name", 1);
            }
            ParseCellResult selectedCell;
            try {
                selectedCell = ParseCellParameters(args[0]);
            } catch (FormatException formatException) {
                throw new InvalidCommandArgument(formatException.Message, 1);
            }

            int2 from = ArgParseXYCoordinateInt(args, argXIndex: 1, argYIndex: 2, player);
            int2 to = ArgParseXYCoordinateInt(args, argXIndex: 3, argYIndex: 4, player);

            if (!Utils.IsInWorld(from)) {
                throw new InvalidCommandArgument($"The cell 'from' position is out of the world {from}");
            }
            if (!Utils.IsInWorld(to)) {
                throw new InvalidCommandArgument($"The cell 'to' position is out of the world {to}");
            }
            if (from.x > to.x) { Utils.Swap(ref to.x, ref from.x); }
            if (from.y > to.x) { Utils.Swap(ref to.y, ref from.y); }

            int replacedCellsNum = Math.Max(0, to.x - from.x + 1) * Math.Max(0, to.y - from.y + 1);

            Utils.AddChatMessageLocal(
                $"Filled cells from {from} to {to} with {selectedCell.item.Name}. " +
                $"Total replaced cells: {replacedCellsNum}"
            );
            for (int x = from.x; x <= to.x; ++x) {
                for (int y = from.y; y <= to.y; ++y) {
                    SetCell(x, y, selectedCell.item, selectedCell.parameters);
                }
            }
        }, tabCommandFn: (int argIndex) => {
            return GetListOfCCellItemNames();
        });
        AddCommand("/killinfo", (string[] args, CPlayer player) => {
            if (args.Length > 0) {
                throw new InvalidCommandArgument("None arguments are expected");
            }
            foreach (var specieKilled in SSingleton<SUnits>.Inst.SpeciesKilled) {
                Utils.AddChatMessageLocal($"{specieKilled.m_uDesc.GetName()}: {specieKilled.m_nb} ({GVars.SimuTime - specieKilled.m_lastKillTime:0.00})");
            }
        });
        AddCommand("/spawn", (string[] args, CPlayer player) => {
            if (args.Length == 0) {
                throw new InvalidCommandArgument("Expected item cell code name", 1);
            }
            CUnit.CDesc selectedUnit;
            try {
                selectedUnit = ParseUnitCDesc(args[0]);
            } catch (FormatException formatException) {
                throw new InvalidCommandArgument(formatException.Message, 1);
            }
            if (selectedUnit is null) {
                throw new InvalidCommandArgument("Cannot spawn null unit", 1);
            }
            Vector2 spawnPos = ArgParseXYCoordinate(args, argXIndex: 1, argYIndex: 2, player);

            if (!SWorld.GridRectM2.Contains(spawnPos)) {
                throw new InvalidCommandArgument($"The spawn position is out of the world {spawnPos}");
            }
            Utils.AddChatMessageLocal($"Spawned unit {selectedUnit.GetName()} at {spawnPos}");
            SUnits.SpawnUnit(selectedUnit, spawnPos);
        }, tabCommandFn: (int argIndex) => {
            return GUnits.UDescs.Skip(1).Select(x => x.m_codeName).ToList();
        });
        AddCommand("/clearinventory", (string[] args, CPlayer player) => {
            if (args.Length > 0) {
                throw new InvalidCommandArgument("None arguments are expected");
            }
            Utils.AddChatMessageLocal($"Cleared {player.m_inventory.Items.Count} items from inventory");
            player.m_inventory.CleanAll();
        });
        AddCommand("/clearpickups", (string[] args, CPlayer player) => {
            if (args.Length > 0) {
                throw new InvalidCommandArgument("None arguments are expected");
            }
            Utils.AddChatMessageLocal($"Cleared {SPickups.Pickups.Count} pickups");
            SSingleton<SPickups>.Inst.CleanAll();
        });
        AddCommand("/clone", (string[] args, CPlayer player) => {
            int2 srcFrom = ArgParseXYCoordinateInt(args, argXIndex: 0, argYIndex: 1, player);
            int2 srcTo = ArgParseXYCoordinateInt(args, argXIndex: 2, argYIndex: 3, player);
            int2 dest = ArgParseXYCoordinateInt(args, argXIndex: 4, argYIndex: 5, player);
            
            if (!Utils.IsInWorld(srcFrom)) {
                throw new InvalidCommandArgument($"The start source position is out of the world {srcFrom}");
            }
            if (!Utils.IsInWorld(srcTo)) {
                throw new InvalidCommandArgument($"The end source position is out of the world {srcTo}");
            }
            if (srcFrom.x > srcTo.x) { Utils.Swap(ref srcTo.x, ref srcFrom.x); }
            if (srcFrom.y > srcTo.y) { Utils.Swap(ref srcTo.y, ref srcFrom.y); }

            if (!Utils.IsInWorld(dest)) {
                throw new InvalidCommandArgument($"The start destination position is out of the world {dest}");
            }
            if (!Utils.IsInWorld(dest + (srcTo - srcFrom))) {
                throw new InvalidCommandArgument($"The end destination position is out of the world {dest + (srcTo - srcFrom)}");
            }

            int clonedCellsNum = Math.Max(0, srcTo.x - srcFrom.x + 1) * Math.Max(0, srcTo.y - srcFrom.y + 1);
            Utils.AddChatMessageLocal(
                $"Cloned cells from source region {srcFrom}-{srcTo} to destination starting at {dest}. " +
                $"Total cloned cells: {clonedCellsNum}"
            );

            bool isOverlapping = (dest.y >= srcFrom.y);
            int iStep = !isOverlapping ? 1 : -1;
            int iStart = !isOverlapping ? 0 : srcTo.x - srcFrom.x - 1;
            int iEnd = !isOverlapping ? srcTo.x - srcFrom.x : -1;

            int copyLength = srcTo.y - srcFrom.y;
            for (int i = iStart; i != iEnd; i += iStep) {
                int srcIdx = srcFrom.y + (i + srcFrom.x) * SWorld.Gs.y;
                int destIdx = dest.y + (i + dest.x) * SWorld.Gs.y;
                Array.Copy(SWorld.Grid, srcIdx, SWorld.Grid, destIdx, copyLength);
            }
        });
    }
}
