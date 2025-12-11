using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
//using UnityEngine;

/*
 NEURAL NETWORK
 * Designed to use Genetic Algorithms for Training instead of Back Propogation and Loss Calculation.
 * 
 * I chose to have each hidden layer be the same size for simplicity.
 * I use Sigmoid for the Activation function due to its range of outputs being 0 and 1 which was perfect for probability testing in the outputs.
 * Source: https://www.geeksforgeeks.org/machine-learning/neural-networks-a-beginners-guide/
 * 
 * biases + values represent a neuron.
 * weights represents the connections from each neuron to the neuron in the next layer.
 * 
 * My weights are in the range of -1.0f to 1.0f
 * 
 * INPUTS SHOULD BE IN THE RANGE FROM 0.0f TO 1.0f.
 * The input range should match the Activation Function I use, in my case Sigmoid which gives a range from 0.0f to 1.0f.
 * 
 * I may consider using ReLu but I'm unsure about it providing outputs that aren't in a set range.
 * Although I guess it could still work for the snake turning, since all I need to do is compare the size of each to get the highest value.
 * Not sure how my inputs should be handled in this case however.
 * 
 * I used to store weights and biases as jagged arrays, but I've now opted to use the genes directly with a simple gene index counter which has drastically simplified
 * the network.
 */

public class NeuralNetwork
{
    /// SETTINGS

    private int numberOfInputNeurons = 1;
    private int numberOfOutputNeurons = 1;
    private int numberOfHiddenLayers = 0;
    private int numberOfHiddenLayerNeurons = 1;
    private int outputLayerNumber = 2;
    private int totalLayers = 2;

    /// NEURAL NETWORK

    private float[][] values; // The stored / calculated value for each neuron. Layer, Neuron in Layer. All Layers.
    DNA dna;
    private int geneIndex = 0;

    public NeuralNetwork(DNA newDna, int numInputNeurons, int numOutputNeurons, int numHiddenLayers, int numHiddenLayerNeurons)
    {
        //// Initialisation network

        dna = newDna;
        numberOfInputNeurons = numInputNeurons;
        numberOfOutputNeurons = numOutputNeurons;
        numberOfHiddenLayers = numHiddenLayers;
        numberOfHiddenLayerNeurons = numHiddenLayerNeurons;

        outputLayerNumber = 1 + numberOfHiddenLayers;
        totalLayers = 2 + numberOfHiddenLayers;

        // Calculate neurons per layer
        int[] neuronsPerLayer = new int[numberOfHiddenLayers+2];
        neuronsPerLayer[0] = numberOfInputNeurons;
        neuronsPerLayer[outputLayerNumber] = numberOfOutputNeurons;
        for (int i = 1; i < numberOfHiddenLayers+1; i++)
        {
            neuronsPerLayer[i] = numberOfHiddenLayerNeurons;
        }

        /// Create Value Arrays

        values = new float[totalLayers][];
        for (int layerNum = 0; layerNum < neuronsPerLayer.Length ; layerNum++)
        {
            int numNeuronsCurrentLayer = neuronsPerLayer[layerNum];
            values[layerNum] = new float[numNeuronsCurrentLayer];
        }
    }

    static private int calculateNumberOfGenesForWeights(int numHiddenLayers, int numHiddenLayerNeurons, int numInputNeurons, int numOutputNeurons)
    {
        if (numHiddenLayers > 0)
        {
            return numInputNeurons * numHiddenLayerNeurons + (numHiddenLayers-1) * numHiddenLayerNeurons * numHiddenLayerNeurons + numHiddenLayerNeurons * numOutputNeurons;
        }
        return numInputNeurons * numOutputNeurons;
    }

    static public int CalculateNumberOfGenesForNeuralNetwork(int numHiddenLayers, int numHiddenLayerNeurons, int numInputNeurons, int numOutputNeurons)
    {
        int genesForBiases = numHiddenLayers * numHiddenLayerNeurons + numOutputNeurons;
        int genesForWeights = calculateNumberOfGenesForWeights(numHiddenLayers, numHiddenLayerNeurons, numInputNeurons, numOutputNeurons);
        return genesForBiases + genesForWeights;        
    }

    // Call on each frame, set inputs before calling.
    public void CalculateOutputs()
    {
        // This forward propogates the Input Data and calculates Output Data which can then be used by the application.
        // The values are calculated for every neuron in a layer before moving to the next layer. Each layer depends
        // on values from the previous layer. The Input layer should be set externally before calling this function.

        geneIndex = 0;
        for (int layerNum = 1; layerNum < totalLayers; layerNum++)
        {
            int numNeuronsCurrentLayer = values[layerNum].Length;
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
        int numNeurons = layerIndex - 1 == 0 ? numberOfInputNeurons : numberOfHiddenLayerNeurons;
        for (int previousLayerNeuronNum = 0; previousLayerNeuronNum < numNeurons; previousLayerNeuronNum++)
        {
            output += (dna.Genes[geneIndex] * values[layerIndex - 1][previousLayerNeuronNum]);
            geneIndex++;
        }
        output += dna.Genes[geneIndex];
        geneIndex++; // Weight for next linearTransformation should be on the next index
        return output;
    }

    // Returns a value 0.f to 1.f
    private float activationFunction(float linearTransformationValue)
    {
        // https://machinelearningmastery.com/a-gentle-introduction-to-sigmoid-function/
        // I used the sigmoid function for this.
        //return 1.0f / (1.0f + MathF.Exp(-linearTransformationValue));

        // ReLU, ensures values are always positive and is less expensive to compute. Also seems to provide the best results in Deep Networks (Hidden Layers).
        return MathF.Max(0, linearTransformationValue);

        // TanH ~ Shifts Sigmoid into -1 to 1 range, slightly more expensive.
        //return (2.0f / (1 + MathF.Exp(-2 * linearTransformationValue))) - 1;
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
