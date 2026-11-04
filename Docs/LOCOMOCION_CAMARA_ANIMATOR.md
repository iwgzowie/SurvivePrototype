# Locomoción, cámara, Animator, IK y debug

## Contrato del sistema

Esta guía describe el runtime usado por
[UPPPlayer.prefab](<D:/SurvivePrototype/Assets/Prefabs/Player/UPPPlayer.prefab>) en
[Playground](<D:/SurvivePrototype/Assets/StarterAssets/ThirdPersonController/Scenes/Playground.unity>).
La base es singleplayer, usa teclado y mouse y no depende de sistemas de
combate. El componente externo PlayerWeaponController reutiliza sus estados
de aim y su cámara para el equipamiento y disparo descritos en
[Ítems, pickup y armas](ITEMS_ARMAS.md).

Assets principales:

- [UPPPlayer.prefab](<D:/SurvivePrototype/Assets/Prefabs/Player/UPPPlayer.prefab>)
- [UPPThirdPersonCamera.prefab](<D:/SurvivePrototype/Assets/Prefabs/Cameras/UPPThirdPersonCamera.prefab>)
- [UPPAnimatorTPS.controller](<D:/SurvivePrototype/Assets/Animations/Player/Controllers/UPPAnimatorTPS.controller>)
- [UPPPlayerCharacterInputs.asset](<D:/SurvivePrototype/Assets/Settings/Input/UPPPlayerCharacterInputs.asset>)

Componentes centrales:

- [UPPCharacterMovementComponent.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Character/UPPCharacterMovementComponent.cs>)
- [UPPAnimatorControllerComponent.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Character/UPPAnimatorControllerComponent.cs>)
- [UPPIKCharacterComponent.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Character/UPPIKCharacterComponent.cs>)
- [UPPGizmosDebugComponent.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Character/UPPGizmosDebugComponent.cs>)
- [UPPPlayerIntegration.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Character/UPPPlayerIntegration.cs>)
- [UPPTPSCameraController.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Camera/UPPTPSCameraController.cs>)

La implementación física compartida vive en
[UPPCharacterMovementCore.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Internal/UPPCharacterMovementCore.cs>).
Es una clase base abstracta, no un componente adicional del prefab.

## Separación de responsabilidades

| Componente | Responsabilidad |
| --- | --- |
| UPPCharacterMovementComponent | Entrada, estados de locomoción, física, postura, cápsula y root motion |
| UPPAnimatorControllerComponent | Controller asset, mapa de parámetros y pesos de layers |
| UPPIKCharacterComponent | Foot IK, pelvis, adhesión opcional, look-at de aim y body lean |
| UPPGizmosDebugComponent | Interruptor maestro y visualizaciones granulares |
| UPPPlayerIntegration | Binding jugador-cámara-trace-crosshair-IK-debug |
| UPPTPSCameraController | Seguimiento, rotación, colisión y estados Normal/Aiming |

Los cuatro primeros componentes están separados para que movimiento, animación,
IK y diagnóstico puedan configurarse sin concentrar todas las variables en una
sola clase. UPPCharacterMovementComponent exige por RequireComponent Animator,
Rigidbody, CapsuleCollider, UPPAnimatorControllerComponent y
UPPIKCharacterComponent. El componente de debug es opcional fuera del prefab
baseline.

## Orden de ejecución

UPPCharacterMovementComponent tiene orden -1000:

| Ciclo | Responsabilidad |
| --- | --- |
| Awake | Cachear referencias, crear la dirección auxiliar e inicializar cápsula |
| Update | Input y eventos |
| FixedUpdate | Suelo, pared, escalones, rotación, cápsula y traslación física |
| OnAnimatorMove | Convertir delta de root motion en velocidad física |

UPPAnimatorControllerComponent tiene orden -500 y publica parámetros y peso de
la layer de piernas en Update. El prefab usa Animator Normal y Rigidbody
Interpolate: la animación acompaña cada frame y la física conserva su paso de
0,02 s. La rotación usa MoveRotation; escalones y pendientes usan MovePosition,
sin sobrescribir el Transform interpolado.

