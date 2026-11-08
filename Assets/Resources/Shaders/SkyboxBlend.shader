// 360도 하늘 그림(위도-경도 파노라마) 두 장을 섞어 보여 주는 스카이박스. DayNight 가 시간에 맞춰 _Blend 를 바꾼다.
// Resources 폴더에 있어서 빌드에도 들어간다(Shader.Find 로 찾는다).
Shader "Monstera/SkyboxBlend"
{
    Properties
    {
        _TexA ("Sky A", 2D) = "grey" {}
        _TexB ("Sky B", 2D) = "grey" {}
        _Blend ("Blend A→B", Range(0, 1)) = 0
        _Exposure ("Exposure", Range(0, 2)) = 1
        _Rotation ("Rotation", Range(0, 360)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _TexA, _TexB;
            float _Blend, _Exposure, _Rotation;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(float4 v : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v);
                o.dir = v.xyz;
                return o;
            }

            float2 LatLong(float3 d)
            {
                d = normalize(d);
                float u = atan2(d.x, d.z) / (2 * UNITY_PI) + 0.5 + _Rotation / 360.0;
                float v = asin(clamp(d.y, -1, 1)) / UNITY_PI + 0.5;
                return float2(u, v);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 밉맵 0번을 직접 읽어 그림 이음매(경도 0/360도)에 줄이 생기지 않게
                float4 uv = float4(LatLong(i.dir), 0, 0);
                float4 c = lerp(tex2Dlod(_TexA, uv), tex2Dlod(_TexB, uv), _Blend);
                return float4(c.rgb * _Exposure, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
