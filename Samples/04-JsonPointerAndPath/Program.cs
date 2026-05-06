// 04 – JSON Pointer (RFC 6901) and JSONPath
// Demonstrates AtPointer, AtPath, TryAtPointer, TryAtPath on
// JsonDocument, JsonValue, JsonArray, and JsonObject.

using SimdJson;

Console.WriteLine("=== 04 – JSON Pointer & JSONPath ===\n");

const string json = """
{
  "store": {
    "name": "Tech Store",
    "inventory": [
      {"id": 1, "product": "Laptop",  "price": 999.99, "inStock": true},
      {"id": 2, "product": "Mouse",   "price":  29.99, "inStock": true},
      {"id": 3, "product": "Monitor", "price": 349.00, "inStock": false}
    ],
    "address": {
      "street": "123 Main St",
      "city": "Springfield",
      "zip": "12345"
    }
  }
}
""";

// ── 1. RFC 6901 JSON Pointer on document ──────────────────────────────────
Console.WriteLine("── JSON Pointer ──");
using var doc = SimdJsonParser.Shared.Parse(json);

using var storeName = doc.AtPointer("/store/name");
Console.WriteLine($"store name       : {storeName.GetString()}");

using var firstProduct = doc.AtPointer("/store/inventory/0/product");
Console.WriteLine($"first product    : {firstProduct.GetString()}");

using var secondPrice = doc.AtPointer("/store/inventory/1/price");
Console.WriteLine($"second price     : {secondPrice.GetDouble():F2}");

using var city = doc.AtPointer("/store/address/city");
Console.WriteLine($"city             : {city.GetString()}");

// ── 2. TryAtPointer – non-throwing ────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("── TryAtPointer ──");
using var doc2 = SimdJsonParser.Shared.Parse(json);
if (doc2.TryAtPointer("/store/address/zip", out var zip))
{
    Console.WriteLine($"zip found        : {zip!.GetString()}");
    zip.Dispose();
}
if (!doc2.TryAtPointer("/store/address/country", out var country))
{
    country?.Dispose();
    Console.WriteLine("country          : not found (no exception)");
}

// ── 3. JSON Pointer on a nested value ────────────────────────────────────
Console.WriteLine();
Console.WriteLine("── Pointer from nested value ──");
using var doc3      = SimdJsonParser.Shared.Parse(json);
using var inventory = doc3.GetField("store").GetField("inventory");
using var thirdItem = inventory.AtPointer("/2/product");
Console.WriteLine($"third product    : {thirdItem.GetString()}");

// ── 4. JSON Pointer on a JsonObject ──────────────────────────────────────
Console.WriteLine();
Console.WriteLine("── Pointer from JsonObject ──");
using var doc4    = SimdJsonParser.Shared.Parse(json);
using var address = doc4.GetField("store").GetField("address").GetObject();
using var street  = address.AtPointer("/street");
Console.WriteLine($"street           : {street.GetString()}");

// ── 5. JSON Pointer on a JsonArray ────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("── Pointer from JsonArray ──");
using var doc5  = SimdJsonParser.Shared.Parse(json);
using var arr   = doc5.GetField("store").GetField("inventory").GetArray();
using var mouse = arr.AtPointer("/1/product");
Console.WriteLine($"arr pointer [1]  : {mouse.GetString()}");

// ── 6. JSONPath ───────────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("── JSONPath ──");
using var doc6  = SimdJsonParser.Shared.Parse(json);
using var pName = doc6.AtPath("$.store.name");
Console.WriteLine($"$.store.name     : {pName.GetString()}");

using var doc7   = SimdJsonParser.Shared.Parse(json);
using var pPrice = doc7.AtPath("$.store.inventory[0].price");
Console.WriteLine($"$.store.inventory[0].price : {pPrice.GetDouble():F2}");

// ── 7. TryAtPath ──────────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("── TryAtPath ──");
using var doc8 = SimdJsonParser.Shared.Parse(json);
if (doc8.TryAtPath("$.store.address.city", out var pathCity))
{
    Console.WriteLine($"path city        : {pathCity!.GetString()}");
    pathCity.Dispose();
}
if (!doc8.TryAtPath("$.store.owner", out var owner))
{
    owner?.Dispose();
    Console.WriteLine("owner            : not found via path (no exception)");
}
