using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CodeTag
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CodeTagAnalyzer : DiagnosticAnalyzer
    {
        private static readonly HashSet<string> _codeTagAttributes = new()
        {
            "CodeTag",
            "CodeTagAttribute",
            "DefineCodeTag",
            "DefineCodeTagAttribute"
        };

        private static readonly HashSet<string> _defineCodeTagAttributes = new()
        {
            "DefineCodeTag",
            "DefineCodeTagAttribute"
        };

        private static readonly IReadOnlyList<string> _noTags = new List<string>().AsReadOnly();
        private static readonly HashSet<ISymbol> _noSymbols = new(SymbolEqualityComparer.Default);
        private static readonly ConcurrentSparseValueCache<ISymbol, IReadOnlyList<string>> _tagCache = new(_noTags, SymbolEqualityComparer.Default);
        private static readonly ConcurrentSparseValueCache<ISymbol, HashSet<ISymbol>> _symbolsContainedInSymbolCache = new(_noSymbols, SymbolEqualityComparer.Default);

        internal static bool IsCodeTagAttribute(string? name) => name is not null && _codeTagAttributes.Contains(name);
        internal static bool IsDefineCodeTag(string? name) => name is not null && _defineCodeTagAttributes.Contains(name);


        private static readonly DiagnosticDescriptor _missingTagRule =
            new("CT001",
                title: "Missing CodeTag",
                messageFormat: """Consider adding [CodeTag("{0}")] to element '{1}'""",
                category: "Tagging",
                defaultSeverity: DiagnosticSeverity.Info,
                isEnabledByDefault: true,
                description: "Methods or properties referencing another method or property with a CodeTag may optionally have a CodeTag with the same key.");

        private static readonly DiagnosticDescriptor _unnecessaryTagRule =
            new(
                "CT002",
                "Unnecessary CodeTag",
                """Unnecessary CodeTag [CodeTag("{0}")] on element '{1}'""",
                "Tagging",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "CodeTags should only be applied where necessary. Consider using the DefineCodeTag attribute if you intend to tag this element.");

        private static readonly DiagnosticDescriptor _duplicateTagRule = 
            new(
                "CT003",
                "Duplicate CodeTag",
                """Duplicate CodeTag [CodeTag("{0})"] on element '{1}'""",
                "Tagging",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "CodeTags on a single element must be unique.");

        private static readonly DiagnosticDescriptor _invalidTagRule =
            new(
                "CT004",
                "Invalid CodeTag",
                """Invalid CodeTag [CodeTag("{0})] on element '{1}'""",
                "Tagging",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "CodeTags cannot be null, empty, or whitespace.");


        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(_missingTagRule, _unnecessaryTagRule, _duplicateTagRule, _invalidTagRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSymbolAction(AnalyzeSymbolContext, SymbolKind.Method, SymbolKind.Property);
        }

        private void AnalyzeSymbolContext(SymbolAnalysisContext context)
        {
            ISymbol symbol = context.Symbol;

            var currentCodeTags = _tagCache.GetValue(symbol, () => symbol
                .GetAttributes()
                .Where(a => IsCodeTagAttribute(a.AttributeClass.Name))
                .Select(a => _tagFromAttribute(context, symbol, a))
                .Where(tag => tag is not null)
                .ToList());

            foreach (var diagnostic in currentCodeTags
                         .GroupBy(tag => tag)
                         .Where(g => g.Count() > 1)
                         .Select(g => g.Key)
                         .Select(tag => Diagnostic.Create(_duplicateTagRule, symbol.Locations[0], tag, symbol.Name)))
            {
                context.ReportDiagnostic(diagnostic);
            }

            // Contains tags that the current element includes based on its references (direct or indirect) to other tagged elements
            var referencedTags = _getReferencedTags(symbol, context.Compilation, context);

            foreach (var diagnostic in currentCodeTags.Except(referencedTags).Select(unnecessaryTag => Diagnostic.Create(_unnecessaryTagRule, symbol.Locations[0], unnecessaryTag, symbol.Name)))
            {
                context.ReportDiagnostic(diagnostic);
            }

            foreach (var diagnostic in referencedTags.Except(currentCodeTags).Distinct().Select(missingTag => Diagnostic.Create(_missingTagRule, symbol.Locations[0], missingTag, symbol.Name)))
            {
                context.ReportDiagnostic(diagnostic);
            }
        }

        private IEnumerable<ISymbol> _extractSymbolsFromLambdaOrAnonymous(SyntaxNode node, SemanticModel semanticModel)
        {
            if (node is not LambdaExpressionSyntax && node is not AnonymousMethodExpressionSyntax)
                yield break;

            foreach (var n in node.DescendantNodes(descendIntoChildren: n => n is ExpressionSyntax).OfType<IdentifierNameSyntax>())
            {
                var symbol = semanticModel.GetSymbolInfo(n).Symbol;
                if (symbol is IMethodSymbol || symbol is IPropertySymbol)
                    yield return symbol;
            }
        }

        private IEnumerable<ISymbol> _gatherReferencedSymbols(ISymbol symbol, Compilation compilation)
        {
            if (_symbolsContainedInSymbolCache.TryGetValue(symbol, out var cachedResult))
                return cachedResult;

            Stack<ISymbol> symbolsToProcess = new();
            HashSet<ISymbol> processedSymbols = new(SymbolEqualityComparer.Default);
            symbolsToProcess.Push(symbol);

            while (symbolsToProcess.Count > 0)
            {
                var currentSymbol = symbolsToProcess.Pop();

                if (!processedSymbols.Add(currentSymbol))
                    continue;

                if (_symbolsContainedInSymbolCache.TryGetValue(currentSymbol, out cachedResult))
                {
                    processedSymbols.UnionWith(cachedResult);
                    continue;
                }

                HashSet<ISymbol> currentReferencedSymbols = new(SymbolEqualityComparer.Default);

                foreach (var syntaxRef in currentSymbol.DeclaringSyntaxReferences)
                {
                    var semanticModel = compilation.GetSemanticModel(syntaxRef.SyntaxTree);
                    var nodes = syntaxRef.GetSyntax().DescendantNodes();

                    var refSymbols = nodes
                        .Select(node => semanticModel.GetSymbolInfo(node).Symbol)
                        .Where(s => s is IMethodSymbol || s is IPropertySymbol);

                    var lambdaSymbols = nodes
                        .Where(n => n is LambdaExpressionSyntax || n is AnonymousMethodExpressionSyntax)
                        .SelectMany(n => _extractSymbolsFromLambdaOrAnonymous(n, semanticModel));

                    var newReferencedSymbols = new HashSet<ISymbol>(refSymbols.Concat(lambdaSymbols), SymbolEqualityComparer.Default);

                    foreach (var refSymbol in newReferencedSymbols)
                    {
                        symbolsToProcess.Push(refSymbol);
                    }

                    currentReferencedSymbols.UnionWith(newReferencedSymbols);
                }

                if (currentReferencedSymbols.Count == 0)
                {
                    _symbolsContainedInSymbolCache.AddEmpty(currentSymbol);
                    continue;
                }

                _symbolsContainedInSymbolCache.Add(currentSymbol, currentReferencedSymbols);
                processedSymbols.UnionWith(currentReferencedSymbols);
            }

            if (processedSymbols.Count > 0)
            {
                _symbolsContainedInSymbolCache.Add(symbol, processedSymbols);
                return processedSymbols;
            }

            _symbolsContainedInSymbolCache.AddEmpty(symbol);
            return _noSymbols;
        }

        private IReadOnlyList<string> _getReferencedTags(ISymbol symbol, Compilation compilation, SymbolAnalysisContext context)
        {
            var references = _gatherReferencedSymbols(symbol, compilation);

            var seenTags = new HashSet<string>();
            return references
                .SelectMany(refSymbol => _tagCache.GetValue(
                    refSymbol,
                    () => refSymbol
                        .GetAttributes()
                        .Where(a => IsDefineCodeTag(a.AttributeClass.Name))
                        .Select(a => _tagFromAttribute(context, refSymbol, a))
                        .Where(tag => tag is not null)
                        .ToList()))
                .Where(tag => seenTags.Add(tag))
                .ToList();
        }

        private string _tagFromAttribute(SymbolAnalysisContext context, ISymbol refSymbol, AttributeData a)
        {
            string tag = GetTagKey(a, refSymbol);
            if (!string.IsNullOrWhiteSpace(tag)) return tag;

            var diagnostic = Diagnostic.Create(_invalidTagRule, refSymbol.Locations[0], "<none>");
            context.ReportDiagnostic(diagnostic);
            return null!;
        }

        internal static string GetTagKey(AttributeData attribute, ISymbol appliedToSymbol)
        {
            if (attribute.ConstructorArguments.Length == 1)
            {
                return (string)attribute.ConstructorArguments[0].Value;
            }
            
            return _generateTagKey(appliedToSymbol);
        }

        private static string _generateTagKey(ISymbol symbol)
        {
            var tag = new StringBuilder(symbol.Name, 32);

            var currentType = symbol.ContainingType;
            while (currentType is not null)
            {
                tag.Insert(0, '.');
                tag.Insert(0, currentType.Name);
                currentType = currentType.ContainingType;
            }

            if (symbol.ContainingNamespace is null || symbol.ContainingNamespace.IsGlobalNamespace)
                return tag.ToString();

            tag.Insert(0, '.');
            tag.Insert(0, symbol.ContainingNamespace.ToDisplayString());

            return tag.ToString();
        }
    }
}
