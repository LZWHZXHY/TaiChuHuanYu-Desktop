<template>
  <div class="wiki-admin-manager" v-loading="loading">
    <header class="module-header">
      <div class="header-content">
        <h2 class="page-title">维基治理中枢</h2>
        <p class="md-subtitle">全量内容管理、分类治理与版本回溯</p>
      </div>
      <button v-if="activeTab === 'categories'" class="btn-text" @click="handleAddNew">＋ 新增分类</button>
    </header>

    <nav class="md-tabs">
      <span class="tab-item" :class="{ active: activeTab === 'categories' }" @click="activeTab = 'categories'">分类管理</span>
      <span class="tab-item" :class="{ active: activeTab === 'requests' }" @click="activeTab = 'requests'">分类申请</span>
      <span class="tab-item" :class="{ active: activeTab === 'revisions' }" @click="activeTab = 'revisions'">内容审核</span>
      <span class="tab-item" :class="{ active: activeTab === 'articles' }" @click="activeTab = 'articles'">文章治理</span>
    </nav>

    <div v-if="activeTab === 'categories'" class="table-card">
      <table class="ink-table">
        <thead><tr><th>ID</th><th>名称</th><th>排序</th><th class="text-right">操作</th></tr></thead>
        <tbody>
          <tr v-for="item in sortedCategories" :key="item.id">
            <td class="mono">#{{ String(item.id).padStart(3, '0') }}</td>
            <td>{{ item.name }}</td>
            <td>{{ item.sortOrder }}</td>
            <td class="text-right actions">
              <button class="btn-s" @click="handleEdit(item)">修订</button>
              <button class="btn-s danger" @click="handleDelete(item)">抹除</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="activeTab === 'requests'" class="table-card">
      <table class="ink-table">
        <thead><tr><th>名称</th><th>申请理由</th><th class="text-right">审批</th></tr></thead>
        <tbody>
          <tr v-for="req in categoryRequests" :key="req.id">
            <td>{{ req.name }}</td>
            <td class="text-gray">{{ req.reason }}</td>
            <td class="text-right actions">
              <button class="btn-s success" @click="handleApprove(req)">[批准]</button>
              <button class="btn-s danger" @click="handleReject(req)">[驳回]</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="activeTab === 'revisions'" class="table-card">
      <table class="ink-table">
        <thead><tr><th>标题</th><th>提交人</th><th>内容预览</th><th class="text-right">决策</th></tr></thead>
        <tbody>
          <tr v-for="rev in pendingRevisions" :key="rev.id">
            <td>{{ rev.title }}</td>
            <td class="mono">{{ rev.authorId.substring(0, 6) }}</td>
            <td class="link" @click="showFullContent(rev.content)">查看预览</td>
            <td class="text-right actions">
              <button class="btn-s success" @click="processRevision(rev.id, true)">[通过]</button>
              <button class="btn-s danger" @click="processRevision(rev.id, false)">[驳回]</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="activeTab === 'articles'" class="table-card">
      <table class="ink-table">
        <thead><tr><th>标题</th><th>状态</th><th>最后更新</th><th class="text-right">治理操作</th></tr></thead>
        <tbody>
          <tr v-for="art in allArticles" :key="art.id">
            <td>{{ art.title }}</td>
            <td>
              <span :class="art.isDeleted ? 'status-archived' : 'status-live'">
                {{ art.isDeleted ? '已下架' : '公开中' }}
              </span>
            </td>
            <td class="text-gray">{{ new Date(art.updatedAt).toLocaleDateString() }}</td>
            <td class="text-right actions">
              <button class="btn-s" @click="handleToggleArchive(art)">
                {{ art.isDeleted ? '[恢复]' : '[下架]' }}
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <Teleport to="body">
      <div v-if="showEditModal" class="modal-mask" @mousedown="showEditModal = false">
        <div class="modal-container" @mousedown.stop>
          <h3>{{ isEdit ? '修订分类' : '开辟新分类' }}</h3>
          <input v-model="form.name" placeholder="分类名称" class="md-input" />
          <button class="btn-black" @click="submitForm">确认并同步</button>
        </div>
      </div>
      <div v-if="showContentModal" class="modal-mask" @mousedown="showContentModal = false">
        <div class="modal-container preview" @mousedown.stop>
          <SpiritPreview :modelValue="currentViewContent" />
          <button class="btn-black" @click="showContentModal = false">关闭预览</button>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { adminWikiApi, wikiReviewApi } from '@/api/Admin';
import SpiritPreview from '@/components/SpiritTextComponents/SpiritPreview.vue';

const loading = ref(false);
const activeTab = ref<'categories' | 'requests' | 'revisions' | 'articles'>('categories');
const categories = ref<any[]>([]);
const categoryRequests = ref<any[]>([]);
const pendingRevisions = ref<any[]>([]);
const allArticles = ref<any[]>([]);

