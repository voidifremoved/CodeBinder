// SPDX-FileCopyrightText: (C) 2020 Francesco Pretto <ceztko@gmail.com>
// SPDX-License-Identifier: MIT
using CodeBinder.Apple.Attributes;
using CodeBinder.Attributes;
using System.Linq;

namespace CodeBinder.Apple;

abstract class MethodWriter<TMethod> : ObjCCodeWriter<TMethod>
    where TMethod : BaseMethodDeclarationSyntax
{
    public bool IsStatic { get; private set; }
    public bool IsNative { get; private set; }

    protected MethodWriter(TMethod method, ObjCCompilationContext context, ObjCFileType fileType)
        : base(method, context, fileType)
    {
        IsStatic = Item.IsStatic(Context);
        IsNative = Item.HasAttribute<NativeAttribute>(context);
    }

    protected override void Write()
    {
        // TODO: Move it ObjCBaseTypeWriter.WriteTypeMembers
        var verbatimConversions = Item.GetAttributes<VerbatimConversionAttribute>(Context);
        var conversion = verbatimConversions.FirstOrDefault((attrib) =>
        {
            if (attrib.ConstructorArguments.Length == 1)
            {
                if (FileType.IsImplementation())
                    return true;
                else
                    return false;
            }
            else
            {
                return FileType.IsConversionType(attrib.GetConstructorArgument<ConversionType>(0));
            }
        });
        if (conversion != null)
        {
            // Use the verbatim conversion instead
            string verbatimStr = conversion.ConstructorArguments.Length == 1
                ? conversion.GetConstructorArgument<string>(0)
                : conversion.GetConstructorArgument<string>(1);
            Builder.AppendLine(verbatimStr);
        }
        else
        {
            write();
        }
    }

    void write()
    {
        WriteModifiers();
        WriteReturnType();
        Builder.Append(MethodName);
        WriteParameters();
        writeMethodBody();
    }

    protected virtual void WriteParameters()
    {
        int parameterCount = ParameterCount;
        if (parameterCount == 1)
        {
            writeParameters(Item.ParameterList, parameterCount);
        }
        else if (parameterCount > 1)
        {
            using (Builder.Indent())
            {
                using (Builder.Indent())
                {
                    writeParameters(Item.ParameterList, parameterCount);
                    Builder.AppendLine();
                }
            }
        }
    }

    protected virtual void WriteModifiers()
    {
        if (IsStatic)
            Builder.Append("+");
        else
            Builder.Append("-");

        Builder.Space();
    }

    protected void WriteType(TypeSyntax type, ObjCTypeUsageKind kind)
    {
        Builder.Append(type.GetObjCType(kind, Context));
    }

    protected void WriteOptionalParameters(int optionalIndex, bool commaSeparator)
    {
        string separator = " :";
        if (commaSeparator)
            separator = ", ";
        for (int i = 0; i < Item.ParameterList.Parameters.Count; i++)
        {
            var parameter = Item.ParameterList.Parameters[i];
            if (i > 0)
                Builder.Append(separator);

            if (i < optionalIndex)
                Builder.Append(parameter.Identifier.Text);
            else
                Builder.Append(parameter.Default!.Value, Context);
        }
    }

    void writeMethodBody()
    {
        if (FileType.IsHeader())
        {
            Builder.EndOfStatement();
        }
        else
        {
            using (Builder.AppendLine().Block())
            {
                if (Item.Body == null)
                {
                    Debug.Assert(Item.IsAbstract(Context));
                    // Objective C doesn't have abstract properties
                    Builder.Append("@throw [NSException exceptionWithName:@\"Not implemented\" reason:nil userInfo:nil]").EndOfStatement();
                }
                else
                {
                    WriteMethodBodyPrefixInternal();
                    if (DoWriteMethodBody && !Context.Conversion.SkipBody)
                        Builder.Append(Item.Body, Context, true).AppendLine();
                    WriteMethodBodyPostfixInternal();
                }
            }
        }
    }

    private void writeParameters(ParameterListSyntax list, int parameterCount)
    {
        bool first = true;
        for (int i = 0; i < parameterCount; i++)
        {
            Builder.AppendLine(ref first);
            var parameter = list.Parameters[i];
            writeParameter(parameter);
        }
    }

    private void writeParameter(ParameterSyntax parameter)
    {
        bool isRef = parameter.IsRef() | parameter.IsOut();
        if (parameter.TryGetAttribute<SelectorAttribute>(Context, out var selector))
        {
            // This parameter has a specified selector
            Builder.Append(selector.GetConstructorArgument<string>(0));
        }

        Builder.Colon();
        Builder.Parenthesized(() =>
            WriteType(parameter.Type!, isRef ? ObjCTypeUsageKind.DeclarationByRef : ObjCTypeUsageKind.Declaration)
        );
        Builder.Append(parameter.Identifier.Text);
    }

    public override ObjWriterType Type => IsNative ? ObjWriterType.CLangMethod : ObjWriterType.Method;

    protected virtual void WriteMethodBodyPrefixInternal() { /* Do nothing */ }

    protected virtual void WriteMethodBodyPostfixInternal() { /* Do nothing */ }

    protected void WriteReturnType()
    {
        using (Builder.Parenthesized(false))
        {
            writeReturnType();
        }
    }

    protected virtual void writeReturnType()
    {
        Builder.Append("void");
    }

    public virtual bool DoWriteMethodBody
    {
        get { return true; }
    }

    public virtual int ParameterCount
    {
        get { return Item.ParameterList.Parameters.Count; }
    }

    public abstract string MethodName { get; }
}

