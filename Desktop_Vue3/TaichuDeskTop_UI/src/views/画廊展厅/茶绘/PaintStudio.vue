<!-- PaintStudio.vue：茶绘画室。画布（缩放、拖动、压感、平滑）、工具、图层、成员、聊天、导出 / 发到画廊。网络事件都在这里处理，画图交给 paintEngine。 -->
<template>
  <div ref="rootEl" class="studio" :class="{ 'is-panning': panning || spaceDown || tool === 'hand', immersive, 'is-fullscreen': fullscreen }">
    <!-- 顶栏 -->
    <header class="studio-bar">
      <button type="button" class="bar-btn" title="离开画室" @click="leave">← 离开</button>
      <div class="room-title">
        <p class="eyebrow">CHAHUI · LIVE CANVAS</p>
        <b>{{ room.name }}</b>
      </div>
      <button type="button" class="room-code" title="复制邀请链接" @click="copyLink"><small>房间号</small>{{ room.roomId }}<span>{{ copied ? '已复制链接' : '复制链接' }}</span></button>
      <ul class="members" aria-label="画室成员">
        <li v-for="m in members" :key="m.userId" :class="{ off: !m.online }" :title="m.online ? m.name : `${m.name}（掉线中）`">
          <i :style="{ background: m.color }"></i>{{ m.name }}<em v-if="same(m.userId, ownerId)">房主</em>
          <button v-if="amOwner && !same(m.userId, me)" type="button" class="kick" :title="`请 ${m.name} 离开`" @click="kick(m)">×</button>
        </li>
        <li v-for="n in Math.max(0, room.maxMembers - members.length)" :key="`e${n}`" class="empty">空位</li>
      </ul>
      <div class="bar-right">
        <button type="button" class="bar-btn" title="撤销（Ctrl+Z）" @click="undo">↶</button>
        <button type="button" class="bar-btn" title="重做（Ctrl+Shift+Z）" @click="redo">↷</button>
        <span class="zoom">
          <button type="button" title="缩小" @click="zoomBy(1 / 1.25)">−</button>
          <button type="button" class="zoom-val" title="适应窗口" @click="fit">{{ Math.round(zoom * 100) }}%</button>
          <button type="button" title="放大" @click="zoomBy(1.25)">+</button>
        </span>
        <button type="button" class="bar-btn" title="一笔一笔回放这张画是怎么画出来的，还能录成视频" @click="replayOpen = true">过程回放</button>
        <span class="view-btns">
          <button type="button" class="bar-btn" :class="{ on: immersive }" :title="immersive ? '退出专注模式（显示网站导航）' : '专注模式：画室铺满整个窗口'" @click="immersive = !immersive">{{ immersive ? '退出专注' : '专注模式' }}</button>
          <button v-if="canFullscreen" type="button" class="bar-btn" :title="fullscreen ? '退出全屏' : '全屏（连浏览器地址栏也藏起来）'" @click="toggleFullscreen">{{ fullscreen ? '退出全屏' : '全屏' }}</button>
          <button type="button" class="bar-btn" :title="panelOpen ? '收起右侧面板，画布更大' : '展开右侧面板'" @click="panelOpen = !panelOpen">{{ panelOpen ? '收起面板' : '展开面板' }}</button>
        </span>
        <button type="button" class="bar-btn" title="把一张图片放进新图层，可以缩放、旋转、摆位置（参考图、贴图）" @click="importInput?.click()">导入图片</button>
        <input ref="importInput" type="file" accept="image/png,image/jpeg,image/webp,image/gif,image/bmp" hidden @change="onImport" />
        <button type="button" class="bar-btn" title="下载 PNG" @click="exportPng">导出</button>
        <button type="button" class="bar-btn primary" @click="openPublish">发到画廊</button>
      </div>
    </header>

    <p v-if="notice" class="studio-notice" :class="noticeKind" role="status">{{ notice }}</p>

    <div class="studio-body" :class="{ 'no-panel': !panelOpen }">
      <!-- 工具 -->
      <nav class="tools" aria-label="工具">
        <button v-for="t in TOOLS" :key="t.key" type="button" :class="{ on: tool === t.key, gap: t.gap }" :title="`${t.name}（${t.hotkey}）`" :aria-label="t.name" :aria-pressed="tool === t.key" @click="tool = t.key">
          <svg class="tool-icon" viewBox="0 0 24 24" aria-hidden="true"><path :d="t.icon" :stroke-dasharray="t.dash ? '3 2.4' : undefined" /></svg><small>{{ t.name }}</small>
        </button>
        <span class="tool-color" :style="{ background: color }" title="当前颜色"></span>
      </nav>

      <!-- 画布 -->
      <section
        ref="viewEl"
        class="view"
        :style="{ cursor: viewCursor }"
        @pointerdown="onDown"
        @pointermove="onMove"
        @pointerup="onUp"
        @pointercancel="onCancel"
        @pointerleave="onLeave"
        @wheel.prevent="onWheel"
        @contextmenu.prevent
      >
        <div ref="stageEl" class="stage" :style="{ width: `${room.width}px`, height: `${room.height}px`, transform: `translate(${pan.x}px, ${pan.y}px) scale(${zoom})` }"></div>
        <!-- 别人的光标 -->
        <div v-for="c in visibleCursors" :key="c.userId" class="peer-cursor" :style="{ transform: `translate(${pan.x + c.x * zoom}px, ${pan.y + c.y * zoom}px)`, '--peer': c.color }">
          <i></i><span>{{ c.name }}</span>
        </div>
        <!-- 选区（蚂蚁线）、渐变方向 -->
        <svg v-if="selD || gradScreen || xfHandles" class="sel-overlay" aria-hidden="true">
          <template v-if="selD"><path :d="selD" class="ants ants-bg" /><path :d="selD" class="ants ants-fg" /></template>
          <g v-if="xfHandles" class="xf-box">
            <path :d="xfHandles.d" class="xf-frame" />
            <line :x1="xfHandles.top.x" :y1="xfHandles.top.y" :x2="xfHandles.rot.x" :y2="xfHandles.rot.y" class="xf-frame" />
            <circle :cx="xfHandles.rot.x" :cy="xfHandles.rot.y" r="5.5" class="xf-rot" />
            <rect v-for="h in xfHandles.pts" :key="`${h.hx},${h.hy}`" :x="h.x - 4.5" :y="h.y - 4.5" width="9" height="9" class="xf-handle" />
          </g>
          <g v-if="gradScreen" class="grad-line">
            <line :x1="gradScreen.x0" :y1="gradScreen.y0" :x2="gradScreen.x1" :y2="gradScreen.y1" />
            <circle :cx="gradScreen.x0" :cy="gradScreen.y0" r="4" /><circle :cx="gradScreen.x1" :cy="gradScreen.y1" r="4" />
          </g>
        </svg>
        <!-- 有选区时的快捷操作 -->
        <div v-if="xf" class="sel-bar xf-bar" role="toolbar" aria-label="自由变换" @pointerdown.stop>
          <b>变换</b>
          <span class="xf-tip">拖角缩放（Shift 等比）· 框外拖动旋转 · 框里拖动移动</span>
          <button type="button" class="ok" title="Enter" @click="applyTransform">✓ 确定</button>
          <button type="button" title="Esc" @click="cancelTransform">取消</button>
        </div>
        <div v-else-if="selection && !selDraft" class="sel-bar" role="toolbar" aria-label="选区操作" @pointerdown.stop>
          <b>选区</b>
          <button type="button" title="缩放、旋转选区里的内容（T）" @click="startTransform">⤡ 自由变换</button>
          <button type="button" title="水平翻转选区里的内容" @click="selectionOp('flipH')">⇋ 水平翻转</button>
          <button type="button" title="垂直翻转选区里的内容" @click="selectionOp('flipV')">⇵ 垂直翻转</button>
          <button type="button" title="用当前颜色填满选区" @click="selectionOp('fill')">填充</button>
          <button type="button" class="danger" title="删除选区里的内容（Delete）" @click="selectionOp('delete')">删除</button>
          <button type="button" title="取消选区（Esc / Ctrl+D）" @click="deselect">✕</button>
        </div>
        <!-- 自己的笔刷轮廓 -->
        <div v-if="showRing" class="brush-ring" :style="{ width: `${ringSize}px`, height: `${ringSize}px`, transform: `translate(${brushRing.x - ringSize / 2}px, ${brushRing.y - ringSize / 2}px)` }"></div>
        <p v-if="loading" class="view-loading">正在载入画布…</p>
        <p class="view-hint">{{ toolHint }}</p>
      </section>

      <!-- 右侧面板 -->
      <aside v-show="panelOpen" class="panel">
        <!-- 工具选项 -->
        <section v-if="optTool" class="block tool-opts">
          <p class="block-tag">{{ optTag }}</p>
          <template v-if="tool === 'blur' || tool === 'smudge'">
            <p class="brush-desc">{{ tool === 'blur' ? '在画过的地方来回涂，让颜色变柔、边缘变虚，多涂几遍更模糊。只对当前图层生效。' : '像用手指推颜料：把颜色顺着拖动方向带过去，适合混色、做过渡、拉出发丝。只对当前图层生效。' }}</p>
            <label class="slider">大小 <input v-model.number="fxSize" type="range" min="4" :max="fxMaxSize" step="1" /><b>{{ fxSize }}</b></label>
            <label class="slider" :title="tool === 'blur' ? '每涂一遍模糊多少' : '越大颜色被带得越远'">强度 <input v-model.number="fxStrength" type="range" min="5" max="100" step="1" /><b>{{ fxStrength }}%</b></label>
            <label class="slider" title="越大线条越平滑，但会有一点拖尾">平滑 <input v-model.number="smooth" type="range" min="0" max="10" step="1" /><b>{{ smooth }}</b></label>
          </template>
          <template v-else-if="tool === 'fill' || tool === 'gradient'">
            <p class="brush-desc">{{ tool === 'fill' ? '点一下，把颜色相近、连在一起的区域填满。线稿画在别的图层也没关系：默认按所有图层合起来的画面找边界，填到当前图层上。' : '在画布上拖一条线：从起点的当前颜色渐变到终点。有选区时只填选区，没有选区时铺满当前图层。' }}</p>
            <div class="color-row">
              <label class="color-input" title="自定义颜色"><input v-model="color" type="color" aria-label="颜色" /><span :style="{ background: color }"></span></label>
              <button v-for="c in SWATCHES" :key="c" type="button" class="swatch" :class="{ on: c === color }" :style="{ background: c }" :aria-label="`颜色 ${c}`" @click="color = c"></button>
            </div>
            <div v-if="recent.length" class="color-row recent">
              <small>最近</small>
              <button v-for="c in recent" :key="c" type="button" class="swatch sm" :style="{ background: c }" :aria-label="`最近用过的颜色 ${c}`" @click="color = c"></button>
            </div>
            <template v-if="tool === 'fill'">
              <label class="slider" title="越大，颜色差得越多也会被填进去">容差 <input v-model.number="fx.fillTolerance" type="range" min="0" max="100" step="1" /><b>{{ fx.fillTolerance }}</b></label>
              <label class="slider" title="向外多填几像素，盖住线稿边缘半透明的地方，不留白边">扩展 <input v-model.number="fx.fillExpand" type="range" min="0" max="4" step="1" /><b>{{ fx.fillExpand }}px</b></label>
              <label class="slider">不透明度 <input v-model.number="fx.fillOpacity" type="range" min="5" max="100" step="1" /><b>{{ fx.fillOpacity }}%</b></label>
              <div class="seg" role="radiogroup" aria-label="按哪些图层找边界">
                <button type="button" role="radio" :aria-checked="fx.fillSample === 'all'" :class="{ on: fx.fillSample === 'all' }" @click="fx.fillSample = 'all'">按所有图层</button>
                <button type="button" role="radio" :aria-checked="fx.fillSample === 'layer'" :class="{ on: fx.fillSample === 'layer' }" @click="fx.fillSample = 'layer'">只看当前图层</button>
              </div>
            </template>
            <template v-else>
              <div class="seg" role="radiogroup" aria-label="渐变形状">
                <button type="button" role="radio" :aria-checked="fx.gradType === 'linear'" :class="{ on: fx.gradType === 'linear' }" @click="fx.gradType = 'linear'">线性</button>
                <button type="button" role="radio" :aria-checked="fx.gradType === 'radial'" :class="{ on: fx.gradType === 'radial' }" @click="fx.gradType = 'radial'">径向（从中心向外）</button>
              </div>
              <div class="seg" role="radiogroup" aria-label="渐变终点">
                <button type="button" role="radio" :aria-checked="fx.gradEnd === 'clear'" :class="{ on: fx.gradEnd === 'clear' }" @click="fx.gradEnd = 'clear'">渐变到透明</button>
                <button type="button" role="radio" :aria-checked="fx.gradEnd === 'color'" :class="{ on: fx.gradEnd === 'color' }" @click="fx.gradEnd = 'color'">渐变到另一种颜色</button>
              </div>
              <div v-if="fx.gradEnd === 'color'" class="color-row">
                <small class="row-label">终点颜色</small>
                <label class="color-input" title="终点颜色"><input v-model="fx.gradColor2" type="color" aria-label="终点颜色" /><span :style="{ background: fx.gradColor2 }"></span></label>
                <button type="button" class="link" title="起点和终点颜色对调" @click="swapGradColors">⇄ 对调</button>
              </div>
              <label class="slider">不透明度 <input v-model.number="fx.gradOpacity" type="range" min="5" max="100" step="1" /><b>{{ fx.gradOpacity }}%</b></label>
            </template>
          </template>
          <template v-else-if="tool === 'shape'">
            <div class="seg" role="radiogroup" aria-label="形状">
              <button type="button" role="radio" :aria-checked="fx.shapeKind === 'line'" :class="{ on: fx.shapeKind === 'line' }" @click="fx.shapeKind = 'line'">／ 直线</button>
              <button type="button" role="radio" :aria-checked="fx.shapeKind === 'rect'" :class="{ on: fx.shapeKind === 'rect' }" @click="fx.shapeKind = 'rect'">▭ 矩形</button>
              <button type="button" role="radio" :aria-checked="fx.shapeKind === 'ellipse'" :class="{ on: fx.shapeKind === 'ellipse' }" @click="fx.shapeKind = 'ellipse'">◯ 椭圆</button>
            </div>
            <div class="checks">
              <label :class="{ off: fx.shapeKind === 'line' }"><input v-model="fx.shapeFill" type="checkbox" :disabled="fx.shapeKind === 'line'" />实心（用当前颜色填满）</label>
            </div>
            <p class="brush-desc">拖动画形状，按住 Shift 画正方形 / 正圆 / 45° 直线。{{ fx.shapeFill && fx.shapeKind !== 'line' ? '实心形状用下面的颜色和不透明度。' : '线条用下面选的笔刷、粗细和颜色。' }}</p>
          </template>
          <template v-else>
            <p class="brush-desc">{{ tool === 'lasso' ? '按住拖动，随手圈出一块选区。' : tool === 'marquee' ? '按住拖动，框出矩形选区（Shift 正方形）。' : '拖动移动选区里的内容；没有选区时移动整个图层。' }}在选区里拖动可以移动内容，按住 Alt 拖动是复制。有选区时，画笔、橡皮、模糊、油漆桶、渐变都只作用在选区里。</p>
            <div class="op-row">
              <button type="button" class="mini" title="Ctrl+A" @click="selectAll">全选</button>
              <button type="button" class="mini" :disabled="!selection" title="Esc / Ctrl+D" @click="deselect">取消选区</button>
            </div>
            <div class="op-row">
              <button type="button" class="mini" :disabled="!!xf" :title="selection ? '缩放、旋转选区里的内容（T）' : '没有选区时变换整个图层（T）'" @click="startTransform">⤡ 自由变换</button>
              <button type="button" class="mini" :disabled="!selection" @click="selectionOp('flipH')">⇋ 水平翻转</button>
              <button type="button" class="mini" :disabled="!selection" @click="selectionOp('flipV')">⇵ 垂直翻转</button>
              <button type="button" class="mini" :disabled="!selection" @click="selectionOp('fill')">用当前颜色填充</button>
              <button type="button" class="mini danger" :disabled="!selection" title="Delete" @click="selectionOp('delete')">删除内容</button>
            </div>
            <div class="color-row">
              <label class="color-input" title="当前颜色（填充选区用）"><input v-model="color" type="color" aria-label="颜色" /><span :style="{ background: color }"></span></label>
              <button v-for="c in SWATCHES.slice(0, 9)" :key="c" type="button" class="swatch" :class="{ on: c === color }" :style="{ background: c }" :aria-label="`颜色 ${c}`" @click="color = c"></button>
            </div>
          </template>
        </section>

        <section v-if="showBrushBlock" class="block">
          <div class="block-head">
            <p class="block-tag">BRUSH / {{ prefTool === 'eraser' ? '橡皮笔刷' : '笔刷' }}</p>
            <button type="button" class="link" title="把这支笔刷的设置恢复成默认" @click="resetBrush">恢复默认</button>
          </div>
          <div class="brush-grid" role="radiogroup" aria-label="笔刷">
            <button v-for="b in BRUSHES" :key="b.kind" type="button" role="radio" class="brush-btn" :class="{ on: brushFor[prefTool] === b.kind }" :aria-checked="brushFor[prefTool] === b.kind" :title="b.desc" @click="selectBrush(b.kind)">
              <canvas :ref="el => setPreviewEl(b.kind, el as HTMLCanvasElement | null)" width="160" height="44" aria-hidden="true"></canvas>
              <span>{{ b.name }}</span>
            </button>
          </div>
          <div class="my-brushes">
            <div class="sub-head">
              <span class="sub-tag">我的笔刷 · {{ myBrushes.length }}</span>
              <span class="sub-actions">
                <button type="button" class="link" title="自己做一支笔刷：笔尖、间距、散布、抖动、渐细都能调" @click="openEditor(null)">＋ 制作</button>
                <button type="button" class="link" title="浏览大家公开的笔刷，收藏后就能用" @click="libraryOpen = true">笔刷库</button>
              </span>
            </div>
            <div v-if="myBrushes.length" class="brush-grid">
              <div v-for="b in myBrushes" :key="b.id" class="brush-btn custom" role="radio" tabindex="0" :class="{ on: customOf(prefTool)?.id === b.id }" :aria-checked="customOf(prefTool)?.id === b.id" :title="b.mine ? b.name : `${b.name} · ${b.ownerName}`" @click="selectCustom(b)" @keydown.enter.prevent="selectCustom(b)">
                <canvas :ref="el => setCustomPreviewEl(b.id, el as HTMLCanvasElement | null)" width="160" height="44" aria-hidden="true"></canvas>
                <span>{{ b.name }}</span>
                <button v-if="b.mine" type="button" class="edit" title="编辑这支笔刷" @click.stop="openEditor(b)">✎</button>
                <i v-else class="fav-mark" title="收藏的笔刷">♥</i>
              </div>
            </div>
            <p v-else class="brush-desc">还没有自己的笔刷。点「＋ 制作」做一支，或者去「笔刷库」收藏别人公开的。</p>
          </div>
          <p class="brush-desc">{{ currentBrush.desc }}</p>
          <div v-if="prefTool === 'brush'" class="color-row">
            <label class="color-input" title="自定义颜色"><input v-model="color" type="color" aria-label="颜色" /><span :style="{ background: color }"></span></label>
            <button v-for="c in SWATCHES" :key="c" type="button" class="swatch" :class="{ on: c === color }" :style="{ background: c }" :aria-label="`颜色 ${c}`" @click="color = c"></button>
          </div>
          <div v-if="recent.length" class="color-row recent">
            <small>最近</small>
            <button v-for="c in recent" :key="c" type="button" class="swatch sm" :style="{ background: c }" :aria-label="`最近用过的颜色 ${c}`" @click="color = c"></button>
          </div>
          <label class="slider">粗细 <input v-model.number="size" type="range" min="1" :max="currentBrush.maxSize" step="1" /><b>{{ size }}</b></label>
          <label class="slider">不透明度 <input v-model.number="opacityPct" type="range" min="5" max="100" step="1" /><b>{{ opacityPct }}%</b></label>
          <label v-if="hasHardness" class="slider" title="越小边缘越柔和">硬度 <input v-model.number="hardnessPct" type="range" min="0" max="100" step="5" /><b>{{ hardnessPct }}%</b></label>
          <label class="slider" title="越大线条越平滑，但会有一点拖尾">平滑 <input v-model.number="smooth" type="range" min="0" max="10" step="1" /><b>{{ smooth }}</b></label>
          <div v-if="currentBrush.kind !== 'custom'" class="checks">
            <label :class="{ off: currentBrush.kind === 'marker' }" :title="currentBrush.kind === 'marker' ? '马克笔粗细固定' : '用数位笔时，按得越重越粗'"><input type="checkbox" :checked="!!(flags & FLAG_PRESSURE_SIZE)" :disabled="currentBrush.kind === 'marker'" @change="toggleFlag(FLAG_PRESSURE_SIZE)" />压感控制粗细</label>
            <label title="用数位笔时，按得越重颜色越浓"><input type="checkbox" :checked="!!(flags & FLAG_PRESSURE_OPACITY)" @change="toggleFlag(FLAG_PRESSURE_OPACITY)" />压感控制浓淡</label>
          </div>
        </section>

        <section class="block layers">
          <div class="block-head">
            <p class="block-tag">LAYERS / 图层 · {{ layers.length }}</p>
            <button type="button" class="mini" title="在当前图层上面新建一个图层" @click="layerAct('add', currentLayer, '')">＋ 新建</button>
          </div>
          <ol>
            <li v-for="l in layersTopDown" :key="l.id" :class="{ on: l.id === currentLayer, hidden: hidden.has(l.id) }" @click="currentLayer = l.id">
              <button type="button" class="eye" :title="hidden.has(l.id) ? '显示（只影响自己看到的）' : '隐藏（只影响自己看到的）'" @click.stop="toggleHidden(l.id)">{{ hidden.has(l.id) ? '◌' : '●' }}</button>
              <div class="layer-name">
                <b>{{ l.name }}</b>
                <small>{{ l.creatorName }}<template v-if="l.opacity < 1"> · {{ Math.round(l.opacity * 100) }}%</template></small>
              </div>
              <span v-if="l.locked" class="lock" :title="`${l.creatorName} 锁定了，只有 TA 能画`">🔒</span>
            </li>
          </ol>
          <div v-if="selected" class="layer-ops">
            <label v-if="canManage(selected)" class="slider wide">图层不透明度 <input type="range" min="0" max="100" step="1" :value="Math.round(selected.opacity * 100)" @change="onLayerOpacity" /><b>{{ Math.round(selected.opacity * 100) }}%</b></label>
            <div class="op-row">
              <button type="button" class="mini" :disabled="!canManage(selected)" title="上移" @click="layerAct('up', selected.id)">↑</button>
              <button type="button" class="mini" :disabled="!canManage(selected)" title="下移" @click="layerAct('down', selected.id)">↓</button>
              <button type="button" class="mini" :disabled="!canManage(selected)" @click="renameLayer">重命名</button>
              <button type="button" class="mini" :disabled="!canManage(selected)" @click="layerAct('lock', selected.id, '', selected.locked ? 0 : 1)">{{ selected.locked ? '解锁' : '锁定' }}</button>
              <button type="button" class="mini danger" :disabled="!canManage(selected)" @click="clearLayer">清空</button>
              <button type="button" class="mini danger" :disabled="!canManage(selected) || layers.length <= 1" @click="deleteLayer">删除</button>
            </div>
            <p v-if="!canManage(selected)" class="hint">这个图层是 {{ selected.creatorName }} 建的，只有 TA 或房主能修改它的设置。</p>
          </div>
        </section>

        <section class="block chat">
          <p class="block-tag">CHAT / 聊天</p>
          <ol ref="chatEl">
            <li v-for="(m, i) in chat" :key="i"><b :style="{ color: m.color }">{{ m.name }}</b>{{ m.text }}</li>
            <li v-if="!chat.length" class="muted">边画边聊吧</li>
          </ol>
          <form @submit.prevent="sendChat"><input v-model="chatText" maxlength="200" placeholder="说点什么…" aria-label="聊天内容" /><button type="submit" class="mini" :disabled="!chatText.trim()">发送</button></form>
        </section>
      </aside>
    </div>

    <BrushEditor v-if="editorOpen" :brush="editing" :color="color" @close="editorOpen = false" @saved="onBrushSaved" @deleted="onBrushDeleted" />
    <div v-if="libraryOpen" class="lib-mask" role="dialog" aria-modal="true" aria-label="笔刷库" @click.self="libraryOpen = false">
      <div class="lib">
        <header class="lib-head">
          <div><p class="eyebrow">BRUSH LIBRARY / 笔刷库</p><b>大家公开的笔刷 · 收藏后出现在「我的笔刷」</b></div>
          <button type="button" class="bar-btn" @click="libraryOpen = false">关闭</button>
        </header>
        <BrushSquare pickable @use="useLibraryBrush" @changed="loadMine" />
      </div>
    </div>
    <ReplayPlayer v-if="replayOpen" :room-id="room.roomId" :title="room.name" :colors="memberColors" @close="replayOpen = false" />
    <ArtworkPublishModal v-model="publishOpen" :initial-files="publishFiles" :initial-title="`${room.name}`" :initial-description="publishDesc" initial-tags="茶绘" @published="onPublished" />
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, shallowRef, watch } from 'vue'
import type * as signalR from '@microsoft/signalr'
import request from '@/utils/request'
import ArtworkPublishModal from '../ArtworkPublishModal.vue'
import ReplayPlayer from './ReplayPlayer.vue'
import BrushEditor from './BrushEditor.vue'
import BrushSquare from './BrushSquare.vue'
import { brushApi, drawBrushPreview, onTipLoaded, type BrushView } from './brushApi'
import { BRUSHES, FLAG_COPY, FLAG_ERASE, FLAG_PRESSURE_OPACITY, FLAG_PRESSURE_SIZE, FLAG_RADIAL, PaintEngine, brushOf, renderStroke, type BrushKind, type LayerMeta, type Stroke, type StrokeStyle } from './paintEngine'
import { floodFill, pointInPolygon, polygonArea, polygonBounds, simplifyPolygon } from './pixelOps'
import { forgetPatch, onPatchLoaded, registerPatch, uploadPatch, usePatchRoom } from './patchCache'
import type { PaintChatMsg, PaintMemberView, PaintRoomView } from './paintTypes'


