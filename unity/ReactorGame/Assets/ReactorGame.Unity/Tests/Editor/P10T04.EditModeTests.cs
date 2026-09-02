using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ReactorGame.Unity.P10T04
{
    public sealed class P10T04EditModeTests
    {
        [Test]
        public void ControlsBindsRendersAndForwardsApprovedCommands()
        {
            GameObject gameObject = new GameObject("P10-T04-controls-edit-mode");
            try
            {
                Phase10ShellView shell = gameObject.AddComponent<Phase10ShellView>();
                Phase8UnityRuntimeAdapter adapter = gameObject.AddComponent<Phase8UnityRuntimeAdapter>();
                Phase10ControlsView controls = gameObject.AddComponent<Phase10ControlsView>();
                FakeRuntimePort port = new FakeRuntimePort(CreateSnapshot());

                adapter.Bind(port);
                controls.Bind(adapter);

                Assert.That(shell.IsBuilt, Is.True);
                Assert.That(controls.IsBuilt, Is.True);
                Assert.That(controls.IsBound, Is.True);
                Assert.That(controls.ObservedText, Does.Contain("Power 95.0%"));
                Assert.That(controls.PacingText, Does.Contain("x10"));
                Assert.That(controls.PowerTargetText, Is.EqualTo("0.95"));
                Assert.That(controls.TiltTargetText, Is.EqualTo("0.1"));

                InputField[] inputs = controls.GetComponentsInChildren<InputField>(true);
                Assert.That(inputs, Has.Length.EqualTo(5));
                inputs[0].text = "0.875";
                inputs[1].text = "0.125";
                inputs[2].text = "190";
                inputs[3].text = "4";
                inputs[4].text = "NAT-U-SYNTHETIC";

                Assert.That(controls.ApplyPowerTarget().Accepted, Is.True);
                Assert.That(controls.ApplyTiltTarget().Accepted, Is.True);
                Assert.That(controls.RefuelTowardEndA().Accepted, Is.True);
                Assert.That(controls.AdvanceControlTick().Accepted, Is.True);
                Assert.That(controls.AdvanceOneSecond().Accepted, Is.True);
                Assert.That(controls.Pause().Accepted, Is.True);
                Assert.That(controls.Resume().Accepted, Is.True);
                Assert.That(controls.UseAuditPlayback().Accepted, Is.True);
                Assert.That(controls.UseAcceleratedPlayback().Accepted, Is.True);

                Assert.That(port.Commands, Has.Count.EqualTo(9));
                Assert.That(port.Commands[0].Kind, Is.EqualTo(Phase8UnityCommandKindV1.QueuePowerTarget));
                Assert.That(port.Commands[0].TargetFraction, Is.EqualTo(0.875));
                Assert.That(port.Commands[1].Kind, Is.EqualTo(Phase8UnityCommandKindV1.QueueTiltTarget));
                Assert.That(port.Commands[1].TargetFraction, Is.EqualTo(0.125));
                Assert.That(port.Commands[2].Kind, Is.EqualTo(Phase8UnityCommandKindV1.RefuelChannel));
                Assert.That(port.Commands[2].ChannelIndex, Is.EqualTo(190U));
                Assert.That(port.Commands[2].RefuellingDirectionId, Is.EqualTo(Phase10ControlsView.TowardEndADirectionId));
                Assert.That(port.Commands[2].ShiftCount, Is.EqualTo(4));
                Assert.That(port.Commands[2].FuelTypeId, Is.EqualTo("NAT-U-SYNTHETIC"));
                Assert.That(port.Commands[3].WallMilliseconds, Is.EqualTo(100UL));
                Assert.That(port.Commands[4].WallMilliseconds, Is.EqualTo(1000UL));
                Assert.That(port.Commands[7].PlaybackModeId, Is.EqualTo(Phase10ControlsView.AuditPlaybackModeId));
                Assert.That(port.Commands[8].PlaybackModeId, Is.EqualTo(Phase10ControlsView.AcceleratedPlaybackModeId));
                for (int index = 0; index < port.Commands.Count; index++)
                {
                    Assert.That(port.Commands[index].Sequence, Is.EqualTo((ulong)(index + 1)));
                }

                Assert.That(controls.StatusText, Does.Contain("accepted"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void RebindingDoesNotDuplicateListenersAndInvalidInputFailsClosed()
        {
            GameObject gameObject = new GameObject("P10-T04-duplicate-edit-mode");
            try
            {
                gameObject.AddComponent<Phase10ShellView>();
                Phase8UnityRuntimeAdapter adapter = gameObject.AddComponent<Phase8UnityRuntimeAdapter>();
                Phase10ControlsView controls = gameObject.AddComponent<Phase10ControlsView>();
                FakeRuntimePort port = new FakeRuntimePort(CreateSnapshot());
                adapter.Bind(port);
                controls.Bind(adapter);
                controls.Bind(adapter);

                Button advanceButton;
                Assert.That(
                    controls.TryGetActionButton(
                        Phase10ControlActionV1.AdvanceControlTick,
                        out advanceButton),
                    Is.True);
                advanceButton.onClick.Invoke();
                advanceButton.onClick.Invoke();
                Assert.That(port.Commands, Has.Count.EqualTo(2));

                InputField powerInput = controls.GetComponentsInChildren<InputField>(true)[0];
                powerInput.SetTextWithoutNotify(string.Empty);
                Assert.That(controls.ApplyPowerTarget(), Is.Null);
                Assert.That(port.Commands, Has.Count.EqualTo(2));
                Assert.That(controls.StatusText, Does.Contain("finite invariant decimal"));

                port.RejectKind = Phase8UnityCommandKindV1.Pause;
                Phase8UnityCommandResultV1 rejection = controls.Pause();
                Assert.That(rejection.Accepted, Is.False);
                Assert.That(controls.StatusText, Does.Contain("Fake.Rejected"));

                controls.Unbind();
                Assert.That(controls.IsBound, Is.False);
                Assert.That(controls.AdvanceControlTick(), Is.Null);
                Assert.That(controls.StatusText, Does.Contain("not bound"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ControlsRequireAValidAdapterSnapshot()
        {
            GameObject gameObject = new GameObject("P10-T04-invalid-bind-edit-mode");
            try
            {
                Phase8UnityRuntimeAdapter adapter = gameObject.AddComponent<Phase8UnityRuntimeAdapter>();
                Phase10ControlsView controls = gameObject.AddComponent<Phase10ControlsView>();

                Assert.That(
                    () => controls.Bind(adapter),
                    Throws.InvalidOperationException.With.Message.Contains("requires a bound adapter"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static Phase8UnityPresentationSnapshotV1 CreateSnapshot()
        {
            return new Phase8UnityPresentationSnapshotV1(
                "p8-t02-tutorial-startup",
                "tutorial",
                "play-accelerated-10x",
                10.0,
                100,
                600.0,
                12.0,
                1.2,
                0.95,
                0.10,
                1.0,
                1.0,
                2,
                0,
                1,
                913.667,
                1,
                "Running",
                false);
        }

        private sealed class FakeRuntimePort : IPhase8RuntimePort
        {
            public FakeRuntimePort(Phase8UnityPresentationSnapshotV1 snapshot)
            {
                Snapshot = snapshot;
            }

            public Phase8UnityPresentationSnapshotV1 Snapshot { get; }

            public List<Phase8UnityInputCommandV1> Commands { get; } =
                new List<Phase8UnityInputCommandV1>();

            public Phase8UnityCommandKindV1? RejectKind { get; set; }

            public Phase8UnityCommandResultV1 Execute(Phase8UnityInputCommandV1 command)
            {
                Commands.Add(command);
                if (RejectKind.HasValue && RejectKind.Value == command.Kind)
                {
                    return Phase8UnityCommandResultV1.RejectedResult(
                        command,
                        "Fake.Rejected",
                        "The test runtime rejected this command.",
                        Snapshot);
                }

                return Phase8UnityCommandResultV1.AcceptedResult(command, Snapshot);
            }
        }
    }
}