UPPIKCharacterComponent tiene orden -250:

| Ciclo | Responsabilidad |
| --- | --- |
| OnAnimatorIK, Base | Mirada; pies y cuerpo si Legs no aporta peso |
| OnAnimatorIK, Legs activa | Pies y cuerpo sobre la zancada direccional final |
| LateUpdate | Lean procedural sobre la pose ya evaluada |

UPPPlayerIntegration resuelve un rig existente o crea UPPThirdPersonCamera
desde el prefab configurado en Awake. Después enlaza referencias al iniciar y
reintenta en LateUpdate hasta que la cámara esté disponible. La cámara lee look
y cambia de estado en Update. Con orden 100, sigue el pivot y aplica posición,
colisión y FOV en LateUpdate, después de la animación/IK y antes del trace. El
seguimiento usa deltaTime y suavizado exponencial por frame.

## Input activo

| Acción | PC | Lectura |
| --- | --- | --- |
| Move | WASD | Vector2 normalizado |
| Look | Mouse delta | Vector2 procesado por el asset |
| Jump | Espacio | Flanco de presión |
| Run | Shift izquierdo | Mantenido |
| Aim | RMB | Mantenido y flanco de presión |
| Crouch | C | Flanco de presión |
| Prone | Z | Flanco de presión |
| Roll | Ctrl izquierdo | Flanco de presión |

El asset de locomoción contiene bindings alternativos de gamepad, pero el
baseline está definido para PC. Sus ocho acciones no incluyen disparo,
recarga, interacción, melee, inventario ni slots. PlayerWeaponController
administra una acción Fire independiente con New Input System, vinculada por
defecto al botón izquierdo del mouse. El pickup es automático por trigger.

SetActiveInputs habilita o deshabilita las ocho acciones como grupo. Al salir
de Play Mode, el asset las deshabilita para evitar estados retenidos en el
Editor.

### Semántica del prefab

El prefab usa:

- UseDefaultControllerInput activado;
- AutoRun activado;
- WalkOnRunButton desactivado;
- SprintOnRunButton activado;
- UnlimitedSprintDuration activado;
- AimMode en OnePressToAim.

Por eso, WASD usa velocidad de carrera y Shift agrega sprint. AutoRun no genera
movimiento sin input. RMB alterna aim; para apuntado mantenido hay que usar
HoldToAim.

## Movimiento y estados

UPPCharacterMovementComponent expone las reglas editables. El core resuelve
las operaciones físicas, pero solo el componente concreto lee input y coordina
el ciclo Unity.

Valores principales del prefab:

| Campo | Valor |
| --- | ---: |
| Speed | 3 |
| WalkSpeed | 0,5 |
| CrouchSpeed | 0,5 |
| RunSpeed | 1,5 |
| SprintingSpeedMax | 3 |
| SprintingAcceleration | 2 |
| SprintingDeceleration | 0,6 |
| RotationSpeed | 5 |
| JumpForce | 3 |
| StoppingSpeed | 2 |
| AirInfluenceControll | 0,5 |
| MaxWalkableAngle | 45 |

Speed es la escala final. WalkSpeed, RunSpeed, CrouchSpeed y las velocidades de
aim son multiplicadores objetivo de VelocityMultiplier.

### Movimiento libre y aim

MovementMode.Free permite alternar aim. AlwaysInAimMode mantiene IsAiming,
FiringMode y su IK activos.

Fuera de aim, el personaje rota hacia DirectionTransform, calculado con cámara
e input. Con CurvedMovement se traslada sobre su forward y rota
progresivamente. Durante aim, el root mira al punto de cámara y el movimiento
es direccional, con:

- FireModeWalkSpeed;
- FireModeRunSpeed;
- FireModeCrouchSpeed.

FiringMode significa locomoción de aim. PlayerWeaponController consulta ese
estado desde la capa de gameplay y resuelve el disparo por separado.

Opciones relevantes:

