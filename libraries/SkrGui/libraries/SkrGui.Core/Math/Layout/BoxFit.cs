namespace SkrGui;

// Source: SkrGuiCore/math/layout/box_fit.hpp (611561f8).
public struct FittedSizes
{
    public Sizef Source,Destination;
    public FittedSizes(Sizef source,Sizef destination){Source=source;Destination=destination;}
}
public static class GuiMath
{
    public static FittedSizes ApplyBoxFit(EBoxFit fit,Sizef inputSize,Sizef outputSize)
    {
        if(inputSize.Height<=0||inputSize.Width<=0||outputSize.Height<=0||outputSize.Width<=0)return new();
        Sizef sourceSize=new(),destinationSize=new();
        switch(fit)
        {
            case EBoxFit.Fill:sourceSize=inputSize;destinationSize=outputSize;break;
            case EBoxFit.Contain:
                sourceSize=inputSize;
                if(outputSize.Width/outputSize.Height>sourceSize.Width/sourceSize.Height)destinationSize=new(sourceSize.Width*outputSize.Height/sourceSize.Height,outputSize.Height);
                else destinationSize=new(outputSize.Width,sourceSize.Height*outputSize.Width/sourceSize.Width);
                break;
            case EBoxFit.Cover:
                if(outputSize.Width/outputSize.Height>inputSize.Width/inputSize.Height)sourceSize=new(inputSize.Width,inputSize.Width*outputSize.Height/outputSize.Width);
                else sourceSize=new(inputSize.Height*outputSize.Width/outputSize.Height,inputSize.Height);
                destinationSize=outputSize;break;
            case EBoxFit.FitWidth:
                if(outputSize.Width/outputSize.Height>inputSize.Width/inputSize.Height){sourceSize=new(inputSize.Width,inputSize.Width*outputSize.Height/outputSize.Width);destinationSize=outputSize;}
                else{sourceSize=inputSize;destinationSize=new(outputSize.Width,sourceSize.Height*outputSize.Width/sourceSize.Width);}
                break;
            case EBoxFit.FitHeight:
                if(outputSize.Width/outputSize.Height>inputSize.Width/inputSize.Height){sourceSize=inputSize;destinationSize=new(sourceSize.Width*outputSize.Height/sourceSize.Height,outputSize.Height);}
                else{sourceSize=new(inputSize.Height*outputSize.Width/outputSize.Height,inputSize.Height);destinationSize=outputSize;}
                break;
            case EBoxFit.None:sourceSize=new(CppMath.Min(inputSize.Width,outputSize.Width),CppMath.Min(inputSize.Height,outputSize.Height));destinationSize=sourceSize;break;
            case EBoxFit.ScaleDown:
                sourceSize=inputSize;destinationSize=inputSize;float aspectRatio=inputSize.Width/inputSize.Height;
                if(destinationSize.Height>outputSize.Height)destinationSize=new(outputSize.Height*aspectRatio,outputSize.Height);
                if(destinationSize.Width>outputSize.Width)destinationSize=new(outputSize.Width,outputSize.Width/aspectRatio);
                break;
        }
        return new(sourceSize,destinationSize);
    }
}
