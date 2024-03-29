﻿// Depth Color Grading VolumeComponent
// SPDX-FileCopyrightText: Copyright (c) Takayuki Matsuoka
// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DepthColorGrading
{
    public sealed class DepthColorGrading : VolumeComponent, IPostProcessComponent
    {
        public ClampedFloatParameter blend       = new(value: 0f, min: 0f, max: 1f);
        public ClampedFloatParameter skyBoxBlend = new(value: 0.5f, min: 0f, max: 1f);
        public ClampedFloatParameter falloff     = new(value: 0.1f, min: 0f, max: 1f);

        public ClampedFloatParameter h0     = new(value: 0f, min: -1f, max: 1f);
        public ClampedFloatParameter s0     = new(value: 0f, min: -1f, max: 1f);
        public ClampedFloatParameter l0     = new(value: 0f, min: -1f, max: 1f);
        public ClampedFloatParameter gamma0 = new(value: 0f, min: -1f, max: 1f);
        public ColorParameter        tint0  = new(Color.white);
        public ColorParameter        lift0  = new(Color.black);

        public ClampedFloatParameter h1     = new(value: 0f, min: -1f, max: 1f);
        public ClampedFloatParameter s1     = new(value: 0f, min: -1f, max: 1f);
        public ClampedFloatParameter l1     = new(value: 0f, min: -1f, max: 1f);
        public ClampedFloatParameter gamma1 = new(value: 0f, min: -1f, max: 1f);
        public ColorParameter        tint1  = new(Color.white);
        public ColorParameter        lift1  = new(Color.black);

        public bool IsActive()         => blend.value > 0f;
        public bool IsTileCompatible() => false;
    }
}
