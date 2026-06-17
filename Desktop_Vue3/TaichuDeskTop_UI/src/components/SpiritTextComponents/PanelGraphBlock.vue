<template>
  <node-view-wrapper class="spirit-panel-graph-container" :class="{ 'has-maximized-layer': isInspecting }">
    
    <div class="graph-layout">
      <div class="visual-zone">
        <svg :viewBox="`0 0 ${width} ${height}`" class="radar-svg">
          <polygon v-for="scale in [1, 0.75, 0.5, 0.25]" :key="scale" :points="getGridPoints(scale)" class="radar-grid-line" />
          <line v-for="(axis, index) in axisLines" :key="'axis-'+index" :x1="centerX" :y1="centerY" :x2="axis.x" :y2="axis.y" class="radar-axis" />
          
          <g v-for="(layer, lIdx) in activeLayers" :key="'layer-'+lIdx" class="radar-layer-group">
            <polygon :points="getLayerValuePoints(layer)" class="radar-value-area" :style="{ '--layer-color': layer.color }" />
            <circle v-for="(point, pIdx) in getLayerDataPoints(layer)" :key="'p-'+pIdx" :cx="point.x" :cy="point.y" r="3.5" class="radar-value-dot" :style="{ stroke: layer.color }" />
          </g>
          <text v-for="(label, index) in labelPositions" :key="'label-'+index" :x="label.x" :y="label.y" :text-anchor="label.anchor" class="radar-label">
            {{ label.name }} (上限: {{ formatNumber(getDynamicMaxLimit(label.schemaItem)) }})
          </text>
        </svg>
      </div>

      <div class="config-zone">
        <div class="zone-header">
          <div class="template-selector-bar">
            <span class="panel-tag">📊 天道阵列面板</span>
            <button v-if="!isEditing" @click="isInspecting = true" class="action-ghost-btn">🔍 沉浸洞察数据</button>
            <button v-if="isEditing" @click="saveAsTemplate" class="action-ghost-btn">💾 存为通用骨架</button>
            <button v-if="isEditing" @click="loadTemplateMenu = !loadTemplateMenu" class="action-ghost-btn">📋 唤入通用骨架</button>
          </div>
          <div class="actions-flex-end">
            <button @click="toggleEditMode" class="spirit-mini-btn">{{ isEditing ? '闭合操控台' : '重塑演化矩阵' }}</button>
          </div>
        </div>

        <div v-if="loadTemplateMenu" class="template-pop-menu">
          <div v-for="tpl in savedTemplates" :key="tpl.name" @click="applyTemplate(tpl)" class="tpl-item">{{ tpl.name }} ({{ tpl.schema.length }}维)</div>
          <div v-if="savedTemplates.length === 0" class="tpl-empty">本地暂无暂存骨架</div>
        </div>

        <div v-if="isEditing" class="designer-panel">
          <div class="tabs-header">
            <span :class="{ active: activeTab === 'schema' }" @click="activeTab = 'schema'">1. 阵营维度与公式设计</span>
            <span :class="{ active: activeTab === 'data' }" @click="activeTab = 'data'">2. 叠层多目标数值填报</span>
          </div>
          
          <div v-if="activeTab === 'schema'" class="schema-designer-list">
            <div v-for="(item, idx) in localSchema" :key="idx" class="schema-row-card">
              <div class="row-main">
                <input v-model="item.name" class="spirit-input name-input" placeholder="属性维度名" @input="syncToTiptap" />
                <select v-model="item.type" class="spirit-select" @change="syncToTiptap">
                  <option value="base">原生可见滑块</option>
                  <option value="raw_counter">寄存数据(不上墙)</option>
                  <option value="computed">天道公式衍生</option>
                </select>
                <div class="range-box" v-if="item.type !== 'raw_counter'">
                  <input v-model="item.min" class="spirit-input min-max" placeholder="下限" @input="syncToTiptap" />
                  <span>~</span>
                  <input v-model="item.max" class="spirit-input min-max" placeholder="上限/AUTO" @input="syncToTiptap" />
                </div>
                <label v-if="item.type !== 'raw_counter'" class="reverse-label">
                  <input type="checkbox" v-model="item.reverse" @change="syncToTiptap" />
                  <span>越低越好</span>
                </label>
                <button @click="removeSchemaItem(idx)" class="del-btn" :disabled="localSchema.length<=3">✕</button>
              </div>
              <div v-if="item.type === 'computed'" class="formula-row">
                <span class="fx-symbol">ƒ(x) =</span>
                <input v-model="item.formula" class="spirit-input formula-input" placeholder="输入公式如: [累计总伤害] / [总场次]" @input="syncToTiptap" />
              </div>
            </div>
            <button @click="addNewSchemaItem" class="spirit-add-btn">+ 肆意添加新维度属性</button>
          </div>
          
          <div v-if="activeTab === 'data'" class="layers-manager-panel">
            <div class="layers-top-actions">
              <button @click="addNewLayer" class="spirit-mini-btn">+ 追加横向对比叠层(如新英雄/流派)</button>
            </div>

            <div class="matrix-grid-scroll-container">
              <table class="matrix-grid-table">
                <thead>
                  <tr>
                    <th class="sticky-col-header">属性维度 \ 目标</th>
                    <th v-for="(layer, lIdx) in localLayers" :key="'th-'+lIdx" :style="{ '--th-color': layer.color }">
                      <div class="th-input-wrapper">
                        <input v-model="layer.color" type="color" class="matrix-color-picker" @change="syncToTiptap" />
                        <input v-model="layer.name" class="matrix-th-input" placeholder="名字" @input="syncToTiptap" />
                        <button @click="removeLayer(lIdx)" class="matrix-del-col" :disabled="localLayers.length<=1">✕</button>
                      </div>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="schemaItem in localSchema" :key="schemaItem.name">
                    <td class="sticky-col-dim">
                      <span class="dim-badge" :class="schemaItem.type">{{ schemaItem.name }}</span>
                    </td>
                    <td v-for="(layer, lIdx) in localLayers" :key="'td-'+lIdx+'-'+schemaItem.name">
                      
                      <div v-if="schemaItem.type === 'base'" class="matrix-cell-flex">
                        <input type="range" :min="schemaItem.min" :max="getDynamicMaxLimit(schemaItem)" step="any" v-model="layer.values[schemaItem.name]" @input="syncToTiptap" class="matrix-slider" />
                        <input type="text" v-model="layer.values[schemaItem.name]" class="matrix-num-input" @input="syncToTiptap" />
                      </div>

                      <div v-else-if="schemaItem.type === 'raw_counter'" class="matrix-cell-counter">
                        <div class="counter-actions-row">
                          <button @click="quickAdjustCounter(layer, schemaItem.name, 1)" class="matrix-step-btn">+1</button>
                          <button @click="quickAdjustCounter(layer, schemaItem.name, -1)" class="matrix-step-btn">-1</button>
                          <input type="text" v-model="layer.values[schemaItem.name]" class="matrix-num-input plain" @input="syncToTiptap" />
                        </div>
                        <input type="number" placeholder="输入回车追加累计..." class="matrix-append-input" @keyup.enter="appendMatchStream($event, layer, schemaItem.name)" />
                      </div>

                      <div v-else class="matrix-cell-computed">
                        {{ formatNumber(calculateFormula(schemaItem, layer)) }}
                      </div>

                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <div v-else class="static-layers-legends">
          <div v-for="layer in localLayers" :key="layer.name" class="legend-tag">
            <span class="legend-color-dot" :style="{ backgroundColor: layer.color }"></span>
            <span class="legend-name">{{ layer.name || '未命名目标' }}</span>
          </div>
        </div>
      </div>
    </div>

    <Teleport to="body">
      <div v-if="isInspecting" class="spirit-inspect-portal-root">
        <div class="inspect-overlay" @click="isInspecting = false"></div>
        <div class="maximize-window-card">
          <div class="window-inner-layout">
            <div class="portal-visual-zone">
              <svg viewBox="0 0 360 300" class="portal-radar-svg">
                <polygon v-for="scale in [1, 0.75, 0.5, 0.25]" :key="scale" :points="getGridPoints(scale)" class="radar-grid-line" />
                <line v-for="(axis, index) in axisLines" :key="'lbl-ax-'+index" :x1="centerX" :y1="centerY" :x2="axis.x" :y2="axis.y" class="radar-axis" />
                <g v-for="(layer, lIdx) in activeLayers" :key="'l-'+lIdx" class="radar-layer-group" :class="{ 'is-muted': focusedLayerName && focusedLayerName !== layer.name, 'is-focused': focusedLayerName === layer.name }">
                  <polygon :points="getLayerValuePoints(layer)" class="radar-value-area" :style="{ '--layer-color': layer.color }" />
                  <circle v-for="(point, pIdx) in getLayerDataPoints(layer)" :key="'pt-'+pIdx" :cx="point.x" :cy="point.y" r="4" class="radar-value-dot" :style="{ stroke: layer.color }" />
                </g>
                <text v-for="(label, index) in labelPositions" :key="'lbl-'+index" :x="label.x" :y="label.y" :text-anchor="label.anchor" class="radar-label">{{ label.name }}</text>
              </svg>
            </div>
            <div class="portal-config-zone">
              <div class="portal-header-row">
                <span class="portal-tag-title">📊 寰宇天道洞察矩阵</span>
                <button @click="isInspecting = false" class="spirit-mini-btn portal-close-btn">退出洞察</button>
              </div>
              <div class="inspect-scroll-area">
                <div v-for="(layer, lIdx) in localLayers" :key="lIdx" class="inspect-target-card" @mouseenter="focusedLayerName = layer.name" @mouseleave="focusedLayerName = null" :style="{ '--target-color': layer.color }">
                  <div class="target-card-top">
                    <span class="dot" :style="{ backgroundColor: layer.color }"></span>
                    <span class="name">{{ layer.name }}</span>
                    <span class="focus-tip">✨ 独立高亮聚焦</span>
                  </div>
                  <div class="target-values-wall">
                    <div v-for="item in visibleSchema" :key="item.name" class="wall-cell">
                      <div class="cell-attr-name">{{ item.name }}</div>
                      <div class="cell-attr-value">
                        <span class="actual-v">{{ formatNumber(getLayerValue(item, layer)) }}</span>
                        <span class="max-v">/{{ getDynamicMaxLimit(item) }}</span>
                      </div>
                      <div class="cell-badges">
                        <span v-if="isHighest(item, layer)" class="badge highest">最高</span>
                        <span v-if="isLowest(item, layer)" class="badge lowest">最低</span>
                        <span v-if="isMaxedOut(item, layer)" class="badge maxed">满阶</span>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </Teleport>

  </node-view-wrapper>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { NodeViewWrapper, nodeViewProps } from '@tiptap/vue-3'

