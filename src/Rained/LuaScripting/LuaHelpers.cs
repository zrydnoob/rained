using System.Diagnostics;
using System.Reflection;
using KeraLua;
namespace Rained.LuaScripting;

/// <summary>
/// Class used to push C# functions to the Lua state.
/// 
/// NLua has a similar mechanism for this, however it is somewhat limited.
/// Optional arguments and multi-return functions are not properly supported.
/// Additionally, there is no try-catch for functions pushed to the stack with
/// lua_pushcfunction, so if a C# function called from Lua throws an error,
/// it won't be caught by the Lua error handler.
/// </summary>
static class LuaHelpers
{
    public delegate int LuaFunction(Lua lua);
    
    [Serializable]
    public class LuaErrorException : Exception
    {
        public LuaErrorException() { }
        public LuaErrorException(string message) : base(message) { }
        public LuaErrorException(string message, Exception inner) : base(message, inner) { }
    }
    
    private const string FuncWrapperMetatable = "luahelpers_delegate";
    private const string FuncMetatable = "luahelpers_function";
    private const string UserDataMetatable = "luahelpers_userdata";

    private static int nextID = 1;
    private static readonly Dictionary<int, object> allocatedObjects = [];
    
    private static readonly KeraLua.LuaFunction gcDelegate = new KeraLua.LuaFunction(GCDelegate);
    private static readonly KeraLua.LuaFunction callDelegate = new KeraLua.LuaFunction(CallDelegate);
    private static readonly KeraLua.LuaFunction mtDelegate = new KeraLua.LuaFunction(MetatableDelegate);

    private static readonly KeraLua.LuaFunction wrapperGcDelegate = new KeraLua.LuaFunction(WrapperGCDelegate);
    private static readonly KeraLua.LuaFunction wrapperCallDelegate = new KeraLua.LuaFunction(WrapperCallDelegate);

    private static readonly KeraLua.LuaFunction userdataGcDelegate = new KeraLua.LuaFunction(UserDataGCDelegate);

    public static void Init(Lua lua)
    {
        // create function metatable
        lua.NewMetaTable(FuncMetatable);
        lua.PushCFunction(gcDelegate);
        lua.SetField(-2, "__gc");

        lua.PushCFunction(mtDelegate);
        lua.SetField(-2, "__metatable");

        lua.Pop(1);

        // create wrapper metatable
        lua.NewMetaTable(FuncWrapperMetatable);
        lua.PushCFunction(wrapperGcDelegate);
        lua.SetField(-2, "__gc");

        lua.PushCFunction(mtDelegate);
        lua.SetField(-2, "__metatable");

        lua.Pop(1);

        // create userdata metatable
        lua.NewMetaTable(UserDataMetatable);
        lua.PushCFunction(gcDelegate);
        lua.SetField(-2, "__gc");

        lua.PushCFunction(mtDelegate);
        lua.SetField(-2, "__metatable");

        lua.Pop(1);
    }

    private static int MetatableDelegate(nint luaPtr)
    {
        Lua lua = Lua.FromIntPtr(luaPtr)!;
        lua.PushString("the metatable is locked");
        return 1;
    }

    private static unsafe int GCDelegate(nint luaPtr)
    {
        Lua lua = Lua.FromIntPtr(luaPtr)!;
        int id = *((int*)lua.CheckUserData(1, FuncMetatable));
        allocatedObjects.Remove(id);
        return 0;
    }

    private static unsafe int CallDelegate(nint luaPtr)
    {
        Lua lua = Lua.FromIntPtr(luaPtr)!;
        int id = *((int*)lua.CheckUserData(Lua.UpValueIndex(1), FuncMetatable));
        var func = (LuaFunction) allocatedObjects[id];

        try
        {
            return func(lua);
        }
        catch (LuaErrorException e)
        {
            return lua.ErrorWhere(e.Message);
        }
        catch (Exception e)
        {
            Log.UserLogger.Error("A C# exception occured in a Lua context!\n{Error}", e);
            return lua.Error("C# exception: " + e.Message);
        }
    }

    private static unsafe int WrapperGCDelegate(nint luaPtr)
    {
        Lua lua = Lua.FromIntPtr(luaPtr)!;
        int id = *((int*)lua.CheckUserData(1, FuncWrapperMetatable));

        allocatedObjects.Remove(id);
        return 0;
    }

