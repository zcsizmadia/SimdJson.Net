using System.Text;
using SimdJson.Net.Internal;

namespace SimdJson.Net.Tests;

/// <summary>Direct tests for the SIMD stage-1 structural indexer.</summary>
public class StructuralIndexerTests
{
    private static int[] IndexOf(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        var ix = new int[StructuralIndexer.EstimateMaxIndices(bytes.Length)];
        int n = StructuralIndexer.Index(bytes, ix);
        return ix[..n];
    }

    [Test]
    public async Task Finds_Brackets_And_Colons()
    {
        var ix = IndexOf("{\"a\":1}");
        // Expected positions: { (0), " (1), : (4), 1 (5), } (6)
        await Assert.That(ix).IsEquivalentTo(new[] { 0, 1, 4, 5, 6 });
    }

    [Test]
    public async Task Skips_Structurals_Inside_Strings()
    {
        var ix = IndexOf("\"a:b,c{}\"");
        // Only the opening quote (offset 0) should be reported.
        await Assert.That(ix).IsEquivalentTo(new[] { 0 });
    }

    [Test]
    public async Task Handles_Escaped_Quotes()
    {
        var ix = IndexOf("\"a\\\"b\""); // "a\"b"
        // The escaped " is not a string boundary; only opening quote at 0 is reported.
        await Assert.That(ix).IsEquivalentTo(new[] { 0 });
    }

    [Test]
    public async Task Identifies_Atom_Starts()
    {
        var ix = IndexOf("[true,false,null,42]");
        // [, t, f, n, 4, ]
        await Assert.That(ix).IsEquivalentTo(new[] { 0, 1, 5, 6, 11, 12, 16, 17, 19 });
    }
}
