Shader "Lords of the Valley/Cloud Sky"
{
 Properties
 {
  _Zenith ("Zenith", Color) = (0.15,0.40,0.68,1)
  _Horizon ("Horizon", Color) = (0.68,0.80,0.86,1)
  _CloudColor ("Cloud Light", Color) = (1,0.96,0.88,1)
  _Coverage ("Cloud Coverage", Range(0,1)) = 0.48
  _Storm ("Storm Front", Range(0,1)) = 0.2
 }
 SubShader
 {
  Tags { "Queue"="Background" "RenderType"="Background" "RenderPipeline"="UniversalPipeline" }
  Cull Off ZWrite Off
  Pass
  {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   float4 _Zenith, _Horizon, _CloudColor;
   float _Coverage, _Storm;
   struct V { float4 position : SV_POSITION; float3 ray : TEXCOORD0; };
   V vert(float4 position : POSITION)
   {
    V o; o.position=TransformObjectToHClip(position.xyz); o.ray=position.xyz; return o;
   }
   float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
   float noise(float2 p)
   {
    float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
    return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
   }
   float fbm(float2 p)
   {
    float value=0, weight=0.5;
    for(int i=0;i<5;i++) { value+=noise(p)*weight; p=float2(p.x*1.6-p.y*1.2,p.x*1.2+p.y*1.6)+13.4; weight*=0.5; }
    return value;
   }
   half4 frag(V input) : SV_Target
   {
    float3 ray=normalize(input.ray);
    float3 sun=GetMainLight().direction;
    float sunDot=saturate(dot(ray,sun));
    float3 color=lerp(_Horizon.rgb,_Zenith.rgb,pow(saturate(ray.y),0.45));
    color+=float3(1,0.75,0.43)*pow(sunDot,24)*0.24;
    color+=float3(1,0.92,0.72)*smoothstep(0.9996,0.99985,sunDot)*3;
    float2 p=ray.xz/max(0.16,ray.y+0.18)*2.8+_Time.y*float2(0.005,0.002);
    float storm=smoothstep(-0.25,0.65,ray.x)*_Storm;
    float density=fbm(p);
    float coverage=smoothstep(0.66-_Coverage*0.32-storm*0.12,0.82-_Coverage*0.32-storm*0.12,density);
    float shade=saturate((fbm(p+sun.xz*0.16)-density)*6+0.55);
    float3 cloud=lerp(float3(0.38,0.49,0.61),_CloudColor.rgb,shade);
    cloud=lerp(cloud,cloud*0.62,storm);
    float cirrus=smoothstep(0.52,0.72,fbm(p*float2(0.65,4)+23))*0.24;
    color=lerp(color,_CloudColor.rgb,cirrus*smoothstep(0,0.25,ray.y));
    color=lerp(color,cloud,coverage*smoothstep(-0.02,0.13,ray.y));
    return half4(color,1);
   }
   ENDHLSL
  }
 }
}
