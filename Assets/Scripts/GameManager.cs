/*
Should Spawn new Apples each time one has been consumed
Should control the size of the scene.
 */

using Mono.Cecil;
using System.Linq;
using TMPro;
using UnityEngine;

public enum TileType
{
    Empty = 0,
    Wall = 1,
    Apple = 2,
    Snake = 3
}

public class GameManager : MonoBehaviour
{
    public GameObject SnakeGamePrefab;

    /// SNAKE SETTINGS

    private Vector2 baseSceneSize = new Vector2(32, 16);
    private float baseCameraSize = 10.0f;
    public Vector2 SceneSize = new Vector2(32, 16);
    public float fixedTimeStep = 0.1f;
    public int GrowthPerApple = 1;
    public bool SnakeColourGradient = false; // Off by default for performance (If you need a TON of snake games running at the same time).
    public Color headColor = Color.green;
    public Color tailColor = new Color(0, 0.5f, 0, 1);
    public bool fixedRNGSeed = true;
    public int randomGenerationSeed = 42; 

    /// GENETIC ALGORITHMS

    public int populationSize = 10;
    public float mutationRate = 0.01f;
    public int generationLimit = 15; // When to stop simulating and display the best candidate.
    public int numGenes = 10;
    public float MutationRate = 0.01f;
    public float SelectionPercentage = 0.5f; // Percentage of population sorted by fitness to use for the next generation.
    private int generation = 0;
    private int currentSnake = 0; // Run the games sequentially, this is how newly created snakes will get their corresponding DNA on initialization.
    private DNA bestPerformer = null;
    private bool simulationTerminated = false;
    private bool snakeCreated = false;

    public TextMeshProUGUI distanceToObstacleText;
    public TextMeshProUGUI distanceToAppleText;
    public TextMeshProUGUI seeAppleText;
    public TextMeshProUGUI snakeNumText;
    public TextMeshProUGUI popSizeText;
    public TextMeshProUGUI genNumText;
    public TMP_InputField timeStepInput;
    public TextMeshProUGUI timeStepText;

    /// GAMEOBJECTS

    private DNA[] population;
    private SnakeBehaviour snake;
    private GameObject[] walls;
    private GameObject apple;
    public GameObject GetApple() { return apple; } // Purely so it doesn't show on the inspector and make things confusing. I still want to be able to read it in my snake class though.

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Time.fixedDeltaTime = fixedTimeStep;
        timeStepInput.onEndEdit.AddListener(delegate { SetTimeStep(timeStepInput.text); }); // This will call the Set function when input is finished

        SceneSize.x = Mathf.Max(4.0f, SceneSize.x);
        SceneSize.y = Mathf.Max(4.0f, SceneSize.y);
        // Ensure scene size is even so that camera is always centred.
        if (SceneSize.x % 2 != 0) { SceneSize.x += 1; }
        if (SceneSize.y % 2 != 0) { SceneSize.y += 1; }

        /// RESIZE CAMERA TO FIT SCENE INTO VIEW

        Camera camera = Camera.main;
        Vector2 cameraSizeIncrement = new Vector2(baseCameraSize / baseSceneSize.x, baseCameraSize / baseSceneSize.y);
        Vector2 sizeDifference = new Vector2(SceneSize.x - baseSceneSize.x, SceneSize.y - baseSceneSize.y);
        if (SceneSize.x <= baseSceneSize.x && SceneSize.y <= baseSceneSize.y)
        {
            camera.orthographicSize = 10.0f;
        }
        else if (sizeDifference.x * cameraSizeIncrement.x > sizeDifference.y * cameraSizeIncrement.y)
        {
            camera.orthographicSize = baseCameraSize + (cameraSizeIncrement.x * sizeDifference.x);
        }
        else
        {
            camera.orthographicSize = baseCameraSize + (cameraSizeIncrement.y * sizeDifference.y);
        }

        /// PREPARE THE SNAKES

