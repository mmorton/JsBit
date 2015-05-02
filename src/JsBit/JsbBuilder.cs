using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Ajax.Utilities;

namespace JsBit
{
    public class JsbBuildOptions
    {
        public string SourcePath { get; set; }
        public string DeployPath { get; set; }
        public string DestinationPath { get; set; }
        public Encoding OutputEncoding { get; set; }
        public CodeSettings CodeSettings { get; set; }
        public CssSettings CssSettings { get; set; }

        public JsbBuildOptions()
        {
            OutputEncoding = Encoding.UTF8;
            CodeSettings = new CodeSettings();
            CssSettings = new CssSettings();
        }
    }

    public class JsbBuilder
    {
        public void Build(JsbProject project, JsbBuildOptions options)
        {
            var buildOptions = options ?? new JsbBuildOptions();

            ApplyProjectOptions(project, buildOptions);

            Directory.CreateDirectory(buildOptions.DeployPath);

            var packageBuildOrder = ResolveBuildOrder(project);

            foreach (var package in packageBuildOrder)
            {
                BuildPackage(project, package, buildOptions);
            }

            CopyProjectResources(project, buildOptions);
        }

        public void Build(JsbProject project)
        {
            Build(project, null);
        }

        private void CopyProjectResources(JsbProject project, JsbBuildOptions options)
        {
					if (project.Resources == null) return;
            Console.WriteLine("Copying project resources.");

            foreach (var resource in project.Resources)
            {
                Console.WriteLine("- Searching for files to copy using filter: '{0}'.", resource.Filter);

                var filter = new Regex(resource.Filter, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                var sourcePath = Path.GetFullPath(Path.Combine(options.SourcePath, resource.Source));
                var destinationPath = Path.GetFullPath(Path.Combine(options.DeployPath, resource.Destination));
                var files = Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories)
                    .AsEnumerable()
                    .Select(name => new FileInfo(name))
                    .Where(file => filter.IsMatch(file.Name) && ((file.Attributes & FileAttributes.Hidden) == 0))
                    .ToList();

                Console.WriteLine("- Copying {0} files to '{1}'.", files.Count, destinationPath);

                foreach (var file in files)
                {
                    var relativeFilePath = file.FullName.Substring(sourcePath.Length);
                    if (relativeFilePath.StartsWith(@"\") || relativeFilePath.StartsWith(@"/"))
                        relativeFilePath = relativeFilePath.Substring(1);

                    var destinationFilePath = Path.Combine(destinationPath, relativeFilePath);

                    if (!Directory.Exists(Path.GetDirectoryName(destinationFilePath)))
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath));

                    file.CopyTo(destinationFilePath, true);
                }
            }
        }

