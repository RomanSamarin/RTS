using System;

public static class HungerChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run()
    {
        var hunger = new HungerTimer();
        Check(!hunger.TryDamage(1000, 20), "fed unit cannot receive hunger damage");
        hunger.Begin(100, 120);
        Check(hunger.Remaining(100) == 120 && !hunger.TryDamage(219.9f, 20), "full grace also applies when hunger starts long after scene start");
        hunger.Begin(180, 120);
        Check(hunger.StartedAt == 100 && hunger.Remaining(180) == 40, "repeated search, combat or orders cannot restart grace");
        Check(hunger.TryDamage(220, 20) && !hunger.TryDamage(220, 20), "first damage at grace boundary occurs once");
        Check(!hunger.TryDamage(239, 20) && hunger.TryDamage(240, 20), "periodic damage starts after grace");
        var replacement = new HungerTimer();
        replacement.Restore(hunger.StartedAt, hunger.DamageBeginsAt, hunger.NextDamageAt);
        Check(replacement.Remaining(250) == 0 && !replacement.TryDamage(259, 20) && replacement.TryDamage(260, 20), "changing profession preserves elapsed grace and next damage tick");
        replacement.Reset(); replacement.Begin(300, 120);
        Check(replacement.Remaining(300) == 120 && !replacement.TryDamage(419, 20), "new hunger after meal gets a new full grace period");

        var orders = new MovementOrderState();
        int supply = orders.Begin(MovementOrderState.Order.Supply);
        Check(orders.TryBeginFood(out int meal) && !orders.End(supply), "automatic supply duty yields to hunger");
        orders.End(meal);
        int manual = orders.Begin(MovementOrderState.Order.PlayerMove);
        Check(!orders.TryBeginFood(out _), "manual movement blocks meal until arrival");
        orders.End(manual); Check(orders.TryBeginFood(out meal), "meal resumes as soon as player movement completes");
        int combat = orders.Begin(MovementOrderState.Order.Combat);
        Check(!orders.End(meal) && !orders.TryBeginFood(out _), "combat preempts an existing meal trip");
        orders.End(combat); Check(orders.TryBeginFood(out _), "meal may resume after combat");
        Console.WriteLine("PASS: hunger grace, interrupted searches, profession transfer, repeated meals and food priorities");
    }
}