class MethodWriter : MethodWriter<MethodDeclarationSyntax>
{
    int _optionalIndex;

    public MethodWriter(MethodDeclarationSyntax method, int optionalIndex,
        ObjCCompilationContext context, ObjCFileType fileType)
        : base(method, context, fileType)
    {
        _optionalIndex = optionalIndex;
    }

    protected override void writeReturnType()
    {
        var methodSymbol = Item.GetDeclaredSymbol<IMethodSymbol>(Context);
        if (methodSymbol.HasObjCReplacement(ObjCSymbolUsage.Declaration, out var replacement) && replacement.ReturnType != null)
            Builder.Append(replacement.ReturnType);
        else
            WriteType(Item.ReturnType, ObjCTypeUsageKind.Declaration);
    }

    protected override void WriteMethodBodyPrefixInternal()
    {
        if (_optionalIndex >= 0)
        {
            var typeSymbol = Item.ReturnType.GetTypeSymbolThrow(Context);
            if (typeSymbol.SpecialType != SpecialType.System_Void)
                Builder.Append("return").Space();

            using (Builder.Bracketed(false))
            {
                Builder.Append("self").Space().Append(MethodName).Colon();
                WriteOptionalParameters(_optionalIndex, false);
            }

            Builder.EndOfStatement();
        }
        else
        {
            if (Context.Conversion.SkipBody)
                Builder.Append(Item.ReturnType.GetObjCDefaultReturnStatement(Context)).EndOfStatement();
        }
    }

    public bool IsParentInterface
    {
        get { return Item.Parent!.IsKind(SyntaxKind.InterfaceDeclaration); }
    }

    public override string MethodName
    {
        get
        {
            var methodSymbol = Item.GetDeclaredSymbol<IMethodSymbol>(Context);
            return methodSymbol.GetObjCName(ObjCSymbolUsage.Declaration, Context);
        }
    }

    public override bool DoWriteMethodBody
    {
        // Don't write true method body if it's just an optional parameter trampoline
        get { return _optionalIndex == -1; }
    }

    public override int ParameterCount
    {
        get
        {
            if (_optionalIndex == -1)
                return base.ParameterCount;

            return _optionalIndex;
        }
    }
}

class ObjCConstructorWriter : MethodWriter<ConstructorDeclarationSyntax>
{
    int _optionalIndex;

    public ObjCConstructorWriter(ConstructorDeclarationSyntax method, int optionalIndex,
        ObjCCompilationContext context, ObjCFileType fileType)
        : base(method, context, fileType)
    {
        _optionalIndex = optionalIndex;
    }

    protected override void WriteParameters()
    {
        if (!IsStatic)
            base.WriteParameters();
    }

    protected override void writeReturnType()
    {
        if (IsStatic)
            Builder.Append("void");
        else
            Builder.Append("id");
    }

