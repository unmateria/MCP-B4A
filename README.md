![mcp-b4a](https://github.com/user-attachments/assets/115aa63b-3a12-4408-8ae1-479290cfca2f)


# MCP Server for B4A

Bridges [Claude Code](https://claude.ai/claude-code) with the [B4A](https://www.b4x.com/b4a.html) (Basic4Android) ecosystem.

Exposes 17 tools for compiling projects, reading/modifying layouts, exploring libraries, and debugging via ADB — all from within Claude Code.

---

## Tools

| Tool | Description |
|------|-------------|
| `b4a_get_config` | Returns current configuration |
| `b4a_set_config` | Updates a configuration value |
| `b4a_build` | Compiles a B4A project via B4ABuilder.exe |
| `b4a_get_build_log` | Returns the last build log |
| `b4a_read_project` | Reads project metadata from a .b4a file |
| `b4a_list_project_files` | Lists source files, layouts, and assets |
| `b4a_project_context` | Single-call project overview (app info + last error) |
| `b4a_read_layout` | Converts .bal/.bil → JSON |
| `b4a_write_layout` | Writes JSON → .bal/.bil |
| `b4a_list_layouts` | Lists all layouts in a project directory |
| `b4a_list_libraries` | Lists available B4A libraries |
| `b4a_get_library_docs` | Returns compact docs for a library |
| `b4a_search_library` | Searches across library documentation |
| `b4a_read_manifest` | Extracts Manifest Editor block |
| `b4a_write_manifest` | Updates Manifest Editor block |
| `b4a_get_logcat` | Returns filtered logcat (B4A tag) |
| `b4a_list_devices` | Lists connected ADB devices |

---

## Prerequisites

- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (or use the self-contained build)
- B4A IDE installed (for compilation)
- Android SDK Platform Tools (for ADB tools)

---

## Installation

### 1. Build

```powershell
cd B4aMcp
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ../publish
```

### 2. Configure Claude Code

Add to `.claude/settings.json` (or `%APPDATA%\Claude\claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "b4a": {
      "command": "C:\\path\\to\\publish\\B4aMcp.exe",
      "args": []
    }
  }
}
```

### 3. First-time setup

Run `b4a_get_config` — if b4aPath is empty, you will see a warning. Configure with:

```
b4a_set_config(key="b4aPath", value="C:\\B4A")
b4a_set_config(key="additionalLibrariesPath", value="C:\\B4A\\SharedLibs")
```

Config is stored at `%APPDATA%\mcp-b4a\config.json`.

---

## Layout Files (.bal / .bil)

B4A stores UI layouts in a proprietary binary format. This server includes a full port of the official [BalConverter](https://github.com/B4X-Community/B4X-BalConverter) to VB.NET.

- `.bal` — standard layout (used in Activity layouts)
- `.bil` — internal layout variant (RECT32/CNULL entries stripped on write)

**Roundtrip safety:** `b4a_write_layout` validates JSON structure and always creates a `.bak` backup before writing.

---

## Security Notes

- `b4a_build` only accepts paths to existing `.b4a` files.
- No tool deletes files — all writes create `.bak` backups.
- ADB tools are read-only (no arbitrary command execution).

---

## Development

```powershell
cd B4aMcp
dotnet build
dotnet run
```

The server communicates via **stdio** (MCP standard). It does not open any network ports.
