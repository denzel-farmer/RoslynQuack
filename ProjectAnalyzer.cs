
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

    class ProjectAnalyzer
    {
        private Project project;
        private string outputPath;
        private Logger logger;
        private Compilation compilation;
        // private SemanticModel semanticModel;
        private List<DeserializationAnalyzer> deserAnalyzers;
        /* All types in the project */
        private readonly List<INamedTypeSymbol> allTypes;


        /* Find all deserialization calls in the project */
        private List<DeserializationAnalyzer> InitDeserializationAnalyzers()
        {
            // Debug print all types
            // logger.Debug("All types: ");
            // for (int i = 0; i < allTypes.Count; i++)
            // {
            //     logger.Plain(LogLevel.Debug, allTypes[i].Name + " ");
            // }

            logger.Info("Finding deserialization calls");

            var deserCallAnalyzers = new List<DeserializationAnalyzer>();


            // /* Get the symbol object of the DeserializeObject method */
            // var typeSymbol = compilation.GetTypeByMetadataName("Newtonsoft.Json.JsonConvert");



            /* Iterate over each document and find all calls to the DeserializeObject method */
            // TODO may be more robust to use the semantic model?
            foreach (var document in project.Documents)
            {
                logger.Info("Analyzing document: " + document.Name);
                var tree = document.GetSyntaxTreeAsync().Result;
                var model = compilation.GetSemanticModel(tree);

                // var methodSyntax = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
                // var methodSymbol = model.GetDeclaredSymbol(methodSyntax);
                // if (methodSymbol != null)
                //     Console.WriteLine("Method: " + methodSymbol.ToString());

                // var invocationSyntax = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();
                // var invokedSymbol = model.GetSymbolInfo(invocationSyntax).Symbol; //Same as MyClass.Method1
                // if (invokedSymbol != null)
                //     Console.WriteLine("Invocation: " + invokedSymbol.ToString());

                var root = tree.GetRoot();

                var nodes = root.DescendantNodesAndSelf();
                foreach (var node in nodes)
                {
                    // Check all invocations 
                    if (node is InvocationExpressionSyntax)
                    {

                        logger.Debug("Found invocation node: " + node.ToString());

                        var symbolinfo = model.GetSymbolInfo(node);
                        var symbol = symbolinfo.Symbol;

                        if (symbol == null)
                        {
                            logger.Debug("Can't resolve symbol, candiate reason: " + symbolinfo.CandidateReason);

                            if (symbolinfo.CandidateSymbols != null)
                            {
                                foreach (var candidate in symbolinfo.CandidateSymbols)
                                {
                                    logger.Debug("Candidate symbol: " + candidate.ToDisplayString());
                                }
                            }

                            continue;
                        }

                        logger.Debug("Resolved invocation symbol: " + symbol.ToDisplayString());

                        var deserCallResult = DeserializationCallFactory.TryCreateDeserializationCall(symbol, node, model, compilation, project);

                        if (deserCallResult != null)
                        {
                            logger.Info("Found deserialization call symbol: " + symbol.ToDisplayString());

                            var deserAnalyzer = new DeserializationAnalyzer(deserCallResult, allTypes, logger);
                            deserCallAnalyzers.Add(deserAnalyzer);


                            // Print out the number of references to the deserialization method
                            // TODO replace scanning with a single call to FindReferencesAsync, and/or use it to double-check
                            var referencesToDeser = SymbolFinder.FindReferencesAsync(symbol, document.Project.Solution).Result;
                            logger.Info("Number of references to deserialization method: " + referencesToDeser.Count());

                            // Check for now that we only let DeserializeObject past
                            var invocation = (InvocationExpressionSyntax)node;
                            var method = invocation.Expression as MemberAccessExpressionSyntax;
                            if (method == null || method.Name.Identifier.Text != "DeserializeObject")
                            {
                                logger.Warn("Found non-DeserializeObject deserialization call: " + invocation);
                                continue;
                            }
                        }


                    }
                }

            }

            return deserCallAnalyzers;
        }
        /* Helpers for getting all types, from stackoverflow */
        private IEnumerable<INamedTypeSymbol> GetAllTypes() =>
        GetAllTypes(compilation.GlobalNamespace);

        private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol @namespace)
        {
            foreach (var type in @namespace.GetTypeMembers())
                foreach (var nestedType in GetNestedTypes(type))
                    yield return nestedType;

            foreach (var nestedNamespace in @namespace.GetNamespaceMembers())
                foreach (var type in GetAllTypes(nestedNamespace))
                    yield return type;
        }

        private static IEnumerable<INamedTypeSymbol> GetNestedTypes(INamedTypeSymbol type)
        {
            yield return type;
            foreach (var nestedType in type.GetTypeMembers()
                .SelectMany(nestedType => GetNestedTypes(nestedType)))
                yield return nestedType;
        }

        private Compilation GetCompilation()
        {
            var maybeCompilation = project.GetCompilationAsync().Result;

            if (maybeCompilation == null)
            {
                var message = "Failed to compile project";
                logger.Error(message);
                throw new Exception(message);
            }


            var diagnostics = maybeCompilation.GetDiagnostics();//.Where(n => n.Severity == DiagnosticSeverity.Error);
            if (diagnostics.Any())
            {
                var message = "Compilation diagnostics:";
                logger.Warn(message);
                foreach (var msg in diagnostics)
                {
                    logger.Plain(LogLevel.Warn, msg.ToString() + "\n");
                    // throw new Exception(message);
                }
            }

            /* Add referenced assemblies to compilation (TODO this method is not particularly robust) */

            /* Get paths to all possible runtime assemblies (TODO switch to reference assemblies?) */
            var trustedAssembliesPaths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);

            /* Get the names of assemblies actually referenced by the compilation */
            var referencedAssemblies = new List<string>();
            foreach (var reference in maybeCompilation.ReferencedAssemblyNames)
            {
                referencedAssemblies.Add(reference.Name);
            }

            logger.Debug("Referenced Assemblies: ");
            for (int i = 0; i < referencedAssemblies.Count; i++)
            {
                logger.Plain(LogLevel.Debug, referencedAssemblies[i] + " ");
            }
            logger.Plain(LogLevel.Debug, "\n");

            /* Filter runtime assembly paths to only include those referenced by the compilation */
            var references = trustedAssembliesPaths
                .Where(p => referencedAssemblies.Contains(Path.GetFileNameWithoutExtension(p)))
                .Select(p => MetadataReference.CreateFromFile(p))
                .ToList();

            // /* Add the filtered references to the compilation */
            // maybeCompilation = maybeCompilation.AddReferences(references);


            /* Test retrieving third party type */
            var typeSymbol = maybeCompilation.GetTypeByMetadataName("Newtonsoft.Json.JsonConvert");
            logger.Info("Found JSON.NET symbol: " + typeSymbol.ToDisplayString());
            return maybeCompilation;
        }
        public ProjectAnalyzer(Project project, string outputPath, Logger logger)
        {
            logger.Info("Initializing ProjectAnalyzer");
            this.project = project;
            this.outputPath = outputPath;
            this.logger = logger;

            this.compilation = GetCompilation();
            // this.semanticModel = compilation.GetSemanticModel();

            logger.Info("Retrieving all types in project");
            // Iterate over type enumerator to generate list 
            allTypes = new List<INamedTypeSymbol>(GetAllTypes());

            logger.Info("Retrieved type count: " + allTypes.Count);


            this.deserAnalyzers = InitDeserializationAnalyzers();

        }
        public string DisplayCompilation()
        {
            var results = new StringBuilder();
            // Traverse the symbol tree to find all namespaces, types, methods and fields.
            foreach (var ns in compilation.Assembly.GlobalNamespace.GetNamespaceMembers())
            {
                results.AppendLine();
                results.Append(ns.Kind);
                results.Append(": ");
                results.Append(ns.Name);
                foreach (var type in ns.GetTypeMembers())
                {
                    results.AppendLine();
                    results.Append("    ");
                    results.Append(type.TypeKind);
                    results.Append(": ");
                    results.Append(type.Name);
                    foreach (var member in type.GetMembers())
                    {
                        results.AppendLine();
                        results.Append("       ");
                        if (member.Kind == SymbolKind.Field || member.Kind == SymbolKind.Method)
                        {
                            results.Append(member.Kind);
                            results.Append(": ");
                            results.Append(member.Name);
                        }
                    }
                }
            }
            results.AppendLine();
            results.Append("Referenced Assemblies:\n");
            foreach (var reference in compilation.ReferencedAssemblyNames)
            {
                results.Append(reference.Name + " ");
            }
            return results.ToString();
        }
        /* Analyze a project by analyzing each Deserialization Call site (TODO currently assumes exactly one assembly per project) */
        internal void analyze()
        {

            /* Run analysis on each deserialization call */
            foreach (var deserAnalyzer in deserAnalyzers)
            {
                deserAnalyzer.analyze();
            }

        }
    }
}