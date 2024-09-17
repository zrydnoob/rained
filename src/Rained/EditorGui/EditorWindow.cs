using Raylib_cs;
using System.Numerics;
using ImGuiNET;

namespace RainEd;

static class EditorWindow
{
    // input overrides for lmb because of alt + left click drag
    private static bool isLmbPanning = false;
    private static bool isLmbDown = false;
    private static bool isLmbClicked = false;
    private static bool isLmbReleased = false;
    private static bool isLmbDragging = false;

    public static bool IsPanning { get => isLmbPanning; set => isLmbPanning = value; }

    public static bool IsKeyDown(ImGuiKey key)
    {
        if (ImGui.GetIO().WantTextInput) return false;
        return ImGui.IsKeyDown(key);
    }

    public static bool IsKeyPressed(ImGuiKey key)
    {
        if (ImGui.GetIO().WantTextInput) return false;
        return ImGui.IsKeyPressed(key);
    }

    // need to use Raylib.IsKeyPressed instead of EditorWindow.IsKeyPressed
    // because i specifically disabled the Tab key in ImGui input handling
    public static bool IsTabPressed()
    {
        if (ImGui.GetIO().WantTextInput) return false;
        return Raylib.IsKeyPressed(KeyboardKey.Tab);
    }

    public static bool IsMouseClicked(ImGuiMouseButton button, bool repeat = false)
    {
        if (button == ImGuiMouseButton.Left) return isLmbClicked;
        if (button == ImGuiMouseButton.Right) return KeyShortcuts.Activated(KeyShortcut.RightMouse);
        return ImGui.IsMouseClicked(button, repeat);
    }

    public static bool IsMouseDown(ImGuiMouseButton button)
    {
        if (button == ImGuiMouseButton.Left) return isLmbDown;
        if (button == ImGuiMouseButton.Right) return KeyShortcuts.Active(KeyShortcut.RightMouse);
        return ImGui.IsMouseDown(button);
    }

    public static bool IsMouseDoubleClicked(ImGuiMouseButton button)
    {
        if (isLmbPanning) return false;
        return ImGui.IsMouseDoubleClicked(button);
    }

    public static bool IsMouseReleased(ImGuiMouseButton button)
    {
        if (button == ImGuiMouseButton.Left) return isLmbReleased;
        if (button == ImGuiMouseButton.Right) return KeyShortcuts.Deactivated(KeyShortcut.RightMouse);
        return ImGui.IsMouseReleased(button);
    }

    public static bool IsMouseDragging(ImGuiMouseButton button)
    {
        if (button == ImGuiMouseButton.Left) return isLmbDragging;
        return ImGui.IsMouseDragging(button);
    }

    private static FileBrowser? fileBrowser = null;

    private static string notification = "";
    private static float notificationTime = 0f;
    private static float notifFlash = 0f;
    private static int timerDelay = 10;

    private static DrizzleRenderWindow? drizzleRenderWindow = null;
    private static LevelResizeWindow? levelResizeWin = null;
    public static LevelResizeWindow? LevelResizeWindow { get => levelResizeWin; }

    public static void ShowNotification(string msg)
    {
        if (notification == "" || notificationTime != 3f)
        {
            notification = msg;
        }
        else
        {
            notification += "\n" + msg;
        }
        
        notificationTime = 3f;
        notifFlash = 0f;
    }

    private static Action? promptCallback;
    private static bool promptUnsavedChanges;
    private static bool promptUnsavedChangesCancelable;

    public static void PromptUnsavedChanges(Action callback, bool canCancel = true)
    {
        var changeHistory = RainEd.Instance.ChangeHistory;
        promptUnsavedChangesCancelable = canCancel;

        if (changeHistory.HasChanges || (!canCancel && string.IsNullOrEmpty(RainEd.Instance.CurrentFilePath)))
        {
            promptUnsavedChanges = true;
            promptCallback = callback;
        }
        else
        {
            callback();
        }
    }

