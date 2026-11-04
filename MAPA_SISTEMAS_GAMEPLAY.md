# Archivos de gameplay y UI

Rutas relativas a `D:\SurvivePrototype`. Estado: 3 de noviembre de 2026.

| Sistema | Archivo principal | Qué modificar ahí |
| --- | --- | --- |
| Inventario y hotbar | [PlayerWeaponInventory.cs](Assets/Scripts/Combat/PlayerWeaponInventory.cs) | Seis espacios compartidos por armas y pilas de curación, selección con 1–6/rueda, clic para usar consumible, G para soltar, compras y conservación de munición. |
| Equipar, disparar y recargar | [PlayerWeaponController.cs](Assets/Scripts/Combat/PlayerWeaponController.cs) | Controles y comportamiento del arma equipada. |
| Estado de cada arma | [WeaponItem.cs](Assets/Scripts/Items/WeaponItem.cs) | Munición actual, recarga, propietario y arma recogible en el mundo. |
| Datos de armas | [WeaponDefinition.cs](Assets/Scripts/Items/WeaponDefinition.cs) + [Assets/Data/Weapons](Assets/Data/Weapons) | Los ocho `WD_*.asset` contienen estadísticas e **Icon**, compartido por HUD, inventario y vendedor. |
| Interacción | [PlayerInteractionController.cs](Assets/Scripts/Combat/PlayerInteractionController.cs) + [IWorldInteractable.cs](Assets/Scripts/Items/IWorldInteractable.cs) | Detección, texto de interacción y acción con E. |
| HUD de gameplay Synty | [SyntyGameplayHUD.cs](Assets/Scripts/UI/SyntyGameplayHUD.cs) | Conecta y actualiza vida, monedas, torretas, llave, arma/munición, inventario, oleadas, jefe, tiempo y puntaje. |
| Diseño del HUD | [SyntyGameplayHUD.prefab](Assets/Prefabs/UI/SyntyGameplayHUD.prefab) | Posiciones, tamaños, imágenes y textos; está integrado en `Level 2` y `Level 2 UI`. |
| Generación del HUD | [SyntyGameplayHudBuilder.cs](Assets/Editor/SyntyGameplayHudBuilder.cs) + [SyntyGameplayHudSetup.cs](Assets/Editor/SyntyGameplayHudSetup.cs) | Construye el prefab e integra las escenas. Regenerar sobrescribe el diseño generado. |
| Lista del vendedor y precios | [VC_Armero.asset](Assets/Data/Vendor/VC_Armero.asset) | Array **Offers** en el Inspector: prefab del arma, definición de torreta o cargador explosivo, precio y orden. Diez productos. |
| Estructura del catálogo | [VendorCatalog.cs](Assets/Scripts/Economy/VendorCatalog.cs) | `VendorCatalog` / `VendorOffer`: nombre, icono y descripción derivados de los datos del producto. |
| Compras y apertura/cierre | [PlayerVendorController.cs](Assets/Scripts/Economy/PlayerVendorController.cs) | Validación de alcance, fondos y capacidad; cobro, entrega/devolución, cursor y bloqueo del movimiento. |
| Diseño del vendedor Synty | [PlayerVendorView.cs](Assets/Scripts/Economy/PlayerVendorView.cs) | Tarjetas, distribución, botones, precio, saldo, contador de inventario y estados de compra. Es la parte visual de la misma clase `PlayerVendorController`. Se genera al ejecutar. |
| Tema visual del vendedor | [SyntyVendorTheme.asset](Assets/Resources/UI/SyntyVendorTheme.asset) + [VendorUITheme.cs](Assets/Scripts/UI/VendorUITheme.cs) | Referencias a sprites/font Synty y colores. El asset se carga desde Resources/UI. |
| NPC vendedor | [NpcVendor.cs](Assets/Scripts/Economy/NpcVendor.cs) + [NpcVendor.prefab](Assets/Prefabs/Vendor/NpcVendor.prefab) | Nombre, catálogo asignado, distancia de comercio e interacción con E. |
| Monedas del jugador | [PlayerWallet.cs](Assets/Scripts/Economy/PlayerWallet.cs) | Saldo, sumar, gastar y notificar cambios. |
| Monedas en el mundo | [CoinPickup.cs](Assets/Scripts/CoinPickup.cs) | Recogida y entrega al wallet; modelos/materiales en `Assets/Prefabs/coin*.prefab`. |
| Reserva y colocación de torretas | [PlayerTurretController.cs](Assets/Scripts/Turrets/PlayerTurretController.cs) | Kits, capacidad, colocar con T y orientación Q/E. |
| Datos de torreta | [TD_AutoTurret.asset](Assets/Data/Vendor/TD_AutoTurret.asset) + [TurretDefinition.cs](Assets/Scripts/Turrets/TurretDefinition.cs) | Icono, estadísticas y prefab desplegado. |
| Torretas activas | [AutoTurret.cs](Assets/Scripts/Turrets/AutoTurret.cs) | Detección de objetivos, giro horizontal, elevación y disparos alternados entre las dos bocas. |
| Modelo de torreta Steampunk | [SteampunkTurret.fbx](Assets/Models/Turrets/Steampunk/SteampunkTurret.fbx) + [SteampunkTurret.mat](Assets/Materials/Vendor/SteampunkTurret.mat) | Modelo y textura proporcionados desde Desktop/steampunk gun turret 3d model. Material URP. El FBX original también se usa en miniatura en el pickup. |
| Adaptación del modelo de torreta | [SteampunkTurretModelSetup.cs](Assets/Editor/SteampunkTurretModelSetup.cs) + [Rig](Assets/Models/Turrets/Steampunk/Rig) | Deriva tres mallas conservando UV/normales: base fija, mecanismo de giro y cañones con elevación. Conserva el prefab y su referencia en la definición; regenera el icono del mercader. |
| Vida del jugador | [PlayerHealth.cs](Assets/Scripts/PlayerHealth.cs) | Daño, curación, muerte y evento que actualiza el HUD. |
| Curación recogible | [HealingPickup.cs](Assets/Scripts/Items/HealingPickup.cs) + [BandagePickup.prefab](Assets/Prefabs/Consumables/BandagePickup.prefab) | Objeto 3D que flota y rota usando MasterItem; E lo guarda en la hotbar. No hay recogida por contacto. |
| Modelo 3D de las vendas | [MedicalBandage.fbx](Assets/Models/Consumables/MedicalBandage/MedicalBandage.fbx) + [MedicalBandage.mat](Assets/Materials/Consumables/MedicalBandage.mat) | FBX proporcionado desde Desktop/medical bandage 3d model. Material URP con color, normal y metallic/smoothness empaquetado a partir de roughness. Reemplaza el modelo anterior de primitivas. |
| Datos de curación | [HD_Bandages.asset](Assets/Data/Consumables/HD_Bandages.asset) + [HealingDefinition.cs](Assets/Scripts/Items/HealingDefinition.cs) | Icono Synty, 35 de vida por uso y máximo 5 por pila. El prefab conserva los materiales del modelo 3D. |
| Cargador explosivo | [AD_ExplosiveMagazine.asset](Assets/Data/Vendor/AD_ExplosiveMagazine.asset) + [ExplosiveAmmoDefinition.cs](Assets/Scripts/Items/ExplosiveAmmoDefinition.cs) | Radio 2,5 m, daño máximo 180 e impulso 6. Su precio de 90 monedas se configura en el catálogo. |
| Munición especial del arma | [WeaponItem.cs](Assets/Scripts/Items/WeaponItem.cs) | Carga un cargador explosivo completo en el arma equipada. Devuelve las balas normales sobrantes a la reserva; conserva el estado al cambiar de arma. Al agotarlo permite recargar munición normal. |
| Daño de explosión | [ExplosiveRoundImpact.cs](Assets/Scripts/Combat/ExplosiveRoundImpact.cs) | Una explosión por bala, daño de área con caída por distancia y bloqueo por paredes; no multiplica la explosión por los perdigones de escopeta. |
| Mini explosión de balas | [GunslingerMiniBulletExplosion.prefab](Assets/Prefabs/VFX/GunslingerMiniBulletExplosion.prefab) + [AD_ExplosiveMagazine.asset](Assets/Data/Vendor/AD_ExplosiveMagazine.asset) | Variante de `Bullet explosion` de Realistic Gunslinger effects 1.0.1. Campos **Impact Effect Prefab**, **Impact Effect Scale** (0,3) y **Impact Effect Lifetime** (2,5 s). Aparece en el impacto, orientada por la normal de superficie. |
| Reutilización de efectos | [WeaponVfxPool.cs](Assets/Scripts/Combat/WeaponVfxPool.cs) | Reutiliza impactos, bocas y casquillos por escena; limita el número de instancias y respeta la pausa. La mini explosión queda en el punto del golpe, sin seguir al zombi ni al arma. |
| Presentación de explosión de reserva | [ExplosiveImpactEffect.cs](Assets/Scripts/Combat/ExplosiveImpactEffect.cs) + [ExplosiveFlash.mat](Assets/Resources/Gameplay/ExplosiveFlash.mat) | Respaldo procedural si una definición de munición no tiene prefab visual asignado. |
| Muerte por explosión | [EnemyHealth.cs](Assets/Scripts/EnemyHealth.cs) + [EnemyRagdoll.cs](Assets/Scripts/Combat/EnemyRagdoll.cs) | Muerte y estadística una sola vez; cuerpos y articulaciones del esqueleto humanoide, sin Animator/NavMesh mientras simula. La muerte normal mantiene su animación. |
| Apuntado hold | [UPPPlayer.prefab](Assets/Prefabs/Player/UPPPlayer.prefab) + [UPPCharacterMovementComponent.cs](Assets/Scripts/Player/Character/UPPCharacterMovementComponent.cs) | Campo **AimMode = HoldToAim**. Mantener RMB / gatillo izquierdo para apuntar, soltar para terminar. |
| Oleadas | [EnemySpawner.cs](Assets/Scripts/EnemySpawner.cs) | Oleadas, enemigos y descansos. |
| Aparición del boss | [BossSpawner.cs](Assets/Scripts/BossSpawner.cs) | Aparición del jefe al completar las oleadas. |
| Lógica del boss y loot | [BossBase.cs](Assets/Scripts/BossBase.cs) | Clase `BossBase`: comportamiento y referencias del loot. |
| Llaves en el mundo | [KeyPickup.cs](Assets/Scripts/KeyPickup.cs) | E recoge una llave y la agrega al inventario del jugador; no hay recogida automática ni flag global compartido. |
| Llaves del jugador | [PlayerKeyInventory.cs](Assets/Scripts/Items/PlayerKeyInventory.cs) | Cuenta llaves acumuladas por jugador, agrega y consume una unidad; bloquea uso durante pausa o muerte. El HUD muestra la cantidad. |
| Puertas | [PuertaConLlave.cs](Assets/Scripts/PuertaConLlave.cs) + [DoorBarrierInteraction.cs](Assets/Scripts/Items/DoorBarrierInteraction.cs) | Prerrequisitos, apertura con E y puertas que comparten una sola apertura/llave. `blockingObjects` mueve las barreras físicas junto con la puerta, `prerequisiteDoors` exige puertas previas y `linkedDoors` enlaza las que se abren juntas. |
| Orden de zonas en Level 2 | [DoorKeyProgressionSetup.cs](Assets/Editor/DoorKeyProgressionSetup.cs) | Configura Zona2 → Zona3 → par Zona4/Zona4.1 → Zona5. Las referencias quedan guardadas en la escena. |
| Puntaje y tiempo | [GameStatsManager.cs](Assets/Scripts/GameStatsManager.cs) | Estadísticas que consume el HUD. La versión en `Leaderboard/` es una subclase de compatibilidad. |
| Creador de personajes | [CharacterCreationMenu.cs](Assets/Scripts/Character/CharacterCreationMenu.cs) | Menú, opciones y aplicación de la personalización. |
| Personaje visible en SceneAlejandro | [SceneCharacterPreview.cs](Assets/Scripts/Character/SceneCharacterPreview.cs) | Preview 3D del personaje seleccionado. |
| Botón Agente y cámaras | [MenuBehaviour.cs](Assets/Scripts/MenuBehaviour.cs) + [CameraSwitch.cs](Assets/Scripts/CameraSwitch.cs) | Entrada al creador y navegación/cambio de cámara. |

