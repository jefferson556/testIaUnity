## 🕹️ Tabla de Mapeo de Teclas y Funcionalidades

| Tecla / Control | Acción | Descripción | Escena / Contexto |
|---|---|---|---|
| `W` `A` `S` `D` o `Flechas` | Movimiento | Desplaza al personaje (Gato) en las 4 direcciones cardinales: arriba, abajo, izquierda y derecha. | Todas las escenas de laberinto |
| `E` | Interactuar | Permite entrar a las cuevas de viaje rápido y teletransportarse entre portales cuando el personaje se encuentra sobre ellas. | Tutorial y `MazeLevel_Procedural` |
| `SHIFT` (mantener) | Vista Panorámica / Zoom | Aleja temporalmente la cámara para visualizar una mayor parte del mapa. La duración y el tiempo de recarga dependen de la dificultad activa. | Tutorial y `MazeLevel_Procedural` |
| `Q` | Alternar modo Jugador / IA | Alterna en tiempo real entre el control manual del jugador y el control autónomo mediante el agente de Inteligencia Artificial entrenado con PPO. | `MazeLevel_Procedural` |
| `R` | Reintentar / Reiniciar | Reinicia inmediatamente el laberinto actual, cancela la partida en curso y genera una nueva instancia del nivel. | `MazeLevel_Procedural` |
| `Clic Izquierdo` | Navegación UI / Formularios | Permite interactuar con botones, formularios, registro de perfil, selección de usuario y otros elementos de la interfaz. | Menú Principal y HUD |

### 🎮 Resumen rápido de controles

```text
W / A / S / D  → Movimiento
Flechas        → Movimiento
E              → Usar cueva / Interactuar
SHIFT          → Vista panorámica
Q              → Alternar Jugador / IA
R              → Reiniciar nivel
Clic Izquierdo → Interfaz y formularios
```

 **Nota:** 
>La opción de alternar entre Jugador e IA está disponible únicamente en la escena principal procedural. En la escena de tutorial, el control permanece enfocado en el jugador.

> La opción para salir una ves se ingresa a llenar los datos y no se quiere llenar nada pq ya se tiene un perfil no se la agrego buscar el perfil de nuevo.
> Puede suceder que el mapa no se genere correctamente ya que existen mejoras que hacer al algoritmo de generacion automatica.en tal caso tendria que volver a reiniciar.
---
# 🧠 Laberinto Procedural Adaptativo con Unity ML-Agents

Proyecto desarrollado en **Unity** que combina generación procedural de laberintos, recopilación de métricas de juego y aprendizaje por refuerzo mediante **Unity ML-Agents**.

El objetivo del proyecto es experimentar con técnicas de inteligencia artificial para analizar el desempeño del jugador y utilizar esa información para modificar progresivamente características de la dificultad de los siguientes niveles.

El entorno también incluye un agente capaz de jugar automáticamente los laberintos generados, permitiendo generar sesiones y métricas sin depender exclusivamente de jugadores humanos.

---

## 🎮 Descripción del juego

El jugador controla un personaje dentro de un laberinto generado proceduralmente.

Para completar el nivel debe resolver una secuencia de objetivos:

1. Explorar el laberinto.
2. Encontrar una **cueva** que permite acceder a una nueva zona.
3. Encontrar y recoger el **hacha**.
4. Utilizar el hacha para destruir determinados obstáculos.
5. Encontrar la **llave**.
6. Llegar hasta la **meta** para completar el nivel.

Los elementos importantes del nivel, sus distancias y diferentes características del laberinto pueden variar entre partidas.

Esto permite generar distintos grados de dificultad sin tener que diseñar cada nivel manualmente.

---

# 🎯 Objetivo del proyecto

El proyecto investiga el uso de inteligencia artificial para crear un sistema de dificultad adaptable.

La idea general es utilizar el comportamiento observado durante una partida para determinar si el siguiente nivel debería:

* aumentar su dificultad;
* mantener la dificultad actual;
* reducir su dificultad.

Entre los parámetros que pueden modificarse se encuentran:

* tamaño del mapa;
* cantidad de conexiones adicionales del laberinto;
* distancias entre objetivos;
* distribución de obstáculos;
* características de las cuevas;
* configuración general del nivel.

La aplicación está pensada como un prototipo para estudiar cómo la adaptación automática de dificultad puede utilizarse en experiencias orientadas a ejercicios de resolución de problemas.

---

# 🤖 Inteligencia Artificial

El proyecto utiliza **Unity ML-Agents** y contiene dos conceptos principales de agentes.

## 1. MazeAgent

`MazeAgent` es un agente entrenado para jugar automáticamente el laberinto.

Su objetivo es aprender la misma secuencia de acciones que debe realizar un jugador:

```text
INICIO
   ↓
Buscar Cueva A
   ↓
Atravesar la cueva
   ↓
Buscar Hacha
   ↓
Regresar / continuar exploración
   ↓
Destruir obstáculos
   ↓
Buscar Llave
   ↓
Buscar Meta
   ↓
FIN DEL NIVEL
```

El agente recibe observaciones relacionadas con elementos como:

* posición del agente;
* posición y dirección de los objetivos;
* velocidad;
* posesión del hacha;
* posesión de la llave;
* estado actual de la misión.

Sus acciones permiten controlar:

* movimiento arriba;
* movimiento abajo;
* movimiento izquierda;
* movimiento derecha;
* interacción con objetos.

El entrenamiento utiliza **PPO (Proximal Policy Optimization)**.


# 📊 Métricas

Durante cada partida se recopilan estadísticas relacionadas con el comportamiento del jugador o del agente.

Entre ellas se encuentran:

* tiempo total del nivel;
* tiempo hasta encontrar el hacha;
* tiempo hasta encontrar la llave;
* tiempo hasta alcanzar la meta;
* distancia recorrida;
* porcentaje de exploración;
* cantidad de celdas visitadas;
* celdas repetidas;
* uso de cuevas;
* golpes contra objetos destructibles;
* intentos de golpe sin tener el hacha;
* número de reinicios;
* cantidad de pasos del episodio;
* motivo de finalización;
* eficiencia del recorrido;
* eficiencia desde la llave hasta la meta;
* uso de caminos alternativos.

Las métricas permiten evaluar diferentes situaciones.

### Nivel demasiado fácil

Por ejemplo:

* tiempo de resolución muy bajo;
* pocas celdas exploradas;
* recorrido muy directo;
* pocos errores.

### Nivel equilibrado

* tiempo de resolución moderado;
* exploración razonable;
* progreso continuo;
* nivel completado correctamente.

### Nivel demasiado difícil

* tiempo cercano al límite;
* gran cantidad de exploración repetida;
* numerosos retrocesos;
* reinicios;
* episodio terminado por timeout;
* nivel no completado.

Estas métricas pueden ser utilizadas como observaciones para un modelo de aprendizaje automático.

---

# 🧩 Generación procedural

Los niveles son generados dinámicamente durante la ejecución.

El sistema controla elementos como:

```text
Spawn del jugador
       ↓
Cueva A
       ↓
Zona del Hacha
       ↓
Obstáculos destructibles
       ↓
Llave
       ↓
Meta
```

El generador también valida que los objetivos sean alcanzables antes de aceptar el mapa generado.

Entre los parámetros configurables se encuentran:

```text
mapWidth
mapHeight
extraConnections

minPlayerToCaveADistance
minAxeToStartAndMetaDistance
minKeyToAxeDistance
minKeyToMetaDistance
minPlayerToMetaDistance

enableTravelCaves
maximumTravelCavePairs

minimumPathDistanceBetweenTravelCaves
minimumShortcutSaving
```

Estos parámetros permiten generar diferentes configuraciones manteniendo las reglas necesarias para que el nivel pueda completarse.

