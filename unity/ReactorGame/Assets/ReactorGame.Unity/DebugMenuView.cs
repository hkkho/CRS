using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ReactorSim.Game;
using UnityEngine;
using UnityEngine.UI;

namespace ReactorGame.Unity
{
    /// <summary>
    /// Presentation-only actions exposed by the debug playtesting overlay.
    /// The aliases keep the public lookup convenient for callers while the
    /// overlay creates one button for each canonical action.
    /// </summary>
    public enum DebugMenuActionV1 : byte
    {
        Pause = 0,
        Resume = 1,
        SingleStep = 2,
        Playback1x = 3,
        Playback10x = 4,
        Playback60x = 5,
        AdvanceHour = 6,
        AdvanceDay = 7,
        JumpEquilibrium = 8,
        Restart = 9,
        GrantFuel = 10,
        ClearPendingActions = 11,
        ResetSyntheticResponse = 12,
        CopyDigest = 13,

        Step = SingleStep,
        PlaybackOneX = Playback1x,
        PlaybackTenX = Playback10x,
        PlaybackSixtyX = Playback60x,
        Playback1X = Playback1x,
        Playback10X = Playback10x,
        Playback60X = Playback60x,
        TimeScale1x = Playback1x,
        TimeScale10x = Playback10x,
        TimeScale60x = Playback60x,
        AddHour = AdvanceHour,
        AddDay = AdvanceDay,
        AdvanceOneHour = AdvanceHour,
        AdvanceOneDay = AdvanceDay,
        PlusOneHour = AdvanceHour,
        PlusOneDay = AdvanceDay,
        JumpToEquilibrium = JumpEquilibrium,
        JumpToEquilibriumLikeState = JumpEquilibrium,
        RestartPracticeSession = Restart,
        GrantFreshBundles = GrantFuel,
        GrantFuelBundles = GrantFuel,
        GrantPlus100Fuel = GrantFuel,
        GrantPlus100Bundles = GrantFuel,
        ClearPending = ClearPendingActions,
        ResetSynthetic = ResetSyntheticResponse,
        ResetResponse = ResetSyntheticResponse,
        ResetScore = ResetSyntheticResponse,
        CopyStateDigest = CopyDigest,
        CopyStateDigestHash = CopyDigest
    }

    /// <summary>
    /// A dynamic legacy uGUI debug overlay for deterministic owner playtesting.
    /// It owns no simulation state: every runtime mutation is sent through
    /// Phase8UnityRuntimeAdapter, with restart delegated to the same-object
    /// UnityGameController when that controller is available.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebugMenuView : MonoBehaviour
    {
        public const ulong SingleStepWallMilliseconds = 100;
        public const double SimulationHourSeconds = 3600.0;
        public const double SimulationDaySeconds = 86400.0;
        public const double EquilibriumLikeJumpSeconds = 360.0;
        public const uint GrantFuelBundleCount = 100;

        public const string Playback1xModeId = "audit-real-time-1x";
        public const string Playback10xModeId = "play-accelerated-10x";
        public const string Playback60xModeId = "debug-accelerated-60x";

        private static readonly Color OverlayColor = new Color(0.018f, 0.026f, 0.045f, 0.96f);
        private static readonly Color PanelColor = new Color(0.035f, 0.060f, 0.095f, 0.985f);
        private static readonly Color ButtonColor = new Color(0.16f, 0.20f, 0.27f, 1.0f);
        private static readonly Color ButtonHighlightColor = new Color(0.28f, 0.40f, 0.52f, 1.0f);
        private static readonly Color ButtonPressedColor = new Color(0.40f, 0.56f, 0.64f, 1.0f);
        private static readonly Color DebugAccentColor = new Color(1.0f, 0.76f, 0.22f, 1.0f);

        private readonly Dictionary<DebugMenuActionV1, Button> _buttons =
            new Dictionary<DebugMenuActionV1, Button>();

        private Phase8UnityRuntimeAdapter _runtimeAdapter;
        private Phase8UnityPresentationSnapshotV1 _snapshot;
        private RectTransform _overlayRoot;
        private Text _stateText;
        private Text _rawCoreText;
        private Text _commandText;
        private Text _statusText;
        private Text _digestText;
        private bool _isBuilt;
        private bool _hasCommandResult;
        private ulong _lastCommandSequence;
        private string _lastCommandKind = "none";
        private bool _lastCommandAccepted;
        private string _lastDiagnosticCode = string.Empty;

        public bool IsBuilt
        {
            get { return _isBuilt; }
        }

        public bool IsBound
        {
            get { return _runtimeAdapter != null; }
        }

        public bool IsVisible
        {
            get { return _overlayRoot != null && _overlayRoot.gameObject.activeSelf; }
        }

        public Phase8UnityPresentationSnapshotV1 Snapshot
        {
            get { return _snapshot; }
        }

        public int ActionButtonCount
        {
            get { return _buttons.Count; }
        }

        public string StateText
        {
            get { return GetText(_stateText); }
        }

        public string RawCoreText
        {
            get { return GetText(_rawCoreText); }
        }

        public string CommandText
        {
            get { return GetText(_commandText); }
        }

        public string StatusText
        {
            get { return GetText(_statusText); }
        }

        public string DigestText
        {
            get { return GetText(_digestText); }
        }

        private void Awake()
        {
            if (GetComponent<Phase10ShellView>() != null)
            {
                EnsureVisuals();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote) || Input.GetKeyDown(KeyCode.F1))
            {
                ToggleVisibility();
            }
        }