## Iconos de las ocho armas

Se asignan en el campo **Icon** de cada `WeaponDefinition`. No hay una segunda tabla de iconos en el HUD: el arma equipada, los seis espacios y las ofertas del vendedor leen ese mismo campo.

Origen: `Assets/Synty/InterfaceMilitaryCombatHUD/Sprites/Icons_Weapons/`.

| Arma / asset | Sprite Synty a color |
| --- | --- |
| `WD_Pistol.asset` | `ICON_SM_Wep_Pistol_01_Military` |
| `WD_Revolver.asset` | `ICON_SM_Wep_Pistol_Revolver_01_BattleRoyale` |
| `WD_SMG.asset` | `ICON_SM_Wep_SubMGun_Lite_01_BattleRoyale` |
| `WD_AssaultRifle.asset` | `ICON_SM_Wep_Preset_A_Rifle_02_Military` |
| `WD_MachineGun.asset` | `ICON_SM_Wep_MachineGun_USA_01_Military` |
| `WD_Shotgun.asset` | `ICON_SM_Wep_Shotgun_01_Military` |
| `WD_HuntingRifle.asset` | `ICON_SM_Wep_Rifle_01_Military` |
| `WD_SniperRifle.asset` | `ICON_SM_Wep_Sniper_01_Military` |

