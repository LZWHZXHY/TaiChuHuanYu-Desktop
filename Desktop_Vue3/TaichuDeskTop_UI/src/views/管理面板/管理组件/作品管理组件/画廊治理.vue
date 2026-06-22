<template>
  <div class="gallery-sub-module">
    <div class="sub-header">
      <div class="filter-panel">
        <input 
          v-model="searchQuery" 
          @keyup.enter="handleSearch"
          type="text" 
          placeholder="搜索画廊 ID、标题或画师标识..." 
          class="ink-input search-bar"
        />
        <select v-model="filterStatus" class="ink-select" @change="handleSearch">
          <option value="">所有流转状态</option>
          <option value="published">已发布 (正常)</option>
          <option value="reviewing">审核中 (静默)</option>
          <option value="rejected">已驳回 (违规)</option>
          <option value="hidden">用户隐藏</option>
        </select>
      </div>
      <button class="btn-refresh" @click="handleSearch" :disabled="loading">
        {{ loading ? '同步中...' : '刷新画廊大盘' }}
      </button>
    </div>

    <div class="table-responsive">
      <table class="ink-table">
        <thead>
          <tr>
            <th width="100">标识</th>
            <th width="80">封面</th>
            <th width="220">画廊元数据</th>
            <th width="140">画师 / 创作者</th>
            <th width="140">数据特征</th>
            <th width="100">状态</th>
            <th width="180" class="text-right">干涉操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="loading && galleryList.length === 0">
            <td colspan="7" class="empty-cell">检索画廊数据中...</td>
          </tr>
          <tr v-else-if="galleryList.length === 0">
            <td colspan="7" class="empty-cell">未捕捉到任何画廊实体</td>
          </tr>
          
          <tr v-for="work in galleryList" :key="work.id" class="data-row">
            <td class="mono font-sm" :title="work.id">#{{ work.id.substring(0, 8) }}</td>
            
            <td>
              <div class="cover-preview">
                <img v-if="work.coverUrl" :src="work.coverUrl" alt="cover" />
                <div v-else class="cover-placeholder">IMG</div>
              </div>
            </td>
            
            <td>
              <div class="work-base-info">
                <span class="work-title text-truncate" :title="work.title">{{ work.title }}</span>
                <div class="work-tags">
                  <span class="category-tag">视觉画廊</span>
                  <span v-if="work.isFeatured" class="feature-tag">🔥 推荐</span>
                </div>
              </div>
            </td>
            
            <td>
              <div class="author-info">
                <span class="author-name text-truncate">{{ work.authorName }}</span>
                <span class="mono font-xs text-muted">{{ work.authorId.substring(0, 6) }}</span>
              </div>
            </td>
            
            <td>
              <div class="stats-matrix mono font-sm">
                <span title="浏览" class="stat-item">👁 {{ work.views || 0 }}</span>
                <span title="喜爱" class="stat-item">♥ {{ work.likes || 0 }}</span>
              </div>
            </td>
            
            <td>
              <span :class="['status-badge', work.status]">
                {{ getStatusLabel(work.status) }}
              </span>
            </td>
            
            <td class="text-right actions">
              <button class="btn-action edit" @click="handleOpenGovernance(work)">干涉</button>
              <button class="btn-action danger" @click="handleTakedown(work)" v-if="work.status === 'published'">下架</button>
              <button class="btn-action edit" @click="handleRestore(work)" v-else>恢复</button>
              <button class="btn-action danger" @click="handleDelete(work)">抹除</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    
    <div class="pagination-footer" v-if="totalCount > 0">
      <span class="page-info">共聚合 {{ totalCount }} 个画廊，每页 {{ pageSize }} 条</span>
      <div class="page-controls">
        <button class="btn-page" :disabled="currentPage === 1" @click="changePage(currentPage - 1)">上一区间</button>
        <span class="current-page">{{ currentPage }} / {{ totalPages }}</span>
        <button class="btn-page" :disabled="currentPage >= totalPages" @click="changePage(currentPage + 1)">下一区间</button>
      </div>
    </div>

    <Teleport to="body">
      <div v-if="showGovModal" class="modal-mask" @click.self="closeModal">
        <div class="modal-container scroll-y">
          <header class="modal-header">
            <div>
              <h3>画廊干涉中枢</h3>
              <p class="mono font-sm">实体追踪码: {{ targetWork?.id }}</p>
            </div>
            <button class="close-icon" @click="closeModal">×</button>
          </header>

          <div class="modal-body">
            <fieldset class="gov-fieldset">
              <legend>流量与特征干涉</legend>
              <div class="form-grid">
                <div class="field">
                  <label>基础浏览量 (Views)</label>
                  <input type="number" v-model.number="editForm.views" />
                </div>
                <div class="field">
                  <label>喜爱值 (Likes)</label>
                  <input type="number" v-model.number="editForm.likes" />
                </div>
                <div class="field full">
                  <label class="checkbox-label" style="display: flex; align-items: center; gap: 8px; margin-top: 10px;">
                    <input type="checkbox" v-model="editForm.isFeatured" />
                    <span style="font-size: 0.85rem; font-weight: bold; color: #111;">赋予太初流量池推荐权重 (Featured)</span>
                  </label>
                </div>
              </div>
            </fieldset>

            <fieldset class="gov-fieldset danger-zone">
              <legend>状态裁决</legend>
              <div class="field full">
                <select v-model="editForm.status" class="ink-select" style="width: 100%;">
                  <option value="published">允许公开流转 (Published)</option>
                  <option value="reviewing">置于审核静默 (Reviewing)</option>
                  <option value="rejected">全域驳回拦截 (Rejected)</option>
                  <option value="hidden">强制隐藏 (Hidden)</option>
                </select>
              </div>
            </fieldset>
          </div>

          <footer class="modal-footer">
            <button class="btn-cancel" @click="closeModal">放弃干涉</button>
            <button class="btn-submit" @click="submitGovernance" :disabled="submitting">
              {{ submitting ? '指令广播中...' : '提交裁决至太初域' }}
            </button>
          </footer>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