interface SchemaItem { name: string; type: 'base' | 'raw_counter' | 'computed'; min: number | string; max: number | string; reverse: boolean; formula?: string; }
interface LayerItem { name: string; color: string; values: Record<string, number | string>; }

const props = defineProps(nodeViewProps)

const isEditing = ref(false)
const isInspecting = ref(false)
const focusedLayerName = ref<string | null>(null)
const activeTab = ref<'schema' | 'data'>('schema')
const loadTemplateMenu = ref(false)
const savedTemplates = ref<any[]>([])

const width = 360; const height = 300; const centerX = width / 2; const centerY = height / 2; const radius = 90;
const localSchema = ref<SchemaItem[]>([]); const localLayers = ref<LayerItem[]>([]);

const initData = () => {
  const attrs = props.node.attrs.attributesList
  if (attrs && attrs.schema) {
    localSchema.value = attrs.schema; localLayers.value = attrs.layers
  } else if (Array.isArray(attrs) && attrs.length > 0 && ('value' in attrs[0])) {
    localSchema.value = attrs.map(a => ({ name: a.name, type: 'base', min: a.min ?? 0, max: a.max ?? 100, reverse: false }))
    const defaultValues: Record<string, any> = {}; attrs.forEach(a => { defaultValues[a.name] = a.value })
    localLayers.value = [{ name: '当前流层', color: '#0066cc', values: defaultValues }]
  } else {
    localSchema.value = [
      { name: '总场次', type: 'raw_counter', min: 0, max: 1000, reverse: false },
      { name: '累计总伤害', type: 'raw_counter', min: 0, max: 1000000, reverse: false },
      { name: '场均伤害', type: 'computed', formula: '[累计总伤害] / [总场次]', min: 0, max: 'AUTO', reverse: false },
      { name: '速度', type: 'base', min: 0, max: 100, reverse: false },
      { name: '力量', type: 'base', min: 0, max: 100, reverse: false }
    ]
    localLayers.value = [{ name: '当前状态流层', color: '#0066cc', values: { '总场次': 10, '累计总伤害': 12000, '速度': 80, '力量': 70 } }]
  }
}

