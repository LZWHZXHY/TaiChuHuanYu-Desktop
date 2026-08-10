<!-- src/views/世界观管理/type-editors/LocationEditor.vue -->
<template>
  <div class="location-editor">
    <div class="editor-grid">
      <div class="left-column">
        <div class="field-group">
          <label>危险等级</label>
          <div class="danger-levels">
            <button
              v-for="level in dangerLevels"
              :key="level"
              class="level-btn"
              :class="{
                active: localData.dangerLevel === level,
                [level]: true
              }"
              @click="localData.dangerLevel = level"
            >
              {{ level }}
            </button>
          </div>
          <p class="hint">选择地点的危险等级，会显示对应的颜色标识</p>
        </div>
      </div>

      <div class="right-column">
        <div class="field-group">
          <label>地图坐标</label>
          <div class="coordinate-group">
            <div class="coord-item">
              <span class="coord-label">X</span>
              <input
                v-model.number="localData.coordinate!.x"
                type="number"
                step="0.01"
              />
            </div>
            <div class="coord-item">
              <span class="coord-label">Y</span>
              <input
                v-model.number="localData.coordinate!.y"
                type="number"
                step="0.01"
              />
            </div>
          </div>
          <p class="coord-hint">用于在地图上标记位置</p>
        </div>

        <div class="map-preview">
          <div class="map-placeholder">
            <span v-if="localData.coordinate">
              📍 坐标: ({{ localData.coordinate.x }}, {{ localData.coordinate.y }})
            </span>
            <span v-else>暂无坐标信息</span>
          </div>
        </div>

        <div class="empty-hint">
          面积、人口、气候、统治者等信息<br />
          可通过「<strong>自定义属性</strong>」自由添加
        </div>
      </div>
    </div>

    <!-- 🔥 提示用户使用内容块插入关联卡片 -->
    <div class="editor-hint">
      <p>💡 提示：在「关联内容」区域点击「+气候」按钮，可以插入「气候」卡片</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import type { LocationData } from '../card_type'

const props = defineProps<{
  modelValue?: LocationData
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: LocationData): void
}>()

// ❌ 删除了 area、population、climate、ruler
const defaultData: LocationData = {
  id: '',
  projectId: '',
  type: 'location',
  title: '',
  coverImage: '',
  attributes: [],
  description: '',
  tags: [],
  relations: [],
  contentBlocks: [],
  createdAt: '',
  updatedAt: '',
  dangerLevel: undefined,
  coordinate: { x: 0, y: 0 },
}

const localData = ref<LocationData>({
  ...defaultData,
  ...(props.modelValue || {}),
})

const dangerLevels = ['低', '中', '高', '极度危险'] as const

watch(
  localData,
  (val) => {
    emit('update:modelValue', val)
  },
  { deep: true }
)
</script>

<style scoped>
.location-editor {
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
.field-group input {
  width: 100%;
}
.hint {
  font-size: 11px;
  color: #94a3b8;
  margin: 4px 0 0;
  font-style: italic;
}

.danger-levels {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}
.level-btn {
  padding: 4px 14px;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  background: white;
  cursor: pointer;
  transition: 0.15s;
  font-size: 13px;
}
.level-btn.active {
  border-color: #4f46e5;
  background: #eef2ff;
}
.level-btn.低.active {
  background: #dcfce7;
  border-color: #16a34a;
}
.level-btn.中.active {
  background: #fef3c7;
  border-color: #d97706;
}
.level-btn.高.active {
  background: #fecaca;
  border-color: #dc2626;
}
.level-btn.极度危险.active {
  background: #fef2f2;
  border-color: #ef4444;
  color: #ef4444;
}

.coordinate-group {
  display: flex;
  gap: 12px;
}
.coord-item {
  display: flex;
  align-items: center;
  gap: 4px;
}
.coord-label {
  font-size: 12px;
  font-weight: 600;
  color: #64748b;
}
.coord-item input {
  width: 80px;
  padding: 4px 8px;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
}
.coord-item input:focus {
  outline: none;
  border-color: #4f46e5;
}
.coord-hint {
  font-size: 12px;
  color: #94a3b8;
  margin: 4px 0 0;
}

.map-placeholder {
  min-height: 80px;
  background: #f1f5f9;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #94a3b8;
  font-size: 14px;
}

.empty-hint {
  margin-top: 16px;
  padding: 16px;
  text-align: center;
  color: #c0c4cc;
  font-size: 13px;
  border: 1px dashed #e2e8f0;
  border-radius: 6px;
  line-height: 1.8;
}
.empty-hint strong {
  color: #4f46e5;
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