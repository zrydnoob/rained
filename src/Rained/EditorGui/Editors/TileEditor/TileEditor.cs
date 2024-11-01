using Rained.Assets;
using Raylib_cs;
using System.Numerics;
using ImGuiNET;
using Rained.Autotiles;
using Rained.LevelData;
namespace Rained.EditorGui.Editors;

partial class TileEditor : IEditorMode
{
    public string Name { get => "贴图"; }

    private readonly LevelWindow window;
    private Tile selectedTile;
    private int selectedMaterial = 1;
    private bool isToolActive = false;
    private bool wasToolActive = false;

    private SelectionMode selectionMode = SelectionMode.Materials;
    private SelectionMode lastSelectionMode;
    private SelectionMode? forceSelection = null;
    private int selectedTileGroup = 0;
    private int selectedMatGroup = 0;

    private bool forcePlace, modifyGeometry, disallowMatOverwrite;
    private bool isMouseHeldInMode = false;

    // true if attaching a chain on a chain holder
    private bool chainHolderMode = false;
    private int chainHolderX = -1;
    private int chainHolderY = -1;
    private int chainHolderL = -1;

    private int materialBrushSize = 1;

    private Autotile? selectedAutotile = null;
    private IAutotileInputBuilder? activePathBuilder = null;

    // this is used to fix force placement when
    // holding down lmb
    private int lastPlaceX = -1;
    private int lastPlaceY = -1;
    private int lastPlaceL = -1;


    // this bool makes it so only one item (material, tile) can be removed
    // while the momuse is hovered over the same cell
    private bool removedOnSameCell = false;
    private int lastMouseX = -1;
    private int lastMouseY = -1;

    // rect fill start
    private CellPosition rectStart;

    enum RectMode { Inactive, Place, Remove };
    private RectMode rectMode = 0;

    enum PlacementMode { None, Force, Geometry };
    private PlacementMode placementMode = PlacementMode.None;

    // time at which the geo modifier key was pressed
    // used for the tile geoification feature.
    private float modifierButtonPressTime = 0f;
    
    public TileEditor(LevelWindow window) {
        this.window = window;
        lastSelectionMode = selectionMode;
        selectedTile = RainEd.Instance.TileDatabase.Categories[0].Tiles[0];
        selectedMaterial = 1;

        RainEd.Instance.ChangeHistory.UndidOrRedid += () =>
        {
            chainHolderMode = false;
            removedOnSameCell = false;
        };
    }

    public void Load()
    {
        activePathBuilder = null;
        isToolActive = false;
        ProcessSearch(); // defined in TileEditorToolbar.cs
    }

    public void Unload()
    {
        window.CellChangeRecorder.TryPushChange();
        lastPlaceX = -1;
        lastPlaceY = -1;
        lastPlaceL = -1;
        rectMode = RectMode.Inactive;
        chainHolderMode = false;
    }

    private static void DrawTile(int tileInt, int x, int y, float lineWidth, float tileSize, Color color)
    {
        if (tileInt == 0)
        {
            // air is represented by a cross (OMG ASCEND WITH GORB???)
            // an empty cell (-1) would mean any tile is accepted
            Raylib.DrawLineV(
                startPos: new Vector2(x * tileSize + 5, y * tileSize + 5),
                endPos: new Vector2((x+1) * tileSize - 5, (y+1) * tileSize - 5),
                color
            );

            Raylib.DrawLineV(
                startPos: new Vector2((x+1) * tileSize - 5, y * tileSize + 5),
                endPos: new Vector2(x * tileSize + 5, (y+1) * tileSize - 5),
                color
            );
        }
        else if (tileInt > 0)
        {
            var cellType = (GeoType)tileInt;
            switch (cellType)
            {
                case GeoType.Solid:
                    RlExt.DrawRectangleLinesRec(
                        new Rectangle(x * tileSize, y * tileSize, tileSize, tileSize),
                        color
                    );
                    break;

                case GeoType.Platform:
                    RlExt.DrawRectangleLinesRec(
                        new Rectangle(x * tileSize, y * tileSize, tileSize, 10),
                        color
                    );
                    break;

                case GeoType.Glass:
                    RlExt.DrawRectangleLinesRec(
                        new Rectangle(x * tileSize, y * tileSize, tileSize, tileSize),
                        color
                    );
                    break;

                case GeoType.ShortcutEntrance:
                    RlExt.DrawRectangleLinesRec(
                        new Rectangle(x * tileSize, y * tileSize, tileSize, tileSize),
                        Color.Red
                    );
                    break;

                case GeoType.SlopeLeftDown:
                    Raylib.DrawTriangleLines(
                        new Vector2(x+1, y+1) * tileSize,
                        new Vector2(x+1, y) * tileSize,
                        new Vector2(x, y) * tileSize,
                        color
                    );
                    break;

                case GeoType.SlopeLeftUp:
                    Raylib.DrawTriangleLines(
                        new Vector2(x, y+1) * tileSize,
                        new Vector2(x+1, y+1) * tileSize,
                        new Vector2(x+1, y) * tileSize,
                        color
                    );
                    break;

                case GeoType.SlopeRightDown:
                    Raylib.DrawTriangleLines(
                        new Vector2(x+1, y) * tileSize,
                        new Vector2(x, y) * tileSize,
                        new Vector2(x, y+1) * tileSize,
                        color
                    );
                    break;

                case GeoType.SlopeRightUp:
                    Raylib.DrawTriangleLines(
                        new Vector2(x+1, y+1) * tileSize,
                        new Vector2(x, y) * tileSize,
                        new Vector2(x, y+1) * tileSize,
                        color
                    );
                    break;
            }
        }
    }

