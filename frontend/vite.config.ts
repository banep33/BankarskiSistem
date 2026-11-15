import { fileURLToPath, URL } from 'node:url'
import { defineConfig, loadEnv, type Plugin } from 'vite'
import vue from '@vitejs/plugin-vue'

function apiOrigin(baseUrl: string | undefined): string | null {
  if (!baseUrl || !/^https?:\/\//.test(baseUrl)) return null
  return new URL(baseUrl).origin
}

function contentSecurityPolicy(isDev: boolean, extraConnect: string | null, includeFrameAncestors: boolean): string {
  const connect = ["'self'", extraConnect, isDev ? 'ws:' : null].filter(Boolean).join(' ')
  return [
    "default-src 'self'",
    "script-src 'self'",
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data:",
    "font-src 'self'",
    `connect-src ${connect}`,
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    includeFrameAncestors ? "frame-ancestors 'none'" : null,
  ]
    .filter(Boolean)
    .join('; ')
}

function securityHeaders(isDev: boolean, extraConnect: string | null): Record<string, string> {
  return {
    'Content-Security-Policy': contentSecurityPolicy(isDev, extraConnect, true),
    'X-Content-Type-Options': 'nosniff',
    'X-Frame-Options': 'DENY',
    'Referrer-Policy': 'no-referrer',
    'Permissions-Policy': 'camera=(), microphone=(), geolocation=()',
  }
}

function cspMeta(isDev: boolean, extraConnect: string | null): Plugin {
  return {
    name: 'bankarski-csp-meta',
    transformIndexHtml: () => [
      { tag: 'meta', attrs: { 'http-equiv': 'Content-Security-Policy', content: contentSecurityPolicy(isDev, extraConnect, false) }, injectTo: 'head-prepend' },
    ],
  }
}

export default defineConfig(({ mode, command }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const isDev = command === 'serve'
  const extraConnect = apiOrigin(env.VITE_API_BASE_URL)

  return {
    plugins: [vue(), cspMeta(isDev, extraConnect)],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    server: {
      port: 5173,
      headers: securityHeaders(true, extraConnect),
      proxy: {
        '/api': {
          target: env.VITE_API_PROXY_TARGET || 'http://localhost:5039',
          changeOrigin: true,
        },
      },
    },
    preview: {
      headers: securityHeaders(false, extraConnect),
    },
    build: {
      sourcemap: false,
    },
  }
})
