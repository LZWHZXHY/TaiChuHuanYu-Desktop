from pathlib import Path
import re
root=Path.cwd()
backup=root/'.tmp/finish-editors/before'
def write(name,text):
    p=root/name; b=backup/name
    if p.exists() and not b.exists():
        b.parent.mkdir(parents=True,exist_ok=True); b.write_bytes(p.read_bytes())
    p.parent.mkdir(parents=True,exist_ok=True); p.write_text(text,encoding='utf-8')
def edit(name,fn): write(name,fn((root/name).read_text(encoding='utf-8-sig')))

# 普通详情沿用按图片数量决定的排版；大图展览是独立的第二层。
p='src/views/画廊展厅/ArtworkDetail.vue'
s=(root/p).read_text(encoding='utf-8-sig')
s=s.replace('<div class="md-viewport ', '<div :inert="viewerIndex !== null" :aria-hidden="viewerIndex !== null" class="md-viewport ')
s=s.replace('<option value="auto">展台</option>','<option value="auto">自动</option><option value="carousel">逐张查看</option>')
start=s.index('        <section class="md-visual"')
end=s.index('        <article ref=',start)
s=s[:start]+'''        <section class="md-visual" aria-label="作品图片">
          <div v-if="!steps.length" class="img-placeholder">暂无可展示的图片</div>
          <div v-else-if="activeMode === 'timeline' || activeMode === 'masonry'" :class="activeMode === 'timeline' ? 'process-timeline' : 'masonry-gallery'">
            <figure v-for="(step, idx) in steps" :key="idx" class="exhibit-frame">
              <button class="image-open" :aria-label="`全屏查看第 ${idx + 1} 张图片`" @click="openImage(idx, $event)"><img :src="step.imageUrl" :alt="step.description || artwork.title" :loading="idx ? 'lazy' : undefined" @error="onImgError" /></button>
              <figcaption v-if="step.description">{{ step.description }}</figcaption>
            </figure>
          </div>
          <div v-else>
            <button class="image-open" aria-label="全屏查看当前图片" @click="openImage(currentCarouselIndex, $event)"><img :src="currentStep.imageUrl" :alt="currentStep.description || artwork.title" @error="onImgError" /></button>
            <p v-if="currentStep.description" class="image-caption">{{ currentStep.description }}</p>
            <div v-if="steps.length > 1" class="carousel-thumbs">
              <button v-for="(item, idx) in steps" :key="idx" :aria-label="`查看第 ${idx + 1} 张画幅`" :aria-pressed="idx === currentCarouselIndex" :class="['thumb-btn', { active: idx === currentCarouselIndex }]" @click="currentCarouselIndex = idx"><img :src="item.imageUrl" alt="" loading="lazy" /></button>
            </div>
          </div>
        </section>

'''+s[end:]
insert='''    <div v-if="viewerIndex !== null && artwork" ref="imageViewer" class="art-image-viewer" role="dialog" aria-modal="true" aria-label="全屏图片" tabindex="-1" @click.self="closeImage">
      <img class="viewer-image" :src="viewerStep?.imageUrl" :alt="viewerStep?.description || artwork.title" @error="onImgError" />
      <button class="viewer-close" aria-label="关闭全屏图片" @click="closeImage">×</button>
      <div class="viewer-caption"><span>{{ (viewerIndex + 1).toString().padStart(2, '0') }} / {{ steps.length.toString().padStart(2, '0') }}</span><strong>{{ artwork.title }}</strong><small>{{ artwork.author?.username || '无名漫游者' }}<template v-if="artwork.watermarkEnabled"> · 水印保护</template></small><p>{{ viewerStep?.description || parsedContent?.map(block => block.text).join(' ') }}</p></div>
      <div v-if="steps.length > 1" class="viewer-paging"><button aria-label="上一张图片" :disabled="viewerIndex === 0" @click="changeImage(-1)">←</button><button aria-label="下一张图片" :disabled="viewerIndex === steps.length - 1" @click="changeImage(1)">→</button></div>
    </div>
'''
s=s.replace('    <ArtworkPublishModal',insert+'    <ArtworkPublishModal')
s=s.replace('<h2 class="title">{{ artwork.title }}</h2>','<h1 class="title">{{ artwork.title }}</h1>')
s=s.replace("const defaultDisplayMode = () => props.mode === 'carousel' ? 'auto' : props.mode || 'auto'","const defaultDisplayMode = () => props.mode || 'auto'")
s=s.replace("const activeMode = computed(() => displayMode.value === 'auto' ? 'carousel' : displayMode.value)",'''const activeMode = computed(() => displayMode.value !== 'auto' ? displayMode.value : steps.value.length <= 1 ? 'single' : steps.value.length <= 3 ? 'timeline' : 'masonry')
const viewerIndex = ref<number | null>(null)
const imageViewer = ref<HTMLElement>()
const viewerStep = computed(() => viewerIndex.value === null ? null : steps.value[viewerIndex.value])
let imageTrigger: HTMLElement | null = null
function openImage(index: number, event?: MouseEvent) {
  imageTrigger = event?.currentTarget as HTMLElement | null
  viewerIndex.value = index
  nextTick(() => imageViewer.value?.focus())
}
function closeImage() {
  viewerIndex.value = null
  nextTick(() => imageTrigger?.isConnected ? imageTrigger.focus() : dialog.value?.focus())
}
function changeImage(direction: number) {
  if (viewerIndex.value === null) return
  viewerIndex.value = Math.max(0, Math.min(steps.value.length - 1, viewerIndex.value + direction))
  nextTick(() => { if (document.activeElement === document.body) imageViewer.value?.focus() })
}''')
s=s.replace("if (event.key === 'Escape') { event.preventDefault(); emit('close'); return }","if (event.key === 'Escape') { event.preventDefault(); viewerIndex.value !== null ? closeImage() : emit('close'); return }")
s=s.replace("if (event.key === 'Tab' && dialog.value) {","const activeDialog = viewerIndex.value !== null ? imageViewer.value : dialog.value\n  if (event.key === 'Tab' && activeDialog) {")
s=s.replace("dialog.value.querySelectorAll<HTMLElement>","activeDialog.querySelectorAll<HTMLElement>").replace("target === dialog.value","target === activeDialog")
s=s.replace("|| activeMode.value !== 'carousel'", "|| (viewerIndex.value === null && activeMode.value !== 'carousel')")
s=s.replace("changeSlide(event.key === 'ArrowLeft' ? -1 : 1)","(viewerIndex.value !== null ? changeImage : changeSlide)(event.key === 'ArrowLeft' ? -1 : 1)")
s=s.replace("editing.value = false; artwork.value = null;", "viewerIndex.value = null; editing.value = false; artwork.value = null;")
# 保留正文样式，换回随主题的详情布局。
css=s[s.index('.md-article {'):s.index('.md-loading-bar {')]
s=s[:s.index('<style scoped>')]+'''<style scoped>
.md-detail-mask { position: fixed; inset: 0; z-index: 9999; overflow-y: auto; overscroll-behavior: contain; background: var(--bg-main); outline: none; }
.md-detail-mask:has(.art-image-viewer) { overflow: hidden; }
.md-viewport { width: 100%; max-width: 1200px; padding: 0 24px 48px; margin: auto; box-sizing: border-box; }
.md-viewport .md-controls { position: sticky; top: 0; z-index: 10; display: flex; align-items: center; flex-wrap: wrap; gap: 12px; padding: 14px 0; margin-bottom: 24px; background: var(--bg-main); border-bottom: 1px solid var(--border-line); }
.md-controls :is(button,a,select) { font: inherit; font-size: 13px; color: var(--text-primary); background: var(--paper-card); border: 1px solid var(--border-line); padding: 8px 12px; border-radius: var(--radius-sm); cursor: pointer; text-decoration: none; }
.md-controls :is(button,a,select):focus-visible, .md-visual button:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 3px; }
.exhibit-layout { display: flex; align-items: center; gap: 8px; font-size: 12px; color: var(--text-secondary); margin-left: auto; }
.md-visual { max-width: 960px; margin: 0 auto 32px; }
.image-open { display: block; border: 0; padding: 0; width: 100%; background: var(--bg-sub); cursor: zoom-in; }
.image-open img { display: block; width: 100%; max-height: 80vh; object-fit: contain; margin: auto; }
.exhibit-frame { margin: 0 0 24px; break-inside: avoid; }.image-caption,.exhibit-frame figcaption { padding: 10px 0; font-size: 13px; color: var(--text-secondary); white-space: pre-wrap; }
.masonry-gallery { columns: 2; gap: 24px; }.masonry-gallery img { max-height: none; }
.carousel-thumbs { display: flex; gap: 10px; overflow-x: auto; padding: 12px 2px; }
.thumb-btn { width: 64px; height: 56px; flex-shrink: 0; padding: 3px; border: 1px solid var(--border-line); background: var(--paper-card); cursor: pointer; }.thumb-btn.active { border-color: var(--accent-color); }.thumb-btn img { width: 100%; height: 100%; object-fit: contain; }
.art-image-viewer { position: fixed; inset: 0; z-index: 30; background: rgb(3 7 11 / .94); backdrop-filter: blur(8px); display: grid; place-items: center; outline: none; color: #fff; }
.viewer-image { width: 100%; height: 100%; max-height: 100dvh; object-fit: contain; }
.viewer-close,.viewer-paging button { border: 1px solid #667b88; background: rgb(10 20 28 / .8); color: #fff; width: 44px; height: 44px; cursor: pointer; font-size: 22px; }
.viewer-close { position: absolute; right: 20px; top: 20px; }.viewer-paging { position: absolute; right: 24px; bottom: 24px; display: flex; gap: 10px; }.viewer-paging button:disabled { opacity: .35; cursor: default; }
.art-image-viewer button:focus-visible { outline: 2px solid #77dbec; outline-offset: 3px; }
.art-image-viewer button:not(:disabled):hover { background: #254d5a; }
.viewer-caption { position: absolute; left: 0; bottom: 0; padding: 40px 28px 24px; max-width: min(600px,calc(100% - 156px)); background: linear-gradient(transparent,rgb(0 0 0 / .8)); pointer-events: none; overflow-wrap: anywhere; }
.viewer-caption span,.viewer-caption small { display: block; font-size: 11px; color: #d2dce4; }.viewer-caption strong { display: block; font-size: 17px; margin: 8px 0; }.viewer-caption p { font-size: 12px; line-height: 1.7; margin: 8px 0 0; display: -webkit-box; -webkit-line-clamp: 3; -webkit-box-orient: vertical; overflow: hidden; }
'''+css.replace('color: #d5e0eb','color: var(--text-secondary)')+'''.md-loading-bar { position: fixed; top: 0; height: 2px; width: 100%; background: var(--accent-color); z-index: 10000; }
@media(max-width:640px) { .md-viewport { padding: 0 12px 24px; }.exhibit-layout { order: 2; margin: 0; width: 100%; }.md-article { padding: 16px; }.masonry-gallery { columns: 1; }.viewer-caption { padding: 30px 16px 16px; }.viewer-close { right: 12px; top: 12px; }.viewer-paging { right: 12px; bottom: 16px; } }
</style>
'''
write(p,s)

