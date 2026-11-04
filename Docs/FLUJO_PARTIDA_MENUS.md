# Flujo de partida, menús, derrota y victoria

Fecha de implementación: 13/09/2026. Unity 6000.3.5f2.

Esta guía describe los ajustes de gameplay y la incorporación posterior de
oleadas y victoria. La validación de los cinco ajustes iniciales está al final;
sus resultados no se extienden automáticamente al spawner y al cierre por
victoria, cuya integración se documenta en [Oleadas](OLEADAS.md).

## Cinco frentes de integración

| Frente | Implementación |
| --- | --- |
| Entrada y escenas | SceneAlejandro es la entrada de Build Settings; StartGame abre el creador Sidekick si falta un perfil válido y carga Level 1 directamente si ya existe. Playground continúa como escena de trabajo. |
| Menú, pausa y opciones | MenuBehaviour coordina botones, Esc/Start de gamepad, tiempo y cursor. MenuOptions conecta volumen, resolución y pantalla completa a los controles existentes. |
| Fin de partida y reintento | La muerte muestra derrota mediante PlayerHealth y PlayerDeathController. Eliminar la última oleada con el jugador vivo muestra victoria. Ambos paneles permiten reintentar o volver al menú. |
| Enemigos y configuración | EnemySpawner genera oleadas configurables. Las variantes conservan los valores del Inspector; el ataque a distancia comprueba alcance y obstáculos. La patrulla admite los puntos asignados cuando useRandomPatrol está apagado. |
| Interacción con el arma | PlayerInteractionController selecciona con raycast, muestra un aviso y recoge con E dentro del alcance configurable. El contacto ya no equipa automáticamente. |

EnemySpawner y EnemyWave controlan la secuencia de enemigos. Cada oleada
termina cuando se generó su composición y se eliminó a todos sus enemigos;
completar la última con el jugador vivo llama a ShowVictory(). Los parámetros
y las reglas de conteo están en [Oleadas](OLEADAS.md).

## Escenas y assets

- [SceneAlejandro](<D:/SurvivePrototype/Assets/Scenes/SceneAlejandro.unity>): menú principal.
- [Level 1](<D:/SurvivePrototype/Assets/Scenes/Level 1.unity>): escena de partida con mapa, jugador, rifle y enemigos.
- [EditorBuildSettings.asset](<D:/SurvivePrototype/ProjectSettings/EditorBuildSettings.asset>):
  SceneAlejandro primero y Level 1 segundo, ambos habilitados.
- [UPPPlayer.prefab](<D:/SurvivePrototype/Assets/Prefabs/Player/UPPPlayer.prefab>): locomoción UPP,
  arma, interacción, salud, respuesta a muerte y Canvas InteractionPrompt.
- [PauseMenuCanvas.prefab](<D:/SurvivePrototype/Assets/Gustavo UI/PauseMenuCanvas.prefab>):
  pausa, GameOverPanel y VictoryPanel, coordinados por MenuBehaviour.

~~~text
SceneAlejandro
  -> Iniciar / MenuBehaviour.StartGame
      -> Sin perfil: creador de personaje Sidekick
          -> Cancelar: volver al menú sin guardar
          -> Crear y jugar: validar y guardar perfil, luego Level 1
      -> Con perfil válido: Level 1 directamente
      -> Level 1
          -> E: recoger arma visible y cercana
          -> RMB: aim; LMB: disparo
          -> Esc: pausa
              -> Reanudar: continuar la partida
              -> Volver al menú: SceneAlejandro
          -> EnemySpawner: completar todas las oleadas
              -> MenuBehaviour.ShowVictory
                  -> Reintentar: recargar Level 1
                  -> Volver al menú: SceneAlejandro
          -> PlayerHealth.OnDeath
              -> PlayerDeathController
                  -> MenuBehaviour.ShowGameOver
                      -> Reintentar: recargar Level 1
                      -> Volver al menú: SceneAlejandro
~~~

Salir invoca Application.Quit(). El cierre real de la aplicación debe
comprobarse en el ejecutable; no se toma la falta de cierre del Editor como
una falla del botón.

## Menú principal y pausa

[MenuBehaviour.cs](<D:/SurvivePrototype/Assets/Scripts/MenuBehaviour.cs>) debe quedar en un objeto
que permanezca activo mientras su panel de pausa se muestra u oculta.
container apunta al contenido de pausa, gameOverContainer a derrota y
victoryContainer a victoria. El menú principal usa isMainMenu activado; no
necesita container.

Los botones de navegación usan StartGame(), BackToMainMenu(),
ReloadCurrentScene(), ResumeGame() y ExitGame(). gameplayScene es Level 1 y
mainMenuScene es SceneAlejandro. ChangeScene(sceneName) rechaza nombres vacíos
o escenas fuera de la configuración de build antes de intentar cargarlas.

