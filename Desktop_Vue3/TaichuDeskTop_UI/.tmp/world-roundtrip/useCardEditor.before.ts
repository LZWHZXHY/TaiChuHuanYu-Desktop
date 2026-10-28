// src/composables/世界观管理/useCardEditor.ts
import { ref, computed, unref, onScopeDispose, watch, type Ref } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { useUserStore } from '@/stores/user'
import { useCardDraft } from './useCardDraft'
import { useWorldStore } from '@/stores/world'
import type { CreateCardPayload, CardSummary } from '@/api/types/world'
import { cardToForm, mergeCardForms, type CardForm } from './useCardForm'

export function useCardEditor(
  form: Ref<CardForm>,
  resetForm: () => void,
  setFormData: (data: Partial<CardForm>) => void,
  checkQuota: () => Promise<boolean>,
  projectId: Ref<string> | string = ''
) {
  const route = useRoute()
  const store = useWorldStore()

  const draft = useCardDraft(form)
  watch(() => useUserStore().token, () => { loadVersion++; draft.suspend(); resetConflict(); resetForm(); loadError.value = true }, { flush: 'sync' })

  const saving = ref(false)
  const loading = ref(false)
  const loadError = ref(false)
  let loadVersion = 0
  const isCreating = ref(true)
  const conflicts = ref<(keyof CardForm)[]>([])
  const conflictChoices = ref<Partial<Record<keyof CardForm, 'local' | 'server'>>>({})
  const remoteForm = ref<CardForm | null>(null)
  let remoteVersion = ''
  let mergeResult: CardForm | null = null
  function resetConflict() { conflicts.value = []; conflictChoices.value = {}; remoteForm.value = null; mergeResult = null }

  const snapshot = () => JSON.stringify(form.value)
  const savedSnapshot = ref(snapshot())
  const markClean = () => { savedSnapshot.value = snapshot() }
  const isDirty = computed(() => !loading.value && !loadError.value && snapshot() !== savedSnapshot.value)

  // 仅显式传入的条目决定编辑目标，阅读页路由不能覆盖新建表单。
  const currentCardId = ref<string>('')

  // 优先使用传入的 projectId，如果没有则从路由获取
  const routeProjectId = computed(() => unref(projectId) || String(route.params.projectId || route.params.id || ''))
  const routeCardId = computed(() => currentCardId.value || undefined)

  const startCreating = () => {
    loadVersion++
    draft.suspend()
    resetConflict()
    resetForm()
    isCreating.value = true
    currentCardId.value = ''
    loading.value = false
    loadError.value = false
    markClean()
    draft.bind(routeProjectId.value, '')
  }

  // ===== 加载卡片数据 =====
  const loadCardData = async (cardData?: Pick<CardSummary, 'id'> | null) => {
    const version = ++loadVersion
    draft.suspend()
    resetConflict()
    const cardId = cardData?.id
    if (!cardId) {
      startCreating()
      return
    }

    // ✅ 记住当前卡片 ID
    currentCardId.value = cardId
    isCreating.value = false
    loadError.value = false

    const pid = routeProjectId.value
    if (!pid) {
      throw new Error('缺少 projectId')
    }

    loading.value = true
    try {
      // 表单使用独立副本，未保存的图库、引用和时间线不能改动阅读缓存。
      const fullCard = JSON.parse(JSON.stringify(await store.fetchCardDetail(pid, cardId)))
      if (version !== loadVersion) return

      if (!fullCard) {
        throw new Error('卡片数据为空')
      }

      setFormData(cardToForm(fullCard))
      markClean()
      draft.bind(pid, cardId)

    } catch (error) {
      if (version !== loadVersion) return
      console.error('加载卡片数据失败:', error)
      loadError.value = true
    } finally {
      if (version === loadVersion) loading.value = false
    }
  }

  function resolveConflicts() {
    if (!remoteForm.value || !mergeResult || conflicts.value.some(key => !conflictChoices.value[key])) return
    for (const key of conflicts.value) {
      if (conflictChoices.value[key] === 'server') Object.assign(mergeResult, { [key]: remoteForm.value[key] })
    }
    const baseline = JSON.stringify(remoteForm.value)
    form.value = JSON.parse(JSON.stringify(mergeResult))
    savedSnapshot.value = baseline
    draft.rebase(baseline)
    resetConflict()
  }
  async function prepareUpdate(version: number) {
    const latest = await store.fetchCardDetail(routeProjectId.value, currentCardId.value, true)
    if (version !== loadVersion) return false
    const remote = cardToForm(latest)
    let base: CardForm | null = null
    try { base = JSON.parse(draft.baseSnapshot.value || savedSnapshot.value) } catch { /* 旧草稿无有效基线时必须人工核对 */ }
    const result = mergeCardForms(base, form.value, remote)
    remoteVersion = latest.updatedAt
    if (result.conflicts.length) {
      remoteForm.value = remote; mergeResult = result.merged
      conflicts.value = result.conflicts; conflictChoices.value = {}
      return false
    }
    form.value = result.merged
    savedSnapshot.value = JSON.stringify(remote)
    draft.rebase(savedSnapshot.value)
    return true
  }

  // ===== 保存卡片 =====
  const handleSave = async (onSaved: (cardId: string) => void) => {
    if (saving.value || loading.value || loadError.value || draft.pending.value || conflicts.value.length) return
    if (!form.value.title.trim()) {
      ElMessage.warning('请输入标题')
      return
    }

    saving.value = true
    const version = loadVersion
    const pid = routeProjectId.value
    try {
      if (isCreating.value) {
        const canCreate = await checkQuota()
        if (version !== loadVersion) return
        if (!canCreate) {
          ElMessage.warning('当前世界卡片数量已达上限，请扩容')
          return
        }
      }

      if (!isCreating.value && !await prepareUpdate(version)) return

      const cardPayload: CreateCardPayload = {
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
        const created = await store.createCard(pid, cardPayload)
        if (version !== loadVersion) return
        currentCardId.value = created.id
        isCreating.value = false
        ElMessage.success('已创建')
      } else {
        if (!currentCardId.value) throw new Error('缺少卡片 ID')
        await store.updateCard(currentCardId.value, { ...cardPayload, expectedUpdatedAt: remoteVersion })
        ElMessage.success('已更新')
      }

      if (version === loadVersion) { await draft.acceptSaved(); if (version !== loadVersion) return; markClean(); onSaved(currentCardId.value) }
    } catch (error: any) {
      console.error('保存失败:', error)
      if (version !== loadVersion) return
      if (error?.response?.status === 409 && error?.response?.data?.code === 'WORLD_CARD_CONFLICT') {
        ElMessage.warning('条目刚被其他页面更新，本地草稿已保留。再次保存时会核对最新内容。')
      } else if (error?.response?.data?.code === 'CARD_LIMIT_EXCEEDED') {
        ElMessage.warning({
          message: error.response.data.message || '卡片数量已达上限，请扩容',
          duration: 5000,
          showClose: true,
        })
      } else {
        ElMessage.error(error?.response?.data?.message || '保存失败，填写的内容已保留，请重试')
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
      await draft.acceptSaved()
      markClean()
      ElMessage.success('已删除')
      onDeleted()
    } catch (error) {
      if (error !== 'cancel') console.error(error)
    }
  }

  onScopeDispose(() => { loadVersion++ })
  return {
    draft,
    conflicts, conflictChoices, remoteForm, resolveConflicts,
    isDirty,
    markClean,
    saving,
    loading,
    loadError,
    isCreating,
    routeProjectId,
    routeCardId,
    currentCardId,
    startCreating,
    loadCardData,
    handleSave,
    handleDelete,
  }
}
