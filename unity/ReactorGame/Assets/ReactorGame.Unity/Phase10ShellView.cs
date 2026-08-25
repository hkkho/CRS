using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ReactorGame.Unity
{
    public enum Phase10ShellPageV1 : byte
    {
        Dashboard = 0,
        CoreMap = 1,
        Timeline = 2,
        Controls = 3
    }

    /// <summary>
    /// Visual-only Phase 10 shell. It owns page visibility and layout only;
    /// simulation state and commands remain behind Phase8UnityRuntimeAdapter.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
    public sealed class Phase10ShellView : MonoBehaviour
    {
        public const float MinimumTouchTargetPixels = 88.0f;

        public static readonly Vector2 LandscapeReferenceResolution =
            new Vector2(1920.0f, 1080.0f);

        private readonly Dictionary<Phase10ShellPageV1, GameObject> _pages =
            new Dictionary<Phase10ShellPageV1, GameObject>();

        private readonly Dictionary<Phase10ShellPageV1, Button> _navigationButtons =
            new Dictionary<Phase10ShellPageV1, Button>();

        private RectTransform _safeAreaRoot;
        private RectTransform _pageRoot;
        private Text _pageTitle;
        private Phase10SafeAreaLayout _safeAreaLayout;
        private Phase10ShellPageV1 _activePage;
        private bool _isBuilt;

        public event Action<Phase10ShellPageV1> PageChanged;

        public bool IsBuilt
        {
            get { return _isBuilt; }
        }

        public Phase10ShellPageV1 ActivePage
        {
            get { return _activePage; }
        }

        public Phase10SafeAreaLayout SafeAreaLayout
        {
            get { return _safeAreaLayout; }
        }

        public Canvas ShellCanvas
        {
            get { return GetComponent<Canvas>(); }
        }

        public RectTransform PageRoot
        {
            get { return _pageRoot; }
        }

        public int NavigationButtonCount
        {
            get { return _navigationButtons.Count; }
        }

        private void Awake()
        {
            BuildVisualShell();
        }

        private void OnEnable()
        {
            if (_isBuilt && _safeAreaLayout != null)
            {
                _safeAreaLayout.Refresh();
            }
        }

        public void BuildVisualShell()
        {
            if (_isBuilt)
            {
                return;
            }

            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = LandscapeReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GetComponent<GraphicRaycaster>();

            RectTransform canvasRoot = GetComponent<RectTransform>();
            Stretch(canvasRoot);

            _safeAreaRoot = CreateRect("SafeArea", canvasRoot);
            Stretch(_safeAreaRoot);
            _safeAreaLayout = _safeAreaRoot.gameObject.AddComponent<Phase10SafeAreaLayout>();

            RectTransform header = CreateRect("Header", _safeAreaRoot);
            SetAnchors(header, new Vector2(0.0f, 0.88f), new Vector2(1.0f, 1.0f));
            SetOffsets(header, 0.0f, 0.0f, 0.0f, 0.0f);
            AddImage(header, new Color(0.035f, 0.055f, 0.090f, 1.0f));

            _pageTitle = AddLabel(
                header,
                "Reactor Operations",
                32,
                new Color(0.92f, 0.96f, 1.0f, 1.0f),
                TextAnchor.MiddleLeft);
            Stretch(_pageTitle.rectTransform);
            SetOffsets(_pageTitle.rectTransform, 32.0f, 0.0f, 32.0f, 0.0f);

            _pageRoot = CreateRect("Pages", _safeAreaRoot);
            SetAnchors(_pageRoot, new Vector2(0.0f, 0.12f), new Vector2(1.0f, 0.88f));
            SetOffsets(_pageRoot, 0.0f, 0.0f, 0.0f, 0.0f);

            CreatePage(
                Phase10ShellPageV1.Dashboard,
                "Dashboard",
                "Live reactor overview will appear here.",
                new Color(0.055f, 0.090f, 0.145f, 1.0f));
            CreatePage(
                Phase10ShellPageV1.CoreMap,
                "Core Map",
                "Channel and bundle inspection will appear here.",
                new Color(0.050f, 0.120f, 0.120f, 1.0f));
            CreatePage(
                Phase10ShellPageV1.Timeline,
                "Timeline",
                "Trends and event cause/effect will appear here.",
                new Color(0.105f, 0.075f, 0.145f, 1.0f));
            CreatePage(
                Phase10ShellPageV1.Controls,
                "Controls",
                "Runtime commands and confirmation panels will appear here.",
                new Color(0.145f, 0.095f, 0.055f, 1.0f));

            RectTransform navigation = CreateRect("Navigation", _safeAreaRoot);
            SetAnchors(navigation, new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.12f));
            SetOffsets(navigation, 0.0f, 0.0f, 0.0f, 0.0f);
            AddImage(navigation, new Color(0.025f, 0.040f, 0.070f, 1.0f));

            HorizontalLayoutGroup layout = navigation.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8.0f;
            layout.padding = new RectOffset(16, 16, 8, 8);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            CreateNavigationButton(navigation, Phase10ShellPageV1.Dashboard, "Dashboard");
            CreateNavigationButton(navigation, Phase10ShellPageV1.CoreMap, "Core Map");
            CreateNavigationButton(navigation, Phase10ShellPageV1.Timeline, "Timeline");
            CreateNavigationButton(navigation, Phase10ShellPageV1.Controls, "Controls");

            _isBuilt = true;
            _activePage = Phase10ShellPageV1.Dashboard;
            SetActivePage(_activePage, false);
            _safeAreaLayout.Refresh();

            if (Application.isPlaying && EventSystem.current == null)
            {
                GameObject eventSystemObject = new GameObject(
                    "EventSystem",
                    typeof(EventSystem),
                    typeof(StandaloneInputModule));
                eventSystemObject.transform.SetParent(transform.parent, false);
            }
        }

        public bool NavigateTo(Phase10ShellPageV1 page)
        {
            if (!_isBuilt || !_pages.ContainsKey(page))
            {
                return false;
            }

            if (_activePage == page)
            {
                return true;
            }

            SetActivePage(page, true);
            return true;
        }

        public bool TryGetNavigationButton(
            Phase10ShellPageV1 page,
            out Button button)
        {
            return _navigationButtons.TryGetValue(page, out button);
        }

        private void CreatePage(
            Phase10ShellPageV1 page,
            string title,
            string description,
            Color backgroundColor)
        {
            RectTransform pageRoot = CreateRect(
                "Page." + page,
                _pageRoot);
            Stretch(pageRoot);
            AddImage(pageRoot, backgroundColor);

            Text pageLabel = AddLabel(
                pageRoot,
                title + "\n\n" + description,
                28,
                new Color(0.86f, 0.92f, 0.98f, 1.0f),
                TextAnchor.MiddleCenter);
            Stretch(pageLabel.rectTransform);

            _pages.Add(page, pageRoot.gameObject);
        }

        private void CreateNavigationButton(
            RectTransform navigation,
            Phase10ShellPageV1 page,
            string label)
        {
            RectTransform buttonRoot = CreateRect(
                "Navigation." + page,
                navigation);
            Image image = AddImage(
                buttonRoot,
                new Color(0.095f, 0.145f, 0.215f, 1.0f));
            image.raycastTarget = true;

            Button button = buttonRoot.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.095f, 0.145f, 0.215f, 1.0f);
            colors.highlightedColor = new Color(0.16f, 0.29f, 0.42f, 1.0f);
            colors.pressedColor = new Color(0.23f, 0.42f, 0.54f, 1.0f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            LayoutElement layout = buttonRoot.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = MinimumTouchTargetPixels;
            layout.preferredHeight = MinimumTouchTargetPixels;

            Text text = AddLabel(
                buttonRoot,
                label,
                22,
                Color.white,
                TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);

            button.onClick.AddListener(delegate { NavigateTo(page); });
            _navigationButtons.Add(page, button);
        }

        private void SetActivePage(Phase10ShellPageV1 page, bool notify)
        {
            _activePage = page;
            foreach (KeyValuePair<Phase10ShellPageV1, GameObject> item in _pages)
            {
                item.Value.SetActive(item.Key == page);
            }

            if (_pageTitle != null)
            {
                _pageTitle.text = GetPageTitle(page);
            }

            if (notify && PageChanged != null)
            {
                PageChanged(page);
            }
        }

        private static string GetPageTitle(Phase10ShellPageV1 page)
        {
            switch (page)
            {
                case Phase10ShellPageV1.CoreMap:
                    return "Core Map";
                case Phase10ShellPageV1.Timeline:
                    return "Timeline";
                case Phase10ShellPageV1.Controls:
                    return "Controls";
                default:
                    return "Dashboard";
            }
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        private static Image AddImage(RectTransform parent, Color color)
        {
            Image image = parent.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text AddLabel(
            RectTransform parent,
            string textValue,
            int fontSize,
            Color color,
            TextAnchor alignment)
        {
            RectTransform labelRoot = CreateRect("Label", parent);
            Text label = labelRoot.gameObject.AddComponent<Text>();
            label.text = textValue;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return label;
        }

        private static void Stretch(RectTransform rect)
        {
            SetAnchors(rect, Vector2.zero, Vector2.one);
            SetOffsets(rect, 0.0f, 0.0f, 0.0f, 0.0f);
        }

        private static void SetAnchors(
            RectTransform rect,
            Vector2 minimum,
            Vector2 maximum)
        {
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
        }

        private static void SetOffsets(
            RectTransform rect,
            float left,
            float bottom,
            float right,
            float top)
        {
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }

    /// <summary>
    /// Maps a physical screen safe area to normalized Canvas anchors. It is a
    /// visual layout helper and never supplies time or simulation input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Phase10SafeAreaLayout : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private Vector2 _lastScreenSize;
        private bool _hasApplied;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled)
            {
                Refresh();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && isActiveAndEnabled)
            {
                InvalidateSafeArea();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused && isActiveAndEnabled)
            {
                InvalidateSafeArea();
            }
        }

        public void InvalidateSafeArea()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Apply(
                Screen.safeArea,
                new Vector2(Screen.width, Screen.height));
        }

        public void Apply(Rect safeArea, Vector2 screenSize)
        {
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }

            if (screenSize.x <= 0.0f || screenSize.y <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(screenSize),
                    "The screen size must be positive.");
            }

            Rect boundedSafeArea = safeArea;
            boundedSafeArea.xMin = Mathf.Clamp(boundedSafeArea.xMin, 0.0f, screenSize.x);
            boundedSafeArea.xMax = Mathf.Clamp(boundedSafeArea.xMax, 0.0f, screenSize.x);
            boundedSafeArea.yMin = Mathf.Clamp(boundedSafeArea.yMin, 0.0f, screenSize.y);
            boundedSafeArea.yMax = Mathf.Clamp(boundedSafeArea.yMax, 0.0f, screenSize.y);

            if (_hasApplied &&
                boundedSafeArea == _lastSafeArea &&
                screenSize == _lastScreenSize)
            {
                return;
            }

            _rectTransform.anchorMin = new Vector2(
                boundedSafeArea.xMin / screenSize.x,
                boundedSafeArea.yMin / screenSize.y);
            _rectTransform.anchorMax = new Vector2(
                boundedSafeArea.xMax / screenSize.x,
                boundedSafeArea.yMax / screenSize.y);
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;

            _lastSafeArea = boundedSafeArea;
            _lastScreenSize = screenSize;
            _hasApplied = true;
        }
    }
}
