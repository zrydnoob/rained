using System.Text;
using NLua;
using NLua.Exceptions;
using Rained.Autotiles;
using Rained.EditorGui;
using Rained.LevelData;
namespace Rained.LuaScripting;

static class LuaInterface
{
    public const int VersionMajor = 2;
    public const int VersionMinor = 1;
    public const int VersionRevision = 0;
    
    static private Lua luaState = null!;
    public static Lua NLuaState { get => luaState; }
    
    delegate void LuaPrintDelegate(params string[] args);

    private static LuaHelpers.LuaFunction loaderDelegate = new(RainedLoader);
    private static string scriptsPath = null!;

    private static Dictionary<int, int> registeredCmds = [];
    private const string CommandID = "RainedCommandID";

    public static void Initialize()
    {
        scriptsPath = Path.GetRelativePath(Environment.CurrentDirectory, Path.Combine(Boot.AppDataPath, "scripts"));

        luaState = new Lua()
        {
            UseTraceback = true
        };
        LuaHelpers.Init(luaState.State);

        luaState["print"] = new LuaPrintDelegate(LuaPrint);
        luaState["warn"] = new LuaPrintDelegate(LuaWarning); 

        // configure package.path
        var package = (LuaTable) luaState["package"];
        package["path"] = Path.Combine(scriptsPath, "?.lua") + ";" + Path.Combine(scriptsPath, "?", "init.lua");

        // global functions
        LuaHelpers.PushCsFunction(luaState.State, new Action<string, bool?>(AutoRequire));
        luaState.State.SetGlobal("autorequire");

        // add rained module to require preloader
        // (this is just so that my stupid/smart lua linter doesn't give a bunch of warnings about an undefined global)
        luaState.State.GetSubTable((int)KeraLua.LuaRegistry.Index, "_PRELOAD");
        LuaHelpers.PushLuaFunction(luaState.State, loaderDelegate);
        luaState.State.SetField(-2, "rained");
        luaState.State.Pop(1); // pop preload table

        // initialize global variables
        string globalsInit = """
        rained = require("rained")

        GEO_TYPE = {
            AIR = 0,
            SOLID = 1,
            SLOPE_RIGHT_UP = 2,
            SLOPE_LEFT_UP = 3,
            SLOPE_RIGHT_DOWN = 4,
            SLOPE_LEFT_DOWN = 5,
            FLOOR = 6,
            SHORTCUT_ENTRANCE = 7,
            GLASS = 9
        }

        OBJECT_TYPE = {
            NONE = 0,
            HORIZONTAL_BEAM = 1,
            VERTICAL_BEAM = 2,
            HIVE = 3,
            SHORTCUT = 5,
            ENTRANCE = 6,
            CREATURE_DEN = 7,
            ROCK = 9,
            SPEAR = 10,
            CRACK = 11,
            FORBID_FLY_CHAIN = 12,
            GARBAGE_WORM = 13,
            WATERFALL = 18,
            WHACK_A_MOLE_HOLE = 19,
            WORM_GRASS = 20,
            SCAVENGER_HOLE = 21
        }
        """;
        luaState.DoString(globalsInit);

        luaState.State.NewMetaTable(CommandID);
        LuaHelpers.PushLuaFunction(luaState.State, static (KeraLua.Lua lua) =>
        {
            luaState.State.PushString("The metatable is locked!");
            return 1;
        });
        luaState.State.SetField(-2, "__metatable");

        // disable NLua import function
        // damn... stupid nlua library
        // i can't disable the debug library even though
        // 1. there is a function called luaL_traceback
        // 2. they could have just saved the function from the debug library into the registry
        // ...
        luaState.DoString("import = nil");

        luaState.DoFile(Path.Combine(scriptsPath, "init.lua"));
        if (Directory.Exists(Path.Combine(scriptsPath, "autoload")))
        {
            luaState.DoString("autorequire('autoload', true)");
        }
    }

