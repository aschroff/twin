using System.Collections;
using System.Linq;
using CW.Common;
using NUnit.Framework;
using PaintCore;
using PaintIn3D;
using UnityEngine;

namespace NoAPICalls
{
    /// <summary>
    /// Shared flows and helpers for the tests of the tools on the Edit screen (Marker, Filler,
    /// Text, ...): getting to the Edit screen ready to paint, painting a stroke in the way the
    /// app's pipeline sees it, and measuring what it left on the body.
    /// </summary>
    public abstract class EditUiTestBase : TwinPaintTestBase
    {
        /// <summary>Time for a replay of the body to reach the texture (see the painting guide).</summary>
        protected const float SettleSeconds = 0.5f;

        /// <summary>From the main screen to the Edit screen.</summary>
        protected IEnumerator OpenEditScreen()
        {
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
        }

        /// <summary>The LipEdema twin on the Edit screen, framed on the body, its first group the
        /// current one.</summary>
        protected IEnumerator PrepareToPaintInEditMode()
        {
            yield return LoadLipEdemaTwin();
            yield return BackToMain();
            yield return OpenEditScreen();
            yield return SelectView(BodyView);
            yield return SelectGroupForPainting(FindPartManager().groups[0]);
        }

        /// <summary>Moves the camera further away from the body by the given factor, keeping what
        /// is in the centre of the screen.</summary>
        protected static IEnumerator ZoomOut(float factor)
        {
            ViewManager viewManager = Object.FindFirstObjectByType<ViewManager>(FindObjectsInactive.Include);
            Assert.IsNotNull(viewManager, "No ViewManager in the scene.");
            var view = viewManager.shootView();
            view.sizeCamera *= factor;
            viewManager.select(view);
            yield return new WaitForSeconds(SettleSeconds);
        }

        /// <summary>Drives the paint pipeline of the active tool through the given screen
        /// positions - down on the first, up on the last. The whole stroke has to happen within one
        /// frame: between frames the real mouse, which is not pressed, ends the synthetic finger
        /// (see the painting guide).</summary>
        protected static IEnumerator PaintInOneFrame(params Vector2[] screenPoints)
        {
            CwHitScreen[] hitScreens = Object.FindObjectsOfType<CwHitScreen>(false);
            Assert.Greater(hitScreens.Length, 0, "No active CwHitScreen - is a tool selected?");

            foreach (CwHitScreen hitScreen in hitScreens)
            {
                CwPointerMouse pointerMouse = hitScreen.GetComponent<CwPointerMouse>();
                if (pointerMouse == null) continue;
                hitScreen.GuiLayers = 0; // bypass the "started over GUI" guard

                for (int i = 0; i < screenPoints.Length; i++)
                {
                    pointerMouse.GetFinger(1, screenPoints[i], 1.0f, true, out CwInputManager.Finger finger);
                    hitScreen.HandleFingerUpdate(finger, i == 0, i == screenPoints.Length - 1);
                }
                pointerMouse.TryNullFinger(1);
            }

            yield return null;
            yield return null; // CwPaintableManager flushes the commands in LateUpdate
        }

        /// <summary>How many pixels differ between two reads of the body texture, and the average
        /// colour of those pixels in the second read.</summary>
        protected static int ChangedPixels(Color32[] before, Color32[] after, out Color average)
        {
            Assert.AreEqual(before.Length, after.Length, "The body texture changed its size.");
            long r = 0, g = 0, b = 0;
            int changed = 0;
            for (int i = 0; i < before.Length; i++)
            {
                Color32 x = before[i];
                Color32 y = after[i];
                if (x.r == y.r && x.g == y.g && x.b == y.b && x.a == y.a) continue;
                changed++;
                r += y.r;
                g += y.g;
                b += y.b;
            }
            average = changed == 0
                ? Color.clear
                : new Color(r / (255f * changed), g / (255f * changed), b / (255f * changed));
            return changed;
        }

        /// <summary>The colour of the palette that is closest to <paramref name="colour"/>.</summary>
        protected static Color Nearest(Color[] palette, Color colour)
        {
            return palette.OrderBy(candidate =>
                Mathf.Pow(candidate.r - colour.r, 2) +
                Mathf.Pow(candidate.g - colour.g, 2) +
                Mathf.Pow(candidate.b - colour.b, 2)).First();
        }
    }
}
