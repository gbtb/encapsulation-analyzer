using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EncapsulationAnalyzer.Core.Fixes
{
    internal class AccesibilityRewriter : CSharpSyntaxRewriter
    {
        private readonly IEnumerable<MemberDeclarationSyntax> _declarationsToFix;

        public AccesibilityRewriter(IEnumerable<SyntaxNode> declarationsToFix)
        {
            _declarationsToFix = declarationsToFix.OfType<MemberDeclarationSyntax>().ToList();
        }

        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            if (!_declarationsToFix.Any(d => d.IsEquivalentTo(node)))
                return base.VisitClassDeclaration(node);
            
            var publicKeyword = FindPublicKeyword(node);
            if (publicKeyword.IsMissing)
                return base.VisitClassDeclaration(node);
            
            return ReplaceWithInternalKeyword(node, publicKeyword);
        }

        public override SyntaxNode? VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            if (!_declarationsToFix.Any(d => d.IsEquivalentTo(node)))
                return base.VisitEnumDeclaration(node);
            
            var publicKeyword = FindPublicKeyword(node);
            if (publicKeyword.IsMissing)
                return base.VisitEnumDeclaration(node);
            
            return ReplaceWithInternalKeyword(node, publicKeyword);
        }

        public override SyntaxNode? VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            if (!_declarationsToFix.Any(d => d.IsEquivalentTo(node)))
                return base.VisitInterfaceDeclaration(node);
            
            var publicKeyword = FindPublicKeyword(node);
            if (publicKeyword.IsMissing)
                return base.VisitInterfaceDeclaration(node);
            
            return ReplaceWithInternalKeyword(node, publicKeyword);
        }

        public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node)
        {
            if (!_declarationsToFix.Any(d => d.IsEquivalentTo(node)))
                return base.VisitRecordDeclaration(node);
            
            var publicKeyword = FindPublicKeyword(node);
            if (publicKeyword.IsMissing)
                return base.VisitRecordDeclaration(node);
            
            return ReplaceWithInternalKeyword(node, publicKeyword);
        }
        
        private static SyntaxNode? ReplaceWithInternalKeyword<TDeclaration>(TDeclaration node, SyntaxToken publicKeyword) where TDeclaration: MemberDeclarationSyntax
        {
            var internalKeyword = SyntaxFactory.Token(publicKeyword.LeadingTrivia, SyntaxKind.InternalKeyword,
                publicKeyword.TrailingTrivia);
            return node.WithModifiers(node.Modifiers.Replace(publicKeyword, internalKeyword));
        }

        private static SyntaxToken FindPublicKeyword(MemberDeclarationSyntax node)
        {
            var publicKeywordIdx = node.Modifiers.IndexOf(SyntaxKind.PublicKeyword);
            return publicKeywordIdx > -1 ? node.Modifiers[publicKeywordIdx] : SyntaxFactory.MissingToken(SyntaxKind.PublicKeyword);
        }
    }
}