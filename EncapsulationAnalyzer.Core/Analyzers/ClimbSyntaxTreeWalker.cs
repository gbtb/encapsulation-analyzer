using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EncapsulationAnalyzer.Core.Analyzers
{

    /// <summary>
    /// Syntax walker impl used to ascend syntax tree. From type references inside type declaration to type declaration itself.
    /// Aimed to find public members which uses "probably-internal" other type
    /// </summary>
    internal class ClimbSyntaxTreeWalker: CSharpSyntaxWalker
    {
        private bool _isPublicMember;
        public bool Result { get; private set; }

        public override void DefaultVisit(SyntaxNode node)
        {
            if (node != null)
                Visit(node.Parent);
        }

        public override void Visit(SyntaxNode node)
        {
            if (node is InterfaceDeclarationSyntax interfaceDeclarationSyntax)
            {
                Result = interfaceDeclarationSyntax.Modifiers.Any(SyntaxKind.PublicKeyword);
                return;
            }
            
            if (node is TypeDeclarationSyntax typeDeclaration)
            {
                Result = typeDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword) && _isPublicMember;
                return;
            }
            
            base.Visit(node);
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            if (node.Modifiers.Any(SyntaxKind.PublicKeyword))
            {
                _isPublicMember = true;
                Visit(node.Parent);
            }
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            _isPublicMember = node.Modifiers.Any(SyntaxKind.PublicKeyword);
            Visit(node.Parent);
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            _isPublicMember = node.Modifiers.Any(SyntaxKind.PublicKeyword);
            Visit(node.Parent);
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            if (node.Modifiers.Any(SyntaxKind.PublicKeyword))
                Visit(node.Parent);
        }
    }
}