- EnableAim, EnableRoll y EnableProne permiten cortar capacidades;
- BlockHorizontalInput y BlockVerticalInput filtran ejes;
- BlockFireModeOnCursorVisible cancela aim cuando el cursor está visible;
- SetMoveInput permite inyectar movimiento desde otra capa;
- DisableLocomotion bloquea movimiento y limpia aim salvo en
  AlwaysInAimMode;
- EnableMove restaura el permiso.

### Posturas y acciones

- C alterna crouch y levantarse.
- Z alterna prone y salida.
- Saltar requiere suelo, permiso, no estar en roll y no estar prone.
- Ctrl activa Roll si el Animator expone el trigger correcto.
- UPPAnimatorStateEvent puede bloquear/restaurar movimiento, rotación o IK de
  aim, y comenzar/finalizar roll en un tiempo normalizado.
- Existe un fail-safe configurable, de dos segundos en el prefab, para
  finalizar IsRolling si falta el evento del Animator. El evento normal puede
  cerrarlo antes y nunca levanta un DisableLocomotion activo.

Los valores 1, 2 y 3 del enum de eventos están reservados por compatibilidad con
eventos de armas ya eliminados.

### Suelo, paredes, pendientes y escalones

- GroundCheck usa OverlapBox, ignora triggers y descarta colliders propios.
- WhatIsGround debe excluir la layer física del jugador.
- WallAHeadCheck usa un raycast frontal y reduce la velocidad al detectar pared.
- Superar MaxWalkableAngle activa deslizamiento por la normal del suelo.
- La corrección de escalones tiene ramas grounded y ungrounded.
- Si las máscaras principales están vacías, el core intenta usar Default,
  Terrain y Walls. Es preferible configurarlas explícitamente.

## Cápsula adaptativa

La cápsula ya forma parte de UPPCharacterMovementComponent; no existe un
componente separado.

- CapsuleToResize referencia el CapsuleCollider principal.
- HeightOffset escala la distancia cabeza-pies.
- En prone o roll, la cápsula usa dirección Z y ProneAndRollCenterY.
- De pie o crouch usa dirección Y.
- En aire, el centro se aproxima mediante cabeza y punto medio de pies.
- CenterOffset permite un ajuste local.

Requiere Avatar Humanoid con cabeza y pies válidos. No hay chequeo de techo
antes de levantarse; debe verificarse manualmente en espacios bajos.

## Cámara TPS

[UPPTPSCameraController](<D:/SurvivePrototype/Assets/Scripts/Player/Camera/UPPTPSCameraController.cs>)
hereda transición y colisión de
[UPPCameraSystemLib.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Libraries/UPPCameraSystemLib.cs>).

El runtime usa dos estados:

| Estado | Condición | Distancia/FOV del prefab |
| --- | --- | --- |
| Normal | Sin aim | 3 / 50 |
| Aiming | IsAiming o FiringMode | 1,5 / 50 |

Aim mantiene TargetToFollow y solo interpola al estado de cámara configurado.
No se acerca a un socket, no usa mira de arma y no entra en primera persona.
La colisión usa CollisionLayers del CameraState actual.

TargetToFollow usa HumanoidSpine para seguir la posición del personaje. Con
FollowUpTarget desactivado, la orientación vertical se mantiene estable y no
hereda la inclinación animada o procedural de la columna. Con FollowUpTarget
activado, el controlador usa explícitamente el up del root del personaje.

Opciones importantes:

- FollowUpTarget usa el up del personaje;
- EnableAutoRotator realinea tras AutoRotateTime;
- GeneralSensibility y GeneralVerticalSensibility;
- InvertHorizontal e InvertVertical;
- LockCursor y HideCursor.

UPPApplyCameraSettings aplica sensibilidad e inversión desde UPPGameSettings.
Esas preferencias usan PlayerPrefs y la sensibilidad se limita entre 0,01 y 10.

UPPPlayer.prefab referencia UPPThirdPersonCamera mediante cameraRigPrefab y
tiene createCameraIfMissing activo. La integración busca primero una cámara UPP
existente para evitar duplicar MainCamera y AudioListener. Si no existe, crea
el rig como root de escena y conecta InputAsset, characterTarget,
TargetToFollow y MyPivotCamera. El rig no debe ser hijo del personaje.

