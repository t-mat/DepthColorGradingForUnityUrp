// Depth Color Grading Shader (HLSL) for Unity URP
// SPDX-FileCopyrightText: Copyright (c) Takayuki Matsuoka
// SPDX-License-Identifier: MIT

#include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/NormalReconstruction.hlsl"


//
// Textures
//
TEXTURE2D_X(_MainTex);
TEXTURE2D_X(_DepthColorGradingLut0);
TEXTURE2D_X(_DepthColorGradingLut1);

float4 sampleMainTex(float2 uv) { return SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, uv); }
float4 sampleLut0(float2 uv) { return SAMPLE_TEXTURE2D_X(_DepthColorGradingLut0, sampler_LinearClamp, uv); }
float4 sampleLut1(float2 uv) { return SAMPLE_TEXTURE2D_X(_DepthColorGradingLut1, sampler_LinearClamp, uv); }


//
// Parameters
//
float4 _DepthColorGradingParams;
float4 _DepthColorGradingParams2;
float4 _DepthColorGradingParams3;

half  getSkyboxBlend()                      { return _DepthColorGradingParams.x; }  // skybox blend
half  getFinalBlend()                       { return _DepthColorGradingParams.y; }  // final blend
half  getInvHdrScale()                      { return _DepthColorGradingParams.z; }  // 1/hdrScale
half  getHdrScale()                         { return _DepthColorGradingParams.w; }  // hdrScale

half  getOneOverLutHeight()                 { return _DepthColorGradingParams2.x; } // 1 / lutHeight
half  getLutHeightMinus1()                  { return _DepthColorGradingParams2.y; } // lutHeight - 1
float getFalloff()                          { return _DepthColorGradingParams2.z; } // falloff
// _DepthColorGradingParams2.w : unused

half  getOneOverLutWidthHalf()              { return _DepthColorGradingParams3.x; } // (1/lutWidth) * 0.5
half  getOneOverLutHeightHalf()             { return _DepthColorGradingParams3.y; } // (1/lutHeight) * 0.5
half  getLutHeightMinus1xOneOverLutWidth()  { return _DepthColorGradingParams3.z; } // (lutHeight-1) / lutWidth
half  getLutHeightMinus1xOneOverLutHeight() { return _DepthColorGradingParams3.w; } // (lutHeight-1) / lutHeight


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

//  Transform screen uv and raw depth to view coordinate position.
//
//  Usage:
//      float2 screenUv = ...;
//      float  rawDepth = GetRawDepth(screenUv); // Packages/com.unity.render-pipelines.universal/ShaderLibrary/NormalReconstruction.hlsl
//      float3 posVS    = TransformScreenUvAndRawDepthToPosVS(screenUv, rawDepth);
//
//  note(1) : Forward direction of view coordinate indicates Z-.
//            Backward is Z+.
float3 TransformScreenUvAndRawDepthToPosVS(half2 screenUv, float rawDepth) {
    const float linearEyeDepth = TransformRawDepthToLinearEyeDepth(rawDepth);
    const float4 depthToViewParams = getDepthToViewParams();
    return float3(
        linearEyeDepth * ((screenUv.x * depthToViewParams.x) + depthToViewParams.y),
        linearEyeDepth * ((screenUv.y * depthToViewParams.z) + depthToViewParams.w),
        -linearEyeDepth
    );
}

//  Determine rawDepth is background or not.
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
//  LUT functions
//
//  usage:
//      float4 uvut = computeLut2dUvut(color.rgb);
//      float3 color0 = lookupLut0(uvut);
//      float3 color1 = lookupLut1(uvut);
//

// Compute "uvut" vector for color lookup.
half4 computeLut2dUvut(half3 color) {
    const half z        = color.z * getLutHeightMinus1();
    const half shift    = floor(z);
    const half t        = z - shift;
    const half x0       = color.x * getLutHeightMinus1xOneOverLutWidth() + getOneOverLutWidthHalf() + shift * getOneOverLutHeight();
    const half y        = color.y * getLutHeightMinus1xOneOverLutHeight() + getOneOverLutHeightHalf();
    const half x1       = x0 + getOneOverLutHeight();
    return half4(x0, y, x1, t);
}

//  Sample LUT0 as an interleaved 3D LUT texture.
half3 lookupLut0(half4 uvut) {
    const half3 a = sampleLut0(half2(uvut.x, uvut.y)).rgb;  // Sample slice #0 of 2D LUT
    const half3 b = sampleLut0(half2(uvut.z, uvut.y)).rgb;  // Sample slice #1 of 2D LUT
    return lerp(a, b, uvut.w).rgb;                          // Interpolate slices
}

//  Sample LUT1 as an interleaved 3D LUT texture.
half3 lookupLut1(half4 uvut) {
    const half3 a = sampleLut1(half2(uvut.x, uvut.y)).rgb;  // Sample slice #0 of 2D LUT
    const half3 b = sampleLut1(half2(uvut.z, uvut.y)).rgb;  // Sample slice #1 of 2D LUT
    return lerp(a, b, uvut.w).rgb;                          // Interpolate slices
}


//  Depth color grading main function
half3 DepthColorGrading(half2 screenUv, half3 srcColor, float rawDepth) {
    const half4  uvut       = computeLut2dUvut(srcColor.rgb);
    const half3  colorNear  = lookupLut0(uvut);
    const half3  colorFar   = lookupLut1(uvut);
    const float3 posVS      = TransformScreenUvAndRawDepthToPosVS(screenUv, rawDepth);
    const float  distance   = length(posVS);
    const half   intensity  = saturate(pow(2, getFalloff() * distance));

    half t, f;
    if(isBackgroundRawDepth(rawDepth)) {
        t = 0;
        f = getSkyboxBlend();
    } else {
        t = intensity;
        f = getFinalBlend();
    }

    const half3 c0 = lerp(colorFar, colorNear, t);
    const half3 c1 = lerp(srcColor.xyz, c0, f);
    return c1;
}


//  Fragment shader entry point
half4 Frag(Varyings input) : SV_Target {
    const half2  screenUv  = input.uv;
    const float  rawDepth  = GetRawDepth(screenUv);
    const half4  srcColor4 = sampleMainTex(screenUv);
    const half3  lowColor3 = saturate(srcColor4.rgb * getInvHdrScale());
    const half3  grdColor3 = DepthColorGrading(screenUv, lowColor3, rawDepth);
    const half3  o         = grdColor3 * getHdrScale();
    return half4(o.r, o.g, o.b, srcColor4.a);
}
