<!-- src/views/世界观管理/type-editors/CreatureEditor.vue -->
<template>
  <div class="creature-editor">
    <div class="editor-grid">
      <div class="left-column">
        <div class="field-group">
          <label>栖息地</label>
          <input v-model="localData.habitat" placeholder="如：森林、山地、洞穴" />
        </div>

        <div class="field-group">
          <label>饮食</label>
          <el-select v-model="localData.diet" filterable allow-create placeholder="选择或输入饮食类型">
            <el-option
              v-for="d in ['肉食', '草食', '杂食', '腐食', '魔法能量']"
              :key="d"
              :label="d"
              :value="d"
            />
          </el-select>
        </div>

        <div class="field-group">
          <label>性情</label>
          <input v-model="localData.temperament" placeholder="如：温顺、凶暴、狡猾" />
        </div>
      </div>

      <div class="right-column">
        <div class="field-group">
          <label>威胁等级</label>
          <div class="threat-levels">
            <button
              v-for="t in threatLevels"
              :key="t"
              class="threat-btn"
              :class="{ active: localData.threatLevel === t, [t]: true }"
              @click="localData.threatLevel = t"
            >
              {{ t }}
            </button>
          </div>
        </div>

        <div class="field-group">
          <label>特殊能力</label>
          <div class="tag-input-wrapper">
            <input
              v-model="abilityInput"
              placeholder="输入能力名称，按回车添加"
              @keydown.enter.prevent="addAbility"
            />
            <button class="add-tag-btn" @click="addAbility">添加</button>
          </div>
          <div class="tag-list">
            <span v-for="a in localData.abilities" :key="a" class="tag-item">
              {{ a }}
              <button class="remove-tag" @click="removeAbility(a)">×</button>
            </span>
          </div>
        </div>
      </div>
    </div>

    <!-- 🔥 提示用户使用内容块插入关联卡片 -->
    <div class="editor-hint">
      <p>💡 提示：在「关联内容」区域点击「+物种」按钮，可以插入「所属物种」卡片</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import type { CreatureData } from '../card_type'

const props = defineProps<{
  modelValue?: CreatureData
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: CreatureData): void
}>()

const abilityInput = ref('')

// ❌ 删除了 species 字段
const defaultData: CreatureData = {
  id: '',
  projectId: '',
  type: 'creature',
  title: '',
  coverImage: '',
  attributes: [],
  description: '',
  tags: [],
  relations: [],
  contentBlocks: [],
  createdAt: '',
  updatedAt: '',
  habitat: '',
  diet: '',
  temperament: '',
  abilities: [],
  threatLevel: undefined,
}

const localData = ref<CreatureData>({
  ...defaultData,
  ...(props.modelValue || {}),
})

const threatLevels = ['低', '中', '高', '极度危险'] as const

const addAbility = () => {
  const text = abilityInput.value.trim()
  if (!text) return
  if (localData.value.abilities?.includes(text)) {
    ElMessage.warning('已存在')
    return
  }
  if (!localData.value.abilities) localData.value.abilities = []
  localData.value.abilities.push(text)
  abilityInput.value = ''
}

const removeAbility = (a: string) => {
  if (!localData.value.abilities) return
  localData.value.abilities = localData.value.abilities.filter(item => item !== a)
}

watch(localData, (val) => {
  emit('update:modelValue', val)
}, { deep: true })
</script>

<style scoped>
.creature-editor {
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

.threat-levels {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}
.threat-btn {
  padding: 4px 14px;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  background: white;
  cursor: pointer;
  font-size: 13px;
}
.threat-btn.active {
  border-color: #4f46e5;
  background: #eef2ff;
}
.threat-btn.低.active { background: #dcfce7; border-color: #16a34a; }
.threat-btn.中.active { background: #fef3c7; border-color: #d97706; }
.threat-btn.高.active { background: #fecaca; border-color: #dc2626; }
.threat-btn.极度危险.active { background: #fef2f2; border-color: #ef4444; color: #ef4444; }

.tag-input-wrapper {
  display: flex;
  gap: 4px;
}
.tag-input-wrapper input {
  flex: 1;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  padding: 4px 8px;
  font-size: 13px;
}
.tag-input-wrapper input:focus {
  outline: none;
  border-color: #4f46e5;
}
.add-tag-btn {
  padding: 4px 12px;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  background: white;
  cursor: pointer;
}
.add-tag-btn:hover {
  background: #f1f5f9;
}

.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-top: 4px;
}
.tag-item {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  background: #eef2ff;
  color: #4f46e5;
  padding: 2px 8px 2px 10px;
  border-radius: 12px;
  font-size: 12px;
}
.remove-tag {
  background: none;
  border: none;
  color: #94a3b8;
  cursor: pointer;
  font-size: 14px;
}
.remove-tag:hover {
  color: #ef4444;
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