    private static void DrawTileSpecs(Tile selectedTile, int tileOriginX, int tileOriginY,
        float tileSize = Level.TileSize,
        byte alpha = 255
    )
    {
        var lineWidth = 1f / RainEd.Instance.LevelView.ViewZoom;
        var prefs = RainEd.Instance.Preferences;

        if (selectedTile.HasSecondLayer)
        {
            var col = prefs.TileSpec2.ToRGBA(alpha);

            for (int x = 0; x < selectedTile.Width; x++)
            {
                for (int y = 0; y < selectedTile.Height; y++)
                {
                    Rlgl.PushMatrix();
                    Rlgl.Translatef(tileOriginX * tileSize + 2, tileOriginY * tileSize + 2, 0);

                    sbyte tileInt = selectedTile.Requirements2[x,y];
                    DrawTile(tileInt, x, y, lineWidth, tileSize, col);
                    Rlgl.PopMatrix();
                }
            }
        }

        // first layer
        {
            var col = prefs.TileSpec1.ToRGBA(alpha);

            for (int x = 0; x < selectedTile.Width; x++)
            {
                for (int y = 0; y < selectedTile.Height; y++)
                {
                    Rlgl.PushMatrix();
                    Rlgl.Translatef(tileOriginX * tileSize, tileOriginY * tileSize, 0);

                    sbyte tileInt = selectedTile.Requirements[x,y];
                    DrawTile(tileInt, x, y, lineWidth, tileSize, col);
                    Rlgl.PopMatrix();
                }
            }
        }
    }

