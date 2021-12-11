// Depth Color Grading VolumeComponent
// SPDX-FileCopyrightText: Copyright (c) Takayuki Matsuoka
// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DepthColorGrading
{
    public sealed class DepthColorGrading : VolumeComponent, IPostProcessComponent
    {
        public ClampedFloatParameter blend       = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter skyBoxBlend = new ClampedFloatParameter(0.5f, 0f, 1f);
        public ClampedFloatParameter falloff     = new ClampedFloatParameter(0.1f, 0f, 1f);

        public ClampedFloatParameter h0          = new ClampedFloatParameter(0f, -1f, 1f);
        public ClampedFloatParameter s0          = new ClampedFloatParameter(0f, -1f, 1f);
        public ClampedFloatParameter l0          = new ClampedFloatParameter(0f, -1f, 1f);
        public ClampedFloatParameter gamma0      = new ClampedFloatParameter(0f, -1f, 1f);
        public ColorParameter        tint0       = new ColorParameter(Color.white);
        public ColorParameter        lift0       = new ColorParameter(Color.black);

        public ClampedFloatParameter h1          = new ClampedFloatParameter(0f, -1f, 1f);
        public ClampedFloatParameter s1          = new ClampedFloatParameter(0f, -1f, 1f);
        public ClampedFloatParameter l1          = new ClampedFloatParameter(0f, -1f, 1f);
        public ClampedFloatParameter gamma1      = new ClampedFloatParameter(0f, -1f, 1f);
        public ColorParameter        tint1       = new ColorParameter(Color.white);
        public ColorParameter        lift1       = new ColorParameter(Color.black);

        public bool IsActive() => blend.value > 0f;
        public bool IsTileCompatible() => false;
    }
}
