// ===== 游戏相关类型 =====

export interface GameOption {
  label: string
  value: number
  image?: string
}

export interface GameQuestion {
  type: 'single' | 'yesno' | 'likert' | 'multiple'
  text: string
  image?: string
  options: GameOption[]
}

export interface GameResult {
  min: number
  max: number
  title: string
  description?: string
  icon?: string
  image?: string
}

// 创建游戏 DTO（发给后端）
export interface CreateGameDto {
  type: string
  icon: string
  title: string
  description?: string
  scoring?: 'sum' | 'average'
  questions: GameQuestion[]
  results: GameResult[]
}

// 更新游戏 DTO（连题目一起改时 questions 和 results 都要传）
export interface UpdateGameDto {
  title?: string
  description?: string
  icon?: string
  status?: '草稿' | '已发布'
  scoring?: 'sum' | 'average'
  questions?: GameQuestion[]
  results?: GameResult[]
}

// 游戏实体（后端返回）
export interface Game {
  id: number
  type: string
  icon: string
  title: string
  description: string
  status: '草稿' | '已发布'
  creatorId: string
  createdAt: string
  updatedAt: string
  expCost: number
  playCount: number
  /** 列表接口给的：作者名、题目数 */
  creatorName?: string
  questionCount?: number
  creator?: {
    id: string
    username: string
  }
  questionnaire?: {
    id: number
    scoring: 'sum' | 'average'
    questions: (Omit<GameQuestion, 'options'> & { id: number; order: number; options: (GameOption & { id: number })[] })[]
    results: (GameResult & { id: number; order: number })[]
  }
}

// ===== 试玩记录类型 =====

/** 交卷：每道题选了哪些选项 id（按题目顺序），分数由服务器算 */
export interface SubmitSessionDto {
  gameId: number
  optionIds: number[][]
}

export interface SubmitSessionResult {
  id: number
  totalScore: number
  result: { id: number; title: string; description?: string; icon?: string; image?: string; min: number; max: number } | null
}

export interface SaveSessionDto {
  gameId: number
  totalScore: number
  resultId?: number | null
  answers: number[]
}

export interface GameSession {
  id: number
  gameId: number
  userId: string
  totalScore: number
  resultId?: number
  answersJson: string
  createdAt: string
  game: Game
  result?: GameResult & { id: number }
}

// ===== API 响应类型 =====

export interface PaginatedResponse<T> {
  total: number
  items: T[]
}

export interface SessionSummary {
  id: number
  gameTitle: string
  gameIcon: string
  totalScore: number
  resultTitle: string | null
  resultIcon: string | null
  createdAt: string
}

export interface SessionDetail {
  id: number
  gameTitle: string
  gameIcon: string
  totalScore: number
  result: {
    title: string
    description: string
    icon: string
    image: string
  } | null
  createdAt: string
  questions: {
    id: number
    type: string
    text: string
    image: string
    selectedScore: number
    options: {
      id: number
      label: string
      value: number
      image: string
    }[]
  }[]
}


