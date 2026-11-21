using System.Collections.Generic;

/// <summary>Host owns slot 1. A reconnect gets a free slot, not a trusted client-supplied owner.</summary>
public sealed class NetworkPlayerSlots
{
    private readonly Dictionary<ulong, int> players = new Dictionary<ulong, int>();
    public bool TryJoin(ulong client, out int slot)
    {
        if (players.TryGetValue(client, out slot)) return true;
        for (int candidate = 1; candidate <= 4; candidate++)
        {
            if (players.ContainsValue(candidate)) continue;
            players.Add(client, candidate); slot = candidate; return true;
        }
        slot = 0; return false;
    }
    public int Get(ulong client) => players.TryGetValue(client, out var slot) ? slot : 0;
    public void Leave(ulong client) => players.Remove(client);
    public void Clear() => players.Clear();
    public static Faction Side(int slot) => slot == 1 ? Faction.Player : slot == 2 ? Faction.Player2
        : slot == 3 ? Faction.Player3 : slot == 4 ? Faction.Player4 : Faction.Neutral;
    public static bool CanCommand(int slot, Faction owner) => slot > 0 && slot <= 4 && Side(slot) == owner;
    public static bool ValidPoint(float x, float y, float z) => Finite(x) && Finite(y) && Finite(z);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && System.Math.Abs(value) <= 100000f;
}
