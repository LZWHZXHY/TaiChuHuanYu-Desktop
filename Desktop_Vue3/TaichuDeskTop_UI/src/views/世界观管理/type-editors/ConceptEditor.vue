<!-- src/views/世界观管理/type-editors/ConceptEditor.vue -->
<template>
  <div class="concept-editor">
    <div class="empty-state">
      <div class="empty-icon">✦</div>
      <p class="empty-title">设定卡片</p>
      <p class="empty-desc">
        世界观规则、宇宙真理、魔法体系、哲学概念、教义法则等抽象设定，<br />
        可以通过「<strong>自定义属性</strong>」和「<strong>关联内容</strong>」自由构建
      </p>
    </div>

    <!-- 🔥 提示用户使用内容块插入关联卡片 -->
    <div class="editor-hint">
      <p>💡 提示：在「关联内容」区域可以关联「事件」「技能」「地点」等具体卡片</p>
    </div>
  </div>
</template>

<script setup lang="ts">
// 概念卡片是一个纯粹的空模板，用于承载抽象设定
// 所有信息通过「自定义属性」和「关联内容」来扩展
</script>

<style scoped>
.concept-editor {
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