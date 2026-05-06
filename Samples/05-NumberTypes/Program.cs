// 05 – Number Types
// Demonstrates GetNumberType, IsNegative, IsInteger, GetNumber (JsonNumber),
// GetRawJsonToken, and numbers-in-strings helpers.

using SimdJson;

Console.WriteLine("=== 05 – Number Types ===\n");

const string json = """
{
  "double":    3.14,
  "signed":   -42,
  "unsigned":  18446744073709551615,
  "big":       99999999999999999999,
  "zero":      0,
  "negDouble": -2.718,
  "quoted":   "3.14"
}
""";

// Use a dedicated parser so SimdJsonParser.Shared calls below don't invalidate this doc.
using var mainParser = new SimdJsonParser();
using var doc = mainParser.Parse(json);

// ── 1. GetNumberType to choose the right getter ───────────────────────────
Console.WriteLine("── GetNumberType ──");

string[] keys = ["double", "signed", "unsigned", "big", "zero", "negDouble"];
foreach (var key in keys)
{
    doc.Rewind();
    using var val  = doc.GetField(key);
    var type = val.GetNumberType();
    string reading = type switch
    {
        JsonNumberType.FloatingPoint  => val.GetDouble().ToString("G"),
        JsonNumberType.SignedInteger   => val.GetInt64().ToString(),
        JsonNumberType.UnsignedInteger => val.GetUInt64().ToString(),
        JsonNumberType.BigInteger      => val.GetRawJsonToken(),   // too big for 64-bit
        _ => "?"
    };
    Console.WriteLine($"  {key,-10} -> {type,-15} = {reading}");
}

// ── 2. GetNumber — type and value in one call ─────────────────────────────
Console.WriteLine();
Console.WriteLine("── GetNumber (JsonNumber) ──");
doc.Rewind();
using var numVal = doc.GetField("double");
JsonNumber num = numVal.GetNumber();
Console.WriteLine($"  NumberType : {num.NumberType}");
Console.WriteLine($"  AsDouble() : {num.AsDouble()}");
Console.WriteLine($"  ToString() : {num}");

doc.Rewind();
using var numVal2 = doc.GetField("signed");
JsonNumber num2 = numVal2.GetNumber();
Console.WriteLine($"  NumberType : {num2.NumberType}");
Console.WriteLine($"  AsInt64()  : {num2.AsInt64()}");

// ── 3. IsNegative / IsInteger on the document root level ─────────────────
Console.WriteLine();
Console.WriteLine("── Document-level IsNegative / IsInteger ──");
using var singleNeg = SimdJsonParser.Shared.Parse("-7");
Console.WriteLine($"  -7  IsNegative={singleNeg.IsNegative()}, IsInteger={singleNeg.IsInteger()}");
using var singlePi  = SimdJsonParser.Shared.Parse("3.14");
Console.WriteLine($"  3.14 IsNegative={singlePi.IsNegative()}, IsInteger={singlePi.IsInteger()}");

// ── 4. GetRawJsonToken — big integer / exact representation ───────────────
Console.WriteLine();
Console.WriteLine("── GetRawJsonToken (BigInteger) ──");
doc.Rewind();
using var bigVal = doc.GetField("big");
Console.WriteLine($"  raw token: {bigVal.GetRawJsonToken()}");

// ── 5. Numbers stored as strings ("quoted numbers") ──────────────────────
Console.WriteLine();
Console.WriteLine("── Numbers-in-strings ──");
doc.Rewind();
using var quoted = doc.GetField("quoted");
Console.WriteLine($"  raw string    : {quoted.GetString()}");
Console.WriteLine($"  as double     : {quoted.GetDoubleInString()}");

// ── 6. All typed getters on a value ──────────────────────────────────────
Console.WriteLine();
Console.WriteLine("── Typed getters ──");
using var intDoc = SimdJsonParser.Shared.Parse("""{"n":255}""");
using var n      = intDoc.GetField("n");
Console.WriteLine($"  GetInt64  : {n.GetInt64()}");
Console.WriteLine($"  GetUInt64 : {n.GetUInt64()}");
Console.WriteLine($"  GetInt32  : {n.GetInt32()}");
Console.WriteLine($"  GetDouble : {n.GetDouble()}");
Console.WriteLine($"  GetFloat  : {n.GetFloat()}");
Console.WriteLine($"  GetDecimal: {n.GetDecimal()}");
