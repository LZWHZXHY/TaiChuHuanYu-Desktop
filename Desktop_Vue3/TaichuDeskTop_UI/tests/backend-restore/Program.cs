using System.Text.Json;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

try
{
    var root = Path.GetFullPath(args[1]);
    var config = new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json",true).Build();
    if (args[0] == "setup")
    {
        await using var connection = new MySqlConnection(config.GetConnectionString("DefaultConnection"));
        await connection.OpenAsync();
        await using var history = connection.CreateCommand();
        history.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = '20261026033651_AddWorldRestore'";
        if (Convert.ToInt32(await history.ExecuteScalarAsync()) == 0)
        {
            var sql = await File.ReadAllTextAsync(Path.Combine(root,"Migrations","WorldRestore.sql"));
            if (sql.Contains("DROP ",StringComparison.OrdinalIgnoreCase) || sql.Contains("UPDATE ",StringComparison.OrdinalIgnoreCase) || sql.Contains("ALTER ",StringComparison.OrdinalIgnoreCase)) throw new Exception("Unexpected migration SQL");
            await using var command = connection.CreateCommand(); command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
        Console.WriteLine("WorldRestores schema ready; existing tables and data unchanged.");
    }
    else if (args[0] == "cloud-check")
    {
        var source = JsonDocument.Parse(await File.ReadAllTextAsync(args[2]));
        var jobId = source.RootElement.GetProperty("id").GetGuid();
        var image = source.RootElement.GetProperty("images").EnumerateObject().Single().Name.Split('/').Last();
        if (!System.Text.RegularExpressions.Regex.IsMatch(image,@"^[a-f0-9]{64}\.png$")) throw new Exception("Unexpected test image");
        var service = new TaiChuWeb_V2.Services.Cos.CosService(config);
        var client = (COSXML.CosXml)typeof(TaiChuWeb_V2.Services.Cos.CosService).GetField("_cosXml",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(service)!;
        try { var result=client.HeadObject(new COSXML.Model.Object.HeadObjectRequest(config["TencentCloud:COS:Bucket"],$"world/restores/{jobId:N}/{image}")); Console.WriteLine(JsonSerializer.Serialize(new {restoreId=jobId,originStatus=result.httpCode,deleted=result.httpCode==404})); if(result.httpCode!=404)Environment.ExitCode=1; }
        catch(COSXML.CosException.CosServerException ex) { var code=ex.GetType().GetMethod("GetStatusCode")?.Invoke(ex,null); Console.WriteLine(JsonSerializer.Serialize(new {restoreId=jobId,originStatus=code,deleted=Convert.ToInt32(code)==404})); if(Convert.ToInt32(code)!=404)Environment.ExitCode=1; }
    }
    else await RestoreChecks.Run(config,Guid.Parse(args[2]));
}
catch(Exception ex) { Console.Error.WriteLine(ex.ToString()); Environment.ExitCode=1; }

