using System;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;

namespace MapOverlay
{
    public enum TileProvider
    {
        OpenStreetMap,
        CartoLight,
        CartoLightNoLabels,
        CartoVoyager,
        OpenTopoMap,
        Custom,
    }

    [FileLocation(nameof(MapOverlay))]
    [SettingsUIGroupOrder(kViewGroup, kMapGroup, kCacheGroup)]
    [SettingsUIShowGroupName(kViewGroup, kMapGroup, kCacheGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";
        public const string kViewGroup = "View";
        public const string kMapGroup = "Map";
        public const string kCacheGroup = "Cache";

        public const int kMinZoom = 14;
        public const int kMaxZoom = 19;
        public const int kMinArea = 1;
        public const int kMaxArea = 10;

        // Shown in the panel when the place is unknown: the centre of the Zócalo, Mexico City.
        public const double kDefaultLat = 19.432608;
        public const double kDefaultLon = -99.133209;

        private int m_Opacity = 50;
        private int m_Zoom = 17;
        private int m_AreaKm = 3;
        private float m_HeightOffset = 1f;
        private float m_Rotation;

        public Setting(IMod mod) : base(mod)
        {
        }

        [SettingsUISection(kSection, kViewGroup)]
        public bool Visible { get; set; } = true;

        [SettingsUISlider(min = 0, max = 100, step = 1, unit = Unit.kPercentage, updateOnDragEnd = true)]
        [SettingsUISection(kSection, kViewGroup)]
        public int Opacity
        {
            get => m_Opacity;
            set => m_Opacity = Math.Max(0, Math.Min(100, value));
        }

        [SettingsUISection(kSection, kViewGroup)]
        public bool OnTop { get; set; } = true;

        [SettingsUISlider(min = 0, max = 50, step = 0.5f, unit = Unit.kFloatSingleFraction, updateOnDragEnd = true)]
        [SettingsUISection(kSection, kViewGroup)]
        public float HeightOffset
        {
            get => m_HeightOffset;
            set => m_HeightOffset = Math.Max(0f, Math.Min(50f, value));
        }

        [SettingsUISection(kSection, kMapGroup)]
        public TileProvider Provider { get; set; } = TileProvider.OpenStreetMap;

        [SettingsUITextInput]
        [SettingsUISection(kSection, kMapGroup)]
        [SettingsUIHideByCondition(typeof(Setting), nameof(IsNotCustomProvider))]
        public string CustomUrl { get; set; } = "";

        [SettingsUISlider(min = kMinZoom, max = kMaxZoom, step = 1, unit = Unit.kInteger, updateOnDragEnd = true)]
        [SettingsUISection(kSection, kMapGroup)]
        public int Zoom
        {
            get => m_Zoom;
            set => m_Zoom = Math.Max(kMinZoom, Math.Min(kMaxZoom, value));
        }

        [SettingsUISlider(min = kMinArea, max = kMaxArea, step = 1, unit = Unit.kInteger, updateOnDragEnd = true)]
        [SettingsUISection(kSection, kMapGroup)]
        public int AreaKm
        {
            get => m_AreaKm;
            set => m_AreaKm = Math.Max(kMinArea, Math.Min(kMaxArea, value));
        }

        [SettingsUISection(kSection, kMapGroup)]
        public bool FollowCamera { get; set; } = true;

        // Setter-only bool renders as a button and is not saved.
        [SettingsUISection(kSection, kCacheGroup)]
        [SettingsUIConfirmation]
        public bool ClearCache { set { TileCache.RequestClear(); } }

        // Placement, edited from the in-game panel.
        [SettingsUIHidden]
        public double CenterLat { get; set; } = kDefaultLat;

        [SettingsUIHidden]
        public double CenterLon { get; set; } = kDefaultLon;

        [SettingsUIHidden]
        public double AnchorX { get; set; }

        [SettingsUIHidden]
        public double AnchorZ { get; set; }

        [SettingsUIHidden]
        public float Rotation
        {
            get => m_Rotation;
            set => m_Rotation = NormalizeAngle(value);
        }

        public bool IsNotCustomProvider() => Provider != TileProvider.Custom;

        internal static float NormalizeAngle(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees)) return 0f;
            degrees %= 360f;
            if (degrees > 180f) degrees -= 360f;
            else if (degrees <= -180f) degrees += 360f;
            return degrees;
        }

        public override void SetDefaults()
        {
            Visible = true;
            Opacity = 50;
            OnTop = true;
            HeightOffset = 1f;
            Provider = TileProvider.OpenStreetMap;
            CustomUrl = "";
            Zoom = 17;
            AreaKm = 3;
            FollowCamera = true;
            CenterLat = kDefaultLat;
            CenterLon = kDefaultLon;
            AnchorX = 0;
            AnchorZ = 0;
            Rotation = 0f;
        }
    }
}
