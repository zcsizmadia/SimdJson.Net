using System.Runtime.InteropServices;

namespace SimdJson.Internal;

/// <summary>
/// Low-level P/Invoke bindings for SimdJsonNative.dll/.so/.dylib.
/// All methods load the library on first use via <see cref="NativeLoader"/>.
/// Function pointers are resolved lazily at class initialisation.
/// </summary>
internal static unsafe partial class NativeMethods
{
    private const string Lib = "SimdJsonNative";

    static NativeMethods() => NativeLoader.EnsureLoaded();

    // ── Blittable struct matching SimdJsonNumber in simdjson_native.h ──────

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct NativeNumber
    {
        [FieldOffset(0)] public int    Type;   // SimdJsonNumberType
        [FieldOffset(4)] public int    Pad;
        [FieldOffset(8)] public double FloatingPoint;
        [FieldOffset(8)] public long   SignedInteger;
        [FieldOffset(8)] public ulong  UnsignedInteger;
    }

    // ── Library info ──────────────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_GetVersion")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial byte* GetVersion();

    // ── Parser lifecycle ──────────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_CreateParser")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial nint CreateParser();

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DestroyParser")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial void DestroyParser(nint parser);

    // ── Document lifecycle ────────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_Parse")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int Parse(nint parser, byte* json, nuint length, out nint outDoc);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DestroyDocument")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial void DestroyDocument(nint doc);

    // ── Document root access ──────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DocumentGetType")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int DocumentGetType(nint doc, out JsonValueKind outType);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DocumentGetArray")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int DocumentGetArray(nint doc, out nint outArray);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DocumentGetObject")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int DocumentGetObject(nint doc, out nint outObject);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DocumentGetFieldByKey")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int DocumentGetFieldByKey(nint doc, byte* key, out nint outValue);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DocumentAtPointer")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int DocumentAtPointer(nint doc, byte* pointer, out nint outValue);

    // ── Value inspection ──────────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueGetType")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueGetType(nint value, out JsonValueKind outType);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueGetString")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueGetString(nint value, out byte* outPtr, out nuint outLen);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueGetDouble")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueGetDouble(nint value, out double outVal);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueGetInt64")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueGetInt64(nint value, out long outVal);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueGetUInt64")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueGetUInt64(nint value, out ulong outVal);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueGetBool")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueGetBool(nint value, out int outVal);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueIsNull")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueIsNull(nint value, out int outIsNull);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueGetArray")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueGetArray(nint value, out nint outArray);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueGetObject")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueGetObject(nint value, out nint outObject);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueGetFieldByKey")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueGetFieldByKey(nint value, byte* key, out nint outValue);

    // ── Array iteration ───────────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ArrayBegin")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ArrayBegin(nint array, out nint outIter);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ArrayIterNext")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ArrayIterNext(nint iter, out nint outValue, out int outDone);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DestroyArrayIter")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial void DestroyArrayIter(nint iter);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ArrayCount")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ArrayCount(nint array, out nuint outCount);