// 模态框控制
const showEditModal = ref(false);
const showContentModal = ref(false);
const isEdit = ref(false);
const currentViewContent = ref<any>('');
const form = ref({ id: undefined, name: '', parentId: null, sortOrder: 0, ownershipType: 0, ownerId: null });

// 统一数据加载
const loadAllData = async () => {
  loading.value = true;
  try {
    const [c, r, rev, articles] = await Promise.all([
      adminWikiApi.getAllCategories(),
      adminWikiApi.getCategoryRequests(),
      wikiReviewApi.getPending(),
      adminWikiApi.getAllArticlesForManagement() 
    ]);
    categories.value = c || [];
    categoryRequests.value = r || [];
    pendingRevisions.value = rev || [];
    allArticles.value = articles || [];
  } finally { loading.value = false; }
};

// 治理操作
const handleToggleArchive = async (art: any) => {
  if (!confirm(`确定要 ${art.isDeleted ? '恢复公开' : '执行下架'} [${art.title}] 吗？`)) return;
  await wikiReviewApi.toggleArticleArchive(art.id);
  await loadAllData();
};

const processRevision = async (id: number, approved: boolean) => {
  const remarks = approved ? '通过' : prompt('驳回理由：');
  if (remarks === null) return;
  await wikiReviewApi.handle(id, { approved, remarks });
  await loadAllData();
};

// 基础 CRUD 操作
const handleApprove = async (req: any) => { await adminWikiApi.approveCategoryRequest(req.id); await loadAllData(); };
const handleReject = async (req: any) => { await adminWikiApi.rejectCategoryRequest(req.id); await loadAllData(); };
const handleEdit = (item: any) => { isEdit.value = true; form.value = { ...item }; showEditModal.value = true; };
const handleDelete = async (item: any) => { await adminWikiApi.deleteCategory(item.id); await loadAllData(); };
const handleAddNew = () => { isEdit.value = false; form.value = { id: undefined, name: '', parentId: null, sortOrder: 0, ownershipType: 0, ownerId: null }; showEditModal.value = true; };
const submitForm = async () => { 
  isEdit.value ? await adminWikiApi.updateCategory(form.value.id!, form.value) : await adminWikiApi.createCategory(form.value); 
  showEditModal.value = false; 
  await loadAllData(); 
};

const showFullContent = (content: any) => { currentViewContent.value = content; showContentModal.value = true; };
const sortedCategories = computed(() => [...categories.value].sort((a, b) => a.sortOrder - b.sortOrder));

onMounted(loadAllData);
</script>

<style scoped>
.wiki-admin-manager { padding: 40px; background: #fff; min-height: 100vh; color: #1a1a1a; }
.module-header { display: flex; justify-content: space-between; align-items: flex-end; margin-bottom: 40px; }
.md-tabs { display: flex; gap: 32px; border-bottom: 1px solid #eee; margin-bottom: 30px; }
.tab-item { cursor: pointer; color: #999; padding-bottom: 10px; font-size: 0.9rem; }
.tab-item.active { color: #000; border-bottom: 2px solid #000; font-weight: 600; }

.ink-table { width: 100%; border-collapse: collapse; }
.ink-table th { text-align: left; padding: 12px 0; font-size: 0.75rem; color: #999; text-transform: uppercase; border-bottom: 1px solid #eee; }
.ink-table td { padding: 16px 0; border-bottom: 1px solid #f9f9f9; font-size: 0.9rem; }

.actions { display: flex; justify-content: flex-end; gap: 15px; }
.btn-s { background: none; border: none; cursor: pointer; color: #86868b; font-size: 0.85rem; }
.btn-s:hover { color: #000; }
.btn-s.danger:hover { color: #ff3b30; }
.btn-s.success:hover { color: #10b981; }

.status-live { color: #10b981; font-weight: 500; font-size: 0.8rem; }
.status-archived { color: #ef4444; font-weight: 500; font-size: 0.8rem; }

.modal-mask { position: fixed; inset: 0; background: rgba(0,0,0,0.4); display: flex; justify-content: center; align-items: center; z-index: 999; }
.modal-container { background: #fff; width: 400px; padding: 40px; }
.modal-container.preview { width: 700px; max-height: 80vh; overflow-y: auto; }
.md-input { width: 100%; padding: 10px; margin-bottom: 20px; border: 1px solid #eee; }
.btn-black { width: 100%; background: #000; color: #fff; border: none; padding: 12px; cursor: pointer; }
.text-gray { color: #86868b; }
.link { color: #3b82f6; cursor: pointer; text-decoration: underline; }
</style>