using System.Numerics;
using ImGuiNET;
using Raylib_cs;

// i probably should create an IGUIWindow interface for the various miscellaneous windows...
namespace RainEd;
static class PreferencesWindow
{
    private const string WindowName = "Preferences";
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

    private readonly static string[] NavTabs = ["General", "Shortcuts", "Theme", "Assets", "Drizzle"];
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
    private static Vector3 tileSpec1Color;
    private static Vector3 tileSpec2Color;
    private static float contentScale;

    private static void ShowGeneralTab(bool entered)
    {
        static Vector3 HexColorToVec3(HexColor color) => new(color.R / 255f, color.G / 255f, color.B / 255f);
        static HexColor Vec3ToHexColor(Vector3 vec) => new(
            (byte)(Math.Clamp(vec.X, 0f, 1f) * 255f),
            (byte)(Math.Clamp(vec.Y, 0f, 1f) * 255f),
            (byte)(Math.Clamp(vec.Z, 0f, 1f) * 255f)
        );

        var prefs = RainEd.Instance.Preferences;
        
        ImGui.SeparatorText("Level Colors");
        {
            if (entered)
            {
                layerColor1 = HexColorToVec3(prefs.LayerColor1);
                layerColor2 = HexColorToVec3(prefs.LayerColor2);
                layerColor3 = HexColorToVec3(prefs.LayerColor3);
                bgColor = HexColorToVec3(prefs.BackgroundColor);
                tileSpec1Color = HexColorToVec3(prefs.TileSpec1);
                tileSpec2Color = HexColorToVec3(prefs.TileSpec2);
            }

            ImGui.ColorEdit3("##Layer Color 1", ref layerColor1);

            ImGui.SameLine();
            if (ImGui.Button("X##ResetLC1"))
            {
                layerColor1 = new HexColor("#000000").ToVector3();
            }
            ImGui.SetItemTooltip("Reset");
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Layer Color 1");

            ImGui.ColorEdit3("##Layer Color 2", ref layerColor2);

            ImGui.SameLine();
            if (ImGui.Button("X##ResetLC2"))
            {
                layerColor2 = new HexColor("#59ff59").ToVector3();
            }
            ImGui.SetItemTooltip("Reset");
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Layer Color 2");

            ImGui.ColorEdit3("##Layer Color 3", ref layerColor3);

            ImGui.SameLine();
            if (ImGui.Button("X##ResetLC3"))
            {
                layerColor3 = new HexColor("#ff1e1e").ToVector3();
            }
            ImGui.SetItemTooltip("Reset");
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Layer Color 3");

            ImGui.ColorEdit3("##Background Color", ref bgColor);

            ImGui.SameLine();
            if (ImGui.Button("X##ResetBGC"))
            {
                bgColor = new HexColor(127, 127, 127).ToVector3();
            }
            ImGui.SetItemTooltip("Reset");
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Background Color");

            // L1 TILE SPECS
            ImGui.ColorEdit3("##Tile Specs L1", ref tileSpec1Color);
            ImGui.SameLine();
            if (ImGui.Button("X##ResetTS1"))
            {
                tileSpec1Color = new HexColor("#99FF5B").ToVector3();
            }
            ImGui.SetItemTooltip("Reset");
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Tile Specs L1");

            // L2 TILE SPECS
            ImGui.ColorEdit3("##Tile Specs L2", ref tileSpec2Color);
            ImGui.SameLine();
            if (ImGui.Button("X##ResetTS2"))
            {
                tileSpec2Color = new HexColor("#61A338").ToVector3();
            }
            ImGui.SetItemTooltip("Reset");
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Tile Specs L2");

            // update layer colors in preferences class
            prefs.LayerColor1 = Vec3ToHexColor(layerColor1);
            prefs.LayerColor2 = Vec3ToHexColor(layerColor2);
            prefs.LayerColor3 = Vec3ToHexColor(layerColor3);
            prefs.BackgroundColor = Vec3ToHexColor(bgColor);
            prefs.TileSpec1 = Vec3ToHexColor(tileSpec1Color);
            prefs.TileSpec2 = Vec3ToHexColor(tileSpec2Color);
        }

        ImGui.SeparatorText("Display");
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
            ImGui.Text("Content Scale");

            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            ImGui.SetItemTooltip(
                """
                The default value for this is determined
                by your monitor's DPI.
                """
            );

            // Font Selection
            {
                ImGui.PushItemWidth(ImGui.GetFontSize() * 12f);

                var curFont = Fonts.GetCurrentFont();
                if (ImGui.BeginCombo("Font", curFont ?? ""))
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
            if (ImGui.Checkbox("Show camera numbers", ref showCameraNumbers))
                prefs.ShowCameraNumbers = showCameraNumbers;
            
            bool materialSelectorPreviews = prefs.MaterialSelectorPreview;
            if (ImGui.Checkbox("Show previews in the material selector", ref materialSelectorPreviews))
                prefs.MaterialSelectorPreview = materialSelectorPreviews;
            
            bool doubleClickToCreateProp = prefs.DoubleClickToCreateProp;
            if (ImGui.Checkbox("Double-click to create props", ref doubleClickToCreateProp))
                prefs.DoubleClickToCreateProp = doubleClickToCreateProp;
            
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.BeginItemTooltip())
            {
                ImGui.TextUnformatted(
                    """
                    Enabling this brings back the old prop
                    selection/creation controls, where double-
                    clicking the left mouse button placed down
                    a prop instead of a single right click.
                    """
                );
                ImGui.EndTooltip();
            }
            
            bool hideScreenSize = prefs.HideScreenSize;
            if (ImGui.Checkbox("Hide screen size parameters in the resize window", ref hideScreenSize))
                prefs.HideScreenSize = hideScreenSize;
            
            ImGui.PushItemWidth(ImGui.GetTextLineHeight() * 10f);
            
            // camera border view mode
            var camBorderMode = (int) prefs.CameraBorderMode;
            if (ImGui.Combo("Camera border view mode", ref camBorderMode, "Inner Border\0Outer Border\0Both Borders"))
                prefs.CameraBorderMode = (UserPreferences.CameraBorderModeOption) camBorderMode;
            
            // autotile mouse mode
            var autotileMouseMode = (int) prefs.AutotileMouseMode;
            if (ImGui.Combo("Autotile mouse mode", ref autotileMouseMode, "Click\0Hold"))
                prefs.AutotileMouseMode = (UserPreferences.AutotileMouseModeOptions) autotileMouseMode;
            
            // prop selection layer filter
            var propSelectionLayerFilter = (int) prefs.PropSelectionLayerFilter;
            if (ImGui.Combo("Prop selection layer filter", ref propSelectionLayerFilter, "All\0Current\0In Front"))
                prefs.PropSelectionLayerFilter = (UserPreferences.PropSelectionLayerFilterOption) propSelectionLayerFilter;
            
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.BeginItemTooltip())
            {
                ImGui.TextUnformatted(
                    """
                    This controls which layers you can select
                    in the prop editor relative to the current
                    view layer.
                    
                    - All: Will allow you to select props from
                    any layer.
                    - Current: Will only allow you to select
                    props in the currently viewed layer.
                    - In Front: Will only allow you to select
                    props in the current layer as well as all
                    layers behind it.
                    """
                );
                ImGui.End();
            }
            
