import { useCallback, useEffect, useMemo, useReducer, useRef, useState } from "react";
import type { CSSProperties, FormEvent, ReactNode } from "react";
import {
  AUTHORITATIVE_WASM_UNAVAILABLE_MESSAGE,
  createCanduPlaytestBridge,
  type CanduPlaytestBridgeLifecycle,
} from "./bridge";
import { CoreScene } from "./components/CoreScene";
import {
  BASE_CLOCK_SIMULATION_SECONDS_PER_WALL_SECOND,
  BASE_CLOCK_WALL_SECONDS_PER_SIMULATION_HOUR,
  type CanduChannelSnapshot,
  type CanduCommand,
  type CanduCommandResponse,
  type CanduEvent,
  type CanduSnapshot,
  type PlaybackModeId,
  type RefuelRequest,
} from "./protocol";
import {
  createInitialUiState,
  playtestUiReducer,
  type ConsoleMode,
  type CoreViewMode,
  type PlaytestUiState,
} from "./reducer";
import {
  createReplayArchive,
  downloadReplayArchive,
  loadNote,
  loadReplayArchive,
  NOTE_STORAGE_KEY,
  replayArchive,
  REPLAY_STORAGE_KEY,
  saveNote,
  saveReplayArchive,
  serializeReplayForDownload,
  stableDigest,
} from "./replay";
import {
  formatClockDuration,
  formatSimulationTime,
  formatSignedNumber,
  getFlowArrow,
  getFlowDirectionLabel,
  getChannelBand,
  getHeatColor,
  getOverallStatus,
  getPowerLabel,
  getTiltLabel,
  formatPowerWatts,
  formatReactivity,
} from "./visuals";

const bridge = createCanduPlaytestBridge();
const LIVE_CLOCK_WALL_INTERVAL_MS = 1_000;

