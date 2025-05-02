/*
* App setup
* This runs when a preferences.json file could not be located on boot, which probably means
* that the user needs to set up their Data folder
*/

using ImGuiNET;
using Rained;
using Rained.EditorGui;
using Raylib_cs;
using System.Numerics;
using System.IO.Compression;
using System.Diagnostics;

class AppSetup
{
    // 0 = not started
    // 1 = downloading
    // 2 = extracting
    private int downloadStage = 0;
    private float downloadProgress = 0f;

    private string? callbackRes = null;
    private List<string> missingDirs = [];
    private float callbackWait = 1f;
    private FileBrowser? fileBrowser = null;
    private Task? downloadTask = null;

    private const string StartupText = """
    欢迎来到 Rained 设置屏幕！请配置 Rain World 关卡编辑器数据文件夹的位置。

    如果您之前安装了 Rain World 关卡编辑器，您可以单击“选择数据文件夹”按钮。

    如果您之前安装了官方编辑器或者像社区编辑器一样的编辑器, 那么您将选择包含可执行文件的文件夹。否则，您应该找到并选择上编辑器的 Drizzle 数据文件夹。

    如果不知道如何做，请点击“下载数据文件”按钮。 注意，数据文件存放在Github上，因网络原因可能会下载失败，可使用镜像网站下载。
    """;

    private enum SetupState
    {
        // where the user decides if they want to use a pre-exicsting RWLE install,
        // or download data from the internet
        SetupChoice,

        // where the user configures the data download
        // can do vanilla, or certain parts of solar's.
        DownloadConfiguration,

        // stuff is being downloaded from the internet.
        Downloading,

        // setup is done, now launching rained...
        Finished
    }

    private SetupState setupState = SetupState.SetupChoice;

    public bool Start(out string? assetDataPath)
    {
        assetDataPath = null;

        Fonts.SetFont("AlibabaPuHuiTi");

        while (true)
        {
            if (Raylib.WindowShouldClose())
            {
                return false;
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(0, 0, 0, 0));
            Boot.ImGuiController!.Update(Raylib.GetFrameTime());

            ImGuiExt.EnsurePopupIsOpen("Configure Data");
            ImGuiExt.CenterNextWindow(ImGuiCond.Always);

            bool exitAppSetup = false;

            if (ImGuiExt.BeginPopupModal("Configure Data", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove))
            {
                switch (setupState)
                {
                    case SetupState.SetupChoice:
                        ShowSetupChoice();
                        break;

                    case SetupState.DownloadConfiguration:
                        ShowDownloadConfiguration();
                        break;

                    case SetupState.Downloading:
                        ShowDownload();
                        break;

                    case SetupState.Finished:
                        exitAppSetup = ShowFinished(out assetDataPath);
                        break;

                    default:
                        throw new UnreachableException("Invalid setup state mode");
                }

                ImGui.EndPopup();
            }

            Boot.ImGuiController!.Render();
            Raylib.EndDrawing();

            if (exitAppSetup) break;
        }

        return true;
    }

