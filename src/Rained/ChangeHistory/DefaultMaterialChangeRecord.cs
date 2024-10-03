// this is just a single property, so I figured it wasn't worth it
// to write a whole DefaultMaterialChangeRecorder for it, and instead
// write the code for that in TileEditor
using Rained.EditorGui.Editors;
namespace Rained.ChangeHistory;

class DefaultMaterialChangeRecord : IChangeRecord
{
    private readonly int oldMat;
    private readonly int newMat;

    public DefaultMaterialChangeRecord(int oldMat, int newMat)
    {
        this.oldMat = oldMat;
        this.newMat = newMat;
    }

    public void Apply(bool useNew)
    {
        RainEd.Instance.LevelView.EditMode = (int) EditModeEnum.Tile;
        RainEd.Instance.Level.DefaultMaterial = useNew ? newMat : oldMat;
    }
}