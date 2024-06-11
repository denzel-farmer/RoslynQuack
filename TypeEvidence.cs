using System.Text;
using Microsoft.CodeAnalysis;

namespace Quack.Analysis
{

    // TODO subclasses for exact versus duck?
    public class TypeEvidence
    {
        // Includes: node, reason string, and base type 
        private SyntaxNode node;
        private string reason;
        private List<ITypeSymbol> allowedTypeSet;
        private bool isExact;

        public TypeEvidence(SyntaxNode node, List<ITypeSymbol> newAllowedTypes, bool isExact, string reason)
        {
            this.node = node;
            this.reason = reason;
            this.allowedTypeSet = newAllowedTypes;
            this.isExact = isExact;
        }

        // Constructor for exact evidence, which only provides a single type
        public TypeEvidence(SyntaxNode node, ITypeSymbol newAllowedType, string reason)
        {
            this.node = node;
            this.reason = reason;
            this.allowedTypeSet = [newAllowedType];
            this.isExact = true;
        }

        // ToString override
        public override string ToString()
        {
            // Use stringbuilder to make a string with each allowed type
            StringBuilder sb = new StringBuilder();

            sb.Append("[Implied Types ");
            if (isExact)
            {
                sb.Append("(Exact): ");
            }
            else
            {
                sb.Append("(Duck): ");
            }
            foreach (var type in allowedTypeSet)
            {
                sb.Append(type.ToString());
                sb.Append(", ");
            }
            sb.Append("]");
            sb.Append(" Reason: ");
            sb.Append(reason);
            return sb.ToString();
        }




    }
}