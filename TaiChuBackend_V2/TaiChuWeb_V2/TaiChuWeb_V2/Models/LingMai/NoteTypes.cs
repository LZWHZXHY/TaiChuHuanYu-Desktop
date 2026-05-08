// TaiChuWeb_V2/Models/LingMai/NoteTypes.cs
namespace TaiChuWeb_V2.Models.LingMai
{
    public static class NoteTypes
    {
        public const string Note = "note";         // 基础笔记、长文随笔、博客
        public const string Thought = "thought";   // 短动态、简语
        public const string Art = "art";           // 艺术作品、画廊
        public const string Video = "video";       // 视频内容
        public const string Audio = "audio";       // 声音/音乐（未来扩展）

        // 校验前端传过来的 Type 是否合法
        public static bool IsValid(string type)
        {
            return type switch
            {
                Note or Thought or Art or Video or Audio => true,
                _ => false
            };
        }

        /// <summary>
        /// 🌟 视界隔离规则：判断该类型的内容是否默认进侧边栏树状目录
        /// </summary>
        public static bool ShouldShowInSidebarByDefault(string type)
        {
            return type switch
            {
                Note => true,       // 只有长文随笔/博客默认进侧边栏
                _ => false          // 简语、艺术品、视频默认不进侧边栏，防止杂乱
            };
        }
    }
}