using System.Numerics;
using ImGuiNET;
using NLua;
using NLua.Exceptions;
using Rained.Autotiles;
namespace Rained.LuaScripting;

class LuaAutotile : Autotile
{
    public LuaAutotile(LuaAutotileInterface wrapper) : base() {
        LuaWrapper = wrapper;
    }

    public LuaAutotile(LuaAutotileInterface wrapper, string name) : base(name) {
        LuaWrapper = wrapper;
    }

    public LuaFunction? LuaFillPathProcedure = null;
    public LuaFunction? LuaFillRectProcedure = null;
    public LuaFunction? OnOptionChanged = null;

    public LuaAutotileInterface LuaWrapper;

    public override bool AllowIntersections { get => LuaWrapper.AllowIntersections; }
    private List<string>? missingTiles = null;
    
    public enum ConfigDataType
    {
        Boolean, Integer
    };

    public class ConfigOption
    {
        public readonly string ID;
        public readonly string Name;
        public readonly ConfigDataType DataType;

        public bool BoolValue = false;
        public int IntValue = 0;
        public readonly int IntMin = int.MinValue;
        public readonly int IntMax = int.MaxValue;

        public ConfigOption(string id, string name, bool defaultValue)
        {
            ID = id;
            Name = name;
            DataType = ConfigDataType.Boolean;
            BoolValue = defaultValue;
        }

        public ConfigOption(string id, string name, int defaultValue, int min = int.MinValue, int max = int.MaxValue)
        {
            ID = id;
            Name = name;
            DataType = ConfigDataType.Integer;
            IntValue = Math.Clamp(defaultValue, min, max);
            IntMin = min;
            IntMax = max;
        }
    }

    public Dictionary<string, ConfigOption> Options = [];

    public void RunOptionChangeCallback(string id)
    {
        try
        {
            OnOptionChanged?.Call(LuaWrapper, id);
        }
        catch (LuaScriptException e)
        {
            LuaInterface.HandleException(e);
        }
    }

    public void AddOption(ConfigOption option)
    {
        Options.Add(option.ID, option);
    }

    public bool TryGetOption(string id, out ConfigOption? data)
    {
        return Options.TryGetValue(id, out data);
    }

    public override void TileRect(int layer, Vector2i rectMin, Vector2i rectMax, bool force, bool geometry)
    {
        if (CheckMissingTiles().Count > 0) return;

        string? modifierStr = null;
        if (geometry)
            modifierStr = "geometry";
        else if (force)
            modifierStr = "force";

        try
        {
            LuaFillRectProcedure?.Call(
                LuaWrapper, layer + 1,
                rectMin.X, rectMin.Y,
                rectMax.X, rectMax.Y,
                modifierStr
            );
        }
        catch (LuaScriptException e)
        {
            LuaInterface.HandleException(e);
        }
    }

    public override void TilePath(int layer, PathSegment[] pathSegments, bool force, bool geometry)
    {
        if (CheckMissingTiles().Count > 0) return;

        var luaState = LuaInterface.NLuaState;

        // push a new table on the stack with data
        // initialized to the given path segment
        static void CreateSegment(PathSegment seg)
        {
            var state = LuaInterface.NLuaState.State;
            state.NewTable();
            state.PushBoolean(seg.Left);
            state.SetField(-2, "left");
            state.PushBoolean(seg.Right);
            state.SetField(-2, "right");
            state.PushBoolean(seg.Up);
            state.SetField(-2, "up");
            state.PushBoolean(seg.Down);
            state.SetField(-2, "down");

            state.PushInteger(seg.X);
            state.SetField(-2, "x");
            state.PushInteger(seg.Y);
            state.SetField(-2, "y");
        }

        string? modifierStr = null;
        if (geometry)
            modifierStr = "geometry";
        else if (force)
            modifierStr = "force";

        // create segment table
        luaState.State.CreateTable(pathSegments.Length, 0);
        for (int i = 0; i < pathSegments.Length; i++)
        {
            CreateSegment(pathSegments[i]);

            // segmentTable[i+1] = newSegment
            // segmentTable is at stack index -2
            // newSegment is at the top of the stack
            luaState.State.RawSetInteger(-2, i + 1);
        }

        try
        {
            LuaTable segmentTable = (LuaTable) luaState.Pop();
            LuaFillPathProcedure?.Call(LuaWrapper, layer + 1, segmentTable, modifierStr);
        }
        catch (LuaScriptException e)
        {
            LuaInterface.HandleException(e);
        }
    }

