namespace Nocturne.Core.Models.V4;

/// <summary>
/// Static catalog of known insulin formulations with default pharmacokinetic properties.
/// </summary>
public static class InsulinCatalog
{
    private static readonly IReadOnlyList<InsulinFormulation> _formulations =
    [
        // Rapid-acting
        new() { Id = "humalog",      Name = "Humalog (Insulin Lispro)",      Category = InsulinCategory.RapidActing, DefaultDia = 4.0, DefaultPeak = 75,  Curve = "rapid-acting", Concentration = 100 },
        new() { Id = "humalog-u200", Name = "Humalog U200 (Insulin Lispro)", Category = InsulinCategory.RapidActing, DefaultDia = 4.0, DefaultPeak = 75,  Curve = "rapid-acting", Concentration = 200 },
        new() { Id = "novorapid",    Name = "NovoRapid (Insulin Aspart)",    Category = InsulinCategory.RapidActing, DefaultDia = 4.0, DefaultPeak = 75,  Curve = "rapid-acting", Concentration = 100 },
        new() { Id = "apidra",       Name = "Apidra (Insulin Glulisine)",    Category = InsulinCategory.RapidActing, DefaultDia = 4.0, DefaultPeak = 75,  Curve = "rapid-acting", Concentration = 100 },
        new() { Id = "fiasp",        Name = "Fiasp (Faster Aspart)",         Category = InsulinCategory.RapidActing, DefaultDia = 3.5, DefaultPeak = 55,  Curve = "ultra-rapid",  Concentration = 100 },
        new() { Id = "lyumjev",      Name = "Lyumjev (URLi Lispro)",         Category = InsulinCategory.RapidActing, DefaultDia = 3.5, DefaultPeak = 55,  Curve = "ultra-rapid",  Concentration = 100 },
        new() { Id = "lyumjev-u200", Name = "Lyumjev U200 (URLi Lispro)",    Category = InsulinCategory.RapidActing, DefaultDia = 3.5, DefaultPeak = 55,  Curve = "ultra-rapid",  Concentration = 200 },

        // Short-acting
        new() { Id = "humulin-r",      Name = "Humulin R (Regular)",      Category = InsulinCategory.ShortActing, DefaultDia = 5.0, DefaultPeak = 90,  Curve = "bilinear", Concentration = 100 },
        new() { Id = "humulin-r-u500", Name = "Humulin R U500 (Regular)", Category = InsulinCategory.ShortActing, DefaultDia = 5.0, DefaultPeak = 90,  Curve = "bilinear", Concentration = 500 },
        new() { Id = "actrapid",       Name = "Actrapid (Regular)",       Category = InsulinCategory.ShortActing, DefaultDia = 5.0, DefaultPeak = 90,  Curve = "bilinear", Concentration = 100 },

        // Long-acting
        new() { Id = "lantus",  Name = "Lantus (Insulin Glargine)", Category = InsulinCategory.LongActing, DefaultDia = 24.0, DefaultPeak = 480, Curve = "bilinear", Concentration = 100 },
        new() { Id = "levemir", Name = "Levemir (Insulin Detemir)", Category = InsulinCategory.LongActing, DefaultDia = 18.0, DefaultPeak = 420, Curve = "bilinear", Concentration = 100 },

        // Ultra-long-acting
        new() { Id = "tresiba",      Name = "Tresiba (Insulin Degludec)",      Category = InsulinCategory.UltraLongActing, DefaultDia = 42.0, DefaultPeak = 660, Curve = "bilinear", Concentration = 100 },
        new() { Id = "tresiba-u200", Name = "Tresiba U200 (Insulin Degludec)", Category = InsulinCategory.UltraLongActing, DefaultDia = 42.0, DefaultPeak = 660, Curve = "bilinear", Concentration = 200 },
        new() { Id = "toujeo",       Name = "Toujeo (Insulin Glargine)",       Category = InsulinCategory.UltraLongActing, DefaultDia = 36.0, DefaultPeak = 720, Curve = "bilinear", Concentration = 300 },

        // Custom
        new() { Id = "custom", Name = "Custom", Category = InsulinCategory.RapidActing, DefaultDia = 4.0, DefaultPeak = 75, Curve = "rapid-acting", Concentration = 100 },
    ];

    public static IReadOnlyList<InsulinFormulation> GetAll() => _formulations;

    public static InsulinFormulation? GetById(string id) =>
        _formulations.FirstOrDefault(f => f.Id == id);

    public static IReadOnlyList<InsulinFormulation> GetByCategory(InsulinCategory category) =>
        _formulations.Where(f => f.Category == category).ToList();
}
