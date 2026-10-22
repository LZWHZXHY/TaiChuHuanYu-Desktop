<template>
  <aside class="preview-section">
    <div class="preview-card">
      <h4>实时预览</h4>
      <div class="preview-content">
        <div class="preview-header">
          <h3>{{ form.name || '世界名称' }}</h3>
          <span class="preview-tag" :class="form.isPublic ? 'public' : 'private'">
            {{ form.isPublic ? '公开' : '私有' }}
          </span>
        </div>
        <p class="preview-desc">{{ form.description || '世界简介...' }}</p>
        <div class="preview-meta">
          <span v-if="form.type">类型：{{ form.type }}</span>
          <span>标签：{{ form.tags.length ? form.tags.map(t => '#' + t).join(' ') : '无' }}</span>
          <span>创建于：{{ new Date().toLocaleDateString() }}</span>
        </div>
      </div>
    </div>
  </aside>
</template>

<script setup lang="ts">
defineProps<{ form: { name: string; description: string; isPublic: boolean; type: string; tags: string[] } }>()
</script>

<style scoped>
.preview-section {
  position: sticky;
  top: 24px;
}
.preview-card {
  background: var(--paper-card);
  border-radius: 24px;
  padding: 24px 28px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.02);
  border: 1px solid var(--border-line);
}
.preview-card h4 {
  margin: 0 0 16px 0;
  font-size: 14px;
  font-weight: 600;
  color: var(--text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}
.preview-content {
  min-height: 180px;
}
.preview-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}
.preview-header h3 {
  margin: 0;
  font-size: 20px;
  font-weight: 600;
  color: var(--text-primary);
}
.preview-tag {
  font-size: 11px;
  font-weight: 500;
  padding: 2px 12px;
  border-radius: 20px;
  height: 24px;
  line-height: 24px;
}
.preview-tag.public {
  background: var(--success-soft);
  color: var(--success);
}
.preview-tag.private {
  background: var(--bg-sub);
  color: var(--text-secondary);
}
.preview-desc {
  color: var(--text-secondary);
  font-size: 15px;
  line-height: 1.6;
  margin: 0 0 16px 0;
  min-height: 50px;
}
.preview-meta {
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 13px;
  color: var(--text-secondary);
}
@media (max-width: 820px) {
  .preview-section {
    position: static;
  }
}
</style>
