using ImGuiNET;
using Raylib_cs;
using Rained.LevelData;
namespace Rained.EditorGui.Editors;

class EnvironmentEditor : IEditorMode
{
    public string Name { get => "环境"; }
    private readonly LevelWindow window;

    private ChangeHistory.EnvironmentChangeRecorder changeRecorder;
    private bool isDragging = false;

    public EnvironmentEditor(LevelWindow window)
    {
        this.window = window;
        changeRecorder = new();
        changeRecorder.TakeSnapshot();

        RainEd.Instance.ChangeHistory.Cleared += () =>
        {
            changeRecorder = new();
            changeRecorder.TakeSnapshot();
        };

        RainEd.Instance.ChangeHistory.UndidOrRedid += () =>
        {
            changeRecorder.TakeSnapshot();
        };
    }

    public void Unload()
    {
        changeRecorder.PushChange();
    }

    private void RecordItemChanges()
    {
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            changeRecorder.PushChange();
        }
    }

    public void DrawToolbar()
    {
        var level = RainEd.Instance.Level;

        if (ImGui.Begin("环境", ImGuiWindowFlags.NoFocusOnAppearing))
        {
            ImGui.Text("瓦片随机种子");
            ImGui.SetNextItemWidth(-0.001f);

            ImGui.SliderInt("##seed", ref level.TileSeed, 0, 400, "%i", ImGuiSliderFlags.AlwaysClamp);
            RecordItemChanges();

            ImGui.Checkbox("封闭房间", ref level.DefaultMedium);
            RecordItemChanges();

            ImGui.Checkbox("日光", ref level.HasSunlight);
            RecordItemChanges();

            ImGui.Checkbox("水", ref level.HasWater);
            RecordItemChanges();

            ImGui.Checkbox("水渲染在最前面", ref level.IsWaterInFront);
            RecordItemChanges();
        }
        ImGui.End();
    }

    private void DrawWater()
    {
        var level = RainEd.Instance.Level;

        float waterHeight = level.WaterLevel + level.BufferTilesBot + 0.5f;
        Raylib.DrawRectangle(
            0,
            (int)((level.Height - waterHeight) * Level.TileSize),
            level.Width * Level.TileSize,
            (int)(waterHeight * Level.TileSize),
            new Color(0, 0, 255, 100)
        );
    }

    public void DrawViewport(RlManaged.RenderTexture2D mainFrame, RlManaged.RenderTexture2D[] layerFrames)
    {
        bool wasDragging = isDragging;
        isDragging = false;

        var level = RainEd.Instance.Level;
        var levelRender = window.Renderer;

        // set water level
        if (window.IsViewportHovered && level.HasWater)
        {
            if (EditorWindow.IsMouseDown(ImGuiMouseButton.Left))
            {
                isDragging = true;

                var my = Math.Clamp(window.MouseCy, -1, level.Height - level.BufferTilesBot);
                level.WaterLevel = (int)(level.Height - level.BufferTilesBot - 0.5f - my);
            }
        }

        // draw level background (solid white)
        Raylib.DrawRectangle(0, 0, level.Width * Level.TileSize, level.Height * Level.TileSize, LevelWindow.BackgroundColor);

        // draw the layers
        var drawTiles = RainEd.Instance.Preferences.ViewTiles;
        var drawProps = RainEd.Instance.Preferences.ViewProps;

        for (int l = Level.LayerCount - 1; l >= 0; l--)
        {
            var alpha = l == 0 ? 255 : 50;
            var color = LevelWindow.GeoColor(alpha);
            int offset = l * 2;

            Rlgl.PushMatrix();
            Rlgl.Translatef(offset, offset, 0f);
            levelRender.RenderGeometry(l, color);

            if (drawTiles)
                levelRender.RenderTiles(l, (int)(alpha * (100.0f / 255.0f)));

            if (drawProps)
                levelRender.RenderProps(l, (int)(alpha * (100.0f / 255.0f)));

            Rlgl.PopMatrix();

            // draw water behind first layer if set
            if (l == 1 && level.HasWater && !level.IsWaterInFront)
                DrawWater();
        }

        // draw water
        if (level.HasWater && level.IsWaterInFront)
            DrawWater();

        levelRender.RenderGrid();
        levelRender.RenderBorder();
        levelRender.RenderCameraBorders();

        if (wasDragging && !isDragging)
            changeRecorder.PushChange();
    }
}