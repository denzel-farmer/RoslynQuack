using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;


namespace Quack.Analysis
{

    public class TypeEvidenceRuleSet
    {
        private readonly TypeEvidenceRule[] rules;

        private SemanticModel model;

        public TypeEvidenceRuleSet(SemanticModel model, Logger logger)
        {
            // TODO find a way to not pass logger everywhere?
            rules = [new CastRule(logger), new JSONDeserializationRule(logger)];
            this.model = model;
        }

        public List<TypeEvidence> ExtractEvidence(SyntaxNode node)
        {
            // TODO is it possible to match multiple rules on a single symbol?
            List<TypeEvidence> newEvidence = new();
            foreach (var rule in rules)
            {
                var evidence = rule.CheckNode(node, model);
                if (evidence != null)
                {
                    // Add evidence to list
                    newEvidence.Add(evidence);
                }
            }

            return newEvidence;
        }
    }

    // TODO confirm: much abbreviated list of type rules, because 
    // we only make rules for explicit type conversions 
    abstract class TypeEvidenceRule
    {
        protected Logger logger;

        public TypeEvidenceRule(Logger logger)
        {
            this.logger = logger;
        }

        /* Rule-checking helpers (TODO unify with other helpers) */

        /* Indicates if rule generates exact or duck typing evidence */
        public abstract bool IsExact();

        /* Tries to produce type evidence about the given node based on the rule */
        public abstract TypeEvidence? CheckNode(SyntaxNode node, SemanticModel model);


      //  public abstract TypeEvidence? CheckSymbol(ISymbol symbol);

    }

    class CastRule : TypeEvidenceRule 
    {
        private const string reason = "Cast";
        public CastRule(Logger logger) : base(logger)
        {

        }

        public override bool IsExact()
        {
            return true;
        }

        public override TypeEvidence? CheckNode(SyntaxNode node, SemanticModel model)
        {
            var nodeKind = node.Kind();
            if (nodeKind == SyntaxKind.CastExpression)
            {
                logger.Info("Node is a cast expression");
                var castNode = (CastExpressionSyntax)node;

                logger.Info("Cast type: " + castNode.Type.ToString());

                var type = model.GetTypeInfo(castNode.Type).Type;

                logger.Info("Semantic-based cast type: " + type.ToString());

                // var expr = castNode.Expression;
                // var exprType = model.GetTypeInfo(expr).Type;
                return new TypeEvidence(node, type, "Explicit cast at " + node.GetLocation().GetLineSpan().ToString());
            }
            return null;
        }

    }
// TODO add rules to get initial type (like initial deserialization call, and member access)
    // DeserializeObject<T>(string json) -> T
    // T out = DeserializeObject(string json, Type type) -> T
    // a.b -> type of member b in typeof(a)

    class JSONDeserializationRule : TypeEvidenceRule
    {
        private const string reason = "JSON Deserialization";
        public JSONDeserializationRule(Logger logger) : base(logger)
        {

        }

        public override bool IsExact()
        {
            return true;
        }

        public override TypeEvidence? CheckNode(SyntaxNode node, SemanticModel model)
        {
            var nodeKind = node.Kind();
            if (nodeKind == SyntaxKind.InvocationExpression)
            {
                logger.Info("Node is an invocation expression");
                var invocationNode = (InvocationExpressionSyntax)node;

                var methodSymbol = (IMethodSymbol)model.GetSymbolInfo(invocationNode).Symbol;

                if (methodSymbol == null)
                {
                    logger.Warn("Method symbol is null");
                    return null;
                }

                logger.Info("Method symbol: " + methodSymbol.ToString());

                if (methodSymbol.Name == "DeserializeObject")
                {
                    logger.Info("Method is DeserializeObject, extracting initial type");

                    // var typeArg = invocationNode.ArgumentList.Arguments[0].Expression;
                    // var type = model.GetTypeInfo(typeArg).Type;
                    
                    var type = methodSymbol.TypeArguments[0];

                    logger.Assert(methodSymbol.TypeArguments.Length == 1, "DeserializeObject has more than one type argument");
                    logger.Assert(methodSymbol.TypeArguments[0].Kind == SymbolKind.NamedType, "Type argument is not a named type");

                    logger.Info("Type argument: " + type.ToString());

                    return new TypeEvidence(node, type, reason);
                }
            }
            return null;
        }
    }
}

    /* TODO expand inheritence to remove duplicate code among rules (e.g. unify rules that 
     * check the kind of the containing symbol) */
     // TODO delete this rule, and make sure these rules really can't tell us anything more than the compiled type
