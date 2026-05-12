// TaiChuWeb_V2/Models/LingMai/NoteTypes.cs
namespace TaiChuWeb_V2.Models.LingMai
{
    public static class NoteTypes
    {
        public const string Note = "note";         // 基础笔记、长文随笔、博客
        public const string Thought = "thought";   // 短动态、简语
        public const string Art = "art";           // 艺术作品、画廊
        public const string Video = "video";       // 视频内容
        public const string Audio = "audio";       // 声音/音乐
        public const string Event = "event";       // 世界事件
        public const string Character = "char";    // 角色档案
        public const string Wiki = "wiki";         // 世界观设定/百科
        public const string Community = "community"; // 🌟 新增：社区规则、教程、公共知识

        public static bool IsValid(string type)
        {
            return type switch
            {
                Note or Thought or Art or Video or Audio
                or Event or Character or Wiki or Community => true, // 🌟 包含新类型
                _ => false
            };
        }

        /// <summary>
        /// 🌟 优化后的规则：百科类和随笔类默认进侧边栏，方便结构化查阅
        /// </summary>
        public static bool ShouldShowInSidebarByDefault(string type)
        {
            return type switch
            {
                Note or Wiki or Character or Community => true, // 🌟 百科类内容必须进目录树
                _ => false
            };
        }
    }
}