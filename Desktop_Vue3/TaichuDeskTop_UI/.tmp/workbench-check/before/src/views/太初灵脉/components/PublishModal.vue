<template>
  <transition name="fade">
    <div v-if="modelValue" class="md-overlay" @click.self="close()">
      <div class="md-container">
        <header class="md-header">
          <div class="md-title">
            <span class="md-label">发布碎片</span>
            <h1>{{ publishTitle }}</h1>
          </div>
          <button class="md-close" @click="close()">✕</button>
        </header>

        <div class="md-body">
          <div v-if="noteType === 'doc'" class="simple-confirm-text">分享后，项目成员可以阅读当前文档；后续修改也会同步显示。此操作不改变文档原有的公开状态。</div>
          <div v-else class="simple-confirm-text">
            即将将此 <strong>{{ displayType }}</strong> 固化至灵脉广场。
          </div>

          <transition name="fade">
            <div v-if="noteType === 'wiki'" class="extra-zone">
              <div class="form-group">
                <label>所属界域 (Category)</label>
                <select v-model="wikiData.categoryId" :disabled="isLoadingCategories">
                  <option value="" disabled>选择界域...</option>
                  <option v-for="cat in flatCategories" :key="cat.id" :value="cat.id">
                    {{ '—'.repeat(cat.level) }} {{ cat.name }}
                  </option>
                </select>
              </div>

              <div class="form-group">
                <label>意象标签 (Tags)</label>
                <div class="tag-input-box">
                  <span v-for="(tag, i) in wikiData.tags" :key="i" class="tag-pill">
                    {{ tag }} <button @click="removeTag(i)">×</button>
                  </span>
                  <input v-model="tagInput" @keydown.enter.prevent="addTag" placeholder="输入标签按回车" />
                </div>
              </div>
            </div>
          </transition>

          <transition name="fade">
            <div v-if="noteType === 'doc'" class="extra-zone">
              <div class="form-group">
                <label>归属协作项目 (Project)</label>
                <select v-model="projectData.projectId" :disabled="isLoadingProjects">
                  <option value="" disabled>选择要发布到的项目...</option>
                  <option v-for="proj in availableProjects" :key="proj.id" :value="proj.id">
                    {{ proj.name }}
                  </option>
                </select>
              </div>
            </div>
          </transition>
        </div>

        <p v-if="error" class="publish-error" role="alert">{{ error }}</p>
        <footer class="md-footer">
          <button class="btn-cancel" @click="close()">放弃</button>
          <button 
            class="btn-primary" 
            :disabled="isProcessing || !canSubmit"
            @click="handleConfirm"
          >
            {{ isProcessing ? '正在保存...' : noteType === 'doc' ? '确认分享' : '确认发布' }}
          </button>
        </footer>
      </div>
    </div>
  </transition>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch, onUnmounted } from 'vue';
import { wikiApi, type IWikiCategory } from '@/api/Wiki';
import { lingmaiApi } from '../../../api/lingmai';
import projectService from '@/api/projectService'; 

const props = defineProps<{ 
  modelValue: boolean; 
  noteId: string; 
  noteType: string; 
  spaceName: string;
  beforePublish?: (noteId: string) => Promise<void>; 
}>();

const emit = defineEmits(['update:modelValue', 'success']);

const isProcessing = ref(false), error = ref('');
let generation = 0;
const close = () => { if (!isProcessing.value) emit('update:modelValue', false); };
onUnmounted(() => { generation++; });

// Wiki 数据
const wikiData = reactive({ categoryId: 3, tags: [] as string[] }); 
const tagInput = ref('');
const rawCategories = ref<IWikiCategory[]>([]);
const isLoadingCategories = ref(false);

// Doc (项目文档) 数据
const projectData = reactive({ projectId: '' });
const availableProjects = ref<{ id: string; name: string }[]>([]); // 🌟 显式锁死强类型约束
const isLoadingProjects = ref(false);

const displayType = computed(() => {
  const map: Record<string, string> = { 
    'wiki': '百科词条', 'char': '角色档案', 
    'post': '太初随笔', 'blog': '博客', 'art': '艺术画廊', 'doc': '项目文档' 
  };
  return map[props.noteType] || '灵脉碎片';
});

const publishTitle = computed(() => {
  if (props.noteType === 'wiki') return '发布至百科宇宙';
  if (props.noteType === 'doc') return '发布至协作项目'; 
  return '固化至灵脉广场';
});

const flatCategories = computed(() => {
  const result: (IWikiCategory & { level: number })[] = [];
  const buildTree = (parentId: number | null, level: number) => {
    rawCategories.value.filter(c => c.parentId === parentId).forEach(c => {
      result.push({ ...c, level });
      buildTree(c.id, level + 1);
    });
  };
  buildTree(null, 0);
  return result;
});

