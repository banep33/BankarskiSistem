<script setup lang="ts">
import UsersTable from '@/components/banking/UsersTable.vue'
</script>

<template>
  <div class="page">
    <header class="page-header">
      <div>
        <h1 class="page-title">Klijenti</h1>
        <p class="page-subtitle">Pretraga klijenata, provera identiteta (KYC) i otvaranje računa. Svaki uvid u podatke klijenta se evidentira.</p>
      </div>
    </header>
    <UsersTable fixed-role="Korisnik" :allow-roles="['Korisnik']" />
  </div>
</template>
