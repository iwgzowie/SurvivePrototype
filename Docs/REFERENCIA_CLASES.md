# Referencia de clases de UPP y gameplay

## Alcance

Esta referencia cubre las clases propias de locomoción, cámara, Animator, IK,
diagnóstico y las capas externas de ítems, armas, salud y flujo de partida.
La documentación vive en Docs,
fuera de Assets. El uso de los prefabs y sus parámetros está en
[Ítems, pickup y armas](ITEMS_ARMAS.md) y
[Flujo de partida, menús y fin de partida](FLUJO_PARTIDA_MENUS.md).
La configuración y API de EnemySpawner y EnemyWave están en
[Oleadas de enemigos y victoria](OLEADAS.md).
La creación de perfil y apariencia modular Sidekick está documentada en
[Customización de personaje](PERSONAJES_CUSTOMIZACION.md).

Estados usados:

- **Activo:** serializado y parte del flujo del prefab o cámara.
- **Infraestructura:** clase base o datos consumidos por una clase activa.
- **Disponible:** API compilada que el baseline no invoca directamente.

Los nombres FireMode y FiringMode significan aim direccional por compatibilidad
con el Animator. El disparo pertenece a PlayerWeaponController y WeaponItem.

## Componentes principales del personaje

### UPPCharacterMovementComponent

Archivo:
[UPPCharacterMovementComponent.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Character/UPPCharacterMovementComponent.cs>)  
Namespace: UPP.ThirdPersonController  
Estado: **Activo**

Componente concreto de entrada, estados y física. Hereda
UPPCharacterMovementCore y requiere Rigidbody, Animator, CapsuleCollider,
UPPAnimatorControllerComponent y UPPIKCharacterComponent. El componente de
debug está presente en el prefab baseline, pero no es una dependencia física.

Responsabilidades:

- leer UPPPlayerCharacterInputAsset;
- caminar/correr, sprint, salto, crouch, prone y roll;
- alternar aim y locomoción direccional;
- coordinar suelo, paredes, pendientes y escalones;
- decidir traslación por Rigidbody o root motion;
- actualizar la cápsula según la pose en el paso de física;
- emitir eventos de locomoción.

Configuración propia:

- Inputs y UseDefaultControllerInput;
- AutoRun y WalkOnRunButton;
- BlockVerticalInput y BlockHorizontalInput;
- EnableRoll, EnableAim y EnableProne;
- BlockFireModeOnCursorVisible;
- CapsuleToResize, HeightOffset, ProneAndRollCenterY y CenterOffset.

API principal:

- ControllerInputs;
- DisableLocomotion y EnableMove;
- HeadFeetDistance;
- GetMiddlePointBetweenFeet;
- GetBodyCenter.

No conoce armas, daño, inventario ni equipamiento.

### UPPCharacterMovementCore

Archivo:
[UPPCharacterMovementCore.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Internal/UPPCharacterMovementCore.cs>)  
Namespace: UPP.ThirdPersonController.Internal  
Estado: **Infraestructura**, clase base abstracta

Contiene el cálculo reutilizable de:

- orientación relativa a cámara;
- velocidades y aceleración de sprint;
- movimiento libre y direccional de aim;
- control aéreo;
- suelo, pared, pendiente y escalones;
- root motion;
- estados y permisos de locomoción;
- eventos UnityEvent.

Tipos relevantes:

- MovementMode: Free o AlwaysInAimMode;
- PressAimMode: HoldToAim o OnePressToAim;
- UPPCharacterMovementEvents: eventos de run, sprint, roll, salto, postura,
  movimiento e ingreso/salida de aim.

Estado público:

- IsMoving, IsRunning e IsSprinting;
- IsCrouched, IsProne e IsRolling;
- IsGrounded, IsJumping, IsSliding y WallAHead;
- IsAiming, FiringMode y FiringModeIK;
- CanMove, CanRotate, CanJump y DisableAllMove;
- VelocityMultiplier, GroundAngle, GroundNormal y GroundPoint;
- anim, AnimatorController, rb, coll, MyPivotCamera y HumanoidSpine.

API de extensión:

- SetMoveInput y _Move;
- _Jump, _Crouch, _Prone, _GetUp y _Roll;
- disableMove, enableMove, disableRotation y enableRotation;
- disableFireModeIK, enableFireModeIK, startRolling y stopRolling;
- SetForwardOrientation y GetForwardOrientation;
- GetLookPosition y GetLookDirectionEulerAngles;
- PhysicalIgnore.

Los métodos con guion bajo se conservan como contrato histórico para eventos y
extensión. El input normal debe pasar por ControllerInputs.

### UPPAnimatorControllerComponent

Archivo:
[UPPAnimatorControllerComponent.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Character/UPPAnimatorControllerComponent.cs>)  
Namespace: UPP.ThirdPersonController  
Estado: **Activo**

Centraliza la relación entre movimiento y Animator:

- asigna controllerAsset durante Awake si assignControllerOnAwake está activo;
- cachea nombre y tipo de parámetros;
- publica estados de locomoción;
- calcula IdleTurn y MovingTurn;
- conduce el peso de Legs Motion Layer durante aim;
- dispara Roll y actualiza contacto con suelo.

Configuración:

- controllerAsset;
- assignControllerOnAwake;
- updateMode (Normal en el prefab actual, con Rigidbody Interpolate);
- layerBlendSpeed;
- ParameterMap con índices y nombres.

API:

- AssignController;
- ValidateAnimatorContract;
- TriggerRoll;
- SetGroundContact;
- Animator, Parameters y CanTriggerRoll.

ValidateAnimatorContract comprueba los 14 parámetros consumidos y ambos índices
de layer. No valida transiciones, clips ni máscaras.

### ParameterMap

Tipo anidado de UPPAnimatorControllerComponent. Define:

- BaseLayerIndex y LegsLayerIndex;
- Moving, Running, Speed, Horizontal, Vertical;
- IdleTurn, MovingTurn y LandingIntensity;
- Grounded, Jumping, FireMode, Crouched y Prone;
- Roll.

El Animator activo tiene Base Layer en índice 0 y Legs Motion Layer en índice
1. No existe una layer de upper body.

### UPPIKCharacterComponent

Archivo:
[UPPIKCharacterComponent.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Character/UPPIKCharacterComponent.cs>)  
Namespace: UPP.ThirdPersonController  
Estado: **Activo**

Componente de IK de locomoción del núcleo UPP. Concentra:

- posicionamiento y rotación independientes de ambos pies desde la pose animada actual;
- transición IK/FK;
- ajuste relativo de animator.bodyPosition por desnivel, sin filtrar la animación;
- look-at hacia AimPoint;
- lean procedural de RootBone.

Configuración de pies:

- EnableFootPlacement, GroundLayers y GlobalWeight;
- RaycastMaxDistance, RaycastHeight, Radius y MaxStepHeight;
- UseDynamicFootPlacing y nombres de curvas;
- SmoothTransitions, SpeedInTransition y SpeedOutTransition.

Configuración de aim:

- EnableAimLookAt;
- LookAtBodyWeight y HeadIKBodyWeight;
- AimIKTransitionSpeed.

Configuración de lean:

- RootBone y RootBoneSpineLean;
- RootBoneSpineMovement;
- intensidades, velocidad, bloqueo frontal y LeanAxis.

API:

- ConfigureAimTrace;
- StartFootPlacement;
- GetCalculatedAnimatorCenterOfMass;
- resultados HasLeftFootHit, HasRightFootHit, LeftFootHit y RightFootHit.

UPPIKCharacterComponent no resuelve grips ni manos de armas. Esa presentación
pertenece a PlayerWeaponPose, fuera del núcleo UPP, y comparte su Animator.

### UPPGizmosDebugComponent

Archivo:
[UPPGizmosDebugComponent.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Character/UPPGizmosDebugComponent.cs>)  
Namespace: UPP.ThirdPersonController  
Estado: **Activo**

Centraliza todos los Gizmos, Handles y visuales de aim del sistema propio. No
modifica las mecánicas observadas; sólo controla su presentación de debug.

Controles:

- DrawDebug, master general;
- OnlyInEditMode;
- DrawGroundCheck, DrawWallCheck y DrawStepCorrection;
- DrawFootIK, DrawBodyPlacement y DrawBodyLean;
- DrawCamera, DrawAimTrace y DrawCrosshair;
- WireColor.

Configure recibe cámara, trace y crosshair. Los gizmos de cámara y trace se
dibujan en este componente. El crosshair se controla mediante
UPPDebugCrosshair.DebugVisible.

Apagar un visual no desactiva su sistema: DrawFootIK no apaga IK y
DrawAimTrace no detiene el raycast.

### UPPPlayerIntegration

Archivo:
[UPPPlayerIntegration.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Character/UPPPlayerIntegration.cs>)  
Namespace: UPP.Runtime.Character  
Estado: **Activo**

Capa de bootstrap y binding temprano. Requiere
UPPCharacterMovementComponent, reutiliza un UPPTPSCameraController existente o
crea cameraRigPrefab cuando createCameraIfMissing está activo. Después enlaza y
reintenta hasta que la cámara esté disponible:

- Inputs del movimiento con InputAsset de cámara;
- characterTarget y TargetToFollow;
- MyPivotCamera del movimiento con el controlador resuelto;
- MainCamera con UPPAimTrace;
- UPPAimTrace con UPPIKCharacterComponent;
- trace y cámara con UPPDebugCrosshair;
- cámara, trace y crosshair con UPPGizmosDebugComponent.

TargetToFollow prefiere HumanoidSpine y usa el transform del jugador como
fallback. La columna se usa como objetivo de posición; con FollowUpTarget
desactivado, RotateCamera conserva una orientación vertical estable en lugar
de copiar el up animado del hueso.

La búsqueda prioriza la MainCamera compatible y luego cualquier
UPPTPSCameraController activo. Solo si no encuentra uno instancia el prefab
como root de la escena, evitando cámaras y AudioListener duplicados. En una
cámara compatible existente, completa UPPAimTrace y UPPDebugCrosshair si
faltan.

