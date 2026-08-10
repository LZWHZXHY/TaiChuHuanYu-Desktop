<!-- src/views/世界观管理/type-editors/CharacterEditor.vue -->
<template>
  <div class="character-editor">
    <div class="editor-grid">
      <!-- 左列 -->
      <div class="left-column">
        <div class="field-group">
          <label>年龄</label>
          <input v-model.number="localData.age" type="number" min="0" placeholder="如：25" />
        </div>

        <div class="field-group">
          <label>性别</label>
          <div class="gender-options">
            <button
              v-for="g in genderOptions"
              :key="g"
              class="gender-btn"
              :class="{ active: localData.gender === g }"
              @click="localData.gender = g"
            >
              {{ g }}
            </button>
          </div>
        </div>
      </div>

      <!-- 右列 -->
      <div class="right-column">
        <div class="field-group">
          <label>战力等级</label>
          <div class="power-level">
            <input
              v-model.number="localData.powerLevel"
              type="range"
              min="0"
              max="100"
              step="1"
            />
            <span class="power-value">{{ localData.powerLevel || 0 }}</span>
          </div>
        </div>

        <div class="field-group">
          <label>六维属性</label>
          <div class="stat-bars">
            <div v-for="stat in statConfigs" :key="stat.key" class="stat-bar">
              <span class="stat-label">{{ stat.label }}</span>
              <div class="bar-track">
                <div
                  class="bar-fill"
                  :style="{
                    width: (localData.stats?.[stat.key as keyof typeof localData.stats] || 0) + '%'
                  }"
                ></div>
              </div>
              <input
                v-model.number="localData.stats![stat.key as keyof typeof localData.stats]"
                type="number"
                min="0"
                max="100"
                class="stat-input"
              />
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 🔥 提示用户使用内容块插入关联卡片 -->
    <div class="editor-hint">
      <p>💡 提示：在「关联内容」区域点击「+职业」「+物种」「+派系」等按钮，可以插入相关卡片</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import type { CharacterData } from '../card_type'

const props = defineProps<{
  modelValue?: CharacterData
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: CharacterData): void
}>()

// ❌ 删除了 race、occupation、affiliation
const defaultData: CharacterData = {
  id: '',
  projectId: '',
  type: 'character',
  title: '',
  coverImage: '',
  attributes: [],
  description: '',
  tags: [],
  relations: [],
  contentBlocks: [],
  createdAt: '',
  updatedAt: '',
  age: undefined,
  gender: undefined,
  powerLevel: 0,
  stats: {
    strength: 0,
    agility: 0,
    intelligence: 0,
    charisma: 0,
    endurance: 0,
    luck: 0,
  },
}

const localData = ref<CharacterData>({
  ...defaultData,
  ...(props.modelValue || {}),
})

const genderOptions = ['男', '女', '其他', '未知'] as const

const statConfigs = [
  { key: 'strength', label: '力量' },
  { key: 'agility', label: '敏捷' },
  { key: 'intelligence', label: '智力' },
  { key: 'charisma', label: '魅力' },
  { key: 'endurance', label: '耐力' },
  { key: 'luck', label: '幸运' },
]

watch(localData, (val) => {
  emit('update:modelValue', val)
}, { deep: true })
</script>

<style scoped>
.character-editor {
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

.gender-options {
  display: flex;
  gap: 6px;
}
.gender-btn {
  padding: 4px 16px;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  background: white;
  cursor: pointer;
  transition: 0.15s;
}
.gender-btn.active {
  background: #4f46e5;
  color: white;
  border-color: #4f46e5;
}
.gender-btn:hover:not(.active) {
  background: #f1f5f9;
}

.power-level {
  display: flex;
  align-items: center;
  gap: 12px;
}
.power-level input[type="range"] {
  flex: 1;
  accent-color: #4f46e5;
}
.power-value {
  font-weight: 600;
  font-size: 18px;
  color: #4f46e5;
  min-width: 30px;
  text-align: center;
}

.stat-bars {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.stat-bar {
  display: flex;
  align-items: center;
  gap: 8px;
}
.stat-label {
  font-size: 12px;
  color: #64748b;
  width: 40px;
  flex-shrink: 0;
}
.bar-track {
  flex: 1;
  height: 6px;
  background: #f1f5f9;
  border-radius: 4px;
  overflow: hidden;
}
.bar-fill {
  height: 100%;
  background: linear-gradient(90deg, #4f46e5, #818cf8);
  border-radius: 4px;
  transition: width 0.3s;
}
.stat-input {
  width: 44px;
  padding: 2px 4px;
  border: 1px solid #e2e8f0;
  border-radius: 4px;
  font-size: 12px;
  text-align: center;
}
.stat-input:focus {
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