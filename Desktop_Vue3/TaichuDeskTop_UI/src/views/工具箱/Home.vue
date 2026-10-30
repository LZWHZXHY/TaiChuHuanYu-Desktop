<script setup lang = "ts">
import { RouterLink } from 'vue-router';
const modules = import.meta.glob('./*/index.vue')

interface ToolMeta {
  title: string
  description: string
  tags: string[]
}

const metas = import.meta.glob<{ default: ToolMeta }>('./*/meta.ts', { eager: true })

const tools = Object.entries(modules).map(([path, component]) => {
  const name = path.split('/')[1]        // 'percolation'
  const meta = metas[`./${name}/meta.ts`]?.default ?? {
        title: "未捕捉到正确的模型名字",
        description: '未捕捉到正确的模型描述',
        tags: [],
    }
  return { name, component, meta }
})

console.log('合并后的 tools：', tools)

</script>

<template>

    <div class = "home">
        <header class="home-header">
            <h1>工具箱</h1>
            <p>一些自己写着玩、顺手验证想法的小东西。</p>
        </header>

        <div class="tool-grid">
            <RouterLink v-for="tool in tools" :key="tool.name" class="tool-card" :to="`/toolBox/${tool.name}`">
            <h2 class="tool-title">{{ tool.meta.title }}</h2>
            <p class="tool-desc">{{ tool.meta.description }}</p>
            <div class="tool-tags">
                <span v-for="tag in tool.meta.tags" :key="tag" class="tool-tag">{{ tag }}</span>
            </div>
            </RouterLink>
        </div>

    </div>


</template>

<style scoped>
.home {
  max-width: 1100px;
  margin: 0 auto;
  padding: 40px 20px;
}

.home-header h1 {
  font-size: 28px;
  margin: 0 0 8px;
  color: var(--tb-text);
}

.home-header p {
  font-size: 14px;
  color: var(--tb-text-muted);
  margin: 0;
}

.tool-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 16px;
  margin-top: 28px;
}

.tool-card {
  text-decoration: none;
  color: inherit;
  background: var(--tb-card-bg);
  border: 1px solid var(--tb-border);
  border-radius: 0;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 8px;
  transition: 0.15s;
}

.tool-card:hover {
  transform: translateY(-2px);
  border-color: var(--tb-accent);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
}

.tool-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--tb-text);
  margin: 0;
}

.tool-desc {
  font-size: 13px;
  color: var(--tb-text-muted);
  line-height: 1.6;
  margin: 0;
  flex: 1;
}

.tool-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: auto;
}

.tool-tag {
  font-size: 11px;
  padding: 2px 8px;
  border-radius: 0;
  background: var(--tb-bg);
  color: var(--tb-text-muted);
}
</style>