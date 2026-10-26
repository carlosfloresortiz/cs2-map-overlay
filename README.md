# Map Overlay

Mod de código para Cities: Skylines II (interfaz en inglés). Muestra OpenStreetMap sobre el terreno a escala real (1 m del mundo real = 1 unidad del juego), como papel cebolla, para calcar trazados de ciudades reales. No afecta a la partida guardada.

## Uso (0.1.1)

El botón del mod está en la barra superior izquierda de la partida. Abre un panel con:

- **Mostrar mapa** y **Opacidad**.
- **Búsqueda**: sugerencias mientras escribes (Photon); Enter busca con Nominatim y, si no encuentra nada, con Photon. También acepta coordenadas (`19.4326, -99.1332`) o un enlace de Google Maps u OpenStreetMap. El lugar elegido aparece donde mira la cámara.
- **Mover mapa**: las flechas desplazan el mapa según la vista de la cámara, con pasos de 1, 5, 25 o 100 m. ◎ trae el centro del mapa al punto que mira la cámara.
- **Rotación**: gira el mapa alrededor del punto que mira la cámara.
- **Estilo**: OpenStreetMap, CARTO claro, CARTO claro sin etiquetas (el más limpio para calcar), CARTO Voyager, OpenTopoMap o una URL propia (en Opciones).
- **Detalle**: nivel de zoom de las teselas (14–19). Solo cambia la nitidez; la escala siempre es 1:1.
- **Área**: lado del cuadrado de mapa que se carga (1–10 km).
- **Encima de todo**: dibuja el mapa sobre edificios y relieve. Desactívalo para que quede pegado al terreno.
- **Cargar donde miras**: al mover la cámara lejos del centro, carga la zona nueva sin desplazar el mapa.

En **Opciones → Map Overlay** están los mismos ajustes, la altura sobre el terreno, la URL personalizada y el botón para borrar la caché.

Las teselas se guardan en `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\ModsData\MapOverlay\tiles` y se vuelven a descargar a los 30 días.

Log: `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Logs\MapOverlay.log`

## Compilar e instalar

Cierra el juego y ejecuta:

```powershell
cd MapOverlay
dotnet build -c Release
```

La compilación hace tres cosas:

1. Compila la interfaz (`UI`, React) con `npm run build` en `UI\build`. Si falta `node_modules`, primero ejecuta `npm install`.
2. Compila la DLL.
3. Copia todo a `%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\MapOverlay`.

## Cómo funciona

- `GeoMath` convierte entre teselas Web Mercator y un plano local este/norte en metros reales alrededor del centro del mapa (radios WGS84), así que la escala es 1:1 en cualquier latitud. `MapFrame` coloca ese plano en el mundo: un punto de anclaje (X, Z) y una rotación.
- `OverlayController` crea una malla de 16×16 celdas por tesela que sigue la altura del terreno (`TerrainSystem` y `TerrainUtils.SampleHeight`). Las alturas se recalculan poco a poco en cada fotograma, así que el mapa sigue al terreno si lo modificas.
- Cada tesela usa `HDRP/Unlit`. El juego no incluye la variante transparente de ese shader, así que la opacidad se hace en dos pasadas que no leen alfa: una oscurece el fondo (`dst * (1 - a)`) y otra suma el mapa (`+ mapa * a`). «Encima de todo» cambia la prueba de profundidad a `Always`.
- Las descargas usan `UnityWebRequest`, con 2 conexiones como máximo y una pausa de 60 s si el servidor responde 403 o 429.
- `MapOverlayUISystem` publica el estado para el panel con los bindings del grupo `mapOverlay`.

## Condiciones de uso de los mapas

- Los datos son © colaboradores de OpenStreetMap (ODbL). El panel muestra la atribución de cada estilo.
- Los servidores de teselas de openstreetmap.org son para uso moderado y piden permiso para apps distribuidas con uso intensivo. Para uso personal no hay problema; antes de publicar el mod en Paradox Mods conviene usar otro estilo por defecto o un servidor propio.
- Nominatim admite como máximo una petición por segundo y no permite autocompletar; por eso las sugerencias usan Photon (komoot), con una pausa de 350 ms al escribir.
