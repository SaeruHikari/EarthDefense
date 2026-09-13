/*
 * Portions of this file are adapted from Material Color Utilities.
 * Copyright 2021-2022 Google LLC
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


using System.Numerics;
namespace SkrGui;
// Source: build.hct_color.cpp at 611561f8; expression/control-flow translation.
internal static class HCTColorAlgorithm
{
    private static readonly Double3[] _scaled_discount_from_linear_rows = new Double3[]{
        new Double3(0.001200833568784504, 0.002389694492170889, 0.0002795742885861124),
        new Double3(0.0005891086651375999, 0.0029785502573438758, 0.0003270666104008398),
        new Double3(0.00010146692491640572, 0.0005364214359186694, 0.0032979401770712076),
    };
    private static readonly Double3[] _linear_from_scaled_discount_rows = new Double3[]{
        new Double3(1373.2198709594231, -1100.4251190754821, -7.278681089101213),
        new Double3(-271.815969077903, 559.6580465940733, -32.46047482791194),
        new Double3(1.9622899599665666, -57.173814538844006, 308.7233197812385),
    };
    private static readonly Double3[] _scaled_discount_from_xyz_rows = new Double3[]{
        new Double3(0.0015919440008891239, 0.0025792921963529525, -0.00020415021189209551),
        new Double3(-0.00095893252636511449, 0.004614859909415161, 0.00017569522297675275),
        new Double3(-0.0000075431595260605332, 0.00017761074801332964, 0.0034581957718111747),
    };
    private static readonly Double3[] _xyz_from_scaled_discount_rows = new Double3[]{
        new Double3(469.37925267151945, -263.92333862449578, 41.117956617179061),
        new Double3(97.685440816566739, 162.18910519892913, -2.4733555082601155),
        new Double3(-3.9932330450700513, -8.9056103737469616, 289.38484685728116),
    };
    private static readonly Double3 _y_from_linear = new(0.2126, 0.7152, 0.0722);
    private const double _background_y_to_white_y = 0.18418651851244416;
    private const double _aw = 29.980997194447337;
    private const double _nbb = 1.0169191804458755;
    private const double _ncb = 1.0169191804458755;
    private const double _c = 0.69;
    private const double _nc = 1.0;
    private const double _z = 1.909169568483652;
    private const double _chroma_base = 0.8834525670408592;
    private const double _t_inner_coeff = 1.1319226830134397;
    private const double _pi = 3.14159265358979323846;
    private const double _degrees_to_radians = _pi / 180.0;
    private const double _radians_to_degrees = 180.0 / _pi;
    private static readonly double[] _critical_planes = new double[]{
        0.015176349177441876,
        0.045529047532325624,
        0.07588174588720938,
        0.10623444424209313,
        0.13658714259697685,
        0.16693984095186062,
        0.19729253930674434,
        0.2276452376616281,
        0.2579979360165119,
        0.28835063437139563,
        0.3188300904430532,
        0.350925934958123,
        0.3848314933096426,
        0.42057480301049466,
        0.458183274052838,
        0.4976837250274023,
        0.5391024159806381,
        0.5824650784040898,
        0.6277969426914107,
        0.6751227633498623,
        0.7244668422128921,
        0.775853049866786,
        0.829304845476233,
        0.8848452951698498,
        0.942497089126609,
        1.0022825574869039,
        1.0642236851973577,
        1.1283421258858297,
        1.1946592148522128,
        1.2631959812511864,
        1.3339731595349034,
        1.407011200216447,
        1.4823302800086415,
        1.5599503113873272,
        1.6398909516233677,
        1.7221716113234105,
        1.8068114625156377,
        1.8938294463134073,
        1.9832442801866852,
        2.075074464868551,
        2.1693382909216234,
        2.2660538449872063,
        2.36523901573795,
        2.4669114995532007,
        2.5710888059345764,
        2.6777882626779785,
        2.7870270208169257,
        2.898822059350997,
        3.0131901897720907,
        3.1301480604002863,
        3.2497121605402226,
        3.3718988244681087,
        3.4967242352587946,
        3.624204428461639,
        3.754355295633311,
        3.887192587735158,
        4.022731918402185,
        4.160988767090289,
        4.301978482107941,
        4.445716283538092,
        4.592217266055746,
        4.741496401646282,
        4.893568542229298,
        5.048448422192488,
        5.20615066083972,
        5.3666897647573375,
        5.5300801301023865,
        5.696336044816294,
        5.865471690767354,
        6.037501145825082,
        6.212438385869475,
        6.390297286737924,
        6.571091626112461,
        6.7548350853498045,
        6.941541251256611,
        7.131223617812143,
        7.323895587840543,
        7.5195704746346665,
        7.7182615035334345,
        7.919981813454504,
        8.124744458384042,
        8.332562408825165,
        8.543448553206703,
        8.757415699253682,
        8.974476575321063,
        9.194643831691977,
        9.417930041841839,
        9.644347703669503,
        9.873909240696694,
        10.106627003236781,
        10.342513269534024,
        10.58158024687427,
        10.8238400726681,
        11.069304815507364,
        11.317986476196008,
        11.569896988756009,
        11.825048221409341,
        12.083451977536606,
        12.345119996613247,
        12.610063955123938,
        12.878295467455942,
        13.149826086772048,
        13.42466730586372,
        13.702830557985108,
        13.984327217668513,
        14.269168601521828,
        14.55736596900856,
        14.848930523210871,
        15.143873411576273,
        15.44220572664832,
        15.743938506781891,
        16.04908273684337,
        16.35764934889634,
        16.66964922287304,
        16.985093187232053,
        17.30399201960269,
        17.62635644741625,
        17.95219714852476,
        18.281524751807332,
        18.614349837764564,
        18.95068293910138,
        19.290534541298456,
        19.633915083172692,
        19.98083495742689,
        20.331304511189067,
        20.685334046541502,
        21.042933821039977,
        21.404114048223256,
        21.76888489811322,
        22.137256497705877,
        22.50923893145328,
        22.884842241736916,
        23.264076429332462,
        23.6469514538663,
        24.033477234264016,
        24.42366364919083,
        24.817520537484558,
        25.21505769858089,
        25.61628489293138,
        26.021211842414342,
        26.429848230738664,
        26.842203703840827,
        27.258287870275353,
        27.678110301598522,
        28.10168053274597,
        28.529008062403893,
        28.96010235337422,
        29.39497283293396,
        29.83362889318845,
        30.276079891419332,
        30.722335150426627,
        31.172403958865512,
        31.62629557157785,
        32.08401920991837,
        32.54558406207592,
        33.010999283389665,
        33.4802739966603,
        33.953417292456834,
        34.430438229418264,
        34.911345834551085,
        35.39614910352207,
        35.88485700094671,
        36.37747846067349,
        36.87402238606382,
        37.37449765026789,
        37.87891309649659,
        38.38727753828926,
        38.89959975977785,
        39.41588851594697,
        39.93615253289054,
        40.460400508064545,
        40.98864111053629,
        41.520882981230194,
        42.05713473317016,
        42.597404951718396,
        43.141702194811224,
        43.6900349931913,
        44.24241185063697,
        44.798841244188324,
        45.35933162437017,
        45.92389141541209,
        46.49252901546552,
        47.065252796817916,
        47.64207110610409,
        48.22299226451468,
        48.808024568002054,
        49.3971762874833,
        49.9904556690408,
        50.587870934119984,
        51.189430279724725,
        51.79514187861014,
        52.40501387947288,
        53.0190544071392,
        53.637271562750364,
        54.259673423945976,
        54.88626804504493,
        55.517063457223934,
        56.15206766869424,
        56.79128866487574,
        57.43473440856916,
        58.08241284012621,
        58.734331877617365,
        59.39049941699807,
        60.05092333227251,
        60.715611475655585,
        61.38457167773311,
        62.057811747619894,
        62.7353394731159,
        63.417162620860914,
        64.10328893648692,
        64.79372614476921,
        65.48848194977529,
        66.18756403501224,
        66.89098006357258,
        67.59873767827808,
        68.31084450182222,
        69.02730813691093,
        69.74813616640164,
        70.47333615344107,
        71.20291564160104,
        71.93688215501312,
        72.67524319850172,
        73.41800625771542,
        74.16517879925733,
        74.9167682708136,
        75.67278210128072,
        76.43322770089146,
        77.1981124613393,
        77.96744375590167,
        78.74122893956174,
        79.51947534912904,
        80.30219030335869,
        81.08938110306934,
        81.88105503125999,
        82.67721935322541,
        83.4778813166706,
        84.28304815182372,
        85.09272707154808,
        85.90692527145302,
        86.72564993000343,
        87.54890820862819,
        88.3767072518277,
        89.2090541872801,
        90.04595612594655,
        90.88742016217518,
        91.73345337380438,
        92.58406282226491,
        93.43925555268066,
        94.29903859396902,
        95.16341895893969,
        96.03240364439274,
        96.9059996312159,
        97.78421388448044,
        98.6670533535366,
        99.55452497210776,
    };
    // source line 481
    public static HCTColor FromXYZ(Double3 xyz)
    {
if (!IsFinite(xyz) || xyz.Y < 0.0 || xyz.Y > 1.0)
    {
        GuiAssert.Verify(false ,"HCTColor.FromXYZ requires finite SDR XYZ with Y in [0, 1]");
        return new HCTColor();
    }

    Double3 xyz100 = new Double3(xyz.X * 100.0, xyz.Y * 100.0, xyz.Z * 100.0);
    Double3 scaled = MultiplyRows(_scaled_discount_from_xyz_rows, xyz100);
    if (!IsFinite(scaled))
    {
        GuiAssert.Verify(false ,"HCTColor.FromXYZ overflowed the fixed CAM16 transform");
        return new HCTColor();
    }
    return FromScaledDiscount(scaled, xyz100.Y);
    }
    // source line 498
    public static HCTColor FromLinear(Double3 linear)
    {
if (!IsFinite(linear))
    {
        GuiAssert.Verify(false ,"HCTColor.FromLinear requires finite RGB components");
        return new HCTColor();
    }

    Double3 linear100 = new Double3(linear.X * 100.0,
        linear.Y * 100.0,
        linear.Z * 100.0);
    double y = _y_from_linear.X * linear100.X +
        _y_from_linear.Y * linear100.Y + _y_from_linear.Z * linear100.Z;
    if (y < 0.0 || y > 100.0)
    {
        GuiAssert.Verify(false ,"HCTColor.FromLinear requires SDR luminance Y in [0, 1]");
        return new HCTColor();
    }

    Double3 scaled = MultiplyRows(_scaled_discount_from_linear_rows, linear100);
    return FromScaledDiscount(scaled, y);
    }
    // source line 522
    public static HCTColor FromSRGB(SRGBColor color)
    {
if (!double.IsFinite(color.R) || !double.IsFinite(color.G) || !double.IsFinite(color.B) ||
        color.R < 0.0f || color.R > 1.0f ||
        color.G < 0.0f || color.G > 1.0f ||
        color.B < 0.0f || color.B > 1.0f)
    {
        GuiAssert.Verify(false ,"HCTColor.FromSRGB requires encoded RGB in [0, 1]");
        return new HCTColor();
    }

    return FromLinear(new Double3(LinearizeSrgb((double)(color.R)),
        LinearizeSrgb((double)(color.G)),
        LinearizeSrgb((double)(color.B))));
    }
    // source line 541
    public static bool SolveExact(HCTColor hct,ref Double3 scaled,ref Double3 linear)
    {
double y = YFromLstar(hct.Tone);
    if (hct.Tone <= 0.0001 || hct.Tone >= 99.9999 || hct.Chroma < 0.0001)
    {
        linear = new Double3(y, y, y);
        scaled = MultiplyRows(_scaled_discount_from_linear_rows, linear);
        return true;
    }

    double hue_radians = hct.Hue * _degrees_to_radians;
    return FindExactByJ(hue_radians,hct.Chroma,y,ref scaled,ref linear);
    }
    // source line 558
    public static Double3 SolveContinuousSrgb(HCTColor hct)
    {
Double3 scaled = new();
    Double3 linear = new();
    if (SolveExact(hct,ref scaled,ref linear))
    {
        double kGamutTolerance = 1.0e-7;
        if (linear.X >= -kGamutTolerance && linear.X <= 100.0 + kGamutTolerance &&
            linear.Y >= -kGamutTolerance && linear.Y <= 100.0 + kGamutTolerance &&
            linear.Z >= -kGamutTolerance && linear.Z <= 100.0 + kGamutTolerance)
        {
            return new Double3(CppMath.Clamp(linear.X, 0.0, 100.0),
                CppMath.Clamp(linear.Y, 0.0, 100.0),
                CppMath.Clamp(linear.Z, 0.0, 100.0));
        }
    }

    double y          = YFromLstar(hct.Tone);
    double target_hue = hct.Hue * _degrees_to_radians;
    Double3 compressed = new();
    if (!BisectToLimitContinuous(y,target_hue,ref compressed))
    {
        GuiAssert.Verify(false ,"continuous HCT gamut boundary solve did not converge");
        return new Double3();
    }
    return compressed;
    }
    // source line 587
    public static uint SolveSrgb8(HCTColor hct)
    {
double hue    = SanitizeDegrees(hct.Hue);
    double chroma = hct.Chroma;
    double lstar  = hct.Tone;
    if (chroma < 0.0001 || lstar < 0.0001 || lstar > 99.9999)
    {
        return ArgbFromLstar(lstar);
    }

    double   hue_radians = hue * _degrees_to_radians;
    double   y           = YFromLstar(lstar);
    uint exact       = FindSrgb8ByJ(hue_radians, chroma, y);
    if (exact != 0)
    {
        return exact;
    }
    Double3 boundary = new();
    if (!BisectToLimitSrgb8(y,hue_radians,ref boundary))
    {
        GuiAssert.Verify(false ,"8-bit HCT gamut boundary solve did not converge");
        return 0xff000000u;
    }
    return ArgbFromLinear(boundary);
    }
    // source line 612
    public static Double3 XyzFromScaled(Double3 scaled)
    {
return MultiplyRows(_xyz_from_scaled_discount_rows, scaled);
    }
    // source line 616
    public static SRGBColor EncodeSrgb(Double3 linear)
    {
return new SRGBColor((float)(EncodeSrgb(linear.X)),
        (float)(EncodeSrgb(linear.Y)),
        (float)(EncodeSrgb(linear.Z)),
        1.0f);
    }
    // source line 627
    public static bool IsValid(HCTColor hct)
    {
return double.IsFinite(hct.Hue) && double.IsFinite(hct.Chroma) && double.IsFinite(hct.Tone) &&
        hct.Hue >= 0.0 && hct.Hue < 360.0 && hct.Chroma >= 0.0 &&
        hct.Tone >= 0.0 && hct.Tone <= 100.0;
    }
    // source line 635
    public static bool ValidateMatrices()
    {
double kTolerance = 5.0e-15;
    if (!AreInverse(
            _scaled_discount_from_linear_rows,
            _linear_from_scaled_discount_rows,
            kTolerance
        ) ||
        !AreInverse(
            _scaled_discount_from_xyz_rows,
            _xyz_from_scaled_discount_rows,
            kTolerance
        ))
    {
        return false;
    }

    Double3[] kExpectedXYZFromLinear = new Double3[]{
        new Double3(0.41233895, 0.35762064, 0.18051042),
        new Double3(0.2126, 0.7152, 0.0722),
        new Double3(0.01932141, 0.11916382, 0.95034478),
    };
    for (uint row = 0; row < 3; ++row)
    {
        for (uint column = 0; column < 3; ++column)
        {
            double actual = MatrixElement(
                _xyz_from_scaled_discount_rows,
                _scaled_discount_from_linear_rows,
                row,
                column
            );
            double expected = Component(kExpectedXYZFromLinear[row], column);
            double error    = actual > expected ? actual - expected : expected - actual;
            if (error > kTolerance)
            {
                return false;
            }
        }
    }
    return true;
    }
    // source line 679
    public static double Component(Double3 value,uint index)
    {
return index == 0 ? value.X : index == 1 ? value.Y :
                                               value.Z;
    }
    // source line 687
    public static double MatrixElement(Double3[] lhs,Double3[] rhs,uint row,uint column)
    {
return Component(lhs[row], 0) * Component(rhs[0], column) +
        Component(lhs[row], 1) * Component(rhs[1], column) +
        Component(lhs[row], 2) * Component(rhs[2], column);
    }
    // source line 698
    public static bool AreInverse(Double3[] lhs,Double3[] rhs,double tolerance)
    {
for (uint direction = 0; direction < 2; ++direction)
    {
        for (uint row = 0; row < 3; ++row)
        {
            for (uint column = 0; column < 3; ++column)
            {
                double actual   = direction == 0 ?
                      MatrixElement(lhs, rhs, row, column) :
                      MatrixElement(rhs, lhs, row, column);
                double expected = row == column ? 1.0 : 0.0;
                double error    = actual > expected ? actual - expected : expected - actual;
                if (error > tolerance)
                {
                    return false;
                }
            }
        }
    }
    return true;
    }
    // source line 724
    public static Double3 MultiplyRows(Double3[] rows,Double3 value)
    {
return new Double3(rows[0].X * value.X + rows[0].Y * value.Y + rows[0].Z * value.Z,
        rows[1].X * value.X + rows[1].Y * value.Y + rows[1].Z * value.Z,
        rows[2].X * value.X + rows[2].Y * value.Y + rows[2].Z * value.Z);
    }
    // source line 737
    public static bool IsFinite(Double3 value)
    {
return double.IsFinite(value.X) && double.IsFinite(value.Y) && double.IsFinite(value.Z);
    }
    // source line 741
    public static double SanitizeDegrees(double degrees)
    {
double normalized = CppMath.Fmod(degrees, 360.0);
    return normalized < 0.0 ? normalized + 360.0 : normalized;
    }
    // source line 746
    public static double SanitizeRadians(double radians)
    {
return CppMath.Fmod(radians + _pi * 8.0, _pi * 2.0);
    }
    // source line 750
    public static bool IsOppositeSign(double lhs,double rhs)
    {
return (lhs < 0.0 && rhs > 0.0) || (lhs > 0.0 && rhs < 0.0);
    }
    // source line 754
    public static double ChromaticAdaptation(double component)
    {
double af = Math.Pow(Math.Abs(component), 0.42);
    return Math.CopySign(400.0 * af / (af + 27.13), component);
    }
    // source line 759
    public static double InverseChromaticAdaptation(double adapted)
    {
double adapted_abs = Math.Abs(adapted);
    double @base        = CppMath.Max(0.0, 27.13 * adapted_abs / (400.0 - adapted_abs));
    return Math.CopySign(Math.Pow(@base, 1.0 / 0.42), adapted);
    }
    // source line 765
    public static double YFromLstar(double lstar)
    {
if (lstar > 8.0)
    {
        double root = (lstar + 16.0) / 116.0;
        return root * root * root * 100.0;
    }
    return lstar / (24389.0 / 27.0) * 100.0;
    }
    // source line 774
    public static double LstarFromY(double y)
    {
if (y <= 0.0)
    {
        return 0.0;
    }

    double kEpsilon   = 216.0 / 24389.0;
    double     normalized = y / 100.0;
    if (normalized <= kEpsilon)
    {
        return (24389.0 / 27.0) * normalized;
    }
    return 116.0 * Math.Pow(normalized, 1.0 / 3.0) - 16.0;
    }
    // source line 789
    public static double LinearizeSrgb(double encoded)
    {
if (encoded <= 0.040449936)
    {
        return encoded / 12.92;
    }
    return Math.Pow((encoded + 0.055) / 1.055, 2.4);
    }
    // source line 797
    public static double EncodeSrgb(double linear100)
    {
double normalized = CppMath.Clamp(linear100 / 100.0, 0.0, 1.0);
    if (normalized <= 0.0031308)
    {
        return normalized * 12.92;
    }
    return 1.055 * Math.Pow(normalized, 1.0 / 2.4) - 0.055;
    }
    // source line 806
    public static int DelinearizedByte(double linear100)
    {
double encoded = EncodeSrgb(linear100);
    return CppMath.Clamp((int)(HctMath.Round(encoded * 255.0)), 0, 255);
    }
    // source line 811
    public static uint ArgbFromLinear(Double3 linear)
    {
uint r = (uint)(DelinearizedByte(linear.X));
    uint g = (uint)(DelinearizedByte(linear.Y));
    uint b = (uint)(DelinearizedByte(linear.Z));
    return 0xff000000u | (r << 16) | (g << 8) | b;
    }
    // source line 818
    public static uint ArgbFromLstar(double lstar)
    {
uint component = (uint)(
        DelinearizedByte(YFromLstar(lstar))
    );
    return 0xff000000u | (component << 16) | (component << 8) | component;
    }
    // source line 827
    public static HCTColor FromScaledDiscount(Double3 scaled,double y)
    {
if (y == 0.0)
    {
        return new HCTColor();
    }
    if (y < 0.0 || !IsFinite(scaled))
    {
        GuiAssert.Verify(false ,"XYZ/Linear input is outside the supported HCT appearance domain");
        return new HCTColor();
    }

    double r_a = ChromaticAdaptation(scaled.X);
    double g_a = ChromaticAdaptation(scaled.Y);
    double b_a = ChromaticAdaptation(scaled.Z);

    double a  = (11.0 * r_a - 12.0 * g_a + b_a) / 11.0;
    double b  = (r_a + g_a - 2.0 * b_a) / 9.0;
    double u  = (20.0 * r_a + 20.0 * g_a + 21.0 * b_a) / 20.0;
    double p2 = (40.0 * r_a + 20.0 * g_a + b_a) / 20.0;

    double hue_radians = Math.Atan2(b, a);
    double hue         = SanitizeDegrees(hue_radians * _radians_to_degrees);
    double ac          = p2 * _nbb;
    if (!(ac > 0.0) || !double.IsFinite(ac))
    {
        GuiAssert.Verify(false ,"XYZ/Linear input is outside the supported HCT appearance domain");
        return new HCTColor();
    }

    double j         = 100.0 * Math.Pow(ac / _aw, _c * _z);
    double hue_prime = hue < 20.14 ? hue + 360.0 : hue;
    double e_hue     = 0.25 * (Math.Cos(hue_prime * _degrees_to_radians + 2.0) + 3.8);
    double p1        = (50000.0 / 13.0) * e_hue * _nc * _ncb;
    double temporary = p1 * Math.Sqrt(a * a + b * b) / (u + 0.305);
    double alpha     = Math.Pow(CppMath.Max(0.0, temporary), 0.9) * _chroma_base;
    double chroma    = alpha * Math.Sqrt(j / 100.0);
    double tone      = LstarFromY(y);
    if (!double.IsFinite(hue) || !double.IsFinite(chroma) || !double.IsFinite(tone))
    {
        GuiAssert.Verify(false ,"XYZ/Linear input could not be represented as finite HCT");
        return new HCTColor();
    }

    return new HCTColor(hue, chroma, tone);
    }
    // source line 876
    public static bool ScaledFromJ(double hue_radians,double chroma,double j,ref Double3 scaled,bool require_invertible_response)
    {
if (!(j > 0.0) || !double.IsFinite(j))
    {
        return false;
    }

    double j_normalized = j / 100.0;
    double alpha        = chroma == 0.0 ? 0.0 : chroma / Math.Sqrt(j_normalized);
    double temporary    = Math.Pow(alpha * _t_inner_coeff, 1.0 / 0.9);
    double ac           = _aw * Math.Pow(j_normalized, 1.0 / _c / _z);
    double p2           = ac / _nbb;
    double e_hue        = 0.25 * (Math.Cos(hue_radians + 2.0) + 3.8);
    double p1           = e_hue * (50000.0 / 13.0) * _nc * _ncb;
    double h_sin        = Math.Sin(hue_radians);
    double h_cos        = Math.Cos(hue_radians);
    double denominator  = 23.0 * p1 + 11.0 * temporary * h_cos +
        108.0 * temporary * h_sin;
    if (denominator == 0.0 || !double.IsFinite(denominator))
    {
        return false;
    }

    double gamma = 23.0 * (p2 + 0.305) * temporary / denominator;
    double a     = gamma * h_cos;
    double b     = gamma * h_sin;
    double r_a   = (460.0 * p2 + 451.0 * a + 288.0 * b) / 1403.0;
    double g_a   = (460.0 * p2 - 891.0 * a - 261.0 * b) / 1403.0;
    double b_a   = (460.0 * p2 - 220.0 * a - 6300.0 * b) / 1403.0;
    if (require_invertible_response &&
        (Math.Abs(r_a) >= 400.0 || Math.Abs(g_a) >= 400.0 || Math.Abs(b_a) >= 400.0))
    {
        return false;
    }
    scaled = new Double3(InverseChromaticAdaptation(r_a),
        InverseChromaticAdaptation(g_a),
        InverseChromaticAdaptation(b_a));
    return IsFinite(scaled);
    }
    // source line 923
    public static bool EvaluateJ(double hue_radians,double chroma,double j,ref Double3 scaled,ref Double3 linear,ref double actual_y)
    {
if (!ScaledFromJ(hue_radians,chroma,j,ref scaled,true))
    {
        return false;
    }

    linear   = MultiplyRows(_linear_from_scaled_discount_rows, scaled);
    actual_y = _y_from_linear.X * linear.X +
        _y_from_linear.Y * linear.Y + _y_from_linear.Z * linear.Z;
    if (!double.IsFinite(actual_y))
    {
        return false;
    }
    return true;
    }
    // source line 946
    public static bool BisectJ(double hue_radians,double chroma,double target_y,double y_tolerance,double left_j,double left_residual,double right_j,double right_residual,ref Double3 scaled,ref Double3 linear)
    {
if (left_j > right_j)
    {
        (left_j,right_j)=(right_j,left_j);
        (left_residual,right_residual)=(right_residual,left_residual);
    }
    if (!IsOppositeSign(left_residual, right_residual))
    {
        return false;
    }

    uint kMaxBisectionIterations = 80;
    for (uint iteration = 0; iteration < kMaxBisectionIterations; ++iteration)
    {
        double mid_j = left_j + (right_j - left_j) * 0.5;
        if (mid_j == left_j || mid_j == right_j)
        {
            return false;
        }

        double mid_y = new();
        if (!EvaluateJ(hue_radians,chroma,mid_j,ref scaled,ref linear,ref mid_y))
        {

            return false;
        }
        double mid_residual = mid_y - target_y;
        if (Math.Abs(mid_residual) <= y_tolerance)
        {
            return true;
        }

        if (IsOppositeSign(left_residual, mid_residual))
        {
            right_j = mid_j;
        }
        else
        {
            left_j        = mid_j;
            left_residual = mid_residual;
        }
    }
    return false;
    }
    // source line 1009
    public static bool FindExactByJ(double hue_radians,double chroma,double y,ref Double3 scaled,ref Double3 linear)
    {
double j_initial   = Math.Sqrt(y) * 11.0;
    double y_tolerance = CppMath.Max(1.0e-12, y * 1.0e-11);

    double             initial_residual     = 0.0;
    bool               initial_valid        = false;
    double             previous_j           = 0.0;
    double             previous_residual    = 0.0;
    bool               has_previous         = false;
    double             j                    = j_initial;
    uint kMaxNewtonIterations = 32;
    for (uint iteration = 0; iteration < kMaxNewtonIterations; ++iteration)
    {
        double actual_y = new();
        if (!EvaluateJ(hue_radians,chroma,j,ref scaled,ref linear,ref actual_y))
        {
            break;
        }
        double residual = actual_y - y;
        if (iteration == 0)
        {
            initial_residual = residual;
            initial_valid    = true;
        }
        if (Math.Abs(residual) <= y_tolerance)
        {
            return true;
        }
        if (has_previous && IsOppositeSign(previous_residual, residual) &&
            BisectJ(hue_radians,chroma,y,y_tolerance,previous_j,previous_residual,j,residual,ref scaled,ref linear))
        {
            return true;
        }

        if (!(actual_y > 0.0))
        {
            break;
        }
        double next_j = j - residual * j / (2.0 * actual_y);
        if (!(next_j > 0.0) || !double.IsFinite(next_j) || next_j == j)
        {
            break;
        }

        previous_j        = j;
        previous_residual = residual;
        has_previous      = true;
        j                 = next_j;
    }




    uint kMaxSearchLayers  = 32;
    double[] kSearchFactors = new double[]{ 0.5, 2.0 };
    double[] search_j = new double[]{ j_initial, j_initial };
    double[] search_residual = new double[]{ initial_residual, initial_residual };
    bool[] search_valid = new bool[]{ initial_valid, initial_valid };
    for (uint layer = 0; layer < kMaxSearchLayers; ++layer)
    {
        for (uint direction = 0; direction < 2; ++direction)
        {
            double next_j = search_j[direction] * kSearchFactors[direction];
            double next_y = new();
            bool   next_valid = EvaluateJ(hue_radians,chroma,next_j,ref scaled,ref linear,ref next_y);
            double next_residual = next_valid ? next_y - y : 0.0;
            if (next_valid && Math.Abs(next_residual) <= y_tolerance)
            {
                return true;
            }
            if (search_valid[direction] && next_valid &&
                IsOppositeSign(search_residual[direction], next_residual) &&
                BisectJ(hue_radians,chroma,y,y_tolerance,search_j[direction],search_residual[direction],next_j,next_residual,ref scaled,ref linear))
            {
                return true;
            }
            search_j[direction]        = next_j;
            search_residual[direction] = next_residual;
            search_valid[direction]    = next_valid;
        }
    }
    return false;
    }
    // source line 1138
    public static uint FindSrgb8ByJ(double hue_radians,double chroma,double y)
    {
double j = Math.Sqrt(y) * 11.0;
    for (uint iteration = 0; iteration < 5; ++iteration)
    {
        Double3 scaled = new();
        if (!ScaledFromJ(hue_radians,chroma,j,ref scaled,false))
        {
            return 0;
        }
        Double3 linear = MultiplyRows(_linear_from_scaled_discount_rows, scaled);
        if (linear.X < 0.0 || linear.Y < 0.0 || linear.Z < 0.0)
        {
            return 0;
        }

        double actual_y = _y_from_linear.X * linear.X +
            _y_from_linear.Y * linear.Y + _y_from_linear.Z * linear.Z;
        if (!(actual_y > 0.0))
        {
            return 0;
        }
        if (iteration == 4 || Math.Abs(actual_y - y) < 0.002)
        {
            if (linear.X > 100.01 || linear.Y > 100.01 || linear.Z > 100.01)
            {
                return 0;
            }
            return ArgbFromLinear(linear);
        }
        j -= (actual_y - y) * j / (2.0 * actual_y);
    }
    return 0;
    }
    // source line 1178
    public static double HueOf(Double3 linear)
    {
Double3 scaled = MultiplyRows(_scaled_discount_from_linear_rows, linear);
    double  r_a    = ChromaticAdaptation(scaled.X);
    double  g_a    = ChromaticAdaptation(scaled.Y);
    double  b_a    = ChromaticAdaptation(scaled.Z);
    double  a      = (11.0 * r_a - 12.0 * g_a + b_a) / 11.0;
    double  b      = (r_a + g_a - 2.0 * b_a) / 9.0;
    return Math.Atan2(b, a);
    }
    // source line 1188
    public static bool AreInCyclicOrder(double a,double b,double c)
    {
return SanitizeRadians(b - a) < SanitizeRadians(c - a);
    }
    // source line 1192
    public static double Axis(Double3 value,uint axis)
    {
switch (axis)
    {
    case 0:
        return value.X;
    case 1:
        return value.Y;
    case 2:
        return value.Z;
    default:
        GuiAssert.Require(false,"Unreachable HCT branch");
        return 0.0;
    }
    }
    // source line 1207
    public static Double3 SetCoordinate(Double3 source,double coordinate,Double3 target,uint axis)
    {
double source_coordinate = Axis(source, axis);
    double amount            = (coordinate - source_coordinate) /
        (Axis(target, axis) - source_coordinate);
    return new Double3(source.X + (target.X - source.X) * amount,
        source.Y + (target.Y - source.Y) * amount,
        source.Z + (target.Z - source.Z) * amount);
    }
    // source line 1223
    public static bool IsBounded(double value)
    {
return value >= 0.0 && value <= 100.0;
    }
    // source line 1227
    public static Double3 NthVertex(double y,uint index)
    {
double coordinate_a = index % 4 <= 1 ? 0.0 : 100.0;
    double coordinate_b = index % 2 == 0 ? 0.0 : 100.0;
    if (index < 4)
    {
        double g = coordinate_a;
        double b = coordinate_b;
        double r = (y - g * _y_from_linear.Y - b * _y_from_linear.Z) /
            _y_from_linear.X;
        return IsBounded(r) ? new Double3(r, g, b) : new Double3(-1.0);
    }
    if (index < 8)
    {
        double b = coordinate_a;
        double r = coordinate_b;
        double g = (y - r * _y_from_linear.X - b * _y_from_linear.Z) /
            _y_from_linear.Y;
        return IsBounded(g) ? new Double3(r, g, b) : new Double3(-1.0);
    }

    double final_r = coordinate_a;
    double final_g = coordinate_b;
    double final_b = (y - final_r * _y_from_linear.X - final_g * _y_from_linear.Y) /
        _y_from_linear.Z;
    return IsBounded(final_b) ? new Double3(final_r, final_g, final_b) : new Double3(-1.0);
    }
    // source line 1254
    public static bool BisectToSegment(double y,double target_hue,ref Double3 left,ref Double3 right)
    {
double left_hue    = 0.0;
    double right_hue   = 0.0;
    bool   initialized = false;
    bool   uncut       = true;
    for (uint index = 0; index < 12; ++index)
    {
        Double3 mid = NthVertex(y, index);
        if (mid.X < 0.0)
        {
            continue;
        }

        double mid_hue = HueOf(mid);
        if (!initialized)
        {
            left        = mid;
            right       = mid;
            left_hue    = mid_hue;
            right_hue   = mid_hue;
            initialized = true;
            continue;
        }
        if (uncut || AreInCyclicOrder(left_hue, mid_hue, right_hue))
        {
            uncut = false;
            if (AreInCyclicOrder(left_hue, target_hue, mid_hue))
            {
                right     = mid;
                right_hue = mid_hue;
            }
            else
            {
                left     = mid;
                left_hue = mid_hue;
            }
        }
    }
    return initialized;
    }
    // source line 1300
    public static bool BisectToLimitSrgb8(double y,double target_hue,ref Double3 result)
    {
Double3 left = new();
    Double3 right = new();
    if (!BisectToSegment(y,target_hue,ref left,ref right))
    {
        return false;
    }

    double left_hue = HueOf(left);
    for (uint axis = 0; axis < 3; ++axis)
    {
        double left_axis  = Axis(left, axis);
        double right_axis = Axis(right, axis);
        if (left_axis == right_axis)
        {
            continue;
        }

        int      left_plane    = -1;
        int      right_plane   = 255;
        double left_encoded  = EncodeSrgb(left_axis) * 255.0;
        double right_encoded = EncodeSrgb(right_axis) * 255.0;
        if (left_axis < right_axis)
        {
            left_plane  = (int)(Math.Floor(left_encoded - 0.5));
            right_plane = (int)(Math.Ceiling(right_encoded - 0.5));
        }
        else
        {
            left_plane  = (int)(Math.Ceiling(left_encoded - 0.5));
            right_plane = (int)(Math.Floor(right_encoded - 0.5));
        }

        for (uint iteration = 0; iteration < 8; ++iteration)
        {
            if (Math.Abs(right_plane - left_plane) <= 1)
            {
                break;
            }
            int mid_plane = (int)(
                Math.Floor((left_plane + right_plane) / 2.0)
            );
            Double3 mid = SetCoordinate(
                left,
                _critical_planes[mid_plane],
                right,
                axis
            );
            double mid_hue = HueOf(mid);
            if (AreInCyclicOrder(left_hue, target_hue, mid_hue))
            {
                right       = mid;
                right_plane = mid_plane;
            }
            else
            {
                left       = mid;
                left_hue   = mid_hue;
                left_plane = mid_plane;
            }
        }
    }
    result = new Double3((left.X + right.X) * 0.5,
        (left.Y + right.Y) * 0.5,
        (left.Z + right.Z) * 0.5);
    return true;
    }
    // source line 1374
    public static bool BisectToLimitContinuous(double y,double target_hue,ref Double3 result)
    {
Double3 left = new();
    Double3 right = new();
    if (!BisectToSegment(y,target_hue,ref left,ref right))
    {
        return false;
    }

    double left_hue = HueOf(left);
    for (uint iteration = 0; iteration < 32; ++iteration)
    {
        Double3 mid = new Double3((left.X + right.X) * 0.5,
            (left.Y + right.Y) * 0.5,
            (left.Z + right.Z) * 0.5);
        double mid_hue = HueOf(mid);
        if (AreInCyclicOrder(left_hue, target_hue, mid_hue))
        {
            right = mid;
        }
        else
        {
            left     = mid;
            left_hue = mid_hue;
        }
    }
    result = new Double3((left.X + right.X) * 0.5,
        (left.Y + right.Y) * 0.5,
        (left.Z + right.Z) * 0.5);
    return true;
    }
}
