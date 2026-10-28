try { var count = await WorldDraftChecks.Run(); Console.WriteLine($"World draft checks passed: {count}"); return 0; }
catch (Exception error) { Console.Error.WriteLine(error); return 1; }
