# Ítems, pickup y armas

## Alcance y estado de la entrega

El sistema de armas extiende el gameplay existente: MasterItem conserva la
flotación y el attachment; WeaponItem agrega configuración y munición por arma;
PlayerInteractionController selecciona el pickup; PlayerWeaponController
administra equipamiento, intercambio, recarga y disparo hitscan usando el aim
y la cámara de UPP. PlayerWeaponInventory conserva seis slots y
PlayerWeaponHotbar los presenta mediante eventos, junto al HUD del arma activa.

Se lleva **una sola arma equipada y hasta seis armas en el inventario**.
Cada slot conserva la instancia real del pickup, con su cargador y reserva;
las armas guardadas permanecen ocultas y no disparan. No se apilan armas ni
se implementa guardado persistente. Una partida nueva comienza sin armas.

**Ampliación actual del 24/10/2026: GunFX URP y auditoría de audio/VFX
IMPLEMENTADOS / INTEGRADOS / VERIFICADOS en Play Mode controlado después de
la poda: 229/229 PASS, exit code 0 y 22 capturas.** Se agregan perfiles por arma para fogonazo,
vainas cosméticas e impactos por superficie, sobre los eventos y la geometría
del disparo existente. La limpieza autorizada se limita a audio y VFX sin uso,
con manifiesto explícito y respaldo externo verificado antes de borrar.
La poda eliminó 1.273 assets y sus metas, sin fallos, con respaldo externo
verificado. Las 22 capturas se revisaron sin defectos materiales detectados;
la compilación final sin helpers terminó PASS, exit code 0.

**Ampliación de recargas del 24/10/2026: IMPLEMENTADA / VERIFICADA en Play Mode
controlado: 114/114 PASS, exit code 0 y 24 capturas WAV.** Los ocho presets tienen cuatro cues de audio por recarga,
sincronizados con el progreso real y reproducidos con pitch 1. Son cuatro
cortes de dos grabaciones mecánicas reales de JU TPS reutilizados por tipo,
no ocho grabaciones específicas. La evidencia de audio/VFX que sigue es anterior
a esta ampliación y permanece separada. El análisis FFT externo pasó 69/69
y la compilación final sin helper terminó PASS, exit code 0.

**Audio Modern Weapons del 24/10/2026: IMPLEMENTADO / INTEGRADO / VERIFICADO
en Play Mode controlado después de la poda: 178/178 PASS, exit code 0.**
WeaponAudioSetup vincula perfiles acústicos del paquete a los ocho presets.
Cada disparo presenta tres capas con variantes; no genera audio sintético.
El paquete no aporta clips nombrados de recarga; los cues agregados después
proceden de JU TPS. Los resultados del 19/10 que siguen son históricos y no
verifican estas referencias ni el nuevo audio.

**Entrega anterior del 19/10/2026: mecanismos propios e imágenes reales de
las ocho armas IMPLEMENTADOS / INTEGRADOS / VERIFICADOS en Play Mode controlado:
142/142 PASS y 26 capturas.** El setup de Editor terminó PASS:
guardó 18 clips locales de piezas y ocho sprites transparentes de 512 × 320,
vinculados a las definiciones y a la hotbar. Se revisaron las ocho fotografías,
las 24 capturas de mecanismos y la hotbar en ambas resoluciones.
La compilación final después de retirar el helper terminó **PASS, exit code 0**. Los
resultados históricos de inventario y feel descritos abajo se mantienen separados.

**Entrega anterior del 19/10/2026: inventario de seis slots, hotbar y retiro
del sonido provisional de recarga IMPLEMENTADOS / INTEGRADOS / VERIFICADOS en
Play Mode controlado: 66/66 comprobaciones PASS y cuatro capturas de UI.**
La compilación final después de retirar el helper externo terminó **PASS, exit code 0**.
Los resultados anteriores de feel permanecen separados de esta ejecución.

**Evidencia anterior del 19/10/2026, previa al inventario: IMPLEMENTADA /
INTEGRADA / VERIFICADA en Play Mode controlado.**
Los ocho presets disparan al mantener LMB, recargan automáticamente al vaciarse
y ejecutan la recarga Humanoid real de RIFLE PRO. Se agregaron apoyo de manos
mediante IK, retroceso por preset, apertura de la mira y audio sintético propio.
Pasaron **169/169 comprobaciones** y se generaron **44 capturas**, con revisión
visual de las ocho armas en aim y de momentos de recarga. Esas **169/169** no
validan los seis slots, la hotbar, los mecanismos/fotografías posteriores ni
el retiro posterior del audio de recarga.
La compilación final de esa entrega
después de retirar el paquete externo de validación terminó **PASS, exit code 0**. No se
hizo una sesión manual completa de Level 1 ni una revisión auditiva manual;
una build empaquetada sigue **NO VERIFICADA**. Los **17/17 grupos** del primer
sistema son evidencia histórica separada.

Código y assets propios:

- [MasterItem.cs](<D:/SurvivePrototype/Assets/Scripts/Items/MasterItem.cs>): pickup, suelo, flotación y attachment.
- [WeaponDefinition.cs](<D:/SurvivePrototype/Assets/Scripts/Items/WeaponDefinition.cs>): configuración compartida del arma.
- [WeaponItem.cs](<D:/SurvivePrototype/Assets/Scripts/Items/WeaponItem.cs>): arma recogible, munición y visuales.
- [PlayerWeaponController.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/PlayerWeaponController.cs>): equipamiento y combate.
- [PlayerInteractionController.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/PlayerInteractionController.cs>): selección e interacción.
- [PlayerWeaponHUD.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/PlayerWeaponHUD.cs>): nombre, cargador, reserva y estado.
- [PlayerWeaponInventory.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/PlayerWeaponInventory.cs>): seis slots, selección y conservación de instancias.
- [PlayerWeaponHotbar.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/PlayerWeaponHotbar.cs>): barra de slots, selección, nombre y munición.
- [WeaponMechanismAnimator.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/WeaponMechanismAnimator.cs>): presentación de las piezas móviles del arma.
- [WeaponMechanismSetup.cs](<D:/SurvivePrototype/Assets/Editor/WeaponMechanismSetup.cs>): clips propios de disparo y recarga por modelo.
- [WeaponIconSetup.cs](<D:/SurvivePrototype/Assets/Editor/WeaponIconSetup.cs>): fotografía de los modelos reales en una escena preview aislada.
- [WeaponPresentationSetup.cs](<D:/SurvivePrototype/Assets/Editor/WeaponPresentationSetup.cs>): integración de mecanismos, fotografías e inventario/UI.
- [WeaponInventorySetup.cs](<D:/SurvivePrototype/Assets/Editor/WeaponInventorySetup.cs>): integración del inventario y su hotbar en el Canvas existente.
- [PlayerWeaponPose.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/PlayerWeaponPose.cs>): agarre humanoide, apoyo y retroceso visual.
- [PlayerWeaponFeedback.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/PlayerWeaponFeedback.cs>): audio por eventos de disparo y recarga.
- [WeaponVfxProfile.cs](<D:/SurvivePrototype/Assets/Scripts/Items/WeaponVfxProfile.cs>): referencias, escalas y tiempos de efectos por preset.
- [WeaponVfxPresenter.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/WeaponVfxPresenter.cs>): fogonazo, vainas e impactos a partir del estado real del arma.
- [WeaponVfxPool.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/WeaponVfxPool.cs>): hasta 128 efectos reutilizables por escena.
- [WeaponImpactSurface.cs](<D:/SurvivePrototype/Assets/Scripts/Combat/WeaponImpactSurface.cs>): clasificación explícita de la superficie del collider.
- [WeaponVfxSetup.cs](<D:/SurvivePrototype/Assets/Editor/WeaponVfxSetup.cs>): adaptadores one-shot de GunFX URP y vinculación de ocho perfiles.
- [AssetAudioVfxAudit.cs](<D:/SurvivePrototype/Assets/Editor/AssetAudioVfxAudit.cs>): inventario externo de dependencias y poda explícita respaldada.
- [WeaponSystemSetup.cs](<D:/SurvivePrototype/Assets/Editor/WeaponSystemSetup.cs>): preparación idempotente de los assets.
- [WeaponFeelSetup.cs](<D:/SurvivePrototype/Assets/Editor/WeaponFeelSetup.cs>): integración central de esta ampliación.
- [WeaponAnimationSetup.cs](<D:/SurvivePrototype/Assets/Editor/WeaponAnimationSetup.cs>): idle y recarga originales de RIFLE PRO.
- [WeaponGripSetup.cs](<D:/SurvivePrototype/Assets/Editor/WeaponGripSetup.cs>): grips y componente de pose.
- [WeaponAudioSetup.cs](<D:/SurvivePrototype/Assets/Editor/WeaponAudioSetup.cs>): perfiles Modern Weapons, cues de recarga JU TPS y fuentes separadas.
- [UPPPlayer.prefab](<D:/SurvivePrototype/Assets/Prefabs/Player/UPPPlayer.prefab>): jugador canónico.
- [Weapon_Rifle.prefab](<D:/SurvivePrototype/Assets/Prefabs/Items/Weapon_Rifle.prefab>): conserva el root histórico, usa arte Synty de AssaultRifle y deja inactivos sus bloques anteriores.

## Controles y prueba en el mapa

| Acción | Teclado y mouse | Gamepad configurado en armas |
| --- | --- | --- |
| Apuntar | RMB alterna aim mediante UPP | Binding de Aim del asset UPP |
| Disparar | Mantener LMB; los ocho presets repiten hasta vaciarse | Mantener Right Trigger |
| Recoger / guardar; reemplazar si está lleno | E | Sin binding nuevo |
| Seleccionar un slot, incluso vacío | 1–6 o Numpad 1–6 | Sin selección directa nueva |
| Recorrer armas ocupadas | Rueda arriba: anterior; abajo: siguiente | D-pad Left / Right |
| Recargar | R | Button West |
| Soltar sólo el slot seleccionado | G | D-pad Down |
| Pausa | Esc | Start, mediante MenuBehaviour |