const visibleSchema = computed(() => localSchema.value.filter(s => s.type !== 'raw_counter'))
const totalAxes = computed(() => visibleSchema.value.length)
const angles = computed<number[]>(() => { const total = totalAxes.value; return visibleSchema.value.map((_, i) => (i * 2 * Math.PI) / total - Math.PI / 2) })

const calculateFormula = (item: SchemaItem, layer: LayerItem): number => {
  if (!item.formula) return 0; let expr = item.formula
  localSchema.value.forEach(s => { if (s.type === 'base' || s.type === 'raw_counter') { const val = parseFloat(layer.values[s.name] as string) || 0; expr = expr.replace(new RegExp(`\\[${s.name}\\]`, 'g'), String(val)) } })
  try { const cleanExpr = expr.replace(/[^0-9.+\-*/() ]/g, ''); const result = Function(`"use strict"; return (${cleanExpr})`)(); return isFinite(result) ? result : 0 } catch { return 0 }
}

const getLayerValue = (item: SchemaItem, layer: LayerItem): number => item.type === 'computed' ? calculateFormula(item, layer) : (parseFloat(layer.values[item.name] as string) || 0)

// 🌟 核心突破一：天道自适应动态最大值结算网
const getDynamicMaxLimit = (item: SchemaItem): number => {
  if (String(item.max).trim().toUpperCase() === 'AUTO') {
    if (localLayers.value.length === 0) return 100
    // 横向扫描所有人当前算出或填入的最高值
    const allLayerValues = localLayers.value.map(l => getLayerValue(item, l))
    const currentMax = Math.max(...allLayerValues)
    // 如果最高值是0，兜底返回100，否则以最大那个人作为自适应上限
    return currentMax === 0 ? 100 : currentMax
  }
  return parseFloat(item.max as string) || 100
}