[SyntyWeaponIconSetup.cs](Assets/Editor/SyntyWeaponIconSetup.cs) contiene la asignación inicial y la configuración del tema. Menú Unity: **Survive > UI > Asignar iconos a color y tema Synty del vendedor**. Volver a ejecutarlo restaura estas asignaciones. El generador anterior `WeaponIconSetup.cs` genera fotografías de armas y conserva los iconos externos ya asignados, como los de Synty.

## Presentación anterior y capturas

`PlayerWeaponHUD.cs`, `PlayerWeaponHotbar.cs`, `PlayerHealthUI.cs` y `WaveHUD.cs` son las vistas anteriores. En las dos escenas Level 2, la presentación actual usa `SyntyGameplayHUD`.

La tienda Synty usa el controlador existente del jugador, por lo que su diseño se aplica a los NPC vendedores que abren ese controlador. Catálogo y precios siguen en `VC_Armero.asset`.

Las vendas se seleccionan con 1–6 o rueda y se usan con clic izquierdo / gatillo derecho. Con vida completa, pausa, muerte o comercio abierto no se consumen. Los seis espacios cuentan también las pilas de vendas. El cargador explosivo exige un arma equipada y no permite comprar otro mientras queden balas especiales en ese cargador. El estado se indica como **MUNICIÓN EXPLOSIVA** y **EXP** en la hotbar. Para ver el décimo producto del mercader, desplazarse con la rueda o la barra vertical.

[HealingExplosiveSetup.cs](Assets/Editor/HealingExplosiveSetup.cs) genera los assets propios y coloca tres vendas cerca del inicio de cada escena Level 2; no modifica los assets Synty originales. [HealingExplosiveValidation.cs](Assets/Editor/HealingExplosiveValidation.cs) ejecuta la validación integrada. Evidencia: [Logs/HealingExplosive](Logs/HealingExplosive).

[MedicalBandageModelSetup.cs](Assets/Editor/MedicalBandageModelSetup.cs) importa el modelo médico, prepara el material y lo centra a 42 cm en el mismo prefab de recogida. El generador de gameplay también usa este FBX; ya no construye la venda anterior con primitivas. Se conserva el icono Synty de la hotbar.

La torreta Steampunk mide 1,4 m de alto y reserva un radio de 1,2 m para colocarla, incluyendo el barrido de los cañones. Conserva el precio de 150 monedas, daño 20, cadencia de 4 disparos/s y alcance de 15 m. El suelo queda bajo el centro real de su base. [SteampunkTurretValidation.cs](Assets/Editor/SteampunkTurretValidation.cs) comprueba compra, colocación, rig y combate con los prefabs reales; evidencia en [Logs/SteampunkTurret](Logs/SteampunkTurret).

[GunslingerExplosiveSetup.cs](Assets/Editor/GunslingerExplosiveSetup.cs) prepara y asigna la mini explosión. El paquete completo está en `Assets/Hovl Studio/Realistic Gunslinger effects` y sus recursos compartidos en `Assets/Hovl Studio/HSFiles`. La variante del juego usa las partículas y materiales del paquete; su vida útil la administra el pool. Evidencia de importación, muestras visuales y prueba del disparo explosivo en [Logs/GunslingerExplosive](Logs/GunslingerExplosive).

