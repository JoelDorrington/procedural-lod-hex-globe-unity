Shader "HexGlobe/PlanetTerrain"
{
    Properties {
        _PlanetCenter ("Planet Center (World Position)", Vector) = (0,0,0,0)
        _PlanetRadius ("Planet Radius", Float) = 30
        _SeaLevel ("Sea Level (world height)", Float) = 30
        
        // Simplified tiered colors and thresholds (absolute heights above sea level)
        _WaterColor ("Water Color", Color) = (0.10,0.20,0.60,1)
    _CoastMax ("Coastline Max Height", Float) = 0.1
        _CoastColor ("Coastline Color", Color) = (1.0,1.0,0.0,1)
    _LowlandsMax ("Lowlands Max Height", Float) = 0.3
        _LowlandsColor ("Lowlands Color", Color) = (0.75,0.95,0.55,1)
    _HighlandsMax ("Highlands Max Height", Float) = 0.5
        _HighlandsColor ("Highlands Color", Color) = (0.55,0.70,0.50,1)
    _MountainsMax ("Mountains Max Height", Float) = 0.8
        _MountainsColor ("Mountains Color", Color) = (0.30,0.30,0.30,1)
    _SnowcapsMax ("Snowcaps Max Height", Float) = 0.99
        _SnowcapsColor ("Snowcaps Color", Color) = (1.0,1.0,1.0,1)

        // Overlay (unchanged)
        _OverlayColor ("Overlay Color", Color) = (1,1,1,1)
        _OverlayOpacity ("Overlay Opacity", Range(0,1)) = 0.9
        _OverlayEnabled ("Overlay Enabled", Float) = 1
        _OverlayEdgeThreshold ("Overlay Edge Threshold", Range(0,1)) = 0.15
        _OverlayAAScale ("Overlay AA Scale", Float) = 100
        _UseDualOverlay ("Use Dual Overlay", Float) = 1
        _DualOverlayCube ("Dual Overlay Cubemap", CUBE) = "white" {}
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

            float _SeaLevel;
            // Tier properties
            float4 _WaterColor;
            float _CoastMax; float4 _CoastColor;
            float _LowlandsMax; float4 _LowlandsColor;
            float _HighlandsMax; float4 _HighlandsColor;
            float _MountainsMax; float4 _MountainsColor;
            float _SnowcapsMax; float4 _SnowcapsColor;
            float4 _PlanetCenter; // Planet center in world coordinates
            // Overlay HLSL-exposed properties
            float4 _OverlayColor;
            float _OverlayOpacity;
            float _OverlayEnabled;
            float _OverlayEdgeThreshold;
            float _OverlayAAScale;
            float _UseDualOverlay;
            float _PlanetRadius;
            UNITY_DECLARE_TEXCUBE(_DualOverlayCube);

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
                // Calculate height relative to planet center, then distance above sea level
                float3 planetToVertex = i.worldPos - _PlanetCenter.xyz;
                float worldR = length(planetToVertex);
                float hAboveSea = max(0.0, worldR - _SeaLevel);

                // Determine tier color with linear blend between adjacent tiers
                float4 c0 = _WaterColor;
                float4 c1 = _CoastColor;
                float4 c2 = _LowlandsColor;
                float4 c3 = _HighlandsColor;
                float4 c4 = _MountainsColor;
                float4 c5 = _SnowcapsColor;

                float4 col;
                if (hAboveSea <= 0.0001) {
                    col = c0;
                } else if (hAboveSea <= _CoastMax) {
                    float t = saturate(hAboveSea / max(0.0001, _CoastMax));
                    col = lerp(c0, c1, t);
                } else if (hAboveSea <= _LowlandsMax) {
                    float t = saturate((hAboveSea - _CoastMax) / max(0.0001, _LowlandsMax - _CoastMax));
                    col = lerp(c1, c2, t);
                } else if (hAboveSea <= _HighlandsMax) {
                    float t = saturate((hAboveSea - _LowlandsMax) / max(0.0001, _HighlandsMax - _LowlandsMax));
                    col = lerp(c2, c3, t);
                } else if (hAboveSea <= _MountainsMax) {
                    float t = saturate((hAboveSea - _HighlandsMax) / max(0.0001, _MountainsMax - _HighlandsMax));
                    col = lerp(c3, c4, t);
                } else {
                    float t = saturate((hAboveSea - _MountainsMax) / max(0.0001, _SnowcapsMax - _MountainsMax));
                    col = lerp(c4, c5, t);
                }

                float4 finalCol = col;

                // Dual overlay: sample a pre-rasterized cubemap where R channel = edge strength.
                float4 outCol = finalCol;
                if (_OverlayEnabled > 0.5 && _UseDualOverlay > 0.5)
                {
                    float3 p = normalize(planetToVertex);
                    // Sample cubemap (assumes generator writes edge strength into .r)
                    float edgeSample = UNITY_SAMPLE_TEXCUBE(_DualOverlayCube, p).r;
                    edgeSample = saturate(edgeSample);
                    // Antialias: scale AA with planet radius so large planets keep reasonable edge widths
                    float radiusScale = max(0.0001, _PlanetRadius);
                    float aa = max(0.005 * _OverlayAAScale / radiusScale, 1e-6);
                    // Antialiased mask centered on threshold
                    float edgeMask = smoothstep(_OverlayEdgeThreshold - aa, _OverlayEdgeThreshold + aa, edgeSample);
                    // Scale overlay alpha slightly based on radius to reduce overdraw on tiny planets
                    float alphaScale = saturate(1.0 / sqrt(radiusScale));
                    float edgeAlpha = saturate(_OverlayOpacity * edgeMask * alphaScale);
                    float4 overlayRGB = float4(_OverlayColor.rgb, 1.0);
                    outCol = lerp(outCol, overlayRGB, edgeAlpha);
                }

                // Clamp final color to avoid NaNs/infs that can trigger magenta fallback
                outCol.rgb = saturate(outCol.rgb);
                // Opaque: ensure alpha = 1
                outCol.a = 1.0;
                return outCol;
            }
            ENDHLSL
        }
    }
}
