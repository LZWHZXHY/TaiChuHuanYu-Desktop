import mitt from 'mitt';

type Events = {
  'api-error': { msg: string; detail: string };
};

export const bus = mitt<Events>();