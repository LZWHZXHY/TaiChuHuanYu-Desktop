<template>
  <transition name="fade">
    <div v-if="modelValue" class="md-modal-overlay" @click.self="$emit('update:modelValue', false)">
      <div class="md-modal-container">
        <header class="md-header">
          <div class="md-title">
            <h1>编织发布</h1>
            <span class="md-space-tag"># {{ spaceName }}</span>
          </div>
          <button class="md-close-btn" @click="$emit('update:modelValue', false)">ESC</button>
        </header>

        <div class="md-body">
          <p class="md-label">选择折射形态 / SELECT TYPE</p>
          <ul class="md-type-list">
            <li 
              v-for="opt in PUBLISH_OPTIONS" 
              :key="opt.type"
              :class="{ 'is-active': selectedType === opt.type }"
              @click="selectedType = opt.type"
            >
              <span class="md-radio-indicator"></span>
              <div class="md-type-content">
                <span class="md-type-title">{{ opt.title }}</span>
                <span class="md-type-desc">{{ opt.desc }}</span>
              </div>
            </li>
          </ul>

          <transition name="expand">
            <div v-if="selectedType === 'wiki'" class="md-extra-form">
              <div class="md-form-group">
                <label class="md-label">词条分类 / CATEGORY</label>
                <select v-model="wikiData.categoryId" class="md-select" :disabled="isLoadingCategories">
                  <option value="" disabled>{{ isLoadingCategories ? '解析界域结构中...' : '请选择所属界域...' }}</option>
                  <option 
                    v-for="cat in flatCategories" 
                    :key="cat.id" 
                    :value="cat.id"
                  >
                    {{ ' '.repeat(cat.level) + (cat.level > 0 ? '└─ ' : '') + cat.name }}
                  </option>
                </select>
              </div>

              <div class="md-form-group">
                <label class="md-label">意象标签 / TAGS</label>
                <div class="md-tags-container">
                  <span 
                    v-for="(tag, index) in wikiData.tags" 
                    :key="index" 
                    class="md-tag"
                  >
                    # {{ tag }}
                    <button class="md-tag-remove" @click="removeTag(index)">&times;</button>
                  </span>
                  <input 
                    v-model="tagInput"
                    @keydown.enter.prevent="addTag"
                    @keydown.delete="handleTagDelete"
                    type="text" 
                    class="md-input" 
                    placeholder="输入标签后按回车..."
                  />
                </div>
              </div>
            </div>
          </transition>
        </div>

        <footer class="md-footer">
          <div class="md-footer-line"></div>
          <div class="md-actions">
            <button class="md-btn-secondary" @click="$emit('update:modelValue', false)">放弃</button>
            <button 
              class="md-btn-primary" 
              :disabled="isProcessing || !canSubmit"
              @click="handleConfirm"
            >
              {{ isProcessing ? '同步中...' : '确认发布' }}
            </button>
          </div>
        </footer>
      </div>
    </div>
  </transition>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch, onMounted } from 'vue';
import { wikiApi, type IWikiCategory } from '@/api/Wiki'; // 🌟 引入真实 API
import { lingmaiApi } from '../../../api/lingmai'; // 原有的笔记发布 API

const props = defineProps<{
  modelValue: boolean;
  noteId: string;
  spaceName: string;
  initialType?: string;
}>();

const emit = defineEmits(['update:modelValue', 'success']);

const PUBLISH_OPTIONS = [
  { type: 'note',    title: '随笔 (Blog)', desc: '深度记录，保留于知识骨架中。' },
  { type: 'thought', title: '简语 (Post)', desc: '瞬时灵感，不占据目录空间。' },
  { type: 'wiki',    title: '词条 (Wiki)', desc: '底层设定，作为世界观之基石。' },
  { type: 'art',     title: '画廊 (Gallery)', desc: '视觉呈现，将意象物理同步至艺术展厅。' }
];

// --- 🌟 分类数据流 ---
const rawCategories = ref<IWikiCategory[]>([]);
const isLoadingCategories = ref(false);