    public static void HandleException(LuaScriptException e)
    {
        EditorWindow.ShowNotification("Error!");

        Exception actualException = e.IsNetException ? e.InnerException! : e;
        string? stackTrace = actualException.Data["Traceback"] as string;
        LogError(stackTrace is not null ? actualException.Message + '\n' + stackTrace : actualException.Message);
    }

    private static int RainedLoader(KeraLua.Lua lua)
    {
        lua.NewTable();

        luaState.Push(new Func<string>(GetVersion));
        lua.SetField(-2, "getVersion");

        // function getApiVersion
        LuaHelpers.PushLuaFunction(lua, static (KeraLua.Lua lua) =>
        {
            lua.PushInteger(VersionMajor);
            lua.PushInteger(VersionMinor);
            lua.PushInteger(VersionRevision);
            return 3;
        });
        lua.SetField(-2, "getApiVersion");

        luaState.Push(new Action<string>(ShowNotification));
        lua.SetField(-2, "alert");

        // function registerCommand
        LuaHelpers.PushLuaFunction(lua, static (KeraLua.Lua lua) => {
            string name = lua.CheckString(1);
            lua.CheckType(2, KeraLua.LuaType.Function);

            lua.PushCopy(2);
            int funcRef = lua.Ref(KeraLua.LuaRegistry.Index);
            int cmdId = RainEd.Instance.RegisterCommand(name, RunCommand);
            registeredCmds[cmdId] = funcRef;

            unsafe
            {
                var ud = (int*) lua.NewUserData(sizeof(int));
                lua.SetMetaTable(CommandID);
                *ud = cmdId;
            }

            return 1;
        });
        lua.SetField(-2, "registerCommand");

        // function getLevelWidth
        LuaHelpers.PushLuaFunction(lua, static (KeraLua.Lua lua) => {
            lua.PushInteger(RainEd.Instance.Level.Width);
            return 1;
        });
        lua.SetField(-2, "getLevelWidth");

        // function getLevelHeight
        LuaHelpers.PushLuaFunction(lua, static (KeraLua.Lua lua) => {
            lua.PushInteger(RainEd.Instance.Level.Height);
            return 1;
        });
        lua.SetField(-2, "getLevelHeight");

        // function isInBounds
        luaState.Push(static (int x, int y) =>
        {
            return RainEd.Instance.Level.IsInBounds(x, y);
        });
        lua.SetField(-2, "isInBounds");

        // CELLS namespace
        {
            lua.NewTable();

            // function getGeo
            luaState.Push(static (int x, int y, int layer) =>
            {
                layer--;
                if (!RainEd.Instance.Level.IsInBounds(x, y)) return 0;
                if (layer < 0 || layer > 2) return 0;
                return (int) RainEd.Instance.Level.Layers[layer, x, y].Geo;
            });
            lua.SetField(-2, "getGeo");

            // function setGeo
            luaState.Push(static (int x, int y, int layer, int geoType) =>
            {
                layer--;
                if (!RainEd.Instance.Level.IsInBounds(x, y)) return;
                if (layer < 0 || layer > 2) return;
                if (geoType < 0 || geoType == 8 || geoType > 9) throw new Exception("invalid geo type " + geoType);
                RainEd.Instance.Level.Layers[layer, x, y].Geo = (GeoType) geoType;
                RainEd.Instance.LevelView.Renderer.InvalidateGeo(x, y, layer);
            });
            lua.SetField(-2, "setGeo");

            // function getMaterial
            luaState.Push(static (int x, int y, int layer) =>
            {
                layer--;
                if (!RainEd.Instance.Level.IsInBounds(x, y)) return null;
                if (layer < 0 || layer > 2) return null;

                var idx = RainEd.Instance.Level.Layers[layer, x, y].Material;
                if (idx == 0) return null;
                return RainEd.Instance.MaterialDatabase.GetMaterial(idx)?.Name;
            });
            lua.SetField(-2, "getMaterial");

            // function setMaterial
            LuaHelpers.PushLuaFunction(lua, static (KeraLua.Lua lua) =>
            {
                var x = (int) lua.CheckNumber(1);
                var y = (int) lua.CheckNumber(2);
                var layer = (int) lua.CheckNumber(3) - 1;
                string? matName = lua.IsNil(4) ? null : lua.CheckString(4);

                int matId;
                if (matName is not null)
                {
                    var mat = RainEd.Instance.MaterialDatabase.GetMaterial(matName);
                    if (mat is null)
                        return lua.ArgumentError(4, $"'{matName}' is not a recognized material");
                    matId = mat.ID;
                }
                else
                {
                    matId = 0;
                }

                if (!RainEd.Instance.Level.IsInBounds(x, y)) return 0;
                if (layer < 0 || layer > 2) return 0;
                RainEd.Instance.Level.Layers[layer, x, y].Material = matId;
                return 0;
            });
            lua.SetField(-2, "setMaterial");

            // function setMaterialId
            LuaHelpers.PushLuaFunction(lua, static (KeraLua.Lua lua) =>
            {
                var x = (int) lua.CheckNumber(1);
                var y = (int) lua.CheckNumber(2);
                var layer = (int) lua.CheckNumber(3) - 1;
                var matId = (int) lua.CheckNumber(4);
                
                if (!RainEd.Instance.Level.IsInBounds(x, y)) return 0;
                if (layer < 0 || layer > 2) return 0;
                RainEd.Instance.Level.Layers[layer, x, y].Material = matId;
                return 0;
            });
            lua.SetField(-2, "setMaterialId");

            // function getMaterial
            luaState.Push(static (int x, int y, int layer) =>
            {
                layer--;
                if (!RainEd.Instance.Level.IsInBounds(x, y)) return 0;
                if (layer < 0 || layer > 2) return 0;

                return RainEd.Instance.Level.Layers[layer, x, y].Material;
            });
            lua.SetField(-2, "getMaterialId");

            // function getObjects
            LuaHelpers.PushLuaFunction(lua, static (KeraLua.Lua lua) =>
            {
                int x = (int) lua.CheckNumber(1);
                int y = (int) lua.CheckNumber(2);
                int layer = (int) lua.CheckInteger(3) - 1;

                lua.NewTable();
                if (!RainEd.Instance.Level.IsInBounds(x, y)) return 1;
                if (layer < 0 || layer > 2) return 1;

                ref var cell = ref RainEd.Instance.Level.Layers[layer, x, y];
                for (int i = 1; i < 32; i++)
                {
                    if (cell.Has((LevelObject)(1 << (i-1))))
                    {
                        lua.PushInteger(i);
                        lua.SetInteger(-2, lua.Length(-2) + 1);
                    }
                }

                return 1;
            });
            lua.SetField(-2, "getObjects");

            // function setObjects
            LuaHelpers.PushLuaFunction(lua, static (KeraLua.Lua lua) =>
            {
                int x = (int) lua.CheckNumber(1);
                int y = (int) lua.CheckNumber(2);
                int layer = (int) lua.CheckInteger(3) - 1;
                lua.CheckType(4, KeraLua.LuaType.Table);
                
                if (!RainEd.Instance.Level.IsInBounds(x, y)) return 0;
                if (layer < 0 || layer > 2) return 0;

                LevelObject objects = 0;

                for (int i = 1; i <= lua.Length(4); i++)
                {
                    int k = (int) lua.GetInteger(4, i);
                    if (k < 1 || k >= 32) throw new LuaHelpers.LuaErrorException("invalid geometry object");
                    objects |= (LevelObject)(1 << (k-1));
                }

                RainEd.Instance.Level.Layers[layer, x, y].Objects = objects;
                RainEd.Instance.LevelView.Renderer.InvalidateGeo(x, y, layer);

                return 0;
            });
            lua.SetField(-2, "setObjects");

            // function getTileData
            LuaHelpers.PushLuaFunction(lua, static (KeraLua.Lua lua) =>
            {
                int x = (int) lua.CheckNumber(1);
                int y = (int) lua.CheckNumber(2);
                int layer = (int) lua.CheckInteger(3) - 1;

                if (!RainEd.Instance.Level.IsInBounds(x, y)) return 0;
                if (layer < 0 || layer > 2) return 0;

                ref var cell = ref RainEd.Instance.Level.Layers[layer, x, y];

                if (cell.TileHead is not null)
                    lua.PushString(cell.TileHead.Name);
                else
                    lua.PushNil();
                
                if (cell.TileRootX >= 0)
                    lua.PushInteger(cell.TileRootX);
                else
                    lua.PushNil();
                
                if (cell.TileRootY >= 0)
                    lua.PushInteger(cell.TileRootY);
                else
                    lua.PushNil();
                
                if (cell.TileLayer >= 0)
                    lua.PushInteger(cell.TileLayer);
                else
                    lua.PushNil();
                
                return 4;
            });
            lua.SetField(-2, "getTileData");

            // function setTileHead
            LuaHelpers.PushLuaFunction(lua, static (KeraLua.Lua lua) =>
            {
                int x = (int) lua.CheckNumber(1);
                int y = (int) lua.CheckNumber(2);
                int layer = (int) lua.CheckInteger(3) - 1;
                string? tileName = null;

                if (!lua.IsNoneOrNil(4))
                    tileName = lua.CheckString(4);

                if (!RainEd.Instance.Level.IsInBounds(x, y)) return 0;
                if (layer < 0 || layer > 2) return 0;

                Assets.Tile? tile = null;

                if (tileName is not null)
                {
                    if (!RainEd.Instance.TileDatabase.HasTile(tileName))
                        throw new LuaHelpers.LuaErrorException($"tile '{tileName}' does not exist");
                    
                    tile = RainEd.Instance.TileDatabase.GetTileFromName(tileName);
                }

                RainEd.Instance.Level.SetTileHead(layer, x, y, tile);
                RainEd.Instance.LevelView.Renderer.InvalidateTileHead(x, y, layer);

                return 0;
            });
            lua.SetField(-2, "setTileHead");

            // function setTileRoot
            LuaHelpers.PushLuaFunction(lua, static (KeraLua.Lua lua) =>
            {
                int x = (int) lua.CheckNumber(1);
                int y = (int) lua.CheckNumber(2);
                int layer = (int) lua.CheckInteger(3) - 1;

                int tileRootX = (int) lua.CheckNumber(4);
                int tileRootY = (int) lua.CheckNumber(5);
                int tileLayer = (int) lua.CheckInteger(6) - 1;

                if (!RainEd.Instance.Level.IsInBounds(x, y)) return 0;
                if (layer < 0 || layer > 2) return 0;

                if (!RainEd.Instance.Level.IsInBounds(tileRootX, tileRootY) || tileLayer < 0 || tileLayer > 2)
                    throw new LuaHelpers.LuaErrorException("target tile root is out of bounds");
                
                // invalidate old tile head
                var cell = RainEd.Instance.Level.Layers[layer, x, y];
                if (cell.TileRootX != -1)
                {
                    RainEd.Instance.LevelView.Renderer.InvalidateTileHead(cell.TileRootX, cell.TileRootY, cell.TileLayer);    
                }

                RainEd.Instance.Level.SetTileRoot(layer, x, y, tileRootX, tileRootY, tileLayer);
                RainEd.Instance.LevelView.Renderer.InvalidateTileHead(tileRootX, tileRootY, tileLayer);

                return 0;
            });
            lua.SetField(-2, "setTileRoot");

            // function clearTileRoot
            LuaHelpers.PushLuaFunction(lua, static (KeraLua.Lua lua) =>
            {
                int x = (int) lua.CheckNumber(1);
                int y = (int) lua.CheckNumber(2);
                int layer = (int) lua.CheckInteger(3) - 1;

                if (!RainEd.Instance.Level.IsInBounds(x, y)) return 0;
                if (layer < 0 || layer > 2) return 0;

                RainEd.Instance.Level.ClearTileRoot(layer, x, y);
                RainEd.Instance.LevelView.Renderer.InvalidateTileHead(x, y, layer);
                
                return 0;
            });
            lua.SetField(-2, "clearTileRoot");

            lua.SetField(-2, "cells");
        }

        // TILES namespace
        {
            lua.NewTable();

            //luaState.Push(new Func<string, object?, Autotile>(CreateAutotile));
            LuaHelpers.PushLuaFunction(lua, LuaCreateAutotile);
            lua.SetField(-2, "createAutotile");

            LuaHelpers.PushCsFunction(lua, new PlaceTileDelegate(PlaceTile));
            lua.SetField(-2, "placeTile");

            // function getTileAt
            luaState.Push(static (int x, int y, int layer) => {
                var level = RainEd.Instance.Level;
                if (layer < 1 || layer > 3) return null;
                if (!level.IsInBounds(x, y)) return null;
                var tile = RainEd.Instance.Level.GetTile(level.Layers[layer-1, x, y]);
                return tile?.Name;
            });
            lua.SetField(-2, "getTileAt");

            // function hasTileHead
            luaState.Push(static (int x, int y, int layer) => {
                var level = RainEd.Instance.Level;
                if (layer < 1 || layer > 3) return false;
                if (!level.IsInBounds(x, y)) return false;
                return level.Layers[layer-1, x, y].TileHead is not null;
            });
            lua.SetField(-2, "hasTileHead");

            // function deleteTile
            LuaHelpers.PushLuaFunction(lua, static (KeraLua.Lua lua) => {
                int x = (int) lua.CheckInteger(1);
                int y = (int) lua.CheckInteger(2);
                int layer = (int) lua.CheckInteger(3);
                bool removeGeo = false;

                if (!lua.IsNoneOrNil(4))
                    removeGeo = lua.ToBoolean(4);
                
                var level = RainEd.Instance.Level;
                if (layer < 1 || layer > 3) return 0;
                if (!level.IsInBounds(x, y)) return 0;
                level.RemoveTileCell(layer - 1, x, y, removeGeo);
                return 0;
            });
            lua.SetField(-2, "deleteTile");

            // function autotilePath
            LuaHelpers.PushLuaFunction(lua, LuaStandardPathAutotile);
            lua.SetField(-2, "autotilePath");

            // function getTileInfo
            LuaHelpers.PushLuaFunction(lua, static (KeraLua.Lua lua) =>
            {
                var tileName = lua.CheckString(1);
                if (!RainEd.Instance.TileDatabase.HasTile(tileName)) return 0;
                var tileData = RainEd.Instance.TileDatabase.GetTileFromName(tileName);

                lua.NewTable();
                lua.PushString(tileData.Name); // name
                lua.SetField(-2, "name");
                lua.PushString(tileData.Category.Name); // category
                lua.SetField(-2, "category");
                lua.PushInteger(tileData.Width); // width
                lua.SetField(-2, "width");
                lua.PushInteger(tileData.Height); // height
                lua.SetField(-2, "height");
                lua.PushInteger(tileData.BfTiles); // bfTiles
                lua.SetField(-2, "bfTiles");
                lua.PushInteger(tileData.CenterX); // cx
                lua.SetField(-2, "centerX");
                lua.PushInteger(tileData.CenterY); // cy
                lua.SetField(-2, "centerY");

                // create specs table
                lua.CreateTable(tileData.Width * tileData.Height, 0);
                int i = 1;
                for (int x = 0; x < tileData.Width; x++)
                {
                    for (int y = 0; y < tileData.Height; y++)
                    {
                        lua.PushInteger(tileData.Requirements[x,y]);
                        lua.RawSetInteger(-2, i++);
                    }
                }
                lua.SetField(-2, "specs");

                // create specs2 table
                if (tileData.HasSecondLayer)
                {
                    lua.CreateTable(tileData.Width * tileData.Height, 0);
                    i = 1;
                    for (int x = 0; x < tileData.Width; x++)
                    {
                        for (int y = 0; y < tileData.Height; y++)
                        {
                            lua.PushInteger(tileData.Requirements2[x,y]);
                            lua.RawSetInteger(-2, i++);
                        }
                    }

                    lua.SetField(-2, "specs2");
                }

                return 1;
            });
            lua.SetField(-2, "getTileInfo");

            lua.SetField(-2, "tiles");
        }

        return 1;
    }

