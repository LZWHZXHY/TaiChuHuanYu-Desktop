import request from '@/utils/request';

export interface FollowStatus {
  following: boolean;
  followerCount: number;
  followingCount: number;
}

export const followApi = {
  toggle: (followeeId: string) =>
    request.post<{ following: boolean; followerCount: number }>(`/Follow/${followeeId}`, undefined, { silentError: true }),
  status: (followeeId: string) =>
    request.get<FollowStatus>(`/Follow/status/${followeeId}`, { silentError: true })
};
