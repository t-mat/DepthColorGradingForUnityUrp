// Depth Color Grading scriptable renderer pass
// SPDX-FileCopyrightText: Copyright (c) Takayuki Matsuoka
// SPDX-License-Identifier: MIT

#define ENABLE_PROFILER
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DepthColorGrading {
    public sealed class DepthColorGradingPass : ScriptableRenderPass
    {
        private const string ShaderName = "PostEffect/DepthColorGrading";

        private static class ShaderId
        {
            public static readonly int MainTex           = Shader.PropertyToID("_MainTex");
            public static readonly int TempBuf           = Shader.PropertyToID("_TempBuf");
            public static readonly int Lut0              = Shader.PropertyToID("_DepthColorGradingLut0");
            public static readonly int Lut1              = Shader.PropertyToID("_DepthColorGradingLut1");
            public static readonly int Params            = Shader.PropertyToID("_DepthColorGradingParams");
            public static readonly int Params2           = Shader.PropertyToID("_DepthColorGradingParams2");
            public static readonly int Params3           = Shader.PropertyToID("_DepthColorGradingParams3");
            public static readonly int DepthToViewParams = Shader.PropertyToID("_DepthColorGradingDepthToViewParams");
        }

#if ENABLE_PROFILER
        private const string ProfilingSamplerBlockName = "DepthColorGradingPass";
        private static readonly ProfilingSampler ProfilingSampler = new ProfilingSampler(ProfilingSamplerBlockName);
#endif

        private readonly Material _material;
        private readonly DepthColorGradingLut2d _lut2d0 = new DepthColorGradingLut2d();
        private readonly DepthColorGradingLut2d _lut2d1 = new DepthColorGradingLut2d();

//      public static DepthColorGradingPass Create() => new DepthColorGradingPass(RenderPassEvent.AfterRenderingOpaques);
//      public static DepthColorGradingPass Create() => new DepthColorGradingPass(RenderPassEvent.BeforeRenderingPostProcessing);
        public static DepthColorGradingPass Create() => new DepthColorGradingPass(RenderPassEvent.AfterRenderingPostProcessing);

        private DepthColorGradingPass(RenderPassEvent renderPassEvent)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"Shader not found. ({ShaderName})");
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(shader);
            if (_material == null)
            {
                Debug.LogError("Material not created.");
                return;
            }

            this.renderPassEvent = renderPassEvent;
        }

        public void Cleanup()
        {
            CoreUtils.Destroy(_material);
        }

        public void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(this);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            ref CameraData cameraData = ref renderingData.cameraData;
            if (!cameraData.postProcessEnabled)
            {
                return;
            }

            VolumeStack volumeStack = VolumeManager.instance.stack;
            DepthColorGrading depthColorGrading = volumeStack.GetComponent<DepthColorGrading>();
            if (depthColorGrading == null || !depthColorGrading.IsActive())
            {
                return;
            }

            Camera camera = cameraData.camera;
            camera.TryGetComponent<UniversalAdditionalCameraData>(out var universalAdditionalCameraData);
            if (universalAdditionalCameraData != null)
            {
                // Require depth texture
                universalAdditionalCameraData.requiresDepthOption = UnityEngine.Rendering.Universal.CameraOverrideOption.On;
            }

            {
                float blend = depthColorGrading.blend.value;
                float skyBoxBlend = depthColorGrading.skyBoxBlend.value;
                float far = Mathf.Pow(16384.0f, Mathf.Pow(depthColorGrading.falloff.value, 2.0f));
                float falloff = -1.0f / (far * 0.5f);
                float hdrScale = cameraData.isHdrEnabled ? 4.0f : 1.0f;
                float lutWidth = _lut2d0.Width;
                float lutHeight = _lut2d0.Height;

                float a = skyBoxBlend * blend;
                float b = blend;
                float c = 1.0f / hdrScale;
                float d = hdrScale;

                float a2 = 1.0f / lutHeight;
                float b2 = lutHeight - 1;
                float c2 = falloff;
                float d2 = 0;               // (unused)

                float a3 = 0.5f / lutWidth;
                float b3 = 0.5f / lutHeight;
                float c3 = (lutHeight-1) / lutWidth;
                float d3 = (lutHeight-1) / lutHeight;

                _material.SetVector(ShaderId.Params, new Vector4(a, b, c, d));
                _material.SetVector(ShaderId.Params2, new Vector4(a2, b2, c2, d2));
                _material.SetVector(ShaderId.Params3, new Vector4(a3, b3, c3, d3));
            }

            {
                Matrix4x4 c = camera.projectionMatrix;
                float m00 = c[0,0];
                float m02 = c[0,2];
                float m11 = c[1,1];
                float m20 = c[2,0];
                float x = 2f / m00;
                float y = (-1f - m02) / m00;
                float z = 2f / m11;
                float w = (-1f - m20) / m11;
                _material.SetVector(ShaderId.DepthToViewParams, new Vector4(x, y, z, w));
            }

            {
                _lut2d0.h       = depthColorGrading.h0.value;
                _lut2d0.s       = depthColorGrading.s0.value;
                _lut2d0.l       = depthColorGrading.l0.value;
                _lut2d0.gamma   = -depthColorGrading.gamma0.value;
                _lut2d0.tint    = depthColorGrading.tint0.value;
                _lut2d0.lift    = depthColorGrading.lift0.value;

                _lut2d1.h       = depthColorGrading.h1.value;
                _lut2d1.s       = depthColorGrading.s1.value;
                _lut2d1.l       = depthColorGrading.l1.value;
                _lut2d1.gamma   = -depthColorGrading.gamma1.value;
                _lut2d1.tint    = depthColorGrading.tint1.value;
                _lut2d1.lift    = depthColorGrading.lift1.value;

                _material.SetTexture(ShaderId.Lut0, _lut2d0.Texture);
                _material.SetTexture(ShaderId.Lut1, _lut2d1.Texture);
            }

            CommandBuffer cmd = CommandBufferPool.Get();
#if ENABLE_PROFILER
            using (new ProfilingScope(cmd, ProfilingSampler))
#endif
            {
                var renderer = renderingData.cameraData.renderer;
                RenderTextureDescriptor rtd = renderingData.cameraData.cameraTargetDescriptor;
                rtd.depthBufferBits = 0;
                rtd.msaaSamples = 1;

                cmd.GetTemporaryRT(ShaderId.TempBuf, rtd, FilterMode.Bilinear);
                cmd.Blit(source: renderer.cameraColorTarget, dest: ShaderId.TempBuf);
                cmd.SetGlobalTexture(ShaderId.MainTex, ShaderId.TempBuf);
                cmd.Blit(source: ShaderId.TempBuf, dest: renderer.cameraColorTarget, _material, pass: 0);
                cmd.ReleaseTemporaryRT(ShaderId.TempBuf);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
