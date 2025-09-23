Shader "Custom/TerrainSignalHeatmap"
{
    Properties
    {
        _MainTex    ("Heatmap Texture", 2D) = "white" {}
        _Intensity  ("Intensity", Range(0, 2)) = 1.0
        _Alpha      ("Alpha", Range(0, 1)) = 0.8
        _EdgeFade   ("Edge Fade (UV)", Range(0, 1)) = 0.2
        _ClipMin    ("Alpha Clip Min", Range(0, 0.2)) = 0.01
        _DepthFade  ("Depth Fade", Range(0, 5)) = 1.0
        _NoiseTex   ("Blue Noise (optional)", 2D) = "gray" {}
        _NoiseAmp   ("Noise Amount", Range(0, 0.1)) = 0.02
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent+10"
            "RenderPipeline"="UniversalPipeline"
        }

        // Blending/Depth: draw over terrain, behind buildings (which write depth)
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off
        // Small polygon offset to further reduce z-fighting with ground
        Offset -1, -1

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma shader_feature_local _ HEATMAP_NOISE
            #pragma instancing_options procedural:setup
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float  fogCoord    : TEXCOORD3;
                float4 screenPos   : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _Intensity;
                float  _Alpha;
                float  _EdgeFade;
                float  _ClipMin;
                float  _DepthFade;
                float  _NoiseAmp;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS);
                VertexNormalInputs   n = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = p.positionCS;
                OUT.positionWS  = p.positionWS;
                OUT.normalWS    = n.normalWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.fogCoord    = ComputeFogFactor(OUT.positionHCS.z);
                OUT.screenPos   = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

            // Soft depth fade against scene geometry (needs camera depth texture)
            half DepthFade(float4 screenPos, float fade)
            {
                #if defined(_CameraDepthTexture)
                    float2 uv = screenPos.xy / screenPos.w;
                    float  sceneRaw = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, uv).r;
                    float  sceneEye = LinearEyeDepth(sceneRaw, _ZBufferParams);
                    float  pixEye   = LinearEyeDepth(screenPos.z / screenPos.w, _ZBufferParams);
                    float  diff     = saturate( (sceneEye - pixEye) * fade ); // positive when heatmap is close to intersecting
                    return diff; // 0 near intersection, 1 far
                #else
                    return 1.0h;
                #endif
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // Base color from heatmap texture
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // Early clip for very low alpha / masked pixels (buildings etc.)
                if (c.a <= _ClipMin) discard;

                // Optional subtle noise to reduce banding/shimmer (toggle by setting _NoiseAmp>0 and assigning texture)
                if (_NoiseAmp > 0.0001h)
                {
                    float2 nUV = frac(IN.uv * float2(1024.0, 1024.0) + _Time.y); // tiny scroll
                    half n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, nUV).r;
                    c.rgb += (n - 0.5h) * _NoiseAmp;
                }

                // Edge fade (UV space) so the square blends out
                if (_EdgeFade > 0.0h)
                {
                    half2 d = saturate(min(IN.uv, 1.0h - IN.uv) / _EdgeFade);
                    half edge = d.x * d.y;         // 0 at edges, 1 at center
                    c.a *= edge;
                }

                // Simple lambert-ish lighting to keep some scene coherence
                Light l = GetMainLight();
                half NdotL = saturate(dot(normalize(IN.normalWS), normalize(l.direction)));
                half lightFactor = lerp(0.7h, 1.0h, NdotL * 0.5h + 0.5h);
                c.rgb *= lightFactor;

                // Depth fade where it intersects other geometry (roads/props)
                half df = DepthFade(IN.screenPos, _DepthFade);
                c.a *= df;

                // Intensity and global alpha
                c.rgb *= _Intensity;
                c.a   *= _Alpha;

                // Fog
                c.rgb = MixFog(c.rgb, IN.fogCoord);
                return c;
            }

            // Minimal procedural instancing hook (nothing per-instance right now)
            void setup() {}
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