## Progresión de llaves y puertas en Level 2

| Apertura | Requisito | Llaves consumidas |
| --- | --- | --- |
| `PuertaZona2` | Tener una llave | 1 |
| `PuertaZona3` | `PuertaZona2` abierta y otra llave | 1 |
| `PuertaZona4` o `PuertaZona4.1` | `PuertaZona3` abierta y otra llave | 1 en total: ambas se abren juntas |
| `PuertaZona5` | Ambas `PuertaZona4` abiertas y otra llave | 1 |

Todas las aperturas requieren E. Intentar una puerta fuera de orden no gasta llaves y el aviso indica cuál falta abrir. Se pueden acumular varias llaves, pero no saltear etapas. Las puertas abiertas no vuelven a consumir. Una instancia nueva de jugador comienza sin llaves; las puertas de una escena nueva comienzan cerradas.

[DoorKeyProgressionValidation.cs](Assets/Editor/DoorKeyProgressionValidation.cs) prueba la configuración real de las cinco puertas y sus barreras en una escena aislada: 45 comprobaciones correctas, incluyendo E, consumo, ambos sentidos del par Zona4, varias llaves, eventos repetidos, pausa y muerte. Evidencia en [Logs/DoorProgression](Logs/DoorProgression).

Capturas y resultado de la validación: [Logs/SyntyVendorUI](Logs/SyntyVendorUI). Para regenerarlos: **Survive > UI > Validar vendedor Synty y mostrar todos los iconos**. La validación usa una escena aislada y los prefabs reales de jugador/vendedor/HUD.

## Recompensas por bajas y boss de Level 2

- `EnemyHealth.cs`: cada zombi entrega **5 monedas** al autor del golpe letal; el boss usa **150 monedas** directas. Sólo una recompensa por muerte. El daño sin autor (por ejemplo, comandos de depuración) no acredita dinero. `PlayerWallet.Changed` actualiza el HUD existente.
- `PlayerRewardFeedback.cs`: sonido 2D de recompensa en el jugador, sin interrumpir los sonidos del arma. Clip: `Assets/Audio/Rewards/ZombieKillReward.wav`. Los cuatro prefabs de zombis y el boss lo tienen asignado en `EnemyHealth.rewardSound`.
- `PlayerWeaponController.cs`, `ExplosiveRoundImpact.cs` y `AutoTurret.cs`: transmiten el autor del daño; `PlayerTurretController.cs` asigna al dueño cuando coloca la torreta.
- `Assets/Prefabs/Enemies/Boss_Zombie.prefab`: modelo Synty ApocalypseZombie_05, altura 3 m, materiales y animaciones humanoides. Vida base 700, multiplicada por la configuración de cada oleada. NavMeshAgent tipo **Boss**, radio 0,8 m, altura 3 m y baseOffset 1,5 m.
- `BossBase.cs`: IA, animación, furia, esbirros y loot. Se renombró el archivo anterior para que coincida con la clase `BossBase`, conservando su GUID. Los esbirros usan su propio tipo de NavMesh; el cadáver conserva la animación/ragdoll que administra `EnemyHealth`.
- `BossSpawner.cs`: referencia al nuevo prefab guardada en Level 2. Busca NavMesh del tipo correcto, comprueba la vinculación, aplica multiplicadores de vida/daño/velocidad y evita un segundo boss simultáneo. Si falla la aparición, `EnemySpawner.cs` termina con un error explícito en lugar de esperar indefinidamente.
- Las tres superficies activas de Level 2 usan sus datos válidos existentes (Enemy 2, Ranged 1, Boss 2); se comprobó ruta completa desde BossSpawnPoint al jugador. No fue necesario volver a hornearlas. Hay assets de navegación antiguos sin uso que no cargan, además de un error previo al cargar `New Terrain 3.asset`; se conservaron.
- Configuración y validación: `ZombieRewardBossSetup.cs`, `ZombieRewardBossValidation.cs`; resultados en `Logs/ZombieRewardBoss/validation.json`, ruta de navegación en `level-navigation.txt` y render del modelo en `boss-model.png`.

Validación final: **30/30**, sin errores durante las comprobaciones en Play Mode. El boss apareció mediante las oleadas reales, se movió por su NavMesh y su derrota inició el descanso. La captura del modelo se revisó visualmente. Sin build distribuido ni escucha manual del sonido.

## Dónde cambiar monedas, precios y velocidad

Abrí **Survive > Gameplay > Balance de enemigos y vendedor** fuera de Play Mode. La ventana edita los assets reales; no mantiene copias de sus valores. Las propiedades de prefabs y catálogo se guardan al cambiarlas, y la progresión del spawner se guarda con la escena (Ctrl+S).

| Ajuste | Ubicación original en el Inspector |
| --- | --- |
| Monedas de cada zombi (5 por defecto) | `Assets/Players/EnemyChasing.prefab`, `EnemyChasingRanged.prefab`, `EnemyPatrol.prefab` o `EnemyAttack.prefab` > **EnemyHealth > Recompensa por baja > Monedas por baja** (`killCoins`). |
| Monedas directas del boss (150 por defecto) | `Assets/Prefabs/Enemies/Boss_Zombie.prefab` > **BossBase > Direct Gold Reward**. Las monedas físicas del loot se controlan con **Min Coins / Max Coins**. |
| Precio de cada oferta | `Assets/Data/Vendor/VC_Armero.asset` > **Offers > oferta > Price**. Afecta al HUD y al cobro del mercader. |
| Velocidad inicial de cada zombi | Los mismos prefabs > controlador de enemigo > **Velocidad base (m/s)** (`moveSpeed`). |
| Velocidad inicial del boss | `Boss_Zombie.prefab` > **NavMeshAgent > Speed**. |
| Multiplicador inicial de la oleada | Level 2 > **EnemySpawnerManager > EnemySpawner > Base Wave Templates > Speed Multiplier**. Actualmente 0.3: un zombi con base 3 m/s comienza a 0.9 m/s. |
| Incremento en cada oleada | Level 2 > **EnemySpawnerManager > EnemySpawner > Velocidad por oleada > Speed Increase Per Wave**. Por defecto **0.04 = 4 % de la velocidad inicial por oleada**. |