## Trace y crosshair

[UPPAimTrace](<D:/SurvivePrototype/Assets/Scripts/Player/Aiming/UPPAimTrace.cs>) genera en LateUpdate
un rayo con ViewportPointToRay(0.5, 0.5). Usa RaycastNonAlloc con un buffer de
24 hits, ignora triggers, descarta la jerarquía del jugador y elige el impacto
externo más cercano.

Resultados públicos:

- HasHit;
- LastHit;
- AimPoint;
- LastRay.

El prefab usa distancia máxima 500 y máscara completa. AimPoint es el impacto o
el punto a distancia máxima si no hubo hit.

[UPPDebugCrosshair](<D:/SurvivePrototype/Assets/Scripts/Player/Aiming/UPPDebugCrosshair.cs>) dibuja
una cruz con OnGUI. Se muestra durante aim, queda blanca sin hit y verde con
hit. UPPPlayerIntegration llama Configure, conecta trace y cámara y fuerza la
política aim-only.

UPPPlayerIntegration también entrega el trace a UPPIKCharacterComponent. La
mirada usa AimPoint cuando está disponible y recurre a la dirección de cámara
si falta el trace.

La traza y el crosshair son diagnóstico y no infligen daño. PlayerWeaponController
resuelve la selección del objetivo desde la cámara y la obstrucción del cañón
por separado; el crosshair puede indicar un objetivo que esté tapado desde el
arma por un obstáculo cercano. Esa comprobación física pertenece al disparo.

## Animator Controller

El equivalente conceptual al Animation Blueprint es
[UPPAnimatorTPS.controller](<D:/SurvivePrototype/Assets/Animations/Player/Controllers/UPPAnimatorTPS.controller>).
UPPAnimatorControllerComponent centraliza:

- controllerAsset y assignControllerOnAwake;
- ParameterMap;
- cache de nombres y tipos para no escribir parámetros incompatibles;
- parámetros de locomoción;
- transición de peso de Legs Motion Layer;
- TriggerRoll, SetGroundContact y AssignController;
- ValidateAnimatorContract como comprobación mínima.

ValidateAnimatorContract comprueba los 14 parámetros usados y que BaseLayerIndex
y LegsLayerIndex existan. No valida transiciones, clips ni Avatar Masks.

### Layers

| Índice | Layer | Uso |
| ---: | --- | --- |
| 0 | Base Layer | Pose completa, locomoción y posturas |
| 1 | Legs Motion Layer | Movimiento direccional de piernas durante aim |

El peso de Legs Motion Layer interpola hacia 1 solamente si FiringMode está
activo, el personaje no está en roll y DisableAllMove está apagado. Fuera de
esas condiciones vuelve a 0 con layerBlendSpeed.

No hay split de upper body, layer de brazos, selección por tipo de arma ni IK
de manos.

### Parámetros runtime

| Tipo | Parámetros |
| --- | --- |
| Bool | Moving, Running, Grounded, Jumping, FireMode, Crouched, Prone |
| Float | Speed, Horizontal, Vertical, IdleTurn, MovingTurn, LandingIntensity |
| Trigger | Roll |

Los nombres viven en UPPAnimatorControllerComponent.ParameterMap y distinguen
mayúsculas de minúsculas. Horizontal y Vertical reciben movimiento relativo al
aim cuando FiringMode está activo y vuelven a cero fuera de aim.

### Cambiar la animación de aim

La pose actual del torso en **Strafe Torso Pose** es **Rifle Idle Aiming**.
El mismo clip ocupa las cuatro entradas de Speed (0 / 0,5 / 1 / 1,6). La capa
Legs Motion Layer aporta el movimiento direccional y el IK resuelve los pies.
El apuntado prone conserva su árbol y sus clips propios. El agarre preciso de
la mano izquierda necesita ajustarse al modelo del arma; no hay IK de manos.

