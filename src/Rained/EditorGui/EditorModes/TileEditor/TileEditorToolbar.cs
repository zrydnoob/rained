using ImGuiNET;
using System.Numerics;


namespace RainEd;

partial class TileEditor : IEditorMode
{
    enum SelectionMode
    {
        Materials, Tiles, Autotiles
    }
    private string searchQuery = "";

    // available groups (available = passes search)
    private readonly List<int> matSearchResults = [];
    private readonly List<int> tileSearchResults = [];

    private RlManaged.Texture2D? _loadedMatPreview = null;
    private string _activeMatPreview = "";

    private void ProcessSearch()
    {
        var tileDb = RainEd.Instance.TileDatabase;
        var matDb = RainEd.Instance.MaterialDatabase;

        tileSearchResults.Clear();
        matSearchResults.Clear();

        // find material groups that have any entires that pass the searchq uery
        for (int i = 0; i < matDb.Categories.Count; i++)
        {
            // if search query is empty, add this group to the results
            if (searchQuery == "")
            {
                matSearchResults.Add(i);
                continue;
            }

            // search is not empty, so scan the materials in this group
            // if there is one material that that passes the search query, the
            // group gets put in the list
            // (further searching is done in DrawViewport)
            for (int j = 0; j < matDb.Categories[i].Materials.Count; j++)
            {
                // this material passes the search, so add this group to the search results
                if (matDb.Categories[i].Materials[j].Name.Contains(searchQuery, StringComparison.CurrentCultureIgnoreCase))
                {
                    matSearchResults.Add(i);
                    break;
                }
            }
        }

        // find groups that have any entries that pass the search query
        for (int i = 0; i < tileDb.Categories.Count; i++)
        {
            // if search query is empty, add this group to the search query
            if (searchQuery == "")
            {
                tileSearchResults.Add(i);
                continue;
            }

            // search is not empty, so scan the tiles in this group
            // if there is one tile that that passes the search query, the
            // group gets put in the list
            // (further searching is done in DrawViewport)
            for (int j = 0; j < tileDb.Categories[i].Tiles.Count; j++)
            {
                // this tile passes the search, so add this group to the search results
                if (tileDb.Categories[i].Tiles[j].Name.Contains(searchQuery, StringComparison.CurrentCultureIgnoreCase))
                {
                    tileSearchResults.Add(i);
                    break;
                }
            }
        }
    }

    public void ShowEditMenu()
    {
        //KeyShortcuts.ImGuiMenuItem(KeyShortcut.IncreaseBrushSize, "Increase Brush Size");
        //KeyShortcuts.ImGuiMenuItem(KeyShortcut.DecreaseBrushSize, "Decrease Brush Size");
        KeyShortcuts.ImGuiMenuItem(KeyShortcut.SetMaterial, "设置所选材料为默认值");
    }