---

# 🕳️ Sistema de cuevas

El juego incluye cuevas que funcionan como teletransportes entre diferentes zonas del laberinto.

Estas cuevas pueden:

* conectar zonas separadas;
* permitir acceder al hacha;
* funcionar como atajos;
* modificar el camino óptimo hacia la meta.

El uso de cuevas también puede registrarse como parte de las métricas del jugador.

---

# 🧱 Arquitectura del proyecto

La lógica principal se encuentra organizada dentro de:

```text
Assets/
│
├── Data/
├── ML-Agents/
├── MetricsLogs/
├── Palettes/
├── Resources/
├── Scenes/
├── art/
├── prefabs/
│
└── scripts/
    │
    ├── Algorithm/
    ├── Data/
    ├── Difficulty/
    ├── Generation/
    ├── Inventory/
    ├── Items/
    ├── Managers/
    ├── Movement/
    ├── Obstacles/
    ├── Portals/
    ├── Rendering/
    ├── Spawning/
    ├── Training/
    ├── Tutorial/
    ├── UI/
    ├── Utils/
    └── Validation/
```

Algunos componentes importantes son:

### Generation

```text
DynamicLevelManager.cs
TravelCavePairManager.cs
```

Responsables de la generación y configuración dinámica del nivel.

### Training

```text
MazeAgent.cs
TrainingConfig.cs
TrainingLevelManager.cs
TrainingModeInitializer.cs
```

Contienen la lógica relacionada con el entorno de entrenamiento del agente.

### Difficulty

```text
DifficultyAdapterAgent.cs
DifficultyManager.cs
DifficultyMetrics.cs
DifficultyMetricsCollector.cs
DifficultyProfile.cs
DifficultySettings.cs
KeyToGoalTracker.cs
```

Gestionan las métricas, dificultad y adaptación del nivel.

---

# 🗺️ Escenas principales

El proyecto contiene varias escenas.

### `MazeLevel_Procedural`

Escena principal del juego con generación procedural.

```text
Assets/Scenes/MazeLevel_Procedural.unity
```

### `MazeLevel_Train`

Entorno utilizado para entrenamiento y evaluación mediante ML-Agents.

```text
Assets/Scenes/MazeLevel_Train.unity
```

---

# 🛠️ Tecnologías utilizadas

| Tecnología         | Uso                              |
| ------------------ | -------------------------------- |
| Unity              | Motor del videojuego             |
| C#                 | Programación de la lógica        |
| Unity ML-Agents    | Aprendizaje por refuerzo         |
| PPO                | Algoritmo de entrenamiento       |
| Python             | Entrenamiento mediante ML-Agents |
| ONNX               | Modelo entrenado para inferencia |
| Unity Input System | Controles                        |
| Tilemap            | Representación del laberinto     |

---

# 📦 Versiones principales

El proyecto fue desarrollado con:

```text
Unity 6000.5.2f1
Unity ML-Agents 4.1.0
Unity AI Inference 2.6.1
Unity Input System 1.19.0
Universal Render Pipeline 17.6.0
```

Para evitar problemas de compatibilidad se recomienda abrir el proyecto utilizando la misma versión de Unity.

---

# 🚀 Instalación

## 1. Clonar el repositorio

```bash
git clone https://github.com/jefferson556/testIaUnity.git
```

Entrar al proyecto:

```bash
cd testIaUnity
```

---

## 2. Abrir desde Unity Hub

Desde **Unity Hub**:

```text
Add
 ↓
Add project from disk
 ↓
Seleccionar la carpeta testIaUnity
```

Abrir el proyecto utilizando:

```text
Unity 6000.5.2f1
```

Unity instalará las dependencias definidas en:

```text
Packages/manifest.json
```

---

# 🎮 Ejecutar el juego

Abrir la escena:

```text
Assets/Scenes/MazeLevel_Procedural.unity
```

