from pathlib import Path
p=Path('src/App.vue'); s=p.read_text(encoding='utf-8-sig').replace('const handleNavigation = (item: Plugin)', 'const handleNavigation = (item: Pick<Plugin, \'name\' | \'url\'>)'); p.write_text(s,encoding='utf-8')
root=Path('E:/TaiChuHuanYu_DeskTop/TaiChuHuanYu-Desktop/TaiChuBackend_V2/tests/LingMai.Regression')
stage=Path('.tmp/repair-theme/backend')
p=root/'TestContext.cs'; s=p.read_text(encoding='utf-8-sig'); (stage/'TestContext.cs.before').write_text(s,encoding='utf-8')
s=s.replace('=> Inner.Execute(expression);','=> Inner.Execute(new TestCollationVisitor().Visit(expression));').replace('=> Inner.Execute<TResult>(expression);','=> Inner.Execute<TResult>(new TestCollationVisitor().Visit(expression));').replace('else result = Inner.Execute(expression);','else result = Inner.Execute(new TestCollationVisitor().Visit(expression));')
s+='''
// 内存替身只验证权限与结果；排序规则的 SQL 生成由 ContentFlowChecks 验证。
sealed class TestCollationVisitor : ExpressionVisitor
{
    protected override Expression VisitMethodCall(MethodCallExpression node) =>
        node.Method.DeclaringType == typeof(RelationalDbFunctionsExtensions) && node.Method.Name == nameof(RelationalDbFunctionsExtensions.Collate)
            ? Visit(node.Arguments[1]) : base.VisitMethodCall(node);
}
'''; (stage/'TestContext.cs').write_text(s,encoding='utf-8')
p=root/'ContentFlowChecks.cs'; s=p.read_text(encoding='utf-8-sig'); (stage/'ContentFlowChecks.cs.before').write_text(s,encoding='utf-8')
s=s.replace('            Check(sql.Contains("LIMIT"), "catalog database pagination " + type);','''            Check(sql.Contains("LIMIT"), "catalog database pagination " + type);
            if (type is "blog" or "post" or "all") {
                Check(sql.Contains("COLLATE utf8mb4_general_ci"), "catalog legacy collation compatibility " + type);
                Check(sql.Contains("IsPublic") && sql.Contains("Status"), "catalog retains source privacy " + type);
            }''')
s=s.replace('        Check(WikiController.ValidContent', '''        // C# 14 将数组扩展 Contains 绑定到 ReadOnlySpan，EF 9 无法求值；使用 Enumerable 重载。
        var artworkIds = new[] { 1, 2 };
        var gallerySql = new ArtworkEngagementService(db).Stats(null).Where(a => Enumerable.Contains(artworkIds, a.Id)).ToQueryString();
        Check(gallerySql.Contains(" IN (1, 2)"), "gallery ID filter translates on net10");
        foreach (var tagIds in new[] { Array.Empty<string>(), new[] { "1", "2" } }) {
            var tagSql = db.TagAssignments.Where(t => t.EntityType == "Artwork" && Enumerable.Contains(tagIds, t.EntityId)).ToQueryString();
            Check(tagSql.Length > 0, "catalog tag IDs translate, including wiki-only empty IDs");
        }
        Check(WikiController.ValidContent''',1)
(stage/'ContentFlowChecks.cs').write_text(s,encoding='utf-8')
