using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.UIElements;

/*
NOTE:
I found the best results when I train with a fixed RNG seed THEN tested against random rng seeds (random apple spawns).
Recommend using parallel. You can put the neural network to any size but it will drastically lower the speed
of training. You get perfectly fine results with 0 hidden layers in your neural network.
A low selection rate of 0.1 is typically best for faster training, you'll see within the first 500 or so generations 
some fast progress, 0.5 for example can take far longer going into the 1000's of generations before you see similar
results.

IMPROVEMENT IDEAS:
- Neural Network stores only the values for the current and previous layer, and just ping pongs between them as it computes each layer.
This will save a bit on memory, particularly with deep layers. Can simply use a bool for the 'ping pong' process.
- Can further optimize the neural network by using a 1D array for the values.
- I can improve SpawnApple in SnakeGame by keeping count of areas it already attempted to spawn an apple, this can stop games sometimes taking a long time
if the snake gets particularly long (Although this has yet to be a noticable issue).
- I should try adding functionality to the neural network for having varying sizes of hidden layers.
- The neural network could also benefit from having varying activation functions for the hidden and output layers. E.g.
Sigmoid for the output, TanH or ReLU for the hidden layers.
- I could optimize the snake game by using bitwise operations for the grid and snake movement instead of a grid of ints.
- Make SnakeGame stat variables private and instead use getters (since they shouldn't be modified outside of the class).
 */
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
    public Vector2 SceneSize = new Vector2(32, 32);
    public float FixedTimeStep = 0.05f;
    public int GrowthPerApple = 1;
    public bool SnakeColourGradient = true; // Turn off if you want a performance boost, but only really matters if you are displaying the snake during training.
    public Color HeadColor = Color.green;
    public Color TailColor = new Color(0, 0.5f, 0, 1);
    public bool FixedRNGSeed = true;
    public int RandomGenerationSeed = 42;

    /// NEURAL NETWORK SETTINGS

    public const int NumberOfInputNeurons = 16; // Should match the number of snake inputs I pass into the neural network.
    public const int NumberOfOutputNeurons = 3;
    public int NumberOfHiddenLayers = 1;
    public int NumberOfHiddenLayerNeurons = 6;

    /// GENETIC ALGORITHM SETTINGS

    public bool ParallelExecution = true; // Instead of running snake unity game objects which visually display the game, I run snake processes on multiple threads. Only show the final snake game.
    public int PopulationSize = 1000;
    public int GenerationLimit = 3000; // When to stop simulating and display the best candidate.
    public bool GenerationLimitEnabled = true;
    private int numGenes = 1000; // Should match the number required for the neural network.
    public float MutationRate = 0.05f;
    public float SelectionPercentage = 0.1f; // Percentage of population sorted by fitness to use for the next generation.
    public float ElitistPopulationPercentage = 0.01f; // Percentage of population to fully preserve between generations.

    /// GENETIC ALGORITHM

    public float ScorePerApple = 10.0f;
    public float ScoreProgressToNextAppleMultiplier = 1.0f;
    public float ScoreMovesMultiplier = 0.0f;
    public float ScoreDecayRate = 0.01f;

    CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(); // For stopping threads
    private Task parallelTrainingTask;
    private bool parallelTaskRunning = false;
    private int generation = 0;
    private int currentSnake = 0; // Run the games sequentially, this is how newly created snakes will get their corresponding DNA on initialization.
    private DNA bestDNA = null; // Highest Fitness DNA
    private int bestFitnessGeneration = 0;
    private bool simulationTerminated = true;
    private bool trainingStarted = false;
    private DNA[] population;
    private TilemapSnakeGame displayedSnakeGame;
    private System.Random random = new System.Random();
    private StreamWriter streamWriter; // Used for writing to file.
    private string trainingLogFileName = "GeneticAlgorithmLog";
    private int runCount = 0; // How many training sessions since program launch
    private float generationsBestFitness = 0;
    private float generationTotalFitness = 0;
    private float generationAverageFitness = 0;
    private float generationsLowestFitness = 0;
    private int mostApplesEaten = 0;
    private Stopwatch stopwatch;
    private bool pauseDisplayedGame = false;

    /// UI
    private bool geneticAlgorithmUIEnabled = false;
    private bool gameSettingsUIEnabled = false;
    private bool trainingProgressUIEnabled = true;
    private bool neuralNetworkUIEnabled = false;
    private bool fitnessSettingsUIEnabled = false;
    private bool snakeStatsUIEnabled = true;
    private string dnaFileName = "bestDNA1";
    private string inputPop = "";
    //private string inputGeneNum = "";
    private string inputGenLimit = "";
    private string inputTimeStep = "";
    private string inputAppleGrowth = "";
    private string inputSelectionPercentage = "";
    private string inputElitistSelectionPercentage = "";
    private string inputMutationRate = "";
    private string inputRNGSeed = "";
    private string inputSceneX = "";
    private string inputSceneY = "";
    private string inputHiddenLayerNum = "";
    private string inputHiddenLayerNeuronNum = "";
    private string inputScoreApple = "";
    private string inputScoreAppleProgressMultiplier = "";
    private string inputScoreMoveMultiplier = "";
    private string inputScoreDecayRate = "";



    ///////////////
    ///// FUNCTIONS



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        numGenes = NeuralNetwork.CalculateNumberOfGenesForNeuralNetwork(NumberOfHiddenLayers, NumberOfHiddenLayerNeurons, NumberOfInputNeurons, NumberOfOutputNeurons);

        Time.fixedDeltaTime = FixedTimeStep;
        validateSceneSize();
        updateCamera();
        createInitialPopulation();
        createSnake();
    }

    void OnApplicationQuit()
    {
        shutdown();
    }
    private void OnDestroy()
    {
        shutdown();
    }
    private void shutdown()
    {
        streamWriter.Close();
        cancellationTokenSource.Cancel(); // Tells all threads that they should stop execution.
        parallelTrainingTask.Wait();
        cancellationTokenSource.Dispose();
        cancellationTokenSource = new CancellationTokenSource();
    }

    private void createInitialPopulation()
    {
        PopulationSize = Mathf.Max(PopulationSize, 1); // EDGE CASE: Ensure population size is never 0.
        population = new DNA[PopulationSize];
        for (int i = 0; i < PopulationSize; i++)
        {
            population[i] = new DNA(numGenes, random);
        }
        bestDNA = population[0];
        stopwatch = new Stopwatch();
    }
    private void validateSceneSize()
    {
        // Ensure scene is of the minimum playable size.
        SceneSize.x = Mathf.Max(4.0f, SceneSize.x);
        SceneSize.y = Mathf.Max(4.0f, SceneSize.y);
        // Ensure scene size is even so that camera is always centred.
        if (SceneSize.x % 2 != 0) { SceneSize.x += 1; }
        if (SceneSize.y % 2 != 0) { SceneSize.y += 1; }
    }
    private void updateCamera()
    {
        // Will scale the camera based on scene size so that the entire level is in view.

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

        Rect widgetRect = new Rect(menuRect.x + menuRect.size.x * 0.05f, menuRect.y + menuRect.size.x * 0.05f, menuRect.size.x * 0.9f, menuRect.size.x * 0.11f);
        float widgetVerticalSpacing = menuRect.size.x * 0.235f; // Should increment widgetRect y by this after each widget to space the UI out.
        int inputInt;
        float inputFloat;
        Color defaultColor = GUI.color;

        GUIStyle header = new GUIStyle(GUI.skin.label);
        header.fontSize = 16;
        header.fontStyle = FontStyle.Bold;

        const float TEXT_VERTICAL_SPACING_MULTIPLIER = 0.4f;

        /// RIGHT-SIDE MENU

        Rect quitButtonRect = new Rect(Screen.width - widgetRect.size.x * 1.1f, 0.0f + widgetRect.size.y * 1.1f, widgetRect.size.x, widgetRect.size.y);
        if (GUI.Button(quitButtonRect, "Quit"))
        {
            Application.Quit();
        }
        quitButtonRect.y += widgetVerticalSpacing * 1.0f;

        if (GUI.Button(quitButtonRect, "Save DNA"))
        {
            StreamWriter dnaWriter = new StreamWriter(dnaFileName + ".dna");
            bestDNA.Serialize(dnaWriter, ref NumberOfHiddenLayers, ref NumberOfHiddenLayerNeurons);
            dnaWriter.Close();
        }
        quitButtonRect.y += widgetVerticalSpacing * 0.6f;
        if (GUI.Button(quitButtonRect, "Load DNA"))
        {
            StreamReader dnaLoader = new StreamReader(dnaFileName + ".dna");
            bestDNA.Deserialize(dnaLoader, ref NumberOfHiddenLayers, ref NumberOfHiddenLayerNeurons);
            dnaLoader.Close();
            numGenes = NeuralNetwork.CalculateNumberOfGenesForNeuralNetwork(NumberOfHiddenLayers, NumberOfHiddenLayerNeurons, NumberOfInputNeurons, NumberOfOutputNeurons);
            displayedSnakeGame.Restart(bestDNA.Clone(), this);

            // Stop UI overwriting the values
            inputHiddenLayerNum = "";
            inputHiddenLayerNeuronNum = "";
        }
        quitButtonRect.y += widgetVerticalSpacing * 0.6f;
        //string simulateButtonName = !pauseDisplayedGame ? "Stop Simulating Best DNA" : "Simulate Best DNA";
        //if (GUI.Button(quitButtonRect, simulateButtonName))
        //{
        //    pauseDisplayedGame = !pauseDisplayedGame;
        //    if(!pauseDisplayedGame) { displayedSnakeGame.Restart(bestDNA.Clone(), this); }
        //}
        //quitButtonRect.y += widgetVerticalSpacing * 0.6f;       
        GUILayout.BeginArea(quitButtonRect);
        dnaFileName = GUILayout.TextField(dnaFileName);
        GUILayout.EndArea();

        /// TRAINING PAUSE-RESUME-START-STOP BUTTONS

        {
            if (!trainingStarted)
            {
                if (GUI.Button(widgetRect, "Start Training"))
                {
                    trainingStarted = true;
                    simulationTerminated = false;
                    parallelTaskRunning = false;
                    runCount++;
                    generation = 0;
                    currentSnake = 0;
                    bestFitnessGeneration = 0;
                    generationsBestFitness = 0;
                    generationTotalFitness = 0;
                    generationAverageFitness = 0;
                    generationsLowestFitness = 0;
                    numGenes = NeuralNetwork.CalculateNumberOfGenesForNeuralNetwork(NumberOfHiddenLayers, NumberOfHiddenLayerNeurons, NumberOfInputNeurons, NumberOfOutputNeurons);
                    validateSceneSize();
                    updateCamera();
                    createInitialPopulation();
                    createSnake();
                    stopwatch = Stopwatch.StartNew();

                    // Start writing to file. Will append to previous entries instead of overwriting.
                    string fileName = trainingLogFileName + ".csv";
                    streamWriter = new StreamWriter(fileName, append: true); // This means I could reuse one file and have multiple training sessions logged to it.
                    // Write Settings ~ Space out from previous training session (If any)
                    if (new FileInfo(fileName).Length != 0)
                    {
                        streamWriter.WriteLine("");
                    }
                    streamWriter.WriteLine("RunNumber,Mutation Rate,Population Size,Gene Size,Parallel,TimeStep,Fixed RNG Seed,Selection Percentage,Elitist Selection Percentage," +
                        "HiddenLayers,HiddenLayerNeurons,ScoreMoveEfficiencyMultiplier,ScoreProgressNextAppleMultiplier");
                    streamWriter.WriteLine(runCount + "," + MutationRate + "," + PopulationSize + "," + numGenes + "," + ParallelExecution + "," + FixedTimeStep + "," + FixedRNGSeed + "," + 
                        SelectionPercentage + "," + ElitistPopulationPercentage + ","+NumberOfHiddenLayers+","+NumberOfHiddenLayerNeurons+","+ScoreMovesMultiplier+","+ScoreProgressToNextAppleMultiplier);
                    streamWriter.WriteLine("");
                    // Write Header
                    streamWriter.WriteLine("Generation,Best Fitness,Lowest Fitness,Average Fitness,Time to Compute (ms),Most Apples Eaten");
                }
            }
            else
            {
                if (simulationTerminated)
                {
                    GUI.enabled = !shouldSimulationTerminate();
                    if (GUI.Button(widgetRect, "Resume Training"))
                    {
                        simulationTerminated = false;
                        parallelTaskRunning = false;
                    }
                    GUI.enabled = true;
                }
                else
                {
                    if (GUI.Button(widgetRect, "Pause Training"))
                    {
                        simulationTerminated = true;
                        displayedSnakeGame.GetSnakeGame().Alive = false;
                        displayedSnakeGame.Restart(bestDNA.Clone(), this);
                    }
                }
            }
            widgetRect.y += widgetVerticalSpacing * 0.6f;
            GUI.enabled = (trainingStarted);
            if (GUI.Button(widgetRect, "Stop Training"))
            {
                trainingStarted = false;
                simulationTerminated = true;
                cancellationTokenSource.Cancel();
                if (parallelTrainingTask != null)
                {
                    parallelTrainingTask.Wait(); // Waits until it stops first.
                }
                cancellationTokenSource.Dispose();
                cancellationTokenSource = new CancellationTokenSource();
                streamWriter.Close();
            }
            GUI.enabled = true;
            widgetRect.y += widgetVerticalSpacing * 0.6f;

            /// Display Game Pause Button

            if (simulationTerminated)
            {
                if (pauseDisplayedGame)
                {
                    GUI.color = Color.red;
                    if (GUI.Button(widgetRect, "Resume Displayed Game"))
                    {
                        pauseDisplayedGame = false;
                    }
                }
                else
                {
                    GUI.color = Color.green;
                    if (GUI.Button(widgetRect, "Pause Displayed Game"))
                    {
                        pauseDisplayedGame = true;
                    }
                }
                widgetRect.y += widgetVerticalSpacing * 0.6f;
                GUI.color = defaultColor;
            }

            GUI.enabled = (!trainingStarted);
            if (GUI.Button(widgetRect, "Clear Log File"))
            {
                string fileName = trainingLogFileName + ".csv";
                streamWriter = new StreamWriter(fileName, append: false);
                streamWriter.Close();
            }
            GUI.enabled = true;
            //widgetRect.y += widgetVerticalSpacing * 0.6f;
        }
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER * 1.3f;

        /// TAB BUTTONS
        // Because of how large the UI is, I'll make it possible to enable/disable tabs

        Rect tabButtonsWidget = new Rect(widgetRect.x, widgetRect.y, widgetRect.width * 0.33f, widgetRect.height * 0.7f);
        {
            GUI.color = gameSettingsUIEnabled ? defaultColor : Color.red;
            if (GUI.Button(tabButtonsWidget, "Game"))
            {
                gameSettingsUIEnabled = !gameSettingsUIEnabled;
            }
            tabButtonsWidget.x += tabButtonsWidget.width;
            GUI.color = geneticAlgorithmUIEnabled ? defaultColor : Color.red;
            if (GUI.Button(tabButtonsWidget, "GE"))
            {
                geneticAlgorithmUIEnabled = !geneticAlgorithmUIEnabled;
            }
            tabButtonsWidget.x += tabButtonsWidget.width;
            GUI.color = trainingProgressUIEnabled ? defaultColor : Color.red;
            if (GUI.Button(tabButtonsWidget, "Training"))
            {
                trainingProgressUIEnabled = !trainingProgressUIEnabled;
            }

            // NEXT ROW
            tabButtonsWidget.x = widgetRect.x;
            tabButtonsWidget.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUI.color = fitnessSettingsUIEnabled ? defaultColor : Color.red;
            if (GUI.Button(tabButtonsWidget, "Fitness"))
            {
                fitnessSettingsUIEnabled = !fitnessSettingsUIEnabled;
            }
            tabButtonsWidget.x += tabButtonsWidget.width;
            GUI.color = neuralNetworkUIEnabled ? defaultColor : Color.red;
            if (GUI.Button(tabButtonsWidget, "Network"))
            {
                neuralNetworkUIEnabled = !neuralNetworkUIEnabled;
            }
            tabButtonsWidget.x += tabButtonsWidget.width;
            GUI.color = snakeStatsUIEnabled ? defaultColor : Color.red;
            if (GUI.Button(tabButtonsWidget, "Snake"))
            {
                snakeStatsUIEnabled = !snakeStatsUIEnabled;
            }
            tabButtonsWidget.x += tabButtonsWidget.width;
        }
        widgetRect.y = tabButtonsWidget.y + widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER * 0.7f;
        GUI.color = defaultColor;

        /// NEURAL NETWORK SETTINGS

        if (neuralNetworkUIEnabled)
        {
            GUI.Label(widgetRect, "Neural Network", header);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            int hiddenLayerNumPrev = NumberOfHiddenLayers;
            int hiddenLayerNeuronNumPrev = NumberOfHiddenLayerNeurons;

            GUI.Label(widgetRect, "Number of Hidden Layers: " + NumberOfHiddenLayers);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUI.enabled = !trainingStarted;
            GUILayout.BeginArea(widgetRect);
            inputHiddenLayerNum = GUILayout.TextField(inputHiddenLayerNum);
            if (int.TryParse(inputHiddenLayerNum, out inputInt))
            {
                NumberOfHiddenLayers = inputInt;
            }
            GUILayout.EndArea();
            GUI.enabled = true;
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            GUI.Label(widgetRect, "Neurons per Hidden Layer: " + NumberOfHiddenLayerNeurons);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUI.enabled = !trainingStarted;
            GUILayout.BeginArea(widgetRect);
            inputHiddenLayerNeuronNum = GUILayout.TextField(inputHiddenLayerNeuronNum);
            if (int.TryParse(inputHiddenLayerNeuronNum, out inputInt))
            {
                NumberOfHiddenLayerNeurons = inputInt;
            }
            GUILayout.EndArea();
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUI.enabled = true;

            if (hiddenLayerNeuronNumPrev != NumberOfHiddenLayerNeurons || hiddenLayerNumPrev != NumberOfHiddenLayers)
            {
                numGenes = NeuralNetwork.CalculateNumberOfGenesForNeuralNetwork(NumberOfHiddenLayers, NumberOfHiddenLayerNeurons, NumberOfInputNeurons, NumberOfOutputNeurons);
            }
        }
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER * 0.1f;

        /// SNAKE GAME SETTINGS

        if (gameSettingsUIEnabled)
        {
            GUI.Label(widgetRect, "Snake Settings", header);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            GUI.enabled = !trainingStarted;
            GUILayout.BeginArea(widgetRect);
            ParallelExecution = GUILayout.Toggle(ParallelExecution, "Parallel Execution");
            GUILayout.EndArea();
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUI.enabled = true;

            GUILayout.BeginArea(widgetRect);
            SnakeColourGradient = GUILayout.Toggle(SnakeColourGradient, "Snake Colour Gradient");
            GUILayout.EndArea();
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            GUI.enabled = !trainingStarted;
            GUI.Label(widgetRect, "Game Size: " + SceneSize);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUILayout.BeginArea(widgetRect);
            inputSceneX = GUILayout.TextField(inputSceneX);
            if (float.TryParse(inputSceneX, out inputFloat))
            {
                SceneSize.x = inputFloat;
            }
            GUILayout.EndArea();
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUILayout.BeginArea(widgetRect);
            inputSceneY = GUILayout.TextField(inputSceneY);
            if (float.TryParse(inputSceneY, out inputFloat))
            {
                SceneSize.y = inputFloat;
            }
            GUILayout.EndArea();
            GUI.enabled = true;
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            GUI.Label(widgetRect, "Growth per Apple: " + GrowthPerApple);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUILayout.BeginArea(widgetRect);
            inputAppleGrowth = GUILayout.TextField(inputAppleGrowth);
            if (int.TryParse(inputAppleGrowth, out inputInt))
            {
                GrowthPerApple = inputInt;
            }
            GUILayout.EndArea();
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            GUI.Label(widgetRect, "Timestep: " + FixedTimeStep);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUILayout.BeginArea(widgetRect);
            inputTimeStep = GUILayout.TextField(inputTimeStep);
            if (float.TryParse(inputTimeStep, out inputFloat))
            {
                Time.fixedDeltaTime = inputFloat;
                FixedTimeStep = inputFloat;
            }           
            GUILayout.EndArea();
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            //GUI.enabled = !trainingStarted;
            GUI.Label(widgetRect, "RNG Seed: " + RandomGenerationSeed);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUILayout.BeginArea(widgetRect);
            FixedRNGSeed = GUILayout.Toggle(FixedRNGSeed, "Fixed RNG Seed");
            GUILayout.EndArea();
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUILayout.BeginArea(widgetRect);
            inputRNGSeed = GUILayout.TextField(inputRNGSeed);
            if (int.TryParse(inputRNGSeed, out inputInt))
            {
                RandomGenerationSeed = inputInt;
            }
            GUILayout.EndArea();
            //GUI.enabled = true;
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
        }
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER * 0.1f;

        /// GENETIC ALGORITHM SETTINGS

        if (geneticAlgorithmUIEnabled)
        {
            GUI.Label(widgetRect, "Genetic Algorithm", header);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            GUI.Label(widgetRect, "Population Size: " + PopulationSize);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            GUI.enabled = !trainingStarted; // DISABLE BEGIN
            GUILayout.BeginArea(widgetRect);
            inputPop = GUILayout.TextField(inputPop);
            if (int.TryParse(inputPop, out inputInt))
            {
                PopulationSize = inputInt;
            }
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUILayout.EndArea();
            GUI.enabled = true; // DISABLE END

            GUI.Label(widgetRect, "Number of Genes: " + numGenes);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            //GUI.enabled = !trainingStarted; // DISABLE BEGIN
            //GUILayout.BeginArea(widgetRect);
            //inputGeneNum = GUILayout.TextField(inputGeneNum);
            //if (int.TryParse(inputGeneNum, out inputInt))
            //{
            //    numGenes = inputInt;
            //}
            //widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            //GUILayout.EndArea();
            //GUI.enabled = true; // DISABLE END

            GUI.Label(widgetRect, "Mutation Rate: " + MutationRate);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUILayout.BeginArea(widgetRect);
            inputMutationRate = GUILayout.TextField(inputMutationRate);
            if (float.TryParse(inputMutationRate, out inputFloat))
            {
                MutationRate = inputFloat;
                MutationRate = Mathf.Clamp01(MutationRate);
            }
            GUILayout.EndArea();
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            GUI.Label(widgetRect, "Selection Percentage: " + SelectionPercentage);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUILayout.BeginArea(widgetRect);
            inputSelectionPercentage = GUILayout.TextField(inputSelectionPercentage);
            if (float.TryParse(inputSelectionPercentage, out inputFloat))
            {
                SelectionPercentage = inputFloat;
                SelectionPercentage = Mathf.Clamp01(SelectionPercentage);
            }
            GUILayout.EndArea();
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            GUI.Label(widgetRect, "Elite Selection Percentage: " + ElitistPopulationPercentage);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUILayout.BeginArea(widgetRect);
            inputElitistSelectionPercentage = GUILayout.TextField(inputElitistSelectionPercentage);
            if (float.TryParse(inputElitistSelectionPercentage, out inputFloat))
            {
                ElitistPopulationPercentage = inputFloat;
                ElitistPopulationPercentage = Mathf.Clamp01(ElitistPopulationPercentage);
            }
            GUILayout.EndArea();
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            GUI.Label(widgetRect, "Generation Limit: " + GenerationLimit);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUILayout.BeginArea(widgetRect);
            GenerationLimitEnabled = GUILayout.Toggle(GenerationLimitEnabled, "Generation Limit");
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUILayout.EndArea();
            GUILayout.BeginArea(widgetRect);
            inputGenLimit = GUILayout.TextField(inputGenLimit);
            if (int.TryParse(inputGenLimit, out inputInt))
            {
                GenerationLimit = inputInt;
            }
            GUILayout.EndArea();
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            
        }
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER * 0.1f;

        /// FITNESS SETTINGS

        if (fitnessSettingsUIEnabled)
        {
            GUI.Label(widgetRect, "Fitness Settings", header);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            GUI.Label(widgetRect, "Score per Apple: " + ScorePerApple);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUILayout.BeginArea(widgetRect);
            inputScoreApple = GUILayout.TextField(inputScoreApple);
            if (float.TryParse(inputScoreApple, out inputFloat))
            {
                ScorePerApple = inputFloat;
            }
            GUILayout.EndArea();
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            GUI.Label(widgetRect, "Score Apple Progress Multiplier: " + ScoreProgressToNextAppleMultiplier);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUILayout.BeginArea(widgetRect);
            inputScoreAppleProgressMultiplier = GUILayout.TextField(inputScoreAppleProgressMultiplier);
            if (float.TryParse(inputScoreAppleProgressMultiplier, out inputFloat))
            {
                ScoreProgressToNextAppleMultiplier = inputFloat;
            }
            GUILayout.EndArea();
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            GUI.Label(widgetRect, "Score Moves Multiplier: " + ScoreMovesMultiplier);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUILayout.BeginArea(widgetRect);
            inputScoreMoveMultiplier = GUILayout.TextField(inputScoreMoveMultiplier);
            if (float.TryParse(inputScoreMoveMultiplier, out inputFloat))
            {
                ScoreMovesMultiplier = inputFloat;
            }
            GUILayout.EndArea();
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            GUI.Label(widgetRect, "Score Decay Rate: " + ScoreDecayRate);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            GUILayout.BeginArea(widgetRect);
            inputScoreDecayRate = GUILayout.TextField(inputScoreDecayRate);
            if (float.TryParse(inputScoreDecayRate, out inputFloat))
            {
                ScoreDecayRate = inputFloat;
            }
            GUILayout.EndArea();
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
        }
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER * 0.1f;

        /// TRAINING STATISTICS

        if (trainingProgressUIEnabled)
        {
            GUI.Label(widgetRect, "Training Progress", header);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            GUI.Label(widgetRect, "Best Fitness: " + bestDNA.Fitness);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            GUI.Label(widgetRect, "Best Fitness Generation: " + bestFitnessGeneration);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            GUI.Label(widgetRect, "Most Apples Eaten: " + bestDNA.MostApplesEaten);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            GUI.Label(widgetRect, "Generation: " + generation);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            GUI.Label(widgetRect, "Snake: " + currentSnake);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

            GUI.Label(widgetRect, "GenerationTime(ms): " + stopwatch.ElapsedMilliseconds);
            widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
        }
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER * 0.1f;

        if (snakeStatsUIEnabled)
        {
            if (!parallelTaskRunning)
            {
                GUI.Label(widgetRect, "Snake Statistics", header);
                widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

                displayedSnakeGame.GetSnakeGame().CalculateFitness(ScorePerApple, ScoreMovesMultiplier, ScoreProgressToNextAppleMultiplier, ScoreDecayRate);
                GUI.Label(widgetRect, "Fitness: " + displayedSnakeGame.GetSnakeGame().Dna.Fitness);
                widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

                GUI.Label(widgetRect, "Apples Eaten: " + displayedSnakeGame.GetSnakeGame().NumberOfApplesConsumed);
                widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

                GUI.Label(widgetRect, "Sees Apple: " + displayedSnakeGame.GetSnakeGame().SeeApple);
                widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

                GUI.Label(widgetRect, "DistanceToApple: " + displayedSnakeGame.GetSnakeGame().DistanceToApple);
                widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

                GUI.Label(widgetRect, "DirectionToApple: " + displayedSnakeGame.GetSnakeGame().DirectionToApple);
                widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

                GUI.Label(widgetRect, "DistanceToFrontObstacle: " + displayedSnakeGame.GetSnakeGame().DistanceToObstacleInFront);
                widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

                GUI.Label(widgetRect, "DistanceToLeftObstacle: " + displayedSnakeGame.GetSnakeGame().DistanceToLeftObstacle);
                widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

                GUI.Label(widgetRect, "DistanceToRightObstacle: " + displayedSnakeGame.GetSnakeGame().DistanceToRightObstacle);
                widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

                GUI.Label(widgetRect, "DistanceToWallBehind: " + displayedSnakeGame.GetSnakeGame().DistanceToWallBehind);
                widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

                GUI.Label(widgetRect, "AverageMoveEfficiency: " + displayedSnakeGame.GetSnakeGame().AverageMoveEfficiency.ToString("F3"));
                widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

                GUI.Label(widgetRect, "MinimumMovesToApple: " + displayedSnakeGame.GetSnakeGame().MinimumMovesToApple);
                widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;

                GUI.Label(widgetRect, "Moves since last Apple: " + displayedSnakeGame.GetSnakeGame().NumMovesSinceLastApple);
                widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER;
            }
        }
        widgetRect.y += widgetVerticalSpacing * TEXT_VERTICAL_SPACING_MULTIPLIER * 0.1f;
    }

    // This is ideal for running the snake game when visually displaying as I can control the timestep.
    public void FixedUpdate()
    {
        if (!simulationTerminated && trainingStarted)
        {
            if (ParallelExecution)
            {
                if (!parallelTaskRunning)
                {
                    // Run the training on a separate thread so that it doesn't block the main thread (UI Input, etc).
                    parallelTaskRunning = true;
                    parallelTrainingTask = Task.Run(() => parallelTrainingLoop());
                }
            }
            else
            {
                sequentialSnakeGameUpdate();
            }
        }
        else if (!pauseDisplayedGame)
        {
            // Run the best fitness DNA repeatedly.
            if (displayedSnakeGame.GetSnakeGame().Alive == false) {
                // If neural network has been modified, ensure dna has enough genes.
                if (bestDNA.Genes.Length < numGenes)
                {
                    float[] newGenes = new float[numGenes];
                    for (int i = 0; i < numGenes; i++)
                    {
                        if (i < bestDNA.Genes.Length) { newGenes[i] = bestDNA.Genes[i]; }
                        else { newGenes[i] = 0.0f; }
                    }
                    bestDNA.Genes = newGenes;
                }

                displayedSnakeGame.Restart(bestDNA.Clone(), this); 
            }
            else { displayedSnakeGame.UpdateSnake(); }
        }
    }

    // ONLY RUN THIS ON A SEPARATE THREAD, OTHERWISE IT WILL BLOCK THE REST OF THE APPLICATION SUCH AS THE UI.
    private void parallelTrainingLoop()
    {
        while (!cancellationTokenSource.Token.IsCancellationRequested && !simulationTerminated)
        {
            parallelSnakeGameUpdate();
        }
        parallelTaskRunning = false;
    }
    private void parallelSnakeGameUpdate()
    {
        /*
        This function will attempt to train as many snake games in parallel as it can in each generation.
        Once the simulation is terminated, it will then display a single game with the best fitness DNA.
         */

        // https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.paralleloptions.maxdegreeofparallelism?view=net-10.0#system-threading-tasks-paralleloptions-maxdegreeofparallelism
        // I've set it so that it leaves at least one core available so your PC doesn't explode when you make poor decisions.
        //Debug.Log(Environment.ProcessorCount);
        ParallelOptions options = new ParallelOptions ();
        options.MaxDegreeOfParallelism = Environment.ProcessorCount - 1;
        // Through the magic of parallel.for, it will create as many threads as it can to run the snake games in parallel.
        Parallel.For(0, PopulationSize, options, i =>
        {
            if (cancellationTokenSource.Token.IsCancellationRequested)
            {
                // Doesn't have to do anything, purpose is to stop the threads as soon as possible.
            }
            else
            {
                var game = new SnakeGame();
                game.Initialise(population[i], this);
                while (!cancellationTokenSource.Token.IsCancellationRequested && game.Alive)
                {
                    game.Update();
                }
                population[i].Fitness = game.CalculateFitness(ScorePerApple, ScoreMovesMultiplier, ScoreProgressToNextAppleMultiplier, ScoreDecayRate);
            }
        });
        // Determine best fitness    
        generationsLowestFitness = population[0].Fitness; // So that it doesn't start at 0.
        for (int i = 0; i < population.Length; i++)
        {
            checkAndHandleIfSnakeHasBestDNA(i);
        }
        if (cancellationTokenSource.Token.IsCancellationRequested) { return; }
        // Next Generation
        if (!shouldSimulationTerminate())
        {
            createNewGeneration();
        }
        else
        {
            displayedSnakeGame.GetSnakeGame().Alive = false;
        }
    }
    private void sequentialSnakeGameUpdate()
    {
        /*
        This visually displays each snake game that is running, when simulation is terminated
        it will then run and display a snake game with the best fitness DNA.
         */

        if (displayedSnakeGame.GetSnakeGame().Alive == false)
        {         
            if (currentSnake == 0) { generationsLowestFitness = population[0].Fitness; }
            population[currentSnake].Fitness = displayedSnakeGame.GetSnakeGame().CalculateFitness(ScorePerApple, ScoreMovesMultiplier, ScoreProgressToNextAppleMultiplier, ScoreDecayRate);
            checkAndHandleIfSnakeHasBestDNA(currentSnake);

            // Move to the next snake
            currentSnake++;
            if (currentSnake >= population.Length)
            {
                // Next Generation
                if (!shouldSimulationTerminate())
                {
                    createNewGeneration();
                }
                else
                {
                    displayedSnakeGame.GetSnakeGame().Alive = false;
                }
            }
            // Update snake DNA
            displayedSnakeGame.Restart(population[currentSnake], this);
        }
        else
        {
            displayedSnakeGame.UpdateSnake();
        }
    }

    private void checkAndHandleIfSnakeHasBestDNA(int snakeIndex)
    {
        if (snakeIndex == 0) { generationsLowestFitness = population[0].Fitness; }
        // Update statistics
        generationsBestFitness = MathF.Max(population[snakeIndex].Fitness, generationsBestFitness);
        generationsLowestFitness = MathF.Min(population[snakeIndex].Fitness, generationsLowestFitness);
        generationTotalFitness += population[snakeIndex].Fitness;
        // Determine if snake is the best candidate.
        if (population[snakeIndex].Fitness > bestDNA.Fitness)
        {
            // Store the dna (Once program is terminated, can then use the best DNA for the AI).
            // Could optionally serialize it as well.
            bestDNA = population[snakeIndex].Clone();
            bestFitnessGeneration = generation;

            // Data for comparing the active dna later
            bestDNA.GenerationNumber = generation;
            bestDNA.SnakeNumber = snakeIndex;
        }
    }
    private bool shouldSimulationTerminate()
    {
        if (GenerationLimitEnabled && generation > GenerationLimit)
        {
            simulationTerminated = true;
            return true;
        }

        return false;
    }
    private void createNewGeneration()
    {
        stopwatch.Stop();
        // Write to file 
        generationAverageFitness = generationTotalFitness / population.Length;
        // Write to File
        streamWriter.WriteLine(generation + "," + generationsBestFitness + "," + generationsLowestFitness + "," + generationAverageFitness +","+ stopwatch.Elapsed.TotalMilliseconds + "," + bestDNA.MostApplesEaten);

        // Create new population from previous generation.

        DNA[] newPopulation = new DNA[PopulationSize];

        // Sort in descending order of fitness
        System.Array.Sort(population, (a, b) => b.Fitness.CompareTo((a.Fitness)));

        int elitePopulation = (int)(PopulationSize * ElitistPopulationPercentage);
        if (ElitistPopulationPercentage > 0 && elitePopulation <= 0) { elitePopulation = 1; }
        for (int i = 0; i < PopulationSize; i++)
        {
            // Idea is that a percentage of the best populace won't be lost to random chance, but instead will be preserved and carried through generations until better are found.
            if (i < elitePopulation)
            {
                newPopulation[i] = population[i];
            }
            else
            {
                /// Selection
                DNA parent1 = chooseParent(random);
                DNA parent2 = chooseParent(random);

                /// Crossover
                DNA child = parent1.Crossover(parent2, random);

                /// Mutation
                // Increase mutation rate if no change detected for a significant period of time
                if (bestFitnessGeneration-generation >= 500)
                {
                    child.Mutate(MutationRate+0.5f, random);
                }
                else
                {
                    child.Mutate(MutationRate, random);
                }
                    

                newPopulation[i] = child;
            }
        }
        generation++;
        currentSnake = 0;
        generationsBestFitness = 0;
        generationTotalFitness = 0;
        generationAverageFitness = 0;
        generationsLowestFitness = 0;
        population = newPopulation;

        stopwatch = Stopwatch.StartNew();
    }

    // Purely for initialising a new gameObject, ideally this should only be called once and then the game should be restarted once finished.
    public void createSnake()
    {
        if (displayedSnakeGame != null) { Destroy(displayedSnakeGame.gameObject); }
        GameObject newSnake = Instantiate(SnakeGamePrefab);
        newSnake.transform.position = new Vector3(0.0f - (SceneSize.x/2), 0.0f - (SceneSize.y/2), 0.0f); // Centre the game on the screen
        TilemapSnakeGame newSnakeGame = newSnake.GetComponent<TilemapSnakeGame>();
        if (simulationTerminated) { newSnakeGame.Initialise(bestDNA.Clone(), this); }
        else { newSnakeGame.Initialise(population[currentSnake], this); }
        displayedSnakeGame = newSnakeGame;
    }

    private DNA chooseParent(System.Random random)
    {
        // Population should be sorted in descending order of fitness before calling this function.

        // Chooses a random parent from the fittest candidates of the population.
        int fittestPopulationLength = (int)(PopulationSize * SelectionPercentage);
        int parentIndex = random.Next(0, fittestPopulationLength);
        return population[parentIndex];
    }
}
