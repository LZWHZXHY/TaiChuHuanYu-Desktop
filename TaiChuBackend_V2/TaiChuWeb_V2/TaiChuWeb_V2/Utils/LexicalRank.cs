// Utils/LexicalRank.cs
namespace TaiChuWeb_V2.Utils
{
    public static class LexicalRank
    {
        private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";


        public static string GetMiddle(string prev = "", string next = "")
        {
            if (string.IsNullOrEmpty(prev)) prev = Alphabet[0].ToString();
            if (string.IsNullOrEmpty(next)) next = Alphabet[^1].ToString();

            int minLen = Math.Min(prev.Length, next.Length);
            int i = 0;

            // 找到第一个不相等的字符
            while (i < minLen && prev[i] == next[i]) i++;

            int charPrev = i < prev.Length ? Alphabet.IndexOf(prev[i]) : 0;
            int charNext = i < next.Length ? Alphabet.IndexOf(next[i]) : Alphabet.Length - 1;

            if (charNext - charPrev > 1)
            {
                // 中间还有空间，取中值
                return prev.Substring(0, i) + Alphabet[(charPrev + charNext) / 2];
            }
            else
            {
                // 中间没空间了，延长字符串
                return prev + Alphabet[Alphabet.Length / 2];
            }
        }
    }
}