# 小说信息/章节窗口复用现有编辑面板，不在阅读正文内加入动画。
for name in ['index.vue','Detail.vue']:
    p='src/views/世界观管理/Novels/'+name
    s=(root/p).read_text(encoding='utf-8-sig')
    s=s.replace('content-page"','content-page community-surface tone-world editor-surface"',1)
    s=s.replace('class="content-header"','class="content-header studio-heading"').replace('class="novel-head"','class="novel-head studio-heading"')
    s=s.replace('class="modal"','class="modal workbench-skin" role="dialog" aria-modal="true" aria-label="小说信息"')
    s=s.replace('class="modal wide"','class="modal wide workbench-skin" role="dialog" aria-modal="true" aria-label="章节编辑"')
    s=s.replace('class="modal-title"','class="modal-title studio-heading"')
    s=s.replace('border-radius: 14px;', 'border-radius: var(--radius-sm); border: 1px solid var(--surface-edge);')
    s=s.replace('max-height: 90vh;', 'max-height: calc(100dvh - 40px); box-sizing: border-box;')
    s=s.replace('min-height: 380px;', 'min-height: min(380px,45dvh);')
    s=s.replace('class="modal-actions"','class="modal-actions editor-footer"')
    s=s.replace('</style>','''.modal-actions { position: sticky; bottom: -24px; padding: 16px 0; background: var(--paper-card); border-top: 1px solid var(--border-line); }
.modal .field input,.modal .field textarea,.modal .field select { min-width: 0; width: 100%; box-sizing: border-box; }
@media(max-width:640px) { .mask { padding: 12px; }.modal { padding: 16px; max-height: calc(100dvh - 24px); }.modal-actions { bottom: -16px; }.field-row { grid-template-columns: 1fr; } }
</style>''')
    if name=='Detail.vue':
        s=s.replace('<label class="field"><span>标题 *</span>', '<p v-if="error" class="err" role="alert">{{ error }}</p>\n        <label class="field"><span>标题 *</span>',1)
    write(p,s)

