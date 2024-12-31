using Rained.LevelData;
using Rained.EditorGui.Editors;
namespace Rained.ChangeHistory;

struct EffectConfigData
{
    public Effect.LayerMode Layer;
    public bool Is3D;
    public int PlantColor;
    public bool AffectGradientsAndDecals;
    public int[] CustomValues;
    public int Seed;

    public EffectConfigData(Effect effect)
    {
        Layer = effect.Layer;
        Is3D = effect.Is3D;
        PlantColor = effect.PlantColor;
        CustomValues = new int[effect.CustomValues.Length];
        Seed = effect.Seed;
        AffectGradientsAndDecals = effect.AffectGradientsAndDecals;

        for (int i = 0; i < effect.CustomValues.Length; i++)
        {
            CustomValues[i] = effect.CustomValues[i];
        }
    }

    public readonly void Apply(Effect effect)
    {
        effect.Layer = Layer;
        effect.Is3D = Is3D;
        effect.PlantColor = PlantColor;
        effect.Seed = Seed;
        effect.AffectGradientsAndDecals = AffectGradientsAndDecals;

        for (int i = 0; i < CustomValues.Length; i++)
        {
            effect.CustomValues[i] = CustomValues[i];
        }
    }
};

class EffectMatrixChangeRecord : IChangeRecord
{
    private readonly Effect effect;
    private readonly float[,] oldMatrix;
    private readonly float[,] newMatrix;

    public EffectMatrixChangeRecord(Effect effect, float[,] oldMatrix, float[,] newMatrix)
    {
        this.effect = effect;
        this.oldMatrix = oldMatrix;
        this.newMatrix = newMatrix;
    }

    public void Apply(bool useNew)
    {
        RainEd.Instance.LevelView.EditMode = (int) EditModeEnum.Effect;
        RainEd.Instance.LevelView.GetEditor<EffectsEditor>().SelectedEffect = RainEd.Instance.Level.Effects.IndexOf(effect);

        var matrixToApply = useNew ? newMatrix : oldMatrix;

        for (int x = 0; x < effect.Width; x++)
        {
            for (int y = 0; y < effect.Height; y++)
            {
                effect.Matrix[x,y] = matrixToApply[x,y];
            }
        }
    }
}

class EffectsListChangeRecord : IChangeRecord
{
    private readonly Effect[] oldList;
    private readonly Effect[] newList;
    private readonly int oldSelected;
    private readonly int newSelected;

    public EffectsListChangeRecord(Effect[] oldList, Effect[] newList, int oldSelected, int newSelected)
    {
        this.oldList = oldList;
        this.newList = newList;
        this.oldSelected = oldSelected;
        this.newSelected = newSelected;
    }

    public void Apply(bool useNew)
    {
        RainEd.Instance.LevelView.EditMode = (int) EditModeEnum.Effect;

        var apply = useNew ? newList : oldList;
        var fxList = RainEd.Instance.Level.Effects;
        fxList.Clear();

        for (int i = 0; i < apply.Length; i++)
        {
            fxList.Add(apply[i]);
        }

        // change selected effect
        EffectsEditor fxEditor = RainEd.Instance.LevelView.GetEditor<EffectsEditor>();
        fxEditor.SelectedEffect = useNew ? newSelected : oldSelected;
    }
}

class EffectConfigChangeRecord : IChangeRecord
{
    private readonly Effect effect;
    private readonly EffectConfigData oldConfig;
    private readonly EffectConfigData newConfig;

    public EffectConfigChangeRecord(Effect effect, EffectConfigData oldConfig, EffectConfigData newConfig)
    {
        this.effect = effect;
        this.oldConfig = oldConfig;
        this.newConfig = newConfig;
    }

    public void Apply(bool useNew)
    {
        var fxList = RainEd.Instance.Level.Effects;
        RainEd.Instance.LevelView.EditMode = (int) EditModeEnum.Effect;
        RainEd.Instance.LevelView.GetEditor<EffectsEditor>().SelectedEffect = fxList.IndexOf(effect);

        var target = useNew ? newConfig : oldConfig;
        target.Apply(effect);
    }
}

