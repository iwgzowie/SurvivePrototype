using UnityEngine;

namespace UPP.Runtime.Aiming
{
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    [AddComponentMenu("UPP/Aim/Trace central de cámara")]
    public sealed class UPPAimTrace : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Camera sourceCamera;
        [SerializeField, Tooltip("Jerarquía que se excluye del trace para no impactar al propio jugador.")]
        private Transform ignoredRoot;
        [SerializeField, Min(1f)] private float maximumDistance = 500f;
        [SerializeField] private LayerMask hitMask = ~0;
        public bool HasHit { get; private set; }
        public RaycastHit LastHit { get; private set; }
        public Vector3 AimPoint { get; private set; }
        public Ray LastRay { get; private set; }

        private readonly RaycastHit[] hits = new RaycastHit[24];

        private void Awake()
        {
            sourceCamera ??= GetComponent<UnityEngine.Camera>();
            sourceCamera ??= UnityEngine.Camera.main;
        }

        private void LateUpdate()
        {
            RefreshTrace();
        }

        public void Configure(UnityEngine.Camera playerCamera, Transform rootToIgnore = null)
        {
            sourceCamera = playerCamera;
            ignoredRoot = rootToIgnore;
        }

        public void RefreshTrace()
        {
            if (sourceCamera == null)
            {
                HasHit = false;
                LastHit = default;
                LastRay = default;
                AimPoint = Vector3.zero;
                return;
            }

            LastRay = sourceCamera.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f));
            int count = Physics.RaycastNonAlloc(
                LastRay,
                hits,
                maximumDistance,
                hitMask,
                QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            RaycastHit selected = default;

            for (int index = 0; index < count; index++)
            {
                RaycastHit candidate = hits[index];
                if (candidate.collider == null
                    || (ignoredRoot != null
                        && candidate.transform.IsChildOf(ignoredRoot)))
                {
                    continue;
                }

                if (candidate.distance < nearest)
                {
                    nearest = candidate.distance;
                    selected = candidate;
                }
            }

            HasHit = nearest < float.PositiveInfinity;
            LastHit = selected;
            AimPoint = HasHit
                ? selected.point
                : LastRay.origin + LastRay.direction * maximumDistance;

        }
    }
}
