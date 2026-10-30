using System.Security.Cryptography;
using MySqlConnector;
using TaiChuWeb_V2.Services.Chat;

namespace TaiChuWeb_V2.Services.Paint
{
    public sealed record PaintBrushRow(Guid Id, Guid OwnerId, string Name, PaintBrushParams Params, bool IsPublic, int Favorites, DateTime CreatedAt, DateTime UpdatedAt, bool Favorited);

    /// <summary>
    /// 茶绘笔刷库：自己做的笔刷（跟账号保存）、笔刷广场（公开的笔刷）、收藏、笔尖图。
    /// 数据表 paint_brushes / paint_brush_favorites / paint_brush_tips 第一次使用时自动创建。
    /// 笔尖图按内容的 SHA-256 存，内容不变地址就不变，所以可以长期缓存。
    /// </summary>
    public sealed class PaintBrushService(IConfiguration configuration)
    {
        public const int MaxBrushesPerUser = 60;
        public const int MaxTipBytes = 96 * 1024;

        private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        private readonly SemaphoreSlim _schemaLock = new(1, 1);
        private volatile bool _ready;

        private async Task<MySqlConnection> OpenAsync(CancellationToken ct = default)
        {
            if (!_ready) await EnsureSchemaAsync(ct);
            var c = new MySqlConnection(_connectionString);
            await c.OpenAsync(ct);
            return c;
        }

        private async Task EnsureSchemaAsync(CancellationToken ct)
        {
            await _schemaLock.WaitAsync(ct);
            try
            {
                if (_ready) return;
                await using var c = new MySqlConnection(_connectionString);
                await c.OpenAsync(ct);
                await using var cmd = c.CreateCommand();
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS paint_brushes (
  Id CHAR(36) NOT NULL PRIMARY KEY,
  OwnerId CHAR(36) NOT NULL,
  Name VARCHAR(40) NOT NULL,
  ParamsJson TEXT NOT NULL,
  IsPublic TINYINT(1) NOT NULL DEFAULT 0,
  Favorites INT NOT NULL DEFAULT 0,
  CreatedAt DATETIME NOT NULL,
  UpdatedAt DATETIME NOT NULL,
  KEY IX_paint_brushes_owner (OwnerId, UpdatedAt),
  KEY IX_paint_brushes_public (IsPublic, Favorites),
  KEY IX_paint_brushes_public_new (IsPublic, CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS paint_brush_favorites (
  UserId CHAR(36) NOT NULL,
  BrushId CHAR(36) NOT NULL,
  CreatedAt DATETIME NOT NULL,
  PRIMARY KEY (UserId, BrushId),
  KEY IX_paint_brush_favorites_brush (BrushId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS paint_brush_tips (
  Hash CHAR(64) NOT NULL PRIMARY KEY,
  Png MEDIUMBLOB NOT NULL,
  CreatedAt DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                await cmd.ExecuteNonQueryAsync(ct);
                _ready = true;
            }
            finally { _schemaLock.Release(); }
        }

        private const string Columns = "b.Id, b.OwnerId, b.Name, b.ParamsJson, b.IsPublic, b.Favorites, b.CreatedAt, b.UpdatedAt";

        private static PaintBrushRow Read(MySqlDataReader r, bool favorited) => new(
            ChatStore.ReadGuid(r, 0), ChatStore.ReadGuid(r, 1), r.GetString(2),
            PaintBrushParams.FromJson(r.GetString(3)) ?? new PaintBrushParams(),
            r.GetBoolean(4), r.GetInt32(5), r.GetDateTime(6), r.GetDateTime(7), favorited);

        public static string? CheckName(string? name, out string clean)
        {
            clean = (name ?? "").Trim();
            if (clean.Length == 0) return "给笔刷起个名字吧";
            if (clean.Length > 20) return "名字最多 20 个字";
            return null;
        }

        /// <summary>自己做的 + 收藏的（收藏的只算还公开着的）</summary>
        public async Task<List<PaintBrushRow>> MineAsync(Guid u, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            var list = new List<PaintBrushRow>();
            await using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = $"SELECT {Columns} FROM paint_brushes b WHERE b.OwnerId=@u ORDER BY b.CreatedAt";
                cmd.Parameters.AddWithValue("@u", u.ToString());
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct)) list.Add(Read(r, false));
            }
            await using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = $"SELECT {Columns} FROM paint_brush_favorites f JOIN paint_brushes b ON b.Id=f.BrushId WHERE f.UserId=@u AND b.IsPublic=1 AND b.OwnerId<>@u ORDER BY f.CreatedAt";
                cmd.Parameters.AddWithValue("@u", u.ToString());
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct)) list.Add(Read(r, true));
            }
            return list;
        }

