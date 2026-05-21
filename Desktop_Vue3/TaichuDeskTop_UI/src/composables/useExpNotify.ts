// composables/useExpNotify.ts
import { h, render } from 'vue'
import ExpNotification from '../components/ExpNotification.vue'

let container: HTMLElement | null = null
let componentInstance: any = null

export function useExpNotify() {
  const notify = (amount: number) => {
    if (!componentInstance) {
      container = document.createElement('div')
      const vnode = h(ExpNotification)
      render(vnode, container)
      document.body.appendChild(container)
      componentInstance = vnode.component?.exposed
    }
    
    componentInstance?.show(amount)
  }

  return { notify }
}