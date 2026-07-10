import { saveFileContent, getFileContent, insertImage, postMessage } from './api.js';

let currentFile = null;
let currentContent = '';
let originalContent = '';
let isEditMode = false;

let fileNameEl, contentDiv, statusEl, editBtn, saveBtn, cancelBtn;

// ========== 自动补全相关 ==========
let mdFileList = [];
let autocompleteActive = false;
let autocompletePopup = null;

// ========== 命令面板相关 ==========
let commandActive = false;
let commandPopup = null;
const commandList = [
    { id: 'insert-image', label: '🖼️ 插入图片', action: handleInsertImage }
];
let commandFiltered = [];
let commandSelectedIndex = -1;

// ========== 初始化弹窗 ==========
function initPopups() {
    if (!autocompletePopup) {
        const popup = document.createElement('div');
        popup.id = 'autocomplete-popup';
        popup.style.cssText = `
            display: none;
            position: absolute;
            background: #2d2d2d;
            border: 1px solid #3e3e3e;
            border-radius: 4px;
            max-height: 200px;
            overflow-y: auto;
            z-index: 1000;
            min-width: 200px;
            box-shadow: 0 4px 8px rgba(0,0,0,0.5);
            color: #d4d4d4;
            padding: 4px 0;
        `;
        document.body.appendChild(popup);
        autocompletePopup = popup;
    }
    if (!commandPopup) {
        const popup = document.createElement('div');
        popup.id = 'command-popup';
        popup.style.cssText = `
            display: none;
            position: absolute;
            background: #2d2d2d;
            border: 1px solid #3e3e3e;
            border-radius: 4px;
            max-height: 200px;
            overflow-y: auto;
            z-index: 1000;
            min-width: 200px;
            box-shadow: 0 4px 8px rgba(0,0,0,0.5);
            color: #d4d4d4;
            padding: 4px 0;
        `;
        document.body.appendChild(popup);
        commandPopup = popup;
    }
    document.addEventListener('click', function(e) {
        if (autocompletePopup && !autocompletePopup.contains(e.target) && e.target !== document.activeElement) {
            closeAutocomplete();
        }
        if (commandPopup && !commandPopup.contains(e.target) && e.target !== document.activeElement) {
            closeCommand();
        }
    });
}

// ========== 自动补全（[[） ==========
export function setMdFileList(files) {
    mdFileList = (files || [])
        .filter(f => {
            const path = f.Path || f.path || '';
            return path.endsWith('.md');
        })
        .map(f => {
            const path = f.Path || f.path;
            const name = path.replace(/^.*[\\/]/, '').replace('.md', '');
            return { path, name, display: path };
        });
}

function isAtTrigger(textarea, pos) {
    const before = textarea.value.substring(0, pos);
    if (before.length < 2) return false;
    const lastTwo = before.slice(-2);
    if (lastTwo !== '[[') return false;
    const after = textarea.value.substring(pos);
    if (after.startsWith(']]')) return false;
    return true;
}

