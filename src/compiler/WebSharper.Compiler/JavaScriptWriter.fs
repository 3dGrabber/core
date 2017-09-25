﻿// $begin{copyright}
//
// This file is part of WebSharper
//
// Copyright (c) 2008-2016 IntelliFactory
//
// Licensed under the Apache License, Version 2.0 (the "License"); you
// may not use this file except in compliance with the License.  You may
// obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
// implied.  See the License for the specific language governing
// permissions and limitations under the License.
//
// $end{copyright}

// Transforms WebSharper.Core.AST into WebSharper.JavaScript.Syntax
// used for writing .js files
module WebSharper.Compiler.JavaScriptWriter

module J = WebSharper.Core.JavaScript.Syntax
module I = WebSharper.Core.JavaScript.Identifier

open WebSharper.Core
open WebSharper.Core.AST

type P = WebSharper.Core.JavaScript.Preferences

let defaultNames = Set [ "window" ] 

type Environment =
    {
        Preference : WebSharper.Core.JavaScript.Preferences
        mutable ScopeNames : Set<string>
        mutable CompactVars : int
        mutable ScopeIds : Map<Id, string>
        ScopeVars : ResizeArray<J.Id>
        FuncDecls : ResizeArray<J.Statement>
        mutable InFuncScope : bool
        OuterScope : bool
    }
    static member New(pref) =
        {
            Preference = pref    
            ScopeNames = defaultNames
            CompactVars = 0 
            ScopeIds = Map [ Id.Global(), "<any>window" ] 
            ScopeVars = ResizeArray()
            FuncDecls = ResizeArray()
            InFuncScope = false
            OuterScope = true
        }

    member this.NewInner(?ns) =
        let outer = defaultArg ns false
        {
            Preference = this.Preference    
            ScopeNames = if outer then defaultNames else this.ScopeNames
            CompactVars = this.CompactVars
            ScopeIds = this.ScopeIds
            ScopeVars = ResizeArray()
            FuncDecls = ResizeArray()
            InFuncScope = true
            OuterScope = outer
        }

    member this.Declarations =
        if this.ScopeVars.Count = 0 then [] else
            [ J.Vars (this.ScopeVars |> Seq.map (fun v -> v, None) |> List.ofSeq) ]
        
let undef = J.Unary(J.UnaryOperator.``void``, J.Constant (J.Literal.Number "0"))

let transformId (env: Environment) (id: Id) =
    if id.HasStrongName then " " + id.Name.Value else
    try Map.find id env.ScopeIds
    with _ -> 
        //"MISSINGVAR" + I.MakeValid (defaultArg id.Name "_")
        failwithf "Undefined variable during writing JavaScript: %s" (string id)

let formatter = WebSharper.Core.JavaScript.Identifier.MakeFormatter()

let getCompactName (env: Environment) =
    let vars = env.ScopeNames
    let mutable name = formatter env.CompactVars   
    env.CompactVars <- env.CompactVars + 1   
    while vars |> Set.contains name do
        name <- formatter env.CompactVars   
        env.CompactVars <- env.CompactVars + 1   
    name

type IdKind =
    | VarDeclId
    | FuncArgId
    | InnerId

let defineId (env: Environment) kind (id: Id) =
    if id.HasStrongName then " " + id.Name.Value else
    let addToDecl, isParam =
        match kind with
        | VarDeclId -> false, false
        | FuncArgId -> false, true
        | InnerId -> true, false
    let res =
        if env.Preference = P.Compact then
            let name = getCompactName env    
            env.ScopeIds <- env.ScopeIds |> Map.add id name
            if addToDecl then env.ScopeVars.Add(name)
            name 
        else 
            let vars = env.ScopeNames
            let mutable name = (I.MakeValid (defaultArg id.Name "$1"))
            while vars |> Set.contains name do
                name <- Resolve.newName name 
            env.ScopeNames <- vars |> Set.add name
            env.ScopeIds <- env.ScopeIds |> Map.add id name
            if addToDecl then env.ScopeVars.Add(name)
            name
    if isParam && id.IsOptional then
        res + "?"
    else
        res
       
let invalidForm c =
    failwithf "invalid form at writing JavaScript: %s" c

type CollectVariables(env: Environment) =
    inherit StatementVisitor()

    override this.VisitFuncDeclaration(f, _, _) =
        defineId env FuncArgId f |> ignore    

    override this.VisitVarDeclaration(v, _) =
        defineId env InnerId v |> ignore

    override this.VisitNamespace(n, s) =
        env.ScopeNames <- env.ScopeNames |> Set.add n
        s |> List.iter this.VisitStatement

