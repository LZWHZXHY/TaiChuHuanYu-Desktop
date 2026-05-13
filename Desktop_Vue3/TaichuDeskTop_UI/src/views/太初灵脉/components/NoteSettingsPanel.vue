<template>
  <transition name="slide">
    <div v-if="modelValue" class="note-settings-drawer">
      <div class="drawer-header">
        <div class="tab-switcher">
          <button 
            :class="{ active: activeTab === 'fragment' }" 
            @click="activeTab = 'fragment'"
          >碎片设定</button>
          <button 
            :class="{ active: activeTab === 'space' }" 
            @click="activeTab = 'space'"
          >位面感应</button>
        </div>
        <button class="close-icon-btn" @click="$emit('update:modelValue', false)">✕</button>
      </div>

      <div class="drawer-content">
        <div v-if="activeTab === 'fragment' && note">
          <section class="settings-section">
            <label>碎片归属位面</label>
            <div class="select-wrapper">
              <select :value="note.spaceId" @change="updateNoteField('spaceId', $event)">
                <option v-for="s in spaces" :key="s.id" :value="s.id">{{ s.name }}</option>
              </select>
            </div>
          </section>

          <section class="settings-section">
            <label>碎片本质 (Type)</label>
            <div class="type-grid">
              <button 
                v-for="t in availableTypes" 
                :key="t.value"
                :class="{ active: note.type === t.value }"
                @click="updateNoteField('type', t.value)"
              >
                {{ t.label }}
              </button>
            </div>
          </section>

          <section class="settings-section">
            <label>显示与感应控制</label>
            <div class="setting-item">
              <div class="item-info">
                <span class="title">在侧边栏常驻</span>
                <span class="desc">取消后仅能通过全屏图谱感应</span>
              </div>
              <label class="spirit-switch">
                <input 
                  type="checkbox" 
                  :checked="note.showInSidebar" 
                  @change="updateNoteField('showInSidebar', ($event.target as HTMLInputElement).checked)"
                />
                <span class="slider"></span>
              </label>
            </div>

            <div class="setting-item">
              <div class="item-info">
                <span class="title">允许被反向引用</span>
                <span class="desc">开启后其他笔记可感应此碎片</span>
              </div>
              <label class="spirit-switch">
                <input 
                  type="checkbox" 
                  :checked="!note.isPrivate" 
                  @change="updateNoteField('isPrivate', !($event.target as HTMLInputElement).checked)"
                />
                <span class="slider"></span>
              </label>
            </div>
          </section>

          <div class="danger-zone">
            <button class="delete-link-btn" @click="$emit('delete', note.id)">彻底从灵脉中焚毁</button>
          </div>
        </div>

        <div v-if="activeTab === 'space' && currentSpace">
          <section class="settings-section">
            <label>位面真名</label>
            <input 
              class="spirit-input" 
              :value="currentSpace.name" 
              @change="updateSpaceField('name', ($event.target as HTMLInputElement).value)"
            />
          </section>

          <section class="settings-section">
            <label>全域显示过滤 (维度切换)</label>
            <p class="section-desc">勾选你想在侧边栏感应到的内容维度：</p>
            
            <div class="filter-matrix">
              <div v-for="t in availableTypes" :key="t.value" class="matrix-item">
                <span class="matrix-label">{{ t.label }}</span>
                <label class="spirit-switch mini">
                  <input 
                    type="checkbox" 
                    v-model="displayFilters[t.value]"
                  />
                  <span class="slider"></span>
                </label>
              </div>
            </div>
          </section>

          <section class="settings-section">
            <label>位面边界控制</label>
            <div class="setting-item">
              <div class="item-info">
                <span class="title">公开位面</span>
                <span class="desc">设为私密后，该位面所有发布内容将从广场隐匿</span>
              </div>
              <label class="spirit-switch">
                <input 
                  type="checkbox" 
                  :checked="currentSpace.isPublic" 
                  @change="updateSpaceField('isPublic', ($event.target as HTMLInputElement).checked)"
                />
                <span class="slider"></span>
              </label>
            </div>
          </section>
        </div>
      </div>
    </div>
  </transition>
</template>

<script setup lang="ts">
import { ref, reactive, watch } from 'vue';

const props = defineProps<{
  modelValue: boolean;
  note: any;
  spaces: any[];
  currentSpaceId: string;
  // 接收外部的显示过滤器状态
  filters: any; 
}>();

const emit = defineEmits(['update:modelValue', 'update-note-meta', 'update-space-meta', 'update-filters', 'delete']);

const activeTab = ref<'fragment' | 'space'>('fragment');
const availableTypes = [
  { value: 'wiki', label: '世界观' },
  { value: 'char', label: '角色' },
  { value: 'art', label: '艺术' },
  { value: 'note', label: '随笔' },
  { value: 'thought', label: '简语' }
];

// 本地化的过滤器状态，用于双向绑定
const displayFilters = reactive({ ...props.filters });

// 监听本地过滤器变化并同步到全局
watch(displayFilters, (newVal) => {
  emit('update-filters', { ...newVal });
}, { deep: true });

