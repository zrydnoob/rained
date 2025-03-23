# 开发指南
本文档适用于希望开发或修改 Rained 的用户。Rained 是一个基于 MIT 许可证的开源项目，因此您可以自由地使用其源代码，只需按照许可证要求进行适当的署名。您也可以通过提交 Pull Request 来为 Rained 的开发做出贡献，我们非常欢迎这样的贡献。GitHub 仓库的根目录下的 README.md 文件中提供了一些说明，但本文档进一步详细介绍了开发环境的设置。

## 使用 ANGLE

!!! info

    简而言之：如果您在 Windows 系统上，请将 `src\Glib\angle\win-x64` 中的 DLL 文件复制到 `C:\Program Files\dotnet` 目录下。

在 Windows 上，Rained 倾向于使用 [ANGLE](https://chromium.googlesource.com/angle/angle)，这是一个为各种图形 API 实现的 OpenGL ES。在其他操作系统上，Rained 则倾向于使用桌面版的 OpenGL 3.3。这样做的原因是 Windows 的 OpenGL 驱动程序可能会有些问题，具体取决于用户的硬件供应商，且优化不佳。此外，我经常收到一些来源不明的 OpenGL 错误报告，尽管这些问题可能已经修复。

ANGLE 由一组 DLL 文件提供，这些文件的构建需要很长时间。幸运的是，本仓库中已经存储了 Windows 和 Linux 的预构建 ANGLE 二进制文件，这些文件是我从 Electron 项目中提取的，因为我自己无法成功构建它们。

然而，Rained 在引用这些 DLL 文件时存在一个问题。由于搜索 ANGLE DLL 文件的过程不经过 C# 的 DLL 解析器，这意味着 ANGLE DLL 文件必须位于 PATH 环境变量中，或者在 Windows 上，必须位于运行的可执行文件所在的目录中。对于发布包来说，这不是问题，因为无论如何都需要将这些文件放在那里，但在运行非发布版本时，Rained 有两种启动方式，这两种方式都不会自动将 ANGLE DLL 文件放在可执行文件的目录中。

如果通过 `dotnet Rained.dll` 运行程序，ANGLE DLL 文件需要位于 `dotnet.exe` 所在的目录中，否则程序将无法启动。如果通过直接运行 Rained.exe 来启动程序，ANGLE DLL 文件需要位于包含 Rained.exe 的构建目录中。您也可以将 ANGLE DLL 文件复制到 DLL 搜索路径中的某个目录，这样在两种启动情况下都能正常工作。

如果您不想做这些操作，还有一种方法可以构建 Rained 并使用桌面版 OpenGL，这样就不需要这些 DLL 文件的准备工作。具体方法将在本文档后面介绍。

## .NET CLI
以下是使用 Git 克隆 Rained 并使用 .NET CLI 构建的说明：

1. 使用 Git 克隆：
```bash
git clone --recursive https://github.com/pkhead/rained
cd rained
```

2. 编译 Drizzle
```bash
cd src/Drizzle
dotnet run --project Drizzle.Transpiler
```

3. 返回到根目录，构建并运行 Rained
```bash
# only needs to be run once
dotnet tool restore

# usage of desktop GL or GLES/ANGLE is determined by OS.
dotnet cake

# alternative build command with desktop GL forced on.
dotnet cake --gles=false
```

4. 运行项目!
```bash
dotnet run --no-build --project src/Rained/Rained.csproj
```

很遗憾，我没有在 IDE（如 Visual Studio 或 JetBrains 的 IDE）中设置构建过程的步骤，因为我不使用这些 IDE。但希望您能够从这些说明中推断出如何在您选择的 IDE 中工作。

## 着色器
如果您想创建新的着色器或修改现有的着色器，您需要将它们通过着色器预处理器运行。着色器预处理器的存在是为了让着色器源文件包含其他着色器文件——这是 OpenGL 着色器编译本身不支持的——并处理普通 GLSL 和 ES GLSL 之间的差异。

为了使用着色器预处理器，您需要在系统上安装 Python 3 和 [glslang](https://github.com/KhronosGroup/glslang)。我不认为 glslang 有安装程序，但您需要以某种方式安装它，以便在任何终端中键入 `glslangValidator` 时都能运行正确的可执行文件，这可以通过修改系统或用户的 PATH 来实现。

一旦两者都安装好了，着色器预处理器将在调用 `dotnet cake` 时自动运行。如果没有安装所需的软件，预处理步骤将被跳过。

## 文档
文档是使用 [Material for MkDocs](https://squidfunk.github.io/mkdocs-material/) 构建的。您需要安装 python 和 pip 来构建它。

```bash
# install material for mkdocs
pip install mkdocs-material

# serve docs on http://localhost:8000
mkdocs serve

# build doc site
mkdocs build
```

## 子项目
Rained 在 C# 解决方案中有多个项目。以下是它们的简要描述：

|     Name           |      Description                                           |
| ------------------ | ---------------------------------------------------------- |
| **Drizzle**        | 将原始渲染器从 Lingo 移植到 C#。                              |
| **Glib**           | OpenGL 3.3/OpenGL ES 2.0 和 Silk.NET 的封装。               |
| **Glib.ImGui**     | Glib/Silk.NET 的 ImGui.NET 后端。                           |
| **Glib.Tests**     | 用于 Glib 视觉输出的测试程序。                                |
| **ImGui.NET**      | 启用 Freetype 的 ImGui.NET 版本。                           |
| **Rained**         | 整个 Rained 应用程序。                                      |
| **Rained.Console** | 从控制台环境启动 Rained 的 C 应用程序。                       |
| **Rained.Tests**   | Rained 的一些单元测试。                                     |

还有一个 [rainedvm](https://github.com/pkhead/rainedvm)，它是一个单独的程序，作为版本管理工具。它是用 C++ 编程的，并且是单独分发的，因此它是一个单独的仓库。

## ImGui .ini 文件
每当您启动 Rained 时，`config/imgui.ini` 文件都会被修改，这使得版本控制想要跟踪不必要的更改。然而，它不能放在 .gitignore 中，因为 Rained 需要一个初始的 imgui.ini 文件。因此，如果您实际上不想更新 config/imgui.ini，我建议两种做法：

1. 运行 `git update-index --assume-unchanged config/imgui.ini`。这将使 Git 忽略对该文件的任何更改，尽管在切换分支时可能会导致问题。在这种情况下，您可以使用 `git stash`。如果您想撤销此操作，请运行 `git update-index --no-assume-unchanged config/imgui.ini`。

2. 在每次提交之前，手动取消暂存 `config/imgui.ini`，或者手动暂存除该文件之外的所有文件。

## "nightly" 标签
"nightly" 标签的存在只是为了让我能够创建每晚的 GitHub 发布。这有点烦人。我不建议与之交互。

由于每次发布时操作都会删除并重新创建 "nightly" 标签，因此要在您的克隆中更新该标签（假设您想要这样做），您需要运行以下 Git 命令：

```bash
git tag -d nightly # delete the nightly tag on your clone
git fetch origin tag nightly # fetch the nightly tag from origin
# running `git fetch` or `git pull` itself after deleting the tag should also work.
```
