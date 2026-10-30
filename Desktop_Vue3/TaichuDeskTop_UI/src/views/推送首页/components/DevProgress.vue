<template>
  <section id="dev-progress" class="dev-progress" aria-label="官方开发进度">
    <header class="dp-header">
      <h2 class="dp-title">🚧 开发进度</h2>
      <RouterLink to="/dev-progress" class="dp-all">查看全部 →</RouterLink>
    </header>

    <p v-if="loading" class="dp-empty">加载中…</p>
    <p v-else-if="!visibleProjects.length" class="dp-empty">暂无进行中的项目</p>
    <ul v-else class="dp-list">
      <li v-for="p in visibleProjects.slice(0, 6)" :key="p.id" class="dp-item">
        <span class="dp-icon">{{ p.icon }}</span>
        <span class="dp-name">{{ p.name }}</span>
        <span class="dp-bar"><span class="dp-fill" :style="{ width: p.progress + '%' }"></span></span>
        <span class="dp-pct">{{ p.progress }}%</span>
      </li>
    </ul>
  </section>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { devProjectApi, type DevProject } from '@/api/devProject'

const projects = ref<DevProject[]>([])
const loading = ref(false)

// 已全部完成的项目（自身 100%，且所有子项目也 100%）在首页省略
const visibleProjects = computed(() => {
  const list = projects.value
  const childrenOf = new Map<string, DevProject[]>()
  for (const p of list) {
    if (p.parentId) {
      const arr = childrenOf.get(p.parentId) || []
      arr.push(p)
      childrenOf.set(p.parentId, arr)
    }
  }
  const isDone = (p: DevProject): boolean => {
    const kids = childrenOf.get(p.id) || []
    const selfDone = (p.progress ?? 0) >= 100
    if (!kids.length) return selfDone
    return selfDone && kids.every(k => (k.progress ?? 0) >= 100)
  }
  return list.filter(p => !isDone(p))
})

onMounted(async () => {
  loading.value = true
  try {
    const data = await devProjectApi.list()
    projects.value = Array.isArray(data) ? data : []
  } catch { projects.value = [] } finally { loading.value = false }
})
</script>

<style scoped>
.dev-progress { background: var(--paper-card); border: 1px solid var(--border-line); border-radius: 0; padding: 20px; }
.dp-header { display: flex; align-items: baseline; gap: 10px; margin-bottom: 14px; }
.dp-title { margin: 0; font-size: 1.05rem; }
.dp-all { margin-left: auto; color: var(--accent-color); font-size: .8rem; text-decoration: none; white-space: nowrap; }
.dp-all:hover { text-decoration: underline; }
.dp-empty { color: var(--text-secondary); text-align: center; padding: 16px 0; font-size: .82rem; }
.dp-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 12px; }
.dp-item { display: flex; align-items: center; gap: 10px; }
.dp-icon { font-size: 1.1rem; flex-shrink: 0; }
.dp-name { flex: 0 0 auto; max-width: 40%; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: .88rem; }
.dp-bar { flex: 1; height: 6px; background: var(--bg-sub); border-radius: 0; overflow: hidden; }
.dp-fill { display: block; height: 100%; background: linear-gradient(90deg, var(--accent-color), var(--success)); border-radius: 0; }
.dp-pct { flex: 0 0 auto; font-size: .78rem; color: var(--text-secondary); font-variant-numeric: tabular-nums; min-width: 38px; text-align: right; }
</style>
