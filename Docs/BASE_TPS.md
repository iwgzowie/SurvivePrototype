# Base TPS UPP

## Propósito y ubicación

Esta guía define la base de locomoción, cámara, Animator, IK y diagnóstico del
proyecto. El sistema es singleplayer para PC y su responsabilidad termina en
mover, orientar y animar al personaje.

La documentación propia vive en la carpeta raíz
[Docs](./), fuera de Assets. Unity no necesita importar estas guías ni generar
archivos .meta para ellas.

Documentos relacionados:

- [Locomoción, cámara, Animator, IK y debug](LOCOMOCION_CAMARA_ANIMATOR.md)
- [Referencia de clases](REFERENCIA_CLASES.md)
- [Ítems, pickup y armas](ITEMS_ARMAS.md)
- [Enemigos y modelos ApocalypseZombies](ENEMIGOS_ZOMBIES.md)
- [Level 1: integración y rendimiento](LEVEL1_INTEGRACION_RENDIMIENTO.md)
- [Corrección de foot IK y temblor de movimiento](CORRECCION_IK_MOVIMIENTO.md)
- [Flujo de partida, menús y fin de partida](FLUJO_PARTIDA_MENUS.md)
- [Creación y customización de personaje Sidekick](PERSONAJES_CUSTOMIZACION.md)
- [Oleadas configurables y victoria](OLEADAS.md)

El código, los prefabs, el Animator Controller y la escena son la fuente de
verdad. Si una guía contradice un asset serializado, hay que corregir ambos en
la misma tarea.

## Baseline del proyecto

- Unity: 6000.3.5f2.
- Entrada de la partida: [SceneAlejandro](<D:/SurvivePrototype/Assets/Scenes/SceneAlejandro.unity>),
  seguida por [Level 1](<D:/SurvivePrototype/Assets/Scenes/Level 1.unity>). Son las dos escenas
  habilitadas en Build Settings, en ese orden.
- Escena de trabajo de locomoción:
  [Playground](<D:/SurvivePrototype/Assets/StarterAssets/ThirdPersonController/Scenes/Playground.unity>);
  no es la entrada de la entrega.
- Jugador: [UPPPlayer.prefab](<D:/SurvivePrototype/Assets/Prefabs/Player/UPPPlayer.prefab>).
- Rig de cámara canónico, creado automáticamente por el jugador:
  [UPPThirdPersonCamera.prefab](<D:/SurvivePrototype/Assets/Prefabs/Cameras/UPPThirdPersonCamera.prefab>).
- Animator Controller:
  [UPPAnimatorTPS.controller](<D:/SurvivePrototype/Assets/Animations/Player/Controllers/UPPAnimatorTPS.controller>).
- Input:
  [UPPPlayerCharacterInputs.asset](<D:/SurvivePrototype/Assets/Settings/Input/UPPPlayerCharacterInputs.asset>).
- Ensamblado runtime:
  [UPP.Runtime.asmdef](<D:/SurvivePrototype/Assets/Scripts/Player/UPP.Runtime.asmdef>).

## Alcance actual

La base conserva:

- movimiento relativo a cámara mediante Rigidbody;
- caminar/correr, sprint, salto, crouch, prone y roll;
- detección de suelo y paredes, pendientes y corrección de escalones;
- cápsula adaptativa según la pose;
- cámara TPS con seguimiento, colisión y estado over-the-shoulder de aim;
- traza desde el centro del viewport y crosshair de diagnóstico;
- Animator Humanoid con locomoción normal y direccional durante aim;
- foot IK, posicionamiento de cuerpo, look-at de aim y body lean;
- root motion opcional para locomoción;
- un interruptor maestro y opciones granulares para el debug visual;
- sensibilidad e inversión de cámara mediante PlayerPrefs.

## Gameplay conectado y límites de la base

El jugador incorpora PlayerWeaponController, PlayerInteractionController,
PlayerHealth y PlayerDeathController como componentes de gameplay. MasterItem
y su clase hija WeaponItem controlan la flotación y el attachment. La recogida
se solicita con E cuando el raycast de cámara encuentra un arma dentro del
alcance configurable y sin obstáculos; tocar su trigger no la equipa. El
disparo usa aim y cámara de UPP. La ampliación de armas incorpora presets
WeaponDefinition, cargador y reserva finitos por pickup, recogida con E,
recarga con R, soltar con G y HUD por eventos. PlayerWeaponInventory conserva
hasta seis instancias en slots; sólo una queda equipada y las otras se ocultan.
PlayerWeaponHotbar presenta selección, fotografía, nombre y munición de los seis slots.
No hay stacking ni guardado persistente; una partida nueva comienza vacía.
Ver [Ítems, pickup y armas](ITEMS_ARMAS.md).

El sistema incorpora PlayerWeaponPose para manos y retroceso, y
PlayerWeaponFeedback para audio por eventos. Los ocho presets disparan al
mantener LMB y recargan automáticamente al vaciarse si queda reserva; R permite
la recarga manual. Las animaciones de aim y recarga usan el paquete RIFLE PRO
V2.7A2, importado completo bajo Assets/Rifle_27A2_Pro sin modificar el original
de E:/Unity. WeaponFeelSetup.CreateAndIntegrate() integra estas referencias.

