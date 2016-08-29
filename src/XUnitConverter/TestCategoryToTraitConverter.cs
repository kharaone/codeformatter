// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace XUnitConverter
{
    public sealed class TestCategoryConverter : ConverterBase
    {
        private readonly TestCategoryRewriter _rewriter = new TestCategoryRewriter();

        protected override Task<Solution> ProcessAsync(Document document, SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            var newNode = _rewriter.Visit(syntaxNode);
            if (newNode != syntaxNode)
            {
                document = document.WithSyntaxRoot(newNode);
            }

            return Task.FromResult(document.Project.Solution);
        }

        internal sealed class TestCategoryRewriter : CSharpSyntaxRewriter
        {
            public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax syntaxNode)
            {
                var newAttributes = new SyntaxList<AttributeListSyntax>();

                foreach (var attributeList in syntaxNode.AttributeLists)
                {
                    var nodeToUpdate =
                    attributeList
                    .Attributes
                    .FirstOrDefault(attribute => attribute.Name.ToString().Equals("TestCategory"));

                    if (nodeToUpdate != null)
                    {
                        var categoryName = nodeToUpdate.ArgumentList.Arguments.ToString();
                        var traitAttribute =
                            nodeToUpdate.WithName(ParseName("Trait"))
                                .WithArgumentList(ParseAttributeArgumentList(string.Concat("(",@"""Category"",", categoryName,@")")));

                        var newAttribute =
                            (AttributeListSyntax)VisitAttributeList(
                                attributeList.ReplaceNode(nodeToUpdate, traitAttribute));

                        newAttributes = newAttributes.Add(newAttribute);
                    }
                    else
                    {
                        newAttributes = newAttributes.Add(attributeList);
                    }
                }

                //Get the leading trivia (the newlines and comments)
                var leadTriv = syntaxNode.GetLeadingTrivia();
                syntaxNode = syntaxNode.WithAttributeLists(newAttributes);
                //Append the leading trivia to the method
                syntaxNode = syntaxNode.WithLeadingTrivia(leadTriv);
                return syntaxNode;
            }
        }
    }
}
