// Translucent unlit shader for the projected hypercube. Depth writing is off and the blend
// is additive-over, so the eight cells stack up without needing to be sorted: overlapping
// faces simply brighten each other, which reads as depth rather than as a sorting error.
Shader "GravityLab/Hypercube Translucent"
{
    Properties
    {
        _BaseColor("Tint", Color) = (1, 1, 1, 1)
        _Opacity("Opacity", Range(0.0, 1.0)) = 0.25

        [Space(10)]
        _RimPower("Edge-on Boost Power", Range(0.5, 8.0)) = 2.0
        _RimStrength("Edge-on Boost", Range(0.0, 3.0)) = 1.0

        [Space(10)]
        [Enum(Off, 0, On, 1)] _ZWrite("ZWrite", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0   // Off: see back faces
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }
        LOD 100

        Pass
        {
            Name "HypercubeTranslucent"
            Tags { "LightMode" = "UniversalForward" }

            // Src alpha over one: the destination is never darkened, so draw order does not
            // matter and nothing pops as the shape turns.
            Blend SrcAlpha One
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex HypercubeVertex
            #pragma fragment HypercubeFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half  _Opacity;
                half  _RimPower;
                half  _RimStrength;
                float _ZWrite;
                float _Cull;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings HypercubeVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;

                // Cell colours arrive as vertex colour, the same as on the slice mesh.
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);

                return output;
            }

            half4 HypercubeFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 colour = input.color.rgb * _BaseColor.rgb;
                half alpha = _Opacity * input.color.a * _BaseColor.a;

                // A face seen edge-on covers little screen area but is where the structure
                // reads, so lift it. Without this the silhouette of each cell disappears.
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half facing = saturate(abs(dot(normalize(input.normalWS), viewDirWS)));
                half edgeOn = pow(1.0h - facing, _RimPower);

                colour += colour * edgeOn * _RimStrength;
                alpha = saturate(alpha + edgeOn * _RimStrength * _Opacity);

                // Additive blending needs the colour premultiplied, since the blend factor
                // on the source is alpha but the destination is left at one.
                colour *= alpha;
                colour = MixFog(colour, input.fogFactor);

                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
