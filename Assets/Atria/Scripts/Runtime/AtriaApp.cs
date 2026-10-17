using System.Collections.Generic;
using Atria.App.UI;
using Atria.Core.Data;
using Atria.Core.Navigation;
using Atria.Core.Search;
using UnityEngine;

namespace Atria.App
{
    /// <summary>
    /// Composition root and screen flow: loads the packaged building data, builds the 3D view and
    /// UI, and wires search -> entrance -> route -> walkthrough. Everything runs offline from
    /// assets inside the app; nothing is fetched at runtime.
    /// </summary>
    public sealed class AtriaApp : MonoBehaviour
    {
        [Header("Data (packaged under a Resources folder)")]
        [Tooltip("Resources path of the building JSON, without extension.")]
        public string BuildingResourcePath = "Atria/Data/building";
        [Tooltip("Development only: drag a building JSON here (e.g. Assets/Atria/MappingData/prp_g_left_draft.json) to load it instead of the Resources file. Leave empty for the default demo.")]
        public TextAsset BuildingDataOverride;

        [Header("Navigation")]
        [Tooltip("Walking speed for ETAs in m/s. 0 = use the value in the building file.")]
        public float WalkingSpeedOverride = 0f;
        public RoutePathSettings PathSettings = new RoutePathSettings();

        [Header("Walkthrough")]
        [Tooltip("Autoplay speed as a multiple of walking speed.")]
        public float AutoplaySpeedMultiplier = 2f;
        public float EyeHeight = 1.6f;
        public float HorizontalFov = 75f;
        public float PitchDegrees = 6f;

        [Header("Rendering / performance")]
        public int TargetFrameRate = 60;
        public Color SkyColor = new Color32(196, 216, 236, 255);
        public BuildingVisualSettings Visuals = new BuildingVisualSettings();

        private BuildingData _building;
        private RouteFinder _router;
        private RoomSearch _search;
        private BuildingView _view;
        private RouteVisuals _routeVisuals;
        private RouteCameraController _cameraController;
        private Camera _camera;
        private AtriaUI _ui;
        private Material _surfaceMaterial, _signMaterial;
        private Font _font;

        private RoomData _selectedRoom;
        private int _selectedEntrance = -1;
        private RouteResult _route;
        private RoutePath _path;
        private bool _autoplay;
        private Rect _lastViewport;

        private void Awake()
        {
            Application.targetFrameRate = TargetFrameRate;
            QualitySettings.vSyncCount = 0;
            QualitySettings.shadows = ShadowQuality.Disable;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateMaterials();

            string loadError = LoadBuilding();
            CreateCamera();
            _ui = new AtriaUI();
            _ui.Build(transform, _font, BannerText(), WalkthroughTag(), _building != null ? _building.Entrances : new List<EntranceData>());
            WireUi();

            if (loadError != null)
            {
                _ui.SetResults(new List<RoomMatch>(), loadError);
                _ui.SetRouteSummary("Building data could not be loaded.", true, false);
                return;
            }

            _view = BuildingRenderer.Build(_building, transform, _surfaceMaterial, _signMaterial, _font, Visuals);
            _routeVisuals = new RouteVisuals(transform, _surfaceMaterial);
            if (_building.Entrances.Count > 0) _selectedEntrance = 0;
            _ui.SetSelectedEntrance(_selectedEntrance);
            OnQueryChanged(string.Empty);
            UpdateRoute();
        }

        private string LoadBuilding()
        {
            bool fromResources = BuildingDataOverride == null;
            TextAsset asset = fromResources ? Resources.Load<TextAsset>(BuildingResourcePath) : BuildingDataOverride;
            if (asset == null) return $"Missing building data at Resources/{BuildingResourcePath}.json";
            try
            {
                _building = BuildingDataLoader.FromJson(asset.text);
            }
            catch (System.FormatException e)
            {
                Debug.LogError("[Atria] " + e.Message);
                return "Building data is invalid: " + e.Message;
            }
            finally
            {
                if (fromResources) Resources.UnloadAsset(asset);
            }

            ValidationReport report = BuildingValidator.Validate(_building);
            Debug.Log($"[Atria] Loaded {_building.DisplayName} (status: {_building.DataStatus}) from {(fromResources ? "Resources/" + BuildingResourcePath : asset.name)}");
            if (!report.IsValid) Debug.LogError("[Atria] Building data validation failed: " + report);
            else if (report.Warnings.Count > 0) Debug.LogWarning("[Atria] Building data warnings: " + report);
            _router = new RouteFinder(_building);
            _search = new RoomSearch(_building.Rooms);
            return null;
        }