const quickAdjustCounter = (layer: LayerItem, name: string, step: number) => { const current = parseFloat(layer.values[name] as string) || 0; layer.values[name] = Math.max(0, current + step); syncToTiptap() }
const appendMatchStream = (event: Event, layer: LayerItem, counterName: string) => {
  const target = event.target as HTMLInputElement; const inputVal = parseFloat(target.value); if (isNaN(inputVal)) return
  const previousTotal = parseFloat(layer.values[counterName] as string) || 0
  layer.values[counterName] = previousTotal + inputVal
  const matchCountSchema = localSchema.value.find(s => s.name.includes('场次') && s.type === 'raw_counter')
  if (matchCountSchema) {
    const prevCount = parseFloat(layer.values[matchCountSchema.name] as string) || 0
    layer.values[matchCountSchema.name] = prevCount + 1
  }
  target.value = ''; syncToTiptap()
}

const isHighest = (item: SchemaItem, currentLayer: LayerItem): boolean => { if (localLayers.value.length <= 1) return false; const allValues = localLayers.value.map(l => getLayerValue(item, l)); return getLayerValue(item, currentLayer) === Math.max(...allValues) && Math.max(...allValues) !== Math.min(...allValues) }
const isLowest = (item: SchemaItem, currentLayer: LayerItem): boolean => { if (localLayers.value.length <= 1) return false; const allValues = localLayers.value.map(l => getLayerValue(item, l)); return getLayerValue(item, currentLayer) === Math.min(...allValues) && Math.max(...allValues) !== Math.min(...allValues) }
const isMaxedOut = (item: SchemaItem, layer: LayerItem): boolean => getLayerValue(item, layer) >= getDynamicMaxLimit(item)

const getGridPoints = (scale: number): string => { const r = radius * scale; return angles.value.map(angle => `${centerX + r * Math.cos(angle)},${centerY + r * Math.sin(angle)}`).join(' ') }
const axisLines = computed(() => angles.value.map(angle => ({ x: centerX + radius * Math.cos(angle), y: centerY + radius * Math.sin(angle) })))

