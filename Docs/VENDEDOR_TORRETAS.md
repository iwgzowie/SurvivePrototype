# Vendedor NPC y torretas

Implementación del 24/10/2026. Integra las armas configuradas, el inventario de seis espacios, la interacción desde cámara, las monedas existentes y `EnemyHealth`.

## Ajustes actuales de gameplay — Level 2, 01/11/2026

El jugador comienza cada instancia de partida con **0 monedas** y **una pistola**
en el primer espacio del inventario, con cargador y reserva del preset existente.
La entrega se hace una sola vez al iniciar; habilitar nuevamente el inventario no
duplica el arma. La pistola se selecciona cuando se habilitan los controles del juego.

Durante la colocación, mantener **Q** gira la torreta hacia la izquierda y mantener
**E** la gira hacia la derecha, a **90 grados por segundo**. Su orientación comienza
en cero y es independiente de los giros del personaje y la cámara. Q y E juntas
cancelan el giro. La base desplegada conserva el ángulo elegido; la cabeza sigue
apuntando automáticamente a enemigos. E no activa la interacción mientras se coloca.
Las consultas de espacio libre y apoyo también respetan el ángulo de colocación.

El impulso de salto del prefab del jugador cambia de **3 a 1.8542421**. Se mantiene
la gravedad, la masa de 85 kg y la lógica UPP existente. El valor se calibró para
alcanzar aproximadamente el **30% de la altura anterior**, teniendo en cuenta que
`InAirMovementControl` desplaza al personaje 1 m/s hacia abajo durante el vuelo;
multiplicar directamente el impulso por 0.3 o por la raíz de 0.3 no reproduce esa
reducción con el controlador completo activo.

La validación específica abre `Assets/Scenes/Level 2.unity` directamente y no
reconfigura el menú ni la lista de escenas de la build. Se ejecuta desde
`Survive > Gameplay > Validar inicio, torreta y salto de Level 2`. Evidencia:
[resultado JSON](D:/SurvivePrototype/Logs/LevelTwoGameplay-Validation.json) y
[log de Unity](D:/SurvivePrototype/Logs/LevelTwoGameplay-Validation.log).

Resultado ejecutado: **23/23 comprobaciones aprobadas, 0 errores de runtime**.
Con el controlador UPP completo activo, la altura medida pasó de **1.81072 m a
0.54309 m**: proporción **0.29993**, equivalente a **70.007% menos altura**.
Se comprobaron pulsaciones mantenidas de Q/E, giro simultáneo nulo, independencia
del personaje, conservación de la orientación al colocar y ausencia de armas
duplicadas al volver a habilitar el inventario. Los spawners se deshabilitan sólo
en la instancia de prueba para aislar los cambios. No se guardó ni alteró el mapa
de Level 2; la configuración se hereda del prefab del jugador. No constituye una
partida completa ni una build distribuida.

## Integración original en Level 1 — 24/10/2026

El armero está cerca del punto inicial del jugador, en `(-10.06, -0.10, -1.21)`.
Acercarse, apuntar al cuerpo y presionar **E**. La tienda muestra el saldo, los precios,
el estado de cada compra y las estadísticas de los productos. **Escape** o **Cerrar**
cierra la tienda. La partida sigue corriendo mientras se compra.

Las armas compradas se guardan en el primer espacio libre y se seleccionan con los
controles actuales **1–6 / rueda**. La compra no cambia el arma en mano. Con los seis
espacios ocupados se rechaza la compra sin descontar monedas ni reemplazar armas.

Las torretas se guardan como kits en una reserva propia de hasta diez unidades.
Presionar **T**, apuntar al suelo y confirmar con **clic izquierdo**. La vista previa
verde indica un sitio válido y la roja muestra el motivo del rechazo. **Q/E** ajustan
el giro. **T, Escape o
clic derecho** cancela sin gastar el kit. El clic que coloca o cierra la tienda queda
bloqueado temporalmente para que no dispare también el arma del jugador.

La colocación exige suelo a cinco metros como máximo, pendiente de hasta 15 grados,
apoyo bajo los cuatro extremos y volumen libre. Rechaza enemigos, jugadores,
vendedores, otras torretas y cuerpos dinámicos como superficie de apoyo. Comprueba
también el trayecto desde el jugador, los objetos bajos debajo de la base y vuelve
a validar al confirmar. Cada kit del mundo puede recogerse una sola vez.

## Balance inicial editable

Cada jugador comienza la partida con **0 monedas y una pistola**. `CoinPickup` acredita su valor
a `PlayerWallet`, incluso si el collider pertenece a un hijo del jugador, y evita
créditos duplicados. El saldo y los kits pertenecen a esa partida: no se persisten
entre cargas de escena. No se agregaron nuevas recompensas por matar enemigos.

| Producto | Monedas |
| --- | ---: |
| Pistola | 50 |
| Revólver | 90 |
| Subfusil | 120 |
| Rifle de asalto | 160 |
| Escopeta | 140 |
| Rifle de caza | 180 |
| Rifle de precisión | 240 |
| Ametralladora | 220 |
| Torreta automática | 150 |

Precios en `Assets/Data/Vendor/VC_Armero.asset`. Saldo inicial en el componente
`PlayerWallet` del prefab `Assets/Prefabs/Player/UPPPlayer.prefab`.

