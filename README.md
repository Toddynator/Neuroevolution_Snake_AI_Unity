## CMP304_Assessment_Project
I made this for my AI University Module.
A Neuroevolution ai for Classic Snake. 
Neuroevolution is the combination of a genetic algorithm and a neural network, where the genetic algorithm is used to train the neural network.

For a 14x14 snake grid, I managed to achieve a snake ai that consumed 195 apples, reaching full length.
In random seeds, the snake ai still performed well, with around 30-70 apples being consumed consistently, as well as the snake being able to support varying sizes of grids.

# Most successful settings
For a 16x16 grid (14x14 playable area) with a fixed rng seed of '42': 
- 0 Hidden Layers
- 19 Input Neurons
- 3 Output Neurons
- 1000 Population
- 0.05 Mutation Rate
- 0.1 Selection (10%)
- 0.01 Elitist Selection (1%)
- 10 Score per Apple
- 1 Multiplier of score for progress to next apple (Progress is how close they were to the apple relative to the initial distance).
- 3 Score multiplier for optimal moves (I achieved 150 apples with this set to 0 so I don't think it is particularly effective).

# Findings
- Boolean Input Neurons are best for a neural network (even if there is already theoretically duplicate information being inputted). For example
the distances to obstacles are passed in, but also passing in booleans for whether
the snake was directly next to an obstacle had a massive effect, with 300+% increase in
fitness & apples eaten.
- Input neurons were the single biggest contributing factor to the success of the snake. The more I added the higher the average fitness was across runs.
- Converting input values into a consistent range made training far more consistent, AI seemed to perform better overall.
- ReLU Activation function for the neural network seemed to improve the performance of deep networks overall.
- Using a fixed RNG seed helped it most when training.
- A higher population made each training session more likely to get a higher fitness before converging.
- Hidden layers did not improve the ai and instead seemed detrimental, but did change the behaviour and strategies developed. More testing
will be required to determine if this actually results in a better ai or not.
- Parallelisation is a must for training a neural network.
- Most successful strategy adopted was a winding strategy, likely with the neural network taking into account the length of the snake.
Deep Networks that were trained seemed to generally adopt a more optimal moveset, achieving less apples and lower fitness scores, but
taking the most optimal path to apples, generally travelling in diagonal paths.

bestDNA1.dna save file:
<img width="1087" height="547" alt="FullGrid" src="https://github.com/user-attachments/assets/d5299ead-f6fe-42c2-afc8-a9015ea92eb9" />

Old DNA:
<img width="847" height="526" alt="150AppleBESTSNAKEYET" src="https://github.com/user-attachments/assets/e923e827-1762-4539-a023-37a9863bf6d4" />
<img width="1078" height="537" alt="158Apples" src="https://github.com/user-attachments/assets/e745fe86-d971-48c5-9a91-f0d6db522a7d" />

