import { defineConfig } from "vite";
import { offlinePlugin } from './offlinePlugin';

export default defineConfig({
  plugins: [offlinePlugin()],
  build: {
    target: "es2022",
    sourcemap: true,
  },
});