**Ampliación actual del 24/10/2026: GunFX URP y auditoría audio/VFX IMPLEMENTADOS
/ INTEGRADOS / VERIFICADOS en Play Mode controlado posterior a la poda:
229/229 PASS, exit code 0 y 22 capturas.** Ocho perfiles conectan fogonazos, vainas cosméticas e impactos
por seis superficies al disparo existente. Un pool de 128 efectos por escena
conserva tiempo escalado; las partículas no administran balas ni daño.
El borrado autorizado queda limitado a audio/VFX sin referencias supervivientes,
con manifiesto explícito, grafo revalidado y respaldo externo verificado.

**Ampliación de recargas del 24/10/2026: IMPLEMENTADA / VERIFICADA en Play Mode
controlado: 114/114 PASS, exit code 0 y 24 capturas WAV.** Los ocho presets presentan cuatro cues cada uno según
ReloadProgress, con pitch 1 y audio separado del disparo. Son cuatro cortes
de dos grabaciones reales de JU TPS, reutilizados y sincronizados por preset;
no ocho grabaciones específicas. Sólo se importan esos cuatro WAV y sus metas.
Se probaron TryReload/Animator reales, transferencia única, cancelaciones,
pausa/reanudación y final con frame largo. AudioRenderer capturó 2.087.936
frames de muestras; los ocho mixes naturales tienen picos 0,191926–0,237857,
sin clipping y con cero errores de runtime. El fixture usó una escena temporal
y no guardó escenas. El análisis FFT externo pasó 69/69: las 32 fases aparecen
una sola vez, timing con desvío máximo de 24,67 ms y ganancias relativas por
clip verificadas entre perfiles. Log:
[WeaponReloadAudio-Validation-Final.log](<D:/SurvivePrototype/Logs/WeaponReloadAudio-Validation-Final.log>).
La compilación final sin helper terminó **PASS, exit code 0**:
[WeaponReloadAudio-FinalCompile.log](<D:/SurvivePrototype/Logs/WeaponReloadAudio-FinalCompile.log>).
El escaneo posterior registró cero candidatos/bloqueos, 279 recursos retenidos
y 125 AudioClip; reporte
[post-reload-audio-audit.json](<C:/Users/pc/.codex/visualizations/2026/10/19/01a153f2-ffb7-7f82-bfbd-04190b330ad4/reload-audio-evidence/post-reload-audio-audit.json>).
Manifest/lockfile se restauraron byte a byte contra Packages-before-validation
y el helper externo con sus fuentes fue eliminado. El hash de Level 1 coincide
con el capturado inmediatamente antes de la suite, después del cambio de NPC
de otra tarea; no implica igualdad con el inicio de la entrega VFX.

**Audio Modern Weapons del 24/10/2026: IMPLEMENTADO / INTEGRADO / VERIFICADO
en Play Mode controlado posterior a la poda: 178/178 PASS, exit code 0.**
Los ocho presets usan perfiles OneShot/Stop/Tail con variantes independientes
de la dispersión de gameplay. El paquete no aporta clips nombrados de recarga;
los cues posteriores proceden de JU TPS y no de síntesis.
La procedencia y las correspondencias acústicas están en
[Ítems y armas](ITEMS_ARMAS.md). Los resultados siguientes son históricos.

**Entrega anterior del 19/10/2026: mecanismos e imágenes reales de las armas
IMPLEMENTADOS / INTEGRADOS / VERIFICADOS en Play Mode controlado: 142/142 PASS
y 26 capturas.** WeaponMechanismAnimator presenta las piezas
originales mediante 18 clips locales; WeaponDefinition.Icon referencia ocho
fotografías reales Synty de 512 × 320 con transparencia. La hotbar conserva
seis slots y agrega seis imágenes serializadas. El setup de Editor pasó y
se revisaron los ocho renders, las 24 capturas de mecanismos y la UI en dos
resoluciones. La compilación final sin helper terminó **PASS, exit code 0**.
Las evidencias anteriores se mantienen separadas.

**Entrega anterior del 19/10/2026: inventario/hotbar de seis slots y retiro
del sonido provisional de recarga IMPLEMENTADOS / INTEGRADOS / VERIFICADOS en
Play Mode controlado: 66/66 PASS y cuatro capturas de UI.** La compilación final
sin el helper externo terminó PASS, exit code 0. Selección
directa 1–6 permite quedar desarmado en un slot vacío; la rueda recorre los
ocupados. E guarda/equipa en el primer espacio libre y, si está lleno,
reemplaza el seleccionado dejando el anterior en el suelo del pickup.
G suelta sólo el seleccionado. Cambiar cancela la recarga sin transferir balas.

**Evidencia anterior a los seis slots: feel IMPLEMENTADO / INTEGRADO /
VERIFICADO en Play Mode controlado, 169/169 comprobaciones y 44 capturas.** Se revisaron
visualmente las ocho armas en aim y momentos seleccionados de recarga.
La compilación final tras retirar el paquete temporal terminó **PASS, exit code 0**.
Los 17/17 grupos de Editor y Play Mode pertenecen a la entrega anterior de
munición, HUD y pickups; no prueban automáticamente el nuevo feel. Level 1
conserva pickups de Pistol, Shotgun y AssaultRifle. Sidekick Characters aporta
los personajes y Polygon Apocalypse aporta el arte de armas. Se reutiliza
una recarga de rifle en los ocho presets. El audio sintético de disparo de
esa entrega se reemplaza por perfiles Modern Weapons en la ampliación del
24/10. El retiro de Reload_Weapon.wav dejó silenciosa la recarga en esa entrega;
la ampliación actual agrega cues reales de JU TPS y conserva ReloadSound como
respaldo opcional, sin regenerar el placeholder.
Las 169 comprobaciones no validan el inventario/hotbar, los mecanismos e
imágenes posteriores, la retirada del placeholder ni el audio Modern Weapons.
Una sesión manual completa, la escucha manual del audio y una build
distribuida siguen **NO VERIFICADAS**.

