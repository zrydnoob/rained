using System.Globalization;
using ImGuiNET;

namespace RainEd;

static class EmergencySaveWindow
{
    public const string WindowName = "��⵽���������ļ���";
    public static bool IsWindowOpen = false;

    private static string[] savList = [];
    private static string[] savDisplays = [];
    private static string[] dateList = [];

    private static int radio = -1;

    public static void UpdateList(string[] emSavList)
    {
        radio = -1;
        savList = new string[emSavList.Length];
        dateList = new string[emSavList.Length];
        savDisplays = new string[emSavList.Length];

        var culture = Boot.UserCulture;

        for (int i = 0; i < emSavList.Length; i++)
        {
            var writeTime = File.GetLastWriteTime(emSavList[i]);
            var levelName = Path.GetFileNameWithoutExtension(emSavList[i]);

            savList[i] = emSavList[i];
            savDisplays[i] = levelName[0..levelName.LastIndexOf('-')];
            dateList[i] = writeTime.ToString(culture.DateTimeFormat.ShortDatePattern, culture) + " �� " + writeTime.ToString(culture.DateTimeFormat.ShortTimePattern, culture);
        }

        if (emSavList.Length == 1) radio = 0;
    }

    public static void ShowWindow()
    {
        if (!ImGui.IsPopupOpen(WindowName) && IsWindowOpen)
        {
            ImGui.OpenPopup(WindowName);

            // center popup modal
            ImGuiExt.CenterNextWindow(ImGuiCond.Appearing);
        }

        if (ImGuiExt.BeginPopupModal(WindowName, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
        {
            ImGui.PushTextWrapPos(ImGui.GetTextLineHeight() * 32.0f);
            ImGui.TextWrapped("Rained��⵽һ�������������档�����ѡ����δ������ǡ�");
            ImGui.TextWrapped("���ѡ����ļ����������鿴�ؿ��������Ҫ��ʹ�á����Ϊ����ԭʼ�ؿ��ļ��滻Ϊ�������档");
            ImGui.PopTextWrapPos();
            
            var tableFlags = ImGuiTableFlags.RowBg;
            if (ImGui.BeginTable("���������б�", 2, tableFlags))
            {
                ImGui.TableSetupColumn("�ļ���");
                ImGui.TableSetupColumn("����");
                ImGui.TableHeadersRow();

                for (int i = 0; i < savList.Length; i++)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.RadioButton(savDisplays[i] + "##" + savList[i], ref radio, i);
                    ImGui.TableSetColumnIndex(1);
                    ImGui.TextUnformatted(dateList[i]);
                }

                ImGui.EndTable();
            }

            ImGui.Separator();

            if (ImGui.Button("��", StandardPopupButtons.ButtonSize) && radio >= 0)
            {
                RainEd.Instance.LoadLevel(savList[radio]);
                ImGui.CloseCurrentPopup();
                IsWindowOpen = false;
            }

            ImGui.SameLine();
            if (ImGui.Button("����", StandardPopupButtons.ButtonSize))
            {
                ImGui.CloseCurrentPopup();
                IsWindowOpen = false;
            }

            ImGui.SameLine();
            if (ImGui.Button("����", StandardPopupButtons.ButtonSize))
            {
                RainEd.DiscardEmergencySaves();
                ImGui.CloseCurrentPopup();
                IsWindowOpen = false;
            }

            ImGui.EndPopup();
        }
    }
}