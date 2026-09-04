import { describe, expect, it } from "vitest";
import {
  BASE_CLOCK_SIMULATION_SECONDS_PER_WALL_SECOND,
  CORE_CHANNEL_COUNT,
} from "./protocol";
import { createSyntheticFixtureBridge } from "./fixture";

describe("synthetic CANDU-6 playtest fixture", () => {
  it("advances one simulated hour every two wall seconds at the base rate", async () => {
    const bridge = createSyntheticFixtureBridge();

    const firstHalfHour = await bridge.dispatch({ type: "advance", wallMilliseconds: 1_000 });
    expect(firstHalfHour.snapshot.simulationTimeSeconds).toBe(BASE_CLOCK_SIMULATION_SECONDS_PER_WALL_SECOND);

    const oneHour = await bridge.dispatch({ type: "advance", wallMilliseconds: 1_000 });
    expect(oneHour.snapshot.simulationTimeSeconds).toBe(3_600);
    expect(oneHour.snapshot.wallElapsedSeconds).toBe(2);
  });

  it("keeps a day jump paused until a live speed is selected again", async () => {
    const bridge = createSyntheticFixtureBridge();

    await bridge.dispatch({ type: "set-playback-mode", modeId: "pause" });
    const jumped = await bridge.dispatch({ type: "step", simulationSeconds: 86_400 });
    expect(jumped.snapshot.isPaused).toBe(true);
    expect(jumped.snapshot.simulationTimeSeconds).toBe(86_400);

    const resumed = await bridge.dispatch({ type: "set-playback-mode", modeId: "1x" });
    expect(resumed.snapshot.isPaused).toBe(false);
    expect(resumed.snapshot.playbackModeId).toBe("1x");
  });

  it("uses the stepped 22-by-22 CANDU-6 face and alternating channel flow", () => {
    const bridge = createSyntheticFixtureBridge();
    const channels = bridge.getSnapshot().core.channels;
    const expectedRowLengths = [6, 12, 14, 16, 18, 18, 20, 20, 22, 22, 22, 22, 22, 22, 20, 20, 18, 18, 16, 14, 12, 6];

    expect(channels).toHaveLength(CORE_CHANNEL_COUNT);
    expect(expectedRowLengths.map((_, row) => channels.filter((channel) => channel.gridRow === row).length)).toEqual(expectedRowLengths);
    expect(channels[0]).toMatchObject({ gridColumn: 8, gridRow: 0, flowDirection: "toward-end-b" });
    expect(channels[1].flowDirection).toBe("toward-end-a");
    expect(channels.find((channel) => channel.gridRow === 10 && channel.gridColumn === 10)?.flowDirection).toBe("toward-end-b");

    const outerChannel = channels.find((channel) => channel.gridRow === 0);
    const centerChannel = channels.find((channel) => channel.gridRow === 10 && channel.gridColumn === 10);
    expect(centerChannel!.localPowerFraction).toBeGreaterThan(outerChannel!.localPowerFraction);
    expect(centerChannel!.bundles[5].localPowerFraction).toBeGreaterThan(centerChannel!.bundles[0].localPowerFraction);

    const channelPowerTotal = channels.reduce((sum, channel) => sum + channel.powerWatts, 0);
    const bundlePowerTotal = channels.reduce(
      (sum, channel) => sum + channel.bundles.reduce((channelSum, bundle) => channelSum + bundle.powerWatts, 0),
      0,
    );
    expect(channels[0].powerWatts).toBeGreaterThan(0);
    expect(channels[0].powerWatts).toBeCloseTo(channels[0].bundles.reduce((sum, bundle) => sum + bundle.powerWatts, 0), 6);
    expect(channelPowerTotal).toBeCloseTo(channels[0].powerWatts + channels.slice(1).reduce((sum, channel) => sum + channel.powerWatts, 0), 6);
    expect(channelPowerTotal).toBeCloseTo(bridge.getSnapshot().physics.totalPowerWatts, 6);
    expect(bundlePowerTotal).toBeCloseTo(bridge.getSnapshot().physics.totalPowerWatts, 6);
    expect(bridge.getSnapshot().physics.powerBalanceRelativeError).toBeLessThan(1e-12);
  });

  it("derives a refuelling reactivity preview from the candidate bundle state", async () => {
    const bridge = createSyntheticFixtureBridge();
    const before = bridge.getSnapshot();

    const response = await bridge.dispatch({
      type: "preview-refuel",
      request: {
        channelIndex: 0,
        directionId: "toward-end-b",
        shiftCount: 4,
        fuelTypeId: "NAT-U-SYNTHETIC",
      },
    });

    expect(response.accepted).toBe(true);
    expect(response.preview).not.toBeNull();
    expect(response.preview!.predictedReactivityDelta).toBeGreaterThan(0);
    expect(response.preview!.projectedPowerFraction).toBe(before.physics.powerAmplitude);
    expect(response.snapshot.physics.reactivity).toBe(before.physics.reactivity);
  });
});