for name,rootclass,head,tone in [
 ('src/views/太虚游/Custom/index.vue','custom-studio','studio-header','game'),
 ('src/views/太虚游/Custom/components/CreateQuestionnaire.vue','create-questionnaire','create-header','game'),
 ('src/views/太虚游/Custom/components/CreateSimulation.vue','create-simulation','create-header','game'),
 ('src/views/太初灵脉/components/WorkspaceCanvas.vue','workspace-canvas-frame','canvas-header','world'),
 ('src/views/太初灵脉/components/WorkspaceSchedule.vue','workspace-schedule-frame','schedule-header','world')]:
    s=(root/name).read_text(encoding='utf-8-sig')
    s=s.replace(f'class="{rootclass}"',f'class="{rootclass} workbench-skin editor-surface community-surface tone-{tone}"',1)
    s=s.replace(f'class="{head}"',f'class="{head} studio-heading"')
    if 'Custom' in name:
        s=s.replace('var(--ink-black)', 'var(--text-primary)').replace('var(--ink-gray)', 'var(--text-secondary)').replace('var(--line-raw)','var(--border-line)').replace('var(--paper-sub)','var(--bg-sub)').replace('var(--cinnabar)','var(--accent-color)')
        s=s.replace('var(--cinnabar, var(--danger))','var(--accent-color)')
        s=s.replace('font-family: var(--font-family, \'Noto Serif SC\', serif);','font-family: inherit;').replace('letter-spacing: 0.2em;','letter-spacing: .02em;').replace('border-radius: 30px;','border-radius: var(--radius-sm);')
        s=s.replace('class="select-header"','class="select-header studio-heading"')
    if 'CreateQuestionnaire' in name:
        s=s.replace('>✕</button>', ' aria-label="删除当前项">✕</button>')
        s=s.replace('</style>','''.create-form > .form-group:not(.questions-group):not(.results-group) { padding: 0 16px; }
.question-item,.result-item { border-left: 3px solid var(--accent-color); border-radius: var(--radius-sm); }
.form-actions { position: sticky; bottom: 0; background: var(--paper-card); padding: 16px; border-top: 1px solid var(--border-line); }
input,select,textarea { min-width: 0; box-sizing: border-box; }
@media(max-width:640px) { .option-item { flex-wrap: wrap; }.option-item input:first-child,.option-item input:nth-child(3) { flex: 1 1 100%; width: 100%; }.question-header,.group-header { flex-wrap: wrap; gap: 8px; }.create-header p { margin-left: 0; }.result-row input { width: 40%; }.q-image-group { flex-wrap: wrap; } }
</style>''')
    if rootclass=='custom-studio':
        s=s.replace('<div\n            v-for="type', '<button type="button"\n            v-for="type').replace(':class="{ \'coming-soon\': type.comingSoon }"',':class="{ \'coming-soon\': type.comingSoon }" :disabled="type.comingSoon"')
        s=s.replace('<span v-if="type.comingSoon" class="type-badge">即将支持</span>\n          </div>','<span v-if="type.comingSoon" class="type-badge">即将支持</span>\n          </button>')
        s=s.replace('</style>','''.custom-studio { box-sizing: border-box; }.type-card { font: inherit; text-align: left; color: var(--text-primary); }.type-card:disabled { cursor: default; }
@media(max-width:640px) { .custom-studio { padding: 12px; }.header-right { width: 100%; }.exp-display { flex-wrap: wrap; white-space: normal; }.type-grid { grid-template-columns: 1fr; } }
</style>''')
    if 'WorkspaceCanvas' in name:
        s=s.replace("import { ref } from 'vue'\n",'').replace('placeholder="未命名图谱 / Canvas Title"','aria-label="画布标题" placeholder="未命名图谱"')
        s=s.replace('多维交织的灵脉思绪星图','拖动节点调整位置，双击编辑内容，点击连线修改关系。')
        s=s.replace('outline: none; }','outline: none; color: var(--text-primary); }')
        s=s.replace('.canvas-container { flex: 1;', '.canvas-container { min-height: 380px; flex: 1;')
        s=s.replace('font-size: 2.2rem;', 'font-size: clamp(20px,3vw,30px);')
    if 'WorkspaceSchedule' in name:
        s=s.replace('placeholder="未命名日程空间"','aria-label="计划表标题" placeholder="未命名计划表"')
        s=s.replace('时间线与任务矩阵的自由流沙交织','在日历安排日期，在看板整理进度；拖动任务即可调整。').replace('📅 时间演化星图','日历').replace('📋 状态演化矩阵','任务看板')
        s=s.replace('@click="adjustMonth(-1)"','aria-label="上个月" @click="adjustMonth(-1)"').replace('@click="adjustMonth(1)"','aria-label="下个月" @click="adjustMonth(1)"')
        s=s.replace('@click="addNewItemIntoDate(day.dateStr)"', ':aria-label="`在 ${day.dateStr} 添加日程`" @click="addNewItemIntoDate(day.dateStr)"')
        s=s.replace('height: 100vh;', 'min-height: 100%;')
        s=s.replace('font-size: 2.6rem;', 'font-size: clamp(20px,3vw,30px);').replace('padding: 0 48px 40px;', 'padding: 20px 24px 32px;')
        s=s.replace('color: item.themeColor,','color: \'var(--text-primary)\',')
        s=s.replace('opacity: 0.25;', 'opacity: 0.6;')
        s=s.replace('</style>','''.calendar-day-cell:focus-within .quick-add-event-btn,.kanban-column:focus-within .add-card-btn,.column-header:focus-within .column-actions-trigger,.schedule-card:focus-within .mini-color-picker { opacity: 1; }
.quick-add-event-btn { min-width: 24px; min-height: 24px; }.calendar-grid-box { overflow-x: auto; }.grid-header-row,.grid-days-container { min-width: 560px; }
@media(hover:none) { .quick-add-event-btn,.add-card-btn,.column-actions-trigger,.mini-color-picker,.strip-mini-color-picker { opacity: 1; } }
@media(max-width:640px) { .schedule-main-stage { padding: 16px 12px; }.section-top-bar { flex-wrap: wrap; gap: 12px; }.kanban-columns-container { display: flex; flex-wrap: nowrap; overflow-x: auto; }.kanban-column { min-width: 240px; flex: 0 0 240px; } }
@media(prefers-reduced-motion:reduce) { *,*::before,*::after { transition: none !important; } }
</style>''')
    write(name,s)

