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
public partial struct HCTColor
{
    // source line 1428
    public static HCTColor FromXYZ(Double3 xyz)
    {
return HCTColorAlgorithm.FromXYZ(xyz);
    }
    // source line 1432
    public static HCTColor FromLinear(double r,double g,double b)
    {
return HCTColorAlgorithm.FromLinear(new Double3(r, g, b));
    }
    // source line 1436
    public static HCTColor FromLinear(Double3 rgb)
    {
return FromLinear(rgb.X, rgb.Y, rgb.Z);
    }
    // source line 1440
    public static HCTColor FromLinear(Vector3 rgb)
    {
return FromLinear(rgb.X, rgb.Y, rgb.Z);
    }
    // source line 1444
    public static HCTColor FromSRGB(SRGBColor color)
    {
return HCTColorAlgorithm.FromSRGB(color);
    }
    // source line 1450
    public Double3 ToXyz()
    {
if (!HCTColorAlgorithm.IsValid(this))
    {
        GuiAssert.Verify(false ,"HCTColor must contain finite normalized components");
        return new Double3();
    }

    Double3 scaled = new();
    Double3 linear = new();
    if (!HCTColorAlgorithm.SolveExact(this,ref scaled,ref linear))
    {
        GuiAssert.Verify(false ,"exact HCT to XYZ solve did not converge");
        return new Double3();
    }

    Double3 xyz100 = HCTColorAlgorithm.XyzFromScaled(scaled);
    return new Double3(xyz100.X / 100.0, xyz100.Y / 100.0, xyz100.Z / 100.0);
    }
    // source line 1490
    public SRGBColor ToSrgb()
    {
if (!HCTColorAlgorithm.IsValid(this))
    {
        GuiAssert.Verify(false ,"HCTColor must contain finite normalized components");
        return new SRGBColor();
    }
    return HCTColorAlgorithm.EncodeSrgb(
        HCTColorAlgorithm.SolveContinuousSrgb(this)
    );
    }
    // source line 1501
    public SRGBColor ToSrgb8()
    {
if (!HCTColorAlgorithm.IsValid(this))
    {
        GuiAssert.Verify(false ,"HCTColor must contain finite normalized components");
        return new SRGBColor();
    }
    return SRGBColor.FromARGB32(HCTColorAlgorithm.SolveSrgb8(this));
    }
}
