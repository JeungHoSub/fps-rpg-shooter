// 화면 색감 (영화풍): 채도 조금 낮춤 · 어두운 곳 푸르게/밝은 곳 따뜻하게 · 대비 · 가장자리 어둡게 · 고운 필름 입자
// Core/PostGrade.cs 가 메인 카메라에서 쓴다. 내장 그래픽에서도 가볍게 (텍스처 한 번 읽기).
Shader "Hidden/Hunt/Grade"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Fxaa;
            float _Sat;
            float _Contrast;
            float _Pivot;
            float _Vignette;
            float _Grain;
            float _Seed;
            float4 _Shadow;
            float4 _Highlight;

            float3 S (float2 uv) { return tex2Dlod(_MainTex, float4(uv, 0.0, 0.0)).rgb; } // 분기 안에서도 안전한 읽기
            float Luma (float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

            // 가벼운 계단 현상 줄이기 (FXAA 2 방식, 9번 읽기) — 안티에일리어싱이 꺼진 '하'·'중' 품질용
            float3 Fxaa (float2 uv)
            {
                float2 t = _MainTex_TexelSize.xy;
                float3 cM = S(uv);
                float lNW = Luma(S(uv + float2(-1.0, -1.0) * t));
                float lNE = Luma(S(uv + float2( 1.0, -1.0) * t));
                float lSW = Luma(S(uv + float2(-1.0,  1.0) * t));
                float lSE = Luma(S(uv + float2( 1.0,  1.0) * t));
                float lM = Luma(cM);
                float lMin = min(lM, min(min(lNW, lNE), min(lSW, lSE)));
                float lMax = max(lM, max(max(lNW, lNE), max(lSW, lSE)));
                if (lMax - lMin < max(0.03, lMax * 0.125)) return cM; // 가장자리가 아니면 그대로
                float2 dir;
                dir.x = -((lNW + lNE) - (lSW + lSE));
                dir.y =  ((lNW + lSW) - (lNE + lSE));
                float reduce = max((lNW + lNE + lSW + lSE) * 0.03125, 1.0 / 128.0);
                float rcpMin = 1.0 / (min(abs(dir.x), abs(dir.y)) + reduce);
                dir = clamp(dir * rcpMin, -8.0, 8.0) * t;
                float3 a = 0.5 * (S(uv + dir * (1.0 / 3.0 - 0.5)) + S(uv + dir * (2.0 / 3.0 - 0.5)));
                float3 b = a * 0.5 + 0.25 * (S(uv - dir * 0.5) + S(uv + dir * 0.5));
                float lB = Luma(b);
                return (lB < lMin || lB > lMax) ? a : b;
            }

            fixed4 frag (v2f_img i) : SV_Target
            {
                float3 c = _Fxaa > 0.5 ? Fxaa(i.uv) : tex2D(_MainTex, i.uv).rgb;
                float l = dot(c, float3(0.299, 0.587, 0.114));
                c = lerp(float3(l, l, l), c, _Sat);
                c *= lerp(_Shadow.rgb, _Highlight.rgb, saturate(l * 1.8));
                c = saturate((c - _Pivot) * _Contrast + _Pivot);
                float2 d = i.uv - 0.5;
                c *= saturate(1.0 - dot(d, d) * _Vignette);
                float n = frac(sin(dot(i.uv * _ScreenParams.xy + _Seed, float2(12.9898, 78.233))) * 43758.5453);
                c += (n - 0.5) * _Grain;
                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
