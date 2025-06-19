using System;
using System.Diagnostics;
using System.Linq.Expressions;

namespace ChasmTracker;

public static class Assert
{
	[Conditional("DEBUG")]
	public static void IsTrue(Expression<Func<bool>> x, string msg)
	{
		if (!x.Compile().Invoke())
		{
			var caller = new StackTrace(fNeedFileInfo: true).GetFrame(1);

			string message =
$@"Assertion failed: {msg}

File: {caller?.GetFileName() ?? "<unknown>"}
Line: {caller?.GetFileLineNumber() ?? -1}

Expression: {x.Body}

Chasm Tracker will now terminate.";

			OS.ShowMessageBox("Assertion triggered!", message, OSMessageBoxTypes.Error);

			/* XXX should this use Program.Exit ?
			 * I mean, it's not like it's totally necessary to exit everything.
			 * Especially if we're coming from mem.c or something. */
			Environment.Exit(1);
		}
	}
}
