// src/views/太初俱乐部/api/customerService.ts
import request from '@/utils/request'

export interface CustomerService {
  id: string
  name: string
  wechat: string
  avatar?: string
  status: 'online' | 'busy' | 'offline'
  currentLoad: number
  capacity: number
  isAssignable: boolean
}

/**
 * 获取客服列表（含在线状态）
 * GET /api/club/customer-services
 */
export function fetchCustomerServices() {
  return request.get<CustomerService[]>('/club/customer-services')
}