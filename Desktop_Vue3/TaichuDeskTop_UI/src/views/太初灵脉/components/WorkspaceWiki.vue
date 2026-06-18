<template>
  <div class="workspace-wiki">
    <header class="wiki-header">
      <div class="wiki-breadcrumbs">
        <span>灵脉百科</span>
        <span class="sep">/</span>
        <span>词条修订</span>
      </div>
      
      <div class="wiki-title-row">
        <input 
          :value="title" 
          @input="$emit('update:title', ($event.target as HTMLInputElement).value)"
          class="wiki-title-input" 
          placeholder="无标题词条..." 
        />
      </div>
    </header>
    
    <div class="wiki-layout-clean">
      <article class="wiki-main-article">
        <slot name="editor"></slot>
      </article>
    </div>
  </div>
</template>

<script setup lang="ts">
// 🌟 瘦身与契约对齐：补齐 noteId 与 extraData 的可选声明，完全释放通道给右侧属性栏
defineProps<{ 
  title: string;
  noteId?: string;
  extraData?: string;
}>();

defineEmits(['update:title']);
</script>

<style scoped>
/* 🌟 核心版面优化 */
.workspace-wiki { 
  max-width: 840px; /* 限制百科文章的最大视觉宽度，使其更符合人类黄金阅读视线 */
  margin: 0 auto; 
  padding: 40px 0 80px; 
}

.wiki-header { 
  margin-bottom: 50px; 
}

.wiki-breadcrumbs { 
  font-size: 12px; 
  color: #a1a1a6; 
  letter-spacing: 0.05em; 
  margin-bottom: 24px; 
}

.wiki-breadcrumbs .sep { 
  margin: 0 8px; 
  font-weight: 300; 
  opacity: 0.5; 
}

.wiki-title-row { 
  display: flex; 
  align-items: center; 
}

.wiki-title-input { 
  font-size: 2.8rem; 
  font-weight: 800; 
  border: none; 
  background: transparent; 
  outline: none; 
  flex: 1; 
  color: #1d1d1f; 
  letter-spacing: -0.03em; 
  line-height: 1.2; 
}

/* 纯净容器布局 */
.wiki-layout-clean {
  width: 100%;
}

.wiki-main-article { 
  width: 100%;
  min-height: 500px; 
}

/* 响应式调整 */
@media (max-width: 1024px) {
  .workspace-wiki { 
    padding: 20px 0 40px; 
  }
  .wiki-header { 
    margin-bottom: 32px; 
  }
  .wiki-title-input { 
    font-size: 2.2rem; 
  }
}
</style>