    private static void OpenLevelBrowser(FileBrowser.OpenMode openMode, Action<string> callback)
    {
        static bool levelCheck(string path, bool isRw)
        {
            return isRw;
        }

        fileBrowser = new FileBrowser(openMode, callback, RainEd.Instance.IsTemporaryFile ? null : Path.GetDirectoryName(RainEd.Instance.CurrentFilePath));
        fileBrowser.AddFilterWithCallback("关卡文件", levelCheck, ".txt");
        fileBrowser.PreviewCallback = (string path, bool isRw) =>
        {
            if (isRw) return new BrowserLevelPreview(path);
            return null;
        };
    }

    private static void DrawMenuBar()
    {
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("文件"))
            {
                KeyShortcuts.ImGuiMenuItem(KeyShortcut.New, "新建");
                KeyShortcuts.ImGuiMenuItem(KeyShortcut.Open, "打开");

                var recentFiles = RainEd.Instance.Preferences.RecentFiles;
                if (ImGui.BeginMenu("最近打开"))
                {
                    if (recentFiles.Count == 0)
                    {
                        ImGui.MenuItem("(无最近的文件)", false);
                    }
                    else
                    {
                        // traverse backwards
                        for (int i = recentFiles.Count - 1; i >= 0; i--)
                        {
                            var filePath = recentFiles[i];

                            if (ImGui.MenuItem(Path.GetFileName(filePath)))
                            {
                                if (File.Exists(filePath))
                                {
                                    PromptUnsavedChanges(() =>
                                    {
                                        RainEd.Instance.LoadLevel(filePath);
                                    });
                                }
                                else
                                {
                                    ShowNotification("无法访问文件");
                                    recentFiles.RemoveAt(i);
                                }
                            }

                        }
                    }

                    ImGui.EndMenu();
                }

                KeyShortcuts.ImGuiMenuItem(KeyShortcut.Save, "保存");
                KeyShortcuts.ImGuiMenuItem(KeyShortcut.SaveAs, "另存为...");

                ImGui.Separator();

                KeyShortcuts.ImGuiMenuItem(KeyShortcut.Render, "渲染...");
                KeyShortcuts.ImGuiMenuItem(KeyShortcut.ExportGeometry, "渲染为关卡.txt...");
                ImGui.MenuItem("批量渲染", false);

                ImGui.Separator();
                if (ImGui.MenuItem("偏好"))
                {
                    PreferencesWindow.OpenWindow();
                }

                ImGui.Separator();
                if (ImGui.MenuItem("退出", "Alt+F4"))
                {
                    PromptUnsavedChanges(() => RainEd.Instance.Running = false);
                }

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("编辑"))
            {
                KeyShortcuts.ImGuiMenuItem(KeyShortcut.Undo, "撤销");
                KeyShortcuts.ImGuiMenuItem(KeyShortcut.Redo, "重做");
                //ImGui.Separator();
                //ImGuiMenuItemShortcut(ShortcutID.Cut, "Cut");
                //ImGuiMenuItemShortcut(ShortcutID.Copy, "Copy");
                //ImGuiMenuItemShortcut(ShortcutID.Paste, "Paste");
                ImGui.Separator();

                if (ImGui.MenuItem("设置关卡大小..."))
                {
                    levelResizeWin = new LevelResizeWindow();
                }

                var customCommands = RainEd.Instance.CustomCommands;
                if (customCommands.Count > 0)
                {
                    ImGui.Separator();

                    if (ImGui.BeginMenu("命令"))
                    {
                        foreach (RainEd.Command cmd in customCommands)
                        {
                            if (ImGui.MenuItem(cmd.Name))
                            {
                                cmd.Callback(cmd.ID);
                            }
                        }

                        ImGui.EndMenu();
                    }
                }

                ImGui.Separator();
                RainEd.Instance.LevelView.ShowEditMenu();

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("视图"))
            {
                var prefs = RainEd.Instance.Preferences;

                KeyShortcuts.ImGuiMenuItem(KeyShortcut.ViewZoomIn, "放大");
                KeyShortcuts.ImGuiMenuItem(KeyShortcut.ViewZoomOut, "缩小");
                if (ImGui.MenuItem("重置视图"))
                {
                    RainEd.Instance.LevelView.ResetView();
                }

                ImGui.Separator();

                var renderer = RainEd.Instance.LevelView.Renderer;

                KeyShortcuts.ImGuiMenuItem(KeyShortcut.ToggleViewGrid, "显示网格", renderer.ViewGrid);
                KeyShortcuts.ImGuiMenuItem(KeyShortcut.ToggleViewTiles, "显示贴图", prefs.ViewTiles);
                KeyShortcuts.ImGuiMenuItem(KeyShortcut.ToggleViewProps, "显示道具", prefs.ViewProps);
                KeyShortcuts.ImGuiMenuItem(KeyShortcut.ToggleViewCameras, "显示相机边框", renderer.ViewCameras);
                KeyShortcuts.ImGuiMenuItem(KeyShortcut.ToggleViewGraphics, "显示贴图预览", prefs.ViewPreviews);

                if (ImGui.MenuItem("显示被遮挡的杆子", null, renderer.ViewObscuredBeams))
                {
                    renderer.ViewObscuredBeams = !renderer.ViewObscuredBeams;
                    renderer.InvalidateGeo(0);
                    renderer.InvalidateGeo(1);
                    renderer.InvalidateGeo(2);
                }

                if (ImGui.MenuItem("显示瓦片主要格", null, renderer.ViewTileHeads))
                {
                    renderer.ViewTileHeads = !renderer.ViewTileHeads;
                }

                ImGui.Separator();

                if (ImGui.MenuItem("快捷键", null, ShortcutsWindow.IsWindowOpen))
                {
                    ShortcutsWindow.IsWindowOpen = !ShortcutsWindow.IsWindowOpen;
                }

                if (ImGui.MenuItem("插件日志", null, LuaInterface.IsLogWindowOpen))
                {
                    LuaInterface.IsLogWindowOpen = !LuaInterface.IsLogWindowOpen;
                }

                if (ImGui.MenuItem("调色板", null, PaletteWindow.IsWindowOpen))
                {
                    PaletteWindow.IsWindowOpen = !PaletteWindow.IsWindowOpen;
                }

                ImGui.Separator();
                
                if (ImGui.MenuItem("打开数据文件夹..."))
                    RainEd.Instance.ShowPathInSystemBrowser(RainEd.Instance.AssetDataPath, false);
                
                if (ImGui.MenuItem("打开渲染文件夹..."))
                    RainEd.Instance.ShowPathInSystemBrowser(Path.Combine(RainEd.Instance.AssetDataPath, "Levels"), false);
                
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("帮助"))
            {
                if (ImGui.MenuItem("向导..."))
                {
                    GuideViewerWindow.IsWindowOpen = true;
                }

                if (ImGui.MenuItem("关于..."))
                {
                    AboutWindow.IsWindowOpen = true;
                }
                
                ImGui.EndMenu();
            }

