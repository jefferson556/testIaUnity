using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Agente de ML-Agents para controlar la navegación del gato dentro del laberinto.
/// Soporta observaciones vectoriales, sensores Raycast y Heurística manual.
/// </summary>
[AddComponentMenu("Training/Maze Agent")]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(RayPerceptionSensorComponent2D))]
public class MazeAgent : Agent
{
    [Header("Referencias del Laberinto")]
    [SerializeField] private TrainingConfig trainingConfig;
    [SerializeField] private DynamicLevelManager levelManager;
    [SerializeField] private MazeGenerator mazeGenerator;
    [SerializeField] private MazeTilemapRenderer mazeRenderer;
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private int maxStepsPerEpisode = 9000;

    [Header("Configuración de Recompensas")]
    [SerializeField] private float goalReward = 2.0f;
    [SerializeField] private float wallPenalty = -0.01f;
    [SerializeField] private float stepPenalty = -0.0005f;

    private int currentEpisodeStepCount = 0;
    public int CurrentEpisodeStepCount => currentEpisodeStepCount;

    private Rigidbody2D rb;
    private Vector3 initialPosition;
    private Transform goalTransform;
    private float previousDistanceToGoal;
    private float previousDistanceToKey;
    private float previousDistanceToCurrentTarget;
    private CatMovement humanMovement;
    private CatInputReader humanInput;
    private CatInventory inventory;
    private Transform keyTransform;
    private Transform caveATransform;
    private Transform caveBTransform;
    private Transform axeTransform;
    private float previousDistanceToAxe;
    private bool hasTraversedCaveA = false;
    private bool hasTraversedCaveB = false;
    private Vector2 currentMoveDirection = Vector2.down;

    public bool IsGenerating { get; private set; } = false;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody2D>();
        humanMovement = GetComponent<CatMovement>();
        humanInput = GetComponent<CatInputReader>();

        if (trainingConfig == null)
        {
            trainingConfig = Resources.Load<TrainingConfig>("TrainingConfig");
            if (trainingConfig == null)
            {
                var initializer = FindAnyObjectByType<TrainingModeInitializer>();
                if (initializer != null) trainingConfig = initializer.Config;
            }
        }

        if (trainingConfig == null)
        {
            Debug.LogError("[MazeAgent] ¡ERROR CRÍTICO! No se pudo cargar el TrainingConfig.");
        }
        else
        {
            Debug.Log($"[MazeAgent] TrainingConfig cargado. Modo entrenamiento: {trainingConfig.trainingMode}, Empezar con hacha: {trainingConfig.startWithAxe}");
        }

        // Configurar tamaño de observaciones (25) y 2 ramas discretas de acción (5 de movimiento, 2 de botón E)
        var bp = GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
        if (bp != null)
        {
            bp.BrainParameters.VectorObservationSize = 25;
            bp.BrainParameters.ActionSpec = Unity.MLAgents.Actuators.ActionSpec.MakeDiscrete(5, 2);
        }

        // Sincronizar MaxStep nativo de ML-Agents con maxStepsPerEpisode
        if (maxStepsPerEpisode > 0)
        {
            MaxStep = maxStepsPerEpisode;
        }

        // 1. CORRECCIÓN DE CEGUERA: Asegurar que el sensor pueda ver destructibles y cuevas
        var raySensor = GetComponent<Unity.MLAgents.Sensors.RayPerceptionSensorComponent2D>();
        if (raySensor != null && raySensor.DetectableTags != null)
        {
            string[] requiredTags = new string[] { "Desctruct", "Destruct", "Cave", "Portal", "Cueva", "TravelCave" };
            foreach (var tag in requiredTags)
            {
                if (!raySensor.DetectableTags.Contains(tag))
                {
                    raySensor.DetectableTags.Add(tag);
                }
            }
            Debug.Log($"[MazeAgent] Etiquetas inyectadas en RaySensor de {gameObject.name}. Total detectables: {raySensor.DetectableTags.Count}");
        }

