<template>
  <div class="table-card">
    <div class="filter-bar" style="margin-bottom: 20px;">
      <label>
        <input type="checkbox" v-model="showOnlyPending" /> 仅显示待审核记录
      </label>
    </div>

    <table class="ink-table">
      <thead>
        <tr>
          <th>标题</th>
          <th>分类</th>
          <th>提交人</th>
          <th>提交时间</th>
          <th>状态</th>
          <th>内容预览</th>
          <th class="text-right">决策</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="rev in filteredData" :key="rev.id">
          <td>
            <div class="rev-title">{{ rev.title }}</div>
            <div class="rev-summary">{{ rev.editSummary }}</div>
          </td>
          <td>{{ rev.categoryName }}</td> 
          <td>
            <div class="user-info">
              <strong>{{ rev.contributorName }}</strong>
              <span v-if="rev.isOriginal" class="tag tag-original">发布人</span>
              <span v-else class="tag tag-contributor">贡献者</span>
            </div>
            <div class="mono" style="font-size: 0.7rem; color: #888;">
              ID: {{ rev.contributorId?.substring(0, 6) }}
            </div>
          </td>
          <td class="text-gray">{{ formatDate(rev.createdAt) }}</td>
          <td>
            <span :class="getStatusClass(rev.status)">
              {{ rev.status === 0 ? '待审核' : (rev.status === 1 ? '已通过' : '已驳回') }}
            </span>
          </td>
          <td class="link" @click="openPreview(rev.content)">查看</td>
          <td class="text-right actions">
            <template v-if="rev.status === 0">
              <button class="btn-s success" @click="handleReview(rev, true)">[通过]</button>
              <button class="btn-s danger" @click="handleReview(rev, false)">[驳回]</button>
            </template>
            <span v-else class="text-gray">-</span>
          </td>
        </tr>
      </tbody>
    </table>

    <Teleport to="body">
      <div v-if="showModal" class="modal-mask" @mousedown="showModal = false">
        <div class="modal-container" @mousedown.stop>
          <div class="modal-header">
            <h3>内容详情预览</h3>
            <button class="btn-close" @click="showModal = false">✕</button>
          </div>
          <div class="preview-scroll-area">
            <SpiritPreview :model-value="previewContent" />
          </div>
          <button class="btn-black" @click="showModal = false" style="margin-top: 20px;">关闭</button>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';
import { wikiReviewApi } from '@/api/Admin';
import SpiritPreview from '@/components/SpiritTextComponents/SpiritPreview.vue';

const props = defineProps<{ data: any[] }>();
const emit = defineEmits(['refresh']);

const showOnlyPending = ref(true);
const showModal = ref(false);
const previewContent = ref<any>(null);

const filteredData = computed(() => {
  return showOnlyPending.value 
    ? props.data.filter(rev => rev.status === 0) 
    : props.data;
});

const formatDate = (isoString: string) => {
  return isoString ? new Date(isoString).toLocaleString() : '-';
};

const handleReview = async (rev: any, approved: boolean) => {
  if (approved) {
    const others = props.data.filter(i => 
      i.articleId === rev.articleId && i.id !== rev.id && i.status === 0
    );
    
    if (others.length > 0 && !confirm(`批准此版本将自动驳回该文章其他 ${others.length} 个待审核版本。确定继续吗？`)) {
      return;
    }
    for (const o of others) {
      await wikiReviewApi.handle(o.id, { approved: false, remarks: '新版本已通过，自动失效' });
    }
  }
  
  await wikiReviewApi.handle(rev.id, { approved, remarks: approved ? '通过' : '驳回' });
  emit('refresh');
};

const openPreview = (content: string) => {
  previewContent.value = content;
  showModal.value = true;
};

const getStatusClass = (status: number) => {
  if (status === 0) return 'text-gray';
  if (status === 1) return 'status-live';
  return 'status-archived';
};
</script>

<style scoped>
@import './Wiki子组件风格.css';

.rev-title { font-weight: 600; color: #000; }
.rev-summary { font-size: 0.75rem; color: #86868b; margin-top: 2px; }
.user-info { display: flex; align-items: center; gap: 6px; }
.tag { font-size: 0.65rem; padding: 1px 4px; border-radius: 4px; }
.tag-original { background: #e6ffed; color: #28a745; border: 1px solid #b7eb8f; }
.tag-contributor { background: #e6f7ff; color: #007bff; border: 1px solid #91d5ff; }

.filter-bar { font-size: 0.85rem; color: #515154; cursor: pointer; }
.modal-mask { position: fixed; inset: 0; background: rgba(0,0,0,0.5); display: flex; justify-content: center; align-items: center; z-index: 9999; }
.modal-container { background: #fff; width: 600px; padding: 30px; border-radius: 12px; display: flex; flex-direction: column; max-height: 80vh; }
.modal-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
.preview-scroll-area { flex: 1; overflow-y: auto; border: 1px solid #f2f2f7; padding: 20px; border-radius: 8px; }
.btn-close { border: none; background: none; cursor: pointer; font-size: 1.2rem; }
.btn-black { width: 100%; background: #000; color: #fff; padding: 12px; border: none; cursor: pointer; border-radius: 6px; }
</style>