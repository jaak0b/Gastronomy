# T001: Repository scaffolding

## 1. Objective

Scaffold the entire GastronomyApp repository so that every later task lands in an existing,
building, testable structure. This task produces no domain logic, no endpoints, and no UI beyond
what the official templates generate. It creates the .NET solution and projects, the Avalonia
desktop shell, the Vue frontend, and the shared build configuration files, wired together so that
one command builds the whole backend and desktop, one command runs a passing placeholder test in
every test project, and the frontend builds into the backend's static file folder. Definition of
done: `dotnet build GastronomyApp.slnx` succeeds with zero warnings, `dotnet test
GastronomyApp.slnx` passes every placeholder test, `npm run build` in `frontend/` succeeds and
writes into `backend/GastronomyApp.Api/wwwroot`, and `npm test -- --run` in `frontend/` passes.

## 2. Repository layout after completion

```
GastronomyApp/
  GastronomyApp.slnx
  Directory.Build.props
  Directory.Packages.props
  .editorconfig
  .gitignore                              (existing file, edited)
  CLAUDE.md                                (existing, untouched)
  docs/
    tasks/
      T001-scaffolding.md                 (this file)
  backend/
    CLAUDE.md                              (existing, untouched)
    GastronomyApp.Core/
      GastronomyApp.Core.csproj
      Class1.cs removed, no replacement file (empty project is fine)
    GastronomyApp.Infrastructure/
      GastronomyApp.Infrastructure.csproj
    GastronomyApp.Api/
      GastronomyApp.Api.csproj
      wwwroot/                             (created empty, gitignored, filled by frontend build)
    GastronomyApp.Core.Tests/
      GastronomyApp.Core.Tests.csproj
      ScaffoldingSmokeTest.cs
    GastronomyApp.Infrastructure.Tests/
      GastronomyApp.Infrastructure.Tests.csproj
      ScaffoldingSmokeTest.cs
    GastronomyApp.Api.Tests/
      GastronomyApp.Api.Tests.csproj
      ScaffoldingSmokeTest.cs
  desktop/
    GastronomyApp.Desktop/
      GastronomyApp.Desktop.csproj
      App.axaml / App.axaml.cs
      MainWindow.axaml / MainWindow.axaml.cs
      Program.cs
      app.manifest (template default, if generated)
    GastronomyApp.Desktop.Tests/
      GastronomyApp.Desktop.Tests.csproj
      ScaffoldingSmokeTest.cs
  frontend/
    CLAUDE.md                              (existing, untouched)
    .claude/                               (existing, untouched)
    package.json
    vite.config.ts
    tsconfig.json (and template-generated siblings)
    index.html
    src/
      core/
        placeholder.ts
      components/
      stores/
      locales/
        de.json
        en.json
      main.ts
      App.vue
      (other template-generated files: assets/, etc.)
    tests/
      core/
        placeholder.spec.ts
    e2e/
      playwright.config.ts
```

## 3. Ordered steps

Work through these in order. Run all commands from the repository root
(`E:\Development\GastronomyApp`) unless a step says otherwise. Do not run any `git` command as part
of this task; the owner commits separately.

### Step 0: Preconditions

Confirm `dotnet --version` reports a .NET 9 SDK at version 9.0.2xx or later (the slnx solution
format used in Step 2 requires this minimum), `node --version` reports a current LTS Node, and
`npm --version` works. If any tool is missing or the wrong major or minimum version, stop and
report the gap; do not attempt to install a different tool as a substitute.

### Step 1: Root build configuration files

