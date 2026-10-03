import { readFileSync } from "node:fs";
import { gunzipSync } from "node:zlib";
import { createHash } from "node:crypto";
import { expect, it } from "vitest";
import { parseProtocolResponse, parseProtocolSnapshot } from "../src/protocol";

it("strictly consumes the same full, compact, replacement, failure, resync and reset bytes as C#", () => {
  const rows = JSON.parse(gunzipSync(readFileSync("../../tests/Fixtures/playtest-v2.json.gz")).toString("utf8"));
  expect(rows.map(row => row.name)).toEqual(["full", "compact", "replacement", "failure", "resync", "reset"]);
  let previous;
  for (const row of rows) {
    expect(createHash("sha256").update(row.response).digest("hex")).toBe(row.sha256);
    const wire = JSON.parse(row.response);
    if (row.name === "resync") {
      expect(() => parseProtocolResponse(row.response, previous)).toThrow();
      const authoritative = parseProtocolSnapshot(row.resyncSnapshot);
      const response = parseProtocolResponse(row.response, previous, authoritative);
      expect(response.requiresResync).toBe(true);
      expect(response.snapshot.sequence).toBe(previous.sequence);
      continue;
    }
    const response = parseProtocolResponse(row.response, previous);
    expect(response.snapshot.core.channels).toHaveLength(380);
    expect(response.snapshot.core.channels[210].bundles).toHaveLength(12);
    if (row.name === "compact" || row.name === "failure") {
      expect(wire).not.toHaveProperty("snapshot");
      expect(wire).not.toHaveProperty("coreReplacement");
      expect(response.snapshot.core).toBe(previous.core);
    }
    if (row.name === "replacement") {
      expect(wire.coreReplacement.channels).toHaveLength(380);
      expect(response.snapshot.core).not.toBe(previous.core);
      expect(response.snapshot.refuellingOperationCount).toBe(1);
    }
    expect(response.accepted).toBe(row.name !== "failure");
    if (row.name === "reset") {
      expect(response.sequence).toBe(1);
      expect(response.snapshot.refuellingOperationCount).toBe(0);
      expect(response.snapshot.simulationTimeSeconds).toBe(0);
    }
    previous = response.snapshot;
  }
});
