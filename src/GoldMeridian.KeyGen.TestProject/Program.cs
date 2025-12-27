using GoldMeridian.CodeAnalysis;

namespace GoldMeridian.KeyGen.TestProject;

public class MyData
{
    public int A { get; set; }
}

[ExtensionDataFor<MyData>]
public class MyDataExtension
{
    public string? B { get; set; }
}

internal static class Program
{
    public static void Main()
    {
        _ = new MyData().Extension;
    }
}