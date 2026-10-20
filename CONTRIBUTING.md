# Contributing

Small, focused changes are welcome.

1. Fork and branch.
2. Keep the rule from the README: Core, Actions, State, Model and Orders.Fake must not reference the Agents SDK. Only `Northstar.Agent.Host` does.
3. Run the same commands as CI before opening a PR:

   ```powershell
   dotnet build -warnaserror
   dotnet test
   dotnet format --verify-no-changes
   ```

4. Add or update a test for any behavior change. Tests must pass without secrets, network or a model key.
5. Never commit secrets or real tenant/customer data.

By contributing you agree your work is licensed under the MIT license.
