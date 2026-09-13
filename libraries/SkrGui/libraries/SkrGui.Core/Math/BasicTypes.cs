namespace SkrGui;

// Source: SkrGuiCore/basic_types.hpp (611561f8).
public enum EAxis : byte { Horizontal, Vertical }
public enum ETextDirection : byte { RTL, LTR }
public enum ETextBaseline : byte { Alphabetic, Ideographic }
public enum EVerticalDirection : byte { Up, Down }
public enum EBoxFit : byte { Fill, Contain, Cover, FitWidth, FitHeight, None, ScaleDown }
public enum EClipBehavior : byte { None, HardEdge, AntiAlias, AntiAliasWithSaveLayer }
public static class GuiConstants { public const float FlutterPrecisionErrorTolerance = 1e-10f; }
