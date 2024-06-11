using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Quack.Analysis
{
    class SolutionAnalyzer
    {
        private string solutionPath;
        // TODO replace with more generic outputer object?
        private string outputPath;
        private MSBuildWorkspace workspace;

        private Logger logger;

        private Solution solution;
        private List<ProjectAnalyzer> projectAnalyzers;

        /* Retrieve all projects inside solution and initialize each */
        private List<ProjectAnalyzer> InitProjectAnalyzers()
        {
            logger.Info("Initializing ProjectAnalyzers");
            var projects = solution.Projects;


            var projectAnalyzers = new List<ProjectAnalyzer>();
            foreach (var project in projects)
            {
                var projectAnalyzer = new ProjectAnalyzer(project, outputPath, logger);
                logger.Info("Added project " + project.Name);
                logger.Debug("Compilation: ");
                logger.Plain(LogLevel.Debug, projectAnalyzer.DisplayCompilation());
                projectAnalyzers.Add(projectAnalyzer);
            }

            return projectAnalyzers;
        }

        public SolutionAnalyzer(string solutionPath, string outputPath, MSBuildWorkspace workspace, Logger logger)
        {
            logger.Info("Initializing SolutionAnalyzer");

            this.solutionPath = solutionPath;
            this.outputPath = outputPath;
            this.workspace = workspace;
            this.logger = logger;

            this.solution = workspace.OpenSolutionAsync(solutionPath)
                                    .Result;

            // Collect all projects from solution 
            this.projectAnalyzers = InitProjectAnalyzers();

        }

        /* Analyze a solution by analyzing each contained project */
        public void analyze()
        {
            logger.Info("Analyzing solution");
            // Run analysis on each project 
            foreach (var projectAnalyzer in projectAnalyzers)
            {
                projectAnalyzer.analyze();
            }
            logger.Info("Solution analysis finished");
        }
    }
}