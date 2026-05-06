// SimdJsonNative – simdjson_native.cpp
// Implements the C API declared in include/simdjson_native.h using the simdjson
// On-Demand parser.  All C++ objects are heap-allocated and exposed as
// opaque void* handles.  Each "Destroy" function must be called exactly
// once for every matching "Create / Get / Begin" call.

#define SIMDJSONNATIVE_EXPORTS   // activate dllexport / visibility("default")

#include "simdjson_native.h"
#include "simdjson.h"

#include <cstring>
#include <memory>
#include <string>

// ─── Internal helpers ────────────────────────────────────────────────────────

static SimdJsonError translate_error(simdjson::error_code ec) noexcept {
    using namespace simdjson;
    switch (ec) {
        case SUCCESS:                    return SIMDJSON_BRIDGE_SUCCESS;
        case CAPACITY:                   return SIMDJSON_BRIDGE_ERR_CAPACITY;
        case INCORRECT_TYPE:             return SIMDJSON_BRIDGE_ERR_INCORRECT_TYPE;
        case NO_SUCH_FIELD:              return SIMDJSON_BRIDGE_ERR_NO_SUCH_FIELD;
        case INDEX_OUT_OF_BOUNDS:        return SIMDJSON_BRIDGE_ERR_INDEX_OUT_OF_BOUNDS;
        case TAPE_ERROR:
        case INCOMPLETE_ARRAY_OR_OBJECT:
        case UTF8_ERROR:
        case UNESCAPED_CHARS:
        case UNCLOSED_STRING:
        case NUMBER_ERROR:               return SIMDJSON_BRIDGE_ERR_PARSE_ERROR;
        case OUT_OF_ORDER_ITERATION:     return SIMDJSON_BRIDGE_ERR_ITERATION_ERROR;
        case INVALID_JSON_POINTER:       return SIMDJSON_BRIDGE_ERR_INVALID_POINTER;
        case SCALAR_DOCUMENT_AS_VALUE:   return SIMDJSON_BRIDGE_ERR_SCALAR_DOCUMENT;
        default:                         return SIMDJSON_BRIDGE_ERR_UNKNOWN;
    }
}

// ─── Internal structs ────────────────────────────────────────────────────────

// Owns the padded JSON buffer + the document produced from it.
struct BridgeDocument {
    simdjson::padded_string   json_buf;   // owns the padded copy of input
    simdjson::ondemand::document doc;     // iterator into json_buf
};

// Wraps an ondemand::value (ephemeral – must be consumed before document moves)
struct BridgeValue {
    simdjson::ondemand::value value;
};

struct BridgeArray {
    simdjson::ondemand::array array;
};

struct BridgeObject {
    simdjson::ondemand::object object;
};

// Array iterator state
struct BridgeArrayIter {
    simdjson::ondemand::array_iterator current;
    simdjson::ondemand::array_iterator end;
    // Keep a reference to the array so the iterator stays valid
    simdjson::ondemand::array array;
    // Advance past previous element at the START of the next IterNext call,
    // not before returning the current value (skip_child would invalidate it).
    bool need_advance = false;
};

// Object iterator state
struct BridgeObjectIter {
    simdjson::ondemand::object_iterator current;
    simdjson::ondemand::object_iterator end;
    simdjson::ondemand::object object;
    // Buffer for unescaped key (valid until next call to Next)
    std::string key_buf;
    bool need_advance = false;
};

// ─── Null-guard macro ────────────────────────────────────────────────────────
#define CHECK_NULL(ptr) do { if (!(ptr)) return SIMDJSON_BRIDGE_ERR_NULL_POINTER; } while(0)

// ─── Library info ────────────────────────────────────────────────────────────

extern "C" const char* SJNATIVE_CALL SimdJsonNative_GetVersion(void) {
    return SIMDJSON_VERSION;
}

// ─── Parser lifecycle ────────────────────────────────────────────────────────

extern "C" SimdJsonParser SJNATIVE_CALL SimdJsonNative_CreateParser(void) {
    try {
        return new simdjson::ondemand::parser();
    } catch (...) {
        return nullptr;
    }
}

extern "C" void SJNATIVE_CALL SimdJsonNative_DestroyParser(SimdJsonParser parser) {
    delete static_cast<simdjson::ondemand::parser*>(parser);
}

// ─── Document lifecycle ──────────────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_Parse(
    SimdJsonParser    parser,
    const char*       json,
    size_t            length,
    SimdJsonDocument* out_doc)
{
    CHECK_NULL(parser);
    CHECK_NULL(json);
    CHECK_NULL(out_doc);

    auto* p = static_cast<simdjson::ondemand::parser*>(parser);

    try {
        auto* bd = new BridgeDocument();

        // Copy the input into an owned padded buffer so callers need not
        // manage padding themselves.
        bd->json_buf = simdjson::padded_string(json, length);

        auto err = p->iterate(bd->json_buf).get(bd->doc);
        if (err) {
            delete bd;
            return translate_error(err);
        }

        *out_doc = bd;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) {
        return SIMDJSON_BRIDGE_ERR_UNKNOWN;
    }
}

extern "C" void SJNATIVE_CALL SimdJsonNative_DestroyDocument(SimdJsonDocument doc) {
    delete static_cast<BridgeDocument*>(doc);
}

