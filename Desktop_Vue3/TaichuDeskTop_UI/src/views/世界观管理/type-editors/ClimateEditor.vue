<!-- src/views/世界观管理/type-editors/ClimateEditor.vue -->
<template>
  <div class="climate-editor">
    <div class="editor-grid">
      <!-- ===== 左列：基础信息 ===== -->
      <div class="left-column">
        <div class="field-group">
          <label>气候名称 <span class="required">*</span></label>
          <input
            v-model="localData.climateName"
            placeholder="如：天灾、永夜、晶雨、混沌风暴"
          />
          <p class="hint">这个气候现象的核心名称</p>
        </div>

        <div class="field-group">
          <label>别名</label>
          <div class="tag-input-wrapper">
            <input
              v-model="aliasInput"
              placeholder="输入别名，按回车添加"
              @keydown.enter.prevent="addAlias"
            />
            <button class="add-tag-btn" @click="addAlias">添加</button>
          </div>
          <div class="tag-list">
            <span v-for="a in localData.alternativeNames" :key="a" class="tag-item">
              {{ a }}
              <button class="remove-tag" @click="removeAlias(a)">×</button>
            </span>
            <span v-if="!localData.alternativeNames?.length" class="tag-empty">暂无别名</span>
          </div>
        </div>

        <div class="field-group">
          <label>表现形式</label>
          <div class="tag-input-wrapper">
            <input
              v-model="manifestationInput"
              placeholder="输入表现形式，按回车添加"
              @keydown.enter.prevent="addManifestation"
            />
            <button class="add-tag-btn" @click="addManifestation">添加</button>
          </div>
          <div class="tag-list">
            <span
              v-for="m in localData.manifestations"
              :key="m"
              class="tag-item manifestation"
            >
              {{ m }}
              <button class="remove-tag" @click="removeManifestation(m)">×</button>
            </span>
            <span v-if="!localData.manifestations?.length" class="tag-empty">暂无表现形式</span>
          </div>
          <p class="hint">如：暴风、雪灾、陨石坠落、晶化雨、空间裂隙</p>
        </div>

        <div class="field-group">
          <label>频率</label>
          <div class="option-group">
            <button
              v-for="f in frequencyOptions"
              :key="f"
              class="option-btn"
              :class="{ active: localData.frequency === f }"
              @click="localData.frequency = f"
            >
              {{ f }}
            </button>
          </div>
        </div>

        <div class="field-group">
          <label>规律性</label>
          <div class="option-group">
            <button
              v-for="p in predictabilityOptions"
              :key="p"
              class="option-btn"
              :class="{ active: localData.predictability === p }"
              @click="localData.predictability = p"
            >
              {{ p }}
            </button>
          </div>
        </div>
      </div>

      <!-- ===== 右列：深层设定 ===== -->
      <div class="right-column">
        <div class="field-group">
          <label>成因</label>
          <textarea
            v-model="localData.cause"
            rows="3"
            placeholder="描述这个气候现象的成因..."
            class="textarea-field"
          />
          <p class="hint">是什么导致了这种气候？</p>
        </div>

        <div class="field-group">
          <label>影响</label>
          <div class="tag-input-wrapper">
            <input
              v-model="effectInput"
              placeholder="输入影响，按回车添加"
              @keydown.enter.prevent="addEffect"
            />
            <button class="add-tag-btn" @click="addEffect">添加</button>
          </div>
          <div class="tag-list">
            <span
              v-for="e in localData.effects"
              :key="e"
              class="tag-item effect"
            >
              {{ e }}
              <button class="remove-tag" @click="removeEffect(e)">×</button>
            </span>
            <span v-if="!localData.effects?.length" class="tag-empty">暂无影响</span>
          </div>
          <p class="hint">如：城市毁灭、地形重塑、生态崩溃、文明衰落</p>
        </div>

        <div class="field-group">
          <label>遗留影响</label>
          <div class="tag-input-wrapper">
            <input
              v-model="aftermathInput"
              placeholder="输入遗留影响，按回车添加"
              @keydown.enter.prevent="addAftermath"
            />
            <button class="add-tag-btn" @click="addAftermath">添加</button>
          </div>
          <div class="tag-list">
            <span
              v-for="a in localData.aftermath"
              :key="a"
              class="tag-item aftermath"
            >
              {{ a }}
              <button class="remove-tag" @click="removeAftermath(a)">×</button>
            </span>
            <span v-if="!localData.aftermath?.length" class="tag-empty">暂无遗留影响</span>
          </div>
          <p class="hint">灾害结束后留下的长期影响</p>
        </div>

        <div class="field-group">
          <label>应对方式</label>
          <div class="tag-input-wrapper">
            <input
              v-model="countermeasureInput"
              placeholder="输入应对方式，按回车添加"
              @keydown.enter.prevent="addCountermeasure"
            />
            <button class="add-tag-btn" @click="addCountermeasure">添加</button>
          </div>
          <div class="tag-list">
            <span
              v-for="c in localData.countermeasures"
              :key="c"
              class="tag-item countermeasure"
            >
              {{ c }}
              <button class="remove-tag" @click="removeCountermeasure(c)">×</button>
            </span>
            <span v-if="!localData.countermeasures?.length" class="tag-empty">暂无应对方式</span>
          </div>
          <p class="hint">如：移动城市迁徙、地下避难所、魔法结界、祭祀仪式</p>
        </div>

        <div class="field-group">
          <label>安全区域</label>
          <input v-model="localData.safeZones" placeholder="如：天灾安全带、庇护所、结界区" />
          <p class="hint">是否存在相对安全的区域</p>
        </div>
      </div>
    </div>

    <!-- ===== 提示 ===== -->
    <div class="editor-hint">
      <p>💡 提示：在「关联内容」区域可以插入「地点」「生态」「物种」等卡片，关联受此气候影响的设定</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import type { ClimateData } from '../card_type'

