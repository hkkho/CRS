import { defineConfig } from "vite";
import { fileURLToPath } from "node:url";
import { offlinePlugin } from './offlinePlugin';

export default defineConfig({
  plugins: [offlinePlugin()],
  server: {
    fs: { allow: [".", fileURLToPath(new URL("../../docs/code-library/manual", import.meta.url))] },
  },
  build: {
    target: "es2022",
    sourcemap: true,
  },
});
