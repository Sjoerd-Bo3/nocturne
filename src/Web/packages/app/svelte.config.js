import adapter from "@sveltejs/adapter-node";
import { vitePreprocess } from "@sveltejs/vite-plugin-svelte";

export default {
  preprocess: vitePreprocess(),
  kit: {
    alias: {
      $lib: "./src/lib",
      $api: "./src/lib/api/",
      "$api-clients": "./src/lib/api/generated/nocturne-api-client",
      $routes: "./src/routes",
    },
    adapter: adapter({
      // Output directory for the built server
      out: "build",
      // Enable precompression
      precompress: true,
    }),
    experimental: {
      remoteFunctions: true,
      tracing: {
        server: true
      },
      instrumentation: {
        server: true
      }
    },
  },
  compilerOptions: {
    experimental: {
      async: true,
    },
  },
};
