// Learn more about F# at http://fsharp.org

open System
open Nest

module Domain =
    type SomeType = { yello: string; }

open Domain

module Elasticsearch =
    let func (f:('a->'b)) =
        new Func<'a,'b>(f)

    let connect uri =
        new ElasticClient(new ConnectionSettings(Uri(uri)))

    let client =
        "http://localhost:9200"
        |> connect

    let querySelector (sd : SearchDescriptor<'T>) =
        sd.
            From(new System.Nullable<int>(0)).
            Size(new System.Nullable<int>(50)) :> ISearchRequest

    let t () : ISearchResponse<'SomeType> = client.Search<'SomeType>(func(querySelector))

    // --* Active Patterns *--

    let (|SearchSuccess|SearchFail|) (res : ISearchResponse<'T>) =
        match res.IsValid with
        | true -> SearchSuccess
        | false -> SearchFail

    let (|IndexSuccess|IndexFail|) (res : IIndexResponse) =
        match res.IsValid with
        | true -> IndexSuccess
        | false -> IndexFail

    // --* Types *--

    type ElasticClientResult<'T when 'T : not struct> =
        | Search of ISearchResponse<'T>
        | Index of IIndexResponse
        | Failure

    type ElasticActionResult<'T when 'T : not struct> =
        | Searched of 'T
        | Indexed of 'T
        | NoResult

    // --* ComputationExpression *--

    type SearchBuilder() =
        member this.Bind(m, f) =
            match m with
            | SearchSuccess -> f m
            | SearchFail -> NoResult

        member this.Return(x : ISearchResponse<'T>) = Searched x.Documents

        member this.ReturnFrom(x) =
            match x with
            | SearchSuccess -> Searched x.Documents
            | SearchFail -> NoResult
        
        member this.Zero() = NoResult

    type IndexBuilder() =
        member this.Bind(m, f) =
            match m with
            | IndexSuccess -> f m
            | IndexFail -> NoResult

        member this.Return(x : IIndexResponse) = Indexed x.Id

        member this.ReturnFrom(x) =
            match x with
            | IndexSuccess -> Indexed x
            | IndexFail -> NoResult

        member this.Zero() = NoResult

    type WorkflowBuilder() =
        member this.Bind(m, f) =
            match m with
            | Searched s -> f s
            | Indexed i -> Some i
            | NoResult -> None

        member this.Return(x) = Some x

        member this.ReturnFrom(x) =
            match x with
            | Searched s -> Some s
            | Indexed i -> Some i
            | NoResult -> None

        member this.Zero() = None

    let elastic = new WorkflowBuilder()

    let search = new SearchBuilder()

open Elasticsearch


[<EntryPoint>]
let main argv =
    printfn "Hello World from F#!"


    printfn "%A" search
    let workflow =
        elastic {
            return! search { return! t() } 
        }

    0 // return an integer exit code
