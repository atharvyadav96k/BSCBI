# Installation process

## installation step
- Create folder in c drive "codebaseindexer"

## verify 
- open cmd in the same folder and run the cmd `codeindex.exe`
- if op will appear. 
- congratulation you indexer got installed

## how to use
#### we can integrate this tool with any ai tool
1. create `.md` file to give instruction to the ai 
2. Past the following instruction for better result

| Command | Argument | What it does |
|---|---|---|
| `search` | name/fragment `[--all]` | Ranked name search. Prints `id  [language]  kind  name` per hit — nothing more. Hides `Import`/`Field` hits by default since they're usually reference-site noise, not the declaration you want; pass `--all` to include them. |
| `info` | nodeId | Prints kind, qualified name, file:line, signature, and doc comment for one node. |
| `get-code` | nodeId | Prints the full source body of one node. |
| `children` | nodeId | Lists the direct members (methods/fields/nested types) declared inside this node — a cheap alternative to `get-code` when you just want a class's shape, not its full body. Prints `id  kind  name  (line N)` — no path or qualified-name repeated per row, since every child is always in the same file as its container. |
| `tree` | `[path]` `[--depth N]` `[--full]` | Directory tree of indexed files, optionally scoped to a subfolder. Capped at depth 3 by default — deeper folders print `... N more entries not shown` inline rather than dumping everything (large repos with vendored/third-party trees can otherwise produce thousands of irrelevant lines before anything relevant shows up). Use `--depth N` to raise the cap or `--full` for the old unlimited behavior. |
| `outline` | — | Namespace/class/method outline (organized by code scope, not folders). |
| `locate` | fragment | Find files by name or path fragment. |
| `refs` | nodeId | Every node that references this one, via **any** relationship. |
| `callers` | nodeId | Methods that call this method. Only valid on a `Method` node — calling it on any other kind prints an explanatory error instead of a silent empty result. |
| `callees` | nodeId | Methods this method calls. Same `Method`-only restriction as `callers`. |
| `subtypes` | nodeId | Types that `extends`/`implements` this one. Only valid on a `Class`/`Interface`/`Struct` node. |
| `usages` | nodeId | Parameters/fields/properties typed as this one — e.g. constructor dependency injection. Only valid on a `Class`/`Interface`/`Struct`/`Enum` node. |

`refs`/`callers`/`callees`/`subtypes`/`usages` results can legitimately span multiple files (unlike `children`, which is always single-file), so the path isn't dropped — but it's printed once as a group header per file rather than repeated on every row:

## Recommended workflow for answering a question about the code

0. For orientation on an unfamiliar/large project, **`tree`** (default depth-3) or **`tree <subfolder>`** to scope to the area you care about — don't reach for `--full` unless you actually need every leaf file, since large repos can bury the relevant folders under thousands of vendored/third-party files otherwise.
1. **`search <term>`** to find candidate symbols. Use the `[language]` and `kind` columns to disambiguate same-named hits across files/languages (e.g. a TS interface vs. a C# class both named `Order`). Add `--all` if you're specifically hunting for imports or field declarations, since those are hidden by default.
2. **`info <nodeId>`** on the most likely candidate to confirm it's the right one (check the file path and signature) before spending a full `get-code` call on it.
3. **`children <nodeId>`** when you just need a class/namespace's shape (its member list) rather than the full implementation — much cheaper than `get-code`.
4. **`get-code <nodeId>`** to read the actual implementation.
5. For "who else touches this" questions, use **`refs`** (broadest), or the narrower **`callers`** / **`callees`** / **`subtypes`** / **`usages`** depending on exactly what relationship you're asking about — but first check the node's `kind` (from `search`/`info`) matches what the command expects, or it'll just tell you so and do nothing.


## Example session

```powershell
& "c:\codebaseindexer\codeindex.exe" index "C:\MyProject"
cd "C:\MyProject"
& "c:\codebaseindexer\codeindex.exe" search AuthService
& "c:\codebaseindexer\codeindex.exe" info <id-from-above>
& "c:\codebaseindexer\codeindex.exe" children <id-from-above>
& "c:\codebaseindexer\codeindex.exe" usages <id-from-above>
& "c:\codebaseindexer\codeindex.exe" get-code <id-from-above>
```