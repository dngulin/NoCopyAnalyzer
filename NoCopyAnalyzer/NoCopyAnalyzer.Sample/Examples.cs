// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using System;

namespace NoCopyAnalyzer.Sample;

public static class Examples
{
    // Uncomment the next line to get analyzer errors
    // [NoCopy]
    public struct SampleStruct : ISampleInterface
    {
        public int SampleField;
    }

    public struct ParentStruct
    {
        public SampleStruct InnerStruct;
    }

    public static void Foo(SampleStruct argument)
    {
        ReceiveByValue(new SampleStruct());
        ReceiveAsObject(new SampleStruct());
        ReceiveAsInterface(new SampleStruct());

        var s = new SampleStruct();
        var localCapture = () => { ReceiveByValue(s); };

        var parameterCapture = () => ReceiveByValue(argument);
    }

    public static SampleStruct ReceiveByValue(SampleStruct s)
    {
        return s;
    }

    public static void ReceiveAsObject(object argument)
    {
        Property = (SampleStruct)argument;
    }

    public static void ReceiveAsInterface(ISampleInterface _)
    {
    }

    public static SampleStruct Property { get; set; }
}

[AttributeUsage(AttributeTargets.Struct)]
public class NoCopyAttribute : Attribute
{
}

public interface ISampleInterface
{
}