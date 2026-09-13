using SkrGui;
using static SkrGui.Tests.OriginalMathHelpers;
using static SkrGui.Tests.OriginalWidgetFixtures;
namespace SkrGui.Tests;
internal static class VisualTempTextTests
{

private sealed class VisualTempTextFixture:IDisposable
{
 public TextServices Service;public EmbeddedTextFontProvider Provider;public string Family="";public bool IsReady;
 public VisualTempTextFixture()
 {
  Service=TextServices.CreateFallback(new TextServicesDesc{AddSystemFontProvider=false});
  Provider=new();Provider.AddFace(new(){Family="Skr Visual Test Latin",SourceKey="embedded://visual-temp-text/latin/v1",Data=TestFontAssets.Get("Latin")});
  IsReady=Service!=null&&Provider!=null;if(!IsReady)return;
  Family="Skr Visual Test Latin";Service!.AddFontProvider(Provider!);
 }
 public VisualTempText MakeText(Utf8StringView value)
 {TextStyle style=new(){FontFamilies=Family,FontSize=13};var result=new VisualTempText();result.SetTextStyle(style);result.SetText(value);return result;}
 public VisualOwner Mount(VisualTempText text,VisualTestBackend? backend=null)
 {backend??=new();var result=new VisualOwner(Service,backend);result.SetRoot(text);return result;}
 public bool Paint(VisualOwner owner,VisualTestBackend backend)
 {VisualRenderDesc desc=new(){Target=backend.MakeTarget(new Sizei(512,256))};return owner.Paint()&&owner.Render(desc);}
 public void Dispose()=>Service.Dispose();
}
private static VisualTestCommand? FindCommand(VisualTestBackend backend,EVisualTestCommand kind)
{foreach(var command in backend.Commands)if(command.Kind==kind)return command;return null;}
private static VisualTestCommand? FindDrawCommand(VisualTestBackend backend)
{foreach(var command in backend.Commands)if(command.Kind==EVisualTestCommand.Text||command.Kind==EVisualTestCommand.Mesh)return command;return null;}
private static Rectf CommandBound(VisualTestBackend backend,VisualTestCommand command)
{
 Check.That(command.Range.IndexCount>0);ulong indexEnd=(ulong)command.Range.IndexStart+command.Range.IndexCount;Check.That(indexEnd<=(ulong)backend.Indices.Length);
 uint firstIndex=backend.Indices[command.Range.IndexStart];Check.That(firstIndex<backend.Vertices.Length);
 Rectf result=Rectf.Points(backend.Vertices[firstIndex].Pos,backend.Vertices[firstIndex].Pos);
 for(ulong i=(ulong)command.Range.IndexStart+1;i<indexEnd;++i){uint vertexIndex=backend.Indices[i];Check.That(vertexIndex<backend.Vertices.Length);result=result.Unite(Rectf.Points(backend.Vertices[vertexIndex].Pos,backend.Vertices[vertexIndex].Pos));}return result;
}

[GuiTest("visual/visual_temp_text_tests.cpp::gui/visual-temp-text/layout-and-baseline")]
public static void Case0(){using var fixture=new VisualTempTextFixture();

    Expect(fixture.IsReady);

    var text = fixture.MakeText("Hello");
    var owner = fixture.Mount(text);
    BoxConstraints constraints = new BoxConstraints(0.0f, 200.0f, 0.0f, 100.0f);
    owner.Layout(constraints);

    Expect(text.Size().Width > 0.0f);
    Expect(text.Size().Height > 0.0f);
    Expect(text.TextSize().Width > 0.0f);
    ExpectFalse(text.HasVisualOverflow());
    ExpectFalse(text.DidExceedMaxLines());

    CheckSizef(text.GetDryLayout(constraints), text.Size().Width, text.Size().Height);

    float? dry_baseline = text.GetDryBaseline(constraints, ETextBaseline.Alphabetic);
    float? actual_baseline = text.GetDistanceToActualBaseline(ETextBaseline.Alphabetic);
    Expect(dry_baseline);
    Expect(actual_baseline);
    ExpectNear(dry_baseline.Value, actual_baseline.Value, kFloatEpsilon);
    ExpectFalse(
        text.GetDryBaseline(constraints, ETextBaseline.Ideographic)
    );
    ExpectFalse(
        text.GetDistanceToActualBaseline(ETextBaseline.Ideographic)
    );

}
[GuiTest("visual/visual_temp_text_tests.cpp::gui/visual-temp-text/soft-wrap-and-visible-overflow")]
public static void Case1(){using var fixture=new VisualTempTextFixture();

    Expect(fixture.IsReady);

    var wrapped = fixture.MakeText("Hello world again");
    var wrapped_owner = fixture.Mount(wrapped);
    wrapped.SetSoftWrap(true);
    wrapped_owner.Layout(new BoxConstraints(0.0f, 35.0f, 0.0f, 200.0f));

    Expect(wrapped.Size().Width <= 35.0f + kFloatEpsilon);

    var visible = fixture.MakeText("Hello world again");
    var visible_owner = fixture.Mount(visible);
    visible.SetSoftWrap(false);
    visible.SetOverflow(ETextOverflow.Visible);
    visible.SetTextAlign(EVisualTempTextAlign.Right);
    visible_owner.Layout(new BoxConstraints(0.0f, 35.0f, 0.0f, 200.0f));

    CheckSizef(visible.Size(), 35.0f, visible.TextSize().Height);
    Expect(visible.TextSize().Width > visible.Size().Width);
    Expect(visible.HasVisualOverflow());
    Expect(wrapped.TextSize().Height > visible.TextSize().Height);

}
[GuiTest("visual/visual_temp_text_tests.cpp::gui/visual-temp-text/ellipsis-max-lines")]
public static void Case2(){using var fixture=new VisualTempTextFixture();

    Expect(fixture.IsReady);

    var text = fixture.MakeText("Hello world again");
    var owner = fixture.Mount(text);
    text.SetOverflow(ETextOverflow.Ellipsis);
    text.SetMaxLines(1u);
    owner.Layout(new BoxConstraints(0.0f, 35.0f, 0.0f, 200.0f));



    Expect(text.Size().Width <= 35.0f + kFloatEpsilon);
    Greater(text.TextSize().Width, 0.0f);
    Expect(text.DidExceedMaxLines());
    Expect(text.HasVisualOverflow());
    Equal(
        text.EllipsisCodepoint(),
        TextDefaults.EllipsisCodepoint
    );

}
[GuiTest("visual/visual_temp_text_tests.cpp::gui/visual-temp-text/width-trim-is-not-max-lines")]
public static void Case3(){using var fixture=new VisualTempTextFixture();

    Expect(fixture.IsReady);

    var text = fixture.MakeText("Hello world again");
    var owner = fixture.Mount(text);
    text.SetSoftWrap(false);
    text.SetOverflow(ETextOverflow.Ellipsis);
    owner.Layout(new BoxConstraints(0.0f, 35.0f, 0.0f, 100.0f));

    Expect(text.HasVisualOverflow());
    ExpectFalse(text.DidExceedMaxLines());

}
[GuiTest("visual/visual_temp_text_tests.cpp::gui/visual-temp-text/start-end-alignment")]
public static void Case4(){using var fixture=new VisualTempTextFixture();

    Expect(fixture.IsReady);

    var text = fixture.MakeText("Hello");
    var backend = new VisualTestBackend();
    var owner = fixture.Mount(text, backend);
    text.SetTextAlign(EVisualTempTextAlign.Start);
    text.SetTextDirection(ETextDirectionMode.RTL);
    owner.Layout(BoxConstraints.Tight(new Sizef(120.0f, 40.0f)));

    Expect(text.TextAlign() == EVisualTempTextAlign.Start);
    Expect(text.TextDirection() == ETextDirectionMode.RTL);
    Sizef start_size = text.TextSize();
    Expect(fixture.Paint(owner, backend));
    VisualTestCommand? start_command = FindCommand(backend, EVisualTestCommand.Text);
    Expect(start_command != null);
    Rectf start_bound = start_command.HasValue ? CommandBound(backend, start_command.Value) : Rectf.Zero();

    text.SetTextAlign(EVisualTempTextAlign.End);
    owner.Layout(BoxConstraints.Tight(new Sizef(120.0f, 40.0f)));
    Expect(text.TextAlign() == EVisualTempTextAlign.End);
    CheckSizef(text.TextSize(), start_size.Width, start_size.Height);
    Expect(fixture.Paint(owner, backend));
    VisualTestCommand? end_command = FindCommand(backend, EVisualTestCommand.Text);
    Expect(end_command != null);
    if (start_command.HasValue && end_command.HasValue)
    {
        Greater(start_bound.Left, CommandBound(backend, end_command.Value).Left);
    }

}
[GuiTest("visual/visual_temp_text_tests.cpp::gui/visual-temp-text/overflow-controls-visual-clip")]
public static void Case5(){using var fixture=new VisualTempTextFixture();

    Expect(fixture.IsReady);

    var backend = new VisualTestBackend();
    var text = fixture.MakeText("Hello");
    var owner = fixture.Mount(text, backend);
    owner.Layout(BoxConstraints.Tight(new Sizef(120.0f, 40.0f)));

    Expect(fixture.Paint(owner, backend));
    Expect(FindCommand(backend, EVisualTestCommand.BeginClip) != null);

    text.SetOverflow(ETextOverflow.Visible);
    owner.Layout(BoxConstraints.Tight(new Sizef(120.0f, 40.0f)));
    Expect(fixture.Paint(owner, backend));
    Expect(FindCommand(backend, EVisualTestCommand.BeginClip) == null);

}
[GuiTest("visual/visual_temp_text_tests.cpp::gui/visual-temp-text/paint-requires-committed-layout")]
public static void Case6(){using var fixture=new VisualTempTextFixture();

    Expect(fixture.IsReady);

    var backend = new VisualTestBackend();
    var text = fixture.MakeText("Initial");
    var owner = fixture.Mount(text, backend);
    BoxConstraints constraints = BoxConstraints.Tight(new Sizef(120.0f, 40.0f));
    owner.Layout(constraints);

    Expect(fixture.Paint(owner, backend));
    Expect(FindCommand(backend, EVisualTestCommand.Text) != null);
    Expect(
        text.GetDistanceToActualBaseline(ETextBaseline.Alphabetic)
    );

    text.SetText("Updated content");
    Expect(fixture.Paint(owner, backend));
    Expect(FindCommand(backend, EVisualTestCommand.Text) == null);
    ExpectFalse(
        text.GetDistanceToActualBaseline(ETextBaseline.Alphabetic)
    );

    owner.Layout(constraints);
    Expect(fixture.Paint(owner, backend));
    Expect(FindCommand(backend, EVisualTestCommand.Text) != null);
    Expect(
        text.GetDistanceToActualBaseline(ETextBaseline.Alphabetic)
    );

    text.SetTextAlign(EVisualTempTextAlign.Right);
    Expect(fixture.Paint(owner, backend));
    Expect(FindCommand(backend, EVisualTestCommand.Text) == null);
    ExpectFalse(
        text.GetDistanceToActualBaseline(ETextBaseline.Alphabetic)
    );

    owner.Layout(constraints);
    Expect(fixture.Paint(owner, backend));
    Expect(FindCommand(backend, EVisualTestCommand.Text) != null);

}
[GuiTest("visual/visual_temp_text_tests.cpp::gui/visual-temp-text/font-revision-requires-committed-layout")]
public static void Case7(){using var fixture=new VisualTempTextFixture();

    Expect(fixture.IsReady);

    var backend = new VisualTestBackend();
    var text = fixture.MakeText("Font revision");
    var owner = fixture.Mount(text, backend);
    BoxConstraints constraints = BoxConstraints.Tight(new Sizef(160.0f, 40.0f));
    owner.Layout(constraints);

    Expect(
        text.GetDistanceToActualBaseline(ETextBaseline.Alphabetic)
    );
    Expect(fixture.Paint(owner, backend));
    Expect(FindCommand(backend, EVisualTestCommand.Text) != null);

    var second_provider = new EmbeddedTextFontProvider();
    second_provider.AddFace(new EmbeddedFontSource { Family=fixture.Family, SourceKey="embedded://visual-temp-text/latin/v2",Data=TestFontAssets.Get("Latin") });
    fixture.Service.AddFontProvider(second_provider);

    ExpectFalse(
        text.GetDistanceToActualBaseline(ETextBaseline.Alphabetic)
    );
    Expect(fixture.Paint(owner, backend));
    Expect(FindCommand(backend, EVisualTestCommand.Text) == null);

    owner.Layout(constraints);
    Expect(
        text.GetDistanceToActualBaseline(ETextBaseline.Alphabetic)
    );
    Expect(fixture.Paint(owner, backend));
    Expect(FindCommand(backend, EVisualTestCommand.Text) != null);

}
[GuiTest("visual/visual_temp_text_tests.cpp::gui/visual-temp-text/detach-discards-owner-bound-layout")]
public static void Case8(){using var fixture=new VisualTempTextFixture();

    Expect(fixture.IsReady);

    var backend = new VisualTestBackend();
    var text = fixture.MakeText("Owner-bound layout");
    var first_owner = fixture.Mount(text, backend);
    BoxConstraints constraints = BoxConstraints.Tight(new Sizef(160.0f, 40.0f));
    first_owner.Layout(constraints);

    Expect(fixture.Paint(first_owner, backend));
    Expect(FindCommand(backend, EVisualTestCommand.Text) != null);

    first_owner.RemoveRoot();
    Expect(text.Owner() == null);

    var second_owner = fixture.Mount(text, backend);
    Expect(fixture.Paint(second_owner, backend));
    Expect(FindCommand(backend, EVisualTestCommand.Text) == null);

    second_owner.Layout(constraints);
    Expect(fixture.Paint(second_owner, backend));
    Expect(FindCommand(backend, EVisualTestCommand.Text) != null);

}
[GuiTest("visual/visual_temp_text_tests.cpp::gui/visual-temp-text/tab-stops-round-trip")]
public static void Case9(){using var fixture=new VisualTempTextFixture();

    Expect(fixture.IsReady);

    BoxConstraints constraints = new BoxConstraints(
        0.0f,
        300.0f,
        0.0f,
        100.0f
    );
    var text = fixture.MakeText("A\tB");
    var owner = fixture.Mount(text);
    text.SetSoftWrap(false);
    text.SetOverflow(ETextOverflow.Visible);

    float custom_stop = 120.0f;
    text.SetTabStops(new[]{custom_stop});
    owner.Layout(constraints);
    Sizef custom_size = text.TextSize();

    text.ClearTabStops();
    owner.Layout(constraints);
    Sizef cleared_size = text.TextSize();

    var reference = fixture.MakeText("A\tB");
    var reference_owner = fixture.Mount(reference);
    reference.SetSoftWrap(false);
    reference.SetOverflow(ETextOverflow.Visible);
    reference_owner.Layout(constraints);

    Greater(custom_size.Width, cleared_size.Width);
    CheckSizef(
        cleared_size,
        reference.TextSize().Width,
        reference.TextSize().Height
    );
    Expect(text.TabStops().IsEmpty());

}
[GuiTest("visual/visual_temp_text_tests.cpp::gui/visual-temp-text/resolves-text-service-from-owner")]
public static void Case10(){using var fixture=new VisualTempTextFixture();

    Expect(fixture.IsReady);

    TextStyle style = new();
    style.FontFamilies = fixture.Family;
    style.FontSize = 13.0f;

    var backend = new VisualTestBackend();
    var visual_owner = new VisualOwner(fixture.Service, backend);
    var text = new VisualTempText();
    text.SetTextStyle(style);
    text.SetText("Owner text fixture.Service");
    visual_owner.SetRoot(text);
    visual_owner.Layout(new BoxConstraints(0.0f, 200.0f, 0.0f, 100.0f));

    Expect(text.Owner() == visual_owner);
    Expect(text.Size().Width > 0.0f);
    Expect(text.Size().Height > 0.0f);

}
[GuiTest("visual/visual_temp_text_tests.cpp::gui/visual-temp-text/tight-width-paint-alignment")]
public static void Case11(){using var fixture=new VisualTempTextFixture();

    Expect(fixture.IsReady);

    var backend = new VisualTestBackend();
    var visual_owner = new VisualOwner(fixture.Service, backend);
    var text = fixture.MakeText("😀");
    visual_owner.SetRoot(text);

    float kVisualWidth = 120.0f;
    EVisualTempTextAlign[] alignments={
        EVisualTempTextAlign.Left,
        EVisualTempTextAlign.Center,
        EVisualTempTextAlign.Right,
    };
    float left_bound = 0.0f;
    Sizef reference_text_size = new();
    for (int i = 0; i < alignments.Length; ++i)
    {
        text.SetTextAlign(alignments[i]);
        visual_owner.Layout(BoxConstraints.Tight(new Sizef(kVisualWidth, 40.0f)));

        Less(text.TextSize().Width, kVisualWidth);
        Expect(fixture.Paint(visual_owner, backend));
        VisualTestCommand? command = FindDrawCommand(backend);
        Expect(command != null);
        if (!command.HasValue)
        {
            return;
        }
        Rectf bound = CommandBound(backend, command.Value);

        if (i == 0u)
        {
            left_bound = bound.Left;
            reference_text_size = text.TextSize();
        }
        else
        {
            CheckSizef(
                text.TextSize(),
                reference_text_size.Width,
                reference_text_size.Height
            );
        }

        float free_width = kVisualWidth - text.TextSize().Width;
        float expected_offset = alignments[i] == EVisualTempTextAlign.Center ?
            free_width * 0.5f :
            (alignments[i] == EVisualTempTextAlign.Right ? free_width : 0.0f);
        ExpectNear(
            bound.Left - left_bound,
            expected_offset,
            kFloatEpsilon
        );
    }

    text.SetTextAlign(EVisualTempTextAlign.Center);
    visual_owner.Layout(new BoxConstraints(0.0f, kVisualWidth, 0.0f, 40.0f));
    CheckSizef(
        text.Size(),
        text.TextSize().Width,
        text.TextSize().Height
    );
    Expect(fixture.Paint(visual_owner, backend));
    VisualTestCommand? compact_command = FindDrawCommand(backend);
    Expect(compact_command != null);
    ExpectNear(
        CommandBound(backend, compact_command.Value).Left,
        left_bound,
        kFloatEpsilon
    );

}
}
