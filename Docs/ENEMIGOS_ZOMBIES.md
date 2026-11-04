# Enemigos con modelos ApocalypseZombies

## Prefabs integrados

Los cuatro prefabs de `Assets/Players` conservan su raíz, tag Enemy,
CapsuleCollider, NavMeshAgent, EnemyHealth y configuración de IA. Se reemplazó
la cápsula visible por un modelo anidado de Synty dentro de `ZombieVisual`.

| Prefab | Comportamiento | Modelo | Animator Controller |
| --- | --- | --- | --- |
| EnemyChasing | Melee; persigue permanentemente | ApocalypseZombie_01 | Zombie_Melee |
| EnemyChasingRanged | Distancia; persigue permanentemente | ApocalypseZombie_02 | Zombie_Ranged_02 (override de Zombie_Ranged) |
| EnemyPatrol | Melee; patrulla y persigue al detectar | ApocalypseZombie_03 | Zombie_Melee_03 (override de Zombie_Melee) |
| EnemyAttack | Distancia; patrulla y persigue al detectar | ApocalypseZombie_04 | Zombie_Ranged_04 (override de Zombie_Ranged) |

Los modelos originales están en
`Assets/Synty/SidekickCharacters/Characters/ApocalypseZombies`. El modelo 05
queda disponible como alternativa. Los originales de Synty no se modificaron.

Los prefabs tenían referencias a GUID de scripts que ya no correspondían a
los `.meta` actuales. Se reparó cada referencia con la clase que corresponde
al nombre del prefab, conservando sus demás datos. `EnemyChasing` ahora usa
`EnemyChasing`, y `EnemyPatrol` usa `EnemyPatrol`.

## Animator y paquetes de animación

Los cuatro enemigos usan clips Humanoid adaptados de
`Monster_Animations_Zombie_v1.0` y `Zombie_Animations_Set_v1.0`, obtenidos de
los paquetes locales en `E:/Unity`. Los FBX de origen utilizados se conservan
bajo `Assets/Animations/ZombiePacks`; los clips editables del juego mantienen
sus rutas y GUID en `Assets/Animations/Enemies`.

| Clip del juego | Paquete | Origen y adaptación |
| --- | --- | --- |
| Zombie_Idle | Zombie Animations Set | Zombie_Idle_01; loop |
| Zombie_Walk | Zombie Animations Set | Zombie_Walk_01_Forward_InPlace; loop |
| Zombie_Run | Zombie Animations Set | Zombie_Run_01_Forward_InPlace; loop |
| Zombie_MeleeAttack | Monster Animations Zombie | Zombie_ATK_0105; zarpazo, sin loop |
| Zombie_SpitAttack | Monster Animations Zombie | Zombie_SK_Firing_C0149; primeros 37 frames, sin loop |
| Zombie_Death | Monster Animations Zombie | Zombie_Die14; caída hacia adelante a 1.5x, sin loop y pose final sostenida |

Monster se importó como Humanoid para retargetear sobre los Avatar de Synty.
La selección evita los ataques de Zombie Set que adelantan la cadera más de
un metro respecto de la cápsula. Para escupir se usa la preparación, flexión
del torso y recuperación inicial de Firing; se recortó el salto posterior.
Los clips finales compensan la altura del cuerpo según el mesh Synty para
corregir penetraciones del suelo propias del retarget. Run conserva las fases
aéreas. No se añade IK de pies ni se mueve la raíz mediante root motion.

`Zombie_Melee.controller` y `Zombie_Ranged.controller` conservan Locomotion,
Attack y Death. Locomotion mezcla Idle/Walk/Run con Speed en metros por segundo
(umbrales 0, 0.36 y 2.32). LocomotionSpeed acelera la reproducción al superar
la velocidad nativa de Run: `max(1, velocidad / animationRunSpeed)`. El valor
configurado en los cuatro prefabs es 2.32 m/s.

Attack usa el trigger del mismo nombre y el multiplicador AttackSpeed. El
impacto configurado es 1.3 s para el zarpazo y 0.6 s para la escupida, antes de
aplicar `attackAnimationImpactTime / attackWindupTime`. El daño mantiene el
windup de la IA; la recuperación completa del estado etiquetado Attack bloquea
un nuevo ataque para que cada golpe corresponda a un gesto. El cooldown sigue
actuando como intervalo mínimo.