            ImGui.EndMainMenuBar();
        }
    }

    private static void HandleShortcuts()
    {
        var changeHistory = RainEd.Instance.ChangeHistory;
        var prefs = RainEd.Instance.Preferences;
        var renderer = RainEd.Instance.LevelView.Renderer;

        if (KeyShortcuts.Activated(KeyShortcut.New))
        {
            PromptUnsavedChanges(() =>
            {
                Log.Information("加载默认关卡...");
                RainEd.Instance.LoadDefaultLevel();
                Log.Information("完成!");
            });
        }

        if (KeyShortcuts.Activated(KeyShortcut.Open))
        {
            PromptUnsavedChanges(() =>
            {
                OpenLevelBrowser(FileBrowser.OpenMode.Read, RainEd.Instance.LoadLevel);
            });
        }

        if (KeyShortcuts.Activated(KeyShortcut.Save))
        {
            if (RainEd.Instance.IsTemporaryFile)
                OpenLevelBrowser(FileBrowser.OpenMode.Write, SaveLevelCallback);
            else
                SaveLevelCallback(RainEd.Instance.CurrentFilePath);
        }

        if (KeyShortcuts.Activated(KeyShortcut.SaveAs))
        {
            OpenLevelBrowser(FileBrowser.OpenMode.Write, SaveLevelCallback);
        }

        if (KeyShortcuts.Activated(KeyShortcut.Undo))
        {
            changeHistory.Undo();
        }

        if (KeyShortcuts.Activated(KeyShortcut.Redo))
        {
            changeHistory.Redo();
        }

        if (KeyShortcuts.Activated(KeyShortcut.Render))
        {
            PromptUnsavedChanges(() =>
            {
                drizzleRenderWindow = new DrizzleRenderWindow(false);
            }, false);
        }

        if (KeyShortcuts.Activated(KeyShortcut.ExportGeometry))
        {
            PromptUnsavedChanges(() =>
            {
                drizzleRenderWindow = new DrizzleRenderWindow(true);
            }, false);
        }

        if (KeyShortcuts.Activated(KeyShortcut.ToggleViewGrid))
        {
            renderer.ViewGrid = !renderer.ViewGrid;
        }

        if (KeyShortcuts.Activated(KeyShortcut.ToggleViewTiles))
        {
            prefs.ViewTiles = !prefs.ViewTiles;
        }

        if (KeyShortcuts.Activated(KeyShortcut.ToggleViewProps))
        {
            prefs.ViewProps = !prefs.ViewProps;
        }

        if (KeyShortcuts.Activated(KeyShortcut.ToggleViewCameras))
        {
            renderer.ViewCameras = !renderer.ViewCameras;
        }

        if (KeyShortcuts.Activated(KeyShortcut.ToggleViewGraphics))
        {
            prefs.ViewPreviews = !prefs.ViewPreviews;
        }
    }

    private static void SaveLevelCallback(string path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            bool success;

            try
            {
                RainEd.Instance.SaveLevel(path);
                success = true;
            }
            catch
            {
                success = false;
            }

            if (success)
                promptCallback?.Invoke();
        }

        promptCallback = null;
    }

    static void ShowMiscWindows()
    {
        ShortcutsWindow.ShowWindow();
        AboutWindow.ShowWindow();
        LevelLoadFailedWindow.ShowWindow();
        PreferencesWindow.ShowWindow();
        PaletteWindow.ShowWindow();
        LuaInterface.ShowLogs();
        EmergencySaveWindow.ShowWindow();
        GuideViewerWindow.ShowWindow();
    }

    /// <summary>
    /// Called on startup, and asks the user if
    /// they want to load the emergency save file if it exists.
    /// </summary>
    public static void RequestLoadEmergencySave()
    {
        var list = RainEd.DetectEmergencySaves();
        if (list.Length > 0)
        {
            EmergencySaveWindow.UpdateList(list);
            EmergencySaveWindow.IsWindowOpen = true;
        }
    }
    
    private static bool closeDrizzleRenderWindow = false;

    public static void Render()
    {
        DrawMenuBar();
        HandleShortcuts();
        
        RainEd.Instance.LevelView.Render();

        // render level browser
        FileBrowser.Render(ref fileBrowser);
        
        // render drizzle render, if in progress
        // disposing of drizzle render window must be done on the next frame
        // otherwise the texture ID given to ImGui for the previee will be invalid
        // and it will spit out an opengl error. it's not a fatal error, it's just...
        // not supposed to happen.
        if (drizzleRenderWindow is not null)
        {
            RainEd.Instance.IsLevelLocked = true;
            RainEd.Instance.NeedScreenRefresh();

            if (closeDrizzleRenderWindow)
            {
                closeDrizzleRenderWindow = false;
                drizzleRenderWindow.Dispose();
                drizzleRenderWindow = null;
                RainEd.Instance.IsLevelLocked = false;

                // the whole render process allocates ~1 gb of memory
                // so, try to free all that
                GC.Collect(2, GCCollectionMode.Aggressive, true, true);
                GC.WaitForFullGCComplete();
            }
            
            // if this returns true, the render window had closed
            else if (drizzleRenderWindow.DrawWindow())
            {
                closeDrizzleRenderWindow = true;
            }
        }

        // render level resize window
        if (levelResizeWin is not null)
        {
            levelResizeWin.DrawWindow();
            if (!levelResizeWin.IsWindowOpen) levelResizeWin = null;
        }
        
        // notification window
        if (notificationTime > 0f) {
            ImGuiWindowFlags windowFlags =
                ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings |
                ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove;
            
            ImGuiViewportPtr viewport = ImGui.GetMainViewport();
            const float pad = 10f;

            Vector2 windowPos = new(
                viewport.WorkPos.X + pad,
                viewport.WorkPos.Y + viewport.WorkSize.Y - pad
            );
            Vector2 windowPosPivot = new(0f, 1f);
            ImGui.SetNextWindowPos(windowPos, ImGuiCond.Always, windowPosPivot);

            var flashValue = (float) (Math.Sin(Math.Min(notifFlash, 0.25f) * 16 * Math.PI) + 1f) / 2f;
            var windowBg = ImGui.GetStyle().Colors[(int) ImGuiCol.WindowBg];

            if (flashValue > 0.5f)
                ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(flashValue, flashValue, flashValue, windowBg.W));
            else
                ImGui.PushStyleColor(ImGuiCol.WindowBg, windowBg);
            
            if (ImGui.Begin("Notification", windowFlags))
                ImGui.TextUnformatted(notification);
            ImGui.End();

            ImGui.PopStyleColor();

            if (timerDelay == 0)
            {
                var dt = Raylib.GetFrameTime();
                notificationTime -= dt;
                notifFlash += dt;
            }
        }

        ShowMiscWindows();

        // prompt unsaved changes
        if (promptUnsavedChanges)
        {
            promptUnsavedChanges = false;
            ImGui.OpenPopup("未保存的更改");

            // center popup 
            ImGuiExt.CenterNextWindow(ImGuiCond.Appearing);
        }

        bool unused = true;
        if (ImGui.BeginPopupModal("未保存的更改", ref unused, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
        {
            if (promptUnsavedChangesCancelable)
            {
                ImGui.Text("继续之前是否要保存您的更改？");
            }
            else
            {
                ImGui.Text("您必须先保存，然后才能继续。\n您想现在保存吗？");
            }

            ImGui.Separator();

            if (ImGui.Button("保存", StandardPopupButtons.ButtonSize) || ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.Space))
            {
                ImGui.CloseCurrentPopup();

                // unsaved change callback is run in SaveLevel
                if (string.IsNullOrEmpty(RainEd.Instance.CurrentFilePath))
                    OpenLevelBrowser(FileBrowser.OpenMode.Write, SaveLevelCallback);
                else
                    SaveLevelCallback(RainEd.Instance.CurrentFilePath);
            }

            ImGui.SameLine();
            if (ImGui.Button("不了", StandardPopupButtons.ButtonSize) || (!promptUnsavedChangesCancelable && ImGui.IsKeyPressed(ImGuiKey.Escape)))
            {
                ImGui.CloseCurrentPopup();

                if (promptUnsavedChangesCancelable)
                {
                    if (promptCallback is not null)
                    {
                        promptCallback();
                    }
                }
                
                promptCallback = null;
            }

            if (promptUnsavedChangesCancelable)
            {
                ImGui.SameLine();
                if (ImGui.Button("取消", StandardPopupButtons.ButtonSize) || ImGui.IsKeyPressed(ImGuiKey.Escape))
                {
                    ImGui.CloseCurrentPopup();
                    promptCallback = null;
                }
            }

            ImGui.EndPopup();
        }

        if (timerDelay > 0)
            timerDelay--;
    }

    public static void UpdateMouseState()
    {
        bool wasLmbDown = isLmbDown;
        isLmbClicked = false;
        isLmbDown = false;
        isLmbReleased = false;
        isLmbDragging = false;

        if (!isLmbPanning)
        {
            isLmbDown = ImGui.IsMouseDown(ImGuiMouseButton.Left);
            isLmbDragging = ImGui.IsMouseDragging(ImGuiMouseButton.Left);

            // manually set Clicked or Released bools based on lmbdown state changes
            // this is so it registers that the mouse was released when ther user alt+tabs out of the window
            if (!wasLmbDown && isLmbDown)
                isLmbClicked = true;

            if (wasLmbDown && !isLmbDown)
                isLmbReleased = true;
        }
    }
}