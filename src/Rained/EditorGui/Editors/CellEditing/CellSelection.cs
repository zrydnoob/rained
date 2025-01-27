namespace Rained.EditorGui.Editors.CellEditing;
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
    public LayerSelection?[] Selections => selections;
    public bool IsGeometryMoveActive => movingGeometry is not null;

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

    private readonly LayerSelection?[] selections = new LayerSelection?[Level.LayerCount];
    private readonly LayerSelection?[] tmpSelections = new LayerSelection?[Level.LayerCount];

    private int movingW = 0;
    private int movingH = 0;
    private (bool mask, LevelCell cell)[,,]? movingGeometry = null;

    private int cancelOrigX = 0;
    private int cancelOrigY = 0;
    private (bool mask, LevelCell cell)[,,]? cancelGeoData = null;

    // used for mouse drag
    private bool mouseWasDragging = false;
    private SelectToolState? mouseDragState = null;

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
            if (ImGui.Button("OK") || EditorWindow.IsKeyPressed(ImGuiKey.Enter))
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
        if (ImGui.Button("Done") || EditorWindow.IsKeyPressed(ImGuiKey.Enter))
        {
            SubmitMove();
            Active = false;
        }
        
        ImGui.SameLine();
        ImGui.BeginDisabled(movingGeometry is null);
        if (ImGui.Button("Cancel"))
        {
            CancelMove();
            ClearSelection();
        }
        ImGui.EndDisabled();

        if (EditorWindow.IsKeyPressed(ImGuiKey.Escape))
        {
            bool doExit = movingGeometry is null;
            CancelMove();
            ClearSelection();
            if (doExit) Active = false;
        }
    }

    public void Update(ReadOnlySpan<bool> layerMask, int activeLayer)
    {
        // TODO: crosshair cursor
        Debug.Assert(layerMask.Length == Level.LayerCount);

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

        var activeOp = curOpOverride ?? curOp;
        if (curTool == SelectionTool.MagicWand)
        {
            if (view.IsViewportHovered && EditorWindow.IsMouseClicked(ImGuiMouseButton.Left))
            {
                var layerSelection = StaticSelectionTools.MagicWand(view.MouseCx, view.MouseCy, activeLayer);
                if (layerSelection is not null)
                {
                    Array.Fill(tmpSelections, null);
                    tmpSelections[activeLayer] = layerSelection;
                    CombineMasks(tmpSelections);
                }
                else if (activeOp is SelectionOperator.Replace or SelectionOperator.Intersect) selections[activeLayer] = null;
            }
        }
        else if (curTool == SelectionTool.TileSelect)
        {
            if (view.IsViewportHovered && EditorWindow.IsMouseClicked(ImGuiMouseButton.Left))
            {
                Array.Fill(tmpSelections, null);
                if (StaticSelectionTools.TileSelect(
                    view.MouseCx, view.MouseCy, activeLayer, tmpSelections
                ))
                {
                    CombineMasks(tmpSelections);
                }
                else if (activeOp is SelectionOperator.Replace or SelectionOperator.Intersect) ClearSelection();
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
                        SelectionTool.MoveSelection => new SelectionMoveDragState(this, view.MouseCx, view.MouseCy, layerMask),
                        SelectionTool.MoveSelected => new SelectedMoveDragState(this, view.MouseCx, view.MouseCy, layerMask),
                        _ => throw new UnreachableException("Invalid curTool")
                    };

                    if (activeOp is SelectionOperator.Replace or SelectionOperator.Intersect && mouseDragState is IApplySelection)
                        ClearSelection(layerMask);
                }

                mouseDragState!.Update(view.MouseCx, view.MouseCy, layerMask);
            }
            else if (mouseWasDragging && mouseDragState is not null)
            {
                if (mouseDragState is IApplySelection selTool)
                {
                    Array.Fill(tmpSelections, null);
                    if (selTool.ApplySelection(tmpSelections, layerMask))
                    {
                        CombineMasks(tmpSelections);
                    }
                    else if (activeOp is SelectionOperator.Replace or SelectionOperator.Intersect) ClearSelection(layerMask);
                }
            }
        }

        mouseWasDragging = EditorWindow.IsMouseDragging(ImGuiMouseButton.Left);

        // update layer colors
        Span<Color> layerColors = stackalloc Color[3];
        {
            var layerCol1 = RainEd.Instance.Preferences.LayerColor1;
            var layerCol2 = RainEd.Instance.Preferences.LayerColor2;
            var layerCol3 = RainEd.Instance.Preferences.LayerColor3;
            layerColors[0] = new Color(255 - layerCol1.R, 255 - layerCol1.G, 255 - layerCol1.B, (byte)255);
            layerColors[1] = new Color(layerCol2.R, layerCol2.G, layerCol2.B, (byte)255);
            layerColors[2] = new Color(layerCol3.R, layerCol3.G, layerCol3.B, (byte)255);
        }

        // draw
        Raylib.BeginShaderMode(Shaders.OutlineMarqueeShader);

        Shaders.OutlineMarqueeShader.GlibShader.SetUniform("time", (float)Raylib.GetTime());
        RainEd.Instance.NeedScreenRefresh();

        // draw selection outline
        bool isSelectionActive = false;
        for (int l = 0; l < Level.LayerCount; l++)
        {
            ref var selection = ref selections[l];
            if (selection is not null)
            {
                isSelectionActive = true;

                var w = selection.maxX - selection.minX + 1;
                var h = selection.maxY - selection.minY + 1;
                Debug.Assert(w > 0 && h > 0);

                var col = layerColors[l];
                int offsetX = l - activeLayer;
                int offsetY = l - activeLayer;

                for (int y = 0; y < h; y++)
                {
                    var gy = selection.minY + y;
                    for (int x = 0; x < w; x++)
                    {
                        if (!selection.mask[y,x]) continue;
                        var gx = selection.minX + x;

                        bool left = x == 0 || !selection.mask[y,x-1];
                        bool right = x == w-1 || !selection.mask[y,x+1];
                        bool top = y == 0 || !selection.mask[y-1,x];
                        bool bottom = y == h-1 || !selection.mask[y+1,x];

                        if (left) Raylib.DrawLine(
                            offsetX + gx * Level.TileSize,
                            offsetY + gy * Level.TileSize,
                            offsetX + gx * Level.TileSize,
                            offsetY + (gy+1) * Level.TileSize,
                            col
                        );

                        if (right) Raylib.DrawLine(
                            offsetX + (gx+1) * Level.TileSize,
                            offsetY + gy * Level.TileSize,
                            offsetX + (gx+1) * Level.TileSize,
                            offsetY + (gy+1) * Level.TileSize,
                            col
                        );

                        if (top) Raylib.DrawLine(
                            offsetX + gx * Level.TileSize,
                            offsetY + gy * Level.TileSize,
                            offsetX + (gx+1) * Level.TileSize,
                            offsetY + gy * Level.TileSize,
                            col
                        );

                        if (bottom) Raylib.DrawLine(
                            offsetX + gx * Level.TileSize,
                            offsetY + (gy+1) * Level.TileSize,
                            offsetX + (gx+1) * Level.TileSize,
                            offsetY + (gy+1) * Level.TileSize,
                            col
                        );
                    }
                }
            }
        }

        Raylib.EndShaderMode();

        // copy
        // (paste is handled by GeometryEditor, since paste can be done without first entering selection mode)
        if (KeyShortcuts.Activated(KeyShortcut.Copy) && isSelectionActive)
        {
            CopySelectedGeometry();
        }
    }

    private void CopySelectedGeometry()
    {
        if (!IsSelectionActive()) return;

        int selX, selY, selW, selH;
        (bool mask, LevelCell cell)[,,] geometryData;

        if (movingGeometry is not null)
        {
            var renderer = RainEd.Instance.LevelView.Renderer;
            geometryData = movingGeometry;
            (selX, selY) = (renderer.OverlayX, renderer.OverlayY);
            (selW, selH) = (movingW, movingH);
        }
        else
        {
            geometryData = MakeCellGroup(out selX, out selY, out selW, out selH, false);
        }

        var serializedData = CellSerialization.SerializeCells(selX, selY, selW, selH, geometryData);
        if (!Platform.SetClipboard(Boot.Window, Platform.ClipboardDataType.LevelCells, serializedData))
        {
            EditorWindow.ShowNotification("Could not copy!");
        }
    }

    public bool PasteGeometry(byte[] serializedData)
    {
        SubmitMove();

        var data = CellSerialization.DeserializeCells(serializedData, out int origX, out int origY, out int width, out int height);
        if (data is null) return false;

        // set selection data
        for (int l = 0; l < Level.LayerCount; l++)
        {
            var selLayer = new LayerSelection(
                minX: origX,
                minY: origY,
                maxX: origX + width - 1,
                maxY: origY + height - 1
            );

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    selLayer.mask[y,x] = data[l,x,y].mask;
                }
            }

            selections[l] = selLayer;
        }
        CropSelection();

        // set move data
        movingGeometry = data;
        movingW = width;
        movingH = height;

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

    private void CombineMasks(ReadOnlySpan<LayerSelection?> dstSelections)
    {
        for (int l = 0; l < Level.LayerCount; l++)
        {
            var srcSel = selections[l];
            var dstSel = dstSelections[l];
            if (dstSel is null) continue;

            var op = curOpOverride ?? curOp;

            switch (op)
            {
                case SelectionOperator.Replace:
                case SelectionOperator.Add:
                    if (op == SelectionOperator.Replace || srcSel is null)
                    {
                        selections[l] = new LayerSelection(
                            dstSel.minX, dstSel.minY,
                            dstSel.maxX, dstSel.maxY,
                            dstSel.mask
                        );
                    }
                    else if (op == SelectionOperator.Add)
                    {
                        var newSel = new LayerSelection(
                            minX: Math.Min(srcSel.minX, dstSel.minX),
                            minY: Math.Min(srcSel.minY, dstSel.minY),
                            maxX: Math.Max(srcSel.maxX, dstSel.maxX),
                            maxY: Math.Max(srcSel.maxY, dstSel.maxY)
                        );

                        // source
                        int ox = srcSel.minX - newSel.minX;
                        int oy = srcSel.minY - newSel.minY;
                        int w = srcSel.maxX - srcSel.minX + 1;
                        int h = srcSel.maxY - srcSel.minY + 1;
                        for (int y = 0; y < h; y++)
                        {
                            var y2 = y + oy;
                            for (int x = 0; x < w; x++)
                            {
                                var x2 = x + ox;
                                newSel.mask[y2,x2] = srcSel.mask[y,x];
                            }
                        }

                        // dest
                        ox = dstSel.minX - newSel.minX;
                        oy = dstSel.minY - newSel.minY;
                        w = dstSel.maxX - dstSel.minX + 1;
                        h = dstSel.maxY - dstSel.minY + 1;
                        for (int y = 0; y < h; y++)
                        {
                            var y2 = y + oy;
                            for (int x = 0; x < w; x++)
                            {
                                var x2 = x + ox;
                                newSel.mask[y2, x2] |= dstSel.mask[y,x];
                            }
                        }

                        selections[l] = newSel;
                    }

                    break;

                case SelectionOperator.Subtract:
                {
                    if (srcSel is null) break;

                    var newSel = new LayerSelection(
                        minX: srcSel.minX,
                        minY: srcSel.minY,
                        maxX: srcSel.maxX,
                        maxY: srcSel.maxY
                    );

                    // dest
                    var oldW = srcSel.maxX - srcSel.minX + 1;
                    var oldH = srcSel.maxY - srcSel.minY + 1;
                    var ox = newSel.minX - dstSel.minX;
                    var oy = newSel.minY - dstSel.minY;
                    var w = dstSel.maxX - dstSel.minX + 1;
                    var h = dstSel.maxY - dstSel.minY + 1;

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
                                newSel.mask[y,x] = srcSel.mask[y,x] & (srcSel.mask[y,x] ^ dstSel.mask[ly,lx]);
                            }
                        }
                    }

                    selections[l] = newSel;
                    break;
                }

                case SelectionOperator.Intersect:
                {
                    if (srcSel is null) break;

                    var newSel = new LayerSelection(
                        minX: int.Max(srcSel.minX, dstSel.minX),
                        minY: int.Max(srcSel.minY, dstSel.minY),
                        maxX: int.Max(srcSel.maxX, dstSel.maxX),
                        maxY: int.Max(srcSel.maxY, dstSel.maxY)
                    );

                    if (newSel.maxX < newSel.minX || newSel.maxY < newSel.minY)
                    {
                        selections[l] = null;
                        break;
                    }
                    
                    // source
                    var ox0 = newSel.minX - srcSel.minX;
                    var oy0 = newSel.minY - srcSel.minY;
                    var w0 = srcSel.maxX - srcSel.minX + 1;
                    var h0 = srcSel.maxY - srcSel.minY + 1;

                    // dest
                    var ox1 = newSel.minX - dstSel.minX;
                    var oy1 = newSel.minY - dstSel.minY;
                    var w1 = dstSel.maxX - dstSel.minX + 1;
                    var h1 = dstSel.maxY - dstSel.minY + 1;

                    // in dest bounds
                    var newW = newSel.maxX - newSel.minX + 1;
                    var newH = newSel.maxY - newSel.minY + 1;
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
                            newSel.mask[y,x] = srcSel.mask[y0,x0] & dstSel.mask[y1,x1];
                        }
                    }

                    selections[l] = newSel;
                    break;
                }
            }
        }

        CropSelection();
    }

    public void CropSelection()
    {
        for (int l = 0; l < Level.LayerCount; l++)
        {
            ref var selection = ref selections[l];
            if (selection is null) continue;

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            bool hasValue = false;

            for (int gy = selection.minY; gy <= selection.maxY; gy++)
            {
                for (int gx = selection.minX; gx <= selection.maxX; gx++)
                {
                    var x = gx - selection.minX;
                    var y = gy - selection.minY;
                    if (selection.mask[y,x])
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
                selection = null;
                return;
            }

            var newW = maxX - minX + 1;
            var newH = maxY - minY + 1;
            var newMask = new bool[newH, newW];

            for (int y = 0; y < newH; y++)
            {
                for (int x = 0; x < newW; x++)
                {
                    var lx = x + minX - selection.minX;
                    var ly = y + minY - selection.minY;
                    newMask[y,x] = selection.mask[ly,lx];
                }
            }

            selection.minX = minX;
            selection.minY = minY;
            selection.maxX = maxX;
            selection.maxY = maxY;
            selection.mask = newMask;
        }
    }

    public void SubmitMove()
    {
        cancelGeoData = null;
        if (movingGeometry is null)
            return;

        var level = RainEd.Instance.Level;
        var rndr = RainEd.Instance.LevelView.Renderer;

        RainEd.Instance.LevelView.CellChangeRecorder.BeginChange();

        // apply moved geometry
        for (int y = 0; y < movingH; y++)
        {
            var gy = rndr.OverlayY + y;
            for (int x = 0; x < movingW; x++)
            {
                var gx = rndr.OverlayX + x;
                for (int l = 0; l < Level.LayerCount; l++)
                {
                    if (!level.IsInBounds(gx, gy)) continue;

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
            
            for (int y = 0; y < movingH; y++)
            {
                var gy = cancelOrigY + y;
                for (int x = 0; x < movingW; x++)
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

    private (bool mask, LevelCell cell)[,,] MakeCellGroup(out int selX, out int selY, out int selW, out int selH, bool eraseSource)
    {
        // (selX, selY) = furthest top-left of layer selection
        selX = int.MaxValue;
        selY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;

        for (int l = 0; l < Level.LayerCount; l++)
        {
            ref var sel = ref selections[l];
            if (sel is null) continue;

            selX = int.Min(selX, sel.minX);
            selY = int.Min(selY, sel.minY);
            maxX = int.Max(maxX, sel.maxX);
            maxY = int.Max(maxY, sel.maxY);
        }

        selW = maxX - selX + 1;
        selH = maxY - selY + 1;
        var level = RainEd.Instance.Level;
        var renderer = RainEd.Instance.LevelView.Renderer;

        if (eraseSource)
        {
            RainEd.Instance.LevelView.CellChangeRecorder.BeginChange();
        }

        static bool GetMaskFromGlobalCoords(ref readonly LayerSelection sel, int x, int y)
        {
            if (x >= sel.minX && y >= sel.minY && x <= sel.maxX && y <= sel.maxY)
                return sel.mask[y - sel.minY, x - sel.minX];
            else
                return false;
        }

        var geometry = new (bool mask, LevelCell cell)[Level.LayerCount, selW, selH];
        for (int y = 0; y < selH; y++)
        {
            var gy = selY + y;
            for (int x = 0; x < selW; x++)
            {
                var gx = selX + x;

                for (int l = 0; l < Level.LayerCount; l++)
                {
                    ref readonly var sel = ref selections[l];
                    if (sel is null) continue;

                    if (!level.IsInBounds(gx, gy))
                        continue;

                    ref var srcCell = ref level.Layers[l,gx,gy];
                    ref var dstCell = ref geometry[l,x,y];
                    bool selMask = GetMaskFromGlobalCoords(in sel, gx, gy);
                    dstCell.mask = selMask;
                    dstCell.cell = srcCell;

                    // change tile head references to be relative to the origin
                    // of the overlay
                    if (dstCell.cell.HasTile() && dstCell.cell.TileHead is null)
                    {
                        dstCell.cell.TileRootX -= selX;
                        dstCell.cell.TileRootY -= selY;

                        // if the tile head is outside of the selection,
                        // then erase this tile body.
                        var tx = dstCell.cell.TileRootX;
                        var ty = dstCell.cell.TileRootY;
                        var tLayerSel = selections[dstCell.cell.TileLayer];
                        if (tx < 0 || ty < 0 ||
                            tx >= selW || ty >= selH ||
                            !(tLayerSel is not null && GetMaskFromGlobalCoords(in tLayerSel, tx + selX, ty + selY))
                        )
                        {
                            dstCell.cell.TileRootX = -1;
                            dstCell.cell.TileRootY = -1;
                            dstCell.cell.TileLayer = -1;
                        }
                    }

                    if (eraseSource && selMask)
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

    public bool IsSelectionActive()
    {
        for (int l = 0; l < Level.LayerCount; l++)
        {
            if (selections[l] is not null)
                return true;
        }

        return false;
    }

    public void ClearSelection()
    {
        selections[0] = null;
        selections[1] = null;
        selections[2] = null;
    }

    public void ClearSelection(ReadOnlySpan<bool> layerMasks)
    {
        for (int l = 0; l < Level.LayerCount; l++)
        {
            if (layerMasks[l]) selections[l] = null;
        }
    }

    public void BeginMove()
    {
        if (!IsSelectionActive())
        {
            cancelGeoData = null;
            movingGeometry = null;
            return;
        }

        // create separate copy of selected cells in order for the Cancel operation
        // to work properly. can't use movingGeometry because it may modify the
        // data of cells (i.e. tile head references)
        var level = RainEd.Instance.Level;

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        for (int l = 0; l < Level.LayerCount; l++)
        {
            ref var sel = ref selections[l];
            if (sel is null) continue;

            minX = int.Min(minX, sel.minX);
            minY = int.Min(minY, sel.minY);
            maxX = int.Max(maxX, sel.maxX);
            maxY = int.Max(maxY, sel.maxY);
        }

        var selW = maxX - minX + 1;
        var selH = maxY - minY + 1;
        cancelGeoData = new (bool mask, LevelCell cell)[Level.LayerCount, selW, selH];
        cancelOrigX = minX;
        cancelOrigY = minY;

        for (int y = 0; y < selH; y++)
        {
            var gy = cancelOrigY + y;
            for (int x = 0; x < selW; x++)
            {
                var gx = cancelOrigX + x;
                for (int l = 0; l < Level.LayerCount; l++)
                {
                    ref var sel = ref selections[l];
                    if (sel is null) continue;

                    if (gx >= sel.minX && gy >= sel.minY && gx <= sel.maxX && gy <= sel.maxY && level.IsInBounds(gx, gy))
                    {
                        var ly = gy - sel.minY;
                        var lx = gx - sel.minX;
                        cancelGeoData[l,x,y] = (sel.mask[ly,lx], level.Layers[l,gx,gy]);
                    }
                    else
                    {
                        cancelGeoData[l,x,y] = (false, new LevelCell());
                    }
                }
            }
        }

        var renderer = RainEd.Instance.LevelView.Renderer;
        movingGeometry = MakeCellGroup(out int movingX, out int movingY, out movingW, out movingH, true);

        // send it to geo renderer
        renderer.OverlayX = movingX;
        renderer.OverlayY = movingY;
        renderer.SetOverlay(
            width: movingW,
            height: movingH,
            geometry: movingGeometry
        );
    }
}