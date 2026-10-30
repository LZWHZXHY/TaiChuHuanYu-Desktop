<script setup lang="ts">
// /user/:id 的入口：看自己时打开个人中心，看别人（或自己加 ?view=public 预览）时打开公开主页。
import { computed, defineAsyncComponent } from 'vue'
import { useRoute } from 'vue-router'
import { useUserStore } from '@/stores/user'

const props = defineProps<{ id: string }>()
const route = useRoute()
const userStore = useUserStore()
const isSelf = computed(() => userStore.isLoggedIn && String(userStore.userId || '').toLowerCase() === String(props.id || '').toLowerCase())
const showCenter = computed(() => isSelf.value && route.query.view !== 'public')

const MyCenter = defineAsyncComponent(() => import('./index.vue'))
const PublicProfile = defineAsyncComponent(() => import('./PublicProfile.vue'))
</script>

<template>
  <MyCenter v-if="showCenter" />
  <PublicProfile v-else :id="id" :key="id" :is-self="isSelf" />
</template>
