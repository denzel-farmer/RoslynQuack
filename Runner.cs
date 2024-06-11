
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Quack.Analysis;

namespace Quack
{
    class Runner
    {

        static string[] get_args()
        {
            //TODO implement for real
            return ["samples/basic-weather/CSharpTest.sln",
                    "outputDir"];
        }

        static void Main(string[] args)
        {
    
            args = get_args();

            if (args.Length != 2)
            {
                // TODO replace with logger that transparently logs to file and prints to stdout
                // with log levels
                Console.WriteLine("Usage: Quack <solutionPath> <outputDir>");
                Console.WriteLine("Args: " + args[0] + " " + args[1]);
                return;
            }

            string solutionPath = args[0];
            // If relative path
            if (!Path.IsPathRooted(solutionPath))
            {
                solutionPath = Path.GetFullPath(solutionPath);
            }

            // If absolute path
            if (!File.Exists(solutionPath))
            {
                Console.WriteLine("Solution path " + solutionPath + " does not exist");
                return;
            }

            string outputPath = args[1];
            if (!Directory.Exists(outputPath))
            {
                Console.WriteLine("Output directory does not exist. Creating it...");
                Directory.CreateDirectory(outputPath);
            }

            Logger logger = new(outputPath);

            MSBuildLocator.RegisterDefaults();
            using (var workspace = MSBuildWorkspace.Create())
            {
                logger.Info("Initializing Analysis");
                SolutionAnalyzer analyzer = new(solutionPath, outputPath, workspace, logger);
                logger.Info("Running Analysis");
                analyzer.analyze();
                logger.Info("Analyzer Complete");
            }
        }
    }
}