API:

- Configure;
- BindRuntimeReferences.

## Input

### UPPPlayerCharacterInputAsset

Archivo:
[UPPPlayerCharacterInputAsset.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Input/UPPPlayerCharacterInputAsset.cs>)  
Namespace: UPP.ThirdPersonController.UPPInputSystem  
Estado: **Activo**

ScriptableObject con MovementActions:

- Move;
- Look;
- Jump;
- Run;
- Aim;
- Crouch;
- Prone;
- Roll.

API de lectura:

- MoveAxis y LookAxis;
- IsJumpTriggered;
- IsRunPressed e IsRunPerformed;
- IsAimTriggered e IsAimPressed;
- IsCrouchTriggered, IsProneTriggered e IsRollTriggered;
- SetActiveInputs.

El asset activo es
[UPPPlayerCharacterInputs.asset](<D:/SurvivePrototype/Assets/Settings/Input/UPPPlayerCharacterInputs.asset>).

## Cámara

### CameraState

Archivo:
[UPPCameraSystemLib.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Libraries/UPPCameraSystemLib.cs>)  
Namespace: UPP.ThirdPersonController.CameraSystems  
Estado: **Infraestructura**

Datos serializables de un estado de cámara:

- distancia, velocidad de transición y FOV;
- offsets de target y Camera;
- sensibilidad y límites verticales;
- CollisionLayers;
- identificador de configuración.

### UPPCameraController

Archivo:
[UPPCameraSystemLib.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Libraries/UPPCameraSystemLib.cs>)  
Namespace: UPP.ThirdPersonController.CameraSystems  
Estado: **Activo**, clase base

Implementa transición, rotación, seguimiento, colisión, FOV, cursor y estados
custom.

API principal:

- SetCameraStateTransition;
- SetCustomCameraStateTransition;
- DisableCustomStateTransitioningState;
- SetPivotCameraPosition;
- SetCameraPosition;
- SetCameraRotation;
- RotateCamera;
- SetCameraCollision;
- SetFieldOfView;
- LockMouse.

No dibuja gizmos y no modifica Time.fixedDeltaTime. Esas responsabilidades no
pertenecen a la cámara.

### UPPTPSCameraController

Archivo:
[UPPTPSCameraController.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Camera/UPPTPSCameraController.cs>)  
Namespace: UPP.ThirdPersonController.CameraSystems  
Estado: **Activo**

Especialización TPS. Lee InputAsset, sigue a characterTarget, administra
auto-rotación y selecciona NormalCameraState o AimCameraState.

PlayerStates solo contiene Normal y Aiming. Aim es over-the-shoulder, no ADS
en primera persona. RawMouseX/Y y SmoothedMouseX/Y permiten inspeccionar la
entrada procesada.

## Aim diagnóstico

### UPPAimTrace

Archivo:
[UPPAimTrace.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Aiming/UPPAimTrace.cs>)  
Namespace: UPP.Runtime.Aiming  
Estado: **Activo**

Traza desde el centro del viewport. Usa un buffer de 24 raycasts, ignora
triggers y la jerarquía configurada en ignoredRoot, y conserva el hit externo
más cercano.

API:

- Configure;
- RefreshTrace;
- HasHit;
- LastHit;
- AimPoint;
- LastRay.

No dibuja por sí misma y no aplica daño.

### UPPDebugCrosshair

Archivo:
[UPPDebugCrosshair.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Aiming/UPPDebugCrosshair.cs>)  
Namespace: UPP.Runtime.Aiming  
Estado: **Activo**

Dibuja mediante OnGUI una cruz centrada. IsVisible requiere DebugVisible y aim,
salvo que alwaysVisible esté activo. Configure conecta trace/cámara y fuerza la
política aim-only. El color cambia si UPPAimTrace.HasHit.

Es diagnóstico, no HUD de producción.

## Animator

### UPPAnimatorStateEvent

Archivo:
[UPPAnimatorStateEvent.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Animation/UPPAnimatorStateEvent.cs>)  
Namespace: UPP.ThirdPersonController.AnimatorStateMachineBehaviours  
Estado: **Activo**

Ejecuta una vez, al alcanzar Duration normalizado:

- DisableMovement o EnableMovement;
- DisableRotation o EnableRotation;
- DisableAimIK o EnableAimIK;
- StopRolling o StartRolling.

Los valores 1, 2 y 3 del enum están reservados porque antes pertenecían a armas.
No existe un evento de disparo, recarga ni root motion.

## Preferencias de cámara

### UPPGameSettings

Archivo:
[UPPGameSettings.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Settings/UPPGameSettings.cs>)  
Namespace: UPP.ThirdPersonController.GameSettings  
Estado: **Infraestructura**

API estática respaldada por PlayerPrefs:

- CameraInvertVertical;
- CameraInvertHorizontal;
- CameraSensibility;
- OnChangeSettings;
- ApplySettings;
- ResetSettings.

Solo administra preferencias de cámara.

### UPPApplyCameraSettings

Archivo:
[UPPApplyCameraSettings.cs](<D:/SurvivePrototype/Assets/Scripts/Player/Settings/UPPApplyCameraSettings.cs>)  
Namespace: UPP.ThirdPersonController.GameSettings  
Estado: **Activo**

Escucha OnChangeSettings y aplica sensibilidad e inversión al
UPPCameraController serializado en el prefab.

## Root motion

La configuración pertenece a UPPCharacterMovementCore y es expuesta por
UPPCharacterMovementComponent:

- RootMotion;
- RootMotionSpeed;
- RootMotionRotation;
- RootMotionDeltaPosition.

Es opt-in y está desactivada en el prefab. Se limita a locomoción grounded y se
excluye durante salto, roll y aim. La traslación se proyecta al plano y se
aplica sobre Rigidbody; la rotación es opcional.

UPPAnimatorStateEvent no modifica estos flags.

## Dependencias permitidas

~~~text
Input -> CharacterMovement -> Rigidbody / Capsule
                          -> AnimatorController -> Animator
                          -> IKCharacter
                          -> GizmosDebug

PlayerIntegration -> TPSCamera
                  -> AimTrace -> IKCharacter
                  -> DebugCrosshair
                  -> GizmosDebug

Animator -> AnimatorStateEvent -> CharacterMovement

MasterItem -> PlayerWeaponController <-> WeaponItem
WeaponItem -> WeaponDefinition
PlayerWeaponController -> CharacterMovement / AimTrace / Camera
                       -> EnemyHealth
PlayerInteractionController -> PlayerWeaponInventory.TryCollect
PlayerWeaponInventory -> PlayerWeaponController / WeaponItem / MasterItem
PlayerWeaponHotbar -> PlayerWeaponInventory / eventos PlayerWeaponController / PlayerHealth / WeaponDefinition.Icon
WeaponMechanismAnimator -> WeaponItem.ShotPresented / StateChanged / AnimationClip
WeaponVfxPresenter -> WeaponItem.ShotPresented / ImpactPresented / StateChanged
                   -> WeaponDefinition.VfxProfile / WeaponVfxPool / WeaponImpactSurface
PlayerWeaponHUD -> PlayerWeaponController / WeaponItem / PlayerHealth
PlayerWeaponPose -> PlayerWeaponController / WeaponItem / Animator / estados UPP
PlayerWeaponFeedback -> eventos PlayerWeaponController / WeaponDefinition / AudioSource
~~~

## Ítems y armas

Las clases de esta sección viven en Assets/Scripts, fuera del ensamblado UPP.
Consumen los estados de locomoción y el rayo de aim existentes; UPP no depende
de ellas. Los valores y pasos de configuración están en
[Ítems, pickup y armas](ITEMS_ARMAS.md).

### ItemType y MasterItem

Archivo:
[MasterItem.cs](<D:/SurvivePrototype/Assets/Scripts/Items/MasterItem.cs>)  
Namespace: Survive.Items  
Estado: **Activo** en MasterItem.prefab y por herencia en Weapon_Rifle.prefab

ItemType contiene Generic = 0 y WeaponType = 1. MasterItem requiere
SphereCollider y Rigidbody y resuelve:

- configuración del trigger y cuerpo cinemático;
- proyección inicial sobre el suelo;
- altura en centímetros, oscilación y giro configurables durante Play Mode;
- attachment al socket, desactivación de colliders y preservación de escala;
- restauración de colliders y layers al volver al mundo.

API:

- Type, IsEquipped e IsOwnedBy(ownerRoot);
- TryAttach(ownerRoot, socket, localPosition, localEulerAngles);
- PlaceInWorld(position, rotation);
- SetItemType, protegido para las clases hijas.

Un MasterItem Generic flota pero no se equipa como arma. La vista previa de
altura se dibuja con Gizmos; no mueve la instancia en Edit Mode. El trigger
permite detectar el volumen del pickup, pero ya no provoca la recogida por
contacto: esa decisión pertenece a PlayerInteractionController.
IsEquipped incluye instancias adquiridas que están guardadas. El inventario
las conserva inactivas bajo InventoryStorage; TryMoveOwnedAttachment es una
primitiva interna que admite únicamente el mismo dueño y mantiene colisiones
desactivadas. EquippedWeapon del controlador identifica el arma en mano.

### WeaponDefinition

Archivo:
[WeaponDefinition.cs](<D:/SurvivePrototype/Assets/Scripts/Items/WeaponDefinition.cs>)  
Namespace: Survive.Items  
Estado: **Presets históricos verificados el 19/10; audio/VFX del 24/10 verificados en 178/178 y 229/229 PASS, respectivamente**

