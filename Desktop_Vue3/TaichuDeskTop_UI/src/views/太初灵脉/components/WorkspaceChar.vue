<template>
  <div class="taichu-char-workspace">
    <!-- ========================================== -->
    <!-- 1. 顶部：高维度角色核心档案名片 -->
    <!-- ========================================== -->
    <header class="char-profile-section">
      <!-- 左侧：头像/立绘祭坛 -->
      <div class="avatar-altar-container">
        <div class="avatar-altar">
          <img v-if="localMeta.avatarUrl" :src="localMeta.avatarUrl" class="altar-image" alt="真容" />
          <div v-else class="altar-placeholder">👤</div>
          <button class="altar-upload-btn" @click="triggerAvatarUpload">
            <span>{{ localMeta.avatarUrl ? '重塑真容' : '上传立绘' }}</span>
          </button>
        </div>
      </div>

      <!-- 右侧：多维基础文字档案格 -->
      <div class="profile-inputs-grid">
        <div class="name-row-full">
          <input 
            :value="displayTitle" 
            @input="onNameInput" 
            class="char-name-input" 
            placeholder="姓名 / 尊号" 
          />
        </div>
        
        <div class="meta-inputs-flex">
          <div class="info-cell">
            <span class="cell-label">命格头衔</span>
            <input v-model="localMeta.identity" @input="syncAllToBlocks" class="cell-input" placeholder="如：混沌行者" />
          </div>
          <div class="info-cell">
            <span class="cell-label">所属阵营</span>
            <input v-model="localMeta.faction" @input="syncAllToBlocks" class="cell-input" placeholder="如：太初圣地" />
          </div>
          <div class="info-cell">
            <span class="cell-label">性别/形态</span>
            <input v-model="localMeta.gender" @input="syncAllToBlocks" class="cell-input" placeholder="如：男 / 灵体" />
          </div>
          <div class="info-cell">
            <span class="cell-label">寿元/年龄</span>
            <input v-model="localMeta.age" @input="syncAllToBlocks" class="cell-input" placeholder="如：3000岁" />
          </div>
          <div class="info-cell">
            <span class="cell-label">身高/体型</span>
            <input v-model="localMeta.height" @input="syncAllToBlocks" class="cell-input" placeholder="如：185cm" />
          </div>
          <div class="info-cell">
            <span class="cell-label">本命星象</span>
            <input v-model="localMeta.astrology" @input="syncAllToBlocks" class="cell-input" placeholder="如：紫微破军" />
          </div>
        </div>
      </div>
    </header>

    <!-- ========================================== -->
    <!-- 2. 中部双栏：左天道演化矩阵图组 vs 右天赋特质 -->
    <!-- ========================================== -->
    <div class="char-double-columns">
      <!-- 左栏：全新升级的自适应破格叠层雷达引擎与横向矩阵填报系统 -->
      <div class="column-card radar-panel-card">
        <div class="panel-header border-style">
          <div class="template-selector-bar">
            <span class="panel-title">📊 命途属性演化矩阵 ({{ totalAxes }}维)</span>
            <button @click="saveAsTemplate" class="action-ghost-btn">💾 存为骨架</button>
            <button @click="loadTemplateMenu = !loadTemplateMenu" class="action-ghost-btn">📋 唤入骨架</button>
          </div>
          <button @click="isRadarEditing = !isRadarEditing" class="spirit-mini-btn">
            {{ isRadarEditing ? '锁定矩阵配置' : '重塑演化矩阵' }}
          </button>
        </div>

        <!-- 骨架快选菜单 -->
        <div v-if="loadTemplateMenu" class="template-pop-menu">
          <div v-for="tpl in savedTemplates" :key="tpl.name" @click="applyTemplate(tpl)" class="tpl-item">{{ tpl.name }} ({{ tpl.schema.length }}维)</div>
          <div v-if="savedTemplates.length === 0" class="tpl-empty">本地暂无暂存骨架</div>
        </div>

        <!-- 雷达物理渲染视窗：放行 1.0 百分比上限，支持极致数据破格锐角刺穿外圈！ -->
        <div class="radar-render-box">
          <svg viewBox="0 0 360 260" class="radar-svg">
            <polygon v-for="scale in [1, 0.75, 0.5, 0.25]" :key="scale" :points="getGridPoints(scale)" class="radar-grid-line" />
            <line v-for="(axis, index) in axisLines" :key="'axis-'+index" :x1="centerX" :y1="centerY" :x2="axis.x" :y2="axis.y" class="radar-axis" />
            
            <g v-for="(layer, lIdx) in localLayers" :key="'layer-'+lIdx" class="radar-layer-group">
              <polygon :points="getLayerValuePoints(layer)" class="radar-value-area" :style="{ '--layer-color': layer.color }" />
              <circle v-for="(point, pIdx) in getLayerDataPoints(layer)" :key="'p-'+pIdx" :cx="point.x" :cy="point.y" r="3.5" class="radar-value-dot" :style="{ stroke: layer.color }" />
            </g>
            
            <text v-for="(label, index) in labelPositions" :key="'label-'+index" :x="label.x" :y="label.y" :text-anchor="label.anchor" class="radar-label">
              {{ label.name }} ({{ formatNumber(getDynamicMaxLimit(label.schemaItem)) }})
            </text>
          </svg>
        </div>

        <!-- 常态闭合下的流层气泡图例 -->
        <div v-if="!isRadarEditing" class="static-layers-legends inline-card">
          <div v-for="layer in localLayers" :key="layer.name" class="legend-tag">
            <span class="legend-color-dot" :style="{ backgroundColor: layer.color }"></span>
            <span class="legend-name">{{ layer.name }}</span>
          </div>
        </div>

        <!-- 操控台设计视窗 -->
        <div v-if="isRadarEditing" class="radar-designer-container">
          <div class="tabs-header">
            <span :class="{ active: radarTab === 'schema' }" @click="radarTab = 'schema'">1. 维度与公式大纲</span>
            <span :class="{ active: radarTab === 'data' }" @click="radarTab = 'data'">2. 横向矩阵数据填报</span>
          </div>

          <!-- 操控子面板 A：公式与指标骨架设计 -->
          <div v-if="radarTab === 'schema'" class="schema-designer-inner-list">
            <div v-for="(item, idx) in localSchema" :key="idx" class="schema-row-card-inner">
              <div class="row-main-flex">
                <input v-model="item.name" class="editor-inline-input name-field-large" placeholder="属性名" @input="syncAllToBlocks" />
                <select v-model="item.type" class="spirit-select-mini" @change="syncAllToBlocks">
                  <option value="base">滑块维度</option>
                  <option value="raw_counter">寄存池(不上墙)</option>
                  <option value="computed">公式衍生</option>
                </select>
                <div class="range-inputs-mini" v-if="item.type !== 'raw_counter'">
                  <input v-model="item.min" class="editor-inline-input bound-field-mini" placeholder="0" @input="syncAllToBlocks" />
                  <span>~</span>
                  <input v-model="item.max" class="editor-inline-input bound-field-mini" placeholder="AUTO" @input="syncAllToBlocks" />
                </div>
                <label v-if="item.type !== 'raw_counter'" class="reverse-label-mini">
                  <input type="checkbox" v-model="item.reverse" @change="syncAllToBlocks" /> <span>逆收益</span>
                </label>
                <button @click="removeSchemaItem(idx)" class="delete-inline-btn" :disabled="localSchema.length <= 3">✕</button>
              </div>
              <div v-if="item.type === 'computed'" class="formula-row-inner">
                <span class="fx-symbol">ƒ(x) =</span>
                <input v-model="item.formula" class="editor-inline-input formula-input-inner" placeholder="公式如: [力量] * 1.5" @input="syncAllToBlocks" />
              </div>
            </div>
            <button @click="addNewSchemaItem" class="add-dim-btn">+ 肆意扩张新维度属性</button>
          </div>

          <!-- 操控子面板 B：颠覆式横向 Excel 矩阵式无滚轮快速填报网格 -->
          <div v-if="radarTab === 'data'" class="matrix-grid-inner-panel">
            <button @click="addNewLayer" class="add-dim-btn style-solid">+ 追加对比叠层(如变身流派/状态备份)</button>
            <div class="matrix-grid-scroll-container">
              <table class="matrix-grid-table">
                <thead>
                  <tr>
                    <th class="sticky-col-header">属性维度 \ 状态</th>
                    <th v-for="(layer, lIdx) in localLayers" :key="'th-'+lIdx">
                      <div class="th-input-wrapper">
                        <input v-model="layer.color" type="color" class="matrix-color-picker" @change="syncAllToBlocks" />
                        <input v-model="layer.name" class="matrix-th-input" @input="syncAllToBlocks" />
                        <button @click="removeLayer(lIdx)" class="matrix-del-col" :disabled="localLayers.length<=1">✕</button>
                      </div>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="schemaItem in localSchema" :key="schemaItem.name">
                    <td class="sticky-col-dim"><span class="dim-badge" :class="schemaItem.type">{{ schemaItem.name }}</span></td>
                    <td v-for="(layer, lIdx) in localLayers" :key="'td-'+lIdx+'-'+schemaItem.name">
                      <!-- 原生输入滑块 -->
                      <div v-if="schemaItem.type === 'base'" class="matrix-cell-flex">
                        <input type="range" :min="schemaItem.min" :max="getDynamicMaxLimit(schemaItem)" step="any" v-model="layer.values[schemaItem.name]" @input="syncAllToBlocks" class="matrix-slider" />
                        <input type="text" v-model="layer.values[schemaItem.name]" class="matrix-num-input" @input="syncAllToBlocks" />
                      </div>
                      <!-- 寄存池流水快速累计 -->
                      <div v-else-if="schemaItem.type === 'raw_counter'" class="matrix-cell-counter">
                        <div class="counter-actions-row">
                          <button @click="quickAdjustCounter(layer, schemaItem.name, 1)" class="matrix-step-btn">+1</button>
                          <button @click="quickAdjustCounter(layer, schemaItem.name, -1)" class="matrix-step-btn">-1</button>
                          <input type="text" v-model="layer.values[schemaItem.name]" class="matrix-num-input plain" @input="syncAllToBlocks" />
                        </div>
                        <input type="number" placeholder="回车追加累计..." class="matrix-append-input" @keyup.enter="appendMatchStream($event, layer, schemaItem.name)" />
                      </div>
                      <!-- 天道衍生公式展示 -->
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
      </div>

      <!-- 右栏：天赋玄功 & 宿命特质  -->
      <div class="column-card traits-panel-card">
        <div class="panel-header">
          <span class="panel-title">⚡ 天赋玄功 & 宿命特质</span> 
          <button @click="addNewTrait" class="spirit-mini-btn">+ 添加特质</button> 
        </div>
        
        <div class="traits-scroll-area"> 
          <div v-for="(trait, idx) in localTraits" :key="idx" class="trait-node"> 
            <div class="trait-title-row"> 
              <input v-model="trait.title" @input="syncAllToBlocks" class="trait-title-input" placeholder="功法/特质名称" /> 
              <button @click="removeTrait(idx)" class="delete-inline-btn">✕</button> 
            </div>
            <textarea v-model="trait.desc" @input="syncAllToBlocks" class="trait-desc-textarea" placeholder="输入关于此天赋或功法的具体效果机理..." rows="2" /> 
          </div>
          <div v-if="localTraits.length === 0" class="empty-placeholder-tip">暂无特质描述，点击右上角赐予功法</div> 
        </div>
      </div>
    </div>

    <!-- ========================================== -->
    <!-- 3. 下部：天道因果羁绊（人物关系网络）  -->
    <!-- ========================================== -->
    <section class="char-relations-section"> 
      <div class="panel-header border-style"> 
        <span class="panel-title">🔗 天道因果羁绊（人物关系）</span> 
        <button @click="addNewRelation" class="spirit-mini-btn">+ 添加羁绊</button> 
      </div>

      <div class="relations-matrix-grid"> 
        <div v-for="(rel, idx) in localRelations" :key="idx" class="relation-card"> 
          <div class="rel-top-bar"> 
            <input v-model="rel.targetName" @input="syncAllToBlocks" class="rel-name-input" placeholder="目标角色姓名" /> 
            <input v-model="rel.type" @input="syncAllToBlocks" class="rel-type-input" placeholder="关系类型 (如: 师徒)" /> 
            <button @click="removeRelation(idx)" class="delete-inline-btn">✕</button> 
          </div>
          <textarea v-model="rel.description" @input="syncAllToBlocks" class="rel-desc-textarea" placeholder="描述此段因果的具体由来的纠葛..." rows="2" /> 
        </div>
      </div>
      <div v-if="localRelations.length === 0" class="empty-placeholder-tip text-center">命途清澈，暂无天道因果羁绊纠葛</div> 
    </section>

    <!-- ========================================== -->
    <!-- 4. 底部：核心生平传记（主长文富文本编辑器）  -->
    <!-- ========================================== -->
    <section class="char-biography-section"> 
      <div class="panel-header border-style"> 
        <span class="panel-title">📜 岁月生平履历详纪</span> 
      </div>
      <div class="biography-editor-wrapper"> 
        <slot name="editor"></slot> 
      </div>
    </section>

    <input ref="fileInputRef" type="file" accept="image/*" style="display: none" @change="handleAvatarSelected" /> 
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import { useSpiritData } from '@/composables/useSpiritData'; 
import { useCos } from '@/composables/useCos'; 

