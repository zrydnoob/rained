using ImGuiNET;
using System.Numerics;

namespace RainEd;

static class LevelLoadFailedWindow
{
    public const string WindowName = "加载失败";
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
            ImGui.Text("由于无法识别的资产，无法加载该关卡。");

            // show unknown props
            if (LoadResult!.UnrecognizedProps.Length > 0)
            {
                ImGui.SeparatorText("无法识别的道具");
                foreach (var name in LoadResult.UnrecognizedProps)
                {
                    ImGui.BulletText(name);
                }
            }

            // show unknown tiles
            if (LoadResult!.UnrecognizedTiles.Length > 0)
            {
                ImGui.SeparatorText("无法识别的贴图");
                foreach (var name in LoadResult.UnrecognizedTiles)
                {
                    ImGui.BulletText(name);
                }
            }

            // show unknown materials
            if (LoadResult!.UnrecognizedMaterials.Length > 0)
            {
                ImGui.SeparatorText("无法识别的材料");
                foreach (var name in LoadResult.UnrecognizedMaterials)
                {
                    ImGui.BulletText(name);
                }
            }

            // show unknown effects
            if (LoadResult!.UnrecognizedEffects.Length > 0)
            {
                ImGui.SeparatorText("无法识别的特效");
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