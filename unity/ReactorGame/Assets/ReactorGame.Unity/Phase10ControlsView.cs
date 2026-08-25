using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace ReactorGame.Unity
{
    /// <summary>
    /// Presentation-only actions exposed by the first Phase 10 Controls page.
    /// These names do not expand the P10-T01 runtime command vocabulary.
    /// </summary>
    public enum Phase10ControlActionV1 : byte
    {
        AdvanceControlTick = 0,
        AdvanceOneSecond = 1,
        Pause = 2,
        Resume = 3,
        AuditPlayback = 4,
        AcceleratedPlayback = 5,
        ApplyPowerTarget = 6,
        ApplyTiltTarget = 7
    }

    /// <summary>
    /// Touch-oriented presenter for the approved P10-T01 command seam. It
    /// never owns runtime state, command sequences, pacing, or domain bounds.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Phase10ControlsView : MonoBehaviour
    {
        public const ulong InteractiveControlTickMilliseconds = 100;
        public const ulong InteractiveAdvanceMilliseconds = 1000;
        public const string AuditPlaybackModeId = "audit-real-time-1x";
        public const string AcceleratedPlaybackModeId = "play-accelerated-10x";

        private readonly Dictionary<Phase10ControlActionV1, Button> _buttons =
            new Dictionary<Phase10ControlActionV1, Button>();

        private Phase8UnityRuntimeAdapter _runtimeAdapter;
        private RectTransform _controlsRoot;
        private InputField _powerTargetInput;
        private InputField _tiltTargetInput;
        private Text _observedText;
        private Text _pacingText;
        private Text _statusText;
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

        public string ObservedText
        {
            get { return GetText(_observedText); }
        }

        public string PacingText
        {
            get { return GetText(_pacingText); }
        }

        public string StatusText
        {
            get { return GetText(_statusText); }
        }

        public string PowerTargetText
        {
            get { return _powerTargetInput == null ? string.Empty : _powerTargetInput.text; }
        }

        public string TiltTargetText
        {
            get { return _tiltTargetInput == null ? string.Empty : _tiltTargetInput.text; }
        }

        private void Awake()
        {
            // Keep the graphical surface present in the Bootstrap scene even
            // while the host is still wiring the approved runtime seam.
            if (GetComponent<Phase10ShellView>() != null)
            {
                EnsureVisuals();
            }
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
                    "The Controls view requires a bound adapter with a snapshot.");
            }

            EnsureVisuals();
            Unsubscribe();
            _runtimeAdapter = runtimeAdapter;
            _runtimeAdapter.SnapshotChanged += HandleSnapshotChanged;
            _runtimeAdapter.CommandCompleted += HandleCommandCompleted;
            _powerTargetInput.text = Format(snapshot.NormalizedPowerFraction);
            _tiltTargetInput.text = Format(snapshot.AbsoluteTiltFraction);
            SetControlAvailability(true);
            Render(snapshot);
            SetStatus("Controls ready; commands use explicit wall time.");
        }

        public void Unbind()
        {
            Unsubscribe();
            _runtimeAdapter = null;
            Snapshot = null;
            ClearPresentation();
            SetControlAvailability(false);
        }

        public bool TryGetActionButton(
            Phase10ControlActionV1 action,
            out Button button)
        {
            return _buttons.TryGetValue(action, out button);
        }

        public Phase8UnityCommandResultV1 AdvanceControlTick()
        {
            return Dispatch(
                delegate
                {
                    return _runtimeAdapter.AdvanceWallMilliseconds(
                        InteractiveControlTickMilliseconds);
                });
        }

        public Phase8UnityCommandResultV1 AdvanceOneSecond()
        {
            return Dispatch(
                delegate
                {
                    return _runtimeAdapter.AdvanceWallMilliseconds(
                        InteractiveAdvanceMilliseconds);
                });
        }

        public Phase8UnityCommandResultV1 Pause()
        {
            return Dispatch(delegate { return _runtimeAdapter.Pause(); });
        }

        public Phase8UnityCommandResultV1 Resume()
        {
            return Dispatch(delegate { return _runtimeAdapter.Resume(); });
        }

        public Phase8UnityCommandResultV1 UseAuditPlayback()
        {
            return Dispatch(
                delegate
                {
                    return _runtimeAdapter.SetPlaybackMode(AuditPlaybackModeId);
                });
        }

        public Phase8UnityCommandResultV1 UseAcceleratedPlayback()
        {
            return Dispatch(
                delegate
                {
                    return _runtimeAdapter.SetPlaybackMode(AcceleratedPlaybackModeId);
                });
        }

        public Phase8UnityCommandResultV1 ApplyPowerTarget()
        {
            if (!TryReadFiniteTarget(_powerTargetInput, "power", out double target))
            {
                return null;
            }

            return Dispatch(delegate { return _runtimeAdapter.QueuePowerTarget(target); });
        }

        public Phase8UnityCommandResultV1 ApplyTiltTarget()
        {
            if (!TryReadFiniteTarget(_tiltTargetInput, "tilt", out double target))
            {
                return null;
            }

            return Dispatch(delegate { return _runtimeAdapter.QueueTiltTarget(target); });
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

        private void HandleCommandCompleted(Phase8UnityCommandResultV1 result)
        {
            if (result == null)
            {
                SetStatus("Command rejected: the adapter returned no result.");
                return;
            }

            string sequence = result.Sequence.ToString(CultureInfo.InvariantCulture);
            if (result.Accepted)
            {
                SetStatus(
                    "Last command: " + result.Kind +
                    " accepted (sequence " + sequence + ").");
                return;
            }

            SetStatus(
                "Last command: " + result.Kind +
                " rejected [" + result.DiagnosticCode + "] " +
                result.DiagnosticMessage);
        }

        private void Render(Phase8UnityPresentationSnapshotV1 snapshot)
        {
            Snapshot = snapshot;
            _observedText.text =
                "Observed: Power " + FormatPercent(snapshot.NormalizedPowerFraction) +
                " | Tilt " + FormatPercent(snapshot.AbsoluteTiltFraction) +
                " | State " + (snapshot.IsPaused ? "Paused" : "Running");
            _pacingText.text =
                "Pacing: x" + Format(snapshot.AccelerationFactor) +
                " | Control tick " + Format(snapshot.WallControlTickMilliseconds) +
                " ms | Simulation " + Format(snapshot.SimulationTimeSeconds) +
                " s | Wall " + Format(snapshot.WallElapsedSeconds) + " s";
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
                    "The Controls view must be attached to a Phase10ShellView.");
            }

            shell.BuildVisualShell();
            if (!shell.TryGetPageRoot(
                    Phase10ShellPageV1.Controls,
                    out RectTransform pageRoot))
            {
                throw new InvalidOperationException(
                    "The Phase 10 shell has no Controls page root.");
            }

            _controlsRoot = CreateRect("ControlsData", pageRoot);
            SetAnchors(_controlsRoot, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.94f));
            SetOffsets(_controlsRoot, 0.0f, 0.0f, 0.0f, 0.0f);

            Image cardImage = _controlsRoot.gameObject.AddComponent<Image>();
            cardImage.color = new Color(0.025f, 0.055f, 0.090f, 0.96f);
            cardImage.raycastTarget = false;

            VerticalLayoutGroup layout = _controlsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8.0f;
            layout.padding = new RectOffset(24, 24, 20, 20);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            AddValueLabel(_controlsRoot, "ControlsHeading", "Touch controls", 26, TextAnchor.MiddleLeft, 38);
            _observedText = AddValueLabel(_controlsRoot, "ControlsObserved", "Observed: unavailable", 20, TextAnchor.MiddleLeft, 36);
            _pacingText = AddValueLabel(_controlsRoot, "ControlsPacing", "Pacing: unavailable", 20, TextAnchor.MiddleLeft, 36);
            _statusText = AddValueLabel(_controlsRoot, "ControlsStatus", "Status: waiting for runtime binding", 20, TextAnchor.MiddleLeft, 50);

            CreateTargetRow(
                _controlsRoot,
                "Power target",
                "ControlsPowerTarget",
                Phase10ControlActionV1.ApplyPowerTarget,
                "Apply power",
                delegate { ApplyPowerTarget(); });
            CreateTargetRow(
                _controlsRoot,
                "Absolute tilt target",
                "ControlsTiltTarget",
                Phase10ControlActionV1.ApplyTiltTarget,
                "Apply tilt",
                delegate { ApplyTiltTarget(); });

            RectTransform timeRow = CreateRow("ControlsTimeRow", _controlsRoot);
            AddActionButton(
                timeRow,
                Phase10ControlActionV1.AdvanceControlTick,
                "Advance 100 ms",
                delegate { AdvanceControlTick(); });
            AddActionButton(
                timeRow,
                Phase10ControlActionV1.AdvanceOneSecond,
                "Advance 1 s",
                delegate { AdvanceOneSecond(); });
            AddActionButton(
                timeRow,
                Phase10ControlActionV1.Pause,
                "Pause",
                delegate { Pause(); });
            AddActionButton(
                timeRow,
                Phase10ControlActionV1.Resume,
                "Resume",
                delegate { Resume(); });

            RectTransform playbackRow = CreateRow("ControlsPlaybackRow", _controlsRoot);
            AddActionButton(
                playbackRow,
                Phase10ControlActionV1.AuditPlayback,
                "Use 1x audit",
                delegate { UseAuditPlayback(); });
            AddActionButton(
                playbackRow,
                Phase10ControlActionV1.AcceleratedPlayback,
                "Use 10x play",
                delegate { UseAcceleratedPlayback(); });

            _isBuilt = true;
            SetControlAvailability(false);
        }

        private void CreateTargetRow(
            RectTransform parent,
            string labelText,
            string inputName,
            Phase10ControlActionV1 action,
            string buttonText,
            Action callback)
        {
            RectTransform row = CreateRow(inputName + "Row", parent);
            Text label = AddValueLabel(row, inputName + "Label", labelText, 20, TextAnchor.MiddleLeft, 88);
            LayoutElement labelLayout = label.GetComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1.0f;
            labelLayout.minWidth = 260.0f;

            InputField input = CreateInputField(row, inputName);
            if (action == Phase10ControlActionV1.ApplyPowerTarget)
            {
                _powerTargetInput = input;
            }
            else
            {
                _tiltTargetInput = input;
            }

            AddActionButton(row, action, buttonText, callback);
        }

        private InputField CreateInputField(RectTransform parent, string name)
        {
            RectTransform inputRoot = CreateRect(name, parent);
            LayoutElement inputLayout = inputRoot.gameObject.AddComponent<LayoutElement>();
            inputLayout.minHeight = Phase10ShellView.MinimumTouchTargetPixels;
            inputLayout.preferredHeight = Phase10ShellView.MinimumTouchTargetPixels;
            inputLayout.preferredWidth = 260.0f;

            Image background = inputRoot.gameObject.AddComponent<Image>();
            background.color = new Color(0.10f, 0.16f, 0.22f, 1.0f);
            background.raycastTarget = true;

            InputField input = inputRoot.gameObject.AddComponent<InputField>();
            input.targetGraphic = background;
            input.contentType = InputField.ContentType.DecimalNumber;
            input.lineType = InputField.LineType.SingleLine;

            RectTransform textRoot = CreateRect("Text", inputRoot);
            SetAnchors(textRoot, Vector2.zero, Vector2.one);
            SetOffsets(textRoot, 16.0f, 0.0f, 16.0f, 0.0f);
            Text text = textRoot.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            input.textComponent = text;
            input.text = string.Empty;
            return input;
        }

        private Button AddActionButton(
            RectTransform parent,
            Phase10ControlActionV1 action,
            string labelText,
            Action callback)
        {
            RectTransform buttonRoot = CreateRect("Button." + action, parent);
            LayoutElement buttonLayout = buttonRoot.gameObject.AddComponent<LayoutElement>();
            buttonLayout.minHeight = Phase10ShellView.MinimumTouchTargetPixels;
            buttonLayout.preferredHeight = Phase10ShellView.MinimumTouchTargetPixels;
            buttonLayout.preferredWidth = 230.0f;
            buttonLayout.flexibleWidth = 1.0f;

            Image image = buttonRoot.gameObject.AddComponent<Image>();
            image.color = new Color(0.095f, 0.145f, 0.215f, 1.0f);
            image.raycastTarget = true;

            Button button = buttonRoot.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.095f, 0.145f, 0.215f, 1.0f);
            colors.highlightedColor = new Color(0.16f, 0.29f, 0.42f, 1.0f);
            colors.pressedColor = new Color(0.23f, 0.42f, 0.54f, 1.0f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            Text text = AddValueLabel(buttonRoot, "Label", labelText, 20, TextAnchor.MiddleCenter, 88);
            SetAnchors(text.rectTransform, Vector2.zero, Vector2.one);
            SetOffsets(text.rectTransform, 12.0f, 0.0f, 12.0f, 0.0f);
            button.onClick.AddListener(delegate { callback(); });
            _buttons.Add(action, button);
            return button;
        }

        private static RectTransform CreateRow(string name, RectTransform parent)
        {
            RectTransform row = CreateRect(name, parent);
            LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
            rowLayout.minHeight = Phase10ShellView.MinimumTouchTargetPixels;
            rowLayout.preferredHeight = Phase10ShellView.MinimumTouchTargetPixels;

            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12.0f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return row;
        }

        private Phase8UnityCommandResultV1 Dispatch(
            Func<Phase8UnityCommandResultV1> command)
        {
            if (_runtimeAdapter == null)
            {
                SetStatus("Command rejected: the runtime adapter is not bound.");
                return null;
            }

            try
            {
                return command();
            }
            catch (Exception exception)
            {
                SetStatus("Command rejected before dispatch: " + exception.Message);
                return null;
            }
        }

        private bool TryReadFiniteTarget(
            InputField input,
            string targetName,
            out double target)
        {
            target = 0.0;
            if (input == null ||
                !double.TryParse(
                    input.text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out target) ||
                double.IsNaN(target) ||
                double.IsInfinity(target))
            {
                SetStatus(
                    "Input rejected: " + targetName +
                    " target must be a finite invariant decimal.");
                return false;
            }

            return true;
        }

        private void Unsubscribe()
        {
            if (_runtimeAdapter != null)
            {
                _runtimeAdapter.SnapshotChanged -= HandleSnapshotChanged;
                _runtimeAdapter.CommandCompleted -= HandleCommandCompleted;
            }
        }

        private void ClearPresentation()
        {
            if (!_isBuilt)
            {
                return;
            }

            _observedText.text = "Observed: unavailable";
            _pacingText.text = "Pacing: unavailable";
            _statusText.text = "Status: waiting for runtime binding";
        }

        private void SetControlAvailability(bool isAvailable)
        {
            foreach (Button button in _buttons.Values)
            {
                button.interactable = isAvailable;
            }

            if (_powerTargetInput != null)
            {
                _powerTargetInput.interactable = isAvailable;
            }

            if (_tiltTargetInput != null)
            {
                _tiltTargetInput.interactable = isAvailable;
            }
        }

        private void SetStatus(string status)
        {
            if (_statusText != null)
            {
                _statusText.text = status;
            }
        }

        private static Text AddValueLabel(
            RectTransform parent,
            string name,
            string value,
            int fontSize,
            TextAnchor alignment,
            float height)
        {
            RectTransform labelRoot = CreateRect(name, parent);
            LayoutElement layout = labelRoot.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;

            Text label = labelRoot.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.color = new Color(0.90f, 0.95f, 1.0f, 1.0f);
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            label.text = value;
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
