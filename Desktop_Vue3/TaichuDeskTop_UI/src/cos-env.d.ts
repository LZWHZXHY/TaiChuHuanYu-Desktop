// src/cos-env.d.ts

// 1. 腾讯云 COS 声明保持不变
declare module 'cos-js-sdk-v5' {
    const COS: any;
    export default COS;
}

// 2. 🌟 全量放行 luckysheet 核心模块
declare module 'luckysheet' {
    const luckysheet: any;
    export default luckysheet;
}

// 3. 🌟 核心补丁：放行副作用直接引入的非标准 .js 和 .css 物理路径
declare module 'luckysheet/dist/plugins/plugins.js' {
    const content: any;
    export default content;
}

declare module 'luckysheet/dist/plugins/css/pluginsCss.css' {
    const content: any;
    export default content;
}

declare module 'luckysheet/dist/css/luckysheet.css' {
    const content: any;
    export default content;
}