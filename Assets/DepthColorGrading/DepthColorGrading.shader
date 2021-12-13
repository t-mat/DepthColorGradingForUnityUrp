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
            #pragma vertex   DepthColorGrading_FullscreenVert
            #pragma fragment DepthColorGrading_Frag
            ENDHLSL
        }
    }
}
