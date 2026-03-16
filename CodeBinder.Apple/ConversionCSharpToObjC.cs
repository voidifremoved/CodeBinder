// SPDX-FileCopyrightText: (C) 2020 Francesco Pretto <ceztko@gmail.com>
// SPDX-License-Identifier: MIT
using CodeBinder.Attributes;
using BinderPolicies = CodeBinder.Attributes.Features;

namespace CodeBinder.Apple;

[ConversionLanguageName("ObjectiveC")]
[ConfigurationSwitch("conversion-prefix", "The conversion prefix for types. The default is \"OC\"", true)]
public class ConversionCSharpToObjC : CSharpLanguageConversion<ObjCCompilationContext>
{
    internal const string SourcePreamble = "/* This file was generated. DO NOT EDIT! */";

    internal const string HeaderExtension = "h";
    internal const string ImplementationExtension = "mm";
    internal const string TypesHeader = "OCTypes.h";
    internal const string BaseTypesHeader = "CBOCBaseTypes.h";
    internal const string InternalBasePath = "Internal";
    internal const string SupportBasePath = "Support";
    const string DefaultConversionPrefix = "OC";

    string _ConversionPrefix;

    public ConversionCSharpToObjC()
    {
        _ConversionPrefix = DefaultConversionPrefix;
    }

    public override string GetMethodBaseName(IMethodSymbol symbol, string? stem)
    {
        if (symbol.MethodKind == MethodKind.Constructor)
        {
            if (stem == null)
                return "init";
            else return $"init{stem}";

        }
        else
        {
            return base.GetMethodBaseName(symbol, stem);
        }
    }
    public override bool TryParseExtraArgs(List<KeyValuePair<string, string?>> args)
    {
        // Try parse --interface-only switch
        if (args.Count == 1 && args[0].Key == "conversion-prefix")
        {
            ConversionPrefix = args[0].Value;
            return true;
        }

        return false;
    }

    protected override ObjCCompilationContext CreateCSharpCompilationContext()
    {
        return new ObjCCompilationContext(this);
    }

    // NOTE: Delegates are not yet fully supported, PassByRef doesn't work perfectly as well
    // _policies = new List<string>() { BinderPolicies.PassByRef, BinderPolicies.PassByRef, BinderPolicies.Delegates };
    public override IReadOnlyCollection<string> SupportedPolicies =>
        new string[] { BinderPolicies.InstanceFinalizers};

    public override OverloadFeature? OverloadFeatures => OverloadFeature.ParameterArity;

    public override bool NeedNamespaceMapping => false;

    public override bool DiscardNative => true;

    public bool SkipBody { get; set; }

    public override MethodCasing MethodCasing => MethodCasing.LowerCamelCase;

    public override IReadOnlyList<string> PreprocessorDefinitions
    {
        get { return new string[] { "OBJECTIVEC", "APPLE" }; }
    }

    [AllowNull]
    public string ConversionPrefix
    {
        get => _ConversionPrefix;
        set
        {
            if (value == null || value.Length == 0)
            {
                _ConversionPrefix = DefaultConversionPrefix;
                return;
            }

            if (value.Length > 6)
                throw new Exception("Conversion prefix is too long (maximum 6 characters)");

            _ConversionPrefix = value;
        }
    }

    public override IEnumerable<IConversionWriter> DefaultConversions
    {
        get
        {
            yield return new ObjCBaseTypesHeaderConversion();
            yield return new StringConversionWriter(nameof(ObjCResources.CBHandledObject_Internal_h).ToObjCHeaderFilename(), () => ObjCResources.CBHandledObject_Internal_h) { BasePath = InternalBasePath, GeneratedPreamble = SourcePreamble };
            yield return new StringConversionWriter(nameof(ObjCResources.CBHandledObject_mm).ToObjCImplementationFilename(), () => ObjCResources.CBHandledObject_mm) { BasePath = SupportBasePath, GeneratedPreamble = SourcePreamble };
            yield return new StringConversionWriter(nameof(ObjCResources.CBOCInterop_h).ToObjCHeaderFilename(), () => ObjCResources.CBOCInterop_h) { BasePath = InternalBasePath, GeneratedPreamble = SourcePreamble };
            yield return new StringConversionWriter(nameof(ObjCResources.CBOCInterop_mm).ToObjCImplementationFilename(), () => ObjCResources.CBOCInterop_mm) { BasePath = InternalBasePath, GeneratedPreamble = SourcePreamble };
            yield return new StringConversionWriter(nameof(ObjCClasses.CBException_mm).ToObjCImplementationFilename(), () => ObjCClasses.CBException_mm) { BasePath = SupportBasePath, GeneratedPreamble = SourcePreamble };
            yield return new StringConversionWriter(nameof(ObjCClasses.CBIEqualityCompararer_h).ToObjCHeaderFilename(), () => ObjCClasses.CBIEqualityCompararer_h) { BasePath = SupportBasePath, GeneratedPreamble = SourcePreamble };
            yield return new StringConversionWriter(nameof(ObjCClasses.CBIReadOnlyList_h).ToObjCHeaderFilename(), () => ObjCClasses.CBIReadOnlyList_h) { BasePath = SupportBasePath, GeneratedPreamble = SourcePreamble };
            yield return new StringConversionWriter(nameof(ObjCClasses.CBIDisposable_h).ToObjCHeaderFilename(), () => ObjCClasses.CBIDisposable_h) { BasePath = SupportBasePath, GeneratedPreamble = SourcePreamble };
            yield return new StringConversionWriter(nameof(ObjCClasses.CBKeyValuePair_mm).ToObjCImplementationFilename(), () => ObjCClasses.CBKeyValuePair_mm) { BasePath = SupportBasePath, GeneratedPreamble = SourcePreamble };
            yield return new StringConversionWriter(nameof(ObjCClasses.CBHandleRef_mm).ToObjCImplementationFilename(), () => ObjCClasses.CBHandleRef_mm) { BasePath = SupportBasePath, GeneratedPreamble = SourcePreamble };
        }
    }
}
