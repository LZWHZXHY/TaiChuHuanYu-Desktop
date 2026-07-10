import { openLink, getBacklinks } from './api.js';

let currentPath = '';

// 处理 [[...]] 替换并请求反向链接
export function processContentAndLinks(content, path) {
    currentPath = path;
    // 替换 [[...]] 为 wikilink 元素
    if (content) {
        const html = content.replace(/\[\[([^\]]+)\]\]/g, (match, p1) => {
            let parts = p1.split('|');
            let linkPath = parts[0].trim();
            let displayName = parts.length > 1 ? parts[1].trim() : linkPath;
            if (!linkPath.endsWith('.md')) linkPath += '.md';
            // 这里只返回 HTML，实际插入由 editor 模块完成，但我们通过事件让 editor 更新
            return `<span class="wikilink" data-path="${linkPath}">${displayName}</span>`;
        });
        // 触发更新内容的事件
        const event = new CustomEvent('content-updated', { detail: { html, path } });
        document.dispatchEvent(event);
    }
    // 请求反向链接
    getBacklinks(path);
}

// 渲染反向链接列表
export function renderBacklinks(links) {
    let container = document.getElementById('backlinks-section');
    if (!container) {
        container = document.createElement('div');
        container.id = 'backlinks-section';
        container.style.marginTop = '30px';
        container.style.borderTop = '1px solid #3e3e3e';
        container.style.paddingTop = '15px';
        const contentDiv = document.getElementById('fileContent');
        if (contentDiv) contentDiv.appendChild(container);
    }
    let html = '<h3 style="color:#888;font-weight:300;">反向链接</h3>';
    if (links.length === 0) {
        html += '<p style="color:#666;">没有笔记引用此文件</p>';
    } else {
        html += '<ul style="list-style:none;padding:0;">';
        links.forEach(link => {
            html += `<li style="padding:4px 0;"><span class="wikilink" data-path="${link}">${link}</span></li>`;
        });
        html += '</ul>';
    }
    container.innerHTML = html;
    // 绑定点击事件
    container.querySelectorAll('.wikilink').forEach(el => {
        el.addEventListener('click', function() {
            openLink(this.dataset.path);
        });
    });
}