    public override void ConfigGui()
    {
        if (CheckMissingTiles().Count > 0)
        {
            ImGui.Text("Missing required tiles:");
            foreach (var tileName in missingTiles!)
            {
                ImGui.BulletText(tileName);
            }
        }
        else
        {
            ImGui.PushItemWidth(ImGui.GetTextLineHeight() * 8.0f);

            foreach (var opt in Options.Values)
            {
                ImGui.PushID(opt.ID);

                if (opt.DataType == ConfigDataType.Boolean)
                {
                    if (ImGui.Checkbox(opt.Name, ref opt.BoolValue))
                        RunOptionChangeCallback(opt.ID);
                }
                else if (opt.DataType == ConfigDataType.Integer)
                {
                    if (ImGui.InputInt(opt.Name, ref opt.IntValue))
                        opt.IntValue = Math.Clamp(opt.IntValue, opt.IntMin, opt.IntMax);
                    
                    if (ImGui.IsItemDeactivatedAfterEdit())
                        RunOptionChangeCallback(opt.ID);
                }

                ImGui.PopID();
            }

            ImGui.PopItemWidth();
        }
    }

    public List<string> CheckMissingTiles()
    {
        if (missingTiles is not null) return missingTiles;
        missingTiles = [];

        var luaState = LuaInterface.NLuaState;

        var table = LuaWrapper.RequiredTiles;
        if (table is null) return missingTiles;

        for (int i = 1; table[i] is not null; i++)
        {
            if (table[i] is string tileName)
            {
                if (!RainEd.Instance.TileDatabase.HasTile(tileName))
                {
                    missingTiles.Add(tileName);
                }
            }
            else
            {
                luaState.Push(table[i]);
                LuaInterface.LogError($"invalid requiredTiles table for autotile '{Name}': expected string for item {i}, got {luaState.State.TypeName(-1)}");
                IsReady = false;
                break;
            }
        }

        if (missingTiles.Count > 0)
        {
            CanActivate = false;
        }

        return missingTiles;
    }
}

class LuaAutotileInterface
{
    [LuaHide]
    public readonly LuaAutotile autotile;

    [LuaMember(Name = "name")]
    public string Name { get => autotile.Name; set => autotile.Name = value; }

    [LuaMember(Name = "type")]
    public string Type
    {
        get => autotile.Type switch
        {
            AutotileType.Path => "path",
            AutotileType.Rect => "rect",
            _ => throw new Exception()
        };

        set => autotile.Type = value switch
        {
            "path" => AutotileType.Path,
            "rect" => AutotileType.Rect,
            _ => throw new Exception($"invalid autotile type '{value}'"),
        };
    }

    [LuaMember(Name = "allowIntersections")]
    public bool AllowIntersections = false;

    [LuaMember(Name = "tilePath")]
    public LuaFunction? TilePath { get => autotile.LuaFillPathProcedure; set => autotile.LuaFillPathProcedure = value; }
    
    [LuaMember(Name = "tileRect")]
    public LuaFunction? TileRect { get => autotile.LuaFillRectProcedure; set => autotile.LuaFillRectProcedure = value; }
    
    [LuaMember(Name = "requiredTiles")]
    public LuaTable? RequiredTiles = null;

    public LuaAutotileInterface()
    {
        autotile = new LuaAutotile(this);
    }

    [LuaMember(Name = "addToggleOption")]
    public void AddToggleOption(string id, string name, bool defaultValue)
        => autotile.AddOption(new LuaAutotile.ConfigOption(id, name, defaultValue));
    
    [LuaMember(Name = "addIntOption")]
    public void AddIntOption(string id, string name, int defaultValue)
        => autotile.AddOption(new LuaAutotile.ConfigOption(id, name, defaultValue));
    
    // min and max have to be doubles, as i want the user to be able to pass
    // math.huge into them
    [LuaMember(Name = "addIntOption")]
    public void AddIntOption(string id, string name, int defaultValue, double min, double max)
    {
        int intMin = double.IsPositiveInfinity(min) ? int.MinValue : (int) min;
        int intMax = double.IsPositiveInfinity(max) ? int.MaxValue : (int) max;
        autotile.AddOption(new LuaAutotile.ConfigOption(id, name, defaultValue, intMin, intMax));
    }

    [LuaMember(Name = "getOption")]
    public object? GetOption(string id)
    {
        if (autotile.TryGetOption(id, out LuaAutotile.ConfigOption? data))
        {
            if (data!.DataType == LuaAutotile.ConfigDataType.Boolean)
                return data.BoolValue;
            else if (data!.DataType == LuaAutotile.ConfigDataType.Integer)
                return data.IntValue;

            return null;
        }

        throw new LuaException($"option '{id}' does not exist");
    }

    [LuaMember(Name = "onOptionChanged")]
    public LuaFunction? OnOptionChanged { get => autotile.OnOptionChanged; set => autotile.OnOptionChanged = value; }
}