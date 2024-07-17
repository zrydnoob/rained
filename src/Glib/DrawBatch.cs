using Bgfx_cs;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Glib;

/// <summary>
/// Class used to handle shape drawing
/// </summary>
internal class DrawBatch : BgfxResource
{
    private const uint VertexDataSize = 9;
    private const uint MaxVertices = 4096;
    
    private readonly float[] batchData;
    private int vertexCount;
    private Bgfx.VertexLayout _vertexLayout;

    private MeshPrimitiveType _drawMode;
    public Color DrawColor = Color.White;
    public Vector2 UV = Vector2.Zero;
    public Matrix4x4 TransformMatrix;
    
    private Texture? _texture;
    public Texture? Texture
    {
        get => _texture;
        set
        {
            if (_texture == value) return;
            Draw();
            _texture = value;
        }
    }

    private Shader? _shader;
    public Shader? Shader
    {
        get => _shader;
        set
        {
            if (_shader == value) return;
            Draw();
            _shader = value;
        }
    }

    public Action<Bgfx.StateFlags> DrawCallback { get; set; }

    public unsafe DrawBatch(Action<Bgfx.StateFlags> drawCallback)
    {
        batchData = new float[MaxVertices * VertexDataSize];
        vertexCount = 0;
        _texture = null!;
        DrawCallback = drawCallback;

        var layout = new Bgfx.VertexLayout();
        Bgfx.vertex_layout_begin(&layout, Bgfx.RendererType.Noop);
        Bgfx.vertex_layout_add(&layout, Bgfx.Attrib.Position, 3, Bgfx.AttribType.Float, false, false);
        Bgfx.vertex_layout_add(&layout, Bgfx.Attrib.TexCoord0, 2, Bgfx.AttribType.Float, false, false);
        Bgfx.vertex_layout_add(&layout, Bgfx.Attrib.Color0, 4, Bgfx.AttribType.Float, false, false);
        Bgfx.vertex_layout_end(&layout);
        _vertexLayout = layout;
    }

    public unsafe void NewFrame(Texture initialTex)
    {
        vertexCount = 0;
        _texture = initialTex;
        _shader = null;

        DrawColor = Color.White;
        UV = Vector2.Zero;
    }

    public unsafe void Draw()
    {
        if (vertexCount == 0) return;

        Bgfx.TransientVertexBuffer vertexBuf;
        fixed (Bgfx.VertexLayout* layout = &_vertexLayout)
            Bgfx.alloc_transient_vertex_buffer(&vertexBuf, (uint)vertexCount, layout);

        fixed (float* data = batchData)
        {
            Buffer.MemoryCopy(data, vertexBuf.data, vertexBuf.size, vertexCount * VertexDataSize * sizeof(float));
        }

        Bgfx.set_transient_vertex_buffer(0, &vertexBuf, 0, (uint)vertexCount);

        var state = _drawMode switch
        {
            MeshPrimitiveType.Points => Bgfx.StateFlags.PtPoints,
            MeshPrimitiveType.Lines => Bgfx.StateFlags.PtLines,
            MeshPrimitiveType.LineStrip => Bgfx.StateFlags.PtLinestrip,
            MeshPrimitiveType.Triangles => Bgfx.StateFlags.None,
            MeshPrimitiveType.TriangleStrip => Bgfx.StateFlags.PtTristrip,
            _ => throw new Exception("Invalid MeshPrimitiveType")
        };

        DrawCallback(state);
        vertexCount = 0;
    }

    private void CheckCapacity(uint newVertices)
    {
        if (vertexCount + newVertices >= MaxVertices)
        {
            Draw();
        }
    }

    private void BeginDraw(uint requiredCapacity, MeshPrimitiveType newDrawMode = MeshPrimitiveType.Triangles)
    {
        CheckCapacity(requiredCapacity);

        // flush batch on texture/draw mode change
        if (_drawMode != newDrawMode)
        {
            Draw();
            _drawMode = newDrawMode;
        }
    }

