import { onMounted, onUnmounted, type Ref } from 'vue'

/**
 * 滚动视差：页面滚动时，装饰元素以不同速度移动，形成前后层次。
 * · 所有页头里的装饰线条和粒子徽记（.section-trace）自动参与；
 * · 其他元素加 data-parallax="0.1"（数值越大移动越多，负数反向）。
 * 只改 CSS 变量 --parallax-y，不影响布局；减少动态效果时不启用。
 */
export function useScrollParallax(viewport: Ref<HTMLElement | undefined>) {
  let frame = 0
  let media: MediaQueryList | undefined
  const SELECTOR = '[data-parallax], .section-trace'

  // 偏移量只由滚动距离决定：元素在“自然位置”时偏移为 0，随滚动慢半拍（比正文移动得慢）。
  // · 首屏内的元素（例如页头装饰）：页面没滚动时就在原位，不会一打开就错开；
  // · 首屏以下的元素：滚到视口中央时回到原位。
  // 计算时先扣掉当前已经施加的偏移，避免“按偏移后的位置再算偏移”导致越算越偏、来回跳。
  const update = () => {
    frame = 0
    const vp = viewport.value
    if (!vp || !media?.matches) return
    const box = vp.getBoundingClientRect()
    const half = box.height / 2
    vp.querySelectorAll<HTMLElement>(SELECTOR).forEach(el => {
      const rect = el.getBoundingClientRect()
      if (rect.bottom < box.top - 300 || rect.top > box.bottom + 300) return
      const applied = parseFloat(el.style.getPropertyValue('--parallax-y')) || 0
      const center = rect.top - applied - box.top + rect.height / 2 // 未偏移时在视口中的位置
      const natural = Math.min(half, center + vp.scrollTop) // 元素的“自然位置”
      const factor = Number(el.dataset.parallax ?? (el.classList.contains('section-particles') ? 0.18 : 0.1))
      const limit = el.classList.contains('section-trace') ? 36 : 60
      const offset = Math.max(-limit, Math.min(limit, (natural - center) * factor))
      el.style.setProperty('--parallax-y', `${offset.toFixed(1)}px`)
    })
  }
  const schedule = () => { if (!frame) frame = requestAnimationFrame(update) }
  const clear = () => {
    viewport.value?.querySelectorAll<HTMLElement>(SELECTOR).forEach(el => el.style.removeProperty('--parallax-y'))
  }
  const onMedia = () => { media?.matches ? schedule() : clear() }

  onMounted(() => {
    media = matchMedia('(prefers-reduced-motion: no-preference)')
    media.addEventListener('change', onMedia)
    viewport.value?.addEventListener('scroll', schedule, { passive: true })
    window.addEventListener('resize', schedule, { passive: true })
    schedule()
  })
  onUnmounted(() => {
    cancelAnimationFrame(frame)
    media?.removeEventListener('change', onMedia)
    viewport.value?.removeEventListener('scroll', schedule)
    window.removeEventListener('resize', schedule)
  })
  /** 路由切换后新页面的元素需要重新计算一次 */
  return { refresh: schedule }
}
