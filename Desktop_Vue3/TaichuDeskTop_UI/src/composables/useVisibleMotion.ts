import { onMounted, onUnmounted, ref, watch, type Ref } from 'vue'

/** 装饰离开视口或标签页进入后台后暂停，避免持续绘制不可见动画。 */
export function useVisibleMotion(target: Ref<Element | undefined>) {
  const active = ref(false)
  let inView = false
  let observer: IntersectionObserver | undefined
  const sync = () => { active.value = inView && !document.hidden }
  watch(target, (element, previous) => {
    if (previous) observer?.unobserve(previous)
    inView = false; sync()
    if (element) observer?.observe(element)
  }, { flush: 'post' })
  onMounted(() => {
    observer = new IntersectionObserver(([entry]) => { inView = entry?.isIntersecting ?? false; sync() })
    if (target.value) observer.observe(target.value)
    document.addEventListener('visibilitychange', sync)
  })
  onUnmounted(() => { observer?.disconnect(); document.removeEventListener('visibilitychange', sync) })
  return active
}
