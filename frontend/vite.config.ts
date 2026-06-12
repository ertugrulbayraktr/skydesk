/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      // Plain HTTP in dev: the API listens on 5098 under both launch profiles,
      // and HTTPS redirection is disabled in Development (see Program.cs), so
      // no self-signed certificate handling is needed here.
      '/api': {
        target: 'http://localhost:5098',
        changeOrigin: true,
      },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
  },
})