const props = defineProps<{ connection: signalR.HubConnection; room: PaintRoomView; me: string }>()
const emit = defineEmits<{ left: [reason: string]; reload: [] }>()

/** 有自己笔刷设置的工具 */
type PaintTool = 'brush' | 'eraser'
/** 按住拖动、一笔一笔画的工具 */
type StrokeTool = PaintTool | 'blur' | 'smudge'
type ToolKey = 'brush' | 'eraser' | 'blur' | 'smudge' | 'fill' | 'gradient' | 'shape' | 'lasso' | 'marquee' | 'move' | 'picker' | 'hand'
/** 工具栏（icon 是 24×24 的线条图标路径） */
const TOOLS: { key: ToolKey; name: string; icon: string; hotkey: string; dash?: boolean; gap?: boolean }[] = [
  { key: 'brush', name: '画笔', hotkey: 'B', icon: 'M3.5 20.5c3 0 5-1.4 5-3.8 0-1.2-.8-2-2-2-1.7 0-3 1.4-3 5.8zM8.6 14.6 19.5 3.7l1 1L9.6 15.6' },
  { key: 'eraser', name: '橡皮', hotkey: 'E', icon: 'M4.5 15.5 12.8 7.2l5.5 5.5-6.8 6.8H8.5zM9.3 20h11M9 11l5.5 5.5' },
  { key: 'blur', name: '模糊', hotkey: 'R', icon: 'M12 3.5c3.2 4.3 6 7.4 6 10.6a6 6 0 0 1-12 0c0-3.2 2.8-6.3 6-10.6zM9.2 14.5a2.8 2.8 0 0 0 2.8 2.8' },
  { key: 'smudge', name: '涂抹', hotkey: 'S', icon: 'M9.5 20.5v-8a1.5 1.5 0 0 1 3 0v-1a1.5 1.5 0 0 1 3 0v1.2a1.5 1.5 0 0 1 3 0v3.3c0 2.7-2 4.5-4.6 4.5zM12.5 11.5V4.3a1.5 1.5 0 0 0-3 0v8.2M3 8.5c1.5-1 2.5 1 4 0' },
  { key: 'fill', name: '油漆桶', hotkey: 'G', gap: true, icon: 'M4.6 11.2 11 4.8l7.6 7.6-6.1 6.1a1.8 1.8 0 0 1-2.5 0l-5.4-5.4a1.3 1.3 0 0 1 0-1.9zM4.5 12h14M20 15.5s1.8 2.2 1.8 3.3a1.8 1.8 0 0 1-3.6 0c0-1.1 1.8-3.3 1.8-3.3z' },
  { key: 'gradient', name: '渐变', hotkey: 'N', icon: 'M4 4h16v16H4zM4 14 14 4M4 19.5 19.5 4M9.5 20 20 9.5M15 20l5-5' },
  { key: 'shape', name: '形状', hotkey: 'U', icon: 'M3.5 4.5h9.5V14H3.5zM16 11a4.8 4.8 0 1 1 0 9.6 4.8 4.8 0 0 1 0-9.6z' },
  { key: 'lasso', name: '套索', hotkey: 'L', gap: true, icon: 'M12 4c4.7 0 8 2.3 8 5.3s-3.3 5.2-8 5.2-8-2.3-8-5.2S7.3 4 12 4zM6.6 13.4c-1.3 1.8-.6 4.1 1.6 4.1 1.6 0 2 1.4.6 3' },
  { key: 'marquee', name: '矩形选区', hotkey: 'M', dash: true, icon: 'M4 5h16v14H4z' },
  { key: 'move', name: '移动', hotkey: 'V', icon: 'M12 3v18M3 12h18M12 3 9.5 5.5M12 3l2.5 2.5M12 21l-2.5-2.5M12 21l2.5-2.5M3 12l2.5-2.5M3 12l2.5 2.5M21 12l-2.5-2.5M21 12l-2.5 2.5' },
  { key: 'picker', name: '吸管', hotkey: 'I', gap: true, icon: 'M14.5 4.5a2.1 2.1 0 0 1 3 0l2 2a2.1 2.1 0 0 1 0 3L17 12l-5-5zM12.5 7.5l-8 8V19.5h4l8-8' },
  { key: 'hand', name: '抓手', hotkey: 'H / 空格', icon: 'M8 13.5V6.2a1.5 1.5 0 0 1 3 0v5V4.6a1.5 1.5 0 0 1 3 0v6.6V6.2a1.5 1.5 0 0 1 3 0V14c0 4-2.5 7-6.5 7-3 0-5-1.6-6-4l-1.6-3.3a1.5 1.5 0 0 1 2.6-1.5z' },
]
const HOTKEYS: Record<string, ToolKey> = { b: 'brush', e: 'eraser', r: 'blur', s: 'smudge', g: 'fill', n: 'gradient', u: 'shape', l: 'lasso', m: 'marquee', v: 'move', i: 'picker', h: 'hand' }
/** 有选项面板的工具（画笔类工具用下面的笔刷面板） */
const OPTION_TOOLS: ToolKey[] = ['blur', 'smudge', 'fill', 'gradient', 'shape', 'lasso', 'marquee', 'move']
const RING_TOOLS: ToolKey[] = ['brush', 'eraser', 'blur', 'smudge']
const SWATCHES = ['#1b2128', '#ffffff', '#8a949c', '#c2272d', '#f59e0b', '#facc15', '#22c55e', '#2dd4bf', '#3b82f6', '#6366f1', '#a855f7', '#f472b6', '#7c4a2d', '#f3d9c4']

