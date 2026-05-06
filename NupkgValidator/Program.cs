using SimdJson;

namespace NupkgValidator;

/// <summary>
/// Validates that the SimdJson native library can be loaded from the NuGet package
/// runtime assets for the current platform.
/// </summary>
class Program
{
    static int Main(string[] args)
    {
        try
        {
            using var doc = SimdJsonParser.Shared.Parse("""{"name":"Alice","age":30}""");
            using var name = doc.GetField("name");
            Console.Error.WriteLine($"Success.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Failed! {e.Message}");
            return 1;
        }
    }
}
