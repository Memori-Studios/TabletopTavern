// Grants the EditMode test assembly access to internal members of TabletopTavern.Core.
//
// This exists so tests can drive simulation internals directly instead of reflecting into private
// fields. The existing MageAutoResolveTests harness had to reflect, and says so in its own failure
// message: "FAILED to reflect AutoResolveBattleManager internals - a field or method was renamed."
// A rename should break a test at COMPILE time with the member name in the error, not at run time
// with a null FieldInfo.
//
// Only members deliberately marked `internal` are exposed. Nothing here makes private members visible,
// and nothing here changes what ships: InternalsVisibleTo has no runtime cost and the test assembly
// is Editor-only.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("TabletopTavern.Tests.Editor")]
