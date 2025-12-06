/*
Should Spawn new Apples each time one has been consumed
Should control the size of the scene.
 */

using Mono.Cecil;
using System.Linq;
using System.Reflection.Emit;
using TMPro;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UI;

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

    public bool parallelExecution = false; // Instead of running snake unity game objects which visually display the game, I run snake processes on multiple threads. Only show the final snake game.
    public int populationSize = 10;
    public int generationLimit = 15; // When to stop simulating and display the best candidate.
    public bool generationLimitEnabled = true;
    public int numGenes = 10;
    public float MutationRate = 0.01f;
    public float SelectionPercentage = 0.5f; // Percentage of population sorted by fitness to use for the next generation.
    public float elitistPopulationPercentage = 0.1f; // Percentage of population to fully preserve between generations.
    private int generation = 0;
    private int currentSnake = 0; // Run the games sequentially, this is how newly created snakes will get their corresponding DNA on initialization.
    private DNA bestDNA = null; // Highest Fitness DNA
    private int bestFitnessGeneration = 0;
    private bool simulationTerminated = false;

    public TextMeshProUGUI distanceToObstacleText;
    public TextMeshProUGUI distanceToAppleText;
    public TextMeshProUGUI seeAppleText;
    public TextMeshProUGUI snakeNumText;
    public TextMeshProUGUI popSizeText;
    public TextMeshProUGUI genNumText;
    public TMP_InputField timeStepInput;
    public TextMeshProUGUI timeStepText;
    public TextMeshProUGUI bestFitnessText;
    public TextMeshProUGUI fitnessText;
    public TextMeshProUGUI bestFitnessGenerationText;
    public TextMeshProUGUI generationLimitText;
    public TMP_InputField generationLimitInput;
    public Toggle generationLimitToggle;
    public Button pauseButton;

    /// GAMEOBJECTS

    private DNA[] population;
    private SnakeBehaviour snake;
    private SnakeGame snakeProcess;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Time.fixedDeltaTime = fixedTimeStep;

        /// EDGE CASES
        
        // Ensure scene is of the minimum playable size.
        SceneSize.x = Mathf.Max(4.0f, SceneSize.x);
        SceneSize.y = Mathf.Max(4.0f, SceneSize.y);
        // Ensure scene size is even so that camera is always centred.
        if (SceneSize.x % 2 != 0) { SceneSize.x += 1; }
        if (SceneSize.y % 2 != 0) { SceneSize.y += 1; }

        /// UI 

        timeStepInput.onSubmit.AddListener(delegate { SetTimeStep(timeStepInput.text); }); // This will call the Set function when input is finished
        generationLimitInput.onSubmit.AddListener(delegate { SetGenerationLimit(generationLimitInput.text); });
        pauseButton.onClick.AddListener(delegate { simulationTerminated = !simulationTerminated; });

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
        bestDNA = population[0];
        CreateSnake();
        snakeProcess = new SnakeGame();
        snakeProcess.Initialize(population[0], this);
    }

    // Update is called once per frame
    void Update()
    {
        /// UPDATE UI

        // Generation Limit Toggle
        bool prevGenLimitState = generationLimitEnabled;
        generationLimitEnabled = generationLimitToggle.isOn;
        // Pause Button State
        if (simulationTerminated) { pauseButton.GetComponentInChildren<TextMeshProUGUI>().text = "Resume Training"; }
        else { pauseButton.GetComponentInChildren<TextMeshProUGUI>().text = "Pause Training"; }

        snakeNumText.text = "Snake Number: " + currentSnake;
        popSizeText.text = "Population Size: " + populationSize;
        genNumText.text = "Generation: " + generation;
        timeStepText.text = "TimeStep: " + fixedTimeStep;
        generationLimitText.text = "Generation Limit: " + generationLimit;
        bestFitnessText.text = "Best Fitness: " + bestDNA.fitness;
        bestFitnessGenerationText.text = "Best Fitness Generation: " + bestFitnessGeneration;

        // Snake Specific UI
        if (simulationTerminated || !parallelExecution)
        {
            distanceToObstacleText.text = "Distance to Obstacle: " + snake.distanceToObstacleInFront;
            distanceToAppleText.text = "Distance to Apple: " + snake.distanceToApple;
            seeAppleText.text = "Sees Apple: " + snake.seeApple;
            snake.CalculateFitness();
            fitnessText.text = "Fitness: " + snake.dna.fitness;          
        }
        else
        {
            distanceToObstacleText.text = "Distance to Obstacle: " + snakeProcess.distanceToObstacleInFront;
            distanceToAppleText.text = "Distance to Apple: " + snakeProcess.distanceToApple;
            seeAppleText.text = "Sees Apple: " + snakeProcess.seeApple;
            snakeProcess.CalculateFitness();
            fitnessText.text = "Fitness: " + snakeProcess.dna.fitness;
        }
    }

    // This is ideal for running the snake game when visually displaying as I can control the timestep.
    public void FixedUpdate()
    {
        if (parallelExecution)
        {
            parallelSnakeGameUpdate();
        }
        else
        {
            sequentialSnakeGameUpdate();
        }
    }

    private void parallelSnakeGameUpdate()
    {
        /*
        NOTE:
        Currently this is just running my pure C# snake game script, 
        need to setup C# multithreading next.

        This function will attempt to train as many snake games in parallel as it can in each generation.
        Once the simulation is terminated, it will then display a single game with the best fitness DNA.
         */

        snakeProcess.Update();
        if (simulationTerminated)
        {
            // Run the best fitness DNA repeatedly.
            if (snake.alive == false) { snake.Restart(bestDNA.Clone()); }
        }
        else
        {
            if (snakeProcess.alive == false)
            {
                // Determine if snake is the best candidate.
                population[currentSnake].fitness = snakeProcess.CalculateFitness();
                if (population[currentSnake].fitness > bestDNA.fitness)
                {
                    // Store the dna (Once program is terminated, can then use the best DNA for the AI).
                    // Could optionally serialize it as well.
                    bestDNA = snakeProcess.dna.Clone();
                    bestFitnessGeneration = generation;
                }

                // Move to the next snake
                currentSnake++;
                if (currentSnake >= population.Length)
                {
                    // Next Generation
                    generation++;

                    if (generationLimitEnabled && generation > generationLimit)
                    {
                        simulationTerminated = true;
                    }
                    else
                    {
                        createNewGeneration();
                    }

                    currentSnake = 0;
                }
                // Update snake DNA
                snakeProcess.Restart(population[currentSnake]);
            }
        }
    }
    private void sequentialSnakeGameUpdate()
    {
        /*
        This visually displays each snake game that is running, when simulation is terminated
        it will then run and display a snake game with the best fitness DNA.
         */

        if (snake.alive == false)
        {
            if (simulationTerminated == false)
            {
                // Determine if snake is the best candidate.
                population[currentSnake].fitness = snake.CalculateFitness();
                if (population[currentSnake].fitness > bestDNA.fitness)
                {
                    // Store the dna (Once program is terminated, can then use the best DNA for the AI).
                    // Could optionally serialize it as well.
                    bestDNA = snake.dna.Clone();
                    bestFitnessGeneration = generation;
                }

                // Move to the next snake
                currentSnake++;
                if (currentSnake >= population.Length)
                {
                    // Next Generation
                    generation++;

                    if (generationLimitEnabled && generation > generationLimit)
                    {
                        simulationTerminated = true;
                    }
                    else
                    {
                        createNewGeneration();
                    }

                    currentSnake = 0;
                }
                // Update snake DNA
                snake.Restart(population[currentSnake]);
            }
            else
            {
                // Recreate the best snake over and over after simulation is terminated.
                snake.Restart(bestDNA.Clone());
            }
        }
    }

    private void createNewGeneration()
    {
        DNA[] newPopulation = new DNA[populationSize];

        // Sort in descending order of fitness
        System.Array.Sort(population, (a, b) => b.fitness.CompareTo((a.fitness)));

        for (int i = 0; i < populationSize; i++)
        {
            // Idea is that a percentage of the best populace won't be lost to random chance, but instead will be preserved and carried through generations until better are found.
            if (i < (int)(populationSize * elitistPopulationPercentage))
            {
                newPopulation[i] = population[i];
            }
            else
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
        }
        population = newPopulation;
    }

    // Purely for initialising a new gameObject, ideally this should only be called once and then the game should be restarted once finished.
    public void CreateSnake()
    {
        if (snake != null) { Destroy(snake.gameObject); }
        GameObject newSnake = Instantiate(SnakeGamePrefab);
        newSnake.transform.position = new Vector3(0.0f - (SceneSize.x/2), 0.0f - (SceneSize.y/2), 0.0f); // Centre the game on the screen
        SnakeBehaviour newSnakeBehaviour = newSnake.GetComponent<SnakeBehaviour>();
        if (simulationTerminated) { newSnakeBehaviour.Initialize(bestDNA.Clone(), this); }
        else { newSnakeBehaviour.Initialize(population[currentSnake], this); }
        snake = newSnakeBehaviour;
    }

    private DNA chooseParent()
    {
        // Population should be sorted in descending order of fitness before calling this function.

        // Chooses a random parent from the fittest candidates of the population.
        int fittestPopulationLength = (int)(populationSize * SelectionPercentage);
        int parentIndex = UnityEngine.Random.Range(0, fittestPopulationLength);
        return population[parentIndex];
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
    public void SetGenerationLimit(string limitString)
    {
        int limit;
        // Ensure string is valid
        if (int.TryParse(limitString, out limit))
        {
            generationLimit = limit;
        }
    }
}
