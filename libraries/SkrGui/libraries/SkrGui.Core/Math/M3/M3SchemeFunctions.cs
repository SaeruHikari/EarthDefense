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
internal enum EPalette : byte
{
    Primary,
    Secondary,
    Tertiary,
    Neutral,
    NeutralVariant,
    Error,
    Count,
}
internal enum ERole : byte
{
    Surface,
    SurfaceDim,
    SurfaceBright,
    SurfaceContainerLowest,
    SurfaceContainerLow,
    SurfaceContainer,
    SurfaceContainerHigh,
    SurfaceContainerHighest,
    OnSurface,
    OnSurfaceVariant,
    Outline,
    OutlineVariant,
    InverseSurface,
    InverseOnSurface,
    Shadow,
    Scrim,

    Primary,
    OnPrimary,
    PrimaryContainer,
    OnPrimaryContainer,
    InversePrimary,
    PrimaryFixed,
    PrimaryFixedDim,
    OnPrimaryFixed,
    OnPrimaryFixedVariant,

    Secondary,
    OnSecondary,
    SecondaryContainer,
    OnSecondaryContainer,
    SecondaryFixed,
    SecondaryFixedDim,
    OnSecondaryFixed,
    OnSecondaryFixedVariant,

    Tertiary,
    OnTertiary,
    TertiaryContainer,
    OnTertiaryContainer,
    TertiaryFixed,
    TertiaryFixedDim,
    OnTertiaryFixed,
    OnTertiaryFixedVariant,

    Error,
    OnError,
    ErrorContainer,
    OnErrorContainer,