const same = (a?: string | null, b?: string | null) => !!a && !!b && a.toLowerCase() === b.toLowerCase()

// ================= 状态 =================
const viewEl = ref<HTMLElement | null>(null)
const stageEl = ref<HTMLElement | null>(null)
const chatEl = ref<HTMLOListElement | null>(null)
const engine = shallowRef<PaintEngine | null>(null)
const loading = ref(true)
const members = ref<PaintMemberView[]>(props.room.members)
const ownerId = ref(props.room.ownerId)
const layers = ref<LayerMeta[]>(props.room.layers)
const chat = ref<PaintChatMsg[]>([...props.room.chat])
const chatText = ref('')
const hidden = ref(new Set<number>())
const currentLayer = ref(0)
const tool = ref<ToolKey>('brush')
const color = ref(loadPref('color', '#1b2128'))
// ===== 笔刷：画笔和橡皮各自选笔刷，每种笔刷各自记住粗细、不透明度、硬度、压感设置 =====
interface BrushPref { size: number; opacity: number; hardness: number; flags: number }
const defaultPref = (t: PaintTool, kind: BrushKind): BrushPref => {
  const b = brushOf(kind)
  return t === 'eraser'
    ? { size: Math.max(20, b.size * 2), opacity: 100, hardness: Math.round(b.hardness * 100), flags: b.flags & FLAG_PRESSURE_SIZE }
    : { size: b.size, opacity: Math.round(b.opacity * 100), hardness: Math.round(b.hardness * 100), flags: b.flags }
}
const prefs: Record<string, BrushPref> = (() => {
  let saved: Record<string, BrushPref> = {}
  try { saved = JSON.parse(loadPref('brush-prefs', '{}')) || {} } catch { /* 忽略 */ }
  const out: Record<string, BrushPref> = {}
  for (const t of ['brush', 'eraser'] as PaintTool[]) for (const b of BRUSHES) {
    const k = `${t}:${b.kind}`
    const v = saved[k]
    out[k] = v && typeof v.size === 'number' ? { ...defaultPref(t, b.kind), ...v } : defaultPref(t, b.kind)
  }
  return out
})()
const savedKind = (k: string): BrushKind => (k === 'custom' ? 'custom' : brushOf(k).kind)
const brushFor = reactive<Record<PaintTool, BrushKind>>({ brush: savedKind(loadPref('brush-kind', 'pen')), eraser: savedKind(loadPref('eraser-kind', 'pen')) })
const prefTool = ref<PaintTool>('brush')
// ===== 自制笔刷：自己做的 + 收藏的（跟账号保存） =====
const myBrushes = ref<BrushView[]>([])
const customFor = reactive<Record<PaintTool, string>>({ brush: loadPref('brush-custom', ''), eraser: loadPref('eraser-custom', '') })
/** 这个工具当前选的自制笔刷（没选或者还没载入时为 null） */
const customOf = (t: PaintTool) => (brushFor[t] === 'custom' ? myBrushes.value.find(b => b.id === customFor[t]) ?? null : null)
/** 实际用的笔刷种类：选了自制笔刷但找不到时先按圆头笔 */
const kindOf = (t: PaintTool): BrushKind => (brushFor[t] === 'custom' ? (customOf(t) ? 'custom' : 'pen') : brushFor[t])
const customDefault = (t: PaintTool, b: BrushView): BrushPref => (t === 'eraser'
  ? { size: Math.max(20, Math.round(b.params.size * 2)), opacity: 100, hardness: 100, flags: 0 }
  : { size: Math.round(b.params.size), opacity: Math.round(b.params.opacity * 100), hardness: 100, flags: 0 })
const prefKeyOf = () => {
  const t = prefTool.value
  const c = customOf(t)
  const key = c ? `${t}:c:${c.id}` : `${t}:${kindOf(t)}`
  if (!prefs[key]) prefs[key] = c ? customDefault(t, c) : defaultPref(t, kindOf(t))
  return key
}
const size = ref(prefs[prefKeyOf()]!.size)
const opacityPct = ref(prefs[prefKeyOf()]!.opacity)
const hardnessPct = ref(prefs[prefKeyOf()]!.hardness)
const flags = ref(prefs[prefKeyOf()]!.flags)
const currentBrush = computed(() => {
  const c = customOf(prefTool.value)
  if (c) return { kind: 'custom' as BrushKind, name: c.name, desc: c.mine ? `自制笔刷「${c.name}」· 笔尖和动态在 ✎ 编辑里改` : `「${c.name}」· ${c.ownerName} 做的笔刷`, size: c.params.size, opacity: c.params.opacity, hardness: 1, flags: 0, maxSize: c.params.maxSize }
  return brushOf(kindOf(prefTool.value))
})
const hasHardness = computed(() => ['pen', 'ink', 'marker', 'soft'].includes(currentBrush.value.kind))
const smooth = ref(Number(loadPref('smooth', '3')) || 0)
// ===== 其他工具的设置（跟浏览器保存） =====
interface FxPrefs {
  blurSize: number; blurStrength: number; smudgeSize: number; smudgeStrength: number
  fillTolerance: number; fillSample: 'all' | 'layer'; fillExpand: number; fillOpacity: number
  gradType: 'linear' | 'radial'; gradEnd: 'clear' | 'color'; gradColor2: string; gradOpacity: number
  shapeKind: 'line' | 'rect' | 'ellipse'; shapeFill: boolean
}
const FX_DEFAULT: FxPrefs = {
  blurSize: 40, blurStrength: 60, smudgeSize: 30, smudgeStrength: 70,
  fillTolerance: 12, fillSample: 'all', fillExpand: 1, fillOpacity: 100,
  gradType: 'linear', gradEnd: 'clear', gradColor2: '#ffffff', gradOpacity: 100,
  shapeKind: 'rect', shapeFill: false,
}
const fx = reactive<FxPrefs>((() => { try { return { ...FX_DEFAULT, ...(JSON.parse(loadPref('fx', '{}')) || {}) } } catch { return { ...FX_DEFAULT } } })())
const isSmudge = () => tool.value === 'smudge'
const fxSize = computed({ get: () => (isSmudge() ? fx.smudgeSize : fx.blurSize), set: (v: number) => { if (isSmudge()) fx.smudgeSize = v; else fx.blurSize = v } })
const fxStrength = computed({ get: () => (isSmudge() ? fx.smudgeStrength : fx.blurStrength), set: (v: number) => { if (isSmudge()) fx.smudgeStrength = v; else fx.blurStrength = v } })
const fxMaxSize = computed(() => (isSmudge() ? 200 : 300))
const optTool = computed(() => OPTION_TOOLS.includes(tool.value))
const showBrushBlock = computed(() => !optTool.value || tool.value === 'shape')
const optTag = computed(() => ({
  blur: 'BLUR / 模糊', smudge: 'SMUDGE / 涂抹', fill: 'FILL / 油漆桶', gradient: 'GRADIENT / 渐变', shape: 'SHAPE / 形状',
  lasso: 'SELECT / 套索', marquee: 'SELECT / 矩形选区', move: 'MOVE / 移动',
} as Record<string, string>)[tool.value] ?? '')
const toolHint = computed(() => ({
  blur: '在画过的地方涂抹，越涂越柔 · [ ] 调大小',
  smudge: '按住拖动，把颜色往前推 · [ ] 调大小',
  fill: '点一下填满颜色相近的区域 · 有选区时只填选区里',
  gradient: '拖一条线：从起点颜色渐变到终点 · Shift 锁定 45°',
  shape: '拖动画形状 · Shift 正方形 / 正圆 / 45° 直线',
  lasso: '拖动圈出选区 · 在选区里拖动 = 移动内容，按住 Alt 复制 · Esc 取消选区',
  marquee: '拖出矩形选区（Shift 正方形）· 在选区里拖动 = 移动内容 · Ctrl+A 全选',
  move: '拖动移动选区里的内容（没有选区时移动整个图层）· 按住 Alt 复制 · T 自由变换',
} as Record<string, string>)[tool.value] ?? 'Ctrl + 滚轮缩放 · 空格 / 右键拖动 · 双指缩放 · [ ] 调粗细')
// ===== 选区（文档坐标的多边形 x, y, x, y, …，只在自己这边） =====
const selection = ref<number[] | null>(null)
/** 正在圈 / 框的选区 */
const selDraft = ref<number[] | null>(null)
/** 正在移动选区内容时的偏移 */
const selOffset = reactive({ x: 0, y: 0 })
const gradLine = ref<{ x0: number; y0: number; x1: number; y1: number } | null>(null)
const recent = ref<string[]>([])
const zoom = ref(1)
const pan = reactive({ x: 0, y: 0 })
const panning = ref(false)
const spaceDown = ref(false)
const brushRing = reactive({ x: 0, y: 0, visible: false })
const copied = ref(false)
const notice = ref(''), noticeKind = ref<'info' | 'error'>('info')
const publishOpen = ref(false)
const publishFiles = ref<File[]>([])
const publishDesc = ref('')
const cursors = reactive(new Map<string, { userId: string; x: number; y: number; at: number }>())
const now = ref(Date.now())

const amOwner = computed(() => same(ownerId.value, props.me))
const layersTopDown = computed(() => [...layers.value].reverse())
const selected = computed(() => layers.value.find(l => l.id === currentLayer.value) ?? null)
const ringSize = computed(() => Math.max(4, (tool.value === 'blur' || tool.value === 'smudge' ? fxSize.value : size.value) * zoom.value))
const showRing = computed(() => brushRing.visible && RING_TOOLS.includes(tool.value) && !panning.value && !spaceDown.value && !xf.value)
const viewCursor = computed(() => {
  if (panning.value) return 'grabbing'
  if (spaceDown.value || tool.value === 'hand') return 'grab'
  if (xf.value) return 'default'
  if (tool.value === 'move') return 'move'
  if (RING_TOOLS.includes(tool.value)) return 'none'
  return 'crosshair'
})
/** 选区轮廓（屏幕坐标的 SVG 路径） */
const selD = computed(() => {
  const draft = selDraft.value
  const t = xf.value
  const poly = draft ?? (t && selection.value ? mapPoly(selection.value, xfMatrix(t)) : selection.value)
  if (!poly || poly.length < 4) return ''
  const ox = draft ? 0 : selOffset.x, oy = draft ? 0 : selOffset.y
  const z = zoom.value
  let d = ''
  for (let i = 0; i + 1 < poly.length; i += 2) d += `${i ? 'L' : 'M'}${((poly[i]! + ox) * z + pan.x).toFixed(1)} ${((poly[i + 1]! + oy) * z + pan.y).toFixed(1)}`
  return `${d}Z`
})
const gradScreen = computed(() => {
  const g = gradLine.value
  if (!g) return null
  const z = zoom.value
  return { x0: g.x0 * z + pan.x, y0: g.y0 * z + pan.y, x1: g.x1 * z + pan.x, y1: g.y1 * z + pan.y }
})
const visibleCursors = computed(() => [...cursors.values()]
  .filter(c => now.value - c.at < 5000)
  .map(c => { const m = members.value.find(x => same(x.userId, c.userId)); return m ? { ...c, name: m.name, color: m.color } : null })
  .filter((c): c is NonNullable<typeof c> => !!c))

function loadPref(k: string, d: string) { try { return localStorage.getItem(`chahui-${k}`) ?? d } catch { return d } }
function savePref(k: string, v: string) { try { localStorage.setItem(`chahui-${k}`, v) } catch { /* 忽略 */ } }
watch(color, v => savePref('color', v))
watch(fx, v => savePref('fx', JSON.stringify(v)), { deep: true })
function loadBrushPref() {
  const p = prefs[prefKeyOf()]!
  p.size = Math.min(p.size, currentBrush.value.maxSize)
  size.value = p.size; opacityPct.value = p.opacity; hardnessPct.value = p.hardness; flags.value = p.flags
}
watch([size, opacityPct, hardnessPct, flags], () => {
  prefs[prefKeyOf()] = { size: size.value, opacity: opacityPct.value, hardness: hardnessPct.value, flags: flags.value }
  savePref('brush-prefs', JSON.stringify(prefs))
})
watch(tool, () => { if (xf.value) void applyTransform() })
watch(tool, t => {
  // 形状的空心线条用画笔的笔刷
  if (t === 'brush' || t === 'eraser') prefTool.value = t
  else if (t === 'shape') prefTool.value = 'brush'
  else return
  loadBrushPref()
})
function selectBrush(kind: BrushKind) {
  brushFor[prefTool.value] = kind
  savePref(prefTool.value === 'brush' ? 'brush-kind' : 'eraser-kind', kind)
  loadBrushPref()
}
function selectCustom(b: BrushView) {
  const t = prefTool.value
  if (!myBrushes.value.some(x => x.id === b.id)) myBrushes.value = [...myBrushes.value, b]
  brushFor[t] = 'custom'
  customFor[t] = b.id
  savePref(t === 'brush' ? 'brush-kind' : 'eraser-kind', 'custom')
  savePref(`${t}-custom`, b.id)
  loadBrushPref()
}
function resetBrush() {
  const t = prefTool.value
  const c = customOf(t)
  prefs[prefKeyOf()] = c ? customDefault(t, c) : defaultPref(t, kindOf(t))
  loadBrushPref()
}