// 拦截逻辑：不同类型校验不同必填项
const canSubmit = computed(() => {
  if (props.noteType === 'wiki') return !isLoadingCategories.value && flatCategories.value.some(c => c.id === wikiData.categoryId);
  if (props.noteType === 'doc') return !isLoadingProjects.value && !!projectData.projectId;
  return true;
});

const addTag = () => {
  const t = tagInput.value.trim();
  if (t && !wikiData.tags.includes(t)) wikiData.tags.push(t);
  tagInput.value = '';
};

const removeTag = (i: number) => wikiData.tags.splice(i, 1);

const handleConfirm = async () => {
  if (isProcessing.value || !canSubmit.value) return;
  const noteId = props.noteId, type = props.noteType, scope = generation;
  isProcessing.value = true; error.value = '';
  try {
    await props.beforePublish?.(noteId);
    if (scope !== generation) return;
    const payload = { type, ...(type === 'wiki' ? { categoryId: wikiData.categoryId, tags: wikiData.tags } : {}),
      ...(type === 'doc' ? { projectId: projectData.projectId } : {}) };
    const result = await lingmaiApi.publishNote(noteId, payload);
    if (scope !== generation) return;
    emit('success', { noteId, message: result.message }); emit('update:modelValue', false);
  } catch (cause: any) {
    if (scope === generation) error.value = cause.friendlyMessage || cause.message || '发布失败，请重试';
  } finally { isProcessing.value = false; }
};

watch(() => [props.modelValue, props.noteId, props.noteType], async () => {
  const scope = ++generation;
  error.value = ''; isLoadingCategories.value = false; isLoadingProjects.value = false;
  if (!props.modelValue) return;
  projectData.projectId = ''; rawCategories.value = []; availableProjects.value = [];
  wikiData.categoryId = 3; wikiData.tags = []; tagInput.value = '';
  try {
    if (props.noteType === 'wiki') {
      isLoadingCategories.value = true;
      const data = await wikiApi.getCategories();
      if (scope === generation) rawCategories.value = data || [];
    } else if (props.noteType === 'doc') {
      isLoadingProjects.value = true;
      const data = await projectService.getMyProjects();
      if (scope === generation) availableProjects.value = data.map(item => ({ id: item.id, name: item.name }));
    }
  } catch (cause: any) {
    if (scope === generation) error.value = cause.friendlyMessage || '发布选项加载失败，请关闭后重新打开';
  } finally {
    if (scope === generation) { isLoadingCategories.value = false; isLoadingProjects.value = false; }
  }
}, { immediate: true });
</script>

<style scoped>
.publish-error { color: var(--accent-color); font-size: .85rem; }
input, select { background: var(--paper-card); color: var(--text-primary); }
.md-container { max-width: calc(100vw - 32px); box-sizing: border-box; max-height: 90vh; overflow-y: auto; }
.md-overlay { position: fixed; inset: 0; background: var(--paper-card); backdrop-filter: blur(8px); display: flex; align-items: center; justify-content: center; z-index: 5000; }
.md-container { width: 440px; background: var(--paper-bg); padding: 32px; border-radius: 20px; box-shadow: 0 20px 40px rgba(0,0,0,0.08); }
.md-header { margin-bottom: 32px; display: flex; justify-content: space-between; }
.md-label { font-size: 10px; color: var(--text-secondary); text-transform: uppercase; letter-spacing: 0.1em; }
.md-title h1 { font-size: 20px; font-weight: 700; margin: 4px 0 0; }
.simple-confirm-text { padding: 20px; color: var(--text-primary); font-size: 14px; text-align: center; border: 1px solid var(--border-line); border-radius: 12px; }
.extra-zone { padding-top: 10px; }
.form-group { margin-bottom: 16px; }
.form-group label { display: block; font-size: 11px; color: var(--text-secondary); margin-bottom: 8px; }
select { width: 100%; border: 1px solid var(--border-line); padding: 8px; border-radius: 6px; font-size: 13px; outline: none; }
.tag-input-box { border: 1px solid var(--border-line); border-radius: 6px; padding: 4px; display: flex; flex-wrap: wrap; gap: 4px; }
.tag-pill { background: var(--border-line); padding: 2px 8px; border-radius: 4px; font-size: 11px; display: flex; align-items: center; gap: 4px; }
.tag-pill button { border: none; background: none; cursor: pointer; }
.tag-input-box input { border: none; padding: 4px; font-size: 12px; outline: none; flex: 1; }
.md-footer { margin-top: 32px; display: flex; gap: 12px; }
.btn-cancel { flex: 1; background: none; border: none; color: var(--text-secondary); font-size: 13px; cursor: pointer; }
.btn-primary { flex: 2; padding: 10px; background: var(--accent-color); color: var(--paper-card); border: none; border-radius: 8px; font-weight: 600; cursor: pointer; }
.btn-primary:disabled { opacity: 0.4; cursor: not-allowed; }
.md-close { background: none; border: none; color: var(--text-secondary); cursor: pointer; }
</style>
