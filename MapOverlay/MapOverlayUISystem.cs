using Colossal.UI.Binding;
using Game.UI;

namespace MapOverlay
{
    // Exposes the settings and the overlay state to the in-game panel (UI\src). Values are read
    // every UI update, so changes made from the Options menu also show up in the panel.
    public partial class MapOverlayUISystem : UISystemBase
    {
        private const string kGroup = "mapOverlay";

        protected override void OnCreate()
        {
            base.OnCreate();
            AddUpdateBinding(new GetterValueBinding<bool>(kGroup, "visible", () => Mod.Setting?.Visible ?? false));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "opacity", () => Mod.Setting?.Opacity ?? 50));
            AddUpdateBinding(new GetterValueBinding<bool>(kGroup, "onTop", () => Mod.Setting?.OnTop ?? true));
            AddUpdateBinding(new GetterValueBinding<bool>(kGroup, "follow", () => Mod.Setting?.FollowCamera ?? true));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "zoom", () => Mod.Controller?.EffectiveZoom ?? 17));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "area", () => Mod.Setting?.AreaKm ?? 3));
            AddUpdateBinding(new GetterValueBinding<float>(kGroup, "rotation", () => Mod.Setting?.Rotation ?? 0f));
            AddUpdateBinding(new GetterValueBinding<double>(kGroup, "lat", () => Mod.Setting?.CenterLat ?? 0));
            AddUpdateBinding(new GetterValueBinding<double>(kGroup, "lon", () => Mod.Setting?.CenterLon ?? 0));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "provider", () => (int)(Mod.Setting?.Provider ?? TileProvider.OpenStreetMap)));
            AddUpdateBinding(new GetterValueBinding<string>(kGroup, "attribution", () => TileSources.Attribution(Mod.Setting?.Provider ?? TileProvider.OpenStreetMap)));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "tilesReady", () => Mod.Controller?.TilesReady ?? 0));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "tilesTotal", () => Mod.Controller?.TilesTotal ?? 0));
            AddUpdateBinding(new GetterValueBinding<int>(kGroup, "tilesFailed", () => Mod.Controller?.TilesFailed ?? 0));
            AddUpdateBinding(new GetterValueBinding<string>(kGroup, "problem", () => Mod.Controller?.Problem ?? ""));
            AddUpdateBinding(new GetterValueBinding<string>(kGroup, "searchStatus", () => Mod.Controller?.Geocoder?.Status ?? ""));
            AddUpdateBinding(new GetterValueBinding<string>(kGroup, "searchResults", () => Mod.Controller?.Geocoder?.ResultsJson ?? "[]"));

            AddBinding(new TriggerBinding<bool>(kGroup, "setVisible", v => Change(s => s.Visible = v)));
            AddBinding(new TriggerBinding<int>(kGroup, "setOpacity", v => Change(s => s.Opacity = v)));
            AddBinding(new TriggerBinding<bool>(kGroup, "setOnTop", v => Change(s => s.OnTop = v)));
            AddBinding(new TriggerBinding<bool>(kGroup, "setFollow", v => Change(s => s.FollowCamera = v)));
            AddBinding(new TriggerBinding<int>(kGroup, "setZoom", v => Change(s => s.Zoom = v)));
            AddBinding(new TriggerBinding<int>(kGroup, "setArea", v => Change(s => s.AreaKm = v)));
            AddBinding(new TriggerBinding<int>(kGroup, "setProvider", SetProvider));
            AddBinding(new TriggerBinding<float>(kGroup, "setRotation", v => Mod.Controller?.SetRotation(v)));
            AddBinding(new TriggerBinding<float, float>(kGroup, "nudge", (right, forward) => Mod.Controller?.Nudge(right, forward)));
            AddBinding(new TriggerBinding(kGroup, "bringHere", () => Mod.Controller?.BringHere()));
            AddBinding(new TriggerBinding<string>(kGroup, "search", q => Mod.Controller?.Geocoder?.Search(q)));
            AddBinding(new TriggerBinding<string>(kGroup, "suggest", q => Mod.Controller?.Geocoder?.Suggest(q)));
            AddBinding(new TriggerBinding(kGroup, "clearSearch", () => Mod.Controller?.Geocoder?.Clear()));
            AddBinding(new TriggerBinding<double, double>(kGroup, "goTo", (lat, lon) => Mod.Controller?.GoTo(lat, lon)));
        }

        private static void Change(System.Action<Setting> change)
        {
            var setting = Mod.Setting;
            if (setting == null) return;
            change(setting);
            setting.ApplyAndSave();
        }

        private static void SetProvider(int value)
        {
            if (value < 0 || value > (int)TileProvider.Custom) return;
            Change(s => s.Provider = (TileProvider)value);
        }
    }
}
