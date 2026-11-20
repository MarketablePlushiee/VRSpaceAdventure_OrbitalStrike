using UnityEngine;

/// <summary>
/// Controla el impacto de cada proyectil láser contra dianas, minas o núcleos del jefe,
/// además de la asistencia teledirigida cuando el potenciador está activo.
/// </summary>
public class LaserProjectile : MonoBehaviour
{
    public bool isPiercing = false;
    public bool isHoming = false;
    public float homingTurnSpeed = 8f;

    private DiegeticWeapon ownerWeapon;
    private Rigidbody rb;
    private Transform homingTarget;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ownerWeapon = FindFirstObjectByType<DiegeticWeapon>();
    }

    private void Start()
    {
        if (isHoming)
        {
            FindNearestTarget();
        }
    }

    private void FixedUpdate()
    {
        if (isHoming && homingTarget != null && rb != null)
        {
            Vector3 dirToTarget = (homingTarget.position - transform.position).normalized;
            float speed = rb.linearVelocity.magnitude;
            Vector3 newDir = Vector3.RotateTowards(rb.linearVelocity.normalized, dirToTarget, homingTurnSpeed * Time.fixedDeltaTime, 0f);
            rb.linearVelocity = newDir * speed;
            transform.rotation = Quaternion.LookRotation(newDir);
        }
    }

    private void FindNearestTarget()
    {
        TargetObjective[] targets = FindObjectsByType<TargetObjective>(FindObjectsSortMode.None);
        float closestDist = 120f;
        foreach (var t in targets)
        {
            if (t.objectiveType == TargetObjective.ObjectiveType.Asteroid) continue;
            float dist = Vector3.Distance(transform.position, t.transform.position);
            // Solo busca objetivos que estén por delante del disparo
            if (dist < closestDist && t.transform.position.z > transform.position.z)
            {
                closestDist = dist;
                homingTarget = t.transform;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TargetObjective target = other.GetComponentInParent<TargetObjective>();
        if (target != null)
        {
            target.TakeDamage(1, ownerWeapon);
            if (!isPiercing)
            {
                Destroy(gameObject);
            }
            return;
        }
    }
}