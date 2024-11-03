using Raylib_cs;
using ImGuiNET;
using System.Numerics;
using Rained.LevelData;
namespace Rained.EditorGui.Editors;

class GeometryEditor : IEditorMode
{
    public string Name { get => "几何"; }

    private readonly LevelWindow window;

    public enum Tool : int
    {
        Wall = 0,
        Air,
        Inverse,
        Glass,
        Slope,
        Platform,
        HorizontalBeam,
        VerticalBeam,
        Rock,
        Spear,
        Crack,
        Waterfall,
        ShortcutEntrance,
        Shortcut,
        Entrance,
        CreatureDen,
        WhackAMoleHole,
        ScavengerHole,
        Hive,
        ForbidFlyChain,
        GarbageWorm,
        WormGrass,
        CopyBackwards,

        ToolCount // not an enum, just the number of tools
    }

    private static readonly Dictionary<Tool, string> ToolNames = new()
    {
        { Tool.Wall,            "墙"              },
        { Tool.Air,             "空气"               },
        { Tool.Inverse,         "切换墙壁/空气"   },
        { Tool.Slope,           "斜坡"             },
        { Tool.Platform,        "平台"          },
        { Tool.Rock,            "石头"              },
        { Tool.Spear,           "矛"             },
        { Tool.Crack,           "裂缝"           },
        { Tool.HorizontalBeam,  "水平杆子"   },
        { Tool.VerticalBeam,    "垂直杆子"     },
        { Tool.Glass,           "隐形墙"    },
        { Tool.ShortcutEntrance,"管道入口" },
        { Tool.Shortcut,        "管道路径圆点"      },
        { Tool.CreatureDen,     "生物巢穴"      },
        { Tool.Entrance,        "房间入口"     },
        { Tool.Hive,            "蝠蝇"       },
        { Tool.ForbidFlyChain,  "蝠蝇阻碍"  },
        { Tool.Waterfall,       "瀑布"         },
        { Tool.WhackAMoleHole,  "Whack-a-mole Hole" },
        { Tool.ScavengerHole,   "拾荒者管道"    },
        { Tool.GarbageWorm,     "垃圾虫"      },
        { Tool.WormGrass,       "舌草"        },
        { Tool.CopyBackwards,   "向后复制"    },
    };

    private static readonly Dictionary<Tool, Vector2> ToolTextureOffsets = new()
    {
        { Tool.Wall,            new(1, 0) },
        { Tool.Air,             new(2, 0) },
        { Tool.Inverse,         new(0, 0) },
        { Tool.Slope,           new(3, 0) },
        { Tool.Platform,        new(0, 1) },
        { Tool.Rock,            new(1, 1) },
        { Tool.Spear,           new(2, 1) },
        { Tool.Crack,           new(3, 1) },
        { Tool.HorizontalBeam,  new(0, 2) },
        { Tool.VerticalBeam,    new(1, 2) },
        { Tool.Glass,           new(2, 2) },
        { Tool.ShortcutEntrance,new(3, 2) },
        { Tool.Shortcut,        new(0, 3) },
        { Tool.CreatureDen,     new(1, 3) },
        { Tool.Entrance,        new(2, 3) },
        { Tool.Hive,            new(3, 3) },
        { Tool.ForbidFlyChain,  new(0, 4) },
        { Tool.Waterfall,       new(2, 4) },
        { Tool.WhackAMoleHole,  new(3, 4) },
        { Tool.ScavengerHole,   new(0, 5) },
        { Tool.GarbageWorm,     new(1, 5) },
        { Tool.WormGrass,       new(2, 5) },
        { Tool.CopyBackwards,   new(3, 5) }
    };

    private static readonly Color[] LayerColors =
    [
        new(0, 0, 0, 255),
        new(89, 255, 89, 100),
        new(255, 30, 30, 70)
    ];

    private Tool selectedTool = Tool.Wall;
    private bool isToolActive = false;
    private bool ignoreClick = false;
    private bool isErasing = false;
    private readonly RlManaged.Texture2D toolIcons;

    // tool rect - for wall/air/inverse/geometry tools
    private bool isToolRectActive;
    private int toolRectX;
    private int toolRectY;
    private int lastMouseX, lastMouseY;

    private bool[] layerMask;

    private enum MirrorFlags
    {
        MirrorX = 1,
        MirrorY = 2
    }
    private MirrorFlags mirrorFlags = 0;
    private MirrorFlags mirrorDrag = 0;

    private int mirrorOriginX;
    private int mirrorOriginY;

    private float MirrorPositionX {
        get => mirrorOriginX / 2f;
        set => mirrorOriginX = (int) Math.Round(value * 2f);
    }

    private float MirrorPositionY {
        get => mirrorOriginY / 2f;
        set => mirrorOriginY = (int) Math.Round(value * 2f);
    }