    public void DrawToolbar()
    {
        var tileDb = RainEd.Instance.TileDatabase;
        var matDb = RainEd.Instance.MaterialDatabase;
        var prefs = RainEd.Instance.Preferences;

        if (ImGui.Begin("贴图选择器", ImGuiWindowFlags.NoFocusOnAppearing))
        {
            // work layer
            {
                int workLayerV = window.WorkLayer + 1;
                ImGui.SetNextItemWidth(ImGui.GetTextLineHeightWithSpacing() * 4f);
                ImGui.InputInt("工作层级", ref workLayerV);
                window.WorkLayer = Math.Clamp(workLayerV, 1, 3) - 1;
            }

            // default material button (or press E)
            int defaultMat = RainEd.Instance.Level.DefaultMaterial;
            ImGui.TextUnformatted($"默认材料: {matDb.GetMaterial(defaultMat).Name}");

            if (selectionMode != SelectionMode.Materials)
                ImGui.BeginDisabled();

            if ((ImGui.Button("设置所选材料为默认值") || KeyShortcuts.Activated(KeyShortcut.SetMaterial)) && selectionMode == SelectionMode.Materials)
            {
                var oldMat = RainEd.Instance.Level.DefaultMaterial;
                var newMat = selectedMaterial;
                RainEd.Instance.Level.DefaultMaterial = newMat;

                if (oldMat != newMat)
                    RainEd.Instance.ChangeHistory.Push(new ChangeHistory.DefaultMaterialChangeRecord(oldMat, newMat));
            }

            if (selectionMode != SelectionMode.Materials)
                ImGui.EndDisabled();

            if (ImGui.IsItemHovered() && selectionMode != SelectionMode.Materials)
            {
                ImGui.SetTooltip("没有材料被选择");
            }

            // search bar
            var searchInputFlags = ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EscapeClearsAll;

            if (ImGui.BeginTabBar("ModeSelector"))
            {
                var halfWidth = ImGui.GetContentRegionAvail().X / 2f - ImGui.GetStyle().ItemSpacing.X / 2f;

                ImGuiTabItemFlags materialsFlags = ImGuiTabItemFlags.None;
                ImGuiTabItemFlags tilesFlags = ImGuiTabItemFlags.None;
                ImGuiTabItemFlags autotilesFlags = ImGuiTabItemFlags.None;

                // apply force selection
                if (forceSelection == SelectionMode.Materials)
                    materialsFlags = ImGuiTabItemFlags.SetSelected;
                else if (forceSelection == SelectionMode.Tiles)
                    tilesFlags = ImGuiTabItemFlags.SetSelected;
                else if (forceSelection == SelectionMode.Autotiles)
                    autotilesFlags = ImGuiTabItemFlags.SetSelected;

                // Materials tab
                if (ImGuiExt.BeginTabItem("材料", materialsFlags))
                {
                    if (selectionMode != SelectionMode.Materials)
                    {
                        selectionMode = SelectionMode.Materials;
                        ProcessSearch();
                    }

                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    if (ImGui.InputTextWithHint("##Search", "搜索...", ref searchQuery, 128, searchInputFlags))
                    {
                        ProcessSearch();
                    }

                    var boxHeight = ImGui.GetContentRegionAvail().Y;
                    if (ImGui.BeginListBox("##Groups", new Vector2(halfWidth, boxHeight)))
                    {
                        foreach (var i in matSearchResults)
                        {
                            var group = matDb.Categories[i];

                            if (ImGui.Selectable(group.Name, selectedMatGroup == i) || matSearchResults.Count == 1)
                                selectedMatGroup = i;
                        }

                        ImGui.EndListBox();
                    }

                    // group listing (effects) list box
                    ImGui.SameLine();
                    if (ImGui.BeginListBox("##Materials", new Vector2(halfWidth, boxHeight)))
                    {
                        var matList = matDb.Categories[selectedMatGroup].Materials;

                        for (int i = 0; i < matList.Count; i++)
                        {
                            var mat = matList[i];

                            // don't show this prop if it doesn't pass search test
                            if (!mat.Name.Contains(searchQuery, StringComparison.CurrentCultureIgnoreCase))
                                continue;

                            if (ImGui.Selectable(mat.Name, mat.ID == selectedMaterial))
                            {
                                selectedMaterial = mat.ID;
                            }

                            // show material preview when hovered
                            if (prefs.MaterialSelectorPreview && ImGui.IsItemHovered())
                            {
                                if (_activeMatPreview != mat.Name)
                                {
                                    _activeMatPreview = mat.Name;
                                    _loadedMatPreview?.Dispose();
                                    _loadedMatPreview = RlManaged.Texture2D.Load(Path.Combine(Boot.AppDataPath, "assets", "mat-previews", mat.Name + ".png"));
                                }

                                if (_loadedMatPreview is not null && Raylib_cs.Raylib.IsTextureReady(_loadedMatPreview))
                                {
                                    ImGui.BeginTooltip();
                                    ImGuiExt.Image(_loadedMatPreview);
                                    ImGui.EndTooltip();
                                }
                            }
                        }

                        ImGui.EndListBox();
                    }

                    ImGui.EndTabItem();
                }

                // Tiles tab
                if (ImGuiExt.BeginTabItem("贴图", tilesFlags))
                {
                    if (selectionMode != SelectionMode.Tiles)
                    {
                        selectionMode = SelectionMode.Tiles;
                        ProcessSearch();
                    }

                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    if (ImGui.InputTextWithHint("##Search", "搜索...", ref searchQuery, 128, searchInputFlags))
                    {
                        ProcessSearch();
                    }

                    var boxHeight = ImGui.GetContentRegionAvail().Y;
                    if (ImGui.BeginListBox("##Groups", new Vector2(halfWidth, boxHeight)))
                    {
                        var drawList = ImGui.GetWindowDrawList();
                        float textHeight = ImGui.GetTextLineHeight();

                        foreach (var i in tileSearchResults)
                        {
                            var group = tileDb.Categories[i];
                            var cursor = ImGui.GetCursorScreenPos();

                            if (ImGui.Selectable("  " + group.Name, selectedTileGroup == i) || tileSearchResults.Count == 1)
                                selectedTileGroup = i;

                            drawList.AddRectFilled(
                                p_min: cursor,
                                p_max: cursor + new Vector2(10f, textHeight),
                                ImGui.ColorConvertFloat4ToU32(new Vector4(group.Color.R / 255f, group.Color.G / 255f, group.Color.B / 255f, 1f))
                            );
                        }

                        ImGui.EndListBox();
                    }

                    // group listing (effects) list box
                    ImGui.SameLine();
                    if (ImGui.BeginListBox("##Tiles", new Vector2(halfWidth, boxHeight)))
                    {
                        var tileList = tileDb.Categories[selectedTileGroup].Tiles;

                        for (int i = 0; i < tileList.Count; i++)
                        {
                            var tile = tileList[i];

                            // don't show this prop if it doesn't pass search test
                            if (!tile.Name.Contains(searchQuery, StringComparison.CurrentCultureIgnoreCase))
                                continue;

                            if (ImGui.Selectable(tile.Name, tile == selectedTile))
                            {
                                selectedTile = tile;
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();

                                var previewTexFound = RainEd.Instance.AssetGraphics.GetTilePreviewTexture(tile, out var previewTexture, out var previewRect);
                                if (previewTexFound && previewTexture is not null && previewRect is not null)
                                    ImGuiExt.ImageRect(previewTexture!, previewRect!.Value!.Width, previewRect.Value.Height!, previewRect.Value, tile.Category.Color);
                                else
                                    ImGuiExt.ImageSize(RainEd.Instance.PlaceholderTexture, 16, 16);

                                ImGui.EndTooltip();
                            }
                        }

                        ImGui.EndListBox();
                    }

                    ImGui.EndTabItem();
                }

                // Autotiles tab
                if (ImGuiExt.BeginTabItem("自动图块", autotilesFlags))
                {
                    if (selectionMode != SelectionMode.Autotiles)
                    {
                        selectionMode = SelectionMode.Autotiles;
                        //ProcessSearch();
                    }

                    var catalog = RainEd.Instance.Autotiles;
                    var autotileGroups = catalog.AutotileCategories;

                    // deselect autotile if it was removed
                    if (selectedAutotile is not null && !RainEd.Instance.Autotiles.HasAutotile(selectedAutotile))
                    {
                        selectedAutotile = null;
                    }

                    var boxWidth = ImGui.GetTextLineHeight() * 16f;

                    // create autotile button
                    ImGui.BeginGroup();
                    if (ImGui.Button("创建自动图块", new Vector2(boxWidth, 0f)))
                    {
                        RainEd.Instance.Autotiles.OpenCreatePopup();
                        ImGui.OpenPopup("创建自动图块");
                        ImGuiExt.CenterNextWindow(ImGuiCond.Appearing);
                    }

                    RainEd.Instance.Autotiles.RenderCreatePopup();

                    // autotile list
                    var boxHeight = ImGui.GetContentRegionAvail().Y;
                    if (ImGui.BeginListBox("##Autotiles", new Vector2(boxWidth, boxHeight)))
                    {
                        for (int i = 0; i < autotileGroups.Count; i++)
                        {
                            ImGui.PushID(i);
                            var group = catalog.GetAutotilesInCategory(i);

                            if (group.Count > 0 && ImGui.TreeNode(autotileGroups[i]))
                            {
                                foreach (var autotile in group)
                                {
                                    if (ImGui.Selectable(autotile.Name, selectedAutotile == autotile))
                                    {
                                        selectedAutotile = autotile;
                                    }
                                }

                                ImGui.TreePop();
                            }

                            ImGui.PopID();
                        }

                        ImGui.EndListBox();
                    }
                    ImGui.EndGroup();

                    // selected autotile options
                    ImGui.SameLine();
                    ImGui.BeginGroup();
                    if (selectedAutotile is not null)
                    {
                        var autotile = selectedAutotile;

                        ImGui.SeparatorText(autotile.Name);
                        if (autotile.Type == Autotiles.AutotileType.Path)
                        {
                            ImGui.Text("路径自动图块");
                        }
                        else if (autotile.Type == Autotiles.AutotileType.Rect)
                        {
                            ImGui.Text("矩形自动图块");
                        }

                        ImGui.Separator();

                        if (!autotile.IsReady)
                        {
                            ImGui.TextWrapped("加载此自动图块文件时出现问题。");
                        }
                        else
                        {
                            autotile.ConfigGui();
                        }
                    }
                    else
                    {
                        ImGui.TextDisabled("(未选择自动图块文件)");
                    }
                    ImGui.EndGroup();

                    ImGui.EndTabItem();
                }

                forceSelection = null;
                ImGui.EndTabBar();
            }
        }
        ImGui.End();

        // shift+tab to switch between tabs
        if (KeyShortcuts.Activated(KeyShortcut.SwitchTab))
        {
            forceSelection = (SelectionMode)(((int)selectionMode + 1) % 3);
        }

        // tab to change work layer
        if (KeyShortcuts.Activated(KeyShortcut.SwitchLayer))
        {
            window.WorkLayer = (window.WorkLayer + 1) % 3;
        }

        // A/D to change selected group
        if (KeyShortcuts.Activated(KeyShortcut.NavLeft))
        {
            if (selectionMode == SelectionMode.Tiles)
            {
                selectedTileGroup = Mod(selectedTileGroup - 1, tileDb.Categories.Count);
                selectedTile = tileDb.Categories[selectedTileGroup].Tiles[0];
            }
            else if (selectionMode == SelectionMode.Materials)
            {
                selectedMatGroup = Mod(selectedMatGroup - 1, matDb.Categories.Count);
                selectedMaterial = matDb.Categories[selectedMatGroup].Materials[0].ID;
            }
        }

        if (KeyShortcuts.Activated(KeyShortcut.NavRight))
        {
            if (selectionMode == SelectionMode.Tiles)
            {
                selectedTileGroup = Mod(selectedTileGroup + 1, tileDb.Categories.Count);
                selectedTile = tileDb.Categories[selectedTileGroup].Tiles[0];
            }
            else if (selectionMode == SelectionMode.Materials)
            {
                selectedMatGroup = Mod(selectedMatGroup + 1, matDb.Categories.Count);
                selectedMaterial = matDb.Categories[selectedMatGroup].Materials[0].ID;
            }
        }

        // W/S to change selected tile in group
        if (KeyShortcuts.Activated(KeyShortcut.NavUp))
        {
            if (selectionMode == SelectionMode.Tiles)
            {
                var tileList = selectedTile.Category.Tiles;
                selectedTile = tileList[Mod(tileList.IndexOf(selectedTile) - 1, tileList.Count)];
            }
            else if (selectionMode == SelectionMode.Materials)
            {
                var mat = matDb.GetMaterial(selectedMaterial);
                var matList = mat.Category.Materials;
                selectedMaterial = matList[Mod(matList.IndexOf(mat) - 1, matList.Count)].ID;
            }
        }

        if (KeyShortcuts.Activated(KeyShortcut.NavDown))
        {
            if (selectionMode == SelectionMode.Tiles)
            {
                var tileList = selectedTile.Category.Tiles;
                selectedTile = tileList[Mod(tileList.IndexOf(selectedTile) + 1, tileList.Count)];
            }
            else if (selectionMode == SelectionMode.Materials)
            {
                var mat = matDb.GetMaterial(selectedMaterial);
                var matList = mat.Category.Materials;
                selectedMaterial = matList[Mod(matList.IndexOf(mat) + 1, matList.Count)].ID;
            }
        }
    }

    private static int Mod(int a, int b)
        => (a % b + b) % b;
}