const editorOpen = ref(false)
const editing = ref<BrushView | null>(null)
const libraryOpen = ref(false)
function openEditor(b: BrushView | null) { editing.value = b; editorOpen.value = true }
async function loadMine() {
  try { myBrushes.value = await brushApi.mine() } catch { /* 拿不到就只用预设笔刷 */ }
  loadBrushPref()
  void nextTick(drawPreviews)
}
function onBrushSaved(b: BrushView) {
  const i = myBrushes.value.findIndex(x => x.id === b.id)
  myBrushes.value = i >= 0 ? myBrushes.value.map(x => (x.id === b.id ? b : x)) : [...myBrushes.value, b]
  editorOpen.value = false
  // 改过参数：这支笔刷的大小、不透明度回到新的默认值
  delete prefs[`${prefTool.value}:c:${b.id}`]
  selectCustom(b)
  say(`笔刷「${b.name}」已保存${b.isPublic ? '，并公开到了知识库' : ''}`)
}
function onBrushDeleted(id: string) {
  myBrushes.value = myBrushes.value.filter(b => b.id !== id)
  editorOpen.value = false
  for (const t of ['brush', 'eraser'] as PaintTool[]) if (brushFor[t] === 'custom' && customFor[t] === id) brushFor[t] = 'pen'
  loadBrushPref()
  void nextTick(drawPreviews)
}
function useLibraryBrush(b: BrushView) {
  selectCustom(b)
  libraryOpen.value = false
  void loadMine()
}
function toggleFlag(f: number) { flags.value = flags.value ^ f }
const currentStyle = (t: PaintTool): StrokeStyle => {
  const base = { tool: t, color: color.value, size: size.value, opacity: Math.round(opacityPct.value) / 100 }
  const c = customOf(t)
  if (c) return { ...base, brush: 'custom', hardness: 1, flags: 0, custom: { ...c.params } }
  const kind = kindOf(t)
  return { ...base, brush: kind, hardness: hasHardness.value ? hardnessPct.value / 100 : brushOf(kind).hardness, flags: flags.value }
}

// ===== 笔刷预览：每个按钮上画一小段示意笔画，颜色跟着当前颜色 =====
const previewEls = new Map<BrushKind, HTMLCanvasElement>()
function setPreviewEl(kind: BrushKind, el: HTMLCanvasElement | null) { if (el) previewEls.set(kind, el); else previewEls.delete(kind) }
function drawPreviews() {
  const pts: number[] = []
  for (let i = 0; i <= 40; i++) {
    const t = i / 40
    pts.push(Math.round((12 + t * 136) * 10), Math.round((22 + Math.sin(t * Math.PI * 2) * 11) * 10), Math.round(Math.sin(t * Math.PI) * 100))
  }
  const sc = document.createElement('canvas'); sc.width = 160; sc.height = 44
  const scratch = sc.getContext('2d')!
  for (const b of BRUSHES) {
    const c = previewEls.get(b.kind)
    const cx = c?.getContext('2d')
    if (!c || !cx) continue
    cx.clearRect(0, 0, c.width, c.height)
    const p = prefs[`${prefTool.value}:${b.kind}`]!
    const previewColor = prefTool.value === 'eraser' ? '#8a949c' : color.value
    renderStroke(cx, { tool: 'brush', color: previewColor, size: Math.min(22, Math.max(3, p.size * 0.6)), opacity: p.opacity / 100, brush: b.kind, hardness: p.hardness / 100, flags: p.flags || 0, points: pts }, scratch, 160, 44)
  }
  for (const b of myBrushes.value) {
    const c = customPreviewEls.get(b.id)
    if (c) drawBrushPreview(c, b.params, prefTool.value === 'eraser' ? '#8a949c' : color.value)
  }
}
const customPreviewEls = new Map<string, HTMLCanvasElement>()
function setCustomPreviewEl(id: string, el: HTMLCanvasElement | null) { if (el) customPreviewEls.set(id, el); else customPreviewEls.delete(id) }
// 笔尖图载入完成：画布和预览都重画
const offTip = onTipLoaded(() => { engine.value?.refreshAll(); drawPreviews() })
// 填充的图取回来以后重画
const offPatch = onPatchLoaded(() => engine.value?.refreshAll())
watch([color, prefTool, size, opacityPct, hardnessPct, flags, myBrushes, showBrushBlock], () => void nextTick(drawPreviews))

// ===== 专注模式 / 全屏 / 面板 =====
const rootEl = ref<HTMLElement | null>(null)
const immersive = ref(loadPref('immersive', '1') === '1')
const panelOpen = ref(loadPref('panel', '1') === '1')
const fullscreen = ref(false)
const replayOpen = ref(false)
const canFullscreen = typeof document !== 'undefined' && !!document.documentElement.requestFullscreen
watch(immersive, v => savePref('immersive', v ? '1' : '0'))
watch(panelOpen, v => savePref('panel', v ? '1' : '0'))
watch([immersive, panelOpen, fullscreen], () => void nextTick(() => window.setTimeout(fit, 60)))
async function toggleFullscreen() {
  try {
    if (document.fullscreenElement) await document.exitFullscreen()
    else await rootEl.value?.requestFullscreen()
  } catch { say('浏览器不允许全屏', 'error') }
}
function onFullscreenChange() { fullscreen.value = !!document.fullscreenElement }
const memberColors = computed(() => Object.fromEntries(members.value.map(m => [m.userId.toLowerCase(), m.color])))
watch(smooth, v => savePref('smooth', String(v)))

let noticeTimer = 0
function say(text: string, kind: 'info' | 'error' = 'info') {
  notice.value = text; noticeKind.value = kind
  window.clearTimeout(noticeTimer)
  noticeTimer = window.setTimeout(() => { notice.value = '' }, 4000)
}

const canManage = (l: LayerMeta) => same(l.creatorId, props.me) || amOwner.value
const canDrawOn = (l: LayerMeta | null) => !!l && (!l.locked || same(l.creatorId, props.me))

/** 选一个默认图层：自己建的最上面那个，没有就最上面的 */
function pickDefaultLayer() {
  if (layers.value.some(l => l.id === currentLayer.value)) return
  const mine = [...layers.value].reverse().find(l => same(l.creatorId, props.me))
  currentLayer.value = (mine ?? layers.value[layers.value.length - 1])?.id ?? 0
}

// ================= 进入画室 =================
let ready = false
const queue: (() => void)[] = []
const handlers: [string, (...args: any[]) => void][] = []
function on(name: string, fn: (...args: any[]) => void) {
  const wrapped = (...args: any[]) => { if (ready) fn(...args); else queue.push(() => fn(...args)) }
  handlers.push([name, wrapped])
  props.connection.on(name, wrapped)
}

async function loadBase(layer: LayerMeta): Promise<CanvasImageSource | null> {
  if (!layer.hasBase) return null
  const blob = await request.get<Blob>(`/Paint/${props.room.roomId}/layers/${layer.id}/base`, { params: { v: layer.baseVersion }, responseType: 'blob', silentError: true })
  return await createImageBitmap(blob)
}

async function init() {
  const room = props.room
  usePatchRoom(room.roomId)
  const e = new PaintEngine(stageEl.value!, room.width, room.height)
  engine.value = e
  e.setLayers(room.layers)
  e.loadStrokes(room.strokes.map(s => ({ ...s })))
  try {
    const bases = await Promise.all(room.layers.map(async l => [l.id, await loadBase(l)] as const))
    for (const [id, img] of bases) if (img) e.setBase(id, img)
  } catch (err: any) {
    // 取底图的时候刚好有人合并了图层：整个画室重新载入一次
    if (err?.response?.status === 409) { emit('reload'); return }
    say('有图层的底图没取到，画面可能不完整，可以刷新一下', 'error')
  }
  e.renderAll()
  pickDefaultLayer()
  ready = true
  queue.splice(0).forEach(fn => fn())
  loading.value = false
  await nextTick()
  fit()
  scrollChat()
}

// ================= 服务器事件 =================
const liveKey = (userId: string, key: string) => `${userId.toLowerCase()}:${key}`
on('Members', (list: PaintMemberView[]) => { members.value = list })
on('Owner', (id: string) => { ownerId.value = id })
let selectNewLayer = false
on('Layers', (list: LayerMeta[]) => {
  const before = new Set(layers.value.map(l => l.id))
  engine.value?.setLayers(list)
  layers.value = list
  if (selectNewLayer) {
    const fresh = list.find(l => !before.has(l.id) && same(l.creatorId, props.me))
    if (fresh) { currentLayer.value = fresh.id; selectNewLayer = false }
  }
  for (const id of [...hidden.value]) if (!list.some(l => l.id === id)) hidden.value.delete(id)
  pickDefaultLayer()
})
on('LiveBegin', (d: StrokeStyle & { userId: string; key: string; layerId: number; points: number[] }) => {
  engine.value?.liveBegin(liveKey(d.userId, d.key), d.layerId, { tool: d.tool, color: d.color, size: d.size, opacity: d.opacity, brush: d.brush, hardness: d.hardness, flags: d.flags, custom: d.custom, clip: d.clip }, d.points)
})
on('LivePoints', (d: { userId: string; key: string; points: number[] }) => engine.value?.livePoints(liveKey(d.userId, d.key), d.points))
on('LiveCancel', (d: { userId: string; key: string }) => engine.value?.liveEnd(liveKey(d.userId, d.key)))
on('Stroke', (d: { key: string; stroke: Stroke }) => {
  engine.value?.liveEnd(liveKey(d.stroke.userId, d.key))
  engine.value?.commit(d.stroke)
})
on('Undone', (d: { seq: number; undone: boolean }) => engine.value?.setUndone(d.seq, d.undone))
on('Cleared', (d: { layerId: number; baseVersion: number }) => {
  engine.value?.clearLayer(d.layerId)
  patchLayer(d.layerId, { hasBase: false, baseVersion: d.baseVersion })
})
// 服务器请自己把旧笔画合成底图上传
const bakedCache = new Map<number, { upto: number; canvas: HTMLCanvasElement }>()
on('BakeRequest', (d: { layerId: number; upto: number }) => {
  const c = engine.value?.bake(d.layerId, d.upto)
  if (!c) return
  bakedCache.set(d.layerId, { upto: d.upto, canvas: c })
  c.toBlob(async blob => {
    if (!blob) return
    const body = new FormData()
    body.append('file', blob, 'layer.png')
    try { await request.post(`/Paint/${props.room.roomId}/layers/${d.layerId}/base`, body, { params: { upto: d.upto }, silentError: true }) } catch { bakedCache.delete(d.layerId) }
  }, 'image/png')
})
on('Baked', async (d: { layerId: number; upto: number; baseVersion: number }) => {
  const cached = bakedCache.get(d.layerId)
  bakedCache.delete(d.layerId)
  patchLayer(d.layerId, { hasBase: true, baseVersion: d.baseVersion })
  const e = engine.value
  if (!e || e.applyBake(d.layerId, d.upto, cached && cached.upto === d.upto ? cached.canvas : null)) return
  // 本地合成不了（有填充的图还没取到）：直接取服务器上合好的底图
  const meta = layers.value.find(l => l.id === d.layerId)
  if (!meta) return
  try {
    const img = await loadBase({ ...meta, hasBase: true, baseVersion: d.baseVersion })
    if (img) e.applyBake(d.layerId, d.upto, img)
  } catch (err: any) { if (err?.response?.status === 409) emit('reload') }
})
on('Chat', (m: PaintChatMsg) => { chat.value.push(m); if (chat.value.length > 120) chat.value.shift(); scrollChat() })
on('Cursor', (d: { userId: string; x: number; y: number }) => { cursors.set(d.userId.toLowerCase(), { userId: d.userId, x: d.x, y: d.y, at: Date.now() }) })
on('Left', (reason: string) => emit('left', reason))

function patchLayer(id: number, patch: Partial<LayerMeta>) {
  layers.value = layers.value.map(l => (l.id === id ? { ...l, ...patch } : l))
}

// ================= 调用 =================
async function invoke<T = string | null>(method: string, ...args: unknown[]): Promise<T | undefined> {
  try { return await props.connection.invoke<T>(method, ...args) } catch { say('网络不稳定，操作没有送达', 'error'); return undefined }
}
async function act(method: string, ...args: unknown[]) {
  const err = await invoke<string | null>(method, ...args)
  if (err) say(err, 'error')
}
const undo = () => act('Undo')
const redo = () => act('Redo')
const layerAct = (action: string, layerId: number, text = '', value = 0) => {
  if (action === 'add') selectNewLayer = true
  return act('Layer', action, layerId, text, value)
}
function renameLayer() {
  const l = selected.value
  if (!l) return
  const name = window.prompt('图层名字', l.name)
  if (name && name.trim() && name.trim() !== l.name) void layerAct('rename', l.id, name.trim())
}
function clearLayer() {
  const l = selected.value
  if (l && window.confirm(`清空「${l.name}」上的所有内容？这一步不能撤销。`)) void layerAct('clear', l.id)
}
function deleteLayer() {
  const l = selected.value
  if (l && window.confirm(`删除图层「${l.name}」？这一步不能撤销。`)) void layerAct('delete', l.id)
}
function onLayerOpacity(e: Event) {
  const l = selected.value
  if (l) void layerAct('opacity', l.id, '', Number((e.target as HTMLInputElement).value) / 100)
}
function toggleHidden(id: number) {
  const next = new Set(hidden.value)
  if (next.has(id)) next.delete(id); else next.add(id)
  hidden.value = next
  engine.value?.setHidden(id, next.has(id))
}
function kick(m: PaintMemberView) { if (window.confirm(`请 ${m.name} 离开画室？`)) void act('Kick', m.userId) }
async function sendChat() {
  const text = chatText.value.trim()
  if (!text) return
  chatText.value = ''
  await act('Chat', text)
}
function scrollChat() { void nextTick(() => { if (chatEl.value) chatEl.value.scrollTop = chatEl.value.scrollHeight }) }
async function leave() {
  if (!window.confirm('离开画室？画室会保留到所有人离开 30 分钟后；还没导出的画记得先导出或发到画廊。')) return
  await invoke('Leave')
  emit('left', 'left')
}
async function copyLink() {
  const url = `${location.origin}${location.pathname}?room=${props.room.roomId}`
  try { await navigator.clipboard.writeText(url); copied.value = true; window.setTimeout(() => { copied.value = false }, 1500) } catch { say(`邀请链接：${url}`) }
}

