using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

/*
In classic snake, the snake can only 2 turn directions, it can't go backwards.
It can also choose to keep moving forward. With this in mind, I can represent input as a simple
integer of -1 to 1, with 0 representing moving forward without turning.
 */

public class DNA
{
    public int[] genes;
    public float fitness = 0.0f;

    public DNA(int numGenes)
    {
        /// Initialize Genes
        // Should start with random inputs then converge on a solution over several generations of mutations and crossovers.

        genes = new int[numGenes];    
        for (int i = 0; i < numGenes; i++)
        {
            genes[i] = UnityEngine.Random.Range(-1, 2);
        }
    }

    public DNA Crossover (DNA otherParent)
    {
        DNA child = new DNA(genes.Length);

        /// Uniform Crossover
        // 50/50 for each gene which parent will be used.
        for (int i = 0; i < genes.Length; i++)
        {
            if (UnityEngine.Random.value > 0.5f)
            {
                child.genes[i] = otherParent.genes[i];
            }
            else
            {
                child.genes[i] = genes[i];
            }
        }

        /// Single Point Crossover
        //int parent1Length = UnityEngine.Random.Range(0, genes.Length);
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
    public void Mutate(float mutationRate)
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
            if (UnityEngine.Random.Range(0.0f, 1.0f) < mutationRate)
            {
                int geneToEdit = UnityEngine.Random.Range(0, genes.Length);
                genes[geneToEdit] = UnityEngine.Random.Range(-1, 2);
            }
        }
    }
}