interface SchemaItem { name: string; type: 'base' | 'raw_counter' | 'computed'; min: number | string; max: number | string; reverse: boolean; formula?: string; }
interface LayerItem { name: string; color: string; values: Record<string, number | string>; }
interface TraitItem { title: string; desc: string; } 
interface RelationItem { targetName: string; type: string; description: string; } 
interface FullCharMeta {
  avatarUrl: string; identity: string; faction: string;
  gender: string; age: string; height: string; astrology: string;
} 

// 🌟 核心突破：将所有具名属性全部加 "?" 松绑为非必填的可选参数！
// 这样画布分发器无论是只传 block，还是在任意场景残缺传递，TS 插件都会一路放行！
const props = defineProps<{ 
  title?: string; 
  noteId?: string; 
  extraData?: string; 
  blocks?: any[]; 
}>(); 

const emit = defineEmits(['update:title', 'change']); 

const { activeNote } = useSpiritData(); 
const { uploadFile } = useCos(); 

// 状态控制层
const isRadarEditing = ref(false);
const radarTab = ref<'schema' | 'data'>('schema');
const loadTemplateMenu = ref(false);
const savedTemplates = ref<any[]>([]);
const fileInputRef = ref<HTMLInputElement>(); 
let isInitialized = false; 
let saveTimer: any = null; 

