using UnityEngine;

/// <summary>Los eventos llegan al objeto del Animator; la fuente 3D pertenece al enemigo raíz.</summary>
[DisallowMultipleComponent]
public sealed class ZombieFootstepAnimationEvents : MonoBehaviour
{
    ZombieFootstepAudio owner;
    public void Bind(ZombieFootstepAudio audio) => owner = audio;
    public void Footstep(AnimationEvent animationEvent)
    {
        // En un BlendTree sólo emite la locomoción dominante.
        if (animationEvent.isFiredByAnimator && animationEvent.animatorClipInfo.weight < .5f) return;
        if (owner == null) owner = GetComponentInParent<ZombieFootstepAudio>();
        if (owner != null) owner.Footstep();
    }
}
