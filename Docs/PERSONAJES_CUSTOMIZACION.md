# Creación y personalización de personaje

Implementación del 20/10/2026 para Unity 6000.3.5f2.

## Flujo de entrada

`MenuBehaviour.StartGame()` mantiene el botón Play de `SceneAlejandro` como entrada.
Cuando no hay un perfil guardado y válido, abre `CharacterCreationMenu` sobre
el menú principal. Cancelar cierra el borrador y permite volver a presionar Play;
no crea un perfil. Confirmar valida el personaje y guarda el perfil antes de
cargar `Level 1` en el siguiente cuadro.

Si ya existe un perfil válido, Play carga directamente `Level 1`. Reiniciar una
partida o volver al menú conserva ese perfil. Entrar a una escena de gameplay
directamente desde el Editor no fuerza la creación de un personaje.

## Opciones disponibles

El creador ofrece nombre, género masculino/femenino, cara, pelo y componentes
faciales; tipo de cuerpo con tamaño y musculatura; ropa y accesorios. Los
presets de cara y vestimenta se pueden combinar y después ajustar por parte.
Los filtros por colección reducen la lista de opciones sin excluir los módulos
del catálogo.

Los selectores conservan la cara y el conjunto elegidos al reconstruir los
controles, cambiar de pestaña o cambiar de género. Cada pestaña recuerda su
filtro y posición de desplazamiento. Al modificar piezas individuales, el
selector indica `Cara personalizada` o `Conjunto personalizado` cuando la
combinación deja de coincidir con una receta. Los presets corporales resaltan
el que coincide con las proporciones actuales.

`SidekickCharacterLabels` presenta nombres como `Hombre robot 1`,
`Mujer superviviente 2` y `Hombre soldado espacial 3`; el encabezado identifica
el tipo de pieza. Los números de piezas son únicos dentro de cada estilo y
slot, incluso cuando distintos packs comparten familia. Las etiquetas son de
presentación: no modifican los IDs del catálogo ni del perfil guardado.

Sidekick utiliza las mismas mallas modulares para ambos géneros. El compositor
aplica `masculineFeminine` a todas las partes que incluyen ese blend shape,
`defaultSkinny`/`defaultHeavy` para tamaño y `defaultBuff` para musculatura.
No cambia la escala ni la cápsula de colisión del jugador: las variaciones son
visuales y mantienen el contrato de locomoción existente.

El catálogo se genera a partir de los FBX modulares y las recetas `.sk` del
paquete importado. Incluye HumanSpecies, ApocalypseSurvivors, ApocalypseOutlaws,
ScifiSoldiers, ApocalypseZombies y ScifiRobots. Los módulos robóticos y algunos
accesorios no ofrecen todos los blend shapes corporales; conservan la geometría
que aporta el asset. Combinar especies o accesorios puede producir superposición
visual y requiere seleccionar piezas compatibles.

## Integración y persistencia

- `CharacterProfile` conserva nombre, género, tamaño, músculo y IDs estables de
  partes por slot. Los slots obligatorios evitan confirmar un personaje sin
  cabeza, torso, brazos, manos, cadera, piernas o pies.
- `CharacterProfileStore` escribe `character-profile.json` dentro de
  `Application.persistentDataPath`. Usa un archivo temporal y reemplazo atómico;
  el guardado anterior queda en `.bak`. Si el archivo principal está dañado,
  intenta recuperar el backup. No utiliza las claves de opciones del menú.
- `SidekickCharacterCatalog` vive en
  `Assets/Resources/Character/SidekickCatalog.asset` y referencia los modelos y
  materiales necesarios para una build.
- `SidekickCharacterAppearance` está en `UPPPlayer`. Crea renderers modulares y
  resuelve huesos por nombre sobre el rig existente. Conserva Animator, Avatar,
  controlador de locomoción, cámara, IK y socket de arma; oculta el renderer
  anterior después de componer correctamente el nuevo aspecto.
- El creador se presenta sobre un estudio 3D a pantalla completa con cámara de
  perspectiva, luces, plataforma y utilería. El rig Sidekick se muestra de cuerpo
  completo con el mismo encuadre en todas las pestañas; se puede girar arrastrando
  el escenario o con los controles de rotación. La interfaz acompaña desde un
  panel lateral translúcido. Al cerrar, se destruye el estudio y se restauran las
  cámaras, canvases y controles del menú.

### Corrección del estudio del 24/10/2026

El montaje inicial colocaba la pared entre la cámara y el personaje, desplazaba
la figura hacia el panel y reutilizaba la transparencia del fondo para el texto
de la pestaña activa y del botón de confirmación. Se corrigieron la posición del
fondo y el color opaco del texto. El encuadre reserva el área izquierda para todo
el cuerpo, conservando margen al girar y al cambiar la proporción de pantalla.
La corrección posterior de estabilidad usa un volumen fijo de presentación:
aplicar modelos, caras, ropa o proporciones no modifica posición, rotación, FOV
ni proyección de la cámara. Sólo un cambio de resolución reajusta el encuadre.
El apoyo sobre la plataforma se calcula con la geometría horneada del calzado,
sin usar accesorios para desplazar al personaje ni para recalcular la cámara.

