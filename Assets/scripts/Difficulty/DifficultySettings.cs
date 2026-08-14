using UnityEngine;

[System.Serializable]
public class DifficultySettings
{
    [Header("Dimensiones y Complejidad")]
    [Tooltip("Ancho del mapa.")]
    public int mapWidth = 15;

    [Tooltip("Alto del mapa.")]
    public int mapHeight = 15;

    [Tooltip("Cantidad de caminos alternativos. A mayor valor, laberinto más fácil y menos lineal.")]
    public int extraConnections = 2;

    [Header("Tiempo Límite (Gameplay)")]
    [Tooltip("Tiempo máximo en segundos antes de reiniciar el mapa si no se alcanza la meta. (Solo afecta al modo jugador)")]
    public float maxTimeLimitInSeconds = 500f;

    [Header("Distancias y Posiciones")]
    [Tooltip("Distancia mínima entre el jugador y Cueva A.")]
    public float minPlayerToCaveADistance = 2f;

    [Tooltip("Distancia mínima del hacha respecto al inicio y a la meta (para obligar el uso de cuevas).")]
    public float minAxeToStartAndMetaDistance = 8f;

    [Tooltip("Distancia mínima entre el hacha y la llave.")]
    public float minKeyToAxeDistance = 4f;

    [Tooltip("Distancia mínima entre la llave y la casa.")]
    public float minKeyToMetaDistance = 4f;

    [Tooltip("Distancia mínima entre el jugador y la casa/meta.")]
    public float minPlayerToMetaDistance = 8f;

    [Header("Cuevas Opcionales de Viaje Rápido")]
    [Tooltip("Activa o desactiva la generación de cuevas opcionales de atajo.")]
    public bool enableTravelCaves = true;

    [Tooltip("Número máximo de parejas de cuevas opcionales a generar. La cantidad real depende del tamaño del mapa.")]
    public int maximumTravelCavePairs = 1;

    [Tooltip("Ancho mínimo del mapa para habilitar cuevas opcionales.")]
    public int minimumMapWidthForTravelCaves = 10;

    [Tooltip("Alto mínimo del mapa para habilitar cuevas opcionales.")]
    public int minimumMapHeightForTravelCaves = 10;

    [Tooltip("Celdas transitables mínimas en la región principal para generar 1 pareja.")]
    public int minWalkableCellsForOnePair = 40;

    [Tooltip("Celdas transitables mínimas para generar 2 parejas.")]
    public int minWalkableCellsForTwoPairs = 90;

    [Tooltip("Celdas transitables mínimas para generar 3 o más parejas.")]
    public int minWalkableCellsForThreePairs = 150;

    [Tooltip("Distancia mínima en pasos de celda entre las entradas A y B de una pareja.")]
    public int minimumPathDistanceBetweenTravelCaves = 6;

    [Tooltip("Ahorro mínimo requerido (NormalPathDistance - TeleportCost) para crear una pareja.")]
    public int minimumShortcutSaving = 4;

    [Tooltip("Costo de usar un portal en unidades del pathfinder. Si es igual al costo de un paso normal (1), se usa BFS; si difiere, se usa Dijkstra.")]
    public float teleportCost = 3f;

    [Tooltip("Tamaño de la zona de acceso al hacha (ancho y alto).")]
    public Vector2Int axeZoneSize = new Vector2Int(3, 3);

    [Header("Destructibles")]
    [Tooltip("Porcentaje de paredes internas que se convierten en destructibles decorativos (0.0 a 1.0).")]
    [Range(0f, 1f)]
    public float destructibleWallsPercentage = 0.10f;

    [Tooltip("Vida de los destructibles de progresión (barreras).")]
    [Min(1)]
    public int missionDestructiblesHealth = 1;

    [Tooltip("Activa la aparición de destructibles aleatorios en el laberinto.")]
    public bool spawnDestructibles = true;

    [Header("Jugador y Ayudas")]
    [Tooltip("Velocidad de movimiento del jugador.")]
    public float playerMoveSpeed = 4f;

    // [FUTURO / HINTS] Lógica de pistas reservada para cuando se implemente la mecánica visual de ayudas.
    [Tooltip("[FUTURO] Cantidad de pistas disponibles.")]
    public int hintsAvailable = 3;

    [Tooltip("[FUTURO] Tiempo en segundos antes de mostrar una pista.")]
    public float hintDelaySeconds = 15f;

    [Tooltip("[FUTURO] Intensidad de las pistas (opaco, brillo, etc.).")]
    [Range(0f, 1f)]
    public float hintIntensity = 1f;

    [Tooltip("Resalta el objetivo actual.")]
    public bool highlightObjectives = true;

    [Tooltip("Muestra una flecha o indicador de dirección hacia el objetivo.")]
    public bool showDirectionIndicator = true;

    [Header("Zoom de Cámara")]
    [Tooltip("Tiempo máximo permitido para ver el mapa alejado (en segundos).")]
    public float zoomOutMaxDuration = 4f;

    [Tooltip("Tiempo de enfriamiento antes de poder usar el zoom otra vez (en segundos).")]
    public float zoomOutCooldown = 3f;

    [Tooltip("Tamaño ortográfico de la cámara alejada.")]
    public float zoomOutSize = 9f;

    [Tooltip("Tamaño ortográfico de la cámara normal.")]
    public float normalZoomSize = 4f;

    public DifficultySettings Clone()
    {
        return (DifficultySettings)this.MemberwiseClone();
    }
}