Fórmula: `velocidad = velocidad base del prefab × Speed Multiplier de la plantilla × (1 + índice de oleada × Speed Increase Per Wave)`. Índice 0 para la primera oleada. Ejemplo con base 3 m/s y plantilla 0.3: 0.900, 0.936, 0.972 m/s. El aumento continúa después del boss y ya no tiene el antiguo tope fijo de multiplicador 2. Si se alternan plantillas con menor multiplicador, se impone un piso creciente para que la velocidad no baje respecto de la oleada anterior.

### Enemigos conectados con el jugador

- `EnemyNavigation.cs` valida una ruta completa **hasta el jugador**, usando el tipo y radio de agente correspondiente. Rechaza rutas que atraviesen barreras físicas aunque el NavMesh antiguo diga que hay paso.
- `PuertaConLlave.cs` configura obstáculos con carving para las puertas y sus barreras; al abrirlas se retiran junto con la colisión. La navegación se actualiza después de unos cuadros.
- `EnemySpawner.cs` y `BossSpawner.cs` descartan puntos aislados y buscan una posición libre en la zona accesible del jugador. No abren puertas ni saltean requisitos de llaves.
- `EnemyReachability.cs` revisa cada 1.5 s la conexión. Si un enemigo queda aislado durante 3 s, o intenta desplazarse y permanece atascado 6 s, busca una posición accesible y lo reubica conservando su salud. No lo mata, no da monedas y mantiene el conteo de la oleada. Si el jugador está momentáneamente fuera del NavMesh, espera.
- Los enemigos generados por oleadas y los esbirros persiguen al jugador incluso fuera del radio local de detección; los enemigos de patrulla colocados manualmente conservan su comportamiento. Los esbirros del boss también validan su ruta antes de aparecer.
- Validación: **Survive > Gameplay > Validar navegación de enemigos y balance**; evidencia en `Logs/EnemyNavigation/validation.json`. Incluye Level 2 con puntos forzados a otra isla y una prueba con puerta cerrada/abierta para los tres tipos de agente. No equivale a probar todas las posiciones del mapa.

Validación de navegación y velocidad: **46/46** comprobaciones correctas. Se registró por separado el error previo de carga de `Assets/New Terrain 3.asset`; no hubo errores nuevos durante estas comprobaciones.

Regresión de interacción tras agregar carving: **45/45**, sin errores, en `Logs/DoorProgression/result.json`. Se conserva E, orden de zonas, puertas enlazadas y consumo de llaves.

## Menú principal, pausa de Level 2 y velocidad de esbirros

- `MenuBehaviour.gameplayScene` y su valor guardado en SceneAlejandro apuntan a **Level 2**. La escena está habilitada en `ProjectSettings/EditorBuildSettings.asset`; Level 1 se conserva en la lista. El flujo de crear personaje y jugar usa el mismo destino.
- `Assets/Prefabs/UI/SyntyPauseMenu.prefab` está integrado en Level 2: ESC abre/cierra pausa, muestra cursor y detiene Time.timeScale. Botones Continuar, Reiniciar, Menú principal y Salir del juego, con tema Synty compartido. EventSystem usa InputSystemUIInputModule. Los cierres con ESC de vendedor/colocación conservan su prioridad existente.
- `BossBase.ApplyWaveMultipliers` conserva el multiplicador de velocidad de la oleada y lo aplica al controlador de cada esbirro antes de Start. Ejemplo: prefab a 8 m/s y multiplicador 0.312 -> **2.496 m/s**, igual al zombi de ese tipo en esa oleada. La furia del boss no multiplica la velocidad de sus esbirros. Se conservan las velocidades base configuradas por el usuario en los prefabs.
- Configuración: `Assets/Editor/GameplayMenuFixSetup.cs`. Validación: `GameplayMenuFixValidation.cs`, con botón Play real, ESC, click en Continuar, reinicio, vuelta a SceneAlejandro e invocación temporizada de esbirros, incluyendo furia. Captura real de pausa en `Logs/GameplayMenuFix/pause-level2.png`. No se probó un build distribuido.

### Origen y asignación del sonido de recompensa

`Assets/Audio/Rewards/ZombieKillReward.wav` fue generado para este proyecto como un timbre corto de dos notas (síntesis de tonos, aproximadamente 0.24 s); no proviene de Synty ni de un paquete de terceros. Se asigna en **EnemyHealth > Reward Sound** en los prefabs de zombis y boss. `PlayerRewardFeedback.cs` lo reproduce en 2D al acreditar la recompensa por baja.

La recogida de monedas físicas es independiente: **CoinPickup > Collect Sound** está vacío en `Assets/Prefabs/coin.prefab` y `coin 1.prefab`. Cambiar Reward Sound cambia la recompensa por matar; asignar Collect Sound cambia la recogida del objeto del suelo.

