using UnityEngine;
using Cinemachine;
using System.Collections;

public class CameraSwitch : MonoBehaviour
{
    [Header("Cámaras")]
    [SerializeField] private CinemachineVirtualCamera mainMenuCamera;
    [SerializeField] private CinemachineVirtualCamera characterSelectCamera;

    [Header("Canvas")]
    [SerializeField] private GameObject mainMenuCanvas;
    [SerializeField] private GameObject characterSelectCanvas;
    [SerializeField] private MenuBehaviour characterMenu;

    [Header("Transición")]
    [SerializeField] private float canvasDelay = 0.75f;

    private Coroutine transitionCoroutine;
    private Transform agentAnchor;
    private int framedWidth, framedHeight;

    public void ShowMainMenu()
    {
        agentAnchor = null;
        mainMenuCamera.Priority = 10;
        characterSelectCamera.Priority = 0;

        if (characterMenu == null) StartCanvasTransition(mainMenuCanvas, characterSelectCanvas);
    }

    public void ShowCharacterSelect()
    {
        if (characterMenu != null)
        {
            characterMenu.OpenAgentCreator();
            return;
        }
        FocusAgent();
        StartCanvasTransition(characterSelectCanvas, mainMenuCanvas);
    }

    public void FocusAgent(Transform anchor = null)
    {
        agentAnchor = anchor;
        if (agentAnchor != null) FrameAgent();
        mainMenuCamera.Priority = 0;
        characterSelectCamera.Priority = 10;
    }

    private void LateUpdate()
    {
        if (agentAnchor != null && (Screen.width != framedWidth || Screen.height != framedHeight)) FrameAgent();
    }

    private void FrameAgent()
    {
        framedWidth = Screen.width;
        framedHeight = Screen.height;
        // Un volumen fijo conserva el encuadre al cambiar cuerpo, ropa o girar.
        // El tercio derecho queda reservado para el formulario del creador.
        const float radius = 1.15f, height = 2.25f;
        var viewport = new Rect(.055f, .12f, .56f, .82f);
        float aspect = Mathf.Max(.1f, (float)framedWidth / Mathf.Max(1, framedHeight));
        float tangent = Mathf.Tan(characterSelectCamera.m_Lens.FieldOfView * .5f * Mathf.Deg2Rad);
        float pitch = Mathf.Asin(Mathf.Clamp(Mathf.Abs(characterSelectCamera.transform.forward.y), 0, 1));
        float vertical = height * .5f * Mathf.Cos(pitch) + radius * Mathf.Sin(pitch);
        float depth = radius * Mathf.Cos(pitch) + height * .5f * Mathf.Sin(pitch);
        float distance = Mathf.Max(vertical / (tangent * viewport.height),
            radius / (tangent * aspect * viewport.width)) * 1.04f + depth;
        float viewHeight = 2 * distance * tangent;
        Transform view = characterSelectCamera.transform;
        Vector3 focus = agentAnchor.position + Vector3.up * (height * .5f);
        view.position = focus - view.forward * distance
            + view.right * ((.5f - viewport.center.x) * viewHeight * aspect)
            + view.up * ((.5f - viewport.center.y) * viewHeight);
    }

    private void StartCanvasTransition(GameObject canvasToShow, GameObject canvasToHide)
    {
        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        transitionCoroutine = StartCoroutine(CanvasTransition(canvasToShow, canvasToHide));
    }

    private IEnumerator CanvasTransition(GameObject canvasToShow, GameObject canvasToHide)
    {
        yield return new WaitForSeconds(canvasDelay);

        canvasToHide.SetActive(false);
        canvasToShow.SetActive(true);

        transitionCoroutine = null;
    }
}
