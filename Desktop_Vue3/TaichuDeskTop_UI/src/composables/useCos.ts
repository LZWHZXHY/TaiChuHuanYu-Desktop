// useCos.ts
import COS from 'cos-js-sdk-v5';
import request from '../utils/request';
import { ref } from 'vue';

interface UploadResult {
  url: string;
  location: string;
}

interface RegisteredAsset {
  assetId: string;
  url: string;
  location: string;
  kind: string;
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
        const data: any = await request.get('/Cos/get-credential');
        callback({
          TmpSecretId: data.credentials.tmpSecretId,
          TmpSecretKey: data.credentials.tmpSecretKey,
          XCosSecurityToken: data.credentials.sessionToken,
          StartTime: data.startTime,
          ExpiredTime: data.expiredTime,
        });
      } catch (err: any) {
        console.error('获取 COS 密钥失败:', err.friendlyMessage || err);
      }
    }
  });

  /**
   * 只上传，不登记。保留原行为，兼容旧调用点。
   */
  const uploadFile = async (file: File, folder: string = 'uploads'): Promise<UploadResult> => {
    isUploading.value = true;
    progress.value = 0;

    const Bucket = 'tchy-images-1361988423';
    const Region = 'ap-beijing';

    let subFolder = folder;
    if (file.type.startsWith('image/')) subFolder = `${folder}/images`;
    else if (file.type.startsWith('video/')) subFolder = `${folder}/videos`;
    else if (file.type.startsWith('audio/')) subFolder = `${folder}/music`;
    else if (file.type === 'application/pdf') subFolder = `${folder}/pdfs`;

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
          resolve({
            url: `https://img.bianyuzhou.com/${Key}`,
            location: data.Location
          });
        }
      });
    });
  };

  /**
   * 登记到 assets 表，拿 assetId
   */
  const registerAsset = async (payload: {
    url: string;
    storageKey?: string;
    fileName?: string;
    mimeType?: string;
    size?: number;
    kind?: string;
    width?: number;
    height?: number;
    spaceId?: string;
  }): Promise<{ id: string; url: string; kind: string }> => {
    return await request.post('/assets/register', {
      kind: 'image',
      ...payload,
    }) as any;
  };

  /**
   * 上传 + 登记，推荐用这个。返回 assetId + url + kind。
   */
  const uploadAndRegister = async (
    file: File,
    folder: string = 'lingmai',
    spaceId?: string
  ): Promise<RegisteredAsset> => {
    const result = await uploadFile(file, folder);

    const kind = file.type.startsWith('image/') ? 'image'
               : file.type === 'application/pdf' ? 'pdf'
               : file.type.startsWith('video/') ? 'video'
               : file.type.startsWith('audio/') ? 'audio'
               : 'other';

    const asset: any = await registerAsset({
      url: result.url,
      storageKey: result.location,
      fileName: file.name,
      mimeType: file.type,
      size: file.size,
      kind,
      spaceId,
    });

    return {
      assetId: asset.id,
      url: asset.url,
      location: result.location,
      kind: asset.kind,
    };
  };

  return {
    uploadFile,
    registerAsset,
    uploadAndRegister,
    isUploading,
    progress,
  };
}