Si falla la construcción o se desactiva el creador, se restauran los Canvas,
controles y cámaras originales y se permite volver a intentar desde Play.

### Corrección de desplegables del 24/10/2026

El formulario tenía `sortingOrder=30000`, el mismo que TMP asigna a la lista de
opciones. Su bloqueador de clics usa 29999 y quedaba debajo del formulario. Esto
permitía que los textos del cuerpo se dibujaran sobre la lista de género y que
los controles del fondo recibieran los clics destinados al desplegable.

El formulario ahora usa 1000: formulario < bloqueador 29999 < lista 30000. Las
listas e ítems tienen fondo opaco y apertura inmediata. Se añadieron barras de
desplazamiento al formulario y a las listas y se separó el título de los sliders
de su valor. Escape/B cierra primero la lista abierta y conserva el creador.

La validación anterior cambiaba directamente el valor de los selectores; no
cubría abrir la lista, elegir su Toggle ni comprobar el bloqueador. El recorrido
actual usa raycasts y eventos de puntero, rueda y arrastre para realizar esas
acciones y comprueba que no queden listas ni bloqueadores huérfanos.

`Survive/Character/Crear e integrar catálogo Sidekick` regenera la integración;
`Survive/Characters` contiene las validaciones. Regenerar no modifica los FBX,
recetas, materiales ni prefabs originales de Sidekick.

## Validación reproducible

`Survive.Editor.CharacterCustomizationValidation.Run()` comprueba cobertura del
catálogo frente a los FBX importados, IDs y materiales, binding de huesos,
composición de todos los módulos en lotes por slot, todas las recetas por
género, pesos de blend shapes, geometría del torso, conservación del rig y
persistencia con recuperación de backup. Usa archivos de prueba dentro de
`Logs/CharacterCustomization`.

`RunAll()` agrega la prueba real en Play Mode: botón Play sin perfil, cancelar,
volver a crear, controles de género/cuerpo/nombre, guardar, entrar a `Level 1`,
volver al menú y entrar directamente con el mismo perfil. Usa
`ProfilePathOverride` exclusivo del Editor para no tocar el personaje del usuario.
`RunPlayMode()` ejecuta ese flujo directamente. Incluye clics mediante raycast y
EventSystem, texto opaco y con contraste, cuerpo completo dentro del área libre,
ausencia de oclusiones por geometría del estudio, giro y limpieza al cerrar.
`RunCreatorUI()` ejecuta abrir, cancelar, reabrir y personalizar con esos controles,
y termina cerrando el borrador sin cargar gameplay. Guarda `creator-ui-result.json`.
Comprueba además la selección de recetas tras reconstrucción, pestañas y género,
filtros y scroll independientes, piezas manuales y resaltado de presets. Mide
posición, rotación, FOV y proyección de cámara, escala del rig y anclas del estudio.
Barre 148 combinaciones de recetas (37 x 2 géneros x 2 extremos corporales) y
188 lotes modulares que cubren los 1.146 IDs en cada uno de esos cuatro escenarios.
Usa mallas horneadas reales para comprobar el encuadre fijo y restaura exactamente
el borrador del recorrido UI antes de cerrar.
Las capturas leen el framebuffer real del GameView a 1440x900, incluidos los
desplegables, sin modificar las capas, el Canvas ni la cámara para la evidencia.
El cierre por Escape se comprueba como evento de navegación `cancelHandler`;
no se presenta como una prueba de teclado físico.

`RunCrossSession()` debe ejecutarse en otro proceso Unity después de `RunAll()`.
Lee el perfil de prueba que quedó en disco y comprueba Play directo y aplicación
del mismo personaje en gameplay. Esto separa la persistencia entre escenas de
la persistencia entre ejecuciones de la aplicación.

Los resultados se escriben como `edit-result.json`, `play-result.json` y
`cross-session-result.json`, junto con capturas PNG en la misma carpeta de logs.
La existencia del validador no equivale a una prueba aprobada: los resultados de
la ejecución actual deben revisarse antes de reportar `VERIFICADO`.

## Estado de entrega

**IMPLEMENTADO:** catálogo de 1.146 módulos y 37 recetas, composición, perfil,
persistencia y flujo de creación integrados en el código y prefab del proyecto.

**VERIFICADO antes de la actualización visual en Unity 6000.3.5f2:** validación
de assets, composición, geometría, rig y persistencia **221/221**; flujo real en
Play Mode **61/61**, con **0 errores de runtime registrados**. Se probaron
cancelación sin crear perfil, género, tamaño y musculatura desde los controles
reales, cambio de cara y conjunto, confirmación, entrada a `Level 1`, vuelta al
menú y Play directo con el mismo personaje. El jugador aplicó exactamente el
JSON persistido. Esos conteos no cubren el nuevo montaje visual 3D.

