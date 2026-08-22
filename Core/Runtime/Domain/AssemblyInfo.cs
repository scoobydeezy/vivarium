using System.Runtime.CompilerServices;

// The Domain keeps a few operations internal because only the settlement loop should call them —
// notably Scheduler.EnterExecution/ExitExecution, which arm the §11.4 phase guard. The test assembly
// needs them to prove that guard actually fires, so it gets access rather than the API being widened
// for everyone.
[assembly: InternalsVisibleTo("Vivarium.Domain.Tests")]
