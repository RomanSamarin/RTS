/// <summary>Номер команды не даёт завершению старого приказа отменить новый.</summary>
public sealed class MovementOrderState
{
    public enum Order { None, Food, PlayerMove, Combat, Supply, Retreat }
    public Order Current { get; private set; }
    private int revision;
    public bool Owns(int token) => token != 0 && token == revision && Current != Order.None;
    public bool TryBeginFood(out int token)
    {
        token = 0;
        if (Current != Order.None && Current != Order.Supply) return false;
        token = Begin(Order.Food); return true;
    }
    public bool TryBeginSupply(out int token)
    {
        token = 0;
        if (Current != Order.None) return false;
        token = Begin(Order.Supply); return true;
    }
    public int Begin(Order order)
    {
        if (order == Order.None) throw new System.ArgumentException("Use End to release an order.");
        unchecked { revision++; if (revision == 0) revision++; }
        Current = order; return revision;
    }
    public bool End(int token)
    {
        if (!Owns(token)) return false;
        Current = Order.None; return true;
    }
}
