# Sdk-style solution file (.slnproj)

`MSBuild.SolutionSdk` is the equivalent of the new csproj "sdk-style" format applied to sln files.

With `MSBuild.SolutionSdk`, the content of a solution file can be as lean as this:
```xml
<Project Sdk="MSBuild.SolutionSdk/X.X.X" />
```

>X.X.X represent the latest version of the [MSBuild.SolutionSdk](https://www.nuget.org/packages/MSBuild.SolutionSdk/) NuGet package.

# Building a .slnproj
A .slnproj file is a standard msbuild project, the command line build experience is the same as a .sln file.

The default properties are `Configuration=Debug;Platform=AnyCPU` and the default target is `Build`.

```shell
msbuild MySolution.slnproj
msbuild MySolution.slnproj /p:Configuration=Release /t:Rebuild
```

The supported targets are:
- Build
- Clean
- Rebuild
- Restore
- Publish
- ListProjects

# Comparison with sln files
Slnproj may not be meant for you if you only work in Visual Studio since they are not supported*, but it can be an interesting choice for working with VS Code or creating build-only solution files.
## .slnproj
Pros | Cons
---- | ----
Readable/editable file format | Not supported by Visual Studio*
Easy to merge in source control | 
Sane and powerful configuration management | 

## .sln
Pros | Cons
---- | ----
Supported by Visual Studio | Nearly impossible to edit without tool
&#xfeff; | Hard to merge in source control conflicts
&#xfeff; | Configuration management impossible without tool
&#xfeff; | Even with tool configuration management is limited

*\* .slnproj can work with "Visual Studio -> Open Folder"*

# Examples
## The defaults

```xml
<Project Sdk="MSBuild.SolutionSdk/X.X.X" />
```

By default, all csproj/vbproj/fsproj/vcxproj under the folder hierarchy of the solution file will be included in the solution. For simple solutions, the .slnproj file may never have to be edited again.

## Explicit project inclusion

```xml
<Project Sdk="MSBuild.SolutionSdk/X.X.X">

    <PropertyGroup>
        <EnableDefaultProjectItems>false</EnableDefaultProjectItems>
    </PropertyGroup>

    <ItemGroup>
        <Project Include="ProjectA\ProjectA.csproj" />
        <Project Include="ProjectB\ProjectB.csproj" />
    </ItemGroup>

</Project>
```

This .slnproj disabled automatic project include by setting the `EnableDefaultProjectItems` property to `false`. Projects can then be added by specifying `Project` elements in an `ItemGroup`.

## Custom configuration management
```xml
<Project Sdk="MSBuild.SolutionSdk/X.X.X">

    <ItemGroup Condition="'$(Configuration)' == 'Debug'">
        <Project Update="ProjectA\ProjectA.csproj" Configuration="windows-debug" />
    </ItemGroup>

</Project>
```

Here, the `Debug` configuration is overriden by `windows-debug` for ProjectA.csproj.

## Build a project twice
```xml
<Project Sdk="MSBuild.SolutionSdk/X.X.X">

    <PropertyGroup>
        <EnableDefaultProjectItems>false</EnableDefaultProjectItems>
    </PropertyGroup>

    <ItemGroup Condition="'$(Configuration)' == 'Debug'">
        <Project Include="ProjectA\ProjectA.csproj" Configuration="windows-debug" />
        <Project Include="ProjectA\ProjectA.csproj" Configuration="linux-debug" />
    </ItemGroup>

    <ItemGroup Condition="'$(Configuration)' == 'Release'">
        <Project Include="ProjectA\ProjectA.csproj" Configuration="windows-release" />
        <Project Include="ProjectA\ProjectA.csproj" Configuration="linux-release" />
    </ItemGroup>

</Project>
```

You can include the same project multiple times with different metadata to build different savor of the same project with a single msbuild command.

## Declaring project dependencies
```xml
<Project Sdk="MSBuild.SolutionSdk/X.X.X">

    <PropertyGroup>
        <EnableDefaultProjectItems>false</EnableDefaultProjectItems>
    </PropertyGroup>

    <ItemGroup>
        <Project Include="ProjectA\ProjectA.csproj" />
        <Project Include="ProjectB\ProjectB.csproj" DependsOn="ProjectA" />
    </ItemGroup>

</Project>
```

The build order is normally enforced by `ProjectReference`. Otherwise, explicit project dependencies can be declared to enforce a custom build order.

## Automatically skipping unsupported configurations and platforms

You may have defined custom build configurations that not every project in your solution implements. You would typically go in the Visual Studio Configuration Manager and uncheck those projects to prevent them from building.

This is done automatically by .slnproj, but you need to add a `PackageReference` to all projects (ideally declared once in Directory.Build.props).

```xml
    <PackageReference Include="MSBuild.SolutionSdk.Hook" Version="X.X.X" />
```

# Roadmap
- Full documentation
- Integrate with SlnGen

# Open Questions
- Should the Sdk import and extends Microsoft.Common props & targets?
- Otherwise, should it import Directory.Build props & targets?
