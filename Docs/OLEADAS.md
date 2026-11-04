# Oleadas de enemigos y victoria

El sistema usa [EnemySpawner.cs](<D:/SurvivePrototype/Assets/Scripts/EnemySpawner.cs>) y
[EnemyWave.cs](<D:/SurvivePrototype/Assets/Scripts/EnemyWave.cs>). Cada oleada configura sus propias
cantidades, tiempos, prefabs y ubicaciones. La partida se gana cuando terminó
la generación de la última oleada y ya no queda vivo ninguno de los enemigos
que este spawner generó, siempre que el jugador siga vivo.

No alcanza con esperar un tiempo ni con terminar de generar enemigos. Primero
se elimina toda la oleada actual; recién entonces empieza la espera de la
siguiente. Al completar la última se llama a MenuBehaviour.ShowVictory().

Esta guía describe la implementación y sus campos. Al final se registra
el alcance de la validación en Play dentro del Editor; no se ejecutó una build.

## Configurar EnemySpawner

Agregá un único EnemySpawner para la secuencia de la partida. Los enemigos
necesitan un NavMesh compatible con el tipo y las áreas de sus NavMeshAgent.

| Campo del Inspector | Función |
| --- | --- |
| Prefab melee | Prefab predeterminado del enemigo cuerpo a cuerpo. El nombre serializado sigue siendo chasingPrefab. |
| Prefab a distancia | Prefab predeterminado del enemigo a distancia. El nombre serializado sigue siendo chasingRangedPrefab. |
| Prefab patrullero | Variante adicional opcional, mediante patrolPrefab. |
| Waves | Lista ordenada de configuraciones EnemyWave. Su tamaño define cuántas oleadas hay. |
| Auto Start | Inicia una ejecución desde Start. Desactivado permite llamar StartWaves() desde otra lógica. |
| Player Health | Salud del jugador que debe sobrevivir. Sin referencia, busca un PlayerHealth con tag Player en la misma escena. |
| Menu | MenuBehaviour de partida. Sin referencia, busca uno de la misma escena que no sea menú principal. |
| Default Spawn Points | Puntos usados por las oleadas que no tienen una lista propia. |
| Nav Mesh Sample Distance | Distancia de búsqueda de un punto válido en el NavMesh. Valor inicial: 2 metros. |
| Max Spawn Attempts | Límite de intentos para encontrar una ubicación por enemigo. Valor inicial: 32. |

Los nombres de scripts y campos históricos se conservan para mantener las
referencias: EnemyChasing/chasingPrefab corresponde a **Melee** y
EnemyChasingRanged/chasingRangedPrefab a **Distancia**. Las etiquetas del
Inspector distinguen los dos tipos sin renombrar los assets existentes.

Un prefab usado con cantidad mayor que cero debe estar activo y tener
EnemyHealth y un NavMeshAgent habilitado en su raíz. Usá los prefabs del
proyecto en Assets/Players, con sus modelos, Animator y controladores ya
configurados. El spawner instancia esos prefabs; no reemplaza su lógica de
combate ni su configuración de patrulla.

## Ejemplo integrado en Level 1

| Oleada | Melee | Distancia | Patrulleros | Delay Before Wave | Spawn Interval | Spawn Radius |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 2 | 1 | 0 | 8 s | 0,6 s | 3,5 m |
| 2 | 3 | 2 | 0 | 5 s | 0,6 s | 3,5 m |
| 3 | 4 | 3 | 0 | 5 s | 0,6 s | 3,5 m |

Son valores de partida para editar desde el Inspector. Las dos instancias
manuales anteriores se conservan desactivadas y sus posiciones pasan a ser
puntos de aparición. La secuencia genera sus propios enemigos y no cuenta
esas instancias manuales. No se agrega un HUD de conteo; el estado queda
expuesto mediante las propiedades públicas del spawner.

## Configurar cada oleada

| Campo | Función |
| --- | --- |
| Wave Name | Nombre descriptivo para identificar la oleada en el Inspector. |
| Cantidad melee | chasingCount: cantidad de enemigos cuerpo a cuerpo de esa oleada. |
| Chasing Prefab | Override opcional del prefab melee únicamente para esa oleada. Vacío usa el predeterminado del spawner. |
| Cantidad a distancia | chasingRangedCount: cantidad de enemigos a distancia. |
| Chasing Ranged Prefab | Override opcional del prefab a distancia únicamente para esa oleada. |
| Cantidad de patrulleros | patrolCount: grupo adicional opcional. Cero lo omite. |
| Patrol Prefab | Override opcional de la variante patrullera para esa oleada. |
| Delay Before Wave | Espera antes de generar esa oleada, en segundos de juego. Valor inicial: 5. |
| Spawn Interval | Espera entre apariciones, en segundos de juego. Valor inicial: 0,5. Cero permite generar la composición en el mismo cuadro mientras la partida está activa. |
| Spawn Radius | Dispersión horizontal alrededor del punto elegido, en metros. Valor inicial: 3. Cero usa el punto sin dispersión. |
| Spawn Points | Lista de puntos de esa oleada. Vacía usa Default Spawn Points; sin ninguna lista se usa el Transform del spawner. |