    public GeometryEditor(LevelWindow levelView)
    {
        layerMask = new bool[3];
        layerMask[0] = true;
        mirrorOriginX = RainEd.Instance.Level.Width;
        mirrorOriginY = RainEd.Instance.Level.Height;

        window = levelView;
        toolIcons = RlManaged.Texture2D.Load(Path.Combine(Boot.AppDataPath, "assets", "tool-icons.png"));

        switch (RainEd.Instance.Preferences.GeometryViewMode)
        {
            case "overlay":
                layerViewMode = LayerViewMode.Overlay;
                break;

            case "stack":
                layerViewMode = LayerViewMode.Stack;
                break;

            default:
                Log.Error("Invalid layer view mode '{ViewMode}' in preferences.json", RainEd.Instance.Preferences.GeometryViewMode);
                break;
        }
    }

    public enum LayerViewMode : int
    {
        Overlay = 0,
        Stack = 1
    }

    public LayerViewMode layerViewMode = LayerViewMode.Overlay;
    private readonly string[] viewModeNames = ["叠加", "单层"];

    public void Load()
    {
        // if there is only a single geo layer active,
        // update the active geo layer to match the
        // global work layer.
        int activeLayer = -1;

        for (int i = 0; i < Level.LayerCount; i++)
        {
            if (layerMask[i])
            {
                if (activeLayer >= 0) return;
                activeLayer = i;
            }
        }

        layerMask[0] = false;
        layerMask[1] = false;
        layerMask[2] = false;
        layerMask[window.WorkLayer] = true;
    }

    public void Unload()
    {
        isToolActive = false;
        isToolRectActive = false;
        mirrorDrag = 0;
        window.CellChangeRecorder.TryPushChange();

        // if there is only a single geo layer active,
        // update the global work layer variable to the
        // layer of the active geo layer.
        int activeLayer = -1;

        for (int i = 0; i < Level.LayerCount; i++)
        {
            if (layerMask[i])
            {
                if (activeLayer >= 0) return;
                activeLayer = i;
            }

        }

        window.WorkLayer = activeLayer == -1 ? 0 : activeLayer;
    }

    public void ReloadLevel()
    {
        mirrorOriginX = RainEd.Instance.Level.Width;
        mirrorOriginY = RainEd.Instance.Level.Height;
    }

    public void SavePreferences(UserPreferences prefs)
    {
        switch (layerViewMode)
        {
            case LayerViewMode.Overlay:
                prefs.GeometryViewMode = "overlay";
                break;

            case LayerViewMode.Stack:
                prefs.GeometryViewMode = "stack";
                break;

            default:
                Log.Error("Invalid LayerViewMode {EnumID}", (int)layerViewMode);
                break;
        }
    }

    private static bool ToolCanRectPlace(Tool tool)
    {
        return tool switch
        {
            Tool.CreatureDen => false,
            Tool.Entrance => false,
            Tool.GarbageWorm => false,
            Tool.Rock => false,
            Tool.ScavengerHole => false,
            Tool.ShortcutEntrance => false,
            Tool.Spear => false,
            _ => true,
        };
    }

    private static bool ToolCanFloodFill(Tool tool)
    {
        return tool switch
        {
            Tool.Wall => true,
            Tool.Air => true,
            _ => false
        };
    }

    private void GetRectBounds(out int rectMinX, out int rectMinY, out int rectMaxX, out int rectMaxY)
    {
        var mx = window.MouseCx;
        var my = window.MouseCy;

        rectMinX = Math.Min(mx, toolRectX);
        rectMinY = Math.Min(my, toolRectY);
        rectMaxX = Math.Max(mx, toolRectX);
        rectMaxY = Math.Max(my, toolRectY);

        // constrain to square
        if (selectedTool == Tool.Slope)
        {
            int size = Math.Max(Math.Abs(mx - toolRectX), Math.Abs(my - toolRectY));

            int startX = toolRectX;
            int startY = toolRectY;
            int endX = startX + size * (mx - toolRectX >= 0 ? 1 : -1);
            int endY = startY + size * (my - toolRectY >= 0 ? 1 : -1);

            rectMinX = Math.Min(startX, endX);
            rectMaxX = Math.Max(startX, endX);
            rectMinY = Math.Min(startY, endY);
            rectMaxY = Math.Max(startY, endY);
        }
    }

