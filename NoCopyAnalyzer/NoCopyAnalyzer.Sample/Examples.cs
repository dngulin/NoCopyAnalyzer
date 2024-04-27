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

        var s1 = new SampleStruct();

        var s2 = s1;
        s1 = s2;

        var localCapture = () => { ReceiveByValue(s1); };
        var parameterCapture = () => ReceiveByValue(argument);
    }

    public static SampleStruct ReceiveByValue(SampleStruct s)
    {
        ref var r1 = ref s;

        var s2 = new SampleStruct();
        r1 = ref s2;

        return s;
    }

    public static void ReceiveAsObject(object argument)
    {
        Property = (SampleStruct)argument;
        Property = new SampleStruct();
        Property = default(SampleStruct);
        Property = default;
    }

    public static void ReceiveAsInterface(ISampleInterface _)
    {
    }

    public static void ReturnAsOut(out SampleStruct result)
    {
        result = default;
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