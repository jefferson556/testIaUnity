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

    [Header("Cuevas")]
    [Tooltip("Permite habilitar o deshabilitar atajos de viaje rápido adicionales.")]
    public bool enableTravelCaves = true;

    [Tooltip("Distancia de ruta mínima entre cuevas de viaje rápido.")]
    public int minimumPathDistanceBetweenTravelCaves = 10;

    [Tooltip("Ahorro de camino mínimo requerido para crear un atajo de viaje rápido.")]
    public int minimumShortcutSaving = 8;

    [Tooltip("Cantidad de parejas de cuevas de viaje rápido (para soporte futuro).")]
    public int travelCavePairs = 1;

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

    [Tooltip("Cantidad de pistas disponibles.")]
    public int hintsAvailable = 3;

    [Tooltip("Tiempo en segundos antes de mostrar una pista.")]
    public float hintDelaySeconds = 15f;

    [Tooltip("Intensidad de las pistas (opaco, brillo, etc.).")]
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
