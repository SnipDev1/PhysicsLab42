Shader "Custom/Parallax"
{
    Properties
    {
        // General
        [MainColor] _Color("Global Tint Color", Color) = (1, 1, 1, 1)
        [MainTexture] _MainTex("MainTex Atlas (3x3)", 2D) = "white" {}
        [Toggle] _DecodeRGBM("DecodeRGBM", Int) = 0
        [Toggle] _GammaToLinear("GammaToLinear", Int) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Culling", Float) = 2
        [Enum(Off, 0, On, 1)] _ZWrite("ZWrite", Float) = 1

        // Window Position
        _WindowDepth ("Window Distance Z", Float) = 0.0

        // Front Layer (Frame / Window)
        _FrontAlpha ("Front Opacity", Range(0, 1)) = 1.0
        _FrontColor ("Front Tint", Color) = (1, 1, 1, 1)
        _FrontDist ("Front Z Distance", Float) = 0.0
        _FrontOffset ("Front Offset (X, Y)", Vector) = (0, 0, 0, 0)
        _FrontScale ("Front Scale", Float) = 1.0
        _FrontSubTex ("Front Atlas Cell (0..8)", Float) = 0

        // Rough Transparent Plastic Layer (behind Front, before Interior)
        _PlasticAlpha ("Plastic Opacity", Range(0, 1)) = 0.25
        _PlasticColor ("Plastic Tint", Color) = (0.92, 0.95, 1.0, 1.0)
        _PlasticRoughness ("Plastic Roughness", Range(0, 1)) = 0.45
        _PlasticBlur ("Plastic Blur (Frosted)", Range(0, 1)) = 0.35
        _PlasticGrain ("Plastic Surface Grain", Range(0, 1)) = 0.4
        _PlasticGrainScale ("Plastic Grain Scale", Float) = 60.0
        _PlasticDist ("Plastic Z Distance", Float) = 0.05
        _PlasticSpecular ("Plastic Specular", Range(0, 1)) = 0.6
        _PlasticFresnel ("Plastic Fresnel", Range(0, 1)) = 0.5

        // Center Layer (Mid 1 / Main Object)
        _Mid1Alpha ("Center Opacity", Range(0, 1)) = 1.0
        _Mid1Color ("Center Tint", Color) = (1, 1, 1, 1)
        _Mid1Dist ("Center Z Distance", Float) = 0.42
        _Mid1Offset ("Center Offset (X, Y)", Vector) = (0, 0, 0, 0)
        _Mid1Scale ("Center Scale", Float) = 1.0
        _Mid1SubTex ("Center Atlas Cell (0..8)", Float) = 6
        _CenterOffset ("Legacy Center Offset", Float) = 0.0

        // Middle Layer 2
        _Mid2Alpha ("Mid 2 Opacity", Range(0, 1)) = 0.0
        _Mid2Color ("Mid 2 Tint", Color) = (1, 1, 1, 1)
        _Mid2Dist ("Mid 2 Z Distance", Float) = 0.25
        _Mid2Offset ("Mid 2 Offset (X, Y)", Vector) = (0, 0, 0, 0)
        _Mid2Scale ("Mid 2 Scale", Float) = 1.0
        _Mid2SubTex ("Mid 2 Atlas Cell (0..8)", Float) = 2

        // Middle Layer 3
        _Mid3Alpha ("Mid 3 Opacity", Range(0, 1)) = 0.0
        _Mid3Color ("Mid 3 Tint", Color) = (1, 1, 1, 1)
        _Mid3Dist ("Mid 3 Z Distance", Float) = 0.60
        _Mid3Offset ("Mid 3 Offset (X, Y)", Vector) = (0, 0, 0, 0)
        _Mid3Scale ("Mid 3 Scale", Float) = 1.0
        _Mid3SubTex ("Mid 3 Atlas Cell (0..8)", Float) = 8

        // Room and Walls Layer (Back)
        _RoomAlpha ("Room Opacity", Range(0, 1)) = 1.0
        _RoomColor ("Room Tint", Color) = (1, 1, 1, 1)
        _RoomDepth ("Room Depth", Float) = 1.0
        _RoomOffset ("Room Offset (X, Y, Z)", Vector) = (0, 0, 0, 0)
        _RoomScale ("Room Scale (X, Y)", Vector) = (1, 1, 0, 0)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry"}
        Cull [_Cull]
        ZWrite [_ZWrite]

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "TextureLib.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float4 _MainTex_ST;
            float _WindowDepth;

            float _FrontAlpha;
            half4 _FrontColor;
            float _FrontDist;
            float4 _FrontOffset;
            float _FrontScale;
            float _FrontSubTex;

            float _PlasticAlpha;
            half4 _PlasticColor;
            float _PlasticRoughness;
            float _PlasticBlur;
            float _PlasticGrain;
            float _PlasticGrainScale;
            float _PlasticDist;
            float _PlasticSpecular;
            float _PlasticFresnel;

            float _Mid1Alpha;
            half4 _Mid1Color;
            float _Mid1Dist;
            float4 _Mid1Offset;
            float _Mid1Scale;
            float _Mid1SubTex;
            float _CenterOffset;

            float _Mid2Alpha;
            half4 _Mid2Color;
            float _Mid2Dist;
            float4 _Mid2Offset;
            float _Mid2Scale;
            float _Mid2SubTex;

            float _Mid3Alpha;
            half4 _Mid3Color;
            float _Mid3Dist;
            float4 _Mid3Offset;
            float _Mid3Scale;
            float _Mid3SubTex;

            float _RoomAlpha;
            half4 _RoomColor;
            float _RoomDepth;
            float4 _RoomOffset;
            float4 _RoomScale;

            int _DecodeRGBM;
            int _GammaToLinear;
            float _Cull;
            float _ZWrite;
        CBUFFER_END

        struct appdata
        {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct v2f
        {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 rayDir : TEXCOORD2;
            float3 rayOrigin : TEXCOORD1;
        };

        float4 face_color(float3 ro, float3 rd, float len, float3 boxPos, float3 boxHalfSize, float blur)
        {
            float4 color = float4(0, 0, 0, 1);
            float3 pos = rd * len + ro;
            float3 p = pos - boxPos;
            float3 normP = float3(
                p.x / max(0.001, boxHalfSize.x * 2.0),
                p.y / max(0.001, boxHalfSize.y * 2.0),
                p.z / max(0.001, boxHalfSize.z * 2.0)
            );

            float3 nor;
            float id;
            float2 uv;
            get_wall_info(normP, nor, id, uv);

            float2 clamped_uv = clamp(uv, 0.001, 0.999);
            float2 uuvv = sub_texture_uv(clamped_uv, 3.0, 3.0, id).xy;

            float blurMult = 1.0 + blur * 8.0;
            float2 ddx_uv = ddx(uv) * (1.0 / 3.0) * blurMult;
            float2 ddy_uv = ddy(uv) * (1.0 / 3.0) * blurMult;
            color = SAMPLE_TEXTURE2D_GRAD(_MainTex, sampler_MainTex, uuvv, ddx_uv, ddy_uv);

            if (_DecodeRGBM > 0)
            {
                color.rgb = DecodeRGBM(color);
            }
            if (_GammaToLinear > 0)
            {
                color.rgb = Gamma20ToLinear(color.rgb);
            }
            return color;
        }

        float4 plane_layer(float3 ro, float3 rd, float planeZ, float2 offset, float scale, int subTexId, float maxDist, float alpha, half4 tint, float blur)
        {
            if (alpha <= 0.001)
                return float4(0, 0, 0, 0);

            if (abs(rd.z) < 1e-6)
                return float4(0, 0, 0, 0);

            float d = (planeZ - ro.z) / rd.z;
            if (d <= 0.0 || d >= maxDist)
                return float4(0, 0, 0, 0);

            float3 pos = rd * d + ro;
            float s = max(0.01, scale);
            float2 puv = (pos.xy - offset) / s + float2(0.5, 0.5);

            if (puv.x > 0.001 && puv.x < 0.999 && puv.y > 0.001 && puv.y < 0.999)
            {
                float2 uuvv = sub_texture_uv(puv, 3.0, 3.0, subTexId).xy;
                float blurMult = 1.0 + blur * 8.0;
                float2 ddx_puv = ddx(puv) * (1.0 / 3.0) * blurMult;
                float2 ddy_puv = ddy(puv) * (1.0 / 3.0) * blurMult;
                float4 col = SAMPLE_TEXTURE2D_GRAD(_MainTex, sampler_MainTex, uuvv, ddx_puv, ddy_puv);

                if (_DecodeRGBM > 0)
                    col.rgb = DecodeRGBM(col);
                if (_GammaToLinear > 0)
                    col.rgb = Gamma20ToLinear(col.rgb);

                col.rgb *= tint.rgb;
                col.a *= tint.a * alpha;
                return col;
            }
            return float4(0, 0, 0, 0);
        }

        float plastic_hash(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        float plastic_noise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = plastic_hash(i);
            float b = plastic_hash(i + float2(1.0, 0.0));
            float c = plastic_hash(i + float2(0.0, 1.0));
            float d = plastic_hash(i + float2(1.0, 1.0));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        float plastic_fbm(float2 p)
        {
            float n = plastic_noise(p);
            n += 0.5 * plastic_noise(p * 2.13);
            n += 0.25 * plastic_noise(p * 4.37);
            return n * 0.5714;
        }

        v2f vert (appdata v)
        {
            v2f o;
            o.uv = v.uv;

            // Retain actual mesh vertices in object space so Cube faces are not collapsed to z=0
            float3 posOS = v.vertex.xyz;

            // Full ObjectToWorld transformation respecting object rotation, scale, and translation
            float3 worldPos = TransformObjectToWorld(posOS);
            o.pos = TransformWorldToHClip(worldPos);

            // Transform camera to object space using inverse model matrix
            float3 worldCamPos = GetCameraPositionWS();
            float3 objCamPos = mul(GetWorldToObjectMatrix(), float4(worldCamPos, 1.0)).xyz;

            // Ray origin and direction in object space
            o.rayDir = posOS - objCamPos;
            o.rayOrigin = objCamPos;

            return o;
        }

        half4 frag (v2f i) : SV_Target
        {
            float3 ro = i.rayOrigin;
            float3 rd = normalize(i.rayDir);

            // If ray points away from the interior (+Z), cull
            if (rd.z <= 1e-5)
            {
                return half4(1, 0, 0, 1);
            }

            float zFront = _WindowDepth;
            float zDepth = max(0.01, _RoomDepth);
            float roomSx = (_RoomScale.x > 0.001) ? _RoomScale.x : 1.0;
            float roomSy = (_RoomScale.y > 0.001) ? _RoomScale.y : 1.0;

            float3 boxCenter = float3(_RoomOffset.x, _RoomOffset.y, zFront + 0.5 * zDepth + _RoomOffset.z);
            float3 boxHalfSize = float3(0.5 * roomSx, 0.5 * roomSy, 0.5 * zDepth);
            box roomBox = box_init(boxCenter, boxHalfSize);
            float3 rayHit = box_intersect(ro, rd, roomBox);

            if (rayHit.x > rayHit.y || rayHit.y <= 0.0)
            {
                return half4(0, 1, 0, 1);
            }

            // Plastic layer plane setup (positioned behind Front layer)
            float frontZ = zFront + _FrontDist;
            float plasticZ = frontZ + max(0.001, _PlasticDist);
            float tPlastic = (plasticZ - ro.z) / rd.z;

            float4 plasticCol = float4(0, 0, 0, 0);
            float3 interior_ro = ro;
            float3 interior_rd = rd;
            float blurAmt = 0.0;

            if (_PlasticAlpha > 0.001 && tPlastic > 0.0 && tPlastic < rayHit.y)
            {
                float3 plasticPos = ro + rd * tPlastic;
                float2 grainUV = plasticPos.xy * _PlasticGrainScale;
                float grain = plastic_fbm(grainUV);

                float2 eps = float2(0.02, 0.0);
                float2 gradGrain = float2(
                    plastic_fbm(grainUV + eps.xy) - plastic_fbm(grainUV - eps.xy),
                    plastic_fbm(grainUV + eps.yx) - plastic_fbm(grainUV - eps.yx)
                ) * _PlasticGrain;

                // Microfacet normal perturbed by roughness and surface grain
                float3 plasticN = normalize(float3(-gradGrain * (_PlasticRoughness * 1.5), -1.0));
                float3 V = -rd;
                float NdotV = saturate(dot(plasticN, V));
                float fresnel = pow(1.0 - NdotV, 3.0) * _PlasticFresnel;

                // Main directional light specular reflection
                Light mainLight = GetMainLight();
                float3 lightDirOS = normalize(mul((float3x3)GetWorldToObjectMatrix(), mainLight.direction));
                float3 H = normalize(V + lightDirOS);
                float NdotH = saturate(dot(plasticN, H));
                float specExp = lerp(64.0, 4.0, _PlasticRoughness);
                float spec = pow(NdotH, specExp) * _PlasticSpecular * (1.0 - _PlasticRoughness * 0.4);

                // Plastic surface color & opacity
                float3 surfRGB = _PlasticColor.rgb * (1.0 + (grain - 0.5) * _PlasticRoughness * 0.25);
                surfRGB += mainLight.color * spec + fresnel * 0.5;
                float surfA = saturate(_PlasticAlpha * (1.0 + fresnel * 0.5));
                plasticCol = float4(surfRGB, surfA);

                // Scattering / frosted refraction for rays entering behind the plastic
                float scatter = _PlasticRoughness * 0.08 + _PlasticBlur * 0.06;
                interior_rd = normalize(rd + float3(gradGrain * scatter, 0.0));
                interior_ro = plasticPos;
                blurAmt = _PlasticBlur * _PlasticAlpha;
            }

            // Intersect interior ray with room boundaries
            float3 interiorHit = box_intersect(interior_ro, interior_rd, roomBox);
            float hitDist = (interiorHit.x <= interiorHit.y && interiorHit.y > 0.0) ? interiorHit.y : rayHit.y;

            // 1. Back / Room walls layer
            float4 roomCol = face_color(interior_ro, interior_rd, hitDist, boxCenter, boxHalfSize, blurAmt);
            roomCol.rgb *= _RoomColor.rgb;
            float roomA = saturate(_RoomAlpha * _RoomColor.a);
            float4 col = float4(roomCol.rgb * roomA, roomA);

            // 2. Interior layers (Center/Mid1, Mid2, Mid3) - all behind the plastic
            struct LayerConfig
            {
                float z;
                float2 offset;
                float scale;
                int subTexId;
                float alpha;
                half4 tint;
            };

            LayerConfig layers[3];

            // Center (Mid 1)
            float mid1Dist = (_Mid1Dist > 0.001) ? _Mid1Dist : (0.5 * zDepth + _CenterOffset);
            layers[0].z = zFront + mid1Dist;
            layers[0].offset = _Mid1Offset.xy;
            layers[0].scale = _Mid1Scale;
            layers[0].subTexId = (int)clamp(round(_Mid1SubTex), 0.0, 8.0);
            layers[0].alpha = _Mid1Alpha;
            layers[0].tint = _Mid1Color;

            // Mid 2
            layers[1].z = zFront + _Mid2Dist;
            layers[1].offset = _Mid2Offset.xy;
            layers[1].scale = _Mid2Scale;
            layers[1].subTexId = (int)clamp(round(_Mid2SubTex), 0.0, 8.0);
            layers[1].alpha = _Mid2Alpha;
            layers[1].tint = _Mid2Color;

            // Mid 3
            layers[2].z = zFront + _Mid3Dist;
            layers[2].offset = _Mid3Offset.xy;
            layers[2].scale = _Mid3Scale;
            layers[2].subTexId = (int)clamp(round(_Mid3SubTex), 0.0, 8.0);
            layers[2].alpha = _Mid3Alpha;
            layers[2].tint = _Mid3Color;

            // Sort 3 interior layers descending by Z (deepest layer first, closest last)
            if (layers[0].z < layers[1].z) { LayerConfig tmp = layers[0]; layers[0] = layers[1]; layers[1] = tmp; }
            if (layers[1].z < layers[2].z) { LayerConfig tmp = layers[1]; layers[1] = layers[2]; layers[2] = tmp; }
            if (layers[0].z < layers[1].z) { LayerConfig tmp = layers[0]; layers[0] = layers[1]; layers[1] = tmp; }

            // Blend interior layers back-to-front
            [unroll]
            for (int k = 0; k < 3; k++)
            {
                if (layers[k].alpha > 0.001)
                {
                    float4 layerCol = plane_layer(interior_ro, interior_rd, layers[k].z, layers[k].offset, layers[k].scale, layers[k].subTexId, hitDist, layers[k].alpha, layers[k].tint, blurAmt);
                    if (layerCol.a > 0.001)
                    {
                        col.rgb = lerp(col.rgb, layerCol.rgb, layerCol.a);
                        col.a = layerCol.a + col.a * (1.0 - layerCol.a);
                    }
                }
            }

            // 3. Blend Plastic layer over the interior
            if (plasticCol.a > 0.001)
            {
                col.rgb = lerp(col.rgb, plasticCol.rgb, plasticCol.a);
                col.a = plasticCol.a + col.a * (1.0 - plasticCol.a);
            }

            // 4. Front Layer (Frame / Dial / Window glass) - rendered IN FRONT of plastic, sharp
            if (_FrontAlpha > 0.001)
            {
                int frontSub = (int)clamp(round(_FrontSubTex), 0.0, 8.0);
                float4 frontCol = plane_layer(ro, rd, frontZ, _FrontOffset.xy, _FrontScale, frontSub, rayHit.y, _FrontAlpha, _FrontColor, 0.0);
                if (frontCol.a > 0.001)
                {
                    col.rgb = lerp(col.rgb, frontCol.rgb, frontCol.a);
                    col.a = frontCol.a + col.a * (1.0 - frontCol.a);
                }
            }

            col *= _Color;
            return col;
        }
        
        ENDHLSL

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}
