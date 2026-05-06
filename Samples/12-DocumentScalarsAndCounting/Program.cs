// 12 – Document Scalar Getters, CountElements/CountFields, and GetInt32/GetUInt32
// Demonstrates:
//   - doc.GetString / GetBool / GetDouble / GetInt64 / GetUInt64 / IsNull
//     for JSON documents whose root is a scalar value
//   - doc.GetInt64InString / GetDoubleInString / GetUInt64InString
//     for numbers stored as JSON strings
//   - doc.At(index)          — access array element by index directly on a document
//   - doc.CountElements()    — count array elements (requires a full scan)
//   - doc.CountFields()      — count object fields (requires a full scan)
//   - val.CountElements()    — same but starting from a JsonValue
//   - val.CountFields()      — same but starting from a JsonValue
//   - val.GetInt32()         — native 32-bit signed integer getter
//   - val.GetUInt32()        — native 32-bit unsigned integer getter
//   - val.GetString(allowReplacement: true) — allow lone surrogates in strings

using SimdJson;

Console.WriteLine("=== 12 – Document Scalar Getters, Counting & Int32/UInt32 ===\n");

// ─── 1. Root-scalar documents ─────────────────────────────────────────────
// simdjson On-Demand can parse documents whose root is a bare scalar.
Console.WriteLine("── 1. Root-scalar documents ──");

using var docStr   = SimdJsonParser.Shared.Parse("""  "hello world"  """);
using var docBool  = SimdJsonParser.Shared.Parse("true");
using var docNull  = SimdJsonParser.Shared.Parse("null");
using var docDbl   = SimdJsonParser.Shared.Parse("3.14");
using var docInt   = SimdJsonParser.Shared.Parse("-99");
using var docUInt  = SimdJsonParser.Shared.Parse("18446744073709551615");

Console.WriteLine($"string : {docStr.GetString()}");         // hello world
Console.WriteLine($"bool   : {docBool.GetBool()}");          // True
Console.WriteLine($"null?  : {docNull.IsNull()}");           // True
Console.WriteLine($"double : {docDbl.GetDouble()}");         // 3.14
Console.WriteLine($"int64  : {docInt.GetInt64()}");          // -99
Console.WriteLine($"uint64 : {docUInt.GetUInt64()}");        // 18446744073709551615

// ─── 2. Numbers stored as strings ─────────────────────────────────────────
// GetInt64InString / GetDoubleInString / GetUInt64InString parse the numeric
// value out of a JSON string rather than a JSON number literal.
Console.WriteLine("\n── 2. Numbers stored as strings ──");

using var docIStr = SimdJsonParser.Shared.Parse("""  "-42"  """);
using var docDStr = SimdJsonParser.Shared.Parse("""  "2.718"  """);
using var docUStr = SimdJsonParser.Shared.Parse("""  "100"   """);

Console.WriteLine($"int64 in string   : {docIStr.GetInt64InString()}");    // -42
Console.WriteLine($"double in string  : {docDStr.GetDoubleInString()}");   // 2.718
Console.WriteLine($"uint64 in string  : {docUStr.GetUInt64InString()}");   // 100

// ─── 3. doc.At(index) — direct array access ───────────────────────────────
// At(index) skips to the nth element of a root array without iterating.
Console.WriteLine("\n── 3. doc.At(index) ──");

using var docArr = SimdJsonParser.Shared.Parse("[10, 20, 30, 40, 50]");
using var second = docArr.At(1);
using var last   = new SimdJsonParser().Parse("[10, 20, 30, 40, 50]").At(4);  // fresh parser

Console.WriteLine($"element [1] : {second.GetInt64()}");    // 20
Console.WriteLine($"element [4] : {last.GetInt64()}");      // 50

// ─── 4. CountElements on an array document ────────────────────────────────
// CountElements performs a full scan to count array elements.
// NOTE: after counting the iterator is exhausted; Rewind() resets it.
Console.WriteLine("\n── 4. doc.CountElements() ──");

