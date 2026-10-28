try { Console.WriteLine($"PASS {await RecoveryChecks.Run()} recovery and authorization checks"); }
catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
