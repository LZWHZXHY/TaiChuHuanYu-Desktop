import { getFileContent, getFileList } from './api.js';

let currentFile = null;
let fileTreeContainer = null;

export function initTree(containerId) {
    fileTreeContainer = document.getElementById(containerId);
}

export function renderTree(files) {
    const validFiles = (files || []).filter(f => f && (f.Path || f.path));
    if (validFiles.length === 0) {
        fileTreeContainer.innerHTML = '<div style="padding:20px;color:#888;">库中暂无文件</div>';
        return;
    }

    const root = { children: [] };
    validFiles.forEach(f => {
        const path = f.Path || f.path;
        const parts = path.split('/');
        let current = root;
        for (let i = 0; i < parts.length; i++) {
            const part = parts[i];
            if (i === parts.length - 1) {
                if (!current.children) current.children = [];
                current.children.push({
                    type: 'file',
                    name: part,
                    path: path,
                    size: f.Size || f.size,
                    modified: f.Modified || f.modified
                });
            } else {
                if (!current.children) current.children = [];
                let folder = current.children.find(c => c.type === 'folder' && c.name === part);
                if (!folder) {
                    folder = { type: 'folder', name: part, path: path, children: [] };
                    current.children.push(folder);
                }
                current = folder;
            }
        }
    });

    // 排序
    function sortChildren(node) {
        if (node.children) {
            node.children.sort((a, b) => {
                if (a.type === 'folder' && b.type !== 'folder') return -1;
                if (a.type !== 'folder' && b.type === 'folder') return 1;
                return a.name.localeCompare(b.name);
            });
            node.children.forEach(child => sortChildren(child));
        }
    }
    sortChildren(root);

    fileTreeContainer.innerHTML = '';
    if (root.children && root.children.length > 0) {
        const ul = createTreeUL(root.children);
        fileTreeContainer.appendChild(ul);
    } else {
        fileTreeContainer.innerHTML = '<div style="padding:20px;color:#888;">库中暂无文件</div>';
    }
}

function createTreeUL(children) {
    const ul = document.createElement('ul');
    ul.className = 'tree-ul';
    children.forEach(child => {
        const li = document.createElement('li');
        const div = document.createElement('div');
        div.className = 'tree-item' + (child.type === 'folder' ? ' folder' : ' file');

        if (child.type === 'folder') {
            const toggleSpan = document.createElement('span');
            toggleSpan.className = 'folder-toggle';
            toggleSpan.textContent = '▶';
            const iconSpan = document.createElement('span');
            iconSpan.className = 'icon';
            iconSpan.textContent = '📁';
            const nameSpan = document.createElement('span');
            nameSpan.className = 'name';
            nameSpan.textContent = child.name;
            div.appendChild(toggleSpan);
            div.appendChild(iconSpan);
            div.appendChild(nameSpan);
            div.addEventListener('click', function(e) {
                e.stopPropagation();
                const childUL = li.querySelector('ul');
                if (childUL) {
                    const isHidden = childUL.style.display === 'none';
                    childUL.style.display = isHidden ? '' : 'none';
                    toggleSpan.textContent = isHidden ? '▼' : '▶';
                }
            });
            li.appendChild(div);
            if (child.children && child.children.length > 0) {
                const childUL = createTreeUL(child.children);
                childUL.style.paddingLeft = '20px';
                childUL.style.display = 'none';
                li.appendChild(childUL);
            }
        } else {
            const iconSpan = document.createElement('span');
            iconSpan.className = 'icon';
            iconSpan.textContent = '📄';
            const nameSpan = document.createElement('span');
            nameSpan.className = 'name';
            nameSpan.textContent = child.name;
            div.appendChild(iconSpan);
            div.appendChild(nameSpan);
            div.addEventListener('click', function(e) {
                e.stopPropagation();
                document.querySelectorAll('.tree-item.active').forEach(el => el.classList.remove('active'));
                div.classList.add('active');
                loadFileContent(child.path);
            });
            li.appendChild(div);
        }
        ul.appendChild(li);
    });
    return ul;
}

export function loadFileContent(path) {
    currentFile = path;
    const ext = path.split('.').pop().toLowerCase();
    const contentDiv = document.getElementById('fileContent');
    const fileNameEl = document.getElementById('fileName');

    // ---- PDF 预览 ----
    if (ext === 'pdf') {
        fileNameEl.textContent = path;
        contentDiv.innerHTML = `<iframe src="https://vault.local/${path}" style="width:100%;height:100%;border:none;min-height:600px;"></iframe>`;
        const event = new CustomEvent('file-selected', { detail: { path } });
        document.dispatchEvent(event);
        return;
    }

    // ---- 图片预览 ----
    if (['jpg', 'jpeg', 'png', 'gif', 'bmp', 'webp', 'svg', 'ico'].includes(ext)) {
        fileNameEl.textContent = path;
        contentDiv.innerHTML = `<img src="https://vault.local/${path}" style="max-width:100%;height:auto;display:block;margin:20px auto;border-radius:4px;" alt="${path}" />`;
        const event = new CustomEvent('file-selected', { detail: { path } });
        document.dispatchEvent(event);
        return;
    }

    // ---- 其他文件（如 .md, .txt）走原有逻辑 ----
    const event = new CustomEvent('file-selected', { detail: { path } });
    document.dispatchEvent(event);
    getFileContent(path);
}

export function expandAll() {
    document.querySelectorAll('#fileTree ul ul').forEach(ul => ul.style.display = '');
    document.querySelectorAll('.folder-toggle').forEach(t => t.textContent = '▼');
}

export function refreshTree() {
    getFileList();
}