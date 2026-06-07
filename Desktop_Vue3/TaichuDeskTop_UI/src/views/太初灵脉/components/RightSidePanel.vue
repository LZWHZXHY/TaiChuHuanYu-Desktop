<template>
  <aside class="spirit-right-panel">
    <section class="panel-section relation-block">
      <div class="section-header">
        <span class="header-title">🔗 关系引用</span>
        <div class="relation-badge-tabs">
          <span 
            :class="['badge-tab', { active: subTab === 'back' }]" 
            @click="subTab = 'back'"
          >
            反链 ({{ backlinks.length }})
          </span>
          <span 
            :class="['badge-tab', { active: subTab === 'out' }]" 
            @click="subTab = 'out'"
          >
            正链 ({{ outlinks.length }})
          </span>
        </div>
      </div>

      <div class="section-content is-scrollable">
        <div v-if="isLinksLoading" class="panel-mini-loading">感应星轨中...</div>
        <div v-else class="links-list">
          <div 
            v-for="(link, i) in (subTab === 'back' ? backlinks : outlinks) as any[]" 
            :key="link.id || i" 
            class="link-card"
            @click="$emit('select', link.id)"
          >
            <div class="link-title">{{ link.title || '无标题碎片' }}</div>
            <div v-if="link.excerpt" class="link-excerpt">{{ link.excerpt }}</div>
          </div>
          <div v-if="(subTab === 'back' ? backlinks : outlinks).length === 0" class="empty-placeholder">
            暂无引用关联
          </div>
        </div>
      </div>
    </section>

    <section class="panel-section properties-block">
      <div class="section-header">
        <span class="header-title">📋 元数据属性</span>
        <button class="ghost-add-btn" @click="addProperty">+ 新增</button>
      </div>

      <div class="section-content">
        <div class="prop-list">
          <div v-for="(prop, index) in localProperties" :key="prop.id" class="prop-row">
            <input 
              v-model="prop.key" 
              class="prop-input key-input" 
              placeholder="属性名" 
              @change="handlePropChange"
            />
            <span class="colon">:</span>
            <input 
              v-model="prop.value" 
              class="prop-input val-input" 
              placeholder="未指定" 
              @change="handlePropChange"
            />
            <button class="del-prop-btn" @click="removeProperty(index)">✕</button>
          </div>
          <div v-if="localProperties.length === 0" class="empty-placeholder">
            暂无自定义属性
          </div>
        </div>
      </div>
    </section>

    <section class="panel-section tags-block">
      <div class="section-header">
        <span class="header-title">🏷️ 灵脉标签</span>
      </div>

      <div class="section-content">
        <div class="tags-wrapper">
          <span v-for="(tag, index) in localTags" :key="index" class="spirit-tag">
            # {{ tag }}
            <span class="tag-remove" @click="removeTag(index)">×</span>
          </span>
          
          <input 
            v-model="newTagInput"
            class="tag-inline-input"
            placeholder="+ 输入标签按下回车..."
            @keyup.enter="addTag"
          />
        </div>
      </div>
    </section>
  </aside>
</template>

<script setup lang="ts">
import { ref, watchEffect, watch } from 'vue';
import { lingmaiApi } from '@/api/lingmai';

const props = defineProps({
  noteId: { type: String, required: true },
  extraData: { type: String, default: '[]' }
});

// 🌟【核心修复】暗号必须完美匹配父组件的 v-model:extraData 以及 @change
const emit = defineEmits(['update:extraData', 'change', 'select']);

const subTab = ref('back');
const isLinksLoading = ref(false);
const backlinks = ref<any[]>([]);
const outlinks = ref<any[]>([]);

const localProperties = ref<any[]>([]);
const localTags = ref<string[]>([]);
const newTagInput = ref('');

// 1. 深度解析外层传入的 extraData
watchEffect(() => {
  try {
    const parsed = JSON.parse(props.extraData || '[]');
    if (Array.isArray(parsed)) {
      localProperties.value = parsed.filter(p => p && typeof p === 'object' && 'key' in p);
    }
  } catch (e) {
    localProperties.value = [];
  }
});

// 2. 自动化感知双向关系链
const loadLinkRelations = async () => {
  if (!props.noteId) return;
  isLinksLoading.value = true;
  try {
    const res = await lingmaiApi.getBacklinks(props.noteId) as any;
    backlinks.value = res?.backlinks || [];
    outlinks.value = res?.outlinks || [];
  } catch (e) {
    console.error("感应双链失败:", e);
  } finally {
    isLinksLoading.value = false;
  }
};

watch(() => props.noteId, () => {
  loadLinkRelations();
  localTags.value = ['灵脉内核', '百科词条']; 
}, { immediate: true });

