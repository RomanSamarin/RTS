public static class ArmyRejoinRules
{
    public static bool Eligible(bool fullyRecovered,bool armyActive,bool sameArmy,bool allied,bool soldier)
        =>fullyRecovered&&armyActive&&sameArmy&&allied&&soldier;
    public static bool Arrived(float distance,float radius,bool armyMoving)=>!armyMoving&&distance<=radius;
}
