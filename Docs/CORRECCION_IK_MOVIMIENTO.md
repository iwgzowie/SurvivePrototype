# Corrección de foot IK y temblor de movimiento

Fecha: 11/09/2026. Unity 6000.3.5f2. Jugador UPPPlayer.

## Cambios

El movimiento escribía la rotación del Transform desde Update sobre un
Rigidbody interpolado. La rotación ahora usa MoveRotation en el paso de física,
junto con las consultas de suelo/pared/escalones y la cápsula. Escalones y
pendientes usan MovePosition. El input permanece en Update. La cámara sigue
la pose final en LateUpdate y usa el tiempo de ese frame. El prefab usa
Animator Normal y mantiene Rigidbody Interpolate.

El IK tomaba elevación del frame anterior y ajustaba la altura según los ejes
locales de cada pie. Con este avatar, sobre el mismo plano dejaba un pie en
Y=0,035 m y el otro en Y=0,211 m. Ahora toma la pose animada de la evaluación
actual, resuelve cada contacto por separado, descarta colliders propios y
superficies fuera de alcance, y conserva la zancada. La pelvis suaviza sólo el
desnivel relativo. La corrección de pendiente inicia con rotación identidad.

FootHeightMultiplier queda en 1 en el prefab. FootHeight sigue configurable;
la altura mínima también respeta los datos de planta del Avatar. La columna
conserva su lean sin invalidar los apoyos de las piernas.

## Evidencia

Comparación con el prefab real, física dinámica, cámara real y paso de física
de 0,02 s. Diez CSV completos: cinco variantes antes y cinco después. No hubo
saltos de frameCount en las fases analizadas. El error RMS compara el avance
horizontal renderizado con la velocidad física integrada usando el tiempo real.

| Recorrido | FPS observados después | Error RMS antes | Error RMS después |
| --- | ---: | ---: | ---: |
| Caminar, objetivo 60 | 60,00 | 13,298 mm | <0,002 mm |
| Correr, objetivo 60 | 60,00 | 42,176 mm | <0,002 mm |
| Caminar, objetivo 120 | 119,98 | 14,047 mm | <0,002 mm |
| Correr, objetivo 120 | 120,00 | 1,406 mm | <0,002 mm |

La variación de velocidad relativa a cámara se redujo entre 85,8 % y 97,4 %.
Esta medida incluye seguimiento intencional además de vibración. Los residuos
finales de la raíz están cerca de la precisión numérica; no representan una
precisión física real de micrómetros. Deshabilitar sólo la rotación en el
baseline también eliminaba el error de raíz; apagar IK o cambiar solamente
Animator a Normal no lo resolvía.

Las comprobaciones de los diez casos aislados de IK pasaron: plano, giro de
90 grados, pendiente de 20 grados, apoyo de un solo pie, collider propio,
peso cero, componente deshabilitado, salto, superficie demasiado alta y
apagado después de activar. Ambos pies quedan en Y=0,100 m sobre plano.
Con apoyo exclusivo izquierdo a Y=0,100 m, ese pie sube a Y=0,200 m y el
derecho conserva su pose animada.

También pasaron seis casos con movimiento y física activos: giro de 90 grados,
aim/strafe con arma equipada y disparo aceptado, salto/aterrizaje,
crouch/prone/roll y retorno, escalón de 20 cm y rampa de 15 grados.
Se inspeccionaron capturas reales de pies en plano/rampa y aim con arma.

## Límites de la validación

Las ejecuciones completaron los escenarios, pero sus resultados globales
conservan success=false por una SocketException de System.Net.WebConnection.
Los checks funcionales individuales pasaron; no se presenta el proceso global
como libre de errores. El stack de red no identifica por sí solo al servicio
que intentó conectar. Los logs completos y CSV permanecen fuera del repositorio.

Se usaron entradas programáticas y escenas temporales con el prefab real.
Esto no reemplaza una revisión manual del feel en Playground ni valida todos
los mapas, un ejecutable empaquetado o la política opcional de root motion.
La prueba de disparo confirma aceptación de la ruta, no repite la validación
previa de daño contra enemigos. Las comprobaciones temporales se retiraron y
los archivos de paquetes se restauraron a su contenido original.
