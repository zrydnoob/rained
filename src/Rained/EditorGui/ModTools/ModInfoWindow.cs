using System.Numerics;
using ImGuiNET;

namespace Rained;

public class ModInfoWindow
{
    public const string WindowTitle = "modInfo 文件生成工具";
    public static bool IsWindowOpen = false;

    private static string ModId = "";
    private static string ModName = "";
    private static string ModVersion = "";
    private static string ModAuthors = "";
    private static string ModDescription = "";
    private static List<string> ModRequirements = ["demo1","demo2","demo3"];
    private static List<string> ModRequirementsNames = [];

    private static bool ChecksumOverrideVersion = false;

    public static void ShowWindow(){
        if (!IsWindowOpen) return;

        var winFlags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDocking;
        if (ImGui.Begin(WindowTitle,ref IsWindowOpen, winFlags))
        {
            ImGui.SeparatorText("基础信息");
            {
                ImGui.InputTextWithHint("Mod 名称","mod name",ref ModName, 128);
                ImGui.InputTextWithHint("Mod Id", "Mod Id", ref ModId, 128);
                ImGui.InputTextWithHint("Mod 版本","mod version",ref ModVersion, 128);
                ImGui.InputTextWithHint("Mod 作者","mod author",ref ModAuthors, 128);
                ImGui.InputTextMultiline("Mod 描述", ref ModDescription, 1024, new Vector2(0,100));
            }

            ImGui.SeparatorText("依赖信息");
            {
                var selectedRequirement = "";
                if (ImGui.BeginListBox("依赖列表"))
                {
                    foreach(var i in ModRequirements)
                    {
                        if (ImGui.Selectable(i, selectedRequirement == i) || ModRequirements.Count == 1)
                            selectedRequirement = i;
                    }
                    ImGui.EndListBox();
                    ImGui.Button("添加");
                    ImGui.SameLine();
                    ImGui.Button("修改");
                    ImGui.SameLine();
                    ImGui.Button("移除");
                }
            }

            ImGui.SeparatorText("更新");
            {
                ImGui.Checkbox("checksum_override_version", ref ChecksumOverrideVersion);
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("一个可选的布尔参数。 如果为 false ，如果在 mod 的文件夹中发现任何文件的更改，则该 mod 将被视为“需要更新”。 \n如果为 true，则仅当 version 参数的值发生更改时，才认为 mod “需要更新”。");
                }
            }

            ImGui.Separator();

            if (ImGui.Button("生成"))
            {
                GenerateModInfoFile();
            }
        }
        ImGui.End();
    }

    public static void GenerateModInfoFile()
    {

    }
}