Las cantidades deben ser no negativas. Se permite que una oleada tenga cero
enemigos, pero la secuencia completa debe incluir al menos uno; una lista
vacía o una secuencia sin enemigos no otorga victoria. Los tiempos y el radio
deben ser finitos y no negativos.

Para alternar la composición durante la generación, el spawner recorre los
tipos disponibles: un melee, uno a distancia y un patrullero, omitiendo los
grupos que ya agotaron su cantidad. Repite hasta completar la composición.
El intervalo se aplica entre apariciones, no entre grupos completos.

La patrulla es opcional. La categoría adicional no obliga a que todos los
enemigos patrullen: el comportamiento depende de su prefab. Configurá allí
useRandomPatrol, patrolPoints, detección, daño y velocidad según corresponda.

## Puntos de aparición y NavMesh

Los puntos se recorren en orden y se reutilizan cuando se llega al final de
la lista. A cada intento se aplica una dispersión aleatoria horizontal dentro
de Spawn Radius. Después se busca una posición sobre el NavMesh compatible
con agentTypeID y areaMask del prefab.

Se exige un camino completo desde el punto configurado hasta la posición
aleatoria: la dispersión no salta a techos o islas desconectadas del NavMesh.
La raíz se coloca con el baseOffset del agente. Se comprueba la separación
respecto de los otros enemigos vivos que este spawner tiene registrados,
usando los radios de sus agentes. Un intento ocupado o sin NavMesh pasa al
siguiente hasta alcanzar Max Spawn Attempts. La rotación inicial se toma del
punto elegido.

Colocá los puntos en sectores transitables y alcanzables del nivel. La
proyección al NavMesh y la separación entre enemigos no reemplazan revisar
que el jugador pueda acceder al sector. Evitá listas con referencias vacías,
puntos dentro de paredes o una dispersión incompatible con el espacio disponible.

Si no se encuentra una ubicación válida o el agente recién creado no queda
sobre el NavMesh, la ejecución pasa a Failed, completa LastError y registra
el error en Console. No salta ese enemigo para otorgar una victoria falsa.

## Conteo y final de partida

Cada enemigo creado por el spawner se registra por su EnemyHealth. Su evento
OnDeath lo retira del conteo una sola vez; no hace falta esperar el retardo
de destrucción del cadáver. También se depuran referencias de enemigos ya
destruidos para no dejar una espera bloqueada por objetos que no existen.

Un enemigo colocado manualmente en la escena no se adopta ni se suma a la
oleada. Si se dejan enemigos manuales activos, su muerte no participa de la
condición de victoria de este spawner. Para que Level 1 dependa únicamente de
la secuencia, las instancias manuales anteriores deben quedar desactivadas.

Antes de pasar de oleada y antes de completar la secuencia se vuelve a
comprobar que el jugador esté vivo y que el menú no haya terminado la partida.
La victoria se decide fuera del callback inmediato de muerte de un enemigo.

[MenuBehaviour.cs](<D:/SurvivePrototype/Assets/Scripts/MenuBehaviour.cs>) expone victoryContainer,
IsVictory, IsFinished y ShowVictory(). La victoria oculta pausa y derrota,
muestra su panel, detiene el tiempo y libera el cursor. Esc y ResumeGame no
reanudan una partida terminada. Derrota y victoria no se reemplazan entre
sí: el primer resultado terminal se conserva. El panel reutiliza los botones
de recargar escena o volver al menú.

## Pausa, cancelación y reintento

La pausa detiene tanto las esperas como la generación de oleadas. Reanudar
continúa la misma secuencia. La muerte del jugador cancela la ejecución; no
se generan más enemigos y no se emite una victoria posterior.

CancelWaves() también permite cancelar desde otra lógica. Deshabilitar el
spawner cancela una ejecución activa. La cancelación deja de contar enemigos
y retira las suscripciones, pero no destruye automáticamente los que ya
fueron creados. Un error de configuración o aparición tampoco los destruye.

