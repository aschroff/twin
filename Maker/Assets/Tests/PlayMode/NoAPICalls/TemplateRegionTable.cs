using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NoAPICalls
{
    /// <summary>
    /// Data-driven catalog of body region template definitions.
    /// Region keys and conventions: see Assets/Resources/BODY_REGIONS.md.
    ///
    /// Anchors are computed from SMPL-X bone world positions at paint time (AFTER the
    /// body has been rotated to the region's yaw/pitch), so they are always valid.
    /// Offsets use patient axes provided by <see cref="AnchorCtx"/> (Up/Left/Forward
    /// rotate together with the body).
    /// </summary>
    public static class TemplateRegionTable
    {
        public enum ToolKind
        {
            MarkerStroke, // Red marker: anchors = [from, to]
            FillCircle    // Red filler: anchors = [center], Radius = world radius of the outline circle
        }

        public struct AnchorCtx
        {
            public Func<string, Vector3> Bone; // bone name -> world position
            public Vector3 Up;                 // patient up (world, post-rotation)
            public Vector3 Left;               // patient left (world, post-rotation)
            public Vector3 Forward;            // patient forward (world, post-rotation)
        }

        public class RegionDef
        {
            public string Key;
            public string Batch;
            public float Yaw;          // body yaw while painting (0 = front view, 180 = back view)
            public float Pitch;        // body pitch while painting (hands/feet/head-top experiments)
            public ToolKind Tool;
            public float Radius;       // FillCircle: world radius of the outline
            public float BrushRadius;  // MarkerStroke: CwPaintSphere.Radius override (0 = keep tool default 0.1).
                                       // NOTE: renders at ~1:10 — Radius 0.1 paints a ~1 cm line core.
            public Func<AnchorCtx, Vector3[]> Anchors;
        }

        private static RegionDef Fill(string batch, string key, float yaw, float radius, Func<AnchorCtx, Vector3> center, float pitch = 0f)
            => new RegionDef { Key = key, Batch = batch, Yaw = yaw, Pitch = pitch, Tool = ToolKind.FillCircle, Radius = radius, Anchors = c => new[] { center(c) } };

        private static RegionDef Stroke(string batch, string key, float yaw, Func<AnchorCtx, Vector3> from, Func<AnchorCtx, Vector3> to, float brushRadius = 0f, float pitch = 0f)
            => new RegionDef { Key = key, Batch = batch, Yaw = yaw, Pitch = pitch, Tool = ToolKind.MarkerStroke, BrushRadius = brushRadius, Anchors = c => new[] { from(c), to(c) } };

        /// <summary>Stroke along two bones with normalized from/to interpolation.</summary>
        private static RegionDef BoneStroke(string batch, string key, float yaw, string boneA, string boneB, float tFrom, float tTo, float brushRadius = 0f, float pitch = 0f)
            => Stroke(batch, key,
                yaw,
                c => Vector3.Lerp(c.Bone(boneA), c.Bone(boneB), tFrom),
                c => Vector3.Lerp(c.Bone(boneA), c.Bone(boneB), tTo),
                brushRadius, pitch);

        public static readonly List<RegionDef> All = Build();

        public static IEnumerable<RegionDef> ForBatch(string batch) => All.Where(r => r.Batch == batch);

        public static IEnumerable<string> Batches => All.Select(r => r.Batch).Distinct();

        private static List<RegionDef> Build()
        {
            var list = new List<RegionDef>();

            // ---------------- Batch: torso_front (yaw 0) ----------------
            const string TF = "torso_front";
            list.Add(Fill(TF, "chest_left", 0, 0.055f, c => Vector3.Lerp(c.Bone("spine3"), c.Bone("left_shoulder"), 0.5f) - c.Up * 0.04f));
            list.Add(Fill(TF, "chest_right", 0, 0.055f, c => Vector3.Lerp(c.Bone("spine3"), c.Bone("right_shoulder"), 0.5f) - c.Up * 0.04f));
            list.Add(Fill(TF, "sternum", 0, 0.035f, c => c.Bone("spine3") + c.Up * 0.04f));
            list.Add(Fill(TF, "abdomen_upper_left", 0, 0.045f, c => AbdomenPoint(c, 0.80f, 0.07f)));
            list.Add(Fill(TF, "abdomen_upper_central", 0, 0.045f, c => AbdomenPoint(c, 0.85f, 0f)));
            list.Add(Fill(TF, "abdomen_upper_right", 0, 0.045f, c => AbdomenPoint(c, 0.80f, -0.07f)));
            list.Add(Fill(TF, "abdomen_central", 0, 0.05f, c => AbdomenPoint(c, 0.50f, 0f)));
            list.Add(Fill(TF, "abdomen_lower_left", 0, 0.045f, c => AbdomenPoint(c, 0.18f, 0.07f)));
            list.Add(Fill(TF, "abdomen_lower_central", 0, 0.045f, c => AbdomenPoint(c, 0.12f, 0f)));
            list.Add(Fill(TF, "abdomen_lower_right", 0, 0.045f, c => AbdomenPoint(c, 0.18f, -0.07f)));
            // groin/genital/buttock sit on strongly curved surfaces where the fill tool's
            // grid points reuse the outline's cached raycast depth and miss the receding
            // surface — use thick marker strokes instead (fresh raycast per point)
            list.Add(Stroke(TF, "groin_left", 0,
                c => Vector3.Lerp(c.Bone("left_hip"), c.Bone("left_knee"), 0.06f) - c.Left * 0.02f,
                c => Vector3.Lerp(c.Bone("left_hip"), c.Bone("left_knee"), 0.14f) + c.Left * 0.02f, 0.15f));
            list.Add(Stroke(TF, "groin_right", 0,
                c => Vector3.Lerp(c.Bone("right_hip"), c.Bone("right_knee"), 0.06f) + c.Left * 0.02f,
                c => Vector3.Lerp(c.Bone("right_hip"), c.Bone("right_knee"), 0.14f) - c.Left * 0.02f, 0.15f));
            list.Add(Stroke(TF, "genital_region", 0,
                c => c.Bone("pelvis") - c.Up * 0.08f,
                c => c.Bone("pelvis") - c.Up * 0.12f, 0.12f));

            // ---------------- Batch: torso_back (yaw 180) ----------------
            const string TB = "torso_back";
            list.Add(Fill(TB, "upper_back_left", 180, 0.055f, c => Vector3.Lerp(c.Bone("spine3"), c.Bone("left_shoulder"), 0.55f) - c.Up * 0.02f));
            list.Add(Fill(TB, "upper_back_right", 180, 0.055f, c => Vector3.Lerp(c.Bone("spine3"), c.Bone("right_shoulder"), 0.55f) - c.Up * 0.02f));
            list.Add(BoneStroke(TB, "spine_central", 180, "neck", "spine1", 0.15f, 0.95f));
            list.Add(Fill(TB, "lower_back_left", 180, 0.04f, c => c.Bone("spine1") + c.Left * 0.06f - c.Up * 0.05f));
            list.Add(Fill(TB, "lower_back_right", 180, 0.04f, c => c.Bone("spine1") - c.Left * 0.06f - c.Up * 0.05f));
            list.Add(Fill(TB, "sacrum", 180, 0.035f, c => c.Bone("pelvis") - c.Up * 0.05f));
            // buttock: thick horizontal marker stroke across the cheek center —
            // see curved-surface note at the groin regions
            list.Add(Stroke(TB, "buttock_left", 180,
                c => Vector3.Lerp(c.Bone("left_hip"), c.Bone("left_knee"), 0.10f) - c.Left * 0.025f,
                c => Vector3.Lerp(c.Bone("left_hip"), c.Bone("left_knee"), 0.10f) + c.Left * 0.04f, 0.3f));
            list.Add(Stroke(TB, "buttock_right", 180,
                c => Vector3.Lerp(c.Bone("right_hip"), c.Bone("right_knee"), 0.10f) + c.Left * 0.025f,
                c => Vector3.Lerp(c.Bone("right_hip"), c.Bone("right_knee"), 0.10f) - c.Left * 0.04f, 0.3f));

            // ---------------- Batch: arms_front (yaw 0) ----------------
            const string AF = "arms_front";
            list.Add(Fill(AF, "shoulder_front_left", 0, 0.04f, c => c.Bone("left_shoulder") + c.Up * 0.02f));
            list.Add(Fill(AF, "shoulder_front_right", 0, 0.04f, c => c.Bone("right_shoulder") + c.Up * 0.02f));
            list.Add(BoneStroke(AF, "upper_arm_front_left", 0, "left_shoulder", "left_elbow", 0.25f, 0.75f));
            list.Add(BoneStroke(AF, "upper_arm_front_right", 0, "right_shoulder", "right_elbow", 0.25f, 0.75f));
            list.Add(Fill(AF, "elbow_front_left", 0, 0.028f, c => c.Bone("left_elbow")));
            list.Add(Fill(AF, "elbow_front_right", 0, 0.028f, c => c.Bone("right_elbow")));
            list.Add(BoneStroke(AF, "forearm_front_left", 0, "left_elbow", "left_wrist", 0.25f, 0.75f));
            list.Add(BoneStroke(AF, "forearm_front_right", 0, "right_elbow", "right_wrist", 0.25f, 0.75f));
            list.Add(Fill(AF, "wrist_left", 0, 0.024f, c => c.Bone("left_wrist")));
            list.Add(Fill(AF, "wrist_right", 0, 0.024f, c => c.Bone("right_wrist")));

            // ---------------- Batch: arms_back (yaw 180) ----------------
            const string AB = "arms_back";
            list.Add(Fill(AB, "shoulder_back_left", 180, 0.04f, c => c.Bone("left_shoulder") + c.Up * 0.02f));
            list.Add(Fill(AB, "shoulder_back_right", 180, 0.04f, c => c.Bone("right_shoulder") + c.Up * 0.02f));
            list.Add(BoneStroke(AB, "upper_arm_back_left", 180, "left_shoulder", "left_elbow", 0.25f, 0.75f));
            list.Add(BoneStroke(AB, "upper_arm_back_right", 180, "right_shoulder", "right_elbow", 0.25f, 0.75f));
            list.Add(Fill(AB, "elbow_back_left", 180, 0.028f, c => c.Bone("left_elbow")));
            list.Add(Fill(AB, "elbow_back_right", 180, 0.028f, c => c.Bone("right_elbow")));
            list.Add(BoneStroke(AB, "forearm_back_left", 180, "left_elbow", "left_wrist", 0.25f, 0.75f));
            list.Add(BoneStroke(AB, "forearm_back_right", 180, "right_elbow", "right_wrist", 0.25f, 0.75f));

            // ---------------- Batch: legs_front (yaw 0) ----------------
            const string LF = "legs_front";
            list.Add(Fill(LF, "hip_left", 0, 0.04f, c => c.Bone("left_hip") + c.Left * 0.045f));
            list.Add(Fill(LF, "hip_right", 0, 0.04f, c => c.Bone("right_hip") - c.Left * 0.045f));
            list.Add(BoneStroke(LF, "thigh_front_left", 0, "left_hip", "left_knee", 0.3f, 0.8f));
            list.Add(BoneStroke(LF, "thigh_front_right", 0, "right_hip", "right_knee", 0.3f, 0.8f));
            list.Add(Fill(LF, "knee_front_left", 0, 0.032f, c => c.Bone("left_knee")));
            list.Add(Fill(LF, "knee_front_right", 0, 0.032f, c => c.Bone("right_knee")));
            list.Add(BoneStroke(LF, "shin_left", 0, "left_knee", "left_ankle", 0.25f, 0.75f));
            list.Add(BoneStroke(LF, "shin_right", 0, "right_knee", "right_ankle", 0.25f, 0.75f));
            list.Add(Fill(LF, "ankle_left", 0, 0.024f, c => c.Bone("left_ankle")));
            list.Add(Fill(LF, "ankle_right", 0, 0.024f, c => c.Bone("right_ankle")));
            list.Add(Fill(LF, "foot_top_left", 0, 0.024f, c => Vector3.Lerp(c.Bone("left_ankle"), c.Bone("left_foot"), 0.6f)));
            list.Add(Fill(LF, "foot_top_right", 0, 0.024f, c => Vector3.Lerp(c.Bone("right_ankle"), c.Bone("right_foot"), 0.6f)));
            list.Add(Stroke(LF, "big_toe_left", 0,
                c => c.Bone("left_foot") + c.Forward * 0.03f - c.Left * 0.015f,
                c => c.Bone("left_foot") + c.Forward * 0.07f - c.Left * 0.015f, 0.08f));
            list.Add(Stroke(LF, "big_toe_right", 0,
                c => c.Bone("right_foot") + c.Forward * 0.03f + c.Left * 0.015f,
                c => c.Bone("right_foot") + c.Forward * 0.07f + c.Left * 0.015f, 0.08f));
            list.Add(Stroke(LF, "toes_left", 0,
                c => c.Bone("left_foot") + c.Forward * 0.05f + c.Left * 0.01f,
                c => c.Bone("left_foot") + c.Forward * 0.05f + c.Left * 0.045f, 0.08f));
            list.Add(Stroke(LF, "toes_right", 0,
                c => c.Bone("right_foot") + c.Forward * 0.05f - c.Left * 0.01f,
                c => c.Bone("right_foot") + c.Forward * 0.05f - c.Left * 0.045f, 0.08f));

            // ---------------- Batch: legs_back (yaw 180) ----------------
            const string LB = "legs_back";
            list.Add(BoneStroke(LB, "thigh_back_left", 180, "left_hip", "left_knee", 0.3f, 0.8f));
            list.Add(BoneStroke(LB, "thigh_back_right", 180, "right_hip", "right_knee", 0.3f, 0.8f));
            list.Add(Fill(LB, "knee_back_left", 180, 0.028f, c => c.Bone("left_knee")));
            list.Add(Fill(LB, "knee_back_right", 180, 0.028f, c => c.Bone("right_knee")));
            list.Add(BoneStroke(LB, "calf_left", 180, "left_knee", "left_ankle", 0.2f, 0.6f));
            list.Add(BoneStroke(LB, "calf_right", 180, "right_knee", "right_ankle", 0.2f, 0.6f));
            list.Add(Fill(LB, "heel_left", 180, 0.022f, c => c.Bone("left_ankle") - c.Up * 0.045f - c.Forward * 0.02f));
            list.Add(Fill(LB, "heel_right", 180, 0.022f, c => c.Bone("right_ankle") - c.Up * 0.045f - c.Forward * 0.02f));

            // ---------------- Batch: head_neck (mixed yaw) ----------------
            const string HN = "head_neck";
            list.Add(Fill(HN, "forehead", 0, 0.03f, c => c.Bone("head") + c.Up * 0.065f));
            list.Add(Stroke(HN, "temple_left", 0,
                c => c.Bone("head") + c.Up * 0.045f + c.Left * 0.05f,
                c => c.Bone("head") + c.Up * 0.03f + c.Left * 0.055f, 0.08f));
            list.Add(Stroke(HN, "temple_right", 0,
                c => c.Bone("head") + c.Up * 0.045f - c.Left * 0.05f,
                c => c.Bone("head") + c.Up * 0.03f - c.Left * 0.055f, 0.08f));
            list.Add(Stroke(HN, "eye_left", 0,
                c => c.Bone("head") + c.Up * 0.028f + c.Left * 0.02f,
                c => c.Bone("head") + c.Up * 0.028f + c.Left * 0.042f, 0.06f));
            list.Add(Stroke(HN, "eye_right", 0,
                c => c.Bone("head") + c.Up * 0.028f - c.Left * 0.02f,
                c => c.Bone("head") + c.Up * 0.028f - c.Left * 0.042f, 0.06f));
            list.Add(Stroke(HN, "nose", 0,
                c => c.Bone("head") + c.Up * 0.02f,
                c => c.Bone("head") - c.Up * 0.005f, 0.06f));
            list.Add(Stroke(HN, "cheek_left", 0,
                c => c.Bone("head") + c.Left * 0.035f,
                c => c.Bone("head") - c.Up * 0.015f + c.Left * 0.03f, 0.07f));
            list.Add(Stroke(HN, "cheek_right", 0,
                c => c.Bone("head") - c.Left * 0.035f,
                c => c.Bone("head") - c.Up * 0.015f - c.Left * 0.03f, 0.07f));
            list.Add(Stroke(HN, "mouth_chin", 0,
                c => c.Bone("head") - c.Up * 0.035f,
                c => c.Bone("head") - c.Up * 0.06f, 0.07f));
            list.Add(Fill(HN, "head_top", 0, 0.03f, c => c.Bone("head") + c.Up * 0.11f, pitch: 35f));
            list.Add(Fill(HN, "head_back", 180, 0.035f, c => c.Bone("head") + c.Up * 0.05f));
            list.Add(Fill(HN, "neck_front", 0, 0.022f, c => c.Bone("neck") + c.Up * 0.03f));
            list.Add(Fill(HN, "neck_back", 180, 0.025f, c => c.Bone("neck") + c.Up * 0.02f));

            // ---------------- Batch: hands (pitch experiments — palms face down in T-pose) ----------------
            const string HA = "hands";
            // Back of hand: tilt body backward so hand dorsum faces the camera.
            list.Add(Fill(HA, "hand_back_left", 0, 0.03f, c => HandCenter(c, "left"), pitch: -50f));
            list.Add(Fill(HA, "hand_back_right", 0, 0.03f, c => HandCenter(c, "right"), pitch: -50f));
            list.Add(Fill(HA, "hand_palm_left", 0, 0.03f, c => HandCenter(c, "left"), pitch: 50f));
            list.Add(Fill(HA, "hand_palm_right", 0, 0.03f, c => HandCenter(c, "right"), pitch: 50f));
            list.Add(FingerStroke(HA, "thumb_left", "left_thumb1", "left_thumb3", -50f));
            list.Add(FingerStroke(HA, "thumb_right", "right_thumb1", "right_thumb3", -50f));
            list.Add(FingerStroke(HA, "index_finger_left", "left_index1", "left_index3", -50f));
            list.Add(FingerStroke(HA, "index_finger_right", "right_index1", "right_index3", -50f));
            list.Add(FingerStroke(HA, "middle_finger_left", "left_middle1", "left_middle3", -50f));
            list.Add(FingerStroke(HA, "middle_finger_right", "right_middle1", "right_middle3", -50f));
            list.Add(FingerStroke(HA, "ring_finger_left", "left_ring1", "left_ring3", -50f));
            list.Add(FingerStroke(HA, "ring_finger_right", "right_ring1", "right_ring3", -50f));
            list.Add(FingerStroke(HA, "little_finger_left", "left_pinky1", "left_pinky3", -50f));
            list.Add(FingerStroke(HA, "little_finger_right", "right_pinky1", "right_pinky3", -50f));

            // ---------------- Batch: extended_misc ----------------
            const string EX = "extended_misc";
            list.Add(Fill(EX, "armpit_left", 0, 0.022f, c => c.Bone("left_shoulder") - c.Up * 0.06f - c.Left * 0.015f));
            list.Add(Fill(EX, "armpit_right", 0, 0.022f, c => c.Bone("right_shoulder") - c.Up * 0.06f + c.Left * 0.015f));
            list.Add(Stroke(EX, "jaw_joint_left", 60,
                c => c.Bone("head") + c.Left * 0.06f,
                c => c.Bone("head") - c.Up * 0.015f + c.Left * 0.06f, 0.06f));
            list.Add(Stroke(EX, "jaw_joint_right", -60,
                c => c.Bone("head") - c.Left * 0.06f,
                c => c.Bone("head") - c.Up * 0.015f - c.Left * 0.06f, 0.06f));
            list.Add(Stroke(EX, "ear_left", 70,
                c => c.Bone("head") + c.Up * 0.02f + c.Left * 0.07f,
                c => c.Bone("head") - c.Up * 0.005f + c.Left * 0.07f, 0.06f));
            list.Add(Stroke(EX, "ear_right", -70,
                c => c.Bone("head") + c.Up * 0.02f - c.Left * 0.07f,
                c => c.Bone("head") - c.Up * 0.005f - c.Left * 0.07f, 0.06f));
            // foot soles: bottom view — likely not paintable with LeanPitchYaw pitch clamps; attempt with strong pitch.
            list.Add(Fill(EX, "foot_sole_left", 0, 0.025f, c => Vector3.Lerp(c.Bone("left_ankle"), c.Bone("left_foot"), 0.7f) - c.Up * 0.03f, pitch: -80f));
            list.Add(Fill(EX, "foot_sole_right", 0, 0.025f, c => Vector3.Lerp(c.Bone("right_ankle"), c.Bone("right_foot"), 0.7f) - c.Up * 0.03f, pitch: -80f));

            return list;
        }

        /// <summary>Point on the abdomen: t = 0 (pelvis height) .. 1 (spine2 height), sideOffset in patient-left meters.</summary>
        private static Vector3 AbdomenPoint(AnchorCtx c, float t, float sideOffset)
        {
            return Vector3.Lerp(c.Bone("pelvis"), c.Bone("spine2"), t) + c.Left * sideOffset;
        }

        private static Vector3 HandCenter(AnchorCtx c, string side)
        {
            return Vector3.Lerp(c.Bone(side + "_wrist"), c.Bone(side + "_middle1"), 0.55f);
        }

        private static RegionDef FingerStroke(string batch, string key, string boneA, string boneB, float pitch)
        {
            // stroke from first knuckle to slightly past the last joint (finger tip)
            return new RegionDef
            {
                Key = key,
                Batch = batch,
                Yaw = 0,
                Pitch = pitch,
                Tool = ToolKind.MarkerStroke,
                BrushRadius = 0.06f,
                Anchors = c => new[]
                {
                    Vector3.Lerp(c.Bone(boneA), c.Bone(boneB), 0.05f),
                    Vector3.LerpUnclamped(c.Bone(boneA), c.Bone(boneB), 1.25f)
                }
            };
        }
    }
}
