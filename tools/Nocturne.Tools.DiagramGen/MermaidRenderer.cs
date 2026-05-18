using Microsoft.EntityFrameworkCore.Metadata;

namespace Nocturne.Tools.DiagramGen;

/// <summary>
/// Renders filtered EF Core model metadata as mermaid erDiagram syntax.
/// </summary>
public static class MermaidRenderer
{
    public static string RenderModule(IModel model, string moduleName, string[] entityNames)
    {
        // Build reverse lookup for labeling external entities
        var entityToModule = new Dictionary<string, string>();
        foreach (var (modName, modEntities) in ModuleMap.Modules)
        {
            foreach (var name in modEntities)
                entityToModule[name] = modName;
        }

        var entitySet = new HashSet<string>(entityNames);
        var entities = model.GetEntityTypes()
            .Where(e => !e.IsOwned() && entitySet.Contains(e.ClrType.Name))
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("erDiagram");

        // Track external entities referenced by cross-module FKs
        var externalEntities = new HashSet<string>();

        foreach (var entity in entities)
        {
            foreach (var fk in entity.GetForeignKeys())
            {
                var principal = fk.PrincipalEntityType;
                if (principal.IsOwned()) continue;

                var fromName = SanitizeName(entity.ClrType.Name);
                var toName = SanitizeName(principal.ClrType.Name);
                var isUnique = fk.IsUnique;
                var isRequired = fk.IsRequired;

                var leftCardinality = isUnique ? "||" : "}o";
                var rightCardinality = isRequired ? "||" : "o|";

                if (entitySet.Contains(principal.ClrType.Name))
                {
                    // Intra-module relationship
                    sb.AppendLine($"    {toName} {rightCardinality}--{leftCardinality} {fromName} : \"\"");
                }
                else
                {
                    // Cross-module relationship — render with dotted line
                    externalEntities.Add(principal.ClrType.Name);
                    sb.AppendLine($"    {toName} {rightCardinality}..{leftCardinality} {fromName} : \"\"");
                }
            }
        }

        sb.AppendLine();

        // Render full entity blocks for module members
        foreach (var entity in entities)
        {
            var name = SanitizeName(entity.ClrType.Name);
            sb.AppendLine($"    {name} {{");

            foreach (var property in entity.GetProperties())
            {
                if (property.IsShadowProperty()) continue;

                var typeName = GetMermaidType(property);
                var propName = property.Name;
                var markers = new List<string>();

                if (property.IsPrimaryKey()) markers.Add("PK");
                if (property.IsForeignKey()) markers.Add("FK");

                var markerStr = markers.Count > 0 ? $" \"{string.Join(",", markers)}\"" : "";
                sb.AppendLine($"        {typeName} {propName}{markerStr}");
            }

            sb.AppendLine("    }");
        }

        // Render stub blocks for external entities (cross-module references)
        foreach (var extName in externalEntities.OrderBy(n => n))
        {
            var sanitized = SanitizeName(extName);
            var extModule = entityToModule.TryGetValue(extName, out var m) ? ModuleMap.Titles[m] : "external";
            sb.AppendLine($"    {sanitized} {{");
            sb.AppendLine($"        string _module \"{extModule}\"");
            sb.AppendLine("    }");
        }

        return sb.ToString();
    }

    public static string RenderOverview(IModel model)
    {
        var entityToModule = new Dictionary<string, string>();
        foreach (var (moduleName, entityNames) in ModuleMap.Modules)
        {
            foreach (var name in entityNames)
            {
                entityToModule[name] = moduleName;
            }
        }

        var crossModuleEdges = new HashSet<(string from, string to)>();

        foreach (var entityType in model.GetEntityTypes())
        {
            if (entityType.IsOwned()) continue;
            if (!entityToModule.TryGetValue(entityType.ClrType.Name, out var fromModule)) continue;

            foreach (var fk in entityType.GetForeignKeys())
            {
                var principal = fk.PrincipalEntityType;
                if (principal.IsOwned()) continue;
                if (!entityToModule.TryGetValue(principal.ClrType.Name, out var toModule)) continue;
                if (fromModule == toModule) continue;

                var edge = string.Compare(fromModule, toModule, StringComparison.Ordinal) < 0
                    ? (fromModule, toModule)
                    : (toModule, fromModule);
                crossModuleEdges.Add(edge);
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("graph LR");

        foreach (var (moduleName, entityNames) in ModuleMap.Modules)
        {
            var title = ModuleMap.Titles[moduleName];
            var count = entityNames.Length;
            var nodeId = SanitizeName(moduleName);
            sb.AppendLine($"    {nodeId}[\"{title}<br/><small>{count} entities</small>\"]");
        }

        sb.AppendLine();

        foreach (var (from, to) in crossModuleEdges.OrderBy(e => e.from))
        {
            sb.AppendLine($"    {SanitizeName(from)} --- {SanitizeName(to)}");
        }

        return sb.ToString();
    }

    private static string SanitizeName(string name)
    {
        return name
            .Replace("Entity", "")
            .Replace("-", "_");
    }

    private static string GetMermaidType(IProperty property)
    {
        var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

        return clrType switch
        {
            _ when clrType == typeof(Guid) => "uuid",
            _ when clrType == typeof(string) => "string",
            _ when clrType == typeof(int) => "int",
            _ when clrType == typeof(long) => "bigint",
            _ when clrType == typeof(bool) => "bool",
            _ when clrType == typeof(DateTime) => "datetime",
            _ when clrType == typeof(DateTimeOffset) => "datetimeoffset",
            _ when clrType == typeof(decimal) => "decimal",
            _ when clrType == typeof(double) => "double",
            _ when clrType == typeof(float) => "float",
            _ when clrType == typeof(byte[]) => "bytea",
            _ when clrType == typeof(TimeSpan) => "interval",
            _ when clrType == typeof(DateOnly) => "date",
            _ when clrType == typeof(TimeOnly) => "time",
            _ when clrType.IsEnum => "enum",
            _ => "object",
        };
    }
}