export default function App() {
  const [uiState, uiDispatch] = useReducer(
    playtestUiReducer,
    undefined,
    () => createInitialUiState(bridge.getSnapshot(), bridge.status),
  );
  const [feedbackNote, setFeedbackNote] = useState(() => loadNote());
  const [commandError, setCommandError] = useState("");
  const [copyStatus, setCopyStatus] = useState("");
  const [isCommandPending, setIsCommandPending] = useState(false);
  const pendingCountRef = useRef(0);
  const backgroundCommandInFlightRef = useRef(false);

  useEffect(() => {
    const lifecycle = bridge as CanduPlaytestBridgeLifecycle;
    const unsubscribe = lifecycle.subscribe((bridgeStatus, nextSnapshot) => {
      uiDispatch({ type: "bridge-state", bridgeStatus, snapshot: nextSnapshot });
    });
    uiDispatch({ type: "bridge-state", bridgeStatus: bridge.status, snapshot: bridge.getSnapshot() });
    return unsubscribe;
  }, []);

  const snapshot = uiState.snapshot;
  const selectedChannel = snapshot.core.channels[uiState.selectedChannelIndex] ?? snapshot.core.channels[0];
  const overallStatus = getOverallStatus(snapshot);
  const stateDigest = useMemo(
    () =>
      stableDigest({
        protocol: snapshot.protocol,
        source: snapshot.source,
        sequence: snapshot.sequence,
        simulationTimeSeconds: snapshot.simulationTimeSeconds,
        normalizedPowerFraction: snapshot.normalizedPowerFraction,
        absoluteTiltFraction: snapshot.absoluteTiltFraction,
        scoreTotal: snapshot.scoreTotal,
        freshBundlesAvailable: snapshot.freshBundlesAvailable,
        selectedChannelIndex: selectedChannel?.channelIndex,
        selectedChannel: selectedChannel
          ? {
              localPowerFraction: selectedChannel.localPowerFraction,
              localTiltFraction: selectedChannel.localTiltFraction,
              bundles: selectedChannel.bundles.map((bundle) => [bundle.bundleId, bundle.stateVersion, bundle.currentBurnupMwdPerKg]),
            }
          : null,
      }),
    [selectedChannel, snapshot],
  );

  const sendCommand = useCallback(
    async (command: CanduCommand, record = true, showPending = true): Promise<CanduCommandResponse | null> => {
      if (showPending) {
        pendingCountRef.current += 1;
        setIsCommandPending(true);
      }
      setCommandError("");
      try {
        const response = await bridge.dispatch(command);
        uiDispatch({ type: "command-result", response, record });
        return response;
      } catch (error) {
        const message = error instanceof Error ? error.message : "The browser bridge returned an unknown error.";
        if (showPending) {
          setCommandError(message);
        }
        return null;
      } finally {
        if (showPending) {
          pendingCountRef.current -= 1;
          if (pendingCountRef.current === 0) {
            setIsCommandPending(false);
          }
        }
      }
    },
    [],
  );

  useEffect(() => {
    if (snapshot.isPaused || snapshot.playbackModeId === "pause") {
      return;
    }

    const timer = window.setInterval(() => {
      if (pendingCountRef.current === 0 && !backgroundCommandInFlightRef.current) {
        backgroundCommandInFlightRef.current = true;
        void sendCommand({ type: "advance", wallMilliseconds: LIVE_CLOCK_WALL_INTERVAL_MS }, false, false)
          .finally(() => {
            backgroundCommandInFlightRef.current = false;
          });
      }
    }, LIVE_CLOCK_WALL_INTERVAL_MS);
    return () => window.clearInterval(timer);
  }, [sendCommand, snapshot.isPaused, snapshot.playbackModeId]);

  useEffect(() => {
    saveNote(feedbackNote);
  }, [feedbackNote]);

  const selectChannel = useCallback((channelIndex: number) => {
    uiDispatch({ type: "select-channel", channelIndex });
    uiDispatch({ type: "clear-preview" });
  }, []);

  const queuePowerTarget = useCallback((targetFraction: number) => {
    void sendCommand({ type: "queue-power-target", targetFraction });
  }, [sendCommand]);

  const queueTiltTarget = useCallback((targetFraction: number) => {
    void sendCommand({ type: "queue-tilt-target", targetFraction });
  }, [sendCommand]);

  const setPlayback = useCallback((modeId: PlaybackModeId) => {
    void sendCommand({ type: "set-playback-mode", modeId });
  }, [sendCommand]);

  const stepSimulation = useCallback((hours: number) => {
    void sendCommand({ type: "step", simulationSeconds: hours * 3600 });
  }, [sendCommand]);

  const previewRefuel = useCallback((request: RefuelRequest) => {
    void sendCommand({ type: "preview-refuel", request });
  }, [sendCommand]);

  const commitRefuel = useCallback((request: RefuelRequest) => {
    void sendCommand({ type: "commit-refuel", request });
  }, [sendCommand]);

  const changeMode = useCallback((mode: ConsoleMode) => {
    uiDispatch({ type: "set-mode", mode });
    void bridge.initializeMode(mode)
      .then((nextSnapshot) => {
        uiDispatch({ type: "bridge-state", bridgeStatus: bridge.status, snapshot: nextSnapshot });
      })
      .catch((error: unknown) => {
        setCommandError(error instanceof Error ? error.message : "The browser bridge could not change modes.");
      });
  }, []);

  const saveReplay = useCallback(() => {
    const archive = createReplayArchive(uiState.history, uiState.snapshot.source);
    const saved = saveReplayArchive(archive);
    uiDispatch({
      type: "set-replay-status",
      status: saved ? `Saved ${archive.commands.length} commands to local replay storage.` : "Local replay storage is unavailable in this browser.",
    });
  }, [uiState.history]);

  const downloadReplay = useCallback(() => {
    const archive = createReplayArchive(uiState.history, uiState.snapshot.source);
    downloadReplayArchive(archive);
    uiDispatch({ type: "set-replay-status", status: `Downloaded ${archive.commands.length} commands as JSON.` });
  }, [uiState.history]);

  const replaySaved = useCallback(async () => {
    let archive;
    try {
      archive = loadReplayArchive();
    } catch (error) {
      const message = error instanceof Error ? error.message : "Saved replay JSON is invalid.";
      uiDispatch({ type: "set-replay-status", status: message });
      return;
    }
    if (archive === null) {
      uiDispatch({ type: "set-replay-status", status: "No saved replay is available yet." });
      return;
    }

    setIsCommandPending(true);
    setCommandError("");
    try {
      const resetResponse = await bridge.dispatch({ type: "reset" });
      uiDispatch({ type: "command-result", response: resetResponse, record: false });
      const count = await replayArchive(bridge, archive, (_record, response) => {
      uiDispatch({ type: "command-result", response, record: false });
      });
      uiDispatch({ type: "set-replay-status", status: `Replayed ${count} commands from local storage.` });
    } catch (error) {
      const message = error instanceof Error ? error.message : "Replay failed in the browser bridge.";
      setCommandError(message);
    } finally {
      setIsCommandPending(false);
    }
  }, []);

  const copyDigest = useCallback(async () => {
    try {
      await navigator.clipboard.writeText(stateDigest);
      setCopyStatus("Digest copied");
    } catch {
      setCopyStatus("Copy unavailable");
    }
    window.setTimeout(() => setCopyStatus(""), 2200);
  }, [stateDigest]);

  const resetRun = useCallback(() => {
    void sendCommand({ type: "reset" });
  }, [sendCommand]);

  if (selectedChannel === undefined) {
    return <div className="fatal-state">Core snapshot did not contain a selectable channel.</div>;
  }

  const bridgeInteractive = uiState.bridgeStatus.source === "wasm" || uiState.bridgeStatus.source === "synthetic-fixture";
  const commandControlsDisabled = isCommandPending || !bridgeInteractive;

  return (
    <div className="app-shell" data-bridge-status={uiState.bridgeStatus.source}>
      <Sidebar mode={uiState.mode} onChangeMode={changeMode} snapshot={snapshot} bridgeInteractive={bridgeInteractive} />
      <main className="console-main">
        <TopBar bridgeStatus={uiState.bridgeStatus} snapshot={snapshot} overallStatus={overallStatus} />
        <section className="hero-row" aria-labelledby="page-title">
          <div>
            <p className="eyebrow">STEADY-STATE CANDU / MILESTONE 0.5</p>
            <h1 id="page-title">Keep the core in balance.</h1>
            <p className="hero-copy">
              Inspect the flux surface, choose a channel, and use on-power refuelling to keep the operating envelope quiet.
            </p>
          </div>
          <div className="hero-session-card">
            <div className="hero-session-line">
              <span className={bridgeInteractive ? "status-dot is-live" : "status-dot is-warm"} aria-hidden="true" />
              <span>{bridgeInteractive ? "Practice session live" : "Playtest unavailable"}</span>
            </div>
            <strong>{formatSimulationTime(snapshot.simulationTimeSeconds)}</strong>
            <span>wall {formatClockDuration(snapshot.wallElapsedSeconds)} · seed 1001</span>
          </div>
        </section>

        <section className="metric-grid" aria-label="Reactor summary">
          <MetricCard
            label="Reactor power"
            value={formatPowerWatts(snapshot.physics.totalPowerWatts)}
            detail={`${getPowerLabel(snapshot.physics.actualPowerFraction)} actual · setpoint ${formatPowerWatts(snapshot.physics.referencePowerWatts * snapshot.targetPowerFraction)}`}
            indicator={powerIndicator(snapshot)}
            tone={overallStatus === "attention" ? "warning" : "cyan"}
          />
          <MetricCard
            label="Axial tilt"
            value={getTiltLabel(snapshot.absoluteTiltFraction)}
            detail={`target ${getTiltLabel(snapshot.targetTiltFraction)}`}
            indicator={snapshot.absoluteTiltFraction >= 0 ? "upper half > lower" : "lower half > upper"}
            tone="violet"
          />
          <MetricCard
            label="State reactivity"
            value={formatReactivity(snapshot.physics.reactivity)}
            detail={`k ${snapshot.physics.effectiveK.toFixed(5)} · ${snapshot.physics.solverIterationCount} outer iterations`}
            indicator={snapshot.physics.isAuthoritative ? "full-core diffusion · state-level" : "compatibility projection"}
            tone="violet"
          />
          <MetricCard
            label="Run score"
            value={snapshot.scoreTotal.toFixed(0)}
            detail={`${snapshot.scoreDelta >= 0 ? "+" : ""}${snapshot.scoreDelta.toFixed(1)} this update`}
            indicator="stability + utilization"
            tone="amber"
          />
          <MetricCard
            label="Fresh reserve"
            value={`${snapshot.freshBundlesAvailable}`}
            detail="bundles available"
            indicator={`${snapshot.refuellingOperationCount} shifts committed`}
            tone="green"
          />
        </section>

        <div
          className="source-banner"
          data-bridge-source={uiState.bridgeStatus.source}
          data-authoritative-bridge={uiState.bridgeStatus.source === "wasm" ? "active" : "inactive"}
          role="status"
        >
          <span className="source-banner-label">
            <span className="status-dot is-warm" aria-hidden="true" />
            {uiState.bridgeStatus.title}
          </span>
          <span>{uiState.bridgeStatus.detail}.</span>
          <span className="source-banner-physics">{snapshot.physics.isAuthoritative ? "SPATIAL SOLVE" : "REDUCED MODEL"} · {snapshot.physics.sourceId} · {snapshot.physics.solverIdentity}</span>
          <span className="source-banner-protocol">{snapshot.protocol}</span>
        </div>

        {uiState.bridgeStatus.source === "unavailable" ? (
          <div className="bridge-unavailable" role="alert">
            <strong>{AUTHORITATIVE_WASM_UNAVAILABLE_MESSAGE}</strong>
            <span>Production playtest controls are disabled until the C#/.NET browser bridge is available.</span>
          </div>
        ) : null}

        {uiState.mode === "play" ? (
          <PlayWorkspace
            snapshot={snapshot}
            selectedChannel={selectedChannel}
            selectedChannelIndex={uiState.selectedChannelIndex}
            coreViewMode={uiState.coreViewMode}
            preview={uiState.preview}
            history={uiState.history}
            feedbackNote={feedbackNote}
            isCommandPending={commandControlsDisabled}
            commandError={commandError}
            onSelectChannel={selectChannel}
            onChangeCoreView={(viewMode) => uiDispatch({ type: "set-core-view", viewMode })}
            onPreviewRefuel={previewRefuel}
            onCommitRefuel={commitRefuel}
            onSetPlayback={setPlayback}
            onStepSimulation={stepSimulation}
            onQueuePowerTarget={queuePowerTarget}
            onQueueTiltTarget={queueTiltTarget}
            onChangeNote={setFeedbackNote}
          />
        ) : (
          <LabWorkspace
            snapshot={snapshot}
            selectedChannel={selectedChannel}
            selectedChannelIndex={uiState.selectedChannelIndex}
            coreViewMode={uiState.coreViewMode}
            history={uiState.history}
            stateDigest={stateDigest}
            replayStatus={uiState.replayStatus}
            copyStatus={copyStatus}
            isCommandPending={commandControlsDisabled}
            onSelectChannel={selectChannel}
            onChangeCoreView={(viewMode) => uiDispatch({ type: "set-core-view", viewMode })}
            onCopyDigest={copyDigest}
            onSaveReplay={saveReplay}
            onDownloadReplay={downloadReplay}
            onReplaySaved={replaySaved}
            onClearHistory={() => uiDispatch({ type: "clear-history" })}
            onResetRun={resetRun}
          />
        )}

        {commandError ? (
          <div className="command-error" role="alert">
            <strong>Bridge error.</strong> {commandError}
          </div>
        ) : null}

        <footer className="app-footer">
          <span>candu-playtest-v1 · static browser client</span>
          <span>Unity remains the authoritative desktop presentation seam.</span>
        </footer>
      </main>
    </div>
  );
}

