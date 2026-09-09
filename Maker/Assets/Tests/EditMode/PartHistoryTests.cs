using NUnit.Framework;
using UnityEngine;

namespace EditModeTests
{
    /// <summary>
    /// Undo and redo of parts, the bookkeeping only: which part goes, where it comes back, what
    /// clears the redo list, and what the history ignores. No scene and no texture - the
    /// PartManager lives on a bare GameObject and the "strokes" are empty commands, which is all
    /// the history looks at. The repaint itself is covered by UndoRedoPlayModeTests.
    /// </summary>
    public class PartHistoryTests
    {
        private GameObject host;
        private PartManager partManager;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("PartHistoryTests");
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

        /// <summary>One paint command arriving from the texture. Appends to the current part, or
        /// starts a new one when the manager says so - exactly as a real stroke does.</summary>
        private void Stroke()
        {
            partManager.addCommand(new PartManager.CommandDataTwin
            {
                id = System.Guid.NewGuid().ToString(),
                data = new PaintCommandSerialization.CommandData()
            });
        }

        /// <summary>Paints a finished part: a stroke, closed off the way a tool change closes it.</summary>
        private PartManager.PartData PaintPart()
        {
            partManager.EnforceNewPart();
            Stroke();
            PartManager.PartData part = partManager.currentPart;
            Assert.IsNotNull(part, "Painting should have created a part.");
            return part;
        }

        [Test]
        public void NothingPainted_NothingToUndoOrRedo()
        {
            NewGroup("A");

            Assert.IsFalse(partManager.CanUndo);
            Assert.IsFalse(partManager.CanRedo);
            Assert.IsFalse(partManager.Undo());
            Assert.IsFalse(partManager.Redo());
        }

        [Test]
        public void Undo_TakesTheLastPartOutOfItsGroup()
        {
            PartManager.GroupData group = NewGroup("A");
            PartManager.PartData first = PaintPart();
            PartManager.PartData second = PaintPart();
            Stroke(); // the second part has two strokes - both go together
            Assert.AreEqual(2, group.groupParts.Count, "Setup: two parts.");
            Assert.AreEqual(2, second.partCommands.Count, "Setup: the second part has two strokes.");

            Assert.IsTrue(partManager.Undo());

            CollectionAssert.AreEqual(new[] { first }, group.groupParts, "Only the last part goes.");
            Assert.IsTrue(partManager.CanUndo, "The first part is still there to undo.");
            Assert.IsTrue(partManager.CanRedo);
        }

        [Test]
        public void Redo_PutsThePartBackWhereItWas()
        {
            PartManager.GroupData group = NewGroup("A");
            PartManager.PartData first = PaintPart();
            PartManager.PartData second = PaintPart();

            Assert.IsTrue(partManager.Undo());
            Assert.IsTrue(partManager.Undo());
            Assert.AreEqual(0, group.groupParts.Count, "Both parts undone.");
            Assert.IsFalse(partManager.CanUndo);

            Assert.IsTrue(partManager.Redo());
            CollectionAssert.AreEqual(new[] { first }, group.groupParts, "Redo returns the parts in reverse order of undoing.");
            Assert.IsTrue(partManager.Redo());
            CollectionAssert.AreEqual(new[] { first, second }, group.groupParts, "Each part returns to its old place.");
            Assert.AreSame(group, second.group, "The part is linked to its group again.");
            Assert.IsFalse(partManager.CanRedo);
            Assert.IsTrue(partManager.CanUndo);
        }

        [Test]
        public void Undo_WorksAcrossGroups_InPaintingOrder()
        {
            PartManager.GroupData groupA = NewGroup("A");
            PartManager.PartData inA = PaintPart();
            PartManager.GroupData groupB = NewGroup("B");
            PartManager.PartData inB = PaintPart();
            Assert.AreSame(groupB, inB.group, "Setup: the second part went into group B.");

            Assert.IsTrue(partManager.Undo());
            CollectionAssert.AreEqual(new[] { inA }, groupA.groupParts, "Group A keeps its part.");
            Assert.AreEqual(0, groupB.groupParts.Count, "The part in group B was painted last, so it goes first.");

            Assert.IsTrue(partManager.Undo());
            Assert.AreEqual(0, groupA.groupParts.Count);

            Assert.IsTrue(partManager.Redo());
            CollectionAssert.AreEqual(new[] { inA }, groupA.groupParts, "Redo returns to the right group.");
        }

        [Test]
        public void NewPaintAfterUndo_EndsWhatCouldBeRedone()
        {
            PartManager.GroupData group = NewGroup("A");
            PartManager.PartData undone = PaintPart();
            Assert.IsTrue(partManager.Undo());
            Assert.IsTrue(partManager.CanRedo);

            PartManager.PartData painted = PaintPart();

            Assert.IsFalse(partManager.CanRedo, "New paint after Undo ends the redo history.");
            Assert.IsFalse(partManager.Redo());
            CollectionAssert.AreEqual(new[] { painted }, group.groupParts);
            Assert.AreNotSame(undone, painted);
        }

