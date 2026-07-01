// src/utils/templateFactory.ts
export const getInitialBlocks = (type: string) => {
  switch (type) {
    // 白板等异构引擎保留
    case 'canvas': return [{ type: 'canvas-node', data: "{}" }]; 
    
    // 一切文字流，皆为最干净的 Note
    case 'note':
    default:
      return [{ type: 'paragraph', data: JSON.stringify({}) }];
  }
};