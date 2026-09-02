import { describe, expect, it } from "vitest";
import {
  parseProtocolResponse,
  parseReplayArchive,
  PROTOCOL_VERSION,
  serializeProtocolCommand,
  serializeReplayArchive,
  type CanduCommandResponse,
  type CanduReplayArchive,
} from "./protocol";

describe("candu-playtest-v1 protocol serialization", () => {
  it("serializes command envelopes with stable key ordering", () => {
    const command = { type: "queue-power-target", targetFraction: 1 } as const;

    expect(serializeProtocolCommand(command)).toBe(
      '{"payload":{"targetFraction":1,"type":"queue-power-target"},"protocol":"candu-playtest-v1","type":"command"}',
    );
  });

  it("accepts a response envelope and preserves the typed response payload", () => {
    const response = {
      protocol: PROTOCOL_VERSION,
      accepted: true,
      sequence: 3,
      command: { type: "pause" },
      message: "Simulation paused.",
      diagnostics: [],
      snapshot: { protocol: PROTOCOL_VERSION, core: { channels: [] } },
      preview: null,
    } as unknown as CanduCommandResponse;

    expect(parseProtocolResponse(JSON.stringify({ protocol: PROTOCOL_VERSION, type: "response", payload: response }))).toEqual(response);
  });

  it("rejects archives from another protocol version", () => {
    const archive = {
      protocol: "other-protocol",
      kind: "command-replay",
      source: "synthetic-fixture",
      createdAt: "2026-01-01T00:00:00.000Z",
      commands: [],
    } as unknown as CanduReplayArchive;

    expect(() => parseReplayArchive(serializeReplayArchive(archive))).toThrow(/candu-playtest-v1/);
  });
});
