<!-- src/components/KnowledgeGraph/GraphDetailsPanel.vue -->
<template>
  <Transition name="slide-right">
    <div v-if="show && node" class="details-panel" @click.stop>
      <!-- 头部区域优化：增强发光质感与层级 -->
      <div class="panel-header">
        <div class="title-area">
          <h2 :style="{ color: getTheme(node.type).label, textShadow: `0 0 15px ${hexToRgba(getTheme(node.type).color, 0.5)}` }">
            {{ node.name }}
          </h2>
          <span class="type-badge" :style="{ borderColor: getTheme(node.type).color, color: getTheme(node.type).color, background: hexToRgba(getTheme(node.type).color, 0.1) }">
            {{ getTheme(node.type).typeLabel }}
          </span>
        </div>
        <button class="close-btn" @click="$emit('close')">
          <svg viewBox="0 0 24 24" width="22" height="22" stroke="currentColor" stroke-width="2" fill="none">
            <line x1="18" y1="6" x2="6" y2="18"></line>
            <line x1="6" y1="6" x2="18" y2="18"></line>
          </svg>
        </button>
      </div>

      <div class="panel-content custom-scrollbar">
        <div v-if="node.coverImage" class="cover-image-wrapper">
          <img :src="node.coverImage" :alt="node.name" />
        </div>

        <!-- 寰宇纪要：引入卡片化容器与 Tiptap 富文本深度渲染 -->
        <div class="section-block">
          <div class="section-header">
            <span class="section-icon">📚</span>
            <h3 class="section-title">寰宇纪要</h3>
          </div>
          <div class="content-box">
            <!-- 👇 新增：绑定 handleContentClick 以拦截附件卡片点击 👇 -->
            <div 
              v-if="node.description" 
              class="description-text tiptap-content" 
              v-html="node.description"
              @click="handleContentClick"
            ></div>
            <p v-else class="description-placeholder">
              暂无对此节点的详细寰宇纪要。请在灵脉空间中进行编纂。
            </p>
          </div>
        </div>

        <!-- 关联碎片：改为高密度、极简科幻风格的双列列表 -->
        <div v-if="neighbors.length > 0" class="section-block">
          <div class="section-header">
            <span class="section-icon">🔗</span>
            <h3 class="section-title">关联碎片</h3>
          </div>
          <div class="neighbor-grid">
            <button 
              v-for="neighbor in neighbors" 
              :key="neighbor.id"
              class="neighbor-item"
              :style="{ '--theme-color': getTheme(neighbor.type).color, '--theme-bg': hexToRgba(getTheme(neighbor.type).color, 0.08) }"
              @click="$emit('navigate:node', neighbor)"
            >
              <div class="neighbor-info">
                <span class="neighbor-name">{{ neighbor.name }}</span>
                <span class="neighbor-type" :style="{ color: getTheme(neighbor.type).color }">
                  {{ getTheme(neighbor.type).typeLabel }}
                </span>
              </div>
              <svg class="nav-icon" viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" stroke-width="2" fill="none">
                <polyline points="9 18 15 12 9 6"></polyline>
              </svg>
            </button>
          </div>
        </div>
      </div>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import type { GraphNode } from './types'
import { useKnowledgeGraphData } from './useKnowledgeGraphData'
// 👇 新增：引入 request 工具 👇
import request from '@/utils/request'

defineProps<{
  show: boolean;
  node: GraphNode | null;
  neighbors: GraphNode[];
}>()

defineEmits<{
  'close': [];
  'navigate:node': [node: GraphNode];
}>()

const { getTheme, hexToRgba } = useKnowledgeGraphData()

