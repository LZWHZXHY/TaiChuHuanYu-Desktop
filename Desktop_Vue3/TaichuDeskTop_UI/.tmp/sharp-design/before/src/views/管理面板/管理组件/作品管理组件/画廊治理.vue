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
          <option value="">所有状态</option>
          <option value="published">已发布</option>
          <option value="reviewing">审核中</option>
          <option value="rejected">已驳回</option>
          <option value="hidden">已隐藏</option>
        </select>
      </div>
      <button class="btn-refresh" @click="handleSearch" :disabled="loading">
        {{ loading ? '同步中...' : '刷新列表' }}
      </button>
    </div>

    <p v-if="error" class="error-message" role="alert">{{ error }}</p>
    <div class="table-responsive">
      <table class="ink-table">
        <thead>
          <tr>
            <th width="100">标识</th>
            <th width="80">封面</th>
            <th width="220">作品信息</th>
            <th width="140">画师 / 创作者</th>
            <th width="140">浏览与点赞</th>
            <th width="100">状态</th>
            <th width="180" class="text-right">管理操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="loading && galleryList.length === 0">
            <td colspan="7" class="empty-cell">检索画廊数据中...</td>
          </tr>
          <tr v-else-if="galleryList.length === 0">
            <td colspan="7" class="empty-cell">暂无符合条件的作品</td>
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
                <span title="点赞数" class="stat-item">♥ {{ work.likes || 0 }}</span>
              </div>
            </td>
            
            <td>
              <span :class="['status-badge', work.status]">
                {{ getStatusLabel(work.status) }}
              </span>
            </td>
            
            <td class="text-right actions">
              <button :disabled="submitting" class="btn-action edit" @click="handleOpenGovernance(work)">编辑</button>
              <button :disabled="submitting" class="btn-action danger" @click="handleStatus(work, 'rejected')" v-if="work.status === 'published'">下架</button>
              <button :disabled="submitting" class="btn-action edit" @click="handleStatus(work, 'published')" v-else>恢复</button>
              <button :disabled="submitting" class="btn-action danger" @click="handleDelete(work)">删除</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    
    <div class="pagination-footer" v-if="totalCount > 0">
      <span class="page-info">共 {{ totalCount }} 个作品，每页 {{ pageSize }} 条</span>
      <div class="page-controls">
        <button class="btn-page" :disabled="currentPage === 1" @click="changePage(currentPage - 1)">上一页</button>
        <span class="current-page">{{ currentPage }} / {{ totalPages }}</span>
        <button class="btn-page" :disabled="currentPage >= totalPages" @click="changePage(currentPage + 1)">下一页</button>
      </div>
    </div>

    <Teleport to="body">
      <div v-if="showGovModal" class="modal-mask" @click.self="closeModal">
        <div class="modal-container scroll-y workbench-skin editor-surface community-surface tone-admin">
          <header class="modal-header studio-heading">
            <div>
              <h3>管理作品</h3>
              <p class="mono font-sm">作品编号： {{ targetWork?.id }}</p>
            </div>
            <button :disabled="submitting" class="close-icon" @click="closeModal">×</button>
          </header>

          <div class="modal-body">
            <fieldset class="gov-fieldset">
              <legend>展示设置</legend>
              <div class="form-grid">
                <div class="field">
                  <label>浏览量</label>
                  <input type="number" min="0" step="1" v-model.number="editForm.views" />
                </div>
                <div class="field">
                  <label>点赞数</label>
                  <output>{{ targetWork?.likes ?? 0 }}</output>
                  <small>按用户实际点赞统计，不支持手动修改</small>
                </div>
                <div class="field full">
                  <label class="checkbox-label" style="display: flex; align-items: center; gap: 8px; margin-top: 10px;">
                    <input type="checkbox" v-model="editForm.isFeatured" />
                    <span style="font-size: 0.85rem; font-weight: bold; color: var(--text-primary);">推荐作品</span>
                  </label>
                </div>
              </div>
            </fieldset>

            <fieldset class="gov-fieldset danger-zone">
              <legend>发布状态</legend>
              <div class="field full">
                <select v-model="editForm.status" class="ink-select" style="width: 100%;">
                  <option value="published">已发布</option>
                  <option value="reviewing">审核中</option>
                  <option value="rejected">已驳回</option>
                  <option value="hidden">已隐藏</option>
                </select>
              </div>
            </fieldset>
          </div>

          <p v-if="error" class="error-message" role="alert">{{ error }}</p>
          <footer class="modal-footer">
            <button :disabled="submitting" class="btn-cancel" @click="closeModal">取消</button>
            <button class="btn-submit" @click="submitGovernance" :disabled="submitting">
              {{ submitting ? '保存中...' : '保存' }}
            </button>
          </footer>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { adminProductApi, type GalleryDto, type GalleryGovernanceDto } from '@/api/Admin/AdminProduct';