    private static unsafe int WrapperCallDelegate(nint luaPtr)
    {
        Lua lua = Lua.FromIntPtr(luaPtr)!;
        var luaNumArgs = lua.GetTop();
        
        int* userData = (int*) lua.CheckUserData(Lua.UpValueIndex(1), FuncWrapperMetatable);
        int id = *userData;
        var func = (Delegate) allocatedObjects[id];

        var paramInfo = func.Method.GetParameters();
        var parameters = new object?[paramInfo.Length];
        int luaParamIndex = 1;
        for (int i = 0; i < paramInfo.Length; i++)
        {
            var param = paramInfo[i];
            if (param.IsOut) continue;

            var type = param.ParameterType;
            bool isNullable = Nullable.GetUnderlyingType(type) != null;

            if ((!type.IsValueType || isNullable) && (luaParamIndex > luaNumArgs || lua.IsNoneOrNil(luaParamIndex)))
            {
                parameters[i] = null;
            }
            else
            {
                if (isNullable) type = Nullable.GetUnderlyingType(type);
                
                if (type == typeof(int))
                {
                    parameters[i] = (int)lua.CheckInteger(luaParamIndex);
                }
                else if (type == typeof(long))
                {
                    parameters[i] = lua.CheckInteger(luaParamIndex);
                }
                else if (type == typeof(float))
                {
                    parameters[i] = (float) lua.CheckNumber(luaParamIndex);
                }
                else if (type == typeof(double))
                {
                    parameters[i] = lua.CheckNumber(luaParamIndex);
                }
                else if (type == typeof(string))
                {
                    parameters[i] = lua.CheckString(luaParamIndex);
                }
                else if (type == typeof(bool))
                {
                    parameters[i] = lua.ToBoolean(luaParamIndex);
                }
                else
                {
                    throw new Exception("Unsupported parameter type");
                }
            }

            luaParamIndex++;
        }

        int returnCount = 0;
        object? ret;
        try
        {
            ret = func.DynamicInvoke(parameters);
        }
        catch (TargetInvocationException e)
        {
            if (e.InnerException is not null)
                return lua.Error("C# exception: " + e.InnerException.Message);
            else
                throw;
        }

        if (ret is not null)
        {
            PushValue(lua, ret);
            returnCount++;
        }

        // process out arguments
        for (int i = 0; i < paramInfo.Length; i++)
        {
            var param = paramInfo[i];
            if (param.IsOut)
            {
                PushValue(lua, parameters[i]);
                returnCount++;
            }
        }

        return returnCount;
    }

    private static void PushValue(Lua lua, object? v)
    {
        if (v is null)
        {
            lua.PushNil();
        }
        else if (v is int v1)
        {
            lua.PushInteger(v1);
        }
        else if (v is long v2)
        {
            lua.PushInteger(v2);
        }
        else if (v is float v3)
        {
            lua.PushNumber(v3);
        }
        else if (v is double v4)
        {
            lua.PushNumber(v4);
        }
        else if (v is string v5)
        {
            lua.PushString(v5);
        }
        else if (v is bool v6)
        {
            lua.PushBoolean(v6);
        }
        else
        {
            throw new Exception("Unsupported value type");
        }
    }

    /// <summary>
    /// Push a C# delegate to the stack of a Lua state.
    /// This actually creates a wrapper to properly fill out the arguments and return values.
    /// </summary>
    /// <param name="lua"></param>
    /// <param name="func"></param>
    public static unsafe void PushCsFunction(Lua lua, Delegate func)
    {
        int* userData = (int*) lua.NewUserData(sizeof(int));
        *userData = nextID;
        
        lua.GetMetaTable(FuncWrapperMetatable);
        lua.SetMetaTable(-2);
        allocatedObjects[nextID++] = func;

        lua.PushCClosure(wrapperCallDelegate, 1);
    }

    /// <summary>
    /// Push a KeraLua.LuaFunction to the stack of a Lua state.
    /// When called from Lua, C# exceptions will be caught correctly.
    /// </summary>
    /// <param name="lua">The Lua state.</param>
    /// <param name="func">The `KeraLua.LuaFunction` to push.</param>
    public static unsafe void PushLuaFunction(Lua lua, LuaFunction func)
    {
        int* userData = (int*) lua.NewUserData(sizeof(int));
        *userData = nextID;

        lua.GetMetaTable(FuncMetatable);
        lua.SetMetaTable(-2);
        allocatedObjects[nextID++] = func;

        lua.PushCClosure(callDelegate, 1);
    }

    public static unsafe void PushClosureWithUserdata(Lua lua, object userdata, KeraLua.LuaFunction func)
    {
        int* userData = (int*) lua.NewUserData(sizeof(int));
        *userData = nextID;

        lua.GetMetaTable(UserDataMetatable);
        lua.SetMetaTable(-2);
        allocatedObjects[nextID++] = userdata;

        lua.PushCClosure(func, 1);
    }

    private static unsafe int UserDataGCDelegate(nint luaPtr)
    {
        Lua lua = Lua.FromIntPtr(luaPtr)!;
        int id = *((int*)lua.CheckUserData(1, UserDataMetatable));
        allocatedObjects.Remove(id);
        return 0;
    }

