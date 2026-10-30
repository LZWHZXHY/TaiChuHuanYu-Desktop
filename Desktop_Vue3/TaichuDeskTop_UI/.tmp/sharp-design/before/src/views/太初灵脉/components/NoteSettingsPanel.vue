<template>
  <transition name="slide">
    <div v-if="modelValue" class="note-settings-drawer workbench-skin editor-surface community-surface tone-library">
      <div class="drawer-header studio-heading">
        <div class="tab-switcher">
          <button :class="{ active: activeTab === 'fragment' }" @click="activeTab = 'fragment'">设置</button>
          <button :class="{ active: activeTab === 'space' }" @click="activeTab = 'space'">位面</button>
        </div>
        <button class="close-icon-btn" aria-label="关闭碎片设置" @click="$emit('update:modelValue', false)">✕</button>
      </div>

      <div class="drawer-content">
        <div v-if="activeTab === 'fragment' && note">
          <section class="settings-section">
            <h4 class="section-title">核心属性</h4>
            <div class="field-group">
              <label>归属位面</label>
              <select :value="note.spaceId" @change="updateNoteField('spaceId', $event)">
                <option v-for="s in spaces" :key="s.id" :value="s.id">{{ s.name }}</option>
              </select>
            </div>
            <div class="field-group">
              <label>碎片形态</label>
              <div class="type-chips">
                <button 
                  v-for="t in availableTypes" :key="t.value"
                  :class="{ active: note.type === t.value }"
                  @click="updateNoteField('type', t.value)"
                >{{ t.label }}</button>
              </div>
              <p class="type-dest">👉 {{ destinationText }}</p>
            </div>
          </section>

          <section class="settings-section">
            <h4 class="section-title">灵脉交互</h4>
            <div class="toggle-row">
              <span>侧边栏索引</span>
              <label class="spirit-switch">
                <input type="checkbox" :checked="note.showInSidebar" @change="updateNoteField('showInSidebar', ($event.target as HTMLInputElement).checked)" />
                <span class="slider"></span>
              </label>
            </div>
            <div class="toggle-row">
              <span class="row-label">
                发布状态
                <button class="help-tip" type="button" aria-label="发布去向说明">
                  ?
                  <span class="help-bubble">{{ destinationText }}</span>
                </button>
              </span>
              <label class="spirit-switch">
                <input type="checkbox" :checked="note.isPublic" disabled />
                <span class="slider"></span>
              </label>
            </div>
          </section>

          <section class="settings-section actions">
             <button class="action-link" @click="$emit('open-history')">🕒 版本历史</button>
             <button class="action-link danger" @click="$emit('delete', note.id)">🗑️ 焚毁碎片</button>
          </section>
        </div>

        <div v-if="activeTab === 'space' && currentSpace">
          <section class="settings-section">
            <h4 class="section-title">位面配置</h4>
            <input class="spirit-input" :value="currentSpace.name" @change="updateSpaceField('name', ($event.target as HTMLInputElement).value)" placeholder="位面名称" />
          </section>
          <section class="settings-section">
            <h4 class="section-title">维度过滤</h4>
            <div class="filter-matrix">
              <div v-for="t in availableTypes" :key="t.value" class="matrix-item">
                <span class="matrix-label">{{ t.label }}</span>
                <label class="spirit-switch mini">
                  <input type="checkbox" v-model="displayFilters[t.value]" />
                  <span class="slider"></span>
                </label>
              </div>
            </div>
          </section>
        </div>
      </div>

      <div class="drawer-footer">
        <button 
          class="publish-btn" 
          :class="{ 'is-active': note?.isPublic }"
          :disabled="publishing || (!note?.isPublic && !canPublish)"
          @click="$emit('publish-click')"
        >{{ note?.type === 'doc' ? '分享至项目' : note?.isPublic ? '取消发布' : '发布至广场' }}</button>
        <button v-if="note?.type === 'doc'" class="publish-btn" :disabled="publishing" @click="$emit('unshare')">取消所有项目分享</button>
        <button v-if="note?.type !== 'doc' && note?.isPublic && canPublish" class="publish-btn" :disabled="publishing" @click="$emit('refresh-publish')">更新发布</button>
        <button class="save-btn" @click="$emit('save')">同步至灵脉</button>
      </div>
    </div>
  </transition>
