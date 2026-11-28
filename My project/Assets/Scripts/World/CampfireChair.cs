using System.Linq;
using UnityEngine;

namespace Amusnet.World
{
    public sealed class CampfireChair : MonoBehaviour
    {
        [SerializeField] Animator sitter;
        [SerializeField] Transform seat, furniture, rearFeet;
        [SerializeField, Min(0.1f)] float jumpSeconds = 0.54f;
        [SerializeField, Min(0f)] float jumpDistance = 1.05f;
        [SerializeField, Min(0f)] float jumpHeight = 0.32f;

        Transform actor, hips, spine, head;
        readonly Transform[] thighs = new Transform[2], shins = new Transform[2],
            feet = new Transform[2], arms = new Transform[2], forearms = new Transform[2];
        Transform[] posedBones;
        Quaternion[] rest;
        Vector3 chairPosition, start, finish;
        Quaternion chairRotation;

        public Vector3 LandingPoint => seat.position + transform.forward * jumpDistance;

        void Awake()
        {
            actor = sitter.transform;
            var bones = sitter.GetComponentsInChildren<Transform>();
            Transform Bone(string boneName) => bones.First(bone => bone.name == boneName);
            hips = Bone("Hips");
            spine = Bone("Spine");
            head = Bone("Head");
            for (int side = 0; side < 2; side++)
            {
                string suffix = side == 0 ? ".L" : ".R";
                thighs[side] = Bone("Thigh" + suffix);
                shins[side] = Bone("Shin" + suffix);
                feet[side] = Bone("Foot" + suffix);
                arms[side] = Bone("UpperArm" + suffix);
                forearms[side] = Bone("Forearm" + suffix);
            }
            posedBones = new[] { hips, spine, head }.Concat(thighs).Concat(shins)
                .Concat(feet).Concat(arms).Concat(forearms).ToArray();
            rest = posedBones.Select(bone => bone.localRotation).ToArray();
            chairPosition = furniture.localPosition;
            chairRotation = furniture.localRotation;
        }

        public void Sit()
        {
            sitter.enabled = false;
            furniture.SetLocalPositionAndRotation(chairPosition, chairRotation);
            actor.rotation = transform.rotation;
            RestoreBones();
            actor.position += seat.position - hips.position;
            Pose(1f, 0f);
        }

        public void BeginJump(Vector3 landing)
        {
            start = actor.position;
            var terrain = Terrain.activeTerrain;
            if (terrain) landing.y = terrain.SampleHeight(landing) + terrain.transform.position.y;
            else if (Physics.Raycast(landing + Vector3.up * 2f, Vector3.down, out var hit, 5f))
                landing.y = hit.point.y;
            finish = landing;
        }

        public bool Jump(float elapsed)
        {
            float t = Mathf.Clamp01(elapsed / jumpSeconds);
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.54f, t));
            float hop = Mathf.Sin(t * Mathf.PI);
            actor.position = Vector3.Lerp(start, finish, rise) + Vector3.up * (hop * jumpHeight);
            RestoreBones();
            Pose(1f - rise, hop);

            float tip = Mathf.InverseLerp(0.08f, 0.89f, t);
            float angle = Mathf.Lerp(0f, -90f, tip * tip);
            if (t > 0.89f) angle += Mathf.Sin(Mathf.InverseLerp(0.89f, 1f, t) * Mathf.PI) * 5f;
            furniture.SetLocalPositionAndRotation(chairPosition, chairRotation);
            furniture.RotateAround(rearFeet.position, transform.right, angle);
            if (t < 1f) return false;

            RestoreBones();
            sitter.enabled = true;
            sitter.speed = 1f;
            return true;
        }

        void RestoreBones()
        {
            for (int i = 0; i < posedBones.Length; i++) posedBones[i].localRotation = rest[i];
        }

        void Pose(float sitting, float hop)
        {
            Bend(spine, 9f * sitting + 14f * hop);
            Bend(head, -7f * sitting - 10f * hop);
            for (int side = 0; side < 2; side++)
            {
                Bend(thighs[side], -84f * sitting - (side == 0 ? 26f : -11f) * hop);
                Bend(shins[side], 88f * sitting + 24f * hop);
                Bend(feet[side], -4f * sitting - 6f * hop);
                Bend(arms[side], -32f * sitting - (side == 0 ? 53f : 26f) * hop);
                Bend(forearms[side], -12f * sitting - 28f * hop);
            }
        }

        void Bend(Transform bone, float angle) =>
            bone.rotation = Quaternion.AngleAxis(angle, actor.right) * bone.rotation;
    }
}