interface SidebarProps {
  mode: ConsoleMode;
  snapshot: CanduSnapshot;
  bridgeInteractive: boolean;
  onChangeMode: (mode: ConsoleMode) => void;
}

function Sidebar({ mode, snapshot, bridgeInteractive, onChangeMode }: SidebarProps) {
  return (
    <aside className="sidebar" aria-label="Playtest navigation">
      <div className="brand-lockup">
        <div className="brand-mark" aria-hidden="true">
          <span />
          <span />
          <span />
        </div>
        <div>
          <div className="brand-name">CANDU</div>
          <div className="brand-subtitle">PLAYTEST CONSOLE</div>
        </div>
      </div>

      <div className="sidebar-rule" />
      <nav className="mode-nav" aria-label="Console mode">
        <p className="sidebar-label">Workspace</p>
        <button className={mode === "play" ? "nav-button is-active" : "nav-button"} type="button" onClick={() => onChangeMode("play")} aria-current={mode === "play" ? "page" : undefined} disabled={!bridgeInteractive}>
          <span className="nav-icon" aria-hidden="true">◈</span>
          <span>
            <strong>Play</strong>
            <small>Operate the core</small>
          </span>
          {mode === "play" ? <span className="nav-pulse" aria-hidden="true" /> : null}
        </button>
        <button className={mode === "lab" ? "nav-button is-active" : "nav-button"} type="button" onClick={() => onChangeMode("lab")} aria-current={mode === "lab" ? "page" : undefined} disabled={!bridgeInteractive}>
          <span className="nav-icon" aria-hidden="true">⌘</span>
          <span>
            <strong>Lab</strong>
            <small>Inspect the bridge</small>
          </span>
          {mode === "lab" ? <span className="nav-pulse" aria-hidden="true" /> : null}
        </button>
      </nav>

      <div className="sidebar-session">
        <p className="sidebar-label">Current run</p>
        <div className="sidebar-session-card">
          <div className="sidebar-session-header">
            <span className="status-dot is-live" aria-hidden="true" />
            <span>Practice</span>
            <span className="session-seed">#1001</span>
          </div>
          <div className="sidebar-session-time">{formatSimulationTime(snapshot.simulationTimeSeconds)}</div>
          <div className="sidebar-session-meta">{snapshot.refuellingOperationCount} refuelling shifts</div>
        </div>
      </div>

      <div className="sidebar-bottom">
        <div className="mini-system-status">
          <span className="status-dot is-warm" aria-hidden="true" />
          <div>
            <span>System status</span>
            <strong>{bridgeInteractive ? "Nominal" : "Bridge offline"}</strong>
          </div>
        </div>
        <p className="sidebar-hint">Use Grid Map for full keyboard channel selection.</p>
      </div>
    </aside>
  );
}

interface TopBarProps {
  bridgeStatus: PlaytestUiState["bridgeStatus"];
  snapshot: CanduSnapshot;
  overallStatus: "stable" | "watch" | "attention";
}

