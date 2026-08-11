// src/composables/世界观管理/useCardEditor.ts
import { ref, computed, nextTick } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useWorldStore } from '@/stores/world'
import { worldApi } from '@/api/worldApi'
import { type CardType } from '@/views/世界观管理/card_type'
import type { AttributeItem } from '@/views/世界观管理/card_type'

export function useCardEditor(
  form: any,
  resetForm: () => void,
  setFormData: (data: any) => void,
  checkQuota: () => Promise<boolean>,
  // 新增：显式传入 projectId，优先使用
  projectId?: string
) {
  const route = useRoute()
  const store = useWorldStore()

  const saving = ref(false)
  const loading = ref(false)
  const isCreating = ref(true)

  // 优先使用传入的 projectId，如果没有则从路由获取
  const routeProjectId = computed(() => (route.params.projectId as string) || projectId || '')
  const routeCardId = computed(() => route.params.cardId as string | undefined)
  const isEditMode = computed(() => !!routeCardId.value || !!projectId)  // 如果有 projectId 表示处于编辑状态

  // ===== 加载卡片数据 =====
  const loadCardData = async (cardData?: any) => {
    const cardId = routeCardId.value || cardData?.id
    if (!cardId) {
      resetForm()
      isCreating.value = true
      return
    }

    // 如果有传入的 projectId，优先使用，否则用路由中的
    const projectId = routeProjectId.value
    if (!projectId) {
      throw new Error('缺少 projectId')
    }

    loading.value = true
    try {
      await store.fetchCardDetail(projectId, cardId)
      const fullCard = store.currentCard

      if (!fullCard) {
        throw new Error('卡片数据为空')
      }

      // 🔍 调试日志
      console.log('🔍 fullCard.outRelations:', fullCard.outRelations)

      const rawAttributes = fullCard.attributes || []
      const attributes: AttributeItem[] = rawAttributes.map((attr: any) => ({
        key: attr.key,
        value: attr.value,
        type: attr.type || 'short'
      }))

      const relations = (fullCard.outRelations || []).map((r: any) => ({
        targetCardId: r.targetCardId,
        relationType: r.relationType,
      }))

      console.log('🔍 映射后的 relations:', relations)

      setFormData({
        title: fullCard.title || '',
        type: fullCard.type as CardType,
        coverImage: fullCard.coverImage || '',
        galleryImages: fullCard.galleryImages || [],
        attributes,
        description: fullCard.description || '',
        content: fullCard.content || '{}',
        tags: Array.isArray(fullCard.tags) ? fullCard.tags : [],
        relations,
        contentBlocks: fullCard.contentBlocks || [],
      })

      await nextTick()
      console.log('✅ form.relations 已设置:', form.value.relations)

      isCreating.value = false
    } catch (error) {
      console.error('加载卡片数据失败:', error)
      ElMessage.error('加载卡片数据失败')
      resetForm()
      isCreating.value = true
    } finally {
      loading.value = false
    }
  }

  // ===== 保存卡片 =====
  const handleSave = async (onSaved: () => void) => {
    if (!form.value.title.trim()) {
      ElMessage.warning('请输入标题')
      return
    }

    // 检查配额（仅创建时）
    if (isCreating.value) {
      const canCreate = await checkQuota()
      if (!canCreate) {
        ElMessage.warning('当前世界卡片数量已达上限，请扩容')
        return
      }
    }

    const cardPayload = {
      title: form.value.title.trim(),
      type: form.value.type,
      coverImage: form.value.coverImage,
      galleryImages: form.value.galleryImages,
      attributes: form.value.attributes,
      description: form.value.description.trim(),
      content: form.value.content || '{}',
      tags: form.value.tags,
    }

    let toRemove: any[] = []
    let toAdd: { targetCardId: string; relationType: string }[] = []

    if (!isCreating.value) {
      const cardId = routeCardId.value
      if (!cardId) throw new Error('缺少卡片 ID')

      let existingRelations = store.getCardDetailById(cardId)?.outRelations || []
      if (existingRelations.length === 0) {
        // 这里需要 projectId，使用 routeProjectId
        await store.fetchCardDetail(routeProjectId.value, cardId)
        existingRelations = store.getCardDetailById(cardId)?.outRelations || []
      }

      const newRelations = form.value.relations || []

      toRemove = existingRelations.filter((old: any) =>
        !newRelations.some((n: { targetCardId: string; relationType: string }) =>
          n.targetCardId === old.targetCardId
        )
      )
      toAdd = newRelations.filter((n: { targetCardId: string; relationType: string }) =>
        !existingRelations.some((old: any) => old.targetCardId === n.targetCardId)
      )
    }

    saving.value = true
    try {
      let cardId = routeCardId.value

      if (isCreating.value) {
        const newCard = await store.createCard(routeProjectId.value, cardPayload)
        cardId = newCard.id
        for (const rel of form.value.relations || []) {
          await store.addRelation(cardId, rel.targetCardId, rel.relationType)
        }
        ElMessage.success('已创建')
      } else {
        const existingCardId = routeCardId.value
        if (!existingCardId) throw new Error('缺少卡片 ID')
        await store.updateCard(existingCardId, cardPayload)
        for (const rel of toRemove) {
          await store.removeRelation(existingCardId, rel.id)
        }
        for (const rel of toAdd) {
          await store.addRelation(existingCardId, rel.targetCardId, rel.relationType)
        }
        await store.fetchCardDetail(routeProjectId.value, existingCardId, true)
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
      const cardId = routeCardId.value
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
    isEditMode,
    loadCardData,
    handleSave,
    handleDelete,
  }
}