        /// <summary>笔刷广场：sort = hot（收藏最多）/ new（最新）</summary>
        public async Task<(List<PaintBrushRow> Items, int Total)> SquareAsync(Guid? u, string? sort, string? q, int offset, int limit, CancellationToken ct = default)
        {
            offset = Math.Max(0, offset);
            limit = Math.Clamp(limit, 1, 60);
            // 搜索词可以是名字的一部分，也可以是笔刷编号（知识库引用链接用编号）
            var where = "b.IsPublic=1" + (string.IsNullOrWhiteSpace(q) ? "" : " AND (b.Name LIKE @q OR b.Id = @qid)");
            var order = sort == "new" ? "b.CreatedAt DESC" : "b.Favorites DESC, b.CreatedAt DESC";
            await using var c = await OpenAsync(ct);
            int total;
            await using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = $"SELECT COUNT(*) FROM paint_brushes b WHERE {where}";
                if (!string.IsNullOrWhiteSpace(q)) { cmd.Parameters.AddWithValue("@q", $"%{q.Trim()}%"); cmd.Parameters.AddWithValue("@qid", q.Trim()); }
                total = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            }
            var list = new List<PaintBrushRow>();
            await using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = $@"SELECT {Columns}, {(u == null ? "0" : "EXISTS(SELECT 1 FROM paint_brush_favorites f WHERE f.UserId=@u AND f.BrushId=b.Id)")}
FROM paint_brushes b WHERE {where} ORDER BY {order} LIMIT @l OFFSET @o";
                if (u != null) cmd.Parameters.AddWithValue("@u", u.Value.ToString());
                if (!string.IsNullOrWhiteSpace(q)) { cmd.Parameters.AddWithValue("@q", $"%{q.Trim()}%"); cmd.Parameters.AddWithValue("@qid", q.Trim()); }
                cmd.Parameters.AddWithValue("@l", limit);
                cmd.Parameters.AddWithValue("@o", offset);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct)) list.Add(Read(r, Convert.ToInt64(r.GetValue(8)) != 0));
            }
            return (list, total);
        }

        private static async Task<PaintBrushRow?> GetAsync(MySqlConnection c, Guid id, CancellationToken ct)
        {
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {Columns} FROM paint_brushes b WHERE b.Id=@id";
            cmd.Parameters.AddWithValue("@id", id.ToString());
            await using var r = await cmd.ExecuteReaderAsync(ct);
            return await r.ReadAsync(ct) ? Read(r, false) : null;
        }

        public async Task<(PaintBrushRow? Row, string? Error)> CreateAsync(Guid u, string? name, PaintBrushParams? p, bool isPublic, CancellationToken ct = default)
        {
            if (CheckName(name, out var clean) is { } err) return (null, err);
            if (p == null) return (null, "笔刷参数不对");
            await using var c = await OpenAsync(ct);
            await using (var count = c.CreateCommand())
            {
                count.CommandText = "SELECT COUNT(*) FROM paint_brushes WHERE OwnerId=@u";
                count.Parameters.AddWithValue("@u", u.ToString());
                if (Convert.ToInt32(await count.ExecuteScalarAsync(ct)) >= MaxBrushesPerUser) return (null, $"每人最多保存 {MaxBrushesPerUser} 支笔刷");
            }
            var id = Guid.NewGuid();
            var now = DateTime.UtcNow;
            await using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "INSERT INTO paint_brushes (Id, OwnerId, Name, ParamsJson, IsPublic, Favorites, CreatedAt, UpdatedAt) VALUES (@id,@u,@n,@p,@pub,0,@t,@t)";
                cmd.Parameters.AddWithValue("@id", id.ToString());
                cmd.Parameters.AddWithValue("@u", u.ToString());
                cmd.Parameters.AddWithValue("@n", clean);
                cmd.Parameters.AddWithValue("@p", p.Normalized().ToJson());
                cmd.Parameters.AddWithValue("@pub", isPublic);
                cmd.Parameters.AddWithValue("@t", now);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            return (await GetAsync(c, id, ct), null);
        }

        public async Task<(PaintBrushRow? Row, string? Error)> UpdateAsync(Guid u, Guid id, string? name, PaintBrushParams? p, bool isPublic, CancellationToken ct = default)
        {
            if (CheckName(name, out var clean) is { } err) return (null, err);
            if (p == null) return (null, "笔刷参数不对");
            await using var c = await OpenAsync(ct);
            var row = await GetAsync(c, id, ct);
            if (row == null) return (null, "笔刷不存在");
            if (row.OwnerId != u) return (null, "只能改自己的笔刷");
            await using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "UPDATE paint_brushes SET Name=@n, ParamsJson=@p, IsPublic=@pub, UpdatedAt=@t WHERE Id=@id";
                cmd.Parameters.AddWithValue("@id", id.ToString());
                cmd.Parameters.AddWithValue("@n", clean);
                cmd.Parameters.AddWithValue("@p", p.Normalized().ToJson());
                cmd.Parameters.AddWithValue("@pub", isPublic);
                cmd.Parameters.AddWithValue("@t", DateTime.UtcNow);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            return (await GetAsync(c, id, ct), null);
        }

        public async Task<string?> DeleteAsync(Guid u, Guid id, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            var row = await GetAsync(c, id, ct);
            if (row == null) return "笔刷不存在";
            if (row.OwnerId != u) return "只能删除自己的笔刷";
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM paint_brush_favorites WHERE BrushId=@id; DELETE FROM paint_brushes WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
            return null;
        }

        /// <summary>收藏 / 取消收藏（只能收藏别人公开的笔刷），返回新的收藏数</summary>
        public async Task<(int Favorites, string? Error)> FavoriteAsync(Guid u, Guid id, bool on, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            var row = await GetAsync(c, id, ct);
            if (row == null) return (0, "笔刷不存在");
            if (on && row.OwnerId == u) return (row.Favorites, "这是你自己的笔刷");
            if (on && !row.IsPublic) return (row.Favorites, "这支笔刷没有公开");
            await using var tx = await c.BeginTransactionAsync(ct);
            int changed;
            await using (var cmd = c.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = on
                    ? "INSERT IGNORE INTO paint_brush_favorites (UserId, BrushId, CreatedAt) VALUES (@u,@id,@t)"
                    : "DELETE FROM paint_brush_favorites WHERE UserId=@u AND BrushId=@id";
                cmd.Parameters.AddWithValue("@u", u.ToString());
                cmd.Parameters.AddWithValue("@id", id.ToString());
                cmd.Parameters.AddWithValue("@t", DateTime.UtcNow);
                changed = await cmd.ExecuteNonQueryAsync(ct);
            }
            if (changed > 0)
            {
                await using var cmd = c.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = $"UPDATE paint_brushes SET Favorites = GREATEST(0, Favorites {(on ? "+" : "-")} 1) WHERE Id=@id";
                cmd.Parameters.AddWithValue("@id", id.ToString());
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
            return (Math.Max(0, row.Favorites + (changed > 0 ? (on ? 1 : -1) : 0)), null);
        }

        // ================= 笔尖图 =================

        /// <summary>保存笔尖图（PNG），返回内容哈希。已经有同样的图就直接返回</summary>
        public async Task<(string? Hash, string? Error)> SaveTipAsync(byte[] png, CancellationToken ct = default)
        {
            if (png.Length < 8 || png[0] != 0x89 || png[1] != 0x50 || png[2] != 0x4E || png[3] != 0x47) return (null, "笔尖图只接受 PNG");
            if (png.Length > MaxTipBytes) return (null, "笔尖图太大了");
            var hash = Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "INSERT IGNORE INTO paint_brush_tips (Hash, Png, CreatedAt) VALUES (@h,@p,@t)";
            cmd.Parameters.AddWithValue("@h", hash);
            cmd.Parameters.AddWithValue("@p", png);
            cmd.Parameters.AddWithValue("@t", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync(ct);
            return (hash, null);
        }

        public async Task<byte[]?> GetTipAsync(string hash, CancellationToken ct = default)
        {
            if (!PaintBrushParams.IsHash(hash)) return null;
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT Png FROM paint_brush_tips WHERE Hash=@h";
            cmd.Parameters.AddWithValue("@h", hash);
            return await cmd.ExecuteScalarAsync(ct) as byte[];
        }
    }
}
