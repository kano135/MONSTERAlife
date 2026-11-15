// 땅에 '구멍'을 뚫어 보이게 하는 투명 판. 색은 안 그리고 깊이만 써서, 뒤에 그려지는 땅(평면)이 이 자리에 안 그려진다.
// 우물 안쪽(벽·물)은 이것보다 먼저 그려 두므로 그대로 보인다. Lake(우물)가 쓴다.
Shader "Monstera/DepthHole"
{
    SubShader
    {
        Tags { "Queue" = "Geometry-1" "RenderType" = "Opaque" }
        Pass
        {
            ColorMask 0
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 vert(float4 v : POSITION) : SV_POSITION { return UnityObjectToClipPos(v); }
            fixed4 frag() : SV_Target { return 0; }
            ENDCG
        }
    }
    Fallback Off
}
