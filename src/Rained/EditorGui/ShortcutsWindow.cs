using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using ImGuiNET;
namespace RainEd;

static partial class ShortcutsWindow
{
    public static bool IsWindowOpen = false;
    
    private readonly static string[] NavTabs = new string[] { "����", "�����༭", "���α༭", "��Ƭ��ͼ�༭", "����༭", "�ƹ�༭", "��Ч�༭", "���߱༭" };

    private readonly static (string, string)[][] TabData = new (string, string)[][]
    {
        // General
        [
            ("������", "����"),
            ("[ViewZoomIn]/[ViewZoomOut]", "�Ŵ�/��С"),
            ("����м�", "�ƶ�"),
            ("Alt+������", "�ƶ�"),
            ("[Undo]", "����"),
            ("[Redo]", "����"),
            ("[Render]", "��Ⱦ"),
            ("[ExportGeometry]", "��ȾΪ�ؿ�.txt"),
            ("1", "�༭����"),
            ("2", "�༭����"),
            ("3", "�༭��Ƭ��ͼ"),
            ("4", "�༭���"),
            ("5", "�༭�ƹ�"),
            ("6", "�༭��Ч"),
            ("7", "�༭����"),
        ],

        // Environment
        [
            ("������", "����ˮ��߶�")
        ],

        // Geometry
        [
            ("[NavUp][NavLeft][NavDown][NavRight]", "ѡ����Ƭ"),
            ("������", "����/ɾ��"),
            ("����Ҽ�", "ɾ������"),
            ("Shift+������", "���ѡ��"),
            ("[FloodFill]+������", "��ˮ���"),
            ("[SwitchLayer]", "ѭ���㼶"),
            ("[ToggleLayer1]", "�л���1"),
            ("[ToggleLayer2]", "�л���2"),
            ("[ToggleLayer3]", "�л���3"),
            ("[ToggleMirrorX]", "�л�����X"),
            ("[ToggleMirrorY]", "�л�����Y")
        ],

        // Tile
        [
            ("[SwitchLayer]", "�л��㼶"),
            ("[SwitchTab]", "�л�ѡ����ѡ�"),
            ("[NavUp]/[NavDown]", "�����ѡ���"),
            ("[NavLeft]/[NavRight]", "�����Ƭ���"),
            ("Shift+������", "���Ĳ��ʱ�ˢ��С"),
            ("[DecreaseBrushSize]/[IncreaseBrushSize]", "���Ĳ��ʱ�ˢ��С"),
            ("[Eyedropper]", "�Ӳ㼶��ѡȡ����"),
            ("[SetMaterial]", "��ѡ����������ΪĬ�ϲ���"),
            ("������", "������ͼ/����"),
            ("����Ҽ�", "ɾ����ͼ/����"),
            ("Shift+������", "���������ͼ/����"),
            ("Shift+����Ҽ�", "����ɾ����ͼ/����"),
            ("[TileIgnoreDifferent]+������", "���Բ�ͬ�Ĳ���"),
            ("[TileIgnoreDifferent]+������", "���Բ��ʻ���Ƭ"),
            ("[TileForcePlacement]+������", "ǿ��ƽ�̷���"),
            ("[TileForceGeometry]+������", "ǿ��ƽ����Ƭ"),
            ("[TileForceGeometry]+����Ҽ�", "ɾ����ͼ����Ƭ"),
        ],

        // Camera
        [
            ("˫��", "�������"),
            ("[NewObject]", "�������"),
            ("������", "ѡ�����"),
            ("����Ҽ�", "�����������"),
            ("[RemoveObject]", "ɾ����ѡ���"),
            ("[Duplicate]", "����ѡ���������"),
            ("[CameraSnapX]/[NavUp]/[NavDown]", "��X�Ჶ׽���������"),
            ("[CameraSnapY]/[NavLeft]/[NavRight]", "��Y�Ჶ׽���������"),
        ],

        // Light
        [
            ("[NavUp][NavLeft][NavDown][NavRight]", "�������Ŀ¼"),
            ("[ZoomLightIn]", "�����ƶ�����"),
            ("[ZoomLightOut]", "�����ƶ�����"),
            ("[RotateLightCW]", "˳ʱ����ת����"),
            ("[RotateLightCCW]", "˳ʱ����ת����"),
            ("[ScaleLightBrush]+����ƶ�", "���ű�ˢ"),
            ("[RotateLightBrush]+����ƶ�", "��ת��ˢ"),
            ("[ResetBrushTransform]", "���ñ�ˢ�任"),
            ("������", "������Ӱ"),
            ("����Ҽ�", "���ƹ���"),
        ],

        // Effects
        [
            ("������", "������Ч"),
            ("Shift+������", "ǿ��������Ч"),
            ("����Ҽ�", "������Ч"),
            ("Shift+������", "���ı�ˢ��С"),
            ("[DecreaseBrushSize]/[IncreaseBrushSize]", "���ı�ˢ��С"),
        ],

        // Props
        [
            ("[SwitchLayer]", "�л��㼶"),
            ("[SwitchTab]", "�л�ѡ����ѡ�"),
            ("[NavUp]/[NavDown]", "�����ѡ���"),
            ("[NavLeft]/[NavRight]", "����������"),
            ("[Eyedropper]", "��ȡ����µĵ���"),
            ("�Ҽ����", "��������"),
            ("[NewObject]", "��������"),
            ("������", "ѡ�����"),
            ("Shift+������", "��������ӵ�ѡ���б�"),
            ("˫��", "��������µĵ���"),
            ("[RemoveObject]", "ɾ����ѡ����"),
            ("[ToggleVertexMode]", "�л�����ģʽ"),
            ("[Duplicate]", "����ѡ���ĵ���"),
            ("[RopeSimulation]", "ģ��ѡ������������")
        ]
    };

    public static void ShowWindow()
    {
        if (!IsWindowOpen) return;

        var editMode = RainEd.Instance.LevelView.EditMode;
        if (ImGui.Begin("��ݼ�", ref IsWindowOpen))
        {
            if (ImGui.BeginTabBar("��ݼ�"))
            {
                if (ImGui.BeginTabItem("����"))
                {
                    ShowTab(0);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("��ǰ�༭ģʽ"))
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
        } ImGui.End();
    }

    private static void ShowTab(int navTab)
    {
        var strBuilder = new StringBuilder();

        var tableFlags = ImGuiTableFlags.RowBg;
        if (ImGui.BeginTable("ControlTable", 2, tableFlags))
        {
            ImGui.TableSetupColumn("��ݼ�", ImGuiTableColumnFlags.WidthFixed, ImGui.GetTextLineHeight() * 10.0f);
            ImGui.TableSetupColumn("����");
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