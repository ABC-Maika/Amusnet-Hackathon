using UnityEngine;

namespace Amusnet.ChickenEscape
{
    internal sealed class EscapeKeeperRig
    {
        readonly Transform root, hips, spine;
        readonly Limb leftArm, rightArm, leftLeg, rightLeg;
        readonly Transform[] bones;
        readonly Quaternion[] rotations;
        readonly float standingHipHeight;
        Vector3 hipsPosition;
        bool posed;

        public EscapeKeeperRig(Animator animator, Transform keeper)
        {
            root = keeper;
            hips = animator.transform.Find("Skeleton/Root/Hips");
            if (!hips) return;

            spine = hips.Find("Spine");
            if (!spine) return;
            float restHeight = keeper.InverseTransformPoint(hips.position).y;
            standingHipHeight = restHeight > .7f ? restHeight : 1.08f;

            leftArm = new Limb(spine, "UpperArm.L/Forearm.L/Hand.L");
            rightArm = new Limb(spine, "UpperArm.R/Forearm.R/Hand.R");
            leftLeg = new Limb(hips, "Thigh.L/Shin.L/Foot.L");
            rightLeg = new Limb(hips, "Thigh.R/Shin.R/Foot.R");
            if (!leftArm.Valid || !rightArm.Valid || !leftLeg.Valid || !rightLeg.Valid) return;

            bones = new[]
            {
                spine, leftArm.Upper, leftArm.Lower, rightArm.Upper, rightArm.Lower,
                leftLeg.Upper, leftLeg.Lower, rightLeg.Upper, rightLeg.Lower
            };
            rotations = new Quaternion[bones.Length];
        }

        public void Restore()
        {
            if (!posed) return;
            hips.localPosition = hipsPosition;
            for (int i = 0; i < bones.Length; i++) bones[i].localRotation = rotations[i];
            posed = false;
        }

        public Vector3 Cradle(Vector3 target, float crouch, float reach)
        {
            if (bones == null) return target;
            Bend(crouch);
            Vector3 across = root.right * .13f;
            leftArm.Reach(Vector3.Lerp(leftArm.End.position, target - across, reach), -root.right - Vector3.up * .85f);
            rightArm.Reach(Vector3.Lerp(rightArm.End.position, target + across, reach), root.right - Vector3.up * .85f);
            return (leftArm.End.position + rightArm.End.position) * .5f;
        }

        public void HoldCollar(Vector3 collar, float reach)
        {
            if (bones == null) return;
            Bend(reach * .94f);
            rightArm.Reach(Vector3.Lerp(rightArm.End.position, collar, reach), root.right - Vector3.up);
            Vector3 brace = hips.position - root.right * .24f + root.forward * .23f - Vector3.up * .29f;
            leftArm.Reach(Vector3.Lerp(leftArm.End.position, brace, reach), -root.right - Vector3.up);
        }

        void Bend(float crouch)
        {
            hipsPosition = hips.localPosition;
            for (int i = 0; i < bones.Length; i++) rotations[i] = bones[i].localRotation;
            posed = true;

            // The market idle clips key small pelvis offsets. Anchor a reach to the rig's standing height.
            float neutralHeight = root.position.y + standingHipHeight;
            if (hips.position.y < neutralHeight - .20f)
                hips.position += Vector3.up * (neutralHeight - hips.position.y);
            if (crouch <= .001f) return;
            var leftFoot = leftLeg.End.position;
            var rightFoot = rightLeg.End.position;
            hips.position += (root.forward * .15f - Vector3.up * .51f) * crouch;
            spine.rotation = Quaternion.AngleAxis(59f * crouch, root.right) * spine.rotation;
            leftLeg.Reach(leftFoot, root.forward - root.right * .12f);
            rightLeg.Reach(rightFoot, root.forward + root.right * .12f);
        }

        readonly struct Limb
        {
            public readonly Transform Upper, Lower, End;
            public bool Valid => Upper && Lower && End;

            public Limb(Transform parent, string path)
            {
                End = parent.Find(path);
                Lower = End ? End.parent : null;
                Upper = Lower ? Lower.parent : null;
            }

            public void Reach(Vector3 target, Vector3 pole)
            {
                Vector3 shoulder = Upper.position;
                Vector3 offset = target - shoulder;
                if (offset.sqrMagnitude < .0001f) return;

                float first = Vector3.Distance(shoulder, Lower.position);
                float second = Vector3.Distance(Lower.position, End.position);
                Vector3 direction = offset.normalized;
                Vector3 bend = Vector3.ProjectOnPlane(pole, direction).normalized;
                if (bend.sqrMagnitude < .01f) return;

                float distance = Mathf.Clamp(offset.magnitude, Mathf.Abs(first - second) + .005f, first + second - .005f);
                float along = (first * first - second * second + distance * distance) / (2f * distance);
                float sideways = Mathf.Sqrt(Mathf.Max(0f, first * first - along * along));
                Vector3 elbow = shoulder + direction * along + bend * sideways;

                Upper.rotation = Quaternion.FromToRotation(Lower.position - shoulder, elbow - shoulder) * Upper.rotation;
                Lower.rotation = Quaternion.FromToRotation(End.position - Lower.position,
                    shoulder + direction * distance - Lower.position) * Lower.rotation;
            }
        }
    }
}
