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
internal sealed class M3SchemeBuilder
{
    private M3ColorScheme _scheme = new();
    private bool _has_tertiary_source;
    private HCTColor _tertiary_source;
    private readonly double[] _minimum_tones=new double[6], _maximum_tones=new double[6];
    private readonly bool[] _has_minimum_tone=new bool[6], _has_maximum_tone=new bool[6];
    private readonly double[] _tones=new double[45];
    private readonly SRGBColor[] _colors=Enumerable.Repeat(new SRGBColor(),45).ToArray();
    private readonly bool[] _tone_resolved=new bool[45], _color_resolved=new bool[45];
    public M3SchemeBuilder(HCTColor primary_source,HCTColor? tertiary_source,bool is_dark,double contrast_level)
    {
        _has_tertiary_source=tertiary_source.HasValue;
        _tertiary_source=tertiary_source ?? new HCTColor();
        _scheme.PrimarySourceColor=primary_source;
        if(tertiary_source.HasValue) _scheme.TertiarySourceColor=tertiary_source.Value;
        _scheme.IsDark=is_dark;
        _scheme.ContrastLevel=contrast_level;
        InitializePalettes();
    }
    // source line 368
    public M3ColorScheme Build()
    {

        AssignColors();
        return _scheme;

    }
    // source line 376
    public HCTColor SelectedTertiarySource()
    {

        return _has_tertiary_source ? _tertiary_source : _scheme.PrimarySourceColor;

    }
    // source line 380
    public void InitializePalettes()
    {

        HCTColor primary = _scheme.PrimarySourceColor;
        _scheme.PrimaryPalette = M3TonalPalette.FromHueAndChroma(primary.Hue, primary.Chroma);
        _scheme.SecondaryPalette =
            M3TonalPalette.FromHueAndChroma(primary.Hue, primary.Chroma * 0.5);
        _scheme.NeutralPalette =
            M3TonalPalette.FromHueAndChroma(primary.Hue, primary.Chroma * 0.2);
        _scheme.NeutralVariantPalette = _scheme.NeutralPalette;

        HCTColor tertiary = SelectedTertiarySource();
        bool same_quantized_color =
            primary.ToSrgb8().ToArgb32() == tertiary.ToSrgb8().ToArgb32();
        _scheme.TertiaryPalette = same_quantized_color ?
            M3TonalPalette.FromHueAndChroma(primary.Hue, primary.Chroma * 0.75) :
            M3TonalPalette.FromHueAndChroma(tertiary.Hue, tertiary.Chroma);

        _scheme.ErrorPalette = M3TonalPalette.FromHueAndChroma(
            ErrorHue(primary.Hue, _scheme.TertiaryPalette.Hue()),
            CppMath.Max(primary.Chroma, 50.0)
        );

    }
    // source line 403
    public M3TonalPalette Palette(EPalette palette)
    {

        switch (palette)
        {
        case EPalette.Primary:
            return _scheme.PrimaryPalette;
        case EPalette.Secondary:
            return _scheme.SecondaryPalette;
        case EPalette.Tertiary:
            return _scheme.TertiaryPalette;
        case EPalette.Neutral:
            return _scheme.NeutralPalette;
        case EPalette.NeutralVariant:
            return _scheme.NeutralVariantPalette;
        case EPalette.Error:
            return _scheme.ErrorPalette;
        default:
            return _scheme.NeutralPalette;
        }

    }
    // source line 424
    public EPalette RolePalette(ERole role)
    {

        if (role >= ERole.Primary && role <= ERole.OnPrimaryFixedVariant)
        {
            return EPalette.Primary;
        }
        if (role >= ERole.Secondary && role <= ERole.OnSecondaryFixedVariant)
        {
            return EPalette.Secondary;
        }
        if (role >= ERole.Tertiary && role <= ERole.OnTertiaryFixedVariant)
        {
            return EPalette.Tertiary;
        }
        if (role >= ERole.Error)
        {
            return EPalette.Error;
        }
        return EPalette.Neutral;

    }
    // source line 445
    public static double FindBestToneForChroma(M3TonalPalette palette,
        bool decreasing)
    {

        double hue = palette.Hue();
        double chroma = palette.Chroma();
        double tone = decreasing ? 100.0 : 0.0;
        double answer = tone;
        HCTColor best = M3ColorHelper.SampleHctRaw(hue, chroma, tone);

        while (best.Chroma < chroma)
        {
            if (tone < 0.0 || tone > 100.0)
            {
                break;
            }
            tone += decreasing ? -1.0 : 1.0;
            HCTColor candidate = M3ColorHelper.SampleHctRaw(hue, chroma, tone);
            if (best.Chroma < candidate.Chroma)
            {
                best = candidate;
                answer = tone;
            }
        }
        return answer;

    }
    // source line 473
    public double MaximumTone(EPalette palette)
    {

        int index = PaletteIndex(palette);
        if (!_has_maximum_tone[index])
        {
            _maximum_tones[index] = FindBestToneForChroma(Palette(palette), true);
            _has_maximum_tone[index] = true;
        }
        return _maximum_tones[index];

    }
    // source line 484
    public double MinimumTone(EPalette palette)
    {

        int index = PaletteIndex(palette);
        if (!_has_minimum_tone[index])
        {
            _minimum_tones[index] = FindBestToneForChroma(Palette(palette), false);
            _has_minimum_tone[index] = true;
        }
        return _minimum_tones[index];

    }
    // source line 495
    public ERole HighestSurface()
    {

        return _scheme.IsDark ? ERole.SurfaceBright : ERole.SurfaceDim;

    }
    // source line 500
    public ERole Background(ERole role, ref bool has_background)
    {

        has_background = true;
        switch (role)
        {
        case ERole.OnSurface:
        case ERole.OnSurfaceVariant:
        case ERole.Outline:
        case ERole.OutlineVariant:
        case ERole.Primary:
        case ERole.PrimaryContainer:
        case ERole.PrimaryFixed:
        case ERole.PrimaryFixedDim:
        case ERole.Secondary:
        case ERole.SecondaryContainer:
        case ERole.SecondaryFixed:
        case ERole.SecondaryFixedDim:
        case ERole.Tertiary:
        case ERole.TertiaryContainer:
        case ERole.TertiaryFixed:
        case ERole.TertiaryFixedDim:
        case ERole.Error:
        case ERole.ErrorContainer:
            return HighestSurface();
        case ERole.InverseOnSurface:
        case ERole.InversePrimary:
            return ERole.InverseSurface;
        case ERole.OnPrimary:
            return ERole.Primary;
        case ERole.OnPrimaryContainer:
            return ERole.PrimaryContainer;
        case ERole.OnPrimaryFixed:
        case ERole.OnPrimaryFixedVariant:
            return ResolveTone(ERole.PrimaryFixed) > 57.0 ?
                ERole.PrimaryFixedDim :
                ERole.PrimaryFixed;
        case ERole.OnSecondary:
            return ERole.Secondary;
        case ERole.OnSecondaryContainer:
            return ERole.SecondaryContainer;
        case ERole.OnSecondaryFixed:
        case ERole.OnSecondaryFixedVariant:
            return ResolveTone(ERole.SecondaryFixed) > 57.0 ?
                ERole.SecondaryFixedDim :
                ERole.SecondaryFixed;
        case ERole.OnTertiary:
            return ERole.Tertiary;
        case ERole.OnTertiaryContainer:
            return ERole.TertiaryContainer;
        case ERole.OnTertiaryFixed:
        case ERole.OnTertiaryFixedVariant:
            return ResolveTone(ERole.TertiaryFixed) > 57.0 ?
                ERole.TertiaryFixedDim :
                ERole.TertiaryFixed;
        case ERole.OnError:
            return ERole.Error;
        case ERole.OnErrorContainer:
            return ERole.ErrorContainer;
        default:
            has_background = false;
            return ERole.Surface;
        }

    }
    // source line 564
    public double FixedTone(EPalette palette)
    {

        switch (palette)
        {
        case EPalette.Primary: {
            HCTColor source = _scheme.PrimarySourceColor;
            if (source.Chroma <= 12.0)
            {
                return 90.0;
            }
            return source.Tone > 55.0 ?
                ClampDouble(61.0, 90.0, source.Tone) :
                ClampDouble(30.0, 49.0, source.Tone);
        }
        case EPalette.Secondary:
            return ClampDouble(61.0, 90.0, MaximumTone(EPalette.Secondary));
        case EPalette.Tertiary: {
            double source_tone = SelectedTertiarySource().Tone;
            return source_tone > 55.0 ?
                ClampDouble(61.0, 90.0, source_tone) :
                ClampDouble(20.0, 49.0, source_tone);
        }
        default:
            return 0.0;
        }

    }
    // source line 591
    public double BaseTone(ERole role)
    {

        HCTColor primary = _scheme.PrimarySourceColor;
        switch (role)
        {
        case ERole.Surface:
            return _scheme.IsDark ? 4.0 : 98.0;
        case ERole.SurfaceDim:
            return _scheme.IsDark ? 4.0 : 87.0;
        case ERole.SurfaceBright:
            return _scheme.IsDark ? 18.0 : 98.0;
        case ERole.SurfaceContainerLowest:
            return _scheme.IsDark ? 0.0 : 100.0;
        case ERole.SurfaceContainerLow:
            return _scheme.IsDark ? 6.0 : 96.0;
        case ERole.SurfaceContainer:
            return _scheme.IsDark ? 9.0 : 94.0;
        case ERole.SurfaceContainerHigh:
            return _scheme.IsDark ? 12.0 : 92.0;
        case ERole.SurfaceContainerHighest:
            return _scheme.IsDark ? 15.0 : 90.0;
        case ERole.InverseSurface:
            return _scheme.IsDark ? 98.0 : 4.0;
        case ERole.Shadow:
        case ERole.Scrim:
            return 0.0;
        case ERole.Primary:
            return primary.Chroma <= 12.0 ? (_scheme.IsDark ? 80.0 : 40.0) : primary.Tone;
        case ERole.PrimaryContainer:
            if (!_scheme.IsDark && primary.Chroma <= 12.0)
            {
                return 90.0;
            }
            return primary.Tone > 55.0 ?
                ClampDouble(61.0, 90.0, primary.Tone) :
                ClampDouble(30.0, 49.0, primary.Tone);
        case ERole.InversePrimary:
            return MaximumTone(EPalette.Primary);
        case ERole.PrimaryFixed:
        case ERole.PrimaryFixedDim:
            return FixedTone(EPalette.Primary);
        case ERole.Secondary:
            return _scheme.IsDark ?
                MinimumTone(EPalette.Secondary) :
                MaximumTone(EPalette.Secondary);
        case ERole.SecondaryContainer:
            return _scheme.IsDark ?
                ClampDouble(20.0, 49.0, MinimumTone(EPalette.Secondary)) :
                ClampDouble(61.0, 90.0, MaximumTone(EPalette.Secondary));
        case ERole.SecondaryFixed:
        case ERole.SecondaryFixedDim:
            return FixedTone(EPalette.Secondary);
        case ERole.Tertiary:
            return SelectedTertiarySource().Tone;
        case ERole.TertiaryContainer: {
            double source_tone = SelectedTertiarySource().Tone;
            return source_tone > 55.0 ?
                ClampDouble(61.0, 90.0, source_tone) :
                ClampDouble(20.0, 49.0, source_tone);
        }
        case ERole.TertiaryFixed:
        case ERole.TertiaryFixedDim:
            return FixedTone(EPalette.Tertiary);
        case ERole.Error:
            return MaximumTone(EPalette.Error);
        case ERole.ErrorContainer:
            return _scheme.IsDark ?
                MinimumTone(EPalette.Error) :
                MaximumTone(EPalette.Error);
        default: {
            bool has_background = false;
            ERole background = Background(role,ref has_background);
            return has_background ? ResolveTone(background) : 0.0;
        }
        }

    }
    // source line 668
    public double Curve(ERole role)
    {

        switch (role)
        {
        case ERole.OnSurface:
            return _scheme.IsDark ? 11.0 : 9.0;
        case ERole.OnSurfaceVariant:
            return _scheme.IsDark ? 6.0 : 4.5;
        case ERole.Outline:
            return 3.0;
        case ERole.OutlineVariant:
            return 1.5;
        case ERole.InverseOnSurface:
        case ERole.OnPrimaryFixed:
        case ERole.OnSecondaryFixed:
        case ERole.OnTertiaryFixed:
            return 7.0;
        case ERole.Primary:
        case ERole.Secondary:
        case ERole.Tertiary:
        case ERole.Error:
        case ERole.OnPrimaryFixedVariant:
        case ERole.OnSecondaryFixedVariant:
        case ERole.OnTertiaryFixedVariant:
            return 4.5;
        case ERole.OnPrimary:
        case ERole.OnPrimaryContainer:
        case ERole.InversePrimary:
        case ERole.OnSecondary:
        case ERole.OnSecondaryContainer:
        case ERole.OnTertiary:
        case ERole.OnTertiaryContainer:
        case ERole.OnError:
        case ERole.OnErrorContainer:
            return 6.0;
        case ERole.PrimaryContainer:
        case ERole.PrimaryFixed:
        case ERole.PrimaryFixedDim:
        case ERole.SecondaryContainer:
        case ERole.SecondaryFixed:
        case ERole.SecondaryFixedDim:
        case ERole.TertiaryContainer:
        case ERole.TertiaryFixed:
        case ERole.TertiaryFixedDim:
        case ERole.ErrorContainer:
            return _scheme.ContrastLevel > 0.0 ? 1.5 : 0.0;
        default:
            return 0.0;
        }

    }
    // source line 719
    public bool IsBackgroundRole(ERole role)
    {

        switch (role)
        {
        case ERole.Surface:
        case ERole.SurfaceDim:
        case ERole.SurfaceBright:
        case ERole.SurfaceContainerLowest:
        case ERole.SurfaceContainerLow:
        case ERole.SurfaceContainer:
        case ERole.SurfaceContainerHigh:
        case ERole.SurfaceContainerHighest:
        case ERole.InverseSurface:
        case ERole.Primary:
        case ERole.PrimaryContainer:
        case ERole.PrimaryFixed:
        case ERole.PrimaryFixedDim:
        case ERole.Secondary:
        case ERole.SecondaryContainer:
        case ERole.SecondaryFixed:
        case ERole.SecondaryFixedDim:
        case ERole.Tertiary:
        case ERole.TertiaryContainer:
        case ERole.TertiaryFixed:
        case ERole.TertiaryFixedDim:
        case ERole.Error:
        case ERole.ErrorContainer:
            return true;
        default:
            return false;
        }

    }
    // source line 752
    public bool IsFixedDim(ERole role)
    {

        return role == ERole.PrimaryFixedDim ||
            role == ERole.SecondaryFixedDim ||
            role == ERole.TertiaryFixedDim;

    }
    // source line 759
    public bool IsAccentPair(ERole role)
    {

        return role == ERole.Primary ||
            role == ERole.Secondary ||
            role == ERole.Tertiary ||
            role == ERole.Error;

    }
    // source line 767
    public ERole AccentContainer(ERole role)
    {

        switch (role)
        {
        case ERole.Primary:
            return ERole.PrimaryContainer;
        case ERole.Secondary:
            return ERole.SecondaryContainer;
        case ERole.Tertiary:
            return ERole.TertiaryContainer;
        default:
            return ERole.ErrorContainer;
        }

    }
    // source line 782
    public ERole FixedRole(ERole fixed_dim)
    {

        switch (fixed_dim)
        {
        case ERole.PrimaryFixedDim:
            return ERole.PrimaryFixed;
        case ERole.SecondaryFixedDim:
            return ERole.SecondaryFixed;
        default:
            return ERole.TertiaryFixed;
        }

    }
    // source line 795
    public double AdjustContrast(double tone, ERole background, double normal_ratio)
    {

        double background_tone = ResolveTone(background);
        double desired_ratio = ContrastCurve(normal_ratio, _scheme.ContrastLevel);
        if (RatioOfTones(background_tone, tone) >= desired_ratio &&
            _scheme.ContrastLevel >= 0.0)
        {
            return tone;
        }
        return ForegroundTone(background_tone, desired_ratio);

    }
    // source line 807
    public double AvoidAwkwardZone(ERole role, double tone)
    {

        if (!IsBackgroundRole(role) || IsFixedDim(role))
        {
            return tone;
        }
        return tone >= 57.0 ?
            ClampDouble(65.0, 100.0, tone) :
            ClampDouble(0.0, 49.0, tone);

    }
    // source line 818
    public double ResolveTone(ERole role)
    {

        int index = RoleIndex(role);
        if (_tone_resolved[index])
        {
            return _tones[index];
        }

        double answer = BaseTone(role);
        if (IsAccentPair(role))
        {
            double reference = ResolveTone(AccentContainer(role));
            answer = _scheme.IsDark ?
                ClampDouble(reference + 5.0, 100.0, answer) :
                ClampDouble(0.0, reference - 5.0, answer);
        }
        else if (IsFixedDim(role))
        {
            answer = ClampDouble(0.0, 100.0, ResolveTone(FixedRole(role)) - 5.0);
        }

        bool has_background = false;
        ERole background = Background(role,ref has_background);
        double curve = Curve(role);

        if (IsAccentPair(role) || IsFixedDim(role))
        {
            if (has_background && curve != 0.0)
            {
                answer = AdjustContrast(answer, background, curve);
            }
            answer = AvoidAwkwardZone(role, answer);
        }
        else if (has_background && curve != 0.0)
        {
            answer = AdjustContrast(answer, background, curve);
            answer = AvoidAwkwardZone(role, answer);
        }

        _tones[index] = answer;
        _tone_resolved[index] = true;
        return answer;

    }
    // source line 862
    public double ChromaMultiplier(ERole role)
    {

        switch (role)
        {
        case ERole.SurfaceDim:
            return _scheme.IsDark ? 1.0 : 1.7;
        case ERole.SurfaceBright:
            return _scheme.IsDark ? 1.7 : 1.0;
        case ERole.SurfaceContainerLow:
            return 1.25;
        case ERole.SurfaceContainer:
            return 1.4;
        case ERole.SurfaceContainerHigh:
            return 1.5;
        case ERole.SurfaceContainerHighest:
        case ERole.OnSurface:
        case ERole.OnSurfaceVariant:
        case ERole.Outline:
        case ERole.OutlineVariant:
        case ERole.InverseSurface:
            return 1.7;
        default:
            return 1.0;
        }

    }
    // source line 888
    public SRGBColor ResolveColor(ERole role)
    {

        int index = RoleIndex(role);
        if (_color_resolved[index])
        {
            return _colors[index];
        }

        M3TonalPalette palette = Palette(RolePalette(role));
        double tone = ResolveTone(role);
        double multiplier = ChromaMultiplier(role);
        SRGBColor result = multiplier == 1.0 ?
            palette.Srgb(tone) :
            M3ColorHelper.SampleSrgb8(
                palette.Hue(),
                palette.Chroma() * multiplier,
                tone
            );
        _colors[index] = result;
        _color_resolved[index] = true;
        return result;

    }
    // source line 911
    public void AssignColors()
    {

        _scheme.Surface = ResolveColor(ERole.Surface);
        _scheme.SurfaceDim = ResolveColor(ERole.SurfaceDim);
        _scheme.SurfaceBright = ResolveColor(ERole.SurfaceBright);
        _scheme.SurfaceContainerLowest = ResolveColor(ERole.SurfaceContainerLowest);
        _scheme.SurfaceContainerLow = ResolveColor(ERole.SurfaceContainerLow);
        _scheme.SurfaceContainer = ResolveColor(ERole.SurfaceContainer);
        _scheme.SurfaceContainerHigh = ResolveColor(ERole.SurfaceContainerHigh);
        _scheme.SurfaceContainerHighest = ResolveColor(ERole.SurfaceContainerHighest);
        _scheme.OnSurface = ResolveColor(ERole.OnSurface);
        _scheme.OnSurfaceVariant = ResolveColor(ERole.OnSurfaceVariant);
        _scheme.Outline = ResolveColor(ERole.Outline);
        _scheme.OutlineVariant = ResolveColor(ERole.OutlineVariant);
        _scheme.InverseSurface = ResolveColor(ERole.InverseSurface);
        _scheme.InverseOnSurface = ResolveColor(ERole.InverseOnSurface);
        _scheme.Shadow = ResolveColor(ERole.Shadow);
        _scheme.Scrim = ResolveColor(ERole.Scrim);

        _scheme.Primary = ResolveColor(ERole.Primary);
        _scheme.OnPrimary = ResolveColor(ERole.OnPrimary);
        _scheme.PrimaryContainer = ResolveColor(ERole.PrimaryContainer);
        _scheme.OnPrimaryContainer = ResolveColor(ERole.OnPrimaryContainer);
        _scheme.InversePrimary = ResolveColor(ERole.InversePrimary);
        _scheme.PrimaryFixed = ResolveColor(ERole.PrimaryFixed);
        _scheme.PrimaryFixedDim = ResolveColor(ERole.PrimaryFixedDim);
        _scheme.OnPrimaryFixed = ResolveColor(ERole.OnPrimaryFixed);
        _scheme.OnPrimaryFixedVariant = ResolveColor(ERole.OnPrimaryFixedVariant);

        _scheme.Secondary = ResolveColor(ERole.Secondary);
        _scheme.OnSecondary = ResolveColor(ERole.OnSecondary);
        _scheme.SecondaryContainer = ResolveColor(ERole.SecondaryContainer);
        _scheme.OnSecondaryContainer = ResolveColor(ERole.OnSecondaryContainer);
        _scheme.SecondaryFixed = ResolveColor(ERole.SecondaryFixed);
        _scheme.SecondaryFixedDim = ResolveColor(ERole.SecondaryFixedDim);
        _scheme.OnSecondaryFixed = ResolveColor(ERole.OnSecondaryFixed);
        _scheme.OnSecondaryFixedVariant = ResolveColor(ERole.OnSecondaryFixedVariant);

        _scheme.Tertiary = ResolveColor(ERole.Tertiary);
        _scheme.OnTertiary = ResolveColor(ERole.OnTertiary);
        _scheme.TertiaryContainer = ResolveColor(ERole.TertiaryContainer);
        _scheme.OnTertiaryContainer = ResolveColor(ERole.OnTertiaryContainer);
        _scheme.TertiaryFixed = ResolveColor(ERole.TertiaryFixed);
        _scheme.TertiaryFixedDim = ResolveColor(ERole.TertiaryFixedDim);
        _scheme.OnTertiaryFixed = ResolveColor(ERole.OnTertiaryFixed);
        _scheme.OnTertiaryFixedVariant = ResolveColor(ERole.OnTertiaryFixedVariant);

        _scheme.Error = ResolveColor(ERole.Error);
        _scheme.OnError = ResolveColor(ERole.OnError);
        _scheme.ErrorContainer = ResolveColor(ERole.ErrorContainer);
        _scheme.OnErrorContainer = ResolveColor(ERole.OnErrorContainer);



        _scheme.Background = _scheme.Surface;
        _scheme.OnBackground = _scheme.OnSurface;
        _scheme.SurfaceVariant = _scheme.SurfaceContainerHighest;
        _scheme.SurfaceTint = _scheme.Primary;
        _scheme.PrimaryDim = _scheme.Primary;
        _scheme.SecondaryDim = _scheme.Secondary;
        _scheme.TertiaryDim = _scheme.Tertiary;
        _scheme.ErrorDim = _scheme.Error;

    }
}
