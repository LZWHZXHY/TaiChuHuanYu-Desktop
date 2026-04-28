// useCos.ts
import COS from 'cos-js-sdk-v5';
import request from '../utils/request'; // 【关键】引入你封装好的 request 实例
import { ref } from 'vue';

interface UploadResult {
  url: string;
  location: string;
}

interface CosProgressData {
  percent: number;
  [key: string]: any;
}

export function useCos() {
  const isUploading = ref(false);
  const progress = ref(0);

  const cos = new COS({
    getAuthorization: async (options: any, callback: (data: any) => void) => {
      try {
        // 【关键修改】使用封装好的 request，路径只需要写后缀
        // 注意：因为你的拦截器返回了 response.data，所以这里直接解构得到的即是后端对象
        const data: any = await request.get('/Cos/get-credential');
        
        callback({
          TmpSecretId: data.credentials.tmpSecretId,
          TmpSecretKey: data.credentials.tmpSecretKey,
          XCosSecurityToken: data.credentials.sessionToken,
          StartTime: data.startTime, 
          ExpiredTime: data.expiredTime,
        });
      } catch (err: any) {
        // 这里可以读取你拦截器里封装的 friendlyMessage
        console.error('获取 COS 密钥失败:', err.friendlyMessage || err);
      }
    }
  });

  const uploadFile = async (file: File, folder: string = 'uploads'): Promise<UploadResult> => {
    isUploading.value = true;
    progress.value = 0;

    // 建议：这里的配置也可以尝试通过后端接口动态下发
    const Bucket = 'tchy-images-1361988423'; 
    const Region = 'ap-beijing'; 

    let subFolder = folder;
    if (file.type.startsWith('image/')) subFolder = `${folder}/images`;
    else if (file.type.startsWith('video/')) subFolder = `${folder}/videos`;
    else if (file.type.startsWith('audio/')) subFolder = `${folder}/music`;

    const fileName = `${Date.now()}-${file.name}`;
    const Key = `${subFolder}/${fileName}`;

    return new Promise((resolve, reject) => {
      cos.uploadFile({
        Bucket,
        Region,
        Key,
        Body: file,
        onProgress: (progressData: CosProgressData) => {
          progress.value = Math.floor(progressData.percent * 100);
        }
      }, (err: any, data: any) => {
        isUploading.value = false;
        if (err) {
          reject(err);
        } else {
          // 手动拼接访问地址
          resolve({
            url: `https://img.bianyuzhou.com/${Key}`,
            location: data.Location
          });
        }
      });
    });
  };

  return {
    uploadFile,
    isUploading,
    progress
  };
}