using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace NoCopyAnalyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NoCopyAnalyzer : DiagnosticAnalyzer
{
    private static DiagnosticDescriptor Rule(int id, string title, string msg)
    {
        return new DiagnosticDescriptor(
            id: $"NCP{id:D2}",
            title: title,
            messageFormat: msg,
            category: "NoCopy",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );
    }

    private static readonly DiagnosticDescriptor ParameterRule = Rule(
        1, "Passed by Value", "Type `{0}` is marked as `NoCopy` and should be received only by reference"
    );

    private static readonly DiagnosticDescriptor ArgumentRule = Rule(
        2, "Received by Value", "Type `{0}` is marked as `NoCopy` and should be passed only by reference"
    );

    private static readonly DiagnosticDescriptor FieldRule = Rule(
        3, "Field of Copy Type", "Type `{0}` is marked as `NoCopy` and can be a field only of a `NoCopy` type"
    );

    private static readonly DiagnosticDescriptor BoxingRule = Rule(
        4, "Boxed", "Type `{0}` is marked as `NoCopy` and shouldn't be boxed"
    );

    private static readonly DiagnosticDescriptor CaptureRule = Rule(
        5, "Captured by Closure", "Type `{0}` is marked as `NoCopy` and shouldn't be captured by a closure"
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        ParameterRule,
        ArgumentRule,
        FieldRule,
        BoxingRule,
        CaptureRule
    );

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(AnalyzeParameter, SymbolKind.Parameter);
        context.RegisterOperationAction(AnalyzeArgument, OperationKind.Argument);
        context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
        context.RegisterOperationAction(AnalyzeBoxing, OperationKind.Conversion);
        context.RegisterSyntaxNodeAction(
            AnalyzeCaptures,
            SyntaxKind.AnonymousMethodExpression,
            SyntaxKind.SimpleLambdaExpression,
            SyntaxKind.ParenthesizedLambdaExpression,
            SyntaxKind.LocalFunctionStatement
        );
    }

    private static void AnalyzeParameter(SymbolAnalysisContext ctx)
    {
        var sym = (IParameterSymbol)ctx.Symbol;
        if (sym.RefKind != RefKind.None)
            return;

        if (!IsNonCopyType(sym.Type))
            return;

        ctx.ReportDiagnostic(Diagnostic.Create(ParameterRule, sym.Locations.First(), sym.Type.Name));
    }

    private static void AnalyzeArgument(OperationAnalysisContext ctx)
    {
        var op = (IArgumentOperation)ctx.Operation;
        if (op.Parameter != null && op.Parameter.RefKind != RefKind.None)
            return;

        var t = op.Value.Type;
        if (t == null || !IsNonCopyType(t))
            return;

        ctx.ReportDiagnostic(Diagnostic.Create(ArgumentRule, op.Value.Syntax.GetLocation(), t.Name));
    }

    private static void AnalyzeField(SymbolAnalysisContext ctx)
    {
        var sym = (IFieldSymbol)ctx.Symbol;
        if (sym.Type.TypeKind != TypeKind.Struct || sym.ContainingType.TypeKind != TypeKind.Struct)
            return;

        if (!IsNonCopyType(sym.Type) || IsNonCopyType(sym.ContainingType))
            return;

        ctx.ReportDiagnostic(Diagnostic.Create(FieldRule, sym.Locations.First(), sym.Type.Name));
    }

    private static void AnalyzeBoxing(OperationAnalysisContext ctx)
    {
        var op = (IConversionOperation)ctx.Operation;
        var typeFrom = op.Operand.Type;
        var typeTo = op.Type;

        if (typeFrom == null || typeTo == null)
            return;

        if (typeFrom.TypeKind != TypeKind.Struct || !typeTo.IsReferenceType)
            return;

        if (!IsNonCopyType(typeFrom))
            return;

        ctx.ReportDiagnostic(Diagnostic.Create(BoxingRule, op.Operand.Syntax.GetLocation(), typeFrom.Name));
    }

    private static void AnalyzeCaptures(SyntaxNodeAnalysisContext ctx)
    {
        var dataFlowAnalysis = ctx.SemanticModel.AnalyzeDataFlow(ctx.Node);
        var capturedVariables = dataFlowAnalysis.Captured;

        foreach (var capture in capturedVariables)
        {
            var t = GetCaptureSymbolType(capture);
            if (t != null && IsNonCopyType(t))
                ctx.ReportDiagnostic(Diagnostic.Create(CaptureRule, capture.Locations.First(), t.Name));
        }
    }

    private static bool IsNonCopyType(ITypeSymbol t)
    {
        return t.TypeKind == TypeKind.Struct && t.GetAttributes().Any(IsNonCopyAttribute);
    }

    private static bool IsNonCopyAttribute(AttributeData a)
    {
        var name = a.AttributeClass?.Name;
        if (name == null)
            return false;

        return name.EndsWith("NoCopyAttribute") || name.EndsWith("NoCopy");
    }

    private static ITypeSymbol? GetCaptureSymbolType(ISymbol symbol)
    {
        return symbol.Kind switch
        {
            SymbolKind.Local => ((ILocalSymbol)symbol).Type,
            SymbolKind.Parameter => ((IParameterSymbol)symbol).Type,
            _ => null
        };
    }
}