// 🌟 智能自适应网关：若外层无显式传参（如画布内调用），组件自动向全局主内存树 activeNote 里锚定捞取
const displayTitle = computed(() => props.title || activeNote.value?.title || '未命名碎片');
const displayNoteId = computed(() => props.noteId || activeNote.value?.id || '');

const localMeta = ref<FullCharMeta>({
  avatarUrl: '', identity: '', faction: '',
  gender: '', age: '', height: '', astrology: ''
}); 

const localSchema = ref<SchemaItem[]>([]);
const localLayers = ref<LayerItem[]>([]);
const localTraits = ref<TraitItem[]>([]); 
const localRelations = ref<RelationItem[]>([]); 

// SVG 常数几何网络空间
const width = 360; const height = 260; const centerX = width / 2; const centerY = height / 2; const radius = 75;

const visibleSchema = computed(() => localSchema.value.filter(s => s.type !== 'raw_counter'));
const totalAxes = computed(() => visibleSchema.value.length);
const angles = computed<number[]>(() => {
  const total = totalAxes.value;
  return visibleSchema.value.map((_, i) => (i * 2 * Math.PI) / total - Math.PI / 2);
});

const getGridPoints = (scale: number): string => {
  const r = radius * scale;
  return angles.value.map((angle) => `${centerX + r * Math.cos(angle)},${centerY + r * Math.sin(angle)}`).join(' ');
};

