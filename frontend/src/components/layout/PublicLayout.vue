<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
</script>

<template>
  <div class="public">
    <header class="public-header">
      <RouterLink to="/prijava" class="brand">Bankarski Sistem</RouterLink>
      <nav class="row">
        <RouterLink to="/javno/pomoc">Pomoć i cenovnik</RouterLink>
        <RouterLink to="/javno/dokumenti/privatnost">Privatnost</RouterLink>
        <RouterLink to="/javno/dokumenti/uslovi">Uslovi</RouterLink>
        <RouterLink :to="auth.isAuthenticated ? auth.homeRoute : '/prijava'" class="cta">
          {{ auth.isAuthenticated ? 'Nazad u aplikaciju' : 'Prijava' }}
        </RouterLink>
      </nav>
    </header>
    <main class="public-content">
      <RouterView />
    </main>
  </div>
</template>

<style scoped>
.public {
  min-height: 100vh;
}

.public-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: var(--space-4);
  max-width: var(--content-max-width);
  margin: var(--space-4) auto 0;
  padding: var(--space-3) var(--space-6);
  border-radius: var(--radius-pill);
  background: var(--color-surface);
  box-shadow: var(--shadow-sm);
}

.brand {
  font-weight: 700;
  text-decoration: none;
  font-size: var(--font-size-lg);
}

nav a {
  text-decoration: none;
  font-weight: 500;
  font-size: var(--font-size-sm);
}

.cta {
  padding: 0.45rem 1rem;
  border-radius: var(--radius-pill);
  background: var(--color-primary);
  color: #fff;
}

.public-content {
  max-width: 960px;
  margin: 0 auto;
  padding: var(--space-8) var(--space-4);
}
</style>