Resultado final del flujo de menú/pausa/esbirros: **26/26**, con `errors` vacío en `Logs/GameplayMenuFix/validation.json`. Se registraron aparte los assets previos `New Terrain 3.asset`, el prefab faltante `LevelLoader` (GUID e03da6a7ced7b0f45b5982deba906d5c), y una excepción de conexión de red del Editor. Se conserva evidencia de esos avisos en `baselineErrors`; no se modificaron esos assets durante este arreglo.

## Planos del boss, apertura animada, cajas comunes y desgaste de torretas

- El boss suelta **un plano y una llave garantizados**. `Assets/Prefabs/Items/DoorBlueprint.prefab` es un objeto 3D de papel enrollado con dibujo de plano; se recoge con **E**, sin recogida automática. `BossBase.doorBlueprintPrefab` controla el drop.
- `DoorBlueprintPickup.cs` entrega el plano a `PlayerDoorGuide.cs`. Un plano guía una apertura lógica: si todavía no hay llave, espera; al tenerla aplica `Assets/Resources/Gameplay/DoorGuideNeon.mat` a los renderers de la próxima etapa válida. Los materiales originales se conservan y se restauran al desbloquear. No se modifican los materiales compartidos del escenario.
- La secuencia sigue **Zona2 → Zona3 → Zona4/Zona4.1 → Zona5**. El par Zona4 se resalta y se abre junto, con una llave y un plano en total. Se pueden acumular planos. El HUD indica la zona señalada o que hace falta una llave. El plano no es obligatorio para abrir: la llave y los prerrequisitos siguen siendo la regla de acceso.
- `PuertaConLlave.cs` ya no destruye ni desactiva las puertas. Desplaza puerta y barreras hacia arriba durante **1.4 s**, con movimiento suave y tiempo escalado (la pausa lo detiene). El modelo queda visible arriba y los obstáculos de carving se liberan al terminar. Se quitaron las marcas estáticas de los objetos móviles. En Level 2 el desplazamiento es de 5 a 6.3 m según altura; editar **Opening Translation / Opening Duration** en cada puerta.
- `Assets/Data/Vendor/AD_CommonAmmoBox.asset`: caja común de **60 balas**, a la reserva del arma equipada. `VC_Armero.asset > Offers` fija el precio, inicialmente **30 monedas**. Se añadió al final del catálogo, conservando índices y precios de armas, torreta y cargador explosivo. No ocupa hotbar, no recarga instantáneamente y no sustituye el cargador explosivo activo. Sin arma, reserva llena, pausa o monedas insuficientes, no cobra.
- `TD_AutoTurret.asset`: **Max Health 150**, **Health Cost Per Shot 10**, **Shots Per Second 2**. Son **15 disparos** por torreta; el último también hace daño. Una pared, la falta de objetivo o la pausa no gastan vida. Al agotarse deja de disparar, libera su obstáculo y se elimina. `TurretHealthDisplay.cs` muestra barra y vida sobre el modelo; la previsualización no la muestra. `AutoTurret.TakeDamage` permite aplicar daño adicional desde otros sistemas.
- Los parámetros de torreta y cantidad de balas comunes también aparecen en **Survive > Gameplay > Balance de enemigos y vendedor**.
- Integración: `DoorBlueprintSuppliesSetup.cs`. Validación: `DoorBlueprintSuppliesValidation.cs`; evidencia, imágenes y configuración en `Logs/DoorBlueprintSupplies`. Usa puertas/barreras reales copiadas de Level 2 y prefabs reales en una escena aislada; no es un build ni una partida completa.

Validación final de planos, puertas animadas, munición y desgaste: **44/44**, sin errores nuevos. La excepción previa de conexión de red del Editor se registra aparte en `baselineErrors`. Regresión de interacción y progresión de puertas: **45/45**, sin errores.

Regresión de navegación tras la apertura animada: **46/46**, sin errores nuevos; se verifica que abrir libere rutas de los tres tipos de agente. Total de esta entrega: **135 comprobaciones correctas** (44 nuevas + 45 puertas/llaves + 46 navegación). Capturas revisadas en `Logs/DoorBlueprintSupplies`.

## Cámara al apuntar, derrota y clasificación Synty