const axisLines = computed(() => angles.value.map((angle) => ({
  x: centerX + radius * Math.cos(angle), y: centerY + radius * Math.sin(angle)
})));

// 公式解构执行单元
const calculateFormula = (item: SchemaItem, layer: LayerItem): number => {
  if (!item.formula) return 0;
  let expr = item.formula;
  localSchema.value.forEach(s => {
    if (s.type === 'base' || s.type === 'raw_counter') {
      const val = parseFloat(layer.values[s.name] as string) || 0;
      expr = expr.replace(new RegExp(`\\[${s.name}\\]`, 'g'), String(val));
    }
  });
  try {
    const cleanExpr = expr.replace(/[^0-9.+\-*/() ]/g, '');
    const result = Function(`"use strict"; return (${cleanExpr})`)();
    return isFinite(result) ? result : 0;
  } catch { return 0; }
};

const getLayerValue = (item: SchemaItem, layer: LayerItem): number => {
  return item.type === 'computed' ? calculateFormula(item, layer) : (parseFloat(layer.values[item.name] as string) || 0);
};

const getDynamicMaxLimit = (item: SchemaItem): number => {
  if (String(item.max).trim().toUpperCase() === 'AUTO') {
    if (localLayers.value.length === 0) return 100;
    const allLayerValues = localLayers.value.map(l => getLayerValue(item, l));
    const currentMax = Math.max(...allLayerValues);
    return currentMax === 0 ? 100 : currentMax;
  }
  return parseFloat(item.max as string) || 100;
};

// 破格数据映射网络：支持极致数据破格锐角刺穿外圈！
const getLayerDataPoints = (layer: LayerItem) => {
  return visibleSchema.value.map((item, i) => {
    const angle = angles.value[i];
    const currVal = getLayerValue(item, layer);
    const minVal = parseFloat(item.min as string) || 0;
    const maxVal = getDynamicMaxLimit(item);
    const range = maxVal - minVal;
    
    let percent = range === 0 ? 0 : (currVal - minVal) / range;
    if (item.reverse) percent = range === 0 ? 1 : (maxVal - currVal) / range;
    
    const breakOutPercent = Math.max(0, percent);
    return {
      x: centerX + radius * breakOutPercent * Math.cos(angle),
      y: centerY + radius * breakOutPercent * Math.sin(angle)
    };
  });
};

const getLayerValuePoints = (layer: LayerItem): string => getLayerDataPoints(layer).map(p => `${p.x},${p.y}`).join(' ');

const labelPositions = computed(() => {
  const offsetRadius = radius + 15;
  return visibleSchema.value.map((item, i) => {
    const angle = angles.value[i];
    let anchor = 'middle';
    if (Math.cos(angle) > 0.15) anchor = 'start';
    else if (Math.cos(angle) < -0.15) anchor = 'end';
    return { x: centerX + offsetRadius * Math.cos(angle), y: centerY + offsetRadius * Math.sin(angle) + 4, name: item.name, anchor, schemaItem: item };
  });
});

const formatNumber = (val: number | string): string => {
  const num = parseFloat(val as string);
  return isNaN(num) ? '0' : parseFloat(num.toFixed(2)).toString();
};