ScriptableObject creado mediante Survive/Items/Weapon Definition o por el
setup. Contiene displayName, damage, range, shotsPerSecond, automatic,
magazineSize, startingReserveAmmo, reloadDuration, pelletsPerShot y spreadDegrees.
La ampliación de presentación agrega recoilDegrees, recoilBackDistance,
recoilRecoverySpeed e icon, expuesto como Sprite Icon. La ampliación de audio
agrega shotVariations, shotMechanismVariations y shotTailVariations,
expuestos como IReadOnlyList<AudioClip>, más shotVolume,
shotMechanismVolume y shotTailVolume, ganancias de 0 a 1. ShotSound queda como
respaldo de la capa cercana sin variantes; ReloadSound permanece opcional.
reloadAudioCues y reloadVolume agregan la lista de fases y la ganancia general,
expuestas como ReloadAudioCues y ReloadVolume. ReloadAudioCue, serializable
en este mismo archivo, contiene normalizedTime, clip y volume, con propiedades
NormalizedTime, Clip y Volume; no administra munición.
Las ocho referencias de icono apuntan a fotografías reales de 512 × 320 bajo
Assets/UI/WeaponIcons. No guardan datos mutables del inventario. Los ocho presets
serializados tienen Automatic=true para repetir al mantener LMB.
ReloadSound es respaldo sin cues válidos. Los ocho presets tienen cuatro cues
cada uno, reutilizando cuatro cortes reales de JU TPS con pitch 1; ReloadSound
queda vacío al usar esos cues. La ampliación está IMPLEMENTADA y VERIFICADA
en Play Mode controlado: 114/114 PASS. WeaponAudioSetup vincula ShotSound y las tres listas a
Modern Weapons y las fases de recarga a JU TPS, sin incorporar munición mutable.
Expone las propiedades de lectura con los mismos nombres en PascalCase.
Guarda configuración compartida, sin munición mutable de una instancia.
vfxProfile, expuesto como VfxProfile, referencia los prefabs y tiempos
cosméticos de WeaponVfxProfile; no agrega estado balístico.

### WeaponItem

Archivo:
[WeaponItem.cs](<D:/SurvivePrototype/Assets/Scripts/Items/WeaponItem.cs>)  
Namespace: Survive.Items  
Estado: **Activo** en Weapon_Rifle.prefab y ocho prefabs nuevos; munición y presets **verificados en pruebas controladas** el 19/10/2026

Clase hija de MasterItem. Fija ItemType.WeaponType en Reset, OnValidate y Awake.
Publica la configuración, conserva la munición por pickup y presenta el disparo:

- Muzzle;
- Definition, DisplayName, Damage, Range, ShotsPerSecond y Automatic;
- MagazineSize, ReloadDuration, PelletsPerShot y SpreadDegrees;
- AmmoInMagazine, ReserveAmmo, HasAmmo, IsReloading y ReloadProgress;
- EquippedLocalPosition y EquippedLocalEulerAngles;
- LeftHandGrip y TwoHanded, consumidos por PlayerWeaponPose;
- TryConsumeRound(), TryStartReload(), TickReload(deltaTime) y CancelReload();
- AddReserveAmmo(amount) y ResetAmmunition();
- ShowShot(start, end, hit, tracerIndex = 0);
- ShowImpact(RaycastHit) y evento ImpactPresented, de tipo Action<WeaponItem, RaycastHit>;
- eventos StateChanged, ShotPresented y ReloadCompleted, de tipo Action<WeaponItem>.

Muzzle usa el Transform raíz si no se asigna una referencia. Para un rifle,
conviene asignar un hijo en la salida del cañón con +Z hacia adelante.
Definition tiene prioridad sobre los valores locales. Sin preset, conserva los
campos históricos y usa la configuración local de munición. El cargador inicia
lleno y la reserva se inicializa una sola vez por instancia. Cada disparo
consume una bala aunque tenga varios perdigones. Soltar o intercambiar conserva
ambos contadores; ResetAmmunition los reinicia solamente por una llamada explícita.

TickReload recibe tiempo escalado desde el controlador y transfiere las balas
necesarias al completar la duración, sin superar la reserva disponible. Cancelar
no transfiere balas. ReloadCompleted se emite sólo al completar, después de
transferir munición y antes de StateChanged; cancelar no lo emite. Permite
terminar los cues sin confundir final natural con cancelación.
StateChanged informa consumo, inicio/final/cancelación de recarga, agregado
de reserva y reinicio. ShowShot administra un pool acotado a
64 LineRenderer reutilizables y compatibilidad con un objeto opcional de fogonazo.
WeaponVfxSetup retira el fogonazo provisional al integrar GunFX. No aplica daño
ni decide la cadencia: eso pertenece al jugador.

ShotPresented se emite en ShowShot solamente para tracerIndex 0: informa una
presentación por disparo físico, también en escopeta, sin multiplicarse por
perdigón. WeaponMechanismAnimator y WeaponVfxPresenter lo consumen para presentar
el mecanismo y fogonazo/vainas. ImpactPresented es independiente: ShowImpact
exige collider y normal válidos, y el controlador entrega sólo RaycastHit real.
El bool hit de ShowShot no crea geometría de impacto ni sangre ficticia.

### WeaponVfxProfile

Archivo: [WeaponVfxProfile.cs](<D:/SurvivePrototype/Assets/Scripts/Items/WeaponVfxProfile.cs>)  
Namespace: Survive.Items  
Estado: **Integrado y verificado en Play Mode controlado posterior a la poda: 229/229 PASS el 24/10/2026**

ScriptableObject de presentación por preset, referenciado desde WeaponDefinition.
Expone MuzzlePrefab/Scale/Lifetime, ShellPrefab/Scale/Lifetime,
EjectionDelaySeconds, EjectCasingsOnReload, ReloadEjectionProgress e
ImpactScale/Lifetime. GetImpactPrefab(WeaponImpactSurfaceKind) resuelve seis
superficies. Si falta una variante inorgánica usa Concrete; Flesh sin efecto
propio devuelve null, sin sustituirlo por polvo de hormigón.

### WeaponImpactSurface y WeaponImpactSurfaceKind

Archivo: [WeaponImpactSurface.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/WeaponImpactSurface.cs>)  
Namespace: Survive.Combat  
Estado: **Seis superficies verificadas en la ejecución de 229/229 PASS el 24/10/2026**

El enum declara Concrete, Metal, Wood, Dirt, Flesh y Glass. El componente
serializa surface y expone Surface en el collider o sus padres. Tiene prioridad
sobre EnemyHealth/Flesh, TerrainCollider/Dirt y la inferencia por nombre de
sharedMaterial físico. No cambia materiales ni propiedades de colisión.

### WeaponVfxPresenter

Archivo: [WeaponVfxPresenter.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/WeaponVfxPresenter.cs>)  
Namespace: Survive.Combat  
Estado: **Integrado y verificado en la ejecución posterior a la poda de 229/229 PASS**

Requiere WeaponItem y usa LateUpdate 1200. Escucha ShotPresented,
ImpactPresented y StateChanged. Recibe weapon y shellEjectionPort; el perfil
proviene de WeaponDefinition. Emite un fogonazo por tiro físico y vainas desde
la recámara calibrada, separada de Muzzle. Cada impacto usa hit.point y
hit.normal reales, con separación de 5 mm y superficie resuelta. Si el enemigo
Flesh ya dispone de partículas propias, no duplica el efecto de EnemyHealth;
consulta su propiedad HasHitParticleEffect, sin apropiarse de esas partículas.

Pistol/SMG/AssaultRifle/MachineGun usan demoras cortas de expulsión;
Shotgun/HuntingRifle/SniperRifle usan 0,24/0,28/0,32 segundos para acompañar
palanca/cerrojo. Revolver retiene las vainas y las expulsa al 39 % de la recarga;
conserva el contador al guardar/soltar y no vuelve a emitir vainas ya expulsadas
si la recarga se cancela y reinicia. No consume balas ni inicia recargas.
Al perder control/equipamiento cancela expulsiones pendientes y retira los
fogonazos anclados. Impactos/vainas emitidos terminan en el mundo.

### WeaponVfxPool

Archivo: [WeaponVfxPool.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/WeaponVfxPool.cs>)  
Namespace: Survive.Combat  
Estado: **Verificado en la ejecución de 229/229 PASS, con estrés separado de capacidad y descarga de escena**

GetOrCreate(Scene) crea un pool por escena cargada, sin DontDestroyOnLoad.
Spawn(prefab, position, rotation, scale, lifetime, anchor) reutiliza un máximo
de 128 entradas; al llenarse recupera la de vencimiento más próximo. ReleaseAttached(anchor)
retira fogonazos anclados, sin retirar impactos/vainas independientes.
Los ParticleSystem son one-shot con tiempo escalado y semilla de RNG local;
pausa congela simulación y vencimientos. OnDisable libera las entradas y
OnDestroy limpia el registro de escena y las instancias. Las vainas son
partículas cosméticas, sin Rigidbody de gameplay ni nuevos proyectiles.

### WeaponMechanismAnimator

Archivo:
[WeaponMechanismAnimator.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/WeaponMechanismAnimator.cs>)  
Namespace: Survive.Combat  
Estado: **Implementado e integrado en ocho wrappers y rifle histórico; 142/142 comprobaciones históricas PASS en Play Mode controlado el 19/10/2026**

Requiere WeaponItem y presenta las piezas originales del modelo, sin agregar
un Animator de arma ni administrar munición, cadencia o daño. Recibe weapon,
fireClip, reloadClip y animatedParts. Cachea el reposo y usa
AnimationClip.SampleAnimation sobre curvas absolutas de posición y rotación
local. Su LateUpdate tiene orden 1005, antes de PlayerWeaponPose en 1050.

Escucha ShotPresented y StateChanged. Fire avanza con tiempo escalado y cabe
entre tiros según ShotsPerSecond; Reload usa el ReloadProgress real. La pausa
congela la fase y la presentación vuelve a reposo al completar, cancelar,
guardar, soltar, morir o deshabilitar. No llama a TryConsumeRound ni TickReload.