// 🌟 核心突破二：属性破格破限表现。去除了 Math.min(1, percent) 的钉死枷锁，允许百分比超出 1.0 直接穿透骨架
const getLayerDataPoints = (layer: LayerItem) => {
  return visibleSchema.value.map((item, i) => {
    const angle = angles.value[i]; 
    const currVal = getLayerValue(item, layer); 
    const minVal = parseFloat(item.min as string) || 0; 
    const maxVal = getDynamicMaxLimit(item); 
    const range = maxVal - minVal; 
    
    let percent = range === 0 ? 0 : (currVal - minVal) / range; 
    if (item.reverse) {
      percent = range === 0 ? 1 : (maxVal - currVal) / range;
    }
    
    // 只做下限 0 防护，完全不做 1 上限封锁！让溢出数据（如 percent = 1.5）直接刺穿最外围网格！
    const breakOutPercent = Math.max(0, percent); 
    return { x: centerX + radius * breakOutPercent * Math.cos(angle), y: centerY + radius * breakOutPercent * Math.sin(angle) }
  })
}

const getLayerValuePoints = (layer: LayerItem): string => getLayerDataPoints(layer).map(p => `${p.x},${p.y}`).join(' ')
const labelPositions = computed(() => { const offsetRadius = radius + 15; return visibleSchema.value.map((item, i) => { const angle = angles.value[i]; let anchor = 'middle'; if (Math.cos(angle) > 0.15) anchor = 'start'; else if (Math.cos(angle) < -0.15) anchor = 'end'; return { x: centerX + offsetRadius * Math.cos(angle), y: centerY + offsetRadius * Math.sin(angle) + 4, name: item.name, anchor, schemaItem: item } }) })
const activeLayers = computed(() => localLayers.value)

const toggleEditMode = () => { isEditing.value = !isEditing.value; if (isEditing.value) isInspecting.value = false }
const saveAsTemplate = () => { const name = prompt('输入骨架模板名称:', '自定义模型'); if (!name) return; const currentPool = JSON.parse(localStorage.getItem('spirit_radar_templates') || '[]'); currentPool.push({ name, schema: JSON.parse(JSON.stringify(localSchema.value)) }); localStorage.setItem('spirit_radar_templates', JSON.stringify(currentPool)); loadTemplatesFromStorage() }
const applyTemplate = (tpl: any) => { localSchema.value = JSON.parse(JSON.stringify(tpl.schema)); localLayers.value.forEach(layer => { const newValues: Record<string, any> = {}; localSchema.value.forEach(s => { newValues[s.name] = layer.values[s.name] ?? s.min }); layer.values = newValues }); loadTemplateMenu.value = false; syncToTiptap() }
const loadTemplatesFromStorage = () => { savedTemplates.value = JSON.parse(localStorage.getItem('spirit_radar_templates') || '[]') }

const addNewSchemaItem = () => { localSchema.value.push({ name: `属性${localSchema.value.length+1}`, type: 'base', min: 0, max: 100, reverse: false }); syncToTiptap() }
const removeSchemaItem = (idx: number) => { localSchema.value.splice(idx, 1); syncToTiptap() }
const addNewLayer = () => { const defaultValues: Record<string, any> = {}; localSchema.value.forEach(s => { defaultValues[s.name] = s.min ?? 0 }); localLayers.value.push({ name: `流层目标${localLayers.value.length+1}`, color: '#e63946', values: defaultValues }); syncToTiptap() }
const removeLayer = (idx: number) => { localLayers.value.splice(idx, 1); syncToTiptap() }

const syncToTiptap = () => { props.updateAttributes({ attributesList: { schema: JSON.parse(JSON.stringify(localSchema.value)), layers: JSON.parse(JSON.stringify(localLayers.value)) } }) }
const handleInputBlur = () => { localSchema.value.forEach(s => { if(s.min === '') s.min = 0; if(s.max === '') s.max = 100 }); syncToTiptap() }
const formatNumber = (val: number): string => parseFloat(val.toFixed(2)).toString()

watch(() => props.node.attrs.attributesList, () => { initData() }, { deep: true })
onMounted(() => { initData(); loadTemplatesFromStorage() })
</script>

