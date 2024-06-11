using Microsoft.CodeAnalysis;

namespace Quack.Analysis
{
    /* Class for analyzing a single deserialization call */
    class DeserializationAnalyzer
    {
        private Logger logger;
        private DeserializationCall deserCall;

        private readonly List<INamedTypeSymbol> allTypes;

        public DeserializationAnalyzer(DeserializationCall deserCall, ref readonly List<INamedTypeSymbol> allTypes, Logger logger)
        {
            this.deserCall = deserCall;
            this.allTypes = allTypes;
            this.logger = logger;

        }

        public bool InheritsFromOrIs(ITypeSymbol rootType, ITypeSymbol type)
        {
            if (type == null || rootType == null)
            {
                return false;
            }
            if (SymbolEqualityComparer.Default.Equals(type.BaseType, rootType)
            || SymbolEqualityComparer.Default.Equals(type, rootType))
            {
                return true;
            }

            return InheritsFromOrIs(rootType, type.BaseType);

        }
        private List<ITypeSymbol> filterByRootType(List<ITypeSymbol> availableTypes, ITypeSymbol rootType)
        {
            logger.Info("Filtering available types to those under root type: " + rootType.ToString());

            List<ITypeSymbol> filteredTypes = new();
            foreach (var type in availableTypes)
            {
                if (InheritsFromOrIs(rootType, type))
                {
                    filteredTypes.Add(type);
                    logger.Debug("Added type: " + type.ToString());
                }
            }
            return filteredTypes;
        }

        /* Retrieve all types available at the call site */
        private List<ITypeSymbol> gatherAvailableTypes()
        {
            /* Inefficient implementation: check if each of 'all' types is accessible at the call symbol */
            logger.Info("Gathering available types");
            List<ITypeSymbol> availableTypes = new();


            foreach (var type in allTypes)
            {
                if (deserCall.isAvailableAtCallSite(type))
                {

                    availableTypes.Add(type);
                }
            }
            // TODO this whole thing can be made way more efficient
            ITypeSymbol? expectedRootType = deserCall.getExpectedType();
            if (expectedRootType != null)
            {
                availableTypes = filterByRootType(availableTypes, expectedRootType);
            }

            logger.Info("Reduced available types to count: " + availableTypes.Count);
            if (availableTypes.Count == 0)
            {
                logger.Error("No available types found");
            }
            else if (availableTypes.Count < 100)
            {
                logger.Info("Available types: ");
                foreach (var type in availableTypes)
                {
                    logger.Plain(LogLevel.Info, type.ToString() + ", ");
                }
                logger.Plain(LogLevel.Info, "\n");
            }
            else
            {
                logger.Info("Available types: " + availableTypes.Count);

            }
            return availableTypes;
        }

        public void analyze()
        {
            logger.Info("Analyzing deserialization call");
            logger.Info("Deserialization call: " + deserCall.ToString());

            logger.Info("Deserialization Call Symbol Information: ");
            logger.Plain(LogLevel.Info, deserCall.getCallDebugString());

            /* Gather all available types */
            var availableTypes = gatherAvailableTypes();

            /* Gather type evidence for symbol */
            var gatherer = new TypeEvidenceGatherer(deserCall, logger);
            var typeEvidence = gatherer.GatherTypeEvidence();

            /* Filter available types based on type evidence */




        }
    }
}