Die interrumpe cualquier estado. Death no tiene salida y no repite el clip.
El destroyDelay de los prefabs es aproximadamente 3.57 s: permite completar la caída de 2.64 s y
sostener brevemente la pose final. Los modelos 02/03/04 usan Animator Override
Controller que cambia solamente Zombie_Death por Zombie_Death_02/03/04. Cada
variante corrige la altura de la caída según su mesh y accesorios para que el
cuerpo no atraviese el piso. La caída hacia adelante mantiene los accesorios
de la espalda arriba; evita que el soporte del suero del modelo 02 deje el
cuerpo suspendido en una caída hacia atrás. Los Avatar, modelos, colliders y NavMeshAgent
permanecen iguales.

## Escupida

Los dos enemigos a distancia tienen FirePoint vinculado a la cabeza, junto a
la boca. El clip llama a `EnemyAnimationEvents.PlaySpit` a los 0.6 s del clip, que reproduce un
chorro de partículas verdes orientado al jugador. Una traza limita su duración
hasta el primer obstáculo, ignorando los colliders del propio enemigo.

Este evento es de presentación: no aplica daño. `EnemyAttack` y
`EnemyChasingRanged` comparten `EnemyBaseController.RangedAttackRoutine` y
`CanHitRangedTarget`. Antes de empezar el ataque y al terminar su preparación,
se comprueba que el jugador esté vivo, dentro de `attackRange` y sin obstáculos
entre la boca y el centro de su collider. A distancia se usa el rango estricto;
`hitTolerance` sigue perteneciendo al golpe melee.

`attackObstructionMask` selecciona las capas que bloquean el ataque. Se ignoran
triggers y colliders de las jerarquías del enemigo y del jugador. Un chequeo de
solapamiento de radio `rangedOriginClearanceRadius` (2,5 cm por defecto) impide
lanzar desde una boca que quedó dentro de una pared. Si no hay línea de tiro,
el enemigo continúa persiguiendo en vez de detenerse a atacar la pared. Si el
jugador se cubre o se aleja durante el windup, ese intento no aplica daño.

Con `projectilePrefab` vacío, como en los prefabs actuales, se aplica daño
directo después de esas comprobaciones. Si se asigna un proyectil, se instancia
orientado al objetivo desde `firePoint`; sin ese punto usa la raíz del enemigo.
El prefab de proyectil debe aportar movimiento, colisión y daño propios. La
validación del disparo no reemplaza las colisiones durante el vuelo.

## Salud y muerte

EnemyHealth envía Die al Animator del modelo hijo y restablece Speed/Attack al
morir. La IA y la navegación se desactivan como antes, y el objeto se destruye
con el destroyDelay configurado para dejar terminar el clip nuevo. El flash de impacto guarda los colores
originales por valor, para restaurarlos correctamente en el modelo Synty.

## Edición y límites

- Usá los prefabs de `Assets/Players` para colocar los enemigos en una escena.
- El NavMesh de la escena debe estar horneado para que puedan moverse.
- Se conservó la cápsula de altura 2, centro Y 0 y baseOffset 1 del agente.
  El modelo visual compensa el origen aproximadamente en Y -1.
- Los valores del Inspector de movimiento, alcance, daño, tiempos y patrulla
  prevalecen: las clases derivadas ya no los sobrescriben en `Start()`. La base
  asigna `moveSpeed` al NavMeshAgent al iniciar; `stoppingDistance` se configura
  directamente en el agente.
- Para usar una ruta por puntos, desactivá `useRandomPatrol` y asigná los
  `patrolPoints` de la instancia en orden. Se saltean referencias vacías y,
  si no hay ningún destino válido, el enemigo queda quieto. `waitTimeAtPoint`
  controla la espera entre destinos. Con patrullaje aleatorio activado, se
  elige un primer destino al iniciar aunque no haya puntos asignados.
- Level 1 ahora tiene instancias de EnemyChasing y EnemyChasingRanged, junto
  al jugador y el arma. El prefab del mapa ya está disponible; su instancia
  se convirtió en geometría propia de la escena para resolver la demora de
  apertura. Ver [Integración y rendimiento de Level 1](LEVEL1_INTEGRACION_RENDIMIENTO.md).

El menú `Survive > Enemies > Integrar modelos ApocalypseZombies` permite generar
la integración cuando aún no existe. Conserva los clips/controllers/modelos
ya integrados para que no sobrescriba ajustes manuales posteriores.

## Valores preservados de los prefabs

