// src/composables/世界观管理/useCardUpload.ts
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import { useCos } from '@/composables/useCos'

export function useCardUpload() {
  const { uploadFile } = useCos()
  const uploadingCover = ref(false)
  const uploadingGallery = ref(false)

  const uploadCover = async (file: File): Promise<string | null> => {
    if (!file.type.startsWith('image/')) {
      ElMessage.warning('请上传图片')
      return null
    }
    if (file.size > 5 * 1024 * 1024) {
      ElMessage.warning('最大5MB')
      return null
    }
    uploadingCover.value = true
    try {
      const result = await uploadFile(file, 'world/covers')
      ElMessage.success('上传成功')
      return result.url
    } catch (error) {
      ElMessage.error('上传失败')
      return null
    } finally {
      uploadingCover.value = false
    }
  }

  const uploadGallery = async (files: FileList): Promise<string[]> => {
    uploadingGallery.value = true
    try {
      const uploadPromises = Array.from(files).map(file =>
        uploadFile(file, 'world/gallery')
      )
      const results = await Promise.all(uploadPromises)
      const urls = results.map(r => r.url)
      ElMessage.success(`成功上传 ${urls.length} 张图片`)
      return urls
    } catch (error) {
      ElMessage.error('部分图片上传失败')
      return []
    } finally {
      uploadingGallery.value = false
    }
  }

  return {
    uploadingCover,
    uploadingGallery,
    uploadCover,
    uploadGallery,
  }
}