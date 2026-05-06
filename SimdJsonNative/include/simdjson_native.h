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

#ifdef __cplusplus
} // extern "C"
#endif