class EffectsChangeRecorder : ChangeRecorder
{
    private Effect? activeEffect = null;
    private float[,] snapshot = new float[0,0];
    private Effect[]? oldFxList = null;
    private int selectedEffect;
    
    private Effect? activeConfigEffect = null;
    private EffectConfigData configSnapshot;

    public void BeginMatrixChange(Effect effect)
    {
        if (activeEffect != null)
        {
            ValidationError("EffectsChangeRecorder.BeginMatrixChange() called twice");
            return;
        }
        
        activeEffect = effect;
        snapshot = (float[,]) activeEffect.Matrix.Clone();
    }

    public void PushMatrixChange()
    {
        if (activeEffect == null)
        {
            ValidationError("EffectsChangeRecorder.PushMatrixChange() called, but recorder is not active");
            return;
        }
        
        // check if it had changed
        bool didChange = false;
        for (int x = 0; x < activeEffect.Width; x++)
        {
            for (int y = 0; y < activeEffect.Height; y++)
            {
                if (snapshot[x,y] != activeEffect.Matrix[x,y])
                {
                    didChange = true;
                    break;
                }
            }
        }

        // if it had changed, push a change record
        if (didChange)
        {
            RainEd.Instance.ChangeHistory.Push(new EffectMatrixChangeRecord(activeEffect, snapshot, (float[,]) activeEffect.Matrix.Clone()));
        }

        activeEffect = null;
    }

    public void TryPushMatrixChange()
    {
        if (activeEffect == null) return;
        PushMatrixChange();
    }

    public void BeginListChange()
    {
        if (oldFxList != null)
        {
            ValidationError("EffectsChangeRecorder.BeginListChange() called twice");
            return;
        }

        EffectsEditor fxEditor = RainEd.Instance.LevelView.GetEditor<EffectsEditor>();
        
        oldFxList = RainEd.Instance.Level.Effects.ToArray();
        selectedEffect = fxEditor.SelectedEffect;
    }

    public void PushListChange()
    {
        if (oldFxList == null)
        {
            ValidationError("EffectsChangeRecorder.PushListChange() called, but recorder is not active");
            return;
        }
        
        EffectsEditor fxEditor = RainEd.Instance.LevelView.GetEditor<EffectsEditor>();

        var newFxList = RainEd.Instance.Level.Effects;
        var newSelectedEffect = fxEditor.SelectedEffect;

        bool didChange = newFxList.Count != oldFxList.Length;
        if (!didChange) // if the length of the fx stack didn't change, loop through the array
        {
            for (int i = 0; i < newFxList.Count; i++)
            {
                if (newFxList[i] != oldFxList[i])
                {
                    didChange = true;
                    break;
                }
            }
        }

        // don't do anything if it has been concluded there wasn't actually
        // a change
        if (didChange)
        {
            RainEd.Instance.ChangeHistory.Push(new EffectsListChangeRecord(
                oldFxList, newFxList.ToArray(),
                selectedEffect, newSelectedEffect    
            ));
        }

        oldFxList = null;
    }

    public void TryPushListChange()
    {
        if (oldFxList == null) return;
        PushListChange();
    }

    public void SetCurrentConfig(Effect effect)
    {
        if (activeConfigEffect == effect) return;

        activeConfigEffect = effect;
        configSnapshot = new(effect);
    }

    public void UpdateConfigSnapshot()
    {
        if (activeConfigEffect is not null)
            configSnapshot = new(activeConfigEffect);
    }

    public void PushConfigChange()
    {
        if (activeConfigEffect is not null)
        {
            var currentState = new EffectConfigData(activeConfigEffect);

            if (!currentState.Equals(configSnapshot))
            {
                RainEd.Instance.ChangeHistory.Push(new EffectConfigChangeRecord(activeConfigEffect, configSnapshot, currentState));
            }

            UpdateConfigSnapshot();
        }
    }
}