<script setup lang="ts">
import UsersTable from '@/components/banking/UsersTable.vue'
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Korisnici</h1>
        <p class="page-subtitle">Svi korisnici sistema: klijenti, bankari i administratori.</p>
      </div>
    </header>
    <UsersTable :allow-roles="['Korisnik', 'Bankar', 'Administrator']" />
  </div>
</template>