    private static void RunCommand(int id)
    {
        luaState.State.RawGetInteger(KeraLua.LuaRegistry.Index, registeredCmds[id]);

        RainEd.Instance.LevelView.CellChangeRecorder.BeginChange();
        
        try
        {
            var func = (LuaFunction) luaState.Pop();
            func.Call();
        }
        catch (LuaScriptException e)
        {
            HandleException(e);
        }

        RainEd.Instance.LevelView.CellChangeRecorder.PushChange();
    }

    private static int LuaCreateAutotile(KeraLua.Lua lua)
    {
        var name = lua.CheckString(1);
        var category = "Misc";

        // the optional second argument is the category name
        if (!lua.IsNoneOrNil(2))
        {
            category = lua.CheckString(2);
        }

        var autotile = new LuaAutotileInterface()
        {
            Name = name
        };

        RainEd.Instance.Autotiles.AddAutotile(autotile.autotile, category);
        luaState.Push(autotile);
        return 1;
    }

    private static int LuaStandardPathAutotile(KeraLua.Lua lua)
    {
        var state = lua;

        // arg 1: tile table
        state.CheckType(1, KeraLua.LuaType.Table);
        // arg 2: layer
        int layer = (int) state.CheckInteger(2) - 1;
        // arg 3: segment list
        state.CheckType(3, KeraLua.LuaType.Table);
        
        // arg 4: modifier string
        string modifierStr = "";
        if (!state.IsNoneOrNil(4))
            modifierStr = state.CheckString(4);

        int startIndex = 0;
        int endIndex = (int) state.Length(3);

        // arg 5: optional start index
        if (!state.IsNoneOrNil(5))
            startIndex = (int) state.CheckInteger(5) - 1;
        
        // arg 6: optional end index
        if (!state.IsNoneOrNil(6))
            endIndex = (int) state.CheckInteger(6);
        
        // verify layer argument
        if (layer < 0 || layer > 2) return 0;
        
        var tileTable = new PathTileTable();

        // parse tiling options
        if (state.GetField(1, "placeJunctions") != KeraLua.LuaType.Nil)
        {
            if (!state.IsBoolean(-1)) return state.Error("invalid tile table");
            tileTable.AllowJunctions = state.ToBoolean(-1);
        }

        if (state.GetField(1, "placeCaps") != KeraLua.LuaType.Nil)
        {
            if (!state.IsBoolean(-1)) return state.Error("invaild tile table");
            tileTable.PlaceCaps = state.ToBoolean(-1);
        }

        state.Pop(2);

        // parse the tile table
        if (state.GetField(1, "ld") != KeraLua.LuaType.String) return state.Error("invalid tile table");
        tileTable.LeftDown = state.ToString(-1);
        if (state.GetField(1, "lu") != KeraLua.LuaType.String) return state.Error("invalid tile table");
        tileTable.LeftUp = state.ToString(-1);
        if (state.GetField(1, "rd") != KeraLua.LuaType.String) return state.Error("invalid tile table");
        tileTable.RightDown = state.ToString(-1);
        if (state.GetField(1, "ru") != KeraLua.LuaType.String) return state.Error("invalid tile table");
        tileTable.RightUp = state.ToString(-1);
        if (state.GetField(1, "vertical") != KeraLua.LuaType.String) return state.Error("invalid tile table");
        tileTable.Vertical = state.ToString(-1);
        if (state.GetField(1, "horizontal") != KeraLua.LuaType.String) return state.Error("invalid tile table");
        tileTable.Horizontal = state.ToString(-1);

        state.Pop(6);

        if (tileTable.AllowJunctions)
        {
            if (state.GetField(1, "tr") != KeraLua.LuaType.String) return state.Error("invalid tile table");
            tileTable.TRight = state.ToString(-1);
            if (state.GetField(1, "tu") != KeraLua.LuaType.String) return state.Error("invalid tile table");
            tileTable.TUp = state.ToString(-1);
            if (state.GetField(1, "tl") != KeraLua.LuaType.String) return state.Error("invalid tile table");
            tileTable.TLeft = state.ToString(-1);
            if (state.GetField(1, "td") != KeraLua.LuaType.String) return state.Error("invalid tile table");
            tileTable.TDown = state.ToString(-1);
            if (state.GetField(1, "x") != KeraLua.LuaType.String) return state.Error("invalid tile table");
            tileTable.XJunct = state.ToString(-1);

            state.Pop(5);
        }

        if (tileTable.PlaceCaps)
        {
            if (state.GetField(1, "capRight") != KeraLua.LuaType.String) return state.Error("invalid tile table");
            tileTable.CapRight = state.ToString(-1);
            if (state.GetField(1, "capUp") != KeraLua.LuaType.String) return state.Error("invalid tile table");
            tileTable.CapUp = state.ToString(-1);
            if (state.GetField(1, "capLeft") != KeraLua.LuaType.String) return state.Error("invalid tile table");
            tileTable.CapLeft = state.ToString(-1);
            if (state.GetField(1, "capDown") != KeraLua.LuaType.String) return state.Error("invalid tile table");
            tileTable.CapDown = state.ToString(-1);

            state.Pop(4);
        }

        // parse path segment table
        var pathSegments = new List<PathSegment>();

        state.PushCopy(3); // push segment table onto stack
        
        // begin looping through table
        state.PushNil();
        while (state.Next(-2))
        {
            // the value is at the top of the stack
            if (!state.IsTable(-1)) return state.Error("invalid segment table");
            int tableIndex = state.GetTop();
            var segment = new PathSegment();

            if (state.GetField(tableIndex, "right") != KeraLua.LuaType.Boolean) return state.Error("invalid segment table");
            segment.Right = state.ToBoolean(-1);
            if (state.GetField(tableIndex, "up") != KeraLua.LuaType.Boolean) return state.Error("invalid segment table");
            segment.Up = state.ToBoolean(-1);
            if (state.GetField(tableIndex, "left") != KeraLua.LuaType.Boolean) return state.Error("invalid segment table");
            segment.Left = state.ToBoolean(-1);
            if (state.GetField(tableIndex, "down") != KeraLua.LuaType.Boolean) return state.Error("invalid segment table");
            segment.Down = state.ToBoolean(-1);
            if (state.GetField(tableIndex, "x") != KeraLua.LuaType.Number) return state.Error("invalid segment table");
            segment.X = (int) state.ToNumber(-1);
            if (state.GetField(tableIndex, "y") != KeraLua.LuaType.Number) return state.Error("invalid segment table");
            segment.Y = (int) state.ToNumber(-1);

            pathSegments.Add(segment);

            state.Pop(6); // pop retrieved values of table
            state.Pop(1); // pop value
        }

        // pop the segment table
        state.Pop(1);

        var modifier = modifierStr switch
        {
            "geometry" => TilePlacementMode.Geometry,
            "force" => TilePlacementMode.Force,
            _ => TilePlacementMode.Normal
        };

        // *inhale* then finally, run the autotiler
        Autotile.StandardTilePath(tileTable, layer, [..pathSegments], modifier, startIndex, endIndex);

        return 0;
    }