API: FireClip, ReloadClip, AnimatedParts, IsFiring, IsReloading, NormalizedTime,
PreviewPose(reload, progress) y ResetPresentation(). PreviewPose permite
inspeccionar un clip sin disparar, iniciar recarga ni modificar munición.
Las piezas móviles no implican estado balístico por bala ni carga de cartuchos
uno a uno. Las vainas cosméticas de GunFX son una presentación separada.
La coreografía de manos sigue siendo la
recarga Humanoid compartida de RIFLE PRO.

### PlayerWeaponController

Archivo:
[PlayerWeaponController.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/PlayerWeaponController.cs>)  
Namespace: Survive.Combat  
Estado: **Activo** en UPPPlayer.prefab

Requiere UPPCharacterMovementComponent. Se ejecuta en LateUpdate con orden
1100, después de cámara y trace, y administra:

- acciones Fire, Reload y DropWeapon de New Input System, con bindings editables;
- equipamiento de una sola arma activa y resolución del socket de mano derecha;
- coordinación con PlayerWeaponInventory, que conserva hasta seis instancias;
- recarga R/Button West, recarga automática al vaciarse y soltar G/D-pad Down;
- permisos de disparo según aim, locomoción, roll, salud opcional, pausa y cursor;
- alineación delegada en PlayerWeaponPose cuando resuelve manos; respaldo de rotación del objeto si falta esa presentación;
- cadencia automática o por nueva presión y consumo de una bala por tiro;
- selección de objetivo mediante el rayo de UPPAimTrace y la máscara de disparo;
- comprobación de cañón solapado, segmento entre jugador y cañón y trayectoria;
- hasta 64 perdigones con dispersión y daño mediante EnemyHealth, incluyendo colliders hijos;
- mira simple opcional con impacto, recarga y apertura por tiro, independiente de DrawDebug;
- parámetros y peso de la capa Weapon Actions para recarga Humanoid.

API:

- EquippedWeapon, Health, Inventory e IsReloadAnimationActive;
- TryEquip(weapon);
- TrySwapWeapon(weapon);
- TryFire();
- TryReload();
- DropEquippedWeapon(position, rotation);
- WeaponChanged, WeaponStateChanged y ShotFired, de tipo Action<WeaponItem>.

TryFire programático conserva los mismos permisos y cadencia que el input.
TryEquip exige que el movimiento sea de un jugador activo y no reemplaza un
arma existente. El socket se resuelve por referencia explícita, por el nombre
WeaponSocket, por la mano derecha Humanoid y, como último recurso, por un
Transform de respaldo relativo al root con offsets configurables.

El jugador canónico recibe los pickups por PlayerWeaponInventory.TryCollect;
la selección 1–6/rueda cambia el arma activa sin destruir ni recrear el resto.
TrySwapWeapon conserva la compatibilidad del controlador sin inventario:
usa TryEquip cuando está desarmado. Si hay un arma equipada,
adjunta primero la nueva y solamente después devuelve la anterior al lugar
del pickup; una falla de attachment conserva el arma previa. El intercambio
establece un margen de 0,15 segundos antes de disparar. Fire usa LMB/Right
Trigger, Reload usa R/Button West y DropWeapon usa G/D-pad Down.

Los ocho presets actuales usan Automatic=true: mantener Fire repite hasta
vaciar el cargador. automaticReloadOnEmpty está activo en el prefab y solicita
recarga después de 0,18 segundos si queda reserva. Al mantener Fire se retoma
el disparo cuando terminan recarga y transición de salida. La capacidad de
configurar un preset semiautomático sigue disponible.

TryReload no requiere aim. Pausar congela TickReload; roll, muerte, intercambio,
soltar y deshabilitar el control cancelan la recarga sin transferir munición.
weaponAnimator y reloadAnimationClip están serializados en el jugador.
WeaponReloading y WeaponReloadSpeed controlan la acción; la velocidad es
`reloadAnimationClip.length / weapon.ReloadDuration`. IsReloadAnimationActive
consulta la etiqueta WeaponReload en el estado actual o siguiente durante
la transición. El peso de Weapon Actions parte de 0 y se activa solamente
durante la acción y su salida; no se permite disparar durante esa salida.
Todos los raycasts de un tiro se resuelven antes de aplicar daño: desactivar un
collider al morir no habilita penetración para los siguientes perdigones.

La mira de gameplay oculta temporalmente UPPDebugCrosshair para evitar dos
cruces superpuestas y restaura su estado al deshabilitarse o soltar el arma.

### PlayerWeaponInventory

Archivo:
[PlayerWeaponInventory.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/PlayerWeaponInventory.cs>)  
Namespace: Survive.Combat  
Estado: **Integrado; entrega anterior de inventario con 66/66 PASS en Play Mode controlado**

Administra seis slots (`Capacity = 6`, `SlotCount`), con SelectedIndex de base
cero. Conserva las instancias reales de WeaponItem: una equipada y hasta cinco
guardadas/ocultas, cada una con su propia munición. No hay stacking ni guardado
persistente y una partida nueva comienza vacía. El movimiento UPP no adquiere
responsabilidades de inventario.

API: GetWeapon(index), HasFreeSlot, StorageRoot, CanManageInventory,
TryCollect(weapon), TrySelect(index), TryCycle(direction),
TryDropSelected(position, rotation) y evento Changed sin argumentos.
TryCollect elige el primer slot libre y equipa la nueva instancia; con los
seis ocupados reemplaza el seleccionado y deja el anterior en el suelo del
pickup. G sólo suelta el seleccionado. Seleccionar un slot vacío permite
quedar desarmado conservando los otros slots.

1–6 y Numpad 1–6 seleccionan directamente. Rueda arriba recorre hacia el
anterior y abajo hacia el siguiente, omitiendo vacíos; D-pad Left/Right ofrece
el mismo ciclo en gamepad. Los slots no se compactan. Cambiar de slot cancela
la recarga sin transferir balas. Pausa,
cursor libre según contrato, muerte, roll y controles deshabilitados bloquean
la operación. La ejecución nueva verificó estos casos; las 169 pruebas
históricas de feel permanecen como evidencia anterior separada.
El componente usa LateUpdate con orden 1025: después de menú/cámara/trace y
antes de la pose 1050 y el disparo 1100. InventoryStorage se crea bajo el dueño
cuando hace falta. Al cambiar, el controlador mueve los attachments y activa
sólo la instancia seleccionada; no usa recogida pública para armas ya propias.

### PlayerWeaponHotbar

Archivo:
[PlayerWeaponHotbar.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/PlayerWeaponHotbar.cs>)  
Namespace: Survive.Combat  
Estado: **Integrado con fotografías; evidencia histórica del 19/10/2026: 142/142 PASS y dos capturas de UI. Inventario anterior: 66/66 PASS**

Presentación por eventos de los seis slots, en WeaponHotbar dentro del Canvas
PlayerInteractionHUD. Recibe inventory, weaponController, canvasGroup y arrays
de fondos, barras de selección, iconImages y textos de número/nombre/munición. Muestra
Vacío, `cargador / reserva`, recarga y falta de balas. La selección usa color y
barra de acento. No intercepta raycasts ni clicks.

Las seis Image serializadas usan weapon.Definition.Icon y conservan proporción.
Refresh oculta la imagen de un slot vacío y presenta el sprite de cada arma
sin cargar archivos ni generar fotografías en runtime.

Escucha inventory.Changed, WeaponChanged, WeaponStateChanged y OnDeath;
Refresh() actualiza todas las tarjetas. Oculta la barra al morir o deshabilitar
el controlador/inventario. No decide equipamiento ni modifica munición.

### PlayerWeaponPose

Archivo:
[PlayerWeaponPose.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/PlayerWeaponPose.cs>)  
Namespace: Survive.Combat  
Estado: **Integrado y verificado en Play Mode controlado y capturas**

Componente externo al núcleo UPP, en el mismo GameObject del Animator
Humanoid. Recibe animator, movement y weaponController; usa orden 1050.
OnAnimatorIK limpia los pesos de objetivos de manos y registra la evaluación
del Animator en el cuadro actual. LateUpdate ajusta los huesos reales después
de cámara/trace UPP y antes del disparo en 1100; sólo trabaja si hubo evaluación
fresca. El Animator del jugador usa AlwaysAnimate para mantener esa condición.
No utiliza calibración de objetivos Humanoid normalizados ni modifica Legs.mask.

La muñeca derecha orienta el arma que conserva su attachment. El apoyo izquierdo
se resuelve de forma analítica con dos segmentos rígidos, respetando las
longitudes del brazo y antebrazo, sin estirar ni escalar huesos. LeftHandGrip y
TwoHanded definen el apoyo; sus marcadores se ubican al alcance físico del rig.
HandlesAlignment informa al controlador que la pose resuelve la orientación.

Admite de pie, crouch y prone. Libera el ajuste durante recarga y su salida,
roll, muerte y controles deshabilitados.
ShotFired acumula retroceso limitado; RecoilDegrees, RecoilBackDistance y
RecoilRecoverySpeed del preset controlan la respuesta y recuperación visual.
No cambia cámara, daño ni cadencia. LastSolvedFrame registra el cuadro resuelto;
LastSolvedLayer=-1 identifica el ajuste posterior al Animator y
LeftHandReachError expone la distancia restante al apoyo. Los dedos usan las curvas del clip de aim;
no se simula contacto individual de dedos con la malla.

### PlayerWeaponFeedback

Archivo:
[PlayerWeaponFeedback.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/PlayerWeaponFeedback.cs>)  
Namespace: Survive.Combat  
Estado: **Audio Modern Weapons verificado en 178/178 PASS; recarga JU TPS verificada en Play Mode controlado: 114/114 PASS, exit code 0**

