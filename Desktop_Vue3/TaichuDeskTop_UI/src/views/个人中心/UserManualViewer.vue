<!-- UserManualViewer.vue -->
<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import request from '../../utils/request'

interface MenuItem {
  id: number
  slug: string
  title: string
}

interface MenuCategory {
  id: number
  name: string
  articles: MenuItem[]
}

interface ArticleDetail {
  id: number
  slug: string
  title: string
  content: string
  updatedAt: string
}

const categories = ref<MenuCategory[]>([])
const currentArticle = ref<ArticleDetail | null>(null)
const currentSlug = ref('')
const loadingMenu = ref(false)
const loadingContent = ref(false)

// 1. 获取目录树
const fetchMenu = async () => {
  loadingMenu.value = true
  try {
    const res: any = await request.get('/manual/menu')
    categories.value = res.data || []

    // 默认加载第一个分类的第一篇文档
    if (categories.value.length > 0 && categories.value[0].articles.length > 0) {
      loadArticle(categories.value[0].articles[0].slug)
    }
  } catch (error) {
    console.error('获取手册目录失败:', error)
  } finally {
    loadingMenu.value = false
  }
}

// 2. 根据 slug 加载正文
const loadArticle = async (slug: string) => {
  if (currentSlug.value === slug) return
  currentSlug.value = slug
  loadingContent.value = true
  try {
    const res: any = await request.get(`/manual/article/${slug}`)
    currentArticle.value = res.data || null
  } catch (error) {
    console.error('获取文档内容失败:', error)
  } finally {
    loadingContent.value = false
  }
}

// 3. 渲染 Markdown 为 HTML
const renderedHtml = computed(() => {
  if (!currentArticle.value?.content) return ''
  return currentArticle.value.content
    .replace(/^### (.*$)/gim, '<h3>$1</h3>')
    .replace(/^## (.*$)/gim, '<h2>$1</h2>')
    .replace(/^# (.*$)/gim, '<h1>$1</h1>')
    .replace(/\!\[(.*?)\]\((.*?)\)/gim, '<img alt="$1" src="$2" class="manual-img" />')
    .replace(/\[(.*?)\]\((.*?)\)/gim, '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>')
    .replace(/^\- (.*$)/gim, '<li>$1</li>')
    .replace(/\n/gim, '<br />')
})

const formatDate = (dateStr?: string) => {
  if (!dateStr) return '--'
  return new Date(dateStr).toLocaleDateString()
}

onMounted(() => {
  fetchMenu()
})
</script>

<template>
  <div class="manual-viewer-panel">
    <!-- 左侧章节目录 -->
    <aside class="viewer-sidebar">
      <div v-if="loadingMenu" class="loading-hint">加载章节中...</div>
      <div v-else-if="categories.length === 0" class="empty-hint">暂无公开手册</div>
      
      <div v-for="cat in categories" :key="cat.id" class="category-block">
        <span class="cat-title">{{ cat.name }}</span>
        <ul class="article-list">
          <li 
            v-for="art in cat.articles" 
            :key="art.slug"
            class="article-item"
            :class="{ active: currentSlug === art.slug }"
            @click="loadArticle(art.slug)"
          >
            {{ art.title }}
          </li>
        </ul>
      </div>
    </aside>

    <!-- 右侧正文区 -->
    <main class="viewer-content">
      <div v-if="loadingContent" class="loading-state">
        <span>正在调取典籍卷轴...</span>
      </div>

      <template v-else-if="currentArticle">
        <header class="doc-header">
          <h2>{{ currentArticle.title }}</h2>
          <span class="doc-time">更新于 {{ formatDate(currentArticle.updatedAt) }}</span>
        </header>
        <div class="doc-body" v-html="renderedHtml"></div>
      </template>

      <div v-else class="empty-state">
        <p>请从左侧选择要查阅的章节</p>
      </div>
    </main>
  </div>
</template>

<style scoped>
.manual-viewer-panel {
  display: flex;
  background: #fff;
  border: 1px solid #f0f0f0;
  border-radius: 12px;
  overflow: hidden;
  min-height: 480px;
  max-height: 700px;
  animation: fadeIn 0.4s ease;
}

/* 目录栏 */
.viewer-sidebar {
  width: 200px;
  flex-shrink: 0;
  background: #fafbfc;
  border-right: 1px solid #eaeef2;
  padding: 16px 12px;
  overflow-y: auto;
}

.category-block { margin-bottom: 18px; }
.cat-title {
  font-size: 0.75rem;
  font-weight: 700;
  color: #8c959f;
  text-transform: uppercase;
  padding: 0 8px;
  display: block;
  margin-bottom: 6px;
}

.article-list { list-style: none; padding: 0; margin: 0; }
.article-item {
  padding: 8px 10px;
  border-radius: 6px;
  font-size: 0.85rem;
  color: #57606a;
  cursor: pointer;
  transition: all 0.2s;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.article-item:hover { background: #f3f4f6; color: #24292f; }
.article-item.active {
  background: #24292f;
  color: #fff;
  font-weight: 600;
}

/* 正文栏 */
.viewer-content {
  flex: 1;
  padding: 24px 32px;
  overflow-y: auto;
}

.doc-header {
  border-bottom: 1px solid #eaeef2;
  padding-bottom: 12px;
  margin-bottom: 20px;
}
.doc-header h2 {
  font-size: 1.4rem;
  font-weight: 700;
  margin: 0 0 6px;
  color: #24292f;
}
.doc-time { font-size: 0.75rem; color: #8c959f; }

.doc-body {
  font-size: 0.95rem;
  line-height: 1.7;
  color: #333;
}
:deep(.doc-body h1) { font-size: 1.3rem; margin: 20px 0 10px; border-bottom: 1px dashed #eaeef2; padding-bottom: 6px; }
:deep(.doc-body h2) { font-size: 1.15rem; margin: 16px 0 8px; }
:deep(.doc-body h3) { font-size: 1rem; margin: 12px 0 6px; }
:deep(.doc-body a) { color: #0969da; text-decoration: underline; }
:deep(.manual-img) {
  max-width: 100%;
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0,0,0,0.06);
  margin: 12px 0;
  display: block;
}

.loading-hint, .empty-hint, .empty-state, .loading-state {
  color: #8c959f;
  font-size: 0.85rem;
  text-align: center;
  padding: 40px 10px;
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(5px); }
  to { opacity: 1; transform: translateY(0); }
}

@media (max-width: 768px) {
  .manual-viewer-panel { flex-direction: column; max-height: none; }
  .viewer-sidebar { width: 100%; border-right: none; border-bottom: 1px solid #eaeef2; }
}
</style>