Shader "HexGlobe/PlanetTerrain"
{
    Properties {
        _ColorLow ("Low Color", Color) = (0.1,0.2,0.6,1)
        _ColorHigh ("High Color", Color) = (0.15,0.35,0.15,1)
        _ColorMountain ("Mountain Color", Color) = (0.5,0.5,0.5,1)
        _Color ("Color", Color) = (1,1,1,1)
        _FadeProgress ("Fade Progress", Float) = 1.0
        _SeaLevel ("Sea Level (world height)", Float) = 30
        _MountainStart ("Mountain Start Height Offset", Float) = 4
        _MountainFull ("Mountain Full Height Offset", Float) = 10
        _SlopeBoost ("Slope Influence", Range(0,1)) = 0.4
        _SnowStart ("Snow Start World Height", Float) = 42
        _SnowFull ("Snow Full World Height", Float) = 48
        _SnowSlopeBoost ("Snow Slope Influence", Range(0,1)) = 0.5
        _SnowColor ("Snow Color", Color) = (0.9,0.9,0.95,1)
        _ShallowBand ("Shallow Water Band Height", Float) = 2
        _ShallowColor ("Shallow Water Color", Color) = (0.12,0.25,0.55,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" }
        LOD 200
    Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 n : NORMAL;
                float3 worldPos : TEXCOORD1;
                float2 uv : TEXCOORD0;
            };

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float _FadeProgress;

            float4 _ColorLow;
            float4 _ColorHigh;
            float4 _ColorMountain;
            float4 _Color;
            float _SeaLevel;
            float _MountainStart;
            float _MountainFull;
            float _SlopeBoost;
            float _SnowStart; float _SnowFull; float _SnowSlopeBoost; float4 _SnowColor;
            float _ShallowBand; float4 _ShallowColor;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.n = UnityObjectToWorldNormal(v.normal);
                o.uv = v.uv;
                return o;
            }

            // Simple hash for dithering fade (avoid per-pixel alpha blending cost)
            float Hash21(float2 p)
            {
                // from Inigo Quilez style hashing
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float4 frag(v2f i) : SV_Target
            {
                // Approximate height: distance from world origin minus sea level
                float worldR = length(i.worldPos);
                float height = worldR - _SeaLevel;
                float mountainT = saturate((height - _MountainStart) / max(0.0001, (_MountainFull - _MountainStart)));
                // Slope factor: steeper (normal more horizontal) -> higher slopeFactor
                float slope = 1 - saturate(abs(i.n.y)); // 0 at flat top/bottom, 1 at vertical cliff
                float slopeInfluence = slope * _SlopeBoost;
                mountainT = saturate(mountainT + slopeInfluence);
                // Base biome gradient between low/high using normalized height within sea->mountain range
                float baseT = saturate(height / max(0.0001, _MountainStart));
                float4 baseCol = lerp(_ColorLow, _ColorHigh, baseT);
                // Shallow water tint: blend near/below sea level
                float shallowT = 0.0;
                if (_ShallowBand > 0.0)
                {
                    // Negative heights (below sea) full shallow, then fade out over band above sea
                    float below = saturate((-height) / max(0.0001, _ShallowBand)); // below sea level
                    float above = saturate(1.0 - (height / _ShallowBand)); // within band above
                    shallowT = max(below, above) * 0.85; // slight cap for subtlety
                }
                baseCol = lerp(baseCol, _ShallowColor, shallowT);
                float4 finalCol = lerp(baseCol, _ColorMountain, mountainT);
                // Snow layer
                float snowRange = max(0.0001, _SnowFull - _SnowStart);
                float snowT = saturate((worldR - _SnowStart) / snowRange);
                // Flatter surfaces (higher normal.y) accumulate more snow
                float flatFactor = saturate(i.n.y);
                snowT = saturate(snowT + flatFactor * _SnowSlopeBoost * (1 - snowT));
                finalCol = lerp(finalCol, _SnowColor, snowT);

                // Output biome color (height banded) with fade mask only on alpha
                float fade = _FadeProgress;
                finalCol.a *= fade;
                return finalCol;
            }
            ENDHLSL
        }
    }
}