let flattenJS s =
    let res = ResizeArray()
    let rec add a =
        match J.IgnoreStatementPos a with
        | J.Block b -> b |> List.iter add
        | J.Empty -> ()
        | _ -> res.Add a
    s |> Seq.iter add
    List.ofSeq res    

let flattenFuncBody s =
    let b = flattenJS [s]
    match List.rev b with
    | J.IgnoreSPos (J.Return None) :: more -> List.rev more
    | _ -> b

let block s = J.Block (flattenJS s)

let rec transformExpr (env: Environment) (expr: Expression) : J.Expression =
    let inline trE x = transformExpr env x
    let inline trI x = transformId env x
    match expr with
    | Undefined -> undef
    | This -> J.This
    | Base -> J.Super
    | Arguments -> J.Var "arguments"
    | Var id -> J.Var (trI id)
    | Value v ->
        match v with
        | Null     -> J.Literal.Null
        | Bool   v -> if v then J.Literal.True else J.Literal.False
        | Byte   v -> J.Number (string v)
        | Char   v -> J.String (string v)
        | Double v -> J.Number (string v)
        | Int    v -> J.Number (string v)
        | Int16  v -> J.Number (string v)
        | Int64  v -> J.Number (string v)
        | SByte  v -> J.Number (string v)
        | Single v -> J.Number (string v)
        | String v -> J.String v
        | UInt16 v -> J.Number (string v)
        | UInt32 v -> J.Number (string v)
        | UInt64 v -> J.Number (string v)
        | Decimal _ -> failwith "Cannot write Decimal directly to JavaScript output"
        |> J.Constant
    | Application (e, ps, _, _) -> J.Application (trE e, ps |> List.map trE)
    | VarSet (id, e) -> J.Binary(J.Var (trI id), J.BinaryOperator.``=``, trE e)   
    | ExprSourcePos (pos, e) -> 
        let jpos =
            {
                File = pos.FileName
                Line = fst pos.Start
                Column = snd pos.Start
                EndLine = fst pos.End
                EndColumn = snd pos.End
            } : J.SourcePos
        J.ExprPos (J.IgnoreExprPos(trE e), jpos)
    | Function (ids, b) ->
        let innerEnv = env.NewInner()
        let args = ids |> List.map (defineId innerEnv FuncArgId) 
        CollectVariables(innerEnv).VisitStatement(b)
        let body = b |> transformStatement innerEnv |> flattenFuncBody
        //let useStrict =
        //    if env.OuterScope then
        //        [ J.Ignore (J.Constant (J.String "use strict")) ]
        //    else []
        J.Lambda(None, args, flattenJS (innerEnv.Declarations @ body))
    | ItemGet (x, y, _) 
        -> (trE x).[trE y]
    | Binary (x, y, z) ->
        match y with
        | BinaryOperator.``!==``        -> J.Binary(trE x, J.BinaryOperator.``!==``       , trE z)
        | BinaryOperator.``!=``         -> J.Binary(trE x, J.BinaryOperator.``!=``        , trE z)
        | BinaryOperator.``%``          -> J.Binary(trE x, J.BinaryOperator.``%``         , trE z)
        | BinaryOperator.``&&``         -> J.Binary(trE x, J.BinaryOperator.``&&``        , trE z)
        | BinaryOperator.``&``          -> J.Binary(trE x, J.BinaryOperator.``&``         , trE z)
        | BinaryOperator.``*``          -> J.Binary(trE x, J.BinaryOperator.``*``         , trE z)
        | BinaryOperator.``+``          -> J.Binary(trE x, J.BinaryOperator.``+``         , trE z)
        | BinaryOperator.``-``          -> J.Binary(trE x, J.BinaryOperator.``-``         , trE z)
        | BinaryOperator.``/``          -> J.Binary(trE x, J.BinaryOperator.``/``         , trE z)
        | BinaryOperator.``<<``         -> J.Binary(trE x, J.BinaryOperator.``<<``        , trE z)
        | BinaryOperator.``<=``         -> J.Binary(trE x, J.BinaryOperator.``<=``        , trE z)
        | BinaryOperator.``<``          -> J.Binary(trE x, J.BinaryOperator.``<``         , trE z)
        | BinaryOperator.``===``        -> J.Binary(trE x, J.BinaryOperator.``===``       , trE z)
        | BinaryOperator.``==``         -> J.Binary(trE x, J.BinaryOperator.``==``        , trE z)
        | BinaryOperator.``>=``         -> J.Binary(trE x, J.BinaryOperator.``>=``        , trE z)
        | BinaryOperator.``>>>``        -> J.Binary(trE x, J.BinaryOperator.``>>>``       , trE z)
        | BinaryOperator.``>>``         -> J.Binary(trE x, J.BinaryOperator.``>>``        , trE z)
        | BinaryOperator.``>``          -> J.Binary(trE x, J.BinaryOperator.``>``         , trE z)
        | BinaryOperator.``^``          -> J.Binary(trE x, J.BinaryOperator.``^``         , trE z)
        | BinaryOperator.``in``         -> J.Binary(trE x, J.BinaryOperator.``in``        , trE z)
        | BinaryOperator.``instanceof`` -> J.Binary(trE x, J.BinaryOperator.``instanceof``, trE z)
        | BinaryOperator.``|``          -> J.Binary(trE x, J.BinaryOperator.``|``         , trE z)
        | BinaryOperator.``||``         -> J.Binary(trE x, J.BinaryOperator.``||``        , trE z)
        | _ -> failwith "invalid BinaryOperator enum value"
    | ItemSet(x, y, z) -> (trE x).[trE y] ^= trE z
    | MutatingBinary(x, y, z) ->
        match y with
        | MutatingBinaryOperator.``=``    -> J.Binary(trE x, J.BinaryOperator.``=``    , trE z)
        | MutatingBinaryOperator.``+=``   -> J.Binary(trE x, J.BinaryOperator.``+=``   , trE z)
        | MutatingBinaryOperator.``-=``   -> J.Binary(trE x, J.BinaryOperator.``-=``   , trE z)
        | MutatingBinaryOperator.``*=``   -> J.Binary(trE x, J.BinaryOperator.``*=``   , trE z)
        | MutatingBinaryOperator.``/=``   -> J.Binary(trE x, J.BinaryOperator.``/=``   , trE z)
        | MutatingBinaryOperator.``%=``   -> J.Binary(trE x, J.BinaryOperator.``%=``   , trE z)
        | MutatingBinaryOperator.``&=``   -> J.Binary(trE x, J.BinaryOperator.``&=``   , trE z)
        | MutatingBinaryOperator.``^=``   -> J.Binary(trE x, J.BinaryOperator.``^=``   , trE z)
        | MutatingBinaryOperator.``|=``   -> J.Binary(trE x, J.BinaryOperator.``|=``   , trE z)
        | MutatingBinaryOperator.``<<=``  -> J.Binary(trE x, J.BinaryOperator.``<<=``  , trE z)
        | MutatingBinaryOperator.``>>=``  -> J.Binary(trE x, J.BinaryOperator.``>>=``  , trE z)
        | MutatingBinaryOperator.``>>>=`` -> J.Binary(trE x, J.BinaryOperator.``>>>=`` , trE z)
        | _ -> failwith "invalid MutatingBinaryOperator enum value"
    | Object fs -> J.NewObject (fs |> List.map (fun (k, v) -> k, trE v))
    | New (x, y) -> J.New(trE x, y |> List.map trE)
    | Sequential x ->
        let x =
            match List.rev x with 
            | [] | [_] -> x
            | h :: t ->
                h :: (t |> List.map (function (IgnoreSourcePos.Unary(UnaryOperator.``void``, a)) | a -> a))  
                |> List.rev
        x |> List.map trE |> List.reduce (fun a b -> J.Binary(a, J.BinaryOperator.``,``, b))
    | Conditional (cond, then_, else_) ->
        J.Conditional(trE cond, trE then_, trE else_)
    | NewArray arr -> J.NewArray (List.map (trE >> Some) arr)
    | Unary(x, y) ->
        match x with
        | UnaryOperator.``!``    -> J.Unary(J.UnaryOperator.``!``, trE y)
        | UnaryOperator.``void`` -> J.Unary(J.UnaryOperator.``void``, trE y)
        | UnaryOperator.``+``    -> J.Unary(J.UnaryOperator.``+``, trE y)
        | UnaryOperator.``-``    -> J.Unary(J.UnaryOperator.``-``, trE y)
        | UnaryOperator.``~``    -> J.Unary(J.UnaryOperator.``~``, trE y)
        | UnaryOperator.typeof   -> J.Unary(J.UnaryOperator.typeof, trE y)
        | _ -> failwith "invalid UnaryOperator enum value"
    | MutatingUnary(x, y) ->
        match x with
        | MutatingUnaryOperator.``()++`` -> J.Postfix(trE y, J.PostfixOperator.``++``)
        | MutatingUnaryOperator.``()--`` -> J.Postfix(trE y, J.PostfixOperator.``--``)
        | MutatingUnaryOperator.``++()`` -> J.Unary(J.UnaryOperator.``++``, trE y)
        | MutatingUnaryOperator.``--()`` -> J.Unary(J.UnaryOperator.``--``, trE y)
        | MutatingUnaryOperator.delete   -> J.Unary(J.UnaryOperator.delete, trE y)
        | _ -> failwith "invalid MutatingUnaryOperator enum value"
    | GlobalAccess { Module = StandardLibrary | CurrentModule; Address = a } ->
        let rec get a =
            match a with
            | [] -> failwith "empty local address"
            | [ n ] -> J.Var n
            | n :: r -> (get r).[J.Constant (J.String n)]
        get a.Value
    | _ -> 
        invalidForm (GetUnionCaseName expr)

