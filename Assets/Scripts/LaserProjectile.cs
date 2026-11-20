using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Proyectil láser con detección continua SphereCast (ignorando la propia cabina),
/// soporte para Disparo Perforante y Disparo Teledirigido.
/// </summary>
public class LaserProjectile : MonoBehaviour
{
    [HideInInspector] public DiegeticWeapon ownerWeapon;
    [HideInInspector] public Transform ignoredRoot;
    public bool isPiercing = false;
    public bool isHoming = false;
    public float homingTurnSpeed = 8f;
    public float homingSearchRadius = 25f;
    public float hitRadius = 0.25f;

    private Rigidbody rb;
    private Vector3 lastPosition;
    private readonly HashSet<TargetObjective> hitTargets = new HashSet<TargetObjective>();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        lastPosition = transform.position;
    }

    private void FixedUpdate()
    {
        if (isHoming && rb != null)
        {
            SteerTowardNearestTarget();
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
        }

        return false;
    }

    private void SteerTowardNearestTarget()
    {
        TargetObjective[] allTargets = FindObjectsByType<TargetObjective>(FindObjectsSortMode.None);
        TargetObjective best = null;
        float bestDist = homingSearchRadius;

        foreach (TargetObjective t in allTargets)
        {
            if (t == null || t.objectiveType == TargetObjective.ObjectiveType.Asteroid || hitTargets.Contains(t))
                continue;

            Vector3 toTarget = t.transform.position - transform.position;
            float d = toTarget.magnitude;
            if (d < bestDist && Vector3.Dot(transform.forward, toTarget.normalized) > 0.35f)
            {
                bestDist = d;
                best = t;
            }
        }

        if (best != null)
        {
            Vector3 desiredDir = (best.transform.position - transform.position).normalized;
            float speed = rb.linearVelocity.magnitude;
            Vector3 newDir = Vector3.Slerp(rb.linearVelocity.normalized, desiredDir, Time.fixedDeltaTime * homingTurnSpeed);
            rb.linearVelocity = newDir * speed;
            transform.rotation = Quaternion.LookRotation(newDir);
        }
    }
}