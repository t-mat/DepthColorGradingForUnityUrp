// 2D LUT for color grading
// SPDX-FileCopyrightText: Copyright (c) Takayuki Matsuoka
// SPDX-License-Identifier: MIT

using Unity.Collections;
using UnityEngine;

namespace DepthColorGrading
{
    public sealed class DepthColorGradingLut2d
    {
        [Range(-1f, 1f)] public float h     = 0.0f;
        [Range(-1f, 1f)] public float s     = 0.0f;
        [Range(-1f, 1f)] public float l     = 0.0f;
        [Range(-1f, 1f)] public float gamma = 0.0f;
        public                  Color tint  = Color.white;
        public                  Color lift  = Color.black;

        private Texture2D _lut;
        private float     _oH = -16.0f;
        private float     _oS;
        private float     _oL;
        private float     _oGamma;
        private Color     _oTint;
        private Color     _oLift;

        private bool IsDirty()
        {
            static bool Equal(float x, float y) => x == y;

            return ! Equal(h,     _oH)     || //
                   ! Equal(s,     _oS)     ||
                   ! Equal(l,     _oL)     ||
                   ! Equal(gamma, _oGamma) ||
                   tint != _oTint          ||
                   lift != _oLift;
        }

        private void UpdateCachedValue()
        {
            _oH     = h;
            _oS     = s;
            _oL     = l;
            _oGamma = gamma;
            _oTint  = tint;
            _oLift  = lift;
        }

        private const int           Size = 16;
        public        int           Width  => Size * Size;
        public        int           Height => Size;
        private const TextureFormat Format = TextureFormat.RGBA32;

        public Texture2D Texture
        {
            get
            {
                if (_lut == null || IsDirty())
                {
                    if (_lut == null)
                    {
                        _lut = CreateTexture2D(Format, Width, Height);
                    }

                    UpdateCachedValue();
                    UpdateLutTexture2D(_lut, h, s, l, gamma, tint, lift);
                }

                return _lut;
            }
        }

        private static Texture2D CreateTexture2D(TextureFormat format, int width, int height) =>
            new(width, height, format, mipChain: false, linear: true)
            {
                wrapMode = TextureWrapMode.Clamp, anisoLevel = 0, hideFlags = HideFlags.HideAndDontSave
            };

        private static void UpdateLutTexture2D(
            Texture2D tex2d,
            float     deltaH,
            float     deltaS,
            float     deltaL,
            float     deltaGamma,
            Color     tint,
            Color     lift
        )
        {
            deltaH     = Mathf.Pow(deltaH,     3f);
            deltaGamma = Mathf.Pow(deltaGamma, 3);
            lift.a     = Mathf.Pow(lift.a,     2);
            tint.a     = Mathf.Pow(tint.a,     2);

            int                  width    = tex2d.width;
            int                  height   = tex2d.height;
            NativeArray<Color32> color32S = tex2d.GetPixelData<Color32>(mipLevel: 0);
            UpdateLutNativeArray(color32S, width, height, deltaH, deltaS, deltaL, deltaGamma, tint, lift);
            tex2d.Apply(updateMipmaps: false);
        }

        private static void UpdateLutNativeArray(
            NativeArray<Color32> color32S,
            int                  width,
            int                  height,
            float                deltaH,
            float                deltaS,
            float                deltaL,
            float                deltaGamma,
            Color                tint,
            Color                lift
        )
        {
            float gm   = Mathf.Pow(10.0f, deltaGamma);
            int   size = height;

            // k : Mapping coefficient from int[0,size-1] to float[0,1].
            float k = 1.0f / (size - 1);

            for (int y = 0; y < height; y++)
            {
                float sg = y * k;
                for (int x = 0; x < width; x++)
                {
                    float sr = x % size        * k;
                    float sb = (int)(x / size) * k;
                    float gr = Mathf.Pow(sr, gm);
                    float gg = Mathf.Pow(sg, gm);
                    float gb = Mathf.Pow(sb, gm);
                    Color.RGBToHSV(new Color(gr, gg, gb, 0), out float h, out float s, out float v);
                    h += deltaH;
                    s += deltaS;
                    s =  Mathf.Clamp(value: s, min: 0, max: 1);
                    v *= Mathf.Clamp(value: deltaL, min: 0, max: 1) + 1.0f;
                    Color   newColor = Color.HSVToRGB(h, s, v, hdr: false);
                    Vector3 newRgb   = new(newColor.r, newColor.g, newColor.b);
                    if (deltaL < 0.0f)
                    {
                        newRgb *= deltaL + 1.0f;
                    }

                    static byte F(float newRgb, float tint, float tintA, float lift, float liftA)
                    {
                        float x = Mathf.Lerp(a: newRgb, b: newRgb * tint, t: tintA) + lift * liftA;

                        return (byte)Mathf.Clamp(
                            value: x * 256,
                            min: 0,
                            max: 255
                        );
                    }

                    byte ir = F(newRgb.x, tint.r, tint.a, lift.r, lift.a);
                    byte ig = F(newRgb.y, tint.g, tint.a, lift.g, lift.a);
                    byte ib = F(newRgb.z, tint.b, tint.a, lift.b, lift.a);

                    color32S[y * width + x] = new Color32(
                        r: ir,
                        g: ig,
                        b: ib,
                        a: 0
                    );
                }
            }
        }
    }
}
