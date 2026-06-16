<template>
  <div class="spirit-block-wrapper">
    <div v-if="isTextNode(block.type)" class="block-text">
      {{ parsedText }}
    </div>

    <div v-else-if="block.type === 'image'" class="block-image-preview">
      <img :src="blockData.attrs?.src" alt="图片预览" class="preview-img" />
    </div>

    <div v-else-if="block.type === 'map-pin'" class="block-map-preview">
      <div class="pin-icon">📍</div>
      <div class="pin-info">
        <div class="pin-title">{{ blockData.title || '未知地点' }}</div>
        <div class="pin-coords">{{ blockData.lat?.toFixed(3) }}, {{ blockData.lng?.toFixed(3) }}</div>
      </div>
    </div>

    <div v-else-if="block.type === 'status'" :class="['block-status', `status-${blockData.state || 'default'}`]">
      <span class="status-dot"></span>
      {{ blockData.text || '状态' }}
    </div>

    <div v-else-if="block.type === 'excel-grid'" class="block-excel-preview">
      <div class="excel-preview-header">
        <span class="excel-icon">📊</span>
        <span class="excel-tag">SPREADSHEET</span>
      </div>
      <div class="excel-preview-body">
        <div class="preview-row" v-for="(row, rIdx) in tableShortCut" :key="rIdx">
          <span v-for="(cell, cIdx) in row" :key="cIdx" class="preview-cell">
            {{ cell }}
          </span>
        </div>
      </div>
    </div>

    <div v-else-if="!['canvas-node', 'canvas-edge', 'text', 'hardBreak', 'horizontalRule'].includes(block.type)" class="block-unknown">
      [未知能量块: {{ block.type }}]
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  block: any 
}>()

// 🌟 判断是否为可以提取文字的节点类型
const isTextNode = (type: string) => {
  return ['paragraph', 'heading', 'blockquote', 'bulletList', 'orderedList', 'taskList', 'codeBlock'].includes(type)
}

// 安全解析 JSON Data
const blockData = computed(() => {
  if (typeof props.block.data === 'string') {
    try { return JSON.parse(props.block.data) } catch { return {} }
  }
  return props.block.data || {}
})

// 🌟 终极递归文本提取引擎：不管列表/引用潜套多深，都能把纯文字榨取出来
const extractText = (nodes: any[]): string => {
  if (!nodes || !Array.isArray(nodes)) return ''
  return nodes.map(n => {
    if (n.text) return n.text
    if (n.content) return extractText(n.content) // 递归向下找
    return ''
  }).join(' ') // 用空格连接断开的文本
}

const parsedText = computed(() => {
  try {
    if (blockData.value.content) {
      return extractText(blockData.value.content)
    }
  } catch (e) {}
  return ''
})

// 🌟 新增：切出 Excel 的前 2 行 3 列用于白板卡片微缩视窗展示
const tableShortCut = computed(() => {
  try {
    if (blockData.value && blockData.value.cells && Array.isArray(blockData.value.cells)) {
      return blockData.value.cells.slice(0, 2).map((row: any) => {
        if (Array.isArray(row)) {
          return row.slice(0, 3).map(cell => (cell !== null && cell !== undefined ? cell : ''))
        }
        return ['', '', '']
      })
    }
  } catch (e) {}
  return [['(空表格)']]
})
</script>

<style scoped>
.spirit-block-wrapper {
  margin-bottom: 8px;
  width: 100%;
}

/* 文本样式 */
.block-text {
  font-size: 13px;
  color: #3a3a3c;
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 3; /* 卡片内最多显示3行文本 */
  -webkit-box-orient: vertical;
  overflow: hidden;
}

/* 🌟 新增：图片预览样式 */
.block-image-preview {
  width: 100%;
  border-radius: 8px;
  overflow: hidden;
  margin-top: 4px;
  border: 1px solid rgba(0, 0, 0, 0.05);
}

.preview-img {
  width: 100%;
  max-height: 140px; /* 限制最高高度，避免卡片过长 */
  object-fit: cover;
  display: block;
}

/* 地图图钉预览样式 */
.block-map-preview {
  display: flex;
  align-items: center;
  gap: 10px;
  background: #f5f5f7;
  padding: 8px 12px;
  border-radius: 8px;
  border: 1px solid #e5e5ea;
}
.pin-icon { font-size: 18px; }
.pin-title { font-size: 13px; font-weight: 600; color: #1d1d1f; }
.pin-coords { font-size: 11px; color: #86868b; margin-top: 2px; }

/* 状态标签样式 */
.block-status {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 8px;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 600;
  width: fit-content;
}
.status-default { background: #f2f2f7; color: #86868b; }
.status-processing { background: #e5f0ff; color: #0066cc; }
.status-done { background: #e8f5e9; color: #34c759; }
.status-dot { width: 6px; height: 6px; border-radius: 50%; background: currentColor; }

/* 🌟 新增：Excel 电子表格微缩 Apple 风格样式 */
.block-excel-preview {
  background: #f5f5f7;
  border: 1px solid #e5e5ea;
  border-radius: 8px;
  padding: 8px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-top: 4px;
}
.excel-preview-header {
  display: flex;
  align-items: center;
  gap: 4px;
}
.excel-icon { font-size: 12px; }
.excel-tag { font-size: 9px; font-weight: 700; color: #86868b; letter-spacing: 0.05em; }

.excel-preview-body {
  display: flex;
  flex-direction: column;
  gap: 2px;
  background: #ffffff;
  border: 1px solid rgba(0, 0, 0, 0.04);
  border-radius: 4px;
  padding: 4px;
  overflow: hidden;
}
.preview-row {
  display: flex;
  gap: 2px;
}
.preview-cell {
  flex: 1;
  font-size: 10px;
  color: #3a3a3c;
  background: #fafafa;
  padding: 2px;
  text-align: center;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  border: 0.5px solid #efeff4;
}

/* 兜底样式 */
.block-unknown {
  font-size: 11px;
  color: #ff3b30;
  background: #ffeeea;
  padding: 4px 8px;
  border-radius: 4px;
}
</style>