El prefab del jugador trae el aviso de interacción. La escena de partida
incorpora pausa, derrota y victoria mediante PauseMenuCanvas; el menú principal abre
Level 1. MenuBehaviour administra navegación, tiempo y cursor, mientras
MenuOptions conecta los controles de volumen, resolución y pantalla completa.
La configuración está en [Flujo de partida](FLUJO_PARTIDA_MENUS.md).
EnemySpawner genera las [oleadas configurables](OLEADAS.md) de Level 1 y
otorga la victoria cuando se elimina el último enemigo de la última oleada.

Estos componentes viven fuera del núcleo UPP. Movimiento, Animator, IK y debug
siguen sin administrar salud, daño, inventario, armas ni IA. Los controladores
de enemigos y salud del proyecto son otra capa de gameplay.

Quedan fuera de esta integración:

- melee e inventario general de objetos;
- armaduras, consumibles, durabilidad y accesorios;
- networking, guardado persistente y stacking;
- proyectiles físicos, penetración y coreografías Humanoid específicas de manos por mecanismo;
- carga de cartuchos uno a uno con efecto sobre munición y vainas como cuerpos
  rígidos de gameplay; los casquillos visibles de GunFX son partículas cosméticas;
- un acabado visual final del HUD y de los agarres. Existen el HUD de salud,
  la mira, el aviso de interacción, los menús y el HUD de munición serializado.

Los nombres históricos **FireMode**, **FiringMode** y **OnEnterFireMode**
significan aim direccional. El disparo lo resuelve PlayerWeaponController,
no esos estados de locomoción.

## Arquitectura de componentes

Equivalencias aproximadas para quien viene de Unreal:

| Unity/UPP | Responsabilidad | Equivalencia conceptual |
| --- | --- | --- |
| UPPCharacterMovementComponent | Input, estados, física, cápsula y política de root motion | Character Movement más parte del Character |
| UPPAnimatorControllerComponent | Asignación del controller, parámetros y peso de layers | Enlace con el Animation Blueprint |
| UPPIKCharacterComponent | Pies, pelvis, mirada de aim y lean | Anim Graph IK y Control Rig procedural |
| UPPGizmosDebugComponent | Visuales de movimiento, IK, cámara, trace y crosshair | Debug draw centralizado |
| UPPTPSCameraController | Cámara normal/aim, seguimiento y colisión | Camera Manager o camera rig TPS |
| UPPPlayerIntegration | Binding entre jugador, cámara, trace y debug | Capa de integración del Pawn |
| UPPAnimatorStateEvent | Eventos temporizados de estados del Animator | Notify o lógica de estado del Anim BP |
| PlayerInteractionController | Selección visible y recogida/intercambio con E | Componente de interacción |
| WeaponDefinition / WeaponItem | Preset, sprite y munición por instancia | Datos y estado del arma |
| PlayerWeaponController | Equipamiento, intercambio, recarga y disparo | Componente de armas |
| PlayerWeaponInventory | Seis slots, selección y conservación de instancias | Inventario de armas |
| PlayerWeaponHotbar | Fotografía, número, selección, nombre y munición por slot | Barra de selección de armas |
| WeaponMechanismAnimator | Curvas locales de piezas según disparo/recarga reales | Presentación mecánica del arma |
| PlayerWeaponPose | Manos hacia grips y retroceso visual por preset | Presentación de armas mediante IK |
| PlayerWeaponFeedback | Audio de disparo y recarga mediante eventos | Presentación sonora de combate |
| WeaponVfxProfile / WeaponVfxPresenter / WeaponVfxPool | Fogonazos, vainas e impactos cosméticos | Presentación VFX por arma y escena |
| WeaponImpactSurface | Tipo de superficie del collider | Selección del efecto sin alterar física |
| PlayerWeaponHUD | Nombre, cargador, reserva y estado mediante eventos | Presentación de combate |
| PlayerHealth / PlayerDeathController | Salud y respuesta a la derrota | Estado y presentación de muerte |
| MenuBehaviour / MenuOptions | Navegación, pausa, derrota y opciones | Coordinación de pantallas de partida |

Flujo principal:

~~~text
UPPPlayerCharacterInputs.asset
  -> UPPCharacterMovementComponent
      -> Rigidbody + CapsuleCollider
      -> UPPAnimatorControllerComponent
      -> UPPIKCharacterComponent
      -> UPPGizmosDebugComponent

UPPPlayerIntegration
  -> reutiliza o crea UPPThirdPersonCamera
  -> UPPTPSCameraController
      -> Camera normal / Camera de aim TPS
      -> UPPAimTrace
      -> UPPDebugCrosshair
      -> UPPGizmosDebugComponent
~~~

El núcleo matemático y físico reutilizable está en
[UPPCharacterMovementCore.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Internal/UPPCharacterMovementCore.cs>).
No es otro componente para agregar al prefab: es la clase base de
UPPCharacterMovementComponent.

