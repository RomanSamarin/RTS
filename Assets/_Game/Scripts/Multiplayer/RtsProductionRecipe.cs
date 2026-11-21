using _Game.Scripts.UI;

/// <summary>A host-only instance of an ordinary catalog recipe.</summary>
public sealed class RtsProductionRecipe : RecipeBuilding
{
    public override ResourceType InputType => input;
    public override ResourceType OutputType => output;
    public override ResourcesMove.UnitRole Profession => profession;
    private ResourceType input;
    private ResourceType output;
    private ResourcesMove.UnitRole profession;

    internal void Configure(RecipeBuilding definition)
    {
        input = definition.InputType;
        output = definition.OutputType;
        profession = definition.Profession;
        inputPerBatch = definition.inputPerBatch;
        outputPerBatch = definition.outputPerBatch;
        secondsPerBatch = definition.secondsPerBatch;
        inputCapacity = definition.inputCapacity;
        outputCapacity = definition.outputCapacity;
    }
}
