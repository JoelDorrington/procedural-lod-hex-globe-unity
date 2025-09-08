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
        _PlanetCenter ("Planet Center (World Position)", Vector) = (0,0,0,0)
    }
    SubShader
    {
    Tags { "RenderType"="Opaque" }
        LOD 200
    Pass
        {
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
            float _FadeDirection; // 1 for child (fade in), -1 for parent (fade out)
            float4 _PlanetCenter; // Planet center in world coordinates

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                
                // Transform normal to world space but normalize afterward for consistency
                float3 worldNormal = mul((float3x3)unity_ObjectToWorld, v.normal);
                o.n = normalize(worldNormal);
                
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // Calculate height relative to planet center, not world origin
                float3 planetToVertex = i.worldPos - _PlanetCenter.xyz;
                float worldR = length(planetToVertex);
                float height = worldR - _SeaLevel;
                
                // Use geometric normal derived from world position for consistent lighting
                // This eliminates transform-based inconsistencies between tiles
                float3 geometricNormal = normalize(planetToVertex);
                
                float mountainT = saturate((height - _MountainStart) / max(0.0001, (_MountainFull - _MountainStart)));
                // Slope factor: use geometric normal for consistency
                float slope = 1 - saturate(abs(geometricNormal.y)); // 0 at flat top/bottom, 1 at vertical cliff
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
                // Snow layer - use distance from planet center
                float snowRange = max(0.0001, _SnowFull - _SnowStart);
                float snowT = saturate((worldR - _SnowStart) / snowRange);
                // Flatter surfaces (higher normal.y) accumulate more snow - use geometric normal
                float flatFactor = saturate(geometricNormal.y);
                snowT = saturate(snowT + flatFactor * _SnowSlopeBoost * (1 - snowT));
                finalCol = lerp(finalCol, _SnowColor, snowT);

                // Opaque: ignore fade, always return full alpha
                finalCol.a = 1.0;
                return finalCol;
            }
            ENDHLSL
        }
    }
}