    public void DrawViewport(RlManaged.RenderTexture2D mainFrame, RlManaged.RenderTexture2D[] layerFrames)
    {
        window.BeginLevelScissorMode();
        wasToolActive = isToolActive;
        isToolActive = false;

        var level = RainEd.Instance.Level;
        var levelRender = window.Renderer;
        var matDb = RainEd.Instance.MaterialDatabase;

        // draw level background (solid white)
        Raylib.DrawRectangle(0, 0, level.Width * Level.TileSize, level.Height * Level.TileSize, LevelWindow.BackgroundColor);

        // draw layers
        var drawProps = RainEd.Instance.Preferences.ViewProps;
        for (int l = Level.LayerCount - 1; l >= 0; l--)
        {
            // draw layer into framebuffer
            Raylib.BeginTextureMode(layerFrames[l]);

            Raylib.EndScissorMode();
            Raylib.ClearBackground(Color.Blank);
            window.BeginLevelScissorMode();

            Rlgl.PushMatrix();
            levelRender.RenderGeometry(l, LevelWindow.GeoColor(255));
            levelRender.RenderTiles(l, 255);
            if (drawProps)
                levelRender.RenderProps(l, 100);
            Rlgl.PopMatrix();
        }

        // draw alpha-blended result into main frame
        Raylib.BeginTextureMode(mainFrame);
        for (int l = Level.LayerCount - 1; l >= 0; l--)
        {
            Rlgl.PushMatrix();
            Rlgl.LoadIdentity();

            var alpha = l == window.WorkLayer ? 255 : 50;
            RlExt.DrawRenderTexture(layerFrames[l], 0, 0, new Color(255, 255, 255, alpha));
            Rlgl.PopMatrix();
        }

        // determine if the user is hovering over a shortcut block
        // if so, make the shortcuts more apparent
        {
            var shortcutAlpha = 127;

            if (window.IsMouseInLevel())
            {
                var cell = level.Layers[window.WorkLayer, window.MouseCx, window.MouseCy];
                bool shortcut = cell.Geo == GeoType.ShortcutEntrance;

                if (!shortcut)
                {
                    foreach (var obj in Level.ShortcutObjects)
                    {
                        if (cell.Has(obj))
                        {
                            shortcut = true;
                            break;
                        }
                    }
                }

                if (shortcut) shortcutAlpha = 255;
            }

            levelRender.RenderShortcuts(new Color(255, 255, 255, shortcutAlpha));

        }
        levelRender.RenderGrid();
        levelRender.RenderBorder();
        levelRender.RenderCameraBorders();

        if (lastMouseX != window.MouseCx || lastMouseY != window.MouseCy)
        {
            lastMouseX = window.MouseCx;
            lastMouseY = window.MouseCy;
            removedOnSameCell = false;
        }

        if (KeyShortcuts.Activated(KeyShortcut.TileForceGeometry))
        {
            modifierButtonPressTime = (float)Raylib.GetTime();
        }
        
        if (RainEd.Instance.Preferences.TilePlacementModeToggle)
        {
            if (KeyShortcuts.Activated(KeyShortcut.TileForceGeometry))
            {
                placementMode = placementMode == PlacementMode.Geometry
                    ? PlacementMode.None : PlacementMode.Geometry;
            }

            if (KeyShortcuts.Activated(KeyShortcut.TileForcePlacement) && selectionMode != SelectionMode.Materials)
            {
                placementMode = placementMode == PlacementMode.Force
                    ? PlacementMode.None : PlacementMode.Force;
            }
            
            modifyGeometry = placementMode == PlacementMode.Geometry;
            forcePlace = placementMode == PlacementMode.Force;
        }
        else
        {
            modifyGeometry = KeyShortcuts.Active(KeyShortcut.TileForceGeometry);
            forcePlace = KeyShortcuts.Active(KeyShortcut.TileForcePlacement);
        }

        disallowMatOverwrite = KeyShortcuts.Active(KeyShortcut.TileIgnoreDifferent);

        if (chainHolderMode)
        {
            window.WriteStatus("Chain Attach");
        }
        else
        {
            // show hovered tile/material in status text
            if (window.IsMouseInLevel())
            {
                ref var mouseCell = ref level.Layers[window.WorkLayer, window.MouseCx, window.MouseCy];

                if (mouseCell.HasTile())
                {
                    var tile = level.GetTile(mouseCell);
                    if (tile is not null)
                    {
                        window.WriteStatus(tile.Name, 3);
                    }
                    else
                    {
                        window.WriteStatus("Stray tile fragment", 3);
                    }
                }
                else if (mouseCell.Material != 0)
                {
                    var matInfo = RainEd.Instance.MaterialDatabase.GetMaterial(mouseCell.Material);
                    window.WriteStatus(matInfo.Name, 3);
                }
            }

            if (selectionMode == SelectionMode.Tiles || selectionMode == SelectionMode.Autotiles)
            {
                if (modifyGeometry)
                    window.WriteStatus("Force Geometry");
                else if (forcePlace)
                    window.WriteStatus("Force Placement");

                if (disallowMatOverwrite)
                    window.WriteStatus("Ignore Materials");
            }
            else if (selectionMode == SelectionMode.Materials)
            {
                if (disallowMatOverwrite)
                    window.WriteStatus("Disallow Overwrite");

                if (modifyGeometry)
                    window.WriteStatus("Force Geometry");
            }
        }

        if (selectionMode != lastSelectionMode)
        {
            lastSelectionMode = selectionMode;
            rectMode = RectMode.Inactive;
        }

        if (window.IsViewportHovered)
        {
            // begin change if left or right button is down
            // regardless of what it's doing
            if (EditorWindow.IsMouseDown(ImGuiMouseButton.Left) || KeyShortcuts.Active(KeyShortcut.RightMouse))
            {
                if (!wasToolActive) window.CellChangeRecorder.BeginChange();
                isToolActive = true;
            }

            if (chainHolderMode)
            {
                ProcessChainAttach();
                isMouseHeldInMode = false;
            }
            else
            {
                if (isToolActive && !wasToolActive)
                {
                    isMouseHeldInMode = true;
                }

                if (!isToolActive && wasToolActive)
                {
                    isMouseHeldInMode = false;
                }

                // render selected tile
                switch (selectionMode)
                {
                    case SelectionMode.Tiles:
                        ProcessTiles();
                        break;

                    case SelectionMode.Materials:
                        ProcessMaterials();
                        break;

                    case SelectionMode.Autotiles:
                        ProcessAutotiles();
                        break;
                }

                // material and tile eyedropper and removal
                if (window.IsMouseInLevel() && rectMode == RectMode.Inactive)
                {
                    int tileLayer = window.WorkLayer;
                    int tileX = window.MouseCx;
                    int tileY = window.MouseCy;

                    ref var mouseCell = ref level.Layers[window.WorkLayer, window.MouseCx, window.MouseCy];

                    // if this is a tile body, find referenced tile head
                    if (mouseCell.HasTile() && mouseCell.TileHead is null)
                    {
                        tileLayer = mouseCell.TileLayer;
                        tileX = mouseCell.TileRootX;
                        tileY = mouseCell.TileRootY;

                        //if (!level.IsInBounds(tileX, tileY) || level.Layers[tileLayer, tileX, tileY].TileHead is null)
                        //    ImGui.SetTooltip("Detached tile body");
                    }


                    // eyedropper
                    if (KeyShortcuts.Activated(KeyShortcut.Eyedropper))
                    {
                        // tile eyedropper
                        if (mouseCell.HasTile())
                        {
                            var tile = level.Layers[tileLayer, tileX, tileY].TileHead;

                            if (tile is null)
                            {
                                Log.UserLogger.Error("Could not find tile head");
                            }
                            else
                            {
                                forceSelection = SelectionMode.Tiles;
                                selectedTile = tile;
                                selectedTileGroup = selectedTile.Category.Index;
                            }
                        }

                        // material eyedropper
                        else
                        {
                            if (mouseCell.Material > 0)
                            {
                                selectedMaterial = mouseCell.Material;
                                var matInfo = matDb.GetMaterial(selectedMaterial);

                                // select tile group that contains this material
                                var idx = matDb.Categories.IndexOf(matInfo.Category);
                                if (idx == -1)
                                {
                                    EditorWindow.ShowNotification("Error");
                                    Log.UserLogger.Error("Error eyedropping material '{MaterialName}' (ID {ID})", matInfo.Name, selectedMaterial);
                                }
                                else
                                {
                                    selectedMatGroup = idx;
                                    forceSelection = SelectionMode.Materials;
                                }
                            }
                        }
                    }

                    // remove tile on right click
                    if (!removedOnSameCell && isMouseHeldInMode && EditorWindow.IsMouseDown(ImGuiMouseButton.Right) && mouseCell.HasTile())
                    {
                        if (selectionMode == SelectionMode.Autotiles || selectionMode == SelectionMode.Tiles || (selectionMode == SelectionMode.Materials && !disallowMatOverwrite))
                        {
                            removedOnSameCell = true;
                            level.RemoveTileCell(window.WorkLayer, window.MouseCx, window.MouseCy, modifyGeometry);
                        }
                    }
                }
            }
        }

        if (wasToolActive && !isToolActive)
        {
            // process once again in case it does something
            // when user lets go of mouse
            switch (selectionMode)
            {
                case SelectionMode.Tiles:
                    ProcessTiles();
                    break;

                case SelectionMode.Materials:
                    ProcessMaterials();
                    break;

                case SelectionMode.Autotiles:
                    ProcessAutotiles();
                    break;
            }

            window.CellChangeRecorder.PushChange();
            lastPlaceX = -1;
            lastPlaceY = -1;
            lastPlaceL = -1;
            removedOnSameCell = false;
        }

        Raylib.EndScissorMode();
    }

