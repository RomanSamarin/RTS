using System;
using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;

// Адаптер идентичности объектов; проверяем настоящие правила и производственные классы.
public static class FactionMember
{
    private static readonly Dictionary<GameObject,Faction> owners=new Dictionary<GameObject,Faction>();
    public static void Set(GameObject obj,Faction side)=>owners[obj]=side;
    public static Faction Of(GameObject obj)=>owners.TryGetValue(obj,out var side)?side:Faction.Player;
    public static bool CanUse(GameObject actor,GameObject building)=>FactionRules.AreAllies(Of(actor),Of(building));
}
public static class FactionChecks
{
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Faction: "+message);}
    public static void Run()
    {
        foreach(Faction a in Enum.GetValues<Faction>())foreach(Faction b in Enum.GetValues<Faction>())
        {
            bool neutral=a==Faction.Neutral||b==Faction.Neutral;
            Check(FactionRules.AreAllies(a,b)==(!neutral&&a==b),"alliance matrix");
            Check(FactionRules.AreEnemies(a,b)==(!neutral&&a!=b),"hostility matrix");
            Check(FactionRules.AreEnemies(a,b)==FactionRules.AreEnemies(b,a),"symmetric hostility");
        }
        Check(FactionRules.IsControllable(Faction.Player)&&!FactionRules.IsControllable(Faction.Bandits)&&!FactionRules.IsControllable(Faction.Neutral),"local control");
        var players = new[] { Faction.Player, Faction.Player2, Faction.Player3, Faction.Player4 };
        for (int i = 0; i < players.Length; i++)
        {
            Check(FactionRules.PlayerId(players[i]) == i + 1, "stable player identity");
            foreach (var other in players)
                Check(FactionRules.IsControllable(other, players[i]) == (other == players[i]), "control depends on local player");
        }
        Check(FactionRules.InferLegacy(true,true,true,true,true)==Faction.Neutral,"resource before legacy enemy tag");
        Check(FactionRules.InferLegacy(false,true,true,false,true)==Faction.Bandits,"bandit AI overrides worker component");
        Check(FactionRules.InferLegacy(false,false,true,true,true)==Faction.Player,"old builder false flag is not enemy ownership");
        Check(FactionRules.InferLegacy(false,false,false,true,true)==Faction.Bandits,"old hostile soldier migration");
        Check(FactionRules.InferLegacy(false,false,false,false,true)==Faction.Player,"legacy building defaults to player");
        var player=new Mill();var enemy=new WarehouseStorage(); enemy.SetCapacity(100);enemy.AddResource(ResourceType.Wheat,20);
        FactionMember.Set(enemy.gameObject,Faction.Bandits);
        Check(player.AvailableSupply(enemy,ResourceType.Wheat)==0,"cannot plan inputs from hostile warehouse");
        FactionMember.Set(enemy.gameObject,Faction.Player);
        Check(player.AvailableSupply(enemy,ResourceType.Wheat)==20,"allied physical inputs available");
        FactionMember.Set(enemy.gameObject, Faction.Player2);
        Check(player.AvailableSupply(enemy, ResourceType.Wheat) == 0, "second player's stock stays separate");
        FactionMember.Set(player.gameObject, Faction.Player2);
        Check(player.AvailableSupply(enemy, ResourceType.Wheat) == 20, "second player can use own stock");
        var barracks=new TrainingEquipmentStore();
        FactionMember.Set(enemy.gameObject,Faction.Bandits);
        enemy.AddResource(ResourceType.Sword,1);
        Check(barracks.AvailableSupply(enemy,ResourceType.Sword)==0,"training store rejects hostile claims");
        Console.WriteLine("PASS: faction relations, control, legacy migration and production warehouse ownership");
    }
}