function TopBar({ bridgeStatus, snapshot, overallStatus }: TopBarProps) {
  const statusCopy = overallStatus === "stable" ? "Operating envelope stable" : overallStatus === "watch" ? "Watch axial response" : "Attention required";
  const sourceCopy = bridgeStatus.source === "wasm" ? "WASM LINK" : bridgeStatus.source === "synthetic-fixture" ? "SYNTHETIC DATA" : bridgeStatus.source === "loading" ? "BRIDGE LOADING" : "BRIDGE OFFLINE";
  return (
    <header className="topbar">
      <div className="breadcrumb"><span>WORKSPACE</span><span className="breadcrumb-slash">/</span><strong>LIVE PLAYTEST</strong></div>
      <div className="topbar-actions">
        <span className={`envelope-status is-${overallStatus}`}><span className="status-dot" aria-hidden="true" />{statusCopy}</span>
        <span className="topbar-divider" aria-hidden="true" />
        <span className="topbar-source"><span className="status-dot is-warm" aria-hidden="true" />{sourceCopy}</span>
        <span className="topbar-time">{formatSimulationTime(snapshot.simulationTimeSeconds)}</span>
      </div>
    </header>
  );
}

interface MetricCardProps {
  label: string;
  value: string;
  detail: string;
  indicator: string;
  tone: "cyan" | "violet" | "amber" | "green" | "warning";
}

function MetricCard({ label, value, detail, indicator, tone }: MetricCardProps) {
  return (
    <article className={`metric-card tone-${tone}`}>
      <div className="metric-card-top"><span>{label}</span><span className="metric-card-symbol" aria-hidden="true">↗</span></div>
      <div className="metric-card-value">{value}</div>
      <div className="metric-card-detail">{detail}</div>
      <div className="metric-card-indicator"><span className="metric-track"><span className="metric-track-fill" /></span>{indicator}</div>
    </article>
  );
}

interface PlayWorkspaceProps {
  snapshot: CanduSnapshot;
  selectedChannel: CanduChannelSnapshot;
  selectedChannelIndex: number;
  coreViewMode: CoreViewMode;
  preview: RefuelRequestPreview | null;
  history: PlaytestUiState["history"];
  feedbackNote: string;
  isCommandPending: boolean;
  commandError: string;
  onSelectChannel: (channelIndex: number) => void;
  onChangeCoreView: (viewMode: CoreViewMode) => void;
  onPreviewRefuel: (request: RefuelRequest) => void;
  onCommitRefuel: (request: RefuelRequest) => void;
  onSetPlayback: (modeId: PlaybackModeId) => void;
  onStepSimulation: (hours: number) => void;
  onQueuePowerTarget: (targetFraction: number) => void;
  onQueueTiltTarget: (targetFraction: number) => void;
  onChangeNote: (note: string) => void;
}

type RefuelRequestPreview = NonNullable<PlaytestUiState["preview"]>;

function PlayWorkspace(props: PlayWorkspaceProps) {
  return (
    <>
      <div className="workspace-grid">
        <section className="panel core-panel" aria-labelledby="core-heading">
          <PanelHeader kicker="Spatial overview" title="Core heat map" id="core-heading" action={<span className="panel-live"><span className="status-dot is-live" aria-hidden="true" />LIVE</span>} />
          <CoreScene
            channels={props.snapshot.core.channels}
            selectedChannelIndex={props.selectedChannelIndex}
            viewMode={props.coreViewMode}
            onSelectChannel={props.onSelectChannel}
            onChangeViewMode={props.onChangeCoreView}
          />
          <HeatLegend />
          <div className="core-panel-footer">
            <span><strong>{props.snapshot.core.channelCount}</strong> channels</span>
            <span><strong>{props.snapshot.core.bundlePositionCount}</strong> bundles / channel</span>
            <span><strong>{props.snapshot.diagnostics.convergence.state}</strong> response</span>
          </div>
        </section>

        <aside className="detail-column">
          <ChannelDetail channel={props.selectedChannel} />
          <RefuelPlanner
            selectedChannel={props.selectedChannel}
            preview={props.preview}
            freshBundlesAvailable={props.snapshot.freshBundlesAvailable}
            isCommandPending={props.isCommandPending}
            onPreview={props.onPreviewRefuel}
            onCommit={props.onCommitRefuel}
          />
        </aside>
      </div>

      <div className="lower-grid">
        <ControlDeck
          snapshot={props.snapshot}
          isCommandPending={props.isCommandPending}
          onSetPlayback={props.onSetPlayback}
          onStepSimulation={props.onStepSimulation}
          onQueuePowerTarget={props.onQueuePowerTarget}
          onQueueTiltTarget={props.onQueueTiltTarget}
        />
        <FeedbackPanel
          snapshot={props.snapshot}
          history={props.history}
          feedbackNote={props.feedbackNote}
          commandError={props.commandError}
          onChangeNote={props.onChangeNote}
        />
      </div>
    </>
  );
}

function PanelHeader({ kicker, title, id, action }: { kicker: string; title: string; id?: string; action?: ReactNode }) {
  return (
    <div className="panel-header">
      <div><p className="panel-kicker">{kicker}</p><h2 id={id}>{title}</h2></div>
      {action ? <div className="panel-header-action">{action}</div> : null}
    </div>
  );
}

function HeatLegend() {
  return (
    <div className="heat-legend" aria-label="Channel power heat map legend">
      <span className="legend-caption">LOCAL POWER</span>
      <span className="legend-low">LOW</span>
      <span className="legend-gradient" aria-hidden="true" />
      <span className="legend-high">HIGH</span>
      <span className="legend-caption legend-end">relative to nominal</span>
    </div>
  );
}