    private void ProcessChainAttach()
    {
        var chainX = (int)Math.Floor(window.MouseCellFloat.X + 0.5f);
        var chainY = (int)Math.Floor(window.MouseCellFloat.Y + 0.5f);

        RainEd.Instance.Level.SetChainData(
            chainHolderL, chainHolderX, chainHolderY,
            chainX - 1, chainY - 1
        );

        // left-click to confirm
        if (EditorWindow.IsMouseClicked(ImGuiMouseButton.Left))
        {
            chainHolderMode = false;
        }
        // escape or right click to abort
        else if (EditorWindow.IsKeyPressed(ImGuiKey.Escape) || KeyShortcuts.Activated(KeyShortcut.RightMouse))
        {
            chainHolderMode = false;
            RainEd.Instance.Level.RemoveChainData(chainHolderL, chainHolderX, chainHolderY);
        }
    }

    private void ProcessMaterials()
    {
        activePathBuilder = null;

        var level = RainEd.Instance.Level;

        // rect place mode
        if (rectMode != RectMode.Inactive)
        {
            var rMinX = Math.Min(rectStart.X, window.MouseCx);
            var rMaxX = Math.Max(rectStart.X, window.MouseCx);
            var rMinY = Math.Min(rectStart.Y, window.MouseCy);
            var rMaxY = Math.Max(rectStart.Y, window.MouseCy);
            var rWidth = rMaxX - rMinX + 1;
            var rHeight = rMaxY - rMinY + 1;

            Raylib.DrawRectangleLinesEx(
                new Rectangle(rMinX * Level.TileSize, rMinY * Level.TileSize, rWidth * Level.TileSize, rHeight * Level.TileSize),
                2f / window.ViewZoom,
                RainEd.Instance.MaterialDatabase.GetMaterial(selectedMaterial).Color
            );

            if (!isToolActive)
            {
                if (rectMode == RectMode.Place)
                {
                    for (int x = rMinX; x <= rMaxX; x++)
                    {
                        for (int y = rMinY; y <= rMaxY; y++)
                        {
                            if (!level.IsInBounds(x, y)) continue;

                            if (!disallowMatOverwrite || level.Layers[window.WorkLayer, x, y].Material == 0)
                            {
                                if (modifyGeometry)
                                {
                                    level.Layers[window.WorkLayer, x, y].Geo = GeoType.Solid;
                                    window.Renderer.InvalidateGeo(x, y, window.WorkLayer);
                                }

                                level.Layers[window.WorkLayer, x, y].Material = selectedMaterial;
                            }
                        }
                    }
                }
                else if (rectMode == RectMode.Remove)
                {
                    for (int x = rMinX; x <= rMaxX; x++)
                    {
                        for (int y = rMinY; y <= rMaxY; y++)
                        {
                            if (!level.IsInBounds(x, y)) continue;

                            if (!disallowMatOverwrite || level.Layers[window.WorkLayer, x, y].Material == selectedMaterial)
                            {
                                level.Layers[window.WorkLayer, x, y].Material = 0;

                                if (modifyGeometry)
                                {
                                    level.Layers[window.WorkLayer, x, y].Geo = GeoType.Air;
                                    window.Renderer.InvalidateGeo(x, y, window.WorkLayer);
                                }
                            }

                            if (!disallowMatOverwrite && level.Layers[window.WorkLayer, x, y].HasTile())
                            {
                                level.RemoveTileCell(window.WorkLayer, x, y, modifyGeometry);
                            }
                        }
                    }
                }

                rectMode = RectMode.Inactive;
            }
        }

        // check if rect place mode will start
        else if (isMouseHeldInMode && isToolActive && !wasToolActive && EditorWindow.IsKeyDown(ImGuiKey.ModShift))
        {
            if (EditorWindow.IsMouseDown(ImGuiMouseButton.Left))
            {
                rectMode = RectMode.Place;
            }
            else if (EditorWindow.IsMouseDown(ImGuiMouseButton.Right))
            {
                rectMode = RectMode.Remove;
            }

            if (rectMode != RectMode.Inactive)
                rectStart = new CellPosition(window.MouseCx, window.MouseCy, window.WorkLayer);
        }

        // normal material mode
        else
        {
            bool brushSizeKey =
                KeyShortcuts.Activated(KeyShortcut.IncreaseBrushSize) || KeyShortcuts.Activated(KeyShortcut.DecreaseBrushSize);

            if (EditorWindow.IsKeyDown(ImGuiKey.ModShift) || brushSizeKey)
            {
                window.OverrideMouseWheel = true;

                if (Raylib.GetMouseWheelMove() > 0.0f || KeyShortcuts.Activated(KeyShortcut.IncreaseBrushSize))
                    materialBrushSize += 2;
                else if (Raylib.GetMouseWheelMove() < 0.0f || KeyShortcuts.Activated(KeyShortcut.DecreaseBrushSize))
                    materialBrushSize -= 2;

                materialBrushSize = Math.Clamp(materialBrushSize, 1, 21);
            }

            // draw grid cursor
            int cursorLeft = window.MouseCx - materialBrushSize / 2;
            int cursorTop = window.MouseCy - materialBrushSize / 2;

            Raylib.DrawRectangleLinesEx(
                new Rectangle(
                    cursorLeft * Level.TileSize,
                    cursorTop * Level.TileSize,
                    Level.TileSize * materialBrushSize,
                    Level.TileSize * materialBrushSize
                ),
                2f / window.ViewZoom,
                RainEd.Instance.MaterialDatabase.GetMaterial(selectedMaterial).Color
            );

            // place material
            int placeMode = 0;
            if (isMouseHeldInMode)
            {
                if (EditorWindow.IsMouseDown(ImGuiMouseButton.Left))
                    placeMode = 1;
                else if (EditorWindow.IsMouseDown(ImGuiMouseButton.Right))
                    placeMode = 2;
            }

            if (placeMode != 0 && (placeMode == 1 || !removedOnSameCell))
            {
                // place or remove materials inside cursor
                for (int x = cursorLeft; x <= window.MouseCx + materialBrushSize / 2; x++)
                {
                    for (int y = cursorTop; y <= window.MouseCy + materialBrushSize / 2; y++)
                    {
                        if (!level.IsInBounds(x, y)) continue;

                        ref var cell = ref level.Layers[window.WorkLayer, x, y];
                        if (cell.HasTile()) continue;

                        if (placeMode == 1)
                        {
                            if (!disallowMatOverwrite || cell.Material == 0)
                            {
                                if (modifyGeometry)
                                {
                                    level.Layers[window.WorkLayer, x, y].Geo = GeoType.Solid;
                                    window.Renderer.InvalidateGeo(x, y, window.WorkLayer);
                                }

                                cell.Material = selectedMaterial;
                            }
                        }
                        else
                        {
                            if (!disallowMatOverwrite || cell.Material == selectedMaterial)
                            {
                                if (modifyGeometry)
                                {
                                    level.Layers[window.WorkLayer, x, y].Geo = GeoType.Air;
                                    window.Renderer.InvalidateGeo(x, y, window.WorkLayer);
                                }

                                cell.Material = 0;
                                removedOnSameCell = true;
                            }
                        }
                    }
                }
            }
        }
    }

