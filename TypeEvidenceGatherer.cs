using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace Quack.Analysis
{
    class TypeEvidenceGatherer
    {
        private DeserializationCall deserCall;
        private Logger logger;

        private TypeEvidenceRuleSet ruleSet;

        private List<TypeEvidence> gatheredEvidence;
        private List<SyntaxNode> visitedNodes;

        public TypeEvidenceGatherer(DeserializationCall deserCall, Logger logger)
        {
            this.deserCall = deserCall;
            this.logger = logger;
            this.ruleSet = new TypeEvidenceRuleSet(deserCall.model, logger);
            this.gatheredEvidence = [];
        }


        private List<SyntaxNode> FindSymbolUsers(ISymbol variableSymbol)
        {
            var nodeList = new List<SyntaxNode>();
            // Find all references to the variable
            var references = SymbolFinder.FindReferencesAsync(variableSymbol, deserCall.project.Solution).Result;
            var referenceLocations = references.SelectMany(r => r.Locations).ToList();

            logger.Info("Found " + referenceLocations.Count() + " references to variable " + variableSymbol.Name);
            foreach (var location in referenceLocations)
            {
                logger.Info("Reference at " + location.Location.SourceTree.FilePath + ":" + (location.Location.GetLineSpan().StartLinePosition.Line + 1));
                // Convert location to node and add to list
                // TODO could do more complex dataflow analysis (i.e. exclude previous nodes in the same block)
                var referenceNode = location.Location.SourceTree.GetRoot().FindNode(location.Location.SourceSpan);
                logger.Info("User Node: " + MiscUtils.FirstNLines(referenceNode.ToString(), 3));

                logger.Assert(referenceNode.IsKind(SyntaxKind.IdentifierName), "Unhandled reference node kind in UserNodes(): " + referenceNode.Kind());


                nodeList.Add(referenceNode);
            }

            return nodeList;


        }

        /* For a given node, retrive all nodes which 'use' that node */
        /* TODO build out with more cases--currently adding case-by-case as they appear
        in test program */
        private List<SyntaxNode>? UserNodes(SyntaxNode node)
        {
            // TODO implement this
            if (node == null || node.Parent == null)
            {
                logger.Info("UserNodes() reached null node or parent");
                return [];
            }

            var nodeList = new List<SyntaxNode>();
            var nodeKind = node.Kind();

            logger.Info("Finding user nodes for node of kind " + nodeKind);

            switch (nodeKind)
            {
                // An argument node sohuld add the parameter node from the method definition
                case SyntaxKind.Argument:
                    logger.Info("Handling argument node");
                    // get the argument node and index
                    var argNode = (ArgumentSyntax)node;
                    var argNodeList = (ArgumentListSyntax)argNode.Parent;

                    // Get the index of the argument
                    var argIndex = argNodeList.Arguments.IndexOf(argNode);

                    logger.Info("Argument index: " + argIndex);

                    // Find the method node and symbol
                    var methodNode = (InvocationExpressionSyntax)argNodeList.Parent;
                    var methodSymbol = deserCall.model.GetSymbolInfo(methodNode).Symbol;

                    logger.Info("Caller method: " + methodSymbol.ToString());

                    // Find the method declaration
                    var syntaxReference = methodSymbol.DeclaringSyntaxReferences;
                    if (syntaxReference.Length == 0)
                    {
                        logger.Warn("Could not find method declaration (may be external method), so cannot restrict type further");
                        return null;
                    }

                    var declaration = syntaxReference.Single().GetSyntax() as MethodDeclarationSyntax;

                    // Find the parameter node
                    var paramNode = declaration.ParameterList.Parameters[argIndex];

                    logger.Assert(paramNode.IsKind(SyntaxKind.Parameter), "Computed parameter node is not a parameter node kind");

                    logger.Info("Found parameter node");

                    nodeList.Add(paramNode);
                    break;
                // TODO move localdeclarationstatement and variabledeclaration to common anonymous case if possible
                case SyntaxKind.LocalDeclarationStatement:
                    logger.Info("Handling local declaration statement node");

                    var localDeclNode = (LocalDeclarationStatementSyntax)node;
                    nodeList.Add(node.Parent); // TODO make sure this is required
                    break;

                // A variable assignment node should add all users of the variable
                case SyntaxKind.VariableDeclaration:
                    logger.Info("Handling variable declaration node");
                    nodeList.Add(node.Parent); // TODO make sure this is required
                    break;
                case SyntaxKind.VariableDeclarator:
                    logger.Info("Handling variable declarator node");

                    logger.Assert(node.IsKind(SyntaxKind.VariableDeclarator), "Unhandled identifier kind in UserNodes(): " + node.Kind());

                    var declaratorSymbol = deserCall.model.GetDeclaredSymbol(node);

                    nodeList.AddRange(FindSymbolUsers(declaratorSymbol));

                    nodeList.Add(node.Parent); // TODO make sure this is required
                    break;

                case SyntaxKind.Parameter:
                    var parameterSymbol = deserCall.model.GetDeclaredSymbol(node);

                    nodeList.AddRange(FindSymbolUsers(parameterSymbol));
                    break;

                case SyntaxKind.SimpleAssignmentExpression:

                    logger.Info("Handling assignment node");

                    var assignmentNode = (AssignmentExpressionSyntax)node;

                    // Variable being assigned
                    var lhs = assignmentNode.Left;

                    // TODO For now, can only handle simple assignments
                    logger.Assert(lhs.IsKind(SyntaxKind.IdentifierName), "Unhandled LHS kind in UserNodes(): " + lhs.Kind());

                    var variableSymbol = deserCall.model.GetSymbolInfo(lhs).Symbol;

                    var newNodeList = FindSymbolUsers(variableSymbol);
                    // Add all to nodeList except for itself
                    foreach (var newNode in newNodeList)
                    {
                        if (newNode != lhs)
                        {
                            nodeList.Add(newNode);
                        }
                    }

                    // // Find all references to the variable
                    // var variableSymbol = deserCall.model.GetSymbolInfo(lhs).Symbol;

                    // var references = SymbolFinder.FindReferencesAsync(variableSymbol, deserCall.project.Solution).Result;
                    // var referenceLocations = references.SelectMany(r => r.Locations).ToList();

                    // logger.Info("Found " + referenceLocations.Count() + " references to variable " + variableSymbol.Name);
                    // foreach (var location in referenceLocations)
                    // {
                    //     logger.Info("Reference at " + location.Location.SourceTree.FilePath + ":" + (location.Location.GetLineSpan().StartLinePosition.Line + 1));
                    //     // Convert location to node and add to list
                    //     // TODO could do more complex dataflow analysis (i.e. exclude previous nodes in the same block)
                    //     var referenceNode = location.Location.SourceTree.GetRoot().FindNode(location.Location.SourceSpan);
                    //     logger.Info("User Node: " + MiscUtils.FirstNLines(referenceNode.ToString(), 3));

                    //     logger.Assert(referenceNode.IsKind(SyntaxKind.IdentifierName), "Unhandled reference node kind in UserNodes(): " + referenceNode.Kind());

                    //     // Don't add the node itself to the list 
                    //     if (referenceNode == lhs)
                    //     {
                    //         logger.Info("Skipping self-reference");
                    //         continue;
                    //     }

                    //     nodeList.Add(referenceNode);
                    // }



                    nodeList.Add(node.Parent); // TODO make sure this is required
                    break;

                /* For 'anonymous' nodes that just return a value, the only user is parent (TODO some of these
                might need more logic) */
                case SyntaxKind.InvocationExpression:
                case SyntaxKind.IdentifierName:
                // TODO could do member analysis here? Probably not, probably wait until allowList finished
                // then convert each possible object to an object graph
                case SyntaxKind.SimpleMemberAccessExpression:
                case SyntaxKind.ExpressionStatement:
                case SyntaxKind.Block:
                case SyntaxKind.EqualsValueClause:
                case SyntaxKind.CastExpression:
                case SyntaxKind.ParenthesizedExpression:


                    nodeList.Add(node.Parent);
                    // Handle default case
                    break;

                /* For non-expression cases that dead-end, don't add any users */
                case SyntaxKind.MethodDeclaration:
                    break;

                default:
                    logger.Error("Unhandled node kind in UserNodes(): " + nodeKind);
                    throw new System.Exception("Unhandled node kind in UserNodes(): " + nodeKind);
            }



            return nodeList;
        }

        private List<TypeEvidence> AnalyzeNode(SyntaxNode node)
        {
            logger.Info("Analyzing node: " + MiscUtils.FirstNLines(node.ToString(), 3));

            // Create a list of evidence for this node
            var allowList = new List<TypeEvidence>();

            // Extract evidence from the toplevel node 
            allowList.AddRange(ruleSet.ExtractEvidence(node));

            // Extract evidence from all user nodes
            var userNodes = UserNodes(node);
            if (userNodes == null)
            {
                logger.Info("At least one user node could not be analyzed, cannot resetrict type further");
                // Exits early, type evidence SHOULD have added all possible types
                return allowList;
            }

            foreach (var userNode in userNodes)
            {
                var userAllowList = AnalyzeNode(userNode);
                allowList.AddRange(userAllowList);
            }

            return allowList;
        }


        public List<TypeEvidence> GatherTypeEvidence()
        {
            logger.Info("Gathering type evidence");
            // Follow the deserialization call node 
            var node = deserCall.node;

            gatheredEvidence = AnalyzeNode(node);

            logger.Info("Finished gathering evidence");
            foreach (var evidence in gatheredEvidence)
            {
                logger.Plain(LogLevel.Info, evidence.ToString() + "\n");
            }
            return gatheredEvidence;
        }
    }
}