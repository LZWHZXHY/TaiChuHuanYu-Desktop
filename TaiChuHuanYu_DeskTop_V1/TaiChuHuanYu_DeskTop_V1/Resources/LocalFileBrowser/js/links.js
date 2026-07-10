import { openLink } from './api.js';

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
    if (!links || links.length === 0) {
        html += '<p style="color:#666;">没有笔记引用此文件</p>';
    } else {
        html += '<ul style="list-style:none;padding:0;">';
        links.forEach(link => {
            const safeLink = link.replace(/</g, '&lt;').replace(/>/g, '&gt;');
            html += `<li style="padding:4px 0;">
                        <span class="wikilink" data-path="${safeLink}">${safeLink}</span>
                     </li>`;
        });
        html += '</ul>';
    }
    container.innerHTML = html;

    container.querySelectorAll('.wikilink').forEach(el => {
        el.addEventListener('click', function() {
            openLink(this.dataset.path);
        });
    });
}