# 画布不再对每一个节点执行模糊，节点和控件随日夜主题切换。
p='src/components/SpiritCanvas.vue'; s=(root/p).read_text(encoding='utf-8-sig')
s=s.replace('pattern-color="#c7c7cc"','pattern-color="var(--border-line)"')
s=s.replace('class="edge-editor-panel"','class="edge-editor-panel workbench-skin editor-surface community-surface tone-world"').replace('class="editor-header"','class="editor-header studio-heading"')
s=s.replace('class="close-btn"','class="close-btn" aria-label="关闭连线设置"')
s=s.replace('<div v-for="color in presetColors"', '<button type="button" :aria-label="`连线颜色 ${color}`" :aria-pressed="edgeColor === color" v-for="color in presetColors"').replace('@click="edgeColor = color; applyEdgeUpdate()">\n              </div>','@click="edgeColor = color; applyEdgeUpdate()">\n              </button>')
# 仅替换样式区，保留用户设置的节点/边颜色及存储数据。
i=s.index('<style scoped>'); head=s[:i]; css=s[i:]
for a,b in {'rgba(255, 255, 255, 0.95)':'var(--paper-card)','rgba(255, 255, 255, 0.85)':'var(--paper-card)','rgba(0, 0, 0, 0.08)':'var(--border-line)','rgba(0, 0, 0, 0.05)':'var(--border-line)','rgba(0, 0, 0, 0.02)':'var(--bg-sub)','#1d1d1f':'var(--text-primary)','#86868b':'var(--text-secondary)','#c7c7cc':'var(--text-secondary)','#d2d2d7':'var(--border-line)','#0066cc':'var(--accent-color)','background: white':'background: var(--paper-card)','border: 2px solid white':'border: 2px solid var(--paper-card)','rgba(250, 245, 255, 0.95)':'var(--accent-soft)','#e0d4f5':'var(--border-line)','#673ab7':'var(--accent-color)'}.items(): css=css.replace(a,b)
css=re.sub(r'\s*backdrop-filter: [^;]+;', '',css)
css=css.replace('border-radius: 16px;', 'border-radius: var(--radius-sm);').replace('border-radius: 12px;','border-radius: var(--radius-sm);')
css=css.replace('  width: 260px;','  width: min(260px,calc(100% - 24px));\n  max-height: calc(100% - 24px); overflow-y: auto; box-sizing: border-box;').replace('  top: 24px;','  top: 12px;').replace('  right: 24px;','  right: 12px;').replace('  overflow: hidden;\n}', '  overflow-x: hidden;\n}')
css=css.replace('</style>','''.control-group input,.control-group select { color: var(--text-primary); }
:deep(.vue-flow__controls-button) { background: var(--paper-card); color: var(--text-primary); border-color: var(--border-line); }:deep(.vue-flow__controls-button svg) { fill: currentColor; }
:deep(.vue-flow__edge-textbg) { fill: var(--paper-card); }:deep(.vue-flow__edge-text) { fill: var(--text-primary); }
button:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 2px; }
@media(hover:none) { :deep(.custom-handle) { opacity: 1; } }
@media(prefers-reduced-motion:reduce) { :deep(*),* { transition: none !important; animation: none !important; } }
</style>''')
write(p,head+css)

