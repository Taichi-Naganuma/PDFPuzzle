using System.Diagnostics;
using System.Windows.Controls;
using PDFPuzzle.Utilities;
using Xunit;

namespace PDFPuzzle.Tests
{
    public class WiringGuardTest
    {
        // TraceListener を一時挿入し、警告キャプチャを行うヘルパ
        private sealed class CapturingListener : TraceListener
        {
            public string Captured { get; private set; } = string.Empty;
            public override void Write(string? message) => Captured += message;
            public override void WriteLine(string? message) => Captured += message + "\n";
        }

        // --- WarnIfWrongSender ---

        [StaFact]
        public void WarnIfWrongSender_MatchingName_DoesNotWarn()
        {
            var listener = new CapturingListener();
            Trace.Listeners.Add(listener);
            try
            {
                var button = new Button { Name = "ActivateButton" };
                WiringGuard.WarnIfWrongSender(button, "ActivateButton");
                Assert.DoesNotContain("mismatch", listener.Captured);
            }
            finally { Trace.Listeners.Remove(listener); }
        }

        [StaFact]
        public void WarnIfWrongSender_MismatchedName_EmitsWarning()
        {
            var listener = new CapturingListener();
            Trace.Listeners.Add(listener);
            try
            {
                var button = new Button { Name = "CancelButton" };
                WiringGuard.WarnIfWrongSender(button, "ActivateButton");
                Assert.Contains("mismatch", listener.Captured);
                Assert.Contains("expected x:Name='ActivateButton'", listener.Captured);
                Assert.Contains("got='CancelButton'", listener.Captured);
            }
            finally { Trace.Listeners.Remove(listener); }
        }

        // --- WarnIfWrongCommandSource ---

        [StaFact]
        public void WarnIfWrongCommandSource_MatchingParameter_DoesNotWarn()
        {
            var listener = new CapturingListener();
            Trace.Listeners.Add(listener);
            try
            {
                WiringGuard.WarnIfWrongCommandSource("SaveButton", "SaveButton");
                Assert.DoesNotContain("mismatch", listener.Captured);
            }
            finally { Trace.Listeners.Remove(listener); }
        }

        [StaFact]
        public void WarnIfWrongCommandSource_MismatchedParameter_EmitsWarning()
        {
            var listener = new CapturingListener();
            Trace.Listeners.Add(listener);
            try
            {
                WiringGuard.WarnIfWrongCommandSource("DeleteButton", "SaveButton");
                Assert.Contains("Command source mismatch", listener.Captured);
            }
            finally { Trace.Listeners.Remove(listener); }
        }
    }
}