## Controles de PC

| Acción | Control | Comportamiento del prefab |
| --- | --- | --- |
| Movimiento | WASD | Dirección relativa a cámara |
| Cámara | Mouse | Cursor bloqueado durante el juego |
| Sprint | Shift | Se mantiene mientras hay dirección válida |
| Salto | Espacio | Solo si locomoción y postura lo permiten |
| Crouch | C | Alterna crouch/levantarse |
| Prone | Z | Alterna prone/salida |
| Roll | Ctrl izquierdo | Activa el trigger Roll |
| Aim TPS | RMB | Toggle con OnePressToAim |
| Disparo | Mantener LMB | Repite en los ocho presets con aim, arma equipada y munición; también Right Trigger |
| Recoger / guardar o reemplazar | E | Primer slot libre y autoequipar; si está lleno reemplaza el seleccionado, con alcance/visibilidad existentes |
| Seleccionar slot | 1–6 / Numpad 1–6 | Incluye vacíos: deja al personaje desarmado sin borrar las otras armas |
| Recorrer armas | Rueda arriba/anterior, abajo/siguiente | Omite slots vacíos; D-pad Left/Right en gamepad |
| Recargar | R / automática al vaciarse | Ejecuta recarga Humanoid y transfiere reserva al completar la duración; R también Button West |
| Soltar arma seleccionada | G | Deja sólo esa instancia delante del jugador conservando munición; también D-pad Down |
| Pausa | Esc | Alterna pausa/reanudar; también Start en gamepad |

El prefab usa AutoRun y WalkOnRunButton desactivado: WASD entra en velocidad de
carrera y Shift agrega sprint. AutoRun no mueve al personaje sin input.

## Componentes requeridos

El root del jugador necesita:

- Rigidbody, CapsuleCollider y Animator con Avatar Humanoid;
- UPPCharacterMovementComponent;
- UPPAnimatorControllerComponent;
- UPPIKCharacterComponent;
- UPPGizmosDebugComponent;
- UPPPlayerIntegration.

El prefab también incluye PlayerWeaponController, PlayerWeaponInventory, PlayerWeaponPose,
PlayerWeaponFeedback, PlayerInteractionController, PlayerHealth y
PlayerDeathController, junto al Canvas del aviso de interacción.
WeaponSystemSetup agregó PlayerWeaponHUD como hijo de ese Canvas, con todas
sus referencias serializadas y verificadas el 19/10/2026.
Estas capas de gameplay no son requisitos del núcleo de movimiento UPP.
WeaponInventorySetup integra PlayerWeaponHotbar como WeaponHotbar dentro del
mismo Canvas; seis tarjetas muestran fotografía, número, nombre, cargador/reserva
y selección. Las imágenes apuntan a WeaponDefinition.Icon y se ocultan en slots vacíos.
El cambio de slot respeta pausa/cursor, muerte, roll y controles deshabilitados.
Los ocho wrappers de armas y el rifle histórico incorporan WeaponMechanismAnimator,
que muestrea sus clips de piezas en LateUpdate 1005, antes de la pose del jugador.
ShotPresented inicia un ciclo por disparo físico y ReloadProgress conduce la
recarga visual. No administra balas ni daño. Devuelve las piezas a reposo al
cancelar, guardar, soltar, morir o deshabilitar; la pausa congela la fase.
WeaponDefinition.VfxProfile referencia ocho perfiles GunFX. Cada arma incorpora
WeaponVfxPresenter, que recibe un ShotPresented por tiro físico e ImpactPresented
por RaycastHit real. Usa ShellEjectionPort para las vainas, normal real para
impactos y seis tipos de WeaponImpactSurface; respeta las partículas de impacto
propias de EnemyHealth. El revólver conserva vainas hasta el 39 % de la recarga
sin duplicarlas si ésta se cancela después. Shotgun y rifles expulsan durante
la palanca/cerrojo, con retardo configurado por modelo. WeaponVfxPool comparte
128 entradas por escena y tiempo escalado; no modifica daño, munición ni spread.
PlayerWeaponPose registra la evaluación fresca del Animator desde OnAnimatorIK
y ajusta los huesos reales en LateUpdate con orden 1050, después de cámara/trace
y antes del disparo en 1100. Orienta la muñeca derecha y resuelve el brazo
izquierdo con dos segmentos rígidos, sin escalado. Admite de pie, crouch y prone;
libera las manos durante recarga, roll, muerte o controles deshabilitados.
El Animator del jugador usa AlwaysAnimate y Legs.mask conserva su configuración
original. WeaponAudio contiene fuentes
separadas de disparo y recarga, conservando el audio de pasos existente.
PlayerWeaponFeedback recibe ShotFired una vez por tiro físico y presenta tres
capas simultáneas OneShot/Stop/Tail en Shots, con pitch 1, ganancias por preset
y variantes elegidas por System.Random local sin repetición inmediata cuando
hay alternativas. No usa Dist para el arma local cercana. Su LateUpdate 1200
pausa/reanuda las fuentes después del controlador 1100; no altera el RNG de
spread ni controla munición, cadencia o mecanismos visuales.
ReloadAudioCues presenta una sola vez cada fase alcanzada por ReloadProgress,
con ganancia por cue y ReloadVolume general. Cancelar, cambiar, soltar,
morir o deshabilitar corta la fuente; ReloadCompleted permite terminar la cola
natural al completar y recuperar fases saltadas por un frame largo. La pausa
congela progreso y fuentes. ReloadSound es respaldo sin cues válidos y también
usa pitch 1. El setup preserva clips personalizados y no regenera el placeholder.
Los cuatro cortes PCM16 a 48 kHz usan DecompressOnLoad/preload; los disparos
conservan su importación ADPCM. Hay 125 AudioClip actuales: 102 de disparo,
19 de locomoción y cuatro de recarga; el conteo 121 de la poda es histórico.
PlayerDeathController detiene controles, cuerpo, Animator y cámara de seguimiento
al recibir OnDeath; la Camera y el AudioListener siguen presentando la escena.

