using _Game.Scripts.UI;

public class GoldVein : OreDeposit
{
    public override ResourceType Resource => ResourceType.Gold;
    protected override int DefaultAmount => 5;
}