const loading = ref(false), submitting = ref(false), showGovModal = ref(false);
const error = ref('');
const galleryList = ref<GalleryDto[]>([]), targetWork = ref<GalleryDto | null>(null);
const searchQuery = ref(''), filterStatus = ref('');
const currentPage = ref(1), pageSize = 10, totalCount = ref(0);
const totalPages = computed(() => Math.ceil(totalCount.value / pageSize) || 1);
const editForm = ref<GalleryGovernanceDto>({ views: 0, isFeatured: false, status: 'published' });
let requestId = 0;

const fetchGalleries = async () => {
  const id = ++requestId;
  loading.value = true;
  try {
    const res = await adminProductApi.getGalleryWorks({ page: currentPage.value, pageSize,
      search: searchQuery.value.trim() || undefined, status: filterStatus.value || undefined });
    if (id !== requestId) return;
    galleryList.value = res.items;
    totalCount.value = res.totalCount;
  } catch {
    if (id === requestId) error.value = '加载作品失败，请刷新重试。';
  } finally {
    if (id === requestId) loading.value = false;
  }
};
onMounted(fetchGalleries);
const handleSearch = () => { error.value = ''; currentPage.value = 1; return fetchGalleries(); };
const changePage = (page: number) => { if (page >= 1 && page <= totalPages.value) { currentPage.value = page; return fetchGalleries(); } };
const handleOpenGovernance = (work: GalleryDto) => {
  if (submitting.value) return;
  error.value = '';
  targetWork.value = work;
  editForm.value = { views: work.views, isFeatured: work.isFeatured, status: work.status };
  showGovModal.value = true;
};
const closeModal = () => { if (!submitting.value) { showGovModal.value = false; targetWork.value = null; } };

const updateWork = async (work: GalleryDto, data: GalleryGovernanceDto) => {
  if (submitting.value) return false;
  if (!Number.isInteger(data.views) || data.views < 0) { error.value = '浏览量必须是非负整数。'; return false; }
  submitting.value = true;
  error.value = '';
  try {
    await adminProductApi.updateGalleryGovernance(work.id, data);
    await fetchGalleries();
    return true;
  } catch {
    error.value = '保存失败，修改内容已保留，请重试。';
    return false;
  } finally { submitting.value = false; }
};
const handleStatus = async (work: GalleryDto, status: 'published' | 'rejected') => {
  if (submitting.value || !confirm('确定要' + (status === 'published' ? '恢复' : '下架') + '《' + work.title + '》吗？')) return;
  await updateWork(work, { views: work.views, isFeatured: work.isFeatured, status });
};
const submitGovernance = async () => {
  if (targetWork.value && await updateWork(targetWork.value, { ...editForm.value })) closeModal();
};
const handleDelete = async (work: GalleryDto) => {
  if (submitting.value || !confirm('确定永久删除《' + work.title + '》吗？此操作不可撤销。')) return;
  submitting.value = true;
  error.value = '';
  try {
    await adminProductApi.deleteGalleryWork(work.id);
    if (galleryList.value.length === 1 && currentPage.value > 1) currentPage.value--;
    await fetchGalleries();
  } catch { error.value = '删除失败，请重试。'; }
  finally { submitting.value = false; }
};
const getStatusLabel = (status: string) => ({ published: '已发布', reviewing: '审核中', rejected: '已驳回', hidden: '已隐藏' }[status] || status);
</script>

<style scoped>
.error-message { color: var(--color-danger, var(--danger)); font-size: 0.85rem; }
.field output { padding: 8px; font-variant-numeric: tabular-nums; }
.field small { color: var(--text-secondary); font-size: 0.75rem; }
button:disabled { opacity: 0.5; cursor: not-allowed; }
.gallery-sub-module { display: flex; flex-direction: column; gap: 16px; animation: fadeIn 0.3s ease; }
.sub-header { display: flex; justify-content: space-between; gap: 20px; margin-bottom: 8px; }
.filter-panel { display: flex; gap: 12px; flex: 1; }
.ink-input, .ink-select { border: 1px solid var(--border-line); padding: 8px 12px; border-radius: 4px; font-size: 0.85rem; outline: none; transition: 0.2s; }
.ink-input:focus, .ink-select:focus { border-color: var(--border-line); }
.search-bar { width: 300px; }
.btn-refresh { background: var(--paper-card); border: 1px solid var(--border-line); padding: 8px 16px; border-radius: 4px; cursor: pointer; font-size: 0.85rem; transition: 0.2s; }
.btn-refresh:hover { border-color: var(--border-line); }

