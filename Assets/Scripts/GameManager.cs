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
using System.Threading;
using System.Threading.Tasks;

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

    private bool trainingStarted = false;

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
    private TilemapSnakeGame displayedSnakeGame;

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
    }

    private void OnGUI()
    {
        /// CREATE UI MENU

        Rect menuRect = new Rect(
            Screen.width * 0.0125f,
            Screen.height * 0.025f,
            Screen.width * 0.2f,
            Screen.height * 0.95f
        );

        GUIStyle style = new GUIStyle();
        // Create background texture
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.1f, 0.25f));
        texture.Apply();
        style.normal.background = texture;
        GUI.Box(menuRect, "Main Menu", style);

        /// CREATE UI WIDGETS

        Rect widgetRect = new Rect(menuRect.x + menuRect.size.x * 0.05f, menuRect.y + menuRect.size.x * 0.05f, menuRect.size.x * 0.9f, menuRect.size.y * 0.05f);
        float widgetVerticalSpacing = menuRect.size.y * 0.1f; // Should increment widgetRect y by this after each widget to space the UI out.

        GUIStyle header = new GUIStyle(GUI.skin.label);
        header.fontSize = 16;
        header.fontStyle = FontStyle.Bold;

        const float TEXT_VERTICAL_SPACING_MULTIPLIER = 0.4f;

        if (!trainingStarted)
        {
            if (GUI.Button(widgetRect, "Start Training"))
            {

            }
        }
        else
        {
            if (simulationTerminated)
            {
                if (GUI.Button(widgetRect, "Resume Training"))
                {

                }
            }
            else
            {
                if (GUI.Button(widgetRect, "Pause Training"))
                {

                }
            }
        }
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER * 1.5f;

        GUI.Label(widgetRect, "Game Size: " + SceneSize);
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
        GUI.Label(widgetRect, "Growth per Apple: " + GrowthPerApple);
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
        GUI.Label(widgetRect, "Timestep: " + fixedTimeStep);
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
        GUI.Label(widgetRect, "RNG Seed: " + randomGenerationSeed);
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

        GUI.Label(widgetRect, "Genetic Algorithm", header);
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

        GUI.Label(widgetRect, "Population Size: " + populationSize);
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
        GUI.Label(widgetRect, "Number of Genes: " + numGenes);
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
        GUI.Label(widgetRect, "Mutation Rate: " + MutationRate);
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
        GUI.Label(widgetRect, "Selection Percentage: " + SelectionPercentage);
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
        GUI.Label(widgetRect, "Elite Selection Percentage: " + elitistPopulationPercentage);
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
        GUI.Label(widgetRect, "Generation Limit: " + generationLimit);
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

        GUI.Label(widgetRect, "Training Progress", header);
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

        GUI.Label(widgetRect, "Best Fitness: " + bestDNA.fitness);
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
        GUI.Label(widgetRect, "Best Fitness Generation: " + bestFitnessGeneration);
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
        GUI.Label(widgetRect, "Generation: " + generation);
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
        GUI.Label(widgetRect, "Snake: " + currentSnake);
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

        if (simulationTerminated)
        {
            GUI.Label(widgetRect, "Snake Statistics", header);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            GUI.Label(widgetRect, "Fitness: " + displayedSnakeGame.GetSnakeGame().dna.fitness);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUI.Label(widgetRect, "Apples Eaten: " + displayedSnakeGame.GetSnakeGame().numberOfApplesConsumed);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUI.Label(widgetRect, "DistanceToObstacle: " + displayedSnakeGame.GetSnakeGame().distanceToObstacleInFront);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUI.Label(widgetRect, "Sees Apple: " + displayedSnakeGame.GetSnakeGame().seeApple);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUI.Label(widgetRect, "DistanceToApple: " + displayedSnakeGame.GetSnakeGame().distanceToApple);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
        }
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
        if (simulationTerminated)
        {
            distanceToObstacleText.text = "Distance to Obstacle: " + displayedSnakeGame.GetSnakeGame().distanceToObstacleInFront;
            distanceToAppleText.text = "Distance to Apple: " + displayedSnakeGame.GetSnakeGame().distanceToApple;
            seeAppleText.text = "Sees Apple: " + displayedSnakeGame.GetSnakeGame().seeApple;
            displayedSnakeGame.GetSnakeGame().CalculateFitness();
            fitnessText.text = "Fitness: " + displayedSnakeGame.GetSnakeGame().dna.fitness;          
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
        This function will attempt to train as many snake games in parallel as it can in each generation.
        Once the simulation is terminated, it will then display a single game with the best fitness DNA.
         */

        if (simulationTerminated)
        {
            // Run the best fitness DNA repeatedly.
            if (displayedSnakeGame.GetSnakeGame().alive == false) { displayedSnakeGame.Restart(bestDNA.Clone()); }
        }
        else
        {
            // This is crazy fast holy moly, snake games in parallel.
            // Recommend a timestep of 0.01 if you want to interact with the UI so that can you pause or modify settings, but lower will mean far faster results.
            Parallel.For(0, populationSize, i =>
            {
                var game = new SnakeGame();
                game.Initialize(population[i], this);
                while (game.alive) { game.Update(); }
                population[i].fitness = game.CalculateFitness();
            });
            // Determine best fitness
            for (int i = 0; i < population.Length; i++)
            {
                if (population[i].fitness > bestDNA.fitness)
                {
                    // Store the dna (Once program is terminated, can then use the best DNA for the AI).
                    // Could optionally serialize it as well.
                    bestDNA = population[i].Clone();
                    bestFitnessGeneration = generation;
                }
            }
            // Next Generation
            if (generationLimitEnabled && generation > generationLimit)
            {
                simulationTerminated = true;
            }
            else
            {
                createNewGeneration();
            }
        }
    }
    private void sequentialSnakeGameUpdate()
    {
        /*
        This visually displays each snake game that is running, when simulation is terminated
        it will then run and display a snake game with the best fitness DNA.
         */

        if (displayedSnakeGame.GetSnakeGame().alive == false)
        {
            if (simulationTerminated == false)
            {
                // Determine if snake is the best candidate.
                population[currentSnake].fitness = displayedSnakeGame.GetSnakeGame().CalculateFitness();
                if (population[currentSnake].fitness > bestDNA.fitness)
                {
                    // Store the dna (Once program is terminated, can then use the best DNA for the AI).
                    // Could optionally serialize it as well.
                    bestDNA = displayedSnakeGame.GetSnakeGame().dna.Clone();
                    bestFitnessGeneration = generation;
                }

                // Move to the next snake
                currentSnake++;
                if (currentSnake >= population.Length)
                {
                    // Next Generation
                    if (generationLimitEnabled && generation > generationLimit)
                    {
                        simulationTerminated = true;
                    }
                    else
                    {
                        createNewGeneration();
                    }
                }
                // Update snake DNA
                displayedSnakeGame.Restart(population[currentSnake]);
            }
            else
            {
                // Recreate the best snake over and over after simulation is terminated.
                displayedSnakeGame.Restart(bestDNA.Clone());
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
        generation++;
        currentSnake = 0;
        population = newPopulation;
    }

    // Purely for initialising a new gameObject, ideally this should only be called once and then the game should be restarted once finished.
    public void CreateSnake()
    {
        if (displayedSnakeGame != null) { Destroy(displayedSnakeGame.gameObject); }
        GameObject newSnake = Instantiate(SnakeGamePrefab);
        newSnake.transform.position = new Vector3(0.0f - (SceneSize.x/2), 0.0f - (SceneSize.y/2), 0.0f); // Centre the game on the screen
        TilemapSnakeGame newSnakeGame = newSnake.GetComponent<TilemapSnakeGame>();
        if (simulationTerminated) { newSnakeGame.Initialize(bestDNA.Clone(), this); }
        else { newSnakeGame.Initialize(population[currentSnake], this); }
        displayedSnakeGame = newSnakeGame;
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
