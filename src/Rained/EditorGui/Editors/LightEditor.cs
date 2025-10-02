using System.Numerics;
using ImGuiNET;
using Raylib_cs;
using Rained.LevelData;
using System.Runtime.CompilerServices;

namespace Rained.EditorGui.Editors;

class LightEditor : IEditorMode
{
    public string Name { get => "灯光"; }
    public bool SupportsCellSelection => false;

    private readonly LevelWindow window;

    private Vector2 brushSize = new(50f, 70f);
    private float brushRotation = 0f;
    private int selectedBrush = 0;
    private bool brushPreview = false; // true when user is changing brush parameters through slider

    private bool isCursorEnabled = true;
    private bool isDrawing = false;
    private bool isChangingParameters = false; // true if the user is using keyboard shortcuts to change parameters
    private Vector2 savedMouseGp = new();
    private Vector2 savedMousePos = new();

    private bool warpMode = false;
    private bool warpModeSubmit = false;
    private readonly Vector2[] warpPoints = new Vector2[4];
    private int hoveredVertexIndex = -1;
    private RlManaged.RenderTexture2D? tmpFramebuffer;

    // Bleh
    // I think i need to change how the EditorMode classes work.
    // specifically, to make a unique set of each per document.
    // instead of using the same one across multiple documents.
    private readonly ConditionalWeakTable<Level, ChangeHistory.LightChangeRecorder> changeRecorders = [];

    public LightEditor(LevelWindow window)
    {
        this.window = window;
        // ReloadLevel();

        RainEd.Instance.ChangeHistory.Cleared += ReloadLevel;

        RainEd.Instance.ChangeHistory.UndidOrRedid += () =>
        {
            if (changeRecorders.TryGetValue(RainEd.Instance.Level, out var changeRecorder))
                changeRecorder.UpdateParametersSnapshot();
        };
    }

    public void LevelCreated(Level level)
    {
        ChangeHistory.LightChangeRecorder changeRecorder;
        if (level.LightMap.IsLoaded)
            changeRecorder = new ChangeHistory.LightChangeRecorder(level.LightMap.GetImage());
        else
            changeRecorder = new ChangeHistory.LightChangeRecorder(null);

        changeRecorders.AddOrUpdate(level, changeRecorder);
    }

    public void LevelClosed(Level level)
    {
        changeRecorders.Remove(level);
    }

    public void ReloadLevel()
    {
        if (changeRecorders.TryGetValue(RainEd.Instance.Level, out var changeRecorder))
            changeRecorder?.Dispose();

        tmpFramebuffer?.Dispose();
        tmpFramebuffer = null;

        var level = RainEd.Instance.Level;
        if (level.LightMap.IsLoaded)
            changeRecorder = new ChangeHistory.LightChangeRecorder(level.LightMap.GetImage());
        else
            changeRecorder = new ChangeHistory.LightChangeRecorder(null);

        changeRecorders.AddOrUpdate(RainEd.Instance.Level, changeRecorder);
        changeRecorder?.UpdateParametersSnapshot();
    }

    public void ChangeLevel(Level newLevel) { }

    public void Load()
    {
        if (!RainEd.Instance.Level.LightMap.IsLoaded)
            EditorWindow.ShowNotification("The lightmap is too large to be loaded.");

        if (changeRecorders.TryGetValue(RainEd.Instance.Level, out var changeRecorder))
            changeRecorder.ClearStrokeData();
    }

    public void Unload()
    {
        if (isChangingParameters)
        {
            if (changeRecorders.TryGetValue(RainEd.Instance.Level, out var changeRecorder))
                changeRecorder.PushParameterChanges();
            isChangingParameters = false;
        }

        if (!isCursorEnabled)
        {
            Raylib.ShowCursor();
            Raylib.SetMousePosition((int)savedMousePos.X, (int)savedMousePos.Y);
            isCursorEnabled = true;
        }

        tmpFramebuffer?.Dispose();
        tmpFramebuffer = null;

        warpMode = false;
        warpModeSubmit = false;
    }

