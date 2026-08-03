using System.Collections.Generic;
using NUnit.Framework;

namespace RunRealms2.Tests
{
    public sealed class WorldBlueprintTests
    {
        [Test]
        public void LocalBlueprint_IsDeterministicAndValid()
        {
            var first = WorldBlueprint.Local("REMIX-SEED-001");
            var second = WorldBlueprint.Local("REMIX-SEED-001");
            Assert.That(first.IsValid(), Is.True);
            Assert.That(second.IsValid(), Is.True);
            CollectionAssert.AreEqual(first.realmOrder, second.realmOrder);
            CollectionAssert.AreEqual(first.intensityCurve, second.intensityCurve);
        }

        [Test]
        public void LocalBlueprint_VisitsEveryRealmBeforeRepeating()
        {
            for (var i = 0; i < 200; i++)
            {
                var blueprint = WorldBlueprint.Local("SEED-" + i);
                var values = new HashSet<int>(blueprint.realmOrder);
                Assert.That(values.Count, Is.EqualTo(RuntimeAssets.Palettes.Length));
            }
        }

        [Test]
        public void InvalidBlueprint_IsRejected()
        {
            var blueprint = new WorldBlueprint
            {
                realmOrder = new[] { 0, 0, 1, 2, 3, 4, 5, 6 },
                intensityCurve = new[] { 0.2f, 0.3f, 0.4f, 0.5f }
            };
            Assert.That(blueprint.IsValid(), Is.False);
        }

        [Test]
        public void StableHash_ChangesWhenSeedChanges()
        {
            Assert.That(WorldBlueprint.StableHash("A"), Is.Not.EqualTo(WorldBlueprint.StableHash("B")));
            Assert.That(WorldBlueprint.StableHash("A"), Is.EqualTo(WorldBlueprint.StableHash("A")));
        }
    }
}
