using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Services
{
    public class SystemConfigService
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private const string CACHE_KEY = "SystemConfigs";

        public SystemConfigService(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<string> GetValueAsync(string key, string defaultValue = "")
        {
            var dict = await GetConfigDictionaryAsync();
            return dict.TryGetValue(key, out string value) ? value : defaultValue;
        }

        public async Task<int> GetIntAsync(string key, int defaultValue = 0)
        {
            var value = await GetValueAsync(key);
            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        private async Task<Dictionary<string, string>> GetConfigDictionaryAsync()
        {
            if (_cache.TryGetValue(CACHE_KEY, out Dictionary<string, string> cached))
                return cached;

            // ✅ 先查询列表，再同步转字典（避免 ToDictionaryAsync 类型推断问题）
            var configsList = await _context.SystemConfigs.ToListAsync();
            var configs = configsList.ToDictionary(c => c.Key, c => c.Value);

            _cache.Set(CACHE_KEY, configs, TimeSpan.FromMinutes(10));
            return configs;
        }

        public void ClearCache()
        {
            _cache.Remove(CACHE_KEY);
        }
    }
}