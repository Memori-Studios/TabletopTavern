// Grants the EditMode test assembly access to internals of TabletopTavern.Core.Systems.
//
// ISystem structs in this assembly are declared without an access modifier, which makes them
// internal. That is correct - nothing outside the assembly should be scheduling them by hand - but it
// also means a test cannot tick one directly, and ticking one system in isolation is the whole point
// of the ECS tests: running a whole SystemGroup instead would make every assertion depend on the
// ordering attributes of every other system.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("TabletopTavern.Tests.Editor")]