Las referencias weaponController, shotSource y reloadSource se serializan.
Escucha ShotFired, WeaponChanged y WeaponStateChanged; no decide cadencia ni
munición. Cada ShotFired presenta tres PlayOneShot en shotSource: variante
cercana OneShot con ShotVolume, Stop con ShotMechanismVolume y Tail con
ShotTailVolume. Es una presentación por tiro físico, aunque haya perdigones.
Stop es una capa del disparo, sin acción mecánica separada. No usa Dist para
el arma cercana. Un System.Random local selecciona cada capa evitando el
clip anterior si hay alternativas; no consume UnityEngine.Random ni altera
spread. El pitch se fija en 1. ShotSound respalda sólo la capa cercana cuando
no hay variantes. ReloadAudioCues se evalúa contra ReloadProgress y reproduce
una vez cada fase alcanzada con ReloadVolume por cue.Volume. Conserva pitch 1
y duración original de los cortes; no estira una secuencia completa. Escucha
ReloadCompleted del arma observada para recuperar cues saltados por un frame
largo y dejar terminar la cola natural. Cancelar, cambiar, soltar, morir o
deshabilitar detiene la fuente sin presentar fases pendientes. Al rehabilitar
feedback a mitad de recarga omite fases ya pasadas. ReloadSound es respaldo
de una sola reproducción cuando no hay cues válidos, también con pitch 1.
LateUpdate con orden 1200
pausa/reanuda las fuentes después de PlayerWeaponController (1100), incluida
una pausa iniciada ese frame por MenuBehaviour. Muerte o deshabilitación
detienen las fuentes. WeaponAudio/Shots y WeaponAudio/Reload son fuentes separadas
de las fuentes existentes de pasos.
Los ocho presets actuales tienen cuatro cues cada uno y ReloadSound vacío.
Reutilizan cuatro cortes de dos grabaciones reales de JU TPS, no ocho
grabaciones específicas. Se eliminaron Reload_Weapon.wav y sus referencias
y no se regeneran. Modern Weapons no aporta clips nombrados de recarga;
su procedencia se distingue del audio JU TPS posterior. La ejecución histórica del
19/10 verificó los ocho ReloadSound vacíos,
el WAV/meta retirados y los ocho ShotSound conservados, con recarga animada
silenciosa; no verifica las grabaciones ni el comportamiento agregados el 24/10.

### PlayerInteractionController

Archivo:
[PlayerInteractionController.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/PlayerInteractionController.cs>)  
Namespace: Survive.Combat  
Estado: **Activo** en UPPPlayer.prefab

Requiere PlayerWeaponController y se ejecuta en LateUpdate con orden 1150,
después de la cámara. Resuelve su acción Interact de New Input System, con E
como binding inicial, y administra:

- selección del arma mediante el raycast central de cámara, incluyendo su trigger;
- alcance medido desde interactionOriginOffset del jugador hasta el centro del arma;
- descarte de colliders propios y triggers ajenos, conservando paredes sólidas;
- trayectos libres desde cámara y jugador hasta el arma, incluidos orígenes dentro de obstáculos;
- bloqueo por pausa, muerte, roll, locomoción deshabilitada o cursor libre según configuración;
- aviso de guardar o reemplazar slot N con nombre del arma mediante CanvasGroup y TMP_Text;
- delegación a PlayerWeaponInventory.TryCollect al presionar E, con fallback histórico TrySwapWeapon si falta inventario.

API: CurrentTarget, HasTarget, InteractionDistance, RefreshTarget() y
TryInteract(). Esta última revalida todas las condiciones antes de equipar.
Las referencias serializadas incluyen weaponController, weaponInventory, playerHealth, sourceCamera,
promptCanvasGroup y promptText. interactionDistance comienza en 2,5 metros;
interactionOriginOffset en (0, 1, 0). La máscara hitMask debe incluir tanto
los pickups como las superficies que bloquean su recogida.

### PlayerWeaponHUD

Archivo:
[PlayerWeaponHUD.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/PlayerWeaponHUD.cs>)  
Namespace: Survive.Combat  
Estado: **Activo**, serialización y referencias verificadas el 19/10/2026

Presenta nombre, cargador/reserva y estado de recarga o falta de munición. Se
agrega como hijo WeaponHUD del Canvas PlayerInteractionHUD, abajo a la derecha,
mediante el setup. Las referencias serializadas son weaponController,
playerHealth, weaponNameText, ammoText, statusText y hudCanvasGroup.

Se suscribe a WeaponChanged, WeaponStateChanged y OnDeath al habilitarse y
retira listeners al deshabilitarse. Refresh() es su API pública. Usa la misma
salud configurada en el controlador, incluido un override de escena, y oculta
el panel si murió o se deshabilitó el controlador. No administra munición ni
requiere un Update para consultar su estado.

### WeaponSystemSetup

Archivo:
[WeaponSystemSetup.cs](<D:/SurvivePrototype/Assets/Editor/WeaponSystemSetup.cs>)  
Namespace: Survive.Editor  
Estado: **Ejecutado y verificado** el 19/10/2026

CreatePresets() crea ocho WeaponDefinition en Assets/Data/Weapons y ocho
prefabs en Assets/Prefabs/Items/Weapons. Conserva los assets propios existentes.
El arte procede de PolygonApocalypse/Prefabs/Weapons/Guns; Sidekick Characters
aporta personajes, sin armas encontradas en este checkout. Los materiales URP
propios viven fuera del paquete original, que no se modifica.

CreateAndIntegrate() además agrega el HUD al jugador canónico, asigna
WD_AssaultRifle al Weapon_Rifle histórico y llama a WeaponFeelSetup para la
presentación actual. No guarda Level 1. IntegrateLevel1() es una operación explícita independiente
que prepara tres pickups de Pistol, Shotgun y AssaultRifle bajo
WeaponSystem_Presets. Verifica piso, altura, pendiente, espacio y trayecto desde
el jugador antes de modificarla; rechaza la escena abierta si tiene cambios
sin guardar y evita duplicar el contenedor existente.

### WeaponFeelSetup y preparación de presentación

Archivos:
[WeaponFeelSetup.cs](<D:/SurvivePrototype/Assets/Editor/WeaponFeelSetup.cs>),
[WeaponAnimationSetup.cs](<D:/SurvivePrototype/Assets/Editor/WeaponAnimationSetup.cs>),
[WeaponGripSetup.cs](<D:/SurvivePrototype/Assets/Editor/WeaponGripSetup.cs>) y
[WeaponAudioSetup.cs](<D:/SurvivePrototype/Assets/Editor/WeaponAudioSetup.cs>)  
Namespace: Survive.Editor  
Estado: **Feel anterior: 169/169 controladas PASS. Inventario/hotbar y retiro
del audio provisional: 66/66 PASS. Esas ejecuciones históricas no verifican el
audio Modern Weapons del 24/10/2026, que tiene su evidencia separada de 178/178
PASS posterior a la poda con muestras reales del motor.**

WeaponFeelSetup.CreateAndIntegrate() es la entrada central. Requiere importar
RIFLE PRO V2.7A2: el paquete original de E:/Unity se conserva y sus 950 entradas,
incluidos 895 FBX, están bajo Assets/Rifle_27A2_Pro. El setup integra animaciones,
grips, Automatic=true en los ocho presets, datos de retroceso, referencias
del controlador, arte Synty para el rifle histórico y WeaponPresentationSetup.
Esta última integra mecanismos, fotos e inventario/UI. La integración de
inventario ejecuta después WeaponAudioSetup. Conserva el root
y los bloques inactivos del rifle histórico; no reescribe el paquete Synty.

WeaponAnimationSetup.CreateAndIntegrate() referencia el idle
W2_Stand_Aim_Idle_IPC en los hijos existentes de Strafe Torso Pose y la recarga
W2_Stand_Aim_Reload_IPC, Humanoid de 2 segundos. SourceReloadClip expone el
subasset original; la integración falla si falta o es incompatible. Agrega
Weapon Actions con estados Empty/Reload, etiqueta WeaponReload, IKPass,
WeaponReloading bool y WeaponReloadSpeed float. WeaponUpperBody habilita torso,
brazos, dedos e IK de manos y excluye raíz y piernas. La misma recarga de rifle
se reutiliza en los ocho presets; no hay coreografías Humanoid específicas de
manos por mecanismo. Las piezas de armas sí tienen clips locales propios.

WeaponGripSetup.ConfigureGripsAndPlayer() configura LeftHandGrip/TwoHanded
y agrega PlayerWeaponPose al Animator canónico.

### WeaponAudioSetup

Archivo: [WeaponAudioSetup.cs](<D:/SurvivePrototype/Assets/Editor/WeaponAudioSetup.cs>)  
Namespace: Survive.Editor  
Estado: **Disparos verificados en 178/178 PASS; integración de 32 cues JU TPS verificada en Play Mode controlado: 114/114 PASS**

CreateAndIntegrate() requiere los WAV originales bajo SoundsFolder,
Assets/Modern Weapons ALL-IN-ONE Sound System/Sounds, y valida todos los
presets y clips antes de modificarlos. Vincula OneShot/Stop/Tail y ganancias
por perfil; ShotSound apunta a la primera variante cercana. FN509 tiene seis
variantes por capa y los otros siete perfiles cuatro. Reemplaza referencias
propias del prototipo o de este paquete, conservando un ShotSound externo
personalizado y su configuración. Rechaza Play Mode y no genera WAV.

Los perfiles son Pistol/FN509, Revolver/DesertEagle, SMG/SIG_MPX,
AssaultRifle/AK74, Shotgun/Kel-Tec, HuntingRifle/M2010,
SniperRifle/CheyTac y MachineGun/M249. Desert Eagle y M2010 son
correspondencias tímbricas para modelos sin equivalente exacto en el paquete;
Kel-Tec es sólo acústica, mientras Shotgun conserva su palanca visual.
Los clips de disparo asignados usan CompressedInMemory/ADPCM, preload y frecuencia original,
sin forceToMono ni background. No carga toda la biblioteca en el jugador.

