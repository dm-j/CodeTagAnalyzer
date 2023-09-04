using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CodeTag
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CodeTagCodeFixProvider)), Shared]
    public class CodeTagCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => 
            ImmutableArray.Create("CT003");

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            if (root.FindNode(diagnosticSpan).FirstAncestorOrSelf<MethodDeclarationSyntax>() is not MethodDeclarationSyntax methodDeclaration)
                return;

            var attributeList = methodDeclaration.AttributeLists
                                                 .SelectMany(al => al.Attributes)
                                                 .Where(attr => CodeTagAnalyzer.IsCodeTagAttribute(attr.Name.ToString()))
                                                 .ToList();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Remove duplicate CodeTags",
                    createChangedDocument: cancellationToken => _removeDuplicateCodeTagsAsync(context.Document, attributeList, cancellationToken),
                    equivalenceKey: nameof(CodeTagCodeFixProvider)),
                diagnostic);
        }

        private async Task<Document> _removeDuplicateCodeTagsAsync(Document document, List<AttributeSyntax> attributeList, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var attributesToRemove = attributeList
                .Select(attributeSyntax => new
                {
                    AttributeSyntax = attributeSyntax,
                    AttributeSymbol = semanticModel.GetSymbolInfo(attributeSyntax, cancellationToken).Symbol as IMethodSymbol,
                    AttributeDataSymbol = semanticModel.GetDeclaredSymbol(attributeSyntax.Parent.Parent, cancellationToken)
                })
                .Where(data => (CodeTagAnalyzer.IsCodeTagAttribute(data.AttributeSymbol?.ContainingType.Name)) && data.AttributeDataSymbol is not null)
                .Select(data => new
                {
                    data.AttributeSyntax,
                    Key = CodeTagAnalyzer.GetTagKey(data.AttributeDataSymbol.GetAttributes().First(a => CodeTagAnalyzer.IsCodeTagAttribute(a.AttributeClass.Name)), data.AttributeDataSymbol)
                })
                .GroupBy(data => data.Key)
                .SelectMany(group => group
                    .OrderBy(data => data.AttributeSyntax.ArgumentList?.Arguments.Count ?? 0)
                    .ThenBy(data => data.AttributeSyntax.Name.ToString())
                    .Skip(1))
                .ToList();

            var newRoot = root;

            var nodesToRemove = attributesToRemove.Select(attributeData => attributeData.AttributeSyntax).ToList();

            newRoot = newRoot.RemoveNodes(nodesToRemove, SyntaxRemoveOptions.KeepNoTrivia);

            var emptyAttributeLists = newRoot.DescendantNodes()
                .OfType<AttributeListSyntax>()
                .Where(al => !al.Attributes.Any());

            newRoot = newRoot.RemoveNodes(emptyAttributeLists, SyntaxRemoveOptions.KeepNoTrivia);

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
