// Depth Color Grading Shader (HLSL) for Unity URP
// SPDX-FileCopyrightText: Copyright (c) Takayuki Matsuoka
// SPDX-License-Identifier: MIT

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/NormalReconstruction.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"


struct DepthColorGrading_Attributes {
    float4 positionOS : POSITION;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};


struct DepthColorGrading_Varyings {
                  float4 positionCS : SV_POSITION;
    noperspective float2 uv         : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};


//
// Samplers
//
#ifndef UNIVERSAL_POSTPROCESSING_COMMON_INCLUDED
SAMPLER(sampler_LinearClamp);   // Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl
SAMPLER(sampler_PointClamp);    // Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl
#endif


//
// Textures
//
TEXTURE2D_X(_MainTex);
TEXTURE2D_X(_DepthColorGradingLut0);
TEXTURE2D_X(_DepthColorGradingLut1);

real4 sampleMainTex(real2 uv) { return SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, uv); }
real4 sampleLut0(real2 uv) { return SAMPLE_TEXTURE2D_X(_DepthColorGradingLut0, sampler_LinearClamp, uv); }
real4 sampleLut1(real2 uv) { return SAMPLE_TEXTURE2D_X(_DepthColorGradingLut1, sampler_LinearClamp, uv); }


//
// Parameters
//
real4  _DepthColorGradingParams;
float4 _DepthColorGradingParams2;
real4  _DepthColorGradingParams3;

real  getSkyboxBlend()                      { return _DepthColorGradingParams.x; }  // skybox blend
real  getFinalBlend()                       { return _DepthColorGradingParams.y; }  // final blend
real  getInvHdrScale()                      { return _DepthColorGradingParams.z; }  // 1/hdrScale
real  getHdrScale()                         { return _DepthColorGradingParams.w; }  // hdrScale

real  getOneOverLutHeight()                 { return _DepthColorGradingParams2.x; } // 1 / lutHeight
real  getLutHeightMinus1()                  { return _DepthColorGradingParams2.y; } // lutHeight - 1
float getFalloff()                          { return _DepthColorGradingParams2.z; } // falloff
// _DepthColorGradingParams2.w : unused

real  getOneOverLutWidthHalf()              { return _DepthColorGradingParams3.x; } // (1/lutWidth) * 0.5
real  getOneOverLutHeightHalf()             { return _DepthColorGradingParams3.y; } // (1/lutHeight) * 0.5
real  getLutHeightMinus1xOneOverLutWidth()  { return _DepthColorGradingParams3.z; } // (lutHeight-1) / lutWidth
real  getLutHeightMinus1xOneOverLutHeight() { return _DepthColorGradingParams3.w; } // (lutHeight-1) / lutHeight


// _DepthColorGradingDepthToViewParams:
//      Precomputed parameters for TransformScreenUvAndRawDepthToPosVS().
//      It can be computed in Unity C#, for example:
//
//          Matrix4x4 c = camera.projectionMatrix;
//          float x = 2f / c[0,0];
//          float y = (-1f - c[0,2]) / c[0,0];
//          float z = 2f / c[1,1];
//          float w = (-1f - c[2,0]) / c[1,1];
//          myMaterial.SetVector("_DepthColorGradingDepthToViewParams", new Vector4(x, y, z, w));
//
float4 _DepthColorGradingDepthToViewParams;
float4 getDepthToViewParams()   { return _DepthColorGradingDepthToViewParams; }


//
// Utility functions
//

// Transform raw depth to linear eye depth
float TransformRawDepthToLinearEyeDepth(float rawDepth) {
    return LinearEyeDepth(rawDepth, _ZBufferParams); // Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl
}

// Transform screen uv and raw depth to view coordinate position.
//
//  Usage:
//      float2 screenUv = ...;
//      float  rawDepth = GetRawDepth(screenUv); // Packages/com.unity.render-pipelines.universal/ShaderLibrary/NormalReconstruction.hlsl
//      float3 posVS    = TransformScreenUvAndRawDepthToPosVS(screenUv, rawDepth);
//
//  note(1) : Forward direction of view coordinate indicates Z-.
//            Backward is Z+.
float3 TransformScreenUvAndRawDepthToPosVS(real2 screenUv, float rawDepth) {
    const float linearEyeDepth = TransformRawDepthToLinearEyeDepth(rawDepth);
    const float4 depthToViewParams = getDepthToViewParams();
    return float3(
        linearEyeDepth * ((screenUv.x * depthToViewParams.x) + depthToViewParams.y),
        linearEyeDepth * ((screenUv.y * depthToViewParams.z) + depthToViewParams.w),
        -linearEyeDepth
    );
}

