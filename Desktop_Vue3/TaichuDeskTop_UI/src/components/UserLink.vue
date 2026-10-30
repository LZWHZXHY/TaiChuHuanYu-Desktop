<script setup lang="ts">
// 用户名/头像统一入口：有用户 ID 时点击进入个人主页，没有时按普通文字显示。
// nested：放在另一个链接或按钮里面时使用（HTML 不允许链接套链接），改用 span + 点击跳转。
import { computed } from 'vue'
import { useRouter } from 'vue-router'

const props = defineProps<{ id?: string | number | null; name?: string | null; nested?: boolean }>()
const router = useRouter()
const to = computed(() => props.id ? { name: 'UserCenter', params: { id: String(props.id) } } : null)
const label = computed(() => props.name ? `查看 ${props.name} 的主页` : '查看个人主页')

function go(event: Event) {
  if (!to.value) return
  event.preventDefault()
  event.stopPropagation()
  router.push(to.value).catch(() => {})
}
</script>

<template>
  <RouterLink v-if="to && !nested" :to="to" class="user-link" :title="label" @click.stop><slot>{{ name }}</slot></RouterLink>
  <span v-else-if="to" class="user-link" role="link" tabindex="0" :title="label" @click="go" @keydown.enter="go"><slot>{{ name }}</slot></span>
  <span v-else class="user-link is-plain"><slot>{{ name }}</slot></span>
</template>

<style>
/* 用 :where 把优先级降到 0，调用处给的 class（如 .author-info、.comment-avatar）总能覆盖这里的默认样式。 */
:where(.user-link) { display: inline-flex; align-items: center; gap: 6px; min-width: 0; color: inherit; font: inherit; text-decoration: none; cursor: pointer; }
:where(.user-link.is-plain) { cursor: inherit; }
:where(.user-link:not(.is-plain)):is(:hover, :focus-visible) { color: var(--accent-color); text-decoration: underline; text-underline-offset: 3px; }
:where(.user-link:not(.is-plain)):focus-visible { outline: 2px solid var(--accent-color); outline-offset: 2px; }
</style>