// 引入共享类型，确保前后端数据格式完全对齐
import { adminProductApi, type GalleryDto, type GalleryGovernanceDto } from '@/api/Admin/AdminProduct'; 

const loading = ref(false);
const submitting = ref(false);
const showGovModal = ref(false);

const galleryList = ref<GalleryDto[]>([]);
const targetWork = ref<GalleryDto | null>(null);

const searchQuery = ref('');
const filterStatus = ref('');

const currentPage = ref(1);
const pageSize = ref(10);
const totalCount = ref(0);
const totalPages = computed(() => Math.ceil(totalCount.value / pageSize.value) || 1);

// 使用严格的类型推导
const editForm = ref<GalleryGovernanceDto>({ views: 0, likes: 0, isFeatured: false, status: 'published' });

const fetchGalleries = async () => {
  loading.value = true;
  try {
    const res = await adminProductApi.getGalleryWorks({
      page: currentPage.value,
      pageSize: pageSize.value,
      search: searchQuery.value.trim() || undefined,
      status: filterStatus.value || undefined
    });
    
    galleryList.value = res.items; 
    totalCount.value = res.totalCount;
  } catch (error) {
    console.error('拉取画廊大盘数据失败', error);
  } finally {
    loading.value = false;
  }
};
onMounted(fetchGalleries);

const handleSearch = () => { currentPage.value = 1; fetchGalleries(); };
const changePage = (page: number) => { if (page >= 1 && page <= totalPages.value) { currentPage.value = page; fetchGalleries(); } };

const handleOpenGovernance = (work: GalleryDto) => {
  targetWork.value = work;
  editForm.value = { views: work.views, likes: work.likes, isFeatured: work.isFeatured, status: work.status };
  showGovModal.value = true;
};
const closeModal = () => { showGovModal.value = false; targetWork.value = null; };

// 快捷操作：仅仅是修改状态，数据保留
const handleTakedown = async (work: GalleryDto) => { 
  if (confirm(`确定要下架《${work.title}》吗？`)) {
    try {
      await adminProductApi.updateGalleryGovernance(work.id, { views: work.views, likes: work.likes, isFeatured: work.isFeatured, status: 'rejected' });
      work.status = 'rejected';
    } catch (err) {
      alert('下架指令失败');
    }
  }
};

const handleRestore = async (work: GalleryDto) => { 
  if (confirm(`确定要恢复《${work.title}》的流转吗？`)) {
    try {
      await adminProductApi.updateGalleryGovernance(work.id, { views: work.views, likes: work.likes, isFeatured: work.isFeatured, status: 'published' });
      work.status = 'published';
    } catch (err) {
      alert('恢复指令失败');
    }
  }
};

// 🌟 新增操作：彻底抹除物理数据
const handleDelete = async (work: GalleryDto) => {
  if (!confirm(`【极度危险】确定要将《${work.title}》从太初数据库中彻底抹除吗？此操作不可逆！`)) {
    return;
  }
  
  try {
    await adminProductApi.deleteGalleryWork(work.id);
    
    // 成功后，从当前前端列表中剔除该项，实现无刷新更新
    galleryList.value = galleryList.value.filter(item => item.id !== work.id);
    totalCount.value -= 1; 
  } catch (error) {
    console.error('抹除指令执行失败', error);
    alert('抹除失败，请检查网络或后台报错');
  }
};

const submitGovernance = async () => {
  if (!targetWork.value) return;
  submitting.value = true;
  try {
    await adminProductApi.updateGalleryGovernance(targetWork.value.id, editForm.value);
    
    // API 成功响应后，乐观更新前端 UI
    Object.assign(targetWork.value, editForm.value);
    closeModal();
  } catch (error) {
    console.error('干涉指令执行失败', error);
  } finally {
    submitting.value = false;
  }
};

const getStatusLabel = (status: string) => ({ published: '流转中', reviewing: '审核静默', rejected: '拦截封禁', hidden: '自行隐匿' }[status] || status);
</script>

