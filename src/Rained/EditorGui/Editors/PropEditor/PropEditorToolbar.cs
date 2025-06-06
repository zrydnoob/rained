using ImGuiNET;
using System.Numerics;
using Raylib_cs;
using Rained.Assets;
using Rained.LevelData;

namespace Rained.EditorGui.Editors;

partial class PropEditor : IEditorMode
{
    private readonly string[] PropRenderTimeNames = ["渲染效果前", "渲染效果后"];
    private readonly string[] RopeReleaseModeNames = ["None", "Left", "Right"];

    enum SelectionMode
    {
        Props,
        Tiles
    };

    private int selectedPropGroup = 0;
    private int selectedTileGroup = 0;
    private int selectedPropIdx = 0;
    private int selectedTileIdx = 0;
    private SelectionMode selectionMode = SelectionMode.Props;
    private SelectionMode? forceSelection = null;
    private PropInit? selectedInit;
    private RlManaged.RenderTexture2D previewTexture = null!;
    private PropInit? curPropPreview = null;

    private int zTranslateValue = 0;
    private bool zTranslateActive = false;
    private bool zTranslateWrap = false;
    private Dictionary<Prop, int> zTranslateDepths = [];

    // search results only process groups because i'm too lazy to have
    // it also process the resulting props
    // plus, i don't think it's much of an optimization concern because then
    // it'd only need to filter props per one category, and there's not
    // that many props per category
    private string searchQuery = "";
    private readonly List<(int, PropCategory)> searchResults = new();
    private readonly List<(int, PropTileCategory)> tileSearchResults = new();

    private bool isRopeSimulationActive = false;
    private bool wasRopeSimulationActive = false;

    #region Multiselect Inputs
    // what a reflective mess...

    private void MultiselectDragInt(string label, string fieldName, float v_speed = 1f, int v_min = int.MinValue, int v_max = int.MaxValue)
    {
        var field = typeof(Prop).GetField(fieldName)!;
        var targetV = (int)field.GetValue(selectedProps[0])!;

        bool isSame = true;
        for (int i = 1; i < selectedProps.Count; i++)
        {
            if ((int)field.GetValue(selectedProps[i])! != targetV)
            {
                isSame = false;
                break;
            }
        }

        if (isSame)
        {
            int v = (int)field.GetValue(selectedProps[0])!;
            if (ImGui.DragInt(label, ref v, v_speed, v_min, v_max))
            {
                foreach (var prop in selectedProps)
                    field.SetValue(prop, v);
            }
        }
        else
        {
            int v = 0;
            if (ImGui.DragInt(label, ref v, v_speed, v_min, v_max, string.Empty))
            {
                foreach (var prop in selectedProps)
                    field.SetValue(prop, v);
            }
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            changeRecorder.PushSettingsChanges();
        }
    }

    private void MultiselectSliderInt(string label, string fieldName, int v_min, int v_max, string format = "%i", ImGuiSliderFlags flags = 0)
    {
        var field = typeof(Prop).GetField(fieldName)!;
        var targetV = (int)field.GetValue(selectedProps[0])!;
        var style = ImGui.GetStyle();

        bool isSame = true;
        for (int i = 1; i < selectedProps.Count; i++)
        {
            if ((int)field.GetValue(selectedProps[i])! != targetV)
            {
                isSame = false;
                break;
            }
        }

        bool depthOffsetInput = fieldName == nameof(Prop.DepthOffset);
        if (depthOffsetInput)
        {
            var w = ImGui.CalcItemWidth() - ImGui.GetFrameHeight() * 2 - style.ItemInnerSpacing.X * 2;
            ImGui.PushItemWidth(w);
        }
        
        if (isSame)
        {
            int v = (int) field.GetValue(selectedProps[0])!;
            if (ImGui.SliderInt(depthOffsetInput ? "##"+label : label, ref v, v_min, v_max, format, flags))
            {
                foreach (var prop in selectedProps)
                    field.SetValue(prop, v);
            }
        }
        else
        {
            int v = int.MinValue;
            if (ImGui.SliderInt(depthOffsetInput ? "##"+label : label, ref v, v_min, v_max, string.Empty, flags))
            {
                foreach (var prop in selectedProps)
                    field.SetValue(prop, v);
            }
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
            changeRecorder.PushSettingsChanges();

        if (depthOffsetInput)
        {
            // decrement/increment input
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, style.ItemInnerSpacing);

            ImGui.PushButtonRepeat(true);
            bool fast = EditorWindow.IsKeyDown(ImGuiKey.ModShift);
            var delta = fast ? 10 : 1;

            ImGui.SameLine();
            if (ImGui.Button(fast ? "<" : "-", Vector2.One * ImGui.GetFrameHeight()))
            {
                foreach (var prop in selectedProps)
                    field.SetValue(prop, Math.Max(0, (int)field.GetValue(prop)! - delta));
            }

            if (ImGui.IsItemDeactivated())
                changeRecorder.PushSettingsChanges();

            ImGui.SameLine();
            if (ImGui.Button(fast ? ">" : "+", Vector2.One * ImGui.GetFrameHeight()))
            {
                foreach (var prop in selectedProps)
                    field.SetValue(prop, Math.Min(Level.LayerCount*10-1, (int)field.GetValue(prop)! + delta));
            }

            if (ImGui.IsItemDeactivated())
                changeRecorder.PushSettingsChanges();

            ImGui.PopButtonRepeat();

            ImGui.SameLine();
            ImGui.Text(label);

            ImGui.PopStyleVar();
            ImGui.PopItemWidth();
        }
    }

