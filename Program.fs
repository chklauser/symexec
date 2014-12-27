
type Term = int
type VariableStore = Map<string, Term>
type HeapLocation = { reference: Term ; field: string }
type HeapChunk = { location: HeapLocation ; value: Term }
type SymbolicState = { locals: VariableStore;  heap: (HeapChunk list) }

/// Type of a symbolic computation. May 'return' more than once if it branches.
type SymComp<'a> = SymbolicState -> (SymbolicState * 'a) list

module SymbolicExecution =

    /// This type implements the workflow expression (monadic do notation in F#)
    /// It's a combination of a state and list monad. 
    /// We use the state to track the execution environment (variables, heap)
    /// so that the user doesn't have to thread it through the program.
    /// The list-part of the monad is used to implement branching. By default
    /// a branching value causes the entire computation to branch.
    /// Other primitives can give the user more control over branching behaviour.
    type SymbolicExecutionBuilder() = 
        /// The monadic bind `am >>= f`
        /// Applies the cotinuation for each branch of `am`
        member s.Bind(am: 'a SymComp, f: 'a -> 'b SymComp): 'b SymComp = fun σ -> 
            let branches = am σ 
            branches |> List.collect (fun (σ',a) -> f a σ')
        /// The "lift" function. 
        member s.Return(a: 'a): 'a SymComp = fun σ -> [(σ, a)]
        // This is all we need to support monadic do notation. The following definitions
        // allow us to support more constructs in monadic do notation

        member s.For(xs: 'a seq, body: 'a -> unit SymComp): unit SymComp = 
            let rec iterate (xs: 'a list) = 
                match xs with 
                | x :: xs' -> s.Bind(body x, fun _ -> iterate xs')
                | [] -> s.Return ()
            iterate (List.ofSeq xs)

        /// Zero is used for if-without-else statements
        member s.Zero() = s.Return ()

        /// Statement sequence semantics: drop value but use each resulting state
        member s.Combine(am: 'a SymComp, bm: 'b SymComp): 'b SymComp = fun σ ->
            let branches = am σ
            branches |> List.collect (fun (σ',_) -> bm σ')

        /// Delay compensates for the fact that F# is evaluated eagerly.
        /// Since our monad is already represented by a function, we can simply
        /// use our ordinary SymComp function.
        member s.Delay(f: unit -> 'a SymComp) = fun σ -> f () σ

    /// Builder for symbolic execution computation expressions
    let symbolic = new SymbolicExecutionBuilder()

    // These are primites that manipulate symbolic state in the background. They bypass the monadic abstraction
    // to perform their work.

    /// Read a variable from the store
    let read_var name : Term SymComp = fun σ -> [(σ, σ.locals.Item(name))]

    /// Update a variable in the store
    let store_var name value : unit SymComp = fun σ -> 
        let σ' = { σ with locals = Map.add name value σ.locals }
        [(σ', ())]

    // We could also provide primitives for reading and replacing the entire local variable store/heap.
    // For this demonstration, they are not necessary.

    let add_chunk (chunk: HeapChunk) : unit SymComp = fun σ ->
        let σ' = { σ with heap = chunk :: σ.heap }
        [(σ', ())]

    /// Just write the heap chunk value, leaving hypothetical other fiels (say, permissions) intact
    let write_heap_value (loc: HeapLocation) (newValue: Term) : unit SymComp = fun σ ->
        let heap' = σ.heap |> List.map (fun ch ->
                if ch.location = loc
                    then { ch with value = newValue }
                    else ch
            )
        let σ' = { σ with heap = heap' }
        [(σ', ())]

    /// Compress heap
    // Note how, form the user's perspective, this primitive manipulates the heap as a side-effect
    // but since that change is threaded via the state monad, it's effects remain perfectly predictable
    let compress_heap : unit SymComp = fun σ -> 
        let heap' = σ.heap |> Seq.groupBy (fun ch -> ch.location) |> Seq.map (fun (loc, chs) -> 
                // here, we would unify the terms
                let unifiedTerm = chs |> Seq.map (fun ch -> ch.value) |> Seq.max
                { location = loc; value = unifiedTerm }
            )
        let σ' = { σ with heap = List.ofSeq heap' }
        [(σ', ())]

    let rec read_heap_value (loc: HeapLocation) : Term SymComp = fun σ ->
        let matching = σ.heap |> List.filter (fun ch -> ch.location = loc)
        match matching.Length with
        | 0 -> failwith "not found"
        | 1 -> [(σ, (List.head matching).value)]
        | _ -> // found more than one chunk: compress and retry
            symbolic.Combine(compress_heap, read_heap_value loc) σ

    /// Non-deterministic branch. Works a bit like fork: returns twice, once with true, once with false
    let nondeterministic_branch : bool SymComp = fun σ ->
        [σ,true; σ,false]

    /// Construct that more closely resembles the branch method from Silicon.
    /// The two branches are delimited, though.
    let branch f1 f2 = fun σ -> 
        [σ, (f1 σ, f2 σ)]
        


[<EntryPoint>]
let main argv = 
    printfn "%A" argv
    0 // return an integer exit code
