<template><p role="status">正在打开知识库…</p></template>
<script setup lang="ts">
import { onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
const route = useRoute(), router = useRouter();
onMounted(() => router.replace({ name: 'KnowledgeHome', query: { ...route.query, type: 'wiki' } }));
</script>