        private string BannerText()
        {
            if (_building == null || _building.IsVerified) return null;
            return _building.DataStatus == "sample"
                ? "SAMPLE DATA – this demo layout and its DEMO room numbers are NOT the real PRP building."
                : $"PROVISIONAL RECONSTRUCTION – {_building.DisplayName}. Layout estimated from sketches, not surveyed. NOT for real navigation.";
        }

        /// <summary>Short reminder kept visible on the walkthrough screen.</summary>
        private string WalkthroughTag()
        {
            if (_building == null || _building.IsVerified) return null;
            return _building.DataStatus == "sample" ? "SAMPLE LAYOUT – not the real building" : "PROVISIONAL PRP DRAFT – not surveyed";
        }

        private void CreateMaterials()
        {
            // Both shaders live under a Resources folder so they are always included in builds.
            Shader surface = Shader.Find("Atria/VertexLit");
            Shader text = Shader.Find("Atria/SignText");
            if (surface == null || text == null) Debug.LogError("[Atria] Atria shaders not found; check Assets/Atria/Resources/Atria/Shaders.");
            _surfaceMaterial = new Material(surface != null ? surface : Shader.Find("Unlit/Color")) { name = "AtriaSurface" };
            _signMaterial = new Material(text != null ? text : _font.material.shader) { name = "AtriaSignText", mainTexture = _font.material.mainTexture };
            Font.textureRebuilt += OnFontTextureRebuilt;
        }

        private void OnFontTextureRebuilt(Font font)
        {
            if (font == _font && _signMaterial != null) _signMaterial.mainTexture = font.material.mainTexture;
        }

        private void CreateCamera()
        {
            Camera existing = Camera.main;
            if (existing != null) existing.gameObject.SetActive(false); // the default scene camera is not used

            var go = new GameObject("RouteCamera");
            go.transform.SetParent(transform, false);
            go.tag = "MainCamera";
            _camera = go.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = SkyColor;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 250f;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.useOcclusionCulling = false;
            _camera.cullingMask = 0; // nothing rendered until the walkthrough opens
            _cameraController = go.AddComponent<RouteCameraController>();
            _cameraController.EyeHeight = EyeHeight;
            _cameraController.HorizontalFov = HorizontalFov;
            _cameraController.PitchDegrees = PitchDegrees;
        }

        private void WireUi()
        {
            _ui.QueryChanged += OnQueryChanged;
            _ui.RoomChosen += room => { _selectedRoom = room; UpdateRoute(); };
            _ui.EntranceChosen += index => { _selectedEntrance = index; _ui.SetSelectedEntrance(index); UpdateRoute(); };
            _ui.StartPressed += EnterWalkthrough;
            _ui.BackPressed += ExitWalkthrough;
            _ui.ResetPressed += () => { SetAutoplay(false); SetProgress(0f); };
            _ui.AutoplayPressed += () =>
            {
                if (!_autoplay && _cameraController.Progress >= 1f) SetProgress(0f);
                SetAutoplay(!_autoplay);
            };
            _ui.ScrubbedByUser += value => { SetAutoplay(false); SetProgress(value); };
        }

        private void OnQueryChanged(string query)
        {
            if (_search == null) return;
            List<RoomMatch> matches = _search.Search(query);
            string message = null;
            if (matches.Count == 0)
                message = string.IsNullOrWhiteSpace(query)
                    ? "No rooms are in the building data yet."
                    : $"No room matches “{query.Trim()}”.\nCheck the number, or the room may not be mapped yet.";
            _ui.SetResults(matches, message);

            // Typing a complete, known room number selects it without an extra tap.
            RoomData exact = _search.FindExact(query);
            if (exact != null && exact != _selectedRoom)
            {
                _selectedRoom = exact;
                UpdateRoute();
            }
        }

        private void UpdateRoute()
        {
            _route = null;
            _path = null;
            _ui.SetSelectedRoom(_selectedRoom != null ? "Destination: " + RoomLabel(_selectedRoom) : "No room selected", _selectedRoom != null);

            if (_selectedRoom == null) { _ui.SetRouteSummary("Choose a room to see the route.", false, false); return; }
            if (_selectedEntrance < 0) { _ui.SetRouteSummary("Choose the entrance you will use.", false, false); return; }

            EntranceData entrance = _building.Entrances[_selectedEntrance];
            RouteResult route = _router.FindRoute(entrance.Id, _selectedRoom.Id, WalkingSpeedOverride);
            if (!route.Success)
            {
                _ui.SetRouteSummary(FailureText(route.Failure), true, false);
                return;
            }
            _route = route;
            _path = RoutePath.FromRoute(route, PathSettings);
            string summary = $"{RouteProgress.FormatDistance(route.DistanceMetres)}  ·  about {RouteProgress.FormatDuration(route.EstimatedSeconds)} walk";
            if (route.UsesUnverifiedData) summary += "\nEstimate based on unverified layout data.";
            _ui.SetRouteSummary(summary, false, true);
        }