    public static unsafe object GetUserData(Lua lua)
    {
        int id = *((int*)lua.CheckUserData(Lua.UpValueIndex(1), UserDataMetatable));
        return allocatedObjects[id];
    }

    public static void ModuleFunction(this Lua lua, string funcName, KeraLua.LuaFunction func)
    {
        lua.PushCFunction(func);
        lua.SetField(-2, funcName);
    }

    public static void ModuleFunction(this Lua lua, string funcName, LuaHelpers.LuaFunction func)
    {
        LuaHelpers.PushLuaFunction(lua, func);
        lua.SetField(-2, funcName);
    }

    public static int ErrorWhere(this Lua lua, string msg, int level = 1)
    {
        lua.Where(level);
        lua.PushString(msg);
        lua.Concat(2);
        return lua.Error();
    }

    /// <summary>
    /// Error message handler to be used with lua_pcall.
    /// </summary>
    /// <param name="luaPtr"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static int ErrorHandler(nint luaPtr, int errObj)
    {
        var lua = Lua.FromIntPtr(luaPtr);
        if (errObj < 0) errObj = lua.GetTop() - (errObj + 1);

        // create exception from first argument (error object)
        var msg = lua.ToString(errObj);
        var exceptionMsg = msg;

        // if error string has stack/position info, begin traceback to where
        // it is located on the stack rather than actual location where the error
        // was thrown.
        int level = 0;
        string source = "";
        if (msg.Contains(':'))
        {
            for (int i = 0;; i++)
            {
                LuaDebug ar = new();
                if (lua.GetStack(i, ref ar) == 0) break;
                if (!lua.GetInfo("Sl", ref ar)) {
                    Log.Debug("error handler: could not get lua activation record");
                    break;
                }
                
                var name = ar.Source;
                name = (name[0] == '@' || name[0] == '=') ? name[1..] : name;
                if (name == "[C]") continue;

                var testStr = name + ":" + ar.CurrentLine + ": ";
                if (msg.Length >= testStr.Length && msg[..testStr.Length] == testStr)
                {
                    exceptionMsg = msg[testStr.Length..];
                    level = i;
                    source = name;
                    break;
                }
            }
        }

        var exception = new NLua.Exceptions.LuaScriptException(exceptionMsg, source);

        lua.Traceback(lua, level);
        exception.Data["Traceback"] = lua.ToString(-1);
        LuaInterface.HandleException(exception);
        lua.Pop(1); // remove traceback

        // return original error object?
        // lua.PushCopy(1);
        return 1;
    }

    public static LuaStatus ResumeCoroutine(Lua lua, Lua? from, int arguments, out int results)
    {
        try
        {
            var status = lua.Resume(from, arguments, out results);

            if (!(status is LuaStatus.OK or LuaStatus.Yield))
            {
                ErrorHandler(lua.Handle, -1);
            }

            return status;
        }
        catch (NoLevelException)
        {
            lua.Where(1);
            lua.PushString("a level is not loaded");
            lua.Concat(2);
            ErrorHandler(lua.Handle, -1);
            results = 0;
            return LuaStatus.ErrRun;
        }
    }

    public static LuaStatus Call(Lua lua, int arguments, int results)
    {
        lua.PushCFunction(static (nint luaPtr) =>
        {
            ErrorHandler(luaPtr, 1);
            return 1;
        });

        lua.Insert(-arguments - 2);
        try
        {
            LuaStatus stat = lua.PCall(arguments, results, -arguments - 2);
            if (stat == LuaStatus.ErrRun)
            {
                lua.Remove(-2); // pop the error handler, keeping the original error object
            }
            else
            {
                lua.Remove(-(results + 1)); // pop the error handler
            }
            return stat;
        }
        catch (NoLevelException)
        {
            lua.Where(1);
            lua.PushString("a level is not loaded");
            lua.Concat(2);
            ErrorHandler(lua.Handle, -1);
            return LuaStatus.ErrRun;
        }
    }

    public static LuaStatus DoFile(Lua lua, string path)
    {
        var stat = lua.LoadFile(path);
        if (stat != KeraLua.LuaStatus.OK)
        {
            LuaInterface.Host.Error(lua.ToString(-1));
            return stat;
        }
        else
        {
            return Call(lua, 0, 0);
        }
    }

    public static LuaStatus DoString(Lua lua, string str, string? name = null)
    {
        var stat = name is not null ? lua.LoadString(str, name) : lua.LoadString(str);
        if (stat != KeraLua.LuaStatus.OK)
        {
            LuaInterface.Host.Error(lua.ToString(-1));
            return stat;
        }
        else
        {
            return Call(lua, 0, 0);
        }
    }

    public static void LevelCheck(Lua lua)
    {
        if (LuaInterface.Host.ActiveDocument == -1)
        {
            lua.Where(1);
            lua.PushString("a level is not loaded");
            lua.Concat(2);
            lua.Error();
        }
    }
}