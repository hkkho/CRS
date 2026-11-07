# 2. Browser commands and transport

[bridge.ts](../../web/candu-playtest/src/bridge.ts) serializes requests and tracks their IDs. [wasmWorker.ts](../../web/candu-playtest/src/wasmWorker.ts) loads the staged WASM module and calls the [managed exports](../../src/ReactorSim.BrowserHost/BrowserExports.cs). The instance [PlaytestRuntime](../../src/ReactorSim.Browser/PlaytestRuntime.cs) owns the session, sequences and replay digest.

```annotated-source
path: web/candu-playtest/src/wasmWorker.ts
title: "Serialize requests in the worker"
language: typescript
start: "scope.addEventListener(\"message\", (event) => {"
end: "      scope.postMessage({ type: \"error\", id: event.data.id, error: formatError(error) });"
steps:
- through: "      const resultJson = await dispatchRequest(api, event.data);"
  explanation: "Chain the request behind its predecessor and wait for the managed exports before executing it."
- through: "      scope.postMessage({ type: \"error\", id: event.data.id, error: formatError(error) });"
  explanation: "Return the response and timings with the same ID. Transport failure is reported separately from a rejected simulation command."
```

```annotated-source
path: web/candu-playtest/src/wasmWorker.ts
title: "Yield between managed transaction phases"
language: typescript
start: "        if ((root.payload ?? root).type === \"commit-day\") {"
end: "          return normalizeJson(progress.responseJson);"
steps:
- through: "            await new Promise(resolve => setTimeout(resolve, 0));"
  explanation: "Begin the shared transaction and publish progress before yielding the worker event loop."
- through: "          return normalizeJson(progress.responseJson);"
  explanation: "Continue the managed operation until it produces the final response. Later requests remain queued."
```

The [protocol parser](../../web/candu-playtest/src/protocol.ts) materializes a compact patch only against its declared base sequence. A mismatch requests a full snapshot. WASM load failure disables operating controls. See the [wire protocol](../spec/browser-playtest-protocol-v2.md) for identities and failure semantics.
