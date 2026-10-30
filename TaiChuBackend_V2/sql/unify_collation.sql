-- 统一数据库排序规则（collation）为 utf8mb4_general_ci
-- 背景：PublishedNotes 等少数表是 utf8mb4_0900_ai_ci，与其他表的 utf8mb4_general_ci 不一致，
--       跨表比较列时 MySQL 会报 “Illegal mix of collations”。代码里已经绕开了，这个脚本从根上统一。
--
-- 使用步骤（请在低峰期执行，执行前务必先备份整个数据库）：
--   1. 运行【第一步】，查看哪些表/列不是 utf8mb4_general_ci。
--   2. 运行【第二步】，它会生成一组 ALTER TABLE 语句（不会直接修改任何东西）。
--   3. 检查生成的语句，确认无误后复制出来逐条执行。
--   4. 运行【第三步】，确认结果为空。
-- 注意：CONVERT TO 会重建整张表，大表执行期间会锁表；只影响文本列，Guid(char(36)) 列内容不变。

-- 【第一步】列出排序规则不一致的表和列
SELECT TABLE_NAME, COLUMN_NAME, COLLATION_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND COLLATION_NAME IS NOT NULL
  AND COLLATION_NAME <> 'utf8mb4_general_ci'
ORDER BY TABLE_NAME, ORDINAL_POSITION;

-- 【第二步】生成转换语句（只生成，不执行）
SELECT DISTINCT CONCAT('ALTER TABLE `', TABLE_NAME, '` CONVERT TO CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;') AS sql_to_run
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND COLLATION_NAME IS NOT NULL
  AND COLLATION_NAME <> 'utf8mb4_general_ci';

-- 同时把数据库默认排序规则改掉，以后新建的表也保持一致（把 your_database 换成实际库名）：
-- ALTER DATABASE `your_database` CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;

-- 【第三步】复查：结果为空即表示已统一
SELECT TABLE_NAME, COLUMN_NAME, COLLATION_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND COLLATION_NAME IS NOT NULL
  AND COLLATION_NAME <> 'utf8mb4_general_ci';