using var docCount = SimdJsonParser.Shared.Parse("[1, 2, 3, 4, 5, 6, 7]");
int elemCount = docCount.CountElements();
Console.WriteLine($"element count : {elemCount}");           // 7

// ─── 5. CountFields on an object document ─────────────────────────────────
Console.WriteLine("\n── 5. doc.CountFields() ──");

using var docObj = SimdJsonParser.Shared.Parse("""{"x":1,"y":2,"z":3}""");
int fieldCount = docObj.CountFields();
Console.WriteLine($"field count : {fieldCount}");            // 3

// ─── 6. val.CountElements / val.CountFields ───────────────────────────────
Console.WriteLine("\n── 6. val.CountElements() / val.CountFields() ──");

using var docNested = SimdJsonParser.Shared.Parse("""{"nums":[10,20,30],"meta":{"a":1,"b":2}}""");
using var numsVal = docNested.GetField("nums");
using var metaVal = docNested.GetField("meta");

Console.WriteLine($"nums element count : {numsVal.CountElements()}");  // 3
Console.WriteLine($"meta field count   : {metaVal.CountFields()}");    // 2

// ─── 7. GetInt32 / GetUInt32 ─────────────────────────────────────────────
// Direct 32-bit integer getters that throw SimdJsonException on overflow.
Console.WriteLine("\n── 7. val.GetInt32() / val.GetUInt32() ──");

using var docInts = SimdJsonParser.Shared.Parse("""{"signed":-500,"unsigned":4000000000}""");
using var signedVal   = docInts.GetField("signed");
using var unsignedVal = docInts.GetField("unsigned");

Console.WriteLine($"int32  : {signedVal.GetInt32()}");       // -500
Console.WriteLine($"uint32 : {unsignedVal.GetUInt32()}");    // 4000000000

// TryGetInt32 / TryGetUInt32 return false on type mismatch or overflow:
using var docInts2 = SimdJsonParser.Shared.Parse("""{"big":3000000000}""");
using var bigVal = docInts2.GetField("big");
bool ok = bigVal.TryGetInt32(out int result32);
Console.WriteLine($"TryGetInt32 on 3000000000 : ok={ok}");  // ok=False

// ─── 8. GetString(allowReplacement: true) ────────────────────────────────
// Normally GetString() rejects lone surrogates in JSON strings.
// allowReplacement: true replaces them with the Unicode replacement character
// instead of throwing.
Console.WriteLine("\n── 8. GetString(allowReplacement: true) ──");

using var docValid = SimdJsonParser.Shared.Parse("""{"s":"simple string"}""");
using var validStr = docValid.GetField("s");
Console.WriteLine($"GetString(allowReplacement: true) : {validStr.GetString(allowReplacement: true)}");

// ─── 9. GetRawJsonSpan on document, array, object ─────────────────────────
// Returns the raw UTF-8 bytes of the value exactly as they appear in the source.
Console.WriteLine("\n── 9. GetRawJsonSpan ──");

using var docRaw = SimdJsonParser.Shared.Parse("""{"payload":[1,2,3]}""");
using var payloadArr = docRaw.GetField("payload").GetArray();
ReadOnlySpan<byte> arrSpan = payloadArr.GetRawJsonSpan();
Console.WriteLine($"array raw json : {System.Text.Encoding.UTF8.GetString(arrSpan)}");  // [1,2,3]

using var docRaw2 = SimdJsonParser.Shared.Parse("""{"meta":{"v":1}}""");
using var metaObj = docRaw2.GetField("meta").GetObject();
ReadOnlySpan<byte> objSpan = metaObj.GetRawJsonSpan();
Console.WriteLine($"object raw json : {System.Text.Encoding.UTF8.GetString(objSpan)}");  // {"v":1}

using var docRaw3 = new SimdJsonParser().Parse("""{"a":1}""");
ReadOnlySpan<byte> docSpan = docRaw3.GetRawJsonSpan();
Console.WriteLine($"document raw json : {System.Text.Encoding.UTF8.GetString(docSpan)}");  // {"a":1}

Console.WriteLine("\nDone.");