        private static string FailureText(RouteFailure failure)
        {
            switch (failure)
            {
                case RouteFailure.UnknownRoom: return "That room is not in the building data.";
                case RouteFailure.UnknownEntrance: return "That entrance is not in the building data.";
                case RouteFailure.MissingNode: return "This room's location has not been mapped yet.";
                case RouteFailure.Unreachable: return "No mapped route reaches this room from that entrance yet. Try another entrance.";
                default: return "A route could not be calculated.";
            }
        }

        private static string RoomLabel(RoomData room) => string.IsNullOrEmpty(room.Name) ? room.Number : $"{room.Number} ({room.Name})";

        private void EnterWalkthrough()
        {
            if (_route == null || _path == null) return;
            _view.Doors.TryGetValue(Atria.Core.Geometry.FloorLayout.DoorKey(_route.Room.Id, _route.DoorIndex), out DoorVisual door);
            _routeVisuals.Show(_path, door);
            _ui.ShowWalkthrough();
            _ui.SetWalkthroughTitle($"To {_route.Room.Number}\nfrom {_route.Entrance.Label}");
            _camera.cullingMask = ~0;
            _cameraController.SetRoute(_path, 0f);
            SetAutoplay(false);
            SetProgress(0f);
            UpdateViewport(force: true);
        }

        private void ExitWalkthrough()
        {
            SetAutoplay(false);
            _ui.ShowSearch();
            _camera.cullingMask = 0;
            _camera.rect = new Rect(0, 0, 1, 1);
            _routeVisuals?.Clear();
        }

        private void SetProgress(float progress)
        {
            if (_route == null) return;
            _cameraController.Progress = progress;
            float p = _cameraController.Progress;
            float remaining = RouteProgress.RemainingDistance(_route.DistanceMetres, p);
            float seconds = RouteProgress.RemainingSeconds(_route.DistanceMetres, p, _route.WalkingSpeedMps);
            string progressText = p >= 1f ? $"Arrived at {_route.Room.Number}" : $"{Mathf.RoundToInt(p * 100f)}% of the way";
            string remainingText = p >= 1f
                ? $"The door is in front of you. Total {RouteProgress.FormatDistance(_route.DistanceMetres)}."
                : $"{RouteProgress.FormatDistance(remaining)} left  ·  about {RouteProgress.FormatDuration(seconds)}";
            _ui.SetProgress(p, progressText, remainingText);
        }

        private void SetAutoplay(bool on)
        {
            _autoplay = on;
            _ui.SetAutoplay(on);
        }

        private void Update()
        {
            _ui.ApplySafeArea();
            if (BackRequested())
            {
                if (_ui.IsWalkthroughVisible) ExitWalkthrough();
                else Application.Quit();
            }
            if (!_ui.IsWalkthroughVisible) return;

            UpdateViewport(force: false);
            if (_autoplay && _route != null && _path != null && _path.Length > 0f)
            {
                float speed = _route.WalkingSpeedMps * Mathf.Max(0.1f, AutoplaySpeedMultiplier);
                float next = _cameraController.Progress + speed * Time.deltaTime / _path.Length;
                SetProgress(next);
                if (next >= 1f) SetAutoplay(false);
            }
        }

        /// <summary>Restricts the 3D view to the area between the walkthrough's top bar and bottom panel.</summary>
        private void UpdateViewport(bool force)
        {
            Rect viewport = _ui.WalkthroughViewport();
            if (!force && viewport == _lastViewport) return;
            _lastViewport = viewport;
            _camera.rect = viewport;
            _cameraController.InvalidateFieldOfView();
        }

        private static bool BackRequested()
        {
#if ATRIA_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        private void OnDestroy()
        {
            Font.textureRebuilt -= OnFontTextureRebuilt;
            _routeVisuals?.Clear();
            if (_surfaceMaterial != null) Destroy(_surfaceMaterial);
            if (_signMaterial != null) Destroy(_signMaterial);
        }
    }
}
