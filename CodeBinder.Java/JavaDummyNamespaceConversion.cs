// SPDX-FileCopyrightText: (C) 2020 Francesco Pretto <ceztko@gmail.com>
// SPDX-License-Identifier: MIT


using System.IO;
using System.Text;

namespace CodeBinder.Java;

class JavaDummyNamespaceConversion : JavaConversionWriterBase
{
    string[] _nsPartials;

    public JavaDummyNamespaceConversion(string[] nsPartials)
    {
        _nsPartials = nsPartials;
    }

    protected override string? GetBasePath()
    {
        return Path.Combine(_nsPartials);
    }

    protected override string GetFileName()
    {
        return "CB_Dummy.java";
    }

    protected override void write(CodeBuilder builder)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < _nsPartials.Length; i++)
        {
            if (i != 0)
                sb.Append('.');

            sb.Append(_nsPartials[i]);
        }

        builder.Append("package").Space().Append(sb.ToString()).EndOfStatement();
        builder.AppendLine();

        builder.Append("""
// This dummy class allow to always import parent
// namespace imports with wildcard ".*"
final class CB_Dummy
{
    private CB_Dummy() {}
}
""");
    }
}
