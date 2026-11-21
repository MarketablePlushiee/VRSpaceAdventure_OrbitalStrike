using System.Collections.Generic;
using UnityEngine;

public class LaserProjectile : MonoBehaviour
{
    [HideInInspector] public DiegeticWeapon ownerWeapon;
    [HideInInspector] public Transform ignoredRoot;
    [HideInInspector] public Transform softAssistTarget;
    public bool isPiercing = false;
    public bool isHoming = false;

    [Header("Ajustes de Teledirigido (Power-Up)")]
    public float homingTurnRadiansPerSec = 14f;
    public float homingSearchRadius = 90f;
    public float magnetSnapRadius = 2.2f;
    public float hitRadius = 0.3f;

    private Rigidbody rb;
    private Vector3 lastPosition;
    private readonly HashSet<Component> hitTargets = new HashSet<Component>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
        lastPosition = transform.position;
    }

    private void FixedUpdate()
    {
        if (rb != null)
        {
            if (isHoming)
            {
                SteerTowardNearestTarget();
            }
            else if (softAssistTarget != null && softAssistTarget.gameObject.activeInHierarchy)
            {
                ApplySubtleAimAssist();
            }
        }

        Vector3 step = transform.position - lastPosition;
        float dist = step.magnitude;
        if (dist > 0.001f)
        {
            RaycastHit[] hits = Physics.SphereCastAll(lastPosition, hitRadius, step.normalized, dist, ~0, QueryTriggerInteraction.Collide);
            foreach (RaycastHit hit in hits)
            {
                if (ProcessHitCollider(hit.collider))
                {
                    return;
                }
            }
        }

        lastPosition = transform.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        ProcessHitCollider(other);
    }

    private void ApplySubtleAimAssist()
    {
        Vector3 toTarget = softAssistTarget.position - transform.position;
        float dist = toTarget.magnitude;
        float speed = rb.linearVelocity.magnitude;
        if (speed < 0.1f || dist < 0.05f) return;

        // Corrección suave en vuelo si el objetivo se desplaza levemente
        Vector3 currentDir = rb.linearVelocity / speed;
        if (Vector3.Dot(currentDir, toTarget / dist) > 0.85f)
        {
            Vector3 newDir = Vector3.RotateTowards(currentDir, toTarget / dist, 2.2f * Time.fixedDeltaTime, 0f);
            rb.linearVelocity = newDir * speed;
            transform.rotation = Quaternion.LookRotation(newDir);

            if (dist <= 0.9f)
            {
                rb.MovePosition(Vector3.MoveTowards(transform.position, softAssistTarget.position, speed * Time.fixedDeltaTime));
            }
        }
    }

    private bool ProcessHitCollider(Collider other)
    {
        if (other == null) return false;
        if (ignoredRoot != null && other.transform.IsChildOf(ignoredRoot)) return false;

        VRConsoleButton btn = other.GetComponentInParent<VRConsoleButton>();
        if (btn != null)
        {
            btn.ExecuteAction();
            Destroy(gameObject);
            return true;
        }

        TargetObjective target = other.GetComponentInParent<TargetObjective>();
        if (target != null && !hitTargets.Contains(target))
        {
            hitTargets.Add(target);
            target.TakeDamage(1, ownerWeapon);

            if (!isPiercing)
            {
                Destroy(gameObject);
                return true;
            }
            return false;
        }

        BossCore core = other.GetComponentInParent<BossCore>();
        if (core != null && !core.IsDestroyed && !hitTargets.Contains(core))
        {
            hitTargets.Add(core);
            core.TakeCoreDamage(50, ownerWeapon);

            if (!isPiercing)
            {
                Destroy(gameObject);
                return true;
            }
            return false;
        }

        if (!other.isTrigger)
        {
            Destroy(gameObject);
            return true;
        }

        return false;
    }

    private void SteerTowardNearestTarget()
    {
        Transform bestTarget = null;
        float bestDist = homingSearchRadius;
        Vector3 currentVel = rb.linearVelocity;
        float speed = currentVel.magnitude;
        if (speed < 0.1f) return;

        Vector3 currentDir = currentVel / speed;

        TargetObjective[] allTargets = FindObjectsByType<TargetObjective>(FindObjectsSortMode.None);
        foreach (TargetObjective t in allTargets)
        {
            if (t == null || t.objectiveType == TargetObjective.ObjectiveType.Asteroid || hitTargets.Contains(t))
                continue;

            Vector3 toTarget = t.transform.position - transform.position;
            float d = toTarget.magnitude;
            if (d < bestDist && Vector3.Dot(currentDir, toTarget.normalized) > 0.1f)
            {
                bestDist = d;
                bestTarget = t.transform;
            }
        }

        if (bestTarget == null)
        {
            BossCore[] activeCores = FindObjectsByType<BossCore>(FindObjectsSortMode.None);
            foreach (BossCore c in activeCores)
            {
                if (c == null || c.IsDestroyed || hitTargets.Contains(c))
                    continue;

                Vector3 toCore = c.transform.position - transform.position;
                float d = toCore.magnitude;
                if (d < homingSearchRadius * 1.3f && Vector3.Dot(currentDir, toCore.normalized) > 0.1f)
                {
                    bestDist = d;
                    bestTarget = c.transform;
                }
            }
        }

        if (bestTarget != null)
        {
            Vector3 desiredDir = (bestTarget.position - transform.position).normalized;
            Vector3 newDir = Vector3.RotateTowards(currentDir, desiredDir, homingTurnRadiansPerSec * Time.fixedDeltaTime, 0f);
            rb.linearVelocity = newDir * speed;
            transform.rotation = Quaternion.LookRotation(newDir);

            if (bestDist <= magnetSnapRadius)
            {
                rb.MovePosition(Vector3.MoveTowards(transform.position, bestTarget.position, speed * Time.fixedDeltaTime));
            }
        }
    }
}