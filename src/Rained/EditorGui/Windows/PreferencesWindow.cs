using System.Numerics;
using ImGuiNET;
using Raylib_cs;

// i probably should create an IGUIWindow interface for the various miscellaneous windows...
namespace Rained.EditorGui;

static class PreferencesWindow
{
    private const string WindowName = "偏好";
    private static bool isWindowOpen = false;
    public static bool IsWindowOpen { get => isWindowOpen; }

    enum NavTabEnum : int
    {
        General = 0,
        Shortcuts = 1,
        Theme = 2,
        Assets = 3,
        Drizzle = 4
    }

    private readonly static string[] NavTabs = ["全局", "快捷键", "主题", "资产", "Drizzle"];
    private readonly static string[] RendererNames = ["Direct3D 11", "Direct3D 12", "OpenGL", "Vulkan"];
    private static NavTabEnum selectedNavTab = NavTabEnum.General;

    private static KeyShortcut activeShortcut = KeyShortcut.None;
    private static DrizzleConfiguration? activeDrizzleConfig = null;

    private static bool openPopupCmd = false;
    public static void OpenWindow()
    {
        openPopupCmd = true;
    }

    public static void ShowWindow()
    {
        bool justOpened = false;

        if (openPopupCmd)
        {
            justOpened = true;
            openPopupCmd = false;
            isWindowOpen = true;

            // center popup modal
            ImGuiExt.CenterNextWindow(ImGuiCond.Appearing);
            ImGui.SetNextWindowSize(new Vector2(ImGui.GetTextLineHeight() * 50f, ImGui.GetTextLineHeight() * 30f), ImGuiCond.FirstUseEver);
        }

        // keep track of this, as i want to clear some data
        // when the following tabs are no longer shown
        bool showAssetsTab = false;
        bool showRenderSettingsTab = false;

        if (isWindowOpen)
        {
            if (ImGui.Begin(WindowName, ref isWindowOpen, ImGuiWindowFlags.NoDocking))
            {
                var lastNavTab = selectedNavTab;

                // show navigation sidebar
                ImGui.BeginChild("Nav", new Vector2(ImGui.GetTextLineHeight() * 12.0f, ImGui.GetContentRegionAvail().Y), ImGuiChildFlags.Border);
                {
                    for (int i = 0; i < NavTabs.Length; i++)
                    {
                        if (ImGui.Selectable(NavTabs[i], i == (int)selectedNavTab))
                        {
                            selectedNavTab = (NavTabEnum)i;
                        }
                    }
                }
                ImGui.EndChild();

                ImGui.SameLine();
                ImGui.BeginChild("Controls", ImGui.GetContentRegionAvail());
                
                switch (selectedNavTab)
                {
                    case NavTabEnum.General:
                        ShowGeneralTab(justOpened || lastNavTab != selectedNavTab);
                        break;

                    case NavTabEnum.Shortcuts:
                        ShowShortcutsTab();
                        break;

                    case NavTabEnum.Theme:
                        ShowThemeTab(justOpened || lastNavTab != selectedNavTab);
                        break;
                    
                    case NavTabEnum.Assets:
                        AssetManagerGUI.Show();
                        showAssetsTab = true;
                        break;
                    
                    case NavTabEnum.Drizzle:
                        ShowDrizzleTab();
                        showRenderSettingsTab = true;
                        break;
                }

                ImGui.EndChild();
            }
            ImGui.End();
        }
        else
        {
            activeShortcut = KeyShortcut.None;
        }

        // handle shortcut binding
        if (activeShortcut != KeyShortcut.None)
        {
            // abort binding if not on the shortcut tabs, or on mouse input or if escape is pressed
            if (selectedNavTab != NavTabEnum.Shortcuts ||
                EditorWindow.IsKeyPressed(ImGuiKey.Escape) ||
                ImGui.IsMouseClicked(ImGuiMouseButton.Left) ||
                ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                activeShortcut = KeyShortcut.None;
            }
            else
            {
                // get mod flags
                ImGuiModFlags modFlags = ImGuiModFlags.None;

                if (ImGui.IsKeyDown(ImGuiKey.ModCtrl)) modFlags |= ImGuiModFlags.Ctrl;
                if (ImGui.IsKeyDown(ImGuiKey.ModAlt)) modFlags |= ImGuiModFlags.Alt;
                if (ImGui.IsKeyDown(ImGuiKey.ModShift)) modFlags |= ImGuiModFlags.Shift;
                if (ImGui.IsKeyDown(ImGuiKey.ModSuper)) modFlags |= ImGuiModFlags.Super;

                // find the key that is currently pressed
                if (Raylib.IsKeyPressed(KeyboardKey.Tab))
                {
                    KeyShortcuts.Rebind(activeShortcut, ImGuiKey.Tab, modFlags);
                    activeShortcut = KeyShortcut.None;
                }
                else
                {
                    for (int ki = (int)ImGuiKey.NamedKey_BEGIN; ki < (int)ImGuiKey.NamedKey_END; ki++)
                    {
                        ImGuiKey key = (ImGuiKey) ki;
                        
                        // don't process if this is a modifier key
                        if (KeyShortcuts.IsModifierKey(key))
                            continue;
                        
                        if (ImGui.IsKeyPressed(key))
                        {
                            // rebind the shortcut to this key
                            KeyShortcuts.Rebind(activeShortcut, key, modFlags);
                            activeShortcut = KeyShortcut.None;
                            break;
                        }
                    }
                }
            }
        }

        if (!showAssetsTab)
        {
            AssetManagerGUI.Unload();
        }

        if (!showRenderSettingsTab)
        {
            activeDrizzleConfig = null;
        }
    }

