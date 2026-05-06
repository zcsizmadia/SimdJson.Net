#pragma once

#include <stdint.h>
#include <stddef.h>

// Platform-specific export/import macros
#if defined(_WIN32) || defined(__CYGWIN__)
  #ifdef SIMDJSONNATIVE_EXPORTS
    #define SJNATIVE_API __declspec(dllexport)
  #else
    #define SJNATIVE_API __declspec(dllimport)
  #endif
  #define SJNATIVE_CALL __cdecl
#else
  #ifdef SIMDJSONNATIVE_EXPORTS
    #define SJNATIVE_API __attribute__((visibility("default")))
  #else
    #define SJNATIVE_API
  #endif
  #define SJNATIVE_CALL
#endif

#ifdef __cplusplus
extern "C" {
#endif

// ─── Opaque handle types ────────────────────────────────────────────────────
// All handles are heap-allocated C++ objects exposed as void*.
// The caller must pair every Create/Open call with the matching Destroy call.

typedef void* SimdJsonParser;    // ondemand::parser  (reusable, one doc at a time)
typedef void* SimdJsonDocument;  // ondemand::document + owning padded_string
typedef void* SimdJsonValue;     // ondemand::value   (temporary – invalid after doc moves)
typedef void* SimdJsonArray;     // ondemand::array   iterator
typedef void* SimdJsonObject;    // ondemand::object  iterator
typedef void* SimdJsonArrayIter; // iterator over an array
typedef void* SimdJsonObjectIter;// iterator over an object

// ─── Error codes ────────────────────────────────────────────────────────────
// Mirrors simdjson::error_code values (cast to int32_t).
// SUCCESS == 0, all error conditions are > 0.
typedef int32_t SimdJsonError;

#define SIMDJSON_BRIDGE_SUCCESS                0
#define SIMDJSON_BRIDGE_ERR_CAPACITY          -1
#define SIMDJSON_BRIDGE_ERR_INCORRECT_TYPE    -2
#define SIMDJSON_BRIDGE_ERR_NO_SUCH_FIELD     -3
#define SIMDJSON_BRIDGE_ERR_INDEX_OUT_OF_BOUNDS -4
#define SIMDJSON_BRIDGE_ERR_NULL_POINTER      -5
#define SIMDJSON_BRIDGE_ERR_PARSE_ERROR       -6
#define SIMDJSON_BRIDGE_ERR_ITERATION_ERROR   -7
#define SIMDJSON_BRIDGE_ERR_INVALID_POINTER   -8   // INVALID_JSON_POINTER
#define SIMDJSON_BRIDGE_ERR_SCALAR_DOCUMENT   -9   // SCALAR_DOCUMENT_AS_VALUE
#define SIMDJSON_BRIDGE_ERR_UNKNOWN           -99

// ─── JSON value types ────────────────────────────────────────────────────────
typedef enum SimdJsonType {
    SIMDJSON_TYPE_ARRAY   = 0,
    SIMDJSON_TYPE_OBJECT  = 1,
    SIMDJSON_TYPE_NUMBER  = 2,
    SIMDJSON_TYPE_STRING  = 3,
    SIMDJSON_TYPE_BOOLEAN = 4,
    SIMDJSON_TYPE_NULL    = 5,
    SIMDJSON_TYPE_UNKNOWN = 6
} SimdJsonType;

// ─── JSON number sub-types ───────────────────────────────────────────────────
typedef enum SimdJsonNumberType {
    SIMDJSON_NUMBER_TYPE_FLOATING_POINT   = 0,   // double
    SIMDJSON_NUMBER_TYPE_SIGNED_INTEGER   = 1,   // int64
    SIMDJSON_NUMBER_TYPE_UNSIGNED_INTEGER = 2,   // uint64 (>= 2^63)
    SIMDJSON_NUMBER_TYPE_BIG_INTEGER      = 3    // integer outside 64-bit range
} SimdJsonNumberType;

// ─── Library info ────────────────────────────────────────────────────────────

/** Returns the simdjson library version string (e.g. "3.9.0"). */
SJNATIVE_API const char* SJNATIVE_CALL SimdJsonNative_GetVersion(void);

// ─── Parser lifecycle ────────────────────────────────────────────────────────

/**
 * Creates a new reusable parser.
 * One parser should be used per thread; it can parse one document at a time.
 */
SJNATIVE_API SimdJsonParser SJNATIVE_CALL SimdJsonNative_CreateParser(void);

/** Destroys a parser created by SimdJsonNative_CreateParser. */
SJNATIVE_API void SJNATIVE_CALL SimdJsonNative_DestroyParser(SimdJsonParser parser);

// ─── Document lifecycle ──────────────────────────────────────────────────────

/**
 * Parses a JSON string and returns an owning document handle.
 * The library makes an internal padded copy of the input buffer, so the caller
 * does not need to keep the buffer alive after this call returns.
 *
 * @param parser   Parser handle (must not be in use by another document).
 * @param json     Pointer to the UTF-8 JSON text.
 * @param length   Byte length of the JSON text (without a null terminator).
 * @param out_doc  Receives the document handle on success.
 * @return SIMDJSON_BRIDGE_SUCCESS on success, an error code otherwise.
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_Parse(
    SimdJsonParser parser,
    const char*    json,
    size_t         length,
    SimdJsonDocument* out_doc);

/** Destroys a document returned by SimdJsonNative_Parse. */
SJNATIVE_API void SJNATIVE_CALL SimdJsonNative_DestroyDocument(SimdJsonDocument doc);

// ─── Document root access ────────────────────────────────────────────────────

/**
 * Returns the JSON type of the document root.
 * (array / object / number / string / boolean / null)
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetType(
    SimdJsonDocument doc, SimdJsonType* out_type);

/** Gets the root as an array iterator. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetArray(
    SimdJsonDocument doc, SimdJsonArray* out_array);

/** Gets the root as an object iterator. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetObject(
    SimdJsonDocument doc, SimdJsonObject* out_object);

/** Gets a field of the root object by key (order-insensitive lookup). */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetFieldByKey(
    SimdJsonDocument doc, const char* key, SimdJsonValue* out_value);

/** Gets an array element at a JSON Pointer path (e.g. "/0/name"). */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentAtPointer(
    SimdJsonDocument doc, const char* json_pointer, SimdJsonValue* out_value);

// ─── Value inspection ────────────────────────────────────────────────────────

SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetType(
    SimdJsonValue value, SimdJsonType* out_type);

SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetString(
    SimdJsonValue value, const char** out_ptr, size_t* out_len);

SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetDouble(
    SimdJsonValue value, double* out_val);

SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetInt64(
    SimdJsonValue value, int64_t* out_val);

SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetUInt64(
    SimdJsonValue value, uint64_t* out_val);

SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetBool(
    SimdJsonValue value, int32_t* out_val);  // 1 = true, 0 = false

SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueIsNull(
    SimdJsonValue value, int32_t* out_is_null);

SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetArray(
    SimdJsonValue value, SimdJsonArray* out_array);

SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetObject(
    SimdJsonValue value, SimdJsonObject* out_object);

/** Gets a child field by key from a value that is an object. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetFieldByKey(
    SimdJsonValue value, const char* key, SimdJsonValue* out_value);

// ─── Array iteration ─────────────────────────────────────────────────────────

/**
 * Opens an iterator over an array.
 * The iterator must be destroyed with SimdJsonNative_DestroyArrayIter when done.
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ArrayBegin(
    SimdJsonArray array, SimdJsonArrayIter* out_iter);

/**
 * Advances the iterator and retrieves the next value.
 * Returns SIMDJSON_BRIDGE_SUCCESS and sets *out_value while elements remain.
 * When the array is exhausted, *out_done is set to 1.
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ArrayIterNext(
    SimdJsonArrayIter iter, SimdJsonValue* out_value, int32_t* out_done);

SJNATIVE_API void SJNATIVE_CALL SimdJsonNative_DestroyArrayIter(SimdJsonArrayIter iter);

/** Returns the number of elements in the array (requires a full scan). */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ArrayCount(
    SimdJsonArray array, size_t* out_count);

// ─── Object iteration ────────────────────────────────────────────────────────

/**
 * Opens an iterator over an object's fields.
 * The iterator must be destroyed with SimdJsonNative_DestroyObjectIter when done.
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectBegin(
    SimdJsonObject object, SimdJsonObjectIter* out_iter);

/**
 * Advances the iterator.
 * On each step, *out_key_ptr / *out_key_len receive the unescaped field key and
 * *out_value receives the field value.
 * When all fields are visited, *out_done is set to 1.
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectIterNext(
    SimdJsonObjectIter iter,
    const char** out_key_ptr, size_t* out_key_len,
    SimdJsonValue* out_value,
    int32_t* out_done);

SJNATIVE_API void SJNATIVE_CALL SimdJsonNative_DestroyObjectIter(SimdJsonObjectIter iter);

/** Returns the number of fields in the object (requires a full scan). */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectCount(
    SimdJsonObject object, size_t* out_count);

/** Gets a field from an object by key (order-insensitive lookup). */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectGetFieldByKey(
    SimdJsonObject object, const char* key, SimdJsonValue* out_value);

// ─── Value cleanup ───────────────────────────────────────────────────────────

/**
 * Releases a SimdJsonValue, SimdJsonArray, or SimdJsonObject handle.
 * String pointers returned by SimdJsonNative_ValueGetString point into the
 * document buffer and remain valid until the document is destroyed.
 */
SJNATIVE_API void SJNATIVE_CALL SimdJsonNative_DestroyValue(SimdJsonValue value);
SJNATIVE_API void SJNATIVE_CALL SimdJsonNative_DestroyArray(SimdJsonArray array);
SJNATIVE_API void SJNATIVE_CALL SimdJsonNative_DestroyObject(SimdJsonObject object);

// ─── Number type inspection ──────────────────────────────────────────────────

/** Returns the sub-type of a number value (float / signed / unsigned / big). */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetNumberType(
    SimdJsonValue value, SimdJsonNumberType* out_type);

/** Returns 1 if the number value is negative, 0 otherwise. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueIsNegative(
    SimdJsonValue value, int32_t* out_val);

/** Returns 1 if the number value is an integer (no fractional part), 0 otherwise. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueIsInteger(
    SimdJsonValue value, int32_t* out_val);

// ─── Raw JSON access ─────────────────────────────────────────────────────────

/**
 * Returns the raw JSON token for a scalar value (quotes included for strings).
 * For arrays/objects returns only the opening '[' or '{'.
 * The pointer is valid for the lifetime of the owning document.
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueRawJsonToken(
    SimdJsonValue value, const char** out_ptr, size_t* out_len);

/**
 * Returns the full raw JSON for a value (traverses arrays/objects).
 * The pointer is valid for the lifetime of the owning document.
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueRawJson(
    SimdJsonValue value, const char** out_ptr, size_t* out_len);

/** Returns the full raw JSON for an array (consumes the iterator). */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ArrayRawJson(
    SimdJsonArray array, const char** out_ptr, size_t* out_len);

/** Returns the full raw JSON for an object (consumes the iterator). */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectRawJson(
    SimdJsonObject object, const char** out_ptr, size_t* out_len);

/** Returns the full raw JSON for the document root. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentRawJson(
    SimdJsonDocument doc, const char** out_ptr, size_t* out_len);

// ─── Numbers encoded as strings ──────────────────────────────────────────────

/** Parses a number from a quoted JSON string value into a double. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetDoubleInString(
    SimdJsonValue value, double* out_val);

/** Parses a number from a quoted JSON string value into an int64. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetInt64InString(
    SimdJsonValue value, int64_t* out_val);

/** Parses a number from a quoted JSON string value into a uint64. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetUInt64InString(
    SimdJsonValue value, uint64_t* out_val);

// ─── JSON Pointer and JSONPath ───────────────────────────────────────────────

/** Gets a value via a JSONPath expression on the document root. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentAtPath(
    SimdJsonDocument doc, const char* json_path, SimdJsonValue* out_value);

/** Gets a value via a JSON Pointer path starting from a value. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueAtPointer(
    SimdJsonValue value, const char* json_pointer, SimdJsonValue* out_value);

/** Gets a value via a JSONPath expression starting from a value. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueAtPath(
    SimdJsonValue value, const char* json_path, SimdJsonValue* out_value);

/** Gets a value via a JSON Pointer path starting from an object. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectAtPointer(
    SimdJsonObject object, const char* json_pointer, SimdJsonValue* out_value);

/** Gets a value via a JSONPath expression starting from an object. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectAtPath(
    SimdJsonObject object, const char* json_path, SimdJsonValue* out_value);

// ─── Order-sensitive field lookup ────────────────────────────────────────────

/**
 * Searches forward from the current iterator position for a field with
 * the given key (does not rewind). Faster than the unordered variant when
 * fields are accessed in declaration order.
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentFindField(
    SimdJsonDocument doc, const char* key, SimdJsonValue* out_value);

SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueFindField(
    SimdJsonValue value, const char* key, SimdJsonValue* out_value);

SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectFindField(
    SimdJsonObject object, const char* key, SimdJsonValue* out_value);

// ─── Document rewind ─────────────────────────────────────────────────────────

/** Rewinds the document iterator to the start. Allows re-reading the document. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentRewind(SimdJsonDocument doc);

// ─── Array and Object index/reset ────────────────────────────────────────────

/** Returns the element at a zero-based index without iterating from the start. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ArrayAt(
    SimdJsonArray array, size_t index, SimdJsonValue* out_value);

/** Resets the array iterator so it can be traversed again. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ArrayReset(SimdJsonArray array);

/** Resets the object iterator so it can be traversed again. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectReset(SimdJsonObject object);

// ─── Utilities ───────────────────────────────────────────────────────────────

/**
 * Minifies JSON by removing all unnecessary whitespace.
 * dst must point to a buffer of at least src_len bytes.
 * On success, *out_new_len receives the number of bytes written.
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_Minify(
    const char* src, size_t src_len, char* dst, size_t* out_new_len);

/**
 * Returns 1 if the bytes constitute valid UTF-8, 0 otherwise.
 * Does not parse JSON; pure UTF-8 validation.
 */
SJNATIVE_API int32_t SJNATIVE_CALL SimdJsonNative_ValidateUtf8(
    const char* src, size_t src_len);

// ─── JSON number value struct ─────────────────────────────────────────────────
// Returned by SimdJsonNative_ValueGetNumber. Explicit padding ensures ABI
// stability across MSVC / GCC / Clang on both 32-bit and 64-bit platforms.
typedef struct SimdJsonNumber {
    int32_t  type;    /* SimdJsonNumberType */
    int32_t  _pad;    /* explicit alignment padding (do not use) */
    union {
        double   floating_point;
        int64_t  signed_integer;
        uint64_t unsigned_integer;
    } value;
} SimdJsonNumber;

// ─── Type predicates ─────────────────────────────────────────────────────────

/** Returns 1 if the value is a scalar (not array or object), 0 otherwise. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueIsScalar(
    SimdJsonValue value, int32_t* out_val);

/** Returns 1 if the value is a JSON string, 0 otherwise. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueIsString(
    SimdJsonValue value, int32_t* out_val);

/** Returns 1 if the array contains no elements, 0 otherwise. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ArrayIsEmpty(
    SimdJsonArray array, int32_t* out_val);

/** Returns 1 if the document root is a scalar (not array or object), 0 otherwise. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentIsScalar(
    SimdJsonDocument doc, int32_t* out_val);

/** Returns 1 if the document root is a JSON string, 0 otherwise. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentIsString(
    SimdJsonDocument doc, int32_t* out_val);

// ─── Document root as value ──────────────────────────────────────────────────

/**
 * Returns the document root as a generic SimdJsonValue handle.
 * Fails with SIMDJSON_BRIDGE_ERR_SCALAR_DOCUMENT if the root is an array or object;
 * use DocumentGetArray / DocumentGetObject in those cases.
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetValue(
    SimdJsonDocument doc, SimdJsonValue* out_value);

// ─── Parse location / depth ──────────────────────────────────────────────────

/**
 * Returns the current parse position as a byte offset from the start of the
 * document's JSON buffer. Requires the owning document handle to compute the offset.
 * Useful for error reporting ("error at byte N").
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueCurrentOffset(
    SimdJsonValue value, SimdJsonDocument doc, size_t* out_offset);

/** Returns the current JSON nesting depth of a value (0 = at root level). */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueCurrentDepth(
    SimdJsonValue value, int32_t* out_depth);

/** Returns the current parse offset (in bytes from document start) for the document. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentCurrentOffset(
    SimdJsonDocument doc, size_t* out_offset);

/** Returns the current JSON nesting depth for the document (0 = at root level). */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentCurrentDepth(
    SimdJsonDocument doc, int32_t* out_depth);

// ─── Parser configuration ────────────────────────────────────────────────────

/**
 * Creates a parser with a custom maximum document capacity (bytes).
 * Pass 0 to use the simdjson default (SIMDJSON_MAXSIZE_BYTES, typically 4 GiB).
 */
SJNATIVE_API SimdJsonParser SJNATIVE_CALL SimdJsonNative_CreateParserWithCapacity(
    size_t max_capacity);

/** Returns the current internal buffer capacity (0 if no document has been parsed yet). */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ParserCapacity(
    SimdJsonParser parser, size_t* out_capacity);

/** Returns the maximum allowed document capacity. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ParserMaxCapacity(
    SimdJsonParser parser, size_t* out_max_capacity);

/** Sets the maximum allowed document capacity. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ParserSetMaxCapacity(
    SimdJsonParser parser, size_t max_capacity);

/** Returns the maximum JSON nesting depth the parser supports (compile-time constant). */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ParserMaxDepth(
    SimdJsonParser parser, size_t* out_max_depth);

// ─── Structured number ───────────────────────────────────────────────────────

/**
 * Gets the full typed number value in a single call.
 * For big-integer values (type == SIMDJSON_NUMBER_TYPE_BIG_INTEGER),
 * use ValueRawJsonToken to retrieve the decimal string representation.
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetNumber(
    SimdJsonValue value, SimdJsonNumber* out_number);

// ─── Wobbly / WTF-8 strings ──────────────────────────────────────────────────

/**
 * Returns the string value allowing lone Unicode surrogates (WTF-8 / CESU-8).
 * The returned bytes may not be valid UTF-8. Use for round-tripping JSON that
 * was produced by runtimes (e.g. Java) that emit lone surrogates.
 * The pointer is valid for the lifetime of the owning document.
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetWobblyString(
    SimdJsonValue value, const char** out_ptr, size_t* out_len);

/** Gets the document root as a wobbly string. Only valid when the root is a JSON string. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetWobblyString(
    SimdJsonDocument doc, const char** out_ptr, size_t* out_len);

// ─── Array pointer / path navigation ─────────────────────────────────────────

/** Gets a value via a JSON Pointer path starting from an array (e.g. "/0/name"). */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ArrayAtPointer(
    SimdJsonArray array, const char* json_pointer, SimdJsonValue* out_value);

/** Gets a value via a JSONPath expression starting from an array (e.g. "$[0].name"). */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ArrayAtPath(
    SimdJsonArray array, const char* json_path, SimdJsonValue* out_value);

// ─── Object predicates ───────────────────────────────────────────────────────

/** Returns 1 if the object contains no fields, 0 otherwise. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectIsEmpty(
    SimdJsonObject object, int32_t* out_val);

// ─── find_field_unordered ────────────────────────────────────────────────────

/**
 * Searches the document root for a field by key without requiring fields to be
 * in order (may rewind). Equivalent to document::find_field_unordered().
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentFindFieldUnordered(
    SimdJsonDocument doc, const char* key, SimdJsonValue* out_value);

/**
 * Searches a value (object context) for a field by key without requiring fields
 * to be in order. Equivalent to value::find_field_unordered().
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueFindFieldUnordered(
    SimdJsonValue value, const char* key, SimdJsonValue* out_value);

/**
 * Searches an object for a field by key without requiring fields to be in order.
 * Equivalent to object::find_field_unordered().
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectFindFieldUnordered(
    SimdJsonObject object, const char* key, SimdJsonValue* out_value);

// ─── Document number helpers ─────────────────────────────────────────────────

/** Returns the number type of the document root (only valid when root is a number). */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetNumberType(
    SimdJsonDocument doc, int32_t* out_type);

/** Returns 1 if the document root number is negative, 0 otherwise. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentIsNegative(
    SimdJsonDocument doc, int32_t* out_val);

/** Returns 1 if the document root number is an integer (no fractional part), 0 otherwise. */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentIsInteger(
    SimdJsonDocument doc, int32_t* out_val);

/**
 * Gets the full typed number from the document root in a single call.
 * For big-integer values use DocumentRawJsonToken to retrieve the decimal string.
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetNumber(
    SimdJsonDocument doc, SimdJsonNumber* out_number);

/**
 * Returns the raw JSON token for the document root (e.g. the literal number or string token).
 * The pointer is valid for the lifetime of the document.
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentRawJsonToken(
    SimdJsonDocument doc, const char** out_ptr, size_t* out_len);

// ─── Parser – allow incomplete JSON ──────────────────────────────────────────

/**
 * Parses JSON that may be truncated (e.g. a partial download).
 * Equivalent to parser::iterate_allow_incomplete_json().
 * The returned document handle must be destroyed with SimdJsonNative_DestroyDocument.
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ParseAllowIncompleteJson(
    SimdJsonParser parser, const char* json, size_t length, SimdJsonDocument* out_doc);

// ─── Raw JSON string (without unescaping) ────────────────────────────────────

/**
 * Returns the raw (still-escaped) bytes of a JSON string value, without the
 * surrounding quote characters. For example, the JSON value "hello\nworld"
 * yields bytes [h,e,l,l,o,\,n,w,o,r,l,d] (backslash + n, not a newline).
 * The pointer is valid for the lifetime of the owning document.
 * Fails with SIMDJSON_BRIDGE_ERR_INCORRECT_TYPE if the value is not a string.
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetRawJsonString(
    SimdJsonValue value, const char** out_ptr, size_t* out_len);

// ─── Wildcard path iteration ──────────────────────────────────────────────────

/**
 * Callback invoked for each value matching a JSONPath wildcard expression.
 * The borrowed_value handle is valid ONLY for the duration of the callback;
 * the caller must NOT destroy it (it is stack-allocated in the bridge).
 * context is the user-data pointer passed to SimdJsonNative_DocumentForEachAtPath /
 * SimdJsonNative_ValueForEachAtPath.
 */
typedef void (SJNATIVE_CALL *SimdJsonWildcardCallback)(void* borrowed_value, void* context);

/**
 * Iterates over all values matching json_path (e.g. "$.items[*]") starting from the
 * document root and calls callback for each match.
 * path_len is the byte length of the UTF-8 path string (without null terminator).
 * The document is rewound before iteration.
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentForEachAtPath(
    SimdJsonDocument doc, const char* path, size_t path_len,
    SimdJsonWildcardCallback callback, void* context);

/**
 * Iterates over all values matching json_path starting from a value (which must be
 * an array or object) and calls callback for each match.
 * path_len is the byte length of the UTF-8 path string (without null terminator).
 */
SJNATIVE_API SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueForEachAtPath(
    SimdJsonValue value, const char* path, size_t path_len,
    SimdJsonWildcardCallback callback, void* context);

#ifdef __cplusplus
} // extern "C"
#endif
