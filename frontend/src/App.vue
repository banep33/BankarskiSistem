<script setup lang="ts">
import { RouterView } from 'vue-router'
import ToastHost from '@/components/ui/ToastHost.vue'
import ReauthDialog from '@/components/security/ReauthDialog.vue'
</script>

<template>
  <RouterView />
  <ToastHost />
  <ReauthDialog />
</template>
