<!-- UserTitle.vue：名字旁边的小头衔。传 user-id 时自动读取对方佩戴的头衔（全站合并请求、有缓存）；也可以直接传 title。没戴头衔时什么都不显示。
     赞助头衔（觉醒者 / 超凡者 / 大超凡者 / 君者）有自己的颜色，越高档越亮眼。 -->
<template>
  <span v-if="text" class="user-title-badge" :class="sponsorTier ? ['sponsor-title', `sponsor-t${sponsorTier}`] : null" :title="sponsorTier ? `赞助头衔：${text}（感谢支持太初寰宇）` : `头衔：${text}`">{{ sponsorTier ? '✦ ' : '' }}{{ text }}</span>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { appearanceOf } from '@/utils/appearanceCache'

const props = defineProps<{ userId?: string | number | null; title?: string | null }>()
const text = computed(() => props.title !== undefined ? props.title || '' : appearanceOf(props.userId)?.title || '')
/** 赞助档位（1 觉醒者 … 4 君者），不是赞助头衔为 0 */
const SPONSOR_TIERS = ['觉醒者', '超凡者', '大超凡者', '君者']
const sponsorTier = computed(() => SPONSOR_TIERS.indexOf(text.value.trim()) + 1)
</script>

<style>
/* 不用 scoped：各处字号不同，徽章跟着父元素缩放；:where 让调用处可以覆盖 */
:where(.user-title-badge) { display: inline-block; flex: 0 0 auto; max-width: 12em; margin-left: 6px; padding: 0 6px; overflow: hidden; border: 1px solid var(--accent-color); color: var(--accent-color); font-size: 11px; font-weight: 600; font-style: normal; line-height: 16px; letter-spacing: .04em; text-overflow: ellipsis; white-space: nowrap; vertical-align: middle; }
:where(.user-title-badge.sponsor-title) { border-color: transparent; color: #fff; text-shadow: 0 1px 0 rgba(0, 0, 0, .25); }
:where(.user-title-badge.sponsor-t1) { background: linear-gradient(135deg, #2f9e8f, #3bb8a6); }
:where(.user-title-badge.sponsor-t2) { background: linear-gradient(135deg, #3b6fd8, #7a5cf0); }
:where(.user-title-badge.sponsor-t3) { background: linear-gradient(135deg, #c2410c, #f97316); }
:where(.user-title-badge.sponsor-t4) { background: linear-gradient(135deg, #a16207, #eab308 55%, #fde68a); color: #3b2a00; text-shadow: none; box-shadow: 0 0 0 1px rgba(234, 179, 8, .45), 0 0 8px rgba(234, 179, 8, .35); }
</style>
