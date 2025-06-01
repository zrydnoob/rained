using ImGuiNET;
using Rained.LevelData;
namespace Rained.EditorGui;

static class LevelLoadFailedWindow
{
    public const string WindowName = "未识别的资产";
    public static bool IsWindowOpen = false;

    public static LevelLoadResult? LoadResult = null;

    public static Action? LoadAnywayCallback = null;

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
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35f);
            ImGui.TextWrapped("该关卡包含无法识别的资产。试图在这种状态下加载关卡将会删除资产实例。");

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
                ImGui.SeparatorText("无法识别的效果");
                foreach (var name in LoadResult.UnrecognizedEffects)
                {
                    ImGui.BulletText(name);
                }
            }

            if (LoadResult!.UnrecognizedTiles.Length > 0)
            {
                ImGui.TextWrapped("You will need to turn off \"Optimized Tile Previews\" to see the bodies of the affected tiles.");
            }

            ImGui.PopTextWrapPos();
            ImGui.Separator();

            if (ImGui.Button("Load anyway", StandardPopupButtons.ButtonSize))
            {
                LoadAnywayCallback?.Invoke();

                ImGui.CloseCurrentPopup();
                IsWindowOpen = false;
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel", StandardPopupButtons.ButtonSize))
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