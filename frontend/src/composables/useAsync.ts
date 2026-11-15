import { ref, type Ref } from 'vue'
import { ApiError } from '@/api/http'

export function useAsync<T>(loader: () => Promise<T>, initial: T) {
  const data = ref(initial) as Ref<T>
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  async function load(): Promise<void> {
    isLoading.value = true
    error.value = null
    try {
      data.value = await loader()
    } catch (caught) {
      error.value = errorMessage(caught)
    } finally {
      isLoading.value = false
    }
  }

  return { data, isLoading, error, load }
}

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message
  return 'Došlo je do neočekivane greške.'
}