        // Forzando penalización de tiempo más fuerte ignorando el inspector
        stepPenalty = -0.002f;

        inventory = GetComponent<CatInventory>();
        if (inventory == null) inventory = GetComponentInParent<CatInventory>();
        if (inventory != null)
        {
            inventory.OnKeyCollected -= HandleKeyCollected;
            inventory.OnKeyCollected += HandleKeyCollected;
        }

        var traveler = GetComponent<CaveTraveler>();
        if (traveler == null) traveler = GetComponentInParent<CaveTraveler>();
        if (traveler != null)
        {
            traveler.OnTeleport -= HandleTeleport;
            traveler.OnTeleport += HandleTeleport;
        }

        DisableHumanControls();

        // Buscar primero en el contenedor local (TrainingArea) para entrenamiento paralelo
        if (levelManager == null && transform.parent != null)
            levelManager = transform.parent.GetComponentInChildren<DynamicLevelManager>();
        if (levelManager == null) levelManager = FindAnyObjectByType<DynamicLevelManager>();

        if (mazeGenerator == null && transform.parent != null)
            mazeGenerator = transform.parent.GetComponentInChildren<MazeGenerator>();
        if (mazeGenerator == null) mazeGenerator = FindAnyObjectByType<MazeGenerator>();

        if (mazeRenderer == null && transform.parent != null)
            mazeRenderer = transform.parent.GetComponentInChildren<MazeTilemapRenderer>();
        if (mazeRenderer == null) mazeRenderer = FindAnyObjectByType<MazeTilemapRenderer>();

