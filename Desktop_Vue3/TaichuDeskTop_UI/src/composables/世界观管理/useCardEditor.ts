// src/composables/世界观管理/useCardEditor.ts
import { ref, computed, nextTick, unref, type Ref } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useWorldStore } from '@/stores/world'
import type { CardSavePayload, CardSummary } from '@/api/types/world'
import type { CardForm } from './useCardForm'
import { type CardType } from '@/views/世界观管理/card_type'
import type { AttributeItem } from '@/views/世界观管理/card_type'

export function useCardEditor(
  form: Ref<CardForm>,
  resetForm: () => void,
  setFormData: (data: Partial<CardForm>) => void,
  checkQuota: () => Promise<boolean>,
  projectId: Ref<string> | string = ''
) {
  const route = useRoute()
  const store = useWorldStore()

  const saving = ref(false)
  const loading = ref(false)
  let loadVersion = 0
  const isCreating = ref(true)

  // ✅ 当前正在编辑的卡片 ID（来自路由或 cardData prop）
  const currentCardId = ref<string>('')

  // 优先使用传入的 projectId，如果没有则从路由获取
  const routeProjectId = computed(() => (route.params.projectId as string) || unref(projectId))
  const routeCardId = computed(() => route.params.cardId as string | undefined)
  const isEditMode = computed(() => !!routeCardId.value || !!unref(projectId))

  const startCreating = () => {
    loadVersion++
    resetForm()
    isCreating.value = true
    currentCardId.value = ''
    loading.value = false
  }

  // ===== 加载卡片数据 =====
  const loadCardData = async (cardData?: Pick<CardSummary, 'id'> | null) => {
    const version = ++loadVersion
    const cardId = routeCardId.value || cardData?.id
    if (!cardId) {
      startCreating()
      return
    }

    // ✅ 记住当前卡片 ID
    currentCardId.value = cardId

    const pid = routeProjectId.value
    if (!pid) {
      throw new Error('缺少 projectId')
    }

    loading.value = true
    try {
      const fullCard = await store.fetchCardDetail(pid, cardId)
      if (version !== loadVersion) return

      if (!fullCard) {
        throw new Error('卡片数据为空')
      }

      const rawAttributes = fullCard.attributes || []
      const attributes: AttributeItem[] = rawAttributes.map((attr) => ({
        key: attr.key,
        value: attr.value,
        type: (['short', 'long', 'number', 'date', 'boolean'].includes(attr.type) ? attr.type : 'short') as AttributeItem['type'],
      }))

      const relations = (fullCard.outRelations || []).map((r) => ({
        targetCardId: r.targetCardId,
        relationType: r.relationType,
      }))

      // ✅ 解析 content 字符串 → 对象
      let parsedContent: Record<string, any> = {}
      try {
        const raw = fullCard.content
        if (raw) {
          const parsed = JSON.parse(raw)
          if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
            parsedContent = parsed
          }
        }
      } catch (err) {
        console.warn('解析 content 失败，使用空对象:', err)
        parsedContent = {}
      }

      setFormData({
        title: fullCard.title || '',
        type: fullCard.type as CardType,
        coverImage: fullCard.coverImage || '',
        galleryImages: fullCard.galleryImages || [],
        attributes,
        description: fullCard.description || '',
        content: parsedContent,   // ✅ 对象
        tags: Array.isArray(fullCard.tags) ? fullCard.tags : [],
        relations,
        contentBlocks: fullCard.contentBlocks || [],
        timelineEvents: fullCard.timelineEvents || [],
      })

      await nextTick()
      if (version === loadVersion) isCreating.value = false
    } catch (error) {
      if (version !== loadVersion) return
      console.error('加载卡片数据失败:', error)
      ElMessage.error('加载卡片数据失败')
      resetForm()
      isCreating.value = true
      currentCardId.value = ''
    } finally {
      if (version === loadVersion) loading.value = false
    }
  }

  // ===== 保存卡片 =====
  const handleSave = async (onSaved: () => void) => {
    if (saving.value) return
    if (!form.value.title.trim()) {
      ElMessage.warning('请输入标题')
      return
    }

    saving.value = true
    try {
      if (isCreating.value) {
        const canCreate = await checkQuota()
        if (!canCreate) {
          ElMessage.warning('当前世界卡片数量已达上限，请扩容')
          return
        }
      }

      const cardPayload: CardSavePayload = {
        title: form.value.title.trim(),
        type: form.value.type,
        coverImage: form.value.coverImage,
        galleryImages: form.value.galleryImages,
        attributes: form.value.attributes.map(attribute => ({ ...attribute, value: String(attribute.value ?? '') })),
        description: form.value.description.trim(),
        content: JSON.stringify(form.value.content || {}),   // ✅ 对象 → 字符串
        tags: form.value.tags,
        contentBlocks: form.value.contentBlocks,
        timelineEvents: form.value.timelineEvents,
        relations: form.value.relations,
      }

      if (isCreating.value) {
        await store.createCard(routeProjectId.value, cardPayload)
        ElMessage.success('已创建')
      } else {
        if (!currentCardId.value) throw new Error('缺少卡片 ID')
        await store.updateCard(currentCardId.value, cardPayload)
        ElMessage.success('已更新')
      }

      onSaved()
    } catch (error: any) {
      console.error('保存失败:', error)
      if (error?.response?.data?.code === 'CARD_LIMIT_EXCEEDED') {
        ElMessage.warning({
          message: error.response.data.message || '卡片数量已达上限，请扩容',
          duration: 5000,
          showClose: true,
        })
      } else {
        ElMessage.error('保存失败')
      }
    } finally {
      saving.value = false
    }
  }

  // ===== 删除卡片 =====
  const handleDelete = async (onDeleted: () => void) => {
    try {
      await ElMessageBox.confirm('确定删除吗？', '提示', { type: 'warning' })
      const cardId = currentCardId.value
      if (!cardId) {
        throw new Error('缺少卡片 ID')
      }
      await store.deleteCard(cardId)
      ElMessage.success('已删除')
      onDeleted()
    } catch (error) {
      if (error !== 'cancel') console.error(error)
    }
  }

  return {
    saving,
    loading,
    isCreating,
    routeProjectId,
    routeCardId,
    currentCardId,
    isEditMode,
    startCreating,
    loadCardData,
    handleSave,
    handleDelete,
  }
}
