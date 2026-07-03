using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net.Http;

namespace TaiChuWeb_V2.Services
{
    public class WatermarkService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public WatermarkService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<byte[]> ApplyWatermarkAsync(
            string imageUrl,
            string? watermarkText,
            string? watermarkImageUrl,
            string watermarkType,
            string position,
            int fontSize,
            double opacity,
            string color,
            int rotation,
            double imageScale,
            double imageOpacity)
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);

            using var ms = new MemoryStream(imageBytes);
            using var image = Image.FromStream(ms);

            var (x, y) = GetPosition(image.Width, image.Height, position);

            using var graphics = Graphics.FromImage(image);

            // 🔑 关键：设置高质量渲染参数，防止模糊
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;

            // ---- 图片水印 ----
            if ((watermarkType == "image" || watermarkType == "both") && !string.IsNullOrEmpty(watermarkImageUrl))
            {
                try
                {
                    var wmBytes = await httpClient.GetByteArrayAsync(watermarkImageUrl);
                    using var wmMs = new MemoryStream(wmBytes);
                    using var wmImage = Image.FromStream(wmMs);

                    int targetWidth = (int)(wmImage.Width * imageScale);
                    int targetHeight = (int)(wmImage.Height * imageScale);

                    // 🔑 高质量缩放水印图片
                    using var resizedWm = new Bitmap(targetWidth, targetHeight);
                    using var wmGraphics = Graphics.FromImage(resizedWm);
                    wmGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    wmGraphics.SmoothingMode = SmoothingMode.AntiAlias;
                    wmGraphics.DrawImage(wmImage, 0, 0, targetWidth, targetHeight);

                    var colorMatrix = new ColorMatrix { Matrix33 = (float)imageOpacity };
                    using var imageAttributes = new ImageAttributes();
                    imageAttributes.SetColorMatrix(colorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

                    graphics.DrawImage(
                        resizedWm,
                        new Rectangle(x - targetWidth / 2, y - targetHeight / 2, targetWidth, targetHeight),
                        0, 0, targetWidth, targetHeight,
                        GraphicsUnit.Pixel,
                        imageAttributes);
                }
                catch
                {
                    // 图片水印加载失败，静默跳过
                }
            }

            // ---- 文字水印 ----
            if ((watermarkType == "text" || watermarkType == "both") && !string.IsNullOrEmpty(watermarkText))
            {
                var colorParsed = ColorTranslator.FromHtml(color);
                var brushColor = Color.FromArgb((int)(opacity * 255), colorParsed);

                using var brush = new SolidBrush(brushColor);
                using var font = new Font("微软雅黑", fontSize, FontStyle.Bold);
                using var format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };

                if (rotation != 0)
                {
                    graphics.TranslateTransform(x, y);
                    graphics.RotateTransform(rotation);
                    graphics.TranslateTransform(-x, -y);
                }

                graphics.DrawString(watermarkText, font, brush, new PointF(x, y), format);

                if (rotation != 0)
                {
                    graphics.ResetTransform();
                }
            }

            // 🔑 保存为 PNG（无损），或 JPEG（有损但体积小）
            using var outMs = new MemoryStream();

            // 方案 A：PNG 无损（推荐，但文件大）
            image.Save(outMs, ImageFormat.Png);

            // 方案 B：JPEG 有损但可调质量（如需节省空间）
            // var jpegCodec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            // var encoderParams = new EncoderParameters { Param = new[] { new EncoderParameter(Encoder.Quality, 95L) } };
            // image.Save(outMs, jpegCodec, encoderParams);

            return outMs.ToArray();
        }

        private (int x, int y) GetPosition(int imageWidth, int imageHeight, string position)
        {
            int margin = 20;
            return position switch
            {
                "top-left" => (margin, margin),
                "top-center" => (imageWidth / 2, margin),
                "top-right" => (imageWidth - margin, margin),
                "center-left" => (margin, imageHeight / 2),
                "center" => (imageWidth / 2, imageHeight / 2),
                "center-right" => (imageWidth - margin, imageHeight / 2),
                "bottom-left" => (margin, imageHeight - margin),
                "bottom-center" => (imageWidth / 2, imageHeight - margin),
                "bottom-right" => (imageWidth - margin, imageHeight - margin),
                _ => (imageWidth - margin, imageHeight - margin),
            };
        }
    }
}