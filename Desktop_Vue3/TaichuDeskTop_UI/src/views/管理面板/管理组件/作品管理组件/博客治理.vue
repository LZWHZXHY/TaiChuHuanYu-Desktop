<script setup lang="ts">
import { ref, onMounted } from 'vue';
import request from '@/utils/request'; // 直接引入你的请求工具

// 定义接口类型
interface BlogDto {
  id: string;
  title: string;
  authorName: string;
  resonance: number;
  publishedAt: string;
}

const blogList = ref<BlogDto[]>([]);
const loading = ref(false);

// 1. 获取博客列表
const fetchBlogs = async () => {
  loading.value = true;
  try {
    // 直接在这里调用 API，省去了单独的 API 文件
    const res = await request.get<{ items: BlogDto[], totalCount: number }>('/admin/product/blog');
    blogList.value = res.items;
  } catch (err) {
    console.error('拉取博客失败', err);
  } finally {
    loading.value = false;
  }
};

// 2. 删除逻辑 (直接调用)
const handleDelete = async (id: string, title: string) => {
  if (!confirm(`确定要彻底抹除《${title}》吗？`)) return;
  
  try {
    await request.delete(`/admin/product/blog/${id}`);
    fetchBlogs(); // 操作成功后刷新
  } catch (err) {
    // request.ts 里的拦截器已经处理了报错弹窗，这里无需额外代码
  }
};

onMounted(fetchBlogs);
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
        <tr v-for="blog in blogList" :key="blog.id">
          <td>{{ blog.title }}</td>
          <td>{{ blog.authorName }}</td>
          <td>{{ blog.resonance }}</td>
          <td>
            <button class="btn-action danger" @click="handleDelete(blog.id, blog.title)">删除</button>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
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
  background: #fff;
  border: 1px solid #f0f0f0;
  border-radius: 12px;
  overflow: hidden;
}

/* 表头：极简、窄距、大写字母 */
.ink-table th {
  text-align: left;
  font-size: 0.7rem;
  font-weight: 700;
  color: #a0a0a0;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  padding: 16px 20px;
  background: #fafafa;
  border-bottom: 1px solid #f0f0f0;
}

/* 行：悬浮时的微妙灰度切换 */
.ink-table td {
  padding: 16px 20px;
  font-size: 0.85rem;
  color: #333;
  border-bottom: 1px solid #f7f7f7;
  transition: background 0.2s ease;
}

.ink-table tr:hover td {
  background: #fdfdfd;
}

/* 按钮：隐形到突显的交互 */
.btn-action {
  background: none;
  border: 1px solid transparent;
  padding: 4px 8px;
  font-size: 0.75rem;
  font-weight: 600;
  cursor: pointer;
  border-radius: 4px;
  transition: all 0.2s;
}

.btn-action.danger {
  color: #a3a3a3;
}

.btn-action.danger:hover {
  color: #dc2626;
  border-color: #fee2e2;
  background: #fff1f2;
}

/* 补充：如果没有数据时的空状态 */
.empty-msg {
  text-align: center;
  padding: 40px;
  color: #ccc;
  font-size: 0.8rem;
  font-style: italic;
}

@keyframes slideIn { 
  from { opacity: 0; transform: translateY(10px); } 
  to { opacity: 1; transform: translateY(0); } 
}
</style>