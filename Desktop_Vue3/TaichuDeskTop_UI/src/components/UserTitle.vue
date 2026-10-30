<!-- UserTitle.vue：名字旁边的小头衔。传 user-id 时自动读取对方佩戴的头衔（全站合并请求、有缓存）；也可以直接传 title。没戴头衔时什么都不显示。 -->
<template>
  <span v-if="text" class="user-title-badge" :title="`头衔：${text}`">{{ text }}</span>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { appearanceOf } from '@/utils/appearanceCache'

const props = defineProps<{ userId?: string | number | null; title?: string | null }>()
const text = computed(() => props.title !== undefined ? props.title || '' : appearanceOf(props.userId)?.title || '')
</script>

<style>
/* 不用 scoped：各处字号不同，徽章跟着父元素缩放；:where 让调用处可以覆盖 */
:where(.user-title-badge) { display: inline-block; flex: 0 0 auto; max-width: 12em; margin-left: 6px; padding: 0 6px; overflow: hidden; border: 1px solid var(--accent-color); color: var(--accent-color); font-size: 11px; font-weight: 600; font-style: normal; line-height: 16px; letter-spacing: .04em; text-overflow: ellipsis; white-space: nowrap; vertical-align: middle; }
</style>