        initialPosition = transform.position;
    }

    private void HandleKeyCollected()
    {
        AddReward(1.0f); // Recompensa intermedia por obtener la llave
        Debug.Log("[MazeAgent] 🗝️ ¡Recompensa otorgada por obtener la Llave!");
    }

    private void HandleTeleport()
    {
        bool hasAxe = inventory != null && inventory.HasAxe;

        if (!hasAxe)
        {
            hasTraversedCaveA = true;
            AddReward(0.5f); // Recompensa por tomar la Cueva A para entrar al recinto del hacha
            Debug.Log("[MazeAgent] 🌀 Teletransporte por Cueva_A_Entrance completado (Entrada a zona del hacha)!");

            if (axeTransform != null)
            {
                previousDistanceToCurrentTarget = Vector3.Distance(transform.position, axeTransform.position);
            }
        }
        else
        {
            hasTraversedCaveB = true;
            AddReward(0.5f); // Recompensa por tomar la Cueva B para salir del recinto del hacha
            Debug.Log("[MazeAgent] 🌀 Teletransporte por Cueva_B_Exit completado (Salida de zona del hacha)!");

            if (keyTransform != null)
            {
                previousDistanceToKey = Vector3.Distance(transform.position, keyTransform.position);
            }
        }
    }

    private void DisableHumanControls()
    {
        if (trainingConfig == null || !trainingConfig.trainingMode)
        {
            return;
        }

        if (humanMovement == null) humanMovement = GetComponent<CatMovement>();
        if (humanMovement != null && humanMovement.enabled)
        {
            humanMovement.enabled = false;
        }

        if (humanInput == null) humanInput = GetComponent<CatInputReader>();
        if (humanInput != null && humanInput.enabled)
        {
            humanInput.enabled = false;
        }
    }

    private void FixedUpdate()
    {
        DisableHumanControls();

        if (IsGenerating) return;

        // Si no hay un DecisionRequester en el GameObject, solicitar decisiones manualmente cada 5 pasos
        if (GetComponent<DecisionRequester>() == null)
        {
            if (StepCount % 5 == 0)
            {
                RequestDecision();
            }
        }
    }

    public void TriggerTimeoutFailure()
    {
        AddReward(-1.0f); // Penalización severa por no llegar a tiempo
        Debug.Log("[MazeAgent] ⏱️ IA se quedó sin tiempo en el nivel.");
        if (DifficultyMetricsCollector.Instance != null && DifficultyMetricsCollector.Instance.IsCollecting)
        {
            DifficultyMetricsCollector.Instance.SetTerminationReason("TIMEOUT");
        }
        EndEpisode();
    }

    public override void OnEpisodeBegin()
    {
        // Si el episodio comienza de nuevo pero estábamos recolectando datos, y el contador local > 0, 
        // significa que Unity ML-Agents truncó el episodio internamente (ej. por MaxStep).
        if (DifficultyMetricsCollector.Instance != null && DifficultyMetricsCollector.Instance.IsCollecting)
        {
            if (currentEpisodeStepCount > 0)
            {
                DifficultyMetricsCollector.Instance.SetTerminationReason("TIMEOUT");
                DifficultyMetricsCollector.Instance.OnLevelEnded(false);
            }
        }

        currentEpisodeStepCount = 0;

        DisableHumanControls();
        hasTraversedCaveA = false;
        hasTraversedCaveB = false;

        IsGenerating = true;

        if (inventory != null)
        {
            inventory.ResetInventory();
        }

        // Detener velocidad residual
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // Solicitar nueva generación de mapa en cada episodio
        if (levelManager == null && transform.parent != null)
            levelManager = transform.parent.GetComponentInChildren<DynamicLevelManager>();
        if (levelManager == null) levelManager = FindAnyObjectByType<DynamicLevelManager>();
        if (levelManager != null)
        {
            levelManager.StartGeneration();
        }
    }

    public void OnGenerationFinished()
    {
        // Buscar referencias de meta, llave y cuevas en la escena ya generada
        FindGoalTransform();
        FindKeyTransform();
        FindCaveTransforms();
        FindAxeTransform();

        // Entregar el hacha de entrenamiento después de que la generación finalice (evita race conditions con DynamicLevelManager)
        if (inventory != null)
        {
            inventory.OnAxeCollected -= HandleAxeCollected;
            inventory.OnAxeCollected += HandleAxeCollected;
            inventory.ResetInventory();
            if (trainingConfig != null && trainingConfig.startWithAxe)
            {
                inventory.CollectAxe();
            }
        }

        // Reposicionar al agente en la celda de inicio
        if (transform.parent != null)
        {
            mazeGenerator = transform.parent.GetComponentInChildren<MazeGenerator>();
            mazeRenderer = transform.parent.GetComponentInChildren<MazeTilemapRenderer>();
        }
        if (mazeGenerator == null) mazeGenerator = FindAnyObjectByType<MazeGenerator>();
        if (mazeRenderer == null) mazeRenderer = FindAnyObjectByType<MazeTilemapRenderer>();

        if (mazeGenerator != null && mazeRenderer != null)
        {
            Vector2Int startCell = mazeGenerator.StartCell;
            transform.position = mazeRenderer.GetWorldPosition(startCell);
        }
        else if (initialPosition != Vector3.zero)
        {
            transform.position = initialPosition;
        }

        if (goalTransform != null)
        {
            previousDistanceToGoal = Vector3.Distance(transform.position, goalTransform.position);
        }

        if (keyTransform != null)
        {
            previousDistanceToKey = Vector3.Distance(transform.position, keyTransform.position);
        }

        if (!hasTraversedCaveA && caveATransform != null)
        {
            previousDistanceToCurrentTarget = Vector3.Distance(transform.position, caveATransform.position);
        }
        else if (axeTransform != null)
        {
            previousDistanceToCurrentTarget = Vector3.Distance(transform.position, axeTransform.position);
        }

        // Reiniciar velocidad
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // Reiniciar estado de animación a Reposo (Cat_Idle)
        var anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.Play("Cat_Idle", 0, 0f);
        }

        IsGenerating = false;
        Debug.Log("[MazeAgent] 🗺️ ¡Laberinto regenerado y configurado correctamente para el nuevo episodio!");
    }

    private void FindGoalTransform()
    {
        goalTransform = null;
        Transform root = (levelManager != null) ? levelManager.transform : (transform.parent != null ? transform.parent : transform);
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != transform && (child.name.Contains("Goal") || child.name.Contains("Meta") || child.name.Contains("Door") || child.CompareTag("Goal")))
            {
                goalTransform = child;
                break;
            }
        }
    }

    private void FindKeyTransform()
    {
        keyTransform = null;
        Transform root = (levelManager != null) ? levelManager.transform : (transform.parent != null ? transform.parent : transform);
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != transform && (child.name.Contains("Key") || child.name.Contains("Llave")))
            {
                keyTransform = child;
                break;
            }
        }
    }

    private void FindCaveTransforms()
    {
        caveATransform = null;
        caveBTransform = null;
        Transform root = (levelManager != null) ? levelManager.transform : (transform.parent != null ? transform.parent : transform);
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != transform)
            {
                if (child.name == "Cave_A_Entrance" || (caveATransform == null && (child.name.Contains("Cave_A_Entrance") || child.name.Contains("Cueva_A"))))
                {
                    caveATransform = child;
                }
                if (child.name == "Cave_B_Exit" || (caveBTransform == null && (child.name.Contains("Cave_B_Exit") || child.name.Contains("Cueva_B"))))
                {
                    caveBTransform = child;
                }
            }
        }
    }

    private void FindAxeTransform()
    {
        axeTransform = null;
        Transform root = (levelManager != null) ? levelManager.transform : (transform.parent != null ? transform.parent : transform);
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != transform && (child.name.Contains("Axe") || child.name.Contains("Hacha") || child.name.Contains("Mission_Axe")))
            {
                axeTransform = child;
                break;
            }
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (IsGenerating)
        {
            sensor.AddObservation(new float[25]);
            return;
        }

        Transform refRoot = transform.parent != null ? transform.parent : transform;

        // Posición relativa del agente (3 valores)
        sensor.AddObservation(refRoot.InverseTransformPoint(transform.position));

        // Meta: posición y dirección (6 valores)
        if (goalTransform != null)
        {
            sensor.AddObservation(refRoot.InverseTransformPoint(goalTransform.position));
            sensor.AddObservation((goalTransform.position - transform.position).normalized);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(Vector3.zero);
        }

        // Velocidad actual del agente (2 valores)
        if (rb != null)
        {
            sensor.AddObservation(rb.linearVelocity);
        }
        else
        {
            sensor.AddObservation(Vector2.zero);
        }

        // Estado del Inventario (2 valores)
        bool hasKey = inventory != null && inventory.HasKey;
        bool hasAxe = inventory != null && inventory.HasAxe;
        sensor.AddObservation(hasKey ? 1f : 0f);
        sensor.AddObservation(hasAxe ? 1f : 0f);
        // Llave: posición y dirección (6 valores)
        // Ocultar la llave si no tiene el hacha O si todavía está en la zona aislada antes de tomar Cueva B
        // (Salvo que empiece con el hacha, en cuyo caso no spawnea en la zona aislada y puede ir a la llave directo)
        bool startWithAxe = trainingConfig != null && trainingConfig.startWithAxe;
        bool readyForKey = hasAxe && (hasTraversedCaveB || caveBTransform == null || startWithAxe);
        
        if (keyTransform != null && !hasKey && readyForKey)
        {
            sensor.AddObservation(refRoot.InverseTransformPoint(keyTransform.position));
            sensor.AddObservation((keyTransform.position - transform.position).normalized);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(Vector3.zero);
        }

        Transform relevantTarget = null;
        if (!hasAxe)
        {
            if (!hasTraversedCaveA && caveATransform != null)
            {
                relevantTarget = caveATransform;
            }
            else if (axeTransform != null)
            {
                relevantTarget = axeTransform;
            }
        }
        else
        {
            if (!hasTraversedCaveB && caveBTransform != null && !startWithAxe)
            {
                relevantTarget = caveBTransform;
            }
        }

        if (relevantTarget != null)
        {
            sensor.AddObservation(refRoot.InverseTransformPoint(relevantTarget.position));
            sensor.AddObservation((relevantTarget.position - transform.position).normalized);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(Vector3.zero);
        }

        // Total Observaciones = 3 + 6 + 2 + 2 + 6 + 6 = 25
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (IsGenerating) return;

        currentEpisodeStepCount++;

        DisableHumanControls();

        // Penalización continua pequeña por cada paso (incentiva llegar rápido)
        AddReward(stepPenalty);

        int moveAction = actions.DiscreteActions[0];
        int interactAction = actions.DiscreteActions.Length > 1 ? actions.DiscreteActions[1] : 0;

        Vector2 dir = Vector2.zero;

        switch (moveAction)
        {
            case 1: dir = Vector2.up; break;
            case 2: dir = Vector2.down; break;
            case 3: dir = Vector2.left; break;
            case 4: dir = Vector2.right; break;
        }

        if (dir != Vector2.zero)
        {
            currentMoveDirection = dir;
            if (humanMovement != null)
            {
                humanMovement.FacingDirection = dir;
            }
        }

        // Si la IA decide accionar el botón de interacción (tecla E / Rama 1)
        if (interactAction == 1)
        {
            // Debug temporal para saber si la IA intenta usar el botón de interactuar
            Debug.Log($"[MazeAgent] IA accionó el botón E en dirección {currentMoveDirection}");
            ExecuteInteractAction();
        }

        if (rb != null)
        {
            if (rb.bodyType == RigidbodyType2D.Kinematic)
            {
                rb.MovePosition(rb.position + dir * moveSpeed * Time.fixedDeltaTime);
            }
            else
            {
                rb.linearVelocity = dir * moveSpeed;
            }
        }
        else
        {
            transform.Translate(dir * moveSpeed * Time.deltaTime);
        }

        // Recompensa de proximidad secuencial de 4 etapas:
        // Etapa 0: Guiarse a Cueva A / Hacha si no posee el hacha
        // Etapa 1: Guiarse a Cueva B (salida de zona hacha) tras tomar el hacha
        // Etapa 2: Guiarse a la Llave tras salir de la zona del hacha
        // Etapa 3: Guiarse a la Meta tras tomar la Llave
        bool hasKey = inventory != null && inventory.HasKey;
        bool hasAxe = inventory != null && inventory.HasAxe;

        Transform targetTransform = null;
        float refDist = 0f;
        int targetStage = 0; // 0=Axe/CaveA, 1=CaveB/Key, 2=Goal

        bool startWithAxe = trainingConfig != null && trainingConfig.startWithAxe;

        if (!hasAxe)
        {
            if (!hasTraversedCaveA && caveATransform != null)
            {
                targetTransform = caveATransform;
                refDist = previousDistanceToCurrentTarget;
                targetStage = 0;
            }
            else if (axeTransform != null)
            {
                targetTransform = axeTransform;
                refDist = previousDistanceToCurrentTarget;
                targetStage = 0;
            }
        }
        else if (!hasTraversedCaveB && caveBTransform != null && !startWithAxe)
        {
            targetTransform = caveBTransform;
            refDist = previousDistanceToKey;
            targetStage = 1;
        }
        else if (!hasKey && keyTransform != null)
        {
            targetTransform = keyTransform;
            refDist = previousDistanceToKey;
            targetStage = 1;
        }
        else if (goalTransform != null)
        {
            targetTransform = goalTransform;
            refDist = previousDistanceToGoal;
            targetStage = 2;
        }

        if (targetTransform != null)
        {
            float currentDist = Vector3.Distance(transform.position, targetTransform.position);
            float distDiff = refDist - currentDist;
            // Se restaura la recompensa Euclidiana pero atenuada, el Wall Fear fue eliminado
            // por lo que ahora el agente podrá rodear paredes usando el step penalty como guía.
            AddReward(distDiff * 0.02f);

            if (targetStage == 0) previousDistanceToCurrentTarget = currentDist;
            else if (targetStage == 1) previousDistanceToKey = currentDist;
            else previousDistanceToGoal = currentDist;
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = 0;

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) discreteActions[0] = 1;
            else if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) discreteActions[0] = 2;
            else if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) discreteActions[0] = 3;
            else if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) discreteActions[0] = 4;
        }

        if (discreteActions.Length > 1)
        {
            bool pressE = keyboard != null && (keyboard.eKey.isPressed || keyboard.spaceKey.isPressed);
            discreteActions[1] = pressE ? 1 : 0;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Detectar recolección de Llave
        if (collision.name.Contains("Key") || collision.name.Contains("Llave"))
        {
            if (inventory != null && !inventory.HasKey)
            {
                inventory.CollectKey();
            }
        }

        // Detectar llegada a la Meta
        if (collision.CompareTag("Goal") || collision.name.Contains("Goal") || collision.name.Contains("Meta") || collision.name.Contains("Door"))
        {
            if (inventory != null && inventory.HasKey)
            {
                AddReward(goalReward);

                if (DifficultyMetricsCollector.Instance != null && DifficultyMetricsCollector.Instance.IsCollecting)
                {
                    DifficultyMetricsCollector.Instance.SetTerminationReason("GOAL");
                    DifficultyMetricsCollector.Instance.OnLevelEnded(true);
                }

                EndEpisode();
            }
            else
            {
                AddReward(-0.02f); // Pequeño castigo por intentar abrir la puerta sin llave
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Debug.Log($"[IA Colisión] 💥 El gato chocó con: '{collision.gameObject.name}' | Tag: '{collision.gameObject.tag}' | Capa: '{LayerMask.LayerToName(collision.gameObject.layer)}'");

        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.name.Contains("Wall"))
        {
            AddReward(wallPenalty);
        }
    }

    // ELIMINADO: OnCollisionStay2D. Castigaba severamente (-0.005 por tick) el simple acto de deslizarse
    // por las paredes. Esto causaba el "Wall Fear" (el agente daba vueltas para no tocar nada).

    private void ExecuteInteractAction()
    {
        bool hasAxe = inventory != null && inventory.HasAxe;

        // Si la IA intenta presionar E/Ataque sin tener el hacha:
        if (!hasAxe)
        {
            // Penalizar a la IA para que aprenda a NO presionar el botón de ataque hasta recoger el hacha
            // ELIMINADO: El castigo masivo (-0.03f) provocaba que la red neuronal generara trauma
            // y nunca volviera a explorar este botón, ni siquiera tras conseguir el hacha.
            return;
        }

        var breaker = GetComponent<AxeObstacleBreaker>();
        if (breaker == null) breaker = GetComponentInChildren<AxeObstacleBreaker>();
        if (breaker != null)
        {
            bool broke = breaker.TryBreakObstacleInDirection(currentMoveDirection);
            if (broke)
            {
                Debug.Log($"[IA Botón E] 🪓 ¡Impacto de hacha exitoso en dirección {currentMoveDirection}!");
                // Aumentado a 0.2f para dar un refuerzo positivo mucho más fuerte al lograrlo.
                AddReward(0.2f);
            }
            else
            {
                // Penalización para evitar que spamee el hacha al aire o contra paredes normales
                // ELIMINADO: Castigar el fallo desalienta a la IA a seguir intentando al principio.
                // Ya existe un stepPenalty que castiga el perder tiempo.
                // AddReward(-0.005f);
            }
        }
        else
        {
            Debug.Log("[MazeAgent] No se encontró el componente AxeObstacleBreaker.");
        }
    }

    private void HandleAxeCollected()
    {
        // Dar una gran recompensa cuando el agente recoge el hacha (solo si no empezó con ella)
        if (trainingConfig != null && !trainingConfig.startWithAxe)
        {
            AddReward(1.0f);
            Debug.Log("[MazeAgent] 🪓 ¡El agente encontró y recogió el hacha! (+1.0 recompensa)");
        }
    }
}
