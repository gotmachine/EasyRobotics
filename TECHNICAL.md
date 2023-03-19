# IK algorithms

I've tried implementing two iterative IK algorithms : gradient descent and CCD (Cyclic Coordinate Descent). 
Here is sumary of the strength and weakness of each, according to my findings.



### Gradient descent

Gradient descent works by minimizing the output of an error function. Each iteration can be summarized like this : 
- for each joint
- run the error function to get the intial error (distance/angle from target)
- apply an angle offset to the joint
- check the error again
- if the error has decreased, move the joint angle in the direction of the choosen offset
- if the error has increased, move the joint angle in the opposite direction of the choosen offset

Characteristics :

- Individual iterations are fast
- Amount of iterations required to converge is quite variable, and overall relatively high. 50+ iterations is usual, and 500+ not uncommon.
- By nature, the algorithm guarantees smooth movements between iterations, no matter how far the target is
- Good stability once the target is reached
- Defining an error function that behave linearly is difficult, especially when trying to combine distance and angle
- The algorithm has tuning knobs (angle offset per iteration, learning rate) that are also difficult to define
- Using an adaptative learning rate with large max values allow the algorithm to get out of local minimas and to keep searching for an alternative converging pose. But it can take a humongous amount of iterations to reach convergence, and can induce stability issues.

### CCD

Cyclic Coordinate Descent works by moving the joint "freely" toward the target, then to constrain the result back according to the joint axis and angle limits.

Characteristics :
- Individual iterations are relatively slow
- Amount of iterations required for convergence is pretty stable and relatively low. Near convergence is usually reached in less than 10 iterations.
- Movement is unpredictable and chaotic between iterations, especially when the target is far away
- In 5/6 DoF kinematic chains, the result is always unstable, the algorithm usually anneals around the target
- When the initial target distance is large, the algorithm is relatively good at finding a decent local minima, altough this is far from guaranteed.


### Getting out of local minimas

Both algorithms are iterative, and have a common issue : they will converge toward a local minima given the starting conditions (initial joint angles). Said otherwise, they often will get stuck in a suboptimal angular solution that will never converge. Finding the optimal solution (or at least a solution that reach the target) requires changing the starting conditions and check convergence, which can end up extremly computationally intensive.

From limited testing, to find a global minima with decent confidence, each joint need to be tested in at least 3 positions : a neutral (middle point between min and max), positive, negative. This quickly get out of hand computationally-wise : testing 3 solutions for a kinematic chain with 9 joints means 3^9 = 19683 combinations. In a basic C# implementation, this can end up taking a few minutes to complete. This being said, keeping that example, by carefully choosing the order in which combinations are tested (neutral first, then alternating pos/neg starting with joints closer to the root), a satisfactory minima is usually found in the first 100 iterations, and limited testing show that if no solution is found by the 1000th iteration, there is very rarely one latter.

# Implementation considerations and issues

Outside of expected issues and limitations with those IK algorithms, there is an additional problem in that KSP joints aren't rigid, far from it.
This mean it's impossible to run those IK algorithms continously based on the current joints/effector position/rotation, as there is considerable noise induced by physics deformations. This becomes exponentially bad when the target is close, with the IK algorithm inducing a self reinforcing oscillation. Solving this requires going into noise filtering or PID stuff, which are notoriously difficult to get working correctly when there is a lot of variance in initial parameters (variable kinematic chain size, weight, strength, purpose, amount of joints, etc).

For now, the solution is to have an abstracted, physics-unaffected representation of the kinematic chain, and to work on that one. This abstraction is kinda necessary anyway to be able to test IK solutions without actually moving the KSP servos. But since the kinematic chain is likely subject to external forces (gravity...), while our abstracted representation is spot-on the target, it's likely that the real chain won't be, so we still need to sync it back periodically. "Periodically" isn't really possible, as this is again subject to creating self reinforcing oscilations.