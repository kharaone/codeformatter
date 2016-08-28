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
    public sealed class IgnoreToSkipConverter : ConverterBase
    {
        private readonly IgnoreToSkipRewriter _rewriter = new IgnoreToSkipRewriter();

        protected override Task<Solution> ProcessAsync(Document document, SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            var newNode = _rewriter.Visit(syntaxNode);
            if (newNode != syntaxNode)
            {
                document = document.WithSyntaxRoot(newNode);
            }

            return Task.FromResult(document.Project.Solution);
        }

        internal sealed class IgnoreToSkipRewriter : CSharpSyntaxRewriter
        {

            public bool IsClassIgnored { get; set; }

            public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax syntaxNode)
            {
                //bool? isAssertTF = IsAssertTrueOrFalse(syntaxNode);
                //if (isAssertTF != null)
                //{
                //    string firstArg = null, secondArg = null;

                //    var expr = syntaxNode.Expression as InvocationExpressionSyntax;
                //    var firstArgNode = expr.ArgumentList.Arguments.First().Expression;

                //    if (firstArgNode.IsKind(SyntaxKind.LogicalNotExpression))
                //    {
                //        // revert True and False
                //        string fmt = isAssertTF.Value ? AssertFalseNoMsg : AssertTrueNoMsg;
                //        // the first char should be !
                //        // the comments associated with this arg will be lost
                //        firstArg = firstArgNode.ToString().Trim().Substring(1);
                //        if (expr.ArgumentList.Arguments.Count == 2)
                //        {
                //            secondArg = expr.ArgumentList.Arguments.Last().ToString().Trim();
                //            fmt = isAssertTF.Value ? AssertFalse : AssertTrue;
                //        }

                //        return SyntaxFactory.ParseStatement(syntaxNode.GetLeadingTrivia().ToFullString() +
                //            string.Format(fmt, firstArg, secondArg) +
                //            syntaxNode.GetTrailingTrivia().ToFullString());
                //    }
                //    else if (firstArgNode.IsKind(SyntaxKind.EqualsExpression) || firstArgNode.IsKind(SyntaxKind.NotEqualsExpression))
                //    {
                //        BinaryExpressionSyntax expr2 = firstArgNode as BinaryExpressionSyntax;
                //        firstArg = expr2.Left.ToString().Trim();
                //        secondArg = expr2.Right.ToString().Trim();

                //        bool isEqual = firstArgNode.IsKind(SyntaxKind.EqualsExpression);
                //        // Assert.True(a==b) || Assert.False(a!=b)
                //        bool positive = isAssertTF.Value && isEqual || !(isAssertTF.Value || isEqual);
                //        var fmt = positive ? AssertEqual : AssertNotEqual;

                //        // special case
                //        if (IsSpecialValue(ref firstArg, ref secondArg, "null"))
                //        {
                //            // Assert.True(cond ==|!= null) || Assert.False(cond ==|!= null)
                //            fmt = positive ? AssertNull : AssertNotNull;
                //        }
                //        else if (IsSpecialValue(ref firstArg, ref secondArg, "true"))
                //        {
                //            // Assert.True(cond ==|!= true) || Assert.False(cond ==|!= true)
                //            fmt = positive ? AssertTrueNoMsg : AssertFalseNoMsg;
                //        }
                //        else if (IsSpecialValue(ref firstArg, ref secondArg, "false"))
                //        {
                //            // Assert.True(cond ==|!= false) || Assert.False(cond ==|!= false)
                //            fmt = positive ? AssertFalseNoMsg : AssertTrueNoMsg;
                //        }
                //        else
                //        {
                //            int v = 0;
                //            // if second is a const (int only for now)
                //            if (int.TryParse(secondArg, out v))
                //            {
                //                // swap
                //                string tmp = firstArg;
                //                firstArg = secondArg;
                //                secondArg = tmp;
                //            }
                //        }

                //        return SyntaxFactory.ParseStatement(
                //                syntaxNode.GetLeadingTrivia().ToFullString() +
                //                string.Format(fmt, firstArg, secondArg) +
                //                syntaxNode.GetTrailingTrivia().ToFullString());
                //    }
                //}

                return base.VisitExpressionStatement(syntaxNode);
            }

            public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax syntaxNode)
            {
                var newAttributes = new SyntaxList<AttributeListSyntax>();
                var isMethodIgnored = false;

                foreach (var attributeList in syntaxNode.AttributeLists)
                {
                    var nodesToRemove =
                        attributeList
                            .Attributes
                            .Where(attribute => attribute.Name.ToString().Equals("Ignore"))
                            .ToArray();

                    //If the lists are the same length, we are removing all attributes and can just avoid populating newAttributes.
                    if(nodesToRemove.Length >0 ) isMethodIgnored = true;
                    if (nodesToRemove.Length != attributeList.Attributes.Count)
                    {
                        var newAttribute =
                            (AttributeListSyntax) VisitAttributeList(
                                attributeList.RemoveNodes(nodesToRemove, SyntaxRemoveOptions.KeepNoTrivia));

                        newAttributes = newAttributes.Add(newAttribute);
                    }
                    
                }

                //Get the leading trivia (the newlines and comments)
                var leadTriv = syntaxNode.GetLeadingTrivia();
                syntaxNode = syntaxNode.WithAttributeLists(newAttributes);
                //Append the leading trivia to the method
                syntaxNode = syntaxNode.WithLeadingTrivia(leadTriv);

                newAttributes = new SyntaxList<AttributeListSyntax>();

                foreach (var attributeList in syntaxNode.AttributeLists)
                {
                    var nodeToUpdate =
                    attributeList
                    .Attributes
                    .FirstOrDefault(attribute => attribute.Name.ToString().Equals("Fact"));

                    if (nodeToUpdate != null && (isMethodIgnored|| IsClassIgnored))
                    {
                        var skippedAttribute = nodeToUpdate.WithArgumentList(
                                AttributeArgumentList(
                                    new SeparatedSyntaxList<AttributeArgumentSyntax>().Add(
                                        AttributeArgument(NameEquals("Skip"), null,
                                            LiteralExpression(SyntaxKind.StringLiteralExpression,
                                                Token(TriviaList(), SyntaxKind.StringLiteralToken,
                                                    @"""Ignored in MSTest""", "Ignored in MSTest", TriviaList()))))));
                        var newAttribute =
                            (AttributeListSyntax)VisitAttributeList(
                                attributeList.ReplaceNode(nodeToUpdate, skippedAttribute));

                        newAttributes = newAttributes.Add(newAttribute);
                    }
                    else
                    {
                        newAttributes = newAttributes.Add(attributeList);
                    }
                }

                //Get the leading trivia (the newlines and comments)
                leadTriv = syntaxNode.GetLeadingTrivia();
                syntaxNode = syntaxNode.WithAttributeLists(newAttributes);
                //Append the leading trivia to the method
                syntaxNode = syntaxNode.WithLeadingTrivia(leadTriv);
                return syntaxNode;
            }

            public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax syntaxNode)
            {
                IsClassIgnored =
                    syntaxNode.AttributeLists.Any(
                        attributeList => attributeList.Attributes.Any(a => a.Name.ToString().Equals("Ignore")));

                var newAttributes = new SyntaxList<AttributeListSyntax>();
             

                foreach (var attributeList in syntaxNode.AttributeLists)
                {
                    var nodesToRemove =
                        attributeList
                            .Attributes
                            .Where(attribute => attribute.Name.ToString().Equals("Ignore"))
                            .ToArray();

                    if (nodesToRemove.Length != attributeList.Attributes.Count)
                    {
                        var newAttribute =
                            (AttributeListSyntax)VisitAttributeList(
                                attributeList.RemoveNodes(nodesToRemove, SyntaxRemoveOptions.KeepNoTrivia));

                        newAttributes = newAttributes.Add(newAttribute);
                    }

                }

                //Get the leading trivia (the newlines and comments)
                var leadTriv = syntaxNode.GetLeadingTrivia();
                syntaxNode = syntaxNode.WithAttributeLists(newAttributes);
                //Append the leading trivia to the method
                syntaxNode = syntaxNode.WithLeadingTrivia(leadTriv);
                return base.VisitClassDeclaration(syntaxNode);
            }

            #region "Helper"

            public const string AssertTrue = "Assert.True({0}, {1});";
            public const string AssertFalse = "Assert.False({0}, {1});";
            public const string AssertTrueNoMsg = "Assert.True({0});";
            public const string AssertFalseNoMsg = "Assert.False({0});";
            public const string AssertEqual = "Assert.Equal({0}, {1});";
            public const string AssertNotEqual = "Assert.NotEqual({0}, {1});";
            public const string AssertNull = "Assert.Null({0});";
            public const string AssertNotNull = "Assert.NotNull({0});";

            public static bool IsSpecialValue(ref string first, ref string second, string cond)
            {
                if (first == cond || second == cond)
                {
                    if (first == cond)
                        first = second;

                    second = null;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="node"></param>
            /// <returns>true=Assert.True; false=Assert.False; null=Neither </returns>
            public static bool? IsAssertTrueOrFalse(SyntaxNode node)
            {
                if (node != null && node.IsKind(SyntaxKind.ExpressionStatement))
                {
                    var invoke = (node as ExpressionStatementSyntax).Expression as InvocationExpressionSyntax;
                    if (invoke != null)
                    {
                        var expr = invoke.Expression as MemberAccessExpressionSyntax;
                        if (!(expr == null || expr.Name == null || expr.Expression == null))
                        {
                            var id = expr.Name.Identifier.ToString().Trim();
                            var caller = expr.Expression.ToString().Trim();

                            if (caller == "Assert")
                            {
                                if (id == "True")
                                    return true;
                                if (id == "False")
                                    return false;
                            }
                        }
                    }
                }

                return null;
            }

            #endregion
        }
    }
}