    // this, specifically, is generic for both the items list and the field type,
    // because i use this for both prop properties and rope-type rope properties
    private void MultiselectEnumInput<T, E>(List<T> items, string label, string fieldName, string[] enumNames) where E : Enum
    {
        var field = typeof(T).GetField(fieldName)!;
        E targetV = (E)field.GetValue(items[0])!;

        bool isSame = true;
        for (int i = 1; i < items.Count; i++)
        {
            if (!((E)field.GetValue(items[i])!).Equals(targetV))
            {
                isSame = false;
                break;
            }
        }

        var previewText = isSame ? enumNames[(int)Convert.ChangeType(targetV, targetV.GetTypeCode())] : "";

        if (ImGui.BeginCombo(label, previewText))
        {
            for (int i = 0; i < enumNames.Length; i++)
            {
                E e = (E)Convert.ChangeType(i, targetV.GetTypeCode());
                bool sel = isSame && e.Equals(targetV);
                if (ImGui.Selectable(enumNames[i], sel))
                {
                    foreach (var item in items)
                        field.SetValue(item, e);

                    changeRecorder.PushSettingsChanges();
                }

                if (sel)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }
    }

    private static void MultiselectSwitchInput<T, E>(List<T> items, string label, string fieldName, ReadOnlySpan<string> values) where E : Enum
    {
        var field = typeof(T).GetField(fieldName)!;
        object targetV = field.GetValue(items[0])!;

        bool isSame = true;
        for (int i = 1; i < items.Count; i++)
        {
            if (!field.GetValue(items[i])!.Equals(targetV))
            {
                isSame = false;
                break;
            }
        }

        int selected = isSame ? (int)targetV : -1;

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemInnerSpacing);

        if (ImGuiExt.ButtonSwitch(label, values, ref selected))
        {
            E e = (E)Convert.ChangeType(selected, ((E)targetV).GetTypeCode());
            foreach (var item in items)
                field.SetValue(item, e);
        }

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.PopStyleVar();
        ImGui.Text(label);
    }

