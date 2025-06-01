using ImGuiNET;
using Raylib_cs;
using System.Runtime.InteropServices;

namespace Rained;

enum KeyShortcut : int
{
    None = -1,

    RightMouse,

    // Edit modes
    EnvironmentEditor, GeometryEditor, TileEditor,
    CameraEditor, LightEditor, EffectsEditor, PropEditor,

    // General
    NavUp, NavLeft, NavDown, NavRight,
    NewObject, RemoveObject, SwitchLayer, SwitchTab, Duplicate,
    ViewZoomIn, ViewZoomOut,
    IncreaseBrushSize, DecreaseBrushSize,

    New, Open, Save, SaveAs, CloseFile, CloseAllFiles,
    Cut, Copy, Paste, Undo, Redo,
    Select,

    Render, ExportGeometry,

    SelectEditor,

    // Geometry
    ToggleLayer1, ToggleLayer2, ToggleLayer3,
    ToggleMirrorX, ToggleMirrorY,
    FloodFill,
    ToolWall, ToolShortcutEntrance, ToolShortcutDot,

    // Tile Editor
    Eyedropper, SetMaterial,
    TileForceGeometry, TileForcePlacement, TileIgnoreDifferent,

    // Light
    ResetBrushTransform,
    ZoomLightIn, ZoomLightOut,
    RotateLightCW, RotateLightCCW,
    ScaleLightBrush, RotateLightBrush,
    LightmapStretch,

    RotateBrushCW, RotateBrushCCW,
    PreviousBrush, NextBrush,

    // Camera
    CameraSnapX, CameraSnapY,

    // Props
    ToggleVertexMode, RopeSimulation, RopeSimulationFast, ResetSimulation,

    // View settings shortcuts
    ToggleViewGrid, ToggleViewTiles, ToggleViewProps,
    ToggleViewCameras, ToggleViewGraphics, ToggleViewNodeIndices,
    RotatePropCW, RotatePropCCW,
    ChangePropSnapping,

    /// <summary>
    /// Do not bind - this is just the number of shortcut IDs
    /// </summary>
    COUNT
}

static class KeyShortcuts
{
    public static readonly string CtrlName;
    public static readonly string ShiftName;
    public static readonly string AltName;
    public static readonly string SuperName;

    static KeyShortcuts()
    {
        ShiftName = "Shift";

        if (OperatingSystem.IsMacOS())
        {
            CtrlName = "Cmd";
            AltName = "Option";
            SuperName = "Ctrl";
        }
        else
        {
            CtrlName = "Ctrl";
            AltName = "Alt";

            if (OperatingSystem.IsWindows())
            {
                SuperName = "Win";
            }
            else
            {
                SuperName = "Super";
            }
        }
    }

    private class KeyShortcutBinding
    {
        public readonly KeyShortcut ID;
        public readonly string Name;
        public string ShortcutString = null!;
        public ImGuiKey Key;
        public ImGuiModFlags Mods;
        public ImGuiModFlags AllowedMods;
        public bool IsActivated = false;
        public bool IsDeactivated = false;
        public bool IsDown = false;
        public bool AllowRepeat = false;

        public readonly ImGuiKey OriginalKey;
        public readonly ImGuiModFlags OriginalMods;

        public KeyShortcutBinding(
            string name, KeyShortcut id, ImGuiKey key, ImGuiModFlags mods,
            bool allowRepeat = false,
            ImGuiModFlags allowedMods = ImGuiModFlags.None
        )
        {
            if (key == ImGuiKey.Backspace) key = ImGuiKey.Delete;

            Name = name;
            ID = id;
            Key = key;
            Mods = mods;
            AllowRepeat = allowRepeat;
            AllowedMods = allowedMods;

            OriginalKey = Key;
            OriginalMods = Mods;

            GenerateShortcutString();
        }

        public void GenerateShortcutString()
        {
            // build shortcut string
            var str = new List<string>();

            if (Mods.HasFlag(ImGuiModFlags.Ctrl))
                str.Add(CtrlName);

            if (Mods.HasFlag(ImGuiModFlags.Shift))
                str.Add(ShiftName);

            if (Mods.HasFlag(ImGuiModFlags.Alt))
                str.Add(AltName);

            if (Mods.HasFlag(ImGuiModFlags.Super))
                str.Add(SuperName);

            str.Add(ImGui.GetKeyName(Key));

            ShortcutString = string.Join('+', str);
        }