const loadCategories = async () => {
  isLoadingCategories.value = true;
  try {
    rawCategories.value = await wikiApi.getCategories();
  } catch (error) {
    console.error("界域读取失败", error);
  } finally {
    isLoadingCategories.value = false;
  }
};

// 页面挂载时拉取分类
onMounted(loadCategories);

// 🌟 将扁平的 API 数组转换为带有层级深度（level）的拍平列表，方便 <select> 渲染树状结构
const flatCategories = computed(() => {
  const result: (IWikiCategory & { level: number })[] = [];
  
  // 递归寻找子节点
  const buildTree = (parentId: number | null, level: number) => {
    const children = rawCategories.value.filter(c => c.parentId === parentId);
    // 后端已经通过 SortOrder 排序过，所以直接按顺序处理即可
    for (const child of children) {
      result.push({ ...child, level });
      buildTree(child.id, level + 1);
    }
  };

  buildTree(null, 0);
  return result;
});


const selectedType = ref(props.initialType || 'note');
const isProcessing = ref(false);

// Wiki 专属数据状态
const wikiData = reactive({
  categoryId: '' as number | string,
  tags: [] as string[]
});
const tagInput = ref('');

// 监听初始类型变化
watch(() => props.initialType, (val) => {
  if (val) selectedType.value = val;
});

// 重置表单（当切换类型时，可选择性重置）
watch(selectedType, (newType) => {
  if (newType !== 'wiki') {
    wikiData.categoryId = '';
    wikiData.tags = [];
    tagInput.value = '';
  }
});

// 标签逻辑
const addTag = () => {
  const trimmed = tagInput.value.trim();
  if (trimmed && !wikiData.tags.includes(trimmed)) {
    wikiData.tags.push(trimmed);
  }
  tagInput.value = ''; // 清空输入框
};

const removeTag = (index: number) => {
  wikiData.tags.splice(index, 1);
};

// 如果输入框为空且按下删除键，删除最后一个标签
const handleTagDelete = () => {
  if (tagInput.value === '' && wikiData.tags.length > 0) {
    wikiData.tags.pop();
  }
};

// 提交按钮状态控制
const canSubmit = computed(() => {
  if (selectedType.value === 'wiki') {
    // 发布到 Wiki 时，强制要求选择分类
    return !!wikiData.categoryId;
  }
  return true;
});

const handleConfirm = async () => {
  if (!props.noteId || !canSubmit.value) return;
  isProcessing.value = true;
  
  try {
    const payload = {
      noteId: props.noteId,
      type: selectedType.value,
      // 如果是 wiki，将额外数据打包传出
      ...(selectedType.value === 'wiki' ? { 
        categoryId: wikiData.categoryId, 
        tags: wikiData.tags 
      } : {})
    };

    console.log('提交发布载荷:', payload);
    
    // 🌟 核心分发逻辑：Wiki走专属通道，其余全部走灵脉通道
    if (selectedType.value === 'wiki') {
      // Wiki 词条发布
      await wikiApi.publishFromNote(payload);
    } else {
      // 博客(note)、帖子(thought)、画廊(art) 走原有接口
      await lingmaiApi.publishNote(props.noteId, selectedType.value);
    }

    // 成功后通知父组件关闭弹窗并刷新
    emit('success', payload);
    emit('update:modelValue', false);
    
  } catch (err) {
    console.error('发布异常', err);
    alert('发布折射时产生干扰，请按 F12 检查控制台报错。'); // 建议加个失败提示
  } finally {
    isProcessing.value = false;
  }
};
</script>

