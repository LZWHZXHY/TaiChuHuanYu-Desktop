import request from '@/utils/request';

export interface ChaiAchievement {
  key: string;
  label: string;
  icon: string;
  unlocked: boolean;
  progress: number;
  target: number;
}

export const chaiAchievementApi = {
  my: () =>
    request.get<{ achievements: ChaiAchievement[]; unlockedCount: number }>('/ChaiAchievement/my', { silentError: true })
};
