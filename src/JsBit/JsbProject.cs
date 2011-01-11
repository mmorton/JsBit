using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json.Serialization;

namespace JsBit
{
    public class JsbInclude
    {
        [JsonProperty("text")]
        public string Text { get; set; }
        [JsonProperty("path")]
        public string Path { get; set; }
    }

    public class JsbPackage
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("file")]
        public string File { get; set; }
        [JsonProperty("isDebug")]
        public bool IsDebug { get; set; }
        [JsonProperty("includeDeps")]
        public bool IncludeDependencies { get; set; }

        [JsonProperty("fileIncludes")]
        public ICollection<JsbInclude> Includes { get; set; }

        [JsonProperty("pkgDeps")]
        public ICollection<string> Dependencies { get; set; }
    }

    public class JsbResource
    {
        [JsonProperty("src")]
        public string Source { get; set; }
        [JsonProperty("dest")]
        public string Destination { get; set; }
        [JsonProperty("filters")]
        public string Filter { get; set; }
    }

    public class JsbProject
    {
        [JsonIgnore]
        public string Path { get; protected set; }
        [JsonProperty("projectName")]
        public string Name { get; set; }
        [JsonProperty("licenseText")]
        public string License { get; set; }
        [JsonProperty("deployDir")]
        public string DeployDir { get; set; }
        [JsonProperty("isDebug")]
        public bool IsDebug { get; set; }

        [JsonProperty("pkgs")]
        public ICollection<JsbPackage> Packages { get; set; }

        [JsonProperty("resources")]
        public ICollection<JsbResource> Resources { get; set; }

        public JsbProject(string projectFile)
        {
            Path = projectFile;
        }

        public static JsbProject Open(string projectFile)
        {
            var project = new JsbProject(projectFile);

            var serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });

            using (var textReader = new StreamReader(projectFile))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                serializer.Populate(jsonReader, project);

                return project;
            }
        }
    }
}
