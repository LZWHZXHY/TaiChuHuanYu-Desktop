<!-- src/views/世界观管理/type-editors/SpeciesEditor.vue -->
<template>
  <div class="species-editor">
    <div class="empty-state">
      <div class="empty-icon">🐉</div>
      <p class="empty-title">物种卡片</p>
      <p class="empty-desc">
        物种的寿命、起源等信息，<br />
        可以通过「<strong>自定义属性</strong>」自由添加
      </p>
    </div>

    <!-- 🔥 提示用户使用内容块插入关联卡片 -->
    <div class="editor-hint">
      <p>💡 提示：在「关联内容」区域点击「+地点」可插入「栖息地」，点击「+技能」可插入「特殊能力」</p>
    </div>
  </div>
</template>

<script setup lang="ts">
// 物种卡片已没有任何预设专属字段
// 所有信息都通过「自定义属性」和「内容块」来扩展
</script>

<style scoped>
.species-editor {
  padding: 16px 0;
  border-top: 1px solid #eef2f6;
  border-bottom: 1px solid #eef2f6;
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 32px 20px;
  text-align: center;
}

.empty-icon {
  font-size: 48px;
  margin: 0 0 12px 0;
}

.empty-title {
  font-size: 16px;
  font-weight: 500;
  color: #1e293b;
  margin: 0 0 8px 0;
}

.empty-desc {
  font-size: 14px;
  color: #94a3b8;
  margin: 0;
  line-height: 1.6;
}
.empty-desc strong {
  color: #4f46e5;
}

.editor-hint {
  margin-top: 12px;
  padding: 8px 12px;
  background: #f0f4ff;
  border-radius: 6px;
  font-size: 12px;
  color: #4f46e5;
  border: 1px solid #c7d2fe;
}
.editor-hint p {
  margin: 0;
}
</style>