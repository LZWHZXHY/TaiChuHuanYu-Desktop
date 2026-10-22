import { ref } from 'vue'

export function useImagePreview() {
  const visible = ref(false)
  const images = ref<string[]>([])
  const index = ref(0)
  function open(urls: string[], initialIndex = 0) {
    if (!urls.length) return
    images.value = [...urls]
    index.value = Math.max(0, Math.min(initialIndex, urls.length - 1))
    visible.value = true
  }
  const close = () => { visible.value = false }
  return { visible, images, index, open, close }
}
