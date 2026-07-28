import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// Em desenvolvimento o Vite roda em 5173 e encaminha /api para a API em 5080.
// Em producao o build cai em wwwroot e e servido pela propria API (mesma origem).
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: false,
      },
    },
  },
  build: {
    outDir: '../backend/src/Domus.Api/wwwroot',
    emptyOutDir: true,
  },
})
