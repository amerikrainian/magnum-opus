using System;
using System.Collections.Generic;
using MagnumOpus.Core;
using MagnumOpus.Core.Graph;
using Xunit;

namespace MagnumOpus.Tests
{
    // A label delegate that throws reads as silent and is logged once, however often the node is
    // rebuilt and read: it must never escape into the module's tick (2026-11-04: a null check through
    // a game type's == operator threw on every navigator rebuild and took the tick down with it).
    public class ThrowingLabelTests
    {
        [Fact]
        public void AThrowingPartIsSilentAndLoggedOnce()
        {
            var errors = new List<string>();
            var prev = CoreLog.Error;
            CoreLog.Error = m => errors.Add(m);
            try
            {
                // One message per test run (the logged-once memory is static), the same on every call.
                string message = "label broke " + Guid.NewGuid();
                string Broken() => throw new InvalidOperationException(message);
                Func<string> broken = Broken;
                for (int i = 0; i < 3; i++)
                {
                    var b = new GraphBuilder();
                    b.AddItem(ControlId.Structural("cell"), new NodeVtable
                    {
                        Announcements = new[]
                        {
                            new NodeAnnouncement(broken, kind: AnnouncementKinds.Label),
                            new NodeAnnouncement(() => "value", kind: AnnouncementKinds.Value),
                        },
                    });
                    var node = b.Build().Nodes[ControlId.Structural("cell")];
                    Assert.Equal("value", GraphAnnouncer.LeafText(node));
                    Assert.Null(GraphAnnouncer.FirstPartText(node));
                }
                Assert.Single(errors);
            }
            finally { CoreLog.Error = prev; }
        }
    }
}
