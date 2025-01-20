namespace Rained.EditorGui.Editors;
using Raylib_cs;
using ImGuiNET;
using System.Numerics;
using Rained.LevelData;
using System.Diagnostics;

/// <summary>
/// Operations for copying, pasting, and moving of cells.
/// </summary>
class CellSelection
{
    public static CellSelection? Instance { get; set; } = null;

    public bool Active { get; private set; } = true;
    public bool PasteMode { get; set; } = false;
    public bool AffectTiles { get; set; } = true;

    private static RlManaged.Texture2D icons = null!;
    enum IconName
    {
        SelectRect,
        MoveSelected,
        MoveSelection,
        LassoSelect,
        MagicWand,
        TileSelect,
        OpReplace,
        OpAdd,
        OpSubtract,
        OpIntersect
    };

    enum SelectionTool
    {
        Rect,
        Lasso,
        MagicWand,
        TileSelect,
        MoveSelection,
        MoveSelected,
    };
    private SelectionTool curTool = SelectionTool.Rect;
    static readonly (IconName icon, string name)[] toolInfo = [
        (IconName.SelectRect, "Rectangle Select"),
        (IconName.LassoSelect, "Lasso Select"),
        (IconName.MagicWand, "Magic Wand"),
        (IconName.TileSelect, "Tile Select"),
        (IconName.MoveSelection, "Move Selection"),
        (IconName.MoveSelected, "Move Selected"),
    ];

    enum SelectionOperator
    {
        Replace,
        Add,
        Subtract,
        Intersect
    }

    // this is set by ui
    private SelectionOperator curOp = SelectionOperator.Replace;

    // this is set by keyboard controls
    private SelectionOperator? curOpOverride = null;

    static readonly (IconName icon, string name)[] operatorInfo = [
        (IconName.OpReplace, "Replace"),
        (IconName.OpAdd, "Add"),
        (IconName.OpSubtract, "Subtract"),
        (IconName.OpIntersect, "Intersect"),
    ];

    private int selectionMinX = 0;
    private int selectionMinY = 0;
    private int selectionMaxX = 0;
    private int selectionMaxY = 0;
    private bool selectionActive = false;
    private bool[,] selectionMask = new bool[0,0];
    private (bool mask, LevelCell cell)[,,]? movingGeometry = null;

    private int cancelOrigX = 0;
    private int cancelOrigY = 0;
    private (bool mask, LevelCell cell)[,,]? cancelGeoData = null;

    // used for mouse drag
    private bool mouseWasDragging = false;
    private Tool? mouseDragState = null;
    abstract class Tool
    {
        public abstract void Update(int mouseX, int mouseY);
        //public virtual void Submit() {}
    }

    interface ISelectionTool
    {
        public bool ApplySelection(out int minX, out int minY, out int maxX, out int maxY, out bool[,] mask);
    }

    public CellSelection()
    {
        icons ??= RlManaged.Texture2D.Load(Path.Combine(Boot.AppDataPath, "assets", "selection-icons.png"));
    }

    private static Rectangle GetIconRect(IconName icon)
    {
        return new Rectangle((int)icon * 16f, 0, 16f, 16f);
    }

    private static bool IconButton(IconName icon)
    {
        var framePadding = ImGui.GetStyle().FramePadding;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, framePadding);
        var buttonSize = 16 * Boot.PixelIconScale;
        var desiredHeight = ImGui.GetFrameHeight();
        
        // sz + pad*2 = w
        // pad = (w - sz) / 2
        ImGui.GetStyle().FramePadding = new Vector2(
            MathF.Floor( (desiredHeight - buttonSize) / 2f ),
            MathF.Floor( (desiredHeight - buttonSize) / 2f )
        );

        var textColorVec4 = ImGui.GetStyle().Colors[(int)ImGuiCol.Text] * 255f;
        bool pressed = ImGuiExt.ImageButtonRect(
            "##test",
            icons,
            buttonSize, buttonSize,
            GetIconRect(icon),
            new Color((int)textColorVec4.X, (int)textColorVec4.Y, (int)textColorVec4.Z, (int)textColorVec4.W)
        );

