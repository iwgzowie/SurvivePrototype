using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Survive.Combat
{
    /// <summary>Reutiliza un máximo de 128 efectos cosméticos por escena, con tiempo escalado.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Survive/Combate/Pool de efectos de armas")]
    public sealed class WeaponVfxPool : MonoBehaviour
    {
        private const int Capacity = 128;
        private static readonly Dictionary<int, WeaponVfxPool> ScenePools = new Dictionary<int, WeaponVfxPool>();

        private sealed class Entry
        {
            public GameObject Prefab;
            public GameObject Instance;
            public ParticleSystem[] Systems;
            public Transform Anchor;
            public float ExpiresAt;
            public bool Active;
        }

        private readonly List<Entry> entries = new List<Entry>(Capacity);
        private readonly System.Random random = new System.Random();
        private int sceneHandle;

        public static WeaponVfxPool GetOrCreate(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            if (ScenePools.TryGetValue(scene.handle, out WeaponVfxPool existing) && existing != null)
                return existing;

            var root = new GameObject("WeaponVfxPool");
            SceneManager.MoveGameObjectToScene(root, scene);
            WeaponVfxPool pool = root.AddComponent<WeaponVfxPool>();
            pool.sceneHandle = scene.handle;
            ScenePools[scene.handle] = pool;
            return pool;
        }

        /// <summary>Los efectos sin anchor simulan en mundo; las bocas pueden acompañar al Muzzle.</summary>
        public bool Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float scale, float lifetime,
            Transform anchor = null)
        {
            if (prefab == null || !isActiveAndEnabled || Time.timeScale <= 0f) return false;
            Entry entry = Acquire(prefab);
            if (entry == null) return false;

            entry.Anchor = anchor;
            Transform target = entry.Instance.transform;
            target.SetParent(anchor != null ? anchor : transform, false);
            target.SetPositionAndRotation(position, rotation);
            target.localScale = prefab.transform.localScale * Mathf.Max(.001f, scale);
            entry.ExpiresAt = Time.time + Mathf.Max(.02f, lifetime);
            entry.Active = true;
            entry.Instance.SetActive(true);

            foreach (ParticleSystem system in entry.Systems)
            {
                if (system == null) continue;
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = system.main;
                main.useUnscaledTime = false;
                main.stopAction = ParticleSystemStopAction.None;
                main.loop = false;
                if (anchor == null) main.simulationSpace = ParticleSystemSimulationSpace.World;
                system.useAutoRandomSeed = false;
                system.randomSeed = (uint)random.Next(1, int.MaxValue);
                system.Play(false);
            }
            return true;
        }

        /// <summary>Retira bocas ancladas; los impactos y casquillos ya emitidos terminan en el mundo.</summary>
        public void ReleaseAttached(Transform anchor)
        {
            if (anchor == null) return;
            foreach (Entry entry in entries)
                if (entry.Active && entry.Anchor == anchor) Release(entry);
        }

        private Entry Acquire(GameObject prefab)
        {
            Entry free = null;
            Entry oldest = null;
            foreach (Entry entry in entries)
            {
                if (entry.Instance == null) entry.Active = false;
                if (!entry.Active)
                {
                    if (entry.Prefab == prefab)
                    {
                        free = entry;
                        break;
                    }
                    free ??= entry;
                }
                else if (oldest == null || entry.ExpiresAt < oldest.ExpiresAt) oldest = entry;
            }
            if (free == null)
            {
                if (entries.Count < Capacity)
                {
                    free = new Entry();
                    entries.Add(free);
                }
                else free = oldest;
            }
            if (free == null) return null;
            Release(free);
            if (free.Instance == null || free.Prefab != prefab)
            {
                if (free.Instance != null) Destroy(free.Instance);
                free.Prefab = prefab;
                free.Instance = Instantiate(prefab, transform);
                free.Instance.SetActive(false);
                free.Systems = free.Instance.GetComponentsInChildren<ParticleSystem>(true);
                if (free.Systems.Length == 0)
                {
                    Destroy(free.Instance);
                    free.Instance = null;
                    return null;
                }
            }
            return free;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            foreach (Entry entry in entries)
                if (entry.Active && (entry.Instance == null || Time.time >= entry.ExpiresAt)) Release(entry);
        }

        private void Release(Entry entry)
        {
            if (entry.Systems != null)
                foreach (ParticleSystem system in entry.Systems)
                    if (system != null) system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (entry.Instance != null)
            {
                entry.Instance.SetActive(false);
                entry.Instance.transform.SetParent(transform, false);
            }
            entry.Active = false;
            entry.Anchor = null;
        }

        private void OnDisable()
        {
            foreach (Entry entry in entries) Release(entry);
        }

        private void OnDestroy()
        {
            if (ScenePools.TryGetValue(sceneHandle, out WeaponVfxPool cached) && cached == this)
                ScenePools.Remove(sceneHandle);
            foreach (Entry entry in entries)
                if (entry.Instance != null) Destroy(entry.Instance);
            entries.Clear();
        }
    }
}
