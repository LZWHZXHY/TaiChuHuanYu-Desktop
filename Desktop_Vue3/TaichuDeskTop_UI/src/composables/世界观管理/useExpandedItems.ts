import { ref } from 'vue'

export function useExpandedItems() {
  const expanded = ref(new Set<string>())
  const isExpanded = (id: string) => expanded.value.has(id)
  function toggle(id: string, value = !isExpanded(id)) {
    if (value) expanded.value.add(id)
    else expanded.value.delete(id)
  }
  return { isExpanded, toggle }
}