// ─── Document root access ────────────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetType(
    SimdJsonDocument doc, SimdJsonType* out_type)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_type);
    auto* bd = static_cast<BridgeDocument*>(doc);
    simdjson::ondemand::json_type t;
    auto err = bd->doc.type().get(t);
    if (err) return translate_error(err);
    *out_type = static_cast<SimdJsonType>(t);
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetArray(
    SimdJsonDocument doc, SimdJsonArray* out_array)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_array);
    auto* bd = static_cast<BridgeDocument*>(doc);
    try {
        auto* ba = new BridgeArray();
        auto err = bd->doc.get_array().get(ba->array);
        if (err) { delete ba; return translate_error(err); }
        *out_array = ba;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetObject(
    SimdJsonDocument doc, SimdJsonObject* out_object)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_object);
    auto* bd = static_cast<BridgeDocument*>(doc);
    try {
        auto* bo = new BridgeObject();
        auto err = bd->doc.get_object().get(bo->object);
        if (err) { delete bo; return translate_error(err); }
        *out_object = bo;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetFieldByKey(
    SimdJsonDocument doc, const char* key, SimdJsonValue* out_value)
{
    CHECK_NULL(doc);
    CHECK_NULL(key);
    CHECK_NULL(out_value);
    auto* bd = static_cast<BridgeDocument*>(doc);
    try {
        auto* bv = new BridgeValue();
        simdjson::error_code ec;
        bd->doc[key].tie(bv->value, ec);
        if (ec) { delete bv; return translate_error(ec); }
        *out_value = bv;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentAtPointer(
    SimdJsonDocument doc, const char* json_pointer, SimdJsonValue* out_value)
{
    CHECK_NULL(doc);
    CHECK_NULL(json_pointer);
    CHECK_NULL(out_value);
    auto* bd = static_cast<BridgeDocument*>(doc);
    try {
        auto* bv = new BridgeValue();
        simdjson::error_code ec;
        bd->doc.at_pointer(json_pointer).tie(bv->value, ec);
        if (ec) { delete bv; return translate_error(ec); }
        *out_value = bv;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

// ─── Value inspection ────────────────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetType(
    SimdJsonValue value, SimdJsonType* out_type)
{
    CHECK_NULL(value);
    CHECK_NULL(out_type);
    auto* bv = static_cast<BridgeValue*>(value);
    simdjson::ondemand::json_type t;
    auto err = bv->value.type().get(t);
    if (err) return translate_error(err);
    *out_type = static_cast<SimdJsonType>(t);
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetString(
    SimdJsonValue value, const char** out_ptr, size_t* out_len)
{
    CHECK_NULL(value);
    CHECK_NULL(out_ptr);
    CHECK_NULL(out_len);
    auto* bv = static_cast<BridgeValue*>(value);
    std::string_view sv;
    auto err = bv->value.get_string().get(sv);
    if (err) return translate_error(err);
    *out_ptr = sv.data();
    *out_len = sv.size();
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetDouble(
    SimdJsonValue value, double* out_val)
{
    CHECK_NULL(value);
    CHECK_NULL(out_val);
    auto* bv = static_cast<BridgeValue*>(value);
    auto err = bv->value.get_double().get(*out_val);
    return translate_error(err);
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetInt64(
    SimdJsonValue value, int64_t* out_val)
{
    CHECK_NULL(value);
    CHECK_NULL(out_val);
    auto* bv = static_cast<BridgeValue*>(value);
    auto err = bv->value.get_int64().get(*out_val);
    return translate_error(err);
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetUInt64(
    SimdJsonValue value, uint64_t* out_val)
{
    CHECK_NULL(value);
    CHECK_NULL(out_val);
    auto* bv = static_cast<BridgeValue*>(value);
    auto err = bv->value.get_uint64().get(*out_val);
    return translate_error(err);
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetBool(
    SimdJsonValue value, int32_t* out_val)
{
    CHECK_NULL(value);
    CHECK_NULL(out_val);
    auto* bv = static_cast<BridgeValue*>(value);
    bool b;
    auto err = bv->value.get_bool().get(b);
    if (err) return translate_error(err);
    *out_val = b ? 1 : 0;
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueIsNull(
    SimdJsonValue value, int32_t* out_is_null)
{
    CHECK_NULL(value);
    CHECK_NULL(out_is_null);
    auto* bv = static_cast<BridgeValue*>(value);
    *out_is_null = bv->value.is_null() ? 1 : 0;
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetArray(
    SimdJsonValue value, SimdJsonArray* out_array)
{
    CHECK_NULL(value);
    CHECK_NULL(out_array);
    auto* bv = static_cast<BridgeValue*>(value);
    try {
        auto* ba = new BridgeArray();
        auto err = bv->value.get_array().get(ba->array);
        if (err) { delete ba; return translate_error(err); }
        *out_array = ba;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetObject(
    SimdJsonValue value, SimdJsonObject* out_object)
{
    CHECK_NULL(value);
    CHECK_NULL(out_object);
    auto* bv = static_cast<BridgeValue*>(value);
    try {
        auto* bo = new BridgeObject();
        auto err = bv->value.get_object().get(bo->object);
        if (err) { delete bo; return translate_error(err); }
        *out_object = bo;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetFieldByKey(
    SimdJsonValue value, const char* key, SimdJsonValue* out_value)
{
    CHECK_NULL(value);
    CHECK_NULL(key);
    CHECK_NULL(out_value);
    auto* bv = static_cast<BridgeValue*>(value);
    try {
        auto* child = new BridgeValue();
        auto err = bv->value[key].get(child->value);
        if (err) { delete child; return translate_error(err); }
        *out_value = child;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

// ─── Array iteration ─────────────────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ArrayBegin(
    SimdJsonArray array, SimdJsonArrayIter* out_iter)
{
    CHECK_NULL(array);
    CHECK_NULL(out_iter);
    auto* ba = static_cast<BridgeArray*>(array);
    try {
        auto* it = new BridgeArrayIter();
        it->array   = ba->array;
        it->current = it->array.begin();
        it->end     = it->array.end();
        *out_iter = it;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ArrayIterNext(
    SimdJsonArrayIter iter, SimdJsonValue* out_value, int32_t* out_done)
{
    CHECK_NULL(iter);
    CHECK_NULL(out_value);
    CHECK_NULL(out_done);
    auto* it = static_cast<BridgeArrayIter*>(iter);
    // Advance past the previous element NOW (after C# has consumed it).
    if (it->need_advance) {
        ++it->current;
        it->need_advance = false;
    }
    if (it->current == it->end) {
        *out_done = 1;
        *out_value = nullptr;
        return SIMDJSON_BRIDGE_SUCCESS;
    }
    try {
        auto* bv = new BridgeValue();
        simdjson::error_code ec;
        (*it->current).tie(bv->value, ec);
        if (ec) { delete bv; return translate_error(ec); }
        it->need_advance = true;  // advance next time, not now
        *out_value = bv;
        *out_done  = 0;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

extern "C" void SJNATIVE_CALL SimdJsonNative_DestroyArrayIter(SimdJsonArrayIter iter) {
    delete static_cast<BridgeArrayIter*>(iter);
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ArrayCount(
    SimdJsonArray array, size_t* out_count)
{
    CHECK_NULL(array);
    CHECK_NULL(out_count);
    auto* ba = static_cast<BridgeArray*>(array);
    auto err = ba->array.count_elements().get(*out_count);
    return translate_error(err);
}

// ─── Object iteration ────────────────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectBegin(
    SimdJsonObject object, SimdJsonObjectIter* out_iter)
{
    CHECK_NULL(object);
    CHECK_NULL(out_iter);
    auto* bo = static_cast<BridgeObject*>(object);
    try {
        auto* it = new BridgeObjectIter();
        it->object  = bo->object;
        it->current = it->object.begin();
        it->end     = it->object.end();
        *out_iter = it;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectIterNext(
    SimdJsonObjectIter iter,
    const char** out_key_ptr, size_t* out_key_len,
    SimdJsonValue* out_value,
    int32_t* out_done)
{
    CHECK_NULL(iter);
    CHECK_NULL(out_key_ptr);
    CHECK_NULL(out_key_len);
    CHECK_NULL(out_value);
    CHECK_NULL(out_done);

    auto* it = static_cast<BridgeObjectIter*>(iter);
    // Advance past the previous field NOW (after C# has consumed it).
    if (it->need_advance) {
        ++it->current;
        it->need_advance = false;
    }
    if (it->current == it->end) {
        *out_done = 1;
        *out_key_ptr = nullptr;
        *out_key_len = 0;
        *out_value   = nullptr;
        return SIMDJSON_BRIDGE_SUCCESS;
    }
    try {
        simdjson::ondemand::field f;
        auto err = (*it->current).get(f);
        if (err) return translate_error(err);

        // Unescape the key and store it in the iterator's own buffer so the
        // pointer remains valid until the next call to Next.
        std::string_view key_sv;
        err = f.unescaped_key().get(key_sv);
        if (err) return translate_error(err);
        it->key_buf.assign(key_sv.data(), key_sv.size());

        auto* bv = new BridgeValue();
        bv->value = f.value();

        it->need_advance = true;  // advance next time, not now
        *out_key_ptr = it->key_buf.data();
        *out_key_len = it->key_buf.size();
        *out_value   = bv;
        *out_done    = 0;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

extern "C" void SJNATIVE_CALL SimdJsonNative_DestroyObjectIter(SimdJsonObjectIter iter) {
    delete static_cast<BridgeObjectIter*>(iter);
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectCount(
    SimdJsonObject object, size_t* out_count)
{
    CHECK_NULL(object);
    CHECK_NULL(out_count);
    auto* bo = static_cast<BridgeObject*>(object);
    auto err = bo->object.count_fields().get(*out_count);
    return translate_error(err);
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectGetFieldByKey(
    SimdJsonObject object, const char* key, SimdJsonValue* out_value)
{
    CHECK_NULL(object);
    CHECK_NULL(key);
    CHECK_NULL(out_value);
    auto* bo = static_cast<BridgeObject*>(object);
    try {
        auto* bv = new BridgeValue();
        simdjson::error_code ec;
        bo->object[key].tie(bv->value, ec);
        if (ec) { delete bv; return translate_error(ec); }
        *out_value = bv;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

// ─── Value cleanup ───────────────────────────────────────────────────────────

extern "C" void SJNATIVE_CALL SimdJsonNative_DestroyValue(SimdJsonValue value) {
    delete static_cast<BridgeValue*>(value);
}

extern "C" void SJNATIVE_CALL SimdJsonNative_DestroyArray(SimdJsonArray array) {
    delete static_cast<BridgeArray*>(array);
}

extern "C" void SJNATIVE_CALL SimdJsonNative_DestroyObject(SimdJsonObject object) {
    delete static_cast<BridgeObject*>(object);
}

// ─── Number type inspection ──────────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetNumberType(
    SimdJsonValue value, SimdJsonNumberType* out_type)
{
    CHECK_NULL(value);
    CHECK_NULL(out_type);
    auto* bv = static_cast<BridgeValue*>(value);
    simdjson::ondemand::number_type nt;
    auto err = bv->value.get_number_type().get(nt);
    if (err) return translate_error(err);
    switch (nt) {
        case simdjson::ondemand::number_type::floating_point_number:
            *out_type = SIMDJSON_NUMBER_TYPE_FLOATING_POINT; break;
        case simdjson::ondemand::number_type::signed_integer:
            *out_type = SIMDJSON_NUMBER_TYPE_SIGNED_INTEGER; break;
        case simdjson::ondemand::number_type::unsigned_integer:
            *out_type = SIMDJSON_NUMBER_TYPE_UNSIGNED_INTEGER; break;
        case simdjson::ondemand::number_type::big_integer:
            *out_type = SIMDJSON_NUMBER_TYPE_BIG_INTEGER; break;
        default:
            *out_type = SIMDJSON_NUMBER_TYPE_FLOATING_POINT; break;
    }
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueIsNegative(
    SimdJsonValue value, int32_t* out_val)
{
    CHECK_NULL(value);
    CHECK_NULL(out_val);
    auto* bv = static_cast<BridgeValue*>(value);
    // is_negative() returns bool directly (no error_code)
    *out_val = bv->value.is_negative() ? 1 : 0;
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueIsInteger(
    SimdJsonValue value, int32_t* out_val)
{
    CHECK_NULL(value);
    CHECK_NULL(out_val);
    auto* bv = static_cast<BridgeValue*>(value);
    bool is_int;
    auto err = bv->value.is_integer().get(is_int);
    if (err) return translate_error(err);
    *out_val = is_int ? 1 : 0;
    return SIMDJSON_BRIDGE_SUCCESS;
}

// ─── Raw JSON access ─────────────────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueRawJsonToken(
    SimdJsonValue value, const char** out_ptr, size_t* out_len)
{
    CHECK_NULL(value);
    CHECK_NULL(out_ptr);
    CHECK_NULL(out_len);
    auto* bv = static_cast<BridgeValue*>(value);
    // raw_json_token() returns string_view directly (no error_code)
    auto sv = bv->value.raw_json_token();
    *out_ptr = sv.data();
    *out_len = sv.size();
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueRawJson(
    SimdJsonValue value, const char** out_ptr, size_t* out_len)
{
    CHECK_NULL(value);
    CHECK_NULL(out_ptr);
    CHECK_NULL(out_len);
    auto* bv = static_cast<BridgeValue*>(value);
    std::string_view sv;
    auto err = bv->value.raw_json().get(sv);
    if (err) return translate_error(err);
    *out_ptr = sv.data();
    *out_len = sv.size();
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ArrayRawJson(
    SimdJsonArray array, const char** out_ptr, size_t* out_len)
{
    CHECK_NULL(array);
    CHECK_NULL(out_ptr);
    CHECK_NULL(out_len);
    auto* ba = static_cast<BridgeArray*>(array);
    std::string_view sv;
    auto err = ba->array.raw_json().get(sv);
    if (err) return translate_error(err);
    *out_ptr = sv.data();
    *out_len = sv.size();
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectRawJson(
    SimdJsonObject object, const char** out_ptr, size_t* out_len)
{
    CHECK_NULL(object);
    CHECK_NULL(out_ptr);
    CHECK_NULL(out_len);
    auto* bo = static_cast<BridgeObject*>(object);
    std::string_view sv;
    auto err = bo->object.raw_json().get(sv);
    if (err) return translate_error(err);
    *out_ptr = sv.data();
    *out_len = sv.size();
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentRawJson(
    SimdJsonDocument doc, const char** out_ptr, size_t* out_len)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_ptr);
    CHECK_NULL(out_len);
    auto* bd = static_cast<BridgeDocument*>(doc);
    std::string_view sv;
    auto err = bd->doc.raw_json().get(sv);
    if (err) return translate_error(err);
    *out_ptr = sv.data();
    *out_len = sv.size();
    return SIMDJSON_BRIDGE_SUCCESS;
}

// ─── Numbers encoded as strings ──────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetDoubleInString(
    SimdJsonValue value, double* out_val)
{
    CHECK_NULL(value);
    CHECK_NULL(out_val);
    auto* bv = static_cast<BridgeValue*>(value);
    auto err = bv->value.get_double_in_string().get(*out_val);
    return translate_error(err);
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetInt64InString(
    SimdJsonValue value, int64_t* out_val)
{
    CHECK_NULL(value);
    CHECK_NULL(out_val);
    auto* bv = static_cast<BridgeValue*>(value);
    auto err = bv->value.get_int64_in_string().get(*out_val);
    return translate_error(err);
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetUInt64InString(
    SimdJsonValue value, uint64_t* out_val)
{
    CHECK_NULL(value);
    CHECK_NULL(out_val);
    auto* bv = static_cast<BridgeValue*>(value);
    auto err = bv->value.get_uint64_in_string().get(*out_val);
    return translate_error(err);
}

// ─── JSON Pointer and JSONPath ───────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentAtPath(
    SimdJsonDocument doc, const char* json_path, SimdJsonValue* out_value)
{
    CHECK_NULL(doc);
    CHECK_NULL(json_path);
    CHECK_NULL(out_value);
    auto* bd = static_cast<BridgeDocument*>(doc);
    try {
        auto* bv = new BridgeValue();
        simdjson::error_code ec;
        bd->doc.at_path(json_path).tie(bv->value, ec);
        if (ec) { delete bv; return translate_error(ec); }
        *out_value = bv;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueAtPointer(
    SimdJsonValue value, const char* json_pointer, SimdJsonValue* out_value)
{
    CHECK_NULL(value);
    CHECK_NULL(json_pointer);
    CHECK_NULL(out_value);
    auto* bv = static_cast<BridgeValue*>(value);
    try {
        auto* child = new BridgeValue();
        simdjson::error_code ec;
        bv->value.at_pointer(json_pointer).tie(child->value, ec);
        if (ec) { delete child; return translate_error(ec); }
        *out_value = child;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueAtPath(
    SimdJsonValue value, const char* json_path, SimdJsonValue* out_value)
{
    CHECK_NULL(value);
    CHECK_NULL(json_path);
    CHECK_NULL(out_value);
    auto* bv = static_cast<BridgeValue*>(value);
    try {
        auto* child = new BridgeValue();
        simdjson::error_code ec;
        bv->value.at_path(json_path).tie(child->value, ec);
        if (ec) { delete child; return translate_error(ec); }
        *out_value = child;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectAtPointer(
    SimdJsonObject object, const char* json_pointer, SimdJsonValue* out_value)
{
    CHECK_NULL(object);
    CHECK_NULL(json_pointer);
    CHECK_NULL(out_value);
    auto* bo = static_cast<BridgeObject*>(object);
    try {
        auto* bv = new BridgeValue();
        simdjson::error_code ec;
        bo->object.at_pointer(json_pointer).tie(bv->value, ec);
        if (ec) { delete bv; return translate_error(ec); }
        *out_value = bv;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectAtPath(
    SimdJsonObject object, const char* json_path, SimdJsonValue* out_value)
{
    CHECK_NULL(object);
    CHECK_NULL(json_path);
    CHECK_NULL(out_value);
    auto* bo = static_cast<BridgeObject*>(object);
    try {
        auto* bv = new BridgeValue();
        simdjson::error_code ec;
        bo->object.at_path(json_path).tie(bv->value, ec);
        if (ec) { delete bv; return translate_error(ec); }
        *out_value = bv;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

// ─── Order-sensitive field lookup ────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentFindField(
    SimdJsonDocument doc, const char* key, SimdJsonValue* out_value)
{
    CHECK_NULL(doc);
    CHECK_NULL(key);
    CHECK_NULL(out_value);
    auto* bd = static_cast<BridgeDocument*>(doc);
    try {
        auto* bv = new BridgeValue();
        simdjson::error_code ec;
        bd->doc.find_field(key).tie(bv->value, ec);
        if (ec) { delete bv; return translate_error(ec); }
        *out_value = bv;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueFindField(
    SimdJsonValue value, const char* key, SimdJsonValue* out_value)
{
    CHECK_NULL(value);
    CHECK_NULL(key);
    CHECK_NULL(out_value);
    auto* bv = static_cast<BridgeValue*>(value);
    try {
        auto* child = new BridgeValue();
        simdjson::error_code ec;
        bv->value.find_field(key).tie(child->value, ec);
        if (ec) { delete child; return translate_error(ec); }
        *out_value = child;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectFindField(
    SimdJsonObject object, const char* key, SimdJsonValue* out_value)
{
    CHECK_NULL(object);
    CHECK_NULL(key);
    CHECK_NULL(out_value);
    auto* bo = static_cast<BridgeObject*>(object);
    try {
        auto* bv = new BridgeValue();
        simdjson::error_code ec;
        bo->object.find_field(key).tie(bv->value, ec);
        if (ec) { delete bv; return translate_error(ec); }
        *out_value = bv;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

// ─── Document rewind ─────────────────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentRewind(SimdJsonDocument doc)
{
    CHECK_NULL(doc);
    auto* bd = static_cast<BridgeDocument*>(doc);
    bd->doc.rewind();
    return SIMDJSON_BRIDGE_SUCCESS;
}

// ─── Array and Object index/reset ────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ArrayAt(
    SimdJsonArray array, size_t index, SimdJsonValue* out_value)
{
    CHECK_NULL(array);
    CHECK_NULL(out_value);
    auto* ba = static_cast<BridgeArray*>(array);
    try {
        auto* bv = new BridgeValue();
        simdjson::error_code ec;
        ba->array.at(index).tie(bv->value, ec);
        if (ec) { delete bv; return translate_error(ec); }
        *out_value = bv;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ArrayReset(SimdJsonArray array)
{
    CHECK_NULL(array);
    auto* ba = static_cast<BridgeArray*>(array);
    if (auto err = ba->array.reset().error()) return translate_error(err);
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectReset(SimdJsonObject object)
{
    CHECK_NULL(object);
    auto* bo = static_cast<BridgeObject*>(object);
    if (auto err = bo->object.reset().error()) return translate_error(err);
    return SIMDJSON_BRIDGE_SUCCESS;
}

// ─── Utilities ───────────────────────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_Minify(
    const char* src, size_t src_len, char* dst, size_t* out_new_len)
{
    CHECK_NULL(src);
    CHECK_NULL(dst);
    CHECK_NULL(out_new_len);
    auto err = simdjson::minify(src, src_len, dst, *out_new_len);
    return translate_error(err);
}

extern "C" int32_t SJNATIVE_CALL SimdJsonNative_ValidateUtf8(
    const char* src, size_t src_len)
{
    if (!src) return 0;
    return simdjson::validate_utf8(src, src_len) ? 1 : 0;
}

// ─── Type predicates ─────────────────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueIsScalar(
    SimdJsonValue value, int32_t* out_val)
{
    CHECK_NULL(value);
    CHECK_NULL(out_val);
    auto* bv = static_cast<BridgeValue*>(value);
    bool b;
    auto err = bv->value.is_scalar().get(b);
    if (err) return translate_error(err);
    *out_val = b ? 1 : 0;
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueIsString(
    SimdJsonValue value, int32_t* out_val)
{
    CHECK_NULL(value);
    CHECK_NULL(out_val);
    auto* bv = static_cast<BridgeValue*>(value);
    bool b;
    auto err = bv->value.is_string().get(b);
    if (err) return translate_error(err);
    *out_val = b ? 1 : 0;
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ArrayIsEmpty(
    SimdJsonArray array, int32_t* out_val)
{
    CHECK_NULL(array);
    CHECK_NULL(out_val);
    auto* ba = static_cast<BridgeArray*>(array);
    bool b;
    auto err = ba->array.is_empty().get(b);
    if (err) return translate_error(err);
    *out_val = b ? 1 : 0;
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentIsScalar(
    SimdJsonDocument doc, int32_t* out_val)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_val);
    auto* bd = static_cast<BridgeDocument*>(doc);
    bool b;
    auto err = bd->doc.is_scalar().get(b);
    if (err) return translate_error(err);
    *out_val = b ? 1 : 0;
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentIsString(
    SimdJsonDocument doc, int32_t* out_val)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_val);
    auto* bd = static_cast<BridgeDocument*>(doc);
    bool b;
    auto err = bd->doc.is_string().get(b);
    if (err) return translate_error(err);
    *out_val = b ? 1 : 0;
    return SIMDJSON_BRIDGE_SUCCESS;
}

// ─── Document root as value ──────────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetValue(
    SimdJsonDocument doc, SimdJsonValue* out_value)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_value);
    auto* bd = static_cast<BridgeDocument*>(doc);
    try {
        auto* bv = new BridgeValue();
        auto err = bd->doc.get_value().get(bv->value);
        if (err) { delete bv; return translate_error(err); }
        *out_value = bv;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

// ─── Parse location / depth ──────────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueCurrentOffset(
    SimdJsonValue value, SimdJsonDocument doc, size_t* out_offset)
{
    CHECK_NULL(value);
    CHECK_NULL(doc);
    CHECK_NULL(out_offset);
    auto* bv = static_cast<BridgeValue*>(value);
    auto* bd = static_cast<BridgeDocument*>(doc);
    const char* loc = nullptr;
    auto err = bv->value.current_location().get(loc);
    if (err) return translate_error(err);
    const char* base = bd->json_buf.data();
    *out_offset = (loc >= base) ? static_cast<size_t>(loc - base) : 0;
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueCurrentDepth(
    SimdJsonValue value, int32_t* out_depth)
{
    CHECK_NULL(value);
    CHECK_NULL(out_depth);
    auto* bv = static_cast<BridgeValue*>(value);
    *out_depth = bv->value.current_depth();
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentCurrentOffset(
    SimdJsonDocument doc, size_t* out_offset)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_offset);
    auto* bd = static_cast<BridgeDocument*>(doc);
    const char* loc = nullptr;
    auto err = bd->doc.current_location().get(loc);
    if (err) return translate_error(err);
    const char* base = bd->json_buf.data();
    *out_offset = (loc >= base) ? static_cast<size_t>(loc - base) : 0;
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentCurrentDepth(
    SimdJsonDocument doc, int32_t* out_depth)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_depth);
    auto* bd = static_cast<BridgeDocument*>(doc);
    *out_depth = bd->doc.current_depth();
    return SIMDJSON_BRIDGE_SUCCESS;
}

// ─── Parser configuration ────────────────────────────────────────────────────

extern "C" SimdJsonParser SJNATIVE_CALL SimdJsonNative_CreateParserWithCapacity(
    size_t max_capacity)
{
    try {
        return new simdjson::ondemand::parser(max_capacity);
    } catch (...) {
        return nullptr;
    }
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ParserCapacity(
    SimdJsonParser parser, size_t* out_capacity)
{
    CHECK_NULL(parser);
    CHECK_NULL(out_capacity);
    auto* p = static_cast<simdjson::ondemand::parser*>(parser);
    *out_capacity = p->capacity();
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ParserMaxCapacity(
    SimdJsonParser parser, size_t* out_max_capacity)
{
    CHECK_NULL(parser);
    CHECK_NULL(out_max_capacity);
    auto* p = static_cast<simdjson::ondemand::parser*>(parser);
    *out_max_capacity = p->max_capacity();
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ParserSetMaxCapacity(
    SimdJsonParser parser, size_t max_capacity)
{
    CHECK_NULL(parser);
    auto* p = static_cast<simdjson::ondemand::parser*>(parser);
    p->set_max_capacity(max_capacity);
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ParserMaxDepth(
    SimdJsonParser parser, size_t* out_max_depth)
{
    CHECK_NULL(parser);
    CHECK_NULL(out_max_depth);
    auto* p = static_cast<simdjson::ondemand::parser*>(parser);
    *out_max_depth = p->max_depth();
    return SIMDJSON_BRIDGE_SUCCESS;
}

// ─── Structured number ───────────────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetNumber(
    SimdJsonValue value, SimdJsonNumber* out_number)
{
    CHECK_NULL(value);
    CHECK_NULL(out_number);
    auto* bv = static_cast<BridgeValue*>(value);
    simdjson::ondemand::number num;
    auto err = bv->value.get_number().get(num);
    if (err) return translate_error(err);
    auto nt = num.get_number_type();
    switch (nt) {
        case simdjson::ondemand::number_type::floating_point_number:
            out_number->type = SIMDJSON_NUMBER_TYPE_FLOATING_POINT;
            out_number->_pad = 0;
            out_number->value.floating_point = num.get_double();
            break;
        case simdjson::ondemand::number_type::signed_integer:
            out_number->type = SIMDJSON_NUMBER_TYPE_SIGNED_INTEGER;
            out_number->_pad = 0;
            out_number->value.signed_integer = num.get_int64();
            break;
        case simdjson::ondemand::number_type::unsigned_integer:
            out_number->type = SIMDJSON_NUMBER_TYPE_UNSIGNED_INTEGER;
            out_number->_pad = 0;
            out_number->value.unsigned_integer = num.get_uint64();
            break;
        default: // big_integer
            out_number->type = SIMDJSON_NUMBER_TYPE_BIG_INTEGER;
            out_number->_pad = 0;
            out_number->value.signed_integer = 0;
            break;
    }
    return SIMDJSON_BRIDGE_SUCCESS;
}

// ─── Wobbly / WTF-8 strings ──────────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetWobblyString(
    SimdJsonValue value, const char** out_ptr, size_t* out_len)
{
    CHECK_NULL(value);
    CHECK_NULL(out_ptr);
    CHECK_NULL(out_len);
    auto* bv = static_cast<BridgeValue*>(value);
    std::string_view sv;
    auto err = bv->value.get_wobbly_string().get(sv);
    if (err) return translate_error(err);
    *out_ptr = sv.data();
    *out_len = sv.size();
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetWobblyString(
    SimdJsonDocument doc, const char** out_ptr, size_t* out_len)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_ptr);
    CHECK_NULL(out_len);
    auto* bd = static_cast<BridgeDocument*>(doc);
    std::string_view sv;
    auto err = bd->doc.get_wobbly_string().get(sv);
    if (err) return translate_error(err);
    *out_ptr = sv.data();
    *out_len = sv.size();
    return SIMDJSON_BRIDGE_SUCCESS;
}

// ─── Array pointer / path navigation ─────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ArrayAtPointer(
    SimdJsonArray array, const char* json_pointer, SimdJsonValue* out_value)
{
    CHECK_NULL(array);
    CHECK_NULL(json_pointer);
    CHECK_NULL(out_value);
    auto* ba = static_cast<BridgeArray*>(array);
    try {
        auto* bv = new BridgeValue();
        simdjson::error_code ec;
        ba->array.at_pointer(json_pointer).tie(bv->value, ec);
        if (ec) { delete bv; return translate_error(ec); }
        *out_value = bv;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ArrayAtPath(
    SimdJsonArray array, const char* json_path, SimdJsonValue* out_value)
{
    CHECK_NULL(array);
    CHECK_NULL(json_path);
    CHECK_NULL(out_value);
    auto* ba = static_cast<BridgeArray*>(array);
    try {
        auto* bv = new BridgeValue();
        simdjson::error_code ec;
        ba->array.at_path(json_path).tie(bv->value, ec);
        if (ec) { delete bv; return translate_error(ec); }
        *out_value = bv;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

// ─── Object predicates ───────────────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectIsEmpty(
    SimdJsonObject object, int32_t* out_val)
{
    CHECK_NULL(object);
    CHECK_NULL(out_val);
    auto* bo = static_cast<BridgeObject*>(object);
    bool b = false;
    auto err = bo->object.is_empty().get(b);
    if (err) return translate_error(err);
    *out_val = b ? 1 : 0;
    return SIMDJSON_BRIDGE_SUCCESS;
}

// ─── find_field_unordered ────────────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentFindFieldUnordered(
    SimdJsonDocument doc, const char* key, SimdJsonValue* out_value)
{
    CHECK_NULL(doc);
    CHECK_NULL(key);
    CHECK_NULL(out_value);
    auto* bd = static_cast<BridgeDocument*>(doc);
    try {
        auto* bv = new BridgeValue();
        simdjson::error_code ec;
        bd->doc.find_field_unordered(key).tie(bv->value, ec);
        if (ec) { delete bv; return translate_error(ec); }
        *out_value = bv;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueFindFieldUnordered(
    SimdJsonValue value, const char* key, SimdJsonValue* out_value)
{
    CHECK_NULL(value);
    CHECK_NULL(key);
    CHECK_NULL(out_value);
    auto* bv = static_cast<BridgeValue*>(value);
    try {
        auto* child = new BridgeValue();
        simdjson::error_code ec;
        bv->value.find_field_unordered(key).tie(child->value, ec);
        if (ec) { delete child; return translate_error(ec); }
        *out_value = child;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectFindFieldUnordered(
    SimdJsonObject object, const char* key, SimdJsonValue* out_value)
{
    CHECK_NULL(object);
    CHECK_NULL(key);
    CHECK_NULL(out_value);
    auto* bo = static_cast<BridgeObject*>(object);
    try {
        auto* bv = new BridgeValue();
        simdjson::error_code ec;
        bo->object.find_field_unordered(key).tie(bv->value, ec);
        if (ec) { delete bv; return translate_error(ec); }
        *out_value = bv;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

// ─── Document number helpers ─────────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetNumberType(
    SimdJsonDocument doc, int32_t* out_type)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_type);
    auto* bd = static_cast<BridgeDocument*>(doc);
    simdjson::ondemand::number_type nt;
    auto err = bd->doc.get_number_type().get(nt);
    if (err) return translate_error(err);
    switch (nt) {
        case simdjson::ondemand::number_type::floating_point_number:
            *out_type = SIMDJSON_NUMBER_TYPE_FLOATING_POINT; break;
        case simdjson::ondemand::number_type::signed_integer:
            *out_type = SIMDJSON_NUMBER_TYPE_SIGNED_INTEGER; break;
        case simdjson::ondemand::number_type::unsigned_integer:
            *out_type = SIMDJSON_NUMBER_TYPE_UNSIGNED_INTEGER; break;
        case simdjson::ondemand::number_type::big_integer:
            *out_type = SIMDJSON_NUMBER_TYPE_BIG_INTEGER; break;
        default:
            *out_type = SIMDJSON_NUMBER_TYPE_FLOATING_POINT; break;
    }
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentIsNegative(
    SimdJsonDocument doc, int32_t* out_val)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_val);
    auto* bd = static_cast<BridgeDocument*>(doc);
    bool b = bd->doc.is_negative();
    *out_val = b ? 1 : 0;
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentIsInteger(
    SimdJsonDocument doc, int32_t* out_val)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_val);
    auto* bd = static_cast<BridgeDocument*>(doc);
    bool b = false;
    auto err = bd->doc.is_integer().get(b);
    if (err) return translate_error(err);
    *out_val = b ? 1 : 0;
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetNumber(
    SimdJsonDocument doc, SimdJsonNumber* out_number)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_number);
    auto* bd = static_cast<BridgeDocument*>(doc);
    simdjson::ondemand::number num;
    auto err = bd->doc.get_number().get(num);
    if (err) return translate_error(err);
    out_number->_pad = 0;
    switch (num.get_number_type()) {
        case simdjson::ondemand::number_type::floating_point_number:
            out_number->type = SIMDJSON_NUMBER_TYPE_FLOATING_POINT;
            out_number->value.floating_point = double(num);
            break;
        case simdjson::ondemand::number_type::signed_integer:
            out_number->type = SIMDJSON_NUMBER_TYPE_SIGNED_INTEGER;
            out_number->value.signed_integer = int64_t(num);
            break;
        case simdjson::ondemand::number_type::unsigned_integer:
            out_number->type = SIMDJSON_NUMBER_TYPE_UNSIGNED_INTEGER;
            out_number->value.unsigned_integer = uint64_t(num);
            break;
        default: // big_integer
            out_number->type = SIMDJSON_NUMBER_TYPE_BIG_INTEGER;
            out_number->_pad = 0;
            out_number->value.signed_integer = 0;
            break;
    }
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentRawJsonToken(
    SimdJsonDocument doc, const char** out_ptr, size_t* out_len)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_ptr);
    CHECK_NULL(out_len);
    auto* bd = static_cast<BridgeDocument*>(doc);
    std::string_view sv;
    auto err = bd->doc.raw_json_token().get(sv);
    if (err) return translate_error(err);
    *out_ptr = sv.data();
    *out_len = sv.size();
    return SIMDJSON_BRIDGE_SUCCESS;
}

// ─── Parser – allow incomplete JSON ──────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ParseAllowIncompleteJson(
    SimdJsonParser parser, const char* json, size_t length, SimdJsonDocument* out_doc)
{
    CHECK_NULL(parser);
    CHECK_NULL(json);
    CHECK_NULL(out_doc);
    auto* p = static_cast<simdjson::ondemand::parser*>(parser);
    try {
        auto* bd = new BridgeDocument();
        bd->json_buf = simdjson::padded_string(json, length);
        simdjson::error_code err = p->iterate_allow_incomplete_json(bd->json_buf).get(bd->doc);
        if (err) {
            delete bd;
            return translate_error(err);
        }
        *out_doc = bd;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) {
        return SIMDJSON_BRIDGE_ERR_UNKNOWN;
    }
}

// ─── Raw JSON string (without unescaping) ────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetRawJsonString(
    SimdJsonValue value, const char** out_ptr, size_t* out_len)
{
    CHECK_NULL(value);
    CHECK_NULL(out_ptr);
    CHECK_NULL(out_len);
    auto* bv = static_cast<BridgeValue*>(value);
    // raw_json_token() is a non-consuming peek that returns the full token including quotes.
    std::string_view token = bv->value.raw_json_token();
    if (token.size() < 2 || token.front() != '"') {
        return SIMDJSON_BRIDGE_ERR_INCORRECT_TYPE;
    }
    // Return the inner bytes: after the opening quote, minus the closing quote.
    *out_ptr = token.data() + 1;
    *out_len = token.size() - 2;
    return SIMDJSON_BRIDGE_SUCCESS;
}

// ─── Wildcard path iteration ──────────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentForEachAtPath(
    SimdJsonDocument doc, const char* path, size_t path_len,
    SimdJsonWildcardCallback callback, void* context)
{
    CHECK_NULL(doc);
    CHECK_NULL(path);
    CHECK_NULL(callback);
    auto* bd = static_cast<BridgeDocument*>(doc);
    try {
        auto err = bd->doc.for_each_at_path_with_wildcard(
            std::string_view(path, path_len),
            [callback, context](simdjson::ondemand::value val) {
                BridgeValue bv;
                bv.value = std::move(val);
                callback(&bv, context);
            }
        );
        return translate_error(err);
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueForEachAtPath(
    SimdJsonValue value, const char* path, size_t path_len,
    SimdJsonWildcardCallback callback, void* context)
{
    CHECK_NULL(value);
    CHECK_NULL(path);
    CHECK_NULL(callback);
    auto* bv = static_cast<BridgeValue*>(value);
    try {
        auto err = bv->value.for_each_at_path_with_wildcard(
            std::string_view(path, path_len),
            [callback, context](simdjson::ondemand::value val) {
                BridgeValue inner;
                inner.value = std::move(val);
                callback(&inner, context);
            }
        );
        return translate_error(err);
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

// ─── ForEachAtPath on array and object ───────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ArrayForEachAtPath(
    SimdJsonArray array, const char* path, size_t path_len,
    SimdJsonWildcardCallback callback, void* context)
{
    CHECK_NULL(array);
    CHECK_NULL(path);
    CHECK_NULL(callback);
    auto* ba = static_cast<BridgeArray*>(array);
    try {
        auto err = ba->array.for_each_at_path_with_wildcard(
            std::string_view(path, path_len),
            [callback, context](simdjson::ondemand::value val) {
                BridgeValue bv;
                bv.value = std::move(val);
                callback(&bv, context);
            }
        );
        return translate_error(err);
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ObjectForEachAtPath(
    SimdJsonObject object, const char* path, size_t path_len,
    SimdJsonWildcardCallback callback, void* context)
{
    CHECK_NULL(object);
    CHECK_NULL(path);
    CHECK_NULL(callback);
    auto* bo = static_cast<BridgeObject*>(object);
    try {
        auto err = bo->object.for_each_at_path_with_wildcard(
            std::string_view(path, path_len),
            [callback, context](simdjson::ondemand::value val) {
                BridgeValue bv;
                bv.value = std::move(val);
                callback(&bv, context);
            }
        );
        return translate_error(err);
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

// ─── Document scalar getters ──────────────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetString(
    SimdJsonDocument doc, const char** out_ptr, size_t* out_len)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_ptr);
    CHECK_NULL(out_len);
    auto* bd = static_cast<BridgeDocument*>(doc);
    std::string_view sv;
    auto err = bd->doc.get_string().get(sv);
    if (err) return translate_error(err);
    *out_ptr = sv.data();
    *out_len = sv.size();
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetStringAllowReplacement(
    SimdJsonDocument doc, const char** out_ptr, size_t* out_len)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_ptr);
    CHECK_NULL(out_len);
    auto* bd = static_cast<BridgeDocument*>(doc);
    std::string_view sv;
    auto err = bd->doc.get_string(true).get(sv);
    if (err) return translate_error(err);
    *out_ptr = sv.data();
    *out_len = sv.size();
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetBool(
    SimdJsonDocument doc, int32_t* out_val)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_val);
    auto* bd = static_cast<BridgeDocument*>(doc);
    bool b;
    auto err = bd->doc.get_bool().get(b);
    if (err) return translate_error(err);
    *out_val = b ? 1 : 0;
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentIsNull(
    SimdJsonDocument doc, int32_t* out_is_null)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_is_null);
    auto* bd = static_cast<BridgeDocument*>(doc);
    bool b;
    auto err = bd->doc.is_null().get(b);
    if (err) return translate_error(err);
    *out_is_null = b ? 1 : 0;
    return SIMDJSON_BRIDGE_SUCCESS;
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetDouble(
    SimdJsonDocument doc, double* out_val)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_val);
    auto* bd = static_cast<BridgeDocument*>(doc);
    auto err = bd->doc.get_double().get(*out_val);
    return translate_error(err);
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetInt64(
    SimdJsonDocument doc, int64_t* out_val)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_val);
    auto* bd = static_cast<BridgeDocument*>(doc);
    auto err = bd->doc.get_int64().get(*out_val);
    return translate_error(err);
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetUInt64(
    SimdJsonDocument doc, uint64_t* out_val)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_val);
    auto* bd = static_cast<BridgeDocument*>(doc);
    auto err = bd->doc.get_uint64().get(*out_val);
    return translate_error(err);
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetDoubleInString(
    SimdJsonDocument doc, double* out_val)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_val);
    auto* bd = static_cast<BridgeDocument*>(doc);
    auto err = bd->doc.get_double_in_string().get(*out_val);
    return translate_error(err);
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetInt64InString(
    SimdJsonDocument doc, int64_t* out_val)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_val);
    auto* bd = static_cast<BridgeDocument*>(doc);
    auto err = bd->doc.get_int64_in_string().get(*out_val);
    return translate_error(err);
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentGetUInt64InString(
    SimdJsonDocument doc, uint64_t* out_val)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_val);
    auto* bd = static_cast<BridgeDocument*>(doc);
    auto err = bd->doc.get_uint64_in_string().get(*out_val);
    return translate_error(err);
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentCountElements(
    SimdJsonDocument doc, size_t* out_count)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_count);
    auto* bd = static_cast<BridgeDocument*>(doc);
    auto err = bd->doc.count_elements().get(*out_count);
    return translate_error(err);
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentCountFields(
    SimdJsonDocument doc, size_t* out_count)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_count);
    auto* bd = static_cast<BridgeDocument*>(doc);
    auto err = bd->doc.count_fields().get(*out_count);
    return translate_error(err);
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_DocumentAt(
    SimdJsonDocument doc, size_t index, SimdJsonValue* out_value)
{
    CHECK_NULL(doc);
    CHECK_NULL(out_value);
    auto* bd = static_cast<BridgeDocument*>(doc);
    try {
        auto* bv = new BridgeValue();
        simdjson::error_code ec;
        bd->doc.at(index).tie(bv->value, ec);
        if (ec) { delete bv; return translate_error(ec); }
        *out_value = bv;
        return SIMDJSON_BRIDGE_SUCCESS;
    } catch (...) { return SIMDJSON_BRIDGE_ERR_UNKNOWN; }
}

// ─── Value count_elements / count_fields ─────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueCountElements(
    SimdJsonValue value, size_t* out_count)
{
    CHECK_NULL(value);
    CHECK_NULL(out_count);
    auto* bv = static_cast<BridgeValue*>(value);
    auto err = bv->value.count_elements().get(*out_count);
    return translate_error(err);
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueCountFields(
    SimdJsonValue value, size_t* out_count)
{
    CHECK_NULL(value);
    CHECK_NULL(out_count);
    auto* bv = static_cast<BridgeValue*>(value);
    auto err = bv->value.count_fields().get(*out_count);
    return translate_error(err);
}

// ─── Native int32 / uint32 getters ───────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetInt32(
    SimdJsonValue value, int32_t* out_val)
{
    CHECK_NULL(value);
    CHECK_NULL(out_val);
    auto* bv = static_cast<BridgeValue*>(value);
    auto err = bv->value.get_int32().get(*out_val);
    return translate_error(err);
}

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetUInt32(
    SimdJsonValue value, uint32_t* out_val)
{
    CHECK_NULL(value);
    CHECK_NULL(out_val);
    auto* bv = static_cast<BridgeValue*>(value);
    auto err = bv->value.get_uint32().get(*out_val);
    return translate_error(err);
}

// ─── String with allow_replacement ───────────────────────────────────────────

extern "C" SimdJsonError SJNATIVE_CALL SimdJsonNative_ValueGetStringAllowReplacement(
    SimdJsonValue value, const char** out_ptr, size_t* out_len)
{
    CHECK_NULL(value);
    CHECK_NULL(out_ptr);
    CHECK_NULL(out_len);
    auto* bv = static_cast<BridgeValue*>(value);
    std::string_view sv;
    auto err = bv->value.get_string(true).get(sv);
    if (err) return translate_error(err);
    *out_ptr = sv.data();
    *out_len = sv.size();
    return SIMDJSON_BRIDGE_SUCCESS;
}
