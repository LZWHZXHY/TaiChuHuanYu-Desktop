// ../api/interact.ts
import request from '../utils/request';

// 必须加 export，否则外部组件无法 import 它
export interface InteractionResponse {
  isActive: boolean;
  newCount: number;
}

export const interactApi = {
  /**
   * 通用交互接口
   */
  toggleAction: (targetId: string | number, targetType: string, actionType: string) =>
    // 这里使用泛型，确保返回值的 data 部分符合 InteractionResponse 结构
    request.post<any, InteractionResponse>('/Interaction/toggle-action', null, {
      params: { targetId, targetType, actionType }
    })
};