1. Abrí Level 1 o una escena con suelo y UPPPlayer. Arrastrá un prefab desde
   Assets/Prefabs/Items/Weapons si la escena no trae el pickup que querés probar.
2. El root marca la colocación sobre el suelo. Al entrar en Play Mode el arma
   flota, oscila y gira según los parámetros de MasterItem.
3. Acercate a menos de 2,5 metros del centro del arma y centrala en la cámara.
   Aparece el aviso con su nombre. Presioná E para recogerla; tocar el trigger
   no la equipa. No hace falta estar en aim para interactuar.
4. Alterná aim con RMB y mantené LMB. Los ocho presets repiten a su propia
   cadencia hasta vaciar el cargador, también pistola, revólver y rifles lentos.
5. Al vaciarse y quedar reserva, la recarga automática comienza después de
   0,18 segundos. R permite recargar antes. Observá la animación: la munición
   se transfiere al completar la duración del preset. Mantener LMB reanuda el
   disparo después de la recarga y su transición de salida; no dispara durante
   esa acción ni crea balas cuando se termina la reserva.
6. Recogé otra arma con E: ocupa el primer slot libre y queda equipada. Las
   anteriores se guardan con la misma munición. Con los seis slots ocupados,
   E reemplaza sólo el seleccionado y deja el arma anterior en el suelo del pickup.
7. Usá 1–6 para seleccionar cualquier slot. Un slot vacío deja al personaje
   desarmado; la rueda recorre sólo slots ocupados. Cambiar de arma cancela la
   recarga en curso sin transferir balas. Pausa, cursor libre, muerte, roll o
   controles deshabilitados bloquean la selección según el contrato de gameplay.
8. G suelta únicamente el arma seleccionada delante del jugador; conserva las
   otras cinco. Volver a recoger una usada mantiene su cargador y reserva.

## Presets y procedencia del arte

Las armas disponibles provienen de **Polygon Apocalypse**, en
[Prefabs/Weapons/Guns](<D:/SurvivePrototype/Assets/PolygonApocalypse/Prefabs/Weapons/Guns>).
Sidekick Characters aporta personajes y ropa; no se encontraron armas de ese
paquete en este checkout. El setup preserva los assets originales de Synty.

Cada preset usa un WeaponDefinition en **Assets/Data/Weapons/WD_ID.asset** y un
prefab propio en **Assets/Prefabs/Items/Weapons/Weapon_ID.prefab**. Los valores
iniciales son de prototipo; no constituyen balance validado.

| ID | Daño por perdigón | Perdigones | Alcance m | Tiros/s | Automática | Cargador | Reserva | Recarga s | Dispersión ° |
| --- | ---: | ---: | ---: | ---: | --- | ---: | ---: | ---: | ---: |
| Pistol | 24 | 1 | 60 | 4 | Sí | 12 | 72 | 1,5 | 0,35 |
| Revolver | 48 | 1 | 80 | 2 | Sí | 6 | 36 | 2,3 | 0,25 |
| SMG | 16 | 1 | 55 | 12 | Sí | 30 | 150 | 1,8 | 1,6 |
| AssaultRifle | 25 | 1 | 100 | 8 | Sí | 30 | 150 | 2,2 | 0,75 |
| Shotgun | 12 | 8 | 35 | 1,1 | Sí | 8 | 40 | 2,8 | 5 |
| HuntingRifle | 75 | 1 | 160 | 1,2 | Sí | 5 | 30 | 2,6 | 0,15 |
| SniperRifle | 110 | 1 | 240 | 0,8 | Sí | 5 | 20 | 3 | 0,05 |
| MachineGun | 22 | 1 | 120 | 10 | Sí | 60 | 180 | 3,8 | 1,3 |

| ID | Prefab de arte original |
| --- | --- |
| Pistol | SM_Wep_Pistol_01 |
| Revolver | SM_Wep_Revolver_01 |
| SMG | SM_Wep_SubMGun_01 |
| AssaultRifle | SM_Wep_AssaultRifle_01 |
| Shotgun | SM_Wep_Shotgun_01 |
| HuntingRifle | SM_Wep_HuntingRifle_Clean_01 |
| SniperRifle | SM_Wep_SniperRifle_01 |
| MachineGun | SM_Wep_MachineGun_01 |

Los prefabs propios contienen el modelo Synty como instancia anidada, sin sus
colliders de prop. El pickup usa su SphereCollider trigger y Rigidbody
cinemático. Los materiales Standard del paquete se adaptan a URP Lit en
Assets/Materials/Items/Weapons, sin reescribir el material original. La pistola
oculta el silenciador y la culata opcionales mediante overrides de la instancia.

El eje local +Z apunta al cañón y +Y hacia arriba. Las posiciones iniciales de
Muzzle se calcularon con la geometría de los FBX; el offset de agarre es un
punto de partida que necesita revisión visual con el personaje y sus posturas.

Las animaciones vienen del paquete autorizado
`E:/Unity/RIFLE_PRO_MoCap_Animation_Pack_vV2.7A2.unitypackage`, importado completo
en **Assets/Rifle_27A2_Pro**: 950 entradas y 895 FBX. El archivo original se
preserva. El idle de aim usa `Animation/IPC/Loops/W2_Stand_Aim_Idle_IPC.fbx`;
la recarga usa `Animation/IPC/W2_Stand_Aim_Reload_IPC.fbx`, clip Humanoid de
2 segundos con Avatar MotusMan_v55. Se referencia el subasset original.
Una misma recarga Humanoid de rifle se reutiliza en los ocho presets. Las
manos del personaje no tienen una coreografía específica para cada cargador,
tambor o cartucho; las piezas de las armas tienen los clips locales propios
descritos a continuación.

Los modelos Synty **sí permiten animar sus mecanismos**: ya traen piezas
separadas con sus propios Transform y MeshFilter. Pistol, SMG y AssaultRifle
incluyen Slide, Magazine y Trigger; Revolver incluye Cylinder, Cylinder_Spin,
Hammer y balas individuales; HuntingRifle y SniperRifle incluyen Bolt;
Shotgun tiene Handle y Trigger, y MachineGun añade Magazine y Slide.
Se animan esas piezas originales mediante posiciones y rotaciones locales,
sin separar mallas ni reescribir los assets Synty. RIFLE PRO sigue aportando
el movimiento del personaje. Los mecanismos son una autoría propia sobre los
prefabs de armas del proyecto; no son clips importados desde los FBX Synty.

## Mecanismos y fotografías de las armas

WeaponMechanismSetup genera **18 AnimationClip legacy** bajo
[Assets/Animations/Weapons](<D:/SurvivePrototype/Assets/Animations/Weapons>): Fire y Reload para
cada uno de los ocho presets, más dos clips AssaultRifle_Legacy para el rifle
histórico. Este último usa sus propias rutas de binding porque el arte está
bajo SyntyVisual. Los clips contienen curvas absolutas de posición y rotación
local de las piezas; no agregan root motion ni controlan munición.

| Preset | Piezas durante el disparo | Piezas durante la recarga |
| --- | --- | --- |
| Pistol | Trigger y Slide | Magazine y Slide |
| Revolver | Trigger, Cylinder_Spin y Hammer | Apertura de Cylinder, giro de Cylinder_Spin y movimiento visual de las seis balas existentes |
| SMG | Trigger y Slide | Magazine y Slide |
| AssaultRifle | Trigger y Slide | Magazine y Slide |
| Shotgun | Trigger y ciclo de palanca de Handle | Ciclo de palanca; sin inserción individual de cartuchos |
| HuntingRifle | Trigger y ciclo de Bolt | Apertura, desplazamiento y cierre de Bolt |
| SniperRifle | Trigger y ciclo de Bolt | Magazine y Bolt |
| MachineGun | Trigger y Slide | Magazine y Slide |

WeaponMechanismAnimator vive en cada wrapper y ejecuta LateUpdate con orden
1005, antes de la pose de manos (1050). Escucha WeaponItem.ShotPresented,
emitido por ShowShot sólo para tracerIndex 0: una escopeta anima un ciclo por
disparo físico, no uno por perdigón. Muestrea Fire mediante SampleAnimation y
tiempo escalado; el ciclo cabe entre tiros según la cadencia. Reload usa el
ReloadProgress real del arma. No inicia recargas, no consume ni transfiere
balas y no aplica daño. Cada muestreo usa curvas locales absolutas, sin sumar
desplazamientos sobre el cuadro anterior.

Las piezas vuelven a su reposo al completar/cancelar la acción, soltar,
guardar, morir o deshabilitar el control. La pausa conserva la fase. El
Handle de Shotgun es una palanca bajo el gatillo: rota sobre su pivote, sin
deslizarse como un bombeo. El LeftHandGrip permanece en el apoyo fijo del guardamanos.
La munición de recarga sigue transfiriéndose completa al finalizar la duración
del preset: estas animaciones no implementan carga de cartuchos uno a uno
ni estado balístico por bala del tambor. La ampliación GunFX presenta vainas
cosméticas mediante partículas por separado, sin agregar física de munición.

WeaponIconSetup fotografía exclusivamente la jerarquía visual Synty del
wrapper, en una escena preview aislada con tres luces y cámara ortográfica.
Conserva los materiales URP y la silueta real; no usa imágenes generadas por IA.
Deshabilita lógica, FX y colliders de la copia temporal. El fondo es transparente,
la boca apunta a la derecha y el encuadre se centra desde el alpha de un primer
render para dejar aproximadamente 10% de margen. No guarda escenas ni modifica
RenderSettings o el pipeline global.

Los ocho archivos [Assets/UI/WeaponIcons](<D:/SurvivePrototype/Assets/UI/WeaponIcons>) se llaman
Icon_ID.png. Se importan como Sprite Single de **512 × 320**, sRGB, alpha de
entrada, sin mipmaps ni compresión, Clamp y filtro bilineal. WeaponDefinition
guarda el Sprite en icon y lo expone mediante Icon. El generador actualiza
referencias vacías o propias de esa carpeta y preserva un sprite personalizado
de otra ubicación. El argumento opcional `-surviveWeaponIconPreview` permite
guardar una plancha de revisión con los ocho renders fuera de los assets.

