<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
</script>

<template>
  <main class="not-found">
    <p class="code">404</p>
    <h1>Stranica ne postoji</h1>
    <p class="muted">Adresa je možda pogrešno uneta ili stranica više nije dostupna.</p>
    <RouterLink :to="auth.isAuthenticated ? auth.homeRoute : '/prijava'" class="back">Nazad na početnu</RouterLink>
  </main>
</template>

<style scoped>
.not-found {
  min-height: 100vh;
  display: grid;
  place-content: center;
  justify-items: center;
  gap: var(--space-3);
  text-align: center;
  padding: var(--space-6);
}

.code {
  font-size: 5rem;
  font-weight: 700;
  color: var(--color-primary);
  letter-spacing: -0.05em;
}

.back {
  margin-top: var(--space-4);
  padding: 0.65rem 1.2rem;
  border-radius: var(--radius-pill);
  background: var(--color-forest-900);
  color: #fff;
  text-decoration: none;
  font-weight: 600;
}
</style>