const currentSpace = ref<any>(null);

// 实时获取当前位面的完整数据
watch(() => props.currentSpaceId, (id) => {
  currentSpace.value = props.spaces.find(s => s.id === id) || null;
}, { immediate: true });

const updateNoteField = (field: string, eventOrValue: any) => {
  const value = eventOrValue?.target ? eventOrValue.target.value : eventOrValue;
  emit('update-note-meta', { [field]: value });
};

const updateSpaceField = (field: string, value: any) => {
  emit('update-space-meta', { id: props.currentSpaceId, [field]: value });
};
</script>

<style scoped>
.note-settings-drawer {
  position: fixed; top: 0; right: 0; bottom: 0; width: 340px;
  background: rgba(255, 255, 255, 0.95); backdrop-filter: blur(25px);
  border-left: 1px solid #f2f2f2; z-index: 3000; padding: 24px;
  box-shadow: -20px 0 50px rgba(0,0,0,0.02); display: flex; flex-direction: column;
}

/* Tab 切换器样式 */
.drawer-header { margin-bottom: 30px; display: flex; justify-content: space-between; align-items: flex-start; }
.tab-switcher { 
  display: flex; background: #f5f5f7; padding: 3px; border-radius: 10px; gap: 2px;
}
.tab-switcher button {
  border: none; background: none; padding: 6px 12px; font-size: 13px; font-weight: 500;
  border-radius: 8px; cursor: pointer; color: #86868b; transition: all 0.2s;
}
.tab-switcher button.active { background: #fff; color: #1d1d1f; box-shadow: 0 2px 8px rgba(0,0,0,0.05); }

.drawer-content { flex: 1; overflow-y: auto; padding-right: 4px; }
.settings-section { margin-bottom: 32px; }
.settings-section label { display: block; font-size: 12px; color: #86868b; font-weight: 600; margin-bottom: 12px; }
.section-desc { font-size: 12px; color: #a1a1a6; margin-bottom: 16px; }

/* 类型网格 */
.type-grid { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 8px; }
.type-grid button {
  padding: 8px; border: 1px solid #d2d2d7; background: #fff; border-radius: 8px;
  font-size: 12px; cursor: pointer; transition: all 0.2s;
}
.type-grid button.active { border-color: #0066cc; color: #0066cc; background: rgba(0,102,204,0.05); }

/* 过滤矩阵 */
.filter-matrix { background: #f9f9fb; padding: 16px; border-radius: 12px; }
.matrix-item { 
  display: flex; justify-content: space-between; align-items: center; padding: 8px 0;
  border-bottom: 1px solid #f2f2f2;
}
.matrix-item:last-child { border-bottom: none; }
.matrix-label { font-size: 13px; color: #1d1d1f; }

.spirit-input {
  width: 100%; height: 40px; border: 1px solid #d2d2d7; border-radius: 10px;
  padding: 0 12px; outline: none; transition: border-color 0.2s;
}
.spirit-input:focus { border-color: #0066cc; }

.select-wrapper select {
  width: 100%; height: 40px; border-radius: 10px; border: 1px solid #d2d2d7;
  padding: 0 12px; outline: none; background: #fff; font-size: 14px;
}

.setting-item { display: flex; justify-content: space-between; align-items: center; margin-top: 16px; }
.item-info .title { display: block; font-size: 14px; color: #1d1d1f; font-weight: 500; }
.item-info .desc { display: block; font-size: 11px; color: #86868b; margin-top: 2px; line-height: 1.4; }

/* 开关样式 */
.spirit-switch { position: relative; display: inline-block; width: 36px; height: 20px; flex-shrink: 0; }
.spirit-switch.mini { width: 32px; height: 18px; }
.spirit-switch input { opacity: 0; width: 0; height: 0; }
.slider {
  position: absolute; cursor: pointer; inset: 0; background-color: #d2d2d7;
  transition: .3s; border-radius: 20px;
}
.slider:before {
  position: absolute; content: ""; height: 16px; width: 16px;
  left: 2px; bottom: 2px; background-color: white; transition: .3s; border-radius: 50%;
}
.spirit-switch.mini .slider:before { height: 14px; width: 14px; }
input:checked + .slider { background-color: #0066cc; }
input:checked + .slider:before { transform: translateX(16px); }
.spirit-switch.mini input:checked + .slider:before { transform: translateX(14px); }

.danger-zone { margin-top: 40px; padding-top: 20px; border-top: 1px solid #f2f2f2; }
.delete-link-btn { color: #ff3b30; background: none; border: none; font-size: 13px; font-weight: 500; cursor: pointer; }
.close-icon-btn { background: #f5f5f7; border: none; width: 28px; height: 28px; border-radius: 50%; cursor: pointer; }

.slide-enter-active, .slide-leave-active { transition: transform 0.4s cubic-bezier(0.16, 1, 0.3, 1); }
.slide-enter-from, .slide-leave-to { transform: translateX(100%); }
</style>