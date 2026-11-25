// 1인칭 총 재질: 모델에 UV가 없어도 물체 공간 잡음으로 총철 결(파커라이징 입자)·닳은 광택·나뭇결을 만든다.
// 가장자리에 옅은 푸른 달빛 테두리 → 어두운 밤에도 총 윤곽이 산다.
Shader "Hunt/GunMetal"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 0.3, 0.32, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0.6
        _Glossiness ("Smoothness", Range(0, 1)) = 0.6
        _Grain ("Grain", Range(0, 1)) = 0.6
        _Wood ("Wood", Range(0, 1)) = 0
        _Rim ("Rim", Range(0, 2)) = 0.7
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        fixed4 _Color;
        half _Metallic, _Glossiness, _Grain, _Wood, _Rim;
        float _HuntWet; // 비 세기 (Atmosphere가 전역으로 넣는다, 0~1)

        struct Input
        {
            float3 opos;
            float3 viewDir;
            float3 worldNormal;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.opos = v.vertex.xyz;
        }

        float hash3(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        float vnoise(float3 x)
        {
            float3 i = floor(x);
            float3 f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            float a = lerp(hash3(i), hash3(i + float3(1, 0, 0)), f.x);
            float b = lerp(hash3(i + float3(0, 1, 0)), hash3(i + float3(1, 1, 0)), f.x);
            float c = lerp(hash3(i + float3(0, 0, 1)), hash3(i + float3(1, 0, 1)), f.x);
            float d = lerp(hash3(i + float3(0, 1, 1)), hash3(i + float3(1, 1, 1)), f.x);
            return lerp(lerp(a, b, f.y), lerp(c, d, f.y), f.z);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 p = IN.opos;
            // 총철: 아주 고운 입자 + 조금 굵은 얼룩
            float fine = vnoise(p * 900.0);
            float mid = vnoise(p * 140.0);
            float wear = vnoise(p * 45.0 + 7.0);
            float metalTone = lerp(1.0 - 0.2 * _Grain, 1.0 + 0.14 * _Grain, fine * 0.55 + mid * 0.45);
            // 나무: 총 길이(z) 방향으로 흐르는 나뭇결 + 옹이 얼룩
            float ring = sin(p.z * 170.0 + vnoise(p * float3(60.0, 60.0, 8.0)) * 9.0) * 0.5 + 0.5;
            float woodTone = lerp(0.72, 1.18, ring) * lerp(0.9, 1.05, mid);
            float tone = lerp(metalTone, woodTone, _Wood);

            // 비에 젖음: 조금 어둡고 더 반들반들 + 군데군데 맺힌 물방울
            float wet = saturate(_HuntWet);
            float drop = step(0.9, vnoise(p * 420.0 + 3.0)) * wet;
            o.Albedo = _Color.rgb * tone * lerp(1.0, 0.85, wet) * (1.0 - drop * 0.25);
            o.Metallic = _Metallic * (1.0 - _Wood);
            // 손이 닿는 곳처럼 군데군데 더 반들반들 (닳은 광택)
            o.Smoothness = saturate(_Glossiness * lerp(0.7, 1.15, wear) * lerp(1.0, 0.55, _Wood) * lerp(0.9, 1.05, fine));
            o.Smoothness = saturate(lerp(o.Smoothness, max(o.Smoothness, 0.75), wet * 0.6) + drop);

            half rim = 1.0 - saturate(dot(normalize(IN.viewDir), normalize(IN.worldNormal)));
            o.Emission = pow(rim, 3.0) * _Rim * half3(0.32, 0.38, 0.5) * 0.22 * (1.0 - _Wood * 0.6);
        }
        ENDCG
    }
    FallBack "Standard"
}