## Preparación idempotente

**Survive/Weapons/Crear presets e integrar jugador** ejecuta
WeaponSystemSetup.CreateAndIntegrate(). Crea los ocho presets faltantes,
agrega WeaponHUD al Canvas gameplay existente y vincula el rifle histórico con
WD_AssaultRifle. Después llama a **WeaponFeelSetup.CreateAndIntegrate()**, que
integra animaciones, grips, datos de retroceso y audio; requiere RIFLE PRO ya
importado. Esa operación fija los ocho presets en Automatic=true y configura
sus valores de presentación. No modifica Level 1. CreatePresets() permite
generar solamente los datos y prefabs faltantes.

Las operaciones de presentación también están disponibles por separado:
WeaponGripSetup.ConfigureGripsAndPlayer(), WeaponAnimationSetup.CreateAndIntegrate(),
WeaponAudioSetup.CreateAndIntegrate() y WeaponPresentationSetup.CreateAndIntegrate().
La última ejecuta, en orden, WeaponMechanismSetup, WeaponIconSetup y
WeaponInventorySetup; también está disponible en **Survive/Weapons/Animar
mecanismos y fotografiar las ocho armas**. WeaponFeelSetup llama a esa entrada.
El setup central serializa el Animator,
reloadAnimationClip y automaticReloadOnEmpty del controlador. WeaponAudioSetup
requiere las grabaciones originales bajo
Assets/Modern Weapons ALL-IN-ONE Sound System/Sounds y valida todos los
recursos antes de modificar los presets. Reemplaza las referencias de disparo
del prototipo por perfiles de tres capas; un ShotSound personalizado ajeno
al prototipo y al paquete conserva su configuración. Para recargas requiere
los cuatro cortes JU TPS bajo Assets/Audio/Weapons/Reload y configura cuatro
cues por preset, con ganancia por cue y por recarga. Preserva un ReloadSound
o cues con clips personalizados ajenos a esa carpeta. No genera WAV ni
regenera el placeholder eliminado. También se ejecuta por
**Survive/Weapons/Integrar audio por tipo y recargas JU TPS**. No hace falta
repetir estas operaciones para jugar una integración ya guardada.

**Survive/Weapons/Integrar inventario y hotbar de seis slots** ejecuta
WeaponInventorySetup.CreateAndIntegrate(). Agrega/vincula PlayerWeaponInventory
y crea WeaponHotbar en PlayerInteractionHUD, sin otro Canvas. La entrada de
presentación invoca esta integración, que a su vez ejecuta el setup de audio.
La barra presenta seis tarjetas con fotografía, número, nombre, munición y selección; la operación
reconstruye únicamente su propio contenedor WeaponHotbar. El setup y la
ejecución anterior de 66 comprobaciones pasaron antes de agregar las fotografías;
en esa entrega se revisó la UI a
1920×1080 y 1280×720 sin superposición con el HUD existente.

**Survive/Weapons/Integrar tres pickups en Level 1** ejecuta IntegrateLevel1()
por separado. La integración ejecutada agrega Pistol, Shotgun y AssaultRifle
bajo WeaponSystem_Presets, cerca del jugador real de la escena. Antes de
modificarla, busca tres posiciones con piso, altura y pendiente aceptables,
espacio libre y trayecto directo desde el jugador. Si no las encuentra,
rechaza la operación. También rechaza una Level 1 abierta con cambios sin
guardar y evita duplicar el contenedor en una segunda ejecución.

El setup y la integración se ejecutaron con resultado **PASS el 19/10/2026**.
La escena agregó 231 líneas y no quitó ninguna frente al backup anterior a la
tarea. La integración está serializada; jugar no requiere repetir el setup.

## Interacción y aviso

PlayerInteractionController delega en PlayerWeaponInventory.TryCollect.
Busca el primer slot libre y equipa el pickup; con capacidad completa reemplaza
el seleccionado y coloca el anterior en el suelo del pickup. No destruye ni
recrea instancias para cambiar de slot. En jugadores sin el componente de
inventario conserva la ruta histórica TrySwapWeapon del controlador.

| Campo | Valor inicial / contrato |
| --- | --- |
| Interaction Distance | 2,5 m desde el origen de interacción del jugador al centro del arma |
| Interaction Origin Offset | (0, 1, 0), en espacio local del jugador |
| Source Camera | Referencia explícita; si falta o está inactiva, Camera.main |
| Hit Mask | Incluye pickups, paredes y superficies que bloquean la recogida |
| Require Locked Cursor | Activo: bloquea interacción con cursor libre |
| Interact Action | E, editable en el Inspector |
| Prompt Canvas Group / Prompt Text | CanvasGroup y TMP_Text del aviso existente |
| Aviso con inventario | [E] Guardar arma / [E] Reemplazar slot N, seguidos por el nombre |

El raycast sale del centro de la cámara, incluye el trigger del pickup e ignora
los colliders propios. Además comprueba los trayectos cámara-centro del arma y
jugador-centro del arma. No alcanza con que un trigger sobresalga de una pared.
La interacción se bloquea al pausar, morir, hacer roll, deshabilitar controles
o activar DisableAllMove. Con Require Locked Cursor también se bloquea en menús.

API: CurrentTarget, HasTarget, InteractionDistance, RefreshTarget() y
TryInteract(). TryInteract vuelve a consultar alcance, objetivo y obstáculos.

## Suelo, flotación y física

Estos campos pertenecen a MasterItem y se heredan en WeaponItem:

| Campo | Valor inicial | Uso |
| --- | ---: | --- |
| Item Type | Generic / WeaponType | Clasificación; WeaponItem fija WeaponType |
| Hover Height Centimeters | 100 | Altura del root sobre el suelo detectado |
| Bob Amplitude Centimeters | 5 | Oscilación vertical; 0 la desactiva |
| Bob Cycles Per Second | 0,65 | Frecuencia de oscilación |
| Rotation Degrees Per Second | 45 | Giro sobre Y; 0 lo desactiva |
| Ground Layers | Todas | Superficies posibles de apoyo |
| Ground Probe Start Height | 2 m | Altura desde donde se busca el piso |
| Ground Probe Distance | 50 m | Distancia adicional del raycast hacia abajo |

Con los valores iniciales el root oscila entre 95 y 105 cm sobre el piso. La
medición corresponde al root, no al borde inferior del modelo. PlaceInWorld()
vuelve a buscar apoyo y aplica los valores desde ese punto, sin acumular offsets.
La búsqueda ignora triggers, el propio ítem y jugadores con controlador de armas.
Si no encuentra piso, toma la posición de colocación como referencia.

MasterItem requiere SphereCollider y Rigidbody. El collider funciona como
trigger; el Rigidbody es cinemático y no usa gravedad. Equipar desactiva los
colliders y la detección física, cambia las layers a las del jugador y conserva
la escala mundial al adjuntar. PlaceInWorld restaura colliders y layers. No
agregues MasterItem y WeaponItem juntos: WeaponItem ya hereda la base.

Al guardar un arma, el inventario la mantiene adjunta a InventoryStorage bajo
el mismo dueño y desactiva su GameObject. No pasa por PlaceInWorld ni pierde
su munición. MasterItem.IsEquipped incluye los objetos adquiridos guardados;
IsOwnedBy(owner) valida esa pertenencia y TryMoveOwnedAttachment mueve el
attachment internamente entre mano y almacenamiento. Para consultar el arma
activa se usa PlayerWeaponController.EquippedWeapon, no ese flag del ítem.

## Pose y manos

UPPPlayer trae WeaponSocket como hijo de hand_r. Si falta, el controlador busca
otro WeaponSocket y después la mano derecha Humanoid. Como último recurso crea
un socket relativo al jugador, con los offsets de fallback configurados.

Equipped Local Position y Equipped Local Euler Angles ajustan cada arma;
Attachment Position Offset y Attachment Euler Offset agregan el ajuste del
jugador. El root representa el agarre. Muzzle es un Transform hijo en la boca
del cañón con +Z hacia adelante. Align Weapon While Aiming orienta el objeto
equipado al objetivo sin cambiar los huesos.

Los hijos de los estados existentes Strafe Torso Pose usan el idle Humanoid
`W2_Stand_Aim_Idle_IPC` de RIFLE PRO; se conserva el grafo de locomoción.
PlayerWeaponPose usa orden 1050: en OnAnimatorIK limpia los pesos de objetivos
de manos y registra que el Animator se evaluó en el cuadro actual. Después,
en LateUpdate, ajusta los huesos reales tras cámara/trace UPP y antes del
disparo (orden 1100). La muñeca derecha orienta el arma; el brazo izquierdo
resuelve dos segmentos rígidos hacia el apoyo, sin estirar ni escalar huesos.
El Animator del jugador usa AlwaysAnimate para evaluar también fuera de cámara.
WeaponItem expone LeftHandGrip y TwoHanded: el apoyo sigue el grip de cada
prefab, incluidas las ocho armas. Los marcadores se colocan hacia atrás para
quedar al alcance físico del brazo. Los dedos conservan las curvas del clip;
no hay simulación individual de contacto de dedos con la geometría.

El ajuste admite de pie, crouch y prone; se libera al recargar, hacer roll,
morir o deshabilitar controles, para no competir con esas acciones. El solver
trabaja sobre la animación fresca de cada cuadro y conserva la máscara de
piernas original; no calibra objetivos normalizados Humanoid. Mientras actúa,
el controlador delega en la pose la alineación al objetivo. El retroceso visual
usa recoilDegrees, recoilBackDistance y recoilRecoverySpeed del preset,
con límites y recuperación; no modifica daño ni la cámara. Las mediciones del
run controlado verificaron apoyo de muñecas y alineación de cañón en los ocho
presets; las capturas de aim y recarga se revisaron visualmente. Esto no valida
una coreografía de manos distinta para cada mecanismo ni un playtest manual completo.

