<template>
  <node-view-wrapper class="pdf-embed-wrapper">
    <div class="pdf-header">
      <span class="pdf-icon">📄</span>
      <span class="pdf-name">{{ node.attrs.fileName || 'PDF 文档' }}</span>
      <div class="pdf-actions">
        <a :href="node.attrs.url" target="_blank" class="pdf-btn">打开原文件</a>
        <button class="pdf-btn danger" @click="deleteSelf">移除</button>
      </div>
    </div>
    <div class="pdf-body" :style="{ height: node.attrs.height || '600px' }">
      <iframe
        v-if="node.attrs.url"
        :src="node.attrs.url"
        frameborder="0"
        width="100%"
        height="100%"
      ></iframe>
      <div v-else class="pdf-empty">PDF 加载中…</div>
    </div>
  </node-view-wrapper>
</template>

<script setup lang="ts">
import { NodeViewWrapper, nodeViewProps } from '@tiptap/vue-3'

const props = defineProps(nodeViewProps)

const deleteSelf = () => {
  props.deleteNode()
}
</script>

<style scoped>
.pdf-embed-wrapper {
  margin: 16px 0;
  border: 1px solid #e5e5ea;
  border-radius: 12px;
  overflow: hidden;
  background: #ffffff;
}
.pdf-header {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 16px;
  background: #fbfbfd;
  border-bottom: 1px solid #e5e5ea;
  font-size: 13px;
}
.pdf-icon { font-size: 16px; }
.pdf-name {
  flex: 1;
  font-weight: 600;
  color: #1d1d1f;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.pdf-actions { display: flex; gap: 8px; }
.pdf-btn {
  padding: 4px 10px;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 500;
  text-decoration: none;
  color: #0066cc;
  background: rgba(0, 102, 204, 0.08);
  border: none;
  cursor: pointer;
  transition: all 0.15s;
}
.pdf-btn:hover { background: rgba(0, 102, 204, 0.15); }
.pdf-btn.danger { color: #ff3b30; background: rgba(255, 59, 48, 0.08); }
.pdf-btn.danger:hover { background: rgba(255, 59, 48, 0.15); }
.pdf-body {
  width: 100%;
  background: #f5f5f7;
  position: relative;
}
.pdf-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #c7c7cc;
  font-size: 13px;
}
</style>