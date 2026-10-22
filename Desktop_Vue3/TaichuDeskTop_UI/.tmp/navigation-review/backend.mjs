import fs from 'node:fs';
const source='../../TaiChuBackend_V2/TaiChuWeb_V2/TaiChuWeb_V2/Controllers/LingMai/LingMaiPublishController.cs';
let code=fs.readFileSync(source,'utf8').replace(/\r\n/g,'\n');
fs.writeFileSync('.tmp/navigation-review/LingMaiPublishController.cs.before',code);
code=code.replace('[FromQuery] int pageSize = 20)', '[FromQuery] int pageSize = 20,\n            [FromQuery] string? tag = null)');
code=code.replace('            var safePageSize = Math.Clamp(pageSize, 1, 50);',`            if (!string.IsNullOrWhiteSpace(tag))
            {
                tag = tag.Trim();
                if (tag.Length > 50) return BadRequest(new { message = "分类标签不能超过 50 字" });
                // 使用发布时的标签快照，兼容 Unicode 转义和未转义的旧 JSON。
                var encoded = JsonSerializer.Serialize(tag);
                var literal = JsonSerializer.Serialize(tag, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                query = query.Where(pn => pn.Tags != null && (pn.Tags.Contains(encoded) || pn.Tags.Contains(literal) || pn.Tags == tag));
            }

            var safePageSize = Math.Clamp(pageSize, 1, 50);`);
fs.writeFileSync('.tmp/navigation-review/LingMaiPublishController.cs',code);