const props = defineProps<{
  modelValue?: ClimateData
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: ClimateData): void
}>()

// ===== 输入状态 =====
const aliasInput = ref('')
const manifestationInput = ref('')
const effectInput = ref('')
const aftermathInput = ref('')
const countermeasureInput = ref('')

// ===== 默认数据 =====
const defaultData: ClimateData = {
  id: '',
  projectId: '',
  type: 'climate',
  title: '',
  coverImage: '',
  attributes: [],
  description: '',
  tags: [],
  relations: [],
  contentBlocks: [],
  createdAt: '',
  updatedAt: '',
  climateName: '',
  alternativeNames: [],
  manifestations: [],
  frequency: '',
  predictability: '',
  cause: '',
  effects: [],
  aftermath: [],
  countermeasures: [],
  safeZones: '',
}

// ===== 本地数据 =====
const localData = ref<ClimateData>({
  ...defaultData,
  ...(props.modelValue || {}),
})

// ===== 选项 =====
const frequencyOptions = ['高频', '周期性', '罕见', '随机'] as const
const predictabilityOptions = ['规律可循', '规律难循', '完全随机'] as const

// ============================================================
//  别名
// ============================================================
const addAlias = () => {
  const text = aliasInput.value.trim()
  if (!text) return
  if (localData.value.alternativeNames?.includes(text)) {
    ElMessage.warning('已存在')
    return
  }
  if (!localData.value.alternativeNames) localData.value.alternativeNames = []
  localData.value.alternativeNames.push(text)
  aliasInput.value = ''
}

const removeAlias = (item: string) => {
  if (!localData.value.alternativeNames) return
  localData.value.alternativeNames = localData.value.alternativeNames.filter(a => a !== item)
}

// ============================================================
//  表现形式
// ============================================================
const addManifestation = () => {
  const text = manifestationInput.value.trim()
  if (!text) return
  if (localData.value.manifestations?.includes(text)) {
    ElMessage.warning('已存在')
    return
  }
  if (!localData.value.manifestations) localData.value.manifestations = []
  localData.value.manifestations.push(text)
  manifestationInput.value = ''
}

const removeManifestation = (item: string) => {
  if (!localData.value.manifestations) return
  localData.value.manifestations = localData.value.manifestations.filter(m => m !== item)
}

// ============================================================
//  影响
// ============================================================
const addEffect = () => {
  const text = effectInput.value.trim()
  if (!text) return
  if (localData.value.effects?.includes(text)) {
    ElMessage.warning('已存在')
    return
  }
  if (!localData.value.effects) localData.value.effects = []
  localData.value.effects.push(text)
  effectInput.value = ''
}

