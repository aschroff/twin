using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoAPICalls
{
    /// <summary>
    /// The logo that says the app is asking the model something.
    /// </summary>
    /// <remarks>
    /// <para>An answer takes anything from five to thirty seconds, and until now the app said
    /// nothing at all in that time - the same complaint the loading panel fixed for twin switches.
    /// This is that panel with the logo in it instead of a line of text, in the same place.</para>
    ///
    /// <para>Two things are easy to get wrong and are pinned here: the count has to survive a run
    /// that asks one question after another without blinking between them, and a request that
    /// fails has to take its count with it - a leaked count leaves the logo on screen for the rest
    /// of the session.</para>
    /// </remarks>
    [Category(Processes.AppFrame)]
    public class ThinkingOverlayPlayModeTests : PlayModeTestBase
    {
        [UnityTearDown]
        public override IEnumerator TearDown()
        {
            // never leave the counter standing for the next test
            while (BusyOverlay.Thinking) BusyOverlay.EndThinking();
            yield return base.TearDown();
        }

        [UnityTest]
        public IEnumerator Thinking_ShowsTheLogoWhereTheLoadingMessageGoes()
        {
            Assert.IsFalse(BusyOverlay.Thinking, "Nothing should be pending before the test asks.");

            BusyOverlay.BeginThinking();
            yield return null;

            Assert.IsTrue(BusyOverlay.Thinking, "The app should count itself as asking.");
            Assert.IsTrue(BusyOverlay.Visible, "The logo should be on screen.");

            GameObject overlay = GameObject.Find(BusyOverlay.PrefabName);
            Assert.IsNotNull(overlay, "The overlay was never put on the canvas.");

            Transform logo = overlay.transform.Find("Logo");
            Transform box = overlay.transform.Find("Box");
            Assert.IsNotNull(logo, "The overlay carries no Logo.");
            Assert.IsNotNull(box, "The overlay carries no Box.");

            Assert.IsTrue(logo.gameObject.activeSelf, "The logo should be showing.");
            Assert.IsFalse(box.gameObject.activeSelf,
                "The message box and the logo share the place in the middle - only one at a time.");

            var logoRect = (RectTransform)logo;
            var boxRect = (RectTransform)box;
            Assert.AreEqual(boxRect.anchoredPosition, logoRect.anchoredPosition,
                "The logo has to appear where the loading message appears.");

            var raw = logo.GetComponent<RawImage>();
            Assert.IsNotNull(raw, "The logo is not a RawImage.");
            Assert.IsNotNull(raw.texture, "The logo has no texture - nothing would be seen.");

            // the same picture the top bar shows
            RawImage inTheBar = FindGameObjectByPath(
                "Canvas/Main UI/Top/GameObject/Selectors/GameObject").GetComponent<RawImage>();
            Assert.AreSame(inTheBar.texture, raw.texture,
                "The overlay should show the app's own logo, not some other picture.");

            Assert.IsFalse(BusyOverlay.BlocksInput,
                "An answer can take half a minute; the twin has to stay usable while it is fetched.");

            BusyOverlay.EndThinking();
            Assert.IsFalse(BusyOverlay.Thinking, "The logo should go when nothing is pending.");
        }

        /// <summary>
        /// With drawings assigned, the logo moves - and it turns around rather than jumping back.
        /// </summary>
        /// <remarks>The drawings themselves are not in the repository yet, so this makes its own:
        /// what is being checked is the playing, not the art. Four frames give the six step cycle
        /// 1 2 3 4 3 2, which is what the field's documentation promises.</remarks>
        [UnityTest]
        public IEnumerator Thinking_PlaysTheFramesThereAndBackAgain()
        {
            BusyOverlay.BeginThinking();
            yield return null;

            GameObject overlay = GameObject.Find(BusyOverlay.PrefabName);
            var busy = overlay.GetComponent<BusyOverlay>();
            var raw = overlay.transform.Find("Logo").GetComponent<RawImage>();

            var frames = new Texture2D[4];
            for (int i = 0; i < frames.Length; i++) frames[i] = new Texture2D(2, 2);

            SetPrivate(busy, "logoFrames", frames);
            SetPrivate(busy, "logoFrameSeconds", 0.02f);

            // restart so the wag picks the frames up
            BusyOverlay.EndThinking();
            BusyOverlay.BeginThinking();

            var seen = new System.Collections.Generic.List<int>();
            float waited = 0f;
            while (waited < 3f && seen.Count < 10)
            {
                int index = System.Array.IndexOf(frames, raw.texture);
                if (index >= 0 && (seen.Count == 0 || seen[seen.Count - 1] != index)) seen.Add(index);
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            BusyOverlay.EndThinking();

            Assert.Greater(seen.Count, 4, "The logo never changed - it is standing still.");
            foreach (int index in seen)
            {
                Assert.GreaterOrEqual(index, 0);
                Assert.Less(index, frames.Length);
            }

            // every step moves by exactly one drawing, in either direction: that is what makes it
            // a wag instead of a flick back to the beginning
            for (int i = 1; i < seen.Count; i++)
            {
                Assert.AreEqual(1, Mathf.Abs(seen[i] - seen[i - 1]),
                    "The drawings jumped from " + seen[i - 1] + " to " + seen[i]
                    + " instead of moving one step - the sequence should turn around at the ends.");
            }

            Assert.IsTrue(seen.Contains(0) && seen.Contains(frames.Length - 1),
                "Both ends of the wag should be reached.");

            foreach (Texture2D frame in frames) UnityEngine.Object.DestroyImmediate(frame);
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var info = target.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(info, "BusyOverlay has no field '" + field + "'.");
            info.SetValue(target, value);
        }

        /// <summary>A run over many parts asks one question after another - the logo stays up.</summary>
        [UnityTest]
        public IEnumerator Thinking_IsCounted_SoASeriesOfQuestionsDoesNotBlink()
        {
            BusyOverlay.BeginThinking();
            BusyOverlay.BeginThinking();
            yield return null;
            Assert.IsTrue(BusyOverlay.Thinking);

            BusyOverlay.EndThinking();
            Assert.IsTrue(BusyOverlay.Thinking,
                "One of two questions came back - the logo has to stay up for the other.");

            BusyOverlay.EndThinking();
            Assert.IsFalse(BusyOverlay.Thinking, "Both are back; the logo may go.");
        }

        /// <summary>
        /// A request that fails must not leave the logo behind.
        /// </summary>
        /// <remarks>Driven through a service that never got a client, so the request fails at once
        /// and no token is spent. That is the same path a real failure takes - the exception is
        /// caught by the task and handed to the error callback - which is what makes this a fair
        /// test of the <c>finally</c>.</remarks>
        [UnityTest]
        public IEnumerator Thinking_EndsEvenWhenTheRequestFails()
        {
            // the failure is the point of the test, so its log line is expected rather than a fault
            LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex("AI request failed.*"));

            var host = new GameObject("AI probe");
            try
            {
                var probe = host.AddComponent<FailingService>();
                string error = null;
                bool wasThinking = false;

                IEnumerator ask = probe.Ask(e => error = e, () => wasThinking = BusyOverlay.Thinking);
                while (ask.MoveNext()) yield return ask.Current;

                Assert.IsTrue(wasThinking, "The logo should have been up while the request was out.");
                Assert.IsNotNull(error, "The request was supposed to fail.");
                Assert.IsFalse(BusyOverlay.Thinking,
                    "A failed request left its count behind - the logo would never go away again.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        /// <summary>An AIService that was never started, so it has no client and every request
        /// fails immediately - no network, no key, no tokens.</summary>
        private class FailingService : Code.AI.AIService
        {
            public IEnumerator Ask(Action<string> onError, Action whileOut)
            {
                IEnumerator request = RequestStructuredCoroutine<Code.DocumentMapping>(
                    "does not matter", _ => { }, onError);

                bool first = true;
                while (request.MoveNext())
                {
                    if (first) { whileOut(); first = false; }
                    yield return request.Current;
                }
            }
        }
    }
}