La torreta usa el modelo de la ametralladora existente, una base propia, audio
espacial, trazador y el efecto de boca del arma. Su configuración está en
`Assets/Data/Vendor/TD_AutoTurret.asset`: **15 m**, **20 de daño**, **4 tiros/s**.
Adquiere al enemigo vivo visible más cercano y revalida rango y obstáculos antes
de cada disparo. Usa `EnemyHealth.TakeDamage`, conservando muerte, puntaje y avisos
de oleadas. No daña jugadores. El jugador y las paredes sí bloquean sus tiros.
Incluye `NavMeshObstacle` con carving para que los enemigos rodeen la base.

La versión inicial tiene munición ilimitada y no recibe daño. `lifetimeSeconds = 0`
la conserva hasta salir de la escena; un valor positivo configura su duración.
Estas son decisiones provisionales de balance, no un sistema de mejoras, reparación
o reventa.

## Assets y responsabilidades

- `NpcVendor`: catálogo, nombre y revalidación de alcance/obstáculos del vendedor.
- `PlayerVendorController`: UI y transacción; devuelve monedas si falla la entrega.
- `PlayerWeaponInventory.TryStorePurchase`: entrega sin reemplazos ni cambios de selección.
- `PlayerWallet`: saldo por jugador, gasto validado y crédito con saturación de entero.
- `PlayerTurretController`: reserva, vista previa y colocación. No ocupa slots de armas.
- `TurretItem : MasterItem`: kit recogible en el mundo por la misma tecla E.
- `TurretDefinition`: datos compartidos y referencia al prefab desplegado.
- `AutoTurret`: adquisición, cadencia, línea de visión, presentación y daño.
- `IWorldInteractable`: extensión del raycast existente para NPC y kits.

Prefabs propios en `Assets/Prefabs/Vendor/`: `NpcVendor`, `TurretItem` y `AutoTurret`.
Se conservan los prefabs originales de Polygon Apocalypse. Las copias anteriores a
la integración de Level 1 y UPPPlayer están en `.utmp/VendorTurrets-Backup/`.

## Herramientas y validación

`Survive > Vendedor > Crear e integrar vendedor y torretas` genera assets faltantes
y agrega un vendedor a Level 1 si todavía no existe uno. No reemplaza precios ni
definiciones ya creadas. Al guardar escenas desde Unity se deben preservar los datos
serializados de otros sistemas, incluyendo campos antiguos de oleadas.

`Survive > Vendedor > Validar compras y torretas en Play Mode` ejecuta un escenario
aislado con los prefabs reales, sin guardar la escena de prueba ni perfiles. En modo
batch se desactiva la exigencia de captura de mouse sólo en las instancias de prueba.
La configuración del prefab de juego sigue exigiendo cursor bloqueado.

Resultado y alcance ejecutado: `Logs/VendorTurret-Validation.json`.
Log de integración: `Logs/VendorTurret-Setup.log`.
Captura de tienda: `Logs/VendorTurret-Shop.png`.

Estado final: **IMPLEMENTADO Y VERIFICADO EN PLAY MODE**, **73/73 comprobaciones**,
**0 errores de runtime** en la ejecución final. Incluye compras válidas e inválidas,
devolución ante fallo de entrega, inventario lleno, reserva llena, recogida única de
kits y monedas con collider hijo, pausa/muerte, validación de suelo, obstáculos bajos,
cadencia, rango, cambio de objetivo y paredes que bloquean el disparo.

La prueba adicional cargó **Level 1 real**, abrió la tienda mediante el raycast desde
el spawn original, compró una pistola y un kit, encontró suelo válido y desplegó una
torreta consumiendo exactamente ese kit. La captura se revisó visualmente a 1280×900;
se corrigieron el tamaño del mensaje de compra, la ocultación inmediata del HUD y el
modo de importación del icono de la torreta.

El smoke de Level 1 detectó una incompatibilidad previa: la escena conserva sus tres
oleadas en `waves`, mientras el código usa `baseWaveTemplates`. Se agregó únicamente
`FormerlySerializedAs("waves")` a ese campo de `EnemySpawner`, para cargar los datos
originales sin reemplazarlos. La prueba confirmó que el spawner vuelve a arrancar.
La comparación contra el backup de la escena conserva el bloque original de oleadas;
el cambio de escena de esta tarea sólo agrega la instancia del vendedor.

**NO VERIFICADO:** partida manual completa, navegación de una oleada alrededor de
las torretas, balance prolongado y build distribuida. El escenario controlado y el
smoke no sustituyen esas pruebas.

## Corrección del material del NPC — 24/10/2026

El modelo del armero conservaba un material Standard del paquete original. Se reutilizó el conversor existente de materiales de armas para crear `Assets/Materials/Vendor/PolygonApocalypse_Material_01_A_5bb68a94_URP.mat`, con shader URP/Lit, textura original en Base Map, color blanco sin tinte y mapa normal original. Los overrides se guardaron en el prefab propio del vendedor; el prefab y el material originales de Polygon Apocalypse se conservaron.

**VERIFICADO:** compilación correcta, 35 slots de materiales con shader y textura presentes y render del prefab guardado con camuflaje y colores correctos. Evidencia: [render del NPC](D:/SurvivePrototype/Logs/VendorNpc-Material.png) y [log de Unity](D:/SurvivePrototype/Logs/VendorNpc-Material.log). Menú de reparación: `Survive > Vendedor > Corregir material URP del NPC`.

La documentación vigente ahora reside en `E:\SurvivePrototype\Documentacion`. `D:\SurvivePrototype\Docs` es un junction hacia esa carpeta; las rutas anteriores siguen resolviendo al mismo contenido.