Las estadísticas que antes imponían los scripts se trasladaron a los cuatro
prefabs para que la corrección del Inspector conserve sus valores de juego.
El estado inicial de los dos Chasing también está serializado; no determina
por sí solo la persecución permanente, que depende de su detectionRange.

| Prefab | moveSpeed | attackRange | attackDamage | attackCooldown | attackWindupTime | detectionRange | randomPatrolRadius |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| EnemyChasing | 5 | 1,8 | 10 | 0,8 | 0,5 | 9999 | 5 |
| EnemyChasingRanged | 4 | 10 | 10 | 2 | 0,4 | 9999 | 5 |
| EnemyPatrol | 4,5 | 1,5 | 8 | 1,5 | 0,5 | 8 | 12 |
| EnemyAttack | 2,5 | 10 | 10 | 2,5 | 0,5 | 12 | 6 |

Los cuatro mantienen `useRandomPatrol` activo como configuración inicial y
`stoppingDistance` del agente en 0,2. Cambiar esos valores en una instancia
permite adaptar su comportamiento sin modificar la clase ni el spawner.
Las animaciones, recuperación del ataque, modelos y referencias existentes
se conservan. Al deshabilitar la IA se cancela su ataque pendiente.

Las comprobaciones de animación documentadas debajo pertenecen a la integración
anterior. No acreditan por sí solas las nuevas validaciones de obstrucción y
configuración; éstas requieren su verificación específica en Play.

## Validación de los paquetes (13/09/2026)

- Compilación final de Unity, con las herramientas temporales retiradas: salida 0 y sin errores C#.
- 202/202 comprobaciones de gameplay correctas en seis casos de Play: los
  cuatro combates y dos patrullas.
- Cada variante completó tres ataques y tres impactos; los dos enemigos a
  distancia emitieron tres escupidas sincronizadas con su daño.
- Se verificaron las curvas Humanoid, loops, recuperación, cadencia de pasos,
  deformación real de la malla, apoyo sobre el piso, muerte terminal y destrucción
  al terminar el retardo. Las raíces permanecieron inmóviles después de morir.
- Se inspeccionaron nueve clips finales sobre los modelos correspondientes.
- Level 1 resuelve los nuevos clips de sus dos enemigos por herencia de prefab;
  no necesita modificaciones nuevas de la escena.
- El resultado global del runner es FAIL porque el Editor registró una
  SocketException de System.Net, también observada antes de esta integración.
  No falló ningún check de gameplay ni hubo frames de nuestros scripts en esa
  excepción. No se presenta esta ejecución como una prueba global sin errores.

Los reportes y capturas están fuera del repositorio en `zombie-pack-integration`.
Se retira el paquete temporal de herramientas y se restauran los archivos de
Packages exactamente. Estas pruebas son Play en una escena controlada y lectura
real de Level 1; no constituyen una build final del juego.

## Validación anterior de los modelos

Validado el 12/09/2026 con Unity 6000.3.5f2:

- Compilación final sin el paquete temporal: salida 0 y ningún error C#.
- 102/102 comprobaciones funcionales pasaron en seis casos de Play: cuatro
  combates y dos patrullas, sobre un NavMesh temporal plano.
- Se comprobaron Avatar Humanoid, referencias, persecución, Speed, Attack,
  daño, partículas y muerte terminal con Animator hijo. Las cuatro raíces
  tuvieron desplazamiento cero después de morir.
- La geometría animada apoyó entre 0.009 y 0.023 m sobre el piso en las
  muestras de locomoción. Se inspeccionaron capturas de movimiento, ataques
  y muerte, y se corrigió la pose inicial de rodillas antes de validar de nuevo.
- Después del ajuste visual final de partículas se hicieron dos comprobaciones
  adicionales a siete metros: ambas variantes emitieron 16 gotas, avanzaron
  aproximadamente 1.24 m desde la boca y terminaron con código de salida 0.
- El runner de los seis casos marcó resultado global FAIL por una única
  SocketException/ConnectFailure con stack System.Net. No hubo checks de
  gameplay fallidos ni llamadas a nuestros scripts en esa excepción. Se
  conserva ese resultado original; no se lo presenta como ejecución limpia.

La evidencia queda fuera del repositorio, en la carpeta de trabajo
`zombie-integration/validation-results`, junto a `validation-final.log` y
`spit-preview-final.log`. Se retiró el paquete temporal y se restauraron
manifest.json y packages-lock.json exactamente a su estado previo.

Estas comprobaciones no reemplazan probar los mapas finales ni una build.
La geometría de navegación de esas escenas puede cambiar el comportamiento.