y presionar:

```text
Play ▶
```

El nivel será generado proceduralmente al iniciar.

---

# 🧠 Entrenamiento con ML-Agents

Para entrenar el agente debe existir un entorno Python compatible con Unity ML-Agents.

Una vez configurado, desde la carpeta donde se encuentra el proyecto puede ejecutarse:

```bash
mlagents-learn maze_config.yaml --run-id=MazeAgent_Training
```

Luego abrir en Unity:

```text
Assets/Scenes/MazeLevel_Train.unity
```

y presionar:

```text
Play ▶
```

Unity se conectará con el proceso de entrenamiento iniciado desde Python.

---

# ⚙️ Configuración del entrenamiento

El archivo:

```text
maze_config.yaml
```

contiene la configuración de los agentes.

## MazeAgent

Utiliza:

```yaml
trainer_type: ppo
```

con una red neuronal configurada para aprender la navegación del entorno.

También utiliza una señal de recompensa de curiosidad para incentivar la exploración.

## DifficultyAdapter

También utiliza PPO, pero trabaja sobre las métricas de desempeño y toma decisiones de adaptación entre niveles.

---

# 📈 Flujo completo del sistema

```text
                ┌───────────────────────┐
                │ Generación procedural │
                │      del nivel        │
                └───────────┬───────────┘
                            │
                            ▼
                ┌───────────────────────┐
                │ Jugador / MazeAgent   │
                │   resuelve el nivel   │
                └───────────┬───────────┘
                            │
                            ▼
                ┌───────────────────────┐
                │ Recolección de        │
                │ métricas              │
                └───────────┬───────────┘
                            │
                            ▼
                ┌───────────────────────┐
                │ DifficultyAdapter     │
                │ analiza rendimiento   │
                └───────────┬───────────┘
                            │
                 ┌──────────┼───────────┐
                 ▼          ▼           ▼

             DECREASE      KEEP      INCREASE

                 │          │           │
                 └──────────┼───────────┘
                            ▼
                ┌───────────────────────┐
                │ Configuración del     │
                │ siguiente nivel       │
                └───────────────────────┘
```

---
## 🎮 Probar el juego sin instalar Unity

También se encuentra disponible una versión compilada del proyecto para **Windows**, por lo que no es necesario instalar Unity ni configurar ML-Agents para probar el juego.

### Pasos

1. Descargar el archivo `.rar` desde el enlace de Google Drive proporcionado:

   **[Descargar versión ejecutable desde Google Drive](https://drive.google.com/file/d/1V0R8yVH_ReEe3MidSxfxLexaEfMB_eIt/view?usp=drive_link))**

2. Descomprimir completamente el archivo `.rar`.

3. Abrir la carpeta descomprimida.

4. Ejecutar:

```text
My project (1).exe
```

5. El juego se iniciará directamente.

> ⚠️ **Importante:** No ejecutar el archivo directamente desde el `.rar`. Primero se debe descomprimir todo el contenido.

La carpeta debe conservar archivos y directorios como:

```text
My project (1).exe
My project (1)_Data/
MonoBleedingEdge/
D3D12/
UnityPlayer.dll
DirectML.dll
```

Estos archivos forman parte de la compilación de Unity y deben mantenerse juntos en la misma carpeta para que el juego funcione correctamente.

Si Windows muestra una advertencia de seguridad al ejecutar el archivo, puede ser debido a que el ejecutable no está firmado digitalmente. En ese caso se puede seleccionar **Más información → Ejecutar de todas formas**, siempre que el archivo haya sido descargado desde el enlace oficial proporcionado para este proyecto.

---

# 👨‍💻 Autor

**Jefferson Perez**

Proyecto desarrollado como prototipo de experimentación con:

* videojuegos;
* generación procedural;
* inteligencia artificial;
* Reinforcement Learning;
* Unity ML-Agents;
* adaptación dinámica de dificultad.


