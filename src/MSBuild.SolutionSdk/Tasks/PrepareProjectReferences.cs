using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MSBuild.SolutionSdk.Tasks
{
    public class PrepareProjectReferences : Task
    {
        private class ProjectNode
        {
            public ITaskItem ProjectItem;

            public int OriginalOrder { get; }

            public string ProjectName { get; }

            public string ProjectPath { get; }

            public string DependsOn { get; }

            public string ActiveConfiguration { get; }

            public string ActivePlatform { get; private set; }

            public bool SkipProject { get; private set; }

            public List<ProjectNode> DependentProjects { get; }

            public bool HasDependencies => DependsOn != "";

            public ProjectNode(ITaskItem projectItem, int originalOrder)
            {
                DependentProjects = new List<ProjectNode>();

                ProjectItem = projectItem;
                OriginalOrder = originalOrder;

                ProjectName = projectItem.GetMetadata("Filename");
                ProjectPath = projectItem.GetMetadata("FullPath");
                DependsOn = projectItem.GetMetadata("DependsOn");
                ActiveConfiguration = projectItem.GetMetadata("Configuration");
                ActivePlatform = projectItem.GetMetadata("Platform");
            }

            public void SetProjectMetadata(ITaskItem projectMetadata)
            {
                if (!projectMetadata.GetMetadata("UsingMicrosoftNETSdk").Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    SkipProject = string.IsNullOrEmpty(projectMetadata.GetMetadata("OutputPath"));
                    return;
                }

                var supportedConfigurations = projectMetadata
                    .GetMetadata("Configurations")
                    .Split(new [] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                var supportedPlatforms = projectMetadata
                    .GetMetadata("Platforms")
                    .Split(new [] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                SkipProject =
                    !supportedConfigurations.Contains(ActiveConfiguration, StringComparer.OrdinalIgnoreCase) ||
                    !supportedPlatforms.Contains(ActivePlatform, StringComparer.OrdinalIgnoreCase);
            }

            public ITaskItem CreateProjectReferenceItem(int buildOrder)
            {
                var item = new TaskItem(ProjectItem.ItemSpec);
                item.SetMetadata("Properties", $"Configuration={ActiveConfiguration};Platform={ActivePlatform}");
                item.SetMetadata("AdditionalProperties", ProjectItem.GetMetadata("AdditionalProperties"));
                item.SetMetadata("BuildOrder", buildOrder.ToString());
                return item;
            }
        }

        [Required]
        public ITaskItem[] Projects { get; set; }

        [Required]
        public ITaskItem[] ProjectsMetadata { get; set; }

        [Output]
        public ITaskItem[] ProjectReferences { get; private set; }

        public override bool Execute()
        {
            var nodes = new Dictionary<string, ProjectNode>(Projects.Length);
            var skippedProjects = new List<ProjectNode>();
            bool hasAtLeastOneDependency = false;

            for (var i = 0; i < Projects.Length; i++)
            {
                var project = Projects[i];
                var node = new ProjectNode(project, i);

                if (node.DependsOn != "")
                    hasAtLeastOneDependency = true;

                nodes.Add(node.ProjectPath, node);
            }

            foreach (var metadata in ProjectsMetadata)
            {
                var node = nodes[metadata.ItemSpec];
                node.SetProjectMetadata(metadata);

                if (node.SkipProject)
                    skippedProjects.Add(node);
            }

            foreach (var project in skippedProjects)
            {
                Log.LogMessage("Skipped project '{0}' due to unsupported configuration or platform", project.ProjectName);
                nodes.Remove(project.ProjectPath);
            }

            if (hasAtLeastOneDependency)
            {
                if (!TryAssignDependencies(nodes))
                    return false;

                ProjectReferences = GetOrderedProjectReferenceWithDependencies(nodes);
            }
            else
            {
                ProjectReferences = nodes.Values
                    .OrderBy(item => item.OriginalOrder)
                    .Select(item => item.CreateProjectReferenceItem(buildOrder:0))
                    .ToArray();
            }

            return true;
        }

        private bool TryAssignDependencies(Dictionary<string, ProjectNode> projects)
        {
            var projectNameMap = (
                from p in projects.Values
                let projectName = Path.GetFileNameWithoutExtension(p.ProjectPath)
                group p by projectName)
                .ToDictionary(item => item.Key, item => item.ToList());

            foreach (var project in projects.Values)
            {
                if (!project.HasDependencies)
                    continue;

                string dependencyPath = project.DependsOn;
                string dependencyProjectName = Path.GetFileNameWithoutExtension(dependencyPath);

                if (!projectNameMap.TryGetValue(dependencyProjectName, out var possibleParent))
                {
                    Log.LogError("The project specified in DependsOn has not been found in the solution ['{0}' -> '{1}']", project.ProjectName, project.DependsOn);
                    return false;
                }

                if (possibleParent.Count == 1)
                {
                    possibleParent[0].DependentProjects.Add(project);
                }
                else if (!Path.HasExtension(dependencyPath))
                {
                    Log.LogError("Ambiguous project name specified in DependsOn, an unambiguous project path must be specified ['{0}' -> '{1}']", project.ProjectName, project.DependsOn);
                    return false;
                }
                else
                {
                    ProjectNode matchedParent = null;

                    foreach (var parent in possibleParent)
                    {
                        if (parent.ProjectPath.EndsWith(dependencyPath, StringComparison.OrdinalIgnoreCase))
                        {
                            if (matchedParent != null)
                            {
                                Log.LogError("Ambiguous project name specified in DependsOn, an unambiguous project path must be specified ['{0}' -> '{1}']", project.ProjectName, project.DependsOn);
                                return false;
                            }

                            matchedParent = parent;
                        }
                    }

                    if (matchedParent == null)
                    {
                        Log.LogError("The project specified in DependsOn has not been found in the solution ['{0}' -> '{1}']", project.ProjectName, project.DependsOn);
                        return false;
                    }

                    matchedParent.DependentProjects.Add(project);
                }
            }

            return true;
        }

        private ITaskItem[] GetOrderedProjectReferenceWithDependencies(Dictionary<string, ProjectNode> nodes)
        {
            var references = new List<ITaskItem>(nodes.Count);
            var buildStep = nodes.Values.Where(item => !item.HasDependencies).ToList();
            int buildOrder = 0;

            while (buildStep.Count > 0)
            {
                var currentStep = buildStep.ToArray();

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