    public void ShowEditMenu()
    {
        KeyShortcuts.ImGuiMenuItem(KeyShortcut.ResetBrushTransform, "重置笔刷变换");
        KeyShortcuts.ImGuiMenuItem(KeyShortcut.LightmapStretch, "Warp", enabled: !warpMode);
    }

    public void DrawStatusBar()
    {
        if (warpMode)
        {
            ImGui.Text("形变");

            ImGui.SameLine();
            if (ImGui.Button("确认") || EditorWindow.IsKeyPressed(ImGuiKey.Enter))
            {
                warpModeSubmit = true;
            }

            ImGui.SameLine();
            if (ImGui.Button("取消  ") || EditorWindow.IsKeyPressed(ImGuiKey.Escape))
            {
                warpMode = false;
            }
        }
    }

    public void DrawToolbar()
    {
        bool wasParamChanging = isChangingParameters;
        isChangingParameters = false;

        var level = RainEd.Instance.Level;
        var brushDb = RainEd.Instance.LightBrushDatabase;
        var prefs = RainEd.Instance.Preferences;
        brushPreview = false;
        changeRecorders.TryGetValue(level, out var changeRecorder);

        if (ImGui.Begin("灯光###Light Catalog", ImGuiWindowFlags.NoFocusOnAppearing))
        {
            if (changeRecorder is null) ImGui.BeginDisabled();

            ImGui.PushItemWidth(ImGui.GetTextLineHeight() * 8.0f);

            ImGui.SliderAngle("光线角度", ref level.LightAngle, 0f, 360f, "%.1f deg");
            if (ImGui.IsItemDeactivatedAfterEdit())
                changeRecorder?.PushParameterChanges();

            ImGui.SliderFloat("光线距离", ref level.LightDistance, 1f, Level.MaxLightDistance, "%.3f", ImGuiSliderFlags.AlwaysClamp);
            if (ImGui.IsItemDeactivatedAfterEdit())
                changeRecorder?.PushParameterChanges();

            ImGui.PopItemWidth();

            // draw light angle ring
            var avail = ImGui.GetContentRegionAvail();
            if (avail.X != 0) // this happens when the window first appears
            {
                ImGui.NewLine();

                var drawList = ImGui.GetWindowDrawList();
                var screenCursor = ImGui.GetCursorScreenPos();

                var minRadius = 8f;
                var maxRadius = 70f;
                var radius = (level.LightDistance - 1f) / (Level.MaxLightDistance - 1f) * (maxRadius - minRadius) + minRadius;
                var centerRadius = (5f - 1f) / (Level.MaxLightDistance - 1f) * (maxRadius - minRadius) + minRadius;

                centerRadius *= Boot.WindowScale;
                radius *= Boot.WindowScale;

                var color = ImGui.ColorConvertFloat4ToU32(ImGui.GetStyle().Colors[(int)ImGuiCol.Text]);

                var circleCenter = screenCursor + new Vector2(avail.X / 2f, maxRadius * Boot.WindowScale);
                drawList.AddCircle(circleCenter, centerRadius, color); // draw center circle
                drawList.AddCircle(circleCenter, radius, color); // draw distance circle

                // draw angle
                var correctedAngle = MathF.PI / 2f + level.LightAngle;

                drawList.AddCircleFilled(
                    new Vector2(MathF.Cos(correctedAngle), MathF.Sin(correctedAngle)) * radius + circleCenter,
                    6f * Boot.WindowScale,
                    color
                );

                ImGui.InvisibleButton("闪电", new Vector2(avail.X, maxRadius * 2f * Boot.WindowScale));
                if (ImGui.IsItemActive())
                {
                    isChangingParameters = true;

                    var vecDiff = (ImGui.GetMousePos() - circleCenter) / Boot.WindowScale;

                    level.LightAngle = MathF.Atan2(vecDiff.Y, vecDiff.X) - MathF.PI / 2f;
                    if (level.LightAngle < 0)
                    {
                        level.LightAngle += 2f * MathF.PI;
                    }

                    level.LightDistance = (vecDiff.Length() - minRadius) / (maxRadius - minRadius) * (Level.MaxLightDistance - 1f) + 1f;
                    level.LightDistance = Math.Clamp(level.LightDistance, 1f, Level.MaxLightDistance);
                }
                ImGui.NewLine();
            }

            if (changeRecorder is null) ImGui.EndDisabled();
        }
        ImGui.End();

        if (ImGui.Begin("笔刷", ImGuiWindowFlags.NoFocusOnAppearing))
        {
            if (ImGui.Button("重设笔刷") || KeyShortcuts.Activated(KeyShortcut.ResetBrushTransform))
            {
                brushSize = new(50f, 70f);
                brushRotation = 0f;
            }

            var rotInRadians = Util.Mod(brushRotation, 360f) / 180f * MathF.PI;
            ImGui.DragFloat2("大小", ref brushSize, 2f, 1f, float.PositiveInfinity, "%.0f px", ImGuiSliderFlags.AlwaysClamp);
            if (ImGui.IsItemActive()) brushPreview = true;
            if (ImGui.SliderAngle("旋转", ref rotInRadians, 0f, 360f))
                brushRotation = rotInRadians / MathF.PI * 180f;
            if (ImGui.IsItemActive()) brushPreview = true;

            ImGui.BeginChild("BrushCatalog", ImGui.GetContentRegionAvail());
            {
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0));
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2, 2));
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));

                int i = 0;
                int cols = (int)(ImGui.GetContentRegionAvail().X / (64 * Boot.WindowScale + 4));
                if (cols == 0) cols = 1;
                foreach (var brush in RainEd.Instance.LightBrushDatabase.Brushes)
                {
                    var texture = brush.Texture;

                    // highlight selected brush
                    if (i == selectedBrush)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonHovered]);

                        // buttons will have a more transparent hover color
                    }
                    else
                    {
                        Vector4 col = ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonHovered];
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered,
                            new Vector4(col.X, col.Y, col.Z, col.W / 4f));
                    }

                    ImGui.PushID(i);
                    if (ImGuiExt.ImageButtonRect("##Texture", texture, 64 * Boot.WindowScale, 64 * Boot.WindowScale, new Rectangle(0, 0, texture.Width, texture.Height)))
                    {
                        selectedBrush = i;
                    }

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(brush.Name);

                    ImGui.PopStyleColor();

                    ImGui.PopID();

                    i++;
                    if (!(i % cols == 0)) ImGui.SameLine();
                }

                ImGui.PopStyleVar(2);
                ImGui.PopStyleColor();
            }
            ImGui.EndChild();
        }
        ImGui.End();

        // keyboard catalog navigation
        if (prefs.LightEditorControlScheme == UserPreferences.LightEditorControlSchemeOption.Mouse)
        {
            // S to move down one row
            if (KeyShortcuts.Activated(KeyShortcut.NavDown))
            {
                int maxRow = brushDb.Brushes.Count / 3;
                int curRow = selectedBrush / 3;
                int col = selectedBrush % 3;

                // selected will only wrap around if it was on
                // the last row
                if (curRow == maxRow)
                {
                    curRow = 0;
                }
                else
                {
                    curRow++;
                }

                selectedBrush = Math.Clamp(curRow * 3 + col, 0, brushDb.Brushes.Count - 1);
            }

            // W to move up one row
            if (KeyShortcuts.Activated(KeyShortcut.NavUp))
            {
                int maxRow = brushDb.Brushes.Count / 3;
                int curRow = selectedBrush / 3;
                int col = selectedBrush % 3;

                // selected will only wrap around if it was on
                // the first row
                if (curRow == 0)
                {
                    curRow = maxRow;
                }
                else
                {
                    curRow--;
                }

                selectedBrush = Math.Clamp(curRow * 3 + col, 0, brushDb.Brushes.Count - 1);
            }

            // D to move right
            if (KeyShortcuts.Activated(KeyShortcut.NavRight))
                selectedBrush = (selectedBrush + 1) % brushDb.Brushes.Count;

            // A to move left
            if (KeyShortcuts.Activated(KeyShortcut.NavLeft))
            {
                if (selectedBrush == 0)
                    selectedBrush = brushDb.Brushes.Count - 1;
                else
                    selectedBrush--;
            }
        }
        else
        {
            if (KeyShortcuts.Activated(KeyShortcut.NextBrush))
                selectedBrush = (selectedBrush + 1) % brushDb.Brushes.Count;

            if (KeyShortcuts.Activated(KeyShortcut.PreviousBrush))
            {
                if (selectedBrush == 0)
                    selectedBrush = brushDb.Brushes.Count - 1;
                else
                    selectedBrush--;
            }
        }

        // when shift is held, WASD changes light parameters
        if (changeRecorder is not null)
        {
            var lightAngleChange = 0f;
            var lightDistChange = 0f;

            if (KeyShortcuts.Active(KeyShortcut.RotateLightCW))
            {
                isChangingParameters = true;
                lightAngleChange = 1f;
            }

            if (KeyShortcuts.Active(KeyShortcut.RotateLightCCW))
            {
                isChangingParameters = true;
                lightAngleChange = -1f;
            }

            if (KeyShortcuts.Active(KeyShortcut.ZoomLightIn))
            {
                isChangingParameters = true;
                lightDistChange = -1f;
            }

            if (KeyShortcuts.Active(KeyShortcut.ZoomLightOut))
            {
                isChangingParameters = true;
                lightDistChange = 1f;
            }

            if (isChangingParameters)
            {
                level.LightAngle += lightAngleChange * (100f / 180f * MathF.PI) * Raylib.GetFrameTime();
                level.LightDistance += lightDistChange * 20f * Raylib.GetFrameTime();

                // wrap around light angle
                if (level.LightAngle > 2f * MathF.PI)
                    level.LightAngle -= 2f * MathF.PI;
                if (level.LightAngle < 0)
                    level.LightAngle += 2f * MathF.PI;

                // clamp light distance
                level.LightDistance = Math.Clamp(level.LightDistance, 1f, Level.MaxLightDistance);
            }
        }

        if (wasParamChanging && !isChangingParameters)
        {
            changeRecorder!.PushParameterChanges();
        }
    }

    private static void DrawOcclusionPlane(RlManaged.RenderTexture2D? fb = null)
    {
        var level = RainEd.Instance.Level;
        fb ??= level.LightMap.RenderTexture;

        // render light plane
        var levelBoundsW = level.Width * 20;
        var levelBoundsH = level.Height * 20;

        if (fb is not null)
        {
            RlExt.DrawRenderTextureV(
                fb,
                new Vector2(levelBoundsW - level.LightMap.Width, levelBoundsH - level.LightMap.Height),
                new Color(255, 0, 0, 100)
            );
        }
        /*Raylib.DrawTextureRec(
            level.LightMap.Texture,
            new Rectangle(0, level.LightMap.Height, level.LightMap.Width, -level.LightMap.Height),
            new Vector2(levelBoundsW - level.LightMap.Width, levelBoundsH - level.LightMap.Height),
            new Color(255, 0, 0, 100)
        );*/
    }

    private static Vector2 CalcCastOffset()
    {
        var level = RainEd.Instance.Level;
        var correctedAngle = level.LightAngle + MathF.PI / 2f;
        return new(
            -MathF.Cos(correctedAngle) * level.LightDistance * Level.TileSize,
            -MathF.Sin(correctedAngle) * level.LightDistance * Level.TileSize
        );
    }

    public void DrawViewport(RlManaged.RenderTexture2D mainFrame, RlManaged.RenderTexture2D[] layerFrames)
    {
        var level = RainEd.Instance.Level;
        var levelRender = window.Renderer;

        var levelBoundsW = level.Width * 20;
        var levelBoundsH = level.Height * 20;
        var lightMapOffset = new Vector2(
            levelBoundsW - level.LightMap.Width,
            levelBoundsH - level.LightMap.Height
        );

        // draw light background (solid white)
        if (level.LightMap.IsLoaded)
        {
            Raylib.DrawRectangle(
                (int)lightMapOffset.X,
                (int)lightMapOffset.Y,
                level.Width * Level.TileSize - (int)lightMapOffset.X,
                level.Height * Level.TileSize - (int)lightMapOffset.Y,
                Color.White
            );
        }

        // draw level
        levelRender.RenderLevel(new Rendering.LevelRenderConfig()
        {
            Fade = 30f / 255f
        });
        levelRender.RenderBorder();
        levelRender.RenderCameraBorders();

        if (level.LightMap.RenderTexture is not null)
        {
            if (warpMode)
            {
                ProcessWarpMode(lightMapOffset, mainFrame);
            }
            else
            {
                ProcessBrushMode(lightMapOffset, mainFrame);

                if (KeyShortcuts.Activated(KeyShortcut.LightmapStretch))
                {
                    warpMode = true;
                    var levelBounds = new Vector2(level.LightMap.Width, level.LightMap.Height);

                    warpPoints[0] = Vector2.Zero;
                    warpPoints[1] = Vector2.UnitX * levelBounds;
                    warpPoints[2] = Vector2.One * levelBounds;
                    warpPoints[3] = Vector2.UnitY * levelBounds;
                }
            }
        }
    }

    private void RenderCursor(Texture2D tex, Vector2 mpos, bool cast)
    {
        var screenSize = brushSize / window.ViewZoom;

        // cast of brush preview
        if (cast)
        {
            var castOffset = CalcCastOffset();
            Raylib.DrawTexturePro(
                tex,
                new Rectangle(0, 0, tex.Width, tex.Height),
                new Rectangle(
                    mpos.X * Level.TileSize + castOffset.X,
                    mpos.Y * Level.TileSize + castOffset.Y,
                    screenSize.X, screenSize.Y
                ),
                screenSize / 2f,
                brushRotation,
                new Color(0, 0, 0, 80)
            );
        }
        else
        {
            // draw preview on occlusion plane
            Raylib.DrawTexturePro(
                tex,
                new Rectangle(0, 0, tex.Width, tex.Height),
                new Rectangle(
                    mpos.X * Level.TileSize,
                    mpos.Y * Level.TileSize,
                    screenSize.X, screenSize.Y
                ),
                screenSize / 2f,
                brushRotation,
                new Color(255, 0, 0, 100)
            );
        }
    }

    private void ProcessBrushMode(Vector2 lightMapOffset, RlManaged.RenderTexture2D mainFrame)
    {
        var wasCursorEnabled = isCursorEnabled;
        var wasDrawing = isDrawing;
        isCursorEnabled = true;
        isDrawing = false;

        var prefs = RainEd.Instance.Preferences;
        var level = RainEd.Instance.Level;
        changeRecorders.TryGetValue(level, out var changeRecorder);

        var shader = Shaders.LevelLightShader;
        Raylib.BeginShaderMode(shader);

        // render cast
        var castOffset = CalcCastOffset();

        RlExt.DrawRenderTextureV(level.LightMap.RenderTexture!, lightMapOffset + castOffset, new Color(0, 0, 0, 80));

        // Render mouse cursor
        if (window.IsViewportHovered && changeRecorder is not null)
        {
            var tex = RainEd.Instance.LightBrushDatabase.Brushes[selectedBrush].Texture;
            var mpos = window.MouseCellFloat;
            if (!wasCursorEnabled) mpos = savedMouseGp;

            var screenSize = brushSize / window.ViewZoom;

            // render brush preview
            // if drawing, draw on light texture instead of screen
            var lmb = EditorWindow.IsMouseDown(ImGuiMouseButton.Left);
            var rmb = EditorWindow.IsMouseDown(ImGuiMouseButton.Right);
            if (lmb || rmb)
            {
                isDrawing = true;

                DrawOcclusionPlane();

                Rlgl.LoadIdentity(); // why the hell do i have to call this
                level.LightMap.RaylibBeginTextureMode();

                // draw on brush plane
                BrushAtom atom = new()
                {
                    rect = new Rectangle(
                        mpos.X * Level.TileSize - lightMapOffset.X,
                        mpos.Y * Level.TileSize - lightMapOffset.Y,
                        screenSize.X, screenSize.Y
                    ),
                    rotation = brushRotation,
                    mode = lmb,
                    brush = selectedBrush
                };

                changeRecorder.RecordAtom(atom);
                LightMap.DrawAtom(atom);

                Raylib.BeginTextureMode(mainFrame);
            }
            else
            {
                // cast of brush preview
                RenderCursor(tex, mpos, true);
                DrawOcclusionPlane();

                // draw preview on on occlusion plane
                RenderCursor(tex, mpos, false);
            }

            switch (prefs.LightEditorControlScheme)
            {
                case UserPreferences.LightEditorControlSchemeOption.Mouse:
                    {
                        var doScale = KeyShortcuts.Active(KeyShortcut.ScaleLightBrush);
                        var doRotate = KeyShortcuts.Active(KeyShortcut.RotateLightBrush);

                        if (doScale || doRotate)
                        {
                            if (wasCursorEnabled)
                            {
                                savedMouseGp = mpos;
                                savedMousePos = Raylib.GetMousePosition();
                            }
                            isCursorEnabled = false;

                            if (doScale)
                                brushSize += Raylib.GetMouseDelta();
                            if (doRotate)
                                brushRotation -= Raylib.GetMouseDelta().Y / 2f;
                        }

                        break;
                    }

                case UserPreferences.LightEditorControlSchemeOption.Keyboard:
                    {
                        var rotSpeed = Raylib.GetFrameTime() * 120f;

                        if (KeyShortcuts.Active(KeyShortcut.RotateBrushCW))
                            brushRotation += rotSpeed;
                        if (KeyShortcuts.Active(KeyShortcut.RotateBrushCCW))
                            brushRotation -= rotSpeed;

                        var scaleSpeed = Raylib.GetFrameTime() * 120f;

                        if (KeyShortcuts.Active(KeyShortcut.NavRight))
                            brushSize.X += scaleSpeed;
                        if (KeyShortcuts.Active(KeyShortcut.NavLeft))
                            brushSize.X -= scaleSpeed;
                        if (KeyShortcuts.Active(KeyShortcut.NavUp))
                            brushSize.Y += scaleSpeed;
                        if (KeyShortcuts.Active(KeyShortcut.NavDown))
                            brushSize.Y -= scaleSpeed;

                        break;
                    }
            }

            brushSize.X = MathF.Max(0f, brushSize.X);
            brushSize.Y = MathF.Max(0f, brushSize.Y);
        }
        else
        {
            var tex = RainEd.Instance.LightBrushDatabase.Brushes[selectedBrush].Texture;
            var mpos = (window.Renderer.ViewTopLeft + window.Renderer.ViewBottomRight) / 2;

            if (brushPreview) RenderCursor(tex, mpos, false);
            DrawOcclusionPlane();
            if (brushPreview) RenderCursor(tex, mpos, true);
        }

        Raylib.EndShaderMode();

        // record stroke data at the end of the stroke
        if (wasDrawing && !isDrawing)
        {
            changeRecorder!.EndStroke();
        }

        // handle cursor lock when transforming brush
        if (!isCursorEnabled)
        {
            Raylib.SetMousePosition((int)savedMousePos.X, (int)savedMousePos.Y);
        }

        if (wasCursorEnabled != isCursorEnabled)
        {
            if (isCursorEnabled)
            {
                Raylib.ShowCursor();
                Raylib.SetMousePosition((int)savedMousePos.X, (int)savedMousePos.Y);
            }
            else
            {
                Raylib.HideCursor();
            }
        }
    }

    private void ProcessWarpMode(Vector2 lightMapOffset, RlManaged.RenderTexture2D mainFrame)
    {
        var level = RainEd.Instance.Level;
        changeRecorders.TryGetValue(level, out var changeRecorder);

        // render stretched lightmap into intermediary framebuffer

        if (tmpFramebuffer is null || tmpFramebuffer.Texture.Width != level.LightMap.Width || tmpFramebuffer.Texture.Height != level.LightMap.Height)
        {
            tmpFramebuffer?.Dispose();
            tmpFramebuffer = RlManaged.RenderTexture2D.Load(level.LightMap.Width, level.LightMap.Height);
        }

        Rlgl.PushMatrix();
        Rlgl.LoadIdentity();

        Raylib.BeginTextureMode(tmpFramebuffer);
        Raylib.BeginShaderMode(Shaders.LightStretchShader);
        LightMap.UpdateWarpShaderUniforms(warpPoints);
        RlExt.DrawRenderTexture(level.LightMap.RenderTexture!, 0, 0, Color.White);
        // Raylib.EndShaderMode();

        // render lightmap into main framebuffer
        Raylib.BeginTextureMode(mainFrame);
        Rlgl.PopMatrix();
        Raylib.BeginShaderMode(Shaders.LevelLightShader);

        // render cast
        var castOffset = CalcCastOffset();
        RlExt.DrawRenderTextureV(tmpFramebuffer, lightMapOffset + castOffset, new Color(0, 0, 0, 80));

        // render occlusion plane
        DrawOcclusionPlane(tmpFramebuffer);

        Raylib.EndShaderMode();

        // move active vertex
        var mousePos = window.MouseCellFloat * 20 - lightMapOffset;
        if (EditorWindow.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (hoveredVertexIndex != -1)
            {
                warpPoints[hoveredVertexIndex] = mousePos;
            }
        }
        else
        {
            // find warp vertex closest to mouse
            hoveredVertexIndex = -1;
            float minDistSq = 40 / window.ViewZoom; // initial value is the distance threshold
            minDistSq *= minDistSq;
            for (int i = 0; i < 4; i++)
            {
                var distSq = Vector2.DistanceSquared(warpPoints[i], mousePos);
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    hoveredVertexIndex = i;
                }
            }
        }

        // draw warp vertices
        Span<Vector2> cornerOrigins =
        [
            Vector2.Zero,
            Vector2.UnitX * level.LightMap.Width,
            Vector2.One * new Vector2(level.LightMap.Width, level.LightMap.Height),
            Vector2.UnitY * level.LightMap.Height,
        ];

        for (int i = 0; i < 4; i++)
        {
            var pt = warpPoints[i];

            var color = Color.Red;
            if (i != hoveredVertexIndex)
            {
                color.A = 180;
            }

            // corner origin
            Raylib.DrawCircleV(cornerOrigins[i] + lightMapOffset, 2f / window.ViewZoom * Boot.WindowScale, color);

            // warp point
            Raylib.DrawCircleV(pt + lightMapOffset, 5f / window.ViewZoom * Boot.WindowScale, color);

            // ring
            Raylib.DrawCircleLinesV(cornerOrigins[i] + lightMapOffset, Vector2.Distance(cornerOrigins[i], pt), color);

            // Line
            Raylib.DrawLineV(cornerOrigins[i] + lightMapOffset, pt + lightMapOffset, color);
        }

        for (int i = 0; i < 4; i++)
        {
            // draw quad lines
            var a = warpPoints[i];
            var b = warpPoints[(i + 1) % 4];
            Raylib.DrawLineV(a + lightMapOffset, b + lightMapOffset, Color.Red);
        }

        if (warpModeSubmit)
        {
            warpModeSubmit = false;

            var imgClone = RlManaged.Image.Copy(level.LightMap.GetImage());

            Rlgl.PushMatrix();
            Rlgl.LoadIdentity();

            level.LightMap.RaylibBeginTextureMode();
            Raylib.ClearBackground(Color.White);
            RlExt.DrawRenderTexture(tmpFramebuffer, 0, 0, Color.White);

            Raylib.BeginTextureMode(mainFrame);

            Rlgl.PopMatrix();

            changeRecorder!.PushWarpChange(imgClone, warpPoints);
            warpMode = false;
        }
    }
}