// 3. 🌟【核心修复】将属性改动转化为规范的 JSON 字符串逆向贯穿给 v-model
const handlePropChange = () => {
  const validProps = localProperties.value.filter(p => p.key.trim() || p.value.trim());
  const jsonString = JSON.stringify(validProps);
  
  // 第一步：触发 v-model 机制，让父组件的 activeNote.extraData 瞬间被复写为最新数据字符串
  emit('update:extraData', jsonString);
  
  // 第二步：触发 @change 机制，立刻通知父组件执行 triggerDebouncedSync() 进入防抖保存
  emit('change');
};

const addProperty = () => {
  localProperties.value.push({
    id: Math.random().toString(36).substring(2, 9),
    key: '',
    value: ''
  });
  handlePropChange();
};

const removeProperty = (index: number) => {
  localProperties.value.splice(index, 1);
  handlePropChange();
};

// 4. 标签驱动逻辑保持原样
const addTag = () => {
  const cleanTag = newTagInput.value.trim().replace('#', '');
  if (cleanTag && !localTags.value.includes(cleanTag)) {
    localTags.value.push(cleanTag);
  }
  newTagInput.value = '';
};

const removeTag = (index: number) => {
  localTags.value.splice(index, 1);
};
</script>

<style scoped>
/* 保持你的精美垂直分层 CSS 样式不变 */
.spirit-right-panel { width: 310px; border-left: 1px solid #f2f2f7; background: #ffffff; display: flex; flex-direction: column; height: 100%; box-sizing: border-box; }
.panel-section { display: flex; flex-direction: column; border-bottom: 1px solid #f2f2f7; padding: 16px; box-sizing: border-box; }
.relation-block { flex: 1; min-height: 200px; overflow: hidden; }
.properties-block { max-height: 350px; overflow-y: auto; }
.tags-block { border-bottom: none; background: #fafafa; }
.section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; }
.header-title { font-size: 12px; font-weight: 700; color: #1d1d1f; letter-spacing: 0.05em; }
.relation-badge-tabs { display: flex; background: #f2f2f7; padding: 2px; border-radius: 6px; }
.badge-tab { font-size: 11px; padding: 3px 8px; border-radius: 4px; color: #86868b; cursor: pointer; transition: all 0.2s; }
.badge-tab.active { background: #ffffff; color: #0066cc; font-weight: 600; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
.section-content { width: 100%; }
.section-content.is-scrollable { flex: 1; overflow-y: auto; scrollbar-width: none; }
.section-content.is-scrollable::-webkit-scrollbar { display: none; }
.prop-list { display: flex; flex-direction: column; gap: 8px; }
.prop-row { display: flex; align-items: center; position: relative; gap: 4px; }
.prop-input { border: 1px solid transparent; background: transparent; padding: 4px 6px; font-size: 12px; width: 42%; border-radius: 4px; }
.prop-input:hover { background: #f5f5f7; }
.prop-input:focus { background: #ffffff; border-color: #0066cc; box-shadow: 0 0 0 2px rgba(0,102,204,0.08); }
.key-input { text-align: right; color: #86868b; font-weight: 500; }
.val-input { color: #1d1d1f; }
.colon { color: #d2d2d7; font-size: 12px; }
.ghost-add-btn { background: none; border: none; color: #0066cc; font-size: 11px; font-weight: 600; cursor: pointer; padding: 2px 6px; border-radius: 4px; }
.ghost-add-btn:hover { background: rgba(0,102,204,0.05); }
.del-prop-btn { background: none; border: none; color: #c7c7cc; cursor: pointer; opacity: 0; transition: opacity 0.2s; font-size: 12px; }
.prop-row:hover .del-prop-btn { opacity: 1; }
.del-prop-btn:hover { color: #ff3b30; }
.link-card { padding: 10px; background: #f9f9fb; border-radius: 8px; margin-bottom: 8px; cursor: pointer; border: 1px solid transparent; transition: all 0.2s; }
.link-card:hover { border-color: #d2d2d7; background: #ffffff; }
.link-title { font-size: 12px; font-weight: 600; color: #1d1d1f; }
.link-excerpt { font-size: 11px; color: #86868b; margin-top: 2px; display: -webkit-box; -webkit-line-clamp: 1; -webkit-box-orient: vertical; overflow: hidden; }
.tags-wrapper { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; }
.spirit-tag { font-size: 11px; background: rgba(0, 102, 204, 0.06); color: #0066cc; padding: 4px 8px; border-radius: 6px; display: inline-flex; align-items: center; gap: 4px; font-weight: 500; }
.tag-remove { cursor: pointer; color: #a1a1a6; font-weight: bold; transition: color 0.2s; }
.tag-remove:hover { color: #ff3b30; }
.tag-inline-input { border: none; background: transparent; outline: none; font-size: 11px; color: #86868b; padding: 4px; flex: 1; min-width: 100px; }
.empty-placeholder, .panel-mini-loading { font-size: 11px; color: #c7c7cc; text-align: center; padding: 16px 0; }
</style>