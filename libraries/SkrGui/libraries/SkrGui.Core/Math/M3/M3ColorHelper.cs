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

namespace SkrGui;
internal static class M3ColorHelper
{
    // source line 52
    public static bool IsFinite(HCTColor color)
    {

    return double.IsFinite(color.Hue) &&
        double.IsFinite(color.Chroma) &&
        double.IsFinite(color.Tone);

    }
    // source line 58
    public static bool IsFiniteRgb(SRGBColor color)
    {

    return double.IsFinite(color.R) &&
        double.IsFinite(color.G) &&
        double.IsFinite(color.B);

    }
    // source line 65
    public static bool IsYellow(double hue)
    {

    return hue >= 105.0 && hue < 125.0;

    }
    // source line 70
    public static SRGBColor Srgb8FromBytes(byte red,
    byte green,
    byte blue)
    {

    return SRGBColor.FromARGB(255, red, green, blue);

    }
    // source line 79
    public static double LinearizeSrgb8(byte encoded)
    {




    double normalized = (double)(encoded) / 255.0;
    if (normalized <= 0.040449936)
    {
        return normalized / 12.92;
    }
    return Math.Pow((normalized + 0.055) / 1.055, 2.4);

    }
    // source line 92
    public static HCTColor HctFromSrgb8(SRGBColor color)
    {

    return HCTColor.FromLinear(
        LinearizeSrgb8(color.Red8()),
        LinearizeSrgb8(color.Green8()),
        LinearizeSrgb8(color.Blue8())
    );

    }
    // source line 101
    public static SRGBColor SampleSrgb8Raw(double hue,
    double chroma,
    double tone)
    {

    return new HCTColor(hue, chroma, tone).ToSrgb8();

    }
    // source line 110
    public static HCTColor SampleHctRaw(double hue, double chroma, double tone)
    {

    return HctFromSrgb8(SampleSrgb8Raw(hue, chroma, tone));

    }
    // source line 115
    public static SRGBColor SampleSrgb8(double hue, double chroma, double tone)
    {

    if (tone == 99.0 && IsYellow(hue))
    {
        SRGBColor lower = SampleSrgb8Raw(hue, chroma, 98.0);
        SRGBColor upper = SampleSrgb8Raw(hue, chroma, 100.0);
        return Srgb8FromBytes(
            (byte)(((ushort)(lower.Red8()) + upper.Red8() + 1u) / 2u),
            (byte)(((ushort)(lower.Green8()) + upper.Green8() + 1u) / 2u),
            (byte)(((ushort)(lower.Blue8()) + upper.Blue8() + 1u) / 2u)
        );
    }
    return SampleSrgb8Raw(hue, chroma, tone);

    }
    // source line 130
    public static HCTColor SampleHct(double hue, double chroma, double tone)
    {

    return HctFromSrgb8(SampleSrgb8(hue, chroma, tone));

    }
    // source line 135
    public static HCTColor CanonicalizeSource(HCTColor color)
    {

    HCTColor normalized = new HCTColor(color.Hue, color.Chroma, color.Tone);
    return HctFromSrgb8(normalized.ToSrgb8());

    }
    // source line 141
    public static HCTColor CanonicalizeSource(SRGBColor color)
    {

    return HctFromSrgb8(Srgb8FromBytes(color.Red8(), color.Green8(), color.Blue8()));

    }
}
