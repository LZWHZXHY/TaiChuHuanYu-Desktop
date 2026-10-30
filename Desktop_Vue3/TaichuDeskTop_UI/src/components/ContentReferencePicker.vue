<template>
  <details class="reference-picker" :open="open" @toggle="open = ($event.target as HTMLDetailsElement).open">
    <summary>引用社区内容（可选）</summary>
    <p>引用会插入正文链接，不会改变内容归属或标签。</p>
    <form @submit.prevent="search(1)">
      <select v-model="type" aria-label="引用类型" @change="search(1)"><option v-for="(label, key) in knowledgeTypes" :key="key" :value="key">{{ label }}</option></select>
      <input v-model="query" aria-label="查找引用内容" maxlength="100" placeholder="搜索文章、帖子、画作或公共知识" />
      <button :disabled="loading">搜索</button>
    </form>
    <p v-if="loading" role="status">正在查找…</p>
    <p v-else-if="error" role="alert">{{ error }}</p>
    <ul v-else>
      <li v-for="item in items" :key="item.type + item.id">
        <button type="button" @click="select(item)">
          <span>{{ knowledgeTypes[item.type] }}</span>
          {{ item.title }}
          <small>{{ item.author }}</small>
        </button>
      </li>
    </ul>
    <p v-if="searched && !loading && !error && !items.length">没有找到公开内容，请换个关键词。</p>
    <nav v-if="total > 24" aria-label="引用结果分页"><button type="button" :disabled="loading || page === 1" @click="search(page - 1)">上一页</button><span>{{ page }} / {{ Math.ceil(total / 24) }}</span><button type="button" :disabled="loading || page * 24 >= total" @click="search(page + 1)">下一页</button></nav>
  </details>
  <p v-if="message" role="status">{{ message }}</p>
</template>

<script setup lang="ts">
import { ref, watch, onUnmounted } from 'vue';
import { useRouter } from 'vue-router';
import { knowledgeApi, knowledgeTypes, knowledgeRoute, type KnowledgeType, type KnowledgeItem } from '@/api/knowledge';
import { brushApi } from '@/views/画廊展厅/茶绘/brushApi';
const emit = defineEmits<{
    select: [
        value: {
            title: string;
            href: string;
        }
    ];
}>();
const router = useRouter(), open = ref(false), query = ref(''), loading = ref(false), error = ref(''), searched = ref(false), items = ref<KnowledgeItem[]>([]);
let version = 0;
const type = ref<KnowledgeType>('all'), page = ref(1), total = ref(0), message = ref('');
function href(item: KnowledgeItem) {
    return router.resolve(knowledgeRoute(item)).href;
}
function select(item: KnowledgeItem) {
    emit('select', { title: item.title, href: href(item) });
    open.value = false;
    message.value = `已插入引用：${item.title}`;
}
async function search(nextPage = 1) {
    const current = ++version;
    loading.value = true;
    error.value = '';
    searched.value = true;
    try {
        let res;
        if (type.value === 'resource') {
            const resources = await knowledgeApi.resources();
            if (!resources.success) throw new Error();
            const matches = resources.data.filter(file => file.name.toLowerCase().includes(query.value.trim().toLowerCase()));
            res = { total: matches.length, items: matches.slice((nextPage - 1) * 24, nextPage * 24).map(file => ({ id: file.name, type: 'resource' as const, title: file.name, summary: '', author: '', tags: '', updatedAt: '' })) };
        } else if (type.value === 'brush') {
            // 茶绘笔刷不在资料目录里，直接查笔刷库
            const r = await brushApi.square({ q: query.value.trim() || undefined, offset: (nextPage - 1) * 24, limit: 24 });
            res = { total: r.total, items: r.items.map(b => ({ id: b.id, type: 'brush' as const, title: b.name, summary: `${b.ownerName} 做的茶绘笔刷`, author: b.ownerName, tags: '', updatedAt: b.updatedAt })) };
        } else res = await knowledgeApi.search({ type: type.value, search: query.value.trim(), page: nextPage });
        if (current === version) {
            items.value = res.items;
            total.value = res.total;
            page.value = nextPage;
        }
    }
    catch {
        if (current === version)
            error.value = '引用目录加载失败，请重新搜索。';
    }
    finally {
        if (current === version)
            loading.value = false;
    }
}
watch(open, value => { if (value && !searched.value)
    void search(); });
onUnmounted(() => version++);
</script>

<style scoped>
.reference-picker {
  border: 1px solid var(--border-line);
  border-radius: 0;
  padding: 12px;
  margin: 12px 0;
  font-size: .9rem;
}
summary {
  cursor: pointer;
}
p, small {
  color: var(--text-secondary);
}
form {
  display: flex;
  gap: 8px;
}
input {
  flex: 1;
  min-width: 0;
  padding: 10px;
}
input, button, select {
  color: var(--text-primary);
  background: var(--paper-card);
  border: 1px solid var(--border-line);
  border-radius: 0;
}
form { flex-wrap: wrap; }
nav { display: flex; gap: 8px; align-items: center; }
button {
  padding: 8px 12px;
  cursor: pointer;
}
ul {
  list-style: none;
  padding: 0;
  max-height: 250px;
  overflow-y: auto;
}
li {
  margin: 6px 0;
}
li button {
  width: 100%;
  text-align: left;
}
span {
  color: var(--accent-color);
  margin-right: 8px;
}
</style>