        public bool IsKeyPressed()
        {
            if (ID == KeyShortcut.RightMouse && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                return true;
            }

            if (Key == ImGuiKey.None) return false;

            bool kp;

            // i disable imgui from receiving tab inputs
            if (Key == ImGuiKey.Tab)
                kp = (bool)Raylib.IsKeyPressed(KeyboardKey.Tab);

            // delete/backspace will do the same thing
            else if (Key == ImGuiKey.Delete)
                kp = ImGui.IsKeyPressed(ImGuiKey.Delete, AllowRepeat) || ImGui.IsKeyPressed(ImGuiKey.Backspace, AllowRepeat);

            else
                kp = ImGui.IsKeyPressed(Key, AllowRepeat);

            return kp &&
            CheckModKey(ImGuiModFlags.Ctrl, ImGuiKey.ModCtrl) &&
            CheckModKey(ImGuiModFlags.Shift, ImGuiKey.ModShift) &&
            CheckModKey(ImGuiModFlags.Alt, ImGuiKey.ModAlt) &&
            CheckModKey(ImGuiModFlags.Super, ImGuiKey.ModSuper);
        }

        public bool IsKeyDown()
        {
            if (ID == KeyShortcut.RightMouse && ImGui.IsMouseDown(ImGuiMouseButton.Right))
            {
                return true;
            }

            if (Key == ImGuiKey.None) return false;

            bool kp;

            // i disable imgui from receiving tab inputs
            if (Key == ImGuiKey.Tab)
                kp = (bool)Raylib.IsKeyDown(KeyboardKey.Tab);

            // delete/backspace will do the same thing
            else if (Key == ImGuiKey.Delete)
                kp = ImGui.IsKeyDown(ImGuiKey.Delete) || ImGui.IsKeyDown(ImGuiKey.Backspace);

            else
                kp = ImGui.IsKeyDown(Key);

            return kp &&
            CheckModKey(ImGuiModFlags.Ctrl, ImGuiKey.ModCtrl) &&
            CheckModKey(ImGuiModFlags.Shift, ImGuiKey.ModShift) &&
            CheckModKey(ImGuiModFlags.Alt, ImGuiKey.ModAlt) &&
            CheckModKey(ImGuiModFlags.Super, ImGuiKey.ModSuper);
        }

        private bool CheckModKey(ImGuiModFlags mod, ImGuiKey key)
        {
            var down = ImGui.IsKeyDown(key);
            return (Mods.HasFlag(mod) == down) || (down && AllowedMods.HasFlag(mod));
        }
    }
    private static readonly Dictionary<KeyShortcut, KeyShortcutBinding> keyShortcuts = [];


    private static void Register(
        string name, KeyShortcut id, ImGuiKey key, ImGuiModFlags mods,
        bool allowRepeat = false,
        ImGuiModFlags allowedMods = ImGuiModFlags.None
    )
    {
        keyShortcuts.Add(id, new KeyShortcutBinding(name, id, key, mods, allowRepeat, allowedMods));
    }

    public static void Rebind(KeyShortcut id, ImGuiKey key, ImGuiModFlags mods)
    {
        if (key == ImGuiKey.Backspace) key = ImGuiKey.Delete;

        var data = keyShortcuts[id];
        data.Key = key;
        data.Mods = mods;
        data.GenerateShortcutString();
    }

    public static void Rebind(KeyShortcut id, string shortcut)
    {
        var keyStr = shortcut.Split('+');
        ImGuiModFlags mods = ImGuiModFlags.None;
        ImGuiKey tKey = ImGuiKey.None;

        for (int i = 0; i < keyStr.Length - 1; i++)
        {
            var modStr = keyStr[i];

            if (modStr == CtrlName)
                mods |= ImGuiModFlags.Ctrl;
            else if (modStr == AltName)
                mods |= ImGuiModFlags.Alt;
            else if (modStr == ShiftName)
                mods |= ImGuiModFlags.Shift;
            else if (modStr == SuperName)
                mods |= ImGuiModFlags.Super;
            else
                throw new Exception($"Unknown modifier key '{modStr}'");
        }

        if (keyStr[^1] == "None")
        {
            tKey = ImGuiKey.None;
        }
        else
        {
            for (int ki = (int)ImGuiKey.NamedKey_BEGIN; ki < (int)ImGuiKey.NamedKey_END; ki++)
            {
                ImGuiKey key = (ImGuiKey)ki;
                if (keyStr[^1] == ImGui.GetKeyName(key))
                {
                    tKey = key;
                    break;
                }
            }

            // throw an exception if the ImGuiKey was not found from the string
            if (tKey == ImGuiKey.None)
                throw new Exception($"Unknown key '{keyStr[^1]}'");
        }

        // assign to binding data
        KeyShortcutBinding data = keyShortcuts[id];
        data.Key = tKey;
        data.Mods = mods;
        data.GenerateShortcutString();
    }