# 三套认证表单共用样式，改用明确文案和可键盘操作的切换按钮。
for name in ['LoginForm','RegisterForm','ForgotForm']:
    p=f'src/views/身份认证/{name}.vue'; s=(root/p).read_text(encoding='utf-8-sig')
    s=s[:s.index('<style scoped>')]+'<style scoped src="./auth-form.css"></style>\n'
    s=re.sub(r'<span @click="([^\"]+)">([^<]+)</span>',r'<button type="button" class="text-link" @click="\1">\2</button>',s)
    s=re.sub(r'<label>([^<]+)</label>\s*(<input[^>]+)', lambda m: '<label>'+m[1]+'</label>\n      '+m[2]+' aria-label="'+m[1].split('/')[0].strip()+'"',s)
    s=s.replace('<button @click="submit"','<button class="black-btn" @click="submit"')
    replacements={'账号 / IDENTITY':'账号','输入您的名号':'用户名或邮箱','密钥 / PASSWORD':'密码','输入密钥':'输入密码','正在接入...':'正在登录…','确认接入':'登录','返回接入':'返回登录','绑定地址 / REGISTERED EMAIL':'注册邮箱','校验代码 / VERIFICATION CODE':'邮箱验证码','新设密钥 / NEW PASSWORD':'新密码','不少于6位新密钥':'不少于6位新密码','正在重塑密钥...':'正在重置…','重置身份密印':'重置密码','返回接入界面':'返回登录','新密钥长度':'新密码长度','密印重塑成功！请尝试重新接入':'密码重置成功，请重新登录','重塑失败':'重置失败','验证码已发往灵觉地址':'验证码已发送到邮箱','请填写绑定地址':'请填写注册邮箱','昵称 / USERNAME':'昵称','邮件地址 / EMAIL':'邮箱','验证代码 / VERIFICATION CODE':'邮箱验证码','密钥/ PASSWORD':'密码','认知中...':'正在注册…','开始认证':'注册'}
    for a,b in replacements.items():s=s.replace(a,b)
    s=s.replace('type="password"', 'type="password" autocomplete="'+('current-password' if name=='LoginForm' else 'new-password')+'"')
    s=s.replace('type="email"','type="email" autocomplete="email" aria-label="邮箱"')
    s=s.replace('maxlength="6"','maxlength="6" inputmode="numeric" autocomplete="one-time-code"')
    s=s.replace('<p v-if="errorMsg"','<p role="alert" v-if="errorMsg"').replace('<p v-if="message"','<p role="status" v-if="message"')
    write(p,s)
