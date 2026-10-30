using System.Text.Json;
using System.Text.RegularExpressions;

namespace TaiChuWeb_V2.Services.Paint
{
    /// <summary>
    /// 自制笔刷的参数。保存到笔刷库、以及每一笔自制笔刷的笔画里都带一份（规范化之后），
    /// 这样笔刷以后再改，已经画下去的笔画也不会变样。字段名在 JSON 里是驼峰（tip / tipHash / hardness …）。
    /// </summary>
    public sealed class PaintBrushParams
    {
        /// <summary>round 圆形笔尖 / image 图案笔尖（TipHash 指向上传的笔尖图）</summary>
        public string Tip { get; set; } = "round";
        public string? TipHash { get; set; }
        /// <summary>圆形笔尖的硬度 0~1</summary>
        public double Hardness { get; set; } = 1;
        /// <summary>扁圆程度 0.05~1（1 = 正圆）</summary>
        public double Roundness { get; set; } = 1;
        /// <summary>笔尖角度（度）</summary>
        public double Angle { get; set; }
        /// <summary>笔尖随笔画方向转动</summary>
        public bool FollowDirection { get; set; }
        /// <summary>间距（笔尖直径的倍数）0.02~3</summary>
        public double Spacing { get; set; } = 0.1;
        /// <summary>散布（笔尖直径的倍数）0~3</summary>
        public double Scatter { get; set; }
        /// <summary>每一步盖几个印章 1~6</summary>
        public int Count { get; set; } = 1;
        public double SizeJitter { get; set; }
        public double OpacityJitter { get; set; }
        public double AngleJitter { get; set; }
        /// <summary>每个印章的浓度 0.02~1</summary>
        public double Flow { get; set; } = 1;
        /// <summary>压感为 0 时保留的粗细比例 0~1</summary>
        public double MinSize { get; set; } = 0.2;
        public bool PressureSize { get; set; } = true;
        public bool PressureOpacity { get; set; }
        /// <summary>起笔 / 收笔渐细的长度（像素）0~400</summary>
        public double TaperStart { get; set; }
        public double TaperEnd { get; set; }
        /// <summary>选用这支笔刷时的默认大小、不透明度，和大小滑块的上限</summary>
        public double Size { get; set; } = 12;
        public double Opacity { get; set; } = 1;
        public double MaxSize { get; set; } = 200;

        public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private static readonly Regex HashRe = new("^[0-9a-f]{64}$", RegexOptions.Compiled);
        public static bool IsHash(string? s) => s != null && HashRe.IsMatch(s);

        private static double C(double v, double min, double max, double fallback) => double.IsNaN(v) || double.IsInfinity(v) ? fallback : Math.Round(Math.Clamp(v, min, max), 3);

        /// <summary>把所有数值夹到合法范围（客户端传什么都不怕）</summary>
        public PaintBrushParams Normalized()
        {
            var image = Tip == "image" && IsHash(TipHash);
            var maxSize = C(MaxSize, 4, 300, 200);
            return new PaintBrushParams
            {
                Tip = image ? "image" : "round",
                TipHash = image ? TipHash : null,
                Hardness = C(Hardness, 0, 1, 1),
                Roundness = C(Roundness, 0.05, 1, 1),
                Angle = C(Angle, -360, 360, 0),
                FollowDirection = FollowDirection,
                Spacing = C(Spacing, 0.02, 3, 0.1),
                Scatter = C(Scatter, 0, 3, 0),
                Count = Math.Clamp(Count, 1, 6),
                SizeJitter = C(SizeJitter, 0, 1, 0),
                OpacityJitter = C(OpacityJitter, 0, 1, 0),
                AngleJitter = C(AngleJitter, 0, 1, 0),
                Flow = C(Flow, 0.02, 1, 1),
                MinSize = C(MinSize, 0, 1, 0.2),
                PressureSize = PressureSize,
                PressureOpacity = PressureOpacity,
                TaperStart = C(TaperStart, 0, 400, 0),
                TaperEnd = C(TaperEnd, 0, 400, 0),
                MaxSize = maxSize,
                Size = C(Size, 1, maxSize, 12),
                Opacity = C(Opacity, 0.02, 1, 1),
            };
        }

        public string ToJson() => JsonSerializer.Serialize(this, Json);
        public static PaintBrushParams? FromJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonSerializer.Deserialize<PaintBrushParams>(json, Json)?.Normalized(); }
            catch (JsonException) { return null; }
        }
    }
}