<style scoped>
.gallery-sub-module { display: flex; flex-direction: column; gap: 16px; animation: fadeIn 0.3s ease; }
.sub-header { display: flex; justify-content: space-between; gap: 20px; margin-bottom: 8px; }
.filter-panel { display: flex; gap: 12px; flex: 1; }
.ink-input, .ink-select { border: 1px solid #e0e0e0; padding: 8px 12px; border-radius: 4px; font-size: 0.85rem; outline: none; transition: 0.2s; }
.ink-input:focus, .ink-select:focus { border-color: #111; }
.search-bar { width: 300px; }
.btn-refresh { background: #fff; border: 1px solid #e0e0e0; padding: 8px 16px; border-radius: 4px; cursor: pointer; font-size: 0.85rem; transition: 0.2s; }
.btn-refresh:hover { border-color: #111; }

.ink-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 0.88rem; }
.ink-table th { padding: 12px 16px; color: #888; font-size: 0.75rem; border-bottom: 2px solid #111; }
.ink-table td { padding: 16px; border-bottom: 1px solid #f7f7f7; vertical-align: middle; }
.data-row:hover td { background-color: #fafafa; }

.cover-preview { width: 48px; height: 32px; border-radius: 4px; overflow: hidden; background: #f0f0f0; border: 1px solid #e5e5e5; display: flex; align-items: center; justify-content: center; }
.cover-preview img { width: 100%; height: 100%; object-fit: cover; }
.cover-placeholder { font-size: 0.6rem; color: #ccc; font-weight: 800; letter-spacing: 1px; }

.work-base-info { display: flex; flex-direction: column; gap: 4px; }
.work-title { font-weight: 700; color: #111; }
.work-tags { display: flex; gap: 6px; }
.category-tag { font-size: 0.7rem; background: #f1f5f9; color: #475569; padding: 2px 6px; border-radius: 3px; }
.feature-tag { font-size: 0.7rem; background: #fff1f2; color: #e11d48; border: 1px solid #fecdd3; padding: 1px 4px; border-radius: 3px; }
.author-info { display: flex; flex-direction: column; }
.author-name { font-weight: 600; color: #333; }
.text-muted { color: #999; }
.mono { font-family: ui-monospace, monospace; }
.font-sm { font-size: 0.8rem; }
.font-xs { font-size: 0.7rem; }
.text-truncate { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 160px; display: inline-block; }
.stats-matrix { display: flex; gap: 8px; color: #666; }
.stat-item { background: #fafafa; padding: 2px 6px; border-radius: 4px; border: 1px solid #eee; }
.status-badge { font-size: 0.75rem; font-weight: 600; padding: 4px 8px; border-radius: 4px; }
.status-badge.published { background: #f0fdf4; color: #16a34a; }
.status-badge.reviewing { background: #fffbeb; color: #d97706; }
.status-badge.rejected { background: #fef2f2; color: #dc2626; text-decoration: line-through; }
.status-badge.hidden { background: #f1f5f9; color: #64748b; }
.btn-action { background: none; border: none; font-size: 0.8rem; font-weight: 700; cursor: pointer; margin-left: 12px; padding: 0; }
.btn-action.edit { color: #2563eb; }
.btn-action.danger { color: #dc2626; }
.btn-action:hover { text-decoration: underline; }
.pagination-footer { display: flex; justify-content: space-between; align-items: center; padding: 16px 0; border-top: 1px solid #f0f0f0; margin-top: 10px; }
.page-controls { display: flex; align-items: center; gap: 12px; }
.btn-page { background: #fff; border: 1px solid #ddd; padding: 4px 12px; border-radius: 4px; cursor: pointer; }
.btn-page:disabled { opacity: 0.4; cursor: not-allowed; }

.modal-mask { position: fixed; inset: 0; background: rgba(255,255,255,0.85); backdrop-filter: blur(12px); z-index: 9999; display: flex; justify-content: center; align-items: center; }
.modal-container { background: #fff; border: 1px solid #000; width: 500px; padding: 30px; }
.modal-header { display: flex; justify-content: space-between; margin-bottom: 20px; }
.close-icon { background: none; border: none; font-size: 1.5rem; cursor: pointer; }
.gov-fieldset { border: 1px solid #eee; padding: 16px; margin-bottom: 20px; }
.gov-fieldset legend { font-size: 0.75rem; color: #999; font-weight: 800; }
.danger-zone { border-color: #fecaca; background: #fffafa; }
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.field { display: flex; flex-direction: column; gap: 4px; }
.field.full { grid-column: span 2; }
.field label { font-size: 0.7rem; color: #aaa; font-weight: 700; }
.field input, .field select { padding: 8px; border: 1px solid #eaeaea; }
.modal-footer { display: flex; justify-content: flex-end; gap: 10px; }
.btn-cancel { padding: 10px 20px; border: 1px solid #eee; background: none; cursor: pointer; }
.btn-submit { padding: 10px 20px; background: #111; color: #fff; border: none; cursor: pointer; }
@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
</style>