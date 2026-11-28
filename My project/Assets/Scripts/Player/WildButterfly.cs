using UnityEngine;

/// <summary>
/// A wild butterfly that wanders around its home patch until the player pairs with it. While pairing it
/// circles the player; afterwards it flies off and disappears. Created and driven by ButterflySwarm.
/// </summary>
public sealed class WildButterfly : MonoBehaviour
{
    const float Speed = 2.4f;

    Vector3 home, target;
    float bobPhase;
    float radius;
    Transform partner;
    bool leaving;

    /// <summary>True once the player has paired with it.</summary>
    public bool Taken { get; private set; }

    public void Init(Vector3 homePoint, float wanderRadius)
    {
        home = homePoint;
        radius = wanderRadius;
        bobPhase = Random.value * 10f;
        PickTarget();
        transform.position = target;
    }

    public void Pair(Transform with)
    {
        Taken = true;
        partner = with;
    }

    public void Leave()
    {
        partner = null;
        leaving = true;
        Destroy(gameObject, 4f);
    }

    void Update()
    {
        Vector3 goal;
        float speed = Speed;
        if (partner != null)
        {
            float a = Time.time * 4f;
            goal = partner.position + new Vector3(Mathf.Cos(a), 0.3f, Mathf.Sin(a)) * 1.1f;
            speed *= 3f;
        }
        else if (leaving)
        {
            goal = transform.position + (transform.forward + Vector3.up) * 5f;
            speed *= 1.5f;
        }
        else
        {
            if ((target - transform.position).sqrMagnitude < 0.5f) PickTarget();
            goal = target;
        }

        Vector3 step = Vector3.MoveTowards(transform.position, goal, speed * Time.deltaTime) - transform.position;
        step.y += Mathf.Sin(Time.time * 5f + bobPhase) * 0.4f * Time.deltaTime; // fluttering bob
        transform.position += step;
        Vector3 flat = new Vector3(step.x, 0f, step.z);
        if (flat.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 6f * Time.deltaTime);
    }

    void PickTarget()
    {
        Vector2 r = Random.insideUnitCircle * radius;
        var p = home + new Vector3(r.x, 0f, r.y);
        p.y = WinWalker.GroundY(p, home.y) + Random.Range(1.5f, 4f);
        target = p;
    }
}
