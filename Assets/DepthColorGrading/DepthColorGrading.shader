// Depth Color Grading Shader for Unity URP
Shader "PostEffect/DepthColorGrading"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            HLSLPROGRAM
            #include "DepthColorGrading.hlsl"
            #pragma vertex FullscreenVert // Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
