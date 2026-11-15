import { createApp } from 'vue'
import { createPinia } from 'pinia'
import '@fontsource/instrument-sans/400.css'
import '@fontsource/instrument-sans/500.css'
import '@fontsource/instrument-sans/600.css'
import '@fontsource/instrument-sans/700.css'
import './assets/styles/tokens.css'
import './assets/styles/base.css'
import App from './App.vue'
import router from './router'
import { onUnauthorized } from './api/http'
import { useAuthStore } from './stores/auth'
import { useUiStore } from './stores/ui'

const app = createApp(App)
app.use(createPinia())
app.use(router)

onUnauthorized(() => {
  const auth = useAuthStore()
  if (!auth.isAuthenticated) return
  auth.clear()
  useUiStore().notify('Sesija je istekla ili je odjavljena sa drugog uređaja. Prijavite se ponovo.', 'info')
  router.push({ path: '/prijava', query: { razlog: 'sesija' } })
})

app.mount('#app')
