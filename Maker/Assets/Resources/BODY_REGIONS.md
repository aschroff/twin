# Body Region Catalog (Text → Part template library)

> **Status:** Revision 3 (2026-07-31). Naming convention approved; individual fingers, full
> front/back coverage of the major joints, big toe, jaw joint (rev 2).
> **Generated & app-verified so far:** torso_front (13) + torso_back (8) — all 21 torso regions
> reviewed in the app via per-group toggling (2026-07-31). Remaining batches: arms front/back,
> legs front/back, head/neck, hands, extended.
> **Tool rule learned during generation:** flat/gently curved regions use the fill tool;
> strongly curved regions (groin, genital, buttock — later also fingers/face) use thick marker
> strokes, because the fill's grid reuses the outline's cached raycast depth and misses
> receding surfaces. Marker brush `Radius` renders at ~1:10 (0.1 → ~1 cm line core).
>
> **Conventions**
> - Keys are `snake_case` English, used as template part names (`PartData.description`).
> - `left`/`right` is always the **patient's** left/right (matches SMPL-X bone naming and medical convention).
> - `front`/`back` refers to the camera view the template is painted from (front view = camera-facing surface at yaw 0, back view = yaw 180).
> - **Core** regions cover typical complaint phrasings; **Extended** adds finer face/hand/foot detail.
> - Circumferential descriptions ("bandage around the elbow") are handled by the LLM selecting multiple regions (e.g. `elbow_front_left` + `elbow_back_left`).

## Head & Neck

| # | Key | English | German | View | Tier |
|---|-----|---------|--------|------|------|
| 1 | `head_top` | Top of head | Scheitel / Kopfoberseite | front | Core |
| 2 | `forehead` | Forehead | Stirn | front | Core |
| 3 | `temple_left` | Left temple | Schläfe links | front | Core |
| 4 | `temple_right` | Right temple | Schläfe rechts | front | Core |
| 5 | `eye_left` | Left eye region | Augenregion links | front | Extended |
| 6 | `eye_right` | Right eye region | Augenregion rechts | front | Extended |
| 7 | `nose` | Nose | Nase | front | Extended |
| 8 | `cheek_left` | Left cheek | Wange links | front | Extended |
| 9 | `cheek_right` | Right cheek | Wange rechts | front | Extended |
| 10 | `mouth_chin` | Mouth / chin | Mund / Kinn | front | Extended |
| 11 | `jaw_joint_left` | Left jaw joint (TMJ) | Kiefergelenk links | front* | Extended |
| 12 | `jaw_joint_right` | Right jaw joint (TMJ) | Kiefergelenk rechts | front* | Extended |
| 13 | `ear_left` | Left ear | Ohr links | side* | Extended |
| 14 | `ear_right` | Right ear | Ohr rechts | side* | Extended |
| 15 | `head_back` | Back of head | Hinterkopf | back | Core |
| 16 | `neck_front` | Front of neck / throat | Hals vorne / Kehle | front | Core |
| 17 | `neck_back` | Nape of neck | Nacken | back | Core |

\* ears and jaw joints may need a slight side rotation (yaw ±60°) to hit properly — verify during generation.

## Torso — Front

| # | Key | English | German | View | Tier |
|---|-----|---------|--------|------|------|
| 18 | `chest_left` | Left chest | Brust links | front | Core |
| 19 | `chest_right` | Right chest | Brust rechts | front | Core |
| 20 | `sternum` | Breastbone | Brustbein | front | Core |
| 21 | `abdomen_upper_left` | Upper left abdomen | Oberbauch links | front | Core |
| 22 | `abdomen_upper_central` | Upper central abdomen (epigastric) | Oberbauch Mitte | front | Core |
| 23 | `abdomen_upper_right` | Upper right abdomen | Oberbauch rechts | front | Core |
| 24 | `abdomen_central` | Central abdomen / navel | Bauchmitte / Nabelregion | front | Core |
| 25 | `abdomen_lower_left` | Lower left abdomen | Unterbauch links | front | Core |
| 26 | `abdomen_lower_central` | Lower central abdomen | Unterbauch Mitte | front | Core |
| 27 | `abdomen_lower_right` | Lower right abdomen | Unterbauch rechts | front | Core |
| 28 | `groin_left` | Left groin | Leiste links | front | Core |
| 29 | `groin_right` | Right groin | Leiste rechts | front | Core |
| 30 | `genital_region` | Genital region | Genitalbereich | front | Extended |

## Torso — Back

| # | Key | English | German | View | Tier |
|---|-----|---------|--------|------|------|
| 31 | `upper_back_left` | Upper back left (shoulder blade) | Oberer Rücken links (Schulterblatt) | back | Core |
| 32 | `upper_back_right` | Upper back right (shoulder blade) | Oberer Rücken rechts (Schulterblatt) | back | Core |
| 33 | `spine_central` | Mid back / spine | Mittlerer Rücken / Wirbelsäule | back | Core |
| 34 | `lower_back_left` | Lower back left | Unterer Rücken links (Lende) | back | Core |
| 35 | `lower_back_right` | Lower back right | Unterer Rücken rechts (Lende) | back | Core |
| 36 | `sacrum` | Sacrum / tailbone | Kreuzbein / Steißbein | back | Core |
| 37 | `buttock_left` | Left buttock | Gesäß links | back | Core |
| 38 | `buttock_right` | Right buttock | Gesäß rechts | back | Core |

## Arms & Hands (per side)

