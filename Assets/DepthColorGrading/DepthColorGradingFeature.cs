// Depth Color Grading Renderer Feature
// SPDX-FileCopyrightText: Copyright (c) Takayuki Matsuoka
// SPDX-License-Identifier: MIT

using UnityEngine.Rendering.Universal;

namespace DepthColorGrading
{
    public sealed class DepthColorGradingFeature : ScriptableRendererFeature
    {
        private DepthColorGradingPass _pass;

        public override void Create()
            => _pass = DepthColorGradingPass.Create();

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
            => _pass.AddRenderPasses(renderer, ref renderingData);

        protected override void Dispose(bool disposing)
        {
            _pass.Cleanup();
            base.Dispose(disposing);
        }
    }
}