La capa **Weapon Actions** contiene Empty y Reload, con máscara
WeaponUpperBody para torso, brazos, dedos e IK de manos, sin raíz ni piernas.
Su peso serializado es 0; el controlador lo activa sólo durante recarga y la
transición de salida. Reload lleva la etiqueta WeaponReload. WeaponReloading
es bool y WeaponReloadSpeed es float: la velocidad se calcula como
`reloadAnimationClip.length / weapon.ReloadDuration`, usando la duración real
del clip. El disparo espera también a que termine la salida de esta capa.

## Configuración, munición y recarga

WeaponDefinition contiene displayName, damage, range, shotsPerSecond,
automatic, magazineSize, startingReserveAmmo, reloadDuration, pelletsPerShot,
spreadDegrees, recoilDegrees, recoilBackDistance, recoilRecoverySpeed,
shotSound, shotVariations, shotMechanismVariations, shotTailVariations,
reloadSound, reloadAudioCues, reloadVolume, shotVolume, shotMechanismVolume,
shotTailVolume, icon y vfxProfile.
ShotVariations, ShotMechanismVariations y ShotTailVariations exponen las tres
listas como IReadOnlyList<AudioClip>; ShotVolume, ShotMechanismVolume y
ShotTailVolume exponen sus ganancias de 0 a 1. ShotSound es el respaldo de
la capa cercana si no tiene variantes. Es configuración compartida; modificarla
afecta a las armas que la referencian. WeaponItem sin Definition usa sus campos locales
históricos y los valores locales nuevos de munición, conservando compatibilidad.
ReloadAudioCue contiene normalizedTime, clip y volume; ReloadAudioCues expone
la lista de lectura y ReloadVolume la ganancia general de recarga. ReloadSound
se conserva como respaldo cuando no hay cues válidos.

Cada instancia de WeaponItem guarda AmmoInMagazine, ReserveAmmo e IsReloading.
Comienza con el cargador lleno y la reserva del preset. Un disparo consume una
bala, incluso si produce varios perdigones o queda bloqueado por una pared.
Cambiar, soltar o volver a recoger el arma conserva esa munición. Agotar el
cargador inicia la recarga automática si automaticReloadOnEmpty está activo
(valor del prefab: true), queda reserva y el jugador puede operar el arma.
El margen inicial es de 0,18 segundos; R continúa disponible para recarga manual.

TryStartReload() rechaza cargador lleno, reserva vacía o una recarga ya activa.
Al completar reloadDuration, transfiere solamente las balas necesarias y
disponibles. TickReload(deltaTime) recibe tiempo escalado del controlador:
pausar congela el avance. Soltar, cambiar, morir, hacer roll o deshabilitar el
control cancela la recarga sin transferir balas.

API de WeaponItem: Definition, DisplayName, Damage, Range, ShotsPerSecond,
Automatic, MagazineSize, ReloadDuration, PelletsPerShot, SpreadDegrees,
AmmoInMagazine, ReserveAmmo, HasAmmo, IsReloading, ReloadProgress,
TryConsumeRound(), TryStartReload(), TickReload(deltaTime), CancelReload(),
AddReserveAmmo(amount) y ResetAmmunition(). Esta última reinicia expresamente
el estado; equipar o soltar nunca la invoca.

## Disparo hitscan, obstáculos y feedback

Fire, Reload y DropWeapon son acciones propias del controlador; no se agregan
al asset de locomoción. Disparar requiere aim, arma habilitada, cargador con
balas, ausencia de recarga, movimiento habilitado, jugador vivo y juego sin
pausa. También respeta roll, DisableAllMove y Require Locked Cursor.

El objetivo usa el rayo central de la cámara UPP con el alcance y Hit Mask del
arma. Se verifica el volumen de seguridad del cañón y el trayecto cuerpo-cañón
antes del tiro. Después se resuelve cada perdigón desde Muzzle; spreadDegrees
define la dispersión angular. El controlador limita a 64 perdigones por tiro.
Las consultas saturadas repiten la búsqueda con el conjunto completo para no
perder un obstáculo por el tamaño del buffer.

Todos los impactos de un disparo se calculan antes de aplicar daño. Así, la
muerte y desactivación del collider por el primer perdigón no permite que los
siguientes atraviesen ese mismo enemigo. Cada impacto busca EnemyHealth en el
collider o sus padres y entrega Damage al componente existente. La geometría
sin EnemyHealth bloquea el tiro y no recibe daño.

ShowShot(start, end, hit, tracerIndex = 0) presenta los trazadores y emite
ShotPresented sólo desde el índice cero. El pool de trazas reutiliza hasta
64 LineRenderer, uno por perdigón, con sharedMaterial. El setup GunFX retira
el MuzzleFlash provisional y delega el fogonazo a WeaponVfxPresenter.
ShowImpact(RaycastHit) emite ImpactPresented únicamente con collider y normal
válidos. El controlador lo llama por cada impacto geométrico real, incluida
la obstrucción entre cuerpo y cañón; el bool visual hit de ShowShot no fabrica
un impacto para un tiro bloqueado sin RaycastHit. Los impactos se presentan
antes de aplicar el daño, conservando el bloqueo de perdigones del mismo tiro.
No hay proyectiles físicos, velocidad de bala ni penetración. La mira propia
funciona con DrawDebug apagado, cambia de color durante recarga y muestra
feedback breve al impactar un enemigo. Oculta y restaura UPPDebugCrosshair.
La apertura de la mira responde al retroceso y se recupera después del tiro.

PlayerWeaponFeedback escucha ShotFired, WeaponChanged y WeaponStateChanged.
Por cada tiro físico, incluidos los tiros de varios perdigones, solicita tres
PlayOneShot simultáneos en WeaponAudio/Shots: capa cercana OneShot, capa Stop
y cola Tail, cada una con su ganancia. Stop es parte del sonido del disparo;
no se sincroniza como una acción separada de cerrojo, palanca o recarga.
Las variantes se eligen con System.Random local, sin consumir UnityEngine.Random
ni alterar el spread de gameplay. Cada capa evita repetir el clip anterior
si dispone de otra opción; el historial se reinicia al cambiar de Definition.
El pitch del disparo se mantiene en 1 para conservar la afinación original.
No se usa la capa Dist para el arma local cercana.

WeaponAudio/Reload permanece separado de Shots y de las fuentes existentes
de pasos. Los ocho presets presentan cuatro cues de JU TPS cada uno al alcanzar
su normalizedTime en ReloadProgress, sin repetirlos durante la misma recarga.
El pitch permanece en 1 y cada corte conserva su duración; no se estira una
secuencia completa para cubrir toda la animación. ReloadSound sigue vacío en
estos presets porque usan cues. Un ReloadSound personalizado sin cues válidos
se reproduce una vez como respaldo, también con pitch 1. Cancelar, cambiar,
soltar, morir o deshabilitar corta la fuente; completar naturalmente deja
terminar la cola del último cue. Al rehabilitar feedback en mitad de una
recarga se omiten las fases ya pasadas. El placeholder Reload_Weapon.wav
continúa retirado y el setup no lo regenera.
LateUpdate con orden 1200 pausa/reanuda las fuentes después del controlador
de armas (1100), también si MenuBehaviour pausó durante ese mismo frame.
Deshabilitar feedback o morir detiene sus fuentes.

API del controlador: EquippedWeapon, Health, Inventory, TryEquip(weapon),
TrySwapWeapon(weapon), TryFire(), TryReload() y
DropEquippedWeapon(position, rotation). TryFire() programático conserva las
mismas condiciones y cadencia del input.

## HUD y eventos

PlayerWeaponHotbar ocupa el centro inferior del Canvas PlayerInteractionHUD.
Su panel mide 660 × 146 unidades de UI, con margen inferior de 28 y tarjetas
de 100 × 116. Cada fotografía ocupa 90 × 56 y conserva su proporción.
Muestra los seis slots, el seleccionado, fotografía, nombre y `cargador / reserva` de cada
instancia; usa estados de vacío, recarga y falta de balas. No captura clicks ni
bloquea raycasts. Escucha PlayerWeaponInventory.Changed y los eventos existentes
del controlador/salud; no administra equipamiento ni actualiza munición por
sondeo continuo. Las seis referencias iconImages usan WeaponDefinition.Icon;
el slot vacío no muestra imagen y el cambio de selección conserva el sprite
de la instancia correspondiente. El HUD del arma activa permanece abajo a la derecha.

WeaponItem emite StateChanged al consumir munición, iniciar/completar/cancelar
recarga, sumar reserva o reiniciar el estado. ReloadCompleted se emite sólo
al completar la recarga, después de transferir munición y antes de StateChanged;
permite recuperar cues saltados por un frame largo y conservar su cola natural.
Cancelar nunca emite ese evento. ShotPresented informa una
presentación por disparo físico, desde el trazador de índice cero.
PlayerWeaponController emite
WeaponChanged, WeaponStateChanged y ShotFired; los tres entregan WeaponItem.

PlayerWeaponHUD se coloca bajo PlayerInteractionHUD, en la esquina inferior
derecha; el HUD de salud existente queda a la izquierda. Recibe referencias
serializadas a weaponController, playerHealth, weaponNameText, ammoText,
statusText y hudCanvasGroup. Presenta nombre, `cargador / reserva`, recarga,
falta de munición e instrucciones. Suscribe eventos al habilitarse y los
retira al deshabilitarse. Oculta el panel si el controlador está deshabilitado
o el jugador murió. No consulta ni modifica munición cada cuadro.

## Efectos GunFX URP del 24/10/2026