//     class FuncArgRule : TypeEvidenceRule
//     {
//         private const string reason = "Function argument";
//         public FuncArgRule(Logger logger) : base(logger)
//         {

//         }

//         public override bool IsExact()
//         {
//             return true;
//         }
//         /* If symbol is the argument to a function, rule matches */
//         public override TypeEvidence? CheckNode(SyntaxNode node, SemanticModel model)
//         {
//             // TODO put in helper function or base implementation
//             logger.Info("Checking node: \n" + MiscUtils.FirstNLines(node.ToString(), 3) + "\n");

//             var nodeKind = node.Kind();

//             logger.Info("Node kind: " + nodeKind.ToString());

//             if (nodeKind == SyntaxKind.Argument) {
//                 logger.Info("Node is an argument");

//                 // var symbol = model.GetSymbolInfo(node).Symbol;

//                 // logger.Info("Symbol: " + symbol.ToString());

//                 // var argList = node.Parent;

//                 // logger.Assert(argList.Kind() == SyntaxKind.ArgumentList, "Parent of argument is not an argument list");

//                 // // var argIndex = argList.ChildNodes().IndexOf(node);

//                 // var methodCall = argList.Parent;

//                 // logger.Assert(methodCall.Kind() == SyntaxKind.InvocationExpression, "Parent of argument list is not an invocation expression");

//                 // var methodCallSymbol = (IMethodSymbol)model.GetSymbolInfo(methodCall).Symbol;

//                 // var parameters = methodCallSymbol.Parameters;



//             }

//             // var parentNode = node.Parent;
            
//             // logger.Info("Parent node: \n" + parentNode.ToString() + "\n");

//             // var parentNodeKind = parentNode.Kind();

//             // logger.Info("Parent node kind: " + parentNodeKind.ToString());

//             // if (parentNodeKind )


//             // var symbol = model.GetSymbolInfo(parentNode).Symbol;







//             // logger.Info("Parent symbol: " + symbol.ToString());
//             // // Throw error if symbol is null
//             // if (symbol == null)
//             // {
//             //     logger.Error("Node " + node.ToString() + " has null symbol");
//             //     throw new System.Exception("Symbol is null");
//             // }

//             return null;
//         }


//         public override TypeEvidence? CheckSymbol(ISymbol symbol)
//         {
//             var containingSymbol = symbol.ContainingSymbol;
//             if (containingSymbol.Kind == SymbolKind.Method)
//             {
//                 logger.Info("Symbol is a function argument");
//                 // Get parameters from containgSymbol
//                 var method = (IMethodSymbol)containingSymbol;
//                 var parameters = method.Parameters;

//                 // Check if symbol is in parameters
//                 // TODO could symbol be two parameters? 
//                 // TODO implicit parameters?
//                 foreach (var param in parameters)
//                 {
//                     // TODO get better at comparing symbols
//                     if (SymbolEqualityComparer.Default.Equals(param, symbol))
//                     {
//                         logger.Info("Found specific param, creating evidence");
//                         logger.Info("Param type: " + param.Type.ToString());
//                         return new TypeEvidence(symbol, param.Type, reason);
//                     }
//                 }
//                 logger.Error("Symbol is not a function argument but containg symbol is a method");
//                 throw new System.Exception("Symbol is not a function argument");
//             }


//             return null;
//         }
//     }

//     // TODO make class for catching non-analyzable nodes (e.g. calls to external functions)
// }