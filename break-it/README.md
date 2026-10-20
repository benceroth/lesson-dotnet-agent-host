# Now break it

Four deliberate failures, each aimed at a boundary from the article. The runner feeds the real `SupportResponder` bad input and checks the host degrades the way the folder's README says.

```powershell
dotnet run --project break-it            # all four
dotnet run --project break-it -- 03-state-lost-on-restart
```

The exit code is 0 only if every selected failure is handled as documented.

| Folder | Boundary | What goes wrong |
|---|---|---|
| [01-malformed-model-output](01-malformed-model-output/README.md) | model → host | The model returns something that is not a valid request |
| [02-out-of-range-argument](02-out-of-range-argument/README.md) | host → tool | The model asks for an order id that cannot be valid |
| [03-state-lost-on-restart](03-state-lost-on-restart/README.md) | host → state | The process restarts between two turns |
| [04-model-hangs](04-model-hangs/README.md) | host → model | The model never answers |

To see a failure for real, remove the fix named in the README (for example the `OrderIdFormat` check) and re-run: the scenario turns red.
