using ImGuiNET;
using System.Numerics;

namespace RainEd;

static class LevelLoadFailedWindow
{
    public const string WindowName = "����ʧ��";
    public static bool IsWindowOpen = false;

    public static LevelLoadResult? LoadResult = null;

    public static void ShowWindow()
    {
        if (!ImGui.IsPopupOpen(WindowName) && IsWindowOpen)
        {
            ImGui.OpenPopup(WindowName);

            // center popup modal
            ImGuiExt.CenterNextWindow(ImGuiCond.Appearing);
        }

        if (ImGui.BeginPopupModal(WindowName, ref IsWindowOpen, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
        {
            ImGui.Text("�����޷�ʶ����ʲ����޷����ظùؿ���");

            // show unknown props
            if (LoadResult!.UnrecognizedProps.Length > 0)
            {
                ImGui.SeparatorText("�޷�ʶ��ĵ���");
                foreach (var name in LoadResult.UnrecognizedProps)
                {
                    ImGui.BulletText(name);
                }
            }

            // show unknown tiles
            if (LoadResult!.UnrecognizedTiles.Length > 0)
            {
                ImGui.SeparatorText("�޷�ʶ�����ͼ");
                foreach (var name in LoadResult.UnrecognizedTiles)
                {
                    ImGui.BulletText(name);
                }
            }

            // show unknown materials
            if (LoadResult!.UnrecognizedMaterials.Length > 0)
            {
                ImGui.SeparatorText("�޷�ʶ��Ĳ���");
                foreach (var name in LoadResult.UnrecognizedMaterials)
                {
                    ImGui.BulletText(name);
                }
            }

            // show unknown effects
            if (LoadResult!.UnrecognizedEffects.Length > 0)
            {
                ImGui.SeparatorText("�޷�ʶ�����Ч");
                foreach (var name in LoadResult.UnrecognizedEffects)
                {
                    ImGui.BulletText(name);
                }
            }

            ImGui.Separator();
            if (StandardPopupButtons.Show(PopupButtonList.OK, out _))
            {
                ImGui.CloseCurrentPopup();
                IsWindowOpen = false;
            }

            ImGui.EndPopup();
        }
        else
        {
            LoadResult = null;
        }
    }
}