StartWaves() sólo inicia desde Idle. Una ejecución Completed, Cancelled o
Failed no se reinicia sobre los mismos objetos. Reintentar la partida recarga
la escena: se crean otra vez el jugador, el spawner y su configuración inicial.

## API y estados observables

| API | Uso |
| --- | --- |
| StartWaves() | Devuelve si pudo iniciar una nueva ejecución. |
| CancelWaves() | Cancela una secuencia activa. |
| State | Idle, Waiting, Spawning, WaitingForEnemies, Completed, Cancelled o Failed. |
| CurrentWaveIndex | Índice desde cero; antes de iniciar vale -1. |
| CurrentWaveNumber | Número legible desde uno; antes de iniciar vale cero. |
| WaveCount | Cantidad de oleadas de la secuencia. |
| AliveEnemyCount / AliveEnemies | Enemigos registrados que todavía no fueron retirados del conteo. |
| SpawnedInCurrentWave | Cantidad generada hasta el momento en la oleada actual. |
| LastError | Motivo del fallo cuando State es Failed. |
| IsRunning | Indica espera, generación o espera de enemigos; no incluye estados terminales. |

Eventos disponibles: OnWaveStarted(index, wave), OnWaveCompleted(index),
OnEnemySpawned(health) y OnAllWavesCompleted(). Los índices de eventos empiezan
en cero. OnWaveCompleted se emite al vaciar la oleada; OnAllWavesCompleted
corresponde únicamente a la terminación satisfactoria de toda la secuencia.

Al iniciar se copia la configuración de la secuencia: cantidades, tiempos,
radios y prefabs ya resueltos a partir de los overrides o los predeterminados.
También se resuelve y copia cada lista de puntos, usando la lista de la oleada,
la predeterminada o el Transform del spawner según corresponda. La distancia
de búsqueda NavMesh y el máximo de intentos se conservan para esa ejecución.
Cambiar esos campos o la lista original del Inspector después de iniciar no
altera la secuencia en curso; los ajustes se toman al empezar otra partida.

La copia fija qué Transform pertenecen a cada lista, pero conserva sus
referencias intencionalmente. Mover o rotar uno de esos puntos durante la
partida cambia las apariciones posteriores que lo usen; no se congelan sus
posiciones. Reemplazar, agregar o quitar entradas en la lista original sí
requiere reiniciar para modificar los puntos de la nueva ejecución.

## Validación del 13 de septiembre de 2026

- Unity 6000.3.5f2: 95/95 comprobaciones de gameplay en Play del Editor.
- Cantidades por tipo, override por oleada, copia de configuración inicial,
  oleada intermedia vacía, pausa con tiempos cero, bajas repetidas, enemigos
  desactivados/destruidos, cancelación y derrota.
- Nueve configuraciones inválidas provocaron los nueve errores esperados;
  ninguna otorgó victoria.
- Level 1 generó 2+1, 3+2 y 4+3 enemigos. Se verificaron sus caminos completos
  al jugador y la victoria después de las quince bajas.
- Botones reales de victoria: recarga desde la primera oleada y regreso a
  SceneAlejandro. Panel renderizado a 1280x720 con textos sin desbordes.
- Las pruebas desactivaron la IA al aparecer cada enemigo y aplicaron daño
  por API para aislar el flujo de oleadas; no equivalen a una partida manual
  completa ni a una build de PC.

La primera ejecución detectó tres apariciones sin camino completo; se agregó
un filtro de continuidad del NavMesh desde el punto configurado. La segunda
ejecución pasó todas las comprobaciones, pero el proceso terminó con código 2
porque el recolector registró además una SocketException/ConnectFailure de
red sin un llamador de gameplay en su traza. Ese error no se ocultó ni se
contó entre los nueve errores de configuración esperados. La misma firma
aparece en una validación previa de animaciones de zombies; no es exclusiva
de las oleadas. Los scripts de gameplay no usan clientes de red. Hay
actualizadores HTTP del Editor, incluido Synty, pero la traza no permite
atribuir el fallo a uno en particular.

La evidencia local está fuera del repositorio, en
`C:/Users/pc/.codex/visualizations/2026/09/11/01a09060-2975-7311-baf7-8b0d0f036998/wave-system/`:
`validation-result.json`, `validation.log` y `victory.png`. Las herramientas
de validación fueron temporales y no forman parte de Assets.

La compilación final, una vez retirado el paquete de herramientas temporales,
terminó con código 0 y sin errores de C#. `manifest.json` y
`packages-lock.json` quedaron idénticos al respaldo previo de esta tarea.
El log de compilación es `final-clean-compile.log` en la misma carpeta local
de evidencia.
