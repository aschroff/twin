using System.Collections;
using System.Linq;
using NUnit.Framework;
using PaintCore;
using PaintIn3D;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    /// <summary>
    /// Undo and redo in the editing header: a click takes the last painted part out of its group
    /// and off the body, the next click brings it back - through the real buttons of the prefab.
    /// Also pins the configuration that keeps the body texture from being copied per stroke, which
    /// is what got the app killed on an iPad with little memory.
    /// </summary>
    public class UndoRedoPlayModeTests : TwinPaintTestBase
    {
        /// <summary>Time for Erase + Refresh to reach the texture (the replay is flushed in
        /// LateUpdate, and a replay right after a change can render late - see the painting guide).</summary>
        private const float SettleSeconds = 0.5f;

        [UnityTest]
        public IEnumerator UndoAndRedo_TakeBackAndRestoreTheLastPart()
        {
            yield return LoadLipEdemaTwin();
            PartManager partManager = FindPartManager();
            PartManager.GroupData group = partManager.groups[0];

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return SelectGroupForPainting(group);
            yield return new WaitForSeconds(SettleSeconds);
            Color32[] blank = BodyPixels();
            Assert.IsFalse(partManager.CanUndo, "Nothing painted yet, nothing to undo.");

            yield return PaintWithMarker("Red");
            Assert.AreEqual(1, group.groupParts.Count, "Setup: the stroke should be one part.");
            string partId = group.groupParts[0].id;
            Assert.Greater(DifferingPixels(blank, BodyPixels()), 0, "Setup: the stroke should show on the body.");

            PartHistoryButton undo = FindHistoryButton(PartHistoryButton.HistoryAction.Undo);
            PartHistoryButton redo = FindHistoryButton(PartHistoryButton.HistoryAction.Redo);
            Assert.IsTrue(undo.Available, "After painting, Undo is offered.");
            Assert.IsFalse(redo.Available, "Nothing undone yet, so Redo is not offered.");

            yield return Click(undo);

            Assert.AreEqual(0, group.groupParts.Count, "Undo takes the part out of its group.");
            Assert.IsFalse(JsonUtility.ToJson(partManager).Contains(partId),
                "The undone part must not be saved - it would come back with the next load.");
            Assert.AreEqual(0, DifferingPixels(blank, BodyPixels()), "Undo takes the paint off the body.");
            Assert.IsFalse(undo.Available);
            Assert.IsTrue(redo.Available);

            yield return Click(redo);

            Assert.AreEqual(1, group.groupParts.Count, "Redo puts the part back into its group.");
            Assert.AreEqual(partId, group.groupParts[0].id, "It is the same part.");
            AssertPartsAreUsable(partManager);
            Assert.Greater(DifferingPixels(blank, BodyPixels()), 0, "Redo paints the part again.");
            Assert.IsTrue(JsonUtility.ToJson(partManager).Contains(partId), "The redone part is saved again.");
            Assert.IsTrue(undo.Available);
            Assert.IsFalse(redo.Available);

            // new paint after undo/redo works and ends the redo history
            yield return Click(undo);
            yield return PaintWithMarker("Green");
            Assert.AreEqual(1, group.groupParts.Count, "The new stroke is the only part.");
            Assert.AreNotEqual(partId, group.groupParts[0].id);
            Assert.IsFalse(redo.Available, "New paint ends what could be redone.");
            AssertPartsAreUsable(partManager);
        }

        /// <summary>The body texture is 8192x8192 (256 MiB). PaintIn3D's texture-state undo kept
        /// one copy per stroke, and iOS killed the app on the seventh stroke on an iPad Air. Undo
        /// is done by replaying the recorded parts instead, so no tool may store texture states.</summary>
        [UnityTest]
        public IEnumerator Painting_StoresNoTextureCopies()
        {
            yield return LoadLipEdemaTwin();
            CwPaintableTexture texture = BodyTexture();
            Assert.AreEqual(CwPaintableTexture.UndoRedoType.None, texture.UndoRedo,
                "The body texture must not keep undo copies of itself.");

            CwHitScreenBase[] tools = Object.FindObjectsOfType<CwHitScreenBase>(true);
            Assert.Greater(tools.Length, 0, "Setup: the painting tools should be in the scene.");
            foreach (CwHitScreenBase tool in tools)
            {
                Assert.IsFalse(tool.StoreStates, $"Tool '{tool.name}' must not ask for texture states.");
            }
            Assert.AreEqual(0, Object.FindObjectsOfType<CwButtonUndoAll>(true).Length,
                "No button may still drive the texture-state undo.");
            Assert.AreEqual(0, Object.FindObjectsOfType<CwButtonRedoAll>(true).Length,
                "No button may still drive the texture-state redo.");

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return SelectGroupForPainting(FindPartManager().groups[0]);
            yield return PaintWithMarker("Red");
            yield return PaintWithMarker("Green");

            Assert.IsFalse(texture.CanUndo, "Painting must not leave texture states behind.");
            Assert.IsFalse(CwStateManager.CanUndo);
            Assert.IsTrue(FindPartManager().CanUndo, "The parts themselves are undoable.");
        }

        /// <summary>The one active button of the header for the given action.</summary>
        private static PartHistoryButton FindHistoryButton(PartHistoryButton.HistoryAction action)
        {
            PartHistoryButton[] buttons = Object.FindObjectsOfType<PartHistoryButton>(false)
                .Where(button => button.isActiveAndEnabled && button.Action == action)
                .ToArray();
            Assert.AreEqual(1, buttons.Length,
                $"Expected exactly one active {action} button, found: " +
                string.Join(", ", buttons.Select(button => GetTransformPath(button.transform))));
            return buttons[0];
        }

        /// <summary>Clicks the button the way the EventSystem does, then lets the repaint settle.</summary>
        private static IEnumerator Click(PartHistoryButton button)
        {
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            yield return null;
            yield return new WaitForSeconds(SettleSeconds);
        }

        private static CwPaintableTexture BodyTexture()
        {
            Body body = Object.FindObjectOfType<Body>();
            Assert.IsNotNull(body, "No Body found in the scene.");
            CwPaintableTexture texture = body.GetComponent<CwPaintableTexture>();
            Assert.IsNotNull(texture, "The Body has no paintable texture.");
            return texture;
        }

        private static Color32[] BodyPixels()
        {
            Texture2D copy = BodyTexture().GetReadableCopy();
            Assert.IsNotNull(copy, "Could not read the body texture.");
            Color32[] pixels = copy.GetPixels32();
            Object.DestroyImmediate(copy);
            return pixels;
        }

        private static int DifferingPixels(Color32[] before, Color32[] after)
        {
            Assert.AreEqual(before.Length, after.Length, "The body texture changed its size.");
            int differing = 0;
            for (int i = 0; i < before.Length; i++)
            {
                Color32 a = before[i];
                Color32 b = after[i];
                if (a.r != b.r || a.g != b.g || a.b != b.b || a.a != b.a)
                {
                    differing++;
                }
            }
            return differing;
        }
    }
}
