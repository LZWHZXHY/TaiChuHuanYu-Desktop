<template>
  <div class="project-reports-root">
    <!-- 顶部操作栏 -->
    <div class="reports-header-bar">
      <div class="filter-pills">
        <button 
          v-for="type in ['all', '日报', '周报', '月报']" 
          :key="type"
          :class="['pill-btn', { active: currentFilter === type }]"
          @click="currentFilter = type"
        >
          {{ type === 'all' ? '全部汇报' : type }}
        </button>
      </div>

      <button class="btn-create" @click="showModal = true">+ 提交汇报</button>
    </div>

    <!-- 加载中状态 -->
    <div v-if="isLoading" class="loading-state">
      <div class="loading-bar"></div>
    </div>

    <!-- 汇报流列表 -->
    <div v-else class="reports-list">
      <article v-for="rep in filteredReports" :key="rep.id" class="report-entry">
        <div class="entry-top">
          <div class="author-info">
            <span class="avatar-box">{{ rep.author ? rep.author.charAt(0) : '?' }}</span>
            <div>
              <strong class="author-name">{{ rep.author }}</strong>
              <span class="report-meta">{{ rep.date }} · 投入 {{ rep.hours }}h · {{ rep.module || '常规推进' }}</span>
            </div>
          </div>
          <span class="type-tag" :class="rep.type">{{ rep.type }}</span>
        </div>

        <div class="entry-content">
          <div class="content-row">
            <span class="label">【核心产出 & 进展】</span>
            <p>{{ rep.summary }}</p>
          </div>
          <div v-if="rep.blockers" class="content-row blocker-row">
            <span class="label">【阻塞对齐 / 踩坑】</span>
            <p>{{ rep.blockers }}</p>
          </div>
          <div v-if="rep.nextPlan" class="content-row">
            <span class="label">【下阶段规划】</span>
            <p>{{ rep.nextPlan }}</p>
          </div>
        </div>

        <!-- 现场存证图片 -->
        <div v-if="rep.images && rep.images.length" class="entry-images">
          <img 
            v-for="(img, idx) in rep.images" 
            :key="idx" 
            :src="img" 
            alt="存证" 
            @click="openImg(img)" 
          />
        </div>
      </article>

      <div v-if="filteredReports.length === 0" class="empty-hint">
        暂无该类别的汇报记录
      </div>
    </div>

    <!-- 提交报告弹窗 (集成 useCos 直传) -->
    <div v-if="showModal" class="modal-backdrop" @click.self="showModal = false">
      <div class="modal-window">
        <header class="modal-head">
          <h3>登记项目汇报</h3>
          <button class="close-x" @click="showModal = false">×</button>
        </header>

        <div class="modal-form">
          <div class="form-row">
            <div class="form-col flex-1">
              <label>汇报类别</label>
              <select v-model="newForm.type" class="clean-input">
                <option value="日报">工作日报</option>
                <option value="周报">周期周报</option>
                <option value="月报">阶段月报</option>
              </select>
            </div>
            <div class="form-col flex-1">
              <label>关联业务意图/模块</label>
              <input v-model="newForm.module" class="clean-input" placeholder="如：剧情大纲、UI交互..." />
            </div>
            <div class="form-col w-120">
              <label>投入工时 (h)</label>
              <input type="number" step="0.5" v-model.number="newForm.hours" class="clean-input" />
            </div>
          </div>

          <div class="form-col">
            <label>核心产出与进展 *</label>
            <textarea 
              v-model="newForm.summary" 
              rows="3" 
              class="clean-input" 
              placeholder="简述交付成果或攻克难点..."
            ></textarea>
          </div>

          <div class="form-col">
            <label>遇到的阻碍或需协作问题（选填）</label>
            <input v-model="newForm.blockers" class="clean-input" placeholder="如需其他成员协助请注明" />
          </div>

          <div class="form-col">
            <label>下一步计划</label>
            <input v-model="newForm.nextPlan" class="clean-input" placeholder="下一阶段的核心目标" />
          </div>

          <!-- COS 快速传图区 -->
          <div class="form-col">
            <label>研发切片存证 (光标聚焦后按 Ctrl+V 即可直传 COS)</label>
            <div class="drop-zone" tabindex="0" @paste="handlePaste">
              <span v-if="!isUploading">点击聚焦后按 Ctrl+V 粘贴截图附件</span>
              <span v-else>正在直传腾讯云 COS...</span>
            </div>
            <div v-if="newForm.images.length" class="img-preview-bar">
              <div v-for="(u, i) in newForm.images" :key="i" class="preview-item">
                <img :src="u" alt="preview" />
                <button class="del-btn" @click="newForm.images.splice(i, 1)">×</button>
              </div>
            </div>
          </div>
        </div>

        <footer class="modal-foot">
          <button class="btn-cancel" @click="showModal = false">取消</button>
          <button class="btn-confirm" :disabled="isSubmitting || isUploading" @click="submitReport">
            {{ isSubmitting ? '正在归档...' : '提交归档' }}
          </button>
        </footer>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { useCos } from '@/composables/useCos';
