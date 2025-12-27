using GoldMeridian.CodeAnalysis;

namespace GoldMeridian.KeyGen.TestProject;

internal static class A
{
    public class Nested;

    [ExtensionDataFor<MyData>]
    [ExtensionDataFor<Nested>]
    public class NestedExtension;
}

public class MyData
{
    public int A { get; set; }
}

[ExtensionDataFor<MyData>]
[ExtensionDataFor<A.Nested>]
public class MyDataExtension
{
    public string? B { get; set; }
}

internal static class Program
{
    public static void Main()
    {
        _ = new MyData().Extension;
        _ = new A.Nested().MyDataExtension;
    }
}
