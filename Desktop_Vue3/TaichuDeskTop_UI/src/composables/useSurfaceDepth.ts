import { onMounted, onUnmounted, type Ref } from 'vue'

/**
 * 卡片 3D：给元素加 data-depth，鼠标在上面移动时整张卡片朝鼠标方向倾斜，
 * 里面的图片反向错开（视差），表面有一道跟随鼠标的反光，背后浮出一块错位的信号色底板。
 * data-depth="soft" 用于大面积展台，倾斜更轻。
 * 单个委托监听，没有常驻动画循环；触屏、减少动态效果时不启用。
 */
export function useSurfaceDepth(root: Ref<HTMLElement | undefined>) {
  let active: HTMLElement | null = null
  let bounds: DOMRect | undefined
  let frame = 0
  let x = 0, y = 0
  let media: MediaQueryList | undefined
  let host: HTMLElement | undefined
  const reset = () => {
    cancelAnimationFrame(frame); frame = 0
    active?.style.removeProperty('--depth-x')
    active?.style.removeProperty('--depth-y')
    active?.removeAttribute('data-depth-active')
    active = null; bounds = undefined
  }
  const ensureGlare = (el: HTMLElement) => {
    if (el.dataset.depth === 'soft' || el.querySelector(':scope > .depth-glare')) return
    const glare = document.createElement('span')
    glare.className = 'depth-glare'
    glare.setAttribute('aria-hidden', 'true')
    el.appendChild(glare)
  }
  const paint = () => {
    frame = 0
    active?.style.setProperty('--depth-x', x.toFixed(3))
    active?.style.setProperty('--depth-y', y.toFixed(3))
  }
  const move = (event: PointerEvent) => {
    if (!media?.matches || document.hidden || event.pointerType !== 'mouse' || !host?.classList.contains('community-design')) return reset()
    const target = event.target instanceof Element ? event.target.closest<HTMLElement>('[data-depth]') : null
    if (!target || !host.contains(target)) return reset()
    if (active !== target) {
      reset(); active = target; bounds = target.getBoundingClientRect()
      ensureGlare(target)
      target.setAttribute('data-depth-active', '')
    }
    if (!bounds?.width || !bounds.height) return
    x = Math.max(-1, Math.min(1, (event.clientX - bounds.left) / bounds.width * 2 - 1))
    y = Math.max(-1, Math.min(1, (event.clientY - bounds.top) / bounds.height * 2 - 1))
    if (!frame) frame = requestAnimationFrame(paint)
  }
  onMounted(() => {
    host = root.value
    media = matchMedia('(hover: hover) and (pointer: fine) and (prefers-reduced-motion: no-preference)')
    host?.addEventListener('pointermove', move, { passive: true })
    host?.addEventListener('pointerleave', reset)
    host?.addEventListener('scroll', reset, { capture: true, passive: true })
    media.addEventListener('change', reset)
    window.addEventListener('resize', reset, { passive: true })
    document.addEventListener('visibilitychange', reset)
  })
  onUnmounted(() => {
    reset()
    host?.removeEventListener('pointermove', move)
    host?.removeEventListener('pointerleave', reset)
    host?.removeEventListener('scroll', reset, true)
    media?.removeEventListener('change', reset)
    window.removeEventListener('resize', reset)
    document.removeEventListener('visibilitychange', reset)
  })
}
