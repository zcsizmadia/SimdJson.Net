// 09 – Real World: GeoJSON parsing
// Parses a GeoJSON FeatureCollection, extracts geometry coordinates and
// properties, and calculates a bounding box — a typical spatial data task.

using SimdJson;

Console.WriteLine("=== 09 – Real World: GeoJSON ===\n");

const string geoJson = """
{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "geometry": { "type": "Point", "coordinates": [-73.9857, 40.7484] },
      "properties": { "name": "Empire State Building", "category": "landmark" }
    },
    {
      "type": "Feature",
      "geometry": { "type": "Point", "coordinates": [-74.0445, 40.6892] },
      "properties": { "name": "Statue of Liberty", "category": "landmark" }
    },
    {
      "type": "Feature",
      "geometry": { "type": "Point", "coordinates": [-73.9669, 40.7812] },
      "properties": { "name": "Central Park", "category": "park" }
    },
    {
      "type": "Feature",
      "geometry": {
        "type": "LineString",
        "coordinates": [[-74.0060, 40.7128], [-73.9857, 40.7484], [-73.9669, 40.7812]]
      },
      "properties": { "name": "Manhattan Route", "category": "route" }
    }
  ]
}
""";

// Bounding-box accumulator
double minLon = double.MaxValue, maxLon = double.MinValue;
double minLat = double.MaxValue, maxLat = double.MinValue;

var landmarks = new List<string>();

using var doc         = SimdJsonParser.Shared.Parse(geoJson);
using var featuresVal = doc.GetField("features");
using var features    = featuresVal.GetArray();

foreach (var feature in features)
{
    // simdjson On-Demand is forward-only: fully consume each nested object
    // before moving to the next sibling field in the parent.
    // Pattern: read ALL geometry fields first, THEN read properties.

    // ── Geometry (fully consumed before properties are touched) ───────────
    using var geomVal  = feature.GetField("geometry");
    using var geom     = geomVal.GetObject();
    using var geomType = geom.GetField("type");
    string geometryType = geomType.GetString();

    double featureLon = 0, featureLat = 0;
    bool isPoint = false;
    var lineCoords = new List<(double lon, double lat)>();

    if (geometryType == "Point")
    {
        isPoint = true;
        using var coordsVal = geom.GetField("coordinates");
        using var coords    = coordsVal.GetArray();
        // Iterate fully (instead of At()) so the array iterator is consumed.
        int ci = 0;
        foreach (var c in coords)
        {
            if (ci == 0)
            {
                featureLon = c.GetDouble();
            }
            else if (ci == 1)
            {
                featureLat = c.GetDouble();
            }

            c.Dispose();
            ci++;
        }
    }
    else if (geometryType == "LineString")
    {
        using var coordArrVal = geom.GetField("coordinates");
        using var coordArr    = coordArrVal.GetArray();
        foreach (var coord in coordArr)
        {
            using var pairArr = coord.GetArray();
            double lon = 0, lat = 0;
            int ci = 0;
            foreach (var c in pairArr)
            {
                if (ci == 0)
                {
                    lon = c.GetDouble();
                }
                else if (ci == 1)
                {
                    lat = c.GetDouble();
                }

                c.Dispose();
                ci++;
            }
            lineCoords.Add((lon, lat));
            coord.Dispose();
        }
    }
    // geom is fully consumed; all using vars above are disposed here.

    // ── Properties (accessed after geometry is done) ──────────────────────
    using var propsVal = feature.GetField("properties");
    using var props    = propsVal.GetObject();
    using var propName = props.GetField("name");
    using var category = props.GetField("category");
    string featureName = propName.GetString();
    string cat         = category.GetString();

    // ── Report ────────────────────────────────────────────────────────────
    Console.WriteLine($"Feature : {featureName}  ({geometryType}, {cat})");

    if (isPoint)
    {
        Console.WriteLine($"          lon={featureLon:F4}, lat={featureLat:F4}");
        minLon = Math.Min(minLon, featureLon);
        maxLon = Math.Max(maxLon, featureLon);
        minLat = Math.Min(minLat, featureLat);
        maxLat = Math.Max(maxLat, featureLat);
        if (cat == "landmark")
        {
            landmarks.Add(featureName);
        }
    }
    else
    {
        int idx = 0;
        foreach (var (lon, lat) in lineCoords)
        {
            Console.WriteLine($"  [{idx++}] lon={lon:F4}, lat={lat:F4}");
            minLon = Math.Min(minLon, lon);
            maxLon = Math.Max(maxLon, lon);
            minLat = Math.Min(minLat, lat);
            maxLat = Math.Max(maxLat, lat);
        }
    }

    feature.Dispose();
}

Console.WriteLine();
Console.WriteLine($"Bounding box:");
Console.WriteLine($"  SW: ({minLon:F4}, {minLat:F4})");
Console.WriteLine($"  NE: ({maxLon:F4}, {maxLat:F4})");

Console.WriteLine();
Console.WriteLine($"Landmarks ({landmarks.Count}):");
foreach (var lm in landmarks)
{
    Console.WriteLine($"  - {lm}");
}