    public static void Reset(KeyShortcut id)
    {
        var data = keyShortcuts[id];
        data.Key = data.OriginalKey;
        data.Mods = data.OriginalMods;
        data.GenerateShortcutString();
    }

    public static bool IsModifierKey(ImGuiKey key)
    {
        return key == ImGuiKey.LeftShift || key == ImGuiKey.RightShift
            || key == ImGuiKey.LeftCtrl || key == ImGuiKey.RightCtrl
            || key == ImGuiKey.LeftAlt || key == ImGuiKey.RightAlt
            || key == ImGuiKey.LeftSuper || key == ImGuiKey.RightSuper
            || key == ImGuiKey.ReservedForModAlt
            || key == ImGuiKey.ReservedForModCtrl
            || key == ImGuiKey.ReservedForModShift
            || key == ImGuiKey.ReservedForModSuper
            || key == ImGuiKey.ModAlt || key == ImGuiKey.ModShift || key == ImGuiKey.ModCtrl || key == ImGuiKey.ModSuper;
    }

    public static bool Activated(KeyShortcut id)
        => keyShortcuts[id].IsActivated;

    public static bool Active(KeyShortcut id)
        => keyShortcuts[id].IsDown;

    public static bool Deactivated(KeyShortcut id)
        => keyShortcuts[id].IsDeactivated;

    public static void ImGuiMenuItem(KeyShortcut id, string name, bool selected = false, bool enabled = true)
    {
        var shortcutData = keyShortcuts[id];
        var shortcutStr = shortcutData.Key == ImGuiKey.None ? null : shortcutData.ShortcutString;

        if (ImGui.MenuItem(name, shortcutStr, selected, enabled))
            shortcutData.IsActivated = true;
    }

    public static string GetShortcutString(KeyShortcut id)
        => keyShortcuts[id].ShortcutString;

    public static string GetName(KeyShortcut id)
        => keyShortcuts[id].Name;

    public static void Update()
    {
        // activate shortcuts on key press
        bool inputDisabled = ImGui.GetIO().WantTextInput;

        foreach (var shortcut in keyShortcuts.Values)
        {
            shortcut.IsActivated = false;

            if (shortcut.IsKeyPressed() && (!inputDisabled || shortcut.ID == KeyShortcut.RightMouse))
            {
                shortcut.IsActivated = true;
                shortcut.IsDown = true;
            }

            shortcut.IsDeactivated = false;
            if (shortcut.IsDown && !shortcut.IsKeyDown())
            {
                shortcut.IsDown = false;
                shortcut.IsDeactivated = true;
            }
        }
    }