    private static void ShowNotification(object? msg)
    {
        if (msg is null) return;
        EditorWindow.ShowNotification(msg.ToString()!);
    }

    delegate bool PlaceTileDelegate(out string? result, string tileName, int x, int y, int layer, string? modifier);
    public static bool PlaceTile(out string? result, string tileName, int x, int y, int layer, string? modifier)
    {
        result = null;

        var level = RainEd.Instance.Level;
        var placeMode = TilePlacementMode.Normal;
        
        // validate arguments
        if (modifier is not null)
        {
            if (modifier != "geometry" && modifier != "force")
            {
                throw new Exception($"expected 'geometry' or 'force' for argument 5, got '{modifier}'");
            }

            if (modifier == "geometry")   placeMode = TilePlacementMode.Geometry;
            else if (modifier == "force") placeMode = TilePlacementMode.Force;
        }
        if (layer < 1 || layer > 3)
            throw new Exception($"invalid layer {layer}");
        if (!RainEd.Instance.TileDatabase.HasTile(tileName))
            throw new Exception($"tile '{tileName}' is not recognized");
        layer--; // layer is 1-based in the lua code
        
        // begin placement
        var tile = RainEd.Instance.TileDatabase.GetTileFromName(tileName);

        var validationStatus = level.SafePlaceTile(tile, layer, x, y, placeMode);
        switch (validationStatus)
        {
            case TilePlacementStatus.OutOfBounds:
                result = "out of bounds";
                return false;
            case TilePlacementStatus.Overlap:
                result = "overlap";
                return false;
            case TilePlacementStatus.Geometry:
                result = "geometry";
                return false;
            case TilePlacementStatus.Success:
                return true;
        }

        return true;
    }