function ChannelDetail({ channel }: { channel: CanduChannelSnapshot }) {
  const band = getChannelBand(channel);
  return (
    <section className="panel channel-panel" aria-labelledby="channel-heading">
      <div className="channel-heading-row">
        <div>
          <p className="panel-kicker">Selected channel</p>
          <h2 id="channel-heading"><span className="channel-prefix">CH</span> {String(channel.channelIndex).padStart(3, "0")}</h2>
        </div>
        <div className="channel-heading-badges">
          <span className={`channel-status is-${band}`}>{band === "nominal" ? "NOMINAL" : band.toUpperCase()}</span>
          <span className="channel-flow-chip" title="Coolant and fuel travel direction for this channel">
            <span aria-hidden="true">{getFlowArrow(channel.flowDirection)}</span>{getFlowDirectionLabel(channel.flowDirection)}
          </span>
        </div>
      </div>
      <div className="channel-metrics">
        <div><span>Channel power</span><strong>{formatPowerWatts(channel.powerWatts)} <small>{getPowerLabel(channel.localPowerFraction)}</small></strong></div>
        <div><span>Flux tilt</span><strong>{getTiltLabel(channel.localTiltFraction)}</strong></div>
        <div><span>Avg. burnup</span><strong>{channel.averageBurnupMwdPerKg.toFixed(1)} <small>MWd/kg</small></strong></div>
      </div>
      <div className="channel-power-bar" aria-label={`Channel ${channel.channelIndex} local power ${getPowerLabel(channel.localPowerFraction)}`}>
        <span className="channel-power-marker" style={{ left: `${Math.min(100, Math.max(0, (channel.localPowerFraction - 0.68) / 0.58 * 100))}%` }} />
      </div>
      <BundlePowerChart channel={channel} />
      <div className="stack-header"><span>Bundle inventory</span><span>burnup / identity</span></div>
      <div className="stack-orientation"><span>END A</span><span><span aria-hidden="true">{getFlowArrow(channel.flowDirection)}</span> coolant + fuel path</span><span>END B</span></div>
      <div className="bundle-stack" role="list" aria-label={`Channel ${channel.channelIndex} bundle stack`}>
        {channel.bundles.map((bundle) => (
          <BundleRow key={bundle.bundleId} bundle={bundle} />
        ))}
      </div>
    </section>
  );
}

function BundlePowerChart({ channel }: { channel: CanduChannelSnapshot }) {
  const displayMaximum = Math.max(1.15, ...channel.bundles.map((bundle) => bundle.localPowerFraction));
  return (
    <div className="bundle-power-chart">
      <div className="bundle-power-chart-header"><span>Bundle power profile</span><span>relative to mean</span></div>
      <div className="bundle-power-bars" role="img" aria-label={`Bundle power across channel ${channel.channelIndex}, from End A to End B`}>
        {channel.bundles.map((bundle) => {
          const height = Math.min(100, Math.max(4, (bundle.localPowerFraction / displayMaximum) * 100));
          return (
            <div
              className={bundle.isFresh ? "bundle-power-column is-fresh" : "bundle-power-column"}
              key={bundle.bundleId}
              title={`Bundle ${bundle.position + 1}: ${formatPowerWatts(bundle.powerWatts)} · ${getPowerLabel(bundle.localPowerFraction)} of mean`}
            >
              <div className="bundle-power-bar-track"><span className="bundle-power-bar-fill" style={{ height: `${height}%`, background: getHeatColor(bundle.localPowerFraction) }} /></div>
              <span className="bundle-power-position">{String(bundle.position + 1).padStart(2, "0")}</span>
            </div>
          );
        })}
      </div>
      <div className="bundle-power-axis"><span>END A</span><span><span aria-hidden="true">{getFlowArrow(channel.flowDirection)}</span> fuel / coolant</span><span>END B</span></div>
    </div>
  );
}

function BundleRow({ bundle }: { bundle: CanduChannelSnapshot["bundles"][number] }) {
  return (
    <div className={bundle.isFresh ? "bundle-row is-fresh" : "bundle-row"} role="listitem">
      <span className="bundle-position">{String(bundle.position + 1).padStart(2, "0")}</span>
      <span className="bundle-reading"><strong>{bundle.isFresh ? "FRESH" : bundle.currentBurnupMwdPerKg.toFixed(1)}</strong><small>{bundle.isFresh ? bundle.fuelTypeId : "MWd/kg"}</small></span>
      <span className="bundle-id" title={bundle.bundleId}>{bundle.bundleId.replace("SYN-B-", "B-")}</span>
    </div>
  );
}

interface RefuelPlannerProps {
  selectedChannel: CanduChannelSnapshot;
  preview: RefuelRequestPreview | null;
  freshBundlesAvailable: number;
  isCommandPending: boolean;
  onPreview: (request: RefuelRequest) => void;
  onCommit: (request: RefuelRequest) => void;
}

function RefuelPlanner({ selectedChannel, preview, freshBundlesAvailable, isCommandPending, onPreview, onCommit }: RefuelPlannerProps) {
  const [directionId, setDirectionId] = useState<RefuelRequest["directionId"]>(selectedChannel.flowDirection);
  const [shiftCount, setShiftCount] = useState<RefuelRequest["shiftCount"]>(4);
  const [fuelTypeId, setFuelTypeId] = useState("NAT-U-SYNTHETIC");
  useEffect(() => setDirectionId(selectedChannel.flowDirection), [selectedChannel.channelIndex, selectedChannel.flowDirection]);
  const request: RefuelRequest = { channelIndex: selectedChannel.channelIndex, directionId, shiftCount, fuelTypeId };
  const previewMatches = preview?.request.channelIndex === selectedChannel.channelIndex && preview.request.directionId === directionId && preview.request.shiftCount === shiftCount;

  const submitPreview = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    onPreview(request);
  };

  return (
    <section className="panel refuel-panel" aria-labelledby="refuel-heading">
      <div className="refuel-title-row">
        <div><p className="panel-kicker">On-power operation</p><h2 id="refuel-heading">Refuelling planner</h2></div>
        <span className="inventory-chip"><span className="status-dot is-live" aria-hidden="true" />{freshBundlesAvailable} fresh</span>
      </div>
      <form className="refuel-form" onSubmit={submitPreview}>
        <label className="field-label">Fuelling direction
          <select value={directionId} onChange={(event) => setDirectionId(event.target.value as RefuelRequest["directionId"])}>
            <option value="toward-end-b">End A → End B {selectedChannel.flowDirection === "toward-end-b" ? "(with flow)" : "(reverse)"}</option>
            <option value="toward-end-a">End B → End A {selectedChannel.flowDirection === "toward-end-a" ? "(with flow)" : "(reverse)"}</option>
          </select>
        </label>
        <div className="field-row">
          <label className="field-label">Shift size
            <select value={shiftCount} onChange={(event) => setShiftCount(Number(event.target.value) as RefuelRequest["shiftCount"])}>
              <option value={4}>4 bundles</option>
              <option value={8}>8 bundles</option>
            </select>
          </label>
          <label className="field-label">Fuel type
            <select value={fuelTypeId} onChange={(event) => setFuelTypeId(event.target.value)}>
              <option value="NAT-U-SYNTHETIC">NAT-U synthetic</option>
            </select>
          </label>
        </div>
        <button className="button button-primary button-wide" type="submit" disabled={isCommandPending}>
          <span aria-hidden="true">⌁</span> Preview shift
        </button>
      </form>

      {previewMatches && preview !== null ? (
        <div className="refuel-preview" aria-live="polite">
          <div className="preview-heading"><span className="preview-check">✓</span><div><strong>Preview ready</strong><span>{shiftCount} bundles · {directionId === "toward-end-b" ? "End A → End B" : "End B → End A"}</span></div></div>
          <div className="preview-grid">
            <PreviewMetric label="Discharge" value={`${preview.dischargeBurnupMwdPerKg.toFixed(1)} MWd/kg`} />
            <PreviewMetric label="Local power" value={formatSignedNumber(preview.localPowerDeltaFraction * 100, 2) + " pts"} />
            <PreviewMetric label="Tilt shift" value={formatSignedNumber(preview.localTiltDeltaFraction * 100, 2) + " pts"} />
            <PreviewMetric label="Δ reactivity" value={formatReactivity(preview.predictedReactivityDelta)} />
            <PreviewMetric label="Score effect" value={formatSignedNumber(preview.projectedScoreDelta, 1)} />
          </div>
          <button className="button button-commit button-wide" type="button" disabled={isCommandPending} onClick={() => onCommit(preview.request)}>
            Commit {preview.request.shiftCount}-bundle shift <span aria-hidden="true">↗</span>
          </button>
          <p className="preview-note">Commit sends the selected transition to the active bridge.</p>
        </div>
      ) : (
        <div className="empty-preview"><span className="empty-preview-icon" aria-hidden="true">◎</span><span>Direction defaults to this channel’s coolant path. Choose a shift size, then preview the bundle movement.</span></div>
      )}
    </section>
  );
}