UPPCharacterMovementComponent declara RequireComponent para Rigidbody,
Animator, CapsuleCollider, UPPAnimatorControllerComponent y
UPPIKCharacterComponent. UPPGizmosDebugComponent es opcional para otros
prefabs, aunque forma parte del baseline. Esta protección no reemplaza la
configuración de referencias, máscaras y Avatar.

El rig canónico de cámara incluye:

- un root con UPPTPSCameraController;
- una Camera hija marcada como MainCamera;
- UPPAimTrace y UPPDebugCrosshair en la Camera gameplay;
- UPPApplyCameraSettings conectado al controlador de cámara.

UPPPlayerIntegration comparte el asset de input entre jugador y cámara, asigna
el target de seguimiento al último hueso de columna disponible, excluye al
jugador del trace y entrega cámara, trace y crosshair al componente de debug.
También asigna el controlador a MyPivotCamera para que el movimiento sea
relativo a la cámara.

UPPPlayer.prefab tiene cameraRigPrefab configurado y createCameraIfMissing
activo. Al iniciar reutiliza primero un UPPTPSCameraController compatible que
ya exista; si no encuentra ninguno, crea UPPThirdPersonCamera como root de la
escena. No convierte la cámara en hija del jugador y no duplica MainCamera ni
AudioListener. Si una cámara compatible carece de UPPAimTrace o
UPPDebugCrosshair, la integración completa esos dos componentes.

## Integrar una escena nueva

1. Instanciar [UPPPlayer.prefab](<D:/SurvivePrototype/Assets/Prefabs/Player/UPPPlayer.prefab>).
2. No es necesario instanciar la cámara manualmente. UPPPlayerIntegration crea
   el rig configurado al entrar en Play Mode. Si la escena ya tiene un rig UPP
   compatible, lo reutiliza.
3. Confirmar que el jugador tenga tag Player. El prefab de cámara canónico ya
   entrega una Camera gameplay con tag MainCamera.
4. Asignar
   [UPPPlayerCharacterInputs.asset](<D:/SurvivePrototype/Assets/Settings/Input/UPPPlayerCharacterInputs.asset>)
   a UPPCharacterMovementComponent. La integración lo comparte con la cámara.
5. Usar un Avatar Humanoid con columna, cadera, cabeza y ambos pies mapeados.
6. Configurar explícitamente las máscaras de suelo, pared, escalones, foot IK,
   colisión de cámara y aim trace. La máscara de suelo no debe incluir la layer
   física del jugador.
7. Confirmar que UPPAnimatorControllerComponent tenga asignado
   [UPPAnimatorTPS.controller](<D:/SurvivePrototype/Assets/Animations/Player/Controllers/UPPAnimatorTPS.controller>)
   y que su ParameterMap coincida con el controller.
8. Ejecutar ValidateAnimatorContract como diagnóstico de integración. Comprueba
   los parámetros usados y ambos índices de layer; no sustituye la revisión
   visual de transiciones, clips ni máscaras.
9. Para una escena de partida, agregar PauseMenuCanvas y un EventSystem con
   InputSystemUIInputModule. Verificar container, gameOverContainer y los
   botones de MenuBehaviour. PlayerDeathController busca un menú de partida
   en su misma escena si no tiene una referencia explícita.
10. Probar la escena en Play Mode. Compilar o copiar assets no valida blends,
    máscaras, colisiones ni calidad de movimiento.

Para reproducir la integración de armas, ejecutar
WeaponFeelSetup.CreateAndIntegrate() después de importar RIFLE PRO V2.7A2.
El setup vincula el idle original a Strafe Torso Pose y la recarga real
W2_Stand_Aim_Reload_IPC a Weapon Actions. Esa capa tiene máscara de torso,
brazos y manos, etiqueta WeaponReload y peso inicial 0; el controlador activa
su peso durante recarga y salida, con WeaponReloading/WeaponReloadSpeed.
reloadAnimationClip.length determina la velocidad respecto de ReloadDuration.
La máscara mantiene raíz y piernas bajo la locomoción existente.
La entrada central llama a WeaponPresentationSetup.CreateAndIntegrate(), que
ejecuta WeaponMechanismSetup, WeaponIconSetup y WeaponInventorySetup en ese
orden. Los primeros crean 18 clips locales bajo Assets/Animations/Weapons y
ocho sprites bajo Assets/UI/WeaponIcons, fotografiando el arte real en preview.
La última vincula inventario, controlador e interacción y crea la hotbar en el
Canvas existente; después ejecuta WeaponAudioSetup, que vincula los ocho
perfiles del paquete Modern Weapons previamente importado y conserva audio
personalizado externo. No genera WAV ni sonido provisional de recarga.
WeaponVfxSetup.CreateAndIntegrate() es una integración explícita separada:
adapta 17 prefabs GunFX URP sin alterar los originales, crea ocho perfiles y
agrega una DecalRendererFeature si falta en PC_Renderer y Mobile_Renderer,
sin reemplazar las otras features. AssetAudioVfxAudit.ScanOnly() inventaría
dependencias sin borrar; PruneApproved() exige selección explícita y respaldo
externo verificado, y vuelve a validar hashes y grafo antes de eliminar.
Los 24 prefabs nativos GunFX conservan su SHA-256; los 32 materiales sí
recibieron la migración URP de serialización 9 a 10, preservando GUID,
shader, texturas y colores. No se afirma identidad binaria de los 155 assets
después de importar en Unity.
La entrega anterior de inventario pasó 66 comprobaciones; esa cifra no valida
los mecanismos ni las imágenes nuevos.