    private void ShowSetupChoice()
    {
        ImGui.PushTextWrapPos(ImGui.GetTextLineHeight() * 50.0f);
        ImGui.TextWrapped(StartupText);
        ImGui.PopTextWrapPos();

        ImGui.Separator();

        FileBrowser.Render(ref fileBrowser);

        if (ImGui.Button("选择数据文件夹"))
        {
            fileBrowser = new FileBrowser(FileBrowser.OpenMode.Directory, FileBrowserCallback, Boot.AppDataPath);
        }

        ImGui.SameLine();
        if (ImGui.Button("下载数据文件（Github）"))
        {
            setupState = SetupState.Downloading;
            downloadTask = DownloadData("https://github.com/SlimeCubed/Drizzle.Data/archive/refs/heads/community.zip");
        }

        ImGui.SameLine();
        if (ImGui.Button("下载数据文件（llkk.cc加速）"))
        {
            setupState = SetupState.Downloading;
            downloadTask = DownloadData("https://gh.llkk.cc/https://github.com/SlimeCubed/Drizzle.Data/archive/refs/heads/community.zip");
        }

        // show missing dirs popup
        if (missingDirs.Count > 0)
        {
            ImGuiExt.EnsurePopupIsOpen("Error");
            ImGuiExt.CenterNextWindow(ImGuiCond.Appearing);
            if (ImGuiExt.BeginPopupModal("Error", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
            {
                ImGui.Text("The given data folder is missing the following subdirectories:");
                foreach (var dir in missingDirs)
                {
                    ImGui.BulletText(dir);
                }

                ImGui.Separator();
                if (StandardPopupButtons.Show(PopupButtonList.OK, out _))
                {
                    ImGui.CloseCurrentPopup();
                    missingDirs.Clear();
                }

                ImGui.EndPopup();
            }
        }
    }

    private void ShowDownloadConfiguration()
    {

    }

    private void ShowDownload()
    {
        if (downloadStage == 1)
        {
            ImGui.Text("正在下载\n Data数据包... \n注：使用镜像以下进度条有bug，下载未完成前均是0%，可打开任务管理器查看内存占用估计下载进度，这个文件的大小大概是128MB");
        }
        else if (downloadStage == 2)
        {
            ImGui.Text("提取中...");
        }
        else
        {
            ImGui.Text("开启中...");
        }

        ImGui.ProgressBar(downloadProgress, new Vector2(ImGui.GetTextLineHeight() * 50.0f, 0.0f));

        // when task has ended,
        // go ahead if it was successful.
        // else, show an error message.
        if (downloadTask is not null && downloadTask.IsCompleted)
        {
            if (downloadTask.IsCompletedSuccessfully)
            {
                downloadTask = null;
                callbackRes = Path.Combine(Boot.AppDataPath, "Data");
                setupState = SetupState.Finished;
            }
            else
            {
                ImGuiExt.EnsurePopupIsOpen("Error");
                ImGuiExt.CenterNextWindow(ImGuiCond.Appearing);
                if (ImGuiExt.BeginPopupModal("Error", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings))
                {
                    if (downloadTask.IsFaulted)
                        ImGui.Text(downloadTask.Exception.Message);

                    else
                        ImGui.Text("The operation was aborted.");

                    ImGui.Separator();
                    if (StandardPopupButtons.Show(PopupButtonList.OK, out _))
                    {
                        ImGui.CloseCurrentPopup();
                        downloadTask = null;
                        setupState = SetupState.SetupChoice;
                    }

                    ImGui.EndPopup();
                }
            }
        }
    }

    private bool ShowFinished(out string? assetDataPath)
    {
        Debug.Assert(callbackRes is not null);
        ImGui.Text("启动 Rained...");

        // wait a bit so that the Launching Rained... message can appear
        callbackWait -= Raylib.GetFrameTime();
        if (callbackWait <= 0f)
        {
            assetDataPath = callbackRes;
            return true;
        }

        assetDataPath = null;
        return false;
    }

    private void FileBrowserCallback(string[] paths)
    {
        if (paths.Length == 0) return;
        var path = paths[0];

        if (!string.IsNullOrEmpty(path))
        {
            // check for any missing directories
            missingDirs.Clear();
            missingDirs.Add("Graphics");
            missingDirs.Add("Props");
            missingDirs.Add("Levels");

            for (int i = missingDirs.Count - 1; i >= 0; i--)
            {
                if (Directory.Exists(Path.Combine(path, missingDirs[i])))
                {
                    missingDirs.RemoveAt(i);
                }
            }

            if (missingDirs.Count == 0)
            {
                callbackRes = path;
                setupState = SetupState.Finished;
            }
        }
    }

    private async Task DownloadData(string url)
    {
        var tempZipFile = Path.GetTempFileName();
        Console.WriteLine("Zip located at " + tempZipFile);

        try
        {
            // download the zip file
            downloadStage = 1;
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("user-agent", Util.HttpUserAgent);
                client.Timeout = Timeout.InfiniteTimeSpan;

                using var outputStream = File.OpenWrite(tempZipFile);

                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var contentLength = response.Content.Headers.ContentLength;
                using var download = await response.Content.ReadAsStreamAsync();

                // read http response into tempZipFile
                var buffer = new byte[8192];
                long totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = await download.ReadAsync(buffer).ConfigureAwait(false)) != 0)
                {
                    await outputStream.WriteAsync(buffer).ConfigureAwait(false);
                    totalBytesRead += bytesRead;

                    if (contentLength.HasValue)
                        downloadProgress = (float)totalBytesRead / contentLength.Value;
                }
            }

            // begin extracting the zip file
            downloadStage = 2;
            downloadProgress = 0f;

            // ensure last character of dest path ends with a directory separator char
            // (for security reasons)
            var extractPath = Boot.AppDataPath;
            if (!extractPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                extractPath += Path.DirectorySeparatorChar;

            using (var zip = ZipFile.OpenRead(tempZipFile))
            {
                // get the number of entries that aren't in the Cast folder
                // (the cast folder will not be extracted)
                int entryCount = 0;
                string ignoreFilter = "Drizzle.Data-community/Cast";
                foreach (var entry in zip.Entries)
                {
                    var fullName = entry.FullName;
                    if (fullName.Length >= ignoreFilter.Length && fullName[0..ignoreFilter.Length] == ignoreFilter)
                        continue;

                    entryCount++;
                }

                int processedEntries = 0;

                foreach (var entry in zip.Entries)
                {
                    // replace the root folder name from "Drizzle.Data-community" to simply "Data"
                    // also, ignore anything in cast - i already copied the data in there into assets/drizzle-cast
                    var modifiedName = "Data" + entry.FullName[entry.FullName.IndexOf('/')..];
                    if (modifiedName.Length >= 9 && modifiedName[0..9] == "Data/Cast") continue;

                    if (entry.FullName.EndsWith('/'))
                    {
                        Directory.CreateDirectory(Path.Combine(extractPath, modifiedName));
                    }
                    else
                    {
                        entry.ExtractToFile(Path.Combine(extractPath, modifiedName), true);
                    }

                    processedEntries++;
                    downloadProgress = (float)processedEntries / entryCount;
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        finally
        {
            Console.WriteLine("Delete " + tempZipFile);
            File.Delete(tempZipFile);
        }
    }
}