    public static void InitShortcuts()
    {
        Register("鼠标右键替代按键", KeyShortcut.RightMouse, ImGuiKey.None, ImGuiModFlags.None,
            allowedMods: ImGuiModFlags.Ctrl | ImGuiModFlags.Shift | ImGuiModFlags.Alt | ImGuiModFlags.Super
        );

        Register("环境编辑器", KeyShortcut.EnvironmentEditor, ImGuiKey._1, ImGuiModFlags.None);
        Register("几何编辑器", KeyShortcut.GeometryEditor, ImGuiKey._2, ImGuiModFlags.None);
        Register("瓦片编辑器", KeyShortcut.TileEditor, ImGuiKey._3, ImGuiModFlags.None);
        Register("相机编辑器", KeyShortcut.CameraEditor, ImGuiKey._4, ImGuiModFlags.None);
        Register("灯光编辑器", KeyShortcut.LightEditor, ImGuiKey._5, ImGuiModFlags.None);
        Register("效果编辑器", KeyShortcut.EffectsEditor, ImGuiKey._6, ImGuiModFlags.None);
        Register("道具编辑器", KeyShortcut.PropEditor, ImGuiKey._7, ImGuiModFlags.None);

        Register("向上导航", KeyShortcut.NavUp, ImGuiKey.W, ImGuiModFlags.None, true);
        Register("向左导航", KeyShortcut.NavLeft, ImGuiKey.A, ImGuiModFlags.None, true);
        Register("向下导航", KeyShortcut.NavDown, ImGuiKey.S, ImGuiModFlags.None, true);
        Register("向右导航", KeyShortcut.NavRight, ImGuiKey.D, ImGuiModFlags.None, true);
        Register("放大视图", KeyShortcut.ViewZoomIn, ImGuiKey.Equal, ImGuiModFlags.None, true);
        Register("缩小视图", KeyShortcut.ViewZoomOut, ImGuiKey.Minus, ImGuiModFlags.None, true);

        Register("创建对象", KeyShortcut.NewObject, ImGuiKey.C, ImGuiModFlags.None, true);
        Register("移除", KeyShortcut.RemoveObject, ImGuiKey.X, ImGuiModFlags.None, true);
        Register("创建副本", KeyShortcut.Duplicate, ImGuiKey.D, ImGuiModFlags.Ctrl, true);

        Register("新建文件", KeyShortcut.New, ImGuiKey.N, ImGuiModFlags.Ctrl);
        Register("打开文件", KeyShortcut.Open, ImGuiKey.O, ImGuiModFlags.Ctrl);
        Register("保存文件", KeyShortcut.Save, ImGuiKey.S, ImGuiModFlags.Ctrl);
        Register("另存为", KeyShortcut.SaveAs, ImGuiKey.S, ImGuiModFlags.Ctrl | ImGuiModFlags.Shift);
        Register("关闭文件", KeyShortcut.CloseFile, ImGuiKey.W, ImGuiModFlags.Ctrl);
        Register("关闭所有文件", KeyShortcut.CloseAllFiles, ImGuiKey.W, ImGuiModFlags.Ctrl | ImGuiModFlags.Shift);

        Register("渲染", KeyShortcut.Render, ImGuiKey.R, ImGuiModFlags.Ctrl);
        Register("渲染几何", KeyShortcut.ExportGeometry, ImGuiKey.R, ImGuiModFlags.Ctrl | ImGuiModFlags.Shift);

        Register("剪切", KeyShortcut.Cut, ImGuiKey.X, ImGuiModFlags.Ctrl);
        Register("复制", KeyShortcut.Copy, ImGuiKey.C, ImGuiModFlags.Ctrl);
        Register("粘贴", KeyShortcut.Paste, ImGuiKey.V, ImGuiModFlags.Ctrl);
        Register("选择", KeyShortcut.Select, ImGuiKey.E, ImGuiModFlags.Ctrl);
        Register("撤销", KeyShortcut.Undo, ImGuiKey.Z, ImGuiModFlags.Ctrl, true);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Register("重做", KeyShortcut.Redo, ImGuiKey.Y, ImGuiModFlags.Ctrl, true);
        else
            Register("重做", KeyShortcut.Redo, ImGuiKey.Z, ImGuiModFlags.Ctrl | ImGuiModFlags.Shift, true);

        Register("编辑器选择", KeyShortcut.SelectEditor, ImGuiKey.GraveAccent, ImGuiModFlags.None);

        Register("循环调整层级", KeyShortcut.SwitchLayer, ImGuiKey.Tab, ImGuiModFlags.None);
        Register("Tab 切换", KeyShortcut.SwitchTab, ImGuiKey.Tab, ImGuiModFlags.Shift);
        Register("增加画笔大小", KeyShortcut.IncreaseBrushSize, ImGuiKey.O, ImGuiModFlags.None, true);
        Register("减小画笔大小", KeyShortcut.DecreaseBrushSize, ImGuiKey.I, ImGuiModFlags.None, true);

        // Geometry
        Register("切换层级 1", KeyShortcut.ToggleLayer1, ImGuiKey.E, ImGuiModFlags.None);
        Register("切换层级 2", KeyShortcut.ToggleLayer2, ImGuiKey.R, ImGuiModFlags.None);
        Register("切换层级 3", KeyShortcut.ToggleLayer3, ImGuiKey.T, ImGuiModFlags.None);
        Register("切换 X 对称", KeyShortcut.ToggleMirrorX, ImGuiKey.F, ImGuiModFlags.None, false);
        Register("切换 Y 对称", KeyShortcut.ToggleMirrorY, ImGuiKey.G, ImGuiModFlags.None, false);
        Register("洪水填充", KeyShortcut.FloodFill, ImGuiKey.Q, ImGuiModFlags.None, false);
        Register("墙壁绘制工具", KeyShortcut.ToolWall, ImGuiKey.Z, ImGuiModFlags.None, false);
        Register("管道入口绘制工具", KeyShortcut.ToolShortcutEntrance, ImGuiKey.X, ImGuiModFlags.None);
        Register("管道路径原点绘制工具", KeyShortcut.ToolShortcutDot, ImGuiKey.C, ImGuiModFlags.None);

        // Tile Editor
        Register("吸管工具", KeyShortcut.Eyedropper, ImGuiKey.Q, ImGuiModFlags.None, true);
        Register("将材料设为默认值", KeyShortcut.SetMaterial, ImGuiKey.E, ImGuiModFlags.None, true);
        Register("强制几何", KeyShortcut.TileForceGeometry, ImGuiKey.G, ImGuiModFlags.None,
            allowedMods: ImGuiModFlags.Shift
        );
        Register("强制放置", KeyShortcut.TileForcePlacement, ImGuiKey.F, ImGuiModFlags.None,
            allowedMods: ImGuiModFlags.Shift
        );
        Register("忽略材料", KeyShortcut.TileIgnoreDifferent, ImGuiKey.R, ImGuiModFlags.None,
            allowedMods: ImGuiModFlags.Shift
        );

        // Light Editor
        Register("重置笔刷变换", KeyShortcut.ResetBrushTransform, ImGuiKey.R, ImGuiModFlags.None);
        Register("向内移动光线", KeyShortcut.ZoomLightIn, ImGuiKey.W, ImGuiModFlags.Shift);
        Register("向外移动光线", KeyShortcut.ZoomLightOut, ImGuiKey.S, ImGuiModFlags.Shift);
        Register("顺时针旋转光线", KeyShortcut.RotateLightCW, ImGuiKey.D, ImGuiModFlags.Shift);
        Register("逆时针旋转光线", KeyShortcut.RotateLightCCW, ImGuiKey.A, ImGuiModFlags.Shift);
        Register("缩放笔刷", KeyShortcut.ScaleLightBrush, ImGuiKey.Q, ImGuiModFlags.None);
        Register("旋转笔刷", KeyShortcut.RotateLightBrush, ImGuiKey.E, ImGuiModFlags.None);

        Register("顺时针旋转笔刷", KeyShortcut.RotateBrushCW, ImGuiKey.E, ImGuiModFlags.None);
        Register("逆时针旋转笔刷", KeyShortcut.RotateBrushCCW, ImGuiKey.Q, ImGuiModFlags.None);
        Register("上一个笔刷", KeyShortcut.PreviousBrush, ImGuiKey.Z, ImGuiModFlags.None,
            allowRepeat: true
        );
        Register("下一个笔刷", KeyShortcut.NextBrush, ImGuiKey.X, ImGuiModFlags.None,
            allowRepeat: true
        );

        Register("Lightmap Warp", KeyShortcut.LightmapStretch, ImGuiKey.None, ImGuiModFlags.None);

        // Camera Editor
        Register("相机 X 轴捕捉", KeyShortcut.CameraSnapX, ImGuiKey.Q, ImGuiModFlags.None);
        Register("相机 Y 轴捕捉", KeyShortcut.CameraSnapY, ImGuiKey.E, ImGuiModFlags.None);

        // Prop Editor
        Register("切换到顶点模式", KeyShortcut.ToggleVertexMode, ImGuiKey.F, ImGuiModFlags.None);
        Register("模拟选定的绳索道具", KeyShortcut.RopeSimulation, ImGuiKey.Space, ImGuiModFlags.None);
        Register("快速模拟选定绳索", KeyShortcut.RopeSimulationFast, ImGuiKey.Space, ImGuiModFlags.Shift);
        Register("重置绳索模拟", KeyShortcut.ResetSimulation, ImGuiKey.None, ImGuiModFlags.None);

        Register("顺时针旋转道具", KeyShortcut.RotatePropCW, ImGuiKey.E, ImGuiModFlags.None);
        Register("逆时针旋转道具", KeyShortcut.RotatePropCCW, ImGuiKey.Q, ImGuiModFlags.None);

        Register("更改道具对齐", KeyShortcut.ChangePropSnapping, ImGuiKey.R, ImGuiModFlags.None);

        // View options
        Register("网格视图", KeyShortcut.ToggleViewGrid, ImGuiKey.G, ImGuiModFlags.Ctrl);
        Register("瓦片视图", KeyShortcut.ToggleViewTiles, ImGuiKey.T, ImGuiModFlags.Ctrl);
        Register("道具视图", KeyShortcut.ToggleViewProps, ImGuiKey.P, ImGuiModFlags.Ctrl);
        Register("相机边界视图", KeyShortcut.ToggleViewCameras, ImGuiKey.M, ImGuiModFlags.Ctrl);
        Register("平铺贴图视图", KeyShortcut.ToggleViewGraphics, ImGuiKey.T, ImGuiModFlags.Ctrl | ImGuiModFlags.Shift);
        Register("节点索引视图", KeyShortcut.ToggleViewNodeIndices, ImGuiKey.None, ImGuiModFlags.None);
    }
}