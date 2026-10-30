<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import request from '@/utils/request';

interface ItemDto {
  id: string;
  title: string;
  authorName: string;
  resonance: number;
  publishedAt: string;
}

const props = withDefaults(defineProps<{ type?: 'blog' | 'post' }>(), { type: 'blog' });

const list = ref<ItemDto[]>([]);
const loading = ref(false);
const label = computed(() => (props.type === 'post' ? '帖子' : '博客'));

async function fetchList() {
  loading.value = true;
  try {
    const res = await request.get<{ items: ItemDto[]; totalCount: number }>(`/admin/product/${props.type}`);
    list.value = res.items || [];
  } catch (err) {
    console.error('拉取内容失败', err);
  } finally {
    loading.value = false;
  }
}

async function handleDelete(id: string, title: string) {
  if (!confirm(`确定要彻底抹除${label.value}《${title}》吗？正文块会一起删除。`)) return;
  try {
    await request.delete(`/admin/product/${props.type}/${id}`);
    fetchList();
  } catch {
    // request.ts 拦截器已统一提示
  }
}

onMounted(fetchList);
watch(() => props.type, fetchList);
</script>

<template>
  <div class="blog-sub-module">
    <table class="ink-table">
      <thead>
        <tr>
          <th>标题</th>
          <th>作者</th>
          <th>共鸣值</th>
          <th>操作</th>
        </tr>
      </thead>
      <tbody>
        <tr v-if="loading">
          <td colspan="4" class="empty-cell">正在读取{{ label }}…</td>
        </tr>
        <tr v-else-if="!list.length">
          <td colspan="4" class="empty-cell">还没有已发布的{{ label }}</td>
        </tr>
        <tr v-for="item in list" v-else :key="item.id">
          <td>{{ item.title }}</td>
          <td>{{ item.authorName }}</td>
          <td>{{ item.resonance }}</td>
          <td>
            <button class="btn-action danger" @click="handleDelete(item.id, item.title)">删除</button>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
.empty-cell { text-align: center; padding: 40px 20px; color: var(--text-secondary); font-size: .85rem; }

/* 容器基调 */
.blog-sub-module {
  animation: slideIn 0.35s cubic-bezier(0.16, 1, 0.3, 1);
  padding: 0 4px;
}

/* 墨水风表格增强 */
.ink-table {
  width: 100%;
  border-collapse: separate; /* 使用 separate 以便控制圆角 */
  border-spacing: 0;
  margin-top: 16px;
  background: var(--paper-card);
  border: 1px solid var(--border-line);
  border-radius: 0;
  overflow: hidden;
}

/* 表头：极简、窄距、大写字母 */
.ink-table th {
  text-align: left;
  font-size: 0.7rem;
  font-weight: 700;
  color: var(--text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.08em;
  padding: 16px 20px;
  background: var(--bg-main);
  border-bottom: 1px solid var(--border-line);
}

/* 行：悬浮时的微妙灰度切换 */
.ink-table td {
  padding: 16px 20px;
  font-size: 0.85rem;
  color: var(--text-primary);
  border-bottom: 1px solid var(--border-line);
  transition: background 0.2s ease;
}

.ink-table tr:hover td {
  background: var(--paper-card);
}

/* 按钮：隐形到突显的交互 */
.btn-action {
  background: none;
  border: 1px solid transparent;
  padding: 4px 8px;
  font-size: 0.75rem;
  font-weight: 600;
  cursor: pointer;
  border-radius: 0;
  transition: all 0.2s;
}

.btn-action.danger {
  color: var(--text-secondary);
}

.btn-action.danger:hover {
  color: var(--danger);
  border-color: var(--danger-soft);
  background: var(--bg-sub);
}

/* 补充：如果没有数据时的空状态 */
.empty-msg {
  text-align: center;
  padding: 40px;
  color: var(--text-secondary);
  font-size: 0.8rem;
  font-style: italic;
}

@keyframes slideIn { 
  from { opacity: 0; transform: translateY(10px); } 
  to { opacity: 1; transform: translateY(0); } 
}
</style>