// 恢复全量积木包数据
const loadFromBlocks = () => {
  const note = activeNote.value as any; 
  if (!note || !note.blocks) return; 

  const charLayoutBlock = note.blocks.find((b: any) => b.type === 'char-layout-block'); 
  if (charLayoutBlock?.data) { 
    try {
      const parsed = JSON.parse(charLayoutBlock.data); 
      if (parsed.meta) localMeta.value = { ...localMeta.value, ...parsed.meta }; 
      if (parsed.traits && Array.isArray(parsed.traits)) localTraits.value = parsed.traits; 
      if (parsed.relations && Array.isArray(parsed.relations)) localRelations.value = parsed.relations; 
      
      if (parsed.schema && Array.isArray(parsed.schema)) {
        localSchema.value = parsed.schema;
        localLayers.value = parsed.layers || [];
      } else {
        localSchema.value = [
          { name: '力量', type: 'base', min: 0, max: 100, reverse: false },
          { name: '速度', type: 'base', min: 0, max: 100, reverse: false },
          { name: '智力', type: 'base', min: 0, max: 100, reverse: false }
        ];
        localLayers.value = [{ name: '初始状态流层', color: '#0066cc', values: { '力量': 70, '速度': 60, '智力': 80 } }];
      }
    } catch (e) {
      console.warn("解析角色积木块异常", e); 
    }
  } else {
    localSchema.value = [
      { name: '力量', type: 'base', min: 0, max: 100, reverse: false },
      { name: '速度', type: 'base', min: 0, max: 100, reverse: false },
      { name: '智力', type: 'base', min: 0, max: 100, reverse: false }
    ];
    localLayers.value = [{ name: '初始状态流层', color: '#0066cc', values: { '力量': 70, '速度': 60, '智力': 80 } }];
  }
};

const syncAllToBlocks = () => {
  if (!isInitialized) return; 
  const note = activeNote.value as any; 
  if (!note) return; 

  const charCardPayload = {
    meta: localMeta.value,
    schema: localSchema.value,
    layers: localLayers.value,
    traits: localTraits.value, 
    relations: localRelations.value 
  };

  const currentBlocks = note.blocks || []; 
  const textBiographyBlocks = currentBlocks.filter((b: any) => b.type !== 'char-layout-block'); 

  const updatedCharMetaBlock = {
    id: `char_meta_block_${displayNoteId.value}`, 
    ownerId: displayNoteId.value, 
    ownerType: 'char',
    type: 'char-layout-block',
    data: JSON.stringify(charCardPayload),
    sortOrder: 999
  };

  const mergedBlocks = [...textBiographyBlocks, updatedCharMetaBlock]; 
  note.blocks = mergedBlocks; 

  if (saveTimer) clearTimeout(saveTimer); 
  saveTimer = setTimeout(() => {
    emit('change', { 
      blocks: mergedBlocks, 
      type: 'char-gallery'
    });
  }, 400); 
};

const onNameInput = (e: Event) => {
  const val = (e.target as HTMLInputElement).value; 
  emit('update:title', val); 
  if (activeNote.value) activeNote.value.title = val; 
  syncAllToBlocks(); 
};

const toggleEditMode = () => { isRadarEditing.value = !isRadarEditing.value; };
const quickAdjustCounter = (layer: LayerItem, name: string, step: number) => { const current = parseFloat(layer.values[name] as string) || 0; layer.values[name] = Math.max(0, current + step); syncAllToBlocks(); };
const appendMatchStream = (event: Event, layer: LayerItem, counterName: string) => {
  const target = event.target as HTMLInputElement; const inputVal = parseFloat(target.value); if (isNaN(inputVal)) return;
  const previousTotal = parseFloat(layer.values[counterName] as string) || 0; layer.values[counterName] = previousTotal + inputVal;
  const matchCountSchema = localSchema.value.find(s => s.name.includes('场次') && s.type === 'raw_counter');
  if (matchCountSchema) {
    const prevCount = parseFloat(layer.values[matchCountSchema.name] as string) || 0; layer.values[matchCountSchema.name] = prevCount + 1;
  }
  target.value = ''; syncAllToBlocks();
};

const addNewSchemaItem = () => { localSchema.value.push({ name: `新核心维度${localSchema.value.length+1}`, type: 'base', min: 0, max: 100, reverse: false }); syncAllToBlocks(); };
const removeSchemaItem = (idx: number) => { localSchema.value.splice(idx, 1); syncAllToBlocks(); };
const addNewLayer = () => { const defaultValues: Record<string, any> = {}; localSchema.value.forEach(s => { defaultValues[s.name] = s.min ?? 0 }); localLayers.value.push({ name: `新叠层形态${localLayers.value.length+1}`, color: '#e63946', values: defaultValues }); syncAllToBlocks(); };
const removeLayer = (idx: number) => { localLayers.value.splice(idx, 1); syncAllToBlocks(); };

