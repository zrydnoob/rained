using System.Numerics;
using ImGuiNET;

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
    }

    private readonly static string[] NavTabs = ["General", "Shortcuts", "Theme", "Assets"];
    private static NavTabEnum selectedNavTab = NavTabEnum.General;

    private static KeyShortcut activeShortcut = KeyShortcut.None;
    private static bool needRestart = false;

    private static bool openPopupCmd = false;
    public static void OpenWindow()
    {
        openPopupCmd = true;
    }

    public static void ShowWindow()
    {
        if (openPopupCmd)
        {
            openPopupCmd = false;
            isWindowOpen = true;
            ImGui.OpenPopup(WindowName);

            // center popup modal
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            ImGui.SetNextWindowSize(new Vector2(ImGui.GetTextLineHeight() * 50f, ImGui.GetTextLineHeight() * 30f), ImGuiCond.FirstUseEver);
        }

        if (ImGui.BeginPopupModal(WindowName, ref isWindowOpen))
        {
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
                    ShowGeneralTab();
                    break;

                case NavTabEnum.Shortcuts:
                    ShowShortcutsTab();
                    break;

                case NavTabEnum.Theme:
                    ShowThemeTab();
                    break;
                
                case NavTabEnum.Assets:
                    ShowAssetsTab();
                    break;
            }

            ImGui.EndChild();
            ImGui.EndPopup();
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

    private static void ShowGeneralTab()
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
            Vector3 layerColor1 = HexColorToVec3(prefs.LayerColor1);
            Vector3 layerColor2 = HexColorToVec3(prefs.LayerColor2);
            Vector3 layerColor3 = HexColorToVec3(prefs.LayerColor3);
            Vector3 bgColor = HexColorToVec3(prefs.BackgroundColor);

            if (ImGui.ColorEdit3("Layer Color 1", ref layerColor1))
                prefs.LayerColor1 = Vec3ToHexColor(layerColor1);
            if (ImGui.ColorEdit3("Layer Color 2", ref layerColor2))
                prefs.LayerColor2 = Vec3ToHexColor(layerColor2);
            if (ImGui.ColorEdit3("Layer Color 3", ref layerColor3))
                prefs.LayerColor3 = Vec3ToHexColor(layerColor3);
            
            if (ImGui.ColorEdit3("Background Color", ref bgColor))
                prefs.BackgroundColor = Vec3ToHexColor(bgColor);
        }
    }

    private static void ShowShortcutsTab()
    {
        ImGui.SeparatorText("General");
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
        ImGui.Separator();
        ShortcutButton(KeyShortcut.Render);

        ImGui.SeparatorText("General Editing");
        ShortcutButton(KeyShortcut.NavUp);
        ShortcutButton(KeyShortcut.NavDown);
        ShortcutButton(KeyShortcut.NavLeft);
        ShortcutButton(KeyShortcut.NavRight);
        ImGui.Separator();
        ShortcutButton(KeyShortcut.NewObject);
        ShortcutButton(KeyShortcut.RemoveObject);
        ImGui.Separator();
        ShortcutButton(KeyShortcut.SwitchLayer);
        ShortcutButton(KeyShortcut.SwitchTab);

        ImGui.SeparatorText("Geometry Edit");
        ShortcutButton(KeyShortcut.ToggleLayer1);
        ShortcutButton(KeyShortcut.ToggleLayer2);
        ShortcutButton(KeyShortcut.ToggleLayer3);

        ImGui.SeparatorText("Tile Edit");
        ShortcutButton(KeyShortcut.Eyedropper);
        ShortcutButton(KeyShortcut.SetMaterial);
        ImGui.Separator();
        ShortcutButton(KeyShortcut.TileForceGeometry);
        ShortcutButton(KeyShortcut.TileForcePlacement);
        ShortcutButton(KeyShortcut.TileIgnoreDifferent);

        ImGui.SeparatorText("Light Edit");
        ShortcutButton(KeyShortcut.ResetBrushTransform);
        ShortcutButton(KeyShortcut.ZoomLightIn);
        ShortcutButton(KeyShortcut.ZoomLightOut);
        ShortcutButton(KeyShortcut.RotateLightCW);
        ShortcutButton(KeyShortcut.RotateLightCCW);
    }

    private static void ShowThemeTab()
    {
        ImGui.SetNextItemWidth(ImGui.GetTextLineHeight() * 12.0f);
        if (ImGui.Combo("Theme", ref RainEd.Instance.Preferences.ThemeIndex, "Dark\0Light\0ImGui Classic"))
        {
            RainEd.Instance.Preferences.ApplyTheme();
        }
    }

    private static void ShowAssetsTab()
    {

        ImGui.AlignTextToFramePadding();
        ImGui.Text("Data Path");
        ImGui.SameLine();

        var oldPath = RainEd.Instance.AssetDataPath;
        if (FileBrowser.Button("DataPath", FileBrowser.OpenMode.Directory, ref RainEd.Instance.AssetDataPath))
        {
            // if path changed, disable asset import until user restarts Rained
            if (Path.GetFullPath(oldPath) != Path.GetFullPath(RainEd.Instance.AssetDataPath))
            {
                needRestart = true;
            }
        }
        ImGui.Separator();
        
        if (needRestart)
        {
            ImGui.Text("(A restart is required before making further changes)");
            ImGui.BeginDisabled();
        }

        ImGui.Button("Import Tiles");
        var tileDb = RainEd.Instance.TileDatabase;

        ImGui.Text("Categories");
        ImGui.BeginListBox("##Categories");
        {
            foreach (var category in tileDb.Categories)
            {
                ImGui.Selectable(category.Name);
            }
        }
        ImGui.EndListBox();

        if (needRestart)
        {
            ImGui.EndDisabled();
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
}