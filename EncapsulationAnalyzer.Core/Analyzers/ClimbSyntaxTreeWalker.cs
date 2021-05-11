using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EncapsulationAnalyzer.Core
{

    public class ClimbSyntaxTreeWalker: CSharpSyntaxWalker
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
                Visit(node.Parent);
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