1. Importar el clip como Humanoid y verificar el Avatar usado para retarget.
2. Abrir UPPAnimatorTPS.controller.
3. Localizar las transiciones condicionadas por FireMode y los blend trees de
   aim, incluidos Running FireMode Blend Tree y Prone FireMode BlendTree.
4. Reemplazar los motions de idle o dirección. En árboles 2D conservar
   Horizontal y Vertical y revisar los thresholds.
5. Mantener Legs Motion Layer en el índice indicado por
   ParameterMap.LegsLayerIndex y conservar su Avatar Mask.
6. Si se reemplaza el controller completo, asignarlo mediante controllerAsset o
   AssignController y actualizar ParameterMap.
7. Probar entrada/salida, idle, ocho direcciones, crouch, prone, salto y roll.

Los nombres Armed, Rifle o FireMode de los clips describen su origen o pose.
Cambiar esos clips modifica la animación; el equipamiento, la cadencia y el
daño siguen a cargo de WeaponItem y PlayerWeaponController. El socket de la
mano acompaña al rig, sin agregar IK de agarre ni una layer de disparo.

## IK y animación procedural

[UPPIKCharacterComponent](<D:/SurvivePrototype/Assets/Scripts/Player/Character/UPPIKCharacterComponent.cs>)
concentra todas las soluciones procedurales del personaje actual.

### Foot IK

- EnableFootPlacement es el permiso principal.
- GroundLayers define superficies válidas; vacío usa Default como fallback.
- RaycastHeight, RaycastMaxDistance, Radius y MaxStepHeight definen consultas.
- GlobalWeight controla la influencia final.
- UseDynamicFootPlacing toma la elevación de GetIKPosition de la evaluación
  actual; desactivado, consulta las curvas LeftFootHeight y RightFootHeight
  si existen.
- FootHeight es la altura mínima del objetivo y respeta también la distancia a
  la planta del Avatar; no depende de los ejes importados del hueso.
- FootHeightMultiplier escala la elevación animada; el prefab usa 1 para
  conservar la zancada.
- Cada pie resuelve su contacto independientemente. Se descartan triggers,
  colliders del jugador, pendientes no transitables y alturas fuera de alcance.
- Las rotaciones son correcciones de pendiente sobre la pose animada. Sólo se
  suaviza el desnivel, sin atrasar la zancada ni la traslación del personaje.
- SmoothTransitions habilita el suavizado; SpeedInTransition y
  SpeedOutTransition controlan la entrada y salida IK/FK.
- El foot placement se retira en aire, salto, roll y prone.

### Pelvis y adhesión

EnableDynamicBodyPlacing corrige animator.bodyPosition por el desnivel de los
contactos válidos, limitado por MaxBodyCrouchHeight. Suaviza el offset relativo,
sin filtrar la altura mundial de la animación ni restar elevación de zancada.
En suelo plano el offset de terreno es cero. GlobalWeight y EnableFootPlacement
también gobiernan esta corrección. BlockBodyPositioning se activa fuera de las
condiciones válidas y durante crouch. JustCalculateBodyPosition calcula sin aplicar.

El componente IK no desplaza el Transform ni el Rigidbody. La adhesión y las
reglas físicas pertenecen a UPPCharacterMovementComponent.

### Look-at de aim

EnableAimLookAt controla la mirada. AimIKTransitionSpeed interpola el peso hacia
FiringModeIK. LookAtBodyWeight y HeadIKBodyWeight reparten la influencia entre
cuerpo y cabeza; prone limita el peso corporal.

La solución usa el AimPoint del trace cuando existe. No resuelve manos, codos
ni grips de armas.

### Body lean

RootBone usa la columna (spine_01 en el prefab), sin modificar ancestros de
las piernas después de resolver los pies. RootBoneSpineLean lo habilita;
RootBoneLeanIntensity, RootBoneLeanSpeed, BlockForwardLeanWeight y AxisToLean
definen el feel.

El lean se apaga durante aim, roll o aire. RootBoneSpineMovement permite una
pequeña contribución vertical al frenar, acotada a 3 cm y aplicada antes del
solver de pies. No acumula desplazamiento entre frames.

## Root motion

La política actual es opcional, no full root motion:

- RootMotion está desactivado en UPPPlayer.prefab;
- en locomoción libre grounded, RootMotion desactiva la traslación común por
  Rigidbody y OnAnimatorMove consume animator.deltaPosition;
- el delta se proyecta sobre el plano del personaje;
- RootMotionSpeed escala la velocidad horizontal;
- se preserva la velocidad vertical del Rigidbody;
- RootMotionRotation aplica animator.deltaRotation mediante MoveRotation;
- aim usa movimiento direccional físico y no consume root motion;
- aire, salto y roll quedan fuera de la política de root motion;
- animator.applyRootMotion solo se marca durante suelo, fuera de salto, roll y
  aim.

UPPAnimatorStateEvent no activa root motion. Ya no existe un behaviour separado
para esa tarea. Si una feature futura alterna RootMotion por estado, debe
restaurar explícitamente la política al salir.

## Debug visual centralizado

[UPPGizmosDebugComponent](<D:/SurvivePrototype/Assets/Scripts/Player/Character/UPPGizmosDebugComponent.cs>)
es el único panel de control del diagnóstico del personaje.

### Interruptores

| Campo | Efecto |
| --- | --- |
| DrawDebug | Master de todos los visuales administrados |
| OnlyInEditMode | Los oculta mientras Application.isPlaying |
| DrawGroundCheck | Caja de detección de suelo |
| DrawWallCheck | Rayo frontal de pared |
| DrawStepCorrection | Volúmenes y hit de escalón |
| DrawFootIK | Rayos, hits y normales de ambos pies |
| DrawBodyPlacement | Posición calculada del cuerpo |
| DrawBodyLean | Arco de lean en Editor |
| DrawCamera | Gizmos de pivot y cámara |
| DrawAimTrace | Línea verde/cyan del trace |
| DrawCrosshair | Cruz OnGUI |

DrawDebug combina su valor con OnlyInEditMode. Los gizmos de movimiento, IK,
cámara y trace se dibujan únicamente dentro de este componente. OnDisable
oculta además el crosshair mediante DebugVisible; OnEnable y OnValidate vuelven
a aplicar esa configuración.

Los toggles visuales no detienen la lógica observada:

- DrawAimTrace no deshabilita RefreshTrace;
- DrawCrosshair no deshabilita aim;
- DrawFootIK no deshabilita el IK;
- DrawCamera no deshabilita seguimiento ni colisión.

Para desactivar una mecánica hay que usar el campo del componente responsable,
por ejemplo EnableFootPlacement o EnableAimLookAt.

WireColor se usa para escalones. La toolbar Gizmos de Scene/Game View sigue
siendo necesaria para los gizmos nativos; el crosshair OnGUI no depende de
ella.

PlayerWeaponController tiene una mira simple propia para el arma equipada,
independiente de DrawDebug. Mientras esa mira está activa, oculta temporalmente
UPPDebugCrosshair y restaura su estado al deshabilitarse o soltar el arma.

El prefab actual tiene master y toggles granulares encendidos. La forma rápida
de jugar sin ruido visual es apagar DrawDebug.

## Validación manual

El repositorio no conserva tests internos. Después de cambiar movimiento,
cámara, Avatar o Animator hay que revisar en Play Mode:

1. WASD, desaceleración, giro y sprint.
2. Salto, control aéreo, aterrizaje y ausencia de self-grounding.
3. Crouch, prone, salida y cápsula bajo techos.
4. Roll y retorno de movimiento e IK.
5. Pendientes, paredes y escalones.
6. Cámara normal, colisión y seguimiento durante aim.
7. RMB, crosshair centrado y LastRay desde el centro del viewport.
8. Blend direccional de aim en todas las posturas soportadas.
9. Pies, pelvis y lean sobre desniveles y cambios de velocidad.
10. Root motion con y sin rotación, sin doble desplazamiento.
11. Master y cada toggle granular de debug.

La compilación C# no prueba serialización, feel, blends ni calidad visual. Si
se crea una comprobación automatizada temporal, debe vivir fuera de Assets y
fuera del repositorio.