<style scoped>
/* 你的 CSS 保持绝对不变，我没有修改任何视觉代码以保持你的留白极简设计 */
.md-modal-overlay { position: fixed; inset: 0; background: rgba(255, 255, 255, 0.95); z-index: 5000; display: flex; align-items: center; justify-content: center; backdrop-filter: blur(2px); }
.md-modal-container { width: 100%; max-width: 500px; padding: 40px; background: transparent; color: #1a1a1a; font-family: "Inter", -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; max-height: 90vh; overflow-y: auto; }
.md-modal-container::-webkit-scrollbar { display: none; }
.md-header { display: flex; justify-content: space-between; align-items: baseline; margin-bottom: 60px; }
.md-title h1 { font-size: 24px; font-weight: 700; margin: 0; letter-spacing: -0.02em; }
.md-space-tag { font-size: 13px; color: #86868b; margin-top: 8px; display: block; font-family: monospace; }
.md-close-btn { background: none; border: 1px solid #e5e5e5; padding: 4px 10px; font-size: 10px; color: #c7c7cc; cursor: pointer; transition: all 0.2s; }
.md-close-btn:hover { border-color: #000; color: #000; }
.md-label { font-size: 11px; font-weight: 700; color: #d2d2d7; letter-spacing: 0.1em; margin-bottom: 24px; display: block; }
.md-type-list { list-style: none; padding: 0; margin: 0; }
.md-type-list li { display: flex; align-items: flex-start; gap: 20px; padding: 20px 0; border-bottom: 1px solid #f2f2f2; cursor: pointer; transition: all 0.2s; opacity: 0.4; }
.md-type-list li:hover { opacity: 0.8; }
.md-type-list li.is-active { opacity: 1; }
.md-radio-indicator { width: 12px; height: 12px; border: 1px solid #000; border-radius: 50%; margin-top: 4px; position: relative; flex-shrink: 0; }
.is-active .md-radio-indicator::after { content: ''; position: absolute; inset: 2px; background: #000; border-radius: 50%; }
.md-type-content { display: flex; flex-direction: column; gap: 4px; }
.md-type-title { font-size: 16px; font-weight: 600; }
.md-type-desc { font-size: 13px; color: #86868b; line-height: 1.5; }
.md-extra-form { margin-top: 40px; padding-top: 40px; border-top: 1px dashed #e5e5e5; display: flex; flex-direction: column; gap: 32px; }
.md-form-group { display: flex; flex-direction: column; }
.md-select { width: 100%; padding: 12px 0; font-size: 14px; font-family: inherit; color: #1a1a1a; background: transparent; border: none; border-bottom: 1px solid #e5e5e5; outline: none; cursor: pointer; appearance: none; transition: border-color 0.2s; }
.md-select:focus { border-bottom-color: #000; }
.md-select:disabled { opacity: 0.5; cursor: not-allowed; }
.md-tags-container { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; border-bottom: 1px solid #e5e5e5; padding-bottom: 8px; transition: border-color 0.2s; }
.md-tags-container:focus-within { border-bottom-color: #000; }
.md-tag { display: inline-flex; align-items: center; gap: 6px; background: #f5f5f7; padding: 4px 10px; font-size: 12px; font-weight: 500; color: #1a1a1a; border-radius: 2px; }
.md-tag-remove { background: none; border: none; padding: 0; font-size: 14px; color: #86868b; cursor: pointer; line-height: 1; }
.md-tag-remove:hover { color: #ff3b30; }
.md-input { flex: 1; min-width: 120px; border: none; background: transparent; font-size: 14px; padding: 8px 0; outline: none; color: #1a1a1a; }
.md-input::placeholder { color: #c7c7cc; }
.md-footer { margin-top: 60px; }
.md-footer-line { height: 1px; background: #eee; width: 40px; margin-bottom: 32px; }
.md-actions { display: flex; gap: 32px; align-items: center; }
.md-btn-secondary { background: none; border: none; font-size: 14px; color: #86868b; cursor: pointer; padding: 0; }
.md-btn-secondary:hover { color: #ff3b30; }
.md-btn-primary { background: #000; color: #fff; border: none; padding: 10px 24px; font-size: 14px; font-weight: 600; cursor: pointer; transition: opacity 0.2s; }
.md-btn-primary:hover { opacity: 0.8; }
.md-btn-primary:disabled { background: #d2d2d7; cursor: not-allowed; }
.fade-enter-active, .fade-leave-active { transition: opacity 0.4s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
.expand-enter-active, .expand-leave-active { transition: all 0.4s cubic-bezier(0.16, 1, 0.3, 1); overflow: hidden; max-height: 300px; opacity: 1; }
.expand-enter-from, .expand-leave-to { max-height: 0; opacity: 0; margin-top: 0; padding-top: 0; border-top-color: transparent; }
</style>