Create `Directory.Build.props` at the repository root, so it applies uniformly to every project
under `backend/` and `desktop/` (the frontend is not MSBuild-managed and is unaffected). Full
contents:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <LangVersion>latest</LangVersion>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
```

Create `Directory.Packages.props` at the repository root with these exact package names. Do not
add any package beyond this list. Resolve each `<version>` placeholder to the latest stable
version available on nuget.org at the time you run this task (check with `dotnet package search
<PackageName>` or the NuGet website), then pin that literal version number in the file; do not
leave a placeholder or a floating version range in the committed file.

```xml
<Project>
  <ItemGroup>
    <PackageVersion Include="NUnit" Version="<latest-stable>" />
    <PackageVersion Include="NUnit3TestAdapter" Version="<latest-stable>" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="<latest-stable>" />
    <PackageVersion Include="FakeItEasy" Version="<latest-stable>" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="<latest-stable>" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="<latest-stable>" />
  </ItemGroup>
</Project>
```

`Microsoft.EntityFrameworkCore.Design` is included alongside `.Sqlite` because it is required to
run `dotnet ef migrations add` later against `GastronomyApp.Infrastructure`; it is not a package
beyond the agreed set, it is part of "EF Core SQLite packages" named in the brief.

Create `.editorconfig` at the repository root:

```ini
root = true

[*]
indent_style = space
end_of_line = lf
insert_final_newline = true
charset = utf-8-bom

[*.cs]
indent_size = 4
file_header_template = unset
csharp_style_namespace_declarations = file_scoped:error
dotnet_diagnostic.CS8321.severity = error
dotnet_style_require_accessibility_modifiers = always:error
dotnet_remove_unnecessary_suppression_exclusions = none
dotnet_diagnostic.IDE0005.severity = error

[*.{js,ts,vue,json,yml,yaml}]
indent_size = 2

