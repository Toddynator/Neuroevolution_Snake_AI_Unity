using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor.Rendering.Universal;
using UnityEngine.InputSystem.Users;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

/*
 NEURAL NETWORK
 * Designed to use Genetic Algorithms for Training instead of Back Propogation and Loss Calculation.
 * 
 * I chose to have each hidden layer be the same size for simplicity.
 * I use Sigmoid for the Activation function (first I came across).
 * Source: https://www.geeksforgeeks.org/machine-learning/neural-networks-a-beginners-guide/
 * 
 * biases + values represent a neuron.
 * weights represents the connections from each neuron to the neuron in the next layer.
 * 
 * Should ensure that number of genes is equal to the number of connections to get best results.
 * Formula for the number required:
 * numberOfInputNeurons*numberOfHiddenLayerNeurons + numberOfHiddenLayers*numberOfHiddenLayerNeurons*numberOfHiddenLayerNeurons + numberOfHiddenLayerNeurons*numberOfOutputNeurons;
 * 
 * My weights are in the range of -1.0f to 1.0f
 * 
 * INPUTS SHOULD BE IN THE RANGE FROM 0.0f TO 1.0f.
 * The input range should match the Activation Function I use, in my case Sigmoid which gives a range from 0.0f to 1.0f.
 */

public class NeuralNetwork
{
    /// SETTINGS

    private int numberOfInputNeurons = 1;
    private int numberOfOutputNeurons = 1;
    private int numberOfHiddenLayers = 0;
    private int numberOfHiddenLayerNeurons = 1;

    /// NEURAL NETWORK

    private float[][][] weights; // Layer Index, Neuron Index in current Layer, Neuron Index in the Next Layer. Represents connections, stores weight for each connection. Doesn't need output layer.
    private float[][] biases; // Bias for each neuron. Layer, Neuron in Layer.
    private float[][] values; // The stored / calculated value for each neuron. Layer, Neuron in Layer.



    ///////////////
    ///// FUNCTIONS
    


    public NeuralNetwork(DNA dna, int numInputNeurons, int numOutputNerons, int numHiddenLayers, int numHiddenLayerNeurons)
    {
        //// Initialisation network

        numberOfInputNeurons = numInputNeurons;
        numberOfOutputNeurons = numOutputNerons;
        numberOfHiddenLayers = numHiddenLayers;
        numberOfHiddenLayerNeurons = numHiddenLayerNeurons;

        int outputLayerNumber = 1 + numberOfHiddenLayers;
        int totalLayers = 2 + numberOfHiddenLayers;

        // Calculate neurons per layer
        int[] neuronsPerLayer = new int[numberOfHiddenLayers+2];
        neuronsPerLayer[0] = numberOfInputNeurons;
        neuronsPerLayer[outputLayerNumber] = numberOfOutputNeurons;
        for (int i = 1; i < numberOfHiddenLayers+1; i++)
        {
            neuronsPerLayer[i] = numberOfHiddenLayerNeurons;
        }

        /// VALIDATION
        // Should check that the number of genes is greater or equal to the total number of weights (+ biases if I decide to have them modifed as well).
        // Ideally the application should ensure that geneNumber is always set to the required number, and instead have settings for the neural network which automatically calculate
        // the number of genes to use.

        /*int numGenes = dna.genes.Length;
        int genesRequired = numberOfInputNeurons*numberOfHiddenLayerNeurons + numberOfHiddenLayers*numberOfHiddenLayerNeurons*numberOfHiddenLayerNeurons + numberOfHiddenLayerNeurons*numberOfOutputNeurons;
        if (numGenes < genesRequired)
        {
            
        }*/

        /// Set Weights, Biaases & Values
        // Initialises weights to dna gene values.  

        // Set weights for every layer except the output layer, which doesn't need weights.
        int geneIndex = 0;
        weights = new float[totalLayers-1][][]; // Not needed for output layer
        biases = new float[totalLayers][]; // Not needed for input layer, but I'm initialising it anyway otherwise it gets too confusing.
        values = new float[totalLayers][];
        for (int layerNum = 0; layerNum < neuronsPerLayer.Length ; layerNum++)
        {
            bool finalLayer = layerNum == neuronsPerLayer.Length - 1;
            int numNeuronsCurrentLayer = neuronsPerLayer[layerNum];

            // Set the index of the neuron belonging to the layer

            if (!finalLayer) { weights[layerNum] = new float[numNeuronsCurrentLayer][]; }
            biases[layerNum] = new float[numNeuronsCurrentLayer];
            values[layerNum] = new float[numNeuronsCurrentLayer];
            // Set the index of the neuron in the next layer
            for (int neuronNum = 0; neuronNum < numNeuronsCurrentLayer; neuronNum++)
            {
                // Set Weights
                if (!finalLayer)
                {
                    int numNeuronsNextLayer = neuronsPerLayer[layerNum + 1];
                    weights[layerNum][neuronNum] = new float[numNeuronsNextLayer];
                    for (int connectionNum = 0; connectionNum < numNeuronsNextLayer; connectionNum++)
                    {
                        // Use Genes for the value of the neuron connection weights
                        // Modulus ensures it doesn't throw an error if not enough genes are created.
                        weights[layerNum][neuronNum][connectionNum] = dna.genes[geneIndex % dna.genes.Length];
                        geneIndex++;
                    }
                }

                // Set Biases & Values
                biases[layerNum][neuronNum] = 0.0f;
                values[layerNum][neuronNum] = 0.0f;
            }
        }
    }

    // Call on each frame, set inputs before calling.
    public void CalculateOutputs()
    {
        // This forward propogates the Input Data and calculates Output Data which can then be used by the application.
        // The values are calculated for every neuron in a layer before moving to the next layer. Each layer depends
        // on values from the previous layer. The Input layer should be set externally before calling this function.

        for (int layerNum = 1; layerNum < weights.Length; layerNum++)
        {
            int numNeuronsCurrentLayer = weights[layerNum].Length;
            for (int neuronNum = 0; neuronNum < numNeuronsCurrentLayer; neuronNum++)
            {
                values[layerNum][neuronNum] = activationFunction(linearTransformation(layerNum, neuronNum));
            }
        }
    }

    // Must be called before the activation function, should be called for every neuron in one layer before being called for the next.
    private float linearTransformation(int layerIndex, int neuronIndex)
    {
        // Sums together the output of each neuron on the previous layer multiplied by the weight associated with the
        // connection from that previous neuron to the current neuron. Then adds a bias associated with the current neuron.

        float output = 0.0f;
        for (int previousLayerNeuronNum = 0; previousLayerNeuronNum < weights[layerIndex-1].Length; previousLayerNeuronNum++)
        {
            output += (weights[layerIndex - 1][previousLayerNeuronNum][neuronIndex] * values[layerIndex-1][previousLayerNeuronNum]);
        }
        output += biases[layerIndex][neuronIndex];
        return output;
    }

    // Returns a value 0.f to 1.f
    private float activationFunction(float linearTransformationValue)
    {
        // https://machinelearningmastery.com/a-gentle-introduction-to-sigmoid-function/
        // I used the sigmoid function for this.
        return 1.0f / (1.0f + MathF.Exp(-linearTransformationValue));
    }

    // Should be called before Calculating the Outputs / Forward Propogating.
    public void SetInputs(float[] inputs)
    {
        Array.Copy(inputs, values[0], values[0].Length);
    }
    public float[] GetOutputs()
    {
        return values[values.Length - 1];
    }
}