    private static Vector3 layerColor1;
    private static Vector3 layerColor2;
    private static Vector3 layerColor3;
    private static Vector3 bgColor;
    private static Vector4 tileSpec1Color;
    private static Vector4 tileSpec2Color;
    private static float contentScale;

    private static void ShowGeneralTab(bool entered)
    {
        static HexColor Vec3ToHexColor(Vector3 vec) => new(
            (byte)(Math.Clamp(vec.X, 0f, 1f) * 255f),
            (byte)(Math.Clamp(vec.Y, 0f, 1f) * 255f),
            (byte)(Math.Clamp(vec.Z, 0f, 1f) * 255f)
        );

        static HexColorRGBA Vec4ToHexColor(Vector4 vec) => new(
            (byte)(Math.Clamp(vec.X, 0f, 1f) * 255f),
            (byte)(Math.Clamp(vec.Y, 0f, 1f) * 255f),
            (byte)(Math.Clamp(vec.Z, 0f, 1f) * 255f),
            (byte)(Math.Clamp(vec.W, 0f, 1f) * 255f)
        );

        var prefs = RainEd.Instance.Preferences;
        
        ImGui.SeparatorText("关卡颜色");
        {
            if (entered)
            {
                layerColor1 = prefs.LayerColor1.ToVector3();
                layerColor2 = prefs.LayerColor2.ToVector3();
                layerColor3 = prefs.LayerColor3.ToVector3();
                bgColor = prefs.BackgroundColor.ToVector3();
                tileSpec1Color = prefs.TileSpec1.ToVector4();
                tileSpec2Color = prefs.TileSpec2.ToVector4();
            }

            ImGui.ColorEdit3("##Layer Color 1", ref layerColor1);

            ImGui.SameLine();
            if (ImGui.Button("X##ResetLC1"))
            {
                layerColor1 = new HexColor("#000000").ToVector3();
            }
            ImGui.SetItemTooltip("重设");
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("层级1颜色");

            ImGui.ColorEdit3("##Layer Color 2", ref layerColor2);

            ImGui.SameLine();
            if (ImGui.Button("X##ResetLC2"))
            {
                layerColor2 = new HexColor("#59ff59").ToVector3();
            }
            ImGui.SetItemTooltip("重设");
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("层级2颜色");

            ImGui.ColorEdit3("##Layer Color 3", ref layerColor3);

            ImGui.SameLine();
            if (ImGui.Button("X##ResetLC3"))
            {
                layerColor3 = new HexColor("#ff1e1e").ToVector3();
            }
            ImGui.SetItemTooltip("重设");
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("层级3颜色");

            ImGui.ColorEdit3("##Background Color", ref bgColor);

            ImGui.SameLine();
            if (ImGui.Button("X##ResetBGC"))
            {
                bgColor = new HexColor(127, 127, 127).ToVector3();
            }
            ImGui.SetItemTooltip("重设");
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("背景颜色");

            // L1 TILE SPECS
            ImGui.ColorEdit4("##Tile Specs L1", ref tileSpec1Color);
            ImGui.SameLine();
            if (ImGui.Button("X##ResetTS1"))
            {
                tileSpec1Color = new HexColorRGBA("#99FF5B").ToVector4();
            }
            ImGui.SetItemTooltip("重设");
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Tile Specs L1");

            // L2 TILE SPECS
            ImGui.ColorEdit4("##Tile Specs L2", ref tileSpec2Color);
            ImGui.SameLine();
            if (ImGui.Button("X##ResetTS2"))
            {
                tileSpec2Color = new HexColorRGBA("#61A338").ToVector4();
            }
            ImGui.SetItemTooltip("重设");
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Tile Specs L2");

            // update layer colors in preferences class
            prefs.LayerColor1 = Vec3ToHexColor(layerColor1);
            prefs.LayerColor2 = Vec3ToHexColor(layerColor2);
            prefs.LayerColor3 = Vec3ToHexColor(layerColor3);
            prefs.BackgroundColor = Vec3ToHexColor(bgColor);
            prefs.TileSpec1 = Vec4ToHexColor(tileSpec1Color);
            prefs.TileSpec2 = Vec4ToHexColor(tileSpec2Color);
        }

        ImGui.SeparatorText("显示");
        {
            if (entered)
            {
                contentScale = Boot.WindowScale;
            }

            ImGui.PushItemWidth(ImGui.GetTextLineHeight() * 10f);

            // Content Scale
            ImGui.DragFloat("##Content Scale", ref contentScale, 0.005f, 1.0f, 2.0f, "%.3f");
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                Boot.WindowScale = contentScale;
                prefs.ContentScale = contentScale;
            }
            ImGui.SameLine();
            if (ImGui.Button("X##Reset Content Scale"))
            {
                contentScale = Boot.Window.ContentScale.Y;
                Boot.WindowScale = contentScale;
                prefs.ContentScale = contentScale;
            }
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("内容缩放");

            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            ImGui.SetItemTooltip(
                """
                通过你的显示器的DPI为此确定了默认值
                """
            );

            // Font Selection
            {
                ImGui.PushItemWidth(ImGui.GetFontSize() * 12f);

                var curFont = Fonts.GetCurrentFont();
                if (ImGui.BeginCombo("字体", curFont ?? ""))
                {
                    foreach (var fontName in Fonts.AvailableFonts)
                    {
                        bool isSelected = fontName == curFont;
                        if (ImGui.Selectable(fontName, isSelected))
                        {
                            Fonts.SetFont(fontName);
                            prefs.Font = fontName;
                        }

                        if (isSelected)
                            ImGui.SetItemDefaultFocus();
                    }

                    ImGui.EndCombo();
                }

                var fontSize = prefs.FontSize;
                if (ImGui.InputInt("字体大小", ref fontSize))
                    prefs.FontSize = fontSize;
                
                if (ImGui.IsItemDeactivatedAfterEdit())
                    Fonts.FontReloadQueued = true;
                
                ImGui.SameLine();
                ImGui.TextDisabled("(?)");
                if (ImGui.BeginItemTooltip())
                {
                    ImGui.Text("此项默认值为13");
                    ImGui.EndTooltip();
                }

                ImGui.PopItemWidth();
            }

            // Vsync
            {
                bool vsync = Boot.Window.VSync;
                if (ImGui.Checkbox("Vsync", ref vsync))
                    Boot.Window.VSync = vsync;
                
                if (!vsync)
                {
                    ImGui.SameLine();

                    ImGui.SetNextItemWidth(ImGui.GetFontSize() * 8.0f);

                    var refreshRate = prefs.RefreshRate;
                    if (ImGui.SliderInt("Refresh rate", ref refreshRate, 30, 240))
                    {
                        prefs.RefreshRate = refreshRate;
                        Raylib.SetTargetFPS(prefs.RefreshRate);
                    }
                }
            }

            ImGui.PopItemWidth();
        }

