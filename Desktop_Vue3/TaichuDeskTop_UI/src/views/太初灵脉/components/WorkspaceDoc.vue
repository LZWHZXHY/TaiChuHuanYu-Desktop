<template>
  <div class="workspace-project-frame">
    <header class="project-header">
      <div class="project-breadcrumbs">
        <span class="badge">项目文档</span>
        <span class="sep">/</span>
        <span>协作空间</span>
      </div>
      
      <div class="project-title-row">
        <input 
          :value="title" 
          @input="$emit('update:title', ($event.target as HTMLInputElement).value)"
          class="project-title-input" 
          placeholder="输入项目文档标题..." 
          spellcheck="false"
        />
      </div>
    </header>
    
    <div class="project-layout-clean">
      <article class="project-main-article">
        <slot name="editor"></slot>
      </article>
    </div>
  </div>
</template>

<script setup lang="ts">
// 严守契约：对齐主控层的多态组件传参
defineProps<{ 
  title: string;
  noteId?: string;
  extraData?: string;
  blocks?: any[];
}>();

defineEmits(['update:title', 'change']);
</script>

<style scoped>
.workspace-project-frame { 
  max-width: 900px; 
  margin: 0 auto; 
  padding: 40px 24px 80px; 
  background: #ffffff;
}

.project-header { 
  margin-bottom: 40px; 
  border-bottom: 2px solid #f2f2f7;
  padding-bottom: 24px;
}

.project-breadcrumbs { 
  font-size: 12px; 
  color: #86868b; 
  font-weight: 600;
  display: flex;
  align-items: center;
  margin-bottom: 20px; 
}

.badge {
  background: rgba(0, 102, 204, 0.1);
  color: #0066cc;
  padding: 4px 8px;
  border-radius: 6px;
  letter-spacing: 0.02em;
}

.project-breadcrumbs .sep { 
  margin: 0 10px; 
  font-weight: 300; 
  opacity: 0.4; 
}

.project-title-row { 
  display: flex; 
  align-items: center; 
}

.project-title-input { 
  font-size: 2.6rem; 
  font-weight: 800; 
  border: none; 
  background: transparent; 
  outline: none; 
  flex: 1; 
  color: #1d1d1f; 
  letter-spacing: -0.02em; 
  line-height: 1.2; 
}

.project-layout-clean {
  width: 100%;
}

.project-main-article { 
  width: 100%;
  min-height: 500px; 
}

@media (max-width: 1024px) {
  .workspace-project-frame { padding: 20px 16px 40px; }
  .project-title-input { font-size: 2rem; }
}
</style>