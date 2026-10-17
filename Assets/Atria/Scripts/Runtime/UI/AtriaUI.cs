using System;
using System.Collections.Generic;
using Atria.Core.Data;
using Atria.Core.Search;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Atria.App.UI
{
    /// <summary>
    /// Builds and owns the two screens (search/plan and walkthrough). Pure view: it raises events
    /// and displays what <see cref="AtriaApp"/> tells it; it contains no routing logic.
    /// </summary>
    public sealed class AtriaUI
    {
        public event Action<string> QueryChanged;
        public event Action<RoomData> RoomChosen;
        public event Action<int> EntranceChosen;
        public event Action StartPressed;
        public event Action BackPressed;
        public event Action ResetPressed;
        public event Action AutoplayPressed;
        /// <summary>Raised only for user drags/taps on the scrubber, never for programmatic updates.</summary>
        public event Action<float> ScrubbedByUser;

        private const int MaxResultRows = 20;

        private Canvas _canvas;
        private RectTransform _safeArea;
        private Rect _appliedSafeArea;
        private GameObject _searchScreen, _walkScreen;
        private InputField _query;
        private RectTransform _resultsContent;
        private readonly List<(Button button, Text label)> _resultRows = new List<(Button, Text)>();
        private readonly List<RoomData> _resultRooms = new List<RoomData>();
        private Text _resultsMessage, _selectedRoom, _routeSummary;
        private readonly List<(Button button, Image image)> _entranceButtons = new List<(Button, Image)>();
        private Button _startButton;
        private Text _walkTitle, _progressText, _remainingText, _autoplayLabel;
        private Slider _slider;
        private RectTransform _walkTopBar, _walkBottomPanel;

        public bool IsWalkthroughVisible => _walkScreen != null && _walkScreen.activeSelf;

        public void Build(Transform parent, Font font, string warningBanner, IList<EntranceData> entrances)
        {
            UiFactory.Font = font;
            var canvasGo = new GameObject("UI", typeof(RectTransform));
            canvasGo.transform.SetParent(parent, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.pixelPerfect = false;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _safeArea = UiFactory.Stretch(UiFactory.Rect("SafeArea", canvasGo.transform));
            ApplySafeArea();

            BuildSearchScreen(warningBanner, entrances);
            BuildWalkthroughScreen(warningBanner);
            ShowSearch();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            // ATRIA_INPUT_SYSTEM is set by Atria.App.asmdef only when the Input System package is
            // installed, so this compiles whichever input backend the project uses.
#if ATRIA_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        // ---------------------------------------------------------------- search screen

        private void BuildSearchScreen(string warningBanner, IList<EntranceData> entrances)
        {
            Image screen = UiFactory.Panel("SearchScreen", _safeArea, UiFactory.Background);
            UiFactory.Stretch(screen.rectTransform);
            _searchScreen = screen.gameObject;
            UiFactory.Vertical(screen, 48, 22);

            UiFactory.Size(UiFactory.Label("Title", screen.transform, "Atria", 76, UiFactory.TextPrimary, FontStyle.Bold), 96);
            UiFactory.Size(UiFactory.Label("Subtitle", screen.transform, "Pearl Research Park · indoor directions · works offline", 30, UiFactory.TextSecondary), 44);

            if (!string.IsNullOrEmpty(warningBanner)) AddBanner(screen.transform, warningBanner, 28);

            UiFactory.Size(UiFactory.Label("DestLabel", screen.transform, "1  Where are you going?", 36, UiFactory.TextPrimary, FontStyle.Bold), 52);
            _query = UiFactory.Input("Query", screen.transform, "Room number", 40);
            UiFactory.Size(_query, 120);
            _query.onValueChanged.AddListener(q => QueryChanged?.Invoke(q));

            // Results list with an overlaid message for empty / unknown states.
            Image resultsBox = UiFactory.Panel("Results", screen.transform, UiFactory.Surface);
            UiFactory.Size(resultsBox, 360, 1);
            var scroll = resultsBox.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = UiFactory.Stretch(UiFactory.Rect("Viewport", resultsBox.transform), 8, 8, 8, 8);
            viewport.gameObject.AddComponent<RectMask2D>();
            _resultsContent = UiFactory.Rect("Content", viewport);
            _resultsContent.anchorMin = new Vector2(0, 1);
            _resultsContent.anchorMax = new Vector2(1, 1);
            _resultsContent.pivot = new Vector2(0.5f, 1);
            _resultsContent.sizeDelta = Vector2.zero;
            UiFactory.Vertical(_resultsContent, 0, 6);
            var fitter = _resultsContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = _resultsContent;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            for (int i = 0; i < MaxResultRows; i++)
            {
                int index = i;
                Button row = UiFactory.Button("Result" + i, _resultsContent, "", 34, UiFactory.Background, UiFactory.TextPrimary, out Text label);
                label.alignment = TextAnchor.MiddleLeft;
                label.fontStyle = FontStyle.Normal;
                UiFactory.Size(row, 96);
                row.onClick.AddListener(() => { if (index < _resultRooms.Count) RoomChosen?.Invoke(_resultRooms[index]); });
                row.gameObject.SetActive(false);
                _resultRows.Add((row, label));
            }
            _resultsMessage = UiFactory.Label("Message", resultsBox.transform, "", 32, UiFactory.TextSecondary, FontStyle.Normal, TextAnchor.MiddleCenter);
            UiFactory.Stretch(_resultsMessage.rectTransform, 32, 32, 16, 16);

            _selectedRoom = UiFactory.Label("Selected", screen.transform, "No room selected", 34, UiFactory.TextSecondary);
            UiFactory.Size(_selectedRoom, 50);

            UiFactory.Size(UiFactory.Label("EntranceLabel", screen.transform, "2  Which entrance are you at?", 36, UiFactory.TextPrimary, FontStyle.Bold), 52);
            for (int i = 0; i < entrances.Count; i++)
            {
                int index = i;
                Button b = UiFactory.Button("Entrance" + i, screen.transform, entrances[i].Label, 34, UiFactory.Surface, UiFactory.TextPrimary, out _);
                UiFactory.Size(b, 104);
                b.onClick.AddListener(() => EntranceChosen?.Invoke(index));
                _entranceButtons.Add((b, (Image)b.targetGraphic));
            }

            _routeSummary = UiFactory.Label("RouteSummary", screen.transform, "", 34, UiFactory.TextPrimary);
            UiFactory.Size(_routeSummary, 96);

            _startButton = UiFactory.Button("Start", screen.transform, "Start walkthrough", 44, UiFactory.Accent, UiFactory.AccentText, out _);
            UiFactory.Size(_startButton, 140);
            _startButton.onClick.AddListener(() => StartPressed?.Invoke());
        }

        private static void AddBanner(Transform parent, string text, int fontSize)
        {
            Image banner = UiFactory.Panel("SampleBanner", parent, UiFactory.Warning);
            UiFactory.Vertical(banner, 18, 0);
            Text t = UiFactory.Label("Text", banner.transform, text, fontSize, UiFactory.TextPrimary, FontStyle.Bold);
            t.alignment = TextAnchor.MiddleCenter;
        }

        // ---------------------------------------------------------------- walkthrough screen

        private void BuildWalkthroughScreen(string warningBanner)
        {
            RectTransform screen = UiFactory.Stretch(UiFactory.Rect("WalkthroughScreen", _safeArea));
            _walkScreen = screen.gameObject;

            Image top = UiFactory.Panel("TopBar", screen, UiFactory.Overlay);
            _walkTopBar = top.rectTransform;
            _walkTopBar.anchorMin = new Vector2(0, 1);
            _walkTopBar.anchorMax = new Vector2(1, 1);
            _walkTopBar.pivot = new Vector2(0.5f, 1);
            _walkTopBar.sizeDelta = new Vector2(0, string.IsNullOrEmpty(warningBanner) ? 150 : 200);
            Button back = UiFactory.Button("Back", top.transform, "‹ Search", 34, UiFactory.Background, UiFactory.Accent, out _);
            RectTransform backRt = back.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(0, 1);
            backRt.anchorMax = new Vector2(0, 1);
            backRt.pivot = new Vector2(0, 1);
            backRt.anchoredPosition = new Vector2(24, -24);
            backRt.sizeDelta = new Vector2(230, 102);
            back.onClick.AddListener(() => BackPressed?.Invoke());
            _walkTitle = UiFactory.Label("Title", top.transform, "", 36, UiFactory.TextPrimary, FontStyle.Bold);
            RectTransform titleRt = _walkTitle.rectTransform;
            titleRt.anchorMin = new Vector2(0, 1);
            titleRt.anchorMax = new Vector2(1, 1);
            titleRt.pivot = new Vector2(0, 1);
            titleRt.offsetMin = new Vector2(278, -126);
            titleRt.offsetMax = new Vector2(-24, -24);
            if (!string.IsNullOrEmpty(warningBanner))
            {
                Text tag = UiFactory.Label("SampleTag", top.transform, "SAMPLE LAYOUT – not the real building", 26, new Color32(150, 90, 0, 255), FontStyle.Bold, TextAnchor.MiddleCenter);
                tag.rectTransform.anchorMin = new Vector2(0, 0);
                tag.rectTransform.anchorMax = new Vector2(1, 0);
                tag.rectTransform.pivot = new Vector2(0.5f, 0);
                tag.rectTransform.sizeDelta = new Vector2(0, 54);
            }

            Image bottom = UiFactory.Panel("BottomPanel", screen, UiFactory.Overlay);
            _walkBottomPanel = bottom.rectTransform;
            _walkBottomPanel.anchorMin = new Vector2(0, 0);
            _walkBottomPanel.anchorMax = new Vector2(1, 0);
            _walkBottomPanel.pivot = new Vector2(0.5f, 0);
            _walkBottomPanel.sizeDelta = new Vector2(0, 470);
            UiFactory.Vertical(bottom, 36, 14);

            _progressText = UiFactory.Label("Progress", bottom.transform, "", 34, UiFactory.TextPrimary, FontStyle.Bold);
            UiFactory.Size(_progressText, 48);
            _remainingText = UiFactory.Label("Remaining", bottom.transform, "", 32, UiFactory.TextSecondary);
            UiFactory.Size(_remainingText, 46);

            _slider = UiFactory.Slider("Scrubber", bottom.transform, 96);
            UiFactory.Size(_slider, 120);
            _slider.onValueChanged.AddListener(v => ScrubbedByUser?.Invoke(v));

            Image hint = UiFactory.Panel("HintSpacer", bottom.transform, Color.clear);
            hint.raycastTarget = false;
            Text hintText = UiFactory.Label("Hint", hint.transform, "Drag to move along the route. This preview does not track where you are.", 24, UiFactory.TextSecondary, FontStyle.Italic, TextAnchor.MiddleCenter);
            UiFactory.Stretch(hintText.rectTransform);
            UiFactory.Size(hint, 36);

            RectTransform row = UiFactory.Rect("Buttons", bottom.transform);
            UiFactory.Horizontal(row, 0, 18);
            UiFactory.Size(row, 108);
            Button reset = UiFactory.Button("Reset", row, "Reset", 32, UiFactory.Background, UiFactory.TextPrimary, out _);
            reset.onClick.AddListener(() => ResetPressed?.Invoke());
            Button play = UiFactory.Button("Autoplay", row, "Play", 32, UiFactory.Background, UiFactory.TextPrimary, out _autoplayLabel);
            play.onClick.AddListener(() => AutoplayPressed?.Invoke());
            Button change = UiFactory.Button("NewDestination", row, "New room", 32, UiFactory.Background, UiFactory.TextPrimary, out _);
            change.onClick.AddListener(() => BackPressed?.Invoke());
        }

        // ---------------------------------------------------------------- state updates

        public void ShowSearch()
        {
            _searchScreen.SetActive(true);
            _walkScreen.SetActive(false);
        }

        public void ShowWalkthrough()
        {
            _searchScreen.SetActive(false);
            _walkScreen.SetActive(true);
        }

        public void SetResults(List<RoomMatch> matches, string message)
        {
            _resultRooms.Clear();
            for (int i = 0; i < _resultRows.Count; i++)
            {
                bool active = i < matches.Count;
                _resultRows[i].button.gameObject.SetActive(active);
                if (!active) continue;
                RoomData room = matches[i].Room;
                _resultRooms.Add(room);
                _resultRows[i].label.text = string.IsNullOrEmpty(room.Name) ? room.Number : room.Number + "   ·   " + room.Name;
            }
            _resultsMessage.text = message ?? string.Empty;
            _resultsMessage.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        public void SetSelectedRoom(string text, bool hasSelection)
        {
            _selectedRoom.text = text;
            _selectedRoom.color = hasSelection ? UiFactory.TextPrimary : UiFactory.TextSecondary;
        }

        public void SetSelectedEntrance(int index)
        {
            for (int i = 0; i < _entranceButtons.Count; i++)
                _entranceButtons[i].image.color = i == index ? UiFactory.Selected : UiFactory.Surface;
        }

        public void SetRouteSummary(string text, bool isError, bool canStart)
        {
            _routeSummary.text = text;
            _routeSummary.color = isError ? UiFactory.Error : UiFactory.TextPrimary;
            _startButton.interactable = canStart;
        }

        public void SetWalkthroughTitle(string text) => _walkTitle.text = text;

        public void SetProgress(float progress, string progressText, string remainingText)
        {
            _slider.SetValueWithoutNotify(progress);
            _progressText.text = progressText;
            _remainingText.text = remainingText;
        }

        public void SetAutoplay(bool playing) => _autoplayLabel.text = playing ? "Pause" : "Play";

        /// <summary>Normalised screen rect left visible between the walkthrough's top bar and bottom panel.</summary>
        public Rect WalkthroughViewport()
        {
            ApplySafeArea();
            float scale = _canvas.scaleFactor;
            float top = (_walkTopBar.rect.height * scale + (Screen.height - _appliedSafeArea.yMax)) / Screen.height;
            float bottom = (_walkBottomPanel.rect.height * scale + _appliedSafeArea.yMin) / Screen.height;
            float height = Mathf.Clamp01(1f - top - bottom);
            return new Rect(0f, bottom, 1f, Mathf.Max(0.1f, height));
        }

        /// <summary>Keeps controls clear of notches and gesture bars.</summary>
        public void ApplySafeArea()
        {
            Rect safe = Screen.safeArea;
            if (safe == _appliedSafeArea || Screen.width <= 0 || Screen.height <= 0) return;
            _appliedSafeArea = safe;
            _safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            _safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            _safeArea.offsetMin = Vector2.zero;
            _safeArea.offsetMax = Vector2.zero;
        }
    }
}
