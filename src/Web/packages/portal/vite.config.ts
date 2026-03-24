import { sveltekit } from '@sveltejs/kit/vite';
import tailwindcss from '@tailwindcss/vite';
import { wuchale } from '@wuchale/vite-plugin';
import lingo from 'vite-plugin-lingo';
import { defineConfig } from 'vite';


export default defineConfig({
  plugins: [tailwindcss(), wuchale(),
    lingo({
      route: '/_translations',  // Route where editor UI is served
      localesDir: '../../locales',  // Path to .po files
    }), sveltekit()],
  server: {
    host: "0.0.0.0",
    port: parseInt(process.env.PORT || "5173", 10),
    strictPort: true,
  },
  ssr: {
    noExternal: ['@nocturne/app', 'lucide-svelte']
  }
});