- **Cámara:** `Assets/Prefabs/Cameras/UPPThirdPersonCamera.prefab` > `UPPTPSCameraController` > `Aim Camera State > Right Camera Offset` está en **1 m** (antes 0.5). El estado normal sigue a 0.5 m. Se conservan distancia, campo de visión, colisión y el control de apuntado por hold. El controlador también usa ese valor como defecto para rigs nuevos.
- **Derrota:** `Assets/Prefabs/UI/SyntyResultsOverlay.prefab` está integrado en Level 2. `PlayerDeathController` llama a `MenuBehaviour.ShowGameOver`; éste activa el panel Synty y `SyntyResultsUI.ShowDefeat` finaliza las estadísticas. Muestra puntaje, enemigos, oleada, tiempo y precisión. Permite guardar nombre/puntaje, reintentar, ver la clasificación y volver al menú principal.
- **Puntaje vivo:** Level 2 incluye `GameStatsManager`, y `EnemySpawner.StartWaves` también garantiza su existencia antes de comenzar una partida. El HUD lee `GameStatsManager.totalScore`, el mismo valor congelado al perder y guardado en la tabla. La fórmula se actualiza durante la partida: combate + oleada × 250 + segundos × 5 redondeados + críticos × 50. Por la fórmula existente, la primera oleada aporta 250 puntos desde el inicio.
- **Combate:** zombi común **100**, esbirro del boss **50**, boss **1000**. `BossBase` marca a sus invocados y `EnemyHealth` registra una sola baja. `PlayerWeaponController` cuenta disparos e impactos; la escopeta cuenta un disparo por gatillo y un acierto si impactó al menos un enemigo. Los explosivos cuentan acierto si dañaron algún enemigo, incluso desde un impacto sobre una pared. Las torretas aportan bajas al puntaje, sin alterar la precisión del arma del jugador.
- `GameStatsManager.FinalizeGameStats` congela el resultado; repetirlo o registrar bajas tardías no cambia el puntaje final. Reiniciar crea una partida nueva y volver al menú deja de sumar tiempo. El adaptador antiguo de estadísticas en `Assets/Scripts/Leaderboard/` usa ahora las mismas reglas compartidas.
- **Leaderboard:** `LeaderboardManager.cs` conserva los datos locales bajo las claves `LB_Count`, `LB_Name_i`, `LB_Score_i`. Ordena y limita a las mejores diez marcas. Borrar la tabla borra únicamente sus entradas, sin borrar otras preferencias. `SyntyResultsUI.cs` presenta las filas con fuente, paneles, tarjetas y botones de `SyntyVendorTheme`.
- La clasificación se abre desde el botón de leaderboard que ya existía en SceneAlejandro, desde la pausa y desde la derrota. ESC vuelve al panel anterior, sin reanudar si el jugador perdió. Si el puntaje no entra en la tabla, la derrota informa ese resultado; guardar está protegido contra pulsaciones repetidas en una misma partida.
- Integración: **Survive > UI > Integrar derrota clasificación y puntaje** (`SyntyResultsSetup.cs`). Validación: **Survive > UI > Validar cámara derrota HUD y clasificación** (`SyntyResultsValidation.cs`). Datos de prueba aislados de los records reales. Capturas y resultados en `Logs/SyntyResults`; no se hizo build distribuido.

Resultado final de cámara/derrota/HUD/clasificación: **34/34**, sin errores nuevos, en `Logs/SyntyResults/validation.json`. Se registran aparte los avisos anteriores de `New Terrain 3.asset`, el prefab `LevelLoader` faltante y la excepción de red del Editor. La validación usa y elimina sus propias claves de PlayerPrefs, conservando los records reales. Se revisaron visualmente las capturas de cámara, derrota y clasificación.


## Ambiente postapocalíptico de Level 2: día y tormenta nocturna

- `Assets/Scripts/CityAtmosphere.cs`, objeto **CityAtmosphere** en Level 2, sincroniza el ambiente con `StormLightingSkybox.NightProgress`. No introduce otro reloj ni otro trigger: `StormZoneSkybox` sigue iniciando la tormenta y conserva la transición original de 30 segundos. La pausa congela el avance.
- Los perfiles **Assets/Settings/Level2Atmosphere/City_Day.asset** y **City_Night.asset** controlan ACES, exposición, contraste, saturación, balance de blancos, tonos de sombras/luces, bloom y viñeta. El volumen diurno tiene peso 1/prioridad 20 y el nocturno peso igual al progreso/prioridad 21, de modo que la transición mezcla los valores sin saltos. Sin motion blur, profundidad de campo, aberración cromática ni grano durante el combate.
- La iluminación ambiental usa gradiente de tres colores para conservar detalle bajo techos y entre edificios. La bruma exponencial suave pasa de polvo diurno a azul grisáceo nocturno. El sol conserva intensidad/rotación controladas por `StormLightingSkybox`; la configuración del mapa usa día 1.15 y noche 0.28 con orientación lunar desde arriba. Faroles, lluvia y relámpagos siguen controlados por los scripts originales.
- **City_Skybox.mat** es una variante propia para este mapa; el material original de Skybox no se modifica. `SkyboxController` sigue creando su instancia de juego y mezclando los colores nocturnos. Los perfiles tampoco se instancian ni se modifican en runtime: sólo cambia el peso del volumen.
- La cámara UPP creada al jugar recibe HDR, postproceso, SMAA High, dithering y actualización de volúmenes por frame desde el controlador de esta escena. No cambia el prefab de cámara de otros mapas. El HUD conserva sus Canvas de overlay.
- Ajustes: seleccionar **CityAtmosphere** para bruma/relleno/colores; seleccionar los perfiles para efectos; seleccionar **storm/StormLightingSkybox** para duración e intensidad nocturna. Integración reproducible: **Survive > Ambiente > Integrar ciudad postapocalíptica** (`CityAtmosphereSetup.cs`). Validación nativa y capturas: **Survive > Ambiente > Validar día y noche en Level 2** (`CityAtmosphereValidation.cs`), en `Logs/CityAtmosphere`.


Validación final del ambiente: **39/39 comprobaciones en Play Mode**, `errors: []`, en `Logs/CityAtmosphere/validation.json`. Se revisaron visualmente las capturas `01-day-gameplay.png`, `02-transition-gameplay.png`, `03-night-gameplay.png` y `04-night-city.png`, corrigiendo dos iteraciones demasiado oscuras antes del resultado final. El control recorre la transición de 30 segundos con tiempo acelerado y comprueba que la pausa detenga el progreso; invoca el callback del trigger original con el collider real del jugador y también con un collider ajeno. Comprueba perfiles efectivos del stack URP, faroles reales presentes/apagados/encendidos, lluvia, activación de relámpagos y descarga de los volúmenes al volver al menú. La captura nocturna se toma entre relámpagos. Los avisos anteriores de `New Terrain 3.asset` y `LevelLoader` faltante se registran aparte en `baselineErrors`. Sin medición de FPS ni build distribuido.

Valores finales para ajustar el look: exposición diurna **+0.35 EV**, nocturna **+0.85 EV**; contraste **6/4**; saturación **-12/-16**; bloom **0.16/0.24**. La luz ambiental nocturna conserva detalle en superficies laterales, además de la luz lunar; el controlador sólo actualiza el ambiente cuando cambia el progreso y no ejecuta capturas de reflexión ni bakes de GI por frame.