El paquete fuente es
Realistic_Gun_VFX_Muzzle_Flash_Bullet_Impact_Ejections_Gun_VFX_VFX_v1.0 (1).unitypackage.
De su GunFX_URP.unitypackage anidado se importó el cierre de dependencias de
17 prefabs fuente: cinco fogonazos, seis expulsores de vainas y seis impactos.
Son **155 assets, 168.848.693 bytes (aproximadamente 161 MiB), 87 carpetas y
397 archivos verificados**, bajo Assets/HIVEMIND/GunFX/URP. La importación no
incluye HDRP, Built-in ni audio. Reporte:
[urp_import_report.json](<C:/Users/pc/.codex/visualizations/2026/10/19/01a153f2-ffb7-7f82-bfbd-04190b330ad4/vfx-evidence/urp_import_report.json>).

WeaponVfxSetup.CreateAndIntegrate(), disponible en **Survive/Weapons/Integrar
GunFX URP en las ocho armas**, crea 17 adaptadores bajo
Assets/Prefabs/VFX/Weapons y ocho WVFX_* bajo Assets/Data/Weapons/VFX.
Vincula WeaponDefinition.VfxProfile y un WeaponVfxPresenter por wrapper,
incluido el rifle histórico. Conserva los 17 prefabs fuente y sus dependencias
para regenerar los adaptadores sin reescribir los prefabs fuente.

Los adaptadores desactivan loop, playOnAwake y tiempo no escalado. Los de
fogonazo/vainas usan bursts de una sola ejecución; cada expulsión produce una
vaina. Se retiran scripts de terceros, incluido el spawner de colisiones de
sangre que creaba objetos fuera del pool, además de audio, Rigidbody y Collider
de los adaptadores. Las colisiones de partículas no aplican fuerza al gameplay.
No se modifica el prefab fuente para hacer estos cambios.

La auditoría posterior a Unity verificó **24 prefabs nativos GunFX sin cambios
de SHA-256**, incluidos los que entran como dependencia. Los 32 materiales
recibieron la migración de serialización URP de versión 9 a 10: conservan GUID,
shader, texturas y colores. Doce actualizan _DstBlendAlpha de 0 a 10, los dos
materiales de sangre incorporan _EmissiveMultiplier=2 y Glass cambia
m_LightmapFlags de 0 a 6. Por eso no se afirma que los 155 assets nativos sean
idénticos byte a byte después de abrirlos en Unity. Reporte:
[material-migrations.json](<C:/Users/pc/.codex/visualizations/2026/10/19/01a153f2-ffb7-7f82-bfbd-04190b330ad4/vfx-evidence/material-migrations.json>).

WeaponVfxPresenter escucha ShotPresented, ImpactPresented y StateChanged.
Un tiro físico produce un fogonazo anclado a Muzzle, aunque tenga varios
perdigones. Las vainas salen de ShellEjectionPort, calibrado por modelo,
no desde la punta del cañón. Los tiempos configurados son:

| Preset | Expulsión cosmética |
| --- | --- |
| Pistol | 0,025 s después del tiro |
| SMG | 0,015 s después del tiro |
| AssaultRifle | 0,020 s después del tiro |
| MachineGun | 0,015 s después del tiro |
| Shotgun | 0,24 s después del tiro, durante el ciclo de palanca |
| HuntingRifle | 0,28 s después del tiro, durante el ciclo de cerrojo |
| SniperRifle | 0,32 s después del tiro, durante el ciclo de cerrojo |
| Revolver | Retiene las vainas disparadas y las expulsa al 39 % de la recarga |

El contador de vainas del revólver pertenece a la instancia visual y se conserva
al guardar/soltar. Si la recarga se cancela después de expulsarlas, reiniciarla
no vuelve a emitir las mismas vainas. No decide balas disponibles, daño ni
transferencia de munición; ésta sigue ocurriendo al finalizar la recarga real.
Cambiar, guardar, soltar, morir o deshabilitar cancela expulsiones diferidas
y retira el fogonazo anclado; vainas/impactos ya emitidos terminan en el mundo.

WeaponImpactSurface declara Concrete, Metal, Wood, Dirt, Flesh o Glass en el
collider o sus padres. Tiene prioridad sobre la detección automática: EnemyHealth
es Flesh, TerrainCollider es Dirt y los nombres de sharedMaterial físico permiten
reconocer metal, madera, vidrio o tierra; el resto usa Concrete. No instancia
materiales de render. Cada impacto se orienta con la normal real de RaycastHit,
separado 5 mm de la superficie. Si EnemyHealth ya tiene partículas propias de
impacto, se respeta ese efecto y no se superpone otra sangre de GunFX.

WeaponVfxPool mantiene un máximo de 128 entradas por escena, sin persistir entre
escenas. Reutiliza las instancias, recupera la de vencimiento más próximo si se llena y utiliza
tiempo escalado y RNG local para partículas, sin consumir el azar del spread.
La pausa congela simulación y vencimientos. El setup agrega DecalRendererFeature
a PC_Renderer y Mobile_Renderer sólo si falta, conservando las otras features.
Son efectos cosméticos: el disparo sigue siendo hitscan, sin proyectiles físicos.

**Estado: importación y setup de Editor VERIFICADOS**, este último con ocho
perfiles, 17 adaptadores, seis superficies y decals en ambos renderers, exit code 0.
Log: [WeaponVfx-Setup.log](<D:/SurvivePrototype/Logs/WeaponVfx-Setup.log>).
**Validación posterior a la poda: 229/229 PASS**, exit code 0, el 24/10/2026
entre 13:55:11 y 13:55:16 de Buenos Aires (16:55:11–16:55:16 UTC), con cero
errores de gameplay y cero errores de red del Editor. Generó 22 capturas de
los ocho fogonazos, las ocho políticas de vainas y los seis impactos, con
partículas visibles y cero píxeles de error de shader rosa en la comparación
de render. Comprobó un fogonazo por tiro físico, fallos de tiro sin impacto
fabricado, geometría/normal real, seis superficies y ausencia de sangre duplicada.
También verificó demoras de vainas desde la recámara, retención del revólver
al guardar, cancelación antes/después de expulsar sin duplicación, recarga sin
transferencias extra, pausa, drop/disable/muerte, pool acotado y descarga de escena.

Los tiros usan LMB de Input System en el PlayerLoop real con los prefabs y
jugador serializados de Level 1. El fixture adapta foco/cursor e input en
memoria, deshabilita enemigos y usa colliders/cámara transitorios en layer 30
para separar la geometría de prueba del mapa. Las seis capturas de superficie
aislaron sólo el render a esa layer, sin mover ni resimular partículas. El
fondo oscuro es deliberado para el closeup, no el fondo del gameplay.
La comparación activa/desactiva temporalmente ParticleSystemRenderer en el
mismo frame y los restaura. El estrés de capacidad/descarga llama al pool
directamente en otra escena transitoria y se distingue de los tiros reales.
No se guardaron escena, materiales, prefabs ni Definitions desde el fixture.
Reporte: [vfx-report.json](<C:/Users/pc/.codex/visualizations/2026/10/19/01a153f2-ffb7-7f82-bfbd-04190b330ad4/vfx-evidence/vfx-report.json>).
Log: [WeaponVfx-Validation-Final.log](<D:/SurvivePrototype/Logs/WeaponVfx-Validation-Final.log>).
La pasada inicial de 228 tuvo 15 fallos de fixture (null de Unity, oclusión y
geometría de seguridad); la ejecución final ajusta ese helper, sin cambios de
código productivo posteriores al setup. **Las 22 capturas finales se revisaron**
sin shaders rosas, armas cruzadas ni defectos materiales detectados; los impactos
son visibles en el fixture aislado. **Compilación final sin helpers PASS,
exit code 0**, en [WeaponVfx-FinalCompile.log](<D:/SurvivePrototype/Logs/WeaponVfx-FinalCompile.log>).
No equivale a una sesión manual completa de Level 1 ni a una build distribuida.
Las cifras del 19/10 siguen siendo históricas.

## Limpieza autorizada de audio y VFX

AssetAudioVfxAudit.ScanOnly() escribe un inventario externo de sólo lectura,
con GUID, bytes, SHA-256 del asset/meta, referencias y motivo para conservar
o proponer cada candidato. No borra por ejecutar el setup o la auditoría.
Su universo abarca AudioClip, prefabs VFX dedicados y recursos exclusivos
de GunFX y de PolygonApocalypse/Prefabs/FX. Protege las referencias productivas serializadas, escenas de build,
escenas y demos supervivientes de proveedores, fuentes de regeneración del setup,
Resources, Addressables, assets precargados y recursos compartidos con contenido
ajeno a VFX. Los scripts y otras familias fuera de ese alcance no se podan.

PruneApproved() requiere un manifiesto externo de este proyecto con approvedPaths
explícitos. Verifica rutas absolutas dentro del proyecto y rechaza symlinks o
junctions en el asset, meta o sus ancestros, antes del respaldo y antes de borrar.
Recalcula el grafo y compara GUID, tamaño y hashes del asset/meta;
antes de la primera eliminación guarda y verifica una copia externa de todos
los archivos seleccionados. Usa nombres planos por GUID en files/, con backupAsset
y backupMeta en el manifiesto para relacionarlos con su ruta original y evitar
MAX_PATH. Repite la comprobación del grafo después del respaldo
y sólo elimina el subconjunto que siga cumpliendo esas condiciones. El resultado
y la ruta del respaldo quedan en el reporte; una poda parcial se informa como fallo.

La importación inicial de audio contiene 1.318 WAV y los ocho perfiles usan
102 variantes originales en total. Los WAV ajenos a referencias supervivientes
y los ocho disparos sintéticos antiguos son candidatos a revisión, no una lista
de borrado implícita. Los 17 prefabs fuente GunFX son necesarios para regenerar
los adaptadores y deben conservarse.

El escaneo final de candidatos pasó con cero bloqueos:
**1.273 candidatos, 663.008.689 bytes (aproximadamente 632,29 MiB)**.
Son 1.224 AudioClip (1.216 WAV Modern Weapons sin uso y ocho sintéticos
antiguos) más 49 recursos VFX dedicados (28 prefabs, 11 FBX, nueve materiales
y un PNG). El escaneo clasificó 121 AudioClip como retenidos: las 102 variantes
Modern Weapons y 19 sonidos de locomoción.
Log: [WeaponVfx-Audit.log](<D:/SurvivePrototype/Logs/WeaponVfx-Audit.log>).

