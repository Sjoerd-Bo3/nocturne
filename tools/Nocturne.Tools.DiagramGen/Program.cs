using DbToMermaid;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Nocturne.Infrastructure.Data;
using Nocturne.Tools.DiagramGen;

var optionsBuilder = new DbContextOptionsBuilder<NocturneDbContext>();
optionsBuilder.UseNpgsql("Host=localhost"); // connection never opened

await using var context = new NocturneDbContext(optionsBuilder.Options);

// Parse args: [outputPath] [--module <name>]
var outputPath = "docs/diagrams/er-diagram.mmd";
string? moduleName = null;

for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--module" && i + 1 < args.Length)
    {
        moduleName = args[++i];
    }
    else if (!args[i].StartsWith('-'))
    {
        outputPath = args[i];
    }
}

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

if (moduleName == null)
{
    // Full monolithic diagram via EfToMermaid
    await EfToMermaid.RenderToFile(context.Model, outputPath);
    Console.WriteLine($"Full ER diagram written to {outputPath}");
}
else if (moduleName == "overview")
{
    var mermaid = MermaidRenderer.RenderOverview(context.Model);
    await File.WriteAllTextAsync(outputPath, mermaid);
    Console.WriteLine($"Module overview diagram written to {outputPath}");
}
else
{
    if (!ModuleMap.Modules.TryGetValue(moduleName, out var entityNames))
    {
        Console.Error.WriteLine($"Unknown module: {moduleName}");
        Console.Error.WriteLine($"Available: {string.Join(", ", ModuleMap.Modules.Keys)}");
        Environment.Exit(1);
        return;
    }

    var mermaid = MermaidRenderer.RenderModule(context.Model, moduleName, entityNames);
    await File.WriteAllTextAsync(outputPath, mermaid);
    Console.WriteLine($"Module '{moduleName}' ER diagram written to {outputPath}");
}
