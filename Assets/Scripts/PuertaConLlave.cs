using Survive.Items;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections;
using System.Linq;

/// <summary>Conserva la barrera física hasta que el jugador usa la llave con Interactuar.</summary>
[DisallowMultipleComponent]
public class PuertaConLlave : MonoBehaviour, IWorldInteractable, IWorldInteractionColliderGroup, IPlayerInteractionPrompt
{
    [Header("Efectos de apertura")]
    [SerializeField] private GameObject objectdisappearingeffect;
    [SerializeField] private AudioClip openSound;

    [Header("Reglas")]
    [SerializeField] private bool keyConsume = true;
    [SerializeField, HideInInspector] private bool destroyObject = false; // Campo legado: abrir ya no destruye el modelo.
    [Header("Animación de apertura")]
    [SerializeField] private Vector3 openingTranslation = new Vector3(0, 5, 0);
    [SerializeField, Min(.1f)] private float openingDuration = 1.4f;
    [SerializeField, Min(0)] private int guideOrder;
    [Tooltip("Barreras adicionales del mismo paso que se retiran al desbloquear.")]
    [SerializeField] private GameObject[] blockingObjects = new GameObject[0];
    [Tooltip("Estas puertas deben estar abiertas antes de permitir esta apertura.")]
    [SerializeField] private PuertaConLlave[] prerequisiteDoors = new PuertaConLlave[0];
    [Tooltip("Puertas que se abren juntas y consumen una sola llave en total. Puede vincularse en ambos sentidos.")]
    [SerializeField] private PuertaConLlave[] linkedDoors = new PuertaConLlave[0];
    private bool opening;
    public static event Action<PuertaConLlave> Opened;
    public int GuideOrder => guideOrder;
    public bool IsAnimating { get; private set; }
    public bool CanUnlockStage => !IsOpen && isActiveAndEnabled && OpeningGroup().All(d => !d.IsOpen && d.PrerequisiteProblem() == null);
    public bool OpensTogether(PuertaConLlave door) => OpeningGroup().Contains(door);
    public Renderer[] GuideRenderers => GetComponentsInChildren<Renderer>(true)
        .Concat(blockingObjects.Where(b => b != null).SelectMany(b => b.GetComponentsInChildren<Renderer>(true)))
        .Distinct().ToArray();
    public GameObject[] BlockingObjects => blockingObjects;

    private void Awake()
    {
        AddNavigationBarriers(gameObject);
        foreach (var barrier in blockingObjects) if (barrier != null) AddNavigationBarriers(barrier);
    }

    private static void AddNavigationBarriers(GameObject root)
    {
        foreach (var collider in root.GetComponentsInChildren<Collider>(true))
        {
            if (collider.isTrigger || !collider.enabled) continue;
            if (collider.GetComponent<NavMeshObstacle>() != null) continue;
            var obstacle = collider.gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            if (collider is BoxCollider box) { obstacle.center = box.center; obstacle.size = box.size; }
            else if (collider is MeshCollider mesh && mesh.sharedMesh != null)
            { obstacle.center = mesh.sharedMesh.bounds.center; obstacle.size = mesh.sharedMesh.bounds.size; }
            else
            {
                var bounds = collider.bounds;
                obstacle.center = collider.transform.InverseTransformPoint(bounds.center);
                Vector3 size = collider.transform.InverseTransformVector(bounds.size);
                obstacle.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            }
            // Un collider plano también necesita un volumen para cortar el NavMesh.
            obstacle.size = Vector3.Max(obstacle.size, Vector3.one * .15f);
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
        }
    }

    public bool IsOpen { get; private set; }
    public string InteractionPrompt => PrerequisiteProblem() ?? "[E] Desbloquear " + name + " · 1 llave";
    public string GetInteractionPrompt(GameObject player)
    {
        foreach (PuertaConLlave member in OpeningGroup())
        {
            string problem = member.PrerequisiteProblem(); if (problem != null) return problem;
        }
        PlayerKeyInventory keys = PlayerKeyInventory.ForPlayer(player);
        return keys != null && keys.HasKey ? "[E] Desbloquear " + name + " · 1 llave" : "Necesitás una llave del boss";
    }

