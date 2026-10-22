import * as THREE from 'three'
import type { WorldGraphNode } from './worldGraph'

export const createNodeObject = (node: WorldGraphNode) => {
  const scale = 4;
  const baseW = 160;
  const baseH = 48;

  const textLen = node.title.length;
  const width = Math.max(baseW, baseW + (textLen - 6) * 14);
  const height = baseH;

  const canvas = document.createElement('canvas');
  canvas.width = width * scale;
  canvas.height = height * scale;
  const ctx = canvas.getContext('2d')!;
  ctx.scale(scale, scale);

  // 圆角矩形
  const radius = 12;
  ctx.beginPath();
  ctx.moveTo(radius, 0);
  ctx.lineTo(width - radius, 0);
  ctx.quadraticCurveTo(width, 0, width, radius);
  ctx.lineTo(width, height - radius);
  ctx.quadraticCurveTo(width, height, width - radius, height);
  ctx.lineTo(radius, height);
  ctx.quadraticCurveTo(0, height, 0, height - radius);
  ctx.lineTo(0, radius);
  ctx.quadraticCurveTo(0, 0, radius, 0);
  ctx.closePath();

  // 渐变
  const gradient = ctx.createLinearGradient(0, 0, 0, height);
  gradient.addColorStop(0, node.color);
  gradient.addColorStop(1, shadeColor(node.color, -15));
  ctx.fillStyle = gradient;
  ctx.fill();

  // 文字
  ctx.fillStyle = '#ffffff';
  ctx.font = 'bold 16px -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(node.title, width / 2, height / 2 + 1);

  const texture = new THREE.CanvasTexture(canvas);
  texture.minFilter = THREE.LinearFilter;
  texture.magFilter = THREE.LinearFilter;

  const material = new THREE.SpriteMaterial({
    map: texture,
    transparent: true,
    depthTest: false,
  });
  const sprite = new THREE.Sprite(material);

  const worldHeight = 10;
  const worldWidth = worldHeight * (width / height);
  sprite.scale.set(worldWidth, worldHeight, 1);

  return sprite;
};

const shadeColor = (hex: string, percent: number): string => {
  const num = parseInt(hex.replace('#', ''), 16);
  const r = Math.max(0, Math.min(255, (num >> 16) + percent));
  const g = Math.max(0, Math.min(255, ((num >> 8) & 0x00ff) + percent));
  const b = Math.max(0, Math.min(255, (num & 0x0000ff) + percent));
  return '#' + ((r << 16) | (g << 8) | b).toString(16).padStart(6, '0');
};