</template>

<script setup lang="ts">
import { ref, reactive, watch, computed } from 'vue';

const props = defineProps<{
  modelValue: boolean;
  note: any;
  spaces: any[];
  currentSpaceId: string;
  filters: any; 
  publishing?: boolean;
  canPublish: boolean; // 接收从 index.vue 传来的多态流字数与类型校验结果
}>();

const emit = defineEmits(['update:modelValue', 'update-note-meta', 'update-space-meta', 'update-filters', 'delete', 'open-history', 'publish-click', 'unshare', 'refresh-publish', 'save']);

const activeTab = ref<'fragment' | 'space'>('fragment');

// 🌟【形态数据对齐】：移除过时的 thought，无缝补齐随笔(blog)、白板(canvas)、地图(map)、表格(excel)
const availableTypes = [
  { value: 'note', label: '笔记 (Note)' },
  { value: 'doc', label: '项目文档 (Doc)' },
  { value: 'blog', label: '随笔 (Blog)' },
  { value: 'schedule', label: '计划表 (schedule)'},
  { value: 'post', label: '简语 (Post)' }, 
  { value: 'wiki', label: '词条 (Wiki)' }, 
  { value: 'art', label: '艺术 (Art)' },
  { value: 'canvas', label: '星图白板 (Canvas)' },
  { value: 'map', label: '世界地图 (Map)' },
  { value: 'excel', label: '表格 (Excel)' }
];

const currentSpace = ref<any>(null);

watch(() => props.currentSpaceId, (id) => {
  currentSpace.value = props.spaces.find(s => s.id === id) || null;
}, { immediate: true });

watch(() => props.spaces, () => {
  currentSpace.value = props.spaces.find(s => s.id === props.currentSpaceId) || null;
}, { deep: true });

// 响应式过滤器
const displayFilters = reactive({ ...props.filters });

// 🌟【体验优化】：当外部过滤器状态变化时，同步向设置面板内倒灌状态
watch(() => props.filters, (newFilters) => {
  if (newFilters) {
    Object.assign(displayFilters, newFilters);
  }
}, { deep: true });

watch(displayFilters, (newVal) => emit('update-filters', { ...newVal }), { deep: true });

const updateNoteField = (field: string, val: any) => emit('update-note-meta', { [field]: val?.target ? val.target.value : val });
const updateSpaceField = (field: string, val: any) => emit('update-space-meta', { id: props.currentSpaceId, [field]: val });

// 🌟【根绝报错】：删除了原先同名的 const canPublish 计算属性，确保 props.canPublish 正常生效

// 碎片形态 → 发布后出现的板块
const typeLabels: Record<string, string> = {
  note: '笔记', doc: '项目文档', blog: '随笔', schedule: '计划表', post: '简语',
  wiki: '词条', art: '艺术', canvas: '星图白板', map: '世界地图', excel: '表格'
};
const destinationMap: Record<string, string> = {
  blog: '太初随笔（博客）', post: '太初随笔（帖子）', wiki: '灵脉百科', art: '画廊展厅',
  doc: '太初协作（项目文档）', char: '柴圈板块（角色档案）'
};
const destinationText = computed(() => {
  const t = props.note?.type as string;
  if (!t) return '发布后会出现在对应的社区板块';
  const dest = destinationMap[t];
  return dest ? `「${typeLabels[t] || t}」发布后 → ${dest}` : `「${typeLabels[t] || t}」不对外发布，仅存于灵脉内部`;
});
</script>

