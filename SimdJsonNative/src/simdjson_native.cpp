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