        ImGui.PopStyleVar();
        return pressed;
    }

    public void DrawStatusBar()
    {
        if (PasteMode)
        {
            if (ImGui.Button("Apply") || EditorWindow.IsKeyPressed(ImGuiKey.Enter))
            {
                SubmitMove();
                Active = false;
            }
            
            ImGui.SameLine();
            if (ImGui.Button("Cancel") || EditorWindow.IsKeyPressed(ImGuiKey.Escape))
            {
                CancelMove();
                Active = false;
            }
            
            return;
        }

        // selection mode options
        // for tile editor mode, Tile Select button appears.
        using (var group = ImGuiExt.ButtonGroup.Begin("Selection Mode", AffectTiles ? 5 : 4, 0))
        {
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0f, 0f));
            for (int i = 0; i < toolInfo.Length; i++)
            {
                if (toolInfo[i].icon == IconName.TileSelect && !AffectTiles)
                    continue;
                
                group.BeginButton(i, (int)curTool == i);

                ref var info = ref toolInfo[i];
                if (IconButton(info.icon))
                {
                    SubmitMove();
                    curTool = (SelectionTool)i;
                }
                ImGui.SetItemTooltip(info.name);

                group.EndButton();
            }
            ImGui.PopStyleVar();
        }

        // operator mode options
        ImGui.SameLine();
        using (var group = ImGuiExt.ButtonGroup.Begin("Operator Mode", 4, 0))
        {
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0f, 0f));
            for (int i = 0; i < operatorInfo.Length; i++)
            {
                group.BeginButton(i, (int)(curOpOverride ?? curOp) == i);

                ref var info = ref operatorInfo[i];
                if (IconButton(info.icon))
                {
                    curOp = (SelectionOperator)i;
                }
                ImGui.SetItemTooltip(info.name);

                group.EndButton();
            }
            ImGui.PopStyleVar();
        }

        ImGui.SameLine();
        if (ImGui.Button("Apply") || EditorWindow.IsKeyPressed(ImGuiKey.Enter))
        {
            SubmitMove();
            Active = false;
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Cancel") || EditorWindow.IsKeyPressed(ImGuiKey.Escape))
        {
            CancelMove();
            Active = false;
        }
    }

    public void Update(int layer)
    {
        // TODO: crosshair cursor
        if (PasteMode)
        {
            curTool = SelectionTool.MoveSelected;
        }
        
        var view = RainEd.Instance.LevelView;
        view.Renderer.OverlayAffectTiles = AffectTiles;

        curOpOverride = null;
        if (EditorWindow.IsKeyDown(ImGuiKey.ModShift))
        {
            curOpOverride = SelectionOperator.Add;
        }

        if (curTool == SelectionTool.MagicWand)
        {
            if (view.IsViewportHovered && EditorWindow.IsMouseClicked(ImGuiMouseButton.Left))
            {
                if (MagicWand(
                    view.MouseCx, view.MouseCy, layer,
                    out int minX, out int minY, out int maxX, out int maxY,
                    out bool[,] mask
                ))
                {
                    CombineMasks(minX, minY, maxX, maxY, mask);
                }
                else selectionActive = false;
            }
        }
        else if (curTool == SelectionTool.TileSelect)
        {
            if (view.IsViewportHovered && EditorWindow.IsMouseClicked(ImGuiMouseButton.Left))
            {
                if (TileSelect(
                    view.MouseCx, view.MouseCy, layer,
                    out int minX, out int minY, out int maxX, out int maxY,
                    out bool[,] mask
                ))
                {
                    CombineMasks(minX, minY, maxX, maxY, mask);
                }
                else selectionActive = false;
            }
        }
        else
        {
            if (view.IsViewportHovered && EditorWindow.IsMouseDragging(ImGuiMouseButton.Left))
            {
                if (!mouseWasDragging)
                {
                    mouseDragState = curTool switch
                    {
                        SelectionTool.Rect => new RectDragState(view.MouseCx, view.MouseCy),
                        SelectionTool.Lasso => new LassoDragState(view.MouseCx, view.MouseCy),
                        SelectionTool.MoveSelection => new SelectionMoveDragState(this, view.MouseCx, view.MouseCy),
                        SelectionTool.MoveSelected => new SelectedMoveDragState(this, view.MouseCx, view.MouseCy),
                        _ => throw new UnreachableException("Invalid curTool")
                    };

                    if ((curOpOverride ?? curOp) == SelectionOperator.Replace && mouseDragState is ISelectionTool) selectionActive = false;
                }

                mouseDragState!.Update(view.MouseCx, view.MouseCy);
            }
            else if (mouseWasDragging && mouseDragState is not null)
            {
                if (mouseDragState is ISelectionTool selTool)
                {
                    if (selTool.ApplySelection(out int minX, out int minY, out int maxX, out int maxY, out bool[,] mask))
                    {
                        CombineMasks(minX, minY, maxX, maxY, mask);
                    }
                    else selectionActive = false;
                }
            }
        }

        mouseWasDragging = EditorWindow.IsMouseDragging(ImGuiMouseButton.Left);

        // draw
        Raylib.BeginShaderMode(Shaders.OutlineMarqueeShader);

        Shaders.OutlineMarqueeShader.GlibShader.SetUniform("time", (float)Raylib.GetTime());
        RainEd.Instance.NeedScreenRefresh();

        // draw selection outline
        if (selectionActive)
        {
            var w = selectionMaxX - selectionMinX + 1;
            var h = selectionMaxY - selectionMinY + 1;
            Debug.Assert(w > 0 && h > 0);

            for (int y = 0; y < h; y++)
            {
                var gy = selectionMinY + y;
                for (int x = 0; x < w; x++)
                {
                    if (!selectionMask[y,x]) continue;
                    var gx = selectionMinX + x;

                    bool left = x == 0 || !selectionMask[y,x-1];
                    bool right = x == w-1 || !selectionMask[y,x+1];
                    bool top = y == 0 || !selectionMask[y-1,x];
                    bool bottom = y == h-1 || !selectionMask[y+1,x];

                    if (left) Raylib.DrawLine(
                        gx * Level.TileSize,
                        gy * Level.TileSize,
                        gx * Level.TileSize,
                        (gy+1) * Level.TileSize,
                        Color.White
                    );

                    if (right) Raylib.DrawLine(
                        (gx+1) * Level.TileSize,
                        gy * Level.TileSize,
                        (gx+1) * Level.TileSize,
                        (gy+1) * Level.TileSize,
                        Color.White
                    );

                    if (top) Raylib.DrawLine(
                        gx * Level.TileSize,
                        gy * Level.TileSize,
                        (gx+1) * Level.TileSize,
                        gy * Level.TileSize,
                        Color.White
                    );

                    if (bottom) Raylib.DrawLine(
                        gx * Level.TileSize,
                        (gy+1) * Level.TileSize,
                        (gx+1) * Level.TileSize,
                        (gy+1) * Level.TileSize,
                        Color.White
                    );
                }
            }
        }

        Raylib.EndShaderMode();

        // copy
        // (paste is handled by GeometryEditor, since paste can be done without first entering selection mode)
        if (KeyShortcuts.Activated(KeyShortcut.Copy) && selectionActive)
        {
            CopySelectedGeometry();
        }
    }

    private void CopySelectedGeometry()
    {
        if (!selectionActive) return;

        var selW = selectionMaxX - selectionMinX + 1;
        var selH = selectionMaxY - selectionMinY + 1;
        (bool mask, LevelCell cell)[,,] geometryData;

        if (movingGeometry is not null)
        {
            geometryData = movingGeometry;
        }
        else
        {
            geometryData = MakeCellGroup(out selW, out selH, false);
        }

        var serializedData = CellSerialization.SerializeCells(selectionMinX, selectionMinY, selW, selH, geometryData);
        Platform.SetClipboard(Boot.Window, Platform.ClipboardDataType.LevelCells, serializedData);
    }

    public bool PasteGeometry(byte[] serializedData)
    {
        SubmitMove();

        var data = CellSerialization.DeserializeCells(serializedData, out int origX, out int origY, out int width, out int height);
        if (data is null) return false;

        // set selection data
        selectionActive = true;
        selectionMinX = origX;
        selectionMinY = origY;
        selectionMaxX = origX + width - 1;
        selectionMaxY = origY + height - 1;
        selectionMask = new bool[height,width];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                selectionMask[y,x] = data[0,x,y].mask;
            }
        }

        // set move data
        movingGeometry = data;

        // send overlay to renderer
        var rndr = RainEd.Instance.LevelView.Renderer;
        rndr.OverlayX = origX;
        rndr.OverlayY = origY;
        rndr.SetOverlay(
            width: width,
            height: height,
            geometry: movingGeometry
        );

        //File.WriteAllBytes("test.rwc", serializedData);
        return true;
    }

    public static void BeginPaste(ref CellSelection? inst)
    {
        if (Platform.GetClipboard(Boot.Window, Platform.ClipboardDataType.LevelCells, out var serializedCells))
        {
            inst ??= new CellSelection()
            {
                PasteMode = true
            };
            inst.curTool = SelectionTool.MoveSelected;
            inst.PasteGeometry(serializedCells);
        }
    }

    private void CombineMasks(int minX, int minY, int maxX, int maxY, bool[,] mask)
    {
        var oldMinX = selectionMinX;
        var oldMinY = selectionMinY;
        var oldMaxX = selectionMaxX;
        var oldMaxY = selectionMaxY;
        var oldMask = selectionMask;
        var op = curOpOverride ?? curOp;

        switch (op)
        {
            case SelectionOperator.Replace:
            case SelectionOperator.Add:
                if (op == SelectionOperator.Replace || !selectionActive)
                {
                    selectionActive = true;
                    selectionMinX = minX;
                    selectionMinY = minY;
                    selectionMaxX = maxX;
                    selectionMaxY = maxY;
                    selectionMask = mask;
                }
                else if (op == SelectionOperator.Add)
                {
                    selectionActive = true;
                    selectionMinX = Math.Min(oldMinX, minX);
                    selectionMinY = Math.Min(oldMinY, minY);
                    selectionMaxX = Math.Max(oldMaxX, maxX);
                    selectionMaxY = Math.Max(oldMaxY, maxY);
                    selectionMask = new bool[selectionMaxY - selectionMinY + 1, selectionMaxX - selectionMinX + 1];

                    // source
                    int ox = oldMinX - selectionMinX;
                    int oy = oldMinY - selectionMinY;
                    int w = oldMaxX - oldMinX + 1;
                    int h = oldMaxY - oldMinY + 1;
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            var gx = x + ox;
                            var gy = y + oy;
                            selectionMask[gy,gx] = oldMask[y,x];
                        }
                    }

                    // dest
                    ox = minX - selectionMinX;
                    oy = minY - selectionMinY;
                    w = maxX - minX + 1;
                    h = maxY - minY + 1;
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            var gx = x + ox;
                            var gy = y + oy;
                            selectionMask[gy,gx] |= mask[y,x];
                        }
                    }
                }

                break;

            case SelectionOperator.Subtract:
            {
                if (!selectionActive) break;
                selectionActive = true;

                // dest
                var oldW = oldMaxX - oldMinX + 1;
                var oldH = oldMaxY - oldMinY + 1;
                var ox = selectionMinX - minX;
                var oy = selectionMinY - minY;
                var w = maxX - minX + 1;
                var h = maxY - minY + 1;

                // in source bounds
                for (int y = 0; y < oldH; y++)
                {
                    for (int x = 0; x < oldW; x++)
                    {
                        var lx = x + ox;
                        var ly = y + oy;
                        if (lx >= 0 && ly >= 0 && lx < w && ly < h)
                        {
                            // A  B  OUT
                            // 0  0  0
                            // 0  1  0
                            // 1  0  1
                            // 1  1  0
                            selectionMask[y,x] &= selectionMask[y,x] ^ mask[ly,lx];
                        }
                    }
                }

                CropSelection();
                break;
            }

            case SelectionOperator.Intersect:
            {
                if (!selectionActive) break;
                selectionActive = true;

                selectionMinX = Math.Max(oldMinX, minX);
                selectionMinY = Math.Max(oldMinY, minY);
                selectionMaxX = Math.Min(oldMaxX, maxX);
                selectionMaxY = Math.Min(oldMaxY, maxY);
                if (selectionMaxX < selectionMinX || selectionMaxY < selectionMinY)
                {
                    selectionActive = false;
                    break;
                }

                selectionMask = new bool[selectionMaxY - selectionMinY + 1, selectionMaxX - selectionMinX + 1];
                
                // source
                var ox0 = selectionMinX - oldMinX;
                var oy0 = selectionMinY - oldMinY;
                var w0 = oldMaxX - oldMinX + 1;
                var h0 = oldMaxY - oldMinY + 1;

                // dest
                var ox1 = selectionMinX - minX;
                var oy1 = selectionMinY - minY;
                var w1 = maxX - minX + 1;
                var h1 = maxY - minY + 1;

                // in dest bounds
                var newW = selectionMaxX - selectionMinX + 1;
                var newH = selectionMaxY - selectionMinY + 1;
                for (int y = 0; y < newH; y++)
                {
                    for (int x = 0; x < newW; x++)
                    {
                        var x0 = x + ox0;
                        var y0 = y + oy0;
                        var x1 = x + ox1;
                        var y1 = y + oy1;

                        if (!(x0 >= 0 && y0 >= 0 && x1 < w0 && y1 < h0)) continue;
                        if (!(x1 >= 0 && y1 >= 0 && x1 < w1 && y1 < h1)) continue;
                        selectionMask[y,x] = oldMask[y0,x0] & mask[y1,x1];
                    }
                }

                CropSelection();
                break;
            }
        }
    }

    private static bool TileSelect(int mouseX, int mouseY, int layer, out int p_minX, out int p_minY, out int p_maxX, out int p_maxY, out bool[,] mask)
    {
        var level = RainEd.Instance.Level;
        p_minX = int.MaxValue;
        p_minY = int.MaxValue;
        p_maxX = int.MinValue;
        p_maxY = int.MinValue;

        if (level.Layers[layer, mouseX, mouseY].HasTile())
        {
            var tileHeadPos = level.GetTileHead(layer, mouseX, mouseY);
            Assets.Tile? tile = level.Layers[tileHeadPos.Layer, tileHeadPos.X, tileHeadPos.Y].TileHead;
            if (tile is null)
            {
                mask = new bool[0,0];
                return false;
            }

            p_minX = tileHeadPos.X - tile.CenterX;
            p_minY = tileHeadPos.Y - tile.CenterY;
            p_maxX = p_minX + tile.Width - 1;
            p_maxY = p_minY + tile.Height - 1;
            mask = new bool[tile.Height, tile.Width];
            mask[tile.CenterY, tile.CenterX] = true;

            for (int x = 0; x < tile.Width; x++)
            {
                for (int y = 0; y < tile.Height; y++)
                {
                    for (int l = 0; l < Level.LayerCount; l++)
                    {
                        ref var c = ref level.Layers[l, x + p_minX, y + p_minY];
                        if (c.TileRootX == tileHeadPos.X && c.TileRootY == tileHeadPos.Y && c.TileLayer == tileHeadPos.Layer)
                        {
                            mask[y,x] = true;
                        }
                    }
                }
            }

            return true;
        }
        else
        {
            mask = new bool[0,0];
            return false;
        }
    }

    private static bool MagicWand(int mouseX, int mouseY, int layer, out int p_minX, out int p_minY, out int p_maxX, out int p_maxY, out bool[,] mask)
    {
        var level = RainEd.Instance.Level;
        p_minX = int.MaxValue;
        p_minY = int.MaxValue;
        p_maxX = int.MinValue;
        p_maxY = int.MinValue;
        if (!level.IsInBounds(mouseX, mouseY)) 
        {
            mask = new bool[0,0];
            return false;
        }

        var levelMask = new bool[level.Height,level.Width];

        bool isSolidGeo(int x, int y)
        {
            return level.Layers[layer, x, y].Geo is
                GeoType.Solid or
                GeoType.SlopeRightUp or
                GeoType.SlopeLeftUp or
                GeoType.SlopeRightDown or
                GeoType.SlopeLeftDown or
                GeoType.Platform;
        }
        bool selectGeo = isSolidGeo(mouseX, mouseY);

        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        var hasValue = false;
        bool success = Rasterization.FloodFill(
            mouseX, mouseY, level.Width, level.Height,
            isSimilar: (int x, int y) =>
            {
                return isSolidGeo(x, y) == selectGeo && !levelMask[y,x];
                //return false;
            },
            plot: (int x, int y) =>
            {
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
                levelMask[y,x] = true;
                hasValue = true;
            }
        );

        if (!success)
        {
            EditorWindow.ShowNotification("Magic wand selection too large!");
            mask = new bool[0,0];
            return false;
        }

        if (!hasValue)
        {
            mask = new bool[0,0];
            p_minX = mouseX;
            p_minY = mouseY;
            p_maxX = mouseX;
            p_maxY = mouseY;
            return false;
        }

        var aabbW = maxX - minX + 1;
        var aabbH = maxY - minY + 1;
        p_minX = minX;
        p_minY = minY;
        p_maxX = maxX;
        p_maxY = maxY;
        mask = new bool[aabbH,aabbW];

        for (int y = 0; y < aabbH; y++)
        {
            for (int x = 0; x < aabbW; x++)
            {
                var gx = minX + x;
                var gy = minY + y;
                mask[y,x] = levelMask[gy,gx];
            }
        }

        return true;
    }

    public void CropSelection()
    {
        if (!selectionActive) return;

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;
        bool hasValue = false;

        for (int gy = selectionMinY; gy <= selectionMaxY; gy++)
        {
            for (int gx = selectionMinX; gx <= selectionMaxX; gx++)
            {
                var x = gx - selectionMinX;
                var y = gy - selectionMinY;
                if (selectionMask[y,x])
                {
                    hasValue = true;
                    minX = Math.Min(minX, gx);
                    minY = Math.Min(minY, gy);
                    maxX = Math.Max(maxX, gx);
                    maxY = Math.Max(maxY, gy);
                }
            }
        }

        if (!hasValue)
        {
            selectionActive = false;
            return;
        }

        var newW = maxX - minX + 1;
        var newH = maxY - minY + 1;
        var newMask = new bool[newH, newW];

        for (int y = 0; y < newH; y++)
        {
            for (int x = 0; x < newW; x++)
            {
                var lx = x + minX - selectionMinX;
                var ly = y + minY - selectionMinY;
                newMask[y,x] = selectionMask[ly,lx];
            }
        }

        selectionMinX = minX;
        selectionMinY = minY;
        selectionMaxX = maxX;
        selectionMaxY = maxY;
        selectionMask = newMask;
    }

    public void SubmitMove()
    {
        cancelGeoData = null;
        if (movingGeometry is null)
            return;

        var level = RainEd.Instance.Level;
        var rndr = RainEd.Instance.LevelView.Renderer;
        var selW = selectionMaxX - selectionMinX + 1;
        var selH = selectionMaxY - selectionMinY + 1;

        RainEd.Instance.LevelView.CellChangeRecorder.BeginChange();

        // apply moved geometry
        for (int y = 0; y < selH; y++)
        {
            var gy = rndr.OverlayY + y;
            for (int x = 0; x < selW; x++)
            {
                var gx = rndr.OverlayX + x;
                for (int l = 0; l < Level.LayerCount; l++)
                {
                    ref var srcCell = ref movingGeometry[l,x,y];
                    if (!srcCell.mask) continue;

                    ref var dstCell = ref level.Layers[l,gx,gy];
                    dstCell.Geo = srcCell.cell.Geo;
                    dstCell.Objects = srcCell.cell.Objects;
                    dstCell.Material = srcCell.cell.Material;

                    if (AffectTiles)
                    {
                        if (srcCell.cell.TileHead is not null)
                        {
                            dstCell.TileHead = srcCell.cell.TileHead;
                            dstCell.TileRootX = gx;
                            dstCell.TileRootY = gy;
                            dstCell.TileLayer = l;
                            rndr.InvalidateTileHead(gx, gy, l);
                        }
                        else if (srcCell.cell.HasTile())
                        {
                            dstCell.TileRootX = srcCell.cell.TileRootX + rndr.OverlayX;
                            dstCell.TileRootY = srcCell.cell.TileRootY + rndr.OverlayY;
                            dstCell.TileLayer = srcCell.cell.TileLayer;
                        }
                    }

                    rndr.InvalidateGeo(gx, gy, l);
                    if (l == 0)
                        RainEd.Instance.CurrentTab!.NodeData.InvalidateCell(gx, gy);
                }
            }
        }

        movingGeometry = null;
        rndr.ClearOverlay();

        RainEd.Instance.LevelView.CellChangeRecorder.PushChange();
    }

    public void CancelMove()
    {
        if (movingGeometry is null)
            return;
        
        movingGeometry = null;
        RainEd.Instance.LevelView.Renderer.ClearOverlay();

        if (cancelGeoData is not null)
        {
            var level = RainEd.Instance.Level;
            var renderer = RainEd.Instance.LevelView.Renderer;
            var nodeData = RainEd.Instance.CurrentTab!.NodeData;

            var selW = selectionMaxX - selectionMinX + 1;
            var selH = selectionMaxY - selectionMinY + 1;
            for (int y = 0; y < selH; y++)
            {
                var gy = cancelOrigY + y;
                for (int x = 0; x < selW; x++)
                {
                    var gx = cancelOrigX + x;
                    for (int l = 0; l < Level.LayerCount; l++)
                    {
                        if (!level.IsInBounds(gx, gy)) continue;
                        if (!cancelGeoData[l,x,y].mask) continue;
                        level.Layers[l,gx,gy] = cancelGeoData[l,x,y].cell;

                        renderer.InvalidateGeo(gx, gy, l);
                        if (l == 0) nodeData.InvalidateCell(gx, gy);
                        if (level.Layers[l,gx,gy].TileHead is not null)
                            renderer.InvalidateTileHead(gx, gy, l);
                    }
                }
            }

            cancelGeoData = null;
        }
    }

    private (bool mask, LevelCell cell)[,,] MakeCellGroup(out int selW, out int selH, bool eraseSource)
    {
        selW = selectionMaxX - selectionMinX + 1;
        selH = selectionMaxY - selectionMinY + 1;
        var level = RainEd.Instance.Level;
        var renderer = RainEd.Instance.LevelView.Renderer;

        if (eraseSource)
        {
            RainEd.Instance.LevelView.CellChangeRecorder.BeginChange();
        }

        var geometry = new (bool mask, LevelCell cell)[Level.LayerCount, selW, selH];
        for (int y = 0; y < selH; y++)
        {
            var gy = selectionMinY + y;
            for (int x = 0; x < selW; x++)
            {
                var gx = selectionMinX + x;

                for (int l = 0; l < Level.LayerCount; l++)
                {
                    ref var srcCell = ref level.Layers[l,gx,gy];
                    ref var dstCell = ref geometry[l,x,y];
                    dstCell.mask = selectionMask[y,x];
                    dstCell.cell = srcCell;

                    // change tile head references to be relative to the origin
                    // of the overlay
                    if (dstCell.cell.HasTile() && dstCell.cell.TileHead is null)
                    {
                        dstCell.cell.TileRootX -= selectionMinX;
                        dstCell.cell.TileRootY -= selectionMinY;

                        // if the tile head is outside of the selection,
                        // then erase this tile body.
                        var tx = dstCell.cell.TileRootX;
                        var ty = dstCell.cell.TileRootY;
                        if (tx < 0 || ty < 0 ||
                            tx >= selW || ty >= selH ||
                            !selectionMask[ty, tx]
                        )
                        {
                            dstCell.cell.TileRootX = -1;
                            dstCell.cell.TileRootY = -1;
                            dstCell.cell.TileLayer = -1;
                        }
                    }

                    if (eraseSource && selectionMask[y,x])
                    {
                        srcCell.Geo = GeoType.Air;
                        srcCell.Objects = LevelObject.None;
                        srcCell.Material = 0;

                        if (AffectTiles)
                        {
                            bool hadHead = srcCell.TileHead is not null;
                            srcCell.TileRootX = -1;
                            srcCell.TileRootY = -1;
                            srcCell.TileLayer = -1;
                            srcCell.TileHead = null;

                            if (hadHead)
                                renderer.InvalidateTileHead(gx, gy, l);
                        }

                        renderer.InvalidateGeo(gx, gy, l);
                        if (l == 0)
                            RainEd.Instance.CurrentTab!.NodeData.InvalidateCell(gx, gy);
                    }
                }
            }
        }

        if (eraseSource)
        {
            RainEd.Instance.LevelView.CellChangeRecorder.PushChange();
        }

        return geometry;
    }

    private void BeginMove()
    {
        // create separate copy of selected cells in order for the Cancel operation
        // to work properly. can't use movingGeometry because it may modify the
        // data of cells (i.e. tile head references)
        var level = RainEd.Instance.Level;
        var selW = selectionMaxX - selectionMinX + 1;
        var selH = selectionMaxY - selectionMinY + 1;
        cancelGeoData = new (bool mask, LevelCell cell)[Level.LayerCount, selW, selH];
        cancelOrigX = selectionMinX;
        cancelOrigY = selectionMinY;

        for (int y = 0; y < selH; y++)
        {
            var gy = cancelOrigY + y;
            for (int x = 0; x < selW; x++)
            {
                var gx = cancelOrigX + x;
                for (int l = 0; l < Level.LayerCount; l++)
                {
                    cancelGeoData[l,x,y] = (selectionMask[y,x], level.IsInBounds(gx, gy) ? level.Layers[l,gx,gy] : new LevelCell());
                }
            }
        }

        var renderer = RainEd.Instance.LevelView.Renderer;
        movingGeometry = MakeCellGroup(out int overlayW, out int overlayH, true);

        // send it to geo renderer
        renderer.SetOverlay(
            width: overlayW,
            height: overlayH,
            geometry: movingGeometry
        );
    }

    class RectDragState : Tool, ISelectionTool
    {
        private int selectionStartX = -1;
        private int selectionStartY = -1;
        private int mouseMinX = 0;
        private int mouseMaxX = 0;
        private int mouseMinY = 0;
        private int mouseMaxY = 0;

        public RectDragState(int startX, int startY)
        {
            selectionStartX = startX;
            selectionStartY = startY;
        }

        public override void Update(int mouseX, int mouseY)
        {
            mouseMinX = Math.Min(selectionStartX, mouseX);
            mouseMaxX = Math.Max(selectionStartX, mouseX);
            mouseMinY = Math.Min(selectionStartY, mouseY);
            mouseMaxY = Math.Max(selectionStartY, mouseY);
            var w = mouseMaxX - mouseMinX + 1;
            var h = mouseMaxY - mouseMinY + 1;

            Raylib.DrawRectangleLines(
                mouseMinX * Level.TileSize,
                mouseMinY * Level.TileSize,
                w * Level.TileSize,
                h * Level.TileSize,
                Color.White
            );
        }
        
        public bool ApplySelection(out int minX, out int minY, out int maxX, out int maxY, out bool[,] mask)
        {
            minX = mouseMinX;
            maxX = mouseMaxX;
            minY = mouseMinY;
            maxY = mouseMaxY;

            var w = maxX - minX + 1;
            var h = maxY - minY + 1;
            mask = new bool[h,w];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    mask[y,x] = true;
                }
            }

            return true;
        }
    }

    class LassoDragState : Tool, ISelectionTool
    {
        private List<Vector2i> points = [];

        public LassoDragState(int startX, int startY)
        {
            points.Add(new Vector2i(startX, startY));
        }
        
        public override void Update(int mouseX, int mouseY)
        {
            var newPoint = new Vector2i(mouseX, mouseY);
            if (newPoint != points[^1])
            {
                //Rasterization.Bresenham(points[^1].X, points[^1].Y, newPoint.X, newPoint.Y, (x, y) =>
                //{
                    points.Add(newPoint);
                //});
            }

            // draw points
            var rctx = RainEd.RenderContext;
            rctx.UseGlLines = true;
            rctx.DrawColor = Glib.Color.White;

            for (int i = 1; i < points.Count; i++)
            {
                var ptA = points[i-1];
                var ptB = points[i];

                rctx.DrawLine(
                    (ptA.X + 0.5f) * Level.TileSize,
                    (ptA.Y + 0.5f) * Level.TileSize,
                    (ptB.X + 0.5f) * Level.TileSize,
                    (ptB.Y + 0.5f) * Level.TileSize
                );
            }

            {
                var ptA = points[^1];
                var ptB = points[0];

                rctx.DrawLine(
                    (ptA.X + 0.5f) * Level.TileSize,
                    (ptA.Y + 0.5f) * Level.TileSize,
                    (ptB.X + 0.5f) * Level.TileSize,
                    (ptB.Y + 0.5f) * Level.TileSize
                );
            }

            /*var lastX = 0;
            var lastY = 0;
            bool first = true;
            Rasterization.Bresenham(points[^1].X, points[^1].Y, points[0].X, points[0].Y, (x, y) =>
            {
                if (!first)
                {
                    rctx.DrawLine(
                        (lastX + 0.5f) * Level.TileSize,
                        (lastY + 0.5f) * Level.TileSize,
                        (x + 0.5f) * Level.TileSize,
                        (y + 0.5f) * Level.TileSize
                    );
                }

                first = false;
                lastX = x;
                lastY = y;
            });*/
        }

        public bool ApplySelection(out int p_minX, out int p_minY, out int p_maxX, out int p_maxY, out bool[,] p_mask)
        {
            // calc bounding box of points
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            var maxX = int.MinValue;
            var maxY = int.MinValue;

            if (points.Count == 0)
            {
                p_minX = 0;
                p_minY = 0;
                p_maxX = 0;
                p_maxY = 0;
                p_mask = new bool[0,0];
                return false;
            }

            foreach (var pt in points)
            {
                minX = Math.Min(pt.X, minX);
                minY = Math.Min(pt.Y, minY);
                maxX = Math.Max(pt.X, maxX);
                maxY = Math.Max(pt.Y, maxY);
            }

            var w = maxX - minX + 1;
            var h = maxY - minY + 1;
            var mask = new bool[h,w];

            // polygon rasterization:
            // a point is inside a polygon if, given an infinitely long horizontal
            // line that intersects with the point, the line intersects with the polygon's
            // edges an even number of times.
            // so, to rasterize a polygon, for each scanline we can cast a horizontal ray from
            // the left side of the polygon's AABB to the right side, and then move along each
            // point along that ray. if so far, that ray intersected with an even number of edges,
            // draw a pixel.
            
            // gather lines
            var lines = new List<(Vector2 a, Vector2 b)>();
            for (int i = 1; i < points.Count; i++)
            {
                var ptA = (Vector2) points[i-1];
                var ptB = (Vector2) points[i];
                lines.Add((ptA, ptB));
            }

            if (points[^1] != points[0])
                lines.Add(((Vector2) points[^1], (Vector2) points[0]));
            
            // this function casts a ray towards the right from (rx, ry)
            // and fills the distances array to the distance from the ray
            // to each intersectin segment. it is also sorted.
            List<float> distances = [];
            void CalcIntersections(float rx, float ry)
            {
                distances.Clear();
                var gy = ry;
                var gx = rx;
                foreach (var (ptA, ptB) in lines)
                {
                    var lineMinY = Math.Min(ptA.Y, ptB.Y);
                    var lineMaxY = Math.Max(ptA.Y, ptB.Y);
                    if (!(gy >= lineMinY && gy <= lineMaxY)) continue;
                    
                    float dist;
                    if (ptA.X == ptB.X) // line is completely vertical
                    {
                        dist = ptA.X - gx;
                    }
                    else
                    {
                        var slope = (ptB.Y - ptA.Y) / (ptB.X - ptA.X);
                        if (slope == 0) continue; // line is parallel with ray
                        dist = (gy - ptA.Y) / slope + ptA.X - gx;
                    }

                    distances.Add(dist);
                }
                distances.Sort();
            }

            // use this distances array to fill a scanline
            // we don't actually need to keep track of even/odd intersections,
            // we just iterate through each pair of intersections in the list.
            // it does the same thing.
            void Scanline(int y)
            {
                for (int i = 0; i < distances.Count; i += 2)
                {
                    var xEnd = i+1 < distances.Count ? (int)distances[i+1] : w;
                    for (int x = (int)distances[i]; x < xEnd; x++)
                    {
                        mask[y,x] = true;
                    }
                }
            }

            for (int y = 0; y < h; y++)
            {
                // check for all four corners of each pixel in the polygon, not just one.
                // otherwise, the selection mask will be "offset".
                CalcIntersections(minX, y + minY + 0.05f); // top-left
                Scanline(y);
                CalcIntersections(minX - 1.0f, y + minY + 0.05f); // top-right
                Scanline(y);
                CalcIntersections(minX, y + minY - 0.05f); // bottom-left
                Scanline(y);
                CalcIntersections(minX - 1.0f, y + minY - 0.05f); // bottom-right
                Scanline(y);
            }

            p_minX = minX;
            p_minY = minY;
            p_maxX = maxX;
            p_maxY = maxY;
            p_mask = mask;
            return true;
        }
    }

    class SelectionMoveDragState : Tool
    {
        private readonly int offsetX;
        private readonly int offsetY;
        private readonly int selW, selH;
        private readonly CellSelection controller;

        public SelectionMoveDragState(CellSelection controller, int startX, int startY)
        {
            this.controller = controller;
            offsetX = startX - controller.selectionMinX;
            offsetY = startY - controller.selectionMinY;
            selW = controller.selectionMaxX - controller.selectionMinX + 1;
            selH = controller.selectionMaxY - controller.selectionMinY + 1;
        }

        public override void Update(int mouseX, int mouseY)
        {
            controller.selectionMinX = mouseX - offsetX;
            controller.selectionMinY = mouseY - offsetY;
            controller.selectionMaxX = controller.selectionMinX + selW - 1;
            controller.selectionMaxY = controller.selectionMinY + selH - 1;
        }
    }

    class SelectedMoveDragState : Tool
    {
        private readonly int offsetX;
        private readonly int offsetY;
        private readonly int selW, selH;
        private readonly CellSelection controller;

        public SelectedMoveDragState(CellSelection controller, int startX, int startY)
        {
            this.controller = controller;
            offsetX = startX - controller.selectionMinX;
            offsetY = startY - controller.selectionMinY;
            selW = controller.selectionMaxX - controller.selectionMinX + 1;
            selH = controller.selectionMaxY - controller.selectionMinY + 1;
            var level = RainEd.Instance.Level;
            var renderer = RainEd.Instance.LevelView.Renderer;

            // create geometry overlay array
            // and clear out selection
            if (controller.movingGeometry is null)
            {
                controller.BeginMove();
            }
        }

        public override void Update(int mouseX, int mouseY)
        {
            var rndr = RainEd.Instance.LevelView.Renderer;
            rndr.OverlayX = mouseX - offsetX;
            rndr.OverlayY = mouseY - offsetY;
            controller.selectionMinX = rndr.OverlayX;
            controller.selectionMinY = rndr.OverlayY;
            controller.selectionMaxX = controller.selectionMinX + selW - 1;
            controller.selectionMaxY = controller.selectionMinY + selH - 1;
        }
    }
}