**Poda ejecutada y VERIFICADA: 1.273 assets eliminados, cero fallos,
663.008.689 bytes (632,29 MiB) de assets/metas retirados**, exit code 0.
Se confirmó en disco la ausencia de todos los paths eliminados y sus metas;
Modern Weapons conserva 102 WAV. No es una medición del espacio de Library.
Reporte: [cleanup-result.json](<C:/Users/pc/.codex/visualizations/2026/10/19/01a153f2-ffb7-7f82-bfbd-04190b330ad4/vfx-evidence/cleanup-result.json>).
Log: [WeaponVfx-Cleanup-Final.log](<D:/SurvivePrototype/Logs/WeaponVfx-Cleanup-Final.log>).
El respaldo queda en
[effects-backup-20261024T164515351-5e8a8bf9/manifest.json](<C:/Users/pc/.codex/visualizations/2026/10/19/01a153f2-ffb7-7f82-bfbd-04190b330ad4/vfx-evidence/effects-backup-20261024T164515351-5e8a8bf9/manifest.json>),
con correspondencia de rutas, GUID y hashes. El runtime posterior tiene
evidencias separadas: audio 178/178 y VFX 229/229 PASS; la poda por sí sola
no demuestra funcionamiento en Play Mode.

La auditoría de integridad posterior verificó **2.546 archivos de respaldo sin
diferencias de hash**, las 102 grabaciones Modern Weapons sin cambios de bytes,
los 121 AudioClip retenidos sin diferencias y cero referencias serializadas
a GUID eliminados. Los 28 prefabs retirados son FX antiguos de Synty dentro
del alcance autorizado; el histórico de 30 prefabs fuente de armas intactos
del 19/10 no es una afirmación sobre todos los assets Synty de esta poda.
Reporte: [post-cleanup-integrity.json](<C:/Users/pc/.codex/visualizations/2026/10/19/01a153f2-ffb7-7f82-bfbd-04190b330ad4/vfx-evidence/post-cleanup-integrity.json>).
Después se retiraron 32 carpetas vacías exclusivamente de audio/VFX y sus metas,
sin sumar directorios al conteo de 1.273 assets. Manifest y lockfile de Packages
se restauraron byte a byte y se retiraron los dos helpers UPM externos.
El SHA-256 final de Level 1 coincide exactamente con el respaldo previo a VFX,
al igual que manifest.json y packages-lock.json.
**Compilación final sin helpers PASS, exit code 0**:
[WeaponVfx-FinalCompile.log](<D:/SurvivePrototype/Logs/WeaponVfx-FinalCompile.log>).
El escaneo posterior del alcance audio/VFX pasó con **cero candidatos,
275 recursos retenidos, cero bytes candidatos y cero bloqueos**:
[post-cleanup-audit.json](<C:/Users/pc/.codex/visualizations/2026/10/19/01a153f2-ffb7-7f82-bfbd-04190b330ad4/vfx-evidence/post-cleanup-audit.json>).

## Audio de recarga JU TPS del 24/10/2026

**IMPLEMENTADO / VERIFICADO en Play Mode controlado: 114/114 PASS, exit code 0.** La fuente local autorizada es
E:/Unity/JU_TPS_3_-_Third_Person_Shooter_GameKit_Vehicle_Physics_v3.3.9.unitypackage,
SHA-256 9358506c560bb03bacd8677b6d81281424945643351eedc062556fd5c0ae9bfb.
Se tomaron únicamente Reload.wav y Bolt Sliding.wav de
Assets/Julhiecio TPS Controller/Audio/Guns/Guns Sounds WAV Alternatives.
Los originales y sus metas permanecen sólo en la evidencia externa; no se
importaron scripts, prefabs ni el resto de audio de JU TPS.

Los cuatro WAV derivados están en Assets/Audio/Weapons/Reload con GUID nuevos.
Conservan PCM16 estéreo a 48 kHz, sin normalización, síntesis, resampling ni
cambio de afinación. Sólo se recortaron muestras y se aplicaron fades lineales
de 96 frames (2 ms) en cada extremo para evitar clicks.

| Corte | Fuente | Frames de origen, fin excluido | Duración |
| --- | --- | --- | ---: |
| JU_TPS_Reload_A | Reload.wav | 5.040–16.080 | 0,230 s |
| JU_TPS_Reload_B | Reload.wav | 16.800–26.750 | 0,207292 s |
| JU_TPS_Bolt_A | Bolt Sliding.wav | 3.120–9.840 | 0,140 s |
| JU_TPS_Bolt_B | Bolt Sliding.wav | 11.280–20.160 | 0,185 s |

Se reutilizan esos cuatro cortes en **ocho perfiles y 32 cues** con tiempos y
ganancias por preset para acompañar las piezas animadas. Los nombres A/B no
acreditan una acción concreta de cargador o tambor ni ocho grabaciones propias
de esos modelos. La escopeta conserva su palanca. No cambia la transferencia
completa de munición al terminar la recarga ni agrega carga por cartucho.
WeaponAudioSetup configura sólo estos cuatro clips como PCM DecompressOnLoad
con preload; los 102 WAV de disparo Modern Weapons conservan ADPCM.
El inventario de audio actual suma **125 clips: 102 de disparo, 19 de locomoción
y cuatro de recarga**. Las cifras de 121 retenidos de la auditoría anterior
describen el estado previo a esta ampliación.

Procedencia, GUID, SHA-256 y rangos exactos:
[JU-TPS-derived-report.json](<C:/Users/pc/.codex/visualizations/2026/10/19/01a153f2-ffb7-7f82-bfbd-04190b330ad4/reload-audio-evidence/JU-TPS-derived-report.json>).
La comprobación externa de muestras confirmó interior idéntico a la fuente,
extremos en cero y originales intactos. Los ocho WeaponDefinition conservan
todos los campos anteriores, excluidos los dos nuevos reloadAudioCues y
reloadVolume, según
[definition-preservation.json](<C:/Users/pc/.codex/visualizations/2026/10/19/01a153f2-ffb7-7f82-bfbd-04190b330ad4/reload-audio-evidence/definition-preservation.json>).

La ejecución final de recargas pasó **114/114**, con cero errores de runtime,
el 24/10/2026 entre 14:13:41 y 14:13:49 de Buenos Aires
(17:13:41–17:13:49 UTC). Usó una escena temporal aislada con el jugador,
Animator y ocho prefabs guardados reales; TryReload, selección, drop y salud
son los del gameplay. El fixture relaja sólo el bloqueo de cursor y no guarda
escenas. Comprobó cuatro fases configuradas por preset, pitch 1, transferencia
de munición una sola vez, animación activa, rechazo de recarga inválida,
cancelación/cambio/drop/disable/muerte, pausa/reanudación, fin con frame largo
y reanudación que completa en el mismo frame sin perder los cues finales.

AudioRenderer capturó **24 WAV y 2.087.936 frames de muestras** a 48 kHz estéreo.
Los ocho mixes de recarga natural alcanzaron picos 0,191926–0,237857, sin
muestras recortadas. El motor se grabó offline, sin salida a parlantes.
Reporte: [reload-audio-report.json](<C:/Users/pc/.codex/visualizations/2026/10/19/01a153f2-ffb7-7f82-bfbd-04190b330ad4/reload-audio-evidence/reload-audio-report.json>).
Log: [WeaponReloadAudio-Validation-Final.log](<D:/SurvivePrototype/Logs/WeaponReloadAudio-Validation-Final.log>).
El análisis independiente de los WAV capturados pasó **69/69**, exit code 0:
correlación FFT confirmó las 32 fases una sola vez, los ocho timings con desvío
máximo de 24,67 ms y 28 comparaciones de ganancia relativa entre perfiles.
La relación medida/esperada fue aproximadamente 0,999931–1,000014 al comparar
el mismo clip contra Pistol. Esa comparación contempla la respuesta del motor
por clip; no acredita igualdad de ganancia absoluta entre el PCM crudo y el
mix procesado por Unity.
Reporte: [reload-audio-waveform-analysis.json](<C:/Users/pc/.codex/visualizations/2026/10/19/01a153f2-ffb7-7f82-bfbd-04190b330ad4/reload-audio-evidence/reload-audio-waveform-analysis.json>).
**Compilación final sin helper PASS, exit code 0**:
[WeaponReloadAudio-FinalCompile.log](<D:/SurvivePrototype/Logs/WeaponReloadAudio-FinalCompile.log>).
El escaneo posterior pasó con **cero candidatos, cero bloqueos, 279 recursos
retenidos y 125 AudioClip**:
[post-reload-audio-audit.json](<C:/Users/pc/.codex/visualizations/2026/10/19/01a153f2-ffb7-7f82-bfbd-04190b330ad4/reload-audio-evidence/post-reload-audio-audit.json>).
Manifest y lockfile se restauraron byte a byte contra Packages-before-validation
y se eliminó el helper externo de validación con sus fuentes. Level 1 conserva
el SHA-256 capturado inmediatamente antes de esta suite; ese punto ya incluía
los cambios de NPC realizados por otra tarea y no se compara con el inicio
de la entrega de VFX. La validación de recarga no guardó escenas.
Las ejecuciones 178/178 de disparo y 229/229 VFX son anteriores a los
cues de recarga y se mantienen separadas. Escucha manual y build distribuida
siguen NO VERIFICADAS.

## Audio Modern Weapons del 24/10/2026

La importación tomó únicamente los **1.318 WAV originales**,
765.504.910 bytes (aproximadamente 730 MiB), sus metas y las 36 carpetas
antecesoras con sus metas. Conserva las rutas bajo
Assets/Modern Weapons ALL-IN-ONE Sound System y los GUID originales del paquete.
No incorpora sus scripts, demos, prefabs, escenas ni mixer; la reproducción
pertenece al sistema existente. El archivo fuente se llama
Modern_Weapons_ALL-IN-ONE_Sound_System_v1.2.unitypackage, aunque sus metadatos
internos declaran packageVersion 1.1; esa diferencia se registra sin renombrar
ni corregir el original.

