using System;
using System.Collections.Generic;
using System.Globalization;
using ReactorSim.Game;
using UnityEngine;
using UnityEngine.UI;

namespace ReactorGame.Unity
{
    public enum Phase10RrsReserveWarningTierV1 : byte
    {
        Unavailable = 0,
        Normal = 1,
        Caution = 2,
        Critical = 3,
        Exhausted = 4
    }

    /// <summary>
    /// Read-only Dashboard projection of the approved Phase 10 presentation
    /// snapshot. The RRS card binds GameSession's authoritative projection;
    /// this component only formats state and owns presentation visuals.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Phase10DashboardView : MonoBehaviour
    {
        public const double RrsCautionBoundaryFraction = 0.15;
        public const double RrsCriticalBoundaryFraction = 0.05;
        private const double RrsCommandCueTolerance = 1.0e-6;

        private static readonly Color ReserveNormalColor =
            new Color(0.18f, 0.82f, 0.78f, 1.0f);
        private static readonly Color ReserveCautionColor =
            new Color(1.0f, 0.68f, 0.22f, 1.0f);
        private static readonly Color ReserveCriticalColor =
            new Color(1.0f, 0.30f, 0.20f, 1.0f);
        private static readonly Color ReserveExhaustedColor =
            new Color(1.0f, 0.16f, 0.34f, 1.0f);
        private static readonly Color ReserveUnavailableColor =
            new Color(0.52f, 0.60f, 0.68f, 1.0f);
        private static readonly Color RunStakesGuidanceColor =
            new Color(0.77f, 0.87f, 0.93f, 1.0f);
        private static readonly Color RunStakesDisabledColor =
            new Color(0.42f, 0.49f, 0.57f, 1.0f);

        private Phase8UnityRuntimeAdapter _runtimeAdapter;
        private RectTransform _dashboardRoot;
        private Text _scenarioText;
        private Text _playbackText;
        private Text _pacingText;
        private Text _powerText;
        private Text _resourcesText;
        private Text _scoreText;
        private Text _outcomeText;
        private Text _reserveValueText;
        private Text _reserveHeadroomText;
        private Text _reserveRangeText;
        private Text _rrsStatusText;
        private Text _rrsPressureText;
        private Text _rrsWarningText;
        private Text _rrsPowerText;
        private Text _runStakesText;
        private Text _runStakesGuidanceText;
        private Text _runStakesTerminalText;
        private Image _reserveMeterImage;
        private Button _restartRunButton;
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

        public GameRrsPresentationSnapshot RrsSnapshot { get; private set; }

        public Phase10RrsReserveWarningTierV1 RrsWarningTier { get; private set; }

        public float ReserveMeterFillAmount
        {
            get { return _reserveMeterImage == null ? 0.0f : _reserveMeterImage.fillAmount; }
        }

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

        public string ReserveText
        {
            get { return GetText(_reserveValueText); }
        }

        public string RrsHeadroomText
        {
            get { return GetText(_reserveHeadroomText); }
        }

        public string RrsRangeText
        {
            get { return GetText(_reserveRangeText); }
        }

        public string RrsStatusText
        {
            get { return GetText(_rrsStatusText); }
        }

        public string RrsPressureText
        {
            get { return GetText(_rrsPressureText); }
        }

        public string RrsWarningText
        {
            get { return GetText(_rrsWarningText); }
        }

        public string RrsPowerText
        {
            get { return GetText(_rrsPowerText); }
        }

        public string RunStakesText
        {
            get { return GetText(_runStakesText); }
        }

        public string RunStakesGuidanceText
        {
            get { return GetText(_runStakesGuidanceText); }
        }

        public string TerminalReasonText
        {
            get { return GetText(_runStakesTerminalText); }
        }

        public Button RestartRunButton
        {
            get { return _restartRunButton; }
        }

        public bool RestartRunButtonInteractable
        {
            get { return _restartRunButton != null && _restartRunButton.interactable; }
        }

        public bool TryGetRestartRunButton(out Button button)
        {
            button = _restartRunButton;
            return button != null;
        }

        /// <summary>
        /// Deterministic presentation-only warning classification. The
        /// terminal bit remains authoritative: Unity never infers exhaustion
        /// from a future action or predicts a reserve trajectory.
        /// </summary>
        public static Phase10RrsReserveWarningTierV1 GetReserveWarningTier(
            double averageFillFraction,
            bool isGameOver)
        {
            if (double.IsNaN(averageFillFraction) ||
                double.IsInfinity(averageFillFraction) ||
                averageFillFraction < 0.0 ||
                averageFillFraction > 1.0)
            {
                return Phase10RrsReserveWarningTierV1.Unavailable;
            }

            if (isGameOver)
            {
                return Phase10RrsReserveWarningTierV1.Exhausted;
            }

            double nearestBoundary = Math.Min(
                averageFillFraction,
                1.0 - averageFillFraction);
            if (nearestBoundary <= RrsCriticalBoundaryFraction)
            {
                return Phase10RrsReserveWarningTierV1.Critical;
            }

            if (nearestBoundary <= RrsCautionBoundaryFraction)
            {
                return Phase10RrsReserveWarningTierV1.Caution;
            }

            return Phase10RrsReserveWarningTierV1.Normal;
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
            _powerText.text = "Power: " + FormatPercent(snapshot.ActualPowerFraction) +
                " actual | Setpoint: " + FormatPercent(snapshot.NormalizedPowerFraction) +
                " | Tilt: " + FormatPercent(snapshot.AbsoluteTiltFraction) +
                " | Control margin: " + FormatPercent(snapshot.ControlMarginFraction);
            _resourcesText.text = "Device: " + FormatPercent(snapshot.DeviceAvailableFraction) +
                " | Refuel requests: " + Format(snapshot.RefuelRequestsRemaining) +
                " | Pending actions: " + Format(snapshot.PendingActionCount) +
                " | Scripted events: " + Format(snapshot.ProcessedScriptedEventCount);
            _scoreText.text = "Score: " + Format(snapshot.ScoreTotal) +
                " | Turn summaries: " + Format(snapshot.TurnSummaryCount);
            _outcomeText.text = "Outcome: " + snapshot.OutcomeId;
            RenderRrs(snapshot.Rrs);
            RenderRunStakes(snapshot, snapshot.Rrs);
        }

        private void RenderRrs(GameRrsPresentationSnapshot rrs)
        {
            RrsSnapshot = rrs;
            if (rrs == null)
            {
                ClearRrsPresentation();
                return;
            }

            double averageFill = rrs.AverageFillFraction;
            RrsWarningTier = GetReserveWarningTier(averageFill, rrs.IsGameOver);
            _reserveMeterImage.fillAmount = Mathf.Clamp01((float)averageFill);
            _reserveValueText.text = FormatPercent(averageFill);
            _reserveHeadroomText.text =
                "HEADROOM  /  TO EMPTY " + FormatPercent(averageFill) +
                "  |  TO FULL " + FormatPercent(1.0 - averageFill);
            _reserveRangeText.text =
                "ZONE RANGE  /  " + FormatPercent(rrs.MinimumFillFraction) +
                " — " + FormatPercent(rrs.MaximumFillFraction) +
                "  |  SPREAD " + FormatPoints(
                    rrs.MaximumFillFraction - rrs.MinimumFillFraction);
            _rrsStatusText.text = "AUTO RRS  /  " +
                (rrs.IsGameOver
                    ? "TERMINAL"
                    : (rrs.ControllerConverged ? "CONVERGED" : "ADJUSTING")) +
                "  /  ITER " + Format(rrs.ControllerIterationCount);
            _rrsPressureText.text = "PRESSURE CUE  /  " + FormatPressureCue(rrs);
            _rrsPowerText.text =
                "CURRENT RESPONSE  /  POWER ERROR " +
                FormatSignedWatts(rrs.PowerErrorWatts) +
                "  |  NET RHO " + FormatSignedRho(rrs.CompensatedNetReactivity);
            _rrsWarningText.text = FormatWarningText(rrs, RrsWarningTier);
            _rrsWarningText.fontSize = rrs.IsGameOver ? 21 : 17;

            Color accent = GetWarningColor(RrsWarningTier);
            _reserveMeterImage.color = accent;
            _reserveValueText.color = accent;
            _rrsWarningText.color = accent;
            _rrsStatusText.color = rrs.ControllerConverged && !rrs.IsGameOver
                ? new Color(0.58f, 0.92f, 0.78f, 1.0f)
                : accent;
        }

        private void RenderRunStakes(
            Phase8UnityPresentationSnapshotV1 snapshot,
            GameRrsPresentationSnapshot rrs)
        {
            if (snapshot == null)
            {
                ClearRunStakesPresentation();
                return;
            }

            _runStakesText.text =
                "SCORE  " + Format(snapshot.ScoreTotal) +
                "  ·  FRESH BUNDLES  " + Format(snapshot.FreshBundlesAvailable) +
                "\nREFUELLING OPS  " + Format(snapshot.RefuellingOperationCount) +
                "  ·  ACTUAL POWER  " + FormatPercent(snapshot.ActualPowerFraction) +
                "\n" + FormatNearestRrsHeadroom(rrs);

            bool isTerminal = rrs != null && rrs.IsGameOver;
            _runStakesGuidanceText.text = isTerminal
                ? "RUN GUIDANCE  /  DISABLED — TERMINAL RRS STATE"
                : rrs == null
                    ? "RUN GUIDANCE  /  WAITING FOR RRS TELEMETRY"
                    : "KEEP RRS RESERVE AWAY FROM 0% / 100%.\n" +
                      "CONSERVE FRESH BUNDLES. STABLE POWER + USEFUL DISCHARGED BURNUP EARN SCORE.";
            _runStakesGuidanceText.color = isTerminal
                ? RunStakesDisabledColor
                : RunStakesGuidanceColor;

            _runStakesTerminalText.gameObject.SetActive(isTerminal);
            if (isTerminal)
            {
                _runStakesTerminalText.text =
                    "TERMINAL RRS EXHAUSTION\n" +
                    (string.IsNullOrWhiteSpace(rrs.GameOverReason)
                        ? "RRS RESERVE LIMIT REACHED"
                        : rrs.GameOverReason);
            }
            else
            {
                _runStakesTerminalText.text = string.Empty;
            }

            _restartRunButton.interactable = isTerminal;
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
            SetAnchors(_dashboardRoot, new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.96f));
            SetOffsets(_dashboardRoot, 0.0f, 0.0f, 0.0f, 0.0f);

            Image cardImage = _dashboardRoot.gameObject.AddComponent<Image>();
            cardImage.color = new Color(0.018f, 0.040f, 0.068f, 0.98f);
            cardImage.raycastTarget = false;
            Outline outline = _dashboardRoot.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.11f, 0.32f, 0.39f, 0.85f);
            outline.effectDistance = new Vector2(2.0f, -2.0f);

