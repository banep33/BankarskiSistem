<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, RouterView, useRouter } from 'vue-router'
import AppButton from '@/components/ui/AppButton.vue'
import { useAuthStore } from '@/stores/auth'
import { publicOrigin } from '@/utils/publicOrigin'

const auth = useAuthStore()
const router = useRouter()

const backTarget = computed(() => publicOrigin(auth.isAuthenticated ? auth.homeRoute : '/prijava'))
const backTitle = computed(() => String(router.resolve(backTarget.value).meta.title ?? 'prethodna stranica'))

function goBack(): void {
  router.push(backTarget.value)
}
</script>

<template>
  <div class="public">
    <header class="public-header">
      <RouterLink to="/prijava" class="brand">Bankarski Sistem</RouterLink>
      <RouterLink :to="auth.isAuthenticated ? auth.homeRoute : '/prijava'" class="cta">
        {{ auth.isAuthenticated ? 'U aplikaciju' : 'Prijava' }}
      </RouterLink>
      <nav class="public-nav" aria-label="Javne stranice">
        <RouterLink to="/javno/pomoc">Pomoć i cenovnik</RouterLink>
        <RouterLink to="/javno/dokumenti/privatnost">Privatnost</RouterLink>
        <RouterLink to="/javno/dokumenti/uslovi">Uslovi</RouterLink>
      </nav>
    </header>
    <main class="public-content">
      <AppButton class="back" variant="ghost" size="sm" icon="arrow-left" :title="`Povratak na: ${backTitle}`" :aria-label="`Nazad na stranicu ${backTitle}`" @click="goBack">
        Nazad
      </AppButton>
      <RouterView />
    </main>
  </div>
</template>

<style scoped>
.public {
  min-height: 100vh;
}

.public-header {
  display: grid;
  grid-template-columns: auto 1fr auto;
  grid-template-areas: 'brand nav cta';
  align-items: center;
  gap: var(--space-4);
  max-width: var(--content-max-width);
  margin: var(--space-4) auto 0;
  padding: var(--space-3) var(--space-6);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-pill);
  background: var(--color-surface);
  box-shadow: var(--shadow-sm);
}

.brand {
  grid-area: brand;
  font-weight: 700;
  text-decoration: none;
  font-size: var(--font-size-lg);
  white-space: nowrap;
}

.public-nav {
  grid-area: nav;
  display: flex;
  justify-content: center;
  gap: var(--space-2);
}

.public-nav a {
  padding: 0.4rem 0.8rem;
  border-radius: var(--radius-pill);
  text-decoration: none;
  font-weight: 500;
  font-size: var(--font-size-sm);
  white-space: nowrap;
  color: var(--color-text-muted);
}

.public-nav a:hover,
.public-nav a.router-link-active {
  background: var(--color-surface-muted);
  color: var(--color-text);
}

.cta {
  grid-area: cta;
  padding: 0.45rem 1rem;
  border-radius: var(--radius-pill);
  background: var(--color-primary);
  color: #fff;
  font-weight: 600;
  font-size: var(--font-size-sm);
  text-decoration: none;
  white-space: nowrap;
}

@media (max-width: 720px) {
  .public-header {
    grid-template-columns: 1fr auto;
    grid-template-areas:
      'brand cta'
      'nav nav';
    margin: var(--space-3) var(--space-3) 0;
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-lg);
    row-gap: var(--space-3);
  }

  .public-nav {
    display: grid;
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: var(--space-1);
    padding-top: var(--space-3);
    border-top: 1px solid var(--color-border);
  }

  .public-nav a {
    padding: 0.45rem 0.25rem;
    text-align: center;
    font-size: var(--font-size-xs);
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .public-content {
    padding: var(--space-5) var(--space-4);
  }
}

.public-content {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  max-width: 960px;
  margin: 0 auto;
  padding: var(--space-8) var(--space-4);
}

.back {
  align-self: flex-start;
}
</style>