**Primera comprobación del estudio del 24/10/2026, anterior al arreglo de listas:** Play Mode terminó con **218 PASS,
4 FAIL y 0 errores de runtime**. Pasaron apertura desde Play sin perfil, clics
por raycast/EventSystem, textos opacos y con contraste, cuerpo completo dentro
del área izquierda, ausencia de oclusiones del escenario, rotación y retorno a
frente, cancelar y restaurar el menú, reapertura sin duplicados, guardar, entrada
a gameplay y Play directo con el perfil creado. El apoyo del calzado ya no queda
oculto por la plataforma.

Los cuatro FAIL correspondían al control que exige que el personaje ocupe al
menos el 40% de la altura del GameView automático. En la comprobación actual se
fijó el framebuffer real del GameView a 1440x900 y ese umbral pasa sin reducirlo.
Las capturas anteriores de la cámara real a 1440x900 se revisaron visualmente y
muestran cuerpo completo y controles legibles para ambos perfiles probados.
La evidencia final y la anterior quedan en
`Logs/CharacterCustomization/20261024-regression`: `final-play-result.json`,
`verified-play.log`, `creator-fixed.png`, `feminine-fixed.png` y
`previous-evidence/`. El perfil del usuario no se usó para estas pruebas.

**VERIFICADO UI anterior a los ajustes de selección y encuadre fijo:** `RunCreatorUI()` terminó con **762/762 PASS, 0 FAIL y
0 errores de runtime**, con salida Unity 0. Se abrieron y seleccionaron opciones
de género, recetas de cara y ropa, filtros por pack y módulos individuales mediante
clics reales de EventSystem/raycast; se desplazaron las listas y se arrastraron
los sliders. Se comprobaron contraste, opacidad, orden de capas, bloqueo de
controles de fondo, cierre por fuera/cancel, reconstrucción, giro, cancelación,
restauración y reapertura. Las capturas de género y ropa abiertos se revisaron.
Evidencia: `Logs/CharacterCustomization/20261024-dropdown-ui/`, especialmente
`creator-ui-result.json`, `creator-ui-final.log`, `05-gender-popup.png` y
`06-outfit-popup.png`.

**Integración con gameplay previa a los ajustes de selección:** el recorrido completo registró **788 PASS
y 1 FAIL global**, con dos errores de `EnemySpawner` al cargar `Level 1`:
`La lista de plantillas de oleadas está vacía.` Las comprobaciones de UI,
confirmación, persistencia y Play directo pasaron; no se informa PASS global de
ese recorrido. Se preservan `integration-result.json` y `ui-play2.log` en la misma
carpeta. Esta revisión no modifica la configuración de oleadas.

**VERIFICADO selección, nombres y encuadre fijo del 24/10/2026:** los controles
reales y los barridos de geometría completaron **1.049 comprobaciones PASS**.
Se conservaron recetas, género, módulos, filtros, scroll y presets; se comprobaron
nombres simples y el mismo encuadre para todas las recetas y los 1.146 módulos,
en ambos géneros y con complexión -100/100 y musculatura 100. No hubo cambios de
cámara, proyección, FOV, escala del rig ni anclas del escenario al aplicar opciones.
Las capturas 1440x900 se revisaron visualmente.

La ejecución completa terminó con **1 FAIL global y 1 excepción de runtime**,
una `SocketException` de conexión en segundo plano, que también ocurrió en la
primera ejecución (1.026 PASS antes de ampliar la cobertura modular). El stack
registrado contiene sólo llamadas de red de .NET; no permite atribuir con certeza
el emisor. El actualizador del Editor de Sidekick inicia una consulta HTTP al
abrir Unity, pero esa relación es una inferencia, no una atribución probada.
No se filtró la excepción ni se modificó el paquete Sidekick para omitirla.
No se reporta PASS global. Evidencia final en
`Logs/CharacterCustomization/20261024-selection-framing/`: `creator-ui-result.json`,
`creator-ui-final.log` y las cuatro capturas del creador; `first-result.json` y
`first-run/` preservan la primera ejecución. El perfil del usuario no se utilizó.
**Histórico del 20/10/2026, entre ejecuciones:** una segunda sesión Unity leyó el perfil que
había quedado en disco, entró directamente desde Play y aplicó el mismo JSON al
jugador: **16/16 comprobaciones**, **0 errores registrados**.

**PENDIENTE de gameplay:** revisar la configuración vacía de oleadas de
`EnemySpawner`. **NO VERIFICADO:** build distribuida, rendimiento prolongado ni
revisión visual de todas las posibles combinaciones de accesorios.
