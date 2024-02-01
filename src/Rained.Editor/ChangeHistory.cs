using System.Numerics;
namespace RainEd;

public class ChangeHistory
{
    private readonly RainEd editor;
    public Level Level { get => editor.Level; }

    public ChangeHistory(RainEd editor)
    {
        this.editor = editor;
    }
    
    private struct CellChange
    {
        public int X, Y, Layer;
        public LevelCell OldState, NewState;
    };

    private struct CameraData
    {
        public Vector2 Position;
        public float[] CornerOffsets = new float[4];
        public float[] CornerAngles = new float[4];

        public CameraData(Camera camera)
        {
            Position = camera.Position;
            camera.CornerOffsets.CopyTo(CornerOffsets, 0);
            camera.CornerAngles.CopyTo(CornerAngles, 0);
        }

        public readonly void Apply(Camera camera)
        {
            camera.Position = Position;
            CornerOffsets.CopyTo(camera.CornerOffsets, 0);
            CornerAngles.CopyTo(camera.CornerAngles, 0);
        }

        public readonly bool IsEqual(Camera other)
        {
            if (Position != other.Position) return false;

            for (int i = 0; i < 4; i++)
                if (CornerOffsets[i] != other.CornerOffsets[i]) return false;
            
            return true;
        }
    }

    private class CameraChange
    {
        public CameraData[] OldData;
        public CameraData[] NewData;

        public CameraChange(CameraData[] oldData, CameraData[] newData)
        {
            OldData = oldData;
            NewData = newData;
        }
    }

    private struct ChangeRecord
    {
        public int EditMode;

        public List<CellChange> CellChanges = new();
        public CameraChange? CameraChange = null;

        public ChangeRecord() {}

        public readonly bool HasChange()
        {
            return CellChanges.Count > 0 || CameraChange is not null;
        }

        public readonly void Apply(Level level, bool useNew)
        {
            // apply cell changes
            foreach (CellChange change in CellChanges)
            {
                level.Layers[change.Layer, change.X, change.Y] = useNew ? change.NewState : change.OldState;
            }

            // apply camera changes
            if (CameraChange is not null)
            {
                Console.WriteLine("apply cameras");
                var data = useNew ? CameraChange.NewData : CameraChange.OldData;
                if (level.Cameras.Count > data.Length) level.Cameras.RemoveRange(data.Length-1, level.Cameras.Count - data.Length);
                for (int i = 0; i < data.Length; i++)
                {
                    if (i < level.Cameras.Count)
                        data[i].Apply(level.Cameras[i]);
                    else
                    {
                        var cam = new Camera();
                        data[i].Apply(cam);
                        level.Cameras.Add(cam);
                    }
                }
            }
        }
    }

    private class Snapshot
    {
        public int EditMode;
        public LevelCell[,,] Layers;
        public CameraData[] Cameras;

        public Snapshot(Level level, int editMode)
        {
            EditMode = editMode;

            // get layers
            Layers = (LevelCell[,,]) level.Layers.Clone();
            
            // get cameras
            Cameras = new CameraData[level.Cameras.Count];
            for (int i = 0; i < level.Cameras.Count; i++)
            {
                Cameras[i] = new CameraData(level.Cameras[i]);
            }
        }

    }

    private readonly Stack<ChangeRecord> undoStack = new();
    private readonly Stack<ChangeRecord> redoStack = new();
    private Snapshot? oldSnapshot = null;

    public void BeginChange()
    {
        if (oldSnapshot is not null) throw new Exception("BeginChange() already called");
        oldSnapshot = new Snapshot(Level, editor.Window.EditMode);    
    }

    public void TryEndChange()
    {
        if (oldSnapshot is null) return;
        EndChange();
    }

    public void EndChange()
    {
        if (oldSnapshot is null) throw new Exception("EndChange() already called");
        redoStack.Clear();
        ChangeRecord changes = new()
        {
            EditMode = oldSnapshot.EditMode
        };

        // find changes made to layers
        for (int l = 0; l < Level.LayerCount; l++)
        {
            for (int x = 0; x < Level.Width; x++)
            {
                for (int y = 0; y < Level.Height; y++)
                {
                    if (!oldSnapshot.Layers[l,x,y].Equals(Level.Layers[l,x,y]))
                    {
                        changes.CellChanges.Add(new CellChange()
                        {
                            X = x, Y = y, Layer = l,
                            OldState = oldSnapshot.Layers[l,x,y],
                            NewState = Level.Layers[l,x,y]
                        });
                    }
                }
            }
        }

        // find changes made to cameras
        bool camerasChanged = oldSnapshot.Cameras.Length != Level.Cameras.Count;
        if (!camerasChanged)
            for (int i = 0; i < oldSnapshot.Cameras.Length; i++)
            {
                if (!oldSnapshot.Cameras[i].IsEqual(Level.Cameras[i]))
                {
                    camerasChanged = true;
                    break;
                }
            }
        
        if (camerasChanged)
        {
            var newCameraData = new CameraData[Level.Cameras.Count];
            for (int i = 0; i < Level.Cameras.Count; i++)
                newCameraData[i] = new CameraData(Level.Cameras[i]);
            
            changes.CameraChange = new CameraChange(oldSnapshot.Cameras, newCameraData);
        }

        // record changes
        if (changes.HasChange())
            undoStack.Push(changes);

        oldSnapshot = null;
    }

    public void Undo()
    {
        if (undoStack.Count == 0) return;
        var record = undoStack.Pop();
        redoStack.Push(record);
        editor.Window.EditMode = record.EditMode;
        record.Apply(Level, false);
    }

    public void Redo()
    {
        if (redoStack.Count == 0) return;
        var record = redoStack.Pop();
        undoStack.Push(record);
        editor.Window.EditMode = record.EditMode;
        record.Apply(Level, true);
    }
}