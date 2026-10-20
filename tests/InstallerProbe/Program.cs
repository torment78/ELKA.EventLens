File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "probe-started.txt"), DateTime.UtcNow.ToString("O"));
if (args.Contains("--hold")) Thread.Sleep(TimeSpan.FromMinutes(2));
