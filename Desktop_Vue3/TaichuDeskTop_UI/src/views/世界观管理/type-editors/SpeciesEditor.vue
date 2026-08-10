<!-- src/views/世界观管理/type-editors/SpeciesEditor.vue -->
<template>
  <div class="species-editor">
    <div class="editor-grid">
      <div class="left-column">
        <div class="field-group">
          <label>栖息地</label>
          <input v-model="localData.habitat" placeholder="如：森林、沙漠、海洋" />
        </div>

        <div class="field-group">
          <label>饮食</label>
          <input v-model="localData.diet" placeholder="如：肉食、草食、杂食" />
        </div>

        <div class="field-group">
          <label>寿命（年）</label>
          <input v-model.number="localData.lifespan" type="number" min="0" placeholder="如：200" />
        </div>

        <div class="field-group">
          <label>起源</label>
          <input v-model="localData.origin" placeholder="物种起源" />
        </div>
      </div>

      <div class="right-column">
        <div class="field-group">
          <label>特殊能力</label>
          <div class="tag-input-wrapper">
            <input
              v-model="abilityInput"
              placeholder="输入能力，按回车添加"
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
      <p>💡 提示：如需关联「所属生物」或「所属生态」，可在「关联内容」区域点击「+生物」或「+生态」按钮插入对应卡片</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import type { SpeciesData } from '../card_type'

const props = defineProps<{
  modelValue?: SpeciesData
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: SpeciesData): void
}>()

const abilityInput = ref('')

const defaultData: SpeciesData = {
  id: '',
  projectId: '',
  type: 'species',
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
  lifespan: undefined,
  abilities: [],
  origin: '',
}

const localData = ref<SpeciesData>({
  ...defaultData,
  ...(props.modelValue || {}),
})

const addAbility = () => {
  const text = abilityInput.value.trim()
  if (!text) return
  if (localData.value.abilities?.includes(text)) {
    ElMessage.warning('已存在')
    return
  }
  if (!localData.value.abilities) {
    localData.value.abilities = []
  }
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
.species-editor {
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