// 👇 新增：监听富文本区域内的点击事件，处理 NAS 下载逻辑 👇
// 👇 替换原有的 handleContentClick 👇
const handleContentClick = async (event: MouseEvent) => {
  const target = event.target as HTMLElement;
  const card = target.closest('.nas-attachment-card') as HTMLElement;
  
  if (card) {
    const fileName = card.dataset.filename;
    console.log('[DEBUG] 1. 点击了卡片，准备下载的文件名:', fileName);
    
    if (!fileName) {
      console.error('[DEBUG] 找不到绑定的文件名！');
      return;
    }

    const btn = card.querySelector('.nas-download-btn');
    if (btn) btn.textContent = '申请密匙中...';

    try {
      console.log('[DEBUG] 2. 正在向后端发起请求...');
      const res: any = await request.get(`/Resource/download?fileName=${encodeURIComponent(fileName)}`);
      
      console.log('[DEBUG] 3. 后端返回的完整数据:', res);
      
      // 兼容你项目中各种可能的 axios 拦截器嵌套格式
      const downloadUrl = res.downloadUrl || res.data?.downloadUrl || res.data?.data?.downloadUrl;
      
      if (downloadUrl) {
        console.log('[DEBUG] 4. 成功解析出下载直链:', downloadUrl);
        if (btn) btn.textContent = '获取成功，正在拉取';
        
        // --- 强制下载机制 ---
        const a = document.createElement('a');
        a.href = downloadUrl;
        a.download = fileName.split('/').pop() || 'download';
        a.target = '_blank'; // 加上这个，如果是跨域或者混合内容，至少会在新标签页打开让你看到报错
        document.body.appendChild(a);
        
        try {
          a.click();
        } catch(e) {
          console.warn('[DEBUG] 浏览器拦截了静默下载，尝试新窗口打开');
          window.open(downloadUrl, '_blank');
        }
        
        document.body.removeChild(a);
        
        setTimeout(() => {
          if (btn) btn.textContent = '点击下载资源';
        }, 2000);
      } else {
        console.error('[DEBUG] 没有在返回值里找到 downloadUrl 字段！');
        if (btn) btn.textContent = '未获取到下载链接';
      }
    } catch (error) {
      console.error('[DEBUG] 5. 请求过程发生错误:', error);
      if (btn) btn.textContent = '请求被拦截或出错';
    }
  }
}
</script>

<style scoped>
.details-panel {
  position: absolute; 
  top: 0; 
  right: 0; 
  z-index: 20;
  width: 520px; 
  height: 100vh; 
  background: linear-gradient(180deg, rgba(15, 23, 42, 0.95) 0%, rgba(2, 6, 23, 0.98) 100%);
  border-left: 1px solid rgba(56, 189, 248, 0.15); 
  backdrop-filter: blur(24px);
  -webkit-backdrop-filter: blur(24px);
  box-shadow: -15px 0 40px rgba(0, 0, 0, 0.4); 
  display: flex; 
  flex-direction: column;
}

/* --- Header --- */
.panel-header {
  padding: 24px 32px; 
  display: flex; 
  align-items: flex-start; 
  justify-content: space-between;
  border-bottom: 1px solid rgba(148, 163, 184, 0.1);
  background: radial-gradient(ellipse at top left, rgba(56, 189, 248, 0.05) 0%, transparent 70%);
}
.title-area h2 { 
  font-size: 28px; 
  font-weight: 700; 
  margin: 0 0 10px 0; 
  letter-spacing: 1.5px; 
}
.type-badge {
  display: inline-block; 
  font-size: 11px; 
  font-weight: 600;
  letter-spacing: 0.2em;
  text-transform: uppercase; 
  padding: 4px 10px; 
  border: 1px solid;
  border-radius: 4px; 
}
.close-btn {
  background: rgba(255, 255, 255, 0.03); 
  border: 1px solid rgba(255, 255, 255, 0.05); 
  color: #94a3b8; 
  cursor: pointer;
  padding: 8px; 
  transition: all 0.2s ease; 
  border-radius: 6px;
}
.close-btn:hover { 
  background: rgba(239, 68, 68, 0.1); 
  border-color: rgba(239, 68, 68, 0.3);
  color: #ef4444; 
}

/* --- Content Area --- */
.panel-content { 
  flex: 1; 
  overflow-y: auto; 
  padding: 32px; 
}

.section-block {
  margin-bottom: 40px;
}

.section-header {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 16px;
}
.section-icon {
  font-size: 16px;
  opacity: 0.8;
}
.section-title { 
  color: #f8fafc; 
  font-size: 16px; 
  font-weight: 600; 
  margin: 0; 
  letter-spacing: 1.5px; 
}

/* 纪要内容框设计 */
.content-box {
  background: rgba(0, 0, 0, 0.25);
  border: 1px solid rgba(255, 255, 255, 0.03);
  border-radius: 10px;
  padding: 24px;
  box-shadow: inset 0 2px 10px rgba(0, 0, 0, 0.2);
}

.description-text { 
  font-size: 15px; 
  line-height: 1.9; 
  color: #cbd5e1; 
  text-align: justify; 
  letter-spacing: 0.5px; 
}
.description-placeholder { 
  font-size: 14px; 
  color: #64748b; 
  font-style: italic; 
  text-align: center;
  padding: 20px 0;
}