            ImGui.PopItemWidth();
        }

        ImGui.SeparatorText("Miscellaneous");
        {
            // they've brainwashed me to not add this
            //bool showHiddenEffects = prefs.ShowDeprecatedEffects;
            //if (ImGui.Checkbox("Show deprecated effects", ref showHiddenEffects))
            //    prefs.ShowDeprecatedEffects = showHiddenEffects;

            bool versionCheck = prefs.CheckForUpdates;
            if (ImGui.Checkbox("Check for updates", ref versionCheck))
                prefs.CheckForUpdates = versionCheck;
            
            bool optimizedTile = prefs.OptimizedTilePreviews;
            if (ImGui.Checkbox("Optimized tile previews", ref optimizedTile))
                prefs.OptimizedTilePreviews = optimizedTile;
            
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            ImGui.SetItemTooltip(
                """
                This will optimize tile preview rendering such
                that only tile cells located in the bounds of
                its tile head will be rendered. If this option
                is turned off, all tile bodies will be
                processed regardless or not if it is within the
                bounds of its tile head.

                Turning this off may be useful if you have very
                erroneous tiles in a level and want to see them,
                but otherwise there is no reason to do so.
                """
            );

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
        ImGui.SeparatorText("Accessibility");
        ShortcutButton(KeyShortcut.RightMouse);
        
        ImGui.SeparatorText("General");
        ShortcutButton(KeyShortcut.ViewZoomIn);
        ShortcutButton(KeyShortcut.ViewZoomOut);
        ImGui.Separator();
        ShortcutButton(KeyShortcut.Undo);
        ShortcutButton(KeyShortcut.Redo);
        ShortcutButton(KeyShortcut.Cut);
        ShortcutButton(KeyShortcut.Copy);
        ShortcutButton(KeyShortcut.Paste);
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

        ImGui.SeparatorText("Editing");
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

        ImGui.SeparatorText("Geometry");
        ShortcutButton(KeyShortcut.ToggleLayer1);
        ShortcutButton(KeyShortcut.ToggleLayer2);
        ShortcutButton(KeyShortcut.ToggleLayer3);
        ShortcutButton(KeyShortcut.ToggleMirrorX);
        ShortcutButton(KeyShortcut.ToggleMirrorY);
        ShortcutButton(KeyShortcut.FloodFill);

        ImGui.SeparatorText("Tiles");
        ShortcutButton(KeyShortcut.SetMaterial);
        ImGui.Separator();
        ShortcutButton(KeyShortcut.TileForceGeometry);
        ShortcutButton(KeyShortcut.TileForcePlacement);
        ShortcutButton(KeyShortcut.TileIgnoreDifferent);

        ImGui.SeparatorText("Cameras");
        ShortcutButton(KeyShortcut.CameraSnapX);
        ShortcutButton(KeyShortcut.CameraSnapY);

        ImGui.SeparatorText("Light");
        ShortcutButton(KeyShortcut.ResetBrushTransform);
        ShortcutButton(KeyShortcut.ScaleLightBrush);
        ShortcutButton(KeyShortcut.RotateLightBrush);
        ImGui.Separator();
        ShortcutButton(KeyShortcut.ZoomLightIn);
        ShortcutButton(KeyShortcut.ZoomLightOut);
        ShortcutButton(KeyShortcut.RotateLightCW);
        ShortcutButton(KeyShortcut.RotateLightCCW);

        ImGui.SeparatorText("Props");
        ShortcutButton(KeyShortcut.ToggleVertexMode);
        ShortcutButton(KeyShortcut.RopeSimulation);
        ShortcutButton(KeyShortcut.ResetSimulation);
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
        if (ImGui.BeginCombo("Theme", RainEd.Instance.Preferences.Theme))
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

        if (ImGui.TreeNode("Theme Editor"))
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
        ImGui.SetItemTooltip("Reset");

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

        ImGui.SeparatorText("Options");

        bool boolRef;
        var prefs = RainEd.Instance.Preferences;

        // static lingo runtime
        {
            boolRef = prefs.StaticDrizzleLingoRuntime;
            if (ImGui.Checkbox("Initialize the Zygote runtime on app startup", ref boolRef))
                prefs.StaticDrizzleLingoRuntime = boolRef;
            
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            ImGui.SetItemTooltip(
                """
                This will run the Zygote runtime initialization
                process once, when the app starts. This results
                in a longer startup time and more idle RAM
                usage, but will decrease the time it takes to
                start a render.

                This option requires a restart in order to
                take effect.    
                """);
        }

        // show render preview
        {
            boolRef = prefs.ShowRenderPreview;
            if (ImGui.Checkbox("Show render preview", ref boolRef))
                prefs.ShowRenderPreview = boolRef;
        }
        
        ImGui.SeparatorText("Rendering");

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