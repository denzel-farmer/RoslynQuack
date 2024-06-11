
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics.SymbolStore;
using Microsoft.Build.Tasks.Deployment.Bootstrapper;
using Microsoft.CodeAnalysis.FindSymbols;

namespace Quack.Analysis
{

    /* Class to create a deserialization call object based on symbol */
    class DeserializationCallFactory
    {
        // TODO probably don't need to pass project/compilation around, semantic model should be enough
        public static DeserializationCall? TryCreateDeserializationCall(ISymbol symbol, SyntaxNode node, SemanticModel model, Compilation compilation, Project project)
        {

            if (symbol.Kind != SymbolKind.Method)
            {
                return null;
            }

            var methodSymbol = (IMethodSymbol)symbol;

            if (JSONConvertDeserObjectWithGeneric.IsDeserializationMethod(methodSymbol))
            {
                return new JSONConvertDeserObjectWithGeneric(methodSymbol, node, model, compilation, project);
            }
            return null;
        }
    }


    /* Class describing a generic deserialization call */

    class DeserializationCall
    {
        public IMethodSymbol symbol { get; private set; }
        public SyntaxNode node { get; private set; }
        // TODO might not be needed
        private Compilation compilation;
        public Project project { get; private set; }
        protected ITypeSymbol? expectedType;
        public SemanticModel model { get; private set; }

        public DeserializationCall(IMethodSymbol symbol, SyntaxNode node, SemanticModel model, Compilation compilation, Project project)
        {
            this.symbol = symbol;
            this.node = node;
            this.compilation = compilation;
            this.project = project;
            this.model = model;
        }

        public bool HasExpectedType()
        {
            return expectedType != null;
        }

        public ITypeSymbol? getExpectedType()
        {
            return expectedType;
        }

        // // TODO move these helpers into utility classes
        // public 

        public bool isAvailableAtCallSite(INamedTypeSymbol type)
        {
            return compilation.IsSymbolAccessibleWithin(type, symbol.ContainingType);
        }

        public string getCallDebugString()
        {
            StringBuilder debugStrBuilder = new StringBuilder();

            //  symbol name and location
            debugStrBuilder.AppendLine("Symbol: " + this.ToString());

            // containing symbol 
            debugStrBuilder.AppendLine("Containing Symbol: " + symbol.ContainingSymbol.ToString());

            // symbol kind (should be Method)
            debugStrBuilder.AppendLine("Kind: " + symbol.Kind.ToString());

            // method kind 
            debugStrBuilder.AppendLine("Method Kind: " + symbol.MethodKind.ToString());

            // Expected Type
            if (expectedType != null)
            {
                debugStrBuilder.AppendLine("Expected Type: " + expectedType.ToString());
            }

            return debugStrBuilder.ToString();

        }

        //To string method 
        public override string ToString()
        {
            // Print symbol name, file, and line
            return symbol.ToString() + " at " + node.SyntaxTree.FilePath + ":" + node.GetLocation().GetLineSpan().StartLinePosition.Line;

        }
    }

    class JSONConvertDeserObjectWithGeneric : DeserializationCall
    {
        public JSONConvertDeserObjectWithGeneric(IMethodSymbol symbol, SyntaxNode node, SemanticModel model, Compilation compilation, Project project) : base(symbol, node, model, compilation, project)
        {
            this.expectedType = extractExpectedType();
        }

        private ITypeSymbol extractExpectedType()
        {
            return symbol.TypeArguments[0];
        }

        public static bool IsDeserializationMethod(IMethodSymbol symbol)
        {
            // TODO come up with more tests for this 
            return symbol.Name == "DeserializeObject" && symbol.Arity == 1;
        }
    }

}