<style scoped>
.spirit-panel-graph-container { background: #ffffff; border: 1px solid rgba(0, 0, 0, 0.05); border-radius: 20px; padding: 20px; margin: 18px 0; box-shadow: 0 4px 30px rgba(0,0,0,0.01); position: relative; }
.graph-layout { display: flex; flex-direction: column; gap: 24px; }
@media (min-width: 768px) {
  .graph-layout.is-editing { flex-direction: row; }
  .graph-layout.is-editing .visual-zone { flex: 4; }
  .graph-layout.is-editing .config-zone { flex: 6; border-left: 1px solid #f2f2f7; padding-left: 20px; }
}
.visual-zone { display: flex; justify-content: center; align-items: center; }
.radar-svg { width: 100%; max-width: 280px; height: auto; overflow: visible; }
.radar-grid-line { fill: none; stroke: #e5e5ea; stroke-width: 0.8; }
.radar-axis { stroke: rgba(0, 0, 0, 0.03); stroke-width: 1; stroke-dasharray: 2 2; }
.radar-value-area { fill: var(--layer-color); fill-opacity: 0.1; stroke: var(--layer-color); stroke-width: 2.2; stroke-linejoin: round; }
.matrix-slider { flex: 1; height: 3px; accent-color: #0066cc; }
.radar-value-dot { fill: #ffffff; stroke-width: 2; }
.radar-label { font-size: 10px; font-weight: 700; fill: #1d1d1f; }
.config-zone { display: flex; flex-direction: column; gap: 14px; position: relative; min-width: 0; }
.zone-header { display: flex; justify-content: space-between; align-items: center; }
.template-selector-bar { display: flex; align-items: center; gap: 8px; }
.panel-tag { font-size: 12px; font-weight: 800; color: #86868b; text-transform: uppercase; letter-spacing: 0.05em; }

.matrix-grid-scroll-container { width: 100%; overflow-x: auto; border: 1px solid #e5e5ea; border-radius: 12px; background: #ffffff; box-shadow: 0 2px 12px rgba(0,0,0,0.02); margin-top: 8px; }
.matrix-grid-table { width: 100%; border-collapse: collapse; font-size: 12px; text-align: left; }
.matrix-grid-table th { background: #f5f5f7; padding: 10px 14px; border-bottom: 2px solid #e5e5ea; border-right: 1px solid #efeff4; min-width: 200px; box-sizing: border-box; }
.matrix-grid-table th.sticky-col-header { position: sticky; left: 0; z-index: 5; background: #e8e8ed; min-width: 110px; font-weight: 700; color: #1d1d1f; }
.matrix-grid-table td { padding: 8px 12px; border-bottom: 1px solid #efeff4; border-right: 1px solid #efeff4; vertical-align: middle; box-sizing: border-box; }
.matrix-grid-table td.sticky-col-dim { position: sticky; left: 0; z-index: 4; background: #fbfbfd; font-weight: 700; border-right: 2px solid #e5e5ea; min-width: 110px; }

.dim-badge { font-size: 11px; padding: 2px 6px; border-radius: 4px; display: inline-block; }
.dim-badge.base { background: rgba(0, 102, 204, 0.06); color: #0066cc; }
.dim-badge.raw_counter { background: rgba(142, 142, 147, 0.1); color: #555559; }
.dim-badge.computed { background: rgba(52, 199, 89, 0.08); color: #24b249; }

.th-input-wrapper { display: flex; align-items: center; gap: 6px; }
.matrix-color-picker { border: none; background: transparent; width: 18px; height: 18px; cursor: pointer; padding: 0; }
.matrix-th-input { border: none; background: transparent; font-weight: 700; font-size: 12px; color: #1d1d1f; outline: none; flex: 1; width: 60px; border-bottom: 1px dashed transparent; }
.matrix-th-input:focus { border-bottom-color: #0066cc; }
.matrix-del-col { background: none; border: none; color: #ff3b30; cursor: pointer; font-size: 10px; }

.matrix-cell-flex { display: flex; align-items: center; gap: 6px; }
.matrix-num-input { width: 40px; font-size: 11px; text-align: center; border: 1px solid #d2d2d7; border-radius: 4px; padding: 2px; }
.matrix-num-input.plain { border: none; background: #f5f5f7; font-weight: 700; width: 34px; }
.matrix-cell-counter { display: flex; flex-direction: column; gap: 4px; }
.counter-actions-row { display: flex; align-items: center; gap: 3px; }
.matrix-step-btn { background: #e8e8ed; border: none; font-size: 9px; font-weight: 700; width: 20px; height: 18px; border-radius: 3px; cursor: pointer; }
.matrix-step-btn:hover { background: #d2d2d7; }
.matrix-append-input { font-size: 10px; padding: 2px 4px; border: 1px solid #0066cc; border-radius: 4px; color: #0066cc; outline: none; width: 100%; box-sizing: border-box; }
.matrix-cell-computed { font-weight: 700; color: #0066cc; background: rgba(0,102,204,0.03); padding: 4px; border-radius: 4px; text-align: center; }

.spirit-inspect-portal-root { position: fixed; inset: 0; z-index: 1000000 !important; display: flex; align-items: center; justify-content: center; }
.inspect-overlay { position: absolute; inset: 0; background: rgba(230, 230, 235, 0.65); backdrop-filter: blur(30px) saturate(180%); z-index: 10; cursor: zoom-out; }
.maximize-window-card { position: relative; width: 92vw; height: 88vh; background: #ffffff !important; border-radius: 24px; box-shadow: 0 35px 80px rgba(0, 0, 0, 0.15), 0 0 1px rgba(0, 0, 0, 0.2); border: 1px solid rgba(0, 0, 0, 0.06); padding: 32px; z-index: 20; box-sizing: border-box; }
.window-inner-layout { display: flex; width: 100%; height: 100%; gap: 36px; }
.portal-visual-zone { flex: 4.5; display: flex; justify-content: center; align-items: center; background: #fafafa; border-radius: 18px; border: 1px solid rgba(0,0,0,0.02); }
.portal-radar-svg { width: 100%; max-width: 440px; height: auto; overflow: visible; }
.portal-config-zone { flex: 5.5; display: flex; flex-direction: column; min-width: 0; }
.portal-header-row { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
.portal-tag-title { font-size: 15px; font-weight: 800; color: #1d1d1f; letter-spacing: -0.01em; }
.portal-close-btn { background: #ff3b30 !important; color: #ffffff !important; font-weight: 700; border: none; }
.static-layers-legends { display: flex; flex-wrap: wrap; gap: 8px; background: #f5f5f7; padding: 8px 14px; border-radius: 10px; margin-top: 6px; }
.legend-tag { display: flex; align-items: center; gap: 6px; font-size: 11px; font-weight: 600; color: #1d1d1f; }
.legend-color-dot { width: 8px; height: 8px; border-radius: 50%; display: inline-block; }

.inspect-scroll-area { display: flex; flex-direction: column; gap: 16px; overflow-y: auto; flex: 1; padding-right: 4px; }
.inspect-target-card { background: #f5f5f7; border-radius: 16px; padding: 16px; border: 1px solid transparent; transition: all 0.2s; }
.inspect-target-card:hover { background: #ffffff; border-color: var(--target-color); box-shadow: 0 8px 32px rgba(0,0,0,0.05); }
.target-card-top { display: flex; align-items: center; gap: 8px; margin-bottom: 12px; }
.target-card-top .dot { width: 10px; height: 10px; border-radius: 50%; }
.target-card-top .name { font-size: 14px; font-weight: 700; color: #1d1d1f; }
.target-card-top .focus-tip { font-size: 11px; color: #0066cc; margin-left: auto; opacity: 0; font-weight: 600; }
.inspect-target-card:hover .focus-tip { opacity: 1; }
.target-values-wall { display: grid; grid-template-columns: repeat(auto-fill, minmax(140px, 1fr)); gap: 10px; }
.wall-cell { background: rgba(255,255,255,0.7); padding: 10px 12px; border-radius: 10px; border: 0.5px solid rgba(0,0,0,0.02); display: flex; flex-direction: column; position: relative; }
.cell-attr-name { font-size: 11px; color: #86868b; font-weight: 600; margin-bottom: 4px; }
.cell-attr-value { font-size: 13px; font-weight: 700; color: #1d1d1f; }
.cell-attr-value .max-v { font-size: 11px; color: #c7c7cc; font-weight: 500; }
.cell-badges { position: absolute; right: 6px; top: 6px; display: flex; flex-direction: column; gap: 2px; }
.badge { font-size: 8px; padding: 1px 4px; border-radius: 3px; font-weight: 700; }
.badge.highest { background: rgba(52, 199, 89, 0.1); color: #34c759; }
.badge.lowest { background: rgba(255, 59, 48, 0.1); color: #ff3b30; }
.badge.maxed { background: rgba(0, 102, 204, 0.1); color: #0066cc; }

.action-ghost-btn { background: none; border: none; color: #0066cc; font-size: 11px; font-weight: 600; cursor: pointer; padding: 2px 6px; border-radius: 4px; }
.action-ghost-btn:hover { background: rgba(0, 102, 204, 0.05); }
.template-pop-menu { position: absolute; top: 32px; left: 0; background: #ffffff; border: 1px solid #e5e5ea; border-radius: 10px; box-shadow: 0 8px 24px rgba(0,0,0,0.08); padding: 6px; z-index: 50; width: 180px; }
.tpl-item { font-size: 12px; padding: 6px 10px; border-radius: 6px; cursor: pointer; color: #1d1d1f; }
.tpl-item:hover { background: #f5f5f7; color: #0066cc; }
.tabs-header { display: flex; border-bottom: 1px solid #f2f2f7; gap: 16px; padding-bottom: 2px; }
.tabs-header span { font-size: 12px; font-weight: 600; color: #86868b; cursor: pointer; padding-bottom: 6px; }
.tabs-header span.active { color: #0066cc; font-weight: 700; position: relative; }
.tabs-header span.active::after { content: ''; position: absolute; bottom: -1px; left: 0; right: 0; height: 2px; background: #0066cc; }
.schema-designer-list { display: flex; flex-direction: column; gap: 8px; max-height: 250px; overflow-y: auto; }
.schema-row-card { background: #f5f5f7; padding: 10px; border-radius: 12px; display: flex; flex-direction: column; gap: 6px; }
.row-main { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }
.schema-row-card .name-input { width: 85px; font-weight: 700; }
.spirit-select { border: 1px solid #d2d2d7; border-radius: 6px; padding: 3px; font-size: 11px; background: #ffffff; outline: none; }
.range-box { display: flex; align-items: center; gap: 2px; font-size: 11px; color: #86868b; }
.min-max { width: 34px; text-align: center; font-size: 11px; padding: 2px; }
.reverse-label { display: flex; align-items: center; gap: 3px; font-size: 10px; color: #86868b; cursor: pointer; }
.formula-row { display: flex; align-items: center; gap: 6px; background: #ffffff; padding: 4px 8px; border-radius: 6px; border: 1px solid rgba(0,0,0,0.03); }
.fx-symbol { font-size: 11px; font-weight: 700; color: #0066cc; font-family: monospace; }
.formula-input { flex: 1; border: none; padding: 2px; font-size: 11px; background: transparent; font-family: monospace; }
.layers-manager-panel { display: flex; flex-direction: column; gap: 10px; }
.layers-scroll-box { display: flex; flex-direction: column; gap: 12px; max-height: 240px; overflow-y: auto; }
.layer-data-card { border: 1px solid #efeff4; padding: 12px; border-radius: 14px; background: #ffffff; }
.layer-card-header { display: flex; align-items: center; gap: 8px; border-bottom: 1px dashed #efeff4; padding-bottom: 6px; margin-bottom: 8px; }
.layer-name { font-weight: 700; width: 140px; }
.layer-color-picker { border: none; width: 24px; height: 24px; background: transparent; cursor: pointer; padding: 0; }
.layer-values-grid { display: flex; flex-direction: column; gap: 6px; }
.value-fill-row { display: flex; align-items: center; justify-content: space-between; gap: 12px; background: #fbfbfd; padding: 4px 8px; border-radius: 6px; }
.v-name { font-size: 11px; font-weight: 600; color: #3a3a3c; width: 80px; }
.v-num { width: 38px; text-align: center; font-size: 11px; padding: 2px; border: none; background: #f5f5f7; font-weight: 700; }
.spirit-input { border: 1px solid #d2d2d7; border-radius: 6px; padding: 4px; font-size: 12px; background: #ffffff; outline: none; color: #1d1d1f; }
.del-btn { background: none; border: none; color: #ff3b30; cursor: pointer; font-size: 11px; }
.spirit-mini-btn { background: #0066cc; color: #ffffff; border: none; padding: 4px 10px; border-radius: 6px; font-size: 11px; font-weight: 600; cursor: pointer; }
.spirit-mini-btn:hover { background: #0055b3; }
.spirit-add-btn { background: none; border: 1px dashed #0066cc; color: #0066cc; padding: 6px; border-radius: 8px; font-size: 11px; font-weight: 600; cursor: pointer; text-align: center; }
</style>