## RIFLE PRO para acciones actuales y conservación de recursos

Por indicación del usuario se integraron las animaciones compatibles con las acciones actuales antes de limpiar la biblioteca. El controlador sigue siendo **Assets/Animations/Player/Controllers/UPPAnimatorTPS.controller**; no se agregó otro controlador ni otro dueño del movimiento.

- **Caminar armado:** ocho direcciones `W2_Walk_Aim_*_Loop_IPC`.
- **Correr armado:** ocho direcciones `W2_Jog_Aim_*_Loop_IPC`, conservando los umbrales de los árboles y la cadencia anterior mediante el factor de duración entre clips.
- **Moverse agachado armado:** ocho direcciones `W2_CrouchWalk_Aim_*_Loop_IPC`, además de idle y giros agachados.
- **Giros de pie:** variantes `W2_Stand_Aim_Turn_In_Place_L/R_Loop_IPC` en el idle armado.
- **Salto armado:** tres fases `W2_Stand_Aim_Jump_Start/Air/End_IPC` en la capa de piernas. El salto sin arma, reptar y rodar conservan sus clips y eventos originales; RIFLE PRO no reemplaza las acciones para las que no hay una variante equivalente integrada.
- La pose de apuntado y la recarga RIFLE PRO existentes se conservan. La recarga sigue en la capa superior con máscara y puede coexistir con locomoción. El retroceso sigue perteneciendo al sistema de armas.
- **34 fuentes FBX de animación** conectadas (más el material de importación). La lista exacta está en `Logs/RifleGameplayAnimations/used-sources.txt`. Las variantes sin uso se eliminan después de recalcular las dependencias del controlador actualizado.
- Integración: **Survive > Animaciones > Integrar RIFLE PRO en acciones actuales** (`Assets/Editor/RifleGameplayAnimationSetup.cs`). Validación: `RifleGameplayAnimationValidation.cs`, **63/63 en Play Mode**, con ocho direcciones para caminar/correr/agachado, desplazamiento físico, salto real, prone, roll y su salida, disparo y recarga real en movimiento. Resultado `Logs/RifleGameplayAnimations/validation.json`; capturas revisadas. Sin build distribuido.
- Comparación contra el controlador previo: mismos documentos y topología, **84 transiciones/máquinas/eventos idénticos**, mismo contrato de capas y parámetros. Se conservaron fuerza de salto, física, input, máscaras y eventos de estado.

La decisión de limpieza confirmada es **conservar las bibliotecas de recursos artísticos y UI, y eliminar demos/ejemplos**. También se conservan fuentes de generación, Resources, sonidos y mapas propios. El respaldo inicial de 7.893 candidatos quedó completo y **no se borró ninguno de esos archivos** antes de recibir esta preferencia. El plan final usa un subconjunto del inventario revisado y protege las animaciones recién conectadas.


Resultado estructural de la limpieza con las preferencias confirmadas: **989 assets retirados**, más sus `.meta`, por **1.488.649.004 bytes (1,39 GiB)** dentro de Assets. **Cero fallos de borrado y cero referencias faltantes nuevas** en el cierre conservado. Se retiraron 879 archivos del paquete RIFLE PRO sin uso, 73 demos/ejemplos de Synty, 4 escenas demo de Polygon, 1 demo de Hovl, 14 elementos de demo/plantilla de Starter Assets y 18 restos antiguos (incluidos seis NavMesh sin referencia y tres prefabs de UI/pociones anteriores). Las bibliotecas artísticas, sonidos, mapas propios y las 34 animaciones RIFLE PRO conectadas permanecen. La lista exacta está en `Logs/UnusedContent/LIMPIEZA_REALIZADA.md` y la evidencia en `result.json`.

Respaldo aplicado: `.utmp/UnusedAssets-20261104T152031-e20cd3/manifest.json`, con payloads por GUID y hashes SHA-256 de cada archivo y su `.meta`. Se conserva además el respaldo inicial, que no llegó a aplicarse. Al permanecer los respaldos en D:, retirar 1,39 GiB de Assets no implica haber liberado esa cantidad de espacio total del disco.

El paquete original Modern Weapons v1.2 fue inspeccionado completo: **1.318 clips de audio**, sin archivos de recarga, cargador o cerrojo. Se conservan las recargas JU TPS porque están en uso y Modern Weapons no trae sustitutos de recarga. Inventario del paquete original: `Logs/AudioInventory/modern-weapons-package-paths.json`.


Verificación posterior al borrado: **catálogo/perfil/rig 221/221, con 1.146 módulos de personaje**, y **día/transición/noche en Level 2 39/39**. No aparecieron errores nuevos; se conservaron aparte los avisos previos de Terrain 3 y LevelLoader. Evidencia: `Logs/UnusedContent/validation-after-prune.log` y `Logs/CityAtmosphere/validation.json`. Las animaciones seleccionadas habían pasado **63/63** antes de la limpieza y sus 34 fuentes se verificaron presentes después. El preset original del cielo se restauró idéntico desde el respaldo y se protege como biblioteca de arte; el conteo final de 989 archivos ya lo contempla.

Auditoría final tras restaurar el preset: **10.307 assets conservados, cero candidatos adicionales y cero bloqueos** dentro del alcance acordado. Se conservaron las bibliotecas solicitadas, por lo que esta cifra no significa que cada recurso de biblioteca esté colocado en una escena. Resultado `Logs/UnusedContent/audit.json`, ejecución `final-audit.log`.
