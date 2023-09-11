using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace CodeTag
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CodeTagAnalyzer : DiagnosticAnalyzer
    {
        private static readonly HashSet<string> CodeTagAttributes = new()
        {
            "CodeTag",
            "CodeTagAttribute",
        };

        private static readonly HashSet<string> DefineCodeTagAttributes = new()
        {
            "DefineCodeTag",
            "DefineCodeTagAttribute"
        };

        private static readonly IReadOnlyList<string> NoTags = new List<string>().AsReadOnly();
        private static readonly HashSet<ISymbol> NoSymbols = new(SymbolEqualityComparer.Default);
        private static readonly ConcurrentSparseValueCache<ISymbol, IReadOnlyList<string>> TagCache = new(NoTags, SymbolEqualityComparer.Default);
        private static readonly ConcurrentSparseValueCache<ISymbol, HashSet<ISymbol>> SymbolsContainedInSymbolCache = new(NoSymbols, SymbolEqualityComparer.Default);
        private static readonly ConcurrentHashSet<ISymbol> SymbolsAlreadyAnalyzed = new(SymbolEqualityComparer.Default);

        private static readonly ConcurrentDictionary<ISymbol, string> DefineTagNames =
            new(SymbolEqualityComparer.Default);

        internal static bool IsCodeTagAttribute(string? name) => name is not null && CodeTagAttributes.Contains(name);
        internal static bool IsDefineCodeTag(string? name) => name is not null && DefineCodeTagAttributes.Contains(name);


        internal static readonly DiagnosticDescriptor CodeTagRule =
            new("CT001",
                title: "CodeTag compliance",
                messageFormat: "Element '{0}': {1} {2}",
                category: "Tagging",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "Within a CodeTag-enabled class, method, or property, a CodeTagAttribute must be applied for each directly or indirectly referenced DefineCodeTagAttribute.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(CodeTagRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Method, SymbolKind.NamedType, SymbolKind.Property);
        }

        private void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            ISymbol symbol = context.Symbol;

            if (!HasEnableCodeTagAttribute(symbol)) return;

            if (symbol is INamedTypeSymbol namedTypeSymbol)
            {
                AnalyzeNamedTypeSymbol(namedTypeSymbol, context);
                return;
            }
            AnalyzeIndividualSymbol(symbol, context);
        }

        private bool HasEnableCodeTagAttribute(ISymbol symbol)
        {
            return symbol.GetAttributes().Any(attr => attr.AttributeClass.Name == "EnableCodeTag" || attr.AttributeClass.Name == "EnableCodeTagAttribute");
        }

        private void AnalyzeNamedTypeSymbol(INamedTypeSymbol symbol, SymbolAnalysisContext context)
        {
            if (!SymbolsAlreadyAnalyzed.Add(symbol))
                return;

            foreach (var member in symbol.GetMembers().Where(member =>
                         member is IMethodSymbol || member is IPropertySymbol || member is INamedTypeSymbol))
            {
                if (member is INamedTypeSymbol namedTypeSymbol)
                {
                    AnalyzeNamedTypeSymbol(namedTypeSymbol, context);
                    continue;
                }

                if (!SymbolsAlreadyAnalyzed.Add(member))
                    continue;

                AnalyzeIndividualSymbol(member, context);
            }
        }

        private void AnalyzeIndividualSymbol(ISymbol symbol, SymbolAnalysisContext context)
        {
            // Get all CodeTag attributes applied directly to the element
            var codeTagsOnSymbol = CodeTagsCurrentlyOnSymbol(symbol);

            // Check for duplicates
            var distinctCodeTagsAppliedToThisSymbol = new HashSet<string>();

            foreach (var tag in codeTagsOnSymbol)
            {
                if (!distinctCodeTagsAppliedToThisSymbol.Add(tag))
                {
                    context.ReportDiagnostic(Diagnostic.Create(CodeTagRule, symbol.Locations[0], symbol.Name, "Duplicate Code Tag", tag));
                }
            }

            // Get all DefineCodeTag attributes from the referenced methods/properties but not on the symbol itself
            var containedDefineCodeTags = GetContainedDefineCodeTags(symbol, context.Compilation);

            // Exclude defined code tags on the current symbol from the set of tags to be checked for unnecessary tags
            var definedTagsOnCurrentSymbol = symbol
                .GetAttributes()
                .Where(a => IsDefineCodeTag(a.AttributeClass.Name))
                .Select(a => _tagFromAttribute(symbol, a))
                .Where(tag => tag is not null)
                .Aggregate(new HashSet<string>(), (acc, next) => { acc.Add(next); return acc; });

            // Reporting missing CodeTags
            foreach (var tag in containedDefineCodeTags.Except(distinctCodeTagsAppliedToThisSymbol))
            {
                context.ReportDiagnostic(Diagnostic.Create(CodeTagRule, symbol.Locations[0], symbol.Name, "Missing Code Tag", tag));
            }

            // Reporting unnecessary CodeTags
            foreach (var tag in distinctCodeTagsAppliedToThisSymbol.Except(containedDefineCodeTags.Union(definedTagsOnCurrentSymbol)))
            {
                context.ReportDiagnostic(Diagnostic.Create(CodeTagRule, symbol.Locations[0], symbol.Name, "Unnecessary Code Tag", tag));
            }
        }

        internal static IReadOnlyList<string> GetContainedDefineCodeTags(ISymbol symbol, Compilation compilation) =>
            _getReferencedTags(symbol, compilation)
                .Except(symbol.GetAttributes()
                    .Where(a => IsDefineCodeTag(a.AttributeClass.Name))
                    .Select(a => _tagFromAttribute(symbol, a)))
                .ToList()
                .AsReadOnly();

        internal static IReadOnlyList<string> CodeTagsCurrentlyOnSymbol(ISymbol symbol) =>
            symbol
                .GetAttributes()
                .Where(a => IsCodeTagAttribute(a.AttributeClass.Name))
                .Select(a => _tagFromAttribute(symbol, a))
                .Where(tag => tag is not null)
                .ToList()
                .AsReadOnly();

        private static IEnumerable<ISymbol> _extractSymbolsFromLambdaOrAnonymous(SyntaxNode node, SemanticModel semanticModel)
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

        private static HashSet<ISymbol> _gatherReferencedSymbols(ISymbol symbol, Compilation compilation)
        {
            if (SymbolsContainedInSymbolCache.TryGetValue(symbol, out var cachedResult))
                return cachedResult;

            HashSet<ISymbol> referencedSymbols = new(SymbolEqualityComparer.Default);

            foreach (var syntaxRef in symbol.DeclaringSyntaxReferences)
            {
                var semanticModel = compilation.GetSemanticModel(syntaxRef.SyntaxTree);
                var nodes = syntaxRef.GetSyntax().DescendantNodes();

                var refSymbols = nodes
                    .Select(node => semanticModel.GetSymbolInfo(node).Symbol)
                    .Where(s => s is IMethodSymbol || s is IPropertySymbol)
                    .ToList();

                var lambdaSymbols = nodes
                    .Where(n => n is LambdaExpressionSyntax || n is AnonymousMethodExpressionSyntax)
                    .SelectMany(n => _extractSymbolsFromLambdaOrAnonymous(n, semanticModel))
                    .ToList();

                foreach (var refSymbol in refSymbols.Concat(lambdaSymbols))
                {
                    var deeperReferences = _gatherReferencedSymbols(refSymbol, compilation);
                    referencedSymbols.UnionWith(deeperReferences);
                    referencedSymbols.Add(refSymbol);
                }
            }

            SymbolsContainedInSymbolCache.Add(symbol, referencedSymbols);

            return referencedSymbols;
        }

        private static HashSet<string> _getReferencedTags(ISymbol symbol, Compilation compilation)
        {
            var references = _gatherReferencedSymbols(symbol, compilation);

            var seenTags = new HashSet<string>();
            return references
                .SelectMany(refSymbol => TagCache.GetValue(
                    refSymbol,
                    () => refSymbol
                        .GetAttributes()
                        .Where(a => IsDefineCodeTag(a.AttributeClass.Name))
                        .Select(a => _tagFromAttribute(refSymbol, a))
                        .Where(tag => tag is not null)
                        .ToList()))
                .Where(tag => seenTags.Add(tag))
                .Aggregate(new HashSet<string>(), (acc, next) =>
                {
                     acc.Add(next);
                     return acc;
                });
        }

        private static string _tagFromAttribute(ISymbol refSymbol, AttributeData a)
        {
            string tag = GetTagKey(a, refSymbol);
            if (!string.IsNullOrWhiteSpace(tag)) return tag;
            return null!;
        }

        internal static string GetTagKey(AttributeData attribute, ISymbol appliedToSymbol)
        {
            if (DefineTagNames.TryGetValue(appliedToSymbol, out var result))
                return result;

            string tag = default!;
            if (attribute.ConstructorArguments.Length == 1)
            {
                tag = (string)attribute.ConstructorArguments[0].Value;
            }
            else
            {
                tag = _generateTagKey(appliedToSymbol);
            }

            DefineTagNames.TryAdd(appliedToSymbol, tag);
            return tag;
        }

        private const int DefaultTagSize = 32;

        private static string _generateTagKey(ISymbol symbol)
        {
            var tag = new StringBuilder(symbol.Name, DefaultTagSize);

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