            RectTransform accent = CreateRect("DashboardAccent", _dashboardRoot);
            SetAnchors(accent, new Vector2(0.0f, 0.985f), Vector2.one);
            SetOffsets(accent, 0.0f, 0.0f, 0.0f, 0.0f);
            AddImage(accent, new Color(0.18f, 0.82f, 0.78f, 0.95f));
            IgnoreLayout(accent);

            VerticalLayoutGroup layout = _dashboardRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10.0f;
            layout.padding = new RectOffset(26, 26, 22, 22);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            RectTransform header = CreateRect("DashboardHeader", _dashboardRoot);
            SetLayoutHeight(header, 46.0f);
            HorizontalLayoutGroup headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 12.0f;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = true;
            headerLayout.childForceExpandHeight = true;
            AddLabel(
                header,
                "DashboardHeading",
                "RRS RESERVE  //  LIVE",
                25,
                46.0f,
                new Color(0.88f, 0.97f, 1.0f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            AddLabel(
                header,
                "DashboardSubheading",
                "LIQUID-ZONE CONTROL  ·  14 ZONES",
                14,
                46.0f,
                new Color(0.43f, 0.72f, 0.78f, 1.0f),
                TextAnchor.MiddleRight,
                FontStyle.Normal);

            RectTransform rrsRow = CreateRect("RrsReserveRow", _dashboardRoot);
            SetLayoutHeight(rrsRow, 330.0f);
            HorizontalLayoutGroup rrsRowLayout = rrsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            rrsRowLayout.spacing = 12.0f;
            rrsRowLayout.childAlignment = TextAnchor.UpperLeft;
            rrsRowLayout.childControlWidth = true;
            rrsRowLayout.childControlHeight = true;
            rrsRowLayout.childForceExpandWidth = false;
            rrsRowLayout.childForceExpandHeight = true;

            RectTransform reservePanel = CreatePanel(
                rrsRow,
                "ReservePanel",
                new Color(0.030f, 0.085f, 0.105f, 0.98f));
            SetFlexibleWidth(reservePanel, 1.20f);
            AddLabel(
                reservePanel,
                "ReserveEyebrow",
                "PRIMARY RESERVE METER",
                14,
                24.0f,
                new Color(0.42f, 0.82f, 0.82f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            _reserveValueText = AddLabel(
                reservePanel,
                "AverageReserveValue",
                "--",
                52,
                68.0f,
                new Color(0.18f, 0.82f, 0.78f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            AddLabel(
                reservePanel,
                "AverageReserveCaption",
                "AVERAGE LIQUID-ZONE FILL  ·  ACCEPTED CURRENT STATE",
                13,
                24.0f,
                new Color(0.64f, 0.78f, 0.82f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            RectTransform meterRoot = CreateRect("ReserveMeter", reservePanel);
            SetLayoutHeight(meterRoot, 34.0f);
            AddImage(meterRoot, new Color(0.012f, 0.035f, 0.050f, 1.0f));
            RectTransform fillRoot = CreateRect("ReserveMeterFill", meterRoot);
            SetAnchors(fillRoot, new Vector2(0.012f, 0.16f), new Vector2(0.988f, 0.84f));
            SetOffsets(fillRoot, 0.0f, 0.0f, 0.0f, 0.0f);
            _reserveMeterImage = AddImage(fillRoot, ReserveNormalColor);
            _reserveMeterImage.type = Image.Type.Filled;
            _reserveMeterImage.fillMethod = Image.FillMethod.Horizontal;
            _reserveMeterImage.fillOrigin = 0;
            _reserveMeterImage.fillAmount = 0.0f;
            RectTransform meterLabels = CreateRect("ReserveMeterLabels", reservePanel);
            SetLayoutHeight(meterLabels, 22.0f);
            HorizontalLayoutGroup meterLabelLayout = meterLabels.gameObject.AddComponent<HorizontalLayoutGroup>();
            meterLabelLayout.childAlignment = TextAnchor.MiddleLeft;
            meterLabelLayout.childControlWidth = true;
            meterLabelLayout.childControlHeight = true;
            meterLabelLayout.childForceExpandWidth = true;
            meterLabelLayout.childForceExpandHeight = true;
            AddLabel(
                meterLabels,
                "ReserveEmptyLabel",
                "EMPTY  /  0%",
                13,
                22.0f,
                new Color(0.54f, 0.63f, 0.70f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            AddLabel(
                meterLabels,
                "ReserveFullLabel",
                "FULL  /  100%",
                13,
                22.0f,
                new Color(0.54f, 0.63f, 0.70f, 1.0f),
                TextAnchor.MiddleRight,
                FontStyle.Normal);
            _reserveHeadroomText = AddLabel(
                reservePanel,
                "ReserveHeadroom",
                "HEADROOM  /  TO EMPTY --  |  TO FULL --",
                17,
                31.0f,
                new Color(0.90f, 0.96f, 1.0f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            _reserveRangeText = AddLabel(
                reservePanel,
                "ReserveRange",
                "ZONE RANGE  /  --  |  SPREAD --",
                15,
                28.0f,
                new Color(0.67f, 0.80f, 0.84f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            CreateFlexibleSpacer(reservePanel);
            AddLabel(
                reservePanel,
                "ReserveReadoutNote",
                "Headroom is measured from the current average fill; zone range shows local spread.",
                12,
                31.0f,
                new Color(0.43f, 0.58f, 0.64f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Normal);

            RectTransform signalPanel = CreatePanel(
                rrsRow,
                "RrsSignalPanel",
                new Color(0.048f, 0.060f, 0.095f, 0.98f));
            SetFlexibleWidth(signalPanel, 0.80f);
            AddLabel(
                signalPanel,
                "RrsSignalEyebrow",
                "AUTOMATIC RRS SIGNAL",
                14,
                24.0f,
                new Color(0.50f, 0.67f, 0.92f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            _rrsStatusText = AddLabel(
                signalPanel,
                "RrsStatus",
                "AUTO RRS  /  WAITING",
                22,
                50.0f,
                new Color(0.58f, 0.92f, 0.78f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            _rrsWarningText = AddLabel(
                signalPanel,
                "RrsWarning",
                "RESERVE TELEMETRY UNAVAILABLE",
                17,
                54.0f,
                ReserveUnavailableColor,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            _rrsPressureText = AddLabel(
                signalPanel,
                "RrsPressureCue",
                "PRESSURE CUE  /  WAITING",
                16,
                48.0f,
                new Color(0.90f, 0.94f, 1.0f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            _rrsPowerText = AddLabel(
                signalPanel,
                "RrsPowerResponse",
                "CURRENT RESPONSE  /  WAITING",
                15,
                31.0f,
                new Color(0.68f, 0.76f, 0.88f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            CreateFlexibleSpacer(signalPanel);
            AddLabel(
                signalPanel,
                "RrsSignalNote",
                "Cue reflects only the accepted current command and convergence state.",
                12,
                31.0f,
                new Color(0.43f, 0.53f, 0.68f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Normal);

            RectTransform bottomRow = CreateRect("DashboardBottomRow", _dashboardRoot);
            SetLayoutHeight(bottomRow, 280.0f);
            HorizontalLayoutGroup bottomRowLayout = bottomRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            bottomRowLayout.spacing = 12.0f;
            bottomRowLayout.childAlignment = TextAnchor.UpperLeft;
            bottomRowLayout.childControlWidth = true;
            bottomRowLayout.childControlHeight = true;
            bottomRowLayout.childForceExpandWidth = false;
            bottomRowLayout.childForceExpandHeight = true;

            RectTransform operatingPanel = CreatePanel(
                bottomRow,
                "OperatingSnapshot",
                new Color(0.030f, 0.042f, 0.070f, 0.94f));
            SetFlexibleWidth(operatingPanel, 1.20f);
            AddLabel(
                operatingPanel,
                "OperatingSnapshotHeading",
                "OPERATING PULSE",
                14,
                24.0f,
                new Color(0.48f, 0.68f, 0.78f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            _scenarioText = AddLabel(
                operatingPanel,
                "DashboardScenario",
                "Scenario: waiting for runtime binding",
                15,
                27.0f,
                new Color(0.82f, 0.90f, 0.97f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            _playbackText = AddLabel(
                operatingPanel,
                "DashboardPlayback",
                "Playback: unavailable",
                15,
                27.0f,
                new Color(0.82f, 0.90f, 0.97f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            _pacingText = AddLabel(
                operatingPanel,
                "DashboardPacing",
                "Pacing: unavailable",
                15,
                27.0f,
                new Color(0.82f, 0.90f, 0.97f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            _powerText = AddLabel(
                operatingPanel,
                "DashboardPower",
                "Power: unavailable",
                15,
                27.0f,
                new Color(0.82f, 0.90f, 0.97f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            _resourcesText = AddLabel(
                operatingPanel,
                "DashboardResources",
                "Resources: unavailable",
                15,
                27.0f,
                new Color(0.82f, 0.90f, 0.97f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            _scoreText = AddLabel(
                operatingPanel,
                "DashboardScore",
                "Score: unavailable",
                15,
                27.0f,
                new Color(0.82f, 0.90f, 0.97f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            _outcomeText = AddLabel(
                operatingPanel,
                "DashboardOutcome",
                "Outcome: unavailable",
                15,
                27.0f,
                new Color(0.92f, 0.94f, 1.0f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Bold);

            RectTransform runStakesPanel = CreatePanel(
                bottomRow,
                "RunStakesPanel",
                new Color(0.062f, 0.052f, 0.090f, 0.96f));
            SetFlexibleWidth(runStakesPanel, 0.80f);
            AddLabel(
                runStakesPanel,
                "RunStakesHeading",
                "RUN STAKES  //  CURRENT RUN",
                18,
                20.0f,
                new Color(0.93f, 0.82f, 0.98f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            _runStakesText = AddLabel(
                runStakesPanel,
                "RunStakesValues",
                "SCORE  --  ·  FRESH BUNDLES  --\nREFUELLING OPS  --  ·  ACTUAL POWER  --\nNEAREST RRS HEADROOM  /  --",
                13,
                50.0f,
                new Color(0.92f, 0.91f, 1.0f, 1.0f),
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            _runStakesGuidanceText = AddLabel(
                runStakesPanel,
                "RunStakesGuidance",
                "RUN GUIDANCE  /  WAITING FOR RRS TELEMETRY",
                12,
                30.0f,
                RunStakesGuidanceColor,
                TextAnchor.MiddleLeft,
                FontStyle.Normal);
            _runStakesTerminalText = AddLabel(
                runStakesPanel,
                "RunStakesTerminal",
                string.Empty,
                12,
                40.0f,
                ReserveExhaustedColor,
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            _runStakesTerminalText.gameObject.SetActive(false);
            _restartRunButton = AddButton(
                runStakesPanel,
                "RestartRunButton",
                "RESTART PRACTICE RUN",
                delegate { RestartPracticeSession(); });
            _restartRunButton.interactable = false;

            _isBuilt = true;
        }

        private void Unsubscribe()
        {
            if (_runtimeAdapter != null)
            {
                _runtimeAdapter.SnapshotChanged -= HandleSnapshotChanged;
            }
        }

        private void RestartPracticeSession()
        {
            UnityGameController controller = GetComponent<UnityGameController>();
            if (controller != null)
            {
                controller.RestartPracticeSession();
            }
        }

        private void ClearPresentation()
        {
            Snapshot = null;
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
            ClearRrsPresentation();
            ClearRunStakesPresentation();
        }

        private void ClearRrsPresentation()
        {
            RrsSnapshot = null;
            RrsWarningTier = Phase10RrsReserveWarningTierV1.Unavailable;
            if (_reserveMeterImage != null)
            {
                _reserveMeterImage.fillAmount = 0.0f;
                _reserveMeterImage.color = ReserveUnavailableColor;
            }

            if (_reserveValueText != null)
            {
                _reserveValueText.text = "--";
                _reserveValueText.color = ReserveUnavailableColor;
            }

            if (_reserveHeadroomText != null)
            {
                _reserveHeadroomText.text = "HEADROOM  /  TO EMPTY --  |  TO FULL --";
            }

            if (_reserveRangeText != null)
            {
                _reserveRangeText.text = "ZONE RANGE  /  --  |  SPREAD --";
            }

            if (_rrsStatusText != null)
            {
                _rrsStatusText.text = "AUTO RRS  /  WAITING";
            }

            if (_rrsPressureText != null)
            {
                _rrsPressureText.text = "PRESSURE CUE  /  WAITING";
            }

            if (_rrsWarningText != null)
            {
                _rrsWarningText.text = "RESERVE TELEMETRY UNAVAILABLE";
                _rrsWarningText.color = ReserveUnavailableColor;
            }

            if (_rrsPowerText != null)
            {
                _rrsPowerText.text = "CURRENT RESPONSE  /  WAITING";
            }
        }

        private void ClearRunStakesPresentation()
        {
            if (_runStakesText != null)
            {
                _runStakesText.text =
                    "SCORE  --  ·  FRESH BUNDLES  --\n" +
                    "REFUELLING OPS  --  ·  ACTUAL POWER  --\n" +
                    "NEAREST RRS HEADROOM  /  --";
            }

            if (_runStakesGuidanceText != null)
            {
                _runStakesGuidanceText.text = "RUN GUIDANCE  /  WAITING FOR RUNTIME";
                _runStakesGuidanceText.color = RunStakesGuidanceColor;
            }

            if (_runStakesTerminalText != null)
            {
                _runStakesTerminalText.text = string.Empty;
                _runStakesTerminalText.gameObject.SetActive(false);
            }

            if (_restartRunButton != null)
            {
                _restartRunButton.interactable = false;
            }
        }

        private static string FormatNearestRrsHeadroom(GameRrsPresentationSnapshot rrs)
        {
            if (rrs == null)
            {
                return "NEAREST RRS HEADROOM  /  --";
            }

            double averageFill = rrs.AverageFillFraction;
            if (double.IsNaN(averageFill) ||
                double.IsInfinity(averageFill) ||
                averageFill < 0.0 ||
                averageFill > 1.0)
            {
                return "NEAREST RRS HEADROOM  /  --";
            }

            double toEmpty = averageFill;
            double toFull = 1.0 - averageFill;
            bool emptyIsNearest = toEmpty <= toFull;
            return "NEAREST RRS HEADROOM  /  " +
                FormatPercent(emptyIsNearest ? toEmpty : toFull) +
                " TO " + (emptyIsNearest ? "EMPTY" : "FULL") +
                "  ·  AVG " + FormatPercent(averageFill);
        }

        private static string FormatPressureCue(GameRrsPresentationSnapshot rrs)
        {
            if (rrs.IsGameOver)
            {
                return "TERMINAL  ·  RESERVE EXHAUSTED";
            }

            double commandMagnitude = MaxAbsolute(rrs.AppliedFillCommand);
            if (!rrs.ControllerConverged)
            {
                return commandMagnitude > RrsCommandCueTolerance
                    ? "CORRECTING  ·  CURRENT COMMAND " + FormatPercent(commandMagnitude)
                    : "CORRECTING  ·  RESIDUAL REMAINS";
            }

            return commandMagnitude > RrsCommandCueTolerance
                ? "SETTLED  ·  CURRENT COMMAND " + FormatPercent(commandMagnitude)
                : "HOLDING  ·  NO CURRENT COMMAND";
        }

        private static string FormatWarningText(
            GameRrsPresentationSnapshot rrs,
            Phase10RrsReserveWarningTierV1 warningTier)
        {
            switch (warningTier)
            {
                case Phase10RrsReserveWarningTierV1.Exhausted:
                    return "RESERVE EXHAUSTED  /  " +
                        (string.IsNullOrWhiteSpace(rrs.GameOverReason)
                            ? "TERMINAL RRS STATE"
                            : rrs.GameOverReason);
                case Phase10RrsReserveWarningTierV1.Critical:
                    return IsLowSide(rrs.AverageFillFraction)
                        ? "LOW RESERVE  /  CRITICAL  ·  " +
                          FormatPercent(rrs.AverageFillFraction) + " TO EMPTY"
                        : "HIGH RESERVE  /  CRITICAL  ·  " +
                          FormatPercent(1.0 - rrs.AverageFillFraction) + " TO FULL";
                case Phase10RrsReserveWarningTierV1.Caution:
                    return IsLowSide(rrs.AverageFillFraction)
                        ? "LOW RESERVE  /  CAUTION  ·  " +
                          FormatPercent(rrs.AverageFillFraction) + " TO EMPTY"
                        : "HIGH RESERVE  /  CAUTION  ·  " +
                          FormatPercent(1.0 - rrs.AverageFillFraction) + " TO FULL";
                case Phase10RrsReserveWarningTierV1.Normal:
                    return "RESERVE NORMAL  /  OPERATING BAND";
                default:
                    return "RESERVE TELEMETRY UNAVAILABLE";
            }
        }

        private static bool IsLowSide(double averageFillFraction)
        {
            return averageFillFraction <= 0.5;
        }

        private static Color GetWarningColor(Phase10RrsReserveWarningTierV1 warningTier)
        {
            switch (warningTier)
            {
                case Phase10RrsReserveWarningTierV1.Caution:
                    return ReserveCautionColor;
                case Phase10RrsReserveWarningTierV1.Critical:
                    return ReserveCriticalColor;
                case Phase10RrsReserveWarningTierV1.Exhausted:
                    return ReserveExhaustedColor;
                case Phase10RrsReserveWarningTierV1.Normal:
                    return ReserveNormalColor;
                default:
                    return ReserveUnavailableColor;
            }
        }

        private static double MaxAbsolute(IReadOnlyList<double> values)
        {
            if (values == null)
            {
                return 0.0;
            }

            double maximum = 0.0;
            for (int index = 0; index < values.Count; index++)
            {
                double value = values[index];
                if (!double.IsNaN(value) && !double.IsInfinity(value))
                {
                    maximum = Math.Max(maximum, Math.Abs(value));
                }
            }

            return maximum;
        }

        private static Text AddLabel(
            RectTransform parent,
            string name,
            string textValue,
            int fontSize,
            float height,
            Color color,
            TextAnchor alignment,
            FontStyle fontStyle)
        {
            RectTransform labelRoot = CreateRect(name, parent);
            LayoutElement layout = labelRoot.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;

            Text label = labelRoot.gameObject.AddComponent<Text>();
            label.text = textValue;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private static Button AddButton(
            RectTransform parent,
            string name,
            string labelText,
            Action callback)
        {
            RectTransform buttonRoot = CreateRect(name, parent);
            LayoutElement buttonLayout = buttonRoot.gameObject.AddComponent<LayoutElement>();
            buttonLayout.minHeight = Phase10ShellView.MinimumTouchTargetPixels;
            buttonLayout.preferredHeight = Phase10ShellView.MinimumTouchTargetPixels;

            Color buttonColor = new Color(0.13f, 0.10f, 0.20f, 1.0f);
            Image image = AddImage(buttonRoot, buttonColor);
            image.raycastTarget = true;

            Button button = buttonRoot.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = buttonColor;
            colors.highlightedColor = new Color(0.28f, 0.22f, 0.40f, 1.0f);
            colors.pressedColor = new Color(0.40f, 0.30f, 0.52f, 1.0f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            Text text = AddLabel(
                buttonRoot,
                "RestartRunLabel",
                labelText,
                16,
                Phase10ShellView.MinimumTouchTargetPixels,
                Color.white,
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            SetAnchors(text.rectTransform, Vector2.zero, Vector2.one);
            SetOffsets(text.rectTransform, 10.0f, 0.0f, 10.0f, 0.0f);
            button.onClick.AddListener(delegate { callback(); });
            return button;
        }

        private static RectTransform CreatePanel(
            RectTransform parent,
            string name,
            Color color)
        {
            RectTransform panel = CreateRect(name, parent);
            Image image = AddImage(panel, color);
            image.raycastTarget = false;
            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5.0f;
            layout.padding = new RectOffset(14, 14, 14, 14);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return panel;
        }

        private static Image AddImage(RectTransform parent, Color color)
        {
            Image image = parent.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        private static void CreateFlexibleSpacer(RectTransform parent)
        {
            RectTransform spacer = CreateRect("Spacer", parent);
            LayoutElement layout = spacer.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 0.0f;
            layout.flexibleHeight = 1.0f;
        }

        private static void IgnoreLayout(RectTransform rect)
        {
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
        }

        private static void SetFlexibleWidth(RectTransform rect, float flexibleWidth)
        {
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.flexibleWidth = flexibleWidth;
            layout.minWidth = 180.0f;
        }

        private static void SetLayoutHeight(RectTransform rect, float height)
        {
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
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

        private static string Format(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Format(uint value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatPercent(double value)
        {
            return value.ToString("0.0%", CultureInfo.InvariantCulture);
        }

        private static string FormatPoints(double value)
        {
            return value.ToString("0.0", CultureInfo.InvariantCulture) + " pts";
        }

        private static string FormatSignedWatts(double value)
        {
            return FormatSigned(value, "0.0") + " W";
        }

        private static string FormatSignedRho(double value)
        {
            return FormatSigned(value, "0.0000");
        }

        private static string FormatSigned(double value, string format)
        {
            if (Math.Abs(value) < 0.0000005)
            {
                return 0.0.ToString(format, CultureInfo.InvariantCulture);
            }

            return (value > 0.0 ? "+" : string.Empty) +
                value.ToString(format, CultureInfo.InvariantCulture);
        }
    }
}
