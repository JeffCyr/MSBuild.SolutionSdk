# Sdk-style solution file (slnproj)

`MSBuild.SolutionSdk` is the equivalent of the new csproj "sdk-style" format applied to sln files.

With `MSBuild.SolutionSdk`, the content of a solution file can be as lean as this:
```xml
<Project Sdk="MSBuild.SolutionSdk/X.X.X" />
```


By default, all csproj/vbproj/fsproj/vcxproj under the folder hierarchy of the solution file will be included in the solution.

>X.X.X represent the latest version of the `MSBuild.SolutionSdk` package.

>While the solution file can have any extension, the proposed convention is to use .slnproj.

# Comparison with sln files
Slnproj may not be meant for you if you only work in Visual Studio since they are not supported*, but it can be an interesting choice for working with VS Code or creating build-only solution files.
## slnproj
Pros | Cons
---- | ----
Readable/editable file format | Not supported by Visual Studio*
Easy to merge in source control | 
Sane configuration management | 

**Slnproj can work with "Visual Studio -> Open Folder"*

## sln
Pros | Cons
---- | ----
Supported by Visual Studio | Nearly impossible to edit without tool
&#xfeff; | Hard to merge in source control conflicts
&#xfeff; | Configuration management impossible without tool
&#xfeff; | Even with tool configuration management is hard