function PreviewMetric({ label, value }: { label: string; value: string }) {
  return <div><span>{label}</span><strong>{value}</strong></div>;
}

interface ControlDeckProps {
  snapshot: CanduSnapshot;
  isCommandPending: boolean;
  onSetPlayback: (modeId: PlaybackModeId) => void;
  onStepSimulation: (hours: number) => void;
  onQueuePowerTarget: (targetFraction: number) => void;
  onQueueTiltTarget: (targetFraction: number) => void;
}

function ControlDeck({ snapshot, isCommandPending, onSetPlayback, onStepSimulation, onQueuePowerTarget, onQueueTiltTarget }: ControlDeckProps) {
  const [powerTarget, setPowerTarget] = useState(snapshot.targetPowerFraction);
  const [tiltTarget, setTiltTarget] = useState(snapshot.targetTiltFraction);
  useEffect(() => setPowerTarget(snapshot.targetPowerFraction), [snapshot.targetPowerFraction]);
  useEffect(() => setTiltTarget(snapshot.targetTiltFraction), [snapshot.targetTiltFraction]);

  return (
    <section className="panel controls-panel" aria-labelledby="controls-heading">
      <PanelHeader kicker="Operator controls" title="Control deck" id="controls-heading" action={<span className="control-lock"><span className="status-dot is-live" aria-hidden="true" />AUTO-REGULATION</span>} />
      <div className="control-sections">
        <div className="playback-section">
          <div className="control-label-row"><span>Playback</span><strong>{snapshot.isPaused ? "PAUSED" : snapshot.playbackModeId}</strong></div>
          <div className="playback-buttons" role="group" aria-label="Simulation playback speed">
            {(["pause", "1x", "10x", "60x"] as PlaybackModeId[]).map((modeId) => (
              <button
                key={modeId}
                className={snapshot.playbackModeId === modeId ? "speed-button is-active" : "speed-button"}
                type="button"
                aria-label={modeId === "pause" ? "Pause simulation" : snapshot.isPaused ? `Resume simulation at ${modeId}` : `Set playback to ${modeId}`}
                aria-pressed={snapshot.playbackModeId === modeId}
                disabled={isCommandPending}
                onClick={() => onSetPlayback(modeId)}
              >
                {modeId === "pause" ? "Ⅱ" : modeId}
              </button>
            ))}
          </div>
          <div className="playback-rate-note" title={`${BASE_CLOCK_SIMULATION_SECONDS_PER_WALL_SECOND} simulated seconds per wall second`}><span>Base clock</span><strong>{BASE_CLOCK_WALL_SECONDS_PER_SIMULATION_HOUR} s = 1 simulated hour</strong></div>
          <div className="step-row"><span>Jump while paused</span><button type="button" className="step-button" disabled={isCommandPending} onClick={() => onStepSimulation(1)}>+1 h</button><button type="button" className="step-button" disabled={isCommandPending} onClick={() => onStepSimulation(8)}>+8 h</button><button type="button" className="step-button" disabled={isCommandPending} onClick={() => onStepSimulation(24)}>+1 d</button></div>
        </div>

        <div className="target-section">
          <div className="target-control">
            <div className="control-label-row"><label htmlFor="power-target">Power target</label><strong>{getPowerLabel(powerTarget)}</strong></div>
            <input id="power-target" className="range-input range-cyan" type="range" min="0.8" max="1.2" step="0.005" value={powerTarget} aria-valuetext={`${getPowerLabel(powerTarget)} power target`} onChange={(event) => setPowerTarget(Number(event.target.value))} />
            <div className="range-scale"><span>80%</span><span>nominal</span><span>120%</span></div>
            <button type="button" className="link-button" disabled={isCommandPending} onClick={() => onQueuePowerTarget(powerTarget)}>Queue power target <span aria-hidden="true">→</span></button>
          </div>
          <div className="target-control">
            <div className="control-label-row"><label htmlFor="tilt-target">Tilt target</label><strong>{getTiltLabel(tiltTarget)}</strong></div>
            <input id="tilt-target" className="range-input range-violet" type="range" min="-0.2" max="0.2" step="0.005" value={tiltTarget} aria-valuetext={`${getTiltLabel(tiltTarget)} axial tilt target`} onChange={(event) => setTiltTarget(Number(event.target.value))} />
            <div className="range-scale"><span>-20%</span><span>flat</span><span>+20%</span></div>
            <button type="button" className="link-button" disabled={isCommandPending} onClick={() => onQueueTiltTarget(tiltTarget)}>Queue tilt target <span aria-hidden="true">→</span></button>
          </div>
        </div>
      </div>
      <div className="control-footnote"><span className="footnote-icon" aria-hidden="true">i</span> Power and tilt targets are queued to the active regulating response; refuelling remains a player command.</div>
    </section>
  );
}