    private string PrerequisiteProblem()
    {
        foreach (PuertaConLlave required in prerequisiteDoors)
            if (!ReferenceEquals(required, null) && !required.IsOpen)
                return required != null ? "Primero abrí " + required.name : "Primero desbloqueá la zona anterior";
        return null;
    }

    private List<PuertaConLlave> OpeningGroup()
    {
        var group = new List<PuertaConLlave>(); var pending = new Stack<PuertaConLlave>(); var visited = new HashSet<PuertaConLlave>(); pending.Push(this);
        while (pending.Count > 0)
        {
            PuertaConLlave item = pending.Pop(); if (item == null || !visited.Add(item)) continue; group.Add(item);
            foreach (PuertaConLlave linked in item.linkedDoors) if (linked != null) pending.Push(linked);
        }
        return group;
    }

    public bool CanInteract(GameObject player)
    {
        PlayerHealth health = player != null ? player.GetComponentInParent<PlayerHealth>() : null;
        return isActiveAndEnabled && !IsOpen && !opening && Time.timeScale > 0f && health != null && !health.IsDead;
    }

    public bool OwnsCollider(Collider collider)
    {
        if (collider == null) return false;
        if (collider.transform.IsChildOf(transform)) return true;
        foreach (GameObject barrier in blockingObjects)
            if (barrier != null && collider.transform.IsChildOf(barrier.transform)) return true;
        return false;
    }

    public bool Interact(GameObject player)
    {
        if (!CanInteract(player)) return false;
        List<PuertaConLlave> group = OpeningGroup(); bool consume = false;
        foreach (PuertaConLlave member in group)
        {
            if (member.IsOpen || member.opening || member.PrerequisiteProblem() != null) return false;
            consume |= member.keyConsume;
        }
        PlayerKeyInventory keys = PlayerKeyInventory.ForPlayer(player);
        if (keys == null || !keys.HasKey) return false;
        foreach (PuertaConLlave member in group) member.opening = true;
        try
        {
            if (consume && !keys.TryConsumeKey()) return false;
            // Publicar todo el grupo antes de ocultar objetos o ejecutar efectos.
            foreach (PuertaConLlave member in group) member.IsOpen = true;
            Opened?.Invoke(this);
            foreach (PuertaConLlave member in group) member.OpenVisuals();
            return true;
        }
        finally { foreach (PuertaConLlave member in group) member.opening = false; }
    }

    private void OpenVisuals()
    {
        if (openSound != null) AudioSource.PlayClipAtPoint(openSound, transform.position);
        if (objectdisappearingeffect != null) Instantiate(objectdisappearingeffect, transform.position, Quaternion.identity);
        StartCoroutine(AnimateOpen());
    }

    private IEnumerator AnimateOpen()
    {
        IsAnimating = true;
        var candidates = new List<Transform> { transform };
        candidates.AddRange(blockingObjects.Where(b => b != null).Select(b => b.transform));
        var roots = candidates.Distinct().Where(t => !candidates.Any(other => other != t && t.IsChildOf(other))).ToArray();
        var starts = roots.Select(t => t.position).ToArray();
        float elapsed = 0;
        while (elapsed < openingDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / Mathf.Max(.1f, openingDuration)));
            for (int i = 0; i < roots.Length; i++) if (roots[i] != null) roots[i].position = starts[i] + openingTranslation * t;
            yield return null;
        }
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null) continue;
            roots[i].position = starts[i] + openingTranslation;
            foreach (var obstacle in roots[i].GetComponentsInChildren<NavMeshObstacle>(true)) obstacle.enabled = false;
        }
        IsAnimating = false;
    }
}