        populationSize = Mathf.Max(populationSize, 1); // EDGE CASE: Ensure population size is never 0.
        population = new DNA[populationSize];
        for (int i = 0; i < populationSize; i++)
        {
            population[i] = new DNA(numGenes);
        }
        bestPerformer = population[0];
        CreateSnake();
    }

    // Update is called once per frame
    void Update()
    {
        /// UPDATE UI
        // Later this should probably be moved to game controller, and game controller should be in charge of selecting the snake and updating the UI.

        if (snake != null)
        {
            distanceToObstacleText.text = "Distance to Obstacle: " + snake.distanceToObstacleInFront;
            distanceToAppleText.text = "Distance to Apple: " + snake.distanceToApple;
            seeAppleText.text = "Sees Apple: " + snake.seeApple;
            snakeNumText.text = "Snake Number: " + currentSnake;
            popSizeText.text = "Population Size: " + populationSize;
            genNumText.text = "Generation: " + generation;
            timeStepText.text = "TimeStep: " + fixedTimeStep;
        }
    }

    public void FixedUpdate()
    {
        // Should defer creation of snake from destroying it (Unity defers destroyed process I assume, since this has been an issue otherwise).
        if (!snakeCreated)
        {
            // Need to ensure it doesn't try to recreate a snake whilst it is already pending creation.
            // Been stuck in an infinite loop otherwise.
            CreateSnake();
        }
        if (snake.alive == false)
        {
            if (simulationTerminated == false)
            {
                // Determine if snake is the best candidate.
                population[currentSnake].fitness = snake.CalculateFitness();
                if (population[currentSnake].fitness > bestPerformer.fitness)
                {
                    // Store the dna (Once program is terminated, can then use the best DNA for the AI).
                    // Could optionally serialize it as well.
                    bestPerformer = snake.dna;
                }

                // Remove the previous Snake
                HandleDestroyingSnake();
                currentSnake++;
                if (currentSnake >= population.Length)
                {
                    // Next Generation
                    generation++;

                    if (generation > generationLimit)
                    {
                        simulationTerminated = true;
                    }
                    else
                    {
                        createNewGeneration();
                    }

                    currentSnake = 0;
                }
            }
            else
            {
                // Recreate the best snake over and over after simulation is terminated.
                HandleDestroyingSnake();
            }
        }
    }

    private void HandleDestroyingSnake()
    {
        snakeCreated = false;
    }

    private void createNewGeneration()
    {
        /// TODO ~ Crossover, mutation, etc

        DNA[] newPopulation = new DNA[populationSize];

        for (int i = 0; i < populationSize; i++)
        {
            // Selection
            DNA parent1 = chooseParent();
            DNA parent2 = chooseParent();

            // Crossover
            DNA child = parent1.Crossover(parent2);

            // Mutation
            child.Mutate(MutationRate);

            newPopulation[i] = child;
        }
        population = newPopulation;

        // Sort in descending order of fitness
        System.Array.Sort(population, (a, b) => b.fitness.CompareTo((a.fitness)));
    }

    public void CreateSnake()
    {
        if (snake != null) { Destroy(snake.gameObject); }
        GameObject newSnake = Instantiate(SnakeGamePrefab);
        newSnake.transform.position = new Vector3(0.0f - (SceneSize.x/2), 0.0f - (SceneSize.y/2), 0.0f); // Centre the game on the screen
        SnakeBehaviour newSnakeBehaviour = newSnake.GetComponent<SnakeBehaviour>();
        if (simulationTerminated)
        {
            // Use the best performer
            newSnakeBehaviour.Initialize(bestPerformer, this);
        }
        else
        {
            newSnakeBehaviour.Initialize(population[currentSnake], this);
        }
        snakeCreated = true;
        snake = newSnakeBehaviour;
    }

    public void SetTimeStep(string stepString) // Used by inputField UI to adjust timestep during runtime
    {
        float step;
        // Ensure string is valid, if so, set the new timestep.
        if (float.TryParse(stepString, out step))
        {
            Time.fixedDeltaTime = step;
            fixedTimeStep = step;
        }
    }

    private DNA chooseParent()
    {
        // Population should be sorted in descending order of fitness before calling this function.

        // Chooses a random parent from the fittest candidates of the population.
        int fittestPopulationLength = (int)(populationSize * SelectionPercentage);
        int parentIndex = UnityEngine.Random.Range(0, fittestPopulationLength);
        return population[parentIndex];
    }
}