[*.md]
trim_trailing_whitespace = false
```

`dotnet_diagnostic.IDE0005.severity = error` is the analyzer id for unused usings. On its own an
`.editorconfig` severity is evaluated by the IDE only; `Directory.Build.props` also sets
`EnforceCodeStyleInBuild` to `true`, which makes `dotnet build` itself run the code style analyzers
and apply these severities during the command-line build. Combined with `TreatWarningsAsErrors`,
an unused using, or a non-file-scoped namespace, fails `dotnet build`. Use UTF-8
with BOM (`charset = utf-8-bom`) for `.cs` files only because that is the .NET template default;
this line does not apply to non-C# files, which is why it sits under the `[*.cs]` section.

### Step 2: Solution file and Core project

Create the solution:

```powershell
dotnet new sln -n GastronomyApp --format slnx
```

This produces `GastronomyApp.slnx` at the root. Both `dotnet new sln --format slnx` and every
`dotnet sln <file>.slnx add` command used later in this document require a recent .NET 9 SDK
(9.0.2xx or later, already confirmed in Step 0). If the installed SDK does not support
`--format slnx`, stop and report the gap rather than falling back to the classic `.sln` format.

Create `GastronomyApp.Core`:

```powershell
dotnet new classlib -n GastronomyApp.Core -o backend/GastronomyApp.Core
```

Delete the template-generated `Class1.cs` in that project; leave the project with no source files.
Edit `backend/GastronomyApp.Core/GastronomyApp.Core.csproj` so it contains only:

```xml
<Project Sdk="Microsoft.NET.Sdk">
</Project>
```

(`TargetFramework`, `Nullable`, `ImplicitUsings` are inherited from the root
`Directory.Build.props`; do not repeat them here or in any other `.csproj` in this task.)

### Step 3: Infrastructure project

```powershell
dotnet new classlib -n GastronomyApp.Infrastructure -o backend/GastronomyApp.Infrastructure
```

Delete its template `Class1.cs`. Add the project reference to Core:

```powershell
dotnet add backend/GastronomyApp.Infrastructure reference backend/GastronomyApp.Core
```

Add the EF Core SQLite package references (versions already centrally pinned in
`Directory.Packages.props`, so no version is passed here):

```powershell
dotnet add backend/GastronomyApp.Infrastructure package Microsoft.EntityFrameworkCore.Sqlite
dotnet add backend/GastronomyApp.Infrastructure package Microsoft.EntityFrameworkCore.Design
```

### Step 4: Api project (class library, not an executable)

Decision for this task: use the plain `Microsoft.NET.Sdk` with an explicit `FrameworkReference` to
`Microsoft.AspNetCore.App`, not `Microsoft.NET.Sdk.Web` with `<OutputType>Library</OutputType>`.
Reason: `Microsoft.NET.Sdk.Web` bakes in an implicit expectation of being the deployable web
project (it wires `Microsoft.NET.Sdk.Web.ProjectSystem` targets tied to publish-as-app,
`wwwroot` static web asset packaging for a hosted app, and apphost generation logic that has to be
fought back down with `OutputType=Library`). The plain SDK plus `FrameworkReference` gives exactly
the ASP.NET Core assemblies (`Microsoft.AspNetCore.App` shared framework: Kestrel, MVC, SignalR,
static files) with none of the executable-hosting assumptions, which matches this project's actual
role: a library the desktop host calls into. Create it manually rather than through a template,
because `dotnet new` has no template for this exact shape:

```powershell
mkdir backend/GastronomyApp.Api
```

Create `backend/GastronomyApp.Api/GastronomyApp.Api.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
</Project>
```

Add project references:

```powershell
dotnet add backend/GastronomyApp.Api reference backend/GastronomyApp.Core
dotnet add backend/GastronomyApp.Api reference backend/GastronomyApp.Infrastructure
```

Create the empty folder `backend/GastronomyApp.Api/wwwroot/` (it must exist so the frontend build
has somewhere to write into; an empty directory is not tracked by git on its own, which is fine
since the frontend build step or a later task populates it. If you need a placeholder to make the
directory creatable by your tooling, use a `.gitkeep` file with no content and delete it once
`wwwroot` gets real content from Step 8).

Do not add any source file to `GastronomyApp.Api` in this task. It is not required to contain a
`Program.cs`, a `Startup`, or any endpoint yet; that is later work. An empty class library with
only the `FrameworkReference` is correct for this task's scope.

### Step 5: Add all four backend/desktop production projects to the solution

```powershell
dotnet sln GastronomyApp.slnx add backend/GastronomyApp.Core
dotnet sln GastronomyApp.slnx add backend/GastronomyApp.Infrastructure
dotnet sln GastronomyApp.slnx add backend/GastronomyApp.Api
```

(Desktop project is added in Step 7 once it exists.)

### Step 6: Backend test projects

For each of the three test projects below: create with the NUnit template, delete the
template-generated placeholder test class, add the project reference to the production project it
tests, add FakeItEasy, and write the honestly-named smoke test.

```powershell
dotnet new nunit -n GastronomyApp.Core.Tests -o backend/GastronomyApp.Core.Tests
dotnet new nunit -n GastronomyApp.Infrastructure.Tests -o backend/GastronomyApp.Infrastructure.Tests
dotnet new nunit -n GastronomyApp.Api.Tests -o backend/GastronomyApp.Api.Tests
```

The NUnit template already references `NUnit`, `NUnit3TestAdapter`, and `Microsoft.NET.Test.Sdk`
as `PackageReference` entries; because central package management is on, edit each generated
`.csproj` to remove the `Version="..."` attribute from those three `PackageReference` lines (the
version now comes from `Directory.Packages.props`; a version attribute present on a
`PackageReference` while central package management is enabled is a build error). Delete the
template's generated test class file (`Tests.cs` or `UnitTest1.cs`, whichever the installed
template produced).

The template also emits a `PackageReference Include="coverlet.collector"` with a version
attribute. Architect decision, settled: remove that `PackageReference` entirely from every test
project's `.csproj`, rather than stripping its version. This repository deliberately has no
coverage gate (root rules: mutation testing and a coverage gate are deliberately not adopted yet),
so the collector has no job here, and it is not on the allowed package list in this document. Do
not add a matching `PackageVersion` entry for it either; simply delete the line. Apply the same
rule to any other template-emitted package reference you encounter anywhere in this task that is
not on this document's allowed package list: delete it, then confirm the affected project still
builds. If removing it causes a build failure you cannot resolve by deletion alone, stop and
report the gap instead of adding the package to the allowed list yourself.

Add references:

```powershell
dotnet add backend/GastronomyApp.Core.Tests reference backend/GastronomyApp.Core
dotnet add backend/GastronomyApp.Core.Tests package FakeItEasy