// ================= 视图：缩放、拖动 =================
function fit() {
  const v = viewEl.value
  if (!v) return
  const r = v.getBoundingClientRect()
  const z = Math.min((r.width - 32) / props.room.width, (r.height - 32) / props.room.height)
  zoom.value = Math.max(0.05, Math.min(4, z))
  pan.x = (r.width - props.room.width * zoom.value) / 2
  pan.y = (r.height - props.room.height * zoom.value) / 2
}
function zoomAt(factor: number, cx: number, cy: number) {
  const nz = Math.max(0.05, Math.min(8, zoom.value * factor))
  pan.x = cx - (cx - pan.x) * (nz / zoom.value)
  pan.y = cy - (cy - pan.y) * (nz / zoom.value)
  zoom.value = nz
}
function zoomBy(f: number) {
  const r = viewEl.value?.getBoundingClientRect()
  if (r) zoomAt(f, r.width / 2, r.height / 2)
}
/** Ctrl / ⌘ + 滚轮（触控板双指捏合也是）缩放；普通滚轮上下移动，Shift + 滚轮左右移动 */
function onWheel(e: WheelEvent) {
  const r = viewEl.value!.getBoundingClientRect()
  if (e.ctrlKey || e.metaKey) zoomAt(Math.exp(-e.deltaY * 0.0025), e.clientX - r.left, e.clientY - r.top)
  else if (e.shiftKey) pan.x -= e.deltaY || e.deltaX
  else { pan.x -= e.deltaX; pan.y -= e.deltaY }
}
/** 屏幕坐标 → 文档坐标 */
function toDoc(e: { clientX: number; clientY: number }) {
  const r = viewEl.value!.getBoundingClientRect()
  return { x: (e.clientX - r.left - pan.x) / zoom.value, y: (e.clientY - r.top - pan.y) / zoom.value, sx: e.clientX - r.left, sy: e.clientY - r.top }
}

// ================= 画 =================
interface Active { key: string; pointerId: number; layerId: number; style: StrokeStyle; all: number[]; buffer: number[]; sx: number; sy: number; last: [number, number] | null; began: boolean }
let active: Active | null = null
let flushTimer = 0
const touches = new Map<number, { x: number; y: number }>()
let pinch: { dist: number; mx: number; my: number } | null = null
let panStart: { x: number; y: number; px: number; py: number; pointerId: number } | null = null
let cursorSentAt = 0

function newKey() { return Math.random().toString(36).slice(2, 10) + Date.now().toString(36).slice(-4) }

function pushPoint(a: Active, x: number, y: number, pressure: number) {
  // 画出画布太远的部分没意义，也会被服务器拒绝：夹在画布外 150 像素以内
  x = Math.max(-150, Math.min(props.room.width + 150, x))
  y = Math.max(-150, Math.min(props.room.height + 150, y))
  // 平滑：位置向手指 / 笔尖慢慢靠拢
  const k = 1 / (1 + smooth.value * 0.55)
  a.sx += (x - a.sx) * k
  a.sy += (y - a.sy) * k
  const px = a.sx, py = a.sy
  if (a.last && Math.hypot(px - a.last[0], py - a.last[1]) < 0.6) return
  a.last = [px, py]
  const tri = [Math.round(px * 10), Math.round(py * 10), Math.round(Math.max(0, Math.min(1, pressure)) * 100)]
  a.all.push(...tri)
  a.buffer.push(...tri)
  engine.value?.mineAdd(a.key, tri)
  if (a.buffer.length >= 600) flush()
}

function flush() {
  const a = active
  if (!a || !a.buffer.length) return
  const chunk = a.buffer.splice(0)
  if (!a.began) {
    a.began = true
    void props.connection.invoke<string | null>('StrokeBegin', a.key, a.layerId, a.style, chunk)
      .then(err => { if (err) say(err, 'error') }).catch(() => undefined)
  } else void props.connection.send('StrokePoints', a.key, chunk).catch(() => undefined)
}

const pressureOf = (e: PointerEvent) => (e.pointerType === 'pen' ? (e.pressure || 0.5) : 1)

function capture(id: number) { try { viewEl.value?.setPointerCapture(id) } catch { /* 有的浏览器在指针已经抬起时会报错，忽略 */ } }

function onDown(e: PointerEvent) {
  if (loading.value) return
  const p = toDoc(e)
  if (e.pointerType === 'touch') {
    touches.set(e.pointerId, { x: p.sx, y: p.sy })
    if (touches.size === 2) {
      // 两根手指：取消正在画的一笔，改成缩放 / 拖动
      cancelActive()
      cancelDrag()
      const [a, b] = [...touches.values()]
      pinch = { dist: Math.hypot(a!.x - b!.x, a!.y - b!.y), mx: (a!.x + b!.x) / 2, my: (a!.y + b!.y) / 2 }
      return
    }
    if (touches.size > 2) return
  }
  if (e.button === 1 || e.button === 2 || tool.value === 'hand' || spaceDown.value) {
    panning.value = true
    panStart = { x: p.sx, y: p.sy, px: pan.x, py: pan.y, pointerId: e.pointerId }
    capture(e.pointerId)
    return
  }
  if (e.button !== 0) return
  if (tool.value === 'picker') {
    const c = engine.value?.pickColor(p.x, p.y)
    if (c) { color.value = c; tool.value = 'brush' }
    return
  }
  if (drag) return
  if (xf.value) return beginXfDrag(e, p)
  const tk = tool.value
  if (tk === 'lasso' || tk === 'marquee' || tk === 'move') return beginSelectTool(e, p, tk)
  const layer = drawableLayer()
  if (!layer) return
  if (tk === 'fill') return void doFill(p.x, p.y, layer)
  if (tk === 'gradient' || tk === 'shape') return beginShapeDrag(e, p, tk, layer)
  const t = tk as StrokeTool
  const style = styleFor(t)
  active = { key: newKey(), pointerId: e.pointerId, layerId: layer.id, style, all: [], buffer: [], sx: p.x, sy: p.y, last: null, began: false }
  engine.value?.mineBegin(active.key, layer.id, style)
  pushPoint(active, p.x, p.y, pressureOf(e))
  capture(e.pointerId)
  window.clearInterval(flushTimer)
  flushTimer = window.setInterval(flush, 50)
  if (t === 'brush') rememberColor(color.value)
}

function onMove(e: PointerEvent) {
  const p = toDoc(e)
  if (drag && e.pointerId === drag.pointerId) updateDrag(drag, p, e.shiftKey)
  brushRing.x = p.sx; brushRing.y = p.sy; brushRing.visible = e.pointerType !== 'touch'
  if (e.pointerType === 'touch' && touches.has(e.pointerId)) {
    touches.set(e.pointerId, { x: p.sx, y: p.sy })
    if (pinch && touches.size === 2) {
      const [a, b] = [...touches.values()]
      const dist = Math.hypot(a!.x - b!.x, a!.y - b!.y), mx = (a!.x + b!.x) / 2, my = (a!.y + b!.y) / 2
      pan.x += mx - pinch.mx; pan.y += my - pinch.my
      if (pinch.dist > 0) zoomAt(dist / pinch.dist, mx, my)
      pinch = { dist, mx, my }
      return
    }
  }
  if (panning.value && panStart && e.pointerId === panStart.pointerId) {
    pan.x = panStart.px + p.sx - panStart.x
    pan.y = panStart.py + p.sy - panStart.y
    return
  }
  if (active && e.pointerId === active.pointerId) {
    const events = typeof e.getCoalescedEvents === 'function' ? e.getCoalescedEvents() : []
    for (const ev of events.length ? events : [e]) { const q = toDoc(ev); pushPoint(active, q.x, q.y, pressureOf(ev)) }
  }
  // 光标位置发给别人（最多每 60 毫秒一次）
  const t = performance.now()
  if (t - cursorSentAt > 60 && p.x >= 0 && p.y >= 0 && p.x <= props.room.width && p.y <= props.room.height) {
    cursorSentAt = t
    void props.connection.send('Cursor', p.x, p.y).catch(() => undefined)
  }
}

async function onUp(e: PointerEvent) {
  touches.delete(e.pointerId)
  if (touches.size < 2) pinch = null
  if (panStart && e.pointerId === panStart.pointerId) { panning.value = false; panStart = null; return }
  if (drag && e.pointerId === drag.pointerId) {
    const d = drag
    drag = null
    updateDrag(d, toDoc(e), e.shiftKey)
    await finishDrag(d)
    return
  }
  const a = active
  if (!a || e.pointerId !== a.pointerId) return
  active = null
  window.clearInterval(flushTimer)
  // 平滑会落后一点，抬笔时补上最后的真实位置
  const p = toDoc(e)
  const tri = [Math.round(p.x * 10), Math.round(p.y * 10), Math.round(pressureOf(e) * 100)]
  if (smooth.value > 0 && a.last && Math.hypot(p.x - a.last[0], p.y - a.last[1]) >= 0.6) { a.all.push(...tri); a.buffer.push(...tri); engine.value?.mineAdd(a.key, tri) }
  if (!a.began) {
    // 很短的一笔（点一下）：开始和结束一起发
    a.began = true
    const err = await invoke<string | null>('StrokeBegin', a.key, a.layerId, a.style, a.buffer.splice(0))
    if (err) { say(err, 'error'); engine.value?.mineDone(a.key, null); return }
  }
  const res = await invoke<{ error: string | null; seq: number }>('StrokeEnd', a.key, a.buffer.splice(0))
  if (res?.error) say(res.error, 'error')
  if (res && res.seq > 0) engine.value?.mineDone(a.key, { ...a.style, points: a.all, seq: res.seq, layerId: a.layerId, userId: props.me, undone: false })
  else engine.value?.mineDone(a.key, null)
}

function cancelActive() {
  const a = active
  if (!a) return
  active = null
  window.clearInterval(flushTimer)
  engine.value?.mineDone(a.key, null)
  if (a.began) void props.connection.send('StrokeCancel', a.key).catch(() => undefined)
}
function onCancel(e: PointerEvent) {
  touches.delete(e.pointerId)
  if (touches.size < 2) pinch = null
  if (panStart && e.pointerId === panStart.pointerId) { panning.value = false; panStart = null }
  if (active && e.pointerId === active.pointerId) cancelActive()
  if (drag && e.pointerId === drag.pointerId) cancelDrag()
}
function onLeave() { brushRing.visible = false }

// ================= 其他工具：模糊 / 涂抹 / 油漆桶 / 渐变 / 形状 / 选区 =================

/** 当前图层能不能画（不能时提示原因） */
function drawableLayer(): LayerMeta | null {
  const layer = selected.value
  if (!layer) { say('先选一个图层', 'error'); return null }
  if (!canDrawOn(layer)) { say(`「${layer.name}」被 ${layer.creatorName} 锁定了，换个图层画吧`, 'error'); return null }
  if (hidden.value.has(layer.id)) { say('当前图层被你隐藏了，先点眼睛显示出来', 'error'); return null }
  return layer
}

/** 选区 → 笔画的 clip（x*10, y*10, …），没有选区为 null */
const clipOf = () => (selection.value ? selection.value.map(v => Math.round(v * 10)) : null)
/** 多边形 / 点列（x, y, …）→ 笔画点（x*10, y*10, 100, …） */
function tri(poly: number[]) {
  const out: number[] = []
  for (let i = 0; i + 1 < poly.length; i += 2) out.push(Math.round(poly[i]! * 10), Math.round(poly[i + 1]! * 10), 100)
  return out
}
const clampX = (x: number) => Math.max(0, Math.min(props.room.width, x))
const clampY = (y: number) => Math.max(0, Math.min(props.room.height, y))
/** 拖动终点：Shift 时锁成正方形（square）或 45° 方向（angle） */
function constrain(x0: number, y0: number, x1: number, y1: number, shift: boolean, mode: 'square' | 'angle') {
  if (!shift) return [x1, y1] as const
  const dx = x1 - x0, dy = y1 - y0
  if (mode === 'square') { const m = Math.max(Math.abs(dx), Math.abs(dy)); return [x0 + Math.sign(dx || 1) * m, y0 + Math.sign(dy || 1) * m] as const }
  const len = Math.hypot(dx, dy), a = Math.round(Math.atan2(dy, dx) / (Math.PI / 4)) * (Math.PI / 4)
  return [x0 + Math.cos(a) * len, y0 + Math.sin(a) * len] as const
}
const rectPoly = (x0: number, y0: number, x1: number, y1: number) => [x0, y0, x1, y0, x1, y1, x0, y1]
function ellipsePoly(x0: number, y0: number, x1: number, y1: number) {
  const cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, rx = Math.abs(x1 - x0) / 2, ry = Math.abs(y1 - y0) / 2
  const n = Math.max(24, Math.min(360, Math.round((Math.PI * (rx + ry)) / 3)))
  const out: number[] = []
  for (let i = 0; i < n; i++) { const a = (i / n) * Math.PI * 2; out.push(cx + Math.cos(a) * rx, cy + Math.sin(a) * ry) }
  return out
}
/** 每隔 2 像素插一个点（形状的线条用笔刷画，点要够密，转角才不会被抹圆） */
function densify(poly: number[], closed: boolean) {
  const pts = closed ? [...poly, poly[0]!, poly[1]!] : poly
  const out: number[] = [pts[0]!, pts[1]!]
  for (let i = 2; i + 1 < pts.length; i += 2) {
    const ax = pts[i - 2]!, ay = pts[i - 1]!, bx = pts[i]!, by = pts[i + 1]!
    const n = Math.max(1, Math.ceil(Math.hypot(bx - ax, by - ay) / 2))
    for (let k = 1; k <= n; k++) out.push(ax + ((bx - ax) * k) / n, ay + ((by - ay) * k) / n)
  }
  return out
}
/** 选区最多 2000 个顶点（服务器上限 3000） */
function capPolygon(poly: number[]) {
  let eps = 0.6, out = simplifyPolygon(poly, eps)
  while (out.length > 4000 && eps < 64) { eps *= 1.6; out = simplifyPolygon(poly, eps) }
  return out
}

/** 画笔 / 橡皮 / 模糊 / 涂抹落笔时的样式（有选区时只画在选区里） */
function styleFor(t: StrokeTool): StrokeStyle {
  if (t === 'blur' || t === 'smudge') {
    return { tool: t, color: '#000000', size: fxSize.value, opacity: Math.max(5, fxStrength.value) / 100, brush: 'soft', hardness: 0.5, flags: 0, clip: clipOf() }
  }
  return { ...currentStyle(t), clip: clipOf() }
}

/** 把一个一次性的操作发给服务器（点多时分几段发），成功后本地预览换成正式的笔画 */
async function sendOp(key: string, layerId: number, style: StrokeStyle, points: number[]): Promise<boolean> {
  const e = engine.value
  const chunks: number[][] = []
  for (let i = 0; i < points.length; i += 900) chunks.push(points.slice(i, i + 900))
  const first = chunks.shift() ?? []
  const last = chunks.pop() ?? []
  const err = await invoke<string | null>('StrokeBegin', key, layerId, style, first)
  if (err !== null) { if (err) say(err, 'error'); e?.mineDone(key, null); return false }
  for (const c of chunks) await props.connection.send('StrokePoints', key, c).catch(() => undefined)
  const res = await invoke<{ error: string | null; seq: number }>('StrokeEnd', key, last)
  if (res?.error) say(res.error, 'error')
  if (res && res.seq > 0) { e?.mineDone(key, { ...style, points, seq: res.seq, layerId, userId: props.me, undone: false }); return true }
  e?.mineDone(key, null)
  return false
}

