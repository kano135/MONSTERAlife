// 언덕용 땅 셰이더: 평평한 땅(URP Unlit)과 똑같은 그림·색에, 비탈만 해 쪽은 밝게·반대쪽은 어둡게 칠한다.
// 평평한 곳은 밝기가 정확히 1이라 땅과 언덕의 이음매가 안 보인다. Landscape 가 쓴다.
// _Src/_Dst/_ZW 로 불투명(황무지 층)·반투명(풀밭 층) 둘 다 쓴다.
// 가파른 면(단의 턱·절벽)은 _RockColor(바위)로 칠한다: 불규칙한 돌덩이가 맞물린 무늬(덩이마다 밝기가 조금씩 다르고 사이에 틈).
// 풀밭 층은 _RockColor.a = 0 이라 바위 면에서 사라진다. 땅 높이(0)보다 낮은 곳은 _SandColor(모래)로 칠한다.
Shader "Monstera/GroundShaded"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        [Enum(UnityEngine.Rendering.BlendMode)] _Src ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _Dst ("Dst Blend", Float) = 0
        _ZW ("ZWrite", Float) = 1
        _RockColor ("Rock (steep faces)", Color) = (0.72, 0.62, 0.8, 1)
        _SandColor ("Sand (below ground level)", Color) = (0.96, 0.88, 0.7, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            Blend [_Src] [_Dst]
            ZWrite [_ZW]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _RockColor;
            fixed4 _SandColor;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float shade : TEXCOORD1;
                float rock : TEXCOORD3;
                float3 wpos : TEXCOORD4;
                float3 wn : TEXCOORD5;
                UNITY_FOG_COORDS(2)
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                float3 n = UnityObjectToWorldNormal(v.normal);
                float3 L = normalize(float3(-0.45, 0.75, -0.5));
                o.shade = clamp(1 + (dot(n, L) - L.y) * 0.9, 0.6, 1.2);
                o.rock = saturate((0.93 - n.y) * 5);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.wn = n;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * _Color;
                // 불규칙한 돌 무늬(보로노이): 크기·모양이 제각각인 돌덩이가 맞물려 있고 사이에 어두운 틈
                float3 an = abs(i.wn);
                float along = an.x > an.z ? i.wpos.z : i.wpos.x; // 벽을 따라가는 가로 좌표
                float2 q = float2(along / 2.6, i.wpos.y / 1.9);
                q += 0.35 * float2(sin(q.y * 1.7 + 1.3), sin(q.x * 1.3 + 0.7)); // 줄이 반듯하지 않게 휜다
                float2 cell = floor(q), fr = frac(q);
                float d1 = 8, d2 = 8; float2 id = 0;
                for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        float2 o = float2(ox, oy);
                        float2 pt = o + float2(hash(cell + o), hash(cell + o + 17.3)) - fr;
                        float dd = dot(pt, pt);
                        if (dd < d1) { d2 = d1; d1 = dd; id = cell + o; }
                        else if (dd < d2) d2 = dd;
                    }
                float tone = 0.84 + 0.24 * hash(id + 3.1);
                float crack = smoothstep(0.02, 0.12, sqrt(d2) - sqrt(d1));
                fixed4 r = _RockColor;
                // 시간대 색은 밝기 위주로(땅의 초록빛이 바위에 묻지 않게)
                float lum = dot(_Color.rgb, float3(0.3, 0.5, 0.2));
                r.rgb *= lerp(float3(lum, lum, lum), _Color.rgb, 0.3) * tone * lerp(0.7, 1.0, crack);
                // 땅보다 낮은 곳(바닷가·강바닥)은 모래. 풀밭 층은 _RockColor.a = 0 이라 모래에서도 사라진다.
                fixed4 sd = fixed4(_SandColor.rgb * lerp(float3(lum, lum, lum), _Color.rgb, 0.3), _RockColor.a);
                c = lerp(c, sd, saturate((-i.wpos.y - 0.03) * 5));
                c = lerp(c, r, i.rock);
                c.rgb *= i.shade;
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
