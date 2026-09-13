using SkrGui;
using static SkrGui.Tests.OriginalWidgetFixtures;
using static SkrGui.Tests.OriginalMathHelpers;
using static SkrGui.Tests.OriginalWidgetSurfaceHelpers;
namespace SkrGui.Tests;
internal static class WidgetSurfaceTests
{
private static TempText MakeTextWidget(long key, string text){
    TempText result = new TempText();
    result.Key = new Key(key);
    result.Text = Identity(text);
    return result;
}
private static PlacementLayout MakeComplexWidgetTree(bool updated){
    return Gui.Widget<PlacementLayout>(w0 => {
        w0.WidthRule = updated ? VisualPlacementAxisSize.Sized(640.0f) : VisualPlacementAxisSize.Expand();
        w0.HeightRule = updated ? VisualPlacementAxisSize.Shrink() : VisualPlacementAxisSize.Expand();
        w0.ClipBehavior = updated ? EClipBehavior.HardEdge : EClipBehavior.None;
        w0.Children.Add(Gui.Widget<PlacementSlot>(w1 => {
            w1.Placement = updated ? Placement.Align(
                                         Alignment.Center(),
                                         new Sizef(300.0f, 200.0f)
                                     ) :
                                     Placement.Fill();
            w1.SizeMode = updated ? EPlacementSizeMode.Loose : EPlacementSizeMode.Tight;
            w1.Child = Gui.Widget<Stack>(w2 => {
                w2.Alignment = updated ? new AlignmentMixed(Alignment.Center()) : new AlignmentMixed(AlignmentDirectional.TopStart());
                w2.Fit = updated ? EStackFit.Expand : EStackFit.Loose;
                w2.Children.Add(Gui.Widget<Positioned>(w3 => {
                    if (updated)
                    {
                        w3.Left = 12.0f;
                        w3.Top = 18.0f;
                        w3.Width = 240.0f;
                        w3.Height = 160.0f;
                    }
                    w3.Child = Gui.Widget<Grid>(w4 => {
                        w4.ColumnGap = updated ? 7.0f : 0.0f;
                        w4.RowGap = updated ? 9.0f : 0.0f;
                        w4.Children.Add(Gui.Widget<GridSlot>(w5 => {
                            w5.RowStart = updated ? 2u : 0u;
                            w5.ColumnStart = updated ? 3u : 0u;
                            w5.RowSpan = updated ? 2u : 1u;
                            w5.ColumnSpan = updated ? 2u : 1u;
                            w5.Child = Gui.Widget<Column>(w6 => {
                                w6.Spacing = updated ? 11.0f : 0.0f;
                                w6.Children.Add(Gui.Widget<Expanded>(w7 => {
                                    w7.Flex = updated ? 4 : 1;
                                    w7.Child = Gui.Widget<Row>(w8 => {
                                        w8.Spacing = updated ? 13.0f : 0.0f;
                                        w8.Children.Add(Gui.Widget<Flexible>(w9 => {
                                            w9.Flex = updated ? 5 : 1;
                                            w9.Child = Gui.Widget<Padding>(w10 => {
                                                w10.PaddingValue = updated ? EdgeInsets.All(16.0f) : EdgeInsets.Zero();
                                                w10.Child = Gui.Widget<TempText>(w11 => {
                                                    w11.Text = updated ? "updated" : "initial";
                                                });
                                            });
                                        }));
                                    });
                                }));
                            });
                        }));
                    });
                }));
            });
        }));
    });
}
[GuiTest("widgets/widget_surface_tests.cpp::gui/widgets/canonical-map-and-update")]
public static void Case0(){

{
        CheckVisualWidget<TempText, NexusVisualLeaf, VisualTempText>(
            (TempText widget) => {
                widget.Text = "updated";
                widget.TextStyle.FontFamilies = "Test, Fallback";
                widget.TextStyle.FontSize = 24.0f;
                widget.TextStyle.FontWeight = EFontWeight.Bold;
                widget.TextStyle.FontStyle = EFontStyle.Italic;
                widget.TextStyle.FontStretch = EFontStretch.Expanded;
                widget.Color = new SRGBColor(
                    0.1f,
                    0.2f,
                    0.3f,
                    0.4f
                );
                widget.TextAlign = EVisualTempTextAlign.End;
                widget.TextDirection = ETextDirectionMode.RTL;
                widget.SoftWrap = false;
                widget.Overflow = ETextOverflow.Ellipsis;
                widget.TextScaleFactor = 1.5f;
                widget.MaxLines = 3u;
                widget.TabStops.Add(48.0f);
                widget.EllipsisCodepoint = 0x2026u;
            },
            (VisualTempText visual) => {
                Expect(visual.Text().IsEmpty());
                Expect(
                    visual.TextStyle().FontFamilies.IsEmpty()
                );
                Equal(visual.TextStyle().FontSize, 14.0f);
                Equal(
                    visual.TextStyle().FontWeight,
                    EFontWeight.Regular
                );
                Equal(
                    visual.TextStyle().FontStyle,
                    EFontStyle.Normal
                );
                Equal(
                    visual.TextStyle().FontStretch,
                    EFontStretch.Normal
                );
                Expect(
                    visual.Color() ==
                    new SRGBColor(1.0f, 1.0f, 1.0f, 1.0f)
                );
                Equal(
                    visual.TextAlign(),
                    EVisualTempTextAlign.Start
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirectionMode.Auto
                );
                Expect(visual.SoftWrap());
                Equal(
                    visual.Overflow(),
                    ETextOverflow.Clip
                );
                Equal(visual.TextScaleFactor(), 1.0f);
                ExpectFalse(visual.MaxLines().HasValue);
                Expect(visual.TabStops().IsEmpty());
                Equal(
                    visual.EllipsisCodepoint(),
                    TextDefaults.EllipsisCodepoint
                );
            },
            (VisualTempText visual) => {
                Expect(visual.Text() == "updated");
                Expect(
                    visual.TextStyle().FontFamilies ==
                    "Test, Fallback"
                );
                Equal(visual.TextStyle().FontSize, 24.0f);
                Equal(
                    visual.TextStyle().FontWeight,
                    EFontWeight.Bold
                );
                Equal(
                    visual.TextStyle().FontStyle,
                    EFontStyle.Italic
                );
                Equal(
                    visual.TextStyle().FontStretch,
                    EFontStretch.Expanded
                );
                Expect(
                    visual.Color() ==
                    new SRGBColor(0.1f, 0.2f, 0.3f, 0.4f)
                );
                Equal(
                    visual.TextAlign(),
                    EVisualTempTextAlign.End
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirectionMode.RTL
                );
                ExpectFalse(visual.SoftWrap());
                Equal(
                    visual.Overflow(),
                    ETextOverflow.Ellipsis
                );
                Equal(visual.TextScaleFactor(), 1.5f);
                Expect(visual.MaxLines().HasValue);
                Equal(visual.MaxLines().Value, 3u);
                Equal(visual.TabStops().Size(), 1u);
                if (visual.TabStops().Size() == 1u)
                {
                    Equal(visual.TabStops()[0], 48.0f);
                }
                Equal(visual.EllipsisCodepoint(), 0x2026u);
            }
        );
    }


{
        CheckVisualWidget<AspectRatio, NexusVisualSingleChild, VisualAspectRatio>(
            (AspectRatio widget) => {
                widget.AspectRatioValue = 2.0f;
            },
            (VisualAspectRatio visual) => {
                Equal(visual.AspectRatio(), 1.0f);
            },
            (VisualAspectRatio visual) => {
                Equal(visual.AspectRatio(), 2.0f);
            }
        );
    }


{
        Rectf clip_rect = Rectf.LTWH(1.0f, 2.0f, 30.0f, 40.0f);
        CheckVisualWidget<ClipRect, NexusVisualSingleChild, VisualClipRect>(
            (ClipRect widget) => {
                widget.ClipBehavior = EClipBehavior.AntiAlias;
                widget.CustomClipRect = clip_rect;
            },
            (VisualClipRect visual) => {
                Equal(
                    visual.ClipBehavior(),
                    EClipBehavior.HardEdge
                );
                ExpectFalse(
                    visual.CustomClipRect().HasValue
                );
            },
            (VisualClipRect visual) => {
                Equal(
                    visual.ClipBehavior(),
                    EClipBehavior.AntiAlias
                );
                Expect(
                    visual.CustomClipRect().HasValue
                );
                Expect(
                    visual.CustomClipRect().Value == clip_rect
                );
            }
        );
    }


{
        BoxConstraints constraints = new BoxConstraints(1.0f, 100.0f, 2.0f, 80.0f);
        CheckVisualWidget<ConstrainedBox, NexusVisualSingleChild, VisualConstrained>(
            (ConstrainedBox widget) => {
                widget.AdditionalConstraints = constraints;
            },
            (VisualConstrained visual) => {
                Expect(
                    visual.AdditionalConstraints() == new BoxConstraints()
                );
            },
            (VisualConstrained visual) => {
                Expect(
                    visual.AdditionalConstraints() == constraints
                );
            }
        );
    }


{
        CheckVisualWidget<FittedBox, NexusVisualSingleChild, VisualFitted>(
            (FittedBox widget) => {
                widget.Fit = EBoxFit.Cover;
                widget.Alignment = Alignment.BottomRight();
                widget.TextDirection = ETextDirection.RTL;
                widget.ClipBehavior = EClipBehavior.HardEdge;
            },
            (VisualFitted visual) => {
                Equal(visual.Fit(), EBoxFit.Contain);
                Expect(
                    visual.Alignment() == Alignment.Center()
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirection.LTR
                );
                Equal(
                    visual.ClipBehavior(),
                    EClipBehavior.None
                );
            },
            (VisualFitted visual) => {
                Equal(visual.Fit(), EBoxFit.Cover);
                Expect(
                    visual.Alignment() == Alignment.BottomRight()
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirection.RTL
                );
                Equal(
                    visual.ClipBehavior(),
                    EClipBehavior.HardEdge
                );
            }
        );
    }


{
        CheckVisualWidget<IntrinsicHeight, NexusVisualSingleChild, VisualIntrinsicHeight>(
            (IntrinsicHeight unused) => {},
            (VisualIntrinsicHeight unused) => {},
            (VisualIntrinsicHeight unused) => {}
        );
    }


{
        CheckVisualWidget<IntrinsicWidth, NexusVisualSingleChild, VisualIntrinsicWidth>(
            (IntrinsicWidth widget) => {
                widget.StepWidth = 8.0f;
                widget.StepHeight = 12.0f;
            },
            (VisualIntrinsicWidth visual) => {
                ExpectFalse(visual.StepWidth().HasValue);
                ExpectFalse(visual.StepHeight().HasValue);
            },
            (VisualIntrinsicWidth visual) => {
                Expect(visual.StepWidth().HasValue);
                Expect(visual.StepHeight().HasValue);
                Equal(visual.StepWidth().Value, 8.0f);
                Equal(visual.StepHeight().Value, 12.0f);
            }
        );
    }


{
        CheckVisualWidget<LimitedBox, NexusVisualSingleChild, VisualLimited>(
            (LimitedBox widget) => {
                widget.MaxWidth = 100.0f;
                widget.MaxHeight = 200.0f;
            },
            (VisualLimited visual) => {
                Expect(double.IsInfinity(visual.MaxWidth()));
                Expect(double.IsInfinity(visual.MaxHeight()));
            },
            (VisualLimited visual) => {
                Equal(visual.MaxWidth(), 100.0f);
                Equal(visual.MaxHeight(), 200.0f);
            }
        );
    }


{
        CheckVisualWidget<Opacity, NexusVisualSingleChild, VisualOpacity>(
            (Opacity widget) => {
                widget.OpacityValue = 0.25f;
                widget.OpacityMode = EVisualOpacityMode.Override;
            },
            (VisualOpacity visual) => {
                Equal(visual.Opacity(), 1.0f);
                Equal(
                    visual.OpacityMode(),
                    EVisualOpacityMode.Multiply
                );
            },
            (VisualOpacity visual) => {
                Equal(visual.Opacity(), 0.25f);
                Equal(
                    visual.OpacityMode(),
                    EVisualOpacityMode.Override
                );
            }
        );
    }


{
        CheckVisualWidget<Transform, NexusVisualSingleChild, VisualTransform>(
            (Transform widget) => {
                widget.PaintTransform.ApplyOffset2D(
                    new Offsetf(3.0f, 4.0f)
                );
                widget.Origin = new Offsetf(5.0f, 6.0f);
                widget.Alignment = Alignment.BottomRight();
                widget.TextDirection = ETextDirection.RTL;
                widget.TransformHitTests = false;
            },
            (VisualTransform visual) => {
                ExpectFalse(visual.Transform().HasTransform());
                Expect(visual.Origin() == Offsetf.Zero());
                Expect(
                    visual.Alignment() == Alignment.TopLeft()
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirection.LTR
                );
                Expect(visual.TransformHitTests());
            },
            (VisualTransform visual) => {
                Expect(
                    visual.Transform().Offset() ==
                    new Offsetf(3.0f, 4.0f)
                );
                Expect(
                    visual.Origin() == new Offsetf(5.0f, 6.0f)
                );
                Expect(
                    visual.Alignment() == Alignment.BottomRight()
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirection.RTL
                );
                ExpectFalse(visual.TransformHitTests());
            }
        );
    }


{
        CheckVisualWidget<Visibility, NexusVisualSingleChild, VisualVisibility>(
            (Visibility widget) => {
                widget.VisibilityValue = false;
                widget.MaintainSize = true;
            },
            (VisualVisibility visual) => {
                Expect(visual.Visibility());
                ExpectFalse(visual.MaintainSize());
            },
            (VisualVisibility visual) => {
                ExpectFalse(visual.Visibility());
                Expect(visual.MaintainSize());
            }
        );
    }


{
        VisualBorderRadius radius =
            VisualBorderRadius.All(Radius.Circular(6.0f));
        EdgeInsets thickness =
            EdgeInsets.FromLTRB(1.0f, 2.0f, 3.0f, 4.0f);
        SRGBColor BorderColor = new SRGBColor(
            0.2f,
            0.3f,
            0.4f,
            1.0f
        );
        SRGBColor BackgroundColor = new SRGBColor(
            0.6f,
            0.7f,
            0.8f,
            0.9f
        );
        CheckVisualWidget<Border, NexusVisualSingleChild, VisualBorder>(
            (Border widget) => {
                widget.Radius = radius;
                widget.BorderThickness = thickness;
                widget.BorderColor = BorderColor;
                widget.BackgroundColor = BackgroundColor;
            },
            (VisualBorder visual) => {
                Expect(visual.Radius().IsZero());
                Expect(visual.BorderThickness().IsZero());
                Expect(
                    visual.BorderColor() ==
                    new SRGBColor(0.0f, 0.0f, 0.0f, 0.0f)
                );
                Expect(
                    visual.BackgroundColor() ==
                    new SRGBColor(0.0f, 0.0f, 0.0f, 0.0f)
                );
            },
            (VisualBorder visual) => {
                Expect(visual.Radius() == radius);
                Expect(
                    visual.BorderThickness() == thickness
                );
                Expect(
                    visual.BorderColor() == BorderColor
                );
                Expect(
                    visual.BackgroundColor() == BackgroundColor
                );
            }
        );
    }


{
        EdgeInsetsMixed padding =
            EdgeInsets.FromLTRB(1.0f, 2.0f, 3.0f, 4.0f);
        CheckVisualWidget<Padding, NexusVisualSingleChild, VisualPadding>(
            (Padding widget) => {
                widget.PaddingValue = padding;
                widget.TextDirection = ETextDirection.RTL;
            },
            (VisualPadding visual) => {
                Expect(
                    visual.Padding() == new EdgeInsetsMixed()
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirection.LTR
                );
            },
            (VisualPadding visual) => {
                Expect(visual.Padding() == padding);
                Equal(
                    visual.TextDirection(),
                    ETextDirection.RTL
                );
            }
        );
    }


{
        CheckVisualWidget<Align, NexusVisualSingleChild, VisualPositioned>(
            (Align widget) => {
                widget.Alignment = AlignmentDirectional.BottomEnd();
                widget.TextDirection = ETextDirection.RTL;
                widget.WidthFactor = 2.0f;
                widget.HeightFactor = 3.0f;
            },
            (VisualPositioned visual) => {
                Expect(
                    visual.Alignment() == Alignment.Center()
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirection.LTR
                );
                ExpectFalse(visual.WidthFactor().HasValue);
                ExpectFalse(visual.HeightFactor().HasValue);
            },
            (VisualPositioned visual) => {
                Expect(
                    visual.Alignment() ==
                    AlignmentDirectional.BottomEnd()
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirection.RTL
                );
                Expect(visual.WidthFactor().HasValue);
                Expect(visual.HeightFactor().HasValue);
                Equal(visual.WidthFactor().Value, 2.0f);
                Equal(visual.HeightFactor().Value, 3.0f);
            }
        );
    }
}
[GuiTest("widgets/widget_surface_tests.cpp::gui/widgets/canonical-multi-child-map-and-update")]
public static void Case1(){

{
        CheckVisualWidget<Flex, NexusVisualMultiChild, VisualFlex>(
            (Flex widget) => {
                widget.Direction = EAxis.Vertical;
                widget.MainAxisSize = EMainAxisSize.Min;
                widget.MainAxisAlignment =
                    EMainAxisAlignment.SpaceEvenly;
                widget.CrossAxisAlignment =
                    ECrossAxisAlignment.Baseline;
                widget.TextDirection = ETextDirection.RTL;
                widget.VerticalDirection = EVerticalDirection.Up;
                widget.TextBaseline = ETextBaseline.Ideographic;
                widget.ClipBehavior = EClipBehavior.HardEdge;
                widget.Spacing = 7.0f;
            },
            (VisualFlex visual) => {
                Equal(
                    visual.Direction(),
                    EAxis.Horizontal
                );
                Equal(
                    visual.MainAxisSize(),
                    EMainAxisSize.Max
                );
                Equal(
                    visual.MainAxisAlignment(),
                    EMainAxisAlignment.Start
                );
                Equal(
                    visual.CrossAxisAlignment(),
                    ECrossAxisAlignment.Center
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirection.LTR
                );
                Equal(
                    visual.VerticalDirection(),
                    EVerticalDirection.Down
                );
                ExpectFalse(visual.TextBaseline().HasValue);
                Equal(
                    visual.ClipBehavior(),
                    EClipBehavior.None
                );
                Equal(visual.Spacing(), 0.0f);
            },
            (VisualFlex visual) => {
                Equal(
                    visual.Direction(),
                    EAxis.Vertical
                );
                Equal(
                    visual.MainAxisSize(),
                    EMainAxisSize.Min
                );
                Equal(
                    visual.MainAxisAlignment(),
                    EMainAxisAlignment.SpaceEvenly
                );
                Equal(
                    visual.CrossAxisAlignment(),
                    ECrossAxisAlignment.Baseline
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirection.RTL
                );
                Equal(
                    visual.VerticalDirection(),
                    EVerticalDirection.Up
                );
                Expect(visual.TextBaseline().HasValue);
                Equal(
                    visual.TextBaseline().Value,
                    ETextBaseline.Ideographic
                );
                Equal(
                    visual.ClipBehavior(),
                    EClipBehavior.HardEdge
                );
                Equal(visual.Spacing(), 7.0f);
            }
        );
    }


{
        CheckVisualWidget<Wrap, NexusVisualMultiChild, VisualWrap>(
            (Wrap widget) => {
                widget.Direction = EAxis.Vertical;
                widget.Alignment = EWrapAlignment.SpaceAround;
                widget.Spacing = 3.0f;
                widget.RunAlignment = EWrapAlignment.SpaceEvenly;
                widget.RunSpacing = 5.0f;
                widget.CrossAxisAlignment =
                    EWrapCrossAlignment.End;
                widget.TextDirection = ETextDirection.RTL;
                widget.VerticalDirection = EVerticalDirection.Up;
                widget.ClipBehavior = EClipBehavior.HardEdge;
            },
            (VisualWrap visual) => {
                Equal(
                    visual.Direction(),
                    EAxis.Horizontal
                );
                Equal(
                    visual.Alignment(),
                    EWrapAlignment.Start
                );
                Equal(visual.Spacing(), 0.0f);
                Equal(
                    visual.RunAlignment(),
                    EWrapAlignment.Start
                );
                Equal(visual.RunSpacing(), 0.0f);
                Equal(
                    visual.CrossAxisAlignment(),
                    EWrapCrossAlignment.Start
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirection.LTR
                );
                Equal(
                    visual.VerticalDirection(),
                    EVerticalDirection.Down
                );
                Equal(
                    visual.ClipBehavior(),
                    EClipBehavior.None
                );
            },
            (VisualWrap visual) => {
                Equal(
                    visual.Direction(),
                    EAxis.Vertical
                );
                Equal(
                    visual.Alignment(),
                    EWrapAlignment.SpaceAround
                );
                Equal(visual.Spacing(), 3.0f);
                Equal(
                    visual.RunAlignment(),
                    EWrapAlignment.SpaceEvenly
                );
                Equal(visual.RunSpacing(), 5.0f);
                Equal(
                    visual.CrossAxisAlignment(),
                    EWrapCrossAlignment.End
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirection.RTL
                );
                Equal(
                    visual.VerticalDirection(),
                    EVerticalDirection.Up
                );
                Equal(
                    visual.ClipBehavior(),
                    EClipBehavior.HardEdge
                );
            }
        );
    }


{
        CheckVisualWidget<Stack, NexusVisualMultiChild, VisualStack>(
            (Stack widget) => {
                widget.Alignment = Alignment.BottomRight();
                widget.TextDirection = ETextDirection.RTL;
                widget.Fit = EStackFit.Expand;
                widget.ClipBehavior = EClipBehavior.None;
            },
            (VisualStack visual) => {
                Expect(
                    visual.Alignment() ==
                    AlignmentDirectional.TopStart()
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirection.LTR
                );
                Equal(visual.Fit(), EStackFit.Loose);
                Equal(
                    visual.ClipBehavior(),
                    EClipBehavior.HardEdge
                );
            },
            (VisualStack visual) => {
                Expect(
                    visual.Alignment() == Alignment.BottomRight()
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirection.RTL
                );
                Equal(visual.Fit(), EStackFit.Expand);
                Equal(
                    visual.ClipBehavior(),
                    EClipBehavior.None
                );
            }
        );
    }


{
        CheckVisualWidget<IndexedStack, NexusVisualMultiChild, VisualIndexedStack>(
            (IndexedStack widget) => {
                widget.Alignment = Alignment.Center();
                widget.TextDirection = ETextDirection.RTL;
                widget.Fit = EStackFit.Passthrough;
                widget.ClipBehavior = EClipBehavior.None;
                widget.Index = 4u;
            },
            (VisualIndexedStack visual) => {
                Expect(
                    visual.Alignment() ==
                    AlignmentDirectional.TopStart()
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirection.LTR
                );
                Equal(visual.Fit(), EStackFit.Loose);
                Equal(
                    visual.ClipBehavior(),
                    EClipBehavior.HardEdge
                );
                Expect(visual.Index().HasValue);
                Equal(visual.Index().Value, 0u);
            },
            (VisualIndexedStack visual) => {
                Expect(
                    visual.Alignment() == Alignment.Center()
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirection.RTL
                );
                Equal(
                    visual.Fit(),
                    EStackFit.Passthrough
                );
                Equal(
                    visual.ClipBehavior(),
                    EClipBehavior.None
                );
                Expect(visual.Index().HasValue);
                Equal(visual.Index().Value, 4u);
            }
        );
    }


{
        CheckVisualWidget<Grid, NexusVisualMultiChild, VisualGrid>(
            (Grid widget) => {
                widget.Columns.Clear();
                widget.Columns.Add(VisualGridTrackSize.Fixed(40.0f));
                widget.Columns.Add(VisualGridTrackSize.Star(2.0f));
                widget.Rows.Clear();
                widget.Rows.Add(VisualGridTrackSize.Auto());
                widget.Rows.Add(
                    VisualGridTrackSize.Percent(0.25f)
                );
                widget.ColumnGap = 4.0f;
                widget.RowGap = 6.0f;
                widget.JustifyContent =
                    EVisualGridContentAlignment.SpaceBetween;
                widget.AlignContent =
                    EVisualGridContentAlignment.Center;
                widget.JustifyItems =
                    EVisualGridItemAlignment.End;
                widget.AlignItems =
                    EVisualGridItemAlignment.Center;
                widget.TextDirection = ETextDirection.RTL;
                widget.ClipToBounds = true;
            },
            (VisualGrid visual) => {
                Equal(visual.NumColumns(), 1u);
                Equal(visual.NumRows(), 1u);
                Equal(
                    visual.ColumnTrack(0).Kind(),
                    EVisualGridTrackKind.Star
                );
                Equal(
                    visual.ColumnTrack(0).Value(),
                    1.0f
                );
                Equal(
                    visual.RowTrack(0).Kind(),
                    EVisualGridTrackKind.Star
                );
                Equal(
                    visual.RowTrack(0).Value(),
                    1.0f
                );
                Equal(visual.ColumnGap(), 0.0f);
                Equal(visual.RowGap(), 0.0f);
                Equal(
                    visual.JustifyContent(),
                    EVisualGridContentAlignment.Stretch
                );
                Equal(
                    visual.AlignContent(),
                    EVisualGridContentAlignment.Stretch
                );
                Equal(
                    visual.JustifyItems(),
                    EVisualGridItemAlignment.Stretch
                );
                Equal(
                    visual.AlignItems(),
                    EVisualGridItemAlignment.Stretch
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirection.LTR
                );
                ExpectFalse(visual.ClipToBounds());
            },
            (VisualGrid visual) => {
                Equal(visual.NumColumns(), 2u);
                Equal(visual.NumRows(), 2u);
                Equal(
                    visual.ColumnTrack(0).Kind(),
                    EVisualGridTrackKind.Fixed
                );
                Equal(
                    visual.ColumnTrack(0).Value(),
                    40.0f
                );
                Equal(
                    visual.ColumnTrack(1).Kind(),
                    EVisualGridTrackKind.Star
                );
                Equal(
                    visual.ColumnTrack(1).Value(),
                    2.0f
                );
                Equal(
                    visual.RowTrack(0).Kind(),
                    EVisualGridTrackKind.Auto
                );
                Equal(
                    visual.RowTrack(1).Kind(),
                    EVisualGridTrackKind.Percent
                );
                Equal(
                    visual.RowTrack(1).Value(),
                    0.25f
                );
                Equal(visual.ColumnGap(), 4.0f);
                Equal(visual.RowGap(), 6.0f);
                Equal(
                    visual.JustifyContent(),
                    EVisualGridContentAlignment.SpaceBetween
                );
                Equal(
                    visual.AlignContent(),
                    EVisualGridContentAlignment.Center
                );
                Equal(
                    visual.JustifyItems(),
                    EVisualGridItemAlignment.End
                );
                Equal(
                    visual.AlignItems(),
                    EVisualGridItemAlignment.Center
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirection.RTL
                );
                Expect(visual.ClipToBounds());
            }
        );
    }


{
        CheckVisualWidget<PlacementLayout, NexusVisualMultiChild, VisualPlacement>(
            (PlacementLayout widget) => {
                widget.WidthRule =
                    VisualPlacementAxisSize.Sized(320.0f);
                widget.WidthRule.SetBoundsPx(120.0f, 500.0f);
                widget.HeightRule =
                    VisualPlacementAxisSize.Shrink();
                widget.HeightRule.SetBoundsPx(80.0f, 400.0f);
                widget.ClipBehavior = EClipBehavior.HardEdge;
            },
            (VisualPlacement visual) => {
                Equal(
                    visual.WidthRule().Kind(),
                    EVisualPlacementAxisSizeKind.Expand
                );
                Equal(
                    visual.HeightRule().Kind(),
                    EVisualPlacementAxisSizeKind.Expand
                );
                Equal(
                    visual.ClipBehavior(),
                    EClipBehavior.None
                );
            },
            (VisualPlacement visual) => {
                Equal(
                    visual.WidthRule().Kind(),
                    EVisualPlacementAxisSizeKind.Sized
                );
                Equal(
                    visual.WidthRule().ValuePx(),
                    320.0f
                );
                Expect(
                    visual.WidthRule().MinValuePx().HasValue
                );
                Expect(
                    visual.WidthRule().MaxValuePx().HasValue
                );
                Equal(
                    visual.WidthRule().MinValuePx().Value,
                    120.0f
                );
                Equal(
                    visual.WidthRule().MaxValuePx().Value,
                    500.0f
                );
                Equal(
                    visual.HeightRule().Kind(),
                    EVisualPlacementAxisSizeKind.Shrink
                );
                Expect(
                    visual.HeightRule().MinValuePx().HasValue
                );
                Expect(
                    visual.HeightRule().MaxValuePx().HasValue
                );
                Equal(
                    visual.HeightRule().MinValuePx().Value,
                    80.0f
                );
                Equal(
                    visual.HeightRule().MaxValuePx().Value,
                    400.0f
                );
                Equal(
                    visual.ClipBehavior(),
                    EClipBehavior.HardEdge
                );
            }
        );
    }
}
[GuiTest("widgets/widget_surface_tests.cpp::gui/widgets/slot-map-and-update")]
public static void Case2(){

{
        CheckSlotWidget<Flex, VisualFlex, FlexSlot, VisualFlexSlot>(
            (FlexSlot widget) => {
                widget.Flex = 3;
                widget.Fit = EFlexFit.Loose;
            },
            (VisualFlexSlot slot) => {
                Equal(slot.Flex(), 0);
                Equal(slot.Fit(), EFlexFit.Tight);
            },
            (VisualFlexSlot slot) => {
                Equal(slot.Flex(), 3);
                Equal(slot.Fit(), EFlexFit.Loose);
            }
        );
    }


{
        CheckSlotWidget<Stack, VisualStack, Positioned, VisualStackSlot>(
            (Positioned widget) => {
                widget.Left = 1.0f;
                widget.Top = 2.0f;
                widget.Right = 3.0f;
                widget.Bottom = 4.0f;
                widget.Width = 50.0f;
                widget.Height = 60.0f;
            },
            (VisualStackSlot slot) => {
                ExpectFalse(slot.Left().HasValue);
                ExpectFalse(slot.Top().HasValue);
                ExpectFalse(slot.Right().HasValue);
                ExpectFalse(slot.Bottom().HasValue);
                ExpectFalse(slot.Width().HasValue);
                ExpectFalse(slot.Height().HasValue);
            },
            (VisualStackSlot slot) => {
                Expect(slot.Left().HasValue);
                Expect(slot.Top().HasValue);
                Expect(slot.Right().HasValue);
                Expect(slot.Bottom().HasValue);
                Expect(slot.Width().HasValue);
                Expect(slot.Height().HasValue);
                Equal(slot.Left().Value, 1.0f);
                Equal(slot.Top().Value, 2.0f);
                Equal(slot.Right().Value, 3.0f);
                Equal(slot.Bottom().Value, 4.0f);
                Equal(slot.Width().Value, 50.0f);
                Equal(slot.Height().Value, 60.0f);
            }
        );
    }


{
        CheckSlotWidget<Grid, VisualGrid, GridSlot, VisualGridSlot>(
            (GridSlot widget) => {
                widget.RowStart = 2u;
                widget.ColumnStart = 3u;
                widget.RowSpan = 4u;
                widget.ColumnSpan = 5u;
                widget.JustifySelf =
                    EVisualGridItemAlignment.Center;
                widget.AlignSelf = EVisualGridItemAlignment.End;
            },
            (VisualGridSlot slot) => {
                Equal(slot.RowStart(), 0u);
                Equal(slot.ColumnStart(), 0u);
                Equal(slot.RowSpan(), 1u);
                Equal(slot.ColumnSpan(), 1u);
                ExpectFalse(slot.JustifySelf().HasValue);
                ExpectFalse(slot.AlignSelf().HasValue);
            },
            (VisualGridSlot slot) => {
                Equal(slot.RowStart(), 2u);
                Equal(slot.ColumnStart(), 3u);
                Equal(slot.RowSpan(), 4u);
                Equal(slot.ColumnSpan(), 5u);
                Expect(slot.JustifySelf().HasValue);
                Expect(slot.AlignSelf().HasValue);
                Equal(
                    slot.JustifySelf().Value,
                    EVisualGridItemAlignment.Center
                );
                Equal(
                    slot.AlignSelf().Value,
                    EVisualGridItemAlignment.End
                );
            }
        );
    }


{
        Placement placement = Placement.Align(
            Alignment.BottomRight(),
            new Sizef(80.0f, 40.0f),
            new Offsetf(3.0f, 5.0f)
        );
        CheckSlotWidget<PlacementLayout, VisualPlacement, PlacementSlot, VisualPlacementSlot>(
            (PlacementSlot widget) => {
                widget.Placement = placement;
                widget.SizeMode = EPlacementSizeMode.Loose;
            },
            (VisualPlacementSlot slot) => {
                Expect(
                    slot.Placement() == Placement.Fill()
                );
                Equal(
                    slot.SizeMode(),
                    EPlacementSizeMode.Tight
                );
            },
            (VisualPlacementSlot slot) => {
                Expect(slot.Placement() == placement);
                Equal(
                    slot.SizeMode(),
                    EPlacementSizeMode.Loose
                );
            }
        );
    }
}
[GuiTest("widgets/widget_surface_tests.cpp::gui/widgets/convenience-map-and-update")]
public static void Case3(){

{
        CheckVisualWidget<SizedBox, NexusVisualSingleChild, VisualConstrained>(
            (SizedBox widget) => {
                widget.Width = 120.0f;
                widget.Height = 80.0f;
            },
            (VisualConstrained visual) => {
                Expect(
                    visual.AdditionalConstraints() == new BoxConstraints()
                );
            },
            (VisualConstrained visual) => {
                BoxConstraints constraints =
                    visual.AdditionalConstraints();
                Equal(constraints.MinWidth, 120.0f);
                Equal(constraints.MaxWidth, 120.0f);
                Equal(constraints.MinHeight, 80.0f);
                Equal(constraints.MaxHeight, 80.0f);
            }
        );
    }


{
        CheckVisualWidget<Center, NexusVisualSingleChild, VisualPositioned>(
            (Center widget) => {
                widget.WidthFactor = 2.0f;
                widget.HeightFactor = 3.0f;
            },
            (VisualPositioned visual) => {
                Expect(
                    visual.Alignment() == Alignment.Center()
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirection.LTR
                );
                ExpectFalse(visual.WidthFactor().HasValue);
                ExpectFalse(visual.HeightFactor().HasValue);
            },
            (VisualPositioned visual) => {
                Expect(
                    visual.Alignment() == Alignment.Center()
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirection.LTR
                );
                Expect(visual.WidthFactor().HasValue);
                Expect(visual.HeightFactor().HasValue);
                Equal(visual.WidthFactor().Value, 2.0f);
                Equal(visual.HeightFactor().Value, 3.0f);
            }
        );
    }


{
        CheckVisualWidget<Row, NexusVisualMultiChild, VisualFlex>(
            (Row widget) => {
                widget.MainAxisSize = EMainAxisSize.Min;
                widget.MainAxisAlignment =
                    EMainAxisAlignment.End;
                widget.CrossAxisAlignment =
                    ECrossAxisAlignment.Stretch;
                widget.TextDirection = ETextDirection.RTL;
                widget.VerticalDirection = EVerticalDirection.Up;
                widget.TextBaseline = ETextBaseline.Alphabetic;
                widget.ClipBehavior = EClipBehavior.HardEdge;
                widget.Spacing = 9.0f;
            },
            (VisualFlex visual) => {
                Equal(
                    visual.Direction(),
                    EAxis.Horizontal
                );
                Equal(visual.Spacing(), 0.0f);
            },
            (VisualFlex visual) => {
                Equal(
                    visual.Direction(),
                    EAxis.Horizontal
                );
                Equal(
                    visual.MainAxisSize(),
                    EMainAxisSize.Min
                );
                Equal(
                    visual.MainAxisAlignment(),
                    EMainAxisAlignment.End
                );
                Equal(
                    visual.CrossAxisAlignment(),
                    ECrossAxisAlignment.Stretch
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirection.RTL
                );
                Equal(
                    visual.VerticalDirection(),
                    EVerticalDirection.Up
                );
                Expect(visual.TextBaseline().HasValue);
                Equal(
                    visual.TextBaseline().Value,
                    ETextBaseline.Alphabetic
                );
                Equal(
                    visual.ClipBehavior(),
                    EClipBehavior.HardEdge
                );
                Equal(visual.Spacing(), 9.0f);
            }
        );
    }


{
        CheckVisualWidget<Column, NexusVisualMultiChild, VisualFlex>(
            (Column widget) => {
                widget.MainAxisSize = EMainAxisSize.Min;
                widget.MainAxisAlignment =
                    EMainAxisAlignment.Center;
                widget.CrossAxisAlignment =
                    ECrossAxisAlignment.End;
                widget.TextDirection = ETextDirection.RTL;
                widget.VerticalDirection = EVerticalDirection.Up;
                widget.TextBaseline = ETextBaseline.Ideographic;
                widget.ClipBehavior = EClipBehavior.HardEdge;
                widget.Spacing = 11.0f;
            },
            (VisualFlex visual) => {
                Equal(
                    visual.Direction(),
                    EAxis.Vertical
                );
                Equal(visual.Spacing(), 0.0f);
            },
            (VisualFlex visual) => {
                Equal(
                    visual.Direction(),
                    EAxis.Vertical
                );
                Equal(
                    visual.MainAxisSize(),
                    EMainAxisSize.Min
                );
                Equal(
                    visual.MainAxisAlignment(),
                    EMainAxisAlignment.Center
                );
                Equal(
                    visual.CrossAxisAlignment(),
                    ECrossAxisAlignment.End
                );
                Equal(
                    visual.TextDirection(),
                    ETextDirection.RTL
                );
                Equal(
                    visual.VerticalDirection(),
                    EVerticalDirection.Up
                );
                Expect(visual.TextBaseline().HasValue);
                Equal(
                    visual.TextBaseline().Value,
                    ETextBaseline.Ideographic
                );
                Equal(
                    visual.ClipBehavior(),
                    EClipBehavior.HardEdge
                );
                Equal(visual.Spacing(), 11.0f);
            }
        );
    }


{
        CheckSlotWidget<Flex, VisualFlex, Flexible, VisualFlexSlot>(
            (Flexible widget) => {
                widget.Flex = 4;
            },
            (VisualFlexSlot slot) => {
                Equal(slot.Flex(), 1);
                Equal(slot.Fit(), EFlexFit.Loose);
            },
            (VisualFlexSlot slot) => {
                Equal(slot.Flex(), 4);
                Equal(slot.Fit(), EFlexFit.Loose);
            }
        );
    }


{
        CheckSlotWidget<Flex, VisualFlex, Expanded, VisualFlexSlot>(
            (Expanded widget) => {
                widget.Flex = 5;
            },
            (VisualFlexSlot slot) => {
                Equal(slot.Flex(), 1);
                Equal(slot.Fit(), EFlexFit.Tight);
            },
            (VisualFlexSlot slot) => {
                Equal(slot.Flex(), 5);
                Equal(slot.Fit(), EFlexFit.Tight);
            }
        );
    }
}
[GuiTest("widgets/widget_surface_tests.cpp::gui/widgets/child-topology-and-order")]
public static void Case4(){

{
        using var host = new WidgetTreeHost(new TempText());
        Equal(host.Root.NumChildren(), 0u);
    }


{
        Padding initial = new Padding();
        initial.Child = MakeTextWidget(1, "initial");
        using var host = new WidgetTreeHost(initial);

        var nexus =
            RttrCast<NexusVisualSingleChild>(host.Root);
        var visual =
            host.Root.Visual().RttrCast<VisualPadding>();
        Expect(nexus);
        Expect(visual);
        Equal(nexus.NumChildren(), 1u);
        Nexus child_nexus = nexus.ChildAt(0);
        VisualNode child_visual = child_nexus.Visual();
        Equal(visual.Child(), child_visual);

        Padding updated = new Padding();
        updated.Child = MakeTextWidget(1, "updated");
        host.Update(updated);
        Equal(nexus.NumChildren(), 1u);
        Equal(nexus.ChildAt(0), child_nexus);
        Equal(nexus.ChildAt(0).Visual(), child_visual);
        Equal(visual.Child(), child_visual);
        var child_text = RttrCast<VisualTempText>(child_visual);
        Expect(child_text);
        Expect(
            child_text.Text() == "updated"
        );

        host.Update(new Padding());
        Equal(nexus.NumChildren(), 0u);
        Equal(visual.Child(), null);
    }


{
        Stack initial = new Stack();
        initial.Children.Add(MakeTextWidget(1, "first"));
        initial.Children.Add(MakeTextWidget(2, "second"));
        using var host = new WidgetTreeHost(initial);

        var nexus =
            RttrCast<NexusVisualMultiChild>(host.Root);
        var visual =
            host.Root.Visual().RttrCast<VisualStack>();
        Expect(nexus);
        Expect(visual);
        Equal(nexus.NumChildren(), 2u);
        Equal(visual.NumChildren(), 2u);
        Nexus first_nexus = nexus.ChildAt(0);
        Nexus second_nexus = nexus.ChildAt(1);
        VisualNode first_visual = visual.ChildAt(0);
        VisualNode second_visual = visual.ChildAt(1);

        Stack updated = new Stack();
        updated.Children.Add(MakeTextWidget(2, "second-updated"));
        updated.Children.Add(MakeTextWidget(1, "first-updated"));
        host.Update(updated);

        Equal(nexus.NumChildren(), 2u);
        Equal(visual.NumChildren(), 2u);
        Equal(nexus.ChildAt(0), second_nexus);
        Equal(nexus.ChildAt(1), first_nexus);
        Equal(visual.ChildAt(0), second_visual);
        Equal(visual.ChildAt(1), first_visual);
        var second_text =
            RttrCast<VisualTempText>(second_visual);
        var first_text =
            RttrCast<VisualTempText>(first_visual);
        Expect(second_text);
        Expect(first_text);
        Expect(
            second_text.Text() == "second-updated"
        );
        Expect(
            first_text.Text() == "first-updated"
        );

        Stack grown = new Stack();
        grown.Children.Add(MakeTextWidget(2, "second-grown"));
        grown.Children.Add(MakeTextWidget(3, "third"));
        grown.Children.Add(MakeTextWidget(1, "first-grown"));
        host.Update(grown);

        Equal(nexus.NumChildren(), 3u);
        Equal(visual.NumChildren(), 3u);
        Equal(nexus.ChildAt(0), second_nexus);
        Equal(nexus.ChildAt(2), first_nexus);
        Equal(visual.ChildAt(0), second_visual);
        Equal(visual.ChildAt(2), first_visual);
        Nexus third_nexus = nexus.ChildAt(1);
        VisualNode third_visual = visual.ChildAt(1);
        Equal(third_nexus.Visual(), third_visual);

        Stack shrunk = new Stack();
        shrunk.Children.Add(MakeTextWidget(3, "third-final"));
        host.Update(shrunk);

        Equal(nexus.NumChildren(), 1u);
        Equal(visual.NumChildren(), 1u);
        Equal(nexus.ChildAt(0), third_nexus);
        Equal(visual.ChildAt(0), third_visual);
        var third_text =
            RttrCast<VisualTempText>(third_visual);
        Expect(third_text);
        Expect(
            third_text.Text() == "third-final"
        );
    }


{
        Padding initial = new Padding();
        SizedBox initial_child = new SizedBox();
        initial_child.Key = new Key((long)(1));
        initial_child.Width = 40.0f;
        initial.Child = initial_child;
        using var host = new WidgetTreeHost(initial);

        Nexus initial_nexus = host.Root.ChildAt(0);
        VisualNode initial_visual = initial_nexus.Visual();
        Equal(
            initial_nexus.Widget().GetType(),
            typeof(SizedBox)
        );

        Padding updated = new Padding();
        ConstrainedBox updated_child =
            new ConstrainedBox();
        updated_child.Key = new Key((long)(1));
        updated_child.AdditionalConstraints =
            BoxConstraints.TightWidth(40.0f);
        updated.Child = updated_child;
        host.Update(updated);

        Nexus updated_nexus = host.Root.ChildAt(0);
        NotEqualNumeric(updated_nexus, initial_nexus);
        NotEqualNumeric(updated_nexus.Visual(), initial_visual);
        Equal(
            updated_nexus.Widget().GetType(),
            typeof(ConstrainedBox)
        );
        Equal(
            updated_nexus.Visual().GetType(),
            typeof(VisualConstrained)
        );
    }
}
[GuiTest("widgets/widget_surface_tests.cpp::gui/widgets/complex-dsl-tree")]
public static void Case5(){
    using var host = new WidgetTreeHost(MakeComplexWidgetTree(false));

    Nexus placement_nexus = host.Root;
    Equal(placement_nexus.NumChildren(), 1u);
    Nexus placement_slot_nexus = placement_nexus.ChildAt(0);
    Equal(placement_slot_nexus.NumChildren(), 1u);
    Nexus stack_nexus = placement_slot_nexus.ChildAt(0);
    Equal(stack_nexus.NumChildren(), 1u);
    Nexus positioned_nexus = stack_nexus.ChildAt(0);
    Equal(positioned_nexus.NumChildren(), 1u);
    Nexus grid_nexus = positioned_nexus.ChildAt(0);
    Equal(grid_nexus.NumChildren(), 1u);
    Nexus grid_slot_nexus = grid_nexus.ChildAt(0);
    Equal(grid_slot_nexus.NumChildren(), 1u);
    Nexus column_nexus = grid_slot_nexus.ChildAt(0);
    Equal(column_nexus.NumChildren(), 1u);
    Nexus expanded_nexus = column_nexus.ChildAt(0);
    Equal(expanded_nexus.NumChildren(), 1u);
    Nexus row_nexus = expanded_nexus.ChildAt(0);
    Equal(row_nexus.NumChildren(), 1u);
    Nexus flexible_nexus = row_nexus.ChildAt(0);
    Equal(flexible_nexus.NumChildren(), 1u);
    Nexus padding_nexus = flexible_nexus.ChildAt(0);
    Equal(padding_nexus.NumChildren(), 1u);
    Nexus text_nexus = padding_nexus.ChildAt(0);

    var placement_visual =
        placement_nexus.Visual().RttrCast<VisualPlacement>();
    var stack_visual =
        stack_nexus.Visual().RttrCast<VisualStack>();
    var grid_visual =
        grid_nexus.Visual().RttrCast<VisualGrid>();
    var column_visual =
        column_nexus.Visual().RttrCast<VisualFlex>();
    var row_visual =
        row_nexus.Visual().RttrCast<VisualFlex>();
    var padding_visual =
        padding_nexus.Visual().RttrCast<VisualPadding>();
    var text_visual =
        text_nexus.Visual().RttrCast<VisualTempText>();
    Expect(placement_visual);
    Expect(stack_visual);
    Expect(grid_visual);
    Expect(column_visual);
    Expect(row_visual);
    Expect(padding_visual);
    Expect(text_visual);

    VisualSlot placement_slot = stack_visual.Slot();
    VisualSlot positioned_slot = grid_visual.Slot();
    VisualSlot grid_slot = column_visual.Slot();
    VisualSlot expanded_slot = row_visual.Slot();
    VisualSlot flexible_slot = padding_visual.Slot();
    Expect(placement_slot);
    Expect(positioned_slot);
    Expect(grid_slot);
    Expect(expanded_slot);
    Expect(flexible_slot);
    Equal(
        placement_slot.GetType(),
        typeof(VisualPlacementSlot)
    );
    Equal(
        positioned_slot.GetType(),
        typeof(VisualStackSlot)
    );
    Equal(
        grid_slot.GetType(),
        typeof(VisualGridSlot)
    );
    Equal(
        expanded_slot.GetType(),
        typeof(VisualFlexSlot)
    );
    Equal(
        flexible_slot.GetType(),
        typeof(VisualFlexSlot)
    );

    Equal(placement_visual.NumChildren(), 1u);
    Equal(placement_visual.ChildAt(0), stack_visual);
    Equal(stack_visual.NumChildren(), 1u);
    Equal(stack_visual.ChildAt(0), grid_visual);
    Equal(grid_visual.NumChildren(), 1u);
    Equal(grid_visual.ChildAt(0), column_visual);
    Equal(column_visual.NumChildren(), 1u);
    Equal(column_visual.ChildAt(0), row_visual);
    Equal(row_visual.NumChildren(), 1u);
    Equal(row_visual.ChildAt(0), padding_visual);
    Equal(padding_visual.Child(), text_visual);

    host.Update(MakeComplexWidgetTree(true));

    Equal(host.Root, placement_nexus);
    Equal(
        placement_nexus.ChildAt(0),
        placement_slot_nexus
    );
    Equal(
        placement_slot_nexus.ChildAt(0),
        stack_nexus
    );
    Equal(stack_nexus.ChildAt(0), positioned_nexus);
    Equal(positioned_nexus.ChildAt(0), grid_nexus);
    Equal(grid_nexus.ChildAt(0), grid_slot_nexus);
    Equal(grid_slot_nexus.ChildAt(0), column_nexus);
    Equal(column_nexus.ChildAt(0), expanded_nexus);
    Equal(expanded_nexus.ChildAt(0), row_nexus);
    Equal(row_nexus.ChildAt(0), flexible_nexus);
    Equal(flexible_nexus.ChildAt(0), padding_nexus);
    Equal(padding_nexus.ChildAt(0), text_nexus);

    Equal(placement_nexus.Visual(), placement_visual);
    Equal(stack_nexus.Visual(), stack_visual);
    Equal(grid_nexus.Visual(), grid_visual);
    Equal(column_nexus.Visual(), column_visual);
    Equal(row_nexus.Visual(), row_visual);
    Equal(padding_nexus.Visual(), padding_visual);
    Equal(text_nexus.Visual(), text_visual);
    Equal(stack_visual.Slot(), placement_slot);
    Equal(grid_visual.Slot(), positioned_slot);
    Equal(column_visual.Slot(), grid_slot);
    Equal(row_visual.Slot(), expanded_slot);
    Equal(padding_visual.Slot(), flexible_slot);

    Equal(
        placement_visual.WidthRule().Kind(),
        EVisualPlacementAxisSizeKind.Sized
    );
    Equal(
        placement_visual.WidthRule().ValuePx(),
        640.0f
    );
    Equal(
        placement_visual.HeightRule().Kind(),
        EVisualPlacementAxisSizeKind.Shrink
    );
    Equal(
        placement_visual.ClipBehavior(),
        EClipBehavior.HardEdge
    );
    Expect(
        ((VisualPlacementSlot)(placement_slot))
            .Placement() ==
        Placement.Align(
            Alignment.Center(),
            new Sizef(300.0f, 200.0f)
        )
    );
    Equal(
        ((VisualPlacementSlot)(placement_slot))
            .SizeMode(),
        EPlacementSizeMode.Loose
    );
    Equal(stack_visual.Fit(), EStackFit.Expand);
    Expect(
        ((VisualStackSlot)(positioned_slot))
            .Left()
            .HasValue
    );
    Expect(
        ((VisualStackSlot)(positioned_slot))
            .Top()
            .HasValue
    );
    Equal(
        ((VisualStackSlot)(positioned_slot)).Left().Value,
        12.0f
    );
    Equal(
        ((VisualStackSlot)(positioned_slot)).Top().Value,
        18.0f
    );
    Equal(grid_visual.ColumnGap(), 7.0f);
    Equal(grid_visual.RowGap(), 9.0f);
    Equal(
        ((VisualGridSlot)(grid_slot)).RowStart(),
        2u
    );
    Equal(
        ((VisualGridSlot)(grid_slot)).ColumnStart(),
        3u
    );
    Equal(column_visual.Direction(), EAxis.Vertical);
    Equal(column_visual.Spacing(), 11.0f);
    Equal(
        ((VisualFlexSlot)(expanded_slot)).Flex(),
        4
    );
    Equal(
        ((VisualFlexSlot)(expanded_slot)).Fit(),
        EFlexFit.Tight
    );
    Equal(row_visual.Direction(), EAxis.Horizontal);
    Equal(row_visual.Spacing(), 13.0f);
    Equal(
        ((VisualFlexSlot)(flexible_slot)).Flex(),
        5
    );
    Equal(
        ((VisualFlexSlot)(flexible_slot)).Fit(),
        EFlexFit.Loose
    );
    Expect(
        padding_visual.Padding() == EdgeInsets.All(16.0f)
    );
    Expect(text_visual.Text() == "updated");
}
}
