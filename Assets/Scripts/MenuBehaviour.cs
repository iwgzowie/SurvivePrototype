using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MenuBehaviour : MonoBehaviour
{

    public GameObject container;

    void Start()
    {
        container.SetActive(false);
        Time.timeScale = 1;
    }

    void Update()
    {
     //   if (Input.GetKeyDown(KeyCode.Escape))    <-------- no me quiere tomar esto en el nuevo input system, lo dejo por si uds si lo usan
     //   {                                                  Si no se usa, borrar sin drama.
     //       container.SetActive(!container.activeSelf);
     //   }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (container.activeSelf)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }


    // Funcion para cambio de escena - modificar valor en el inspector!
    public void ChangeScene(string name)
    {
        SceneManager.LoadScene(name);
    }
    
    // Función para cerrar el juego 
    public void ExitGame()
    {
        Debug.Log("Game Closed"); // Para chequear que reacciona el botón, no tengo otra manera de corroborarlo.
        Application.Quit();
    }

    // Función para abrir menu de pausa y pausar el tiempo.
    void PauseGame()
    {
        container.SetActive (true);
        Time.timeScale = 0;
    }

    // Función para cerrar el menu de pausa y reanudar el tiempo.
    void ResumeGame()
    {
        container.SetActive(false);
        Time.timeScale = 1;
    }
}
