namespace TaiChuWeb_V2.Services.Shop
{
    /// <summary>
    /// 看文件头判断图片格式、是不是动图（不解码整张图）。
    /// 用户自己上传的头像只能是静态图：GIF、动态 WebP、APNG 一律拒绝——动图头像只能用官方提供的（可以在交易行出售）。
    /// </summary>
    public static class AvatarImage
    {
        public const int MaxUserBytes = 5 * 1024 * 1024;
        public const int MaxOfficialBytes = 8 * 1024 * 1024;

        public sealed record Info(string Ext, string ContentType, bool Animated);

        /// <summary>识别不了（不是 JPG / PNG / WebP / GIF）时返回 null</summary>
        public static Info? Sniff(ReadOnlySpan<byte> b)
        {
            if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return new("jpg", "image/jpeg", false);
            if (b.Length >= 6 && b[0] == (byte)'G' && b[1] == (byte)'I' && b[2] == (byte)'F' && b[3] == (byte)'8')
                return new("gif", "image/gif", true); // GIF 一律按动图处理（单帧 GIF 也没必要用户自己传）
            if (b.Length >= 8 && b[0] == 0x89 && b[1] == (byte)'P' && b[2] == (byte)'N' && b[3] == (byte)'G')
                return new("png", "image/png", PngIsAnimated(b));
            if (b.Length >= 16 && b[0] == (byte)'R' && b[1] == (byte)'I' && b[2] == (byte)'F' && b[3] == (byte)'F'
                && b[8] == (byte)'W' && b[9] == (byte)'E' && b[10] == (byte)'B' && b[11] == (byte)'P')
                return new("webp", "image/webp", WebpIsAnimated(b));
            return null;
        }

        /// <summary>APNG：在第一个 IDAT 之前有 acTL 块</summary>
        private static bool PngIsAnimated(ReadOnlySpan<byte> b)
        {
            var pos = 8;
            while (pos + 8 <= b.Length)
            {
                var len = (b[pos] << 24) | (b[pos + 1] << 16) | (b[pos + 2] << 8) | b[pos + 3];
                var type = b.Slice(pos + 4, 4);
                if (type.SequenceEqual("acTL"u8)) return true;
                if (type.SequenceEqual("IDAT"u8) || len < 0) return false;
                pos += 12 + len;
            }
            return false;
        }

        /// <summary>动态 WebP：VP8X 头里的动画标志位，或者有 ANIM / ANMF 块</summary>
        private static bool WebpIsAnimated(ReadOnlySpan<byte> b)
        {
            var pos = 12;
            while (pos + 8 <= b.Length)
            {
                var type = b.Slice(pos, 4);
                var len = b[pos + 4] | (b[pos + 5] << 8) | (b[pos + 6] << 16) | (b[pos + 7] << 24);
                if (type.SequenceEqual("VP8X"u8) && pos + 8 < b.Length && (b[pos + 8] & 0x02) != 0) return true;
                if (type.SequenceEqual("ANIM"u8) || type.SequenceEqual("ANMF"u8)) return true;
                if (len < 0) return false;
                pos += 8 + len + (len & 1);
            }
            return false;
        }
    }
}
