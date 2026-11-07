using System;
public static class SiegeRules
{
    public static bool InRange(float distance,float minimum,float maximum)=>distance>=Math.Max(0,minimum)&&distance<=Math.Max(minimum,maximum);
    public static float Splash(float distance,float radius)=>radius<=0?0:Math.Max(0,1-distance/radius);
}
