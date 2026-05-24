<template>
  <div class="wiki-admin-manager" v-loading="loading">
    <header class="module-header">
      <div class="header-content">
        <h2 class="page-title">维基管理中心</h2>
        <p class="md-subtitle">太初百科的知识骨架与内容治理中枢</p>
      </div>
      <div class="action-btns" v-if="activeTab === 'categories'">
        <button class="btn-add" @click="handleAddNew">＋ 新增分类</button>
      </div>
    </header>

    <nav class="md-tabs">
      <span class="tab-item" :class="{ active: activeTab === 'categories' }" @click="activeTab = 'categories'">正式分类</span>
      <span class="tab-item" :class="{ active: activeTab === 'requests' }" @click="activeTab = 'requests'">分类申请</span>
      <span class="tab-item" :class="{ active: activeTab === 'revisions' }" @click="activeTab = 'revisions'">内容审核</span>
    </nav>

    <div class="table-card" v-if="activeTab === 'categories'">
      <table class="ink-table">
        <thead><tr><th>ID</th><th>名称</th><th>模式</th><th>排序</th><th class="text-right">操作</th></tr></thead>
        <tbody>
          <tr v-for="item in sortedCategories" :key="item.id">
            <td class="mono">#{{ String(item.id).padStart(3, '0') }}</td>
            <td>{{ item.name }}</td>
            <td><span class="badge" :class="item.ownershipType === 1 ? 'private-node' : 'public-node'">{{ item.ownershipType === 1 ? '私有' : '共有' }}</span></td>
            <td>{{ item.sortOrder }}</td>
            <td class="text-right actions">
              <button class="btn-s" @click="handleEdit(item)">[修订]</button>
              <button class="btn-s danger" @click="handleDelete(item)">[抹除]</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div class="table-card" v-if="activeTab === 'requests'">
      <table class="ink-table">
        <thead><tr><th>名称</th><th>申请理由</th><th class="text-right">审批</th></tr></thead>
        <tbody>
          <tr v-for="req in categoryRequests" :key="req.id">
            <td class="bold-name">### {{ req.name }}</td>
            <td class="reason-text">{{ req.reason || '无理由' }}</td>
            <td class="text-right actions">
              <button class="btn-s success" @click="handleApprove(req)">[批准]</button>
              <button class="btn-s danger" @click="handleReject(req)">[驳回]</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div class="table-card" v-if="activeTab === 'revisions'">
      <table class="ink-table">
        <thead><tr><th>标题</th><th>提交人</th><th>分类</th><th>内容预览</th><th class="text-right">操作</th></tr></thead>
        <tbody>
          <tr v-for="rev in pendingRevisions" :key="rev.id">
            <td class="bold-name">{{ rev.title }}</td>
            <td class="mono">{{ rev.authorId.substring(0, 8) }}</td>
            <td>{{ rev.categoryName || '未知' }}</td>
            <td @click="showFullContent(rev.content)" class="content-cell">点击预览内容</td>
            <td class="text-right actions">
              <button class="btn-s success" @click="processRevision(rev.id, true)">[通过]</button>
              <button class="btn-s danger" @click="processRevision(rev.id, false)">[驳回]</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <Teleport to="body">
      <div v-if="showEditModal" class="modal-mask" @mousedown="showEditModal = false">
        <div class="modal-container" @mousedown.stop>
          <h3>{{ isEdit ? '修订分类' : '开辟新分类' }}</h3>
          <div class="form-grid">
            <input v-model="form.name" placeholder="分类名称" />
            <select v-model="form.parentId"><option :value="null">-- 顶级 --</option><option v-for="c in categories" :key="c.id" :value="c.id">{{ c.name }}</option></select>
          </div>
          <button class="btn-confirm" @click="submitForm">确认并同步</button>
        </div>
      </div>
      <div v-if="showContentModal" class="modal-mask" @mousedown="showContentModal = false">
        <div class="modal-container" style="width:700px; height:600px; overflow-y:auto;" @mousedown.stop>
          <h3>词条详情预览</h3>
          <div class="preview-wrapper">
             <SpiritPreview :modelValue="currentViewContent" />
          </div>
          <button class="btn-confirm" @click="showContentModal = false">关闭</button>
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
const activeTab = ref<'categories' | 'requests' | 'revisions'>('categories');
const categories = ref<any[]>([]);
const categoryRequests = ref<any[]>([]);
const pendingRevisions = ref<any[]>([]);
const showEditModal = ref(false);
const showContentModal = ref(false);
const isEdit = ref(false);
const currentViewContent = ref<any>('');
const form = ref({ id: undefined, name: '', parentId: null, sortOrder: 0, ownershipType: 0, ownerId: null });