Agrega PlayerWeaponFeedback y serializa WeaponAudio/Shots y WeaponAudio/Reload,
fuentes 2D sin loop ni playOnAwake. Configura cuatro cues en cada uno de los
ocho presets desde Assets/Audio/Weapons/Reload/JU_TPS_*.wav, conservando
ReloadSound o cues personalizados ajenos a esa carpeta. Sólo esos cuatro
cortes usan PCM DecompressOnLoad con preload, sin mono ni cambio de frecuencia.
La fuente es JU TPS 3.3.9: Reload.wav y Bolt Sliding.wav, conservados sólo en
evidencia externa; recortes por muestras y fades de 96 frames/2 ms a 48 kHz,
sin normalización, síntesis ni cambio de pitch. No importa scripts o prefabs
JU TPS ni regenera el placeholder. La entrada es
Survive/Weapons/Integrar audio por tipo y recargas JU TPS; WeaponInventorySetup
ya la invoca después de guardar el prefab. La selección de importación es
1.318 WAV, aproximadamente 730 MiB, con 36 carpetas y metas originales; no
scripts, demos ni mixer. El archivo dice v1.2 y sus metadatos internos 1.1.
Procedencia y ganancias completas en [Ítems y armas](ITEMS_ARMAS.md).

### WeaponVfxSetup

Archivo: [WeaponVfxSetup.cs](<D:/SurvivePrototype/Assets/Editor/WeaponVfxSetup.cs>)  
Namespace: Survive.Editor  
Estado: **Importación/setup PASS; runtime posterior a la poda verificado en 229/229 PASS, exit code 0**

CreateAndIntegrate() valida RequiredSourceAssets (17 prefabs GunFX URP), crea
cinco adaptadores de fogonazo, seis de vainas y seis de impacto bajo
Assets/Prefabs/VFX/Weapons, y ocho perfiles bajo Assets/Data/Weapons/VFX.
Vincula WeaponVfxProfile, WeaponVfxPresenter y ShellEjectionPort a los ocho
wrappers y al rifle histórico; retira sólo el fogonazo provisional propio.
Conserva los originales como fuentes de regeneración. La importación seleccionó
155 assets/161 MiB y 87 carpetas del paquete URP anidado en el v1.0 (1),
sin HDRP, Built-in ni audio.
La verificación posterior mantiene 24 prefabs nativos GunFX con SHA-256 intacto.
Los 32 materiales migraron serialización URP 9→10 conservando GUID, shader,
texturas y colores; hay cambios de alpha blend en 12, emisión por defecto en
dos de sangre y flags de lightmap en Glass. No implica identidad binaria de
todos los assets nativos después de Unity.

Los adaptadores son one-shot, sin scripts ajenos, audio, Rigidbody ni Collider;
se elimina el spawner de colisiones de sangre que escaparía del pool.
Desactiva loop/playOnAwake/tiempo no escalado, usa un burst por expulsión y
evita fuerza/mensajes de colisión hacia el gameplay. Agrega DecalRendererFeature
sólo si falta en PC_Renderer y Mobile_Renderer, conservando las otras features.
IntegrateAndAudit() ejecuta el setup y después ScanOnly(), sin borrar.
Menú: Survive/Weapons/Integrar GunFX URP en las ocho armas.

### AssetAudioVfxAudit

Archivo: [AssetAudioVfxAudit.cs](<D:/SurvivePrototype/Assets/Editor/AssetAudioVfxAudit.cs>)  
Namespace: Survive.Editor  
Estado: **Escaneo y poda PASS, 1.273 assets eliminados, cero fallos; runtime posterior: audio 178/178 y VFX 229/229 PASS**

ScanOnly() escribe fuera del proyecto un AuditReport con raíces, candidatos,
retenidos, referencias dinámicas, bloqueos, hashes de asset/meta y tamaños.
El alcance es audio, prefabs VFX dedicados y recursos exclusivos de GunFX y
PolygonApocalypse/Prefabs/FX;
protege contenido productivo, escenas/demos supervivientes, recursos compartidos
no VFX, Resources/Addressables, precargados y RequiredSourceAssets del setup.
No elimina por ejecutar el escaneo. No poda scripts ni otras familias ajenas.

PruneApproved() exige manifiesto externo del mismo proyecto y approvedPaths
explícitos. Verifica rutas absolutas dentro del proyecto y rechaza symlinks o
junctions en assets, metas y ancestros, antes del respaldo y antes de borrar.
Vuelve a escanear dependencias y compara GUID, tamaño y SHA-256;
respalda todos los assets/metas seleccionados y verifica esas copias antes
de borrar. Usa files/ con nombres planos por GUID y campos backupAsset/backupMeta
para conservar la correspondencia con la ruta original sin exceder MAX_PATH.
Revalida el grafo tras copiar y elimina sólo el subconjunto que
siga siendo candidato. Registra deletedPaths/failedPaths y backupRoot;
una poda parcial produce fallo. Los 17 originales GunFX se conservan para
regeneración. El escaneo registró 1.273 candidatos (1.224 audio y 49 recursos
VFX dedicados), 632,29 MiB y 121 clips clasificados como conservados: 102
Modern Weapons y 19 de locomoción. La poda ejecutada eliminó los 1.273 assets
y sus metas con cero fallos, exit code 0. Se verificaron en disco todos los
paths eliminados y los 102 WAV Modern Weapons restantes. El respaldo externo
quedó completo y verificado. Log:
[WeaponVfx-Cleanup-Final.log](<D:/SurvivePrototype/Logs/WeaponVfx-Cleanup-Final.log>).
Reporte y ruta de restauración en [Ítems y armas](ITEMS_ARMAS.md).
La auditoría posterior verificó 2.546 archivos de respaldo sin diferencias,
102 WAV Modern Weapons y 121 AudioClip retenidos sin cambios, y cero
referencias serializadas a GUID eliminados. Los 28 prefabs VFX antiguos
Synty retirados pertenecen al alcance de esta poda; la preservación histórica
del arte de armas se mantiene como evidencia separada del 19/10.

### WeaponInventorySetup

Archivo:
[WeaponInventorySetup.cs](<D:/SurvivePrototype/Assets/Editor/WeaponInventorySetup.cs>)  
Namespace: Survive.Editor  
Estado: **Setup e imágenes del 19/10 incluidos en 142/142 PASS; inventario anterior: 66/66 PASS. Audio del 24/10 verificado aparte en 178/178 PASS**

CreateAndIntegrate() agrega/vincula inventario, controlador e interacción en
UPPPlayer, reutiliza PlayerInteractionHUD y crea seis tarjetas con referencias
serializadas de PlayerWeaponHotbar. Reconstruye sólo su propio WeaponHotbar y
ejecuta WeaponAudioSetup después de guardar el prefab. Rechaza Play Mode.
El menú es Survive/Weapons/Integrar inventario y hotbar de seis slots.
El panel se ancla al centro inferior, 660 × 146 unidades de UI y margen de 28,
con seis tarjetas de 100 × 116. Cada fotografía ocupa 90 × 56; número, nombre
y munición tienen sus propios renglones. Serializa seis imágenes, textos y selección.

### WeaponPresentationSetup, WeaponMechanismSetup y WeaponIconSetup

Archivos:
[WeaponPresentationSetup.cs](<D:/SurvivePrototype/Assets/Editor/WeaponPresentationSetup.cs>),
[WeaponMechanismSetup.cs](<D:/SurvivePrototype/Assets/Editor/WeaponMechanismSetup.cs>) y
[WeaponIconSetup.cs](<D:/SurvivePrototype/Assets/Editor/WeaponIconSetup.cs>)  
Namespace: Survive.Editor  
Estado: **Setup de Editor PASS; ocho fotografías revisadas; evidencia histórica de 142/142 PASS en Play Mode controlado el 19/10/2026**

WeaponPresentationSetup.CreateAndIntegrate() ejecuta mecanismos, iconos y
WeaponInventorySetup en ese orden. WeaponFeelSetup llama a esta entrada;
el menú es Survive/Weapons/Animar mecanismos y fotografiar las ocho armas.
Rechaza Play Mode y no modifica Level 1.

WeaponMechanismSetup genera 18 clips legacy bajo Assets/Animations/Weapons:
Fire/Reload para los ocho presets y dos AssaultRifle_Legacy con rutas de
binding adecuadas a SyntyVisual del rifle histórico. Agrega/configura
WeaponMechanismAnimator y sus referencias en los wrappers propios. No
reescribe modelos, materiales ni prefabs originales del paquete Synty.

Los ocho prefabs originales de Polygon Apocalypse tienen piezas separadas:
Slide/Magazine/Trigger en pistola, SMG y AssaultRifle; Cylinder/Hammer y balas
individuales en Revolver; Bolt en HuntingRifle/SniperRifle; Handle/Trigger en
Shotgun; Magazine/Slide/Handle en MachineGun. Son Transform con MeshFilter
propios, sin Animator ni Animation; los FBX tienen clipAnimations vacío e
importAnimation desactivado.

Los clips propios animan Trigger/Slide y Magazine donde existen; Revolver
incluye Cylinder, Cylinder_Spin, Hammer y movimiento visual de sus seis balas;
los dos rifles de cerrojo animan Bolt. HuntingRifle no tiene Magazine animado.
Shotgun usa Handle como palanca bajo el gatillo, con rotación sobre su pivote,
no como corredera de bombeo; su LeftHandGrip queda en el guardamanos fijo.
No se implementa munición por cartucho ni una coreografía distinta de manos.

WeaponIconSetup fotografía sólo la jerarquía visual real de cada wrapper en
una escena preview aislada: cámara ortográfica, tres luces y materiales URP.
Deshabilita lógica/FX de la copia, usa RGBA sin postprocesado y encuadra desde
la silueta alpha para un margen aproximado de 10%, con la boca hacia la derecha.
Conserva escenas, RenderSettings y pipeline globales.

