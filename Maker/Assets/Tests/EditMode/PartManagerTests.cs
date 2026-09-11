using NUnit.Framework;
using UnityEngine;

namespace EditModeTests
{
    /// <summary>
    /// The bookkeeping over groups and parts: finding them, deleting them, and getting them back
    /// out of a saved twin. Undo and redo are covered separately by <c>PartHistoryTests</c>.
    /// </summary>
    /// <remarks>
    /// No scene and no texture, as in <c>PartHistoryTests</c>: the manager lives on a bare
    /// GameObject and the parts carry no strokes, which is all this bookkeeping looks at.
    /// <c>LoadData</c> works here too - it binds loaded commands to the scene's paintable texture
    /// only when there is one.
    /// </remarks>
    public class PartManagerTests
    {
        private GameObject host;
        private PartManager partManager;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("PartManagerTests");
            partManager = host.AddComponent<PartManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
        }

        /// <summary>A group the way the app creates one; the first call also creates the list.</summary>
        private PartManager.GroupData NewGroup(string name)
        {
            PartManager.GroupData group = partManager.StartNewGroup(null);
            group.id = name;
            group.name = name;
            group.visible = true;
            return group;
        }

        /// <summary>
        /// Clears the current group through the backing field, as <c>ClearAll</c> does. The
        /// <c>currentGroup</c> property cannot take null - its setter marks the new group as
        /// selected without checking - so assigning null there would throw rather than clear.
        /// </summary>
        private void NoGroupIsCurrent()
        {
            typeof(PartManager)
                .GetField("CurrentGroup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(partManager, null);
        }

        private PartManager.PartData NewPart(PartManager.GroupData group, string id, string description = "a finding")
        {
            PartManager.PartData part = partManager.addPart(group, id);
            part.description = description;
            return part;
        }

        // --- finding things ------------------------------------------------------

        [Test]
        public void getPart_findsAPartInAnyGroup()
        {
            PartManager.GroupData left = NewGroup("left");
            PartManager.GroupData right = NewGroup("right");
            NewPart(left, "part-1");
            NewPart(right, "part-2");

            Assert.AreEqual("part-2", partManager.getPart("part-2").id);
            Assert.IsNull(partManager.getPart("part-3"), "An unknown id should find nothing.");
        }

        [Test]
        public void getGroup_findsTheGroupHoldingThePart()
        {
            PartManager.GroupData left = NewGroup("left");
            PartManager.GroupData right = NewGroup("right");
            NewPart(left, "part-1");
            PartManager.PartData second = NewPart(right, "part-2");

            Assert.AreSame(right, partManager.getGroup(second));
            Assert.IsNull(partManager.getGroup(new PartManager.PartData { id = "part-3" }));
        }

        /// <summary>
        /// The two lookups are not interchangeable: <c>getGroup</c> compares ids, while
        /// <c>FindGroupContainingPart</c> compares references. Written down as it stands - the
        /// difference is known and not worth a fix today (TWIN-453) - so that anyone tempted to
        /// merge them sees what would change.
        /// </summary>
        [Test]
        public void getGroup_matchesById_whereFindGroupContainingPart_matchesByReference()
        {
            PartManager.GroupData group = NewGroup("left");
            NewPart(group, "part-1");
            var sameIdDifferentObject = new PartManager.PartData { id = "part-1" };

            Assert.AreSame(group, partManager.getGroup(sameIdDifferentObject),
                "getGroup is expected to match on the id.");
            Assert.IsNull(partManager.FindGroupContainingPart(sameIdDifferentObject),
                "FindGroupContainingPart is expected to match on the reference.");
        }

        // --- descriptions --------------------------------------------------------

        [Test]
        public void AllPartsDescribed_isFalseWhileAPartHasNoDescription()
        {
            PartManager.GroupData group = NewGroup("left");
            NewPart(group, "part-1");
            PartManager.PartData undescribed = NewPart(group, "part-2", description: "");

            Assert.IsFalse(partManager.AllPartsDescribed());

            undescribed.description = "a second finding";

            Assert.IsTrue(partManager.AllPartsDescribed());
        }

        /// <summary>
        /// With nothing painted there is nothing to describe, and the answer is yes. Written down
        /// as it stands by decision (TWIN-453) - the callers in the AI flow read it as "nothing
        /// left to do".
        /// </summary>
        [Test]
        public void AllPartsDescribed_withoutAnyParts_isTrue()
        {
            NewGroup("left");

            Assert.IsTrue(partManager.AllPartsDescribed());
        }

        // --- current group -------------------------------------------------------

        [Test]
        public void trySetCurrentGroupIfEmpty_takesTheFirstGroupOnlyWhenNoneIsCurrent()
        {
            PartManager.GroupData first = NewGroup("first");
            NewGroup("second");

            Assert.IsNull(partManager.trySetCurrentGroupIfEmpty(),
                "A group is already current, so nothing should change.");

            NoGroupIsCurrent();

            Assert.AreSame(first, partManager.trySetCurrentGroupIfEmpty());
            Assert.AreSame(first, partManager.currentGroup);
        }

        // --- deleting ------------------------------------------------------------

        [Test]
        public void deletePart_takesThePartOutOfItsGroup()
        {
            PartManager.GroupData group = NewGroup("left");
            PartManager.PartData first = NewPart(group, "part-1");
            NewPart(group, "part-2");

            partManager.deletePart(first);

            Assert.IsNull(partManager.getPart("part-1"), "The part is still there.");
            Assert.IsNotNull(partManager.getPart("part-2"), "The other part went with it.");
            Assert.AreEqual(1, group.groupParts.Count);
        }

        [Test]
        public void deletePart_ofAPartInNoGroup_leavesTheOthersAlone()
        {
            PartManager.GroupData group = NewGroup("left");
            NewPart(group, "part-1");

            partManager.deletePart(new PartManager.PartData { id = "stray" });

            Assert.IsNotNull(partManager.getPart("part-1"));
            Assert.AreEqual(1, group.groupParts.Count);
        }

        /// <summary>
        /// A group can reach this without a <c>Group</c> object behind it - anything that builds
        /// the data without the scene does. Deleting one must not throw (TWIN-453).
        /// </summary>
        [Test]
        public void deleteGroup_withoutAGroupObject_removesItInsteadOfThrowing()
        {
            PartManager.GroupData group = NewGroup("left");
            NewPart(group, "part-1");

            partManager.deleteGroup(group);

            CollectionAssert.DoesNotContain(partManager.groups, group);
            Assert.IsNull(partManager.getPart("part-1"), "The group's part outlived its group.");
        }

        [Test]
        public void ClearAll_dropsEveryGroupAndTheCurrentOne()
        {
            NewPart(NewGroup("left"), "part-1");

            partManager.ClearAll();

            CollectionAssert.IsEmpty(partManager.groups);
            Assert.IsNull(partManager.currentGroup);
            Assert.IsFalse(partManager.Undo(), "The history survived ClearAll.");
        }

        // --- the round trip ------------------------------------------------------

        /// <summary>
        /// What a twin carries between sessions. The part's back-reference to its group is
        /// deliberately not saved - it would make the json cycle - so the check that it is there
        /// again afterwards is a check on the relinking, not on the serializer.
        /// </summary>
        [Test]
        public void SaveData_thenLoadData_bringsBackGroupsAndTheirParts()
        {
            PartManager.GroupData left = NewGroup("left");
            NewPart(left, "part-1", description: "swelling");
            PartManager.GroupData right = NewGroup("right");
            NewPart(right, "part-2", description: "scar");

            var stored = new ConfigData("Knee", "001");
            partManager.SaveData(stored);
            partManager.ClearAll();
            Assert.IsEmpty(partManager.groups, "Precondition: everything is gone before loading.");

            partManager.LoadData(stored);

            Assert.AreEqual(2, partManager.groups.Count);
            Assert.AreEqual("swelling", partManager.getPart("part-1").description);
            Assert.AreEqual("scar", partManager.getPart("part-2").description);
            Assert.AreSame(partManager.getGroup(partManager.getPart("part-1")),
                partManager.getPart("part-1").group,
                "The part's link back to its group was not restored after loading.");
        }
    }
}