const currentUser = { id: 'admin-id', isAdmin: true };

const loadAllData = async () => {
  loading.value = true;
  try {
    const [c, r, rev] = await Promise.all([
      adminWikiApi.getAllCategories(),
      adminWikiApi.getCategoryRequests(),
      wikiReviewApi.getPending(currentUser.id, currentUser.isAdmin)
    ]);
    categories.value = c || [];
    categoryRequests.value = r || [];
    pendingRevisions.value = rev || [];
  } finally { loading.value = false; }
};

const showFullContent = (content: any) => { 
  currentViewContent.value = content; 
  showContentModal.value = true; 
};

const processRevision = async (id: number, approved: boolean) => {
  const remarks = approved ? '通过' : prompt('驳回理由：');
  if (remarks === null) return;
  await wikiReviewApi.handle(id, { currentUserId: currentUser.id, isAdmin: currentUser.isAdmin, approved, remarks });
  await loadAllData();
};

const handleApprove = async (req: any) => { await adminWikiApi.approveCategoryRequest(req.id); await loadAllData(); };
const handleReject = async (req: any) => { await adminWikiApi.rejectCategoryRequest(req.id); await loadAllData(); };
const handleEdit = (item: any) => { isEdit.value = true; form.value = { ...item }; showEditModal.value = true; };
const handleDelete = async (item: any) => { await adminWikiApi.deleteCategory(item.id); await loadAllData(); };
const handleAddNew = () => { isEdit.value = false; form.value = { id: undefined, name: '', parentId: null, sortOrder: 0, ownershipType: 0, ownerId: null }; showEditModal.value = true; };
const submitForm = async () => { isEdit.value ? await adminWikiApi.updateCategory(form.value.id!, form.value) : await adminWikiApi.createCategory(form.value); showEditModal.value = false; await loadAllData(); };

const sortedCategories = computed(() => [...categories.value].sort((a, b) => a.sortOrder - b.sortOrder));
onMounted(loadAllData);
</script>

<style scoped>
.wiki-admin-manager { padding: 40px; background: #fff; min-height: 100vh; }
.module-header { display: flex; justify-content: space-between; margin-bottom: 40px; }
.md-tabs { display: flex; gap: 32px; border-bottom: 1px solid #f0f0f0; margin-bottom: 32px; }
.tab-item { cursor: pointer; color: #888; padding-bottom: 12px; }
.tab-item.active { color: #000; border-bottom: 2px solid #000; font-weight: 500; }
.ink-table { width: 100%; border-collapse: collapse; }
.ink-table th { padding: 12px; font-size: 0.75rem; color: #888; border-bottom: 1px solid #111; text-align: left; }
.ink-table td { padding: 16px; border-bottom: 1px solid #f6f6f6; font-size: 0.9rem; }
.content-cell { cursor: pointer; color: #3b82f6; text-decoration: underline; font-size: 0.8rem; }
.actions { display: flex; justify-content: flex-end; gap: 10px; }
.btn-s { background: none; border: none; cursor: pointer; color: #666; }
.btn-s.danger { color: #ef4444; }
.btn-s.success { color: #10b981; }
.modal-mask { position: fixed; inset: 0; background: rgba(0,0,0,0.5); display: flex; justify-content: center; align-items: center; z-index: 999; }
.modal-container { background: #fff; width: 440px; padding: 40px; }
.preview-wrapper { border: 1px solid #eee; padding: 20px; border-radius: 8px; background: #fafafa; }
.btn-confirm { width: 100%; background: #000; color: #fff; border: none; padding: 14px; margin-top: 30px; cursor: pointer; }
</style>