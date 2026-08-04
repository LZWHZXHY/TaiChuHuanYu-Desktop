import request from '@/utils/request'

// ===== 类型定义 =====

export interface CreateOptionDto {
  optionText: string
  optionValue?: string
  sortOrder?: number
}

export interface CreateQuestionDto {
  questionType: number  // 1=单选 2=多选 3=填空 4=评分 5=排序 6=矩阵
  title: string
  description?: string
  isRequired?: boolean
  sortOrder?: number
  config?: string
  options: CreateOptionDto[]
}

export interface CreateSurveyRequest {
  title: string
  description?: string
  coverImage?: string
  startTime: string
  endTime: string
  isPublic?: boolean
  allowAnonymous?: boolean
  maxSubmissions?: number
  questions: CreateQuestionDto[]
}

export interface SurveyListItem {
  id: number
  title: string
  description?: string
  coverImage?: string
  status: number
  startTime: string
  endTime: string
  totalSubmissions: number
  questionCount: number
  createdAt: string
  creatorName: string
}

export interface OptionDto {
  id: number
  optionText: string
  optionValue?: string
  sortOrder: number
}

export interface QuestionFillDto {
  id: number
  questionType: number
  title: string
  description?: string
  isRequired: boolean
  config?: string
  options: OptionDto[]
}

export interface SurveyFillResponse {
  id: number
  title: string
  description?: string
  coverImage?: string
  status: number
  isPublic: boolean
  hasSubmitted: boolean
  questions: QuestionFillDto[]
}

export interface QuestionAnswerDto {
  questionId: number
  answerText?: string
  selectedOptionIds?: number[]
  sortResult?: number[]
  matrixResult?: Record<string, number>
}

export interface SubmitSurveyRequest {
  answers: QuestionAnswerDto[]
  completedTime?: number
}

// ===== API 方法 =====

// 创建问卷
export const createSurvey = (data: CreateSurveyRequest) => {
  return request.post('/survey/create', data)
}

// 获取问卷列表
export const getSurveyList = (status?: string) => {
  const url = status ? `/survey/list?status=${status}` : '/survey/list'
  return request.get(url)
}



// 获取问卷填写内容（用户端）
export const getSurveyFill = (id: number) => {
  return request.get(`/survey/${id}/fill`)
}

// 提交问卷
export const submitSurvey = (id: number, data: SubmitSurveyRequest) => {
  return request.post(`/survey/${id}/submit`, data)
}

// 发布问卷
export const publishSurvey = (id: number) => {
  return request.post(`/survey/${id}/publish`)
}



// 结束问卷
export const closeSurvey = (id: number) => {
  return request.post(`/survey/${id}/close`)
}

// 删除问卷
export const deleteSurvey = (id: number) => {
  return request.delete(`/survey/${id}`)
}



// 获取问卷统计结果
export const getSurveyStats = (id: number) => {
  return request.get(`/survey/${id}/stats`)
}

// 获取问卷详情（用于编辑）
export const getSurveyDetail = (id: number) => {
  return request.get(`/survey/${id}`)
}

// 更新问卷
export const updateSurvey = (id: number, data: any) => {
  return request.put(`/survey/${id}`, data)
}