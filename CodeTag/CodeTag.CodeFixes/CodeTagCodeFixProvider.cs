using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTag
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CodeTagCodeFixProvider)), Shared]
    public class CodeTagCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create("CT001");

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf()
                .OfType<MemberDeclarationSyntax>().First();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Fix CodeTags",
                    createChangedDocument: c => FixCodeTagsAsync(context.Document, declaration, c),
                    equivalenceKey: nameof(CodeTagCodeFixProvider)),
                diagnostic);
        }


        private async Task<Document> FixCodeTagsAsync(Document document, MemberDeclarationSyntax declaration, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var symbol = ModelExtensions.GetDeclaredSymbol(semanticModel, declaration);
            var requiredTags = CodeTagAnalyzer.GetContainedDefineCodeTags(symbol, semanticModel.Compilation);

            requiredTags = requiredTags.OrderBy(tag => tag.Length).ThenBy(tag => tag).ToList();

            var attributes = requiredTags.Select(tag =>
                SyntaxFactory.Attribute(SyntaxFactory.ParseName("CodeTag"),
                SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.AttributeArgument(SyntaxFactory.ParseExpression($"\"{tag}\"")))))).ToArray();

            var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList(attributes));

            var newDeclaration = declaration.WithAttributeLists(SyntaxFactory.SingletonList(attributeList));

            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = oldRoot.ReplaceNode(declaration, newDeclaration);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