dotnet add backend/GastronomyApp.Infrastructure.Tests reference backend/GastronomyApp.Infrastructure
dotnet add backend/GastronomyApp.Infrastructure.Tests package FakeItEasy

dotnet add backend/GastronomyApp.Api.Tests reference backend/GastronomyApp.Api
dotnet add backend/GastronomyApp.Api.Tests package FakeItEasy
```

Write the same smoke test shape into each of the three, with the project's own namespace. For
`GastronomyApp.Core.Tests`, create `backend/GastronomyApp.Core.Tests/ScaffoldingSmokeTest.cs`:

```csharp
namespace GastronomyApp.Core.Tests;

public class ScaffoldingSmokeTest
{
    [Test]
    public void TestRunner_Executes_Passes()
    {
        Assert.That(1 + 1, Is.EqualTo(2));
    }
}
```

Repeat identically for `backend/GastronomyApp.Infrastructure.Tests/ScaffoldingSmokeTest.cs` with
namespace `GastronomyApp.Infrastructure.Tests`, and for
`backend/GastronomyApp.Api.Tests/ScaffoldingSmokeTest.cs` with namespace
`GastronomyApp.Api.Tests`. Delete this fixture in the first task that adds a real fixture to that
project; do not accumulate real tests alongside it.

Add all three to the solution:

```powershell
dotnet sln GastronomyApp.slnx add backend/GastronomyApp.Core.Tests
dotnet sln GastronomyApp.slnx add backend/GastronomyApp.Infrastructure.Tests
dotnet sln GastronomyApp.slnx add backend/GastronomyApp.Api.Tests
```

### Step 7: Desktop project

Install the official Avalonia templates if not already installed:

```powershell
dotnet new install Avalonia.Templates
```

Use current stable Avalonia 11.x (whatever `Avalonia.Templates` installs as latest stable at
execution time; do not pin an older 11.x version deliberately). Create the MVVM app template:

```powershell
dotnet new avalonia.mvvm -n GastronomyApp.Desktop -o desktop/GastronomyApp.Desktop
```

Add the reference to Api:

```powershell
dotnet add desktop/GastronomyApp.Desktop reference backend/GastronomyApp.Api
```

Open `desktop/GastronomyApp.Desktop/GastronomyApp.Desktop.csproj` and remove any
`<TargetFramework>`, `<Nullable>`, or `<ImplicitUsings>` property the template wrote, so the
project inherits those from the root `Directory.Build.props` instead of overriding it (the
template will have written `net9.0` or similar explicitly; delete that line so there is exactly
one source of truth for the target framework across the repository). Leave Avalonia-specific
properties (`<AvaloniaUseCompiledBindingsByDefault>`, etc.) exactly as the template generated
them.

Set the window title. Open `desktop/GastronomyApp.Desktop/MainWindow.axaml` and set the `Title`
attribute on the root `Window` element to `GastronomyApp`. Architect decision, settled: this
literal is a deliberate, documented exception to the no-string-literals-in-axaml rule, because
"GastronomyApp" is the product name, invariant across German and English, not a translatable
sentence. Do not build resx or vue-i18n plumbing to localize it in this task; the localization
task revisits this literal once that machinery exists. Do not add any other control, menu, tray
icon, or server-hosting code; the template's default single blank window with that title is the
full scope of this task for the desktop UI.

The Avalonia MVVM template's generated `Program.cs` contains a `static` `Main` method and a
`static` `BuildAvaloniaApp` method. Leave both as the template generated them: they are forced by
the framework's application entry point convention, which falls under the framework-metadata
registration exception to the no-static-methods rule, and are not something this task, or any
later one, should try to rewrite into an instance method.

Add to the solution:

```powershell
dotnet sln GastronomyApp.slnx add desktop/GastronomyApp.Desktop
```

### Step 7b: Desktop test project

```powershell
dotnet new nunit -n GastronomyApp.Desktop.Tests -o desktop/GastronomyApp.Desktop.Tests
```

Strip the `Version` attributes from its NUnit/test-SDK `PackageReference` entries as in Step 6.
Delete its template test class file. Add references:

```powershell
dotnet add desktop/GastronomyApp.Desktop.Tests reference desktop/GastronomyApp.Desktop
dotnet add desktop/GastronomyApp.Desktop.Tests package FakeItEasy
dotnet add desktop/GastronomyApp.Desktop.Tests package Avalonia.Headless
dotnet add desktop/GastronomyApp.Desktop.Tests package Avalonia.Headless.NUnit
```

`Avalonia.Headless` and `Avalonia.Headless.NUnit` are not yet listed in `Directory.Packages.props`
because that file's brief only named NUnit, NUnit3TestAdapter, Microsoft.NET.Test.Sdk, FakeItEasy,
and the EF Core SQLite packages. Add both to `Directory.Packages.props` now, pinned to the latest
stable version matching the Avalonia version the template installed in Step 7 (Avalonia package
versions must match across the solution or the build fails), so this test project's
`PackageReference` entries stay version-free like every other centrally managed package.

Write `desktop/GastronomyApp.Desktop.Tests/ScaffoldingSmokeTest.cs`:

```csharp
namespace GastronomyApp.Desktop.Tests;

