using System.Text;
using Microsoft.CodeAnalysis;

namespace Quack.Analysis
{

    // TODO subclasses for exact versus duck?
    public class TypeEvidence
    {
        // Includes: node, reason string, and base type 
        protected SyntaxNode node;
        protected string reason;
        protected List<ITypeSymbol> allowedTypeSet;
        protected bool isExact;

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
        // make this overridable

        public virtual List<ITypeSymbol> getAllowedTypes()
        {
            return allowedTypeSet;
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

    // Evidence where the type is untrackable, so the allowed classes set includes
    // all subclasses of the parent classes
    public class UntrackableTypeEvidence : TypeEvidence
    {
        public UntrackableTypeEvidence(SyntaxNode node, List<ITypeSymbol> parents, string reason) : base(node, parents, true, reason)
        {
        }
        // Make an untrackable type evidence with a set of parents extracted from a previous type evidence
        public UntrackableTypeEvidence(SyntaxNode node, TypeEvidence evidence, string reason) : base(node, evidence.getAllowedTypes(), true, reason)
        {
        }

        // TODO this feels poorly written
        public UntrackableTypeEvidence(SyntaxNode node, List<TypeEvidence> evidences, string reason) : base(node, null, true, reason)
        {
            // Merge allowed types from each evidence
            List<ITypeSymbol> allowedTypes = new List<ITypeSymbol>();
            foreach (var evidence in evidences)
            {
                allowedTypes.AddRange(evidence.getAllowedTypes());
            }
            this.allowedTypeSet = allowedTypes;
        }

        public override List<ITypeSymbol> getAllowedTypes()
        {
            // Get all subclasses of the parent classes
            // List<ITypeSymbol> allowedTypes = new List<ITypeSymbol>();
            // foreach (var parent in allowedTypeSet)
            // {
            //     allowedTypes.AddRange(parent.GetSubclasses());
            // }
            throw new NotImplementedException("Getting all subtypes of parent not implemented");
            //return allowedTypes;
        }

        public override string ToString()
        {
            return "[Untrackable Type]" + base.ToString();
        }
    }
}