using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace ReactorGame.Unity
{
    /// <summary>
    /// Read-only Dashboard projection of the approved P10-T01 presentation
    /// snapshot. Binding is explicit so this component never creates or owns
    /// a runtime port.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Phase10DashboardView : MonoBehaviour
    {
        private Phase8UnityRuntimeAdapter _runtimeAdapter;
        private RectTransform _dashboardRoot;
        private Text _scenarioText;
        private Text _playbackText;
        private Text _pacingText;
        private Text _powerText;
        private Text _resourcesText;
        private Text _scoreText;
        private Text _outcomeText;
        private bool _isBuilt;

        public bool IsBuilt
        {
            get { return _isBuilt; }
        }

        public bool IsBound
        {
            get { return _runtimeAdapter != null; }
        }

        public Phase8UnityPresentationSnapshotV1 Snapshot { get; private set; }

        public string ScenarioText
        {
            get { return GetText(_scenarioText); }
        }

        public string PlaybackText
        {
            get { return GetText(_playbackText); }
        }

        public string PacingText
        {
            get { return GetText(_pacingText); }
        }

        public string PowerText
        {
            get { return GetText(_powerText); }
        }

        public string ResourcesText
        {
            get { return GetText(_resourcesText); }
        }

        public string ScoreText
        {
            get { return GetText(_scoreText); }
        }

        public string OutcomeText
        {
            get { return GetText(_outcomeText); }
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
                    "The Dashboard requires a bound adapter with a snapshot.");
            }

            EnsureVisuals();
            Unsubscribe();
            _runtimeAdapter = runtimeAdapter;
            _runtimeAdapter.SnapshotChanged += HandleSnapshotChanged;
            Render(snapshot);
        }

        public void Unbind()
        {
            Unsubscribe();
            _runtimeAdapter = null;
            Snapshot = null;
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
                Render(snapshot);
            }
        }

        private void Render(Phase8UnityPresentationSnapshotV1 snapshot)
        {
            Snapshot = snapshot;
            _scenarioText.text = "Scenario: " + snapshot.ScenarioId +
                " | Difficulty: " + snapshot.DifficultyId;
            _playbackText.text = "Playback: " + snapshot.PlaybackModeId +
                " | State: " + (snapshot.IsPaused ? "Paused" : "Running");
            _pacingText.text = "Pacing: x" + Format(snapshot.AccelerationFactor) +
                " | Control tick: " + Format(snapshot.WallControlTickMilliseconds) + " ms" +
                " | Simulation: " + Format(snapshot.SimulationTimeSeconds) + " s" +
                " | Wall: " + Format(snapshot.WallElapsedSeconds) + " s";
            _powerText.text = "Power: " + FormatPercent(snapshot.NormalizedPowerFraction) +
                " | Tilt: " + FormatPercent(snapshot.AbsoluteTiltFraction) +
                " | Control margin: " + FormatPercent(snapshot.ControlMarginFraction);
            _resourcesText.text = "Device: " + FormatPercent(snapshot.DeviceAvailableFraction) +
                " | Refuel requests: " + Format(snapshot.RefuelRequestsRemaining) +
                " | Pending actions: " + Format(snapshot.PendingActionCount) +
                " | Scripted events: " + Format(snapshot.ProcessedScriptedEventCount);
            _scoreText.text = "Score: " + Format(snapshot.ScoreTotal) +
                " | Turn summaries: " + Format(snapshot.TurnSummaryCount);
            _outcomeText.text = "Outcome: " + snapshot.OutcomeId;
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
                    "The Dashboard must be attached to a Phase10ShellView.");
            }

            shell.BuildVisualShell();
            if (!shell.TryGetPageRoot(
                    Phase10ShellPageV1.Dashboard,
                    out RectTransform pageRoot))
            {
                throw new InvalidOperationException(
                    "The Phase 10 shell has no Dashboard page root.");
            }

            _dashboardRoot = CreateRect("DashboardData", pageRoot);
            SetAnchors(_dashboardRoot, new Vector2(0.06f, 0.10f), new Vector2(0.94f, 0.90f));
            SetOffsets(_dashboardRoot, 0.0f, 0.0f, 0.0f, 0.0f);

            Image cardImage = _dashboardRoot.gameObject.AddComponent<Image>();
            cardImage.color = new Color(0.025f, 0.055f, 0.090f, 0.94f);
            cardImage.raycastTarget = false;

            VerticalLayoutGroup layout = _dashboardRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8.0f;
            layout.padding = new RectOffset(28, 28, 24, 24);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            _scenarioText = AddValueLabel(_dashboardRoot, "DashboardScenario");
            _playbackText = AddValueLabel(_dashboardRoot, "DashboardPlayback");
            _pacingText = AddValueLabel(_dashboardRoot, "DashboardPacing");
            _powerText = AddValueLabel(_dashboardRoot, "DashboardPower");
            _resourcesText = AddValueLabel(_dashboardRoot, "DashboardResources");
            _scoreText = AddValueLabel(_dashboardRoot, "DashboardScore");
            _outcomeText = AddValueLabel(_dashboardRoot, "DashboardOutcome");
            _isBuilt = true;
        }

        private void Unsubscribe()
        {
            if (_runtimeAdapter != null)
            {
                _runtimeAdapter.SnapshotChanged -= HandleSnapshotChanged;
            }
        }

        private void ClearPresentation()
        {
            if (!_isBuilt)
            {
                return;
            }

            _scenarioText.text = "Scenario: waiting for runtime binding";
            _playbackText.text = "Playback: unavailable";
            _pacingText.text = "Pacing: unavailable";
            _powerText.text = "Power: unavailable";
            _resourcesText.text = "Resources: unavailable";
            _scoreText.text = "Score: unavailable";
            _outcomeText.text = "Outcome: unavailable";
        }

        private static Text AddValueLabel(RectTransform parent, string name)
        {
            RectTransform labelRoot = CreateRect(name, parent);
            LayoutElement layout = labelRoot.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 34.0f;
            layout.preferredHeight = 34.0f;

            Text label = labelRoot.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 22;
            label.color = new Color(0.90f, 0.95f, 1.0f, 1.0f);
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

        private static string GetText(Text text)
        {
            return text == null ? string.Empty : text.text;
        }

        private static string Format(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Format(uint value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatPercent(double value)
        {
            return value.ToString("0.0%", CultureInfo.InvariantCulture);
        }
    }
}
