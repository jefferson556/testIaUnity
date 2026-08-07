using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
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

    private Rigidbody2D rb;
    private Vector3 initialPosition;
    private Transform goalTransform;
    private float previousDistanceToGoal;

    private CatMovement humanMovement;
    private CatInputReader humanInput;
    private CatInventory inventory;
    private Transform keyTransform;
    private float previousDistanceToKey;
    private Transform caveATransform;
    private Transform caveBTransform;
    private float previousDistanceToAxe;
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
        MaxStep = maxStepsPerEpisode;

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
        AddReward(0.25f); // Recompensa por tomar el atajo automático de la cueva
        Debug.Log("[MazeAgent] 🌀 ¡Teletransporte automático por cueva completado!");
    }

    private void DisableHumanControls()
    {
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

    public override void OnEpisodeBegin()
    {
        DisableHumanControls();

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

        // Entregar el hacha de entrenamiento después de que la generación finalice (evita race conditions con DynamicLevelManager)
        if (inventory != null)
        {
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

        if (caveATransform != null)
        {
            previousDistanceToAxe = Vector3.Distance(transform.position, caveATransform.position);
        }

        // Reiniciar velocidad
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        IsGenerating = false;
        Debug.Log("[MazeAgent] 🗺️ ¡Laberinto regenerado y configurado correctamente para el nuevo episodio!");
    }

    private void FindGoalTransform()
    {
        goalTransform = null;
        Transform root = transform.parent != null ? transform.parent : transform;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != transform && (child.name.Contains("Goal") || child.name.Contains("Meta") || child.name.Contains("Door") || child.CompareTag("Goal")))
            {
                goalTransform = child;
                break;
            }
        }
        if (goalTransform == null)
        {
            GameObject goalObj = GameObject.FindWithTag("Goal");
            if (goalObj != null) goalTransform = goalObj.transform;
        }
    }

    private void FindKeyTransform()
    {
        keyTransform = null;
        Transform root = transform.parent != null ? transform.parent : transform;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != transform && (child.name.Contains("Key") || child.name.Contains("Llave")))
            {
                keyTransform = child;
                break;
            }
        }
        if (keyTransform == null)
        {
            GameObject keyObj = GameObject.Find("Mission_Key");
            if (keyObj != null) keyTransform = keyObj.transform;
        }
    }

    private void FindCaveTransforms()
    {
        caveATransform = null;
        caveBTransform = null;
        Transform root = transform.parent != null ? transform.parent : transform;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.Contains("Cave_A") || child.name.Contains("Cueva_A")) caveATransform = child;
            if (child.name.Contains("Cave_B") || child.name.Contains("Cueva_B")) caveBTransform = child;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (IsGenerating)
        {
            sensor.AddObservation(new float[25]);
            return;
        }

        // Posición relativa del agente (3 valores)
        sensor.AddObservation(transform.localPosition);

        // Meta: posición y dirección (6 valores)
        if (goalTransform != null)
        {
            sensor.AddObservation(goalTransform.localPosition);
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
        if (keyTransform != null && !hasKey)
        {
            sensor.AddObservation(keyTransform.localPosition);
            sensor.AddObservation((keyTransform.position - transform.position).normalized);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(Vector3.zero);
        }

        // Cueva relevante: posición y dirección (6 valores)
        Transform relevantCave = null;
        if (!hasAxe && caveATransform != null)
        {
            relevantCave = caveATransform;
        }
        else if (hasAxe && caveBTransform != null)
        {
            relevantCave = caveBTransform;
        }

        if (relevantCave != null)
        {
            sensor.AddObservation(relevantCave.localPosition);
            sensor.AddObservation((relevantCave.position - transform.position).normalized);
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
                if (dir != Vector2.zero)
                {
                    rb.MovePosition(rb.position + dir * moveSpeed * Time.fixedDeltaTime);
                }
            }
        }
        else
        {
            transform.Translate(dir * moveSpeed * Time.deltaTime);
        }

        // Recompensa de proximidad secuencial de 3 etapas:
        // Etapa 0: Guiarse a Cueva A / Hacha si no la posee
        // Etapa 1: Guiarse a la Llave si posee Hacha pero no posee Llave
        // Etapa 2: Guiarse a la Meta si posee Hacha y Llave
        bool hasKey = inventory != null && inventory.HasKey;
        bool hasAxe = inventory != null && inventory.HasAxe;

        Transform targetTransform = null;
        float refDist = 0f;
        int targetStage = 0; // 0=Axe/CaveA, 1=Key, 2=Goal

        if (!hasAxe && caveATransform != null)
        {
            targetTransform = caveATransform;
            refDist = previousDistanceToAxe;
            targetStage = 0;
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

            AddReward(distDiff * 0.1f);

            if (targetStage == 0) previousDistanceToAxe = currentDist;
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

    private void ExecuteInteractAction()
    {
        // Accionar hacha al presionar el botón de interacción (tecla E / Rama 1)
        var breaker = GetComponent<AxeObstacleBreaker>();
        if (breaker == null) breaker = GetComponentInChildren<AxeObstacleBreaker>();
        if (breaker != null)
        {
            bool broke = breaker.TryBreakObstacleInDirection(currentMoveDirection);
            if (broke)
            {
                Debug.Log($"[IA Botón E] 🪓 ¡Impacto de hacha exitoso en dirección {currentMoveDirection}!");
                AddReward(0.08f);
            }
        }
    }
}