.ink-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 0.88rem; }
.ink-table th { padding: 12px 16px; color: var(--text-secondary); font-size: 0.75rem; border-bottom: 2px solid var(--border-line); }
.ink-table td { padding: 16px; border-bottom: 1px solid var(--border-line); vertical-align: middle; }
.data-row:hover td { background-color: var(--bg-main); }

.cover-preview { width: 48px; height: 32px; border-radius: 4px; overflow: hidden; background: var(--bg-sub); border: 1px solid var(--border-line); display: flex; align-items: center; justify-content: center; }
.cover-preview img { width: 100%; height: 100%; object-fit: cover; }
.cover-placeholder { font-size: 0.6rem; color: var(--text-secondary); font-weight: 800; letter-spacing: 1px; }

.work-base-info { display: flex; flex-direction: column; gap: 4px; }
.work-title { font-weight: 700; color: var(--text-primary); }
.work-tags { display: flex; gap: 6px; }
.category-tag { font-size: 0.7rem; background: var(--bg-sub); color: var(--text-secondary); padding: 2px 6px; border-radius: 3px; }
.feature-tag { font-size: 0.7rem; background: var(--bg-sub); color: var(--danger); border: 1px solid var(--danger-soft); padding: 1px 4px; border-radius: 3px; }
.author-info { display: flex; flex-direction: column; }
.author-name { font-weight: 600; color: var(--text-primary); }
.text-muted { color: var(--text-secondary); }
.mono { font-family: ui-monospace, monospace; }
.font-sm { font-size: 0.8rem; }
.font-xs { font-size: 0.7rem; }
.text-truncate { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 160px; display: inline-block; }
.stats-matrix { display: flex; gap: 8px; color: var(--text-secondary); }
.stat-item { background: var(--bg-main); padding: 2px 6px; border-radius: 4px; border: 1px solid var(--border-line); }
.status-badge { font-size: 0.75rem; font-weight: 600; padding: 4px 8px; border-radius: 4px; }
.status-badge.published { background: var(--success-soft); color: var(--success); }
.status-badge.reviewing { background: var(--bg-main); color: var(--warning); }
.status-badge.rejected { background: var(--danger-soft); color: var(--danger); text-decoration: line-through; }
.status-badge.hidden { background: var(--bg-sub); color: var(--text-secondary); }
.btn-action { background: none; border: none; font-size: 0.8rem; font-weight: 700; cursor: pointer; margin-left: 12px; padding: 0; }
.btn-action.edit { color: var(--accent-color); }
.btn-action.danger { color: var(--danger); }
.btn-action:hover { text-decoration: underline; }
.pagination-footer { display: flex; justify-content: space-between; align-items: center; padding: 16px 0; border-top: 1px solid var(--border-line); margin-top: 10px; }
.page-controls { display: flex; align-items: center; gap: 12px; }
.btn-page { background: var(--paper-card); border: 1px solid var(--border-line); padding: 4px 12px; border-radius: 4px; cursor: pointer; }
.btn-page:disabled { opacity: 0.4; cursor: not-allowed; }

.modal-mask { position: fixed; inset: 0; background: var(--overlay); backdrop-filter: blur(12px); z-index: 9999; display: flex; justify-content: center; align-items: center; }
.modal-container { background: var(--paper-card); border: 1px solid var(--border-line); width: 500px; padding: 30px; 
  max-height: 90dvh; overflow-y: auto; border-radius: var(--radius-md);
}
.modal-header { display: flex; justify-content: space-between; margin-bottom: 20px; }
.close-icon { background: none; border: none; font-size: 1.5rem; cursor: pointer; }
.gov-fieldset { border: 1px solid var(--border-line); padding: 16px; margin-bottom: 20px; }
.gov-fieldset legend { font-size: 0.75rem; color: var(--text-secondary); font-weight: 800; }
.danger-zone { border-color: var(--danger-soft); background: var(--paper-card); }
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.field { display: flex; flex-direction: column; gap: 4px; }
.field.full { grid-column: span 2; }
.field label { font-size: 0.7rem; color: var(--text-secondary); font-weight: 700; }
.field input, .field select { padding: 8px; border: 1px solid var(--border-line); }
.modal-footer { display: flex; justify-content: flex-end; gap: 10px; }
.btn-cancel { padding: 10px 20px; border: 1px solid var(--border-line); background: none; cursor: pointer; }
.btn-submit { padding: 10px 20px; background: var(--accent-color); color: var(--on-accent); border: none; cursor: pointer; }
@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
</style>