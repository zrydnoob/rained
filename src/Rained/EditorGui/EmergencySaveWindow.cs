using System.Globalization;
using ImGuiNET;
namespace Rained.EditorGui;

static class EmergencySaveWindow
{
    public const string WindowName = "检测到紧急保存文件！";
    public static bool IsWindowOpen = false;

    private static string[] savList = [];
    private static string[] savDisplays = [];
    private static string[] dateList = [];

    public static void UpdateList(string[] emSavList)
    {
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
            dateList[i] = writeTime.ToString(culture.DateTimeFormat.ShortDatePattern, culture) + " 在 " + writeTime.ToString(culture.DateTimeFormat.ShortTimePattern, culture);
        }
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
            ImGui.TextWrapped("Rained检测到一个或多个紧急保存。你可以选择如何处理它们。");
            ImGui.TextWrapped("如果选择打开文件，建议您查看关卡，如果需要，使用“另存为”将原始关卡文件替换为紧急保存。");
            ImGui.PopTextWrapPos();

            var tableFlags = ImGuiTableFlags.RowBg;
            if (ImGui.BeginTable("紧急保存列表", 2, tableFlags))
            {
                ImGui.TableSetupColumn("文件名");
                ImGui.TableSetupColumn("日期");
                ImGui.TableHeadersRow();

                for (int i = 0; i < savList.Length; i++)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.TextUnformatted(savDisplays[i]);
                    ImGui.TableSetColumnIndex(1);
                    ImGui.TextUnformatted(dateList[i]);
                }

                ImGui.EndTable();
            }

            ImGui.Separator();

            if (ImGui.Button("打开所有", StandardPopupButtons.ButtonSize))
            {
                foreach (var save in savList)
                {
                    RainEd.Instance.LoadLevel(save);
                }

                ImGui.CloseCurrentPopup();
                IsWindowOpen = false;
            }

            ImGui.SameLine();
            if (ImGui.Button("忽略", StandardPopupButtons.ButtonSize))
            {
                ImGui.CloseCurrentPopup();
                IsWindowOpen = false;
            }

            ImGui.SameLine();
            if (ImGui.Button("放弃所有", StandardPopupButtons.ButtonSize))
            {
                RainEd.DiscardEmergencySaves();
                ImGui.CloseCurrentPopup();
                IsWindowOpen = false;
            }

            ImGui.EndPopup();
        }
    }
}