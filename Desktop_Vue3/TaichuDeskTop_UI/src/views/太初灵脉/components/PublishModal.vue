<template>
  <transition name="fade">
    <div v-if="modelValue" class="md-overlay" @click.self="$emit('update:modelValue', false)">
      <div class="md-container">
        <header class="md-header">
          <div class="md-title">
            <span class="md-label">发布碎片</span>
            <h1>{{ publishTitle }}</h1>
          </div>
          <button class="md-close" @click="$emit('update:modelValue', false)">✕</button>
        </header>

        <div class="md-body">
          <div class="simple-confirm-text">
            即将将此 <strong>{{ displayType }}</strong> 固化至灵脉广场。
          </div>

          <transition name="fade">
            <div v-if="noteType === 'wiki'" class="wiki-extra-zone">
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
        </div>

        <footer class="md-footer">
          <button class="btn-cancel" @click="$emit('update:modelValue', false)">放弃</button>
          <button 
            class="btn-primary" 
            :disabled="isProcessing || !canSubmit"
            @click="handleConfirm"
          >
            {{ isProcessing ? '正在锚定...' : '确认发布' }}
          </button>
        </footer>
      </div>
    </div>
  </transition>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue';
import { wikiApi, type IWikiCategory } from '@/api/Wiki';
import { lingmaiApi } from '../../../api/lingmai';

const props = defineProps<{ 
  modelValue: boolean; 
  noteId: string; 
  noteType: string; // 🌟 接收当前笔记的真实类型
  spaceName: string; 
}>();

const emit = defineEmits(['update:modelValue', 'success']);

const isProcessing = ref(false);
const wikiData = reactive({ categoryId: 3, tags: [] as string[] }); // 🌟 默认值 3 对齐你的分类 ID
const tagInput = ref('');
const rawCategories = ref<IWikiCategory[]>([]);
const isLoadingCategories = ref(false);

const displayType = computed(() => {
  const map: Record<string, string> = { 
    'wiki': '百科词条', 'char': '角色档案', 
    'post': '太初随笔', 'blog': '博客', 'art': '艺术画廊', 'thought': '简语' 
  };
  return map[props.noteType] || '灵脉碎片';
});

const publishTitle = computed(() => {
  if (props.noteType === 'wiki') return '发布至百科宇宙';
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

const canSubmit = computed(() => props.noteType !== 'wiki' || !!wikiData.categoryId);

const addTag = () => {
  const t = tagInput.value.trim();
  if (t && !wikiData.tags.includes(t)) wikiData.tags.push(t);
  tagInput.value = '';
};

const removeTag = (i: number) => wikiData.tags.splice(i, 1);

// 🌟 统一发布逻辑：不分流，直接传给后端
const handleConfirm = async () => {

  console.log("即将发送的发布形态:", props.noteType);

  isProcessing.value = true;
  try {
    await lingmaiApi.publishNote(props.noteId, {
      type: props.noteType,
      categoryId: wikiData.categoryId,
      tags: wikiData.tags
    });

    emit('success');
    emit('update:modelValue', false);
  } catch (err) { 
    console.error(err);
    alert('发布系统感应到干扰'); 
  } finally {
    isProcessing.value = false;
  }
};

onMounted(async () => {
  console.log("【调试】发布弹窗已挂载，开始拉取界域...");
  isLoadingCategories.value = true;
  try {
    const data = await wikiApi.getCategories();
    console.log("【调试】界域API返回结果:", data);
    rawCategories.value = data || [];
  } catch (error) {
    console.error("【调试】界域读取崩溃:", error);
  } finally {
    isLoadingCategories.value = false;
  }
});
</script>

<style scoped>
/* 极简视觉引导风格 */
.md-overlay { position: fixed; inset: 0; background: rgba(255,255,255,0.8); backdrop-filter: blur(8px); display: flex; align-items: center; justify-content: center; z-index: 5000; }
.md-container { width: 440px; background: #fff; padding: 32px; border-radius: 20px; box-shadow: 0 20px 40px rgba(0,0,0,0.08); }
.md-header { margin-bottom: 32px; display: flex; justify-content: space-between; }
.md-label { font-size: 10px; color: #86868b; text-transform: uppercase; letter-spacing: 0.1em; }
.md-title h1 { font-size: 20px; font-weight: 700; margin: 4px 0 0; }
.simple-confirm-text { padding: 20px; color: #1d1d1f; font-size: 14px; text-align: center; border: 1px solid #f2f2f7; border-radius: 12px; }
.wiki-extra-zone { padding-top: 10px; }
.form-group { margin-bottom: 16px; }
.form-group label { display: block; font-size: 11px; color: #86868b; margin-bottom: 8px; }
select { width: 100%; border: 1px solid #f2f2f7; padding: 8px; border-radius: 6px; font-size: 13px; outline: none; }
.tag-input-box { border: 1px solid #f2f2f7; border-radius: 6px; padding: 4px; display: flex; flex-wrap: wrap; gap: 4px; }
.tag-pill { background: #f2f2f7; padding: 2px 8px; border-radius: 4px; font-size: 11px; display: flex; align-items: center; gap: 4px; }
.tag-pill button { border: none; background: none; cursor: pointer; }
.tag-input-box input { border: none; padding: 4px; font-size: 12px; outline: none; flex: 1; }
.md-footer { margin-top: 32px; display: flex; gap: 12px; }
.btn-cancel { flex: 1; background: none; border: none; color: #86868b; font-size: 13px; cursor: pointer; }
.btn-primary { flex: 2; padding: 10px; background: #000; color: #fff; border: none; border-radius: 8px; font-weight: 600; cursor: pointer; }
.md-close { background: none; border: none; color: #c7c7cc; cursor: pointer; }
</style>