    private static string GetVersion()
    {
        return RainEd.Version;
    }

    private static void AutoRequire(string path, bool? recurse)
    {
        List<string> combineParams = [Boot.AppDataPath, "scripts"];
        combineParams.AddRange(path.Split('.'));
        var filePath = Path.Combine([..combineParams]);

        if (!Directory.Exists(filePath))
            throw new Exception($"path '{path}' does not exist");

        var files = Directory.GetFiles(filePath).ToList();
        files.Sort();
        foreach (var fileName in files)
        {
            if (Path.GetExtension(fileName) != ".lua") continue;

            luaState.State.GetGlobal("require");
            luaState.State.PushString(path + "." + Path.GetFileNameWithoutExtension(fileName));
            luaState.State.Call(1, 0);
        }

        if (recurse.GetValueOrDefault())
        {
            var subDirs = Directory.GetDirectories(filePath).ToList();
            subDirs.Sort();
            foreach (var dirName in subDirs)
            {
                AutoRequire(path + "." + Path.GetFileName(dirName), true);
            }
        }
    }

    public static void LogInfo(string msg)
    {
        Log.UserLogger.Information("[lua] " + msg);
    }

    public static void LogWarning(string msg)
    {
        Log.UserLogger.Warning("[lua] " + msg);
    }

    public static void LogError(string msg)
    {
        Log.UserLogger.Error("[lua] " + msg);
    }

    private static void LuaPrint(params object[] args)
    {
        StringBuilder stringBuilder = new();

        foreach (var v in args)
        {
            var str = v is null ? "nil" : v.ToString()!;
            stringBuilder.Append(str);
            stringBuilder.Append(' ', 8 - str.Length % 8);
        }

        LogInfo(stringBuilder.ToString());
    }

    private static void LuaWarning(params object[] args)
    {
        StringBuilder stringBuilder = new();

        foreach (var v in args)
        {
            var str = v is null ? "nil" : v.ToString()!;
            stringBuilder.Append(str);
            stringBuilder.Append(' ', 8 - str.Length % 8);
        }

        LogWarning(stringBuilder.ToString());        
    }
}