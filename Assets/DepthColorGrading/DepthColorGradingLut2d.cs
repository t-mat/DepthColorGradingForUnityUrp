// 2D LUT for color grading
// SPDX-FileCopyrightText: Copyright (c) Takayuki Matsuoka
// SPDX-License-Identifier: MIT

using UnityEngine;

namespace DepthColorGrading {
    public sealed class DepthColorGradingLut2d {
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
            return h     != _oH
                || s     != _oS
                || l     != _oL
                || gamma != _oGamma
                || tint  != _oTint
                || lift  != _oLift;
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
                if ((_lut == null) || IsDirty())
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

        private static Texture2D CreateTexture2D(TextureFormat format, int width, int height)
        {
            var tex = new Texture2D(width, height, format, mipChain: false, linear: true);
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.anisoLevel = 0;
            tex.hideFlags  = HideFlags.HideAndDontSave;
            return tex;
        }

        private static void UpdateLutTexture2D(Texture2D tex2d, float dh, float ds, float dl, float dgamma, Color tint,
                                               Color     lift)
        {
            dh     = Mathf.Pow(dh,     3f);
            dgamma = Mathf.Pow(dgamma, 3);
            lift.a = Mathf.Pow(lift.a, 2);
            tint.a = Mathf.Pow(tint.a, 2);
            float gm = Mathf.Pow(10.0f, dgamma);

            int width  = tex2d.width;
            int height = tex2d.height;
            int size   = height;

            // d : Mapping coefficient from int[0,size-1] to float[0,1].
            float d = 1.0f / (size - 1);

            var colors = new Color [width * height];
            for (int y = 0; y < height; y++)
            {
                float sg = y * d;
                for (int x = 0; x < width; x++)
                {
                    float sr = (x % size) * d;
                    float sb = (x / size) * d;
                    float gr = Mathf.Pow(sr, gm);
                    float gg = Mathf.Pow(sg, gm);
                    float gb = Mathf.Pow(sb, gm);
                    Color.RGBToHSV(new Color(gr, gg, gb, 0), out float h, out float s, out float v);
                    h += dh;
                    s += ds;
                    s =  Mathf.Clamp(value: s, min: 0, max: 1);
                    v *= Mathf.Clamp(value: dl, min: 0, max: 1) + 1.0f;
                    Color   newColor = Color.HSVToRGB(h, s, v, hdr: false);
                    Vector3 newRgb   = new Vector3(newColor.r, newColor.g, newColor.b);
                    if (dl < 0.0f)
                    {
                        newRgb *= dl + 1.0f;
                    }

                    float or = Mathf.Lerp(newRgb.x, newRgb.x * tint.r, tint.a) + lift.r * lift.a;
                    float og = Mathf.Lerp(newRgb.y, newRgb.y * tint.g, tint.a) + lift.g * lift.a;
                    float ob = Mathf.Lerp(newRgb.z, newRgb.z * tint.b, tint.a) + lift.b * lift.a;

                    colors[y * width + x] = new Color(r: or, g: og, b: ob, a: 0);
                }
            }

            tex2d.SetPixels(colors);
            tex2d.Apply(updateMipmaps: false);
        }
    }
}
