using System.Reflection;

var assembly = Assembly.LoadFrom("src/FluentStorage.Github/bin/Debug/net8.0/Olbrasoft.FluentStorage.Github.dll");
var type = assembly.GetType("Olbrasoft.FluentStorage.Github.GitHubBlobStorage");

Console.WriteLine("Constructors found:");
foreach (var ctor in type.GetConstructors())
{
    Console.WriteLine($"  {ctor}");
    foreach (var param in ctor.GetParameters())
    {
        Console.WriteLine($"    - {param.ParameterType.Name} {param.Name}");
    }
    Console.WriteLine();
}
