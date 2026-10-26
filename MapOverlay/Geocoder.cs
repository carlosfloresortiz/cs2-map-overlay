using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Game.SceneFlow;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace MapOverlay
{
    // Place search on OpenStreetMap data. Suggestions while typing come from Photon, which allows
    // search-as-you-type; a full search (Enter) uses Nominatim and falls back to Photon.
    // Coordinates and map links are read directly without going online.
    internal sealed class Geocoder : IDisposable
    {
        private const string kNominatim = "https://nominatim.openstreetmap.org/search?format=jsonv2&limit=6&q=";
        private const string kPhoton = "https://photon.komoot.io/api/?limit=6&q=";

        // Nominatim allows at most one request per second and no autocomplete.
        private const float kNominatimInterval = 1.1f;
        private const float kPhotonInterval = 0.3f;

        private enum Kind { Suggest, Search }

        private struct Result
        {
            public string Name;
            public double Lat;
            public double Lon;
        }

        // "19.43, -99.13", Google Maps ".../@19.43,-99.13,17z" and OpenStreetMap "#map=17/19.43/-99.13".
        private static readonly Regex s_Plain = new Regex(@"^\s*(-?\d{1,2}(?:\.\d+)?)\s*[,;\s]\s*(-?\d{1,3}(?:\.\d+)?)\s*$");
        private static readonly Regex s_Google = new Regex(@"@(-?\d{1,2}\.\d+),(-?\d{1,3}\.\d+)");
        private static readonly Regex s_Osm = new Regex(@"map=\d+(?:\.\d+)?/(-?\d{1,2}\.\d+)/(-?\d{1,3}\.\d+)");

        private readonly OverlayController m_Controller;
        private UnityWebRequest m_Request;
        private Kind m_RequestKind;
        private string m_RequestQuery;
        private string m_Queued;
        private Kind m_QueuedKind;
        private float m_LastNominatim = -10f;
        private float m_LastPhoton = -10f;

        // "", "searching", "empty", "error", "moved"
        public string Status { get; private set; } = "";
        // [{"name":..., "lat":..., "lon":...}] for the panel.
        public string ResultsJson { get; private set; } = "[]";

        public Geocoder(OverlayController controller)
        {
            m_Controller = controller;
        }

        public void Search(string query) => Enqueue(query, Kind.Search);

        public void Suggest(string query) => Enqueue(query, Kind.Suggest);

        private void Enqueue(string query, Kind kind)
        {
            query = (query ?? "").Trim();
            if (query.Length == 0)
            {
                Clear();
                return;
            }
            if (TryParseCoordinates(query, out double lat, out double lon))
            {
                // Typing coordinates only moves the map once confirmed.
                m_Queued = null;
                ResultsJson = "[]";
                if (kind == Kind.Suggest)
                {
                    Status = "";
                    return;
                }
                Status = "moved";
                m_Controller.GoTo(lat, lon);
                return;
            }
            m_Queued = query;
            m_QueuedKind = kind;
            Status = "searching";
        }

        public void Clear()
        {
            m_Queued = null;
            ResultsJson = "[]";
            Status = "";
        }

        public void Tick()
        {
            if (m_Request != null && m_Request.isDone) Finish();
            if (m_Queued == null || m_Request != null) return;
            float now = Time.realtimeSinceStartup;
            if (m_QueuedKind == Kind.Search ? now - m_LastNominatim >= kNominatimInterval : now - m_LastPhoton >= kPhotonInterval)
                Start(m_Queued, m_QueuedKind);
        }

        private void Start(string query, Kind kind)
        {
            m_Queued = null;
            m_RequestQuery = query;
            m_RequestKind = kind;
            string url;
            if (kind == Kind.Search)
            {
                m_LastNominatim = Time.realtimeSinceStartup;
                url = kNominatim + Uri.EscapeDataString(query);
            }
            else
            {
                m_LastPhoton = Time.realtimeSinceStartup;
                url = kPhoton + Uri.EscapeDataString(query);
            }
            m_Request = UnityWebRequest.Get(url);
            m_Request.SetRequestHeader("User-Agent", TileSources.UserAgent);
            string locale = GameManager.instance?.localizationManager?.activeLocaleId;
            if (kind == Kind.Search && !string.IsNullOrEmpty(locale))
                m_Request.SetRequestHeader("Accept-Language", locale + ",en;q=0.5");
            m_Request.timeout = 15;
            m_Request.SendWebRequest();
        }

        private void Finish()
        {
            using (var request = m_Request)
            {
                m_Request = null;
                if (m_Queued != null) return;  // A newer query replaces this one.

                var results = new List<Result>();
                bool ok = request.result == UnityWebRequest.Result.Success;
                if (ok)
                {
                    try
                    {
                        if (m_RequestKind == Kind.Search) ReadNominatim(request.downloadHandler.text, results);
                        else ReadPhoton(request.downloadHandler.text, results);
                    }
                    catch (Exception ex)
                    {
                        ok = false;
                        Mod.log.Warn($"Could not read {m_RequestKind} results for \"{m_RequestQuery}\": {ex.Message}");
                    }
                }
                else
                {
                    Mod.log.Warn($"{m_RequestKind} failed for \"{m_RequestQuery}\" ({request.responseCode} {request.error})");
                }

                if (m_RequestKind == Kind.Search && results.Count == 0)
                {
                    // Photon matches partial names and typos better; try it before giving up.
                    m_Queued = m_RequestQuery;
                    m_QueuedKind = Kind.Suggest;
                    return;
                }

                ResultsJson = ToJson(results);
                Status = results.Count > 0 ? "" : ok ? "empty" : "error";
            }
        }

        private static void ReadNominatim(string text, List<Result> results)
        {
            foreach (var item in JArray.Parse(text))
            {
                if (TryNumber(item["lat"], out double lat) && TryNumber(item["lon"], out double lon))
                    results.Add(new Result { Name = (string)item["display_name"] ?? "", Lat = lat, Lon = lon });
            }
        }

        private static void ReadPhoton(string text, List<Result> results)
        {
            var root = JObject.Parse(text);
            if (!(root["features"] is JArray features)) return;
            foreach (var feature in features)
            {
                var coords = feature["geometry"]?["coordinates"] as JArray;
                if (coords == null || coords.Count < 2) continue;
                if (!TryNumber(coords[0], out double lon) || !TryNumber(coords[1], out double lat)) continue;
                var p = feature["properties"];
                string name = (string)p?["name"];
                string street = (string)p?["street"];
                if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(street))
                    name = (street + " " + ((string)p?["housenumber"] ?? "")).Trim();
                var parts = new List<string>();
                foreach (string part in new[] { name, (string)p?["city"], (string)p?["state"], (string)p?["country"] })
                    if (!string.IsNullOrEmpty(part) && !parts.Contains(part)) parts.Add(part);
                if (parts.Count == 0) continue;
                results.Add(new Result { Name = string.Join(", ", parts), Lat = lat, Lon = lon });
            }
        }

        private static bool TryNumber(JToken token, out double value)
        {
            value = 0;
            if (token == null) return false;
            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
            {
                value = token.Value<double>();
                return true;
            }
            return double.TryParse((string)token, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string ToJson(List<Result> results)
        {
            var json = new JArray();
            foreach (var r in results)
                json.Add(new JObject { ["name"] = r.Name, ["lat"] = r.Lat, ["lon"] = r.Lon });
            return json.ToString(Newtonsoft.Json.Formatting.None);
        }

        public void Dispose()
        {
            m_Request?.Dispose();
            m_Request = null;
        }

        internal static bool TryParseCoordinates(string text, out double lat, out double lon)
        {
            lat = lon = 0;
            var match = s_Plain.Match(text);
            if (!match.Success) match = s_Google.Match(text);
            if (!match.Success) match = s_Osm.Match(text);
            if (!match.Success) return false;
            if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out lat)) return false;
            if (!double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out lon)) return false;
            return Math.Abs(lat) <= 90 && Math.Abs(lon) <= 180;
        }
    }
}