function getSearchKeyword(textarea, pos) {
    const before = textarea.value.substring(0, pos);
    const match = before.match(/\[\[([^\]]*)$/);
    return match ? match[1] : '';
}

function showAutocomplete(keyword, textarea) {
    const filtered = mdFileList.filter(item =>
        item.name.toLowerCase().includes(keyword.toLowerCase()) ||
        item.path.toLowerCase().includes(keyword.toLowerCase())
    );
    if (filtered.length === 0) {
        closeAutocomplete();
        return;
    }

    const popup = autocompletePopup;
    const rect = textarea.getBoundingClientRect();
    const cursorPos = getCaretCoordinates(textarea, textarea.selectionStart);
    popup.style.left = (rect.left + cursorPos.left) + 'px';
    popup.style.top = (rect.top + cursorPos.top + 20) + 'px';

    let html = '<ul style="list-style:none;padding:0;margin:0;">';
    filtered.forEach(item => {
        html += `<li data-path="${item.path}" style="padding:4px 12px;cursor:pointer;transition:background 0.1s;"
                    onmouseover="this.style.background='#3a3a3a'"
                    onmouseout="this.style.background='transparent'">${item.display}</li>`;
    });
    html += '</ul>';
    popup.innerHTML = html;
    popup.style.display = 'block';
    autocompleteActive = true;

    popup.querySelectorAll('li').forEach(li => {
        li.addEventListener('click', function() {
            insertLink(textarea, this.dataset.path);
            closeAutocomplete();
        });
    });
}

function insertLink(textarea, path) {
    const start = textarea.selectionStart;
    const before = textarea.value.substring(0, start);
    const after = textarea.value.substring(start);
    const lastOpen = before.lastIndexOf('[[');
    if (lastOpen === -1) return;
    const newText = before.substring(0, lastOpen) + '[[' + path + ']]' + after;
    textarea.value = newText;
    const newPos = lastOpen + path.length + 4;
    textarea.selectionStart = textarea.selectionEnd = newPos;
    textarea.dispatchEvent(new Event('input'));
}

function closeAutocomplete() {
    if (autocompletePopup) {
        autocompletePopup.style.display = 'none';
        autocompletePopup.innerHTML = '';
    }
    autocompleteActive = false;
}

// ========== 命令面板 ==========
function isAtCommand(textarea, pos) {
    const before = textarea.value.substring(0, pos);
    if (before.length === 0) return false;
    return before[before.length - 1] === '/';
}

function showCommand(textarea) {
    commandFiltered = commandList;
    const popup = commandPopup;
    const rect = textarea.getBoundingClientRect();
    const cursorPos = getCaretCoordinates(textarea, textarea.selectionStart);
    popup.style.left = (rect.left + cursorPos.left) + 'px';
    popup.style.top = (rect.top + cursorPos.top + 20) + 'px';

    let html = '<ul style="list-style:none;padding:0;margin:0;">';
    commandFiltered.forEach((cmd, index) => {
        html += `<li data-index="${index}" style="padding:4px 12px;cursor:pointer;transition:background 0.1s;"
                    onmouseover="this.style.background='#3a3a3a'"
                    onmouseout="this.style.background='transparent'">${cmd.label}</li>`;
    });
    html += '</ul>';
    popup.innerHTML = html;
    popup.style.display = 'block';
    commandActive = true;

    popup.querySelectorAll('li').forEach(li => {
        li.addEventListener('click', function() {
            const idx = parseInt(this.dataset.index);
            const cmd = commandFiltered[idx];
            if (cmd) {
                closeCommand();
                const textarea = document.getElementById('editArea');
                if (textarea) {
                    const start = textarea.selectionStart;
                    const before = textarea.value.substring(0, start - 1);
                    const after = textarea.value.substring(start);
                    textarea.value = before + after;
                    textarea.selectionStart = textarea.selectionEnd = start - 1;
                    textarea.dispatchEvent(new Event('input'));
                }
                cmd.action();
            }
        });
    });
}

function closeCommand() {
    if (commandPopup) {
        commandPopup.style.display = 'none';
        commandPopup.innerHTML = '';
    }
    commandActive = false;
}

// ========== 插入图片 ==========
function handleInsertImage() {
    if (!currentFile) {
        alert('请先打开一个文档再插入图片');
        return;
    }
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = 'image/*';
    input.onchange = function(e) {
        const file = e.target.files[0];
        if (!file) return;
        const reader = new FileReader();
        reader.onload = function(ev) {
            const base64 = ev.target.result.split(',')[1];
            insertImage(currentFile, file.name, base64);
            statusEl.textContent = '正在上传图片...';
        };
        reader.readAsDataURL(file);
    };
    input.click();
}

// ========== 编辑器核心 ==========
export function initEditor(fileNameId, contentId, statusId, editBtnId, saveBtnId, cancelBtnId) {
    fileNameEl = document.getElementById(fileNameId);
    contentDiv = document.getElementById(contentId);
    statusEl = document.getElementById(statusId);
    editBtn = document.getElementById(editBtnId);
    saveBtn = document.getElementById(saveBtnId);
    cancelBtn = document.getElementById(cancelBtnId);

    if (editBtn) editBtn.addEventListener('click', toggleEditMode);
    if (saveBtn) saveBtn.addEventListener('click', saveContent);
    if (cancelBtn) cancelBtn.addEventListener('click', cancelEdit);

    initPopups();
}

export function displayFileContent(content, path) {
    currentFile = path;
    originalContent = content;
    currentContent = content;
    fileNameEl.textContent = path || '请选择文件';

    if (isEditMode) {
        const textarea = document.getElementById('editArea');
        if (textarea) {
            textarea.value = content || '';
        }
        return;
    }

    if (!content) {
        contentDiv.innerHTML = '<div class="placeholder">📄 文件为空</div>';
        return;
    }

    const ext = path ? path.split('.').pop().toLowerCase() : '';
    if (ext === 'md') {
        try {
            if (typeof marked === 'undefined') {
                console.error('[displayFileContent] marked 库未加载！');
                contentDiv.innerHTML = `<pre>${content}</pre>`;
                return;
            }

            // 创建自定义渲染器
            const renderer = new marked.Renderer();

            // 重写 image 方法
            renderer.image = function(...args) {
    let href, title, text;
    // 判断第一个参数类型：对象（v15+）还是字符串（旧版）
    if (args[0] && typeof args[0] === 'object') {
        const token = args[0];
        href = token.href || '';
        title = token.title || '';
        text = token.text || '';
    } else {
        // 旧版签名 (href, title, text)
        href = args[0] || '';
        title = args[1] || '';
        text = args[2] || '';
    }

    if (typeof href !== 'string' || href === '') {
        return `<img src="" alt="${text || ''}" />`;
    }

    let finalHref = href;
    if (!href.startsWith('http://') && !href.startsWith('https://')) {
        // 去掉开头的 ./ 或 /
        let cleanHref = href.replace(/^\.?\//, '');
        let currentDir = currentFile ? currentFile.substring(0, currentFile.lastIndexOf('/') + 1) : '';
        finalHref = 'https://vault.local/' + currentDir + cleanHref;
    }

    // 加时间戳避免缓存
    const cacheBuster = `?t=${Date.now()}`;
    return `<img src="${finalHref}${cacheBuster}" alt="${text || ''}" ${title ? `title="${title}"` : ''} />`;
};

            // 使用自定义渲染器解析
            let html = marked.parse(content, { renderer });

            // 处理 [[...]] 链接
            html = html.replace(/\[\[([^\]]+)\]\]/g, (match, p1) => {
                let parts = p1.split('|');
                let linkPath = parts[0].trim();
                let displayName = parts.length > 1 ? parts[1].trim() : linkPath;
                if (!linkPath.endsWith('.md')) linkPath += '.md';
                return `<span class="wikilink" data-path="${linkPath}">${displayName}</span>`;
            });

            contentDiv.innerHTML = html;

            // 绑定 wikilink 点击事件
            contentDiv.querySelectorAll('.wikilink').forEach(el => {
                el.addEventListener('click', function() {
                    postMessage('OPEN_LINK', { path: this.dataset.path });
                });
            });

        } catch (e) {
            console.error('[displayFileContent] Markdown 渲染失败:', e);
            contentDiv.innerHTML = `<pre>${content}</pre>`;
        }
    } else {
        contentDiv.innerHTML = `<pre style="white-space:pre-wrap;">${content}</pre>`;
    }
}

function toggleEditMode() {
    if (!currentFile) {
        alert('请先选择一个文件');
        return;
    }
    if (isEditMode) {
        exitEditMode(false);
    } else {
        enterEditMode();
    }
}

function enterEditMode() {
    isEditMode = true;
    const textarea = document.createElement('textarea');
    textarea.className = 'edit-textarea';
    textarea.id = 'editArea';
    textarea.value = currentContent || '';
    contentDiv.innerHTML = '';
    contentDiv.appendChild(textarea);
    editBtn.textContent = '📖 预览';
    saveBtn.style.display = 'inline-block';
    cancelBtn.style.display = 'inline-block';
    statusEl.textContent = '编辑模式';
    textarea.focus();
    setupEditorListeners(textarea);
}

function exitEditMode(save) {
    isEditMode = false;
    editBtn.textContent = '✏️ 编辑';
    saveBtn.style.display = 'none';
    cancelBtn.style.display = 'none';
    if (save) {
        if (currentFile) {
            getFileContent(currentFile);
        }
        statusEl.textContent = '预览模式 (已保存)';
    } else {
        displayFileContent(originalContent, currentFile);
        statusEl.textContent = '预览模式';
    }
}

function saveContent() {
    const textarea = document.getElementById('editArea');
    if (!textarea) {
        alert('请先进入编辑模式');
        return;
    }
    const content = textarea.value;
    saveFileContent(currentFile, content);
    statusEl.textContent = '保存中...';
}

function cancelEdit() {
    exitEditMode(false);
}

export function onSaveComplete() {
    exitEditMode(true);
}

export function onImageInserted(imagePath, insertedText) {
    statusEl.textContent = '图片已插入';
    if (isEditMode) {
        const textarea = document.getElementById('editArea');
        if (textarea) {
            const start = textarea.selectionStart;
            const before = textarea.value.substring(0, start);
            const after = textarea.value.substring(start);
            textarea.value = before + insertedText + after;
            const newPos = start + insertedText.length;
            textarea.selectionStart = textarea.selectionEnd = newPos;
            textarea.focus();
            textarea.dispatchEvent(new Event('input'));
            currentContent = textarea.value;
        }
    }
}

// ========== 编辑器监听器 ==========
function setupEditorListeners(textarea) {
    textarea.addEventListener('input', function(e) {
        const pos = this.selectionStart;
        if (isAtTrigger(this, pos)) {
            const keyword = getSearchKeyword(this, pos);
            showAutocomplete(keyword, this);
            closeCommand();
            return;
        }
        if (isAtCommand(this, pos)) {
            showCommand(this);
            closeAutocomplete();
            return;
        }
        closeAutocomplete();
        closeCommand();
    });

    textarea.addEventListener('keydown', function(e) {
        if (autocompleteActive) {
            const popup = autocompletePopup;
            const items = popup.querySelectorAll('li');
            if (!items.length) return;
            let current = popup.querySelector('.selected');
            let idx = -1;
            if (current) idx = Array.from(items).indexOf(current);
            if (e.key === 'ArrowDown') {
                e.preventDefault();
                idx = (idx + 1) % items.length;
                items.forEach(item => item.classList.remove('selected'));
                items[idx].classList.add('selected');
                items[idx].scrollIntoView({ block: 'nearest' });
            } else if (e.key === 'ArrowUp') {
                e.preventDefault();
                idx = (idx - 1 + items.length) % items.length;
                items.forEach(item => item.classList.remove('selected'));
                items[idx].classList.add('selected');
                items[idx].scrollIntoView({ block: 'nearest' });
            } else if (e.key === 'Enter' || e.key === 'Tab') {
                e.preventDefault();
                const selected = popup.querySelector('.selected') || items[0];
                if (selected) {
                    insertLink(textarea, selected.dataset.path);
                    closeAutocomplete();
                }
            } else if (e.key === 'Escape') {
                closeAutocomplete();
            }
            return;
        }

        if (commandActive) {
            const popup = commandPopup;
            const items = popup.querySelectorAll('li');
            if (!items.length) return;
            let current = popup.querySelector('.selected');
            let idx = -1;
            if (current) idx = Array.from(items).indexOf(current);
            if (e.key === 'ArrowDown') {
                e.preventDefault();
                idx = (idx + 1) % items.length;
                items.forEach(item => item.classList.remove('selected'));
                items[idx].classList.add('selected');
                items[idx].scrollIntoView({ block: 'nearest' });
            } else if (e.key === 'ArrowUp') {
                e.preventDefault();
                idx = (idx - 1 + items.length) % items.length;
                items.forEach(item => item.classList.remove('selected'));
                items[idx].classList.add('selected');
                items[idx].scrollIntoView({ block: 'nearest' });
            } else if (e.key === 'Enter' || e.key === 'Tab') {
                e.preventDefault();
                const selected = popup.querySelector('.selected') || items[0];
                if (selected) {
                    const idx = parseInt(selected.dataset.index);
                    const cmd = commandFiltered[idx];
                    if (cmd) {
                        closeCommand();
                        const start = textarea.selectionStart;
                        const before = textarea.value.substring(0, start - 1);
                        const after = textarea.value.substring(start);
                        textarea.value = before + after;
                        textarea.selectionStart = textarea.selectionEnd = start - 1;
                        textarea.dispatchEvent(new Event('input'));
                        cmd.action();
                    }
                }
            } else if (e.key === 'Escape') {
                closeCommand();
            }
            return;
        }
    });
}

// ========== 辅助函数 ==========
function getCaretCoordinates(element, position) {
    const div = document.createElement('div');
    const style = window.getComputedStyle(element);
    ['fontSize', 'fontFamily', 'fontWeight', 'letterSpacing', 'lineHeight', 'padding'].forEach(s => div.style[s] = style[s]);
    div.style.position = 'absolute';
    div.style.whiteSpace = 'pre-wrap';
    div.style.wordWrap = 'break-word';
    div.style.visibility = 'hidden';
    div.textContent = element.value.substring(0, position);
    const span = document.createElement('span');
    span.textContent = element.value.substring(position) || ' ';
    div.appendChild(span);
    document.body.appendChild(div);
    const { offsetLeft, offsetTop } = span;
    document.body.removeChild(div);
    return { left: offsetLeft, top: offsetTop };
}