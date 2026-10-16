<!-- src/views/世界观管理/type-editors/SkillEditor.vue -->
<template>
  <div class="skill-editor">
    <div class="editor-grid">
      <div class="left-column">
        <div class="field-group">
          <label>技能类型</label>
          <div class="type-options">
            <button
              v-for="t in skillTypes"
              :key="t"
              class="type-btn"
              :class="{ active: localData.skillType === t }"
              @click="localData.skillType = t"
            >
              {{ t }}
            </button>
          </div>
        </div>

        <div class="field-group">
          <label>消耗</label>
          <input v-model="localData.cost" placeholder="如：100 法力、10 怒气" />
        </div>

        <div class="field-group">
          <label>冷却时间</label>
          <input v-model="localData.cooldown" placeholder="如：10秒、1小时、每日" />
        </div>
      </div>

      <div class="right-column">
        <div class="field-group">
          <label>效果描述</label>
          <textarea v-model="localData.effect" rows="3" placeholder="详细描述技能效果..." class="effect-textarea" />
        </div>

        <div class="field-group">
          <label>前置条件</label>
          <input v-model="localData.prerequisite" placeholder="如：等级 20、拥有 XX 技能" />
        </div>

        <div class="field-group">
          <label>等级</label>
          <input v-model.number="localData.level" type="number" placeholder="如：1" min="0" />
        </div>
      </div>
    </div>

    <!-- 🔥 提示用户使用内容块插入关联卡片 -->
    <div class="editor-hint">
      <p>💡 提示：如需关联「所属职业」或「持有角色」，可在「关联内容」区域点击「+职业」或「+角色」按钮插入对应卡片</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import type { SkillData } from '../card_type'

const props = defineProps<{
  modelValue?: SkillData
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: SkillData): void
}>()

const defaultData: SkillData = {
  id: '',
  projectId: '',
  type: 'skill',
  title: '',
  coverImage: '',
  attributes: [],
  description: '',
  tags: [],
  relations: [],
  contentBlocks: [],
  createdAt: '',
  updatedAt: '',
  skillType: undefined,
  cost: '',
  cooldown: '',
  effect: '',
  prerequisite: '',
  level: undefined,
}

const localData = ref<SkillData>({
  ...defaultData,
  ...(props.modelValue || {}),
})

const skillTypes = ['主动', '被动', '终极', '天赋'] as const

watch(localData, (val) => {
  emit('update:modelValue', val)
}, { deep: true })
</script>

<style scoped>
.skill-editor {
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

.type-options {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}
.type-btn {
  padding: 4px 14px;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  background: white;
  cursor: pointer;
}
.type-btn.active {
  background: #4f46e5;
  color: white;
  border-color: #4f46e5;
}

.effect-textarea {
  width: 100%;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  padding: 6px 10px;
  font-family: inherit;
  resize: vertical;
  min-height: 60px;
}
.effect-textarea:focus {
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