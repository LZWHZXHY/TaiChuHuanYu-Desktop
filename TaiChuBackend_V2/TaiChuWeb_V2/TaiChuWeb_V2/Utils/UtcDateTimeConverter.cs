using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaiChuWeb_V2.Utils
{
    /// <summary>
    /// 统一时间的输出格式。
    /// 项目里存进数据库的时间都是 UTC（DateTime.UtcNow / UTC_TIMESTAMP()），但从 MySQL 读出来时丢了时区标记，
    /// 默认会输出成 "2026-10-30T23:35:12" 这种不带 Z 的样子，浏览器把它当成“看的人自己的本地时间”——
    /// 于是在美国刚发的回复，国内的人看就成了“8 小时前”。这里把没有时区标记的时间一律按 UTC 输出（带 Z），
    /// 每个人的浏览器再换算成自己的本地时间。
    /// 例外：正好 00:00:00 的值当成“日期”（生日、签到日、岁月史书发生的那天……），保持原样不加 Z，
    /// 免得在西半球被换算成前一天。
    /// 读入：带时区的（…Z / +08:00）一律转成 UTC；不带时区的原样（和以前一样）。
    /// </summary>
    public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetDateTime();
            return value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            if (value.Kind == DateTimeKind.Unspecified && value.TimeOfDay != TimeSpan.Zero)
                value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
            writer.WriteStringValue(value);
        }
    }

    /// <summary>
    /// 按“墙上时间”原样输出、不加时区（给存的是服务器本地时间的旧字段用，例如财务流水的日期）。
    /// 用法：在属性上加 [JsonConverter(typeof(NaiveDateTimeConverter))]。
    /// </summary>
    public sealed class NaiveDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDateTime();

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            => writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Unspecified));
    }
}
