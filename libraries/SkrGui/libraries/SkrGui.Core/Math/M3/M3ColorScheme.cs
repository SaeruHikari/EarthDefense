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

using static SkrGui.M3SchemeFunctions;
namespace SkrGui;
public struct M3ColorScheme
{
    public const uint kSpecVersion=2026;
    public M3ColorScheme() { }
    public HCTColor PrimarySourceColor = new();
    public HCTColor? TertiarySourceColor = null;
    public bool IsDark = new();
    public double ContrastLevel = new();
    public M3TonalPalette PrimaryPalette = new();
    public M3TonalPalette SecondaryPalette = new();
    public M3TonalPalette TertiaryPalette = new();
    public M3TonalPalette NeutralPalette = new();
    public M3TonalPalette NeutralVariantPalette = new();
    public M3TonalPalette ErrorPalette = new();
    public SRGBColor Background = new();
    public SRGBColor OnBackground = new();
    public SRGBColor Surface = new();
    public SRGBColor SurfaceDim = new();
    public SRGBColor SurfaceBright = new();
    public SRGBColor SurfaceContainerLowest = new();
    public SRGBColor SurfaceContainerLow = new();
    public SRGBColor SurfaceContainer = new();
    public SRGBColor SurfaceContainerHigh = new();
    public SRGBColor SurfaceContainerHighest = new();
    public SRGBColor OnSurface = new();
    public SRGBColor SurfaceVariant = new();
    public SRGBColor OnSurfaceVariant = new();
    public SRGBColor Outline = new();
    public SRGBColor OutlineVariant = new();
    public SRGBColor InverseSurface = new();
    public SRGBColor InverseOnSurface = new();
    public SRGBColor Shadow = new();
    public SRGBColor Scrim = new();
    public SRGBColor SurfaceTint = new();
    public SRGBColor Primary = new();
    public SRGBColor PrimaryDim = new();
    public SRGBColor OnPrimary = new();
    public SRGBColor PrimaryContainer = new();
    public SRGBColor OnPrimaryContainer = new();
    public SRGBColor InversePrimary = new();
    public SRGBColor PrimaryFixed = new();
    public SRGBColor PrimaryFixedDim = new();
    public SRGBColor OnPrimaryFixed = new();
    public SRGBColor OnPrimaryFixedVariant = new();
    public SRGBColor Secondary = new();
    public SRGBColor SecondaryDim = new();
    public SRGBColor OnSecondary = new();
    public SRGBColor SecondaryContainer = new();
    public SRGBColor OnSecondaryContainer = new();
    public SRGBColor SecondaryFixed = new();
    public SRGBColor SecondaryFixedDim = new();
    public SRGBColor OnSecondaryFixed = new();
    public SRGBColor OnSecondaryFixedVariant = new();
    public SRGBColor Tertiary = new();
    public SRGBColor TertiaryDim = new();
    public SRGBColor OnTertiary = new();
    public SRGBColor TertiaryContainer = new();
    public SRGBColor OnTertiaryContainer = new();
    public SRGBColor TertiaryFixed = new();
    public SRGBColor TertiaryFixedDim = new();
    public SRGBColor OnTertiaryFixed = new();
    public SRGBColor OnTertiaryFixedVariant = new();
    public SRGBColor Error = new();
    public SRGBColor ErrorDim = new();
    public SRGBColor OnError = new();
    public SRGBColor ErrorContainer = new();
    public SRGBColor OnErrorContainer = new();
    // source line 1004
    public static M3ColorScheme FromCMF(HCTColor primary_source_color,
    bool is_dark,
    double contrast_level = 0.0)
    {

    if (!M3ColorHelper.IsFinite(primary_source_color) || !double.IsFinite(contrast_level))
    {
        GuiAssert.Verify(false ,"M3ColorScheme.FromCMF requires finite inputs");
        return new M3ColorScheme();
    }

    HCTColor primary = M3ColorHelper.CanonicalizeSource(primary_source_color);
    return BuildScheme(primary, null, is_dark, CppMath.Clamp(contrast_level, -1.0, 1.0));

    }
    // source line 1019
    public static M3ColorScheme FromCMF(HCTColor primary_source_color,
    HCTColor tertiary_source_color,
    bool is_dark,
    double contrast_level = 0.0)
    {

    if (!M3ColorHelper.IsFinite(primary_source_color) ||
        !M3ColorHelper.IsFinite(tertiary_source_color) ||
        !double.IsFinite(contrast_level))
    {
        GuiAssert.Verify(false ,"M3ColorScheme.FromCMF requires finite inputs");
        return new M3ColorScheme();
    }

    HCTColor primary = M3ColorHelper.CanonicalizeSource(primary_source_color);
    HCTColor tertiary = M3ColorHelper.CanonicalizeSource(tertiary_source_color);
    return BuildScheme(primary, tertiary, is_dark, CppMath.Clamp(contrast_level, -1.0, 1.0));

    }
    // source line 1039
    public static M3ColorScheme FromCMF(SRGBColor primary_source_color,
    bool is_dark,
    double contrast_level = 0.0)
    {

    if (!M3ColorHelper.IsFiniteRgb(primary_source_color) || !double.IsFinite(contrast_level))
    {
        GuiAssert.Verify(false ,"M3ColorScheme.FromCMF requires finite encoded RGB and contrast");
        return new M3ColorScheme();
    }

    HCTColor primary = M3ColorHelper.CanonicalizeSource(primary_source_color);
    return BuildScheme(primary, null, is_dark, CppMath.Clamp(contrast_level, -1.0, 1.0));

    }
    // source line 1055
    public static M3ColorScheme FromCMF(SRGBColor primary_source_color,
    SRGBColor tertiary_source_color,
    bool is_dark,
    double contrast_level = 0.0)
    {

    if (!M3ColorHelper.IsFiniteRgb(primary_source_color) ||
        !M3ColorHelper.IsFiniteRgb(tertiary_source_color) ||
        !double.IsFinite(contrast_level))
    {
        GuiAssert.Verify(false ,"M3ColorScheme.FromCMF requires finite encoded RGB and contrast");
        return new M3ColorScheme();
    }

    HCTColor primary = M3ColorHelper.CanonicalizeSource(primary_source_color);
    HCTColor tertiary = M3ColorHelper.CanonicalizeSource(tertiary_source_color);
    return BuildScheme(primary, tertiary, is_dark, CppMath.Clamp(contrast_level, -1.0, 1.0));

    }
}
