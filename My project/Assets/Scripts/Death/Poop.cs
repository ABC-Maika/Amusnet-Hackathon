using UnityEngine;

namespace Butterfly.Death
{
    /// <summary>
    /// What's left of the butterfly after a predator digests it. Built from primitives (no art assets):
    ///  - Bird: a white dropping that falls and splats into a white-and-grey splotch.
    ///  - Frog: a single glossy dark olive pellet.
    ///  - Fish: a thin brown string that sinks slowly, trailing bubbles.
    /// It falls (or sinks) to the first solid surface below, then lands. DeathManager watches Landed.
    /// </summary>
    public sealed class Poop : MonoBehaviour
    {
        public PoopKind Kind { get; private set; }
        public bool Landed { get; private set; }
        public Vector3 LandPoint { get; private set; }

        static readonly Color BirdWhite = new Color(0.96f, 0.95f, 0.9f);
        static readonly Color BirdGrey = new Color(0.32f, 0.31f, 0.3f);
        static readonly Color FrogOlive = new Color(0.2f, 0.22f, 0.09f);
        static readonly Color FishBrown = new Color(0.42f, 0.27f, 0.12f);

        Transform body;
        float velocity;
        ParticleSystem bubbles;

        public static Poop Spawn(PoopKind kind, Vector3 at)
        {
            var go = new GameObject(kind + " Poop");
            go.transform.position = at;
            var poop = go.AddComponent<Poop>();
            poop.Kind = kind;
            poop.Build();
            return poop;
        }

        void Build()
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            switch (Kind)
            {
                case PoopKind.Bird:
                    Part(PrimitiveType.Sphere, body, Vector3.zero, new Vector3(0.09f, 0.14f, 0.09f), BirdWhite, 0.6f);
                    Part(PrimitiveType.Sphere, body, new Vector3(0f, -0.03f, 0f), new Vector3(0.045f, 0.06f, 0.045f), BirdGrey, 0.6f);
                    break;
                case PoopKind.Frog:
                    // One glossy pellet lying on its side.
                    Part(PrimitiveType.Capsule, body, new Vector3(0f, 0.05f, 0f), new Vector3(0.15f, 0.12f, 0.15f), FrogOlive, 0.8f)
                        .localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case PoopKind.Fish:
                    for (int i = 0; i < 7; i++)
                    {
                        var p = new Vector3(Mathf.Sin(i * 0.9f) * 0.03f, 0f, i * 0.055f - 0.17f);
                        Part(PrimitiveType.Capsule, body, p, new Vector3(0.025f, 0.035f, 0.025f), FishBrown, 0.8f)
                            .localRotation = Quaternion.Euler(90f, Mathf.Sin(i * 0.9f) * 25f, 0f);
                    }
                    bubbles = MakeBubbles();
                    break;
            }
        }

        static Transform Part(PrimitiveType shape, Transform parent, Vector3 position, Vector3 scale, Color color, float smoothness)
        {
            var go = GameObject.CreatePrimitive(shape);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var material = go.GetComponent<Renderer>().material;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else material.color = color;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            return go.transform;
        }

        ParticleSystem MakeBubbles()
        {
            var go = new GameObject("Bubbles");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = 1.2f;
            main.startSpeed = 0.25f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startColor = new Color(0.85f, 0.95f, 1f, 0.7f);
            main.gravityModifier = -0.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 10f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            ps.Play();
            return ps;
        }

        void Update()
        {
            if (Landed) return;
            float dt = Time.deltaTime;
            if (Kind == PoopKind.Fish)
            {
                velocity = 0.5f;                                  // sinks slowly, wobbling
                body.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 2f) * 20f, 0f);
            }
            else velocity = Mathf.Min(velocity + 9.81f * dt, 14f); // falls
            float step = velocity * dt;

            Vector3 from = transform.position;
            if (Physics.Raycast(from + Vector3.up * 0.05f, Vector3.down, out var hit, step + 0.1f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                Land(hit.point, hit.normal);
                return;
            }
            float ground = WinWalker.GroundY(from, float.NegativeInfinity);
            if (from.y - step <= ground)
            {
                Land(new Vector3(from.x, ground, from.z), Vector3.up);
                return;
            }
            transform.position = from + Vector3.down * step;
        }

        void Land(Vector3 point, Vector3 normal)
        {
            Landed = true;
            LandPoint = point;
            transform.SetPositionAndRotation(point, Quaternion.FromToRotation(Vector3.up, normal));
            if (bubbles) bubbles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            switch (Kind)
            {
                case PoopKind.Bird:
                    // The dropping splats into a white splotch with a grey middle and a few spatters.
                    Destroy(body.gameObject);
                    body = new GameObject("Splat").transform;
                    body.SetParent(transform, false);
                    Part(PrimitiveType.Cylinder, body, new Vector3(0f, 0.004f, 0f), new Vector3(0.34f, 0.004f, 0.3f), BirdWhite, 0.55f);
                    Part(PrimitiveType.Sphere, body, new Vector3(0.02f, 0.012f, -0.01f), new Vector3(0.12f, 0.025f, 0.1f), BirdGrey, 0.6f);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 72f * Mathf.Deg2Rad + 0.4f;
                        Part(PrimitiveType.Sphere, body, new Vector3(Mathf.Cos(a) * 0.24f, 0.006f, Mathf.Sin(a) * 0.22f),
                            new Vector3(0.05f, 0.01f, 0.05f) * (0.6f + 0.15f * i), BirdWhite, 0.55f);
                    }
                    break;
                case PoopKind.Frog:
                case PoopKind.Fish:
                    body.localPosition = new Vector3(0f, 0.02f, 0f); // settle on the surface
                    break;
            }
            StartCoroutine(Squash());
        }

        System.Collections.IEnumerator Squash()
        {
            // A quick squash-and-settle so the landing reads.
            Vector3 rest = body.localScale;
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                float k = t / 0.25f;
                float s = 1f + Mathf.Sin(k * Mathf.PI) * 0.35f;
                body.localScale = new Vector3(rest.x * s, rest.y / s, rest.z * s);
                yield return null;
            }
            body.localScale = rest;
        }
    }
}
