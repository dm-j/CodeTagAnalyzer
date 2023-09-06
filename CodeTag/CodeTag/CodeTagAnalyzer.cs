using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
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

        private static readonly IReadOnlyList<(string tag, bool novel)> _empty = new List<(string tag, bool novel)>().AsReadOnly();
        private static readonly ConcurrentSparseValueCache<ISymbol, IReadOnlyList<(string tag, bool novel)>> _tagCache = new(new List<(string tag, bool novel)>().AsReadOnly(), SymbolEqualityComparer.Default);

        internal static bool IsCodeTagAttribute(string? name) => name is not null && _codeTagAttributes.Contains(name);
        internal static bool IsDefineCodeTag(string? name) => name is not null && _defineCodeTagAttributes.Contains(name);


        private static readonly DiagnosticDescriptor _missingTagRule =
            new("CT001",
                title: "Missing CodeTag",
                messageFormat: @"Method or property '{0}' must have [CodeTag(""{1}"")]",
                category: "Tagging",
                defaultSeverity: DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "Methods or properties referencing another method or property with a CodeTag must also have a CodeTag with the same key.");

        private static readonly DiagnosticDescriptor _unnecessaryTagRule =
            new(
                "CT002",
                "Unnecessary CodeTag",
                @"Unnecessary CodeTag [CodeTag(""{0}"")] on '{1}'",
                "Tagging",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "CodeTags should only be applied where necessary. New CodeTags are introduced ");

        private static readonly DiagnosticDescriptor _duplicateTagRule = 
            new(
                "CT003",
                "Duplicate CodeTag",
                @"Duplicate CodeTag [CodeTag(""{0}"")] on '{1}'",
                "Tagging",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "CodeTags on a single element must be unique.");

        private static readonly DiagnosticDescriptor _invalidTagRule =
            new(
                "CT004",
                "Invalid CodeTag",
                @"Invalid CodeTag [CodeTag(""{0}"")] on '{1}'",
                "Tagging",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true,
                description: "CodeTags cannot be null, empty, or whitespace.");


        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(_missingTagRule, _unnecessaryTagRule, _duplicateTagRule, _invalidTagRule); } }

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
                                    .Where(tag => tag.tag is not null)
                                    .ToList());

            // Contains tags that the current element must include based on its references to other tagged elements
            HashSet<string> necessaryTags = new();

            // Contains tags that the current element has but may not be required based on its current implementation
            HashSet<string> unnecessaryTags = new();

            var duplicateTags = currentCodeTags.GroupBy(x => x.tag).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            foreach (var tag in duplicateTags)
            {
                var diagnostic = Diagnostic.Create(_duplicateTagRule, symbol.Locations[0], tag, symbol.Name);
                context.ReportDiagnostic(diagnostic);
            }

            foreach (var (tag, novel) in currentCodeTags)
            {
                if (novel) continue;

                necessaryTags.Add(tag);
                unnecessaryTags.Add(tag);
            }

            var usedTags = _getUsedTags(symbol, context.Compilation, context);

            foreach (var usedTag in usedTags.Select(tag => tag.tag).Distinct())
            {
                unnecessaryTags.Remove(usedTag);
                if (!necessaryTags.Contains(usedTag))
                {
                    var diagnostic = Diagnostic.Create(_missingTagRule, symbol.Locations[0], symbol.Name, usedTag);
                    context.ReportDiagnostic(diagnostic);
                }
            }

            foreach (var unnecessaryTag in unnecessaryTags)
            {
                var diagnostic = Diagnostic.Create(_unnecessaryTagRule, symbol.Locations[0], unnecessaryTag, symbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private IEnumerable<ISymbol> _extractSymbolsFromLambdaOrAnonymous(SyntaxNode node, SemanticModel semanticModel) =>
            node switch
            {
                LambdaExpressionSyntax or AnonymousMethodExpressionSyntax => 
                    node.DescendantNodes()
                        .Select(n => semanticModel.GetSymbolInfo(n).Symbol)
                        .Where(s => s != null),
                _ => 
                    Enumerable.Empty<ISymbol>()
            };

        private IReadOnlyList<(string tag, bool novel)> _getUsedTags(ISymbol symbol, Compilation compilation, SymbolAnalysisContext context)
        {
            var syntaxRefs = symbol.DeclaringSyntaxReferences;
            if (syntaxRefs.Length == 0)
            {
                return _empty;
            }

            var references = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            foreach (var syntaxRef in syntaxRefs)
            {
                var semanticModel = compilation.GetSemanticModel(syntaxRef.SyntaxTree);
                var nodes = syntaxRef.GetSyntax().DescendantNodes();
                var refSymbols = nodes
                                    .Select(node => semanticModel.GetSymbolInfo(node).Symbol)
                                    .Where(s => s is not null);
                var lambdaSymbols = nodes
                                    .Where(n => n is LambdaExpressionSyntax or AnonymousMethodExpressionSyntax)
                                    .SelectMany(n => _extractSymbolsFromLambdaOrAnonymous(n, semanticModel));

                references.UnionWith(refSymbols);
                references.UnionWith(lambdaSymbols);
            }

            var seenTags = new HashSet<string>();
            return references
                .SelectMany(refSymbol => _tagCache.GetValue(
                    refSymbol,
                    () => refSymbol
                        .GetAttributes()
                        .Where(a => IsCodeTagAttribute(a.AttributeClass.Name))
                        .Select(a => _tagFromAttribute(context, refSymbol, a))
                        .Where(tag => tag.tag is not null)
                        .ToList()))
                .Where(pair => seenTags.Add(pair.tag))
                .ToList();
        }

        private (string tag, bool novel) _tagFromAttribute(SymbolAnalysisContext context, ISymbol refSymbol, AttributeData a)
        {
            string tag = GetTagKey(a, refSymbol);
            bool novel = IsDefineCodeTag(a.AttributeClass.Name);
            if (string.IsNullOrWhiteSpace(tag))
            {
                var diagnostic = Diagnostic.Create(_invalidTagRule, refSymbol.Locations[0], "<none>");
                context.ReportDiagnostic(diagnostic);
                return (null!, false);
            }
            return (tag, novel);
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

            if (symbol.ContainingNamespace is not null && !symbol.ContainingNamespace.IsGlobalNamespace)
            {
                tag.Insert(0, '.');
                tag.Insert(0, symbol.ContainingNamespace.ToDisplayString());
            }

            return tag.ToString();
        }
    }
}
