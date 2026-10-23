import { defineConfig } from "vitest/config";
import { fileURLToPath } from "node:url";

export default defineConfig({
  server: {
    fs: { allow: [".", fileURLToPath(new URL("../../docs/code-library/manual", import.meta.url))] },
  },
  test: {
    environment: "jsdom",
    globals: true,
    include: ["src/**/*.test.ts", "tests/**/*.test.mjs"],
  },
});