public class ScaffoldingSmokeTest
{
    [Test]
    public void TestRunner_Executes_Passes()
    {
        Assert.That(1 + 1, Is.EqualTo(2));
    }
}
```

Do not write an Avalonia headless UI test in this task; the packages are installed so a later task
can add one, but proving the plain NUnit runner works is enough for this smoke test.

Add to the solution:

```powershell
dotnet sln GastronomyApp.slnx add desktop/GastronomyApp.Desktop.Tests
```

### Step 8: Frontend scaffold

From the repository root:

```powershell
npm create vite@latest frontend -- --template vue-ts
```

If the `frontend/` directory already contains `CLAUDE.md` and `.claude/`, the scaffolding
command must not overwrite or delete either. If `npm create vite@latest` refuses to run because
the target directory is non-empty, do not force it or delete existing files to make way. Instead
scaffold into a temporary empty directory and move only the newly generated files and folders
(`package.json`, `vite.config.ts`, `tsconfig*.json`, `index.html`, `src/`, `public/` if present,
`.gitignore` if the template writes one, `README.md` if generated) into `frontend/`, without
touching the pre-existing `CLAUDE.md` or `.claude/`. Verify after the move that both
`frontend/CLAUDE.md` and `frontend/.claude/` (with its `skills/` subfolder) still exist and are
byte-for-byte unchanged from before this step.

Install dependencies and the agreed additional packages, from inside `frontend/`:

```powershell
npm install
npm install pinia vue-i18n
npm install -D vitest @vue/test-utils jsdom
npm install -D @playwright/test
```

Do not install any package beyond this list (Vite, Vue, TypeScript, and vue-tsc come from the
`create vite` template itself and are not "additional"). If you judge that another package is
needed to satisfy this task, stop and report the gap instead of adding it.

Install Playwright's browser binaries (this is a one-time download, not an npm package, and is
allowed per the brief):

```powershell
npx playwright install
```

### Step 9: Frontend folder layout

Inside `frontend/src/`, create these folders if the template did not:

- `src/core/` with a placeholder module `src/core/placeholder.ts`:

  ```typescript
  export function scaffoldingIdentity<T>(value: T): T {
    return value;
  }
  ```

  This file has no Vue import and touches no DOM, matching the `src/core/` framework-agnostic rule
  for real code that lands here later. Delete it in the same task that adds the first real
  `src/core/` module.

- `src/components/` (empty folder, no file needed beyond what the template already placed there).
- `src/stores/` (empty folder; Pinia store modules land here later).
- `src/locales/` with two files:

  `src/locales/de.json`:
  ```json
  {
    "app": {
      "title": "GastronomyApp"
    }
  }
  ```

  `src/locales/en.json`:
  ```json
  {
    "app": {
      "title": "GastronomyApp"
    }
  }
  ```

Create `frontend/tests/core/placeholder.spec.ts`:

```typescript
import { describe, expect, it } from 'vitest'
import { scaffoldingIdentity } from '../../src/core/placeholder'