| # | Key | English | German | View | Tier |
|---|-----|---------|--------|------|------|
| 39/40 | `shoulder_front_left` / `..._right` | Shoulder, front | Schulter vorne | front | Core |
| 41/42 | `shoulder_back_left` / `..._right` | Shoulder, back | Schulter hinten | back | Core |
| 43/44 | `armpit_left` / `armpit_right` | Armpit | Achselhöhle | front | Extended |
| 45/46 | `upper_arm_front_left` / `..._right` | Upper arm, front | Oberarm vorne | front | Core |
| 47/48 | `upper_arm_back_left` / `..._right` | Upper arm, back | Oberarm hinten | back | Core |
| 49/50 | `elbow_front_left` / `..._right` | Crook of elbow | Ellenbeuge | front | Core |
| 51/52 | `elbow_back_left` / `..._right` | Elbow (olecranon) | Ellenbogen | back | Core |
| 53/54 | `forearm_front_left` / `..._right` | Forearm, front | Unterarm vorne | front | Core |
| 55/56 | `forearm_back_left` / `..._right` | Forearm, back | Unterarm hinten | back | Core |
| 57/58 | `wrist_left` / `wrist_right` | Wrist | Handgelenk | front | Core |
| 59/60 | `hand_palm_left` / `..._right` | Palm of hand | Handfläche | front† | Extended |
| 61/62 | `hand_back_left` / `..._right` | Back of hand | Handrücken | front† | Core |
| 63/64 | `thumb_left` / `thumb_right` | Thumb | Daumen | front† | Core |
| 65/66 | `index_finger_left` / `..._right` | Index finger | Zeigefinger | front† | Core |
| 67/68 | `middle_finger_left` / `..._right` | Middle finger | Mittelfinger | front† | Core |
| 69/70 | `ring_finger_left` / `..._right` | Ring finger | Ringfinger | front† | Core |
| 71/72 | `little_finger_left` / `..._right` | Little finger | Kleiner Finger | front† | Core |

† palm/finger orientation in the SMPL-X T-pose must be verified during generation — front view may show the palm or the back of the hand depending on pose; the generator should pick the view per hand side accordingly. Individual fingers are thin targets: use a small brush radius and possibly a zoomed-in camera (the part's stored `view` benefits from this anyway).

## Legs & Feet (per side)

| # | Key | English | German | View | Tier |
|---|-----|---------|--------|------|------|
| 73/74 | `hip_left` / `hip_right` | Hip | Hüfte | front | Core |
| 75/76 | `thigh_front_left` / `..._right` | Thigh, front | Oberschenkel vorne | front | Core |
| 77/78 | `thigh_back_left` / `..._right` | Thigh, back | Oberschenkel hinten | back | Core |
| 79/80 | `knee_front_left` / `..._right` | Knee | Knie | front | Core |
| 81/82 | `knee_back_left` / `..._right` | Back of knee | Kniekehle | back | Core |
| 83/84 | `shin_left` / `shin_right` | Shin | Schienbein | front | Core |
| 85/86 | `calf_left` / `calf_right` | Calf | Wade | back | Core |
| 87/88 | `ankle_left` / `ankle_right` | Ankle | Knöchel / Sprunggelenk | front | Core |
| 89/90 | `foot_top_left` / `..._right` | Top of foot | Fußrücken | front | Core |
| 91/92 | `heel_left` / `heel_right` | Heel | Ferse | back | Extended |
| 93/94 | `big_toe_left` / `big_toe_right` | Big toe | Großzehe | front | Core |
| 95/96 | `toes_left` / `toes_right` | Toes (2nd–5th) | Zehen (2.–5.) | front | Extended |
| 97/98 | `foot_sole_left` / `..._right` | Sole of foot | Fußsohle | bottom‡ | Extended |

‡ soles need a bottom-up camera angle (pitch), which the app's LeanPitchYaw may or may not allow — verify; if not paintable, drop or approximate via heel + toes.

## Joint coverage (for the LLM prompt's "joint" vocabulary)

All major joints are explicitly covered, front and back where anatomically relevant.
Circumferential complaints ("around the …") map to both sides of the joint:

| Joint | German | Regions |
|-------|--------|---------|
| Jaw (TMJ) | Kiefergelenk | `jaw_joint_left/right` |
| Shoulder | Schultergelenk | `shoulder_front_*` + `shoulder_back_*` |
| Elbow | Ellenbogengelenk | `elbow_front_*` + `elbow_back_*` |
| Wrist | Handgelenk | `wrist_*` |
| Finger joints | Fingergelenke | covered by the individual finger regions |
| Hip | Hüftgelenk | `hip_*` (front) + `buttock_*` (posterior aspect) |
| Knee | Kniegelenk | `knee_front_*` + `knee_back_*` |
| Ankle | Sprunggelenk | `ankle_*` (front) + `heel_*` (posterior aspect) |
| Big toe joint | Großzehengrundgelenk | covered by `big_toe_*` (gout!) |

## Summary

- **Core:** 74 keys — includes all abdomen quadrants, joints front/back, limb segments, individual fingers, big toe.
- **Extended:** 24 keys — face detail, jaw joints, ears, armpit, genital region, palm, heel, toes, soles.
- **Total:** 98 region keys.

## Open review questions

1. Breast as separate region (`breast_left/right`) vs. covered by `chest_left/right`? (medical relevance vs. sensitivity)
2. Do we need `flank_left/right` (Flanke) as separate lateral regions between abdomen and back? Common in phrasing ("Flankenschmerz") — currently not in the list; would require a ±90° side view.
3. `spine_central` as one strip vs. split into cervical/thoracic/lumbar spine segments?
4. ~~Thumb / individual fingers?~~ → resolved: individual fingers are Core (rev 2).
5. Jaw joint: keep as Extended or promote to Core?