    private void ProcessTiles()
    {
        activePathBuilder = null;

        var level = RainEd.Instance.Level;

        // mouse position is at center of tile
        // tileOrigin is the top-left of the tile, so some math to adjust
        //var tileOriginFloat = window.MouseCellFloat + new Vector2(0.5f, 0.5f) - new Vector2(selectedTile.Width, selectedTile.Height) / 2f;
        var tileOriginX = window.MouseCx - selectedTile.CenterX;
        int tileOriginY = window.MouseCy - selectedTile.CenterY;

        // rect place mode
        if (rectMode != RectMode.Inactive)
        {
            var tileWidth = selectedTile.Width;
            var tileHeight = selectedTile.Height;

            if (rectMode == RectMode.Remove)
            {
                tileWidth = 1;
                tileHeight = 1;
            }

            var gridW = (float)(window.MouseCellFloat.X - rectStart.X) / tileWidth;
            var gridH = (float)(window.MouseCellFloat.Y - rectStart.Y) / tileHeight;
            var rectW = (int)(gridW > 0f ? MathF.Ceiling(gridW) : (MathF.Floor(gridW) - 1)) * tileWidth;
            var rectH = (int)(gridH > 0f ? MathF.Ceiling(gridH) : (MathF.Floor(gridH) - 1)) * tileHeight;

            // update minX and minY to fit new rect size
            float minX, minY;

            if (gridW > 0f)
                minX = rectStart.X;
            else
                minX = rectStart.X + rectW + tileWidth;

            if (gridH > 0f)
                minY = rectStart.Y;
            else
                minY = rectStart.Y + rectH + tileHeight;

            Raylib.DrawRectangleLinesEx(
                new Rectangle(minX * Level.TileSize, minY * Level.TileSize, Math.Abs(rectW) * Level.TileSize, Math.Abs(rectH) * Level.TileSize),
                1f / window.ViewZoom,
                Color.White
            );

            if (!isToolActive)
            {
                // place tiles
                if (rectMode == RectMode.Place)
                {
                    for (int x = 0; x < Math.Abs(rectW); x += tileWidth)
                    {
                        for (int y = 0; y < Math.Abs(rectH); y += tileHeight)
                        {
                            var tileRootX = (int)minX + x + selectedTile.CenterX;
                            var tileRootY = (int)minY + y + selectedTile.CenterY;
                            if (!level.IsInBounds(tileRootX, tileRootY)) continue;

                            var status = level.ValidateTilePlacement(
                                selectedTile,
                                tileRootX - selectedTile.CenterX, tileRootY - selectedTile.CenterY, window.WorkLayer,
                                modifyGeometry || forcePlace
                            );

                            if (status == TilePlacementStatus.Success)
                            {
                                level.PlaceTile(selectedTile, window.WorkLayer, tileRootX, tileRootY, modifyGeometry);
                            }
                        }
                    }
                }

                // remove tiles
                else
                {
                    for (int x = 0; x < Math.Abs(rectW); x++)
                    {
                        for (int y = 0; y < Math.Abs(rectH); y++)
                        {
                            var gx = (int)minX + x;
                            var gy = (int)minY + y;
                            if (!level.IsInBounds(gx, gy)) continue;

                            if (level.Layers[window.WorkLayer, gx, gy].HasTile())
                                level.RemoveTileCell(window.WorkLayer, gx, gy, modifyGeometry);

                            if (!disallowMatOverwrite)
                                level.Layers[window.WorkLayer, gx, gy].Material = 0;
                        }
                    }
                }

                rectMode = RectMode.Inactive;
            }

            // if the force geo button is tapped, will geoify tiles
            // instead of placing new ones
            else if (KeyShortcuts.Deactivated(KeyShortcut.TileForceGeometry) &&
                ((float)Raylib.GetTime() - modifierButtonPressTime) < 0.3)
            {
                for (int x = 0; x < Math.Abs(rectW); x++)
                {
                    for (int y = 0; y < Math.Abs(rectH); y++)
                    {
                        var gx = (int)minX + x;
                        var gy = (int)minY + y;
                        if (!level.IsInBounds(gx, gy)) continue;
                        GeoifyTile(gx, gy, window.WorkLayer);
                    }
                }

                rectMode = RectMode.Inactive;
            }
        }

        // check if rect place mode will start
        else if (isMouseHeldInMode && isToolActive && !wasToolActive && EditorWindow.IsKeyDown(ImGuiKey.ModShift))
        {
            int rectOffsetX = 0;
            int rectOffsetY = 0;

            if (EditorWindow.IsMouseDown(ImGuiMouseButton.Left))
            {
                rectMode = RectMode.Place;
                rectOffsetX = -selectedTile.CenterX;
                rectOffsetY = -selectedTile.CenterY;
            }
            else if (EditorWindow.IsMouseDown(ImGuiMouseButton.Right))
            {
                rectMode = RectMode.Remove;
            }

            if (rectMode != RectMode.Inactive)
                rectStart = new CellPosition(window.MouseCx + rectOffsetX, window.MouseCy + rectOffsetY, window.WorkLayer);
        }

        // single-tile place mode
        else
        {
            // draw tile requirements
            DrawTileSpecs(selectedTile, tileOriginX, tileOriginY);

            // check if requirements are satisfied
            TilePlacementStatus validationStatus;

            if (level.IsInBounds(window.MouseCx, window.MouseCy))
                validationStatus = level.ValidateTilePlacement(
                    selectedTile,
                    tileOriginX, tileOriginY, window.WorkLayer,
                    modifyGeometry || forcePlace
                );
            else
                validationStatus = TilePlacementStatus.OutOfBounds;

            // draw tile preview
            Rectangle srcRect, dstRect;
            dstRect = new Rectangle(
                new Vector2(tileOriginX, tileOriginY) * Level.TileSize - new Vector2(2, 2),
                new Vector2(selectedTile.Width, selectedTile.Height) * Level.TileSize
            );

            var tileTexFound = RainEd.Instance.AssetGraphics.GetTilePreviewTexture(selectedTile, out var previewTexture, out var previewRect);
            if (tileTexFound)
            {
                //srcRect = new Rectangle(Vector2.Zero, new Vector2(selectedTile.Width * 16, selectedTile.Height * 16));
                srcRect = previewRect!.Value;
                Raylib.EndShaderMode();
            }
            else
            {
                srcRect = new Rectangle(Vector2.Zero, new Vector2(selectedTile.Width * 2, selectedTile.Height * 2));
                Raylib.BeginShaderMode(Shaders.UvRepeatShader);
            }

            // draw tile preview
            Raylib.DrawTexturePro(
                previewTexture ?? RainEd.Instance.PlaceholderTexture,
                srcRect, dstRect,
                Vector2.Zero, 0f,
                validationStatus == TilePlacementStatus.Success ? new Color(255, 255, 255, 200) : new Color(255, 0, 0, 200)
            );

            Raylib.EndShaderMode();

            // place tile on click
            if (isMouseHeldInMode && EditorWindow.IsMouseDown(ImGuiMouseButton.Left))
            {
                if (validationStatus == TilePlacementStatus.Success)
                {
                    // extra if statement to prevent overwriting the already placed tile
                    // when holding down LMB
                    if (lastPlaceX == -1 || !(modifyGeometry || forcePlace) || !level.IsIntersectingTile(
                        selectedTile,
                        tileOriginX, tileOriginY, window.WorkLayer,
                        lastPlaceX, lastPlaceY, lastPlaceL
                    ))
                    {
                        level.PlaceTile(
                            selectedTile,
                            window.WorkLayer, window.MouseCx, window.MouseCy,
                            modifyGeometry
                        );

                        lastPlaceX = window.MouseCx;
                        lastPlaceY = window.MouseCy;
                        lastPlaceL = window.WorkLayer;

                        if (selectedTile.Tags.Contains("Chain Holder"))
                        {
                            chainHolderMode = true;
                            chainHolderX = lastPlaceX;
                            chainHolderY = lastPlaceY;
                            chainHolderL = lastPlaceL;
                        }
                    }
                }
                else if (EditorWindow.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    string errStr = validationStatus switch
                    {
                        TilePlacementStatus.OutOfBounds => "Tile is out of bounds",
                        TilePlacementStatus.Overlap => "Tile is overlapping another",
                        TilePlacementStatus.Geometry => "Tile geometry requirements not met",
                        _ => "Unknown tile placement error"
                    };

                    EditorWindow.ShowNotification(errStr);
                }
            }

            // remove material under mouse cursor
            if (!removedOnSameCell && isMouseHeldInMode && EditorWindow.IsMouseDown(ImGuiMouseButton.Right) && level.IsInBounds(window.MouseCx, window.MouseCy))
            {
                ref var cell = ref level.Layers[window.WorkLayer, window.MouseCx, window.MouseCy];
                if (!cell.HasTile() && !disallowMatOverwrite)
                {
                    cell.Material = 0;
                    removedOnSameCell = true;
                }
            }
        }
    }

