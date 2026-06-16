<template>
  <node-view-wrapper class="spirit-panel-graph-container">
    <div class="graph-layout" :class="{ 'is-editing': isEditing }">
      
      <div class="visual-zone">
        <svg :viewBox="`0 0 ${width} ${height}`" class="radar-svg">
          <polygon 
            v-for="scale in [1, 0.75, 0.5, 0.25]" 
            :key="scale"
            :points="getGridPoints(scale)" 
            class="radar-grid-line"
          />
          
          <line 
            v-for="(axis, index) in axisLines" 
            :key="'axis-'+index"
            :x1="centerX" :y1="centerY" 
            :x2="axis.x" :y2="axis.y" 
            class="radar-axis"
          />

          <polygon :points="valuePoints" class="radar-value-area" />

          <circle 
            v-for="(point, index) in dataPoints" 
            :key="'point-'+index"
            :cx="point.x" :cy="point.y" 
            r="4" 
            class="radar-value-dot"
          />

          <text 
            v-for="(label, index) in labelPositions" 
            :key="'label-'+index"
            :x="label.x" :y="label.y"
            :text-anchor="label.anchor"
            class="radar-label"
          >
            {{ label.name || '未命名' }} ({{ formatNumber(label.value) }})
          </text>
        </svg>
      </div>

      <div class="config-zone">
        <div class="zone-header">
          <div class="title-area">
            <span>维度数量: <strong>{{ totalAxes }}维</strong></span>
          </div>
          <button @click="isEditing = !isEditing" class="spirit-mini-btn">
            {{ isEditing ? '保存并闭合' : '设计多维属性' }}
          </button>
        </div>

        <div v-if="isEditing" class="designer-panel">
          <div class="preset-row">
            <span class="preset-label">快捷重塑:</span>
            <button @click="applyPreset(5)" class="preset-btn">5维</button>
            <button @click="applyPreset(6)" class="preset-btn">6维</button>
            <button @click="applyPreset(7)" class="preset-btn">7维</button>
          </div>

          <div class="attr-editor-list">
            <div v-for="(attr, index) in localAttributes" :key="index" class="attr-editor-row">
              <input 
                v-model="attr.name" 
                type="text" 
                placeholder="自定义属性名" 
                class="spirit-input attr-name" 
                @input="syncToTiptap"
              />
              
              <div class="range-inputs">
                <input 
                  v-model="attr.min" 
                  type="text" 
                  placeholder="Min" 
                  class="spirit-input num-input" 
                  @input="syncToTiptap"
                  @blur="handleInputBlur"
                />
                <span class="split">~</span>
                <input 
                  v-model="attr.max" 
                  type="text" 
                  placeholder="Max" 
                  class="spirit-input num-input" 
                  @input="syncToTiptap"
                  @blur="handleInputBlur"
                />
              </div>

              <div class="value-slider-box">
                <input 
                  v-model="attr.value" 
                  type="range" 
                  :min="attr.min" 
                  :max="attr.max" 
                  step="any"
                  class="spirit-slider"
                  @input="syncToTiptap"
                />
                <input 
                  v-model="attr.value" 
                  type="text" 
                  class="spirit-input value-num"
                  @input="syncToTiptap"
                  @blur="handleInputBlur"
                />
              </div>

              <button 
                @click="removeAttribute(index)" 
                class="delete-row-btn" 
                :disabled="localAttributes.length <= 3"
              >✕</button>
            </div>
          </div>
          
          <button @click="addNewAttribute" class="spirit-add-btn">+ 肆意添加自定义维度</button>
        </div>
      </div>

    </div>
  </node-view-wrapper>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { NodeViewWrapper, nodeViewProps } from '@tiptap/vue-3'

// 1. 声明多维属性条目的严格类型接口（支持输入阶段的 string/number 混态过渡）
interface AttributeItem {
  name: string
  value: number | string
  min: number | string
  max: number | string
}

const props = defineProps(nodeViewProps)

// 编辑器视窗控制状态
const isEditing = ref(false)
const width = 360
const height = 300
const centerX = width / 2
const centerY = height / 2
const radius = 95 // 网格最大可延伸半径

// 深度拷贝 Tiptap 的核心属性进行隔离绑定
const localAttributes = ref<AttributeItem[]>(JSON.parse(JSON.stringify(props.node.attrs.attributesList)))

// 动态追踪唯度数量（5维、6维、7维等）
const totalAxes = computed<number>(() => localAttributes.value.length)

// 2. 数学核心：根据当前唯度总数，完全等分 360 度圆周弧度
const angles = computed<number[]>(() => {
  const total = totalAxes.value
  return localAttributes.value.map((_: AttributeItem, i: number): number => {
    // 减去 Math.PI / 2 让第一个属性轴线永远笔直指向正上方 (12点方向)
    return (i * 2 * Math.PI) / total - Math.PI / 2
  })
})

