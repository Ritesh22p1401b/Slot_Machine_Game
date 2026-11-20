using System.Runtime.CompilerServices;

// Allows the test and editor assemblies to inject deterministic random providers and fast timings.
[assembly: InternalsVisibleTo("SlotMachine.Tests.EditMode")]
[assembly: InternalsVisibleTo("SlotMachine.Tests.PlayMode")]
[assembly: InternalsVisibleTo("SlotMachine.Editor")]