    private void ProcessAutotiles()
    {
        var time = (float)Raylib.GetTime();
        bool deactivate = false;
        bool endOnClick = RainEd.Instance.Preferences.AutotileMouseMode == UserPreferences.AutotileMouseModeOptions.Click;

        // if mouse was pressed
        if (isToolActive && !wasToolActive && !KeyShortcuts.Active(KeyShortcut.RightMouse))
        {
            if (activePathBuilder is null)
            {
                if (
                    selectedAutotile is not null &&
                    selectedAutotile.IsReady &&
                    selectedAutotile.CanActivate
                )
                {
                    activePathBuilder = selectedAutotile.Type switch
                    {
                        AutotileType.Path => new AutotilePathBuilder(selectedAutotile),
                        AutotileType.Rect => new AutotileRectBuilder(selectedAutotile, new Vector2i(window.MouseCx, window.MouseCy)),
                        _ => null
                    };
                }
            }
            else if (endOnClick)
            {
                deactivate = true;
            }
        }

        // if mouse was released
        if (!isToolActive && wasToolActive)
        {
            if (!endOnClick)
            {
                deactivate = true;
            }
        }

        if (activePathBuilder is not null)
        {
            activePathBuilder.Update();

            // press escape to cancel path building
            if (EditorWindow.IsKeyPressed(ImGuiKey.Escape))
            {
                activePathBuilder = null;
            }
            else if (deactivate)
            {
                activePathBuilder.Finish(window.WorkLayer, forcePlace, modifyGeometry);
                activePathBuilder = null;
            }
        }
    }

    private void GeoifyTile(int x, int y, int layer)
    {
        var level = RainEd.Instance.Level;
        var render = RainEd.Instance.LevelView.Renderer;

        if (!level.Layers[layer, x, y].HasTile()) return;

        Tile? tile = level.GetTile(layer, x, y);
        if (tile is null)
        {
            Log.Error("GeoifyTile: Tile not found?");
            return;
        }

        var tileHeadPos = level.GetTileHead(layer, x, y);
        var localX = x - tileHeadPos.X + tile.CenterX;
        var localY = y - tileHeadPos.Y + tile.CenterY;
        var localZ = layer - tileHeadPos.Layer;
        
        sbyte[,] requirements;
        if (localZ == 0)
            requirements = tile.Requirements;
        else if (localZ == 1)
            requirements = tile.Requirements2;
        else
        {
            Log.Error("GeoifyTile: localZ is not 0 or 1");
            return;
        }

        level.Layers[layer, x, y].Geo = (GeoType) requirements[localX, localY];
        render.InvalidateGeo(x, y, layer);
    }
}