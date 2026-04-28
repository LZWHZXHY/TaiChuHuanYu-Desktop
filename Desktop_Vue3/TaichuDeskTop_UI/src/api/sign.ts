import request from '../utils/request';


export type SignData = Record<string, number>;

export const signApi = {

  getMonthData: (year: number, month: number) => 
    request.get<any, SignData>('/Sign/month-data', { 
      params: { year, month } 
    }),


  doSign: () => 
    request.post<any, { message: string, pointsAdded: number }>('/Sign/do-sign')
};