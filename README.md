# Sdk-style solution file (slnproj)

`MSBuild.SolutionSdk` is the equivalent of the new csproj "sdk-style" format applied to sln files.

With `MSBuild.SolutionSdk`, the content of a solution file can be as lean as this:
```xml
<Project Sdk="MSBuild.SolutionSdk/X.X.X" />
```


By default, all csproj/vbproj/fsproj/vcxproj under the folder hierarchy of the solution file will be included in the solution.

>X.X.X represent the latest version of the `MSBuild.SolutionSdk` package.

>While the solution file can have any extension, the proposed convention is to use .slnproj.


