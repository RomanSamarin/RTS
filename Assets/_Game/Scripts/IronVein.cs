using _Game.Scripts.UI;

public class IronVein : OreDeposit
{
    public override ResourceType Resource => ResourceType.Iron;
    protected override int DefaultAmount => 10;
}