// 3. 动态绘制背景多边形网格顶点坐标串
const getGridPoints = (scale: number): string => {
  const r = radius * scale
  return angles.value.map((angle: number): string => {
    const x = centerX + r * Math.cos(angle)
    const y = centerY + r * Math.sin(angle)
    return `${x},${y}`
  }).join(' ')
}

// 4. 动态绘制轴线辐射骨架
const axisLines = computed(() => {
  return angles.value.map((angle: number) => ({
    x: centerX + radius * Math.cos(angle),
    y: centerY + radius * Math.sin(angle)
  }))
})

// 5. 核心洗白：利用 parseFloat 全面支持浮点数，并映射百分比位置
const dataPoints = computed(() => {
  return localAttributes.value.map((attr: AttributeItem, i: number) => {
    const angle: number = angles.value[i]
    
    const minVal = parseFloat(attr.min as string) || 0
    const maxVal = parseFloat(attr.max as string) || 100
    const currVal = parseFloat(attr.value as string) || 0
    
    const range = maxVal - minVal
    const percent = range === 0 ? 0 : (currVal - minVal) / range
    const boundedPercent = Math.max(0, Math.min(1, percent)) // 越界安全防护
    
    const currRadius = radius * boundedPercent
    return {
      x: centerX + currRadius * Math.cos(angle),
      y: centerY + currRadius * Math.sin(angle)
    }
  })
})

// 生成多边形覆盖面坐标集
const valuePoints = computed<string>(() => 
  dataPoints.value.map((p: { x: number; y: number }): string => `${p.x},${p.y}`).join(' ')
)

// 6. 智能标签位置及文字对齐锚点计算
const labelPositions = computed(() => {
  const offsetRadius = radius + 15 
  return localAttributes.value.map((attr: AttributeItem, i: number) => {
    const angle: number = angles.value[i]
    const x = centerX + offsetRadius * Math.cos(angle)
    const y = centerY + offsetRadius * Math.sin(angle)
    
    // 智能决策文本对齐锚点，防止左右两侧排版发生突兀位移或溢出
    let anchor = 'middle'
    const cos = Math.cos(angle)
    if (cos > 0.15) anchor = 'start' // 右侧文字左对齐
    else if (cos < -0.15) anchor = 'end' // 左侧文字右对齐

    return { x, y: y + 4, name: attr.name, value: attr.value, anchor }
  })
})

// 7. 过滤浮点数十进制超长尾巴，保留最多两位小数，并抹除无用的末尾 0
const formatNumber = (val: number | string): string => {
  const num = parseFloat(val as string)
  if (isNaN(num)) return '0'
  return parseFloat(num.toFixed(2)).toString()
}

// 快捷生成 N 维经典模型
const applyPreset = (dimension: number): void => {
  const defaultNames = ['力量', '敏捷', '智力', '耐力', '精神', '运气', '爆发', '速度', '技巧']
  const newAttrs: AttributeItem[] = []
  for (let i = 0; i < dimension; i++) {
    newAttrs.push({
      name: defaultNames[i] || `自定义属性${i + 1}`,
      value: 50.0,
      min: 0.0,
      max: 100.0
    })
  }
  localAttributes.value = newAttrs
  syncToTiptap()
}

// 肆意添加自定义新维度
const addNewAttribute = (): void => {
  localAttributes.value.push({
    name: `新维度${localAttributes.value.length + 1}`,
    value: 50.0,
    min: 0.0,
    max: 100.0
  })
  syncToTiptap()
}

// 移除维度
const removeAttribute = (index: number): void => {
  if (localAttributes.value.length > 3) {
    localAttributes.value.splice(index, 1)
    syncToTiptap()
  }
}

// 实时向 Tiptap AST 抽象语法树广播数据
const syncToTiptap = (): void => {
  props.updateAttributes({
    attributesList: JSON.parse(JSON.stringify(localAttributes.value))
  })
}

// 当输入框失焦 (Blur) 时触发严格的数值清洗与边界约束防护
const handleInputBlur = (): void => {
  localAttributes.value.forEach((attr: AttributeItem) => {
    let minNum = attr.min === '' ? 0 : parseFloat(attr.min as string)
    let maxNum = attr.max === '' ? 100 : parseFloat(attr.max as string)
    let currNum = attr.value === '' ? 0 : parseFloat(attr.value as string)

    if (isNaN(minNum)) minNum = 0
    if (isNaN(maxNum)) maxNum = 100
    if (isNaN(currNum)) currNum = minNum

    // 纠正越界数据
    if (currNum > maxNum) currNum = maxNum
    if (currNum < minNum) currNum = minNum

    attr.min = minNum
    attr.max = maxNum
    attr.value = currNum
  })
  syncToTiptap()
}