import request from '@/utils/request';

const props = defineProps<{
  projectId: string;
  initialData?: any;
}>();

const emit = defineEmits(['updated']);

const { uploadFile, isUploading } = useCos();

const currentFilter = ref('all');
const showModal = ref(false);
const isLoading = ref(false);
const isSubmitting = ref(false);
const reports = ref<any[]>([]);

const filteredReports = computed(() => {
  if (currentFilter.value === 'all') return reports.value;
  return reports.value.filter(r => r.type === currentFilter.value);
});

const newForm = ref({
  type: '周报',
  module: '常规推进',
  hours: 4,
  summary: '',
  blockers: '',
  nextPlan: '',
  images: [] as string[]
});

// 🌟 从真实后端拉取汇报流水
const fetchReports = async () => {
  if (!props.projectId) return;
  isLoading.value = true;
  try {
    const res: any = await request.get(`/project/${props.projectId}/reports`);
    const list = res.data || res || [];
    reports.value = list.map((r: any) => ({
      id: r.id,
      author: r.authorName || '成员',
      type: r.type,
      module: r.module,
      hours: r.hours,
      summary: r.summary,
      blockers: r.blockers,
      nextPlan: r.nextPlan,
      images: r.images || [],
      date: r.date
    }));
  } catch (err: any) {
    console.error('拉取汇报流水失败:', err);
  } finally {
    isLoading.value = false;
  }
};

const handlePaste = async (e: ClipboardEvent) => {
  const items = e.clipboardData?.items;
  if (!items) return;
  for (const item of items) {
    if (item.type.includes('image')) {
      e.preventDefault();
      const file = item.getAsFile();
      if (!file) continue;
      try {
        const res = await uploadFile(file, `projects/${props.projectId}/reports`);
        newForm.value.images.push(res.url);
      } catch (uploadErr) {
        console.error('截图上传 COS 失败:', uploadErr);
        alert('图片存证上传失败，请重试');
      }
      break;
    }
  }
};

// 🌟 向后端真实提交汇报
const submitReport = async () => {
  if (!newForm.value.summary.trim()) {
    alert('请填写主要产出内容');
    return;
  }

  isSubmitting.value = true;
  try {
    await request.post(`/project/${props.projectId}/reports`, {
      type: newForm.value.type,
      module: newForm.value.module,
      hours: newForm.value.hours,
      summary: newForm.value.summary.trim(),
      blockers: newForm.value.blockers,
      nextPlan: newForm.value.nextPlan,
      images: newForm.value.images
    });

    // 成功后重置表单并关闭弹窗
    newForm.value = { 
      type: '周报', 
      module: '常规推进',
      hours: 4, 
      summary: '', 
      blockers: '', 
      nextPlan: '', 
      images: [] 
    };
    showModal.value = false;

    // 刷新列表并通知父级
    await fetchReports();
    emit('updated');
    alert('汇报已成功归档入脉！');
  } catch (err: any) {
    console.error('提交汇报失败:', err);
    alert(err.response?.data?.message || err.response?.data || '提交汇报失败，请确认您在此项目中具备汇报权限');
  } finally {
    isSubmitting.value = false;
  }
};

const openImg = (u: string) => window.open(u, '_blank');

watch(() => props.projectId, () => {
  fetchReports();
}, { immediate: true });

onMounted(fetchReports);
</script>

