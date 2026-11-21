using System;

public static class NetworkPlayerChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception("Network: " + message); }
    public static void Run()
    {
        var slots = new NetworkPlayerSlots();
        ulong[] clients = { 0, 109, 502, 43 };
        for (int i = 0; i < clients.Length; i++)
        {
            Check(slots.TryJoin(clients[i], out int slot) && slot == i + 1, "four unique slots");
            Check(slots.TryJoin(clients[i], out slot) && slot == i + 1, "duplicate approval preserves slot");
            foreach (Faction side in Enum.GetValues<Faction>())
                Check(NetworkPlayerSlots.CanCommand(slot, side) == (FactionRules.PlayerId(side) == slot), "ownership matrix");
        }
        Check(!slots.TryJoin(1000, out _) && slots.Get(1000) == 0, "fifth connection rejected");
        Check(!NetworkPlayerSlots.CanCommand(slots.Get(999), Faction.Player), "unknown sender rejected");
        slots.Leave(502);
        Check(slots.TryJoin(800, out int reused) && reused == 3 && slots.Get(502) == 0, "disconnected sender loses authority");
        Check(!NetworkPlayerSlots.CanCommand(5, Faction.Neutral), "invalid slot rejected");
        Check(NetworkPlayerSlots.ValidPoint(0, 5, -200), "normal point accepted");
        Check(!NetworkPlayerSlots.ValidPoint(float.NaN, 0, 0), "NaN rejected");
        Check(!NetworkPlayerSlots.ValidPoint(0, float.PositiveInfinity, 0), "infinity rejected");
        Check(!NetworkPlayerSlots.ValidPoint(0, 0, float.MaxValue), "out of world bounds rejected");
        slots.Clear();
        Check(slots.Get(0) == 0 && slots.TryJoin(0, out reused) && reused == 1, "reset clears authority");
        Console.WriteLine("Network player/ownership/point checks passed.");
    }
}
