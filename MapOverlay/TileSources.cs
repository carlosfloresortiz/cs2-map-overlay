using System;
using System.IO;
using UnityEngine;

namespace MapOverlay
{
    // Tile servers and the on-disk tile cache.
    internal static class TileSources
    {
        // Tile servers ask apps to identify themselves.
        public const string UserAgent = "CS2-MapOverlay/" + Mod.kVersion + " (Cities: Skylines II mod)";

        private static readonly string[] s_Subdomains = { "a", "b", "c" };

        public static string Template(Setting s)
        {
            switch (s.Provider)
            {
                case TileProvider.CartoLight: return "https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}.png";
                case TileProvider.CartoLightNoLabels: return "https://{s}.basemaps.cartocdn.com/light_nolabels/{z}/{x}/{y}.png";
                case TileProvider.CartoVoyager: return "https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png";
                case TileProvider.OpenTopoMap: return "https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png";
                case TileProvider.Custom: return (s.CustomUrl ?? "").Trim();
                default: return "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
            }
        }

        public static int MaxZoom(TileProvider provider) => provider == TileProvider.OpenTopoMap ? 17 : 19;

        public static string Attribution(TileProvider provider)
        {
            switch (provider)
            {
                case TileProvider.CartoLight:
                case TileProvider.CartoLightNoLabels:
                case TileProvider.CartoVoyager:
                    return "© OpenStreetMap contributors © CARTO";
                case TileProvider.OpenTopoMap:
                    return "© OpenStreetMap contributors, SRTM | © OpenTopoMap (CC-BY-SA)";
                case TileProvider.Custom:
                    return "";
                default:
                    return "© OpenStreetMap contributors";
            }
        }

        public static bool IsValidTemplate(string template) =>
            !string.IsNullOrEmpty(template)
            && (template.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || template.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            && template.Contains("{z}") && template.Contains("{x}") && template.Contains("{y}");

        public static string Url(string template, int z, int x, int y) =>
            template
                .Replace("{s}", s_Subdomains[(x + y) % s_Subdomains.Length])
                .Replace("{z}", z.ToString())
                .Replace("{x}", x.ToString())
                .Replace("{y}", y.ToString());

        // Folder name for a template, so switching servers never mixes tiles.
        public static string CacheKey(string template)
        {
            uint hash = 2166136261;
            foreach (char c in template)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash.ToString("x8");
        }
    }

    internal static class TileCache
    {
        // Tiles older than this are downloaded again; the old file is used if that fails.
        public static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

        private static volatile bool s_ClearRequested;

        public static string Root => Path.Combine(Application.persistentDataPath, "ModsData", nameof(MapOverlay), "tiles");

        public static string PathFor(string cacheKey, int z, int x, int y) =>
            Path.Combine(Root, cacheKey, z.ToString(), x.ToString(), y + ".png");

        public static void RequestClear() => s_ClearRequested = true;

        public static bool ConsumeClearRequest()
        {
            if (!s_ClearRequested) return false;
            s_ClearRequested = false;
            return true;
        }

        public static void ClearDisk()
        {
            try
            {
                if (Directory.Exists(Root)) Directory.Delete(Root, true);
                Mod.log.Info("Tile cache cleared.");
            }
            catch (Exception ex)
            {
                Mod.log.Error(ex, "Could not clear the tile cache.");
            }
        }

        public static bool TryRead(string path, out byte[] bytes, out bool stale)
        {
            bytes = null;
            stale = false;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length == 0) return false;
                stale = DateTime.UtcNow - info.LastWriteTimeUtc > MaxAge;
                bytes = File.ReadAllBytes(path);
                return true;
            }
            catch (Exception ex)
            {
                Mod.log.Warn($"Could not read cached tile {path}: {ex.Message}");
                return false;
            }
        }

        public static void Write(string path, byte[] bytes)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, bytes);
            }
            catch (Exception ex)
            {
                Mod.log.Warn($"Could not cache tile {path}: {ex.Message}");
            }
        }
    }
}
