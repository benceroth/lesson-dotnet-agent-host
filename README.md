# lesson-dotnet-agent-host

Stage 3 of the companion code for *Build Your First .NET Agent Without Hiding the Hard Parts*: conversation state can live in SQLite (`State:Provider` = `Sqlite`) and survives a restart; the in-memory store does not.

```powershell
dotnet test --filter StateStoreTests
```

The full README is on `main`.

<sub>[Bence Roth](https://broth.hu/)</sub>
