using ImGuiNET;
using System.Numerics;
namespace RainEd;

enum PopupButtonList
{
    OK,
    OKCancel,
    YesNo
};

static class StandardPopupButtons
{
    public static Vector2 ButtonSize { get => new Vector2(ImGui.GetTextLineHeight() * 6f, 0f); }

    public static bool Show(PopupButtonList list, out int buttonPressed)
    {
        var size = ButtonSize;
        bool pressed = false;
        buttonPressed = -1;

        switch (list)
        {
            case PopupButtonList.OK:
                if (ImGui.Button("确定", size) || EditorWindow.IsKeyPressed(ImGuiKey.Space) || EditorWindow.IsKeyPressed(ImGuiKey.Enter) || EditorWindow.IsKeyPressed(ImGuiKey.Escape))
                {
                    pressed = true;
                    buttonPressed = 0;
                }
                break;
            
            case PopupButtonList.OKCancel:
                if (ImGui.Button("确定", size) || EditorWindow.IsKeyPressed(ImGuiKey.Space) || EditorWindow.IsKeyPressed(ImGuiKey.Enter))
                {
                    pressed = true;
                    buttonPressed = 0;
                }

                ImGui.SameLine();
                if (ImGui.Button("取消", size) || EditorWindow.IsKeyPressed(ImGuiKey.Escape))
                {
                    pressed = true;
                    buttonPressed = 1;
                }

                break;
            
            case PopupButtonList.YesNo:
                if (ImGui.Button("是", size) || EditorWindow.IsKeyPressed(ImGuiKey.Space) || EditorWindow.IsKeyPressed(ImGuiKey.Enter))
                {
                    pressed = true;
                    buttonPressed = 0;
                }

                ImGui.SameLine();
                if (ImGui.Button("否", size) || EditorWindow.IsKeyPressed(ImGuiKey.Escape))
                {
                    pressed = true;
                    buttonPressed = 1;
                }

                break;
        }

        return pressed;
    }
}