        /// <summary>
        /// Builds the overlay once from legacy uGUI primitives. It is safe to
        /// call repeatedly while a host is wiring the shell and adapter.
        /// </summary>
        public void EnsureVisuals()
        {
            if (_isBuilt)
            {
                return;
            }

            Phase10ShellView shell = GetComponent<Phase10ShellView>();
            if (shell == null)
            {
                throw new InvalidOperationException(
                    "The Debug Menu must be attached to a Phase10ShellView.");
            }

            shell.BuildVisualShell();
            Canvas shellCanvas = shell.ShellCanvas;
            RectTransform canvasRoot = shellCanvas == null
                ? null
                : shellCanvas.GetComponent<RectTransform>();
            if (canvasRoot == null)
            {
                throw new InvalidOperationException(
                    "The Phase 10 shell has no canvas root for the Debug Menu.");
            }

            _overlayRoot = CreateRect("DebugMenuOverlay", canvasRoot);
            Stretch(_overlayRoot);

            Canvas overlayCanvas = _overlayRoot.gameObject.AddComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = shellCanvas.sortingOrder + 10;
            _overlayRoot.gameObject.AddComponent<GraphicRaycaster>();

            Image overlayImage = _overlayRoot.gameObject.AddComponent<Image>();
            overlayImage.color = OverlayColor;
            overlayImage.raycastTarget = true;

            RectTransform panel = CreateRect("DebugMenuPanel", _overlayRoot);
            SetAnchors(panel, new Vector2(0.025f, 0.035f), new Vector2(0.68f, 0.965f));
            SetOffsets(panel, 0.0f, 0.0f, 0.0f, 0.0f);

            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = PanelColor;
            panelImage.raycastTarget = true;

            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6.0f;
            layout.padding = new RectOffset(18, 18, 14, 14);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            AddValueLabel(
                panel,
                "DebugMenuHeading",
                "DEBUG PLAYTEST MENU  |  F1 / ~ to toggle",
                26,
                DebugAccentColor,
                40.0f);
            _stateText = AddValueLabel(
                panel,
                "DebugMenuState",
                "DEBUG STATE [PLAYTEST ONLY] | waiting for runtime binding",
                18,
                Color.white,
                116.0f);
            _rawCoreText = AddValueLabel(
                panel,
                "DebugMenuRawCore",
                "Raw core channel: unavailable",
                17,
                new Color(0.72f, 0.90f, 0.95f, 1.0f),
                56.0f);
            _commandText = AddValueLabel(
                panel,
                "DebugMenuCommand",
                "Last command/result: none",
                17,
                new Color(0.82f, 0.88f, 0.95f, 1.0f),
                54.0f);
            _statusText = AddValueLabel(
                panel,
                "DebugMenuStatus",
                "DEBUG STATE | waiting for runtime binding",
                17,
                DebugAccentColor,
                52.0f);

            RectTransform timeRow = CreateRow("DebugMenuTimeRow", panel);
            AddActionButton(timeRow, DebugMenuActionV1.Pause, "Pause", delegate { Pause(); });
            AddActionButton(timeRow, DebugMenuActionV1.Resume, "Resume", delegate { Resume(); });
            AddActionButton(timeRow, DebugMenuActionV1.SingleStep, "Single step 100 ms", delegate { SingleStep(); });

            RectTransform playbackRow = CreateRow("DebugMenuPlaybackRow", panel);
            AddActionButton(playbackRow, DebugMenuActionV1.Playback1x, "1x", delegate { SetPlayback1x(); });
            AddActionButton(playbackRow, DebugMenuActionV1.Playback10x, "10x", delegate { SetPlayback10x(); });
            AddActionButton(playbackRow, DebugMenuActionV1.Playback60x, "60x", delegate { SetPlayback60x(); });

            RectTransform jumpRow = CreateRow("DebugMenuJumpRow", panel);
            AddActionButton(jumpRow, DebugMenuActionV1.AdvanceHour, "+1 hour", delegate { AdvanceOneHour(); });
            AddActionButton(jumpRow, DebugMenuActionV1.AdvanceDay, "+1 day", delegate { AdvanceOneDay(); });
            AddActionButton(
                jumpRow,
                DebugMenuActionV1.JumpEquilibrium,
                "Equilibrium-like jump",
                delegate { JumpToEquilibriumLikeState(); });

            RectTransform cheatRow = CreateRow("DebugMenuCheatRow", panel);
            AddActionButton(
                cheatRow,
                DebugMenuActionV1.Restart,
                "Restart practice",
                delegate { RestartPracticeSession(); });
            AddActionButton(
                cheatRow,
                DebugMenuActionV1.GrantFuel,
                "Grant +100 fuel",
                delegate { GrantFreshBundles(); });
            AddActionButton(
                cheatRow,
                DebugMenuActionV1.ClearPendingActions,
                "Clear pending",
                delegate { ClearPendingActions(); });
            AddActionButton(
                cheatRow,
                DebugMenuActionV1.ResetSyntheticResponse,
                "Reset response",
                delegate { ResetSyntheticResponse(); });

            RectTransform diagnosticsRow = CreateRow("DebugMenuDiagnosticsRow", panel);
            AddActionButton(
                diagnosticsRow,
                DebugMenuActionV1.CopyDigest,
                "Copy state digest",
                delegate { CopyStateDigest(); });

            _digestText = AddValueLabel(
                panel,
                "DebugMenuDigest",
                "Digest: unavailable",
                15,
                new Color(0.68f, 0.82f, 0.90f, 1.0f),
                76.0f);

            _isBuilt = true;
            SetControlAvailability(false);
            _overlayRoot.gameObject.SetActive(false);
        }