        ImGui.SeparatorText("Interface");
        {
            bool showCameraNumbers = prefs.ShowCameraNumbers;
            if (ImGui.Checkbox("显示摄像机数量", ref showCameraNumbers))
                prefs.ShowCameraNumbers = showCameraNumbers;
            
            bool materialSelectorPreviews = prefs.MaterialSelectorPreview;
            if (ImGui.Checkbox("在材质选择器中显示预览", ref materialSelectorPreviews))
                prefs.MaterialSelectorPreview = materialSelectorPreviews;
            
            bool doubleClickToCreateProp = prefs.DoubleClickToCreateProp;
            if (ImGui.Checkbox("双击创建道具", ref doubleClickToCreateProp))
                prefs.DoubleClickToCreateProp = doubleClickToCreateProp;
            
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.BeginItemTooltip())
            {
                ImGui.TextUnformatted(
                    """
                    启用此选项会恢复旧的道具选择/创建控件，双击鼠标左键放置道具，而不是单击鼠标右键。
                    """
                );
                ImGui.EndTooltip();
            }
            
            bool hideScreenSize = prefs.HideScreenSize;
            if (ImGui.Checkbox("在调整大小窗口中隐藏屏幕大小参数", ref hideScreenSize))
                prefs.HideScreenSize = hideScreenSize;
            
