using System;

public static class MovementOrderChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run()
    {
        var orders = new MovementOrderState();
        Check(orders.TryBeginFood(out int food), "idle unit may seek food");
        Check(!orders.TryBeginFood(out _), "a second food route cannot replace active route");
        int move = orders.Begin(MovementOrderState.Order.PlayerMove);
        Check(!orders.Owns(food) && !orders.End(food), "late food cancellation cannot clear player move");
        Check(orders.Current == MovementOrderState.Order.PlayerMove && !orders.TryBeginFood(out _), "hunger cannot replace player movement");
        int attack = orders.Begin(MovementOrderState.Order.Combat);
        Check(!orders.End(move) && !orders.TryBeginFood(out _), "late arrival cannot clear attack; hunger cannot replace combat");
        Check(orders.End(attack), "combat releases ownership");
        Check(orders.TryBeginFood(out int nextFood), "food becomes available when priority order ends");
        Check(!orders.End(food) && orders.Owns(nextFood), "previous food request cannot cancel new food request");
        Check(orders.End(nextFood) && !orders.End(nextFood), "completion and cancellation are idempotent");
        Check(orders.TryBeginFood(out int disabledFood), "disable setup");
        Check(orders.End(disabledFood) && orders.Current == MovementOrderState.Order.None, "interrupted food releases ownership");
        int combat = orders.Begin(MovementOrderState.Order.Combat);
        int retreat = orders.Begin(MovementOrderState.Order.Retreat);
        Check(!orders.End(combat) && orders.Owns(retreat), "old combat completion cannot cancel retreat");
        Check(!orders.TryBeginFood(out _) && !orders.TryBeginSupply(out _), "food and escort cannot interrupt retreat");
        int replacementMove = orders.Begin(MovementOrderState.Order.PlayerMove);
        Check(!orders.End(retreat) && orders.Owns(replacementMove), "new player movement replaces retreat; stale arrival cannot cancel it");
        retreat = orders.Begin(MovementOrderState.Order.Retreat);
        Check(orders.End(retreat) && orders.Current == MovementOrderState.Order.None, "arrival or stop releases retreat");
        Check(orders.TryBeginFood(out _), "food is available again after retreat");
        Console.WriteLine("PASS: food/manual/combat/retreat priority and stale order cancellation");
    }
}