    // ── Object iteration ──────────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ObjectBegin")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ObjectBegin(nint obj, out nint outIter);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ObjectIterNext")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ObjectIterNext(
        nint iter,
        out byte* outKeyPtr, out nuint outKeyLen,
        out nint outValue,
        out int outDone);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DestroyObjectIter")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial void DestroyObjectIter(nint iter);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ObjectCount")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ObjectCount(nint obj, out nuint outCount);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ObjectGetFieldByKey")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ObjectGetFieldByKey(nint obj, byte* key, out nint outValue);

    // ── Cleanup ───────────────────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DestroyValue")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial void DestroyValue(nint value);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DestroyArray")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial void DestroyArray(nint array);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DestroyObject")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial void DestroyObject(nint obj);

    // ── Number type inspection ────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueGetNumberType")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueGetNumberType(nint value, out JsonNumberType outType);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueIsNegative")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueIsNegative(nint value, out int outVal);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueIsInteger")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueIsInteger(nint value, out int outVal);

    // ── Raw JSON access ───────────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueRawJsonToken")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueRawJsonToken(nint value, out byte* outPtr, out nuint outLen);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueRawJson")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueRawJson(nint value, out byte* outPtr, out nuint outLen);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ArrayRawJson")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ArrayRawJson(nint array, out byte* outPtr, out nuint outLen);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ObjectRawJson")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ObjectRawJson(nint obj, out byte* outPtr, out nuint outLen);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DocumentRawJson")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int DocumentRawJson(nint doc, out byte* outPtr, out nuint outLen);

    // ── Numbers encoded as strings ────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueGetDoubleInString")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueGetDoubleInString(nint value, out double outVal);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueGetInt64InString")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueGetInt64InString(nint value, out long outVal);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueGetUInt64InString")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueGetUInt64InString(nint value, out ulong outVal);

    // ── JSON Pointer and JSONPath ──────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DocumentAtPath")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int DocumentAtPath(nint doc, byte* jsonPath, out nint outValue);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueAtPointer")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueAtPointer(nint value, byte* jsonPointer, out nint outValue);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueAtPath")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueAtPath(nint value, byte* jsonPath, out nint outValue);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ObjectAtPointer")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ObjectAtPointer(nint obj, byte* jsonPointer, out nint outValue);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ObjectAtPath")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ObjectAtPath(nint obj, byte* jsonPath, out nint outValue);

    // ── Order-sensitive field lookup ──────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DocumentFindField")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int DocumentFindField(nint doc, byte* key, out nint outValue);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueFindField")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueFindField(nint value, byte* key, out nint outValue);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ObjectFindField")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ObjectFindField(nint obj, byte* key, out nint outValue);

    // ── Document rewind ───────────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DocumentRewind")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int DocumentRewind(nint doc);

    // ── Array and Object index/reset ──────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ArrayAt")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ArrayAt(nint array, nuint index, out nint outValue);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ArrayReset")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ArrayReset(nint array);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ObjectReset")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ObjectReset(nint obj);

    // ── Utilities ─────────────────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_Minify")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int Minify(byte* src, nuint srcLen, byte* dst, out nuint outNewLen);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValidateUtf8")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValidateUtf8(byte* src, nuint srcLen);

    // ── Type predicates ───────────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueIsScalar")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueIsScalar(nint value, out int outVal);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueIsString")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueIsString(nint value, out int outVal);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ArrayIsEmpty")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ArrayIsEmpty(nint array, out int outVal);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DocumentIsScalar")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int DocumentIsScalar(nint doc, out int outVal);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DocumentIsString")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int DocumentIsString(nint doc, out int outVal);

    // ── Document root as value ────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DocumentGetValue")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int DocumentGetValue(nint doc, out nint outValue);

    // ── Parse location / depth ────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueCurrentOffset")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueCurrentOffset(nint value, nint doc, out nuint outOffset);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueCurrentDepth")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueCurrentDepth(nint value, out int outDepth);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DocumentCurrentOffset")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int DocumentCurrentOffset(nint doc, out nuint outOffset);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DocumentCurrentDepth")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int DocumentCurrentDepth(nint doc, out int outDepth);

    // ── Parser configuration ──────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_CreateParserWithCapacity")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial nint CreateParserWithCapacity(nuint maxCapacity);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ParserCapacity")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ParserCapacity(nint parser, out nuint outCapacity);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ParserMaxCapacity")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ParserMaxCapacity(nint parser, out nuint outMaxCapacity);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ParserSetMaxCapacity")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ParserSetMaxCapacity(nint parser, nuint maxCapacity);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ParserMaxDepth")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ParserMaxDepth(nint parser, out nuint outMaxDepth);

    // ── Structured number ─────────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueGetNumber")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueGetNumber(nint value, out NativeNumber outNumber);

    // ── Wobbly / WTF-8 strings ────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueGetWobblyString")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueGetWobblyString(nint value, out byte* outPtr, out nuint outLen);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DocumentGetWobblyString")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int DocumentGetWobblyString(nint doc, out byte* outPtr, out nuint outLen);

    // ── Array pointer / path navigation ──────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ArrayAtPointer")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ArrayAtPointer(nint array, byte* jsonPointer, out nint outValue);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ArrayAtPath")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ArrayAtPath(nint array, byte* jsonPath, out nint outValue);

    // ── Object predicates ─────────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ObjectIsEmpty")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ObjectIsEmpty(nint obj, out int outVal);

    // ── find_field_unordered ──────────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DocumentFindFieldUnordered")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int DocumentFindFieldUnordered(nint doc, byte* key, out nint outValue);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueFindFieldUnordered")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueFindFieldUnordered(nint value, byte* key, out nint outValue);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ObjectFindFieldUnordered")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ObjectFindFieldUnordered(nint obj, byte* key, out nint outValue);

    // ── Document number helpers ───────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DocumentGetNumberType")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int DocumentGetNumberType(nint doc, out int outType);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DocumentIsNegative")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int DocumentIsNegative(nint doc, out int outVal);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DocumentIsInteger")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int DocumentIsInteger(nint doc, out int outVal);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DocumentGetNumber")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int DocumentGetNumber(nint doc, out NativeNumber outNumber);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DocumentRawJsonToken")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int DocumentRawJsonToken(nint doc, out byte* outPtr, out nuint outLen);

    // ── Parser – allow incomplete JSON ────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ParseAllowIncompleteJson")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ParseAllowIncompleteJson(nint parser, byte* json, nuint length, out nint outDoc);

    // ── Raw JSON string (without unescaping) ──────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueGetRawJsonString")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueGetRawJsonString(nint value, out byte* outPtr, out nuint outLen);

    // ── Wildcard path iteration ───────────────────────────────────────────

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_DocumentForEachAtPath")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int DocumentForEachAtPath(nint doc, byte* path, nuint pathLen, nint callback, nint context);

    [LibraryImport(Lib, EntryPoint = "SimdJsonNative_ValueForEachAtPath")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial int ValueForEachAtPath(nint value, byte* path, nuint pathLen, nint callback, nint context);
}
