
namespace CodeBinder.Apple;

/// <summary>
/// Conversion to produce symbols.ld64.exports file for LD64 linking (Apple specific)
/// </summary>
class ObjCSymbolsExportLD64Conversion : ConversionWriter
{
    const string baseExports = """
_OBJC_CLASS_$_CBBoolArray
_OBJC_METACLASS_$_CBBoolArray
_OBJC_CLASS_$_CBCharArray
_OBJC_METACLASS_$_CBCharArray
_OBJC_CLASS_$_CBDoubleArray
_OBJC_METACLASS_$_CBDoubleArray
_OBJC_CLASS_$_CBException
_OBJC_METACLASS_$_CBException
_OBJC_EHTYPE_$_CBException
_OBJC_CLASS_$_CBFinalizableObject
_OBJC_METACLASS_$_CBFinalizableObject
_OBJC_CLASS_$_CBFloatArray
_OBJC_METACLASS_$_CBFloatArray
_OBJC_CLASS_$_CBHandleRef
_OBJC_METACLASS_$_CBHandleRef
_OBJC_CLASS_$_CBHandledObject
_OBJC_METACLASS_$_CBHandledObject
_OBJC_CLASS_$_CBHandledObjectBase
_OBJC_METACLASS_$_CBHandledObjectBase
_OBJC_CLASS_$_CBHandledObjectFinalizer
_OBJC_METACLASS_$_CBHandledObjectFinalizer
_OBJC_CLASS_$_CBInt16Array
_OBJC_METACLASS_$_CBInt16Array
_OBJC_CLASS_$_CBInt32Array
_OBJC_METACLASS_$_CBInt32Array
_OBJC_CLASS_$_CBInt64Array
_OBJC_METACLASS_$_CBInt64Array
_OBJC_CLASS_$_CBInt8Array
_OBJC_METACLASS_$_CBInt8Array
_OBJC_CLASS_$_CBKeyValuePair
_OBJC_METACLASS_$_CBKeyValuePair
_OBJC_CLASS_$_CBNSIntegerArray
_OBJC_METACLASS_$_CBNSIntegerArray
_OBJC_CLASS_$_CBNSUIntegerArray
_OBJC_METACLASS_$_CBNSUIntegerArray
_OBJC_CLASS_$_CBPtrArray
_OBJC_METACLASS_$_CBPtrArray
_OBJC_CLASS_$_CBUInt16Array
_OBJC_METACLASS_$_CBUInt16Array
_OBJC_CLASS_$_CBUInt32Array
_OBJC_METACLASS_$_CBUInt32Array
_OBJC_CLASS_$_CBUInt64Array
_OBJC_METACLASS_$_CBUInt64Array
_OBJC_CLASS_$_CBUInt8Array
_OBJC_METACLASS_$_CBUInt8Array
""";

    public ObjCCompilationContext Compilation { get; private set; }

    public ObjCSymbolsExportLD64Conversion(ObjCCompilationContext compilation)
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
        builder.AppendLine(baseExports);
        foreach (var type in Compilation.StorageTypes)
        {
            var symbol = type.Symbol;
            if (!symbol.HasAccessibility(Accessibility.Public))
                continue;

            var typeName = symbol.GetObjCName(Compilation);
            builder.Append("_OBJC_CLASS_$_").AppendLine(typeName);
            builder.Append("_OBJC_METACLASS_$_").AppendLine(typeName);
            if (symbol.IsException())
            {
                // Exceptions need a special symbol
                builder.Append("_OBJC_EHTYPE_$_").AppendLine(typeName);
            }   
 
            foreach (var member in symbol.GetMembers())
            {
                // Accessibile (public or protected) fields need a special symbol
                if (member.Kind == SymbolKind.Field
                    && (member.DeclaredAccessibility == Accessibility.Public
                    || member.DeclaredAccessibility == Accessibility.Protected
                    || member.DeclaredAccessibility == Accessibility.ProtectedAndInternal))
                {
                    builder.Append("_OBJC_IVAR_$_").Append(typeName).Dot().AppendLine(member.Name);
                }
            }
        }
    }
}
