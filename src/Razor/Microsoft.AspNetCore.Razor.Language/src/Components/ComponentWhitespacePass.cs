﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.Components
{
    internal class ComponentWhitespacePass : ComponentIntermediateNodePassBase, IRazorDirectiveClassifierPass
    {
        protected override void ExecuteCore(RazorCodeDocument codeDocument, DocumentIntermediateNode documentNode)
        {
            if (codeDocument == null)
            {
                throw new ArgumentNullException(nameof(codeDocument));
            }

            if (documentNode == null)
            {
                throw new ArgumentNullException(nameof(documentNode));
            }

            if (!IsComponentDocument(documentNode))
            {
                return;
            }

            if (documentNode.Options.SuppressPrimaryMethodBody)
            {
                // There's no benefit running the whitespace trimmer if we're not emitting
                // the method bodies.
                return;
            }

            // There's no benefit running the whitespace trimmer during design-time builds
            if (documentNode.Options.DesignTime)
            {
                return;
            }

            // Respect @preservewhitespace directives
            if (PreserveWhitespaceIsEnabled(documentNode))
            {
                return;
            }

            var method = documentNode.FindPrimaryMethod();
            if (method != null)
            {
                RemoveContiguousWhitespace(method.Children, TraversalDirection.Forwards);
                RemoveContiguousWhitespace(method.Children, TraversalDirection.Backwards);

                var visitor = new Visitor();
                visitor.Visit(method);
            }
        }

        private static bool PreserveWhitespaceIsEnabled(DocumentIntermediateNode documentNode)
        {
            // If there's no @preservewhitespace attribute, the default is that we *don't* preserve whitespace
            var shouldPreserveWhitespace = false;

            foreach (var preserveWhitespaceDirective in documentNode.FindDirectiveReferences(ComponentPreserveWhitespaceDirective.Directive))
            {
                var token = ((DirectiveIntermediateNode)preserveWhitespaceDirective.Node).Tokens.FirstOrDefault();
                var shouldPreserveWhitespaceContent = token?.Content;
                if (shouldPreserveWhitespaceContent != null)
                {
                    shouldPreserveWhitespace = string.Equals(shouldPreserveWhitespaceContent, "true", StringComparison.Ordinal);
                }
            }

            return shouldPreserveWhitespace;
        }

        private static int RemoveContiguousWhitespace(IntermediateNodeCollection nodes, TraversalDirection direction, int? startIndex = null)
        {
            var position = startIndex.GetValueOrDefault(direction == TraversalDirection.Forwards ? 0 : nodes.Count - 1);
            var countRemoved = 0;
            while (position >= 0 && position < nodes.Count)
            {
                var node = nodes[position];
                bool shouldRemoveNode;
                bool shouldContinueIteration;

                switch (node)
                {
                    case IntermediateToken intermediateToken:
                        shouldRemoveNode = string.IsNullOrWhiteSpace(intermediateToken.Content);
                        shouldContinueIteration = shouldRemoveNode;
                        break;

                    case HtmlContentIntermediateNode htmlContentIntermediateNode:
                        RemoveContiguousWhitespace(htmlContentIntermediateNode.Children, direction);
                        shouldRemoveNode = htmlContentIntermediateNode.Children.Count == 0;
                        shouldContinueIteration = shouldRemoveNode;
                        break;

                    case MarkupElementIntermediateNode _:
                    case CSharpExpressionIntermediateNode _:
                    case TagHelperIntermediateNode _:
                        // These node types may produce non-whitespace output at runtime
                        shouldRemoveNode = false;
                        shouldContinueIteration = false;
                        break;

                    case CSharpCodeIntermediateNode _:
                        shouldRemoveNode = false;
                        shouldContinueIteration = false;
                        break;

                    default:
                        shouldRemoveNode = false;
                        shouldContinueIteration = true; // Because other types of nodes don't produce output
                        break;
                }

                if (shouldRemoveNode)
                {
                    nodes.RemoveAt(position);
                    countRemoved++;
                    if (direction == TraversalDirection.Forwards)
                    {
                        position--;
                    }
                }

                position += direction == TraversalDirection.Forwards ? 1 : -1;

                if (!shouldContinueIteration)
                {
                    break;
                }
            }

            return countRemoved;
        }

        enum TraversalDirection
        {
            Forwards,
            Backwards
        }

        class Visitor : IntermediateNodeWalker
        {
            public override void VisitMarkupElement(MarkupElementIntermediateNode node)
            {
                RemoveContiguousWhitespace(node.Children, TraversalDirection.Forwards);
                RemoveContiguousWhitespace(node.Children, TraversalDirection.Backwards);
                VisitDefault(node);
            }

            public override void VisitTagHelperBody(TagHelperBodyIntermediateNode node)
            {
                // The goal here is to remove leading/trailing whitespace inside component child content. However,
                // at the time this whitespace pass runs, ComponentChildContent is still TagHelperBody in the tree.
                RemoveContiguousWhitespace(node.Children, TraversalDirection.Forwards);
                RemoveContiguousWhitespace(node.Children, TraversalDirection.Backwards);
                VisitDefault(node);
            }

            public override void VisitDefault(IntermediateNode node)
            {
                // For any CSharpCodeIntermediateNode children, remove their preceding and trailing whitespace
                for (var childIndex = 0; childIndex < node.Children.Count; childIndex++)
                {
                    if (node.Children[childIndex] is CSharpCodeIntermediateNode)
                    {
                        childIndex -= RemoveContiguousWhitespace(node.Children, TraversalDirection.Backwards, childIndex - 1);
                        RemoveContiguousWhitespace(node.Children, TraversalDirection.Forwards, childIndex + 1);
                    }
                }

                base.VisitDefault(node);
            }
        }
    }
}
