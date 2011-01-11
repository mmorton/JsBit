using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NDesk.Options;

namespace JsBit.ConsoleApplication
{
    class Program
    {
        public class Options
        {
            public string ProjectFilePath { get; set; }
            public string DestinationPath { get; set; }
            public bool DisplayHelp { get; set; }
        }

        static Options ParseOptions(string[] args)
        {
            var options = new Options();
            var set = new OptionSet
            {
                { "p|project=", v => options.ProjectFilePath = v },
                { "d|destination=", v => options.DestinationPath = v },
                { "h|?|help", v => options.DisplayHelp = v != null }
            };

            set.Parse(args);
     
            return options;
        }

        static void ShowHelp()
        {
            Console.WriteLine("jsbit v1.0");
            Console.WriteLine();
            Console.WriteLine("jsbit [OPTIONS]"); 
            Console.WriteLine("  -project|p > The path to the project file.");
            Console.WriteLine("  -destination|d > The destination path.");
            Console.WriteLine("  -help|h > Show this message.");
        }

        static void Main(string[] args)
        {
            var options = ParseOptions(args);
            if (options.DisplayHelp)
            {
                ShowHelp();
                return;
            }

            if (String.IsNullOrEmpty(options.ProjectFilePath) ||
                String.IsNullOrEmpty(options.DestinationPath))
            {
                ShowHelp();
                return;
            }

            try
            {
                var builder = new JsbBuilder();
                var project = JsbProject.Open(options.ProjectFilePath);
                var buildOptions = new JsbBuildOptions
                {
                    DestinationPath = options.DestinationPath
                };

                builder.Build(project, buildOptions);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Environment.Exit(1);
            }            
        }
    }
}
