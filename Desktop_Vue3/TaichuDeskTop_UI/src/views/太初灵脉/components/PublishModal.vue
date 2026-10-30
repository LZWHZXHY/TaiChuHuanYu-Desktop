<template>
  <transition name="fade">
    <div v-if="modelValue" class="md-overlay" @click.self="close()">
      <div class="md-container workbench-skin editor-surface community-surface tone-library">
        <header class="md-header studio-heading"><SectionTrace />
          <div class="md-title">
            <span class="md-label">{{ noteType === 'doc' ? '分享' : '发布' }}</span>
            <h1>{{ publishTitle }}</h1>
          </div>
          <button class="md-close" @click="close()">✕</button>
        </header>

        <div class="md-body">
          <div v-if="noteType === 'doc'" class="simple-confirm-text">分享后，项目成员可以阅读当前文档；后续修改也会同步显示。此操作不改变文档原有的公开状态。</div>
          <div v-else-if="isStreamType" class="simple-confirm-text">
            发布后会出现在 <strong>论坛</strong>，之后修改可以再次“更新发布”。
          </div>
          <div v-else class="simple-confirm-text">
            这篇<strong>{{ displayType }}</strong>发布后会出现在社区，之后修改可以再次“更新发布”。
          </div>

          <!-- 帖子和博客按标签进入论坛版块；在这里直接选，不必再去右侧面板手动加标签。 -->
          <fieldset v-if="isStreamType" class="topic-zone">
            <legend>发布到版块</legend>
            <label class="topic-option"><input v-model="topic" type="radio" value="" /> 不选版块</label>
            <label v-for="item in FORUM_BOARDS" :key="item.tag" class="topic-option"><input v-model="topic" type="radio" :value="item.tag" /> {{ item.icon }} {{ item.tag }}</label>
            <div v-if="availableSubs.length" class="sub-row">
              <span class="sub-label">小类</span>
              <label v-for="item in availableSubs" :key="item.tag" class="topic-option" :title="item.desc"><input v-model="subTopic" type="radio" :value="item.tag" /> {{ item.tag }}</label>
            </div>
          </fieldset>

          <transition name="fade">
            <div v-if="noteType === 'wiki'" class="extra-zone">
              <div class="form-group">
                <label>百科分类</label>
                <select v-model="wikiData.categoryId" :disabled="isLoadingCategories">
                  <option value="" disabled>选择分类…</option>
                  <option v-for="cat in flatCategories" :key="cat.id" :value="cat.id">
                    {{ '—'.repeat(cat.level) }} {{ cat.name }}
                  </option>
                </select>
              </div>

              <div class="form-group">
                <label>标签（可选）</label>
                <div class="tag-input-box">
                  <span v-for="(tag, i) in wikiData.tags" :key="i" class="tag-pill">
                    {{ tag }} <button @click="removeTag(i)">×</button>
                  </span>
                  <input v-model="tagInput" @keydown.enter.prevent="addTag" placeholder="输入后按回车添加" />
                </div>
              </div>
            </div>
          </transition>

          <transition name="fade">
            <div v-if="noteType === 'doc'" class="extra-zone">
              <div class="form-group">
                <label>分享到哪个项目</label>
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
          <button class="btn-cancel" @click="close()">取消</button>
          <button 
            class="btn-primary" 
            :disabled="isProcessing || !canSubmit"
            @click="handleConfirm"
          >
            {{ isProcessing ? '正在发布…' : noteType === 'doc' ? '确认分享' : '确认发布' }}
          </button>
        </footer>
      </div>
    </div>
  </transition>
</template>

<script setup lang="ts">
import SectionTrace from '@/components/SectionTrace.vue'
import { ref, reactive, computed, watch, onUnmounted } from 'vue';
import { wikiApi, type IWikiCategory } from '@/api/Wiki';
import { lingmaiApi } from '../../../api/lingmai';
import projectService from '@/api/projectService'; 
import { noteKind } from '@/utils/NoteType';
import { FORUM_BOARDS, findBoard, boardOf, applyBoardTags } from '@/utils/forumBoards';
import { canOpenAdmin } from '@/utils/adminAccess';
import { useUserStore } from '@/stores/user';

const props = defineProps<{ 
  modelValue: boolean; 
  noteId: string; 
  noteType: string; 
  spaceName: string;
  currentTags?: string[];
  beforePublish?: (noteId: string, tags?: string[]) => Promise<void>; 
}>();

