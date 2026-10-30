<template>
  <div class="spirit-map-wrapper">
    <div ref="mapContainer" class="leaflet-container"></div>
    
    <div class="map-toolbar">
      <span class="hint">🗺️ 双击地图任意位置创建图钉 | 拖拽更改位置</span>
      
      <button 
        class="upload-btn" 
        @click="triggerUpload" 
        :disabled="isUploading"
        :class="{ 'is-loading': isUploading }"
      >
        {{ isUploading ? '上传中...' : '📸 更换底图' }}
      </button>
      <input 
        ref="fileInput" 
        type="file" 
        accept="image/*" 
        style="display: none" 
        @change="handleImageUpload" 
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch, shallowRef } from 'vue'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import { useSpiritData } from '../composables/useSpiritData'
// 👇 引入你写好的 COS 组合式函数
import { useCos } from '../composables/useCos'
// 👇 补上这行，告诉组件我们要接收从外面传进来的背景数据
const props = defineProps<{
  bgUrl?: string;
  bgBounds?: L.LatLngBoundsExpression;
}>()
// 👇 增加 update-map-bg 事件，用来通知父组件保存地图配置
const emit = defineEmits(['open-editor', 'update-map-bg']) 
const { notes, createNewNote, updateNoteContent, selectNote } = useSpiritData()
const { uploadFile, isUploading } = useCos()

const mapContainer = ref<HTMLElement | null>(null)
const map = shallowRef<L.Map | null>(null)
const markerLayer = shallowRef<L.LayerGroup | null>(null)

const currentOverlay = shallowRef<L.ImageOverlay | null>(null)
const fileInput = ref<HTMLInputElement | null>(null)

// 默认底图常量
const MAP_IMAGE_URL = 'https://images.unsplash.com/photo-1585314062340-f1a5a7c9328d?q=80&w=2000&auto=format&fit=crop'
const MAP_BOUNDS: L.LatLngBoundsExpression = [[0, 0], [1000, 1500]]

/**
 * 🌟 核心引擎 1：初始化纯平面世界
 */
const initMap = () => {
  if (!mapContainer.value) return

  map.value = L.map(mapContainer.value, {
    crs: L.CRS.Simple,
    minZoom: -1,
    maxZoom: 3,
    zoomControl: false,
    attributionControl: false
  })

  const initialUrl = props.bgUrl || MAP_IMAGE_URL
  const initialBounds = props.bgBounds || MAP_BOUNDS

  currentOverlay.value = L.imageOverlay(initialUrl, initialBounds).addTo(map.value)
  map.value.fitBounds(initialBounds)
  
  L.control.zoom({ position: 'bottomright' }).addTo(map.value)
  markerLayer.value = L.layerGroup().addTo(map.value)
  map.value.on('dblclick', handleMapDoubleClick)
}

/**
 * 🌟 触发本地图片上传
 */
const triggerUpload = () => {
  fileInput.value?.click()
}

/**
 * 🌟 读取图片 -> 上传至 COS -> 替换 Leaflet 底图
 */
const handleImageUpload = async (e: Event) => {
  const target = e.target as HTMLInputElement
  const file = target.files?.[0]
  if (!file) return

  try {
    // 1. 上传图片到腾讯云 COS 的 map-backgrounds 目录下
    const uploadResult = await uploadFile(file, 'map-backgrounds')
    const permanentUrl = uploadResult.url // 获取真实的外网云端链接

    // 2. 加载图片以获取真实宽高，防止地图拉伸
    const img = new Image()
    img.onload = () => {
      if (!map.value) return

      const newBounds: L.LatLngBoundsExpression = [[0, 0], [img.height, img.width]]

      // 3. 卸载旧底图
      if (currentOverlay.value) {
        map.value.removeLayer(currentOverlay.value)
      }

      // 4. 铺上带有真实云端链接的地图底图，并自适应视角
      currentOverlay.value = L.imageOverlay(permanentUrl, newBounds).addTo(map.value)
      map.value.fitBounds(newBounds)
      
      // 5. 通知外层组件：底图已更换，请存入数据库！
      emit('update-map-bg', { url: permanentUrl, bounds: newBounds })

      target.value = ''
    }
    img.src = permanentUrl

  } catch (error) {
    console.error('地图底图上传失败:', error)
    alert('地图底图上传失败，请重试')
  } finally {
    // 确保清空 input，防止同名文件上传失败后无法再次选中
    target.value = ''
  }
}

/**
 * 🌟 核心引擎 2：渲染极简风格的自定义图钉
 */
