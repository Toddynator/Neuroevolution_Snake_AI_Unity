using System;
using System.Collections.Generic;
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
    public float[] genes;
    public float fitness = 0.0f;

    // Values to use for ID : Allows me to check if DNA is the same as other dna, even when cloned.
    public int generationNumber = 0;
    public int snakeNumber = 0;

    public DNA(int numGenes, System.Random random)
    {
        /// Initialize Genes
        // Should start with random inputs then converge on a solution over several generations of mutations and crossovers.

        genes = new float[numGenes];    
        for (int i = 0; i < numGenes; i++)
        {
            genes[i] = generateRandomGene(random);
        }
    }
    public DNA(int numGenes)
    {
        genes = new float[numGenes];
    }

    private float generateRandomGene(System.Random random)
    {
        // Convert NextDouble() output into the range -1.0f to 1.0f.
        return (float)((random.NextDouble() * 2.0) - 1.0);
    }

    public DNA Clone()
    {
        DNA copy = new DNA(genes.Length);
        copy.genes = (float[])genes.Clone();
        copy.fitness = fitness;
        return copy;
    }

    public DNA Crossover (DNA otherParent, System.Random random)
    {
        DNA child = new DNA(genes.Length);

        /// Uniform Crossover
        // 50/50 for each gene which parent will be used.
        for (int i = 0; i < genes.Length; i++)
        {
            if (random.NextDouble() > 0.5f)
            {
                child.genes[i] = otherParent.genes[i];
            }
            else
            {
                child.genes[i] = genes[i];
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

        for (int i = 0; i < genes.Length; i++)
        {
            if (random.NextDouble() < mutationRate)
            {
                int geneToEdit = random.Next(genes.Length);
                genes[geneToEdit] = generateRandomGene(random);
            }
        }
    }
}
