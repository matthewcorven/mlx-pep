# Aspire Zig AppHost Planning Context

This bundle is generated deterministically from the current default branch of the public `microsoft/aspire` repository.
Use it to evaluate long-code-research planning quality for adding Zig as a supported AppHost language alongside C# and TypeScript.

## Unsupported-language diagnostic in the AppHost build targets

Source: `src/Aspire.Hosting.AppHost/build/Aspire.Hosting.AppHost.in.targets`

```text
140:                       Lines="%(AspireHostProjectMetadataSource.Source)"
141:                       WriteOnlyWhenDifferent="true" />
142:     <ItemGroup>
143:       <FileWrites Include="$(_AspireIntermediatePath)references\_AppHost.ProjectMetadata.g.cs" />
144:       <Compile Include="$(_AspireIntermediatePath)references\_AppHost.ProjectMetadata.g.cs" />
145:     </ItemGroup>
146:   </Target>
147: 
148:   <Target Name="_WarnOnUnsupportedLanguage" Condition="'$(Language)' != 'C#'">
149:     <Warning Code="ASPIRE001" Text="The '$(Language)' language isn't fully supported by Aspire - some code generation targets will not run, so will require manual authoring." HelpLink="https://aka.ms/aspire/diagnostics/aspire001" />
150:   </Target>
151: 
152:   <!--
153:   Validates that all the ProjectReferences of an Aspire AppHost project are executables and
154:   informs the developer to set 'IsAspireProjectResource=false' if they really intended on ProjectReferencing a library.
155:   -->
156:   <Target Name="_ValidateAspireHostProjectResources"
```

## Published diagnostic text for partially supported AppHost languages

Source: `docs/list-of-diagnostics.md`

```text
5: | Diagnostic ID | Severity | Description | Location |
6: | ------------- | -------- | ----------- | -------- |
7: | `ASPIRE001` | Warning | The '\[ProjectLanguage\]' language isn't fully supported by Aspire - some code generation targets will not run, so will require manual authoring. | [src/Aspire.Hosting.AppHost/build/Aspire.Hosting.AppHost.in.targets](../src/Aspire.Hosting.AppHost/build/Aspire.Hosting.AppHost.in.targets) |
8: | `ASPIRE002` | Warning | '\[ProjectName\]' is an Aspire AppHost project but necessary dependencies aren't present. Are you missing an Aspire.Hosting.AppHost PackageReference? | [src/Aspire.Hosting.Sdk/SDK/Sdk.in.targets](../src/Aspire.Hosting.Sdk/SDK/Sdk.in.targets) |
9: | `ASPIRE003` | Warning | '\[ProjectName\]' is an Aspire AppHost project that requires Visual Studio version 17.10 or above to work correctly. You are using version $(MSBuildVersion). | [src/Aspire.Hosting.Sdk/SDK/Sdk.in.targets](../src/Aspire.Hosting.Sdk/SDK/Sdk.in.targets) |
```

## CLI registration points for language discovery, guest AppHost projects, and TypeScript tooling checks

Source: `src/Aspire.Cli/Program.cs`

```text
498: 
499:         // AppHost server session factory for RPC communication.
500:         builder.Services.AddSingleton<IAppHostServerSessionFactory, AppHostServerSessionFactory>();
501: 
502:         // AppHost project handlers.
503:         builder.Services.AddSingleton<DotNetAppHostProject>();
504:         builder.Services.AddSingleton<Func<LanguageInfo, GuestAppHostProject>>(sp =>
505:         {
506:             return language => ActivatorUtilities.CreateInstance<GuestAppHostProject>(sp, language);
507:         });
508:         builder.Services.AddSingleton<IAppHostProjectFactory, AppHostProjectFactory>();
509: 
510:         // Environment checking services.
511:         builder.Services.AddSingleton<IEnvironmentCheck, AspireVersionCheck>();
512:         builder.Services.AddSingleton<IEnvironmentCheck, WslEnvironmentCheck>();
513:         builder.Services.AddSingleton<IEnvironmentCheck, DotNetSdkCheck>();
514:         builder.Services.AddSingleton<IEnvironmentCheck, TypeScriptAppHostToolingCheck>();
515:         builder.Services.AddSingleton<IEnvironmentCheck, DeprecatedWorkloadCheck>();
516:         builder.Services.AddSingleton<IEnvironmentCheck, DevCertsCheck>();
```

## CLI output format spec showing AppHost discovery for C# and TypeScript

Source: `docs/specs/cli-output-formats.md`

```text
24: 
25: ```json
26: [
27:   {
28:     "path": "/path/to/MyApp.AppHost/MyApp.AppHost.csproj",
29:     "language": "C#",
30:     "status": "buildable"
31:   },
32:   {
33:     "path": "/path/to/ts-app/apphost.ts",
34:     "language": "TypeScript",
35:     "status": "possibly-unbuildable"
36:   }
37: ]
38: ```
39: 
40: Use `--format json --stream` to receive discovery results as NDJSON, with one complete AppHost candidate object per line. `--stream` is valid only with `--format json`.
41: 
42: ```json
43: {"path":"/path/to/MyApp.AppHost/MyApp.AppHost.csproj","language":"C#","status":"buildable"}
44: {"path":"/path/to/ts-app/apphost.ts","language":"TypeScript","status":"possibly-unbuildable"}
45: ```
46: 
47: Stream output is emitted in arrival order from parallel discovery; lines are not sorted. The non-streaming `--format json` snapshot above is sorted by `path`. If you need a deterministic order for streamed output, pipe through your own sort step (for example `jq -s 'sort_by(.path)'`).
48: 
```

## TypeScript polyglot API compatibility notes and checked validation surfaces

Source: `docs/ci/typescript-api-compat.md`

```text
77: ```text
78: BREAK capability-removed Aspire.Hosting.Redis Aspire.Hosting.Redis/withRedisCommander -- https://github.com/microsoft/aspire/issues/16961 -- Removed unsupported API before GA
79: ```
80: 
81: Suppression matching is exact. Unused suppressions fail the check so stale entries are removed when the API surface changes again.
82: 
83: ## Generated TypeScript declarations
84: 
85: This check currently compares the ATS source surface that feeds TypeScript generation. Generator implementation changes are still covered by TypeScript code generation tests and `tests/PolyglotAppHosts/*/TypeScript` validation, but they are not yet classified against a generated `.d.ts` baseline.
86: 
87: If generator-only API shape changes need the same breaking-change treatment, extend `tools/TypeScriptApiCompat` to generate declaration-only output from the checked-in TypeScript validation AppHosts and compare those declarations with the same suppression format.
```
