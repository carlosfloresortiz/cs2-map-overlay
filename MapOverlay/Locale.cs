using System.Collections.Generic;
using Colossal;

namespace MapOverlay
{
    // English only; registered for every game language.
    public class Locale : IDictionarySource
    {
        // Keys read by the in-game panel (UI/src/mods/panel.tsx).
        public const string kUIPrefix = "MapOverlay.UI.";

        private readonly Setting m_Setting;

        public Locale(Setting setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            var s = m_Setting;
            return new Dictionary<string, string>
            {
                { s.GetSettingsLocaleID(), "Map Overlay" },
                { s.GetOptionTabLocaleID(Setting.kSection), "Main" },

                { s.GetOptionGroupLocaleID(Setting.kViewGroup), "View" },
                { s.GetOptionGroupLocaleID(Setting.kMapGroup), "Map" },
                { s.GetOptionGroupLocaleID(Setting.kCacheGroup), "Cache" },

                { s.GetOptionLabelLocaleID(nameof(Setting.Visible)), "Show map" },
                { s.GetOptionDescLocaleID(nameof(Setting.Visible)), "Shows the map over the terrain. You can also use the mod's button at the top left of the game." },
                { s.GetOptionLabelLocaleID(nameof(Setting.Opacity)), "Opacity" },
                { s.GetOptionDescLocaleID(nameof(Setting.Opacity)), "How visible the map is. 100% makes it opaque." },
                { s.GetOptionLabelLocaleID(nameof(Setting.OnTop)), "On top of everything" },
                { s.GetOptionDescLocaleID(nameof(Setting.OnTop)), "Draws the map over buildings, roads and hills, like tracing paper. Turn off to lay it on the terrain so objects cover it." },
                { s.GetOptionLabelLocaleID(nameof(Setting.HeightOffset)), "Height above terrain (m)" },
                { s.GetOptionDescLocaleID(nameof(Setting.HeightOffset)), "Gap between the map and the terrain. Raise it if the map flickers or sinks into the ground with \"On top of everything\" off." },

                { s.GetOptionLabelLocaleID(nameof(Setting.Provider)), "Map style" },
                { s.GetOptionDescLocaleID(nameof(Setting.Provider)), "Tile server. \"CARTO light, no labels\" shows the street layout most clearly." },
                { s.GetEnumValueLocaleID(TileProvider.OpenStreetMap), "OpenStreetMap" },
                { s.GetEnumValueLocaleID(TileProvider.CartoLight), "CARTO light" },
                { s.GetEnumValueLocaleID(TileProvider.CartoLightNoLabels), "CARTO light, no labels" },
                { s.GetEnumValueLocaleID(TileProvider.CartoVoyager), "CARTO Voyager" },
                { s.GetEnumValueLocaleID(TileProvider.OpenTopoMap), "OpenTopoMap (terrain)" },
                { s.GetEnumValueLocaleID(TileProvider.Custom), "Custom" },
                { s.GetOptionLabelLocaleID(nameof(Setting.CustomUrl)), "Tile URL" },
                { s.GetOptionDescLocaleID(nameof(Setting.CustomUrl)), "XYZ template, for example https://server/{z}/{x}/{y}.png. {s} becomes a, b or c. Follow the server's terms of use." },
                { s.GetOptionLabelLocaleID(nameof(Setting.Zoom)), "Detail level" },
                { s.GetOptionDescLocaleID(nameof(Setting.Zoom)), "Tile zoom level. It does not change the scale, which is always 1:1, only the sharpness. Higher = sharper, but more downloads." },
                { s.GetOptionLabelLocaleID(nameof(Setting.AreaKm)), "Area size (km)" },
                { s.GetOptionDescLocaleID(nameof(Setting.AreaKm)), "Side of the square of map loaded around the anchor point." },
                { s.GetOptionLabelLocaleID(nameof(Setting.FollowCamera)), "Load where you look" },
                { s.GetOptionDescLocaleID(nameof(Setting.FollowCamera)), "When the camera moves away from the centre, loads the map of the new area without moving it." },

                { s.GetOptionLabelLocaleID(nameof(Setting.ClearCache)), "Clear tile cache" },
                { s.GetOptionDescLocaleID(nameof(Setting.ClearCache)), "Deletes downloaded tiles in ModsData\\MapOverlay\\tiles. They download again when shown." },
                { s.GetOptionWarningLocaleID(nameof(Setting.ClearCache)), "Delete all downloaded tiles?" },

                { kUIPrefix + "Title", "Map Overlay" },
                { kUIPrefix + "Visible", "Show map" },
                { kUIPrefix + "Opacity", "Opacity" },
                { kUIPrefix + "OnTop", "On top of everything" },
                { kUIPrefix + "Follow", "Load where you look" },
                { kUIPrefix + "SearchPlaceholder", "Place, address or coordinates" },
                { kUIPrefix + "Search", "Search" },
                { kUIPrefix + "Searching", "Searching…" },
                { kUIPrefix + "NoResults", "No results." },
                { kUIPrefix + "SearchError", "Search failed. Check your connection." },
                { kUIPrefix + "SearchHint", "The chosen place appears where the camera looks." },
                { kUIPrefix + "Move", "Move map" },
                { kUIPrefix + "Step", "Step" },
                { kUIPrefix + "MoveHint", "Arrows follow the camera view." },
                { kUIPrefix + "BringHere", "Bring centre here" },
                { kUIPrefix + "Rotation", "Rotation" },
                { kUIPrefix + "Reset", "Reset" },
                { kUIPrefix + "Style", "Style" },
                { kUIPrefix + "Detail", "Detail" },
                { kUIPrefix + "Area", "Area" },
                { kUIPrefix + "Tiles", "Tiles" },
                { kUIPrefix + "Failed", "failed" },
                { kUIPrefix + "ProblemNetwork", "Some tiles did not download. Check your connection." },
                { kUIPrefix + "ProblemBlocked", "The server asked to wait. Downloads resume in a minute." },
                { kUIPrefix + "ProblemBadUrl", "The custom URL is not valid (Options)." },
                { kUIPrefix + "Provider.OpenStreetMap", "OpenStreetMap" },
                { kUIPrefix + "Provider.CartoLight", "CARTO light" },
                { kUIPrefix + "Provider.CartoLightNoLabels", "CARTO light, no labels" },
                { kUIPrefix + "Provider.CartoVoyager", "CARTO Voyager" },
                { kUIPrefix + "Provider.OpenTopoMap", "OpenTopoMap" },
                { kUIPrefix + "Provider.Custom", "Custom" },
            };
        }

        public void Unload()
        {
        }
    }
}