// 监听由于 Undo / Redo 或历史记录撤销引起的外界状态流转
watch(() => props.node.attrs.attributesList, (newVal) => {
  localAttributes.value = JSON.parse(JSON.stringify(newVal))
}, { deep: true })
</script>

<style scoped>
/* 容器框架 */
.spirit-panel-graph-container {
  background: #ffffff;
  border: 1px solid rgba(0, 0, 0, 0.06);
  border-radius: 16px;
  padding: 16px;
  margin: 18px 0;
  box-shadow: 0 4px 24px rgba(0, 0, 0, 0.02);
}

.graph-layout {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

@media (min-width: 768px) {
  .graph-layout.is-editing { flex-direction: row; }
  .graph-layout.is-editing .visual-zone { flex: 4; }
  .graph-layout.is-editing .config-zone {
    flex: 6;
    border-left: 1px solid #f2f2f7;
    padding-left: 20px;
  }
}

.visual-zone {
  display: flex;
  justify-content: center;
  align-items: center;
}

.radar-svg {
  width: 100%;
  max-width: 340px;
  height: auto;
  overflow: visible;
}

/* 核心雷达样式图层 */
.radar-grid-line { fill: none; stroke: #e5e5ea; stroke-width: 1; }
.radar-axis { stroke: #f2f2f7; stroke-width: 1.2; stroke-dasharray: 2 2; }
.radar-value-area {
  fill: rgba(0, 102, 204, 0.16);
  stroke: #0066cc;
  stroke-width: 2.5;
  stroke-linejoin: round;
  transition: points 0.2s ease-out;
}
.radar-value-dot { fill: #ffffff; stroke: #0066cc; stroke-width: 2; }
.radar-label { 
  font-size: 10px; 
  font-weight: 600; 
  fill: #1d1d1f; 
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
}

/* 后台操控区样式 */
.config-zone { display: flex; flex-direction: column; gap: 12px; }
.zone-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 13px;
  color: #86868b;
}
.title-area strong { color: #0066cc; }

.designer-panel { display: flex; flex-direction: column; gap: 12px; }
.preset-row {
  display: flex;
  align-items: center;
  gap: 6px;
  background: #f5f5f7;
  padding: 6px 10px;
  border-radius: 8px;
}
.preset-label { font-size: 11px; color: #86868b; font-weight: 600; }
.preset-btn {
  background: #ffffff;
  border: 1px solid #d2d2d7;
  border-radius: 4px;
  font-size: 11px;
  padding: 2px 6px;
  cursor: pointer;
  color: #1d1d1f;
  transition: background 0.15s;
}
.preset-btn:hover { background: #f5f5f7; }

.attr-editor-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  max-height: 240px;
  overflow-y: auto;
}
.attr-editor-row {
  display: flex;
  align-items: center;
  gap: 6px;
  background: #fafafa;
  border: 1px solid #f2f2f7;
  padding: 6px 10px;
  border-radius: 8px;
}

.spirit-input {
  border: 1px solid #d2d2d7;
  border-radius: 6px;
  padding: 4px;
  font-size: 12px;
  background: #ffffff;
  outline: none;
  color: #1d1d1f;
}
.spirit-input:focus { border-color: #0066cc; }

.attr-name { width: 85px; font-weight: 600; }
.range-inputs { display: flex; align-items: center; gap: 1px; }
.num-input { width: 38px; text-align: center; color: #86868b; font-size: 11px; }
.split { font-size: 10px; color: #c7c7cc; }

.value-slider-box { display: flex; align-items: center; gap: 6px; flex: 1; }
.spirit-slider { flex: 1; height: 4px; accent-color: #0066cc; cursor: pointer; }
.value-num { width: 42px; text-align: center; font-weight: 600; color: #0066cc; }

.delete-row-btn { background: none; border: none; color: #ff3b30; cursor: pointer; font-size: 11px; }
.delete-row-btn:disabled { opacity: 0.15; cursor: not-allowed; }

.spirit-mini-btn {
  background: #0066cc;
  color: #ffffff;
  border: none;
  padding: 4px 10px;
  border-radius: 6px;
  font-size: 11px;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.15s;
}
.spirit-mini-btn:hover { background: #0055b3; }

.spirit-add-btn {
  background: none;
  border: 1px dashed #0066cc;
  color: #0066cc;
  padding: 6px;
  border-radius: 8px;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  text-align: center;
  transition: background 0.15s;
}
.spirit-add-btn:hover { background: rgba(0, 102, 204, 0.02); }
</style>