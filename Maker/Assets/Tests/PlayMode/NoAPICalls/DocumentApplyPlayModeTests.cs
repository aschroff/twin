using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Code;
using Code.AI.PromptGeneration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoAPICalls
{
    /// <summary>
    /// Applying a document mapping to the twin (Assets/Code/Proc/Document/DocumentMappingApplier.cs)
    /// — the only step of Document → Twin that changes anything, so the whole point of these tests
    /// is that it changes exactly what the user ticked and nothing else.
    ///
    /// Driven against the LipEdema twin, which ships four groups and a meaning for every marker
    /// and filler; the tests free one tool so a claim can be tested.
    /// </summary>
    public class DocumentApplyPlayModeTests : TwinPaintTestBase
    {
        /// <summary>A tool that carries a meaning in this twin — it may be painted with, but its
        /// meaning may not be overwritten.</summary>
        private const string ToolInUse = "Red";

        /// <summary>The tool the tests free, so the mapping has something to claim.</summary>
        private const string ToolToClaim = "Yellow";

        private const string ProposedGroupName = "Skin changes";
        private const string ClaimedMeaning = "healed scar";

        // real region keys, from two different template twins — the applier has to resolve which
        // twin a key comes from, which is all an LLM answer ever says about a region
        private const string LegLeft = "thigh_front_left";
        private const string LegRight = "thigh_front_right";
        private const string Chest = "chest_left";

        // ------------------------------------------------------------------ nothing confirmed

        /// <summary>The review screen starts with every item unticked, so an Apply straight away
        /// must be a no-op — this is what makes the whole flow safe up to that button.</summary>
        [UnityTest]
        public IEnumerator Apply_NothingConfirmed_LeavesTheTwinAlone()
        {
            yield return LoadLipEdemaTwin();
            PartManager partManager = FindPartManager();
            SettingsManager settingsManager = FindSettingsManager();

            int groupsBefore = partManager.groups.Count;
            int partsBefore = TotalParts(partManager);
            string reportBefore = ReportRow(settingsManager).promptResult;
            string meaningBefore = FindToolMeaningField(ToolToClaim).text;

            DocumentApplyResult result = DocumentMappingApplier.Apply(
                FullMapping(), DocumentMappingSelection.Nothing(), partManager, settingsManager);
            yield return null;

            Assert.IsFalse(result.ChangedAnything, "Nothing was ticked, so nothing may have changed.");
            Assert.AreEqual(groupsBefore, partManager.groups.Count, "No group may be created.");
            Assert.AreEqual(partsBefore, TotalParts(partManager), "No part may be painted.");
            Assert.AreEqual(reportBefore, ReportRow(settingsManager).promptResult, "The report may not change.");
            Assert.AreEqual(meaningBefore, FindToolMeaningField(ToolToClaim).text, "No tool may be claimed.");
            Assert.IsEmpty(result.problems, "A no-op run has nothing to complain about.");
            StringAssert.Contains("nothing was applied", result.Summary());
        }

        // ------------------------------------------------------------------ everything confirmed

        [UnityTest]
        public IEnumerator Apply_WhatWasConfirmed_ReachesTheTwin()
        {
            yield return LoadLipEdemaTwin();
            PartManager partManager = FindPartManager();
            SettingsManager settingsManager = FindSettingsManager();

            // the twin has to have somewhere to put a finding that names an existing group
            PartManager.GroupData existingGroup = partManager.groups[0];
            PartManager.GroupData groupBefore = partManager.currentGroup;
            int groupsBefore = partManager.groups.Count;
            FreeOneTool(ToolToClaim);

            // an existing report has to survive: the patient text is appended, not written over
            ItemPrompt report = ReportRow(settingsManager);
            report.promptResult = "An earlier report.";

            DocumentMapping mapping = FullMapping(existingGroup.name);
            DocumentApplyResult result = DocumentMappingApplier.Apply(
                mapping, DocumentMappingSelection.Everything(mapping), partManager, settingsManager);
            yield return null;
            yield return null; // let CwPaintableManager flush the replayed commands

            Assert.IsEmpty(result.problems, "Nothing in this mapping should have been refused: "
                + string.Join(" | ", result.problems.ToArray()));

            // 1. the proposed group exists, and only that one was added
            Assert.AreEqual(groupsBefore + 1, partManager.groups.Count, "Exactly one group was proposed.");
            PartManager.GroupData created = FindGroup(partManager, ProposedGroupName);
            Assert.IsNotNull(created, $"The group '{ProposedGroupName}' should have been created.");
            Assert.IsNotEmpty(created.id, "A group without an id cannot be saved.");
            Assert.IsTrue(created.visible, "A created group has to be visible, or its parts are invisible.");
            Assert.AreEqual(new[] { ProposedGroupName }, result.createdGroups.ToArray());

            // 2. the free tool now carries the proposed meaning
            Assert.AreEqual(ClaimedMeaning, FindToolMeaningField(ToolToClaim).text);
            Assert.AreEqual(new[] { ToolToClaim }, result.claimedTools.ToArray());
            ToolInfo claimed = ToolInventory.All().First(t => t.name == ToolToClaim);
            Assert.IsTrue(claimed.inUse, "The claimed tool has to count as in use afterwards.");

            // 3. the findings are on the body: two findings, three parts (one covers both legs)
            Assert.AreEqual(2, result.paintedFindings, "Both findings should have been painted.");
            Assert.AreEqual(3, result.paintedParts, "Two regions for the legs plus one for the chest.");

            // the two-region finding went into the existing group it named
            List<PartManager.PartData> legParts = existingGroup.groupParts
                .Where(p => p.regionKey == LegLeft || p.regionKey == LegRight).ToList();
            Assert.AreEqual(2, legParts.Count, $"Both thigh regions belong to '{existingGroup.name}'.");
            foreach (PartManager.PartData part in legParts)
            {
                Assert.AreEqual("Swelling of both thighs.", part.description,
                    "The part carries what the document says, not the region name.");
                Assert.AreSame(existingGroup, part.group);
            }

            // the finding painted with the just-claimed tool carries its NEW meaning - which only
            // holds because the meaning is claimed before anything is painted with the tool
            PartManager.PartData chestPart = created.groupParts.FirstOrDefault(p => p.regionKey == Chest);
            Assert.IsNotNull(chestPart, $"The chest finding belongs to '{ProposedGroupName}'.");
            Assert.AreEqual(ClaimedMeaning, chestPart.meaning,
                "A part copies the tool's meaning when it is painted, so claiming has to come first.");
            Assert.AreEqual(ToolToClaim, chestPart.nameTool);

            // 4. the patient text was appended to the report, keeping what was there
            Assert.IsTrue(result.patientTextAppended);
            StringAssert.StartsWith("An earlier report.", report.promptResult);
            StringAssert.EndsWith("The Stemmer sign is negative on both sides.", report.promptResult);

            // 5. the user's current group is theirs again
            Assert.AreSame(groupBefore, partManager.currentGroup,
                "Applying may move the current group while it paints, but has to put it back.");

            // 6. all of it is real paint, and all of it survives the save pipeline
            AssertPartsAreUsable(partManager);
            DataPersistenceManager.instance.SaveConfig();
            string saved = DataPersistenceManager.instance
                .GetAllProfilesGameData()[DataPersistenceManager.instance.selectedProfileId].commandDetails;
            StringAssert.Contains(ProposedGroupName, saved);
            StringAssert.Contains(LegLeft, saved);
            StringAssert.Contains(Chest, saved);

            StringAssert.Contains("2 findings on the body (3 parts)", result.Summary());
        }

        // ------------------------------------------------------------------ what it refuses

        /// <summary>The answer is a proposal, not a command: a tool that already means something
        /// keeps its meaning, a tool or region the app does not know is dropped, and one bad item
        /// never costs the good ones.</summary>
        [UnityTest]
        public IEnumerator Apply_RefusesWhatTheTwinDoesNotAllow()
        {
            yield return LoadLipEdemaTwin();
            PartManager partManager = FindPartManager();
            SettingsManager settingsManager = FindSettingsManager();

            string keptMeaning = FindToolMeaningField(ToolInUse).text;
            Assert.IsNotEmpty(keptMeaning, $"'{ToolInUse}' is expected to carry a meaning in this twin.");
            ItemPrompt report = ReportRow(settingsManager);
            report.promptResult = "Untouched.";

            var mapping = new DocumentMapping
            {
                PatientText = "This must not be written - it was not ticked.",
                // a meaning for a tool that already has one
                ToolAssignments = new List<ProposedToolMeaning>
                {
                    new ProposedToolMeaning { ToolName = ToolInUse, Meaning = "something else entirely" }
                },
                NewGroups = new List<ProposedGroup>
                {
                    new ProposedGroup { Name = ProposedGroupName, Reason = "left unticked on purpose" }
                },
                Paintings = new List<ProposedPainting>
                {
                    // a tool this app does not have
                    new ProposedPainting
                    {
                        FindingText = "painted with a tool that does not exist",
                        Group = partManager.groups[0].name, ToolName = "Chartreuse",
                        RegionKeys = new List<string> { Chest }, Description = "should not appear"
                    },
                    // one good region and one the region library does not have; the good one still
                    // has to be painted, and the same key twice may not become two parts
                    new ProposedPainting
                    {
                        FindingText = "one region known, one not, one repeated",
                        Group = ProposedGroupName, ToolName = ToolInUse,
                        RegionKeys = new List<string> { LegLeft, "left_earlobe_inner", LegLeft },
                        Description = "Swelling of the left thigh."
                    }
                }
            };

            // the paintings are ticked, the group and the patient text are not
            var selection = new DocumentMappingSelection();
            selection.SetTool(0, true);
            selection.SetPainting(0, true);
            selection.SetPainting(1, true);

            DocumentApplyResult result = DocumentMappingApplier.Apply(
                mapping, selection, partManager, settingsManager);
            yield return null;
            yield return null;

            // the tool kept its meaning, and the refusal says so
            Assert.AreEqual(keptMeaning, FindToolMeaningField(ToolInUse).text,
                "A tool that already means something keeps its meaning.");
            Assert.IsEmpty(result.claimedTools);
            Assert.IsTrue(result.problems.Any(p => p.Contains(ToolInUse) && p.Contains(keptMeaning)),
                "The refusal has to name the tool and the meaning that was kept: "
                + string.Join(" | ", result.problems.ToArray()));

            // the unknown tool cost only its own finding
            Assert.IsTrue(result.problems.Any(p => p.Contains("Chartreuse")),
                "An unknown tool has to be reported: " + string.Join(" | ", result.problems.ToArray()));
            Assert.IsFalse(partManager.groups.SelectMany(g => g.groupParts)
                    .Any(p => p.description == "should not appear"),
                "A finding with an unknown tool may not be painted.");

            // the unknown region cost only itself - the good region of the same finding is painted
            Assert.IsTrue(result.problems.Any(p => p.Contains("left_earlobe_inner")),
                "An unknown region has to be reported: " + string.Join(" | ", result.problems.ToArray()));
            Assert.AreEqual(1, result.paintedFindings);
            Assert.AreEqual(1, result.paintedParts, "The repeated region may not become a second part.");

            // a ticked finding gets its group even when the group's own row was left unticked -
            // a confirmed finding has to have somewhere to live
            PartManager.GroupData created = FindGroup(partManager, ProposedGroupName);
            Assert.IsNotNull(created, "A confirmed finding creates the group it names.");
            Assert.AreEqual(1, created.groupParts.Count);
            Assert.AreEqual(LegLeft, created.groupParts[0].regionKey);

            // the patient text was not ticked, so the report is as it was
            Assert.IsFalse(result.patientTextAppended);
            Assert.AreEqual("Untouched.", report.promptResult);

            // and what may be remembered as applied is only what actually landed: the refused tool
            // and the finding with the unknown tool must stay offerable, or they would come back
            // locked as if they were on the body and could never be tried again
            Assert.IsFalse(result.written.IsToolConfirmed(0), "A refused tool meaning was not written.");
            Assert.IsFalse(result.written.IsPaintingConfirmed(0), "That finding was never painted.");
            Assert.IsTrue(result.written.IsPaintingConfirmed(1), "This one did reach the body.");
            Assert.IsFalse(result.written.PatientTextConfirmed);
            Assert.IsTrue(result.written.IsGroupConfirmed(0),
                "The group exists now, so it counts as applied even though its own row was unticked.");

            AssertPartsAreUsable(partManager);
        }

        // ------------------------------------------------------------------ the review list

        /// <summary>The review screen is the gate: a row per proposal, all unticked, the region
        /// count on the row that costs the parts, and an Apply button that writes exactly the
        /// ticked rows. Driven through the real screen and the real button.</summary>
        [UnityTest]
        public IEnumerator ReviewList_OffersEveryProposalUntickedAndAppliesOnlyWhatIsTicked()
        {
            yield return LoadLipEdemaTwin();
            PartManager partManager = FindPartManager();
            SettingsManager settingsManager = FindSettingsManager();
            var upload = Object.FindObjectOfType<DocumentUploadProcess>(true);
            Assert.IsNotNull(upload, "DocumentUploadProcess not found in the scene.");
            var review = Object.FindObjectOfType<DocumentReviewManager>(true);
            Assert.IsNotNull(review, "DocumentReviewManager not found in the scene.");

            PartManager.GroupData existingGroup = partManager.groups[0];
            int groupsBefore = partManager.groups.Count;
            int partsBefore = TotalParts(partManager);
            ItemPrompt report = ReportRow(settingsManager);
            report.promptResult = "";

            DocumentMapping mapping = FullMapping(existingGroup.name);
            // a real treatment line looks like this - long text over many regions. It is the case
            // the rows have to survive: before they wrapped, everything past the width was cut off.
            mapping.Paintings.Add(new ProposedPainting
            {
                FindingText = "flat-knit compression garments class 2 for both legs, worn daily, "
                    + "renewed every six months and combined with manual lymphatic drainage twice a week",
                Group = existingGroup.name,
                ToolName = ToolInUse,
                RegionKeys = new List<string>
                {
                    LegLeft, LegRight, "thigh_back_left", "thigh_back_right", "knee_front_left",
                    "knee_front_right", "shin_left", "shin_right", "calf_left", "calf_right",
                    "hip_left", "hip_right", "ankle_left", "ankle_right"
                },
                Description = "Compression garments for both legs.",
                Confidence = 0.55f
            });

            upload.ShowMapping("report.pdf", mapping);
            yield return WaitForModeActive("UploadReview");
            yield return null;

            // one row per proposal plus one heading per block: 2 findings, 1 group, 1 tool, 1 text
            List<DocumentReviewRow> rows = review.Rows();
            Assert.AreEqual(5 + 5, rows.Count, "A row per proposal and a heading per block: "
                + string.Join(" / ", rows.Select(r => r.kind + ":" + r.Text()).ToArray()));
            Assert.AreEqual(3, rows.Count(r => r.kind == DocumentReviewRow.ItemKind.Painting));
            Assert.AreEqual(1, rows.Count(r => r.kind == DocumentReviewRow.ItemKind.Group));
            Assert.AreEqual(1, rows.Count(r => r.kind == DocumentReviewRow.ItemKind.Tool));
            Assert.AreEqual(1, rows.Count(r => r.kind == DocumentReviewRow.ItemKind.PatientText));

            // nothing is ticked, so Apply would do nothing - this is what makes the screen a gate
            Assert.IsFalse(rows.Any(r => r.Confirmed), "Every row has to start unticked.");
            Assert.AreEqual(0, review.Selection().Count);

            // and it has to LOOK unticked. The row prefab comes from the group list, where the
            // tick sat inside the toggled graphic and kept its own alpha - every row looked ticked
            // while Selection() said none was.
            foreach (DocumentReviewRow row in rows.Where(r => r.kind != DocumentReviewRow.ItemKind.Heading))
            {
                AssertTickVisible(row, false);
            }

            // the row of the two-region finding says how many parts ticking it costs
            DocumentReviewRow legRow = rows.First(r => r.kind == DocumentReviewRow.ItemKind.Painting && r.index == 0);
            StringAssert.Contains("2 regions", legRow.Text());
            StringAssert.Contains(ToolInUse, legRow.Text());
            // the group is its own chip, because it is the one thing on the row that can be changed
            Assert.AreEqual(existingGroup.name, legRow.GroupText());

            // a finding whose group does not exist yet says so, right on the row
            DocumentReviewRow scarRow = rows.First(r => r.kind == DocumentReviewRow.ItemKind.Painting && r.index == 1);
            Assert.AreEqual(ProposedGroupName + " (new)", scarRow.GroupText(),
                "A finding whose group the twin does not have has to say so.");
            Assert.IsFalse(legRow.GroupText().Contains("(new)"),
                "A finding in a group the twin already has may not be marked as new.");

            // only a finding has a group to change - a heading, a group or the report text has none
            Assert.IsEmpty(rows.First(r => r.kind == DocumentReviewRow.ItemKind.Heading).GroupText());
            Assert.IsEmpty(rows.First(r => r.kind == DocumentReviewRow.ItemKind.PatientText).GroupText());

            // the long row wraps instead of being cut off, so it is taller than a short one and
            // its text fits inside it - a row that clips again fails here
            DocumentReviewRow longRow = rows.First(r => r.kind == DocumentReviewRow.ItemKind.Painting && r.index == 2);
            StringAssert.Contains("14 regions", longRow.Text());
            StringAssert.Contains("uncertain", longRow.Text());
            float shortHeight = ((RectTransform)legRow.transform).rect.height;
            float longHeight = ((RectTransform)longRow.transform).rect.height;
            Assert.Greater(longHeight, shortHeight,
                "The long row has to grow taller than a short one, or its text is cut off again.");
            Text longLabel = longRow.GetComponentsInChildren<Text>(true)
                .First(t => t.text == longRow.Text());
            Assert.LessOrEqual(longLabel.preferredHeight, longHeight + 1f,
                $"The row is {longHeight} tall but its text needs {longLabel.preferredHeight}.");

            // a heading offers no toggle to get wrong
            DocumentReviewRow heading = rows.First(r => r.kind == DocumentReviewRow.ItemKind.Heading);
            StringAssert.Contains(DocumentMappingText.HeadingPaintings, heading.Text());
            Assert.IsFalse(heading.Confirmed);

            // the layout of this screen is the one thing a test cannot judge - the shot is written
            // out so a human can, the way the prompt test writes out the prompt
            yield return Screenshot("review-screen");

            // tick the two-region finding and the report text, leave the rest
            legRow.Confirmed = true;
            rows.First(r => r.kind == DocumentReviewRow.ItemKind.PatientText).Confirmed = true;
            yield return null;
            yield return null;
            Assert.AreEqual(2, review.Selection().Count);
            AssertTickVisible(legRow, true);
            AssertTickVisible(rows.First(r => r.kind == DocumentReviewRow.ItemKind.Group), false);
            yield return Screenshot("review-screen-ticked");

            // Apply through the button of the prefab, so its wiring is covered too
            yield return ClickButtonByPath("Canvas/UploadReview UI/Apply Button");
            yield return null;
            yield return null;

            DocumentApplyResult result = upload.lastResult;
            Assert.IsNotNull(result, "The Apply button has to reach DocumentUploadProcess.");
            Assert.AreEqual(1, result.paintedFindings, "Only the ticked finding may be painted.");
            Assert.AreEqual(partsBefore + 2, TotalParts(partManager), "Its two regions, and nothing else.");
            Assert.IsTrue(result.patientTextAppended);
            StringAssert.EndsWith("The Stemmer sign is negative on both sides.", report.promptResult);

            // the unticked rows left no trace
            Assert.AreEqual(groupsBefore, partManager.groups.Count,
                "The proposed group was not ticked and no ticked finding needed it.");
            Assert.IsEmpty(result.claimedTools, "The tool row was not ticked.");
            Assert.IsFalse(partManager.groups.SelectMany(g => g.groupParts)
                    .Any(p => p.regionKey == Chest), "The unticked chest finding may not be painted.");

            // applying shows the twin again, so the result is looked at on the body
            yield return WaitForModeActive("Main");

            // and coming back offers what is left, with what was applied ticked, locked and dimmed,
            // because there is no undo
            upload.Handle(DocumentUploadProcess.VariantReview);
            yield return WaitForModeActive("UploadReview");
            yield return null;
            List<DocumentReviewRow> after = review.Rows();
            DocumentReviewRow appliedRow = after.First(r => r.kind == DocumentReviewRow.ItemKind.Painting && r.index == 0);
            Assert.IsTrue(appliedRow.Applied, "The applied finding has to come back marked as applied.");
            Assert.IsTrue(appliedRow.Confirmed, "It is on the twin, so it shows as ticked.");
            Assert.IsFalse(appliedRow.GetComponentInChildren<Toggle>(true).interactable,
                "An applied row may not be tickable - there is no undo.");
            Assert.IsFalse(after.First(r => r.kind == DocumentReviewRow.ItemKind.Painting && r.index == 1).Applied,
                "A finding that was not applied stays open.");
            yield return Screenshot("review-screen-after-apply");

            // nothing was refused here, so everything that was ticked is remembered as applied
            Assert.AreEqual(review.Selection().Count, upload.applied.Count,
                "Only what actually reached the twin may be remembered as applied.");

            // pressing Apply again may not paint the same regions a second time
            int partsAfterFirstApply = TotalParts(partManager);
            yield return ClickButtonByPath("Canvas/UploadReview UI/Apply Button");
            yield return null;
            yield return null;
            Assert.AreEqual(partsAfterFirstApply, TotalParts(partManager),
                "A second Apply may not write anything that is already on the twin.");

            AssertPartsAreUsable(partManager);
        }

        // ------------------------------------------------------- what a finding drags along with it

        /// <summary>A ticked finding needs its group to exist and its tool to mean something, and
        /// neither is visible on the finding's own row. Ticking it ticks those rows too, so the
        /// screen shows everything that would be written instead of the applier deciding quietly.
        /// </summary>
        [UnityTest]
        public IEnumerator TickingAFinding_AlsoTicksTheGroupAndToolItNeeds()
        {
            yield return LoadLipEdemaTwin();
            PartManager partManager = FindPartManager();
            var upload = Object.FindObjectOfType<DocumentUploadProcess>(true);
            var review = Object.FindObjectOfType<DocumentReviewManager>(true);

            FreeOneTool(ToolToClaim);
            DocumentMapping mapping = FullMapping(partManager.groups[0].name);
            upload.ShowMapping("report.pdf", mapping);
            yield return WaitForModeActive("UploadReview");
            yield return null;

            Assert.AreEqual(0, review.Selection().Count, "Everything starts unticked.");

            // the chest finding goes into a group the twin has not got, painted with a freed tool
            DocumentReviewRow scar = review.Rows()
                .First(r => r.kind == DocumentReviewRow.ItemKind.Painting && r.index == 1);
            scar.Confirmed = true;
            yield return null;

            DocumentReviewRow groupRow = review.Rows().First(r => r.kind == DocumentReviewRow.ItemKind.Group);
            DocumentReviewRow toolRow = review.Rows().First(r => r.kind == DocumentReviewRow.ItemKind.Tool);
            Assert.IsTrue(groupRow.Confirmed, "The group it needs has to come with it.");
            Assert.IsTrue(toolRow.Confirmed, "The meaning of the tool it is painted with has to come too.");
            Assert.IsFalse(review.Rows().First(r => r.kind == DocumentReviewRow.ItemKind.PatientText).Confirmed,
                "The report text is nothing the finding needs.");

            // and it cannot be unticked while the finding needs it - unticking the group would
            // change nothing (it is created for the finding anyway) and unticking the tool would
            // quietly paint the part in a colour that means nothing
            Assert.IsTrue(groupRow.Required);
            Assert.IsTrue(toolRow.Required);
            Assert.IsFalse(groupRow.GetComponentInChildren<Toggle>(true).interactable);
            Assert.IsFalse(toolRow.GetComponentInChildren<Toggle>(true).interactable);

            // the thigh finding needs neither: its group exists and its tool already means something
            review.Rows().First(r => r.kind == DocumentReviewRow.ItemKind.Painting && r.index == 0)
                .Confirmed = true;
            yield return null;
            yield return Screenshot("review-screen-dependencies");

            // untick the finding again and the two rows are the user's own to decide once more
            scar.Confirmed = false;
            yield return null;
            groupRow = review.Rows().First(r => r.kind == DocumentReviewRow.ItemKind.Group);
            toolRow = review.Rows().First(r => r.kind == DocumentReviewRow.ItemKind.Tool);
            Assert.IsFalse(groupRow.Required, "Nothing needs the group any more.");
            Assert.IsFalse(toolRow.Required, "Nothing needs the tool meaning any more.");
            Assert.IsTrue(groupRow.GetComponentInChildren<Toggle>(true).interactable);
            Assert.IsTrue(toolRow.GetComponentInChildren<Toggle>(true).interactable);

            // put it back for the Apply below
            scar.Confirmed = true;
            yield return null;

            // applying writes all three, so the painted part carries a meaning worth reading
            yield return ClickButtonByPath("Canvas/UploadReview UI/Apply Button");
            yield return WaitForModeActive("Main");

            DocumentApplyResult result = upload.lastResult;
            Assert.IsEmpty(result.problems, "Nothing should be missing: "
                + string.Join(" | ", result.problems.ToArray()));
            Assert.AreEqual(new[] { ToolToClaim }, result.claimedTools.ToArray());
            Assert.AreEqual(new[] { ProposedGroupName }, result.createdGroups.ToArray());

            PartManager.GroupData created = FindGroup(partManager, ProposedGroupName);
            Assert.IsNotNull(created);
            Assert.AreEqual(ClaimedMeaning, created.groupParts[0].meaning,
                "The part has to carry the claimed meaning, not the colour's name.");
        }

        // ------------------------------------------------------------------ changing the group

        /// <summary>The model's group is a recommendation. A part cannot be moved once it is
        /// painted, so the review screen is the only chance to correct it - the chip on the row
        /// opens a picker offering the twin's groups and the ones the document proposed.</summary>
        [UnityTest]
        public IEnumerator GroupPicker_ChangesWhereAFindingGoes()
        {
            yield return LoadLipEdemaTwin();
            PartManager partManager = FindPartManager();
            var upload = Object.FindObjectOfType<DocumentUploadProcess>(true);
            var review = Object.FindObjectOfType<DocumentReviewManager>(true);
            var picker = Object.FindObjectOfType<GroupPickerManager>(true);
            Assert.IsNotNull(picker, "No GroupPickerManager on the review screen.");

            PartManager.GroupData first = partManager.groups[0];
            PartManager.GroupData other = partManager.groups[1];

            DocumentMapping mapping = FullMapping(first.name);
            upload.ShowMapping("report.pdf", mapping);
            yield return WaitForModeActive("UploadReview");
            yield return null;

            Assert.IsFalse(picker.IsAsking(), "The picker only opens when it is asked for.");

            // something is ticked before the group is touched: changing a group rebuilds every row,
            // and a tick may not die with the row that carried it
            review.Rows().First(r => r.kind == DocumentReviewRow.ItemKind.Painting && r.index == 1)
                .Confirmed = true;
            review.Rows().First(r => r.kind == DocumentReviewRow.ItemKind.PatientText).Confirmed = true;
            yield return null;
            // that finding drags its group along - but not its tool: this test never freed Yellow,
            // so the tool already means something and ticking that row could only be refused
            int tickedBefore = review.Selection().Count;
            Assert.AreEqual(3, tickedBefore,
                "The finding, the report text, and the group the finding needs - not the tool.");
            Assert.IsFalse(review.Rows().First(r => r.kind == DocumentReviewRow.ItemKind.Tool).Confirmed,
                "A tool that already carries a meaning may not be ticked on the user's behalf.");

            // tapping the chip asks, and does NOT tick the row - a Button inside the row's Toggle
            DocumentReviewRow legRow = review.Rows()
                .First(r => r.kind == DocumentReviewRow.ItemKind.Painting && r.index == 0);
            legRow.GroupButton().onClick.Invoke();
            yield return null;

            Assert.IsTrue(picker.IsAsking(), "The chip has to open the picker.");
            Assert.IsFalse(legRow.Confirmed, "Tapping the chip may not tick the row.");

            // every group of the twin is on offer, and so is the one the document proposed
            List<string> candidates = picker.Candidates();
            foreach (PartManager.GroupData group in partManager.groups)
            {
                Assert.Contains(group.name, candidates, "Every group of the twin has to be offered.");
            }
            Assert.Contains(ProposedGroupName + " (new)", candidates,
                "A group the document proposed is on offer, marked as new.");

            // pick another one - only the proposal changes, the twin is untouched
            Assert.IsTrue(picker.Pick(other.name), $"'{other.name}' was not offered.");
            yield return null;
            yield return null;

            Assert.IsFalse(picker.IsAsking(), "Picking closes the question.");
            Assert.AreEqual(other.name, mapping.Paintings[0].Group,
                "The pick has to be written back into the proposal.");
            Assert.AreEqual(0, TotalParts(partManager), "Choosing a group may not paint anything.");

            // the ticks set before the group was changed are still there
            Assert.AreEqual(tickedBefore, review.Selection().Count,
                "Changing a group rebuilds the rows, but may not throw the ticks away.");
            Assert.IsTrue(review.Rows()
                .First(r => r.kind == DocumentReviewRow.ItemKind.Painting && r.index == 1).Confirmed);
            Assert.IsTrue(review.Rows()
                .First(r => r.kind == DocumentReviewRow.ItemKind.PatientText).Confirmed);

            // and the row now shows it
            DocumentReviewRow again = review.Rows()
                .First(r => r.kind == DocumentReviewRow.ItemKind.Painting && r.index == 0);
            Assert.AreEqual(other.name, again.GroupText());
            // the text below the rows is regenerated, so it cannot keep claiming the old group
            StringAssert.Contains("group:   " + other.name, review.GetShownText());
            Assert.IsFalse(review.GetShownText().Contains("group:   " + first.name),
                $"The text still says '{first.name}' after the group was changed.");
            yield return Screenshot("review-screen-group-changed");

            // applying it puts the finding in the group that was picked, not the one proposed
            // untick the rest, so this assertion is about the group that was picked and nothing else
            review.Rows().First(r => r.kind == DocumentReviewRow.ItemKind.Painting && r.index == 1)
                .Confirmed = false;
            review.Rows().First(r => r.kind == DocumentReviewRow.ItemKind.PatientText).Confirmed = false;
            review.Rows().First(r => r.kind == DocumentReviewRow.ItemKind.Group).Confirmed = false;
            review.Rows().First(r => r.kind == DocumentReviewRow.ItemKind.Tool).Confirmed = false;
            again.Confirmed = true;
            yield return null;
            yield return ClickButtonByPath("Canvas/UploadReview UI/Apply Button");
            yield return null;
            yield return null;

            Assert.AreEqual(2, other.groupParts.Count, $"Both thigh regions belong to '{other.name}'.");
            Assert.AreEqual(0, first.groupParts.Count, $"'{first.name}' was the recommendation, not the choice.");
        }

        // ------------------------------------------------------------- the way back to the review

        /// <summary>The mapping outlives the screen, so leaving it - by accident, or with something
        /// still ticked - costs neither a pick nor a second call to the API. And it belongs to the
        /// twin it was read for: applying it to another one would paint onto the wrong body.</summary>
        [UnityTest]
        public IEnumerator Review_ComesBackWithoutAnotherUpload_AndOnlyForItsOwnTwin()
        {
            yield return LoadLipEdemaTwin();
            PartManager partManager = FindPartManager();
            var upload = Object.FindObjectOfType<DocumentUploadProcess>(true);
            var review = Object.FindObjectOfType<DocumentReviewManager>(true);

            DocumentMapping mapping = FullMapping(partManager.groups[0].name);
            upload.ShowMapping("report.pdf", mapping);
            yield return WaitForModeActive("UploadReview");
            int rowsShown = review.Rows().Count;
            Assert.Greater(rowsShown, 0);

            // leave the screen without applying anything
            yield return ClickButtonByPath("Canvas/UploadReview UI/Back Button");
            yield return null;
            Assert.AreEqual(0, TotalParts(partManager), "Leaving the screen may not write anything.");

            // and come back to the same proposal - no pick, no second API call
            upload.Handle(DocumentUploadProcess.VariantReview);
            yield return WaitForModeActive("UploadReview");
            Assert.AreEqual(rowsShown, review.Rows().Count,
                "The proposal has to come back as it was, without uploading the document again.");
            Assert.AreSame(mapping, upload.lastMapping);

            // a mapping read for another twin may not be applied to this one
            upload.mappingProfile = "some other twin";
            upload.ShowReview();
            yield return null;
            DocumentApplyResult refused = upload.ApplyConfirmed(DocumentMappingSelection.Everything(mapping));
            Assert.IsFalse(refused.ChangedAnything,
                "A mapping read for another twin may not reach this one.");
            Assert.AreEqual(0, TotalParts(partManager));
        }

        /// <summary>Whether the row's tick is actually drawn. Unity fades the toggle's graphic, so
        /// the graphic has to BE the tick - a tick that only sits inside it stays visible.</summary>
        private static void AssertTickVisible(DocumentReviewRow row, bool expected)
        {
            Toggle toggle = row.GetComponentInChildren<Toggle>(true);
            Assert.IsNotNull(toggle, "The row has no toggle.");
            Assert.IsNotNull(toggle.graphic, "The row's toggle has no graphic, so nothing shows a tick.");
            float alpha = toggle.graphic.canvasRenderer.GetAlpha();
            Assert.AreEqual(expected ? 1f : 0f, alpha, 0.01f,
                $"'{row.Text()}' is {(row.Confirmed ? "" : "not ")}ticked but its tick alpha is {alpha}.");
        }

        /// <summary>Writes what the screen looks like to
        /// Application.temporaryCachePath/UploadReview/ - for reading by eye, not asserted.</summary>
        private static IEnumerator Screenshot(string name)
        {
            yield return new WaitForEndOfFrame();
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            string directory = Path.Combine(Application.temporaryCachePath, "UploadReview");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name + ".png");
            File.WriteAllBytes(path, shot.EncodeToPNG());
            Object.Destroy(shot);
            Debug.Log("[DocumentApplyPlayModeTests] screen written to " + path);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A mapping in the shape a real answer has: one new group, one free tool taken
        /// into use, a finding over both legs and one on the chest, plus a patient text.</summary>
        private static DocumentMapping FullMapping(string existingGroup = null)
        {
            return new DocumentMapping
            {
                DocumentSummary = "A fictional report, for the tests.",
                PatientText = "The Stemmer sign is negative on both sides.",
                NewGroups = new List<ProposedGroup>
                {
                    new ProposedGroup { Name = ProposedGroupName, Reason = "the scar fits no existing group" }
                },
                ToolAssignments = new List<ProposedToolMeaning>
                {
                    new ProposedToolMeaning { ToolName = ToolToClaim, Meaning = ClaimedMeaning, Reason = "no tool means this" }
                },
                Paintings = new List<ProposedPainting>
                {
                    new ProposedPainting
                    {
                        FindingText = "swelling of both thighs",
                        Group = existingGroup ?? ProposedGroupName,
                        ToolName = ToolInUse,
                        RegionKeys = new List<string> { LegLeft, LegRight },
                        Description = "Swelling of both thighs.",
                        Confidence = 0.9f
                    },
                    new ProposedPainting
                    {
                        FindingText = "healed scar on the left chest",
                        Group = ProposedGroupName,
                        ToolName = ToolToClaim,
                        RegionKeys = new List<string> { Chest },
                        Description = "A healed scar on the left chest.",
                        Confidence = 0.6f
                    }
                }
            };
        }

        private static SettingsManager FindSettingsManager()
        {
            var settingsManager = Object.FindObjectOfType<SettingsManager>(true);
            Assert.IsNotNull(settingsManager, "SettingsManager not found in scene.");
            return settingsManager;
        }

        /// <summary>The report row the patient text goes to - the one whose visible Label reads
        /// "Medical Report", not just any row with that label (three share label and level).</summary>
        private static ItemPrompt ReportRow(SettingsManager settingsManager)
        {
            ItemPrompt row = settingsManager.getPromptObjectByLabelText(
                DocumentMappingApplier.ReportRowLabel, ItemPrompt.PromptLevel.Version);
            Assert.IsNotNull(row, $"No prompt row labelled '{DocumentMappingApplier.ReportRowLabel}'.");
            Assert.AreEqual(DocumentMappingApplier.ReportRowLabel, row.LabelText());
            return row;
        }

        private static PartManager.GroupData FindGroup(PartManager partManager, string name)
        {
            return partManager.groups.FirstOrDefault(g => g.name == name);
        }

        private static int TotalParts(PartManager partManager)
        {
            return partManager.groups.Sum(g => g.groupParts.Count);
        }
    }
}
