# NPM Publishing Rules for revit-mcp-server

## Package Overview
The MCP server component of BIM-Bot is published as `revit-mcp-server` on the npm registry.

---

## Publishing Workflow

### 1. Pre-Publish Checklist
- Ensure `revit-mcp-server/package.json` version matches target release version (e.g. `2.3.0`).
- Verify TypeScript builds without errors: `npm run build`.
- Verify `build/index.js` exists and includes all 187 tool schemas.

---

### 2. Testing NPX Execution
- Test package packaging locally: `npm pack`.
- Test local execution: `npx ./revit-mcp-server-2.3.0.tgz`.

---

### 3. Publishing to npm Registry
```bash
cd revit-mcp-server
npm publish --access public
```

---

### 4. Post-Publish Verification
- Check npm package page: `https://www.npmjs.com/package/revit-mcp-server`.
- Verify Shields.io badge updates cleanly: `https://img.shields.io/npm/v/revit-mcp-server`.
