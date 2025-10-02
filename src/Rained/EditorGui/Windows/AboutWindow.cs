using ImGuiNET;
using Rained.LuaScripting;
using System.Runtime.InteropServices;
namespace Rained.EditorGui;

static class AboutWindow
{
    private const string WindowName = "About Rained";
    public static bool IsWindowOpen = false;

    record SystemInfo(string FrameworkName, string OsName, string Arch, string GraphicsAPI, string GraphicsVendor, string GraphicsRenderer);
    private static SystemInfo? systemInfo;
    private readonly static Version? drizzleVersion =
        typeof(global::Drizzle.Lingo.Runtime.LingoRuntime).Assembly.GetName().Version;

    private static SystemInfo GetSystemInfo()
    {
        string osName = RuntimeInformation.OSDescription;
        string frameworkName = RuntimeInformation.FrameworkDescription;
        var arch = RuntimeInformation.OSArchitecture;
        string archName = arch switch
        {
            Architecture.X86 => "x86",
            Architecture.X64 => "x64",
            Architecture.Arm => "arm",
            Architecture.Arm64 => "arm64",
            Architecture.Wasm => "wasm",
            Architecture.S390x => "s390x",
            Architecture.LoongArch64 => "LoongArch64",
            Architecture.Armv6 => "armV6",
            Architecture.Ppc64le => "ppc64le",
            _ => "unknown"
        };

        var rctx = RainEd.RenderContext!;
        systemInfo = new SystemInfo(frameworkName, osName, archName, rctx.GraphicsAPI, rctx.GpuVendor, rctx.GpuRenderer);
        return systemInfo;
    }

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
            ImGui.SameLine(Math.Max(0f, (ImGui.GetWindowWidth() - RainedLogo.Width) / 2.0f));
            RainedLogo.Draw();
            
            ImGui.Text("一个雨世界关卡编辑器 - " + RainEd.Version);
            ImGui.NewLine();
            ImGui.Text("(c) 2024-2025 pkhead - MIT License");
            ImGui.Text("Rain World - Videocult/Adult Swim Games/Akapura Games");

            ImGui.Bullet();
            ImGuiExt.LinkText("GitHub", "https://github.com/pkhead/rained");

            ImGui.Bullet();
            ImGuiExt.LinkText("Credits", "CREDITS.md");

            // notify user of a new version
            if (RainEd.Instance.LatestVersionInfo is not null && RainEd.Instance.LatestVersionInfo.VersionName != RainEd.Version)
            {
                ImGui.NewLine();
                ImGui.Text("发现新版本！");
                ImGui.SameLine();
                ImGuiExt.LinkText(RainEd.Instance.LatestVersionInfo.VersionName, RainEd.Instance.LatestVersionInfo.GitHubReleaseUrl);
            }

            ImGui.Separator();
            ImGui.SeparatorText("特别说明:");
            {
                ImGui.Text("Rained是由 @pkhead 创建，并使用 MIT 许可证发布的。");
                ImGui.Text("Rain World - Videocult/Adult Swim Games/Akapura Games");
                ImGui.Text("此汉化版本是Rained的二次开发，并在汉化基础上进行了改动并添加了新功能。");
                ImGui.Text("尊重原作者的成果，尊重游戏开发者，以下列举在Rained基础上进行的改动");
                ImGui.Text("- 添加了Rain World中文本地化。");
                ImGui.Text("- 添加了效果(Effect)的部分预览缩略图。此部分缩略图是从RWE+ (MIT 协议) 中获取的");
                ImGui.Bullet();
                ImGuiExt.LinkText("RWE+", "https://github.com/timofey260/RWE-Plus");
                ImGui.Text("- 修改了AutoTile的逻辑来支持鼠标右键清除AutoTile");
                ImGui.Text("- 添加了 mod 工具 菜单，来方便生成修改各种文件");
                ImGui.Text("- 修改了更新链接，此后从汉化版仓库中检查更新");
                ImGui.Text("- 添加了Data的下载地址，下载加速服务由llkk.cc提供");
                ImGuiExt.LinkText("llkk.cc", "https://gh.llkk.cc/");
                ImGui.Bullet();
                ImGuiExt.LinkText("Rained Github", "https://github.com/pkhead/rained");
                ImGui.Bullet();
                ImGuiExt.LinkText("汉化版 Rained", "https://github.com/zrydnoob/rained");
            }

            ImGui.SeparatorText("系统信息:");
            {
                if (drizzleVersion is not null)
                    ImGui.BulletText($"Drizzle: {drizzleVersion}");
                ImGui.BulletText($"Lua API: {LuaInterface.VersionMajor}.{LuaInterface.VersionMinor}.{LuaInterface.VersionRevision}");

                ImGui.Separator();
                
                var sysInfo = systemInfo ?? GetSystemInfo();
                ImGui.BulletText(".NET: " + sysInfo.FrameworkName);
                ImGui.BulletText("OS: " + sysInfo.OsName);
                ImGui.BulletText("Arch: " + sysInfo.Arch);
                ImGui.BulletText("Graphics API: " + sysInfo.GraphicsAPI);
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 40.0f);
                ImGui.Bullet();
                ImGui.TextWrapped("Gfx Vendor: " + sysInfo.GraphicsVendor);
                ImGui.Bullet();
                ImGui.TextWrapped("Gfx Driver: " + sysInfo.GraphicsRenderer);
                ImGui.PopTextWrapPos();
            }

            ImGui.EndPopup();
        }
    }
}