const saveAsTemplate = () => { const name = prompt('输入骨架模板名称:', '武力量纲骨架'); if (!name) return; const currentPool = JSON.parse(localStorage.getItem('spirit_radar_templates') || '[]'); currentPool.push({ name, schema: JSON.parse(JSON.stringify(localSchema.value)) }); localStorage.setItem('spirit_radar_templates', JSON.stringify(currentPool)); loadTemplatesFromStorage(); };
const applyTemplate = (tpl: any) => { localSchema.value = JSON.parse(JSON.stringify(tpl.schema)); localLayers.value.forEach(layer => { const newValues: Record<string, any> = {}; localSchema.value.forEach(s => { newValues[s.name] = layer.values[s.name] ?? s.min }); layer.values = newValues }); loadTemplateMenu.value = false; syncAllToBlocks(); };
const loadTemplatesFromStorage = () => { savedTemplates.value = JSON.parse(localStorage.getItem('spirit_radar_templates') || '[]'); };

const addNewTrait = () => { localTraits.value.push({ title: '新功法特质', desc: '' }); syncAllToBlocks(); }; 
const removeTrait = (idx: number) => { localTraits.value.splice(idx, 1); syncAllToBlocks(); }; 
const addNewRelation = () => { localRelations.value.push({ targetName: '', type: '', description: '' }); syncAllToBlocks(); }; 
const removeRelation = (idx: number) => { localRelations.value.splice(idx, 1); syncAllToBlocks(); }; 

const triggerAvatarUpload = () => { fileInputRef.value?.click(); }; 
const handleAvatarSelected = async (e: Event) => {
  const input = e.target as HTMLInputElement; 
  const file = input.files?.[0]; 
  if (!file || !file.type.startsWith('image/')) return; 
  try {
    const result = await uploadFile(file, 'char_avatar'); 
    if (result?.url) { 
      localMeta.value.avatarUrl = result.url; 
      syncAllToBlocks(); 
    }
  } catch (err) { console.error(err); } 
};

watch(() => activeNote.value?.id, (newId) => { 
  if (newId) { 
    isInitialized = false; 
    loadFromBlocks(); 
    isInitialized = true; 
  }
});

onMounted(() => {
  loadTemplatesFromStorage();
  if (activeNote.value) { 
    loadFromBlocks(); 
    isInitialized = true; 
  }
});
</script>

