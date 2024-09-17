using ImGuiNET;

using System.Runtime.InteropServices;
namespace RainEd;

static class AboutWindow
{
    private const string WindowName = "About Rained";
    public static bool IsWindowOpen = false;

    private static RlManaged.Texture2D? rainedLogo0 = null;
    private static RlManaged.Texture2D? rainedLogo1 = null;

    record SystemInfo(string FrameworkName, string OsName, string Arch, string GraphicsAPI, string GraphicsVendor, string GraphicsRenderer);
    private static SystemInfo? systemInfo;

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
            rainedLogo0 ??= RlManaged.Texture2D.Load(Path.Combine(Boot.AppDataPath,"assets","rained-logo-colorless.png"));
            rainedLogo1 ??= RlManaged.Texture2D.Load(Path.Combine(Boot.AppDataPath,"assets","rained-logo-color.png"));

            ImGui.SameLine(Math.Max(0f, (ImGui.GetWindowWidth() - rainedLogo0.Width) / 2.0f));
            
            // draw rained logo, with the outline colored according to the theme 
            var initCursor = ImGui.GetCursorPos();
            var themeColor = ImGui.GetStyle().Colors[(int) ImGuiCol.Button];
            ImGuiExt.Image(rainedLogo0);
            ImGui.SetCursorPos(initCursor);
            ImGuiExt.Image(rainedLogo1.GlibTexture!, new Glib.Color(themeColor.X, themeColor.Y, themeColor.Z, themeColor.W));
            
            ImGui.Text("һ��������ؿ��༭�� - " + RainEd.Version);
            ImGui.NewLine();
            ImGui.Text("(c) 2024 pkhead - MIT License");
            ImGui.Text("Rain World - Videocult/Adult Swim Games/Akapura Games");

            ImGui.Bullet();
            ImGuiExt.LinkText("GitHub", "https://github.com/pkhead/rained");

            ImGui.Bullet();
            ImGuiExt.LinkText("Credits", "CREDITS.md");

            // notify user of a new version
            if (RainEd.Instance.LatestVersionInfo is not null && RainEd.Instance.LatestVersionInfo.VersionName != RainEd.Version)
            {
                ImGui.NewLine();
                ImGui.Text("�����°汾��");
                ImGui.SameLine();
                ImGuiExt.LinkText(RainEd.Instance.LatestVersionInfo.VersionName, RainEd.Instance.LatestVersionInfo.GitHubReleaseUrl);
            }

            ImGui.SeparatorText("ϵͳ��Ϣ:");
            {
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