and transformStatement (env: Environment) (statement: Statement) : J.Statement =
    let inline trE x = transformExpr env x
    let inline trS x = transformStatement env x
    let sequential s effect =
        match List.rev s with
        | h :: t -> effect h :: List.map ExprStatement t |> List.rev          
        | [] -> []
    let sequentialE s =
        sequential s (function IgnoreSourcePos.Unary(UnaryOperator.``void``, e) | e -> ExprStatement e)    
    let flatten s =
        let res = ResizeArray()
        let mutable go = true 
        let rec add a =
            if go then 
                match IgnoreStatementSourcePos a with 
                | Block b -> b |> List.iter add
                | Empty 
                | ExprStatement IgnoreSourcePos.Undefined -> ()
                | ExprStatement (IgnoreSourcePos.Sequential s) ->
                    sequentialE s |> List.iter add
                | Return (IgnoreSourcePos.Sequential s) ->
                    sequential s Return |> List.iter add
                    go <- false
                | Throw (IgnoreSourcePos.Sequential s) ->
                    sequential s Throw |> List.iter add
                    go <- false
                | Return _ 
                | Throw _
                | Break _
                | Continue _ ->
                    res.Add(trS a)
                    go <- false
                | _ -> 
                    res.Add(trS a)
        s |> List.iter add
        List.ofSeq res    
    // collect function declarations to be on top level of functions to satisfy strict mode
    // requirement by some JavaScript engines
    let withFuncDecls f =
        if env.InFuncScope then
            env.InFuncScope <- false
            let woFuncDecls = f()
            env.InFuncScope <- true
            if env.FuncDecls.Count > 0 then
                let res = block (Seq.append env.FuncDecls (Seq.singleton woFuncDecls))
                env.FuncDecls.Clear()
                res
            else woFuncDecls
        else 
            f()
    match statement with
    | Empty -> J.Empty
    | Break(a) -> J.Break (a |> Option.map (fun l -> l.Name.Value))
    | Continue(a) -> J.Continue (a |> Option.map (fun l -> l.Name.Value))
    | ExprStatement (IgnoreSourcePos.Unary(UnaryOperator.``void``, (IgnoreSourcePos.Sequential s)))
    | ExprStatement (IgnoreSourcePos.Sequential s) -> block (sequentialE s |> List.map trS)
    | ExprStatement (IgnoreSourcePos.Unary(UnaryOperator.``void``, e))
    | ExprStatement e -> J.Ignore(trE e)
    | Block s -> block (flatten s)
    | StatementSourcePos (pos, s) -> 
        let jpos =
            {
                File = pos.FileName
                Line = fst pos.Start
                Column = snd pos.Start
                EndLine = fst pos.End
                EndColumn = snd pos.End
            } : J.SourcePos
        J.StatementPos (J.IgnoreStatementPos (trS s), jpos)
    | If(a, b, c) -> 
        withFuncDecls <| fun () -> 
            J.If(trE a, trS b, trS c)
    | Return (IgnoreSourcePos.Unary(UnaryOperator.``void``, a)) -> block [ J.Ignore(trE a); J.Return None ]
    | Return (IgnoreSourcePos.Sequential s) -> block (sequential s Return |> List.map trS)
    | Return IgnoreSourcePos.Undefined -> J.Return None
    | Return a -> J.Return (Some (trE a))
    | VarDeclaration (id, e) ->
        if env.OuterScope then
            match e with
            | IgnoreSourcePos.Undefined -> J.Vars [ transformId env id, None ]
            | _ -> J.Vars [ transformId env id, Some (trE e) ]
        else
            match e with
            | IgnoreSourcePos.Undefined -> J.Empty 
            | _ -> J.Ignore(J.Binary(J.Var (transformId env id), J.BinaryOperator.``=``, trE e))
    | FuncDeclaration (x, ids, b) ->
        let id = transformId env x
        let innerEnv = env.NewInner()
        let args = ids |> List.map (defineId innerEnv FuncArgId) 
        CollectVariables(innerEnv).VisitStatement(b)
        let body = b |> transformStatement innerEnv |> flattenFuncBody
        let f = J.Function(id, args, flattenJS (innerEnv.Declarations @ body))
        if env.InFuncScope then
            f
        else
            env.FuncDecls.Add f 
            J.Empty
    | While(a, b) -> 
        withFuncDecls <| fun () -> 
            J.While (trE a, trS b)
    | DoWhile(a, b) ->
        withFuncDecls <| fun () -> 
            J.Do (trS a, trE b)
    | For(a, b, c, d) -> 
        withFuncDecls <| fun () -> 
            J.For(Option.map trE a, Option.map trE b, Option.map trE c, trS d)
    | Switch(a, b) -> 
        withFuncDecls <| fun () ->
            J.Switch(trE a, 
                b |> List.map (fun (l, s) -> 
                    match l with 
                    | Some l -> J.SwitchElement.Case (trE l, flatten [ s ]) 
                    | _ -> J.SwitchElement.Default (flatten [ s ])
                )
            )
    | Throw (IgnoreSourcePos.Sequential s) -> block (sequential s Throw |> List.map trS)
    | Throw(a) -> J.Throw (trE a)
    | Labeled(a, b) -> 
        withFuncDecls <| fun () -> 
            J.Labelled(a.Name.Value, trS b)
    | TryWith(a, b, c) -> 
        withFuncDecls <| fun () ->
            J.TryWith(trS a, defineId env VarDeclId (match b with Some b -> b | _ -> Id.New()), trS c, None)
    | TryFinally(a, b) ->
        withFuncDecls <| fun () ->
            J.TryFinally(trS a, trS b)
    | ForIn(a, b, c) -> 
        withFuncDecls <| fun () ->
            J.ForVarIn(defineId env VarDeclId a, None, trE b, trS c)
    | ImportAll (a, b) ->
        J.ImportAll(a |> Option.map (defineId env VarDeclId), b)
    | Export a ->
        J.Export (trS a)
    | Declare a ->
        J.Declare (trS a)
    | Namespace (a, b) ->
        let innerEnv = env.NewInner(true)
        J.Namespace (a, List.map (transformStatement innerEnv) b)
    | Class (a, b, c, d) ->
        let isAbstract =
            d |> List.exists (function
                | ClassMethod (_, _, _, None) -> true
                | _ -> false
            )
        J.Class(a, isAbstract, Option.map trE b, List.map trE c, List.map (transformMember env) d)
    | _ -> 
        invalidForm (GetUnionCaseName statement)