const removeEffect = (item: string) => {
  if (!localData.value.effects) return
  localData.value.effects = localData.value.effects.filter(e => e !== item)
}

// ============================================================
//  遗留影响
// ============================================================
const addAftermath = () => {
  const text = aftermathInput.value.trim()
  if (!text) return
  if (localData.value.aftermath?.includes(text)) {
    ElMessage.warning('已存在')
    return
  }
  if (!localData.value.aftermath) localData.value.aftermath = []
  localData.value.aftermath.push(text)
  aftermathInput.value = ''
}

const removeAftermath = (item: string) => {
  if (!localData.value.aftermath) return
  localData.value.aftermath = localData.value.aftermath.filter(a => a !== item)
}

// ============================================================
//  应对方式
// ============================================================
const addCountermeasure = () => {
  const text = countermeasureInput.value.trim()
  if (!text) return
  if (localData.value.countermeasures?.includes(text)) {
    ElMessage.warning('已存在')
    return
  }
  if (!localData.value.countermeasures) localData.value.countermeasures = []
  localData.value.countermeasures.push(text)
  countermeasureInput.value = ''
}

const removeCountermeasure = (item: string) => {
  if (!localData.value.countermeasures) return
  localData.value.countermeasures = localData.value.countermeasures.filter(c => c !== item)
}

// ============================================================
//  双向绑定
// ============================================================
watch(localData, (val) => {
  emit('update:modelValue', val)
}, { deep: true })
</script>

<style scoped>
.climate-editor {
  padding: 16px 0;
  border-top: 1px solid #eef2f6;
  border-bottom: 1px solid #eef2f6;
}

.editor-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 32px;
}

/* ===== 字段组 ===== */
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
.field-group .required {
  color: #ef4444;
}
.field-group input,
.field-group .el-select {
  width: 100%;
}
.field-group input {
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  padding: 6px 10px;
  font-size: 13px;
  background: #fafbfc;
  transition: border-color 0.2s;
}
.field-group input:focus {
  outline: none;
  border-color: #4f46e5;
  background: white;
}

.hint {
  font-size: 11px;
  color: #94a3b8;
  margin: 2px 0 0;
  font-style: italic;
}

/* ===== 文本域 ===== */
.textarea-field {
  width: 100%;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  padding: 6px 10px;
  font-family: inherit;
  font-size: 13px;
  resize: vertical;
  background: #fafbfc;
  transition: border-color 0.2s;
}
.textarea-field:focus {
  outline: none;
  border-color: #4f46e5;
  background: white;
}

/* ===== 选项按钮组 ===== */
.option-group {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}
.option-btn {
  padding: 4px 14px;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  background: white;
  cursor: pointer;
  font-size: 13px;
  transition: 0.15s;
}
.option-btn.active {
  background: #4f46e5;
  color: white;
  border-color: #4f46e5;
}
.option-btn:hover:not(.active) {
  background: #f1f5f9;
}

/* ===== 标签输入 ===== */
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
  background: #fafbfc;
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
  transition: 0.15s;
}
.add-tag-btn:hover {
  background: #f1f5f9;
}

/* ===== 标签列表 ===== */
.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-top: 4px;
  min-height: 24px;
}
.tag-item {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 8px 2px 10px;
  border-radius: 12px;
  font-size: 12px;
}
.tag-item.manifestation {
  background: #fef3c7;
  color: #d97706;
}
.tag-item.effect {
  background: #fecaca;
  color: #dc2626;
}
.tag-item.aftermath {
  background: #e0e7ff;
  color: #4f46e5;
}
.tag-item.countermeasure {
  background: #dcfce7;
  color: #16a34a;
}
.remove-tag {
  background: none;
  border: none;
  color: #94a3b8;
  cursor: pointer;
  font-size: 14px;
  padding: 0 2px;
}
.remove-tag:hover {
  color: #ef4444;
}
.tag-empty {
  font-size: 12px;
  color: #c0c4cc;
  padding: 2px 0;
}

/* ===== 提示条 ===== */
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