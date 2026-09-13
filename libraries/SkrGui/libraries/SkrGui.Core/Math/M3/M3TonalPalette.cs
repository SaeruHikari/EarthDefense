/*
 * Portions of this file are adapted from Material Color Utilities.
 * Copyright 2021-2026 Google LLC
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Diagnostics.CodeAnalysis;
namespace SkrGui;
public struct M3TonalPalette
{
    private double _hue, _chroma;
    private HCTColor _key_color;
    private const double kMaximumChroma = 200.0;
    public M3TonalPalette() { }
    private M3TonalPalette(double hue, double chroma, HCTColor keyColor)
    { _hue=hue;_chroma=chroma;_key_color=keyColor; }
    // source line 31
    public static double MaxChromaAt(double hue,
    int tone,
    double[]  chroma_cache,
    bool[]  is_cached)
    {

    int index = (int)(tone);
    if (!is_cached[index])
    {
        chroma_cache[index] = M3ColorHelper.SampleHctRaw(hue, kMaximumChroma, (double)(tone)).Chroma;
        is_cached[index] = true;
    }
    return chroma_cache[index];

    }
    // source line 50
    public static HCTColor FindKeyColor(double hue, double chroma)
    {

    int kPivotTone = 50;
    int kToneStep = 1;
    double kChromaEpsilon = 0.01;

    double[] chroma_cache = new double[101];
    bool[] is_cached = new bool[101];

    int lower_tone = 0;
    int upper_tone = 100;
    while (lower_tone < upper_tone)
    {
        int middle_tone = (lower_tone + upper_tone) / 2;
        bool is_ascending =
            MaxChromaAt(hue, middle_tone, chroma_cache, is_cached) <
            MaxChromaAt(hue, middle_tone + kToneStep, chroma_cache, is_cached);
        bool has_sufficient_chroma =
            MaxChromaAt(hue, middle_tone, chroma_cache, is_cached) >= chroma - kChromaEpsilon;

        if (has_sufficient_chroma)
        {
            if (Math.Abs(lower_tone - kPivotTone) < Math.Abs(upper_tone - kPivotTone))
            {
                upper_tone = middle_tone;
            }
            else
            {
                if (lower_tone == middle_tone)
                {
                    return M3ColorHelper.SampleHctRaw(hue, chroma, (double)(lower_tone));
                }
                lower_tone = middle_tone;
            }
        }
        else if (is_ascending)
        {
            lower_tone = middle_tone + kToneStep;
        }
        else
        {
            upper_tone = middle_tone;
        }
    }

    return M3ColorHelper.SampleHctRaw(hue, chroma, (double)(lower_tone));

    }
    // source line 114
    public static M3TonalPalette FromHCT(HCTColor color)
    {

    if (!M3ColorHelper.IsFinite(color))
    {
        GuiAssert.Verify(false ,"M3TonalPalette.FromHCT requires a finite color");
        return new M3TonalPalette();
    }

    HCTColor realized = M3ColorHelper.CanonicalizeSource(color);
    return new M3TonalPalette(realized.Hue, realized.Chroma, realized);

    }
    // source line 125
    public static M3TonalPalette FromHueAndChroma(double hue, double chroma)
    {

    if (!double.IsFinite(hue) || !double.IsFinite(chroma))
    {
        GuiAssert.Verify(false ,"M3TonalPalette requires finite hue and chroma");
        return new M3TonalPalette();
    }

    HCTColor normalized = new HCTColor(hue, chroma, 50.0);
    return new M3TonalPalette(normalized.Hue,
        normalized.Chroma,
        FindKeyColor(normalized.Hue, normalized.Chroma));

    }
    // source line 143
    public double Hue()
    {

    return _hue;

    }
    // source line 147
    public double Chroma()
    {

    return _chroma;

    }
    // source line 152
    [UnscopedRef] public ref readonly HCTColor KeyColor()
    {

    return ref _key_color;

    }
    // source line 159
    public HCTColor Hct(double tone)
    {

    if (!double.IsFinite(tone))
    {
        GuiAssert.Verify(false ,"M3TonalPalette.hct requires a finite tone");
        return new HCTColor();
    }
    return M3ColorHelper.SampleHct(_hue, _chroma, CppMath.Clamp(tone, 0.0, 100.0));

    }
    // source line 168
    public SRGBColor Srgb(double tone)
    {

    if (!double.IsFinite(tone))
    {
        GuiAssert.Verify(false ,"M3TonalPalette.srgb requires a finite tone");
        return new SRGBColor();
    }
    return M3ColorHelper.SampleSrgb8(_hue, _chroma, CppMath.Clamp(tone, 0.0, 100.0));

    }
}