// 论坛版块与小类统一取自 forumBoards，发布时写入笔记标签。
const isStreamType = computed(() => props.noteType === 'post' || props.noteType === 'blog');
const topic = ref(''), subTopic = ref('');
const userStore = useUserStore();
const availableSubs = computed(() => (findBoard(topic.value)?.subs || []).filter(sub => !sub.adminOnly || canOpenAdmin(userStore.permissions)));
watch(topic, () => { if (!availableSubs.value.some(sub => sub.tag === subTopic.value)) subTopic.value = availableSubs.value[0]?.tag || ''; });

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

const displayType = computed(() => noteKind(props.noteType).label);

const publishTitle = computed(() => {
  const dest = noteKind(props.noteType).destination;
  if (props.noteType === 'doc') return '分享到协作项目';
  return dest ? `发布到「${dest}」` : '发布';
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
    // 发布快照取笔记当前标签，所以先把分区写进标签并保存，再发布。
    const streamTags = isStreamType.value
      ? applyBoardTags(props.currentTags || [], topic.value, subTopic.value)
      : undefined;
    await props.beforePublish?.(noteId, streamTags);
    if (scope !== generation) return;
    const payload = { type, ...(type === 'wiki' ? { categoryId: wikiData.categoryId, tags: wikiData.tags } : {}),
      ...(streamTags ? { tags: streamTags } : {}),
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
  const existing = boardOf(props.currentTags || []);
  topic.value = existing.board?.tag || '';
  subTopic.value = existing.sub?.tag || '';
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
.md-container { width: 440px; background: var(--paper-bg); padding: 32px; border-radius: 0; box-shadow: 0 20px 40px rgba(0,0,0,0.08); }
.md-header { margin-bottom: 32px; display: flex; justify-content: space-between; }
.md-label { font-size: 10px; color: var(--text-secondary); text-transform: uppercase; letter-spacing: 0.1em; }
.md-title h1 { font-size: 20px; font-weight: 700; margin: 4px 0 0; }
.simple-confirm-text { padding: 20px; color: var(--text-primary); font-size: 14px; text-align: center; border: 1px solid var(--border-line); border-radius: 0; }
.extra-zone { padding-top: 10px; }
.topic-zone { margin: 16px 0 0; padding: 12px 16px; border: 1px solid var(--border-line); display: flex; flex-wrap: wrap; gap: 8px 20px; }
.topic-zone legend { padding: 0 6px; font-size: 11px; color: var(--text-secondary); }
.sub-row { flex-basis: 100%; display: flex; flex-wrap: wrap; align-items: center; gap: 8px 16px; padding-top: 8px; border-top: 1px dashed var(--border-line); }
.sub-label { font-size: 11px; color: var(--text-secondary); }
.topic-option { display: inline-flex; align-items: center; gap: 6px; font-size: 13px; color: var(--text-primary); cursor: pointer; white-space: nowrap; }
.form-group { margin-bottom: 16px; }
.form-group label { display: block; font-size: 11px; color: var(--text-secondary); margin-bottom: 8px; }
select { width: 100%; border: 1px solid var(--border-line); padding: 8px; border-radius: 0; font-size: 13px; outline: none; }
.tag-input-box { border: 1px solid var(--border-line); border-radius: 0; padding: 4px; display: flex; flex-wrap: wrap; gap: 4px; }
.tag-pill { background: var(--border-line); padding: 2px 8px; border-radius: 0; font-size: 11px; display: flex; align-items: center; gap: 4px; }
.tag-pill button { border: none; background: none; cursor: pointer; }
.tag-input-box input { border: none; padding: 4px; font-size: 12px; outline: none; flex: 1; }
.md-footer { margin-top: 32px; display: flex; gap: 12px; }
.btn-cancel { flex: 1; background: none; border: none; color: var(--text-secondary); font-size: 13px; cursor: pointer; }
.btn-primary { flex: 2; padding: 10px; background: var(--accent-color); color: var(--paper-card); border: none; border-radius: 0; font-weight: 600; cursor: pointer; }
.btn-primary:disabled { opacity: 0.4; cursor: not-allowed; }
.md-close { background: none; border: none; color: var(--text-secondary); cursor: pointer; }
</style>
