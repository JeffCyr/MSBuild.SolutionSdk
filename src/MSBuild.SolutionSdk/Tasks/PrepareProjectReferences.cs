using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MSBuild.SolutionSdk.Tasks
{
    public class PrepareProjectReferences : Task
    {
        private class ProjectNode
        {
            private HashSet<string> _projectReferencePaths;
            private HashSet<ProjectNode> _dependentProjects;

            public ITaskItem ProjectItem { get; }

            public int OriginalOrder { get; }

            public string ProjectName { get; }

            public string ProjectPath { get; }

            public string ActiveConfiguration { get; }

            public string ActivePlatform { get; }

            public string AdditionalProperties { get; }

            public List<string> DependsOn { get; }

            public bool SkipProject { get; private set; }

            public IReadOnlyCollection<string> ProjectReferencePaths => _projectReferencePaths;

            public IReadOnlyCollection<ProjectNode> DependentProjects => _dependentProjects;

            public bool HasDependencies => DependsOn.Count > 0 || ProjectReferencePaths.Count > 0;

            public ProjectNode(ITaskItem projectItem, int originalOrder)
            {
                _projectReferencePaths = new HashSet<string>();
                _dependentProjects = new HashSet<ProjectNode>();
                DependsOn = new List<string>();

                ProjectItem = projectItem;
                OriginalOrder = originalOrder;

                ProjectName = projectItem.GetMetadata("Filename");
                ProjectPath = projectItem.GetMetadata("FullPath");
                ActiveConfiguration = projectItem.GetMetadata("Configuration");
                ActivePlatform = projectItem.GetMetadata("Platform");
                AdditionalProperties = ProjectItem.GetMetadata("AdditionalProperties");

                ParseDependsOn(projectItem.GetMetadata("DependsOn"));
            }

            private void ParseDependsOn(string dependsOn)
            {
                if (string.IsNullOrEmpty(dependsOn))
                    return;

                string[] dependencies = dependsOn.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string dependency in dependencies)
                    DependsOn.Add(dependency.Trim());
            }

            public void SetProjectMetadata(Project msbuildProject, bool isCustomBuild)
            {
                if (!msbuildProject.GetPropertyValue("UsingMicrosoftNETSdk").Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    SkipProject = string.IsNullOrEmpty(msbuildProject.GetPropertyValue("OutputPath"));
                    return;
                }

                var supportedConfigurations = msbuildProject
                    .GetPropertyValue("Configurations")
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                var supportedPlatforms = msbuildProject
                    .GetPropertyValue("Platforms")
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                if (isCustomBuild)
                {
                    var projectReferences = msbuildProject.GetItems("ProjectReference");

                    foreach (var projectRef in projectReferences)
                        _projectReferencePaths.Add(projectRef.GetMetadataValue("FullPath"));
                }

                SkipProject =
                    !supportedConfigurations.Contains(ActiveConfiguration, StringComparer.OrdinalIgnoreCase) ||
                    !supportedPlatforms.Contains(ActivePlatform, StringComparer.OrdinalIgnoreCase);
            }

            public ITaskItem CreateProjectReferenceItem(int buildOrder)
            {
                var item = new TaskItem(ProjectItem.ItemSpec);
                item.SetMetadata("Properties", $"Configuration={ActiveConfiguration};Platform={ActivePlatform}");
                item.SetMetadata("AdditionalProperties", AdditionalProperties);
                item.SetMetadata("BuildOrder", buildOrder.ToString());
                return item;
            }

            public void AddAdditionalProperties(Dictionary<string, string> globalProperties)
            {
                if (!string.IsNullOrEmpty(AdditionalProperties))
                {
                    string[] properties = AdditionalProperties.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string property in properties)
                    {
                        string[] values = property.Split(';');

                        if (values.Length != 2)
                        {
                            globalProperties.Add(property, "");
                        }
                        else
                        {
                            globalProperties.Add(values[0].Trim(), values[1].Trim());
                        }
                    }
                }
            }

            public void AddDependentProject(ProjectNode project)
            {
                _dependentProjects.Add(project);
            }
        }

        [Required]
        public ITaskItem[] Projects { get; set; }

        [Required]
        public string MSBuildProjectDirectory { get; set; }

        public bool CustomBuild { get; set; }

        [Output]
        public ITaskItem[] ProjectReferences { get; private set; }

        [Output]
        public bool ActualCustomBuild { get; private set; }

        public override bool Execute()
        {
            var nodes = new Dictionary<string, ProjectNode>(Projects.Length);

            for (var i = 0; i < Projects.Length; i++)
            {
                var project = Projects[i];
                var node = new ProjectNode(project, i);

                // Force CustomBuild if DependsOn is used
                CustomBuild |= node.DependsOn.Count > 0;
                nodes.Add(node.ProjectPath, node);
            }
            //System.Diagnostics.Debug.Fail("Break");
            EvaluateProjectsMetadata(nodes.Values);

            foreach (var project in nodes.Values.Where(item => item.SkipProject).ToArray())
            {
                Log.LogMessage("Skipped project '{0}' due to unsupported configuration or platform", project.ProjectName);
                nodes.Remove(project.ProjectPath);
            }

            if (CustomBuild)
            {
                ActualCustomBuild = true;

                if (!TryAddDependsOnToDependentProjects(nodes))
                    return false;

                if (!TryAddProjectReferencesToDependentProjects(nodes))
                    return false;

                ProjectReferences = GetOrderedProjectReferenceWithDependencies(nodes);

                if (ProjectReferences == null)
                    return false;
            }
            else
            {
                ProjectReferences = nodes.Values
                    .OrderBy(item => item.OriginalOrder)
                    .Select(item => item.CreateProjectReferenceItem(buildOrder: 0))
                    .ToArray();
            }

            return true;
        }

        private void EvaluateProjectsMetadata(IEnumerable<ProjectNode> nodes)
        {
            using (ProjectCollection projectCollection = new ProjectCollection())
            {
                foreach (var node in nodes)
                {
                    var globalProperties = new Dictionary<string, string>
                    {
                        { "Configuration", node.ActiveConfiguration },
                        { "Platform", node.ActivePlatform }
                    };

                    node.AddAdditionalProperties(globalProperties);

                    var msbuildProject = projectCollection.LoadProject(node.ProjectPath, globalProperties, toolsVersion: null);
                    node.SetProjectMetadata(msbuildProject, CustomBuild);
                }
            }
        }

        private bool TryAddProjectReferencesToDependentProjects(Dictionary<string, ProjectNode> projects)
        {
            foreach (var project in projects.Values)
            {
                foreach (string refPath in project.ProjectReferencePaths)
                {
                    if (!projects.TryGetValue(refPath, out var refProject))
                    {
                        Log.LogError("The project reference has not been found in the solution ['{0}' -> '{1}']", project.ProjectName, refPath);
                        return false;
                    }

                    refProject.AddDependentProject(project);
                }
            }

            return true;
        }

        private bool TryAddDependsOnToDependentProjects(Dictionary<string, ProjectNode> projects)
        {
            var projectNameMap = (
                from p in projects.Values
                let projectName = Path.GetFileNameWithoutExtension(p.ProjectPath)
                group p by projectName)
                .ToDictionary(item => item.Key, item => item.ToList());

            foreach (var project in projects.Values)
            {
                foreach (string dependencyPath in project.DependsOn)
                {
                    ProjectNode matchedParent = null;

                    if (!Path.HasExtension(dependencyPath))
                    {
                        var possibleProjects = projectNameMap[dependencyPath];

                        if (projectNameMap.TryGetValue(dependencyPath, out var parents))
                        {
                            if (parents.Count > 1)
                            {
                                Log.LogError("Ambiguous project name specified in DependsOn, an unambiguous project path must be specified ['{0}' -> '{1}']", project.ProjectName, project.DependsOn);
                                return false;
                            }

                            matchedParent = parents[0];
                        }
                    }
                    else if (Path.IsPathRooted(dependencyPath))
                    {
                        projects.TryGetValue(dependencyPath, out matchedParent);
                    }
                    else
                    {
                        string fullPath = Path.GetFullPath(Path.Combine(MSBuildProjectDirectory, dependencyPath));
                        projects.TryGetValue(fullPath, out matchedParent);
                    }

                    if (matchedParent == null)
                    {
                        Log.LogError("The project specified in DependsOn has not been found in the solution ['{0}' -> '{1}']", project.ProjectName, dependencyPath);
                        return false;
                    }

                    matchedParent.AddDependentProject(project);
                }
            }

            return true;
        }

        private ITaskItem[] GetOrderedProjectReferenceWithDependencies(Dictionary<string, ProjectNode> nodes)
        {
            HashSet<ProjectNode> visited = new HashSet<ProjectNode>();

            var references = new List<ITaskItem>(nodes.Count);
            var buildStep = nodes.Values.Where(item => !item.HasDependencies).ToList();
            int buildOrder = 0;

            while (buildStep.Count > 0)
            {
                var currentStep = buildStep.ToArray();

                foreach (var project in currentStep)
                {
                    if (!visited.Add(project))
                    {
                        Log.LogError("Cyclic dependency detected for project '{0}'", project.ProjectName);
                        return null;
                    }
                }

                var referenceItems = currentStep
                    .OrderBy(item => item.OriginalOrder)
                    .Select(item => item.CreateProjectReferenceItem(buildOrder));

                references.AddRange(referenceItems);

                buildStep.Clear();

                foreach (var node in currentStep)
                    buildStep.AddRange(node.DependentProjects);

                ++buildOrder;
            }

            return references.ToArray();
        }
    }
}