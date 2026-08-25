using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace ReactorGame.Unity
{
    /// <summary>
    /// Presentation-only Timeline projection over the existing Phase 10
    /// adapter events. It owns a bounded visual history, not simulation state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Phase10TimelineView : MonoBehaviour
    {
        public const int DefaultSnapshotCapacity = 24;
        public const int DefaultEventCapacity = 8;

        private readonly List<Phase8UnityPresentationSnapshotV1> _snapshots =
            new List<Phase8UnityPresentationSnapshotV1>();
        private readonly List<string> _events = new List<string>();
        private readonly List<GameObject> _plotBars = new List<GameObject>();

        private Phase8UnityRuntimeAdapter _runtimeAdapter;
        private RectTransform _timelineRoot;
        private RectTransform _powerPlotRoot;
        private RectTransform _tiltPlotRoot;
        private RectTransform _marginPlotRoot;
        private RectTransform _eventRoot;
        private Text _statusText;
        private Text _summaryText;
        private Text _legendText;
        private bool _isBuilt;

        public bool IsBuilt
        {
            get { return _isBuilt; }
        }

        public bool IsBound
        {
            get { return _runtimeAdapter != null; }
        }

        public int SnapshotCount
        {
            get { return _snapshots.Count; }
        }

        public int EventCount
        {
            get { return _events.Count; }
        }

        public int PlotBarCount
        {
            get { return _plotBars.Count; }
        }

        public string StatusText
        {
            get { return GetText(_statusText); }
        }

        public string SummaryText
        {
            get { return GetText(_summaryText); }
        }

        public string LegendText
        {
            get { return GetText(_legendText); }
        }

        public string EventLogText
        {
            get { return string.Join("\n", _events.ToArray()); }
        }

        public void Bind(Phase8UnityRuntimeAdapter runtimeAdapter)
        {
            if (runtimeAdapter == null)
            {
                throw new ArgumentNullException(nameof(runtimeAdapter));
            }

            Phase8UnityPresentationSnapshotV1 snapshot = runtimeAdapter.Snapshot;
            if (snapshot == null)
            {
                throw new InvalidOperationException(
                    "The Timeline requires a bound adapter with a snapshot.");
            }

            EnsureVisuals();
            Unsubscribe();
            _runtimeAdapter = runtimeAdapter;
            _snapshots.Clear();
            _events.Clear();
            _runtimeAdapter.SnapshotChanged += HandleSnapshotChanged;
            _runtimeAdapter.CommandCompleted += HandleCommandCompleted;
            RenderEvents();
            AddSnapshot(snapshot);
        }

        public void Unbind()
        {
            Unsubscribe();
            _runtimeAdapter = null;
            _snapshots.Clear();
            _events.Clear();
            ClearPresentation();
        }

        private void Awake()
        {
            EnsureVisuals();
            ClearPresentation();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void HandleSnapshotChanged(
            Phase8UnityPresentationSnapshotV1 snapshot)
        {
            if (snapshot != null)
            {
                AddSnapshot(snapshot);
            }
        }

        private void HandleCommandCompleted(Phase8UnityCommandResultV1 result)
        {
            if (result == null)
            {
                return;
            }

            string disposition = result.Accepted ? "accepted" : "rejected";
            string diagnostic = string.IsNullOrWhiteSpace(result.DiagnosticCode)
                ? string.Empty
                : " [" + result.DiagnosticCode + "]";
            string message = string.IsNullOrWhiteSpace(result.DiagnosticMessage)
                ? string.Empty
                : ": " + result.DiagnosticMessage;
            AddEvent(
                "#" + result.Sequence.ToString(CultureInfo.InvariantCulture) +
                " " + result.Kind + " " + disposition + diagnostic + message);
        }

        private void AddSnapshot(Phase8UnityPresentationSnapshotV1 snapshot)
        {
            if (_snapshots.Count >= DefaultSnapshotCapacity)
            {
                _snapshots.RemoveAt(0);
            }

            _snapshots.Add(snapshot);
            RenderSnapshot(snapshot);
            RenderPlot();
        }

        private void AddEvent(string eventText)
        {
            if (_events.Count >= DefaultEventCapacity)
            {
                _events.RemoveAt(0);
            }

            _events.Add(eventText);
            RenderEvents();
        }

        private void RenderSnapshot(Phase8UnityPresentationSnapshotV1 snapshot)
        {
            string runState = snapshot.IsPaused ? "paused" : "running";
            _statusText.text = "Status: bound | " + runState +
                " | outcome: " + snapshot.OutcomeId;
            _summaryText.text =
                "Samples: " + SnapshotCount.ToString(CultureInfo.InvariantCulture) +
                "/" + DefaultSnapshotCapacity.ToString(CultureInfo.InvariantCulture) +
                " | Simulation: " + Format(snapshot.SimulationTimeSeconds) +
                " s | Wall: " + Format(snapshot.WallElapsedSeconds) + " s";
        }

        private void RenderPlot()
        {
            ClearPlotBars();
            if (_snapshots.Count == 0)
            {
                return;
            }

            float sampleWidth = 1.0f / _snapshots.Count;
            for (int index = 0; index < _snapshots.Count; index++)
            {
                Phase8UnityPresentationSnapshotV1 snapshot = _snapshots[index];
                float x = index * sampleWidth;
                CreatePlotBar(
                    _powerPlotRoot,
                    x,
                    sampleWidth,
                    DisplayFraction(snapshot.NormalizedPowerFraction),
                    new Color(0.18f, 0.73f, 0.98f, 0.90f));
                CreatePlotBar(
                    _tiltPlotRoot,
                    x,
                    sampleWidth,
                    DisplayFraction(snapshot.AbsoluteTiltFraction),
                    new Color(0.98f, 0.66f, 0.24f, 0.90f));
                CreatePlotBar(
                    _marginPlotRoot,
                    x,
                    sampleWidth,
                    DisplayFraction(snapshot.ControlMarginFraction),
                    new Color(0.36f, 0.88f, 0.50f, 0.90f));
            }
        }

        private void RenderEvents()
        {
            if (_eventRoot == null)
            {
                return;
            }

            List<GameObject> children = new List<GameObject>();
            for (int index = 0; index < _eventRoot.childCount; index++)
            {
                children.Add(_eventRoot.GetChild(index).gameObject);
            }

            for (int index = 0; index < children.Count; index++)
            {
                DestroyVisual(children[index]);
            }

            if (_events.Count == 0)
            {
                AddEventLabel("No command results observed.", true);
                return;
            }

            for (int index = 0; index < _events.Count; index++)
            {
                AddEventLabel(_events[index], false);
            }
        }

        private void EnsureVisuals()
        {
            if (_isBuilt)
            {
                return;
            }

            Phase10ShellView shell = GetComponent<Phase10ShellView>();
            if (shell == null)
            {
                throw new InvalidOperationException(
                    "The Timeline must be attached to a Phase10ShellView.");
            }

            shell.BuildVisualShell();
            if (!shell.TryGetPageRoot(
                    Phase10ShellPageV1.Timeline,
                    out RectTransform pageRoot))
            {
                throw new InvalidOperationException(
                    "The Phase 10 shell has no Timeline page root.");
            }

            _timelineRoot = CreateRect("TimelineData", pageRoot);
            SetAnchors(_timelineRoot, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f));
            SetOffsets(_timelineRoot, 0.0f, 0.0f, 0.0f, 0.0f);
            Image cardImage = _timelineRoot.gameObject.AddComponent<Image>();
            cardImage.color = new Color(0.045f, 0.035f, 0.085f, 0.96f);
            cardImage.raycastTarget = false;

            VerticalLayoutGroup layout = _timelineRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6.0f;
            layout.padding = new RectOffset(24, 24, 18, 18);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            AddValueLabel(_timelineRoot, "TimelineHeading", "Trend timeline", 26, 38);
            _statusText = AddValueLabel(_timelineRoot, "TimelineStatus", "Status: waiting for runtime binding", 18, 30);
            _summaryText = AddValueLabel(_timelineRoot, "TimelineSummary", "Samples: 0/24 | no observed history", 18, 30);
            _legendText = AddValueLabel(
                _timelineRoot,
                "TimelineLegend",
                "Power / Tilt / Margin trends are presentation-only observations.",
                16,
                28);

            RectTransform plot = CreateRect("TimelinePlot", _timelineRoot);
            LayoutElement plotLayout = plot.gameObject.AddComponent<LayoutElement>();
            plotLayout.minHeight = 270.0f;
            plotLayout.preferredHeight = 270.0f;
            Image plotImage = plot.gameObject.AddComponent<Image>();
            plotImage.color = new Color(0.015f, 0.025f, 0.050f, 1.0f);
            plotImage.raycastTarget = false;

            _powerPlotRoot = CreatePlotTrack(
                plot,
                "PowerTrend",
                new Vector2(0.03f, 0.68f),
                new Vector2(0.97f, 0.96f),
                new Color(0.06f, 0.13f, 0.20f, 1.0f));
            _tiltPlotRoot = CreatePlotTrack(
                plot,
                "TiltTrend",
                new Vector2(0.03f, 0.37f),
                new Vector2(0.97f, 0.65f),
                new Color(0.20f, 0.12f, 0.06f, 1.0f));
            _marginPlotRoot = CreatePlotTrack(
                plot,
                "MarginTrend",
                new Vector2(0.03f, 0.06f),
                new Vector2(0.97f, 0.34f),
                new Color(0.06f, 0.18f, 0.10f, 1.0f));

            AddValueLabel(_timelineRoot, "TimelineEventHeading", "Event log", 20, 30);
            _eventRoot = CreateRect("TimelineEvents", _timelineRoot);
            LayoutElement eventLayout = _eventRoot.gameObject.AddComponent<LayoutElement>();
            eventLayout.minHeight = 190.0f;
            eventLayout.preferredHeight = 190.0f;
            VerticalLayoutGroup eventGroup = _eventRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            eventGroup.spacing = 2.0f;
            eventGroup.childAlignment = TextAnchor.UpperLeft;
            eventGroup.childControlWidth = true;
            eventGroup.childControlHeight = true;
            eventGroup.childForceExpandWidth = true;
            eventGroup.childForceExpandHeight = false;

            _isBuilt = true;
            RenderEvents();
        }

        private void ClearPresentation()
        {
            if (!_isBuilt)
            {
                return;
            }

            _statusText.text = "Status: waiting for runtime binding";
            _summaryText.text = "Samples: 0/" +
                DefaultSnapshotCapacity.ToString(CultureInfo.InvariantCulture) +
                " | no observed history";
            _legendText.text =
                "Power / Tilt / Margin trends are presentation-only observations.";
            RenderPlot();
            RenderEvents();
        }

        private void Unsubscribe()
        {
            if (_runtimeAdapter != null)
            {
                _runtimeAdapter.SnapshotChanged -= HandleSnapshotChanged;
                _runtimeAdapter.CommandCompleted -= HandleCommandCompleted;
            }
        }

        private void CreatePlotBar(
            RectTransform parent,
            float x,
            float width,
            float value,
            Color color)
        {
            RectTransform bar = CreateRect("Sample", parent);
            float boundedWidth = Mathf.Max(width * 0.82f, 0.002f);
            bar.anchorMin = new Vector2(x + width * 0.09f, 0.0f);
            bar.anchorMax = new Vector2(
                Mathf.Min(1.0f, x + width * 0.09f + boundedWidth),
                Mathf.Lerp(0.04f, 0.96f, value));
            bar.offsetMin = Vector2.zero;
            bar.offsetMax = Vector2.zero;

            Image image = bar.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            _plotBars.Add(bar.gameObject);
        }

        private void AddEventLabel(string eventText, bool muted)
        {
            Text label = AddValueLabel(_eventRoot, "Event", eventText, 16, 24);
            label.color = muted
                ? new Color(0.62f, 0.68f, 0.76f, 1.0f)
                : new Color(0.88f, 0.91f, 0.98f, 1.0f);
        }

        private static RectTransform CreatePlotTrack(
            RectTransform parent,
            string name,
            Vector2 minimum,
            Vector2 maximum,
            Color color)
        {
            RectTransform track = CreateRect(name, parent);
            SetAnchors(track, minimum, maximum);
            SetOffsets(track, 0.0f, 0.0f, 0.0f, 0.0f);
            Image image = track.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return track;
        }

        private static Text AddValueLabel(
            RectTransform parent,
            string name,
            string textValue,
            int fontSize,
            float height)
        {
            RectTransform labelRoot = CreateRect(name, parent);
            LayoutElement layout = labelRoot.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;

            Text label = labelRoot.gameObject.AddComponent<Text>();
            label.text = textValue;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.color = new Color(0.90f, 0.94f, 1.0f, 1.0f);
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
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

        private void ClearPlotBars()
        {
            for (int index = 0; index < _plotBars.Count; index++)
            {
                if (_plotBars[index] != null)
                {
                    DestroyVisual(_plotBars[index]);
                }
            }

            _plotBars.Clear();
        }

        private static void DestroyVisual(GameObject visual)
        {
            if (visual == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(visual);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(visual);
            }
        }

        private static string GetText(Text text)
        {
            return text == null ? string.Empty : text.text;
        }

        private static float DisplayFraction(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return 0.0f;
            }

            return Mathf.Clamp01((float)value);
        }

        private static string Format(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
