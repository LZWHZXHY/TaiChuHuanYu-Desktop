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

// 更新游戏 DTO
export interface UpdateGameDto {
  title?: string
  description?: string
  icon?: string
  status?: '草稿' | '已发布'
  scoring?: 'sum' | 'average'
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
  creator?: {
    id: string
    username: string
  }
  questionnaire?: {
    id: number
    scoring: 'sum' | 'average'
    questions: (GameQuestion & { id: number; order: number })[]
    results: (GameResult & { id: number; order: number })[]
  }
}

// ===== 试玩记录类型 =====

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