// ----- 拖动类工具：套索、矩形选区、移动、渐变、形状 -----
interface Drag {
  kind: 'lasso' | 'marquee' | 'move' | 'gradient' | 'shape' | 'xf'
  pointerId: number; x0: number; y0: number
  /** 套索：已经圈过的点；渐变 / 形状：当前的笔画点 */
  pts: number[]
  key: string; layerId: number; style: StrokeStyle | null
  /** 移动：要移动的多边形和当前偏移 */
  poly: number[] | null; dx: number; dy: number
  /** 自由变换：拖的是哪个控制点，按下时的状态 */
  xf?: { mode: 'move' | 'rot' | 'scale'; hx: number; hy: number; p0x: number; p0y: number; ax: number; ay: number; start: { tx: number; ty: number; sx: number; sy: number; rot: number } }
}
let drag: Drag | null = null

// ----- 自由变换（缩放、旋转、移动选区内容） -----
interface Xf {
  key: string; layerId: number
  /** 变换的多边形（选区，没有选区时是整张画布） */
  poly: number[]
  /** 多边形的外接矩形（变换前） */
  x0: number; y0: number; x1: number; y1: number
  tx: number; ty: number; sx: number; sy: number; rot: number
}
const xf = ref<Xf | null>(null)
const r4 = (v: number) => Math.round(v * 1e4) / 1e4
/** 绕外接矩形中心：先缩放、再旋转、再平移。返回 canvas 矩阵 a, b, c, d, e, f（和服务器一样保留 4 位小数） */
function xfMatrix(t: Xf): number[] {
  const cx = (t.x0 + t.x1) / 2, cy = (t.y0 + t.y1) / 2
  const cos = Math.cos(t.rot), sin = Math.sin(t.rot)
  const a = cos * t.sx, b = sin * t.sx, c = -sin * t.sy, d = cos * t.sy
  const m = [a, b, c, d, cx + t.tx - (a * cx + c * cy), cy + t.ty - (b * cx + d * cy)].map(r4)
  // 只是平移时取整，像素一一对应、不会变糊
  if (t.rot === 0 && t.sx === 1 && t.sy === 1) { m[4] = Math.round(m[4]!); m[5] = Math.round(m[5]!) }
  return m
}
const applyM = (m: number[], x: number, y: number) => [m[0]! * x + m[2]! * y + m[4]!, m[1]! * x + m[3]! * y + m[5]!] as const
function mapPoly(poly: number[], m: number[]) {
  const out: number[] = []
  for (let i = 0; i + 1 < poly.length; i += 2) out.push(...applyM(m, poly[i]!, poly[i + 1]!))
  return out
}
/** 变换框的控制点（屏幕坐标）：8 个缩放点 + 旋转点 */
const xfHandles = computed(() => {
  const t = xf.value
  if (!t) return null
  const m = xfMatrix(t)
  const z = zoom.value
  const scr = (x: number, y: number) => { const [dx, dy] = applyM(m, x, y); return { x: dx * z + pan.x, y: dy * z + pan.y } }
  const cx = (t.x0 + t.x1) / 2, cy = (t.y0 + t.y1) / 2
  const pts: { hx: number; hy: number; x: number; y: number }[] = []
  for (const hy of [-1, 0, 1]) for (const hx of [-1, 0, 1]) {
    if (!hx && !hy) continue
    pts.push({ hx, hy, ...scr(cx + (hx * (t.x1 - t.x0)) / 2, cy + (hy * (t.y1 - t.y0)) / 2) })
  }
  const box = [scr(t.x0, t.y0), scr(t.x1, t.y0), scr(t.x1, t.y1), scr(t.x0, t.y1)]
  const c = scr(cx, cy), top = scr(cx, t.y0)
  const len = Math.hypot(top.x - c.x, top.y - c.y) || 1
  const rot = { x: top.x + ((top.x - c.x) / len) * 26, y: top.y + ((top.y - c.y) / len) * 26 }
  return { pts, box, top, rot, d: `M${box.map(p => `${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join('L')}Z` }
})

function startTransform() {
  if (xf.value || drag) return
  const layer = drawableLayer()
  if (!layer) return
  const poly = selection.value ?? rectPoly(0, 0, props.room.width, props.room.height)
  const b = polygonBounds(poly)
  if (b.x1 - b.x0 < 2 || b.y1 - b.y0 < 2) return say('选区太小了', 'error')
  const key = newKey()
  const style: StrokeStyle = { tool: 'xform', color: '#000000', size: 1, opacity: 1, brush: 'pen', hardness: 1, flags: 0, matrix: [1, 0, 0, 1, 0, 0] }
  engine.value?.mineBegin(key, layer.id, style)
  engine.value?.mineUpdate(key, { points: tri(poly) })
  xf.value = { key, layerId: layer.id, poly, x0: b.x0, y0: b.y0, x1: b.x1, y1: b.y1, tx: 0, ty: 0, sx: 1, sy: 1, rot: 0 }
}
function refreshXf() { const t = xf.value; if (t) engine.value?.mineUpdate(t.key, { matrix: xfMatrix(t) }) }
async function applyTransform() {
  const t = xf.value
  if (!t) return
  xf.value = null
  if (drag?.kind === 'xf') drag = null
  const m = xfMatrix(t)
  if (m.join() === '1,0,0,1,0,0') { engine.value?.mineDone(t.key, null); return }
  const before = selection.value
  if (before) selection.value = mapPoly(before, m)
  const style: StrokeStyle = { tool: 'xform', color: '#000000', size: 1, opacity: 1, brush: 'pen', hardness: 1, flags: 0, matrix: m }
  const ok = await sendOp(t.key, t.layerId, style, tri(t.poly))
  if (!ok && before) selection.value = before
}
function cancelTransform() {
  const t = xf.value
  if (!t) return
  xf.value = null
  if (drag?.kind === 'xf') drag = null
  engine.value?.mineDone(t.key, null)
}
/** 变换模式下按下鼠标：点在控制点上 = 缩放 / 旋转，框里 = 移动，框外 = 旋转 */
function beginXfDrag(e: PointerEvent, p: { x: number; y: number; sx: number; sy: number }) {
  const t = xf.value!, h = xfHandles.value!
  const near = (q: { x: number; y: number }) => Math.hypot(q.x - p.sx, q.y - p.sy) <= 10
  const handle = h.pts.find(near)
  const start = { tx: t.tx, ty: t.ty, sx: t.sx, sy: t.sy, rot: t.rot }
  let mode: 'move' | 'rot' | 'scale' = 'rot', hx = 0, hy = 0, ax = 0, ay = 0
  if (handle) {
    mode = 'scale'; hx = handle.hx; hy = handle.hy
    // 对面的点固定不动
    const cx = (t.x0 + t.x1) / 2, cy = (t.y0 + t.y1) / 2
    ;[ax, ay] = applyM(xfMatrix(t), cx - (hx * (t.x1 - t.x0)) / 2, cy - (hy * (t.y1 - t.y0)) / 2)
  } else if (!near(h.rot) && pointInPolygon(h.box.flatMap(q => [q.x, q.y]), p.sx, p.sy)) mode = 'move'
  drag = { kind: 'xf', pointerId: e.pointerId, x0: p.x, y0: p.y, pts: [], key: '', layerId: t.layerId, style: null, poly: null, dx: 0, dy: 0, xf: { mode, hx, hy, p0x: p.x, p0y: p.y, ax, ay, start } }
  capture(e.pointerId)
}
function updateXfDrag(d: Drag, p: { x: number; y: number }, shift: boolean) {
  const t = xf.value, s = d.xf
  if (!t || !s) return
  const cx = (t.x0 + t.x1) / 2, cy = (t.y0 + t.y1) / 2
  if (s.mode === 'move') {
    t.tx = s.start.tx + Math.round(p.x - s.p0x)
    t.ty = s.start.ty + Math.round(p.y - s.p0y)
  } else if (s.mode === 'rot') {
    const ccx = cx + t.tx, ccy = cy + t.ty
    let r = s.start.rot + Math.atan2(p.y - ccy, p.x - ccx) - Math.atan2(s.p0y - ccy, s.p0x - ccx)
    if (shift) r = Math.round(r / (Math.PI / 12)) * (Math.PI / 12)
    t.rot = Math.abs(r) < 1e-9 ? 0 : r
  } else {
    const W = t.x1 - t.x0, H = t.y1 - t.y0
    const cos = Math.cos(t.rot), sin = Math.sin(t.rot)
    const dx = p.x - s.ax, dy = p.y - s.ay
    const lim = (v: number) => (Math.abs(v) < 0.01 ? 0.01 * (Math.sign(v) || 1) : Math.max(-16, Math.min(16, v)))
    let sx = s.start.sx, sy = s.start.sy
    if (s.hx) sx = lim((dx * cos + dy * sin) / (s.hx * W))
    if (s.hy) sy = lim((-dx * sin + dy * cos) / (s.hy * H))
    if (shift && s.hx && s.hy) {
      const rx = sx / s.start.sx, ry = sy / s.start.sy
      const r = Math.abs(rx) > Math.abs(ry) ? rx : ry
      sx = lim(s.start.sx * r); sy = lim(s.start.sy * r)
    }
    // 保持对面的点不动：新中心 = 固定点 - R·S·(固定点相对中心的偏移)
    const vx = (-sx * s.hx * W) / 2, vy = (-sy * s.hy * H) / 2
    t.sx = sx; t.sy = sy
    t.tx = s.ax - (cos * vx - sin * vy) - cx
    t.ty = s.ay - (sin * vx + cos * vy) - cy
  }
  refreshXf()
}

function beginSelectTool(e: PointerEvent, p: { x: number; y: number }, t: 'lasso' | 'marquee' | 'move') {
  const sel = selection.value
  if (t === 'move' || (sel && pointInPolygon(sel, p.x, p.y))) {
    // 移动选区里的内容（移动工具没有选区时移动整个图层）
    const layer = drawableLayer()
    if (!layer) return
    const W = props.room.width, H = props.room.height
    const poly = sel ?? rectPoly(0, 0, W, H)
    const key = newKey()
    const style: StrokeStyle = { tool: 'xform', color: '#000000', size: 1, opacity: 1, brush: 'pen', hardness: 1, flags: e.altKey ? FLAG_COPY : 0, matrix: [1, 0, 0, 1, 0, 0] }
    engine.value?.mineBegin(key, layer.id, style)
    engine.value?.mineUpdate(key, { points: tri(poly) })
    drag = { kind: 'move', pointerId: e.pointerId, x0: p.x, y0: p.y, pts: [], key, layerId: layer.id, style, poly, dx: 0, dy: 0 }
    capture(e.pointerId)
    return
  }
  // 圈新的选区
  const x = clampX(p.x), y = clampY(p.y)
  drag = { kind: t, pointerId: e.pointerId, x0: x, y0: y, pts: [x, y], key: '', layerId: 0, style: null, poly: null, dx: 0, dy: 0 }
  selDraft.value = t === 'lasso' ? [x, y] : rectPoly(x, y, x, y)
  capture(e.pointerId)
}

function beginShapeDrag(e: PointerEvent, p: { x: number; y: number }, t: 'gradient' | 'shape', layer: LayerMeta) {
  const key = newKey()
  const base = { size: 1, brush: 'pen' as BrushKind, hardness: 1, clip: clipOf() }
  const style: StrokeStyle = t === 'gradient'
    ? { ...base, tool: 'gradient', color: color.value, opacity: fx.gradOpacity / 100, flags: fx.gradType === 'radial' ? FLAG_RADIAL : 0, color2: fx.gradEnd === 'color' ? fx.gradColor2 : null }
    : fx.shapeFill && fx.shapeKind !== 'line'
      ? { ...base, tool: 'polyfill', color: color.value, opacity: Math.round(opacityPct.value) / 100, flags: 0 }
      : styleFor('brush')
  const x = Math.max(-150, Math.min(props.room.width + 150, p.x)), y = Math.max(-150, Math.min(props.room.height + 150, p.y))
  engine.value?.mineBegin(key, layer.id, style)
  drag = { kind: t, pointerId: e.pointerId, x0: x, y0: y, pts: [], key, layerId: layer.id, style, poly: null, dx: 0, dy: 0 }
  capture(e.pointerId)
  rememberColor(color.value)
}

function updateDrag(d: Drag, p: { x: number; y: number }, shift: boolean) {
  const e = engine.value
  switch (d.kind) {
    case 'xf': return updateXfDrag(d, p, shift)
    case 'lasso': {
      const x = clampX(p.x), y = clampY(p.y)
      const n = d.pts.length
      if (Math.hypot(x - d.pts[n - 2]!, y - d.pts[n - 1]!) * zoom.value < 2) return
      d.pts.push(x, y)
      selDraft.value = [...d.pts]
      return
    }
    case 'marquee': {
      const [x1, y1] = constrain(d.x0, d.y0, clampX(p.x), clampY(p.y), shift, 'square')
      selDraft.value = rectPoly(d.x0, d.y0, clampX(x1), clampY(y1))
      return
    }
    case 'move': {
      d.dx = Math.round(p.x - d.x0); d.dy = Math.round(p.y - d.y0)
      selOffset.x = d.dx; selOffset.y = d.dy
      e?.mineUpdate(d.key, { matrix: [1, 0, 0, 1, d.dx, d.dy] })
      return
    }
    case 'gradient': {
      const lim = (v: number, m: number) => Math.max(-150, Math.min(m + 150, v))
      const [x1, y1] = constrain(d.x0, d.y0, p.x, p.y, shift, 'angle')
      const g = { x0: d.x0, y0: d.y0, x1: lim(x1, props.room.width), y1: lim(y1, props.room.height) }
      gradLine.value = g
      d.pts = tri([g.x0, g.y0, g.x1, g.y1])
      e?.mineUpdate(d.key, { points: d.pts })
      return
    }
    case 'shape': {
      const kind = fx.shapeKind
      const lim = (v: number, m: number) => Math.max(-150, Math.min(m + 150, v))
      let [x1, y1] = constrain(d.x0, d.y0, p.x, p.y, shift, kind === 'line' ? 'angle' : 'square')
      x1 = lim(x1, props.room.width); y1 = lim(y1, props.room.height)
      const poly = kind === 'line' ? [d.x0, d.y0, x1, y1] : kind === 'rect' ? rectPoly(d.x0, d.y0, x1, y1) : ellipsePoly(d.x0, d.y0, x1, y1)
      d.pts = d.style?.tool === 'polyfill' ? tri(poly) : tri(densify(poly, kind !== 'line'))
      e?.mineUpdate(d.key, { points: d.pts })
    }
  }
}

async function finishDrag(d: Drag) {
  const e = engine.value
  switch (d.kind) {
    case 'xf': return
    case 'lasso':
    case 'marquee': {
      const poly = selDraft.value
      selDraft.value = null
      const simple = poly && poly.length >= 6 ? capPolygon(poly) : null
      selection.value = simple && simple.length >= 6 && polygonArea(simple) >= 4 ? simple : null
      return
    }
    case 'move': {
      selOffset.x = selOffset.y = 0
      if (!d.dx && !d.dy) { e?.mineDone(d.key, null); return }
      // 选区先跟着走，失败再退回去
      const before = selection.value
      if (before) selection.value = before.map((v, i) => v + (i % 2 ? d.dy : d.dx))
      const ok = await sendOp(d.key, d.layerId, { ...d.style!, matrix: [1, 0, 0, 1, d.dx, d.dy] }, tri(d.poly!))
      if (!ok && before) selection.value = before
      return
    }
    case 'gradient': {
      gradLine.value = null
      const p = d.pts
      if (p.length < 6 || Math.hypot(p[3]! - p[0]!, p[4]! - p[1]!) < 20) { e?.mineDone(d.key, null); return }
      await sendOp(d.key, d.layerId, d.style!, p)
      return
    }
    case 'shape': {
      const b = d.pts.length ? polygonBounds(d.pts.filter((_, i) => i % 3 !== 2)) : null
      if (!b || (b.x1 - b.x0 < 10 && b.y1 - b.y0 < 10)) { e?.mineDone(d.key, null); return }
      await sendOp(d.key, d.layerId, d.style!, d.pts)
    }
  }
}

function cancelDrag() {
  const d = drag
  if (!d) return
  drag = null
  selDraft.value = null
  selOffset.x = selOffset.y = 0
  gradLine.value = null
  if (d.key) engine.value?.mineDone(d.key, null)
}

function swapGradColors() { const c = color.value; color.value = fx.gradColor2; fx.gradColor2 = c }

// ----- 选区操作 -----
function selectAll() { selection.value = rectPoly(0, 0, props.room.width, props.room.height) }
function deselect() { selection.value = null }

async function selectionOp(kind: 'fill' | 'delete' | 'flipH' | 'flipV') {
  const sel = selection.value
  if (!sel) return
  const layer = drawableLayer()
  if (!layer) return
  const key = newKey()
  const pts = tri(sel)
  const base = { size: 1, brush: 'pen' as BrushKind, hardness: 1 }
  let style: StrokeStyle
  let flipped: number[] | null = null
  if (kind === 'fill') style = { ...base, tool: 'polyfill', color: color.value, opacity: fx.fillOpacity / 100, flags: 0 }
  else if (kind === 'delete') style = { ...base, tool: 'polyfill', color: '#000000', opacity: 1, flags: FLAG_ERASE }
  else {
    // 以选区外接矩形的中线翻转；端点取整，像素一一对应
    const b = polygonBounds(sel)
    const ex = Math.floor(b.x0) + Math.ceil(b.x1), ey = Math.floor(b.y0) + Math.ceil(b.y1)
    const matrix = kind === 'flipH' ? [-1, 0, 0, 1, ex, 0] : [1, 0, 0, -1, 0, ey]
    style = { ...base, tool: 'xform', color: '#000000', opacity: 1, flags: 0, matrix }
    flipped = sel.map((v, i) => (kind === 'flipH' ? (i % 2 ? v : ex - v) : (i % 2 ? ey - v : v)))
  }
  if (kind === 'fill') rememberColor(color.value)
  engine.value?.mineBegin(key, layer.id, style)
  engine.value?.mineUpdate(key, { points: pts })
  const ok = await sendOp(key, layer.id, style, pts)
  if (ok && flipped && selection.value === sel) selection.value = flipped
}

// ----- 导入图片：放进一个新图层，接着进入自由变换调整大小和位置 -----
const importInput = ref<HTMLInputElement | null>(null)
let importing = false
/** 新建一个图层并等它成为当前图层；建不了（满了）返回 null */
async function addLayerAndWait(name: string): Promise<LayerMeta | null> {
  if (layers.value.length >= 16) return null
  const before = currentLayer.value
  await layerAct('add', currentLayer.value, name)
  for (let i = 0; i < 60 && currentLayer.value === before; i++) await new Promise(r => window.setTimeout(r, 50))
  return currentLayer.value !== before ? selected.value : null
}
async function onImport(ev: Event) {
  const input = ev.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file || importing) return
  if (!file.type.startsWith('image/')) return say('只能导入图片', 'error')
  if (file.size > 40 * 1024 * 1024) return say('图片太大了（超过 40MB）', 'error')
  const e = engine.value
  if (!e) return
  cancelTransform()
  importing = true
  try {
    const bmp = await createImageBitmap(file).catch(() => null)
    if (!bmp) return say('这张图片读不出来，换一张试试', 'error')
    const W = props.room.width, H = props.room.height
    let scale = Math.min(1, W / bmp.width, H / bmp.height)
    // 编成 PNG 超过 15MB 时再缩小一点（服务器单张上限 16MB）
    let c!: HTMLCanvasElement
    let blob: Blob | null = null
    for (;;) {
      c = document.createElement('canvas')
      c.width = Math.max(1, Math.round(bmp.width * scale)); c.height = Math.max(1, Math.round(bmp.height * scale))
      const cx = c.getContext('2d')!
      cx.imageSmoothingQuality = 'high'
      cx.drawImage(bmp, 0, 0, c.width, c.height)
      blob = await new Promise<Blob | null>(r => c.toBlob(r, 'image/png'))
      if (!blob || blob.size <= 15 * 1024 * 1024 || scale < 0.2) break
      scale *= 0.75
    }
    bmp.close()
    if (!blob) return say('图片处理失败', 'error')
    const name = (file.name.replace(/\.[^.]+$/, '') || '导入的图片').slice(0, 20)
    const layer = (await addLayerAndWait(name)) ?? drawableLayer()
    if (!layer) return
    const x = Math.round((W - c.width) / 2), y = Math.round((H - c.height) / 2)
    const tmp = `local-${newKey()}`
    registerPatch(tmp, c)
    const key = newKey()
    const style: StrokeStyle = { tool: 'patch', color: '#000000', size: 1, opacity: 1, brush: 'pen', hardness: 1, flags: 0, patch: tmp }
    const pts = [x * 10, y * 10, 100]
    e.mineBegin(key, layer.id, style)
    e.mineUpdate(key, { points: pts })
    say('正在上传图片…')
    try {
      const hash = await uploadPatch(props.room.roomId, c, blob)
      const ok = await sendOp(key, layer.id, { ...style, patch: hash }, pts)
      if (ok) {
        selection.value = rectPoly(x, y, x + c.width, y + c.height)
        startTransform()
        say('图片已放进新图层：拖角缩放、拖框外旋转，按 Enter 确定')
      }
    } catch (err: any) {
      e.mineDone(key, null)
      say(err?.response?.data?.message || err?.friendlyMessage || '图片没有上传成功，再试一次', 'error')
    } finally { forgetPatch(tmp) }
  } finally { importing = false }
}

// ----- 油漆桶 -----
let filling = false
async function doFill(x: number, y: number, layer: LayerMeta) {
  const e = engine.value
  if (!e) return
  if (filling) return say('上一次填充还在上传，稍等一下')
  const W = props.room.width, H = props.room.height
  const sx = Math.floor(x), sy = Math.floor(y)
  if (sx < 0 || sy < 0 || sx >= W || sy >= H) return
  if (selection.value && !pointInPolygon(selection.value, x, y)) return say('点在选区外面了：有选区时油漆桶只填选区里面', 'error')
  const src = fx.fillSample === 'layer' ? e.layerCanvas(layer.id) : e.compose('')
  if (!src) return
  const data = src.getContext('2d')!.getImageData(0, 0, W, H).data
  const r = floodFill(data, W, H, sx, sy, Math.round(fx.fillTolerance * 2.55), fx.fillExpand)
  if (!r) return
  // 填充结果做成一张图（只有外接矩形那么大）
  const c = document.createElement('canvas')
  c.width = r.w; c.height = r.h
  const cx = c.getContext('2d')!
  const img = cx.createImageData(r.w, r.h)
  const n = parseInt(color.value.slice(1), 16)
  const R = (n >> 16) & 255, G = (n >> 8) & 255, B = n & 255
  for (let yy = 0; yy < r.h; yy++) {
    const row = (yy + r.y) * W + r.x
    for (let xx = 0; xx < r.w; xx++) {
      if (!r.mask[row + xx]) continue
      const q = (yy * r.w + xx) * 4
      img.data[q] = R; img.data[q + 1] = G; img.data[q + 2] = B; img.data[q + 3] = 255
    }
  }
  cx.putImageData(img, 0, 0)
  // 先用本地的图预览，上传完换成服务器的哈希
  const tmp = `local-${newKey()}`
  registerPatch(tmp, c)
  const key = newKey()
  const style: StrokeStyle = { tool: 'patch', color: color.value, size: 1, opacity: fx.fillOpacity / 100, brush: 'pen', hardness: 1, flags: 0, clip: clipOf(), patch: tmp }
  const pts = [r.x * 10, r.y * 10, 100]
  e.mineBegin(key, layer.id, style)
  e.mineUpdate(key, { points: pts })
  rememberColor(color.value)
  filling = true
  try {
    const hash = await uploadPatch(props.room.roomId, c)
    await sendOp(key, layer.id, { ...style, patch: hash }, pts)
  } catch (err: any) {
    e.mineDone(key, null)
    say(err?.response?.data?.message || err?.friendlyMessage || '填充没有上传成功，再试一次', 'error')
  } finally {
    filling = false
    forgetPatch(tmp)
  }
}

function rememberColor(c: string) {
  recent.value = [c, ...recent.value.filter(x => x !== c)].slice(0, 8)
}

// ================= 快捷键 =================
function isTyping(e: KeyboardEvent) {
  const t = e.target as HTMLElement | null
  return !!t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.isContentEditable)
}
function onKeyDown(e: KeyboardEvent) {
  if (isTyping(e)) return
  const k = e.key.toLowerCase()
  if ((e.ctrlKey || e.metaKey) && k === 'z') { e.preventDefault(); void (e.shiftKey ? redo() : undo()); return }
  if ((e.ctrlKey || e.metaKey) && k === 'y') { e.preventDefault(); void redo(); return }
  if ((e.ctrlKey || e.metaKey) && k === 'a') { e.preventDefault(); selectAll(); return }
  if ((e.ctrlKey || e.metaKey) && k === 'd') { e.preventDefault(); deselect(); return }
  if (e.ctrlKey || e.metaKey || e.altKey) return
  if (e.code === 'Space') { e.preventDefault(); spaceDown.value = true; return }
  if (e.key === 'Escape') { if (xf.value) cancelTransform(); else if (drag) cancelDrag(); else deselect(); return }
  if (e.key === 'Enter' && xf.value) { e.preventDefault(); void applyTransform(); return }
  if (k === 't') { startTransform(); return }
  if ((e.key === 'Delete' || e.key === 'Backspace') && selection.value && !xf.value) { e.preventDefault(); void selectionOp('delete'); return }
  const pixelTool = tool.value === 'blur' || tool.value === 'smudge'
  if (HOTKEYS[k]) tool.value = HOTKEYS[k]!
  else if (k === '[') {
    if (pixelTool) fxSize.value = Math.max(4, Math.round(fxSize.value / 1.2))
    else size.value = Math.max(1, Math.round(size.value / 1.2))
  } else if (k === ']') {
    if (pixelTool) fxSize.value = Math.min(fxMaxSize.value, Math.round(fxSize.value * 1.2) + 1)
    else size.value = Math.min(currentBrush.value.maxSize, Math.round(size.value * 1.2) + (size.value < 5 ? 1 : 0))
  }
}
function onKeyUp(e: KeyboardEvent) { if (e.code === 'Space') spaceDown.value = false }

// ================= 导出 / 发到画廊 =================
function composeBlob(): Promise<Blob | null> {
  const c = engine.value?.compose()
  return new Promise(resolve => (c ? c.toBlob(b => resolve(b), 'image/png') : resolve(null)))
}
async function exportPng() {
  const blob = await composeBlob()
  if (!blob) return
  const a = document.createElement('a')
  a.href = URL.createObjectURL(blob)
  a.download = `${props.room.name}.png`
  a.click()
  window.setTimeout(() => URL.revokeObjectURL(a.href), 2000)
}
async function openPublish() {
  const blob = await composeBlob()
  if (!blob) return
  // 画廊单张限制 10MB，PNG 太大就换成 JPG
  let file = new File([blob], `${props.room.name}.png`, { type: 'image/png' })
  if (blob.size > 9.5 * 1024 * 1024) {
    const jpg = await new Promise<Blob | null>(r => engine.value!.compose().toBlob(b => r(b), 'image/jpeg', 0.92))
    if (jpg) file = new File([jpg], `${props.room.name}.jpg`, { type: 'image/jpeg' })
  }
  publishFiles.value = [file]
  publishDesc.value = `茶绘合作作品 · 参与画师：${members.value.map(m => m.name).join('、')}`
  publishOpen.value = true
}
function onPublished() { say('已经发到画廊了！') }

// ================= 生命周期 =================
let clockTimer = 0
onMounted(() => {
  void init()
  window.addEventListener('keydown', onKeyDown)
  window.addEventListener('keyup', onKeyUp)
  window.addEventListener('resize', fit)
  document.addEventListener('fullscreenchange', onFullscreenChange)
  void nextTick(drawPreviews)
  void loadMine()
  clockTimer = window.setInterval(() => { now.value = Date.now() }, 1000)
})
onBeforeUnmount(() => {
  for (const [name, fn] of handlers) props.connection.off(name, fn)
  window.removeEventListener('keydown', onKeyDown)
  window.removeEventListener('keyup', onKeyUp)
  window.removeEventListener('resize', fit)
  document.removeEventListener('fullscreenchange', onFullscreenChange)
  offTip()
  offPatch()
  if (document.fullscreenElement === rootEl.value) void document.exitFullscreen().catch(() => undefined)
  window.clearInterval(flushTimer); window.clearInterval(clockTimer); window.clearTimeout(noticeTimer)
  engine.value?.destroy()
})
</script>

<style scoped>
.studio { display: flex; flex-direction: column; gap: 10px; height: max(560px, calc(100vh - 150px)); min-height: 0; color: var(--text-primary); }
.studio > .studio-bar, .studio > .studio-notice { flex: none; }
.studio > .studio-body { flex: 1 1 0; }
/* 专注模式：盖住网站导航，铺满整个窗口 */
.studio.immersive { position: fixed; inset: 0; z-index: 2000; height: 100%; padding: 10px; background: var(--bg-main); }
.studio.is-fullscreen { height: 100%; padding: 10px; background: var(--bg-main); }
.eyebrow, .block-tag { margin: 0; font: 10px/1.2 var(--font-mono, monospace); letter-spacing: .16em; color: var(--accent-color); }

/* 顶栏 */
.studio-bar { display: flex; flex-wrap: wrap; align-items: center; gap: 10px 14px; padding: 8px 12px; border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); background: var(--paper-card); }
.room-title { display: grid; gap: 2px; min-width: 0; }
.room-title b { font-size: 16px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 22ch; }
.room-code { display: inline-grid; grid-template-columns: auto auto; column-gap: 8px; align-items: baseline; padding: 4px 10px; border: 1px solid var(--border-line); background: var(--bg-main); color: var(--text-primary); font: 600 14px var(--font-mono, monospace); letter-spacing: .1em; cursor: pointer; }
.room-code small { grid-column: 1 / -1; font: 10px var(--font-sans, sans-serif); letter-spacing: 0; color: var(--text-secondary); text-align: left; }
.room-code span { font: 11px var(--font-sans, sans-serif); letter-spacing: 0; color: var(--accent-color); }
.members { display: flex; flex-wrap: wrap; gap: 6px; margin: 0; padding: 0; list-style: none; }
.members li { position: relative; display: inline-flex; align-items: center; gap: 6px; padding: 4px 10px; border: 1px solid var(--border-line); font-size: 13px; background: var(--bg-main); }
.members li i { width: 9px; height: 9px; border-radius: 50%; }
.members li em { padding: 0 4px; background: var(--accent-color); color: var(--on-accent, #fff); font: 600 10px var(--font-sans, sans-serif); font-style: normal; }
.members li.off { opacity: .5; }
.members li.empty { color: var(--text-secondary); border-style: dashed; font-size: 12px; }
.members .kick { border: 0; background: none; color: var(--text-secondary); cursor: pointer; padding: 0 0 0 2px; font-size: 14px; line-height: 1; }
.members .kick:hover { color: var(--danger); }
.bar-right { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; margin-left: auto; }
.bar-btn { padding: 6px 12px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.bar-btn.primary { border-color: var(--accent-color); background: var(--accent-color); color: var(--on-accent, #fff); font-weight: 600; }
.bar-btn.on { border-color: var(--accent-color); color: var(--accent-color); }
.view-btns { display: inline-flex; gap: 6px; padding-inline: 6px; border-inline: 1px dashed var(--border-line); }
.zoom { display: inline-flex; border: 1px solid var(--border-line); }
.zoom button { padding: 6px 10px; border: 0; background: transparent; color: var(--text-primary); font: inherit; font-size: 13px; cursor: pointer; }
.zoom .zoom-val { min-width: 4.5em; border-inline: 1px solid var(--border-line); font-family: var(--font-mono, monospace); }
.studio-notice { margin: 0; padding: 8px 12px; border-left: 3px solid var(--accent-color); background: color-mix(in srgb, var(--accent-color) 8%, var(--paper-card)); font-size: 13px; }
.studio-notice.error { border-left-color: var(--danger); background: color-mix(in srgb, var(--danger) 8%, var(--paper-card)); }

/* 主体 */
.studio-body { display: grid; grid-template-columns: 64px minmax(0, 1fr) 300px; grid-template-rows: minmax(0, 1fr); gap: 10px; min-height: 0; }
.studio-body.no-panel { grid-template-columns: 64px minmax(0, 1fr); }
.tools { display: flex; flex-direction: column; gap: 2px; padding: 5px; overflow-y: auto; scrollbar-width: thin; border: 1px solid var(--border-line); background: var(--paper-card); }
.tools button { flex: none; display: grid; justify-items: center; gap: 2px; padding: 5px 0 4px; border: 1px solid transparent; background: transparent; color: var(--text-secondary); cursor: pointer; font: inherit; }
.tools button:hover { color: var(--text-primary); }
.tools button.gap { margin-top: 5px; position: relative; }
.tools button.gap::before { content: ''; position: absolute; left: 8px; right: 8px; top: -4px; border-top: 1px dashed var(--border-line); }
.tools button.on { border-color: var(--accent-color); background: color-mix(in srgb, var(--accent-color) 10%, transparent); color: var(--accent-color); }
.tool-icon { width: 20px; height: 20px; fill: none; stroke: currentColor; stroke-width: 1.6; stroke-linecap: round; stroke-linejoin: round; }
.tools small { font-size: 10.5px; line-height: 1.2; white-space: nowrap; }
.tool-color { width: 34px; height: 34px; margin: 8px auto 0; border: 2px solid var(--paper-card); box-shadow: 0 0 0 1px var(--border-line); }

.view { position: relative; overflow: hidden; min-height: 320px; border: 1px solid var(--border-line); touch-action: none; user-select: none;
  background-color: #d9dde0; background-image: linear-gradient(45deg, #cfd4d8 25%, transparent 25%, transparent 75%, #cfd4d8 75%), linear-gradient(45deg, #cfd4d8 25%, transparent 25%, transparent 75%, #cfd4d8 75%); background-size: 24px 24px; background-position: 0 0, 12px 12px; }
:root[data-theme='dark'] .view { background-color: #2a3036; background-image: linear-gradient(45deg, #252a30 25%, transparent 25%, transparent 75%, #252a30 75%), linear-gradient(45deg, #252a30 25%, transparent 25%, transparent 75%, #252a30 75%); }
.stage { position: absolute; left: 0; top: 0; transform-origin: 0 0; background: #fff; box-shadow: 0 6px 24px rgb(0 0 0 / 22%); }
.stage :deep(canvas) { position: absolute; inset: 0; width: 100%; height: 100%; pointer-events: none; }
.view-loading { position: absolute; inset: 0; display: grid; place-items: center; margin: 0; color: var(--text-secondary); font-size: 14px; }
.brush-ring { position: absolute; left: 0; top: 0; border: 1px solid rgb(0 0 0 / 70%); border-radius: 50%; box-shadow: 0 0 0 1px rgb(255 255 255 / 75%); pointer-events: none; }
.peer-cursor { position: absolute; left: 0; top: 0; pointer-events: none; transition: transform .08s linear; }
.peer-cursor i { position: absolute; left: -5px; top: -5px; width: 10px; height: 10px; border-radius: 50%; background: var(--peer); box-shadow: 0 0 0 2px #fff; }
.peer-cursor span { position: absolute; left: 9px; top: 6px; padding: 1px 6px; background: var(--peer); color: #fff; font-size: 11px; white-space: nowrap; }
.is-panning .brush-ring { display: none; }
.sel-overlay { position: absolute; inset: 0; width: 100%; height: 100%; overflow: visible; pointer-events: none; }
.ants { fill: none; stroke-width: 1; }
.ants-bg { stroke: #fff; }
.ants-fg { stroke: #111; stroke-dasharray: 5 4; animation: ants .7s linear infinite; fill: rgb(45 212 191 / 7%); }
@keyframes ants { to { stroke-dashoffset: -9; } }
@media (prefers-reduced-motion: reduce) { .ants-fg { animation: none; } }
.grad-line line { stroke: #fff; stroke-width: 1.5; filter: drop-shadow(0 0 1px rgb(0 0 0 / 80%)); }
.grad-line circle { fill: #fff; stroke: #111; stroke-width: 1; }
.sel-bar { position: absolute; top: 8px; left: 50%; transform: translateX(-50%); display: flex; align-items: center; gap: 2px; padding: 3px; border: 1px solid var(--border-line); background: var(--paper-card); box-shadow: 0 4px 14px rgb(0 0 0 / 18%); font-size: 12px; white-space: nowrap; }
.sel-bar b { padding: 0 8px 0 6px; font: 10px var(--font-mono, monospace); letter-spacing: .12em; color: var(--accent-color); }
.sel-bar button { padding: 4px 8px; border: 0; background: transparent; color: var(--text-primary); font: inherit; cursor: pointer; }
.sel-bar button:hover { background: color-mix(in srgb, var(--accent-color) 12%, transparent); }
.sel-bar button.danger { color: var(--danger); }
.sel-bar button.ok { color: var(--accent-color); font-weight: 600; }
.xf-tip { padding: 0 6px; color: var(--text-secondary); font-size: 11px; }
.xf-frame { fill: none; stroke: var(--accent-color, #0e7490); stroke-width: 1.2; }
.xf-handle { fill: #fff; stroke: var(--accent-color, #0e7490); stroke-width: 1.4; }
.xf-rot { fill: var(--accent-color, #0e7490); stroke: #fff; stroke-width: 1.5; }
.view-hint { position: absolute; left: 8px; bottom: 6px; margin: 0; padding: 2px 8px; background: rgb(0 0 0 / 45%); color: #fff; font-size: 11px; pointer-events: none; }

/* 右侧 */
.panel { display: flex; flex-direction: column; gap: 10px; min-height: 0; overflow-y: auto; }
.block { flex-shrink: 0; display: grid; align-content: start; gap: 8px; min-height: 0; padding: 10px 12px; border: 1px solid var(--border-line); background: var(--paper-card); }
.block-head { display: flex; justify-content: space-between; align-items: center; }
.color-row { display: flex; flex-wrap: wrap; gap: 5px; align-items: center; }
.link { border: 0; background: none; padding: 0; color: var(--text-secondary); font: inherit; font-size: 11px; cursor: pointer; text-decoration: underline dotted; }
.brush-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 4px; }
.brush-btn { display: grid; gap: 2px; padding: 3px 3px 4px; border: 1px solid var(--border-line); background: var(--bg-main); color: var(--text-secondary); font: inherit; font-size: 11px; cursor: pointer; }
.brush-btn canvas { width: 100%; height: auto; aspect-ratio: 160 / 44; background: #fff; }
.brush-btn.on { border-color: var(--accent-color); color: var(--accent-color); box-shadow: inset 0 -2px 0 var(--accent-color); font-weight: 600; }
.brush-desc { margin: 0; font-size: 11px; color: var(--text-secondary); line-height: 1.5; }
.my-brushes { display: grid; gap: 4px; padding-top: 6px; border-top: 1px dashed var(--border-line); }
.sub-head { display: flex; justify-content: space-between; align-items: center; }
.sub-tag { font: 10px var(--font-mono, monospace); letter-spacing: .1em; color: var(--text-secondary); }
.sub-actions { display: inline-flex; gap: 10px; }
.sub-actions .link { color: var(--accent-color); text-decoration: none; font-weight: 600; }
.brush-btn.custom { position: relative; text-align: center; }
.brush-btn .edit { position: absolute; top: 2px; right: 2px; width: 18px; height: 18px; padding: 0; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); font-size: 11px; line-height: 1; cursor: pointer; }
.brush-btn .fav-mark { position: absolute; top: 3px; right: 4px; font-style: normal; font-size: 10px; color: #e11d48; }
.lib-mask { position: fixed; inset: 0; z-index: 4050; display: grid; place-items: center; padding: 16px; background: rgb(8 12 16 / 70%); }
.lib { display: grid; gap: 12px; width: min(1100px, 100%); max-height: 100%; overflow-y: auto; padding: 16px 18px; border: 1px solid var(--border-line); border-top: 3px solid var(--accent-color); background: var(--paper-card); }
.lib-head { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; }
.lib-head b { font-size: 15px; }
.checks { display: flex; flex-wrap: wrap; gap: 4px 12px; font-size: 12px; color: var(--text-secondary); }
.checks label { display: inline-flex; align-items: center; gap: 4px; cursor: pointer; }
.checks label.off { opacity: .45; }
.checks input { accent-color: var(--accent-color); }
.color-row.recent small { font-size: 11px; color: var(--text-secondary); margin-right: 2px; }
.color-input { position: relative; width: 30px; height: 30px; cursor: pointer; }
.color-input input { position: absolute; inset: 0; width: 100%; height: 100%; padding: 0; border: 0; opacity: 0; cursor: pointer; }
.color-input span { display: block; width: 100%; height: 100%; border: 2px solid var(--paper-card); box-shadow: 0 0 0 1px var(--text-primary); }
.swatch { width: 20px; height: 20px; padding: 0; border: 1px solid rgb(0 0 0 / 20%); cursor: pointer; }
.swatch.sm { width: 16px; height: 16px; }
.swatch.on { outline: 2px solid var(--accent-color); outline-offset: 1px; }
.slider { display: grid; grid-template-columns: 4.2em 1fr 3em; align-items: center; gap: 8px; font-size: 12px; color: var(--text-secondary); }
.slider input { width: 100%; accent-color: var(--accent-color); }
.slider.wide { grid-template-columns: auto 1fr 3em; }
.slider b { font: 600 12px var(--font-mono, monospace); color: var(--text-primary); text-align: right; }
.mini { padding: 3px 9px; border: 1px solid var(--border-line); background: transparent; color: var(--text-primary); font: inherit; font-size: 12px; cursor: pointer; }
.mini:disabled { opacity: .45; cursor: not-allowed; }
.mini.danger { color: var(--danger); }
.seg { display: flex; border: 1px solid var(--border-line); }
.seg button { flex: 1; padding: 5px 4px; border: 0; background: transparent; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; }
.seg button + button { border-left: 1px solid var(--border-line); }
.seg button.on { background: color-mix(in srgb, var(--accent-color) 12%, transparent); color: var(--accent-color); font-weight: 600; }
.row-label { font-size: 11px; color: var(--text-secondary); margin-right: 2px; }

.layers ol { list-style: none; margin: 0; padding: 0; overflow: auto; display: grid; align-content: start; gap: 3px; min-height: 60px; max-height: 250px; }
.layers li { display: flex; align-items: center; gap: 8px; padding: 6px 8px; border: 1px solid var(--border-line); background: var(--bg-main); cursor: pointer; }
.layers li.on { border-color: var(--accent-color); box-shadow: inset 3px 0 0 var(--accent-color); }
.layers li.hidden .layer-name { opacity: .45; }
.eye { border: 0; background: none; color: var(--text-secondary); cursor: pointer; font-size: 12px; width: 18px; }
.layer-name { display: grid; min-width: 0; flex: 1; }
.layer-name b { font-size: 13px; font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.layer-name small { font-size: 11px; color: var(--text-secondary); }
.lock { font-size: 12px; }
.layer-ops { display: grid; gap: 6px; padding-top: 6px; border-top: 1px dashed var(--border-line); }
.op-row { display: flex; flex-wrap: wrap; gap: 4px; }
.hint { margin: 0; font-size: 11px; color: var(--text-secondary); line-height: 1.5; }

.chat { flex: 1 1 200px; grid-template-rows: auto minmax(0, 1fr) auto; }
.chat ol { list-style: none; margin: 0; padding: 0; overflow: auto; display: grid; align-content: start; gap: 4px; font-size: 13px; line-height: 1.5; min-height: 60px; }
.chat li b { margin-right: 6px; }
.chat li.muted { color: var(--text-secondary); font-size: 12px; }
.chat form { display: flex; gap: 6px; }
.chat input { flex: 1; min-width: 0; padding: 6px 8px; border: 1px solid var(--border-line); background: var(--bg-main); color: var(--text-primary); font: inherit; font-size: 13px; }

@media (max-width: 1100px) {
  .studio-body { grid-template-columns: 56px minmax(0, 1fr) 260px; }
  .studio-body.no-panel { grid-template-columns: 56px minmax(0, 1fr); }
}
@media (max-width: 860px) {
  .studio { height: auto; }
  .studio > .studio-body { flex: none; }
  .studio.immersive { overflow-y: auto; }
  .view-btns { border: 0; padding: 0; }
  .studio-body { grid-template-columns: 1fr; grid-template-rows: auto; }
  .studio-body.no-panel { grid-template-columns: 1fr; }
  .tools { flex-direction: row; flex-wrap: wrap; justify-content: center; }
  .tools button { padding: 6px 10px; }
  .tools button.gap { margin-top: 0; }
  .tools button.gap::before { display: none; }
  .sel-bar { top: auto; bottom: 8px; max-width: calc(100% - 16px); overflow-x: auto; }
  .xf-tip { display: none; }
  .tool-color { margin: 0 0 0 8px; align-self: center; }
  .view { height: 62vh; }
  .panel { grid-template-rows: auto; }
  .panel { overflow: visible; }
  .chat { flex-basis: auto; }
  .chat ol { max-height: 180px; }
  .view-hint { display: none; }
}
</style>
