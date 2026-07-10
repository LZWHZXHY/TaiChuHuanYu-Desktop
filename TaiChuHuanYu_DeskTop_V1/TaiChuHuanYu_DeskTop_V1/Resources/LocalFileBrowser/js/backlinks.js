// backlinks.js - 反向链接渲染模块
import { openLink } from './api.js';

/**
 * 渲染反向链接列表
 * @param {string[]} links - 反向链接文件路径数组
 */
export function renderBacklinks(links) {
    // 查找或创建反向链接容器
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

    // 生成 HTML
    let html = '<h3 style="color:#888;font-weight:300;">反向链接</h3>';
    if (!links || links.length === 0) {
        html += '<p style="color:#666;">没有笔记引用此文件</p>';
    } else {
        html += '<ul style="list-style:none;padding:0;">';
        links.forEach(link => {
            // 转义 HTML 防止 XSS（但路径通常安全）
            const safeLink = link.replace(/</g, '&lt;').replace(/>/g, '&gt;');
            html += `<li style="padding:4px 0;">
                        <span class="wikilink" data-path="${safeLink}">${safeLink}</span>
                     </li>`;
        });
        html += '</ul>';
    }
    container.innerHTML = html;

    // 为所有 wikilink 绑定点击事件
    container.querySelectorAll('.wikilink').forEach(el => {
        el.addEventListener('click', function() {
            openLink(this.dataset.path);
        });
    });
}