    Count,
}
internal static class M3SchemeFunctions
{
    // source line 102
    public static int RoleIndex(ERole role)
    {

    return (int)(role);

    }
    // source line 107
    public static int PaletteIndex(EPalette palette)
    {

    return (int)(palette);

    }
    // source line 115
    public static double ClampDouble(double minimum, double maximum, double value)
    {

    if (value < minimum)
    {
        return minimum;
    }
    if (value > maximum)
    {
        return maximum;
    }
    return value;

    }
    // source line 128
    public static double Lerp(double start, double stop, double amount)
    {

    return (1.0 - amount) * start + amount * stop;

    }
    // source line 133
    public static double LabF(double value)
    {

    double kEpsilon = 216.0 / 24389.0;
    double kKappa = 24389.0 / 27.0;
    return value > kEpsilon ? Math.Pow(value, 1.0 / 3.0) :
                              (kKappa * value + 16.0) / 116.0;

    }
    // source line 141
    public static double LabInverseF(double value)
    {

    double kEpsilon = 216.0 / 24389.0;
    double kKappa = 24389.0 / 27.0;
    double cube = value * value * value;
    return cube > kEpsilon ? cube : (116.0 * value - 16.0) / kKappa;

    }
    // source line 149
    public static double YFromLstar(double lstar)
    {

    return 100.0 * LabInverseF((lstar + 16.0) / 116.0);

    }
    // source line 154
    public static double LstarFromY(double y)
    {

    return LabF(y / 100.0) * 116.0 - 16.0;

    }
    // source line 159
    public static double RatioOfTones(double first, double second)
    {

    double first_y = YFromLstar(first);
    double second_y = YFromLstar(second);
    double lighter = CppMath.Max(first_y, second_y);
    double darker = lighter == second_y ? first_y : second_y;
    return (lighter + 5.0) / (darker + 5.0);

    }
    // source line 168
    public static double LighterTone(double tone, double ratio)
    {

    double kRatioEpsilon = 0.04;
    double kGamutTolerance = 0.4;
    if (tone < 0.0 || tone > 100.0)
    {
        return -1.0;
    }

    double dark_y = YFromLstar(tone);
    double light_y = ratio * (dark_y + 5.0) - 5.0;
    if (light_y < 0.0 || light_y > 100.0)
    {
        return -1.0;
    }
    double real_ratio = (light_y + 5.0) / (dark_y + 5.0);
    if (real_ratio < ratio && Math.Abs(real_ratio - ratio) > kRatioEpsilon)
    {
        return -1.0;
    }

    double result = LstarFromY(light_y) + kGamutTolerance;
    return result < 0.0 || result > 100.0 ? -1.0 : result;

    }
    // source line 193
    public static double DarkerTone(double tone, double ratio)
    {

    double kRatioEpsilon = 0.04;
    double kGamutTolerance = 0.4;
    if (tone < 0.0 || tone > 100.0)
    {
        return -1.0;
    }

    double light_y = YFromLstar(tone);
    double dark_y = (light_y + 5.0) / ratio - 5.0;
    if (dark_y < 0.0 || dark_y > 100.0)
    {
        return -1.0;
    }
    double real_ratio = (light_y + 5.0) / (dark_y + 5.0);
    if (real_ratio < ratio && Math.Abs(real_ratio - ratio) > kRatioEpsilon)
    {
        return -1.0;
    }

    double result = LstarFromY(dark_y) - kGamutTolerance;
    return result < 0.0 || result > 100.0 ? -1.0 : result;

    }
    // source line 218
    public static double ForegroundTone(double background_tone, double ratio)
    {

    double lighter_answer = LighterTone(background_tone, ratio);
    double darker_answer = DarkerTone(background_tone, ratio);
    double light_tone = lighter_answer < 0.0 ? 100.0 : lighter_answer;
    double dark_tone = CppMath.Max(0.0, darker_answer);
    double lighter_ratio = RatioOfTones(light_tone, background_tone);
    double darker_ratio = RatioOfTones(dark_tone, background_tone);

    if (HctMath.Round(background_tone) < 60.0)
    {
        bool negligible_difference =
            Math.Abs(lighter_ratio - darker_ratio) < 0.1 &&
            lighter_ratio < ratio && darker_ratio < ratio;
        return lighter_ratio >= ratio || lighter_ratio >= darker_ratio || negligible_difference ?
            light_tone :
            dark_tone;
    }
    return darker_ratio >= ratio || darker_ratio >= lighter_ratio ? dark_tone : light_tone;

    }
    // source line 239
    public static double ContrastCurve(double normal, double contrast_level)
    {

    double low = normal;
    double medium = 7.0;
    double high = 21.0;
    if (normal == 1.5)
    {
        medium = 3.0;
        high = 5.5;
    }
    else if (normal == 3.0)
    {
        medium = 4.5;
        high = 7.0;
    }
    else if (normal == 4.5)
    {
        medium = 7.0;
        high = 11.0;
    }
    else if (normal == 6.0)
    {
        medium = 7.0;
        high = 11.0;
    }
    else if (normal == 7.0)
    {
        medium = 11.0;
        high = 21.0;
    }
    else if (normal == 9.0)
    {
        medium = 11.0;
        high = 21.0;
    }
    else if (normal == 11.0)
    {
        medium = 21.0;
        high = 21.0;
    }
    else if (normal == 21.0)
    {
        medium = 21.0;
        high = 21.0;
    }

    if (contrast_level <= -1.0)
    {
        return low;
    }
    if (contrast_level < 0.0)
    {
        return Lerp(low, normal, contrast_level + 1.0);
    }
    if (contrast_level < 0.5)
    {
        return Lerp(normal, medium, contrast_level / 0.5);
    }
    if (contrast_level < 1.0)
    {
        return Lerp(medium, high, (contrast_level - 0.5) / 0.5);
    }
    return high;

    }
    // source line 304
    public static double ErrorHue(double primary_hue, double tertiary_hue)
    {

    if (primary_hue <= 8.0)
    {
        return tertiary_hue <= 24.0 ? 28.0 : tertiary_hue <= 32.0 ? 16.0 :
                                                                    20.0;
    }
    if (primary_hue <= 16.0)
    {
        return tertiary_hue <= 24.0 ? 32.0 : tertiary_hue <= 32.0 ? 20.0 :
                                                                    24.0;
    }
    if (primary_hue <= 20.0)
    {
        return tertiary_hue <= 28.0 ? 32.0 : tertiary_hue <= 32.0 ? 24.0 :
                                                                    28.0;
    }
    if (primary_hue <= 28.0)
    {
        return tertiary_hue <= 24.0 ? 32.0 : 16.0;
    }
    if (primary_hue <= 32.0)
    {
        return tertiary_hue <= 20.0 ? 24.0 : tertiary_hue <= 28.0 ? 16.0 :
                                                                    20.0;
    }
    if (primary_hue <= 40.0)
    {
        return tertiary_hue > 20.0 && tertiary_hue <= 28.0 ? 16.0 : 24.0;
    }
    if (primary_hue <= 152.0)
    {
        return tertiary_hue > 24.0 && tertiary_hue <= 36.0 ? 20.0 : 32.0;
    }
    if (primary_hue <= 272.0)
    {
        return tertiary_hue > 20.0 && tertiary_hue <= 28.0 ? 16.0 : 24.0;
    }
    return tertiary_hue > 12.0 && tertiary_hue <= 28.0 ? 32.0 : 16.0;

    }
    // source line 991
    public static M3ColorScheme BuildScheme(HCTColor primary_source,
    HCTColor? tertiary_source,
    bool is_dark,
    double contrast_level)
    {

    return new M3SchemeBuilder(primary_source, tertiary_source, is_dark, contrast_level).Build();

    }
}
