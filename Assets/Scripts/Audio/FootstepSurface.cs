using UnityEngine;

public enum FootstepSurfaceType
{
    Mud,
    Tile,
    Concrete
}

[DisallowMultipleComponent]
public class FootstepSurface : MonoBehaviour
{
    public FootstepSurfaceType surfaceType = FootstepSurfaceType.Concrete;

    public static FootstepSurfaceType Resolve(Collider collider, FootstepSurfaceType fallback)
    {
        var marker = collider != null ? collider.GetComponentInParent<FootstepSurface>() : null;
        if (marker == null) return fallback;
        switch (marker.surfaceType)
        {
            case FootstepSurfaceType.Mud:
            case FootstepSurfaceType.Tile:
            case FootstepSurfaceType.Concrete: return marker.surfaceType;
            default: return fallback;
        }
    }
}