const createCustomPin = (title: string) => {
  return L.divIcon({
    className: 'custom-spirit-pin',
    html: `
      <div class="pin-visual"></div>
      <div class="pin-title">${title || '未知地标'}</div>
    `,
    iconSize: [120, 40],
    iconAnchor: [60, 40]
  })
}

/**
 * 🌟 核心引擎 3：同步数据大脑，把 notes 撒到地图上
 */
const syncMarkers = () => {
  if (!map.value || !markerLayer.value) return
  markerLayer.value.clearLayers()

  notes.value.forEach(note => {
    try {
      if (!note.extraData || note.extraData === "[]") return
      const extra = JSON.parse(note.extraData)
      
      if (extra.mapPos && extra.mapPos.y !== undefined && extra.mapPos.x !== undefined) {
        const marker = L.marker([extra.mapPos.y, extra.mapPos.x], {
          icon: createCustomPin(note.title),
          draggable: true
        })

        marker.on('dragend', (e) => {
          const newPos = e.target.getLatLng()
          extra.mapPos = { y: newPos.lat, x: newPos.lng }
          note.extraData = JSON.stringify(extra)
        })

        marker.on('click', () => {
          selectNote(note.id)
          emit('open-editor', note.id)
        })

        marker.addTo(markerLayer.value!)
      }
    } catch (e) {
      console.warn('坐标解析失败', e)
    }
  })
}

const handleMapDoubleClick = async (e: L.LeafletMouseEvent) => {
  const { lat, lng } = e.latlng 
  const newNote = await createNewNote({ title: "新发现的坐标" })
  if (!newNote) return

  const extra = { mapPos: { y: lat, x: lng } }
  newNote.extraData = JSON.stringify(extra)
  emit('open-editor', newNote.id)
}

onMounted(() => {
  initMap()
  syncMarkers()
})

watch(
  () => props.bgUrl,
  (newUrl) => {
    // 如果有了新链接，且地图已经初始化好
    if (newUrl && map.value) {
      const newBounds = props.bgBounds || MAP_BOUNDS

      // 卸载掉默认图
      if (currentOverlay.value) {
        map.value.removeLayer(currentOverlay.value)
      }

      // 铺上数据库里拿到的真实图片
      currentOverlay.value = L.imageOverlay(newUrl, newBounds).addTo(map.value)
      map.value.fitBounds(newBounds)
    }
  }
)

onUnmounted(() => {
  if (map.value) {
    map.value.remove()
  }
})
</script>

<style scoped>
.spirit-map-wrapper {
  position: relative;
  width: 100%;
  height: 100vh;
  background: #1d1d1f;
}

.leaflet-container {
  width: 100%;
  height: 100%;
  z-index: 1;
}

.map-toolbar {
  position: absolute;
  top: 16px;
  left: 50%;
  transform: translateX(-50%);
  background: rgba(255, 255, 255, 0.9);
  backdrop-filter: blur(10px);
  padding: 8px 16px;
  border-radius: 20px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  z-index: 1000;
  
  pointer-events: auto; 
  display: flex;
  align-items: center;
  gap: 12px;
}

.hint {
  font-size: 12px;
  font-weight: 600;
  color: #1d1d1f;
  pointer-events: none;
}

.upload-btn {
  padding: 6px 12px;
  background: #1d1d1f;
  color: white;
  border: none;
  border-radius: 12px;
  font-size: 12px;
  font-weight: bold;
  cursor: pointer;
  transition: all 0.2s ease;
}

.upload-btn:hover:not(:disabled) {
  background: #0066cc;
  transform: translateY(-1px);
}

/* 👇 新增：上传中状态的样式 */
.upload-btn.is-loading {
  background: #86868b;
  cursor: not-allowed;
  transform: none;
}

:deep(.custom-spirit-pin) {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: flex-end;
  background: transparent;
  border: none;
}

:deep(.custom-spirit-pin .pin-visual) {
  width: 12px;
  height: 12px;
  background: #0066cc;
  border: 2px solid white;
  border-radius: 50%;
  box-shadow: 0 0 10px rgba(0, 102, 204, 0.6);
  transition: transform 0.2s;
}

:deep(.custom-spirit-pin .pin-title) {
  margin-bottom: 4px;
  padding: 4px 8px;
  background: rgba(255, 255, 255, 0.95);
  border-radius: 6px;
  font-size: 12px;
  font-weight: 600;
  color: #1d1d1f;
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.15);
  white-space: nowrap;
  pointer-events: none;
}

:deep(.custom-spirit-pin:hover .pin-visual) {
  transform: scale(1.5);
  background: #34c759;
  box-shadow: 0 0 15px rgba(52, 199, 89, 0.6);
}
</style>