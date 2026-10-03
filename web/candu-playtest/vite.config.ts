import { defineConfig, loadEnv } from "vite";

// Threaded WASM is an opt-in benchmark build. Shared memory requires isolation.
export default defineConfig(({ mode }) => {
  const headers = loadEnv(mode, ".", "CANDU_").CANDU_CPU_THREADS === "1" ? {
    "Cross-Origin-Opener-Policy": "same-origin",
    "Cross-Origin-Embedder-Policy": "require-corp",
  } : undefined;

  return {
    // Research previews receive only their explicitly staged runtime.
    publicDir: mode === "research" ? false : "public",
    server: { headers },
    preview: { headers },
    build: {
      target: "es2022",
      sourcemap: true,
    },
  };
});