Constantes/API: IconsFolder = Assets/UI/WeaponIcons, Width = 512, Height = 320,
WeaponIds, IconPath(id) y CreateAndIntegrate(). Importa Icon_ID.png como Sprite
Single sRGB, alpha, Clamp, bilineal, sin mipmaps/compresión y maxTextureSize 512.
Vincula icon/Icon si está vacío o pertenece a la carpeta generada; conserva
sprites personalizados externos. El argumento -surviveWeaponIconPreview
permite una plancha de revisión opcional desde los renders reales.

## Salud, derrota y menús

Estas clases viven en Assets/Scripts y no agregan responsabilidades de salud
o navegación de pantallas al ensamblado UPP. El flujo serializado está en
[Flujo de partida](FLUJO_PARTIDA_MENUS.md).

### PlayerHealth

Archivo: [PlayerHealth.cs](<D:/SurvivePrototype/Assets/Scripts/PlayerHealth.cs>)  
Estado: **Activo** en UPPPlayer.prefab

Inicializa la vida al habilitar una nueva instancia mediante Awake. Publica
CurrentHealth, MaxHealth e IsDead y recibe TakeDamage(amount). Ignora valores
no positivos, NaN e infinitos; limita la vida mínima a cero y emite OnDeath una
sola vez al morir. No mueve al jugador ni decide qué menú mostrar.

### PlayerDeathController

Archivo: [PlayerDeathController.cs](<D:/SurvivePrototype/Assets/Scripts/PlayerDeathController.cs>)  
Estado: **Activo** en UPPPlayer.prefab

Requiere PlayerHealth y se suscribe a OnDeath mientras está habilitado. La
propiedad DeathHandled permite observar si se ejecutó la respuesta a derrota.
Deshabilita arma y locomoción; cancela coroutines e invocaciones pendientes del
movimiento; detiene integración, Animator, IK y seguimiento de cámara; deja
el Rigidbody cinemático y sin velocidad. La Camera y el AudioListener continúan
presentando el mapa detrás del panel.

behavioursToDisable permite incluir controles adicionales, como interacción.
menu puede asignarse o resolverse buscando un MenuBehaviour de partida en la
misma escena. La respuesta llama ShowGameOver(). Si falta un menú, detiene el
tiempo, libera el cursor y registra una advertencia de configuración.

### MenuBehaviour

Archivo: [MenuBehaviour.cs](<D:/SurvivePrototype/Assets/Scripts/MenuBehaviour.cs>)  
Estado: **Activo** en el menú principal y PauseMenuCanvas

Controla escenas, paneles, pausa, derrota, victoria y cursor. Sus referencias
públicas son container (pausa), gameOverContainer (derrota) y victoryContainer
(victoria). isMainMenu distingue la pantalla inicial. gameplayScene y
mainMenuScene comienzan en Level 1 y SceneAlejandro. Publica IsPaused,
IsGameOver, IsVictory, IsFinished e IsMainMenu.

API para botones y otros sistemas:

- StartGame(), ChangeScene(sceneName) y BackToMainMenu();
- PauseGame() y ResumeGame();
- ShowGameOver(), ShowVictory() y ReloadCurrentScene();
- ExitGame().

Esc/Start de gamepad alternan pausa únicamente durante la partida. La pausa
guarda la escala de tiempo previa y libera el cursor; reanudar la restaura y
espera soltar el clic antes de volver a bloquear el cursor. Derrota y victoria
cierran pausa, muestran su panel y bloquean reanudar. El primer resultado
terminal se conserva. ChangeScene comprueba que la escena
esté disponible y restaura el tiempo antes de cargarla.

### MenuOptions

Archivo: [MenuOptions.cs](<D:/SurvivePrototype/Assets/Scripts/MenuOptions.cs>)  
Estado: **Integración de los controles existentes de opciones**

Conecta resolutionDropdown (TMP_Dropdown), fullscreenToggle (Toggle) y
volumeSlider (Slider) mediante listeners propios. Se coloca en el root activo
del Canvas para cargar preferencias aunque el panel de opciones esté cerrado.
Al habilitarse prepara una
lista de resoluciones sin tamaños duplicados e incluye el tamaño de ventana
actual. Al deshabilitarse retira los listeners que agregó.

API: SetResolution(index), SetFullscreen(enabled) y SetVolume(value). El
volumen usa AudioListener.volume entre cero y uno. Pantalla completa usa
FullScreenWindow y el modo ventana usa Windowed. persistSettings controla
PlayerPrefs; preferenceKeyPrefix comienza en Survive.Options. Se guardan
Width, Height, Fullscreen y Volume. El volumen pendiente se persiste al cerrar
opciones, pausar la aplicación o salir; no se guarda en disco cada cuadro al
arrastrar el slider. No aplica cambios de pantalla en batch mode.

## Integración serializada

**Ampliación de recargas del 24/10/2026: IMPLEMENTADA / VERIFICADA en Play Mode
controlado: 114/114 PASS, exit code 0.** Ocho WeaponDefinition reciben 32 cues en total desde cuatro cortes
reales de JU TPS bajo Assets/Audio/Weapons/Reload. Las fuentes Reload.wav y
Bolt Sliding.wav quedan sólo en evidencia externa. Los cortes conservan PCM16
estéreo a 48 kHz y pitch 1, con fades de 2 ms; no son ocho grabaciones específicas.
El audio actual suma 125 clips (102 de disparo, 19 de locomoción y cuatro de
recarga). Los conteos de la poda y las ejecuciones 178/178 de audio y 229/229
de VFX que siguen describen el estado anterior y no validan estos nuevos cues.
Procedencia y reporte de muestras en [Ítems y armas](ITEMS_ARMAS.md).
La ejecución usó una escena temporal aislada con prefab UPPPlayer y ocho armas
guardadas reales: TryReload/Animator, 32 fases configuradas, transferencia única,
cancelación/cambio/drop/disable/muerte, pausa/reanudación y final con frame largo
o reanudación que completa en el mismo frame. Cero errores de runtime,
24 capturas WAV y 2.087.936 frames de muestras AudioRenderer; los ocho mixes
naturales alcanzaron picos 0,191926–0,237857 sin clipping. El fixture no guardó
escenas. Los ocho datos conservan los campos anteriores salvo las dos nuevas
propiedades de recarga, según definition-preservation.json. Log:
[WeaponReloadAudio-Validation-Final.log](<D:/SurvivePrototype/Logs/WeaponReloadAudio-Validation-Final.log>).
El análisis FFT independiente pasó 69/69, exit code 0: 32 fases únicas,
ocho timings con desvío máximo 24,67 ms y 28 comparaciones de ganancia relativa
del mismo clip contra Pistol, con relación medida/esperada 0,999931–1,000014.
No afirma igualdad de ganancia absoluta entre PCM crudo y el mix procesado por
Unity. Reporte reload-audio-waveform-analysis.json enlazado en Ítems y armas.
La compilación final sin helper terminó **PASS, exit code 0**:
[WeaponReloadAudio-FinalCompile.log](<D:/SurvivePrototype/Logs/WeaponReloadAudio-FinalCompile.log>).
La auditoría posterior pasó con cero candidatos/bloqueos, 279 recursos retenidos
y 125 AudioClip:
[post-reload-audio-audit.json](<C:/Users/pc/.codex/visualizations/2026/10/19/01a153f2-ffb7-7f82-bfbd-04190b330ad4/reload-audio-evidence/post-reload-audio-audit.json>).
Manifest y lockfile se restauraron byte a byte contra Packages-before-validation;
se eliminaron el helper externo y sus fuentes. El SHA-256 de Level 1 coincide
con el punto inmediatamente anterior a esta suite, que ya incluía el cambio
de NPC de otra tarea; no se atribuye igualdad con el inicio de la entrega VFX.
Escucha manual y build distribuida NO VERIFICADAS.

**Ampliación actual del 24/10/2026:** GunFX URP y auditoría audio/VFX implementados
e integrados por setup de Editor PASS: ocho perfiles, 17 adaptadores, seis
superficies y decals en ambos renderers, exit code 0. Importación verificada:
155 assets/161 MiB, 87 carpetas y 397 archivos del cierre de 17 fuentes URP,
sin HDRP/Built-in/audio. El escaneo tuvo cero bloqueos y 1.273 candidatos
de 632,29 MiB. **Poda PASS: 1.273 assets y sus metas eliminados, cero fallos,
respaldo externo verificado**, exit code 0. **VFX runtime posterior: 229/229
PASS, exit code 0**, con cero errores de gameplay/red y 22 capturas de ocho
fogonazos, ocho políticas de vainas y seis superficies. Verifica un efecto por
tiro físico, impactos geométricos reales, revólver sin vainas duplicadas,
cancelaciones, pausa, pool acotado y descarga de escena. Se usó LMB real del
Input System en Level 1 con input sintético y adaptaciones temporales de
foco/cursor/geometría; las superficies se capturan aisladas en layer 30, con
fondo oscuro y sin resimular partículas. Estrés de pool en otra escena transitoria.
Log: [WeaponVfx-Validation-Final.log](<D:/SurvivePrototype/Logs/WeaponVfx-Validation-Final.log>).
Los dos helpers UPM se retiraron y manifest/lockfile quedaron restaurados
exactamente. Se retiraron aparte 32 carpetas vacías de audio/VFX con sus metas.
Level 1 y ambos archivos de Packages conservan SHA-256 exactos frente al respaldo
previo a VFX; no se guardaron cambios de escena desde la validación.
Las 22 capturas finales se revisaron sin defectos materiales detectados,
shaders rosas ni armas cruzadas; los impactos son visibles en el fixture aislado.
**Compilación final sin helpers PASS, exit code 0**:
[WeaponVfx-FinalCompile.log](<D:/SurvivePrototype/Logs/WeaponVfx-FinalCompile.log>).
El escaneo posterior del alcance registró cero candidatos, 275 recursos
retenidos, cero bytes candidatos y cero bloqueos. Estas pruebas controladas
no constituyen una sesión manual general ni una build distribuida.
Las comprobaciones históricas no verifican estos efectos ni la limpieza.

