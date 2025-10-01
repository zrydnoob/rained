using System.Numerics;
using ImGuiNET;
using Rained.Assets;
using Rained.Drizzle;
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
        General,
        Interface,
        Shortcuts,
        Theme,
        Drizzle,
        Scripts
    }

    private readonly static string[] NavTabs = ["全局", "Interface", "快捷键", "主题", "Drizzle", "脚本"];
    private readonly static string[] RendererNames = ["Direct3D 11", "Direct3D 12", "OpenGL", "Vulkan"];
    private static NavTabEnum selectedNavTab = NavTabEnum.General;

    private static KeyShortcut activeShortcut = KeyShortcut.None;
    private static DrizzleConfiguration? activeDrizzleConfig = null;
    private static FileSystemWatcher? drizzleConfigWatcher = null;
    private static string[]? missingDirs;

    private static bool openPopupCmd = false;
    public static void OpenWindow()
    {
        openPopupCmd = true;
    }

    private static void SetUpDrizzleConfig()
    {
        activeDrizzleConfig ??= DrizzleConfiguration.LoadConfiguration(Path.Combine(RainEd.Instance.AssetDataPath, "editorConfig.txt"));

        drizzleConfigWatcher?.Dispose();
        drizzleConfigWatcher = new FileSystemWatcher(Path.GetDirectoryName(activeDrizzleConfig.FilePath)!, "*.txt");
        drizzleConfigWatcher.NotifyFilter |= NotifyFilters.LastWrite | NotifyFilters.FileName;

        drizzleConfigWatcher.Changed += (object sender, FileSystemEventArgs e) =>
        {
            if (e.Name != "editorConfig.txt") return;

            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Deleted:
                    Log.Debug("Drizzle config deleted");

                    activeDrizzleConfig = null;
                    drizzleConfigWatcher.Dispose();
                    SetUpDrizzleConfig();
                    break;

                case WatcherChangeTypes.Changed:
                    Log.Debug("Drizzle config changed");

                    if (File.Exists(activeDrizzleConfig.FilePath))
                        activeDrizzleConfig.Reload();
                    break;

                case WatcherChangeTypes.Renamed:
                    Log.Debug("Drizzle config renamed/moved");

                    if (!Util.ArePathsEquivalent(e.FullPath, activeDrizzleConfig.FilePath))
                    {
                        activeDrizzleConfig = null;
                        drizzleConfigWatcher.Dispose();
                        SetUpDrizzleConfig();
                    }
                    break;
            }
        };

        drizzleConfigWatcher.Renamed += (object sender, RenamedEventArgs e) =>
        {
            if (
                e.OldFullPath.Equals(activeDrizzleConfig.FilePath, StringComparison.InvariantCultureIgnoreCase) &&
                !e.FullPath.Equals(activeDrizzleConfig.FilePath, StringComparison.InvariantCultureIgnoreCase)
            )
            {
                Log.Debug("Drizzle config renamed/moved");

                activeDrizzleConfig = null;
                drizzleConfigWatcher.Dispose();
                SetUpDrizzleConfig();
            }
        };

        drizzleConfigWatcher.EnableRaisingEvents = true;
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

        if (justOpened)
        {
            SetUpDrizzleConfig();
        }

        if (isWindowOpen)
        {
            var lastNavTab = selectedNavTab;

            if (ImGui.Begin(WindowName, ref isWindowOpen, ImGuiWindowFlags.NoDocking))
            {
                // show navigation sidebar
                ImGui.BeginChild("Nav", new Vector2(ImGui.GetTextLineHeight() * 12.0f, ImGui.GetContentRegionAvail().Y), ImGuiChildFlags.Border);
                {
                    for (int i = 0; i < NavTabs.Length; i++)
                    {
                        // don't show scripts tab if there are no scripts with preferences guis
                        if (i == (int)NavTabEnum.Scripts && !LuaScripting.Modules.GuiModule.HasPreferencesCallbacks)
                            continue;

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

                    case NavTabEnum.Interface:
                        ShowInterfaceTab(justOpened || lastNavTab != selectedNavTab);
                        break;

                    case NavTabEnum.Shortcuts:
                        ShowShortcutsTab();
                        break;

                    case NavTabEnum.Theme:
                        ShowThemeTab(justOpened || lastNavTab != selectedNavTab);
                        break;

                    case NavTabEnum.Drizzle:
                        ShowDrizzleTab();
                        break;

                    case NavTabEnum.Scripts:
                        LuaScripting.Modules.GuiModule.PrefsHook();
                        break;
                }

                ImGui.EndChild();
            }
            ImGui.End();

            if (!isWindowOpen)
            {
                activeDrizzleConfig = null;
                drizzleConfigWatcher?.Dispose();
                drizzleConfigWatcher = null;
            }
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
                        ImGuiKey key = (ImGuiKey)ki;

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
    }

    // TODO: i guess put this somewhere else, because the same code for checking
    // the data path is duplicated in three other places in this codebase.
    private static bool CheckDataPath(string path, out string[] missingDirs)
    {
        // check for any missing directories
        List<string> list = [];
        list.Add("Graphics");
        list.Add("Props");
        list.Add("Levels");

        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (Directory.Exists(Path.Combine(path, list[i])))
            {
                list.RemoveAt(i);
            }
        }

        missingDirs = [.. list];
        return list.Count == 0;
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
        var prefs = RainEd.Instance.Preferences;

        ImGui.SeparatorText("Backups");
        {
            var saveBackups = prefs.SaveFileBackups;
            if (ImGui.Checkbox("Save backups of files", ref saveBackups))
            {
                prefs.SaveFileBackups = saveBackups;
            }

            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.BeginItemTooltip())
            {
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20.0f);
                ImGui.TextWrapped("When you save a level, enabling this will make Rained move the previously saved version of the level to a different file. This serves as the backup file.\n\nWith no backup directory given, the backup will be in the same directory as the work file but with a tilde (~) appended to its file extension. With one, it will be saved to the backup directory with the same name as the work file.");
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }

            ImGui.AlignTextToFramePadding();
            ImGui.Text("Backup Directory");
            ImGui.SameLine();

            var backupDir = prefs.BackupDirectory;
            if (FileBrowser.Button("BackupDirectory", FileBrowser.OpenMode.Directory, ref backupDir, clearButton: true))
            {
                prefs.BackupDirectory = backupDir;
            }
        }

        ImGui.SeparatorText("Assets");
        {
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Data Path");
            ImGui.SameLine();
            ImGui.TextDisabled("(!)");
            if (ImGui.BeginItemTooltip())
            {
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20.0f);
                ImGui.Text("Note that a restart is required in order for changes to take effect.");
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }

            ImGui.SameLine();

            var dataPath = RainEd.Instance.AssetDataPath;
            if (FileBrowser.Button("AssetDataPath", FileBrowser.OpenMode.Directory, ref dataPath))
            {
                if (dataPath is not null)
                {
                    // check for any missing directories
                    if (CheckDataPath(dataPath, out var dirs))
                    {
                        RainEd.Instance.AssetDataPath = dataPath;
                    }
                    else
                    {
                        missingDirs = dirs;
                        ImGui.OpenPopup("Error");
                    }
                }
            }

            // show error message when given data folder is missing some
            // subdirectories
            ImGuiExt.CenterNextWindow(ImGuiCond.Appearing);
            var flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings;
            if (ImGui.IsPopupOpen("Error") && ImGui.BeginPopupModal("Error", flags))
            {
                ImGui.Text("The given data folder is missing the following subdirectories:");

                if (missingDirs is not null)
                {
                    foreach (var dir in missingDirs)
                    {
                        ImGui.BulletText(dir);
                    }
                }

                ImGui.Separator();
                if (StandardPopupButtons.Show(PopupButtonList.OK, out _))
                {
                    ImGui.CloseCurrentPopup();
                    missingDirs = null;
                }

                ImGui.EndPopup();
            }
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

            // sky roots fix
            {
                var skyRootsFix = activeDrizzleConfig!.SkyRootsFix;
                if (ImGui.Checkbox("Require in-bounds effects by default", ref skyRootsFix))
                {
                    activeDrizzleConfig.SkyRootsFix = skyRootsFix;
                    activeDrizzleConfig.SavePreferences();
                }

                ImGui.SameLine();
                ImGui.TextDisabled("(?)");
                if (ImGui.BeginItemTooltip())
                {
                    ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20.0f);
                    ImGui.TextWrapped("This will set the value of the \"Require In-Bounds\" effect property for any newly created effects or effects from levels made before this option was added. This is an alias for the \"Sky roots fix\" option in the Drizzle page.");
                    ImGui.PopTextWrapPos();
                    ImGui.EndTooltip();
                }
            }

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

    private static void ShowInterfaceTab(bool entered)
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

            // grid opacity
            float gridOpacity = prefs.GridOpacity;
            if (ImGui.SliderFloat("##Grid Opacity", ref gridOpacity, 0f, 0.5f))
            {
                prefs.GridOpacity = gridOpacity;
            }
            ImGui.SameLine();
            if (ImGui.Button("X##ResetGRIDOP"))
            {
                prefs.GridOpacity = UserPreferences.DefaultGridOpacity;
            }
            ImGui.SetItemTooltip("Reset");
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("网格不透明度");

            // spec opacity
            float specOpacity = prefs.TileSpecOpacity;
            if (ImGui.SliderFloat("##Tile Specs Opacity", ref specOpacity, 0f, 1f))
            {
                prefs.TileSpecOpacity = specOpacity;
            }
            ImGui.SameLine();
            if (ImGui.Button("X##ResetTILESPEC"))
            {
                prefs.TileSpecOpacity = UserPreferences.DefaultTileSpecOpacity;
            }
            ImGui.SetItemTooltip("重置");
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Tile Specs Opacity");

            // update layer colors in preferences class
            prefs.LayerColor1 = Vec3ToHexColor(layerColor1);
            prefs.LayerColor2 = Vec3ToHexColor(layerColor2);
            prefs.LayerColor3 = Vec3ToHexColor(layerColor3);
            prefs.BackgroundColor = Vec3ToHexColor(bgColor);
            prefs.TileSpec1 = Vec4ToHexColor(tileSpec1Color);
            prefs.TileSpec2 = Vec4ToHexColor(tileSpec2Color);
        }

        ImGui.SeparatorText("用户界面");
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

            // geo icon set
            var geometryIcons = prefs.GeometryIcons;
            if (ImGui.BeginCombo("Geometry icon set", geometryIcons))
            {
                foreach (var str in GeometryIcons.Sets)
                {
                    var isSelected = str == geometryIcons;
                    if (ImGui.Selectable(str, isSelected))
                    {
                        GeometryIcons.CurrentSet = str;
                        prefs.GeometryIcons = GeometryIcons.CurrentSet;
                    }

                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }

                ImGui.EndCombo();
            }

            // camera border view mode
            var camBorderMode = (int)prefs.CameraBorderMode;
            if (ImGui.Combo("Camera border view mode", ref camBorderMode, "Inner Border\0Outer Border\0Both Borders"))
                prefs.CameraBorderMode = (UserPreferences.CameraBorderModeOption)camBorderMode;

            // autotile mouse mode
            var autotileMouseMode = (int)prefs.AutotileMouseMode;
            if (ImGui.Combo("Autotile mouse mode", ref autotileMouseMode, "Click\0Hold"))
                prefs.AutotileMouseMode = (UserPreferences.AutotileMouseModeOptions)autotileMouseMode;

            // tile placement mode toggle
            var tilePlacementToggle = prefs.TilePlacementModeToggle ? 1 : 0;
            if (ImGui.Combo("强制放置快捷键模式", ref tilePlacementToggle, "按住\0切换"))
                prefs.TilePlacementModeToggle = tilePlacementToggle != 0;

            // prop selection layer filter
            var propSelectionLayerFilter = (int)prefs.PropSelectionLayerFilter;
            if (ImGui.Combo("Prop selection layer filter", ref propSelectionLayerFilter, "All\0Current\0In Front"))
                prefs.PropSelectionLayerFilter = (UserPreferences.PropSelectionLayerFilterOption)propSelectionLayerFilter;

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
            var lightEditorControlScheme = (int)prefs.LightEditorControlScheme;
            if (ImGui.Combo("Light editor control scheme", ref lightEditorControlScheme, "Mouse\0Keyboard\0"))
                prefs.LightEditorControlScheme = (UserPreferences.LightEditorControlSchemeOption)lightEditorControlScheme;

            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.BeginItemTooltip())
            {
                ImGui.TextUnformatted(
                    """
                    这将改变灯光编辑器中笔刷的缩放和旋转方式。
                    - 鼠标:按住q/e，移动鼠标分别进行缩放和旋转。
                    - 键盘:模仿官方关卡编辑器中的控件:wasd缩放，q/e旋转。
                    """
                );
                ImGui.End();
            }

            var effectPlacementPos = (int)prefs.EffectPlacementPosition;
            if (ImGui.Combo("Effect placement position", ref effectPlacementPos, "Before selected\0After selected\0First\0Last\0"))
                prefs.EffectPlacementPosition = (UserPreferences.EffectPlacementPositionOption)effectPlacementPos;

            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.BeginItemTooltip())
            {
                ImGui.TextUnformatted(
                    """
                    Changes where effects are inserted into
                    the Active Effects list when created.
                    """
                );
                ImGui.End();
            }

            var effectPlacementAltPos = (int)prefs.EffectPlacementAltPosition;
            if (ImGui.Combo("Effect placement alt position", ref effectPlacementAltPos, "Before selected\0After selected\0First\0Last\0"))
                prefs.EffectPlacementAltPosition = (UserPreferences.EffectPlacementPositionOption)effectPlacementAltPos;

            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.BeginItemTooltip())
            {
                ImGui.TextUnformatted(
                    """
                    Changes where effects are inserted into
                    the Active Effects list when created when
                    SHIFT is held.
                    """
                );
                ImGui.End();
            }

            ImGui.PopItemWidth();
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

                var fontSize = prefs.FontSize;
                if (ImGui.InputInt("Font size", ref fontSize))
                    prefs.FontSize = fontSize;

                if (ImGui.IsItemDeactivatedAfterEdit())
                    Fonts.FontReloadQueued = true;

                ImGui.SameLine();
                ImGui.TextDisabled("(?)");
                if (ImGui.BeginItemTooltip())
                {
                    ImGui.Text("The default value for this is 13.");
                    ImGui.EndTooltip();
                }

                ImGui.PopItemWidth();
            }

            // Vsync
            {
                bool vsync = Boot.Window.VSync;
                if (ImGui.Checkbox("Vsync", ref vsync))
                {
                    Boot.Window.VSync = vsync;
                    prefs.Vsync = vsync;

                    if (vsync)
                    {
                        Boot.RefreshRate = Boot.DefaultRefreshRate;
                    }
                }

                if (!vsync)
                {
                    ImGui.SameLine();

                    ImGui.SetNextItemWidth(ImGui.GetFontSize() * 8.0f);

                    ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemInnerSpacing);
                    var refreshRate = prefs.RefreshRate;
                    if (ImGui.SliderInt("###Refresh rate", ref refreshRate, 30, 240))
                    {
                        prefs.RefreshRate = refreshRate;
                    }

                    if (ImGui.IsItemDeactivatedAfterEdit())
                    {
                        Boot.RefreshRate = prefs.RefreshRate;
                        prefs.RefreshRate = Boot.RefreshRate;
                    }

                    ImGui.SameLine();
                    ImGui.PopStyleVar();
                    if (ImGui.Button("X###refreshratereset"))
                    {
                        Boot.RefreshRate = Boot.DefaultRefreshRate;
                        prefs.RefreshRate = Boot.RefreshRate;
                    }

                    ImGui.SameLine();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text("Refresh rate");
                }
            }

            ImGui.PopItemWidth();
        }
    }

    private static void ShowShortcutsTab()
    {
        ImGui.SeparatorText("易用性");
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
        ShortcutButton(KeyShortcut.RopeSimulationFast);
        ShortcutButton(KeyShortcut.ResetSimulation);
        ShortcutButton(KeyShortcut.RotatePropCCW);
        ShortcutButton(KeyShortcut.RotatePropCW);
        ShortcutButton(KeyShortcut.ChangePropSnapping);
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
        ImGui.PushID((int)id);

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

        ImGui.BeginDisabled(DrizzleManager.StaticRuntime is null);
        if (ImGui.Button("Discard Drizzle runtime"))
            DrizzleManager.DisposeStaticRuntime();
        ImGui.EndDisabled();

        // static lingo runtime
        {
            boolRef = prefs.StaticDrizzleLingoRuntime;
            if (ImGui.Checkbox("Persistent Drizzle runtime", ref boolRef))
            {
                prefs.StaticDrizzleLingoRuntime = boolRef;
                if (!boolRef)
                    DrizzleManager.DisposeStaticRuntime();
            }

            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            ImGui.SetItemTooltip(
                """
                This will keep a Drizzle runtime in the background
                after a render, instead of discarding it and
                recreating a new one on the next render. This
                results in more idle RAM usage, but will decrease
                the time it takes for subsequent renders.
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
        ConfigCheckbox("Sky roots fix");

        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.BeginItemTooltip())
        {
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20.0f);
            ImGui.TextWrapped("This will set the value of the \"Require In-Bounds\" effect property for any newly created effects or effects from levels made before this option was added. This is an alias for the \"Require in-bounds effects by default\" option in the General page.");
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }
}