p='src/views/身份认证/index.vue';s=(root/p).read_text(encoding='utf-8-sig')
s=s.replace('class="auth-page"','class="auth-page workbench-skin editor-surface community-surface tone-world"').replace('class="auth-header"','class="auth-header studio-heading"')
s=s.replace('身份创建 / Register','注册账号').replace('密钥寻回 / Forgot','找回密码').replace('认证接入 / Login','登录社区').replace('太初寰宇 · 身份校验协议 v1.0.4','太初寰宇 · 账号服务')
s=s.replace('max-width: 400px;', 'max-width: 460px; box-sizing: border-box;').replace('margin-bottom: 40px;','margin-bottom: 28px;').replace('margin-top: 60px;', 'margin-top: 28px;')
write(p,s)
write('src/views/身份认证/auth-form.css','''.input-group { margin-bottom: 20px; }
label { display: block; font-size: 13px; color: var(--text-secondary); font-weight: 600; margin-bottom: 8px; }
input { width: 100%; min-width: 0; box-sizing: border-box; padding: 11px 12px; background: var(--paper-card); border: 1px solid var(--border-line); border-radius: var(--radius-sm); color: var(--text-primary); font: inherit; font-size: 14px; }
.email-input-wrapper { display: flex; gap: 8px; }.email-input-wrapper input { flex: 1; }
button { font: inherit; cursor: pointer; border-radius: var(--radius-sm); transition: border-color .18s,background-color .18s; }
.send-code-btn { flex-shrink: 0; padding: 0 12px; background: var(--bg-sub); color: var(--text-primary); border: 1px solid var(--border-line); font-size: 12px; }
.black-btn { width: 100%; padding: 12px; background: var(--accent-color); color: var(--on-accent); border: 1px solid var(--accent-color); font-weight: 600; margin-top: 8px; }
button:not(:disabled):hover { box-shadow: inset 0 -2px var(--signal-color); }.send-code-btn:not(:disabled):hover { border-color: var(--accent-color); }
button:disabled { opacity: .5; cursor: not-allowed; }
input:focus,button:focus-visible { outline: 2px solid var(--accent-color); outline-offset: 2px; }
.footer-links { display: flex; justify-content: center; align-items: center; gap: 12px; margin-top: 24px; }
.text-link { border: 0; background: transparent; padding: 6px 0; color: var(--accent-color); font-size: 13px; }.dot { color: var(--border-line); }
.status-text,.message,.error-text { font-size: 13px; line-height: 1.7; margin-top: 16px; padding: 10px 12px; border-left: 2px solid currentColor; background: var(--bg-sub); }
.error-text,.error { color: var(--danger); }.success-text,.success { color: var(--success); }
@media(prefers-reduced-motion:reduce) { button { transition: none; } }
''')

# 未被基础输入样式覆盖的问卷数值/矩阵字段。
p='src/views/活动中心/问卷中心/SurveyEditor.vue'
edit(p,lambda s:s.replace('  transition: all 0.2s;', '  transition: border-color .2s, background-color .2s;').replace('</style>','''.rating-range input,.matrix-item input { background: var(--paper-card); color: var(--text-primary); min-width: 0; box-sizing: border-box; }
.form-group input,.form-group textarea,.option-input,.q-desc-input { box-sizing: border-box; min-width: 0; }.question-item { border-left: 3px solid var(--surface-edge); border-radius: var(--radius-sm); }
@media(prefers-reduced-motion:reduce) { * { transition: none !important; transform: none !important; } }
</style>'''))
print('Updated editor surfaces and image viewer')
