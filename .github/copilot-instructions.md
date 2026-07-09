# OVDB (OV Database)

Repository guidance (project overview, build/test/run commands, architecture, known issues) is maintained in [`CLAUDE.md`](../CLAUDE.md) at the repository root. Read that file first and follow it.

## Azure rules

- @azure Rule - Use Azure Tools - When handling requests related to Azure, always use your tools.
- @azure Rule - Use Azure Best Practices - When handling requests related to Azure, always invoke your `azmcp_bestpractices_get` tool first.
- @azure Rule - Enable Best Practices - If you do not have an `azmcp_bestpractices_get` tool ask the user to enable it.
