## Symbolic Execution with Monads

A symbolic execution engine can be built around a combined *state* and *list* monad. We use the state part to hold things like the local variable store and the symbolic heap, but potentially also additional data structures built during verification. The list part implements branching.

### Branching
When we need to branch symbolic execution, we literally want a part of our program to *return twice*. In CPS, we cal the continuation passed to us a second time. With the list monad, we return a list containing multiple return values and the monad will automatically instantiate the rest of the computation once for each returned value.

    cond <- equals(a,b)
    if cond
      then A
      else B

Here, if we don't have enough information about `a` and `b`, we just return `[true, false]` from the `equals` function.
The rest of the program will be executed once for `cond == true` and once for `cond == false`.

### Primitives
The author of the monad can introduce additional primitives that can bypass the monadic abstraction. It would, for instance, make sense not to expose the exact representation of the monad's state to the user and instead only provide primitives for the individula components.

    store_var "x" 15
    y <- load_var "y"
    yf <- load_heap y "f"

Other primitives could give the user more control over how branching behaves. 

    (b1, b2) <- branch 
      (λ. {… branch1 …})
      (λ. {… branch2 …})
    if b1 == b2
    	…

### Strengths
The main advantage of this approach is that much of the internals of the symbolic execution engine's core is hidden away from the user.

### Weaknesses

 * Without a do-notation (like in F#, Haskell), still lots of lambdas involved
 * Control flow that deviate from "ordinary" branching often impossible. The `try` mechanism is one example.
 * No short-circuiting by default

### Exotic control flow (try-or-retry, short circuit on error)
Control flow that deviates from "normal branching" cannot be represented in the base monad presented here. It is possible to extend it to be able to support some of these, but each new 'exotic' control flow pattern will require a new extension.

Instead of returning lists of values, we would return lists of 'outcomes'. An outcome could be
 * a bare value
 * a retry-guarded value along with a retry-continuation
 * a fatal error message

 The monad would then be hard-coded to short-circuit on error message outcomes. 
