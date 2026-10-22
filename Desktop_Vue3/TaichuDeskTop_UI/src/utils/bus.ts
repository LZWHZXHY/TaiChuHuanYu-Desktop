import mitt from 'mitt';

type Events = {
  'auth-expired': string;
  'api-error': { 
    msg: string; 
    detail?: string; 
    title?: string; // 🌟 补上这一行
    type?: string;  // 🌟 补上这一行
  };
};

export const bus = mitt<Events>();
