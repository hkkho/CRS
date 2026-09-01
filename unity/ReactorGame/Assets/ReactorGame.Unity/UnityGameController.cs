using System;
using ReactorSim.Game;
using UnityEngine;

namespace ReactorGame.Unity
{
    /// <summary>
    /// Owns the live game session, view binding, and bounded real-time pacing.
    /// Unity time controls presentation requests only; Core remains the owner of
    /// deterministic simulation time and transitions.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class UnityGameController : MonoBehaviour
    {
        private const int MaximumCatchUpTicksPerFrame = 5;

        private Phase8UnityRuntimeAdapter _adapter;
        private Phase10DashboardView _dashboard;
        private Phase10ControlsView _controls;
        private Phase10TimelineView _timeline;
        private double _accumulatedWallMilliseconds;

        public bool IsInitialized { get; private set; }

        public bool AutoAdvance { get; set; } = true;

        public UnityRuntimePort RuntimePort { get; private set; }

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            try
            {
                Phase10ShellView shell = RequireComponent<Phase10ShellView>();
                _adapter = RequireComponent<Phase8UnityRuntimeAdapter>();
                _dashboard = RequireComponent<Phase10DashboardView>();
                _controls = RequireComponent<Phase10ControlsView>();
                _timeline = RequireComponent<Phase10TimelineView>();

                shell.BuildVisualShell();
                RuntimePort = new UnityRuntimePort(PracticeGameSessionFactory.Create());
                _adapter.Bind(RuntimePort);
                _dashboard.Bind(_adapter);
                _controls.Bind(_adapter);
                _timeline.Bind(_adapter);
                IsInitialized = true;
                Debug.Log("CANDU practice session started; real-time simulation pacing is active.");
            }
            catch (Exception exception)
            {
                enabled = false;
                Debug.LogException(exception, this);
            }
        }

        public void Tick(double unscaledDeltaSeconds)
        {
            if (!IsInitialized || !AutoAdvance || unscaledDeltaSeconds <= 0.0 ||
                double.IsNaN(unscaledDeltaSeconds) || double.IsInfinity(unscaledDeltaSeconds))
            {
                return;
            }

            Phase8UnityPresentationSnapshotV1 snapshot = _adapter.Snapshot;
            if (snapshot == null || snapshot.IsPaused ||
                !string.Equals(snapshot.OutcomeId, "Running", StringComparison.Ordinal))
            {
                _accumulatedWallMilliseconds = 0.0;
                return;
            }

            double tickMilliseconds = snapshot.WallControlTickMilliseconds;
            double maximumBacklog = tickMilliseconds * MaximumCatchUpTicksPerFrame;
            _accumulatedWallMilliseconds = Math.Min(
                _accumulatedWallMilliseconds + unscaledDeltaSeconds * 1000.0,
                maximumBacklog);

            int ticks = 0;
            while (_accumulatedWallMilliseconds + 1e-9 >= tickMilliseconds &&
                   ticks < MaximumCatchUpTicksPerFrame)
            {
                Phase8UnityCommandResultV1 result = _adapter.AdvanceWallMilliseconds(
                    snapshot.WallControlTickMilliseconds);
                if (!result.Accepted)
                {
                    Debug.LogWarning(
                        "Automatic simulation advance stopped: " + result.DiagnosticMessage,
                        this);
                    AutoAdvance = false;
                    break;
                }

                _accumulatedWallMilliseconds -= tickMilliseconds;
                ticks++;
            }
        }

        private void OnDestroy()
        {
            if (!IsInitialized)
            {
                return;
            }

            _timeline.Unbind();
            _controls.Unbind();
            _dashboard.Unbind();
            _adapter.Unbind();
            IsInitialized = false;
        }

        private T RequireComponent<T>() where T : Component
        {
            T component = GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException(
                    "The Unity game controller requires " + typeof(T).Name + " on the same object.");
            }

            return component;
        }
    }
}
