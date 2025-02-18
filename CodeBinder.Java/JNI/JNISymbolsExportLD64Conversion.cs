
namespace CodeBinder.JNI;

/// <summary>
/// Conversion to produce symbols.ld64.exports file for LD64 linking (Apple specific)
/// </summary>
class JNISymbolsExportLD64Conversion : ConversionWriter
{
    public JNICompilationContext Compilation { get; private set; }

    public JNISymbolsExportLD64Conversion(JNICompilationContext compilation)
    {
        Compilation = compilation;
    }

    protected override bool? GetUseUTF8Bom() => false;

    protected override string GetFileName()
    {
        return "symbols.ld64.exports";
    }

    protected override void write(CodeBuilder builder)
    {
        builder.AppendLine("_JNI_OnLoad");
        foreach (var module in Compilation.Modules)
        {
            foreach (var method in module.Methods)
                builder.Append("_").AppendLine(method.GetJNIMethodName(module));
        }
    }
}
