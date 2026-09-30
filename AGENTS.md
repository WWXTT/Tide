## CodeGraph

In repositories indexed by CodeGraph (a `.codegraph/` directory exists at the repo root), reach for it BEFORE grep/find or reading files when you need to understand or locate code.
If there is no `.codegraph/` directory, skip CodeGraph entirely.

- **MCP tool** (when available): `codegraph_explore` answers most code questions in one call — the relevant symbols' verbatim source plus the call paths between them, including dynamic-dispatch hops grep can't follow. Name a file or symbol in the query to read its current line-numbered source. If it's listed but deferred, load it by name via tool search.
- **Shell** (always works): `codegraph explore "<symbol names or question>"` prints the same output.



## UnityMCP

When the UnityMCP server is connected (`mcp__UnityMCP__*` tools present), drive the Unity Editor with it instead of asking the user to do things by hand. Read state first — resources (`mcpforunity://editor/state`, `mcpforunity://project/info`, `mcpforunity://instances`, ...) and `read_console` — before mutating.

Core tools (18 essentials, always on):

- **Editor**: `read_console` · `manage_editor` (play/pause/stop, undo/redo, tags/layers) · `execute_menu_item` · `refresh_unity` (asset refresh + compile) · `batch_execute` (batch commands, prefer for repetitive multi-object ops)
- **Assets/Project**: `manage_asset` · `manage_material` · `manage_prefabs` · `manage_packages` · `manage_build` · `manage_physics` · `manage_graphics` · `find_in_file`

- **Helper code — prefer `execute_code`** (scripting_ext group): one-off editor utilities (inspect state, batch-fix, compute in-editor) go through `execute_code` — runs C# in the editor, creates no file. Only use `create_script` when the code must persist in the project.
- **No Python/external-script layer over Unity**: never operate Unity through Python or any wrapper script — no Python that parses or rewrites `.unity`/`.prefab`/`.asset` YAML, batch-edits assets, or calls the MCP endpoint itself. All Unity-side logic runs as C# in the editor via `execute_code` (or a persisted editor script when it must be reused); file-level work goes through the UnityMCP asset tools (`manage_asset`, `manage_prefabs`, ...). If a task cannot be expressed through UnityMCP, report that instead of falling back to Python.
- **If UnityMCP tools are missing at session start**, the bridge is down: check with `claude mcp list`, ask the user to start it (Unity: Window > MCP for Unity), Do not retry blindly.



## Debugging & Self-Correction Guardrails

Self-check is allowed only within an observable, falsifiable, reversible loop. Every iteration must produce new external evidence (log, stack trace, test result, console output, screenshot, request/response, runtime state). If there is no new evidence, do not keep editing code.

Stop and request human intervention when any of these occur:

- Two consecutive fix attempts produce no new evidence.
- You start trying to prove why your implementation “should be correct” instead of testing what is actually happening.
- Edits expand in scope without a verified hypothesis.
- You cannot state a falsifiable hypothesis and a minimal verification experiment.
- You resort to commenting out tests, swallowing exceptions, adding sleeps, hardcoding, or bypassing validation.
- The same error recurs, or fixing one thing breaks another.
- The issue may involve environment, deployment, data, concurrency, permissions, third-party services, or unclear acceptance criteria.

When blocked, do NOT keep patching. Freeze the current state (commit/stash). Then output only:

1. Observed facts: logs, errors, test results — no speculation.
2. Recommended human intervention point, if any.