    private void PushVertex(float x, float y)
    {
        var vec = Vector4.Transform(new Vector4(x, y, 0f, 1f), TransformMatrix);

        uint i = (uint)vertexCount * VertexDataSize;
        batchData[i++] = vec.X / vec.W;
        batchData[i++] = vec.Y / vec.W;
        batchData[i++] = vec.Z / vec.W;
        batchData[i++] = UV.X;
        batchData[i++] = UV.Y;
        batchData[i++] = DrawColor.R;
        batchData[i++] = DrawColor.G;
        batchData[i++] = DrawColor.B;
        batchData[i++] = DrawColor.A;

        vertexCount++;
    }

    public struct BatchDrawHandle : IDisposable
    {
        private readonly static Vector2[] verts = [Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero];
        private readonly static Vector2[] uvs = [Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero,];
        private readonly static Color[] colors = [Glib.Color.Transparent, Glib.Color.Transparent, Glib.Color.Transparent, Glib.Color.Transparent];

        private DrawBatch _batch;
        private int vertIndex = 0;
        private readonly BatchDrawMode mode;
        private Vector2 uv;
        private Color color;

        internal BatchDrawHandle(BatchDrawMode mode, DrawBatch batch)
        {
            this.mode = mode;
            this._batch = batch;

            uv = Vector2.Zero;
            color = batch.DrawColor;
        }

        private readonly bool IsFull()
        {
            return mode switch
            {
                BatchDrawMode.Lines => vertIndex >= 2,
                BatchDrawMode.Triangles => vertIndex >= 3,
                BatchDrawMode.Quads => vertIndex >= 4,
                _ => false,
            };
        }

        public void Flush()
        {
            switch (mode)
            {
                case BatchDrawMode.Triangles:
                {
                    _batch.BeginDraw(3, MeshPrimitiveType.Triangles);
                    for (int i = 0; i < 3; i++)
                    {
                        _batch.DrawColor = colors[i];
                        _batch.UV = uvs[i];
                        _batch.PushVertex(verts[i].X, verts[i].Y);
                    }
                    break;
                }

                case BatchDrawMode.Quads:
                {
                    _batch.BeginDraw(6, MeshPrimitiveType.Triangles);

                    // first triangle
                    _batch.DrawColor = colors[0];
                    _batch.UV = uvs[0];
                    _batch.PushVertex(verts[0].X, verts[0].Y);

                    _batch.DrawColor = colors[1];
                    _batch.UV = uvs[1];
                    _batch.PushVertex(verts[1].X, verts[1].Y);

                    _batch.DrawColor = colors[2];
                    _batch.UV = uvs[2];
                    _batch.PushVertex(verts[2].X, verts[2].Y);

                    // second triangle
                    _batch.PushVertex(verts[2].X, verts[2].Y);

                    _batch.DrawColor = colors[3];
                    _batch.UV = uvs[3];
                    _batch.PushVertex(verts[3].X, verts[3].Y);

                    _batch.DrawColor = colors[0];
                    _batch.UV = uvs[0];
                    _batch.PushVertex(verts[0].X, verts[0].Y);
                    break;
                }

                case BatchDrawMode.Lines:
                {
                    _batch.BeginDraw(2, MeshPrimitiveType.Lines);

                    _batch.DrawColor = colors[0];
                    _batch.UV = uvs[0];
                    _batch.PushVertex(verts[0].X, verts[0].Y);

                    _batch.DrawColor = colors[1];
                    _batch.UV = uvs[1];
                    _batch.PushVertex(verts[1].X, verts[1].Y);
                    break;
                }
            }

            vertIndex = 0;
        }

        public void Vertex(Vector2 v)
            => Vertex(v.X, v.Y);

        public void Vertex(float x, float y)
        {
            if (IsFull()) Flush();
            uvs[vertIndex] = uv;
            colors[vertIndex] = color;
            verts[vertIndex] = new Vector2(x, y);
            vertIndex++;
        }

        public void TexCoord(float u, float v)
        {
            uv = new Vector2(u, v);
        }

        public void TexCoord(Vector2 uv)
        {
            this.uv = uv;
        }

        public void Color(Color color)
        {
            this.color = color;
        }

        public void End()
        {
            if (IsFull()) Flush();
        }

        public void Dispose() => End();
    }

    public BatchDrawHandle BeginBatchDraw(BatchDrawMode mode, Texture? tex = null)
    {
        Texture = tex;
        return new BatchDrawHandle(mode, this);
    }

    protected override void FreeResources(bool disposing)
    {
    }
}