En el menú principal, `StartGame()` consulta `CharacterProfileStore` y el
catálogo Sidekick. El primer Play abre `CharacterCreationMenu`; confirmar
valida las partes, guarda `character-profile.json` en
`Application.persistentDataPath` y entra a la partida. Cancelar no guarda.
Los siguientes Play cargan la partida directamente. Un perfil dañado intenta
recuperarse del respaldo `.bak`; si no hay un perfil válido, se vuelve al
creador. El guardado de personaje es independiente de las opciones de pantalla
y volumen. Ver [Customización de personaje](PERSONAJES_CUSTOMIZACION.md).

Esc, o Start en gamepad, alterna pausa durante una partida activa. La pausa
conserva la escala de tiempo anterior, usa Time.timeScale = 0 y libera el
cursor. Reanudar restaura la escala y espera soltar el botón izquierdo antes
de bloquear otra vez el cursor, para evitar usar el clic del menú como disparo.

Durante derrota o victoria no se puede reanudar con Esc. IsFinished engloba
ambos resultados y evita que uno reemplace al otro. Antes de cargar otra escena, el
menú restablece Time.timeScale = 1. Los objetos de UI necesitan un EventSystem
con InputSystemUIInputModule para los controles de New Input System.

## Opciones existentes

[MenuOptions.cs](<D:/SurvivePrototype/Assets/Scripts/MenuOptions.cs>) conecta el dropdown de
resolución, el toggle de pantalla completa y el slider de volumen. Sus campos
son resolutionDropdown, fullscreenToggle y volumeSlider. El componente vive
en el root activo del Canvas del menú principal y de pausa; los controles
siguen dentro del panel de opciones. Así se aplican las preferencias al entrar
a cada escena, aunque el panel empiece cerrado. Los listeners se
agregan al habilitar el componente y se retiran al deshabilitarlo; no hace
falta duplicarlos manualmente en los eventos del Inspector.

La lista de resoluciones elimina tamaños duplicados e incluye la ventana
actual. SetResolution(index) y SetFullscreen(enabled) usan Screen.SetResolution;
pantalla completa corresponde a FullScreenWindow. SetVolume(value) usa
AudioListener.volume con rango 0–1.

persistSettings controla la persistencia en PlayerPrefs. El prefijo inicial
es Survive.Options y las claves terminan en Width, Height, Fullscreen y Volume.
Las preferencias se leen al habilitar MenuOptions. Una resolución guardada
que ya no esté disponible vuelve al tamaño actual. El volumen se guarda al
cerrar las opciones, pausar la aplicación o salir, evitando escribir cada
cuadro mientras se arrastra el slider.

La conexión y su efecto visible deben verificarse en el flujo integrado. Los
cambios de pantalla se omiten en batch mode; una ejecución sin interfaz no
comprueba resolución ni pantalla completa reales.

## Salud y respuesta a muerte

[PlayerHealth.cs](<D:/SurvivePrototype/Assets/Scripts/PlayerHealth.cs>) conserva CurrentHealth,
MaxHealth e IsDead. TakeDamage(amount) acepta daño positivo y finito, reduce
la vida hasta cero y publica OnDeath una sola vez.

[PlayerDeathController.cs](<D:/SurvivePrototype/Assets/Scripts/PlayerDeathController.cs>) escucha
ese evento. Deshabilita el arma y la locomoción; cancela coroutines e
invocaciones del movimiento; detiene integración UPP, Animator, IK y control
de cámara. Limpia la velocidad del Rigidbody y lo vuelve cinemático. No
apaga la Camera ni el AudioListener, por lo que el mapa permanece detrás del
panel de derrota.

behavioursToDisable incluye controles adicionales como
PlayerInteractionController. La salud también se consulta antes de interactuar
y disparar. El campo menu puede apuntar al menú de la instancia; si está vacío,
se busca un MenuBehaviour de partida en la misma escena. Sin menú, la muerte
igualmente detiene el tiempo, libera el cursor y avisa del error de referencia.

El reintento recarga la escena completa. Se crean otra vez jugador, arma y
enemigos con sus valores iniciales; no es una restauración parcial del jugador
sobre la escena anterior.

## Oleadas en Level 1

La escena usa tres oleadas de ejemplo, editables en EnemySpawner:

| Oleada | Melee | Distancia | Patrulleros | Espera inicial | Intervalo | Radio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 2 | 1 | 0 | 8 s | 0,6 s | 3,5 m |
| 2 | 3 | 2 | 0 | 5 s | 0,6 s | 3,5 m |
| 3 | 4 | 3 | 0 | 5 s | 0,6 s | 3,5 m |

Las dos instancias manuales anteriores se conservan desactivadas. Sus
posiciones se reutilizan como puntos del spawner. Sólo cuentan los enemigos
generados por la secuencia; los manuales no se adoptan automáticamente.
No se agrega un HUD de conteo: CurrentWaveNumber, WaveCount y AliveEnemyCount
quedan disponibles para una UI posterior si se decide incorporarla.

Los nombres históricos de scripts y prefabs se conservan. Las etiquetas del
Inspector muestran Melee y Distancia; los patrulleros son opcionales. Ver
[OLEADAS.md](OLEADAS.md) para cantidades, tiempos, overrides, puntos y NavMesh.

## Enemigos

