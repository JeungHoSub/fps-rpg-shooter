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
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Sat;
            float _Contrast;
            float _Pivot;
            float _Vignette;
            float _Grain;
            float _Seed;
            float4 _Shadow;
            float4 _Highlight;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;
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
