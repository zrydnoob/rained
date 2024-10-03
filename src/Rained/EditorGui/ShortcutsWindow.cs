using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using ImGuiNET;
namespace RainEd;

static partial class ShortcutsWindow
{
    public static bool IsWindowOpen = false;

    private readonly static string[] NavTabs = new string[] { "常规", "环境编辑", "几何编辑", "瓦片贴图编辑", "相机编辑", "灯光编辑", "特效编辑", "道具编辑" };

    private readonly static (string, string)[][] TabData = new (string, string)[][]
    {
        // General
        [
            ("鼠标滚轮", "缩放"),
            ("[ViewZoomIn]/[ViewZoomOut]", "放大/缩小"),
            ("鼠标中键", "移动"),
            ("Alt+鼠标左键", "移动"),
            ("[Undo]", "撤销"),
            ("[Redo]", "重做"),
            ("[Render]", "渲染"),
            ("[ExportGeometry]", "渲染为关卡.txt"),
            ("1", "编辑环境"),
            ("2", "编辑几何"),
            ("3", "编辑瓦片贴图"),
            ("4", "编辑相机"),
            ("5", "编辑灯光"),
            ("6", "编辑特效"),
            ("7", "编辑道具"),
        ],

        // Environment
        [
            ("鼠标左键", "设置水面高度")
        ],

        // Geometry
        [
            ("[NavUp][NavLeft][NavDown][NavRight]", "选择瓦片"),
            ("鼠标左键", "放置/删除"),
            ("鼠标右键", "删除对象"),
            ("Shift+鼠标左键", "填充选区"),
            ("[FloodFill]+鼠标左键", "洪水填充"),
            ("[SwitchLayer]", "循环层级"),
            ("[ToggleLayer1]", "切换层1"),
            ("[ToggleLayer2]", "切换层2"),
            ("[ToggleLayer3]", "切换层3"),
            ("[ToggleMirrorX]", "切换镜像X"),
            ("[ToggleMirrorY]", "切换镜像Y")
        ],

        // Tile
        [
            ("[SwitchLayer]", "切换层级"),
            ("[SwitchTab]", "切换选择器选项卡"),
            ("[NavUp]/[NavDown]", "浏览所选类别"),
            ("[NavLeft]/[NavRight]", "浏览瓦片类别"),
            ("Shift+鼠标滚轮", "更改材质笔刷大小"),
            ("[DecreaseBrushSize]/[IncreaseBrushSize]", "更改材质笔刷大小"),
            ("[Eyedropper]", "从层级中选取材质"),
            ("[SetMaterial]", "将选定内容设置为默认材质"),
            ("鼠标左键", "放置贴图/材料"),
            ("鼠标右键", "删除贴图/材料"),
            ("Shift+鼠标左键", "矩形填充贴图/材料"),
            ("Shift+鼠标右键", "矩形删除贴图/材料"),
            ("[TileIgnoreDifferent]+鼠标左键", "忽略不同的材料"),
            ("[TileIgnoreDifferent]+鼠标左键", "忽略材质或瓦片"),
            ("[TileForcePlacement]+鼠标左键", "强制平铺放置"),
            ("[TileForceGeometry]+鼠标左键", "强制平铺瓦片"),
            ("[TileForceGeometry]+鼠标右键", "删除贴图及瓦片"),
        ],

        // Camera
        [
            ("双击", "创建相机"),
            ("[NewObject]", "创建相机"),
            ("鼠标左键", "选择相机"),
            ("鼠标右键", "重置摄像机角"),
            ("[RemoveObject]", "删除所选相机"),
            ("[Duplicate]", "复制选定的摄像机"),
            ("[CameraSnapX]/[NavUp]/[NavDown]", "将X轴捕捉到其他相机"),
            ("[CameraSnapY]/[NavLeft]/[NavRight]", "将Y轴捕捉到其他相机"),
        ],

        // Light
        [
            ("[NavUp][NavLeft][NavDown][NavRight]", "浏览画笔目录"),
            ("[ZoomLightIn]", "向内移动光线"),
            ("[ZoomLightOut]", "向外移动光线"),
            ("[RotateLightCW]", "顺时针旋转光线"),
            ("[RotateLightCCW]", "顺时针旋转光线"),
            ("[ScaleLightBrush]+鼠标移动", "缩放笔刷"),
            ("[RotateLightBrush]+鼠标移动", "旋转笔刷"),
            ("[ResetBrushTransform]", "重置笔刷变换"),
            ("鼠标左键", "绘制阴影"),
            ("鼠标右键", "绘制光线"),
        ],

        // Effects
        [
            ("鼠标左键", "绘制特效"),
            ("Shift+鼠标左键", "强力绘制特效"),
            ("鼠标右键", "擦除特效"),
            ("Shift+鼠标滚轮", "更改笔刷大小"),
            ("[DecreaseBrushSize]/[IncreaseBrushSize]", "更改笔刷大小"),
        ],

        // Props
        [
            ("[SwitchLayer]", "切换层级"),
            ("[SwitchTab]", "切换选择器选项卡"),
            ("[NavUp]/[NavDown]", "浏览所选类别"),
            ("[NavLeft]/[NavRight]", "浏览道具类别"),
            ("[Eyedropper]", "吸取鼠标下的道具"),
            ("右键点击", "创建道具"),
            ("[NewObject]", "创建道具"),
            ("鼠标左键", "选择道具"),
            ("Shift+鼠标左键", "将道具添加到选择列表"),
            ("双击", "查早鼠标下的道具"),
            ("[RemoveObject]", "删除所选道具"),
            ("[ToggleVertexMode]", "切换顶点模式"),
            ("[Duplicate]", "复制选定的道具"),
            ("[RopeSimulation]", "模拟选定的绳索道具")
        ]
    };

