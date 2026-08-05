<template>
  <div class="oc-card" @click="emit('click', character.id)">
    <div class="oc-canvas-box">
      <img
        v-if="character.coverUrl"
        :src="character.coverUrl"
        :alt="character.title"
        class="oc-avatar"
      />
      <svg
        v-else
        width="60"
        height="90"
        viewBox="0 0 60 90"
        stroke="currentColor"
        fill="none"
        stroke-width="2"
        style="color: var(--text-primary)"
      >
        <circle cx="30" cy="20" r="10" />
        <line x1="30" y1="30" x2="30" y2="60" />
        <line x1="30" y1="40" x2="10" y2="25" />
        <line x1="30" y1="40" x2="50" y2="20" />
        <line x1="30" y1="60" x2="15" y2="85" />
        <line x1="30" y1="60" x2="45" y2="85" />
      </svg>
      <span v-if="character.status === 'draft'" class="badge-draft">草稿</span>
    </div>

    <div class="oc-name">{{ character.title }}</div>
    <div class="oc-meta">
      <span>作者：{{ character.authorName }}</span>
      <span v-if="firstTag" style="color: var(--accent-color);">
        {{ firstTag }}
      </span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { StickmanCharacter } from '../stickman'

const props = defineProps<{
  character: StickmanCharacter
}>()

const emit = defineEmits<{
  click: [id: string]
}>()

// 从 attributes 中提取第一个标签作为展示
const firstTag = computed(() => {
  if (!props.character.attributes?.length) return null
  const tagAttr = props.character.attributes.find(a => a.key === '标签')
  return tagAttr?.value || null
})
</script>



<style scoped>
.oc-card {
  border: 1px solid var(--border-line);
  padding: 16px;
  transition: all 0.3s ease;
  cursor: pointer;
  background: var(--bg-main);
}

.oc-card:hover {
  border-color: var(--accent-color);
  transform: translateY(-2px);
}

.oc-canvas-box {
  position: relative;
  width: 100%;
  height: 140px;
  border: 1px dashed var(--border-line);
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 12px;
  background: var(--bg-sub);
  overflow: hidden;
  transition: border-color 0.3s;
}

.oc-card:hover .oc-canvas-box {
  border-color: var(--accent-color);
}

.oc-avatar {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.badge-draft {
  position: absolute;
  top: 6px;
  right: 6px;
  padding: 2px 10px;
  font-size: 11px;
  color: #ffffff;
  background: rgba(44, 42, 41, 0.75);
  letter-spacing: 0.1em;
  font-family: var(--font-family);
}

.oc-name {
  font-size: 16px;
  letter-spacing: 0.15em;
  margin-bottom: 6px;
  color: var(--text-primary);
}

.oc-meta {
  font-size: 12px;
  color: var(--text-secondary);
  display: flex;
  justify-content: space-between;
  letter-spacing: 0.1em;
}

/* ===== 响应式 ===== */
@media (max-width: 768px) {
  .oc-canvas-box {
    height: 110px;
  }
}

@media (max-width: 480px) {
  .oc-canvas-box {
    height: 90px;
  }

  .oc-name {
    font-size: 14px;
  }

  .oc-meta {
    font-size: 11px;
  }
}
</style>