interface FeedbackPanelProps {
  snapshot: CanduSnapshot;
  history: PlaytestUiState["history"];
  feedbackNote: string;
  commandError: string;
  onChangeNote: (note: string) => void;
}

function FeedbackPanel({ snapshot, history, feedbackNote, commandError, onChangeNote }: FeedbackPanelProps) {
  return (
    <section className="panel feedback-panel" aria-labelledby="feedback-heading">
      <PanelHeader kicker="Operator feedback" title="Session notes" id="feedback-heading" action={<span className="saved-note">autosaves locally</span>} />
      {snapshot.lastEvent ? <EventCallout event={snapshot.lastEvent} /> : null}
      <label className="feedback-label" htmlFor="feedback-note">Feedback note</label>
      <textarea id="feedback-note" value={feedbackNote} onChange={(event) => onChangeNote(event.target.value)} placeholder="What did the core tell you? Record an observation for this playtest…" rows={3} />
      <div className="feedback-bottom"><span>{feedbackNote.length}/500</span><span>{commandError ? "Bridge needs attention" : "Ready for the next decision"}</span></div>
      <div className="activity-header"><span>Recent commands</span><span>{history.length} recorded</span></div>
      <div className="activity-list" aria-label="Recent command history">
        {history.length === 0 ? <p className="activity-empty">Your first target, preview, or step will appear here.</p> : history.slice(-4).reverse().map((entry) => <ActivityRow key={`${entry.index}-${entry.snapshotSequence}`} entry={entry} />)}
      </div>
    </section>
  );
}

function EventCallout({ event }: { event: CanduEvent }) {
  return (
    <div className={`event-callout is-${event.tone}`} role="status">
      <span className="event-icon" aria-hidden="true">{event.tone === "positive" ? "✓" : event.tone === "warning" ? "!" : "·"}</span>
      <div><strong>{event.title}</strong><span>{event.detail}</span></div>
      <time>{formatSimulationTime(event.timeSeconds)}</time>
    </div>
  );
}

function ActivityRow({ entry }: { entry: PlaytestUiState["history"][number] }) {
  return (
    <div className="activity-row">
      <span className={entry.accepted ? "activity-dot is-ok" : "activity-dot is-error"} aria-hidden="true" />
      <div><strong>{commandLabel(entry.command)}</strong><span>{entry.message}</span></div>
      <time>{formatSimulationTime(entry.simulationTimeSeconds)}</time>
    </div>
  );
}

interface LabWorkspaceProps {
  snapshot: CanduSnapshot;
  selectedChannel: CanduChannelSnapshot;
  selectedChannelIndex: number;
  coreViewMode: CoreViewMode;
  history: PlaytestUiState["history"];
  stateDigest: string;
  replayStatus: string;
  copyStatus: string;
  isCommandPending: boolean;
  onSelectChannel: (channelIndex: number) => void;
  onChangeCoreView: (viewMode: CoreViewMode) => void;
  onCopyDigest: () => void;
  onSaveReplay: () => void;
  onDownloadReplay: () => void;
  onReplaySaved: () => void;
  onClearHistory: () => void;
  onResetRun: () => void;
}

function LabWorkspace(props: LabWorkspaceProps) {
  return (
    <>
      <div className="lab-intro">
        <div><p className="eyebrow">BRIDGE INSPECTION / OWNER PLAYTEST</p><h2>See what the browser is actually running.</h2><p>Lab mode exposes protocol identity, convergence signals, and replay controls without changing the Unity seam.</p></div>
        <div className="lab-actions"><button className="button button-quiet" type="button" onClick={props.onResetRun} disabled={props.isCommandPending}>Reset fixture</button><span className="lab-scope">Debug surface · not scored</span></div>
      </div>
      <div className="lab-grid">
        <section className="panel core-panel lab-core-panel" aria-labelledby="lab-core-heading">
          <PanelHeader kicker="Same core / more signal" title="Inspectable heat map" id="lab-core-heading" action={<span className="lab-channel-readout">selected CH {String(props.selectedChannelIndex).padStart(3, "0")}</span>} />
          <CoreScene channels={props.snapshot.core.channels} selectedChannelIndex={props.selectedChannelIndex} viewMode={props.coreViewMode} onSelectChannel={props.onSelectChannel} onChangeViewMode={props.onChangeCoreView} />
          <HeatLegend />
          <div className="lab-selected-readout"><span className="readout-key">Selected channel</span><strong>CH {String(props.selectedChannel.channelIndex).padStart(3, "0")}</strong><span>{getPowerLabel(props.selectedChannel.localPowerFraction)} power · {props.selectedChannel.averageBurnupMwdPerKg.toFixed(2)} MWd/kg avg burnup</span></div>
        </section>
        <LabDiagnostics snapshot={props.snapshot} />
      </div>
      <ReplayPanel history={props.history} source={props.snapshot.source} stateDigest={props.stateDigest} replayStatus={props.replayStatus} copyStatus={props.copyStatus} isCommandPending={props.isCommandPending} onCopyDigest={props.onCopyDigest} onSaveReplay={props.onSaveReplay} onDownloadReplay={props.onDownloadReplay} onReplaySaved={props.onReplaySaved} onClearHistory={props.onClearHistory} />
    </>
  );
}

