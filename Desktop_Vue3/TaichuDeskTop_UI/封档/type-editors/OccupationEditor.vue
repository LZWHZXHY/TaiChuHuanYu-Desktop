<!-- src/views/世界观管理/type-editors/OccupationEditor.vue -->
<template>
  <div class="occupation-editor">
    <div class="editor-grid">
      <div class="left-column">
        <div class="field-group">
          <label>职业等级</label>
          <div class="rank-options">
            <button
              v-for="r in rankOptions"
              :key="r"
              class="rank-btn"
              :class="{ active: localData.rank === r }"
              @click="localData.rank = r"
            >
              {{ r }}
            </button>
          </div>
        </div>

        <div class="field-group">
          <label>前置要求</label>
          <input v-model="localData.requirements" placeholder="如：战士 Lv.10 以上" />
        </div>

        <div class="field-group">
          <label>装备限制</label>
          <div class="tag-input-wrapper">
            <input
              v-model="equipmentInput"
              placeholder="输入装备类型，按回车添加"
              @keydown.enter.prevent="addEquipment"
            />
            <button class="add-tag-btn" @click="addEquipment">添加</button>
          </div>
          <div class="tag-list">
            <span v-for="e in localData.equipment" :key="e" class="tag-item">
              {{ e }}
              <button class="remove-tag" @click="removeEquipment(e)">×</button>
            </span>
          </div>
        </div>
      </div>

      <div class="right-column">
        <div class="field-group">
          <label>核心能力</label>
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
      <p>💡 提示：在「关联内容」区域点击「+派系」或「+职业」按钮，可以插入「所属阵营/信仰」或「进阶职业」卡片</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import type { OccupationData } from '../card_type'

const props = defineProps<{
  modelValue?: OccupationData
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: OccupationData): void
}>()

const abilityInput = ref('')
const equipmentInput = ref('')

// ❌ 删除了 advancement 和 affiliation 字段
const defaultData: OccupationData = {
  id: '',
  projectId: '',
  type: 'occupation',
  title: '',
  coverImage: '',
  attributes: [],
  description: '',
  tags: [],
  relations: [],
  contentBlocks: [],
  createdAt: '',
  updatedAt: '',
  rank: undefined,
  requirements: '',
  abilities: [],
  equipment: [],
}

const localData = ref<OccupationData>({
  ...defaultData,
  ...(props.modelValue || {}),
})

const rankOptions = ['初级', '中级', '高级', '大师', '传说'] as const

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

const addEquipment = () => {
  const text = equipmentInput.value.trim()
  if (!text) return
  if (localData.value.equipment?.includes(text)) {
    ElMessage.warning('已存在')
    return
  }
  if (!localData.value.equipment) localData.value.equipment = []
  localData.value.equipment.push(text)
  equipmentInput.value = ''
}

const removeEquipment = (e: string) => {
  if (!localData.value.equipment) return
  localData.value.equipment = localData.value.equipment.filter(item => item !== e)
}

watch(localData, (val) => {
  emit('update:modelValue', val)
}, { deep: true })
</script>

<style scoped>
.occupation-editor {
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
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  padding: 6px 10px;
}
.field-group input:focus {
  outline: none;
  border-color: #4f46e5;
}

.rank-options {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}
.rank-btn {
  padding: 4px 14px;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  background: white;
  cursor: pointer;
  font-size: 13px;
}
.rank-btn.active {
  background: #4f46e5;
  color: white;
  border-color: #4f46e5;
}

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
  font-size: 13px;
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