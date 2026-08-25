// Unlit line shader that ignores depth, so force arrows stay visible through the objects
// they describe. Vertex colours come through, which is how LineRenderer tints its lines.
Shader "GravityLab/Line Overlay"
{
    Properties
    {
        _BaseColor("Color", Color) = (1, 1, 1, 1)

        // Exposed so the effect can be dialled back to normal depth testing per material.
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 8   // Always
        [Enum(Off, 0, On, 1)] _ZWrite("ZWrite", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Overlay"
            "IgnoreProjector" = "True"
        }
        LOD 100

        Pass
        {
            Name "LineOverlay"
            Tags { "LightMode" = "UniversalForward" }

            // ZTest Always is what puts the line in front of everything already drawn.
            ZTest [_ZTest]
            ZWrite [_ZWrite]
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex LineVertex
            #pragma fragment LineFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _ZTest;
                float _ZWrite;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings LineVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // LineRenderer bakes its start/end colours into vertex colour.
                output.color = input.color;

                return output;
            }

            half4 LineFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return input.color * _BaseColor;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
