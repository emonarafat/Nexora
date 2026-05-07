#r "/home/runner/.nuget/packages/typesense/8.3.0/lib/net8.0/Typesense.dll"

using System.Reflection;
var asm = typeof(Typesense.ITypesenseClient).Assembly;
foreach (var t in asm.GetExportedTypes().OrderBy(x => x.FullName))
{
    Console.WriteLine($"TYPE: {t.FullName}");
    if (!t.IsEnum && t.Name is "SearchResult`1" or "Hit`1" or "FacetCount" or "FacetCountDetail" or "TextMatchInfo" or "Config" or "ImportType")
    {
        foreach (var p in t.GetProperties())
            Console.WriteLine($"  PROP: {p.Name} : {p.PropertyType.Name}");
        foreach (var c in t.GetConstructors())
            Console.WriteLine($"  CTOR: ({string.Join(", ", c.GetParameters().Select(x => x.ParameterType.Name + " " + x.Name))})");
    }
    if (t.IsEnum)
        foreach (var v in Enum.GetNames(t))
            Console.WriteLine($"  VALUE: {v}");
}