and transformMember (env: Environment) (mem: Statement) : J.Member =
    let inline trE x = transformExpr env x
    let inline trS x = transformStatement env x
    match mem with
    | ClassMethod (a, b, c, d) ->
        let innerEnv = env.NewInner(false)
        let args = c |> List.map (defineId innerEnv FuncArgId) 
        let body = 
            d |> Option.map (fun b -> 
                CollectVariables(innerEnv).VisitStatement(b)
                b |> transformStatement innerEnv |> flattenFuncBody
            )
        J.Method(a, b, args, body |> Option.map (fun b -> flattenJS (innerEnv.Declarations @ b)))   
    | ClassConstructor (a, b) ->
        let innerEnv = env.NewInner(false)
        let args = a |> List.map (defineId innerEnv FuncArgId) 
        let body = 
            b |> Option.map (fun b -> 
                CollectVariables(innerEnv).VisitStatement(b)
                b |> transformStatement innerEnv |> flattenFuncBody
            )
        J.Constructor(args, body |> Option.map (fun b -> flattenJS (innerEnv.Declarations @ b)))   
    | ClassProperty (a, b) ->
        J.Property (a, b)
    | _ -> 
        invalidForm (GetUnionCaseName mem)

let transformProgram pref statements =
    let env = Environment.New(pref)
    let collect = CollectVariables(env)
    statements |> List.iter collect.VisitStatement
    J.Ignore (J.Constant (J.String "use strict")) ::
    (statements |> List.map (transformStatement env) |> flattenJS)