## Aim, cámara y diagnóstico central

Aim no es ADS en primera persona. RMB cambia a AimCameraState, que mantiene el
mismo target y acerca la cámara de 3 a 1,5 unidades. No busca un socket del
arma.

UPPAimTrace calcula un rayo mediante ViewportPointToRay(0.5, 0.5), descarta la
jerarquía del jugador y conserva el impacto externo más cercano. El crosshair
se dibuja en el mismo centro y, por defecto, solo se muestra durante aim.

Trace y crosshair son herramientas geométricas y no aplican daño. La capa de
armas usa la cámara para seleccionar el objetivo y comprueba por separado el
trayecto del disparo desde el cañón, incluyendo obstáculos cercanos. Apagar el
debug no deshabilita el disparo.

## Root motion

El baseline no usa full root motion:

- UPPCharacterMovementComponent.RootMotion comienza desactivado;
- con RootMotion desactivado, la locomoción común traslada el Rigidbody;
- con RootMotion activado, la locomoción libre grounded obtiene deltaPosition
  del Animator, lo proyecta sobre el plano del personaje y lo convierte en
  linearVelocity;
- RootMotionSpeed escala esa traslación;
- RootMotionRotation permite aplicar deltaRotation con Rigidbody.MoveRotation;
- aim continúa usando movimiento direccional físico;
- aire, salto y roll no reciben traslación de root motion.

Ya no existe un behaviour separado que active root motion por estado. El flag
público puede configurarse en el componente o ser controlado por una futura
capa externa, pero esa política no está implementada por el Animator actual.

## Debug visual

UPPGizmosDebugComponent es el punto de control:

- DrawDebug es el interruptor maestro;
- OnlyInEditMode oculta todos sus visuales durante Play Mode;
- los toggles granulares controlan ground, pared, escalones, foot IK, body
  placement, lean, cámara, trace y crosshair;
- al deshabilitar el componente dejan de dibujarse sus gizmos de cámara y
  trace, y también se oculta el crosshair externo.

Apagar DrawAimTrace solo oculta la línea: UPPAimTrace sigue calculando
HasHit, LastHit, AimPoint y LastRay. Apagar DrawCrosshair solo oculta la cruz.
La toolbar Gizmos de cada vista sigue controlando los gizmos nativos de Unity;
el crosshair de OnGUI no depende de esa toolbar.

PlayerWeaponController tiene una mira simple propia para el arma equipada,
independiente de DrawDebug. Mientras esa mira está activa, oculta temporalmente
UPPDebugCrosshair y restaura su estado al deshabilitarse o soltar el arma.
La mira da feedback de impacto y recarga y abre con el retroceso del tiro.
El HUD de arma se alimenta de
WeaponChanged y WeaponStateChanged, sin agregar estado de munición a UPP.

WeaponFeelSetup deja DrawDebug desactivado en el prefab para presentar el
combate. Los toggles granulares conservan sus valores; activar DrawDebug
permite recuperar el diagnóstico sin cambiar disparos ni daño.

## Validación

