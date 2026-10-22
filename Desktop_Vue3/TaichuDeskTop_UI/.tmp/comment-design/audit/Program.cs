using MySqlConnector;
using System.Text.Json;
var folder = args[0];
string? connection = null;
foreach (var file in new[] { "appsettings.json", "appsettings.Development.json" }) {
 var path=Path.Combine(folder,file); if(!File.Exists(path))continue;
 using var json=JsonDocument.Parse(File.ReadAllText(path),new JsonDocumentOptions{CommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true});
 if(json.RootElement.TryGetProperty("ConnectionStrings",out var cs)&&cs.TryGetProperty("DefaultConnection",out var v))connection=v.GetString();
}
await using var db=new MySqlConnection(connection); await db.OpenAsync();
foreach(var sql in new[]{
 "SELECT p.Type, CASE WHEN p.OriginalNoteId IS NULL THEN 'legacy-null' WHEN n.Id IS NULL THEN 'source-missing' WHEN n.Status=2 THEN 'banned' WHEN n.IsPublic=0 THEN 'not-public' ELSE 'visible' END state, COUNT(*) count FROM PublishedNotes p LEFT JOIN notes n ON p.OriginalNoteId COLLATE utf8mb4_general_ci=n.Id COLLATE utf8mb4_general_ci GROUP BY p.Type,state",
 "SELECT Type,IsPublic,Status,COUNT(*) count FROM notes GROUP BY Type,IsPublic,Status",
 "SELECT Status,IsApproved,COUNT(*) count FROM Artworks GROUP BY Status,IsApproved"
}) {
 await using var command=new MySqlCommand(sql,db);await using var reader=await command.ExecuteReaderAsync();
 var rows=new List<Dictionary<string,object?>>();while(await reader.ReadAsync()){var row=new Dictionary<string,object?>();for(int i=0;i<reader.FieldCount;i++)row[reader.GetName(i)]=reader.IsDBNull(i)?null:reader.GetValue(i);rows.Add(row);}Console.WriteLine(JsonSerializer.Serialize(rows));
}