<style scoped>
.taichu-char-workspace { max-width: 960px; margin: 0 auto; padding: 32px 24px 120px; background: #ffffff; display: flex; flex-direction: column; gap: 36px; } 
.panel-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px; } 
.panel-header.border-style { border-bottom: 1px solid #f2f2f7; padding-bottom: 8px; margin-bottom: 16px; } 
.panel-title { font-size: 14px; font-weight: 700; color: #1d1d1f; } 
.char-profile-section { display: flex; align-items: flex-start; gap: 32px; background: #fbfbfd; border: 1px solid rgba(0,0,0,0.03); padding: 24px; border-radius: 20px; } 
.avatar-altar-container { flex-shrink: 0; } 
.avatar-altar { position: relative; width: 120px; height: 160px; background: #e8e8ed; border-radius: 14px; overflow: hidden; display: flex; align-items: center; justify-content: center; } 
.altar-image { width: 100%; height: 100%; object-fit: cover; } 
.altar-placeholder { font-size: 48px; color: #aeaea3; } 
.altar-upload-btn { position: absolute; inset: 0; background: rgba(0,0,0,0.5); color: #ffffff; border: none; font-size: 12px; font-weight: 600; cursor: pointer; opacity: 0; display: flex; align-items: center; justify-content: center; transition: opacity 0.2s ease; } 
.avatar-altar:hover .altar-upload-btn { opacity: 1; } 
.profile-inputs-grid { flex: 1; display: flex; flex-direction: column; gap: 16px; } 
.char-name-input { width: 100%; font-size: 2.2rem; font-weight: 800; color: #1d1d1f; border: none; background: transparent; outline: none; border-bottom: 1px solid #e5e5ea; padding-bottom: 4px; } 
.meta-inputs-flex { display: grid; grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); gap: 12px; } 
.info-cell { background: #ffffff; border: 1px solid #e5e5ea; border-radius: 10px; padding: 6px 12px; display: flex; flex-direction: column; } 
.cell-label { font-size: 10px; font-weight: 700; color: #86868b; text-transform: uppercase; margin-bottom: 2px; } 
.cell-input { border: none; background: transparent; outline: none; font-size: 13px; color: #1d1d1f; font-weight: 600; } 
.char-double-columns { display: grid; grid-template-columns: 1fr; gap: 24px; } 
@media (min-width: 768px) { .char-double-columns { grid-template-columns: 4.8fr 5.2fr; } }
.column-card { background: #ffffff; border: 1px solid rgba(0,0,0,0.06); border-radius: 20px; padding: 20px; display: flex; flex-direction: column; } 

.radar-render-box { display: flex; justify-content: center; align-items: center; padding: 10px 0; } 
.radar-svg { width: 100%; max-width: 290px; height: auto; overflow: visible; }
.radar-grid-line { fill: none; stroke: #e5e5ea; stroke-width: 0.8; } 
.radar-axis { stroke: rgba(0,0,0,0.03); stroke-width: 1; stroke-dasharray: 2 2; } 
.radar-value-area { fill: var(--layer-color); fill-opacity: 0.1; stroke: var(--layer-color); stroke-width: 2.2; stroke-linejoin: round; }
.radar-value-dot { fill: #ffffff; stroke-width: 2; }
.radar-label { font-size: 10px; font-weight: 700; fill: #1d1d1f; }

/* 模板与设计面板控制 */
.template-selector-bar { display: flex; align-items: center; gap: 8px; }
.action-ghost-btn { background: none; border: none; color: #0066cc; font-size: 11px; font-weight: 600; cursor: pointer; padding: 2px 6px; border-radius: 4px; }
.action-ghost-btn:hover { background: rgba(0, 102, 204, 0.05); }
.template-pop-menu { position: absolute; top: 40px; left: 20px; background: #ffffff; border: 1px solid #e5e5ea; border-radius: 10px; box-shadow: 0 8px 24px rgba(0,0,0,0.08); padding: 6px; z-index: 50; width: 180px; }
.tpl-item { font-size: 12px; padding: 6px 10px; border-radius: 6px; cursor: pointer; color: #1d1d1f; }
.tpl-item:hover { background: #f5f5f7; color: #0066cc; }
.tpl-empty { font-size: 11px; color: #c7c7cc; text-align: center; padding: 10px; }

.radar-designer-container { display: flex; flex-direction: column; gap: 12px; margin-top: 12px; }
.tabs-header { display: flex; border-bottom: 1px solid #f2f2f7; gap: 16px; padding-bottom: 2px; }
.tabs-header span { font-size: 12px; font-weight: 600; color: #86868b; cursor: pointer; padding-bottom: 6px; }
.tabs-header span.active { color: #0066cc; font-weight: 700; position: relative; }
.tabs-header span.active::after { content: ''; position: absolute; bottom: -1px; left: 0; right: 0; height: 2px; background: #0066cc; }

.schema-designer-inner-list { display: flex; flex-direction: column; gap: 8px; max-height: 260px; overflow-y: auto; }
.schema-row-card-inner { background: #f5f5f7; padding: 10px; border-radius: 12px; display: flex; flex-direction: column; gap: 6px; }
.row-main-flex { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }
.name-field-large { width: 85px; font-weight: 700; }
.spirit-select-mini { border: 1px solid #d2d2d7; border-radius: 6px; padding: 2px; font-size: 11px; background: #ffffff; outline: none; }
.range-inputs-mini { display: flex; align-items: center; gap: 2px; font-size: 11px; color: #86868b; }
.bound-field-mini { width: 34px; text-align: center; font-size: 11px; padding: 2px; }
.reverse-label-mini { display: flex; align-items: center; gap: 2px; font-size: 10px; color: #86868b; cursor: pointer; }
.formula-row-inner { display: flex; align-items: center; gap: 6px; background: #ffffff; padding: 4px 8px; border-radius: 6px; border: 1px solid rgba(0,0,0,0.02); }
.fx-symbol { font-size: 11px; font-weight: 700; color: #0066cc; font-family: monospace; }
.formula-input-inner { flex: 1; border: none; padding: 2px; font-size: 11px; background: transparent; font-family: monospace; }

/* 🌟 横向表格网格系统布局 */
.matrix-grid-inner-panel { display: flex; flex-direction: column; gap: 10px; }
.matrix-grid-scroll-container { width: 100%; overflow-x: auto; border: 1px solid #e5e5ea; border-radius: 12px; background: #ffffff; }
.matrix-grid-table { width: 100%; border-collapse: collapse; font-size: 12px; text-align: left; }
.matrix-grid-table th { background: #f5f5f7; padding: 8px 12px; border-bottom: 2px solid #e5e5ea; border-right: 1px solid #efeff4; min-width: 160px; }
.matrix-grid-table th.sticky-col-header { position: sticky; left: 0; z-index: 5; background: #e8e8ed; min-width: 90px; font-weight: 700; }
.matrix-grid-table td { padding: 6px 10px; border-bottom: 1px solid #efeff4; border-right: 1px solid #efeff4; vertical-align: middle; }
.matrix-grid-table td.sticky-col-dim { position: sticky; left: 0; z-index: 4; background: #fbfbfd; font-weight: 700; border-right: 2px solid #e5e5ea; min-width: 90px; }

.dim-badge { font-size: 10px; padding: 1px 4px; border-radius: 3px; display: inline-block; }
.dim-badge.base { background: rgba(0, 102, 204, 0.06); color: #0066cc; }
.dim-badge.raw_counter { background: rgba(142, 142, 147, 0.1); color: #555559; }
.dim-badge.computed { background: rgba(52, 199, 89, 0.08); color: #24b249; }

.th-input-wrapper { display: flex; align-items: center; gap: 4px; }
.matrix-color-picker { border: none; background: transparent; width: 16px; height: 18px; cursor: pointer; padding: 0; }
.matrix-th-input { border: none; background: transparent; font-weight: 700; font-size: 12px; outline: none; flex: 1; width: 50px; }
.matrix-del-col { background: none; border: none; color: #ff3b30; cursor: pointer; font-size: 10px; }
.matrix-cell-flex { display: flex; align-items: center; gap: 4px; }
.matrix-slider { flex: 1; height: 3px; accent-color: #0066cc; }
.matrix-num-input { width: 34px; font-size: 11px; text-align: center; border: 1px solid #d2d2d7; border-radius: 4px; padding: 1px; }
.matrix-num-input.plain { border: none; background: #f5f5f7; font-weight: 700; }
.matrix-cell-counter { display: flex; flex-direction: column; gap: 3px; }
.counter-actions-row { display: flex; align-items: center; gap: 2px; }
.matrix-step-btn { background: #e8e8ed; border: none; font-size: 9px; font-weight: 700; width: 18px; height: 16px; border-radius: 3px; cursor: pointer; }
.matrix-append-input { font-size: 10px; padding: 2px 4px; border: 1px solid #0066cc; border-radius: 4px; color: #0066cc; outline: none; width: 100%; box-sizing: border-box; }
.matrix-cell-computed { font-weight: 700; color: #0066cc; background: rgba(0,102,204,0.02); padding: 2px; border-radius: 4px; text-align: center; }

.static-layers-legends { display: flex; flex-wrap: wrap; gap: 8px; background: #f5f5f7; padding: 6px 12px; border-radius: 10px; margin-top: 6px; }
.legend-tag { display: flex; align-items: center; gap: 6px; font-size: 11px; font-weight: 600; color: #1d1d1f; }
.legend-color-dot { width: 8px; height: 8px; border-radius: 50%; display: inline-block; }

.static-field-name { font-size: 12px; font-weight: 700; color: #3a3a3c; width: 45px; } 
.editor-inline-input { border: 1px solid #d2d2d7; border-radius: 4px; padding: 2px 4px; font-size: 11px; outline: none; background: #ffffff; } 
.delete-inline-btn { background: none; border: none; color: #ff3b30; cursor: pointer; font-size: 11px; } 
.add-dim-btn { background: none; border: 1px dashed #0066cc; color: #0066cc; padding: 5px; border-radius: 6px; font-size: 11px; font-weight: 600; cursor: pointer; text-align: center; width: 100%; }
.add-dim-btn.style-solid { border-style: solid; background: rgba(0,102,204,0.03); margin-bottom: 4px; }

.traits-scroll-area { flex: 1; overflow-y: auto; max-height: 380px; display: flex; flex-direction: column; gap: 12px; } 
.trait-node { background: #f5f5f7; border-radius: 12px; padding: 12px; display: flex; flex-direction: column; gap: 6px; } 
.trait-title-row { display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid rgba(0,0,0,0.04); padding-bottom: 4px; } 
.trait-title-input { border: none; background: transparent; font-size: 13px; font-weight: 700; color: #1d1d1f; outline: none; width: 80%; } 
.trait-desc-textarea { border: none; background: transparent; font-size: 12px; line-height: 1.5; color: #515154; resize: vertical; outline: none; font-family: inherit; } 
.relations-matrix-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 16px; } 
.relation-card { background: #fbfbfd; border: 1px solid #e5e5ea; border-radius: 14px; padding: 12px; display: flex; flex-direction: column; gap: 8px; } 
.rel-top-bar { display: flex; align-items: center; justify-content: space-between; border-bottom: 1px dashed #e5e5ea; padding-bottom: 4px; gap: 4px; } 
.rel-name-input { border: none; background: transparent; font-size: 13px; font-weight: 700; color: #1d1d1f; outline: none; width: 45%; } 
.rel-type-input { border: none; background: rgba(0, 102, 204, 0.05); color: #0066cc; font-size: 10px; font-weight: 700; padding: 2px 6px; border-radius: 4px; outline: none; width: 40%; text-align: center; } 
.rel-desc-textarea { border: none; background: transparent; font-size: 12px; color: #515154; line-height: 1.4; resize: none; outline: none; font-family: inherit; } 
.biography-editor-wrapper { background: #ffffff; min-height: 300px; } 
.spirit-mini-btn { background: rgba(0, 102, 204, 0.06); color: #0066cc; border: none; padding: 4px 10px; border-radius: 6px; font-size: 11px; font-weight: 600; cursor: pointer; } 
.empty-placeholder-tip { font-size: 12px; color: #c7c7cc; padding: 20px 0; text-align: center; font-style: italic; } 
</style>