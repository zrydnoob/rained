namespace Rained.EditorGui;

using System.Numerics;
using System.Diagnostics;
using ImGuiNET;
using Rained.Drizzle;

class MassRenderProcessWindow
{
    public const string WindowName = "批量渲染###MassRenderProc";
    private readonly Task renderTask;
    private readonly CancellationTokenSource cancelSource;

    public bool IsDone { get; private set; } = false;
    private bool cancel = false;
    private bool renderBegan = false;
    private int renderedLevels = 0;
    private int totalLevels = 1;
    private readonly Dictionary<string, float> levelProgress = [];
    private readonly List<string> problematicLevels = [];

    private Stopwatch elapsedStopwatch = new();
    private bool showTime = false;

    public MassRenderProcessWindow(DrizzleMassRender renderProcess)
    {
        cancelSource = new CancellationTokenSource();

        var prog = new Progress<MassRenderNotification>();
        prog.ProgressChanged += RenderProgressChanged;

        var ct = cancelSource.Token;
        //renderTask = null!;
        renderTask = Task.Run(() =>
        {
            renderProcess.Start(prog, ct);
        }, ct);
    }

    public void Render()
    {
        if (IsDone) return;

        if (!ImGui.IsPopupOpen(WindowName))
        {
            ImGui.OpenPopup(WindowName);

            // center popup modal
            ImGuiExt.CenterNextWindow(ImGuiCond.Appearing);
        }

        ImGui.SetNextWindowSizeConstraints(new Vector2(0f, ImGui.GetTextLineHeight() * 30.0f), Vector2.One * 9999f);
        if (ImGui.BeginPopupModal(WindowName, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
        {
            RainEd.Instance.NeedScreenRefresh();

            ImGui.BeginDisabled(cancel);
            if (ImGui.Button("取消"))
            {
                cancel = true;
                cancelSource.Cancel();
            }
            ImGui.EndDisabled();

            ImGui.BeginDisabled(!renderTask.IsCompleted);
            ImGui.SameLine();
            if (ImGui.Button("关闭"))
            {
                ImGui.CloseCurrentPopup();
                IsDone = true;
            }
            ImGui.EndDisabled();

            ImGui.SameLine();
            if (ImGui.Button("打开成品文件夹"))
            {
                RainEd.Instance.ShowPathInSystemBrowser(Path.Combine(RainEd.Instance.AssetDataPath, "Levels"), false);
            }

            lock (levelProgress)
            {
                float progress = renderedLevels + levelProgress.Values.Sum();
                ImGui.ProgressBar(progress / totalLevels, new Vector2(-0.00001f, 0f));
            }

            // status text
            if (renderTask is null || renderTask.IsFaulted)
            {
                ImGui.Text("有错误发生\n请检查日志文件以获得更多信息。");
                if (elapsedStopwatch.IsRunning) elapsedStopwatch.Stop();
            }
            else if (renderTask.IsCanceled)
            {
                ImGui.Text("渲染被取消。");

                if (elapsedStopwatch.IsRunning)
                    elapsedStopwatch.Stop();
            }
            else if (cancel)
            {
                ImGui.Text("正在取消...");

                if (elapsedStopwatch.IsRunning)
                    elapsedStopwatch.Stop();
            }
            else if (!renderBegan)
            {
                ImGui.Text("正在初始化 Drizzle...");
            }
            else if (renderedLevels < totalLevels)
            {
                if (!elapsedStopwatch.IsRunning)
                    elapsedStopwatch.Start();
                
                ImGui.TextUnformatted($"剩余 {totalLevels - renderedLevels} 关卡...");
                showTime = true;
            }
            else
            {
                ImGui.TextUnformatted("渲染完成");

                if (elapsedStopwatch.IsRunning)
                    elapsedStopwatch.Stop();
            }

            if (showTime)
                ImGui.TextUnformatted(elapsedStopwatch.Elapsed.ToString(@"hh\:mm\:ss", Boot.UserCulture));

            // error list
            if (problematicLevels.Count > 0)
            {
                ImGui.TextUnformatted(problematicLevels.Count + " errors:");

                foreach (var name in problematicLevels)
                {
                    ImGui.BulletText(name);
                }
            }

            ImGui.EndPopup();
        }
    }

    private void RenderProgressChanged(object? sender, MassRenderNotification prog)
    {
        switch (prog)
        {
            case MassRenderBegan began:
                totalLevels = began.Total;
                renderBegan = true;
                break;

            case MassRenderLevelCompleted level:
                lock (levelProgress)
                {
                    renderedLevels++;

                    if (!level.Success)
                    {
                        problematicLevels.Add(level.LevelName);
                    }

                    levelProgress.Remove(level.LevelName);
                }
                break;
            
            case MassRenderLevelProgress levelProg:
                lock (levelProgress)
                {
                    if (!levelProgress.TryAdd(levelProg.LevelName, levelProg.Progress))
                    {
                        levelProgress[levelProg.LevelName] = levelProg.Progress;
                    }
                }

                break;
        }
    }
}