**GunFX URP y limpieza audio/VFX del 24/10/2026:** importación VERIFICADA de
155 assets (aproximadamente 161 MiB), 87 carpetas y 397 archivos del cierre
de 17 prefabs fuente, sin HDRP/Built-in/audio. El setup serializado pasó con
ocho perfiles, 17 adaptadores, seis superficies y decals en ambos renderers,
exit code 0; [WeaponVfx-Setup.log](<D:/SurvivePrototype/Logs/WeaponVfx-Setup.log>).
El escaneo pasó con cero bloqueos: 1.273 candidatos/632,29 MiB y 121 clips
clasificados como retenidos (102 Modern Weapons y 19 de locomoción).
**Poda PASS: 1.273 assets y sus metas eliminados, cero fallos**, exit code 0,
en [WeaponVfx-Cleanup-Final.log](<D:/SurvivePrototype/Logs/WeaponVfx-Cleanup-Final.log>).
Se verificaron los paths eliminados y 102 WAV Modern Weapons restantes;
hay respaldo externo completo con hashes. **Runtime VFX: 229/229 PASS, exit code 0,
22 capturas y cero errores de gameplay/red**, del 24/10/2026 a las 13:55 de
Buenos Aires. Comprueba ocho perfiles, seis superficies, vainas y cancelaciones,
revólver sin duplicación, pausa, pool/descarga de escena y efectos visibles sin
shaders rosas. El fixture de Level 1 usa LMB real de Input System con entrada
sintética, foco/cursor temporal y colliders/cámaras aislados; las capturas de
superficies usan layer 30 y fondo oscuro sin resimular las partículas.
Log: [WeaponVfx-Validation-Final.log](<D:/SurvivePrototype/Logs/WeaponVfx-Validation-Final.log>).
Las 22 capturas finales se revisaron sin defectos materiales detectados ni
shaders rosas, con impactos visibles en el fixture aislado.
**Compilación final sin helpers PASS, exit code 0**:
[WeaponVfx-FinalCompile.log](<D:/SurvivePrototype/Logs/WeaponVfx-FinalCompile.log>).
El escaneo posterior registró cero candidatos, 275 recursos retenidos,
cero bytes candidatos y cero bloqueos. No es una sesión manual general de
Level 1 ni una build distribuida.
El grafo protege referencias productivas, demos supervivientes, fuentes de
regeneración, Resources/Addressables y recursos compartidos fuera de VFX.
Los 632,29 MiB corresponden a assets/metas retirados, sin medir Library.
La comprobación posterior verificó 2.546 archivos de respaldo sin diferencias,
102 WAV originales y 121 AudioClip retenidos sin cambios, y cero referencias
serializadas a GUID eliminados. Los 28 prefabs VFX Synty eliminados no forman
una limpieza general del arte ni invalidan la evidencia histórica de armas.
Detalle, respaldo y reportes en [Ítems y armas](ITEMS_ARMAS.md).
Se retiraron también 32 carpetas vacías de audio/VFX y sus metas, aparte del
conteo de assets. Manifest/lockfile de Packages quedaron restaurados exactamente
y ambos helpers UPM externos se retiraron antes de la compilación final.
Level 1, manifest.json y packages-lock.json conservaron SHA-256 exactos contra
el respaldo previo a esta ampliación VFX.

**Audio Modern Weapons del 24/10/2026: 178/178 PASS después de la poda**,
exit code 0, cero errores de gameplay/red y 17 capturas WAV con AudioRenderer
offline real, 48 kHz estéreo. Las ocho ráfagas tuvieron picos 0,2475–0,4633,
sin clipping; se comprobaron master, perdigones, pausa, variantes e idempotencia.
El fixture aisló las otras fuentes, adaptó foco/cursor e inyectó input en
Level 1 sin guardar assets. No equivale a escucha manual.
Log: [WeaponAudio-Validation-Final.log](<D:/SurvivePrototype/Logs/WeaponAudio-Validation-Final.log>).
La ejecución previa de 159 sin muestras no prueba salida; la final de 178 sí
capturó muestras del motor. La compilación final sin helpers pasó con exit code 0.
**Escucha manual y build distribuida NO VERIFICADAS.** Se importaron inicialmente
1.318 WAV originales (aproximadamente 730 MiB) y 36 carpetas con sus metas,
sin scripts/demos/mixer. El archivo fuente indica v1.2 y sus metadatos internos
packageVersion 1.1. Ninguna cifra del 19/10 verifica el audio nuevo; el detalle
de perfiles, procedencia y límites está en [Ítems y armas](ITEMS_ARMAS.md).

**Evidencia histórica de mecanismos e imágenes: 142/142 PASS, exit code 0**, el 19/10/2026 entre las
10:02:46 y 10:02:51 de Buenos Aires. El run registró cero errores de gameplay
y cero errores de red del Editor; generó 26 capturas, 24 de reposo/disparo/recarga
y dos de UI a 1920×1080/1280×720. Comprobó ocho sprites RGBA e imágenes de slots,
piezas móviles por LMB real de Input System, cadencia/reposo sin drift,
recarga/pausa/cancelaciones y ausencia del audio provisional. Shotgun rota su
palanca unos 52° sin traslación de bombeo; conserva el apoyo fijo. Los 30 prefabs
Synty originales comparados y Level 1 mantuvieron sus hashes.

Se revisaron las ocho fotografías, la hotbar en ambas resoluciones y las 24
capturas de mecanismos de los ocho presets, sin problemas materiales detectados.
Las fases tempranas de algunas capturas de disparo se complementan con las
mediciones por frame. Los closeups aíslan el arma y las imágenes de UI usan cámara/fondo
temporales; no prueban una sesión manual ni una vista completa de gameplay.
El run usa input sintético, foco/cursor adaptados para batch y enemigos
deshabilitados. Los mecanismos no sustituyen las manos Humanoid compartidas
de RIFLE PRO ni implementan munición por cartucho. La compilación final sin
helper terminó **PASS, exit code 0** en
[WeaponPresentation-FinalCompile.log](<D:/SurvivePrototype/Logs/WeaponPresentation-FinalCompile.log>).
El helper fue retirado y manifest/lockfile se restauraron con hashes exactos,
sin diferencias de Git. Reporte y límites en [Ítems y armas](ITEMS_ARMAS.md).
Las cifras siguientes son históricas y no se suman a esta cobertura.

