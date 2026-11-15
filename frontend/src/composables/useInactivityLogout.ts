import { onBeforeUnmount, onMounted, ref } from 'vue'

const ACTIVITY_EVENTS = ['mousemove', 'keydown', 'pointerdown', 'scroll', 'touchstart'] as const

export function useInactivityLogout(onTimeout: () => void) {
  const totalMinutes = Number(import.meta.env.VITE_INACTIVITY_MINUTES ?? 5)
  const warningSeconds = 60
  const isWarningVisible = ref(false)
  const secondsLeft = ref(warningSeconds)

  let lastActivity = Date.now()
  let timer: number | undefined

  function registerActivity(): void {
    if (!isWarningVisible.value) lastActivity = Date.now()
  }

  function stayLoggedIn(): void {
    lastActivity = Date.now()
    isWarningVisible.value = false
  }

  function tick(): void {
    const idleSeconds = (Date.now() - lastActivity) / 1000
    const limitSeconds = totalMinutes * 60
    if (idleSeconds >= limitSeconds) {
      isWarningVisible.value = false
      onTimeout()
      return
    }
    if (idleSeconds >= limitSeconds - warningSeconds) {
      isWarningVisible.value = true
      secondsLeft.value = Math.max(0, Math.ceil(limitSeconds - idleSeconds))
    }
  }

  onMounted(() => {
    ACTIVITY_EVENTS.forEach((event) => window.addEventListener(event, registerActivity, { passive: true }))
    timer = window.setInterval(tick, 1000)
  })

  onBeforeUnmount(() => {
    ACTIVITY_EVENTS.forEach((event) => window.removeEventListener(event, registerActivity))
    window.clearInterval(timer)
  })

  return { isWarningVisible, secondsLeft, stayLoggedIn, totalMinutes }
}