        private void BuildPackage(JsbProject project, JsbPackage package, JsbBuildOptions options)
        {
            Console.WriteLine("Building package '{0}' as '{1}'.", package.Name, package.File);

            var packageExtension = Path.GetExtension(package.File);
            var packageMinifiedPath = Path.Combine(options.DeployPath, package.File);
            var packageDebugPath = Path.Combine(
                Path.GetDirectoryName(packageMinifiedPath),
                Path.GetFileNameWithoutExtension(packageMinifiedPath) + "-debug" + packageExtension
            );
            var packageRootPath = Path.GetDirectoryName(packageMinifiedPath);

            if (!Directory.Exists(packageRootPath))
                Directory.CreateDirectory(packageRootPath);

            String debugContent = null;
            String minifiedContent = null;

            using (var combineWriter = new StringWriter())
            {
                if (!String.IsNullOrEmpty(project.License))
                    WriteLicense(project.License, combineWriter);

                if (package.IncludeDependencies)
                {
                    if (package.Dependencies != null)
                    {
                        Console.WriteLine("- There are {0} dependency include(s).", package.Dependencies.Count);

                        foreach (var dependency in package.Dependencies)
                        {
                            Console.WriteLine("-- {0}", dependency);

                            // use the non minified version of the dependency
                            var dependencyMinifiedPath = Path.Combine(options.DeployPath, dependency);
                            var dependencyDebugPath = Path.Combine(
                                Path.GetDirectoryName(dependencyMinifiedPath),
                                Path.GetFileNameWithoutExtension(dependencyMinifiedPath) + "-debug" + Path.GetExtension(dependency)
                            );

                            if (!File.Exists(dependencyDebugPath))
                                throw new ApplicationException(String.Format("Unable to read dependency file '{0}'.", dependencyDebugPath));

                            combineWriter.WriteLine(File.ReadAllText(dependencyDebugPath));
                        }
                    }
                }

                Console.WriteLine("- There are {0} file include(s).", package.Includes.Count);

                foreach (var include in package.Includes)
                {
                    Console.WriteLine("- - {0}", Path.Combine(include.Path, include.Text));

                    var includePath = Path.Combine(options.SourcePath, Path.Combine(include.Path, include.Text));

                    if (!File.Exists(includePath))
                        throw new ApplicationException(String.Format("Unable to read include file '{0}'.", includePath));

                    combineWriter.WriteLine(File.ReadAllText(includePath));
                }

                debugContent = combineWriter.ToString();
            }

            Console.WriteLine("- Writing standard output to '{0}'.", packageDebugPath);

            File.WriteAllText(packageDebugPath, debugContent, options.OutputEncoding);

            var minifier = new Minifier();

            if (packageExtension.ToLowerInvariant() == ".js")
            {
                Console.WriteLine("- Executing JavaScript minification.");
                minifiedContent = minifier.MinifyJavaScript(debugContent, options.CodeSettings);
            }
            else
            {
                Console.WriteLine("- Executing CSS minification.");
                minifiedContent = minifier.MinifyStyleSheet(debugContent, options.CssSettings);
            }

            Console.WriteLine("- Writing minified output to '{0}'.", packageMinifiedPath);

            File.WriteAllText(packageMinifiedPath, minifiedContent, options.OutputEncoding);
        }

        private void WriteLicense(string license, TextWriter writer)
        {
            var lines = license.Split('\n');

            writer.WriteLine("/*");
            foreach (var line in lines)
            {
                writer.Write(" *");
                writer.WriteLine(line.TrimEnd('\r'));
            }
            writer.WriteLine("*/");
        }

        private ICollection<JsbPackage> ResolveBuildOrder(JsbProject project)
        {
            var order = new List<JsbPackage>();
            var added = new Dictionary<string, JsbPackage>();
            var packages = new Dictionary<string, JsbPackage>();

            Action<JsbPackage, Dictionary<string, JsbPackage>> visit = null;
            visit = (package, visited) =>
            {
                if (added.ContainsKey(package.File)) return;

                if (package.Dependencies != null)
                {
                    visited = visited ?? new Dictionary<string, JsbPackage>();

                    if (visited.ContainsKey(package.File))
                        throw new ApplicationException(String.Format("Circular dependency detected for '{0}'.", package.File));

                    visited[package.File] = package;

                    foreach (var file in package.Dependencies)
                    {
                        if (packages.ContainsKey(file))
                        {
                            visit(packages[file], visited);
                        }
                        else
                            throw new ApplicationException(String.Format("Unable to resolve dependency '{0}' for '{1}'.", file, package.File));
                    }
                }

                order.Add(package);
                added.Add(package.File, package);
            };

            foreach (var package in project.Packages)
                packages.Add(package.File, package);

            foreach (var package in project.Packages)
                visit(package, null);

            return order;
        }

        private void ApplyProjectOptions(JsbProject project, JsbBuildOptions options)
        {
            if (String.IsNullOrEmpty(options.DestinationPath))
                options.DestinationPath = Environment.CurrentDirectory;
            if (String.IsNullOrEmpty(options.SourcePath))
                options.SourcePath = Path.GetDirectoryName(project.Path);        
            if (String.IsNullOrEmpty(options.DeployPath))
                options.DeployPath = Path.Combine(options.DestinationPath, project.DeployDir);
        }
    }
}