    public void DrawToolbar()
    {
        Vector4 textColor = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        var level = RainEd.Instance.Level;

        if (ImGui.Begin("建造", ImGuiWindowFlags.NoFocusOnAppearing))
        {
            // view mode
            {
                ImGui.Text("视图模式");
                ImGui.SetNextItemWidth(-0.0001f);
                if (ImGui.BeginCombo("##ViewMode", viewModeNames[(int)layerViewMode]))
                {
                    for (int i = 0; i < viewModeNames.Length; i++)
                    {
                        bool isSelected = i == (int)layerViewMode;
                        if (ImGui.Selectable(viewModeNames[i], isSelected))
                        {
                            layerViewMode = (LayerViewMode)i;
                        }

                        if (isSelected) ImGui.SetItemDefaultFocus();
                    }

                    ImGui.EndCombo();
                }
            }

            // draw toolbar
            ImGui.Text(ToolNames[selectedTool]);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2, 2));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));

            for (int i = 0; i < (int)Tool.ToolCount; i++)
            {
                Tool toolEnum = (Tool)i;

                if (i % 4 > 0) ImGui.SameLine();

                string toolName = ToolNames[toolEnum];
                Vector2 texOffset = ToolTextureOffsets[toolEnum];

                // highlight selected tool
                if (toolEnum == selectedTool)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonHovered]);

                    // tool buttons will have a more transparent hover color
                }
                else
                {
                    Vector4 col = ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonHovered];
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered,
                        new Vector4(col.X, col.Y, col.Z, col.W / 4f));
                }

                ImGui.PushID(i);

                // create tool button, select if clicked
                if (ImGuiExt.ImageButtonRect("ToolButton", toolIcons, 24 * Boot.PixelIconScale, 24 * Boot.PixelIconScale, new Rectangle(texOffset.X * 24, texOffset.Y * 24, 24, 24), textColor))
                {
                    selectedTool = toolEnum;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(toolName);
                }

                ImGui.PopID();
                ImGui.PopStyleColor();
            }
            
            ImGui.PopStyleVar(3);
            ImGui.PopStyleColor();

            // show work layers
            ImGui.Separator();

            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);

            ImGui.Text("层级");
            ImGuiExt.ButtonFlags("##Layers", ["1", "2", "3"], layerMask);

            // show mirror toggles
            ImGui.Text("镜像");
            {
                var _mirrorFlags = (int) mirrorFlags;
                if (ImGuiExt.ButtonFlags("##Mirror", ["X", "Y"], ref _mirrorFlags))
                    mirrorFlags = (MirrorFlags) _mirrorFlags;
            }

            ImGui.PopItemWidth();

            // show fill rect hint
            if (isToolRectActive)
            {
                GetRectBounds(out var rectMinX, out var rectMinY, out var rectMaxX, out var rectMaxY);
                var rectW = rectMaxX - rectMinX + 1;
                var rectH = rectMaxY - rectMinY + 1;
                window.WriteStatus($"({rectW}, {rectH})");
            }
            else
            {
                if (ToolCanRectPlace(selectedTool))
                    window.WriteStatus("Shift+拖拽以填充选区");

                if (ToolCanFloodFill(selectedTool))
                    window.WriteStatus(KeyShortcuts.GetShortcutString(KeyShortcut.FloodFill) + "+点击以洪水填充");
            }
        }
        ImGui.End();

        // simple layer shortcut
        if (KeyShortcuts.Activated(KeyShortcut.SwitchLayer))
        {
            (layerMask[1], layerMask[2], layerMask[0]) = (layerMask[0], layerMask[1], layerMask[2]);
        }

        // layer mask toggle shortcuts
        if (KeyShortcuts.Activated(KeyShortcut.ToggleLayer1))
            layerMask[0] = !layerMask[0];

        if (KeyShortcuts.Activated(KeyShortcut.ToggleLayer2))
            layerMask[1] = !layerMask[1];

        if (KeyShortcuts.Activated(KeyShortcut.ToggleLayer3))
            layerMask[2] = !layerMask[2];

        if (KeyShortcuts.Activated(KeyShortcut.ToggleMirrorX))
            mirrorFlags ^= MirrorFlags.MirrorX;

        if (KeyShortcuts.Activated(KeyShortcut.ToggleMirrorY))
            mirrorFlags ^= MirrorFlags.MirrorY;
    }

    public void DrawViewport(RlManaged.RenderTexture2D mainFrame, RlManaged.RenderTexture2D[] layerFrames)
    {
        window.BeginLevelScissorMode();

        var level = RainEd.Instance.Level;
        var levelRender = window.Renderer;

        // draw level background (solid white)
        Raylib.DrawRectangle(0, 0, level.Width * Level.TileSize, level.Height * Level.TileSize, LevelWindow.BackgroundColor);

        // update layer colors
        {
            var layerCol1 = RainEd.Instance.Preferences.LayerColor1;
            var layerCol2 = RainEd.Instance.Preferences.LayerColor2;
            var layerCol3 = RainEd.Instance.Preferences.LayerColor3;

            LayerColors[0] = new Color(layerCol1.R, layerCol1.G, layerCol1.B, (byte)255);
            LayerColors[1] = new Color(layerCol2.R, layerCol2.G, layerCol2.B, (byte)100);
            LayerColors[2] = new Color(layerCol3.R, layerCol3.G, layerCol3.B, (byte)70);
        }

        // draw the layers
        int foregroundAlpha = 255; // this is stored for drawing objects later
        var drawTiles = RainEd.Instance.Preferences.ViewTiles;
        var drawProps = RainEd.Instance.Preferences.ViewProps;

        switch (layerViewMode)
        {
            // overlay: each layer is drawn over each other with a unique transparent colors
            case LayerViewMode.Overlay:
                for (int l = 0; l < Level.LayerCount; l++)
                {
                    var color = LayerColors[l];
                    levelRender.RenderGeometry(l, color);

                    if (drawTiles)
                    {
                        levelRender.RenderTiles(l, color.A);
                    }

                    if (drawProps)
                    {
                        levelRender.RenderProps(l, color.A);
                    }
                }

                levelRender.RenderObjects(2, new Color(255, 255, 255, foregroundAlpha / 4));
                levelRender.RenderObjects(1, new Color(255, 255, 255, foregroundAlpha / 2));
                levelRender.RenderObjects(0, new Color(255, 255, 255, foregroundAlpha));

                break;

            // stack: view each layer individually, each other layer is transparent
            case LayerViewMode.Stack:
                // the only layer that is shown completely opaque
                // is the first active layer
                int shownLayer = -1;

                for (int l = 0; l < Level.LayerCount; l++)
                {
                    if (layerMask[l])
                    {
                        shownLayer = l;
                        break;
                    }
                }

                for (int l = Level.LayerCount - 1; l >= 0; l--)
                {
                    var alpha = (l == shownLayer) ? 255 : 50;
                    if (l == 0) foregroundAlpha = alpha;
                    var color = LevelWindow.GeoColor(alpha);
                    int offset = (l - shownLayer) * 2;

                    Rlgl.PushMatrix();
                    Rlgl.Translatef(offset, offset, 0f);
                    levelRender.RenderGeometry(l, color);

                    if (drawTiles)
                    {
                        // if alpha is 255, the product wil be 100 (like in every other edit mode)
                        // and a smaller geo alpha will thus have a smaller tile alpha value
                        levelRender.RenderTiles(l, (int)(alpha * (100.0f / 255.0f)));
                    }

                    if (drawProps)
                    {
                        levelRender.RenderProps(l, (int)(alpha * (100.0f / 255.0f)));
                    }

                    levelRender.RenderObjects(l, new Color(255, 255, 255, alpha));

                    Rlgl.PopMatrix();
                }

                break;
        }

        levelRender.RenderShortcuts(Color.White);
        levelRender.RenderGrid();
        levelRender.RenderBorder();
        levelRender.RenderCameraBorders();

        // draw mirror splits
        MirrorFlags mirrorCursor = 0;

        if (mirrorFlags.HasFlag(MirrorFlags.MirrorX))
        {
            Raylib.DrawLineEx(
                new Vector2(MirrorPositionX * Level.TileSize, 0),
                new Vector2(MirrorPositionX * Level.TileSize, level.Height * Level.TileSize),
                2f / window.ViewZoom,
                mirrorDrag.HasFlag(MirrorFlags.MirrorX) ? Color.White : Color.Red
            );

            // click and drag to move split
            if (MathF.Abs(window.MouseCellFloat.X - MirrorPositionX) * window.ViewZoom < 0.2)
            {
                if (!mirrorDrag.HasFlag(MirrorFlags.MirrorX) && !isToolActive)
                {
                    mirrorCursor |= MirrorFlags.MirrorX;

                    if (EditorWindow.IsMouseClicked(ImGuiMouseButton.Left))
                        mirrorDrag |= MirrorFlags.MirrorX;
                }
                
                if (EditorWindow.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    mirrorOriginX = RainEd.Instance.Level.Width;
                    mirrorDrag &= ~MirrorFlags.MirrorX;
                    ignoreClick = true;
                }
            }
        }

        if (mirrorFlags.HasFlag(MirrorFlags.MirrorY))
        {
            Raylib.DrawLineEx(
                new Vector2(0, MirrorPositionY * Level.TileSize),
                new Vector2(level.Width * Level.TileSize, MirrorPositionY * Level.TileSize),
                2f / window.ViewZoom,
                mirrorDrag.HasFlag(MirrorFlags.MirrorY) ? Color.White : Color.Red
            );

            // click and drag to move split
            if (MathF.Abs(window.MouseCellFloat.Y - MirrorPositionY) * window.ViewZoom < 0.2)
            {
                if (!mirrorDrag.HasFlag(MirrorFlags.MirrorY) && !isToolActive)
                {
                    mirrorCursor |= MirrorFlags.MirrorY;

                    if (EditorWindow.IsMouseClicked(ImGuiMouseButton.Left))
                        mirrorDrag |= MirrorFlags.MirrorY;
                }

                if (EditorWindow.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    mirrorOriginY = RainEd.Instance.Level.Height;
                    mirrorDrag &= ~MirrorFlags.MirrorY;
                    ignoreClick = true;
                }
            }
        }

        // mouse cursor
        mirrorCursor |= mirrorDrag;

        if (mirrorCursor.HasFlag(MirrorFlags.MirrorX) && mirrorCursor.HasFlag(MirrorFlags.MirrorY))
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
        
        else if (mirrorCursor.HasFlag(MirrorFlags.MirrorX))
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
        
        else if (mirrorCursor.HasFlag(MirrorFlags.MirrorY))
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNS);

        // mirror drag logic
        if (mirrorDrag.HasFlag(MirrorFlags.MirrorX))
        {
            mirrorOriginX = !ImGui.IsKeyDown(ImGuiKey.ModShift)
                ? (int)MathF.Round(window.MouseCellFloat.X * 2f)
                : (int)MathF.Round(window.MouseCellFloat.X) * 2;
        }

        if (mirrorDrag.HasFlag(MirrorFlags.MirrorY))
        {
            mirrorOriginY = !ImGui.IsKeyDown(ImGuiKey.ModShift)
                ? (int)MathF.Round(window.MouseCellFloat.Y * 2f)
                : (int)MathF.Round(window.MouseCellFloat.Y) * 2;
        }

        if (mirrorDrag != 0 && !EditorWindow.IsMouseDown(ImGuiMouseButton.Left))
            mirrorDrag = 0;

        // WASD navigation
        if (!ImGui.GetIO().WantCaptureKeyboard && !ImGui.GetIO().WantTextInput)
        {
            int toolRow = (int)selectedTool / 4;
            int toolCol = (int)selectedTool % 4;
            int toolCount = (int)Tool.ToolCount;

            if (KeyShortcuts.Activated(KeyShortcut.NavRight))
            {
                if ((int)selectedTool == (toolCount - 1))
                {
                    toolCol = 0;
                    toolRow = 0;
                }
                else if (++toolCol > 4)
                {
                    toolCol = 0;
                    toolRow++;
                }
            }

            if (KeyShortcuts.Activated(KeyShortcut.NavLeft))
            {
                toolCol--;
                if (toolCol < 0)
                {
                    toolCol = 3;
                    toolRow--;
                }
            }

            if (KeyShortcuts.Activated(KeyShortcut.NavUp))
            {
                toolRow--;
            }

            if (KeyShortcuts.Activated(KeyShortcut.NavDown))
            {
                // if on the last row, wrap back to first row
                // else, just go to next row
                if (toolRow == (toolCount - 1) / 4)
                    toolRow = 0;
                else
                    toolRow++;
            }

            if (toolRow < 0)
            {
                toolRow = (toolCount - 1) / 4;
            }

            selectedTool = (Tool)Math.Clamp(toolRow * 4 + toolCol, 0, toolCount - 1);
        }

        bool isMouseDown = EditorWindow.IsMouseDown(ImGuiMouseButton.Left) || EditorWindow.IsMouseDown(ImGuiMouseButton.Right);
        if (ignoreClick)
            isMouseDown = false;

        if (window.IsViewportHovered && mirrorDrag == 0)
        {
            // cursor rect mode
            if (isToolRectActive)
            {
                GetRectBounds(out var rectMinX, out var rectMinY, out var rectMaxX, out var rectMaxY);
                var rectW = rectMaxX - rectMinX + 1;
                var rectH = rectMaxY - rectMinY + 1;

                // draw tool rect
                Raylib.DrawRectangleLinesEx(
                    new Rectangle(rectMinX * Level.TileSize, rectMinY * Level.TileSize, rectW * Level.TileSize, rectH * Level.TileSize),
                    1f / window.ViewZoom,
                    Color.White
                );

                if (!isMouseDown)
                {
                    ApplyToolRect(!isErasing);
                }
            }

            // normal cursor mode
            // if mouse is within level bounds?
            else
            {
                isToolRectActive = false;

                // draw grid cursor
                Raylib.DrawRectangleLinesEx(
                    new Rectangle(window.MouseCx * Level.TileSize, window.MouseCy * Level.TileSize, Level.TileSize, Level.TileSize),
                    1f / window.ViewZoom,
                    Color.White
                );

                // activate tool on click
                // or if user moves mouse on another tile space
                bool isClicked = false;
                if (!isToolActive && isMouseDown)
                {
                    isClicked = true;
                    isErasing = KeyShortcuts.Active(KeyShortcut.RightMouse);
                    isToolActive = true;
                    window.CellChangeRecorder.BeginChange();
                }

                if (isToolActive)
                {
                    if (isClicked || window.MouseCx != lastMouseX || window.MouseCy != lastMouseY)
                    {
                        if (isClicked && EditorWindow.IsKeyDown(ImGuiKey.ModShift) && ToolCanRectPlace(selectedTool))
                        {
                            isToolRectActive = true;
                            toolRectX = window.MouseCx;
                            toolRectY = window.MouseCy;
                        }
                        else if (isErasing && window.IsMouseInLevel())
                        {
                            Erase(window.MouseCx, window.MouseCy);
                        }
                        else
                        {
                            if (!isToolRectActive)
                            {
                                if (KeyShortcuts.Active(KeyShortcut.FloodFill) && EditorWindow.IsMouseClicked(ImGuiMouseButton.Left) && ToolCanFloodFill(selectedTool))
                                {
                                    for (int l = 0; l < Level.LayerCount; l++)
                                    {
                                        if (layerMask[l])
                                            ActivateToolFloodFill(selectedTool, window.MouseCx, window.MouseCy, l);
                                    }
                                }
                                else
                                {
                                    Util.Bresenham(lastMouseX, lastMouseY, window.MouseCx, window.MouseCy, (int x, int y) =>
                                    {
                                        ActivateTool(selectedTool, x, y, EditorWindow.IsMouseClicked(ImGuiMouseButton.Left));
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }

        if (isToolActive && !isMouseDown)
        {
            isToolActive = false;
            window.CellChangeRecorder.PushChange();
        }

        if (!EditorWindow.IsMouseDown(ImGuiMouseButton.Left) && !EditorWindow.IsMouseDown(ImGuiMouseButton.Right))
            ignoreClick = false;
        
        lastMouseX = window.MouseCx;
        lastMouseY = window.MouseCy;

        Raylib.EndScissorMode();
    }

    private int GetMirroredPositions(int tx, int ty, Span<(int x, int y)> positions)
    {
        int count = 1;
        positions[0] = (tx, ty);

        // if the mirror line is in the center of a tile, and the
        // mouse pos occupies the same tile, mirroring would have no effect.
        // thus, to save computation/mem or make it less error prone or whatever,
        // these flags aid in preventing the storage and processing of the
        // redundant positions.
        bool doMirrorX = tx * 2 + 1 != mirrorOriginX;
        bool doMirrorY = ty * 2 + 1 != mirrorOriginY;

        if (mirrorFlags.HasFlag(MirrorFlags.MirrorX) && doMirrorX)
            positions[count++] = ((int)(MirrorPositionX * 2 - tx - 1), ty);
        
        if (mirrorFlags.HasFlag(MirrorFlags.MirrorY) && doMirrorY)
            positions[count++] = (tx, (int)(MirrorPositionY * 2 - ty - 1));
        
        if (mirrorFlags.HasFlag(MirrorFlags.MirrorX) && mirrorFlags.HasFlag(MirrorFlags.MirrorY))
        {
            if (doMirrorX || doMirrorY)
                positions[count++] = ((int)(MirrorPositionX * 2 - tx - 1), (int)(MirrorPositionY * 2 - ty - 1));
        }

        return count;
    }

    private void Erase(int tx, int ty)
    {
        var level = RainEd.Instance.Level;

        Span<(int x, int y)> mirrorPositions = stackalloc (int x, int y)[4];
        int mirrorCount = GetMirroredPositions(tx, ty, mirrorPositions);

        for (int i = 0; i < mirrorCount; i++)
        {
            var (x, y) = mirrorPositions[i];
            if (!level.IsInBounds(x, y)) return;

            for (int l = 0; l < 3; l++)
            {
                if (!layerMask[l]) continue;

                ref var cell = ref level.Layers[l, x, y];
                cell.Objects = 0;

                if (cell.Geo == GeoType.ShortcutEntrance || selectedTool is Tool.Wall or Tool.Platform or Tool.Slope or Tool.Glass)
                    cell.Geo = GeoType.Air;
                else if (selectedTool is Tool.Air)
                    cell.Geo = GeoType.Solid;

                window.Renderer.InvalidateGeo(x, y, l);
            }
        }
    }

    private void ActivateTool(Tool tool, int tx, int ty, bool pressed)
    {
        Span<(int x, int y)> mirrorPositions = stackalloc (int x, int y)[4];
        int mirrorCount = GetMirroredPositions(tx, ty, mirrorPositions);

        for (int i = 0; i < mirrorCount; i++)
        {
            ActivateToolSingleTile(tool, mirrorPositions[i].x, mirrorPositions[i].y, pressed);
            if (isToolRectActive) return;
        }
    }

    private bool toolPlaceMode;

    private static GeoType CalcPossibleSlopeType(int tx, int ty, int layer)
    {
        var level = RainEd.Instance.Level;

        static bool IsSolid(Level level, int l, int x, int y)
        {
            if (x < 0 || y < 0) return false;
            if (x >= level.Width || y >= level.Height) return false;
            return level.Layers[l, x, y].Geo == GeoType.Solid;
        }

        GeoType newType = GeoType.Air;
        int possibleConfigs = 0;

        // figure out how to orient the slope using solid neighbors
        if (IsSolid(level, layer, tx - 1, ty) && IsSolid(level, layer, tx, ty + 1))
        {
            newType = GeoType.SlopeRightUp;
            possibleConfigs++;
        }

        if (IsSolid(level, layer, tx + 1, ty) && IsSolid(level, layer, tx, ty + 1))
        {
            newType = GeoType.SlopeLeftUp;
            possibleConfigs++;
        }

        if (IsSolid(level, layer, tx - 1, ty) && IsSolid(level, layer, tx, ty - 1))
        {
            newType = GeoType.SlopeRightDown;
            possibleConfigs++;
        }

        if (IsSolid(level, layer, tx + 1, ty) && IsSolid(level, layer, tx, ty - 1))
        {
            newType = GeoType.SlopeLeftDown;
            possibleConfigs++;
        }

        if (possibleConfigs == 1)
            return newType;

        return GeoType.Air;
    }

    private void ActivateToolSingleTile(Tool tool, int tx, int ty, bool pressed)
    {
        var level = RainEd.Instance.Level;
        if (!level.IsInBounds(tx, ty)) return;

        isToolRectActive = false;

        for (int layer = 0; layer < 3; layer++)
        {
            if (!layerMask[layer]) continue;

            var cell = level.Layers[layer, tx, ty];
            LevelObject levelObject = LevelObject.None;

            switch (tool)
            {
                case Tool.Wall:
                    cell.Geo = GeoType.Solid;
                    break;

                case Tool.Air:
                    cell.Geo = GeoType.Air;
                    break;

                case Tool.Platform:
                    cell.Geo = GeoType.Platform;
                    break;

                case Tool.Glass:
                    cell.Geo = GeoType.Glass;
                    break;

                case Tool.Inverse:
                    if (pressed) toolPlaceMode = cell.Geo == GeoType.Air;
                    cell.Geo = toolPlaceMode ? GeoType.Solid : GeoType.Air;
                    break;

                case Tool.ShortcutEntrance:
                    if (layer == 0 && pressed)
                        cell.Geo = cell.Geo == GeoType.ShortcutEntrance ? GeoType.Air : GeoType.ShortcutEntrance;
                    break;

                case Tool.Slope:
                    {
                        if (!pressed) break;

                        var slopeType = CalcPossibleSlopeType(tx, ty, layer);
                        if (slopeType != GeoType.Air)
                        {
                            cell.Geo = slopeType;
                        }

                        break;
                    }

                case Tool.CopyBackwards:
                    {
                        int dstLayer = (layer + 1) % 3;

                        ref var dstCell = ref level.Layers[dstLayer, tx, ty];
                        dstCell.Geo = cell.Geo;
                        dstCell.Objects = cell.Objects;
                        window.Renderer.InvalidateGeo(tx, ty, dstLayer);

                        break;
                    }

                // the following will use the default object tool
                // handler
                case Tool.HorizontalBeam:
                    levelObject = LevelObject.HorizontalBeam;
                    break;

                case Tool.VerticalBeam:
                    levelObject = LevelObject.VerticalBeam;
                    break;

                case Tool.Rock:
                    levelObject = LevelObject.Rock;
                    break;

                case Tool.Spear:
                    levelObject = LevelObject.Spear;
                    break;

                case Tool.Crack:
                    levelObject = LevelObject.Crack;
                    break;

                case Tool.Hive:
                    // hives can only be placed above ground
                    if (ty < level.Height - 1 && level.Layers[layer, tx, ty + 1].Geo == GeoType.Solid)
                    {
                        levelObject = LevelObject.Hive;
                    }
                    else
                    {
                        levelObject = LevelObject.None;
                    }
                    break;

                case Tool.ForbidFlyChain:
                    levelObject = LevelObject.ForbidFlyChain;
                    break;

                case Tool.Waterfall:
                    levelObject = LevelObject.Waterfall;
                    break;

                case Tool.WormGrass:
                    levelObject = LevelObject.WormGrass;
                    break;

                case Tool.Shortcut:
                    levelObject = LevelObject.Shortcut;
                    break;

                case Tool.Entrance:
                    levelObject = LevelObject.Entrance;
                    break;

                case Tool.CreatureDen:
                    levelObject = LevelObject.CreatureDen;
                    break;

                case Tool.WhackAMoleHole:
                    levelObject = LevelObject.WhackAMoleHole;
                    break;

                case Tool.GarbageWorm:
                    levelObject = LevelObject.GarbageWorm;
                    break;

                case Tool.ScavengerHole:
                    levelObject = LevelObject.ScavengerHole;
                    break;
            }

            if (levelObject != LevelObject.None)
            {
                // player can only place objects on work layer 1 (except if it's a beam or crack)
                if (layer == 0 || levelObject == LevelObject.HorizontalBeam || levelObject == LevelObject.VerticalBeam || levelObject == LevelObject.Crack || levelObject == LevelObject.Hive)
                {
                    if (pressed) toolPlaceMode = cell.Has(levelObject);
                    if (toolPlaceMode)
                        cell.Remove(levelObject);
                    else
                        cell.Add(levelObject);
                }
            }

            level.Layers[layer, tx, ty] = cell;
            window.Renderer.InvalidateGeo(tx, ty, layer);
        }
    }

    private void ActivateToolFloodFill(Tool tool, int srcX, int srcY, int layer)
    {
        var level = RainEd.Instance.Level;
        var renderer = RainEd.Instance.LevelView.Renderer;

        if (!level.IsInBounds(srcX, srcY)) return;

        isToolRectActive = false;

        GeoType geoMedium = level.Layers[layer, srcX, srcY].Geo;
        GeoType fillGeo = tool switch
        {
            Tool.Air => GeoType.Air,
            Tool.Wall => GeoType.Solid,
            _ => throw new ArgumentException("ActivateToolFloodFill only supports Tool.Air and Tool.Wall", nameof(tool))
        };

        if (fillGeo == geoMedium) return;

        // use a recursive scanline fill algorithm
        // with a manually-managed stack
        Stack<(int, int)> fillStack = [];
        fillStack.Push((srcX, srcY));

        while (fillStack.Count > 0)
        {
            if (fillStack.Count > 100000)
            {
                Log.UserLogger.Error("Flood fill stack overflow!");
                EditorWindow.ShowNotification("Stack overflow!");
                break;
            }

            (int x, int y) = fillStack.Pop();

            // go to left bounds of this scanline
            while (level.IsInBounds(x, y) && level.Layers[layer, x, y].Geo == geoMedium)
                x--;

            x++;

            bool oldAboveEmpty = false;
            bool oldBelowEmpty = false;

            // go to right bounds of the scanline, spawning new scanlines above or below if detected
            while (level.IsInBounds(x, y) && level.Layers[layer, x, y].Geo == geoMedium)
            {
                bool aboveEmpty = level.IsInBounds(x, y - 1) && level.Layers[layer, x, y - 1].Geo == geoMedium;
                bool belowEmpty = level.IsInBounds(x, y + 1) && level.Layers[layer, x, y + 1].Geo == geoMedium;

                if (aboveEmpty != oldAboveEmpty && aboveEmpty)
                {
                    fillStack.Push((x, y - 1));
                }

                if (belowEmpty != oldBelowEmpty && belowEmpty)
                {
                    fillStack.Push((x, y + 1));
                }

                oldAboveEmpty = aboveEmpty;
                oldBelowEmpty = belowEmpty;

                level.Layers[layer, x, y].Geo = fillGeo;
                renderer.InvalidateGeo(x, y, layer);
                x++;
            }
        }
    }

    private void ApplyToolRect(bool place)
    {
        GetRectBounds(out var rectMinX, out var rectMinY, out var rectMaxX, out var rectMaxY);

        if (place)
        {
            // hardcoded slope handler...
            // but eh, i'm the only one who will modify this code anyway.
            if (selectedTool == Tool.Slope)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (layerMask[i]) RectSlope(i, rectMinX, rectMinY, rectMaxX, rectMaxY);
                }
            }

            // apply the rect to the tool by
            // applying the tool at every cell
            // in the rectangle.
            else
            {
                for (int x = rectMinX; x <= rectMaxX; x++)
                {
                    for (int y = rectMinY; y <= rectMaxY; y++)
                    {
                        ActivateTool(selectedTool, x, y, true);
                    }
                }
            }
        }
        else // erase mode
        {
            for (int x = rectMinX; x <= rectMaxX; x++)
            {
                for (int y = rectMinY; y <= rectMaxY; y++)
                {
                    Erase(x, y);
                }
            }
        }

        isToolRectActive = false;
    }

    static bool IsSolid(Level level, int l, int x, int y)
    {
        if (x < 0 || y < 0) return false;
        if (x >= level.Width || y >= level.Height) return false;
        return level.Layers[l, x, y].Geo == GeoType.Solid;
    }

    private void RectSlope(int layer, int rectLf, int rectTp, int rectRt, int rectBt)
    {
        if (rectRt - rectLf != rectBt - rectTp)
        {
            Log.Error("Slope mode rect fill is not square!");
            return;
        }

        var level = RainEd.Instance.Level;

        static bool isSolid(Level level, int l, int x, int y)
        {
            if (x < 0 || y < 0) return false;
            if (x >= level.Width || y >= level.Height) return false;
            return level.Layers[l, x, y].Geo == GeoType.Solid;
        }

        static int CalcDirection(int x, int y, int layer)
        {
            var level = RainEd.Instance.Level;
            int possibleDirections = 0;
            int selectedDir = -1;

            // right
            if (isSolid(level, layer, x + 1, y))
            {
                possibleDirections++;
                selectedDir = 0;
            }

            // bottom
            if (isSolid(level, layer, x, y + 1))
            {
                possibleDirections++;
                selectedDir = 1;
            }

            // left
            if (isSolid(level, layer, x - 1, y))
            {
                possibleDirections++;
                selectedDir = 2;
            }

            // top
            if (isSolid(level, layer, x, y - 1))
            {
                possibleDirections++;
                selectedDir = 3;
            }

            if (possibleDirections == 1)
            {
                return selectedDir;
            }
            else
            {
                return -1;
            }
        }

        // figure out how to orient the slope by checking the slope type
        // at each corner. slopes can only be formed if the two edges at
        // one of the two diagonals are the same slope
        GeoType slopeType = GeoType.Air;
        int topLeft = CalcDirection(rectLf, rectTp, layer);
        int topRight = CalcDirection(rectRt, rectTp, layer);
        int bottomRight = CalcDirection(rectRt, rectBt, layer);
        int bottomLeft = CalcDirection(rectLf, rectBt, layer);

        // check slope type dependent on the corner directions
        if (bottomLeft == 1 && topRight == 0)
            slopeType = GeoType.SlopeLeftUp;
        else if (bottomLeft == 2 && topRight == 3)
            slopeType = GeoType.SlopeRightDown;
        else if (bottomRight == 1 && topLeft == 2)
            slopeType = GeoType.SlopeRightUp;
        else if (bottomRight == 0 && topLeft == 3)
            slopeType = GeoType.SlopeLeftDown;

        if (slopeType != GeoType.Air)
        {
            int tileY;
            int dy;
            int fillTo;

            // positive dy/dx
            if (slopeType == GeoType.SlopeLeftUp || slopeType == GeoType.SlopeRightDown)
            {
                tileY = rectBt;
                dy = -1;
                fillTo = slopeType == GeoType.SlopeRightDown ? rectTp : rectBt;
            }
            else // negative dy/dx
            {
                tileY = rectTp;
                dy = 1;
                fillTo = slopeType == GeoType.SlopeLeftDown ? rectTp : rectBt;
            }

            List<Vector2i> desiredSlopes = [];

            for (int x = rectLf; x <= rectRt; x++)
            {
                int i = tileY;

                while (true)
                {
                    // use ActivateTool instead of setting geo directly
                    // so mirroring works
                    ActivateTool(Tool.Wall, x, i, true);

                    if (i == fillTo)
                    {
                        break;
                    }

                    if (fillTo == rectBt) i++;
                    else i--;
                }

                // place slopes after
                desiredSlopes.Add(new(x, tileY));
                level.Layers[layer, x, tileY].Geo = slopeType;
                tileY += dy;
            }

            foreach (var pos in desiredSlopes)
            {
                ActivateTool(Tool.Slope, pos.X, pos.Y, true);
            }
        }
    }
}