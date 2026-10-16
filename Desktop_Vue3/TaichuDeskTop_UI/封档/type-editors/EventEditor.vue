<!-- src/views/世界观管理/type-editors/EventEditor.vue -->
<template>
  <div class="event-editor">
    <div class="editor-grid">
      <div class="left-column">
        <div class="field-group">
          <label>开始时间</label>
          <input v-model="localData.startDate" type="date" />
        </div>

        <div class="field-group">
          <label>结束时间</label>
          <input v-model="localData.endDate" type="date" />
        </div>

        <div class="field-group">
          <label>发生地点</label>
          <el-select
            v-model="localData.locationId"
            filterable
            placeholder="选择地点"
            clearable
          >
            <el-option
              v-for="loc in locationOptions"
              :key="loc.id"
              :label="loc.title"
              :value="loc.id"
            />
          </el-select>
        </div>
      </div>

      <div class="right-column">
        <div class="field-group">
          <label>结果</label>
          <input v-model="localData.outcome" placeholder="事件结果，如：联盟胜利" />
        </div>

        <div class="field-group">
          <label>历史意义</label>
          <textarea v-model="localData.significance" rows="4" placeholder="描述事件的历史意义..." class="significance-textarea" />
        </div>
      </div>
    </div>

    <!-- 🔥 提示用户使用内容块插入关联卡片 -->
    <div class="editor-hint">
      <p>💡 提示：在「关联内容」区域点击「+角色」或「+派系」按钮，可以插入「参与方」卡片</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import type { EventData } from '../card_type'
import { useWorldStore } from '@/stores/world'

const props = defineProps<{
  modelValue?: EventData
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: EventData): void
}>()

const store = useWorldStore()

// ❌ 删除了 participants 字段
const defaultData: EventData = {
  id: '',
  projectId: '',
  type: 'event',
  title: '',
  coverImage: '',
  attributes: [],
  description: '',
  tags: [],
  relations: [],
  contentBlocks: [],
  createdAt: '',
  updatedAt: '',
  startDate: '',
  endDate: '',
  locationId: '',
  outcome: '',
  significance: '',
}

// ===== 本地数据 =====
const localData = ref<EventData>({
  ...defaultData,
  ...(props.modelValue || {}),
})

// ===== 地点选项 =====
const locationOptions = computed(() =>
  store.cards.filter(c => c.type === 'location').map(c => ({ id: c.id, title: c.title }))
)

// ===== 双向绑定 =====
watch(localData, (val) => {
  emit('update:modelValue', val)
}, { deep: true })
</script>

<style scoped>
.event-editor {
  padding: 16px 0;
  border-top: 1px solid #eef2f6;
  border-bottom: 1px solid #eef2f6;
}

.editor-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 32px;
}

.field-group {
  margin-bottom: 16px;
}
.field-group label {
  display: block;
  font-size: 13px;
  font-weight: 500;
  color: #334155;
  margin-bottom: 4px;
}
.field-group input,
.field-group .el-select {
  width: 100%;
}
.field-group input {
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  padding: 6px 10px;
}
.field-group input:focus {
  outline: none;
  border-color: #4f46e5;
}

.significance-textarea {
  width: 100%;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  padding: 6px 10px;
  font-family: inherit;
  resize: vertical;
  min-height: 80px;
}
.significance-textarea:focus {
  outline: none;
  border-color: #4f46e5;
}

.editor-hint {
  margin-top: 12px;
  padding: 8px 12px;
  background: #f0f4ff;
  border-radius: 6px;
  font-size: 12px;
  color: #4f46e5;
  border: 1px solid #c7d2fe;
}
.editor-hint p {
  margin: 0;
}
</style>