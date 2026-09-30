using System.Collections;
using System.IO;
using CW.Common;
using NUnit.Framework;
using PaintCore;
using PaintIn3D;
using UnityEngine;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    /// <summary>
    /// The sticker images of a twin. Each sticker slot keeps its image as a PNG in the twin
    /// directory, and its paint commands refer to that image by the hash of the slot. The hash
    /// belongs to the slot, so all twins share it and every twin has an image of its own behind
    /// it - which is what makes loading a twin, and importing one, interesting.
    /// </summary>
    [Category(Processes.MarkUpTheBody)]
    public class StickerPlayModeTests : TwinPaintTestBase
    {
        // both short enough for TwinNameValidator, which allows 11 characters
        const string RedStickerTwin = "RedSticker";
        const string BlueStickerTwin = "BlueSticker";

        /// <summary>Two twins with a different image in the same sticker slot: the image behind
        /// the slot and the image its hash points at have to follow the twin that is open.
        /// </summary>
        [UnityTest]
        public IEnumerator StickerImage_FollowsTheTwinThatIsOpen()
        {
            yield return ResetApp();
            Sticker slot = FindStickerSlot();
            string slotId = SlotId(slot);
            int slotHash = SlotHash(slot);

            yield return CreateTwin(RedStickerTwin);
            string redTwin = DataPersistenceManager.instance.selectedProfileId;
            yield return CreateTwin(BlueStickerTwin);
            string blueTwin = DataPersistenceManager.instance.selectedProfileId;

            WriteStickerImage(redTwin, slotId, Color.red);
            WriteStickerImage(blueTwin, slotId, Color.blue);

            yield return OpenTwin(redTwin);
            AssertStickerImage(slot, Color.red, "after opening the twin with the red sticker");
            AssertRegisteredImage(slot, slotHash);

            yield return OpenTwin(blueTwin);
            AssertStickerImage(slot, Color.blue, "after opening the twin with the blue sticker");
            AssertRegisteredImage(slot, slotHash);

            yield return OpenTwin(redTwin);
            AssertStickerImage(slot, Color.red, "after going back to the twin with the red sticker");
            AssertRegisteredImage(slot, slotHash);
        }

        /// <summary>Every sticker image of a twin travels with it, and an imported twin brings its
        /// own images for the slots the open twin is using with different ones.</summary>
        [UnityTest]
        public IEnumerator ImportedTwin_BringsAllItsStickerImages()
        {
            yield return ResetApp();
            Sticker[] slots = FindStickerSlots(2);

            yield return CreateTwin(BlueStickerTwin);
            string exportedTwin = DataPersistenceManager.instance.selectedProfileId;
            WriteStickerImage(exportedTwin, SlotId(slots[0]), Color.blue);
            WriteStickerImage(exportedTwin, SlotId(slots[1]), Color.green);
            string zipFilePath = DataPersistenceManager.instance.ExportConfigZip();
            Assert.IsTrue(File.Exists(zipFilePath), "Setup: the export should have written a zip file.");

            // a twin using the same two slots with other images is open when the import arrives
            yield return CreateTwin(RedStickerTwin);
            string openTwin = DataPersistenceManager.instance.selectedProfileId;
            WriteStickerImage(openTwin, SlotId(slots[0]), Color.red);
            WriteStickerImage(openTwin, SlotId(slots[1]), Color.yellow);
            yield return OpenTwin(openTwin);
            AssertStickerImage(slots[0], Color.red, "Setup: the open twin should show its own stickers");
            AssertStickerImage(slots[1], Color.yellow, "Setup: the open twin should show its own stickers");

            string importedTwin = DataPersistenceManager.instance.ImportConfig(zipFilePath);
            Assert.AreEqual(exportedTwin + "V01", importedTwin, "The import did not produce a new version.");
            foreach (Sticker slot in slots)
            {
                Assert.IsTrue(File.Exists(Path.Combine(DataPaths.PersistentDataPath, importedTwin, SlotId(slot) + ".png")),
                    $"The image of sticker slot {SlotId(slot)} should travel with the twin.");
            }

            yield return OpenTwin(importedTwin);
            AssertStickerImage(slots[0], Color.blue, "after opening the imported twin");
            AssertStickerImage(slots[1], Color.green, "after opening the imported twin");
            AssertRegisteredImage(slots[0], SlotHash(slots[0]));
            AssertRegisteredImage(slots[1], SlotHash(slots[1]));
        }

        static Sticker FindStickerSlot()
        {
            return FindStickerSlots(1)[0];
        }

        /// <summary>Sticker slots of the scene, each with an id of its own and therefore a hash
        /// of its own.</summary>
        static Sticker[] FindStickerSlots(int count)
        {
            Sticker[] slots = Object.FindObjectsOfType<Sticker>(true);
            Assert.GreaterOrEqual(slots.Length, count, $"The scene should offer at least {count} sticker slots.");
            Sticker[] picked = new Sticker[count];
            System.Array.Copy(slots, picked, count);
            for (int i = 1; i < count; i++)
            {
                Assert.AreNotEqual(SlotHash(picked[i - 1]), SlotHash(picked[i]),
                    "Setup: the picked sticker slots should have hashes of their own.");
            }
            return picked;
        }

        static string SlotId(Sticker slot)
        {
            return slot.GetComponent<Item>().getId();
        }

        static int SlotHash(Sticker slot)
        {
            return slot.GetComponent<Item>().getHash();
        }

        /// <summary>Loads a twin the way the twin list does.</summary>
        IEnumerator OpenTwin(string profileId)
        {
            DataPersistenceManager.instance.ChangeSelectedProfileId(profileId);
            yield return null;
        }

        /// <summary>Creates a twin through the save screen and leaves the app on it.</summary>
        IEnumerator CreateTwin(string name)
        {
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");
            SetInputByName("InputField", name);
            yield return ClickButtonByName("New");
            yield return WaitForModeActive("Main");
        }

        /// <summary>Puts a sticker image of one colour into the directory of a twin, the way
        /// picking an image from the gallery does.</summary>
        static void WriteStickerImage(string profileId, string slotId, Color color)
        {
            Texture2D image = new Texture2D(4, 4);
            Color[] pixels = new Color[image.width * image.height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            image.SetPixels(pixels);
            image.Apply();

            string fullPath = Path.Combine(DataPaths.PersistentDataPath, profileId, slotId + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }

        /// <summary>The image the sticker tool paints with.</summary>
        static Texture2D StickerImage(Sticker slot)
        {
            CwDemoButton button = slot.GetComponent<CwDemoButton>();
            Assert.IsNotNull(button, "The sticker slot has no button, so its tool cannot be found.");
            CwPaintDecal decal = button.IsolateTarget.gameObject.GetComponent<CwPaintDecal>();
            Assert.IsNotNull(decal, "The tool of the sticker slot has no decal.");
            return decal.Texture as Texture2D;
        }

        static void AssertStickerImage(Sticker slot, Color expected, string when)
        {
            Texture2D image = StickerImage(slot);
            Assert.IsNotNull(image, $"The sticker slot has no image {when}.");
            Color actual = image.GetPixel(0, 0);
            Assert.AreEqual(expected.r, actual.r, 0.1f, $"Red channel of the sticker image {when}.");
            Assert.AreEqual(expected.g, actual.g, 0.1f, $"Green channel of the sticker image {when}.");
            Assert.AreEqual(expected.b, actual.b, 0.1f, $"Blue channel of the sticker image {when}.");
        }

        /// <summary>The hash of a slot is what its paint commands refer to, so it has to point at
        /// the same image the tool paints with - otherwise replayed stickers show another twin's
        /// image.</summary>
        static void AssertRegisteredImage(Sticker slot, int slotHash)
        {
            Texture registered;
            Assert.IsTrue(CwSerialization.HashToTexture.TryGetValue(new CwHash(slotHash), out registered),
                $"No image is registered for the hash {slotHash} of the sticker slot.");
            Assert.AreSame(StickerImage(slot), registered,
                "The hash of the sticker slot points at another image than its tool paints with.");
        }
    }
}
