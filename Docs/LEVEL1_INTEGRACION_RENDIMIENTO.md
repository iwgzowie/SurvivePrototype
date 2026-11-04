# Level 1: integración jugable y tiempos de carga

## Actualización de gameplay del 13/09/2026

La entrada de la build ahora es SceneAlejandro (menú), seguida por Level 1.
El jugador incorpora salud, bloqueo al morir y recogida con E por raycast;
Level 1 tiene el PauseMenuCanvas existente, ventana de derrota y EventSystem.
La geometría y el NavMesh se conservaron. El recorrido y las 101 comprobaciones
actuales están en [Flujo de partida y menús](FLUJO_PARTIDA_MENUS.md).
Las mediciones de rendimiento y pruebas por contacto documentadas abajo son
evidencia histórica del 12/09, anterior al cambio de interacción.

## Resultado

Level 1 tiene un punto de inicio en el patio norte, con cuatro instancias en
el grupo `Level1_Gameplay`:

| Instancia | Prefab | Posición guardada aproximada |
| --- | --- | --- |
| UPPPlayer | Assets/Prefabs/Player/UPPPlayer.prefab | (-8, 0.03, 32) |
| Weapon_Rifle_Pickup | Assets/Prefabs/Items/Weapon_Rifle.prefab | (-6.7, -0.1, 31.4) |
| EnemyChasing | Assets/Players/EnemyChasing.prefab | (4, 0.95, 34) |
| EnemyChasingRanged | Assets/Players/EnemyChasingRanged.prefab | (10, 0.95, 29) |

El arma usa su flotación configurable de 100 cm al iniciar Play. Los enemigos
conservan sus comportamientos de persecución, modelos y Animator. El jugador
tiene PlayerHealth en esta instancia y el controller del arma lo referencia.
El prefab original del jugador no se modificó en esta tarea.

`Level1_Navigation` contiene un NavMeshSurface horneado desde los colliders
reales del mapa. Sus datos se guardan en `Assets/Scenes/Level1_Navigation.asset`.
El bake produjo 1493 triángulos navegables; ambos enemigos tienen rutas
completas hacia el punto inicial del jugador. Los actores se excluyen de
futuros bakes mediante NavMeshModifier.

La cámara antigua `Main Camera` queda inactiva. UPPPlayer crea su rig de cámara
habitual al iniciar. Se apagó DrawDebug en la instancia del jugador.

## Causa de la demora

El archivo contenía una instancia de `MAPA.prefab`, llamada `cementerio`, con
113249 overrides. Aproximadamente 23.5 MB eran modificaciones serializadas,
principalmente arrays de geometría, UV, tangentes e índices de ProBuilder.
Más de 111000 valores diferían del prefab: eran cambios reales del mapa.

El GUID que había faltado anteriormente ahora resuelve correctamente a
`Assets/Sprites/prefab/MAPA.prefab`. En esta revisión no faltaba ese asset.

El log previo del editor mostraba:

- Apertura de Level 1: 223.679 s; 223.020 s dentro de Integration y 0.658 s en
  Deserialize.
- Entrada a Play: 224.358 s; la reconstrucción de la escena temporal repetía
  224.210 s de Integration.
- 19453 avisos de dos AudioListeners.
- Sin errores de compilación C#.

La medición controlada reprodujo 223.357 s para abrir la escena. La demora
principal provenía de reconstruir la instancia con esa cantidad de overrides.
ProBuilder también tiene un callback de actualización de prefabs que recorre,
reconstruye y optimiza sus mallas hijas en edición. No apareció una anomalía de
colliders convexos o un script de enemigos que explicara el costo de apertura.

## Reparación y preservación

Se convirtió únicamente la instancia del mapa en objetos propios de la escena
mediante Unpack Outermost Root. Se mantienen sus mallas y componentes
ProBuilder editables. Esa parte de Level 1 ya no recibe cambios automáticos
por el vínculo con MAPA.prefab; el prefab original sigue intacto.

Se comprobó antes, después del unpack y luego de guardar y volver a abrir:

- 200 objetos, 172 renderers y 169 colliders del mapa original.
- 169 MeshFilters, 149155 vértices y 86940 triángulos.
- Los 168 componentes ProBuilderMesh.
- El mismo hash de geometría, normales, UV, índices, transforms y referencias
  de materiales: `0C131A48CFA07E0ED3B618DC6007436FA5A06335C5FA4F35A31559701E55BE76`.

La escena pasó de 71458900 bytes a 54725748 bytes antes de agregar los actores.
La apertura del mapa optimizado sin actores tardó 0.700 s.

## Validación de la escena final

Unity 6000.3.5f2, medición del 12/09/2026:

| Operación | Antes | Después, con jugador/arma/enemigos |
| --- | --- | --- |
| Abrir Level 1 | 223.4 s | 1.14 s |
| Entrar en Play | 224.4 s en el log previo | 2.81 s y 2.57 s |
| Salir de Play | Sin medición previa completa | 0.367 s y 0.367 s |

En ambos ciclos se comprobó:

- una cámara y un AudioListener activos;
- dos enemigos sobre el NavMesh y con rutas;
- recogida del arma por colisión y attachment al jugador;
- cero errores y cero avisos de AudioListeners duplicados.

Se inspeccionaron capturas del mapa antes/después y de gameplay. Las mediciones
son del Editor en esta máquina y no equivalen a una build distribuida.

## Respaldo y evidencia

La copia exacta anterior a esta tarea queda fuera del repositorio en:

`C:/Users/pc/.codex/visualizations/2026/09/11/01a09060-2975-7311-baf7-8b0d0f036998/level1-performance/baseline/Level 1.unity`

En la misma carpeta `level1-performance` están `optimization.json`,
`placement-result.json`, `play-timings.json`, sus logs y las capturas. La primera
apertura de diagnóstico no guardó cambios porque buscaba el prefab en la raíz;
la operación final lo localizó dentro del contenedor Mapa y terminó con PASS.

Se retiraron los scripts y el paquete temporal de trabajo. Las dependencias
del proyecto se restauraron exactamente al estado previo. La compilación final
sin ese paquete terminó con salida 0 y ningún error C#.