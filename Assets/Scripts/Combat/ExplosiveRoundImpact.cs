using System.Collections.Generic;
using Survive.Items;
using UnityEngine;

namespace Survive.Combat
{
    public static class ExplosiveRoundImpact
    {
        /// <summary>Una explosión por bala (también escopeta), un daño por enemigo y paredes que bloquean la onda.</summary>
        public static bool Detonate(RaycastHit impact, ExplosiveAmmoDefinition ammunition, Transform owner)
        {
            if (impact.collider == null || ammunition == null) return false;
            Vector3 center = impact.point + impact.normal * .06f;
            if (ammunition.impactEffectPrefab != null)
            {
                UnityEngine.SceneManagement.Scene scene = owner != null ? owner.gameObject.scene : impact.collider.gameObject.scene;
                WeaponVfxPool.GetOrCreate(scene)?.Spawn(ammunition.impactEffectPrefab, center,
                    Quaternion.FromToRotation(Vector3.up, impact.normal), ammunition.impactEffectScale, ammunition.impactEffectLifetime);
            }
            else ExplosiveImpactEffect.Play(center);
            var damaged = new HashSet<EnemyHealth>();
            EnemyHealth direct = impact.collider.GetComponentInParent<EnemyHealth>();
            if (direct != null && !direct.IsDead)
            {
                direct.TakeExplosiveDamage(ammunition.damage, center, ammunition.impulse, owner != null ? owner.gameObject : null); damaged.Add(direct);
            }
            foreach (Collider collider in Physics.OverlapSphere(center, ammunition.radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (owner != null && collider.transform.IsChildOf(owner)) continue;
                EnemyHealth enemy = collider.GetComponentInParent<EnemyHealth>();
                if (enemy == null || enemy.IsDead || damaged.Contains(enemy)) continue;
                Vector3 point = collider.ClosestPoint(center); Vector3 direction = point - center;
                bool blocked = false;
                foreach (RaycastHit obstacle in Physics.RaycastAll(center, direction.normalized, direction.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    if (!obstacle.transform.IsChildOf(enemy.transform)
                        && obstacle.collider.GetComponentInParent<EnemyHealth>() == null
                        && (owner == null || !obstacle.transform.IsChildOf(owner))) { blocked = true; break; }
                if (blocked) continue;
                damaged.Add(enemy);
                float falloff = Mathf.Lerp(1, .35f, Mathf.Clamp01(direction.magnitude / ammunition.radius));
                enemy.TakeExplosiveDamage(ammunition.damage * falloff, center, ammunition.impulse * falloff, owner != null ? owner.gameObject : null);
            }
            return damaged.Count > 0;
        }
    }
}