    public static void ShowWindow()
    {
        if (!IsWindowOpen) return;

        var editMode = RainEd.Instance.LevelView.EditMode;
        if (ImGui.Begin("快捷键", ref IsWindowOpen))
        {
            if (ImGui.BeginTabBar("快捷键"))
            {
                if (ImGui.BeginTabItem("常规"))
                {
                    ShowTab(0);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("当前编辑模式"))
                {
                    ShowTab(editMode + 1);
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
            /*var halfWidth = ImGui.GetTextLineHeight() * 30.0f;
            ImGui.BeginChild("General", new Vector2(halfWidth, ImGui.GetContentRegionAvail().Y));
            ShowTab(0);
            ImGui.EndChild();

            ImGui.SameLine();
            ImGui.BeginChild("Edit Mode", ImGui.GetContentRegionAvail());
            ShowTab(editMode + 1);
            ImGui.EndChild();*/
            /*ImGui.BeginChild("Nav", new Vector2(ImGui.GetTextLineHeight() * 12.0f, ImGui.GetContentRegionAvail().Y), ImGuiChildFlags.Border);
            {
                for (int i = 0; i < NavTabs.Length; i++)
                {
                    if (ImGui.Selectable(NavTabs[i], i == selectedNavTab))
                    {
                        selectedNavTab = i;
                    }
                }
            }
            ImGui.EndChild();

            ImGui.SameLine();
            ImGui.BeginChild("Controls", ImGui.GetContentRegionAvail());
            ShowTab();
            ImGui.EndChild();*/
        }
        ImGui.End();
    }

    private static void ShowTab(int navTab)
    {
        var strBuilder = new StringBuilder();

        var tableFlags = ImGuiTableFlags.RowBg;
        if (ImGui.BeginTable("ControlTable", 2, tableFlags))
        {
            ImGui.TableSetupColumn("快捷键", ImGuiTableColumnFlags.WidthFixed, ImGui.GetTextLineHeight() * 10.0f);
            ImGui.TableSetupColumn("功能");
            ImGui.TableHeadersRow();

            var tabData = TabData[navTab];

            for (int i = 0; i < tabData.Length; i++)
            {
                var tuple = tabData[i];
                var str = ShortcutRegex().Replace(tuple.Item1, ShortcutEvaluator);

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.Text(str);
                ImGui.TableSetColumnIndex(1);
                ImGui.Text(tuple.Item2);
            }

            ImGui.EndTable();
        }
    }

    private static string ShortcutEvaluator(Match match)
    {
        var shortcutId = Enum.Parse<KeyShortcut>(match.Value[1..^1]);
        return KeyShortcuts.GetShortcutString(shortcutId);
    }

    [GeneratedRegex("\\[(\\w+?)\\]")]
    private static partial Regex ShortcutRegex();
}