/* --- 关联节点（重构为精致卡片） --- */
.neighbor-grid { 
  display: grid; 
  grid-template-columns: repeat(2, 1fr); 
  gap: 12px; 
}
.neighbor-item {
  display: flex; 
  align-items: center; 
  justify-content: space-between;
  width: 100%;
  padding: 14px 16px; 
  background: rgba(255, 255, 255, 0.02);
  border: 1px solid rgba(255, 255, 255, 0.05); 
  border-left: 3px solid var(--theme-color);
  border-radius: 6px;
  text-align: left; 
  cursor: pointer; 
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1); 
}
.neighbor-item:hover { 
  background: var(--theme-bg);
  transform: translateX(4px); 
  border-color: rgba(255, 255, 255, 0.1);
  box-shadow: 0 4px 15px rgba(0, 0, 0, 0.2); 
}
.neighbor-info {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.neighbor-name { 
  color: #f8fafc; 
  font-size: 14px; 
  font-weight: 500; 
}
.neighbor-type { 
  font-size: 10px; 
  letter-spacing: 0.15em; 
  text-transform: uppercase; 
  font-weight: 600;
}
.nav-icon {
  color: #64748b;
  transition: transform 0.2s, color 0.2s;
}
.neighbor-item:hover .nav-icon {
  color: var(--theme-color);
  transform: translateX(3px);
}

/* 富文本增强 */
:deep(.tiptap-content p) {
  margin: 0 0 1em 0;
}
:deep(.tiptap-content p:last-child) {
  margin-bottom: 0;
}
:deep(.tiptap-content strong) {
  color: #f8fafc;
  font-weight: 600;
}

/* 封面图 */
.cover-image-wrapper {
  margin-bottom: 32px; 
  border-radius: 10px; 
  overflow: hidden;
  border: 1px solid rgba(148, 163, 184, 0.15); 
  box-shadow: 0 8px 25px rgba(0,0,0,0.4);
}
.cover-image-wrapper img { width: 100%; height: auto; display: block; }

/* 👇 新增：NAS 附件卡片的赛博 CSS 样式 👇 */
:deep(.nas-attachment-card) {
  display: flex;
  align-items: center;
  background: rgba(14, 165, 233, 0.08);
  border: 1px solid rgba(14, 165, 233, 0.3);
  border-left: 4px solid #0ea5e9;
  border-radius: 8px;
  padding: 16px;
  margin-top: 10px;
  cursor: pointer;
  transition: all 0.3s ease;
}
:deep(.nas-attachment-card:hover) {
  background: rgba(14, 165, 233, 0.15);
  border-color: rgba(14, 165, 233, 0.6);
  transform: translateX(4px);
  box-shadow: 0 4px 20px rgba(14, 165, 233, 0.2);
}
:deep(.nas-icon) {
  font-size: 28px;
  margin-right: 16px;
}
:deep(.nas-info) {
  flex: 1;
}
:deep(.nas-name) {
  color: #f0f9ff;
  font-size: 15px;
  font-weight: 600;
  margin-bottom: 4px;
}
:deep(.nas-size) {
  color: #7dd3fc;
  font-size: 11px;
  letter-spacing: 1px;
}
:deep(.nas-download-btn) {
  color: #0ea5e9;
  font-size: 13px;
  font-weight: 600;
  padding: 8px 12px;
  background: rgba(14, 165, 233, 0.15);
  border-radius: 6px;
  transition: all 0.2s;
}
:deep(.nas-attachment-card:hover .nas-download-btn) {
  background: #0ea5e9;
  color: #020617;
}

/* 动画与滚动条 */
.slide-right-enter-active, .slide-right-leave-active { transition: transform 0.4s cubic-bezier(0.16, 1, 0.3, 1); }
.slide-right-enter-from, .slide-right-leave-to { transform: translateX(100%); }

.custom-scrollbar::-webkit-scrollbar { width: 6px; }
.custom-scrollbar::-webkit-scrollbar-track { background: transparent; }
.custom-scrollbar::-webkit-scrollbar-thumb { background: rgba(148, 163, 184, 0.15); border-radius: 3px; }
.custom-scrollbar::-webkit-scrollbar-thumb:hover { background: rgba(56, 189, 248, 0.4); }
</style>