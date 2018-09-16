# Sdk-style solution file (.slnproj)

`MSBuild.SolutionSdk` is the equivalent of the new csproj "sdk-style" format applied to sln files.

With `MSBuild.SolutionSdk`, the content of a solution file can be as lean as this:
```xml
<Project Sdk="MSBuild.SolutionSdk/X.X.X" />
```

>X.X.X represent the latest version of the [MSBuild.SolutionSdk](https://www.nuget.org/packages/MSBuild.SolutionSdk/) NuGet package.

# Comparison with sln files
Slnproj may not be meant for you if you only work in Visual Studio since they are not supported*, but it can be an interesting choice for working with VS Code or creating build-only solution files.
## .slnproj
Pros | Cons
---- | ----
Readable/editable file format | Not supported by Visual Studio*
Easy to merge in source control | 
Sane configuration management | 

## .sln
Pros | Cons
---- | ----
Supported by Visual Studio | Nearly impossible to edit without tool
&#xfeff; | Hard to merge in source control conflicts
&#xfeff; | Configuration management impossible without tool
&#xfeff; | Even with tool configuration management is hard

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
        <Project Include="ProjectA\ProjectA.csproj" Configuration="windows-debug" />
    </ItemGroup>

</Project>
```

Here, the `Debug` configuration is overriden by `windows-debug` for ProjectA.csproj.

## Build a project twice
```xml
<Project Sdk="MSBuild.SolutionSdk/X.X.X">

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