<style scoped>
.project-reports-root { width: 100%; max-width: 900px; animation: fadeIn 0.6s ease; }
.reports-header-bar { display: flex; justify-content: space-between; align-items: center; margin-bottom: 32px; }
.filter-pills { display: flex; gap: 8px; }
.pill-btn { background: #fafafa; border: 1px solid #f0f0f0; padding: 6px 16px; border-radius: 20px; font-size: 0.8rem; color: #666; cursor: pointer; transition: all 0.2s; }
.pill-btn.active { background: #1a1a1a; color: #fff; border-color: #1a1a1a; }
.btn-create { background: #1a1a1a; color: #fff; border: none; padding: 8px 20px; border-radius: 2px; font-size: 0.85rem; cursor: pointer; transition: background 0.3s; }
.btn-create:hover { background: #333; }

.loading-state { height: 160px; display: flex; align-items: center; justify-content: center; }
.loading-bar { width: 60px; height: 1px; background: #1a1a1a; animation: pulse 1.5s infinite; }

.reports-list { display: flex; flex-direction: column; gap: 20px; }
.report-entry { background: #fff; border: 1px solid #f2f2f2; padding: 24px; box-shadow: 0 4px 16px rgba(0,0,0,0.01); transition: box-shadow 0.2s; }
.report-entry:hover { box-shadow: 0 8px 24px rgba(0,0,0,0.04); }
.entry-top { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
.author-info { display: flex; gap: 10px; align-items: center; }
.avatar-box { width: 32px; height: 32px; border-radius: 50%; background: #1a1a1a; color: #fff; display: flex; align-items: center; justify-content: center; font-size: 0.85rem; font-weight: bold; }
.author-name { font-size: 0.95rem; color: #1a1a1a; display: block; }
.report-meta { font-size: 0.75rem; color: #aaa; }
.type-tag { font-size: 0.7rem; padding: 2px 8px; border-radius: 2px; font-weight: 500; }
.type-tag.日报 { background: #e0f2fe; color: #0284c7; }
.type-tag.周报 { background: #fef3c7; color: #d97706; }
.type-tag.月报 { background: #dcfce7; color: #16a34a; }

.entry-content { display: flex; flex-direction: column; gap: 8px; font-size: 0.9rem; line-height: 1.6; color: #333; }
.content-row .label { font-size: 0.8rem; font-weight: 600; color: #666; }
.blocker-row { color: #dc2626; }
.entry-images { display: flex; gap: 8px; margin-top: 14px; flex-wrap: wrap; }
.entry-images img { width: 90px; height: 60px; object-fit: cover; border-radius: 2px; cursor: pointer; border: 1px solid #eee; transition: transform 0.2s; }
.entry-images img:hover { transform: scale(1.05); }

.modal-backdrop { position: fixed; inset: 0; background: rgba(0,0,0,0.35); backdrop-filter: blur(4px); display: flex; align-items: center; justify-content: center; z-index: 1000; }
.modal-window { background: #fff; width: 540px; padding: 32px; box-shadow: 0 20px 60px rgba(0,0,0,0.12); }
.modal-head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
.modal-head h3 { margin: 0; font-size: 1.15rem; font-weight: 500; }
.close-x { background: none; border: none; font-size: 1.4rem; cursor: pointer; color: #aaa; }
.form-row { display: flex; gap: 12px; }
.flex-1 { flex: 1; }
.w-120 { width: 120px; }
.form-col { margin-bottom: 14px; }
.form-col label { display: block; font-size: 0.75rem; color: #888; margin-bottom: 6px; }
.clean-input { width: 100%; border: 1px solid #eee; padding: 8px 12px; outline: none; box-sizing: border-box; font-size: 0.9rem; transition: border-color 0.2s; }
.clean-input:focus { border-color: #1a1a1a; }
.drop-zone { border: 1px dashed #ddd; padding: 14px; text-align: center; font-size: 0.75rem; color: #999; cursor: pointer; outline: none; transition: all 0.2s; }
.drop-zone:focus, .drop-zone:hover { border-color: #1a1a1a; color: #1a1a1a; background: #fafafa; }
.img-preview-bar { display: flex; gap: 6px; margin-top: 6px; }
.preview-item { position: relative; width: 60px; height: 40px; }
.preview-item img { width: 100%; height: 100%; object-fit: cover; border-radius: 2px; }
.del-btn { position: absolute; top: -4px; right: -4px; background: #ff4757; color: #fff; border: none; border-radius: 50%; width: 16px; height: 16px; font-size: 10px; cursor: pointer; display: flex; align-items: center; justify-content: center; }
.modal-foot { display: flex; justify-content: flex-end; gap: 10px; margin-top: 24px; }
.btn-cancel { background: #f5f5f5; border: none; padding: 8px 16px; font-size: 0.85rem; cursor: pointer; }
.btn-confirm { background: #1a1a1a; color: #fff; border: none; padding: 8px 24px; font-size: 0.85rem; cursor: pointer; transition: background 0.3s; }
.btn-confirm:hover { background: #333; }
.btn-confirm:disabled { background: #ccc; cursor: not-allowed; }
.empty-hint { text-align: center; padding: 60px 0; color: #bbb; font-size: 0.85rem; }

@keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
@keyframes pulse { 0% { transform: scaleX(0.5); opacity: 0.2; } 50% { transform: scaleX(1.5); opacity: 1; } 100% { transform: scaleX(0.5); opacity: 0.2; } }
</style>