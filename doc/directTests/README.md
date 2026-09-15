# Direct diagnostic tests

This folder contains procedures that execute Rhino directly for controlled
compatibility and licensing comparisons.

## Clean-state licensing test

- Procedure: [`clean-state-rhino-licensing-test.md`](clean-state-rhino-licensing-test.md)
- Automation: [`../../tools/run-rhino-clean-state-test.sh`](../../tools/run-rhino-clean-state-test.sh)

The test isolates `HOME` and XDG state without modifying the normal user's Rhino
license cache. Run it first on the Ubuntu VM and then under WSL2 using the same
script revision and syscall selection.