        [Test]
        public void StrokeAfterUndo_StartsANewPart_NotTheUndoneOne()
        {
            PartManager.GroupData group = NewGroup("A");
            partManager.EnforceNewPart();
            Stroke();
            PartManager.PartData inProgress = partManager.currentPart;

            // undo while the part is still open - the tool has not changed
            Assert.IsTrue(partManager.Undo());
            Assert.AreEqual(0, group.groupParts.Count);

            Stroke();

            Assert.AreEqual(1, group.groupParts.Count, "The stroke after Undo is a part of its own.");
            Assert.AreNotSame(inProgress, group.groupParts[0], "It must not revive the undone part.");
            Assert.AreEqual(1, inProgress.partCommands.Count, "The undone part must not grow.");
        }

        [Test]
        public void StrokeAfterRedo_StartsANewPart_NotTheRestoredOne()
        {
            PartManager.GroupData group = NewGroup("A");
            PartManager.PartData restored = PaintPart();
            Assert.IsTrue(partManager.Undo());
            Assert.IsTrue(partManager.Redo());

            Stroke();

            Assert.AreEqual(2, group.groupParts.Count, "The stroke after Redo is a part of its own.");
            Assert.AreEqual(1, restored.partCommands.Count, "The restored part must not grow.");
        }

        [Test]
        public void PartsThatArriveWithTheTwin_AreNotUndoable()
        {
            PartManager.GroupData group = NewGroup("A");
            PartManager.PartData loaded = partManager.addPart(group, "loaded");

            Assert.IsFalse(partManager.CanUndo, "A part that came with the twin was saved on purpose.");
            Assert.IsFalse(partManager.Undo());
            CollectionAssert.AreEqual(new[] { loaded }, group.groupParts);
        }

        [Test]
        public void RecordPaintedPart_MakesAPartCreatedAnotherWayUndoable()
        {
            PartManager.GroupData group = NewGroup("A");
            PartManager.PartData template = partManager.addPart(group, "template");
            partManager.RecordPaintedPart(template);

            Assert.IsTrue(partManager.CanUndo);
            Assert.IsTrue(partManager.Undo());
            Assert.AreEqual(0, group.groupParts.Count);
            Assert.IsTrue(partManager.Redo());
            CollectionAssert.AreEqual(new[] { template }, group.groupParts);
        }

        [Test]
        public void PartDeletedThroughTheList_LeavesTheHistory()
        {
            PartManager.GroupData group = NewGroup("A");
            PartManager.PartData first = PaintPart();
            PartManager.PartData second = PaintPart();

            partManager.deletePart(second);

            Assert.IsTrue(partManager.CanUndo, "The first part is still undoable.");
            Assert.IsTrue(partManager.Undo());
            Assert.AreEqual(0, group.groupParts.Count, "Undo skipped the deleted part and took the first.");
            Assert.IsFalse(partManager.CanUndo);
            Assert.IsTrue(partManager.Redo());
            CollectionAssert.AreEqual(new[] { first }, group.groupParts, "Redo does not resurrect the deleted part.");
            Assert.IsFalse(partManager.CanRedo);
        }

        [Test]
        public void Redo_DropsAPartWhoseGroupIsGone()
        {
            NewGroup("A");
            PartManager.GroupData groupB = NewGroup("B");
            PaintPart();
            Assert.IsTrue(partManager.Undo());

            partManager.groups.Remove(groupB);

            Assert.IsFalse(partManager.Redo(), "The part has no group to return to.");
            Assert.IsFalse(partManager.CanRedo);
        }

        [Test]
        public void LoadingATwin_DropsTheHistory()
        {
            NewGroup("A");
            PaintPart();
            Assert.IsTrue(partManager.Undo());
            Assert.IsTrue(partManager.CanRedo);

            partManager.LoadData(new ConfigData("SMPLX-mesh-female") { commandDetails = "" });

            Assert.IsFalse(partManager.CanUndo);
            Assert.IsFalse(partManager.CanRedo);
        }

        [Test]
        public void UndonePart_IsGoneFromTheSavedData()
        {
            NewGroup("A");
            PartManager.PartData part = PaintPart();
            Assert.IsTrue(JsonUtility.ToJson(partManager).Contains(part.id), "Setup: the part is in the save data.");

            Assert.IsTrue(partManager.Undo());
            Assert.IsFalse(JsonUtility.ToJson(partManager).Contains(part.id),
                "An undone part must not come back with the next load.");

            Assert.IsTrue(partManager.Redo());
            Assert.IsTrue(JsonUtility.ToJson(partManager).Contains(part.id), "A redone part is saved again.");
        }
    }
}
