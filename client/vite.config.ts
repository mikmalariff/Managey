import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// The production build is served by the ASP.NET Core app from its wwwroot folder.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: { '/api': 'http://localhost:5080' },
  },
  build: {
    outDir: '../server/Managey.Api/wwwroot',
    emptyOutDir: true,
  },
})
