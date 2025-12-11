using System;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

/*
Use genes as weights for a neural network.

My initial attempt used integer genes in a range of -1 to 1, and I used that directly as the input for which direction the snake turned.
I now use them as weightings in a range of -1.0f to 1.0f and the neural network now determines the direction based on various inputs.
 */

public class DNA
{
    public float[] Genes;
    public float Fitness = 0.0f;

    // Values to use for ID or Stats: Allows me to check if DNA is the same as other dna, even when cloned.
    public int GenerationNumber = 0;
    public int SnakeNumber = 0;
    public int MostApplesEaten = 0;

    public DNA(int numGenes, System.Random random)
    {
        /// Initialize Genes
        // Should start with random inputs then converge on a solution over several generations of mutations and crossovers.

        Genes = new float[numGenes];    
        for (int i = 0; i < numGenes; i++)
        {
            Genes[i] = generateRandomGene(random);
        }
    }
    public DNA(int numGenes)
    {
        Genes = new float[numGenes];
    }

    private float generateRandomGene(System.Random random)
    {
        // Convert NextDouble() output into the range -1.0f to 1.0f.
        return (float)((random.NextDouble() * 2.0) - 1.0);
    }

    public DNA Clone()
    {
        DNA copy = new DNA(Genes.Length);
        copy.Genes = (float[])Genes.Clone();
        copy.Fitness = Fitness;
        copy.GenerationNumber = GenerationNumber;
        copy.SnakeNumber = SnakeNumber;
        copy.MostApplesEaten = MostApplesEaten;
        return copy;
    }

    public void Serialize(StreamWriter streamWriter, ref int numHiddenLayers, ref int numHiddenLayerNeurons)
    {
        streamWriter.WriteLine(Fitness.ToString());
        streamWriter.WriteLine(GenerationNumber.ToString());
        streamWriter.WriteLine(SnakeNumber.ToString());
        streamWriter.WriteLine(MostApplesEaten.ToString());

        streamWriter.WriteLine(Genes.Length);
        foreach (float gene in Genes)
        {
            streamWriter.WriteLine(gene.ToString());
        }

        streamWriter.WriteLine(numHiddenLayers);
        streamWriter.WriteLine(numHiddenLayerNeurons);
    }
    public void Deserialize(StreamReader streamReader, ref int numHiddenLayers, ref int numHiddenLayerNeurons)
    {
        Fitness = float.Parse(streamReader.ReadLine());
        GenerationNumber = int.Parse(streamReader.ReadLine());
        SnakeNumber = int.Parse(streamReader.ReadLine());
        MostApplesEaten = int.Parse(streamReader.ReadLine());

        int numberOfGenes = int.Parse(streamReader.ReadLine());
        Genes = new float[numberOfGenes];
        for(int i = 0; i < numberOfGenes; i++)
        {
            Genes[i] = float.Parse(streamReader.ReadLine());
        }

        // Check if this saveFile has the hidden layer settings written (This is for backward compatibility).
        if (streamReader.Peek() != -1)
        {
            numHiddenLayers = int.Parse(streamReader.ReadLine());
            numHiddenLayerNeurons = int.Parse(streamReader.ReadLine());
        }
    }

    public DNA Crossover (DNA otherParent, System.Random random)
    {
        DNA child = new DNA(Genes.Length);

        /// Uniform Crossover
        // 50/50 for each gene which parent will be used.
        for (int i = 0; i < Genes.Length; i++)
        {
            if (random.NextDouble() > 0.5f)
            {
                child.Genes[i] = otherParent.Genes[i];
            }
            else
            {
                child.Genes[i] = Genes[i];
            }
        }

        /// Single Point Crossover
        //int parent1Length = random.Next(0, genes.Length);
        //int parent2Length = otherParent.genes.Length - parent1Length;
        //for (int i = 0; i < parent1Length; i++)
        //{
        //    child.genes[i] = genes[i];
        //}
        //for (int i = parent1Length; i < parent2Length; i++)
        //{
        //    child.genes[i] = otherParent.genes[i];
        //}

        return child;
    }

    // Mutation rate between 0.0f and 1.0f
    public void Mutate(float mutationRate, System.Random random)
    {
        //for (int i = 0; i < genes.Length; i++)
        //{
        //    if (UnityEngine.Random.Range(0.0f, 1.0f) < mutationRate)
        //    {
        //        // Mutation Based of: https://medium.com/analytics-vidhya/genetic-algorithm-in-unity-using-c-72f0fafb535c

        //        int a = genes[i];
        //        if (i == genes.Length - 1)
        //        {
        //            genes[i] = genes[0];
        //            genes[0] = a;
        //        }
        //        else
        //        {
        //            genes[i] = genes[i + 1];
        //            genes[i + 1] = a;
        //        }
        //    }
        //}

        /// Random Resetting Mutation
        // One or several positions are randomly selected, a value is determined for each of these positions (In whatever range I use for my genes).

        for (int i = 0; i < Genes.Length; i++)
        {
            if (random.NextDouble() < mutationRate)
            {
                int geneToEdit = random.Next(Genes.Length);
                Genes[geneToEdit] = generateRandomGene(random);
            }
        }
    }
}
