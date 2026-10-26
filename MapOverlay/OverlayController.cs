using System;
using System.Collections.Generic;
using Game;
using Game.Rendering;
using Game.SceneFlow;
using Game.Simulation;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MapOverlay
{
    // Draws map tiles over the terrain at real-world scale and downloads them as needed.
    // Runs as a Unity component so it keeps working while the game is paused.
    public class OverlayController : MonoBehaviour
    {
        // Above this the area shrinks: memory and the tile servers' fair-use limits.
        private const int kMaxTiles = 900;
        private const int kMaxDownloads = 2;
        private const int kMaxCacheLoadsPerFrame = 6;
        private const int kHeightTilesPerFrame = 24;
        private const int kGrid = 16;

        private sealed class Tile
        {
            public int X;      // Unwrapped; may fall outside 0..2^z-1 near the antimeridian.
            public int Y;
            public int Z;
            public GameObject Object;
            public MeshRenderer Renderer;
            public Mesh Mesh;
            public Material Material;
            public Texture2D Texture;
            public Vector3[] Vertices;
            public TileState State;
            public bool Removed;
            public double Priority;
        }

        private enum TileState { Pending, Loading, Ready, Failed }

        private sealed class Download
        {
            public Tile Tile;
            public UnityWebRequest Request;
            public string Path;
            public byte[] Stale;
        }

        private static int[] s_Triangles;
        private static Vector2[] s_Uvs;

        private Setting m_Setting;
        private Shader m_Shader;
        private Material m_DarkenMaterial;
        private GameObject m_Root;
        private readonly Dictionary<long, Tile> m_Tiles = new Dictionary<long, Tile>();
        private readonly List<Tile> m_TileList = new List<Tile>();
        private readonly List<Tile> m_Pending = new List<Tile>();
        private readonly List<Download> m_Downloads = new List<Download>();
        private MapFrame m_Frame;
        private int m_HeightCursor;
        private float m_BackoffUntil;
        private bool m_LoggedUpdateError;

        // Last applied state; a change triggers a rebuild.
        private double m_LayoutLat = double.NaN, m_LayoutLon, m_LayoutAnchorX, m_LayoutAnchorZ;
        private float m_LayoutRotation;
        private int m_LayoutZoom, m_LayoutArea;
        private string m_LayoutTemplate;
        private string m_CacheKey;
        private int m_MaterialOpacity = -1;
        private bool m_MaterialOnTop;

        internal Geocoder Geocoder { get; private set; }
        internal int TilesTotal => m_TileList.Count;
        internal int TilesReady { get; private set; }
        internal int TilesFailed { get; private set; }
        internal string Problem { get; private set; } = "";
        internal int EffectiveZoom => Math.Min(m_Setting.Zoom, TileSources.MaxZoom(m_Setting.Provider));

        public void Initialize(Setting setting)
        {
            m_Setting = setting;
            Geocoder = new Geocoder(this);
            m_Root = new GameObject("MapOverlayTiles") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(m_Root);
            m_Shader = Shader.Find("HDRP/Unlit");
            if (m_Shader == null) Mod.log.Error("Shader HDRP/Unlit not found; the overlay cannot be drawn.");
            else m_DarkenMaterial = CreateMaterial(true);
            BuildSharedGrid();
        }

        public void Shutdown()
        {
            foreach (var d in m_Downloads) d.Request.Dispose();
            m_Downloads.Clear();
            Geocoder?.Dispose();
            ClearTiles();
            if (m_Root != null) Destroy(m_Root);
            if (m_DarkenMaterial != null) Destroy(m_DarkenMaterial);
            m_Root = null;
        }

        private void Update()
        {
            if (m_Setting == null) return;
            try
            {
                Geocoder.Tick();
                if (TileCache.ConsumeClearRequest())
                {
                    ClearTiles();
                    TileCache.ClearDisk();
                }

                bool active = m_Setting.Visible && m_Shader != null && GameManager.instance != null
                    && GameManager.instance.gameMode.IsGameOrEditor();
                if (m_Root.activeSelf != active) m_Root.SetActive(active);
                // Downloads in flight finish even when hidden, but nothing new starts.
                PollDownloads();
                if (!active) return;

                FollowCamera();
                if (LayoutChanged()) RebuildLayout();
                if (m_MaterialOpacity != m_Setting.Opacity || m_MaterialOnTop != m_Setting.OnTop) ApplyMaterialSettings();
                StartLoads();
                RefreshHeights(kHeightTilesPerFrame);
            }
            catch (Exception ex)
            {
                if (!m_LoggedUpdateError) Mod.log.Error(ex, "Overlay update failed.");
                m_LoggedUpdateError = true;
            }
        }

        // ---- Placement ---------------------------------------------------------------------

        internal bool TryGetCameraPivot(out float3 pivot)
        {
            pivot = default;
            var world = World.DefaultGameObjectInjectionWorld;
            var controller = world?.GetExistingSystemManaged<CameraUpdateSystem>()?.activeCameraController;
            if (controller == null) return false;
            pivot = controller.pivot;
            return true;
        }

        private Transform CameraTransform()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var camera = world?.GetExistingSystemManaged<CameraUpdateSystem>()?.activeViewer?.camera;
            if (camera == null) camera = Camera.main;
            return camera != null ? camera.transform : null;
        }

        private MapFrame CurrentFrame() =>
            new MapFrame(m_Setting.CenterLat, m_Setting.CenterLon, m_Setting.AnchorX, m_Setting.AnchorZ, m_Setting.Rotation);

        // Moves the anchor to (x, z) without moving the map content on the ground.
        private void RecenterAt(double x, double z)
        {
            CurrentFrame().ToGeo(x, z, out double lat, out double lon);
            m_Setting.CenterLat = lat;
            m_Setting.CenterLon = lon;
            m_Setting.AnchorX = x;
            m_Setting.AnchorZ = z;
        }

        private void FollowCamera()
        {
            if (!m_Setting.FollowCamera || !TryGetCameraPivot(out var pivot)) return;
            double dx = pivot.x - m_Setting.AnchorX;
            double dz = pivot.z - m_Setting.AnchorZ;
            double limit = m_Setting.AreaKm * 1000.0 / 4.0;
            if (dx * dx + dz * dz < limit * limit) return;
            RecenterAt(pivot.x, pivot.z);
            m_Setting.ApplyAndSave();
        }

        // Puts the given place under the camera.
        internal void GoTo(double lat, double lon)
        {
            m_Setting.CenterLat = GeoMath.ClampLatitude(lat);
            m_Setting.CenterLon = GeoMath.WrapLongitude(lon);
            if (TryGetCameraPivot(out var pivot))
            {
                m_Setting.AnchorX = pivot.x;
                m_Setting.AnchorZ = pivot.z;
            }
            m_Setting.Visible = true;
            m_Setting.ApplyAndSave();
            Mod.log.Info($"Go to {lat:F6}, {lon:F6}");
        }

        // Brings the current map centre to the camera.
        internal void BringHere()
        {
            if (!TryGetCameraPivot(out var pivot)) return;
            m_Setting.AnchorX = pivot.x;
            m_Setting.AnchorZ = pivot.z;
            m_Setting.ApplyAndSave();
        }

        // Slides the map on the ground. right/forward follow the camera's view.
        internal void Nudge(float right, float forward)
        {
            var cam = CameraTransform();
            Vector3 f = cam != null ? cam.forward : Vector3.forward;
            f.y = 0;
            if (f.sqrMagnitude < 1e-6f) f = cam != null ? cam.up : Vector3.forward;
            f.y = 0;
            f.Normalize();
            var r = new Vector3(f.z, 0, -f.x);
            Vector3 move = r * right + f * forward;
            // The anchor stays put; what is shown there changes.
            var frame = CurrentFrame();
            frame.ToGeo(m_Setting.AnchorX - move.x, m_Setting.AnchorZ - move.z, out double lat, out double lon);
            m_Setting.CenterLat = lat;
            m_Setting.CenterLon = lon;
            m_Setting.ApplyAndSave();
        }

        // Turns the map around the point the camera looks at.
        internal void SetRotation(float degrees)
        {
            if (TryGetCameraPivot(out var pivot)) RecenterAt(pivot.x, pivot.z);
            m_Setting.Rotation = degrees;
            m_Setting.ApplyAndSave();
        }

        // ---- Layout ------------------------------------------------------------------------

        private bool LayoutChanged() =>
            m_Setting.CenterLat != m_LayoutLat || m_Setting.CenterLon != m_LayoutLon
            || m_Setting.AnchorX != m_LayoutAnchorX || m_Setting.AnchorZ != m_LayoutAnchorZ
            || m_Setting.Rotation != m_LayoutRotation || EffectiveZoom != m_LayoutZoom
            || m_Setting.AreaKm != m_LayoutArea || TileSources.Template(m_Setting) != m_LayoutTemplate;

        private void RebuildLayout()
        {
            string template = TileSources.Template(m_Setting);
            if (template != m_LayoutTemplate)
            {
                ClearTiles();
                m_CacheKey = TileSources.CacheKey(template);
            }
            m_LayoutLat = m_Setting.CenterLat;
            m_LayoutLon = m_Setting.CenterLon;
            m_LayoutAnchorX = m_Setting.AnchorX;
            m_LayoutAnchorZ = m_Setting.AnchorZ;
            m_LayoutRotation = m_Setting.Rotation;
            m_LayoutZoom = EffectiveZoom;
            m_LayoutArea = m_Setting.AreaKm;
            m_LayoutTemplate = template;
            m_Frame = CurrentFrame();

            // A new template already cleared the tiles above.
            if (!TileSources.IsValidTemplate(template))
            {
                Problem = "BadUrl";
                return;
            }
            if (Problem == "BadUrl") Problem = "";

            int z = m_LayoutZoom;
            double cx = GeoMath.TileX(m_LayoutLon, z);
            double cy = GeoMath.TileY(m_LayoutLat, z);
            double half = m_LayoutArea * 1000.0 / 2.0 / GeoMath.TileMetres(m_LayoutLat, z);
            half = Math.Min(half, Math.Sqrt(kMaxTiles) / 2.0 - 0.5);
            int n = 1 << z;
            int x0 = (int)Math.Floor(cx - half), x1 = (int)Math.Floor(cx + half);
            int y0 = Math.Max(0, (int)Math.Floor(cy - half)), y1 = Math.Min(n - 1, (int)Math.Floor(cy + half));

            var needed = new HashSet<long>();
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    needed.Add(Key(z, x, y));

            for (int i = m_TileList.Count - 1; i >= 0; i--)
            {
                var tile = m_TileList[i];
                if (needed.Contains(Key(tile.Z, tile.X, tile.Y))) continue;
                DestroyTile(tile);
                m_TileList.RemoveAt(i);
                m_Tiles.Remove(Key(tile.Z, tile.X, tile.Y));
            }

            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    long key = Key(z, x, y);
                    if (m_Tiles.ContainsKey(key)) continue;
                    var tile = CreateTile(z, x, y);
                    m_Tiles.Add(key, tile);
                    m_TileList.Add(tile);
                }

            m_Pending.Clear();
            foreach (var tile in m_TileList)
            {
                PlaceTile(tile);
                double dx = tile.X + 0.5 - cx, dy = tile.Y + 0.5 - cy;
                tile.Priority = dx * dx + dy * dy;
                if (tile.State == TileState.Failed) tile.State = TileState.Pending;
                if (tile.State == TileState.Pending) m_Pending.Add(tile);
            }
            m_Pending.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            RefreshHeights(m_TileList.Count);
            CountStates();
        }

        private static long Key(int z, int x, int y) => ((long)z << 58) ^ ((long)(x & 0x1FFFFFFF) << 29) ^ (uint)(y & 0x1FFFFFFF);

        private Tile CreateTile(int z, int x, int y)
        {
            var go = new GameObject($"Tile {z}/{x}/{y}") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(m_Root.transform, false);
            var mesh = new Mesh { name = go.name, hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            var vertices = new Vector3[s_Uvs.Length];
            mesh.vertices = vertices;
            mesh.uv = s_Uvs;
            mesh.triangles = s_Triangles;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return new Tile { X = x, Y = y, Z = z, Object = go, Renderer = renderer, Mesh = mesh, Vertices = vertices };
        }

        // Ground position of every grid point of the tile; heights come later.
        private void PlaceTile(Tile tile)
        {
            int i = 0;
            for (int row = 0; row <= kGrid; row++)
            {
                double lat = GeoMath.TileToLat(tile.Y + (double)row / kGrid, tile.Z);
                for (int col = 0; col <= kGrid; col++, i++)
                {
                    double lon = GeoMath.TileToLon(tile.X + (double)col / kGrid, tile.Z);
                    m_Frame.ToWorld(lat, lon, out double x, out double z);
                    tile.Vertices[i] = new Vector3((float)x, tile.Vertices[i].y, (float)z);
                }
            }
        }

        private void RefreshHeights(int count)
        {
            if (m_TileList.Count == 0) return;
            var terrain = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<TerrainSystem>();
            if (terrain == null) return;
            var data = terrain.GetHeightData();
            if (!data.isCreated || data.resolution.x <= 2) return;
            float offset = m_Setting.HeightOffset;
            count = Math.Min(count, m_TileList.Count);
            for (int n = 0; n < count; n++)
            {
                m_HeightCursor = (m_HeightCursor + 1) % m_TileList.Count;
                var tile = m_TileList[m_HeightCursor];
                var v = tile.Vertices;
                for (int i = 0; i < v.Length; i++)
                    v[i].y = TerrainUtils.SampleHeight(ref data, new float3(v[i].x, 0f, v[i].z)) + offset;
                tile.Mesh.vertices = v;
                tile.Mesh.RecalculateBounds();
            }
        }

        private static void BuildSharedGrid()
        {
            if (s_Triangles != null) return;
            int side = kGrid + 1;
            s_Uvs = new Vector2[side * side];
            for (int row = 0, i = 0; row < side; row++)
                for (int col = 0; col < side; col++, i++)
                    s_Uvs[i] = new Vector2((float)col / kGrid, 1f - (float)row / kGrid);
            s_Triangles = new int[kGrid * kGrid * 6];
            for (int row = 0, t = 0; row < kGrid; row++)
                for (int col = 0; col < kGrid; col++)
                {
                    int a = row * side + col, b = a + 1, c = a + side, d = c + 1;
                    s_Triangles[t++] = a; s_Triangles[t++] = b; s_Triangles[t++] = c;
                    s_Triangles[t++] = b; s_Triangles[t++] = d; s_Triangles[t++] = c;
                }
        }

        private void DestroyTile(Tile tile)
        {
            tile.Removed = true;
            if (tile.Object != null) Destroy(tile.Object);
            if (tile.Mesh != null) Destroy(tile.Mesh);
            if (tile.Material != null) Destroy(tile.Material);
            if (tile.Texture != null) Destroy(tile.Texture);
        }

        private void ClearTiles()
        {
            foreach (var tile in m_TileList) DestroyTile(tile);
            m_TileList.Clear();
            m_Tiles.Clear();
            m_Pending.Clear();
            m_LayoutLat = double.NaN;
            m_LayoutTemplate = null;
            CountStates();
        }

        private void CountStates()
        {
            int ready = 0, failed = 0;
            foreach (var tile in m_TileList)
            {
                if (tile.State == TileState.Ready) ready++;
                else if (tile.State == TileState.Failed) failed++;
            }
            TilesReady = ready;
            TilesFailed = failed;
        }

        // ---- Loading -----------------------------------------------------------------------

        private void StartLoads()
        {
            int cacheLoads = 0;
            for (int i = 0; i < m_Pending.Count;)
            {
                var tile = m_Pending[i];
                if (tile.Removed || tile.State != TileState.Pending)
                {
                    m_Pending.RemoveAt(i);
                    continue;
                }
                string path = TileCache.PathFor(m_CacheKey, tile.Z, Wrap(tile.X, tile.Z), tile.Y);
                if (cacheLoads < kMaxCacheLoadsPerFrame && TileCache.TryRead(path, out var bytes, out bool stale))
                {
                    cacheLoads++;
                    if (!stale || !CanDownload())
                    {
                        // A stale tile shows now and is refreshed on a later visit.
                        SetTexture(tile, bytes);
                        m_Pending.RemoveAt(i);
                        continue;
                    }
                    StartDownload(tile, path, bytes);
                    m_Pending.RemoveAt(i);
                    continue;
                }
                if (CanDownload())
                {
                    StartDownload(tile, path, null);
                    m_Pending.RemoveAt(i);
                    continue;
                }
                break;
            }
        }

        private bool CanDownload() => m_Downloads.Count < kMaxDownloads && Time.realtimeSinceStartup >= m_BackoffUntil;

        private static int Wrap(int x, int z)
        {
            int n = 1 << z;
            return ((x % n) + n) % n;
        }

        private void StartDownload(Tile tile, string path, byte[] stale)
        {
            string url = TileSources.Url(m_LayoutTemplate, tile.Z, Wrap(tile.X, tile.Z), tile.Y);
            var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("User-Agent", TileSources.UserAgent);
            request.timeout = 20;
            request.SendWebRequest();
            tile.State = TileState.Loading;
            m_Downloads.Add(new Download { Tile = tile, Request = request, Path = path, Stale = stale });
        }

        private void PollDownloads()
        {
            bool changed = false;
            for (int i = m_Downloads.Count - 1; i >= 0; i--)
            {
                var d = m_Downloads[i];
                if (!d.Request.isDone) continue;
                m_Downloads.RemoveAt(i);
                changed = true;
                using (var request = d.Request)
                {
                    if (request.result == UnityWebRequest.Result.Success && request.downloadHandler.data?.Length > 0)
                    {
                        byte[] bytes = request.downloadHandler.data;
                        TileCache.Write(d.Path, bytes);
                        if (!d.Tile.Removed) SetTexture(d.Tile, bytes);
                        if (Problem == "Network" || Problem == "Blocked") Problem = "";
                        continue;
                    }

                    long code = request.responseCode;
                    if (code == 429 || code == 403)
                    {
                        // The server asked us to slow down or refused us: pause for a minute.
                        m_BackoffUntil = Time.realtimeSinceStartup + 60f;
                        Problem = "Blocked";
                        Mod.log.Warn($"Tile server answered {code} for {request.url}. Pausing downloads for 60 s.");
                    }
                    else if (code != 404)
                    {
                        Problem = "Network";
                        Mod.log.Warn($"Tile download failed ({code} {request.error}) for {request.url}");
                    }

                    if (d.Tile.Removed) continue;
                    if (d.Stale != null) SetTexture(d.Tile, d.Stale);
                    else d.Tile.State = TileState.Failed;
                }
            }
            if (changed) CountStates();
        }

        private void SetTexture(Tile tile, byte[] bytes)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true)
            {
                name = tile.Object.name,
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            if (!texture.LoadImage(bytes, true))
            {
                Destroy(texture);
                tile.State = TileState.Failed;
                Mod.log.Warn($"Could not decode tile {tile.Object.name}.");
                return;
            }
            if (tile.Texture != null) Destroy(tile.Texture);
            tile.Texture = texture;
            if (tile.Material == null)
            {
                tile.Material = CreateMaterial(false);
                // One submesh and two materials: Unity draws the mesh once with each.
                tile.Renderer.sharedMaterials = new[] { m_DarkenMaterial, tile.Material };
            }
            tile.Material.SetTexture("_UnlitColorMap", texture);
            tile.Renderer.enabled = m_Setting.Opacity > 0;
            tile.State = TileState.Ready;
            CountStates();
        }

        // ---- Materials ---------------------------------------------------------------------

        // The game ships HDRP/Unlit without its transparent variant, so the shader always writes
        // alpha 1 and regular alpha blending comes out opaque. Instead each tile is drawn twice
        // with blend states that never read alpha:
        //   darken: colour a,       Blend Zero OneMinusSrcColor -> dst * (1 - a)
        //   map:    colour tile * a, Blend One One              -> + tile * a
        // which adds up to lerp(dst, tile, a).
        private Material CreateMaterial(bool darken)
        {
            var material = new Material(m_Shader) { hideFlags = HideFlags.HideAndDontSave };
            material.SetFloat("_SurfaceType", 1f);  // Transparent
            material.SetFloat("_BlendMode", 0f);    // Alpha
            material.SetFloat("_DoubleSidedEnable", 1f);
            material.SetFloat("_EnableFogOnTransparent", 0f);
            ConfigureMaterial(material, darken);
            return material;
        }

        private void ConfigureMaterial(Material material, bool darken)
        {
            float a = m_Setting.Opacity / 100f;
            material.SetColor("_UnlitColor", new Color(a, a, a, 1f));
            material.SetFloat("_ZTestTransparent", (float)(m_Setting.OnTop ? CompareFunction.Always : CompareFunction.LessEqual));
            HDMaterial.ValidateMaterial(material);
            // After validation, which sets its own premultiplied-alpha blend states.
            material.SetInt("_SrcBlend", (int)(darken ? UnityEngine.Rendering.BlendMode.Zero : UnityEngine.Rendering.BlendMode.One));
            material.SetInt("_DstBlend", (int)(darken ? UnityEngine.Rendering.BlendMode.OneMinusSrcColor : UnityEngine.Rendering.BlendMode.One));
            material.SetInt("_AlphaSrcBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            material.SetInt("_AlphaDstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_ZWrite", 0);
            // Every darken pass runs before every map pass; tiles never overlap.
            material.renderQueue = (int)RenderQueue.Transparent + (darken ? 0 : 1);
        }

        private void ApplyMaterialSettings()
        {
            m_MaterialOpacity = m_Setting.Opacity;
            m_MaterialOnTop = m_Setting.OnTop;
            ConfigureMaterial(m_DarkenMaterial, true);
            foreach (var tile in m_TileList)
            {
                if (tile.Material == null) continue;
                ConfigureMaterial(tile.Material, false);
                tile.Renderer.enabled = m_Setting.Opacity > 0;
            }
        }
    }
}
