
namespace CodeBinder.JNI;

/// <summary>
/// Conversion to produce symbols.ld.exports file for "ld" linking (GNU ld specific)
/// </summary>
class JNISymbolsExportLDConversion : ConversionWriter
{
    public JNICompilationContext Compilation { get; private set; }

    public JNISymbolsExportLDConversion(JNICompilationContext compilation)
    {
        Compilation = compilation;
    }

    protected override bool? GetUseUTF8Bom() => false;

    protected override string GetFileName()
    {
        return "symbols.ld.exports";
    }

    protected override void write(CodeBuilder builder)
    {
        builder.AppendLine("""
JNI {
    global:
""");

        foreach (var module in Compilation.Modules)
        {
            foreach (var method in module.Methods)
                builder.Append("        ").Append(method.GetJNIMethodName(module)).EndOfLine();
        }

        builder.AppendLine("""
    local: *;
};
""");
    }
}
