// SPDX-FileCopyrightText: (C) 2020 Francesco Pretto <ceztko@gmail.com>
// SPDX-License-Identifier: MIT

using CodeBinder.Attributes;

namespace CodeBinder.Apple;

partial class ObjCTypeConversion<TTypeContext>
{
    public abstract class Implementation : ObjCTypeConversion<TTypeContext>
    {
        public ObjImplementationType ImplementationType { get; private set; }

        public Implementation(TTypeContext context, ConversionCSharpToObjC conversion, ObjImplementationType implementationType)
            : base(context, conversion)
        {
            ImplementationType = implementationType;
        }

        protected sealed override string GetFileName() => ImplementationsFilename;

        protected override string? GetBasePath() =>
            ImplementationType == ObjImplementationType.PublicType ? null : ConversionCSharpToObjC.InternalBasePath;

        protected override IEnumerable<string> Imports
        {
            get
            {
                if (ImplementationType == ObjImplementationType.PublicType)
                {
                    yield return $"\"{ConversionCSharpToObjC.InternalBasePath}/{Compilation.ObjCLibraryHeaderName}\"";
                    yield return $"\"{ConversionCSharpToObjC.InternalBasePath}/{nameof(ObjCResources.CBOCInterop_h).ToObjCHeaderFilename()}\"";
                }
                else
                {
                    yield return $"\"{Compilation.ObjCLibraryHeaderName}\"";
                    yield return $"\"{nameof(ObjCResources.CBOCInterop_h).ToObjCHeaderFilename()}\"";
                }
                yield return $"<{Compilation.CLangLibraryHeaderName}>";
                var attributes = Context.Node.GetAttributes(this);
                foreach (var attribute in attributes)
                {
                    if (attribute.IsAttribute<ImportAttribute>())
                    {
                        var include = new ImportAttribute(attribute.GetConstructorArgument<string>(0));
                        if (attribute.TryGetNamedArgument("Condition", out string? cond))
                            include.Condition = cond;
                        if (attribute.TryGetNamedArgument("Private", out bool priv))
                            include.Private = priv;

                        if (include.Private)
                            yield return include.Name;
                    }
                }
            }
        }

        public override ConversionType ConversionType => ConversionType.Implementation;
    }
}

class ObjCClassConversionImplementation : ObjCTypeConversion<ObjCClassContext>.Implementation
{
    public ObjCClassConversionImplementation(ObjCClassContext context,
            ConversionCSharpToObjC conversion, ObjImplementationType implementationType)
        : base(context, conversion, implementationType) { }

    protected override CodeWriter GetTypeWriter()
    {
        return new ObjCClassWriter(Context.Node, Context.ComputePartialDeclarationsTree(), Context.Compilation, ObjCFileType.Implementation);
    }
}

class ObjCStructConversionImplementation : ObjCTypeConversion<ObjCStructContext>.Implementation
{
    public ObjCStructConversionImplementation(ObjCStructContext context,
            ConversionCSharpToObjC conversion, ObjImplementationType implementationType)
        : base(context, conversion, implementationType) { }

    protected override CodeWriter GetTypeWriter()
    {
        return new ObjCStructWriter(Context.Node, Context.ComputePartialDeclarationsTree(), Context.Compilation, ObjCFileType.Implementation);
    }
}