<style scoped>
.note-settings-drawer {
  position: fixed; top: 0; right: 0; bottom: 0; width: min(400px, 100vw);
  background: var(--paper-card); z-index: 3000; padding: 24px;
  box-shadow: -10px 0 40px rgba(0,0,0,0.05); display: flex; flex-direction: column;
}
.drawer-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 40px; }
.tab-switcher { display: flex; gap: 20px; }
.tab-switcher button { border: none; background: none; font-size: 14px; color: var(--text-secondary); cursor: pointer; padding: 0; transition: color 0.2s; }
.tab-switcher button.active { color: var(--text-primary); font-weight: 700; }
.drawer-content { flex: 1; overflow-y: auto; padding-right: 4px; }
.settings-section { margin-bottom: 32px; }
.section-title { font-size: 11px; text-transform: uppercase; letter-spacing: 0.1em; color: var(--text-secondary); margin-bottom: 20px; }
.field-group { margin-bottom: 24px; }
.field-group label { font-size: 12px; color: var(--text-secondary); margin-bottom: 8px; display: block; }
select, .spirit-input { width: 100%; border: 1px solid var(--border-line); padding: 8px 12px; border-radius: var(--radius-sm); outline: none; transition: 0.2s; font-size: 13px; }
select:focus, .spirit-input:focus { border-color: var(--accent-color); }
.type-chips { display: flex; flex-wrap: wrap; gap: 8px; }
.type-chips button { border: 1px solid var(--border-line); background: var(--paper-card); padding: 6px 12px; border-radius: var(--radius-sm); cursor: pointer; font-size: 12px; transition: 0.2s; color: var(--text-secondary); }
.type-chips button.active { border-color: var(--border-line); color: var(--text-primary); background: var(--bg-sub); font-weight: 600; }
.type-dest { margin: 10px 0 0; font-size: 12px; color: var(--accent-color); line-height: 1.6; }
.toggle-row { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; font-size: 13px; color: var(--text-primary); }

/* 过滤矩阵排版增强 */
.filter-matrix { display: flex; flex-direction: column; gap: 12px; }
.matrix-item { display: flex; justify-content: space-between; align-items: center; font-size: 13px; }
.matrix-label { color: var(--text-primary); }

.action-link { display: block; background: none; border: none; padding: 10px 0; font-size: 13px; color: var(--text-secondary); cursor: pointer; width: 100%; text-align: left; }
.action-link.danger { color: var(--danger); }
.drawer-footer { margin-top: auto; display: flex; gap: 12px; padding-top: 20px; border-top: 1px solid var(--border-line); }
.save-btn { flex: 1; padding: 10px; background: var(--text-primary); color: var(--paper-card); border: none; border-radius: var(--radius-sm); font-weight: 600; cursor: pointer; font-size: 13px; }
.publish-btn { flex: 1; padding: 10px; background: var(--bg-sub); border: none; border-radius: var(--radius-sm); font-weight: 600; cursor: pointer; font-size: 13px; color: var(--text-secondary); transition: all 0.2s; }
.publish-btn:disabled { opacity: 0.4; cursor: not-allowed; }
.publish-btn.is-active { background: var(--success); color: var(--on-accent); }

.spirit-switch { position: relative; display: inline-block; width: 32px; height: 18px; flex-shrink: 0; }
.spirit-switch input { opacity: 0; width: 0; height: 0; }
.slider { position: absolute; cursor: pointer; inset: 0; background-color: var(--bg-sub); transition: .3s; border-radius: var(--radius-sm); }
.slider:before { position: absolute; content: ""; height: 14px; width: 14px; left: 2px; bottom: 2px; background-color: var(--paper-card); transition: .3s; border-radius: 50%; }
input:checked + .slider { background-color: var(--accent-color); }
input:checked + .slider:before { transform: translateX(14px); }
.close-icon-btn { background: none; border: none; font-size: 16px; cursor: pointer; color: var(--text-secondary); }
.slide-enter-active, .slide-leave-active { transition: transform 0.4s cubic-bezier(0.16, 1, 0.3, 1); }
.slide-enter-from, .slide-leave-to { transform: translateX(100%); }

/* 发布去向「?」提示 */
.row-label { display: inline-flex; align-items: center; gap: 6px; }
.help-tip { position: relative; width: 16px; height: 16px; border-radius: 50%; border: 1px solid var(--border-line); background: var(--bg-sub); color: var(--text-secondary); font-size: 11px; line-height: 1; cursor: help; display: inline-flex; align-items: center; justify-content: center; padding: 0; }
.help-bubble { position: absolute; right: -6px; bottom: 135%; width: 220px; background: rgba(20, 20, 24, 0.92); color: #fff; font-size: 12px; line-height: 1.6; padding: 10px 12px; border-radius: var(--radius-sm); opacity: 0; visibility: hidden; pointer-events: none; transition: opacity 0.15s, visibility 0.15s; z-index: 20; text-align: left; white-space: normal; }
.help-tip:hover .help-bubble, .help-tip:focus .help-bubble { opacity: 1; visibility: visible; }
</style>
