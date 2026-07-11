using System.CommandLine;

var root = new RootCommand("SqlBound: compile-time verified SQL for .NET.");
return root.Parse(args).Invoke();