describe('scaffoldingIdentity', () => {
  it('returns the value it was given', () => {
    expect(scaffoldingIdentity(42)).toBe(42)
  })
})
```

This is the "real Vitest test importing it" required by the brief: it is a genuine, currently
meaningful test of the placeholder module's only behavior, not a fake domain test; it is still
scaffolding and gets replaced once `src/core/placeholder.ts` is replaced by real logic.

Create the `frontend/e2e/` folder with `frontend/e2e/playwright.config.ts`:

```typescript
import { defineConfig } from '@playwright/test'

export default defineConfig({
  testDir: '.',
  fullyParallel: true,
})
```

Do not add any `.spec.ts` file under `e2e/` in this task; Playwright is installed and configured
with no tests yet, per the brief.

### Step 10: Wire Vitest into the frontend build

Ensure `frontend/vite.config.ts` (the template generates this file already) configures the build
output directory to land inside the backend's static file folder, and add a `test` block for
Vitest. Full expected contents:

```typescript
import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  build: {
    outDir: '../backend/GastronomyApp.Api/wwwroot',
    emptyOutDir: true,
  },
  test: {
    environment: 'jsdom',
    include: ['tests/**/*.spec.ts'],
  },
})
```

Adjust only the `build` and `test` blocks if the template already generated a `resolve.alias`
block with different but equivalent content; do not remove the template's existing plugin or
alias setup, only add what is missing.

Add a `test` script to `frontend/package.json` if the template did not already add one:

```json
"test": "vitest"
```

(`npm test` then runs Vitest; `npm test -- --run` runs it once instead of in watch mode, which is
the verification command below.)

### Step 11: Root `.gitignore` edit

Open the existing root `.gitignore` and add a new line under the existing `# .NET` section (or a
new small section directly below it), so the built frontend output inside the Api project is never
committed:

```
backend/GastronomyApp.Api/wwwroot/
```

Do not remove, reorder, or otherwise edit any existing line in `.gitignore`; only add this one
line.

### Step 12: Final full-repo build and test pass

Run the four verification commands in Section 5 of this document from the repository root, in
order, and confirm each succeeds before considering this task complete.

## 4. Constraints

Carried forward from the root, backend, and frontend `CLAUDE.md` files; they bind everything you
write in this task exactly as they bind later ones.

- No code comments anywhere, including in `.csproj`, `.json`, `.editorconfig`, `.yml`, or any other
  config or template file you touch. The only allowed comment is a short, non-obvious why the code
  cannot express; nothing in this task needs one. (Markdown, including this document, is exempt.)
- No static methods or properties in anything you write beyond what the templates force on you
  (framework metadata registration is the sole exception).
- Zero AI attribution anywhere: no such text in any file, filename, or generated content.
- Never use the em-dash character, and never a hyphen substituting for one, anywhere you write
  text, including inside this document if you edit it further.
- Do not add any NuGet or npm package beyond the ones named in this document. If you believe
  another package is genuinely required to complete a step as written, stop and report the gap
  rather than adding it.
- `frontend/.claude/` and all three existing `CLAUDE.md` files (`GastronomyApp/CLAUDE.md`,
  `backend/CLAUDE.md`, `frontend/CLAUDE.md`) must survive this task byte-for-byte unchanged.
- Every placeholder test fixture must be named `ScaffoldingSmokeTest` with a method named
  `TestRunner_Executes_Passes` (or, for the frontend, the equivalent honestly-named placeholder
  spec described in Step 9), so nobody mistakes it for real coverage. Delete each one in the same
  task that adds the first real fixture for that project.
