using UnityEngine;

namespace Survive.Combat
{
    public sealed class ExplosiveImpactEffect : MonoBehaviour
    {
        private Light flash;
        private float born;
        public static void Play(Vector3 position)
        {
            var root = new GameObject("ExplosiveRoundBlast"); root.transform.position = position;
            var effect = root.AddComponent<ExplosiveImpactEffect>(); effect.born = Time.time;
            var particles = root.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.loop = false; main.duration = .15f; main.startLifetime = new ParticleSystem.MinMaxCurve(.15f, .45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3, 8); main.startSize = new ParticleSystem.MinMaxCurve(.06f, .2f);
            main.startColor = new Color(1, .48f, .07f); main.gravityModifier = .5f; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 45) });
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .08f;
            var colors = particles.colorOverLifetime; colors.enabled = true; var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.yellow, 0), new GradientColorKey(new Color(1, .15f, .02f), 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) }); colors.color = gradient;
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = Resources.Load<Material>("Gameplay/ExplosiveFlash");
            effect.flash = root.AddComponent<Light>(); effect.flash.color = new Color(1, .48f, .1f); effect.flash.range = 5; effect.flash.intensity = 4;
            particles.Play(); Destroy(root, 1);
        }
        private void Update() { if (flash != null) flash.intensity = Mathf.Max(0, 4 - (Time.time - born) * 24); }
    }
}