**Audio Modern Weapons del 24/10/2026: 178/178 PASS posterior a la poda**,
exit code 0, con cero errores de gameplay/red y 17 WAV reales de AudioRenderer
offline a 48 kHz estéreo. Picos de las ocho ráfagas: 0,2475–0,4633 sin clipping;
master, perdigones, pausa, variantes, pitch y setup idempotente comprobados.
Se usó Level 1 serializado con input sintético, foco/cursor temporal y otras
AudioSources aisladas; no se guardó la escena ni hubo escucha manual.
La pasada previa de 159 sin muestras no prueba salida; la final de 178 sí.
Log: [WeaponAudio-Validation-Final.log](<D:/SurvivePrototype/Logs/WeaponAudio-Validation-Final.log>).
Compilación final sin helpers PASS, exit code 0. Escucha manual y build
distribuida NO VERIFICADAS. Las cifras siguientes
son históricas y no prueban las grabaciones ni las nuevas capas de disparo.

**Entrega anterior del 19/10/2026:** mecanismos e imágenes están implementados e integrados;
el setup de Editor guardó 18 clips, ocho sprites transparentes y las seis
referencias de imágenes en la hotbar. El run final del 19/10/2026 entre las
10:02:46 y 10:02:51 de Buenos Aires pasó **142/142**, exit code 0, con cero
errores de gameplay y cero errores de red del Editor. Generó 26 capturas:
24 de reposo/disparo/recarga y dos de UI a 1920×1080/1280×720. Se revisaron las
ocho fotos, ambas capturas de hotbar y las 24 capturas de mecanismos de los
ocho presets, sin problemas materiales detectados. Algunas capturas de disparo
son tempranas; las mediciones por frame verifican el movimiento máximo.
Comprobó piezas reales, cadencia/reposo sin drift, recarga Humanoid/mecánica,
pausa/cancelaciones, sprites y slots vacíos/selección. La regresión de palanca
registró unos 52° y menos de 1 mm de traslación; los clips no apuntan al root,
Muzzle o LeftHandGrip. Los 30 prefabs Synty comparados y Level 1 mantuvieron sus hashes.

La prueba usa Level 1 real con input sintético y adaptaciones temporales de
foco/cursor, enemigos deshabilitados, closeups aislados y UI con cámara/fondo
temporales. No constituye una sesión manual ni una vista completa de gameplay.
La compilación final sin helper terminó **PASS, exit code 0** en
[WeaponPresentation-FinalCompile.log](<D:/SurvivePrototype/Logs/WeaponPresentation-FinalCompile.log>).
El helper fue retirado y manifest/lockfile se restauraron con hashes exactos,
sin diferencias de Git. Reporte y condiciones en
[Ítems y armas](ITEMS_ARMAS.md). Los resultados históricos siguientes permanecen separados.

**Entrega anterior de inventario:** PlayerWeaponInventory y PlayerWeaponHotbar están
integrados en UPPPlayer, sobre el Canvas existente. Pasaron **66/66** nuevas
comprobaciones en Play Mode controlado, exit code 0, el 19/10/2026 entre las
09:43:40 y 09:43:42 de Buenos Aires; no hubo excepciones de gameplay ni errores
de red del Editor. Se probaron seis instancias, E con capacidad libre/llena,
1–6/rueda/D-pad y vacíos, munición, reemplazo/drop, destrucción externa, LMB
sostenido y cambio durante Fire, recarga nativa silenciosa, bloqueos,
muerte y reintento mediante MenuBehaviour. Level 1 conserva el SHA256 previo.

Se revisaron cuatro capturas de la UI a 1920×1080/1280×720, con selección,
avisos y munición legibles y sin superposición con los HUD existentes. Son
renders del Canvas sobre cámara/RenderTexture temporal con fondo azul:
**no prueban el fondo de gameplay ni una sesión manual**. El run usó input
sintético y adaptaciones temporales de foco, cursor y cámara de interacción.
Se retiró el paquete externo hotbar-validation y manifest/lockfile se
restauraron exactamente contra sus backups. La compilación final sin helper
terminó **PASS, exit code 0**, en [Hotbar-FinalCompile.log](<D:/SurvivePrototype/Logs/Hotbar-FinalCompile.log>).
Level 1 conserva el SHA256 del baseline; manual, escucha y build siguen
**NO VERIFICADOS**. Reporte enlazado en [Ítems y armas](ITEMS_ARMAS.md).
El retiro del placeholder conserva la opción de ReloadSound real.
Las evidencias de 17/17 y 169/169 descritas abajo son anteriores a esta tarea.

La integración histórica de los componentes y sus referencias está guardada en
[UPPPlayer.prefab](<D:/SurvivePrototype/Assets/Prefabs/Player/UPPPlayer.prefab>),
[Weapon_Rifle.prefab](<D:/SurvivePrototype/Assets/Prefabs/Items/Weapon_Rifle.prefab>) y
[Level 1](<D:/SurvivePrototype/Assets/Scenes/Level 1.unity>). La ampliación de armas agrega ocho
presets, referencias del HUD y tres pickups nuevos mediante WeaponSystemSetup.
**Evidencia histórica del primer sistema, 19/10/2026:** Unity 6000.3.5f2 compiló; el setup
guardó ocho definiciones, ocho prefabs y HUD con referencias válidas. Level 1
incorpora exactamente Pistol, Shotgun y AssaultRifle bajo WeaponSystem_Presets,
con 231 líneas agregadas y 0 eliminadas frente al backup anterior a la tarea.
Pasaron 17/17 grupos controlados de Editor y Play Mode: assets y serialización,
aislamiento de munición, instancias nuevas, recarga parcial/cancelación,
intercambio/soltar/bloqueos, hitscan con colliders hijos/obstáculos/filtrado,
escopeta sin penetración al morir y con ocho trazadores, input real Fire/R/G,
E con intercambio/alcance/pared/pausa, recarga congelada por pausa y cancelada
por muerte. Se inspeccionaron dos capturas controladas de aim de rifle/pistola.

El paquete externo temporal se retiró y manifest/lockfile se restauraron
exactamente; la compilación final sin ese paquete terminó PASS, exit code 0.
La ampliación histórica de feel, anterior al inventario, pasó **169/169 comprobaciones** en Play Mode controlado,
con Unity 6000.3.5f2, reporte del 19/10/2026 a las 12:22:23 UTC y **44 capturas generadas**.
Incluye los ocho presets con LMB sostenido, recarga automática/real/reanudación,
apoyo de muñecas, alineación de cañón, retroceso, daño/obstáculos, movimiento
UPP de pie/crouch/prone, pausa, reserva finita y cancelación por intercambio,
soltar, roll, deshabilitación o muerte. Se revisaron las vistas frontal/lateral
de aim de las ocho armas y momentos seleccionados de recarga.
La captura controlada de prone quedó fuera del encuadre fijo de la cámara de
prueba; no constituye prueba visual de esa postura. Sus mediciones numéricas
de pose y daño sí pasaron.

El reporte registró **0 errores de gameplay y 1 timeout de red del Editor**:
SocketException/WebException ConnectFailure en stack asíncrono, con origen no
demostrado y excepción preservada. La prueba programática usó entradas
sintéticas, ajustes temporales de foco/cursor para batch, enemigos deshabilitados
y rango aislado; las adaptaciones no se guardaron en la escena. Los 17/17
anteriores permanecen como evidencia histórica separada.
La compilación final sin el paquete externo de validación terminó **PASS,
exit code 0**; ver [WeaponFeel-FinalCompile.log](<D:/SurvivePrototype/Logs/WeaponFeel-FinalCompile.log>).
El paquete externo fue retirado; manifest/lockfile se restauraron con SHA256
idéntico al backup. El SHA256 de Level 1 coincide exactamente con el anterior
a esta ampliación de presentación.
**NO VERIFICADO:** recorrido manual completo, escucha manual y build empaquetada.
Jugar no requiere repetir el setup; los ajustes se realizan desde el Inspector
sobre el prefab, preset o instancia. La evidencia histórica del 11/09/2026
permanece separada en [Ítems y armas](ITEMS_ARMAS.md).

## Límites de integración

El núcleo de movimiento, Animator, IK y debug no administra gameplay. Armas e
interacción dependen de sus estados públicos; PlayerHealth y PlayerDeathController
separan salud de respuesta a derrota, y MenuBehaviour coordina las pantallas.
El daño a enemigos se entrega a los componentes existentes del proyecto.

El sistema conserva seis armas por instancia y equipa una a la vez. No agrega
inventario general, stacking, melee, armaduras, consumibles, ragdoll, networking
ni guardado persistente. El disparo es hitscan, sin
proyectiles físicos ni penetración. El hand IK pertenece a PlayerWeaponPose.
Reutiliza idle y una recarga Humanoid de rifle de RIFLE PRO; no agrega
coreografías específicas de manos para cada mecanismo. Las piezas originales
de las armas sí usan clips locales propios; no implican munición por cartucho
ni vainas como Rigidbody de gameplay. GunFX agrega partículas cosméticas de
vainas y efectos de impacto, sin modificar daño ni munición.
Los disparos usan perfiles acústicos Modern Weapons; las recargas presentan
cuatro cues reales reutilizados de JU TPS por preset y admiten ReloadSound
como respaldo opcional sin cues válidos. La ampliación de recarga está
IMPLEMENTADA y VERIFICADA en Play Mode controlado: 114/114 PASS. La
munición y recarga pertenecen a WeaponItem/PlayerWeaponController, mediante
estados y eventos públicos estrechos, sin incorporarse al núcleo UPP.

## Política de pruebas y documentación

No hay clases, assemblies ni carpetas de tests internas en el sistema. Una
comprobación temporal debe vivir fuera de Assets y fuera del repositorio.

Las guías propias permanecen bajo Docs. Los enlaces hacia código o assets
parten desde esa carpeta y usan la raíz ../Assets.
