Shader "Custom/HexOverlaySpike" {
    Properties {
        _MainTex ("Base (RGB)", 2D) = "white" {}
    _CellSize ("Cell Size", Float) = 1.0
        _LineThickness ("Line Thickness", Float) = 0.05
        _OverlayColor ("Overlay Color", Color) = (1,1,1,1)
        _OverlayOpacity ("Overlay Opacity", Range(0,1)) = 0.9
    _OverlayEnabled ("Overlay Enabled", Float) = 1
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _CellSize;
            float _LineThickness;
            fixed4 _OverlayColor;
            float _OverlayOpacity;
            float _OverlayEnabled;

            struct appdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert (appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float4 worldP = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos = worldP.xyz;
                return o;
            }

            // Helper: stable per-cube-face 2D coord from world position
            float2 FaceCoord(float3 wp) {
                float ax = abs(wp.x);
                float ay = abs(wp.y);
                float az = abs(wp.z);
                // Determine dominant axis -> cube face
                if (ax >= ay && ax >= az) {
                    // X face -> use z,y (preserve sign to keep continuity per face)
                    return float2(wp.z, wp.y) / ax; // normalize by axis magnitude for stability
                } else if (ay >= ax && ay >= az) {
                    // Y face -> use x,z
                    return float2(wp.x, wp.z) / ay;
                } else {
                    // Z face -> use x,y
                    return float2(wp.x, wp.y) / az;
                }
            }

            // Simple hex-style mapping (approximate) using shear mapping
            float HexMask(float2 coord, float cellSize, float thickness) {
                // Scale coordinates by cell size
                float2 u = coord / cellSize;
                // Shear transform to map hex lattice to grid
                const float K = 0.86602540378; // cos(30deg)
                const float H = 0.5;           // sin(30deg)
                float2 q;
                q.x = u.x * K;
                q.y = u.y + u.x * H;
                // fractional cell position
                float2 f = frac(q) - 0.5;
                // distance to cell center (circular approx)
                float d = length(f);
                // draw thin lines where distance is near the outer radius
                float edge = smoothstep(thickness + 0.01, thickness - 0.01, d);
                // Invert: edge==1 is inside cell, 0 near edges. We want border -> 1-edge
                return 1.0 - edge;
            }

            fixed4 frag (v2f i) : SV_Target {
                fixed4 baseCol = tex2D(_MainTex, i.uv);

                if (_OverlayEnabled < 0.5) return baseCol;

                float2 fc = FaceCoord(i.worldPos);
                // Multiply fc by a constant to avoid very small numbers around origin
                fc *= 1.0;

                float mask = HexMask(fc, _CellSize, _LineThickness);
                // mask is 1 at border, 0 inside; we'll use it as line alpha
                fixed4 overlay = _OverlayColor;
                overlay.a = _OverlayOpacity * mask;

                // simple alpha blend
                fixed4 outCol = lerp(baseCol, overlay, overlay.a);
                return outCol;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}