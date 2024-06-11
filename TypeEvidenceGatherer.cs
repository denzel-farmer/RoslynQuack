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

                logger.Assert(referenceNode.IsKind(SyntaxKind.IdentifierName) || referenceNode.IsKind(SyntaxKind.Argument), "Unhandled reference node kind in UserNodes(): " + referenceNode.Kind());


                nodeList.Add(referenceNode);
            }

            return nodeList;


        }

        /* For a given node, retrive all nodes which 'use' that node */
        /* TODO build out with more cases--currently adding case-by-case as they appear
        in test program */
        private (List<SyntaxNode>?, List<SyntaxNode>?) UserNodes(SyntaxNode node)
        {
            // TODO implement this
            if (node == null || node.Parent == null)
            {
                logger.Info("UserNodes() reached null node or parent");
                return ([], []);
            }

            var nodeList = new List<SyntaxNode>();
            var memberUsageNodes = new List<SyntaxNode>();
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
                        return (null, null);
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

                // TODO could do member analysis here? Probably not, probably wait until allowList finished
                // then convert each possible object to an object graph
                // TODO handle pointer member access expression
                case SyntaxKind.SimpleMemberAccessExpression:
                    // In this case, just add the 'parent.member' node to the member usage list
                    logger.Info("Handling member access node");

                    var memberNode = (MemberAccessExpressionSyntax)node;

                    logger.Info("Member name:" + memberNode.Name);
                    logger.Info("Operator Token: " + memberNode.OperatorToken);

                    logger.Assert(memberNode.OperatorToken.IsKind(SyntaxKind.DotToken), "Unhandled member access operator in UserNodes(): " + memberNode.OperatorToken.Kind());

                    var memberAccessNode = (MemberAccessExpressionSyntax)node;

                    var expr = memberAccessNode.Expression;
                    var exprType = deserCall.model.GetTypeInfo(expr).Type;

                    // Check if member is a field or property
                    var memberSymbol = deserCall.model.GetSymbolInfo(memberAccessNode).Symbol;

                    // If kind is field, we want to follow it so add to member usage
                    if (memberSymbol.Kind == SymbolKind.Field)
                    {
                        memberUsageNodes.Add(node.Parent);
                    }
                    // Otherwise, we can skip it (TODO this probably needs more though)
                    // TODO properties definitely need to be handled more carefully
                    logger.Info("Member is not a field, no users to add");
                    break;

                /* For 'anonymous' nodes that just return a value, the only user is parent (TODO some of these
                might need more logic) */
                case SyntaxKind.InvocationExpression:
                case SyntaxKind.IdentifierName:
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



            return (nodeList, memberUsageNodes);
        }


        // TODO create a class to hold return object?
        // returns all possible types of the node except for the base evidence
        private List<TypeEvidence>? AnalyzeNode(SyntaxNode node, List<TypeEvidence> baseEvidence)
        {
            logger.Info("Analyzing node: " + MiscUtils.FirstNLines(node.ToString(), 3));

            // Create a list of evidence for this node
            var allowList = new List<TypeEvidence>();
            var nextBaseEvidence = baseEvidence;

            // Extract evidence from the toplevel node
            var initialEvidence = ruleSet.ExtractEvidence(node);

            // If node provides evidence, we can safely replace base evidence
            // This is where 'narrowing' happens, because we no longer consider
            // previous base evidence
            if (initialEvidence != null && initialEvidence.Count > 0)
            {
                nextBaseEvidence = initialEvidence;
            }

            // Find each user node of the present node
            var (rootUserNodes, memberNodes) = UserNodes(node);
            // If any user node is untrackable (i.e. external method), return 
            // an untrackable version of the base evidence
            if (rootUserNodes == null)
            {
                logger.Info("At least one user node could not be analyzed, cannot resetrict type further");

                // Exits early, returning base evidence plus a new untrackable evidence
                // that includes all subclasses of the old base evidence and the new evidence
                if (initialEvidence != null && initialEvidence.Count > 0)
                {
                    baseEvidence.AddRange(initialEvidence.Except(baseEvidence));
                }

                return [new UntrackableTypeEvidence(node, baseEvidence, "Untrackable user node")];
            }
            allowList.AddRange(baseEvidence.Except(allowList));

            foreach (var memberNode in memberNodes)
            {
                // TODO empty base evidence might not be right here? it should populate with initial evidence
                var memberAllowList = AnalyzeNode(memberNode, []);
                // For now, we don't track member-use evidence separately since
                // it all gets combined in the final binder anyways (but might be nice to separate for debugging)
                allowList.AddRange(memberAllowList.Except(allowList));
            }

            // Analyze each user node 
            foreach (var userNode in rootUserNodes)
            {
                // Each user node starts with the base evidence (i.e. its type is definitely base evidence or a subclass of it)
                var userEvidence = AnalyzeNode(userNode, nextBaseEvidence);
                allowList.AddRange(userEvidence.Except(allowList));
            }
            return allowList;
            // // New base evidence is the intersection of the base evidence and the initial evidence
            // // TODO prevent duplicate evidence
            // if (initialEvidence != null)
            // {
            //     nextBaseEvidence.AddRange(initialEvidence);
            // }

            // // Retrieve all users of the present node, and nodes of all member usages on the present node
            // (var rootUserNodes, var memberNodes) = UserNodes(node);
            // if (rootUserNodes == null || rootUserNodes.Count == 0)
            // {
            //     logger.Info("At least one user node could not be analyzed, cannot resetrict type further");
            //     // Exits early, returning initial evidence and base evidence
            //     return nextBaseEvidence;
            // }


            // // Analyze each member-use node
            // foreach (var memberNode in memberNodes)
            // {
            //     // TODO empty base evidence might not be right here? should populate with initial evidence
            //     var memberAllowList = AnalyzeNode(memberNode, []);
            //     // For now, we don't track member-use evidence separately since
            //     // it all gets combined in the final binder anyways (but might be nice to separate for debugging)
            //     allowList.AddRange(memberAllowList);
            // }

            // foreach (var userNode in rootUserNodes)
            // {
            //     var userAllowList = AnalyzeNode(userNode, nextBaseEvidence);

            //     // If any user node has no evidence, return the initial evidence
            //     // Anything already collected is irrelevant (although could be 
            //     // added anyways because it should be subclasses of the initial evidence)
            //     if (userAllowList == null || userAllowList.Count == 0)
            //     {
            //         logger.Info("At least one user node could not be analyzed, cannot resetrict type further");
            //         // Exits early, returning initial evidence
            //         return nextBaseEvidence;
            //     }

            //     allowList.AddRange(userAllowList);
            // }

            // return allowList;
        }


        public List<TypeEvidence> GatherTypeEvidence()
        {
            logger.Info("Gathering type evidence");
            // Follow the deserialization call node 
            var node = deserCall.node;

            gatheredEvidence = AnalyzeNode(node, []);

            logger.Info("Finished gathering evidence");
            foreach (var evidence in gatheredEvidence)
            {
                logger.Plain(LogLevel.Info, evidence.ToString() + "\n");
            }
            return gatheredEvidence;
        }
    }
}