function LabDiagnostics({ snapshot }: { snapshot: CanduSnapshot }) {
  const labSolve = snapshot.lab?.spatialSolve;
  const convergence = labSolve === undefined
    ? snapshot.diagnostics.convergence
    : {
        state: labSolve.hasUsableState ? "converged" as const : "pending" as const,
        iterations: labSolve.diagnostics.iterationCount,
        residual: labSolve.diagnostics.residualRelativeInfinity ?? 0,
        relativePowerError: labSolve.finalState === null
          ? 1
          : Math.abs(labSolve.finalState.totalPowerW - 0.4) / 0.4,
        lastSolveMilliseconds: 0,
        solverLabel: "Core SpatialEigenSolve / lab-2x8-synthetic-v1",
      };
  const convergencePercent = convergence.state === "converged" ? 100 : convergence.state === "settling" ? 72 : 42;
  const checks = labSolve === undefined
    ? snapshot.diagnostics.checks
    : [
        { label: "Lab fixture", value: "2 × 8 explicit nodes", status: "pass" as const },
        { label: "Coupled solve", value: labSolve.hasUsableState ? "usable state" : "rejected", status: labSolve.hasUsableState ? "pass" as const : "watch" as const },
        { label: "Energy groups", value: "fast → thermal", status: "info" as const },
      ];
  return (
    <section className="panel diagnostics-panel" aria-labelledby="diagnostics-heading">
      <PanelHeader kicker="Protocol diagnostics" title="Convergence status" id="diagnostics-heading" action={<span className={`diagnostic-state is-${convergence.state}`}>{convergence.state}</span>} />
      <div className="convergence-hero"><div className="convergence-ring" style={{ "--ring-progress": `${convergencePercent}%` } as CSSProperties}><strong>{convergencePercent}%</strong><span>settled</span></div><div><p>Response convergence</p><strong>{convergence.solverLabel}</strong><span>Last solve {convergence.lastSolveMilliseconds.toFixed(2)} ms · {convergence.iterations} iterations</span></div></div>
      <div className="diagnostic-progress"><span style={{ width: `${convergencePercent}%` }} /></div>
      <div className="diagnostic-metrics"><DiagnosticMetric label="Residual" value={convergence.residual.toExponential(2)} /><DiagnosticMetric label="Power error" value={getPowerLabel(convergence.relativePowerError)} /><DiagnosticMetric label="Pending actions" value={String(snapshot.pendingActionCount)} /><DiagnosticMetric label="Devices" value={getPowerLabel(snapshot.deviceAvailableFraction)} /></div>
      <div className="diagnostic-checks">
        {checks.map((check) => <div className="diagnostic-check" key={check.label}><span className={`check-mark is-${check.status}`} aria-hidden="true">{check.status === "pass" ? "✓" : check.status === "watch" ? "!" : "·"}</span><span>{check.label}</span><strong>{check.value}</strong></div>)}
      </div>
      <div className="protocol-card"><div className="protocol-card-header"><span>WIRE CONTRACT</span><span>{snapshot.protocol}</span></div><code>{`{\n  "protocol": "${snapshot.protocol}",\n  "source": "${snapshot.source}",\n  "core": "${snapshot.core.channelCount} channels × ${snapshot.core.bundlePositionCount} bundles"\n}`}</code></div>
      <p className="diagnostic-note"><span aria-hidden="true">i</span> {labSolve === undefined ? (snapshot.physics.isAuthoritative ? "Full-core convergence is reported by ReactorSim.Core using the versioned two-group pack." : "Compatibility fixture diagnostics are shown because an authoritative browser WASM export is not active.") : "These values come from the authoritative Core spatial solve on the explicit synthetic Lab fixture."}</p>
    </section>
  );
}

function DiagnosticMetric({ label, value }: { label: string; value: string }) {
  return <div><span>{label}</span><strong>{value}</strong></div>;
}

interface ReplayPanelProps {
  history: PlaytestUiState["history"];
  source: CanduSnapshot["source"];
  stateDigest: string;
  replayStatus: string;
  copyStatus: string;
  isCommandPending: boolean;
  onCopyDigest: () => void;
  onSaveReplay: () => void;
  onDownloadReplay: () => void;
  onReplaySaved: () => void;
  onClearHistory: () => void;
}

function ReplayPanel({ history, source, stateDigest, replayStatus, copyStatus, isCommandPending, onCopyDigest, onSaveReplay, onDownloadReplay, onReplaySaved, onClearHistory }: ReplayPanelProps) {
  const [showJson, setShowJson] = useState(false);
  const previewJson = useMemo(() => serializeReplayForDownload(createReplayArchive(history, source, "2026-01-01T00:00:00.000Z")), [history, source]);
  return (
    <section className="panel replay-panel" aria-labelledby="replay-heading">
      <div className="replay-heading-row"><div><p className="panel-kicker">Deterministic handoff</p><h2 id="replay-heading">State digest & command replay</h2><p>History is JSON-safe, localStorage-backed, and replayable against the active bridge.</p></div><div className="digest-badge"><span>STATE DIGEST</span><strong>{stateDigest}</strong><button type="button" onClick={onCopyDigest} aria-label="Copy state digest">{copyStatus || "copy"}</button></div></div>
      <div className="replay-controls"><div className="replay-count"><strong>{history.length}</strong><span>recorded commands</span></div><div className="replay-buttons"><button className="button button-secondary" type="button" onClick={onSaveReplay}>Save to localStorage</button><button className="button button-secondary" type="button" onClick={onDownloadReplay}>Download JSON</button><button className="button button-primary" type="button" onClick={onReplaySaved} disabled={isCommandPending}>Replay saved</button><button className="button button-quiet" type="button" onClick={onClearHistory} disabled={history.length === 0}>Clear history</button></div></div>
      <div className="replay-status" role="status"><span className="status-dot is-live" aria-hidden="true" />{replayStatus}<span className="replay-storage-key">{REPLAY_STORAGE_KEY}</span><span className="replay-storage-key">{NOTE_STORAGE_KEY}</span></div>
      <button className="json-disclosure" type="button" aria-expanded={showJson} onClick={() => setShowJson((visible) => !visible)}>{showJson ? "Hide" : "Preview"} replay JSON <span aria-hidden="true">{showJson ? "⌃" : "⌄"}</span></button>
      {showJson ? <pre className="json-preview" aria-label="Replay JSON preview">{previewJson}</pre> : null}
    </section>
  );
}

function powerIndicator(snapshot: CanduSnapshot): string {
  const delta = (snapshot.physics.actualPowerFraction - snapshot.targetPowerFraction) * 100;
  return `${delta >= 0 ? "+" : ""}${delta.toFixed(2)} pts to target`;
}

function commandLabel(command: CanduCommand): string {
  switch (command.type) {
    case "advance": return "Auto advance";
    case "step": return `Step ${command.simulationSeconds / 3600 >= 24 ? "1 day" : `${command.simulationSeconds / 3600} h`}`;
    case "set-playback-mode": return `Playback ${command.modeId}`;
    case "pause": return "Pause";
    case "resume": return "Resume";
    case "queue-power-target": return `Power target ${getPowerLabel(command.targetFraction)}`;
    case "queue-tilt-target": return `Tilt target ${getTiltLabel(command.targetFraction)}`;
    case "preview-refuel": return `Preview CH ${command.request.channelIndex} · ${command.request.shiftCount} bundles`;
    case "commit-refuel": return `Commit CH ${command.request.channelIndex} · ${command.request.shiftCount} bundles`;
    case "reset": return "Reset practice run";
  }
}
