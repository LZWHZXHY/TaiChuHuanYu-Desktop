<template>
  <div class="workspace-art">

    <div class="art-hero-zone" :class="{'has-image': hasImage}">
      <span class="icon">{{ hasImage ? '✨' : '🖼️' }}</span>
      <div class="hero-text">
        <template v-if="!hasImage">
          <p class="main-hint">视觉画廊需要意象的支撑</p>
          <p class="sub-hint">请在下方正文中通过 <b>/指令</b> 或拖拽插入图片<br/>系统将自动将其提取为展厅焦点</p>
        </template>
        <template v-else>
          <p class="main-hint success">视觉意象已成功感应</p>
          <p class="sub-hint">作品已准备好向位面折射</p>
        </template>
      </div>
    </div>
    
    <input 
      :value="title" 
      @input="$emit('update:title', ($event.target as HTMLInputElement).value)"
      class="title-input align-center" 
      placeholder="为你的视觉杰作命名..." 
      spellcheck="false"
      :readonly="readonly"
    />
    
    <div class="art-description-wrapper">
      <div class="desc-label">作品注脚 / Description</div>
      <slot name="editor"></slot>
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  title: string;
  readonly?: boolean;
  hasImage?: boolean; // 🌟 专属 Prop：接收外部的图片校验状态
}>();

defineEmits(['update:title']);
</script>

<style scoped>
.workspace-art {
  max-width: 900px; 
  margin: 0 auto;
  padding: 20px 0 60px;
}

/* 顶部画廊引导区 */
.art-hero-zone {
  width: 100%; 
  height: 240px; 
  background: #fdfdfd; 
  border-radius: 24px;
  display: flex; 
  flex-direction: column; 
  align-items: center; 
  justify-content: center;
  margin-bottom: 40px; 
  border: 2px dashed #d2d2d7; 
  text-align: center;
  transition: all 0.5s cubic-bezier(0.16, 1, 0.3, 1);
}

/* 当检测到图片时，画廊区域的变色特效 (呼应神秘紫) */
.art-hero-zone.has-image { 
  background: rgba(175, 82, 222, 0.03); 
  border-color: rgba(175, 82, 222, 0.3); 
  border-style: solid; 
  box-shadow: 0 10px 40px rgba(175, 82, 222, 0.08);
  transform: translateY(-2px);
}

.art-hero-zone .icon { 
  font-size: 36px; 
  margin-bottom: 16px; 
  opacity: 0.6; 
  transition: all 0.3s;
}
.art-hero-zone.has-image .icon {
  opacity: 1;
  filter: drop-shadow(0 0 8px rgba(175, 82, 222, 0.6));
}

.hero-text .main-hint {
  font-size: 15px;
  font-weight: 700;
  color: #86868b;
  margin-bottom: 6px;
}
.hero-text .main-hint.success {
  color: #af52de;
}
.hero-text .sub-hint {
  font-size: 12px;
  color: #a1a1a6;
  line-height: 1.6;
}

/* 居中标题 */
.title-input { 
  width: 100%; 
  border: none; 
  font-size: 2.8rem; 
  font-weight: 800; 
  margin-bottom: 40px; 
  outline: none; 
  background: transparent; 
  letter-spacing: -0.02em; 
  color: #1d1d1f; 
}
.title-input.align-center { 
  text-align: center; 
}

/* 富文本描述区卡片化 */
.art-description-wrapper {
  background: #ffffff; 
  padding: 40px 50px; 
  border-radius: 20px; 
  box-shadow: 0 8px 30px rgba(0,0,0,0.03);
  border: 1px solid #f2f2f7;
}

.desc-label {
  font-size: 11px;
  font-weight: 700;
  color: #c7c7cc;
  text-transform: uppercase;
  letter-spacing: 0.1em;
  margin-bottom: 24px;
  border-bottom: 1px solid #f2f2f7;
  padding-bottom: 12px;
}

@media (max-width: 1024px) {
  .workspace-art { padding: 0; }
  .art-hero-zone { height: 180px; border-radius: 16px; }
  .title-input { font-size: 2.2rem; }
  .art-description-wrapper { padding: 24px 20px; border-radius: 16px; }
}
</style>