- The TDD red-first rule from the root `CLAUDE.md` does not apply to this task. There is no
  behaviour yet to drive with a failing test; the placeholder tests exist only to prove each test
  runner executes and reports correctly. Do not invent a fake red phase, a fake failing assertion,
  or any other simulation of TDD process for this task.
- No domain code, no REST endpoints, no SignalR hub, no EF Core `DbContext`, no printer transport,
  no authentication, no admin UI screens, no order screens. If a step's instructions seem to imply
  writing any of that, stop and re-read the step; none of them require it.
- No trademarked words in file names or identifiers you introduce.
- Follow every `Directory.Build.props` / `Directory.Packages.props` mechanism exactly as specified;
  do not add a per-project `<TargetFramework>` or an explicit `PackageReference` version anywhere
  once central management is in place, since either one produces a build error or defeats the
  point of centralizing them.

## 5. Verification

Run all four from the repository root. Quote the real command output; do not claim success without
having run the command.

1. ```powershell
   dotnet build GastronomyApp.slnx
   ```
   Success looks like: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)` across every
   project (Core, Infrastructure, Api, Core.Tests, Infrastructure.Tests, Api.Tests, Desktop,
   Desktop.Tests).

2. ```powershell
   dotnet test GastronomyApp.slnx
   ```
   Success looks like: one passed test per test project (four total: Core.Tests,
   Infrastructure.Tests, Api.Tests, Desktop.Tests), each reporting `Passed:` and zero `Failed:`,
   with a final summary of 4 total, 4 passed, 0 failed.

3. ```powershell
   cd frontend
   npm run build
   ```
   Success looks like: the Vite/vue-tsc build completing without a type error, and
   `backend/GastronomyApp.Api/wwwroot/` containing the built `index.html` and hashed asset files
   afterward (confirm with a directory listing after the command exits).

4. ```powershell
   cd frontend
   npm test -- --run
   ```
   Success looks like: Vitest reporting the single `scaffoldingIdentity` test passing, with a
   summary showing `1 passed` and `0 failed`.

**Contingency for template-generated warnings:** `TreatWarningsAsErrors` and
`EnforceCodeStyleInBuild` apply to every project, including the ones a template generated, so a
warning inside template-authored code (Avalonia's generated files, or a type issue vue-tsc reports
in template-authored `.vue` files) fails command 1 or command 3 above exactly as it would in
hand-written code. When that happens, fix the generated file so it is warning-clean if the fix is
mechanical and obvious (an unused using, a missing accessibility modifier, an easily-typed
generic). If the fix is not mechanical and obvious, stop and report the warning instead of guessing
at a structural change. Never suppress a warning with a pragma, an `#nullable disable`, a
`suppressWarnings` attribute, or an inline directive, and never relax `TreatWarningsAsErrors` or
`EnforceCodeStyleInBuild` in `Directory.Build.props` to make a warning go away.

## 6. Out of scope

Explicitly not part of this task; do not attempt any of it here even if it seems like a natural
next step:

- Hosting the ASP.NET Core `GastronomyApp.Api` inside the Avalonia desktop process. The desktop
  project references Api only so the reference compiles; no `WebApplication` is built, configured,
  or started anywhere in this task.
- Any domain model, use case, port interface, EF Core entity, migration, REST endpoint, SignalR
  hub, printer transport implementation, or authentication/device-token logic.
- Any UI beyond the Avalonia template's default blank window (retitled) and the Vite template's
  default starter page. No admin screens, no order screens, no localization wiring beyond the two
  placeholder locale files existing on disk.
- Continuous integration configuration of any kind (no GitHub Actions, no build pipeline files).
- A `README.md`, or any other documentation file beyond this one.
- Any `git` command. The owner reviews and commits this work separately.
- Publishing a single-file self-contained executable for the backend or the desktop app; this task
  only needs `dotnet build` and `dotnet test` to succeed, not `dotnet publish`.