WeaponAudioSetup asigna estos perfiles. Cada disparo combina una variante
elegida independientemente de cada familia OneShot, Stop y Tail; FN509 dispone de seis variantes
por familia y los otros perfiles de cuatro.

| Preset | Grabaciones del paquete | Variantes por capa | Ganancia OneShot / Stop / Tail |
| --- | --- | --- | --- |
| Pistol | FN 509, prefijo FN509 | 6 | 0,30 / 0,08 / 0,045 |
| Revolver | Desert Eagle, prefijo DesertEagle | 4 | 0,38 / 0,10 / 0,055 |
| SMG | SIG MPX, prefijo SIG_MPX | 4 | 0,24 / 0,065 / 0,025 |
| AssaultRifle | AK-74, prefijo AK74 | 4 | 0,30 / 0,075 / 0,03 |
| Shotgun | Kel-Tec KSG, prefijo Kel-Tec | 4 | 0,40 / 0,10 / 0,06 |
| HuntingRifle | M2010 ESR, prefijo M2010 | 4 | 0,36 / 0,09 / 0,035 |
| SniperRifle | CheyTac M200, prefijo CheyTac | 4 | 0,42 / 0,10 / 0,05 |
| MachineGun | M249, prefijo M249 | 4 | 0,28 / 0,07 / 0,025 |

Desert Eagle es una correspondencia tímbrica para Revolver: el paquete no
contiene un revólver real. M2010 cumple el mismo papel para HuntingRifle.
Kel-Tec KSG se usa sólo como correspondencia acústica para Shotgun; su modelo
Synty conserva la animación de palanca y no pasa a ser una escopeta de bombeo.
Los perfiles no cambian estadísticas, mecanismos, munición ni cadencia.

El setup configura sólo los clips asignados con CompressedInMemory, ADPCM,
preload y frecuencia original; no fuerza mono ni carga en background. Las
fuentes locales son 2D, sin loop ni playOnAwake. El setup no asigna toda la
biblioteca al jugador; la auditoría determina qué archivos sin referencias
supervivientes pueden retirarse dentro del alcance autorizado.

**Importación VERIFICADA:** 2.672 archivos nuevos comparados con sus bytes
originales, incluidos WAV y metas; sin colisiones de GUID existentes. El
importador externo no inició Unity. Reporte:
[audio_import_report.json](<C:/Users/pc/.codex/visualizations/2026/10/19/01a153f2-ffb7-7f82-bfbd-04190b330ad4/audio-evidence/audio_import_report.json>).
**Validación posterior a la poda: 178/178 PASS**, exit code 0, el 24/10/2026
entre 13:53:19 y 13:53:30 de Buenos Aires (16:53:19–16:53:30 UTC), con cero
errores de gameplay y cero errores de red del Editor. AudioRenderer produjo
17 WAV de salida real del motor a 48 kHz estéreo, incluidos calibración,
ráfagas de los ocho presets y comparaciones de volumen/pausa. Los picos de las
ocho mezclas quedaron entre 0,2475 y 0,4633, sin muestras recortadas.
El master al 25 % produjo una relación RMS de 0,25048; master en cero y pausa
produjeron silencio. La relación de escopeta con ocho perdigones frente a uno
fue 0,99808: no multiplica el mix por perdigón. Reanudar conservó la cola ya
emitida. Se verificaron referencias/ganancias, variantes sin repetición,
pitch natural, recarga silenciosa e idempotencia del setup sin recrear los
WAV sintéticos eliminados.

La prueba usó el jugador, inventario, Animator y feedback serializados de
Level 1, con enemigos deshabilitados, input sintético, foco/cursor adaptados
en memoria y las otras AudioSources silenciadas sólo durante la medición.
AudioRenderer trabajó offline después de UpdateAudio en frames reales del
PlayerLoop; no envió sonido a parlantes ni constituye escucha manual.
Una Definition temporal de escopeta con clips fijos permitió comparar
perdigones/volumen sin modificar assets. No se guardó la escena.
Reporte: [audio-report.json](<C:/Users/pc/.codex/visualizations/2026/10/19/01a153f2-ffb7-7f82-bfbd-04190b330ad4/audio-evidence/audio-report.json>).
Log: [WeaponAudio-Validation-Final.log](<D:/SurvivePrototype/Logs/WeaponAudio-Validation-Final.log>).
La ejecución anterior de 159 comprobaciones sin muestras no prueba salida
audible; esta evidencia corresponde a la ejecución final de 178 con muestras.
La compilación final sin helpers pasó con exit code 0, según
[WeaponVfx-FinalCompile.log](<D:/SurvivePrototype/Logs/WeaponVfx-FinalCompile.log>).
**Escucha manual y build distribuida NO VERIFICADAS.**
Las comprobaciones históricas siguientes no validan el nuevo audio.

## Evidencia histórica del 19/10/2026 y límites

**Entrega anterior, mecanismos e imágenes: 142/142 PASS en Play Mode
controlado, exit code 0.** El reporte del 19/10/2026 comenzó a las 10:02:46 y
terminó a las 10:02:51 de Buenos Aires (13:02:46–13:02:51 UTC), después de
corregir Shotgun como arma de palanca. Registró cero errores de gameplay y
cero errores de red del Editor. Se generaron **26 capturas**: 24 de las ocho
armas en reposo/disparo/recarga y dos de la hotbar con imágenes a
1920×1080 y 1280×720.

La ejecución nueva comprobó:

- ocho sprites reales 512 × 320 con alpha, margen, referencias Icon y seis
  imágenes serializadas; selección, slot vacío y recogida actualizan el sprite;
- piezas reales de los ocho wrappers, Fire/Reload vinculados y ausencia de
  curvas sobre el root del arma, Muzzle o LeftHandGrip;
- LMB mediante Input System que mueve las piezas, retorno exacto al reposo,
  cadencia sostenida sin drift y armas guardadas que permanecen inmóviles;
- recarga real con avance de mecanismos, capa Humanoid activa, pausa que
  congela piezas/munición y fin de recarga con reserva finita;
- cancelación explícita, cambio de slot, drop, roll, controles deshabilitados
  y muerte que restauran el reposo sin reponer munición;
- palanca de Shotgun con aproximadamente 52° de rotación y traslación menor
  a 1 mm durante disparo y recarga; no simula una corredera de bombeo;
- ausencia del placeholder de recarga, ocho ReloadSound vacíos y conservación
  de las referencias de disparo. No hubo una nueva revisión auditiva manual.

El setup guardó 18 clips locales y ocho sprites RGBA. Se revisaron la plancha
de ocho fotografías, la UI en ambas resoluciones y las 24 capturas de reposo,
disparo y recarga de los ocho presets, sin problemas materiales detectados.
Algunas capturas de disparo muestran una fase temprana; las mediciones por
frame del run verifican el movimiento máximo. Los 30 hashes de prefabs Synty
originales comparados se conservaron, al igual que el hash de Level 1.
Los primeros 131 resultados fueron una ejecución preliminar anterior a la
corrección de palanca; la evidencia final de esta ampliación es **142/142**.

El run usa el jugador, Animator, inventario y Canvas serializados de Level 1
en Play Mode de Editor, con enemigos/spawner deshabilitados en esa instancia.
Procesa input sintético en frames reales y adapta foco/cursor sólo para batch;
no usa PreviewPose para probar mecanismos. Los closeups aíslan el arma real
en una layer de cámara y ocultan temporalmente trazadores/fogonazo; no cambian
la pose mecánica. Las dos imágenes de UI usan el Canvas temporalmente en
ScreenSpaceCamera y un fondo sólido explícito: prueban layout, no una vista
completa de gameplay ni una sesión manual. Esos ajustes no se guardan en los assets.

Reporte:
[mechanism-report.json](<C:/Users/pc/.codex/visualizations/2026/10/19/01a153f2-ffb7-7f82-bfbd-04190b330ad4/mechanism-evidence/mechanism-report.json>).
**Compilación final sin helper: PASS, exit code 0**, registrada en
[WeaponPresentation-FinalCompile.log](<D:/SurvivePrototype/Logs/WeaponPresentation-FinalCompile.log>).
El helper externo fue retirado y manifest/lockfile quedaron restaurados con
hashes exactos y sin diferencias de Git. Los 66/66 y 169/169 siguientes
pertenecen a entregas anteriores y no se suman como prueba de los mecanismos
o de la hotbar con imágenes. Una sesión manual completa y una build distribuida
siguen **NO VERIFICADAS**.

**Entrega anterior de inventario: 66/66 PASS en Play Mode controlado, exit code 0.** El
reporte nuevo comenzó el 19/10/2026 a las 09:43:40 y terminó a las 09:43:42
de Buenos Aires (12:43:40–12:43:42 UTC). Registró cero excepciones de gameplay
y cero errores de red del Editor; generó cuatro capturas de la UI.

La ejecución verificó:

- seis slots serializados, partida/reintento vacíos y Canvas existente compartido;
- E real de Input System para primer pickup y reemplazo con inventario lleno,
  incluyendo los avisos de guardar/reemplazar y devolución del arma anterior;
- selección 1–6, rueda con delta de Windows, ciclo D-pad Left/Right, huecos y
  wrap, conservación de instancias/munición y ausencia de colisión de armas ocultas;
- recarga manual/automática con animación nativa silenciosa, LMB sostenido y
  cambio de arma durante Fire; cancelación de recarga sin crear balas;
- G sólo sobre el seleccionado, recogida posterior y limpieza de referencias
  cuando se destruye externamente un arma guardada;
- bloqueo por pausa, cursor, roll, componentes deshabilitados y muerte, y
  reinicio mediante el flujo real de MenuBehaviour;
- ocho ReloadSound vacíos, retiro del WAV/meta provisional y conservación de
  las ocho referencias de disparo. El soporte de audio real opcional permanece
  en el código; no se atribuye a estas pruebas una revisión auditiva.