    private void MultiselectListInput<T>(string label, string fieldName, List<T> list)
    {
        var field = typeof(Prop).GetField(fieldName)!;
        int targetV = (int)field.GetValue(selectedProps[0])!;

        bool isSame = true;
        for (int i = 1; i < selectedProps.Count; i++)
        {
            if (!field.GetValue(selectedProps[i])!.Equals(targetV))
            {
                isSame = false;
                break;
            }
        }

        var previewText = isSame ? list[targetV]!.ToString() : "";

        if (ImGui.BeginCombo(label, previewText))
        {
            for (int i = 0; i < list.Count; i++)
            {
                var txt = list[i]!.ToString();
                bool sel = isSame && targetV == i;
                if (ImGui.Selectable(txt, sel))
                {
                    foreach (var prop in selectedProps)
                        field.SetValue(prop, i);

                    changeRecorder.PushSettingsChanges();
                }

                if (sel)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }
    }
    #endregion

    private void ProcessSearch()
    {
        searchResults.Clear();
        tileSearchResults.Clear();
        var propDb = RainEd.Instance.PropDatabase;

        // normal props
        if (selectionMode == SelectionMode.Props)
        {
            for (int i = 0; i < propDb.Categories.Count; i++)
            {
                var group = propDb.Categories[i];

                // skip "Tiles as props" categories
                if (group.IsTileCategory) continue;

                foreach (var prop in group.Props)
                {
                    if (prop.Name.Contains(searchQuery, StringComparison.CurrentCultureIgnoreCase))
                    {
                        searchResults.Add((i, group));
                        break;
                    }
                }
            }
        }

        // tile props
        else if (selectionMode == SelectionMode.Tiles)
        {
            for (int i = 0; i < propDb.TileCategories.Count; i++)
            {
                var group = propDb.TileCategories[i];

                foreach (var prop in group.Props)
                {
                    if (prop.Name.Contains(searchQuery, StringComparison.CurrentCultureIgnoreCase))
                    {
                        tileSearchResults.Add((i, group));
                        break;
                    }
                }
            }
        }
    }

    private void UpdatePreview(PropInit prop)
    {
        var texWidth = (int)(prop.Width * 20f);
        var texHeight = (int)(prop.Height * 20f);

        if (previewTexture is null || curPropPreview != prop)
        {
            curPropPreview = prop;

            previewTexture?.Dispose();
            previewTexture = RlManaged.RenderTexture2D.Load(texWidth, texHeight);
        }

        Raylib.BeginTextureMode(previewTexture);
        Raylib.ClearBackground(Color.Blank);
        Raylib.BeginShaderMode(Shaders.PropShader);
        {
            var propTexture = RainEd.Instance.AssetGraphics.GetPropTexture(prop);
            for (int depth = prop.LayerCount - 1; depth >= 0; depth--)
            {
                float whiteFade = Math.Clamp(depth / 16f, 0f, 1f);
                Rectangle srcRect, dstRec;

                if (propTexture is not null)
                {
                    srcRect = prop.GetPreviewRectangle(0, depth);
                    dstRec = new Rectangle(Vector2.Zero, srcRect.Size);
                }
                else
                {
                    srcRect = new Rectangle(Vector2.Zero, 2.0f * Vector2.One);
                    dstRec = new Rectangle(Vector2.Zero, prop.Width * 20f, prop.Height * 20f);
                }

                var drawColor = new Color(255, (int)(whiteFade * 255f), 0, 0);

                if (propTexture is not null)
                {
                    propTexture.DrawRectangle(srcRect, dstRec, drawColor);
                }
                else
                {
                    Raylib.DrawTexturePro(
                        RainEd.Instance.PlaceholderTexture,
                        srcRect, dstRec,
                        Vector2.Zero, 0f,
                        drawColor
                    );
                }
            }
        }
        Raylib.EndShaderMode();
        Raylib.EndTextureMode();
    }

    public void DrawToolbar()
    {
        // rope-type props are only simulated while the "Simulate" button is held down
        // in their prop options
        wasRopeSimulationActive = isRopeSimulationActive;
        isRopeSimulationActive = false;

        foreach (var prop in RainEd.Instance.Level.Props)
        {
            if (prop.Rope is not null) prop.Rope.SimulationSpeed = 0f;
        }

        SelectorToolbar();
        OptionsToolbar();

        if (KeyShortcuts.Activated(KeyShortcut.ToggleVertexMode))
        {
            isWarpMode = !isWarpMode;
        }

        // shift+tab to switch between Tiles/Materials tabs
        if (KeyShortcuts.Activated(KeyShortcut.SwitchTab))
        {
            forceSelection = (SelectionMode)(((int)selectionMode + 1) % 2);
        }

        // tab to change view layer
        if (KeyShortcuts.Activated(KeyShortcut.SwitchLayer))
        {
            window.WorkLayer = (window.WorkLayer + 1) % 3;
        }

        if (isWarpMode)
            RainEd.Instance.LevelView.WriteStatus("Vertex Mode");

        // transform mode hints
        if (transformMode is ScaleTransformMode)
        {
            RainEd.Instance.LevelView.WriteStatus("Shift - Constrain proportion");
            RainEd.Instance.LevelView.WriteStatus("Ctrl - Scale by center");
        }
        else if (transformMode is RotateTransformMode)
        {
            RainEd.Instance.LevelView.WriteStatus("Shift - Snap rotation");
        }
        else if (transformMode is WarpTransformMode)
        {
            RainEd.Instance.LevelView.WriteStatus("Shift - Vertex snap");
        }

        if (isRopeSimulationActive)
        {
            RainEd.Instance.NeedScreenRefresh();
        }
        else if (wasRopeSimulationActive)
        {
            // push rope transform if simulation had just ended
            Log.Information("End rope simulation");
            changeRecorder.PushChanges();
        }
    }

    private void SelectorToolbar()
    {
        var propDb = RainEd.Instance.PropDatabase;

        if (KeyShortcuts.Activated(KeyShortcut.ChangePropSnapping))
        {
            // cycle through the four prop snap modes
            snappingMode = (PropSnapMode)(((int)snappingMode + 1) % 4);
        }

        if (KeyShortcuts.Activated(KeyShortcut.ChangePropSnapping))
        {
            // cycle through the four prop snap modes
            snappingMode = (PropSnapMode)(((int)snappingMode + 1) % 4);
        }

        if (ImGui.Begin("道具", ImGuiWindowFlags.NoFocusOnAppearing))
        {
            // work layer
            {
                int workLayerV = window.WorkLayer + 1;
                ImGui.SetNextItemWidth(ImGui.GetTextLineHeightWithSpacing() * 4f);
                ImGui.InputInt("视图层级", ref workLayerV);
                window.WorkLayer = Math.Clamp(workLayerV, 1, 3) - 1;
            }

            // snapping
            ImGui.SetNextItemWidth(ImGui.GetTextLineHeightWithSpacing() * 4f);
            {
                int snapModeInt = (int)snappingMode;
                if (ImGui.Combo("对齐", ref snapModeInt, "Off\00.25x\00.5x\01x\0"))
                    snappingMode = (PropSnapMode)snapModeInt;
            }

            // flags for search bar
            var searchInputFlags = ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EscapeClearsAll;

            if (ImGui.BeginTabBar("道具选择器"))
            {
                var halfWidth = ImGui.GetContentRegionAvail().X / 2f - ImGui.GetStyle().ItemSpacing.X / 2f;

                ImGuiTabItemFlags propsFlags = ImGuiTabItemFlags.None;
                ImGuiTabItemFlags tilesFlags = ImGuiTabItemFlags.None;

                // apply force selection
                if (forceSelection == SelectionMode.Props)
                    propsFlags = ImGuiTabItemFlags.SetSelected;
                else if (forceSelection == SelectionMode.Tiles)
                    tilesFlags = ImGuiTabItemFlags.SetSelected;

                // Props tab
                if (ImGuiExt.BeginTabItem("道具", propsFlags))
                {
                    if (selectionMode != SelectionMode.Props)
                    {
                        selectionMode = SelectionMode.Props;
                        ProcessSearch();
                    }

                    // search bar
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    if (ImGui.InputTextWithHint("##Search", "搜索...", ref searchQuery, 128, searchInputFlags))
                    {
                        ProcessSearch();
                    }

                    // group list box
                    var boxHeight = ImGui.GetContentRegionAvail().Y;
                    if (ImGui.BeginListBox("##Groups", new Vector2(halfWidth, boxHeight)))
                    {
                        const string leftPadding = "       ";
                        float colorWidth = ImGui.CalcTextSize(leftPadding).X - ImGui.GetStyle().ItemInnerSpacing.X;

                        foreach ((var i, var group) in searchResults)
                        {
                            // redundant skip Tiles as props categories
                            if (group.IsTileCategory) continue; // skip Tiles as props categories

                            var cursor = ImGui.GetCursorScreenPos();
                            ImGui.GetWindowDrawList().AddRectFilled(
                                p_min: cursor,
                                p_max: cursor + new Vector2(colorWidth, ImGui.GetTextLineHeight()),
                                ImGui.ColorConvertFloat4ToU32(new Vector4(group.Color.R / 255f, group.Color.G / 255f, group.Color.B / 255f, 1f))
                            );

                            if (ImGui.Selectable(leftPadding + group.Name, selectedPropGroup == i) || searchResults.Count == 1)
                            {
                                if (i != selectedPropGroup)
                                {
                                    selectedPropGroup = i;
                                    selectedPropIdx = 0;
                                }
                            }
                        }

                        ImGui.EndListBox();
                    }

                    // group listing (effects) list box
                    ImGui.SameLine();
                    if (ImGui.BeginListBox("##Props", new Vector2(halfWidth, boxHeight)))
                    {
                        var propList = propDb.Categories[selectedPropGroup].Props;

                        for (int i = 0; i < propList.Count; i++)
                        {
                            var prop = propList[i];

                            // don't show this prop if it doesn't pass search test
                            if (!prop.Name.Contains(searchQuery, StringComparison.CurrentCultureIgnoreCase))
                                continue;

                            if (ImGui.Selectable(prop.Name, i == selectedPropIdx))
                            {
                                selectedPropIdx = i;
                            }

                            if (ImGui.BeginItemTooltip())
                            {
                                UpdatePreview(prop);
                                ImGuiExt.ImageRenderTextureScaled(previewTexture, new Vector2(Boot.PixelIconScale, Boot.PixelIconScale));
                                ImGui.EndTooltip();
                            }
                        }

                        ImGui.EndListBox();
                    }

                    ImGui.EndTabItem();
                }

                // Tiles as props tab
                if (ImGuiExt.BeginTabItem("贴图", tilesFlags))
                {
                    // if tab changed, reset selected group back to 0
                    if (selectionMode != SelectionMode.Tiles)
                    {
                        selectionMode = SelectionMode.Tiles;
                        ProcessSearch();
                    }

                    // search bar
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    if (ImGui.InputTextWithHint("##Search", "搜索...", ref searchQuery, 128, searchInputFlags))
                    {
                        ProcessSearch();
                    }

                    // group list box
                    var boxHeight = ImGui.GetContentRegionAvail().Y;
                    if (ImGui.BeginListBox("##Groups", new Vector2(halfWidth, boxHeight)))
                    {
                        var drawList = ImGui.GetWindowDrawList();
                        float textHeight = ImGui.GetTextLineHeight();

                        const string leftPadding = "       ";
                        float colorWidth = ImGui.CalcTextSize(leftPadding).X - ImGui.GetStyle().ItemInnerSpacing.X;

                        foreach ((var i, var group) in tileSearchResults)
                        {
                            var cursor = ImGui.GetCursorScreenPos();
                            if (ImGui.Selectable(leftPadding + propDb.TileCategories[i].Name, selectedTileGroup == i) || tileSearchResults.Count == 1)
                            {
                                if (i != selectedTileGroup)
                                {
                                    selectedTileGroup = i;
                                    selectedTileIdx = 0;
                                }
                            }

                            // draw color square
                            drawList.AddRectFilled(
                                p_min: cursor,
                                p_max: cursor + new Vector2(colorWidth, textHeight),
                                ImGui.ColorConvertFloat4ToU32(new Vector4(group.Color.R / 255f, group.Color.G / 255f, group.Color.B / 255f, 1f))
                            );
                        }

                        ImGui.EndListBox();
                    }

                    // group listing (effects) list box
                    ImGui.SameLine();
                    if (ImGui.BeginListBox("##Props", new Vector2(halfWidth, boxHeight)))
                    {
                        var propList = propDb.TileCategories[selectedTileGroup].Props;

                        for (int i = 0; i < propList.Count; i++)
                        {
                            var prop = propList[i];

                            // don't show this prop if it doesn't pass search test
                            if (!prop.Name.Contains(searchQuery, StringComparison.CurrentCultureIgnoreCase))
                                continue;

                            if (ImGui.Selectable(prop.Name, selectedTileIdx == i))
                            {
                                selectedTileIdx = i;
                            }

                            if (ImGui.BeginItemTooltip())
                            {
                                UpdatePreview(prop);
                                ImGuiExt.ImageRenderTextureScaled(previewTexture, new Vector2(Boot.PixelIconScale, Boot.PixelIconScale));
                                ImGui.EndTooltip();
                            }
                        }

                        ImGui.EndListBox();
                    }

                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            forceSelection = null;

        }
        ImGui.End();

        // A/D to change selected group
        if (KeyShortcuts.Activated(KeyShortcut.NavLeft))
        {
            if (selectionMode == SelectionMode.Props)
            {
                while (true) // must skip over the hidden Tiles as props categories (i now doubt the goodiness of this idea)
                {
                    selectedPropGroup = Mod(selectedPropGroup - 1, propDb.Categories.Count);
                    var group = propDb.Categories[selectedPropGroup];
                    if (!group.IsTileCategory && group.Props.Count > 0) break;
                }

                selectedPropIdx = 0;
            }
            else if (selectionMode == SelectionMode.Tiles)
            {
                selectedTileGroup = Mod(selectedTileGroup - 1, propDb.TileCategories.Count);
                selectedTileIdx = 0;
            }
        }

        if (KeyShortcuts.Activated(KeyShortcut.NavRight))
        {
            if (selectionMode == SelectionMode.Props)
            {
                while (true) // must skip over the hidden Tiles as props categories (i now doubt the goodiness of this idea)
                {
                    selectedPropGroup = Mod(selectedPropGroup + 1, propDb.Categories.Count);
                    var group = propDb.Categories[selectedPropGroup];
                    if (!group.IsTileCategory && group.Props.Count > 0) break;
                }

                selectedPropIdx = 0;
            }
            else if (selectionMode == SelectionMode.Tiles)
            {
                selectedTileGroup = Mod(selectedTileGroup + 1, propDb.TileCategories.Count);
                selectedTileIdx = 0;
            }
        }

        // W/S to change selected tile in group
        if (KeyShortcuts.Activated(KeyShortcut.NavUp))
        {
            if (selectionMode == SelectionMode.Props)
            {
                var propList = propDb.Categories[selectedPropGroup].Props;
                selectedPropIdx = Mod(selectedPropIdx - 1, propList.Count);
            }
            else if (selectionMode == SelectionMode.Tiles)
            {
                var propList = propDb.TileCategories[selectedTileGroup].Props;
                selectedTileIdx = Mod(selectedTileIdx - 1, propList.Count);
            }
        }

        if (KeyShortcuts.Activated(KeyShortcut.NavDown))
        {
            if (selectionMode == SelectionMode.Props)
            {
                var propList = propDb.Categories[selectedPropGroup].Props;
                selectedPropIdx = Mod(selectedPropIdx + 1, propList.Count);
            }
            else if (selectionMode == SelectionMode.Tiles)
            {
                var propList = propDb.TileCategories[selectedTileGroup].Props;
                selectedTileIdx = Mod(selectedTileIdx + 1, propList.Count);
            }
        }

        // update selected init
        if (selectionMode == SelectionMode.Props)
        {
            selectedInit = propDb.Categories[selectedPropGroup].Props[selectedPropIdx];
        }
        else if (selectionMode == SelectionMode.Tiles)
        {
            selectedInit = propDb.TileCategories[selectedTileGroup].Props[selectedTileIdx];
        }
        else
        {
            throw new Exception("Prop Editor selectionMode is not Props or Tiles");
        }
    }

    private void OptionsToolbar()
    {
        if (ImGui.Begin("道具选项", ImGuiWindowFlags.NoFocusOnAppearing))
        {
            // prop transformation mode
            if (selectedProps.Count > 0)
            {
                if (selectedProps.Count == 1)
                {
                    var prop = selectedProps[0];

                    ImGui.TextUnformatted($"选择: {prop.PropInit.Name}");
                    ImGui.TextUnformatted($"纵深: {prop.DepthOffset} - {prop.DepthOffset + prop.PropInit.Depth}");
                }
                else
                {
                    ImGui.Text("选定多个道具");
                }

                var btnSize = new Vector2(ImGuiExt.ButtonGroup.CalcItemWidth(ImGui.GetContentRegionAvail().X, 4), 0);
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemInnerSpacing);

                if (ImGui.Button("重置", btnSize))
                {
                    changeRecorder.BeginTransform();
                    foreach (var prop in selectedProps)
                        prop.ResetTransform();
                    changeRecorder.PushChanges();
                }

                ImGui.SameLine();
                if (ImGui.Button("翻转 X", btnSize))
                {
                    changeRecorder.BeginTransform();
                    foreach (var prop in selectedProps)
                        prop.FlipX();
                    changeRecorder.PushChanges();
                }

                ImGui.SameLine();
                if (ImGui.Button("翻转 Y", btnSize))
                {
                    changeRecorder.BeginTransform();
                    foreach (var prop in selectedProps)
                        prop.FlipY();
                    changeRecorder.PushChanges();
                }

                ImGui.SameLine();
                if (ImGui.Button("Depth Move", btnSize))
                {
                    ImGui.OpenPopup("ZTranslate");
                    zTranslateValue = 0;
                    zTranslateDepths.Clear();
                    foreach (var prop in selectedProps)
                        zTranslateDepths.Add(prop, prop.DepthOffset);
                }

                zTranslateActive = false;
                if (ImGui.BeginPopup("ZTranslate"))
                {
                    zTranslateActive = true;
                    ImGui.PushItemWidth(ImGui.GetTextLineHeight() * 20f);
                    ImGui.SliderInt("##depth", ref zTranslateValue, -29, 29);
                    ImGui.PopItemWidth();

                    ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
                    ImGui.Checkbox("Wrap around", ref zTranslateWrap);
                    ImGui.PopStyleVar();

                    if (StandardPopupButtons.Show(PopupButtonList.OKCancel, out var btn))
                    {
                        zTranslateActive = false;

                        if (btn == 0)
                        {
                            changeRecorder.BeginTransform();
                            foreach (var prop in selectedProps)
                            {
                                prop.DepthOffset += zTranslateValue;
                                if (zTranslateWrap)
                                    prop.DepthOffset = Util.Mod(prop.DepthOffset, 30);
                                else
                                    prop.DepthOffset = Math.Clamp(prop.DepthOffset, 0, 29);
                            }
                            changeRecorder.PushChanges();
                        }

                        ImGui.CloseCurrentPopup();
                    }

                    ImGui.EndPopup();
                }

                ImGui.PopStyleVar();

                ImGui.PushItemWidth(Math.Max(
                    ImGui.GetTextLineHeightWithSpacing() * 12f,
                    ImGui.GetContentRegionAvail().X - ImGui.GetTextLineHeightWithSpacing() * 8f
                ));
                MultiselectDragInt("Render Order", "RenderOrder", 0.02f);
                MultiselectSliderInt("Depth Offset", "DepthOffset", 0, 29, "%i", ImGuiSliderFlags.AlwaysClamp);
                MultiselectSliderInt("Seed", "Seed", 0, 999);
                MultiselectEnumInput<Prop, PropRenderTime>(selectedProps, "Render Time", "RenderTime", PropRenderTimeNames);

                // custom depth, if available
                {
                    bool hasCustomDepth = true;
                    foreach (var prop in selectedProps)
                    {
                        if (!prop.PropInit.PropFlags.HasFlag(PropFlags.CustomDepthAvailable))
                        {
                            hasCustomDepth = false;
                            break;
                        }
                    }

                    if (hasCustomDepth)
                        MultiselectSliderInt("自定义纵深", "CustomDepth", 0, 30, "%i", ImGuiSliderFlags.AlwaysClamp);
                }

                // custom color, if available
                {
                    bool hasCustomColor = true;
                    foreach (var prop in selectedProps)
                    {
                        if (!prop.PropInit.PropFlags.HasFlag(PropFlags.CustomColorAvailable))
                        {
                            hasCustomColor = false;
                            break;
                        }
                    }

                    if (hasCustomColor)
                        MultiselectListInput("自定义颜色", "CustomColor", propColorNames);
                }

                // rope properties, if all selected props are ropes
                bool longProps = true;
                bool ropeProps = true;
                bool affineProps = true;
                {
                    // check if they're all affine
                    foreach (var prop in selectedProps)
                    {
                        if (!prop.IsAffine)
                        {
                            affineProps = false;
                            break;
                        }
                    }

                    // check if they're all long props
                    foreach (var prop in selectedProps)
                    {
                        if (!prop.IsLong)
                        {
                            longProps = false;
                            break;
                        }
                    }

                    // check if they're all rope props
                    foreach (var prop in selectedProps)
                    {
                        if (prop.Rope is null)
                        {
                            ropeProps = false;
                            break;
                        }
                    }

                    if (ropeProps)
                    {
                        if (affineProps)
                        {
                            // flexibility drag float
                            // can't make a MultiselectDragFloat function for this,
                            // cus it doesn't directly control a value
                            bool sameFlexi = true;
                            float targetFlexi = selectedProps[0].Rect.Size.Y / selectedProps[0].PropInit.Height;
                            float minFlexi = 0.5f / selectedProps[0].PropInit.Height;

                            for (int i = 1; i < selectedProps.Count; i++)
                            {
                                var prop = selectedProps[i];
                                float flexi = prop.Rect.Size.Y / prop.PropInit.Height;

                                if (MathF.Abs(flexi - targetFlexi) > 0.01f)
                                {
                                    sameFlexi = false;

                                    // idk why i did this cus every rope-type prop
                                    // starts out at the same size anyway
                                    float min = 0.5f / prop.PropInit.Height;
                                    if (min > minFlexi)
                                        minFlexi = min;

                                    break;
                                }
                            }

                            if (!sameFlexi)
                            {
                                targetFlexi = 1f;
                            }

                            // if not all props have the same flexibility value, the display text will be empty
                            // and interacting it will set them all to the default
                            if (ImGui.DragFloat("Flexibility", ref targetFlexi, 0.02f, minFlexi, float.PositiveInfinity, sameFlexi ? "%.2f" : "", ImGuiSliderFlags.AlwaysClamp))
                            {
                                foreach (var prop in selectedProps)
                                {
                                    prop.Rect.Size.Y = targetFlexi * prop.PropInit.Height;
                                }
                            }

                            if (ImGui.IsItemDeactivatedAfterEdit())
                                changeRecorder.PushSettingsChanges();
                        }

                        List<PropRope> ropes = new()
                        {
                            Capacity = selectedProps.Count
                        };
                        foreach (var p in selectedProps)
                            ropes.Add(p.Rope!);

                        MultiselectSwitchInput<PropRope, RopeReleaseMode>(ropes, "Release", "ReleaseMode", ["None", "Left", "Right"]);

                        if (selectedProps.Count == 1)
                        {
                            var prop = selectedProps[0];

                            // thickness
                            if (prop.PropInit.PropFlags.HasFlag(PropFlags.CanSetThickness))
                            {
                                ImGui.SliderFloat("Thickness", ref prop.Rope!.Thickness, 1f, 5f, "%.2f", ImGuiSliderFlags.AlwaysClamp);
                                if (ImGui.IsItemDeactivatedAfterEdit())
                                    changeRecorder.PushSettingsChanges();
                            }

                            // color Zero-G Tube white
                            if (prop.PropInit.PropFlags.HasFlag(PropFlags.Colorize))
                            {
                                if (ImGui.Checkbox("应用颜色", ref prop.ApplyColor))
                                    changeRecorder.PushSettingsChanges();
                            }
                        }

                        // rope simulation controls
                        if (affineProps)
                        {
                            if (ImGui.Button("重置模拟") || KeyShortcuts.Activated(KeyShortcut.ResetSimulation))
                            {
                                changeRecorder.BeginTransform();

                                foreach (var prop in selectedProps)
                                    prop.Rope!.ResetModel();

                                changeRecorder.PushChanges();
                            }

                            var simSpeed = 0f;

                            ImGui.SameLine();
                            ImGui.Button("模拟");

                            if ((ImGui.IsItemActive() || KeyShortcuts.Active(KeyShortcut.RopeSimulation)) && transformMode is null)
                            {
                                simSpeed = 1f;
                            }

                            ImGui.SameLine();
                            ImGui.Button("Fast");
                            if ((ImGui.IsItemActive() || KeyShortcuts.Active(KeyShortcut.RopeSimulationFast)) && transformMode is null)
                            {
                                simSpeed = RainEd.Instance.Preferences.FastSimulationSpeed;
                            }

                            if (simSpeed > 0f)
                            {
                                isRopeSimulationActive = true;

                                if (!wasRopeSimulationActive)
                                {
                                    changeRecorder.BeginTransform();
                                    Log.Information("Begin rope simulation");
                                }

                                foreach (var prop in selectedProps)
                                    prop.Rope!.SimulationSpeed = simSpeed;
                            }
                        }
                    }
                }

                if (selectedProps.Count == 1)
                {
                    var prop = selectedProps[0];

                    // if is a normal prop
                    if (!prop.IsLong)
                    {
                        // prop variation
                        if (prop.PropInit.VariationCount > 1)
                        {
                            var varV = prop.Variation + 1;
                            ImGui.SliderInt(
                                label: "Variation",
                                v: ref varV,
                                v_min: 1,
                                v_max: prop.PropInit.VariationCount,
                                format: varV == 0 ? "随机" : "%i",
                                flags: ImGuiSliderFlags.AlwaysClamp
                            );
                            prop.Variation = Math.Clamp(varV, 0, prop.PropInit.VariationCount) - 1;

                            if (ImGui.IsItemDeactivatedAfterEdit())
                                changeRecorder.PushSettingsChanges();
                        }

                        // apply color
                        if (prop.PropInit.PropFlags.HasFlag(PropFlags.Colorize))
                        {
                            if (ImGui.Checkbox("应用颜色", ref prop.ApplyColor))
                                changeRecorder.PushSettingsChanges();
                        }

                        //ImGui.BeginDisabled();
                        //    bool selfShaded = prop.PropInit.PropFlags.HasFlag(PropFlags.ProcedurallyShaded);
                        //    ImGui.Checkbox("Procedurally Shaded", ref selfShaded);
                        //ImGui.EndDisabled();
                    }

                    // notes
                }

                ImGui.SeparatorText("注意");

                if (longProps && !affineProps)
                {
                    ImGui.Bullet(); ImGui.SameLine();
                    ImGui.TextWrapped("一个或多个选定的绳子或长道具没有作为矩形加载，因此编辑受到限制。重置其变换以再次编辑它。");
                }

                if (selectedProps.Count == 1)
                {
                    var prop = selectedProps[0];

                    bool isDecal = prop.PropInit.Type == PropType.SimpleDecal || prop.PropInit.Type == PropType.VariedDecal;
                    if (!isDecal && prop.DepthOffset <= 5 && prop.DepthOffset + prop.CustomDepth >= 6)
                    {
                        ImGui.Bullet(); ImGui.SameLine();
                        ImGui.TextWrapped("警告:此道具将与游戏层相交(纵深5-6)！");
                    }

                    if (prop.PropInit.PropFlags.HasFlag(PropFlags.Tile))
                        ImGui.BulletText("作为道具的贴图");

                    if (prop.PropInit.PropFlags.HasFlag(PropFlags.Colorize))
                    {
                        ImGui.Bullet(); ImGui.SameLine();

                        if (prop.Rope is not null)
                        {
                            ImGui.TextWrapped("可以通过设置将管染成白色。");
                        }
                        else
                        {
                            ImGui.TextWrapped("如果颜色被激活，建议在效果之后渲染这个道具，因为效果不会影响颜色层。");
                        }
                    }

                    if (!prop.PropInit.PropFlags.HasFlag(PropFlags.ProcedurallyShaded))
                    {
                        ImGui.Bullet(); ImGui.SameLine();
                        ImGui.TextWrapped("请注意，阴影和高光不会随道具旋转，因此过度旋转可能会导致错误的着色。");
                    }

                    // user notes
                    foreach (string note in prop.PropInit.Notes)
                    {
                        ImGui.Bullet(); ImGui.SameLine();
                        ImGui.TextWrapped(note);
                    }
                }

                ImGui.PopItemWidth();
            }
            else
            {
                ImGui.Text("没有选择道具");
            }

        }
        ImGui.End();
    }

    public void ShowEditMenu()
    {
        KeyShortcuts.ImGuiMenuItem(KeyShortcut.Copy, "Copy");

        // TODO: grey this out if prop clipboard data is not available
        KeyShortcuts.ImGuiMenuItem(KeyShortcut.Paste, "Paste");

        KeyShortcuts.ImGuiMenuItem(KeyShortcut.Duplicate, "Duplicate Selected Prop(s)");
        KeyShortcuts.ImGuiMenuItem(KeyShortcut.RemoveObject, "Delete Selected Prop(s)");
        KeyShortcuts.ImGuiMenuItem(KeyShortcut.ToggleVertexMode, "Toggle Vertex Edit");
    }

    private static int Mod(int a, int b)
        => (a % b + b) % b;
}