            bool removeCangleLimit = prefs.RemoveCameraAngleLimit;
            if (ImGui.Checkbox("解锁相机角度限制", ref removeCangleLimit))
                prefs.RemoveCameraAngleLimit = removeCangleLimit;

            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.BeginItemTooltip())
            {
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20.0f);
                ImGui.TextWrapped("通常情况下，除非按住SHIFT键，否则相机角度的强度是有限的。启用此选项后，无需按住SHIFT键即可移除限制，这样做反而会施加限制。");
                ImGui.PopTextWrapPos();

                ImGui.End();
            }
            
            bool geoMaskMouseDecor = prefs.GeometryMaskMouseDecor;
            if (ImGui.Checkbox("在鼠标处显示几何层级指示器", ref geoMaskMouseDecor))
                prefs.GeometryMaskMouseDecor = geoMaskMouseDecor;
            
            bool minUi = prefs.MinimalStatusBar;
            if (ImGui.Checkbox("最小化状态栏", ref minUi))
                prefs.MinimalStatusBar = minUi;
            
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.BeginItemTooltip())
            {
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20.0f);
                ImGui.TextWrapped("这将隐藏状态栏中的某些元素。");
                ImGui.PopTextWrapPos();

                ImGui.End();
            }

            bool hideEditSwitch = prefs.HideEditorSwitch;
            if (ImGui.Checkbox("隐藏编辑器切换菜单", ref hideEditSwitch))
                prefs.HideEditorSwitch = hideEditSwitch;
            
            ImGui.Separator();
            
            ImGui.PushItemWidth(ImGui.GetTextLineHeight() * 10f);
            
            // camera border view mode
            var camBorderMode = (int) prefs.CameraBorderMode;
            if (ImGui.Combo("相机边界渲染", ref camBorderMode, "仅渲染里边界\0仅渲染外边界\0渲染所有边界"))
                prefs.CameraBorderMode = (UserPreferences.CameraBorderModeOption) camBorderMode;
            
            // autotile mouse mode
            var autotileMouseMode = (int) prefs.AutotileMouseMode;
            if (ImGui.Combo("自动图块放置模式", ref autotileMouseMode, "点击起点与终点\0按下拖动"))
                prefs.AutotileMouseMode = (UserPreferences.AutotileMouseModeOptions) autotileMouseMode;
            
            // tile placement mode toggle
            var tilePlacementToggle = prefs.TilePlacementModeToggle ? 1 : 0;
            if (ImGui.Combo("强制放置快捷键模式", ref tilePlacementToggle, "按住\0切换"))
                prefs.TilePlacementModeToggle = tilePlacementToggle != 0;
            
            // prop selection layer filter
            var propSelectionLayerFilter = (int) prefs.PropSelectionLayerFilter;
            if (ImGui.Combo("道具选择层过滤器", ref propSelectionLayerFilter, "全部层\0当前层\0当前层以后"))
                prefs.PropSelectionLayerFilter = (UserPreferences.PropSelectionLayerFilterOption) propSelectionLayerFilter;
            
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.BeginItemTooltip())
            {
                ImGui.TextUnformatted(
                    """
                    这控制了相对于当前视图层，可以在道具编辑器中选择哪些层。
                    
                    - 全部层：这将允许你从任何层选择道具。 
                    - 当前层：只允许你在当前查看的层中选择道具。
                    - 当前层以后:将只允许您选择当前层以及它后面的所有层的道具。
                    """
                );
                ImGui.End();
            }

            // light editor control scheme
            var lightEditorControlScheme = (int) prefs.LightEditorControlScheme;
            if (ImGui.Combo("灯光编辑器控制方案e", ref lightEditorControlScheme, "鼠标\0键盘\0"))
                prefs.LightEditorControlScheme = (UserPreferences.LightEditorControlSchemeOption) lightEditorControlScheme;

            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.BeginItemTooltip())
            {
                ImGui.TextUnformatted(
                    """
                    这将改变灯光编辑器中笔刷的缩放和旋转方式。
                    - 鼠标:按住q/e，移动鼠标分别进行缩放和旋转。
                    -键盘:模仿官方关卡编辑器中的控件:wasd缩放，q/e旋转。
                    """
                );
                ImGui.End();
            }
            
            ImGui.PopItemWidth();
        }

        ImGui.SeparatorText("杂项");
        {
            // they've brainwashed me to not add this
            //bool showHiddenEffects = prefs.ShowDeprecatedEffects;
            //if (ImGui.Checkbox("Show deprecated effects", ref showHiddenEffects))
            //    prefs.ShowDeprecatedEffects = showHiddenEffects;

            bool versionCheck = prefs.CheckForUpdates;
            if (ImGui.Checkbox("检查更新", ref versionCheck))
                prefs.CheckForUpdates = versionCheck;
            
            bool optimizedTile = prefs.OptimizedTilePreviews;
            if (ImGui.Checkbox("优化瓦片预览", ref optimizedTile))
                prefs.OptimizedTilePreviews = optimizedTile;
            
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            ImGui.SetItemTooltip(
                """
                this will optimize tile preview rendering such
                that only tile cells located in the bounds of
                its tile head will be rendered. if this option
                is turned off, all tile bodies will be
                processed regardless or not if it is within the
                bounds of its tile head.

                turning this off may be useful if you have very
                erroneous tiles in a level and want to see them,
                but otherwise there is no reason to do so.
                """
            );

            ImGui.Separator();

            ImGui.PushItemWidth(ImGui.GetTextLineHeight() * 10f);

            var simSpeed = prefs.FastSimulationSpeed;
            if (ImGui.SliderFloat("Fast simulation speed", ref simSpeed, 1f, 20f, "%.0fx"))
            {
                prefs.FastSimulationSpeed = simSpeed;
            }

            ImGui.PopItemWidth();

            //bool multiViewport = prefs.ImGuiMultiViewport;
            //if (ImGui.Checkbox("(EXPERIMENTAL) Multi-windowing", ref multiViewport))
            //    prefs.ImGuiMultiViewport = multiViewport;
            //ImGui.SameLine();
            //ImGui.TextDisabled("(?)");
            //ImGui.SetItemTooltip(
            //    """
            //    Turning this on will allow inner windows to
            //    go outside of the bounds of the main window.
            //    This option requires a restart in order to
            //    take effect.
            //    """
            //);
        }
    }

    private static void ShowShortcutsTab()
    {
        ImGui.SeparatorText("易用性");
        ShortcutButton(KeyShortcut.RightMouse);
        
        ImGui.SeparatorText("全局");
        ShortcutButton(KeyShortcut.ViewZoomIn);
        ShortcutButton(KeyShortcut.ViewZoomOut);
        ImGui.Separator();
        ShortcutButton(KeyShortcut.Undo);
        ShortcutButton(KeyShortcut.Redo);
        ShortcutButton(KeyShortcut.Cut);
        ShortcutButton(KeyShortcut.Copy);
        ShortcutButton(KeyShortcut.Paste);
        ShortcutButton(KeyShortcut.Select);
        ImGui.Separator();
        ShortcutButton(KeyShortcut.New);
        ShortcutButton(KeyShortcut.Open);
        ShortcutButton(KeyShortcut.Save);
        ShortcutButton(KeyShortcut.SaveAs);
        ShortcutButton(KeyShortcut.CloseFile);
        ShortcutButton(KeyShortcut.CloseAllFiles);
        ImGui.Separator();
        ShortcutButton(KeyShortcut.Render);
        ShortcutButton(KeyShortcut.ExportGeometry);

        ImGui.SeparatorText("编辑");
        ShortcutButton(KeyShortcut.SelectEditor);
        ShortcutButton(KeyShortcut.EnvironmentEditor);
        ShortcutButton(KeyShortcut.GeometryEditor);
        ShortcutButton(KeyShortcut.TileEditor);
        ShortcutButton(KeyShortcut.CameraEditor);
        ShortcutButton(KeyShortcut.LightEditor);
        ShortcutButton(KeyShortcut.EffectsEditor);
        ShortcutButton(KeyShortcut.PropEditor);
        ImGui.Separator();
        ShortcutButton(KeyShortcut.NavUp);
        ShortcutButton(KeyShortcut.NavDown);
        ShortcutButton(KeyShortcut.NavLeft);
        ShortcutButton(KeyShortcut.NavRight);
        ImGui.Separator();
        ShortcutButton(KeyShortcut.NewObject);
        ShortcutButton(KeyShortcut.RemoveObject);
        ShortcutButton(KeyShortcut.Duplicate);
        ShortcutButton(KeyShortcut.Eyedropper);
        ImGui.Separator();
        ShortcutButton(KeyShortcut.SwitchLayer);
        ShortcutButton(KeyShortcut.SwitchTab);
        ImGui.Separator();
        ShortcutButton(KeyShortcut.IncreaseBrushSize);
        ShortcutButton(KeyShortcut.DecreaseBrushSize);
        ImGui.Separator();
        ShortcutButton(KeyShortcut.ToggleViewGrid);
        ShortcutButton(KeyShortcut.ToggleViewTiles);
        ShortcutButton(KeyShortcut.ToggleViewGraphics);
        ShortcutButton(KeyShortcut.ToggleViewProps);
        ShortcutButton(KeyShortcut.ToggleViewCameras);
        ShortcutButton(KeyShortcut.ToggleViewNodeIndices);

        ImGui.SeparatorText("几何");
        ShortcutButton(KeyShortcut.ToggleLayer1);
        ShortcutButton(KeyShortcut.ToggleLayer2);
        ShortcutButton(KeyShortcut.ToggleLayer3);
        ShortcutButton(KeyShortcut.ToggleMirrorX);
        ShortcutButton(KeyShortcut.ToggleMirrorY);
        ShortcutButton(KeyShortcut.FloodFill);
        ImGui.Separator();
        ShortcutButton(KeyShortcut.ToolWall);
        ShortcutButton(KeyShortcut.ToolShortcutEntrance);
        ShortcutButton(KeyShortcut.ToolShortcutDot);

        ImGui.SeparatorText("瓦片");
        ShortcutButton(KeyShortcut.SetMaterial);
        ImGui.Separator();
        ShortcutButton(KeyShortcut.TileForceGeometry);
        ShortcutButton(KeyShortcut.TileForcePlacement);
        ShortcutButton(KeyShortcut.TileIgnoreDifferent);

        ImGui.SeparatorText("相机");
        ShortcutButton(KeyShortcut.CameraSnapX);
        ShortcutButton(KeyShortcut.CameraSnapY);

        ImGui.SeparatorText("灯光");
        ShortcutButton(KeyShortcut.ResetBrushTransform);
        ShortcutButton(KeyShortcut.ScaleLightBrush);
        ShortcutButton(KeyShortcut.RotateLightBrush);
        ImGui.Separator();
        ShortcutButton(KeyShortcut.ZoomLightIn);
        ShortcutButton(KeyShortcut.ZoomLightOut);
        ShortcutButton(KeyShortcut.RotateLightCW);
        ShortcutButton(KeyShortcut.RotateLightCCW);
        ImGui.Separator();
        ShortcutButton(KeyShortcut.RotateBrushCW);
        ShortcutButton(KeyShortcut.RotateBrushCCW);
        ShortcutButton(KeyShortcut.PreviousBrush);
        ShortcutButton(KeyShortcut.NextBrush);

        ImGui.SeparatorText("道具");
        ShortcutButton(KeyShortcut.ToggleVertexMode);
        ShortcutButton(KeyShortcut.RopeSimulation);
        ShortcutButton(KeyShortcut.ResetSimulation);
        ShortcutButton(KeyShortcut.RotatePropCCW);
        ShortcutButton(KeyShortcut.RotatePropCW);
    }

    private static readonly List<string> availableThemes = [];
    private static bool initTheme = true;

    private static void ReloadThemeList()
    {
        availableThemes.Clear();
        foreach (var fileName in Directory.EnumerateFiles(Path.Combine(Boot.AppDataPath, "config", "themes")))
        {
            var ext = Path.GetExtension(fileName);
            if (ext != ".json" && ext != ".jsonc") continue;
            availableThemes.Add(Path.GetFileNameWithoutExtension(fileName));    
        }
        availableThemes.Sort();
    }

    private static void ShowThemeTab(bool entered)
    {
        if (initTheme)
        {
            initTheme = false;
            ThemeEditor.ThemeSaved += ReloadThemeList;
        }

        // compile available themes when the tab is clicked
        if (entered)
        {
            ReloadThemeList();        
        }

        ImGui.SetNextItemWidth(ImGui.GetTextLineHeight() * 12.0f);
        if (ImGui.BeginCombo("主题", RainEd.Instance.Preferences.Theme))
        {
            foreach (var themeName in availableThemes)
            {
                if (ImGui.Selectable(themeName, themeName == RainEd.Instance.Preferences.Theme))
                {
                    RainEd.Instance.Preferences.Theme = themeName;
                    RainEd.Instance.Preferences.ApplyTheme();
                    ThemeEditor.SaveRef();
                }
            }

            ImGui.EndCombo();
        }

        if (ImGui.TreeNode("主题编辑器"))
        {
            ThemeEditor.Show();
            ImGui.TreePop();
        }
    }

    private static void ShortcutButton(KeyShortcut id, string? nameOverride = null)
    {
        ImGui.PushID((int) id);

        var btnSize = new Vector2(ImGui.GetTextLineHeight() * 8f, 0f);
        if (ImGui.Button(activeShortcut == id ? "..." : KeyShortcuts.GetShortcutString(id), btnSize))
        {
            activeShortcut = id;
        }

        ImGui.SetItemTooltip(KeyShortcuts.GetShortcutString(id));
        
        // reset button
        ImGui.SameLine();
        if (ImGui.Button("X"))
        {
            KeyShortcuts.Reset(id);
        }
        ImGui.SetItemTooltip("重置");

        ImGui.SameLine();
        ImGui.Text(nameOverride ?? KeyShortcuts.GetName(id));

        ImGui.PopID();
    }

    private static void ShowDrizzleTab()
    {
        activeDrizzleConfig ??= DrizzleConfiguration.LoadConfiguration(Path.Combine(RainEd.Instance.AssetDataPath, "editorConfig.txt"));

        static void ConfigCheckbox(string key)
        {
            bool v = activeDrizzleConfig!.GetConfig(key);
            if (ImGui.Checkbox(key, ref v))
            {
                activeDrizzleConfig.TrySetConfig(key, v);
                activeDrizzleConfig.SavePreferences();
            }
        }

        ImGui.SeparatorText("选项");

        bool boolRef;
        var prefs = RainEd.Instance.Preferences;

        // static lingo runtime
        {
            boolRef = prefs.StaticDrizzleLingoRuntime;
            if (ImGui.Checkbox("应用程序启动时初始化 Drizzle", ref boolRef))
                prefs.StaticDrizzleLingoRuntime = boolRef;
            
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            ImGui.SetItemTooltip(
                """
                这将在应用程序启动时运行一次 Drizzle 运行时初始化过程。这会导致更长的启动时间和更多的空闲RAM使用，但会减少启动渲染所需的时间。

                此选项需要重新启动才能生效。
                """);
        }

        // show render preview
        {
            boolRef = prefs.ShowRenderPreview;
            if (ImGui.Checkbox("显示渲染预览", ref boolRef))
                prefs.ShowRenderPreview = boolRef;
        }
        
        ImGui.SeparatorText("渲染");

        ConfigCheckbox("Grime on gradients");
        ConfigCheckbox("Grime");
        ConfigCheckbox("Material fixes");
        ConfigCheckbox("Slime always affects editor decals");
        ConfigCheckbox("voxelStructRandomDisplace for tiles as props");

        // notice tooltip for voxelStructRandomDisplace for tiles as props
        ImGui.SameLine();
        ImGui.TextDisabled("(!)");
        if (ImGui.BeginItemTooltip())
        {
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20.0f);
            ImGui.TextWrapped("After changing this option, a restart is advised in order to update the props list.");
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }

        ConfigCheckbox("notTrashProp fix");
        ConfigCheckbox("Trash and Small pipes non solid");
        ConfigCheckbox("Gradients with BackgroundScenes fix");
        ConfigCheckbox("Invisible material fix");
        ConfigCheckbox("Tiles as props fixes");
        ConfigCheckbox("Large trash debug log");
        ConfigCheckbox("Rough Rock spreads more");
        ConfigCheckbox("Dark Slime fix");
    }
}