Las cuatro imágenes muestran aviso de E/inventario vacío, seis slots a
1920×1080 y 1280×720 y munición conservada. Se revisaron legibilidad, selección
y ausencia de superposición con los HUD de salud/arma. La captura convierte
temporalmente el Canvas a ScreenSpaceCamera sobre una cámara/RenderTexture
dedicada; su fondo azul es el clear de esa cámara. **Son evidencia de layout,
textos y estados de UI; no prueban el fondo de gameplay ni una sesión manual.**
El run usa frames de Unity e input sintético, con ajustes temporales de foco,
cursor y cámara de interacción. Level 1 conserva SHA256 idéntico al baseline;
no se guardaron cambios de escena.

Reporte:
[hotbar-report.json](<C:/Users/pc/.codex/visualizations/2026/10/19/01a153f2-ffb7-7f82-bfbd-04190b330ad4/hotbar-evidence/hotbar-report.json>).
**Compilación final VERIFICADA:** se retiró el paquete temporal hotbar-validation,
manifest y lockfile se restauraron exactamente contra sus backups y Unity
terminó **PASS, exit code 0** sin el helper externo en
[Hotbar-FinalCompile.log](<D:/SurvivePrototype/Logs/Hotbar-FinalCompile.log>). El SHA256 de Level 1
sigue idéntico al baseline. Sesión manual completa, escucha manual y build
distribuida continúan **NO VERIFICADAS**.
Las cifras históricas siguientes no se suman ni se presentan como cobertura
del inventario.

**Evidencia histórica previa al inventario: agarres, disparo sostenido y
animación real, 169/169 PASS en
Play Mode controlado**, Unity 6000.3.5f2, reporte del 19/10/2026 a las
09:22:23 de Buenos Aires (12:22:23 UTC). El reporte registró **44 capturas**,
**0 errores de gameplay** y **1 error de conexión de red del Editor**.
La excepción SocketException/WebException ConnectFailure fue un timeout en
un stack asíncrono; su origen no quedó demostrado y se preservó aparte.
No se afirma que el Editor haya registrado cero errores.

La cobertura de esa entrega incluyó:

- LMB sostenido en los ocho presets, agotamiento, cadencia, recarga automática,
  avance del clip real de 2 segundos, reanudación tras la salida y recuperación del retroceso;
- apoyo de muñecas prácticamente coincidente y cañón alineado con errores
  angulares de 0,0198° a 0,0593° en las mediciones de aim de los ocho presets;
- daño exacto a EnemyHealth desde collider hijo y bloqueo de cañón aunque la cámara vea al objetivo;
- entrada R, permisos de aim y movimiento real UPP con D, crouch C y prone Z,
  conservando daño y agarre en el cuadro evaluado;
- intercambio, soltar, roll, deshabilitación y muerte que cancelan la recarga;
  pausa, cargador/reserva finitos y agotamiento definitivo.

Se generaron 44 capturas; se inspeccionaron las vistas frontal/lateral de aim
de las ocho armas y momentos de recarga seleccionados, sin problemas visibles
de materiales. La captura controlada de prone quedó fuera de encuadre por la
cámara de prueba fijada a altura de pecho: no constituye evidencia visual de
esa postura. Sus comprobaciones numéricas de pose y daño sí pasaron.
Fue una prueba programática con frames reales de Unity y entradas sintéticas
de New Input System. En la instancia temporal se deshabilitaron enemigos,
se adaptaron foco de input y requisitos de cursor de OS para batch, se acercó
al jugador 0,5 m al pickup existente y se usó un rango aislado en (1000, 0, 1000)
para medir daño; esas adaptaciones no se guardaron en la escena. El caso de
agotamiento usó una reserva temporal de tres balas para acotar la prueba.

Reporte y ajustes completos:
[report.json](<C:/Users/pc/.codex/visualizations/2026/10/19/01a153f2-ffb7-7f82-bfbd-04190b330ad4/weapon-feel-results/report.json>).
**Compilación final VERIFICADA:** después de retirar el paquete temporal,
Unity terminó **PASS, exit code 0** en
[WeaponFeel-FinalCompile.log](<D:/SurvivePrototype/Logs/WeaponFeel-FinalCompile.log>).
Manifest y lockfile se restauraron con SHA256 idéntico a sus backups; el
paquete externo de validación fue retirado. El SHA256 de Level 1 coincide
exactamente con el anterior a esta ampliación de presentación. No se verificó
una sesión manual completa, escucha manual del audio ni una build distribuida.

La evidencia siguiente pertenece al sistema anterior a esta ampliación.

**IMPLEMENTADO / INTEGRADO / VERIFICADO:** Unity 6000.3.5f2 compiló sin errores;
el setup generó ocho WeaponDefinition, ocho prefabs y el HUD con referencias
serializadas. Level 1 contiene exactamente tres pickups nuevos, Pistol,
Shotgun y AssaultRifle, bajo WeaponSystem_Presets. El delta contra el backup
anterior a la tarea fue de **231 líneas agregadas y 0 eliminadas**.

La entrega inicial pasó **17/17 grupos controlados de Editor y Play Mode**, con cobertura de:

- ocho presets, referencias serializadas y los tres pickups exactos de Level 1;
- aislamiento de munición entre armas y reinicio al crear una instancia nueva;
- recarga parcial, cancelación y rechazo de deltaTime inválido;
- intercambio, soltar, conservación de munición y condiciones de bloqueo;
- daño a EnemyHealth desde colliders hijos, paredes y filtrado del jugador;
- una bala por disparo de escopeta, bloqueo por el enemigo que muere y ocho trazadores;
- input real de New Input System para Fire, R y G, incluida una pared frente al lugar de caída;
- E para recoger y cambiar, alcance, paredes y rechazo durante pausa;
- pausa que congela la recarga y muerte que la cancela.

También se generaron e inspeccionaron dos capturas de aim renderizadas en un
entorno controlado, con rifle y pistola. Esto no sustituye una sesión manual de
Level 1. El paquete temporal de pruebas era externo; se retiró y se restauraron
manifest y lockfile exactamente. La compilación final sin ese paquete terminó
**PASS, exit code 0**. No se guardaron suites ni scripts de prueba en el proyecto.

**NO VERIFICADO en esta ampliación:** recorrido manual completo en Level 1,
revisión auditiva manual, build/package y comportamiento de una entrega
distribuida. La compilación final sin el paquete temporal pasó con exit code 0.
**NO IMPLEMENTADO:** coreografías Humanoid propias de manos para cada mecanismo,
carga de cartuchos uno a uno con efecto en munición, vainas como cuerpos rígidos
de gameplay, guardado del inventario,
stacking, melee, proyectiles físicos o penetración. Los ocho valores de balance continúan
siendo iniciales de prototipo; audio y recarga compartida tienen los límites
de presentación descritos arriba.

Lista para repetir la validación al cambiar assets o comportamiento:

1. Compilar Unity y ejecutar el setup; revisar los ocho datos y prefabs sin
   cambios sobre los assets originales del paquete.
2. Revisar referencias de WeaponSocket, Definition, Muzzle, materiales y HUD.
3. Integrar los tres pickups de Level 1 y revisar ubicación, piso y alcance.
4. Probar E desarmado y equipado, aviso, distancia y rechazo detrás de paredes.
5. Mantener LMB con cada uno de los ocho presets: revisar cadencia, alcance,
   cargador vacío y ausencia de disparos extra. El modo semiautomático sigue
   disponible en la API, pero ningún preset de esta entrega lo usa.
6. Probar recarga automática y R con cargador parcial/lleno y reserva
   suficiente/insuficiente/vacía; comprobar clip real, salida y reanudación de LMB.
7. Pausar durante recarga; comprobar que continúe después de reanudar. Cambiar,
   soltar, morir y hacer roll deben cancelar sin crear balas.
8. Volver a recoger armas usadas y confirmar conservación del cargador y reserva.
9. Probar escopeta, múltiples trazadores, daño por perdigón y obstáculos cercanos
   al cañón; el enemigo que muere sigue bloqueando los perdigones de ese tiro.
10. Revisar HUD, ambos agarres, dedos, retroceso, audio, materiales y cambios
    de postura en la escena de entrega; si se genera una build, repetir allí.
11. Revisar Fire/Reload de las piezas de los ocho presets y del rifle histórico,
    una sola presentación por tiro de escopeta y retorno a reposo sin drift.
    Pausar, cancelar, guardar, soltar y morir deben respetar el estado real del arma.
12. Revisar las ocho fotos transparentes, sus referencias Icon y las seis imágenes
    de la hotbar con slots vacíos, selección, reemplazo y resoluciones de entrega.
13. Revisar los ocho perfiles de audio, tres capas por tiro físico, variantes sin
    repetición consecutiva, pitch original, pausa/reanudación y los cuatro cues
    de recarga por preset, sin duplicaciones ni fases pendientes tras cancelar.
    Comparar niveles en ráfagas sostenidas y confirmar que el azar de audio no
    altera dispersión ni munición.
14. Revisar fogonazos/vainas de los ocho perfiles, seis superficies, normales
    reales y ausencia de sangre duplicada; comprobar tiempos de cerrojo/palanca,
    recarga de revólver cancelada/reanudada, pausa y límite del pool.
15. Revisar el manifiesto de limpieza contra sus dependencias, verificar el
    respaldo externo y repetir compilación/referencias/runtime después de la poda.

La evidencia del **11/09/2026** corresponde a la integración histórica de
recogida por contacto: diez grupos reportaron PASS en Editor/Play Mode sobre
herencia, flotación, attachment, rechazo de segunda arma, disparo y obstáculos.
Ese contrato fue reemplazado por E e intercambio. No valida munición, recarga,
presets ni HUD nuevos y no equivale a una sesión manual o build empaquetada.

Las pruebas temporales viven fuera de Assets y del repositorio, según
[Base TPS UPP](BASE_TPS.md). Una compilación correcta no demuestra la calidad
del agarre, las animaciones, los materiales ni la experiencia del mapa.
