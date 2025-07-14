using ImGuiNET;
using System.Numerics;
namespace Rained.EditorGui;

// This is a modified version of the code for ImGui.ShowStyleEditor(), converted to C#
static class ThemeEditor
{
    private static ImGuiStyle ref_saved_style = new();
    private static bool init = true;
    private static ImGuiTextFilter filter = new();
    private static ImGuiColorEditFlags alpha_flags = 0;

    private static FileBrowser? fileBrowser = null;

    public static event Action? ThemeSaved = null;

    private static void HelpMarker(string desc)
    {
        ImGui.TextDisabled("(?)");
        if (ImGui.BeginItemTooltip())
        {
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
            ImGui.TextUnformatted(desc);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }

    public static void SaveRef()
    {
        ImGuiExt.SaveStyleRef(ImGuiExt.Style, ref ref_saved_style);
    }

    public static void SaveStyle()
    {
        fileBrowser = new FileBrowser(FileBrowser.OpenMode.Write, SaveCallback, Path.Combine(Boot.AppDataPath, "config", "themes"));
        fileBrowser.AddFilter("JSON", [".json", ".jsonc"]);

        static void SaveCallback(string[] paths)
        {
            if (paths.Length == 0) return;
            var path = paths[0];

            var style = new SerializableStyle(ImGuiExt.Style);
            style.WriteToFile(path);
            RainEd.Instance.Preferences.Theme = Path.GetFileNameWithoutExtension(path);
            ThemeSaved?.Invoke();
        }
    }

    public static void LoadStyle()
    {
        fileBrowser = new FileBrowser(FileBrowser.OpenMode.Read, Callback, Path.Combine(Boot.AppDataPath, "config", "themes"));
        fileBrowser.AddFilter("JSON", [".json", ".jsonc"]);

        static void Callback(string[] paths)
        {
            if (paths.Length == 0) return;
            var path = paths[0];

            var style = SerializableStyle.FromFile(path);
            style?.Apply(ImGuiExt.Style);
        }
    }

    public static void Show()
    {
        // You can pass in a reference ImGuiStyle structure to compare to, revert to and save to
        // (without a reference style pointer, we will use one compared locally as a reference)
        var style = ImGuiExt.Style;

        // Default to using internal storage as reference
        if (init)
        {
            // (ImGuiStyle) ref_saved_style = (ImGuiStyle&) style
            ImGuiExt.SaveStyleRef(style, ref ref_saved_style);
        }
        
        init = false;
        ref ImGuiStyle @ref = ref ref_saved_style;
        
        ImGui.PushItemWidth(ImGui.GetWindowWidth() * 0.50f);

        //if (ImGui.ShowStyleSelector("Colors##Selector"))
        //    ImGuiExt.SaveStyleRef(style, ref ref_saved_style);
        //ImGui.ShowFontSelector("Fonts##Selector");

        // Simplified Settings (expose floating-pointer border sizes as boolean representing 0.0f or 1.0f)
        if (ImGui.SliderFloat("框架圆角", ref style.FrameRounding, 0.0f, 12.0f, "%.0f"))
            style.GrabRounding = style.FrameRounding; // Make GrabRounding always the same value as FrameRounding
        { bool border = style.WindowBorderSize > 0.0f; if (ImGui.Checkbox("窗口边框", ref border)) { style.WindowBorderSize = border ? 1.0f : 0.0f; } }
        ImGui.SameLine();
        { bool border = style.FrameBorderSize > 0.0f;  if (ImGui.Checkbox("框架边框",  ref border)) { style.FrameBorderSize  = border ? 1.0f : 0.0f; } }
        ImGui.SameLine();
        { bool border = style.PopupBorderSize > 0.0f;  if (ImGui.Checkbox("弹出框边框",  ref border)) { style.PopupBorderSize  = border ? 1.0f : 0.0f; } }

        // Save/Revert button
        if (ImGui.Button("保存"))
        {
            //@ref = ref_saved_style = style;
            ImGuiExt.SaveStyleRef(style, ref ref_saved_style);
            ImGuiExt.SaveStyleRef(style, ref @ref);
        }

        ImGui.SameLine();
        if (ImGui.Button("重置"))
            ImGuiExt.LoadStyleRef(@ref, style);
        ImGui.SameLine();
        HelpMarker("""
            在本地非持久性存储中保存 / 还原更改。默认颜色定义不受影响。
            请使用下方的 “保存到文件” 将其保存至某处。
            """);

        if (ImGui.Button("保存到文件"))
        {
            var styleState = new SerializableStyle(ImGuiExt.Style);
            styleState.WriteToFile(Path.Combine(Boot.AppDataPath, "config", "themes", RainEd.Instance.Preferences.Theme + ".json"));

            ImGuiExt.SaveStyleRef(style, ref ref_saved_style);
            ImGuiExt.SaveStyleRef(style, ref @ref);

            ThemeSaved?.Invoke();
        }

        ImGui.SameLine();
        if (ImGui.Button("文件另存为"))
        {
            SaveStyle();

            ImGuiExt.SaveStyleRef(style, ref ref_saved_style);
            ImGuiExt.SaveStyleRef(style, ref @ref);
        }

        /*ImGui.SameLine();
        if (ImGui.Button("Load from File"))
        {
            LoadStyle();
        }*/

        FileBrowser.Render(ref fileBrowser);

        ImGui.Separator();

        if (ImGui.BeginTabBar("##tabs", ImGuiTabBarFlags.None))
        {
            if (ImGui.BeginTabItem("Size"))
            {
                ImGui.SeparatorText("主要");
                ImGui.SliderFloat2("窗口内边距", ref style.WindowPadding, 0.0f, 20.0f, "%.0f");
                ImGui.SliderFloat2("框架内边距", ref style.FramePadding, 0.0f, 20.0f, "%.0f");
                ImGui.SliderFloat2("项目间距", ref style.ItemSpacing, 0.0f, 20.0f, "%.0f");
                ImGui.SliderFloat2("项目内部间距", ref style.ItemInnerSpacing, 0.0f, 20.0f, "%.0f");
                ImGui.SliderFloat2("触摸额外边距", ref style.TouchExtraPadding, 0.0f, 10.0f, "%.0f");
                ImGui.SliderFloat("缩进间距", ref style.IndentSpacing, 0.0f, 30.0f, "%.0f");
                ImGui.SliderFloat("滚动条尺寸", ref style.ScrollbarSize, 1.0f, 20.0f, "%.0f");
                ImGui.SliderFloat("抓取最小尺寸", ref style.GrabMinSize, 1.0f, 20.0f, "%.0f");

                ImGui.SeparatorText("边框");
                ImGui.SliderFloat("窗口边框宽度", ref style.WindowBorderSize, 0.0f, 1.0f, "%.0f");
                ImGui.SliderFloat("子窗口边框宽度", ref style.ChildBorderSize, 0.0f, 1.0f, "%.0f");
                ImGui.SliderFloat("弹出框边框宽度", ref style.PopupBorderSize, 0.0f, 1.0f, "%.0f");
                ImGui.SliderFloat("框架边框宽度", ref style.FrameBorderSize, 0.0f, 1.0f, "%.0f");
                ImGui.SliderFloat("标签页边框宽度", ref style.TabBorderSize, 0.0f, 1.0f, "%.0f");
                ImGui.SliderFloat("标签栏边框宽度", ref style.TabBarBorderSize, 0.0f, 2.0f, "%.0f");

                ImGui.SeparatorText("圆角");
                ImGui.SliderFloat("窗口圆角半径", ref style.WindowRounding, 0.0f, 12.0f, "%.0f");
                ImGui.SliderFloat("子窗口圆角半径", ref style.ChildRounding, 0.0f, 12.0f, "%.0f");
                ImGui.SliderFloat("框架圆角半径", ref style.FrameRounding, 0.0f, 12.0f, "%.0f");
                ImGui.SliderFloat("弹出框圆角半径", ref style.PopupRounding, 0.0f, 12.0f, "%.0f");
                ImGui.SliderFloat("滚动条圆角半径", ref style.ScrollbarRounding, 0.0f, 12.0f, "%.0f");
                ImGui.SliderFloat("抓取控件圆角半径", ref style.GrabRounding, 0.0f, 12.0f, "%.0f");
                ImGui.SliderFloat("标签页圆角半径", ref style.TabRounding, 0.0f, 12.0f, "%.0f");

                ImGui.SeparatorText("表格");
                ImGui.SliderFloat2("单元格内边距", ref style.CellPadding, 0.0f, 20.0f, "%.0f");
                ImGui.SliderAngle("表格倾斜表头角度", ref style.TableAngledHeadersAngle, -50.0f, +50.0f);

                ImGui.SeparatorText("控件");
                ImGui.SliderFloat2("窗口标题对齐方式", ref style.WindowTitleAlign, 0.0f, 1.0f, "%.2f");
                int window_menu_button_position = (int)style.WindowMenuButtonPosition + 1;
                if (ImGui.Combo("窗口菜单按钮位置", ref window_menu_button_position, "无\0左侧\0右侧\0"))
                    style.WindowMenuButtonPosition = (ImGuiDir)(window_menu_button_position - 1);               

                int colorButtonPos = (int)style.ColorButtonPosition;
                if (ImGui.Combo("颜色按钮位置", ref colorButtonPos, "左侧\0右侧\0"))
                    style.ColorButtonPosition = (ImGuiDir)colorButtonPos;               

                ImGui.SliderFloat2("按钮文本对齐方式", ref style.ButtonTextAlign, 0.0f, 1.0f, "%.2f");
                ImGui.SameLine(); HelpMarker("当按钮大于其文本内容时应用的对齐方式。");
                ImGui.SliderFloat2("可选文本对齐方式", ref style.SelectableTextAlign, 0.0f, 1.0f, "%.2f");
                ImGui.SameLine(); HelpMarker("当选中文本大于其内容时应用的对齐方式。");
                ImGui.SliderFloat("分隔符文本边框大小", ref style.SeparatorTextBorderSize, 0.0f, 10.0f, "%.0f");
                ImGui.SliderFloat2("分隔符文本对齐方式", ref style.SeparatorTextAlign, 0.0f, 1.0f, "%.2f");
                ImGui.SliderFloat2("分隔符文本内边距", ref style.SeparatorTextPadding, 0.0f, 40.0f, "%.0f");
                ImGui.SliderFloat("对数滑块死区", ref style.LogSliderDeadzone, 0.0f, 12.0f, "%.0f");

                /*
                TODO: put this in General preferences
                ImGui.SeparatorText("Tooltips");
                for (int n = 0; n < 2; n++)
                    if (ImGui.TreeNodeEx(n == 0 ? "HoverFlagsForTooltipMouse" : "HoverFlagsForTooltipNav"))
                    {
                        int p = (n == 0) ? (int)style.HoverFlagsForTooltipMouse : (int)style.HoverFlagsForTooltipNav;

                        ImGui.CheckboxFlags("ImGuiHoveredFlags_DelayNone", ref p, (int) ImGuiHoveredFlags.DelayNone);
                        ImGui.CheckboxFlags("ImGuiHoveredFlags_DelayShort", ref p, (int) ImGuiHoveredFlags.DelayShort);
                        ImGui.CheckboxFlags("ImGuiHoveredFlags_DelayNormal", ref p, (int) ImGuiHoveredFlags.DelayNormal);
                        ImGui.CheckboxFlags("ImGuiHoveredFlags_Stationary", ref p, (int) ImGuiHoveredFlags.Stationary);
                        ImGui.CheckboxFlags("ImGuiHoveredFlags_NoSharedDelay", ref p, (int) ImGuiHoveredFlags.NoSharedDelay);

                        if (n == 0)
                            style.HoverFlagsForTooltipMouse = (ImGuiHoveredFlags) p;
                        else
                            style.HoverFlagsForTooltipNav = (ImGuiHoveredFlags) p;
                        
                        ImGui.TreePop();
                    }

                ImGui.SeparatorText("Misc");
                ImGui.SliderFloat2("DisplaySafeAreaPadding", ref style.DisplaySafeAreaPadding, 0.0f, 30.0f, "%.0f"); ImGui.SameLine(); HelpMarker("Adjust if you cannot see the edges of your screen (e.g. on a TV where scaling has not been configured).");
                */

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Colors"))
            {
                /*if (ImGui.Button("Export"))
                {
                    if (output_dest == 0)
                        ImGui.LogToClipboard();
                    else
                        ImGui.LogToTTY();
                    ImGui.LogText("ImVec4* colors = ImGuiExt.StoredStyle.Colors;" + '\n');
                    for (int i = 0; i < (int)ImGuiCol.COUNT; i++)
                    {
                        ref Vector4 col = ref style.Colors[i];
                        string name = ImGui.GetStyleColorName((ImGuiCol) i);
                        //if (!output_only_modified || memcmp(&col, &ref->Colors[i], sizeof(ImVec4)) != 0)
                        if (!output_only_modified || col != @ref.GetColor(i))
                            ImGui.LogText(string.Format("colors[ImGuiCol_{0}]{1}= ImVec4({2}f, {3}f, {4}f, {5}f);\n",
                                name,
                                new string(' ', 23 - name.Length),
                                col.X.ToString("0.00", CultureInfo.InvariantCulture),
                                col.Y.ToString("0.00", CultureInfo.InvariantCulture),
                                col.Z.ToString("0.00", CultureInfo.InvariantCulture),
                                col.W.ToString("0.00", CultureInfo.InvariantCulture)
                            ));
                    }
                    ImGui.LogFinish();
                }
                ImGui.SameLine(); ImGui.SetNextItemWidth(120); ImGui.Combo("##output_type", ref output_dest, "To Clipboard\0To TTY\0");
                ImGui.SameLine(); ImGui.Checkbox("Only Modified Colors", ref output_only_modified);*/

                //ImGuiTextFilterPtr filterPtr = new()
                filter.FilterDraw("过滤颜色", ImGui.GetFontSize() * 16);

                if (ImGui.RadioButton("f", alpha_flags == ImGuiColorEditFlags.None))             { alpha_flags = ImGuiColorEditFlags.None; } ImGui.SameLine();
                if (ImGui.RadioButton("Alpha",  alpha_flags == ImGuiColorEditFlags.AlphaPreview))     { alpha_flags = ImGuiColorEditFlags.AlphaPreview; } ImGui.SameLine();
                if (ImGui.RadioButton("Both",   alpha_flags == ImGuiColorEditFlags.AlphaPreviewHalf)) { alpha_flags = ImGuiColorEditFlags.AlphaPreviewHalf; } ImGui.SameLine();
                HelpMarker(
                    "在颜色列表中:\n" +
                    "左键打开颜色选择器,\n" +
                    "右键打开编辑选项菜单");

                ImGui.SetNextWindowSizeConstraints(new Vector2(0.0f, ImGui.GetTextLineHeightWithSpacing() * 10), new(float.MaxValue, float.MaxValue));
                ImGui.BeginChild("##colors", Vector2.Zero, ImGuiChildFlags.Border | ImGuiChildFlags.NavFlattened, ImGuiWindowFlags.AlwaysVerticalScrollbar | ImGuiWindowFlags.AlwaysHorizontalScrollbar);
                ImGui.PushItemWidth(ImGui.GetFontSize() * -12);
                for (int i = 0; i < (int)ImGuiCol.COUNT; i++)
                {
                    string name = ImGui.GetStyleColorName((ImGuiCol) i);
                    if (!filter.FilterPassFilter(name))
                        continue;
                    ImGui.PushID(i);
                    
                    if (ImGui.Button("?"))
                        ImGui.DebugFlashStyleColor((ImGuiCol)i);
                    ImGui.SetItemTooltip("Flash given color to identify places where it is used.");
                    ImGui.SameLine();

                    ImGui.ColorEdit4("##color", ref style.Colors[i], ImGuiColorEditFlags.AlphaBar | alpha_flags);
                    //if (memcmp(&style.Colors[i], &ref->Colors[i], sizeof(ImVec4)) != 0)
                    if (style.Colors[i] != @ref.GetColor(i))
                    {
                        // Tips: in a real user application, you may want to merge and use an icon font into the main font,
                        // so instead of "Save"/"Revert" you'd use icons!
                        // Read the FAQ and docs/FONTS.md about using icon fonts. It's really easy and super convenient!
                        ImGui.SameLine(0.0f, style.ItemInnerSpacing.X); if (ImGui.Button("Save")) { @ref.SetColor(i, style.Colors[i]); }
                        ImGui.SameLine(0.0f, style.ItemInnerSpacing.X); if (ImGui.Button("Revert")) { style.Colors[i] = @ref.GetColor(i); }
                    }
                    ImGui.SameLine(0.0f, style.ItemInnerSpacing.X);
                    ImGui.TextUnformatted(name);
                    ImGui.PopID();
                }
                ImGui.PopItemWidth();
                ImGui.EndChild();

                ImGui.EndTabItem();
            }

            /*if (ImGui.BeginTabItem("Fonts"))
            {
                ImGuiIOPtr io = ImGui.GetIO();
                ImFontAtlasPtr atlas = io.Fonts;
                HelpMarker("Read FAQ and docs/FONTS.md for details on font loading.");
                ImGui.ShowFontAtlas(atlas);

                // Post-baking font scaling. Note that this is NOT the nice way of scaling fonts, read below.
                // (we enforce hard clamping manually as by default DragFloat/SliderFloat allows CTRL+Click text to get out of bounds).
                const float MIN_SCALE = 0.3f;
                const float MAX_SCALE = 2.0f;
                HelpMarker(
                    """
                    Those are old settings provided for convenience.
                    However, the _correct_ way of scaling your UI is currently to reload your font at the designed size, 
                    rebuild the font atlas, and call style.ScaleAllSizes() on a reference ImGuiStyle structure.
                    Using those settings here will give you poor quality results.
                    """);
                
                
                ImGui.PushItemWidth(ImGui.GetFontSize() * 8);
                if (ImGui.DragFloat("window scale", ref window_scale, 0.005f, MIN_SCALE, MAX_SCALE, "%.2f", ImGuiSliderFlags.AlwaysClamp)) // Scale only this window
                    ImGui.SetWindowFontScale(window_scale);
                ImGui.DragFloat("global scale", ref io.FontGlobalScale, 0.005f, MIN_SCALE, MAX_SCALE, "%.2f", ImGuiSliderFlags.AlwaysClamp); // Scale everything
                ImGui.PopItemWidth();

                ImGui.EndTabItem();
            }*/

            /*if (ImGui.BeginTabItem("Rendering"))
            {
                ImGui.Checkbox("Anti-aliased lines", &style.AntiAliasedLines);
                ImGui.SameLine();
                HelpMarker("When disabling anti-aliasing lines, you'll probably want to disable borders in your style as well.");

                ImGui.Checkbox("Anti-aliased lines use texture", &style.AntiAliasedLinesUseTex);
                ImGui.SameLine();
                HelpMarker("Faster lines using texture data. Require backend to render with bilinear filtering (not point/nearest filtering).");

                ImGui.Checkbox("Anti-aliased fill", &style.AntiAliasedFill);
                ImGui.PushItemWidth(ImGui.GetFontSize() * 8);
                ImGui.DragFloat("Curve Tessellation Tolerance", &style.CurveTessellationTol, 0.02f, 0.10f, 10.0f, "%.2f");
                if (style.CurveTessellationTol < 0.10f) style.CurveTessellationTol = 0.10f;

                // When editing the "Circle Segment Max Error" value, draw a preview of its effect on auto-tessellated circles.
                ImGui.DragFloat("Circle Tessellation Max Error", &style.CircleTessellationMaxError , 0.005f, 0.10f, 5.0f, "%.2f", ImGuiSliderFlags_AlwaysClamp);
                const bool show_samples = ImGui.IsItemActive();
                if (show_samples)
                    ImGui.SetNextWindowPos(ImGui.GetCursorScreenPos());
                if (show_samples && ImGui.BeginTooltip())
                {
                    ImGui.TextUnformatted("(R = radius, N = number of segments)");
                    ImGui.Spacing();
                    ImDrawList* draw_list = ImGui.GetWindowDrawList();
                    const float min_widget_width = ImGui.CalcTextSize("N: MMM\nR: MMM").x;
                    for (int n = 0; n < 8; n++)
                    {
                        const float RAD_MIN = 5.0f;
                        const float RAD_MAX = 70.0f;
                        const float rad = RAD_MIN + (RAD_MAX - RAD_MIN) * (float)n / (8.0f - 1.0f);

                        ImGui.BeginGroup();

                        ImGui.Text("R: %.f\nN: %d", rad, draw_list->_CalcCircleAutoSegmentCount(rad));

                        const float canvas_width = IM_MAX(min_widget_width, rad * 2.0f);
                        const float offset_x     = floorf(canvas_width * 0.5f);
                        const float offset_y     = floorf(RAD_MAX);

                        const ImVec2 p1 = ImGui.GetCursorScreenPos();
                        draw_list->AddCircle(ImVec2(p1.x + offset_x, p1.y + offset_y), rad, ImGui.GetColorU32(ImGuiCol_Text));
                        ImGui.Dummy(ImVec2(canvas_width, RAD_MAX * 2));

                        //const ImVec2 p2 = ImGui.GetCursorScreenPos();
                        //draw_list->AddCircleFilled(ImVec2(p2.x + offset_x, p2.y + offset_y), rad, ImGui.GetColorU32(ImGuiCol_Text));
                        //ImGui.Dummy(ImVec2(canvas_width, RAD_MAX * 2));

                        ImGui.EndGroup();
                        ImGui.SameLine();
                    }
                    ImGui.EndTooltip();
                }
                ImGui.SameLine();
                HelpMarker("When drawing circle primitives with \"num_segments == 0\" tesselation will be calculated automatically.");

                ImGui.DragFloat("Global Alpha", &style.Alpha, 0.005f, 0.20f, 1.0f, "%.2f"); // Not exposing zero here so user doesn't "lose" the UI (zero alpha clips all widgets). But application code could have a toggle to switch between zero and non-zero.
                ImGui.DragFloat("Disabled Alpha", &style.DisabledAlpha, 0.005f, 0.0f, 1.0f, "%.2f"); ImGui.SameLine(); HelpMarker("Additional alpha multiplier for disabled items (multiply over current value of Alpha).");
                ImGui.PopItemWidth();

                ImGui.EndTabItem();
            }*/

            ImGui.EndTabBar();
        }

        ImGui.PopItemWidth();
    }
}