        public void ToggleVisibility()
        {
            if (!_isBuilt && GetComponent<Phase10ShellView>() != null)
            {
                EnsureVisuals();
            }

            if (_overlayRoot != null)
            {
                _overlayRoot.gameObject.SetActive(!_overlayRoot.gameObject.activeSelf);
            }
        }

        public void SetVisible(bool visible)
        {
            if (!_isBuilt && GetComponent<Phase10ShellView>() != null)
            {
                EnsureVisuals();
            }

            if (_overlayRoot != null)
            {
                _overlayRoot.gameObject.SetActive(visible);
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
                    "The Debug Menu requires a bound adapter with a snapshot.");
            }

            EnsureVisuals();
            Unsubscribe();
            _runtimeAdapter = runtimeAdapter;
            _runtimeAdapter.SnapshotChanged += HandleSnapshotChanged;
            _runtimeAdapter.CommandCompleted += HandleCommandCompleted;
            _hasCommandResult = false;
            _lastCommandSequence = 0;
            _lastCommandKind = "none";
            _lastCommandAccepted = false;
            _lastDiagnosticCode = string.Empty;
            Render(snapshot);
            SetControlAvailability(true);
            SetStatus("DEBUG STATE | bound to runtime adapter; commands are not scored.");
        }

        public void Unbind()
        {
            Unsubscribe();
            _runtimeAdapter = null;
            _snapshot = null;
            _hasCommandResult = false;
            _lastCommandSequence = 0;
            _lastCommandKind = "none";
            _lastCommandAccepted = false;
            _lastDiagnosticCode = string.Empty;
            ClearPresentation();
            SetControlAvailability(false);
        }

        private void OnDestroy()
        {
            Unbind();
        }

        public bool TryGetActionButton(DebugMenuActionV1 action, out Button button)
        {
            if (!_isBuilt && GetComponent<Phase10ShellView>() != null)
            {
                EnsureVisuals();
            }

            return _buttons.TryGetValue(action, out button);
        }

        public Phase8UnityCommandResultV1 Pause()
        {
            return Dispatch("Pause", delegate { return _runtimeAdapter.Pause(); });
        }

        public Phase8UnityCommandResultV1 Resume()
        {
            return Dispatch("Resume", delegate { return _runtimeAdapter.Resume(); });
        }

        /// <summary>
        /// Executes exactly one 100 ms wall tick and leaves the runtime paused.
        /// The explicit resume is required because a paused runtime treats an
        /// advance request as a no-op while preserving deterministic wall time.
        /// </summary>
        public Phase8UnityCommandResultV1 SingleStep()
        {
            if (_runtimeAdapter == null)
            {
                SetStatus("DEBUG STATE | Single step unavailable: runtime adapter is not bound.");
                return null;
            }

            Phase8UnityCommandResultV1 resume = Dispatch(
                "Single step resume",
                delegate { return _runtimeAdapter.Resume(); });
            if (resume == null || !resume.Accepted)
            {
                Dispatch("Single step final pause", delegate { return _runtimeAdapter.Pause(); });
                return resume;
            }

            Phase8UnityCommandResultV1 advance = Dispatch(
                "Single step advance",
                delegate { return _runtimeAdapter.AdvanceWallMilliseconds(SingleStepWallMilliseconds); });
            Phase8UnityCommandResultV1 pause = Dispatch(
                "Single step final pause",
                delegate { return _runtimeAdapter.Pause(); });

            if (advance != null && !advance.Accepted)
            {
                return advance;
            }

            SetStatus("DEBUG STATE | single-stepped one 100 ms tick; runtime is paused.");
            return pause == null ? advance : pause;
        }

        public Phase8UnityCommandResultV1 SetPlayback1x()
        {
            return SetPlayback(Playback1xModeId);
        }

        public Phase8UnityCommandResultV1 SetPlayback10x()
        {
            return SetPlayback(Playback10xModeId);
        }

        public Phase8UnityCommandResultV1 SetPlayback60x()
        {
            return SetPlayback(Playback60xModeId);
        }

        public Phase8UnityCommandResultV1 Playback1x()
        {
            return SetPlayback1x();
        }

        public Phase8UnityCommandResultV1 Playback10x()
        {
            return SetPlayback10x();
        }

        public Phase8UnityCommandResultV1 Playback60x()
        {
            return SetPlayback60x();
        }

        public Phase8UnityCommandResultV1 AdvanceOneHour()
        {
            return AdvanceSimulationSeconds(SimulationHourSeconds, "+1 hour");
        }

        public Phase8UnityCommandResultV1 AdvanceOneDay()
        {
            return AdvanceSimulationSeconds(SimulationDaySeconds, "+1 day");
        }

        public Phase8UnityCommandResultV1 AddHour()
        {
            return AdvanceOneHour();
        }

        public Phase8UnityCommandResultV1 AddDay()
        {
            return AdvanceOneDay();
        }

        public Phase8UnityCommandResultV1 JumpToEquilibriumLikeState()
        {
            return AdvanceSimulationSeconds(
                EquilibriumLikeJumpSeconds,
                "equilibrium-like jump");
        }

        public Phase8UnityCommandResultV1 JumpToEquilibrium()
        {
            return JumpToEquilibriumLikeState();
        }

        public Phase8UnityCommandResultV1 GrantFreshBundles()
        {
            return Dispatch(
                "Grant +100 fuel",
                delegate
                {
                    return _runtimeAdapter.Debug(
                        Phase8UnityDebugActionKindV1.GrantFreshBundles,
                        GrantFuelBundleCount);
                });
        }

        public Phase8UnityCommandResultV1 GrantFuel()
        {
            return GrantFreshBundles();
        }

        public Phase8UnityCommandResultV1 ClearPendingActions()
        {
            return Dispatch(
                "Clear pending actions",
                delegate
                {
                    return _runtimeAdapter.Debug(
                        Phase8UnityDebugActionKindV1.ClearPendingActions);
                });
        }

        public Phase8UnityCommandResultV1 ResetSyntheticResponse()
        {
            return Dispatch(
                "Reset synthetic response",
                delegate
                {
                    return _runtimeAdapter.Debug(
                        Phase8UnityDebugActionKindV1.ResetSyntheticResponse);
                });
        }

        /// <summary>
        /// Restarts only through the same-object controller. A standalone view
        /// still exposes the button but reports that the operation is absent.
        /// </summary>
        public bool RestartPracticeSession()
        {
            UnityGameController controller = GetComponent<UnityGameController>();
            if (controller == null)
            {
                SetStatus("DEBUG STATE | restart unavailable: no same-object UnityGameController.");
                return false;
            }

            try
            {
                bool restarted = controller.RestartPracticeSession();
                SetStatus(
                    restarted
                        ? "DEBUG STATE | practice session restarted; debug state is not scored."
                        : "DEBUG STATE | restart unavailable: controller is not initialized.");
                return restarted;
            }
            catch (Exception exception)
            {
                SetStatus("DEBUG STATE | restart unavailable: " + exception.Message);
                return false;
            }
        }

        public bool CopyStateDigest()
        {
            string digest = DigestText;
            if (string.IsNullOrWhiteSpace(digest) ||
                string.Equals(digest, "Digest: unavailable", StringComparison.Ordinal))
            {
                SetStatus("DEBUG STATE | state digest unavailable until a runtime is bound.");
                return false;
            }

            GUIUtility.systemCopyBuffer = digest;
            SetStatus("DEBUG STATE | copied deterministic state digest to clipboard.");
            return true;
        }

        public bool CopyDigest()
        {
            return CopyStateDigest();
        }

        private Phase8UnityCommandResultV1 SetPlayback(string playbackModeId)
        {
            return Dispatch(
                "Set playback " + playbackModeId,
                delegate { return _runtimeAdapter.SetPlaybackMode(playbackModeId); });
        }

        private Phase8UnityCommandResultV1 AdvanceSimulationSeconds(
            double simulationSeconds,
            string actionLabel)
        {
            if (_runtimeAdapter == null)
            {
                SetStatus("DEBUG STATE | " + actionLabel + " unavailable: runtime adapter is not bound.");
                return null;
            }

            Phase8UnityPresentationSnapshotV1 snapshot = _runtimeAdapter.Snapshot ?? _snapshot;
            if (snapshot == null ||
                snapshot.AccelerationFactor <= 0.0 ||
                double.IsNaN(snapshot.AccelerationFactor) ||
                double.IsInfinity(snapshot.AccelerationFactor) ||
                snapshot.WallControlTickMilliseconds == 0)
            {
                SetStatus("DEBUG STATE | " + actionLabel + " unavailable: runtime pacing is invalid.");
                return null;
            }

            ulong wallMilliseconds = ConvertSimulationSecondsToWallMilliseconds(
                simulationSeconds,
                snapshot.AccelerationFactor,
                snapshot.WallControlTickMilliseconds);
            bool wasPaused = snapshot.IsPaused;
            if (wasPaused)
            {
                Phase8UnityCommandResultV1 resume = Dispatch(
                    actionLabel + " resume",
                    delegate { return _runtimeAdapter.Resume(); });
                if (resume == null || !resume.Accepted)
                {
                    Dispatch(
                        actionLabel + " final pause",
                        delegate { return _runtimeAdapter.Pause(); });
                    return resume;
                }
            }

            Phase8UnityCommandResultV1 advance = Dispatch(
                actionLabel + " advance",
                delegate { return _runtimeAdapter.AdvanceWallMilliseconds(wallMilliseconds); });
            if (!wasPaused)
            {
                return advance;
            }

            Phase8UnityCommandResultV1 pause = Dispatch(
                actionLabel + " final pause",
                delegate { return _runtimeAdapter.Pause(); });
            if (advance != null && !advance.Accepted)
            {
                return advance;
            }

            return pause == null ? advance : pause;
        }

        private Phase8UnityCommandResultV1 Dispatch(
            string actionLabel,
            Func<Phase8UnityCommandResultV1> command)
        {
            if (_runtimeAdapter == null)
            {
                SetStatus("DEBUG STATE | " + actionLabel + " unavailable: runtime adapter is not bound.");
                return null;
            }

            try
            {
                Phase8UnityCommandResultV1 result = command();
                if (result == null)
                {
                    SetStatus("DEBUG STATE | " + actionLabel + " rejected: adapter returned no result.");
                }

                return result;
            }
            catch (Exception exception)
            {
                SetStatus("DEBUG STATE | " + actionLabel + " rejected before dispatch: " + exception.Message);
                return null;
            }
        }

        private void HandleSnapshotChanged(Phase8UnityPresentationSnapshotV1 snapshot)
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
                _hasCommandResult = false;
                _commandText.text = "Last command/result: adapter returned no result.";
                SetStatus("DEBUG STATE | command rejected: adapter returned no result.");
                UpdateDigest();
                return;
            }

            _hasCommandResult = true;
            _lastCommandSequence = result.Sequence;
            _lastCommandKind = result.Kind.ToString();
            _lastCommandAccepted = result.Accepted;
            _lastDiagnosticCode = result.DiagnosticCode ?? string.Empty;
            _commandText.text = FormatCommandResult(result);
            SetStatus("DEBUG STATE | " + FormatCommandResult(result));
            UpdateDigest();
        }

        private void Render(Phase8UnityPresentationSnapshotV1 snapshot)
        {
            _snapshot = snapshot;
            int channelIndex;
            GameChannelPresentationSnapshot channel;
            TryGetPreferredChannel(snapshot, out channelIndex, out channel);

            string selectedChannel = channel == null
                ? "none"
                : channelIndex.ToString(CultureInfo.InvariantCulture);
            _stateText.text =
                "DEBUG STATE [PLAYTEST ONLY]\n" +
                "Seed: " + snapshot.Seed.ToString(CultureInfo.InvariantCulture) +
                " | Scenario: " + snapshot.ScenarioId +
                " | " + (snapshot.IsPaused ? "PAUSED" : "RUNNING") + "\n" +
                "Time: " + Format(snapshot.SimulationTimeSeconds) + " s sim / " +
                Format(snapshot.WallElapsedSeconds) + " s wall" +
                " | Playback: " + snapshot.PlaybackModeId + " (x" +
                Format(snapshot.AccelerationFactor) + ")\n" +
                "Power: " + FormatPercent(snapshot.NormalizedPowerFraction) +
                " | Tilt: " + FormatPercent(snapshot.AbsoluteTiltFraction) +
                " | Score: " + Format(snapshot.ScoreTotal) +
                " | Inventory: " + Format(snapshot.FreshBundlesAvailable) +
                " fresh | Pending: " + Format(snapshot.PendingActionCount) +
                " | Channel: " + selectedChannel +
                "\nPhysics: " + snapshot.Core.Physics.SourceId +
                " / P=" + Format(snapshot.Core.Physics.TotalPowerWatts) + " W" +
                " / k=" + FormatPrecise(snapshot.Core.Physics.EffectiveK) +
                " / rho=" + FormatPrecise(snapshot.Core.Physics.Reactivity * 1000.0) + " mk" +
                " / solver=" + snapshot.Core.Physics.SolverIdentity +
                " / it=" + snapshot.Core.Physics.SolverIterationCount +
                " / " + snapshot.Core.Physics.SolveState;

            _rawCoreText.text = FormatRawCore(channelIndex, channel);
            _commandText.text = _hasCommandResult
                ? _commandText.text
                : "Last command/result: none";
            UpdateDigest();
        }

        private void TryGetPreferredChannel(
            Phase8UnityPresentationSnapshotV1 snapshot,
            out int channelIndex,
            out GameChannelPresentationSnapshot channel)
        {
            channelIndex = -1;
            channel = null;
            if (snapshot == null || snapshot.Core == null)
            {
                return;
            }

            CoreMapView coreMap = GetComponent<CoreMapView>();
            if (coreMap != null && coreMap.SelectedChannelIndex >= 0 &&
                coreMap.SelectedChannelIndex < snapshot.Core.Channels.Count)
            {
                channelIndex = coreMap.SelectedChannelIndex;
            }
            else if (snapshot.LastRefuelledChannel >= 0 &&
                     snapshot.LastRefuelledChannel < snapshot.Core.Channels.Count)
            {
                channelIndex = snapshot.LastRefuelledChannel;
            }

            if (channelIndex >= 0)
            {
                channel = snapshot.Core.Channels[channelIndex];
            }
        }

        private string FormatRawCore(
            int channelIndex,
            GameChannelPresentationSnapshot channel)
        {
            if (channel == null)
            {
                return "Raw core channel: unavailable (no selected or last-refuelled channel).";
            }

            StringBuilder builder = new StringBuilder(180);
            builder.Append("Raw core ch ")
                .Append(channelIndex.ToString(CultureInfo.InvariantCulture))
                .Append(": localPower=")
                .Append(FormatPrecise(channel.LocalPowerFraction))
                .Append(" localTilt=")
                .Append(FormatPrecise(channel.LocalTiltFraction))
                .Append(" powerW=")
                .Append(FormatPrecise(channel.PowerWatts))
                .Append(" avgBurnup=")
                .Append(FormatPrecise(channel.AverageBurnupMwDayPerKg))
                .Append(" MWd/kg HM");
            if (channel.Bundles != null && channel.Bundles.Count > 0)
            {
                GameBundlePresentationSnapshot bundle = channel.Bundles[0];
                builder.Append(" b0=")
                    .Append(bundle.BundleId)
                    .Append("/")
                    .Append(FormatPrecise(bundle.CurrentBurnupMwDayPerKg))
                    .Append(" MWd/kg");
            }

            return builder.ToString();
        }

        private string FormatCommandResult(Phase8UnityCommandResultV1 result)
        {
            string disposition = result.Accepted ? "accepted" : "rejected";
            string suffix = result.Accepted
                ? result.Message
                : (string.IsNullOrWhiteSpace(result.DiagnosticCode)
                    ? result.DiagnosticMessage
                    : "[" + result.DiagnosticCode + "] " + result.DiagnosticMessage);
            return "Last command/result: " + result.Kind + " " + disposition +
                " (#" + result.Sequence.ToString(CultureInfo.InvariantCulture) + ")" +
                (string.IsNullOrWhiteSpace(suffix) ? string.Empty : " " + suffix);
        }

        private void UpdateDigest()
        {
            if (_digestText == null)
            {
                return;
            }

            if (_snapshot == null)
            {
                _digestText.text = "Digest: unavailable";
                return;
            }

            int channelIndex;
            GameChannelPresentationSnapshot channel;
            TryGetPreferredChannel(_snapshot, out channelIndex, out channel);
            _digestText.text = BuildDigest(_snapshot, channelIndex, channel);
        }

        private string BuildDigest(
            Phase8UnityPresentationSnapshotV1 snapshot,
            int channelIndex,
            GameChannelPresentationSnapshot channel)
        {
            StringBuilder builder = new StringBuilder(320);
            builder.Append("seed=")
                .Append(snapshot.Seed.ToString(CultureInfo.InvariantCulture))
                .Append(";scenario=")
                .Append(snapshot.ScenarioId ?? string.Empty)
                .Append(";time=")
                .Append(FormatPrecise(snapshot.SimulationTimeSeconds))
                .Append(";wall=")
                .Append(FormatPrecise(snapshot.WallElapsedSeconds))
                .Append(";mode=")
                .Append(snapshot.PlaybackModeId ?? string.Empty)
                .Append(";paused=")
                .Append(snapshot.IsPaused ? "1" : "0")
                .Append(";power=")
                .Append(FormatPrecise(snapshot.NormalizedPowerFraction))
                .Append(";tilt=")
                .Append(FormatPrecise(snapshot.AbsoluteTiltFraction))
                .Append(";score=")
                .Append(FormatPrecise(snapshot.ScoreTotal))
                .Append(";fresh=")
                .Append(snapshot.FreshBundlesAvailable.ToString(CultureInfo.InvariantCulture))
                .Append(";pending=")
                .Append(snapshot.PendingActionCount.ToString(CultureInfo.InvariantCulture))
                .Append(";channel=");

            if (channel == null)
            {
                builder.Append("none");
            }
            else
            {
                builder.Append(channelIndex.ToString(CultureInfo.InvariantCulture))
                    .Append(";rawPower=")
                    .Append(FormatPrecise(channel.LocalPowerFraction))
                    .Append(";powerW=")
                    .Append(FormatPrecise(channel.PowerWatts))
                    .Append(";rawTilt=")
                    .Append(FormatPrecise(channel.LocalTiltFraction))
                    .Append(";burnup=")
                    .Append(FormatPrecise(channel.AverageBurnupMwDayPerKg));
                if (channel.Bundles != null && channel.Bundles.Count > 0)
                {
                    builder.Append(";b0=")
                        .Append(channel.Bundles[0].BundleId ?? string.Empty)
                        .Append("/")
                        .Append(FormatPrecise(channel.Bundles[0].CurrentBurnupMwDayPerKg));
                }
            }

            builder.Append(";lastRefuel=")
                .Append(snapshot.LastRefuelledChannel.ToString(CultureInfo.InvariantCulture))
                .Append("/")
                .Append(snapshot.LastRefuellingDirectionId ?? string.Empty)
                .Append("/")
                .Append(snapshot.LastRefuellingShiftCount.ToString(CultureInfo.InvariantCulture))
                .Append(";cmd=");
            if (!_hasCommandResult)
            {
                builder.Append("none");
            }
            else
            {
                builder.Append(_lastCommandKind)
                    .Append("#")
                    .Append(_lastCommandSequence.ToString(CultureInfo.InvariantCulture))
                    .Append(_lastCommandAccepted ? ":ok" : ":rejected");
                if (!string.IsNullOrWhiteSpace(_lastDiagnosticCode))
                {
                    builder.Append("[").Append(_lastDiagnosticCode).Append("]");
                }
            }

            return builder.ToString();
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

            _stateText.text = "DEBUG STATE [PLAYTEST ONLY] | waiting for runtime binding";
            _rawCoreText.text = "Raw core channel: unavailable";
            _commandText.text = "Last command/result: none";
            _statusText.text = "DEBUG STATE | waiting for runtime binding";
            _digestText.text = "Digest: unavailable";
        }

        private void SetControlAvailability(bool isAvailable)
        {
            foreach (Button button in _buttons.Values)
            {
                button.interactable = isAvailable;
            }
        }

        private void SetStatus(string status)
        {
            if (_statusText != null)
            {
                _statusText.text = status;
            }
        }

        private Button AddActionButton(
            RectTransform parent,
            DebugMenuActionV1 action,
            string labelText,
            Action callback)
        {
            RectTransform buttonRoot = CreateRect("Button." + action, parent);
            LayoutElement buttonLayout = buttonRoot.gameObject.AddComponent<LayoutElement>();
            buttonLayout.minHeight = 58.0f;
            buttonLayout.preferredHeight = 58.0f;
            buttonLayout.minWidth = 128.0f;
            buttonLayout.preferredWidth = 164.0f;
            buttonLayout.flexibleWidth = 1.0f;

            Image image = buttonRoot.gameObject.AddComponent<Image>();
            image.color = ButtonColor;
            image.raycastTarget = true;

            Button button = buttonRoot.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = ButtonHighlightColor;
            colors.pressedColor = ButtonPressedColor;
            colors.selectedColor = ButtonHighlightColor;
            button.colors = colors;

            Text text = AddValueLabel(
                buttonRoot,
                "Label",
                labelText,
                17,
                Color.white,
                58.0f);
            SetAnchors(text.rectTransform, Vector2.zero, Vector2.one);
            SetOffsets(text.rectTransform, 8.0f, 0.0f, 8.0f, 0.0f);
            button.onClick.AddListener(delegate { callback(); });
            _buttons.Add(action, button);
            return button;
        }

        private static RectTransform CreateRow(string name, RectTransform parent)
        {
            RectTransform row = CreateRect(name, parent);
            LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
            rowLayout.minHeight = 58.0f;
            rowLayout.preferredHeight = 58.0f;

            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8.0f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return row;
        }

        private static Text AddValueLabel(
            RectTransform parent,
            string name,
            string value,
            int fontSize,
            Color color,
            float height)
        {
            RectTransform labelRoot = CreateRect(name, parent);
            LayoutElement layout = labelRoot.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;

            Text label = labelRoot.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAnchor.MiddleLeft;
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

        private static ulong ConvertSimulationSecondsToWallMilliseconds(
            double simulationSeconds,
            double accelerationFactor,
            uint wallControlTickMilliseconds)
        {
            double requestedWallMilliseconds = simulationSeconds * 1000.0 / accelerationFactor;
            double tickCount = Math.Round(
                requestedWallMilliseconds / wallControlTickMilliseconds,
                MidpointRounding.AwayFromZero);
            if (tickCount < 1.0)
            {
                tickCount = 1.0;
            }

            double roundedWallMilliseconds = tickCount * wallControlTickMilliseconds;
            if (roundedWallMilliseconds >= ulong.MaxValue)
            {
                return ulong.MaxValue - (ulong.MaxValue % wallControlTickMilliseconds);
            }

            return (ulong)roundedWallMilliseconds;
        }

        private static string GetText(Text text)
        {
            return text == null ? string.Empty : text.text;
        }

        private static string Format(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatPrecise(double value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string FormatPercent(double value)
        {
            return value.ToString("0.0%", CultureInfo.InvariantCulture);
        }
    }
}