**Entrega anterior, inventario/hotbar y recarga sin placeholder: 66/66 PASS, exit
code 0, el 19/10/2026 a las 09:43:40–09:43:42 de Buenos Aires.** El reporte
registró cero excepciones de gameplay y cero errores de red del Editor.
Cubrió E con capacidad libre/llena, seis instancias con munición conservada,
1–6/rueda/D-pad, vacíos, drop G, destrucción externa, bloqueos, muerte y
reintento real; también LMB sostenido, cambios durante Fire y recarga
manual/automática con la animación nativa silenciosa.
Las cuatro capturas revisadas comprueban textos, estados y layout de UI a
1920×1080/1280×720, sin superposición con los HUD existentes. Su cámara y
RenderTexture temporal muestran fondo azul; no son prueba del fondo de juego
ni de una sesión manual. Se usó input sintético con adaptaciones temporales
de foco/cursor/cámara; el SHA256 de Level 1 no cambió.
Se retiró hotbar-validation, manifest/lockfile se restauraron exactamente
contra sus backups y la compilación final sin el paquete temporal terminó
**PASS, exit code 0** en [Hotbar-FinalCompile.log](<D:/SurvivePrototype/Logs/Hotbar-FinalCompile.log>).
Level 1 conserva el SHA256 del baseline. Sesión manual, escucha y build
empaquetada siguen **NO VERIFICADAS**. Ver el reporte nuevo en
[Ítems y armas](ITEMS_ARMAS.md). Las cifras siguientes son históricas.

La entrega inicial del 19/10/2026 pasó compilación con Unity 6000.3.5f2,
setup de ocho presets/prefabs y HUD, integración de tres pickups de Level 1 y
17/17 grupos controlados de Editor y Play Mode. Cubrieron munición, recarga,
intercambio, obstáculos, daño, escopeta, input, pausa y muerte. Se inspeccionaron
dos capturas controladas de aim con rifle/pistola. La escena agregó 231 líneas
y no eliminó ninguna frente al backup anterior a la tarea. Después de retirar
el paquete externo de pruebas y restaurar manifest/lockfile, la compilación
final terminó PASS, exit code 0. Ver el detalle en [Ítems y armas](ITEMS_ARMAS.md).
No se verificó una sesión manual completa de Level 1 ni una build empaquetada.

La ampliación histórica de LMB sostenido, recarga automática, animación
original RIFLE PRO, hand IK, retroceso y audio pasó **169/169 comprobaciones**
en Play Mode controlado el 19/10/2026, con **44 capturas generadas**. Se verificaron los
ocho presets, recarga y reanudación, agarre/alineación, daño y obstáculos,
movimiento de pie/crouch/prone, pausa y cancelación por cambios de estado.
Se revisaron las vistas frontal/lateral de aim de las ocho armas y recargas
seleccionadas. Prone pasó mediciones numéricas de pose/daño; su captura quedó
fuera de encuadre y no prueba visualmente esa postura. El reporte registró
cero errores de gameplay y un timeout
de red del Editor (SocketException/WebException ConnectFailure) de origen
no demostrado, preservado por separado.

La prueba usó entrada sintética y adaptaciones temporales de foco/cursor en
batch, enemigos deshabilitados y un rango aislado para mediciones; no fue una
sesión manual completa. La compilación final después de retirar el paquete
temporal terminó **PASS, exit code 0**, en
[WeaponFeel-FinalCompile.log](<D:/SurvivePrototype/Logs/WeaponFeel-FinalCompile.log>).
Manifest/lockfile se restauraron con SHA256 idéntico al backup y el SHA256 de
Level 1 sigue idéntico al anterior a esta ampliación. Escucha manual y build empaquetada siguen
**NO VERIFICADAS**. Ver reporte y condiciones en [Ítems y armas](ITEMS_ARMAS.md).

No se guardan suites, scripts ni assemblies de test dentro del repositorio.
Las comprobaciones temporales deben ejecutarse desde una ubicación externa y
eliminarse al terminar.

Para validar una entrega:

1. compilar y confirmar que no haya errores C#;
2. iniciar en SceneAlejandro, entrar a Level 1 y verificar referencias serializadas;
3. probar manualmente movimiento, posturas, salto, roll y pendientes;
4. probar cámara normal/aim, colisión, trace y crosshair;
5. revisar los blends de aim, foot IK, pelvis, lean y cápsula;
6. probar root motion con y sin rotación si se modifica esa política;
7. probar seis slots, hotbar, E con capacidad libre/llena, 1–6 y rueda, slot
   vacío y G sólo sobre el seleccionado; después socket, aim, disparo, cargador, reserva,
   LMB sostenido, recarga automática/R, animación real, salida, manos, retroceso,
   disparos audibles, cues de recarga sincronizados y HUD según [Ítems, pickup y armas](ITEMS_ARMAS.md);
8. probar pausa, derrota, reintento, vuelta al menú y opciones según
   [Flujo de partida](FLUJO_PARTIDA_MENUS.md).

Una compilación correcta no demuestra que el feel, las animaciones o la
serialización estén bien.

## Reglas de mantenimiento

- Toda documentación propia permanece bajo Docs, fuera de Assets.
- No guardar archivos de test dentro del proyecto ni del repositorio.
- Todos los comentarios nuevos de código se escriben en español.
- No agregar dependencias de daño, ítems o armas al movimiento.
- Aim, trace y crosshair no deben asumir que existe un arma.
- Todo cambio de input, prefab, Animator, IK, cámara o debug debe actualizar
  estas guías en la misma tarea.
- Un script compilado no se considera integrado hasta estar serializado y
  revisado en la escena activa.
