# Your first app

About ten minutes. You end with a Svelte counter served by ASP.NET Core and reloading on save.

## 1. Make a web project

```sh
dotnet new web -o hello-husk
cd hello-husk
```

## 2. Add the packages

```sh
dotnet add package Extrode.Husk.Build --version 0.1.0-preview.5
dotnet add package Extrode.Husk.AspNetCore --version 0.1.0-preview.5
```

Open `hello-husk.csproj` and add `PrivateAssets="all"` to the `Extrode.Husk.Build` reference. It is a build tool, not code your app needs at run time:

```xml
<PackageReference Include="Extrode.Husk.Build" Version="0.1.0-preview.5" PrivateAssets="all" />
<PackageReference Include="Extrode.Husk.AspNetCore" Version="0.1.0-preview.5" />
```

The first restore downloads about 83 MB: Husk itself (6 MB) and Microsoft's V8 packages for six platforms (77 MB, shared across Husk versions). After that it is cached.

## 3. Write a component

Create `wwwroot/App.svelte`:

```svelte
<script lang="ts">
  let count = $state(0);
</script>

<button onclick={() => (count += 1)}>clicked {count} times</button>

<style>
  button { font-size: 1.25rem; }
</style>
```

## 4. Write the entry point

Create `wwwroot/main.ts`:

```ts
import { mount } from 'svelte';
import App from './App.svelte';

mount(App, { target: document.getElementById('app') as HTMLElement });
```

## 5. Write the page

Create `wwwroot/index.html`:

```html
<!doctype html>
<meta charset="utf-8">
<title>Hello Husk</title>
<link rel="stylesheet" href="/_husk/husk.css">
<script type="importmap" data-husk></script>
<div id="app"></div>
<script type="module" src="/main.js"></script>
```

The empty `<script type="importmap" data-husk>` is a marker. The build fills it with the paths of `svelte` and your files.

## 6. Serve it

Replace `Program.cs`:

```csharp
using Extrode.Husk.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddHusk();
var app = builder.Build();

app.Use((context, next) =>
{
    if (context.Request.Path == "/")
        context.Request.Path = "/index.html";
    return next(context);
});
app.UseRouting();
app.MapStaticAssets();

app.Run();
```

`MapStaticAssets` serves no default document, so the small middleware sends `/` to `/index.html`.

## 7. Run it

```sh
dotnet watch
```

Open the address it prints. Click the button.

Now edit the text in `App.svelte` and save. The page reloads with the change. Break the markup on purpose and save: the error is logged and the browser keeps showing the last good version.

## 8. Build and publish

```sh
dotnet build
dotnet publish -c Release
```

Look under `obj/`: the compiled `App.svelte.js`, its source map and `husk.css` are there. `dotnet publish` ships them with compressed copies, and no `.svelte` or `.ts` sources.

## If something goes wrong

| You see | It means |
|---|---|
| `HUSK002` | Pare's build was left out: remove the `ExcludeAssets` on your `Extrode.Pare.Build` reference, or the reference. |
| `HUSK001` | The build machine is Alpine, FreeBSD or another unsupported platform. |
| `HUSK100` | Svelte rejected your component. The message names the line and column. |
| A blank page | Check the browser console. The usual cause is a missing `data-husk` marker or `/main.js` path. |

Every code: [Error codes](../03-reference/error-codes.md). Next: [What the build produces](../02-guides/what-the-build-produces.md).