[EnemyBaseController.cs](<D:/SurvivePrototype/Assets/Scripts/EnemyBaseController.cs>) contiene la
lógica compartida. Las clases EnemyChasing, EnemyPatrol, EnemyChasingRanged y
EnemyAttack conservan en ejecución las estadísticas y la configuración de
patrulla serializadas en sus prefabs o instancias. Para usar una ruta fija,
desactivá useRandomPatrol y asigná patrolPoints. Se omiten entradas vacías;
si no hay destinos válidos, el agente queda detenido.

Las variantes a distancia reutilizan RangedAttackRoutine y
CanHitRangedTarget. Se comprueba que el jugador esté vivo, dentro de
attackRange y con un trayecto libre desde firePoint. attackObstructionMask
debe incluir las superficies que bloquean el ataque. También se comprueba
que el origen no esté solapando un obstáculo mediante rangedOriginClearanceRadius.
La comprobación se repite después de attackWindupTime: alejarse o ponerse a
cubierto durante la preparación puede evitar el impacto.

Si projectilePrefab está asignado, la rutina instancia ese objeto orientado
hacia el jugador. Si no está asignado, aplica daño directo después de las
comprobaciones. El efecto de escupida sincronizado con la animación no se
presenta como un proyectil físico. El detalle de modelos, clips y prefabs está
en [Enemigos y modelos ApocalypseZombies](ENEMIGOS_ZOMBIES.md).

## Interacción E y widget

[PlayerInteractionController.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/PlayerInteractionController.cs>)
se agrega al root de UPPPlayer. El raycast sale del centro de la cámara e
incluye el trigger del arma. interactionDistance comienza en 2,5 metros,
medidos desde interactionOriginOffset del jugador, inicialmente (0, 1, 0),
hasta el centro del arma. La separación de la cámara TPS no reduce el alcance.

Además se comprueban trayectos libres desde cámara y jugador hasta el centro
del arma. Un trigger que sobresale de una pared no habilita la recogida a
través de ella. La consulta ignora colliders propios y triggers ajenos, pero
conserva obstáculos sólidos incluidos en hitMask.

promptCanvasGroup y promptText apuntan al aviso de interacción del jugador.
El texto inicial es [E] Recoger arma; debe ajustarse si se cambia el binding de
interactAction. No hace falta activar aim. Al presionar E se revalida la
selección y se llama al mismo PlayerWeaponController.TryEquip que resuelve
el attachment existente.

El aviso se oculta cuando no hay objetivo válido, se supera el alcance, hay
un arma equipada, el jugador muere, el juego se pausa, la locomoción está
bloqueada o el cursor queda libre con requireLockedCursor activado. El collider
del pickup conserva su función de detección; MasterItem ya no implementa
recogida por OnTriggerEnter ni OnTriggerStay.

Ver parámetros completos y pasos de uso en [Ítems, pickup y armas](ITEMS_ARMAS.md).

## Validación del 13/09/2026

Unity 6000.3.5f2: **101/101 comprobaciones pasaron**, sin errores registrados
durante la ejecución de los casos. La prueba abre SceneAlejandro y usa el botón
Play para cargar Level 1; luego verifica pausa/reanudación, muerte, reintento
y vuelta al menú con sus componentes y referencias reales.

Se comprobó:

- entrada de E mediante New Input System y equipamiento en el socket existente;
- ausencia de pickup por contacto, aviso visible, alcance, bloqueo por paredes,
  triggers ajenos y recogida bloqueada durante pausa;
- muerte que detiene UPP/Rigidbody, arma, interacción, cámara y Animator;
- derrota que bloquea Esc/Reanudar y reintento que restaura jugador y tiempo;
- botones de menú y opciones de volumen, resolución y pantalla completa enlazados;
- cuatro variantes que respetan estadísticas y configuración de patrullaje;
- ambos ranged: tiro despejado, pared, origen dentro de obstáculo, rango,
  cobertura o alejamiento durante preparación y cancelación al deshabilitar IA;
- un PlayerHealth, un EventSystem y un AudioListener activos en Level 1.

Las comprobaciones de enemigos usan física real y ejecución controlada de sus
rutinas; no sustituyen una recorrida prolongada de navegación por el mapa.
Para simular teclado en batch se usa una copia temporal de InputSettings con
foco de entrada habilitado; se restaura la configuración original al terminar.
Se revisaron capturas de menú principal, pausa, derrota, opciones y aviso de E.

Los controles de pantalla se comprobaron en batch sin cambiar el monitor físico.
Queda por validar su resultado visual y persistencia entre ejecuciones en una
build de PC. Estas pruebas no equivalen a una build distribuida ni validan
las oleadas y el panel de victoria incorporados después de esa ejecución.

La evidencia queda fuera del repositorio en
`C:/Users/pc/.codex/visualizations/2026/09/11/01a09060-2975-7311-baf7-8b0d0f036998/gameplay-delivery-fixes/validation-result.json`.
El paquete temporal de trabajo fue retirado; manifest.json y packages-lock.json
se restauraron byte a byte. La compilación final sin esas herramientas terminó
con salida 0 y sin errores de C#.
