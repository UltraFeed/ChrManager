using ChrManager.Application.Models;

namespace ChrManager.Application;

internal enum PassOutcome { Ready, Handover, Failed }

internal sealed record PassExecution (PassSummary Summary, PassOutcome Outcome)
{
	public bool Handover => Outcome == PassOutcome.Handover;
	public bool Failed => Outcome == PassOutcome.Failed;
}

internal sealed record UpdateResume (UpdateJob? Job);

internal enum SelfUpdateOutcome { None, Handover, Failed }
