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
}