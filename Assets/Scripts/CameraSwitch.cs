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

    [Header("Transición")]
    [SerializeField] private float canvasDelay = 0.75f;

    private Coroutine transitionCoroutine;

    public void ShowMainMenu()
    {
        mainMenuCamera.Priority = 10;
        characterSelectCamera.Priority = 0;

        StartCanvasTransition(mainMenuCanvas, characterSelectCanvas);
    }

    public void ShowCharacterSelect()
    {
        mainMenuCamera.Priority = 0;
        characterSelectCamera.Priority = 10;

        StartCanvasTransition(characterSelectCanvas, mainMenuCanvas);
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