    protected override void WriteMethodBodyPrefixInternal()
    {
        if (IsStatic)
        {
            // https://stackoverflow.com/questions/992070/static-constructor-equivalent-in-objective-c
            var method = Item.GetDeclaredSymbol<IMethodSymbol>(Context);
            Builder.Append($"if (self != [{method.ContainingType.GetObjCName(Context)} class])").AppendLine()
                .IndentChild().Append("return").EndOfStatement().Close();
        }
        else
        {
            void declareNonDynamicConstructorContext(IMethodSymbol constructor)
            {
                Builder.Append("SEL selector = @selector(").Append(constructor.GetObjCName(Context));
                for (int i = 0; i < constructor.Parameters.Length; i++)
                    Builder.Colon();
                Builder.Append(")").EndOfStatement();
                Builder.Append("typedef id (*NonDynamicInitializer)(id, SEL, ");
                bool first = true;
                foreach (var parameter in constructor.Parameters)
                {
                    if (first)
                        first = false;
                    else
                        Builder.Comma().Space();

                    Builder.Append(parameter.Type.GetObjCType(ObjCTypeUsageKind.Declaration, Context));
                }

                Builder.Append(")").EndOfStatement();

                Builder.Append($"NonDynamicInitializer initNonDynamic = (NonDynamicInitializer)class_getMethodImplementation([{Item.GetDeclaredSymbol(Context)!.ContainingType.GetObjCName(Context)} class], selector)").EndOfStatement();
            }

            void invokeConstructorNonDynamic(Action writeParameters)
            {
                Builder.Append("initNonDynamic(self, selector, ");
                writeParameters();
                Builder.Append(")");
            }

            if (_optionalIndex >= 0)
            {
                declareNonDynamicConstructorContext(Item.GetDeclaredSymbol<IMethodSymbol>(Context));

                Builder.Append("return").Space();
                invokeConstructorNonDynamic(() => WriteOptionalParameters(_optionalIndex, true));
                Builder.EndOfStatement();
            }
            else
            {
                InitializerKind initializerKind = InitializerKind.Unknown;
                if (Item.Initializer == null)
                {
                    initializerKind = InitializerKind.Implicit;
                }
                else
                {
                    switch (Item.Initializer.ThisOrBaseKeyword.Kind())
                    {
                        case SyntaxKind.ThisKeyword:
                            initializerKind = InitializerKind.This;
                            break;
                        case SyntaxKind.BaseKeyword:
                            initializerKind = InitializerKind.Base;
                            break;
                        default:
                            throw new NotSupportedException();
                    }
                }

                switch (initializerKind)
                {
                    // Why self = [super init]? https://stackoverflow.com/a/7185530/213871
                    case InitializerKind.Implicit:
                    {
                        Builder.Append("self = [super init]");
                        break;
                    }
                    case InitializerKind.Base:
                    {
                        Builder.Append("self = ").Bracketed().Append("super").Space().Append("init").Append(Item.Initializer!.ArgumentList.Arguments, false, Context).Close();
                        break;
                    }
                    case InitializerKind.This:
                    {
                        declareNonDynamicConstructorContext(Item.Initializer!.GetSymbol<IMethodSymbol>(Context));
                        Builder.Append("self = ");
                        invokeConstructorNonDynamic(() =>
                        {
                            bool first = true;
                            foreach (var arg in Item.Initializer!.ArgumentList.Arguments)
                            {
                                if (first)
                                    first = false;
                                else
                                    Builder.Append(", ");

                                Builder.Append(arg.Expression, Context);
                            }
                        });
                        break;
                    }
                    default:
                        throw new NotSupportedException();
                }

                Builder.EndOfStatement();

                Builder.Append("if (self == nil)").AppendLine()
                    .IndentChild().Append("return nil").EndOfStatement().Close();
            }
        }
    }

    protected override void WriteMethodBodyPostfixInternal()
    {
        if (!IsStatic && _optionalIndex == -1)
            Builder.Append("return self").EndOfStatement();
    }

    public override bool DoWriteMethodBody => _optionalIndex == -1;

    public override int ParameterCount
    {
        get
        {
            if (_optionalIndex == -1)
                return base.ParameterCount;

            return _optionalIndex;
        }
    }

    public override string MethodName
    {
        get
        {
            if (IsStatic)
            {
                return "initialize";
            }
            else
            {
                var methodSymbol = Item.GetDeclaredSymbol<IMethodSymbol>(Context);
                return methodSymbol.GetObjCName(ObjCSymbolUsage.Declaration, Context);
            }
        }
    }

    enum InitializerKind
    {
        Unknown = 0,
        Implicit,
        This,
        Base,
    }
}

// NOTE: Not needed to call [super dealloc]
// https://stackoverflow.com/a/25377583/213871
class ObjCDestructorWriter : MethodWriter<DestructorDeclarationSyntax>
{
    public ObjCDestructorWriter(DestructorDeclarationSyntax method,
        ObjCCompilationContext context, ObjCFileType fileType)
        : base(method, context, fileType) { }

    public override string MethodName
    {
        get { return "dealloc"; }
    }
}