// Determine rawDepth is background or not.
bool isBackgroundRawDepth(float rawDepth) {
#if SHADER_API_GLCORE || SHADER_API_GLES || SHADER_API_GLES3
    if (rawDepth >= 1.0f) {
        return true;    // background
    }
#else
    // DirectX, Vulkan, Metal, Switch
    if (rawDepth <= 0.0f) {
        return true;    // background
    }
#endif
    return false;       // foreground
}


//
// LUT functions
//
//  Usage:
//      float4 uvut = computeLut2dUvut(color.rgb);
//      float3 color0 = lookupLut0(uvut);
//      float3 color1 = lookupLut1(uvut);
//

// Compute "uvut" vector for color lookup.
real4 computeLut2dUvut(real3 color) {
    const real z        = color.z * getLutHeightMinus1();
    const real shift    = floor(z);
    const real t        = z - shift;
    const real x0       = color.x * getLutHeightMinus1xOneOverLutWidth() + getOneOverLutWidthHalf() + shift * getOneOverLutHeight();
    const real y        = color.y * getLutHeightMinus1xOneOverLutHeight() + getOneOverLutHeightHalf();
    const real x1       = x0 + getOneOverLutHeight();
    return real4(x0, y, x1, t);
}

// Sample LUT0 as an interleaved 3D LUT texture.
real3 lookupLut0(real4 uvut) {
    const real3 a = sampleLut0(real2(uvut.x, uvut.y)).rgb;  // Sample slice #0 of 2D LUT
    const real3 b = sampleLut0(real2(uvut.z, uvut.y)).rgb;  // Sample slice #1 of 2D LUT
    return lerp(a, b, uvut.w).rgb;                          // Interpolate slices
}

// Sample LUT1 as an interleaved 3D LUT texture.
real3 lookupLut1(real4 uvut) {
    const real3 a = sampleLut1(real2(uvut.x, uvut.y)).rgb;  // Sample slice #0 of 2D LUT
    const real3 b = sampleLut1(real2(uvut.z, uvut.y)).rgb;  // Sample slice #1 of 2D LUT
    return lerp(a, b, uvut.w).rgb;                          // Interpolate slices
}


//
// Depth color grading functions
//

// Depth color grading main function
real3 DepthColorGrading(real2 screenUv, real3 srcColor, float rawDepth) {
    const real4  uvut       = computeLut2dUvut(srcColor.rgb);
    const real3  colorNear  = lookupLut0(uvut);
    const real3  colorFar   = lookupLut1(uvut);
    const float3 posVS      = TransformScreenUvAndRawDepthToPosVS(screenUv, rawDepth);
    const float  distance   = length(posVS);
    const real   intensity  = saturate(pow(2, getFalloff() * distance));

    real t, f;
    if(isBackgroundRawDepth(rawDepth)) {
        t = 0;
        f = getSkyboxBlend();
    } else {
        t = intensity;
        f = getFinalBlend();
    }

    const real3 c0 = lerp(colorFar, colorNear, t);
    const real3 c1 = lerp(srcColor.xyz, c0, f);
    return c1;
}


// Fragment shader entry point
real4 DepthColorGrading_Frag(DepthColorGrading_Varyings input) : SV_Target {
    const real2  screenUv  = input.uv;
    const float  rawDepth  = GetRawDepth(screenUv);
    const real4  srcColor4 = sampleMainTex(screenUv);
    const real3  lowColor3 = saturate(srcColor4.rgb * getInvHdrScale());
    const real3  grdColor3 = DepthColorGrading(screenUv, lowColor3, rawDepth);
    const real3  o         = grdColor3 * getHdrScale();
    return real4(o.r, o.g, o.b, srcColor4.a);
}


//  Vertex shader
DepthColorGrading_Varyings DepthColorGrading_FullscreenVert(DepthColorGrading_Attributes input) {
    DepthColorGrading_Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    const float3 posOS = input.positionOS.xyz;
    const float4 posCS = mul(UNITY_MATRIX_VP, float4(posOS, 1.0));
    output.positionCS = posCS;
    output.uv = input.uv;

    return output;
}
