/*
Should Spawn new Apples each time one has been consumed
Should control the size of the scene.
 */

using Mono.Cecil;
using System.Linq;
using TMPro;
using UnityEngine;
public class GameManager : MonoBehaviour
{
    public GameObject FoodPrefab;
    public GameObject SnakePrefab;
    public Transform SnakeSegmentPrefab;

    /// SNAKE SETTINGS

    private Vector2 baseSceneSize = new Vector2(32, 16);
    private float baseCameraSize = 10.0f;
    public Vector2 SceneSize = new Vector2(32, 16);
    public float fixedTimeStep = 0.1f;
    public int GrowthPerApple = 1;
    public bool SnakeColourGradient = false; // Off by default for performance (If you need a TON of snake games running at the same time).
    public Color headColor = Color.green;
    public Color tailColor = new Color(0, 0.5f, 0, 1);

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

        /// SETUP THE WALLS 

        walls = GameObject.FindGameObjectsWithTag("Wall");
        walls[0].transform.position = new Vector3(Mathf.Round(-SceneSize.x * 0.5f) - 1.0f, 0.0f, 0.0f);
        walls[1].transform.position = new Vector3(Mathf.Round(SceneSize.x * 0.5f) + 1.0f, 0.0f, 0.0f);
        walls[2].transform.position = new Vector3(0.0f, Mathf.Round(SceneSize.y * 0.5f) + 1.0f, 0.0f);
        walls[3].transform.position = new Vector3(0.0f, Mathf.Round(-SceneSize.y * 0.5f) - 1.0f, 0.0f);

        walls[0].transform.localScale = new Vector3(1.0f, SceneSize.y + 1.0f, 0.0f);
        walls[1].transform.localScale = new Vector3(1.0f, SceneSize.y + 1.0f, 0.0f);
        walls[2].transform.localScale = new Vector3(SceneSize.x + 3.0f, 1.0f, 0.0f);
        walls[3].transform.localScale = new Vector3(SceneSize.x + 3.0f, 1.0f, 0.0f);

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
        // Check if apple should be respawned
        if (GameObject.FindGameObjectsWithTag("Food").Length == 0)
        {
            Vector3 randomizedPosition = Vector3.zero;
            randomizedPosition.x = Mathf.Round(Random.Range(Mathf.Round(-SceneSize.x * 0.5f), Mathf.Round(SceneSize.x * 0.5f)));
            randomizedPosition.y = Mathf.Round(Random.Range(Mathf.Round(-SceneSize.y * 0.5f), Mathf.Round(SceneSize.y * 0.5f)));
            apple = Instantiate(FoodPrefab, randomizedPosition, Quaternion.identity);
        }

        /// UPDATE UI
        // Later this should probably be moved to game controller, and game controller should be in charge of selecting the snake and updating the UI.

        distanceToObstacleText.text = "Distance to Obstacle: " + snake.distanceToObstacleInFront;
        distanceToAppleText.text = "Distance to Apple: " + snake.distanceToApple;
        seeAppleText.text = "Sees Apple: " + snake.seeApple;
        snakeNumText.text = "Snake Number: " + currentSnake;
        popSizeText.text = "Population Size: " + populationSize;
        genNumText.text = "Generation: " + generation;
        timeStepText.text = "TimeStep: " + fixedTimeStep;
    }

    public void FixedUpdate()
    {
        if (snake.alive == false)
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
            Destroy(snake); 
            currentSnake++;          
            if (currentSnake >= population.Length)
            {
                // Next Generation
                generation++;

                //// TERMINATION TODO, going to let it run forever for now.
                //if (generation > generationLimit)
                //{

                //}

                createNewGeneration();

                currentSnake = 0;
            }
            CreateSnake();
        }
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
        snake = Instantiate(SnakePrefab).GetComponent<SnakeBehaviour>();
        snake.Initialize(population[currentSnake], SnakeSegmentPrefab, this);
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
